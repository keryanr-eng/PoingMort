"""Builds a Poing Mort character from the MakeHuman base mesh (MPFB2, CC0).

Stages (each saves a .blend checkpoint in the work directory):
  body     : human with macros, Mixamo-compatible rig, eyes, helpers removed, T-pose rest
  outfit   : clothes, shoes, hair, eyebrows derived from the body, covered skin removed
  textures : skin albedo composed from the MakeHuman UV masks

Usage:
  python tools/blender/build_character.py <player|opponent> <stage> [--preview]
"""
import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import bmesh
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

import pm_common as pm
import pm_cloth as pc

WORK = os.environ.get("PM_WORK", "/tmp/pm_work")
os.makedirs(WORK, exist_ok=True)

PREFIX = "mixamorig:"

CHARACTERS = {
    "player": {
        "display": "Le Joueur",
        "macros": {"gender": 1.0, "age": 0.44, "muscle": 0.68, "weight": 0.45, "proportions": 0.62, "height": 0.56,
                   "cupsize": 0.5, "firmness": 0.5, "race": {"african": 0.86, "caucasian": 0.08, "asian": 0.06}},
        "skin": "#6b4430",
        "outfit": "hoodie",
        "hair": "twists",
        "colors": {"top": "#1f1f22", "top_lining": "#d9d6d0", "pants": "#2a2a2c", "shoe": "#eeece8", "sole": "#f4f2ee",
                   "shoe_accent": "#b9b6b0", "hair": "#16110e"},
        "seed": 7,
    },
    "opponent": {
        "display": "Le Rival",
        "macros": {"gender": 1.0, "age": 0.62, "muscle": 0.92, "weight": 0.68, "proportions": 0.45, "height": 0.6,
                   "cupsize": 0.5, "firmness": 0.5, "race": {"african": 0.2, "caucasian": 0.45, "asian": 0.35}},
        "skin": "#a8765a",
        "outfit": "tank",
        "hair": "buzz",
        "colors": {"top": "#e8e6e1", "top_lining": "#e8e6e1", "pants": "#2c3442", "shoe": "#2b2b2d", "sole": "#d8d4cc",
                   "shoe_accent": "#5a5a5e", "hair": "#1d1915"},
        "seed": 21,
    },
}


def checkpoint(name, stage):
    return os.path.join(WORK, f"{name}_{stage}.blend")


def bone(rig, short):
    return rig.pose.bones.get(PREFIX + short) or rig.pose.bones.get(short)


# ============================================================================ stage: body

def stage_body(name, cfg):
    pm.reset_scene()
    pm.load_mpfb()
    HumanService = pm.mpfb("mpfb.services.humanservice", "HumanService")

    human = HumanService.create_human(macro_detail_dict=cfg["macros"])
    human.name = "Body"
    rig = HumanService.add_builtin_rig(human, "mixamo_unity")
    rig.name = "Armature"
    rig.data.name = "Armature"

    eyes_path = os.path.join(pm.MAKEHUMAN_DATA, "eyes", "low-poly", "low-poly.mhclo")
    eyes = None
    try:
        eyes = HumanService.add_mhclo_asset(eyes_path, human, asset_type="Eyes", subdiv_levels=0, material_type="GAMEENGINE")
    except Exception as exc:  # keep going: eyes are re-created below if needed
        print("eyes via MPFB failed:", exc, flush=True)
    if eyes is not None:
        eyes.name = "Eyes"

    bake_shape_keys(human)
    remove_helpers(human)
    for obj in [human] + ([eyes] if eyes else []):
        for mod in list(obj.modifiers):
            if mod.type in ("MASK", "SUBSURF"):
                obj.modifiers.remove(mod)

    to_t_pose(rig)
    bpy.ops.wm.save_as_mainfile(filepath=checkpoint(name, "body"))
    print("BODY OK", len(human.data.vertices), "verts", flush=True)
    return rig, human, eyes


def bake_shape_keys(obj):
    if obj.data.shape_keys is None:
        return
    pm.set_active(obj)
    with pm.ctx(obj):
        bpy.ops.object.shape_key_remove(all=True, apply_mix=True)


def remove_helpers(obj):
    group = obj.vertex_groups.get("body")
    if group is None:
        print("no 'body' group: helpers kept", flush=True)
        return
    gi = group.index
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    deform = bm.verts.layers.deform.active
    doomed = [v for v in bm.verts if gi not in v[deform] or v[deform][gi] < 0.5]
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def armature_meshes(rig):
    return [o for o in bpy.data.objects if o.type == "MESH" and any(m.type == "ARMATURE" and m.object == rig for m in o.modifiers)]


def to_t_pose(rig):
    """Rotates the arms to a horizontal T-pose, bakes it into the meshes and makes it the rest pose."""
    pm.set_active(rig)
    bpy.ops.object.mode_set(mode="POSE")

    def aim(short, direction):
        pb = bone(rig, short)
        if pb is None:
            return
        bpy.context.view_layer.update()
        head = pb.head.copy()
        cur = (pb.tail - pb.head).normalized()
        q = cur.rotation_difference(direction.normalized())
        pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
        bpy.context.view_layer.update()

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        aim(side + "Arm", Vector((sign, 0.0, 0.0)))
        aim(side + "ForeArm", Vector((sign, 0.0, 0.0)))
        aim(side + "Hand", Vector((sign, 0.0, -0.05)))
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")

    meshes = armature_meshes(rig)
    for obj in meshes:
        mod = next(m for m in obj.modifiers if m.type == "ARMATURE")
        pm.set_active(obj)
        with pm.ctx(obj):
            bpy.ops.object.modifier_apply(modifier=mod.name)

    pm.set_active(rig)
    bpy.ops.object.mode_set(mode="POSE")
    with pm.ctx(rig):
        bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    for obj in meshes:
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        # Keep the armature modifier first (before any other modifier).
        while obj.modifiers.find(mod.name) > 0:
            with pm.ctx(obj):
                bpy.ops.object.modifier_move_up(modifier=mod.name)


