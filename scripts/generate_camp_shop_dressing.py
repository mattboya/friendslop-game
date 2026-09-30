CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/CampShopDressing.blend","man":"ArtSource/camp-shop-dressing-manifest.json","rev":"artifacts/camp-shop-dressing","eye":1.62,
"shelf":{"hx":1.62,"sw":.06,"hy":(1.06,1.95),"lift":.12,"rest":.004,"low":.26,"bt":.05,"zf":-.47,"zb":.29,"bk":.04,"cap":((.37,2.84),(-.66,2.64)),"hold":((-.65,.75),(-.75,.65)),"hz":-.05,"tag":((-.47,-.39),-.48,.96,.26,.028),"twine":(.35,1.05),"text":(1.86,.82,.21),"stripes":8},
"sign":{"b":(2.25,2.17,2.70,.28,.40),"crest":(1.34,2.93),"glow":(4.4,2.12,.27,.08),"post":(1.65,.12,3.22),"brk":(2.28,2.50),"bulbs":6,"goods":{"top":1.30,"z":-.66,"x":(-1.2,.6),"plank":(-1.64,.44,.475,7,-1.095,-.045),"jars":((-1.037,-.623),(-.427,-.013),(.473,.887)),"jz":(-.43,-.01,1.785)}},
"trail":{"post":(2,.23,.24),"crown":(4.2,.42,.35,3.4),"lamp":(2,3.02,-.26,.20,.25),"sun":(-.22,4.25,.56,.43,12,.40),"moon":(.30,4.28,.46,-.22,.06,.38),"bunt":(3.02,.16,10,-.09)},
"potty":{"half":2.81,"floor":.08,"ceil":3.01,"door":.9,"exit":-2.35,"spawn":-1.5,"lens":17.5,"seat":(.52,.58,1.12,2.18,1.66,.36,.34,.06),"lid":(.55,.55,1.15,2.08,2.18,.36),"tank":(1.24,1.24,2.76),"roll":(2.36,1.12,.42,.30,.5),"mirror":(-.15,1.95,.92,.72),"pipe":(.52,2.62,.06)}}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
C=CONFIG;E=C["eye"];TAU=6.2832;UP=M.Rotation(math.pi/2,4,"X")
KITS=("FestivalCampShelf","FestivalSupplySign","FestivalCounterGoods","FestivalTrailhead","FestivalPottyInterior")
setup(KITS)
def plate(n,m,pts,z0,z1,**k):return loft(n,m,[[(x,y,z) for x,y in pts] for z in (z0,z1)],**k)
def arc(cx,cy,r,a0,a1,n,ry=None):return [(cx+r*math.cos(a),cy+(r if ry is None else ry)*math.sin(a)) for a in (a0+(a1-a0)*i/(n-1) for i in range(n))]
def sup(a,b,cx,cy,n=28,e=.6):return [(cx+a*math.copysign(abs(math.cos(t))**e,math.cos(t)),cy+b*math.copysign(abs(math.sin(t))**e,math.sin(t))) for t in (TAU*i/n for i in range(n))]
def star(cx,cy,R,r,n):return [(cx+(R,r)[i%2]*math.cos(-math.pi/2+i*math.pi/n),cy+(R,r)[i%2]*math.sin(-math.pi/2+i*math.pi/n)) for i in range(2*n)]
def moon(cx,cy,R,dx,dy,r,n=12):
    d=math.hypot(dx,dy);t=math.atan2(dy,dx);x=(d*d+R*R-r*r)/(2*d);h=math.sqrt(R*R-x*x);a=math.acos(x/R);g=math.atan2(h,x-d)
    return arc(cx,cy,R,t+a,t+TAU-a,n)+arc(cx+dx,cy+dy,r,t-g,t+g-TAU,n)[1:-1]
def lbl(n,m,text,c,size=.1,depth=.004,R=None,**kw):
    cu=bpy.data.curves.new(n,"FONT");cu.body=text;cu.size=size;cu.extrude=depth/2;cu.align_x="CENTER";cu.align_y="CENTER";cu.resolution_u=2
    tmp=bpy.data.objects.new(n,cu);bpy.context.scene.collection.objects.link(tmp);bpy.context.view_layer.update()
    me=bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()));bpy.data.objects.remove(tmp);bpy.data.curves.remove(cu)
    bm=bmesh.new();bm.from_mesh(me);bpy.data.meshes.remove(me);bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-5)
    bmesh.ops.transform(bm,matrix=M.Translation(A(*c))@(UP if R is None else R),verts=bm.verts[:]);return ob(n,m,bm,**kw)
def ctx(k,name,h=(0,0,0),s=1):
    b=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=os.path.join(R0,C["out"],name+".fbx"))
    for o in set(bpy.data.objects)-b:
        o["kit"]=k;o["tag"]="shell";o["ctx"]=1
        if o.parent is None:o.matrix_world=M.Translation(RZ@A(*h))@M.Scale(s,4)@o.matrix_world
        if o.type=="MESH":
            for i in range(len(o.data.materials)):o.data.materials[i]=MAT.get(o.name.split("__")[-1].split(".")[0],MAT["Dark"])
