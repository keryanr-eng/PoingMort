"""Builds the modular city assets of Poing Mort (buildings and street furniture) and exports FBX.

Conventions (Blender): a building's facade faces -Y, its origin is at the bottom-centre of the
facade, width along X, depth toward +Y. Props have their origin at the base centre.
UVs are world-scaled (metres) so the tiling textures keep a constant density.

Usage: python tools/blender/build_city.py [--preview]
Writes Assets/PoingMort/Art/Models/City/*.fbx and city_assets.json (sizes for the Unity builder).
"""
import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import bmesh
from mathutils import Vector, Matrix

import pm_common as pm

WORK = os.environ.get("PM_WORK", "/tmp/pm_work")
OUT = os.path.join(pm.MODELS, "City")
TEX = pm.TEXTURES

# Texture tile size in metres per material (UV = metres / tile)
TILE = {"PM_BrickRed": (2.0, 2.4), "PM_BrickDark": (2.0, 2.4), "PM_PlasterWarm": (4.0, 4.0), "PM_PlasterGrey": (4.0, 4.0),
        "PM_Concrete": (2.0, 2.0), "PM_RollerDoor": (3.0, 3.0), "PM_Roof": (4.0, 4.0), "PM_MetalDark": (1.0, 1.0),
        "PM_Wood": (1.0, 1.0), "PM_Bark": (1.0, 2.0), "PM_Foliage": (1.5, 1.5)}


