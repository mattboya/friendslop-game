CONFIG = {
 'prefix':'AH03_', 'source':'ArtSource/Generated/FestivalWorld.blend','gear':'ArtSource/Generated/FestivalGear.blend','out':'ArtSource/ProductionPackage03','exports':'Assets/Festival/Art/ProductionSample03','mesh':{'sides':20,'rings':10,'bevel':.012,'bevel_segments':2,'tube_sides':8},'dj':{'knob_height':.07},
 'layout':{'columns':5,'pitch':12,'source_pitch':25,'source_columns':4},'sign':{'size':[1.4,.065,.36],'text':.16,'post':.07,'height':2.25},'bench':{'length':2.1,'depth':.62,'seat':.49,'slat':.055,'leg':.065,'back':1.0},'barrier':{'width':2.4,'height':1.12,'tube':.035,'feet':[.40,.65,.08],'bars':9},'bin':{'radius':.30,'height':.85,'rim':.035,'lid':.12},'light':{'height':3.2,'span':5.,'sag':.36,'radius':.025,'bulbs':9,'bulb':.07},'stash':{'size':[1.5,1.15,1.0],'rail':.07,'slats':5,'gap':.012},'spoon':{'length':.22,'bowl_width':.045,'bowl_length':.065,'bowl_depth':.015,'handle_width':.012,'cord_radius':.003,'cord_loop':.08},'bottle':{'height':.28,'radius':.07,'neck':.025,'cap':.024},'wrist':{'radius':.065,'width':.028,'thickness':.009,'plate':[.075,.026,.065]},'medical':{'sign':[0,2.56,2.99],'sign_size':[1.8,.08,.32],'curtain_x':.05,'curtain_y':-.4,'curtain_top':2.45,'curtain_bottom':.6,'curtain_width':1.7,'curtain_steps':20},'camp':{'rib_spacing':.23,'rib_count':7,'rib_width':.035,'rack_height':2.04},
 'colors':{'Dark':(.026,.042,.055),'Wood':(.34,.20,.10),'Mint':(.055,.36,.32),'Rose':(.75,.22,.18),'Gold':(.84,.52,.16),'Cream':(.88,.78,.57),'Blue':(.16,.25,.36),'Metal':(.28,.33,.35),'Leaf':(.10,.26,.14),'LeafWarm':(.25,.37,.11),'Glass':(.17,.38,.40),'White':(.92,.9,.79),'Bark':(.27,.16,.09),'PaintRose':(.72,.20,.15),'PaintMint':(.055,.29,.28),'PaintGold':(.83,.50,.13),'PaintCream':(.82,.76,.59),'AutoGlass':(.07,.15,.19),'CanvasRose':(.72,.20,.15),'CanvasGold':(.83,.50,.13),'CanvasMint':(.055,.29,.28),'CanvasCream':(.88,.78,.57),'CanvasDark':(.07,.12,.15),'Rubber':(.035,.042,.037),'Needle':(.075,.19,.13),'Stone':(.35,.37,.28),'StageGlowGold':(1,.57,.18),'StageGlowMint':(.16,.85,.63)},
 'surface':{'roughness':.72,'metallic':.55,'emission':1.5},'render':{'size':[1600,1100],'samples':32,'world':[.11,.15,.21],'camera':[29,-39,30],'target':[0,0,1.3],'lens':44,'key':[0,-12,25],'energy':7000,'size_light':20,'ground':[70,70,.12]}}
import bpy,bmesh,math,json,ast
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parents[1];P=CONFIG['prefix'];O=R/CONFIG['out'];E=R/CONFIG['exports'];O.mkdir(parents=True,exist_ok=True);E.mkdir(parents=True,exist_ok=True)
for o in list(bpy.data.objects):
 if o.name.startswith(P):bpy.data.objects.remove(o,do_unlink=True)
for m in list(bpy.data.materials):
 if m.name.startswith(P):bpy.data.materials.remove(m,do_unlink=True)
