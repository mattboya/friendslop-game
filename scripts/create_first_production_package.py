CONFIG = {
 'prefix':'AH01_', 'source':'ArtSource/Generated/FestivalCharacter.blend', 'out':'ArtSource/ProductionPackage', 'exports':'Assets/Festival/Art/ProductionSample', 'seed':29,
 'stall':{'width':4.8,'depth':2.8,'post':.105,'eave':2.85,'ridge':3.42,'counter':1.03,'counter_depth':.65,'plank':.045,'roof_x':14,'roof_y':12,'valance':.22}, 'path':{'width':3.2,'length':9,'segments':24,'clumps':62,'stones':20}, 'poi':{'length':.56,'handle':.125,'head':.085,'cord':.008,'sides':20}, 'mesh':{'sides':24,'rings':12,'bevel':.014,'bevel_segments':2,'tube_sides':8},
 'character':{'eye_x':.101,'eye_z':1.963,'eye_size':(.062,.039,.066),'pupil_size':(.024,.010,.031),'face_y':-.219,'pupil_y':-.261,'brow_z':2.051,'brow_radius':.011,'mouth_z':1.746,'mouth_width':.073,'mouth_radius':.006,'shirt_rings':[(.88,.33,.245),(1.02,.345,.252),(1.22,.35,.252),(1.40,.365,.237),(1.48,.29,.185),(1.56,.145,.133)],'apron_rings':[(.77,.31,.29),(1.02,.36,.30),(1.26,.34,.29),(1.47,.19,.248)],'fps':30,'frames':60,'attendee_pos':(-1.4,-1.55,0),'vendor_pos':(.4,.65,0),'attendee_scale':(.94,.94,1.04),'vendor_scale':(1.08,1.08,.97)},
 'colors':{'SkinA':(.67,.37,.23),'SkinV':(.82,.57,.36),'Coral':(.72,.20,.15),'Teal':(.055,.29,.28),'Ochre':(.83,.50,.13),'Cream':(.88,.78,.57),'Ink':(.026,.042,.055),'White':(.96,.93,.83),'HairA':(.14,.075,.047),'HairV':(.12,.12,.14),'Wood':(.26,.135,.066),'WoodLight':(.43,.25,.12),'Metal':(.20,.24,.25),'Rubber':(.038,.047,.042),'Ground':(.145,.155,.085),'Dirt':(.29,.18,.105),'Stone':(.30,.34,.28),'Leaf':(.09,.21,.12),'LeafLight':(.22,.32,.10),'Glow':(1,.57,.18),'MintGlow':(.16,.85,.63)}, 'surface':{'roughness':.72,'metal':.55,'noise_scale':85,'noise_strength':.085,'noise_distance':.014}, 'render':{'width':1400,'height':1050,'samples':48,'camera':(10,-15,9),'target':(0,0,1.3),'lens':48,'sun_energy':2,'area_energy':1400,'area_size':7,'world':(.10,.16,.25)}}
import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
R=Path(__file__).resolve().parents[1]
P=CONFIG['prefix']; O=R/CONFIG['out']; E=R/CONFIG['exports']
O.mkdir(parents=True,exist_ok=True); E.mkdir(parents=True,exist_ok=True)
for o in list(bpy.data.objects):
 if o.name.startswith(P): bpy.data.objects.remove(o,do_unlink=True)
for m in list(bpy.data.materials):
 if m.name.startswith(P): bpy.data.materials.remove(m,do_unlink=True)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.data.scenes.new(P+'Production');bpy.context.window.scene=scene
random.seed(CONFIG['seed'])
M={}; groups={}; active=[]; export_counts={}
for n,c in CONFIG['colors'].items():
 m=bpy.data.materials.new(P+n); m.diffuse_color=(*c,1); m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*c,1); bs.inputs['Roughness'].default_value=CONFIG['surface']['roughness']
 if n=='Metal': bs.inputs['Metallic'].default_value=CONFIG['surface']['metal']
 if n in ['Glow','MintGlow']:
  bs.inputs['Emission Color'].default_value=(*c,1); bs.inputs['Emission Strength'].default_value=2
 if n in ['Coral','Teal','Ochre','Wood','WoodLight','Dirt','Ground']:
  ns=m.node_tree.nodes; tex=ns.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=CONFIG['surface']['noise_scale']; bump=ns.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=CONFIG['surface']['noise_strength']; bump.inputs['Distance'].default_value=CONFIG['surface']['noise_distance']; m.node_tree.links.new(tex.outputs['Fac'],bump.inputs['Height']); m.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
 M[n]=m

