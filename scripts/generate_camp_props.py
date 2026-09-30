CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/CampProps.blend","man":"ArtSource/camp-props-manifest.json","rev":"artifacts/camp-props","car":"Assets/Festival/Art/Resources/FestivalCampCar.fbx","eye":1.62,"mat":.04,"camp":{"FestivalCampDJ":(8,-3.5),"FestivalReviewPodium":(-8,3.6),"FestivalPicnicTable":(5.2,-1.6),"FestivalCampCooler":(6.85,-3.6),"FestivalLanternStake":(7.8,-6)},"suggest":{"FestivalCampDJ":(4.3,5.4),"FestivalReviewPodium":(-4.3,5.4)},"shade":("Assets/Festival/Art/Resources/FestivalCampShade.fbx",4.3),
"roof":{"crown":(1.83,.055,1.035,-.70,1.97),"rail":(1.0,1.94,.035,-.55,1.92),"foot":(.09,.08,.12),"bar":(2.2,.045,.075),"surf":((-.25,1.45),.62,.11,2.4,.62),"box":((-.10,1.30),1.4,.36,1.9,.60),"lug":((0,1.2),1.48,.32,1.5,.60),"paint":{"FestivalRoofSurfboard":"PaintMint","FestivalRoofBox":"PaintGold","FestivalRoofLuggage":"PaintRose"}},
"dj":{"top":(2.5,1.0,.05,.84),"leg":(.98,.40,.022),"deck":(1.3,.07,.56,.02),"plat":(.38,.19,-.04),"spk":(1.72,.52,.80,.44,1.10),"sign":(2.0,.44,1.78,-.22,.97)},
"cooler":(.9,.65,.6,.96,.1,.66),"lantern":(1.30,1.48,.40,.32,.28,.07),"trim":((17,15),(-6,2,7),16,.11,.14,.5),
"pic":{"FestivalPicnicTable":(.04,.72,2.3,5,.15,.05,.80,.72,.13,.44,.80,"PaintMint"),"FestivalPicnicTableLarge":(0,.79,2.9,6,.145,.06,1.05,.82,.15,.47,.92,"PaintRose")},"pod":(.40,-.34,1.22,(.66,1.16,1.60,-.42),(.46,.46,.30))}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
C=CONFIG;G=C["mat"];RF=C["roof"];REV=C["rev"]
ROOF=("FestivalRoofSurfboard","FestivalRoofBox","FestivalRoofLuggage")
KITS=ROOF+("FestivalCampDJ","FestivalCampCooler","FestivalLanternStake","FestivalMatTrim","FestivalPicnicTable","FestivalPicnicTableLarge","FestivalReviewPodium")
setup(KITS)
def matshell(w,d):return [("Mat","Bark",(0,G/2,0),(w,G,d),0),("Ground","Stone",(0,-.05,0),(w+4,.1,d+4),0)]
def leg(n,m,x,y0,z0,y1,z1,wx,wz):return loft(n,m,[[(x-wx/2,y,z-wz/2),(x+wx/2,y,z-wz/2),(x+wx/2,y,z+wz/2),(x-wx/2,y,z+wz/2)] for y,z in ((y0,z0),(y1,z1))])
def se(w,h,y0,z,n=9,e=.35):return [(w*math.copysign(abs(math.cos(a))**e,math.cos(a)),y0+h*abs(math.sin(a))**e,z) for a in (math.pi*i/(n-1) for i in range(n))]
def lerp(S,z,i):
    for a,b in zip(S,S[1:]):
        if a[0]<=z<=b[0]:return a[i]+(z-a[0])/(b[0]-a[0])*(b[i]-a[i])
def bag(n,m,c,L,rx,ry,ax="z",**k):
    x,y,z=c;R=[]
    for t,s in ((-1,.5),(-.85,.84),(-.5,.98),(.5,.98),(.85,.84),(1,.5)):
        p=[(rx*s*math.cos(a),max(ry*s*math.sin(a),-ry*.8)) for a in (6.2832*j/10 for j in range(10))]
        R.append([(x+u,y+v,z+t*L/2) if ax=="z" else (x+t*L/2,y+v,z+u) for u,v in p])
    return loft(n,m,R,smooth=True,**k)
def ring(n,m,c,r,ax,rt,k=8):
    x,y,z=c;return tube(n,m,[(x,y+r*math.cos(a),z+r*math.sin(a)) if ax=="x" else (x+r*math.cos(a),y+r*math.sin(a),z) for a in (6.2832*i/k for i in range(k))],rt,5,True)
def roof(k):
    y0,cr,hw,z0,z1=RF["crown"];rx,ry,rr,r0,r1=RF["rail"];kit(k,[])
    loft("RoofShell","PaintRose",[[(x,y0+cr*(1-(x/hw)**2),z) for x in (hw*(i/4-1) for i in range(9))]+[(hw,y0-.04,z),(-hw,y0-.04,z)] for z in (z0,z1)],tag="shell",cut=True)
    for s in (-1,1):box("Rail"+str(s),"Metal",(s*rx,ry,(r0+r1)/2),(2*rr,2*rr,r1-r0),tag="shell",cut=True)
