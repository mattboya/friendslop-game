CONFIG={"prefix":"AH04_","fbx":"Assets/Festival/Art/ProductionSample04","src":"ArtSource/ProductionPackage04/CatalogItems.blend","man":"ArtSource/ProductionPackage04/catalog-manifest.json","rev":"artifacts/production04-catalog","text_res":2,"hand_size":.5,"kits":(("GlowstickBundle","LandmarkStickers","DisposableCamera","BuddyTether","EarDefenders","ClaimTicket","ComplaintClipboard","WorkOrderClipboard"),("PortableSpeaker","BubbleMachine","VendorTrolley","MysteryStockBox","MascotHead"),("PoiRibbon","PoiGlow","PoiPixel","PoiFire","PoiDoubleEnded")),
"hand":{"glow":(.025,.56),"glow_mats":("StageGlowMint","StageGlowRose","StageGlowGold"),"sheet":(.44,.60,.006),"stickers":(("sun","Rose"),("moon","Blue"),("star","Mint"),("heart","Cream"),("tent","Gold"),("tree","Cream")),"cam":(.36,.20,.12),"tether":(.20,.12,.012,.46,.03,11),"ear":(.17,.095,.075),"ticket":(.34,.56,.005),"clip":(.46,.64,.02,.42,.54)},
"bulk":{"speaker":(.62,.40,.26,.09),"bubble":(.40,.34,.32,.12,8),"trolley":(1.3,.7,.85,2.0,.22,.075),"box":(.46,.40,.40)},"mascot":{"c":.36,"r":(.49,.55),"wall":.03,"neck":-60,"form":(.46,.66,.40),"pad":.12,"rays":8},
"poi":{"x":.15,"top":1.25,"handle":(.027,.125),"cord":(.008,.435),"head":.085,"peg":.03,"links":7,"ribs":{"PoiRibbon":"Rose","PoiGlow":"Mint","PoiPixel":"Gold","PoiFire":"Metal","PoiDoubleEnded":"Gold"}},
"sign":{"plate":(1.4,.36,.04,.18),"post":.07,"text":.16,"pitch":1.75,"y":1.2,"plates":(("Stage","STAGE","Rose","Cream","star",1,""),("Medical","MEDICAL","Mint","Dark","cross",-1,""),("Shuttle","SHUTTLE","Gold","Dark","bus",1,""),("Market","MARKET","Blue","Cream","bag",-1,""),("Camp","CAMP","Wood","Cream","tent",1,""),("WobbleStage","STAGE","Rose","StageGlowGold","star",-1,"wobble"),("BothWaysMedical","MEDICAL","Mint","StageGlowRose","cross",1,"both"),("DroopShuttle","SHUTTLE","Gold","StageGlowRose","bus",1,"droop"),("TwistCamp","CAMP","Wood","StageGlowMint","tent",-1,"twist"))}}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
C=CONFIG;HD,BK,MS,PO,SG=C["hand"],C["bulk"],C["mascot"],C["poi"],C["sign"];P=C["prefix"];HAND,BULK,POI=C["kits"]
KITS=[P+n for n in HAND+BULK+POI]+[P+"SignPlates"]
setup(KITS);bpy.context.scene.display.shading.show_backface_culling=True
PM=M(((1,0,0),(0,0,1),(0,1,0)));FRONT=rot(90,"x");BACK=rot(180,"y")@FRONT;BUN=[];ROOT={};EXTRA={};PLATES=[];GW={}
def txt(n,m,t,c,size=.1,depth=.004,R=FRONT,T=I,f=None,al="CENTER",**kw):
    cu=bpy.data.curves.new(n,"FONT");cu.body=t;cu.size=size;cu.extrude=depth/2;cu.align_x=al;cu.align_y="CENTER";cu.resolution_u=C["text_res"]
    tmp=bpy.data.objects.new(n,cu);bpy.context.scene.collection.objects.link(tmp);bpy.context.view_layer.update()
    me=bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()));bpy.data.objects.remove(tmp);bpy.data.curves.remove(cu)
    bm=bmesh.new();bm.from_mesh(me);bpy.data.meshes.remove(me);bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-5)
    for l in list(bm.loops.layers.uv.values()):bm.loops.layers.uv.remove(l)
    bmesh.ops.transform(bm,matrix=T@M.Translation(A(*c))@R,verts=bm.verts[:])
    for v in (bm.verts if f else ()):v.co=A(*f(v.co.x,v.co.z,v.co.y))
    o=ob(n,m,bm,**kw);nz=(RZ@T@R).to_3x3()@V((0,0,1))
    for p in (o.data.polygons if not depth else ()):
        if p.normal.dot(nz)<0:p.flip()
    return o
def weld(o):
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-5);bmesh.ops.dissolve_degenerate(bm,dist=1e-6,edges=bm.edges[:])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(o.data);bm.free();return o
def poly(n,m,pts,y0,y1,T=I,**kw):return loft(n,m,[[(x,y0,z) for x,z in pts],[(x,y1,z) for x,z in pts]],T=T,**kw)
def ring(n,m,c,r,rt,plane="xz",k=12,s=5,sq=1,**kw):
    i,j="xyz".index(plane[0]),"xyz".index(plane[1]);pts=[]
    for q in range(k):p=list(c);a=q*2*math.pi/k;p[i]+=r*math.cos(a);p[j]+=r*sq*math.sin(a);pts.append(tuple(p))
    return tube(n,m,pts,rt,s,True,**kw)
def rr(w,l,r,n=3):return [(sx*(w/2-r)+r*math.cos(math.radians(a0+90*q/n)),sz*(l/2-r)+r*math.sin(math.radians(a0+90*q/n))) for sx,sz,a0 in ((1,1,0),(-1,1,90),(-1,-1,180),(1,-1,270)) for q in range(n+1)]
def frame(f,p,h=1e-3):
    q=V(f(*p));d=[(V(f(*[p[j]+h*(i==j) for j in range(3)]))-V(f(*[p[j]-h*(i==j) for j in range(3)]))).normalized() for i in range(3)]
    x=d[0];y=(d[1]-x*x.dot(d[1])).normalized();L=M((x,y,x.cross(y))).transposed()
    return M.Translation(A(*q))@(PM@L@PM).to_4x4()
def gw(ch,sz):
    if ch not in GW:o=txt("_gw","Dark",ch,(0,0,0),1.0);b=bb(o);GW[ch]=b[1].x-b[0].x;bpy.data.objects.remove(o,do_unlink=True);del PARTS[KIT[0]]["_gw"]
    return GW[ch]*sz
def keep(pre,piv,up=None):BUN.append((KIT[0],pre,piv,up))
def bundle(k,pre,piv,up):
    KIT[0]=k;G={};root=None
    for n in [n for n in PARTS[k] if n.startswith(pre+".")]:o=PARTS[k].pop(n);G.setdefault(o["mat"],[]).append(o)
    assert G,pre
    for m,os_ in G.items():
        bm=bmesh.new();sm=os_[0].data.polygons[0].use_smooth
        for o in os_:me=o.data.copy();me.transform(o.matrix_world);bm.from_mesh(me);bpy.data.meshes.remove(me);bpy.data.objects.remove(o,do_unlink=True)
        for l in list(bm.loops.layers.uv.values()):bm.loops.layers.uv.remove(l)
        bmesh.ops.transform(bm,matrix=RZ.inverted(),verts=bm.verts[:])
        o=ob(k[len(P):]+"_"+pre+"__"+m,m,bm,piv=piv,smooth=sm,tag="keep")
        if root:parent(o,root)
        else:root=o
    if up:parent(root,up)
    return root
