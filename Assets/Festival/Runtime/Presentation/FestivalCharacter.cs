using System.Collections.Generic;
using Festival.Core;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Visual comedy only: no animation moves collision or changes authority.</summary>
    public sealed class FestivalCharacter : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static int DevelopmentAnimationUpdates;
        string loggedPerformancePose;
#endif
        public static Transform ViewTransform;
        public bool AlwaysHighDetail;
        public bool UsesDistantMesh { get; private set; }
        sealed class DetailPart
        {
            public SkinnedMeshRenderer Renderer;
            public Mesh Detailed,Distant;
        }
        static Dictionary<string,Mesh> distantMeshes;
        readonly List<DetailPart> detailParts=new List<DetailPart>();
        string fitName;
        readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
        readonly Dictionary<string,Quaternion> rest=new Dictionary<string,Quaternion>();
        readonly Dictionary<string,Quaternion> targets=new Dictionary<string,Quaternion>();
        FestivalFootPlant footPlant;
        FestivalDjHandContact djHandContact;
        Vector3 previous;
        float speed,phase,walkCycle;
        float previousYaw,turnRate,accelerationLean;
        bool hasFacingSample;
        internal float MotionPhase => phase;
        bool hasPrevious;
        int danceStyle;
        public int DanceStyle=>danceStyle;
        int idleStyle;
        string motionKey="";
        float poseStart,transitionEnd,transitionLength=.3f;
        readonly Dictionary<string,Quaternion> transitionFrom=new Dictionary<string,Quaternion>();
        readonly Quaternion[] clipPose=new Quaternion[15];
        Vector3 restHips;
        static float DanceAngularSpeed(int style) => style==0?8.8f:style==1?7.6f:8.3f;
        // TRIP-4: someone lying on the grass watching the clouds lies on their back with their hips this high, in metres at the
        // actor's own scale.
        const float LieHipHeight=.12f;
        int latestDanceDirection=-1;
        float latestDanceStepTime=-100;
        public string Pose="Idle";
        public Transform DjConsole { get; set; }
        public bool Crowd;
        public bool AmbientCrowd;
        public float Threat;
        /// <summary>SPIN-1 hook: a scripted beat the spinner plays over the session's pose. "TakeDose" acts the
        /// existing Consume clip; "DoseReaction" blends in the Panic clip and a head shake by BeatStrength (0-1, the dose's size).</summary>
        public string Beat="";
        public float BeatStrength;
        public FestivalAppearance Appearance { get; private set; }
        public float HeightScale { get; private set; }=1;
        public Vector3 ShapeScale => Appearance.Scale;
        Material ownedMaterial;
        Material garmentMaterial,skinMaterial,hairMaterial,gearMaterial,lensMaterial,scleraMaterial;
        Color baseTint;
        float lastAppliedThreat=-1;
        float lastAnimationTime;
        Material eyeMaterial;
        Texture2D ownedPalette,garmentPalette;
        Renderer eyeRenderer;
        Renderer spoonRenderer;
        SkinnedMeshRenderer faceRenderer;
        int blinkIndex=-1;
        int intoxicatedEyesIndex=-1;
        public bool HighlyIntoxicated { get; private set; }
        public bool RedEyes { get; private set; }
        FestivalPoiRig poiLeft,poiRight;
        string carriedPoi="",poiPairItem="";
        GameObject equippedProp;
        string equippedId="",requestedItem="",offeredItem="",receivedItem="";
        bool exchanging;
        Vector3 exchangeTarget;
        float exchangeWeight,receiptAt=-10;
        int receiptSequence;
        SkinnedMeshRenderer bodyRenderer;
        int gripLeft=-1,gripRight=-1,smileIndex=-1,concernIndex=-1;
        public void SetExchange(bool active,string item,Vector3 target)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(active!=exchanging)DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","exchange","actor="+name+" active="+active);
