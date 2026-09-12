"""
Contact sheet of the roster, shot from the angle the game actually uses.

The camera offset is (0, 18, -10): 61 degrees down, 20.6m out. Anything judged
from a level side-on view is judged from a view no player ever gets, so these
renders match the game or they are not worth taking.
"""
import bpy, sys, os, math
from mathutils import Vector

BASE = os.path.dirname(os.path.abspath(__file__))
CHARS = os.path.abspath(os.path.join(BASE, '../../Assets/_Project/Art/Characters'))
names = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.film_transparent = False
scene.render.resolution_x = scene.render.resolution_y = 560
scene.view_settings.view_transform = 'Standard'
world = bpy.data.worlds.new('W'); scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (.13, .15, .17, 1)

ground = bpy.ops.mesh.primitive_plane_add(size=14, location=(0, 0, 0))
gm = bpy.data.materials.new('ground'); gm.use_nodes = True
gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.17, .18, .19, 1)
bpy.context.object.data.materials.append(gm)

key = bpy.data.lights.new('key', 'SUN'); key.energy = 4.0; key.angle = .3
ko = bpy.data.objects.new('key', key); scene.collection.objects.link(ko)
ko.rotation_euler = (math.radians(52), 0, math.radians(35))
fill = bpy.data.lights.new('fill', 'SUN'); fill.energy = 1.4
fo = bpy.data.objects.new('fill', fill); scene.collection.objects.link(fo)
fo.rotation_euler = (math.radians(64), 0, math.radians(-125))

cam_data = bpy.data.cameras.new('cam'); cam = bpy.data.objects.new('cam', cam_data)
scene.collection.objects.link(cam); scene.camera = cam
cam_data.lens = 50

os.makedirs(os.path.join(BASE, 'proof'), exist_ok=True)

for name in names:
    path = os.path.join(CHARS, name, f'{name}.fbx')
    if not os.path.exists(path):
        print('MISSING', path); continue
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    added = [o for o in bpy.data.objects if o not in before]

    # Game camera direction, framed on the character rather than the world origin.
    meshes = [o for o in added if o.type == 'MESH']
    zs = [(o.matrix_world @ Vector(c)).z for o in meshes for c in o.bound_box]
    mid = ((min(zs) + max(zs)) / 2) if zs else 1.0
    focus = Vector((0, 0, mid))
    direction = Vector((0, -10, 18)).normalized()      # Unity (0,18,-10) in Blender axes
    cam.location = focus + direction * 3.5
    cam.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()

    scene.render.filepath = os.path.join(BASE, 'proof', f'{name}.png')
    bpy.ops.render.render(write_still=True)
    print('RENDERED', name)
    for o in added: bpy.data.objects.remove(o, do_unlink=True)