def table(k,w=1.2,d=1.0):kit(P+k,[("Table","Wood",(0,-.02,0),(w,.04,d),0)])
def hand(k,grip,anchor,note):EXTRA[P+k]={"grip":grip,"handAnchor":[round(v,3) for v in anchor],"runtimeSize":C["hand_size"],"orientation":note}
def icon(pre,kind,T,y=0,t=.003,s=1,col=None):
    Q=lambda pts:[(x*s,z*s) for x,z in pts]
    if kind=="star":poly(pre+"star",col or "Gold",Q([(.05*(1,.42)[q%2]*math.sin(q*math.pi/5),.05*(1,.42)[q%2]*math.cos(q*math.pi/5)) for q in range(10)]),y,y+t,T=T)
    elif kind=="sun":
        cyl(pre+"sun","Gold",(0,y,0),(0,y+t,0),.028*s,12,T=T)
        for q in range(8):a=q*math.pi/4;poly(pre+"ray%d"%q,"Gold",Q([(.026*math.cos(a-.22),.026*math.sin(a-.22)),(.052*math.cos(a),.052*math.sin(a)),(.026*math.cos(a+.22),.026*math.sin(a+.22))]),y,y+t,T=T)
    elif kind=="moon":
        tx,tz=.045*math.cos(math.radians(50)),.045*math.sin(math.radians(50));cx=.022;ri=math.hypot(tx-cx,tz);ta=math.degrees(math.atan2(tz,tx-cx))
        poly(pre+"moon","Cream",Q([(.045*math.cos(math.radians(a)),.045*math.sin(math.radians(a))) for a in range(50,311,20)]+[(cx+ri*math.cos(math.radians(360-ta-(360-2*ta)*q/8)),ri*math.sin(math.radians(360-ta-(360-2*ta)*q/8))) for q in range(1,8)]),y,y+t,T=T)
    elif kind=="heart":poly(pre+"heart","Rose",Q([(.003*16*math.sin(a)**3,.008+.003*(13*math.cos(a)-5*math.cos(2*a)-2*math.cos(3*a)-math.cos(4*a))) for a in [q*math.pi/10 for q in range(20)]]),y,y+t,T=T)
    elif kind=="tent":poly(pre+"tent",col or "Mint",Q([(-.05,-.035),(.05,-.035),(0,.045)]),y,y+t,T=T);poly(pre+"door","Dark",Q([(-.015,-.035),(.015,-.035),(0,.005)]),y+t,y+t+.001,T=T)
    elif kind=="tree":
        for q,(w,z) in enumerate(((.05,-.02),(.04,.01),(.03,.04))):poly(pre+"tree%d"%q,"Leaf",Q([(-w,z-.02),(w,z-.02),(0,z+.03)]),y,y+t,T=T)
        box(pre+"trunk","Wood",(0,y+t/2,-.05*s),(.016*s,t,.03*s),T=T)
    elif kind=="cross":box(pre+"crossA",col or "White",(0,y+t/2,0),(.03*s,t,.09*s),T=T);box(pre+"crossB",col or "White",(0,y+t/2,0),(.09*s,t,.03*s),T=T)
    elif kind=="bus":
        box(pre+"bus","Cream",(0,y+t/2,.005*s),(.11*s,t,.055*s),T=T)
        for q in range(3):box(pre+"win%d"%q,"Glass",((-.03+.03*q)*s,y+t+.0005,.015*s),(.022*s,.001,.018*s),T=T)
        for q in (-1,1):cyl(pre+"wheel%d"%q,"Dark",(.032*q*s,y+t,-.022*s),(.032*q*s,y+t+.002,-.022*s),.014*s,8,T=T)
    elif kind=="bag":
        poly(pre+"bag","Cream",Q([(-.045,-.045),(.045,-.045),(.035,.02),(-.035,.02)]),y,y+t,T=T)
        tube(pre+"handle","Cream",[(-.02*s,y+t/2,.018*s),(-.018*s,y+t/2,.045*s),(.018*s,y+t/2,.045*s),(.02*s,y+t/2,.018*s)],.004*s,4,T=T)
def glowsticks():
    r,L=HD["glow"];ms=HD["glow_mats"];table("GlowstickBundle");R0=3*r+.006
    for i,(x,z) in enumerate([(0,0)]+[(2*r*math.cos(q*math.pi/3),2*r*math.sin(q*math.pi/3)) for q in range(6)]):
        cyl("Stick%d"%i,ms[i%3],(x,.012,z),(x,L-.012,z),r,8)
        cyl("CapB%d"%i,"White",(x,0,z),(x,.014,z),r*.92,8);cyl("CapT%d"%i,"White",(x,L-.014,z),(x,L,z),r*.92,8)
        if i in (2,5):ring("Hook%d"%i,"White",(x,L+.01,z),.012,.003,"xy",8,4)
    for j,y in enumerate((.09,L-.09)):cyl("Band%d"%j,"Rubber",(0,y-.012,0),(0,y+.012,0),3*r+.003,12)
    cyl("Wrap","Cream",(0,L/2-.07,0),(0,L/2+.07,0),R0,12)
    txt("WrapText","Rose","GLOW",(0,L/2,-(R0+.001)),.036,depth=0,f=lambda x,y,z:(-z*math.sin(x/R0),y,z*math.cos(x/R0)))
    hand("GlowstickBundle","RodR",(0,L/2,0),"stands on end; bundle axis +Y, wrap label faces -Z")
