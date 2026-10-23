using System;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;

public static class DrumCircleTests
{
    static void Check(bool value,string message){if(!value)throw new Exception("DrumCircle: "+message);}
    static readonly System.Text.Json.JsonSerializerOptions Json=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    public static void Run()
    {
        var failures=new List<string>();foreach(var test in new Action[]{PrototypeRules,TheCommonStartGivesOnlyTheFrozenLivingRosterDistinctParts,AllBandsUseTheWorstFinalScoreAndRawNearbySuspicion,SilentDisconnectedAndWithdrawnPartsRemainInTheScore,GameTimeAdvancesWhileTheWorldAndDoseHold,ScoringClosesPartsBeforeResumeAndTheCapStillScores,HostSnapshotsContinueAndOldSnapshotsDefaultQuietly})
            try{test();}catch(Exception e){failures.Add(test.Method.Name+" -> "+e.Message);}
        Check(failures.Count==0,string.Join("\n",failures));
    }
    static void PrototypeRules()
    {
        var rules=typeof(FestivalSimulation).Assembly.GetType("Festival.Core.DrumCircle");
        Check(rules!=null,"deterministic drum chart and worst-score rules are available");
        var part=rules.GetMethod("Part");var payout=rules.GetMethod("ClockChange");
        Check(part!=null&&payout!=null,"bounded drum rules expose part and result");
        var signatures=new HashSet<string>();
        for(int player=0;player<8;player++)
        {
            var chart=(RhythmChart)part.Invoke(null,new object[]{123,player,.5});
            Check(chart.Notes.Count==24&&chart.Notes.All(n=>n.HoldSeconds==0),"every part has exactly 24 taps");
            Check(chart.Notes.Select((n,i)=>Math.Abs(n.TimeSeconds-(2+i*.5))<1e-8).All(x=>x),"all parts share start and .5 s spacing");
            string key=string.Join(",",chart.Notes.Select(n=>n.Direction));Check(signatures.Add(key),"eight participants have different parts");
            Check(key==string.Join(",",((RhythmChart)part.Invoke(null,new object[]{123,player,.5})).Notes.Select(n=>n.Direction)),"seeded part is repeatable");
        }
        foreach(var band in new[]{(.75,45),(.749,0),(.4,0),(.399,-30),(0.0,-30)})
        {
            var scores=new[]{1.0,band.Item1};int paid=(int)payout.Invoke(null,new object[]{scores});Check(paid==band.Item2,"worst "+band.Item1+" pays "+band.Item2+", got "+paid);
        }
        var silent=new RhythmJudge((RhythmChart)part.Invoke(null,new object[]{123,0,.5}));silent.Advance(20);
        Check(silent.Score==0&&(int)payout.Invoke(null,new object[]{new[]{1.0,silent.Score}})==-30,"a silent part sinks a perfect crew");
    }