def own(o,n,c):
 o.name=P+n; o.data.materials.clear(); o.data.materials.append(M[c]); active.append(o); return o

def mesh(n,v,f,c):
 d=bpy.data.meshes.new(P+n); d.from_pydata(v,[],f); d.update(); o=bpy.data.objects.new(P+n,d); bpy.context.collection.objects.link(o); own(o,n,c)
 bm=bmesh.new(); bm.from_mesh(d); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(d); bm.free(); return o

def box(n,p,s,c,bevel=True):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p); o=own(bpy.context.object,n,c); o.scale=s; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Edge finish','BEVEL'); mod.width=min(CONFIG['mesh']['bevel'],min(s)/5); mod.segments=CONFIG['mesh']['bevel_segments']; bpy.ops.object.modifier_apply(modifier=mod.name)
 return o

def orb(n,p,s,c):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=CONFIG['mesh']['sides'],ring_count=CONFIG['mesh']['rings'],radius=1,location=p); o=own(bpy.context.object,n,c); o.scale=s; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for f in o.data.polygons:f.use_smooth=True
 return o

def tube(n,pts,r,c):
 v=[]; f=[]; k=CONFIG['mesh']['tube_sides']
 for i,p in enumerate(pts):
  t=Vector(pts[min(i+1,len(pts)-1)])-Vector(pts[max(i-1,0)]); t.normalize(); a=t.cross(Vector((0,0,1)))
  if a.length<.01:a=t.cross(Vector((0,1,0)))
  a.normalize(); b=t.cross(a).normalized()
  for j in range(k):v.append(Vector(p)+r*(a*math.cos(j*math.tau/k)+b*math.sin(j*math.tau/k)))
 for i in range(len(pts)-1):
  for j in range(k):f.append((i*k+j,i*k+(j+1)%k,(i+1)*k+(j+1)%k,(i+1)*k+j))
 f.extend([tuple(reversed(range(k))),tuple((len(pts)-1)*k+j for j in range(k))]); return mesh(n,v,f,c)

def loft(n,rings,c,cx=0,cy=0):
 k=CONFIG['mesh']['sides'];v=[(cx+w*math.cos(j*math.tau/k),cy+d*math.sin(j*math.tau/k),z) for z,w,d in rings for j in range(k)];f=[]
 for i in range(len(rings)-1):
  for j in range(k):f.append((i*k+j,i*k+(j+1)%k,(i+1)*k+(j+1)%k,(i+1)*k+j))
 o=mesh(n,v,f,c)
 for p in o.data.polygons:p.use_smooth=True
 return o

def text(n,t,p,size,c):
 d=bpy.data.curves.new(P+n,'FONT');d.body=t;d.align_x='CENTER';d.size=size;d.extrude=.001; o=bpy.data.objects.new(P+n,d);bpy.context.collection.objects.link(o);o.location=p;o.rotation_euler=(math.pi/2,0,0);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o=bpy.context.object;own(o,n,c);o.select_set(False);return o

