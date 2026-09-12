"""
Shared authoring kit for the infected roster.

WHY ONE SKELETON FOR TWELVE CHARACTERS
--------------------------------------
Every clip in the game - Chase, Attack, GetShot, Stagger, Death, plus the four
ability clips - is authored once, against the normal zombie. Two facts make
those clips reusable only if the rig underneath never changes:

  * The rigs are Generic, not Humanoid, so Unity binds every curve by its
    transform PATH. A differently named armature object breaks every path at
    once, which is why the armature here is still called NormalZombie_Rig.
    The name is load-bearing, not cosmetic.

  * build_zombie.py's frame() keyframes LOCATION on every bone, not just
    rotation. Baked location curves overwrite rest positions, so a skeleton
    with longer arms would simply be snapped back to the zombie's proportions
    the moment a clip played.

So: identical bones, identical rest pose, twelve different bodies. Proportion
comes from mesh volume and from the uniform scale Unity applies per archetype
(AbilityAnimationSetup scales by controller height / 2.1). Anything that needs
a genuinely different posture needs its own clips, and does not belong here.

READING THE SILHOUETTE
----------------------
The camera sits at offset (0, 18, -10) - 61 degrees down, about 20.6m out. A
character is roughly eighty pixels tall. Height barely reads at that pitch;
plan-view mass does. So archetypes are separated by what they add ACROSS the
shoulders and ABOVE the back, not by how tall they are.
"""
import bpy, math, os, json
from mathutils import Vector
from math import sin, cos, pi

# Matches build_zombie.py. 'hero' restores full resolution for renders.
DETAIL = os.environ.get('ENEMY_DETAIL', 'horde')
if DETAIL == 'hero': SPHERE_SEG, SPHERE_RINGS, TUBE_N, BEVEL_SEG = 16, 10, 12, 2
else:                SPHERE_SEG, SPHERE_RINGS, TUBE_N, BEVEL_SEG = 8, 5, 6, 1

# The shared infected skeleton, copied from build_zombie.py without edits.
# Changing any figure here silently rescales every archetype's animation.
BONES = {
    'Root':   ((0, 0, 0),      (0, 0, .22),      None),
    'Pelvis': ((0, 0, .91),    (0, 0, 1.12),     'Root'),
    'Spine':  ((0, 0, 1.08),   (0, -.10, 1.38),  'Pelvis'),
    'Chest':  ((0, -.10, 1.38),(0, -.17, 1.61),  'Spine'),
    'Neck':   ((0, -.15, 1.59),(0, -.23, 1.73),  'Chest'),
    'Head':   ((0, -.23, 1.73),(0, -.27, 2.04),  'Neck'),
}
for _side, _s in (('L', 1), ('R', -1)):
    BONES.update({
        f'Thigh.{_side}':    ((_s*.145, 0, 1.0),     (_s*.17, -.035, .57),  'Pelvis'),
        f'Shin.{_side}':     ((_s*.17, -.035, .57),  (_s*.18, .01, .15),    f'Thigh.{_side}'),
        f'Foot.{_side}':     ((_s*.18, .01, .15),    (_s*.18, -.19, .09),   f'Shin.{_side}'),
        f'UpperArm.{_side}': ((_s*.30, -.12, 1.51),  (_s*.46, -.17, 1.20),  'Chest'),
        f'Forearm.{_side}':  ((_s*.46, -.17, 1.20),  (_s*.51, -.30, .94),   f'UpperArm.{_side}'),
        f'Hand.{_side}':     ((_s*.51, -.30, .94),   (_s*.52, -.37, .79),   f'Forearm.{_side}'),
    })

ARMATURE_OBJECT = 'NormalZombie_Rig'   # see the note at the top before renaming

_parts = []
_mats = {}


def reset():
    _parts.clear(); _mats.clear()
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.actions, bpy.data.meshes, bpy.data.armatures, bpy.data.materials):
        for item in list(block): block.remove(item)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'; scene.render.fps = 30


def mat(name, color, rough=.83):
    if name in _mats: return _mats[name]
    m = bpy.data.materials.new(name); m.diffuse_color = (*color, 1); m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = rough
    _mats[name] = m
    return m


