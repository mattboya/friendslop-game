CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/CampInteriors.blend","man":"ArtSource/camp-interiors-manifest.json","rev":"artifacts/camp-interiors",
"room":{"half":2.81,"floor":.08,"ceil":3.01,"door":.9,"exit":-2.35,"eye":1.62,"spawn":-1.5,"lens":17.5},
"car":{"seat_x":1.36,"cush":(.56,.10,1.2,.3,1.1),"ped":(.84,.8),"back":(1.16,1.2,.26,12),"head":(.6,.3,.24,.08),"ch":.06,"dash":((1.40,.80),(1.34,1.26),(1.42,1.33),(2.38,1.40),(2.40,.80)),"ws":((1.38,2.28),(2.66,2.76),2.2,.04),"wheel":(-1,1.36,1.02,.35,.045,25,.42),"cowl":(-1.0,1.10,.26,.40),"gauge":(-1.26,-.74,1.43,.10),"console":(.70,.62,-.36,1.36),"stack":(.84,.60,1.30,1.24),"door":(.40,1.30,-.95,1.35),"win":(1.40,2.40,-.92,1.32),"bench":(1.0,.24,.70,.95),"mirror":(2.44,2.42),"bobble":(1.55,1.75,.13),"mats":(1.30,1.0,1.15)},
"tent":{"eave":1.78,"ridge":2.98,"sag":.07,"fab":.05,"seams":(-1.62,1.62),"pole":.04,"mat_x":1.15,"mat":(1.25,.05,3.48,.10),"bag":((-1.45,.66,.14),(-1.1,.80,.20),(-.4,.94,.25),(.4,.98,.26),(.95,.96,.24),(1.20,.90,.20)),"arc":9,"pillow":(.85,.18,.36,1.58),"throw":(-1.20,-.80,.05),"lantern":(1.9,2.08,.13,.25),"cooler":(.85,.60,.70,2.10,.08),"pocket":(-2.25,-1.59,.49,.53,1.66),"door":(.75,1.85)}}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
C=CONFIG;RM=C["room"];H,FL,CE=RM["half"],RM["floor"],RM["ceil"]
KITS=("FestivalCarInterior","FestivalTentInterior")
setup(KITS)
def room(k,wall,floor):
    kit(k,[(n,floor if n=="ShellFloor" else wall,c,s,cut) for n,c,s,cut in (("ShellFloor",(0,.02,0),(5.8,.12,5.8),0),("ShellRear",(0,1.55,2.9),(5.8,3.1,.18),0),("ShellFrontL",(-1.9,1.55,-2.9),(2,3.1,.18),1),("ShellFrontR",(1.9,1.55,-2.9),(2,3.1,.18),1),("ShellHeader",(0,2.83,-2.9),(1.8,.55,.18),1),("ShellLeft",(-2.9,1.55,0),(.18,3.1,5.8),1),("ShellRight",(2.9,1.55,0),(.18,3.1,5.8),0),("ShellCeiling",(0,3.1,0),(5.8,.18,5.8),1))])
