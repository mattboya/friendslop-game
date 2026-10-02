using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>MUSIC-2: Matt's five recorded tracks, in the camp DJ's order (CampFeatures.Tracks after SILENCE), with each one's beat
    /// and loop measured from the recording itself (FestivalMusicTests.Measure). The tracks are not one steady tempo (tech house
    /// speeds up from about 128 to 130.5 BPM as it plays), so a track's beat is a grid that starts again every 8 bars.</summary>
    public static class FestivalMusic
    {
        /// <summary>The recordings' sample rate. Every position below counts samples at this rate.</summary>
        public const int Rate=48000;
        /// <summary>Beats in a phrase: 8 bars of 4.</summary>
        public const int PhraseBeats=32;

        public sealed class Track
        {
            /// <summary>The clip's name in Resources/Music.</summary>
            public readonly string Clip;
            /// <summary>Where each 8-bar phrase starts. The beats between two phrase starts are evenly spaced.</summary>
            public readonly int[] Phrases;
            /// <summary>The loop: the first beat, and the same beat in the last phrase before the fade. Each end sits up to 5 ms
            /// off the beat, where the samples either side of the join match, so the join doesn't click.</summary>
            public readonly int FirstBeat,LoopEnd;
            AudioClip audio;
            public Track(string clip,int firstBeat,int loopEnd,params int[] phrases){Clip=clip;FirstBeat=firstBeat;LoopEnd=loopEnd;Phrases=phrases;}
            public string Resource=>"Music/"+Clip;
            /// <summary>The recording, loaded on first use. It streams, so it holds only a small buffer.</summary>
            public AudioClip Audio=>audio!=null?audio:audio=Resources.Load<AudioClip>(Resource);
            // Whether a clip is this track's recording, without loading it or reading its name (which allocates).
            internal bool Is(AudioClip clip)=>audio!=null&&audio==clip;
            public double LoopSeconds=>(LoopEnd-FirstBeat)/(double)Rate;
            /// <summary>The loop's average tempo.</summary>
            public double Bpm=>(Beat(LoopEnd)-Beat(FirstBeat))*60/LoopSeconds;
            /// <summary>Where this sample falls in the beat: 0 on a beat, rising to just under 1 before the next.</summary>
            public float BeatPhase(int sample){double beat=Beat(sample);return (float)(beat-System.Math.Floor(beat));}
            // How many beats this sample is past the first phrase's start.
            double Beat(int sample)
            {
                int k=0;while(k+2<Phrases.Length&&Phrases[k+1]<=sample)k++;
                return PhraseBeats*(k+(sample-Phrases[k])/(double)(Phrases[k+1]-Phrases[k]));
            }
        }

        // Measured by FestivalMusicTests.PrintTheMeasuredTable from Unity's decode at the shipped quality. Each loop runs from the
        // first beat to the same beat of the last whole phrase before the fade.
        public static readonly Track[] Tracks={
            // 129.39 BPM on average, speeding up from 128.1 to 130.7; the loop is 12 phrases from beat 0.
            new Track("tech-house-loop",23399,8570337,23317,743034,1462270,2180185,2896025,3609993,4322800,5033984,5743137,6451277,7158477,7865267,8570570),
            // 129.34 BPM on average, speeding up from 127.1 to 130.6; the loop is 11 phrases from beat 1 (beat 0 is cut off the file).
            new Track("disco-house-loop",21627,7859530,-1257,723672,1443997,2163025,2878221,3591269,4302979,5012101,5718977,6425507,7131974,7837668,8543149),
            // 120.00 BPM, steady; the loop is 11 phrases from beat 0.
            new Track("organic-house-loop",8403,8456350,8219,776018,1543940,2311948,3079896,3847904,4615912,5384160,6152168,6920116,7688124,8456192),
            // 174.37 BPM on average, 174.8 down to 173.9; the loop is 15 phrases from beat 0.
            new Track("drum-and-bass-loop",10970,7938879,10970,538343,1066351,1594495,2122751,2651000,3179324,3707759,4236328,4765030,5293779,5822576,6351565,6884679,7409741,7939012,8468891),
            // 83.47 BPM on average (the one-drop beat; the off-beat skank is twice that); the loop is 7 phrases from beat 0.
            new Track("dub-reggae-loop",17576,7746504,17712,1122274,2228711,3335259,4439896,5542379,6644428,7746660),
        };

        /// <summary>The track a camp DJ choice plays (CampFeatures.Tracks' index), or null for SILENCE.</summary>
        public static Track For(int campTrack)=>campTrack>0&&campTrack<=Tracks.Length?Tracks[campTrack-1]:null;

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