def stickers():
    W,L,t=HD["sheet"];table("LandmarkStickers")
    box("Sheet","White",(0,t/2,0),(W,t,L),.002);box("Header","Blue",(0,t+.0005,L/2-.06),(W-.04,.001,.08));txt("Title","White","MARK IT!",(0,t+.002,L/2-.06),.045,R=I,depth=0)
    for j,(kind,base) in enumerate(HD["stickers"]):
        x,z=(-.10,.10)[j%2],.11-.16*(j//2)
        T=M.Translation(A(x,t,z+.065))@rot(-20,"x")@M.Translation(A(0,0,-.065)) if kind=="heart" else M.Translation(A(x,t,z))
        cyl("Base%d"%j,base,(0,0,0),(0,.004,0),.065,16,T=T);icon("Icon%d"%j,kind,T,.004)
    hand("LandmarkStickers","PaperR",(W/2-.03,t,L/2-.12),"flat paper: face +Y, page top +Z (FestivalMap convention; tilt about X to face the camera)")
def camera():
    W,H,D=HD["cam"];table("DisposableCamera");fz=-D/2
    box("Body","Dark",(0,H/2,0),(W,H,D),.012);box("Sleeve","Rose",(-.03,H/2,0),(W*.66,H+.006,D+.006),.004);box("Stripe","Gold",(-.03,.07,0),(W*.66+.004,.035,D+.01))
    txt("Brand","Cream","SNAP!",(-.08,.115,fz-.004),.04,depth=0)
    cyl("LensBarrel","Dark",(.08,.09,fz),(.08,.09,fz-.045),.058,12);cyl("LensRing","Metal",(.08,.09,fz-.045),(.08,.09,fz-.052),.05,12);cyl("Lens","Glass",(.08,.09,fz-.05),(.08,.09,fz-.056),.036,12)
    box("Flash","Glass",(-.12,.165,fz-.006),(.08,.035,.01));box("Finder","Glass",(.08,.178,fz-.004),(.045,.026,.01));box("FinderBack","Glass",(.08,.178,-fz+.004),(.045,.026,.01))
    cyl("Shutter","Gold",(.12,H,-.01),(.12,H+.02,-.01),.02,8);cyl("Wind","Metal",(-.12,H,.02),(-.12,H+.025,.02),.032,10);box("Counter","Cream",(-.02,H+.003,.03),(.045,.006,.035))
    box("Lug","Dark",(-W/2-.008,.15,0),(.02,.03,.03))
    tube("Strap","Blue",[(-W/2-.012,.15,0),(-W/2-.05,.08,-.02),(-W/2-.09,.008,-.03),(-W/2-.13,.008,0),(-W/2-.09,.008,.03),(-W/2-.05,.08,.02),(-W/2-.012,.15,0)],.006,5)
    hand("DisposableCamera","TinR",(W/2-.07,H/2,0),"lens and flash face -Z (toward the first-person camera)")
def tether():
    cl,cw,cr,span,R,n=HD["tether"];table("BuddyTether",1.3,.5);a,b=cl/2,cw/2;yc=R+.006;g0,g1=math.radians(55),math.radians(125)
    for s,m in ((-1,"Rose"),(1,"Mint")):
        cx=s*(span/2+a+.03);pt=lambda t:(cx+a*math.cos(t),cr,b*math.sin(t)*(1 if math.sin(t)>0 else .85))
        tube("Loop%d"%s,m,[pt(g1+(2*math.pi-(g1-g0))*q/17) for q in range(18)],cr,5)
        cyl("Gate%d"%s,"Metal",pt(g0),pt(g1),cr*.8,6)
        xi=cx+a*(1 if s<0 else -1);ring("CordLoop%d"%s,"Blue",(xi,cr,0),.02,.005,"xy",10,4,sq=.55)
        cyl("Ferrule%d"%s,"Metal",(xi-s*.015,.011,0),(s*span/2,.011,0),.009,8)
    tube("Coil","Blue",[(-span/2+span*j/(n*6),yc+R*math.cos(math.pi+j*math.pi/3),R*math.sin(math.pi+j*math.pi/3)) for j in range(n*6+1)],.006,4)
    hand("BuddyTether","BagR",(-(span/2+a+.03),cr,-b*.85),"lies flat; Rose carabiner (-X) is the holder's end, Mint (+X) clips to the buddy")
def ears():
    cx,R,dp=HD["ear"];table("EarDefenders");cy=R*1.02-.001;yt=cy+R+.04
    for s in (-1,1):
        T=M.Translation(A(s*(cx-dp/2),cy,0))@rot(90*s,"z")
        lathe("Cup%d"%s,"Gold",[(R*.72,0),(R,.012),(R*1.02,.045),(R*.85,dp-.005),(R*.5,dp)],(0,0,0),14,T=T)
        cyl("Badge%d"%s,"Mint",(s*(cx+dp/2-.002),cy,0),(s*(cx+dp/2+.006),cy,0),.035,10)
        ring("Cushion%d"%s,"Rubber",(s*(cx-dp/2-.012),cy,0),.068,.022,"yz",12,6,smooth=True)
        tube("Yoke%d"%s,"Metal",[(s*cx,cy,-R*1.02-.006),(s*cx,cy+R*.8,-R*1.02-.006),(s*cx,yt,-.03),(s*cx,yt,.03),(s*cx,cy+R*.8,R*1.02+.006),(s*cx,cy,R*1.02+.006)],.008,6)
    loft("Band","Dark",[[(c*(cx+o),yt+c2*(cx+o),z) for o,z in ((-.009,-.024),(.009,-.024),(.009,.024),(-.009,.024))] for c,c2 in [(math.cos(q*math.pi/12),math.sin(q*math.pi/12)) for q in range(13)]])
    puff("Pad","Rose",(0,yt+cx-.022,0),(.14,.02,.05))
    hand("EarDefenders","BagR",(0,yt+cx,0),"cups on +/-X resting on the table, band arch in the XY plane; front -Z")
def ticket():
    W,L,t=HD["ticket"];table("ClaimTicket");c=.03;n=.018;zp=-.15
    poly("Card","Rose",[(-W/2+c,L/2),(W/2-c,L/2),(W/2,L/2-c),(W/2,zp+n),(W/2-n,zp),(W/2,zp-n),(W/2,-L/2+c),(W/2-c,-L/2),(-W/2+c,-L/2),(-W/2,-L/2+c),(-W/2,zp-n),(-W/2+n,zp),(-W/2,zp+n),(-W/2,L/2-c)][::-1],0,t)
    ring("Grommet","Gold",(0,t,L/2-.035),.016,.004,"xz",10,4);txt("Header","Cream","LOST + FOUND",(0,t+.002,L/2-.085),.032,R=I,depth=0)
    box("Panel","Cream",(0,t+.0005,.0),(.26,.001,.2));txt("Claim","Rose","CLAIM No.",(0,t+.002,.06),.03,R=I,depth=0);txt("Number","Dark","042",(0,t+.002,-.025),.09,R=I,depth=0)
    for q in range(11):box("Perf%d"%q,"Dark",(-.14+q*.028,t+.0005,zp),(.014,.001,.005))
    txt("StubNo","Cream","042",(0,t+.002,-.2),.05,R=I,depth=0);txt("Stub","Cream","KEEP ME",(0,t+.002,-.245),.024,R=I,depth=0)
    hand("ClaimTicket","PaperR",(W/2-.03,t,L/2-.12),"flat paper: face +Y, top +Z (FestivalStagePass convention)")
def clipboard(k,board,paper,head,ink,title,rows,pen,deco):
    W,L,T,pw,pl=HD["clip"];table(k);yp=T+.004;zc=L/2-.07
    poly("Board",board,rr(W,L,.03),0,T);box("Sheet0",paper,(.008,T+.001,-.03),(pw,.002,pl),R=rot(3,"y"));box("Sheet1",paper,(0,T+.003,-.02),(pw,.002,pl))
    box("ClipPlate","Metal",(0,T+.009,zc),(.22,.01,.06),.003)
    tube("ClipLever","Metal",[(-.08,T+.014,zc-.02),(-.08,T+.04,zc+.005),(-.08,T+.035,zc+.04),(.08,T+.035,zc+.04),(.08,T+.04,zc+.005),(.08,T+.014,zc-.02)],.006,5)
    for x in (-.08,.08):cyl("Rivet%d"%(x*100),"Gold",(x,T+.013,zc+.012),(x,T+.017,zc+.012),.009,8)
    box("Head",head,(0,yp+.0005,zc-.1),(pw-.04,.001,.07));txt("Title",ink,title,(0,yp+.002,zc-.1),.042,R=I,depth=0)
    for j,(w,tick) in enumerate(rows):
        z=zc-.2-j*.075;box("Box%d"%j,"Dark",(-.16,yp+.0005,z),(.036,.001,.036));box("BoxIn%d"%j,paper,(-.16,yp+.0012,z),(.026,.001,.026));txt("Row%d"%j,"Dark",w,(-.12,yp+.002,z),.032,R=I,al="LEFT",depth=0)
        if tick:slab("TickA%d"%j,"Rose",(-.176,yp+.003,z+.004),(-.162,yp+.003,z-.012),.009,.003);slab("TickB%d"%j,"Rose",(-.162,yp+.003,z-.012),(-.134,yp+.003,z+.03),.009,.003)
    box("SignLine","Dark",(-.02,yp+.0005,-.19),(.24,.001,.004))
    tube("Scribble","Blue",[(-.12,yp+.001,-.18),(-.09,yp+.001,-.165),(-.07,yp+.001,-.19),(-.04,yp+.001,-.17),(-.01,yp+.001,-.188)],.003,4)
    p0,p1=V((.182,T+.017,-.27)),V((.198,T+.017,.05));d=(p1-p0).normalized();bd,cp,tp=pen
    cyl("Pen",bd,tuple(p0),tuple(p1-d*.06),.012,8);cyl("PenCap",cp,tuple(p1-d*.07),tuple(p1),.0135,8);cyl("PenTip",tp,tuple(p0),tuple(p0-d*.03),.012,8,r2=.003)
    deco(yp);hand(k,"PaperR",(W/2-.03,T,L/2-.14),"flat board: face +Y, clip/top edge +Z (FestivalMap convention)")
def complaint(yp):
    ring("Face","Dark",(.12,yp+.001,.02),.045,.004,"xz",12,4)
    for x in (.105,.135):cyl("Eye%d"%(x*1000),"Dark",(x,yp,.035),(x,yp+.002,.035),.006,6)
    tube("Frown","Dark",[(.1+.04*q/4,yp+.001,-.005+.012*math.sin(q*math.pi/4)) for q in range(5)],.003,4);txt("Bang","Rose","!!",(.09,yp+.002,-.1),.06,R=I,depth=0)
def workorder(yp):
    zh=.10;box("Hazard","Gold",(0,yp+.0005,zh),(.38,.001,.024))
    for q in range(10):x=-.17+q*.038;poly("Haz%d"%q,"Dark",[(x-.008,zh-.012),(x+.004,zh-.012),(x+.016,zh+.012),(x+.004,zh+.012)][::-1],yp+.0008,yp+.0018)
    ring("Stamp","Mint",(.13,yp+.001,-.12),.045,.004,"xz",14,4);txt("StampOK","Mint","OK",(.13,yp+.002,-.12),.045,R=rot(15,"y"),depth=0)
    for q in range(14):box("Bar%d"%q,"Dark",(.05+q*.0095,yp+.0005,-.25),(.003+.002*(q%3),.001,.05))
def speaker():
    W,H,D,wr=BK["speaker"];kit(P+"PortableSpeaker");y0=.02;cy=y0+H/2;fz=-D/2;top=y0+H
    for x in (-W/2+.07,W/2-.07):
        for z in (-D/2+.05,D/2-.05):cyl("Foot%d%d"%(x*100,z*100),"Rubber",(x,0,z),(x,y0+.004,z),.03,8)
    box("Body","Dark",(0,cy,0),(W,H,D),.035)
    for x in (-W/2+.02,W/2-.02):
        for z in (-D/2+.02,D/2-.02):box("Bumper%d%d"%(x*100,z*100),"Rubber",(x,cy,z),(.05,H+.01,.05),.01)
    for x,m in ((-.145,"StageGlowRose"),(.145,"StageGlowMint")):
        q="%d"%(x*100);wy=cy-.02
        cyl("Surround"+q,"Rubber",(x,wy,fz+.01),(x,wy,fz-.014),wr+.012,16);cyl("Cone"+q,"Metal",(x,wy,fz+.01),(x,wy,fz-.008),wr-.004,16,r2=.045)
        ico("Dust"+q,"Gold",(x,wy,fz-.008),.036,1,s=(1,1,.55));ring("Glow"+q,m,(x,wy,fz-.008),wr+.02,.008,"xy",16,5)
    cyl("TweeterRing","Rubber",(0,cy+.07,fz+.005),(0,cy+.07,fz-.008),.035,10);ico("Tweeter","Gold",(0,cy+.07,fz-.008),.022,1,s=(1,1,.6))
    box("Display","Glass",(0,top-.06,fz-.002),(.14,.04,.01));txt("DisplayText","StageGlowGold","WUB",(0,top-.06,fz-.009),.03,depth=0)
    for x in (-.2,.2):cyl("Post%d"%(x*10),"Metal",(x,top-.005,0),(x,top+.05,0),.018,8)
    tube("Handle","Metal",[(-.2,top+.045,0),(-.2,top+.10,0),(-.15,top+.13,0),(.15,top+.13,0),(.2,top+.10,0),(.2,top+.045,0)],.016,6)
    cyl("Grip","Rubber",(-.13,top+.13,0),(.13,top+.13,0),.026,8);box("Panel","Metal",(0,top+.004,.075),(.30,.01,.08))
    for j,x in enumerate((-.09,0,.09)):cyl("Knob%d"%j,"Gold",(x,top+.008,.075),(x,top+.03,.075),.02,8)
    for j,(x,m) in enumerate(((-.13,"Rose"),(.13,"Mint"))):box("Button%d"%j,m,(x,top+.011,.075),(.025,.012,.03))
    cyl("Port","Rubber",(0,cy,D/2-.005),(0,cy,D/2+.008),.05,12);cyl("PortHole","Dark",(0,cy,D/2+.006),(0,cy,D/2+.01),.038,12)
    EXTRA[P+"PortableSpeaker"]={"carryGrip":"BagR","handAnchor":[0,round(top+.13,3),0],"front":"-Z"}
def bubbles():
    W,H,D,R,n=BK["bubble"];kit(P+"BubbleMachine");y0=.02;fz=-D/2;top=y0+H;wy=.16;wz=fz-.07
    for x in (-W/2+.06,W/2-.06):
        for z in (-D/2+.06,D/2-.06):cyl("Foot%d%d"%(x*100,z*100),"Rubber",(x,0,z),(x,y0+.004,z),.025,8)
    box("Body","Blue",(0,y0+H/2,0),(W,H,D),.03);box("Stripe","Mint",(0,y0+.06,0),(W+.004,.03,D+.004))
    cyl("Fan","Rubber",(0,.25,fz+.005),(0,.25,fz-.012),.085,14);cyl("FanIn","Dark",(0,.25,fz-.012),(0,.25,fz-.014),.07,14);cyl("FanHub","Metal",(0,.25,fz-.012),(0,.25,fz-.02),.015,8)
    for dy in (-.03,0,.03):box("Grille%d"%(dy*100),"Metal",(0,.25+dy,fz-.017),(2*math.sqrt(.07**2-dy*dy),.006,.006))
    cyl("Axle","Metal",(0,wy,fz),(0,wy,wz-.02),.01,8);cyl("Wheel.hub","Mint",(0,wy,wz+.012),(0,wy,wz-.012),.035,10)
    for j in range(n):
        a=math.radians(-90+j*360/n);c=(R*math.cos(a),wy+R*math.sin(a),wz)
        slab("Wheel.spoke%d"%j,"Mint",(0,wy,wz),ad((0,wy,wz),(math.cos(a),math.sin(a),0),R-.028),.012,.008);ring("Wheel.loop%d"%j,"Gold",c,.03,.005,"xy",8,4)
    box("TrayFloor","Mint",(0,.005,fz-.07),(.26,.01,.14));box("TrayFront","Mint",(0,.035,fz-.135),(.26,.07,.01))
    for s in (-1,1):box("TraySide%d"%s,"Mint",(s*.125,.035,fz-.07),(.01,.07,.14))
    box("Solution","Glass",(0,.028,fz-.07),(.24,.036,.125));txt("TrayText","Dark","BLOOP",(0,.036,fz-.142),.04)
    tube("Carry","Mint",[(-.12,top-.005,0),(-.12,top+.05,0),(-.08,top+.08,0),(.08,top+.08,0),(.12,top+.05,0),(.12,top-.005,0)],.016,6)
    cyl("FillCap","Gold",(.1,top-.004,.1),(.1,top+.02,.1),.03,10);box("Switch","Gold",(W/2+.004,.25,.08),(.012,.04,.025))
    for s in (-1,1):
        for q in range(3):box("Vent%d%d"%(s,q),"Dark",(s*(W/2+.002),.16+q*.05,.02),(.006,.018,.14))
    keep("Wheel",(0,wy,wz))
    EXTRA[P+"BubbleMachine"]={"carryGrip":"BagR","handAnchor":[0,round(top+.08,3),0],"front":"-Z","wheelAxis":"Z (spin about local Z at the pivot)"}
def trolley():
    L,Wd,ch,ev,wr,cr=BK["trolley"];kit(P+"VendorTrolley");x0,z0=L/2,Wd/2;dy=.24;px,pz=x0-.08,z0-.03;rg=ev+.15
    box("Deck","Metal",(0,dy-.02,0),(L,.04,Wd));box("Back","PaintMint",(0,(dy+ch)/2,z0-.01),(L,ch-dy,.02))
    for s in (-1,1):box("Side%d"%s,"PaintMint",(s*(x0-.01),(dy+ch)/2,0),(.02,ch-dy,Wd))
    box("Shelf","Wood",(0,.55,0),(L-.04,.025,Wd-.02));box("Counter","Wood",(0,ch+.02,-.02),(L+.06,.04,Wd+.08));box("Kick","Gold",(0,dy+.03,-z0+.01),(L-.04,.06,.02))
    for sx in (-1,1):
        for sz in (-1,1):cyl("Pole%d%d"%(sx,sz),"Metal",(sx*px,ch+.035,sz*pz),(sx*px,ev+.15-.15*pz/.47+.004,sz*pz),.02,8)
    for j in range(8):
        x=-.8+.1+.2*j
        for sz in (-1,1):slab("Canopy%d%d"%(j,sz),("CanvasRose","CanvasCream")[j%2],(x,rg,0),(x,ev,sz*.47),.2,.02)
    for sz in (-1,1):
        for j in range(8):x=-.8+.1+.2*j;loft("Flag%d%d"%(sz,j),("CanvasCream","CanvasRose")[j%2],[[(x-.1,ev-.005,sz*.47+e),(x+.1,ev-.005,sz*.47+e),(x,ev-.13,sz*.47+e)] for e in (-.005,.005)])
    box("Sign","Gold",(0,ev-.2,-pz-.02),(2*px-.02,.2,.03));txt("SignText","Dark","GOODIES",(0,ev-.2,-pz-.037),.13)
    tube("Push","Metal",[(x0,.72,-.25),(x0+.2,.92,-.25),(x0+.2,.92,.25),(x0,.72,.25)],.018,6);cyl("PushGrip","Rubber",(x0+.2,.92,-.18),(x0+.2,.92,.18),.026,8)
    cyl("Axle","Metal",(-.35,wr,-.45),(-.35,wr,.45),.015,8)
    for s,q in ((-1,"L"),(1,"R")):
        z=s*(z0+.05);cyl("Wheel%s.tire"%q,"Rubber",(-.35,wr-.002,z-.03),(-.35,wr-.002,z+.03),wr,16);cyl("Wheel%s.hub"%q,"Metal",(-.35,wr,z-.04),(-.35,wr,z+.04),.07,10)
        cyl("Wheel%s.cap"%q,"Gold",(-.35,wr,z+s*.04),(-.35,wr,z+s*.046),.035,8);keep("Wheel"+q,(-.35,wr,z))
        cz=s*.22;cyl("Stem"+q,"Metal",(.45,dy-.04,cz),(.45,.19,cz),.012,6);box("ForkTop"+q,"Metal",(.45,.19,cz),(.06,.012,.07))
        for e in (-1,1):box("Fork%s%d"%(q,e),"Metal",(.45,.13,cz+e*.025),(.05,.12,.008))
        cyl("CasterAxle"+q,"Metal",(.45,cr,cz-.03),(.45,cr,cz+.03),.008,6);cyl("Caster%s.wheel"%q,"Rubber",(.45,cr-.001,cz-.0175),(.45,cr-.001,cz+.0175),cr,12);keep("Caster"+q,(.45,cr,cz))
    for j,x in enumerate((-.3,.15)):
        box("Crate%d"%j,"Wood",(x,dy+.09,0),(.34,.18,.26),.01)
        for q in range(3):cyl("CrateTin%d%d"%(j,q),"PaintMint",(x-.1+.1*q,dy+.14,.0),(x-.1+.1*q,dy+.24,0),.045,10)
    for j,x in enumerate((-.45,-.3,-.15,0)):cyl("Tin%d"%j,"PaintMint",(x,.5625,-.05),(x,.68,-.05),.05,10);cyl("Lid%d"%j,"Cream",(x,.68,-.05),(x,.7,-.05),.052,10)
    cyl("Jar","Glass",(.3,.5625,-.05),(.3,.73,-.05),.07,10)
    for j in range(5):a=j*1.26;cyl("JarStick%d"%j,HD["glow_mats"][j%3],(.3+.03*math.cos(a),.58,-.05+.03*math.sin(a)),(.3+.05*math.cos(a),.82,-.05+.05*math.sin(a)),.009,6)
    for j,(x,m,s) in enumerate(((-.45,"Rose",.16),(-.25,"Gold",.2),(-.05,"Mint",.13))):box("Box%d"%j,m,(x,ch+.04+s/2,.0),(.16,s,.16),.008)
    box("CashTin","Metal",(.35,ch+.1,.0),(.26,.12,.18),.01);box("CashLatch","Gold",(.35,ch+.12,-.095),(.05,.04,.012))
    EXTRA[P+"VendorTrolley"]={"pushGrip":"both hands","handAnchor":[round(x0+.2,3),.92,0],"front":"-Z (open shelves face -Z)"}
def mystery():
    W,H,D=BK["box"];kit(P+"MysteryStockBox");ft=.008
    box("Body","PaintGold",(0,(H-ft)/2,0),(W,H-ft,D),.006)
    for s,nm in ((-1,"FlapF"),(1,"FlapB")):
        box(nm+".flap","PaintGold",(0,H-ft/2,s*D/4),(W-.004,ft,D/2-.003),.002);box(nm+".tape","Cream",(0,H+.001,s*.0175),(W,.002,.035))
    for s in (-1,1):box("TapeSide%d"%s,"Cream",(s*(W/2+.001),H-.035,0),(.002,.07,.07))
    box("FlapF.label","White",(.1,H+.001,-D/4),(.16,.002,.1));txt("FlapF.qqq","Rose","???",(.1,H+.003,-D/4+.02),.035,R=I,depth=0)
    for q in range(9):box("FlapF.bar%d"%q,"Dark",(.05+q*.012,H+.0025,-D/4-.025),(.004+.002*(q%2),.001,.03))
    txt("QFront","Rose","?",(0,H*.55,-D/2-.002),.3);txt("QBack","Dark","?",(0,H*.55,D/2+.002),.3,R=BACK)
    txt("QRight","Mint","?",(W/2+.002,H*.45,0),.26,R=rot(90,"y")@FRONT);txt("QLeft","Blue","?",(-W/2-.002,H*.45,0),.26,R=rot(-90,"y")@FRONT)
    txt("Word","Dark","MYSTERY",(0,.06,-D/2-.002),.05)
    keep("FlapF",(0,H,-D/2));keep("FlapB",(0,H,D/2))
    EXTRA[P+"MysteryStockBox"]={"carryGrip":"TinR (two-hand carry)","handAnchor":[round(W/2,3),round(H*.6,3),0],"front":"-Z","flaps":"hinge about local X at the pivot; FlapF opens toward -Z, FlapB toward +Z"}
def mascot():
    k=P+"MascotHead";cy=MS["c"];a,b=MS["r"];t=MS["wall"];fw,fh,fd=MS["form"];p0=math.radians(MS["neck"])
    kit(k,[("HeadForm","Stone",(0,fh/2-.01,.03),(fw,fh,fd),0),("Neck","Stone",(0,-.45,0),(.14,.8,.14),0)])
    E=lambda th,ph,o=0:((a+o)*math.cos(ph)*math.sin(th),cy+(b+o)*math.sin(ph),.03+(a+o)*math.cos(ph)*math.cos(th))
    out=[(a*math.cos(p),cy+b*math.sin(p)) for p in [p0+(math.pi/2-p0)*i/10 for i in range(11)]];p75=math.radians(75)
    inn=[((a-t)*math.cos(p),cy+(b-t)*math.sin(p)) for p in [p75-(p75-p0)*i/8 for i in range(9)]]
    weld(lathe("Shell","Mint",out+inn+[out[0]],(0,0,.03),16,cap=False,smooth=True))
    ring("Collar","CanvasGold",(0,(out[0][1]+inn[-1][1])/2,.03),(out[0][0]+inn[-1][0])/2,.035,"xz",12,6,smooth=True)
    cyl("Pad","Dark",(0,fh-.015,.03),(0,cy+(b-t)*math.sin(p75)+.02,.03),MS["pad"],10)
    for s,(dx,dy) in ((-1,(.03,.03)),(1,(-.02,-.035))):
        th,ph=s*.38,.52;e=V(E(th,ph,-.02));n=(V(E(th,ph))-V(E(th,ph,-.1))).normalized()
        ico("Eye%d"%s,"White",tuple(e),.12,2,s=(1,1.15,.7),smooth=True);ico("Pupil%d"%s,"Dark",tuple(e+n*.072+V((dx,dy,0))),.05,1,smooth=True)
        slab("Brow%d"%s,"Dark",E(th-.2,ph+.25+.05*(s>0),.0),E(th+.2,ph+.30-.07*(s>0),.0),.03,.03)
        ico("Cheek%d"%s,"Rose",E(s*.66,-.03,-.03),.07,1,s=(1,.7,.55),smooth=True)
    ico("Nose","Rose",E(0,.22,-.035),.075,1,s=(1.1,.9,.9),smooth=True)
    tm=.72;pt=math.radians(-2);pb=lambda th:pt-math.radians(3+17*math.cos(th/tm*math.pi/2))
    cols=[-tm+2*tm*q/10 for q in range(11)]
    loft("Mouth","Dark",[[E(th,pt+(pb(th)-pt)*u/3,.004) for u in range(4)]+[E(th,pb(th),-.014),E(th,pt,-.014)] for th in cols])
    tube("Lips","Rose",[E(th,pt+.012,.012) for th in cols]+[E(th,pb(th)-.012,.012) for th in cols[::-1][1:-1]],.02,5,True,smooth=True)
    for q in range(1,4):
        ph=pt-math.radians(5*q);ts=[th for th in cols if pb(th)<ph-.01]
        if len(ts)>1:tube("MeshH%d"%q,"Metal",[E(th,ph,.008) for th in ts],.0035,4)
    for th in cols[2:-2:2]:tube("MeshV%d"%(th*100),"Metal",[E(th,pt+(pb(th)-pt)*u/3,.008) for u in range(4)],.0035,4)
    for s in (-1,1):box("Tooth%d"%s,"White",E(s*.07,pt-.045,-.005),(.075,.075,.04),.008)
    for q in range(MS["rays"]):
        th=q*2*math.pi/MS["rays"]+math.pi/MS["rays"];ph=math.radians(64);p=V(E(th,ph,-.03));n=(V(E(th,ph))-V(E(th,ph,-.1))).normalized()
        cyl("Ray%d"%q,"CanvasGold",tuple(p),tuple(p+n*.15),.055,6,r2=.02,smooth=True)
    EXTRA[k]={"attach":"origin = FestivalCharacter Head bone head (character source space (0,1.65,0)); parent to the Head bone with no extra offset/scale","front":"+Z (matches FestivalCharacter facing)","fits":"source-space head incl. ears/hair (x +/-0.32, z -0.26..0.31, top 0.69 above the bone); hats hidden while worn","screen":"wearer eye line looks out through the mouth mesh screen at 0.28 above the bone"}
def handle(pre,x,yT,rib,grip="Dark"):
    r,h=PO["handle"];top=yT+.12+PO["peg"]/2+.004
    cyl(pre+".grip",grip,(x,yT-h,0),(x,yT,0),r,8)
    for j in range(3):cyl(pre+".rib%d"%j,rib,(x,yT-.035-j*.028,0),(x,yT-.041-j*.028,0),r+.003,8)
    cyl(pre+".knob","Metal",(x,yT-.003,0),(x,yT+.03,0),r+.004,8,r2=.014)
    yc,ry=(top+yT+.024)/2,(top-yT-.024)/2;ring(pre+".loop","CanvasDark",(x,yc,0),ry,.004,"yz",8,3,sq=.026/ry)
    cyl(pre+".swivel","Metal",(x,yT-h+.002,0),(x,yT-h-.02,0),.012,6);return yT-h-.02
def chain(pre,x,y0,y1):
    n=PO["links"];rt=.004;w=.013;hl=(y0-y1+.004+2*rt*(n-2))/(2*n);p=2*hl-2*rt;c=y0+.002-rt-hl
    for q in range(n):
        ty,by=c-q*p+hl-w,c-q*p-hl+w;pts=[(w*math.cos(u),ty+w*math.sin(u)) for u in (0,math.pi/2,math.pi)]+[(w*math.cos(u),by+w*math.sin(u)) for u in (math.pi,1.5*math.pi,2*math.pi)]
        tube(pre+".link%d"%q,"Metal",[(x+u,v,0) if q%2==0 else (x,v,u) for u,v in pts],rt,3,True)
    return "zy" if (n-1)%2==0 else "xy"
def head_ribbon(pre,x,yH):
    cyl(pre+".swivel","Metal",(x,yH,0),(x,yH-.02,0),.01,6);ico(pre+".weight","Gold",(x,yH-.055,0),.04,1)
    for q,(m,ph) in enumerate((("CanvasRose",0),("CanvasGold",2.2))):
        rs=[]
        for j in range(11):
            t=j/10;c=(x+(q-.5)*.03+.03*math.sin(ph+t*9),yH-.08-.56*t,.03*math.sin(ph+1+t*6));w=(.045-.025*t)/2;a=ph*.3+t*1.2
            u,v=(math.cos(a)*w,0,math.sin(a)*w),(-math.sin(a)*.002,0,math.cos(a)*.002);rs.append([ad(ad(c,u,s1),v,s2) for s1,s2 in ((1,1),(-1,1),(-1,-1),(1,-1))])
        loft(pre+".ribbon%d"%q,m,rs,smooth=True)
def head_glow(pre,x,yH,m="StageGlowRose"):
    r=PO["head"];yc=yH-.022-r;cyl(pre+".cap","Dark",(x,yH,0),(x,yH-.03,0),.028,8)
    weld(lathe(pre+".ball",m,[(r*math.sin(a),-r*math.cos(a)) for a in [q*math.pi/6 for q in range(7)]],(x,yc,0),12,cap=False));ring(pre+".band","Dark",(x,yc,0),r+.001,.007,"xz",12,4)
def head_pixel(pre,x,yH):
    y0,y1=yH-.045,yH-.185;ri=.07*math.cos(math.pi/8)+.003;ms=HD["glow_mats"]
    cyl(pre+".link","Metal",(x,yH,0),(x,yH-.022,0),.018,6);cyl(pre+".top","Dark",(x,y0,0),(x,yH-.016,0),.07,8,r2=.03)
    cyl(pre+".body","Dark",(x,y0,0),(x,y1,0),.07,8);cyl(pre+".bot","Dark",(x,y1,0),(x,y1-.03,0),.07,8,r2=.03)
    for row in range(3):
        for j in range(8):a=(j+.5)*math.pi/4;box(pre+".px%d_%d"%(row,j),ms[(row+j)%3],(x+ri*math.cos(a),y0-.03-row*.04,ri*math.sin(a)),(.032,.032,.008),R=rot(math.degrees(a)-90,"y"))
def head_fire(pre,x,yH,plane):
    ring(pre+".eye","Metal",(x,yH-.01,0),.01,.003,plane,6,3);box(pre+".bar","Metal",(x,yH-.026,0),(.012,.012,.13));yc=yH-.032-.07
    for s in (-1,1):
        slab(pre+".arm%d"%s,"Metal",(x,yH-.026,s*.06),(x,yc,s*.06),.02,.006);cyl(pre+".washer%d"%s,"Metal",(x,yc,s*.05),(x,yc,s*.057),.02,6)
        tube(pre+".char%d"%s,"Dark",[(x+(.022+.034*q/9)*math.cos(q*.95),yc+(.022+.034*q/9)*math.sin(q*.95),s*.051) for q in range(10)],.004,3)
    cyl(pre+".bolt","Metal",(x,yc,-.066),(x,yc,.066),.007,6);cyl(pre+".wick","Gold",(x,yc,-.05),(x,yc,.05),.062,10)
def head_drop(pre,x,yH,m,ang):
    T=M.Translation(A(x,yH,0))@rot(ang,"z");cyl(pre+".cap","Dark",(0,0,0),(0,-.03,0),.026,8,T=T)
    weld(lathe(pre+".drop",m,[(0,-.02),(.04,-.045),(.062,-.095),(.056,-.14),(.03,-.172),(0,-.18)],(0,0,0),12,T=T,cap=False))
def poi(nm,kind):
    kit(P+nm,[("Peg","Wood",(0,PO["top"]+.12,0),(.64,PO["peg"],PO["peg"]),0)]);yT=PO["top"];h=PO["handle"][1];cl=PO["cord"][1];rib=PO["ribs"][nm];piv={}
    for s,x in ((("",0),) if kind=="double" else (("L",-PO["x"]),("R",PO["x"]))):
        yb=handle("Handle"+s,x,yT,rib,"Rubber" if kind=="fire" else "Dark");keep("Handle"+s,(x,yT-h/2,0));piv["Handle"+s]=(x,yT-h/2,0)
        if kind=="double":
            box("Handle.tbar","Metal",(0,yb-.006,0),(.11,.012,.02));dx=.055;yH=yb-.012-math.sqrt(cl*cl-dx*dx)
            for q,(sx,m) in (("A",(-1,"StageGlowGold")),("B",(1,"StageGlowMint"))):
                cyl("Cord%s.cord"%q,"Cream",(sx*.045,yb-.01,0),(sx*.1,yH-.002,0),PO["cord"][0],6);keep("Cord"+q,(sx*.045,yb-.012,0),"Handle")
                head_drop("Head"+q,sx*.1,yH,m,-sx*math.degrees(math.atan2(dx,yb-.012-yH)));keep("Head"+q,(sx*.1,yH,0),"Cord"+q);piv["Cord"+q]=(sx*.045,yb-.012,0);piv["Head"+q]=(sx*.1,yH,0)
            continue
        yH=yb-cl
        if kind=="fire":plane=chain("Cord"+s,x,yb,yH)
        else:cyl("Cord%s.cord"%s,"Cream",(x,yb+.002,0),(x,yH-.002,0),PO["cord"][0],6)
        keep("Cord"+s,(x,yb,0),"Handle"+s);piv["Cord"+s]=(x,yb,0)
        {"ribbon":lambda:head_ribbon("Head"+s,x,yH),"glow":lambda:head_glow("Head"+s,x,yH),"pixel":lambda:head_pixel("Head"+s,x,yH),"fire":lambda:head_fire("Head"+s,x,yH,plane)}[kind]()
        keep("Head"+s,(x,yH,0),"Cord"+s);piv["Head"+s]=(x,yH,0)
    EXTRA[P+nm]={"grip":"RodR" if kind=="double" else "RodL (Handle L) / RodR (Handle R)","handAnchor":"Handle pivots (handle centre)","hierarchy":"Handle > Cord > Head (keep); Cord pivot = cord attach under the handle swivel, Head pivot = cord attach on the head","pivots":{k:[round(v,3) for v in p] for k,p in piv.items()},"orientation":"hanging display: finger loop over a peg, cord/heads hang along -Y"}
def sign(i,nm,word,board,ink,ic,d,kind):
    W,Hh,th,tip=SG["plate"];ox=(i-4)*SG["pitch"];oy=SG["y"];T0=M.Translation(A(ox,oy,0));z0=SG["post"]/2+.006;z1=z0+th;zc=(z0+z1)/2;h2=Hh/2;ch=.03
    box("Post%d"%i,"Wood",(ox,oy,0),(SG["post"],.7,SG["post"]),tag="shell")
    hs=lambda t:min(h2,h2-ch+t) if t<W-tip else h2*(W-t)/tip
    hb=lambda t:h2*min(1,t/tip,(W-t)/tip)
    hf=hb if kind=="both" else hs
    wv=lambda t,o:.03*math.sin(t*11+o)*hf(t)/h2 if kind=="wobble" else 0
    top,bot=lambda t:hf(t)+wv(t,0),lambda t:-hf(t)+wv(t,1.3)
    X=lambda t:d*(-W/2+t)
    def f(x,y,z):
        if kind=="wobble":a=math.radians(7);return (x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a),z)
        if kind=="droop":g=max(0,d*x-.05);return (x,y-.55*g*g,z)
        if kind=="twist":a=math.radians(45)*x/(W/2);return (x,y*math.cos(a)-(z-zc)*math.sin(a),zc+y*math.sin(a)+(z-zc)*math.cos(a))
        return (x,y,z)
    ts=sorted(set([.004 if kind=="both" else 0,ch,tip,W-tip,W-.004]+([W*q/28 for q in range(1,28)] if kind else [])))
    loft(nm+".board",board,[[f(X(t),top(t),z0),f(X(t),top(t),z1),f(X(t),bot(t),z1),f(X(t),bot(t),z0)] for t in ts],T=T0)
    for j,yy in enumerate((-.1,.1)):tube(nm+".clamp%d"%j,"Metal",[(-.0385,yy,z0-.003),(-.0385,yy,-.0385),(.0385,yy,-.0385),(.0385,yy,z0-.003)],.004,4,True,T=T0)
    ti=.27 if kind=="both" else .17;dp,lz,it=(.01,.004,.012) if kind else (.006,.002,.004);icon(nm+".icon",ic,T0@frame(f,(X(ti),0,z1))@BACK,.003-it,it,1.6)
    a0,b0=ti+.1,W-tip-.02;ws=[gw(c,1) for c in word];gap=.018/SG["text"];tot=sum(ws)+gap*(len(ws)-1);sz=min(SG["text"],(b0-a0-.02)/tot);r=-X((a0+b0)/2)-tot*sz/2
    for j,(c,w) in enumerate(zip(word,ws)):
        x=-(r+w*sz/2);bob=.025*math.sin(j*2.1) if kind=="wobble" else 0;spin=9*math.sin(j*1.7+.5) if kind=="wobble" else 0
        txt(nm+".l%d"%j,ink,c,(0,0,0),sz,dp,R=BACK,T=T0@frame(f,(x,bob,z1+lz))@rot(spin,"z"));r+=(w+gap)*sz
    keep(nm,(ox,oy,0));PLATES.append((i,nm,ox,oy))
