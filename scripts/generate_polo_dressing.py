CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/PoloDressing.blend","man":"ArtSource/polo-dressing-manifest.json","rev":"artifacts/polo-dressing",
"palm":{"tall":(10.2,0,.27,.17,11),"lean":(9.0,2.2,.26,.16,10),"fronds":9,"frond":((1.3,.34,.74),(1.4,-.2,.62),(1.2,-.85,.38)),"crown":.46,"nut":(.22,-.2,.15)},
"wheel":{"axle":7.5,"r":6,"inner":4.2,"legs":(1.1,3.2),"leg_r":.14,"plat":(3.2,.25,2.4),"rim":16,"spokes":8,"gond":(1.2,.9,1.0),"drop":.75}}
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
def wheel_base(k):
    W=G["wheel"];h=W["axle"];lx,lz=W["legs"];px,py,pz=W["plat"];kit(k,[("Ground","Stone",(0,-.05,0),(9,.1,9),0)])
    box("Platform","Wood",(0,py/2,0),(px,py,pz),.03);box("Step","Metal",(0,.06,-pz/2-.25),(1.4,.12,.5),.02)
    for x in (-lx,lx):
        for z in (-lz,lz):cyl("Leg"+str(x)+str(z),"Metal",(x,.06,z),(x,h,0),W["leg_r"],6);box("Foot"+str(x)+str(z),"Dark",(x,.06,z),(.5,.12,.5),.02)
        cyl("Brace"+str(x),"Metal",(x,3,-lz*(1-3/h)),(x,3,lz*(1-3/h)),.07,6);box("Bearing"+str(x),"Gold",(x,h,0),(.36,.5,.5),.04)
def rim(r,deg):return (0,r*math.sin(math.radians(deg)),r*math.cos(math.radians(deg)))
def wheel_rotor(k):
    W=G["wheel"];R,Ri,n=W["r"],W["inner"],W["rim"];kit(k,[("Anchor","Stone",(0,0,0),(.3,.3,.3),0)])
    cyl("Axle","Metal",(-1.2,0,0),(1.2,0,0),.18,8);cyl("Hub","Gold",(-.3,0,0),(.3,0,0),.55,10)
    for s in range(n):
        a,b=360*s/n,360*(s+1)/n
        slab("Rim"+str(s),"StageGlowMint" if s%2==0 else "StageGlowRose",rim(R,a),rim(R,b),.18,.18)
        slab("Inner"+str(s),"Gold",rim(Ri,a),rim(Ri,b),.1,.1);ico("Bulb"+str(s),"StageGlowGold",rim(R+.12,a),.13)
    for s in range(W["spokes"]):
        a=360*s/W["spokes"];p=rim(R,a);cyl("Spoke"+str(s),"Metal",rim(.4,a),p,.06,5);cyl("Hanger"+str(s),"Metal",(-.3,p[1],p[2]),(.3,p[1],p[2]),.05,5)
def gondola(k):
    W=G["wheel"];gw,gh,gd=W["gond"];d=W["drop"];b=-gh/2;kit(k,[("Anchor","Stone",(0,d,0),(.06,.06,.06),0)])
    box("Floor","Rose",(0,b+.04,0),(gw,.08,gd),.02)
    for z in (-gd/2,gd/2):box("Wall"+str(z),"Rose",(0,b+.25,z),(gw,.42,.06),.02)
    for x in (-gw/2,gw/2):box("Side"+str(x),"Rose",(x,b+.25,0),(.06,.42,gd),.02)
    box("Seat","Cream",(0,b+.24,gd/2-.18),(gw-.16,.1,.3),.02)
    for x in (-gw/2+.05,gw/2-.05):
        for z in (-gd/2+.05,gd/2-.05):cyl("Post"+str(x)+str(z),"Metal",(x,b+.45,z),(x,gh/2-.08,z),.025,5)
    box("Roof","Cream",(0,gh/2-.04,0),(gw+.12,.08,gd+.12),.02);box("RoofPeak","Rose",(0,gh/2+.06,0),(gw*.6,.12,gd*.6),.02)
    cyl("Hanger","Metal",(0,gh/2+.12,0),(0,d,0),.04,6)
BUILD={"FestivalPalmTall":lambda k:palm(k,G["palm"]["tall"]),"FestivalPalmLean":lambda k:palm(k,G["palm"]["lean"]),
"FestivalWheelBase":wheel_base,"FestivalWheelRotor":wheel_rotor,"FestivalWheelGondola":gondola}
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