def car():
    c=C["car"];ch=c["ch"];prof=c["dash"]
    def dtop(z):(z0,y0),(z1,y1)=prof[2],prof[3];return y0+(z-z0)/(z1-z0)*(y1-y0)
    def dface(y):(z0,y0),(z1,y1)=prof[0],prof[1];return z0+(y-y0)/(y1-y0)*(z1-z0)
    box("Carpet","Rubber",(0,FL+.015,0),(2*H,.03,2*H))
    box("Headliner","Cream",(0,CE-.0125,0),(2*H,.025,2*H),cut=True)
    cy,cz,cw,chh,cd=c["cush"];bw,bh,bt,rc=c["back"];hw,hh,ht,hg=c["head"];r=math.radians(rc);d=(0,math.cos(r),-math.sin(r));nr=(0,math.sin(r),math.cos(r));mw,md,mz=c["mats"]
    for sx in (-c["seat_x"],c["seat_x"]):
        s="L" if sx<0 else "R"
        box("SeatPedestal"+s,"Dark",(sx,(FL+.03+cy-chh/2)/2,cz),(c["ped"][0],cy-chh/2-FL-.03,c["ped"][1]))
        box("SeatCushion"+s,"Rose",(sx,cy,cz),(cw,chh,cd),ch)
        for b in (-1,1):box("SeatBolster"+s+str(b),"Rose",(sx+b*(cw/2-.09),cy+chh/2+.04,cz),(.18,.10,cd-.06),.03)
        a=(sx,cy+chh/2-.11,cz-cd/2+.09);e=ad(a,d,bh)
        slab("SeatBack"+s,"Rose",a,e,bw,bt,ch)
        for b in (-1,1):
            o=(b*(bw/2-.08),nr[1]*(bt/2+.05),nr[2]*(bt/2+.05))
            slab("SeatWing"+s+str(b),"Rose",ad(a,o,1),ad(e,o,1),.18,.12,.03)
        slab("MapPocket"+s,"Dark",ad(ad(a,d,.25),nr,-bt/2-.01),ad(ad(a,d,.80),nr,-bt/2-.01),bw-.3,.03)
        slab("Headrest"+s,"Rose",ad(e,d,hg),ad(e,d,hg+hh),hw,ht,.05)
        for b in (-.18,.18):cyl("HeadPost"+s+str(b),"Metal",ad(ad(e,d,-.06),(1,0,0),b),ad(ad(e,d,hg+.04),(1,0,0),b),.025,6)
        box("FloorMat"+s,"Dark",(sx,FL+.04,mz),(mw,.02,md))
    kw,kt,k0,k1=c["console"]
    box("Console","Dark",(0,(FL+.03+kt)/2,(k0+k1)/2),(kw,kt-FL-.03,k1-k0))
    cz0=k0+.41
    cyl("Cup","Rose",(0,kt,cz0),(0,kt+.33,cz0),.10,10,.12);cyl("CupLid","White",(0,kt+.33,cz0),(0,kt+.36,cz0),.125,10)
    cyl("Straw","Mint",(.03,kt+.34,cz0),(.08,kt+.58,cz0-.03),.015,6)
    cyl("ShiftBoot","Rubber",(0,kt,.85),(0,kt+.08,.85),.09,8,.05);cyl("ShiftStick","Metal",(0,kt+.06,.85),(0,kt+.38,.82),.025,6);ico("ShiftKnob","Gold",(0,kt+.40,.82),.07)
    loft("Dash","Metal",[[(x,y,z) for z,y in prof] for x in (-H,H)])
    sw,sb,st,sf=c["stack"]
    box("CenterStack","Dark",(0,(sb+st)/2,(sf+1.42)/2),(sw,st-sb,1.42-sf))
    box("Radio","Metal",(0,1.02,sf-.015),(.64,.26,.03));box("RadioScreen","Glass",(0,1.06,sf-.036),(.30,.09,.012))
    for dx in (-.2,0,.2):cyl("RadioDial"+str(dx),"Gold",(dx,.96,sf-.03),(dx,.96,sf-.07),.045,8)
    for vx in (-.2,.2):
        box("Vent"+str(vx),"Rubber",(vx,1.22,sf-.006),(.32,.11,.012))
        for i in range(3):box("VentSlat"+str(vx)+str(i),"Metal",(vx,1.19+i*.03,sf-.018),(.30,.012,.012))
    gz=dface(1.02);box("Glovebox","Cream",(1.10,1.02,gz-.01),(1.00,.30,.03));box("GloveLatch","Gold",(1.10,1.13,gz-.035),(.16,.05,.02))
    cx,cww,chc,cdc=c["cowl"];cb=dtop(1.42)-.02
    box("GaugeCowl","Dark",(cx,cb+chc/2,1.42+cdc/2),(cww,chc,cdc),.03);box("CowlBrow","Dark",(cx,cb+chc-.025,1.40),(cww+.04,.05,.12))
    g1,g2,gy,gr=c["gauge"]
    for i,gx in enumerate((g1,g2)):
        cyl("Gauge"+str(i),"Cream",(gx,gy,1.425),(gx,gy,1.40),gr,12)
        box("Needle"+str(i),"Rose",(gx+.03,gy+.02,1.395),(.12,.018,.01),R=M.Rotation(.6-i*1.1,4,"Y"))
    wx,wy,wz,wr,wt,wtl,wl=c["wheel"];tl=math.radians(wtl);n=(0,-math.sin(tl),math.cos(tl));w=(0,math.cos(tl),math.sin(tl));hub=(wx,wy,wz)
    ring=lambda t,s=1:(wx+wr*s*math.cos(t),wy+wr*s*math.sin(t)*w[1],wz+wr*s*math.sin(t)*w[2])
    tube("WheelRim","Rubber",[ring(i*math.pi/8) for i in range(16)],wt,6,True)
    for t in (0,math.pi,-math.pi/2):cyl("Spoke"+str(t),"Metal",hub,ring(t,.95),.028,6)
    cyl("Hub","Gold",ad(hub,n,-.04),ad(hub,n,.05),.11,10);cyl("Column","Dark",hub,ad(hub,n,wl),.05,8)
    gb,gt,gw,gth=c["ws"]
    slab("Windshield","Glass",(0,gb[0],gb[1]),(0,gt[0],gt[1]),2*gw,gth)
    for s in (-1,1):slab("APillar"+str(s),"Dark",(s*(gw+.02),gb[0]-.02,gb[1]),(s*(gw+.02),gt[0]+.04,gt[1]+.02),.24,.20)
    box("Header","Dark",(0,(gt[0]-.07+CE)/2,(gt[1]-.15+H)/2),(2*H,CE-gt[0]+.07,H-gt[1]+.15))
    d0,d1,z0,z1=c["door"];w0,w1,v0,v1=c["win"];zc=(z0+z1)/2
    for s in (-1,1):
        q=str(s);X=lambda o:s*(H-o)
        box("DoorCard"+q,"Metal",(X(.025),(d0+d1)/2,zc),(.05,d1-d0,z1-z0))
        box("Armrest"+q,"Rose",(X(.11),.98,.10),(.14,.08,.90),.02);box("DoorPocket"+q,"Dark",(X(.08),.55,.40),(.06,.20,.90))
        box("DoorHandle"+q,"Gold",(X(.08),1.12,-.60),(.06,.07,.28))
        cyl("CrankAxle"+q,"Metal",(X(.05),1.10,.80),(X(.13),1.10,.80),.03,6);box("CrankArm"+q,"Metal",(X(.145),1.03,.80),(.03,.18,.05))
        cyl("CrankKnob"+q,"Gold",(X(.16),.97,.80),(X(.23),.97,.80),.035,6)
        box("Sill"+q,"Metal",(X(.07),d1+.05,zc),(.14,.10,z1-z0+.1));box("SideGlass"+q,"Glass",(X(.015),(w0+w1)/2,(v0+v1)/2),(.03,w1-w0,v1-v0))
        box("FrameTop"+q,"Dark",(X(.05),w1+.05,(v0+v1)/2),(.10,.10,v1-v0+.24))
        for fz in (v0-.05,v1+.05):box("FramePost"+q+str(fz),"Dark",(X(.05),(d1+w1+.1)/2,fz),(.10,w1+.1-d1,.14))
    bx0,bh_,bd,bbh=c["bench"];bwid=H-bx0
    for s in (-1,1):
        xc=s*(bx0+H)/2;q=str(s)
        box("BenchBase"+q,"Dark",(xc,(FL+.03+.41)/2,-H+.31),(bwid,.30,.62))
        box("BenchCushion"+q,"Rose",(xc,.41+bh_/2-.01,-H+bd/2+.01),(bwid-.04,bh_,bd),.05)
        slab("BenchBack"+q,"Rose",(xc,.60,-H+.10),(xc,.60+bbh,-H+.10),bwid-.04,.20,.05)
    my,mz2=c["mirror"]
    cyl("MirrorStem","Dark",(0,CE,mz2-.02),(0,my+.08,mz2),.03,6);box("Mirror","Dark",(0,my,mz2),(.72,.20,.08),.02);box("MirrorGlass","Glass",(0,my,mz2-.045),(.64,.14,.01))
    tube("DiceCord1","White",[(-.10,my-.09,mz2),(-.10,my-.32,mz2-.02)],.008,5);box("Die1","Mint",(-.10,my-.40,mz2-.02),(.16,.16,.16),.02,M.Rotation(.35,4,"Z"))
    tube("DiceCord2","White",[(.10,my-.09,mz2),(.12,my-.42,mz2+.01)],.008,5);box("Die2","Mint",(.12,my-.50,mz2+.01),(.16,.16,.16),.02,M.Rotation(-.5,4,"Z"))
    bx_,bz,hr=c["bobble"];ty=dtop(bz);hy=ty+.33+hr-.02
    cyl("BobbleBase","Dark",(bx_,ty-.01,bz),(bx_,ty+.03,bz),.10,10);box("BobbleBody","Rose",(bx_,ty+.135,bz),(.18,.22,.14),.03)
    cyl("BobbleSpring","Metal",(bx_,ty+.23,bz),(bx_,ty+.33,bz),.025,6)
    head=ico("BobbleHead__Gold","Gold",(bx_,hy,bz),hr,piv=(bx_,hy,bz),tag="keep")
    bm=bmesh.new();hc=A(bx_,hy,bz)
    for e in (-1,1):bmesh.ops.create_icosphere(bm,subdivisions=1,radius=.028,matrix=M.Translation(hc+A(e*.048,.025,-hr*.93)))
    bmesh.ops.create_cube(bm,size=1,matrix=M.Translation(hc+A(0,-.04,-hr*.93))@M.Diagonal(A(.07,.018,.03)).to_4x4())
    face=ob("BobbleFace__Dark","Dark",bm,piv=(bx_,hy,bz),tag="keep");face.parent=head;face.location=(0,0,0)
