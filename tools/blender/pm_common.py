"""Shared helpers for the Poing Mort Blender scripts (Blender 4.5, run as the `bpy` Python module).

These scripts are the reproducible source of the 3D assets in Assets/PoingMort/Art/Models.
They run headless: `python tools/blender/<script>.py` with the `bpy` wheel installed
(pip install bpy==4.5.*) and, for the characters, MPFB2 checked out next to the repo.
"""
import math
import os
import sys

import bpy
import bmesh
import mathutils
from mathutils import Vector, Matrix, Quaternion, Euler

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(REPO, "Assets", "PoingMort", "Art")
MODELS = os.path.join(ART, "Models")
TEXTURES = os.path.join(ART, "Textures")

MPFB_SRC = os.environ.get("MPFB_SRC", "/home/user/makehumancommunity/mpfb2/src/mpfb")
MAKEHUMAN_DATA = os.environ.get("MAKEHUMAN_DATA", "/home/user/makehumancommunity/makehuman/makehuman/data")


# --------------------------------------------------------------------------- scene

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in list(bpy.data.collections):
        bpy.data.collections.remove(coll)


def link(obj, collection=None):
    (collection or bpy.context.scene.collection).objects.link(obj)
    return obj


def set_active(obj):
    bpy.context.view_layer.update()
    for o in list(bpy.context.view_layer.objects):
        if o is not None:
            o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def ctx(obj, selected=None):
    """Context override for operators run in background mode."""
    sel = selected if selected is not None else [obj]
    return bpy.context.temp_override(object=obj, active_object=obj, selected_objects=sel,
                                     selected_editable_objects=sel)


# --------------------------------------------------------------------------- MPFB

def load_mpfb():
    """Registers MPFB2 as a Blender extension (it refuses to load as a plain module)."""
    import addon_utils
    ext_root = bpy.utils.user_resource('EXTENSIONS', path="user_default", create=True)
    link_path = os.path.join(ext_root, "mpfb")
    if not os.path.exists(link_path):
        os.symlink(MPFB_SRC, link_path)
    try:
        bpy.ops.extensions.repo_refresh_all()
    except Exception:
        pass
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True, handle_error=None)


def mpfb(path, key):
    import importlib
    for name in list(sys.modules):
        if name.endswith(path):
            return getattr(importlib.import_module(name), key)
    raise ImportError(path)


# --------------------------------------------------------------------------- materials

def principled(name, color, roughness=0.8, metallic=0.0, emission=None, emission_strength=0.0, alpha=1.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color[:3], 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission[:3], 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        mat.blend_method = 'BLEND' if hasattr(mat, "blend_method") else None
    mat.diffuse_color = (*color[:3], 1.0)
    return mat


def image_material(name, image_path, roughness=0.8, tint=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(image_path, check_existing=True)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def srgb(hex_or_tuple):
    """Converts an sRGB colour (hex string or 0-1 tuple) to linear for Blender."""
    if isinstance(hex_or_tuple, str):
        h = hex_or_tuple.lstrip("#")
        c = tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    else:
        c = hex_or_tuple
    def lin(v):
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return tuple(lin(v) for v in c)


# --------------------------------------------------------------------------- render previews

def setup_preview(res=(900, 900), samples=24, background=(0.30, 0.29, 0.33), strength=0.75, engine="BLENDER_EEVEE_NEXT"):
    sc = bpy.context.scene
    try:
        sc.render.engine = engine
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    try:
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    w = sc.world
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (*background, 1)
    bg.inputs[1].default_value = strength


def add_sun(name="Sun", rotation_deg=(55, 0, 35), energy=3.2, color=(1.0, 0.82, 0.66), angle_deg=3.0):
    light = bpy.data.lights.new(name, "SUN")
    light.energy = energy
    light.color = color
    light.angle = math.radians(angle_deg)
    obj = bpy.data.objects.new(name, light)
    link(obj)
    obj.rotation_euler = tuple(math.radians(a) for a in rotation_deg)
    return obj


def add_area(name, location, target, energy=200, size=2.0, color=(1, 1, 1)):
    light = bpy.data.lights.new(name, "AREA")
    light.energy = energy
    light.size = size
    light.color = color
    obj = bpy.data.objects.new(name, light)
    link(obj)
    obj.location = location
    d = Vector(target) - Vector(location)
    obj.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    return obj


def add_camera(location, target, lens=50, name="Cam"):
    cam = bpy.data.cameras.new(name)
    cam.lens = lens
    cam.clip_end = 1000
    obj = bpy.data.objects.new(name, cam)
    link(obj)
    obj.location = location
    d = Vector(target) - Vector(location)
    obj.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.camera = obj
    return obj


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("render:", path, flush=True)


def ground_plane(size=20, color=(0.35, 0.33, 0.32)):
    bpy.ops.mesh.primitive_plane_add(size=size)
    plane = bpy.context.active_object
    plane.name = "PreviewGround"
    plane.data.materials.append(principled("PreviewGround", color, 0.9))
    return plane