# ============================================================================ stage: outfit

def landmarks(rig):
    def head(short):
        b = rig.data.bones.get(PREFIX + short)
        return rig.matrix_world @ b.head_local
    return {
        "hips": head("Hips"), "spine": head("Spine"), "spine2": head("Spine2"), "neck": head("Neck"),
        "head": head("Head"), "arm": head("LeftArm"), "elbow": head("LeftForeArm"), "wrist": head("LeftHand"),
        "upleg": head("LeftUpLeg"), "knee": head("LeftLeg"), "ankle": head("LeftFoot"), "toe": head("LeftToeBase"),
        "eyeL": head("LeftEye"), "eyeR": head("RightEye"),
    }


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))) if b != a else 0.0
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def make_materials(cfg):
    c = cfg["colors"]
    return {
        "top": pm.principled("PM_Top", pm.srgb(c["top"]), 0.92),
        "lining": pm.principled("PM_TopLining", pm.srgb(c["top_lining"]), 0.95),
        "pants": pm.principled("PM_Pants", pm.srgb(c["pants"]), 0.9),
        "shoe": pm.principled("PM_Shoe", pm.srgb(c["shoe"]), 0.6),
        "sole": pm.principled("PM_Sole", pm.srgb(c["sole"]), 0.75),
        "shoe_accent": pm.principled("PM_ShoeAccent", pm.srgb(c["shoe_accent"]), 0.7),
        "hair": pm.principled("PM_Hair", pm.srgb(c["hair"]), 0.65),
        "eyes": pm.principled("PM_Eyes", pm.srgb("#3a2618"), 0.25),
    }


def stage_outfit(name, cfg):
    pm.load_mpfb()
    bpy.ops.wm.open_mainfile(filepath=checkpoint(name, "body"))
    rig = bpy.data.objects["Armature"]
    body = bpy.data.objects["Body"]
    eyes = bpy.data.objects.get("Eyes")
    coll = body.users_collection[0]
    L = landmarks(rig)
    dom = pc.dominant_bones(body)
    tree, tree_bm = pc.body_bvh(body)
    mats = make_materials(cfg)

    pieces = []
    if cfg["outfit"] == "hoodie":
        pieces += make_hoodie(body, dom, tree, L, mats, coll, rig)
    else:
        pieces += make_tank(body, dom, tree, L, mats, coll, rig)
    pieces.append(make_pants(body, dom, tree, L, mats, coll, rig))
    pieces += make_shoes(body, dom, L, mats, coll, rig)
    pieces += make_hair(body, dom, tree, L, mats, coll, rig, cfg)
    pieces.append(make_eyebrows(body, tree, L, mats, coll, rig))
    cleanup_body(body, dom, L, cfg)
    if eyes is not None:
        eyes.data.materials.clear()
        eyes.data.materials.append(mats["eyes"])
    for obj in pieces:
        pc.shade_smooth(obj)
    bpy.ops.wm.save_as_mainfile(filepath=checkpoint(name, "outfit"))
    print("OUTFIT OK", [(o.name, len(o.data.vertices)) for o in pieces], flush=True)


ARM_BONES = {"LeftArm", "RightArm", "LeftForeArm", "RightForeArm"}
TORSO_BONES = {"Hips", "Spine", "Spine1", "Spine2", "LeftShoulder", "RightShoulder", "LeftBreast", "RightBreast", "Neck"}
LEG_BONES = {"LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg", "LeftButtock", "RightButtock"}


