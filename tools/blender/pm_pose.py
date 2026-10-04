"""Procedural posing for the Mixamo-named MakeHuman rig (T-pose rest, character facing -Y, Z up).

A pose is described in body terms (hips offset, spine rotations, fist and foot targets, finger
curl); `apply_pose` solves two-bone IK for arms and legs analytically and writes FK bone
rotations (matrix_basis). Clips are produced by evaluating poses frame by frame and keying
every bone, so the exported FBX contains plain baked FK animation that Unity retargets.

World axes used in this file: +X = character's left, -Y = forward, +Z = up.
"""
import math

import bpy
from mathutils import Vector, Matrix, Quaternion, Euler

P = "mixamorig:"
FWD = Vector((0, -1, 0))
UP = Vector((0, 0, 1))
LEFT = Vector((1, 0, 0))


def rot(pitch=0.0, yaw=0.0, roll=0.0):
    """Body rotation in degrees: pitch > 0 bends forward, yaw > 0 turns to the character's left,
    roll > 0 tilts the top toward the character's left."""
    return (Quaternion(UP, math.radians(yaw)) @ Quaternion(LEFT, math.radians(pitch)) @
            Quaternion(Vector((0, 1, 0)), math.radians(roll)))


class Rig:
    def __init__(self, rig):
        self.obj = rig
        self.bones = {b.name: b for b in rig.data.bones}
        self.rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
        self.order = [b.name for b in rig.data.bones]  # parents always come first
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in rig.data.bones}

    def has(self, short):
        return (P + short) in self.bones

    def rest_head(self, short):
        return self.rest[P + short].to_translation()

    def length(self, short):
        return self.bones[P + short].length


def two_bone(root, target, l1, l2, pole):
    """Returns the middle joint position of a two-bone chain reaching for target."""
    d = target - root
    dist = max(1e-4, min(d.length, (l1 + l2) * 0.999))
    dn = d.normalized()
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(0.0, l1 * l1 - a * a))
    p = (pole - dn * pole.dot(dn))
    if p.length < 1e-5:
        p = Vector((0, 0, -1)) - dn * dn.z
    p.normalize()
    return root + dn * a + p * h


def frame_from(direction, up_hint):
    """Orthonormal basis with Y along direction (Blender bone convention) and Z close to up_hint."""
    y = direction.normalized()
    x = y.cross(up_hint)
    if x.length < 1e-5:
        x = y.cross(Vector((0, 0, 1)) if abs(y.z) < 0.9 else Vector((1, 0, 0)))
    x.normalize()
    z = x.cross(y).normalized()
    m = Matrix((x, y, z)).transposed()  # columns x, y, z
    return m


class Pose:
    """Pose parameters. Positions are in metres relative to the rest pose (world axes)."""

    def __init__(self, **kw):
        self.hips_offset = Vector(kw.get("hips_offset", (0, 0, 0)))
        self.hips = kw.get("hips", Quaternion())
        self.spine = kw.get("spine", Quaternion())
        self.spine1 = kw.get("spine1", Quaternion())
        self.chest = kw.get("chest", Quaternion())
        self.neck = kw.get("neck", Quaternion())
        self.head = kw.get("head", Quaternion())
        self.shoulders = kw.get("shoulders", {"L": Quaternion(), "R": Quaternion()})
        # Hand targets: absolute world positions (with the root at the origin) or None (arms by FK deltas)
        self.hands = kw.get("hands", {"L": None, "R": None})
        self.elbow_poles = kw.get("elbow_poles", {"L": Vector((0.4, 0.3, -1)), "R": Vector((-0.4, 0.3, -1))})
        self.arm_fk = kw.get("arm_fk", {"L": None, "R": None})        # (upper, fore) deltas if no target
        self.hand_rot = kw.get("hand_rot", {"L": Quaternion(), "R": Quaternion()})
        self.feet = kw.get("feet", {"L": None, "R": None})            # ankle target positions
        self.knee_poles = kw.get("knee_poles", {"L": Vector((0.1, -1, 0)), "R": Vector((-0.1, -1, 0))})
        self.foot_rot = kw.get("foot_rot", {"L": Quaternion(), "R": Quaternion()})
        self.toe = kw.get("toe", {"L": 0.0, "R": 0.0})               # toe bend (deg)
        self.fist = kw.get("fist", {"L": 0.25, "R": 0.25})            # 0 open .. 1 fist

    def copy(self):
        p = Pose()
        p.__dict__ = {k: (v.copy() if hasattr(v, "copy") else v) for k, v in self.__dict__.items()}
        for k in ("shoulders", "hands", "elbow_poles", "arm_fk", "hand_rot", "feet", "knee_poles", "foot_rot", "toe", "fist"):
            p.__dict__[k] = {s: (x.copy() if hasattr(x, "copy") else x) for s, x in getattr(self, k).items()}
        return p


