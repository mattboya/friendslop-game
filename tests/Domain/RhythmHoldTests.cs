using System;
using System.Collections.Generic;
using Festival.Core;

// DANCE-10: holds share the chart and judge used by the host and all clients.
public static class RhythmHoldTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("RhythmHold: "+message);}
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{DayOneChartsAreUnchanged,HoldsLastOneOrTwoBeatsAndEndInTime,QuarterNotesNeverHold,HoldingToTheEndScoresAndLettingGoEarlyDrops,ReleaseBoundariesAndUnmatchedInputsAreInert,DropsKeepOneNoteInBothScoreDenominators,MatcherIdsNeverLeakFromAnEarlierInput})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }
    static string Shape(RhythmChart chart)
    {
        var parts=new List<string>();foreach(var n in chart.Notes)parts.Add(((int)Math.Round((n.TimeSeconds-2)*8))+":"+n.Direction);
        return string.Join(" ",parts);
    }
    static void DayOneChartsAreUnchanged()
    {
        // Captured from production before DANCE-10, not regenerated from the changed chart.
        var pins=new[]{
            (0,"0:1 4:2 8:1 8:2 12:0 16:1 20:2 22:0 24:0 26:0 28:2 28:0 32:3 36:2 36:3 40:0 44:2 48:1 52:2 54:2 56:1 60:2 62:2"),
            (7,"0:2 4:1 8:0 12:3 12:1 16:2 18:2 20:0 24:2 26:1 28:3 32:1 32:2 36:2 40:2 44:0 48:1 48:2 52:3 56:1 60:0 62:0"),
            (42,"0:1 4:2 8:1 8:0 12:0 16:2 20:0 22:3 24:0 28:2 32:2 36:1 36:3 40:0 44:0 48:2 50:1 52:1 56:0 58:3 60:2"),
            (-17,"0:3 4:2 8:1 12:3 14:2 16:3 20:2 24:2 28:0 30:2 32:1 36:2 36:3 40:2 44:2 46:3 48:0 50:2 52:2 54:1 56:2 60:3 62:3")};
        foreach(var (seed,shape) in pins)
        {
            var chart=RhythmChart.Create(seed,16,.5,0);Check(Shape(chart)==shape,"Day 1 seed "+seed+" preserves its recorded shape");
            Check(chart.Notes.TrueForAll(n=>n.HoldSeconds==0),"Day 1 contains only taps");
        }
    }
    static RhythmChart OneHold()=>new RhythmChart{DurationSeconds=3,Notes=new List<RhythmNote>{new RhythmNote{Id=17,Direction=2,TimeSeconds=2,HoldSeconds=1}}};
    static void HoldingToTheEndScoresAndLettingGoEarlyDrops()
    {
        foreach(double press in new[]{2.0,2.1})
        {
            double credit=press==2?1:.6;
            var held=new RhythmJudge(OneHold());held.Submit(2,press);
            held.Release(2,2.9);held.Advance(4);
            Check(Math.Abs(held.Score-credit)<1e-9&&held.Drops==0,"a timely release keeps its head's credit");
            var dropped=new RhythmJudge(OneHold());dropped.Submit(2,press);dropped.Release(2,2.5);
            Check(dropped.Score==0&&dropped.ScoreSoFar==0&&dropped.Drops==1&&dropped.LastDroppedNoteId==17&&dropped.LastMatchedNoteId==17,"an early release removes exactly its Perfect or Good credit and exposes its id");
            dropped.Release(2,2.6);dropped.Advance(4);
            Check(dropped.Score==0&&dropped.Hits==1&&dropped.Misses==0&&dropped.Drops==1&&dropped.ExtraPresses==0,"a repeated release cannot drop or penalize the head twice");
            var quiet=new RhythmJudge(OneHold());quiet.Submit(2,press);quiet.Advance(4);
            Check(Math.Abs(quiet.Score-credit)<1e-9&&quiet.Drops==0,"no release keeps the credit for legacy replays and smoke presses");
        }
    }

    static void HoldsLastOneOrTwoBeatsAndEndInTime()
    {
        int one=0,two=0,clamped=0;
        foreach(int level in new[]{1,2,3,9})foreach(double beat in new[]{.25,.4,.5,1.0})foreach(int count in new[]{1,2,3,4,8,16,24,256})
            for(int seed=0;seed<100;seed++)
        {
            var chart=RhythmChart.Create(seed,count,beat,level);
            foreach(var n in chart.Notes)
            {
                Check(n.HoldSeconds==0||n.HoldSeconds==beat||n.HoldSeconds==2*beat,"holds last exactly one or two logical beats");
                if(n.HoldSeconds==0)continue;
                if(n.HoldSeconds==beat)one++;else two++;
                if(n.TimeSeconds>chart.DurationSeconds-2*beat+1e-9)clamped++;
                Check(n.TimeSeconds>=2+2*beat-1e-9,"the first two beats never hold");
                double end=n.TimeSeconds+n.HoldSeconds;
                Check(end<=chart.DurationSeconds+1e-9,"a hold ends inside its chart");
                Check(!chart.Notes.Exists(other=>other!=n&&other.TimeSeconds>=n.TimeSeconds&&other.TimeSeconds<end-1e-9),"every covered beat is a rest with no other head");
                var next=chart.Notes.Find(other=>other.TimeSeconds>=end-1e-9);
                Check(next==null||next.Direction!=n.Direction,"the next playable beat avoids the released lane");
            }
        }
        Check(one>0&&two>0&&clamped>0,"the sample exercises both lengths and final-beat holds");
    }
    static void QuarterNotesNeverHold()
    {
        for(int seed=-100;seed<100;seed++)foreach(int count in new[]{4,24,256})
        {
            var chart=RhythmChart.Create(seed,count,.4,RhythmChart.QuarterNotes);
            Check(chart.Notes.Count==count&&chart.Notes.TrueForAll(n=>n.HoldSeconds==0),"explicit QuarterNotes stays tap-only, including the drum opt-out");
        }
    }
    static void ReleaseBoundariesAndUnmatchedInputsAreInert()
    {
        foreach(double window in new[]{.08,.15,.175})foreach(double offset in new[]{-.000001,0,.000001})
        {
            var judge=new RhythmJudge(OneHold(),window);judge.Submit(2,2);judge.Release(2,3-window+offset);
            bool early=offset<0;
            Check(judge.Drops==(early?1:0)&&judge.Score==(early?0:1),"the exact configured Good boundary retains credit, just before it drops");
            judge.Release(2,2.6);Check(judge.LastDroppedNoteId==null,"a retired hold cannot be dropped by a later input");
        }
        var unmatched=new RhythmJudge(OneHold());
        unmatched.Release(2,2.5);Check(unmatched.ScoreSoFar==null,"an unpressed release judges no head");
        unmatched.Submit(2,2);
        foreach(var input in new[]{(-1,2.5),(4,2.5),(1,2.5),(2,double.NaN),(2,double.PositiveInfinity),(2,-1.0),(2,1.9)})unmatched.Release(input.Item1,input.Item2);
        Check(unmatched.Drops==0&&unmatched.Score==1&&unmatched.ExtraPresses==0&&unmatched.LastDroppedNoteId==null,"wrong lanes, nonfinite times and releases before a hit are inert");
        unmatched.Release(2,2.5);Check(unmatched.Drops==1,"inert releases leave the active hold available");
        var missed=new RhythmJudge(OneHold());missed.Advance(4);missed.Release(2,2.5);
        Check(missed.Hits==0&&missed.Drops==0&&missed.Misses==1&&missed.ExtraPresses==0,"an unpressed head is a miss, not a drop");
        var taps=new RhythmJudge(RhythmChart.Create(7,4,.5,RhythmChart.QuarterNotes));
        foreach(var n in RhythmChart.Create(7,4,.5,RhythmChart.QuarterNotes).Notes){taps.Submit(n.Direction,n.TimeSeconds);taps.Release(n.Direction,n.TimeSeconds+.1);}
        Check(taps.Score==1&&taps.Drops==0&&taps.ExtraPresses==0,"tap releases never change the old score");
    }
    static void DropsKeepOneNoteInBothScoreDenominators()
    {
        var chart=OneHold();chart.DurationSeconds=4;
        chart.Notes.Add(new RhythmNote{Id=31,Direction=0,TimeSeconds=2,HoldSeconds=1});
        chart.Notes.Add(new RhythmNote{Id=44,Direction=1,TimeSeconds=3.5});
        var judge=new RhythmJudge(chart);judge.Submit(2,2.1);judge.Submit(0,2);
        Check(Math.Abs(judge.ScoreSoFar.Value-.8)<1e-9,"Good and Perfect heads use two consumed notes");
        judge.Release(2,2.5);
        Check(Math.Abs(judge.Score-1.0/3)<1e-9&&judge.ScoreSoFar==.5&&judge.Drops==1,"dropping Good removes .6 once without adding a denominator entry or dropping another lane");
        judge.Release(0,3);judge.Submit(1,3.5);judge.Advance(5);
        Check(Math.Abs(judge.Score-2.0/3)<1e-9&&judge.ScoreSoFar==judge.Score&&judge.Hits==3&&judge.Drops==1&&judge.Misses==0,"heads and drops remain distinct at completion");
    }
    static void MatcherIdsNeverLeakFromAnEarlierInput()
    {
        var judge=new RhythmJudge(OneHold());judge.Submit(2,2);
        Check(judge.LastMatchedNoteId==17,"a matched press exposes the production note id");
        judge.Submit(0,2.1);Check(judge.LastMatchedNoteId==null,"an extra press clears the previous match");
        judge.Submit(-1,2.1);Check(judge.LastMatchedNoteId==null,"an invalid press has no match");
        judge.Release(2,2.5);Check(judge.LastDroppedNoteId==17,"a drop exposes the production note id");
        judge.Release(0,2.5);Check(judge.LastDroppedNoteId==null,"an unmatched release clears the previous drop");
    }

}
