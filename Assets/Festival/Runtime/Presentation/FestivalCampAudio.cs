using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>The camp's sound: the DJ's track, chosen in the host snapshot, and the original synthesized gag sounds. MUSIC-2: the
    /// tracks are Matt's recordings (FestivalMusic). Each loops between beats measured from the recording, not over the file's silent
    /// start and faded end. Two decks take turns on the audio clock so the join lands to the sample: the first plays the track from
    /// its start to the loop's end, then the other plays the loop from its first beat, and so on.</summary>
    public sealed class FestivalCampAudio : MonoBehaviour
    {
        const int Rate=24000;
        // How far ahead a track is scheduled, so its stream is open by the time it starts.
        const double Lead=.2;
        readonly AudioClip[] antics=new AudioClip[3];
        readonly AudioSource[] decks=new AudioSource[2];
        AudioSource foley;
        FestivalSession session;
        FestivalMusic.Track track;
        int playing=-1,live;
        // When the deck not live takes over, on the audio clock.
        double turnAt;
        int lastAntics;
        float? beat;
        int beatFrame=-1;

        /// <summary>The deck sounding now (FestivalMusic.BeatPhase reads its beat), or null in silence.</summary>
        public AudioSource Music=>track==null?null:decks[AudioSettings.dspTime<turnAt?live:1-live];
        /// <summary>GAS-5: the camp music's beat phase this frame (FestivalMusic.BeatPhase of Music), or null in silence. It is read
        /// once a frame, so the HUD's zoom and FestivalTrip's overlay and sound pulse on the same moment.</summary>
        public float? Beat{get{if(beatFrame!=Time.frameCount){beatFrame=Time.frameCount;beat=FestivalMusic.BeatPhase(Music);}return beat;}}

        void Awake()
        {
            session=GetComponent<FestivalSession>();
            for(int deck=0;deck<decks.Length;deck++){decks[deck]=gameObject.AddComponent<AudioSource>();decks[deck].playOnAwake=false;decks[deck].spatialBlend=0;}
            foley=gameObject.AddComponent<AudioSource>();foley.playOnAwake=false;foley.spatialBlend=0;
            for(int kind=0;kind<antics.Length;kind++)antics[kind]=CreateAntic(kind);
        }
        static AudioClip CreateAntic(int kind)
        {
            var samples=new float[Rate];
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)Rate;
                double envelope=Math.Exp(-t*(kind==2?4:7));
                double sound=kind==0
                    ?Math.Sin(2*Math.PI*(220+16*Math.Sin(t*13))*t)*.23
                    :kind==1?Math.Sin(2*Math.PI*(490-250*t)*t)*.19
                    :(Math.Sin(2*Math.PI*(130+470*t)*t)+Math.Sin(2*Math.PI*890*t)*.2)*.12;
                samples[n]=(float)(sound*envelope);
            }
            var clip=AudioClip.Create("Original camp antic "+kind,samples.Length,1,Rate,false);
            clip.SetData(samples,0);return clip;
        }
        // Plays the DJ's choice (CampFeatures.Tracks' index; SILENCE stops), its first pass starting at sample from.
        void Play(int selected,int from)
        {
            playing=selected;track=FestivalMusic.For(selected);
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
            var state=session.State;
            int selected=state!=null&&(state.Phase=="Shopping"||state.Phase=="Spinning"||state.Phase=="CampReview")?state.CampMusicTrack:0;
            if(selected!=playing)
            {
                Play(selected,0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","camp_music","track="+selected);
#endif
            }
            // The other deck has taken over: it is live now, and the one that just finished waits for the next turn.
            if(track!=null&&AudioSettings.dspTime>=turnAt){live=1-live;turnAt+=track.LoopSeconds;Queue(decks[1-live]);}
            float volume=session.Profile==null?0:session.Profile.Data.MusicVolume*.6f;
            foreach(var deck in decks)deck.volume=volume;
            var player=session.LocalPlayer;
            if(player==null){lastAntics=0;return;}
            if(player.CampAntics>lastAntics&&player.CampVisitId!="")
            {
                var site=CampFeatures.Find(player.CampVisitId);
                int kind=site?.Kind=="Car"?0:site?.Kind=="Tent"?1:2;
                foley.PlayOneShot(antics[kind],Mathf.Clamp01(session.Profile.Data.MusicVolume+.25f));
            }
            lastAntics=player.CampAntics;
        }
        void OnDestroy(){foreach(var antic in antics)if(antic!=null)Destroy(antic);}
    }
}
