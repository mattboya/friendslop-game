CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/FestivalDressing.blend","man":"ArtSource/festival-dressing-manifest.json","rev":"artifacts/festival-dressing","res":"Assets/Festival/Art/Resources/","stock":((-.245,.01,-.245),(.245,.583,.245)),
"mk":{"w":3.35,"s":(1.18,2.07),"hy":(1.06,1.95),"hz":(-1.10,0.0),"hx":(-2.25,-.75,.75,2.25),"tag":((-.47,-.65),(-.39,-.63)),"body":(-1.65,-.89,-1.71,1.105),"stall":(2.43,1.44,-.87,.65),"cab":(2.47,1.43,.42),"up":(.30,.38,(-3.29,-1.5,0,1.5,3.29),2.62,2.74),"shelf":(-.32,.30,2.01,1.95),"ledge":(.46,-1.84,1.02),"slats":15,"bulbs":3,"vendor":(0,-1.95),"sign":((-.77,2.2175,.2155),(.77,2.5825,.2975)),"row0":(("FestivalLittleSpoon",4),("FestivalStockPrism",4),("FestivalStockMoon",4),("FestivalVoucher",4)),"row1":(("FestivalPoi",4),("FestivalStagePass",1),("FestivalConfetti",4),("FestivalStash",1))},
"tag":{"board":(.96,.26,.05),"edge":(.98,.032,.064,.13),"foot":(.38,.08,.03,.12),"strut":((.03,.03),(-.128,.09),.07,.015)},"bunt":{"h":7.92,"r":(.06,.045),"foot":(.44,.16),"sleeve":(.085,.55),"arm":(7.22,2.32,.025,6.55),"flag":(.75,2.25,7.21,6.55,6.40,.02),"wave":(.14,1.25),"stripe":(6.70,6.78),"cap":.09},
"lamp":{"plate":(.46,.04),"foot":((.16,.04),(.16,.12),(.12,.22),(.085,.45),(.085,.55)),"shaft":(.07,.055,6.64),"lant":(6.64,6.70,7.22,7.42,.32,.23,.28,.35),"bracket":(6.2,.26)},"dj":{"plinth":(1.90,.22,-.70,.632,.50,.17),"riser":(1.125,.08,.50,1.105),"ramp":(2.05,4.6,.30,.26,.05),"deck":(1.85,.625,.95),"who":(0,.08,.69),"stage":(0,-1.40,1.85)},
"tin":{"prism":((.195,.01),(.214,.016),(.225,.03),(.225,.045),(.220,.055),(.220,.44)),"label":(.2262,.10,.40),"rim":((.225,.435),(.2255,.45),(.2255,.49),(.215,.50)),"apex":.583,"emb":(.224,.2295,.2315,.25),"moon":((.20,.01),(.232,.016),(.241,.03),(.232,.045),(.20,.05)),"body":(.229,.045,.41),"band":((.236,.405),(.240,.415),(.240,.44),(.236,.45)),"dome":((.236,.445),(.225,.48),(.195,.515),(.15,.545),(.09,.568),(.004,.583)),"craters":((250,1.5,.045),(300,2.6,.035),(205,3.3,.03),(40,1.8,.045),(110,2.7,.04),(340,1.0,.03),(160,1.2,.035),(275,3.9,.025)),"cres":(.075,.064,.04,.23,-.012)}}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
C=CONFIG
KITS=("FestivalMarketCounter","FestivalPriceTag","FestivalBuntingPole","FestivalLightPole","FestivalDJRiser","FestivalStockPrism","FestivalStockMoon")
for o in [o for o in bpy.data.objects if o.get("ctx")]:bpy.data.objects.remove(o,do_unlink=True)
setup(KITS)
Q=math.pi/8
def olathe(n,m,prof,**kw):return loft(n,m,[[(d/math.cos(Q)*math.cos(Q+j*2*Q),y,d/math.cos(Q)*math.sin(Q+j*2*Q)) for j in range(8)] for d,y in prof],**kw)
def emb(n,m,pts,r0,r1,dy=0,flat=True):
    for s in (-1,1):loft(n+str(s),m,[[(-s*x,y+dy,s*(r if flat else math.sqrt(r*r-x*x))) for x,y in pts] for r in (r0,r1)])
