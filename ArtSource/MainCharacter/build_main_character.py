import bpy
import json
import math
import os
from mathutils import Vector, Quaternion


BASE = os.path.dirname(os.path.abspath(__file__))
UNITY_OUT = os.path.abspath(os.path.join(BASE, "../../Assets/_Project/Art/Characters/MainCharacter"))
DETAIL = os.environ.get("HERO_DETAIL", "hero")
BEVEL_SEGMENTS = 2 if DETAIL == "hero" else 1


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.materials, bpy.data.curves, bpy.data.meshes, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)


def material(name, color, metallic=0.0, roughness=0.65):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return m


def finish(obj, name, mat, bone, bevel=0.0, smooth=False):
    obj.name = name
    obj.data.materials.append(mat)
    if bevel > 0.0:
        mod = obj.modifiers.new("Soft manufactured edge", "BEVEL")
        mod.width = bevel
        mod.segments = BEVEL_SEGMENTS
        mod.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    return obj


def box(name, location, dimensions, mat, bone, rotation=(0.0, 0.0, 0.0), bevel=0.025):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, bone, bevel)


def sphere(name, location, scale, mat, bone, vertices=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=vertices, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, bone, smooth=True)


def cylinder(name, location, radius, depth, mat, bone, axis="Y", vertices=12, bevel=0.0):
    rotation = (math.pi / 2.0, 0.0, 0.0) if axis == "Y" else (0.0, math.pi / 2.0, 0.0) if axis == "X" else (0.0, 0.0, 0.0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    return finish(bpy.context.object, name, mat, bone, bevel, smooth=True)


def cone(name, location, radius1, radius2, depth, mat, bone, axis="Y", vertices=12, bevel=0.0):
    rotation = (math.pi / 2.0, 0.0, 0.0) if axis == "Y" else (0.0, math.pi / 2.0, 0.0) if axis == "X" else (0.0, 0.0, 0.0)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=location, rotation=rotation)
    return finish(bpy.context.object, name, mat, bone, bevel, smooth=True)


def torus(name, location, major, minor, mat, bone, axis="Y"):
    rotation = (math.pi / 2.0, 0.0, 0.0) if axis == "Y" else (0.0, math.pi / 2.0, 0.0)
    bpy.ops.mesh.primitive_torus_add(
        major_segments=16 if DETAIL == "hero" else 10,
        minor_segments=6 if DETAIL == "hero" else 4,
        major_radius=major,
        minor_radius=minor,
        location=location,
        rotation=rotation,
    )
    return finish(bpy.context.object, name, mat, bone, smooth=True)


def create_rig():
    bones = {
        "Root": ((0.0, 0.0, 0.02), (0.0, 0.0, 0.22), None),
        "Pelvis": ((0.0, 0.0, 0.88), (0.0, 0.0, 1.08), "Root"),
        "Spine": ((0.0, 0.0, 1.04), (0.0, -0.01, 1.31), "Pelvis"),
        "Chest": ((0.0, -0.01, 1.31), (0.0, -0.02, 1.53), "Spine"),
        "Neck": ((0.0, -0.02, 1.52), (0.0, -0.03, 1.68), "Chest"),
        "Head": ((0.0, -0.03, 1.67), (0.0, -0.04, 1.94), "Neck"),
    }
    for side, sign in (("L", 1), ("R", -1)):
        bones.update({
            f"Thigh.{side}": ((sign * 0.14, 0.0, 0.94), (sign * 0.16, 0.0, 0.52), "Pelvis"),
            f"Shin.{side}": ((sign * 0.16, 0.0, 0.52), (sign * 0.17, 0.0, 0.13), f"Thigh.{side}"),
            f"Foot.{side}": ((sign * 0.17, 0.0, 0.13), (sign * 0.17, -0.18, 0.07), f"Shin.{side}"),
            f"UpperArm.{side}": ((sign * 0.29, -0.01, 1.43), (sign * 0.44, -0.04, 1.20), "Chest"),
            f"Forearm.{side}": ((sign * 0.44, -0.04, 1.20), (sign * 0.50, -0.18, 0.98), f"UpperArm.{side}"),
            f"Hand.{side}": ((sign * 0.50, -0.18, 0.98), (sign * 0.52, -0.26, 0.83), f"Forearm.{side}"),
        })

    arm = bpy.data.armatures.new("MainCharacter_Skeleton")
    rig = bpy.data.objects.new("MainCharacter_Rig", arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, parent) in bones.items():
        b = arm.edit_bones.new(name)
        b.head = head
        b.tail = tail
        if parent:
            b.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.show_in_front = True
    rig["Character"] = "Reborn survivor / main player"
    rig["Forward"] = "+Y in Blender; +Z after Unity FBX import"
    rig["AnimationFPS"] = 30
    return rig, bones