def lerp_pose(a, b, t):
    p = a.copy()
    p.hips_offset = a.hips_offset.lerp(b.hips_offset, t)
    for k in ("hips", "spine", "spine1", "chest", "neck", "head"):
        setattr(p, k, getattr(a, k).slerp(getattr(b, k), t))
    for side in ("L", "R"):
        p.shoulders[side] = a.shoulders[side].slerp(b.shoulders[side], t)
        p.hand_rot[side] = a.hand_rot[side].slerp(b.hand_rot[side], t)
        p.foot_rot[side] = a.foot_rot[side].slerp(b.foot_rot[side], t)
        p.toe[side] = a.toe[side] + (b.toe[side] - a.toe[side]) * t
        p.fist[side] = a.fist[side] + (b.fist[side] - a.fist[side]) * t
        p.elbow_poles[side] = a.elbow_poles[side].lerp(b.elbow_poles[side], t)
        p.knee_poles[side] = a.knee_poles[side].lerp(b.knee_poles[side], t)
        ha, hb = a.hands[side], b.hands[side]
        p.hands[side] = None if ha is None or hb is None else ha.lerp(hb, t)
        fa, fb = a.feet[side], b.feet[side]
        p.feet[side] = None if fa is None or fb is None else fa.lerp(fb, t)
        aa, ab = a.arm_fk[side], b.arm_fk[side]
        if aa is not None and ab is not None:
            p.arm_fk[side] = (aa[0].slerp(ab[0], t), aa[1].slerp(ab[1], t))
    return p


def basis(direction, normal):
    """Rotation matrix with Y along direction and X along the part of normal orthogonal to it."""
    y = direction.normalized()
    x = normal - y * normal.dot(y)
    if x.length < 1e-6:
        x = y.orthogonal()
    x.normalize()
    z = x.cross(y).normalized()
    return Matrix((x, y, z)).transposed()