def crescent(ro,ri,d,n=8,a=.35,ox=0):
    xi=(ro*ro-ri*ri+d*d)/(2*d);yi=math.sqrt(ro*ro-xi*xi);t0=math.atan2(yi,xi);p0=math.atan2(yi,xi-d)
    P=[(ro*math.cos(t),ro*math.sin(t)) for t in (t0+(2*math.pi-2*t0)*i/(n-1) for i in range(n))]+[(d+ri*math.cos(t),ri*math.sin(t)) for t in (2*math.pi-p0-(2*math.pi-2*p0)*i/(n-1) for i in range(1,n-1))]
    return [(ox+x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a)) for x,y in P]
def star(x,y,r):return [(x+r*(1 if i%2==0 else .3)*math.cos(i*math.pi/4+math.pi/2),y+r*(1 if i%2==0 else .3)*math.sin(i*math.pi/4+math.pi/2)) for i in range(8)]
def counter():
    k=C["mk"];W=k["w"];s0,s1=k["s"];fz,bz,oz,tb=k["body"];sw,st,sfz,sbz=k["stall"];cx,ct,cbz=k["cab"];uz0,uz1,ux,hb,ht=k["up"];sz0,sz1,sb,fb=k["shelf"];lt,lz,lw=k["ledge"]
    kit("FestivalMarketCounter",[("Ground","Stone",(0,-.05,-.6),(16,.1,8),0),("StallCounter","Wood",(0,st-.08,(sfz+sbz)/2),(2*sw,.16,sbz-sfz),0)])
    box("Plinth","Dark",(0,.06,(fz+.05+bz)/2),(2*W-.04,.12,bz-fz-.05))
    box("Carcass","Dark",(0,(.12+tb)/2,(fz+.03+bz)/2),(2*W-.02,tb-.12,bz-fz-.03))
    n=k["slats"];p=(2*W-.02)/n
    for i in range(n):box("Slat"+str(i),"PaintRose" if i%2==0 else "PaintCream",(-W+.01+p*(i+.5),(.19+1.035)/2,fz+.015),(p-.03,1.035-.19,.03))
    for y0,y1 in ((.12,.19),(1.035,tb)):box("Rail"+str(y0),"Wood",(0,(y0+y1)/2,fz+.005),(2*W-.02,y1-y0,.05))
    for s in (-1,1):
        for z in (fz-.005,bz-.045):box("Post"+str(s)+str(z),"Wood",(s*(W-.045),tb/2,z),(.09,tb,.09))
    box("Top","Wood",(0,(tb+s0)/2,(oz+bz)/2),(2*W+.02,s0-tb,bz-oz),.012)
    box("TopFascia","PaintRose",(0,(1.075+s0)/2,oz-.0075),(2*W+.04,s0-1.075,.025))
    for i,hx in enumerate(k["hx"]):
        box("Ledge"+str(i),"Metal",(hx,lt-.0175,(fz+lz)/2),(lw,.035,fz-lz))
        box("LedgeLip"+str(i),"Gold",(hx,lt-.0075,lz),(lw,.055,.02))
        for dx in (-.38,.38):prism("LedgeGusset"+str(i)+str(dx),"Metal",[(fz,lt-.035),(fz-.15,lt-.035),(fz,lt-.17)],hx+dx-.015,hx+dx+.015)
    for s in (-1,1):
        q=str(s)
        box("Cabinet"+q,"Dark",(s*(cx+W-.01)/2,(ct-.04)/2,(bz+cbz)/2),(W-.01-cx,ct-.04,cbz-bz))
        box("CabinetTop"+q,"Wood",(s*(cx-.01+W)/2,ct-.02,(bz+cbz)/2),(W-cx+.01,.04,cbz-bz+.02),.008)
        box("CabinetPanel"+q,"PaintMint",(s*(cx+W-.01)/2,(s0+ct-.04)/2,bz-.005),(W-.05-cx,ct-.04-s0,.012))
        box("EndPanel"+q,"PaintMint",(s*(W-.004),(.19+1.035)/2,(fz+bz)/2),(.012,1.035-.19,bz-fz-.2))
        box("CabinetEnd"+q,"PaintCream",(s*(W-.004),(.19+ct-.1)/2,(bz+cbz)/2),(.012,ct-.29,cbz-bz-.2))
    for x in ux:
        y0=ct if abs(x)>sw else st;box("Upright"+str(x),"Wood",(x,(y0+ht)/2,(uz0+uz1)/2),(.08,ht-y0,uz1-uz0))
        prism("Gusset"+str(x),"Wood",[(uz0,sb),(sz0+.1,sb),(uz0,sb-.3)],x-.025,x+.025)
    box("Header","Dark",(0,(hb+ht)/2,(uz0+uz1)/2),(2*W,ht-hb,uz1-uz0+.04))
    box("HeaderStripe","PaintGold",(0,(hb+ht)/2,uz0-.025),(2*W,.04,.012))
    box("Shelf","Wood",(0,(sb+s1)/2,(sz0+sz1)/2),(2*W,s1-sb,sz1-sz0))
    box("ShelfFascia","PaintRose",(0,(fb+s1)/2,sz0-.0125),(2*W,s1-fb,.025))
    box("BackRail","Wood",(0,s1+.04,uz0-.02),(2*W,.08,.04))
    cz=sz0-.03;P=[];B=[];nb=k["bulbs"]
    for a,b in zip(ux,ux[1:]):
        P+=[(a+(b-a)*j/8,fb+.02-.07*math.sin(math.pi*j/8),cz) for j in range(8)]
        B+=[(a+(b-a)*j/(nb+1),fb+.02-.07*math.sin(math.pi*j/(nb+1)),cz) for j in range(1,nb+1)]
    tube("Festoon","Dark",P+[(ux[-1],fb+.02,cz)],.008,4)
    for i,(x,y,z) in enumerate(B):
        cyl("Socket"+str(i),"Dark",(x,y+.004,z),(x,y-.03,z),.016,6)
        lathe("Bulb"+str(i),"StageGlowGold" if i%2==0 else "StageGlowRose",[(.014,0),(.042,-.026),(.04,-.062),(.018,-.086),(.005,-.092)],(x,y-.028,z),k=6)
    bx=W-.30
    cyl("BellBase","Dark",(-bx,s0,-1.38),(-bx,s0+.025,-1.38),.075,10)
    lathe("Bell","Gold",[(.065,.02),(.062,.05),(.045,.085),(.02,.10),(.004,.102)],(-bx,s0,-1.38),k=10)
    cyl("BellButton","Metal",(-bx,s0+.095,-1.38),(-bx,s0+.13,-1.38),.012,6)
    box("CashBox","PaintMint",(bx,s0+.07,-1.35),(.34,.14,.24),.015)
    tube("CashHandle","Gold",[(bx-.08,s0+.135,-1.35),(bx-.08,s0+.19,-1.35),(bx+.08,s0+.19,-1.35),(bx+.08,s0+.135,-1.35)],.012,5)
    cl=[((-.2,.02,-2.12),(.2,1.45,-1.80)),((-2.40,.01,-.855),(2.40,1.43,.63)),((-1.85,2.46,-.62),(1.85,2.98,-.50)),k["sign"]]
    for s in (-1,1):cl+=[((s*2.25-.08,0,.97),(s*2.25+.08,3.4,1.13)),((s*2.2-.08,2.87,-.51),(s*2.2+.08,2.95,.21))]
    for r,(ty,tz) in enumerate(k["tag"]):
        for hx in k["hx"]:
            y,z,d,h=k["hy"][r]+ty,k["hz"][r]+tz,(.10,.255)[r],(.32,.45)[r];cl.append(((hx-.48,y-.125,z-.045),(hx+.48,y+.125,z+.025)))
            cl.append(((hx-.56,k["s"][r]+.005,k["hz"][r]-d),(hx+.56,k["s"][r]+h,k["hz"][r]+d)))
    return cl