class Mats:
    def __init__(self):
        def tex(name, file, rough):
            m = pm.image_material(name, os.path.join(TEX, file), rough)
            return m
        self.brick_red = tex("PM_BrickRed", "T_Brick_Red.png", 0.9)
        self.brick_dark = tex("PM_BrickDark", "T_Brick_Dark.png", 0.9)
        self.plaster_warm = tex("PM_PlasterWarm", "T_Plaster_Warm.png", 0.92)
        self.plaster_grey = tex("PM_PlasterGrey", "T_Plaster_Grey.png", 0.92)
        self.concrete = tex("PM_Concrete", "T_Concrete.png", 0.9)
        self.roller = tex("PM_RollerDoor", "T_RollerDoor.png", 0.6)
        self.foliage = tex("PM_Foliage", "T_Foliage.png", 0.85)
        self.bark = tex("PM_Bark", "T_Bark.png", 0.9)
        self.roof = pm.principled("PM_Roof", pm.srgb("#3c3a39"), 0.95)
        self.glass = pm.principled("PM_WindowGlass", pm.srgb("#2a3138"), 0.12)
        self.frame = pm.principled("PM_WindowFrame", pm.srgb("#ddd8cf"), 0.6)
        self.frame_dark = pm.principled("PM_FrameDark", pm.srgb("#2a2b2d"), 0.55)
        self.wood = pm.principled("PM_Wood", pm.srgb("#5a3f2c"), 0.75)
        self.metal = pm.principled("PM_MetalDark", pm.srgb("#2d3330"), 0.55, metallic=0.4)
        self.metal_green = pm.principled("PM_MetalGreen", pm.srgb("#2f4136"), 0.55, metallic=0.3)
        self.awning_red = pm.principled("PM_AwningRed", pm.srgb("#8f2a24"), 0.85)
        self.awning_green = pm.principled("PM_AwningGreen", pm.srgb("#2f5a3d"), 0.85)
        self.lamp = pm.principled("PM_LampLight", pm.srgb("#ffe2b0"), 0.4, emission=pm.srgb("#ffd59a"), emission_strength=4.0)
        self.red = pm.principled("PM_RedPaint", pm.srgb("#9c2b22"), 0.6)
        self.signs = {}
        self.interiors = {}

    def sign(self, file):
        if file not in self.signs:
            m = pm.image_material("PM_" + os.path.splitext(file)[0][2:], os.path.join(TEX, file), 0.6)
            self.signs[file] = m
        return self.signs[file]

    def interior(self, file):
        if file not in self.interiors:
            m = pm.image_material("PM_" + os.path.splitext(file)[0][2:], os.path.join(TEX, file), 0.8)
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            img = [n for n in m.node_tree.nodes if n.type == "TEX_IMAGE"][0]
            m.node_tree.links.new(img.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = 1.2
            self.interiors[file] = m
        return self.interiors[file]


class Builder:
    """Accumulates faces with materials and world-scaled UVs into one mesh."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.mats = []

    def mat_index(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def quad(self, pts, mat, uv=None):
        verts = [self.bm.verts.new(Vector(p)) for p in pts]
        f = self.bm.faces.new(verts)
        f.material_index = self.mat_index(mat)
        self._uv(f, mat, uv)
        return f

    def _uv(self, f, mat, uv=None):
        if uv is not None:
            for loop, (u, v) in zip(f.loops, uv):
                loop[self.uv].uv = (u, v)
            return
        f.normal_update()
        n = f.normal
        tw, th = TILE.get(mat.name, (1.0, 1.0))
        for loop in f.loops:
            p = loop.vert.co
            if abs(n.z) > 0.7:
                u, v = p.x / tw, p.y / th
            elif abs(n.x) > abs(n.y):
                u, v = p.y / tw, p.z / th
            else:
                u, v = p.x / tw, p.z / th
            loop[self.uv].uv = (u, v)

    def box(self, mn, mx, mat, faces="all", mat_top=None):
        x0, y0, z0 = mn
        x1, y1, z1 = mx
        P = lambda x, y, z: (x, y, z)
        quads = {
            "front": [P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1)],
            "back": [P(x1, y1, z0), P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1)],
            "left": [P(x0, y1, z0), P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1)],
            "right": [P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1)],
            "top": [P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1)],
            "bottom": [P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0), P(x0, y0, z0)],
        }
        for k, q in quads.items():
            if faces != "all" and k not in faces:
                continue
            self.quad(q, mat_top if (k == "top" and mat_top is not None) else mat)

    def cylinder(self, centre, radius, height, mat, segments=12, radius_top=None, cap=True):
        rt = radius if radius_top is None else radius_top
        cx, cy, cz = centre
        ring0, ring1 = [], []
        for i in range(segments):
            a = 2 * math.pi * i / segments
            ring0.append(self.bm.verts.new(Vector((cx + math.cos(a) * radius, cy + math.sin(a) * radius, cz))))
            ring1.append(self.bm.verts.new(Vector((cx + math.cos(a) * rt, cy + math.sin(a) * rt, cz + height))))
        for i in range(segments):
            j = (i + 1) % segments
            f = self.bm.faces.new((ring0[i], ring0[j], ring1[j], ring1[i]))
            f.material_index = self.mat_index(mat)
            u0, u1 = i / segments * 2 * math.pi * radius, j / segments * 2 * math.pi * radius
            self._uv(f, mat, [(u0, 0), (u1, 0), (u1, height), (u0, height)])
        if cap:
            f = self.bm.faces.new(list(reversed(ring0)))
            f.material_index = self.mat_index(mat)
            self._uv(f, mat)
            f = self.bm.faces.new(ring1)
            f.material_index = self.mat_index(mat)
            self._uv(f, mat)

    def blob(self, centre, radius, mat, subdiv=2, squash=0.85, noise=0.12, seed=0):
        rnd = random.Random(seed)
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius)
        for v in res["verts"]:
            d = v.co.normalized()
            v.co = v.co * (1 + rnd.uniform(-noise, noise))
            v.co.z *= squash
            v.co += Vector(centre)
        faces = {f for v in res["verts"] for f in v.link_faces}
        for f in faces:
            f.material_index = self.mat_index(mat)
            self._uv(f, mat)

    def finish(self, collection=None):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.0005)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        obj = bpy.data.objects.new(self.name, me)
        (collection or bpy.context.scene.collection).objects.link(obj)
        return obj


def wall_with_openings(b, u0, u1, v0, v1, openings, mat, y=0.0):
    """Facade wall in the XZ plane at depth y, with rectangular holes (u = x, v = z)."""
    us = sorted({u0, u1, *[o[0] for o in openings], *[o[1] for o in openings]})
    vs = sorted({v0, v1, *[o[2] for o in openings], *[o[3] for o in openings]})
    for i in range(len(us) - 1):
        for j in range(len(vs) - 1):
            cu, cv = (us[i] + us[i + 1]) / 2, (vs[j] + vs[j + 1]) / 2
            if any(o[0] <= cu <= o[1] and o[2] <= cv <= o[3] for o in openings):
                continue
            b.quad([(us[i], y, vs[j]), (us[i + 1], y, vs[j]), (us[i + 1], y, vs[j + 1]), (us[i], y, vs[j + 1])], mat)


def recess(b, x0, x1, z0, z1, depth, side_mat, back_mat, back_uv=None, y=0.0):
    """Reveals of an opening and its back panel (glass, door...)."""
    d = y + depth
    b.quad([(x0, d, z0), (x0, y, z0), (x0, y, z1), (x0, d, z1)], side_mat)          # left reveal
    b.quad([(x1, y, z0), (x1, d, z0), (x1, d, z1), (x1, y, z1)], side_mat)          # right reveal
    b.quad([(x0, y, z1), (x1, y, z1), (x1, d, z1), (x0, d, z1)], side_mat)          # head
    b.quad([(x0, d, z0), (x1, d, z0), (x1, y, z0), (x0, y, z0)], side_mat)          # sill
    if back_mat is not None:
        b.quad([(x0, d, z0), (x1, d, z0), (x1, d, z1), (x0, d, z1)], back_mat, back_uv)


def window(b, m, x0, x1, z0, z1, wall_mat, depth=0.16, frame=None, sill=True, lit=None):
    frame = frame or m.frame
    recess(b, x0, x1, z0, z1, depth, wall_mat, m.glass if lit is None else lit, back_uv=[(0, 0), (1, 0), (1, 1), (0, 1)] if lit else None)
    # Mullions (cross) slightly in front of the glass
    yf = depth - 0.03
    cx = (x0 + x1) / 2
    b.box((cx - 0.03, yf - 0.03, z0), (cx + 0.03, yf, z1), frame)
    zt = z0 + (z1 - z0) * 0.68
    b.box((x0, yf - 0.03, zt - 0.03), (x1, yf, zt + 0.03), frame)
    for xa, xb in ((x0, x0 + 0.05), (x1 - 0.05, x1)):
        b.box((xa, yf - 0.03, z0), (xb, yf, z1), frame)
    b.box((x0, yf - 0.03, z1 - 0.05), (x1, yf, z1), frame)
    b.box((x0, yf - 0.03, z0), (x1, yf, z0 + 0.05), frame)
    if sill:
        b.box((x0 - 0.08, -0.08, z0 - 0.07), (x1 + 0.08, 0.02, z0), m.concrete)


def building(name, m, width, floors, wall, ground="shop", depth=10.0, floor_h=3.2, sign=None, interior=None,
             awning=None, fire_escape=False, bay=2.9, cornice=True, seed=1, lit_ratio=0.15):
    rnd = random.Random(seed)
    b = Builder(name)
    W2 = width / 2
    H = floors * floor_h
    parapet = 0.9
    top = H + parapet
    # Shell: sides, back, roof
    b.box((-W2, 0.0, 0.0), (W2, depth, top), wall, faces=("back", "left", "right"))
    b.box((-W2 + 0.3, 0.3, H), (W2 - 0.3, depth - 0.3, H + 0.02), m.roof, faces=("top",))
    # Parapet inner faces
    b.box((-W2 + 0.3, 0.3, H), (W2 - 0.3, depth - 0.3, top), wall, faces=("front", "back", "left", "right"))
    b.quad([(-W2, 0, top), (W2, 0, top), (W2 - 0.3, 0.3, top), (-W2 + 0.3, 0.3, top)], m.concrete)
    b.quad([(W2, depth, top), (-W2, depth, top), (-W2 + 0.3, depth - 0.3, top), (W2 - 0.3, depth - 0.3, top)], m.concrete)
    b.quad([(-W2, depth, top), (-W2, 0, top), (-W2 + 0.3, 0.3, top), (-W2 + 0.3, depth - 0.3, top)], m.concrete)
    b.quad([(W2, 0, top), (W2, depth, top), (W2 - 0.3, depth - 0.3, top), (W2 - 0.3, 0.3, top)], m.concrete)

    openings = []
    # Ground floor
    g_top = 3.0
    if ground == "shop":
        sx0, sx1 = -W2 + 0.6, W2 - 0.6
        door_w = 1.2
        dx0 = sx1 - door_w - 0.2
        openings.append((sx0, dx0 - 0.15, 0.55, 2.7))                       # display window
        openings.append((dx0, dx0 + door_w, 0.0, 2.55))                     # shop door
    elif ground == "door":
        openings.append((-0.6, 0.6, 0.0, 2.4))
        for k in range(1, max(1, int(width / bay))):
            x = -W2 + k * width / max(1, int(width / bay))
            if abs(x) > 1.4:
                openings.append((x - 0.6, x + 0.6, 0.9, 2.4))
    elif ground == "garage":
        openings.append((-1.8, 1.8, 0.0, 3.0))
        openings.append((W2 - 1.6, W2 - 0.5, 0.0, 2.3))
    # Upper floors windows
    n_bays = max(1, int(round(width / bay)))
    bay_w = width / n_bays
    upper = []
    for f in range(1, floors):
        z0 = f * floor_h + 0.85
        z1 = z0 + 1.6
        for k in range(n_bays):
            cx = -W2 + bay_w * (k + 0.5)
            upper.append((cx - 0.62, cx + 0.62, z0, z1))
    all_open = openings + upper
    wall_with_openings(b, -W2, W2, 0.0, top, all_open, wall)

    # Ground floor contents
    if ground == "shop":
        o = openings[0]
        recess(b, o[0], o[1], o[2], o[3], 0.12, m.frame_dark, m.glass)
        if interior:
            # Lit interior card behind the glass
            b.quad([(o[0], 0.9, o[2]), (o[1], 0.9, o[2]), (o[1], 0.9, o[3]), (o[0], 0.9, o[3])], m.interior(interior), [(0, 0), (1, 0), (1, 1), (0, 1)])
            # Replace the glass with a semi-open frame: glass panes are thin bars so the interior reads
        for xx in (o[0] + (o[1] - o[0]) / 3, o[0] + 2 * (o[1] - o[0]) / 3):
            b.box((xx - 0.03, 0.06, o[2]), (xx + 0.03, 0.1, o[3]), m.frame_dark)
        d = openings[1]
        recess(b, d[0], d[1], d[2], d[3], 0.2, m.frame_dark, m.glass)
        b.box((d[0], 0.15, d[2]), (d[1], 0.18, d[2] + 0.9), m.frame_dark)
        # Sign band
        if sign:
            sw = min(width - 1.0, 0.6 * 12.8) / 2
            b.box((-W2 + 0.45, -0.07, 2.8), (W2 - 0.45, 0.0, 3.5), m.frame_dark)
            b.quad([(-sw, -0.08, 2.85), (sw, -0.08, 2.85), (sw, -0.08, 3.45), (-sw, -0.08, 3.45)], m.sign(sign), [(0, 0), (1, 0), (1, 1), (0, 1)])
        if awning:
            amat = m.awning_red if awning == "red" else m.awning_green
            ax0, ax1 = o[0] - 0.1, o[1] + 0.1
            b.quad([(ax0, 0.0, 2.75), (ax1, 0.0, 2.75), (ax1, -1.1, 2.35), (ax0, -1.1, 2.35)], amat)
            b.quad([(ax0, -1.1, 2.35), (ax1, -1.1, 2.35), (ax1, -1.1, 2.15), (ax0, -1.1, 2.15)], amat)
            b.quad([(ax1, 0.0, 2.75), (ax0, 0.0, 2.75), (ax0, -1.1, 2.35), (ax1, -1.1, 2.35)], amat)
        b.box((-W2, -0.05, 0.0), (W2, 0.0, 0.5), m.concrete, faces=("front", "top"))
    elif ground == "door":
        d = openings[0]
        recess(b, d[0], d[1], d[2], d[3], 0.3, wall, m.wood)
        b.box((d[0] + 0.15, 0.27, 1.5), (d[1] - 0.15, 0.29, 2.2), m.glass)
        b.box((-1.0, -0.6, 0.0), (1.0, 0.0, 0.17), m.concrete)
        b.box((-0.9, -0.3, 0.17), (0.9, 0.0, 0.34), m.concrete)
        for o in openings[1:]:
            window(b, m, o[0], o[1], o[2], o[3], wall)
    elif ground == "garage":
        g = openings[0]
        recess(b, g[0], g[1], g[2], g[3], 0.25, m.concrete, m.roller, back_uv=[(0, 0), (1.2, 0), (1.2, 1.0), (0, 1.0)])
        d = openings[1]
        recess(b, d[0], d[1], d[2], d[3], 0.15, m.concrete, m.metal)
        if sign:
            sw = min(width - 1.0, 0.6 * 12.8) / 2
            b.quad([(-sw, -0.08, 3.25), (sw, -0.08, 3.25), (sw, -0.08, 3.85), (-sw, -0.08, 3.85)], m.sign(sign), [(0, 0), (1, 0), (1, 1), (0, 1)])
    # Upper windows (a few lit for life at dusk)
    for o in upper:
        lit = m.interior("T_Shop_Interior_Warm.png") if rnd.random() < lit_ratio else None
        window(b, m, o[0], o[1], o[2], o[3], wall, lit=lit)
    # Bands and cornice
    if floors > 1:
        band_z = floor_h + (0.35 if ground in ("shop", "garage") else -0.05)
        b.box((-W2 - 0.02, -0.06, band_z), (W2 + 0.02, 0.0, band_z + 0.22), m.concrete)
    if cornice:
        b.box((-W2 - 0.12, -0.3, H + 0.45), (W2 + 0.12, 0.0, H + 0.75), m.concrete)
        b.box((-W2 - 0.05, -0.18, H + 0.3), (W2 + 0.05, 0.0, H + 0.45), m.concrete)
    # Fire escape on the central bay
    if fire_escape and floors >= 3:
        cx = 0.0 if n_bays % 2 else -bay_w / 2
        for f in range(1, floors):
            z = f * floor_h
            b.box((cx - 1.6, -1.0, z - 0.06), (cx + 1.6, -0.02, z), m.metal)
            b.box((cx - 1.6, -1.02, z), (cx + 1.6, -0.97, z + 0.95), m.metal, faces=("front", "back", "top"))
            for xx in (cx - 1.6, cx + 1.55):
                b.box((xx, -1.02, z), (xx + 0.05, -0.02, z + 0.95), m.metal)
            if f < floors - 1:
                # Stairs to the next platform
                steps = 10
                for s_ in range(steps):
                    t0 = s_ / steps
                    xa = cx - 1.4 + 2.6 * t0
                    za = z + floor_h * t0
                    b.box((xa, -0.9, za), (xa + 0.28, -0.12, za + 0.04), m.metal)
    # Rooftop clutter for the silhouette
    for k in range(rnd.randint(1, 3)):
        x = rnd.uniform(-W2 + 1.2, W2 - 1.2)
        y = rnd.uniform(2.0, depth - 2.0)
        sz = rnd.uniform(0.8, 1.6)
        b.box((x - sz / 2, y - sz / 2, H), (x + sz / 2, y + sz / 2, H + rnd.uniform(0.7, 1.4)), m.concrete)
    obj = b.finish()
    return obj, {"width": width, "depth": depth, "height": top, "floors": floors, "ground": ground}


# --------------------------------------------------------------------------- props

def prop_lamp(m):
    b = Builder("Prop_StreetLamp")
    b.cylinder((0, 0, 0), 0.12, 0.6, m.metal_green, 10, 0.09)
    b.cylinder((0, 0, 0.6), 0.07, 4.9, m.metal_green, 10, 0.055)
    # Arm toward the road (-Y)
    b.box((-0.03, -1.3, 5.38), (0.03, 0.0, 5.45), m.metal_green)
    b.box((-0.22, -1.62, 5.25), (0.22, -1.08, 5.45), m.metal_green)
    b.quad([(-0.19, -1.6, 5.24), (0.19, -1.6, 5.24), (0.19, -1.1, 5.24), (-0.19, -1.1, 5.24)][::-1], m.lamp)
    return b.finish(), {"height": 5.5, "light_offset": [0.0, -1.35, 5.15]}


def prop_bench(m):
    b = Builder("Prop_Bench")
    for x in (-0.75, 0.75):
        b.box((x - 0.04, -0.25, 0.0), (x + 0.04, 0.25, 0.45), m.metal)
        b.box((x - 0.04, 0.18, 0.45), (x + 0.04, 0.25, 0.85), m.metal)
    for k in range(4):
        y = -0.22 + k * 0.11
        b.box((-0.95, y, 0.45), (0.95, y + 0.09, 0.49), m.wood)
    for k in range(3):
        z = 0.55 + k * 0.1
        b.box((-0.95, 0.2, z), (0.95, 0.24, z + 0.08), m.wood)
    return b.finish(), {"size": [1.9, 0.5, 0.85]}


def prop_bin(m):
    b = Builder("Prop_TrashBin")
    b.cylinder((0, 0, 0), 0.26, 0.85, m.metal_green, 14, 0.29)
    b.cylinder((0, 0, 0.85), 0.31, 0.06, m.metal, 14)
    return b.finish(), {"radius": 0.3, "height": 0.9}


def prop_tree(m, seed=1):
    b = Builder("Prop_Tree")
    rnd = random.Random(seed)
    # Concrete planter + trunk + foliage clusters
    b.box((-0.7, -0.7, 0.0), (0.7, 0.7, 0.35), m.concrete, faces=("front", "back", "left", "right", "top"))
    b.cylinder((0, 0, 0.3), 0.16, 2.6, m.bark, 8, 0.11)
    for k in range(5):
        a = 2 * math.pi * k / 5 + rnd.uniform(-0.3, 0.3)
        r = rnd.uniform(0.6, 1.0)
        c = (math.cos(a) * r, math.sin(a) * r, 3.1 + rnd.uniform(-0.2, 0.6))
        b.blob(c, rnd.uniform(1.0, 1.35), m.foliage, 2, 0.85, 0.15, seed + k)
    b.blob((0, 0, 3.9), 1.3, m.foliage, 2, 0.8, 0.12, seed + 9)
    b.cylinder((0.0, 0.0, 2.2), 0.08, 1.0, m.bark, 6, 0.04)
    return b.finish(), {"height": 5.2, "trunk_radius": 0.2, "planter": 1.4}


def prop_hydrant(m):
    b = Builder("Prop_Hydrant")
    b.cylinder((0, 0, 0), 0.12, 0.6, m.red, 10, 0.11)
    b.cylinder((0, 0, 0.6), 0.14, 0.08, m.red, 10)
    b.box((-0.2, -0.05, 0.35), (0.2, 0.05, 0.45), m.red)
    return b.finish(), {"height": 0.7}


def prop_bollard(m):
    b = Builder("Prop_Bollard")
    b.cylinder((0, 0, 0), 0.08, 0.95, m.metal, 10, 0.07)
    b.cylinder((0, 0, 0.95), 0.09, 0.05, m.metal, 10)
    return b.finish(), {"height": 1.0}


def prop_barrier(m):
    b = Builder("Prop_Barrier")
    for x in (-1.0, 1.0):
        b.box((x - 0.03, -0.03, 0.0), (x + 0.03, 0.03, 1.05), m.metal_green)
    b.box((-1.0, -0.03, 0.98), (1.0, 0.03, 1.05), m.metal_green)
    b.box((-1.0, -0.03, 0.2), (1.0, 0.03, 0.26), m.metal_green)
    for k in range(11):
        x = -0.9 + k * 0.18
        b.box((x - 0.012, -0.012, 0.26), (x + 0.012, 0.012, 0.98), m.metal_green)
    return b.finish(), {"width": 2.0, "height": 1.05}


def prop_railing(m):
    """Courtyard fence: 3 m segment of vertical bars."""
    b = Builder("Prop_Fence")
    for x in (-1.5, 1.5):
        b.box((x - 0.04, -0.04, 0.0), (x + 0.04, 0.04, 2.2), m.metal)
    for z in (0.15, 2.05):
        b.box((-1.5, -0.025, z), (1.5, 0.025, z + 0.05), m.metal)
    for k in range(20):
        x = -1.42 + k * 0.15
        b.box((x - 0.012, -0.012, 0.15), (x + 0.012, 0.012, 2.15), m.metal)
    return b.finish(), {"width": 3.0, "height": 2.2}


def prop_crate(m):
    b = Builder("Prop_Crate")
    b.box((-0.45, -0.45, 0.0), (0.45, 0.45, 0.8), m.wood)
    for z in (0.05, 0.4, 0.72):
        b.box((-0.47, -0.47, z), (0.47, 0.47, z + 0.06), m.wood)
    return b.finish(), {"size": 0.9}


def prop_dumpster(m):
    b = Builder("Prop_Dumpster")
    b.box((-0.95, -0.55, 0.15), (0.95, 0.55, 1.2), m.metal_green)
    b.box((-1.0, -0.6, 1.2), (1.0, 0.6, 1.28), m.metal)
    for x in (-0.8, 0.8):
        for y in (-0.4, 0.4):
            b.cylinder((x, y, 0.0), 0.08, 0.15, m.metal, 8)
    return b.finish(), {"size": [1.9, 1.1, 1.3]}


def prop_floodlight(m):
    b = Builder("Prop_Floodlight")
    b.cylinder((0, 0, 0), 0.08, 4.6, m.metal, 8, 0.06)
    b.box((-0.3, -0.25, 4.5), (0.3, 0.05, 4.9), m.metal)
    b.quad([(-0.26, -0.26, 4.53), (0.26, -0.26, 4.53), (0.26, -0.26, 4.87), (-0.26, -0.26, 4.87)], m.lamp)
    return b.finish(), {"height": 4.9, "light_offset": [0.0, -0.4, 4.6]}


def prop_bus_stop(m):
    b = Builder("Prop_BusStop")
    for x in (-1.9, 1.9):
        b.box((x - 0.05, 0.4, 0.0), (x + 0.05, 0.5, 2.5), m.metal)
    b.box((-2.1, -0.4, 2.5), (2.1, 0.7, 2.6), m.metal)
    b.box((-1.9, 0.45, 0.25), (1.9, 0.48, 2.4), m.glass)
    b.box((-1.2, 0.0, 0.45), (1.2, 0.35, 0.5), m.wood)
    b.box((-1.2, 0.3, 0.0), (-1.15, 0.35, 0.45), m.metal)
    b.box((1.15, 0.3, 0.0), (1.2, 0.35, 0.45), m.metal)
    return b.finish(), {"size": [4.2, 1.1, 2.6]}


def prop_sign_one_way(m):
    b = Builder("Prop_SignOneWay")
    b.cylinder((0, 0, 0), 0.04, 2.6, m.metal, 8)
    b.quad([(-0.4, -0.05, 2.25), (0.4, -0.05, 2.25), (0.4, -0.05, 2.45), (-0.4, -0.05, 2.45)], m.sign("T_Sign_SensUnique.png"), [(0, 0), (1, 0), (1, 1), (0, 1)])
    b.quad([(0.4, 0.05, 2.25), (-0.4, 0.05, 2.25), (-0.4, 0.05, 2.45), (0.4, 0.05, 2.45)], m.sign("T_Sign_SensUnique.png"), [(0, 0), (1, 0), (1, 1), (0, 1)])
    return b.finish(), {"height": 2.6}


def prop_wall(m):
    """Concrete wall segment 4 m (courtyard enclosure and block ends)."""
    b = Builder("Prop_Wall")
    b.box((-2.0, -0.15, 0.0), (2.0, 0.15, 3.4), m.concrete)
    b.box((-2.05, -0.2, 3.4), (2.05, 0.2, 3.55), m.concrete)
    return b.finish(), {"width": 4.0, "height": 3.55, "thickness": 0.3}


def tower(m, name, width, depth, floors, seed):
    """Distant tower block for the skyline (simple, lit windows texture)."""
    b = Builder(name)
    H = floors * 3.2
    wall = m.plaster_grey if seed % 2 else m.concrete
    b.box((-width / 2, 0, 0), (width / 2, depth, H), wall, faces=("left", "right", "back", "top"))
    rnd = random.Random(seed)
    lit = m.interior("T_Windows_Lit.png")
    # Front facade: horizontal window bands with the lit texture
    for f in range(floors):
        z0 = f * 3.2 + 0.9
        b.quad([(-width / 2, 0, f * 3.2), (width / 2, 0, f * 3.2), (width / 2, 0, z0), (-width / 2, 0, z0)], wall)
        u = rnd.random()
        b.quad([(-width / 2, 0, z0), (width / 2, 0, z0), (width / 2, 0, z0 + 1.5), (-width / 2, 0, z0 + 1.5)], lit,
               [(u, f / 8.0), (u + width / 12.0, f / 8.0), (u + width / 12.0, f / 8.0 + 0.125), (u, f / 8.0 + 0.125)])
        b.quad([(-width / 2, 0, z0 + 1.5), (width / 2, 0, z0 + 1.5), (width / 2, 0, (f + 1) * 3.2), (-width / 2, 0, (f + 1) * 3.2)], wall)
    return b.finish(), {"width": width, "depth": depth, "height": H}


def export(obj, name):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.data.objects:
        o.select_set(False)
    obj.select_set(True)
    path = os.path.join(OUT, name + ".fbx")
    with pm.ctx(obj, [obj]):
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                                 mesh_smooth_type="FACE", bake_anim=False, path_mode="STRIP")
    return path


BUILDINGS = [
    # name, width, floors, wall, ground, kwargs
    ("Bld_Epicerie", 12.0, 3, "brick_red", "shop", dict(sign="T_Sign_Epicerie.png", interior="T_Shop_Interior_Warm.png", awning="green", seed=3)),
    ("Bld_Laverie", 10.0, 2, "plaster_warm", "shop", dict(sign="T_Sign_Laverie.png", interior="T_Shop_Interior_Cool.png", seed=4)),
    ("Bld_Cafe", 9.0, 3, "plaster_grey", "shop", dict(sign="T_Sign_Cafe.png", interior="T_Shop_Interior_Warm.png", awning="red", seed=5)),
    ("Bld_Residential", 14.0, 4, "brick_dark", "door", dict(fire_escape=True, seed=6)),
    ("Bld_ResidentialB", 11.0, 3, "brick_red", "door", dict(seed=7)),
    ("Bld_Garage", 12.0, 2, "plaster_grey", "garage", dict(sign="T_Sign_Atelier.png", seed=8)),
    ("Bld_Plain", 8.0, 3, "plaster_warm", "door", dict(seed=9, cornice=False)),
]


def main():
    pm.reset_scene()
    m = Mats()
    manifest = {"buildings": {}, "props": {}}
    walls = {"brick_red": m.brick_red, "brick_dark": m.brick_dark, "plaster_warm": m.plaster_warm, "plaster_grey": m.plaster_grey}
    objs = {}
    for name, width, floors, wall, ground, kw in BUILDINGS:
        obj, info = building(name, m, width, floors, walls[wall], ground, **kw)
        objs[name] = obj
        manifest["buildings"][name] = info
    for fn in (prop_lamp, prop_bench, prop_bin, prop_tree, prop_hydrant, prop_bollard, prop_barrier, prop_railing,
               prop_crate, prop_dumpster, prop_floodlight, prop_bus_stop, prop_sign_one_way, prop_wall):
        obj, info = fn(m)
        objs[obj.name] = obj
        manifest["props"][obj.name] = info
    for i, (w, d, f) in enumerate(((16, 14, 9), (12, 12, 12), (18, 14, 7))):
        obj, info = tower(m, f"Bld_Tower{i + 1}", w, d, f, i + 11)
        objs[obj.name] = obj
        manifest["buildings"][obj.name] = info
    for name, obj in objs.items():
        export(obj, name)
    with open(os.path.join(OUT, "city_assets.json"), "w") as fh:
        json.dump(manifest, fh, indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, "city.blend"))
    print("CITY OK", len(objs), "assets", sum(len(o.data.vertices) for o in objs.values()), "verts", flush=True)
    if "--preview" in sys.argv:
        preview(objs, m)
    sys.stdout.flush()
    os._exit(0)


def preview(objs, m):
    """Internal QA render of a short street (Blender, not a Unity capture)."""
    x = -26.0
    order = ["Bld_Garage", "Bld_Laverie", "Bld_Epicerie", "Bld_Residential", "Bld_Cafe", "Bld_ResidentialB", "Bld_Plain"]
    for name in order:
        o = objs[name]
        w = 0
        for bname, bw, *_ in BUILDINGS:
            if bname == name:
                w = bw
        o.location = (x + w / 2, 0, 0)
        x += w
    # ground
    bpy.ops.mesh.primitive_plane_add(size=1)
    g = bpy.context.active_object
    g.scale = (80, 30, 1)
    g.location = (0, -15, 0)
    g.data.materials.append(pm.image_material("PrevAsphalt", os.path.join(TEX, "T_Asphalt.png"), 0.9))
    bpy.ops.mesh.primitive_plane_add(size=1)
    s = bpy.context.active_object
    s.scale = (80, 3.5, 1)
    s.location = (0, -1.75, 0.15)
    s.data.materials.append(pm.image_material("PrevSidewalk", os.path.join(TEX, "T_Sidewalk.png"), 0.9))
    for k, xx in enumerate((-20, -6, 8, 22)):
        lamp = objs["Prop_StreetLamp"].copy()
        bpy.context.scene.collection.objects.link(lamp)
        lamp.location = (xx, -3.0, 0.15)
    tree = objs["Prop_Tree"]
    tree.location = (-13, -2.6, 0.15)
    t2 = tree.copy(); bpy.context.scene.collection.objects.link(t2); t2.location = (15, -2.6, 0.15)
    objs["Prop_Bench"].location = (2, -2.2, 0.15)
    objs["Prop_TrashBin"].location = (4, -2.8, 0.15)
    objs["Bld_Tower1"].location = (-30, 40, 0)
    objs["Bld_Tower2"].location = (10, 55, 0)
    objs["Bld_Tower3"].location = (35, 45, 0)
    pm.setup_preview(res=(1280, 720), samples=24, background=(0.62, 0.45, 0.42), strength=0.9)
    pm.add_sun(rotation_deg=(76, 0, 50), energy=3.4, color=(1.0, 0.72, 0.5))
    pm.add_camera((-14, -16, 1.7), (2, 0, 4.5), lens=28)
    pm.render(os.path.join(WORK, "city_street.png"))


if __name__ == "__main__":
    main()
