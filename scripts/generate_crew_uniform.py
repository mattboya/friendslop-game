CONFIG = {
 'prefix':'AH04W_','source':'ArtSource/Generated/FestivalCharacter.blend','wardrobe':'ArtSource/ProductionPackage03/WardrobeExtension.blend','people':'ArtSource/ProductionPackage03/PeopleUpgrade.blend','out':'ArtSource/ProductionPackage04','exports':'Assets/Festival/Art/ProductionPeople04','renders':'artifacts/production04-crew','fbx':'AH04W_CrewUniform.fbx','blend':'CrewUniform.blend','manifest':'crew-manifest.json','clips':['Idle','Dance','Cheer'],'frames':[1,16,31,46],'fit':{'male':1.04,'female':.96,'widths':[.92,1,1.12],'head_base':.25,'head_step':.008,'head_ref':.258},
 'vest':{'profile':[(.90,.375,.282,-.012),(1.0,.385,.282,-.012),(1.12,.385,.282,-.012),(1.26,.39,.285,-.012),(1.38,.40,.275,-.01),(1.45,.39,.258,-.005),(1.48,.35,.243,0),(1.51,.285,.215,.003),(1.535,.235,.19,.006),(1.56,.20,.175,.01),(1.60,.17,.155,.01),(1.64,.16,.15,.01)],'rows':[.92,.96,1.0,1.04,1.08,1.12,1.16,1.2,1.24,1.28,1.32,1.36,1.4,1.44,1.48,1.51,1.535,1.56],'cols':9,'thick':.012,'clear':.012,'hole':(1.03,1.52,.70,3,1.3,6),'vee':(1.34,.60,.8),'bands':[(.95,1.0),(1.23,1.29)],'lift':.004,'inset':.03,'brace':(.20,.035,1.545,1.30,.19,3,.025),'closure':.009,'field':(96,.88,1.64,.01),'gap':.004,'refine':5,'letters':('CREW',1.07,.14,.09,.045,.03),'tol':.003},
 'pass':{'z':1.195,'size':(.12,.16),'d':(.010,.016),'header':.036,'letters':(.018,.016,.006,.005),'clip':(.012,.022),'ribbon':(.011,.007,.011,.10),'photo':(.036,.044),'sun':.017},'cap':{'rx':.275,'ry':.262,'cy':.012,'front':2.095,'back':2.045,'top':2.31,'thick':.012,'power':.55,'rows':7,'cols':32,'bill':(.95,.20,.035,.014),'button':(.026,.026,.012),'seam':.004,'patch':(.42,.14,.46)},
 'colors':{'HiVis':((.72,.93,.02),.55),'Reflective':((.66,.69,.71),.28),'Ink':((.026,.042,.055),.72),'Magenta':((.70,.06,.32),.6),'White':((.93,.91,.84),.5),'Dark':((.035,.04,.038),.6),'Ochre':((.83,.50,.13),.6),'Teal':((.055,.29,.28),.72)},'render':{'size':(2400,860),'samples':48,'spacing':1.3,'ortho':8.2,'cam_z':1.2,'world':(.14,.19,.25),'lights':[('Key',(-4,-7,8),1900,6),('Fill',(6,-5,4),900,6),('Rim',(0,7,6),1100,5)],'cast':[(0,0,0,0,1,0,2,1,'SkinWarm','Coral','HairDark'),(1,0,3,1,2,1,1,0,'SkinLight','Teal','HairPurple'),(0,1,1,3,0,2,0,1,'SkinDeep','Ink','HairDark'),(1,1,4,2,1,0,3,0,'SkinWarm','Coral','HairGrey'),(0,2,2,1,1,1,3,1,'SkinLight','Teal','HairGrey'),(1,2,5,0,3,2,1,1,'SkinDeep','Ochre','HairDark')],'posed':[(0,'Dance',46),(3,'Dance',20),(4,'Cheer',16)],'pframe':30,'palette':{'SkinWarm':(.62,.32,.19),'SkinDeep':(.27,.115,.07),'SkinLight':(.86,.60,.39),'Coral':(.72,.20,.15),'Teal':(.055,.29,.28),'Ochre':(.83,.50,.13),'Cream':(.88,.78,.57),'Ink':(.026,.042,.055),'HairDark':(.055,.037,.025),'HairGrey':(.29,.30,.29),'HairPurple':(.24,.10,.25),'Ground':(.16,.20,.17)}}}
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=Path(__file__).resolve().parents[1];P=CONFIG['prefix'];O=R/CONFIG['out'];E=R/CONFIG['exports'];A=R/CONFIG['renders'];V=CONFIG['vest'];C=CONFIG['cap'];Q=CONFIG['pass'];G=CONFIG['render'];PI=math.pi;TAU=math.tau;TH=V['thick']
for d in (O,E,A):d.mkdir(parents=True,exist_ok=True)
for c in (bpy.data.objects,bpy.data.meshes,bpy.data.materials,bpy.data.cameras,bpy.data.lights,bpy.data.worlds):
 for x in [x for x in c if x.name.startswith(P)]:c.remove(x)
scene=bpy.data.scenes.get(P+'Crew') or bpy.data.scenes.new(P+'Crew');bpy.context.window.scene=scene;bpy.context.preferences.filepaths.save_version=0;M={}

