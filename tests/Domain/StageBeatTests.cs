using System;
using Festival.Core;

public static class StageBeatTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        Check(StageBeats.Rate==48000&&StageBeats.PhraseBeats==32,"the measured recordings use 48 kHz samples and 8-bar phrases");
        Check(StageBeats.Tracks.Length==7,"all seven recorded camp choices are available");
        string[] clips={"tech-house-loop","disco-house-loop","organic-house-loop","drum-and-bass-loop","dub-reggae-loop"};
        int[] first={23399,21627,8403,10970,17576},end={8570337,7859530,8456350,7938879,7746504};
        // Independent baseline from the shipped, decode-measured table before extraction.
        int[][] phrases={
            new[]{23317,743034,1462270,2180185,2896025,3609993,4322800,5033984,5743137,6451277,7158477,7865267,8570570},
            new[]{-1257,723672,1443997,2163025,2878221,3591269,4302979,5012101,5718977,6425507,7131974,7837668,8543149},
            new[]{8219,776018,1543940,2311948,3079896,3847904,4615912,5384160,6152168,6920116,7688124,8456192},
            new[]{10970,538343,1066351,1594495,2122751,2651000,3179324,3707759,4236328,4765030,5293779,5822576,6351565,6884679,7409741,7939012,8468891},
            new[]{17712,1122274,2228711,3335259,4439896,5542379,6644428,7746660},
        };
        for(int k=0;k<clips.Length;k++)
        {
            var track=StageBeats.For(k+1);
            Check(track==StageBeats.Tracks[k]&&track.Clip==clips[k],"recording order remains unchanged");
            Check(track.FirstBeat==first[k]&&track.LoopEnd==end[k],clips[k]+": preserve measured click-free loop joins");
            Check(track.Phrases.Length==phrases[k].Length,clips[k]+": preserve every authored phrase");
            for(int j=0;j<phrases[k].Length;j++)
            {
                Check(track.Phrases[j]==phrases[k][j],clips[k]+": unchanged decode-measured phrase anchor");
                Check(Math.Abs(track.Beat(phrases[k][j])-j*32)<1e-9&&Math.Abs(track.Sample(j*32)-phrases[k][j])<1e-9,clips[k]+": both conversions meet each measured anchor");
            }
            for(double b=-1;b<=phrases[k].Length*32;b+=.25)
                Check(Math.Abs(track.Beat(track.Sample(b))-b)<1e-9,clips[k]+": fractional subdivisions round trip across phrase drift");
            Check(Math.Abs(track.LoopSeconds-(end[k]-first[k])/48000.0)<1e-9,clips[k]+": original loop duration");
            double jump=track.BeatPhase(track.LoopEnd)-track.BeatPhase(track.FirstBeat);jump-=Math.Round(jump);
            Check(Math.Abs(jump)*60/track.Bpm<.01,clips[k]+": the existing loop joins within 10 ms of the same beat phase");
        }
        foreach(int choice in new[]{int.MinValue,-1,0,8,int.MaxValue})Check(StageBeats.For(choice)==null,"silence and invalid camp choices play nothing");
        string[] added={"desert-rhythm-loop","industrial-techno-loop"};
        for(int k=0;k<added.Length;k++)
        {
            var track=StageBeats.For(k+6);
            Check(track==StageBeats.Tracks[k+5]&&track.Clip==added[k],"additional camp choices append after the existing five");
            Check(track.Phrases.Length>2&&track.FirstBeat>0&&track.LoopEnd>track.FirstBeat,"additional tracks have an authored loop and phrase grid");
            for(int j=1;j<track.Phrases.Length;j++)Check(track.Phrases[j]>track.Phrases[j-1],"additional phrase anchors increase");
            for(double b=-1;b<=track.Phrases.Length*32;b+=.25)
                Check(Math.Abs(track.Beat(track.Sample(b))-b)<1e-9,added[k]+": fractional beat subdivisions round trip");
            double jump=track.BeatPhase(track.LoopEnd)-track.BeatPhase(track.FirstBeat);jump-=Math.Round(jump);
            Check(Math.Abs(jump)*60/track.Bpm<.01,added[k]+": loop joins on the same beat within 10 ms");
        }
        Check(StageBeats.For(6).FirstBeat==21230&&StageBeats.For(6).LoopEnd==8330191,"desert keeps its validated join");
        Check(StageBeats.For(7).FirstBeat==3183890&&StageBeats.For(7).LoopEnd==8247779,"industrial repeats the measured body after its intro");
        Check(Math.Abs(StageBeats.For(7).Beat(StageBeats.For(7).FirstBeat)-160)<.02,"industrial loop begins at phrase 5, beat 160");
        var tech=StageBeats.For(1);
        Check(Math.Abs(tech.Beat(383175.5)-16)<1e-9,"half of tech house's first measured phrase is beat 16");
        Check(Math.Abs(tech.Sample(40)-922843)<1e-9,"beat 40 follows the second measured phrase's tempo");
        Check(Math.Abs(tech.Sample(400)-8923221.5)<1e-9,"beyond the final anchor use the final measured tempo");
        Check(Math.Abs(tech.Sample(-16)+336541.5)<1e-9,"before the first anchor use the first measured tempo");
        Check(tech.Sample(368)-tech.Sample(336)<tech.Sample(32)-tech.Sample(0),"tech house speeds up; do not flatten the phrase drift to its average BPM");
    }
}
