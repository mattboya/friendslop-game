using System;
using System.Collections.Generic;
using Festival.Core;

public static class RhythmReleaseTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("RhythmRelease: "+message);}
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{TheHostTakesAReleaseWithoutStepping,OnlyPressAndReleaseAmountsAreAccepted,
            EveryFinishedRhythmScoresReleasesThroughTheHoldJudge,TrustDancesLoseTrustOnlyForAnEarlyDrop,ArrivalOrderIsNeverReplacedByTimestampOrder,
            AStillDancingFriendsDropSetsTheGroupsWorstScore,ReleasesShareTheFiniteTimingEnvelope,
            TheInputCapCountsReleasesAndDeduplicatesCommands,ConfiguredChartsFitIncludingTheirHoldReleases,DebugTracingIsAbsentFromReleaseRules,
            CheckDanceTapReleasesNeverCostCreditOrSteps})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }
    static FestivalSimulation Floor()
    {
        var game=new FestivalSimulation(7);var player=game.AddPlayer("p0","Tester");
        game.State.Phase="Playing";game.State.LevelIndex=3;game.State.SimulationSeconds=10;game.State.Npcs.Clear();
        player.X=0;player.Z=0;
        game.State.Npcs.Add(new NpcState{Id="partner",X=0,Z=1.5f,Yaw=0,CanTalk=true});
        return game;
    }
    static InteractionState Start(FestivalSimulation game,string kind="Dance")
    {
        var player=game.Player("p0");string command=kind,item="";
        if(kind=="Poi"){item="poi_practice";player.Inventory.Add(new ItemStack{ItemId=item,Count=1});}
        if(kind=="Dj"){player.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});player.X=Catalog.StageTakeoverX;player.Z=Catalog.StageTakeoverZ;}
        var result=game.Execute("p0",new GameCommand{Id="release-start"+(sequence++),Kind=command,TargetId="partner",ItemId=item});
        Check(result.Accepted,"setup starts "+kind+": "+result.Reason);return game.Interaction(game.Player("p0").InteractionId);
    }
    static CommandResult Input(FestivalSimulation game,InteractionState interaction,int lane,double time,int amount=0)
    {
        return game.Execute(interaction.PlayerId,new GameCommand{Id="release-input"+(sequence++),Kind="Rhythm",Direction=lane,TimeSeconds=time,Amount=amount});
    }
    static void TheHostTakesAReleaseWithoutStepping()
    {
        var game=Floor();var interaction=Start(game);var player=game.Player("p0");
        game.State.SimulationSeconds=interaction.StartSeconds+2.25;player.VisualDanceStepDirection=3;
        int steps=player.VisualDanceStepSequence;
        var released=Input(game,interaction,1,2.25,1);
        Check(released.Accepted,"an in-envelope release is accepted");
        Check(player.VisualDanceStepSequence==steps&&player.VisualDanceStepDirection==3,"a release must leave both dance-step fields unchanged");
        Check(interaction.Inputs.Count==1,"a release is one recorded input");
        var note=RhythmChart.For(interaction).Notes[0];game.State.SimulationSeconds=interaction.StartSeconds+note.TimeSeconds;
        Check(Input(game,interaction,note.Direction,note.TimeSeconds).Accepted,"Amount defaults to a press");
        Check(player.VisualDanceStepSequence==steps+1&&player.VisualDanceStepDirection==note.Direction,"a default press advances the dancer once");
    }
    static void OnlyPressAndReleaseAmountsAreAccepted()
    {
        foreach(int amount in new[]{-1,2,int.MaxValue})
        {
            var game=Floor();var interaction=Start(game);game.State.SimulationSeconds=interaction.StartSeconds+2;
            Check(!Input(game,interaction,0,2,amount).Accepted,"Amount "+amount+" is neither a press nor a release");
            Check(interaction.Inputs.Count==0&&game.Player("p0").VisualDanceStepSequence==0,"invalid kinds change no input or step");
        }
    }

    static RhythmChart HoldChart(InteractionState interaction)
    {
        for(int seed=0;seed<1000;seed++)
        {
            interaction.ChartSeed=seed;var chart=RhythmChart.For(interaction);
            // At least fourteen heads also makes the lost-note verdict differ from the old .1 extra-press penalty.
            if(chart.Notes.Count>=14&&chart.Notes.Exists(note=>note.HoldSeconds>0))return chart;
        }
        throw new Exception("setup: a generated hold chart with at least fourteen heads");
    }
    static void Send(FestivalSimulation game,InteractionState interaction,List<(int lane,double time,int amount)> inputs)
    {
        foreach(var input in inputs)
        {
            game.State.SimulationSeconds=Math.Max(game.State.SimulationSeconds,interaction.StartSeconds+input.time);
            var result=Input(game,interaction,input.lane,input.time,input.amount);
            Check(result.Accepted,"a real "+interaction.Kind+" input is accepted: "+result.Reason);
        }
    }
    static List<(int lane,double time,int amount)> Heads(RhythmChart chart)
    {
        var inputs=new List<(int,double,int)>();foreach(var note in chart.Notes)inputs.Add((note.Direction,note.TimeSeconds,0));return inputs;
    }
    static void Verdict(FestivalSimulation game,InteractionState interaction)
    {
        game.State.SimulationSeconds=interaction.StartSeconds+interaction.DurationSeconds+.75;game.Tick(.1);
    }
    static void EveryFinishedRhythmScoresReleasesThroughTheHoldJudge()
    {
        foreach(string kind in new[]{"Dance","Poi","Dj"})foreach(double error in new[]{0,.1})foreach(string release in new[]{"early","timely","none"})
        {
            var game=Floor();var interaction=Start(game,kind);var chart=HoldChart(interaction);var hold=chart.Notes.Find(note=>note.HoldSeconds>0);
            var inputs=Heads(chart);int at=inputs.FindIndex(input=>input.lane==hold.Direction&&input.time==hold.TimeSeconds);
            inputs[at]=(hold.Direction,hold.TimeSeconds+error,0);
            if(release!="none")inputs.Add((hold.Direction,release=="early"?hold.TimeSeconds+error+.01:hold.TimeSeconds+hold.HoldSeconds-.1,1));
            inputs.Sort((a,b)=>a.time.CompareTo(b.time));Send(game,interaction,inputs);Verdict(game,interaction);
            double expected=(chart.Notes.Count-1+(release=="early"?0:error==0?1:.6))/chart.Notes.Count;
            Check(Math.Abs(interaction.Score-expected)<1e-9,kind+" "+release+" release with head error "+error+" scores "+expected+", got "+interaction.Score);
            Check(Math.Abs(game.Player("p0").LastRhythmScore-expected)<1e-9,"the player's stored score uses the same verdict");
            Check(kind=="Dj"?interaction.Phrase==1:interaction.Status=="Complete","the verdict settles this challenge or advances the DJ phrase");
        }
    }
    static void TrustDancesLoseTrustOnlyForAnEarlyDrop()
    {
        foreach(string release in new[]{"timely","none","early"})
        {
            var game=Floor();game.State.Npcs[0].Observers.Add(new ObserverState{PlayerId="p0",Suspicion=40});
            var interaction=Start(game,"Conversation");Check(interaction.TrustDance,"setup: a wary partner asks for a dance");
            RhythmChart chart=null;RhythmNote hold=null;int heads=0;
            for(int seed=0;seed<1000&&hold==null;seed++)
            {
                interaction.ChartSeed=seed;chart=RhythmChart.For(interaction);heads=(int)Math.Ceiling(.6*chart.Notes.Count);
                if(chart.Notes.Count>=14)hold=chart.Notes.FindIndex(note=>note.HoldSeconds>0)<heads?chart.Notes.Find(note=>note.HoldSeconds>0):null;
            }
            Check(hold!=null,"setup: a hold among the heads just sufficient for trust");
            var inputs=Heads(chart).GetRange(0,heads);
            if(release!="none")inputs.Add((hold.Direction,release=="early"?hold.TimeSeconds+.01:hold.TimeSeconds+hold.HoldSeconds-.1,1));
            inputs.Sort((a,b)=>a.time.CompareTo(b.time));Send(game,interaction,inputs);Verdict(game,interaction);
            Check(release=="early"?interaction.Status=="Complete"&&interaction.Turns.Count==0:interaction.Status=="Active"&&interaction.Turns.Count==1,
                release+" release "+(release=="early"?"loses":"retains")+" the credit needed to unlock the conversation");
        }
    }
    static void ArrivalOrderIsNeverReplacedByTimestampOrder()
    {
        var game=Floor();var interaction=Start(game);var chart=HoldChart(interaction);var hold=chart.Notes.Find(note=>note.HoldSeconds>0);
        var inputs=Heads(chart);int head=inputs.FindIndex(input=>input.lane==hold.Direction&&input.time==hold.TimeSeconds);
        inputs.Insert(head,(hold.Direction,hold.TimeSeconds+.01,1));
        // The release arrives before its head, even though its timestamp is later. The envelope allows this .01 s backdating.
        Send(game,interaction,inputs);Check(interaction.Inputs[head].Release&&!interaction.Inputs[head+1].Release,"the accepted log preserves arrival order");
        Verdict(game,interaction);Check(interaction.Score==1,"an unmatched release never becomes a drop by reordering the log");
    }
    static (double suspicion,int heads) LiveGroup(bool drop)
    {
        var game=new FestivalSimulation(7);var firstPlayer=game.AddPlayer("p0","First");var secondPlayer=game.AddPlayer("p1","Second");
        game.State.Phase="Playing";game.State.LevelIndex=3;game.State.SimulationSeconds=10;game.State.Npcs.Clear();
        firstPlayer.X=0;firstPlayer.Z=0;secondPlayer.X=2;secondPlayer.Z=0;
        game.State.Npcs.Add(new NpcState{Id="partner",X=0,Z=1.5f,Yaw=0,CanTalk=true});
        game.State.Npcs.Add(new NpcState{Id="partner1",X=2,Z=1.5f,Yaw=0,CanTalk=true});
        var watcher=new NpcState{Id="watcher",X=1,Z=6,Yaw=180};
        watcher.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=20});watcher.Observers.Add(new ObserverState{PlayerId="p1",Suspicion=20});game.State.Npcs.Add(watcher);
        var first=Start(game);game.State.SimulationSeconds+=2;
        var started=game.Execute("p1",new GameCommand{Id="release-group"+(sequence++),Kind="Dance",TargetId="partner1"});
        Check(started.Accepted,"setup: the friend starts two seconds later");var second=game.Interaction(secondPlayer.InteractionId);
        double cutoff=first.StartSeconds+first.DurationSeconds+.75-second.StartSeconds;RhythmChart chart=null;RhythmNote hold=null;int heads=0;
        for(int seed=0;seed<1000&&hold==null;seed++)
        {
            second.ChartSeed=seed;chart=RhythmChart.For(second);heads=chart.Notes.FindAll(note=>note.TimeSeconds<=cutoff).Count;
            if(heads<=8)hold=chart.Notes.Find(note=>note.HoldSeconds>0&&note.TimeSeconds+.01<=cutoff);
        }
        Check(hold!=null,"setup: an early hold in a partial chart whose verdict differs from an extra press");
        var inputs=new List<(InteractionState interaction,int lane,double time,int amount)>();
        foreach(var note in RhythmChart.For(first).Notes)inputs.Add((first,note.Direction,note.TimeSeconds,0));
        foreach(var note in chart.Notes)if(note.TimeSeconds<=cutoff)inputs.Add((second,note.Direction,note.TimeSeconds,0));
        if(drop)inputs.Add((second,hold.Direction,hold.TimeSeconds+.01,1));
        inputs.Sort((a,b)=>(a.interaction.StartSeconds+a.time).CompareTo(b.interaction.StartSeconds+b.time));
        foreach(var input in inputs)
        {
            game.State.SimulationSeconds=Math.Max(game.State.SimulationSeconds,input.interaction.StartSeconds+input.time);
            Check(Input(game,input.interaction,input.lane,input.time,input.amount).Accepted,"the group enters real timely inputs");
        }
        Verdict(game,first);
        Check(first.Status=="Complete"&&first.Score==1&&second.Status=="Active","the first dancer finishes perfectly while the friend's chart is still live");
        return (watcher.Observers[0].Suspicion,heads);
    }
    static void AStillDancingFriendsDropSetsTheGroupsWorstScore()
    {
        var held=LiveGroup(false);var dropped=LiveGroup(true);
        Check(held.heads==dropped.heads&&held.suspicion==5,"a live perfect friend preserves the -15 group verdict");
        double score=(dropped.heads-1.0)/dropped.heads;
        double expected=20+Math.Round(20-35*score,MidpointRounding.AwayFromZero);
        Check(dropped.suspicion==expected,"the group's live replay loses one head, not an extra-press penalty: expected "+expected+", got "+dropped.suspicion);
    }
    static void ReleasesShareTheFiniteTimingEnvelope()
    {
        foreach(int amount in new[]{0,1})
        {
            foreach(var invalid in new[]{(-1,2.0,2.0),(4,2.0,2.0),(0,-.01,2.0),(0,double.NaN,2.0),
                (0,double.PositiveInfinity,2.0),(0,double.NegativeInfinity,2.0),(0,2.151,2.0),(0,1.249,2.0)})
            {
                var game=Floor();var interaction=Start(game);game.State.SimulationSeconds=interaction.StartSeconds+invalid.Item3;
                Check(!Input(game,interaction,invalid.Item1,invalid.Item2,amount).Accepted,"the envelope rejects invalid lane/time for Amount "+amount+": "+invalid.Item1+", "+invalid.Item2);
                Check(interaction.Inputs.Count==0&&game.Player("p0").VisualDanceStepSequence==0,"a refused input changes no recorded input or step");
            }
            var after=Floor();var ended=Start(after);double time=ended.DurationSeconds+ended.GoodWindowSeconds+.001;
            after.State.SimulationSeconds=ended.StartSeconds+time;
            Check(!Input(after,ended,0,time,amount).Accepted,"one millisecond after the last chart window is refused for Amount "+amount);
            foreach(double boundary in new[]{1.25,2.15})
            {
                var game=Floor();var interaction=Start(game);game.State.SimulationSeconds=interaction.StartSeconds+2;
                Check(Input(game,interaction,0,boundary,amount).Accepted,"the existing .75 s late and .15 s future boundaries remain inclusive");
            }
        }
    }
    static void TheInputCapCountsReleasesAndDeduplicatesCommands()
    {
        var game=Floor();var interaction=Start(game);game.State.SimulationSeconds=interaction.StartSeconds+2;GameCommand first=null;
        for(int input=0;input<128;input++)
        {
            var command=new GameCommand{Id="release-cap"+(sequence++),Kind="Rhythm",Direction=0,TimeSeconds=2,Amount=input%2};if(first==null)first=command;
            Check(game.Execute("p0",command).Accepted,"every input through the 128th, releases included, is accepted");
        }
        Check(interaction.Inputs.Count==128&&interaction.Inputs.FindAll(input=>input.Release).Count==64&&game.Player("p0").VisualDanceStepSequence==64,"64 presses and 64 releases share one budget and only 64 steps");
        Check(game.Execute("p0",first).Accepted&&interaction.Inputs.Count==128&&game.Player("p0").VisualDanceStepSequence==64,"reliable command retries append and animate nothing twice, even at the cap");
        foreach(int amount in new[]{0,1})Check(!Input(game,interaction,0,2,amount).Accepted,"a new press or release after 128 inputs is refused");
        Check(interaction.Inputs.Count==128,"overflow changes no input log");
    }
    static void ConfiguredChartsFitIncludingTheirHoldReleases()
    {
        var maxima=new int[4];var largest=new InteractionState[4];int holds=0;
        // Dance/check/trust: up to eight beats; poi/DJ: twelve; the queued drum part: twenty-four. Count heads AND releases.
        for(int level=0;level<4;level++)foreach(int beats in new[]{8,12,24})foreach(double tempo in new[]{.4,.5})for(int seed=0;seed<200;seed++)
        {
            var interaction=new InteractionState{Kind="Dance",ChartDifficulty=level,ChartSeed=seed,NoteCount=beats,BeatSeconds=tempo};var chart=RhythmChart.For(interaction);
            int budget=chart.Notes.Count;foreach(var note in chart.Notes)if(note.HoldSeconds>0){budget++;holds++;}
            Check(budget<=128,"a configured chart's actual press/release envelope fits 128");
            if(budget>maxima[level]){maxima[level]=budget;largest[level]=interaction;}
        }
        Check(holds>0,"the budget sample includes actual hold releases");
        for(int level=0;level<4;level++)
        {
            var game=Floor();var interaction=Start(game);var sample=largest[level];
            interaction.ChartDifficulty=sample.ChartDifficulty;interaction.ChartSeed=sample.ChartSeed;interaction.NoteCount=sample.NoteCount;interaction.BeatSeconds=sample.BeatSeconds;
            var chart=RhythmChart.For(interaction);interaction.DurationSeconds=chart.DurationSeconds;var inputs=Heads(chart);
            foreach(var note in chart.Notes)if(note.HoldSeconds>0)inputs.Add((note.Direction,note.TimeSeconds+note.HoldSeconds-.1,1));
            inputs.Sort((a,b)=>a.time.CompareTo(b.time));Send(game,interaction,inputs);
            Check(interaction.Inputs.Count==maxima[level]&&game.Player("p0").VisualDanceStepSequence==chart.Notes.Count,"the largest sampled envelope uses the real input consumer, with press-only steps");
            Verdict(game,interaction);Check(interaction.Score==1,"the largest envelope scores every head and timely hold release");
        }
        Console.WriteLine("RhythmRelease maximum sampled input envelopes by level: "+string.Join(", ",maxima));
    }
    static void DebugTracingIsAbsentFromReleaseRules()
    {
        Check(typeof(FestivalSimulation).GetField("RhythmDiagnostic")==null,"the renderer-free release build contains no diagnostic field");
        Check(typeof(RhythmInput).GetField("Release")?.FieldType==typeof(bool),"the functional release field remains in release rules");
    }
    static void CheckDanceTapReleasesNeverCostCreditOrSteps()
    {
        var game=Floor();game.State.TripperId="p0";
        var vision=new VisionState{NpcId="partner",IsTrue=false};game.State.Visions.Add(vision);
        var interaction=Start(game,"ConfirmDance");var chart=RhythmChart.For(interaction);
        Check(chart.Notes.Count==4&&chart.Notes.TrueForAll(note=>note.HoldSeconds==0),"the real check command keeps its four tap notes");
        var inputs=Heads(chart);foreach(var note in chart.Notes)inputs.Add((note.Direction,note.TimeSeconds+.01,1));
        inputs.Sort((a,b)=>a.time.CompareTo(b.time));Send(game,interaction,inputs);Verdict(game,interaction);
        Check(interaction.Status=="Complete"&&interaction.Score==1&&game.Player("p0").LastRhythmScore==1,"letting go of check-dance taps never takes credit");
        Check(interaction.Inputs.Count==8&&game.Player("p0").VisualDanceStepSequence==4,"four tap releases are recorded without four extra steps");
        Check(vision.Confirmed,"the check still confirms the tripper's vision");
    }
}
