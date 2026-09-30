CONFIG = {
 'prefix':'AH03W_','source':'ArtSource/Generated/FestivalCharacter.blend','out':'ArtSource/ProductionPackage03','exports':'Assets/Festival/Art/ProductionPeople03','helpers':'scripts/create_first_production_package.py','mesh':{'sides':24,'rings':12,'bevel':.008,'bevel_segments':2,'tube_sides':8},
 'styles':{'Headgear':['Bucket','Beanie','Visor','Crown','WideBrim','DoublePom','AntennaCap','SunCrown','Mushroom','TieVisor'],'Sunglasses':['Round','Square','Star','Slim','Hexagon','Heart','CatEye','Octagon','Shield','Flower'],'Shirt':['Tee','Hoodie','Zip','Jacket','Rugby','LongHoodie','CroppedZip','UtilityJacket','Colorblock','TourJacket'],'Pants':['Shorts','Trousers','Skirt','Tutu','Cargo','Flare','SplitShorts','PleatedSkirt','LayeredTutu','CuffedTrouser'],'Shoes':['Sneaker','Boot','Platform','Sandal','Court','HighTop','MoonBoot','Trail','SlipOn','SplitColor'],'FacialHair':['Moustache','Goatee','Beard','Sideburn','Chevron','Handlebar','ForkBeard','SoulPatch','Chinstrap','WavyBeard'],'Hairstyle':['Quiff','Bob','Mohawk','Sweep','LongBob','TwinBuns','Spikes','SidePart','Curly','LongWaves'],'Accessory':['Backpack','Pouch','Scarf','Headphones','SunPendant','MoonPendant','BeltBag','Lanyard','Capelet','EarDefenders']},'base':{'Headgear':[0,1,1,3,0,2],'Shirt':[0,1,2,3,0,3],'Pants':[1,1,0,2,3,1],'Shoes':[0,1,2,3,0,1],'Hairstyle':[1,1,2,3,0,3]},
 'fit':{'male':1.04,'female':.96,'widths':[.92,1,1.12],'head_base':.25,'head_step':.008,'head_ref':.258,'arm':.38,'leg':.20},'shape':{'hat_z':2.2,'hat_radius':.29,'pom':.065,'antenna_height':.22,'glasses_x':.109,'glasses_z':1.98,'glasses_y':-.36,'lens_w':.090,'lens_h':.066,'lens_depth':.014,'rim':.008,'samples':32,'hair_length':.20,'curl':.046,'neck_z':1.5,'pendant_z':1.17,'cord':.008,'badge_size':[.12,.016,.15],'sole_gain':.035,'shirt_extension':.10,'crop':.10,'pocket_size':[.13,.045,.14]},
 'colors':{'Skin':(.67,.37,.23),'Coral':(.72,.20,.15),'Ink':(.026,.042,.055),'Teal':(.055,.29,.28),'Cream':(.88,.78,.57),'Dark':(.035,.04,.038),'Hair':(.10,.055,.035),'Ochre':(.83,.50,.13)},'render':{'size':[1800,800],'samples':24,'spacing':1.15,'world':[.16,.20,.25],'key':[0,-5,9],'energy':2200,'area_size':10,'lens':55}}
import bpy,bmesh,math,json,ast
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[1];P=CONFIG['prefix'];O=R/CONFIG['out'];E=R/CONFIG['exports'];M={};active=[];items={};tops={};S=CONFIG['shape'];O.mkdir(parents=True,exist_ok=True);E.mkdir(parents=True,exist_ok=True)
for o in list(bpy.data.objects):
 if o.name.startswith(P):bpy.data.objects.remove(o,do_unlink=True)
for m in list(bpy.data.materials):
 if m.name.startswith(P):bpy.data.materials.remove(m,do_unlink=True)
scene=bpy.data.scenes.new(P+'Wardrobe');bpy.context.window.scene=scene;bpy.context.preferences.filepaths.save_version=0
for n,c in CONFIG['colors'].items():
 m=bpy.data.materials.new(P+n);m.diffuse_color=(*c,1);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*c,1);m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.72;M[n]=m
