CONFIG = {'prefix':'AH04P_','src':'AH03P_','source':'ArtSource/ProductionPackage03/PeopleUpgrade.blend','out':'ArtSource/ProductionPackage04','exports':'Assets/Festival/Art/ProductionPeople04','renders':'artifacts/production04-people','fps':30,'cast':['AttendeeLanky','AttendeeAverage','AttendeeStocky','VendorSupplies','VendorPerformance','VendorStock','Medic','Security','MissingFriend'],
 'clips':{'Walk':{'frames':43,'loop':1,'cycles':2,'arm':.5,'gait':{'S':.5,'D':.6,'la':-.3,'lo':.55,'r1':.12,'r2':.3,'lift':.1,'dir':1,'f0':.03}},'Run':{'frames':31,'loop':1,'cycles':2,'arm':.75,'gait':{'S':.6,'D':.36,'la':-.1,'lo':.8,'r1':.1,'r2':.45,'lift':.2,'dir':1,'f0':-.02,'kick':.7}},'CrawlDowned':{'frames':61,'loop':1,'cycles':2,'gait':{'S':.4,'D':.6,'la':0,'lo':0,'r1':.1,'r2':.1,'lift':.1,'dir':1,'f0':.62,'z0':.19}},'DragOther':{'frames':43,'loop':1,'cycles':2,'gait':{'S':.5,'D':.58,'la':.25,'lo':-.2,'r1':.12,'r2':.2,'lift':.07,'dir':-1,'f0':-.08,'pl':'ball','pt':'heel'}},'BeingDragged':{'frames':43,'loop':1,'cycles':2,'sit':.8,'pelvis':.35,'hips':-.58},'DetainedEscort':{'frames':49,'loop':1,'cycles':2,'gait':{'S':.34,'D':.62,'la':-.15,'lo':.3,'r1':.1,'r2':.25,'lift':.05,'dir':1,'f0':.02}},
 'SwarmLunge':{'frames':49,'loop':1},'WookStare':{'frames':61,'loop':0},'WookLockedOn':{'frames':61,'loop':1},'DJLoop':{'frames':61,'loop':1},'PoiRoutine':{'frames':61,'loop':1,'revs':3,'radius':.23},'BoardShuttle':{'frames':55,'loop':0,'step':.32,'fwd':.47},'Talk':{'frames':73,'loop':1},'Handoff':{'frames':46,'loop':0},'Sell':{'frames':76,'loop':0},'Consume':{'frames':61,'loop':0}},'drag':{'grip':[.4,.42,1.0],'friend':.72,'tug':.03},
 'lod':{'LOD1':{'target':.5,'keep_face':1,'floor':320,'inflate':.002,'tuck':.008},'LOD2':{'target':.175,'keep_face':0,'floor':200,'inflate':.004,'tuck':.012},'layers':[['_Body_',['Shirt','Wardrobe','Shoes','Role','Accessory','Headgear','Hair']],['Shirt',['Role','Accessory']],['Hair',['Headgear']]]},'foot':{'heel':[-.145,-.158],'ball':[.31,-.155],'toe':[.323,-.146],'back':[-.154,-.146]},
 'render':{'showcase':'MissingFriend','partner':'AttendeeLanky','lod_casts':['AttendeeLanky','VendorPerformance','Medic'],'lod_poses':[['Walk',.25],['DJLoop',.1],['CrawlDowned',.3]],'panel':[300,380],'per_sheet':4,'poses':{'loop':[0,.25,.5,.75],'once':[.1,.35,.6,.9]},'ortho':3.0,'wide':4.2,'view':[.85,.52,.3],'tile':[3.2,.4],'check':[.36,.40,.33],'bg':[.30,.34,.40],'ground':[.44,.47,.42],'step':[.62,.52,.36],'label':[.97,.95,.88]}}
import bpy,bmesh,math,json,tempfile
import numpy as np
from pathlib import Path
from mathutils import Vector as V,Matrix as M,Euler as E
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc
C=CONFIG;R=Path(__file__).resolve().parents[1];P=C['prefix'];S0=C['src'];O=R/C['out'];X=R/C['exports'];Q=R/C['renders'];tau=math.tau;FT=C['foot']
for d in (O,X,Q):d.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
for o in [o for o in bpy.data.objects if o.name.startswith(P)]:bpy.data.objects.remove(o,do_unlink=True)
old=bpy.data.scenes.get(P+'People');sc=bpy.data.scenes.new(P+'People');bpy.context.window.scene=sc
if old:bpy.data.scenes.remove(old)
sc.name=P+'People'
for col in (bpy.data.meshes,bpy.data.curves,bpy.data.cameras,bpy.data.armatures,bpy.data.actions,bpy.data.worlds,bpy.data.images,bpy.data.materials):
 for d in [d for d in col if d.name.startswith((P,S0)) and d.users==0]:col.remove(d)
with bpy.data.libraries.load(str(R/C['source']),link=False) as (a,b):b.objects=[n for n in a.objects if any(n.startswith(S0+c+'_') or n==S0+c+'Rig' for c in C['cast'])]
rigs={}
for o in b.objects:
 sc.collection.objects.link(o);c=next(c for c in C['cast'] if o.name.startswith(S0+c))
 if o.type=='ARMATURE':o.name=P+c+'_LOD0Rig';o.data.name=o.name;o.animation_data_clear();o.location=(0,0,0);rigs[c]=o
 else:o.name=P+c+'_LOD0'+o.name[len(S0+c):];o.data.name=o.name
for a in [a for a in bpy.data.actions if a.name.startswith(S0) and a.users==0]:bpy.data.actions.remove(a)
mr=bpy.data.objects.new(P+'MotionRig',rigs[C['cast'][0]].data.copy());mr.data.name=P+'MotionRig';sc.collection.objects.link(mr);bpy.context.view_layer.update()
for r in [mr,*rigs.values()]:
 for pb in r.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
sc.render.fps=C['fps'];sc.frame_start=1
order=[x.name for x in mr.data.bones];par={x.name:x.parent.name if x.parent else None for x in mr.data.bones};L={x.name:x.matrix_local.copy() for x in mr.data.bones}
Rel={n:(L[par[n]].inverted()@L[n] if par[n] else L[n]) for n in order};R0=L['Hips'].to_3x3();LM=Rel['ShinL'].translation.length+Rel['FootL'].translation.length

def W(x,f,z):return V((x,-f,z))
def sn(x):return math.sin(tau*x)
def cs(x):return math.cos(tau*x)
def bump(t,a,b):return math.sin(math.pi*min(max((t-a)/(b-a),0),1))**2
def ks(t,K,loop):
 T=[k[0] for k in K];Pv=[k[1] if isinstance(k[1],tuple) else (k[1],) for k in K];n=len(K);t=t%1 if loop else min(max(t,T[0]),T[-1]);i=min(max(j for j in range(n) if T[j]<=t),n-2)
 def tg(j):
  if j in (0,n-1):
   if not loop:return [0]*len(Pv[0])
   pa,pb,ta,tb=Pv[n-2],Pv[1],T[n-2]-1,T[1]
  else:pa,pb,ta,tb=Pv[j-1],Pv[j+1],T[j-1],T[j+1]
  return [0 if (p-x)*(y-p)<=0 else (y-x)/(tb-ta) for x,p,y in zip(pa,Pv[j],pb)]
 h=T[i+1]-T[i];u=(t-T[i])/h;m0,m1=tg(i),tg(i+1);c=(2*u**3-3*u*u+1,u**3-2*u*u+u,-2*u**3+3*u*u,u**3-u*u)
 r=tuple(c[0]*p0+c[1]*h*a0+c[2]*p1+c[3]*h*a1 for p0,a0,p1,a1 in zip(Pv[i],m0,Pv[i+1],m1));return r if len(r)>1 else r[0]
