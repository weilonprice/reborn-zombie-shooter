"""Author four ability clips on the existing rigs; leave the base character files intact.

Run with Blender --background --python ArtSource/AbilityAnimations/build_ability_animations.py.
The two exports include the reference skin for portable previews. Unity uses their clips
on the original model hierarchies, so there is no additional mesh at runtime.
"""
import bpy
import json
import math
import os
from mathutils import Vector, Quaternion

BASE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(BASE)
OUT = os.path.abspath(os.path.join(ART, '../Assets/_Project/Art/Animations/Abilities'))
FPS = 60


def load_character(folder, name):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(ART, folder, name + '.blend'))
    rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    mesh = next(o for o in rig.children if o.type == 'MESH')
    rig.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    rig.animation_data_create()
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.render.fps_base = 1
    for obj in scene.objects:
        obj.hide_set(False)
    # The survivor's preview collection was saved hidden in its source file.
    for collection in bpy.data.collections:
        collection.hide_render = False
        collection.hide_viewport = False
    return rig, mesh


def pose(rig, mesh, frame, rotations, ground=True):
    bpy.context.scene.frame_set(frame)
    for bone in rig.pose.bones:
        bone.rotation_mode = 'QUATERNION'
        bone.rotation_quaternion = Quaternion()
        bone.location = (0, 0, 0)
        bone.scale = (1, 1, 1)
    for name, angles in rotations.items():
        bone = rig.pose.bones[name]
        q = (Quaternion((1, 0, 0), math.radians(angles[0])) @
             Quaternion((0, 1, 0), math.radians(angles[1])) @
             Quaternion((0, 0, 1), math.radians(angles[2])))
        basis = bone.bone.matrix_local.to_quaternion()
        bone.rotation_quaternion = basis.inverted() @ q @ basis
    if ground:
        bpy.context.view_layer.update()
        evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        low = min((evaluated.matrix_world @ v.co).z for v in evaluated.data.vertices)
        root = rig.pose.bones['Root']
        root.location = root.bone.matrix_local.to_3x3().inverted() @ Vector((0, 0, .01 - low))
    # Leap deliberately has no root lift: BarricadeLeaper owns its world-space arc.
    for bone in rig.pose.bones:
        bone.keyframe_insert('rotation_quaternion', frame=frame, group=bone.name)
        bone.keyframe_insert('location', frame=frame, group=bone.name)


READY = {'Spine': (10, 0, 0), 'Chest': (5, 0, 0), 'Head': (-10, 0, 0),
         'UpperArm.L': (-24, 0, -8), 'UpperArm.R': (-32, 0, 11),
         'Forearm.L': (-28, 0, 0), 'Forearm.R': (-39, 0, 0)}


def authored_clip(rig, mesh, name, anchors, loop=False, ground=True):
    action = bpy.data.actions.new(name)
    rig.animation_data.action = action
    action.use_fake_user = True
    action['loop'] = loop
    action['seconds'] = (anchors[-1][0] - 1) / FPS
    for frame in range(1, anchors[-1][0] + 1):
        for i in range(len(anchors) - 1):
            f0, a = anchors[i]
            f1, b = anchors[i + 1]
            if f0 <= frame <= f1:
                t = (frame - f0) / (f1 - f0)
                t = t * t * (3 - 2 * t)
                angles = {bone: tuple(x + (y - x) * t for x, y in zip(
                    a.get(bone, (0, 0, 0)), b.get(bone, (0, 0, 0))))
                    for bone in set(a) | set(b)}
                pose(rig, mesh, frame, angles, ground)
                break
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    track.strips.new(name, 1, action)
    track.mute = True
    rig.animation_data.action = None
    return {'name': name, 'seconds': action['seconds'], 'frames': anchors[-1][0],
            'fps': FPS, 'loop': loop, 'root_motion': False}