    static FestivalSimulation Started(int count=2,bool watchers=false,int festival=0)
    {
        var s=new FestivalSimulation(123);for(int k=0;k<count;k++)s.AddPlayer("p"+k,"P"+k);
        if(watchers){s.AddPlayer("ghost","Watcher").Life="Spirit";s.AddPlayer("detained","Detained").Life="Detained";s.AddPlayer("gone","Gone").Connected=false;}
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.Phase="Playing";s.State.Npcs.Clear();s.State.Booth=new MinigameState{Kind="DrumCircle",Spot=1};
        var at=FestivalSimulation.BoothAt(s.State);foreach(var p in s.State.Players){p.X=at.X;p.Z=at.Z;}
        for(int k=0;k<35&&s.State.Phase!="Minigame";k++)s.Tick(.1);
        Check(s.State.Phase=="Minigame","actual ring starts the drum game");return s;
    }
    static InteractionState Part(FestivalSimulation s,string id)=>s.State.Interactions.Find(i=>i.PlayerId==id&&i.Kind=="DrumPart");
    static InteractionState RequiredPart(FestivalSimulation s,string id){var i=Part(s,id);Check(i!=null,"common start creates private DrumPart for "+id);return i;}
    static CommandResult Act(FestivalSimulation s,string id,string kind,int direction=0,double time=0)=>s.Execute(id,new GameCommand{Id="drum"+(sequence++),Kind=kind,Direction=direction,TimeSeconds=time});
    static double Accuracy(FestivalSimulation s,string id)
    {
        var b=System.Text.Json.JsonSerializer.SerializeToNode(s.State.Booth,Json);Check(b["DrumAccuracies"]!=null,"public scalar accuracy list is available");
        return b["DrumAccuracies"].AsArray().Single(x=>x["PlayerId"].GetValue<string>()==id)["Accuracy"].GetValue<double>();
    }
    static void AdvanceTo(FestivalSimulation s,double chartTime){var i=RequiredPart(s,"p0");double dt=i.StartSeconds+chartTime-s.State.SimulationSeconds;if(dt>0)s.Tick(dt);}
    static void PlayNotes(FestivalSimulation s,int worstHits,int from=0,int to=24)
    {
        var first=RhythmChart.For(RequiredPart(s,"p0"));for(int k=from;k<to;k++)
        {
            AdvanceTo(s,first.Notes[k].TimeSeconds);
            foreach(string id in s.State.Booth.PlayerIds)
            {
                var i=RequiredPart(s,id);var note=RhythmChart.For(i).Notes[k];if(id=="p1"&&k>=worstHits)continue;
                Check(Act(s,id,"Rhythm",note.Direction,note.TimeSeconds).Accepted,"production timestamped drum press is accepted");
            }
            s.Tick(.01);
        }
    }
    static void Finish(FestivalSimulation s){AdvanceTo(s,RequiredPart(s,"p0").DurationSeconds+.8);Check(s.State.Booth.ScoredAt>=0,"all parts end and the host scores the drum circle");}
    static void TheCommonStartGivesOnlyTheFrozenLivingRosterDistinctParts()
    {
        for(int count=1;count<=8;count++)foreach(int festival in new[]{0,1})
        {
            var s=Started(count,count==2,festival);Check(s.State.Booth.PlayerIds.Count==count,"only connected Alive players play");
            var keys=new HashSet<string>();double start=RequiredPart(s,"p0").StartSeconds;var first=RhythmChart.For(RequiredPart(s,"p0"));
            foreach(string id in s.State.Booth.PlayerIds)
            {
                var i=RequiredPart(s,id);var chart=RhythmChart.For(i);Check(i.StartSeconds==start&&chart.Notes.Select(n=>n.TimeSeconds).SequenceEqual(first.Notes.Select(n=>n.TimeSeconds)),"all parts share one start and grid");
                Check(i.ChartDifficulty==RhythmChart.QuarterNotes&&chart.Notes.Count==24&&chart.Notes.All(n=>n.HoldSeconds==0),"every actual part is 24 quarter-note taps");
                Check(keys.Add(string.Join(",",chart.Notes.Select(n=>n.Direction))),"actual roster parts are distinct");
                Check(!FestivalSimulation.Dancing(i)&&!FestivalSimulation.DancesVisibly(i.Kind),"drum parts never become a dance");
                Check(i.StageTrack==RhythmChart.StageTrackFor(festival,0)&&i.StageBeatStride==RhythmChart.NearestStageStride(StageBeats.For(i.StageTrack),RhythmChart.StageBeatAtWorld(StageBeats.For(i.StageTrack),i.StageFirstHeadWorldSeconds),.5),"parts use the frozen stage grid nearest half a second");
            }
            if(count==2)Check(Part(s,"ghost")==null&&Part(s,"detained")==null&&Part(s,"gone")==null,"spirits, detained and disconnected players watch");
        }
        Check(FestivalSimulation.MinigameKinds.Contains("DrumCircle"),"the bag deals the drum circle now its real consumers have landed");
    }
    static void AllBandsUseTheWorstFinalScoreAndRawNearbySuspicion()
    {
        foreach(var band in new[]{(18,45,-20),(10,0,0),(9,-30,15),(0,-30,15)})
        {
            var s=Started(3);var at=FestivalSimulation.BoothAt(s.State);foreach(var p in s.State.Players)p.Inventory.Add(new ItemStack{ItemId="little_spoon",Count=1});
            foreach(var n in new[]{new NpcState{Id="near",Kind="Wook",X=at.X,Z=at.Z},new NpcState{Id="edge",Kind="Wook",X=at.X+25,Z=at.Z},new NpcState{Id="far",Kind="Wook",X=at.X+26,Z=at.Z},new NpcState{Id="cop",Kind="Cop",X=at.X,Z=at.Z}})
            {foreach(string id in s.State.Booth.PlayerIds)n.Observers.Add(new ObserverState{PlayerId=id,Suspicion=id=="p0"?95:id=="p1"?10:50});s.State.Npcs.Add(n);}
            double duration=s.State.DurationSeconds;PlayNotes(s,band.Item1);Finish(s);
            Check(s.State.Booth.ClockChange==band.Item2&&s.State.DurationSeconds==duration+band.Item2,"worst "+band.Item1+"/24 pays approved time band");
            foreach(var n in s.State.Npcs)foreach(var o in n.Observers)
            {double before=o.PlayerId=="p0"?95:o.PlayerId=="p1"?10:50;double expected=n.Id=="near"||n.Id=="edge"?Math.Max(0,Math.Min(100,before+band.Item3)):before;Check(Math.Abs(o.Suspicion-expected)<1e-8,"raw once-per-participant clamped suspicion at "+n.Id+" for "+o.PlayerId);}
            Check(Math.Abs(Accuracy(s,"p1")-band.Item1/24.0)<1e-8&&Accuracy(s,"p0")==1,"public score includes the worst drummer");
            double suspicion=s.State.Npcs[0].Observers[0].Suspicion;s.Tick(.2);Check(s.State.Npcs[0].Observers[0].Suspicion==suspicion,"banner cannot pay suspicion again");
        }
    }
    static void SilentDisconnectedAndWithdrawnPartsRemainInTheScore()
    {
        foreach(string withdrawal in new[]{"silent","disconnect","cancel","life"})
        {
            var s=Started();var i=RequiredPart(s,"p1");if(withdrawal=="disconnect")s.Disconnect("p1");
            if(withdrawal=="cancel")
            {
                Check(!s.Execute("p1",new GameCommand{Id="drum"+(sequence++),Kind="Cancel",TargetId="ended_interaction"}).Accepted&&i.Status=="Active","a Cancel naming an interaction p1 no longer has leaves the part playing");
                Check(Act(s,"p1","Cancel").Accepted,"participant can withdraw while world is paused");
            }
            if(withdrawal=="life")s.Player("p1").Life="Spirit";
            var chart=RhythmChart.For(RequiredPart(s,"p0"));foreach(var note in chart.Notes){AdvanceTo(s,note.TimeSeconds);Check(Act(s,"p0","Rhythm",note.Direction,note.TimeSeconds).Accepted,"remaining drummer plays");}
            Finish(s);Check(s.State.Booth.PlayerIds.Count==2&&Accuracy(s,"p1")==0&&s.State.Booth.ClockChange==-30,"silent/disconnected/withdrawn frozen participant sinks crew: "+withdrawal);
            Check(i.Status!="Active"&&s.Player("p1").InteractionId=="","withdrawn part is closed and cannot survive resume");
        }
    }
    static void GameTimeAdvancesWhileTheWorldAndDoseHold()
    {
        var s=Started();var p=s.Player("p0");p.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,RemainingSeconds=100});var i=RequiredPart(s,"p0");
        double world=FestivalSimulation.WorldSeconds(s.State),elapsed=s.State.ElapsedSeconds;float x=p.X,z=p.Z;long steps=p.VisualDanceStepSequence;
        Check(!s.TryMove("p0",x+1,z,90,.1),"drum participant is rooted by stopped-world lifecycle");
        var note=RhythmChart.For(i).Notes[0];AdvanceTo(s,note.TimeSeconds);Check(Math.Abs(FestivalSimulation.WorldSeconds(s.State)-world)<1e-8&&s.State.ElapsedSeconds==elapsed&&p.Effects[0].RemainingSeconds==100,"only game time advances");
        Check(!Act(s,"p0","Rhythm",note.Direction,note.TimeSeconds+.16).Accepted,"future drum timestamp still uses existing envelope");
        Check(Act(s,"p0","Rhythm",note.Direction,note.TimeSeconds).Accepted,"raw chart input uses advancing game time");s.Tick(.2);
        Check(Accuracy(s,"p0")==1&&Accuracy(s,"p1")==0,"live accuracy judges notes due so far, rather than dividing early hits by the entire chart");
        s.Tick(RhythmChart.For(i).Notes[1].TimeSeconds-note.TimeSeconds+.85);Check(Accuracy(s,"p0")>0&&Accuracy(s,"p0")<1,"advancing the judge makes later silence lower live accuracy");
        Check(p.VisualDanceStepSequence==steps&&p.X==x&&p.Z==z,"drum inputs never produce dance steps or spacing");
        Check(!Act(s,"p0","Dance").Accepted,"world actions remain paused");
    }
    static void ScoringClosesPartsBeforeResumeAndTheCapStillScores()
    {
        var s=Started();s.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,RemainingSeconds=100});PlayNotes(s,24);Finish(s);
        Check(s.Player("p0").Effects[0].RemainingSeconds==145,"winning extends dose through common score helper");
        foreach(var i in s.State.Interactions.Where(i=>i.Kind=="DrumPart"))Check(i.Status=="Complete"&&s.Player(i.PlayerId).InteractionId=="","every part closes at score before banner/shift");
        Check(!Act(s,"p0","Rhythm",0,0).Accepted,"score rejects later drum input");double world=FestivalSimulation.WorldSeconds(s.State);s.Tick(3.5);Check(s.State.Phase=="Minigame","four-second banner holds");s.Tick(.5);Check(s.State.Phase=="Playing"&&s.State.Booth.Spent,"resume spends booth");Check(Math.Abs(FestivalSimulation.WorldSeconds(s.State)-world)<.11,"resume keeps frozen world phase");
        var capped=Started();foreach(var i in capped.State.Interactions)i.DurationSeconds=1000;capped.Tick(60);capped.Tick(30.1);Check(capped.State.Booth.ScoredAt>=90&&capped.State.Booth.ClockChange==-30,"90-second cap judges missed drum parts");Check(capped.State.Interactions.All(i=>i.Status!="Active"),"cap closes all drum parts");
        var floor=Started();floor.State.ElapsedSeconds=floor.State.DurationSeconds-40;Finish(floor);Check(floor.State.Booth.ClockChange==-25,"drum loss preserves common fifteen-second floor");
    }
    static void HostSnapshotsContinueAndOldSnapshotsDefaultQuietly()
    {
        var s=Started();PlayNotes(s,24,0,12);var restored=new FestivalSimulation();restored.Restore(System.Text.Json.JsonSerializer.Deserialize<RoundState>(System.Text.Json.JsonSerializer.Serialize(s.State,Json),Json));
        PlayNotes(s,24,12);PlayNotes(restored,24,12);Finish(s);Finish(restored);Check(restored.State.Booth.ClockChange==45&&Accuracy(restored,"p0")==Accuracy(s,"p0"),"restored host retains chart and input log without reroll");
        var old=System.Text.Json.JsonSerializer.Deserialize<MinigameState>("{}",Json);var node=System.Text.Json.JsonSerializer.SerializeToNode(old,Json);Check(node["DrumAccuracies"]!=null&&node["DrumAccuracies"].AsArray().Count==0&&node["DrumWorstScore"].GetValue<double>()==-1,"old snapshots retain quiet drum defaults");
    }
}
