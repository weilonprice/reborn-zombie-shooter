"""Three detailed static environment replacements. Run with Blender --background --python."""
from pathlib import Path
exec(Path(__file__).with_name('build_buildings.py').read_text().split('for building_name, building_builder in BUILDERS.items():')[0].replace('bake_anim=False,', 'bake_anim=False, bake_space_transform=True,'))
import random
random.seed(91326)

def beam(name,a,b,width,mat,root):
    mid=(Vector(a)+Vector(b))*.5; direction=Vector(b)-Vector(a)
    ob=box(name,mid,(width,width,direction.length),mat,root,bevel=.015)
    ob.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    return ob

def lettering(text,location,size,mat,root):
    bpy.ops.object.text_add(location=location,rotation=(math.pi/2,0,0))
    ob=bpy.context.object; ob.data.body=text; ob.data.align_x='CENTER'; ob.data.align_y='CENTER'; ob.data.size=size; ob.data.extrude=.008; ob.data.bevel_depth=0; ob.data.resolution_u=2
    ob.data.materials.append(mat); ob.parent=root
    bpy.ops.object.convert(target='MESH'); ob.name='Sign lettering'

def roof_gable(root,w,d,z,roof,trim):
    slope=math.radians(18); rise=d/2*math.tan(slope)
    for side in [-1,1]:
        box('Standing seam roof',(0,side*d/4,z+rise/2),(w+.5,d/2/math.cos(slope)+.25,.16),roof,root,rotation=(-side*slope,0,0),bevel=.025)
        for i in range(int(w/.38)+1):
            x=-w/2+i*.38
            beam('Raised roof seam',(x,side*(d/2+.10),z+.10),(x,0,z+rise+.14),.045,trim,root)
    for x in [-w/2,w/2]:
        verts=[(x,-d/2,z),(x,d/2,z),(x,0,z+rise)]
        mesh=bpy.data.meshes.new('Gable');mesh.from_pydata(verts,[],[(0,1,2)]);mesh.update()
        ob=bpy.data.objects.new('Closed gable',mesh);bpy.context.collection.objects.link(ob);ob.parent=root;mesh.materials.append(trim)
    box('Ridge cap',(0,0,z+rise+.10),(w+.6,.22,.12),trim,root,bevel=.025)
    return z+rise

def common(root,w,d,h,wall,trim,wood,roof):
    box('Foundation',(0,0,.16),(w+.12,d+.12,.32),trim,root,bevel=.05)
    for x in [-w/2,w/2]:
        for y in [-d/2,d/2]: box('Corner flashing',(x,y,h/2),(.16,.16,h),trim,root,bevel=.02)
    # Damp lower masonry, worn blocks distributed around the perimeter.
    for side in [-1,1]:
        for row in range(3):
            for i in range(int(w/.65)):
                if random.random()<.25: continue
                x=-w/2+.34+i*.65+(row%2)*.12
                box('Exposed masonry',(x,side*(d/2+.016),.28+row*.17),(.56,.035,.13),wood,root,bevel=.008)
    # Rear services, downpipe and access door: rear is intentionally detailed too.
    cylinder('Rain downpipe',(w/2-.18,d/2+.12,h*.48),.075,h*.92,trim,root)
    box('Service door',(-w*.27,d/2+.035,1.14),(1.05,.10,2.05),trim,root,bevel=.03)
    box('Service door inset',(-w*.27,d/2+.095,1.25),(.80,.035,1.3),roof,root,bevel=.015)
    box('Electrical cabinet',(w*.28,d/2+.14,1.35),(.62,.24,.78),trim,root,bevel=.03)
    cylinder('Conduit',(w*.28,d/2+.12,2.25),.025,1.2,trim,root,vertices=8)

def hvac(root,x,y,z,trim,roof):
    box('HVAC curb',(x,y,z+.1),(1.6,1.1,.2),trim,root,bevel=.04)
    box('HVAC casing',(x,y,z+.52),(1.4,.95,.7),roof,root,bevel=.055)
    cylinder('Fan recess',(x,y,z+.88),.36,.035,trim,root,vertices=20)
    for i in range(6):
        ob=box('Fan blades',(x,y,z+.91),(.59,.065,.03),roof,root);ob.rotation_euler.z=i*math.pi/3
    for i in range(7):box('Cooling grille',(x-.52+i*.17,y-.49,z+.5),(.04,.035,.43),trim,root)

