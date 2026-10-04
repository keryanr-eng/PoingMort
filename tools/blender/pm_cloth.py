"""Geometry helpers to derive clothes, shoes and hair from a rigged MakeHuman body.

Every garment starts as a copy of body faces, so it inherits the body's UVs and skin weights;
it is then inflated along normals, smoothed (to remove anatomical detail), pushed out of the
body and extended (hems, cuffs). Extra pieces (hood, shoes, hair) get weights transferred
from the nearest body surface.
"""
import math
import random

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

PREFIX = "mixamorig:"


def bone_names(obj):
    return {g.index: g.name for g in obj.vertex_groups}


def dominant_bones(obj):
    """Per-vertex (bone short name, weight) of the strongest bone influence."""
    names = bone_names(obj)
    result = []
    for v in obj.data.vertices:
        best, bw = None, 0.0
        for g in v.groups:
            n = names.get(g.group, "")
            if not n.startswith(PREFIX):
                continue
            if g.weight > bw:
                best, bw = n[len(PREFIX):], g.weight
        result.append((best, bw))
    return result


def duplicate(obj, name):
    new = obj.copy()
    new.data = obj.data.copy()
    new.name = name
    new.data.name = name
    for coll in obj.users_collection:
        coll.objects.link(new)
    return new


def keep_faces(obj, vert_pred):
    """Keeps only faces whose vertices all satisfy vert_pred(index, co)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    keep = [vert_pred(v.index, v.co) for v in bm.verts]
    doomed = [f for f in bm.faces if not all(keep[v.index] for v in f.verts)]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def delete_faces(obj, face_pred):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    doomed = [f for f in bm.faces if face_pred(f)]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def inflate(obj, offset_fn):
    """Moves each vertex along its normal by offset_fn(co, normal) metres."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * offset_fn(v.co.copy(), v.normal.copy())
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def smooth(obj, iterations=8, factor=0.5, pin_boundary=True, weight_fn=None):
    """Laplacian smoothing. weight_fn(co) in 0..1 scales the effect (1 = full)."""
    me = obj.data
    n = len(me.vertices)
    co = np.array([v.co for v in me.vertices], dtype=np.float64)
    edges = np.array([e.vertices for e in me.edges], dtype=np.int64)
    if len(edges) == 0:
        return
    deg = np.bincount(edges.ravel(), minlength=n).astype(np.float64)
    boundary = np.zeros(n, dtype=bool)
    if pin_boundary:
        bm = bmesh.new()
        bm.from_mesh(me)
        for e in bm.edges:
            if e.is_boundary:
                boundary[e.verts[0].index] = True
                boundary[e.verts[1].index] = True
        bm.free()
    w = np.ones(n)
    if weight_fn is not None:
        w = np.array([weight_fn(Vector(c)) for c in co])
    w = w * factor
    w[boundary] = 0.0
    for _ in range(iterations):
        acc = np.zeros_like(co)
        np.add.at(acc, edges[:, 0], co[edges[:, 1]])
        np.add.at(acc, edges[:, 1], co[edges[:, 0]])
        avg = acc / np.maximum(deg, 1)[:, None]
        co = co + (avg - co) * w[:, None]
    for i, v in enumerate(me.vertices):
        v.co = co[i]
    me.update()


def push_out(obj, body_bvh, min_dist_fn):
    """Ensures every vertex is at least min_dist_fn(co) outside the body surface."""
    for v in obj.data.vertices:
        loc, normal, _, dist = body_bvh.find_nearest(v.co)
        if loc is None:
            continue
        d = (v.co - loc).dot(normal)
        need = min_dist_fn(v.co)
        if d < need:
            v.co = v.co + normal * (need - d)
    obj.data.update()


def body_bvh(body):
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.normal_update()
    tree = BVHTree.FromBMesh(bm)
    return tree, bm


