CONFIG={"pkg":"ArtSource/ProductionPackage/FirstProductionPackage.blend","out":"Assets/Festival/Art/Resources","src":"ArtSource/PoiParts.blend","man":"ArtSource/poi-parts-manifest.json","rev":"artifacts/poi-parts/poi-parts.png",
"mat":{"AH01_Teal":"Mint","AH01_Rubber":"Rubber","AH01_MintGlow":"StageGlowMint","AH01_Metal":"Metal","AH01_Cream":"Cream","AH01_Coral":"Rose"},
"handle":("AH01_LEDHandle","AH01_LEDGripRib","AH01_LEDGripRib.001","AH01_LEDGripRib.002","AH01_LEDGripRib.003"),
"pommel":(.085,.0375),"anchor":.03,"led":(.085,"StageGlowMint","Metal"),"practice":(.08,"Cream","Rose")}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
G=CONFIG;K=("FestivalPoiHandle","FestivalPoiHeadLED","FestivalPoiHeadPractice");setup(K)
with bpy.data.libraries.load(os.path.join(R0,G["pkg"]),link=False) as (s,d):d.objects=[n for n in s.objects if n in G["handle"]]
src={o.name:o for o in d.objects};an=lambda:[("Anchor","Stone",(0,0,0),(G["anchor"]*12,.004,.004),0)]
kit(K[0],an());main=src[G["handle"][0]];vs=[main.matrix_world@v.co for v in main.data.vertices];c=sum(vs,V())/len(vs)
for n in G["handle"]:
    o=src[n];bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.transform(bm,matrix=RZ@M.Translation(-c)@o.matrix_world,verts=bm.verts[:])
    ob(n.replace("AH01_","").replace(".",""),G["mat"][o.data.materials[0].name.split(".")[0]],bm,smooth=True)
ico("Pommel","Metal",(0,G["pommel"][0],0),G["pommel"][1])
r,g,m=G["led"];kit(K[1],an());ico("Globe",g,(0,0,0),r,2,smooth=True)
lathe("TopCap",m,[(.040,.07),(.042,.085),(.030,.098),(.012,.104)],k=10);lathe("BottomCap",m,[(.012,-.104),(.030,-.098),(.042,-.085),(.040,-.07)],k=10)
tube("Eyelet",m,[(.012*math.cos(i*math.pi/4),.113+.012*math.sin(i*math.pi/4),0) for i in range(8)],.0035,5,True)
r,g,m=G["practice"];kit(K[2],an());ico("Ball",g,(0,0,0),r,2,smooth=True)
lathe("SockKnot",m,[(.030,.055),(.036,.085),(.022,.105),(.028,.125)],k=10,smooth=True)
tube("SockTail",m,[(0,.12,0),(.02,.15,.01),(.05,.17,.02),(.08,.165,.02)],.012,6,smooth=True)
for o in d.objects:me=o.data;ms=list(me.materials);bpy.data.objects.remove(o,do_unlink=True);bpy.data.meshes.remove(me);[bpy.data.materials.remove(m) for m in ms if m.users==0]
gallery(K,G["rev"],cols=3,gap=.12,names={"FestivalPoiHandle":"Handle","FestivalPoiHeadLED":"LED head","FestivalPoiHeadPractice":"Practice head"})
finish(K,G["out"],G["src"],G["man"])
