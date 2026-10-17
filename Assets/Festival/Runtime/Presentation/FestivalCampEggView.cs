using System.Collections.Generic;
using Festival.Core;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>The eight camp secrets, made from the trip gnome, a sleeping camper and simple shapes, each standing at the host's
    /// point (Show moves them to the points a visit publishes). Nothing collides, so the camp navmesh and its routes stay as they were.
    /// The aim dot picks a secret (Aimed) only where the host would accept a poke.</summary>
    public sealed class FestivalCampEggView : MonoBehaviour
    {
        public const string Name="Camp secrets";
        // The host accepts a poke within 2.5 m of a secret's point (CampEggs.ActivateCampEgg).
        public const float Reach=2.5f;
        public static string PropName(string id)=>"Camp egg "+id;
        // Where the aim dot finds each secret, from its point, and how near the aim line must pass. The button sits at the free end of
        // the DJ table and the dust on car_1's back window, so a look at the deck or the car door misses them.
        private static readonly Dictionary<string,(Vector3 At,float Radius,string Prompt)> Targets=new Dictionary<string,(Vector3,float,string)>{
            {"gnome",(new Vector3(0,.24f,0),.35f,"E  POKE THE GNOME")},
            {"button",(new Vector3(.55f,.88f,-.02f),.25f,"E  PRESS THE BIG RED BUTTON")},
            {"chair",(new Vector3(0,.55f,0),.6f,"E  SIT IN THE HEADLINER'S CHAIR")},
            {"lost_found",(new Vector3(0,.4f,0),.55f,"E  RUMMAGE IN LOST AND FOUND")},
            {"hammock",(new Vector3(0,.7f,0),.9f,"E  POKE THE HAMMOCK SLEEPER")},
            {"flamingo",(new Vector3(0,.6f,0),.7f,"E  SQUEEZE THE FLAMINGO")},
            {"cone",(new Vector3(0,.32f,0),.5f,"E  PAY RESPECTS TO CONE")},
            {"wash_me",(new Vector3(0,1.52f,-1.59f),.55f,"E  WRITE IN THE DUST")},
        };
        private readonly Dictionary<string,Transform> props=new Dictionary<string,Transform>();

        /// <summary>The secrets under camp, each at its fixed point and the gnome at the first visit's spot until Show says otherwise.</summary>
        public static FestivalCampEggView Build(Transform camp)
        {
            var root=new GameObject(Name);root.transform.SetParent(camp,false);
            var view=root.AddComponent<FestivalCampEggView>();
            foreach(var egg in CampFeatures.Eggs)
            {
                var prop=new GameObject(PropName(egg.Id)).transform;prop.SetParent(root.transform,false);
                var point=CampFeatures.EggPoint(egg,0);prop.localPosition=new Vector3(point.X,0,point.Z);
                Dress(prop,egg.Id);view.props[egg.Id]=prop;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("CampEggs","props_built","count="+view.props.Count);
#endif
            return view;
        }
        public Transform Prop(string id)=>props.TryGetValue(id,out var prop)?prop:null;

        /// <summary>Stand each prop at the point the view's round publishes for it.</summary>
        public void Show(IReadOnlyList<CampEggState> eggs)
        {
            if(eggs==null)return;
            foreach(var egg in eggs)
            {
                if(egg==null||!props.TryGetValue(egg.Id,out var prop))continue;
                var at=new Vector3(egg.X,0,egg.Z);if(prop.localPosition==at)continue;
                prop.localPosition=at;if(egg.Id=="gnome")FaceCamp(prop);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DevelopmentDiagnostics.GraphicsEvent("CampEggs","prop_moved","egg="+egg.Id+" x="+egg.X+" z="+egg.Z);
#endif
            }
        }

        public static Vector3 AimPoint(CampEggState egg)=>new Vector3(egg.X,0,egg.Z)+(Targets.TryGetValue(egg.Id,out var target)?target.At:Vector3.zero);
        public static float AimRadius(string id)=>Targets.TryGetValue(id,out var target)?target.Radius:0;
        public static string Prompt(string id)=>Targets.TryGetValue(id,out var target)?target.Prompt:"";

        /// <summary>The secret whose prop the aim line from origin passes nearest, within its radius, if player may poke it from where they
        /// stand: the host's own phase, eligibility and reach, and a headliner's chair nobody else has. "" otherwise.</summary>
        public static string Aimed(RoundState state,PlayerState player,Vector3 origin,Vector3 forward)
        {
            if(state?.CampEggs==null||player==null||state.Phase!="Shopping")return "";
            if(player.Life!="Alive"||player.Ready||player.InteractionId!=""||player.DragTargetId!=""||player.CarryBodyId!=""||player.HeldOfferId!=""||player.CampVisitId!="")return "";
            if(FestivalSimulation.CampSeated(state,player.Id))return "";
            string best="";float nearest=float.MaxValue;
            foreach(var egg in state.CampEggs)
            {
                if(egg==null||!Targets.TryGetValue(egg.Id,out var target))continue;
                if(Vector2.Distance(new Vector2(player.X,player.Z),new Vector2(egg.X,egg.Z))>Reach)continue;
                if(egg.Id=="chair"&&FestivalSimulation.CampSeated(state,egg.SeatedPlayerId))continue;
                var delta=AimPoint(egg)-origin;float along=Vector3.Dot(delta,forward);if(along<=0)continue;
                float miss=(delta-forward*along).magnitude;
                if(miss<=target.Radius&&miss<nearest){nearest=miss;best=egg.Id;}
            }
            return best;
        }

        private static void Dress(Transform prop,string id)
        {
            switch(id)
            {
                case "gnome":
                    var gnome=FestivalArtView.Create(prop,"FestivalCreatureGnome",true);
                    if(gnome!=null)foreach(var r in gnome.GetComponentsInChildren<Renderer>(true))
                    {
                        string paint=r.name.Substring(r.name.LastIndexOf("__",System.StringComparison.Ordinal)+2);
                        r.sharedMaterial=FestivalArtView.MaterialFor(paint=="P0"?"Blue":paint=="P1"||paint=="P10"?"Rose":paint=="P3"?"White":paint=="P8"?"Dark":"Cream");
                    }
                    FaceCamp(prop);break;
                case "button":
                    // On the DJ table's top (.84 m), past the decks and the sign post at its east end.
                    Part(prop,"Button plinth",PrimitiveType.Cube,new Vector3(.55f,.86f,-.02f),new Vector3(.2f,.04f,.2f),"Dark");
                    Part(prop,"Big red button",PrimitiveType.Cylinder,new Vector3(.55f,.9f,-.02f),new Vector3(.13f,.025f,.13f),"StageGlowRed");
                    Label(prop,"DO NOT PRESS",new Vector3(.55f,.845f,.12f),new Vector3(90,180,0),.012f);break;
                case "chair":
                    // A camp chair facing south, the way the host turns its headliner.
                    Part(prop,"Chair seat",PrimitiveType.Cube,new Vector3(0,.42f,0),new Vector3(.52f,.05f,.48f),"CanvasRose");
                    Part(prop,"Chair back",PrimitiveType.Cube,new Vector3(0,.7f,.24f),new Vector3(.52f,.52f,.04f),"CanvasRose");
                    foreach(float x in new[]{-.25f,.25f})
                    {
                        Part(prop,"Chair arm",PrimitiveType.Cube,new Vector3(x,.6f,0),new Vector3(.05f,.04f,.48f),"Metal");
                        foreach(float z in new[]{-.21f,.21f})Part(prop,"Chair leg",PrimitiveType.Cylinder,new Vector3(x,.3f,z),new Vector3(.03f,.3f,.03f),"Metal");
                    }
                    Part(prop,"Reserved board",PrimitiveType.Cube,new Vector3(0,1.035f,.26f),new Vector3(.62f,.15f,.02f),"Cream");
                    Label(prop,"RESERVED: HEADLINER",new Vector3(0,1.035f,.245f),Vector3.zero,.016f,dark:true);
                    Label(prop,"RESERVED: HEADLINER",new Vector3(0,1.035f,.275f),new Vector3(0,180,0),.016f,dark:true);break;
                case "lost_found":
                    Part(prop,"Lost and found box",PrimitiveType.Cube,new Vector3(0,.31f,0),new Vector3(.8f,.62f,.6f),"Wood");
                    Part(prop,"Box flap",PrimitiveType.Cube,new Vector3(0,.68f,-.36f),new Vector3(.78f,.02f,.3f),"Cream").localRotation=Quaternion.Euler(55,0,0);
                    Part(prop,"Left sandal",PrimitiveType.Cube,new Vector3(-.15f,.66f,-.05f),new Vector3(.11f,.03f,.27f),"Rose").localRotation=Quaternion.Euler(30,20,0);
                    Part(prop,"Spiteful glow stick",PrimitiveType.Cylinder,new Vector3(.18f,.68f,.05f),new Vector3(.025f,.13f,.025f),"StageGlowMint").localRotation=Quaternion.Euler(0,0,25);
                    Label(prop,"LOST + FOUND",new Vector3(0,.36f,.305f),new Vector3(0,180,0),.022f);
                    FaceCamp(prop);break;
                case "hammock":
                    foreach(float x in new[]{-1.3f,1.3f})
                    {
                        Part(prop,"Hammock post",PrimitiveType.Cylinder,new Vector3(x,.6f,0),new Vector3(.09f,.6f,.09f),"Wood");
                        var rope=new Vector3(x*.77f,.5f,0)-new Vector3(x,1.1f,0);
                        Part(prop,"Hammock rope",PrimitiveType.Cylinder,new Vector3(x*.885f,.8f,0),new Vector3(.02f,rope.magnitude*.5f,.02f),"Cream").localRotation=Quaternion.FromToRotation(Vector3.up,rope);
                    }
                    Part(prop,"Hammock sling",PrimitiveType.Cube,new Vector3(0,.48f,0),new Vector3(2,.05f,.75f),"CanvasMint");
                    var sleeper=FestivalCharacter.Create(prop,"camp_hammock_sleeper",Color.white);
                    sleeper.Pose=FestivalSimulation.LieDownKind;
                    sleeper.transform.localPosition=new Vector3(-.1f,.5f,0);sleeper.transform.localRotation=Quaternion.Euler(0,90,0);
                    sleeper.transform.localScale=Vector3.Scale(Vector3.one*.85f,sleeper.ShapeScale);break;
                case "flamingo":
                    // One holder, so deflation can squash every air part at once.
                    var air=new GameObject("Flamingo air").transform;air.SetParent(prop,false);
                    Part(air,"Float ring",PrimitiveType.Sphere,new Vector3(0,.1f,0),new Vector3(.95f,.16f,.75f),"Rose");
                    Part(air,"Flamingo body",PrimitiveType.Sphere,new Vector3(0,.42f,-.05f),new Vector3(.7f,.42f,.48f),"Rose");
                    Part(air,"Flamingo neck",PrimitiveType.Cylinder,new Vector3(0,.82f,.2f),new Vector3(.1f,.28f,.1f),"Rose").localRotation=Quaternion.Euler(15,0,0);
                    Part(air,"Flamingo head",PrimitiveType.Sphere,new Vector3(0,1.12f,.28f),new Vector3(.2f,.18f,.22f),"Rose");
                    Part(air,"Flamingo beak",PrimitiveType.Cube,new Vector3(0,1.08f,.43f),new Vector3(.06f,.06f,.16f),"Dark").localRotation=Quaternion.Euler(30,0,0);
                    FaceCamp(prop);break;
                case "cone":
                    Part(prop,"Cone base",PrimitiveType.Cube,new Vector3(0,.015f,0),new Vector3(.38f,.03f,.38f),"Dark");
                    float[] widths={.3f,.24f,.18f,.12f};
                    for(int i=0;i<widths.Length;i++)Part(prop,"Cone",PrimitiveType.Cylinder,new Vector3(0,.12f+i*.15f,0),new Vector3(widths[i],.075f,widths[i]),"Gold");
                    Part(prop,"Cone stripe",PrimitiveType.Cylinder,new Vector3(0,.35f,0),new Vector3(.23f,.025f,.23f),"White");
                    for(int i=0;i<6;i++)
                    {
                        var round=Quaternion.Euler(0,60*i,0)*Vector3.forward*.42f;
                        Part(prop,"Candle",PrimitiveType.Cylinder,round+new Vector3(0,.07f,0),new Vector3(.05f,.07f,.05f),"Cream");
                        Part(prop,"Candle flame",PrimitiveType.Sphere,round+new Vector3(0,.17f,0),new Vector3(.035f,.05f,.035f),"StageGlowGold");
                    }
                    FaceCamp(prop);break;
                case "wash_me":
                    // A dust film on car_1's back window: the glass rises from about 1.4 m, leaning 11 degrees in, about 1.6 m south of the point.
                    var dust=new GameObject("Dust window").transform;dust.SetParent(prop,false);
                    dust.localPosition=new Vector3(0,1.52f,-1.595f);dust.localRotation=Quaternion.Euler(-11.3f,0,0);
                    Part(dust,"Dust film",PrimitiveType.Cube,Vector3.zero,new Vector3(1.05f,.24f,.01f),"PaintCream");
                    Label(dust,"WASH ME",new Vector3(0,0,.008f),new Vector3(0,180,0),.04f,dark:true);break;
            }
        }
        private static void FaceCamp(Transform prop){var toward=-prop.localPosition;toward.y=0;if(toward.sqrMagnitude>0)prop.localRotation=Quaternion.LookRotation(toward);}
        private static Transform Part(Transform parent,string name,PrimitiveType shape,Vector3 at,Vector3 size,string look)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor(look);
            var collider=go.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            return go.transform;
        }
        // TextMesh reads from its back (-z), so a legend facing +z turns 180 degrees.
        private static void Label(Transform parent,string legend,Vector3 at,Vector3 euler,float size,bool dark=false)
        {
            var face=new GameObject(legend+" label");face.transform.SetParent(parent,false);face.transform.localPosition=at;face.transform.localRotation=Quaternion.Euler(euler);
            var text=face.AddComponent<TextMesh>();text.text=legend;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.characterSize=size;text.fontSize=48;text.color=dark?new Color(.16f,.12f,.1f):new Color(.96f,.94f,.84f);
            var font=Resources.Load<Font>("FestivalDisplay");if(font!=null){text.font=font;face.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
        }
    }
}
