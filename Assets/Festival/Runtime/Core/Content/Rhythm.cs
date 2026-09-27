using System;
using System.Collections.Generic;

namespace Festival.Core
{
    [Serializable] public sealed class RhythmNote { public int Id,Direction; public double TimeSeconds; }
    [Serializable] public sealed class RhythmChart
    {
        public List<RhythmNote> Notes = new List<RhythmNote>();
        public double DurationSeconds;
        public static RhythmChart Create(int seed,int count=8,double beatSeconds=.5)
        {
            if(count<1 || count>256) throw new ArgumentOutOfRangeException(nameof(count));
            if(double.IsNaN(beatSeconds)||double.IsInfinity(beatSeconds)||beatSeconds<.25||beatSeconds>1)throw new ArgumentOutOfRangeException(nameof(beatSeconds));
            var chart=new RhythmChart(); var random=new ContentRandom(seed);
            for(int i=0;i<count;i++) chart.Notes.Add(new RhythmNote { Id=i,Direction=random.Next(4),TimeSeconds=2+i*beatSeconds });
            chart.DurationSeconds=chart.Notes[count-1].TimeSeconds+beatSeconds;
            return chart;
        }
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