def tag():
    t=C["tag"];bw,bh,bd=t["board"];ew,eh,ed,ey=t["edge"];fx,fw,fh,fd=t["foot"];(y0,z0),(y1,z1),sw,sk=t["strut"]
    kit("FestivalPriceTag",[("GroundAnchor","Wood",(0,-bh/2-.02,.03),(1.1,.04,.34),0)])
    box("Board","Dark",(0,0,0),(bw,bh,bd),.006)
    box("TopEdge","Gold",(0,ey,0),(ew,eh,ed),.006)
    for s in (-1,1):box("Foot"+str(s),"Gold",(s*fx,-bh/2+fh/2,0),(fw,fh,fd),.005)
    slab("Strut","Dark",(0,y0,z0),(0,y1,z1),sw,sk)
    cyl("Hinge","Gold",(-sw/2-.01,y0+.01,bd/2+.008),(sw/2+.01,y0+.01,bd/2+.008),.012,6)
    return [((-.46,-.095,-.045),(.46,.095,-.026))]
def bunting():
    b=C["bunt"];H=b["h"];r0,r1=b["r"];fw,fh=b["foot"];sr,sh=b["sleeve"];ay,al,ar,by=b["arm"];x0,x1,yt,ys,yp,th=b["flag"];amp,cyc=b["wave"];s0,s1=b["stripe"]
    kit("FestivalBuntingPole")
    box("Footing","Stone",(0,fh/2,0),(fw,fh,fw),.03)
    for sx in (-1,1):
        for sz in (-1,1):cyl("Bolt"+str(sx)+str(sz),"Metal",(sx*.15,fh-.005,sz*.15),(sx*.15,fh+.02,sz*.15),.022,6)
    cyl("Collar","Metal",(0,fh-.005,0),(0,sh,0),sr,8)
    cyl("Pole","Wood",(0,fh,0),(0,H,0),r0,8,r1)
    cyl("PoleCap","Dark",(0,H-.02,0),(0,H+.04,0),r1+.015,8)
    ico("Finial","Gold",(0,H+.04+b["cap"]*.8,0),b["cap"],1)
    for y in (ay,by):cyl("Clamp"+str(y),"Metal",(0,y-.05,0),(0,y+.05,0),.07,8)
    cyl("Arm","Metal",(0,ay,0),(al,ay,0),ar,6)
    ico("ArmKnob","Gold",(al+.02,ay,0),.045,1)
    cyl("Brace","Metal",(.05,by,0),(x0-.05,ay-.02,0),.015,6)
    cyl("Sleeve","CanvasCream",(x0-.01,ay,0),(x1+.01,ay,0),.042,8)
    for x in (x0+.03,(x0+x1)/2,x1-.03):tube("Tie"+str(x),"Gold",[(x,ay+.05*math.cos(i*math.pi/4),.05*math.sin(i*math.pi/4)) for i in range(8)],.01,4,True)
    wz=lambda x,y:amp*(yt-y)/(yt-yp)*math.sin(2*math.pi*cyc*(x-x0)/(x1-x0)+.4)
    bot=lambda x:ys-(ys-yp)*(1-abs(x-(x0+x1)/2)/((x1-x0)/2))
    X=[x0+(x1-x0)*i/12 for i in range(13)]
    rings=[]
    for x in X:
        Y=[yt+(bot(x)-yt)*j/5 for j in range(6)];rings.append([(x,y,wz(x,y)-th/2) for y in Y]+[(x,y,wz(x,y)+th/2) for y in Y[::-1]])
    loft("Flag","CanvasRose",rings)
    loft("FlagStripe","CanvasCream",[[(x,y,wz(x,y)-th/2-.008) for y in (s1,s0)]+[(x,y,wz(x,y)+th/2+.008) for y in (s0,s1)] for x in X])