def material(n,c,r):
 m=bpy.data.materials.new(P+n);m.diffuse_color=(*c,1);m.use_nodes=True;b=m.node_tree.nodes['Principled BSDF'];b.inputs['Base Color'].default_value=(*c,1);b.inputs['Roughness'].default_value=r;M[n]=m

for n,(c,r) in CONFIG['colors'].items():material(n,c,r)
MAT=list(M)
for n,c in G['palette'].items():material('R_'+n,c,.7)

def load(path,names):
 with bpy.data.libraries.load(str(R/path),link=False) as (a,b):b.objects=names
 for o in b.objects:scene.collection.objects.link(o);o.name=P+'Ref_'+o.name.split('.')[0].replace('AH03W_','')
 return b.objects

S=lambda c,r:[f'{c}_{i}' for i in r]
objs=load(CONFIG['source'],['FestivalRig','HairUnderHat','Equipment_LittleSpoon']+[f'Body_{g}_{b}' for g in range(2) for b in range(3)]+[f'Face_{g}_{v}' for g in range(2) for v in range(6)]+[n for c in ('Shirt','Pants','Shoes','Hairstyle','HairTop','Sunglasses','FacialHair') for n in S(c,range(4))]+S('Headgear',range(2)))+load(CONFIG['wardrobe'],['AH03W_'+n for c in ('Shirt','Hairstyle','Sunglasses','FacialHair') for n in S(c,range(4,10))])
ref={o.name[len(P)+4:]:o for o in objs};rig=ref.pop('FestivalRig');rig.name=P+'Rig';rig.animation_data_clear()
for b in rig.pose.bones:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0)
for o in ref.values():
 o.parent=rig;o.hide_render=True
 for md in o.modifiers:
  if md.type=='ARMATURE':md.object=rig
 if o.data.shape_keys:
  o.data.shape_keys.animation_data_clear()
  for k in o.data.shape_keys.key_blocks:k.value=0
with bpy.data.libraries.load(str(R/CONFIG['people']),link=False) as (a,b):b.actions=['AH03P_'+c for c in CONFIG['clips']]
ACT=dict(zip(CONFIG['clips'],b.actions))

def dom(o):return [o.vertex_groups[max(v.groups,key=lambda g:g.weight).group].name if v.groups else '' for v in o.data.vertices]

def isl(me):
 p=list(range(len(me.vertices)))
 def f(i):
  while p[i]!=i:p[i]=p[p[i]];i=p[i]
  return i
 for e in me.edges:p[f(e.vertices[0])]=f(e.vertices[1])
 return [f(i) for i in range(len(p))]

def torso(o):
 d=dom(o);r=isl(o.data);cen={}
 for v,k,g in zip(o.data.vertices,r,d):s=cen.setdefault(k,[Vector(),0,g]);s[0]+=v.co;s[1]+=1
 hood={k for k,(s,n,g) in cen.items() if s.y/n>.04 and s.z/n>1.45};tape={k for k,(s,n,g) in cen.items() if g.startswith('Arm') and abs(s.x/n)<.34 and s.z/n>1.38}
 return [g=='Spine' and k not in hood for g,k in zip(d,r)],[k in tape for k in r]

SH=S('Shirt',range(10));TT={k:torso(ref[k]) for k in SH};TM={k:v[0] for k,v in TT.items()};SP={k:[g=='Spine' for g in dom(ref[k])] for k in SH};BM={k:[g=='Spine' for g in dom(o)] for k,o in ref.items() if k.startswith('Body_')};ARM={k:[g.startswith(('Arm','Forearm','Hand')) for g in dom(o)] for k,o in ref.items() if k.startswith(('Body_','Shirt_'))}
F=CONFIG['fit'];FITS=[{'name':'Basis','b':1.0,'h':1.0,'body':'1_1','key':'Basis'}]+[{'name':f'Fit_{g}_{b}','b':(F['male'] if g==0 else F['female'])*F['widths'][b],'h':(F['head_base']+b*F['head_step'])/F['head_ref'],'body':f'{g}_{b}','key':f'Fit_{g}_{b}'} for g in range(2) for b in range(3)]

def co(o,key=None):
 ks=o.data.shape_keys;src=ks.key_blocks[key].data if key and ks and key in ks.key_blocks else o.data.vertices
 return [o.matrix_world@v.co for v in src]

def tree(parts):
 pts=[];tris=[]
 for pc,o,mask in parts:n=len(pts);pts+=pc;tris+=[[n+i for i in p.vertices] for p in o.data.polygons if mask is None or all(mask[i] for i in p.vertices)]
 return BVHTree.FromPolygons(pts,tris)

def whole(k,f):return tree([(co(ref[k],f['key']),ref[k],None)])
def under(f,shirts=SH,tape=False):return [(co(ref['Body_'+f['body']]),ref['Body_'+f['body']],BM['Body_'+f['body']])]+[(co(ref[k],f['key']),ref[k],[a or (b and tape) for a,b in zip(*TT[k])]) for k in shirts]

def prof(z):
 pr=V['profile'];z=min(max(z,pr[0][0]),pr[-1][0])
 for a,b in zip(pr,pr[1:]):
  if z<=b[0]:u=(z-a[0])/(b[0]-a[0]);return [a[i]+(b[i]-a[i])*u for i in (1,2,3)]