def rack(zs):
    fw,fh,fd=RF["foot"];bl,bh,bd=RF["bar"];rx,ry,rr=RF["rail"][:3];fb=ry+rr-.005;bb=fb+fh-.003
    for i,z in enumerate(zs):
        box(f"Bar{i}","Metal",(0,bb+bh/2,z),(bl,bh,bd),.012)
        for s in (-1,1):
            box(f"Foot{i}{s}","Dark",(s*rx,fb+fh/2,z),(fw,fh,fd),.012)
            box(f"Clamp{i}{s}","Metal",(s*(rx-rr-.01),ry+.005,z),(.02,2*rr+.02,fd*.7))
            box(f"Cap{i}{s}","Rubber",(s*(bl/2+.012),bb+bh/2,z),(.03,bh+.012,bd+.012),.008)
    return bb+bh
def surf():
    k=ROOF[0];roof(k);zs,W,T,L,zc=RF["surf"];yb=rack(zs)-.004;zn,zt=zc-L/2,zc+L/2
    U=(0,.03,.08,.15,.25,.4,.55,.7,.82,.9,.95,.985,1);F=(.06,.32,.55,.75,.9,.99,1,.96,.86,.74,.62,.5,.44);H=(.3,.42,.6,.8,.95,1,1,.95,.85,.72,.6,.5,.42)
    rk=lambda z:.10*((zs[0]-z)/(zs[0]-zn))**2 if z<zs[0] else .04*((z-zs[1])/(zt-zs[1]))**2 if z>zs[1] else 0
    S=[(zn+u*L,W/2*f,T*h) for u,f,h in zip(U,F,H)];top=lambda z:yb+rk(z)+lerp(S,z,2)
    loft("Board","Mint",[[(w*math.copysign(abs(math.cos(a))**.6,math.cos(a)),yb+rk(z)+t/2+t/2*math.copysign(abs(math.sin(a))**.8,math.sin(a)),z) for a in (6.2832*j/12 for j in range(12))] for z,w,t in S])
    loft("Stringer","Cream",[[(x,yb+rk(z)+t+d,z) for x,d in ((-.02,-.004),(.02,-.004),(.02,.004),(-.02,.004))] for z,w,t in S[1:-1]])
    f0,f1=zt-.40,zt-.17;t0,t1=top(f0),top(f1)
    prism("Fin","Rose",[(f0,t0-.01),(f1,t1-.01),(zt-.05,t1+.17),(f1-.06,t1+.19),(f0+.08,t0+.07)],-.012,.012)
    cyl("LeashPlug","Dark",(0,top(zt-.08)-.01,zt-.08),(0,top(zt-.08)+.02,zt-.08),.022,8)
    tube("Leash","Dark",[(0,top(zt-.08)+.015,zt-.08),(.10,top(zt-.08)+.03,zt-.04),(.22,top(zt-.12),zt-.10),(.30,yb+.06,zt-.22),(.36,yb+.02,zs[1]+.02),(.40,yb-.02,zs[1])],.009,5)
    for i,z in enumerate(zs):
        w=lerp(S,z,1);a=w+.03;yt=top(z);yu=yb-.041;zz=z+.05
        tube(f"Strap{i}","Rose",[(-a,yu,z),(-a,yu,zz),(-a,yt,zz),(-w*.5,yt+.008,zz),(0,yt+.01,zz),(w*.5,yt+.008,zz),(a,yt,zz),(a,yu,zz),(a,yu,z)],.012,4)
        box(f"Buckle{i}","Gold",(a+.01,(yu+yt)/2+.03,zz),(.03,.07,.05),.008)
def cargo():
    k=ROOF[1];roof(k);zs,W,Hh,L,zc=RF["box"];yb=rack(zs)-.004;z0=zc-L/2;ys=yb+.13
    S=[(z0+u*L,W/2*f,Hh*g) for u,f,g in zip((0,.04,.12,.25,.5,.75,.9,.97,1),(.55,.78,.93,.99,1,1,.97,.9,.78),(.40,.62,.82,.95,1,1,.95,.85,.70))]
    loft("Tray","Dark",[[(-w+.03,yb,z),(w-.03,yb,z),(w,yb+.03,z),(w,ys,z),(-w,ys,z),(-w,yb+.03,z)] for z,w,h in S])
    loft("Lid","Gold",[se(w,h-.13,ys,z) for z,w,h in S])
    tube("Seam","Dark",[(w+.006,ys,z) for z,w,h in S]+[(-w-.006,ys,z) for z,w,h in S[::-1]],.014,4,True)
    for x0 in (-.16,.16):loft(f"Stripe{x0}","Cream",[[(x0+u,ys+h-.13+d,z) for u,d in ((-.05,-.006),(.05,-.006),(.05,.004),(-.05,.004))] for z,w,h in S[1:-1]])
    for z in (zc-.45,zc+.40):
        for s in (-1,1):box(f"Latch{z}{s}","Metal",(s*(lerp(S,z,1)+.01),ys,z),(.025,.08,.10),.006)
    ze=zc+L/2;cyl("Lock","Metal",(0,ys+.05,ze-.01),(0,ys+.05,ze+.02),.03,8);box("Keyway","Dark",(0,ys+.05,ze+.022),(.008,.03,.006))
    for s in (-1,1):box(f"Grip{s}","Dark",(s*.30,yb+.06,S[0][0]+.005),(.16,.04,.03))
