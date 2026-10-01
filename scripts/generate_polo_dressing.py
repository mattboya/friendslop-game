CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/PoloDressing.blend","man":"ArtSource/polo-dressing-manifest.json","rev":"artifacts/polo-dressing",
"palm":{"tall":(10.2,0,.27,.17,11),"lean":(9.0,2.2,.26,.16,10),"fronds":9,"frond":((1.3,.34,.74),(1.4,-.2,.62),(1.2,-.85,.38)),"crown":.46,"nut":(.22,-.2,.15)},
"wheel":{"axle":7.5,"r":6,"inner":4.2,"legs":(1.1,3.2),"leg_r":.14,"plat":(3.2,.25,2.4),"rim":16,"spokes":8,"gond":(1.2,.9,1.0),"drop":.75,"rim_x":.8,"roof":.04},
"stage":{"screen":(10,4.2,-1.0,1.9,3.2),"arch":(8.8,7.2,3.9,4.4,.16,.35),"pylon":(1.0,.5),"sign":(7.2,1.3,"PALM MIRAGE",.85),
"crowd":((-16,0,-14),(16,6,-4.7)),"dj":((-3.2,1.4,-2.4),(3.2,3.7,.2))},
"canopy":{"mast":(10,-1,11.2,.25),"cap":.55,"width":2.8,"thick":.35,"sag":.6,"petals":((-.78,-.62,9,12,"CanvasRose"),(-.35,-.94,10,18,"CanvasGold"),(.25,-.97,7,22,"CanvasMint")),"floor":6.0},
"tower":{"h":20,"base":4.0,"top":6.0,"levels":8,"post_r":.12,"panels":40,"panel":(1.5,1.1,.08),"cols":("Rose","PaintRose","Gold","Cream","Mint","Glass","Blue")},
"astro":{"torso":((0,5.4,0),(2.2,2.1,3.5)),"helmet":((0,7.6,4.4),2.3),"visor":((0,7.5,6.25),(1.5,1.1,.5)),"pack":((0,8.1,-.6),(3.4,2.4,3.8)),
"armL":((-1.5,6.2,2.0),(-3.0,3.4,4.6),(-3.1,1.15,5.8)),"armR":((1.5,6.2,2.0),(3.5,6.4,5.8),(2.6,8.6,7.4)),"arm_r":.95,"glove":1.15,
"leg":((1.3,4.6,-2.2),(1.9,1.15,-3.6),(1.9,1.15,-7.4)),"leg_r":1.15,"boot":((1.9,1.2,-8.4),(2.2,2.4,2.8)),
"phone":((2.4,9.6,8.4),(2.6,5.0,.35)),"lens":((3.2,11.4,8.62),.35),"rec":((1.8,11.6,8.6),.15),"panel":((0,4.3,3.0),(2.0,1.2,.5))},
"ridge":{"w":66,"d":6,"foot":2.5,"h":(30,40,24),"peaks":((0,0),(.08,.45),(.17,.3),(.27,.85),(.36,.6),(.46,1.0),(.55,.7),(.66,.9),(.76,.4),(.86,.55),(1,0))}}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
import festival_kit as FK
_cam=FK.camera
def _camera(*a,**kw):
    c=_cam(*a,**kw)
    if c.data.type=="ORTHO":c.data.clip_end=max(c.data.clip_end,6*c.data.ortho_scale)
    return c