def create(kind):
    clear_scene();root=root_object(kind)
    wall=material('Warm plaster',(.46,.38,.27));trim=material('Charcoal metal',(.085,.115,.12),.25)
    wood=material('Weathered timber',(.27,.17,.09));roof=material('Oxidized teal steel',(.12,.26,.25),.35)
    glass=material('Dark blue glass',(.035,.09,.12),.2,.28);cream=material('Ivory paint',(.77,.69,.48));red=material('Faded red paint',(.42,.095,.065));rust=material('Rust and brick',(.31,.13,.065))
    if kind=='Shack':
        w,d,h=8.4,6.8,3.6
        box('MainShell',(0,0,h/2),(w,d,h),wall,root,bevel=.07)
        peak=roof_gable(root,w,d,h,roof,trim)
        for row in range(13):
            for side in [-1,1]:box('Timber siding',(0,side*(d/2+.025),.40+row*.23),(w,.06,.19),wood if row%5==0 else wall,root,bevel=.01)
        add_door(root,-2,-d/2,1.3,1.25,2.45,wood,cream)
        add_window(root,1.3,-d/2,2.0,2,1.3,wall,glass,cream)
        for z,tilt in [(1.8,.2),(2.3,-.15)]:
            box('Window barricade',(1.3,-d/2-.23,z),(2.45,.14,.18),wood,root,rotation=(0,tilt,0),bevel=.015)
        add_window(root,w/2+.04,.7,2,1.6,1.2,wall,glass,cream,side='side')
        box('Porch step',(-2,-d/2-.36,.14),(2.2,.6,.28),trim,root,bevel=.03)
        box('Door canopy',(-2,-d/2-.38,2.85),(2.25,.85,.13),roof,root,rotation=(.12,0,0),bevel=.025)
        for x in [-2.9,-1.1]:beam('Canopy bracket',(x,-d/2-.05,2.35),(x,-d/2-.75,2.82),.09,wood,root)
        cylinder('Stove chimney',(2,1,peak+.13),.16,1.3,trim,root)
        cone('Chimney cap',(2,1,peak+.85),.29,.10,.18,rust,root)
        box('Roof patch',(-2,-1.4,h+(d/2-1.4)*math.tan(math.radians(18))+.13),(1.5,1.1,.035),rust,root,rotation=(math.radians(18),0,0))
        box('House number',(-3,-d/2-.09,2.1),(.45,.06,.5),trim,root);lettering('17',(-3,-d/2-.135,2.1),.26,cream,root)
    elif kind=='Storefront':
        w,d,h=12,7.6,4.4
        box('MainShell',(0,0,h/2),(w,d,h),wall,root,bevel=.08)
        box('Roof deck',(0,0,h),(w,d,.16),trim,root)
        for y in [-d/2,d/2]:box('Parapet',(0,y,h+.24),(w+.18,.22,.6),cream,root,bevel=.035)
        for x in [-w/2,w/2]:box('Parapet',(x,0,h+.24),(.22,d,.6),cream,root,bevel=.035)
        add_door(root,-4.4,-d/2,1.45,1.4,2.7,glass,roof)
        for x in [-1.9,1,3.9]:
            add_window(root,x,-d/2,1.95,2.35,2.05,wall,glass,roof)
            box('Display sill',(x,-d/2-.17,.92),(2.55,.32,.16),cream,root,bevel=.025)
        for i in range(16):
            box('Striped canvas awning',(-2.8+i*.55,-d/2-.55,3.25),(.55,1.12,.09),red if i%2==0 else cream,root,rotation=(.2,0,0))
            box('Scalloped valance',(-2.8+i*.55,-d/2-1.10,3.05),(.54,.06,.25),red if i%2==0 else cream,root,bevel=.04)
        box('Store sign',(0,-d/2-.15,4.03),(8,.18,.66),roof,root,bevel=.04)
        lettering('LAST STOP  SUPPLIES',(0,-d/2-.26,4.03),.44,cream,root)
        hvac(root,2,1,h+.08,trim,cream)
        for x in [-3,-1]:
            box('Skylight curb',(x,.2,h+.14),(1.4,2,.2),roof,root,bevel=.025)
            box('Skylight glass',(x,.2,h+.27),(1.15,1.75,.12),glass,root,bevel=.03)
            box('Skylight bar',(x,.2,h+.35),(.06,1.8,.04),cream,root)
        add_window(root,w/2+.03,1,2.1,2,1.4,wall,glass,roof,side='side')
        for z in [1.55,2.3]:box('Boarded display',(3.9,-d/2-.2,z),(2.65,.12,.16),wood,root,rotation=(0,.16,0))
    elif kind=='ApartmentBlock':
        w,d,h=10,8.2,8.4
        wall=material('Terracotta plaster',(.43,.24,.17))
        box('MainShell',(0,0,h/2),(w,d,h),wall,root,bevel=.07)
        box('Ground floor stone',(0,-d/2-.015,1.35),(w,.07,2.7),cream,root)
        for z in [2.8,5.5,8.25]:
            for y in [-d/2,d/2]:box('Floor cornice',(0,y,z),(w+.15,.20,.17),cream,root,bevel=.025)
            for x in [-w/2,w/2]:box('Side cornice',(x,0,z),(.20,d,.17),cream,root,bevel=.025)
        add_door(root,0,-d/2,1.3,1.65,2.5,glass,trim)
        for x in [-3.25,3.25]:add_window(root,x,-d/2,1.45,1.65,1.55,wall,glass,trim)
        box('Entry canopy',(0,-d/2-.55,2.8),(3.2,1.35,.16),roof,root,bevel=.04)
        for x in [-1.38,1.38]:beam('Canopy support',(x,-d/2-.1,2.2),(x,-d/2-1.05,2.78),.08,trim,root)
        box('Entry sign',(0,-d/2-.12,3.15),(2.9,.14,.42),trim,root,bevel=.025)
        lettering('CEDAR COURT',(0,-d/2-.20,3.15),.26,cream,root)
        box('Door step',(0,-d/2-.36,.10),(2.15,.65,.2),trim,root,bevel=.025)
        for z in [4.2,6.9]:
            for x in [-3.25,0,3.25]:
                add_window(root,x,-d/2,z,1.65,1.75,wall,glass,cream)
                floor=z-1.0
                box('Balcony slab',(x,-d/2-.56,floor),(2.45,1.25,.18),trim,root,bevel=.03)
                box('Balcony handrail',(x,-d/2-1.12,floor+1.0),(2.35,.08,.08),roof,root,bevel=.015)
                for dx in [-1.1,-.73,-.36,0,.36,.73,1.1]:
                    box('Balcony baluster',(x+dx,-d/2-1.12,floor+.52),(.055,.06,.95),trim,root)
                for dx in [-1.1,1.1]:box('Balcony side rail',(x+dx,-d/2-.57,floor+1),(.07,1.1,.07),roof,root)
            for y in [-2,1.5]:add_window(root,w/2+.055,y,z,1.6,1.55,wall,glass,cream,side='side')
        # One boarded apartment, and a planter left behind on a balcony.
        for z in [6.7,7.15]:box('Boarded upper window',(-3.25,-d/2-.2,z),(1.95,.12,.15),wood,root,rotation=(0,.18,0))
        box('Empty planter',(3.25,-d/2-.88,3.44),(.9,.33,.26),red,root,bevel=.025)
        box('Planter soil',(3.25,-d/2-.88,3.58),(.76,.22,.025),wood,root)
        # Roof terrace, stairwell access, tank and utility details are visible from play camera.
        box('Roof deck',(0,0,h),(w,d,.18),trim,root)
        for y in [-d/2,d/2]:box('Roof parapet',(0,y,h+.27),(w+.12,.20,.65),cream,root,bevel=.025)
        for x in [-w/2,w/2]:box('Roof parapet',(x,0,h+.27),(.20,d,.65),cream,root,bevel=.025)
        box('Stairwell',(2.6,1.3,h+.7),(2.8,2.7,1.4),wall,root,bevel=.04)
        box('Stairwell cap',(2.6,1.3,h+1.45),(3.05,2.95,.18),roof,root,bevel=.04)
        box('Roof access door',(2.6,-.065,h+.65),(.85,.07,1.25),trim,root,bevel=.02)
        for x in [-2.8,-1.2]:
            for y in [.7,2.3]:box('Water tank legs',(x,y,h+.55),(.10,.10,1.0),trim,root)
        cylinder('Rooftop water tank',(-2,1.5,h+1.45),1.0,1.5,roof,root,vertices=16,bevel=.04)
        for z in [h+.8,h+2.05]:cylinder('Tank retaining band',(-2,1.5,z),1.035,.09,trim,root,vertices=16)
        cone('Tank cap',(-2,1.5,h+2.3),1.04,.15,.28,roof,root,vertices=16)
        cylinder('Tank supply pipe',(-3.15,1.5,h+.9),.06,1.6,trim,root)
        hvac(root,1.5,-2,h+.1,trim,cream)
        for y in [-2.3,0,2.3]:
            for z in [4.2,6.9]:
                box('Rear window',(-w/2-.04,y,z),(.08,1.35,1.55),glass,root)
                box('Rear lintel',(-w/2-.1,y,z+.8),(.16,1.55,.13),cream,root)
        for x in [-2.5,0,2.5]:
            for z in [4.2,6.9]:box('Back window',(x,d/2+.05,z),(1.4,.08,1.5),glass,root)
    else:
        w,d,h=16,9.6,5
        wall=material('Industrial blue siding',(.23,.32,.34))
        box('MainShell',(0,0,h/2),(w,d,h),wall,root,bevel=.07)
        peak=roof_gable(root,w,d,h,roof,trim)
        for side in [-1,1]:
            for i in range(41):box('Corrugated wall rib',(-w/2+i*.4,side*(d/2+.035),2.7),(.06,.08,4.3),roof,root)
        for x in [-4,2]:
            box('Loading bay frame',(x,-d/2-.13,1.95),(4.5,.2,3.9),trim,root,bevel=.035)
            box('Roller shutter',(x,-d/2-.25,1.84),(4.05,.08,3.5),cream,root,bevel=.02)
            for z in range(15):box('Shutter slat',(x,-d/2-.31,.2+z*.235),(4,.045,.035),roof,root)
            box('Loading threshold',(x,-d/2-.36,.12),(4.5,.65,.24),trim,root,bevel=.035)
            for dx in [-2.35,2.35]:
                cylinder('Safety bollard',(x+dx,-d/2-.5,.55),.12,1.1,cream,root,vertices=10)
                cylinder('Bollard stripe',(x+dx,-d/2-.5,.72),.125,.20,trim,root,vertices=10)
        box('Depot sign',(0,-d/2-.18,4.48),(9,.20,.63),trim,root,bevel=.03)
        lettering('NORTHLINE  //  DEPOT 03',(0,-d/2-.30,4.48),.45,cream,root)
        for x in [-5,5]:
            cylinder('Exhaust duct',(x,.7,peak+.12),.25,1.2,trim,root)
            cone('Exhaust hood',(x,.7,peak+.78),.43,.32,.25,roof,root)
        for x in [-3,0,3]:
            box('Roof light',(x,-2.1,h+(d/2-2.1)*math.tan(math.radians(18))+.13),(1.8,2.1,.08),glass,root,rotation=(math.radians(18),0,0),bevel=.025)
        for y in [-2,1.4]: add_window(root,w/2+.07,y,3.4,2.2,1,wall,glass,cream,side='side')
    common(root,w,d,h,wall,trim,wood,roof)
    # Collapse decoration by material; keep a simple wall shell for inspection.
    for mat in list(bpy.data.materials):
        objects=[o for o in root.children_recursive if o.type=='MESH' and o.name!='MainShell' and len(o.data.materials)==1 and o.data.materials[0]==mat]
        if not objects:continue
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:ob.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        if len(objects)>1:bpy.ops.object.join()
        objects[0].name='Detail_'+mat.name
    for ob in root.children_recursive:
        if ob.type!='MESH':continue
        bpy.context.view_layer.objects.active=ob;bpy.ops.object.select_all(action='DESELECT');ob.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    return root,(0,0,h*.5),(w,d,h+2)

