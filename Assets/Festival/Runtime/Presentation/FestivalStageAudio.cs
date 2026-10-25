using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>The main stage's recorded song follows shared world time, holding during a booth game or Results. Dances and the
    /// DJ set ride the song at full volume. After each phrase of a set the crowd in front of the stage roars as loud as the
    /// phrase was good.</summary>
    public sealed class FestivalStageAudio : MonoBehaviour
    {
        static readonly Vector3 Front=new Vector3(0,3,27.5f);
        static readonly Vector3 Crowd=new Vector3(0,1.6f,18);
        const float Full=12,RoarFull=8,RoarGain=.5f,RoarRiseSeconds=.45f,RoarFallSeconds=1.5f;
        GameObject speakers,crowd;
        AudioSource[] decks;
        AudioSource roar;
        AudioClip roarClip;
        FestivalSession session;
        FestivalMusic.Transport transport;
        float roarLevel;

        public static FestivalMusic.Track For(RoundState state)=>state!=null&&(state.Phase=="Playing"||state.Phase==FestivalSimulation.MinigamePhase||state.Phase=="Results")?FestivalMusic.For(StageMusic.Track(state,StageMusic.Main)):null;
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
        public static GameObject Speakers()
        {
            var speakers=new GameObject("Main stage speakers");speakers.transform.position=Front;
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
        /// <summary>Stable recorded beat phase for stage performers, independent of which scheduled deck is live.</summary>
        public float? Beat=>Track==null?(float?)null:Track.BeatPhase((int)RhythmChart.StageSampleAtWorld(Track.Beats,WorldSeconds));
        void Awake()
        {
            session=GetComponent<FestivalSession>();speakers=Speakers();decks=speakers.GetComponents<AudioSource>();
            transport=new FestivalMusic.Transport(decks);
            crowd=new GameObject("Stage crowd roar");crowd.transform.position=Crowd;roar=crowd.AddComponent<AudioSource>();
            roar.playOnAwake=false;roar.loop=true;roar.spatialBlend=1;roar.rolloffMode=AudioRolloffMode.Logarithmic;roar.minDistance=RoarFull;roar.dopplerLevel=0;
            roar.clip=roarClip=CreateRoar();
        }
        void Update(){if(session!=null)Follow(session.State,session.EstimatedSimulationSeconds,AudioSettings.dspTime);}
        /// <summary>One public clock boundary used by the session and isolated stage views. The estimate advances only Playing;
        /// minigame/results snapshots retain the world's exact pause position.</summary>
        public void Follow(RoundState state,double estimatedSimulationSeconds,double dspSeconds)
        {
            var next=For(state);
            double world=next==null?0:FestivalSimulation.WorldSeconds(state);
            if(next!=null&&state.Phase=="Playing")world+=Math.Max(0,estimatedSimulationSeconds-state.SimulationSeconds);
            transport.Follow(next,state?.RoundId??"",Math.Max(0,world),state?.Phase!="Playing",dspSeconds);
            float volume=FestivalSettings.MusicGain(session?.Profile?.Data,.6f);
            foreach(var deck in decks)deck.volume=volume;
            roarLevel=EaseRoar(roarLevel,RoarLevel(state),Time.unscaledDeltaTime);
            FestivalSettings.ApplyEffects(roar,session?.Profile?.Data,RoarGain*roarLevel);
            if(roarLevel>0&&!roar.isPlaying)roar.Play();else if(roarLevel<=0&&roar.isPlaying)roar.Stop();
        }
        void OnDisable(){transport?.Stop();if(roar!=null)roar.Stop();}
        void OnDestroy(){if(speakers!=null)Destroy(speakers);if(crowd!=null)Destroy(crowd);if(roarClip!=null)Destroy(roarClip);}
    }
}
