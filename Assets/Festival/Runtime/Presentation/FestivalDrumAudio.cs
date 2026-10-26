using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>MINI-2: the Drum Circle's own song. While the game runs the stage holds its world sample (FestivalStageAudio) and the
    /// world stands still; the circle plays the song heard at its booth on from that held sample by the game's own clock, the clock its
    /// parts' authored grid was laid on, so the drummers play to the music. Heard in the circle rather than from the stage. It
    /// stops as the game is scored, and as the session ends.</summary>
    public sealed class FestivalDrumAudio : MonoBehaviour
    {
        GameObject speakers;
        AudioSource[] decks;
        FestivalSession session;
        FestivalMusic.Transport transport;

        /// <summary>Where the circle's song is on the stage track's looped world clock: the held world position plus the game's own
        /// seconds; null unless an unscored Drum Circle runs.</summary>
        public static double? SongSeconds(RoundState state,double minigameSeconds)=>state!=null&&state.Phase==FestivalSimulation.MinigamePhase
            &&state.Booth.Kind==FestivalSimulation.DrumCircleGame&&state.Booth.ScoredAt<0?FestivalSimulation.WorldSeconds(state)+Math.Max(0,minigameSeconds):(double?)null;
        /// <summary>The deck sounding now; null while the circle's song is off.</summary>
        public AudioSource Music=>transport?.Current(AudioSettings.dspTime);
        public double WorldSeconds=>transport?.WorldAt(AudioSettings.dspTime)??0;
        void Awake()
        {
            session=GetComponent<FestivalSession>();speakers=new GameObject("Drum circle speakers");
            decks=new[]{speakers.AddComponent<AudioSource>(),speakers.AddComponent<AudioSource>()};
            foreach(var deck in decks){deck.playOnAwake=false;deck.spatialBlend=0;}
            transport=new FestivalMusic.Transport(decks);
        }
        void Update(){if(session!=null)Follow(session.State,session.EstimatedMinigameSeconds,AudioSettings.dspTime);}
        public void Follow(RoundState state,double minigameSeconds,double dspSeconds)
        {
            var song=SongSeconds(state,minigameSeconds);
            transport.Follow(song==null?null:FestivalMusic.For(FestivalSimulation.DrumCircleTrack(state)),state?.RoundId??"",song??0,false,dspSeconds);
            float volume=FestivalSettings.MusicGain(session?.Profile?.Data,.6f);
            foreach(var deck in decks)deck.volume=volume;
        }
        void OnDisable(){transport?.Stop();}
        void OnDestroy(){if(speakers!=null)Destroy(speakers);}
    }
}