def socket(name, location, parent, purpose):
    empty = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(empty)
    empty.location = location
    empty.parent = parent
    empty.empty_display_type = "ARROWS"
    empty.empty_display_size = 0.09
    empty["purpose"] = purpose
    return empty


def make_character():
    clear_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.fps = 30
    rig, bones = create_rig()

    skin = material("Hero • warm skin", (0.48, 0.25, 0.16), metallic=0.0, roughness=0.72)
    skin_light = material("Hero • skin highlights", (0.66, 0.38, 0.23), metallic=0.0, roughness=0.67)
    jacket = material("Hero • faded petrol jacket", (0.055, 0.18, 0.22), metallic=0.24, roughness=0.60)
    jacket_light = material("Hero • jacket panels", (0.09, 0.29, 0.31), metallic=0.20, roughness=0.55)
    shirt = material("Hero • rust shirt", (0.40, 0.10, 0.055), metallic=0.08, roughness=0.73)
    pants = material("Hero • charcoal cargo pants", (0.085, 0.10, 0.13), metallic=0.05, roughness=0.78)
    pants_light = material("Hero • cargo seams", (0.16, 0.18, 0.20), metallic=0.08, roughness=0.70)
    boot = material("Hero • dark boots", (0.075, 0.045, 0.035), metallic=0.12, roughness=0.78)
    rubber = material("Hero • rubber soles", (0.025, 0.028, 0.03), metallic=0.02, roughness=0.88)
    metal = material("Hero • worn hardware", (0.40, 0.31, 0.15), metallic=0.72, roughness=0.38)
    visor = material("Hero • amber visor", (0.70, 0.30, 0.055), metallic=0.42, roughness=0.22)
    glove = material("Hero • gloves", (0.055, 0.065, 0.070), metallic=0.22, roughness=0.68)
    white = material("Hero • eye white", (0.76, 0.70, 0.52), metallic=0.02, roughness=0.54)
    hair = material("Hero • hair", (0.028, 0.025, 0.024), metallic=0.02, roughness=0.80)

    # Feet and legs: broad readable silhouette for the top-down camera.
    for side, sign in (("L", 1), ("R", -1)):
        thigh = f"Thigh.{side}"
        shin = f"Shin.{side}"
        foot = f"Foot.{side}"
        box(f"Cargo thigh {side}", (sign * 0.14, 0.0, 0.68), (0.22, 0.26, 0.49), pants, thigh, rotation=(math.radians(2), 0.0, 0.0), bevel=0.035)
        box(f"Knee pad {side}", (sign * 0.15, -0.13, 0.50), (0.20, 0.055, 0.16), pants_light, shin, bevel=0.018)
        box(f"Cargo shin {side}", (sign * 0.17, 0.0, 0.30), (0.19, 0.24, 0.38), pants, shin, bevel=0.028)
        box(f"Boot sole {side}", (sign * 0.17, -0.08, 0.045), (0.27, 0.38, 0.075), rubber, foot, bevel=0.025)
        box(f"Boot upper {side}", (sign * 0.17, -0.085, 0.13), (0.26, 0.34, 0.18), boot, foot, bevel=0.035)
        for k in range(3):
            box(f"Boot lace {side} {k}", (sign * 0.17, -0.18 + k * 0.055, 0.19), (0.16, 0.018, 0.018), pants_light, foot, bevel=0.004)
        box(f"Cargo pocket {side}", (sign * 0.245, 0.02, 0.72), (0.045, 0.20, 0.20), pants_light, thigh, bevel=0.008)

    # Torso, jacket, scarf, belt and equipment.
    box("Pelvis", (0.0, 0.0, 0.98), (0.42, 0.31, 0.24), pants, "Pelvis", bevel=0.045)
    box("Jacket torso", (0.0, -0.01, 1.28), (0.56, 0.35, 0.55), jacket, "Spine", bevel=0.055)
    box("Jacket lower hem", (0.0, -0.02, 1.06), (0.52, 0.33, 0.08), jacket_light, "Spine", bevel=0.014)
    box("Shirt opening", (0.0, -0.20, 1.35), (0.20, 0.035, 0.37), shirt, "Chest", bevel=0.008)
    box("Chest plate", (0.0, -0.208, 1.43), (0.24, 0.028, 0.17), jacket_light, "Chest", bevel=0.012)
    box("Belt", (0.0, -0.01, 1.02), (0.55, 0.36, 0.06), boot, "Pelvis", bevel=0.028)
    box("Belt buckle", (0.0, -0.205, 1.03), (0.11, 0.028, 0.08), metal, "Pelvis", bevel=0.012)
    box("Radio", (-0.21, -0.21, 1.23), (0.10, 0.06, 0.17), metal, "Spine", bevel=0.012)
    box("Radio antenna", (-0.21, -0.22, 1.37), (0.018, 0.018, 0.20), metal, "Spine", bevel=0.004)
    box("Shoulder pad L", (0.30, -0.02, 1.48), (0.16, 0.31, 0.13), jacket_light, "Chest", bevel=0.025)
    box("Shoulder pad R", (-0.30, -0.02, 1.48), (0.16, 0.31, 0.13), jacket_light, "Chest", bevel=0.025)
    box("Scarf", (0.0, -0.18, 1.59), (0.31, 0.08, 0.12), shirt, "Neck", bevel=0.018)
    box("Scarf tail", (0.16, -0.15, 1.47), (0.09, 0.06, 0.29), shirt, "Chest", rotation=(math.radians(-12), 0.0, math.radians(-5)), bevel=0.012)

    # Arms: jacket sleeves, gloves, and chunky forearm bracers.
    for side, sign in (("L", 1), ("R", -1)):
        upper = f"UpperArm.{side}"
        fore = f"Forearm.{side}"
        hand = f"Hand.{side}"
        box(f"Sleeve {side}", (sign * 0.37, -0.04, 1.33), (0.19, 0.26, 0.40), jacket, upper, rotation=(0.0, math.radians(sign * 5), math.radians(sign * 4)), bevel=0.042)
        box(f"Elbow pad {side}", (sign * 0.45, -0.10, 1.20), (0.16, 0.21, 0.16), jacket_light, fore, bevel=0.022)
        box(f"Forearm guard {side}", (sign * 0.49, -0.15, 1.05), (0.19, 0.22, 0.29), metal, fore, rotation=(math.radians(-8), math.radians(sign * 4), 0.0), bevel=0.028)
        box(f"Glove {side}", (sign * 0.52, -0.25, 0.88), (0.18, 0.18, 0.20), glove, hand, bevel=0.035)
        for i in range(3):
            box(f"Glove finger {side} {i}", (sign * (0.47 + i * 0.035), -0.34, 0.82), (0.035, 0.13, 0.06), glove, hand, rotation=(math.radians(-18), 0.0, math.radians(sign * 4)), bevel=0.012)
        box(f"Wrist strap {side}", (sign * 0.52, -0.20, 0.98), (0.20, 0.055, 0.07), shirt, hand, bevel=0.008)

    # Head and face: cap, mask, eyes and a strong hero read from above.
    sphere("Head", (0.0, -0.03, 1.79), (0.22, 0.20, 0.24), skin, "Head", vertices=16, rings=10)
    box("Hair back", (0.0, 0.08, 1.91), (0.34, 0.25, 0.16), hair, "Head", bevel=0.04)
    box("Cap crown", (0.0, -0.02, 1.99), (0.34, 0.30, 0.16), jacket, "Head", bevel=0.045)
    box("Cap brim", (0.0, -0.20, 1.96), (0.38, 0.18, 0.055), jacket_light, "Head", rotation=(math.radians(-4), 0.0, 0.0), bevel=0.018)
    box("Face mask", (0.0, -0.195, 1.77), (0.27, 0.06, 0.16), glove, "Head", bevel=0.020)
    for sign in (-1, 1):
        sphere(f"Eye {sign}", (sign * 0.082, -0.218, 1.84), (0.038, 0.018, 0.026), visor, "Head", vertices=10, rings=6)
        box(f"Eye guard {sign}", (sign * 0.082, -0.225, 1.84), (0.085, 0.018, 0.055), metal, "Head", bevel=0.008)
    box("Mask filter", (0.0, -0.235, 1.74), (0.09, 0.025, 0.055), metal, "Head", bevel=0.008)
    sphere("Ear L", (0.22, -0.03, 1.80), (0.045, 0.035, 0.07), skin_light, "Head", vertices=10, rings=6)
    sphere("Ear R", (-0.22, -0.03, 1.80), (0.045, 0.035, 0.07), skin_light, "Head", vertices=10, rings=6)

    # Rig-facing attachment points. Unity receives +Z as forward after FBX axis conversion.
    socket("WeaponSocket_R", (0.52, -0.28, 0.92), rig, "primary weapon hand")
    socket("WeaponSocket_L", (0.52, -0.28, 0.92), rig, "support weapon hand")
    socket("BackSocket", (0.0, 0.13, 1.25), rig, "holstered weapon or backpack")
    socket("HeadSocket", (0.0, -0.03, 2.10), rig, "head accessory")

    # Join all authored pieces into one skinned mesh while preserving bone groups/materials.
    parts = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    mesh = bpy.context.object
    mesh.name = "MainCharacter_Body"
    mesh.parent = rig
    modifier = mesh.modifiers.new("Main character skeletal deformation", "ARMATURE")
    modifier.object = rig
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(island_margin=0.015)
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig, mesh


