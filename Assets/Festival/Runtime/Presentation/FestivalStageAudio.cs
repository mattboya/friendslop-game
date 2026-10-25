using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Each stage's recorded song follows shared world time, holding during a booth game or Results, and the two
    /// cross-fade by where the local player stands. Dances and the DJ set never duck it. After each phrase of a set the crowd
    /// in front of the main stage roars as loud as the phrase was good.</summary>
    public sealed class FestivalStageAudio : MonoBehaviour
    {
        static readonly Vector3 Front=new Vector3(StageMusic.MainX,3,StageMusic.MainZ),SmallFront=new Vector3(StageMusic.SmallX,2.2f,StageMusic.SmallZ);
        static readonly Vector3 Crowd=new Vector3(0,1.6f,18);
        const float Full=12,RoarFull=8,RoarGain=.5f,RoarRiseSeconds=.45f,RoarFallSeconds=1.5f;
        GameObject speakers,smallSpeakers,crowd;
        AudioSource[] decks,smallDecks;
        AudioSource roar;
        AudioClip roarClip;
        FestivalSession session;
        FestivalMusic.Transport transport,smallTransport;
        float roarLevel;

        public static FestivalMusic.Track For(RoundState state)=>For(state,StageMusic.Main);
        public static FestivalMusic.Track For(RoundState state,int stage)=>state!=null&&(state.Phase=="Playing"||state.Phase==FestivalSimulation.MinigamePhase||state.Phase=="Results")?FestivalMusic.For(StageMusic.Track(state,stage)):null;
        /// <summary>A stage's share of the music where the listener stands: an equal-power cross-fade on the distances to the two
        /// fronts, 1 at a stage's own front and 0 at the other's.</summary>
        public static float Mix(float x,float z,int stage)
        {
            float main=Mathf.Sqrt((x-StageMusic.MainX)*(x-StageMusic.MainX)+(z-StageMusic.MainZ)*(z-StageMusic.MainZ));
            float small=Mathf.Sqrt((x-StageMusic.SmallX)*(x-StageMusic.SmallX)+(z-StageMusic.SmallZ)*(z-StageMusic.SmallZ));
            float towardSmall=main/Mathf.Max(main+small,1e-4f);
            return stage==StageMusic.Small?Mathf.Sin(towardSmall*Mathf.PI/2):Mathf.Cos(towardSmall*Mathf.PI/2);
        }
        /// <summary>0-1 while the host's cheer for the DJ's last phrase lasts: nothing for a flop, a murmur at 0.4, the full roar
        /// for a perfect phrase. A booth game freezes the cheer's clock, so the roar waits with the held song.</summary>
        public static float RoarLevel(RoundState state)=>state==null||state.Phase!="Playing"||state.StageCheerScore<0||FestivalSimulation.WorldSeconds(state)>=state.StageCheerUntilWorldSeconds?0:Mathf.Clamp01(((float)state.StageCheerScore-.25f)/.75f);
        public static float EaseRoar(float level,float target,float dt)=>Mathf.MoveTowards(level,target,dt/(target>level?RoarRiseSeconds:RoarFallSeconds));
        /// <summary>Three seconds of filtered noise with slow, uneven swells, cross-faded into its own start so it loops without
        /// a click.</summary>
        public static AudioClip CreateRoar()
        {
            const int Rate=24000,Length=Rate*3,Fade=Rate/5;
            var raw=new float[Length+Fade];uint seed=0x2545f491;double low=0,band=0;
            for(int n=0;n<raw.Length;n++)
            {
                double t=n/(double)Rate;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;
                double white=(seed&0xffff)/32767.5-1;low+=(white-low)*.12;band+=(low-band)*.35;
                double swell=.65+.2*Math.Sin(2*Math.PI*t*2/3)+.15*Math.Sin(2*Math.PI*t*5/3+1.3);
                raw[n]=(float)((low-band)*swell);
            }
            var samples=new float[Length];float peak=0;
            for(int n=0;n<Length;n++){float w=n<Fade?n/(float)Fade:1;samples[n]=raw[n]*w+(n<Fade?raw[Length+n]*(1-w):0);peak=Math.Max(peak,Math.Abs(samples[n]));}
            for(int n=0;n<Length;n++)samples[n]*=.7f/peak;
            var clip=AudioClip.Create("Stage crowd roar",Length,1,Rate,false);clip.SetData(samples,0);return clip;
        }
        public static GameObject Speakers(int stage=StageMusic.Main)
        {
            var speakers=new GameObject(stage==StageMusic.Small?"Small stage speakers":"Main stage speakers");speakers.transform.position=stage==StageMusic.Small?SmallFront:Front;
            for(int deck=0;deck<2;deck++)
            {
                var source=speakers.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;
                source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=Full;source.dopplerLevel=0;
            }
            return speakers;
        }
        /// <summary>The selected deck, also retained at its exact sample while the world is held.</summary>
        public AudioSource Music=>transport?.Current(AudioSettings.dspTime);
        public FestivalMusic.Track Track=>transport?.Track;
        public double WorldSeconds=>transport?.WorldAt(AudioSettings.dspTime)??0;
        public AudioSource SmallMusic=>smallTransport?.Current(AudioSettings.dspTime);
        public FestivalMusic.Track SmallTrack=>smallTransport?.Track;
        /// <summary>Stable recorded beat phase for stage performers, independent of which scheduled deck is live.</summary>
        public float? Beat=>Track==null?(float?)null:Track.BeatPhase((int)RhythmChart.StageSampleAtWorld(Track.Beats,WorldSeconds));
        void Awake()
        {
            session=GetComponent<FestivalSession>();speakers=Speakers();decks=speakers.GetComponents<AudioSource>();
            transport=new FestivalMusic.Transport(decks);
            smallSpeakers=Speakers(StageMusic.Small);smallDecks=smallSpeakers.GetComponents<AudioSource>();smallTransport=new FestivalMusic.Transport(smallDecks);
            crowd=new GameObject("Stage crowd roar");crowd.transform.position=Crowd;roar=crowd.AddComponent<AudioSource>();
            roar.playOnAwake=false;roar.loop=true;roar.spatialBlend=1;roar.rolloffMode=AudioRolloffMode.Logarithmic;roar.minDistance=RoarFull;roar.dopplerLevel=0;
            roar.clip=roarClip=CreateRoar();
        }
        void Update(){if(session!=null)Follow(session.State,session.EstimatedSimulationSeconds,AudioSettings.dspTime);}
        /// <summary>One public clock boundary used by the session and isolated stage views. The estimate advances only Playing;
        /// minigame/results snapshots retain the world's exact pause position. The listener is localId, by default the session's
        /// local player; with none both stages play full.</summary>
        public void Follow(RoundState state,double estimatedSimulationSeconds,double dspSeconds,string localId=null)
        {
            var next=For(state);
            double world=next==null?0:FestivalSimulation.WorldSeconds(state);
            if(next!=null&&state.Phase=="Playing")world+=Math.Max(0,estimatedSimulationSeconds-state.SimulationSeconds);
            transport.Follow(next,state?.RoundId??"",Math.Max(0,world),state?.Phase!="Playing",dspSeconds);
            smallTransport.Follow(For(state,StageMusic.Small),state?.RoundId??"",Math.Max(0,world),state?.Phase!="Playing",dspSeconds);
            localId??=session?.LocalPlayerId;var listener=state?.Players.Find(p=>p.Id==localId);
            float main=listener==null?1:Mix(listener.X,listener.Z,StageMusic.Main),small=listener==null?1:Mix(listener.X,listener.Z,StageMusic.Small);
            float volume=FestivalSettings.MusicGain(session?.Profile?.Data,.6f*main),smallVolume=FestivalSettings.MusicGain(session?.Profile?.Data,.6f*small);
            foreach(var deck in decks)deck.volume=volume;
            foreach(var deck in smallDecks)deck.volume=smallVolume;
            roarLevel=EaseRoar(roarLevel,RoarLevel(state),Time.unscaledDeltaTime);
            FestivalSettings.ApplyEffects(roar,session?.Profile?.Data,RoarGain*roarLevel);
            if(roarLevel>0&&!roar.isPlaying)roar.Play();else if(roarLevel<=0&&roar.isPlaying)roar.Stop();
        }
        void OnDisable(){transport?.Stop();smallTransport?.Stop();if(roar!=null)roar.Stop();}
        void OnDestroy(){if(speakers!=null)Destroy(speakers);if(smallSpeakers!=null)Destroy(smallSpeakers);if(crowd!=null)Destroy(crowd);if(roarClip!=null)Destroy(roarClip);}
    }
}
