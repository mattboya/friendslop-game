CONFIG = {
 'prefix':'AH02_', 'out':'ArtSource/ProductionPackage02', 'exports':'Assets/Festival/Art/ProductionSample02', 'helpers':'scripts/create_first_production_package.py', 'mesh':{'sides':24,'rings':12,'bevel':.014,'bevel_segments':2,'tube_sides':8}, 'surface':{'roughness':.7,'metallic':.68,'emission':2},
 'stage':{'width':18.,'depth':9.,'top':1.4,'panels_x':9,'panels_y':5,'skin':.10,'fascia':.055,'panel_gap':.018,'tower_x':8.3,'tower_y':3.6,'height':7.1,'truss':.46,'chord':.052,'brace':.023,'tower_bays':9,'beam_bays':18,'roof_peak':.72,'roof_overhang':.45,'roof_grid':[24,16],'roof_thickness':.045,'stair_count':7,'stair_run':.34,'stair_width':1.6,'lamp_count':7,'speaker_x':9.9,'wing_width':2.8,'wing_height':4.7,'halo_radius':1.85,'halo_center':4.3,'halo_segments':64}, 'speaker':{'width':1.55,'depth':1.25,'height':3.9,'shell':.09,'cone_radius':.44,'cone_depth':.12,'drivers':3,'driver_z':[.77,1.83,2.89],'rim':.035,'screws':8}, 'case':{'size':[1.18,.70,.72],'wheel':.085,'rail':.038,'corner':.08},
 'dj':{'width':3.7,'depth':1.25,'height':.84,'skin':.045,'platter_x':1.18,'platter_radius':.33,'platter_y':.05,'platter_height':.045,'platter_steps':48,'pad_size':.10,'pad_gap':.025,'pad_rows':2,'pad_cols':4,'knob_radius':.042,'knob_height':.07,'channels':4,'channel_pitch':.19,'fader_length':.33,'fader_width':.032,'cap_size':[.105,.065,.055],'screen_size':[.37,.19,.025],'screen_recess':.025,'screen_z':.035,'cable_radius':.012,'feet_size':[.15,.15,.075]}, 'totem':{'base':[1.4,1.25,.58],'column':.27,'crest_z':2.04,'radius':.49,'depth':.15,'rays':8,'ray_tip':.70,'ray_root':.44,'ray_width':.13,'moon_cut_x':.24,'moon_cut_radius':.46,'ring_radius':.55,'ring_wire':.035,'segments':64,'plaque':[.68,.04,.20],'bolts':4}, 'render':{'size':[1600,1000],'samples':48,'world':[.07,.105,.17],'camera':[24,-34,14],'target':[0,0,3.0],'lens':45,'key':[2,-8,16],'key_energy':4200,'key_size':12,'fill':[-9,6,12],'fill_energy':2200,'fill_size':9,'ground':[34,24,.16]},
 'colors':{'Ink':(.026,.042,.055),'Teal':(.055,.29,.28),'Coral':(.72,.20,.15),'Ochre':(.83,.50,.13),'Cream':(.88,.78,.57),'Metal':(.30,.34,.36),'MetalDark':(.12,.15,.17),'Rubber':(.038,.047,.042),'Wood':(.26,.135,.066),'Deck':(.13,.17,.18),'Screen':(.015,.08,.085),'GoldGlow':(1,.57,.18),'MintGlow':(.16,.85,.63),'RoseGlow':(.92,.24,.31),'Ground':(.12,.15,.12)}}
import bpy,bmesh,math,json,ast
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parents[1];P=CONFIG['prefix'];O=R/CONFIG['out'];E=R/CONFIG['exports'];O.mkdir(parents=True,exist_ok=True);E.mkdir(parents=True,exist_ok=True)
for o in list(bpy.data.objects):
 if o.name.startswith(P):bpy.data.objects.remove(o,do_unlink=True)
for m in list(bpy.data.materials):
 if m.name.startswith(P):bpy.data.materials.remove(m,do_unlink=True)
