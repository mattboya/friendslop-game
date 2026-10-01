using System;
using System.Collections.Generic;

namespace Festival.Core
{
    [Serializable] public sealed class RhythmNote { public int Id,Direction; public double TimeSeconds; }
    [Serializable] public sealed class RhythmChart
    {
        public List<RhythmNote> Notes = new List<RhythmNote>();
        public double DurationSeconds;
        // The chart a rhythm challenge plays, the same on the host, in the HUD and in every replay. The tripper's check dance stays
        // four quarter notes whatever the level.
        public static RhythmChart For(InteractionState i)=>Create(i.ChartSeed,i.NoteCount,i.BeatSeconds,i.Kind=="ConfirmDance"?QuarterNotes:i.ChartDifficulty);
        // DANCE-5: after two lead-in quarter notes each beat is a quarter note, eighths (the beat and its "and"), a jump (two lanes at
        // once) or a sixteenth run (3 or 4 notes from the beat), drawn with these percent odds by difficulty, the level the challenge
        // began on: Day 1, Night 1, Day 2, then Night 2 and anything after. Runs start at Night 1.
        static readonly int[][] PatternOdds={new[]{60,25,15,0},new[]{45,25,15,15},new[]{35,30,15,20},new[]{25,30,20,25}};
        const int Eighths=1,Jump=2,Sixteenths=3;
        // The difficulty that puts one quarter note on every beat, as charts were before DANCE-5 (the same seed, the same chart).
        public const int QuarterNotes=-1;
        // `count` beats from a 2 s lead-in. Any two notes a sixteenth apart, across a beat too, are in different lanes.
        public static RhythmChart Create(int seed,int count,double beatSeconds,int difficulty)
        {
            if(count<1 || count>256) throw new ArgumentOutOfRangeException(nameof(count));
            if(double.IsNaN(beatSeconds)||double.IsInfinity(beatSeconds)||beatSeconds<.25||beatSeconds>1)throw new ArgumentOutOfRangeException(nameof(beatSeconds));
            var chart=new RhythmChart{DurationSeconds=2+count*beatSeconds}; var random=new ContentRandom(seed);
            var odds=difficulty<0?null:PatternOdds[Math.Min(difficulty,PatternOdds.Length-1)];
            int before=-1; // the lane a sixteenth before this beat (a 4-note run's last note), or -1
            for(int beat=0;beat<count;beat++)
            {
                int pattern=0;
                if(odds!=null&&beat>=2)for(int roll=random.Next(100);roll>=odds[pattern];pattern++)roll-=odds[pattern];
                int lane=Lane(random,before,-1);Add(chart,beat,0,lane,beatSeconds);
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
        private double earned, elapsed;
        public double GoodWindowSeconds { get; }
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public int ExtraPresses { get; private set; }
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
            bool perfect=best<=0.08+1e-9; earned+=perfect?1:0.6;
            return perfect?"Perfect":"Good";
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
    [Serializable] public struct NoteVisual { public double X,Y,Rotation,Alpha; }
    public static class EffectPresentation
    {
        public static NoteVisual Path(string effectId,int noteId,double progress,bool reducedMotion)
        {
            double u=double.IsNaN(progress)?0:Math.Max(0,Math.Min(1,progress)), remaining=1-u;
            var visual=new NoteVisual { X=0,Y=remaining,Rotation=0,Alpha=1 };
            double phase=(unchecked((uint)noteId)*2654435761u%997)/997.0*Math.PI*2;
            if(effectId=="lsd") visual.X=remaining*(reducedMotion?0.035:0.13)*(Math.Sin(u*11+phase)+0.35*Math.Sin(u*23+phase));
            if(effectId=="mushrooms") visual.X=remaining*(reducedMotion?0.035:0.18)*Math.Sin(u*Math.PI+phase);
            if(effectId=="alcohol") { visual.X=remaining*0.04*Math.Sin(phase+u*6); visual.Rotation=reducedMotion?0:remaining*180*Math.Sin(u*5+phase); }
            return visual;
        }
    }
}