def export(n,objs,anim=False):
 source=list(objs);temporary=[]
 if n in ['Stall','Path']:
  batches={}
  for src in objs:
   o=src.copy();o.data=src.data.copy();bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o
   for md in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=md.name)
   batches.setdefault(o.data.materials[0].name,[]).append(o)
  for mat,parts in batches.items():
   bpy.ops.object.select_all(action='DESELECT')
   for o in parts:o.select_set(True)
   bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name=P+n+'_'+mat;temporary.append(o)
  objs=temporary
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0]
 for o in objs:
  if o.type=='MESH' and o.data.shape_keys:
   for k in o.data.shape_keys.key_blocks:k.value=0
 bpy.ops.export_scene.fbx(filepath=str(E/(P+n+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=anim,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=anim,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
 groups[n]=source;export_counts[n]=len(objs)
 for o in temporary:bpy.data.objects.remove(o,do_unlink=True)

def bind(o,rig,bone):
 o.parent=rig; vg=o.vertex_groups.new(name=bone);vg.add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('FestivalRig','ARMATURE');mod.object=rig

def character(vendor=False):
 global active
 active=[]; C=CONFIG['character']; shape=2 if vendor else 1; n='Vendor' if vendor else 'Attendee'; skin='SkinV' if vendor else 'SkinA'; hair='HairV' if vendor else 'HairA'; shirt='Ochre' if vendor else 'Coral'
 names=['FestivalRig',f'Body_0_{shape}','Pants_1' if vendor else 'Pants_0','Shoes_0','Headgear_0' if not vendor else 'Hairstyle_0','HairUnderHat' if not vendor else 'HairTop_0']
 with bpy.data.libraries.load(str(R/CONFIG['source']),link=False) as (a,b):b.objects=names
 loaded=b.objects;rig=next(o for o in loaded if o.type=='ARMATURE');rig.name=P+n+'Rig';bpy.context.collection.objects.link(rig)
 palette=[skin,shirt,'Ink','Teal','Cream','Ink',hair,'Ochre']
 for o in loaded:
  if o==rig:continue
  bpy.context.collection.objects.link(o);o.parent=rig
  for md in o.modifiers:
   if md.type=='ARMATURE':md.object=rig
  if o.data.shape_keys:
   ks=o.data.shape_keys.key_blocks;fit=ks.get('Fit_0_'+str(shape))
   if fit:
    delta=[fit.data[i].co-ks[0].data[i].co for i in range(len(o.data.vertices))]
    for k in ks:
     if not k.name.startswith('Fit_'):
      for i,dt in enumerate(delta):k.data[i].co+=dt
    for k in list(ks):
     if k.name.startswith('Fit_'):o.shape_key_remove(k)
  uv=o.data.uv_layers.active; indices=[min(7,int(uv.data[p.loop_indices[0]].uv.x*8)) for p in o.data.polygons]
  o.data.materials.clear()
  for c in palette:o.data.materials.append(M[c])
  for p,idx in zip(o.data.polygons,indices):p.material_index=idx
  if o.name.startswith('Pants'):
   if o.data.shape_keys:
    coords=[v.co.copy() for v in o.data.shape_keys.key_blocks[0].data];o.shape_key_clear()
    for v,co in zip(o.data.vertices,coords):v.co=co
   bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index==7],context='FACES');bm.to_mesh(o.data);bm.free()
  o.name=P+n+'_'+o.name;active.append(o)
 body=next(o for o in active if '_Body_' in o.name);tree=BVHTree.FromPolygons([body.matrix_world@v.co for v in body.data.vertices],[tuple(p.vertices) for p in body.data.polygons])
 def seat(x,z,offset):
  hit=tree.ray_cast(Vector((x,-2,z)),Vector((0,1,0)))[0]
  return (x,(hit.y if hit is not None else -.2)-offset,z)
 def part(o,bone='Head'):bind(o,rig,bone);return o
 def shape(o,name,fn):
  if not o.data.shape_keys:o.shape_key_add(name='Basis')
  k=o.shape_key_add(name=name,from_mix=False)
  for i,v in enumerate(o.data.vertices):k.data[i].co=fn(v.co.copy())
 for s in [-1,1]:
  x=C['eye_x']*s;z=C['eye_z'];eye=part(orb(n+'Eye',(x,C['face_y'],z),C['eye_size'],'White'))
  shape(eye,'Blink',lambda v:Vector((v.x,v.y,v.z*.045)))
  pupil=part(orb(n+'Pupil',(x-.008,C['pupil_y'],z),C['pupil_size'],'Ink'))
  shape(pupil,'Blink',lambda v:Vector((v.x,v.y+.025,v.z*.04)))
  part(orb(n+'Catchlight',(x-.015,C['pupil_y']-.010,z+.015),(.009,.005,.009),'White'))
  brow=part(tube(n+'Brow',[seat(x+t,C['brow_z']+.013*math.sin(i*math.pi/6)+s*t*.12,.013) for i,t in enumerate([-.065,-.043,-.022,0,.022,.043,.065])],C['brow_radius'],hair))
  shape(brow,'Concern',lambda v:Vector((v.x,v.y,v.z+.04*(1-min(1,abs(v.x)/.2)))))
  shape(brow,'Delight',lambda v:v+Vector((0,0,.025)))
 nose=part(orb(n+'Nose',(.006,-.247,1.854),(.053,.078,.073),skin))
 for s in [-1,1]:part(orb(n+'Nostril',(s*.028,-.303,1.815),(.010,.006,.006),hair))
 mouth=part(tube(n+'Mouth',[seat(t,C['mouth_z']+.012*(t/C['mouth_width'])**2,C['mouth_radius']) for t in [-.073,-.05,-.025,0,.025,.05,.073]],C['mouth_radius'],'Ink'))
 def mouth_pose(v,amount):
  z=v.z+amount*(abs(v.x)/C['mouth_width'])**1.5
  return Vector(seat(v.x,z,C['mouth_radius']))+Vector((0,v.y-seat(v.x,v.z,C['mouth_radius'])[1],0))
 shape(mouth,'Delight',lambda v:mouth_pose(v,.027));shape(mouth,'Concern',lambda v:mouth_pose(v,-.024))
 broad=1.16 if vendor else 1.04
 rings=[(z,w*broad,d) for z,w,d in C['shirt_rings']]
 part(loft(n+'Shirt',rings,shirt),'Spine')
 for s,b in [(-1,'L'),(1,'R')]:
  part(loft(n+'Sleeve',[(1.22,.12,.145),(1.26,.14,.155),(1.38,.145,.155),(1.44,.12,.13),(1.48,.055,.065),(1.49,.008,.012)],shirt,cx=s*.375),'Arm'+b)
  part(loft(n+'RolledCuff',[(1.218,.123,.15),(1.26,.143,.16),(1.277,.14,.158)],'Cream',cx=s*.375),'Arm'+b)
 part(tube(n+'Collar',[(.15*math.cos(i*math.tau/24),.14*math.sin(i*math.tau/24),1.56) for i in range(25)],.012,'Cream'),'Spine')
 if vendor:
  v=[];f=[]
  for z,w,d in C['apron_rings']:
   for j in range(13):
    x=(j/6-1)*w;v.append((x,-d*math.sqrt(max(.1,1-(x/(w*1.35))**2))-.012,z))
  for r in range(3):
   for j in range(12):f.append((r*13+j,r*13+j+1,(r+1)*13+j+1,(r+1)*13+j))
  o=part(mesh(n+'Apron',v,f,'Teal'),'Spine');mod=o.modifiers.new('Canvas thickness','SOLIDIFY');mod.thickness=.012
  part(box(n+'Pocket',(0,-.319,1.08),(.30,.026,.16),'Teal'),'Spine')
  for s in [-1,1]:part(tube(n+'ApronStrap',[(s*.13,-.218,1.45),(s*.15,-.15,1.56),(s*.15,.12,1.57),(s*.23,.20,1.32)],.023,'Teal'),'Spine')
  part(box(n+'Badge',(-.09,-.270,1.37),(.10,.024,.09),'Cream'),'Spine')
  for s in [-1,1]:part(tube(n+'Moustache',[(s*.012,-.247,1.793),(s*.043,-.253,1.793),(s*.064,-.228,1.78)],.014,hair))
 else:
  for z in [1.05,1.19,1.33]:part(orb(n+'Button',(0,-.256,z),(.012,.008,.012),'Cream'),'Spine')
  part(box(n+'ChestPocket',(-.17,-.225,1.34),(.12,.023,.13),shirt),'Spine')
  part(tube(n+'Wristband',[(-.52+.064*math.cos(i*math.tau/24),-.02+.067*math.sin(i*math.tau/24),.80) for i in range(25)],.017,'Ochre'),'HandL')
 rig.animation_data_create();bpy.context.scene.render.fps=C['fps'];bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=C['frames']
 for label in (['Welcome','Offer'] if vendor else ['Idle','Dance']):
  action=bpy.data.actions.new(P+n+'_'+label);rig.animation_data.action=action
  for frame in range(1,C['frames']+1):
   t=(frame-1)/(C['frames']-1)*math.tau
   for b in rig.pose.bones:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0)
   rig.pose.bones['Spine'].rotation_euler=(.02*math.sin(t),.025*math.sin(t),.025*math.sin(t))
   rig.pose.bones['Head'].rotation_euler=(.035*math.sin(t+.4),.045*math.sin(t),-.025*math.sin(t))
   if label=='Dance':
    rig.pose.bones['Spine'].rotation_euler.z=.095*math.sin(t)
    for s,b in [(-1,'L'),(1,'R')]:
     rig.pose.bones['Arm'+b].rotation_euler=(-.18+s*.08*math.sin(t),0,s*.19)
     rig.pose.bones['Forearm'+b].rotation_euler=(-.35-.10*math.sin(t+s),0,0)
   if label in ['Welcome','Offer']:
    rig.pose.bones['ArmR'].rotation_euler=(-.25,0,-.12)
    rig.pose.bones['ForearmR'].rotation_euler=(-.85-.07*math.sin(t),0,0)
    rig.pose.bones['HandR'].rotation_euler=(0,.15*math.sin(t) if label=='Welcome' else .3,0)
   for b in rig.pose.bones:
    b.keyframe_insert(data_path='rotation_euler',frame=frame)
  track=rig.animation_data.nla_tracks.new();track.name=label;strip=track.strips.new(label,1,action);track.mute=True
 rig.animation_data.action=None
 for b in rig.pose.bones:b.rotation_euler=(0,0,0)
 objs=[rig]+active
 for tr in rig.animation_data.nla_tracks:tr.mute=False
 export(n,objs,True)
 for tr in rig.animation_data.nla_tracks:tr.mute=True
 rig.animation_data.action=None
 for b in rig.pose.bones:b.rotation_euler=(0,0,0)
 for o in active:
  if o.type=='MESH' and o.data.shape_keys:
   for k in o.data.shape_keys.key_blocks:k.value=0
 rig.scale=C['vendor_scale'] if vendor else C['attendee_scale'];rig.location=C['vendor_pos'] if vendor else C['attendee_pos']
 return rig

