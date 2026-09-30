CONFIG = {
 'prefix':'AH03P_','source':'ArtSource/Generated/FestivalCharacter.blend','out':'ArtSource/ProductionPackage03','exports':'Assets/Festival/Art/ProductionPeople03','mesh':{'sides':24,'rings':14,'bevel':.008,'bevel_segments':2,'tube_sides':8},
 'face':{'eye_x':.103,'eye_z':1.968,'eye_size':[.064,.028,.068],'pupil_size':[.024,.007,.031],'iris_size':[.034,.007,.039],'eye_y':-.225,'pupil_y':-.269,'iris_y':-.260,'brow_z':2.059,'brow_radius':.014,'brow_width':.075,'brow_steps':8,'mouth_z':1.747,'mouth_width':.090,'mouth_depth':.006,'mouth_height':.013,'smile':.027,'alarm':.052,'nose':[.054,.079,.070],'nose_pos':[.007,-.254,1.858],'cheek_x':.168,'cheek_z':1.822,'cheek_size':[.042,.010,.030],'lid_radius':.008,'catchlight_size':[.009,.005,.010],'blink_depth':.061,'blink_scale':.012,'eye_offset':-.006},
 'cast':[{'name':'AttendeeLanky','gender':0,'body':0,'shirt':0,'pants':0,'shoes':0,'hair':2,'hat':2,'role':None,'skin':'SkinWarm','top':'Coral','accent':'Ochre','hair_color':'HairDark'},{'name':'AttendeeAverage','gender':1,'body':1,'shirt':3,'pants':2,'shoes':1,'hair':1,'hat':None,'role':None,'skin':'SkinDeep','top':'Teal','accent':'Coral','hair_color':'HairPurple'},{'name':'AttendeeStocky','gender':0,'body':2,'shirt':1,'pants':1,'shoes':2,'hair':3,'hat':1,'role':None,'skin':'SkinLight','top':'Ochre','accent':'Teal','hair_color':'HairGrey'},{'name':'VendorSupplies','gender':1,'body':1,'shirt':0,'pants':1,'shoes':0,'hair':3,'hat':None,'role':'Vendor0','skin':'SkinWarm','top':'Coral','accent':'Teal','hair_color':'HairDark'},{'name':'VendorPerformance','gender':0,'body':2,'shirt':2,'pants':0,'shoes':0,'hair':0,'hat':0,'role':'Vendor1','skin':'SkinDeep','top':'Teal','accent':'Ochre','hair_color':'HairDark'},{'name':'VendorStock','gender':0,'body':0,'shirt':3,'pants':1,'shoes':1,'hair':3,'hat':None,'role':'Vendor2','skin':'SkinLight','top':'Ochre','accent':'Coral','hair_color':'HairGrey'},{'name':'Medic','gender':1,'body':1,'shirt':0,'pants':1,'shoes':1,'hair':1,'hat':None,'role':'Medic','skin':'SkinDeep','top':'Teal','accent':'Coral','hair_color':'HairDark'},{'name':'Security','gender':0,'body':2,'shirt':0,'pants':1,'shoes':2,'hair':0,'hat':2,'role':'Security','skin':'SkinWarm','top':'Ink','accent':'Ochre','hair_color':'HairDark'},{'name':'MissingFriend','gender':1,'body':0,'shirt':2,'pants':0,'shoes':0,'hair':2,'hat':None,'role':'Friend','skin':'SkinLight','top':'Coral','accent':'Teal','hair_color':'HairPurple'}], 'motion':{'fps':30,'frames':61,'spine_sway':.19,'head_sway':.12,'dance_arm':.65,'dance_forearm':.85,'offer_arm':-.85,'offer_forearm':-.65},'render':{'size':[1800,1050],'samples':40,'world':[.14,.19,.25],'camera':[7,-15,6],'target':[0,0,1.25],'lens':52,'key':[0,-7,9],'energy':2000,'fill':[-6,2,7],'fill_energy':1600,'light_size':8,'spacing':1.4,'row_spacing':3.3},
 'colors':{'SkinWarm':(.62,.32,.19),'SkinDeep':(.27,.115,.070),'SkinLight':(.86,.60,.39),'Coral':(.72,.20,.15),'Teal':(.055,.29,.28),'Ochre':(.83,.50,.13),'Cream':(.88,.78,.57),'Ink':(.026,.042,.055),'White':(.95,.92,.82),'HairDark':(.055,.037,.025),'HairGrey':(.29,.30,.29),'HairPurple':(.24,.10,.25),'Iris':(.13,.26,.20),'Mouth':(.045,.012,.009),'Tongue':(.68,.26,.22),'Ground':(.16,.20,.17)},'roughness':.68}