def boundary_loops(obj):
    """Returns lists of vertex indices forming boundary loops."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    edges = [e for e in bm.edges if e.is_boundary]
    adj = {}
    for e in edges:
        a, b = e.verts[0].index, e.verts[1].index
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)
    seen = set()
    loops = []
    for start in adj:
        if start in seen:
            continue
        loop = [start]
        seen.add(start)
        prev, cur = None, start
        while True:
            nxt = [x for x in adj[cur] if x != prev and x not in seen]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
            loop.append(cur)
            seen.add(cur)
        loops.append(loop)
    bm.free()
    return loops


def extrude_loop(obj, loop_pred, offset_fn, steps=1):
    """Extrudes boundary edges whose two vertices satisfy loop_pred(co). offset_fn(co) gives the
    displacement of each new vertex (per step)."""
    for _ in range(steps):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        edges = [e for e in bm.edges if e.is_boundary and loop_pred(e.verts[0].co) and loop_pred(e.verts[1].co)]
        if not edges:
            bm.free()
            return
        res = bmesh.ops.extrude_edge_only(bm, edges=edges)
        new_verts = [g for g in res["geom"] if isinstance(g, bmesh.types.BMVert)]
        for v in new_verts:
            v.co += offset_fn(v.co.copy())
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()


def solidify(obj, thickness, offset=-1.0):
    mod = obj.modifiers.new("Epaisseur", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = offset
    mod.use_even_offset = True
    mod.use_quality_normals = True
    return mod


def apply_modifier(obj, mod_name):
    import pm_common as pm
    pm.set_active(obj)
    with pm.ctx(obj):
        bpy.ops.object.modifier_apply(modifier=mod_name)


def transfer_weights(target, source, max_dist=0.3):
    """Copies skin weights from the nearest point of source (barycentric on the nearest face)."""
    src_names = bone_names(source)
    bm = bmesh.new()
    bm.from_mesh(source.data)
    bm.faces.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    sv = source.data.vertices
    # Remove existing bone groups on the target.
    for g in list(target.vertex_groups):
        if g.name.startswith(PREFIX):
            target.vertex_groups.remove(g)
    groups = {}
    for v in target.data.vertices:
        loc, normal, fi, dist = tree.find_nearest(v.co, max_dist)
        if fi is None:
            continue
        face = bm.faces[fi]
        verts = [l.vert for l in face.loops]
        # Inverse-distance weights over the face vertices (robust for n-gons).
        ws = []
        for fv in verts:
            d = (fv.co - loc).length
            ws.append(1.0 / max(d, 1e-5))
        total = sum(ws)
        acc = {}
        for fv, w in zip(verts, ws):
            for g in sv[fv.index].groups:
                n = src_names.get(g.group, "")
                if not n.startswith(PREFIX):
                    continue
                acc[n] = acc.get(n, 0.0) + g.weight * w / total
        s = sum(acc.values())
        if s <= 0:
            continue
        for n, w in acc.items():
            if w / s < 0.01:
                continue
            grp = groups.get(n)
            if grp is None:
                grp = target.vertex_groups.get(n) or target.vertex_groups.new(name=n)
                groups[n] = grp
            grp.add([v.index], w / s, "REPLACE")
    bm.free()


def assign_material(obj, mat, face_pred=None):
    if mat.name not in [m.name for m in obj.data.materials if m]:
        obj.data.materials.append(mat)
    idx = [m.name for m in obj.data.materials].index(mat.name)
    for p in obj.data.polygons:
        if face_pred is None or face_pred(p):
            p.material_index = idx


def set_weights_single(obj, bone):
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    g = obj.vertex_groups.new(name=PREFIX + bone)
    g.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")


def add_armature(obj, rig):
    for m in list(obj.modifiers):
        if m.type == "ARMATURE":
            obj.modifiers.remove(m)
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    obj.parent = rig
    obj.matrix_parent_inverse = rig.matrix_world.inverted()
    return mod


def new_mesh_object(name, bm, collection):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    collection.objects.link(obj)
    return obj


def shade_smooth(obj, angle_deg=40.0):
    for p in obj.data.polygons:
        p.use_smooth = True
    try:
        with __import__("pm_common").ctx(obj):
            bpy.ops.object.shade_auto_smooth(angle=math.radians(angle_deg))
    except Exception:
        pass


def tapered_tube(bm, path, radii, sides=6, twist=0.0, cap=True):
    """Adds a tube along a polyline (list of Vectors) with per-point radii to bm."""
    rings = []
    up = Vector((0, 0, 1))
    for i, p in enumerate(path):
        if i < len(path) - 1:
            t = (path[i + 1] - p).normalized()
        else:
            t = (p - path[i - 1]).normalized()
        ref = up if abs(t.dot(up)) < 0.95 else Vector((1, 0, 0))
        n1 = t.cross(ref).normalized()
        n2 = t.cross(n1).normalized()
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides + twist * i
            ring.append(bm.verts.new(p + (n1 * math.cos(a) + n2 * math.sin(a)) * radii[i]))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for s in range(sides):
            a, b = rings[i][s], rings[i][(s + 1) % sides]
            c, d = rings[i + 1][(s + 1) % sides], rings[i + 1][s]
            bm.faces.new((a, b, c, d))
    if cap:
        bm.faces.new(list(reversed(rings[0])))
        if radii[-1] > 1e-4:
            bm.faces.new(rings[-1])
    return rings


def snap_boundary(obj, vert_pred, fn):
    """Moves boundary vertices satisfying vert_pred(co) to fn(co) (clean hems, collars, cuffs)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    moved = 0
    for v in bm.verts:
        if any(e.is_boundary for e in v.link_edges) and vert_pred(v.co):
            v.co = fn(v.co.copy())
            moved += 1
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return moved