A=character(); V=character(True)
active=[];S=CONFIG['stall'];w=S['width']/2;d=S['depth']/2
for x in [-w,w]:
 for y in [-d,d]:
  box('PostFoot',(x,y,.06),(.24,.24,.12),'Metal');box('TimberPost',(x,y,S['eave']/2),(S['post'],S['post'],S['eave']),'Wood')
 for y in [-d,d]:tube('DiagonalKnee',[(x,y,S['eave']-.46),(x-math.copysign(.43,x),y,S['eave'])],.035,'WoodLight')
for y in [-d,d]:box('EaveBeam',(0,y,S['eave']),(S['width']+.18,.13,.16),'WoodLight')
for x in [-w,w]:box('SideBeam',(x,0,S['eave']),(.13,S['depth'],.16),'Wood')
box('Ridge',(0,0,S['ridge']),(.13,S['depth']+.32,.13),'Wood')
for y in [-d,d]:
 for s in [-1,1]:tube('Rafter',[(s*(w+.25),y,S['eave']),(0,y,S['ridge'])],.045,'WoodLight')
for ix in range(S['roof_x']):
 v=[];f=[]
 for a in [ix,ix+1]:
  x=(a/S['roof_x']*2-1)*(w+.26)
  for j in range(S['roof_y']+1):
   y=(j/S['roof_y']*2-1)*(d+.23);z=S['ridge']-(S['ridge']-S['eave'])*abs(x)/(w+.26)+.035-.045*math.sin(j/S['roof_y']*math.pi);v.append((x,y,z))
 k=S['roof_y']+1
 for j in range(k-1):f.append((j,j+1,k+j+1,k+j))
 o=mesh('CanvasStripe',v,f,'Teal' if ix%4<2 else 'Cream');m=o.modifiers.new('Canvas edge','SOLIDIFY');m.thickness=.012
