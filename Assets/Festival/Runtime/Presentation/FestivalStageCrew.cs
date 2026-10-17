using System.Collections.Generic;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>CROWD-7: the main stage's crew of six round the resident DJ, on both festivals: two hype dancers at the front lip, two
    /// go-go dancers at the side edges, an MC beside the DJ and a photographer turning between the crowd and the DJ. They dance on the
    /// stage song's beat (FestivalStageAudio.Beat), each with its own clock, step and style, and hold while the world holds (a
    /// minigame). They show only with the festival and are culled like the ambient crowd. Presentation only: nothing on them
    /// collides and the rules never see them.</summary>
    public sealed class FestivalStageCrew : MonoBehaviour
    {
        public const string Prefix="stage_crew_";
        // One performer: where on the deck they stand, how they move, their dance style and their step on the beat.
        readonly struct Role
        {
            public readonly string Name,Pose;
            public readonly Vector3 Position;
            public readonly int Style,Step;
            public Role(string name,string pose,float x,float z,int style,int step){Name=Prefix+name;Pose=pose;Position=new Vector3(x,DeckTop,z);Style=style;Step=step;}
        }
        const float DeckTop=1.4f;
        // The MC keeps .7 m clear of the DJ's mixer riser and of the cable ramp that runs east from it at z 30.3-30.6.
        static readonly Role[] Roles={
            new Role("hype_west","Dance",-3,28.3f,0,0),new Role("hype_east","Dance",3,28.3f,2,1),
            new Role("gogo_west","Dance",-7,31,1,2),new Role("gogo_east","Dance",7,31,1,3),
            new Role("mc",FestivalCharacter.McPose,2.5f,31.4f,0,1),new Role("photographer","Idle",-5,33,0,0)};
        const int Photographer=5;
        // The crew face the middle of the lawn's stage pods (FestivalAmbientCrowd's Stage spots).
        static readonly Vector3 Audience=new Vector3(0,0,21);
        // Culled as the ambient crowd's stage pods are: woken within WakeDistance of the view, kept KeepExtra further once shown,
        // and never culled within Near or while not behind the view.
        const float WakeDistance=50,KeepExtra=3,Near=8,CheckSeconds=.25f;
        // FestivalCharacter's dance tempos make about Pace steps a second. With no song to follow the crew keep FreeBpm. Each time the
        // song's clock moves on by t seconds they close 1-exp(-LockRate·t) of their gap to its beat: the audio clock moves in
        // buffer-sized jumps, whatever the frame rate, and a jump in the song (a late join) closes the gap at once.
        const float Pace=2.6f,FreeBpm=128,LockRate=8;
        // Every crew rhythm repeats within StepCycle steps, so a step count wrapped to it moves on without a jump.
        const int StepCycle=64;
        readonly List<FestivalCharacter> members=new List<FestivalCharacter>(Roles.Length);
        readonly float[] clocks=new float[Roles.Length];
        Quaternion shootCrowd,shootDj;
        FestivalStageAudio stage;
        double stageWorld;
        int stepsPerBeat=1;
        float nextStageSearch,nextVisibilityCheck;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string loggedClip="";
#endif
        /// <summary>The crew's beat count, on the stage song's beat while it plays.</summary>
        public double Beats{get;private set;}
        /// <summary>The stage song the crew follow: the first a session plays, null until one is found.</summary>
        public FestivalStageAudio Song=>stage;
        public int Count=>members.Count;
        public FestivalCharacter Member(int index)=>members[index];

        public void Build(Transform dj)
        {
            if(members.Count!=0)return;
            foreach(var role in Roles)
            {
                var actor=FestivalCharacter.Create(transform,role.Name,Color.white);
                actor.Pose=role.Pose;actor.DanceStyle=role.Style;
                actor.transform.localPosition=role.Position;actor.transform.localRotation=Facing(role.Position,Audience);
                actor.transform.localScale=Vector3.Scale(Vector3.one*.88f,actor.ShapeScale);
                members.Add(actor);
            }
            var photographer=members[Photographer];photographer.SetFilming(true,false);
            shootCrowd=photographer.transform.localRotation;shootDj=Facing(Roles[Photographer].Position,dj.localPosition);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("StageCrew","built","crew="+members.Count);
#endif
        }
        static Quaternion Facing(Vector3 from,Vector3 to){var toward=to-from;toward.y=0;return Quaternion.LookRotation(toward);}

        void Update()
        {
            float dt=FestivalCharacter.WorldFrameDelta;
            FollowStage(dt);
            var view=FestivalCharacter.ViewTransform;bool cull=view!=null&&Time.unscaledTime>=nextVisibilityCheck;
            if(cull)nextVisibilityCheck=Time.unscaledTime+CheckSeconds;
            for(int i=0;i<members.Count;i++)
            {
                var actor=members[i];if(actor==null)continue;
                if(cull)
                {
                    bool shown=Visible(actor.gameObject.activeSelf,actor.transform.position-view.position,view.forward);
                    if(shown!=actor.gameObject.activeSelf)actor.gameObject.SetActive(shown);
                }
                if(!actor.gameObject.activeSelf)continue;
                double step=Beats*stepsPerBeat+Roles[i].Step;
                actor.DanceBeat=(float)(step-StepCycle*System.Math.Floor(step/StepCycle));
                clocks[i]+=dt;actor.SetAnimationClock(clocks[i],dt);
                if(i==Photographer)actor.transform.localRotation=Quaternion.Slerp(shootCrowd,shootDj,Turn(Beats+Roles[i].Step));
            }
        }
        // The song's phase leads only while its world clock moves on: a held song (Results) or none leaves the crew their own pace.
        void FollowStage(float dt)
        {
            if(stage==null&&Time.unscaledTime>=nextStageSearch){nextStageSearch=Time.unscaledTime+1;stage=FindFirstObjectByType<FestivalStageAudio>();}
            var track=stage!=null?stage.Track:null;float? phase=null;float gain=0;double bpm=FreeBpm;
            if(track!=null)
            {
                double world=stage.WorldSeconds;
                if(dt>0&&world>stageWorld){phase=stage.Beat;gain=1-Mathf.Exp(-LockRate*(float)(world-stageWorld));}
                stageWorld=world;bpm=track.Beats.Bpm;
            }
            stepsPerBeat=StepsPerBeat(bpm);
            Beats=Follow(Beats,dt*bpm/60,phase,gain);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string clip=track!=null?track.Clip:"none";
            if(clip!=loggedClip){loggedClip=clip;if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("StageCrew","song","clip="+clip+" bpm="+bpm.ToString("F1")+" steps_per_beat="+stepsPerBeat);}
#endif
        }

        /// <summary>The crew's beat count advance beats on, pulled by gain (0-1) onto the song's beat phase the nearer way round while
        /// it plays (songPhase null when it holds or there is none).</summary>
        public static double Follow(double beats,double advance,float? songPhase,float gain)
        {
            beats+=advance;
            if(songPhase is float phase)beats+=gain*(Mathf.Repeat(phase-(float)(beats-System.Math.Floor(beats))+.5f,1)-.5f);
            return beats;
        }
        /// <summary>One step a beat, or two on a slow one-drop, whichever comes nearer a dancer's own pace.</summary>
        public static int StepsPerBeat(double bpm)=>System.Math.Abs(2*bpm/60-Pace)<System.Math.Abs(bpm/60-Pace)?2:1;
        /// <summary>How far the photographer has turned from the crowd to the DJ (0-1) at a beat count: a bar on each, a beat to turn.</summary>
        public static float Turn(double beats){float t=(float)(beats-8*System.Math.Floor(beats/8));return Mathf.SmoothStep(0,1,t-3)-Mathf.SmoothStep(0,1,t-7);}
        /// <summary>Whether a performer offset from the view, shown or not now, should be.</summary>
        public static bool Visible(bool shown,Vector3 offset,Vector3 forward)
        {
            float range=shown?WakeDistance+KeepExtra:WakeDistance,distance=offset.sqrMagnitude;
            return distance<range*range&&(distance<=Near*Near||Vector3.Dot(forward,offset.normalized)>(shown?-.25f:-.05f));
        }
    }
}
