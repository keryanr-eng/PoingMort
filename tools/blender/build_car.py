"""Builds the Poing Mort sedan (stylised boxy 80s/90s saloon, no brand) and exports it to FBX.

Conventions (Blender): car front toward -Y, Z up, left side (driver, left-hand drive) at +X.
Objects: Body, Door_FL / Door_FR (origin on the hinge), Wheel_FL/FR/RL/RR (origin at the hub,
axle along X), SteeringWheel, and empties used by the game: Seat_Driver (character root when
seated), Door_Entry_L/R, Exit_L/R.

Usage: python tools/blender/build_car.py [--preview]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import bmesh
from mathutils import Vector, Matrix

import pm_common as pm

WORK = os.environ.get("PM_WORK", "/tmp/pm_work")
OUT = os.path.join(pm.MODELS, "Vehicles")

L = 4.86          # length
W = 1.80          # width
WB = 2.74         # wheelbase
TRACK = 1.52
R_WHEEL = 0.33
FRONT_AXLE_Y = -1.37
REAR_AXLE_Y = FRONT_AXLE_Y + WB
BELT = 0.86       # beltline height
ROCKER = 0.25
ROOF = 1.37


def mats():
    return {
        "paint": pm.principled("PM_CarPaint", pm.srgb("#8f8a82"), 0.42),
        "chrome": pm.principled("PM_CarChrome", pm.srgb("#c9c9c6"), 0.22, metallic=0.9),
        "glass": pm.principled("PM_CarGlass", pm.srgb("#1d232a"), 0.08),
        "trim": pm.principled("PM_CarTrim", pm.srgb("#262628"), 0.7),
        "light": pm.principled("PM_CarLight", pm.srgb("#f3eedc"), 0.3, emission=pm.srgb("#fff2d0"), emission_strength=2.0),
        "tail": pm.principled("PM_CarTail", pm.srgb("#8e1410"), 0.35, emission=pm.srgb("#ff2a1a"), emission_strength=1.5),
        "interior": pm.principled("PM_CarInterior", pm.srgb("#2c2a29"), 0.85),
        "tire": pm.principled("PM_Tire", pm.srgb("#18181a"), 0.9),
        "rim": pm.principled("PM_Rim", pm.srgb("#a7a7a4"), 0.35, metallic=0.7),
    }


def new_obj(name, bm, materials):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    for m in materials:
        me.materials.append(m)
    return obj


def box(bm, centre, size, bevel=0.0, segments=2):
    res = bmesh.ops.create_cube(bm, size=1.0)
    verts = res["verts"]
    bmesh.ops.scale(bm, vec=Vector(size), verts=verts)
    bmesh.ops.translate(bm, vec=Vector(centre), verts=verts)
    if bevel > 0:
        edges = list({e for v in verts for e in v.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=segments, affect="EDGES", profile=0.5)
    return verts


def section_loop(points_yz_at_x, y):
    return points_yz_at_x


def body_profile_z(y):
    """Top of the lower body along the length (hood / beltline / trunk)."""
    if y < -L / 2 + 0.12:
        return 0.76
    if y < -0.78:                    # hood, slightly sloping toward the front
        t = (y + L / 2 - 0.12) / (L / 2 - 0.12 - 0.78)
        return 0.78 + 0.08 * t
    if y < 1.15:                     # beltline under the cabin
        return BELT
    if y < L / 2 - 0.1:              # trunk lid
        return 0.86 - 0.03 * (y - 1.15) / (L / 2 - 1.25)
    return 0.8


DOOR_Y0, DOOR_Y1 = -0.8, 0.26
DOOR_Z0 = ROCKER + 0.05
ARCH_R = R_WHEEL + 0.07


def side_outline(y_from, y_to, step=0.04):
    """Polygon (y, z) of a side panel between y_from and y_to, with wheel arches cut from the bottom."""
    top = []
    y = y_from
    while y < y_to - 1e-6:
        top.append((y, body_profile_z(y)))
        y += step
    top.append((y_to, body_profile_z(y_to)))
    bottom = []
    y = y_to
    while y > y_from + 1e-6:
        z = ROCKER
        for ay in (FRONT_AXLE_Y, REAR_AXLE_Y):
            d = abs(y - ay)
            if d < ARCH_R:
                z = max(z, R_WHEEL + 0.02 + math.sqrt(ARCH_R ** 2 - d ** 2))
        # Bumper-height rounding at both ends of the car
        end = max(0.0, (abs(y) - (L / 2 - 0.3)) / 0.3)
        z = max(z, ROCKER + 0.12 * end)
        bottom.append((y, z))
        y -= step
    bottom.append((y_from, max(ROCKER, ROCKER + 0.12 * max(0.0, (abs(y_from) - (L / 2 - 0.3)) / 0.3))))
    return top + bottom


def panel_from_outline(bm, outline, x, thickness, inward):
    """Extrudes a (y, z) polygon placed at x into a slab of the given thickness."""
    front = [bm.verts.new(Vector((x, y, z))) for y, z in outline]
    back = [bm.verts.new(Vector((x + inward * thickness, y, z))) for y, z in outline]
    n = len(outline)
    f1 = bm.faces.new(front)
    f2 = bm.faces.new(list(reversed(back)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[j], front[i], back[i], back[j]))
    return front + back


def bevel_obj(obj, width=0.018, segments=2, angle=35):
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(angle)
    pm.set_active(obj)
    with pm.ctx(obj):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def build_lower_body(m):
    bm = bmesh.new()
    half = W / 2
    t = 0.05
    for sx in (1, -1):
        # Front fender + front quarter (ahead of the door), rear quarter (behind the door), sill under the door.
        bm_outline = side_outline(-L / 2 + 0.02, DOOR_Y0)
        panel_from_outline(bm, bm_outline, sx * half, t, -sx)
        panel_from_outline(bm, side_outline(DOOR_Y1, L / 2 - 0.02), sx * half, t, -sx)
        sill = [(DOOR_Y0, ROCKER), (DOOR_Y1, ROCKER), (DOOR_Y1, DOOR_Z0 - 0.006), (DOOR_Y0, DOOR_Z0 - 0.006)]
        panel_from_outline(bm, sill, sx * half, t, -sx)
    # Hood, trunk, cowl and belt strips (top surfaces spanning the width)
    def deck(y0, y1, inset=0.0, step=0.08):
        ys = []
        y = y0
        while y < y1 - 1e-6:
            ys.append(y)
            y += step
        ys.append(y1)
        rows = []
        for y in ys:
            z = body_profile_z(y)
            rows.append([bm.verts.new(Vector((x, y, z + 0.004 - 0.018 * (x / half) ** 2))) for x in (-half + inset, half - inset)])
        for i in range(len(rows) - 1):
            bm.faces.new((rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0]))
    deck(-L / 2 + 0.02, -0.78)
    deck(1.15, L / 2 - 0.02)
    # Front and rear fascias
    for y, sgn in ((-L / 2 + 0.02, -1), (L / 2 - 0.02, 1)):
        z0, z1 = ROCKER + 0.12, body_profile_z(y)
        vs = [bm.verts.new(Vector(p)) for p in ((-half, y, z0), (half, y, z0), (half, y, z1), (-half, y, z1))]
        bm.faces.new(vs if sgn > 0 else list(reversed(vs)))
    bm.normal_update()
    obj = new_obj("BodyLower", bm, [m["paint"]])
    bevel_obj(obj, 0.015, 2, 40)
    # Belt strips under the side windows (paint), floor and wheel well liners (dark)
    bm = bmesh.new()
    for sx in (1, -1):
        box(bm, (sx * (half - 0.04), (-0.78 + 1.15) / 2, BELT + 0.01), (0.1, 1.15 + 0.78, 0.03), bevel=0.01)
    belt = new_obj("Belt", bm, [m["paint"]])
    bm = bmesh.new()
    box(bm, (0, 0, ROCKER + 0.03), (W - 0.06, L - 0.3, 0.04))
    for ay in (FRONT_AXLE_Y, REAR_AXLE_Y):
        for sx in (1, -1):
            # Liner: a half tube inside the arch so the body never looks hollow.
            segs = 14
            rings = []
            for i in range(segs + 1):
                a_ = math.pi * i / segs
                y = ay + math.cos(a_) * (ARCH_R - 0.01)
                z = R_WHEEL + 0.02 + math.sin(a_) * (ARCH_R - 0.01)
                rings.append((bm.verts.new(Vector((sx * (half - 0.005), y, z))), bm.verts.new(Vector((sx * (half - 0.3), y, z)))))
            for i in range(segs):
                (a1, b1), (a2, b2) = rings[i], rings[i + 1]
                bm.faces.new((a1, a2, b2, b1) if sx > 0 else (b1, b2, a2, a1))
    floor = new_obj("Underbody", bm, [m["trim"]])
    return [obj, belt, floor]


def build_cabin(m):
    """Greenhouse: pillars and roof in paint, windows as glass panels."""
    bm = bmesh.new()
    # Cabin outline (y, z) on the side, at the beltline and at the roof.
    belt_front, belt_rear = -0.78, 1.15
    roof_front, roof_rear = -0.18, 0.72
    half_belt = W / 2 - 0.05
    half_roof = W / 2 - 0.2
    pts = [
        Vector((-half_belt, belt_front, BELT)), Vector((half_belt, belt_front, BELT)),
        Vector((half_belt, belt_rear, BELT)), Vector((-half_belt, belt_rear, BELT)),
        Vector((-half_roof, roof_front, ROOF)), Vector((half_roof, roof_front, ROOF)),
        Vector((half_roof, roof_rear, ROOF)), Vector((-half_roof, roof_rear, ROOF)),
    ]
    v = [bm.verts.new(p) for p in pts]
    faces = [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)]
    for f in faces:
        bm.faces.new([v[i] for i in f])
    bm.normal_update()
    cabin = new_obj("Cabin", bm, [m["paint"], m["glass"]])
    # Windows: inset each side face and assign glass, keeping pillars as frame.
    bm = bmesh.new()
    bm.from_mesh(cabin.data)
    bm.faces.ensure_lookup_table()
    roof = [f for f in bm.faces if f.normal.z > 0.9]
    glass_faces = [f for f in bm.faces if f not in roof]
    res = bmesh.ops.inset_individual(bm, faces=glass_faces, thickness=0.06, depth=-0.015)
    for f in glass_faces:
        f.material_index = 1
    bm.to_mesh(cabin.data)
    bm.free()
    # Split side windows with B-pillars (thin paint quads over the glass).
    bmp = bmesh.new()
    for sx in (1, -1):
        for y0, y1 in ((0.2, 0.3),):
            a = Vector((sx * (half_belt + 0.004), y0, BELT))
            b = Vector((sx * (half_belt + 0.004), y1, BELT))
            c = Vector((sx * (half_roof + 0.004), y1 - 0.12, ROOF))
            d = Vector((sx * (half_roof + 0.004), y0 - 0.12, ROOF))
            vs = [bmp.verts.new(p) for p in (a, b, c, d)]
            bmp.faces.new(vs if sx > 0 else list(reversed(vs)))
    pillars = new_obj("Pillars", bmp, [m["paint"]])
    solid = pillars.modifiers.new("S", "SOLIDIFY")
    solid.thickness = 0.03
    pm.set_active(pillars)
    with pm.ctx(pillars):
        bpy.ops.object.modifier_apply(modifier=solid.name)
    # Give the cabin some thickness.
    solid = cabin.modifiers.new("S", "SOLIDIFY")
    solid.thickness = 0.03
    pm.set_active(cabin)
    with pm.ctx(cabin):
        bpy.ops.object.modifier_apply(modifier=solid.name)
    return cabin, pillars


def build_details(m):
    bm = bmesh.new()
    parts = []
    # Bumpers (chrome)
    for y, d in ((-L / 2 + 0.03, -1), (L / 2 - 0.03, 1)):
        box(bm, (0, y, 0.43), (W * 0.98, 0.14, 0.15), bevel=0.03)
    bumpers = new_obj("Bumpers", bm, [m["chrome"]])
    parts.append(bumpers)
    # Grille + headlights
    bm = bmesh.new()
    box(bm, (0, -L / 2 + 0.06, 0.64), (0.78, 0.05, 0.18), bevel=0.01)
    grille = new_obj("Grille", bm, [m["trim"]])
    parts.append(grille)
    bm = bmesh.new()
    for sx in (1, -1):
        box(bm, (sx * 0.62, -L / 2 + 0.07, 0.65), (0.34, 0.05, 0.15), bevel=0.01)
    heads = new_obj("Headlights", bm, [m["light"]])
    parts.append(heads)
    bm = bmesh.new()
    for sx in (1, -1):
        box(bm, (sx * 0.6, L / 2 - 0.04, 0.68), (0.46, 0.05, 0.16), bevel=0.01)
    tails = new_obj("Taillights", bm, [m["tail"]])
    parts.append(tails)
    # Side trim strip and mirrors, door handles
    bm = bmesh.new()
    for sx in (1, -1):
        # Side rubbing strip, interrupted by the wheel arches.
        y_a, y_b = FRONT_AXLE_Y + ARCH_R + 0.03, REAR_AXLE_Y - ARCH_R - 0.03
        box(bm, (sx * (W / 2 + 0.004), (y_a + y_b) / 2, 0.5), (0.016, y_b - y_a, 0.035), bevel=0.006)
        box(bm, (sx * (W / 2 + 0.004), (-L / 2 + 0.2 + FRONT_AXLE_Y - ARCH_R - 0.03) / 2, 0.5), (0.016, (FRONT_AXLE_Y - ARCH_R - 0.03) - (-L / 2 + 0.2), 0.035), bevel=0.006)
        box(bm, (sx * (W / 2 + 0.004), (L / 2 - 0.2 + REAR_AXLE_Y + ARCH_R + 0.03) / 2, 0.5), (0.016, (L / 2 - 0.2) - (REAR_AXLE_Y + ARCH_R + 0.03), 0.035), bevel=0.006)
        box(bm, (sx * (W / 2 + 0.07), -0.62, BELT + 0.12), (0.12, 0.06, 0.08), bevel=0.015)
        box(bm, (sx * (W / 2 + 0.012), 1.0, 0.76), (0.02, 0.14, 0.03), bevel=0.006)
    trims = new_obj("Trims", bm, [m["trim"]])
    parts.append(trims)
    # Plate recesses (no text)
    bm = bmesh.new()
    box(bm, (0, L / 2 - 0.02, 0.5), (0.5, 0.03, 0.12))
    box(bm, (0, -L / 2 + 0.02, 0.43), (0.5, 0.18, 0.12))
    plates = new_obj("Plates", bm, [pm.principled("PM_CarPlate", pm.srgb("#d8d4c8"), 0.6)])
    parts.append(plates)
    return parts


def build_interior(m):
    bm = bmesh.new()
    # Floor, dashboard, seats, rear bench
    box(bm, (0, 0.2, 0.3), (W - 0.2, 2.0, 0.06))
    box(bm, (0, -0.66, 0.8), (W - 0.2, 0.32, 0.22), bevel=0.04)
    for sx in (1, -1):
        box(bm, (sx * 0.37, 0.15, 0.47), (0.52, 0.5, 0.14), bevel=0.04)      # seat cushion
        box(bm, (sx * 0.37, 0.42, 0.78), (0.5, 0.12, 0.6), bevel=0.04)       # backrest
        box(bm, (sx * 0.37, 0.47, 1.12), (0.26, 0.1, 0.16), bevel=0.03)      # headrest
    box(bm, (0, 1.0, 0.47), (W - 0.25, 0.5, 0.14), bevel=0.04)
    box(bm, (0, 1.25, 0.78), (W - 0.25, 0.12, 0.55), bevel=0.04)
    interior = new_obj("Interior", bm, [m["interior"]])
    # Steering wheel (torus) + column, driver on the left (+X)
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, segments=24, radius=0.19)
    ring_v = list(bm.verts)
    bm.free()
    bm = bmesh.new()
    pts = []
    for i in range(24):
        a = 2 * math.pi * i / 24
        pts.append(Vector((math.cos(a) * 0.185, 0.0, math.sin(a) * 0.185)))
    tube = []
    for i, p in enumerate(pts):
        ring = []
        tangent = (pts[(i + 1) % 24] - pts[i - 1]).normalized()
        n1 = p.normalized()
        n2 = tangent.cross(n1)
        for k in range(8):
            a = 2 * math.pi * k / 8
            ring.append(bm.verts.new(p + (n1 * math.cos(a) + n2 * math.sin(a)) * 0.017))
        tube.append(ring)
    for i in range(24):
        for k in range(8):
            bm.faces.new((tube[i][k], tube[i][(k + 1) % 8], tube[(i + 1) % 24][(k + 1) % 8], tube[(i + 1) % 24][k]))
    box(bm, (0, 0.0, 0.0), (0.12, 0.05, 0.1), bevel=0.02)
    box(bm, (0, -0.12, 0.0), (0.06, 0.24, 0.06))
    wheel = new_obj("SteeringWheel", bm, [m["trim"]])
    wheel.location = (0.37, -0.42, 0.92)
    wheel.rotation_euler = (math.radians(-22), 0, 0)
    return interior, wheel


def build_wheel(m, name, pos):
    bm = bmesh.new()
    seg = 28
    tread = 0.2
    rings = []
    profile = [(R_WHEEL - 0.11, -tread / 2), (R_WHEEL - 0.02, -tread / 2), (R_WHEEL, -tread / 2 + 0.03),
               (R_WHEEL, tread / 2 - 0.03), (R_WHEEL - 0.02, tread / 2), (R_WHEEL - 0.11, tread / 2)]
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring = []
        for r, x in profile:
            ring.append(bm.verts.new(Vector((x, math.cos(a) * r, math.sin(a) * r))))
        rings.append(ring)
    for i in range(seg):
        for k in range(len(profile) - 1):
            bm.faces.new((rings[i][k], rings[i][k + 1], rings[(i + 1) % seg][k + 1], rings[(i + 1) % seg][k]))
    tire_faces = list(bm.faces)
    # Rim / hubcap on both sides
    for side in (-1, 1):
        x = side * (tread / 2 - 0.005)
        centre = bm.verts.new(Vector((x + side * 0.012, 0, 0)))
        outer = [bm.verts.new(Vector((x, math.cos(2 * math.pi * i / seg) * (R_WHEEL - 0.11), math.sin(2 * math.pi * i / seg) * (R_WHEEL - 0.11)))) for i in range(seg)]
        for i in range(seg):
            f = bm.faces.new((centre, outer[i], outer[(i + 1) % seg]) if side > 0 else (centre, outer[(i + 1) % seg], outer[i]))
            f.material_index = 1
    obj = new_obj(name, bm, [m["tire"], m["rim"]])
    obj.location = pos
    return obj


def join(objs, name):
    target = objs[0]
    pm.set_active(target)
    for o in objs[1:]:
        o.select_set(True)
    with pm.ctx(target, objs):
        bpy.ops.object.join()
    target.name = name
    target.data.name = name
    return target


def build_doors(m):
    """Front doors as separate panels with their window; origin on the hinge (front edge)."""
    doors = {}
    half = W / 2
    for side, sx, name in (("L", 1, "Door_FL"), ("R", -1, "Door_FR")):
        bm = bmesh.new()
        outline = [(DOOR_Y0 + 0.006, DOOR_Z0), (DOOR_Y1 - 0.006, DOOR_Z0), (DOOR_Y1 - 0.006, BELT), (DOOR_Y0 + 0.006, BELT)]
        panel_from_outline(bm, outline, sx * half, 0.05, -sx)
        bm.normal_update()
        skin = new_obj(name, bm, [m["paint"], m["glass"], m["interior"], m["trim"]])
        bevel_obj(skin, 0.012, 2, 40)
        # Inner trim panel, handle, window glass and a thin frame.
        bm = bmesh.new()
        box(bm, (sx * (half - 0.055), (DOOR_Y0 + DOOR_Y1) / 2, (DOOR_Z0 + BELT) / 2), (0.012, DOOR_Y1 - DOOR_Y0 - 0.06, BELT - DOOR_Z0 - 0.06))
        inner = new_obj("DoorInner", bm, [m["interior"]])
        bm = bmesh.new()
        a = Vector((sx * (half - 0.07), DOOR_Y0 + 0.05, BELT + 0.02))
        b = Vector((sx * (half - 0.07), DOOR_Y1 - 0.03, BELT + 0.02))
        c = Vector((sx * (half - 0.19), DOOR_Y1 - 0.12, ROOF - 0.06))
        d = Vector((sx * (half - 0.19), DOOR_Y0 + 0.6, ROOF - 0.06))
        vs = [bm.verts.new(p) for p in (a, b, c, d)]
        bm.faces.new(vs if sx > 0 else list(reversed(vs)))
        win = new_obj("DoorWindow", bm, [m["glass"]])
        bm = bmesh.new()
        box(bm, (sx * (half + 0.012), DOOR_Y1 - 0.18, 0.76), (0.02, 0.14, 0.03), bevel=0.006)
        handle = new_obj("DoorHandle", bm, [m["trim"]])
        door = join([skin, inner, win, handle], name)
        hinge = Vector((sx * half, DOOR_Y0, (DOOR_Z0 + BELT) / 2))
        door.data.transform(Matrix.Translation(-hinge))
        door.location = hinge
        doors[side] = door
    return doors


def empty(name, loc, parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "ARROWS"
    e.empty_display_size = 0.2
    e.location = loc
    bpy.context.scene.collection.objects.link(e)
    if parent is not None:
        e.parent = parent
    return e


def main():
    pm.reset_scene()
    m = mats()
    lower_parts = build_lower_body(m)
    cabin, pillars = build_cabin(m)
    details = build_details(m)
    interior, steering = build_interior(m)
    body = join(lower_parts + [cabin, pillars] + details + [interior], "Body")
    doors = build_doors(m)
    wheels = []
    for name, x, y in (("Wheel_FL", TRACK / 2, FRONT_AXLE_Y), ("Wheel_FR", -TRACK / 2, FRONT_AXLE_Y),
                       ("Wheel_RL", TRACK / 2, REAR_AXLE_Y), ("Wheel_RR", -TRACK / 2, REAR_AXLE_Y)):
        wheels.append(build_wheel(m, name, (x, y, R_WHEEL)))
    for o in [body, steering] + list(doors.values()) + wheels:
        for p in o.data.polygons:
            p.use_smooth = False
    root = empty("Car_Sedan", (0, 0, 0))
    for o in [body, steering] + list(doors.values()) + wheels:
        o.parent = root
    # Gameplay markers (Unity reads them by name)
    empty("Seat_Driver", (0.37, 0.12, 0.13), root)
    empty("Door_Entry_L", (W / 2 + 0.42, -0.2, 0.0), root)
    empty("Door_Entry_R", (-(W / 2 + 0.42), -0.2, 0.0), root)
    empty("Exit_L", (W / 2 + 0.62, 0.05, 0.0), root)
    empty("Exit_R", (-(W / 2 + 0.62), 0.05, 0.0), root)

    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, "car.blend"))
    fbx = os.path.join(OUT, "PM_Car_Sedan.fbx")
    for o in bpy.data.objects:
        o.select_set(True)
    with pm.ctx(root, list(bpy.data.objects)):
        bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"MESH", "EMPTY"},
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                                 axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True,
                                 mesh_smooth_type="FACE", bake_anim=False, path_mode="STRIP")
    print("CAR OK", fbx, os.path.getsize(fbx), sum(len(o.data.vertices) for o in bpy.data.objects if o.type == "MESH"), "verts", flush=True)

    if "--preview" in sys.argv:
        pm.setup_preview(res=(1000, 700), samples=16)
        pm.add_sun(rotation_deg=(55, 0, 140), energy=3.2)
        pm.add_area("Fill", (-4, -5, 3), (0, 0, 0.6), energy=400, size=4, color=(0.8, 0.85, 1.0))
        pm.ground_plane(30, color=(0.3, 0.29, 0.28))
        pm.add_camera((4.6, -5.4, 2.0), (0, 0, 0.65), lens=40)
        pm.render(os.path.join(WORK, "car_front34.png"))
        pm.add_camera((-5.0, 4.8, 1.8), (0, 0, 0.65), lens=40)
        pm.render(os.path.join(WORK, "car_rear34.png"))
        doors["L"].rotation_euler = (0, 0, math.radians(-60))
        pm.add_camera((5.5, 0.5, 1.6), (0, -0.2, 0.7), lens=40)
        pm.render(os.path.join(WORK, "car_door_open.png"))
    sys.stdout.flush()
    os._exit(0)


if __name__ == "__main__":
    main()
