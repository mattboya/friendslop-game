CONFIG={"fbx":"Assets/Festival/Art/ProductionSample04","src":"ArtSource/ProductionPackage04/MissionProps.blend","man":"ArtSource/ProductionPackage04/mission-manifest.json","rev":"artifacts/production04-mission","font":3,"eye":1.62,
"booth":{"w":2.0,"d":1.6,"h":2.45,"floor":.2,"win":(.65,1.05,1.85),"counter":(1.5,.34),"roof":(.28,.45),"door":(-.3,.5,2.0),"step":(.34,.2,.9),"wall":"Gold","trim":"Dark","sign":"SECURITY CHECK"},"table":{"top":(1.8,.76,.76),"leg":(.72,.3),"tray":(.46,.08,.34),"trays":((-.55,"Blue"),(0,"Mint"),(.55,"Blue")),"sign":"BAG CHECK"},
"arm":{"hinge":(0,.88,-.305),"len":3.3,"tail":.55,"sec":(.12,.07),"stripes":5,"rest":3.1},"stan":{"pitch":1.5,"hook":.9,"sag":.16,"rope":"Rose"},"fence":{"pitch":3.0,"up":1.46,"h":2.05,"foot":(.2,.14,.62),"scrim":(.24,1.98),"text":"CREW ONLY"},"gate":{"post":2.55,"hinge":-1.40,"stile":1.37,"caster":1.2,"text":"CREW GATE"},
"lock":{"w":.42,"h":1.9,"d":.5,"doors":("Mint","Gold","Rose"),"body":"Blue"},"bag":{"rings":((0,.30,.18),(.03,.38,.24),(.14,.44,.30),(.34,.46,.32),(.50,.42,.28),(.58,.34,.22),(.62,.18,.12)),"tag":"MINE!"},"truck":{"rail":.23,"top":1.15,"wheel":(.295,.13,.14,.035),"spk":(.46,.72,.34)},"blob":{"pallet":1.0,"prof":((.26,0),(.40,.06),(.50,.22),(.52,.42),(.48,.64),(.40,.82),(.28,.96),(.12,1.04),(.02,1.06)),"eye":(.15,.74),"band":.30,"tag":"DO NOT POP"},"crate":{"size":(.8,.66,.6),"text":"FRAGILE"},
"stage":{"deck":(8.0,5.0,1.0),"stair":(.9,2.1,4,.28),"tower":(4.45,-2.0,4.6),"rise":1.15,"cans":(.28,.39,.5,.61,.72),"spk":(3.25,-1.55),"dj":(2.2,.9,.7,.3),"text":"THE B-SIDE"}}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
C=CONFIG;PI=math.pi;RX=rot(90,"x");FX=rot(90,"y")@RX;BX=rot(-90,"y")@RX;BK=rot(180,"y")@RX
K=("AH04_CheckpointBooth","AH04_BagSearchTable","AH04_BarrierArm","AH04_QueueStanchion","AH04_BackstageFence","AH04_CrewGate","AH04_CrewLockers","AH04_BelongingsBag","AH04_HandTruckSpeaker","AH04_InflatableCargo","AH04_FragileCrate","AH04_SideStage")
setup(K)
def lab(n,m,t,c,size=.1,depth=.004,R=RX,T=I,**kw):
    cu=bpy.data.curves.new(n,"FONT");cu.body=t;cu.size=size;cu.extrude=depth/2;cu.align_x="CENTER";cu.align_y="CENTER";cu.resolution_u=C["font"]
    tmp=bpy.data.objects.new(n,cu);bpy.context.scene.collection.objects.link(tmp);bpy.context.view_layer.update()
    me=bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()));bpy.data.objects.remove(tmp);bpy.data.curves.remove(cu)
    bm=bmesh.new();bm.from_mesh(me);bpy.data.meshes.remove(me);bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-5)
    bmesh.ops.transform(bm,matrix=T@M.Translation(A(*c))@R,verts=bm.verts[:]);return ob(n,m,bm,**kw)
def join(n,m,ns,piv):
    bm=bmesh.new()
    for x in ns:
        o=PARTS[KIT[0]].pop(x);me=o.data;me.transform(RZ);bm.from_mesh(me);bpy.data.objects.remove(o);bpy.data.meshes.remove(me)
    for l in list(bm.loops.layers.uv.values()):bm.loops.layers.uv.remove(l)
    return ob(n,m,bm,piv=piv,tag="keep")
def ring(c,r,plane="xy",n=12):
    i,j={"xy":(0,1),"yz":(1,2),"xz":(0,2)}[plane]
    return [tuple(c[a]+(r*math.cos(2*PI*t/n) if a==i else r*math.sin(2*PI*t/n) if a==j else 0) for a in range(3)) for t in range(n)]
def rr(w,d,y,k=14,e=.55):
    return [(w/2*math.copysign(abs(math.cos(a))**e,math.cos(a)),y,d/2*math.copysign(abs(math.sin(a))**e,math.sin(a))) for a in (2*PI*j/k for j in range(k))]
def lerp(tab,y,i=1):
    for a,b in zip(tab,tab[1:]):
        if a[0]<=y<=b[0]:f=(y-a[0])/(b[0]-a[0]);return a[i]+f*(b[i]-a[i])