def zombies(rig, mesh):
    inhale = dict(READY, Spine=(24, 0, 0), Chest=(12, 0, 0), Head=(8, 0, 0))
    inhale.update({'UpperArm.L': (-48, 0, -22), 'UpperArm.R': (-48, 0, 22),
                   'Forearm.L': (-55, 0, 0), 'Forearm.R': (-55, 0, 0)})
    howl = {'Spine': (-15, 0, 0), 'Chest': (-16, 0, 0), 'Neck': (-12, 0, 0),
            'Head': (-28, 0, 0), 'UpperArm.L': (-78, 0, -42), 'UpperArm.R': (-78, 0, 42),
            'Forearm.L': (-58, 0, 0), 'Forearm.R': (-58, 0, 0),
            'Hand.L': (12, 0, -18), 'Hand.R': (12, 0, 18),
            'Thigh.L': (8, 0, -7), 'Thigh.R': (8, 0, 7),
            'Shin.L': (-10, 0, 0), 'Shin.R': (-10, 0, 0)}
    pulse = dict(howl, Chest=(-22, 0, 4), Head=(-34, 0, -4))
    result = [authored_clip(rig, mesh, 'Scream', [
        (1, READY), (15, inhale), (30, howl), (40, pulse), (49, howl),
        (58, pulse), (67, howl), (76, inhale), (91, READY)])]

    crumple = {'Spine': (35, 0, 8), 'Chest': (20, 0, 0), 'Head': (18, 0, 0),
                'Thigh.L': (60, 0, 0), 'Thigh.R': (48, 0, 0),
                'Shin.L': (-115, 0, 0), 'Shin.R': (-105, 0, 0),
                'UpperArm.L': (-25, 0, -30), 'UpperArm.R': (-30, 0, 30)}
    prone = {'Root': (88, 0, -8), 'Head': (-5, 0, 20),
             'UpperArm.L': (25, 0, -15), 'UpperArm.R': (20, 0, 20),
             'Forearm.L': (15, 0, 0), 'Forearm.R': (15, 0, 0)}
    twitch = dict(prone, Head=(-22, 0, -8), Chest=(-10, 0, 0))
    push = {'Root': (50, 0, -5), 'Spine': (-15, 0, 10), 'Head': (-28, 0, 0),
            'UpperArm.L': (-45, 0, -30), 'UpperArm.R': (-50, 0, 25),
            'Forearm.L': (-80, 0, 0), 'Forearm.R': (-85, 0, 0),
            'Thigh.L': (55, 0, 0), 'Thigh.R': (15, 0, 0), 'Shin.L': (-95, 0, 0)}
    kneel = {'Spine': (18, 0, -8), 'Chest': (-10, 0, 0), 'Head': (-20, 0, 0),
             'Thigh.L': (78, 0, 0), 'Thigh.R': (15, 0, 0),
             'Shin.L': (-90, 0, 0), 'Shin.R': (-125, 0, 0),
             'UpperArm.L': (-45, 0, -12), 'UpperArm.R': (-20, 0, 18),
             'Forearm.L': (-40, 0, 0), 'Forearm.R': (-60, 0, 0)}
    result.append(authored_clip(rig, mesh, 'GetUp', [
        (1, READY), (13, crumple), (27, prone), (43, prone), (53, twitch),
        (75, push), (99, kneel), (119, inhale), (133, READY)]))

    coil = dict(crumple, Spine=(20, 0, 0), Chest=(5, 0, 0), Head=(-22, 0, 0))
    tuck = {'Spine': (25, 0, 0), 'Chest': (10, 0, 0), 'Head': (-30, 0, 0),
            'Thigh.L': (90, 0, -14), 'Thigh.R': (78, 0, 14),
            'Shin.L': (-135, 0, 0), 'Shin.R': (-122, 0, 0),
            'Foot.L': (-15, 0, 0), 'Foot.R': (-15, 0, 0),
            'UpperArm.L': (-105, 0, -20), 'UpperArm.R': (-95, 0, 25),
            'Forearm.L': (-35, 0, 0), 'Forearm.R': (-30, 0, 0)}
    reach = dict(READY, Spine=(18, 0, 0), Head=(-18, 0, 0))
    reach.update({'Thigh.L': (38, 0, 0), 'Thigh.R': (20, 0, 0),
                  'Shin.L': (-48, 0, 0), 'Shin.R': (-35, 0, 0),
                  'UpperArm.L': (-65, 0, -35), 'UpperArm.R': (-65, 0, 35)})
    result.append(authored_clip(rig, mesh, 'Leap', [
        (1, READY), (5, coil), (12, tuck), (23, tuck), (31, reach),
        (36, coil), (40, READY)], ground=False))
    return result