def ghost(o):
    del PARTS[o["kit"]][o.name];COL[o["kit"]].objects.unlink(o);bpy.context.scene.collection.objects.link(o);o["tag"]="shell";o["ctx"]=1
def shelf():
    S=C["shelf"];hx,sw=S["hx"],S["sw"];xi=hx-sw;zf,zb,bk,bt,lo=S["zf"],S["zb"],S["bk"],S["bt"],S["low"];(cb,yb),(cf,yf)=S["cap"]
    ss=[h+S["lift"]+S["rest"] for h in S["hy"]];k="FestivalCampShelf";kit(k)
    top=lambda z:yb+(z-cb)*(yf-yb)/(cf-cb)
    for s in (-1,1):
        q=str(s);prism("Side"+q,"Wood",[(zf-.01,0),(cb,0),(cb,yb),(cf,yf),(cf,yf-.1),(cf+.14,yf-.24),(zf-.01,yf-.44)],s*xi,s*hx)
        cyl("Badge"+q,"Gold",(s*(hx-.003),1.72,-.08),(s*(hx+.012),1.72,-.08),.16,14)
        prism("BadgeMoon"+q,"Mint",moon(-.08,1.72,.12,-.06,.03,.1,8),s*(hx+.011),s*(hx+.02))
    W=2*xi+.01
    for i,(y,m) in enumerate(((lo,"Gold"),(ss[0],"Mint"),(ss[1],"Rose"))):
        box("Board"+str(i),"Wood",(0,y-bt/2,(zf+zb)/2),(W,bt,zb-zf),.008)
        box("Fascia"+str(i),m,(0,y-.03,zf-.01),(W-.01,.08,.02))
    box("Kick","Dark",(0,(lo-bt)/2,zf+.03),(W,lo-bt,.02))
    box("Back","Dark",(0,(lo-bt+top(zb))/2,zb+bk/2),(W,top(zb)-lo+bt,bk))
    ty,tw,th=S["text"]
    for y in (ty-th,ty+th):box("Frame"+str(y),"Gold",(0,y,zb-.0075),(2*tw+.03,.03,.015))
    for x in (-tw,tw):box("FrameV"+str(x),"Gold",(x,ty,zb-.0075),(.03,2*th+.03,.015))
    n=S["stripes"];w=(2*hx+.08)/n;off=.015*math.hypot(1,(yf-yb)/(cf-cb))
    for i in range(n):
        x=-hx-.04+w*(i+.5);m=("CanvasRose","CanvasCream")[i%2]
        slab("Stripe"+str(i),m,(x,yb+off,cb+.02),(x,yf+off-.004,cf-.02),w,.03)
        plate("Scallop"+str(i),m,arc(x,yf+.01,w/2,0,-math.pi,7,.13),cf-.035,cf-.005)
    (d0,d1),dz,tw_,thh,ta=S["tag"];dy=(d0,d1);ext=[]
    for r,s in enumerate(ss):
        tt=S["hy"][r]+dy[r]+thh/2+ta/2
        for x in (-S["twine"][1],-S["twine"][0],S["twine"][0],S["twine"][1]):
            q=str(r)+str(x);cyl("Hook"+q,"Gold",(x,s-.04,zf-.005),(x,s-.04,S["hz"]+dz-.015),.012,6)
            cyl("Twine"+q,"Cream",(x,s-.04,S["hz"]+dz-.003),(x,tt+.004,S["hz"]+dz-.003),.006,5)
        for sg in (-1,1):
            hs=sorted(abs(h) for v in S["hold"] for h in v if h*sg>0);a,b=hs[0]-tw_/2-.005,hs[-1]+tw_/2+.005
            ext.append((((a if sg>0 else -b),S["hy"][r]+dy[r]-thh/2-.005,S["hz"]+dz-.035),((b if sg>0 else -a),tt,S["hz"]+dz+.03)))
        ext.append(((-1.33,s+.002,-.40),(1.33,s+.47,.22)))
    y=lo
    box("Carton0","Cream",(-1.0,y+.17,-.02),(.52,.34,.5),.01);box("Tape0","Gold",(-1.0,y+.34,-.02),(.08,.012,.505))
    Tc=M.Translation(A(-.98,0,-.04))@rot(8,"y")
    box("Carton1","Cream",(0,y+.47,0),(.42,.26,.42),.01,T=Tc);box("Tape1","Gold",(0,y+.60,0),(.08,.012,.425),T=Tc)
    box("Cooler","Mint",(.15,y+.19,-.05),(.62,.38,.44),.03);box("CoolerLid","Cream",(.15,y+.41,-.05),(.66,.06,.48),.02)
    for s in (-1,1):tube("CoolerGrip"+str(s),"White",[(.15+s*.31,y+.30,-.17),(.15+s*.36,y+.30,-.14),(.15+s*.36,y+.30,.04),(.15+s*.31,y+.30,.07)],.018,6)
    cyl("Mat0","CanvasRose",(1.1,y+.13,-.33),(1.1,y+.13,.25),.13,10);cyl("Mat1","CanvasMint",(1.12,y+.38,-.30),(1.12,y+.38,.22),.12,10)
    for z in (-.2,.12):cyl("MatStrap"+str(z),"CanvasDark",(1.1,y+.13,z-.02),(1.1,y+.13,z+.02),.135,10)
    return ext
