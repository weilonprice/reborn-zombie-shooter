import bpy, math, os, json
from mathutils import Vector, Quaternion
from math import sin, cos, pi
BASE=os.path.dirname(os.path.abspath(__file__))

# Horde detail budget.
#
# This character is the BASELINE archetype: spawn weight 10, never falls off, so most of the
# sixty enemies alive at wave 15 are this mesh. The camera sits about 23m out, which makes it
# roughly eighty pixels tall on a 1080p screen.
#
# Resolution is therefore lowered at GENERATION time rather than by decimating a finished
# mesh - primitives built coarse stay clean, decimated ones do not. And a Unity LODGroup
# would achieve nothing here: the top-down camera never changes distance, so every zombie
# would sit on the same LOD level forever.
#
# ZOMBIE_DETAIL=hero restores the original resolution for renders, marketing or a boss.
DETAIL=os.environ.get('ZOMBIE_DETAIL','horde')
if DETAIL=='hero': SPHERE_SEG,SPHERE_RINGS,TUBE_N,BEVEL_SEG=16,10,12,2
else: SPHERE_SEG,SPHERE_RINGS,TUBE_N,BEVEL_SEG=8,5,6,1

# Cycles proof renders cost far more than the build. Skipping them makes iterating on the
# polygon budget a few seconds rather than a few minutes.
SKIP_RENDER=os.environ.get('ZOMBIE_SKIP_RENDER','0')=='1'
def render_still():
 if not SKIP_RENDER: bpy.ops.render.render(write_still=True)
OUT=os.path.abspath(os.path.join(BASE,'../../Assets/_Project/Art/Characters/NormalZombie'))
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'; scene.render.fps=30
mats={}
def mat(name,color):
 m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Roughness'].default_value=.83
 mats[name]=m; return m
skin=mat('Skin • ash sage',(.29,.39,.30)); light=mat('Skin • raised planes',(.38,.47,.34)); dark=mat('Skin • sunken planes',(.16,.23,.18))
shirt=mat('Workshirt • faded petrol',(.095,.19,.23)); trim=mat('Workshirt • worn edges',(.17,.29,.31)); pants=mat('Trousers • charcoal indigo',(.075,.095,.12)); seam=mat('Trousers • faded seams',(.13,.16,.18))
boot=mat('Boots • dark leather',(.085,.063,.048)); sole=mat('Boots • rubber',(.028,.032,.03)); hair=mat('Hair • coal',(.055,.065,.053)); mouth=mat('Mouth and sockets',(.045,.035,.03)); tooth=mat('Teeth • old ivory',(.65,.60,.40)); eye=mat('Eyes • cloudy amber',(.76,.79,.51)); wound=mat('Wounds • dried rust',(.25,.085,.065)); metal=mat('Buttons • tarnished brass',(.35,.29,.16))
parts=[]
def finish(o,name,material,bone):
 o.name=name; o.data.materials.append(material)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for p in o.data.polygons:p.use_smooth=True
 g=o.vertex_groups.new(name=bone);g.add(list(range(len(o.data.vertices))),1,'REPLACE');parts.append(o);return o

def ell(name,c,s,material,bone,seg=SPHERE_SEG,rings=SPHERE_RINGS):
 # Call sites pass resolution POSITIONALLY for the parts that want it - the cranium asks for
 # 24x16. Clamping here rather than editing each one keeps the authored intent visible while
 # still honouring the budget, and hero mode is left exactly as written.
 if DETAIL!='hero': seg=min(seg,SPHERE_SEG);rings=min(rings,SPHERE_RINGS)
 bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=c)
 o=bpy.context.object;o.scale=s;return finish(o,name,material,bone)
