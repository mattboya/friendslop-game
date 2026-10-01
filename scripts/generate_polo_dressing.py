CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/PoloDressing.blend","man":"ArtSource/polo-dressing-manifest.json","rev":"artifacts/polo-dressing",
"palm":{"tall":(10.2,0,.27,.17,11),"lean":(9.0,2.2,.26,.16,10),"fronds":9,"frond":((1.3,.34,.74),(1.4,-.2,.62),(1.2,-.85,.38)),"crown":.46,"nut":(.22,-.2,.15)}}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
G=CONFIG
def blade(n,m,a,b,w,t):
    z=(A(*b)-A(*a)).normalized();x=z.cross(V((0,0,1)));x.normalize();y=z.cross(x)
    return box(n,m,[(p+q)/2 for p,q in zip(a,b)],(w,(A(*b)-A(*a)).length,t),0,M((x,y,z)).transposed().to_4x4())
def palm(k,p):
    H,lean,r0,r1,n=p;P=G["palm"];kit(k)
    e=r0*math.sin(math.atan(lean/(n*H)))
    pts=[(lean*(i/n)**2,H*i/n+e,0) for i in range(n+1)]
    for i in range(n):
        r=r0+(r1-r0)*i/n;cyl("Trunk"+str(i),"PalmBark",pts[i],pts[i+1],r,7,r2=r*.84)
    top=pts[-1];ico("Crown","Frond",top,P["crown"],2,s=(1,.7,1))
    for j in range(P["fronds"]):
        a=6.2832*j/P["fronds"]+.35*(j%2);d=(math.cos(a),0,math.sin(a));p0=ad(top,(0,.1,0))
        for s,(L,dy,w) in enumerate(P["frond"]):
            p1=(p0[0]+d[0]*L,p0[1]+dy,p0[2]+d[2]*L);blade("Frond"+str(j)+"_"+str(s),"Frond" if (j+s)%3 else "LeafWarm",p0,p1,w,.05);p0=p1
    nr,ny,rr=P["nut"]
    for j in range(3):
        a=6.2832*j/3+.5;ico("Nut"+str(j),"PalmBark",(top[0]+nr*math.cos(a),top[1]+ny,top[2]+nr*math.sin(a)),rr)
BUILD={"FestivalPalmTall":lambda k:palm(k,G["palm"]["tall"]),"FestivalPalmLean":lambda k:palm(k,G["palm"]["lean"])}
CLEAR={}
REVIEW={}
want=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else list(BUILD)
setup(want)
for k in want:BUILD[k](k)
gallery(want,os.path.join(G["rev"],want[0]+".png"),cols=min(4,len(want)))
for k in want:
    if k in REVIEW:REVIEW[k](k)
for o in [o for o in bpy.data.objects if o.get("ctx")]:bpy.data.objects.remove(o,do_unlink=True)
for m in [m for m in bpy.data.materials if m.users==0 and not m.name.startswith("FK_")]:bpy.data.materials.remove(m)
res=finish(want,G["out"],G["src"],G["man"],clear={k:CLEAR[k]() for k in want if k in CLEAR})
print("BUDGET",{k:v["triangles"] for k,v in res.items()})
