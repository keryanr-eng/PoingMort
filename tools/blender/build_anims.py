"""Authors the Poing Mort animation clips on a character rig and exports the character FBX.

Usage:
  python tools/blender/build_anims.py <player|opponent> [--sheets] [--clips Jab,Cross]

Every clip is a function of normalised time t (0..1) returning a Pose; it is baked at 30 fps
into a Blender action named after the clip (Unity imports one clip per action). All clips are
in place: the game moves the character, the animation never translates the root.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector, Quaternion, Matrix

import pm_common as pm
import pm_pose as pp
from pm_pose import Pose, rot, ease

FPS = 30
WORK = os.environ.get("PM_WORK", "/tmp/pm_work")


def q_axis(axis, deg):
    return Quaternion(Vector(axis), math.radians(deg))


def smooth_pulse(t, a, b, c, d):
    """0 before a, rises to 1 between a and b, holds until c, falls back to 0 at d."""
    if t <= a or t >= d:
        return 0.0
    if t < b:
        return ease((t - a) / (b - a))
    if t <= c:
        return 1.0
    return 1.0 - ease((t - c) / (d - c))


class Body:
    """Landmarks of the character in its rest pose, to express poses in proportion."""

    def __init__(self, rig):
        r = pp.Rig(rig)
        self.r = r
        self.hips = r.rest_head("Hips")
        self.chest = r.rest_head("Spine2")
        self.neck = r.rest_head("Neck")
        self.head = r.rest_head("Head")
        self.shoulder_l = r.rest_head("LeftArm")
        self.shoulder_r = r.rest_head("RightArm")
        self.hip_l = r.rest_head("LeftUpLeg")
        self.hip_r = r.rest_head("RightUpLeg")
        self.ankle_l = r.rest_head("LeftFoot")
        self.ankle_r = r.rest_head("RightFoot")
        self.ankle_z = self.ankle_l.z
        self.leg = r.length("LeftUpLeg") + r.length("LeftLeg")
        self.arm = r.length("LeftArm") + r.length("LeftForeArm")
        self.solver = pp.Solver(rig)

    def blend(self, a, b, t):
        return pp.blend(self.solver, a, b, t)

    def foot(self, side, x=0.0, y=0.0, lift=0.0):
        base = self.ankle_l if side == "L" else self.ankle_r
        return Vector((base.x + x, base.y + y, self.ankle_z + lift))

    def torso_point(self, offset, yaw_deg=0.0, pitch_deg=0.0, base=None, hips_offset=Vector()):
        """Point attached to the chest (rotated by the torso yaw/pitch)."""
        q = rot(pitch=pitch_deg, yaw=yaw_deg)
        b = (base or self.chest) + hips_offset
        return b + q @ Vector(offset)


# ============================================================================ poses

def arms_down(down=76.0, swing_l=0.0, swing_r=0.0, elbow_l=12.0, elbow_r=12.0, out=0.0):
    """FK arms hanging along the body. swing > 0 moves the arm forward."""
    def side(sign, swing, elbow):
        upper = q_axis((1, 0, 0), -swing) @ q_axis((0, 0, 1), sign * -out) @ q_axis((0, 1, 0), sign * down)
        fore = q_axis((0, 0, -sign), elbow)
        return (upper, fore)
    return {"L": side(1.0, swing_l, elbow_l), "R": side(-1.0, swing_r, elbow_r)}


def standing(body, breathe=0.0):
    p = Pose()
    p.hips_offset = Vector((0, 0, -0.008))
    p.feet = {"L": body.foot("L", x=-0.02), "R": body.foot("R", x=0.02)}
    p.knee_poles = {"L": Vector((0.05, -1, 0)), "R": Vector((-0.05, -1, 0))}
    p.arm_fk = arms_down(down=77 - breathe * 1.5, swing_l=4, swing_r=4, elbow_l=14, elbow_r=14)
    p.chest = rot(pitch=-1.5 + breathe * 1.2)
    p.head = rot(pitch=2.0)
    p.fist = {"L": 0.3, "R": 0.3}
    return p


def guard(body, bounce=0.0, yaw=-24.0, weave=0.0, crouch=0.065):
    """Orthodox boxing stance: left foot forward, hands up."""
    p = Pose()
    p.hips_offset = Vector((0.0, 0.03, -crouch + bounce))
    p.hips = rot(yaw=yaw * 0.6)
    p.spine = rot(pitch=4, yaw=yaw * 0.25)
    p.chest = rot(pitch=6, yaw=yaw * 0.15, roll=weave)
    p.neck = rot(yaw=-yaw * 0.45, pitch=-2)
    p.head = rot(pitch=6, yaw=-yaw * 0.3)
    p.feet = {"L": body.foot("L", x=0.02, y=-0.19), "R": body.foot("R", x=-0.06, y=0.2)}
    p.foot_rot = {"L": rot(yaw=-12), "R": rot(yaw=-38)}
    p.knee_poles = {"L": Vector((0.15, -1, 0)), "R": Vector((-0.25, -1, 0))}
    p.hands = {"L": body.torso_point((0.08, -0.31, 0.2), yaw_deg=yaw, hips_offset=p.hips_offset),
               "R": body.torso_point((-0.1, -0.24, 0.26), yaw_deg=yaw, hips_offset=p.hips_offset)}
    p.elbow_poles = {"L": Vector((0.5, 0.2, -1)), "R": Vector((-0.5, 0.2, -1))}
    p.hand_rot = {"L": rot(roll=0), "R": rot(roll=0)}
    p.fist = {"L": 1.0, "R": 1.0}
    return p


def seated(body, steer=0.0):
    p = Pose()
    hip_target_z = 0.42
    p.hips_offset = Vector((0.0, 0.02, hip_target_z - body.hips.z))
    p.hips = rot(pitch=-12)
    p.spine = rot(pitch=2)
    p.chest = rot(pitch=4)
    p.head = rot(pitch=6)
    p.feet = {"L": Vector((body.ankle_l.x + 0.02, -0.66, 0.11)), "R": Vector((body.ankle_r.x - 0.02, -0.68, 0.11))}
    p.knee_poles = {"L": Vector((0.1, -1, 0.6)), "R": Vector((-0.1, -1, 0.6))}
    p.foot_rot = {"L": rot(pitch=-18), "R": rot(pitch=-18)}
    wheel = Vector((0.0, -0.44, 0.79))
    ang = math.radians(steer)
    for side, sx in (("L", 1), ("R", -1)):
        a = math.radians(160 if sx > 0 else 20) + ang
        p.hands[side] = wheel + Vector((math.cos(a) * 0.18, 0.0, math.sin(a) * 0.18 * 0.9 + 0.03))
    p.elbow_poles = {"L": Vector((0.8, 0.4, -1)), "R": Vector((-0.8, 0.4, -1))}
    p.fist = {"L": 0.72, "R": 0.72}
    return p


def lying_on_back(body, settle=0.0):
    p = Pose()
    p.hips_offset = Vector((0.0, 0.42, 0.13 - body.hips.z))
    p.hips = rot(pitch=-86)
    p.spine = rot(pitch=-3)
    p.chest = rot(pitch=-2, roll=4)
    p.head = rot(pitch=-8 + settle * 3, yaw=18)
    p.arm_fk = {"L": (q_axis((0, 1, 0), 48) @ q_axis((0, 0, 1), 20), q_axis((0, 0, -1), 30)),
                "R": (q_axis((0, 1, 0), -55) @ q_axis((0, 0, 1), -12), q_axis((0, 0, 1), 38))}
    p.feet = {"L": None, "R": None}
    p.fist = {"L": 0.35, "R": 0.4}
    p.toe = {"L": -20.0, "R": -20.0}
    return p


# ============================================================================ clips

def clip_idle(body, t):
    breathe = math.sin(2 * math.pi * t)
    p = standing(body, breathe)
    sway = math.sin(2 * math.pi * t) * 0.006
    p.hips_offset = Vector((sway, 0.0, -0.008 + 0.002 * breathe))
    p.head = rot(pitch=2.0, yaw=4.0 * math.sin(2 * math.pi * t + 1.0))
    return p


def foot_cycle(body, side, phase, stance, half_stride, lift, swing_mid_back=0.0, swing_mid_lift=None):
    """Ankle position for a locomotion cycle (character moving toward -Y, feet slide toward +Y on the ground)."""
    if phase < stance:
        s = phase / stance
        y = -half_stride + 2 * half_stride * s
        z = 0.0
        pitch = 12 * (1 - ease(s / 0.15)) if s < 0.15 else (-22 * ease((s - 0.7) / 0.3) if s > 0.7 else 0.0)
    else:
        s = (phase - stance) / (1 - stance)
        y = half_stride - 2 * half_stride * ease(s)
        y += swing_mid_back * math.sin(math.pi * s) * (1 - s)
        z = (swing_mid_lift if swing_mid_lift is not None else lift) * math.sin(math.pi * min(1.0, s * 1.15))
        pitch = -22 * (1 - ease(s / 0.3)) + 10 * ease((s - 0.6) / 0.4) if s > 0.6 else -22 * (1 - ease(s / 0.3))
    return body.foot(side, y=y, lift=z), pitch


def locomotion(body, t, speed, period, stance, lift, lean, arm_swing, elbow, bob, back_kick=0.0, knee_lift=None):
    p = Pose()
    half = speed * period * stance / 2.0
    for side, offset in (("L", 0.0), ("R", 0.5)):
        ph = (t + offset) % 1.0
        pos, pitch = foot_cycle(body, side, ph, stance, half, lift, back_kick, knee_lift)
        pos.x += -0.025 if side == "L" else 0.025
        p.feet[side] = pos
        p.foot_rot[side] = rot(pitch=-pitch)
        p.toe[side] = max(0.0, -pitch) * 0.9
    w = 2 * math.pi * t
    p.hips_offset = Vector((0.012 * math.sin(w) * (1.2 - speed / 6), 0.0, -0.025 - bob * math.cos(2 * w)))
    p.hips = rot(yaw=6 * math.sin(w), roll=2.5 * math.sin(w))
    p.spine = rot(pitch=lean * 0.5, yaw=-4 * math.sin(w))
    p.chest = rot(pitch=lean * 0.5, yaw=-5 * math.sin(w))
    p.head = rot(pitch=-lean * 0.6 + 3)
    swing = arm_swing * math.sin(w)
    p.arm_fk = arms_down(down=74 + elbow * 0.08, swing_l=-swing + 4, swing_r=swing + 4,
                         elbow_l=elbow + 10 * max(0.0, -math.sin(w)), elbow_r=elbow + 10 * max(0.0, math.sin(w)))
    p.knee_poles = {"L": Vector((0.05, -1, 0)), "R": Vector((-0.05, -1, 0))}
    p.fist = {"L": 0.45 if speed > 3 else 0.3, "R": 0.45 if speed > 3 else 0.3}
    return p


def clip_walk(body, t):
    return locomotion(body, t, speed=1.45, period=1.0, stance=0.6, lift=0.085, lean=3, arm_swing=16, elbow=16, bob=0.011)


def clip_run(body, t):
    return locomotion(body, t, speed=3.6, period=0.72, stance=0.4, lift=0.2, lean=9, arm_swing=34, elbow=78, bob=0.022,
                      back_kick=0.25, knee_lift=0.24)


def clip_sprint(body, t):
    return locomotion(body, t, speed=6.4, period=0.6, stance=0.34, lift=0.28, lean=15, arm_swing=46, elbow=86, bob=0.028,
                      back_kick=0.38, knee_lift=0.32)


def clip_fight_idle(body, t):
    bounce = 0.011 * math.sin(4 * math.pi * t)
    p = guard(body, bounce=bounce, weave=2.5 * math.sin(2 * math.pi * t))
    p.hands["L"] = p.hands["L"] + Vector((0, -0.015 * math.sin(2 * math.pi * t + 0.6), 0.008 * math.sin(4 * math.pi * t)))
    return p


def fight_step(body, t, direction):
    p = guard(body, bounce=0.008 * math.sin(4 * math.pi * t))
    d = Vector(direction) * 0.13
    for side, phase in (("L", 0.0), ("R", 0.5)):
        s = (t + phase) % 1.0
        slide = math.sin(2 * math.pi * s)
        lift = max(0.0, math.sin(2 * math.pi * s + math.pi / 2)) * 0.04 if side == "L" else max(0.0, math.sin(2 * math.pi * s - math.pi / 2)) * 0.04
        p.feet[side] = p.feet[side] - d * slide + Vector((0, 0, lift))
    return p


def clip_step_fwd(body, t): return fight_step(body, t, (0, -1, 0))
def clip_step_back(body, t): return fight_step(body, t, (0, 1, 0))
def clip_step_left(body, t): return fight_step(body, t, (1, 0, 0))
def clip_step_right(body, t): return fight_step(body, t, (-1, 0, 0))


def punch(body, t, timings, side, yaw_from, yaw_to, target, pole, extra_lean=0.0, heel_lift=0.0):
    a, b, c, d = timings  # rise starts, full extension, hold end, back to guard
    e = smooth_pulse(t, a, b, c, d)
    yaw = yaw_from + (yaw_to - yaw_from) * e
    p = guard(body, yaw=yaw, crouch=0.065 + 0.012 * e)
    p.chest = p.chest @ rot(pitch=extra_lean * e)
    tgt = body.torso_point(target, yaw_deg=yaw, hips_offset=p.hips_offset)
    p.hands[side] = p.hands[side].lerp(tgt, e)
    p.elbow_poles[side] = p.elbow_poles[side].lerp(Vector(pole), e)
    if heel_lift > 0:
        other = "R" if side == "R" else "L"
        p.foot_rot[other] = p.foot_rot[other] @ rot(pitch=heel_lift * e)
        p.feet[other] = p.feet[other] + Vector((0, 0, 0.03 * e))
    # The other hand tightens its guard.
    o = "R" if side == "L" else "L"
    p.hands[o] = p.hands[o] + Vector((0, 0.03 * e, 0.02 * e))
    return p


# Timings follow AttackDefinition presets: Jab 0.42 s (0.12 / 0.08 / 0.22), Cross 0.55 s (0.16 / 0.09 / 0.30),
# Hook 0.82 s (0.30 / 0.10 / 0.42). Contact happens at the end of the anticipation.
def clip_jab(body, t):
    return punch(body, t, (0.02, 0.29, 0.45, 0.98), "L", -24, -36, (0.05, -0.62, 0.22), (0.9, 0.2, -0.6), extra_lean=4)


def clip_cross(body, t):
    return punch(body, t, (0.03, 0.29, 0.45, 0.98), "R", -24, 22, (-0.02, -0.64, 0.23), (-0.9, 0.2, -0.6), extra_lean=5, heel_lift=-25)


def clip_hook(body, t):
    # Load (rotate right, drop slightly), then a horizontal arc with the elbow up, then recover.
    load = smooth_pulse(t, 0.0, 0.3, 0.36, 0.5)
    swing = smooth_pulse(t, 0.3, 0.46, 0.54, 0.98)
    yaw = -24 - 14 * load + 50 * swing
    p = guard(body, yaw=yaw, crouch=0.075 + 0.02 * load)
    p.spine = p.spine @ rot(roll=-4 * load + 5 * swing)
    start = body.torso_point((0.3, -0.2, 0.17), yaw_deg=yaw, hips_offset=p.hips_offset)
    arc = body.torso_point((0.02, -0.46, 0.2), yaw_deg=yaw, hips_offset=p.hips_offset)
    hand = p.hands["L"].lerp(start, load * (1 - swing)).lerp(arc, swing)
    p.hands["L"] = hand
    p.elbow_poles["L"] = Vector((0.5, 0.2, -1)).lerp(Vector((1, 0.3, 0.9)), max(load, swing))
    p.hands["R"] = p.hands["R"] + Vector((0, 0.02, 0.03)) * swing
    return p


def clip_dodge(body, t):
    e = smooth_pulse(t, 0.0, 0.3, 0.55, 1.0)
    p = guard(body, yaw=-24, crouch=0.065 + 0.07 * e)
    p.hips_offset = p.hips_offset + Vector((-0.06 * e, 0.12 * e, 0))
    p.spine = p.spine @ rot(pitch=-6 * e, roll=-10 * e)
    p.chest = p.chest @ rot(pitch=-5 * e, roll=-8 * e)
    p.feet["R"] = p.feet["R"] + Vector((-0.04, 0.14, 0)) * e
    p.hands["L"] = p.hands["L"] + Vector((-0.04, 0.12, 0.03)) * e
    p.hands["R"] = p.hands["R"] + Vector((-0.04, 0.1, 0.0)) * e
    return p


def clip_block_hit(body, t):
    e = smooth_pulse(t, 0.0, 0.15, 0.3, 1.0)
    p = guard(body, crouch=0.07 + 0.015 * e)
    for side in ("L", "R"):
        p.hands[side] = p.hands[side] + Vector((0.0, 0.07 * e, 0.04 * e))
    p.hands["L"].x -= 0.03 * e
    p.hands["R"].x += 0.03 * e
    p.chest = p.chest @ rot(pitch=-6 * e)
    p.head = p.head @ rot(pitch=10 * e)
    p.hips_offset = p.hips_offset + Vector((0, 0.04 * e, 0))
    return p


def clip_hit(body, t, heavy):
    k = 1.7 if heavy else 1.0
    e = smooth_pulse(t, 0.0, 0.12 if heavy else 0.1, 0.25 if heavy else 0.18, 1.0)
    p = guard(body, crouch=0.065 + 0.02 * e * k)
    p.head = p.head @ rot(pitch=-20 * e * (1.25 if heavy else 1.0), yaw=12 * e, roll=-8 * e)
    p.neck = p.neck @ rot(pitch=-6 * e * (1.3 if heavy else 1.0))
    p.chest = p.chest @ rot(pitch=-10 * e * k, roll=-5 * e)
    p.spine = p.spine @ rot(pitch=-4 * e * k)
    p.hips_offset = p.hips_offset + Vector((0.01, 0.05 * e * k, 0))
    if heavy:
        p.feet["R"] = p.feet["R"] + Vector((0.0, 0.1, 0)) * e
        p.hands["L"] = p.hands["L"] + Vector((0.12, 0.06, -0.12)) * e
        p.hands["R"] = p.hands["R"] + Vector((-0.12, 0.04, -0.1)) * e
    else:
        p.hands["L"] = p.hands["L"] + Vector((0.05, 0.05, -0.04)) * e
    return p


def clip_hit_light(body, t): return clip_hit(body, t, False)
def clip_hit_heavy(body, t): return clip_hit(body, t, True)


def clip_ko(body, t):
    g = guard(body)
    hit = clip_hit(body, min(1.0, t / 0.35) * 0.25, True)
    down = lying_on_back(body)
    if t < 0.2:
        return body.blend(g, hit, ease(t / 0.2))
    buckle = hit.copy()
    buckle.hips_offset = buckle.hips_offset + Vector((0, 0.15, -0.35))
    buckle.hips = rot(pitch=-25)
    buckle.feet = {"L": body.foot("L", y=-0.15), "R": body.foot("R", y=0.05)}
    buckle.knee_poles = {"L": Vector((0.2, -1, 0.3)), "R": Vector((-0.2, -1, 0.3))}
    buckle.hands = {"L": None, "R": None}
    buckle.arm_fk = arms_down(down=74, swing_l=18, swing_r=8, elbow_l=40, elbow_r=30, out=8)
    if t < 0.5:
        return body.blend(hit, buckle, ease((t - 0.2) / 0.3))
    if t < 0.85:
        return body.blend(buckle, down, ease((t - 0.5) / 0.35))
    return lying_on_back(body, settle=ease((t - 0.85) / 0.15))


def clip_get_up(body, t):
    down = lying_on_back(body, settle=1.0)
    sit = Pose()
    sit.hips_offset = Vector((0.0, 0.3, 0.2 - body.hips.z))
    sit.hips = rot(pitch=-20)
    sit.chest = rot(pitch=25)
    sit.head = rot(pitch=10)
    sit.feet = {"L": body.foot("L", y=-0.05, lift=0.0), "R": body.foot("R", y=0.0)}
    sit.knee_poles = {"L": Vector((0.1, -1, 1)), "R": Vector((-0.1, -1, 1))}
    sit.hands = {"L": Vector((0.25, 0.55, 0.08)), "R": Vector((-0.25, 0.55, 0.08))}
    sit.elbow_poles = {"L": Vector((1, 0.5, 0)), "R": Vector((-1, 0.5, 0))}
    sit.fist = {"L": 0.2, "R": 0.2}
    crouch = Pose()
    crouch.hips_offset = Vector((0.0, 0.08, 0.5 - body.hips.z))
    crouch.hips = rot(pitch=20)
    crouch.chest = rot(pitch=25)
    crouch.head = rot(pitch=-10)
    crouch.feet = {"L": body.foot("L", y=-0.1), "R": body.foot("R", y=0.1)}
    crouch.knee_poles = {"L": Vector((0.2, -1, 0.5)), "R": Vector((-0.2, -1, 0.5))}
    crouch.arm_fk = arms_down(down=74, swing_l=38, swing_r=30, elbow_l=55, elbow_r=50)
    crouch.fist = {"L": 0.4, "R": 0.4}
    stand = standing(body)
    if t < 0.35:
        return body.blend(down, sit, ease(t / 0.35))
    if t < 0.7:
        return body.blend(sit, crouch, ease((t - 0.35) / 0.35))
    return body.blend(crouch, stand, ease((t - 0.7) / 0.3))


def clip_car_enter(body, t):
    stand = standing(body)
    duck = standing(body)
    duck.hips_offset = Vector((0.0, 0.0, -0.18))
    duck.spine = rot(pitch=15)
    duck.chest = rot(pitch=22)
    duck.head = rot(pitch=15)
    duck.feet = {"L": body.foot("L", y=-0.05, lift=0.08), "R": body.foot("R", y=0.05)}
    duck.knee_poles = {"L": Vector((0.1, -1, 0.3)), "R": Vector((-0.1, -1, 0.3))}
    sit = seated(body)
    if t < 0.45:
        return body.blend(stand, duck, ease(t / 0.45))
    return body.blend(duck, sit, ease((t - 0.45) / 0.55))


def clip_seated(body, t):
    return seated(body, steer=6 * math.sin(2 * math.pi * t))


def clip_car_exit(body, t):
    sit = seated(body)
    out = standing(body)
    out.hips_offset = Vector((0.0, 0.0, -0.2))
    out.spine = rot(pitch=18)
    out.chest = rot(pitch=20)
    out.head = rot(pitch=10)
    out.feet = {"L": body.foot("L", y=-0.12), "R": body.foot("R", y=0.06, lift=0.05)}
    out.knee_poles = {"L": Vector((0.1, -1, 0.3)), "R": Vector((-0.1, -1, 0.3))}
    stand = standing(body)
    if t < 0.5:
        return body.blend(sit, out, ease(t / 0.5))
    return body.blend(out, stand, ease((t - 0.5) / 0.5))


def clip_fall(body, t):
    run = clip_run(body, 0.1)
    trip = run.copy()
    trip.hips_offset = trip.hips_offset + Vector((0.05, -0.2, -0.4))
    trip.hips = rot(pitch=35, roll=-15)
    trip.chest = rot(pitch=20)
    trip.arm_fk = arms_down(down=20, swing_l=60, swing_r=50, elbow_l=40, elbow_r=40)
    trip.feet = {"L": body.foot("L", y=0.1, lift=0.1), "R": body.foot("R", y=0.25, lift=0.2)}
    down = lying_on_back(body)
    if t < 0.3:
        return body.blend(run, trip, ease(t / 0.3))
    if t < 0.8:
        return body.blend(trip, down, ease((t - 0.3) / 0.5))
    return lying_on_back(body, settle=ease((t - 0.8) / 0.2))


# name -> (function, duration in seconds, loop)
CLIPS = {
    "Idle": (clip_idle, 3.0, True),
    "Walk": (clip_walk, 1.0, True),
    "Run": (clip_run, 0.72, True),
    "Sprint": (clip_sprint, 0.6, True),
    "FightIdle": (clip_fight_idle, 1.0, True),
    "FightStepFwd": (clip_step_fwd, 0.8, True),
    "FightStepBack": (clip_step_back, 0.8, True),
    "FightStepLeft": (clip_step_left, 0.8, True),
    "FightStepRight": (clip_step_right, 0.8, True),
    "Jab": (clip_jab, 0.42, False),
    "Cross": (clip_cross, 0.55, False),
    "Hook": (clip_hook, 0.82, False),
    "Dodge": (clip_dodge, 0.42, False),
    "BlockHit": (clip_block_hit, 0.3, False),
    "HitLight": (clip_hit_light, 0.45, False),
    "HitHeavy": (clip_hit_heavy, 0.7, False),
    "KO": (clip_ko, 1.4, False),
    "GetUp": (clip_get_up, 1.2, False),
    "CarEnter": (clip_car_enter, 0.85, False),
    "Seated": (clip_seated, 2.0, True),
    "CarExit": (clip_car_exit, 0.55, False),
    "Fall": (clip_fall, 0.9, False),
}


def bake_clip(rig, solver, body, name):
    fn, duration, loop = CLIPS[name]
    frames = max(2, int(round(duration * FPS)))
    action = bpy.data.actions.get(name) or bpy.data.actions.new(name)
    action.use_fake_user = True
    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = action
    # Clear previous keys
    for fc in list(action.fcurves) if hasattr(action, "fcurves") else []:
        action.fcurves.remove(fc)
    last = frames if loop else frames
    for f in range(0, last + 1):
        t = (f / frames) if loop else (f / frames)
        t = min(t, 1.0) if not loop else t % 1.0 if f < frames else 0.0
        pose = fn(body, t)
        solver.solve(pose)
        solver.write(frame=f + 1)
    action.frame_range = (1, last + 1)
    return action


def build_all(rig, names=None):
    solver = pp.Solver(rig)
    body = Body(rig)
    for n in (names or CLIPS.keys()):
        bake_clip(rig, solver, body, n)
        print("clip", n, flush=True)


def contact_sheet(rig, name, out_path, cols=6):
    from PIL import Image
    fn, duration, loop = CLIPS[name]
    action = bpy.data.actions[name]
    rig.animation_data.action = action
    f0, f1 = int(action.frame_range[0]), int(action.frame_range[1])
    sample = [round(f0 + (f1 - f0) * k / (cols - 1)) for k in range(cols)]
    tiles = []
    for f in sample:
        bpy.context.scene.frame_set(f)
        path = os.path.join(WORK, f"_frame_{name}_{f}.png")
        pm.render(path)
        tiles.append(Image.open(path).convert("RGB"))
    w, h = tiles[0].size
    sheet = Image.new("RGB", (w * cols, h))
    for i, im in enumerate(tiles):
        sheet.paste(im, (i * w, 0))
    sheet.save(out_path)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    who = args[0]
    pm.load_mpfb()
    bpy.ops.wm.open_mainfile(filepath=os.path.join(WORK, f"{who}_outfit.blend"))
    rig = bpy.data.objects["Armature"]
    clips = None
    for a in sys.argv:
        if a.startswith("--clips="):
            clips = a.split("=", 1)[1].split(",")
    build_all(rig, clips)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, f"{who}_anim.blend"))
    if "--sheets" in sys.argv:
        pm.setup_preview(res=(300, 380), samples=8)
        bpy.context.scene.eevee.taa_render_samples = 8
        pm.add_sun(rotation_deg=(50, 0, 30), energy=3.0)
        pm.add_area("Fill", (-3, -3, 2), (0, 0, 1), energy=150, size=3)
        pm.ground_plane(8)
        pm.add_camera((3.2, -3.4, 1.25), (0, 0, 0.85), lens=45)
        body = bpy.data.objects.get("Body")
        tex = os.path.join(pm.TEXTURES, f"T_{who.capitalize()}_Skin.png")
        if body is not None and os.path.exists(tex):
            body.data.materials.clear()
            body.data.materials.append(pm.image_material("PM_Skin", tex, 0.55))
        for n in (clips or CLIPS.keys()):
            contact_sheet(rig, n, os.path.join(WORK, f"sheet_{who}_{n}.png"))
    sys.stdout.flush()
    os._exit(0)


if __name__ == "__main__":
    main()