#endif
            exchanging=active;offeredItem=active?item:"";if(active)exchangeTarget=target;
            RefreshEquipment();
        }
        public void SetReceipt(int sequence,string item,float age)
        {
            if(sequence==receiptSequence)return;
            receiptSequence=sequence;
            if(age<0||age>1||string.IsNullOrEmpty(item))return;
            receivedItem=item;receiptAt=Time.time-age;RefreshEquipment();
        }
        public static FestivalCharacter Create(Transform parent,string name,Color tint,string role="Attendee")
        {
            var asset=Resources.Load<GameObject>("FestivalCharacter");
            var go=asset!=null?Instantiate(asset,parent):new GameObject(name);
            go.name=name;go.transform.SetParent(parent,false);
            var actor=go.AddComponent<FestivalCharacter>();
            actor.Appearance=FestivalAppearance.For(name,role);
            actor.fitName="Fit_"+actor.Appearance.Gender+"_"+actor.Appearance.Shape;
            if(distantMeshes==null)
            {
                distantMeshes=new Dictionary<string,Mesh>();
                var distant=Resources.Load<GameObject>("FestivalCharacterDistant");
                if(distant!=null)foreach(var renderer in distant.GetComponentsInChildren<SkinnedMeshRenderer>(true))distantMeshes[renderer.name]=renderer.sharedMesh;
            }
            var template=Resources.Load<Material>("FestivalLit");
            if(template==null)template=new Material(Shader.Find("Standard"));
            actor.HeightScale=actor.Appearance.Scale.y;
            actor.danceStyle=FestivalAppearance.Pick(name,"dance",3);
            actor.ownedPalette=actor.Appearance.CreatePalette();
            actor.ownedMaterial=new Material(template){name="Festival actor palette"};
            actor.ownedMaterial.mainTexture=actor.ownedPalette;
            actor.baseTint=Color.Lerp(Color.white,tint,.04f);
            actor.ownedMaterial.color=actor.baseTint;
            actor.ownedMaterial.SetFloat("_Smoothness",.10f);
            actor.garmentPalette=actor.Appearance.CreatePalette(true);
            actor.garmentMaterial=new Material(actor.ownedMaterial){name="Festival tonal fabric",mainTexture=actor.garmentPalette};
            actor.skinMaterial=new Material(actor.ownedMaterial){name="Festival skin"};actor.skinMaterial.SetFloat("_Smoothness",.24f);
            actor.hairMaterial=new Material(actor.ownedMaterial){name="Festival hair"};actor.hairMaterial.SetFloat("_Smoothness",.18f);
            actor.gearMaterial=new Material(actor.ownedMaterial){name="Festival equipment"};actor.gearMaterial.SetFloat("_Smoothness",.30f);
            actor.lensMaterial=new Material(actor.ownedMaterial){name="Festival eyewear"};actor.lensMaterial.SetFloat("_Smoothness",.68f);
            actor.scleraMaterial=new Material(template){name="Festival eye whites",mainTexture=Texture2D.whiteTexture,color=new Color(.98f,.95f,.88f)};
            actor.scleraMaterial.SetFloat("_Smoothness",.22f);
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.name=="EyeGlow")
                {
                    actor.eyeRenderer=renderer;
                    actor.eyeMaterial=new Material(template){name="Festival escalating eyes"};
                    actor.eyeMaterial.mainTexture=Texture2D.whiteTexture;
                    renderer.sharedMaterial=actor.eyeMaterial;renderer.enabled=false;
                }
                else
                {
                    // Unity imports this FBX with eye whites in submesh 0 and
                    // palette colored iris, pupils, brows and lips in submesh 1.
                    if(renderer.name.StartsWith("Face_"))renderer.sharedMaterials=new[]{actor.scleraMaterial,actor.skinMaterial};
                    else renderer.sharedMaterial=actor.SurfaceFor(renderer.name);
                    renderer.enabled=actor.KeepMesh(renderer.name);
                    if(renderer.name=="Equipment_LittleSpoon")actor.spoonRenderer=renderer;
                }
                if(renderer is SkinnedMeshRenderer skin && !renderer.name.StartsWith("Body_"))
                {
                    for(int shape=0;shape<skin.sharedMesh.blendShapeCount;shape++)skin.SetBlendShapeWeight(shape,0);
                    if(renderer.enabled&&renderer.name.StartsWith("Face_")){actor.faceRenderer=skin;actor.blinkIndex=skin.sharedMesh.GetBlendShapeIndex("Blink");actor.intoxicatedEyesIndex=skin.sharedMesh.GetBlendShapeIndex("WideIntoxicatedEyes");}
                    string fit="Fit_"+actor.Appearance.Gender+"_"+actor.Appearance.Shape;
                    int index=skin.sharedMesh.GetBlendShapeIndex(fit);
                    if(index>=0)skin.SetBlendShapeWeight(index,100);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    else Debug.LogWarning("[Festival.Fit] Missing "+fit+" on "+renderer.name);
#endif
                }
                if(renderer is SkinnedMeshRenderer detail&&(renderer.enabled||renderer.name=="EyeGlow"||renderer.name=="Equipment_LittleSpoon")&&distantMeshes.TryGetValue(renderer.name,out var distantMesh))
                    actor.detailParts.Add(new DetailPart{Renderer=detail,Detailed=detail.sharedMesh,Distant=distantMesh});
            }
            var animatedBones=new HashSet<string>{"Hips","Spine","Head","ArmL","ArmR","ForearmL","ForearmR","HandL","HandR","LegL","LegR","ShinL","ShinR","FootL","FootR"};
            // Never reset the presentation root: its facing belongs to the session.
            foreach(var t in go.GetComponentsInChildren<Transform>())if(animatedBones.Contains(t.name)&&!actor.bones.ContainsKey(t.name)){actor.bones[t.name]=t;actor.rest[t.name]=t.localRotation;}
            if(actor.bones.TryGetValue("Hips",out var pelvis))actor.restHips=pelvis.localPosition;
            actor.idleStyle=FestivalAppearance.Pick(name,"idle",3);
            if(actor.bones.Count==15)
                actor.footPlant=new FestivalFootPlant(actor.transform,actor.bones["Hips"],
                    actor.bones["LegL"],actor.bones["ShinL"],actor.bones["FootL"],
                    actor.bones["LegR"],actor.bones["ShinR"],actor.bones["FootR"]);
            foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                if(skin.enabled&&skin.name.StartsWith("Body_"))actor.bodyRenderer=skin;
            if(actor.bodyRenderer!=null)for(int i=0;i<actor.bodyRenderer.sharedMesh.blendShapeCount;i++)
            {
                var key=actor.bodyRenderer.sharedMesh.GetBlendShapeName(i);
                if(key.EndsWith("GripL"))actor.gripLeft=i;if(key.EndsWith("GripR"))actor.gripRight=i;
            }
            if(actor.faceRenderer!=null)for(int i=0;i<actor.faceRenderer.sharedMesh.blendShapeCount;i++)
            {
                var key=actor.faceRenderer.sharedMesh.GetBlendShapeName(i);
                if(key.EndsWith("Smile"))actor.smileIndex=i;if(key.EndsWith("Concern"))actor.concernIndex=i;
            }
            actor.phase=FestivalAppearance.Pick(name,"phase",100)*.137f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(actor.bones.Count<15 || actor.eyeRenderer==null)Debug.LogWarning("[Festival.Art] Modular character asset is missing bones or eye mesh: "+name);
            if(actor.blinkIndex<0||actor.detailParts.Count==0)Debug.LogWarning("[Festival.Art] Facial animation or distant character meshes are missing: "+name);
            if(!name.StartsWith("Cosmetic dancer"))Debug.Log($"[Festival.Art] {name} body={actor.Appearance.Gender}/{actor.Appearance.Shape} face={actor.Appearance.Face} head={actor.Appearance.Headgear} shades={actor.Appearance.Sunglasses} shirt={actor.Appearance.Shirt} pants={actor.Appearance.Pants} shoes={actor.Appearance.Shoes} beard={actor.Appearance.FacialHair} hair={actor.Appearance.Hairstyle}/{actor.Appearance.HairColor} accessory={actor.Appearance.Accessory} role={role}");
