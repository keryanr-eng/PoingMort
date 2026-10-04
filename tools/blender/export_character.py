"""Exports a finished character (mesh + Mixamo-named rig + all clips) to FBX for Unity.

Usage: python tools/blender/export_character.py <player|opponent>

Writes:
  Assets/PoingMort/Art/Models/Characters/PM_<Name>.fbx
  Assets/PoingMort/Art/Models/Characters/PM_<Name>.materials.json  (colours for the Unity builder)
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy

import pm_common as pm

WORK = os.environ.get("PM_WORK", "/tmp/pm_work")
OUT_DIR = os.path.join(pm.MODELS, "Characters")

GROUPS = {
    "Body": ["Body", "Eyes", "Eyebrows"],
    "Hair": ["HairCap", "HairTwists"],
    "Top": ["Hoodie", "Hood", "HoodiePocket", "Drawstrings", "TankTop"],
    "Pants": ["Pants"],
    "Shoes": ["LeftShoe", "RightShoe"],
    "Cap": ["Cap"],
}


def join(name, parts, rig):
    objs = [bpy.data.objects[p] for p in parts if p in bpy.data.objects]
    if not objs:
        return None
    target = objs[0]
    if len(objs) > 1:
        pm.set_active(target)
        for o in objs[1:]:
            o.select_set(True)
        with pm.ctx(target, objs):
            bpy.ops.object.join()
    target.name = name
    target.data.name = name
    target.parent = rig
    target.matrix_parent_inverse = rig.matrix_world.inverted()
    arm = [m for m in target.modifiers if m.type == "ARMATURE"]
    for m in arm[1:]:
        target.modifiers.remove(m)
    if not arm:
        mod = target.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
    else:
        arm[0].object = rig
    for m in list(target.modifiers):
        if m.type not in ("ARMATURE",):
            pm.set_active(target)
            with pm.ctx(target):
                bpy.ops.object.modifier_apply(modifier=m.name)
    return target


def clean_groups(obj, rig):
    bones = {b.name for b in rig.data.bones}
    for g in list(obj.vertex_groups):
        if g.name not in bones:
            obj.vertex_groups.remove(g)


def material_info(mat):
    info = {"name": mat.name}
    if mat.use_nodes:
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            c = bsdf.inputs["Base Color"].default_value
            info["color_linear"] = [round(c[0], 4), round(c[1], 4), round(c[2], 4)]
            info["roughness"] = round(bsdf.inputs["Roughness"].default_value, 3)
            info["metallic"] = round(bsdf.inputs["Metallic"].default_value, 3)
            link = bsdf.inputs["Base Color"].links
            if link and link[0].from_node.type == "TEX_IMAGE" and link[0].from_node.image is not None:
                info["texture"] = os.path.basename(link[0].from_node.image.filepath)
    return info


def main():
    who = [a for a in sys.argv[1:] if not a.startswith("--")][0]
    pm.load_mpfb()
    bpy.ops.wm.open_mainfile(filepath=os.path.join(WORK, f"{who}_anim.blend"))
    rig = bpy.data.objects["Armature"]

    body = bpy.data.objects["Body"]
    tex = os.path.join(pm.TEXTURES, f"T_{who.capitalize()}_Skin.png")
    body.data.materials.clear()
    body.data.materials.append(pm.image_material("PM_Skin", tex, 0.55))

    # Remove preview helpers if any
    for o in list(bpy.data.objects):
        if o.type in ("CAMERA", "LIGHT") or o.name.startswith("PreviewGround"):
            bpy.data.objects.remove(o, do_unlink=True)

    exported = []
    for name, parts in GROUPS.items():
        obj = join(name, parts, rig)
        if obj is not None:
            clean_groups(obj, rig)
            exported.append(obj)

    # Rest pose for the bind, no active action (all actions are exported as takes).
    if rig.animation_data is not None:
        rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    rig.name = "Armature"

    os.makedirs(OUT_DIR, exist_ok=True)
    fbx = os.path.join(OUT_DIR, f"PM_{who.capitalize()}.fbx")
    pm.set_active(rig)
    for o in exported:
        o.select_set(True)
    with pm.ctx(rig, [rig] + exported):
        bpy.ops.export_scene.fbx(
            filepath=fbx,
            use_selection=True,
            object_types={"ARMATURE", "MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
            use_mesh_modifiers=True,
            mesh_smooth_type="FACE",
            use_tspace=False,
            use_armature_deform_only=True,
            add_leaf_bones=False,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            armature_nodetype="NULL",
            bake_anim=True,
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=True,
            bake_anim_force_startend_keying=True,
            bake_anim_step=1.0,
            bake_anim_simplify_factor=0.5,
            path_mode="STRIP",
            embed_textures=False,
        )

    mats = {}
    for o in exported:
        for m in o.data.materials:
            if m and m.name not in mats:
                mats[m.name] = material_info(m)
    manifest = {
        "character": who,
        "fbx": os.path.basename(fbx),
        "meshes": {o.name: [m.name for m in o.data.materials if m] for o in exported},
        "materials": list(mats.values()),
        "clips": sorted(a.name for a in bpy.data.actions if a.users or a.use_fake_user),
        "height": round(max((rig.matrix_world @ v.co).z for v in body.data.vertices), 3),
        "vertices": {o.name: len(o.data.vertices) for o in exported},
    }
    with open(os.path.join(OUT_DIR, f"PM_{who.capitalize()}.materials.json"), "w") as fh:
        json.dump(manifest, fh, indent=2)
    print("EXPORT OK", fbx, os.path.getsize(fbx), manifest["vertices"], len(manifest["clips"]), "clips", flush=True)
    sys.stdout.flush()
    os._exit(0)


if __name__ == "__main__":
    main()