def survivor(rig, mesh):
    # The controller supplies the 540 deg/s spin; this 2/3-second cycle supplies a
    # planted pivot, alternating knee lifts and the open dual-gun silhouette.
    def flourish(side):
        return {'Pelvis': (0, 4 * side, 8 * side), 'Spine': (-6, 0, -12 * side),
                'Chest': (-6, 0, 15 * side), 'Head': (5, 0, -10 * side),
                'UpperArm.L': (-48 - 8 * side, -50, -15),
                'UpperArm.R': (-48 + 8 * side, 50, 15),
                'Forearm.L': (-22 - 9 * side, 0, 0),
                'Forearm.R': (-22 + 9 * side, 0, 0),
                'Hand.L': (0, 0, -8), 'Hand.R': (0, 0, 8),
                'Thigh.L': (15 + 18 * side, 0, -10),
                'Thigh.R': (15 - 18 * side, 0, 10),
                'Shin.L': (-30 - 22 * side, 0, 0),
                'Shin.R': (-30 + 22 * side, 0, 0)}
    return [authored_clip(rig, mesh, 'Ultimate', [
        (1, flourish(0)), (11, flourish(1)), (21, flourish(0)),
        (31, flourish(-1)), (41, flourish(0))], loop=True)]


def export_pack(folder, character, pack, author, proofs):
    rig, mesh = load_character(folder, character)
    clips = author(rig, mesh)
    scene = bpy.context.scene
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.animation_data.action = None
    for track in rig.animation_data.nla_tracks:
        track.mute = False
    scene.frame_start = 1
    scene.frame_end = max(c['frames'] for c in clips)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, pack + '.fbx'),
        use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        axis_forward='-Z', axis_up='Y', bake_anim=True, bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False, bake_anim_simplify_factor=0,
        apply_scale_options='FBX_SCALE_UNITS', path_mode='COPY', embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, pack + '.glb'),
        export_format='GLB', use_selection=True, export_animations=True,
        export_animation_mode='NLA_TRACKS', export_nla_strips=True)
    for track in rig.animation_data.nla_tracks:
        track.mute = True
    rig.animation_data.action = bpy.data.actions[clips[0]['name']]
    scene.frame_end = clips[0]['frames']
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BASE, pack + '.blend'))
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 12
    scene.render.resolution_x = 420
    scene.render.resolution_y = 460
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    camera = scene.camera
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 3.0
    camera.location = (3, -5, 3)
    camera.rotation_euler = (Vector((0, -.25, 1)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    for name, frames in proofs.items():
        rig.animation_data.action = bpy.data.actions[name]
        for frame in frames:
            scene.frame_set(frame)
            scene.render.filepath = os.path.join(BASE, 'proof', f'{name}_{frame:03}.png')
            bpy.ops.render.render(write_still=True)
    return {'pack': pack, 'rig': rig.name, 'bones': len(rig.data.bones), 'clips': clips}


os.makedirs(OUT, exist_ok=True)
os.makedirs(os.path.join(BASE, 'proof'), exist_ok=True)
reports = [export_pack('NormalZombie', 'NormalZombie', 'ZombieAbilities', zombies,
                      {'Scream': [15, 40, 76], 'GetUp': [27, 75, 99], 'Leap': [5, 20, 36]}),
           export_pack('MainCharacter', 'MainCharacter', 'SurvivorAbilities', survivor,
                      {'Ultimate': [1, 11, 31]})]
with open(os.path.join(BASE, 'asset_report.json'), 'w') as handle:
    json.dump(reports, handle, indent=2)
print('ABILITY_ANIMATIONS_COMPLETE', json.dumps(reports), flush=True)