def make_hoodie(body, dom, tree, L, mats, coll, rig):
    hem_z = L["hips"].z - 0.02
    neck_z = L["neck"].z
    wrist_x = L["wrist"].x
    shoulder_x = L["arm"].x

    def region(i, co):
        b = dom[i][0]
        if b in ARM_BONES:
            return abs(co.x) < wrist_x - 0.012
        if b in TORSO_BONES or b in ("LeftButtock", "RightButtock", "LeftUpLeg", "RightUpLeg"):
            if b == "Neck":
                return co.z < neck_z + 0.012
            return hem_z < co.z < neck_z + 0.012
        return False

    hoodie = pc.duplicate(body, "Hoodie")
    pc.keep_faces(hoodie, region)

    def offset(co, n):
        ax = abs(co.x)
        if ax > shoulder_x + 0.03:
            t = (ax - shoulder_x) / (wrist_x - shoulder_x)
            o = lerp(0.022, 0.026, t)
            return lerp(o, 0.010, smoothstep(wrist_x - 0.09, wrist_x - 0.02, ax))
        loose = smoothstep(1.32, 1.0, co.z)
        o = 0.024 + 0.024 * loose
        return lerp(o, 0.012, smoothstep(neck_z - 0.08, neck_z - 0.02, co.z))

    # Clean openings before inflating: flat hem, round neckline lower at the front, straight cuffs.
    neck_c = L["neck"]
    def collar_z(co):
        front = smoothstep(neck_c.y + 0.02, neck_c.y - 0.06, co.y)
        return neck_z - 0.012 - 0.035 * front
    pc.snap_boundary(hoodie, lambda co: co.z < hem_z + 0.05 and abs(co.x) < shoulder_x, lambda co: Vector((co.x, co.y, hem_z)))
    pc.snap_boundary(hoodie, lambda co: co.z > neck_z - 0.08 and abs(co.x) < 0.11, lambda co: Vector((co.x, co.y, collar_z(co))))
    pc.snap_boundary(hoodie, lambda co: abs(co.x) > wrist_x - 0.05, lambda co: Vector((math.copysign(wrist_x - 0.012, co.x), co.y, co.z)))
    pc.relax_boundary(hoodie, lambda co: True, iterations=6, factor=0.5)
    pc.inflate(hoodie, offset)
    pc.smooth(hoodie, iterations=14, factor=0.5)
    pc.push_out(hoodie, tree, lambda co: 0.012 if abs(co.x) < shoulder_x + 0.03 else 0.008)
    # The hem bridges the hips and buttocks like real fabric instead of following the body.
    ext = pc.extents_at(body, hem_z - 0.06, hem_z + 0.08)
    if ext:
        x0, x1, y0, y1 = ext
        centre_h = Vector(((x0 + x1) / 2, (y0 + y1) / 2, 0))
        pc.drape_ellipse(hoodie, centre_h, (x1 - x0) / 2 + 0.03, (y1 - y0) / 2 + 0.032, hem_z + 0.02, blend=0.08)
        pc.smooth(hoodie, iterations=8, factor=0.4, weight_fn=lambda co: 1.0 if co.z < hem_z + 0.12 else 0.0)

    # Hem: long hoodie down to the top of the thighs, slightly tighter ribbing.
    centre_hem = Vector((0.0, L["hips"].y, 0.0))
    def inward(co, k):
        d = Vector((co.x - centre_hem.x, co.y - centre_hem.y, 0.0))
        return -d.normalized() * k if d.length > 1e-4 else Vector()
    pc.extrude_loop(hoodie, lambda co: co.z < hem_z + 0.03 and abs(co.x) < shoulder_x,
                    lambda co: Vector((0, 0, -0.04)) + inward(co, 0.002), steps=2)
    pc.extrude_loop(hoodie, lambda co: co.z < hem_z - 0.05 and abs(co.x) < shoulder_x,
                    lambda co: Vector((0, 0, -0.025)) + inward(co, 0.006), steps=1)
    # Cuffs
    def cuff_inward(co, k):
        d = Vector((0.0, co.y - L["wrist"].y, co.z - L["wrist"].z))
        return -d.normalized() * k if d.length > 1e-4 else Vector()
    pc.extrude_loop(hoodie, lambda co: abs(co.x) > wrist_x - 0.04,
                    lambda co: Vector((math.copysign(0.018, co.x), 0, 0)) + cuff_inward(co, 0.004), steps=1)
    # Collar rib
    pc.extrude_loop(hoodie, lambda co: co.z > neck_z - 0.07 and abs(co.x) < 0.12,
                    lambda co: Vector((0, 0, 0.01)) + inward(co, 0.003), steps=1)

    mod = pc.solidify(hoodie, 0.006)
    mod.use_even_offset = False
    move_first(hoodie, mod)
    pc.apply_modifier(hoodie, mod.name)
    hoodie.data.materials.clear()
    pc.assign_material(hoodie, mats["top"])
    pc.transfer_weights(hoodie, body)

    hood = make_hood(L, mats, coll, rig, body)
    pocket = make_pocket(hoodie, L, mats, coll, rig, body, hem_z)
    strings = make_drawstrings(L, mats, coll, rig, body, tree)
    return [hoodie, hood, pocket, strings]


def move_first(obj, mod):
    while obj.modifiers.find(mod.name) > 0:
        with pm.ctx(obj):
            bpy.ops.object.modifier_move_up(modifier=mod.name)