def roll(pv,th):rf,rz=-pv[0],-pv[1];c,s=math.cos(th),math.sin(th);return rf*c+rz*s,-rf*s+rz*c
def stance(u,g):
 gf=g['f0']+g['dir']*g['S']*(.5-u);th=0;pv=[0,0];z0=g.get('z0',.17)
 if u<g['r1']:th=g['la']*(1-u/g['r1'])**2;pv=FT[g.get('pl','heel')]
 elif u>1-g['r2']:th=g['lo']*((u-1+g['r2'])/g['r2'])**2;pv=FT[g.get('pt','ball')]
 df,dz=roll(pv,th);return gf+pv[0]+df,z0+pv[1]+dz,th
def gait(s,g):
 s%=1;D=g['D']
 if s<D:f,z,th=stance(s/D,g);return f,z,th,2 if th==0 else 1
 u=(s-D)/(1-D);e=1e-4;k=(1-D)/D;p0,p1=stance(1,g),stance(0,g);m0=[(a-b)/e*k for a,b in zip(p0,stance(1-e,g))];m1=[(a-b)/e*k for a,b in zip(stance(e,g),p1)]
 c=(2*u**3-3*u*u+1,u**3-2*u*u+u,-2*u**3+3*u*u,u**3-u*u);f,z,th=[c[0]*a+c[1]*b+c[2]*d+c[3]*q for a,b,d,q in zip(p0,m0,p1,m1)]
 z+=g['lift']*math.sin(math.pi*u**g.get('kick',1))**2;lo=min(z-pf*math.sin(th)+pz*math.cos(th) for pf,pz in FT.values())
 if g['la'] or g['lo']:z+=max(0,.008-lo)
 return f,z,th,0
def tip(x,f,th):df,dz=roll(FT['ball'],th);return (x,f+FT['ball'][0]+df,.17+FT['ball'][1]+dz,th)
def fr(v,p):x=v.normalized();y=(p-x*p.dot(x)).normalized();return M((x,y,x.cross(y))).transposed()
def aim(v0,p0,v1,p1):return fr(v1,p1)@fr(v0,p0).transposed()
def fk(b):
 Pm={}
 for n in order:Pm[n]=(Pm[par[n]]@Rel[n] if par[n] else Rel[n])@M.Translation(b[n][0])@b[n][1].to_4x4()
 return Pm
def tp(Pm,t):return Pm[t[0]]@V((t[1][0],t[1][2],t[1][1])) if isinstance(t[0],str) else W(*t)
def ik(b,Pm,u,l,e,T,pole,pz):
 Fu=Pm[par[u]]@Rel[u];A=Fu.translation;l1=Rel[l].translation.length;l2=Rel[e].translation.length;dv=T-A;d=min(max(dv.length,abs(l1-l2)+1e-4),(l1+l2)*.9995);n=dv.normalized()
 a=(l1*l1-l2*l2+d*d)/(2*d);m=(pole-n*pole.dot(n)).normalized();K=A+n*a+m*math.sqrt(max(l1*l1-a*a,0));T=A+n*d;H=n.cross(m).normalized();r=Fu.to_3x3().inverted();b[u][1]=aim(Rel[l].translation,Rel[l].translation.cross(pz),r@(K-A),r@H)
 Fl=Fu@b[u][1].to_4x4()@Rel[l];r=Fl.to_3x3().inverted();b[l][1]=aim(Rel[e].translation,Rel[e].translation.cross(pz),r@(T-K),r@H);return Fl@b[l][1].to_4x4()@Rel[e]
def solve(s):
 b={n:[V((0,0,0)),M.Identity(3)] for n in order};hl=s.get('hl',(0,0,0));b['Hips'][0]=V((hl[0],hl[2],hl[1]));drop=0;req={}
 for k,n in (('hr','Hips'),('sp','Spine'),('hd','Head')):b[n][1]=E(s.get(k,(0,0,0))).to_matrix()
 for _ in range(8):
  Pm=fk(b);ex=max([0]+[(tp(Pm,s['l'+d][1])-(Pm['Hips']@Rel['Leg'+d]).translation).length-LM*.985 for d in 'LR' if s['l'+d][0]=='ik' and s['l'+d][3]])
  if ex<1e-5:break
  b['Hips'][0].y-=ex;drop+=ex
 Pm=fk(b)
 for d,sg in (('L',-1),('R',1)):
  a=s['a'+d];l=s['l'+d]
  if a[0]=='fk':
   for n,v in zip(('Arm','Forearm','Hand'),a[1:]):b[n+d][1]=E(v).to_matrix()
  else:T=tp(Pm,a[1]);req['h'+d]=T.copy();F=ik(b,Pm,'Arm'+d,'Forearm'+d,'Hand'+d,T,W(*a[2]),V((0,0,-1)));b['Hand'+d][1]=F.to_3x3().inverted()@R0 if a[3]=='down' else E(a[3]).to_matrix()
  if l[0]=='fk':
   for n,v in zip(('Leg','Shin','Foot'),l[1:4]):b[n+d][1]=E(v).to_matrix()
  else:T=tp(Pm,l[1]);F=ik(b,Pm,'Leg'+d,'Shin'+d,'Foot'+d,T,W(sg*.15,1,0),V((0,0,1)));b['Foot'+d][1]=F.to_3x3().inverted()@R0@E(l[2]).to_matrix();req[d]=(T.copy(),l[3])
 return b,drop,req
def stand(s,w=.21,toe=.1):
 for d,sg in (('L',-1),('R',1)):s['l'+d]=('ik',(sg*w,.02,.17),(0,sg*toe,0),2)
 return s

def walk(t,c):
 g=c['gait'];ph=t*c['cycles']%1;a=c['arm'];w=1+.35*sn(t)
 s={'hl':(-.035*sn(ph-.05),0,-.04-.022*cs(2*ph)),'hr':(.06,.13*cs(ph),-.05*sn(ph-.05)),'sp':(.05+.03*cs(2*ph+.1),-.17*cs(ph),.035*sn(ph-.05)+.03*sn(t)),'hd':(-.05+.05*sn(2*ph-.15),.05*cs(ph)+.28*sn(t)**3,-.02*sn(ph)+.04*sn(2*t))}
 for d,sg,o in (('L',-1,0),('R',1,.5)):
  f,z,th,st=gait(ph+o,g);s['l'+d]=('ik',(sg*.19,f,z),(th,sg*.06,0),st)
  s['a'+d]=('fk',(-sg*a*cs(ph)*(w if sg>0 else 1)+.1*sn(2*ph+.2),0,sg*(.14+.04*sn(2*ph))),(-.3-.4*max(0,sg*cs(ph-.08)),0,0),(.15*sn(ph-.2+o),0,0))
 return s