def sign():
    S=C["sign"];bx,y0,y1,zf,zb=S["b"];cs,ct=S["crest"];px,pr,ph=S["post"];gw,gy,gz,gt=S["glow"]
    kit("FestivalSupplySign",[("ShopPost"+str(s),"Wood",(s*px,ph/2,0),(2*pr,ph,2*pr),1) for s in (-1,1)])
    out=[(-bx,y0),(bx,y0),(bx,y1)]+arc(0,y1,cs,0,math.pi,11,ct-y1)+[(-bx,y1)]
    plate("Board","Dark",out,zf,zb);tube("Rim","Gold",[(x,y,zf-.004) for x,y in out],.028,6,True)
    for i,(x,y) in enumerate(arc(0,y1,cs-.12,.25,math.pi-.25,S["bulbs"],ct-y1-.09)+[(s*x,y1-.1) for s in (-1,1) for x in (1.6,1.85,2.1)]):
        cyl("Bulb"+str(i),("StageGlowGold","StageGlowRose")[i%2],(x,y,zf+.005),(x,y,zf-.03),.034,8)
    plate("Tent","Mint",[(-.17,y1+.015),(.17,y1+.015),(0,y1+.175)],zf-.012,zf+.001);plate("TentDoor","Dark",[(-.05,y1+.015),(.05,y1+.015),(0,y1+.11)],zf-.016,zf-.008)
    box("Glow","StageGlowGold",(0,gy,gz),(gw,gt,gt));box("Channel","Dark",(0,gy,gz+gt/2+.045),(gw+.06,gt+.02,.09))
    for s in (-1,1):
        for y in S["brk"]:
            q=str(s)+str(y);box("Bracket"+q,"Metal",(s*px,y,(.10+zf+.006)/2),(.10,.08,zf+.006-.10));cyl("Band"+q,"Metal",(s*px,y-.045,0),(s*px,y+.045,0),pr+.008,10)
