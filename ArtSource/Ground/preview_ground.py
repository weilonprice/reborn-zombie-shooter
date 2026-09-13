"""Ground under real characters, from the game camera. The only honest test."""
import bpy, os, math
from mathutils import Vector
BASE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.abspath(os.path.join(BASE, "../../Assets/_Project/Art"))

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x, sc.render.resolution_y = 960, 560
sc.view_settings.view_transform = 'Standard'
w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (.13, .15, .17, 1)

# Ground: 40m of it, texture tiled every 24m exactly as the builder will.
bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
plane = bpy.context.object
m = bpy.data.materials.new('ground'); m.use_nodes = True
nt = m.node_tree
tex = nt.nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(os.path.join(ART, 'Ground/Ground_Albedo.png'))
mapping = nt.nodes.new('ShaderNodeMapping'); mapping.inputs['Scale'].default_value = (40/24, 40/24, 1)
coord = nt.nodes.new('ShaderNodeTexCoord')
nt.links.new(coord.outputs['UV'], mapping.inputs['Vector'])
nt.links.new(mapping.outputs['Vector'], tex.inputs['Vector'])
p = nt.nodes.get('Principled BSDF')
nt.links.new(tex.outputs['Color'], p.inputs['Base Color'])
p.inputs['Roughness'].default_value = .92
plane.data.materials.append(m)

key = bpy.data.lights.new('key', 'SUN'); key.energy = 2.1
ko = bpy.data.objects.new('key', key); sc.collection.objects.link(ko)
ko.rotation_euler = (math.radians(52), 0, math.radians(35))
fill = bpy.data.lights.new('fill', 'SUN'); fill.energy = .55
fo = bpy.data.objects.new('fill', fill); sc.collection.objects.link(fo)
fo.rotation_euler = (math.radians(64), 0, math.radians(-125))

def place(path, x, y, scale=1.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    for o in [o for o in bpy.data.objects if o not in before]:
        if o.parent is None:
            o.location = (x, y, 0); o.scale = (scale,)*3

place(os.path.join(ART, 'Characters/MainCharacter/MainCharacter.fbx'), 0, 0)
place(os.path.join(ART, 'Characters/NormalZombie/NormalZombie.fbx'), -2.6, 1.8)
place(os.path.join(ART, 'Characters/Brute/Brute.fbx'), 2.8, 2.2)
place(os.path.join(ART, 'Characters/Crawler/Crawler.fbx'), -1.4, -2.4)
place(os.path.join(ART, 'Characters/Screamer/Screamer.fbx'), 3.2, -1.6)

cam_data = bpy.data.cameras.new('cam'); cam_data.lens = 42
cam = bpy.data.objects.new('cam', cam_data); sc.collection.objects.link(cam); sc.camera = cam
direction = Vector((0, -10, 18)).normalized()     # Unity offset (0,18,-10)
cam.location = Vector((0, 0, 1.0)) + direction * 13.5
cam.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()

sc.render.filepath = os.path.join(BASE, 'GroundPreview.png')
bpy.ops.render.render(write_still=True)
print('RENDERED')