# Vertex bounds avoid inflating rotated joined-mesh local bounding boxes.
def bbox_for(root):
    points=[o.matrix_world @ v.co for o in root.children_recursive if o.type=='MESH' for v in o.data.vertices]
    low=[min(p[i] for p in points) for i in range(3)]
    high=[max(p[i] for p in points) for i in range(3)]
    return {'min':low,'max':high,'size':[high[i]-low[i] for i in range(3)]}

# Frame the complete silhouette, including tall vents and projecting awnings.
original_preview = preview_scene
def preview_scene(target):
    collection = original_preview(target)
    camera = bpy.context.scene.camera
    camera.data.type = 'ORTHO'
    building = next(o for o in bpy.data.objects if o.type == 'EMPTY' and o.get('building_id'))
    bounds = bbox_for(building)
    size = bounds['size']
    camera.data.ortho_scale = max(size[0],size[1],size[2])*(1.85 if building.name=='ApartmentBlock' else 1.50)
    target = tuple((bounds['min'][i]+bounds['max'][i])*.5 for i in range(3))
    camera.location = (18,-24,19)
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    bpy.context.scene.render.resolution_x=1200
    bpy.context.scene.render.resolution_y=900
    return collection

import sys
selected = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['Shack','Storefront','Warehouse','ApartmentBlock']
for name in selected:
    export_one(name,lambda name=name:create(name))
    bpy.context.view_layer.update()
    p=Path(BASE)/name/'asset_report.json';report=json.loads(p.read_text())
    report['bounds_meters']=bbox_for(bpy.data.objects[name])
    meshes=[o for o in bpy.data.objects if o.type=='MESH' and o.parent and o.parent.name==name]
    report['triangles']=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
    report['static_colliders']='One root BoxCollider, supplied by ArenaBuilder. No interior.'
    report['uvs']='UV0 smart packed; Unity generates secondary lightmap UVs.'
    p.write_text(json.dumps(report,indent=2))
print('DETAILED_BUILDINGS_COMPLETE')