def _finish(o, name, material, bone):
    o.name = name; o.data.materials.append(material)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for p in o.data.polygons: p.use_smooth = True
    g = o.vertex_groups.new(name=bone)
    g.add(list(range(len(o.data.vertices))), 1, 'REPLACE')
    _parts.append(o)
    return o


def ell(name, c, s, material, bone, seg=SPHERE_SEG, rings=SPHERE_RINGS):
    if DETAIL != 'hero': seg = min(seg, SPHERE_SEG); rings = min(rings, SPHERE_RINGS)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=c)
    o = bpy.context.object; o.scale = s
    return _finish(o, name, material, bone)


def box(name, c, s, material, bone, bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=c)
    o = bpy.context.object; o.scale = s
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = o.modifiers.new('Soft manufactured edges', 'BEVEL')
        mod.width = bevel; mod.segments = BEVEL_SEG
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return _finish(o, name, material, bone)


def tube(name, points, radii, material, bone, other=None, ragged=False, n=TUBE_N):
    if DETAIL != 'hero': n = min(n, TUBE_N)
    pts = [Vector(p) for p in points]
    direction = (pts[-1] - pts[0]).normalized()
    u = direction.cross(Vector((0, 1, 0))).normalized(); v = direction.cross(u).normalized()
    verts = []
    for j, (c, r) in enumerate(zip(pts, radii)):
        rx, ry = r if isinstance(r, tuple) else (r, r)
        for i in range(n):
            t = 2*pi*i/n
            p = c + u*cos(t)*rx + v*sin(t)*ry
            if ragged and j == len(pts)-1:
                p += direction * (.035*sin(i*2.7) + .02*cos(i*4))
            verts.append(p)
    faces = []
    for j in range(len(pts)-1):
        for i in range(n):
            a = j*n + i; b = j*n + (i+1) % n
            faces.append((a, b, b+n, a+n))
    faces.append(tuple(reversed(range(n))))
    faces.append(tuple((len(pts)-1)*n + i for i in range(n)))
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    o = bpy.data.objects.new(name, mesh); bpy.context.collection.objects.link(o)
    o.data.materials.append(material)
    for p in mesh.polygons: p.use_smooth = True
    g = o.vertex_groups.new(name=bone)
    if other:
        h = o.vertex_groups.new(name=other)
        for j in range(len(pts)):
            w = max(0, min(1, (j/(len(pts)-1) - .35) / .5))
            ids = list(range(j*n, (j+1)*n))
            g.add(ids, 1-w, 'REPLACE'); h.add(ids, w, 'REPLACE')
    else:
        g.add(list(range(len(verts))), 1, 'REPLACE')
    _parts.append(o)
    return o