def run(t,c):
 g=c['gait'];ph=t*c['cycles']%1;a=c['arm'];fl=.5-.5*cs(t)
 s={'hl':(-.03*sn(ph-.1),.02,-.085-.035*cs(2*(ph-.18))),'hr':(.2,.18*cs(ph),-.06*sn(ph-.1)),'sp':(.14+.04*cs(2*ph),-.24*cs(ph),.04*sn(ph)),'hd':(-.28+.08*sn(2*ph-.2),.1*cs(ph),.06*sn(2*t))}
 for d,sg,o in (('L',-1,0),('R',1,.5)):
  f,z,th,st=gait(ph+o,g);s['l'+d]=('ik',(sg*.18,f,z),(th,sg*.05,0),st)
  s['a'+d]=('fk',(-sg*a*cs(ph)+.1*sn(2*ph),sg*.2*fl,sg*(.25+.35*fl+.08*sn(2*ph+.3))),(-1.35+.3*sg*cs(ph-.1),0,0),(.25*sn(2*ph+o),0,0))
 return s
def crawl(t,c):
 g=c['gait'];ph=t*c['cycles']%1;tired=.5+.5*cs(t)
 s={'hl':(.03*sn(ph+.1),.03*cs(2*ph),-.59+.012*cs(2*ph)),'hr':(1.35,.08*sn(ph),.07*sn(ph+.1)),'sp':(-.55+.06*cs(2*ph+.2),-.1*sn(ph),-.05*sn(ph)),'hd':(-.35-.08*tired+.06*sn(2*ph-.3),.12*sn(ph-.1),.15*sn(t)+.05*sn(2*ph))}
 for d,sg,o in (('L',-1,0),('R',1,.5)):
  f,z,th,st=gait(ph+o,g);s['a'+d]=('ik',(sg*.3,f,z),(sg*.9,-.6,.3),'down')
  s['l'+d]=('fk',(.05+.05*sn(ph+o),0,sg*(.12+.03*sn(ph))),(.15+.1*max(0,sn(ph+o)),0,0),(1.5+.2*sn(ph+o+.2),sg*.3,0))
 return s
def dragother(t,c):
 g=c['gait'];ph=t*c['cycles']%1;D=C['drag'];tug=D['tug']*(.5+.5*cs(2*ph-.1));look=bump(t,.55,.95)
 s={'hl':(-.03*sn(ph),-.02,-.1-.02*cs(2*ph)),'hr':(-.04,.1*cs(ph),-.04*sn(ph)),'sp':(-.06+.05*cs(2*ph),-.08*cs(ph)+.12*look,.03*sn(ph)),'hd':(.12+.06*sn(2*ph),.95*look,.12*look)}
 for d,sg,o in (('L',-1,0),('R',1,.5)):
  f,z,th,st=gait(ph+o,g);s['l'+d]=('ik',(sg*.22,f,z),(th,sg*.08,0),st);gx,gf,gz=D['grip']
  s['a'+d]=('ik',(sg*gx,gf-tug,gz+.02*cs(2*ph)),(sg*.8,-.5,-.6),(0,0,0))
 return s
def dragged(t,c):
 ph=t*c['cycles']%1;D=C['drag'];tug=D['tug']*(.5+.5*cs(2*ph-.1));j=.5+.5*cs(2*ph-.18);a=c['sit']
 s={'hl':(.02*sn(ph),.02*j,c['hips']+.01*j),'hr':(c['pelvis']-math.pi/2+.03*j,math.pi,.05*sn(ph)),'sp':(a+.04*j,.06*sn(ph),.04*sn(ph+.3)),'hd':(.95+.1*sn(2*ph-.3),.2*sn(t),.3*sn(t+.1)+.08*sn(2*ph))}
 for d,sg in (('L',-1),('R',1)):
  s['a'+d]=('ik',(-sg*D['grip'][0],D['friend']+tug,D['grip'][2]+.02*cs(2*ph)),(-sg*.9,-.3,-.4),(0,0,0))
  s['l'+d]=('fk',(-c['pelvis']-.2+.04*j*sg,sg*.05,sg*(.16+.03*sn(ph+.25*sg))),(.7+.1*j,0,0),(-.5+.15*sn(2*ph+.25*sg),sg*.4,0))
 return s
def escort(t,c):
 g=c['gait'];ph=t*c['cycles']%1;look=bump(t,.35,.75)
 s={'hl':(-.025*sn(ph-.05),0,-.05-.015*cs(2*ph)),'hr':(.08,.06*cs(ph),-.04*sn(ph-.05)),'sp':(.14+.02*cs(2*ph),-.05*cs(ph)-.3*look,.03*sn(ph)),'hd':(.28-.2*look+.04*sn(2*ph-.2),-.75*look,.05*sn(ph)+.1*look)}
 for d,sg,o in (('L',-1,0),('R',1,.5)):
  f,z,th,st=gait(ph+o,g);s['l'+d]=('ik',(sg*.16,f,z),(th,sg*.1,0),st);s['a'+d]=('ik',('Hips',(sg*.1,-.28,.15)),(sg*.9,-.8,.1),(.3,0,0))
 return s
def swarm(t,c):
 K=lambda *k:ks(t,k,1);w=bump(t,.5,.85)*sn(8*t)
 s={'hl':K((0,(0,0,-.07)),(.25,(0,-.1,-.1)),(.45,(.03,.26,-.13)),(.62,(.02,.22,-.11)),(.8,(0,.1,-.09)),(1,(0,0,-.07))),'hr':(K((0,.15),(.25,-.05),(.45,.35),(.62,.32),(.8,.22),(1,.15)),.12*w,.05*w),
  'sp':(K((0,.15),(.25,-.18),(.45,.4),(.62,.34),(.8,.22),(1,.15)),.1*sn(t),.1*w),'hd':(K((0,-.2),(.25,-.05),(.45,-.5),(.62,-.45),(.8,-.3),(1,-.2)),.15*w,.2*w)}
 h=K((0,(.32,.3,1.3)),(.25,(.45,-.05,1.52)),(.45,(.22,1.1,1.2)),(.62,(.1,.86,1.12)),(.8,(.22,.6,1.22)),(1,(.32,.3,1.3)))
 for d,sg in (('L',-1),('R',1)):s['a'+d]=('ik',(sg*(h[0]+.03*w),h[1]+.05*w*sg,h[2]+.04*w),(sg*.8,-.3,-1),(.2,0,0))
 rf=K((0,(.22,.02,.17,0)),(.3,(.22,.02,.17,0)),(.38,(.22,.24,.27,-.2)),(.45,(.24,.42,.17,0)),(.78,(.24,.42,.17,0)),(.86,(.23,.22,.25,.1)),(.94,(.22,.02,.17,0)),(1,(.22,.02,.17,0)))
 s['lL']=('ik',(-.22,.02,.17),(0,-.1,0),2);s['lR']=('ik',rf[:3],(rf[3],.1,0),2 if t<.3 or .45<=t<=.78 or t>=.94 else 0);return s
