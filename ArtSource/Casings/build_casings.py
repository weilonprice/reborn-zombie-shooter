"""Reproducible spent-case meshes and original modal-synthesis impact audio.
Run with Blender --background --python ArtSource/Casings/build_casings.py.
Calibre names describe artistic design references, not a simulation of named firearms.
"""
import bpy
import math
import random
import wave
import struct
import json
from pathlib import Path

BASE = Path(__file__).resolve().parent
OUT = BASE.parents[1] / 'Assets/_Project/Art/Casings'
AUDIO = BASE.parents[1] / 'Assets/_Project/Audio/Casings'
# name, case length, diameter, neck diameter, colour, resonances Hz, decay seconds
TYPES = [
    ('Pistol', .023, .012, .012, (.66,.43,.13), (3700,6100,8900), .105),
    ('SMG', .019, .010, .010, (.78,.58,.22), (4900,7600,10300), .075),
    ('AssaultRifle', .045, .010, .0065, (.69,.48,.16), (2700,4600,7100), .15),
    ('SniperRifle', .067, .013, .009, (.54,.35,.10), (1900,3400,5200), .21),
    ('Shotgun', .070, .020, .020, (.48,.025,.018), (780,1550,2800), .055),
    ('GrenadeLauncher', .048, .042, .042, (.38,.32,.12), (620,1170,2300), .24),
    ('SiphonRifle', .051, .012, .008, (.53,.60,.64), (3100,5350,8300), .23),
]

def material(name, colour, metal):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*colour,1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*colour,1)
    bsdf.inputs['Metallic'].default_value = metal
    bsdf.inputs['Roughness'].default_value = .36
    return mat

def build(name, length, diameter, neck, colour):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    r, nr = diameter/2, neck/2
    # Revolved closed profile: extractor rim, groove, body, shoulder, open soot-dark mouth.
    rim = r*1.10
    shoulder = .74 if neck < diameter else .90
    profile = [(0,0),(rim,0),(rim,length*.035),(r*.88,length*.06),
               (r*.88,length*.10),(r,length*.12),(r,length*shoulder),
               (nr,length*.86 if neck < diameter else length*.94),
               (nr,length),(nr*.77,length),(nr*.77,length*.75),
               (r*.74,length*.18),(0,length*.18)]
    sides=16
    verts=[(rad*math.cos(a*2*math.pi/sides),rad*math.sin(a*2*math.pi/sides),z-length/2)
           for rad,z in profile for a in range(sides)]
    faces=[]
    for j in range(len(profile)-1):
        for a in range(sides):
            faces.append((j*sides+a,j*sides+(a+1)%sides,(j+1)*sides+(a+1)%sides,(j+1)*sides+a))
    mesh=bpy.data.meshes.new(name+'_SpentCase')
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name+'_SpentCase',mesh)
    bpy.context.collection.objects.link(obj)
    body=material(name+'_Body',colour,.08 if name=='Shotgun' else .78)
    rim_mat=material(name+'_Rim',(.62,.42,.15) if name!='SiphonRifle' else (.7,.73,.75),.82)
    inner=material(name+'_Interior',(.055,.043,.029),.3)
    for m in (body,rim_mat,inner): mesh.materials.append(m)
    for face in mesh.polygons:
        section=face.index//sides
        face.material_index=2 if section>=9 else 1 if section<5 else 0
        if name=='Shotgun' and section==5: face.material_index=0
    # Primer disk at the base; a spent case has no projectile at its open end.
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r*.29,depth=length*.012,
                                       location=(0,0,-length*.505))
    primer=bpy.context.object
    primer.data.materials.append(rim_mat)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True);primer.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.join()
    bpy.ops.wm.save_as_mainfile(filepath=str(BASE/(name+'_SpentCase.blend')))
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'_SpentCase.fbx')),use_selection=True,
        object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',
        apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'_SpentCase.glb')),export_format='GLB',use_selection=True)
    return {'weapon':name,'length_m':length,'diameter_m':diameter,
            'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'materials':len(obj.data.materials)}

def impact(name, frequencies, decay, variant):
    rng=random.Random(name+str(variant))
    sr=44100
    duration=max(.22,decay*4)
    samples=[]
    modes=[(f*rng.uniform(.95,1.05),rng.uniform(.3,.6),rng.uniform(0,math.tau)) for f in frequencies]
    previous=0
    for i in range(int(sr*duration)):
        t=i/sr
        noise=rng.uniform(-1,1)
        high=noise-previous;previous=noise
        # Hard strike plus inharmonic metal modes; polymer hulls damp the ring sharply.
        signal=high*.30*math.exp(-t/(.004 if name!='Shotgun' else .009))
        for j,(freq,amp,phase) in enumerate(modes):
            signal+=amp*math.sin(math.tau*freq*t+phase)*math.exp(-t/(decay/(1+j*.7)))
        if name=='Shotgun':signal+=noise*.32*math.exp(-t/.027)
        if name=='GrenadeLauncher':signal+=.4*math.sin(math.tau*210*t)*math.exp(-t/.045)
        signal*=min(1,t/.0006)*min(1,(duration-t)/.02)
        samples.append(signal)
    peak=max(abs(v) for v in samples)
    samples=[v/peak*.72 for v in samples]
    path=AUDIO/f'{name}_Floor_{variant+1}.wav'
    with wave.open(str(path),'wb') as wav:
        wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(sr)
        wav.writeframes(b''.join(struct.pack('<h',int(v*32767)) for v in samples))
    return samples

def main():
    OUT.mkdir(parents=True,exist_ok=True);AUDIO.mkdir(parents=True,exist_ok=True)
    report=[];audition=[]
    for name,length,diameter,neck,colour,freq,decay in TYPES:
        report.append(build(name,length,diameter,neck,colour))
        for variant in range(3):
            sound=impact(name,freq,decay,variant)
            audition+=sound+[0.]*(44100//5)
        audition += [0.]*44100
    with wave.open(str(BASE/'CasingSoundAudition.wav'),'wb') as wav:
        wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(44100)
        wav.writeframes(b''.join(struct.pack('<h',int(v*32767)) for v in audition))
    (BASE/'asset_report.json').write_text(json.dumps(report,indent=2)+'\n')
    print('CASING_ASSETS_COMPLETE: 7 meshes, 21 unique impacts')

if __name__=='__main__':main()