def booth():
    b=C["booth"];W,D,H,F=b["w"],b["d"],b["h"],b["floor"];wx,w0,w1=b["win"];wm,tm=b["wall"],b["trim"];hw,hd=W/2,D/2;t=.06;fz=-hd+t/2;yc=(F+H)/2
    for s in (-1,1):box("Skid"+str(s),"Metal",(0,.05,s*.6),(W,.1,.14))
    box("Floor",tm,(0,.15,0),(W,.1,D))
    box("WallBack",wm,(0,yc,hd-t/2),(W,H-F,t));box("WallL",wm,(-hw+t/2,yc,0),(t,H-F,D-2*t));box("WallR",wm,(hw-t/2,yc,0),(t,H-F,D-2*t))
    box("WallFrontLow",wm,(0,(F+w0)/2,fz),(W,w0-F,t));box("WallFrontHigh",wm,(0,(w1+H)/2,fz),(W,H-w1,t))
    for s in (-1,1):
        q=str(s);box("Jamb"+q,wm,(s*(wx+hw)/2,(w0+w1)/2,fz),(hw-wx,w1-w0,t));box("WinTrim"+q,tm,(s*(wx+.03),(w0+w1)/2+.03,-hd-.01),(.06,w1-w0+.06,.02))
        slab("Bracket"+q,tm,(s*.55,w0-.36,-hd),(s*.55,w0,-hd-b["counter"][1]+.05),.04,.04)
        box("Kick"+q,tm,(0,.3,s*(hd+.006)),(W+.024,.2,.012));box("KickSide"+q,tm,(s*(hw+.006),.3,0),(.012,.2,D))
    box("WinTrimTop",tm,(0,w1+.03,-hd-.01),(2*wx+.12,.06,.02))
    box("Slider","Glass",(-(wx+.05)/2,(w0+w1)/2,fz),(wx-.05,w1-w0,.015));box("Mullion",tm,(-.05,(w0+w1)/2,fz),(.04,w1-w0,.03))
    cw,cd=b["counter"];cy=w0+.025;oz=-hd-cd/2
    box("Counter","Wood",(0,cy,oz+.01),(cw,.05,cd+.02),.008);box("CounterIn","Wood",(0,cy,-hd+t+.17),(W-2*t,.05,.34))
    box("Clipboard",tm,(-.45,w0+.056,oz),(.22,.012,.3));box("Paper","Cream",(-.45,w0+.065,oz+.01),(.18,.006,.24));box("Clip","Metal",(-.45,w0+.07,oz-.13),(.08,.02,.03))
    cyl("BellBase",tm,(.45,w0+.05,oz),(.45,w0+.065,oz),.07,10);lathe("Bell","Gold",[(.06,0),(.055,.02),(.035,.05),(.012,.065),(.014,.08)],(.45,w0+.065,oz),10)
    iz=-hd+t+.14;cyl("MonStand","Metal",(.2,w0+.05,iz),(.2,w0+.2,iz),.02,6);box("Monitor",tm,(.2,w0+.32,iz),(.44,.28,.06));box("Screen","StageGlowMint",(.2,w0+.32,iz-.032),(.38,.22,.006))
    box("Radio",tm,(-.35,w0+.11,iz),(.2,.12,.08));cyl("Antenna","Metal",(-.28,w0+.17,iz),(-.28,w0+.4,iz),.008,4)
    d0,d1,dh=b["door"];dz=(d0+d1)/2
    box("Door","PaintGold",(hw+.01,(F+.02+dh)/2,dz),(.02,dh-F-.02,d1-d0))
    for z in (d0-.025,d1+.025):box("DoorFrame"+str(z),tm,(hw+.015,(F+dh)/2,z),(.03,dh-F,.05))
    box("DoorHead",tm,(hw+.015,dh+.025,dz),(.03,.05,d1-d0+.1));box("DoorGlass","Glass",(hw+.022,1.55,dz),(.012,.4,.35))
    cyl("Lever","Metal",(hw+.02,1.05,d0+.1),(hw+.07,1.05,d0+.1),.015,6);box("LeverGrip","Metal",(hw+.07,1.05,d0+.16),(.025,.025,.14))
    sx,sy,sz=b["step"];box("Step","Metal",(hw+sx/2,sy/2,dz),(sx,sy,sz));box("StepNose","Gold",(hw+sx-.02,sy+.006,dz),(.04,.012,sz))
    box("SideFrame",tm,(-hw-.005,1.55,.1),(.01,.62,.72));box("SideGlass","Glass",(-hw-.012,1.55,.1),(.01,.5,.6))
    rb,rf=b["roof"];box("Roof",tm,(0,H+.06,(rb-rf)/2),(W+2*rb,.12,D+rb+rf),.01)
    box("FasciaF","Gold",(0,H+.06,-hd-rf-.01),(W+2*rb+.04,.08,.02))
    for s in (-1,1):box("Fascia"+str(s),"Gold",(s*(hw+rb+.01),H+.06,(rb-rf)/2),(.02,.08,D+rb+rf))
    sz0=-hd-rf+.1;box("SignBoard",tm,(0,H+.12+.19,sz0),(1.9,.38,.08));box("SignEdge","Gold",(0,H+.12+.385,sz0),(1.94,.03,.1))
    lab("SignText","Gold",b["sign"],(0,H+.31,sz0-.041),.19)
    for s in (-1,1):slab("SignStrut"+str(s),"Metal",(s*.7,H+.12,sz0+.4),(s*.7,H+.42,sz0+.04),.04,.03)
    cyl("BeaconBase",tm,(.75,H+.12,.55),(.75,H+.18,.55),.08,8);ico("Beacon","StageGlowGold",(.75,H+.25,.55),.075,2,(1,1.2,1),smooth=True)