def rrect(hx,z0,z1,r,y):
    P=[]
    for cx,cz,a0 in ((hx-r,z1-r,0),(-hx+r,z1-r,90),(-hx+r,z0+r,180),(hx-r,z0+r,270)):P+=[(cx+r*math.cos(math.radians(a0+30*i)),y,cz+r*math.sin(math.radians(a0+30*i))) for i in range(4)]
    return P
def luggage():
    k=ROOF[2];roof(k);zs,W,Hh,L,zc=RF["lug"];yt=rack(zs);hx=W/2-.02;zf,zb=zc-L/2,zc+L/2;ft=yt+.03;ry=yt+.17
    for s in (-1,1):box(f"Side{s}","Metal",(s*hx,yt+.012,zc),(.04,.03,L))
    for i,z in enumerate((zf+.03,zf+.17,.30,zc,.88,1.14,zb-.03)):box(f"Slat{i}","Metal",(0,yt+.02,z),(2*hx,.02,.05))
    tube("Rail","Metal",rrect(hx,zf-.04,zb-.02,.08,ry),.016,6,True)
    for s in (-1,1):
        for z in (zc-.35,zc+.35,zb-.10):tube(f"Post{s}{z}","Metal",[(s*hx,yt+.01,z),(s*hx,ry,z)],.014,6)
    slab("Deflector","Dark",(0,yt+.015,zf+.02),(0,ry+.012,zf-.04),2*hx-.12,.02)
    ry0=ft+.105;cyl("Roll","Mint",(-.58,ry0,zf+.17),(.58,ry0,zf+.17),.11,10)
    for s in (-1,1):cyl(f"RollEnd{s}","CanvasCream",(s*.575,ry0,zf+.17),(s*.59,ry0,zf+.17),.092,10);ring(f"RollTie{s}","Dark",(s*.36,ry0,zf+.17),.113,"x",.012)
    ct=ft+.235;box("Case","Rose",(-.33,ft+.115,.48),(.74,.24,.56),.035)
    for z in (.40,.56):box(f"CaseRib{z}","Dark",(-.33,ct,z),(.68,.014,.03))
    for z in (.36,.60):box(f"CaseLatch{z}","Gold",(.045,ft+.14,z),(.02,.05,.07))
    cyl("Sticker","Cream",(-.55,ct-.002,.66),(-.55,ct+.004,.66),.06,10)
    ay=ft+.8*.15-.005;bag("DuffelA","CanvasGold",(.40,ay,.57),.80,.16,.15)
    for z in (.47,.67):tube(f"DuffelAStrap{z}","Dark",[(.40-.15,ay,z),(.40-.09,ay+.17,z),(.40+.09,ay+.17,z),(.40+.15,ay,z)],.014,5)
    tube("DuffelAZip","Gold",[(.40,ay+.148,z) for z in (.30,.57,.84)],.01,5)
    by=ft+.8*.13-.005;bag("DuffelB","CanvasMint",(0,by,1.14),1.26,.15,.13,"x")
    tube("DuffelBZip","Gold",[(x,by+.128,1.14) for x in (-.40,0,.40)],.01,5)
    box("SnackBag","Blue",(-.36,ft+.105,.875),(.52,.22,.17),.03);box("SnackFlap","Gold",(-.36,ft+.217,.875),(.40,.01,.10))
    tube("Bungee1","Gold",[(-hx,ry,.45),(-.66,ct+.006,.45),(.02,ct+.006,.45),(.24,ay+.075,.45),(.40,ay+.157,.45),(.56,ay+.075,.45),(hx,ry,.45)],.011,5)
    tube("Bungee2","Rose",[(.40,ry,zf-.04),(.40,ry0+.117,zf+.17),(.40,ay+.10,.24),(.40,ay+.157,.57),(.40,ay+.10,.92),(.40,by+.13,1.14),(.40,ry,zb-.02)],.011,5)