def ease(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


class Solver:
    def __init__(self, rig_obj):
        self.r = Rig(rig_obj)
        self.posed = {}

    def _parent_delta(self, name):
        parent = self.r.parent[name]
        if parent is None:
            return Matrix.Identity(4)
        return self.posed[parent] @ self.r.rest[parent].inverted()

    def _set(self, name, rot3=None, head=None):
        """Sets the armature-space posed matrix of a bone. rot3: absolute rotation (3x3) or None to follow parent."""
        rest = self.r.rest[name]
        follow = self._parent_delta(name) @ rest
        h = follow.to_translation() if head is None else head
        r3 = follow.to_3x3() if rot3 is None else rot3
        m = Matrix.Translation(h) @ r3.to_4x4()
        self.posed[name] = m

    def _set_delta(self, name, q, head=None):
        """Rotation q (world axes at rest) applied after the parent's motion."""
        rest = self.r.rest[name]
        follow = self._parent_delta(name) @ rest
        pd = self._parent_delta(name).to_quaternion()
        r3 = (pd @ q).to_matrix() @ rest.to_3x3()
        self._set(name, r3, head)

    def _aim_bend(self, name, direction, bend_normal, rest_bend_normal):
        """Points the bone along direction and rotates its roll so that its rest bending plane
        (normal rest_bend_normal) maps onto bend_normal: elbows and knees fold the right way."""
        rest = self.r.rest[name]
        rest_dir = (rest.to_3x3() @ Vector((0, 1, 0))).normalized()
        a_rest = basis(rest_dir, rest_bend_normal)
        a_tgt = basis(direction, bend_normal)
        self._set(name, a_tgt @ a_rest.inverted() @ rest.to_3x3())

    def solve(self, pose):
        r = self.r
        self.posed = {}
        for name in r.order:
            short = name[len(P):] if name.startswith(P) else name
            if short == "Hips":
                head = r.rest[name].to_translation() + pose.hips_offset
                self._set(name, (pose.hips.to_matrix() @ r.rest[name].to_3x3()), head)
            elif short == "Spine":
                self._set_delta(name, pose.spine)
            elif short == "Spine1":
                self._set_delta(name, pose.spine1)
            elif short == "Spine2":
                self._set_delta(name, pose.chest)
            elif short == "Neck":
                self._set_delta(name, pose.neck)
            elif short == "Head":
                self._set_delta(name, pose.head)
            elif short in ("LeftShoulder", "RightShoulder"):
                self._set_delta(name, pose.shoulders["L" if short.startswith("Left") else "R"])
            elif short in ("LeftArm", "RightArm"):
                self._arm(pose, "L" if short.startswith("Left") else "R")
            elif short in ("LeftForeArm", "RightForeArm", "LeftHand", "RightHand"):
                pass  # solved by _arm
            elif short in ("LeftUpLeg", "RightUpLeg"):
                self._leg(pose, "L" if short.startswith("Left") else "R")
            elif short in ("LeftLeg", "RightLeg", "LeftFoot", "RightFoot"):
                pass
            elif short in ("LeftToeBase", "RightToeBase"):
                side = "L" if short.startswith("Left") else "R"
                self._set_delta(name, Quaternion(LEFT, math.radians(-pose.toe[side])))
            elif "Hand" in short and any(f in short for f in ("Index", "Middle", "Ring", "Pinky", "Thumb")):
                self._finger(pose, short, name)
            else:
                self._set(name)
        return self.posed

    def _arm(self, pose, side):
        r = self.r
        s = "Left" if side == "L" else "Right"
        up_name, fore_name, hand_name = P + s + "Arm", P + s + "ForeArm", P + s + "Hand"
        sign = 1.0 if side == "L" else -1.0
        # T-pose, palms down: the elbow folds the forearm toward the front, around -Z (left) / +Z (right).
        rest_n = Vector((0, 0, -sign))
        target = pose.hands[side]
        if target is None:
            fk = pose.arm_fk[side] or (Quaternion(), Quaternion())
            self._set_delta(up_name, fk[0])
            self._set_delta(fore_name, fk[1])
        else:
            shoulder = (self._parent_delta(up_name) @ r.rest[up_name]).to_translation()
            l1 = r.bones[up_name].length
            l2 = r.bones[fore_name].length
            elbow = two_bone(shoulder, target, l1, l2, pose.elbow_poles[side])
            n = (elbow - shoulder).cross(target - elbow)
            if n.length < 1e-5:
                n = (elbow - shoulder).cross(pose.elbow_poles[side])
            if n.length < 1e-5:
                n = rest_n.copy()
            n.normalize()
            self._aim_bend(up_name, elbow - shoulder, n, rest_n)
            self._aim_bend(fore_name, target - elbow, n, rest_n)
        self._set_delta(hand_name, pose.hand_rot[side])

    def _leg(self, pose, side):
        r = self.r
        s = "Left" if side == "L" else "Right"
        up_name, low_name, foot_name = P + s + "UpLeg", P + s + "Leg", P + s + "Foot"
        target = pose.feet[side]
        rest_n = Vector((1, 0, 0))   # knees fold the shin backward, around +X
        if target is None:
            self._set(up_name)
            self._set(low_name)
        else:
            hip = (self._parent_delta(up_name) @ r.rest[up_name]).to_translation()
            l1 = r.bones[up_name].length
            l2 = r.bones[low_name].length
            knee = two_bone(hip, target, l1, l2, pose.knee_poles[side])
            n = (knee - hip).cross(target - knee)
            if n.length < 1e-5:
                n = rest_n.copy()
            n.normalize()
            if n.x < 0:
                n = -n
            self._aim_bend(up_name, knee - hip, n, rest_n)
            self._aim_bend(low_name, target - knee, n, rest_n)
        # The foot keeps its rest orientation in world space (flat on the ground) unless foot_rot is given.
        rest = r.rest[foot_name]
        follow = self._parent_delta(foot_name) @ rest
        r3 = pose.foot_rot[side].to_matrix() @ rest.to_3x3()
        self._set(foot_name, r3, follow.to_translation())

    def _finger(self, pose, short, name):
        """Curls a finger segment around its own hinge axis (bone X), toward the palm."""
        side = "L" if short.startswith("Left") else "R"
        curl = pose.fist[side]
        seg = int(short[-1]) if short[-1].isdigit() else 1
        if "Thumb" in short:
            ang = curl * (22 if seg == 1 else 38 if seg == 2 else 30)
        else:
            ang = curl * (72 if seg == 1 else 88 if seg == 2 else 62)
        if "Thumb" in short and curl > 0.05:
            self._thumb(name, side, seg, curl)
            return
        rest = self.r.rest[name].to_3x3()
        axis = rest.col[0].normalized()
        direction = rest.col[1].normalized()
        hand = self.r.rest[P + ("Left" if side == "L" else "Right") + "Hand"].to_3x3()
        palm = hand.col[2].normalized()
        if "Thumb" in short:
            palm = (palm + hand.col[1].normalized() * 0.6).normalized()  # thumb folds over the fingers
        test = Quaternion(axis, math.radians(10)) @ direction
        sign = 1.0 if (test - direction).dot(palm) > 0 else -1.0
        self._set_delta(name, Quaternion(axis, math.radians(sign * ang)))

    def _thumb(self, name, side, seg, curl):
        """Folds the thumb over the curled index and middle fingers (aimed, not hinged)."""
        sname = "Left" if side == "L" else "Right"
        rest4 = self.r.rest[name]
        follow = self._parent_delta(name) @ rest4
        head = follow.to_translation()
        cur = follow.to_3x3().col[1].normalized()
        hand = self.posed.get(P + sname + "Hand")
        i1 = self.posed.get(P + sname + "HandIndex1")
        i2 = self.posed.get(P + sname + "HandIndex2")
        i3 = self.posed.get(P + sname + "HandIndex3")
        m2 = self.posed.get(P + sname + "HandMiddle2")
        if hand is None or i1 is None or i2 is None or i3 is None:
            self._set(name)
            return
        hand_dir = hand.to_3x3().col[1].normalized()
        tip = i3.to_translation() + i3.to_3x3().col[1].normalized() * self.r.bones[P + sname + "HandIndex3"].length
        palm_side = tip - i1.to_translation()
        palm_side = (palm_side - hand_dir * palm_side.dot(hand_dir)).normalized()
        if seg == 1:
            mid = i2.to_translation().lerp(m2.to_translation(), 0.35) if m2 is not None else i2.to_translation()
            target = mid + palm_side * 0.016
            want = (target - head).normalized()
            d = cur.lerp(want, min(1.0, curl * 0.9)).normalized()
        else:
            # Follow-through segments wrap slightly more around the fingers.
            axis = cur.cross(palm_side)
            if axis.length < 1e-5:
                self._set(name)
                return
            d = (Quaternion(axis.normalized(), math.radians(18 * curl)) @ cur).normalized()
        q = cur.rotation_difference(d)
        self._set(name, q.to_matrix() @ follow.to_3x3())

    def write(self, frame=None):
        """Writes the solved pose into the pose bones (matrix_basis), optionally keying it."""
        r = self.r
        rig = r.obj
        for name in r.order:
            pb = rig.pose.bones[name]
            posed = self.posed.get(name)
            if posed is None:
                continue
            parent = r.parent[name]
            parent_posed = self.posed[parent] if parent else Matrix.Identity(4)
            parent_rest = r.rest[parent] if parent else Matrix.Identity(4)
            local_rest = parent_rest.inverted() @ r.rest[name]
            basis = local_rest.inverted() @ parent_posed.inverted() @ posed
            pb.rotation_mode = "QUATERNION"
            loc, q, _ = basis.decompose()
            pb.rotation_quaternion = q
            pb.location = loc if name.endswith("Hips") else Vector((0, 0, 0))
            pb.scale = (1, 1, 1)
            if frame is not None:
                pb.keyframe_insert("rotation_quaternion", frame=frame, group=name)
                if name.endswith("Hips"):
                    pb.keyframe_insert("location", frame=frame, group=name)


def to_ik(solver, pose):
    """Returns a copy of the pose where FK arms/legs are replaced by the equivalent IK targets,
    so that poses mixing FK and IK can be interpolated without passing through a T-pose."""
    out = pose.copy()
    posed = solver.solve(pose)
    for side, s in (("L", "Left"), ("R", "Right")):
        hand = posed[P + s + "Hand"].to_translation()
        elbow = posed[P + s + "ForeArm"].to_translation()
        shoulder = posed[P + s + "Arm"].to_translation()
        if out.hands[side] is None:
            out.hands[side] = hand.copy()
            mid = (shoulder + hand) * 0.5
            pole = elbow - mid
            out.elbow_poles[side] = pole.normalized() if pole.length > 1e-4 else out.elbow_poles[side]
            out.arm_fk[side] = None
        if out.feet[side] is None:
            ankle = posed[P + s + "Foot"].to_translation()
            knee = posed[P + s + "Leg"].to_translation()
            hip = posed[P + s + "UpLeg"].to_translation()
            out.feet[side] = ankle.copy()
            pole = knee - (hip + ankle) * 0.5
            out.knee_poles[side] = pole.normalized() if pole.length > 1e-4 else out.knee_poles[side]
            # Keep the foot orientation the FK pose produced.
            rest = solver.r.rest[P + s + "Foot"].to_3x3()
            out.foot_rot[side] = (posed[P + s + "Foot"].to_3x3() @ rest.inverted()).to_quaternion()
    return out


def blend(solver, a, b, t):
    return lerp_pose(to_ik(solver, a), to_ik(solver, b), t)