def tent():
    t=C["tent"];E,RG,SG,FB,PL=t["eave"],t["ridge"],t["sag"],t["fab"],t["pole"]
    def prof(s,o=0,d=FB):
        p=[(s*H*(1-u),E+(RG-E)*u-SG*math.sin(math.pi*u)-o) for u in (i/6 for i in range(7))];return p+[(x,y-d) for x,y in p[::-1]]
    for s in (-1,1):
        q="L" if s<0 else "R";cut=s<0
        loft("Liner"+q,"CanvasCream" if s<0 else "CanvasRose",[[(x,y,z) for x,y in prof(s)] for z in (-H,H)],cut=cut)
        for sz in t["seams"]:loft("Seam"+q+str(sz),"CanvasGold",[[(x,y,z) for x,y in prof(s,.045,.025)] for z in (sz-.025,sz+.025)],cut=cut)
        box("Hem"+q,"Gold",(s*(H-.04),E-FB-.01,0),(.08,.06,2*H),cut=cut)
        box("GroundEdge"+q,"CanvasDark",(s*2.70,FL+.0175,0),(.08,.035,5.45))
    cyl("RidgePole","Metal",(0,RG-FB-PL,-H),(0,RG-FB-PL,H),PL,8)
    mx=t["mat_x"];mw,mh,ml,mz=t["mat"];base=FL+mh-.005;st=t["bag"];na=t["arc"]
    def wh(z):
        for (z0,w0,h0),(z1,w1,h1) in zip(st,st[1:]):
            if z0<=z<=z1:f=(z-z0)/(z1-z0);return w0+f*(w1-w0),h0+f*(h1-h0)
    def arc(z,w,h,x0,g=0,a0=0,a1=math.pi,n=na):return [(x0+(w/2+g)*math.cos(a),base+(h+g)*math.sin(a),z) for a in (a0+(a1-a0)*i/(n-1) for i in range(n))]
    for s in (-1,1):
        q="L" if s<0 else "R";x0=s*mx
        box("Mat"+q,"CanvasDark",(x0,FL+mh/2,mz),(mw,mh,ml),.02)
        loft("Bag"+q,"CanvasMint" if s<0 else "CanvasGold",[arc(z,w,h,x0) for z,w,h in st],smooth=True)
        z,w,h=st[-1];tube("BagCollar"+q,"CanvasCream",arc(z,w,h,x0,.02),.035,6)
        az=math.radians(40 if s<0 else 140);tube("BagZip"+q,"Gold",[(x0+(w/2+.01)*math.cos(az),base+(h+.01)*math.sin(az),z) for z,w,h in st],.016,5)
        t0,t1,tk=t["throw"];a0,a1=math.radians(18),math.radians(162)
        loft("Throw"+q,"CanvasRose" if s<0 else "CanvasMint",[arc(z,*wh(z),x0,tk,a0,a1,7)+arc(z,*wh(z),x0,-.005,a0,a1,7)[::-1] for z in (t0,(t0+t1)/2,t1)],smooth=True)
        pw,ph,pd,pz=t["pillow"];puff("Pillow"+q,"CanvasCream",(x0,FL+mh+ph/2,pz),(pw,ph,pd))
    lz,ly,lr,lh=t["lantern"];cap=ly+.13+lh
    cyl("LanternBase","Dark",(0,ly,lz),(0,ly+.06,lz),lr+.02,8);cyl("LanternGlobe","Gold",(0,ly+.05,lz),(0,ly+.05+lh,lz),lr,8)
    cyl("LanternCap","Dark",(0,ly+.04+lh,lz),(0,cap,lz),lr+.04,8,.06)
    tube("LanternRing","Metal",[(.035*math.cos(i*math.pi/5),cap+.03+.035*math.sin(i*math.pi/5),lz) for i in range(10)],.01,5,True)
    cyl("LanternCord","Dark",(0,cap+.055,lz),(0,RG-FB-2*PL+.01,lz),.012,5)
    dr,dy=t["door"];Z=H-.018
    tube("DoorZip","Gold",[(-dr,FL+.04,Z),(-dr,dy,Z)]+[(dr*math.cos(math.pi*(1-i/8)),dy+dr*math.sin(math.pi*(1-i/8)),Z) for i in range(1,8)]+[(dr,dy,Z),(dr,FL+.04,Z)],.02,6)
    tube("DoorZipCenter","Gold",[(0,FL+.04,Z),(0,dy+dr,Z)],.02,6);box("ZipPull","Gold",(0,dy+dr-.12,H-.035),(.06,.14,.03))
    p1,p2,pw,ph,ry=t["pocket"]
    box("OrganizerRail","CanvasDark",((p1+p2)/2,ry,H-.025),(abs(p2-p1)+pw+.1,.07,.05))
    for i,px in enumerate((p1,p2)):box("Pocket"+str(i),"CanvasGold",(px,ry-.035-ph/2,H-.04),(pw,ph,.08),.015)
    cyl("Flashlight","Metal",(p1,ry-.035-ph/2+.1,H-.09),(p1,ry+.14,H-.09),.045,8);cyl("FlashlightHead","Dark",(p1,ry+.14,H-.09),(p1,ry+.21,H-.09),.06,8)
    cyl("Toothbrush","Mint",(p2+.04,ry-.20,H-.085),(p2+.08,ry+.16,H-.085),.02,6);box("ToothbrushHead","White",(p2+.083,ry+.19,H-.085),(.05,.07,.05))
    cw,chh,cd,cz,lt=t["cooler"];hy=FL+chh-.16
    box("Cooler","Blue",(0,FL+chh/2,cz),(cw,chh,cd),.04)
    for s in (-1,1):tube("CoolerHandle"+str(s),"White",[(s*cw/2,hy,cz-.15),(s*(cw/2+.065),hy,cz-.13),(s*(cw/2+.065),hy,cz+.13),(s*cw/2,hy,cz+.15)],.022,6)
    cyl("CoolerPlug","White",(.28,FL+.10,cz-cd/2),(.28,FL+.10,cz-cd/2-.03),.035,8)
    lc=(0,FL+chh+lt/2,cz);lid=box("CoolerLid__Cream","Cream",lc,(cw+.05,lt,cd+.04),.02,piv=lc,tag="keep")
    latch=box("CoolerLatch__Gold","Gold",(0,FL+chh+.01,cz-(cd+.04)/2-.005),(.14,.12,.04),piv=lc,tag="keep");latch.parent=lid;latch.location=(0,0,0)
    cyl("Mug","Mint",(.66,FL,cz-.1),(.66,FL+.16,cz-.1),.07,10);tube("MugHandle","Mint",[(.73,FL+.12,cz-.1),(.80,FL+.12,cz-.1),(.80,FL+.05,cz-.1),(.73,FL+.05,cz-.1)],.014,5)
    for fx,ang in ((-.20,.14),(.18,-.10)):
        T=M.Translation(A(fx,0,-2.05))@M.Rotation(ang,4,"Z");q=str(fx)
        box("Sole"+q,"Rose",(0,FL+.015,0),(.20,.03,.52),.01,T=T)
        tube("Strap"+q,"White",[(-.085,FL+.03,.10),(0,FL+.075,.17),(.085,FL+.03,.10)],.012,5,T=T);tube("ToePost"+q,"White",[(0,FL+.075,.17),(0,FL+.03,.19)],.01,5,T=T)
room("FestivalCarInterior","Dark","Wood");car()
room("FestivalTentInterior","CanvasRose","CanvasCream");tent()
ey,sp=RM["eye"],RM["spawn"];VIEWS={"FestivalCarInterior":(("player",(0,ey,sp),(0,1.25,2.2)),("driver",(-.55,1.75,-.8),(-1.0,1.35,1.5),24),("rear",(0,ey,1.0),(0,.9,-2.8)),("cutaway",(-5.2,5.4,-5.6),(0,.7,.4),22,True)),
"FestivalTentInterior":(("player",(0,ey,sp),(0,1.25,2.4)),("detail",(.9,1.45,.4),(-.2,1.2,2.5),24),("rear",(0,ey,1.0),(0,.6,-2.8)),("cutaway",(-5.2,5.4,-5.6),(0,.5,.4),22,True))}
for k in KITS:
    for n,e,a,*o in VIEWS[k]:shot(k,os.path.join(CONFIG["rev"],k+"_"+n+".png"),e,a,*(o or [RM["lens"]]))
door=[((-RM["door"],.14,-H),(RM["door"],2.4,RM["exit"]))]
finish(KITS,CONFIG["out"],CONFIG["src"],CONFIG["man"],clear={k:door for k in KITS})