def box(name,c,s,material,bone,bevel=.02):
 bpy.ops.mesh.primitive_cube_add(size=1,location=c);o=bpy.context.object;o.scale=s
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=BEVEL_SEG
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return finish(o,name,material,bone)
# Cross-section lofts, with a ragged end option. All vertices carry explicit bone weights.
def tube(name,points,radii,material,bone,other=None,ragged=False,n=TUBE_N):
 if DETAIL!='hero': n=min(n,TUBE_N)
 pts=[Vector(p) for p in points]; direction=(pts[-1]-pts[0]).normalized(); u=direction.cross(Vector((0,1,0))).normalized();v=direction.cross(u).normalized()
 verts=[]
 for j,(c,r) in enumerate(zip(pts,radii)):
  rx,ry=r if isinstance(r,tuple) else (r,r)
  for i in range(n):
   t=2*pi*i/n; p=c+u*cos(t)*rx+v*sin(t)*ry
   if ragged and j==len(pts)-1:p+=direction*(.035*sin(i*2.7)+.02*cos(i*4))
   verts.append(p)
 faces=[]
 for j in range(len(pts)-1):
  for i in range(n):a=j*n+i;b=j*n+(i+1)%n;faces.append((a,b,b+n,a+n))
 faces.extend([tuple(reversed(range(n))),tuple((len(pts)-1)*n+i for i in range(n))])
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 for p in mesh.polygons:p.use_smooth=True
 g=o.vertex_groups.new(name=bone)
 if other:
  h=o.vertex_groups.new(name=other)
  for j in range(len(pts)):
   w=max(0,min(1,(j/(len(pts)-1)-.35)/.5));ids=list(range(j*n,(j+1)*n));g.add(ids,1-w,'REPLACE');h.add(ids,w,'REPLACE')
 else:g.add(list(range(len(verts))),1,'REPLACE')
 parts.append(o);return o
bones={
 'Root':((0,0,0),(0,0,.22),None),
 'Pelvis':((0,0,.91),(0,0,1.12),'Root'),
 'Spine':((0,0,1.08),(0,-.10,1.38),'Pelvis'),
 'Chest':((0,-.10,1.38),(0,-.17,1.61),'Spine'),
 'Neck':((0,-.15,1.59),(0,-.23,1.73),'Chest'),
 'Head':((0,-.23,1.73),(0,-.27,2.04),'Neck')}
for side,sgn in [('L',1),('R',-1)]:
 bones.update({f'Thigh.{side}':((sgn*.145,0,1.0),(sgn*.17,-.035,.57),'Pelvis'),f'Shin.{side}':((sgn*.17,-.035,.57),(sgn*.18,.01,.15),f'Thigh.{side}'),f'Foot.{side}':((sgn*.18,.01,.15),(sgn*.18,-.19,.09),f'Shin.{side}'),f'UpperArm.{side}':((sgn*.30,-.12,1.51),(sgn*.46,-.17,1.20),'Chest'),f'Forearm.{side}':((sgn*.46,-.17,1.20),(sgn*.51,-.30,.94),f'UpperArm.{side}'),f'Hand.{side}':((sgn*.51,-.30,.94),(sgn*.52,-.37,.79),f'Forearm.{side}')})
# Body: a work shirt over a stooped, asymmetric frame.
tube('Trousers waist',[(0,0,.89),(0,0,1.04),(0,-.01,1.11)],[(.22,.14),(.24,.15),(.20,.13)],pants,'Pelvis',n=16)
tube('Torso',[(0,0,1.04),(0,-.04,1.18),(0,-.09,1.38),(0,-.13,1.52),(0,-.15,1.58)],[(.20,.14),(.205,.14),(.28,.17),(.32,.17),(.23,.12)],shirt,'Spine','Chest',ragged=True,n=20)
ell('Stooped shoulder mass',(0,.005,1.48),(.29,.155,.145),shirt,'Chest')
tube('Exposed neck',[(0,-.16,1.53),(0,-.21,1.68),(0,-.24,1.77)],[.12,.11,.115],skin,'Neck')
ell('Exposed sternum',(0,-.254,1.51),(.105,.045,.14),skin,'Chest')
for sg in [-1,1]:
 o=box('Frayed collar',(sg*.105,-.277,1.55),(.12,.035,.15),trim,'Chest');o.rotation_euler[1]=sg*-.43
 o=box('Shirt front placket',(sg*.03,-.228,1.26),(.037,.024,.31),trim,'Spine');o.rotation_euler[1]=sg*.06