def make_hood(L, mats, coll, rig, body):
    """Hood worn down: a thick roll around the back of the neck with a light lining."""
    neck = L["neck"]
    centre = Vector((0.0, neck.y + 0.012, neck.z - 0.035))
    bm = bmesh.new()
    sides = 14
    steps = 26
    rings = []
    for i in range(steps + 1):
        u = i / steps                      # 0 = left front, 1 = right front
        a = math.radians(lerp(-150, 150, u))  # around the neck, 0 = back
        back = max(0.0, math.cos(a))       # 1 at the back
        r = 0.108 + 0.03 * back
        p = centre + Vector((math.sin(a) * r * 1.05, math.cos(a) * r, -0.055 * back ** 2))
        # Cross-section grows at the back where the hood fabric bunches on the shoulders.
        sw = lerp(0.017, 0.05, back ** 1.2)
        sh = lerp(0.022, 0.07, back ** 1.2)
        tangent = Vector((math.cos(a) * 1.05, -math.sin(a), 0.0)).normalized()
        outward = Vector((math.sin(a), math.cos(a), 0.0)).normalized()
        upv = tangent.cross(outward).normalized()
        if upv.z < 0:
            upv = -upv
        ring = []
        for s in range(sides):
            t = 2 * math.pi * s / sides
            ring.append(bm.verts.new(p + outward * (math.cos(t) * sw) + upv * (math.sin(t) * sh) - Vector((0, 0, 0.006 * back * math.sin(t) ** 2))))
        rings.append(ring)
    for i in range(steps):
        for s in range(sides):
            bm.faces.new((rings[i][s], rings[i][(s + 1) % sides], rings[i + 1][(s + 1) % sides], rings[i + 1][s]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bm.normal_update()
    hood = pc.new_mesh_object("Hood", bm, coll)
    # "Hoodie sombre à capuche claire": the hood itself is light, like on the art board.
    hood.data.materials.append(mats["lining"])
    pc.transfer_weights(hood, body)
    pc.add_armature(hood, rig)
    return hood


def make_pocket(hoodie, L, mats, coll, rig, body, hem_z):
    """Kangaroo pocket: a patch projected on the front of the hoodie."""
    tree = pc.tree_from_object(hoodie)
    zs = [hem_z - 0.07 + 0.19 * k / 6 for k in range(7)]
    xs = []
    for k, z in enumerate(zs):
        half = lerp(0.135, 0.095, k / 6)
        xs.append([-half + 2 * half * j / 10 for j in range(11)])
    bm = pc.surface_patch(tree, xs, zs, -0.6, Vector((0, 1, 0)), 0.004)
    bm.normal_update()
    pocket = pc.new_mesh_object("HoodiePocket", bm, coll)
    pocket.data.materials.append(mats["top"])
    mod = pc.solidify(pocket, 0.004)
    mod.use_even_offset = False
    pc.apply_modifier(pocket, mod.name)
    pc.transfer_weights(pocket, body)
    pc.add_armature(pocket, rig)
    return pocket


def make_drawstrings(L, mats, coll, rig, body, tree):
    bm = bmesh.new()
    neck = L["neck"]
    for sx in (-1, 1):
        x = sx * 0.034
        hit = tree.ray_cast(Vector((x, -0.5, neck.z - 0.03)), Vector((0, 1, 0)))
        y = (hit[0].y if hit[0] is not None else neck.y - 0.08) - 0.03
        top = Vector((x, y, neck.z - 0.03))
        path = [top + Vector((sx * 0.002 * i, -0.004 * i, -0.04 * i)) for i in range(5)]
        pc.tapered_tube(bm, path, [0.0035] * 4 + [0.0045], sides=6)
    obj = pc.new_mesh_object("Drawstrings", bm, coll)
    obj.data.materials.append(mats["lining"])
    pc.set_weights_single(obj, "Spine2")
    pc.add_armature(obj, rig)
    return obj


def make_tank(body, dom, tree, L, mats, coll, rig):
    hem_z = L["hips"].z - 0.03
    neck_z = L["neck"].z
    armpit_z = L["arm"].z - 0.15

    def region(i, co):
        b = dom[i][0]
        if b not in TORSO_BONES and b not in ("LeftButtock", "RightButtock", "LeftUpLeg", "RightUpLeg"):
            return False
        if b == "Neck" or not (hem_z - 0.01 < co.z < neck_z):
            return False
        ax = abs(co.x)
        front = co.y < L["neck"].y
        if co.z > armpit_z and ax > 0.118:
            return False                      # arm holes
        top = (neck_z - 0.135) if front else (neck_z - 0.085)
        if co.z > top:
            return 0.058 < ax < 0.112           # straps over the shoulders
        return True

    tank = pc.duplicate(body, "TankTop")
    vbone = {i: dom[i][0] for i in range(len(dom))}
    def face_region(c, f):
        bones = [vbone[v.index] for v in f.verts]
        b = max(set(bones), key=bones.count)
        return region_bone(b, c)
    def region_bone(b, co):
        if b not in TORSO_BONES and b not in ("LeftButtock", "RightButtock", "LeftUpLeg", "RightUpLeg"):
            return False
        if b == "Neck" or not (hem_z - 0.01 < co.z < neck_z):
            return False
        ax = abs(co.x)
        front = co.y < L["neck"].y
        if co.z > armpit_z and ax > 0.12:
            return False
        top = (neck_z - 0.13) if front else (neck_z - 0.085)
        if co.z > top:
            return 0.05 < ax < 0.118
        return True
    pc.keep_faces_by_center(tank, face_region)
    pc.snap_boundary(tank, lambda co: co.z < hem_z + 0.04, lambda co: Vector((co.x, co.y, hem_z)))
    pc.relax_boundary(tank, lambda co: co.z > hem_z + 0.02, iterations=10, factor=0.6)
    # Loose enough at the waist to stay over the jeans' waistband.
    pc.inflate(tank, lambda co, n: 0.006 + 0.018 * smoothstep(1.16, 1.02, co.z))
    pc.smooth(tank, iterations=6, factor=0.4)
    pc.push_out(tank, tree, lambda co: 0.004)
    ext = pc.extents_at(body, hem_z - 0.08, hem_z + 0.06)
    if ext:
        x0, x1, y0, y1 = ext
        pc.drape_ellipse(tank, Vector(((x0 + x1) / 2, (y0 + y1) / 2, 0)), (x1 - x0) / 2 + 0.024, (y1 - y0) / 2 + 0.026, hem_z + 0.03, blend=0.06)
    pc.extrude_loop(tank, lambda co: co.z < hem_z + 0.02, lambda co: Vector((0, 0, -0.04)), steps=2)
    mod = pc.solidify(tank, 0.004)
    mod.use_even_offset = False
    move_first(tank, mod)
    pc.apply_modifier(tank, mod.name)
    tank.data.materials.clear()
    pc.assign_material(tank, mats["top"])
    pc.transfer_weights(tank, body)
    return [tank]


def make_pants(body, dom, tree, L, mats, coll, rig):
    waist_z = L["hips"].z + 0.075
    ankle_z = L["ankle"].z
    hip_joint_z = L["upleg"].z
    knee_z = L["knee"].z

    def region(i, co):
        b = dom[i][0]
        if b in LEG_BONES or b in ("Hips", "Spine"):
            return ankle_z + 0.035 < co.z < waist_z
        return False

    pants = pc.duplicate(body, "Pants")
    pc.keep_faces(pants, region)

    def offset(co, n):
        z = co.z
        crotch = smoothstep(0.075, 0.02, abs(co.x)) * smoothstep(hip_joint_z - 0.02, hip_joint_z - 0.12, z)
        o = 0.012 + 0.022 * smoothstep(waist_z - 0.02, hip_joint_z - 0.08, z)   # waist tight, thighs loose
        o += 0.008 * smoothstep(knee_z + 0.1, knee_z - 0.15, z)                # wide below the knee
        o -= 0.012 * crotch
        return o

    pc.snap_boundary(pants, lambda co: co.z > waist_z - 0.04, lambda co: Vector((co.x, co.y, waist_z)))
    pc.snap_boundary(pants, lambda co: co.z < ankle_z + 0.07, lambda co: Vector((co.x, co.y, ankle_z + 0.04)))
    pc.relax_boundary(pants, lambda co: True, iterations=6, factor=0.5)
    pc.inflate(pants, offset)
    pc.smooth(pants, iterations=18, factor=0.5)
    pc.push_out(pants, tree, lambda co: 0.007 if co.z > L["hips"].z else 0.012)

    # Bunched hems over the shoes.
    def radial(co, k):
        cx = math.copysign(L["ankle"].x, co.x)
        d = Vector((co.x - cx, co.y - L["ankle"].y, 0.0))
        return d.normalized() * k if d.length > 1e-4 else Vector()
    pc.extrude_loop(pants, lambda co: co.z < ankle_z + 0.07, lambda co: Vector((0, 0, -0.025)) + radial(co, 0.006), steps=2)
    pc.extrude_loop(pants, lambda co: co.z > waist_z - 0.012, lambda co: Vector((0, 0, 0.012)), steps=1)

    mod = pc.solidify(pants, 0.005)
    move_first(pants, mod)
    pc.apply_modifier(pants, mod.name)
    pants.data.materials.clear()
    pc.assign_material(pants, mats["pants"])
    add_cargo_pockets(pants, L, tree)
    pc.transfer_weights(pants, body)
    return pants


def add_cargo_pockets(pants, L, tree):
    bm = bmesh.new()
    bm.from_mesh(pants.data)
    for sx in (-1, 1):
        z = L["knee"].z + 0.15
        hit = tree.ray_cast(Vector((sx * 0.6, L["knee"].y, z)), Vector((-sx, 0, 0)))
        if hit[0] is None:
            continue
        x = hit[0].x + sx * 0.012
        centre = Vector((x, L["knee"].y + 0.005, z))
        res = bmesh.ops.create_cube(bm, size=1.0)
        verts = res["verts"]
        bmesh.ops.scale(bm, vec=Vector((0.012, 0.13, 0.15)), verts=verts)
        bmesh.ops.translate(bm, vec=centre, verts=verts)
        flap = bmesh.ops.create_cube(bm, size=1.0)["verts"]
        bmesh.ops.scale(bm, vec=Vector((0.016, 0.14, 0.032)), verts=flap)
        bmesh.ops.translate(bm, vec=centre + Vector((sx * 0.003, 0, 0.068)), verts=flap)
    bm.to_mesh(pants.data)
    bm.free()
    pants.data.update()


def shoe_profile(s):
    """Height (m) of the upper and width factor along the shoe, s = 0 heel .. 1 toe."""
    h = lerp(0.108, 0.088, smoothstep(0.12, 0.36, s))
    h = lerp(h, 0.058, smoothstep(0.42, 0.8, s))
    h = lerp(h, 0.036, smoothstep(0.86, 1.0, s))
    w = 0.80 + 0.20 * math.sin(min(1.0, s / 0.64) * math.pi / 2)
    w *= 1.0 - 0.5 * smoothstep(0.84, 1.0, s) ** 1.7
    return h, w


def loft(bm, sections, ring_n, section_fn, cap=True):
    rings = []
    for i in range(sections + 1):
        s = i / sections
        rings.append([bm.verts.new(p) for p in section_fn(s, ring_n)])
    for i in range(sections):
        for k in range(ring_n):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % ring_n], rings[i + 1][(k + 1) % ring_n], rings[i + 1][k]))
    if cap:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    return rings


