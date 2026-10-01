CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/PoloDressing.blend","man":"ArtSource/polo-dressing-manifest.json","rev":"artifacts/polo-dressing",
"palm":{"tall":(10.2,0,.27,.17,11),"lean":(9.0,2.2,.26,.16,10),"fronds":9,"frond":((1.3,.34,.74),(1.4,-.2,.62),(1.2,-.85,.38)),"crown":.46,"nut":(.22,-.2,.15)},
"wheel":{"axle":7.5,"r":6,"inner":4.2,"legs":(1.1,3.2),"leg_r":.14,"plat":(3.2,.25,2.4),"rim":16,"spokes":8,"gond":(1.2,.9,1.0),"drop":.75,"rim_x":.8,"roof":.04},
"stage":{"screen":(10,4.2,-1.0,1.9,3.2),"arch":(8.8,7.2,3.9,4.4,.16,.35),"pylon":(1.0,.5),"sign":(7.2,1.3,"PALM MIRAGE",.85),
"crowd":((-16,0,-14),(16,6,-4.7)),"dj":((-3.2,1.4,-2.4),(3.2,3.7,.2))},
"canopy":{"mast":(10,-1,11.2,.25),"cap":.55,"width":2.8,"thick":.35,"sag":.6,"petals":((-.78,-.62,9,12,"CanvasRose"),(-.35,-.94,10,18,"CanvasGold"),(.25,-.97,7,22,"CanvasMint")),"floor":6.0}}
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
def rim(r,deg,x=0):return (x,r*math.sin(math.radians(deg)),r*math.cos(math.radians(deg)))
def wheel_rotor(k):
    W=G["wheel"];R,Ri,n,X=W["r"],W["inner"],W["rim"],W["rim_x"];kit(k,[("Anchor","Stone",(0,0,0),(.3,.3,.3),0)])
    cyl("Axle","Metal",(-1.2,0,0),(1.2,0,0),.18,8);cyl("Hub","Gold",(-.3,0,0),(.3,0,0),.55,10)
    xi=.3+(Ri-.4)/(R-.4)*(X-.3)
    for sx in (-1,1):
        t="LR"[sx>0]
        for s in range(n):
            a,b=360*s/n,360*(s+1)/n
            slab("Rim"+str(s)+t,"StageGlowMint" if s%2==0 else "StageGlowRose",rim(R,a,sx*X),rim(R,b,sx*X),.18,.18)
            slab("Inner"+str(s)+t,"Gold",rim(Ri,a,sx*xi),rim(Ri,b,sx*xi),.1,.1)
            if s%2==0:ico("Bulb"+str(s)+t,"StageGlowGold",rim(R+.12,a,sx*X),.13)
        for s in range(W["spokes"]):a=360*s/W["spokes"];cyl("Spoke"+str(s)+t,"Metal",rim(.4,a,sx*.3),rim(R,a,sx*X),.06,5)
    for s in range(W["spokes"]):p=rim(R,360*s/W["spokes"]);cyl("Hanger"+str(s),"Metal",(-X,p[1],p[2]),(X,p[1],p[2]),.05,5)
def gondola(k):
    W=G["wheel"];gw,gh,gd=W["gond"];d=W["drop"];b=-gh/2;kit(k,[("Anchor","Stone",(0,d,0),(.06,.06,.06),0)])
    box("Floor","Rose",(0,b+.04,0),(gw,.08,gd),.02)
    for z in (-gd/2,gd/2):box("Wall"+str(z),"Rose",(0,b+.25,z),(gw,.42,.06),.02)
    for x in (-gw/2,gw/2):box("Side"+str(x),"Rose",(x,b+.25,0),(.06,.42,gd),.02)
    box("Seat","Cream",(0,b+.24,gd/2-.18),(gw-.16,.1,.3),.02)
    for x in (-gw/2+.05,gw/2-.05):
        for z in (-gd/2+.05,gd/2-.05):cyl("Post"+str(x)+str(z),"Metal",(x,b+.45,z),(x,gh/2-.08,z),.025,5)
    box("Roof","Cream",(0,gh/2-.04,0),(gw+W["roof"],.08,gd+W["roof"]),.02);box("RoofPeak","Rose",(0,gh/2+.06,0),(gw*.6,.12,gd*.6),.02)
    cyl("Hanger","Metal",(0,gh/2+.12,0),(0,d,0),.04,6)
SB=[]
def stage_bounds():
    if SB:return SB[0]
    old=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=os.path.join(R0,G["out"],"FestivalStage.fbx"))
    new=[o for o in bpy.data.objects if o not in old]
    for o in new:o["ctx"]=1;o["temp"]=1
    bpy.context.view_layer.update();P=[U(o.matrix_world@v.co) for o in new if o.type=="MESH" for v in o.data.vertices]
    SB.append((V([min(p[i] for p in P) for i in range(3)]),V([max(p[i] for p in P) for i in range(3)])));return SB[0]
