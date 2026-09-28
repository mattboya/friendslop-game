using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Festival.Core;
using Festival.Network;

namespace Festival.Presentation
{
    /// <summary>Original runtime world with separate visual assemblies and gameplay collision.</summary>
    public sealed class FestivalWorld : MonoBehaviour
    {
        public const string RootName = "Festival generated world";
        public const string CampRootName = "Festival campsite lobby";
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly Dictionary<string,Material> artMaterials = new Dictionary<string,Material>();
        private readonly List<Light> stageLights = new List<Light>();
        private VolumeProfile duskProfile;
        private Font worldFont;
        private Transform owned;
        private Transform camp;
        private NavMeshSurface surface;
        private NavMeshSurface campSurface;
        private Light festivalSun,campSun;
        private string visiblePhase="";
        private Transform campShopRoot,marketShopRoot;
        private readonly List<GameObject> campProps=new List<GameObject>();
        private readonly List<GameObject> marketProps=new List<GameObject>();
        private readonly List<TextMesh> campTags=new List<TextMesh>();
        private readonly List<TextMesh> marketTags=new List<TextMesh>();
        private readonly List<TextMesh> worldSigns=new List<TextMesh>();
        private readonly Dictionary<string,GameObject> campInteriors=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,Transform> campAnticProps=new Dictionary<string,Transform>();
        private Transform campAnticTarget;
        private Vector3 campAnticRest;
        private float campAnticUntil;
        private int displayedCampAntics;
        private bool campAnticArmed;
        private string visibleInterior="";
        private string visibleInteriorKind="";
        private string displayedRound="";
        public bool IsReady { get; private set; }
        public bool NavigationReady => surface != null && surface.navMeshData != null;
        public bool CampNavigationReady => campSurface != null && campSurface.navMeshData != null;
        public bool IsCampVisible => camp != null && camp.gameObject.activeSelf;
        public Transform PlayerDjConsole { get; private set; }
        private void Awake() { Build(); }
        public void Build()
        {
            if (owned != null || transform.Find(RootName) != null) return;
            worldFont=Resources.Load<Font>("FestivalDisplay");
            owned = new GameObject(RootName).transform; owned.SetParent(transform, false);
            var earth = Material(new Color(.80f,.88f,.83f));
            earth.mainTexture=Resources.Load<Texture2D>("FestivalGround");
            earth.mainTextureScale=new Vector2(10,10);
            var dark = Material(new Color(.12f,.10f,.20f));
            var wood = Material(new Color(.42f,.27f,.23f));
            wood.mainTexture=Resources.Load<Texture2D>("FestivalWood");
            var mint = Material(new Color(.20f,.85f,.65f));
            var rose = Material(new Color(.92f,.25f,.49f));
            var gold = Material(new Color(.98f,.68f,.22f));
            artMaterials["Dark"]=dark;artMaterials["Wood"]=wood;artMaterials["Mint"]=mint;
            artMaterials["Rose"]=rose;artMaterials["Gold"]=gold;
            artMaterials["Cream"]=Material(new Color(.90f,.83f,.65f));
            artMaterials["Blue"]=Material(new Color(.22f,.43f,.74f));
            artMaterials["Metal"]=Material(new Color(.37f,.42f,.49f));
            artMaterials["Leaf"]=Material(new Color(.19f,.39f,.27f));
            artMaterials["LeafWarm"]=Material(new Color(.33f,.46f,.26f));
            artMaterials["Glass"]=Material(new Color(.30f,.74f,.78f));
            artMaterials["White"]=Material(new Color(.88f,.91f,.83f));
            if(artMaterials["Metal"].HasProperty("_Smoothness"))artMaterials["Metal"].SetFloat("_Smoothness",.46f);
            if(artMaterials["Glass"].HasProperty("_Smoothness"))artMaterials["Glass"].SetFloat("_Smoothness",.72f);
            var distantFestivalEarth=Material(new Color(.62f,.71f,.67f));
            distantFestivalEarth.mainTexture=Resources.Load<Texture2D>("FestivalGround");
            distantFestivalEarth.mainTextureScale=new Vector2(32,32);
            Box("Distant festival woodland",new Vector3(0,-.35f,0),new Vector3(220,.68f,220),distantFestivalEarth,false);
            var farRise=Material(distantFestivalEarth.color);
            farRise.mainTexture=Resources.Load<Texture2D>("FestivalGround");
            DistantRise("Festival distant woodland rise",62,79,100,farRise);
            Box("Ground", new Vector3(0,-.3f,0),new Vector3(80,.6f,80),earth);
            var path=Material(new Color(.57f,.39f,.30f));
            path.mainTexture=Resources.Load<Texture2D>("FestivalDirt");
            path.mainTextureScale=new Vector2(2,6);
            var longPath=Material(path.color);
            longPath.mainTexture=path.mainTexture;
            // A 1024 px tile now spans roughly four metres along the approach.
            // The old three repeats over 58 m stretched individual marks away.
            longPath.mainTextureScale=new Vector2(2,15);
            var lampGold=Glow(new Color(1f,.56f,.20f),2.3f);
            var lampRose=Glow(new Color(1f,.18f,.47f),2.1f);
            var lampMint=Glow(new Color(.19f,1f,.76f),2.1f);
            WornMainPath("Main footpath",3.5f,-31f,27f,longPath);
            Box("Market footpath",new Vector3(-11,.013f,-22),new Vector3(23,.02f,4),path,false);
            Box("East footpath",new Vector3(12,.013f,-20),new Vector3(24,.02f,4),path,false);
            Box("West crosspath",new Vector3(-14,.013f,5),new Vector3(28,.02f,3),path,false);
            Box("East crosspath",new Vector3(13,.013f,-4),new Vector3(26,.02f,3),path,false);
            var wornEdge=Material(new Color(.44f,.32f,.25f));
            wornEdge.mainTexture=Resources.Load<Texture2D>("FestivalDirt");
            WornPathEdges("Main path worn borders",3.5f,-31f,27f,wornEdge);
            // A few bright edges read as a touring event from a distance while
            // keeping the walking lanes and collision proxies simple.
            for(int i=0;i<9;i++)
            {
                float z=-30+i*7;
                Box("Path glint west",new Vector3(-3.65f,.038f,z),new Vector3(.11f,.025f,2.1f),i%2==0?lampGold:lampRose,false);
                Box("Path glint east",new Vector3(3.65f,.038f,z),new Vector3(.11f,.025f,2.1f),i%2==0?lampMint:lampGold,false);
            }
            ProxyBox("North boundary",new Vector3(0,2,40),new Vector3(81,4,1),wood);
            ProxyBox("South boundary",new Vector3(0,2,-40),new Vector3(81,4,1),wood);
            ProxyBox("West boundary",new Vector3(-40,2,0),new Vector3(1,4,80),wood);
            ProxyBox("East boundary",new Vector3(40,2,0),new Vector3(1,4,80),wood);
            // Stage sits behind the dance interaction landmark so its front stays accessible.
            ProxyBox("Stage",new Vector3(0,.7f,32),new Vector3(18,1.4f,9),dark);
            ProxyBox("Stage backdrop",new Vector3(0,4,36),new Vector3(18,6,.5f),dark);
            Visual("FestivalStage",new Vector3(0,0,32));
            Sign("AFTER HOURS",new Vector3(0,6.50f,27.10f),gold,.20f);
            Box("DJ console raised base",new Vector3(0,1.51f,30.15f),new Vector3(3.8f,.22f,1.4f),dark,false);
            var djConsole=Visual("FestivalDJDeck",new Vector3(0,1.62f,30.15f));
            Box("Resident DJ low riser",new Vector3(0,1.44f,30.88f),new Vector3(2.25f,.08f,.75f),dark,false);
            var stageDj=FestivalCharacter.Create(owned,"resident_stage_dj",Color.white,"Attendee");
            stageDj.Pose="Dj";
            stageDj.DjConsole=djConsole==null?null:djConsole.transform;
            stageDj.transform.localPosition=new Vector3(0,1.48f,30.84f);
            stageDj.transform.localRotation=Quaternion.Euler(0,180,0);
            stageDj.transform.localScale=Vector3.Scale(Vector3.one,stageDj.ShapeScale);
            // A reachable satellite deck belongs to the stage-front takeover
            // anchor. The resident stays at the main mixer behind the stage lip.
            Box("Takeover deck flight case",new Vector3(Catalog.StageTakeoverX,.48f,Catalog.StageTakeoverZ+.65f),
                new Vector3(2.45f,.96f,.55f),dark);
            Box("Takeover case rim",new Vector3(Catalog.StageTakeoverX,.98f,Catalog.StageTakeoverZ+.65f),
                new Vector3(2.52f,.075f,.62f),artMaterials["Metal"],false);
            var playerDeck=Visual("FestivalDJDeck",new Vector3(Catalog.StageTakeoverX,.75f,Catalog.StageTakeoverZ+.65f));
            if(playerDeck!=null)
            {
                playerDeck.name="Festival takeover DJ deck";
                playerDeck.transform.localRotation=Quaternion.Euler(0,180,0);
                playerDeck.transform.localScale=Vector3.one*.68f;
                PlayerDjConsole=playerDeck.transform;
            }
            foreach(float x in new[]{-3.65f,3.65f})
            {
                var performer=FestivalCharacter.Create(owned,x<0?"stage_hype_west":"stage_hype_east",Color.white);
                performer.Pose="Dance";
                performer.transform.localPosition=new Vector3(x,1.4f,29.55f);
                performer.transform.localRotation=Quaternion.Euler(0,x<0?165:195,0);
                performer.transform.localScale=Vector3.Scale(Vector3.one*.88f,performer.ShapeScale);
            }
            Box("Stage lip glow",new Vector3(0,1.46f,27.55f),new Vector3(18,.08f,.13f),lampRose,false);
            Box("Stage frame left",new Vector3(-8.8f,4.4f,35.65f),new Vector3(.15f,5.6f,.12f),lampMint,false);
            Box("Stage frame right",new Vector3(8.8f,4.4f,35.65f),new Vector3(.15f,5.6f,.12f),lampMint,false);
            Box("Stage frame crown",new Vector3(0,7.2f,35.65f),new Vector3(17.7f,.14f,.12f),lampRose,false);
            ProxyBox("Speaker L",new Vector3(-10,2,31),new Vector3(2,4,2),dark);
            ProxyBox("Speaker R",new Vector3(10,2,31),new Vector3(2,4,2),dark);
            Booth("Vendor",new Vector3(-18,0,-22),rose,wood,"NIGHT MARKET");
            Box("Night market merchandise counter",new Vector3(-18,.72f,-20.05f),new Vector3(6.7f,.16f,1),wood,false);
            Box("Night market lower display ledge",new Vector3(-18,.67f,-21.15f),new Vector3(6.7f,.14f,.8f),wood,false);
            Box("Night market shelf back",new Vector3(-18,1.4f,-19.55f),new Vector3(6.7f,1.5f,.16f),dark,false);
            Box("Night market shelf ledge",new Vector3(-18,1.78f,-20.05f),new Vector3(6.7f,.09f,1),wood,false);
            Box("Night market painted shelf edge",new Vector3(-18,1.78f,-20.57f),new Vector3(6.7f,.055f,.07f),rose,false);
            for(int plank=0;plank<12;plank++)
                Box("Night market counter plank",new Vector3(-21.04f+plank*.55f,1.84f,-20.05f),new Vector3(.48f,.025f,.92f),plank%3==0?artMaterials["Cream"]:wood,false);
            Visual("FestivalStallSupplies",new Vector3(-24,0,-19));
            Visual("FestivalStallPerformance",new Vector3(-18,0,-19));
            Visual("FestivalStallStock",new Vector3(-12,0,-19));
            Booth("Medical",new Vector3(24,0,-20),mint,wood,"MEDICAL +");
            Visual("FestivalMedical",new Vector3(24,0,-17));
            Booth("Holding",new Vector3(27,0,5),gold,wood,"SECURITY");
            Visual("FestivalSecurity",new Vector3(27,0,8));
            Booth("Lost property",new Vector3(-28,0,16),mint,wood,"LOST + FOUND");
            // Keep the exact interaction position open; geometry stands 3m behind it.
            Box("Stash",new Vector3(-25,.5f,-5),new Vector3(2,1,1),gold);
            Box("Stash sign mast",new Vector3(-25,1.23f,-5.03f),new Vector3(.11f,.48f,.11f),wood,false);
            Sign("CREW STASH",new Vector3(-25,1.31f,-5.57f),mint,.075f);
            ProxyBox("Shuttle floor",new Vector3(0,.16f,-36),new Vector3(8,.32f,3),dark);
            ProxyBox("Shuttle rear wall",new Vector3(0,1.55f,-37.5f),new Vector3(8,3,.2f),dark);
            ProxyBox("Shuttle west wall",new Vector3(-4,1.55f,-36),new Vector3(.2f,3,3),dark);
            ProxyBox("Shuttle east wall",new Vector3(4,1.55f,-36),new Vector3(.2f,3,3),dark);
            ProxyBox("Shuttle doorway west",new Vector3(-2.65f,1.55f,-34.5f),new Vector3(2.7f,3,.2f),dark);
            ProxyBox("Shuttle doorway east",new Vector3(2.65f,1.55f,-34.5f),new Vector3(2.7f,3,.2f),dark);
            Visual("FestivalShuttle",new Vector3(0,0,-36));
            Sign("LAST SHUTTLE",new Vector3(0,2.72f,-34.4f),dark,.14f,180);
            for (int i=0;i<8;i++)
            {
                float x = i%2==0 ? -33 : 33; float z=-28+(i/2)*17;
                Box("Light pole",new Vector3(x,3.5f,z),new Vector3(.15f,7,.15f),wood);
                var bulb=Box("Lantern",new Vector3(x,7,z),Vector3.one*.7f,i%2==0?lampGold:lampRose,false);
                var light=bulb.AddComponent<Light>();light.type=LightType.Point;light.range=13;light.intensity=2;light.color=i%2==0?new Color(1,.65f,.25f):new Color(1,.3f,.6f);light.shadows=LightShadows.None;
            }
            foreach(var staff in new[]{
                new {Id="vendor_supplies",Role="Vendor0",X=-24f,Z=-22f},
                new {Id="vendor_performance",Role="Vendor1",X=-18f,Z=-22f},
                new {Id="vendor_stock",Role="Vendor2",X=-12f,Z=-22f},
                new {Id="festival_medic",Role="Medic",X=24f,Z=-20f}})
            {
                var actor=FestivalCharacter.Create(owned,staff.Id,Color.white,staff.Role);
                actor.transform.localPosition=new Vector3(staff.X,0,staff.Z);
                actor.transform.localRotation=Quaternion.Euler(0,180,0);
                actor.transform.localScale=Vector3.Scale(Vector3.one*.78f,actor.ShapeScale);
            }
            // Distinct, visible landmarks for spoken directions during private clues.
            Totem("SUN",new Vector3(16,0,-1.5f),gold,wood,dark);
            Totem("MOON",new Vector3(-16,0,7.5f),mint,wood,dark);
            var leaf=artMaterials["Leaf"];
            var leafWarm=artMaterials["LeafWarm"];
            for(int i=0;i<22;i++)
            {
                float x=-37+(i%11)*7.4f,z=i<11?38:-38;
                if(Mathf.Abs(x)<8&&z<0)continue;
                var tree=Visual(i%4==0?"FestivalTreeFir":i%2==0?"FestivalTreeA":"FestivalTreeB",new Vector3(x,0,z));
                if(tree!=null)tree.transform.localScale=Vector3.one*(.85f+i%3*.08f);
            }
            var approachTrees=new[]{
                new Vector3(-15,0,23),new Vector3(-25,0,26),
                new Vector3(-21,0,11),new Vector3(-19,0,-7),
                new Vector3(15,0,23),new Vector3(25,0,24),
                new Vector3(20,0,11),new Vector3(18,0,-7)};
            for(int i=0;i<44;i++)
            {
                float angle=(i+.5f)*(Mathf.PI*2/44f);
                float radius=46f+(i%4)*3.2f;
                bool framesApproach=i%6==0;
                var position=framesApproach?approachTrees[i/6]:
                    new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                var tree=Visual(i%4==0?"FestivalTreeFir":i%3==0?"FestivalTreeB":"FestivalTreeA",
                    position);
                if(tree!=null)
                {
                    tree.transform.localScale=Vector3.one*(framesApproach?1.05f+(i%4)*.08f:1.2f+(i%5)*.12f);
                    tree.transform.localRotation=Quaternion.Euler(0,(i*97)%360,0);
                }
            }
            foreach(var site in new[]{new Vector3(-24,0,10.5f),new Vector3(23,0,13.5f),new Vector3(22,0,-11.5f)})
            {
                var tree=Visual(site.x<0?"FestivalTreeA":"FestivalTreeB",site);
                if(tree!=null)tree.transform.localScale=Vector3.one*1.22f;
                ProxyBox("Gathering tree trunk",site+new Vector3(0,1.2f,0),new Vector3(.76f,2.4f,.76f),wood);
            }
            foreach(var table in new[]{new Vector3(-24,0,2),new Vector3(22,0,-10)})
            {
                Box("Festival picnic tabletop",table+new Vector3(0,.72f,0),new Vector3(2.4f,.14f,1.18f),wood);
                ProxyBox("Festival picnic table base",table+new Vector3(0,.38f,0),new Vector3(1.5f,.76f,.60f),wood);
                foreach(float side in new[]{-1f,1f})
                    Box("Festival picnic bench",table+new Vector3(side*1.49f,.39f,0),new Vector3(.39f,.12f,2.10f),wood);
            }
            var ambient=owned.gameObject.AddComponent<FestivalAmbientCrowd>();
            ambient.Build();
            for(int i=0;i<12;i++)
            {
                float x=-28+i*5;
                Box("Bunting pole",new Vector3(x,4,-14),new Vector3(.09f,8,.09f),wood,false);
                var flag=Box("Festival pennant",new Vector3(x+1.5f,6.8f,-14),new Vector3(1.5f,.8f,.05f),i%2==0?rose:gold,false);
                flag.transform.localRotation=Quaternion.Euler(0,0,-12+i%3*12);
            }
            for(int i=0;i<8;i++)
            {
                float x=i%2==0?-35:35,z=-22+(i/2)*15;
                Visual("FestivalTent",new Vector3(x,0,z));
            }
            GrassBanks("Meadow grass banks",leafWarm);
            // A few larger, authored undergrowth shapes ground the woodland
            // without filling the walkable route or creating navigation edges.
            for(int i=0;i<28;i++)
            {
                float x=-33f+(i*19%67),z=-32f+(i*37%65);
                if(Mathf.Abs(x)<10f||x< -10f&&z< -15f&&z> -28f||x>18f&&z< -14f&&z> -25f)continue;
                var detail=Visual("FestivalGroveDetail",new Vector3(x,0,z));
                if(detail!=null){detail.transform.localScale=Vector3.one*(.78f+i%4*.09f);detail.transform.localRotation=Quaternion.Euler(0,i*113%360,0);}
            }
            for(int row=0;row<3;row++)for(int i=0;i<15;i++)
            {
                float x=-28+i*4,z=-14+row*16,y=5.7f+Mathf.Abs(x)*.025f;
                Box("Festoon cable",new Vector3(x,y,z),new Vector3(4.1f,.025f,.025f),dark,false);
                var lamp=Box("Warm string bulb",new Vector3(x,y-.12f,z),new Vector3(.16f,.24f,.16f),i%3==0?lampRose:lampGold,false);
                if(i%5==0){var l=lamp.AddComponent<Light>();l.type=LightType.Point;l.range=9;l.intensity=1.5f;l.color=new Color(1,.66f,.25f);}
            }
            for(int i=0;i<5;i++)
            {
                float x=-8+i*4;
                var lamp=Box("Stage lamp",new Vector3(x,5.8f,34),new Vector3(.4f,.4f,.5f),i%2==0?lampMint:lampRose,false);
                var l=lamp.AddComponent<Light>();l.type=LightType.Spot;l.range=25;l.spotAngle=65;l.intensity=8;l.color=i%2==0?mint.color:rose.color;l.shadows=LightShadows.None;
                stageLights.Add(l);
            }
            var sun=new GameObject("Twilight sun");sun.transform.SetParent(owned,false);sun.transform.rotation=Quaternion.Euler(14,-30,0);
            var directional=sun.AddComponent<Light>();directional.type=LightType.Directional;directional.color=new Color(1,.67f,.48f);directional.intensity=.82f;directional.shadows=LightShadows.Hard;festivalSun=directional;
            var skyTemplate=Resources.Load<Material>("FestivalSky");
            if(skyTemplate!=null)
            {
                var dusk=new Material(skyTemplate);
                dusk.SetColor("_SkyTint",new Color(.23f,.19f,.39f));
                dusk.SetColor("_GroundColor",new Color(.17f,.10f,.21f));
                dusk.SetFloat("_AtmosphereThickness",.8f);
                dusk.SetFloat("_Exposure",.53f);
                materials.Add(dusk);RenderSettings.skybox=dusk;
            }
            RenderSettings.sun=directional;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.51f,.59f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.36f,.43f,.58f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=36;RenderSettings.fogEndDistance=90;
            var grade=gameObject.AddComponent<Volume>();grade.isGlobal=true;grade.priority=20;
            duskProfile=ScriptableObject.CreateInstance<VolumeProfile>();grade.sharedProfile=duskProfile;
            duskProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var colorGrade=duskProfile.Add<ColorAdjustments>(true);
            colorGrade.postExposure.Override(.15f);colorGrade.contrast.Override(8f);colorGrade.saturation.Override(-5f);
            var bloom=duskProfile.Add<Bloom>(true);
            bloom.threshold.Override(1.12f);bloom.intensity.Override(.12f);bloom.scatter.Override(.5f);
            Physics.SyncTransforms();
            surface=owned.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects=CollectObjects.Children;
            surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize=true;surface.voxelSize=.12f;
            // Default humanoid agent is conservative for the player's 0.35m capsule.
            // Keep agent type 0 so ordinary NavMesh.CalculatePath uses this surface.
            surface.BuildNavMesh();
            marketShopRoot=ShopDisplayRoot("Night market goods",owned);
            // Only one of the two overlapping walkable spaces is active at a
            // time. The camp has its own navigation and collision geometry.
            owned.gameObject.SetActive(false);
            BuildCamp(earth,wood,mint,rose,gold,dark,path,lampGold,lampRose,lampMint);
            IsReady=NavigationReady&&CampNavigationReady;
            SetPhase("Shopping");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("WorldLifecycle","built","festival_navigation="+NavigationReady+" camp_navigation="+CampNavigationReady);
            var activePipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            DevelopmentDiagnostics.GraphicsEvent("Rendering","world_configuration",
                "pipeline="+(activePipeline==null?"missing":activePipeline.name)
                +" soft_supported="+(activePipeline!=null&&activePipeline.supportsSoftShadows)
                +" festival_shadow_request="+festivalSun.shadows+" camp_shadow_request="+campSun.shadows
                +" world_volume_priority=20");
#endif
        }
        private void BuildCamp(Material earth,Material wood,Material mint,Material rose,Material gold,Material dark,Material path,Material lampGold,Material lampRose,Material lampMint)
        {
            var festival=owned;
            camp=new GameObject(CampRootName).transform;camp.SetParent(transform,false);
            owned=camp;
            var distantEarth=Material(new Color(.64f,.73f,.69f));
            distantEarth.mainTexture=Resources.Load<Texture2D>("FestivalGround");
            distantEarth.mainTextureScale=new Vector2(34,34);
            Box("Distant woodland ground",new Vector3(0,-.35f,0),new Vector3(220,.68f,220),distantEarth,false);
            var campRise=Material(distantEarth.color);
            campRise.mainTexture=Resources.Load<Texture2D>("FestivalGround");
            DistantRise("Camp distant woodland rise",54,72,100,campRise);
            Box("Camp earth",new Vector3(0,-.3f,0),new Vector3(CampFeatures.CampHalfWidth*2,.6f,CampFeatures.CampHalfDepth*2),earth);
            // Keep the existing play boundary, but let woodland scenery make the
            // edge instead of a four-sided brown wall in every camera angle.
            ProxyBox("Camp north boundary",new Vector3(0,1.1f,CampFeatures.CampHalfDepth),new Vector3(CampFeatures.CampHalfWidth*2+1,2.2f,.5f),wood);
            ProxyBox("Camp south boundary",new Vector3(0,1.1f,-CampFeatures.CampHalfDepth),new Vector3(CampFeatures.CampHalfWidth*2+1,2.2f,.5f),wood);
            ProxyBox("Camp west boundary",new Vector3(-CampFeatures.CampHalfWidth,1.1f,0),new Vector3(.5f,2.2f,CampFeatures.CampHalfDepth*2),wood);
            ProxyBox("Camp east boundary",new Vector3(CampFeatures.CampHalfWidth,1.1f,0),new Vector3(.5f,2.2f,CampFeatures.CampHalfDepth*2),wood);
            Box("Camp footpath",new Vector3(0,.024f,0),new Vector3(3.3f,.035f,21),path,false);
            Box("Camp shade lane",new Vector3(0,.025f,-1),new Vector3(13,.036f,3.8f),path,false);
            var campMat=Material(new Color(.46f,.32f,.27f));
            campMat.mainTexture=Resources.Load<Texture2D>("FestivalCanvas");
            campMat.mainTextureScale=new Vector2(5,5);
            Box("Camp gathering mat",new Vector3(0,.02f,0),new Vector3(17,.04f,15),campMat,false);
            for(int stripe=0;stripe<7;stripe++)
            {
                float z=-6+stripe*2;
                Box("Camp woven mat stripe",new Vector3(0,.049f,z),new Vector3(16,.012f,.11f),stripe%2==0?mint:rose,false);
            }
            foreach(float x in new[]{-4.3f,4.3f})
            {
                var shade=Visual("FestivalCampShade",new Vector3(x,0,0));
                if(x>0&&shade!=null)
                    foreach(var renderer in shade.GetComponentsInChildren<Renderer>())
                        if(renderer.name.Contains("__CanvasMint"))renderer.sharedMaterial=FestivalArtView.MaterialFor("CanvasCream");
                ProxyBox("Shade center mast",new Vector3(x,2.08f,0),new Vector3(.28f,4.16f,.28f),wood);
                foreach(float dx in new[]{-3.6f,3.6f})foreach(float dz in new[]{-3.6f,3.6f})
                    ProxyBox("Shade pole",new Vector3(x+dx,1.8f,dz),new Vector3(.13f,3.6f,.13f),wood);
                for(int i=0;i<5;i++)
                {
                    float bulbX=x-3.2f+i*1.6f;
                    Box("Shade bulb cord",new Vector3(bulbX,3.35f,-3.55f),new Vector3(.025f,.28f,.025f),dark,false);
                    Box("Shade bulb socket",new Vector3(bulbX,3.21f,-3.55f),new Vector3(.17f,.08f,.17f),dark,false);
                    SoftProp("Shade warm bulb",new Vector3(bulbX,3.09f,-3.55f),new Vector3(.15f,.13f,.15f),i%2==0?lampGold:lampRose,Quaternion.identity);
                }
                var workLamp=new GameObject("Pavilion warm pool");workLamp.transform.SetParent(camp,false);
                workLamp.transform.localPosition=new Vector3(x,2.75f,0);
                var workLight=workLamp.AddComponent<Light>();workLight.type=LightType.Point;workLight.range=6.5f;workLight.intensity=1.25f;
                workLight.color=new Color(1f,.78f,.55f);workLight.shadows=LightShadows.None;
            }
            for(int i=0;i<4;i++)
            {
                float x=i%2==0?-7.8f:7.8f,z=i<2?-6:6;
                Box("Camp lantern stake",new Vector3(x,.7f,z),new Vector3(.09f,1.4f,.09f),wood,false);
                var lantern=Box("Camp lantern",new Vector3(x,1.48f,z),new Vector3(.42f,.36f,.42f),i%2==0?lampGold:lampMint,false);
                var light=lantern.AddComponent<Light>();light.type=LightType.Point;light.range=8;light.intensity=1.8f;light.color=i%2==0?new Color(1,.63f,.35f):new Color(.4f,1,.8f);
            }
            foreach(var site in CampFeatures.Sites)
            {
                var center=new Vector3(site.X,0,site.Z);
                var resource=site.Kind=="Tent"?(site.Id.EndsWith("7")||site.Id.EndsWith("8")?"FestivalDomeTent":"FestivalTent"):
                    site.Kind=="Car"?(site.Id.EndsWith("5")||site.Id.EndsWith("6")?"FestivalCampVan":"FestivalCampCar"):"FestivalPortaPotty";
                var prop=Visual(resource,center);
                if(prop!=null)
                {
                    prop.name=site.Id.EndsWith("1")||site.Id=="car_5"||site.Id=="tent_7"?resource:site.Id+" "+resource;
                    prop.transform.localRotation=Quaternion.Euler(0,site.Yaw,0);
                    // Shared meshes; roof and car colors vary by a small, fixed camp palette.
                    if(site.Kind=="Car"||site.Kind=="Tent")
                    {
                        string accent=site.Id.EndsWith("1")||site.Id.EndsWith("4")?"Rose":site.Id.EndsWith("2")||site.Id.EndsWith("5")?"Mint":"Gold";
                        foreach(var renderer in prop.GetComponentsInChildren<Renderer>())
                            if(renderer.name.Contains("__CanvasRose"))renderer.sharedMaterial=FestivalArtView.MaterialFor("Canvas"+accent);
                            else if(renderer.name.Contains("__PaintRose"))renderer.sharedMaterial=FestivalArtView.MaterialFor("Paint"+accent);
                            else if(renderer.name.Contains("__Rose"))renderer.sharedMaterial=FestivalArtView.MaterialFor(accent);
                        if(site.Kind=="Tent")prop.transform.localScale=Vector3.one*((site.Id.EndsWith("5")||site.Id.EndsWith("6")) ? .87f : 1f);
                    }
                }
                if(site.Kind=="Tent")ProxyBox("Tent footprint",center+new Vector3(0,.6f,0),new Vector3(3.4f,1.2f,3.1f),dark);
                else if(site.Kind=="Car")
                {
                    ProxyBox("Parked car body",center+new Vector3(0,.7f,0),new Vector3(3.4f,1.4f,5.3f),dark);
                    if(site.Id.EndsWith("2"))Box("Festival road trip surfboard",center+new Vector3(0,1.98f,0),new Vector3(.62f,.11f,2.4f),mint,false);
                    if(site.Id.EndsWith("3"))Box("Overpacked roof box",center+new Vector3(0,2.03f,-.1f),new Vector3(1.4f,.36f,1.9f),gold,false);
                    if(site.Id.EndsWith("4"))Box("Camp luggage stack",center+new Vector3(0,1.99f,-.3f),new Vector3(1.5f,.32f,1.3f),rose,false);
                }
                else
                {
                    ProxyBox("Porta potty cabin",center+new Vector3(0,1.35f,0),new Vector3(2.2f,2.7f,2.2f),dark);
                    Sign("PORTA POTTY",center+new Vector3(0,2.3f,-1.24f),dark,.1f);
                }
            }
            BuildCampInteriors(wood,dark,mint,rose,gold);
            // The shared DJ is on the gathering mat. The server owns the track choice.
            Box("Camp DJ table",new Vector3(CampFeatures.DjX,.76f,CampFeatures.DjZ),new Vector3(2.5f,.16f,1.0f),wood,false);
            Box("Camp DJ deck",new Vector3(CampFeatures.DjX,.89f,CampFeatures.DjZ),new Vector3(1.65f,.10f,.68f),dark,false);
            foreach(float x in new[]{-1.45f,1.45f})
            {
                Box("Camp DJ speaker",new Vector3(CampFeatures.DjX+x,1.16f,CampFeatures.DjZ),new Vector3(.52f,.86f,.44f),dark,false);
                Box("Camp DJ speaker cone",new Vector3(CampFeatures.DjX+x,1.16f,CampFeatures.DjZ-.25f),new Vector3(.30f,.30f,.04f),mint,false);
            }
            Sign("CAMP DJ  •  PICK THE VIBE",new Vector3(CampFeatures.DjX,1.78f,CampFeatures.DjZ-.25f),gold,.037f);
            Box("Round review podium",new Vector3(-8,.62f,3.6f),new Vector3(1.7f,1.2f,.8f),wood,false);
            Sign("ROUND REVIEW",new Vector3(-8,1.39f,3.15f),rose,.038f);
            // Camp life clusters make the shaded center feel used and provide
            // landmarks without obstructing the seller-to-trailhead route.
            foreach(float x in new[]{-5.2f,5.2f})
            {
                Box("Camp picnic tabletop",new Vector3(x,.66f,-1.6f),new Vector3(2.2f,.12f,.9f),wood,false);
                foreach(float side in new[]{-1f,1f})
                    Box("Camp picnic bench",new Vector3(x+side*1.23f,.36f,-1.6f),new Vector3(.43f,.10f,1.8f),wood,false);
                Box("Camp cooler body",new Vector3(x>0?x+1.65f:x-1.65f,.36f,-3.6f),new Vector3(.9f,.65f,.6f),x>0?mint:rose,false);
                Box("Camp cooler lid",new Vector3(x>0?x+1.65f:x-1.65f,.73f,-3.6f),new Vector3(.96f,.1f,.66f),artMaterials["Cream"],false);
            }
            for(int i=0;i<28;i++)
            {
                float x=-21f+(i*17%43),z=-20f+(i*29%41);
                if(Mathf.Abs(x)<9f&&z>-10f&&z<20f)continue;
                var tuft=Box("Camp meadow tuft",new Vector3(x,.18f,z),new Vector3(.10f,.36f,.27f),i%4==0?artMaterials["LeafWarm"]:artMaterials["Leaf"],false);
                tuft.transform.localRotation=Quaternion.Euler(0,i*73%180,18);
            }
            ProxyBox("Seller table collision",new Vector3(0,.62f,8.8f),new Vector3(3.4f,1.2f,1),wood);
            Visual("FestivalCampShop",new Vector3(0,0,9.15f));
            Box("Camp supplies signboard",new Vector3(0,2.5f,9.49f),new Vector3(4.5f,.66f,.12f),dark,false);
            Sign("CAMP SUPPLIES",new Vector3(0,2.5f,9.4f),gold,.075f);
            Box("Seller sign glow",new Vector3(0,2.12f,9.42f),new Vector3(4.4f,.08f,.08f),lampGold,false);
            for(int i=0;i<5;i++)
                Box("Seller counter goods",new Vector3(-1.2f+i*.6f,1.35f,8.65f),new Vector3(.30f,.24f,.36f),i%2==0?rose:mint,false);
            foreach(float x in new[]{-4.65f,4.65f})
            {
                Box("Camp gear shelf",new Vector3(x,.74f,9.05f),new Vector3(3.45f,.14f,.8f),wood,false);
                Box("Camp shelf back",new Vector3(x,1.15f,9.43f),new Vector3(3.45f,1.05f,.12f),dark,false);
                Box("Camp shelf trim",new Vector3(x,1.76f,9.1f),new Vector3(3.5f,.12f,.85f),gold,false);
                foreach(float edge in new[]{-1.7f,1.7f})
                    Box("Shelf frame",new Vector3(x+edge,.94f,9.28f),new Vector3(.09f,1.78f,.09f),wood,false);
                for(int slat=0;slat<8;slat++)
                    Box("Seller shelf wood slat",new Vector3(x-1.46f+slat*.42f,.72f,9.04f),new Vector3(.34f,.055f,.88f),slat%2==0?wood:artMaterials["Cream"],false);
                Sign(x<0?"ESSENTIALS":"FESTIVAL GEAR",new Vector3(x,1.45f,9.31f),gold,.047f);
            }
            Box("Trailhead path",new Vector3(0,.045f,16),new Vector3(4.2f,.04f,14),path,false);
            Box("Trailhead left post",new Vector3(-2,1.8f,19),new Vector3(.23f,3.6f,.24f),wood);
            Box("Trailhead right post",new Vector3(2,1.8f,19),new Vector3(.23f,3.6f,.24f),wood);
            Box("Trailhead crown",new Vector3(0,3.4f,19),new Vector3(4.2f,.42f,.35f),gold,false);
            Sign("READY FOR THE FESTIVAL?",new Vector3(0,3.42f,18.8f),dark,.048f);
            foreach(float x in new[]{-2f,2f})
            {
                var bulb=Box("Trailhead light",new Vector3(x,3.02f,18.74f),Vector3.one*.28f,lampMint,false);
                var light=bulb.AddComponent<Light>();light.type=LightType.Point;light.range=6;light.intensity=2;light.color=mint.color;
            }
            for(int i=0;i<28;i++)
            {
                float angle=(i+.27f)*(Mathf.PI*2/28f);
                float radius=22.0f+(i%4)*1.15f;
                var point=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(Mathf.Abs(point.x)<4f&&point.z>17f)continue;
                bool overlapsSite=false;
                foreach(var site in CampFeatures.Sites)
                {
                    float clearance=site.Kind=="Car"?4.6f:site.Kind=="Tent"?3.8f:3.1f;
                    if(new Vector2(point.x-site.X,point.z-site.Z).sqrMagnitude<clearance*clearance)
                    {overlapsSite=true;break;}
                }
                if(overlapsSite)continue;
                var tree=Visual(i%4==0?"FestivalTreeFir":i%3==0?"FestivalTreeB":"FestivalTreeA",point);
                if(tree!=null)
                {
                    tree.transform.localScale=Vector3.one*(.94f+(i%5)*.10f);
                    tree.transform.localRotation=Quaternion.Euler(0,(i*83)%360,0);
                }
            }
            for(int i=0;i<40;i++)
            {
                float angle=(i+.61f)*(Mathf.PI*2/40f);
                float radius=32f+(i%5)*3.4f;
                var tree=Visual(i%3==1?"FestivalTreeFir":i%3==2?"FestivalTreeB":"FestivalTreeA",
                    new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
                if(tree!=null)
                {
                    tree.transform.localScale=Vector3.one*(1.15f+(i%6)*.13f);
                    tree.transform.localRotation=Quaternion.Euler(0,(i*47)%360,0);
                }
            }
            for(int i=0;i<36;i++)
            {
                float x=-29f+(i*17%59),z=-28f+(i*23%57);
                if(Mathf.Abs(x)<10f&&z> -13f&&z<22f)continue;
                bool nearEntrance=false;
                foreach(var site in CampFeatures.Sites)
                {
                    float dx=x-site.X,dz=z-site.Z;
                    if(dx*dx+dz*dz<25f){nearEntrance=true;break;}
                }
                if(nearEntrance)continue;
                var detail=Visual("FestivalGroveDetail",new Vector3(x,0,z));
                if(detail!=null){detail.transform.localScale=Vector3.one*(.72f+i%3*.14f);detail.transform.localRotation=Quaternion.Euler(0,i*79%360,0);}
            }
            var seller=FestivalCharacter.Create(camp,"camp_seller_2",Color.white,"Vendor0");
            seller.transform.localPosition=new Vector3(0,0,7);
            seller.transform.localRotation=Quaternion.Euler(0,180,0);
            seller.transform.localScale=Vector3.Scale(Vector3.one*.85f,seller.ShapeScale);
            var campLight=new GameObject("Camp sunset");campLight.transform.SetParent(camp,false);
            campLight.transform.rotation=Quaternion.Euler(18,-35,0);
            campSun=campLight.AddComponent<Light>();campSun.type=LightType.Directional;campSun.intensity=1.3f;campSun.color=new Color(1,.82f,.65f);campSun.shadows=LightShadows.Hard;
            Physics.SyncTransforms();
            campSurface=camp.gameObject.AddComponent<NavMeshSurface>();
            campSurface.collectObjects=CollectObjects.Children;
            campSurface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            campSurface.overrideVoxelSize=true;campSurface.voxelSize=.12f;
            campSurface.BuildNavMesh();
            campShopRoot=ShopDisplayRoot("Campsite shelf goods",camp);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("WorldLifecycle","camp_layout",
                "half_width="+CampFeatures.CampHalfWidth+" half_depth="+CampFeatures.CampHalfDepth+
                " sites="+CampFeatures.Sites.Length+" navigation="+CampNavigationReady);
#endif
            owned=festival;
        }
        private void BuildCampInteriors(Material wood,Material dark,Material mint,Material rose,Material gold)
        {
            var previous=owned;
            foreach(var kind in new[]{"Car","Tent","Potty"})
            {
                var room=new GameObject(kind+" interior room");room.transform.SetParent(camp,false);
                campInteriors[kind]=room;owned=room.transform;
                var center=CampInteriorPosition(kind);
                var wall=kind=="Tent"?FestivalArtView.MaterialFor("CanvasRose"):kind=="Potty"?artMaterials["Blue"]:dark;
                Box(kind+" interior floor",center+new Vector3(0,.02f,0),new Vector3(5.8f,.12f,5.8f),kind=="Tent"?FestivalArtView.MaterialFor("CanvasCream"):wood,false);
                Box(kind+" interior rear",center+new Vector3(0,1.55f,2.9f),new Vector3(5.8f,3.1f,.18f),wall,false);
                // Keep the front wall open where the movement exit is. A solid
                // wall here made the room read as a sealed box.
                foreach(float side in new[]{-1f,1f})
                    Box(kind+" interior front side",center+new Vector3(side*1.9f,1.55f,-2.9f),new Vector3(2f,3.1f,.18f),wall,false);
                Box(kind+" interior front header",center+new Vector3(0,2.83f,-2.9f),new Vector3(1.8f,.55f,.18f),wall,false);
                Box(kind+" interior exit threshold",center+new Vector3(0,.07f,-2.56f),new Vector3(1.7f,.035f,.22f),gold,false);
                // A short fabric/trim vestibule closes the view beyond this
                // teleported room, while the near threshold stays walkable.
                Box(kind+" exit vestibule floor",center+new Vector3(0,.02f,-3.65f),new Vector3(1.8f,.12f,1.6f),wall,false);
                foreach(float side in new[]{-1f,1f})
                    Box(kind+" exit vestibule side",center+new Vector3(side*.92f,1.24f,-3.65f),new Vector3(.08f,2.48f,1.6f),wall,false);
                Box(kind+" exit vestibule canopy",center+new Vector3(0,2.51f,-3.65f),new Vector3(1.92f,.10f,1.6f),wall,false);
                Box(kind+" exit vestibule flap",center+new Vector3(0,1.25f,-4.44f),new Vector3(1.86f,2.5f,.08f),
                    kind=="Tent"?FestivalArtView.MaterialFor("CanvasDark"):dark,false);
                Box(kind+" exit flap pull",center+new Vector3(0,1.28f,-4.38f),new Vector3(.045f,.34f,.06f),gold,false);
                Box(kind+" interior left",center+new Vector3(-2.9f,1.55f,0),new Vector3(.18f,3.1f,5.8f),wall,false);
                Box(kind+" interior right",center+new Vector3(2.9f,1.55f,0),new Vector3(.18f,3.1f,5.8f),wall,false);
                Box(kind+" interior ceiling",center+new Vector3(0,3.1f,0),new Vector3(5.8f,.18f,5.8f),wall,false);
                var roomLamp=new GameObject(kind+" interior lamp");roomLamp.transform.SetParent(room.transform,false);
                roomLamp.transform.position=center+new Vector3(0,2.65f,-.35f);
                var interiorLight=roomLamp.AddComponent<Light>();interiorLight.type=LightType.Point;
                interiorLight.range=7;interiorLight.intensity=2.8f;interiorLight.shadows=LightShadows.None;
                interiorLight.color=kind=="Potty"?new Color(.7f,.9f,1f):new Color(1f,.86f,.66f);
                Box(kind+" lamp shade",center+new Vector3(0,2.95f,-.35f),new Vector3(.65f,.12f,.65f),gold,false);
                if(kind=="Car")
                {
                    Box("Car windshield",center+new Vector3(0,1.91f,2.76f),new Vector3(4.13f,1.08f,.07f),artMaterials["Glass"],false);
                    Box("Car windscreen header",center+new Vector3(0,2.55f,2.65f),new Vector3(5.1f,.16f,.25f),dark,false);
                    foreach(float side in new[]{-1f,1f})
                    {
                        Box("Car A pillar",center+new Vector3(side*2.23f,1.87f,2.60f),new Vector3(.20f,1.38f,.24f),dark,false);
                        Box("Car side window",center+new Vector3(side*2.78f,1.92f,.20f),new Vector3(.07f,.94f,2.20f),artMaterials["Glass"],false);
                        Box("Car window sill",center+new Vector3(side*2.72f,1.39f,.20f),new Vector3(.18f,.10f,2.42f),artMaterials["Metal"],false);
                        Box("Car door latch",center+new Vector3(side*2.71f,1.09f,-.62f),new Vector3(.17f,.08f,.30f),gold,false);
                        Box("Car bucket seat cushion",center+new Vector3(side*1.36f,.52f,.05f),new Vector3(1.27f,.36f,1.18f),rose,false);
                        var back=Box("Car bucket seat back",center+new Vector3(side*1.36f,1.12f,.49f),new Vector3(1.22f,1.25f,.27f),rose,false);
                        back.transform.localRotation=Quaternion.Euler(-9,0,0);
                        Box("Car headrest",center+new Vector3(side*1.36f,1.87f,.52f),new Vector3(.60f,.29f,.24f),rose,false);
                        Box("Car floor mat",center+new Vector3(side*1.36f,.11f,1.15f),new Vector3(1.35f,.02f,1.40f),dark,false);
                    }
                    Box("Car dashboard",center+new Vector3(0,1.02f,1.88f),new Vector3(4.75f,.39f,.97f),artMaterials["Metal"],false);
                    Box("Car instrument cowl",center+new Vector3(-1.08f,1.27f,1.48f),new Vector3(1.26f,.24f,.44f),dark,false);
                    for(int spoke=0;spoke<8;spoke++)
                    {
                        float angle=spoke*Mathf.PI/4;
                        var segment=Box("Car steering wheel rim",center+new Vector3(-1+Mathf.Sin(angle)*.35f,1.48f+Mathf.Cos(angle)*.35f,1.04f),new Vector3(.29f,.07f,.11f),dark,false);
                        segment.transform.rotation=Quaternion.Euler(0,0,-spoke*45);
                    }
                    Box("Car steering wheel hub",center+new Vector3(-1,1.48f,1.04f),new Vector3(.21f,.21f,.16f),gold,false);
                    Box("Car radio",center+new Vector3(.7f,1.18f,1.08f),new Vector3(.62f,.24f,.12f),dark,false);
                    for(int dial=0;dial<3;dial++)
                        Box("Car ridiculous radio dial",center+new Vector3(.49f+dial*.20f,1.19f,1.015f),new Vector3(.075f,.075f,.04f),gold,false);
                    Box("Car rear view mirror",center+new Vector3(0,2.30f,2.32f),new Vector3(.72f,.22f,.09f),dark,false);
                    Box("Car hanging fuzzy dice",center+new Vector3(0,1.99f,2.30f),new Vector3(.18f,.18f,.18f),mint,false);
                    Box("Car bobblehead body",center+new Vector3(1.7f,1.35f,1.1f),new Vector3(.18f,.25f,.18f),rose,false);
                    campAnticProps[kind]=Box("Car bobblehead head",center+new Vector3(1.7f,1.55f,1.1f),Vector3.one*.24f,gold,false).transform;
                    Box("Car rear bench",center+new Vector3(0,.45f,-2.38f),new Vector3(4.6f,.72f,.65f),rose,false);
                }
                else if(kind=="Tent")
                {
                    var groundsheet=FestivalArtView.MaterialFor("CanvasDark");
                    foreach(float side in new[]{-1f,1f})
                        Box("Tent groundsheet bound edge",center+new Vector3(side*2.70f,.11f,0),new Vector3(.08f,.035f,5.45f),groundsheet,false);
                    foreach(float side in new[]{-1f,1f})
                    {
                        var roof=Box("Tent pitched interior liner",center+new Vector3(side*1.42f,2.47f,0),new Vector3(3.16f,.09f,5.55f),
                            FestivalArtView.MaterialFor(side<0?"CanvasCream":"CanvasRose"),false);
                        roof.transform.localRotation=Quaternion.Euler(0,0,side<0?25:-25);
                        Box("Tent interior guy tape",center+new Vector3(side*2.34f,1.59f,0),new Vector3(.08f,.07f,5.30f),gold,false);
                        foreach(float seamZ in new[]{-1.62f,1.62f})
                        {
                            var seam=Box("Tent liner stitched panel join",center+new Vector3(side*1.42f,2.49f,seamZ),new Vector3(3.12f,.025f,.045f),
                                FestivalArtView.MaterialFor("CanvasGold"),false);
                            seam.transform.localRotation=roof.transform.localRotation;
                        }
                    }
                    foreach(float x in new[]{-1.15f,1.15f})
                    {
                        Box("Tent sleeping mat",center+new Vector3(x,.14f,.1f),new Vector3(1.3f,.055f,3.48f),groundsheet,false);
                        SoftProp("Tent padded sleeping bag",center+new Vector3(x,.30f,.05f),new Vector3(1.05f,1.48f,.22f),
                            x<0?FestivalArtView.MaterialFor("CanvasMint"):FestivalArtView.MaterialFor("CanvasGold"),Quaternion.Euler(90,0,0));
                        SoftProp("Tent stuffed pillow",center+new Vector3(x,.42f,1.30f),new Vector3(.85f,.30f,.18f),
                            FestivalArtView.MaterialFor("CanvasCream"),Quaternion.Euler(90,0,0));
                        Box("Tent folded blanket",center+new Vector3(x,.39f,-1.04f),new Vector3(.88f,.06f,.38f),
                            x<0?FestivalArtView.MaterialFor("CanvasRose"):FestivalArtView.MaterialFor("CanvasMint"),false);
                    }
                    Box("Tent hanging lantern cord",center+new Vector3(0,2.75f,1.9f),new Vector3(.035f,.40f,.035f),groundsheet,false);
                    Box("Tent tiny lantern shade",center+new Vector3(0,2.49f,1.9f),new Vector3(.52f,.15f,.52f),groundsheet,false);
                    Box("Tent tiny lantern",center+new Vector3(0,2.34f,1.9f),new Vector3(.30f,.27f,.30f),gold,false);
                    Box("Tent rear organizer rail",center+new Vector3(-1.92f,1.68f,2.75f),new Vector3(1.15f,.07f,.06f),groundsheet,false);
                    foreach(float x in new[]{-2.25f,-1.59f})
                        Box("Tent canvas wall pocket",center+new Vector3(x,1.30f,2.72f),new Vector3(.49f,.53f,.10f),
                            FestivalArtView.MaterialFor("CanvasGold"),false);
                    Box("Tent camp cooler",center+new Vector3(0,.37f,2.1f),new Vector3(.85f,.65f,.7f),artMaterials["Blue"],false);
                    campAnticProps[kind]=Box("Tent cooler lid",center+new Vector3(0,.72f,2.1f),new Vector3(.9f,.08f,.74f),artMaterials["Cream"],false).transform;
                    Box("Tent zipper line",center+new Vector3(0,1.5f,2.78f),new Vector3(.05f,2.7f,.05f),gold,false);
                }
                else
                {
                    Box("Porta molded wall panel",center+new Vector3(0,1.53f,2.78f),new Vector3(4.95f,2.61f,.05f),artMaterials["Blue"],false);
                    Box("Porta seat",center+new Vector3(0,.55f,1.7f),new Vector3(1.15f,1.1f,.9f),artMaterials["Cream"],false);
                    campAnticProps[kind]=Box("Porta lid",center+new Vector3(0,1.15f,2.13f),new Vector3(1.23f,1.15f,.1f),artMaterials["Cream"],false).transform;
                    Box("Porta mirror",center+new Vector3(0,1.9f,2.75f),new Vector3(1.2f,.9f,.08f),artMaterials["Glass"],false);
                    Box("Emergency paper roll",center+new Vector3(1.9f,1.1f,.4f),new Vector3(.5f,.5f,.5f),artMaterials["Cream"],false);
                    Box("Porta occupied sign",center+new Vector3(0,2.55f,2.68f),new Vector3(.6f,.22f,.1f),rose,false);
                    Box("Porta coat hook",center+new Vector3(-1.9f,1.81f,.36f),new Vector3(.22f,.24f,.16f),gold,false);
                    Box("Porta hand sanitizer",center+new Vector3(1.8f,1.27f,1.12f),new Vector3(.26f,.39f,.22f),mint,false);
                }
                room.SetActive(false);
            }
            owned=previous;
        }
        public static Vector3 CampInteriorPosition(string kind)=>new Vector3(CampFeatures.InteriorX,0,CampFeatures.InteriorZ(kind));
        public void SetInterior(string siteId)
        {
            siteId=siteId??"";
            if(visibleInterior==siteId)return;
            if(campAnticTarget!=null)campAnticTarget.localPosition=campAnticRest;
            visibleInterior=siteId;
            var site=CampFeatures.Find(siteId);
            string kind=site?.Kind??"";
            visibleInteriorKind=kind;
            foreach(var entry in campInteriors)entry.Value.SetActive(entry.Key==kind);
            if(site!=null&&campInteriors.TryGetValue(kind,out var room))
            {
                var original=CampInteriorPosition(kind);
                room.transform.localPosition=new Vector3(CampFeatures.InteriorSlotX(site)-original.x,
                    0,CampFeatures.InteriorSlotZ(site)-original.z);
            }
            campAnticTarget=campAnticProps.TryGetValue(kind,out var prop)?prop:null;
            campAnticRest=campAnticTarget==null?Vector3.zero:campAnticTarget.localPosition;
            campAnticArmed=false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("WorldLifecycle","interior","site="+(siteId==""?"outside":siteId)+" kind="+kind);
#endif
        }
        public void SetCampAntics(int count)
        {
            if(!campAnticArmed){displayedCampAntics=count;campAnticArmed=true;return;}
            if(count<=displayedCampAntics){displayedCampAntics=count;return;}
            displayedCampAntics=count;campAnticUntil=Time.time+1.1f;
        }
        private static Transform ShopDisplayRoot(string name,Transform parent)
        {
            var root=new GameObject(name).transform;root.SetParent(parent,false);return root;
        }
        public void UpdateShop(RoundState state)
        {
            if(state==null||campShopRoot==null||marketShopRoot==null)return;
            if(displayedRound!=state.RoundId)
            {
                displayedRound=state.RoundId;
                ClearShop(campShopRoot,campProps,campTags);ClearShop(marketShopRoot,marketProps,marketTags);
                for(int i=0;i<state.VendorOffers.Count;i++)
                {
                    BuildShopItem(campShopRoot,campProps,campTags,state.VendorOffers[i],i,true);
                    BuildShopItem(marketShopRoot,marketProps,marketTags,state.VendorOffers[i],i,false);
                }
            }
            for(int i=0;i<state.VendorOffers.Count&&i<campProps.Count;i++)
            {
                var item=Catalog.FindItem(state.VendorOffers[i]);var stock=state.ShopStock.Find(s=>s.ItemId==item.Id);
                int campCount=stock?.CampAvailable??0,marketCount=stock?.MarketAvailable??0;
                ShowCopies(campProps[i],campCount);
                ShowCopies(marketProps[i],marketCount);
                string campText=Catalog.ShopTag(item.Id)+"\n$"+item.Price+"   "+(campCount>0?campCount+" LEFT":"SOLD OUT");
                string marketText=Catalog.ShopTag(item.Id)+"\n$"+item.Price+"   "+(marketCount>0?marketCount+" LEFT":"SOLD OUT");
                if(campTags[i].text!=campText)campTags[i].text=campText;
                if(marketTags[i].text!=marketText)marketTags[i].text=marketText;
            }
        }
        private static void ClearShop(Transform root,List<GameObject> props,List<TextMesh> tags)
        {
            foreach(Transform child in root)Destroy(child.gameObject);
            props.Clear();tags.Clear();
        }
        private static void ShowCopies(GameObject group,int available)
        {
            if(group==null)return;
            for(int n=0;n<group.transform.childCount;n++)group.transform.GetChild(n).gameObject.SetActive(n<available);
        }
        private void BuildShopItem(Transform root,List<GameObject> props,List<TextMesh> tags,string id,int index,bool atCamp)
        {
            var point=Catalog.ShopPoint(atCamp,index);
            var holder=new GameObject("Shop item "+id).transform;holder.SetParent(root,false);
            float row=index/4, y=row==0?1.06f:1.95f;
            holder.localPosition=new Vector3(point.X,y,atCamp?9.0f:index<4?-21.15f:-20.05f);
            string resource=FestivalSession.DropModel(id);
            var copies=new GameObject("Individual shelf copies");copies.transform.SetParent(holder,false);
            int count=Catalog.ShopCopies(id,8);
            for(int n=0;n<count;n++)
            {
                var prop=resource==null?null:FestivalArtView.Create(copies.transform,resource);
                if(prop==null)continue;
                prop.name=id+" copy "+(n+1);
                prop.transform.localPosition=new Vector3((n-(count-1)*.5f)*.31f,.12f,0);
                prop.transform.localScale=Vector3.one*(count==1?.43f:.29f);
            }
            props.Add(copies);
            var card=GameObject.CreatePrimitive(PrimitiveType.Cube);card.name="Painted price tag";card.transform.SetParent(holder,false);
            card.transform.localPosition=new Vector3(0,index<4?-.47f:-.39f,atCamp?-.48f:index<4?-.65f:-.63f);card.transform.localScale=new Vector3(.96f,.26f,.05f);
            card.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor("Dark");
            card.GetComponent<Collider>().enabled=false;Destroy(card.GetComponent<Collider>());
            var edge=GameObject.CreatePrimitive(PrimitiveType.Cube);edge.name="Price tag accent";edge.transform.SetParent(holder,false);
            edge.transform.localPosition=card.transform.localPosition+new Vector3(0,.13f,-.003f);edge.transform.localScale=new Vector3(.96f,.028f,.06f);
            edge.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor("Gold");
            edge.GetComponent<Collider>().enabled=false;Destroy(edge.GetComponent<Collider>());
            var label=new GameObject("Name and price");label.transform.SetParent(holder,false);
            label.transform.localPosition=card.transform.localPosition+new Vector3(0,0,-.038f);
            var text=label.AddComponent<TextMesh>();text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=50;text.characterSize=.013f;text.color=new Color(.96f,.94f,.84f);
            if(worldFont!=null){text.font=worldFont;text.GetComponent<MeshRenderer>().sharedMaterial=worldFont.material;}
            tags.Add(text);
        }
        public void SetPhase(string phase)
        {
            if(camp==null||owned==null||visiblePhase==phase)return;
            visiblePhase=phase;
            bool festival=phase=="Playing"||phase=="Results";
            camp.gameObject.SetActive(!festival);
            owned.gameObject.SetActive(festival);
            RenderSettings.sun=festival?festivalSun:campSun;
            Physics.SyncTransforms();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("WorldLifecycle","phase","space="+(festival?"festival":"campsite")+" camp_navigation="+CampNavigationReady+" festival_navigation="+NavigationReady);
#endif
        }
        private void Update()
        {
            float time=Time.time;
            if(campAnticTarget!=null)
            {
                float remaining=Mathf.Clamp01((campAnticUntil-time)/1.1f);
                float bounce=Mathf.Abs(Mathf.Sin(time*17f))*remaining;
                campAnticTarget.localPosition=campAnticRest+Vector3.up*bounce*(visibleInteriorKind=="Potty" ? .36f : .22f);
            }
            var view=FestivalCharacter.ViewTransform;
            if(view!=null)
            {
                foreach(var sign in worldSigns)
                    if(sign!=null)sign.GetComponent<Renderer>().enabled=Vector3.SqrMagnitude(view.position-sign.transform.position)<
                        (sign.text=="CREW STASH"?64f:sign.text=="AFTER HOURS"?1600f:sign.text=="NIGHT MARKET"?484f:225f);
                foreach(var tag in campTags)
                    if(tag!=null)tag.GetComponent<Renderer>().enabled=Vector3.SqrMagnitude(view.position-tag.transform.position)<64f;
                foreach(var tag in marketTags)
                    if(tag!=null)tag.GetComponent<Renderer>().enabled=Vector3.SqrMagnitude(view.position-tag.transform.position)<64f;
            }
            for(int i=0;i<stageLights.Count;i++)
            {
                var light=stageLights[i];if(light==null)continue;
                var target=new Vector3(Mathf.Sin(time*.65f+i*1.7f)*7,.2f,17+Mathf.Cos(time*.55f+i)*2);
                light.transform.rotation=Quaternion.LookRotation(target-light.transform.position);
            }
        }
        private Material Material(Color color)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            var template=Resources.Load<Material>("FestivalLit");
            var result=template!=null?new Material(template):new Material(shader);result.color=color;result.enableInstancing=true;
            if(result.HasProperty("_Smoothness"))result.SetFloat("_Smoothness",.16f);
            materials.Add(result);return result;
        }
        private Material Glow(Color color,float intensity)
        {
            var result=Material(color);
            if(result.HasProperty("_EmissionColor"))
            {
                result.EnableKeyword("_EMISSION");
                result.SetColor("_EmissionColor",color*intensity);
            }
            return result;
        }
        private GameObject Box(string label,Vector3 position,Vector3 scale,Material material,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=label;go.transform.SetParent(owned,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid){var c=go.GetComponent<Collider>();c.enabled=false;Dispose(c);}return go;
        }
        private GameObject SoftProp(string label,Vector3 position,Vector3 scale,Material material,Quaternion rotation)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name=label;go.transform.SetParent(owned,false);
            go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            return go;
        }
        private void DistantRise(string label,float innerRadius,float crestRadius,float outerRadius,Material material)
        {
            const int steps=128;
            var vertices=new Vector3[(steps+1)*3];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[steps*12];
            for(int ring=0;ring<3;ring++)for(int i=0;i<=steps;i++)
            {
                float angle=i*Mathf.PI*2/steps;
                float radius=ring==0?innerRadius:ring==1?crestRadius:outerRadius;
                float crest=3.8f+1.05f*Mathf.Sin(angle*3+.4f)
                    +.72f*Mathf.Sin(angle*7-1.2f)+.32f*Mathf.Sin(angle*13+2.1f);
                vertices[ring*(steps+1)+i]=new Vector3(Mathf.Cos(angle)*radius,
                    ring==1?crest:-.025f,Mathf.Sin(angle)*radius);
                uv[ring*(steps+1)+i]=new Vector2(i/(float)steps*18,ring*2f);
            }
            for(int ring=0;ring<2;ring++)for(int i=0;i<steps;i++)
            {
                int a=ring*(steps+1)+i,b=a+1,c=(ring+1)*(steps+1)+i,d=c+1;
                int offset=(ring*steps+i)*6;
                triangles[offset]=a;triangles[offset+1]=b;triangles[offset+2]=c;
                triangles[offset+3]=b;triangles[offset+4]=d;triangles[offset+5]=c;
            }
            var mesh=new Mesh{name=label};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateBounds();generatedMeshes.Add(mesh);
            var go=new GameObject(label);go.transform.SetParent(owned,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        private static float PathHalfWidthAt(float z,int sideIndex,float halfWidth)
        {
            // Change the outline slowly so the trail reads as foot-worn earth.
            return halfWidth-.12f+.13f*Mathf.Sin(z*.37f+sideIndex*1.7f)
                +.06f*Mathf.Sin(z*1.19f+sideIndex*2.3f);
        }
        private void WornMainPath(string label,float halfWidth,float startZ,float endZ,Material material)
        {
            const int steps=116;
            var vertices=new Vector3[(steps+1)*2];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[steps*6];
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps,z=Mathf.Lerp(startZ,endZ,t);
                vertices[i*2]=new Vector3(-PathHalfWidthAt(z,0,halfWidth),.027f,z);
                vertices[i*2+1]=new Vector3(PathHalfWidthAt(z,1,halfWidth),.027f,z);
                uv[i*2]=new Vector2(0,t);
                uv[i*2+1]=new Vector2(1,t);
                if(i==steps)continue;
                int a=i*2,o=i*6;
                triangles[o]=a;triangles[o+1]=a+2;triangles[o+2]=a+1;
                triangles[o+3]=a+1;triangles[o+4]=a+2;triangles[o+5]=a+3;
            }
            var mesh=new Mesh{name=label};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateBounds();generatedMeshes.Add(mesh);
            var go=new GameObject(label);go.transform.SetParent(owned,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        private void GrassBanks(string label,Material material)
        {
            // One visual mesh replaces hundreds of individual grass renderers.
            // All strips are outside the main path and attraction clearances.
            var vertices=new List<Vector3>(5000);
            var uv=new List<Vector2>(5000);
            var triangles=new List<int>(7500);
            for(int i=0;i<420;i++)
            {
                float x=-34f+((i*97)%683)/10f;
                float z=-32f+((i*151)%627)/10f;
                if(Mathf.Abs(x)<4.7f||z>22f&&Mathf.Abs(x)<17f||
                    z> -27f&&z< -15f&&x< -10f||z> -24f&&z< -13f&&x>19f)continue;
                float height=.18f+.035f*(i%5);
                for(int blade=0;blade<3;blade++)
                {
                    float angle=(i*43+blade*67)*Mathf.Deg2Rad;
                    float dx=Mathf.Cos(angle),dz=Mathf.Sin(angle);
                    float width=.065f+.015f*(i%3);
                    int a=vertices.Count;
                    vertices.Add(new Vector3(x-dx*width,.018f,z-dz*width));
                    vertices.Add(new Vector3(x+dx*width,.018f,z+dz*width));
                    vertices.Add(new Vector3(x+dz*.09f,height,z-dx*.09f));
                    uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(.5f,1));
                    triangles.Add(a);triangles.Add(a+2);triangles.Add(a+1);
                    triangles.Add(a+1);triangles.Add(a+2);triangles.Add(a);
                }
            }
            var mesh=new Mesh{name=label};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
            mesh.SetTriangles(triangles,0);
            // Both faces share positions; calculating normals would cancel them
            // and turn the tiny cards black under the directional dusk light.
            var normals=new Vector3[vertices.Count];
            for(int i=0;i<normals.Length;i++)normals[i]=Vector3.up;
            mesh.normals=normals;mesh.RecalculateBounds();generatedMeshes.Add(mesh);
            var go=new GameObject(label);go.transform.SetParent(owned,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("Environment","grass_banks","blades="+(vertices.Count/3));
#endif
        }
        private void WornPathEdges(string label,float halfWidth,float startZ,float endZ,Material material)
        {
            const int steps=58;
            var vertices=new Vector3[(steps+1)*4];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[steps*12];
            for(int sideIndex=0;sideIndex<2;sideIndex++)
            {
                float side=sideIndex==0?-1f:1f;
                int baseIndex=sideIndex*(steps+1)*2;
                for(int i=0;i<=steps;i++)
                {
                    float z=Mathf.Lerp(startZ,endZ,i/(float)steps);
                    float edge=PathHalfWidthAt(z,sideIndex,halfWidth);
                    float drift=.10f*Mathf.Sin(z*.83f+sideIndex*1.9f)
                        +.07f*Mathf.Sin(z*1.91f+sideIndex*.8f);
                    vertices[baseIndex+i*2]=new Vector3(side*(edge-.015f),.028f,z);
                    vertices[baseIndex+i*2+1]=new Vector3(side*(edge+.53f+drift),.012f,z);
                    uv[baseIndex+i*2]=new Vector2(0,z*.43f);
                    uv[baseIndex+i*2+1]=new Vector2(1,z*.43f);
                    if(i==steps)continue;
                    int offset=(sideIndex*steps+i)*6;
                    int a=baseIndex+i*2,b=a+1,c=a+2,d=a+3;
                    if(sideIndex==0)
                    {triangles[offset]=a;triangles[offset+1]=b;triangles[offset+2]=c;
                     triangles[offset+3]=b;triangles[offset+4]=d;triangles[offset+5]=c;}
                    else
                    {triangles[offset]=a;triangles[offset+1]=c;triangles[offset+2]=b;
                     triangles[offset+3]=b;triangles[offset+4]=c;triangles[offset+5]=d;}
                }
            }
            var mesh=new Mesh{name=label};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateBounds();generatedMeshes.Add(mesh);
            var go=new GameObject(label);go.transform.SetParent(owned,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        private GameObject ProxyBox(string label,Vector3 position,Vector3 scale,Material material)
        {
            var go=Box(label,position,scale,material);
            go.GetComponent<Renderer>().enabled=false;
            return go;
        }
        private GameObject Visual(string resource,Vector3 position)
        {
            var prefab=Resources.Load<GameObject>(resource);
            if(prefab==null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[Festival.Art] Missing Blender world asset: "+resource);
#endif
                return null;
            }
            var go=Instantiate(prefab,owned);go.name=resource;go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.identity;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var name=renderer.name;int mark=name.LastIndexOf("__",System.StringComparison.Ordinal);
                if(mark>=0)
                {
                    var key=name.Substring(mark+2).Split('.')[0];
                    if(key=="Wood"||key=="Bark"||key=="Leaf"||key=="LeafWarm"||key=="Needle"||key=="Stone"||
                        key=="Rubber"||key=="AutoGlass"||key.StartsWith("Canvas",System.StringComparison.Ordinal)||
                        key.StartsWith("Paint",System.StringComparison.Ordinal)||
                        key.StartsWith("StageGlow",System.StringComparison.Ordinal))
                    {
                        var surface=FestivalArtView.MaterialFor(key);
                        if(surface!=null)renderer.sharedMaterial=surface;
                    }
                    else if(artMaterials.TryGetValue(key,out var tint))renderer.sharedMaterial=tint;
                }
                renderer.shadowCastingMode=resource.StartsWith("FestivalTree")||resource=="FestivalGroveDetail"?ShadowCastingMode.Off:ShadowCastingMode.On;
            }
            foreach(var collider in go.GetComponentsInChildren<Collider>()){collider.enabled=false;Dispose(collider);}
            return go;
        }
        private void Booth(string name,Vector3 p,Material accent,Material wood,string label)
        {
            if(name=="Medical"||name=="Holding")
            {
                var center=p+new Vector3(0,0,3);
                ProxyBox(name+" floor",center+new Vector3(0,.12f,0),new Vector3(6,.24f,5),wood);
                ProxyBox(name+" back",center+new Vector3(0,1.5f,2.45f),new Vector3(6,3,.2f),wood);
                ProxyBox(name+" west",center+new Vector3(-3,1.5f,0),new Vector3(.2f,3,5),wood);
                ProxyBox(name+" east",center+new Vector3(3,1.5f,0),new Vector3(.2f,3,5),wood);
            }
            else
            {
                var backing=Box(name,p+new Vector3(0,1,3),new Vector3(5,2,1.3f),wood);
                if(name=="Vendor")backing.GetComponent<Renderer>().enabled=false;
                var canopy=Box(name+" canopy",p+new Vector3(0,3,3),new Vector3(6,.25f,4),accent);
                if(name=="Vendor")canopy.GetComponent<Renderer>().enabled=false;
            }
            bool locationEntrance=name=="Medical"||name=="Holding";
            Sign(label,p+(locationEntrance?new Vector3(0,3.02f,.1f):new Vector3(0,2.4f,2.2f)),
                accent,locationEntrance ? .12f : .055f);
        }
        private void Totem(string label,Vector3 position,Material accent,Material wood,Material ink)
        {
            ProxyBox(label+" plinth",position+new Vector3(0,.25f,0),new Vector3(1.3f,.5f,1),wood);
            Visual(label=="SUN"?"FestivalSun":"FestivalMoon",position);
            Sign(label,position+new Vector3(0,2.95f,-.65f),accent,.075f);
        }
        private void Sign(string label,Vector3 position,Material material,float size,float yaw=0)
        {
            var go=new GameObject(label+" sign");go.transform.SetParent(owned,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            // Boogaloo glyphs are wider than the TextMesh character-size unit.
            // Leave real painted board behind the entire legend at every angle.
            float width=Mathf.Max(.98f,label.Length*size*1.45f+.50f);
            float height=Mathf.Max(.29f,size*2.25f+.13f);
            var border=GameObject.CreatePrimitive(PrimitiveType.Cube);border.name="Label bubble rim";border.transform.SetParent(go.transform,false);
            border.transform.localPosition=new Vector3(0,0,.025f);border.transform.localScale=new Vector3(width+.075f,height+.075f,.045f);
            border.GetComponent<Renderer>().sharedMaterial=material;var borderCollider=border.GetComponent<Collider>();borderCollider.enabled=false;Dispose(borderCollider);
            var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Label bubble";backing.transform.SetParent(go.transform,false);
            backing.transform.localPosition=new Vector3(0,0,-.012f);backing.transform.localScale=new Vector3(width,height,.045f);
            backing.GetComponent<Renderer>().sharedMaterial=artMaterials["Dark"];var backingCollider=backing.GetComponent<Collider>();backingCollider.enabled=false;Dispose(backingCollider);
            var face=new GameObject("Text");face.transform.SetParent(go.transform,false);face.transform.localPosition=new Vector3(0,0,-.050f);
            var text=face.AddComponent<TextMesh>();text.text=label;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=size;text.fontSize=48;text.color=new Color(.96f,.94f,.84f);
            if(worldFont!=null){text.font=worldFont;text.GetComponent<MeshRenderer>().sharedMaterial=worldFont.material;}
            worldSigns.Add(text);
        }
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        private void OnDestroy()
        {
            if(surface!=null){surface.RemoveData();if(surface.navMeshData!=null)Dispose(surface.navMeshData);}
            if(campSurface!=null){campSurface.RemoveData();if(campSurface.navMeshData!=null)Dispose(campSurface.navMeshData);}
            foreach(var material in materials)if(material!=null)Dispose(material);
            foreach(var mesh in generatedMeshes)if(mesh!=null)Dispose(mesh);
            if(duskProfile!=null)Dispose(duskProfile);
        }
    }
}