def goods():
    G=C["sign"]["goods"];T,Z=G["top"],G["z"];x0,dx=G["x"];pl,pw,ps,pn,pz0,pz1=G["plank"];X=[x0+i*dx for i in range(5)]
    kit("FestivalCounterGoods",[("Plank"+str(i),"Wood",(pl+pw/2+i*ps,T-.05,(pz0+pz1)/2),(pw,.10,pz1-pz0),1) for i in range(pn)])
    x=X[0];cw,ch,cd=.40,.18,.30
    box("CrateFloor","Wood",(x,T+.015,Z),(cw,.03,cd))
    for s in (-1,1):
        box("CratePanel"+str(s),"PaintMint",(x,T+ch/2,Z+s*(cd/2-.01)),(cw,ch,.02))
        for y in (.07,.15):box("CrateSlat"+str(s)+str(y),"Wood",(x+s*(cw/2-.01),T+y,Z),(.02,.06,cd-.04))
    lbl("CrateGlow","Cream","GLOW",(x,T+.095,Z-cd/2-.001),.075,.006)
    for i in range(3):
        for j in range(3):
            q=str(i)+str(j);c=("Mint","Rose","Gold")[(i+j)%3];b=(x+(i-1)*.1,T+.03,Z+(j-1)*.075);t=(x+(i-1)*.15,T+.31+.04*((i*2+j)%3),Z+(j-1)*.11)
            cyl("Stick"+q,c,b,t,.016,6);cyl("Tip"+q,"StageGlow"+c,ad(b,(t[0]-b[0],t[1]-b[1],t[2]-b[2]),.78),ad(b,(t[0]-b[0],t[1]-b[1],t[2]-b[2]),1.06),.021,6)
    y=T
    for i,(m,a,ox) in enumerate((("CanvasRose",0,0),("CanvasMint",6,.012),("CanvasGold",-5,-.01),("CanvasCream",9,.006))):
        Tm=M.Translation(A(X[1]+ox,0,Z))@rot(a,"y");box("Tee"+str(i),m,(0,y+.0325,0),(.34,.065,.27),.015,T=Tm);y+=.062
    cyl("TeePrint","Rose",(0,y-.002,.0),(0,y+.004,0),.065,12,T=Tm);ico("TeeSun","Gold",(0,y+.004,0),.03,1,(1,.3,1),T=Tm)
    tube("TeeCollar","CanvasDark",[(.06*math.cos(t),y+.004,.125-.04*math.sin(t)) for t in (math.pi*i/6 for i in range(7))],.011,5,T=Tm)
    x=X[2]
    lathe("Jar","Glass",[(.10,0),(.13,.03),(.13,.24),(.10,.28),(.095,.30)],(x,T,Z),12)
    cyl("JarBand","Cream",(x,T+.08,Z),(x,T+.18,Z),.134,12);cyl("JarLid","Rose",(x,T+.295,Z),(x,T+.345,Z),.112,12);ico("JarKnob","Gold",(x,T+.37,Z),.036,1)
    box("JarTag","White",(x,T+.13,Z-.134),(.13,.07,.012));lbl("JarTxt","Rose","SNAX",(x,T+.13,Z-.141),.045,.004)
    for i,(ox,oz) in enumerate(((-.19,-.08),(-.16,-.15),(.18,-.12),(.2,-.03))):ico("Puff"+str(i),"Gold",(x+ox,T+.022,Z+oz),.028,1,(1,.8,1))
    x=X[3]
    for i in range(3):
        for j in (-1,1):
            q=str(i)+str(j);bx_,bz=x+(i-1)*.1,Z+j*.05
            lathe("Bottle"+q,"Glass",[(.04,0),(.046,.02),(.046,.17),(.025,.215),(.017,.245)],(bx_,T,bz),8)
            cyl("BCap"+q,"Blue",(bx_,T+.24,bz),(bx_,T+.27,bz),.021,8);cyl("BLabel"+q,"Mint",(bx_,T+.13,bz),(bx_,T+.17,bz),.048,8)
    for j in (-1,1):box("SleeveF"+str(j),"Cream",(x,T+.075,Z+j*.1),(.30,.09,.012));box("SleeveS"+str(j),"Cream",(x+j*.1525,T+.075,Z),(.012,.09,.212))
    box("Divider","Cream",(x,T+.16,Z),(.30,.32,.008));box("Grip","Dark",(x,T+.27,Z),(.09,.035,.01))
    lbl("Aqua","Blue","H2O",(x,T+.075,Z-.107),.05,.004)
    x=X[4]
    cyl("SunCap","Dark",(x-.07,T,Z+.02),(x-.07,T+.04,Z+.02),.03,8);box("SunBody","Gold",(x-.07,T+.14,Z+.02),(.11,.20,.065),.025)
    cyl("SunDecal","Cream",(x-.07,T+.16,Z-.011),(x-.07,T+.16,Z-.016),.035,10)
    box("LotionBody","Mint",(x+.07,T+.085,Z+.04),(.09,.17,.055),.02,R=rot(-12,"y"));cyl("LotionCap","Cream",(x+.07,T+.165,Z+.04),(x+.07,T+.20,Z+.04),.022,8)
    cyl("Tube","Rose",(x-.10,T+.025,Z+.13),(x+.10,T+.025,Z+.13),.025,8);cyl("TubeCap","Dark",(x+.10,T+.025,Z+.13),(x+.13,T+.025,Z+.13),.018,8)
    prism("Card","Cream",[(Z-.20,T),(Z-.12,T),(Z-.16,T+.10)],x-.11,x+.11)
    lbl("CardTxt","Rose","SPF 9000",(x,T+.0507,Z-.1819),.028,.004,R=rot(-21.8,"x")@UP)
    return [((a-.01,T+.001,G["jz"][0]-.01),(b+.01,G["jz"][2]+.01,G["jz"][1]+.01)) for a,b in G["jars"]]
