using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>SPIN-1: the people wheel lands on the tripper, the dose wheel on their dose, then the camera cuts to the
    /// tripper taking it at camp. Everything is computed from the public spin result (SpinSeed, SpinEndsAt, TripperId,
    /// Doses), so every client shows the same spin, landing and reaction without any extra network traffic.</summary>
    public sealed class FestivalSpinner : MonoBehaviour
    {
        // Seconds from the spin's start (SpinEndsAt - SpinSeconds). The last 3 s are the take and the reaction.
        public const float PeopleSpin=2.4f,DoseStarts=3f,DoseSpin=1.4f,TakeStarts=5f,ReactStarts=6.4f;
        const int PeopleTurns=4,DoseTurns=5;
        public enum Stage{Hidden,People,Dose,Take,React}
        public struct Beat
        {
            public Stage Stage;
            // Wheel rotations are clockwise degrees; the pointer sits at the top. Reaction is the dose's size, 0-1.
            public float Elapsed,PeopleDegrees,DoseDegrees,Reaction;
            public bool PeopleLanded,DoseLanded;
            public int Dose;
        }

        // Connected friends in state order; a tripper who just dropped keeps a slice so the wheel can still land.
        public static List<string> Crew(RoundState state)
        {
            var crew=state.Players.FindAll(p=>p.Connected).ConvertAll(p=>p.Id);
            if(state.TripperId!=""&&!crew.Contains(state.TripperId))crew.Add(state.TripperId);
            return crew;
        }
        // Slices run clockwise from the top of the wheel.
        static int SliceAt(IReadOnlyList<int> weights,float wheelDegrees)
        {
            float total=0,end=0,angle=Mathf.Repeat(wheelDegrees,360);foreach(var w in weights)total+=w;
            for(int i=0;i<weights.Count;i++){end+=weights[i]*360f/total;if(angle<end)return i;}
            return weights.Count-1;
        }
        public static int SliceUnderPointer(IReadOnlyList<int> weights,float rotation)=>SliceAt(weights,-rotation);
        // Whole turns, then a seeded spot inside the chosen slice (never on an edge), so every client stops on the same point.
        static float LandingDegrees(IReadOnlyList<int> weights,int slice,int seed,int turns)
        {
            float total=0,start=0;foreach(var w in weights)total+=w;
            for(int i=0;i<slice;i++)start+=weights[i]*360f/total;
            return turns*360+360-(start+weights[slice]*360f/total*(.15f+.7f*Unit(seed)));
        }
        static float Unit(int seed){uint x=unchecked((uint)seed*2654435761u);x^=x>>15;x=unchecked(x*0x2c1b3c6du);x^=x>>12;return (x>>8)/16777216f;}
        static float Turn(float landing,float progress){float left=1-Mathf.Clamp01(progress);return landing*(1-left*left*left);}
        static int[] Equal(int count){var weights=new int[count];for(int i=0;i<count;i++)weights[i]=1;return weights;}
        public static Beat At(RoundState state,List<string> crew,double now)
        {
            var beat=new Beat();
            if(state==null||state.Phase!="Spinning"||crew.Count==0)return beat;
            float t=beat.Elapsed=(float)(now-(state.SpinEndsAt-FestivalSimulation.SpinSeconds));
            beat.Dose=Mathf.Clamp(state.Doses.Find(d=>d.PlayerId==state.TripperId)?.Dose??1,1,FestivalSimulation.DoseSlices.Count);
            beat.Reaction=beat.Dose/(float)FestivalSimulation.DoseSlices.Count;
            beat.PeopleDegrees=Turn(LandingDegrees(Equal(crew.Count),Mathf.Max(0,crew.IndexOf(state.TripperId)),state.SpinSeed,PeopleTurns),t/PeopleSpin);
            beat.DoseDegrees=Turn(LandingDegrees(FestivalSimulation.DoseSlices,beat.Dose-1,unchecked(state.SpinSeed*31+7),DoseTurns),(t-DoseStarts)/DoseSpin);
            beat.PeopleLanded=t>=PeopleSpin;beat.DoseLanded=t>=DoseStarts+DoseSpin;
            beat.Stage=t<DoseStarts?Stage.People:t<TakeStarts?Stage.Dose:t<ReactStarts?Stage.Take:Stage.React;
            return beat;
        }

        // FestivalHud's palette, so the spinner reads as part of the same interface.
        static readonly Color Ink=new Color(.035f,.065f,.075f,1),Paper=new Color(.948f,.928f,.852f,1),Orange=new Color(.99f,.465f,.255f,1);
        static readonly Color[] CrewColors={new Color(.99f,.465f,.255f),new Color(.385f,.86f,.725f),new Color(1,.38f,.61f),new Color(1,.76f,.29f),
            new Color(.34f,.78f,1),new Color(.46f,.95f,.68f),new Color(.66f,.52f,1),new Color(.96f,.62f,.50f)};
        // The dose wheel heats up from calm mint to the hot pink 4-dose sliver.
        static readonly Color[] DoseColors={new Color(.385f,.86f,.725f),new Color(1,.76f,.29f),new Color(.99f,.465f,.255f),new Color(1,.25f,.55f)};
        static readonly string[] Reactions={"","Feels fine. Probably.","Whoa.","The colors are talking.","Oh no. Oh yes. Oh no."};
        const float WheelSize=520;
        // The take camera pushes in from TakeFar to TakeNear. It swings to the first of TakeAngles (degrees from the tripper's
        // facing) whose line to their face misses the scenery and passes TakeClearance from every other friend at camp; past 90 it
        // would see the back of the head.
        const float TakeFar=2.5f,TakeNear=1.8f,TakeClearance=.45f;
        static readonly float[] TakeAngles={0,30,-30,60,-60,90,-90};
        sealed class Wheel{public RectTransform Disc;public RawImage Image;public Text Result;public CanvasGroup Group;public Texture2D Texture;}
        FestivalSession session;
        Font font;
        GameObject overlay,wheels,letterbox;
        Wheel people,dose;
        Text caption;
        RawImage shot;
        RenderTexture shotTexture;
        Camera takeCamera;
        FestivalCharacter acting;
        float focusHeight,takeYaw;
        List<string> crew=new List<string>();
        string spinKey="";

        void Awake()
        {
            session=GetComponent<FestivalSession>();
            font=Resources.Load<Font>("FestivalDisplay");if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            overlay=new GameObject("Festival spinner");overlay.transform.SetParent(transform,false);
            var canvas=overlay.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110; // over the HUD (100)
            var scaler=overlay.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            wheels=Panel(overlay.transform,"Spinner backdrop",new Color(Ink.r,Ink.g,Ink.b,.92f),Vector2.zero,Vector2.one);
            people=MakeWheel(wheels.transform,"People wheel",.3f,"WHO TRIPS?");
            dose=MakeWheel(wheels.transform,"Dose wheel",.7f,"HOW MANY DOSES?");
            var numbers=new List<string>();for(int n=1;n<=FestivalSimulation.DoseSlices.Count;n++)numbers.Add(n.ToString());
            Paint(dose,FestivalSimulation.DoseSlices,DoseColors,numbers);
            letterbox=new GameObject("Take letterbox",typeof(RectTransform));letterbox.transform.SetParent(overlay.transform,false);
            Stretch((RectTransform)letterbox.transform);
            // The take renders into this canvas too, so the HUD stays hidden under the cut as it does under the wheels.
            shot=new GameObject("Take shot",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage)).GetComponent<RawImage>();
            shot.transform.SetParent(letterbox.transform,false);Stretch(shot.rectTransform);shot.raycastTarget=false;shot.enabled=false;
            Panel(letterbox.transform,"Top bar",Color.black,new Vector2(0,.87f),Vector2.one);
            var bottom=Panel(letterbox.transform,"Bottom bar",Color.black,Vector2.zero,new Vector2(1,.13f));
            caption=Label(bottom.transform,"Take caption",40);Stretch(caption.rectTransform);
            overlay.SetActive(false);
            takeCamera=new GameObject("Spinner take camera").AddComponent<Camera>();takeCamera.transform.SetParent(transform,false);
            takeCamera.enabled=false;takeCamera.fieldOfView=40;takeCamera.nearClipPlane=.05f;takeCamera.farClipPlane=130;
            takeCamera.cullingMask=~(1<<30); // everyone, the local body included, but no floating name tags
            takeCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        }
        void LateUpdate()
        {
            var state=session!=null?session.State:null;
            if(state!=null&&state.Phase=="Spinning"&&spinKey!=state.RoundId+":"+state.SpinSeed)Prepare(state);
            var beat=At(state,crew,session!=null?session.EstimatedSimulationSeconds:0);
            overlay.SetActive(beat.Stage!=Stage.Hidden&&!session.MenuOpen);
            FestivalCharacter tripper=null;
            if(beat.Stage!=Stage.Hidden)
            {
                string name=Name(state,state.TripperId),doses=beat.Dose+(beat.Dose==1?" DOSE":" DOSES");
                bool spinning=beat.Stage==Stage.People||beat.Stage==Stage.Dose;
                wheels.SetActive(spinning);letterbox.SetActive(!spinning);
                people.Disc.localEulerAngles=new Vector3(0,0,-beat.PeopleDegrees);people.Result.text=beat.PeopleLanded?name+" TRIPS":"";
                dose.Disc.localEulerAngles=new Vector3(0,0,-beat.DoseDegrees);dose.Result.text=beat.DoseLanded?doses:"";
                dose.Group.alpha=beat.Stage==Stage.People?.35f:1;
                caption.text=name+" TAKES "+doses+(beat.Stage==Stage.React?"\n"+Reactions[beat.Dose]:"");
                if(!spinning)tripper=session.WorldCharacter(state.TripperId);
            }
            Cut(tripper!=null&&tripper.gameObject.activeInHierarchy?tripper:null,beat,state);
        }
        // Freeze the wheels for this spin, so a friend dropping mid-spin cannot reshuffle the slices.
        void Prepare(RoundState state)
        {
            spinKey=state.RoundId+":"+state.SpinSeed;crew=Crew(state);
            Paint(people,Equal(crew.Count),CrewColors,crew.ConvertAll(id=>Name(state,id)));
        }
        static string Name(RoundState state,string id)
        {
            string name=(state.Players.Find(p=>p.Id==id)?.Name??"Friend").ToUpperInvariant();
            return name.Length>10?name.Substring(0,10):name;
        }
        // The camera cuts to the tripper at camp: a slow push-in while they take the dose (the existing Consume clip)
        // and react (the Panic clip, sized by the dose). A tripper out of view (inside a tent) keeps just the caption.
        void Cut(FestivalCharacter character,Beat beat,RoundState state)
        {
            if(character!=acting)
            {
                if(acting!=null){acting.Beat="";acting.AlwaysHighDetail=false;}
                acting=character;
                if(acting!=null)
                {
                    acting.AlwaysHighDetail=true;focusHeight=1.35f;takeYaw=TakeYaw(state);
                    foreach(var bone in acting.GetComponentsInChildren<Transform>())if(bone.name=="Head"){focusHeight=bone.position.y-acting.transform.position.y;break;}
                }
            }
            takeCamera.enabled=shot.enabled=acting!=null;
            if(acting==null){if(shotTexture!=null){takeCamera.targetTexture=null;shot.texture=null;Destroy(shotTexture);shotTexture=null;}return;}
            if(shotTexture==null||shotTexture.width!=Screen.width||shotTexture.height!=Screen.height)
            {
                if(shotTexture!=null)Destroy(shotTexture);
                shotTexture=new RenderTexture(Screen.width,Screen.height,24){name="Spinner take shot"};
                takeCamera.targetTexture=shotTexture;shot.texture=shotTexture;
            }
            acting.Beat=beat.Stage==Stage.Take?"TakeDose":"DoseReaction";acting.BeatStrength=beat.Reaction;
            var body=acting.transform;var focus=body.position+Vector3.up*focusHeight;
            float push=Mathf.InverseLerp(TakeStarts,(float)FestivalSimulation.SpinSeconds,beat.Elapsed);
            takeCamera.transform.position=focus+Quaternion.Euler(0,takeYaw,0)*Vector3.forward*Mathf.Lerp(TakeFar,TakeNear,push)+Vector3.up*.1f;
            takeCamera.transform.LookAt(focus);
            // ponytail: the first-person camera keeps rendering underneath for these 3 s; disable it if camp frame time matters.
        }
        // Nobody can move while Spinning and every client builds the same static world colliders, so every client picks the
        // same angle from the public positions. A line through scenery (the trailhead posts stand inside the gate's 3.2 m Ready
        // circle) counts as no clearance at all, so if no angle clears everything the clearest one wins, preferring friends to posts.
        // ponytail: LineOfSight runs at 1.2 m; something hung at face height with nothing under it slips past, cast at the camera's height then.
        static float TakeYaw(RoundState state)
        {
            var tripper=state.Players.Find(p=>p.Id==state.TripperId);if(tripper==null)return 0;
            var face=new Vector2(tripper.X,tripper.Z);float best=tripper.Yaw,widest=-1;
            foreach(var angle in TakeAngles)
            {
                float yaw=tripper.Yaw+angle,clearance=float.MaxValue;
                var camera=face+new Vector2(Mathf.Sin(yaw*Mathf.Deg2Rad),Mathf.Cos(yaw*Mathf.Deg2Rad))*TakeFar;
                foreach(var p in state.Players)if(p!=tripper&&p.Connected&&p.CampVisitId=="")clearance=Mathf.Min(clearance,Distance(new Vector2(p.X,p.Z),camera,face));
                // Both ways, so a camera that would start inside a post is caught too. The push-in stays on this line.
                if(!FestivalSession.LineOfSight(camera.x,camera.y,face.x,face.y)||!FestivalSession.LineOfSight(face.x,face.y,camera.x,camera.y))clearance=0;
                if(clearance>TakeClearance)return yaw;
                if(clearance>widest){best=yaw;widest=clearance;}
            }
            return best;
        }
        static float Distance(Vector2 point,Vector2 from,Vector2 to)
        {
            var line=to-from;return Vector2.Distance(point,from+line*Mathf.Clamp01(Vector2.Dot(point-from,line)/line.sqrMagnitude));
        }
        void Paint(Wheel wheel,IReadOnlyList<int> weights,Color[] colors,List<string> labels)
        {
            if(wheel.Texture!=null)Destroy(wheel.Texture);
            wheel.Texture=WheelTexture(weights,colors);wheel.Image.texture=wheel.Texture;
            foreach(Transform old in wheel.Disc)Destroy(old.gameObject);
            float total=0,start=0;foreach(var w in weights)total+=w;
            for(int i=0;i<weights.Count;i++)
            {
                float span=weights[i]*360f/total,center=start+span*.5f;start+=span;
                // Names read outward along their slice and turn with the wheel.
                var label=Label(wheel.Disc,"Slice "+labels[i],weights.Count>5?26:34);label.color=Ink;label.text=labels[i];
                label.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(center*Mathf.Deg2Rad),Mathf.Cos(center*Mathf.Deg2Rad))*WheelSize*.3f;
                label.rectTransform.localEulerAngles=new Vector3(0,0,90-center);
            }
        }
        static Texture2D WheelTexture(IReadOnlyList<int> weights,Color[] colors)
        {
            const int size=256;const float radius=size*.5f;
            float total=0;foreach(var w in weights)total+=w;
            var edges=new List<float>();float edge=0;if(weights.Count>1)foreach(var w in weights){edges.Add(edge);edge+=w*360f/total;}
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=x+.5f-radius,dy=y+.5f-radius,distance=Mathf.Sqrt(dx*dx+dy*dy);
                float angle=Mathf.Repeat(Mathf.Atan2(dx,dy)*Mathf.Rad2Deg,360);
                bool divider=false;foreach(var e in edges)divider|=Mathf.Abs(Mathf.DeltaAngle(angle,e))*Mathf.Deg2Rad*distance<1.4f;
                var color=divider||distance>radius-5||distance<radius*.09f?Ink:colors[SliceAt(weights,angle)%colors.Length];
                color.a=Mathf.Clamp01(radius-distance);
                pixels[y*size+x]=color;
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        Wheel MakeWheel(Transform parent,string name,float x,string title)
        {
            var holder=new GameObject(name,typeof(RectTransform),typeof(CanvasGroup));holder.transform.SetParent(parent,false);
            var rect=(RectTransform)holder.transform;rect.anchorMin=rect.anchorMax=new Vector2(x,.5f);rect.sizeDelta=new Vector2(WheelSize,WheelSize);
            var disc=new GameObject("Disc",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));disc.transform.SetParent(holder.transform,false);
            var discRect=(RectTransform)disc.transform;discRect.sizeDelta=new Vector2(WheelSize,WheelSize);disc.GetComponent<RawImage>().raycastTarget=false;
            // A diamond whose lower tip bites into the rim marks the winning slice.
            var pointer=Panel(holder.transform,"Pointer",Paper,new Vector2(.5f,1),new Vector2(.5f,1));
            var tip=(RectTransform)pointer.transform;tip.sizeDelta=new Vector2(44,44);tip.anchoredPosition=new Vector2(0,6);tip.localEulerAngles=new Vector3(0,0,45);
            var heading=Label(holder.transform,"Title",46);heading.text=title;heading.rectTransform.anchoredPosition=new Vector2(0,WheelSize*.5f+80);
            var result=Label(holder.transform,"Result",56);result.color=Orange;result.rectTransform.anchoredPosition=new Vector2(0,-WheelSize*.5f-64);
            return new Wheel{Disc=discRect,Image=disc.GetComponent<RawImage>(),Result=result,Group=holder.GetComponent<CanvasGroup>()};
        }
        static GameObject Panel(Transform parent,string name,Color color,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return go;
        }
        Text Label(Transform parent,string name,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.color=Paper;
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;return text;
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        void OnDestroy()
        {
            if(people?.Texture!=null)Destroy(people.Texture);
            if(dose?.Texture!=null)Destroy(dose.Texture);
            if(shotTexture!=null)Destroy(shotTexture);
        }
    }
}