def ell(t,z,b):rx,ry,cy=prof(z);return Vector((rx*b*math.cos(t),cy+ry*math.sin(t),z))
NT,Z0,Z1,DZ=V['field'];NZ=round((Z1-Z0)/DZ)+1

def field(f):
 t3=tree(under(f,SH,True));raw=[]
 for m in range(NZ):
  z=Z0+m*DZ;c=Vector((0,prof(z)[2],z));row=[]
  for k in range(NT):
   p=ell(k*TAU/NT,z,f['b']);d=(p-c).normalized();h=t3.ray_cast(c+d*1.5,-d,1.5)[0]
   row.append(max(0,(h-c).length+V['clear']-(p-c).length) if h is not None else 0)
  raw.append(row)
 f['raw']=raw;f['D']=smooth(raw)

def smooth(raw):
 D=[[max(raw[j][(k+q)%NT] for j in range(max(0,m-3),min(NZ,m+4)) for q in range(-3,4)) for k in range(NT)] for m in range(NZ)]
 for _ in range(3):D=[[sum(D[j][(k+q)%NT] for j in range(max(0,m-1),min(NZ,m+2)) for q in (-1,0,1))/(3*(min(NZ,m+2)-max(0,m-1))) for k in range(NT)] for m in range(NZ)]
 return D

def delta(D,t,z):
 x=(t%TAU)/TAU*NT;k=int(x);fx=x-k;k%=NT;y=min(max((z-Z0)/DZ,0),NZ-1.0001);m=int(y);fy=y-m
 return (D[m][k]*(1-fx)+D[m][(k+1)%NT]*fx)*(1-fy)+(D[m+1][k]*(1-fx)+D[m+1][(k+1)%NT]*fx)*fy

def surf(t,z,f):p=ell(t,z,f['b']);c=Vector((0,prof(z)[2],z));return p+(p-c).normalized()*delta(f['D'],t,z)
def normal(t,z,f,e=.001):return (surf(t+e,z,f)-surf(t-e,z,f)).cross(surf(t,z+e,f)-surf(t,z-e,f)).normalized()
def pos(t,z,d,f):return surf(t,z,f)+normal(t,z,f)*d
def inv(q,b=1):rx,ry,cy=prof(q.z);return (math.atan2((q.y-cy)/ry,q.x/(rx*b)),q.z)
def axis(p):c=Vector((0,prof(p.z)[2],p.z));d=Vector((p.x,p.y-c.y,0));r=d.length;return c,d/r,r

def hole(z,front=False):
 lo,hi,W,p,s,pf=V['hole'];p=pf if front else p;t=abs(z-(lo+hi)/2)/((hi-lo)/2);return W*(s if front else 1)*(1-t**p)**(1/p) if t<1 else 0

def vee(z):zv,W,p=V['vee'];return W*((z-zv)/(V['rows'][-1]-zv))**p if z>zv else 0

def panels(zs,K,inset=0):
 pts=[];key={};rows=[]
 for i,z in enumerate(zs):
  a=hole(z);f=hole(z,True);b=vee(z);a,f,b=[x+inset if x else 0 for x in (a,f,b)];row=[]
  for t in [-PI/2+b+(PI/2-b-f)*j/K for j in range(K+1)]+[a+(PI-2*a)*j/(2*K) for j in range(2*K+1)]+[PI+f+(PI/2-f-b)*j/K for j in range(K+1)]:
   k=(i,round(t%TAU,6))
   if k not in key:key[k]=len(pts);pts.append((t,z))
   row.append(key[k])
  rows.append(row)
 segs=[(j,j+1) for j in range(K)]+[(K+1+j,K+2+j) for j in range(2*K)]+[(3*K+2+j,3*K+3+j) for j in range(K)]
 return pts,[(r0[a],r0[b],r1[b],r1[a]) for r0,r1 in zip(rows,rows[1:]) for a,b in segs]

def grid(rows):
 k=len(rows[0]);return [p for r in rows for p in r],[(i*k+j,i*k+j+1,(i+1)*k+j+1,(i+1)*k+j) for i in range(len(rows)-1) for j in range(k-1)]

def add(T,f,m,s):T['F'].append(tuple(f));T['M'].append(MAT.index(m));T['S'].append(s)

def solid(T,a,b,faces,m,rim='',s=True):
 n=len(T['V']);k=len(a);T['V']+=a+b;ed=set()
 for f in faces:ed.update(zip(f,f[1:]+f[:1]))
 for f in faces:
  add(T,[n+i for i in f[::-1]],m,s);add(T,[n+k+i for i in f],m,s)
  for x,y in zip(f,f[1:]+f[:1]):
   if (y,x) not in ed:add(T,[n+k+y,n+k+x,n+x,n+y],rim or m,False)

VT={'V':[],'F':[],'M':[],'S':[]};CT={'V':[],'F':[],'M':[],'S':[]};B0=FITS[0]
def layer(pts,faces,d0,d1,m,rim='',s=True):solid(VT,[(t,z,d0) for t,z in pts],[(t,z,d1) for t,z in pts],faces,m,rim,s)

def ribbon(path,hw,d0,d1,m,s=True,n=1):
 rows=[]
 for i,(t,z) in enumerate(path):
  a,b=path[max(i-1,0)],path[min(i+1,len(path)-1)];c=surf(t,z,B0);w=normal(t,z,B0).cross(surf(*b,B0)-surf(*a,B0)).normalized()*hw;rows.append([inv(c+w*(1-2*j/n)) for j in range(n+1)])
 layer(*grid(rows),d0,d1,m,'',s)