def dj():
    k="FestivalCampDJ";kit(k,matshell(6,4));d=C["dj"];tw,td,tt,ty=d["top"];lx,lz,lr=d["leg"]
    box("Top","Cream",(0,ty-tt/2,0),(tw,tt,td),.015)
    for s in (-1,1):box(f"Apron{s}","Metal",(0,ty-tt-.023,s*(td/2-.05)),(tw-.2,.05,.03))
    box("Spine","Metal",(0,ty-tt-.018,0),(tw-.2,.04,.04))
    for s in (-1,1):
        x=s*lx
        for z in (-lz,lz):cyl(f"Leg{s}{z}","Metal",(x,G,z),(x,ty-tt+.002,z),lr,8);cyl(f"Shoe{s}{z}","Rubber",(x,G-.002,z),(x,G+.04,z),lr+.01,8)
        cyl(f"Cross{s}","Metal",(x,.22,-lz),(x,.22,lz),lr*.8,6);cyl(f"Brace{s}","Metal",(x,.22,0),(x*.55,ty-tt-.02,0),lr*.8,6)
    xs=[-tw/2+i*tw/20 for i in range(21)]
    loft("Skirt","CanvasRose",[[(x,y,-td/2-.006-(.03 if i%2 else 0)) for i,x in enumerate(xs)]+[(x,y,-td/2+.002) for x in xs[::-1]] for y in (ty-tt+.005,G+.03)])
    box("SkirtTrim","Gold",(0,ty-.025,-td/2-.025),(tw+.02,.05,.05))
    cw,ch_,cd,cz=d["deck"];dt=ty+ch_-.003;box("Deck","Dark",(0,ty+ch_/2-.003,cz),(cw,ch_,cd),.02)
    px,pr,pz=d["plat"]
    for s in (-1,1):
        q="L" if s<0 else "R";x=s*px
        cyl("Well"+q,"Rubber",(x,dt-.004,pz),(x,dt+.012,pz),pr+.02,16)
        p=cyl(f"DJPlatter{q}__Metal","Metal",(x,dt+.01,pz),(x,dt+.04,pz),pr,16,piv=(x,dt+.025,pz),tag="keep")
        parent(box(f"DJMarker{q}__Gold","Gold",(x,dt+.043,pz),(.30,.01,.05),piv=(x,dt+.025,pz),tag="keep"),p)
        for i in range(4):box(f"Pad{q}{i}","Rose" if i%2 else "Mint",(x-.12+i*.08,dt+.005,cz+.235),(.065,.014,.045),.004)
    for i,x in enumerate((-.08,0,.08)):
        box(f"Slot{i}","Rubber",(x,dt+.001,cz+.10),(.014,.006,.14));box(f"Fader{i}","Gold",(x,dt+.012,cz+.06+i*.03),(.045,.02,.026),.004)
        for j,z in enumerate((-.19,-.12,-.05)):cyl(f"Knob{i}{j}",("Mint","Rose","White")[j],(x,dt-.002,cz+z),(x,dt+.022,cz+z),.019,6)
    box("XSlot","Rubber",(0,dt+.001,cz+.235),(.16,.006,.014));box("XFader","Gold",(.03,dt+.012,cz+.235),(.026,.02,.045),.004)
    for s in (-1,1):box(f"Meter{s}","StageGlowMint",(s*.14,dt+.002,cz-.10),(.014,.008,.12))
    prism("Stand","Metal",[(-.28,ty-.003),(-.48,ty-.003),(-.48,ty+.12),(-.28,ty+.04)],-.15,.15)
    slab("LapBase","Metal",(0,ty+.049,-.285),(0,ty+.129,-.475),.36,.02)
    slab("LapLid","Dark",(0,ty+.125,-.472),(0,ty+.37,-.50),.36,.018)
    slab("LapScreen","Glass",(0,ty+.15,-.461),(0,ty+.355,-.484),.32,.008)
    cyl("LapSticker","Rose",(0,ty+.25,-.49),(0,ty+.251,-.499),.06,10)
    bw,bh,byc,bz,bpx=d["sign"];b0,b1=byc-bh/2,byc+bh/2
    box("Board","Dark",(0,byc,bz+.025),(bw,bh,.05))
    for n,c,s in (("FrameTop",(0,b1,bz+.02),(bw+.04,.04,.07)),("FrameBot",(0,b0,bz+.02),(bw+.04,.04,.07)),("FrameL",(-bw/2,byc,bz+.02),(.04,bh,.07)),("FrameR",(bw/2,byc,bz+.02),(.04,bh,.07))):box(n,"Gold",c,s)
    for s in (-1,1):
        box(f"Post{s}","Metal",(s*bpx,(ty+b1)/2,bz+.08),(.06,b1-ty,.06));box(f"Plate{s}","Dark",(s*bpx,ty+.009,bz+.08),(.18,.02,.18))
        ico(f"Finial{s}","Gold",(s*bpx,b1+.04,bz+.08),.045)
    for i in range(9):ico(f"Bulb{i}","StageGlowGold" if i%2 else "StageGlowRose",(-.80+i*.20,b1+.045,bz+.02),.032)
    sx,sw,sh,sd,sy=d["spk"]
    for s in (-1,1):
        x0=s*sx;q="L" if s<0 else "R";wy=sy+.30
        cyl("Column"+q,"Metal",(x0,.30,0),(x0,sy+.005,0),.025,8);cyl("Collar"+q,"Dark",(x0,.68,0),(x0,.76,0),.045,8)
        cyl("Socket"+q,"Dark",(x0,sy-.06,0),(x0,sy+.002,0),.045,8)
        for i,a in enumerate((0,2.094,4.189)):
            dx,dz=math.cos(a)*s,math.sin(a);f=(x0+.46*dx,G+.015,.46*dz);c0=(x0+.035*dx,.72,.035*dz)
            cyl(f"TLeg{q}{i}","Metal",c0,f,.017,6);cyl(f"TFoot{q}{i}","Rubber",(f[0],G-.002,f[2]),(f[0],G+.03,f[2]),.03,6)
            cyl(f"TBrace{q}{i}","Metal",(x0+.015*dx,.33,.015*dz),ad(c0,(f[0]-c0[0],f[1]-c0[1],f[2]-c0[2]),.5),.011,5)
        box("Cab"+q,"Dark",(x0,sy+sh/2,0),(sw,sh,sd),.03);box("Grille"+q,"Rubber",(x0,sy+sh/2,-sd/2-.003),(sw-.06,sh-.06,.01))
        cyl("Surround"+q,"Metal",(x0,wy,-sd/2-.006),(x0,wy,-sd/2-.022),.19,14)
        cyl(f"DJCone{q}__Mint","Mint",(x0,wy,-sd/2-.02),(x0,wy,-sd/2-.085),.165,14,r2=.05,piv=(x0,wy,-sd/2-.02),tag="keep")
        cyl("Tweeter"+q,"Gold",(x0,sy+.66,-sd/2-.006),(x0,sy+.66,-sd/2-.03),.06,10);box("Port"+q,"Dark",(x0,sy+.09,-sd/2-.009),(.26,.04,.01))
        tube("Handle"+q,"Metal",[(x0-.12,sy+sh-.004,0),(x0-.10,sy+sh+.05,0),(x0+.10,sy+sh+.05,0),(x0+.12,sy+sh-.004,0)],.015,6)
        tube("SpkCable"+q,"Dark",[(s*.55,dt-.035,cz-cd/2+.005),(s*.62,ty+.011,-.36),(s*1.20,ty+.011,-.36),(s*1.265,ty-.03,-.36),(s*1.33,.40,-.30),(s*1.42,G+.011,-.18),(s*1.62,G+.011,-.02),(s*(sx-.036),.12,0),(s*(sx-.036),sy+.005,0)],.012,5)
        box("Plug"+q,"Dark",(s*.55,dt-.035,cz-cd/2-.012),(.04,.03,.03))
    box("Strip","Dark",(.45,G+.02,.30),(.30,.04,.07));box("StripLed","StageGlowRose",(.34,G+.041,.30),(.03,.006,.03))
    tube("Power","Dark",[(.52,G+.02,.30),(.60,G+.011,.42),(.62,.30,.515),(.62,ty-.02,.515),(.62,ty+.011,.46),(.60,ty+.011,cz+cd/2+.01),(.58,dt-.03,cz+cd/2-.005)],.011,5)
    cx,cz0=-.55,.15;box("CrateBase","Wood",(cx,G+.01,cz0),(.44,.02,.36))
    for n,c,s in (("CrateF",(cx,G+.16,cz0-.17),(.44,.30,.02)),("CrateB",(cx,G+.16,cz0+.17),(.44,.30,.02)),("CrateL",(cx-.21,G+.16,cz0),(.02,.30,.32)),("CrateR",(cx+.21,G+.16,cz0),(.02,.30,.32))):box(n,"Wood",c,s)
    for i in range(6):box(f"Record{i}",("Rose","Mint","Gold","Cream","Blue","Rose")[i],(cx-.15+i*.06,G+.165,cz0),(.012,.29,.30),R=rot(-6+i*2.5,"z"))