scene=bpy.data.scenes.new(P+'World');bpy.context.window.scene=scene;bpy.context.preferences.filepaths.save_version=0
M={};active=[];assets={};placements={}
for n,c in CONFIG['colors'].items():
 m=bpy.data.materials.new(P+n);m.diffuse_color=(*c,1);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=CONFIG['surface']['roughness']
 if n=='Metal':bs.inputs['Metallic'].default_value=CONFIG['surface']['metallic']
 if 'Glow' in n:bs.inputs['Emission Color'].default_value=(*c,1);bs.inputs['Emission Strength'].default_value=CONFIG['surface']['emission']
 M[n]=m
for f,names in [('create_first_production_package.py',{'own','mesh','box','orb','tube','loft','text'}),('create_second_production_package.py',{'cylinder','ring','empty','pivot','export'})]:
 tree=ast.parse((R/'scripts'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),f,'exec'))
world_order=list(json.loads((R/'ArtSource/world-manifest.json').read_text())['models']);gear_names=['Confetti','MerchBag','Map','StagePass','Stock','Voucher','Stash'];loaded={}
for path,isgear in [(CONFIG['source'],False),(CONFIG['gear'],True)]:
 with bpy.data.libraries.load(str(R/path),link=False) as (a,b):b.objects=list(a.objects)
 for o in b.objects:
  name=o.get('kit_asset','').removeprefix('Festival')
  if not name or (name in gear_names)!=isgear:continue
  bpy.context.collection.objects.link(o);idx=gear_names.index(name) if isgear else world_order.index('Festival'+name);L=CONFIG['layout'];o.location-=Vector(((idx%L['source_columns'])*L['source_pitch'],-(idx//L['source_columns'])*L['source_pitch'],0));o.name=P+name+'_'+o.name
  for i,mat in enumerate(o.data.materials):
   key=mat.name.removeprefix('Festival').split('.')[0] if mat else o.name.split('__')[-1].split('.')[0];o.data.materials[i]=M[key]
  loaded.setdefault(name,[]).append(o);o.hide_render=True;o.hide_set(True)

def label(t,p,size,c='Cream',back=False):
 o=text('Lettering',t,p,size,c)
 if back:o.rotation_euler.z=math.pi
 return o

def finish(n,front=False):
 bpy.context.view_layer.update()
 if front:
  for o in active:o.matrix_world=Matrix.Rotation(math.pi,4,'Z')@o.matrix_world
 for o in active:o.hide_set(False);o.hide_render=False
 export(n);i=len(assets)-1;L=CONFIG['layout'];pos=((i%L['columns']-(L['columns']-1)/2)*L['pitch'],-(i//L['columns'])*L['pitch'],0);placements[n]=pos
 for o in active:
  if o.parent not in active:o.location+=Vector(pos)

names=['Medical','Security','Shuttle','StallSupplies','StallStock','CampShop','Tent','DomeTent','CampCar','CampVan','CampShade','PortaPotty','TreeA','TreeB','TreeFir','GroveDetail','LittleSpoon']+gear_names
for n in names:
 active=loaded[n];bpy.ops.object.select_all(action='DESELECT')
 if n=='Medical':
  C=CONFIG['medical'];box('ClinicSign',C['sign'],C['sign_size'],'Mint');label('RESET / FIRST AID',(C['sign'][0],C['sign'][1]+C['sign_size'][1]/2,C['sign'][2]-.05),.14,back=True)
  x=C['curtain_x'];y=C['curtain_y'];w=C['curtain_width'];top=C['curtain_top'];bottom=C['curtain_bottom'];v=[];f=[]
  for z in [bottom,top]:
   for i in range(C['curtain_steps']+1):v.append((x+.045*math.sin(i*math.tau/4),y-w/2+i*w/C['curtain_steps'],z))
  for i in range(C['curtain_steps']):f.append((i,i+1,i+C['curtain_steps']+2,i+C['curtain_steps']+1))
  o=mesh('PrivacyCurtain',v,f,'CanvasMint');md=o.modifiers.new('Canvas','SOLIDIFY');md.thickness=.012
  tube('CurtainRail',[(x,y-w/2,top+.06),(x,y+w/2,top+.06)],.025,'Metal')
  for yy in [y-w/2,y+w/2]:tube('CurtainUpright',[(x,yy,.24),(x,yy,top+.06)],.025,'Metal');box('CurtainFoot',(x,yy,.27),(.5,.12,.06),'Metal')
 elif n=='Security':
  label('SECURITY',(0,2.60,2.94),.22,'Dark',True);box('RadioDock',(1.93,.72,1.34),(.32,.24,.17),'Dark');tube('RadioAntenna',[(1.93,.72,1.4),(1.93,.72,1.76)],.009,'Dark');label('COOL DOWN',(0,-.295,2.51),.20,'Gold',True)
 elif n=='Shuttle':
  label('LAST SHUTTLE',(0,-1.60,2.57),.20,'Cream');label('NO FRIEND LEFT BEHIND',(-2.64,-1.68,.97),.095,'Dark')
  for x in [-1.31,1.31]:
   start=len(active);box('FoldingDoorFrame',(x,-1.61,1.42),(.13,.10,2.05),'Metal');box('FoldingDoorLeaf',(x*.95,-1.80,1.42),(.10,.39,2.05),'PaintGold');pivot('DoorLeft' if x<0 else 'DoorRight',active[start:],(x,-1.61,.4))
  tube('CeilingGrabRail',[(-3.0,0,2.50),(3.,0,2.50)],.035,'Metal')
  for x in [-2.4,-1.2,0,1.2,2.4]:tube('HangingStrap',[(x,0,2.50),(x,0,2.17),(x+.15,0,2.08),(x+.30,0,2.17),(x+.30,0,2.50)],.018,'Gold')
 elif n in ['StallSupplies','StallStock']:
  label('ODDS & ENDS' if n=='StallSupplies' else 'GOOD TIMES SUPPLY',(0,1.68,2.61),.2,'Cream',True)
  for x in [-2.25,2.25]:tube('AwningFrontSupport',[(x,0,2.7),(x,1.5,3.06)],.045,'Wood')
  box('BackShelf',(0,-1.20,1.9),(4.25,.4,.08),'Wood')
 elif n=='CampShop':
  box('ShopSign',(0,1.10,2.46),(2.7,.08,.40),'Dark');label('BEFORE YOU GO',(0,1.15,2.36),.22,'Cream',True)
 elif n=='PortaPotty':
  for i in range(CONFIG['camp']['rib_count']):
   x=(i-(CONFIG['camp']['rib_count']-1)/2)*CONFIG['camp']['rib_spacing'];box('MouldedDoorRib',(x,1.17,1.28),(CONFIG['camp']['rib_width'],.028,1.35),'Mint')
  box('OccupancyPlate',(0,1.18,1.95),(.60,.035,.19),'Cream');label('OCCUPIED',(0,1.21,1.90),.095,'Dark',True)
 elif n=='Confetti':
  ring('MuzzleLip',(0,0,.685),.17,.025,'Metal');cylinder('MuzzleInset',(0,0,.681),.135,.012,'Dark');label('PARTY',(0,-.171,.39),.075,'Cream')
 elif n=='Stash':
  for o in list(active):bpy.data.objects.remove(o,do_unlink=True)
  active=[];K=CONFIG['stash'];w,d,h=K['size'];r=K['rail'];t=(h-r*2)/K['slats'];box('CrateFloor',(0,0,r/2),(w,d,r),'Wood')
  for x in [-w/2+r/2,w/2-r/2]:
   for y in [-d/2+r/2,d/2-r/2]:box('CornerPost',(x,y,h/2),(r,r,h),'Metal')
  for i in range(K['slats']):
   z=r+(i+.5)*t
   for y in [-d/2+r/2,d/2-r/2]:box('FrontBackSlat',(0,y,z),(w-r*2,r,t-K['gap']),'Wood')
   for x in [-w/2+r/2,w/2-r/2]:box('SideSlat',(x,0,z),(r,d-r*2,t-K['gap']),'Wood')
  start=len(active)
  for i in range(K['slats']):box('LidPlank',((i+.5)*w/K['slats']-w/2,0,h),(w/K['slats']-K['gap'],d,r),'Wood')
  pivot('StashLid',active[start:],(0,d/2,h));box('Latch',(0,-d/2-r/2,h-r),(.13,r,.20),'Gold');label('SHARED STASH',(0,-d/2-.005,h*.52),.10,'Cream')
 elif n=='LittleSpoon':
  for o in list(active):bpy.data.objects.remove(o,do_unlink=True)
  active=[];K=CONFIG['spoon'];h=K['length'];w=K['bowl_width'];l=K['bowl_length'];dep=K['bowl_depth'];verts=[(0,dep,l/2)];faces=[];steps=CONFIG['mesh']['sides']
  for i in range(steps):a=i*math.tau/steps;verts.append((w/2*math.cos(a),0,l/2+l/2*math.sin(a)))
  for i in range(steps):faces.append((0,i+1,(i+1)%steps+1))
  bowl=mesh('ConcaveSpoonBowl',verts,faces,'Metal');md=bowl.modifiers.new('BowlWall','SOLIDIFY');md.thickness=K['cord_radius'];box('SpoonHandle',(0,0,(h+l)/2),(K['handle_width'],K['cord_radius'],h-l),'Gold');tube('SpoonCord',[(K['cord_loop']*math.sin(i*math.tau/32),0,h+K['cord_loop']*(1-math.cos(i*math.tau/32))) for i in range(33)],K['cord_radius'],'Dark')
 elif n in ['TreeA','TreeB','TreeFir']:
  for i in range(5):
   a=i*math.tau/5;tube('RootFlare',[(0,0,.32),(.34*math.cos(a),.34*math.sin(a),.1),(.66*math.cos(a),.66*math.sin(a),.035)],.07,'Bark')
 finish(n,n in ['Medical','Security','StallSupplies','StallStock','CampShop','PortaPotty'])

active=[];B=CONFIG['bench'];w=B['length'];d=B['depth'];h=B['seat'];t=B['slat'];r=B['leg']
for x in [-w*.37,w*.37]:
 tube('BenchFrame',[(x,-d*.43,0),(x,-d*.43,h-t/2),(x,d*.43,h-t/2),(x,d*.43,0)],r,'Metal');tube('BackStay',[(x,d*.43,h-t/2),(x,d*.50,B['back'])],r,'Metal')
for i in range(4):box('SeatSlat',(0,(i-1.5)*d/4,h),(w,d/4*.90,t),'Wood')
for z in [h+(B['back']-h)*.45,B['back']-t]:box('BackSlat',(0,d*.50,z),(w,t,(B['back']-h)*.35),'Wood')
finish('Bench')
active=[];B=CONFIG['barrier'];w=B['width'];h=B['height'];r=B['tube']
for x in [-w/2,w/2]:box('BarrierFoot',(x,0,B['feet'][2]/2),B['feet'],'Rubber')
tube('ContinuousBarrierFrame',[(-w/2,0,B['feet'][2]),(-w/2,0,h-r),(-w/2+r,0,h),(w/2-r,0,h),(w/2,0,h-r),(w/2,0,B['feet'][2])],r,'Metal');tube('LowerRail',[(-w/2,0,h*.2),(w/2,0,h*.2)],r,'Metal')
for i in range(B['bars']):x=-w/2+(i+1)*w/(B['bars']+1);tube('BarrierBar',[(x,0,h*.2),(x,0,h)],r*.55,'Metal')
finish('CrowdBarrier')
active=[];B=CONFIG['bin'];r=B['radius'];h=B['height'];t=B['rim'];loft('BinShell',[(0,r*.87,r*.87),(h,r,r),(h,r-t,r-t),(t,r*.87-t,r*.87-t)],'Mint');cylinder('BinBottom',(0,0,t/2),r*.87,t,'Mint');ring('BinRim',(0,0,h),r-t/2,t/2,'Metal');label('CANS',(0,-r-.008,h*.6),h*.13,'Cream');finish('RecyclingBin')
active=[];S=CONFIG['sign'];h=S['height'];w,d,t=S['size'];box('SignPost',(0,0,h/2),(S['post'],S['post'],h),'Wood');box('PostFoot',(0,0,.04),(.32,.32,.08),'Metal')
for i,(name,c) in enumerate([('STAGE','Rose'),('FIRST AID','Mint'),('SHUTTLE','Gold')]):
 z=h-i*t*1.2;mesh('Arrow',[(-w/2,-d/2,z-t/2),(w*.35,-d/2,z-t/2),(w/2,-d/2,z),(w*.35,-d/2,z+t/2),(-w/2,-d/2,z+t/2)],[(0,1,2,3,4)],c);label(name,(0,-d/2-.005,z-S['text']*.35),S['text'],'Cream')
finish('WayfindingPost')
active=[];L=CONFIG['light'];h=L['height'];w=L['span'];r=L['radius'];points=[(-w/2+i*w/32,0,h-L['sag']*math.sin(i*math.pi/32)) for i in range(33)];tube('ContinuousLightCable',points,r*.30,'Dark')
for x in [-w/2,w/2]:tube('StringPole',[(x,0,0),(x,0,h)],r*2,'Wood');box('PoleFoot',(x,0,.05),(.4,.4,.10),'Metal')
for i in range(L['bulbs']):
 f=(i+.5)/L['bulbs'];x=-w/2+w*f;z=h-L['sag']*math.sin(f*math.pi);tube('LampDrop',[(x,0,z),(x,0,z-L['bulb'])],r*.5,'Dark');orb('WarmBulb',(x,0,z-L['bulb']*1.8),(L['bulb'],L['bulb'],L['bulb']*1.2),'StageGlowGold')
finish('StringLights')
active=[];B=CONFIG['bottle'];r=B['radius'];h=B['height'];n=B['neck'];loft('BottleBody',[(0,r*.8,r*.8),(.02,r,r),(h*.68,r,r),(h*.83,n,n),(h,n,n)],'Mint');cylinder('BottleBase',(0,0,.006),r*.8,.012,'Mint');cylinder('Cap',(0,0,h+B['cap']/2),n*1.12,B['cap'],'Cream');label('WATER',(0,-r-.003,h*.4),h*.12,'Cream');finish('WaterBottle')
active=[];B=CONFIG['wrist'];r=B['radius'];t=B['thickness'];w=B['width'];loft('RescueBand',[(-w/2,r,r),(w/2,r,r),(w/2,r-t,r-t),(-w/2,r-t,r-t),(-w/2,r,r)],'Rose');box('RecoveryPlate',(0,-r,0),B['plate'],'Gold');box('RecoveryMarkH',(0,-r-B['plate'][1]/2,0),(B['plate'][0]*.65,.003,B['plate'][2]*.16),'StageGlowMint');box('RecoveryMarkV',(0,-r-B['plate'][1]/2,0),(B['plate'][0]*.16,.003,B['plate'][2]*.65),'StageGlowMint');empty('WristSocket',(0,0,0));finish('RecoveryWristband')
active=[];B=CONFIG['sign'];box('StopBase',(0,0,.08),(.7,.7,.16),'Metal');tube('StopPole',[(0,0,.16),(0,0,B['height'])],B['post'],'Metal');box('ShuttleStopSign',(0,0,B['height']),[B['size'][0],B['size'][1],B['size'][2]*2],'Gold');label('LAST SHUTTLE',(0,-B['size'][1]/2-.003,B['height']),B['text']*.80,'Dark');label('WAIT HERE',(0,-B['size'][1]/2-.003,B['height']-B['text']*1.5),B['text']*.65,'Dark');finish('ShuttleStop')
active=[];G=CONFIG['render'];scene.render.engine='CYCLES';scene.cycles.samples=G['samples'];scene.render.resolution_x,scene.render.resolution_y=G['size'];scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new(P+'WorldLight');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(*G['world'],1)
ld=bpy.data.lights.new(P+'Softbox','AREA');ld.energy=G['energy'];ld.shape='DISK';ld.size=G['size_light'];light=bpy.data.objects.new(P+'Softbox',ld);bpy.context.collection.objects.link(light);light.location=G['key'];light.rotation_euler=(Vector(G['target'])-light.location).to_track_quat('-Z','Y').to_euler()
ld=bpy.data.lights.new(P+'Sun','SUN');ld.energy=1.7;light=bpy.data.objects.new(P+'Sun',ld);bpy.context.collection.objects.link(light);light.rotation_euler=(.4,-.5,-.4)
d=bpy.data.cameras.new(P+'Camera');cam=bpy.data.objects.new(P+'Camera',d);bpy.context.collection.objects.link(cam);scene.camera=cam;d.lens=G['lens'];scene.view_settings.view_transform='AgX'
(O/'manifest.json').write_text(json.dumps({'assets':{n:{k:v for k,v in a.items() if k!='objects'} for n,a in assets.items()},'placementsBlender':placements,'source':'Incremental adaptation of original Generated/FestivalWorld and FestivalGear; eight new support assets.','status':'Unity/native review delegated to Sol.'},indent=2)+'\n');(E/'palette.json').write_text(json.dumps({'entries':[{'name':P+n,'rgb':list(c),'roughness':CONFIG['surface']['roughness'],'metallic':CONFIG['surface']['metallic'] if n=='Metal' else 0,'emission':CONFIG['surface']['emission'] if 'Glow' in n else 0} for n,c in CONFIG['colors'].items()]},indent=2)+'\n')
cam.location=G['camera'];cam.rotation_euler=(Vector(G['target'])-cam.location).to_track_quat('-Z','Y').to_euler();bpy.ops.wm.save_as_mainfile(filepath=str(O/'RemainingWorld.blend'))
for title,subset in [('landmarks',['Medical','Security','Shuttle','StallSupplies','StallStock']),('camp',['CampShop','Tent','DomeTent','CampCar','CampVan','CampShade','PortaPotty']),('props',['Bench','CrowdBarrier','RecyclingBin','WayfindingPost','StringLights','ShuttleStop','WaterBottle','RecoveryWristband']),('gear',gear_names+['LittleSpoon','RecoveryWristband','WaterBottle']),('forest',['TreeA','TreeB','TreeFir','GroveDetail'])]:
 for n,a in assets.items():
  for o in a['objects']:o.hide_render=n not in subset
 pitch=2 if title=='gear' else 7 if title=='props' else 11
 for i,n in enumerate(subset):
  shift=Vector((((i%3)-1)*pitch,-(i//3)*pitch,0))-Vector(placements[n])
  for o in assets[n]['objects']:
   if o.parent not in assets[n]['objects']:o.location+=shift
 bpy.context.view_layer.update();corners=[o.matrix_world@Vector(v) for n in subset for o in assets[n]['objects'] if o.type=='MESH' for v in o.bound_box];low=Vector(tuple(min(v[i] for v in corners) for i in range(3)));high=Vector(tuple(max(v[i] for v in corners) for i in range(3)));target=(low+high)/2;span=(high-low).length;cam.data.type='ORTHO';cam.data.ortho_scale=span*1.13;cam.location=target+Vector((span*.45,-span*.8,span*.65));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();floor=box('PreviewFloor',(target.x,target.y,-.07),(span*1.4,span*1.4,.12),'CanvasDark');scene.render.filepath=str(O/(title+'-preview.png'));bpy.ops.render.render(write_still=True);bpy.data.objects.remove(floor,do_unlink=True)
 for i,n in enumerate(subset):
  shift=Vector(placements[n])-Vector((((i%3)-1)*pitch,-(i//3)*pitch,0))
  for o in assets[n]['objects']:
   if o.parent not in assets[n]['objects']:o.location+=shift
