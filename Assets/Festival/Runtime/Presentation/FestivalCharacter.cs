using System.Collections.Generic;
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
        static float DanceAngularSpeed(int style) => style==0?8.8f:style==1?7.6f:8.3f;
        int latestDanceDirection=-1;
        float latestDanceStepTime=-100;
        public string Pose="Idle";
        public Transform DjConsole { get; set; }
        public bool Crowd;
        public bool AmbientCrowd;
        public float Threat;
        public FestivalAppearance Appearance { get; private set; }
        public float HeightScale { get; private set; }=1;
        public Vector3 ShapeScale => Appearance.Scale;
        Material ownedMaterial;
        Material skinMaterial,hairMaterial,gearMaterial,lensMaterial,scleraMaterial;
        Color baseTint;
        float lastAppliedThreat=-1;
        float lastAnimationTime;
        Material eyeMaterial;
        Texture2D ownedPalette;
        Renderer eyeRenderer;
        Renderer spoonRenderer;
        SkinnedMeshRenderer faceRenderer;
        int blinkIndex=-1;
        int intoxicatedEyesIndex=-1;
        public bool HighlyIntoxicated { get; private set; }
        public bool RedEyes { get; private set; }
        FestivalPoiRig poiLeft,poiRight,equippedPoi;
        GameObject equippedProp;
        string equippedId="";
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
            if(actor.bones.Count==15)
                actor.footPlant=new FestivalFootPlant(actor.transform,actor.bones["Hips"],
                    actor.bones["LegL"],actor.bones["ShinL"],actor.bones["FootL"],
                    actor.bones["LegR"],actor.bones["ShinR"],actor.bones["FootR"]);
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
            itemId=itemId??"";
            if(equippedId==itemId)return;
            equippedId=itemId;
            if(equippedProp!=null)Destroy(equippedProp);
            if(equippedPoi!=null)Destroy(equippedPoi.gameObject);
            equippedProp=null;
            if(itemId==""||itemId=="little_spoon"||!bones.TryGetValue("HandR",out var hand))return;
            if(itemId=="poi_led"||itemId=="poi_practice")
            {
                equippedPoi=FestivalPoiRig.Create(hand,new Vector3(0,-.05f,0),1,itemId=="poi_led");
                return;
            }
            var resource=Festival.Network.FestivalSession.DropModel(itemId);
            if(resource==null)return;
            equippedProp=FestivalArtView.Create(hand,resource);
            if(equippedProp==null)return;
            equippedProp.name="Equipped "+itemId;
            var handScale=hand.lossyScale;
            equippedProp.transform.localPosition=new Vector3(0,
                -.23f/Mathf.Max(.01f,Mathf.Abs(handScale.y)),
                -.10f/Mathf.Max(.01f,Mathf.Abs(handScale.z)));
            equippedProp.transform.localRotation=Quaternion.Euler(12,0,-18);
            equippedProp.transform.localScale=new Vector3(.27f/Mathf.Max(.01f,Mathf.Abs(handScale.x)),.27f/Mathf.Max(.01f,Mathf.Abs(handScale.y)),.27f/Mathf.Max(.01f,Mathf.Abs(handScale.z)));
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
            if(performingPoi && poiLeft==null)PreparePoi();
            if(poiLeft!=null){poiLeft.gameObject.SetActive(performingPoi);poiLeft.Spinning=performingPoi;}
            if(poiRight!=null){poiRight.gameObject.SetActive(performingPoi);poiRight.Spinning=performingPoi;}
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
            // Smaller festivalgoers take shorter, more frequent steps. The
            // contact distance must scale with their actual leg reach.
            float strideDistance=Mathf.Lerp(1.0f,1.22f,Mathf.Clamp01((measuredSpeed-1.4f)/2.4f))
                *Mathf.Max(.65f,transform.lossyScale.y);
            if(measuredSpeed>.12f)walkCycle+=delta.magnitude*(Mathf.PI*2/strideDistance);
            float t=Time.time*4+phase,wave=Mathf.Sin(t),walk=Mathf.Sin(walkCycle);
            if(equippedProp!=null)equippedProp.SetActive(Pose!="Poi"&&Pose!="Downed"&&Pose!="Spirit");
            if(equippedPoi!=null)equippedPoi.gameObject.SetActive(Pose!="Poi"&&Pose!="Downed"&&Pose!="Spirit");
            foreach(var item in bones)targets[item.Key]=rest[item.Key];
            bool dance=Crowd||Pose=="Dance"||Pose=="Poi"||Pose=="Dj"||Pose=="Distracted";
            if(Pose=="Downed")
            {
                Aim("Hips",new Vector3(75,0,walk*4));Aim("Head",new Vector3(-35,0,12));
                Aim("ArmL",new Vector3(-85+walk*20,0,20));Aim("ArmR",new Vector3(-85-walk*20,0,-20));
                Aim("LegL",new Vector3(walk*12,0,10));Aim("LegR",new Vector3(-walk*12,0,-10));
            }
            else if(dance)
            {
                // Footwork and torso accents share a beat, while each actor's
                // phase and style keep a crowd from moving in lockstep.
                float beatTime=Time.time*DanceAngularSpeed(danceStyle)+phase;
                float feet=Mathf.Sin(beatTime),opposite=Mathf.Sin(beatTime+Mathf.PI);
                float landing=Mathf.Pow(Mathf.Max(0,Mathf.Cos(beatTime*2)),2);
                float sway=Mathf.Sin(beatTime*.5f);
                float phrase=Mathf.Sin(beatTime*.25f+phase*.37f);
                Aim("Hips",new Vector3(7+landing*6,sway*9,feet*8));
                Aim("Spine",new Vector3(-8-landing*4,-sway*5+phrase*6,-feet*11));
                Aim("Head",new Vector3(2-landing*5,Mathf.Sin(beatTime*.5f+1)*9-phrase*5,-feet*4));
                if(performingPoi)
                {
                    // The performer concentrates on the paired butterfly flow:
                    // relaxed knees, quiet hips and alternating crossed wrists.
                    float circle=Time.time*9f+phase;
                    float exchange=Mathf.Sin(circle);
                    float wristPulse=Mathf.Cos(circle);
                    Aim("Hips",new Vector3(4+landing*2,sway*4,feet*3));
                    Aim("Spine",new Vector3(-5,phrase*5,-feet*4));
                    Aim("Head",new Vector3(4,phrase*-6,-feet*3));
                    Aim("ArmL",new Vector3(-80+wristPulse*6,0,-38+exchange*18));
                    Aim("ArmR",new Vector3(-80+wristPulse*6,0,38-exchange*18));
                    Aim("ForearmL",new Vector3(-50,0,27+exchange*10));
                    Aim("ForearmR",new Vector3(-50,0,-27-exchange*10));
                    Aim("HandL",new Vector3(wristPulse*20,0,exchange*18));
                    Aim("HandR",new Vector3(wristPulse*20,0,-exchange*18));
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
                    Aim("ArmL",new Vector3(-32-feet*19+phrase*5,0,-17+phrase*5));
                    Aim("ArmR",new Vector3(-32+feet*19-phrase*5,0,17+phrase*5));
                    Aim("ForearmL",new Vector3(-62+landing*5,0,0));Aim("ForearmR",new Vector3(-62+landing*5,0,0));
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
                    Aim("ArmL",new Vector3(-42+feet*15+phrase*8,0,-27+phrase*9));
                    Aim("ArmR",new Vector3(-38-feet*18,0,27+phrase*5));
                    Aim("ForearmL",new Vector3(-52,0,17));Aim("ForearmR",new Vector3(-49,0,-14));
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
                    Aim("ArmL",new Vector3(-48-feet*22+phrase*6,0,-23));
                    Aim("ArmR",new Vector3(-50+feet*20,0,24+phrase*5));
                    Aim("ForearmL",new Vector3(-42,0,0));Aim("ForearmR",new Vector3(-39,0,0));
                }
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
                float move=Mathf.Clamp01(speed/1.6f),stride=walk*move;
                float turn=Mathf.Clamp(turnRate/180f,-1f,1f);
                float lean=Mathf.Clamp(accelerationLean*1.1f,-5f,5f);
                Aim("Hips",new Vector3(5*move+lean,turn*4f,stride*5-turn*3f));
                Aim("Spine",new Vector3(Mathf.Sin(t*.5f)*2-5*move-lean*.6f,turn*9f,-stride*5+turn*4f));
                Aim("LegL",new Vector3(stride*39,0,0));Aim("LegR",new Vector3(-stride*39,0,0));
                Aim("ShinL",new Vector3(Mathf.Max(0,-stride)*34,0,0));Aim("ShinR",new Vector3(Mathf.Max(0,stride)*34,0,0));
                Aim("FootL",new Vector3(-stride*39-Mathf.Max(0,-stride)*34,0,0));
                Aim("FootR",new Vector3(stride*39-Mathf.Max(0,stride)*34,0,0));
                Aim("ArmL",new Vector3(-stride*27,0,-8));Aim("ArmR",new Vector3(stride*27,0,8));
                Aim("Head",new Vector3(Mathf.Sin(t*.4f)*3,turn*12f,Mathf.Sin(t*.6f)*4));
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
            if(eyeRenderer!=null&&Mathf.Abs(Threat-lastAppliedThreat)>.001f)
            {
                lastAppliedThreat=Threat;
                float alarm=Mathf.Clamp01((Threat-.24f)/.76f);
                eyeRenderer.enabled=alarm>.01f;
                if(eyeMaterial!=null)eyeMaterial.color=Color.Lerp(new Color(1,.72f,.16f),new Color(1,.08f,.32f),alarm);
                var tint=Color.Lerp(baseTint,new Color(1,.53f,.48f),alarm*.48f);
                if(ownedMaterial!=null)ownedMaterial.color=tint;
                if(skinMaterial!=null)skinMaterial.color=tint;
                if(hairMaterial!=null)hairMaterial.color=tint;
                if(gearMaterial!=null)gearMaterial.color=tint;
                if(lensMaterial!=null)lensMaterial.color=tint;
            }
            float blend=1-Mathf.Exp(-(Pose=="Downed"?7:12)*animationDelta);
            foreach(var item in bones)item.Value.localRotation=Quaternion.Slerp(item.Value.localRotation,targets[item.Key],blend);
            if(dance&&Pose!="Dj")
                footPlant?.Dance(Time.time*DanceAngularSpeed(danceStyle)+phase,
                    danceStyle,animationDelta);
            else footPlant?.Update(Pose!="Downed"&&Pose!="Spirit",
                !dance&&Pose!="Downed"&&Pose!="Spirit"&&speed>.14f,
                delta,speed,walkCycle,strideDistance,animationDelta);
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
        void Aim(string bone,Vector3 angle){if(rest.ContainsKey(bone))targets[bone]=rest[bone]*Quaternion.Euler(angle);}
        void Layer(string bone,Vector3 angle){if(targets.TryGetValue(bone,out var target))targets[bone]=target*Quaternion.Euler(angle);}
        void PreparePoi()
        {
            if(bones.TryGetValue("HandL",out var left))poiLeft=FestivalPoiRig.Create(left,new Vector3(0,-.05f,0),0,true);
            if(bones.TryGetValue("HandR",out var right))poiRight=FestivalPoiRig.Create(right,new Vector3(0,-.05f,0),1,true);
        }
        void OnDestroy()
        {
            if(ownedMaterial!=null){if(Application.isPlaying)Destroy(ownedMaterial);else DestroyImmediate(ownedMaterial);}
            if(ownedPalette!=null){if(Application.isPlaying)Destroy(ownedPalette);else DestroyImmediate(ownedPalette);}
            if(eyeMaterial!=null){if(Application.isPlaying)Destroy(eyeMaterial);else DestroyImmediate(eyeMaterial);}
            foreach(var material in new[]{skinMaterial,hairMaterial,gearMaterial,lensMaterial,scleraMaterial})
                if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        }
    }
}