def table():
    t=C["table"];L,D,H=t["top"];lx,lz=t["leg"];tw,th,td=t["tray"];e=.012
    box("Top","White",(0,H-.0225,0),(L,.045,D),.01);box("Rim","Dark",(0,H-.06,0),(L+.02,.03,D+.02))
    for s in (-1,1):
        x=s*lx;q=str(s)
        for z in (-lz,lz):cyl("Leg"+q+str(z),"Metal",(x,.025,z),(x,H-.075,z),.018,6);cyl("Foot"+q+str(z),"Rubber",(x,0,z),(x,.025,z),.026,6)
        cyl("LegBar"+q,"Metal",(x,.12,-lz),(x,.12,lz),.014,6);cyl("LegMid"+q,"Metal",(x,.5,-lz),(x,.5,lz),.014,6);cyl("Brace"+q,"Metal",(x,.5,0),(x-s*.4,H-.075,0),.014,6)
    for i,(x,m) in enumerate(t["trays"]):
        q=str(i);z=-.1;box("TrayBase"+q,m,(x,H+e/2,z),(tw,e,td))
        for s in (-1,1):box("TrayX"+q+str(s),m,(x+s*(tw/2-e/2),H+th/2,z),(e,th,td));box("TrayZ"+q+str(s),m,(x,H+th/2,z+s*(td/2-e/2)),(tw-2*e,th,e))
    x,z=t["trays"][0][0],-.1;y=H+e
    box("Phone","Dark",(x-.1,y+.0075,z),(.08,.015,.15),.004);box("PhoneScreen","Glass",(x-.1,y+.016,z),(.066,.004,.13))
    tube("KeyRing","Metal",ring((x+.08,y+.006,z-.02),.03,"xz",8),.006,4,True);box("KeyA","Gold",(x+.13,y+.006,z-.03),(.07,.012,.025));box("KeyB","Gold",(x+.1,y+.006,z+.03),(.025,.012,.07))
    x=t["trays"][1][0];puff("Pouch","CanvasRose",(x-.05,y+.04,z),(.24,.07,.18));cyl("Bottle","Glass",(x+.13,y+.035,z-.12),(x+.13,y+.035,z+.08),.035,8);cyl("BottleCap","White",(x+.13,y+.035,z+.08),(x+.13,y+.035,z+.11),.022,6)
    a=(.5,H+.58,.02);dv=V((0-.5,H-(H+.58),-.1-.02)).normalized();b=ad(a,dv,.16)
    cyl("LampBase","Dark",(.72,H,.26),(.72,H+.03,.26),.08,10);cyl("LampArmA","Metal",(.72,H+.03,.26),(.72,H+.5,.2),.013,6);ico("LampJoint","Gold",(.72,H+.5,.2),.025,1)
    cyl("LampArmB","Metal",(.72,H+.5,.2),a,.013,6);cyl("LampShade","Gold",ad(a,dv,-.02),b,.04,10,.1);cyl("LampLens","StageGlowGold",ad(b,dv,-.03),ad(b,dv,-.012),.086,10)
    box("SignPlate","Gold",(-.4,H-.14,-D/2-.016),(.62,.16,.012));lab("SignText","Dark",t["sign"],(-.4,H-.14,-D/2-.023),.075)
def barrier():
    a=C["arm"];hx,hy,hz=a["hinge"];L,tl=a["len"],a["tail"];sh,sd=a["sec"]
    box("Base","Dark",(0,.04,0),(.6,.08,.55),.01);box("Cabinet","Gold",(0,.54,0),(.4,.92,.36),.015);box("Cap","Dark",(0,1.03,0),(.46,.06,.42),.01)
    for i in range(4):box("Vent"+str(i),"Dark",(0,.25+i*.06,-.181),(.24,.025,.006))
    box("WaitPlate","Dark",(0,.72,-.185),(.3,.12,.01));lab("WaitText","Gold","WAIT",(0,.72,-.1905),.075)
    cyl("Shaft","Metal",(hx,hy,-.18),(hx,hy,hz+sd/2),.035,8);cyl("BeaconBase","Dark",(0,1.06,0),(0,1.1,0),.07,8);ico("Beacon","StageGlowGold",(0,1.16,0),.07,2,(1,1.3,1),smooth=True)
    arm=box("BarrierArm__White","White",(hx+(L-tl)/2,hy,hz),(L+tl,sh,sd),.01,piv=(hx,hy,hz),tag="keep");ns=[]
    for i in range(a["stripes"]):ns.append("Stripe"+str(i));box(ns[-1],"Rose",(hx+.55+i*.6,hy,hz),(.3,sh+.006,sd+.006))
    ns.append("Tip");box("Tip","Rose",(hx+L-.045,hy,hz),(.1,sh+.006,sd+.006))
    parent(join("BarrierArmStripes__Rose","Rose",ns,(hx,hy,hz)),arm)
    box("Weight","Dark",(hx-tl+.14,hy,hz),(.3,sh+.08,sd+.04),.01);cyl("Hub","Dark",(hx,hy,hz-sd/2),(hx,hy,hz-sd/2-.025),.07,10)
    parent(join("BarrierArmWeight__Dark","Dark",["Weight","Hub"],(hx,hy,hz)),arm)
    rx=a["rest"];fy=hy-sh/2
    box("RestPlate","Dark",(rx,.02,hz),(.3,.04,.3));cyl("RestPost","Metal",(rx,.04,hz),(rx,fy-.06,hz),.035,8);cyl("RestBand","Gold",(rx,.45,hz),(rx,.55,hz),.039,8)
    box("RestFork","Dark",(rx,fy-.03,hz),(.1,.06,sd+.08))
    for s in (-1,1):box("RestProng"+str(s),"Dark",(rx,fy+.05,hz+s*(sd/2+.025)),(.1,.1,.02))
def post(p,x):
    lathe(p+"Base","Dark",[(.17,0),(.17,.02),(.13,.045),(.05,.065),(.035,.075)],(x,0,0),14)
    cyl(p+"Pole","Metal",(x,.07,0),(x,.94,0),.028,8);cyl(p+"Cap","Gold",(x,.94,0),(x,.97,0),.04,8);ico(p+"Ball","Gold",(x,1.0,0),.042,2)
    for d in (-1,1):tube(p+"Ring"+str(d),"Metal",ring((x+d*.05,C["stan"]["hook"],0),.025,"xy",8),.006,4,True)
def stanchion():
    s=C["stan"];p=s["pitch"]/2;hy=s["hook"];xs=p-.13
    post("S",-p);post("E",p)
    for d in (-1,1):cyl("Clip"+str(d),"Gold",(d*(p-.07),hy,0),(d*xs,hy-.01,0),.02,6)
    tube("Rope",s["rope"],[(-xs+2*xs*u,hy-.01-s["sag"]*4*u*(1-u),0) for u in (i/10 for i in range(11))],.018,6)
    e=join("StanchionEndPost__Metal","Metal",["EPole","ERing-1","ERing1"],(p,0,0))
    parent(join("StanchionEndBase__Dark","Dark",["EBase"],(p,0,0)),e);parent(join("StanchionEndCap__Gold","Gold",["ECap","EBall"],(p,0,0)),e)
def ends(ph,r):
    f=C["fence"];P2=f["pitch"]/2;ux=f["up"];fw,fh,fd=f["foot"]
    for s in (-1,1):
        q=str(s);box("Foot"+q,"Rubber",(s*(P2-.01-fw/2),fh/2,0),(fw,fh,fd),.02)
        cyl("Upright"+q,"Metal",(s*ux,.03,0),(s*ux,ph,0),r,8);cyl("UpCap"+q,"Rubber",(s*ux,ph,0),(s*ux,ph+.03,0),r+.006,8)
    for y in (.5,1.75):box("Clamp"+str(y),"Metal",(P2,y,0),(.15,.06,.08));cyl("Bolt"+str(y),"Gold",(P2,y,-.055),(P2,y,.055),.013,6)