#endif
            return actor;
        }
        bool KeepMesh(string mesh)
        {
            var look=Appearance;
            if(mesh.StartsWith("Body_"))return mesh=="Body_"+look.Gender+"_"+look.Shape;
            if(mesh.StartsWith("Face_"))return mesh=="Face_"+look.Gender+"_"+look.Face;
            if(mesh.StartsWith("Shirt_"))return mesh=="Shirt_"+look.Shirt;
            if(mesh.StartsWith("Pants_"))return mesh=="Pants_"+look.Pants;
            if(mesh.StartsWith("Shoes_"))return mesh=="Shoes_"+look.Shoes;
            if(mesh.StartsWith("Headgear_"))return mesh=="Headgear_"+look.Headgear;
            if(mesh.StartsWith("Sunglasses_"))return mesh=="Sunglasses_"+look.Sunglasses;
            if(mesh.StartsWith("FacialHair_"))return mesh=="FacialHair_"+look.FacialHair;
            if(mesh.StartsWith("HairTop_"))return mesh=="HairTop_"+look.Hairstyle && look.Headgear<0;
            if(mesh=="HairUnderHat")return look.Headgear==2 || look.Headgear==3;
            if(mesh.StartsWith("Hairstyle_"))return mesh=="Hairstyle_"+look.Hairstyle;
            if(mesh.StartsWith("Accessory_"))return mesh=="Accessory_"+look.Accessory;
            if(mesh.StartsWith("Role_"))return mesh=="Role_"+look.Role;
            return false;
        }
        Material SurfaceFor(string mesh)
        {
            if(mesh.StartsWith("Shirt_")||mesh.StartsWith("Pants_"))return garmentMaterial;
            if(mesh.StartsWith("Body_")||mesh.StartsWith("Face_"))return skinMaterial;
            if(mesh.StartsWith("Hair")||mesh.StartsWith("FacialHair_"))return hairMaterial;
            if(mesh.StartsWith("Sunglasses_"))return lensMaterial;
            if(mesh.StartsWith("Equipment_"))return gearMaterial;
            return ownedMaterial;
        }
        public void SetLittleSpoon(bool worn)
        {
            if(spoonRenderer!=null)spoonRenderer.enabled=worn;
        }
        public void SetEquippedItem(string itemId)
        {
            requestedItem=itemId??"";RefreshEquipment();
        }
        void RefreshEquipment()
        {
            string itemId=exchanging?offeredItem:Time.time-receiptAt<.85f?receivedItem:requestedItem;
            if(equippedId==itemId)return;
            equippedId=itemId;
            if(equippedProp!=null)Destroy(equippedProp);
            equippedProp=null;carriedPoi="";
            if(itemId==""||itemId=="little_spoon"||!bones.TryGetValue("HandR",out var hand))return;
            // Poi are a pair; LateUpdate keeps one in each hand.
            if(itemId=="poi_led"||itemId=="poi_practice"){carriedPoi=itemId;return;}
            equippedProp=FestivalHeldItem.Create(hand,itemId,new Vector3(0,-.05f,0),false);
        }
        public void SetHighlyIntoxicated(bool value)
        {
            if(HighlyIntoxicated==value)return;
            HighlyIntoxicated=value;
            if(faceRenderer!=null)
            {
                if(blinkIndex>=0)faceRenderer.SetBlendShapeWeight(blinkIndex,0);
                if(intoxicatedEyesIndex>=0)faceRenderer.SetBlendShapeWeight(intoxicatedEyesIndex,value?100:0);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(value&&intoxicatedEyesIndex<0)Debug.LogWarning("[Festival.Art] Wide intoxicated eyes missing: "+name);
#endif
        }
        public void SetRedEyes(bool value)
        {
            if(RedEyes==value)return;
            RedEyes=value;
            if(scleraMaterial!=null)scleraMaterial.color=value?new Color(1f,.46f,.44f):new Color(.98f,.95f,.88f);
        }
        /// <summary>A visual response to a rhythm press; it never changes movement or scoring.</summary>
        public void PulseDanceStep(int direction)
        {
            if(direction<0||direction>3)return;
            latestDanceDirection=direction;
            latestDanceStepTime=Time.time;
        }
        void LateUpdate()
        {
            RefreshEquipment();
            float viewDistance=ViewTransform==null?0:Vector3.Distance(ViewTransform.position,transform.position);
            if(ViewTransform!=null)UpdateDetailForDistance(viewDistance);
            if(AmbientCrowd&&viewDistance>18f&&((Time.frameCount+Mathf.FloorToInt(phase*10))&1)!=0)return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentAnimationUpdates++;
#endif
            float animationDelta=lastAnimationTime<=0?Time.deltaTime:Mathf.Max(Time.time-lastAnimationTime,.001f);
            lastAnimationTime=Time.time;
            if(faceRenderer!=null&&blinkIndex>=0&&!HighlyIntoxicated)
            {
                float cycle=(Time.time+phase)%(3.6f+phase*.11f);
                faceRenderer.SetBlendShapeWeight(blinkIndex,cycle<.18f?Mathf.Sin(cycle/.18f*Mathf.PI)*100:0);
            }
            bool performingPoi=Pose=="Poi";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!AmbientCrowd&&loggedPerformancePose!=Pose)
            {
                loggedPerformancePose=Pose;
                if(performingPoi)DevelopmentDiagnostics.GraphicsEvent("Motion","poi_butterfly_enter","actor="+name);
                else if(Pose=="Dance")DevelopmentDiagnostics.GraphicsEvent("Motion","dance_enter",
                    "actor="+name+" style="+danceStyle+" angular_speed="+DanceAngularSpeed(danceStyle));
            }
#endif
            // Equipped poi hang from both hands, wheel beside them while walking and join a
            // dance as butterflies on its beat. Rebuild only when the poi type changes.
            bool carryingPoi=carriedPoi!=""&&!Crowd&&(Pose=="Idle"||Pose=="Walk"||Pose=="");
            bool dancingPoi=carriedPoi!=""&&Pose=="Dance";
            bool poiShown=performingPoi||carryingPoi||dancingPoi;
            string pair=carriedPoi!=""?carriedPoi:performingPoi?(poiPairItem==""?"poi_led":poiPairItem):"";
            if(pair!=""&&pair!=poiPairItem)
            {
                if(poiLeft!=null)Destroy(poiLeft.gameObject);
                if(poiRight!=null)Destroy(poiRight.gameObject);
                poiPairItem=pair;PreparePoi(pair=="poi_led");
            }
            float spinRate=dancingPoi?DanceAngularSpeed(danceStyle):9f;
            if(poiLeft!=null){poiLeft.gameObject.SetActive(poiShown);poiLeft.Spinning=performingPoi||dancingPoi;poiLeft.Carrying=carryingPoi&&speed>.4f;poiLeft.SpinRate=spinRate;}
            if(poiRight!=null){poiRight.gameObject.SetActive(poiShown);poiRight.Spinning=performingPoi||dancingPoi;poiRight.Carrying=carryingPoi&&speed>.4f;poiRight.SpinRate=spinRate;}
            var delta=hasPrevious?transform.position-previous:Vector3.zero;previous=transform.position;hasPrevious=true;
            float yaw=transform.eulerAngles.y;
            float yawChange=hasFacingSample?Mathf.DeltaAngle(previousYaw,yaw):0;
            previousYaw=yaw;hasFacingSample=true;
            float measuredTurn=delta.sqrMagnitude<.04f&&Mathf.Abs(yawChange)<75f
                ?Mathf.Clamp(yawChange/animationDelta,-240f,240f):0;
            turnRate=Mathf.Lerp(turnRate,measuredTurn,1-Mathf.Exp(-9f*animationDelta));
            float measuredSpeed=hasPrevious&&delta.magnitude<2?Mathf.Min(6,delta.magnitude/animationDelta):0;
            float previousSpeed=speed;
            speed=Mathf.Lerp(speed,measuredSpeed,1-Mathf.Exp(-12*animationDelta));
            float acceleration=Mathf.Clamp((speed-previousSpeed)/animationDelta,-5f,5f);
            accelerationLean=Mathf.Lerp(accelerationLean,acceleration,1-Mathf.Exp(-8f*animationDelta));
            // Stride and time on each foot follow speed and this actor's real leg
            // length, so a 4-6 m/s jog reads as quick steps rather than a blur.
            float leg=bones.Count==15?Vector3.Distance(bones["LegL"].position,bones["ShinL"].position)+
                Vector3.Distance(bones["ShinL"].position,bones["FootL"].position):.55f;
            var gait=FestivalGait.For(speed,leg);
            if(measuredSpeed>.12f)walkCycle+=delta.magnitude*(Mathf.PI*2/gait.Stride);
            float idleShift=0,still=0,look=0,nod=0,shift=0;
            float t=Time.time*4+phase,wave=Mathf.Sin(t),walk=Mathf.Sin(walkCycle);
            bool lying=Pose==FestivalSimulation.LieDownKind;
            if(equippedProp!=null)equippedProp.SetActive(Pose!="Poi"&&Pose!="Downed"&&Pose!="Spirit"&&!lying);
            foreach(var item in bones)targets[item.Key]=rest[item.Key];
            bool dance=Crowd||Pose=="Dance"||Pose=="Poi"||Pose=="Dj"||Pose=="Distracted";
            if(Pose=="Downed")
            {
                Aim("Hips",new Vector3(75,0,walk*4));Aim("Head",new Vector3(-35,0,12));
                Aim("ArmL",new Vector3(-85+walk*20,0,20));Aim("ArmR",new Vector3(-85-walk*20,0,-20));
                Aim("LegL",new Vector3(walk*12,0,10));Aim("LegR",new Vector3(-walk*12,0,-10));
            }
            else if(lying)
            {
                // TRIP-4: flat on their back, legs out ahead and hands behind the head, gazing up at the clouds. The hips sink to the
                // grass below, once the foot plant has had its say.
                Aim("Hips",new Vector3(-90,0,0));Aim("Spine",new Vector3(-3+Mathf.Sin(Time.time*1.1f+phase)*1.5f,0,0));Aim("Head",new Vector3(-10,0,0));
                Aim("ArmL",new Vector3(-155,0,-35));Aim("ArmR",new Vector3(-155,0,35));Aim("ForearmL",new Vector3(-115,0,0));Aim("ForearmR",new Vector3(-115,0,0));
                Aim("LegL",new Vector3(0,0,7));Aim("LegR",new Vector3(0,0,-7));
            }
            else if(dance)
            {
                // Footwork and torso accents share a beat, while each actor's
                // phase and style keep a crowd from moving in lockstep. Hips roll
                // over the supporting foot, and each style alternates two arm
                // patterns across longer phrases so neighbours rarely match.
                float beatTime=Time.time*DanceAngularSpeed(danceStyle)+phase;
                float feet=Mathf.Sin(beatTime),opposite=Mathf.Sin(beatTime+Mathf.PI);
                float landing=Mathf.Pow(Mathf.Max(0,Mathf.Cos(beatTime*2)),2);
                float sway=Mathf.Sin(beatTime*.5f);
                float phrase=Mathf.Sin(beatTime*.25f+phase*.37f);
                float alt=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.2f,.6f,Mathf.Sin(beatTime*.125f+phase*1.9f)));
                Aim("Hips",new Vector3(7+landing*6,sway*9,feet*13));
                Aim("Spine",new Vector3(-8-landing*4,-sway*5+phrase*6,-feet*15));
                Aim("Head",new Vector3(2-landing*5,Mathf.Sin(beatTime*.5f+1)*9-phrase*5,-feet*4));
                if(performingPoi)
                {
                    // The performer concentrates on the paired butterfly flow:
                    // relaxed knees, quiet hips and alternating crossed wrists.
                    float circle=Time.time*9f+phase;
                    float exchange=Mathf.Sin(circle);
                    Aim("Hips",new Vector3(4+landing*2,sway*4-exchange*3,feet*6));
                    Aim("Spine",new Vector3(-5,phrase*5+exchange*6,-feet*5));
                    Aim("Head",new Vector3(4,phrase*-6-exchange*4,-feet*3));
                    PoiArms(circle);
                    Aim("LegL",new Vector3(Mathf.Max(0,feet)*13,0,0));
                    Aim("LegR",new Vector3(Mathf.Max(0,opposite)*13,0,0));
                    Aim("ShinL",new Vector3(Mathf.Max(0,feet)*14,0,0));
                    Aim("ShinR",new Vector3(Mathf.Max(0,opposite)*14,0,0));
                    Aim("FootL",new Vector3(-Mathf.Max(0,feet)*27,0,0));
                    Aim("FootR",new Vector3(-Mathf.Max(0,opposite)*27,0,0));
                    Layer("Spine",new Vector3(0,Mathf.Sin(circle*.5f)*5,0));
                }
                else if(Pose=="Dj")
                {
                    // A resident DJ works the actual console rather than doing
                    // floor footwork on the raised stage. One hand alternates
                    // between platter and mixer while the other keeps time.
                    Aim("Hips",new Vector3(3,sway*4,feet*3));
                    Aim("Spine",new Vector3(10,-sway*6,-feet*5));
                    Aim("Head",new Vector3(12,Mathf.Sin(beatTime*.5f)*8,feet*3));
                    Aim("ArmL",new Vector3(-62+feet*8,0,-22));
                    Aim("ArmR",new Vector3(-64-opposite*10,0,22));
                    Aim("ForearmL",new Vector3(-58+opposite*10,0,12));
                    Aim("ForearmR",new Vector3(-57+feet*12,0,-12));
                    Aim("HandL",new Vector3(12+feet*13,0,0));
                    Aim("HandR",new Vector3(10+opposite*16,0,0));
                    Aim("LegL",new Vector3(2+feet*3,0,0));
                    Aim("LegR",new Vector3(2-feet*3,0,0));
                }
                else if(danceStyle==0)
                {
                    // Hakken: each knee rises on a separate beat while the
                    // elbows pump low. The head follows the next landing.
                    Aim("LegL",new Vector3(Mathf.Max(0,feet)*31-7,0,feet*4));
                    Aim("LegR",new Vector3(Mathf.Max(0,opposite)*31-7,0,opposite*4));
                    Aim("ShinL",new Vector3(Mathf.Max(0,feet)*25,0,0));
                    Aim("ShinR",new Vector3(Mathf.Max(0,opposite)*25,0,0));
                    Aim("FootL",new Vector3(6-Mathf.Max(0,feet)*56,0,0));
                    Aim("FootR",new Vector3(6-Mathf.Max(0,opposite)*56,0,0));
                    // Low elbow pumps; alternate phrases throw one fist overhead.
                    Aim("ArmL",new Vector3(-32-feet*19+phrase*5,0,-17+phrase*5));
                    Aim("ArmR",Vector3.Lerp(new Vector3(-32+feet*19-phrase*5,0,17+phrase*5),new Vector3(-150+landing*18,0,6),alt));
                    Aim("ForearmL",new Vector3(-62+landing*5,0,0));Aim("ForearmR",new Vector3(Mathf.Lerp(-62+landing*5,-18,alt),0,0));
                    Layer("Spine",new Vector3(landing*7,feet*8,0));
                }
                else if(danceStyle==1)
                {
                    // Shuffle: hips transfer sideways while a foot skims out;
                    // one arm drifts across the body instead of mirroring.
                    Aim("Hips",new Vector3(7+landing*3,sway*13,feet*13));
                    Aim("LegL",new Vector3(10+feet*21,0,7+Mathf.Max(0,sway)*12));
                    Aim("LegR",new Vector3(10-feet*21,0,-7-Mathf.Max(0,-sway)*12));
                    Aim("ShinL",new Vector3(Mathf.Max(0,feet)*16,0,0));
                    Aim("ShinR",new Vector3(Mathf.Max(0,opposite)*16,0,0));
                    Aim("FootL",new Vector3(-10-feet*20-Mathf.Max(0,feet)*16,0,0));
                    Aim("FootR",new Vector3(-10+feet*20-Mathf.Max(0,opposite)*16,0,0));
                    // Running-man arms that swing against the skimming foot, then a clap phrase.
                    Aim("ArmL",Vector3.Lerp(new Vector3(35*feet-20,0,-12),new Vector3(-95+landing*10,0,-18),alt));
                    Aim("ArmR",Vector3.Lerp(new Vector3(-35*feet-20,0,12),new Vector3(-95+landing*10,0,18),alt));
                    Aim("ForearmL",new Vector3(Mathf.Lerp(-75,-55,alt),0,Mathf.Lerp(0,-25,alt)));
                    Aim("ForearmR",new Vector3(Mathf.Lerp(-75,-55,alt),0,Mathf.Lerp(0,25,alt)));
                    Layer("Spine",new Vector3(0,-feet*12,0));Layer("Hips",new Vector3(0,feet*6,0));
                }
                else
                {
                    // Jumpstyle: a kick then a supported return, with a
                    // delayed shoulder swing instead of a rigid mirror pose.
                    Aim("LegL",new Vector3(Mathf.Max(0,feet)*43-8,0,feet*4));
                    Aim("LegR",new Vector3(Mathf.Max(0,opposite)*43-8,0,opposite*4));
                    Aim("ShinL",new Vector3(Mathf.Max(0,feet)*16,0,0));
                    Aim("ShinR",new Vector3(Mathf.Max(0,opposite)*16,0,0));
                    Aim("FootL",new Vector3(8-Mathf.Max(0,feet)*61,0,0));
                    Aim("FootR",new Vector3(8-Mathf.Max(0,opposite)*61,0,0));
                    // Arms pendulum out with each kick; alternate phrases land in a raised V.
                    Aim("ArmL",Vector3.Lerp(new Vector3(-48-feet*22+phrase*6,0,-23+Mathf.Max(0,feet)*35),new Vector3(-158+landing*12,0,24),alt));
                    Aim("ArmR",Vector3.Lerp(new Vector3(-50+feet*20,0,24+phrase*5-Mathf.Max(0,opposite)*35),new Vector3(-158+landing*12,0,-24),alt));
                    Aim("ForearmL",new Vector3(Mathf.Lerp(-42,-12,alt),0,0));Aim("ForearmR",new Vector3(Mathf.Lerp(-39,-12,alt),0,0));
                    Layer("Spine",new Vector3(Mathf.Max(0,Mathf.Abs(feet))*5,0,-feet*8));
                }
                // Dancing with equipped poi keeps the style's footwork while the arms
                // drive the butterfly on the dance beat, which the heads share.
                if(dancingPoi)PoiArms(beatTime);
                float step=Mathf.Clamp01(1-(Time.time-latestDanceStepTime)/.38f);
                if(step>0)
                {
                    float sideways=latestDanceDirection==0?-1:latestDanceDirection==3?1:0;
                    Layer("Hips",new Vector3(latestDanceDirection==1?18*step:-7*step,0,sideways*17*step));
                    Layer("Spine",new Vector3(latestDanceDirection==2?-11*step:0,0,-sideways*13*step));
                    Layer("LegL",new Vector3((latestDanceDirection==0||latestDanceDirection==2?24:-8)*step,0,-sideways*7*step));
                    Layer("LegR",new Vector3((latestDanceDirection==3||latestDanceDirection==2?24:-8)*step,0,-sideways*7*step));
                    Layer("ArmL",new Vector3((latestDanceDirection==2?-25:10)*step,0,-sideways*11*step));
                    Layer("ArmR",new Vector3((latestDanceDirection==2?-25:10)*step,0,-sideways*11*step));
                }
            }
            else
            {
                // Gait mechanics: arms swing opposite the landing foot, the pelvis turns
                // with the stride while the shoulders counter-rotate, and running adds
                // lean, bent elbows and bigger swings. Probed rig axes: +X pitches arms
                // and legs backward, -X on a forearm bends the elbow, +Y yaws toward the
                // actor's +X side, +Z on the hips lifts the +X hip, and "L" bones sit on +X.
                float move=Mathf.Clamp01(speed/1.2f),stride=walk*move,run=gait.Run*move;
                float contact=Mathf.Cos(walkCycle)*move;
                float support=Mathf.Cos(walkCycle-Mathf.PI*gait.Stance)*move;
                float turn=Mathf.Clamp(turnRate/180f,-1f,1f);
                float lean=Mathf.Clamp(accelerationLean*1.1f,-5f,5f)+run*9f;
                float pelvis=Mathf.Lerp(7f,10f,run),shoulders=Mathf.Lerp(6f,11f,run),swing=Mathf.Lerp(24f,40f,run);
                float elbow=Mathf.Lerp(12f,80f,run)*move;
                Aim("Hips",new Vector3(3*move+lean,-pelvis*contact+turn*4f,3.5f*(1-.5f*run)*support-turn*3f));
                Aim("Spine",new Vector3(Mathf.Sin(t*.5f)*2*(1-move)-lean*.35f+run*4f,(pelvis+shoulders)*contact+turn*9f,-2.8f*support+turn*4f));
                Aim("LegL",new Vector3(stride*39,0,0));Aim("LegR",new Vector3(-stride*39,0,0));
                Aim("ShinL",new Vector3(Mathf.Max(0,-stride)*34,0,0));Aim("ShinR",new Vector3(Mathf.Max(0,stride)*34,0,0));
                Aim("FootL",new Vector3(-stride*39-Mathf.Max(0,-stride)*34,0,0));
                Aim("FootR",new Vector3(stride*39-Mathf.Max(0,stride)*34,0,0));
                Aim("ArmL",new Vector3(swing*contact,0,-8+4*run));Aim("ArmR",new Vector3(-swing*contact,0,8-4*run));
                Aim("ForearmL",new Vector3(-elbow-Mathf.Max(0,-contact)*12f,0,0));
                Aim("ForearmR",new Vector3(-elbow-Mathf.Max(0,contact)*12f,0,0));
                Aim("HandL",new Vector3(-run*10f,0,0));Aim("HandR",new Vector3(-run*10f,0,0));
                Aim("Head",new Vector3(Mathf.Sin(t*.4f)*3*(1-move)-run*6f,-shoulders*contact*.8f+turn*12f,Mathf.Sin(t*.6f)*4*(1-move)));
                // Standing still: weight drifts between feet, the chest breathes and the
                // head wanders between glances. Each actor keeps its own rhythm.
                if(Pose!="Downed"&&Pose!="Spirit")
                {
                    still=1-move;
                    float drift=Time.time*.21f+phase*1.3f;
                    shift=(Mathf.Sin(drift)+.4f*Mathf.Sin(drift*2.7f+1.1f))*still;
                    look=Mathf.Clamp((Mathf.PerlinNoise(Time.time*.35f+phase*5.1f,phase)-.5f)*4f,-1f,1f);
                    nod=Mathf.PerlinNoise(phase,Time.time*.23f)-.5f;
                    idleShift=shift*.03f;
                }
                if(Pose=="Detained")
                {Aim("Spine",new Vector3(20,0,0));Aim("Head",new Vector3(15,0,12));Aim("ArmL",new Vector3(-105,0,-18));Aim("ArmR",new Vector3(-105,0,18));}
                else if(Pose=="Rescue"||Pose=="Drag"||Pose=="FindFriend")
                {Aim("Spine",new Vector3(15,0,0));Aim("ArmL",new Vector3(-78,0,-18));Aim("ArmR",new Vector3(-82,0,18));Aim("ForearmL",new Vector3(-28,0,0));Aim("ForearmR",new Vector3(-28,0,0));}
                else if(Pose=="ReadClue")
                {Aim("Head",new Vector3(16,0,-18));Aim("ArmR",new Vector3(-72,0,18));Aim("ForearmR",new Vector3(-55,0,0));}
                else if(Pose=="Extract")
                {Aim("Spine",new Vector3(-15,0,0));Aim("ArmL",new Vector3(-115,0,-20));Aim("ArmR",new Vector3(-115,0,20));}
                else if(Pose=="Accusing"||Pose=="Swarming")
                {Aim("Spine",new Vector3(20,0,0));Aim("Head",new Vector3(-10,0,-10));Aim("ArmR",new Vector3(-90+wave*18,0,10));}
                else if(Pose=="Questioning"||Pose=="Watching")
                {Aim("Head",new Vector3(-8,15,20));Aim("ArmL",new Vector3(-35,0,-20));}
                else if(Pose=="Spirit")
                {Aim("Spine",new Vector3(-10,0,wave*8));Aim("Head",new Vector3(5,0,-wave*12));Aim("ArmL",new Vector3(-35,0,-35));Aim("ArmR",new Vector3(-35,0,35));}
                else if(Pose=="Intoxicated")
                {Aim("Hips",new Vector3(4,0,wave*8));Aim("Spine",new Vector3(-5,0,-wave*11));Aim("Head",new Vector3(Mathf.Sin(t*.7f)*8,0,wave*13));Aim("ArmL",new Vector3(-18+wave*8,0,-12));Aim("ArmR",new Vector3(-18-wave*8,0,12));}
            }
            // Authored acting clips (package 03/04 people) replace the stiff single-frame
            // poses. Upper-body clips leave the legs to the gait and foot plant.
            string key=Beat!=""?Beat:exchanging&&!dance&&Pose!="Downed"&&Pose!="Spirit"&&!lying?"Exchange":Pose;
            if(key!=motionKey)
            {
                foreach(var item in bones)transitionFrom[item.Key]=item.Value.localRotation;
                transitionLength=key=="Downed"||motionKey=="Downed"||key==FestivalSimulation.LieDownKind||motionKey==FestivalSimulation.LieDownKind?.5f:key=="Dance"||key=="Poi"?.22f:.32f;
                motionKey=key;poseStart=Time.time-.02f;transitionEnd=poseStart+transitionLength;
            }
            var acting=ActingClip(key,speed);
            bool fullBodyClip=false;Vector3 clipHips=Vector3.zero;
            var clip=acting.name==null?null:FestivalMotionLibrary.Get(acting.name);
            if(clip!=null)
            {
                float clock=(Time.time-poseStart)*acting.rate+(acting.loop?phase*.53f:0);
                clip.Sample(clock,acting.loop,clipPose,out clipHips);
                float weight=key=="Idle"||key==""||key=="Walk"?Mathf.Clamp01(1-speed/1.2f):key=="DoseReaction"?BeatStrength:1;
                for(int i=0;i<clipPose.Length;i++)
                {
                    string bone=FestivalMotionLibrary.Bones[i];
                    if(!acting.full&&(i<1||i>8))continue;
                    if(targets.ContainsKey(bone))targets[bone]=Quaternion.Slerp(targets[bone],clipPose[i],weight);
                }
                fullBodyClip=acting.full;
            }
            // The dose hits as a head shake that grows with it.
            if(key=="DoseReaction")Layer("Head",new Vector3(0,Mathf.Sin(Time.time*13f+phase)*16f,Mathf.Sin(Time.time*4.5f+phase)*9f)*BeatStrength);
            // Standing life layers on top of any clip: weight drifts between feet, the
            // chest breathes and the head wanders between glances, each on its own rhythm.
            if(still>.01f)
            {
                Layer("Hips",new Vector3(0,0,3f*shift));
                Layer("Spine",new Vector3(Mathf.Sin(Time.time*1.7f+phase)*1.2f*still,0,-2.2f*shift));
                if(key=="Idle"||key=="")Layer("Head",new Vector3(nod*10f*still,look*28f*still,0));
                if(idleStyle==1&&key=="Idle"&&equippedProp==null&&carriedPoi=="")
                {
                    // Hands on hips.
                    Aim("ArmL",new Vector3(12*still,0,-8+42*still));Aim("ArmR",new Vector3(12*still,0,8-42*still));
                    Aim("ForearmL",new Vector3(-95*still,0,-20*still));Aim("ForearmR",new Vector3(-95*still,0,20*still));
                }
            }
            // Carry a prop with a bent elbow instead of swinging it through the
            // thigh. Keep dedicated interaction and performance poses in charge.
            if(!dance&&(Pose=="Idle"||Pose=="Walk"||Pose=="")&&carriedPoi!="")
            {
                // Both hands lead the wheeling heads at waist height, wrists rolling with each turn.
                float roll=Mathf.Sin(Time.time*7.5f+phase)*Mathf.Clamp01(speed/1.2f);
                Aim("ArmL",new Vector3(-24,0,-4));Aim("ArmR",new Vector3(-24,0,4));
                Aim("ForearmL",new Vector3(-58,0,0));Aim("ForearmR",new Vector3(-58,0,0));
                Aim("HandL",new Vector3(roll*22,0,0));Aim("HandR",new Vector3(-roll*22,0,0));
            }
            else if(!dance&&(Pose=="Idle"||Pose=="Walk"||Pose=="")&&equippedProp!=null)
            {
                bool bag=equippedId=="merch_bag";
                Aim("ArmR",new Vector3(bag?-12:-28,0,bag?12:8));
                Aim("ForearmR",new Vector3(bag?-18:-62,0,0));
                Aim("HandR",new Vector3(0,0,-6));
            }
            float presentation=Mathf.Sin(Mathf.Clamp01((Time.time-receiptAt)/.85f)*Mathf.PI);
            if(presentation>.01f&&!dance&&Pose!="Downed"&&Pose!="Spirit"&&!lying)
            {Aim("ArmR",new Vector3(-28-25*presentation,0,8));Aim("ForearmR",new Vector3(-62+15*presentation,0,0));Layer("Head",new Vector3(6*presentation,0,0));}
            bool canExchange=Pose!="Downed"&&Pose!="Spirit"&&!dance&&!lying;
            exchangeWeight=Mathf.MoveTowards(exchangeWeight,exchanging&&canExchange?1:0,animationDelta*4);
            if(exchangeWeight>.001f)
            {
                Layer("Spine",new Vector3(7,0,0)*exchangeWeight);
                Layer("Head",new Vector3(9,0,0)*exchangeWeight);
                Aim("ArmR",new Vector3(-55,0,12));Aim("ForearmR",new Vector3(-45,0,0));
            }
            // ponytail: one third-person curl; use item-specific morphs if close-up playtests show clipping.
            if(bodyRenderer!=null&&!UsesDistantMesh)
            {
                if(gripLeft>=0)bodyRenderer.SetBlendShapeWeight(gripLeft,Mathf.Lerp(bodyRenderer.GetBlendShapeWeight(gripLeft),poiShown?100:0,1-Mathf.Exp(-12*animationDelta)));
                if(gripRight>=0)bodyRenderer.SetBlendShapeWeight(gripRight,Mathf.Lerp(bodyRenderer.GetBlendShapeWeight(gripRight),equippedId!=""||performingPoi?100:0,1-Mathf.Exp(-12*animationDelta)));
            }
            if(faceRenderer!=null&&!UsesDistantMesh)
            {
                if(smileIndex>=0)faceRenderer.SetBlendShapeWeight(smileIndex,Mathf.Lerp(faceRenderer.GetBlendShapeWeight(smileIndex),dance?65:Time.time-receiptAt<1.4f?80:0,1-Mathf.Exp(-7*animationDelta)));
                if(concernIndex>=0)faceRenderer.SetBlendShapeWeight(concernIndex,Mathf.Lerp(faceRenderer.GetBlendShapeWeight(concernIndex),Pose=="Downed"||Pose=="Detained"?85:key=="DoseReaction"?85*BeatStrength:Threat*65,1-Mathf.Exp(-7*animationDelta)));
            }
            if(eyeRenderer!=null&&Mathf.Abs(Threat-lastAppliedThreat)>.001f)
            {
                lastAppliedThreat=Threat;
                float alarm=Mathf.Clamp01((Threat-.24f)/.76f);
                eyeRenderer.enabled=alarm>.01f;
                if(eyeMaterial!=null)eyeMaterial.color=Color.Lerp(new Color(1,.72f,.16f),new Color(1,.08f,.32f),alarm);
                var tint=Color.Lerp(baseTint,new Color(1,.53f,.48f),alarm*.48f);
                if(ownedMaterial!=null)ownedMaterial.color=tint;
                if(garmentMaterial!=null)garmentMaterial.color=tint;
                if(skinMaterial!=null)skinMaterial.color=tint;
                if(hairMaterial!=null)hairMaterial.color=tint;
                if(gearMaterial!=null)gearMaterial.color=tint;
                if(lensMaterial!=null)lensMaterial.color=tint;
            }
            // Pose changes ease out of the previous pose instead of snapping toward the new
            // one; the ease replaces per-frame smoothing so motion starts on the first frame.
            float fade=Mathf.Clamp01(1-(transitionEnd-Time.time)/transitionLength);
            float blend=1-Mathf.Exp(-(Pose=="Downed"?9:16)*animationDelta);
            if(fade<1)
            {
                float eased=fade*fade*(3-2*fade);
                foreach(var item in bones)if(transitionFrom.TryGetValue(item.Key,out var from))targets[item.Key]=Quaternion.Slerp(from,targets[item.Key],eased);
                blend=1;
            }
            foreach(var item in bones)item.Value.localRotation=Quaternion.Slerp(item.Value.localRotation,targets[item.Key],blend);
            var hipsBefore=bones.TryGetValue("Hips",out var hipsBone)?hipsBone.localPosition:Vector3.zero;
            if(dance&&Pose!="Dj")
                footPlant?.Dance(Time.time*DanceAngularSpeed(danceStyle)+phase,
                    danceStyle,animationDelta);
            else footPlant?.Update(Pose!="Downed"&&Pose!="Spirit"&&!lying&&!fullBodyClip,
                !dance&&Pose!="Downed"&&Pose!="Spirit"&&!lying&&speed>.14f,
                delta,speed,walkCycle,gait,animationDelta,idleShift);
            if(fullBodyClip&&hipsBone!=null)
                hipsBone.localPosition=Vector3.Lerp(hipsBefore,restHips+clipHips,1-Mathf.Exp(-10f*animationDelta));
            else if(lying&&hipsBone!=null)
            {
                // Imported bone parents carry a rotated, scaled transform, so the drop is worked out in the world and taken back.
                float above=hipsBone.parent.TransformPoint(restHips).y-transform.position.y-LieHipHeight*transform.lossyScale.y;
                hipsBone.localPosition=Vector3.Lerp(hipsBefore,restHips+hipsBone.parent.InverseTransformVector(Vector3.down*above),1-Mathf.Exp(-10f*animationDelta));
            }
            if(exchangeWeight>.001f&&bones.Count==15)
            {
                var hand=bones["HandR"];
                var target=Vector3.Lerp(hand.position,exchangeTarget+hand.up*.05f,exchangeWeight);
                FestivalDjHandContact.Reach(transform,bones["ArmR"],bones["ForearmR"],hand,target,1);
            }
            if(Pose=="Dj"&&DjConsole!=null&&bones.Count==15)
            {
                if(djHandContact==null)
                {
                    djHandContact=new FestivalDjHandContact(transform,DjConsole,
                        bones["ArmL"],bones["ForearmL"],bones["HandL"],
                        bones["ArmR"],bones["ForearmR"],bones["HandR"]);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","dj_console_contact",
                        "actor="+name+" console="+DjConsole.name);
#endif
                }
                djHandContact.Update(Time.time*6f+phase);
            }
        }
        public void UpdateDetailForDistance(float metres)
        {
            bool distant=!AlwaysHighDetail&&(UsesDistantMesh?metres>=8:metres>10);
            if(distant==UsesDistantMesh)return;
            UsesDistantMesh=distant;
            foreach(var part in detailParts)
            {
                part.Renderer.sharedMesh=distant?part.Distant:part.Detailed;
                for(int shape=0;shape<part.Renderer.sharedMesh.blendShapeCount;shape++)part.Renderer.SetBlendShapeWeight(shape,0);
                int fit=part.Renderer.sharedMesh.GetBlendShapeIndex(fitName);
                if(fit>=0)part.Renderer.SetBlendShapeWeight(fit,100);
            }
            if(faceRenderer!=null)
            {
                blinkIndex=faceRenderer.sharedMesh.GetBlendShapeIndex("Blink");
                intoxicatedEyesIndex=faceRenderer.sharedMesh.GetBlendShapeIndex("WideIntoxicatedEyes");
                if(intoxicatedEyesIndex>=0)faceRenderer.SetBlendShapeWeight(intoxicatedEyesIndex,HighlyIntoxicated?100:0);
            }
        }
        (string name,bool loop,bool full,float rate) ActingClip(string key,float speed)
        {
            switch(key)
            {
                case "Downed":return speed>.35f?("BeingDragged",true,true,Mathf.Clamp(speed/1.2f,.4f,2f)):("CrawlDowned",true,true,.45f);
                case "Drag":return ("DragOther",true,false,1);
                case "Detained":return ("DetainedEscort",true,false,1);
                case "Extract":return ("Cheer",true,false,1);
                case "Watching":return ("WookStare",false,false,1);
                case "Questioning":return ("Talk",true,false,1);
                case "Accusing":return ("WookLockedOn",true,false,1);
                case "Swarming":return ("SwarmLunge",true,false,1);
                case "Exchange":return ("Handoff",false,false,1);
                case "TakeDose":return ("Consume",false,false,1);
                case "DoseReaction":return ("Panic",true,false,.6f+.8f*BeatStrength);
                case "Idle":case "Walk":case "":
                    if(equippedProp!=null||carriedPoi!="")return (null,false,false,0);
                    return ("Idle",true,false,.8f+phase%.3f);
                default:return (null,false,false,0);
            }
        }
        void Aim(string bone,Vector3 angle){if(rest.ContainsKey(bone))targets[bone]=rest[bone]*Quaternion.Euler(angle);}
        void Layer(string bone,Vector3 angle){if(targets.TryGetValue(bone,out var target))targets[bone]=target*Quaternion.Euler(angle);}
        void PoiArms(float circle)
        {
            float exchange=Mathf.Sin(circle),wristPulse=Mathf.Cos(circle);
            Aim("ArmL",new Vector3(-80+wristPulse*14,0,-38+exchange*22));
            Aim("ArmR",new Vector3(-80+wristPulse*14,0,38-exchange*22));
            Aim("ForearmL",new Vector3(-50+exchange*8,0,27+exchange*10));
            Aim("ForearmR",new Vector3(-50-exchange*8,0,-27-exchange*10));
            Aim("HandL",new Vector3(wristPulse*20,0,exchange*18));
            Aim("HandR",new Vector3(wristPulse*20,0,-exchange*18));
        }
        void PreparePoi(bool led)
        {
            if(bones.TryGetValue("HandL",out var left))poiLeft=FestivalPoiRig.Create(left,new Vector3(0,-.05f,0),0,led);
            if(bones.TryGetValue("HandR",out var right))poiRight=FestivalPoiRig.Create(right,new Vector3(0,-.05f,0),1,led);
        }
        void OnDestroy()
        {
            if(ownedMaterial!=null){if(Application.isPlaying)Destroy(ownedMaterial);else DestroyImmediate(ownedMaterial);}
            if(ownedPalette!=null){if(Application.isPlaying)Destroy(ownedPalette);else DestroyImmediate(ownedPalette);}
            if(garmentPalette!=null){if(Application.isPlaying)Destroy(garmentPalette);else DestroyImmediate(garmentPalette);}
            if(eyeMaterial!=null){if(Application.isPlaying)Destroy(eyeMaterial);else DestroyImmediate(eyeMaterial);}
            foreach(var material in new[]{garmentMaterial,skinMaterial,hairMaterial,gearMaterial,lensMaterial,scleraMaterial})
                if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        }
    }
}