def cooler():
    k="FestivalCampCooler";kit(k,matshell(2.5,2.5));bw,bh,bd,lw,lh,ld=C["cooler"];top=G+bh
    box("Body","Rose",(0,G+bh/2,0),(bw,bh,bd),.04)
    box("Skid","White",(0,G+.035,0),(bw+.02,.07,bd+.02),.015);box("Rim","White",(0,top-.022,0),(bw+.012,.044,bd+.012),.01)
    for s in (-1,1):
        for z in (-.17,.17):box(f"Mount{s}{z}","Dark",(s*(bw/2+.012),top-.14,z),(.03,.06,.05),.006)
        tube(f"Handle{s}","White",[(s*(bw/2+.02),top-.15,-.17),(s*(bw/2+.07),top-.19,-.13),(s*(bw/2+.07),top-.19,.13),(s*(bw/2+.02),top-.15,.17)],.018,6)
    cyl("Plug","White",(bw/2-.14,G+.13,-bd/2+.005),(bw/2-.14,G+.13,-bd/2-.03),.035,8);box("PlugTab","Dark",(bw/2-.14,G+.13,-bd/2-.034),(.05,.014,.012))
    box("Keeper","Dark",(0,top-.075,-bd/2-.008),(.10,.04,.02))
    for x in (-.25,.25):cyl(f"Hinge{x}","Dark",(x-.07,top,bd/2+.01),(x+.07,top,bd/2+.01),.02,6)
    label("Brand","White","CHILL",(0,G+.34,-bd/2-.004),.13,.008)
    pv=(0,top,bd/2+.01);lid=box("CampCoolerLid__Cream","Cream",(0,top+lh/2-.002,0),(lw,lh,ld),.03,piv=pv,tag="keep")
    parent(box("CampCoolerLatch__Gold","Gold",(0,top-.02,-ld/2-.006),(.14,.13,.025),.006,piv=pv,tag="keep"),lid)
    bm=bmesh.new()
    for x in (-.27,.27):bmesh.ops.create_cone(bm,cap_ends=True,segments=12,radius1=.07,radius2=.07,depth=.012,matrix=M.Translation(A(x,top+lh-.001,-.14)))
    parent(ob("CampCoolerCups__Dark","Dark",bm,piv=pv,tag="keep"),lid)