def make_shoes(body, dom, L, mats, coll, rig):
    shoes = []
    for side, sx in (("Left", 1), ("Right", -1)):
        pts = [v.co for i, v in enumerate(body.data.vertices)
               if dom[i][0] in (side + "Foot", side + "ToeBase") and v.co.z < L["ankle"].z + 0.06 and v.co.x * sx > 0]
        if not pts:
            continue
        xs = [p.x for p in pts]; ys = [p.y for p in pts]
        cx = (min(xs) + max(xs)) / 2
        half_w = (max(xs) - min(xs)) / 2 + 0.012
        heel_y, toe_y = max(ys) + 0.014, min(ys) - 0.018
        sole_h = 0.026

        def sole_section(s, n):
            y = lerp(heel_y + 0.004, toe_y - 0.004, s)
            _, w = shoe_profile(s)
            w = half_w * w * 1.05
            lift = 0.012 * smoothstep(0.82, 1.0, s) ** 2      # toe spring
            th = lerp(sole_h, sole_h * 0.8, s)
            out = []
            for k in range(n):
                t = 2 * math.pi * k / n
                c, sn = math.cos(t), math.sin(t)
                px = cx + math.copysign(abs(c) ** 0.35, c) * w
                pz = lift + (0.5 + 0.5 * math.copysign(abs(sn) ** 0.35, sn)) * th
                out.append(Vector((px, y, pz)))
            return out

        def upper_section(s, n):
            y = lerp(heel_y, toe_y, s)
            h, w = shoe_profile(s)
            w = half_w * w
            lift = 0.012 * smoothstep(0.82, 1.0, s) ** 2
            base = sole_h * 0.75 + lift
            out = []
            for k in range(n):
                t = math.pi * k / (n - 1)          # half ring: from the inner side over the top to the outer side
                c, sn = math.cos(t), math.sin(t)
                px = cx + c * w
                pz = base + (sn ** 0.7) * (h - base)
                out.append(Vector((px, y, pz)))
            return out

        bm = bmesh.new()
        loft(bm, 24, 18, sole_section)
        n_up = 13
        rings = []
        for i in range(25):
            rings.append([bm.verts.new(p) for p in upper_section(i / 24, n_up)])
        for i in range(24):
            for k in range(n_up - 1):
                bm.faces.new((rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]))
        # Close heel and toe of the upper.
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        bm.normal_update()
        shoe = pc.new_mesh_object(side + "Shoe", bm, coll)
        shoe.data.materials.append(mats["shoe"])
        shoe.data.materials.append(mats["sole"])
        shoe.data.materials.append(mats["shoe_accent"])
        for p in shoe.data.polygons:
            c = p.center
            s_param = (heel_y - c.y) / max(1e-4, heel_y - toe_y)
            if c.z < sole_h * 0.9 + 0.012 * smoothstep(0.82, 1.0, s_param) ** 2 and abs(p.normal.z) < 0.95 or c.z < 0.004:
                p.material_index = 1
            elif 0.3 < s_param < 0.66 and 0.035 < c.z < 0.07 and abs(c.x - cx) > half_w * 0.75:
                p.material_index = 2           # side panel
            elif s_param < 0.08 and c.z > 0.05:
                p.material_index = 2           # heel tab
        # Laces across the instep
        lb = bmesh.new()
        for j in range(5):
            s_param = lerp(0.34, 0.6, j / 4)
            y = lerp(heel_y, toe_y, s_param)
            h, w = shoe_profile(s_param)
            pc.tapered_tube(lb, [Vector((cx - half_w * w * 0.42, y, h - 0.006)), Vector((cx, y - 0.003, h + 0.003)),
                                 Vector((cx + half_w * w * 0.42, y, h - 0.006))], [0.0026] * 3, sides=5)
        laces = pc.new_mesh_object(side + "Laces", lb, coll)
        laces.data.materials.append(mats["sole"])
        for obj in (shoe, laces):
            for g in list(obj.vertex_groups):
                obj.vertex_groups.remove(g)
            gf = obj.vertex_groups.new(name=PREFIX + side + "Foot")
            gt = obj.vertex_groups.new(name=PREFIX + side + "ToeBase")
            ball_y = L["toe"].y
            for v in obj.data.vertices:
                t = smoothstep(ball_y + 0.02, ball_y - 0.02, v.co.y)
                if t < 0.999:
                    gf.add([v.index], 1.0 - t, "REPLACE")
                if t > 0.001:
                    gt.add([v.index], t, "REPLACE")
            pc.add_armature(obj, rig)
        join_into(shoe, [laces])
        shoes.append(shoe)
    return shoes