import bpy,bmesh,math,json,ast
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
R=Path(__file__).resolve().parents[1];P=CONFIG['prefix'];O=R/CONFIG['out'];E=R/CONFIG['exports'];O.mkdir(parents=True,exist_ok=True);E.mkdir(parents=True,exist_ok=True)
for o in list(bpy.data.objects):
 if o.name.startswith(P):bpy.data.objects.remove(o,do_unlink=True)
for m in list(bpy.data.materials):
 if m.name.startswith(P):bpy.data.materials.remove(m,do_unlink=True)
scene=bpy.data.scenes.new(P+'People');bpy.context.window.scene=scene;bpy.context.preferences.filepaths.save_version=0
M={};active=[];cast={};manifest={}
for n,c in CONFIG['colors'].items():
 m=bpy.data.materials.new(P+n);m.diffuse_color=(*c,1);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=CONFIG['roughness'];M[n]=m
source=ast.parse((R/'scripts/create_first_production_package.py').read_text());exec(compile(ast.Module(body=[n for n in source.body if isinstance(n,ast.FunctionDef) and n.name in {'own','mesh','box','orb','tube','loft','text','bind'}],type_ignores=[]),'helpers','exec'))

def shaped(o,poses):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.shape_key_add(name='Basis')
 for n,fn in poses.items():
  k=o.shape_key_add(name=n)
  for v,d in zip(o.data.vertices,k.data):d.co=fn(v.co.copy())
 bind(o,rig,'Head');return o

def join(objs,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0]
 if len(objs)>1:bpy.ops.object.join()
 o=bpy.context.object;o.name=P+name;return o