def fence():
    f=C["fence"];ux=f["up"];y0,y1=f["scrim"];ym=(y0+y1)/2
    ends(f["h"],.024)
    for n,y in (("RailTop",y1-.03),("RailBot",y0-.02)):
        cyl(n,"Metal",(-ux,y,0),(ux,y,0),.022,8)
        for x in (-1.1,-.37,.37,1.1):tube("Tie"+n+str(x),"White",ring((x,y,-.012),.038,"yz",6),.005,4,True)
    box("Scrim","CanvasDark",(0,ym,-.03),(2*ux-.06,y1-y0,.012))
    for y in (y1-.08,y0+.08):box("Band"+str(y),"CanvasGold",(0,y,-.03),(2*ux-.06,.07,.016))
    lab("Text","Gold",f["text"],(0,ym,-.037),.36);lab("TextBack","Gold",f["text"],(0,ym,-.023),.36,R=BK)
def gate():
    g=C["gate"];f=C["fence"];ux=f["up"];ph=g["post"];st=g["stile"];hx=g["hinge"];cx=g["caster"]
    ends(ph,.03)
    box("Header","Dark",(0,ph-.06,0),(2*ux+.06,.1,.08));box("SignBoard","Gold",(0,ph-.28,-.02),(2.2,.34,.04));lab("SignText","Dark",g["text"],(0,ph-.28,-.041),.2)
    for y in (.45,1.65):box("Hinge"+str(y),"Metal",((-ux-st-.02)/2,y,0),(ux-st-.02,.06,.05))
    box("Catch","Gold",(1.42,1.05,0),(.06,.08,.06))
    fr=[]
    for s in (-1,1):fr.append("Stile"+str(s));cyl(fr[-1],"Metal",(s*st,.14,0),(s*st,1.98,0),.024,8)
    for y in (.14,1.98):fr.append("Rail"+str(y));cyl(fr[-1],"Metal",(-st,y,0),(st,y,0),.024,8)
    for d in (-1,1):fr.append("Fork"+str(d));box(fr[-1],"Metal",(cx+d*.025,.095,0),(.01,.09,.05))
    leaf=join("CrewGateLeaf__Metal","Metal",fr,(hx,0,0))
    box("GScrim","CanvasDark",(0,1.06,0),(2*st-.02,1.8,.012));parent(join("CrewGateScrim__CanvasDark","CanvasDark",["GScrim"],(hx,0,0)),leaf)
    for y in (1.86,.28):box("GBand"+str(y),"Gold",(0,y,0),(2*st-.02,.07,.016))
    lab("GText","Gold",f["text"],(0,1.07,-.0075),.3);lab("GTextBack","Gold",f["text"],(0,1.07,.0075),.3,R=BK)
    tube("GHandle","Gold",[(st,.95,-.02),(st,.95,-.08),(st,1.15,-.08),(st,1.15,-.02)],.012,6)
    parent(join("CrewGateTrim__Gold","Gold",["GBand1.86","GBand0.28","GText","GTextBack","GHandle"],(hx,0,0)),leaf)
    parent(cyl("CrewGateCaster__Rubber","Rubber",(cx-.02,.06,0),(cx+.02,.06,0),.06,12,piv=(cx,.06,0),tag="keep"),leaf)
def lockers():
    l=C["lock"];dm=l["doors"];n=len(dm);w,h,d=l["w"],l["h"],l["d"];bm=l["body"];W=n*w;hw=W/2;e=.03;zf=-d/2
    box("Plinth","Dark",(0,.05,.02),(W,.1,d-.06))
    box("Back",bm,(0,(.1+h)/2,d/2-e/2),(W+2*e,h-.1,e))
    for s in (-1,1):box("Side"+str(s),bm,(s*(hw+e/2),(.1+h)/2,0),(e,h-.1,d))
    for y in (h-e/2,.1+e/2):box("Panel"+str(y),bm,(0,y,-e/2),(W,e,d-e))
    for i in range(1,n):box("Div"+str(i),bm,(-hw+i*w,(.1+h)/2,-e/2),(.02,h-.1-2*e,d-e))
    box("Fascia","Dark",(0,h+.06,0),(W+2*e+.02,.12,d+.02));lab("FasciaText","Gold","CREW",(0,h+.06,-d/2-.011),.09)
    for i in range(n):
        xc=-hw+(i+.5)*w;q=str(i+1);xl=-hw+i*w+.005;dw=w-.01;pv=(xl,.12,zf-.01)
        box("Shelf"+q,bm,(xc,h-.35,-e/2+.01),(w-.02,.02,d-e-.02))
        tube("Hook"+q,"Metal",[(xc,h-.45,d/2-e),(xc,h-.45,d/2-e-.06),(xc,h-.40,d/2-e-.08)],.008,5)
        door=box("LockerDoor"+q+"__"+dm[i],dm[i],(xc,(.12+h-.02)/2,zf-.01),(dw,h-.14,.02),.004,piv=pv,tag="keep")
        parent(box("LockerHandle"+q+"__Metal","Metal",(xl+dw-.06,h/2+.05,zf-.0375),(.03,.16,.035),.005,piv=pv,tag="keep"),door)
        ns=["Num"+q];lab(ns[0],"Dark",q,(xc,h-.55,zf-.021),.12)
        for j in range(4):
            for y in (h-.22-j*.05,.3+j*.05):ns.append("Vent"+q+str(j)+str(y));box(ns[-1],"Dark",(xc,y,zf-.022),(dw-.14,.018,.006))
        parent(join("LockerDetail"+q+"__Dark","Dark",ns,pv),door)
    puff("Shirt","Gold",(-hw+1.5*w,h-.34+.03,-.02),(.3,.05,.3))
