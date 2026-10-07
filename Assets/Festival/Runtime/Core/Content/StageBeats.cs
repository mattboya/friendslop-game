using System;

namespace Festival.Core
{
    /// <summary>The seven recordings' authored phrase grid and click-free loop joins, measured from Unity's shipped decode.
    /// Pure sample positions: host rules and presentation share the same drifting beat grid.</summary>
    public static class StageBeats
    {
        public const int Rate=48000,PhraseBeats=32;
        public sealed class Track
        {
            public readonly string Clip;
            public readonly int FirstBeat,LoopEnd;
            public readonly int[] Phrases;
            public Track(string clip,int firstBeat,int loopEnd,params int[] phrases){Clip=clip;FirstBeat=firstBeat;LoopEnd=loopEnd;Phrases=phrases;}
            public double LoopSeconds=>(LoopEnd-FirstBeat)/(double)Rate;
            public double Bpm=>(Beat(LoopEnd)-Beat(FirstBeat))*60/LoopSeconds;
            public float BeatPhase(int sample){double beat=Beat(sample);return (float)(beat-Math.Floor(beat));}
            /// <summary>Beats past the first phrase anchor. Outside the anchors, extends the nearest measured phrase's tempo.</summary>
            public double Beat(double sample)
            {
                int k=0;while(k+2<Phrases.Length&&Phrases[k+1]<=sample)k++;
                return PhraseBeats*(k+(sample-Phrases[k])/(Phrases[k+1]-Phrases[k]));
            }
            /// <summary>The sample position of an authored beat, including fractional subdivisions without sample rounding.</summary>
            public double Sample(double beat)
            {
                int k=(int)Math.Max(0,Math.Min(Phrases.Length-2,Math.Floor(beat/PhraseBeats)));
                return Phrases[k]+(beat/PhraseBeats-k)*(Phrases[k+1]-Phrases[k]);
            }
        }

        // Measured by FestivalMusicTests.PrintTheMeasuredTable from Unity's decode at the shipped quality. Each loop runs from the
        // authored first loop beat to the same beat of the last whole phrase before the fade.
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
            // Strongest beat 122.07 BPM; the loop is 11 phrases from beat 1.
            new Track("desert-rhythm-loop",21230,8330191,-2617,755887,1511303,2266599,3021772,3776823,4531675,5286527,6041439,6796412,7551385,8306478),
            // 145.59 BPM on average; strongest beat 145.40 BPM. The loop repeats 8 phrases from phrase 5 (beat 160), after the sparse intro.
            new Track("industrial-techno-loop",3183890,8247779,11516,648174,1284815,1919939,2550634,3183660,3816251,4450598,5084089,5716630,6351227,6983015,7614198,8247658),
        };

        /// <summary>The camp DJ choice, or null for SILENCE and choices outside the list.</summary>
        public static Track For(int campTrack)=>campTrack>0&&campTrack<=Tracks.Length?Tracks[campTrack-1]:null;
    }
}