def _bake_palette(mesh, name, out_root):
    """
    Collapse every material into one palette texture and one material, exactly as
    build_zombie.py does for the baseline.

    This is not a nicety. Skinned meshes do not batch and the GPU Resident Drawer does not
    touch them, so each material is its own draw call on every one of up to sixty enemies.
    The first version of this roster shipped five to eight materials per archetype - a
    seven-fold regression against the baseline, hidden because a flat-coloured model and a
    textured one look similar in a render and nothing compared them as assets.

    It is also why comparing rendered brightness against the zombie was meaningless: the
    zombie's colour lives in a texture, and these had theirs in Principled base colours.
    """
    cells, size = 4, 16
    colors = []
    for slot in mesh.data.materials:
        rgb = (0.1, 0.1, 0.1)
        if slot and slot.use_nodes:
            node = slot.node_tree.nodes.get('Principled BSDF')
            if node: rgb = tuple(node.inputs['Base Color'].default_value[:3])
        colors.append((*rgb, 1.0))

    width = cells * size
    pixels = []
    for y in range(width):
        for x in range(width):
            index = (y // size) * cells + x // size
            pixels.extend(colors[index] if index < len(colors) else (0.1, 0.1, 0.1, 1))

    out = os.path.join(out_root, name)
    os.makedirs(out, exist_ok=True)
    tex = bpy.data.images.new(f'{name}_Palette', width=width, height=width)
    tex.pixels = pixels
    tex.filepath_raw = os.path.join(out, f'{name}_Palette.png')
    tex.file_format = 'PNG'; tex.save(); tex.pack()

    uv = mesh.data.uv_layers.new(name='palette')
    for face in mesh.data.polygons:
        i = face.material_index
        point = ((i % cells + .5) / cells, (i // cells + .5) / cells)
        for loop in face.loop_indices: uv.data[loop].uv = point
    # Drop the smart-project unwrap. active_render does NOT survive the FBX round trip -
    # the importer marks the FIRST layer as the render one - so leaving both layers in place
    # sampled the sixteen-cell palette with a full unwrap and scattered the colours at
    # random. The baseline zombie has carried exactly this bug since it was authored; see
    # build_zombie.py, fixed in the same commit. One UV layer, no ambiguity.
    # Remove by NAME and re-fetch afterwards: removing a UV layer invalidates every other
    # layer pointer, and setting flags through a stale one exported a mesh with no UVs at all.
    while len(mesh.data.uv_layers) > 1:
        stale = next(l for l in mesh.data.uv_layers if l.name != 'palette')
        mesh.data.uv_layers.remove(stale)
    uv = mesh.data.uv_layers['palette']
    mesh.data.uv_layers.active = uv; uv.active_render = True

    material = bpy.data.materials.new(f'{name} - unified palette')
    material.use_nodes = True
    node = material.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = tex; node.interpolation = 'Closest'
    principled = material.node_tree.nodes.get('Principled BSDF')
    principled.inputs['Roughness'].default_value = .83
    material.node_tree.links.new(node.outputs['Color'], principled.inputs['Base Color'])
    mesh.data.materials.clear(); mesh.data.materials.append(material)
    for face in mesh.data.polygons: face.material_index = 0


def export(name, out_root, character=''):
    """Rig, join, unwrap and write <out_root>/<name>/<name>.fbx. Returns a report."""
    bpy.ops.object.select_all(action='DESELECT')
    arm = bpy.data.armatures.new('Infected_Skeleton')
    rig = bpy.data.objects.new(ARMATURE_OBJECT, arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig; rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for bone, (head, tail, parent) in BONES.items():
        b = arm.edit_bones.new(bone); b.head = head; b.tail = tail
        if parent: b.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front = True; rig.select_set(False)

    for o in _parts: o.select_set(True)
    bpy.context.view_layer.objects.active = _parts[0]
    bpy.ops.object.join()
    mesh = bpy.context.object; mesh.name = f'{name}_Body'
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh.parent = rig
    mod = mesh.modifiers.new('Infected skeletal deformation', 'ARMATURE'); mod.object = rig

    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.018); bpy.ops.object.mode_set(mode='OBJECT')

    _bake_palette(mesh, name, out_root)

    rig['Character'] = character or f'Reborn / {name}'
    rig['Forward'] = '-Y in Blender; +Z in FBX'
    rig['AnimationFPS'] = 30

    out = os.path.join(out_root, name)
    os.makedirs(out, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True); mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    # No bake_anim: these carry no clips of their own and borrow the zombie's.
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out, f'{name}.fbx'), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        axis_forward='-Z', axis_up='Y', bake_anim=False,
        apply_scale_options='FBX_SCALE_UNITS', path_mode='COPY', embed_textures=True)

    mesh.data.calc_loop_triangles()
    verts = len(mesh.data.vertices)
    tris = len(mesh.data.loop_triangles)
    lo = min(v.co.z for v in mesh.data.vertices)
    hi = max(v.co.z for v in mesh.data.vertices)
    half_x = max(abs(v.co.x) for v in mesh.data.vertices)
    # Half-width across the band the hit ray crosses, which is what the hitbox is sized to.
    band = [v.co for v in mesh.data.vertices if 0.90 <= v.co.z <= 1.20]
    chest = max((abs(p.x) for p in band), default=0.0)
    return {'name': name, 'vertices': verts, 'triangles': tris, 'bones': len(arm.bones),
            'materials': len(mesh.data.materials), 'height': round(hi - lo, 3),
            'half_width': round(half_x, 3), 'chest_half_width': round(chest, 3)}
