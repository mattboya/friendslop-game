using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>The stage DJ set's three-lane ride. A lane change travels as a RhythmInput whose Direction is the new lane, and the
    /// host replays a phrase's changes into its score: the share of blocks caught, less Penalty for each grey hit and each wasted move.</summary>
    public static class DjRide
    {
        public const int Lanes=3,StartLane=1;
        // A grey hit or a wasted move costs what an extra press costs on the arrow lanes, so blind mashing scores nothing there too.
        public const double Penalty=.1;
        // For each step: the percent odds that an off-beat block follows a beat, and that a grey sits on a free off-beat.
        static readonly int[] OffBeatOdds={0,25,35,50},GreyOdds={0,20,25,30};
        public static int Step(InteractionState i)=>Math.Min(OffBeatOdds.Length-1,Math.Max(0,i.ChartDifficulty)+i.Phrase/2);
        public static bool Greys(InteractionState i)=>i.ChartDifficulty>=1;

        /// <summary>`count` beats after a 2 s lead, on the tuned grid as RhythmChart.Create draws, so RhythmChart.For lays it on the
        /// stage beats. A grey sits only in the lane just caught when the next block is in another lane, so it can always be dodged.</summary>
        public static RhythmChart Create(int seed,int count,double beatSeconds,int step,bool greys)
        {
            if(count<1||count>256)throw new ArgumentOutOfRangeException(nameof(count));
            if(double.IsNaN(beatSeconds)||double.IsInfinity(beatSeconds)||beatSeconds<.25||beatSeconds>1)throw new ArgumentOutOfRangeException(nameof(beatSeconds));
            step=Math.Max(0,Math.Min(OffBeatOdds.Length-1,step));
            var random=new ContentRandom(seed);var lanes=new int[count];
            for(int beat=0;beat<count;beat++)lanes[beat]=random.Next(Lanes);
            var chart=new RhythmChart{DurationSeconds=2+count*beatSeconds};
            for(int beat=0;beat<count;beat++)
            {
                Add(chart,beat,lanes[beat],false,beatSeconds);
                if(beat+1>=count)continue;
                if(beat>=2&&random.Next(100)<OffBeatOdds[step])Add(chart,beat+.5,lanes[beat]==1?2*random.Next(2):1,false,beatSeconds);
                else if(greys&&lanes[beat+1]!=lanes[beat]&&random.Next(100)<GreyOdds[step])Add(chart,beat+.5,lanes[beat],true,beatSeconds);
            }
            return chart;
        }
        static void Add(RhythmChart chart,double beat,int lane,bool grey,double beatSeconds)=>chart.Notes.Add(new RhythmNote{Id=chart.Notes.Count,Direction=lane,TimeSeconds=2+beat*beatSeconds,Grey=grey});

        public sealed class Tally
        {
            public int Blocks,Caught,GreysHit,Wasted,Combo;
            public double Score=>Blocks==0?0:Math.Max(0,Math.Min(1,(double)Caught/Blocks-Penalty*(GreysHit+Wasted)));
        }
        /// <summary>A block is caught when the rider was in its lane at any moment within `window` of it, and judged once that window
        /// has passed `until` (chart seconds); a grey is hit when the rider is in its lane as it arrives. A move is wasted unless it
        /// is the first to catch a block, dodges a grey or steps through the middle lane on the way across, and is judged once no
        /// block can still be caught by it. Changes replay in time order, arrival order breaking a tie, from StartLane; a release, a
        /// lane outside 0-2 or the lane already held changes nothing.</summary>
        public static Tally Replay(RhythmChart chart,List<RhythmInput> inputs,double window,double until=double.PositiveInfinity)
        {
            var changes=new List<(double time,int order,int lane)>();
            for(int n=0;n<inputs.Count;n++){var x=inputs[n];if(!x.Release&&x.Direction>=0&&x.Direction<Lanes)changes.Add((x.TimeSeconds,n,x.Direction));}
            changes.Sort((a,b)=>a.time!=b.time?a.time.CompareTo(b.time):a.order.CompareTo(b.order));
            // Each stay in one lane, from the move into it (the phrase's start for the first) to the next, with the lane it came from.
            var stays=new List<(double from,double to,int lane,int was)>{(double.NegativeInfinity,double.PositiveInfinity,StartLane,StartLane)};
            foreach(var c in changes){var last=stays[stays.Count-1];if(c.lane==last.lane)continue;stays[stays.Count-1]=(last.from,c.time,last.lane,last.was);stays.Add((c.time,double.PositiveInfinity,c.lane,last.lane));}
            var useful=new bool[stays.Count];var tally=new Tally();
            foreach(var note in chart.Notes)
            {
                double t=note.TimeSeconds;
                if(note.Grey)
                {
                    int at=stays.FindIndex(s=>s.from<=t&&s.to>t);
                    if(stays[at].lane!=note.Direction){if(stays[at].was==note.Direction)useful[at]=true;}else if(t<=until){tally.GreysHit++;tally.Combo=0;}
                    continue;
                }
                int by=stays.FindIndex(s=>s.lane==note.Direction&&s.from<=t+window&&s.to>t-window);if(by>=0)useful[by]=true;
                if(t+window<=until){tally.Blocks++;if(by>=0){tally.Caught++;tally.Combo++;}else tally.Combo=0;}
            }
            for(int k=1;k<stays.Count;k++)if(!useful[k]&&stays[k].to+2*window<=until&&(k+1==stays.Count||Math.Abs(stays[k].was-stays[k+1].lane)!=2))tally.Wasted++;
            return tally;
        }
    }
}