box('Breast pocket',(.17,-.261,1.405),(.13,.025,.13),trim,'Chest',.009)
box('Pocket flap',(.17,-.282,1.46),(.14,.022,.035),shirt,'Chest',.006)
for z in [1.15,1.26,1.37]:ell('Brass button',(0,-.253,z),(.012,.013,.012),metal,'Spine',8,6)
box('Belt',(0,-.012,1.02),(.45,.31,.055),boot,'Pelvis',.035)
box('Belt buckle',(0,-.176,1.025),(.074,.025,.058),metal,'Pelvis',.009)
for sg in [-1,1]:box('Belt loop',(sg*.145,-.17,1.033),(.026,.025,.085),seam,'Pelvis',.005)
# Unequal sleeves, heavy forearms, curled fingers and battered shoes.
for side,sg in [('L',1),('R',-1)]:
 thigh=f'Thigh.{side}';shin=f'Shin.{side}';foot=f'Foot.{side}';upper=f'UpperArm.{side}';fore=f'Forearm.{side}';hand=f'Hand.{side}'
 tube('Trouser leg '+side,[(sg*.14,0,1.0),(sg*.16,-.01,.84),(sg*.17,-.035,.59),(sg*.175,-.02,.48),(sg*.18,.01,.22)],[(.125,.135),(.13,.13),(.105,.10),(.09,.095),(.083,.085)],pants,thigh,shin,ragged=True)
 ell('Knee patch '+side,(sg*.17,-.128,.58),(.074,.022,.092),seam,shin)
 if side=='L':
  ell('Torn knee opening',(sg*.17,-.15,.575),(.058,.017,.059),mouth,shin)
  ell('Exposed knee',(sg*.17,-.163,.58),(.043,.014,.043),skin,shin)
 for j in [-1,1]:box('Pocket seam '+side,(sg*.245,-.01,.93),(.02,.17,.018),seam,thigh,.004)
 box('Boot sole '+side,(sg*.18,-.085,.045),(.215,.37,.065),sole,foot,.025)
 ell('Scuffed boot '+side,(sg*.18,-.085,.105),(.108,.181,.085),boot,foot)
 tube('Boot ankle '+side,[(sg*.18,.015,.12),(sg*.18,.015,.23)],[.091,.084],boot,foot)
 for k in range(3):box('Boot lacing '+side,(sg*.18,-.095-k*.026,.18-k*.016),(.105,.014,.012),seam,foot,.004)
 tube('Arm skin '+side,[bones[upper][0],(sg*.41,-.15,1.30),bones[fore][0],(sg*.49,-.23,1.07),bones[hand][0]],[.11,.095,.09,.10,.074],skin,upper,fore)
 tube('Torn sleeve '+side,[(sg*.29,-.12,1.51),(sg*.365,-.145,1.41),(sg*.425,-.158,1.30 if side=='L' else 1.26)],[.138,.134,.116],shirt,upper,ragged=True)
 tube('Sleeve worn hem '+side,[(sg*.413,-.16,1.33),(sg*.433,-.16,1.29)],[.119,.117],trim,upper,ragged=True)
 ell('Elbow '+side,(sg*.465,-.16,1.20),(.083,.086,.079),dark,fore)
 ell('Palm '+side,(sg*.515,-.33,.876),(.091,.059,.109),skin,hand)
 ell('Knuckles '+side,(sg*.515,-.362,.82),(.087,.035,.034),light,hand)
 for i in range(4):
  x=sg*(.452+i*.041);z=.824-abs(i-1.4)*.009
  tube('Curled finger '+side+str(i),[(x,-.345,z),(x,-.38,z-.052),(x,-.413,z-.085),(x,-.44,z-.06)],[.021,.022,.018,.013],skin,hand,n=8)
  ell('Fingernail '+side+str(i),(x,-.449,z-.056),(.011,.007,.017),tooth,hand,8,6)
 tube('Hooked thumb '+side,[(sg*.447,-.328,.905),(sg*.405,-.366,.873),(sg*.425,-.413,.84)],[.033,.027,.016],skin,hand,n=10)
 ell('Forearm abrasion '+side,(sg*.493,-.307,1.065),(.044,.019,.076),wound,fore)
 for k in range(3):
  o=box('Arm scar '+side,(sg*(.474+k*.016),-.323,1.064+k*.012),(.008,.008,.064),dark,fore,.003);o.rotation_euler[1]=-.32