def stare(t,c):
 K=lambda *k:ks(t,k,0);tr=bump(t,.6,1)*.012*sn(9*t)
 s={'hl':(0,0,K((0,-.025),(.5,-.03),(1,-.05))),'hr':(K((0,0),(1,.05)),K((0,.22),(.3,.22),(.48,-.04),(.58,0)),0),'sp':(K((0,0),(.6,.06),(1,.12)),K((0,.35),(.3,.35),(.45,-.06),(.55,0)),K((0,0),(.6,0),(.8,.04))),
  'hd':(K((0,.05),(.3,0),(.6,.14),(1,.16))+tr,K((0,.3),(.14,.3),(.24,-.1),(.32,0)),K((0,0),(.6,0),(.82,.2),(1,.2))+tr)}
 for d,sg in (('L',-1),('R',1)):s['a'+d]=('fk',(K((0,0),(.6,.05),(1,.1)),0,sg*K((0,.08),(.6,.02),(1,-.02))),(K((0,-.15),(1,-.35)),0,0),(0,sg*.2,0))
 return stand(s,.2,.08)
def locked(t,c):
 s={'hl':(.04*sn(t),-.06,-.1+.015*sn(2*t)),'hr':(.2,.08*sn(t),-.03*sn(t)),'sp':(.32+.03*sn(2*t+.3),0,-.05*sn(t)),'hd':(-.45-.03*sn(2*t+.5),-.08*sn(t),.05*sn(t))}
 for d,sg in (('L',-1),('R',1)):s['a'+d]=('ik',(sg*(.25+.02*sn(4*t+sg*.2)),.45,1.3+.05*sn(2*t+sg*.25)),(sg*.9,-.4,-.6),(-.5+.2*sn(4*t+sg*.1),0,0))
 return stand(s,.26,.18)
def dj(t,c):
 bt=abs(math.sin(4*math.pi*t));e1=.5+.5*cs(t);sw=sn(8*t)
 s={'hl':(.02*sn(t),-.02,-.08+.045*bt),'hr':(.12,.06*sn(t),.03*sn(2*t)),'sp':(.2+.05*abs(math.sin(4*math.pi*t+.25)),.08*sn(t),.05*sn(t+.25)),'hd':(.08+.16*abs(math.sin(4*math.pi*t+.45)),.1*sn(t+.1),.08*sn(2*t))}
 for d,sg,e in (('L',-1,1-e1),('R',1,e1)):tw=(1-e)*.018;s['a'+d]=('ik',(sg*.22+tw*cs(4*t),.44+.07*sw*e+tw*sn(4*t),.93+.012*bt),(sg*.8,-.5,-.5),(.3,sg*1.2,0))
 return stand(s,.24,.12)
def poi(t,c):
 n=c['revs'];r=c['radius'];bn=abs(math.sin(math.pi*2*n*t))
 s={'hl':(.04*sn(t),0,-.06+.03*bn),'hr':(.05,.12*sn(t),.04*sn(2*t)),'sp':(.06,-.1*sn(t),.05*sn(t+.2)),'hd':(-.05+.06*sn(2*n*t+.2),.15*sn(t),.1*sn(n*t))}
 for d,sg,o in (('L',-1,0),('R',1,.5)):a=tau*(n*t+o);s['a'+d]=('ik',(sg*.56,.3+r*math.sin(a),1.12+r*math.cos(a)),(sg*.9,-.4,-.8),(0,0,0))
 return stand(s,.24,.1)
def board(t,c):
 K=lambda *k:ks(t,k,0);st=c['step'];fw=c['fwd'];zs=st+.17
 s={'hl':K((0,(0,0,-.025)),(.12,(.04,.02,-.04)),(.36,(0,.16,-.05)),(.52,(-.02,.28,.05)),(.7,(-.02,fw-.05,st-.06)),(.85,(0,fw,st-.02)),(1,(0,fw,st-.025))),'hr':(K((0,0),(.36,.12),(.6,.15),(.85,.02),(1,0)),0,0),
  'sp':(K((0,0),(.36,.2),(.6,.28),(.85,.05),(1,0)),K((0,0),(.3,-.1),(.7,-.05),(1,0)),0),'hd':(K((0,0),(.12,.3),(.36,.25),(.6,0),(.85,-.05),(1,0)),0,K((0,0),(.8,0),(.88,.12),(.95,-.06),(1,0)))}
 lf=K((0,(-.2,.02,.17,0)),(.12,(-.2,.02,.17,0)),(.22,(-.2,.2,.42,-.3)),(.3,(-.19,fw,zs+.08,-.15)),(.36,(-.19,fw,zs,0)),(1,(-.19,fw,zs,0)))
 rf=K((0,(.2,.02,.17,0)),(.44,(.2,.02,.17,0)),(.47,tip(.2,.02,.2)),(.5,tip(.2,.02,.42)),(.53,tip(.2,.02,.6)),(.56,tip(.2,.02,.72)),(.66,(.21,.3,.76,-.25)),(.8,(.2,fw,zs,0)),(1,(.2,fw,zs,0)))
 s['lL']=('ik',lf[:3],(lf[3],-.08,0),2 if t<.12 or t>=.36 else 0);s['lR']=('ik',rf[:3],(rf[3],.08,0),2 if t<.44 or t>=.8 else 1 if t<.56 else 0)
 s['aR']=('ik',K((0,(.5,.06,.8)),(.12,(.46,.36,1.2)),(.2,(.44,.58,1.36)),(.42,(.44,.58,1.45)),(.58,(.6,.45,1.35)),(.78,(.52,fw+.14,st+.95)),(1,(.5,fw+.06,st+.8))),(1,0,-1),(0,0,0))
 s['aL']=('ik',K((0,(-.5,.06,.8)),(.3,(-.62,.15,1.0)),(.55,(-.66,.3,1.15)),(.75,(-.6,.4,1.2)),(.88,(-.55,fw+.1,st+.9)),(1,(-.5,fw+.06,st+.8))),(-1,-.3,-.6),(0,0,0))
 return s
def talk(t,c):
 K=lambda *k:ks(t,k,1);sh=bump(t,.3,.47)
 s={'hl':(.03*sn(t),0,-.035+.012*sn(2*t)),'hr':(0,.08*sn(t),.03*sn(t+.25)),'sp':(.06-.1*sh+.04*sn(4*t),-.2*bump(t,.05,.3)-.25*bump(t,.55,.8)+.1*sn(t+.1),.06*sn(t)),'hd':(.04+.06*sn(4*t+.1)-.05*sh,.12*sn(t+.15),.1*sn(2*t+.2)+.12*sh)}
 s['aR']=('ik',K((0,(.46,.16,1.0)),(.12,(.3,.45,1.35)),(.25,(.36,.4,1.25)),(.38,(.62,.25,1.38)),(.5,(.46,.16,1.0)),(.62,(.26,.5,1.4)),(.75,(.34,.42,1.28)),(.88,(.48,.16,1.0)),(1,(.46,.16,1.0))),(.9,-.5,-.6),
  (K((0,.1),(.12,-.3),(.38,-.2),(.62,-.4),(1,.1)),K((0,0),(.12,-1.2),(.38,-1.3),(.5,0),(.62,-.6),(1,0)),0))
 s['aL']=('ik',K((0,(-.5,.06,.82)),(.22,(-.48,.12,.9)),(.38,(-.62,.25,1.36)),(.5,(-.46,.14,.95)),(.7,(-.3,.45,1.3)),(.85,(-.47,.1,.88)),(1,(-.5,.06,.82))),(-.9,-.5,-.6),(0,K((0,0),(.38,1.3),(.5,0),(.7,.8),(1,0)),0))
 return stand(s)
