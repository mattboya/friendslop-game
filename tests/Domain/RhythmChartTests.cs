using System;
using System.Collections.Generic;
using Festival.Core;

// DANCE-5: a chart is still `count` beats long, but after two lead-in quarter notes each beat holds one of four patterns drawn
// from the seed: a quarter note, eighths (the beat and its "and"), a jump (two lanes at once) or a sixteenth run (3 or 4 notes
// from the beat, no two neighbours in one lane). The mix gets busier level by level, and Day 1 has no runs.
public static class RhythmChartTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("RhythmChart: "+message);}
    const string Quarter="quarter",Eighths="eighths",Jump="jump",Sixteenths="run";
    // Day 1, Night 1, Day 2, Night 2, and one past the last level (it keeps Night 2's mix).
    static readonly int[] Levels={0,1,2,3,4};

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{TheSameSeedGivesTheSameChart,DurationsAreUnchangedForABeatCount,EveryNoteSitsOnTheSixteenthGrid,
            IdsCountUpInTimeOrder,EveryBeatIsOneOfTheFourPatterns,JumpsUseTwoDifferentLanes,NeighbouringSixteenthsNeverShareALane,
            TheFirstTwoBeatsAreQuarters,DayOneHasNoSixteenthRunsAndNightOneCan,LaterLevelsMixInMoreOfAllThree,
            AJumpScoresPerfectWhenBothKeysArePressedOnTime,AFastRunScoresEveryNotePressedInTurn,AChallengePlaysTheMixOfTheLevelItBeganOn,
            TheHostScoresTheChartThePlayerSees,TheCheckDanceStaysFourQuarterNotes,ALateGoodOnTheLastSixteenthStillCounts})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" rhythm chart test(s) failed:\n"+string.Join("\n",failures));
    }

    // Many seeds at every level, at both tempos the game plays (.5 s challenges, .4 s poi and DJ sets) and every length it plays.
    static IEnumerable<(RhythmChart chart,int level,double beat,int count)> Sample()
    {
        foreach(int level in Levels)foreach(double beat in new[]{.4,.5})foreach(int count in new[]{1,2,3,4,8,12,16})
            for(int seed=-100;seed<100;seed++)yield return (RhythmChart.Create(unchecked(seed*7919+count),count,beat,level),level,beat,count);
    }
    // A note's place on the sixteenth grid: 0 is the first beat, 4 the second, 5 a sixteenth after it.
    static double Grid(RhythmNote n,double beat)=>(n.TimeSeconds-2)/beat*4;
    static int Slot(RhythmNote n,double beat)=>(int)Math.Round(Grid(n,beat));
    static string Shape(RhythmChart chart,double beat=.5){var parts=new List<string>();foreach(var n in chart.Notes)parts.Add(Slot(n,beat)+":"+n.Direction);return string.Join(" ",parts);}
    // What beat b holds, read from where its notes sit; "?" when it is none of the four patterns.
    static string Pattern(RhythmChart chart,double beat,int b)
    {
        var slots=new List<int>();foreach(var n in chart.Notes)if(Slot(n,beat)/4==b)slots.Add(Slot(n,beat)%4);
        string shape=string.Join(",",slots);
        return shape=="0"?Quarter:shape=="0,2"?Eighths:shape=="0,0"?Jump:shape=="0,1,2"||shape=="0,1,2,3"?Sixteenths:"?";
    }
    // How often each pattern fills a beat after the lead-in, over 2000 sixteen-beat charts at one level.
    const int MixCharts=2000,MixBeats=MixCharts*14;
    static Dictionary<string,int> Mix(int level)
    {
        var mix=new Dictionary<string,int>{{Quarter,0},{Eighths,0},{Jump,0},{Sixteenths,0},{"?",0}};
        for(int seed=0;seed<MixCharts;seed++){var chart=RhythmChart.Create(unchecked(seed*7919+level),16,.5,level);for(int b=2;b<16;b++)mix[Pattern(chart,.5,b)]++;}
        return mix;
    }

    static void TheSameSeedGivesTheSameChart()
    {
        foreach(int level in new[]{RhythmChart.QuarterNotes,0,1,2,3})for(int seed=0;seed<200;seed++)
        {
            var a=RhythmChart.Create(seed,16,.5,level);var b=RhythmChart.Create(seed,16,.5,level);
            Check(a.Notes.Count==b.Notes.Count&&a.DurationSeconds==b.DurationSeconds,"seed "+seed+" at level "+level+" builds the same length twice");
            for(int i=0;i<a.Notes.Count;i++)Check(a.Notes[i].Id==b.Notes[i].Id&&a.Notes[i].Direction==b.Notes[i].Direction&&a.Notes[i].TimeSeconds==b.Notes[i].TimeSeconds,
                "seed "+seed+" at level "+level+": note "+i+" is the same for the host and every client");
        }
        var distinct=new HashSet<string>();for(int seed=0;seed<50;seed++)distinct.Add(Shape(RhythmChart.Create(seed,8,.5,3)));
        Check(distinct.Count>=45,"different seeds give different charts ("+distinct.Count+" of 50)");
    }

    static void DurationsAreUnchangedForABeatCount()
    {
        foreach(var (chart,level,beat,count) in Sample())
        {
            Check(Math.Abs(chart.DurationSeconds-(2+count*beat))<1e-9,"a "+count+"-beat chart at "+beat+" s a beat lasts the 2 s lead-in plus its beats (level "+level+")");
            foreach(var n in chart.Notes)Check(n.TimeSeconds>=2-1e-9&&n.TimeSeconds<chart.DurationSeconds,"every note falls inside its chart (level "+level+")");
        }
    }

    static void EveryNoteSitsOnTheSixteenthGrid()
    {
        int between=0;
        foreach(var (chart,level,beat,count) in Sample())foreach(var n in chart.Notes)
        {
            double grid=Grid(n,beat);
            Check(Math.Abs(grid-Math.Round(grid))<1e-6&&Slot(n,beat)<4*count,"note "+n.Id+" at "+n.TimeSeconds+" s sits on the sixteenth grid ("+grid+", level "+level+")");
            if(Slot(n,beat)%4!=0)between++;
        }
        Check(between>0,"some notes fall between the beats");
    }

    static void IdsCountUpInTimeOrder()
    {
        foreach(var (chart,level,_,_) in Sample())for(int i=0;i<chart.Notes.Count;i++)
            Check(chart.Notes[i].Id==i&&(i==0||chart.Notes[i].TimeSeconds>=chart.Notes[i-1].TimeSeconds),"note ids are unique and count up in time order (level "+level+")");
    }

    static void EveryBeatIsOneOfTheFourPatterns()
    {
        foreach(var (chart,level,beat,count) in Sample())for(int b=0;b<count;b++)
            Check(Pattern(chart,beat,b)!="?","level "+level+" beat "+b+" is a quarter, eighths, a jump or a run of 3 or 4 sixteenths from the beat: "+Shape(chart,beat));
    }

    static void JumpsUseTwoDifferentLanes()
    {
        int jumps=0;
        foreach(var (chart,level,beat,_) in Sample())
        {
            var lanes=new Dictionary<int,List<int>>();
            foreach(var n in chart.Notes){int slot=Slot(n,beat);if(!lanes.ContainsKey(slot))lanes[slot]=new List<int>();lanes[slot].Add(n.Direction);}
            foreach(var together in lanes.Values)
            {
                Check(together.Count<=2,"never three arrows at once (level "+level+": "+Shape(chart,beat)+")");
                if(together.Count==2){jumps++;Check(together[0]!=together[1],"a jump uses two different lanes (level "+level+": "+Shape(chart,beat)+")");}
            }
        }
        Check(jumps>0,"charts have jumps");
    }

    static void NeighbouringSixteenthsNeverShareALane()
    {
        int neighbours=0,acrossABeat=0;
        foreach(var (chart,level,beat,_) in Sample())foreach(var a in chart.Notes)foreach(var b in chart.Notes)if(Slot(b,beat)-Slot(a,beat)==1)
        {
            neighbours++;if(Slot(b,beat)%4==0)acrossABeat++;
            Check(a.Direction!=b.Direction,"two notes a sixteenth apart use different lanes (level "+level+": "+Shape(chart,beat)+")");
        }
        Check(neighbours>0&&acrossABeat>0,"charts have sixteenth neighbours, some across a beat ("+neighbours+", "+acrossABeat+" across)");
    }

    static void TheFirstTwoBeatsAreQuarters()
    {
        foreach(var (chart,level,beat,count) in Sample())for(int b=0;b<Math.Min(2,count);b++)
            Check(Pattern(chart,beat,b)==Quarter,"beat "+(b+1)+" is a lead-in quarter note (level "+level+": "+Shape(chart,beat)+")");
    }

    static void DayOneHasNoSixteenthRunsAndNightOneCan()
    {
        var day1=Mix(0);var night1=Mix(1);
        Check(day1[Sixteenths]==0,"Day 1 charts have no sixteenth runs ("+day1[Sixteenths]+")");
        Check(day1[Quarter]>0&&day1[Eighths]>0&&day1[Jump]>0,"Day 1 mixes quarters, eighths and jumps");
        Check(night1[Sixteenths]>0,"Night 1 charts can have sixteenth runs");
    }

    static void LaterLevelsMixInMoreOfAllThree()
    {
        // Percent of beats after the lead-in that are a quarter / eighths / a jump / a run on Day 1, Night 1, Day 2, Night 2, and past it.
        var odds=new[]{new[]{60,25,15,0},new[]{45,25,15,15},new[]{35,30,15,20},new[]{25,30,20,25},new[]{25,30,20,25}};
        string[] names={Quarter,Eighths,Jump,Sixteenths};
        foreach(int level in Levels)
        {
            var mix=Mix(level);
            for(int k=0;k<4;k++){double percent=100.0*mix[names[k]]/MixBeats;Check(Math.Abs(percent-odds[level][k])<=1.5,"level "+level+": "+names[k]+" fills "+percent.ToString("F1")+"% of beats, want "+odds[level][k]+"%");}
        }
    }

    static void AJumpScoresPerfectWhenBothKeysArePressedOnTime()
    {
        RhythmChart chart=null;RhythmNote left=null,right=null;
        for(int seed=0;right==null&&seed<500;seed++)
        {
            chart=RhythmChart.Create(seed,8,.5,0);
            for(int i=1;i<chart.Notes.Count&&right==null;i++)if(chart.Notes[i].TimeSeconds==chart.Notes[i-1].TimeSeconds){left=chart.Notes[i-1];right=chart.Notes[i];}
        }
        Check(right!=null,"setup: a Day 1 chart with a jump");
        var judge=new RhythmJudge(chart);
        foreach(var n in chart.Notes)if(n!=left&&n!=right)judge.Submit(n.Direction,n.TimeSeconds);
        Check(judge.Submit(left.Direction,left.TimeSeconds+.03)=="Perfect"&&judge.Submit(right.Direction,right.TimeSeconds-.03)=="Perfect","both arrows of a jump pressed on time score Perfect");
        judge.Advance(chart.DurationSeconds+1);
        Check(judge.Score==1&&judge.Hits==chart.Notes.Count&&judge.Misses==0,"a jump is two notes, both hit");
        var one=new RhythmJudge(chart);foreach(var n in chart.Notes)if(n!=right)one.Submit(n.Direction,n.TimeSeconds);one.Advance(chart.DurationSeconds+1);
        Check(one.Misses==1&&one.Score<1,"pressing only one arrow of a jump misses the other");
    }

    // At the fastest tempo (.4 s beats, a sixteenth every .1 s) a run pressed in turn, a little late each time, still scores every
    // note: Perfect up to .08 s late, Good just past it.
    static void AFastRunScoresEveryNotePressedInTurn()
    {
        RhythmChart chart=null;
        for(int seed=0;seed<500&&chart==null;seed++){var c=RhythmChart.Create(seed,12,.4,3);for(int b=2;b<12;b++)if(Pattern(c,.4,b)==Sixteenths&&c.Notes.FindAll(n=>Slot(n,.4)/4==b).Count==4)chart=c;}
        Check(chart!=null,"setup: a fast chart with a 4-note run");
        foreach(var (late,grade,score) in new[]{(.07,"Perfect",1.0),(.09,"Good",.6)})
        {
            var judge=new RhythmJudge(chart);
            foreach(var n in chart.Notes)Check(judge.Submit(n.Direction,n.TimeSeconds+late)==grade,"note "+n.Id+" pressed "+late+" s late scores "+grade+": "+Shape(chart,.4));
            judge.Advance(chart.DurationSeconds+1);
            Check(Math.Abs(judge.Score-score)<1e-9&&judge.Misses==0,"every note pressed "+late+" s late counts once");
        }
    }
    static int sequence;
    // p0 stands 1.4 m from a festivalgoer, on the given level, as the tripper with a vision of them (so a check dance is allowed).
    static FestivalSimulation Floor(int level)
    {
        var s=new FestivalSimulation(4);s.AddPlayer("p0","P0");s.State.Phase="Playing";s.State.LevelIndex=level;s.State.Npcs.Clear();
        s.State.Npcs.Add(new NpcState{Id="partner",X=0,Z=1.4f,Yaw=180,CanTalk=true});var p=s.Player("p0");p.X=0;p.Z=0;
        s.State.TripperId="p0";s.State.Visions.Add(new VisionState{Id="vision",Kind="Buyer",NpcId="partner"});
        return s;
    }
    static InteractionState Start(FestivalSimulation s,string kind)
    {
        var started=s.Execute("p0",new GameCommand{Id="chart"+(sequence++),Kind=kind,TargetId="partner"});
        Check(started.Accepted,"setup: the "+kind+" starts: "+started.Reason);return s.Interaction(s.Player("p0").InteractionId);
    }
    static void Finish(FestivalSimulation s,InteractionState i){for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);Check(i.Status=="Complete","setup: the "+i.Kind+" completes");}

    static void AChallengePlaysTheMixOfTheLevelItBeganOn()
    {
        for(int level=0;level<Festivals.LevelCount;level++)foreach(var kind in new[]{"Dance","Conversation"})
        {
            var s=Floor(level);var i=Start(s,kind);
            Check(i.ChartDifficulty==level,kind+" begun on level "+level+" keeps that level's chart difficulty (got "+i.ChartDifficulty+")");
            Check(Shape(RhythmChart.For(i))==Shape(RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds,level)),kind+" plays level "+level+"'s mix from its seed");
            Check(Math.Abs(i.DurationSeconds-(2+i.NoteCount*i.BeatSeconds))<1e-9,kind+" lasts the lead-in and its beats");
        }
    }

    static void TheHostScoresTheChartThePlayerSees()
    {
        var s=Floor(3);var i=Start(s,"Dance");var chart=RhythmChart.For(i);
        Check(chart.Notes.Count>i.NoteCount,"setup: this Night 2 dance has more notes than beats ("+Shape(chart)+")");
        foreach(var n in chart.Notes)i.Inputs.Add(new RhythmInput{Direction=n.Direction,TimeSeconds=n.TimeSeconds});
        Finish(s,i);Check(i.Score==1,"every note of the chart the HUD draws, pressed on time, scores perfect on the host (got "+i.Score+")");
    }

    static void TheCheckDanceStaysFourQuarterNotes()
    {
        var s=Floor(3);var i=Start(s,"ConfirmDance");
        for(int seed=0;seed<200;seed++)
        {
            i.ChartSeed=seed;var chart=RhythmChart.For(i);
            Check(chart.Notes.Count==4,"a Night 2 check dance is four notes (seed "+seed+": "+Shape(chart)+")");
            for(int b=0;b<4;b++)Check(Pattern(chart,i.BeatSeconds,b)==Quarter,"a Night 2 check dance is all quarter notes (seed "+seed+": "+Shape(chart)+")");
        }
    }

    // A 4-note run's last note sits a sixteenth before the chart ends, so a Good press on it can land after the end.
    static void ALateGoodOnTheLastSixteenthStillCounts()
    {
        var s=Floor(3);var i=Start(s,"Dance");i.ChartDifficulty=3;RhythmChart chart=null; // Night 2's mix (stored at the start; set here so this checks only the press)
        for(int seed=0;seed<1000&&chart==null;seed++){i.ChartSeed=seed;var c=RhythmChart.For(i);if(Slot(c.Notes[c.Notes.Count-1],i.BeatSeconds)==4*i.NoteCount-1)chart=c;}
        Check(chart!=null,"setup: a chart whose last note is the last sixteenth");
        var last=chart.Notes[chart.Notes.Count-1];double press=last.TimeSeconds+.14;
        Check(press>i.DurationSeconds,"setup: a press .14 s after the last note comes after the chart ends");
        foreach(var n in chart.Notes)if(n!=last)i.Inputs.Add(new RhythmInput{Direction=n.Direction,TimeSeconds=n.TimeSeconds});
        for(int guard=0;guard<400&&s.State.SimulationSeconds-i.StartSeconds<press+.05;guard++)s.Tick(.05);
        Check(i.Status=="Active","setup: the dance is still being judged");
        var late=s.Execute("p0",new GameCommand{Id="chart"+(sequence++),Kind="Rhythm",Direction=last.Direction,TimeSeconds=press});
        Check(late.Accepted,"a Good press .14 s after the last note is taken: "+late.Reason);
        Check(!s.Execute("p0",new GameCommand{Id="chart"+(sequence++),Kind="Rhythm",Direction=last.Direction,TimeSeconds=i.DurationSeconds+i.GoodWindowSeconds+.01}).Accepted,
            "a press after every note's window has closed is still refused");
        Finish(s,i);
        Check(Math.Abs(i.Score-(chart.Notes.Count-1+.6)/chart.Notes.Count)<1e-9,"the late press scores Good ("+i.Score+")");
    }
}