# Face, built in distinct sculptural planes rather than a plain sphere.
ell('Cranium',(0,-.255,1.868),(.20,.165,.229),skin,'Head',24,16)
ell('Square jaw',(0,-.304,1.725),(.146,.123,.096),skin,'Head',16,10)
ell('Left cheek',(.135,-.368,1.816),(.073,.052,.088),light,'Head')
ell('Right gaunt cheek',(-.132,-.368,1.800),(.067,.045,.081),dark,'Head')
for sg in [-1,1]:
 ell('Ear',(sg*.201,-.24,1.844),(.037,.034,.066),skin,'Head')
 ell('Ear hollow',(sg*.218,-.266,1.845),(.018,.013,.036),dark,'Head')
 ell('Deep eye socket',(sg*.081,-.391,1.898),(.076,.025,.053),dark,'Head')
 ell('Clouded eye',(sg*.081,-.414,1.898),(.041,.019,.028),eye,'Head')
 ell('Dead iris',(sg*.081,-.432,1.899),(.009,.006,.015),tooth,'Head',10,8)
 brow=ell('Heavy brow',(sg*.083,-.398,1.941),(.091,.047,.035),skin,'Head');brow.rotation_euler[1]=sg*-.18
ell('Broken nose bridge',(0,-.414,1.854),(.039,.037,.071),light,'Head')
ell('Broken nose tip',(.01,-.447,1.822),(.045,.031,.029),skin,'Head')
for sg in [-1,1]:ell('Nostril',(sg*.023,-.462,1.815),(.012,.008,.009),mouth,'Head',8,6)
ell('Snarling mouth',(0,-.407,1.751),(.101,.029,.049),mouth,'Head',20,10)
ell('Lower lip',(0,-.413,1.713),(.091,.022,.017),dark,'Head')
for i in range(6):
 if i==1:continue
 box('Broken upper tooth',(-.072+i*.029,-.438,1.769),(.020,.016,.027 if i!=4 else .017),tooth,'Head',.004)
for i in [0,2,3]:box('Lower tooth',(-.052+i*.033,-.437,1.728),(.019,.014,.020),tooth,'Head',.004)
ell('Chin',(0,-.376,1.684),(.081,.057,.034),light,'Head')
ell('Temple wound',(-.171,-.323,1.934),(.029,.034,.06),wound,'Head')
# Receding ragged hair cap, individual locks, no helmet-like full sphere.
ell('Back hair',(0,-.192,2.022),(.164,.11,.073),hair,'Head')
for i in range(9):
 x=-.145+i*.035;y=-.205+.03*sin(i*2);z=2.052+.012*sin(i)
 o=ell('Uneven hair lock',(x,y,z),(.025,.064,.044),hair,'Head',10,6);o.rotation_euler[0]=-.4
for sg in [-1,1]:ell('Sideburn',(sg*.167,-.236,1.96),(.032,.046,.081),hair,'Head')
# Ragged fabric at the lower shirt and visible ripped threads.
for i in range(7):
 x=-.19+i*.062
 tube('Torn shirt tail',[(x,-.165,1.10),(x+.013,-.17,1.045+.012*sin(i))],[.035,.007],shirt,'Spine',n=6)