def lamp():
    l=C["lamp"];pw,pt=l["plate"];r0,r1,tb=l["shaft"];tb,tt,gt,rt,th,g0,g1,rh=l["lant"];by,br=l["bracket"]
    kit("FestivalLightPole")
    box("BasePlate","Metal",(0,pt/2,0),(pw,pt,pw),.01)
    for sx in (-1,1):
        for sz in (-1,1):cyl("Bolt"+str(sx)+str(sz),"Gold",(sx*.17,pt-.005,sz*.17),(sx*.17,pt+.025,sz*.17),.025,6)
    lathe("CastFoot","Dark",l["foot"],k=8)
    cyl("FootCollar","Gold",(0,.52,0),(0,.60,0),.10,8)
    cyl("Shaft","Dark",(0,.55,0),(0,tb,0),r0,8,r1)
    cyl("MidRing","Gold",(0,3.2,0),(0,3.27,0),.08,8)
    box("Hatch","Metal",(0,1.05,-.066),(.075,.22,.03))
    cyl("TopCollar","Gold",(0,by-.05,0),(0,by+.05,0),.085,8)
    for dx,dz in ((1,0),(-1,0),(0,1),(0,-1)):tube("Scroll"+str(dx)+str(dz),"Dark",[(dx*a,y,dz*a) for a,y in ((.05,by),(.14,by+.05),(.21,by+.15),(br,by+.28),(br-.01,tb+.005))],.018,6)
    box("Tray","Dark",(0,(tb+tt)/2,0),(2*th,tt-tb,2*th),.01)
    sq=lambda h,y:[(-h,y,-h),(h,y,-h),(h,y,h),(-h,y,h)]
    loft("Glass","StageGlowGold",[sq(g0,tt),sq(g1,gt)])
    for sx in (-1,1):
        for sz in (-1,1):slab("Mullion"+str(sx)+str(sz),"Dark",(sx*g0,tt,sz*g0),(sx*g1,gt,sz*g1),.04,.04)
    box("Cornice","Dark",(0,gt+.015,0),(2*g1+.08,.03,2*g1+.08))
    loft("Roof","Dark",[sq(rh,gt+.03),sq(.06,rt)])
    cyl("RoofCap","Gold",(0,rt-.01,0),(0,rt+.04,0),.05,8)
    ico("Finial","Gold",(0,rt+.08,0),.045,1)