def trail():
    R=C["trail"];px,pw,pd=R["post"];cw,chh,cd,cy=R["crown"];cb,ct=cy-chh/2,cy+chh/2;lx,ly,lz,lg,lh=R["lamp"]
    kit("FestivalTrailhead")
    for s in (-1,1):
        q=str(s);x=s*px;X=s*lx
        box("Footing"+q,"Stone",(x,.09,0),(.42,.18,.42),.03);box("Post"+q,"Wood",(x,(cb+.02)/2,0),(pw,cb+.02,pd),.02)
        for y,m in ((2.30,"PaintMint"),(2.42,"PaintRose"),(.42,"PaintGold")):box("Band"+q+str(y),m,(x,y,0),(pw+.02,.07,pd+.02))
        slab("Knee"+q,"Wood",(s*(px-pw/2+.02),2.70,0),(s*1.40,cb+.02,0),.10,.10)
        box("EndCap"+q,"Dark",(s*(cw/2+.035),cy,0),(.09,chh+.06,cd+.04),.02)
        for y in (cb+.1,ct-.1):cyl("Bolt"+q+str(y),"Dark",(s*1.93,y,-cd/2+.002),(s*1.93,y,-cd/2-.012),.03,8)
        box("Arm"+q,"Dark",(X,ly-lh/2-.065,(lz-.07-pd/2+.01)/2),(.06,.05,-pd/2+.01-lz+.07));slab("Strut"+q,"Dark",(X,2.55,-pd/2+.01),(X,ly-lh/2-.06,lz-.05),.04,.04)
        box("LampBase"+q,"Dark",(X,ly-lh/2-.02,lz),(.26,.04,.26),.01);box("LampGlass"+q,"StageGlowMint",(X,ly,lz),(lg,lh,lg))
        for cx in (-1,1):
            for cz in (-1,1):box("LampRib"+q+str(cx)+str(cz),"Dark",(X+cx*(lg/2+.005),ly,lz+cz*(lg/2+.005)),(.035,lh,.035))
        box("LampCap"+q,"Dark",(X,ly+lh/2+.015,lz),(.28,.03,.28),.01);cyl("LampKnob"+q,"Dark",(X,ly+lh/2+.03,lz),(X,ly+lh/2+.043,lz),.05,8)
    box("Crown","Gold",(0,cy,0),(cw,chh,cd),.03);box("TopCap","Dark",(0,ct+.02,.02),(cw,.05,cd-.04))
    by,bs,bn,bz=R["bunt"];xe=px-pw/2+.01;yc=lambda x:by-bs*(1-(x/xe)**2)
    tube("Rope","Cream",[(x,yc(x),bz) for x in (xe*(-1+2*i/12) for i in range(13))],.009,5)
    for i in range(bn):
        xc=-1.62+i*3.24/(bn-1);plate("Flag"+str(i),("CanvasRose","CanvasMint","CanvasGold")[i%3],[(xc-.11,yc(xc-.11)+.004),(xc+.11,yc(xc+.11)+.004),(xc,yc(xc)-.24)],bz-.006,bz+.006)
    sx,sy,sR,sr,sn,sd=R["sun"];mx,my,mR,mdx,mdy,mr=R["moon"]
    box("Stand","Dark",(sx,ct+.09,0),(.36,.12,.16));plate("SunRays","Gold",star(sx,sy,sR,sr,sn),-.05,.05)
    plate("SunDisc","StageGlowGold",arc(sx,sy,sd,0,TAU,21)[:-1],-.075,-.045)
    for e in (-1,1):cyl("SunEye"+str(e),"Dark",(sx+e*.12,sy+.09,-.07),(sx+e*.12,sy+.09,-.088),.045,8)
    tube("SunSmile","Dark",[(sx+.17*math.cos(t),sy-.02+.13*math.sin(t),-.078) for t in (-2.6+2.06*i/6 for i in range(7))],.02,5)
    plate("Moon","Mint",moon(mx,my,mR,mdx,mdy,mr,12),-.13,-.07)
    tube("MoonEye","Dark",[(mx+.34+.04*math.cos(t),my+.11+.03*math.sin(t),-.134) for t in (math.pi*i/5 for i in range(6))],.013,5)
    tube("MoonSmile","Dark",[(mx+.30+.08*math.cos(t),my-.06+.06*math.sin(t),-.134) for t in (math.pi+math.pi*i/5 for i in range(6))],.015,5)
    cyl("MoonCheek","Rose",(mx+.40,my+.01,-.13),(mx+.40,my+.01,-.137),.035,8)