def side(v,front):c=math.acos(max(-1,min(1,(v.x if front else -v.x)/prof(v.y)[0])));return (-c if front else c,v.y)
GLYPH={'C':[],'R':[((0,0),(0,1)),((0,1),(1,1)),((1,1),(1,.5)),((1,.5),(0,.5)),((.4,.5),(1,0))],'E':[((0,0),(0,1)),((0,1),(1,1)),((0,.5),(.8,.5)),((0,0),(1,0))],'W':[((0,1),(.22,0),1,0),((.22,0),(.5,.6),0,0),((.5,.6),(.78,0),0,0),((.78,0),(1,1),0,1)]}

def emboss(text,z0,h,lw,gap,wd,d0,d1,m,front):
 W=len(text)*lw+(len(text)-1)*gap;up=d1
 for i,ch in enumerate(text):
  x0=-W/2+i*(lw+gap)
  if ch=='C':layer(*grid([[side(Vector((x0+lw/2+(lw/2+s*wd/2)*math.cos(a),z0+h/2+(h/2+s*wd/2)*math.sin(a))),front) for s in (1,-1)] for a in [math.radians(40+280*j/14) for j in range(15)]]),d0,up,m,'',False)
  for a,b,*e in GLYPH[ch]:
   up+=.0005;ea,eb=e or (1,1)
   p=Vector((x0+a[0]*lw,z0+a[1]*h));q=Vector((x0+b[0]*lw,z0+b[1]*h));u=(q-p).normalized();p-=u*wd/2*ea;q+=u*wd/2*eb
   if not eb:layer([side(q,front)]+[side(q+Vector((math.cos(TAU*j/10),math.sin(TAU*j/10)))*wd/2,front) for j in range(10)],[(0,1+j,1+(j+1)%10) for j in range(10)],d0,up+.0002,m,'',False)
   nv=Vector((-u.y,u.x))*wd/2;k=max(1,math.ceil((q-p).length/.02))
   layer(*grid([[side(p+(q-p)*j/k+nv*s,front) for s in (1,-1)] for j in range(k+1)]),d0,up,m,'',False)

def rect(x0,x1,z0,z1,d0,d1,m,nx=2,nz=2):layer(*grid([[side(Vector((x0+(x1-x0)*i/nx,z0+(z1-z0)*j/nz)),True) for i in range(nx+1)] for j in range(nz+1)]),d0,d1,m)
def disc(x,z,r,d0,d1,m,k=12):layer([side(Vector((x,z)),True)]+[side(Vector((x+r*math.cos(TAU*i/k),z+r*math.sin(TAU*i/k))),True) for i in range(k)],[(0,1+i,1+(i+1)%k) for i in range(k)],d0,d1,m)

for f in FITS:field(f)
K=V['cols'];layer(*panels(V['rows'],K),0,TH,'HiVis','Ink');SHELL=range(len(VT['F']));NS=len(VT['V'])
def hull(f):return BVHTree.FromPolygons([pos(t,z,d,f) for t,z,d in VT['V'][:NS]],[VT['F'][i] for i in SHELL])

def refine(f):
 lo,hi=V['rows'][0]+.005,V['rows'][-1]-.005
 for f['passes'] in range(1,V['refine']+1):
  sh=hull(f);bad=0
  for pc,o,mask in under(f):
   for p,mk in zip(pc,mask):
    if not mk or not lo<p.z<hi:continue
    c,d,r=axis(p);h,_,fi,_=sh.ray_cast(c,d,2)
    if h is None or r-(h-c).length+V['gap']<=0:continue
    bad+=1
    for t,z,_ in (VT['V'][i] for i in VT['F'][fi]):k=round((t%TAU)/TAU*NT)%NT;m=min(NZ-1,max(0,round((z-Z0)/DZ)));f['raw'][m][k]=max(f['raw'][m][k],delta(f['D'],t,z)+r-(h-c).length+V['gap'])
  if not bad:break
  f['D']=smooth(f['raw'])
 f['push']=round(max(max(r) for r in f['raw']),4)