def bag():
    b=C["bag"];R=b["rings"];hd=lambda y:lerp(R,y,2)/2
    loft("Body","CanvasRose",[rr(w,d,y) for y,w,d in R],smooth=True)
    ico("Pocket","CanvasMint",(0,.22,-hd(.22)+.02),.1,2,(1.7,1.25,.7),smooth=True)
    ico("Flap","CanvasGold",(0,.58,-.02),.1,2,(2.1,.6,1.6),smooth=True)
    cyl("Roll","CanvasCream",(-.28,.69,-.03),(.28,.69,-.03),.075,10,smooth=True)
    for x in (-.16,.16):tube("RollStrap"+str(x),"Dark",ring((x,.69,-.03),.082,"yz",8),.012,4,True)
    for x in (-.1,.1):tube("Strap"+str(x),"Dark",[(x,.56,-.16),(x,.46,-hd(.46)-.004),(x,.37,-hd(.37)-.012)],.012,4);box("Buckle"+str(x),"Gold",(x,.40,-hd(.40)-.016),(.05,.035,.015))
    for s in (-1,1):ico("SidePocket"+str(s),"CanvasGold",(s*.235,.2,0),.1,2,(.65,1.1,1.0),smooth=True)
    cyl("Bottle","Mint",(.25,.16,0),(.25,.40,0),.035,8);cyl("BottleCap","White",(.25,.40,0),(.25,.43,0),.025,6)
    for x in (-.1,.1):tube("Shoulder"+str(x),"Dark",[(x*1.1,y,hd(y)+o) for y,o in ((.56,-.006),(.46,.035),(.32,.05),(.18,.035),(.08,-.004))],.02,6)
    tube("Handle","Dark",[(-.07,.585,.085),(-.06,.67,.09),(0,.70,.09),(.06,.67,.09),(.07,.585,.085)],.014,6)
    tube("Sleeve","Blue",[(.1,.54,-.08),(.17,.52,-.17),(.23,.44,-.2)],.032,6);cyl("Cuff","White",(.22,.455,-.197),(.24,.425,-.203),.036,6)
    tube("TagString","White",[(-.06,.67,.09),(-.16,.62,.1),(-.24,.53,.1),(-.275,.47,.08)],.006,4)
    box("Tag","Gold",(-.28,.38,.07),(.012,.18,.2),.004);lab("TagText","Dark",b["tag"],(-.2875,.38,.07),.05,R=BX)
    st=[(math.sin(2*PI*i/10)*(.045 if i%2==0 else .02),math.cos(2*PI*i/10)*(.045 if i%2==0 else .02)) for i in range(10)]
    loft("Star","Gold",[[(-.07+x,.24+y,z) for x,y in st] for z in (-.21,-.19)])
    cyl("Smiley","Mint",(0,.44,-.13),(0,.44,-hd(.44)-.012),.05,12)
    for x in (-.016,.016):box("Eye"+str(x),"Dark",(x,.455,-hd(.44)-.013),(.01,.016,.004))
    tube("Smile","Dark",[(-.022,.43,-hd(.44)-.013),(0,.418,-hd(.44)-.013),(.022,.43,-hd(.44)-.013)],.004,4)
def truck():
    t=C["truck"];rx=t["rail"];tp=t["top"];wx,wy,wz,ww=t["wheel"];sw,sh,sd=t["spk"]
    for s in (-1,1):
        q=str(s);cyl("Rail"+q,"Metal",(s*rx,.015,0),(s*rx,tp,0),.02,8);slab("Bracket"+q,"Metal",(s*rx,.22,0),(s*rx,wy,wz),.03,.03)
        wh=cyl("TruckWheel"+("L" if s<0 else "R")+"__Rubber","Rubber",(s*(wx-ww),wy,wz),(s*(wx+ww),wy,wz),wy,14,piv=(s*wx,wy,wz),tag="keep")
        parent(cyl("TruckHub"+("L" if s<0 else "R")+"__Gold","Gold",(s*(wx+ww),wy,wz),(s*(wx+ww+.015),wy,wz),.065,10,piv=(s*wx,wy,wz),tag="keep"),wh)
    tube("Handle","Metal",[(-rx,tp,0),(-rx,tp+.11,.05),(-.14,tp+.17,.08),(.14,tp+.17,.08),(rx,tp+.11,.05),(rx,tp,0)],.02,8)
    tube("Grip","Rubber",[(-.12,tp+.17,.08),(.12,tp+.17,.08)],.027,8)
    for y in (.3,.62,.95):cyl("Cross"+str(y),"Metal",(-rx,y,0),(rx,y,0),.014,6)
    box("Nose","Metal",(0,.0075,-.15),(.48,.015,.3));cyl("Axle","Metal",(-wx-ww-.02,wy,wz),(wx+ww+.02,wy,wz),.015,6)
    z0=-.02-sd/2;fz=-.02-sd;box("Speaker","Dark",(0,.015+sh/2,z0),(sw,sh,sd),.02);box("Grille","Metal",(0,.375,fz-.006),(sw-.06,sh-.1,.012))
    cyl("Woofer","Rubber",(0,.30,fz-.012),(0,.30,fz-.025),.15,14);tube("WooferRing","Gold",ring((0,.30,fz-.02),.152,"xy",12),.012,4,True);ico("DustCap","Metal",(0,.30,fz-.025),.05,2,(1,1,.5))
    box("Tweeter","Rubber",(0,.6,fz-.0195),(.16,.08,.015))
    for x in (-1,1):
        for z in (-.05,fz+.03):box("Corner"+str(x)+str(z),"Metal",(x*(sw/2-.02),.015+sh-.02,z),(.06,.06,.06))
    sy=.52;box("StrapF","CanvasGold",(0,sy,fz-.02),(sw+.03,.05,.012));box("StrapB","CanvasGold",(0,sy,.028),(sw+.03,.05,.012));box("Ratchet","Metal",(.1,sy,fz-.03),(.07,.07,.02))
    for s in (-1,1):box("StrapS"+str(s),"CanvasGold",(s*(sw/2+.006),sy,(fz-.02+.022)/2),(.012,.05,.022-fz+.02))
    lab("SpkText","Gold","STAGE B",(-sw/2-.0015,.25,z0),.07,R=BX)