def dj():
    d=C["dj"];hw,top,fz,lb,bb,lz=d["plinth"];rw,rt,r0,r1=d["riser"];rx0,rx1,rz,rwd,rh=d["ramp"];dw,dd,dh=d["deck"]
    kit("FestivalDJRiser",[("StageDeck","Dark",(0,-.7,1.85),(18,1.4,9),0)])
    box("CaseBody","Dark",(0,lz/2,(fz+.02+bb)/2),(2*hw-.04,lz,bb-fz-.02))
    box("CaseLid","Dark",(0,(lz+top)/2,(fz+lb)/2),(2*hw,top-lz,lb-fz))
    for s in (-1,1):box("LidEdgeX"+str(s),"Metal",(s*(hw+.005),(lz-.005+top)/2,(fz+lb)/2),(.012,top-lz+.005,lb-fz+.02))
    for z,s in ((fz,-1),(lb,1)):box("LidEdgeZ"+str(s),"Metal",(0,(lz-.005+top)/2,z+s*.005),(2*hw+.02,top-lz+.005,.012))
    box("BaseBumper","Rubber",(0,.0175,(fz+.01+bb+.01)/2),(2*hw-.02,.035,bb-fz))
    for sx in (-1,1):
        for z in (fz+.03,lb-.03):box("LidCorner"+str(sx)+str(z),"Metal",(sx*(hw-.025),top-.0375,z),(.075,.075,.075),.01)
        for z in (fz+.05,bb-.025):box("FootCorner"+str(sx)+str(z),"Metal",(sx*(hw-.05),.0375,z),(.075,.075,.075),.01)
        for z in (-.35,.10):
            box("HandleDish"+str(sx)+str(z),"Metal",(sx*(hw-.016),.09,z),(.016,.075,.24))
            cyl("HandleBar"+str(sx)+str(z),"Dark",(sx*(hw-.004),.09,z-.08),(sx*(hw-.004),.09,z+.08),.012,6)
        box("Latch"+str(sx),"Metal",(sx*(hw-.016),.125,-.125),(.02,.05,.07))
    N=28;f=[(-hw+.04+(2*hw-.08)*i/N,fz+.004+(.012 if i%2 else 0)) for i in range(N+1)]
    loft("Skirt","CanvasDark",[[(x,y,z) for x,z in f+[(x,z+.008) for x,z in f[::-1]]] for y in (.035,lz)])
    box("SkirtHem","PaintRose",(0,.045,fz),(2*hw-.08,.02,.03))
    box("SocketPlate","Metal",(hw-.014,.09,.38),(.012,.09,.14))
    for i,(y,z,m) in enumerate(((.07,.35,"Rubber"),(.09,.38,"Rose"),(.11,.41,"Gold"))):tube("Cable"+str(i),m,[(hw-.01,y,z),(hw+.05,y-.005,z),(hw+.12,.03,rz+(z-.38)*.6),(rx0+.10,.012,rz+(z-.38)*.5)],.011,5)
    prism("CableRamp","Rubber",[(rz-rwd/2,0),(rz+rwd/2,0),(rz+rwd/2-.07,rh),(rz-rwd/2+.07,rh)],rx0,rx1)
    box("RampStripe","Gold",((rx0+rx1)/2,rh+.002,rz),(rx1-rx0-.04,.006,.04))
    box("FloorBox","Dark",(rx1+.14,.09,rz),(.30,.18,.34),.015)
    box("FloorBoxPanel","Metal",(rx1-.01,.10,rz),(.02,.10,.22))
    box("RiserFrame","Dark",(0,(rt-.01)/2,(r0+r1)/2),(2*rw,rt-.01,r1-r0))
    box("RiserMat","Rubber",(0,rt-.005,(r0+r1)/2),(2*rw-.04,.01,r1-r0-.03))
    box("RiserGlowBack","StageGlowMint",(0,.035,r1+.005),(2*rw-.04,.022,.012))
    for s in (-1,1):box("RiserGlow"+str(s),"StageGlowMint",(s*(rw+.005),.035,(r0+r1)/2),(.012,.022,r1-r0-.08))
    return [((-dw,top+.005,-dd),(dw,top+dh,dd)),((-.22,top+.005,lb+.013),(.22,1.6,.85))]