def potty():
    P=C["potty"];H,FL,CE=P["half"],P["floor"],P["ceil"];k="FestivalPottyInterior"
    kit(k,[(n,"Wood" if n=="ShellFloor" else "Blue",c,s,cut) for n,c,s,cut in (("ShellFloor",(0,.02,0),(5.8,.12,5.8),0),("ShellRear",(0,1.55,2.9),(5.8,3.1,.18),0),("ShellFrontL",(-1.9,1.55,-2.9),(2,3.1,.18),1),("ShellFrontR",(1.9,1.55,-2.9),(2,3.1,.18),1),("ShellHeader",(0,2.83,-2.9),(1.8,.55,.18),1),("ShellLeft",(-2.9,1.55,0),(.18,3.1,5.8),1),("ShellRight",(2.9,1.55,0),(.18,3.1,5.8),0),("ShellCeiling",(0,3.1,0),(5.8,.18,5.8),1))])
    box("Panel","Blue",(0,1.53,H-.025),(4.95,2.61,.05))
    for x in (-2.1,-1.5,1.5,2.1):box("PanelRib"+str(x),"Blue",(x,1.53,H-.065),(.10,2.41,.03),.01)
    box("LipTop","White",(0,2.74,H-.065),(4.85,.08,.03),.01)
    for s in (-1,1):
        box("LipLow"+str(s),"White",(s*1.55,.32,H-.065),(1.75,.08,.03),.01)
        for z in (-2.0,-1.0,2.45):box("SideRib"+str(s)+str(z),"Blue",(s*(H-.015),1.55,z),(.03,2.7,.10),.01,cut=s<0)
    sx,sy,sz0,sz1,scz,sa,sb,sr=P["seat"];la,lb,lc,lz0,lz1,lup=P["lid"];tw,tt,tz=P["tank"];pv=(0,lc,(lz0+lz1)/2)
    loft("Pedestal","Cream",[[(x,y,z) for x,z in ((-w,za),(w,za),(w,sz1),(-w,sz1))] for w,y,za in ((.44,FL-.005,1.26),(sx,sy-.08,sz0),(sx-.02,sy,sz0+.03))])
    tube("SeatRing","Cream",[(sa*math.cos(t),sy+.04,scz+sb*math.sin(t)) for t in (TAU*i/16 for i in range(16))],sr,6,True)
    cyl("Abyss","Dark",(0,sy-.005,scz),(0,sy+.005,scz),.30,12)
    for s in (-1,1):box("Hinge"+str(s),"Metal",(s*.30,sy+.02,(lz0+lz1)/2),(.14,.08,.12),.01)
    sp=sup(la,lb,0,lc);rg=lambda z,f:[(x*f,lc+(y-lc)*f,z) for x,y in sp]
    lid=loft("PottyLid__Cream","Cream",[rg(lz0,.92),rg(lz0+.02,1),rg(lz1,1)],piv=pv,tag="keep")
    st=cyl("PottyLidSticker__Gold","Gold",(0,lc+.12,lz0+.001),(0,lc+.12,lz0-.008),.17,14,piv=pv,tag="keep");st.parent=lid;st.location=(0,0,0)
    bm=bmesh.new();fz=lz0-.012
    for e in (-1,1):bmesh.ops.create_icosphere(bm,subdivisions=1,radius=.034,matrix=M.Translation(A(e*.065,lc+.16,fz))@M.Diagonal(A(1,1.25,.35)).to_4x4())
    for sx_,sy_,w_,a_ in ((0,lc+.05,.10,0),(-.068,lc+.072,.05,40),(.068,lc+.072,.05,-40)):bmesh.ops.create_cube(bm,size=1,matrix=M.Translation(A(sx_,sy_,fz))@rot(a_,"z")@M.Diagonal(A(w_,.022,.014)).to_4x4())
    fc=ob("PottyLidFace__Dark","Dark",bm,piv=pv,tag="keep");fc.parent=lid;fc.location=(0,0,0)
    box("Tank","White",(0,(FL+tt)/2,(lz1+tz)/2),(tw,tt-FL,tz-lz1),.05)
    px_,pz_,pr_=P["pipe"]
    cyl("VentPipe","Dark",(px_,tt-.01,pz_),(px_,CE+.005,pz_),pr_,8);cyl("PipeFlange","Metal",(px_,CE-.03,pz_),(px_,CE+.002,pz_),.10,8)
    cyl("PipeBand","Metal",(px_,2.17,pz_),(px_,2.23,pz_),pr_+.008,8);box("PipeClamp","Metal",(px_,2.2,(pz_+H-.05)/2),(.05,.05,H-.05-pz_))
    mx_,my_,mw_,mh_=P["mirror"]
    box("MirrorFrame","Rose",(mx_,my_,H-.075),(mw_,mh_,.05),.02);box("Mirror","Glass",(mx_,my_,H-.103),(mw_-.12,mh_-.12,.012))
    lbl("MirrorTxt","White","LOOKIN GOOD",(mx_,my_+mh_/2-.1,H-.109),.05,.004)
    box("SignPlate","Dark",(-.1,2.52,H-.065),(1.0,.34,.03),.01);box("SignFace","Rose",(-.1,2.52,H-.084),(.92,.25,.008))
    lbl("SignTxt","White","OCCUPIED",(-.1,2.52,H-.09),.14,.008)
    rx,ry,rz,rr,rl=P["roll"]
    box("RollPlate","Metal",(H-.02,ry,rz),(.04,.16,rl+.2))
    for s in (-1,1):box("RollArm"+str(s),"Metal",((rx+H-.04)/2,ry,rz+s*(rl/2+.035)),(H-.04-rx+.05,.06,.03))
    cyl("Spindle","Gold",(rx,ry,rz-rl/2-.07),(rx,ry,rz+rl/2+.07),.045,8);cyl("Roll","Cream",(rx,ry,rz-rl/2),(rx,ry,rz+rl/2),rr,16)
    cyl("RollCore","Rubber",(rx,ry,rz-rl/2-.006),(rx,ry,rz+rl/2+.006),.09,10)
    box("RollTail","Cream",(rx-rr+.004,(ry+FL+.02)/2,rz),(.008,ry-FL-.02,rl-.04));box("RollPool","Cream",(rx-rr-.18,FL+.012,rz),(.40,.024,rl-.04),.005)
    box("Sanitizer","Mint",(H-.12,1.27,1.12),(.24,.38,.26),.03);box("SanWindow","Dark",(H-.242,1.34,1.12),(.01,.12,.16))
    box("SanGel","Glass",(H-.246,1.31,1.12),(.006,.06,.14));box("SanPaddle","Gold",(H-.255,1.15,1.12),(.03,.10,.20),.01)
    cyl("SanNozzle","White",(H-.19,1.085,1.12),(H-.19,1.03,1.12),.022,8);ico("SanDrip","Mint",(H-.19,1.02,1.12),.018,1,(1,1.3,1))
    box("VentFrame","Metal",(H-.015,2.55,1.9),(.03,.44,.62),.01)
    for i in range(4):box("VentSlat"+str(i),"Dark",(H-.035,2.40+i*.1,1.9),(.02,.035,.52))
    cyl("StickerSun","Gold",(H+.001,1.95,-.75),(H-.006,1.95,-.75),.16,12)
    prism("StickerMoon","Mint",moon(-.32,2.02,.13,-.06,.03,.11,8),H-.007,H+.001)
    lbl("Graffiti","Rose","HI MOM",(H-.004,1.58,-.6),.13,.004,R=rot(-90,"y")@UP)
    box("HookPlate","Gold",(-H+.015,1.81,.36),(.03,.2,.12),.01,cut=True)
    tube("Hook","Gold",[(-H+.02,1.80,.36),(-H+.14,1.76,.36),(-H+.20,1.80,.36),(-H+.21,1.88,.36)],.022,6,cut=True)
    tube("BagStrap","CanvasDark",[(-H+.10,1.45,.22),(-H+.14,1.755,.36),(-H+.10,1.45,.50)],.012,5,cut=True)
    puff("Bag","CanvasRose",(0,0,0),(.24,.14,.36),T=M.Translation(A(-H+.10,1.34,.36))@rot(90,"z"),cut=True)
    box("Trough","White",(-H+.17,.72,1.55),(.34,.30,1.2),.04,cut=True);box("TroughWell","Dark",(-H+.16,.86,1.55),(.24,.03,1.08),cut=True)
    ico("UrinalCake","Mint",(-H+.16,.88,1.55),.06,1,(1,.35,1),cut=True);box("Splash","White",(-H+.012,1.25,1.55),(.024,.80,1.2),cut=True)
    cyl("TroughDrain","Metal",(-H+.17,.58,1.55),(-H+.17,FL,1.55),.035,8,cut=True)
    lbl("TroughTxt","Dark","AIM HIGH",(-H+.026,1.45,1.55),.09,.004,R=rot(90,"y")@UP,cut=True)
    box("FloorMat","Rubber",(0,FL+.01,.62),(1.5,.02,1.0),.005);cyl("FloorDrain","Metal",(0,FL-.002,-.35),(0,FL+.012,-.35),.12,10)
    box("PumpBase","Dark",(.85,FL+.04,1.45),(.26,.08,.36),.02);slab("Pedal","Gold",(.85,FL+.07,1.30),(.85,FL+.15,1.60),.20,.035)
    lathe("PlungerCup","Rose",[(.15,0),(.145,.05),(.10,.12),(.045,.15)],(-1.05,FL,2.30),10);cyl("PlungerStick","Wood",(-1.05,FL+.13,2.30),(-1.05,1.22,2.735),.028,6);ico("PlungerKnob","Dark",(-1.05,1.25,2.715),.045,1)
    for i,(x,y) in enumerate(((2.25,FL),(2.51,FL),(2.38,FL+.24))):cyl("Spare"+str(i),"Cream",(x,y,2.45),(x,y+.24,2.45),.13,12);cyl("SpareCore"+str(i),"Rubber",(x,y+.24,2.45),(x,y+.245,2.45),.045,8)
    dx_,dy_,dz_=2.38,FL+.48,2.45
    ico("DuckBody","Gold",(dx_,dy_+.07,dz_),.10,1,(1.2,.75,1));ico("DuckHead","Gold",(dx_,dy_+.19,dz_-.04),.065,1)
    cyl("DuckBeak","Rose",(dx_,dy_+.18,dz_-.09),(dx_,dy_+.175,dz_-.145),.03,6,.012)
    for e in (-1,1):ico("DuckEye"+str(e),"Dark",(dx_+e*.03,dy_+.21,dz_-.09),.012,1)
    tube("GrabBar","Gold",[(1.25,.95,-H-.005),(1.25,.95,-H+.09),(1.25,1.45,-H+.09),(1.25,1.45,-H-.005)],.022,6,cut=True)
    box("Skylight","White",(0,CE-.015,1.3),(2.0,.03,1.4),cut=True)
    for x in (-.5,0,.5):box("SkyRib"+str(x),"Metal",(x,CE-.04,1.3),(.04,.03,1.4),cut=True)
    box("ExitPlate","Dark",(0,2.75,-H+.015),(.64,.22,.03),.01,cut=True)
    lbl("ExitTxt","StageGlowMint","EXIT",(0,2.75,-H+.032),.13,.006,R=rot(180,"y")@UP,cut=True)
    return lid,[((-P["door"],.14,-H),(P["door"],2.4,P["exit"])),((-la-.01,lc+lb+.005,lz0-.02),(la+.01,lc+lb+lup+.02,lz1+.01))]