FK.camera=_camera # the 66 m ridges push the gallery camera past the kit's 500 m clip, which rendered blank
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
def rainbow_tower(k):
    T=G["tower"];h,n=T["h"],T["levels"];kit(k)
    half=lambda y:T["base"]/2+(T["top"]-T["base"])/2*(y/h)
    for sx in (-1,1):
        for sz in (-1,1):cyl("Post"+str(sx)+str(sz),"Metal",(sx*T["base"]/2,0,sz*T["base"]/2),(sx*T["top"]/2,h,sz*T["top"]/2),T["post_r"],6)
    corners=lambda s,y:[(-s,y,-s),(s,y,-s),(s,y,s),(-s,y,s)]
    for L in range(1,n+1):
        y=h*L/n;c=corners(half(y),y)
        for f in range(4):slab("Ring"+str(L)+str(f),"Metal",c[f],c[(f+1)%4],.08,.08)
    for L in range(n):
        y0,y1=h*L/n,h*(L+1)/n
        for f,(nx,nz) in enumerate(((0,-1),(1,0),(0,1),(-1,0))):
            slab("Strut"+str(L)+str(f),"Metal",(nx*half(y0),max(y0,.01),nz*half(y0)),(nx*half(y1),y1,nz*half(y1)),.08,.08)
    pw,ph,pt=T["panel"]
    for i in range(T["panels"]):
        y=.8+i/T["panels"]*(h-2.2);f=i%4;nx,nz=((0,-1),(1,0),(0,1),(-1,0))[f];along=((i//4)%3-1)*.45;s=half(y)
        c=(nx*s+(along if nz else 0),y,nz*s+(along if nx else 0))
        box("Panel"+str(i),T["cols"][i%len(T["cols"])],c,(pw,ph,pt) if nz else (pt,ph,pw))
    s=half(h);slab("TopX1","Metal",(-s,h,-s),(s,h,s),.08,.08);slab("TopX2","Metal",(s,h,-s),(-s,h,s),.08,.08)
    cyl("Crown mast","Metal",(0,h,0),(0,h+1.2,0),.06,6);ico("Star","StageGlowGold",(0,h+1.4,0),.6,1)
def astronaut(k):
    S=G["astro"];kit(k,[("Ground","Stone",(0,-.05,0),(24,.1,26),0)])
    ico("Torso","White",S["torso"][0],1,3,s=S["torso"][1],smooth=True);ico("Helmet","White",S["helmet"][0],S["helmet"][1],3,smooth=True)
    ico("Visor","Glass",S["visor"][0],1,2,s=S["visor"][1],smooth=True);box("Pack","Cream",S["pack"][0],S["pack"][1],.3)
    box("ChestPanel","Blue",S["panel"][0],S["panel"][1],.1)
    for i,m in enumerate(("StageGlowGold","StageGlowMint","StageGlowRose")):ico("Button"+str(i),m,(S["panel"][0][0]-.5+.5*i,S["panel"][0][1],S["panel"][0][2]+.28),.18,1)
    for nme in ("armL","armR"):
        pts=S[nme];tube(nme,"White",pts,S["arm_r"],8,smooth=True);ico(nme+"Glove","Gold",pts[-1],S["glove"],2,smooth=True)
    for sx in (-1,1):
        pts=[(sx*p[0],p[1],p[2]) for p in S["leg"]];tube("Leg"+str(sx),"White",pts,S["leg_r"],8,smooth=True)
        bc,bs=S["boot"];box("Boot"+str(sx),"Gold",(sx*bc[0],bc[1],bc[2]),bs,.25)
    box("Phone","Dark",S["phone"][0],S["phone"][1],.15)
    pc,ps=S["phone"];box("Screen","StageGlowMint",(pc[0],pc[1],pc[2]-ps[2]/2-.02),(ps[0]-.3,ps[1]-.6,.04))
    ico("Lens","Rubber",S["lens"][0],S["lens"][1],2);ico("Rec","StageGlowRose",S["rec"][0],S["rec"][1],1)
def ridge(k,v):
    R=G["ridge"];w,d,H=R["w"],R["d"],R["h"][v];kit(k)
    pk=R["peaks"];sh=[(t,pk[(i+3*v)%(len(pk)-2)+1][1] if 0<i<len(pk)-1 else 0) for i,(t,_) in enumerate(pk)]
    top=[(-w/2+t*w,max(R["foot"]+.5,H*y)) for t,y in sh];top[0]=(-w/2,R["foot"]);top[-1]=(w/2,R["foot"])
    ring=lambda z:[(-w/2,R["foot"],z)]+[(x,y,z) for x,y in top[1:-1]]+[(w/2,R["foot"],z)]
    loft("Ridge","Sand",[ring(0),ring(d)])
    box("Foot","Stone",(0,R["foot"]/2,d/2),(w,R["foot"],d))
BUILD={"FestivalPalmTall":lambda k:palm(k,G["palm"]["tall"]),"FestivalPalmLean":lambda k:palm(k,G["palm"]["lean"]),
"FestivalWheelBase":wheel_base,"FestivalWheelRotor":wheel_rotor,"FestivalWheelGondola":gondola,
"FestivalStageMirage":stage_mirage,"FestivalPetalCanopy":petal_canopy,
"FestivalRainbowTower":rainbow_tower,"FestivalAstronaut":astronaut,
"FestivalDesertRidge1":lambda k:ridge(k,0),"FestivalDesertRidge2":lambda k:ridge(k,1),"FestivalDesertRidge3":lambda k:ridge(k,2)}
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
