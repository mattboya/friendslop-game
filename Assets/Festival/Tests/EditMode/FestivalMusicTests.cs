using System;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Festival.Tests
{
    // MUSIC-2: the camp DJ's seven recorded tracks. Each streams from Resources/Music, and FestivalMusic's beat grid and loop are
    // measured from the recording itself (Measure, below, printed the table). These tests decode each track the way the game plays
    // it and hold the table to it: the tempo, BeatPhase on the beats all the way through the loop, and a loop join with no gap and
    // no click.
    public sealed class FestivalMusicTests
    {
        // A streamed clip can't be read back, so each track is decoded from a throwaway copy that decompresses on load. The copy keeps
        // the shipped compression (Vorbis at 70%), so these are the samples the game plays.
        private const string Probe="Assets/Festival/Tests/EditMode/MusicProbe";
        // The tempo range each style is measured in, from the queue's expectations, widened: house about 115-135 BPM, drum and bass
        // 160-185, and dub reggae's one-drop 70-95 (its off-beat skank doubles that).
        private static readonly Dictionary<string,(double Low,double High)> Styles=new Dictionary<string,(double,double)>{
            {"tech-house-loop",(115,135)},{"disco-house-loop",(115,135)},{"organic-house-loop",(115,135)},{"drum-and-bass-loop",(160,185)},{"dub-reggae-loop",(70,95)},{"desert-rhythm-loop",(110,135)},{"industrial-techno-loop",(135,155)}};
        private static readonly Dictionary<string,float[]> decoded=new Dictionary<string,float[]>();

        [OneTimeSetUp]public void Decode()
        {
            AssetDatabase.DeleteAsset(Probe);AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(Probe),System.IO.Path.GetFileName(Probe));
            foreach(var track in FestivalMusic.Tracks)
            {
                string shipped=AssetDatabase.GetAssetPath(track.Audio),copy=Probe+"/"+track.Clip+".mp3";
                Assert.That(shipped,Is.Not.Empty,"setup: "+track.Resource+" is an asset");
                Assert.That(AssetDatabase.CopyAsset(shipped,copy),Is.True,"setup: copy "+shipped);
                var importer=(AudioImporter)AssetImporter.GetAtPath(copy);var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(copy);clip.LoadAudioData();
                var pcm=new float[clip.samples*clip.channels];
                Assert.That(clip.GetData(pcm,0),Is.True,"setup: decode "+copy);
                decoded[track.Clip]=pcm;
            }
        }
        [OneTimeTearDown]public void Remove(){AssetDatabase.DeleteAsset(Probe);decoded.Clear();}

        [Test]public void TheCampDjPlaysSilenceThenTheSevenTracks()
        {
            Assert.That(CampFeatures.Tracks,Is.EqualTo(new[]{"SILENCE","TECH HOUSE","DISCO HOUSE","ORGANIC HOUSE","DRUM & BASS","DUB REGGAE","DESERT RHYTHM","INDUSTRIAL TECHNO"}),"the DJ's choices");
            Assert.That(FestivalMusic.For(0),Is.Null,"SILENCE plays nothing");
            string[] clips={"tech-house-loop","disco-house-loop","organic-house-loop","drum-and-bass-loop","dub-reggae-loop","desert-rhythm-loop","industrial-techno-loop"};
            for(int i=1;i<CampFeatures.Tracks.Length;i++)Assert.That(FestivalMusic.For(i)?.Clip,Is.EqualTo(clips[i-1]),CampFeatures.Tracks[i]+" plays its recording");
            Assert.That(FestivalMusic.For(CampFeatures.Tracks.Length),Is.Null,"no track past the list");
            Assert.That(FestivalMusic.For(new RoundState().CampMusicTrack)?.Clip,Is.EqualTo("tech-house-loop"),"a new camp starts on TECH HOUSE");
        }

        [TestCase("desert-rhythm-loop",6,21230,8330191,122.00764138066708,8641152)]
        [TestCase("industrial-techno-loop",7,3183890,8247779,145.5924677621046,8629632)]
        public void TheAdditionalRecordingLoadsWithItsValidatedBodyLoop(string clip,int choice,int first,int end,double bpm,int samples)
        {
            var audio=Resources.Load<AudioClip>("Music/"+clip);
            Assert.That(audio,Is.Not.Null,"the additional recording ships in Resources");
            var track=FestivalMusic.For(choice);
            Assert.That(track,Is.Not.Null,"the camp choice selects the additional recording");
            Assert.That((track.Clip,track.FirstBeat,track.LoopEnd),Is.EqualTo((clip,first,end)),"the validated sample anchors are retained");
            Assert.That(track.Bpm,Is.EqualTo(bpm).Within(1e-10));
            if(choice==6)Assert.That(352*60/track.LoopSeconds,Is.EqualTo(122.00803445821927).Within(1e-10),"the desert measurement counts 352 whole beats; grid tempo keeps fractional join nudges");
            Assert.That(audio.samples,Is.EqualTo(samples),"the shipped decode retains the full introductory passage");
        }

        [Test]public void EveryTrackStreamsFromResourcesAsTheGamePlaysIt()
        {
            foreach(var track in FestivalMusic.Tracks)
            {
                var clip=Resources.Load<AudioClip>(track.Resource);
                Assert.That(clip,Is.Not.Null,track.Resource+" loads from Resources");
                Assert.That(track.Audio,Is.EqualTo(clip),track.Clip+": the table's recording is that clip");
                Assert.That(clip.loadType,Is.EqualTo(AudioClipLoadType.Streaming),track.Clip+" streams");
                Assert.That((clip.frequency,clip.channels),Is.EqualTo((FestivalMusic.Rate,2)),track.Clip+": stereo at the table's rate, not forced to mono");
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));var settings=importer.defaultSampleSettings;
                Assert.That((settings.loadType,settings.compressionFormat,settings.preloadAudioData,importer.forceToMono),Is.EqualTo((AudioClipLoadType.Streaming,AudioCompressionFormat.Vorbis,false,false)),track.Clip+"'s .meta: streamed Vorbis, not preloaded, stereo");
                Assert.That(settings.sampleRateSetting,Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate),track.Clip+": source sample rate is preserved");
                Assert.That((importer.loadInBackground,importer.ambisonic),Is.EqualTo((false,false)),track.Clip+": matching import flags");
                Assert.That(settings.quality,Is.EqualTo(.7f).Within(.005f),track.Clip+": Vorbis quality 70");
                Assert.That(decoded[track.Clip].Length/2,Is.EqualTo(clip.samples),track.Clip+": the decoded copy is the shipped clip, sample for sample");
            }
        }

        [Test]public void TheDecodedRecordingsAndCampApiUseThePureAuthoredGrid()
        {
            for(int k=0;k<FestivalMusic.Tracks.Length;k++)
            {
                var track=FestivalMusic.For(k+1);var grid=StageBeats.For(k+1);
                Assert.That(track.Beats,Is.SameAs(grid),track.Clip+": a single measured grid drives host and camp");
                Assert.That((track.Clip,track.FirstBeat,track.LoopEnd,track.LoopSeconds,track.Bpm),Is.EqualTo((grid.Clip,grid.FirstBeat,grid.LoopEnd,grid.LoopSeconds,grid.Bpm)),track.Clip+": the camp API keeps its loop and tempo");
                Assert.That(track.Phrases,Is.SameAs(grid.Phrases),track.Clip+": phrase anchors are not duplicated");
                Assert.That(track.Audio.frequency,Is.EqualTo(StageBeats.Rate),track.Clip+": real decode uses the pure table's sample rate");
                var onsets=Onsets(decoded[track.Clip]);int heard=0,beats=0;
                int first=(int)Math.Ceiling(grid.Beat(grid.FirstBeat)),end=(int)Math.Floor(grid.Beat(grid.LoopEnd));
                for(int beat=first;beat<=end;beat++)
                {
                    double sample=grid.Sample(beat),quarter=StageBeats.Rate*15/grid.Bpm;
                    double offset=StrongestOnset(onsets,sample,quarter);
                    if(!double.IsNaN(offset)&&Math.Abs(offset)<=.03)heard++;
                    beats++;
                    Assert.That(track.BeatPhase((int)Math.Round(sample)),Is.EqualTo(grid.BeatPhase((int)Math.Round(sample))),track.Clip+": the playing source reads Core's conversion");
                    Assert.That(grid.Beat(sample),Is.EqualTo(beat).Within(1e-9),track.Clip+": a measured beat converts back without rounding");
                }
                Assert.That(heard,Is.GreaterThanOrEqualTo(beats*.6),track.Clip+": Core's converted beats land on the decoded recording's onsets");
            }
        }

        [Test]public void EachTempoMatchesAFreshMeasurement()
        {
            foreach(var track in FestivalMusic.Tracks)
            {
                var style=Styles[track.Clip];var measured=Measure(decoded[track.Clip],style.Low,style.High,(int)Math.Round(track.Beats.Beat(track.FirstBeat)));
                Assert.That(track.Bpm,Is.InRange(style.Low,style.High),track.Clip+"'s tempo is in its style's range");
                Assert.That(track.Bpm,Is.EqualTo(measured.Bpm).Within(1),track.Clip+"'s tempo, measured again from its samples");
            }
        }

        [Test]public void TheMeasurementPrinterRetainsTheIndustrialBodyLoop()
        {
            var track=FestivalMusic.For(7);
            Assert.That(track,Is.Not.Null,"the authored industrial track exists");
            var style=Styles[track.Clip];var measured=Measure(decoded[track.Clip],style.Low,style.High,(int)Math.Round(track.Beats.Beat(track.FirstBeat)));
            Assert.That(measured.FirstBeat,Is.EqualTo(track.FirstBeat),"remeasurement keeps phrase 5 rather than repeating the sparse intro");
            Assert.That(measured.LoopEnd,Is.EqualTo(track.LoopEnd),"the body join is measured against the selected start");
            Assert.That(measured.Phrases,Is.EqualTo(track.Phrases),"the first-pass introductory anchors are preserved too");
            Assert.That(measured.Bpm,Is.EqualTo(track.Bpm).Within(1e-10),"printed average includes the measured fractional join positions");
        }

        // The beats BeatPhase marks (where it wraps to 0) against the onsets heard in the recording, phrase by phrase through the loop.
        // A beat is heard when a strong onset (3 times the track's average) is the loudest within a quarter beat of it, and lands
        // within 30 ms. Breakdowns have few onsets, so they count toward the total but not the per-phrase check.
        [Test]public void BeatPhaseLandsOnTheBeatsHeardThroughTheWholeLoop()
        {
            foreach(var track in FestivalMusic.Tracks)
            {
                var onsets=Onsets(decoded[track.Clip]);int heard=0,beats=0;var offsets=new List<double>();var phrases=new List<string>();
                var phrase=new List<double>();int phraseBeats=0;
                double previous=track.BeatPhase(track.FirstBeat-Hop);
                for(int sample=track.FirstBeat;sample<=track.LoopEnd;sample+=Hop)
                {
                    double phase=track.BeatPhase(sample);
                    if(phase<previous-.5)
                    {
                        double beat=sample-phase*Hop/(phase+1-previous),quarter=FestivalMusic.Rate*15/track.Bpm;
                        beats++;phraseBeats++;
                        double offset=StrongestOnset(onsets,beat,quarter);
                        if(!double.IsNaN(offset)&&Math.Abs(offset)<=.03){heard++;offsets.Add(offset);phrase.Add(offset);}
                        if(phraseBeats==FestivalMusic.PhraseBeats)
                        {
                            if(phrase.Count>=8)
                            {
                                double median=Median(phrase);phrases.Add($"{median*1000:F0} ms ({phrase.Count})");
                                Assert.That(Math.Abs(median),Is.LessThanOrEqualTo(.025),track.Clip+": the beats of 8 bars sit on the onsets ("+string.Join(", ",phrases)+")");
                            }
                            phrase.Clear();phraseBeats=0;
                        }
                    }
                    previous=phase;
                }
                Assert.That(beats,Is.GreaterThan(100),"setup: "+track.Clip+" has a loop of beats");
                Assert.That(heard,Is.GreaterThanOrEqualTo(beats*.6),track.Clip+": most of the loop's beats land on an onset");
                Assert.That(Math.Abs(Median(offsets)),Is.LessThanOrEqualTo(.01),track.Clip+": on the beat, not early or late");
            }
        }

        [Test]public void EachLoopJoinsWithoutAGapOrAClick()
        {
            foreach(var track in FestivalMusic.Tracks)
            {
                var pcm=decoded[track.Clip];int first=track.FirstBeat,end=track.LoopEnd;
                Assert.That(0<first&&first<end&&end<=pcm.Length/2,"setup: "+track.Clip+"'s loop lies inside the recording");
                // What's heard around the join: the 200 ms before the loop's end, then the 200 ms from its first beat.
                float Heard(int i,int channel)=>i<0?pcm[2*(end+i)+channel]:pcm[2*(first+i)+channel];
                int quiet=0,longest=0;
                for(int i=-9600;i<9600;i++){quiet=Math.Abs(Heard(i,0))<.001f&&Math.Abs(Heard(i,1))<.001f?quiet+1:0;longest=Math.Max(longest,quiet);}
                Assert.That(longest,Is.LessThanOrEqualTo(FestivalMusic.Rate/50),track.Clip+": no gap of 20 ms or more across the join");
                var steps=new List<float>();
                for(int i=-479;i<480;i++)if(i!=0)for(int c=0;c<2;c++)steps.Add(Math.Abs(Heard(i,c)-Heard(i-1,c)));
                steps.Sort();float usual=steps[(int)(steps.Count*.99)];
                for(int c=0;c<2;c++)Assert.That(Math.Abs(Heard(0,c)-Heard(-1,c)),Is.LessThanOrEqualTo(usual),track.Clip+": the join steps no further than the music either side of it (no click), channel "+c);
                double jump=track.BeatPhase(end)-track.BeatPhase(first);jump-=Math.Round(jump);
                Assert.That(Math.Abs(jump)*60/track.Bpm,Is.LessThanOrEqualTo(.01),track.Clip+": the beat runs on through the join (it picks up where it left off, within 10 ms)");
            }
        }

        [Test,Explicit("Prints StageBeats' measured table. Run it after a recording changes, and paste the result into StageBeats.Tracks.")]
        public void PrintTheMeasuredTable()
        {
            var table=new System.Text.StringBuilder();
            foreach(var track in FestivalMusic.Tracks)
            {
                var style=Styles[track.Clip];var m=Measure(decoded[track.Clip],style.Low,style.High,(int)Math.Round(track.Beats.Beat(track.FirstBeat)));
                table.AppendLine($"            // {m.Bpm:F2} BPM on average over {m.Phrases.Length} phrase starts. {m.Notes}");
                table.AppendLine($"            new Track(\"{track.Clip}\",{m.FirstBeat},{m.LoopEnd},{string.Join(",",m.Phrases)}),");
            }
            Debug.Log("StageBeats.Tracks, measured:\n"+table);
        }

        // ---- Measuring a recording's beat ----

        private const int Hop=240;
        private sealed class Measured { public double Bpm; public int[] Phrases; public int FirstBeat,LoopEnd; public string Notes=""; }

        private static double Median(List<double> values){var sorted=values.OrderBy(v=>v).ToList();return sorted.Count==0?double.NaN:sorted[sorted.Count/2];}

        // Onset strength every 5 ms (Hop samples): how fast the loudness of a 20 ms window rises, log-compressed so quiet and loud
        // passages count alike, and scaled to average 1.
        private static float[] Onsets(float[] pcm)
        {
            int hops=pcm.Length/2/Hop;var square=new double[hops];
            for(int f=0;f<hops*Hop;f++){double m=(pcm[2*f]+pcm[2*f+1])/2;square[f/Hop]+=m*m;}
            var energy=new double[hops];double mean=0;
            for(int h=0;h<hops;h++){double s=0;for(int k=h-2;k<=h+1;k++)if(k>=0&&k<hops)s+=square[k];energy[h]=s/(4*Hop);mean+=energy[h]/hops;}
            double floor=1e-4*mean,total=0;var onsets=new float[hops];
            for(int h=2;h<hops;h++){onsets[h]=(float)Math.Max(0,Math.Log(energy[h]+floor)-Math.Log(energy[h-2]+floor));total+=onsets[h];}
            float scale=(float)(hops/Math.Max(total,1e-9));for(int h=0;h<hops;h++)onsets[h]*=scale;
            return onsets;
        }

        // Seconds from a beat (a sample position) to the strongest onset peak within a quarter beat of it, if that peak is strong.
        private static double StrongestOnset(float[] onsets,double beat,double quarter)
        {
            int best=-1;float strongest=3;
            for(int h=(int)((beat-quarter)/Hop);h<=(int)((beat+quarter)/Hop);h++)
                if(h>0&&h<onsets.Length-1&&onsets[h]>=strongest&&onsets[h]>=onsets[h-1]&&onsets[h]>=onsets[h+1]){strongest=onsets[h];best=h;}
            return best<0?double.NaN:((double)best*Hop-beat)/FestivalMusic.Rate;
        }

        // The strongest beat frequency between low and high BPM, to 0.01 BPM.
        private static double Tempo(float[] onsets,double low,double high)
        {
            double best=low,strongest=-1;
            for(double bpm=low;bpm<=high;bpm+=.01)
            {
                double w=2*Math.PI*bpm/60*Hop/FestivalMusic.Rate,cos=Math.Cos(w),sin=Math.Sin(w),re=1,im=0,sumRe=0,sumIm=0;
                for(int n=0;n<onsets.Length;n++)
                {
                    sumRe+=onsets[n]*re;sumIm+=onsets[n]*im;
                    double next=re*cos-im*sin;im=re*sin+im*cos;re=next;
                    if((n&1023)==0){double norm=Math.Sqrt(re*re+im*im);re/=norm;im/=norm;}
                }
                if(sumRe*sumRe+sumIm*sumIm>strongest){strongest=sumRe*sumRe+sumIm*sumIm;best=bpm;}
            }
            return best;
        }

        private static double At(float[] onsets,double hop){if(hop<0||hop>=onsets.Length-1)return 0;int i=(int)hop;double f=hop-i;return onsets[i]*(1-f)+onsets[i+1]*f;}
        // The onsets on an even beat grid, 16 beats either side of a centre (in hops).
        private static double Comb(float[] onsets,double centre,double beat){double s=0;for(int j=-FestivalMusic.PhraseBeats/2;j<=FestivalMusic.PhraseBeats/2;j++)s+=At(onsets,centre+j*beat);return s;}
        private static double Rms(float[] pcm,long from,long to)
        {
            from=Math.Max(0,from);to=Math.Min(pcm.Length/2,to);if(to<=from)return 0;
            double q=0;for(long i=2*from;i<2*to;i++)q+=pcm[i]*pcm[i];return Math.Sqrt(q/(2*(to-from)));
        }

        // A recording's beat. The tempo is the strongest beat frequency in the style's range. Then, from the first sound, each 8-bar
        // phrase start is fitted to the onsets of the 16 beats either side of it, at a tempo within 0.6% of the last phrase's, because
        // the tracks drift. The loop runs from the first beat to the same beat of the last phrase whose final bar is still at least
        // three quarters as loud as the bar before it (so not fading out). An authored start retains a later body loop.
        // Last, each end moves up to 5 ms to where the samples either side of the join match in value and slope.
        private static Measured Measure(float[] pcm,double low,double high,int authoredFirstBeat=-1)
        {
            var onsets=Onsets(pcm);double bpm=Tempo(onsets,low,high),period=60/bpm*FestivalMusic.Rate/Hop;long frames=pcm.Length/2;
            int firstSound=0;while(firstSound<frames/Hop&&Rms(pcm,(long)firstSound*Hop,(long)(firstSound+1)*Hop)<=.01)firstSound++;
            var anchors=new List<double>();double predicted=firstSound,beatHops=period;
            while(predicted<onsets.Length)
            {
                bool first=anchors.Count==0;
                double reach=first?period*.5:period*.25,slow=first?period*1.03:beatHops*1.006,fast=first?period*.97:beatHops*.994;
                double best=-1,centre=predicted,fitted=beatHops;
                for(double t=fast;t<=slow;t+=period*.0005)for(double c=predicted-reach;c<=predicted+reach;c+=.25)
                {double s=Comb(onsets,c,t);if(s>best){best=s;centre=c;fitted=t;}}
                anchors.Add(centre);beatHops=fitted;predicted=centre+FestivalMusic.PhraseBeats*fitted;
            }
            double Beat(int n){int k=Math.Min(Math.Max(n/FestivalMusic.PhraseBeats,0),anchors.Count-2);return Hop*(anchors[k]+(anchors[k+1]-anchors[k])*(n-(double)FestivalMusic.PhraseBeats*k)/FestivalMusic.PhraseBeats);}
            int firstBeat=0;while(Beat(firstBeat)<0)firstBeat++;
            int lastBeat=-1;
            for(int n=firstBeat+FestivalMusic.PhraseBeats;Beat(n)<=frames;n+=FestivalMusic.PhraseBeats)
                if(Rms(pcm,(long)Beat(n-4),(long)Beat(n))>=.75*Rms(pcm,(long)Beat(n-8),(long)Beat(n-4)))lastBeat=n;
            (long Start,long Stop) Join(long start,long stop)
            {
                const int Nudge=FestivalMusic.Rate/200;double miss=double.MaxValue;long joinedStart=start,joinedStop=stop;
                for(long e=stop-Nudge;e<=stop+Nudge;e++)for(long a=Math.Max(1,start-Nudge);a<=start+Nudge;a++)
                {
                    double m=0;
                    for(int c=0;c<2;c++){double v=pcm[2*e+c]-pcm[2*a+c],slope=pcm[2*e+c]-pcm[2*(e-1)+c]-(pcm[2*a+c]-pcm[2*(a-1)+c]);m+=v*v+4*slope*slope;}
                    if(m<miss){miss=m;joinedStart=a;joinedStop=e;}
                }
                return (joinedStart,joinedStop);
            }
            var join=Join((long)Math.Round(Beat(firstBeat)),(long)Math.Round(Beat(lastBeat)));
            var phrases=anchors.Select(a=>(int)Math.Round(a*Hop)).ToArray();
            // Honor a later authored body start using the printed integer phrase grid, then refit against the measured end.
            // This matches the validated two-step measurement while retaining the introduction's anchors for the first pass.
            if(authoredFirstBeat>firstBeat)
            {
                var introductoryGrid=new StageBeats.Track("measured",(int)join.Start,(int)join.Stop,phrases);
                join=Join((long)Math.Round(introductoryGrid.Sample(authoredFirstBeat)),join.Stop);firstBeat=authoredFirstBeat;
            }
            long joinStart=join.Start,joinStop=join.Stop;
            var grid=new StageBeats.Track("measured",(int)joinStart,(int)joinStop,phrases);
            return new Measured{Bpm=grid.Bpm,Phrases=phrases,
                FirstBeat=(int)joinStart,LoopEnd=(int)joinStop,Notes=$"Strongest beat {bpm:F2} BPM; the loop is {(lastBeat-firstBeat)/FestivalMusic.PhraseBeats} phrases from beat {firstBeat}."};
        }
    }
}