def handoff(t,c):
 K=lambda *k:ks(t,k,0);ln=K((0,0),(.3,.14),(.62,.14),(1,0));ex=bump(t,.2,.7)
 s={'hl':(0,K((0,0),(.3,.06),(.62,.06),(1,0)),-.04),'hr':(ln*.6,K((0,0),(.3,-.12),(.62,-.12),(1,0)),0),'sp':(ln*1.6,K((0,0),(.3,-.3),(.62,-.3),(1,0)),K((0,0),(.3,-.08),(.62,-.08),(1,0))),'hd':(K((0,0),(.3,.15),(.48,.25),(.56,.12),(.8,.05),(1,0)),K((0,0),(.3,-.12),(.62,-.1),(1,0)),K((0,0),(.5,.1),(1,0)))}
 s['aR']=('ik',K((0,(.5,.06,.8)),(.3,(.2,.6,1.25)),(.45,(.18,.64,1.27)),(.62,(.18,.64,1.27)),(.85,(.44,.16,.9)),(1,(.5,.06,.8))),(.9,-.2,-.8),(0,K((0,0),(.3,-1.3),(.62,-1.3),(1,0)),0))
 s['aL']=('ik',(-.5,.06+.04*ex,.8+.03*ex),(-.8,-.6,-.3),(0,0,0));return stand(s)
def sell(t,c):
 K=lambda *k:ks(t,k,0);ln=K((0,0),(.16,.12),(.47,.1),(.6,0),(1,0))
 s={'hl':(0,.05*ln,-.04),'hr':(ln*.6,K((0,0),(.16,-.1),(.47,-.1),(.6,0),(1,0)),0),'sp':(ln*1.6,K((0,0),(.16,-.28),(.47,-.25),(.6,.1),(.72,0),(1,0)),K((0,0),(.16,-.06),(.47,-.06),(.6,0),(1,0))),'hd':(K((0,0),(.16,.12),(.47,.15),(.58,.3),(.68,.1),(1,0)),K((0,0),(.58,.2),(.66,0),(.72,.35),(.8,.35),(.84,-.35),(.9,-.35),(.96,0),(1,0)),K((0,0),(.58,.1),(.7,0),(1,0)))}
 s['aR']=('ik',K((0,(.5,.06,.8)),(.16,(.24,.58,1.25)),(.3,(.24,.6,1.26)),(.38,(.22,.52,1.18)),(.44,(.22,.52,1.18)),(.47,(.22,.52,1.14)),(.58,(.3,.35,1.45)),(.7,(.46,.02,1.0)),(.76,(.44,-.02,.98)),(.8,(.44,-.02,1.02)),(.84,(.44,-.02,.98)),(.88,(.44,-.02,1.02)),(1,(.5,.06,.8))),(.9,-.3,-.7),
  (K((0,0),(.16,0),(.3,0),(.38,.2),(.58,-.3),(.7,0),(1,0)),K((0,0),(.16,-1.3),(.47,-1.4),(.58,-.8),(.7,0),(1,0)),0))
 s['aL']=('ik',(-.5,.06,.8+.02*bump(t,.1,.5)),(-.8,-.6,-.3),(0,0,0));return stand(s)
def consume(t,c):
 K=lambda *k:ks(t,k,0);sh=bump(t,.66,.96)*sn(10*t)
 s={'hl':(.02*sh,0,-.03),'hr':(0,0,.03*sh),'sp':(K((0,0),(.3,-.08),(.5,-.1),(.62,.02),(1,0)),.05*sh,.08*sh),'hd':(K((0,0),(.25,.05),(.35,-.25),(.5,-.3),(.62,0),(1,0)),.12*sh,.2*sh)}
 s['aR']=('ik',K((0,(.5,.06,.8)),(.25,(.14,.42,1.5)),(.32,(.1,.4,1.56)),(.5,(.1,.38,1.58)),(.62,(.36,.22,1.1)),(.74,(.5,.06,.82)),(1,(.5,.06,.8))),(1,-.2,-.6),(K((0,0),(.3,-.4),(.5,-.8),(.62,0),(1,0)),0,0))
 s['aL']=('ik',(-.5-.08*abs(sh),.06,.8+.1*abs(sh)),(-.8,-.6,-.3),(0,0,0));return stand(s)
FN={'Walk':walk,'Run':run,'CrawlDowned':crawl,'DragOther':dragother,'BeingDragged':dragged,'DetainedEscort':escort,'SwarmLunge':swarm,'WookStare':stare,'WookLockedOn':locked,'DJLoop':dj,'PoiRoutine':poi,'BoardShuttle':board,'Talk':talk,'Handoff':handoff,'Sell':sell,'Consume':consume}

mr.animation_data_create();acts={};reqs={};drops={}
for name,c in C['clips'].items():
 n=c['frames'];act=bpy.data.actions.new(P+name);mr.animation_data.action=act;prev={};rows={x:[] for x in order};hips=[];reqs[name]=[];drops[name]=0
 for i in range(n):
  bs,dr,rq=solve(FN[name](i/(n-1),c));drops[name]=max(drops[name],dr);reqs[name].append(rq);hips.append(bs['Hips'][0].copy())
  for x in order:e=bs[x][1].to_euler('XYZ',prev.get(x));prev[x]=e;rows[x].append(e)
 def put(path,k,vals,g):fc=act.fcurve_ensure_for_datablock(mr,path,index=k,group_name=g);fc.keyframe_points.add(n);fc.keyframe_points.foreach_set('co',[v for i,y in enumerate(vals) for v in (i+1,y)]);fc.update()
 for x in order:
  for k in range(3):put(f'pose.bones["{x}"].rotation_euler',k,[e[k] for e in rows[x]],x)
 for k in range(3):put('pose.bones["Hips"].location',k,[h[k] for h in hips],'Hips')
 tr=mr.animation_data.nla_tracks.new();tr.name=name;tr.strips.new(name,1,act);tr.mute=True;acts[name]=act
mr.animation_data.action=None

def pose(r,act,f):
 r.animation_data_create();r.animation_data.action=act
 if act:r.animation_data.action_slot=act.slots[0];sc.frame_set(f)
 else:
  for pb in r.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
  sc.frame_set(1)
