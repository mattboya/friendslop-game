"""Disposable neutral-light reviews of the generated master; never exports cameras."""
import bpy, math, sys
from mathutils import Vector
from pathlib import Path
root=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
style=args[args.index('--face-style')+1] if '--face-style' in args else 'current'
if style not in {'current','scruffy','deadpan'}:raise ValueError(style)
face_variant=int(args[args.index('--face-variant')+1]) if '--face-variant' in args else 1
intoxicated='--intoxicated' in args
red_eyes='--red-eyes' in args
source=root/'ArtSource/FestivalCharacter.blend' if style=='current' else root/'ArtSource/FaceComparisons'/style/'FestivalCharacter.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
visible={'Body_0_1','Face_0_'+str(face_variant),'Shirt_1','Pants_1','Shoes_0','Hairstyle_1','HairTop_1','Accessory_0'}
colors=[(.65,.38,.23,1),(.14,.33,.44,1),(.16,.20,.24,1),(.55,.27,.15,1),(.98,.45,.43,1) if red_eyes else (.94,.91,.83,1),(.025,.035,.044,1),(.17,.08,.045,1),(.94,.52,.12,1)]
mats=[]
for i,c in enumerate(colors):
    m=bpy.data.materials.new('Review swatch '+str(i));m.diffuse_color=c;mats.append(m)
for o in list(bpy.data.objects):
    if o.type!='MESH':continue
    o.hide_render=o.name not in visible
    o.hide_set(o.name not in visible)
    if o.data.shape_keys:
        for k in o.data.shape_keys.key_blocks:
            if k.name!='Basis':k.value=1 if k.name=='Fit_0_1' or k.name=='WideIntoxicatedEyes' and intoxicated else 0
    uv=o.data.uv_layers.active
    indices=[min(7,int(uv.data[p.loop_indices[0]].uv.x*8)) for p in o.data.polygons]
    o.data.materials.clear()
    for m in mats:o.data.materials.append(m)
    for p,i in zip(o.data.polygons,indices):p.material_index=i
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=800;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_cavity=True;scene.display.shading.cavity_type='BOTH';scene.display.shading.curvature_ridge_factor=1.2;scene.display.shading.curvature_valley_factor=1.1
scene.display.shading.show_shadows=True;scene.display.shading.background_type='WORLD';scene.world.color=(.12,.14,.17)
cdata=bpy.data.cameras.new('Review lens');cdata.type='ORTHO';camera=bpy.data.objects.new('Disposable review camera',cdata);scene.collection.objects.link(camera);scene.camera=camera
if '--face-variant' in args:
    out=root/'artifacts/face-implementation'/('scruffy' if face_variant<3 else 'deadpan')/('wide' if intoxicated else 'regular')/('red' if red_eyes else 'clear')
else:
    out=root/('artifacts/character-quality' if style=='current' else 'artifacts/face-comparison/'+style)
out.mkdir(parents=True,exist_ok=True)
for name,pos,target,scale in [('front',(0,-6,1.2),(0,0,1.2),2.7),('three-quarter',(3,-6,2.3),(0,0,1.2),2.7),('face',(1,-6,2.1),(0,0,1.9),.85),('back',(0,6,1.2),(0,0,1.2),2.7),('left',(-6,0,1.2),(0,0,1.2),2.7),('right',(6,0,1.2),(0,0,1.2),2.7),('top',(0,0,6),(0,0,1.2),2.7),('bottom',(0,0,-6),(0,0,1.2),2.7)]:
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();cdata.ortho_scale=scale
    scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