def write_fbx(name,objs,anim=False):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.hide_set(False);o.select_set(True)
 roots=[(o,o.name) for o in objs if o.type=='ARMATURE']
 for o,oldname in roots:o.name='FestivalRig'
 bpy.context.view_layer.objects.active=objs[0];bpy.ops.export_scene.fbx(filepath=str(E/(P+name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=anim,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=anim,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
 for o,oldname in roots:o.name=oldname

for index,C in enumerate(CONFIG['cast']):
 active=[];n=C['name'];g=C['gender'];b=C['body'];names=['FestivalRig',f'Body_{g}_{b}',f'Shirt_{C["shirt"]}',f'Pants_{C["pants"]}',f'Shoes_{C["shoes"]}',f'Hairstyle_{C["hair"]}']
 names+=['HairUnderHat',f'Headgear_{C["hat"]}'] if C['hat'] is not None else [f'HairTop_{C["hair"]}']
 if C['role']:names+=['Role_'+C['role']]
 if n=='MissingFriend':names+=['Accessory_0']
 with bpy.data.libraries.load(str(R/CONFIG['source']),link=False) as (a,bb):bb.objects=names
 rig=next(o for o in bb.objects if o.type=='ARMATURE');bpy.context.collection.objects.link(rig);rig.name=P+n+'Rig';rig.animation_data_clear()
 for bone in rig.pose.bones:bone.rotation_mode='XYZ';bone.rotation_euler=(0,0,0);bone.location=(0,0,0)
 palette=[C['skin'],C['top'],'Ink','Teal','Cream','Ink',C['hair_color'],C['accent']]
 for o in bb.objects:
  if o==rig:continue
  bpy.context.collection.objects.link(o);o.parent=rig
  for md in o.modifiers:
   if md.type=='ARMATURE':md.object=rig
  if o.data.shape_keys:
   ks=o.data.shape_keys.key_blocks;fit=ks.get(f'Fit_{g}_{b}')
   if fit:
    delta=[fit.data[i].co-ks[0].data[i].co for i in range(len(o.data.vertices))]
    for k in ks:
     if not k.name.startswith('Fit_'):
      for i,dt in enumerate(delta):k.data[i].co+=dt
    for k in list(ks):
     if k.name.startswith('Fit_'):o.shape_key_remove(k)
   for k in o.data.shape_keys.key_blocks:k.value=0
   o.data.shape_keys.animation_data_clear()
  uv=o.data.uv_layers.active;indices=[min(7,int(uv.data[p.loop_indices[0]].uv.x*8)) for p in o.data.polygons];o.data.materials.clear()
  for c in palette:o.data.materials.append(M[c])
  for f,idx in zip(o.data.polygons,indices):f.material_index=idx
  if o.name.startswith('Pants') or o.name.startswith('Role_Friend'):
   if o.data.shape_keys:
    coords=[v.co.copy() for v in o.data.shape_keys.key_blocks[0].data];o.shape_key_clear()
    for v,co in zip(o.data.vertices,coords):v.co=co
   bm=bmesh.new();bm.from_mesh(o.data)
   if o.name.startswith('Pants'):remove=[f for f in bm.faces if f.material_index==7]
   else:
    visited=set();remove=[]
    for v in bm.verts:
     if v in visited:continue
     component={v};todo=[v];visited.add(v)
     while todo:
      q=todo.pop()
      for e in q.link_edges:
       q2=e.other_vert(q)
       if q2 not in visited:visited.add(q2);component.add(q2);todo.append(q2)
     if sum(v.co.x for v in component)/len(component)>.3 and sum(v.co.y for v in component)/len(component)>.2:remove.extend({f for v in component for f in v.link_faces})
   bmesh.ops.delete(bm,geom=list(set(remove)),context='FACES');bm.to_mesh(o.data);bm.free()
  o.name=P+n+'_'+o.name;active.append(o)
 body=next(o for o in active if '_Body_' in o.name);tree=BVHTree.FromPolygons([v.co for v in body.data.vertices],[tuple(p.vertices) for p in body.data.polygons])
 def seat(x,z,off=0):
  hit=tree.ray_cast(Vector((x,-2,z)),Vector((0,1,0)))[0];return Vector((x,(hit.y if hit is not None else -.2)-off,z))
 F=CONFIG['face'];start=len(active)
 for s in [-1,1]:
  x=s*F['eye_x'];z=F['eye_z'];cy=seat(x,z).y-F['eye_offset'];eye_center=Vector((x,cy,z))
  def blink(v,x=x,z=z):return Vector((v.x,cy+(v.y-cy)*F['blink_scale'],z+(v.z-z)*F['blink_scale']))
  def tired(v,z=z):return Vector((v.x,v.y,z+(v.z-z)*.55))
  shaped(orb('Sclera',(x,cy,z),F['eye_size'],'White'),{'Blink':blink,'Exhausted':tired})
  iy=cy-F['eye_size'][1];py=iy-F['pupil_size'][1]*.75
  shaped(orb('Iris',(x,iy,z),F['iris_size'],'Iris'),{'Blink':blink,'Exhausted':tired})
  shaped(orb('Pupil',(x,py,z),F['pupil_size'],'Ink'),{'Blink':blink,'Exhausted':tired})
  shaped(orb('EyeGlint',(x-F['eye_size'][0]*.20,py-F['pupil_size'][1],z+F['eye_size'][2]*.25),F['catchlight_size'],'White'),{'Blink':blink,'Exhausted':tired})
  by=cy+F['eye_size'][1]*.85
  pts=[(x+F['eye_size'][0]*1.04*math.cos(i*math.tau/24),cy,z+F['eye_size'][2]*1.02*math.sin(i*math.tau/24)) for i in range(25)]
  shaped(tube('EyeSocket',pts,F['lid_radius'],C['skin']),{'Blink':lambda v:Vector((v.x,seat(v.x,z,-.002).y,z+(v.z-z)*.02))})
  crease=[(x+q*F['eye_size'][0]*.85,by+.01,z-.007*(1-q*q)) for q in [-1,-.66,-.33,0,.33,.66,1]]
  shaped(tube('ClosedLidCrease',crease,F['lid_radius']*.32,C['hair_color']),{'Blink':lambda v:Vector((v.x,seat(v.x,v.z,.004).y,v.z))})
  pts=[seat(x+q*F['brow_width'],F['brow_z']+.01*(1-q*q),F['brow_radius']*.5) for q in [-1+i*2/F['brow_steps'] for i in range(F['brow_steps']+1)]]
  def brow(v,mode,s=s,x=x):
   q=(v.x-x)/F['brow_width'];dz={'Delight':.032,'Alarm':.055,'Concern':-.025*s*q,'Suspicious':.028*s*q-.018,'Exhausted':-.025,'Confused':.040*s}[mode];return v+Vector((0,0,dz))
  shaped(tube('ExpressiveBrow',pts,F['brow_radius'],C['hair_color']),{m:lambda v,m=m:brow(v,m) for m in ['Delight','Alarm','Concern','Suspicious','Exhausted','Confused']})
  cheek=orb('Cheek',seat(s*F['cheek_x'],F['cheek_z'],-.006),F['cheek_size'],C['skin']);shaped(cheek,{'Delight':lambda v:v+Vector((0,-.004,.010))})
 nose=orb('Nose',F['nose_pos'],F['nose'],C['skin']);shaped(nose,{})
 for s in [-1,1]:shaped(orb('Nostril',(s*F['nose'][0]*.5,F['nose_pos'][1]-F['nose'][1]*.70,F['nose_pos'][2]-F['nose'][2]*.6),(.009,.006,.005),C['hair_color']),{})
 z=F['mouth_z'];center=seat(0,z,F['mouth_depth']*.7);mw=F['mouth_width'];mh=F['mouth_height']
 def mouth(v,mode):
  q=v.x/mw;zz=v.z-z
  if mode=='Delight':return Vector((v.x*1.16,v.y,z+zz*2.0+F['smile']*q*q))
  if mode=='Alarm':return Vector((v.x*.58,v.y-.008,z+zz*F['alarm']/mh))
  if mode=='Concern':return Vector((v.x*.85,v.y,z+zz-F['smile']*q*q))
  if mode=='Suspicious':return Vector((v.x*.90,v.y,z+zz+q*F['smile']*.4))
  if mode=='Confused':return Vector((v.x*.70,v.y,z+zz*1.3+q*F['smile']*.5))
  return Vector((v.x*.7,v.y,z+zz*1.8))
 shaped(orb('MouthCavity',center,(mw,F['mouth_depth'],mh),'Mouth'),{m:lambda v,m=m:mouth(v,m) for m in ['Delight','Alarm','Concern','Suspicious','Exhausted','Confused']})
 face_parts=active[start:];face=join(face_parts,n+'_Face');active=active[:start]+[face];face.data.shape_keys.animation_data_clear()
 for k in face.data.shape_keys.key_blocks:k.value=0
 garment=[o for o in active if o!=face and not o.name.startswith(P+n+'_Body') and not o.data.shape_keys]
 if garment:
  joined=join(garment,n+'_Wardrobe');active=[o for o in active if o not in garment]+[joined]
 objs=[rig]+active;write_fbx(n,objs);manifest[n]={'fbx':P+n+'.fbx','body':f'{g}_{b}','role':C['role'],'face':face.name,'expressions':[k.name for k in face.data.shape_keys.key_blocks if k.name!='Basis'],'source':'Original modular base plus new unified expressive face','triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in active),'armature':rig.name}
 rig.location=((index%3-1)*CONFIG['render']['spacing'],-(index//3)*CONFIG['render']['row_spacing'],0);cast[n]={'rig':rig,'objects':objs,'face':face}

first=next(iter(cast.values()));rig=first['rig'];old=rig.location.copy();rig.location=(0,0,0);T=CONFIG['motion'];scene.render.fps=T['fps'];scene.frame_start=1;scene.frame_end=T['frames'];rig.animation_data_create()
for label in ['Idle','Dance','Offer','Assist','Panic','Cheer']:
 action=bpy.data.actions.new(P+label);rig.animation_data.action=action
 for frame in range(1,T['frames']+1):
  t=(frame-1)/(T['frames']-1)*math.tau
  for b in rig.pose.bones:b.rotation_euler=(0,0,0);b.location=(0,0,0)
  rig.pose.bones['Head'].rotation_euler.z=T['head_sway']*math.sin(t)
  if label=='Dance':
   rig.pose.bones['Spine'].rotation_euler.z=T['spine_sway']*math.sin(t)
   for s,side in [(-1,'L'),(1,'R')]:rig.pose.bones['Arm'+side].rotation_euler=(-T['dance_arm']*(.8+.2*math.sin(t+s)),0,s*.35);rig.pose.bones['Forearm'+side].rotation_euler=(-T['dance_forearm']*(.8+.2*math.cos(t+s)),0,0)
  if label in ['Offer','Assist']:
   rig.pose.bones['Spine'].rotation_euler.x=.15*(1-math.cos(t))
   for side in (['R'] if label=='Offer' else ['R','L']):rig.pose.bones['Arm'+side].rotation_euler.x=T['offer_arm']*(1-math.cos(t))*.5;rig.pose.bones['Forearm'+side].rotation_euler.x=T['offer_forearm']*(1-math.cos(t))*.5
  if label in ['Panic','Cheer']:
   for s,side in [(-1,'L'),(1,'R')]:rig.pose.bones['Arm'+side].rotation_euler=(-.55,0,s*(1.7+.12*math.sin(t)));rig.pose.bones['Forearm'+side].rotation_euler.x=-.7+.15*math.sin(t+s)
  for b in rig.pose.bones:b.keyframe_insert(data_path='rotation_euler',frame=frame)
 track=rig.animation_data.nla_tracks.new();track.name=label;strip=track.strips.new(label,1,action);track.mute=True
rig.animation_data.action=None
for tr in rig.animation_data.nla_tracks:tr.mute=False
write_fbx('Motion',[rig],True)
for tr in rig.animation_data.nla_tracks:tr.mute=True
rig.animation_data.action=None;rig.location=old
for b in rig.pose.bones:b.rotation_euler=(0,0,0)
scene.frame_set(1)
G=CONFIG['render'];active=[];box('Ground',(0,-G['row_spacing'], -.06),(10,13,.12),'Ground');scene.render.engine='CYCLES';scene.cycles.samples=G['samples'];scene.render.resolution_x,scene.render.resolution_y=G['size'];scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new(P+'World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(*G['world'],1)
for name,pos,energy in [('Key',G['key'],G['energy']),('Fill',G['fill'],G['fill_energy'])]:
 d=bpy.data.lights.new(P+name,'AREA');d.energy=energy;d.shape='DISK';d.size=G['light_size'];o=bpy.data.objects.new(P+name,d);bpy.context.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector(G['target'])-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new(P+'Camera');cam=bpy.data.objects.new(P+'Camera',d);bpy.context.collection.objects.link(cam);scene.camera=cam;cam.location=G['camera'];cam.rotation_euler=(Vector((0,-G['row_spacing'],1.1))-cam.location).to_track_quat('-Z','Y').to_euler();d.lens=G['lens'];scene.view_settings.view_transform='AgX'
(O/'people-manifest.json').write_text(json.dumps({'cast':manifest,'motion':P+'Motion.fbx','notes':['Cast meshes exported without animation curves: facial poses remain independently controllable.','Rig-only motion file has six stationary acting clips; no locomotion or gameplay transfer claimed.','Existing six body fit shapes baked into each assembled outfit; raw master remains unchanged.']},indent=2)+'\n');(E/'palette.json').write_text(json.dumps({'entries':[{'name':P+n,'rgb':list(c),'roughness':CONFIG['roughness']} for n,c in CONFIG['colors'].items()]},indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=str(O/'PeopleUpgrade.blend'));scene.render.filepath=str(O/'people-preview.png');bpy.ops.render.render(write_still=True)
for a in cast.values():
 for o in a['objects']:o.hide_render=a!=first
rig=first['rig'];rig.location=(0,0,0);cam.location=(.0,-3.2,2.0);cam.rotation_euler=(Vector((0,0,1.93))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=85;scene.render.resolution_x=800;scene.render.resolution_y=800
for pose in ['Neutral','Blink','Delight','Alarm','Suspicious','Confused','Exhausted','Concern']:
 for k in first['face'].data.shape_keys.key_blocks:k.value=1 if k.name==pose else 0
 scene.render.filepath=str(O/('face-'+pose.lower()+'.png'));bpy.ops.render.render(write_still=True)