def ang(a,b):x=math.degrees(a.to_quaternion().rotation_difference(b.to_quaternion()).angle)%360;return min(x,360-x)
def pts(Mb):return [p for x in order for p in (Mb[x].translation,Mb[x]@V((0,.15,0)))]
clipinfo={}
for name,c in C['clips'].items():
 n=c['frames'];fr_=[]
 for f in range(1,n+1):pose(mr,acts[name],f);fr_.append({pb.name:pb.matrix.copy() for pb in mr.pose.bones})
 step=max(ang(a[x],b[x]) for a,b in zip(fr_,fr_[1:]) for x in order);err=herr=0;sole=None;vel=[]
 for i,Mb in enumerate(fr_):
  for d,T in reqs[name][i].items():
   if d[0]=='h':herr=max(herr,(Mb['Hand'+d[1]].translation-T).length);continue
   T,st=T
   if st:err=max(err,(Mb['Foot'+d].translation-T).length)
   sole=min([sole or 9]+[(Mb['Foot'+d]@V((0,pz,pf))).z for pf,pz in FT.values()])
   if st==2 and i and reqs[name][i-1][d][1]==2:vel.append((Mb['Foot'+d].translation-fr_[i-1]['Foot'+d].translation).y*-C['fps'])
 g=c.get('gait');spd=g and g['S']/(g['D']*(n-1)/c['cycles']/C['fps'])
 info={'frames':[1,n],'seconds':round((n-1)/C['fps'],3),'loop':bool(c['loop']),'maxStepDeg':round(step,2),'plantedAnkleErrMm':round(err*1000,2),'handTargetErrMm':round(herr*1000,1),'minSoleZ':sole and round(sole,4),'hipsClampMm':round(drops[name]*1000,1)}
 if c['loop']:info['loopPoseDeltaDeg']=round(max(ang(fr_[0][x],fr_[-1][x]) for x in order),4);info['loopPoseDeltaMm']=round(max((a-b).length for a,b in zip(pts(fr_[0]),pts(fr_[-1])))*1000,3);acc=lambda i,j,k:max((a-2*b+q).length for a,b,q in zip(pts(fr_[i]),pts(fr_[j]),pts(fr_[k])));info['seamAccelMm']=round(acc(-2,0,1)*1000,2);info['maxInteriorAccelMm']=round(max(acc(i-1,i,i+1) for i in range(1,n-1))*1000,2)
 if spd:info['groundSpeed']=round(spd,3);info['plantedFootFwdVelocity']=[round(min(vel),3),round(max(vel),3)] if vel else None
 elif vel:info['plantedFootSpeedMax']=round(max(abs(v) for v in vel),4)
 clipinfo[name]=info
pose(mr,None,1)

def tris(objs):return sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objs)
def edit(o,op,**k):
 with bpy.context.temp_override(object=o,active_object=o,selected_objects=[o],selected_editable_objects=[o]):op(**k)
def gi(v):return max(v.groups,key=lambda g:g.weight).group if len(v.groups) else -1
def decimate(o,o0,ratio,keep,inf):
 me=o.data;k0=o0.data.shape_keys;base=[x.co.copy() for x in (k0.key_blocks[0].data if k0 else o0.data.vertices)]
 extra={k.name:[k.data[i].co-base[i] for i in range(len(base))] for k in k0.key_blocks[1:]} if keep and k0 else {}
 if me.shape_keys:o.shape_key_clear()
 me.vertices.foreach_set('co',[x for v in base for x in v]);md=o.modifiers.new(P+'Decimate','DECIMATE');md.ratio=ratio;md.use_collapse_triangulate=True;md.use_symmetry=abs(sum(v.co.x for v in me.vertices))<.02*len(me.vertices);o.modifiers.move(len(o.modifiers)-1,0);edit(o,bpy.ops.object.modifier_apply,modifier=md.name)
 if me.has_custom_normals:edit(o,bpy.ops.mesh.customdata_custom_splitnormals_clear)
 for v in me.vertices:v.co+=v.normal*inf
 if not extra:return
 om=o0.data;og=[gi(v) for v in om.vertices];pl=[tuple(p.vertices) for p in om.polygons];full=(BVHTree.FromPolygons(base,pl),list(range(len(pl))));tr={}
 for g in set(og):ix=[i for i,p in enumerate(pl) if og[p[0]]==g];tr[g]=(BVHTree.FromPolygons(base,[pl[i] for i in ix]),ix)
 o.shape_key_add(name='Basis');kk={n:o.shape_key_add(name=n,from_mix=False) for n in extra}
 for k in o.data.shape_keys.key_blocks:k.value=0
 for v in me.vertices:
  t,ix=tr.get(gi(v),full);h=t.find_nearest(v.co)
  if h[0] is None:t,ix=full;h=t.find_nearest(v.co)
  p=pl[ix[h[2]]];w=poly_3d_calc([base[i] for i in p],h[0])
  for n,k in kk.items():k.data[v.index].co=v.co+sum((extra[n][i]*wi for i,wi in zip(p,w)),V())
def tuck(inner,outer,d):
 vs=[];fs=[]
 for o in outer:k=len(vs);vs+=[o.matrix_local@v.co for v in o.data.vertices];fs+=[[k+i for i in p.vertices] for p in o.data.polygons]
 t=BVHTree.FromPolygons(vs,fs);me=inner.data;kb=me.shape_keys.key_blocks if me.shape_keys else [];mi=inner.matrix_local;mv=[(v.index,-v.normal*d) for v in me.vertices if t.ray_cast(mi@(v.co+v.normal*.001),(mi.to_3x3()@v.normal).normalized(),.05)[0] is not None]
 for i,dv in mv:
  me.vertices[i].co+=dv
  for k in kb:k.data[i].co+=dv
 return len(mv)
def nonman(objs):
 c=0
 for o in objs:bm=bmesh.new();bm.from_mesh(o.data);c+=sum(1 for e in bm.edges if not e.is_manifold);bm.free()
 return c
LOD={};lodinfo={}
for c in C['cast']:
 r0=rigs[c];m0=sorted([o for o in r0.children if o.type=='MESH'],key=lambda o:o.name);face=next(o for o in m0 if o.name.endswith('_Face'));T0=tris(m0);F=tris([face])
 LOD[c]={'LOD0':(r0,m0)};lodinfo[c]={'LOD0':{'fbx':'../ProductionPeople03/'+S0+c+'.fbx','triangles':T0,'nonManifoldEdges':nonman(m0)}}
 for ln,lc in [(k,v) for k,v in C['lod'].items() if k!='layers']:
  r=r0.copy();r.name=P+c+'_'+ln+'Rig';sc.collection.objects.link(r);r.animation_data_clear();kf=lc['keep_face'];dm=[o for o in m0 if not(kf and o==face)];B=lc['target']*T0-F*kf;lo,hi=0,1;objs=[];rt={}
  for _ in range(40):mid=(lo+hi)/2;lo,hi=(mid,hi) if sum(max(min(tris([o]),lc['floor']),mid*tris([o])) for o in dm)<B else (lo,mid)
  for o in dm:rt[o]=min(1,max(min(tris([o]),lc['floor']),lo*tris([o]))/tris([o]))
  for o0 in m0:
   o=o0.copy();o.data=o0.data.copy();o.name=o0.name.replace('_LOD0_','_'+ln+'_');o.data.name=o.name;sc.collection.objects.link(o);o.parent=r;objs.append(o)
   for md in o.modifiers:
    if md.type=='ARMATURE':md.object=r
   if o0 in rt:decimate(o,o0,rt[o0],kf,0 if any(k in o0.name for k in ('_Body_','_Face')) else lc['inflate'])
  tk=sum(tuck(o,[q for q in objs if any(x in q.name for x in outs)],lc['tuck']) for key,outs in C['lod']['layers'] for o in objs if key in o.name and o.name.split(ln+'_')[1].split('_')[0] not in outs)
  LOD[c][ln]=(r,objs);lodinfo[c][ln]={'tuckedVerts':tk,'fbx':P+c+'_'+ln+'.fbx','triangles':tris(objs),'ratioOfLOD0':round(tris(objs)/T0,3),'meshDecimateRatio':{o.name.split('_LOD0_')[1]:round(x,3) for o,x in rt.items()},'nonManifoldEdges':nonman(objs),'shapeKeys':{o.name.split('_'+ln+'_')[1]:[k.name for k in o.data.shape_keys.key_blocks[1:]] for o in objs if o.data.shape_keys}}