# Rig and join all material regions into one deforming mesh.
bpy.ops.object.select_all(action='DESELECT')
arm=bpy.data.armatures.new('NormalZombie_Skeleton');rig=bpy.data.objects.new('NormalZombie_Rig',arm);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for name,(head,tail,parent) in bones.items():
 b=arm.edit_bones.new(name);b.head=head;b.tail=tail
 if parent:b.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
rig.select_set(False)
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();mesh=bpy.context.object;mesh.name='NormalZombie_Body'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
mesh.parent=rig;mod=mesh.modifiers.new('Zombie skeletal deformation','ARMATURE');mod.object=rig
# UVs available for later texture painting. Materials provide the initial palette.
bpy.context.view_layer.objects.active=mesh;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.018);bpy.ops.object.mode_set(mode='OBJECT')
rig['Character']='Reborn / normal infected civilian';rig['Forward']='-Y in Blender; +Z in FBX';rig['AnimationFPS']=30
for b in rig.pose.bones:b.rotation_mode='QUATERNION'
def worldrot(name,xyz):
 b=rig.pose.bones[name];q=Quaternion((1,0,0),math.radians(xyz[0])) @ Quaternion((0,1,0),math.radians(xyz[1])) @ Quaternion((0,0,1),math.radians(xyz[2]));r=b.bone.matrix_local.to_quaternion();b.rotation_quaternion=r.inverted()@q@r
clips={}
def frame(f,rot={},root=(0,0,0)):
 scene.frame_set(f)
 for b in rig.pose.bones:b.rotation_quaternion=Quaternion();b.location=(0,0,0)
 for name,xyz in rot.items():worldrot(name,xyz)
 rig.pose.bones['Root'].location=rig.pose.bones['Root'].bone.matrix_local.to_3x3().inverted() @ Vector(root)
 bpy.context.view_layer.update()
 evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
 lowest=min((evaluated.matrix_world @ v.co).z for v in evaluated.data.vertices)
 rig.pose.bones['Root'].location += rig.pose.bones['Root'].bone.matrix_local.to_3x3().inverted() @ Vector((0,0,.01-lowest))
 for b in rig.pose.bones:
  b.keyframe_insert(data_path='rotation_quaternion',frame=f,group=b.name);b.keyframe_insert(data_path='location',frame=f,group=b.name)
def new(name):
 rig.animation_data_create();rig.animation_data.action=bpy.data.actions.new(name);rig.animation_data.action.use_fake_user=True
 return rig.animation_data.action
def done(name,n,loop):
 a=rig.animation_data.action;clips[name]={'frames':n,'seconds':(n-1)/30,'loop':loop}
 tr=rig.animation_data.nla_tracks.new();tr.name=name;strip=tr.strips.new(name,1,a);tr.mute=True
 rig.animation_data.action=None
# Continuous asymmetric limp. Root stays in-place for CharacterController movement.
new('Chase')
for f in range(1,34,2):
 t=(f-1)/32*2*pi;s=sin(t);c=cos(t)
 frame(f,{'Pelvis':(0,3*s,4*s),'Spine':(10,0,-3*s),'Chest':(5,0,5*s),'Head':(-10,3*s,-4*s),'Thigh.L':(28*s,0,0),'Thigh.R':(-23*s,0,0),'Shin.L':(-max(0,-s)*42,0,0),'Shin.R':(-max(0,s)*36,0,0),'Foot.L':(8*s,0,0),'Foot.R':(-8*s,0,0),'UpperArm.L':(-24-14*s,0,-8),'UpperArm.R':(-32+12*s,0,11),'Forearm.L':(-28,0,0),'Forearm.R':(-39,0,0),'Hand.L':(15,0,-9),'Hand.R':(23,0,6)},(0,0,.022*cos(t*2)))