def lantern():
    k="FestivalLanternStake";kit(k,matshell(2,2));sy,ly,bw,gw,gh,st=C["lantern"];b=ly-gh/2
    box("Stake","Wood",(0,(sy-.12)/2,0),(st,sy+.12,st),.012)
    for y in (.45,.80):box(f"Band{y}","Gold",(0,y,0),(st+.016,.04,st+.016),.006)
    box("Plate","Dark",(0,sy,0),(.24,.03,.24))
    for s in (-1,1):slab(f"GussetX{s}","Dark",(s*.03,sy-.16,0),(s*.11,sy-.01,0),.03,.02);slab(f"GussetZ{s}","Dark",(0,sy-.16,s*.03),(0,sy-.01,s*.11),.02,.03)
    box("Base","Dark",(0,b-.015,0),(bw,.04,bw),.008);box("Glass","StageGlowGold",(0,ly,0),(gw,gh,gw),.01)
    for sx in (-1,1):
        for sz in (-1,1):box(f"Post{sx}{sz}","Dark",(sx*gw/2,ly,sz*gw/2),(.04,gh+.01,.04))
        box(f"MulZ{sx}","Dark",(0,ly,sx*(gw/2+.002)),(.025,gh,.012));box(f"MulX{sx}","Dark",(sx*(gw/2+.002),ly,0),(.012,gh,.025))
        box(f"BarZ{sx}","Dark",(0,ly,sx*(gw/2+.003)),(gw,.02,.012));box(f"BarX{sx}","Dark",(sx*(gw/2+.003),ly,0),(.012,.02,gw))
    t0=ly+gh/2;box("Cap","Dark",(0,t0+.012,0),(bw,.03,bw),.008);r0=t0+.025
    loft("Roof","Dark",[[(sx*h,y,sz*h) for sx,sz in ((-1,-1),(1,-1),(1,1),(-1,1))] for h,y in ((.24,r0),(.03,r0+.12))])
    ring("HangRing","Gold",(0,r0+.12+.045,0),.05,"z",.012)
    kz=-st/2-.012;box("Knot","Rose",(0,sy-.10,kz),(.06,.05,.03),.008)
    for s in (-1,1):tube(f"Loop{s}","Rose",[(0,sy-.10,kz),(s*.07,sy-.05,kz-.005),(s*.11,sy-.09,kz-.005),(s*.07,sy-.13,kz-.005),(0,sy-.10,kz)],.012,5);slab(f"Tail{s}","Rose",(s*.01,sy-.11,kz),(s*.07,sy-.30,kz-.01),.035,.008)
def trim():
    k="FestivalMatTrim";(W,D),(z0,dz,n),sl,sw,bd,tk=C["trim"];hw,hd=W/2,D/2
    kit(k,[("Mat","Bark",(0,G/2,0),(W,G,D),0),("Ground","Stone",(0,-.05,0),(W+3,.1,D+3),0)])
    for i in range(n):
        z=z0+i*dz;box(f"Stripe{i}","Mint" if i%2==0 else "Rose",(0,G+.003,z),(sl,.008,sw))
        for j in range(int(sl/tk)):box(f"Tick{i}_{j}","Cream",(-sl/2+tk*(j+.5),G+.0095,z),(.035,.006,sw+.03),R=rot(35 if j%2 else -35,"y"))
    for s in (-1,1):
        box(f"BindN{s}","CanvasGold",(0,G+.005,s*(hd-bd/2)),(W+.02,.014,bd));box(f"BindE{s}","CanvasGold",(s*(hw-bd/2),G+.005,0),(bd,.014,D-2*bd))
        box(f"SkirtN{s}","CanvasGold",(0,(G+.012)/2,s*(hd+.006)),(W+.024,G+.012,.012));box(f"SkirtE{s}","CanvasGold",(s*(hw+.006),(G+.012)/2,0),(.012,G+.012,D))
    for sx in (-1,1):
        for sz in (-1,1):
            q=f"{sx}{sz}";box("Patch"+q,"Rose",(sx*(hw-.16),G+.013,sz*(hd-.16)),(.30,.006,.30));kx,kz=sx*(hw+.03),sz*(hd+.03)
            ico("Knot"+q,"Rose",(kx,.035,kz),.035)
            for j in range(5):a=math.atan2(sz,sx)+(j-2)*.22;slab(f"Strand{q}{j}","Gold",(kx,.014,kz),(kx+.30*math.cos(a),.006,kz+.30*math.sin(a)),.026,.012)
