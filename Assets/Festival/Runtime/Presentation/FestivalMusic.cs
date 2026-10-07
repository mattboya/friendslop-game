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