done('Chase',33,True)
new('Attack')
for f,wind,strike in [(1,0,0),(8,1,0),(13,.65,0),(18,0,1),(23,0,.85),(32,0,.2),(39,0,0)]:
 frame(f,{'Spine':(-8*wind+23*strike,0,12*wind-13*strike),'Chest':(-7*wind+10*strike,0,14*wind-15*strike),'Head':(-8*strike,0,-10*wind),'UpperArm.R':(-105*wind-63*strike,-14*wind,25*wind-25*strike),'Forearm.R':(-60*wind-17*strike,0,0),'UpperArm.L':(-30-25*strike,0,-15),'Forearm.L':(-40,0,0),'Thigh.L':(8*strike,0,0),'Thigh.R':(-8*strike,0,0)},(0,-.06*strike,-.025*strike))
done('Attack',39,False)
new('GetShot')
for f,k in [(1,0),(4,1),(7,.7),(12,.15),(18,0)]:
 frame(f,{'Spine':(-16*k,0,-7*k),'Chest':(-10*k,0,0),'Head':(-22*k,8*k,0),'UpperArm.L':(-15*k,0,-15*k),'UpperArm.R':(-24*k,0,14*k),'Forearm.L':(-20*k,0,0)},(0,.035*k,-.016*k))
done('GetShot',18,False)
new('Stagger')
for f,k,side in [(1,0,0),(7,1,-1),(15,.8,1),(24,.5,-.7),(34,.2,.3),(46,0,0)]:
 frame(f,{'Spine':(-22*k,side*7,side*9),'Chest':(-12*k,0,-side*8),'Head':(-20*k,side*12,side*12),'UpperArm.L':(-25*k,0,-35*k),'UpperArm.R':(-20*k,0,40*k),'Forearm.L':(-55*k,0,0),'Forearm.R':(-35*k,0,0),'Thigh.L':(20*k*side,0,0),'Thigh.R':(-22*k*side,0,0),'Shin.L':(-22*k,0,0),'Shin.R':(-18*k,0,0)},(.025*side,.06*k,-.075*k))
done('Stagger',46,False)
new('Death')
for f,fall,knee,z in [(1,0,0,0),(8,0,1,-.12),(17,23,1,-.08),(25,58,.55,.03),(32,88,.1,.17),(36,82,.1,.21),(43,88,0,.17),(55,88,0,.17)]:
 frame(f,{'Root':(fall,0,-5*min(f/32,1)),'Spine':(18*knee,0,0),'Head':(-16*knee,0,12*min(f/32,1)),'Thigh.L':(25*knee,0,0),'Thigh.R':(20*knee,0,0),'Shin.L':(-55*knee,0,0),'Shin.R':(-48*knee,0,0),'UpperArm.L':(-35*knee,0,-28*min(f/32,1)),'UpperArm.R':(-28*knee,0,42*min(f/32,1)),'Forearm.L':(-35*knee,0,0),'Forearm.R':(-30*knee,0,0)},(0,0,z))