def prism_tin():
    t=C["tin"];r0,r1,r2,dy=t["emb"];ld,l0,l1=t["label"]
    kit("FestivalStockPrism",[("Ground","Stone",(0,-.04,0),(2.2,.1,1.0),0)])
    olathe("Tin","Metal",t["prism"]);olathe("Label","Cream",[(ld,l0),(ld,l1)]);olathe("LidRim","Gold",t["rim"])
    R=t["rim"][-1][0]/math.cos(Q);yb=t["rim"][-1][1]
    for j in range(8):
        a,b=Q+j*2*Q,Q+(j+1)*2*Q;bm=bmesh.new()
        v=[bm.verts.new(A(*p)) for p in ((0,t["apex"],0),(R*math.cos(a),yb,R*math.sin(a)),(R*math.cos(b),yb,R*math.sin(b)),(0,yb,0))]
        for f in ((0,1,2),(0,2,3),(0,3,1),(1,3,2)):bm.faces.new([v[i] for i in f])
        ob("Facet"+str(j),("Rose","Gold","Mint","Blue")[j%4],bm)
    g=[(-.065,.012),(-.04,.035),(-.013,.035),(.013,.035),(.04,.035),(.065,.012),(0,-.055)];gc=(-.022,.012);gd=(.022,.012)
    emb("GemRim","Dark",[(x*1.14,y*1.14-.002) for x,y in (g[0],g[1],g[4],g[5],g[6])],r0,r1-.0015,dy)
    for n,m,pts in (("CrownL","Rose",[g[0],g[1],g[2],gc]),("CrownM","Gold",[gc,g[2],g[3],gd]),("CrownR","Mint",[gd,g[3],g[4],g[5]]),("PavL","Blue",[g[0],gc,g[6]]),("PavM","Mint",[gc,gd,g[6]]),("PavR","Rose",[gd,g[5],g[6]])):emb(n,m,pts,r0,r1,dy)
    for i,(x,y,r) in enumerate(((.072,.042,.017),(-.074,-.036,.013),(.06,-.05,.009))):emb("Spark"+str(i),"White",star(x,y,r),r0,r2,dy)