glowsticks();stickers();camera();tether();ears();ticket()
clipboard("ComplaintClipboard","Wood","White","Rose","Cream","COMPLAINT",(("TOO LOUD",1),("BAD VIBES",1),("OTHER",0)),("Blue","Gold","Dark"),complaint)
clipboard("WorkOrderClipboard","Metal","Cream","Gold","Dark","WORK ORDER",(("LOAD IN",1),("GATE B",1),("SIGNED",0)),("Gold","Rose","Wood"),workorder)
speaker();bubbles();trolley();mystery();mascot()
for nm,kind in zip(POI,("ribbon","glow","pixel","fire","double")):poi(nm,kind)
kit(P+"SignPlates",[])
for i,p in enumerate(SG["plates"]):sign(i,*p)
EXTRA[P+"SignPlates"]={"mountPivot":"FBX origin (0,0,0) = post axis at plate centre height; every plate object shares it","fitsPost":"AH03_WayfindingPost: 0.07 m post, plates 1.4 x 0.36 on the +Z face; parent at (0, 2.25 | 1.818 | 1.386, 0) with identity rotation","front":"+Z (as AH03 plates); arrow tip toward +X or -X per plate","plates":{nm:{"tip":"+X" if d>0 else "-X","hallucination":kind or None} for nm,_,_,_,_,d,kind in SG["plates"]}}
for k in KITS:
    fl=audit(k)[0];assert not fl,(k,fl)
