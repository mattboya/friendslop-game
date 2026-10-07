using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>The main stage's recorded song follows shared world time, holding during a booth game or Results. Dances ride
    /// the song at full volume. A DJ takeover silences it while its transport continues, ready for the handoff.</summary>
    public sealed class FestivalStageAudio : MonoBehaviour
    {
        static readonly Vector3 Front=new Vector3(0,3,27.5f);
        const float Full=12,FadeOutSeconds=.3f,RecoverSeconds=1;
        GameObject speakers;
        AudioSource[] decks;
        FestivalSession session;
        FestivalMusic.Transport transport;
        float level=1;

        /// <summary>Palm Mirage's disco/tech house day/night and Ember Playa's dub/drum and bass stay on the original catalog.</summary>
        public static FestivalMusic.Track TrackFor(int festival,int level)=>FestivalMusic.For(RhythmChart.StageTrackFor(festival,level));
        public static FestivalMusic.Track For(RoundState state)=>state!=null&&(state.Phase=="Playing"||state.Phase==FestivalSimulation.MinigamePhase||state.Phase=="Results")?TrackFor(state.FestivalIndex,state.LevelIndex):null;
        /// <summary>Only a stage-front DJ takeover changes the song's gain. Dances and talks keep the song full.</summary>
        public static float Level(RoundState state,string localId)
        {
            if(state!=null)foreach(var p in state.Players)if(p.VisualPose=="Dj")return 0;
            return 1;
        }
        public static float Ease(float level,float target,float dt)=>Mathf.MoveTowards(level,target,dt/(target<level?FadeOutSeconds:RecoverSeconds));
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
            level=Ease(level,Level(state,session?.LocalPlayerId),Time.unscaledDeltaTime);
            float volume=FestivalSettings.MusicGain(session?.Profile?.Data,.6f*level);
            foreach(var deck in decks)deck.volume=volume;
        }
        void OnDisable(){transport?.Stop();}
        void OnDestroy(){if(speakers!=null)Destroy(speakers);}
    }
}