def picnic(k):
    g,top,L,n,pw,pt,lx,bz,bw,bt,fz,fm=C["pic"][k];kit(k,matshell(4,3) if g else None);gap=.012;W=n*pw+(n-1)*gap;BW=2*bw+gap
    for i in range(n):box(f"Plank{i}","Wood",(0,top-pt/2,-W/2+pw/2+i*(pw+gap)),(L,pt,pw),.012)
    for s in (-1,1):
        for j in (-1,1):box(f"Seat{s}{j}","Wood",(0,bt-pt/2,s*bz+j*(bw+gap)/2),(L,pt,bw),.012)
    ct,cs=top-pt-.033,bt-pt-.033
    box("Batten",fm,(0,ct,0),(.08,.07,W-.06))
    for s in (-1,1):
        x=s*(lx-.068);box(f"TopCleat{s}",fm,(x,ct,0),(.08,.07,W-.06));box(f"SeatCleat{s}",fm,(x,cs,0),(.08,.07,2*(bz+BW/2+.03)),.008)
        slab(f"Brace{s}",fm,(x,cs+.005,0),(s*.02,ct-.005,0),.09,.05)
        for j in (-1,1):
            z0,z1=j*fz,j*(W/2-.14);y0,y1=g-.003,top-pt+.002;leg(f"Leg{s}{j}",fm,s*lx,y0,z0,y1,z1,.07,.13)
            for y in (cs,ct):zc=z0+(y-y0)/(y1-y0)*(z1-z0);cyl(f"Bolt{s}{j}{y}","Metal",(s*(lx+.03),y,zc),(s*(lx+.05),y,zc),.022,8)
    return [((-lx+.15,g+.08,s*(W/2+.02)),(lx-.15,cs-.05,s*(bz-BW/2-.02))) for s in (-1,1)]
def podium():
    k="FestivalReviewPodium";kit(k,matshell(3,2.5));bx,fz,fy,(hx,h0,h1,hz),(sw,s2,s3)=C["pod"];p0=G+.08;bk=.30;by=fy-.12
    box("Plinth","Dark",(0,G+.04,-.02),(1.78,.08,.80),.02)
    prism("Body","Rose",[(fz,p0-.003),(bk,p0-.003),(bk,by),(fz,fy)],-bx,bx)
    slab("Desk","Wood",(0,fy+.017,fz+.005),(0,by+.017,bk+.04),2*bx+.12,.04)
    box("Header","Dark",(0,(h0+h1)/2,hz+.04),(2*hx,h1-h0,.08))
    for n,c,s in (("FrameTop",(0,h1,hz+.035),(2*hx+.04,.04,.09)),("FrameBot",(0,h0,hz+.035),(2*hx+.04,.04,.09)),("FrameL",(-hx,(h0+h1)/2,hz+.035),(.04,h1-h0,.09)),("FrameR",(hx,(h0+h1)/2,hz+.035),(.04,h1-h0,.09))):box(n,"Gold",c,s)
    for i in range(7):ico(f"Bulb{i}","StageGlowGold" if i%2 else "StageGlowRose",(-.54+i*.18,h1+.042,hz+.035),.028)
    sc=h1+.02+.17*math.sin(math.radians(54))-.006
    loft("Star","Gold",[[(r*math.cos(math.radians(90+36*i)),sc+r*math.sin(math.radians(90+36*i)),z) for i,r in enumerate([.17,.075]*5)] for z in (hz+.01,hz+.06)])
    ry=.74;cyl("Rosette","Gold",(0,ry,fz+.002),(0,ry,fz-.022),.15,12);cyl("RosetteIn","Rose",(0,ry,fz-.02),(0,ry,fz-.034),.10,12)
    label("One","Dark","1",(0,ry,fz-.037),.15,.008)
    for s in (-1,1):slab(f"Tail{s}","Rose",(s*.05,ry-.10,fz-.012),(s*.11,ry-.36,fz-.012),.075,.012)
    for s,h,m,t in ((-1,s2,"Mint","2"),(1,s3,"Blue","3")):
        x=s*(bx+sw/2-.003);box("Step"+t,m,(x,p0-.003+h/2,-.02),(sw,h,.70),.02);label("Num"+t,"White",t,(x,p0+h/2,-.37-.005),.24,.01)
    y2,x2=p0+s2,-(bx+sw/2);box("TrophyBase","Dark",(x2,y2+.027,-.02),(.16,.06,.16),.01);cyl("TrophyStem","Gold",(x2,y2+.05,-.02),(x2,y2+.16,-.02),.025,8)
    lathe("TrophyCup","Gold",[(.04,.155),(.10,.20),(.125,.28),(.135,.36),(.125,.37)],(x2,y2,-.02),12)
    for s in (-1,1):tube(f"TrophyHandle{s}","Gold",[(x2+s*.12,y2+.33,-.02),(x2+s*.20,y2+.32,-.02),(x2+s*.19,y2+.23,-.02),(x2+s*.10,y2+.22,-.02)],.015,6)
    y3,x3=p0+s3,bx+sw/2;cyl("BellBase","Wood",(x3,y3-.003,-.08),(x3,y3+.03,-.08),.08,10)
    lathe("Bell","Gold",[(.07,.028),(.068,.06),(.05,.09),(.02,.105),(.006,.108)],(x3,y3,-.08),10);cyl("BellButton","Dark",(x3,y3+.10,-.08),(x3,y3+.135,-.08),.012,6)
    for i in range(3):box(f"Card{i}","Cream",(x3+.02,y3+.006+i*.012,.17),(.18,.012,.13),R=rot(-12+i*9,"y"))
    box("MicClamp","Dark",(.34,1.42,-.32),(.06,.08,.04));tube("Gooseneck","Metal",[(.34,1.44,-.31),(.34,1.56,-.28),(.32,1.66,-.20),(.30,1.69,-.12)],.012,6)
    ico("MicHead","Dark",(.30,1.69,-.085),.045,2)
    return [((-.60,1.21,-.62),(.60,1.56,-.4205))]