def join_into(target, others):
    if not others:
        return
    pm.set_active(target)
    for o in others:
        o.select_set(True)
    with pm.ctx(target, [target] + others):
        bpy.ops.object.join()


def head_frame(L):
    centre = Vector((0.0, L["head"].y + 0.012, L["head"].z + 0.07))
    return centre


def scalp_predicate(L):
    centre = head_frame(L)
    def is_scalp(co):
        d = co - centre
        r = d.length
        if r < 1e-4:
            return False
        el = math.degrees(math.asin(max(-1.0, min(1.0, d.z / r))))
        az = abs(math.degrees(math.atan2(d.x, -d.y)))  # 0 = face, 180 = back of the head
        if az <= 35:
            thr = 26
        elif az <= 100:
            thr = lerp(26, 4, (az - 35) / 65)
        else:
            thr = lerp(4, -32, (az - 100) / 80)
        if abs(co.x) > 0.072 and el < 14 and az < 125:
            return False  # ears
        return el > thr
    return is_scalp, centre


def make_hair(body, dom, tree, L, mats, coll, rig, cfg):
    is_scalp, centre = scalp_predicate(L)
    cap = pc.duplicate(body, "HairCap")
    pc.keep_faces(cap, lambda i, co: dom[i][0] in ("Head", "Neck") and is_scalp(co))
    thickness = 0.0045 if cfg["hair"] == "twists" else 0.0025
    pc.inflate(cap, lambda co, n: thickness)
    pc.smooth(cap, iterations=3, factor=0.3)
    cap.data.materials.clear()
    pc.assign_material(cap, mats["hair"])
    pc.set_weights_single(cap, "Head")
    pieces = [cap]
    if cfg["hair"] == "twists":
        pieces.append(make_twists(cap, centre, mats, coll, rig))
    else:
        pieces.append(make_cap_hat(centre, mats, coll, rig, cfg, body, dom, None))
    return pieces


def make_twists(cap, centre, mats, coll, rig):
    rng = random.Random(5)
    bm_cap = bmesh.new()
    bm_cap.from_mesh(cap.data)
    bm_cap.normal_update()
    faces = list(bm_cap.faces)
    areas = [f.calc_area() for f in faces]
    total = sum(areas)
    points = []
    attempts = 0
    while attempts < 9000 and len(points) < 420:
        attempts += 1
        f = rng.choices(faces, weights=areas)[0]
        verts = [l.vert.co for l in f.loops]
        a, b = rng.random(), rng.random()
        if a + b > 1:
            a, b = 1 - a, 1 - b
        p = verts[0] + (verts[1] - verts[0]) * a + (verts[-1] - verts[0]) * b
        d = p - centre
        el = math.degrees(math.asin(max(-1, min(1, d.z / max(d.length, 1e-5)))))
        if el < 30:            # faded sides: twists only on the top
            continue
        if any((p - q).length < 0.0125 for q, _ in points):
            continue
        points.append((p, f.normal.copy()))
    bm_cap.free()
    bm = bmesh.new()
    for p, n in points:
        d = (p - centre).normalized()
        el = math.degrees(math.asin(max(-1, min(1, d.z))))
        length = lerp(0.03, 0.05, smoothstep(30, 75, el)) * rng.uniform(0.85, 1.15)
        direction = (n * 0.9 + d * 0.4 + Vector((0, 0.18, 0.05))).normalized()
        gravity = Vector((0, 0.25, -0.6)) * (1.0 - smoothstep(55, 85, el)) * 0.5
        base = p - n * 0.001
        path = []
        for k in range(5):
            t = k / 4
            path.append(base + direction * (length * t) + gravity * (length * t * t))
        radii = [lerp(0.0062, 0.0026, (k / 4) ** 1.2) for k in range(5)]
        pc.tapered_tube(bm, path, radii, sides=5, twist=0.9)
    twists = pc.new_mesh_object("HairTwists", bm, coll)
    twists.data.materials.append(mats["hair"])
    pc.set_weights_single(twists, "Head")
    pc.add_armature(twists, rig)
    return twists