for f in FITS:refine(f)
for z0,z1 in V['bands']:layer(*panels([z0+(z1-z0)*i/4 for i in range(5)],2*K,V['inset']),TH-.001,TH+V['lift'],'Reflective')
bx,bw,bz,bb,bm,bn,bs=V['brace']
def fr(z):lo,hi=-PI/2+vee(z)+bm,-hole(z,True)-bm;return min(max(-math.acos(min(1,bx/prof(z)[0])),lo),hi) if lo<=hi else (lo+hi)/2
zs=sorted({round(z,4) for a,b in zip(V['rows'],V['rows'][1:]) for z in (a,(a+b)/2) if z<1.47}|{V['rows'][0]+.004});ta=fr(1.49)
path=[(fr(z),z) for z in zs]+[(ta*s,1.49+(bz-1.49)*(1-s**4)) for s in (1,.85,.7,.5,.25,0,-.25,-.5,-.7,-.85,-1)]+[(-fr(z),z) for z in reversed(zs) if z>=bb]
for pth in (path,[(PI-t,z) for t,z in path]):ribbon(pth,bw,TH-.001,TH+V['lift']+.001,'Reflective',True,bn)
ribbon([(-PI/2,V['rows'][0]+.004+.04*i) for i in range(11)],V['closure'],TH-.001,TH+.003,'Dark')
lt,lz,lh,lw,lg,lwd=V['letters'];emboss(lt,lz,lh,lw,lg,lwd,TH-.001,TH+.006,'Ink',False)
zc=Q['z'];cw,ch=Q['size'];d0,d1=Q['d'];top=zc+ch/2;hd=Q['header'];hb=top-.006-hd;cx,cl=Q['clip'];rw,ra,rb,rm=Q['ribbon'];pw,ph=Q['photo']
rect(-cw/2,cw/2,zc-ch/2,top,TH+d0,TH+d1,'White',4,4);rect(-cw/2+.006,cw/2-.006,hb,top-.006,TH+d1-.001,TH+d1+.0025,'HiVis',4,1)
a,b,c,e=Q['letters'];emboss('CREW',hb+hd/2-a/2,a,b,c,e,TH+d1+.001,TH+d1+.004,'Ink',True)
rect(-cw/2+.01,-cw/2+.01+pw,hb-.01-ph,hb-.01,TH+d1-.001,TH+d1+.0025,'Teal',1,1);disc(cw/2-.03,hb-.01-ph/2,Q['sun'],TH+d1-.001,TH+d1+.0025,'Ochre');disc(cw/2-.023,hb-.006-ph/2,Q['sun']*.75,TH+d1+.002,TH+d1+.0035,'White')
for j in range(3):rect(-cw/2+.012,cw/2-.012-j*.02,zc-ch/2+.012+j*.011,zc-ch/2+.017+j*.011,TH+d1-.001,TH+d1+.0025,'Ink',2,1)
rect(-cx,cx,top-.006,top+cl,TH+d0-.002,TH+d1+.004,'Dark',1,2)
right=[(PI/2,1.547),(1.1,1.547),(.6,1.546),(.2,1.546),(-.2,1.543),(-.5,1.535)]+[(-PI/2+vee(z)+rm,z) for z in (1.50,1.46,1.42,1.38)]+[(-PI/2+.10,1.345),(-PI/2+.05,top+cl+.02),(-PI/2+.02,top+cl)]
ribbon([(PI-t,z) for t,z in reversed(right)]+right[1:],rw,TH+ra,TH+rb,'Magenta')

def zband(t):return (C['front']+C['back'])/2+(C['back']-C['front'])/2*math.sin(t)
def dome(t,f):c=max(math.cos(f),0)**C['power'];zb=zband(t);return Vector((C['rx']*c*math.cos(t),C['cy']+C['ry']*c*math.sin(t),zb+(C['top']-zb)*math.sin(f)))
def crown(t,f,d,e=.001):return dome(t,f)+(dome(t+e,f)-dome(t-e,f)).cross(dome(t,f+e)-dome(t,f-e)).normalized()*d

def tube(T,pts,r,m,k=4):
 n=len(T['V'])
 for i,p in enumerate(pts):
  tg=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized();u=tg.cross(Vector((0,0,1)));u=(u if u.length>.01 else tg.cross(Vector((0,1,0)))).normalized();w=tg.cross(u);T['V']+=[p+r*(u*math.cos(j*TAU/k)+w*math.sin(j*TAU/k)) for j in range(k)]
 for i in range(len(pts)-1):
  for j in range(k):add(T,(n+i*k+j,n+i*k+(j+1)%k,n+(i+1)*k+(j+1)%k,n+(i+1)*k+j),m,True)
 add(T,[n+j for j in range(k)][::-1],m,False);add(T,[n+(len(pts)-1)*k+j for j in range(k)],m,False)

def orb(T,c,r,m,k=10,n=5):
 b=len(T['V']);T['V']+=[c+Vector((r[0]*math.sin(PI*i/n)*math.cos(TAU*j/k),r[1]*math.sin(PI*i/n)*math.sin(TAU*j/k),r[2]*math.cos(PI*i/n))) for i in range(1,n) for j in range(k)]+[c+Vector((0,0,r[2])),c-Vector((0,0,r[2]))];t=b+(n-1)*k
 for i in range(n-2):
  for j in range(k):add(T,(b+i*k+j,b+i*k+(j+1)%k,b+(i+1)*k+(j+1)%k,b+(i+1)*k+j),m,True)
 for j in range(k):add(T,(t,b+(j+1)%k,b+j),m,True);add(T,(t+1,b+(n-2)*k+j,b+(n-2)*k+(j+1)%k),m,True)

nr,nc=C['rows'],C['cols'];pts=[(TAU*j/nc,i/(nr-1)*PI/2*.96) for i in range(nr) for j in range(nc)];tip=Vector((0,C['cy'],C['top']))
faces=[(i*nc+j,i*nc+(j+1)%nc,(i+1)*nc+(j+1)%nc,(i+1)*nc+j) for i in range(nr-1) for j in range(nc)]+[((nr-1)*nc+j,(nr-1)*nc+(j+1)%nc,nr*nc) for j in range(nc)]
solid(CT,[crown(t,f,0) for t,f in pts]+[tip],[crown(t,f,C['thick']) for t,f in pts]+[tip+Vector((0,0,C['thick']))],faces,'HiVis','Ink')
sp,L,dr,bt=C['bill'];rows=[]
for i in range(5):
 r=-.1+1.1*i/4;row=[]
 for j in range(11):
  u=j/5-1;t=-PI/2+sp*u;o=crown(t,0,C['thick']/2);d=Vector((math.cos(t)*.45,math.sin(t),0)).normalized();p=o+d*L*(1-.5*u*u)*r;p.z=C['front']+.008-dr*max(r,0)**2-.015*u*u*max(r,0);row.append(p)
 rows.append(row)