def cloud(objs):
 dg=bpy.context.evaluated_depsgraph_get();vs=[];fs=[];ow=[]
 for o in objs:
  e=o.evaluated_get(dg);m=e.to_mesh();k=len(vs);vs+=[o.matrix_world@v.co for v in m.vertices];ow+=[o.name]*len(m.vertices);fs+=[[k+i for i in p.vertices] for p in m.polygons];e.to_mesh_clear()
 return vs,fs,ow
def dev(a,b):
 va,fa,_=cloud(a);vb,_,ow=cloud(b);t=BVHTree.FromPolygons(va,fa);d=sorted((t.find_nearest(v)[3],o) for v,o in zip(vb,ow));x=[q[0] for q in d]
 return {'maxMm':round(x[-1]*1000,1),'p99Mm':round(x[int(len(x)*.99)]*1000,1),'meanMm':round(sum(x)/len(x)*1000,2),'worst':d[-1][1].split(P)[1]}
posecheck={}
for c in C['render']['lod_casts']:
 posecheck[c]={}
 for clip,u in [['Rest',0]]+C['render']['lod_poses']:
  for ln in LOD[c]:pose(LOD[c][ln][0],acts.get(clip),1+round(u*(C['clips'].get(clip,{'frames':1})['frames']-1)))
  posecheck[c][clip]={ln:dev(LOD[c]['LOD0'][1],LOD[c][ln][1]) for ln in ('LOD1','LOD2')}
 b0=next(o for o in LOD[c]['LOD0'][1] if '_Body_' in o.name);b1=next(o for o in LOD[c]['LOD1'][1] if '_Body_' in o.name)
 for ln in LOD[c]:pose(LOD[c][ln][0],None,1)
 for o in (b0,b1):o.data.shape_keys.key_blocks['GripL'].value=o.data.shape_keys.key_blocks['GripR'].value=1
 posecheck[c]['GripBothLOD1Body']=dev([b0],[b1])
 for o in (b0,b1):o.data.shape_keys.key_blocks['GripL'].value=o.data.shape_keys.key_blocks['GripR'].value=0
 posecheck[c]['GripOffLOD1Body']=dev([b0],[b1])
dg=bpy.context.evaluated_depsgraph_get()
for clip in ('CrawlDowned','BeingDragged'):
 n=C['clips'][clip]['frames'];clipinfo[clip]['minMeshZByCast']={}
 for c in C['cast']:
  r,objs=LOD[c]['LOD0'];z=9
  for f in range(1,n+1,4):
   pose(r,acts[clip],f)
   for o in objs:e=o.evaluated_get(dg);m=e.to_mesh();z=min([z]+[(o.matrix_world@v.co).z for v in m.vertices]);e.to_mesh_clear()
  clipinfo[clip]['minMeshZByCast'][c]=round(z,3);pose(r,None,1)

def export(path,objs,anim):
 for o in sc.objects:o.select_set(False)
 for o in objs:o.select_set(True)
 rig=objs[0];old=rig.name;rig.name='FestivalRig';bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=anim,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=anim,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y');rig.name=old
for tr in mr.animation_data.nla_tracks:tr.mute=False
export(X/(P+'Motion.fbx'),[mr],True)
for tr in mr.animation_data.nla_tracks:tr.mute=True
for c in C['cast']:
 for ln in ('LOD1','LOD2'):r,objs=LOD[c][ln];pose(r,None,1);export(X/(P+c+'_'+ln+'.fbx'),[r,*objs],False)

G=C['render'];sc.render.engine='BLENDER_WORKBENCH';sh=sc.display.shading;sh.light='STUDIO';sh.color_type='MATERIAL';sh.show_shadows=True;sh.shadow_intensity=.75;sh.show_object_outline=True;sh.show_cavity=True;sc.display.light_direction=(-.3,.35,.88)
sc.world=bpy.data.worlds.new(P+'World');sc.world.color=G['bg'];sc.view_settings.view_transform='Standard';sc.render.resolution_percentage=100
def mat(n,c):m=bpy.data.materials.new(P+n);m.diffuse_color=(*c,1);return m
def mesh(n,vs,fs,ms,mi=None):
 me=bpy.data.meshes.new(P+n);me.from_pydata(vs,[],fs)
 for m in ms:me.materials.append(m)
 if mi:me.polygons.foreach_set('material_index',mi)
 o=bpy.data.objects.new(P+n,me);sc.collection.objects.link(o);return o
def slab(n,lo,hi,m):x0,y0,z0=lo;x1,y1,z1=hi;return mesh(n,[(x,y,z) for x in (x0,x1) for y in (y0,y1) for z in (z0,z1)],[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)],[m])
sz_,st_=G['tile'];k_=round(sz_/st_);vs_=[];fs_=[];mi_=[]
for i in range(k_):
 for j in range(k_+4):x0,y0=-sz_/2+i*st_,-sz_/2-1.2+j*st_;n0=len(vs_);vs_+=[(x0,y0,0),(x0+st_,y0,0),(x0+st_,y0+st_,0),(x0,y0+st_,0)];fs_.append((n0,n0+1,n0+2,n0+3));mi_.append((i+j)%2)