def relax_boundary(obj, vert_pred, iterations=4, factor=0.5):
    """Smooths boundary vertices along their boundary neighbours only (removes stair steps)."""
    for _ in range(iterations):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        targets = {}
        for v in bm.verts:
            bnd = [e for e in v.link_edges if e.is_boundary]
            if len(bnd) != 2 or not vert_pred(v.co):
                continue
            a = bnd[0].other_vert(v).co
            b = bnd[1].other_vert(v).co
            targets[v.index] = v.co.lerp((a + b) * 0.5, factor)
        bm.verts.ensure_lookup_table()
        for i, co in targets.items():
            bm.verts[i].co = co
        bm.to_mesh(obj.data)
        bm.free()
    obj.data.update()


def surface_patch(target_tree, xs, zs, origin_y, direction, offset, z_offset_fn=None):
    """Grid projected on a surface along `direction` (used for pockets and patches)."""
    bm = bmesh.new()
    grid = []
    for zi, z in enumerate(zs):
        row = []
        for xi, x in enumerate(xs[zi] if isinstance(xs[0], (list, tuple)) else xs):
            o = Vector((x, origin_y, z))
            hit = target_tree.ray_cast(o, direction)
            if hit[0] is None:
                row.append(None)
                continue
            row.append(bm.verts.new(hit[0] + hit[1] * offset))
        grid.append(row)
    for zi in range(len(grid) - 1):
        for xi in range(len(grid[zi]) - 1):
            q = (grid[zi][xi], grid[zi][xi + 1], grid[zi + 1][xi + 1], grid[zi + 1][xi])
            if all(q):
                bm.faces.new(q)
    return bm


def tree_from_object(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    return tree


def drape_ellipse(obj, centre, ax, ay, zmax, blend=0.06):
    """Pushes vertices below zmax out to at least an ellipse (cloth bridges body crevices)."""
    for v in obj.data.vertices:
        if v.co.z > zmax + blend:
            continue
        dx, dy = v.co.x - centre.x, v.co.y - centre.y
        r = math.hypot(dx, dy)
        if r < 1e-5:
            continue
        c, s_ = dx / r, dy / r
        need = 1.0 / math.sqrt((c / ax) ** 2 + (s_ / ay) ** 2)
        if r < need:
            k = 1.0 if v.co.z <= zmax else 1.0 - (v.co.z - zmax) / blend
            nr = r + (need - r) * k
            v.co.x = centre.x + c * nr
            v.co.y = centre.y + s_ * nr
    obj.data.update()


def extents_at(obj, z0, z1):
    xs, ys = [], []
    for v in obj.data.vertices:
        if z0 <= v.co.z <= z1:
            xs.append(v.co.x)
            ys.append(v.co.y)
    return (min(xs), max(xs), min(ys), max(ys)) if xs else None


def keep_faces_by_center(obj, face_pred):
    """Keeps faces whose centre satisfies face_pred(centre, face) (smoother regions for thin straps)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    doomed = [f for f in bm.faces if not face_pred(f.calc_center_median(), f)]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