def stage_mirage(k):
    S=G["stage"];sx,sy,sz,sw,sh=S["screen"];ax,ay,az,ah,ar,ad_=S["arch"];pw,pd=S["pylon"];bw,bh,text,ts=S["sign"]
    kit(k,[("Ground","Stone",(0,-.05,0),(24,.1,12),0)])
    for s in (-1,1):
        x=s*sx;cyl("ScreenPost"+str(s),"Dark",(x,0,sz),(x,sy,sz),.2,8)
        box("ScreenFrame"+str(s),"Dark",(x,sy+sh/2,sz),(sw,sh,.25),.04)
        for i,m in enumerate(("StageGlowRose","StageGlowGold","StageGlowMint")):
            box("ScreenBand"+str(s)+str(i),m,(x,sy+.3+.45+i*.95,sz-.145),(sw-.3,.85,.04))
        box("Pylon"+str(s),"Dark",(s*(ax-pw/2+.15),ay/2,az),(pw,ay,pd))
    for i,m in enumerate(("Rose","Gold","Mint")):
        w,h=ax-ad_*i,ah-ad_*i
        tube("Arch"+str(i),m,[(-w*math.cos(math.pi*t/12),ay+h*math.sin(math.pi*t/12),az) for t in range(13)],ar,6)
    top=ay+ah+ar-.06+bh/2
    box("SignTrim","StageGlowGold",(0,top,az+.03),(bw+.2,bh+.2,.16));box("SignBoard","Dark",(0,top,az-.03),(bw,bh,.2))
    label("SignText","Cream",text,(0,top,az-.134),size=ts,depth=.02)
def petal(n,m,root,dx,dz,L,pitch):
    C_=G["canopy"];W,T,sag=C_["width"]/2,C_["thick"]/2,C_["sag"]
    rings=[];ts=(0,.12,.35,.6,.85,1)
    for t in ts:
        w=max(.05,W*math.sin(math.pi*min(t,.98))**.7);th=max(.03,T*w/W);y=-sag*t*t
        rings.append([(w*math.cos(6.2832*j/8),y+th*math.sin(6.2832*j/8),t*L) for j in range(8)])
    p=math.radians(pitch);d=V((dx*math.cos(p),dz*math.cos(p),-math.sin(p)))
    Tm=M.Translation(A(*root))@d.to_track_quat("Y","Z").to_matrix().to_4x4()
    return loft(n,m,rings,T=Tm)
def petal_canopy(k):
    C_=G["canopy"];mx,mz,mh,mr=C_["mast"];kit(k,[("Ground","Stone",(0,-.05,0),(24,.1,12),0)])
    for s in (-1,1):
        x=s*mx;cyl("Mast"+str(s),"Metal",(x,0,mz),(x,mh,mz),mr,8);ico("Cap"+str(s),"Gold",(x,mh,mz),C_["cap"],2)
        for i,(dx,dz,L,pitch,m) in enumerate(C_["petals"]):petal("Petal"+str(s)+str(i),m,(x,mh,mz),dx*s,dz,L,pitch)
def canopy_clear():
    lo,hi=stage_bounds();return [G["stage"]["crowd"],((max(lo.x,-9.2),lo.y,lo.z),(min(hi.x,9.2),hi.y+.2,hi.z))]
def stage_review(k):
    stage_bounds()
    for name,eye,at in (("front",(0,1.6,-24),(0,6,0)),("high",(18,14,-22),(0,6,0))):
        render(os.path.join(G["rev"],k+"-"+name+".png"),camera(eye,at,28),lambda o:o.get("kit") in ("FestivalStageMirage","FestivalPetalCanopy") or o.get("ctx"))
BUILD={"FestivalPalmTall":lambda k:palm(k,G["palm"]["tall"]),"FestivalPalmLean":lambda k:palm(k,G["palm"]["lean"]),
"FestivalWheelBase":wheel_base,"FestivalWheelRotor":wheel_rotor,"FestivalWheelGondola":gondola,
"FestivalStageMirage":stage_mirage,"FestivalPetalCanopy":petal_canopy}
CLEAR={"FestivalStageMirage":lambda:[G["stage"]["crowd"],G["stage"]["dj"]],"FestivalPetalCanopy":canopy_clear}
REVIEW={"FestivalPetalCanopy":stage_review}
want=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else list(BUILD)
setup(want)
for k in want:BUILD[k](k)
gallery(want,os.path.join(G["rev"],want[0]+".png"),cols=min(4,len(want)))
for k in want:
    if k in REVIEW:REVIEW[k](k)
for o in [o for o in bpy.data.objects if o.get("ctx")]:bpy.data.objects.remove(o,do_unlink=True)
for x in [x for x in bpy.data.meshes if x.users==0]:bpy.data.meshes.remove(x)
for m in [m for m in bpy.data.materials if m.users==0 and not m.name.startswith("FK_")]:bpy.data.materials.remove(m)
res=finish(want,G["out"],G["src"],G["man"],clear={k:CLEAR[k]() for k in want if k in CLEAR})
print("BUDGET",{k:v["triangles"] for k,v in res.items()})