for y in [-d-.23,d+.23]:
 for i in range(S['roof_x']):
  xa=(i/S['roof_x']*2-1)*(w+.26);xb=((i+1)/S['roof_x']*2-1)*(w+.26)
  za=S['ridge']-(S['ridge']-S['eave'])*abs(xa)/(w+.26)+.035;zb=S['ridge']-(S['ridge']-S['eave'])*abs(xb)/(w+.26)+.035
  mesh('ScallopedValance',[(xa,y,za),(xb,y,zb),(xb,y,zb-.14),((xa+xb)/2,y,(za+zb)/2-S['valance']),(xa,y,za-.14)],[(0,1,2,3,4)],'Teal' if i%4<2 else 'Cream')
for x in [-w+.18,w-.18]:
 box('CounterLeg',(x,-d+.2,S['counter']/2),(.12,.14,S['counter']),'Wood')
box('CounterApron',(0,-d+.15,.59),(S['width']-.1,.09,.76),'Teal')
for j in range(4):box('CounterBoard',(0,-d-.12+j*.18,S['counter']),(S['width']+.18,.173,S['plank']),'WoodLight')
for x in [-w+.3,w-.3]:
 box('ShelfUpright',(x,d-.18,1.15),(.08,.08,2.3),'Wood')
for z in [.45,1.12,1.84]:box('RearShelf',(0,d-.22,z),(S['width']-.5,.45,.065),'WoodLight')
box('ShopSign',(0,-d-.09,2.54),(2.95,.10,.45),'Ochre');text('ShopName','MOONLOOP',(0,-d-.151,2.47),.28,'Ink')
text('ShopSubtitle','FLOW GOODS  /  AFTER HOURS',(0,-d-.151,2.33),.075,'Ink')
for x in [-1.28,1.28]:tube('SignHanger',[(x,-d-.08,2.76),(x,-d,2.84)],.014,'Metal')
for x in [-1.55,-1.32,-.90,-.67]:tube('PoiDisplayHook',[(x,-d+.06,2.45),(x,-1.18,2.45),(x,-1.18,2.38)],.009,'Metal')
for x in [-1.7,1.7]:
 tube('LampBracket',[(x,-d,2.72),(x,-d-.24,2.72),(x,-d-.24,2.51)],.018,'Metal');orb('Lantern',(x,-d-.24,2.40),(.10,.10,.15),'Glow');loft('LanternCap',[(2.51,.13,.13),(2.55,.045,.045)],'Metal',x,-d-.24)
