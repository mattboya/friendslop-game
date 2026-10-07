using Festival.Core;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>MUSIC-2: Matt's seven recorded tracks, in the camp DJ's order (CampFeatures.Tracks after SILENCE), with each one's beat
    /// and loop measured from the recording itself (FestivalMusicTests.Measure). The tracks are not one steady tempo (tech house
    /// speeds up from about 128 to 130.5 BPM as it plays), so a track's beat is a grid that starts again every 8 bars.</summary>
    public static class FestivalMusic
    {
        /// <summary>The recordings' sample rate. Every position below counts samples at this rate.</summary>
        public const int Rate=StageBeats.Rate;
        /// <summary>Beats in a phrase: 8 bars of 4.</summary>
        public const int PhraseBeats=StageBeats.PhraseBeats;

        public sealed class Track
        {
            /// <summary>The clip's name in Resources/Music.</summary>
            public readonly string Clip;
            /// <summary>Where each 8-bar phrase starts. The beats between two phrase starts are evenly spaced.</summary>
            public readonly int[] Phrases;
            /// <summary>The loop: the authored body start beat, and the same beat in the last phrase before the fade. Each end sits up to 5 ms
            /// off the beat, where the samples either side of the join match, so the join doesn't click.</summary>
            public readonly int FirstBeat,LoopEnd;
            /// <summary>The pure authored grid shared with the host's rules.</summary>
            public readonly StageBeats.Track Beats;
            AudioClip audio;
            public Track(string clip,int firstBeat,int loopEnd,params int[] phrases):this(new StageBeats.Track(clip,firstBeat,loopEnd,phrases)){}
            internal Track(StageBeats.Track beats){Beats=beats;Clip=beats.Clip;FirstBeat=beats.FirstBeat;LoopEnd=beats.LoopEnd;Phrases=beats.Phrases;}
            public string Resource=>"Music/"+Clip;
            /// <summary>The recording, loaded on first use. It streams, so it holds only a small buffer.</summary>
            public AudioClip Audio=>audio!=null?audio:audio=Resources.Load<AudioClip>(Resource);
            // Whether a clip is this track's recording, without loading it or reading its name (which allocates).
            internal bool Is(AudioClip clip)=>audio!=null&&audio==clip;
            public double LoopSeconds=>Beats.LoopSeconds;
            /// <summary>The loop's average tempo.</summary>
            public double Bpm=>Beats.Bpm;
            /// <summary>Where this sample falls in the beat: 0 on a beat, rising to just under 1 before the next.</summary>
            public float BeatPhase(int sample)=>Beats.BeatPhase(sample);
        }

        // Keep the camp API and recording cache; all authored samples live in the renderer-free table.
        public static readonly Track[] Tracks=System.Array.ConvertAll(StageBeats.Tracks,beats=>new Track(beats));

        /// <summary>The track a camp DJ choice plays (CampFeatures.Tracks' index), or null for SILENCE.</summary>
        public static Track For(int campTrack)
        {
            var selected=StageBeats.For(campTrack);
            foreach(var track in Tracks)if(track.Beats==selected)return track;
            return null;
        }

        /// <summary>The next whole recorded beat at or after this looped world time. Keeps click-free joins separate from
        /// phrase anchors: neither the body start nor the exact loop span is necessarily a whole number of beats.</summary>
        public static double NextStageBeat(Track track,double afterWorldSeconds)
        {
            double sample=RhythmChart.StageSampleAtWorld(track.Beats,afterWorldSeconds);
            double next=track.Beats.Sample(System.Math.Ceiling(track.Beats.Beat(sample)-1e-10));
            if(next>=track.LoopEnd)
                return afterWorldSeconds+(track.LoopEnd-sample+track.Beats.Sample(System.Math.Ceiling(track.Beats.Beat(track.FirstBeat)-1e-10))-track.FirstBeat)/Rate;
            return afterWorldSeconds+(next-sample)/Rate;
        }

        /// <summary>Two recorded decks follow an advancing caller-owned clock. The main stage uses shared world time; a booth
        /// can use its own advancing time without advancing the held world. Owns scheduling and seeks, never gain or beat tables.</summary>
        public sealed class Transport
        {
            const double Lead=.2,CheckSeconds=.5,MaterialDriftSeconds=.06;
            readonly AudioSource[] decks;
            Track track;
            string epoch="";
            bool held,handoff;
            int live;
            double startAt,turnAt,worldAtStart,nextCheck;
            public Transport(AudioSource[] decks)
            {
                if(decks==null||decks.Length!=2||decks[0]==null||decks[1]==null)throw new System.ArgumentException("A recorded transport needs two decks.",nameof(decks));
                this.decks=decks;
            }
            public Track Track=>track;
            public bool Held=>held;
            /// <summary>Continuous clock phase, including before a scheduled start and while held.</summary>
            public double WorldAt(double dspSeconds)=>track==null?0:held?worldAtStart:System.Math.Max(0,worldAtStart+dspSeconds-startAt);
            public AudioSource Current(double dspSeconds)=>track==null?null:held?decks[0]:handoff&&dspSeconds<startAt?decks[1-live]:decks[dspSeconds<turnAt?live:1-live];
            static int Sample(Track track,double world)=>(int)System.Math.Min(track.LoopEnd-1,System.Math.Floor(RhythmChart.StageSampleAtWorld(track.Beats,world)));
            public void Stop()
            {
                foreach(var deck in decks){deck.Stop();deck.clip=null;}
                track=null;epoch="";handoff=false;
            }
            /// <summary>Call once per frame. Epoch/hold changes cue immediately; steady playback checks material drift twice
            /// per second. The stream opens before the projected clock position is heard, rather than adding latency to it.</summary>
            public void Follow(Track next,string nextEpoch,double worldSeconds,bool hold,double dspSeconds)
            {
                if(next==null){if(track!=null)Stop();return;}
                if(double.IsNaN(worldSeconds)||double.IsInfinity(worldSeconds)||worldSeconds<0)throw new System.ArgumentOutOfRangeException(nameof(worldSeconds));
                bool changed=track!=next||epoch!=nextEpoch;
                if(changed)
                {
                    track=next;epoch=nextEpoch;foreach(var deck in decks){deck.Stop();deck.clip=track.Audio;deck.loop=false;deck.pitch=1;}
                    if(hold)Hold(worldSeconds,dspSeconds);else Schedule(worldSeconds,dspSeconds,false);
                    return;
                }
                if(hold!=held){if(hold)Hold(worldSeconds,dspSeconds);else Schedule(worldSeconds,dspSeconds,false);return;}
                if(held)return;
                if(handoff&&dspSeconds>=startAt){handoff=false;Queue(decks[1-live]);}
                // A suspended app can miss both queued decks. Recover once, rather than enqueueing expired audio in a loop.
                if(dspSeconds>=turnAt+track.LoopSeconds){Schedule(worldSeconds,dspSeconds,false);return;}
                if(dspSeconds>=turnAt){live=1-live;turnAt+=track.LoopSeconds;Queue(decks[1-live]);}
                if(dspSeconds<nextCheck||dspSeconds<startAt||handoff)return;
                nextCheck=dspSeconds+CheckSeconds;
                var source=Current(dspSeconds);double expected=RhythmChart.StageSampleAtWorld(track.Beats,worldSeconds);
                double miss=System.Math.Abs(source.timeSamples-expected),span=track.LoopEnd-track.FirstBeat;
                miss=System.Math.Min(miss,System.Math.Abs(span-miss))/Rate;
                bool drift=System.Math.Abs(WorldAt(dspSeconds)-worldSeconds)>MaterialDriftSeconds||!source.isPlaying||miss>MaterialDriftSeconds;
                // The spare deck is already due to join imminently; let that exact join happen before borrowing it for correction.
                if(drift&&turnAt-dspSeconds>Lead+.02)Schedule(worldSeconds,dspSeconds,true);
            }
            void Hold(double world,double dsp)
            {
                foreach(var deck in decks)deck.Stop();held=true;handoff=false;live=0;worldAtStart=world;startAt=dsp;
                decks[0].timeSamples=Sample(track,world);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Trace("hold",decks[0].timeSamples,dsp);
#endif
            }
            void Schedule(double world,double dsp,bool keepLive)
            {
                held=false;worldAtStart=world+Lead;startAt=dsp+Lead;nextCheck=startAt+CheckSeconds;
                int from=Sample(track,worldAtStart);
                if(keepLive)
                {
                    decks[live].SetScheduledEndTime(startAt);live=1-live;decks[live].Stop();handoff=true;
                }
                else{foreach(var deck in decks)deck.Stop();live=0;handoff=false;}
                turnAt=startAt+(track.LoopEnd-from)/(double)Rate;
                decks[live].timeSamples=from;decks[live].PlayScheduled(startAt);decks[live].SetScheduledEndTime(turnAt);
                if(!handoff)Queue(decks[1-live]);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Trace(keepLive?"drift":"cue",from,startAt);
#endif
            }
            void Queue(AudioSource deck){deck.Stop();deck.timeSamples=track.FirstBeat;deck.PlayScheduled(turnAt);deck.SetScheduledEndTime(turnAt+track.LoopSeconds);}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            void Trace(string reason,int sample,double dsp)
            {
                if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("StageTransport",reason,"clip="+track.Clip+" world="+worldAtStart.ToString("F3")+" sample="+sample+" dsp="+dsp.ToString("F3"));
            }
#endif
        }

        /// <summary>Where a playing source is in its track's beat (Track.BeatPhase), or null when it isn't playing one of these:
        /// the beat other effects follow (GAS-5's Giggle Balloon, MUSIC-1's stage).</summary>
        public static float? BeatPhase(AudioSource source)
        {
            if(source==null||source.clip==null||!source.isPlaying)return null;
            foreach(var track in Tracks)if(track.Is(source.clip))return track.BeatPhase(source.timeSamples);
            return null;
        }
    }
}