def worldrot(rig, name, xyz):
    bone = rig.pose.bones[name]
    q = Quaternion((1, 0, 0), math.radians(xyz[0])) @ Quaternion((0, 1, 0), math.radians(xyz[1])) @ Quaternion((0, 0, 1), math.radians(xyz[2]))
    local = bone.bone.matrix_local.to_quaternion()
    bone.rotation_quaternion = local.inverted() @ q @ local


def pose_frame(rig, mesh, frame, rotations=None, root=(0.0, 0.0, 0.0)):
    rotations = rotations or {}
    bpy.context.scene.frame_set(frame)
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion()
        bone.location = (0.0, 0.0, 0.0)
    for name, xyz in rotations.items():
        worldrot(rig, name, xyz)
    rig.pose.bones["Root"].location = root
    for bone in rig.pose.bones:
        bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert(data_path="location", frame=frame, group=bone.name)


def start_clip(rig, name):
    rig.animation_data_create()
    rig.animation_data.action = bpy.data.actions.new(name)
    rig.animation_data.action.use_fake_user = True


def finish_clip(rig, name, frames, loop):
    action = rig.animation_data.action
    for fcurve in action.fcurves:
        for key in fcurve.keyframe_points:
            key.interpolation = "BEZIER"
    action["clip_name"] = name
    action["loop"] = loop
    action["frames"] = frames
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, action)
    strip.mute = True
    rig.animation_data.action = None