clear={"FestivalCampShelf":shelf()};sign();clear["FestivalCounterGoods"]=goods();trail();lid,clear["FestivalPottyInterior"]=potty()
S=C["shelf"];R_=C["rev"];sh=lambda k,n,*a:shot(k,os.path.join(R_,k+"_"+n+".png"),*a)
for r,row in enumerate(((("FestivalStock",4),("FestivalStash",1)),(("FestivalPoi",4),("FestivalMerchBag",4)))):
    for (res,cnt),h in zip(row,S["hold"][0]):
        for n in range(cnt):ctx("FestivalCampShelf",res,(h+(n-(cnt-1)*.5)*.31,S["hy"][r]+S["lift"],S["hz"]),.43 if cnt==1 else .29)
        KIT[0]="FestivalCampShelf";y=S["hy"][r]+S["tag"][0][r];z=S["hz"]+S["tag"][1]
        ghost(box("CtxTag"+str(r)+str(h),"Dark",(h,y,z),(.96,.26,.05)));ghost(box("CtxAccent"+str(r)+str(h),"Gold",(h,y+.13,z-.003),(.96,.028,.06)))
for k in ("FestivalSupplySign","FestivalCounterGoods"):ctx(k,"FestivalCampShop")
VIEWS={"FestivalCampShelf":(("front",(0,E,-3.3),(0,1.45,0),24),("quarter",(-2.9,2.2,-3.3),(0,1.45,0),24),("side",(3.0,1.5,-1.0),(0,1.4,-.25),26)),
"FestivalSupplySign":(("front",(0,E,-4.6),(0,2.25,0),26,True),("bracket",(2.9,2.25,-1.3),(1.5,2.55,.3),30,True),("back",(1.0,2.2,2.4),(1.45,2.62,.3),30,True)),
"FestivalCounterGoods":(("player",(0,E,-2.9),(0,1.35,-.6),24,True),("close",(-.4,1.85,-1.7),(-.1,1.38,-.66),30,True),("closeR",(1.0,1.8,-1.6),(.9,1.38,-.66),30,True)),
"FestivalTrailhead":(("approach",(0,E,-7.5),(0,2.6,0),26),("quarter",(-4.2,2.2,-5.2),(0,2.5,0),28),("emblem",(1.3,3.5,-2.3),(0,4.05,0),35)),
"FestivalPottyInterior":(("player",(0,E,C["potty"]["spawn"]),(0,1.25,2.2),C["potty"]["lens"]),("toilet",(.95,1.5,.45),(0,1.0,2.2),24),("walls",(-1.4,E,-.4),(2.6,1.3,.9),22),("rear",(0,E,1.0),(0,1.4,-2.8),C["potty"]["lens"]),("cutaway",(-5.2,5.4,-5.6),(0,.7,.4),22,True))}
for k in KITS:
    for n,e,a,*o in VIEWS[k]:sh(k,n,e,a,*o)