def make_cap_hat(centre, mats, coll, rig, cfg, body=None, dom=None, L=None):
    """Baseball cap worn backwards, fitted on the skull (opponent)."""
    d_el = 4.0
    cap = pc.duplicate(body, "Cap")
    def region(i, co):
        if dom[i][0] not in ("Head", "Neck"):
            return False
        d = co - centre
        r = d.length
        el = math.degrees(math.asin(max(-1.0, min(1.0, d.z / max(r, 1e-5)))))
        az = abs(math.degrees(math.atan2(d.x, -d.y)))
        thr = lerp(16, -6, smoothstep(40, 160, az))   # covers the forehead less, the back more
        if abs(co.x) > 0.07 and el < 10 and az < 130:
            return False
        return el > thr
    pc.keep_faces(cap, region)
    pc.relax_boundary(cap, lambda co: True, iterations=8, factor=0.6)
    pc.inflate(cap, lambda co, n: 0.011)
    pc.smooth(cap, iterations=10, factor=0.5)
    cap.data.materials.clear()
    cap_mat = pm.principled("PM_Cap", pm.srgb("#1b1b1d"), 0.8)
    pc.assign_material(cap, cap_mat)
    # Brim at the back (cap worn backwards).
    bm = bmesh.new()
    bm.from_mesh(cap.data)
    back = max((v.co for v in bm.verts if abs(v.co.x) < 0.03), key=lambda c: c.y - c.z * 0.4)
    rows, cols = 4, 9
    grid = []
    for r_ in range(rows + 1):
        row = []
        for c_ in range(cols + 1):
            u = c_ / cols * 2 - 1
            v_ = r_ / rows
            p = Vector((u * 0.085 * (1 - 0.3 * v_), back.y + 0.005 + v_ * 0.07, back.z - 0.004 - 0.012 * v_ - 0.03 * u * u * (1 - v_)))
            row.append(bm.verts.new(p))
        grid.append(row)
    for r_ in range(rows):
        for c_ in range(cols):
            bm.faces.new((grid[r_][c_], grid[r_][c_ + 1], grid[r_ + 1][c_ + 1], grid[r_ + 1][c_]))
    bm.to_mesh(cap.data)
    bm.free()
    mod = pc.solidify(cap, 0.005)
    mod.use_even_offset = False
    move_first(cap, mod)
    pc.apply_modifier(cap, mod.name)
    pc.set_weights_single(cap, "Head")
    return cap


def make_eyebrows(body, tree, L, mats, coll, rig):
    bm = bmesh.new()
    for sx in (1, -1):
        eye = L["eyeL"] if sx > 0 else L["eyeR"]
        pts = []
        for k in range(9):
            t = k / 8
            x = sx * lerp(0.011, 0.062, t)
            z = eye.z + lerp(0.021, 0.019, t) + 0.006 * math.sin(t * math.pi)
            hit = tree.ray_cast(Vector((x, eye.y - 0.2, z)), Vector((0, 1, 0)))
            if hit[0] is None:
                continue
            pts.append((hit[0] + hit[1] * 0.0018, hit[1], t))
        for k in range(len(pts) - 1):
            (p0, n0, t0), (p1, n1, t1) = pts[k], pts[k + 1]
            w0, w1 = lerp(0.0062, 0.0028, t0), lerp(0.0062, 0.0028, t1)
            up0 = Vector((0, 0, 1)) - n0 * n0.z
            up1 = Vector((0, 0, 1)) - n1 * n1.z
            a = bm.verts.new(p0 - up0.normalized() * w0 * 0.4)
            b = bm.verts.new(p1 - up1.normalized() * w1 * 0.4)
            c = bm.verts.new(p1 + up1.normalized() * w1 * 0.6 + n1 * 0.001)
            d = bm.verts.new(p0 + up0.normalized() * w0 * 0.6 + n0 * 0.001)
            bm.faces.new((a, b, c, d) if sx > 0 else (d, c, b, a))
    brows = pc.new_mesh_object("Eyebrows", bm, coll)
    brows.data.materials.append(mats["hair"])
    mod = pc.solidify(brows, 0.0015)
    pc.apply_modifier(brows, mod.name)
    pc.set_weights_single(brows, "Head")
    pc.add_armature(brows, rig)
    return brows


def cleanup_body(body, dom, L, cfg):
    """Deletes skin faces fully hidden by clothes (keeps margins at the openings)."""
    hem_z = L["hips"].z - 0.02
    neck_z = L["neck"].z
    wrist_x = L["wrist"].x
    ankle_z = L["ankle"].z
    waist_z = L["hips"].z + 0.075
    hoodie = cfg["outfit"] == "hoodie"
    hidden = []
    for i, v in enumerate(body.data.vertices):
        b, _ = dom[i]
        co = v.co
        h = False
        if b in ARM_BONES:
            h = hoodie and abs(co.x) < wrist_x - 0.06
        elif b in TORSO_BONES and b != "Neck":
            if hoodie:
                h = hem_z + 0.02 < co.z < neck_z - 0.05
            else:
                h = hem_z + 0.02 < co.z < 1.2 and abs(co.x) < 0.11
        if b in LEG_BONES or b == "Hips":
            h = h or (ankle_z + 0.08 < co.z < waist_z - 0.03)
        if b and b.endswith(("Foot", "ToeBase")) and co.z < ankle_z + 0.02:
            h = True
        hidden.append(h)
    pc.delete_faces(body, lambda f: all(hidden[v.index] for v in f.verts))