def moon_tin():
    t=C["tin"];br,b0,b1=t["body"];ro,ri,d,cy,ox=t["cres"];T=rot(11.25,"y");P=t["dome"]
    kit("FestivalStockMoon",[("Ground","Stone",(0,-.04,0),(.8,.1,.8),0)])
    lathe("BaseRing","Gold",t["moon"],k=16,T=T);lathe("Tin","Blue",[(br,b0),(br,b1)],k=16,T=T);lathe("LidBand","Gold",t["band"],k=16,T=T);lathe("Dome","Cream",P,k=16,T=T)
    for i,(a,f,r) in enumerate(t["craters"]):
        i0=int(f);u=f-i0;(ra,ya),(rb,yb)=P[i0],P[i0+1];rr,yy=ra+(rb-ra)*u,ya+(yb-ya)*u;L=math.hypot(yb-ya,ra-rb);nr,ny=(yb-ya)/L,(ra-rb)/L;a=math.radians(a)
        c=(rr*math.cos(a),yy,rr*math.sin(a));n=(nr*math.cos(a),ny,nr*math.sin(a));cyl("Crater"+str(i),"Stone",ad(c,n,-.008),ad(c,n,.004),r,8)
    emb("Crescent","Gold",crescent(ro,ri,d,ox=ox),br-.003,br+.004,cy,False)
    for i,(x,y,r) in enumerate(((.052,.035,.019),(.068,-.03,.013),(-.07,.07,.011))):emb("Star"+str(i),"White",star(x,y,r),br-.003,br+.003,cy,False)
def ctx(k,g,res,pos=(0,0,0),s=1):
    old=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=os.path.join(R0,C["res"]+res+".fbx"));T=M.Translation(RZ@A(*pos))@M.Scale(s,4)
    for o in set(bpy.data.objects)-old:
        o["ctx"]=k;o["grp"]=g
        if o.parent is None:o.matrix_world=T@o.matrix_world
        for sl in o.material_slots:sl.material=MAT.get(o.name.split("__")[-1].split(".")[0],MAT["Dark"])
def dup(src,k,g,pos,s=1):
    T=M.Translation(RZ@A(*pos))@M.Scale(s,4)
    for o in [o for o in PARTS[src].values() if o["tag"]!="shell"]:c=o.copy();del c["kit"];c["ctx"]=k;c["grp"]=g;bpy.context.scene.collection.objects.link(c);c.matrix_world=T@o.matrix_world
def fig(k,x,y,z,h,r):
    P=PARTS[KIT[0]];os_=[cyl("FigBody","Blue",(x,y,z),(x,y+h-2*r,z),r,10),ico("FigHead","Cream",(x,y+h-r,z),r,1),box("FigNose","Rose",(x,y+h-r,z-r),(.05,.05,.12))]
    for n in ("FigBody","FigHead","FigNose"):del P[n]
    for o in os_:COL[KIT[0]].objects.unlink(o);bpy.context.scene.collection.objects.link(o);del o["kit"];o["ctx"]=k;o["grp"]="fig"
def snap(k,nm,eye,at,lens=35,g=None):
    render(os.path.join(C["rev"],k.replace("Festival","")+"_"+nm+".png"),camera(eye,at,lens),lambda o:(o.get("kit")==k and (o.get("tag")!="shell" or o.name.startswith("Ground"))) or (o.get("ctx")==k and (g is None or o.get("grp") in g)))
CL={"FestivalMarketCounter":counter(),"FestivalPriceTag":tag(),"FestivalDJRiser":dj()}
bunting();lamp();prism_tin();moon_tin()
K,mk=KITS[0],C["mk"]
for x,n in ((-6,"FestivalStallSupplies"),(0,"FestivalStallPerformance"),(6,"FestivalStallStock")):ctx(K,"stall0" if x==0 else "stall",n,(x,0,1.05))
for r,row in enumerate((mk["row0"],mk["row1"])):
    for hx,(res,cnt) in zip(mk["hx"],row):
        for i in range(cnt):
            p=(hx+(i-(cnt-1)/2)*.31,mk["hy"][r]+.12,mk["hz"][r]);s=.43 if cnt==1 else .29
            if res in KITS:dup(res,K,"item",p,s)
            else:ctx(K,"item",res,p,s)
        dup("FestivalPriceTag",K,"tag",(hx,mk["hy"][r]+mk["tag"][r][0],mk["hz"][r]+mk["tag"][r][1]))
