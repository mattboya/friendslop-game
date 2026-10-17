using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>MINI-3 (docs/MINIGAMES_2026-09-30.md): the Light Show as this client draws it while its game runs. The booth's stranger
    /// is the glover: facing you, gloving with a light trail from each hand in the glover's colour of the moment, on the game's own
    /// clock while the festival holds (FestivalCharacter.SetAnimationClock). Behind the glover, as you see it, the script everyone
    /// shares plays in its windows: festivalgoers walking across (one carrying a prop), coloured lights sweeping a pool of light over
    /// the ground and shapes flashed on a sign. A dosed viewer's own record adds exactly their dose's fakes, which no other view
    /// carries, each with the vision tells: a wrong shadow (FestivalShadowTellView), a colour slightly off and a shimmer while the view
    /// moves. Nothing here collides or touches the rules. Comfort settings keep every answer: reduced motion only shortens the trails
    /// and drops the shapes' pop, and the lights' glow makes up for the brightness setting. ponytail: rigs and primitives, as the art is
    /// frozen.</summary>
    public sealed class FestivalLightShowView : MonoBehaviour
    {
        public const string RootName="Light show";
        // The show's frame stands on the booth facing the audience. Passers cross PasserSpan m either side of the glover, in Lanes
        // lanes from PasserBehind m behind them, LaneGap apart.
        private const float PasserSpan=4,PasserBehind=3,LaneGap=1.2f;
        private const int Lanes=3;
        // A sweep is a beam from a rig BeamHigh m up and BeamBack m behind the glover down to its pool of light, which crosses
        // SweepSpan m either side, SweepBehind m back.
        private const float SweepSpan=3.5f,SweepBehind=1.5f,BeamHigh=7,BeamBack=6,BeamWidth=.08f;
        // The sign: a SignWide board SignBehind m behind the glover, its middle SignHigh m up; a shape ShapeSize across pops in over
        // PopSeconds.
        private const float SignBehind=2,SignHigh=3,SignWide=1.1f,ShapeSize=.8f,PopSeconds=.2f;
        // Each trail is its hand's last TrailSeconds (ReducedTrailSeconds with reduced motion), TrailWidth wide; each dose step makes the
        // trails and the lamp DoseGrow bigger.
        private const float TrailSeconds=.35f,ReducedTrailSeconds=.15f,TrailWidth=.05f,DoseGrow=.25f;
        private const int TrailPoints=64;
        // The lights glow at Glow of their colour, with the brightness setting's exposure (2 to its power) taken back out.
        private const float Glow=.8f;
        // The show's lamp, LampHigh m over the glover, lights the passers at night.
        private const float LampHigh=3,LampRange=12,LampIntensity=2;
        // A fake passer is cast in this tint all over.
        private static readonly Color FakeCast=new Color(.84f,1f,.86f);
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),EmissionColor=Shader.PropertyToID("_EmissionColor");

        private sealed class Extra
        {
            public Transform Root,Shown; public FestivalCharacter Actor; public FestivalShadowTellView Shadow; public Material Material;
            public LightShowEvent Event; public LightShowFake Fake; public Color Colour; public Vector3 Size; public int Lane;
        }
        private sealed class Trail { public Transform Hand; public LineRenderer Line; public readonly Vector3[] At=new Vector3[TrailPoints]; public readonly float[] When=new float[TrailPoints]; public int Count; }
        private readonly List<Extra> extras=new List<Extra>();
        private readonly Trail[] trails={new Trail(),new Trail()};
        private readonly FestivalVisionMarkers.MotionTracker motion=new FestivalVisionMarkers.MotionTracker();
        private static readonly Dictionary<string,Mesh> shapes=new Dictionary<string,Mesh>();
        private Transform show,sign,shape;
        private MeshFilter shapeFilter;
        private MeshRenderer shapeRenderer;
        private Light lamp;
        private Material trailMaterial,shapeMaterial,shade,glow;
        private Color trailColour;
        private FestivalCharacter glover;
        // The show built: whose view of which level's game, and how big its script and fakes were.
        private string builtRound,builtViewer;
        private int builtLevel,builtEvents,builtFakes;
        private float played=-1;
        private FestivalSession session;
        private FestivalMinigameView minigame;
        private Transform strangerBooth;
        private FestivalCharacter stranger;

        // After FestivalSession.Update has taken this frame's view of the round.
        private void LateUpdate()
        {
            if(session==null){session=GetComponent<FestivalSession>();minigame=GetComponent<FestivalMinigameView>();if(session==null)return;}
            var booth=minigame!=null?minigame.Booth:null;
            if(booth!=strangerBooth){strangerBooth=booth;stranger=booth!=null?booth.GetComponentInChildren<FestivalCharacter>(true):null;}
            var state=session.State;var view=session.ViewCamera!=null?session.ViewCamera.transform:null;
            // You watch from where you stand, still through the game; the camera stands in for a view without you in it.
            var audience=view!=null?view.position:Vector3.zero;
            if(state!=null)foreach(var p in state.Players)if(p.Id==session.LocalPlayerId){audience=new Vector3(p.X,0,p.Z);break;}
            var data=session.Profile?.Data;
            Apply(state,session.LocalPlayerId,session.EstimatedMinigameSeconds,stranger,audience,view,Time.unscaledTime,Time.unscaledDeltaTime,data!=null&&data.ReducedMotion,data!=null?data.Brightness:0);
        }
        // Switched off mid-game, the glover is the world's again until it comes back.
        private void OnDisable(){ReleaseGlover();if(show!=null)show.gameObject.SetActive(false);}

        /// <summary>Draws the Light Show in state, as viewerId's view carries it, at seconds on its game's clock: glover gloves (null:
        /// no glover drawn) facing audience, the point the show is laid out for. view drives the fakes' shimmer and turns the shadows;
        /// time and deltaTime are frame time. Outside a Light Show it draws nothing and gives the glover back to the world.</summary>
        public void Apply(RoundState state,string viewerId,double seconds,FestivalCharacter glover,Vector3 audience,Transform view,float time,float deltaTime,bool reducedMotion=false,float brightness=0)
        {
            var at=state==null?null:FestivalSimulation.BoothAt(state);
            if(at==null||state.Phase!=FestivalSimulation.MinigamePhase||state.Booth.Kind!=FestivalSimulation.LightShowGame){Release();return;}
            var script=state.Booth.LightShow??new LightShowState();LightShowPlayer mine=null;
            foreach(var record in script.Players)if(record.PlayerId==viewerId){mine=record;break;}
            Build(state,script,mine,viewerId);
            float t=Mathf.Max((float)seconds,played),dt=played<0?0:t-played;played=t;
            bool during=t<LightShow.ShowSeconds;int dose=mine!=null?Mathf.Clamp(mine.Dose,0,4):0;float exposure=Mathf.Pow(2,-brightness);
            var spot=new Vector3(at.X,0,at.Z);var toward=audience-spot;toward.y=0;
            show.SetPositionAndRotation(spot,toward.sqrMagnitude>.01f?Quaternion.LookRotation(toward):Quaternion.identity);
            if(!show.gameObject.activeSelf)show.gameObject.SetActive(true);
            Glove(glover,t,dt,during);
            float shimmer=motion.Step(view,deltaTime);
            for(int i=0;i<extras.Count;i++)Place(extras[i],i,t,dt,view,shimmer,time,exposure);
            Sign(script,t,during,reducedMotion,exposure);
            LightShowEvent lit=null;if(during)foreach(var e in script.Events)if(e.Kind==LightShowEventKind.Glover&&t>=e.StartsAt&&t<e.EndsAt)lit=e;
            Trails(lit,t,dt,reducedMotion?ReducedTrailSeconds:TrailSeconds,TrailWidth*(1+DoseGrow*dose),exposure);
            if(lamp.enabled!=during)lamp.enabled=during;lamp.intensity=LampIntensity*(1+DoseGrow*dose);
        }

        // The booth's stranger turns to the audience and gloves through the show, then rests, on the game's clock throughout.
        private void Glove(FestivalCharacter next,float t,float dt,bool during)
        {
            if(next!=glover)
            {
                ReleaseGlover();glover=next;
                foreach(var trail in trails){trail.Hand=null;trail.Count=0;}
                if(glover!=null)foreach(var bone in glover.GetComponentsInChildren<Transform>(true))
                {
                    if(bone.name=="HandL")trails[0].Hand=bone;
                    else if(bone.name=="HandR")trails[1].Hand=bone;
                }
            }
            if(glover==null)return;
            glover.transform.rotation=show.rotation;
            glover.Pose=during?FestivalCharacter.GlovePose:"Idle";
            glover.SetAnimationClock(t,dt);
        }
        private void ReleaseGlover()
        {
            if(glover==null)return;
            glover.ClearAnimationClock();glover.Pose="Idle";glover.transform.localRotation=Quaternion.identity;glover=null;
        }

        // A passer walks the width of the show across its window, facing the way it goes; a sweep's pool of light crosses the ground
        // under its beam. A fake shimmers while the view moves.
        private void Place(Extra extra,int index,float t,float dt,Transform view,float shimmer,float time,float exposure)
        {
            var e=extra.Event;bool on=t>=e.StartsAt&&t<e.EndsAt;
            if(!on){if(extra.Root.gameObject.activeSelf)extra.Root.gameObject.SetActive(false);return;}
            float along=Mathf.InverseLerp(e.StartsAt,e.EndsAt,t),side=e.Side<0?-1:1;
            float scale=extra.Fake!=null&&extra.Fake.MovementShimmer?FestivalVisionMarkers.ShimmerScale(shimmer,time,index):1;
            bool wrong=extra.Fake!=null;var shape=extra.Fake!=null&&extra.Fake.Shadow==LightShowShadowTell.Goblin?FestivalVisionMarkers.FakeShadow.Goblin:FestivalVisionMarkers.FakeShadow.Small;
            if(extra.Actor!=null)
            {
                extra.Root.localPosition=new Vector3(Mathf.Lerp(side*PasserSpan,-side*PasserSpan,along),0,-(PasserBehind+extra.Lane*LaneGap));
                extra.Shown.localRotation=Quaternion.LookRotation(new Vector3(-side,0,0));extra.Shown.localScale=extra.Size*scale;
                if(!extra.Root.gameObject.activeSelf)extra.Root.gameObject.SetActive(true);
                extra.Actor.SetAnimationClock(t,dt);
            }
            else
            {
                extra.Root.localPosition=new Vector3(Mathf.Lerp(side*SweepSpan,-side*SweepSpan,along),0,-SweepBehind);
                if(!extra.Root.gameObject.activeSelf)extra.Root.gameObject.SetActive(true);
                var ground=extra.Root.position;var rig=show.TransformPoint(new Vector3(0,BeamHigh,-BeamBack));var beam=rig-ground;
                extra.Shown.SetPositionAndRotation((rig+ground)/2,Quaternion.FromToRotation(Vector3.up,beam));
                extra.Shown.localScale=new Vector3(BeamWidth*scale,beam.magnitude/2,BeamWidth*scale);
                extra.Material.SetColor(EmissionColor,extra.Colour*(Glow*exposure));
            }
            extra.Shadow.Show(wrong,shape);extra.Shadow.Place(extra.Root.position,view);extra.Shadow.Root.localScale=Vector3.one*scale;
        }

        private void Sign(LightShowState script,float t,bool during,bool reducedMotion,float exposure)
        {
            if(sign.gameObject.activeSelf!=during)sign.gameObject.SetActive(during);
            LightShowEvent flashed=null;
            if(during)foreach(var e in script.Events)if(e.Kind==LightShowEventKind.Sign&&t>=e.StartsAt&&t<e.EndsAt){flashed=e;break;}
            shapeRenderer.enabled=flashed!=null;
            if(flashed==null)return;
            var mesh=ShapeMesh(flashed.Shape);if(shapeFilter.sharedMesh!=mesh)shapeFilter.sharedMesh=mesh;
            float pop=reducedMotion?1:Mathf.SmoothStep(0,1,(t-flashed.StartsAt)/PopSeconds);
            shape.localScale=new Vector3(ShapeSize*pop,ShapeSize*pop,1);
            shapeMaterial.SetColor(EmissionColor,shapeMaterial.color*(Glow*exposure));
        }

        // Each trail holds its hand's places over its last `reach` seconds, newest first, and tapers away behind.
        private void Trails(LightShowEvent lit,float t,float dt,float reach,float width,float exposure)
        {
            if(lit!=null)
            {
                var colour=ColourOf(lit.Colour);
                if(colour!=trailColour){trailColour=colour;trailMaterial.color=colour;}
                trailMaterial.SetColor(EmissionColor,colour*(Glow*exposure));
            }
            foreach(var trail in trails)
            {
                bool shown=lit!=null&&trail.Hand!=null;
                if(trail.Line.gameObject.activeSelf!=shown)trail.Line.gameObject.SetActive(shown);
                if(!shown){trail.Count=0;continue;}
                if(dt>0||trail.Count==0)
                {
                    for(int i=Mathf.Min(trail.Count,TrailPoints-1);i>0;i--){trail.At[i]=trail.At[i-1];trail.When[i]=trail.When[i-1];}
                    trail.At[0]=trail.Hand.position;trail.When[0]=t;trail.Count=Mathf.Min(trail.Count+1,TrailPoints);
                }
                while(trail.Count>1&&trail.When[trail.Count-1]<t-reach)trail.Count--;
                trail.Line.widthMultiplier=width;trail.Line.positionCount=trail.Count;
                for(int i=0;i<trail.Count;i++)trail.Line.SetPosition(i,trail.At[i]);
            }
        }

        /// <summary>The colour a script's colour name is shown in: bright and far apart in hue, so each reads as its name.</summary>
        private static Color ColourOf(string name)=>name switch
        {
            "Red"=>new Color(1f,.13f,.1f),
            "Orange"=>new Color(1f,.5f,.08f),
            "Yellow"=>new Color(1f,.88f,.12f),
            "Green"=>new Color(.15f,.92f,.3f),
            "Blue"=>new Color(.15f,.4f,1f),
            "Purple"=>new Color(.65f,.2f,1f),
            _=>Color.white,
        };

        // Once per show: a rig or a light for each event of the script and each of this viewer's own fakes, with the sign, the trails
        // and the lamp made the first time.
        private void Build(RoundState state,LightShowState script,LightShowPlayer mine,string viewerId)
        {
            int fakes=mine!=null?mine.Fakes.Count:0;
            if(show!=null&&builtRound==state.RoundId&&builtLevel==state.LevelIndex&&builtViewer==viewerId&&builtEvents==script.Events.Count&&builtFakes==fakes)return;
            if(show==null)Frame();
            Discard();builtRound=state.RoundId;builtLevel=state.LevelIndex;builtViewer=viewerId;builtEvents=script.Events.Count;builtFakes=fakes;
            var pool=Pool(FestivalNightLighting.IsNight(state));int passers=0,sweeps=0,lane=0;
            foreach(var e in script.Events)
            {
                if(e.Kind==LightShowEventKind.Passer)extras.Add(Passer("Passer "+passers++,e,null,lane++,pool));
                else if(e.Kind==LightShowEventKind.Sweep)extras.Add(Sweep("Sweep "+sweeps++,e,null));
            }
            for(int j=0;j<fakes;j++)
            {
                var fake=mine.Fakes[j];
                if(fake.Event.Kind==LightShowEventKind.Passer)extras.Add(Passer("Fake passer "+j,fake.Event,fake,lane++,pool));
                else if(fake.Event.Kind==LightShowEventKind.Sweep)extras.Add(Sweep("Fake sweep "+j,fake.Event,fake));
            }
        }
        private void Frame()
        {
            show=new GameObject(RootName).transform;show.SetParent(transform,false);show.gameObject.SetActive(false);
            sign=new GameObject("Shape sign").transform;sign.SetParent(show,false);sign.localPosition=new Vector3(0,SignHigh,-SignBehind);sign.localRotation=Quaternion.Euler(0,180,0);
            Piece(sign,"Board",PrimitiveType.Cube,FestivalArtView.MaterialFor("Dark")).localScale=new Vector3(SignWide,SignWide,.04f);
            shapeMaterial=Glowing(new Color(.96f,.94f,.84f));shapeMaterial.SetFloat("_Cull",(float)CullMode.Off);
            shape=new GameObject("Shape").transform;shape.SetParent(sign,false);shape.localPosition=new Vector3(0,0,-.03f);
            shapeFilter=shape.gameObject.AddComponent<MeshFilter>();shapeRenderer=shape.gameObject.AddComponent<MeshRenderer>();shapeRenderer.sharedMaterial=shapeMaterial;shapeRenderer.shadowCastingMode=ShadowCastingMode.Off;shapeRenderer.enabled=false;
            trailMaterial=Glowing(Color.white);trailColour=Color.white;
            for(int i=0;i<2;i++)
            {
                var line=new GameObject(i==0?"Glover trail L":"Glover trail R").AddComponent<LineRenderer>();line.transform.SetParent(show,false);line.gameObject.SetActive(false);
                line.useWorldSpace=true;line.sharedMaterial=trailMaterial;line.widthCurve=AnimationCurve.Linear(0,1,1,0);line.numCapVertices=2;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.positionCount=0;trails[i].Line=line;
            }
            lamp=new GameObject("Show light").AddComponent<Light>();lamp.transform.SetParent(show,false);lamp.transform.localPosition=Vector3.up*LampHigh;
            lamp.type=LightType.Point;lamp.range=LampRange;lamp.color=new Color(1f,.9f,.8f);lamp.shadows=LightShadows.None;lamp.enabled=false;
        }
        private Extra Passer(string name,LightShowEvent e,LightShowFake fake,int lane,Material pool)
        {
            var root=new GameObject(name).transform;root.SetParent(show,false);root.gameObject.SetActive(false);
            bool carries=fake==null&&e.Prop!="",backpack=carries&&e.Prop=="Backpack";
            var actor=FestivalCharacter.Create(root,PasserName(e.Look,backpack),Color.white);actor.Pose="Walk";
            var size=Vector3.Scale(Vector3.one*.82f,actor.ShapeScale);actor.transform.localScale=size;
            if(carries&&!backpack)Prop(actor.transform,e.Prop);
            if(fake!=null&&fake.TintTell)
            {
                var block=new MaterialPropertyBlock();block.SetColor(BaseColor,FakeCast);
                foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))renderer.SetPropertyBlock(block);
            }
            return new Extra{Root=root,Shown=actor.transform,Actor=actor,Shadow=FestivalShadowTellView.Build(root,pool),Event=e,Fake=fake,Size=size,Lane=lane%Lanes};
        }
        private Extra Sweep(string name,LightShowEvent e,LightShowFake fake)
        {
            var root=new GameObject(name).transform;root.SetParent(show,false);root.gameObject.SetActive(false);
            var colour=ColourOf(e.Colour);if(fake!=null&&fake.TintTell)colour=FestivalVisionMarkers.FakeTint(colour);
            var material=Glowing(colour);var beam=Piece(root,"Beam",PrimitiveType.Cylinder,material);
            return new Extra{Root=root,Shown=beam,Shadow=FestivalShadowTellView.Build(root,material),Material=material,Event=e,Fake=fake,Colour=colour};
        }

        // A look's festivalgoer, wearing the backpack only when it is the prop, so no other passer could be its answer.
        private static readonly string[] passerNames=new string[16];
        private static string PasserName(int look,bool backpack)
        {
            int slot=Mathf.Clamp(look,0,7)*2+(backpack?1:0);if(passerNames[slot]!=null)return passerNames[slot];
            for(int n=0;;n++)
            {
                var name="Light show passer "+look+"."+n;
                if((FestivalAppearance.For(name).Accessory==0)==backpack)return passerNames[slot]=name;
            }
        }
        // The prop, held at the right hand from a few primitives, in metres whatever the body's size.
        private static void Prop(Transform body,string prop)
        {
            var grip=new GameObject("Prop "+prop).transform;grip.SetParent(body,false);var inverse=FestivalHeldItem.Unscale(body);
            grip.localScale=inverse;grip.localPosition=Vector3.Scale(new Vector3(.28f,.95f,.1f),inverse);
            switch(prop)
            {
                case "Umbrella":Part(grip,PrimitiveType.Cylinder,"Metal",new Vector3(0,.5f,0),new Vector3(.025f,.5f,.025f));Part(grip,PrimitiveType.Sphere,"PaintRose",new Vector3(0,1.05f,0),new Vector3(1,.32f,1));break;
                case "Flamingo":
                    Part(grip,PrimitiveType.Cylinder,"Wood",new Vector3(0,.35f,0),new Vector3(.02f,.35f,.02f));Part(grip,PrimitiveType.Sphere,"Rose",new Vector3(0,.78f,0),new Vector3(.22f,.16f,.34f));
                    Part(grip,PrimitiveType.Cylinder,"Rose",new Vector3(0,.98f,.12f),new Vector3(.035f,.16f,.035f));Part(grip,PrimitiveType.Sphere,"Rose",new Vector3(0,1.15f,.14f),new Vector3(.09f,.08f,.12f));
                    Part(grip,PrimitiveType.Cube,"Dark",new Vector3(0,1.13f,.22f),new Vector3(.03f,.03f,.08f));break;
                case "Lantern":Part(grip,PrimitiveType.Cylinder,"Metal",new Vector3(0,.1f,0),new Vector3(.012f,.1f,.012f));Part(grip,PrimitiveType.Cylinder,"StageGlowGold",new Vector3(0,-.12f,0),new Vector3(.16f,.11f,.16f));Part(grip,PrimitiveType.Cylinder,"Dark",new Vector3(0,0,0),new Vector3(.19f,.02f,.19f));break;
                case "Fan":Part(grip,PrimitiveType.Cube,"Wood",new Vector3(0,.09f,0),new Vector3(.03f,.18f,.02f));Part(grip,PrimitiveType.Cylinder,"PaintMint",new Vector3(0,.4f,0),new Vector3(.45f,.005f,.45f)).localRotation=Quaternion.Euler(90,0,0);break;
                case "Balloon":Part(grip,PrimitiveType.Cylinder,"White",new Vector3(0,.45f,0),new Vector3(.006f,.45f,.006f));Part(grip,PrimitiveType.Sphere,"StageGlowRed",new Vector3(0,1.15f,0),new Vector3(.38f,.45f,.38f));break;
            }
        }
        private static Transform Part(Transform parent,PrimitiveType shape,string look,Vector3 at,Vector3 size)
        {
            var part=Piece(parent,shape.ToString(),shape,FestivalArtView.MaterialFor(look));part.localPosition=at;part.localScale=size;return part;
        }
        // A primitive with no collider and no shadow of its own.
        private static Transform Piece(Transform parent,string name,PrimitiveType shape,Material material)
        {
            var piece=GameObject.CreatePrimitive(shape);piece.name=name;piece.transform.SetParent(parent,false);
            var collider=piece.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            var renderer=piece.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            return piece.transform;
        }
        private Material Glowing(Color colour)
        {
            var material=new Material(Resources.Load<Material>("FestivalLit")){color=colour,mainTexture=Texture2D.whiteTexture};
            material.EnableKeyword("_EMISSION");material.SetColor(EmissionColor,colour*Glow);return material;
        }
        // A show belongs to one level, so its passers' pools are picked once, when it is built.
        private Material Pool(bool night)
        {
            var pool=night?glow:shade;if(pool!=null)return pool;
            pool=FestivalShadowTellView.Material(night);
            return night?glow=pool:shade=pool;
        }

        // Each shape flat, a unit across, its front toward -z: a fan of triangles from the middle of its outline.
        private static Mesh ShapeMesh(string name)
        {
            if(shapes.TryGetValue(name,out var mesh)&&mesh!=null)return mesh;
            var outline=Outline(name);var vertices=new Vector3[outline.Length+1];var triangles=new int[outline.Length*3];var middle=Vector2.zero;
            foreach(var point in outline)middle+=point/outline.Length;
            vertices[0]=middle;
            for(int i=0;i<outline.Length;i++){vertices[i+1]=outline[i];triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%outline.Length+1;}
            mesh=new Mesh{name="Light show shape "+name,vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
            return shapes[name]=mesh;
        }
        // Clockwise outlines, as the audience sees them.
        private static Vector2[] Outline(string name)
        {
            switch(name)
            {
                case "Triangle":return new[]{new Vector2(0,.5f),new Vector2(.5f,-.37f),new Vector2(-.5f,-.37f)};
                case "Square":return new[]{new Vector2(-.42f,.42f),new Vector2(.42f,.42f),new Vector2(.42f,-.42f),new Vector2(-.42f,-.42f)};
                case "Diamond":return new[]{new Vector2(0,.5f),new Vector2(.34f,0),new Vector2(0,-.5f),new Vector2(-.34f,0)};
                case "Star":
                {
                    var star=new Vector2[10];
                    for(int i=0;i<10;i++){float angle=Mathf.PI/2-i*Mathf.PI/5;star[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(i%2==0?.5f:.21f);}
                    return star;
                }
                case "Heart":
                {
                    // The classic heart curve, starting at the dip between its lobes.
                    var heart=new Vector2[32];
                    for(int i=0;i<32;i++){float t=i*2*Mathf.PI/32,s=Mathf.Sin(t);heart[i]=new Vector2(16*s*s*s,13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t))/34;}
                    return heart;
                }
                default:
                {
                    var circle=new Vector2[32];
                    for(int i=0;i<32;i++){float angle=Mathf.PI/2-i*2*Mathf.PI/32;circle[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.45f;}
                    return circle;
                }
            }
        }

        // Out of a Light Show: nothing shows, the glover is the world's again and the next show builds afresh.
        private void Release()
        {
            ReleaseGlover();
            if(show!=null&&show.gameObject.activeSelf)show.gameObject.SetActive(false);
            if(extras.Count>0)Discard();
            builtRound=null;played=-1;foreach(var trail in trails)trail.Count=0;
        }
        private void Discard()
        {
            foreach(var extra in extras){if(extra.Root!=null)Dispose(extra.Root.gameObject);if(extra.Material!=null)Dispose(extra.Material);}
            extras.Clear();played=-1;
        }
        // Its objects go with it; the materials it made would not.
        private void OnDestroy()
        {
            ReleaseGlover();
            foreach(var extra in extras)if(extra.Material!=null)Dispose(extra.Material);
            foreach(var material in new[]{trailMaterial,shapeMaterial,shade,glow})if(material!=null)Dispose(material);
        }
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