def build_animations(rig, mesh):
    clips = {}

    start_clip(rig, "Idle")
    for frame, sway in ((1, 0.0), (16, 1.0), (31, 0.0), (46, -1.0), (61, 0.0)):
        pose_frame(rig, mesh, frame, {
            "Spine": (0.8 * sway, 0.0, 1.5 * sway),
            "Chest": (-0.8 * sway, 0.0, -1.8 * sway),
            "Head": (0.5 * sway, 0.0, -1.0 * sway),
            "UpperArm.L": (1.4 * sway, 0.0, -1.5 * sway),
            "UpperArm.R": (-1.4 * sway, 0.0, -1.5 * sway),
            "Forearm.L": (-2.0 * sway, 0.0, 0.0),
            "Forearm.R": (2.0 * sway, 0.0, 0.0),
        })
    finish_clip(rig, "Idle", 61, True)
    clips["Idle"] = {"frames": 61, "seconds": 2.0, "loop": True}

    start_clip(rig, "Walk")
    for frame in range(1, 37, 3):
        t = (frame - 1) / 33.0 * math.tau
        s, c = math.sin(t), math.cos(t)
        pose_frame(rig, mesh, frame, {
            "Pelvis": (0.0, 2.0 * s, 2.0 * s),
            "Spine": (3.0 * c, 0.0, -2.0 * s),
            "Chest": (-2.0 * c, 0.0, 2.5 * s),
            "Head": (1.0 * c, 0.0, -1.5 * s),
            "Thigh.L": (25.0 * s, 0.0, 0.0),
            "Thigh.R": (-25.0 * s, 0.0, 0.0),
            "Shin.L": (-18.0 * max(0.0, -s), 0.0, 0.0),
            "Shin.R": (-18.0 * max(0.0, s), 0.0, 0.0),
            "Foot.L": (-10.0 * s, 0.0, 0.0),
            "Foot.R": (10.0 * s, 0.0, 0.0),
            "UpperArm.L": (-18.0 * s, 0.0, -5.0),
            "UpperArm.R": (18.0 * s, 0.0, 5.0),
            "Forearm.L": (-15.0, 0.0, 0.0),
            "Forearm.R": (-15.0, 0.0, 0.0),
        }, (0.0, 0.0, 0.006 * math.cos(t * 2.0)))
    finish_clip(rig, "Walk", 36, True)
    clips["Walk"] = {"frames": 36, "seconds": 1.17, "loop": True}

    start_clip(rig, "Run")
    for frame in range(1, 25, 2):
        t = (frame - 1) / 22.0 * math.tau
        s, c = math.sin(t), math.cos(t)
        pose_frame(rig, mesh, frame, {
            "Pelvis": (0.0, 3.0 * s, 3.0 * s),
            "Spine": (7.0 * c, 0.0, -3.0 * s),
            "Chest": (-5.0 * c, 0.0, 4.5 * s),
            "Head": (2.0 * c, 0.0, -2.0 * s),
            "Thigh.L": (34.0 * s, 0.0, 0.0),
            "Thigh.R": (-34.0 * s, 0.0, 0.0),
            "Shin.L": (-32.0 * max(0.0, -s), 0.0, 0.0),
            "Shin.R": (-32.0 * max(0.0, s), 0.0, 0.0),
            "Foot.L": (-18.0 * s, 0.0, 0.0),
            "Foot.R": (18.0 * s, 0.0, 0.0),
            "UpperArm.L": (-28.0 * s, 0.0, -8.0),
            "UpperArm.R": (28.0 * s, 0.0, 8.0),
            "Forearm.L": (-28.0, 0.0, 0.0),
            "Forearm.R": (-28.0, 0.0, 0.0),
        }, (0.0, 0.0, 0.012 * math.cos(t * 2.0)))
    finish_clip(rig, "Run", 24, True)
    clips["Run"] = {"frames": 24, "seconds": 0.77, "loop": True}

    start_clip(rig, "Aim")
    for frame, k in ((1, 0.0), (8, 1.0), (24, 1.0), (36, 0.0)):
        pose_frame(rig, mesh, frame, {
            "Spine": (-3.0 * k, 0.0, 0.0),
            "Chest": (-4.0 * k, 0.0, 0.0),
            "Head": (2.0 * k, 0.0, 0.0),
            "UpperArm.L": (-58.0 * k, 0.0, -18.0 * k),
            "UpperArm.R": (-62.0 * k, 0.0, 12.0 * k),
            "Forearm.L": (-50.0 * k, 0.0, 0.0),
            "Forearm.R": (-50.0 * k, 0.0, 0.0),
        })
    finish_clip(rig, "Aim", 36, True)
    clips["Aim"] = {"frames": 36, "seconds": 1.17, "loop": True}

    start_clip(rig, "Fire")
    for frame, kick in ((1, 0.0), (3, 1.0), (6, 0.45), (12, 0.0)):
        pose_frame(rig, mesh, frame, {
            "Spine": (-4.0 * kick, 0.0, 0.0),
            "Chest": (-7.0 * kick, 0.0, 0.0),
            "Head": (-4.0 * kick, 0.0, 0.0),
            "UpperArm.L": (-62.0 - 7.0 * kick, 0.0, -18.0),
            "UpperArm.R": (-66.0 - 10.0 * kick, 0.0, 12.0),
            "Forearm.L": (-50.0, 0.0, 0.0),
            "Forearm.R": (-50.0, 0.0, 0.0),
        })
    finish_clip(rig, "Fire", 12, False)
    clips["Fire"] = {"frames": 12, "seconds": 0.37, "loop": False}

    start_clip(rig, "Reload")
    for frame, k in ((1, 0.0), (12, 1.0), (28, 1.0), (44, 0.4), (58, 0.0)):
        pose_frame(rig, mesh, frame, {
            "Spine": (4.0 * k, 0.0, -12.0 * k),
            "Chest": (5.0 * k, 0.0, -16.0 * k),
            "Head": (-3.0 * k, 0.0, 10.0 * k),
            "UpperArm.L": (-70.0 * k, 0.0, -42.0 * k),
            "UpperArm.R": (-72.0 * k, 0.0, 32.0 * k),
            "Forearm.L": (-66.0 * k, 0.0, 0.0),
            "Forearm.R": (-60.0 * k, 0.0, 0.0),
        })
    finish_clip(rig, "Reload", 58, False)
    clips["Reload"] = {"frames": 58, "seconds": 1.9, "loop": False}

    start_clip(rig, "GetShot")
    for frame, k in ((1, 0.0), (4, 1.0), (9, 0.65), (18, 0.1), (24, 0.0)):
        pose_frame(rig, mesh, frame, {
            "Spine": (-14.0 * k, 0.0, -8.0 * k),
            "Chest": (-10.0 * k, 0.0, 0.0),
            "Head": (-18.0 * k, 8.0 * k, 0.0),
            "UpperArm.L": (-24.0 * k, 0.0, -25.0 * k),
            "UpperArm.R": (-22.0 * k, 0.0, 25.0 * k),
        }, (0.0, 0.035 * k, -0.02 * k))
    finish_clip(rig, "GetShot", 24, False)
    clips["GetShot"] = {"frames": 24, "seconds": 0.77, "loop": False}

    start_clip(rig, "Stagger")
    for frame, k, side in ((1, 0.0, 0.0), (8, 1.0, -1.0), (18, 0.75, 1.0), (30, 0.4, -0.6), (44, 0.0, 0.0)):
        pose_frame(rig, mesh, frame, {
            "Spine": (-18.0 * k, side * 7.0, side * 9.0),
            "Chest": (-10.0 * k, 0.0, -side * 7.0),
            "Head": (-16.0 * k, side * 10.0, side * 12.0),
            "UpperArm.L": (-28.0 * k, 0.0, -34.0 * k),
            "UpperArm.R": (-24.0 * k, 0.0, 36.0 * k),
            "Thigh.L": (17.0 * side * k, 0.0, 0.0),
            "Thigh.R": (-17.0 * side * k, 0.0, 0.0),
        }, (0.025 * side, 0.045 * k, -0.05 * k))
    finish_clip(rig, "Stagger", 44, False)
    clips["Stagger"] = {"frames": 44, "seconds": 1.43, "loop": False}

    start_clip(rig, "Death")
    for frame, fall, k, z in ((1, 0.0, 0.0, 0.0), (10, 16.0, 1.0, -0.08), (22, 58.0, 1.0, -0.18), (32, 82.0, 0.65, 0.03), (45, 88.0, 0.2, 0.18), (58, 88.0, 0.0, 0.17)):
        pose_frame(rig, mesh, frame, {
            "Spine": (20.0 * k, 0.0, 0.0),
            "Chest": (18.0 * k, 0.0, 0.0),
            "Head": (-18.0 * k, 0.0, 10.0 * min(frame / 32.0, 1.0)),
            "Thigh.L": (25.0 * k, 0.0, 0.0),
            "Thigh.R": (22.0 * k, 0.0, 0.0),
            "Shin.L": (-48.0 * k, 0.0, 0.0),
            "Shin.R": (-42.0 * k, 0.0, 0.0),
            "UpperArm.L": (-32.0 * k, 0.0, -28.0 * min(frame / 32.0, 1.0)),
            "UpperArm.R": (-28.0 * k, 0.0, 40.0 * min(frame / 32.0, 1.0)),
        }, (0.0, 0.0, z))
    finish_clip(rig, "Death", 58, False)
    clips["Death"] = {"frames": 58, "seconds": 1.9, "loop": False}
    return clips