pts,fs=grid(rows);solid(CT,[p-Vector((0,0,bt/2)) for p in pts],[p+Vector((0,0,bt/2)) for p in pts],fs,'Ink')
orb(CT,tip+Vector((0,0,C['thick'])),C['button'],'Ink')
for k in range(6):tube(CT,[crown(k*TAU/6,.03+1.42*i/7,C['thick']+.001) for i in range(8)],C['seam'],'Ink')
pa,f0,f1=C['patch'];pts,fs=grid([[(-PI/2+pa*(i/2-1),f0+(f1-f0)*j/2) for i in range(5)] for j in range(3)]);solid(CT,[crown(t,f,C['thick']-.001) for t,f in pts],[crown(t,f,C['thick']+.003) for t,f in pts],fs,'Reflective')

def make(name,T,base,keys,bone,slot):
 me=bpy.data.meshes.new(name);me.from_pydata(base,[],T['F']);bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(me);bm.free()
 for n in MAT:me.materials.append(M[n])
 uv=me.uv_layers.new(name='UVMap')
 for p,m,s in zip(me.polygons,T['M'],T['S']):
  p.material_index=m;p.use_smooth=s
  for l in p.loop_indices:uv.data[l].uv=((m+.5)/8,.5)
 o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);o.parent=rig;o.vertex_groups.new(name=bone).add(list(range(len(base))),1,'REPLACE');o.modifiers.new('FestivalRig','ARMATURE').object=rig;o['wardrobe_slot']=slot;o.shape_key_add(name='Basis')
 for n,c in keys.items():o.shape_key_add(name=n,from_mix=False).data.foreach_set('co',[x for v in c for x in v])
 return o

def place(f):
 out=[pos(t,z,d,f) for t,z,d in VT['V'][:NS]];sh=BVHTree.FromPolygons(out,[VT['F'][i] for i in SHELL])
 for t,z,d in VT['V'][NS:]:s=surf(t,z,f);n=normal(t,z,f);h=sh.ray_cast(s+n*.06,-n,.12)[0];out.append(s+n*d if h is None else h+n*(d-TH))
 return out
VC={f['name']:place(f) for f in FITS};CK={f['name']:[Vector((v.x*f['h'],v.y,v.z)) for v in CT['V']] for f in FITS}
vest=make(P+'Role_Crew',VT,VC['Basis'],{f['name']:VC[f['name']] for f in FITS[1:]},'Spine','Role_Crew');cap=make(P+'Headgear_CrewCap',CT,CT['V'],{f['name']:CK[f['name']] for f in FITS[1:]},'Head','Headgear_CrewCap');cap['hair_companion']='HairUnderHat';vest.hide_render=cap.hide_render=True

def over(a,b):return len(a.overlap(b))
def shell(f):return BVHTree.FromPolygons(VC[f['name']],[VT['F'][i] for i in SHELL])

def clearance(f):
 sh=shell(f);lo,hi=V['rows'][0]+.005,V['rows'][-1]-.005;w=-1;src='';n=out=0
 for pc,o,mask in under(f):
  for p,mk in zip(pc,mask):
   if not mk or not lo<p.z<hi:continue
   c,d,r=axis(p);h=sh.ray_cast(c,d,2)[0]
   if h is None:continue
   pen=r-(h-c).length;ho=sh.ray_cast(c+d*2,-d,2)[0];n+=pen>V['tol'];out+=r>(ho-c).length
   if pen>w:w,src=pen,o.name[len(P)+4:]
 tp=[0,0]
 for k in SH:
  for p,tk in zip(co(ref[k],f['key']),TT[k][1]):
   c,d,r=axis(p);h=sh.ray_cast(c,d,2)[0] if tk else None
   if h is not None and r>(h-c).length:tp=[max(tp[0],r-(h-c).length),tp[1]+(r>(sh.ray_cast(c+d*2,-d,2)[0]-c).length)]
 vt=BVHTree.FromPolygons(VC[f['name']],VT['F'])
 return {'worstPenetration_m':round(w,4),'worstSource':src,'verticesOverTolerance':n,'verticesThroughOuterSurface':out,'pass':w<=V['tol'],'shoulderTapeDepthPastInner_m':round(tp[0],4),'shoulderTapeVerticesThroughOuter':tp[1],'pushOut_m':f['push'],'refinePasses':f['passes'],'overlaps':{k:over(vt,whole(k,f)) for k in S('FacialHair',range(10))+['Equipment_LittleSpoon'] if over(vt,whole(k,f))}}