# ============================================================================ stage: textures

def stage_textures(name, cfg):
    """Skin albedo composed from the MakeHuman UV masks (lips, eyelids, ears, nails)."""
    import numpy as np
    from PIL import Image, ImageFilter
    size = 2048
    tex_dir = os.path.join(pm.MPFB_SRC, "data", "textures")
    def mask(n):
        im = Image.open(os.path.join(tex_dir, n)).convert("L")
        if im.size != (size, size):
            im = im.resize((size, size), Image.BILINEAR)
        return np.asarray(im, dtype=np.float32) / 255.0
    base = np.array(pm_hex(cfg["skin"]), dtype=np.float32)
    rng = np.random.default_rng(cfg["seed"])
    # Low-frequency variation (+/- 4 %) so the skin is not a flat colour.
    noise = rng.normal(0, 1, (64, 64)).astype(np.float32)
    noise_im = Image.fromarray(((noise - noise.min()) / (noise.max() - noise.min()) * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    noise_im = noise_im.filter(ImageFilter.GaussianBlur(24))
    var = (np.asarray(noise_im, dtype=np.float32) / 255.0 - 0.5) * 0.08
    img = np.ones((size, size, 3), dtype=np.float32) * base
    img *= (1.0 + var)[..., None]
    lips = mask("mpfb_lips.jpg")[..., None]
    lip_col = base * np.array([0.78, 0.62, 0.62], dtype=np.float32)
    img = img * (1 - lips * 0.85) + lip_col * lips * 0.85
    lids = mask("mpfb_eyelids.jpg")[..., None]
    img *= (1 - 0.12 * lids)
    ears = mask("mpfb_ears.jpg")[..., None]
    img = img * (1 - 0.25 * ears) + (img * np.array([1.06, 0.95, 0.92], dtype=np.float32)) * 0.25 * ears
    nails = np.clip(mask("mpfb_fingernails.jpg") + mask("mpfb_toenails.jpg"), 0, 1)[..., None]
    nail_col = np.clip(base * 1.35 + np.array([0.06, 0.03, 0.03], dtype=np.float32), 0, 1)
    img = img * (1 - nails * 0.8) + nail_col * nails * 0.8
    out = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), "RGB")
    os.makedirs(pm.TEXTURES, exist_ok=True)
    path = os.path.join(pm.TEXTURES, f"T_{name.capitalize()}_Skin.png")
    out.save(path, optimize=True)
    print("TEXTURE OK", path, flush=True)
    return path


def pm_hex(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]


# ============================================================================ previews

def preview(name, stage, cfg):
    body = bpy.data.objects.get("Body")
    if body is not None:
        tex = os.path.join(pm.TEXTURES, f"T_{name.capitalize()}_Skin.png")
        body.data.materials.clear()
        if os.path.exists(tex):
            body.data.materials.append(pm.image_material("PM_Skin", tex, 0.55))
        else:
            body.data.materials.append(pm.principled("PM_Skin", pm.srgb(cfg["skin"]), 0.55))
    pm.setup_preview(res=(900, 900))
    pm.add_sun(rotation_deg=(50, 0, 30), energy=3.0)
    pm.add_area("Fill", (-3, -3, 2), (0, 0, 1), energy=150, size=3, color=(0.75, 0.8, 1.0))
    pm.add_area("Rim", (2.5, 3, 2.4), (0, 0, 1.2), energy=260, size=2, color=(1.0, 0.75, 0.6))
    for obj in bpy.data.objects:
        if obj.type == "MESH" and not obj.data.materials:
            obj.data.materials.append(pm.principled("Clay_" + obj.name, (0.5, 0.42, 0.38), 0.7))
    pm.add_camera((0.0, -4.4, 1.05), (0, 0, 0.92), lens=50)
    pm.render(os.path.join(WORK, f"{name}_{stage}_front.png"))
    pm.add_camera((3.1, -3.1, 1.05), (0, 0, 0.92), lens=50)
    pm.render(os.path.join(WORK, f"{name}_{stage}_three_quarter.png"))
    pm.add_camera((0.35, -1.0, 1.66), (0, 0, 1.6), lens=55)
    pm.render(os.path.join(WORK, f"{name}_{stage}_face.png"))
    pm.add_camera((0.9, -1.1, 0.45), (0.05, 0, 0.12), lens=50)
    pm.render(os.path.join(WORK, f"{name}_{stage}_feet.png"))
    pm.add_camera((-2.2, 3.2, 1.5), (0, 0, 1.0), lens=50)
    pm.render(os.path.join(WORK, f"{name}_{stage}_back.png"))


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    name, stage = args[0], args[1]
    cfg = CHARACTERS[name]
    random.seed(cfg["seed"])
    if stage == "body":
        stage_body(name, cfg)
    elif stage == "outfit":
        stage_outfit(name, cfg)
    elif stage == "textures":
        stage_textures(name, cfg)
    else:
        raise SystemExit("unknown stage " + stage)
    if "--preview" in sys.argv:
        preview(name, stage, cfg)
    sys.stdout.flush()
    os._exit(0)  # bpy as a module can hang at interpreter shutdown


if __name__ == "__main__":
    main()