source=ast.parse((R/CONFIG['helpers']).read_text());exec(compile(ast.Module(body=[n for n in source.body if isinstance(n,ast.FunctionDef) and n.name in {'own','mesh','box','orb','tube','loft','text','bind'}],type_ignores=[]),'helpers','exec'))
needed=['FestivalRig','HairUnderHat']+[f'{c}_{v}' for c in CONFIG['styles'] for v in range(4)]+[f'HairTop_{v}' for v in range(4)]
with bpy.data.libraries.load(str(R/CONFIG['source']),link=False) as (a,b):b.objects=needed
rig=next(o for o in b.objects if o.type=='ARMATURE');bpy.context.collection.objects.link(rig);rig.name=P+'Rig';rig.animation_data_clear()
for bone in rig.pose.bones:bone.rotation_mode='XYZ';bone.rotation_euler=(0,0,0);bone.location=(0,0,0)
for o in b.objects:
 if o==rig:continue
 bpy.context.collection.objects.link(o);o.parent=rig
 for md in o.modifiers:
  if md.type=='ARMATURE':md.object=rig
 uv=o.data.uv_layers.active;idx=[min(7,int(uv.data[p.loop_indices[0]].uv.x*8)) for p in o.data.polygons];o.data.materials.clear()
 for m in M.values():o.data.materials.append(m)
 for f,i in zip(o.data.polygons,idx):f.material_index=i
 o.name=o.name.split('.')[0];items[o.name]=o
 if o.data.shape_keys:
  o.data.shape_keys.animation_data_clear()
  for k in o.data.shape_keys.key_blocks:k.value=0

def clone(base,name):
 o=items[base].copy();o.data=items[base].data.copy();bpy.context.collection.objects.link(o);o.name=name;return o

def deform(o,fn):
 m=o.matrix_parent_inverse@o.matrix_basis;mi=m.inverted()
 if o.data.shape_keys:
  for k in o.data.shape_keys.key_blocks:
   for v in k.data:v.co=mi@fn(m@v.co)
 for v in o.data.vertices:v.co=mi@fn(m@v.co)

def fitted(o,category,bone):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);bind(o,rig,bone);o.shape_key_add(name='Basis');F=CONFIG['fit'];anchor=-F['arm'] if bone=='ArmL' else F['arm'] if bone=='ArmR' else -F['leg'] if bone in ['LegL','ShinL','FootL'] else F['leg'] if bone in ['LegR','ShinR','FootR'] else 0
 for g in range(2):
  for b in range(3):
   factor=(F['head_base']+b*F['head_step'])/F['head_ref'] if category in ['Headgear','Sunglasses','FacialHair','Hairstyle','HairTop'] else (F['male'] if g==0 else F['female'])*F['widths'][b];key=o.shape_key_add(name=f'Fit_{g}_{b}')
   for v in key.data:v.co.x=anchor+(v.co.x-anchor)*factor
 return o

def collect(parts,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0]
 if len(parts)>1:bpy.ops.object.join()
 o=bpy.context.object;o.name=name;items[name]=o;return o