done('Death',55,False)
# A single palette atlas replaces fifteen material slots for horde rendering.
palette=mesh.data.uv_layers.new(name='PaletteUV')
colors=[m.diffuse_color[:] for m in mesh.data.materials]
size=64; pixels=[]
for yy in range(size):
 for xx in range(size):
  index=(yy//16)*4+xx//16
  pixels.extend(colors[index] if index<len(colors) else (0.1,0.1,0.1,1))
tex=bpy.data.images.new('NormalZombie_Palette',width=size,height=size)
tex.pixels=pixels;tex.filepath_raw=os.path.join(OUT,'NormalZombie_Palette.png');tex.file_format='PNG';tex.save();tex.pack()
for face in mesh.data.polygons:
 i=face.material_index;uv=((i%4+.5)/4,(i//4+.5)/4)
 for li in face.loop_indices:palette.data[li].uv=uv
# The smart-project unwrap has to go, not just be deselected. active_render does not
# survive the FBX round trip - the importer marks the FIRST UV layer as the render one - so
# shipping both layers meant Unity sampled this sixteen-cell palette with a 6,382-point
# unwrap and scattered every colour on the model. Every render of this character since it
# was authored has been of that artifact.
# Remove by NAME and re-fetch: removing a UV layer invalidates the other layer pointers.
while len(mesh.data.uv_layers)>1: mesh.data.uv_layers.remove(next(_l for _l in mesh.data.uv_layers if _l.name!='PaletteUV'))
palette=mesh.data.uv_layers['PaletteUV']
mesh.data.uv_layers.active=palette;palette.active_render=True
material=bpy.data.materials.new('NormalZombie • unified palette');material.use_nodes=True
node=material.node_tree.nodes.new('ShaderNodeTexImage');node.image=tex;node.interpolation='Closest'
p=material.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.83
material.node_tree.links.new(node.outputs['Color'],p.inputs['Base Color'])
mesh.data.materials.clear();mesh.data.materials.append(material)
for face in mesh.data.polygons:face.material_index=0

# Export selected rig and skinned model only.
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
for tr in rig.animation_data.nla_tracks:tr.mute=False
scene.frame_start=1;scene.frame_end=55
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'NormalZombie.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,apply_scale_options='FBX_SCALE_UNITS',path_mode='COPY',embed_textures=True)
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'NormalZombie.glb'),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True)
for tr in rig.animation_data.nla_tracks:tr.mute=True
rig.animation_data.action=bpy.data.actions['Chase'];scene.frame_end=33;scene.frame_set(5)
# Studio scene, excluded from exported assets.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.018));floor=bpy.context.object;floor.name='STUDIO • ground';floor.data.materials.append(mat('Studio backdrop',(.028,.039,.044)))
def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
def area(name,loc,power,color,size):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=color;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=loc;aim(o,(0,0,1))
area('STUDIO • warm key',(3,-4,5),450,(1,.86,.70),4)
area('STUDIO • cool fill',(-3,-2,2.7),240,(.60,.77,1),3)
area('STUDIO • rim',(1,3,4),650,(.58,1,.79),3)
d=bpy.data.cameras.new('Presentation Camera');cam=bpy.data.objects.new('Presentation Camera',d);bpy.context.collection.objects.link(cam);scene.camera=cam;cam.location=(3,-5,2.9);aim(cam,(0,-.08,1.03));d.type='ORTHO';d.ortho_scale=2.65
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.world.color=(.17,.17,.17);scene.view_settings.view_transform='AgX'
scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(BASE,'NormalZombie_Preview.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BASE,'NormalZombie.blend'))
render_still()
# Animation proof frames, three moments per clip.
scene.render.resolution_x=400;scene.render.resolution_y=460;scene.cycles.samples=12
os.makedirs(os.path.join(BASE,'proof'),exist_ok=True)
for name,fs in {'Chase':[1,9,25],'Attack':[8,18,32],'GetShot':[1,4,12],'Stagger':[7,15,34],'Death':[17,32,55]}.items():
 rig.animation_data.action=bpy.data.actions[name]
 cam.location=(3,-5,2.9) if name!='Death' else (3,-5,3)
 aim(cam,(0,-.08,1.03) if name!='Death' else (0,-.8,.6));cam.data.ortho_scale=2.65 if name!='Death' else 3.15
 for f in fs:
  scene.frame_set(f);scene.render.filepath=os.path.join(BASE,'proof',f'{name}_{f:02}.png');render_still()
mesh.data.calc_loop_triangles()
with open(os.path.join(BASE,'asset_report.json'),'w') as f:json.dump({'vertices':len(mesh.data.vertices),'triangles':len(mesh.data.loop_triangles),'bones':len(arm.bones),'materials':len(mesh.data.materials),'clips':clips},f,indent=2)
print('ZOMBIE_BUILD_COMPLETE',len(mesh.data.vertices),len(mesh.data.loop_triangles),flush=True)