def blob():
    c=C["blob"];pw=c["pallet"];T=.145;pr=[(r,T+y) for r,y in c["prof"]];rad=lambda y:lerp([(b,a) for a,b in pr],y)
    for i in range(5):box("Board"+str(i),"Wood",(0,T-.0125,-.42+i*.21),(pw,.025,.17))
    for x in (-.455,0,.455):box("Stringer"+str(x),"Bark",(x,.0725,0),(.09,.095,pw))
    for z in (-.44,0,.44):box("Runner"+str(z),"Wood",(0,.0125,z),(pw,.025,.12))
    box("Fascia","Wood",(0,.0725,-pw/2-.01),(pw,.095,.02));lab("PalletText","Cream",c["tag"],(0,.0725,-pw/2-.021),.065)
    lathe("Body","Mint",pr,(0,0,0),20,smooth=True)
    ex,ey=c["eye"];ey+=T
    for s,dx,dy in ((-1,.03,.03),(1,-.03,-.02)):
        ez=-math.sqrt(rad(ey)**2-ex**2)+.03;q=str(s)
        ico("Eye"+q,"White",(s*ex,ey,ez),.12,2,(1,1.15,.6),smooth=True);pz=ez-.068
        ico("Pupil"+q,"Dark",(s*ex+dx,ey+dy,pz),.055,2,(1,1,.5),smooth=True);ico("Glint"+q,"White",(s*ex+dx-.02,ey+dy+.02,pz-.024),.015,1)
        cy=T+.56;ico("Cheek"+q,"Rose",(s*.30,cy,-math.sqrt(rad(cy)**2-.09)+.012),.06,2,(1.3,.8,.4),smooth=True)
        ico("Arm"+q,"Mint",(0,0,0),.11,2,(1.9,.85,.85),T=M.Translation(A(s*.60,T+.60,0))@rot(-s*35,"z"),smooth=True)
        ico("Foot"+q,"Mint",(s*.18,T+.03,-.30),.12,2,(1.1,.5,1.4),smooth=True)
    mp=[(.19*u,T+.50-.07*(1-u*u)) for u in (i/4-1 for i in range(9))]
    tube("Mouth","Dark",[(x,y,-math.sqrt(rad(y)**2-x*x)+.004) for x,y in mp],.022,6);ico("Tongue","Rose",(.03,T+.425,-math.sqrt(rad(T+.425)**2-.0009)+.005),.05,2,(1.2,.6,.5),smooth=True)
    tp=pr[-1][1];ant=[(0,tp-.02,0),(.03,tp+.1,-.02),(.08,tp+.18,0),(.05,tp+.26,.03),(-.01,tp+.3,.01)]
    tube("Antenna","Gold",ant,.02,6);ico("AntennaBall","StageGlowGold",ant[-1],.07,2,smooth=True)
    by=T+c["band"];br=rad(by)+.004;tube("Band","CanvasGold",ring((0,by,0),br,"xz",20),.028,6,True)
    for a in (45,135,225,315):
        ca,sa=math.cos(math.radians(a)),math.sin(math.radians(a));cx,cz=math.copysign(.44,ca),math.copysign(.44,sa)
        tube("Rope"+str(a),"Cream",[(br*ca,by,br*sa),(cx,T+.03,cz)],.014,5);box("Cleat"+str(a),"Metal",(cx,T+.015,cz),(.08,.03,.05))
    vy=T+.18;vr=rad(vy);cyl("Valve","White",(-vr+.01,vy,-.05),(-vr-.07,vy,-.05),.035,8);cyl("ValveCap","Rose",(-vr-.07,vy,-.05),(-vr-.09,vy,-.05),.045,8)
def crate():
    c=C["crate"];W,H,D=c["size"];sk=.05;ph=H/3;hw,hd=W/2,D/2
    for s in (-1,1):box("Skid"+str(s),"Bark",(s*.3,sk/2,0),(.09,sk,D))
    box("Core","Dark",(0,sk+H/2,0),(W-.02,H,D-.02))
    for i in range(3):
        y=sk+(i+.5)*ph;q=str(i)
        for s in (-1,1):box("PlankZ"+q+str(s),"Wood",(0,y,s*(hd-.01)),(W,ph-.012,.02));box("PlankX"+q+str(s),"Wood",(s*(hw-.01),y,0),(.02,ph-.012,D))
        box("Lid"+q,"Wood",(0,sk+H,(i-1)*D/3),(W,.02,D/3-.012))
    for s in (-1,1):
        z=s*(hd+.01)
        for x in (-1,1):box("BattenV"+str(s)+str(x),"Bark",(x*(hw-.04),sk+H/2,z),(.08,H+.02,.02))
        for y in (sk+.04,sk+H-.04):box("BattenH"+str(s)+str(y),"Bark",(0,y,z),(W-.16,.08,.02))
        x0=s*hw;ar=[(-.022,.22),(.022,.22),(.022,.40),(.07,.40),(0,.52),(-.07,.40),(-.022,.40)]
        for zc in (-.1,.1):prism("Arrow"+str(s)+str(zc),"Dark",[(z+zc,y) for z,y in ar],x0-s*.002,x0+s*.004)
        box("Under"+str(s),"Dark",(x0+s*.001,.17,0),(.006,.03,.34))
    box("LabelPlate","PaintCream",(0,sk+H/2,-hd-.005),(.56,.2,.01));lab("LabelText","Rose",c["text"],(0,sk+H/2,-hd-.011),.12)
def spk(p,x,y,z,w,h,d,nw):
    box(p,"Dark",(x,y+h/2,z),(w,h,d),.02);fz=z-d/2;box(p+"Grille","Metal",(x,y+h/2,fz-.005),(w-.08,h-.08,.01))
    for i in range(nw):
        wy=y+h*(i+.5)/nw;r=min(w,h/nw)*.36;cyl(p+"Cone"+str(i),"Rubber",(x,wy,fz-.01),(x,wy,fz-.025),r,14);tube(p+"Ring"+str(i),"Gold",ring((x,wy,fz-.02),r+.01,"xy",12),.014,4,True)
def can(p,c,aim,g):
    dv=(V(aim)-V(c)).normalized();cyl(p,"Dark",ad(c,dv,-.16),ad(c,dv,.12),.1,10);cyl(p+"Lens",g,ad(c,dv,.12),ad(c,dv,.135),.085,10)