ground=mesh('Ground',vs_,fs_,[mat('Ground',G['ground']),mat('Check',G['check'])],mi_);stepo=slab('Step',(-.7,-1.5,0),(.7,-.25,C['clips']['BoardShuttle']['step']),mat('Step',G['step']))
cam=bpy.data.objects.new(P+'Camera',bpy.data.cameras.new(P+'Camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.clip_end=300
lab=bpy.data.objects.new(P+'Label',bpy.data.curves.new(P+'Label','FONT'));sc.collection.objects.link(lab);lab.data.materials.append(mat('Label',G['label']));lab.data.align_x='LEFT';lab.parent=cam
def look(eye,at,ortho=None,lens=50):
 cam.location=eye;cam.rotation_euler=(V(at)-V(eye)).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO' if ortho else 'PERSP';cam.data.lens=lens
 if ortho:cam.data.ortho_scale=ortho
def show(keep):
 for o in sc.objects:
  if o.type in ('MESH','FONT'):o.hide_render=o not in keep
def tag(tx,at,size):t=bpy.data.objects.new(P+'Tag',bpy.data.curves.new(P+'Tag','FONT'));t.data.body=tx;t.data.size=size;t.data.align_x='CENTER';t.data.materials.append(bpy.data.materials[P+'Label']);sc.collection.objects.link(t);t.location=at;return t
tmp=Path(tempfile.mkdtemp())
def shot(path,w,h):sc.render.resolution_x,sc.render.resolution_y=w,h;sc.render.filepath=str(path);bpy.ops.render.render(write_still=True)
def pix(path):im=bpy.data.images.load(str(path));w,h=im.size;a=np.empty(w*h*4,np.float32);im.pixels.foreach_get(a);bpy.data.images.remove(im);return a.reshape(h,w,4)
def save(a,path):h,w=a.shape[:2];im=bpy.data.images.new(P+'Sheet',w,h);im.pixels.foreach_set(a.ravel());im.filepath_raw=str(path);im.file_format='PNG';im.save();bpy.data.images.remove(im)
hero=LOD[G['showcase']]['LOD0'];mate=LOD[G['partner']]['LOD1'];pw,ph_=G['panel'];vd=V(G['view']).normalized();base=[ground,lab]
for g_,names in enumerate([list(C['clips'])[i:i+G['per_sheet']] for i in range(0,len(C['clips']),G['per_sheet'])]):
 rows=[]
 for name in names:
  c=C['clips'][name];row=[];pair=name in ('DragOther','BeingDragged');keep=base+hero[1]+(mate[1] if pair else [])+([stepo] if name=='BoardShuttle' else []);show(keep)
  if pair:mate[0].location=W(0,C['drag']['grip'][1]+C['drag']['friend'],0);mate[0].rotation_euler=(0,0,math.pi)
  at=W(0,.7 if pair else .2,1.0);sz=G['wide'] if pair else G['ortho'];look(at+W(vd.x,vd.y,vd.z)*12,at,sz);lab.location=(-sz*pw/ph_/2+.06,-sz/2+.07,-2);lab.rotation_euler=(0,0,0);lab.data.size=sz*.05
  for u in G['poses']['loop' if c['loop'] else 'once']:
   f=1+round(u*(c['frames']-1));pose(hero[0],acts[name],f)
   if pair:pose(mate[0],acts['BeingDragged' if name=='DragOther' else 'DragOther'],f)
   lab.data.body=f'{name}  f{f}/{c["frames"]}'+('  loop' if c['loop'] else '');p=tmp/f'{name}_{f}.png';shot(p,pw,ph_);row.append(pix(p))
  rows.append(np.concatenate(row,1))
 save(np.concatenate(rows[::-1],0),Q/f'clips-sheet-{g_+1}.png')
mate[0].location=(0,0,0);mate[0].rotation_euler=(0,0,0);pose(hero[0],None,1);pose(mate[0],None,1)
lc=G['lod_casts'];figs=[];rigs3=[]
for i,c in enumerate(lc):
 for j,ln in enumerate(LOD[c]):r,objs=LOD[c][ln];r.location=((i*3.4+j*1.05)-(len(lc)*3.4-2.3)/2,0,0);figs+=objs;rigs3.append(r)
for r in rigs3:pose(r,acts['Walk'],1+round(.25*(C['clips']['Walk']['frames']-1)))
show(base+figs);ground.hide_render=True;look((0,-26,5.5),(0,0,.9),lens=85);lab.location=(-.4,.125,-2);lab.data.size=.018;lab.data.body='26 m, Walk pose.  '+'  |  '.join(f'{c} 0/1/2' for c in lc);shot(Q/'lod-crowd.png',1400,520)
rows=[]
for c in lc:
 ts=[]
 for j,ln in enumerate(LOD[c]):r=LOD[c][ln][0];r.location=(j*1.1-1.1,0,0);pose(r,acts['Talk'],1+round(.12*(C['clips']['Talk']['frames']-1)));ts.append(tag(f'{c} {ln}  {lodinfo[c][ln]["triangles"]:,} tris',(j*1.1-1.1,-.2,2.33),.06))
 show(base+[o for ln in LOD[c] for o in LOD[c][ln][1]]+ts);ground.hide_render=True;lab.hide_render=True;look((0,-6.2,1.75),(0,0,1.55),lens=50)
 for t in ts:t.rotation_euler=cam.rotation_euler
 p=tmp/f'close_{c}.png';shot(p,1500,640);rows.append(pix(p))
 for t in ts:bpy.data.objects.remove(t,do_unlink=True)
save(np.concatenate(rows[::-1],0),Q/'lod-closeup.png')
c=lc[0];figs=[]
for i,(clip,u) in enumerate(G['lod_poses']):
 for j,ln in enumerate(['LOD0','LOD2']):
  r0,objs0=LOD[c][ln];r=r0.copy();r.name=P+'Posed';sc.collection.objects.link(r);r.animation_data_clear();r.location=((i*2+j)*1.5-3.75,0,0)
  for o0 in objs0:
   o=o0.copy();o.name=P+'PosedMesh';sc.collection.objects.link(o);o.parent=r;figs.append(o)
   for md in o.modifiers:
    if md.type=='ARMATURE':md.object=r
  pose(r,acts[clip],1+round(u*(C['clips'][clip]['frames']-1)));figs.append(tag(f'{clip} {ln}',r.location+V((0,-.3,2.5)),.15))
show(base+figs);ground.hide_render=True;lab.hide_render=True;look((6,-12,4.5),(0,0,.9),lens=45)
for t in figs:
 if t.type=='FONT':t.rotation_euler=cam.rotation_euler
shot(Q/'lod-posed.png',1800,760)
for o in [o for o in sc.objects if o.name.startswith((P+'Posed',P+'Tag'))]:bpy.data.objects.remove(o,do_unlink=True)
for i,c in enumerate(C['cast']):
 for j,ln in enumerate(LOD[c]):r=LOD[c][ln][0];pose(r,None,1);r.location=(j*1.3-1.3,-i*1.6,0)
mr.location=(-3,0,0);show(list(sc.objects));lab.hide_render=True;stepo.hide_render=True
man={'motion':{'fbx':P+'Motion.fbx','armature':'FestivalRig','fps':C['fps'],'takes':'FBX take name = clip name; NLA strips start at frame 1, exported takes start at 0; last frame of a loop repeats the first pose.','curves':'Bone transforms only (rig-only FBX); no mesh, no shape-key curves.',
 'rootMotion':'None. Locomotion clips are in place: planted feet/hands slide on a treadmill at groundSpeed (m/s) along the facing axis; scale Animator speed by actualSpeed/groundSpeed. DragOther travels backwards. BoardShuttle is a one-shot that ends with Hips displaced (0, +%.2f m forward, +%.2f m up) to stand on the AH03_Shuttle floor; move the root at clip end.'%(C['clips']['BoardShuttle']['fwd'],C['clips']['BoardShuttle']['step']),
 'dragPair':{'note':'Play DragOther and BeingDragged with the same normalized time. Place the BeingDragged root %.2f m in front of the DragOther root, rotated 180 degrees; both face each other and travel toward the dragger\'s back. Wrists meet at dragger-space (±%.2f, %.2f fwd, %.2f up).'%(C['drag']['grip'][1]+C['drag']['friend'],*C['drag']['grip'])},'clips':clipinfo},
 'lods':lodinfo,'poseCheck':posecheck,'notes':['LOD FBXs reuse AH03P_ material names; use ProductionPeople03/palette.json.','LOD1 keeps the unified face mesh and its seven expression keys untouched; body GripL/GripR are rebuilt on the decimated body by barycentric transfer restricted to the dominant deform group.','LOD2 drops all shape keys (neutral face, relaxed grip).','All LOD FBXs export WITHOUT animation with the same FestivalRig hierarchy and bone names, so AH03P_Motion and AH04P_Motion clips bind unchanged.','poseCheck distances are nearest-surface deviations of LODn vertices from the posed LOD0 surface (skinning sanity check).']}
(O/'people-manifest.json').write_text(json.dumps(man,indent=1)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=str(O/'PeopleMotionLOD.blend'))