def preview_scene(scene, mesh, rig):
    preview = bpy.data.collections.new("Preview_Only")
    scene.collection.children.link(preview)
    ground_mat = material("Preview ground", (0.025, 0.035, 0.045), metallic=0.08, roughness=0.92)
    bpy.ops.mesh.primitive_plane_add(size=6.0, location=(0.0, 0.0, -0.02))
    ground = bpy.context.object
    ground.name = "PreviewGround"
    ground.data.materials.append(ground_mat)
    for collection in list(ground.users_collection):
        collection.objects.unlink(ground)
    preview.objects.link(ground)
    bpy.ops.object.camera_add(location=(3.2, -5.2, 2.9))
    camera = bpy.context.object
    camera.name = "PreviewCamera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.45
    camera.rotation_euler = (Vector((0.0, -0.02, 1.00)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    for collection in list(camera.users_collection):
        collection.objects.unlink(camera)
    preview.objects.link(camera)
    scene.camera = camera
    for name, location, color, energy, size in (
        ("Key", (3.0, -3.0, 4.5), (1.0, 0.78, 0.55), 500.0, 3.0),
        ("Fill", (-3.0, -2.0, 2.2), (0.35, 0.55, 1.0), 320.0, 2.4),
        ("Rim", (1.0, 3.2, 3.8), (0.60, 0.95, 0.85), 600.0, 2.5),
    ):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.color = color
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (Vector((0.0, -0.02, 1.0)) - light.location).to_track_quat("-Z", "Y").to_euler()
        for collection in list(light.users_collection):
            collection.objects.unlink(light)
        preview.objects.link(light)
    return preview


def main():
    rig, mesh = make_character()
    clips = build_animations(rig, mesh)
    scene = bpy.context.scene
    preview = preview_scene(scene, mesh, rig)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 820
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world.color = (0.012, 0.016, 0.025)
    os.makedirs(BASE, exist_ok=True)
    os.makedirs(UNITY_OUT, exist_ok=True)
    scene.frame_start = 1
    scene.frame_end = 58
    rig.animation_data.action = bpy.data.actions["Idle"]
    scene.frame_set(16)
    scene.render.filepath = os.path.join(BASE, "MainCharacter_Preview.png")
    bpy.ops.render.render(write_still=True)

    preview.hide_viewport = True
    preview.hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BASE, "MainCharacter.blend"))

    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    for track in rig.animation_data.nla_tracks:
        track.mute = False
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(UNITY_OUT, "MainCharacter.fbx"),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        bake_anim_simplify_factor=0.0,
        apply_scale_options="FBX_SCALE_UNITS",
        path_mode="COPY",
    )
    for track in rig.animation_data.nla_tracks:
        track.mute = True
    rig.animation_data.action = bpy.data.actions["Idle"]
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(UNITY_OUT, "MainCharacter.glb"),
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_animation_mode="NLA_TRACKS",
        export_nla_strips=True,
        export_materials="EXPORT",
    )
    report = {
        "character": "MainCharacter",
        "style": "chunky stylized survival shooter hero",
        "forward_axis": "+Y in Blender, +Z after Unity FBX import",
        "mesh_objects": 1,
        "bones": len(rig.data.bones),
        "attachments": ["WeaponSocket_R", "WeaponSocket_L", "BackSocket", "HeadSocket"],
        "clips": clips,
    }
    with open(os.path.join(BASE, "asset_report.json"), "w") as handle:
        json.dump(report, handle, indent=2)
    with open(os.path.join(UNITY_OUT, "README.md"), "w") as handle:
        handle.write(
            "# MainCharacter\n\n"
            "Chunky stylized survivor hero authored for Reborn's top-down survival shooter. "
            "The model uses +Y as forward in Blender and imports with +Z as forward in Unity.\n\n"
            "## Files\n\n"
            "- `MainCharacter.fbx` — Unity skeletal import with baked animation clips.\n"
            "- `MainCharacter.glb` — portable glTF export with embedded materials and animations.\n"
            "- `MainCharacter.blend` — editable Blender source in ArtSource/MainCharacter.\n\n"
            "## Animation clips\n\n"
            "`Idle`, `Walk`, `Run`, `Aim`, `Fire`, `Reload`, `GetShot`, `Stagger`, and `Death`. "
            "Root motion is authored in place so PlayerController remains responsible for movement.\n\n"
            "## Attachment points\n\n"
            "`WeaponSocket_R`, `WeaponSocket_L`, `BackSocket`, and `HeadSocket` are exported as "
            "empty transforms for prefab wiring.\n"
        )
    print("MAIN_CHARACTER_BUILD_COMPLETE", len(rig.data.bones), len(clips), flush=True)


main()