def stage():
    s=C["stage"];W,D,H=s["deck"];hw,hd=W/2,D/2;z0,z1,n,run=s["stair"];tx,tz,th=s["tower"];rs=s["rise"]
    box("Deck","Dark",(0,H-.04,0),(W,.08,D))
    for x in (-hw+.2,0,hw-.2):
        for z in (-hd+.2,hd-.2):cyl("Leg"+str(x)+str(z),"Metal",(x,0,z),(x,H-.08,z),.05,6)
    for z in (-1,1):box("SkirtZ"+str(z),"CanvasDark",(0,(H-.04)/2,z*(hd+.01)),(W+.04,H-.04,.02));box("BandZ"+str(z),"Gold",(0,H-.04,z*(hd+.015)),(W+.06,.08,.03))
    for x in (-1,1):box("SkirtX"+str(x),"CanvasDark",(x*(hw+.01),(H-.04)/2,0),(.02,H-.04,D+.04));box("BandX"+str(x),"Gold",(x*(hw+.015),H-.04,0),(.03,.08,D+.06))
    box("LED","StageGlowMint",(0,H-.13,-hd-.03),(W,.03,.02));lab("SkirtText","Gold",s["text"],(0,.42,-hd-.021),.38)
    rise=H/(n+1);zc=(z0+z1)/2;x0=hw+.02
    for i in range(n):box("Tread"+str(i),"Metal",(x0+run*(n-1-i)+run/2,rise*(i+1)-.02,zc),(run,.04,z1-z0))
    xe=x0+run*n+.06;ys=lambda x:H*(xe-x)/(xe-x0)
    for z in (z0,z1):
        slab("Stringer"+str(z),"Dark",(xe,0,z),(x0-.02,H,z),.22,.05);xa,xb=x0+1.0,x0+.15
        for x in (xa,xb):cyl("RailPost"+str(z)+str(x),"Metal",(x,ys(x),z),(x,ys(x)+.95,z),.025,6)
        tube("Rail"+str(z),"Gold",[(xa,ys(xa)+.95,z),(xb,ys(xb)+.95,z)],.03,6)
    for sx in (-1,1):
        X=sx*tx;q=str(sx);box("TowerPlate"+q,"Metal",(X,.015,tz),(.6,.03,.6));box("TowerCap"+q,"Metal",(X,th+.04,tz),(.42,.08,.42))
        cs=[(X+a*.15,tz+b*.15) for a,b in ((-1,-1),(1,-1),(1,1),(-1,1))];ny=9;sg=(th-.03)/ny
        for j,(cx,cz) in enumerate(cs):cyl("Chord"+q+str(j),"Metal",(cx,.03,cz),(cx,th,cz),.028,6)
        for lv in range(ny):
            y=.03+sg*lv
            for j in range(4):
                (ax,az),(bx,bz)=cs[j],cs[(j+1)%4];cyl("Rung"+q+str(lv)+str(j),"Metal",(ax,y+sg,az),(bx,y+sg,bz),.012,4);cyl("Diag"+q+str(lv)+str(j),"Metal",*(((ax,y,az),(bx,y+sg,bz)) if lv%2 else ((bx,y,bz),(ax,y+sg,az))),.012,4)
    na=16;base=th+.08;P=lambda a:(tx*math.cos(a),base+rs*math.sin(a));N=lambda a:V((math.cos(a)/tx,math.sin(a)/rs)).normalized()
    ch={k:[] for k in ("f","b","o")}
    for i in range(na+1):
        a=PI*i/na;(x,y),nv=P(a),N(a)
        ch["f"].append((x-nv.x*.13,y-nv.y*.13,tz-.15));ch["b"].append((x-nv.x*.13,y-nv.y*.13,tz+.15));ch["o"].append((x+nv.x*.13,y+nv.y*.13,tz))
    for k,pts in ch.items():tube("Arch"+k,"Metal",pts,.028,6)
    for i in range(na):
        f,b=ch["f"][i],ch["b"][i];cyl("ArchRung"+str(i),"Metal",f,b,.012,4);cyl("ArchLf"+str(i),"Metal",f,ch["o"][i+1-(i%2)],.012,4);cyl("ArchLb"+str(i),"Metal",b,ch["o"][i+1-(i%2)],.012,4)
    gl=("StageGlowRose","StageGlowGold","StageGlowMint","StageGlowGold","StageGlowRose")
    for i,u in enumerate(s["cans"]):
        a=PI*u;(x,y),nv=P(a),N(a);cp=(x-nv.x*.13,y-nv.y*.13-.03,tz);c=(cp[0],cp[1]-.32,tz)
        box("Clamp"+str(i),"Metal",cp,(.08,.08,.4));cyl("Hanger"+str(i),"Metal",cp,c,.015,4);can("Can"+str(i),c,(x*.4,0,-7),gl[i])
    for sx in (-1,1):box("FloorStand"+str(sx),"Metal",(sx*1.8,H+.025,-hd+.3),(.26,.05,.22));can("FloorCan"+str(sx),(sx*1.8,H+.15,-hd+.3),(sx*2.4,th,tz+1.5),"StageGlowMint")
    sx0,sz0=s["spk"]
    for sx in (-1,1):
        X=sx*sx0;q=str(sx);spk("Sub"+q+"A",X,H,sz0,1.0,.72,.8,1);spk("Sub"+q+"B",X,H+.72,sz0,1.0,.72,.8,1);spk("Top"+q,X,H+1.44,sz0,.8,.9,.64,2)
    dw,dh,dd,dz=s["dj"];box("Booth","Dark",(0,H+dh/2,dz),(dw,dh,dd));box("BoothTop","Wood",(0,H+dh+.025,dz),(dw+.1,.05,dd+.1),.01)
    for y in (.3,.6):box("Glow"+str(y),"StageGlowRose",(0,H+y,dz-dd/2-.006),(dw-.2,.04,.012))
    ty=H+dh+.05
    for sx in (-1,1):box("Deck"+str(sx),"Dark",(sx*.55,ty+.03,dz),(.5,.06,.4));cyl("Platter"+str(sx),"Metal",(sx*.55,ty+.06,dz),(sx*.55,ty+.08,dz),.15,14)
    box("Mixer","Metal",(0,ty+.04,dz),(.34,.08,.4))
    for j in range(4):cyl("Knob"+str(j),"Gold",(-.1+j*.066,ty+.08,dz+.1),(-.1+j*.066,ty+.11,dz+.1),.018,6)
    for x in (-.07,.07):box("Fader"+str(x),"White",(x,ty+.09,dz-.07),(.03,.02,.05))
    for sx in (-1,1):cyl("BackPost"+str(sx),"Metal",(sx*3.7,H,hd-.15),(sx*3.7,H+3.2,hd-.15),.05,8)
    cyl("BackBar","Metal",(-3.75,H+3.1,hd-.15),(3.75,H+3.1,hd-.15),.05,8);box("Banner","CanvasMint",(0,H+1.85,hd-.17),(7.2,2.4,.02))
    cyl("Sun","Gold",(-.45,H+1.85,hd-.18),(-.45,H+1.85,hd-.19),.75,24);mo=moon((.35,H+1.95),.75,(.75,H+2.2),.62);loft("Moon","Cream",[[(x,y,z) for x,y in mo] for z in (hd-.19,hd-.2)])