for k,pre,piv,up in BUN:ROOT[k,pre]=bundle(k,pre,piv,ROOT.get((k,up)))
R=C["rev"];pj=lambda n:os.path.join(R,n);short=lambda ks:{k:k[len(P):] for k in ks}
gallery([P+n for n in HAND],pj("gallery_handheld.png"),4,.5,names=short([P+n for n in HAND]))
spin=[o for o in COL[P+"MascotHead"].objects if o.parent is None]
bpy.context.view_layer.update()
for o in spin:o.matrix_world=RZ@o.matrix_world
gallery([P+n for n in BULK],pj("gallery_bulky_wearable.png"),3,.8,names=short([P+n for n in BULK]))
bpy.context.view_layer.update()
for o in spin:o.matrix_world=RZ@o.matrix_world
gallery([P+n for n in POI],pj("gallery_poi.png"),5,.4,names=short([P+n for n in POI]))
xs=[ox for _,_,ox,_ in PLATES];yy=SG["y"]
for nm,a,b,o in (("signs_standard.png",0,5,8.8),("signs_hallucination.png",5,9,7.2)):cx=(xs[a]+xs[b-1])/2;shot(P+"SignPlates",pj(nm),(cx+.8,yy+1.0,8),(cx,yy,0),ortho=o)
shot(P+"ComplaintClipboard",pj("fp_complaint_clipboard.png"),(0,.78,-.62),(0,.02,.03),35)
shot(P+"DisposableCamera",pj("fp_disposable_camera.png"),(-.3,.42,-.88),(0,.1,0),35)
shot(P+"PoiPixel",pj("fp_poi_pixel.png"),(-.2,.75,-.52),(0,.56,0),35)
shot(P+"MascotHead",pj("mascot_front.png"),(.7,.75,1.9),(0,.38,0),35)
SK=P+"SignPlates";DL={nm:RZ@A(ox,oy,0) for _,nm,ox,oy in PLATES}
for i,nm,ox,oy in PLATES:ROOT[SK,nm].location-=DL[nm];PARTS[SK]["Post%d"%i].location-=DL[nm]
finish(KITS,C["fbx"],C["src"],C["man"],extra=EXTRA)
for _,nm,_,_ in PLATES:ROOT[SK,nm].location+=DL[nm]
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(R0,C["src"]))
