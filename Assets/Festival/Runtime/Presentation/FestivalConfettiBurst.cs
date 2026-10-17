using System;
using System.Collections.Generic;
using Festival.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>Each confetti burst the session reports (FestivalSession.ConfettiFired) sprays about two seconds of coloured paper from
    /// the shooter's cannon with a pop, both made in code. One world-space particle system holds every burst, on a layer the
    /// first-person camera draws though it hides your own body. The pop goes through the effects volume only: FestivalTrip applies
    /// master and focus to the listener. Burst fires one anywhere, spending nothing, for a camp gag.</summary>
    public sealed class FestivalConfettiBurst : MonoBehaviour
    {
        /// <summary>The paper is a child of this name; each pop plays from a child of PopName.</summary>
        public const string PaperName="Confetti paper",PopName="Confetti pop";
        /// <summary>How far a pop carries, in metres, as far as speech and the inhale.</summary>
        public const float Range=15;
        public const int Pieces=48;
        const int Rate=24000;
        const double PopSeconds=.45,Loudness=.8;
        const float Lifetime=2;
        // Without a cannon in view the burst leaves from about the shooter's right hand, in body space.
        static readonly Vector3 HandReach=new Vector3(.2f,1.35f,.35f);
        static readonly Color[] Paper={new Color(.93f,.26f,.47f),new Color(.98f,.68f,.21f),new Color(.16f,.78f,.64f),new Color(.23f,.42f,.73f),new Color(.95f,.92f,.82f)};

        readonly List<AudioSource> pops=new List<AudioSource>();
        readonly Dictionary<string,Pose> aimed=new Dictionary<string,Pose>();
        FestivalSession session;
        ParticleSystem paper;
        Material material;
        Mesh piece;
        AudioClip pop;
        uint seed=0x9e3779b9;

        /// <summary>How many bursts this presenter has shown.</summary>
        public int Played {get;private set;}

        /// <summary>The pop: a bright knock dropping in pitch over a low thump, then a crackle of paper dying away. The same samples
        /// every time, from a private generator.</summary>
        public static AudioClip CreatePop()
        {
            var samples=new float[(int)(Rate*PopSeconds)];uint noiseSeed=0x243f6a88;
            double knock=0,thump=0,low=0,peak=0;
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)Rate,noise=Next(ref noiseSeed)*2-1;
                knock+=2*Math.PI*(380+1100*Math.Exp(-t/.012))/Rate;thump+=2*Math.PI*(60+110*Math.Exp(-t/.02))/Rate;low+=(noise-low)*.3;
                double sound=Math.Sin(knock)*Math.Exp(-t/.03)+.7*Math.Sin(thump)*Math.Exp(-t/.05)+(noise-low)*(.5*Math.Exp(-t/.004)+.3*Math.Exp(-t/.18));
                sound*=Math.Min(1,(PopSeconds-t)/.02);
                samples[n]=(float)sound;peak=Math.Max(peak,Math.Abs(sound));
            }
            for(int n=0;n<samples.Length;n++)samples[n]=(float)(samples[n]*Loudness/peak);
            var made=AudioClip.Create(PopName,samples.Length,1,Rate,false);made.SetData(samples,0);return made;
        }
        static float Next(ref uint seed){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return (seed&0xffffff)/16777215f;}
        Vector3 Jitter()=>new Vector3(Next(ref seed)*2-1,Next(ref seed)*2-1,Next(ref seed)*2-1);

        void Awake()
        {
            session=GetComponent<FestivalSession>();pop=CreatePop();
            var holder=new GameObject(PaperName);holder.transform.SetParent(transform,false);
            paper=holder.AddComponent<ParticleSystem>();
            var main=paper.main;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=Pieces*8;
            main.gravityModifier=.35f;main.startRotation3D=true;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=paper.emission;emission.enabled=false;
            var shape=paper.shape;shape.enabled=false;
            // Paper catches the air: drag slows its fall to a drift, and a little noise flutters it about.
            var drag=paper.limitVelocityOverLifetime;drag.enabled=true;drag.limit=100;drag.drag=3;drag.multiplyDragByParticleSize=false;drag.multiplyDragByParticleVelocity=false;
            var flutter=paper.noise;flutter.enabled=true;flutter.strength=.5f;flutter.frequency=1.2f;
            var fade=paper.colorOverLifetime;fade.enabled=true;
            var alpha=new Gradient();alpha.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.75f),new GradientAlphaKey(0,1)});
            fade.color=alpha;
            piece=new Mesh{name=PaperName,vertices=new[]{new Vector3(-.5f,-.3f,0),new Vector3(.5f,-.3f,0),new Vector3(.5f,.3f,0),new Vector3(-.5f,.3f,0)},
                uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},triangles=new[]{0,2,1,0,3,2}};
            // The built-in Sprites/Default shader: unlit, two-sided, tinted by each piece's colour and always in player builds.
            material=new Material(Shader.Find("Sprites/Default")){name=PaperName};
            var look=holder.GetComponent<ParticleSystemRenderer>();look.renderMode=ParticleSystemRenderMode.Mesh;look.mesh=piece;look.alignment=ParticleSystemRenderSpace.World;
            look.sharedMaterial=material;look.shadowCastingMode=ShadowCastingMode.Off;look.receiveShadows=false;
        }
        void OnEnable(){if(session!=null)session.ConfettiFired+=Fired;}
        void OnDisable(){if(session!=null)session.ConfettiFired-=Fired;Clear();}

        /// <summary>A burst at `at`: paper sprayed up along `aim` and a pop, heard within Range of the eye. A camp gag calls this
        /// directly; it spends nothing and fools nobody.</summary>
        public void Burst(Vector3 at,Vector3 aim)
        {
            bool audible=Show(at,aim);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Trace("spot",audible);
#endif
        }
        /// <summary>Notes where playerId's cannon fires from now, before the actors refresh their gear, so a burst that spends the last
        /// cannon still leaves from its muzzle. FestivalSession calls it for each drawn player with a new burst.</summary>
        public void Aim(string playerId){if(Muzzle(playerId,out var muzzle))aimed[playerId]=muzzle;}

        // One burst for each report, however many it counts: two cannons in one host step would only read as one bigger burst.
        void Fired(string playerId,int bursts)
        {
            bool cannon=aimed.TryGetValue(playerId,out var muzzle)||Muzzle(playerId,out muzzle);
            bool audible=Show(muzzle.position,muzzle.up);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Trace((cannon?"cannon":"hand")+" bursts="+bursts,audible);
#endif
        }
        // Your own cannon is the one you see: in your first-person hands, or your body's over the shoulder. With none in view (a pose
        // that hides gear, or a use without it in hand) the burst leaves from about the shooter's hand.
        bool Muzzle(string playerId,out Pose muzzle)
        {
            if(playerId==session.LocalPlayerId&&FestivalHeldItem.Muzzle(session.ViewCamera.transform,out muzzle))return true;
            var body=session.WorldCharacter(playerId);
            if(body!=null&&FestivalHeldItem.Muzzle(body.transform,out muzzle))return true;
            muzzle=body!=null?new Pose(body.transform.TransformPoint(HandReach),body.transform.rotation):new Pose(session.EyePosition,Quaternion.identity);
            return false;
        }
        bool Show(Vector3 at,Vector3 aim)
        {
            var up=(aim.normalized*.6f+Vector3.up).normalized;
            for(int i=0;i<Pieces;i++)
            {
                var spray=new ParticleSystem.EmitParams{position=at,applyShapeToPosition=false,velocity=(up+Jitter()*.45f).normalized*(3.5f+2.5f*Next(ref seed)),
                    startLifetime=Lifetime*(.9f+.2f*Next(ref seed)),startSize=.05f+.03f*Next(ref seed),rotation3D=Jitter()*180,angularVelocity3D=Jitter()*540,startColor=Paper[i%Paper.Length]};
                paper.Emit(spray,1);
            }
            bool audible=Vector3.Distance(at,session.EyePosition)<=Range;
            if(audible){var voice=Voice();voice.transform.position=at;FestivalSettings.ApplyEffects(voice,session.Profile?.Data);voice.Play();}
            Played++;return audible;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Trace(string from,bool audible){if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("Confetti","burst","from="+from+" pop="+(audible?"audible":"out_of_earshot")+" paper="+paper.particleCount);}
#endif
        // A pop that has finished plays the next; more play at once only while bursts overlap.
        AudioSource Voice()
        {
            foreach(var idle in pops)if(!idle.isPlaying)return idle;
            var voice=new GameObject(PopName).AddComponent<AudioSource>();voice.transform.SetParent(transform,false);
            voice.playOnAwake=false;voice.clip=pop;voice.spatialBlend=1;voice.dopplerLevel=0;
            voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=1;voice.maxDistance=Range;
            pops.Add(voice);return voice;
        }

        // After FestivalSession.Update has reported this frame's bursts: every pop follows its slider, and leaving clears the lot.
        void LateUpdate()
        {
            aimed.Clear();
            if(session==null||session.State==null){Clear();return;}
            foreach(var voice in pops)FestivalSettings.ApplyEffects(voice,session.Profile?.Data);
        }
        // Leaving the game clears the paper in the air and lets every pop go.
        void Clear()
        {
            if(paper==null||paper.particleCount==0&&pops.Count==0)return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("Confetti","cleanup","paper="+paper.particleCount+" pops="+pops.Count);
#endif
            paper.Clear();
            foreach(var voice in pops)if(voice!=null)Dispose(voice.gameObject);
            pops.Clear();
        }
        void OnDestroy(){Clear();if(pop!=null)Dispose(pop);if(material!=null)Dispose(material);if(piece!=null)Dispose(piece);}
        static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