for i in range(6):
 x=-1.75+i*.65
 loft('StockTin',[(1.873,.11,.11),(1.91,.12,.12),(2.08,.12,.12),(2.10,.11,.11)],'Coral' if i%2 else 'Ochre',x,d-.22)
 box('TinLabel',(x,d-.342,1.985),(.15,.012,.09),'Cream')
for i in range(4):
 x=-1.5+i*.40;box('FoldedTowel',(x,d-.22,1.20),(.34,.32,.10),'Teal' if i%2 else 'Cream')
for x in [-1.7,1.65]:
 box('DisplayTray',(x,-d+.14,1.09),(.58,.46,.06),'Ochre')
 for i in range(3):orb('DisplayPoi',(x+(i-1)*.15,-d+.12,1.18),(.063,.063,.063),'MintGlow' if x<0 else 'Coral')
box('PriceBoard',(.85,-d-.09,1.36),(.48,.06,.50),'Ink');text('Prices','POI\nPRACTICE  5\nLED  12',(.85,-d-.124,1.51),.067,'Cream')
export('Stall',active)
active=[];Q=CONFIG['poi']
for label,x,c in [('Practice',-.15,'Coral'),('LED',.15,'MintGlow')]:
 start=len(active)
 loft(label+'Handle',[(0,.022,.022),(.012,.027,.027),(Q['handle']-.012,.027,.027),(Q['handle'],.018,.018)],'Rubber',x)
 for z in [.028,.048,.068,.088]:loft(label+'GripRib',[(z,.029,.029),(z+.005,.029,.029)],'Teal',x)
 tube(label+'Tether',[(x,0,Q['handle']),(x,0,Q['length'])],Q['cord'],'Cream')
 r=Q['head'];orb(label+'Head',(x,0,Q['length']+r),(r,r,r),'Cream' if label=='Practice' else c)
 if label=='Practice':
  loft('SockNeck',[(Q['length']-.02,.012,.012),(Q['length']+.05,.05,.05)],'Coral',x)
  tube('SockSeam',[(x+r*math.sin(i*math.pi/12),0,Q['length']+r+r*math.cos(i*math.pi/12)) for i in range(13)],.003,'Coral')
 else:
  for z in [Q['length']+.01,Q['length']+.15]:loft('LEDHousingCap',[(z,.035,.035),(z+.009,.034,.034)],'Metal',x)
 objs=active[start:]
 for o in objs:o.location.x-=x
 export('Poi'+label,objs)
 for o in objs:
  o.matrix_world=Matrix.Translation(Vector((-1.55 if label=='Practice' else -.90,-1.18,2.38)))@Matrix.Rotation(math.pi,4,'Y')@o.matrix_world
  twin=o.copy();twin.data=o.data.copy();bpy.context.collection.objects.link(twin);twin.location.x+=.23
