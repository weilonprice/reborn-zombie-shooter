"""Render motion previews. Preview-only transforms illustrate the game's spin/vault."""
import bpy
import math
import os
from mathutils import Vector

BASE = os.path.dirname(os.path.abspath(__file__))
FRAMES = '/tmp/zombie-ability-preview-frames'
os.makedirs(FRAMES, exist_ok=True)
for pack, names in [('ZombieAbilities', [('Scream', 1.5), ('GetUp', 2.2), ('Leap', .65)]),
                    ('SurvivorAbilities', [('Ultimate', 2 / 3)])]:
    bpy.ops.wm.open_mainfile(filepath=os.path.join(BASE, pack + '.blend'))
    scene = bpy.context.scene
    rig = next(o for o in scene.objects if o.type == 'ARMATURE')
    for track in rig.animation_data.nla_tracks:
        track.mute = True
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 8
    scene.render.resolution_x = 360
    scene.render.resolution_y = 400
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    camera = scene.camera
    camera.data.type = 'ORTHO'
    for name, seconds in names:
        rig.animation_data.action = bpy.data.actions[name]
        camera.data.ortho_scale = 4.5 if name == 'Leap' else 3.1
        camera.location = (3, -5, 3)
        aim = Vector((0, -.25, 1.4 if name == 'Leap' else 1))
        camera.rotation_euler = (aim - camera.location).to_track_quat('-Z', 'Y').to_euler()
        for i in range(round(seconds * 24) + 1):
            t = min(seconds, i / 24)
            scene.frame_set(1 + round(t * 60))
            rig.location = (0, 0, 1.6 * math.sin(t / seconds * math.pi) if name == 'Leap' else 0)
            rig.rotation_euler = (0, 0, math.radians(-540 * t) if name == 'Ultimate' else 0)
            scene.render.filepath = os.path.join(FRAMES, f'{name}_{i:03}.png')
            bpy.ops.render.render(write_still=True)
print('ABILITY_PREVIEW_FRAMES_COMPLETE', flush=True)