scene=bpy.data.scenes.new(P+'Production');bpy.context.window.scene=scene;bpy.context.preferences.filepaths.save_version=0
M={};active=[];assets={}
for n,c in CONFIG['colors'].items():
 m=bpy.data.materials.new(P+n);m.use_nodes=True;m.diffuse_color=(*c,1);bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=CONFIG['surface']['roughness']
 if n.startswith('Metal'):bs.inputs['Metallic'].default_value=CONFIG['surface']['metallic'];bs.inputs['Roughness'].default_value=.36
 if n.endswith('Glow'):bs.inputs['Emission Color'].default_value=(*c,1);bs.inputs['Emission Strength'].default_value=CONFIG['surface']['emission']
 M[n]=m
source=ast.parse((R/CONFIG['helpers']).read_text());names={'own','mesh','box','orb','tube','loft','text'}
exec(compile(ast.Module(body=[n for n in source.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),CONFIG['helpers'],'exec'))

def cylinder(n,p,r,h,c,front=False):
 bpy.ops.mesh.primitive_cylinder_add(vertices=CONFIG['mesh']['sides'],radius=r,depth=h,location=p,rotation=(math.pi/2,0,0) if front else (0,0,0));o=own(bpy.context.object,n,c);return o

def ring(n,p,r,t,c,front=False):
 bpy.ops.mesh.primitive_torus_add(major_segments=CONFIG['mesh']['sides']*2,minor_segments=CONFIG['mesh']['tube_sides'],major_radius=r,minor_radius=t,location=p,rotation=(math.pi/2,0,0) if front else (0,0,0));return own(bpy.context.object,n,c)

def cut(o,tool):
 bpy.context.view_layer.objects.active=o;md=o.modifiers.new('Recess','BOOLEAN');md.operation='DIFFERENCE';md.object=tool;bpy.ops.object.modifier_apply(modifier=md.name);active.remove(tool);bpy.data.objects.remove(tool,do_unlink=True)

def empty(n,p):
 o=bpy.data.objects.new(P+n,None);bpy.context.collection.objects.link(o);o.location=p;o.empty_display_size=CONFIG['dj']['knob_height'];active.append(o);return o

def pivot(n,objs,p):
 root=empty(n,p);bpy.context.view_layer.update()
 for o in objs:
  mw=o.matrix_world.copy();o.parent=root;o.matrix_world=mw;o['moving_part']=True
 return root

def export(n):
 source=list(active);batches={};tmp=[];roots=[o for o in source if o.type=='EMPTY']
 for src in source:
  if src.type!='MESH':continue
  o=src.copy();o.data=src.data.copy();bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o
  for md in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=md.name)
  batches.setdefault((src.parent if src.get('moving_part') else None,o.data.materials[0].name),[]).append(o)
 for (parent,mat),parts in batches.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in parts:o.select_set(True)
  bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name=(parent.name if parent else P+n)+'_'+mat;tmp.append(o)
 selected=tmp+roots
 bpy.ops.object.select_all(action='DESELECT')
 for o in selected:o.select_set(True)
 bpy.context.view_layer.objects.active=selected[0]
 bpy.ops.export_scene.fbx(filepath=str(E/(P+n+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
 assets[n]={'file':P+n+'.fbx','sourceObjects':len(source),'exportMeshes':sum(o.type=='MESH' for o in selected),'triangles':sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in selected if o.type=='MESH'),'pivots':{o.name:list(o.location) for o in roots},'objects':source}
 for o in tmp:bpy.data.objects.remove(o,do_unlink=True)
 return source

def move(objs,p):
 for o in objs:
  if o.parent not in objs:o.location+=Vector(p)

def truss(n,a,b,bays):
 S=CONFIG['stage'];a=Vector(a);b=Vector(b);t=(b-a).normalized();u=t.cross(Vector((0,0,1)))
 if u.length<.01:u=t.cross(Vector((0,1,0)))
 u.normalize();v=t.cross(u).normalized();h=S['truss']/2
 corners=[u*h+v*h,-u*h+v*h,-u*h-v*h,u*h-v*h]
 for c in corners:tube(n+'Chord',[a+c,b+c],S['chord'],'Metal')
 for j in range(bays+1):
  p=a.lerp(b,j/bays)
  for k in range(4):tube(n+'Collar',[p+corners[k],p+corners[(k+1)%4]],S['brace'],'Metal')
 for j in range(bays):
  p=a.lerp(b,j/bays);q=a.lerp(b,(j+1)/bays)
  for k in range(4):
   c,d=(corners[k],corners[(k+1)%4]) if j%2 else (corners[(k+1)%4],corners[k]);tube(n+'Web',[p+c,q+d],S['brace'],'Metal')

S=CONFIG['stage'];W=S['width'];D=S['depth'];H=S['top'];h=S['skin'];active=[]
for ix in range(S['panels_x']):
 for iy in range(S['panels_y']):
  x=-W/2+(ix+.5)*W/S['panels_x'];y=-D/2+(iy+.5)*D/S['panels_y'];box('DeckPanel',(x,y,H-h/2),(W/S['panels_x']-S['panel_gap'],D/S['panels_y']-S['panel_gap'],h),'Deck')
for x in [-W/2,W/2]:box('SideFascia',(x,0,H/2),(h,D,H),'Teal')
for y in [-D/2,D/2]:box('DeckFascia',(0,y,H/2),(W,h,H),'Ink')
for i in range(S['panels_x']):
 x=-W/2+(i+.5)*W/S['panels_x'];box('FrontInset',(x,-D/2-h/2,H*.48),(W/S['panels_x']*.90,S['fascia'],H*.62),'Teal')
box('StageLip',(0,-D/2-h,H-h),(W,S['fascia'],h),'Ochre')
for x in [-S['tower_x'],S['tower_x']]:
 for y in [-S['tower_y'],S['tower_y']]:
  box('TowerFoot',(x,y,h),(S['truss']*2,S['truss']*2,h*2),'MetalDark');truss('Tower',(x,y,h*2),(x,y,S['height']),S['tower_bays'])
for y in [-S['tower_y'],S['tower_y']]:truss('Crossbeam',(-S['tower_x'],y,S['height']),(S['tower_x'],y,S['height']),S['beam_bays'])
for x in [-S['tower_x'],S['tower_x']]:truss('Sidebeam',(x,-S['tower_y'],S['height']),(x,S['tower_y'],S['height']),S['tower_bays'])
rx=W/2+S['roof_overhang'];ry=D/2+S['roof_overhang'];nx,ny=S['roof_grid']
def roofz(y):return S['height']+S['truss']/2+S['roof_peak']*(1-abs(y)/ry)
v=[(-rx+2*rx*i/nx,-ry+2*ry*j/ny,roofz(-ry+2*ry*j/ny)) for i in range(nx+1) for j in range(ny+1)]
f=[(i*(ny+1)+j,(i+1)*(ny+1)+j,(i+1)*(ny+1)+j+1,i*(ny+1)+j+1) for i in range(nx) for j in range(ny)]
o=mesh('TouringCanopy',v,f,'Teal');md=o.modifiers.new('Canvas shell','SOLIDIFY');md.thickness=S['roof_thickness']
for x in [-S['tower_x'],0,S['tower_x']]:
 for y in [-S['tower_y'],S['tower_y']]:tube('RoofSaddle',[(x,y,S['height']+S['truss']/2),(x,y,roofz(y))],S['chord'],'Metal')
for x in [-S['tower_x'],0,S['tower_x']]:tube('RoofRib',[(x,-ry,roofz(-ry)),(x,0,roofz(0)),(x,ry,roofz(ry))],S['chord'],'Metal')
tube('RoofRidge',[(-rx,0,roofz(0)),(rx,0,roofz(0))],S['chord'],'Metal')
for y in [-ry,ry]:tube('RoofEdge',[(-rx,y,roofz(y)),(rx,y,roofz(y))],S['chord'],'Metal')
for i in range(S['stair_count']):
 rise=H/S['stair_count'];run=S['stair_run'];x=W/2-S['stair_width'];y=-D/2-(S['stair_count']-i-.5)*run;z=(i+1)*rise
 box('EntryStep',(x,y,z/2),(S['stair_width'],run,z),'Deck');box('StepNosing',(x,y-run/2,z-h/4),(S['stair_width'],h/3,h/2),'Ochre')
for s in [-1,1]:
 x=W/2-S['stair_width']+s*S['stair_width']/2;y0=-D/2-S['stair_count']*S['stair_run'];y1=-D/2
 tube('Handrail',[(x,y0,.1),(x,y0,H*.70),(x,y1,H*1.70),(x,y1,H)],S['chord'],'Metal')
for s,c in [(-1,'Coral'),(1,'Teal')]:
 x=s*(W/2-S['wing_width']/2-.3);y=D/2-.35
 outline=[(x-S['wing_width']/2,y,H),(x+S['wing_width']/2,y,H),(x+S['wing_width']/2,y,H+S['wing_height']*.82),(x+s*S['wing_width']*.20,y,H+S['wing_height']),(x-S['wing_width']/2,y,H+S['wing_height']*.90)]
 o=mesh('FoldedScenicWing',outline,[(0,1,2,3,4)],c);md=o.modifiers.new('Panel thickness','SOLIDIFY');md.thickness=h
 for z in [H+S['wing_height']*.2,H+S['wing_height']*.4,H+S['wing_height']*.6]:
  box('WingLightSocket',(x,y-h,z),(S['wing_width']*.7,h*2,h*2),'Ink');box('WingLightLens',(x,y-h*2,z),(S['wing_width']*.6,h/2,h),'GoldGlow' if s<0 else 'MintGlow')
 for dx in [-S['wing_width']*.35,S['wing_width']*.35]:box('WingFoot',(x+dx,y,H+h/2),(h*3,h*5,h),'MetalDark')
for r,c in [(S['halo_radius'],'Metal'),(S['halo_radius']*.91,'GoldGlow')]:
 pts=[(r*math.cos(i*math.tau/S['halo_segments']),D/2-.45,S['halo_center']+r*math.sin(i*math.tau/S['halo_segments'])) for i in range(S['halo_segments']+1)];tube('BacklineHalo',pts,S['chord'],c)
for x in [-S['halo_radius']*.65,S['halo_radius']*.65]:
 tube('HaloUpright',[(x,D/2-.45,H),(x,D/2-.45,S['halo_center']-S['halo_radius']*.75)],S['chord'],'Metal');box('HaloFoot',(x,D/2-.45,H+h/2),(h*4,h*6,h),'MetalDark')
box('StageNamePanel',(0,-S['tower_y']-.3,S['height']-.2),(7,.12,.75),'Ink');text('StageName','AFTER HOURS',(0,-S['tower_y']-.37,S['height']-.37),.55,'Cream')
for i in range(S['lamp_count']):
 x=(i/(S['lamp_count']-1)*2-1)*S['tower_x']*.87;y=-S['tower_y'];z=S['height']-.64
 tube('LampYoke',[(x-.19,y,z),(x-.19,y,z+.3),(x+.19,y,z+.3),(x+.19,y,z)],S['brace'],'MetalDark')
 cylinder('WashBody',(x,y,z),.18,.32,'Ink',True);cylinder('WashLens',(x,y-.17,z),.145,.025,'MintGlow' if i%2 else 'GoldGlow',True)
export('Stage')

active=[];B=CONFIG['speaker'];bw=B['width'];bd=B['depth'];bh=B['height'];t=B['shell']
box('CabinetBack',(0,bd/2-t/2,bh/2),(bw,t,bh),'Ink')
for s in [-1,1]:box('CabinetSide',(s*(bw/2-t/2),0,bh/2),(t,bd,bh),'Ink')
for z in [t/2,bh-t/2]:box('CabinetCap',(0,0,z),(bw,bd,t),'Ink')
front=box('Baffle',(0,-bd/2+t/2,bh/2),(bw-t*2,t,bh-t*2),'MetalDark')
for z in B['driver_z']:
 tool=cylinder('Cutter',(0,-bd/2,z),B['cone_radius'],t*4,'Ink',True);cut(front,tool)
 ring('DriverSurround',(0,-bd/2,z),B['cone_radius'],B['rim'],'Rubber',True)
 rings=[(B['cone_radius'],-bd/2),(B['cone_radius']*.78,-bd/2+B['cone_depth']*.6),(B['cone_radius']*.32,-bd/2+B['cone_depth'])];v=[];f=[];k=CONFIG['mesh']['sides']
 for r,y in rings:
  for j in range(k):a=j*math.tau/k;v.append((r*math.cos(a),y,z+r*math.sin(a)))
 for a in range(len(rings)-1):
  for j in range(k):f.append((a*k+j,a*k+(j+1)%k,(a+1)*k+(j+1)%k,(a+1)*k+j))
 mesh('RecessedSpeakerCone',v,f,'Rubber');orb('DustCap',(0,-bd/2+B['cone_depth'],z),(B['cone_radius']*.32,B['cone_depth']*.55,B['cone_radius']*.32),'Ink')
 for j in range(B['screws']):
  a=j*math.tau/B['screws'];cylinder('DriverScrew',(B['cone_radius']*1.11*math.cos(a),-bd/2-t*.1,z+B['cone_radius']*1.11*math.sin(a)),t*.13,t*.13,'Metal',True)
for x in [-bw/2+t/2,bw/2-t/2]:box('CabinetRail',(x,-bd/2-t/4,bh/2),(t*.5,t*.5,bh),'Metal')
for s in [-1,1]:
 tube('CarryHandle',[(s*bw/2,-bd*.18,bh*.55),(s*(bw/2+t),-bd*.18,bh*.55),(s*(bw/2+t),bd*.18,bh*.55),(s*bw/2,bd*.18,bh*.55)],B['rim'],'Metal')
export('SpeakerStack');move(active,(-S['speaker_x'],0,0))
for o in list(active):
 twin=o.copy();twin.data=o.data.copy();bpy.context.collection.objects.link(twin);twin.location.x+=S['speaker_x']*2

active=[];J=CONFIG['dj'];w=J['width'];d=J['depth'];z=J['height'];t=J['skin']
box('ConsoleCase',(0,0,(z-t+J['feet_size'][2])/2),(w,d,z-t-J['feet_size'][2]),'Ink');plate=box('TopPanel',(0,0,z-t/2),(w-t*2,d-t*2,t),'MetalDark')
for x in [-w/2+J['feet_size'][0],w/2-J['feet_size'][0]]:
 for y in [-d/2+J['feet_size'][1],d/2-J['feet_size'][1]]:box('ConsoleFoot',(x,y,J['feet_size'][2]/2),J['feet_size'],'Rubber')
for s in [-1,1]:
 x=s*J['platter_x'];start=len(active);cylinder('PlatterMotor',(x,J['platter_y'],z),J['platter_radius']*1.02,t,'Metal');cylinder('Platter',(x,J['platter_y'],z+J['platter_height']/2),J['platter_radius'],J['platter_height'],'Rubber');ring('PlatterRim',(x,J['platter_y'],z+J['platter_height']),J['platter_radius']*.94,t*.25,'Metal');cylinder('PlatterLabel',(x,J['platter_y'],z+J['platter_height']+t*.1),J['platter_radius']*.31,t*.15,'Coral' if s<0 else 'Teal')
 for j in range(J['platter_steps']):
  a=j*math.tau/J['platter_steps'];cylinder('PlatterIndex',(x+J['platter_radius']*.83*math.cos(a),J['platter_y']+J['platter_radius']*.83*math.sin(a),z+J['platter_height']),t*.08,t*.12,'Cream')
 pivot('PlatterLeft' if s<0 else 'PlatterRight',active[start:],(x,J['platter_y'],z))
 sw,sd,sh=J['screen_size'];sy=d*.40;tool=box('ScreenCutter',(x,sy,z),(sw,sd,J['screen_recess']*2),'Ink',False);cut(plate,tool)
 box('RecessedScreen',(x,sy,z-J['screen_recess']/2),(sw*.95,sd*.94,sh*.35),'Screen')
 for j in range(12):
  xx=x-sw*.43+j*sw*.078;yy=sy+math.sin(j*1.7)*sd*.14
  box('Waveform',(xx,yy,z-J['screen_recess']/4),(sw*.045,sd*(.2+.3*abs(math.sin(j))),sh*.12),'MintGlow')
 for row in range(J['pad_rows']):
  for col in range(J['pad_cols']):
   px=x+(col-1.5)*(J['pad_size']+J['pad_gap']);py=-d*.30-row*(J['pad_size']+J['pad_gap']);start=len(active)
   box('PerformancePad',(px,py,z+t/2),(J['pad_size'],J['pad_size'],t),'Coral' if s<0 else 'Teal');pivot(('Left' if s<0 else 'Right')+'Pad'+str(row*J['pad_cols']+col),active[start:],(px,py,z))
for i in range(J['channels']):
 x=(i-(J['channels']-1)/2)*J['channel_pitch'];tool=box('FaderCutter',(x,-.14,z),(J['fader_width'],J['fader_length'],t*3),'Ink',False);cut(plate,tool)
 box('FaderWell',(x,-.14,z-t),(J['fader_width']*.95,J['fader_length'],t/3),'Ink')
 start=len(active);cap=box('FaderCap',(x,-.14+(i-1.5)*J['fader_length']*.12,z+J['cap_size'][2]/2),J['cap_size'],'Cream');pivot('ChannelFader'+str(i),active[start:],tuple(cap.location))
 for row in range(3):
  y=.1+row*J['knob_radius']*2.6;start=len(active);cylinder('EQKnob',(x,y,z+J['knob_height']/2),J['knob_radius'],J['knob_height'],'Ochre' if row==0 else 'Teal');box('KnobIndex',(x,y-J['knob_radius']*.5,z+J['knob_height']),(J['knob_radius']*.18,J['knob_radius']*.75,t*.12),'Cream');pivot('EQ_'+str(i)+'_'+str(row),active[start:],(x,y,z))
 box('ChannelMeter',(x+J['channel_pitch']*.35,0,z+t*.1),(t*.25,J['fader_length']*.55,t*.15),'MintGlow')
text('ConsoleBrand','MOONLOOP / TWO DECK',(0,-d/2-t*.1,z*.45),.13,'Cream')
for s in [-1,1]:
 x=s*w*.33;y=d/2;tube('AudioCable',[(x,y,z*.3),(x,y+.12,z*.3),(x+s*.16,y+.3,z*.14),(x+s*.34,y+.3,t*.4)],J['cable_radius'],'Rubber')
empty('LeftHandContact',(-J['platter_x'],J['platter_y'],z+J['platter_height']));empty('RightHandContact',(J['channel_pitch']*.5,-.14,z+J['cap_size'][2]));empty('PerformerFeet',(0,d/2+.46,0))
export('DJStation');move(active,(0,.1,H))

active=[];K=CONFIG['case'];w,d,z=K['size'];r=K['rail'];wh=K['wheel'];base=wh*2
for x in [-w*.35,w*.35]:
 for y in [-d*.33,d*.33]:cylinder('Caster',(x,y,wh),wh,wh,'Rubber',True);box('CasterFork',(x,y,wh*1.6),(wh*.6,wh*.8,wh),'Metal')
box('FlightCase',(0,0,base+z/2),(w,d,z),'Teal')
for x in [-w/2,w/2]:
 for y in [-d/2,d/2]:box('CaseCorner',(x,y,base+z/2),(r,r,z),'Metal')
for zz in [base,base+z*.72,base+z]:
 for y in [-d/2,d/2]:box('CaseRail',(0,y,zz),(w,r,r),'Metal')
 for x in [-w/2,w/2]:box('CaseRail',(x,0,zz),(r,d,r),'Metal')
for x in [-w*.28,w*.28]:box('ButterflyLatch',(x,-d/2-r,base+z*.72),(r*2,r,r*3),'Ochre')
tube('RecessedCarryHandle',[(-w*.14,-d/2-r,base+z*.4),(-w*.14,-d/2-r*2,base+z*.5),(w*.14,-d/2-r*2,base+z*.5),(w*.14,-d/2-r,base+z*.4)],r*.36,'Metal')
text('CaseStencil','FLOW CREW',(0,-d/2-r,base+z*.23),z*.10,'Cream');export('FlightCase');move(active,(-W*.31,.3,H))

T=CONFIG['totem']
for sun in [True,False]:
 active=[];label='SunTotem' if sun else 'MoonTotem';c='Ochre' if sun else 'Teal';g='GoldGlow' if sun else 'MintGlow';bw,bd,bh=T['base'];cz=T['crest_z'];r=T['radius'];depth=T['depth']
 box(label+'Base',(0,0,bh/2),T['base'],'Wood');box('PlinthCap',(0,0,bh),(bw*1.04,bd*1.04,depth*.30),'MetalDark')
 lo=bh+depth*.15;hi=cz-T['ring_radius'];loft('TaperedColumn',[(lo,T['column']*1.2,T['column']*1.2),(hi,T['column']*.75,T['column']*.75)],c)
 cylinder('ColumnCap',(0,0,hi),T['column']*.86,depth*.25,'Metal')
 ring('CrestRim',(0,0,cz),T['ring_radius'],T['ring_wire'],'Metal',True)
 if sun:
  cylinder('SunDisc',(0,0,cz),r,depth,c,True)
  for i in range(T['rays']):
   a=i*math.tau/T['rays'];u=Vector((math.sin(a),0,math.cos(a)));v=Vector((math.cos(a),0,-math.sin(a)));pts=[]
   for y in [-depth*.45,depth*.45]:
    for p in [u*T['ray_root']-v*T['ray_width']/2,u*T['ray_tip'],u*T['ray_root']+v*T['ray_width']/2]:pts.append((p.x,y,cz+p.z))
   mesh('SunRay',pts,[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)],c)
  ring('SunInset',(0,-depth*.52,cz),r*.76,T['ring_wire']*.55,g,True)
 else:
  moon=cylinder('MoonCrescent',(0,0,cz),r,depth,c,True);tool=cylinder('MoonCutter',(T['moon_cut_x'],0,cz+.055),T['moon_cut_radius'],depth*3,'Ink',True);cut(moon,tool)
  pts=[(r*.85*math.cos(math.pi*.30+i*math.pi*1.40/T['segments']),-depth*.53,cz+r*.85*math.sin(math.pi*.30+i*math.pi*1.40/T['segments'])) for i in range(T['segments']+1)];tube('MoonInset',pts,T['ring_wire']*.55,g)
  for a in [math.pi*.68,math.pi*1.32]:tube('CrescentMount',[(T['ring_radius']*math.cos(a),0,cz+T['ring_radius']*math.sin(a)),(r*.85*math.cos(a),0,cz+r*.85*math.sin(a))],T['ring_wire'],'Metal')
 pw,pd,ph=T['plaque'];box('CluePlaque',(0,-bd/2-pd/2,bh*.5),T['plaque'],c);text('ClueName','SUN' if sun else 'MOON',(0,-bd/2-pd,bh*.5-ph*.25),ph*.58,'Cream')
 for x in [-bw*.35,bw*.35]:
  for y in [-bd*.35,bd*.35]:cylinder('PlinthBolt',(x,y,bh+depth*.33),T['ring_wire']*.55,depth*.07,'Metal')
 empty('PrivateClueAnchor',(0,0,cz));empty('InteractAnchor',(0,-bd/2,bh));export(label);move(active,(-W*.37 if sun else W*.37,-D/2-2.8,0))

active=[];G=CONFIG['render'];box('PreviewGround',(0,0,-G['ground'][2]/2),G['ground'],'Ground')
scene.render.engine='CYCLES';scene.cycles.samples=G['samples'];scene.render.resolution_x,scene.render.resolution_y=G['size'];scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new(P+'Dusk');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(*G['world'],1)
for n,pos,power,size in [('Key',G['key'],G['key_energy'],G['key_size']),('Fill',G['fill'],G['fill_energy'],G['fill_size'])]:
 data=bpy.data.lights.new(P+n,'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(P+n,data);bpy.context.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector(G['target'])-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new(P+'Camera');cam=bpy.data.objects.new(P+'Camera',data);bpy.context.collection.objects.link(cam);cam.location=G['camera'];cam.rotation_euler=(Vector(G['target'])-cam.location).to_track_quat('-Z','Y').to_euler();data.lens=G['lens'];scene.camera=cam;scene.view_settings.view_transform='AgX'
manifest={'package':P,'units':'metres','front':'Blender -Y / Unity +Z','assets':{n:{k:v for k,v in a.items() if k!='objects'} for n,a in assets.items()},'source':'Original procedural geometry. Reuses only named geometry helper functions from the first package.','status':'Authored, Unity/native testing deferred to Sol.'}
(O/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');(E/'palette.json').write_text(json.dumps({'entries':[{'name':P+n,'rgb':list(c),'roughness':.36 if n.startswith('Metal') else CONFIG['surface']['roughness'],'metallic':CONFIG['surface']['metallic'] if n.startswith('Metal') else 0,'emission':CONFIG['surface']['emission'] if n.endswith('Glow') else 0} for n,c in CONFIG['colors'].items()]},indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=str(O/'SecondProductionPackage.blend'))
scene.render.filepath=str(O/'package-preview.png');bpy.ops.render.render(write_still=True)
cam.location=(4,-5,5);cam.rotation_euler=(Vector((0,.1,H+J['height']*.6))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=58;scene.render.filepath=str(O/'dj-preview.png');bpy.ops.render.render(write_still=True)