for x in (-6,0,6):fig(K,x,0,mk["vendor"][1],1.40,.17)
dd=C["dj"];DK="FestivalDJRiser"
ctx(DK,"stage","FestivalStage",dd["stage"]);ctx(DK,"deck","FestivalDJDeck",(0,dd["plinth"][1],0));fig(DK,*dd["who"],1.45,.16)
for x in (-3.65,3.65):fig(DK,x,0,-.6,1.5,.17)
ctx("FestivalStockPrism","old","FestivalStock",(-.62,0,0));dup("FestivalStockMoon","FestivalStockPrism","moon",(.62,0,0))
V=[(K,"player",(0,1.62,-4.6),(0,1.45,-.6),26),(K,"three_quarter",(-5.5,3.0,-6.0),(0,1.3,-.5),30),(K,"tags_closeup",(1.6,1.0,-3.4),(.6,.9,-1.2),30),(K,"upper_shelf",(-.8,1.62,-2.6),(-.8,1.9,-.2),30),(K,"bare",(-4.0,2.4,-5.5),(0,1.2,-.5),30,("stall","stall0","fig")),(K,"side",(5.2,2.2,-3.2),(2.4,1.3,-.6),30,("stall0","item","tag","fig")),
("FestivalPriceTag","front",(0,.05,-1.0),(0,-.02,0),45),("FestivalPriceTag","back",(.8,.35,.9),(0,-.03,0),45),
("FestivalBuntingPole","full",(6.0,3.6,-15.0),(1.0,4.0,0),30),("FestivalBuntingPole","flag",(1.8,7.0,-3.2),(1.2,6.85,0),40),("FestivalBuntingPole","player",(2.5,1.62,-6.5),(1.0,6.0,0),28),
("FestivalLightPole","full",(4.0,3.4,-13.0),(0,3.7,0),30),("FestivalLightPole","lantern",(1.1,7.3,-1.6),(0,6.95,0),40),("FestivalLightPole","player",(1.8,1.62,-4.5),(0,6.3,0),26),
(DK,"audience",(0,.22,-7.0),(0,.6,0),30),(DK,"rear",(3.2,1.9,3.6),(0,.25,.5),30),(DK,"detail",(2.9,.75,-1.3),(1.6,.1,.2),35),(DK,"deck",(-2.8,2.6,2.8),(0,.3,.4),30,("deck","fig")),(DK,"bare",(3.0,1.4,-2.6),(.4,.1,.3),30,("fig",)),
("FestivalStockPrism","compare",(0,.42,-2.2),(0,.3,0),45),("FestivalStockPrism","compare_top",(.8,1.2,-1.6),(0,.3,0),45),("FestivalStockPrism","compare_back",(-.3,.5,2.0),(0,.3,0),45),("FestivalStockPrism","detail",(.35,.62,-.95),(0,.3,0),45,()),("FestivalStockMoon","detail",(.35,.62,-.95),(0,.3,0),45)]
for v in V:snap(*v)
gallery(KITS,os.path.join(C["rev"],"gallery_all.png"),cols=4)
gallery(KITS[1:2]+KITS[5:],os.path.join(C["rev"],"gallery_small.png"),cols=3,gap=.4)
for o in [o for o in bpy.data.objects if o.get("ctx")]:bpy.data.objects.remove(o,do_unlink=True)
bpy.context.view_layer.update()
X={k:{"bounds":[[round(v,3) for v in b] for b in kbb(k)]} for k in KITS}
X["FestivalMarketCounter"].update(origin=[-18,0,-20.05],surfaces={"row0":[mk["s"][0],-21.15],"row1":[mk["s"][1],-20.05]},collision={"center":[-18,.59,-21.35],"size":[6.7,1.18,.82]})
X["FestivalPriceTag"].update(origin="board centre",textOffset=[0,0,-.038])
X["FestivalBuntingPole"].update(origin=[0,0,0],flagCentre=[1.5,6.8,0],recolour={"CanvasRose":"CanvasGold"})
X["FestivalLightPole"].update(origin=[0,0,0],light=[0,7,0],recolour={"StageGlowGold":"StageGlowRose"})
X["FestivalDJRiser"].update(origin=[0,1.40,30.15],surfaces={"plinthTop":1.62,"riserTop":1.48})
for k in KITS[5:]:X[k].update(origin="bottom centre",reference=[list(v) for v in C["stock"]])
finish(KITS,C["out"],C["src"],C["man"],clear=CL,extra=X)