def capcheck(f):
 ct=BVHTree.FromPolygons(CK[f['name']],CT['F']);b=ref['Body_'+f['body']];hs={i:whole(f'Hairstyle_{i}',f) for i in range(10)}
 return {'HairUnderHat':over(ct,whole('HairUnderHat',f)),'HeadSkin':over(ct,tree([(co(b),b,[g=='Head' for g in dom(b)])])),'Faces':sum(over(ct,whole(f'Face_{f["body"][0]}_{v}',f)) for v in range(6)),'SunglassesTouching':[i for i in range(10) if over(ct,whole(f'Sunglasses_{i}',f))],'HairstylesPiercing':[i for i in hs if over(ct,hs[i])],'bucketHairstylesPiercing':[i for i in hs if over(whole('Headgear_0',f),hs[i])],'beanieHairstylesPiercing':[i for i in hs if over(whole('Headgear_1',f),hs[i])]}

def posecheck(shirt='Shirt_3'):
 res={};rig.animation_data_create();lo,hi=V['rows'][0],V['rows'][-1]
 for clip in ['Rest']+CONFIG['clips']:
  if clip!='Rest':rig.animation_data.action=ACT[clip];rig.animation_data.action_slot=ACT[clip].slots[0]
  for f in FITS[1:]:
   sh=shell(f);base=tree([(co(ref['Body_'+f['body']]),ref['Body_'+f['body']],BM['Body_'+f['body']]),(co(ref[shirt],f['key']),ref[shirt],SP[shirt])]);body=ref['Body_'+f['body']];wv=cnt=bw=bc=ov=0
   for k in ref[shirt].data.shape_keys.key_blocks:k.value=1.0 if k.name==f['key'] else 0
   for fr in ([1] if clip=='Rest' else CONFIG['frames']):
    scene.frame_set(fr);dg=bpy.context.evaluated_depsgraph_get();Mi=(rig.matrix_world@rig.pose.bones['Spine'].matrix@rig.data.bones['Spine'].matrix_local.inverted()).inverted()
    pts=[];tris=[]
    for o in (body,ref[shirt]):
     ev=o.evaluated_get(dg);me=ev.to_mesh();am=ARM[o.name[len(P)+4:]];tp=TT[shirt][1] if o!=body else [False]*len(am);n=len(pts);pts+=[Mi@(ev.matrix_world@v.co) for v in me.vertices];tris+=[[n+i for i in q.vertices] for q in me.polygons if all(am[i] for i in q.vertices)]
     for p,mk,tk in zip(pts[n:],am,tp):
      if not mk or tk or not lo<p.z<hi:continue
      c,d,r=axis(p)
      if sh.ray_cast(c,d,2)[0] is None:continue
      ro=(sh.ray_cast(c+d*2,-d,2)[0]-c).length;hs=base.ray_cast(c+d*2,-d,2)[0];rs=(hs-c).length if hs is not None else 0
      if r<rs:bc+=1;bw=max(bw,rs-r)
      elif r<ro:cnt+=1;wv=max(wv,ro-r)
     ev.to_mesh_clear()
    ov=max(ov,len(sh.overlap(BVHTree.FromPolygons(pts,tris))))
   res.setdefault(clip,{})[f['name']]={'armVestTriangleOverlaps':ov,'armVerticesUnderVestOnly':cnt,'vestClipDepth_m':round(wv,4),'armVerticesAlreadyInsideShirt':bc,'shirtClipDepth_m':round(bw,4)}
 rig.animation_data.action=None
 for b in rig.pose.bones:b.rotation_euler=(0,0,0)
 for k in ref[shirt].data.shape_keys.key_blocks:k.value=0
 scene.frame_set(1);return res