def moon(c1,R1,c2,R2,n=14):
    dx,dy=c2[0]-c1[0],c2[1]-c1[1];d=math.hypot(dx,dy);a=(R1*R1-R2*R2+d*d)/(2*d);f=math.atan2(dy,dx);al=math.acos(a/R1);be=math.acos((d-a)/R2)
    return [(c1[0]+R1*math.cos(t),c1[1]+R1*math.sin(t)) for t in (f+al+(2*PI-2*al)*i/n for i in range(n+1))]+[(c2[0]+R2*math.cos(t),c2[1]+R2*math.sin(t)) for t in (f+PI+be-2*be*i/n for i in range(1,n))]
def scene(path,items,eye,at,lens=28,g=(26,26)):
    tmp=[];bpy.context.view_layer.update()
    for k,x,z,r in items:
        Wm=RZ@M.Translation(A(x,0,z))@rot(r,"y")@RZ;cp={}
        for o in COL[k].objects:
            if o.get("tag")!="shell":c=o.copy();c["temp"]=1;bpy.context.scene.collection.objects.link(c);cp[o]=c;tmp.append(c)
        for o,c in cp.items():
            if o.parent in cp:c.parent=cp[o.parent]
            else:c.matrix_world=Wm@o.matrix_world
    KIT[0]=items[0][0];gr=box("SceneGround","Stone",(0,-.03,0),(g[0],.06,g[1]));gr["temp"]=1;del PARTS[KIT[0]]["SceneGround"]
    gr.data.materials[0]=bpy.data.materials.get("FK_Ground") or bpy.data.materials.new("FK_Ground");gr.data.materials[0].diffuse_color=(*KIT_CONFIG["ground"],1);tmp.append(gr)
    render(path,camera(eye,at,lens),lambda o:o.get("temp"))
    for o in tmp:bpy.data.objects.remove(o,do_unlink=True)
def pose(rots):
    old={n:tuple(bpy.data.objects[n].rotation_euler) for n in rots}
    for n,(i,a) in rots.items():bpy.data.objects[n].rotation_euler[i]=a
    return old
kit("AH04_CheckpointBooth");booth()
kit("AH04_BagSearchTable");table()
kit("AH04_BarrierArm");barrier()
kit("AH04_QueueStanchion");stanchion()
kit("AH04_BackstageFence");fence()
kit("AH04_CrewGate");gate()
kit("AH04_CrewLockers");lockers()
kit("AH04_BelongingsBag");bag()
kit("AH04_HandTruckSpeaker");truck()
kit("AH04_InflatableCargo");blob()
kit("AH04_FragileCrate");crate()
kit("AH04_SideStage",[("Ground","Stone",(0,-.05,0),(18,.1,18),0)]);stage()
RV=C["rev"];E=C["eye"];nm={k:k[5:] for k in K}
gallery(K[7:11],RV+"/gallery_objectives.png",2,names=nm)
gallery((K[6],K[0],K[3],K[1]),RV+"/gallery_security.png",2,names=nm)
gallery((K[2],K[4],K[5]),RV+"/gallery_barriers.png",3,names=nm)
old=pose({"BarrierArm__White":(1,1.3),"CrewGateLeaf__Metal":(2,-1.2),**{"LockerDoor%d__%s"%(i+1,m):(2,-1.6-.3*i) for i,m in enumerate(C["lock"]["doors"])}})
gallery((K[2],K[5],K[6]),RV+"/pivot_check.png",3,names=nm)
for n,r in old.items():bpy.data.objects[n].rotation_euler=r
CK=[(K[0],0,0,0),(K[2],1.35,-1.25,0),(K[1],-2.6,-2.2,90),(K[3],-.95,-2.9,90),(K[3],-.95,-4.4,90),(K[3],.95,-2.9,90),(K[3],.95,-4.4,90)]
scene(RV+"/checkpoint_player.png",CK,(-2.2,E,-9.0),(.4,1.1,-1.5),30)
scene(RV+"/checkpoint_overview.png",CK,(-7.5,6.5,-9.5),(.2,.8,-1.8),30)
shot(K[11],RV+"/sidestage_crowd.png",(0,E,-13),(0,2.6,0),26)
shot(K[11],RV+"/sidestage_overview.png",(-10,7.5,-12),(0,2.0,0),28)
shot(K[11],RV+"/sidestage_stairs.png",(10,4,8),(2.5,1.5,.5),30)
shot(K[9],RV+"/inflatable_closeup.png",(-1.6,E,-2.4),(0,.7,0),30)
shot(K[7],RV+"/bag_closeup.png",(-.9,.9,-1.2),(0,.33,0),30)
shot(K[0],RV+"/booth_door.png",(4.2,E,-2.8),(.3,1.2,0),30)
shot(K[4],RV+"/fence_back.png",(2.2,E,4.8),(0,1.1,0),30)
piv={k:{o.name:[round(v,4) for v in U(o.matrix_world.translation)] for o in COL[k].objects if o.get("tag")=="keep"} for k in K}
ex={k:{"pivots":piv[k]} for k in K if piv[k]}
ex.setdefault(K[3],{})["tilingPitch"]=C["stan"]["pitch"];ex[K[4]]={"tilingPitch":C["fence"]["pitch"]};ex[K[5]]["tilingPitch"]=C["fence"]["pitch"]
res=finish(K,C["fbx"],C["src"],C["man"],extra=ex)
for k,v in res.items():print(k,v["triangles"],v["renderers"],v["separate"])
