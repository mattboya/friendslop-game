using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>MUSIC-1: the main stage's sound. While the festival is on screen, the stage front plays the level's track from MUSIC-2's
    /// recordings (FestivalMusic), in 3-D: full within 12 m of the stage front and still there, about a fifth as loud, at the camp
    /// gate. Each festival has its own day and night track. Under decision A (separate beats) the stage ducks to a quarter while you
    /// dance, so the dance's own beat (FestivalRhythmAudio) leads, and a DJ takeover mutes it, so the player's set is the stage's
    /// sound. It goes through the view's AudioListener, so the trip's muffle and tremolo colour it like everything else.</summary>
    public sealed class FestivalStageAudio : MonoBehaviour
    {
        // The stage front, centre (FestivalWorld's stage: 18 m wide at x 0, z 27.5-36.5), at speaker height.
        static readonly Vector3 Front=new Vector3(0,3,27.5f);
        const float Full=12,Ducked=.25f,DuckSeconds=.3f,RecoverSeconds=1;
        // How far ahead a track is scheduled, so its stream is open by the time it starts.
        const double Lead=.2;
        GameObject speakers;
        AudioSource[] decks;
        FestivalSession session;
        FestivalMusic.Track track;
        float level=1;
        int live;
        // When the deck not live takes over, on the audio clock.
        double turnAt;

        /// <summary>The track a festival's stage plays on a level (MUSIC-2's decision A): Palm Mirage, disco house by day and tech house
        /// by night; Ember Playa, dub reggae by day and drum and bass by night. From the shared level, so every client plays the same.</summary>
        public static FestivalMusic.Track TrackFor(int festival,int level)=>FestivalMusic.Tracks[Sets[festival][Festivals.Night(level)?1:0]];
        // Each festival's day and night track, as FestivalMusic.Tracks indexes: tech house 0, disco house 1, drum and bass 3, dub reggae 4.
        static readonly int[][] Sets={new[]{1,0},new[]{4,3}};
        /// <summary>What the stage plays for this view: its level's track while the festival is on screen (Playing, a booth's minigame
        /// pausing it, and Results), or null at camp, where the camp DJ has the music.</summary>
        public static FestivalMusic.Track For(RoundState state)=>state!=null&&(state.Phase=="Playing"||state.Phase==FestivalSimulation.MinigamePhase||state.Phase=="Results")?TrackFor(state.FestivalIndex,state.LevelIndex):null;
        /// <summary>How loud the stage should be for the local player, of full: muted (0) while anyone has the stage-front deck (a DJ
        /// takeover; ViewFor shows every player's pose), a quarter while you play a dance on the lanes (FestivalInput.IsRhythm: a
        /// talk plays on, its trust dance ducks), and full otherwise. Allocates nothing.</summary>
        public static float Level(RoundState state,string localId)
        {
            string doing="";
            foreach(var p in state.Players){if(p.VisualPose=="Dj")return 0;if(p.Id==localId)doing=p.InteractionId;}
            if(doing!="")foreach(var i in state.Interactions)if(i.Id==doing&&i.Status=="Active"&&FestivalInput.IsRhythm(i))return Ducked;
            return 1;
        }
        /// <summary>The stage's level one frame on: down to a quarter in 0.3 s, back up from it in 1 s.</summary>
        public static float Ease(float level,float target,float dt)=>Mathf.MoveTowards(level,target,dt*(1-Ducked)/(target<level?DuckSeconds:RecoverSeconds));
        /// <summary>The stage's speakers at the stage front: two 3-D decks that take turns, so the loop joins on the audio clock.</summary>
        public static GameObject Speakers()
        {
            var speakers=new GameObject("Main stage speakers");speakers.transform.position=Front;
            for(int deck=0;deck<2;deck++)
            {
                var source=speakers.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;
                // Logarithmic: minDistance/distance past Full, about a fifth at the camp gate. No Doppler: riding an art car or swinging
                // the third-person camera must not bend the music's pitch.
                source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=Full;source.dopplerLevel=0;
            }
            return speakers;
        }

        /// <summary>The deck sounding now (FestivalMusic.BeatPhase reads its beat), or null while the stage is silent.</summary>
        public AudioSource Music=>track==null?null:decks[AudioSettings.dspTime<turnAt?live:1-live];

        void Awake()
        {
            session=GetComponent<FestivalSession>();
            speakers=Speakers();decks=speakers.GetComponents<AudioSource>();
        }
        // ponytail: the same two-deck loop as FestivalCampAudio's Play/Queue. Lift both onto one helper when a third music player needs it;
        // MENU-1's title music plays on the camp's own decks, so it added none.
        // Plays a track (null stops), its first pass starting at sample from.
        void Play(FestivalMusic.Track next,int from)
        {
            track=next;
            foreach(var deck in decks){deck.Stop();deck.clip=track?.Audio;}
            if(track==null)return;
            double start=AudioSettings.dspTime+Lead;
            live=0;turnAt=start+(track.LoopEnd-from)/(double)FestivalMusic.Rate;
            decks[0].timeSamples=from;decks[0].PlayScheduled(start);decks[0].SetScheduledEndTime(turnAt);
            Queue(decks[1]);
        }
        // A deck waits to take over at turnAt and plays the loop once.
        void Queue(AudioSource deck){deck.Stop();deck.timeSamples=track.FirstBeat;deck.PlayScheduled(turnAt);deck.SetScheduledEndTime(turnAt+track.LoopSeconds);}
        void Update()
        {
            var state=session.State;var next=For(state);
            if(next!=track)Play(next,0);
            if(track==null)return;
            // The other deck has taken over: it is live now, and the one that just finished waits for the next turn.
            if(AudioSettings.dspTime>=turnAt){live=1-live;turnAt+=track.LoopSeconds;Queue(decks[1-live]);}
            level=Ease(level,Level(state,session.LocalPlayerId),Time.unscaledDeltaTime);
            // At full, as loud up front as the camp DJ (FestivalCampAudio).
            float volume=FestivalSettings.MusicGain(session.Profile?.Data,.6f*level);
            foreach(var deck in decks)deck.volume=volume;
        }
        void OnDestroy(){if(speakers!=null)Destroy(speakers);}
    }
}
