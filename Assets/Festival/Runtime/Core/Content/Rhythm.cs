using System;
using System.Collections.Generic;

namespace Festival.Core
{
    // Grey marks a block to dodge on the DJ ride (DjRide); arrow charts never set it.
    [Serializable] public sealed class RhythmNote { public int Id,Direction; public double TimeSeconds,HoldSeconds; public bool Grey; }
    [Serializable] public sealed class RhythmChart
    {
        public List<RhythmNote> Notes = new List<RhythmNote>();
        public double DurationSeconds;
        // The chart a rhythm challenge plays, the same on the host, in the HUD and in every replay. The tripper's check dance stays
        // four logical quarter notes whatever the level.
        public static RhythmChart For(InteractionState i)
        {
            int difficulty=i.Kind=="ConfirmDance"?QuarterNotes:i.ChartDifficulty;
            RhythmChart Logical()=>i.Kind=="Dj"?DjRide.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds,DjRide.Step(i),DjRide.Greys(i)):Create(i.ChartSeed,i.NoteCount,i.BeatSeconds,difficulty);
            if(i.StageTrack==0)return Logical();
            if(!ValidStageAnchor(i))throw new ArgumentException("Invalid stage chart anchor.",nameof(i));
            var track=StageBeats.For(i.StageTrack);var chart=OnStage(Logical(),i.NoteCount,i.BeatSeconds,track,StageBeatAtWorld(track,i.StageFirstHeadWorldSeconds),i.StageBeatStride);
            double shift=i.StageFirstHeadDelaySeconds-2;
            foreach(var note in chart.Notes)note.TimeSeconds+=shift;
            chart.DurationSeconds+=shift;return chart;
        }
        // After two lead-in quarter notes choose a quarter, eighths, jump, sixteenth run or hold. These percent odds apply to
        // pattern selections; a two-beat hold covers the next beat with a rest. Day 1 keeps its original draws and shapes.
        static readonly int[][] PatternOdds={new[]{60,25,15,0,0},new[]{35,25,15,15,10},new[]{25,30,15,20,10},new[]{15,30,20,25,10}};
        const int Eighths=1,Jump=2,Sixteenths=3,Hold=4;
        // The difficulty that puts one quarter note on every beat, as charts were before DANCE-5 (the same seed, the same chart).
        public const int QuarterNotes=-1;
        // Catalog choices (one-based): existing day/night sets stay unchanged; 0 is reserved for fixed legacy timing.
        public static int StageTrackFor(int festival,int level)=>festival==0?(Festivals.Night(level)?1:2):(Festivals.Night(level)?4:5);
        public static bool ValidStageAnchor(InteractionState i)=>i.StageTrack==0||(i.StageTrack==1||i.StageTrack==2||i.StageTrack==4||i.StageTrack==5)&&Finite(i.StageFirstHeadWorldSeconds)&&i.StageFirstHeadWorldSeconds>=0&&Finite(i.StageFirstHeadDelaySeconds)&&i.StageFirstHeadDelaySeconds>=0&&i.StageFirstHeadDelaySeconds<=60&&(i.StageBeatStride==.5||i.StageBeatStride==1||i.StageBeatStride==2);
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        static double LoopBeatSpan(StageBeats.Track track)=>track.Beat(track.LoopEnd)-track.Beat(track.FirstBeat);
        // Preserve the click-free sample joins exactly. Their offsets from phrase anchors are not whole beat counts.
        // Unwrapped fractional beats carry the exact measured span, so a head or tail crossing a join stays invertible.
        public static double StageSampleAtWorld(StageBeats.Track track,double worldSeconds)
        {
            if(track==null)throw new ArgumentNullException(nameof(track));
            if(!Finite(worldSeconds)||worldSeconds<0)throw new ArgumentOutOfRangeException(nameof(worldSeconds));
            return track.FirstBeat+(worldSeconds-Math.Floor(worldSeconds/track.LoopSeconds)*track.LoopSeconds)*StageBeats.Rate;
        }
        public static double StageBeatAtWorld(StageBeats.Track track,double worldSeconds)=>Math.Floor(worldSeconds/track.LoopSeconds)*LoopBeatSpan(track)+track.Beat(StageSampleAtWorld(track,worldSeconds));
        public static double StageWorldAtBeat(StageBeats.Track track,double beat)
        {
            if(track==null)throw new ArgumentNullException(nameof(track));
            if(!Finite(beat))throw new ArgumentOutOfRangeException(nameof(beat));
            double first=track.Beat(track.FirstBeat),span=LoopBeatSpan(track),cycle=Math.Floor((beat-first)/span);
            return cycle*track.LoopSeconds+(track.Sample(beat-cycle*span)-track.FirstBeat)/StageBeats.Rate;
        }
        public static double NearestStageStride(StageBeats.Track track,double firstBeat,double tunedSeconds)
        {
            if(!Finite(tunedSeconds)||tunedSeconds<=0)throw new ArgumentOutOfRangeException(nameof(tunedSeconds));
            double origin=StageWorldAtBeat(track,firstBeat),best=double.MaxValue,picked=.5;
            foreach(double stride in new[]{.5,1.0,2.0})
            {
                double distance=Math.Abs(StageWorldAtBeat(track,firstBeat+stride)-origin-tunedSeconds);
                if(distance<=best+1e-9){best=distance;picked=stride;}
            }
            return picked;
        }
        // Shared by world dances and advancing minigame charts: the caller owns its clock and preparation start.
        // First head waits through the chart's existing 2 s arrow lead, then chooses the next eligible authored whole beat.
        public static void InitializeStage(InteractionState i,int stageTrack,double startWorldSeconds,double firstHeadLeadSeconds=2)
        {
            var track=StageBeats.For(stageTrack);
            if(track==null||stageTrack==3||stageTrack>5||!Finite(startWorldSeconds)||startWorldSeconds<0||!Finite(firstHeadLeadSeconds)||firstHeadLeadSeconds<0||firstHeadLeadSeconds>59)throw new ArgumentOutOfRangeException(nameof(stageTrack));
            double earliest=startWorldSeconds+firstHeadLeadSeconds,cycle=Math.Floor(earliest/track.LoopSeconds);
            double beat=Math.Ceiling(track.Beat(StageSampleAtWorld(track,earliest))-1e-10),sample=track.Sample(beat);
            if(sample>=track.LoopEnd){cycle++;sample=track.Sample(Math.Ceiling(track.Beat(track.FirstBeat)-1e-10));}
            double anchor=cycle*track.LoopSeconds+(sample-track.FirstBeat)/StageBeats.Rate;
            i.StageTrack=stageTrack;i.StageFirstHeadWorldSeconds=anchor;i.StageFirstHeadDelaySeconds=anchor-startWorldSeconds;
            i.StageBeatStride=NearestStageStride(track,StageBeatAtWorld(track,anchor),i.BeatSeconds);
            i.DurationSeconds=For(i).DurationSeconds;
        }
        // Generate tuned logical draws once, then remap each head and hold endpoint on the same fractional authored grid.
        public static RhythmChart CreateOnStage(int seed,int count,double beatSeconds,int difficulty,StageBeats.Track track,double firstBeat,double stride)
        {
            if(track==null)throw new ArgumentNullException(nameof(track));
            if(!Finite(firstBeat)||stride!=.5&&stride!=1&&stride!=2)throw new ArgumentOutOfRangeException(nameof(firstBeat));
            return OnStage(Create(seed,count,beatSeconds,difficulty),count,beatSeconds,track,firstBeat,stride);
        }
        static RhythmChart OnStage(RhythmChart chart,int count,double beatSeconds,StageBeats.Track track,double firstBeat,double stride)
        {
            double origin=StageWorldAtBeat(track,firstBeat);
            foreach(var note in chart.Notes)
            {
                double headBeat=firstBeat+(note.TimeSeconds-2)/beatSeconds*stride,tailBeat=headBeat+note.HoldSeconds/beatSeconds*stride;
                double head=StageWorldAtBeat(track,headBeat);note.TimeSeconds=2+head-origin;
                note.HoldSeconds=StageWorldAtBeat(track,tailBeat)-head;
            }
            chart.DurationSeconds=2+StageWorldAtBeat(track,firstBeat+count*stride)-origin;return chart;
        }
        // `count` beats from a 2 s lead-in. Any two notes a sixteenth apart, across a beat too, are in different lanes.
        public static RhythmChart Create(int seed,int count,double beatSeconds,int difficulty)
        {
            if(count<1 || count>256) throw new ArgumentOutOfRangeException(nameof(count));
            if(double.IsNaN(beatSeconds)||double.IsInfinity(beatSeconds)||beatSeconds<.25||beatSeconds>1)throw new ArgumentOutOfRangeException(nameof(beatSeconds));
            var chart=new RhythmChart{DurationSeconds=2+count*beatSeconds}; var random=new ContentRandom(seed);
            var odds=difficulty<0?null:PatternOdds[Math.Min(difficulty,PatternOdds.Length-1)];
            int before=-1; // a preceding run's last lane or the hold lane released on this beat, or -1
            for(int beat=0;beat<count;beat++)
            {
                int pattern=0;
                if(odds!=null&&beat>=2)for(int roll=random.Next(100);roll>=odds[pattern];pattern++)roll-=odds[pattern];
                int lane=Lane(random,before,-1);Add(chart,beat,0,lane,beatSeconds);
                if(pattern==Hold)
                {
                    int length=Math.Min(1+random.Next(2),count-beat);
                    chart.Notes[chart.Notes.Count-1].HoldSeconds=length*beatSeconds;
                    before=lane;beat+=length-1;continue;
                }
                if(pattern==Jump)Add(chart,beat,0,Lane(random,before,lane),beatSeconds);
                if(pattern==Eighths)Add(chart,beat,2,Lane(random,-1,-1),beatSeconds);
                before=-1;
                if(pattern==Sixteenths)for(int step=1,length=3+random.Next(2);step<length;step++){lane=Lane(random,lane,-1);Add(chart,beat,step,lane,beatSeconds);if(step==3)before=lane;}
            }
            return chart;
        }
        static void Add(RhythmChart chart,int beat,int sixteenth,int lane,double beatSeconds)=>chart.Notes.Add(new RhythmNote { Id=chart.Notes.Count,Direction=lane,TimeSeconds=2+(beat+sixteenth/4.0)*beatSeconds });
        // A lane drawn evenly from those that are neither a nor b (-1 rules out none), in one draw however many are ruled out.
        static int Lane(ContentRandom random,int a,int b){int pick=random.Next(4-(a>=0?1:0)-(b>=0&&b!=a?1:0));for(int lane=0;;lane++)if(lane!=a&&lane!=b&&pick--==0)return lane;}
    }
    public sealed class RhythmJudge
    {
        private readonly RhythmChart chart;
        private readonly HashSet<int> consumed=new HashSet<int>();
        private readonly Dictionary<int,(int id,double pressed,double end,double credit)> holds=new Dictionary<int,(int,double,double,double)>();
        private double earned, elapsed;
        public double GoodWindowSeconds { get; }
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public int ExtraPresses { get; private set; }
        public int Drops { get; private set; }
        public int? LastMatchedNoteId { get; private set; }
        public int? LastDroppedNoteId { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Action<string> Diagnostic;
#endif
        public double LastErrorSeconds { get; private set; }
        public double Score => Math.Max(0,Math.Min(1,earned/chart.Notes.Count-ExtraPresses*0.1));
        // Like Score, but over only the notes judged so far; null before the first one (nothing to score yet).
        public double? ScoreSoFar => consumed.Count==0?(double?)null:Math.Max(0,Math.Min(1,earned/consumed.Count-ExtraPresses*0.1));
        public bool Complete => elapsed>=chart.DurationSeconds;
        public RhythmJudge(RhythmChart chart,double goodWindow=0.15)
        {
            if(chart==null || chart.Notes==null || chart.Notes.Count==0) throw new ArgumentException("Chart needs notes.",nameof(chart));
            if(double.IsNaN(goodWindow)||double.IsInfinity(goodWindow)||goodWindow<0.08||goodWindow>0.175) throw new ArgumentOutOfRangeException(nameof(goodWindow));
            this.chart=chart; GoodWindowSeconds=goodWindow;
        }
        public string Submit(int direction,double timeSeconds)
        {
            LastMatchedNoteId=null;
            if(direction<0||direction>3||!Finite(timeSeconds)||timeSeconds<0) return "Miss";
            RhythmNote closest=null; double best=double.MaxValue;
            foreach(var note in chart.Notes)
            {
                double distance=Math.Abs(timeSeconds-note.TimeSeconds);
                if(note.Direction==direction&&!consumed.Contains(note.Id)&&distance<=GoodWindowSeconds+1e-9&&distance<best)
                { closest=note; best=distance; }
            }
            if(closest==null) { ExtraPresses++; return "Extra"; }
            consumed.Add(closest.Id); Hits++; LastErrorSeconds=timeSeconds-closest.TimeSeconds;
            bool perfect=best<=0.08+1e-9; double credit=perfect?1:0.6;earned+=credit;LastMatchedNoteId=closest.Id;
            if(closest.HoldSeconds>0)holds[direction]=(closest.Id,timeSeconds,closest.TimeSeconds+closest.HoldSeconds,credit);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Diagnostic?.Invoke("[Festival.RhythmJudge] head id="+closest.Id+" lane="+direction+" error="+LastErrorSeconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
#endif
            return perfect?"Perfect":"Good";
        }
        // Releases never count as presses or new judged notes. Retire a hit hold once; a missing release keeps its head credit.
        public void Release(int direction,double timeSeconds)
        {
            LastDroppedNoteId=null;
            if(direction<0||direction>3||!Finite(timeSeconds)||timeSeconds<0||!holds.TryGetValue(direction,out var hold)||timeSeconds<hold.pressed)return;
            holds.Remove(direction);
            if(timeSeconds<hold.end-GoodWindowSeconds-1e-9)
            {
                earned-=hold.credit;Drops++;LastDroppedNoteId=hold.id;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Diagnostic?.Invoke("[Festival.RhythmJudge] drop id="+hold.id+" lane="+direction+" remaining="+(hold.end-timeSeconds).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
#endif
            }
        }
        // Advance only once the transport grace period expires for these chart times.
        public void Advance(double timeSeconds)
        {
            if(!Finite(timeSeconds)||timeSeconds<elapsed) return;
            elapsed=timeSeconds;
            foreach(var note in chart.Notes) if(!consumed.Contains(note.Id)&&timeSeconds>note.TimeSeconds+GoodWindowSeconds+1e-9)
            { consumed.Add(note.Id); Misses++; }
        }
        private static bool Finite(double value) => !double.IsNaN(value)&&!double.IsInfinity(value);
    }
    [Serializable] public struct NoteVisual { public double X,Y,Rotation,Alpha,Lane; }
    public static class EffectPresentation
    {
        // Shared by the flight and dose presentation; shop effects keep strength zero.
        public static double Strength(int dose)=>dose<1?0:dose==1?.4:dose==2?.6:dose==3?.8:1;
        public static NoteVisual Path(string effectId,int noteId,double progress,bool reducedMotion,int dose=0,int lane=1)
        {
            double u=double.IsNaN(progress)?0:Math.Max(0,Math.Min(1,progress)), remaining=1-u;
            var visual=new NoteVisual { X=0,Y=remaining,Rotation=0,Alpha=1 };
            double phase=(unchecked((uint)noteId)*2654435761u%997)/997.0*Math.PI*2;
            if(effectId=="lsd") visual.X=remaining*(reducedMotion?0.035:0.13)*(Math.Sin(u*11+phase)+0.35*Math.Sin(u*23+phase));
            if(effectId=="mushrooms") visual.X=remaining*(reducedMotion?0.035:0.18)*Math.Sin(u*Math.PI+phase);
            if(effectId=="alcohol") { visual.X=remaining*0.04*Math.Sin(phase+u*6); visual.Rotation=reducedMotion?0:remaining*180*Math.Sin(u*5+phase); }
            double strength=Strength(dose);
            // Direction is explicit: generated note IDs do not identify a lane. Outer lanes always slide inward.
            if(strength>0&&effectId=="lsd")
            {
                double side=lane==0?1:lane==3?-1:Math.Sin(phase)>=0?1:-1, slide=Math.Min(1,u/.75);
                visual.Lane=side*strength*(1-slide*slide);
            }
            if(strength>0&&effectId=="mushrooms")visual.Y=1-(u-.7*strength*Math.Sin(4*Math.PI*u)/(4*Math.PI));
            if(strength>0&&effectId=="ecstasy")visual.Y=1-Math.Pow(u,1+.6*strength);
            if(strength>0&&effectId=="ketamine")
            {
                double stop=1.0/3, resume=stop+.3*strength;
                visual.Y=1-(u<stop?u:u<=resume?stop:stop+(1-stop)*(u-resume)/(1-resume));
            }
            if(strength>0&&effectId=="weed")visual.Y=Math.Pow(remaining,1+.6*strength);
            if(strength>0&&u==1) { visual.X=0;visual.Y=0;visual.Lane=0; }
            return visual;
        }
    }
}