def carshots():
    bpy.ops.import_scene.fbx(filepath=os.path.join(R0,C["car"]));car=list(bpy.context.selected_objects)
    for o in car:o["temp"]=1;o["tag"]="shell"
    for k in ROOF:
        for o in car:
            o["kit"]=k
            if o.type=="MESH":
                key=o.name.split("__")[-1].split(".")[0];m=MAT[RF["paint"][k]] if key=="PaintRose" else MAT.get(key,MAT["Dark"])
                for i in range(len(o.data.materials)):o.data.materials[i]=m
        for v,e,a,l in (("car",(-3.9,3.2,-3.4),(0,1.75,.4),35),("player",(-3.4,1.62,-2.2),(0,1.9,.5),32),("rack",(-2.0,2.45,-1.2),(-.8,2.0,-.1),30)):shot(k,f"{REV}/{k}_{v}.png",e,a,l,cut=True)
    for o in car:bpy.data.objects.remove(o,do_unlink=True)
def campview(path,eye,at,lens,pos):
    bpy.context.view_layer.update();mv=[]
    for k,(x,z) in pos.items():
        off=RZ@A(x,0,z)
        for o in COL[k].objects:
            if o.parent is None:o.location+=off;mv.append((o,off))
    ks=set(C["camp"])|{"FestivalMatTrim"};render(path,camera(eye,at,lens),lambda o:o.get("kit") in ks and (o.get("tag")!="shell" or o.get("kit")=="FestivalMatTrim"))
    for o,off in mv:o.location-=off
surf();cargo();luggage();dj();cooler();lantern();trim()
clr={k:picnic(k) for k in C["pic"]};clr["FestivalReviewPodium"]=podium()
clr.update({k:[((-1.3,.2,-2.6),(1.3,1.82,2.6))] for k in ROOF});clr["FestivalCampDJ"]=[((-.94,1.60,-.45),(.94,1.96,-.2205))]
carshots()
for k,v,e,a,l in (("FestivalCampDJ","front",(0,1.62,-3.6),(0,1.25,0),30),("FestivalCampDJ","three",(-3.0,2.5,-3.1),(0,1.05,0),30),("FestivalCampDJ","booth",(.4,1.62,1.35),(0,.95,-.3),30),
("FestivalCampCooler","three",(-1.5,1.4,-1.8),(0,.38,0),30),("FestivalLanternStake","three",(-1.4,1.62,-2.0),(0,1.15,0),32),("FestivalReviewPodium","front",(0,1.62,-3.0),(0,1.0,0),30),("FestivalReviewPodium","three",(-2.3,2.0,-2.5),(0,.85,0),30),
("FestivalPicnicTable","three",(-2.8,2.2,-2.8),(0,.42,0),30),("FestivalPicnicTableLarge","three",(-3.2,2.3,-3.0),(0,.45,0),30),("FestivalPicnicTableLarge","side",(0,1.62,-3.2),(0,.45,0),30),
("FestivalMatTrim","overview",(-12,10,-13),(0,0,0),26),("FestivalMatTrim","corner",(-6.6,1.62,-9.4),(-8.3,0,-7.2),30)):shot(k,f"{REV}/{k}_{v}.png",e,a,l)
gallery([k for k in KITS if k!="FestivalMatTrim"],f"{REV}/gallery.png",cols=5)
for o in [o for o in bpy.data.objects if o.get("temp")]:bpy.data.objects.remove(o,do_unlink=True)
sf,sx=C["shade"];sh=[]
for x in (-sx,sx):
    bpy.ops.import_scene.fbx(filepath=os.path.join(R0,sf))
    for o in bpy.context.selected_objects:
        o["temp"]=1;o["kit"]="FestivalMatTrim";o["tag"]="shell";sh.append(o)
        if o.parent is None:o.location+=RZ@A(x,0,0)
        if o.type=="MESH":
            key=o.name.split("__")[-1].split(".")[0]
            for i in range(len(o.data.materials)):o.data.materials[i]=MAT.get(key,MAT["Cream"])
for n,pos,e1,a1,e2,a2 in (("current",C["camp"],(1.5,C["eye"],-9.2),(6.5,.9,-3.5),(-4,11,-17),(0,0,0)),("suggested",{**C["camp"],**C["suggest"]},(0,C["eye"],-1.5),(0,1.1,6),(8,10,17),(0,0,1))):
    campview(f"{REV}/camp_view_{n}.png",e1,a1,28,pos);campview(f"{REV}/camp_overview_{n}.png",e2,a2,24,pos)
for o in sh:bpy.data.objects.remove(o,do_unlink=True)
AN={"FestivalLanternStake":{"light":[0,C["lantern"][1],0]},"FestivalCampDJ":{"signFrontZ":-.22,"tableTop":.84},"FestivalReviewPodium":{"signFrontZ":-.42},"FestivalCampCooler":{"lidTop":G+C["cooler"][1]+C["cooler"][4]-.002}}
finish(KITS,C["out"],C["src"],C["man"],clear=clr,extra={k:{"anchors":v} for k,v in AN.items()})