lid.location.z+=C["potty"]["lid"][5];sh("FestivalPottyInterior","lidup",(.95,1.5,.45),(0,1.1,2.2),24);lid.location.z-=C["potty"]["lid"][5]
for o in [o for o in bpy.data.objects if o.get("ctx")]:bpy.data.objects.remove(o,do_unlink=True)
gallery(KITS,os.path.join(R_,"gallery.png"),3);gallery(KITS[:4],os.path.join(R_,"gallery_props.png"),4)
L=C["potty"]["lid"];ss=[round(h+S["lift"]+S["rest"],3) for h in S["hy"]]
finish(KITS,C["out"],C["src"],C["man"],clear=clear,extra={"FestivalCampShelf":{"origin":"(+-4.65,0,9.05)","surfaces":{"row0":ss[0],"row1":ss[1],"low":S["low"]},"textBackingZ":S["zb"]},
"FestivalSupplySign":{"origin":"(0,0,9.15)","boardFrontZ":C["sign"]["b"][3],"glow":"StageGlowGold strip y2.12 z.27 local"},"FestivalCounterGoods":{"origin":"(0,0,9.15)","restsOn":"FestivalCampShop countertop y1.30"},
"FestivalTrailhead":{"origin":"(0,0,19)","crownFrontZ":-C["trail"]["crown"][2]/2,"lanterns":"(+-2,3.02,-.26) local"},"FestivalPottyInterior":{"lidPivot":[0,L[2],(L[3]+L[4])/2],"lidTravel":L[5]}})