CL={f['name']:clearance(f) for f in FITS[1:]};CP={f['name']:capcheck(f) for f in FITS[1:]};PC=posecheck()
bpy.ops.object.select_all(action='DESELECT')
for o in (rig,vest,cap):o.select_set(True)
bpy.context.view_layer.objects.active=rig;rig.name='FestivalRig';bpy.ops.export_scene.fbx(filepath=str(E/CONFIG['fbx']),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y');rig.name=P+'Rig'
tri=lambda o:sum(len(p.vertices)-2 for p in o.data.polygons)
(O/CONFIG['manifest']).write_text(json.dumps({'fbx':str((E/CONFIG['fbx']).relative_to(R)),'source':str((O/CONFIG['blend']).relative_to(R)),'generator':'scripts/generate_crew_uniform.py','rig':'FestivalRig (shared 15-bone master skeleton; bones '+', '.join(b.name for b in rig.data.bones)+')','meshes':{o.name:{'slot':o['wardrobe_slot'],'bone':o.vertex_groups[0].name,'triangles':tri(o),'vertices':len(o.data.vertices),'materials':[m.name for m in o.data.materials],'shapeKeys':[k.name for k in o.data.shape_keys.key_blocks]} for o in (vest,cap)},'trianglesTotal':tri(vest)+tri(cap),'hatRule':{'Headgear_CrewCap':'opaque: hide HairTop_*, show HairUnderHat plus the Hairstyle lower locks'},'fits':[f['name'] for f in FITS[1:]],'fitWidths':{f['name']:round(f['b'],4) for f in FITS[1:]},'palette':{'entries':[{'name':P+n,'rgb':list(c),'roughness':r} for n,(c,r) in CONFIG['colors'].items()]},'torsoClearance':CL,'capFit':CP,'poseCheck':{'shirt':'Shirt_3','frames':CONFIG['frames'],'results':PC},'notes':['Vest, lanyard and pass are one Spine-weighted renderer like the existing Role_* layers; the cap is one Head-weighted Headgear renderer.','Vest Fit keys are measured: x-scaled by the body width factor, then pushed out radially to clear the union of the fitted body torso and all ten Shirt slots (hoodie hood excluded; it lies over the vest collar).','Cap Fit keys follow the Headgear convention (x-scale by head factor).','Fictional CREW print and interlocking sun/moon pass mark; no real brand or agency marks.']},indent=1)+'\n')

def inst(src,key,r,pal,name):
 o=src.copy();o.data=src.data.copy();o.name=o.data.name=P+'R_'+name;scene.collection.objects.link(o);o.parent=r;o.hide_render=False
 for md in o.modifiers:
  if md.type=='ARMATURE':md.object=r
 for k in o.data.shape_keys.key_blocks if o.data.shape_keys else []:k.value=1.0 if k.name==key else 0
 if pal:
  idx=[p.material_index if len(o.data.materials)==8 else min(7,int(o.data.uv_layers.active.data[p.loop_indices[0]].uv.x*8)) for p in o.data.polygons];o.data.materials.clear()
  for c in pal:o.data.materials.append(M['R_'+c])
  for p,i in zip(o.data.polygons,idx):p.material_index=i
 return o

scene.render.engine='CYCLES';scene.cycles.samples=G['samples'];scene.cycles.use_denoising=True;pr=bpy.context.preferences.addons['cycles'].preferences;pr.compute_device_type='METAL';pr.get_devices()
for d in pr.devices:d.use=True
scene.cycles.device='GPU';scene.view_settings.view_transform='AgX';scene.render.resolution_x,scene.render.resolution_y=G['size'];scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new(P+'World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(*G['world'],1)
me=bpy.data.meshes.new(P+'Ground');me.from_pydata([(-12,-6,0),(12,-6,0),(12,8,0),(-12,8,0)],[],[(0,1,2,3)]);me.materials.append(M['R_Ground']);scene.collection.objects.link(bpy.data.objects.new(P+'Ground',me))
for n,loc,en,sz in G['lights']:
 d=bpy.data.lights.new(P+n,'AREA');d.energy=en;d.size=sz;o=bpy.data.objects.new(P+n,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1.2))-o.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new(P+'Camera',bpy.data.cameras.new(P+'Camera'));scene.collection.objects.link(cam);scene.camera=cam;chars=[]
for i,(g,b,fa,sh,pa,so,ha,cp,sk,tp,hc) in enumerate(G['cast']):
 key=f'Fit_{g}_{b}';r=rig.copy();r.name=P+f'R_Rig{i}';scene.collection.objects.link(r);r.location=((i-(len(G['cast'])-1)/2)*G['spacing'],0,0);r.animation_data_clear();pal=[sk,tp,'Ink','Teal','Cream','Ink',hc,'Ink']
 chars.append((r,[inst(ref[k],key,r,pal,f'{i}_{k}') for k in [f'Body_{g}_{b}',f'Face_{g}_{fa}',f'Shirt_{sh}',f'Pants_{pa}',f'Shoes_{so}',f'Hairstyle_{ha}',('HairUnderHat' if cp else f'HairTop_{ha}')]]+[inst(vest,key,r,None,f'{i}_Vest')]+([inst(cap,key,r,None,f'{i}_Cap')] if cp else [])))
cam.data.type='ORTHO';cam.data.ortho_scale=G['ortho'];cam.location=(0,-12,G['cam_z']);cam.rotation_euler=(PI/2,0,0)
bpy.ops.wm.save_as_mainfile(filepath=str(O/CONFIG['blend']))
for view,ang in (('front',0),('side',PI/2),('back',PI)):
 for r,_ in chars:r.rotation_euler.z=ang
 scene.render.filepath=str(A/f'crew-{view}.png');bpy.ops.render.render(write_still=True)
posed={i:(clip,f) for i,clip,f in G['posed']}
for i,(r,parts) in enumerate(chars):
 r.rotation_euler.z=0
 for o in parts:o.hide_render=i not in posed
 if i in posed:
  r.location.x=(list(posed).index(i)-1)*1.5;r.animation_data_create();st=r.animation_data.nla_tracks.new().strips.new(posed[i][0],G['pframe']-posed[i][1]+1,ACT[posed[i][0]]);st.action_slot=ACT[posed[i][0]].slots[0]
scene.frame_set(G['pframe']);cam.data.type='PERSP';cam.data.lens=42;cam.location=(3.4,-7.2,2.7);cam.rotation_euler=(Vector((0,0,1.2))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.resolution_x,scene.render.resolution_y=1800,1200;scene.render.filepath=str(A/'crew-posed.png');bpy.ops.render.render(write_still=True)
for i,(r,parts) in enumerate(chars):
 r.animation_data_clear();r.location.x=0
 for o in parts:o.hide_render=i!=2
for b in chars[2][0].pose.bones:b.rotation_euler=(0,0,0)
scene.frame_set(1);cam.data.lens=85;cam.location=(1.1,-2.6,1.95);cam.rotation_euler=(Vector((0,0,1.62))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.resolution_x,scene.render.resolution_y=1400,1400;scene.render.filepath=str(A/'crew-detail.png');bpy.ops.render.render(write_still=True)
cam.location=(-1.0,2.7,1.75);cam.rotation_euler=(Vector((0,0,1.3))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(A/'crew-detail-back.png');bpy.ops.render.render(write_still=True)