for cat in CONFIG['styles']:
 for v in range(4,10):
  active=[];name=f'{cat}_{v}';parts=[]
  if cat in CONFIG['base']:
   o=clone(f'{cat}_{CONFIG["base"][cat][v-4]}',name);parts=[o]
  if cat=='Headgear':
   if v==4:deform(o,lambda p:Vector((p.x*(1.25 if p.z<2.13 else 1),p.y*(1.25 if p.z<2.13 else 1),p.z)))
   if v==5:
    for x in [-S['hat_radius']*.68,S['hat_radius']*.68]:fitted(orb('TwinPom',(x,0,S['hat_z']+.10),(S['pom'],)*3,'Coral'),cat,'Head')
   if v==6:
    for x in [-S['hat_radius']*.65,S['hat_radius']*.65]:fitted(tube('Antenna',[(x,0,S['hat_z']),(x*1.3,0,S['hat_z']+S['antenna_height'])],S['cord'],'Ink'),cat,'Head');fitted(orb('AntennaTip',(x*1.3,0,S['hat_z']+S['antenna_height']),(S['pom']*.5,)*3,'Ochre'),cat,'Head')
   if v==7:deform(o,lambda p:Vector((p.x,p.y,2.1+(p.z-2.1)*1.6)))
   if v==8:
    deform(o,lambda p:Vector((p.x*1.15,p.y*1.15,p.z)));fitted(orb('MushroomCrown',(0,0,S['hat_z']+.16),(S['hat_radius']*1.2,S['hat_radius']*1.1,.16),'Coral'),cat,'Head')
    for i in range(5):a=i*math.tau/5;fitted(orb('CapSpot',(.20*math.cos(a),.18*math.sin(a),2.49),(.04,.04,.012),'Cream'),cat,'Head')
   if v==9:deform(o,lambda p:Vector((p.x,p.y*.70 if p.y<-.28 else p.y,p.z)));fitted(tube('TieTail',[(.22,.12,2.1),(.30,.20,1.95),(.26,.20,1.78)],S['cord']*3,'Coral'),cat,'Head')
  elif cat=='Sunglasses':
   for side in [-1,1]:
    pts=[]
    for i in range(S['samples']):
     a=i*math.tau/S['samples'];x=math.cos(a);z=math.sin(a)
     if v in [4,7]:steps=6 if v==4 else 8;a=math.floor(i*steps/S['samples'])*math.tau/steps;x=math.cos(a);z=math.sin(a)
     if v==5:x=math.sin(a)**3;z=(13*math.cos(a)-5*math.cos(2*a)-2*math.cos(3*a)-math.cos(4*a))/16
     if v==6:x=math.copysign(abs(x)**.6,x);z=z*.7+side*x*.28
     if v==8:x=math.copysign(abs(x)**.35,x);z=math.copysign(abs(z)**.5,z)*.65
     if v==9:r=1+.15*math.cos(a*6);x*=r;z*=r
     p=(side*S['glasses_x']+x*S['lens_w'],S['glasses_y'],S['glasses_z']+z*S['lens_h'])
     if not pts or Vector(p)!=Vector(pts[-1]):pts.append(p)
    fitted(mesh('Lens',pts,[tuple(range(len(pts)))],'Ink'),cat,'Head');fitted(tube('Frame',pts+[pts[0]],S['rim'],'Ochre'),cat,'Head');fitted(tube('Temple',[(side*(S['glasses_x']+S['lens_w']),S['glasses_y'],S['glasses_z']),(side*.26,-.12,1.975),(side*.26,.0,1.955)],S['rim'],'Ochre'),cat,'Head')
   fitted(tube('Bridge',[(-.02,S['glasses_y'],1.97),(0,S['glasses_y']+.015,1.96),(.02,S['glasses_y'],1.97)],S['rim'],'Ochre'),cat,'Head')
  elif cat=='Shirt':
   if v in [5,9]:deform(o,lambda p:Vector((p.x,p.y,p.z-S['shirt_extension']*max(0,1-(p.z-.83)/.35))))
   if v==6:deform(o,lambda p:Vector((p.x,p.y,p.z+S['crop']*max(0,1-(p.z-.88)/.32))))
   for f in o.data.polygons:
    z=sum(o.data.vertices[i].co.z for i in f.vertices)/len(f.vertices);x=sum(o.data.vertices[i].co.x for i in f.vertices)/len(f.vertices)
    if f.material_index==1 and ((v==4 and int(z/.09)%2) or (v==8 and x<0) or (v==7 and abs(x)>.29)):f.material_index=4 if v==4 else 3
   if v==9:
    for x in [-.19,.19]:fitted(box('TourPocket',(x,-.25,1.28),S['pocket_size'],'Teal'),cat,'Spine')
  elif cat=='Pants':
   if v==4:
    for side,x in [('L',-.20),('R',.20)]:fitted(box('CargoPocket',(x,-.17,.64),S['pocket_size'],'Teal'),cat,'Leg'+side)
   if v==5:deform(o,lambda p:Vector(((.20 if p.x>0 else -.20)+(p.x-(.20 if p.x>0 else -.20))*(1+.6*max(0,1-p.z/.65)),p.y,p.z)))
   if v==6:deform(o,lambda p:Vector((p.x,p.y,p.z+.07*max(0,1-(p.z-.55)/.3) if p.x>0 else p.z)))
   if v==7:deform(o,lambda p:Vector((p.x*(1+.035*math.sin(math.atan2(p.y,p.x)*12)),p.y*(1+.035*math.sin(math.atan2(p.y,p.x)*12)),p.z)))
   if v==8:deform(o,lambda p:Vector((p.x*1.12,p.y*1.12,p.z)))
   if v==9:
    for side,x in [('L',-.20),('R',.20)]:fitted(loft('TrouserCuff',[(.25,.12,.13),(.33,.12,.13)],'Cream',cx=x),cat,'Shin'+side)
  elif cat=='Shoes':
   if v==4:
    for f in o.data.polygons:
     if f.material_index==3:f.material_index=4
   if v==5:deform(o,lambda p:Vector((p.x,p.y,p.z+S['sole_gain']*max(0,min(1,(p.z-.1)/.1)))))
   if v==6:deform(o,lambda p:Vector(((.20 if p.x>0 else -.20)+(p.x-(.20 if p.x>0 else -.20))*1.13,p.y*1.08,p.z*1.12)))
   if v==7:
    for side,x in [('L',-.20),('R',.20)]:fitted(tube('TrailLace',[(x-.08,-.13,.20),(x+.08,-.15,.20),(x-.08,-.18,.18),(x+.08,-.21,.16)],S['cord'],'Coral'),cat,'Foot'+side)
   if v in [8,9]:
    for f in o.data.polygons:
     if f.material_index==3 and (v==8 or sum(o.data.vertices[i].co.x for i in f.vertices)>0):f.material_index=1 if v==9 else 7
  elif cat=='FacialHair':
   if v in [4,5]:
    for s in [-1,1]:fitted(tube('Moustache',[(s*.01,-.245,1.787),(s*.055,-.25,1.795),(s*.105,-.21,1.78 if v==4 else 1.82)],.019,'Hair'),cat,'Head')
   if v==6:
    for s in [-1,1]:fitted(loft('ForkBeard',[(1.50,.025,.03),(1.63,.055,.047),(1.69,.052,.033)],'Hair',cx=s*.065,cy=-.16),cat,'Head')
   if v==7:fitted(orb('SoulPatch',(0,-.212,1.68),(.027,.014,.032),'Hair'),cat,'Head')
   if v in [8,9]:
    for i in range(9):a=math.pi+(i/8)*math.pi;fitted(orb('BeardCurl',(.18*math.cos(a),.16*math.sin(a)-.035,1.67 if v==8 else 1.63+abs(math.cos(a))*.08),(.047,.035,.035 if v==8 else .065),'Hair'),cat,'Head')
  elif cat=='Hairstyle':
   top=clone(f'HairTop_{CONFIG["base"][cat][v-4]}',f'HairTop_{v}');tops[top.name]=top
   if v in [4,9]:deform(o,lambda p:Vector((p.x,p.y,p.z-S['hair_length']*max(0,1-(p.z-1.7)/.32))))
   if v==5:
    for x in [-.23,.23]:fitted(orb('HairBun',(x,.09,2.13),(.09,.08,.09),'Hair'),cat,'Head')
   if v==6:deform(top,lambda p:Vector((p.x,p.y,2.1+(p.z-2.1)*1.6)))
   if v==7:deform(top,lambda p:Vector((p.x+.035*max(0,(p.z-2.1)/.15),p.y,p.z)))
   if v==8:
    for i in range(9):a=i*math.tau/9;fitted(orb('Curl',(.22*math.cos(a),.19*math.sin(a),2.12),[S['curl']]*3,'Hair'),cat,'Head')
  elif cat=='Accessory':
   if v in [4,5,7]:
    fitted(tube('NeckCord',[(-.13,-.15,S['neck_z']),(-.12,-.27,1.32),(0,-.29,S['pendant_z']),(.12,-.27,1.32),(.13,-.15,S['neck_z'])],S['cord'],'Dark'),cat,'Spine')
    if v==7:fitted(box('PassBadge',(0,-.30,S['pendant_z']-.06),S['badge_size'],'Cream'),cat,'Spine')
    else:
     pts=[(.06*math.cos(i*math.tau/24),-.30,S['pendant_z']-.06+.06*math.sin(i*math.tau/24)) for i in range(25)];fitted(tube('Pendant',pts if v==4 else [(x,y,z) for x,y,z in pts[4:21]],S['cord']*(2 if v==4 else 1),'Ochre'),cat,'Spine')
     if v==4:
      for i in range(8):a=i*math.tau/8;fitted(tube('SunRay',[(.07*math.cos(a),-.3,S['pendant_z']-.06+.07*math.sin(a)),(.09*math.cos(a),-.3,S['pendant_z']-.06+.09*math.sin(a))],S['cord']*.6,'Ochre'),cat,'Spine')
   if v==6:fitted(box('BeltBag',(0,-.31,.96),(.37,.13,.19),'Coral'),cat,'Spine');fitted(tube('WaistStrap',[(.35*math.cos(i*math.tau/24),.29*math.sin(i*math.tau/24),1.02) for i in range(25)],S['cord']*2,'Dark'),cat,'Spine')
   if v==8:
    verts=[]
    for z,r in [(1.55,.16),(1.30,.38)]:
     for i in range(17):a=i*math.pi/16;verts.append((r*math.cos(a),.05+r*.78*math.sin(a),z))
    fitted(mesh('Capelet',verts,[(i,i+1,i+18,i+17) for i in range(16)],'Coral'),cat,'Spine')
   if v==9:
    for s in [-1,1]:fitted(orb('EarDefender',(s*.29,.015,1.94),(.053,.090,.10),'Teal'),cat,'Head')
    fitted(tube('Headband',[(.30*math.cos(i*math.pi/24),.015,1.94+.29*math.sin(i*math.pi/24)) for i in range(25)],S['cord']*2,'Dark'),cat,'Head')
  collect(parts+active,name)