active=[];D=CONFIG['path'];v=[]
for j in range(D['segments']+1):
 y=-D['length']/2+j*D['length']/D['segments'];bend=.20*math.sin(j*.35)
 for s in [-1,1]:v.append((s*(D['width']/2+random.uniform(-.12,.12))+bend,y,-.012))
mesh('WornPath',v,[(j*2,j*2+1,j*2+3,j*2+2) for j in range(D['segments'])],'Dirt')
box('Ground',(0,0,-.12),(11,12,.20),'Ground')
for i in range(D['clumps']):
 s=random.choice([-1,1]);x=s*random.uniform(1.85,4.7);y=random.uniform(-5,5);pts=[];faces=[]
 for j in range(7):
  a=random.random()*math.tau;z=random.uniform(.13,.33);dx=random.uniform(-.14,.14);dy=random.uniform(-.14,.14);k=len(pts)
  pts.extend([(x+dx-.022,y+dy,0),(x+dx+.022,y+dy,0),(x+dx+math.cos(a)*.10,y+dy+math.sin(a)*.10,z)]);faces.append((k,k+1,k+2))
 mesh('Grass',pts,faces,'Leaf' if i%2 else 'LeafLight')
for i in range(D['stones']):
 s=random.choice([-1,1]);orb('PathStone',(s*random.uniform(1.9,4.3),random.uniform(-4,4),-.015),(random.uniform(.12,.25),random.uniform(.10,.18),random.uniform(.08,.14)),'Stone')
for x in [-2.25,2.25]:
 for y in [-3,2.8]:
  box('PathBollard',(x,y,.38),(.12,.12,.76),'Wood');box('BollardCap',(x,y,.78),(.17,.17,.055),'Metal');box('BollardLight',(x,y-.068,.65),(.075,.014,.12),'Glow')
export('Path',active)
for o in groups['Path']:o.location.y-=3.8
scene=bpy.context.scene;scene.frame_set(1);scene.render.engine='CYCLES';scene.cycles.samples=CONFIG['render']['samples'];scene.render.resolution_x=CONFIG['render']['width'];scene.render.resolution_y=CONFIG['render']['height'];scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new(P+'Dusk');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(*CONFIG['render']['world'],1)
for typ,pos,power,size in [('AREA',(1,-5,8),CONFIG['render']['area_energy'],CONFIG['render']['area_size']),('AREA',(-4,2,5),800,5)]:
 d=bpy.data.lights.new(P+'Softbox',typ);d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(P+'Softbox',d);bpy.context.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new(P+'Presentation');cam=bpy.data.objects.new(P+'Presentation',d);bpy.context.collection.objects.link(cam);cam.location=CONFIG['render']['camera'];cam.rotation_euler=(Vector(CONFIG['render']['target'])-cam.location).to_track_quat('-Z','Y').to_euler();d.lens=CONFIG['render']['lens'];scene.camera=cam
scene.view_settings.view_transform='AgX'
manifest={'package':P,'source':CONFIG['source'],'materials':{P+n:{'rgb':list(c),'roughness':CONFIG['surface']['roughness']} for n,c in CONFIG['colors'].items()},'assets':{n:{'file':P+n+'.fbx','triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objs if o.type=='MESH'),'sourceObjects':len(objs),'exportObjects':export_counts[n]} for n,objs in groups.items()},'notes':['Original incremental source adaptation; no downloaded game assets.','Blender procedural bump is preview-only; exported base colors require Unity material setup.','Native lighting, importing, animation, contact and gameplay validation delegated to Sol.','Sample clips are upper-body acting with stationary feet, not locomotion replacements.']}
(O/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(E/'palette.json').write_text(json.dumps({'entries':[{'name':P+n,'rgb':list(c),'roughness':CONFIG['surface']['roughness']} for n,c in CONFIG['colors'].items()]},indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=str(O/'FirstProductionPackage.blend'))
scene.render.filepath=str(O/'package-preview.png');bpy.ops.render.render(write_still=True)
cam.location=(3,-8,3.2);cam.rotation_euler=(Vector((-1,-.6,1.45))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=62
scene.render.filepath=str(O/'character-preview.png');bpy.ops.render.render(write_still=True)