items.update(tops)
for key,o in items.items():
 o['wardrobe_slot']=key;o.name=P+key
 for k in o.data.shape_keys.key_blocks if o.data.shape_keys else []:k.value=0
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in items.values():o.select_set(True)
bpy.context.view_layer.objects.active=rig;bpy.ops.export_scene.fbx(filepath=str(E/(P+'WardrobeLibrary.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
(O/'wardrobe-manifest.json').write_text(json.dumps({'styles':CONFIG['styles'],'source':CONFIG['source'],'meshes':list(items),'fits':['Fit_'+str(g)+'_'+str(b) for g in range(2) for b in range(3)],'notes':['32 retained originals and 48 incremental variants, plus shared hair-cap and ten hair-top companions.','Variants include geometric changes and explicit color-block styles. All outfit-combination fit and native LOD acceptance deferred.']},indent=2)+'\n');(E/'wardrobe-palette.json').write_text(json.dumps({'entries':[{'name':P+n,'rgb':list(c),'roughness':.72} for n,c in CONFIG['colors'].items()]},indent=2)+'\n')
G=CONFIG['render'];scene.render.engine='CYCLES';scene.cycles.samples=G['samples'];scene.render.resolution_x,scene.render.resolution_y=G['size'];scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new(P+'World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(*G['world'],1);d=bpy.data.lights.new(P+'Light','AREA');d.energy=G['energy'];d.size=G['area_size'];light=bpy.data.objects.new(P+'Light',d);bpy.context.collection.objects.link(light);light.location=G['key'];light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler();d=bpy.data.cameras.new(P+'Camera');cam=bpy.data.objects.new(P+'Camera',d);bpy.context.collection.objects.link(cam);scene.camera=cam;cam.data.lens=G['lens'];scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(O/'WardrobeExtension.blend'))
for category in CONFIG['styles']:
 for key,o in items.items():o.hide_render=not key.startswith(category+'_')
 for i in range(10):items[f'{category}_{i}'].location.x=(i%5-2)*G['spacing'];items[f'{category}_{i}'].location.y=(i//5)*1.2
 target=Vector((0,.5,1.3 if category in ['Shirt','Accessory'] else .65 if category=='Pants' else .20 if category=='Shoes' else 1.95));cam.data.type='ORTHO';cam.data.ortho_scale=6.5;cam.location=target+Vector((.6,-8,2));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(O/('wardrobe-'+category.lower()+'.png'));bpy.ops.render.render(write_still=True)
 for i in range(10):items[f'{category}_{i}'].location=(0,0,0)
