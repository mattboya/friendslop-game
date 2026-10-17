using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

public static class LightShowTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static void Check(bool value,string message){if(!value)throw new Exception("LightShow: "+message);}
    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new System.Collections.Generic.List<string>();
        foreach(var test in new Action[]{TheCommonStartPersistsTheRealTwentySecondScript,ActiveDoseIsCapturedWithExactlyThatManyStablePrivateFakes,EachViewerDrawsThreeCategoriesAndFourDistinctRealAnswers,FutureDoseDoesNotCountAsActive,
            PublicCopiesDetachTheRealScriptAndWithholdEveryPrivateDraw,HostSnapshotsRetainRecordsAndOldSnapshotsDefaultQuietly,RealAnswersNeverCountPrivateFakes,CorrectEarnsOnePlusDoseAndTheCrewTotalPaysFourSecondsAPoint,
            QuestionsWaitForTheShowThenTakeThreeEightSecondTurns,EveryConnectedAnswerOpensTheNextQuestionAtOnce,AnswersRejectWrongPlayersSlotsChoicesAndReplays,
            DisconnectedPlayersCountAsNotSureWithoutLeavingTheRoster,TheCrewsNetPointsPayThroughTheMinigameClock,ASnapshotMidQuestionKeepsEveryAnswer,
            EachViewerSeesOnlyTheirOwnOpenQuestionsUntilTheReveal,RecipientViewsAreDetachedFromTheHostAndEachOther})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" Light Show test(s) failed:\n"+string.Join("\n",failures));
    }
    static int sequence;
    static CommandResult Answer(FestivalSimulation s,string id,int slot,int choice)=>s.Execute(id,new GameCommand{Id="answer"+(sequence++),Kind=FestivalSimulation.MinigameKind,ItemId="Answer",Amount=choice,Direction=slot});
    static JsonObject Record(FestivalSimulation s,string id)=>Show(s)["Players"].AsArray().Single(p=>p["PlayerId"].GetValue<string>()==id).AsObject();
    static int[] Answers(FestivalSimulation s,string id){var r=Record(s,id);Check(r.ContainsKey("Answers"),"each captured record persists its answers");return r["Answers"].AsArray().Select(a=>a.GetValue<int>()).ToArray();}
    static int Slot(FestivalSimulation s){var show=Show(s);Check(show.ContainsKey("Slot"),"the show persists its open question slot");return show["Slot"].GetValue<int>();}
    static double SlotAt(FestivalSimulation s){var show=Show(s);Check(show.ContainsKey("SlotAt"),"the show persists when its question slot opened");return show["SlotAt"].GetValue<double>();}
    static int Correct(FestivalSimulation s,string id,int slot)=>Record(s,id)["Questions"][slot]["CorrectIndex"].GetValue<int>();
    static bool Near(double a,double b)=>Math.Abs(a-b)<1e-4;
    static bool Scored(FestivalSimulation s)=>s.State.Booth.ScoredAt>=0;
    static LightShowState Visible(FestivalSimulation s,string viewer)
    {
        var method=typeof(FestivalSimulation).GetMethod("VisibleLightShow",BindingFlags.Static|BindingFlags.Public);
        Check(method!=null,"production per-viewer light-show view is missing");return (LightShowState)method.Invoke(null,new object[]{s.State,viewer});
    }
    static JsonObject Seen(FestivalSimulation s,string viewer)=>JsonSerializer.SerializeToNode(Visible(s,viewer),Json).AsObject();
    static string[] Ids(JsonObject view)=>view["Players"].AsArray().Select(p=>p["PlayerId"].GetValue<string>()).ToArray();
    static FestivalSimulation Started(int seed=123,int count=2,Action<FestivalSimulation> before=null)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<count;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.SpinSeed=seed;s.State.Phase="Playing";s.State.Npcs.Clear();s.State.Booth=new MinigameState{Kind="LightShow",Spot=1};
        var at=FestivalSimulation.BoothAt(s.State);foreach(var p in s.State.Players){p.X=at.X;p.Z=at.Z;}
        before?.Invoke(s);for(int k=0;k<35&&s.State.Phase!="Minigame";k++)s.Tick(.1);
        Check(s.State.Phase=="Minigame","actual ring starts the light show");return s;
    }
    static JsonObject Show(FestivalSimulation s)
    {
        var b=JsonSerializer.SerializeToNode(s.State.Booth,Json).AsObject();
        Check(b.ContainsKey("LightShow")&&b["LightShow"]!=null,"common start must persist the real light-show script");return b["LightShow"].AsObject();
    }
    static void CorrectEarnsOnePlusDoseAndTheCrewTotalPaysFourSecondsAPoint()
    {
        var points=typeof(FestivalSimulation).GetMethod("LightShowPoints",BindingFlags.Static|BindingFlags.Public);
        var payout=typeof(FestivalSimulation).GetMethod("LightShowClockChange",BindingFlags.Static|BindingFlags.Public);
        Check(points!=null&&payout!=null,"production Light Show scoring and payout helpers are missing");
        int Points(int dose,int answer,int correct)=>(int)points.Invoke(null,new object[]{dose,answer,correct});
        int Payout(int total)=>(int)payout.Invoke(null,new object[]{total});
        for(int dose=0;dose<=4;dose++)
        {
            Check(Points(dose,2,2)==1+dose,"correct earns 1+d at dose "+dose);
            Check(Points(dose,1,2)==-1,"wrong costs one at dose "+dose);
            Check(Points(dose,-1,2)==0,"not sure costs nothing at dose "+dose);
        }
        Check(Payout(12)==48&&Payout(15)==60&&Payout(16)==60,"net points x4, upper clamp 60");
        Check(Payout(-10)==-40&&Payout(-11)==-40&&Payout(0)==0,"lower clamp -40 and unsure 0");
    }
    static void QuestionsWaitForTheShowThenTakeThreeEightSecondTurns()
    {
        var s=Started(count:2);
        Check(!Answer(s,"p0",0,0).Accepted,"no answer goes in while the show plays");s.Tick(19.9);Check(!Answer(s,"p0",0,0).Accepted,"nor just before it ends");
        Check(Answers(s,"p0").Length==0&&Slot(s)==0&&Near(SlotAt(s),20),"the first question opens as the twenty-second show ends");
        s.Tick(.15);Check(Slot(s)==0&&Answers(s,"p0").Length==0,"the first question waits for answers");
        s.Tick(7.8);Check(Slot(s)==0,"for eight seconds");
        s.Tick(.2);Check(Slot(s)==1&&Near(SlotAt(s),28)&&Answers(s,"p0").SequenceEqual(new[]{-1})&&Answers(s,"p1").SequenceEqual(new[]{-1}),"then closes, every missing answer Not sure");
        s.Tick(15.9);Check(Slot(s)==2&&Near(SlotAt(s),36)&&!Scored(s),"the second closes after its own eight seconds");
        s.Tick(.1);Check(Slot(s)==3&&Scored(s)&&s.State.Booth.ClockChange==0,"the third closes at 44 s and the game scores the crew's nothing");
        Check(Answers(s,"p0").SequenceEqual(new[]{-1,-1,-1})&&Answers(s,"p1").SequenceEqual(new[]{-1,-1,-1}),"every question is answered Not sure");
        s.Tick(FestivalSimulation.MinigameBannerSeconds-.2);Check(s.State.Phase==FestivalSimulation.MinigamePhase,"the existing banner shows the result");
        s.Tick(.3);Check(s.State.Phase=="Playing"&&s.State.Booth.Spent,"then play resumes and the booth is spent");
        var away=Started(count:2);away.Tick(60);
        Check(Slot(away)==3&&Scored(away)&&Answers(away,"p1").SequenceEqual(new[]{-1,-1,-1})&&away.State.Booth.ClockChange==0,"a crew that never answers is scored once, well inside the cap");
    }
    static void EveryConnectedAnswerOpensTheNextQuestionAtOnce()
    {
        var s=Started(count:3);s.Tick(21);
        Check(Answer(s,"p0",0,0).Accepted&&Slot(s)==0,"one answer waits for the rest of the crew");
        Check(Answer(s,"p1",0,-1).Accepted&&Slot(s)==0,"Not sure is an answer that still waits for the last player");
        Check(Answer(s,"p2",0,3).Accepted&&Slot(s)==1&&Near(SlotAt(s),21),"the last answer opens the next question at once");
        Check(Answers(s,"p0").SequenceEqual(new[]{0})&&Answers(s,"p1").SequenceEqual(new[]{-1})&&Answers(s,"p2").SequenceEqual(new[]{3}),"each answer is kept as given");
        s.Tick(7.9);Check(Slot(s)==1,"the next question has its own eight seconds");s.Tick(.2);Check(Slot(s)==2&&Near(SlotAt(s),29),"and closes at its deadline");
        foreach(string id in new[]{"p0","p1","p2"})Check(Answer(s,id,2,1).Accepted,"the crew answers the third");
        Check(Slot(s)==3&&Scored(s),"the last question's final answer scores the game");
    }
    static void AnswersRejectWrongPlayersSlotsChoicesAndReplays()
    {
        var s=Started(count:3,before:x=>x.Player("p2").Life="Spirit");s.Tick(21);string untouched=Show(s).ToJsonString();
        Check(!Answer(s,"p2",0,0).Accepted,"a spectator cannot answer");
        Check(!Answer(s,"p0",1,0).Accepted&&!Answer(s,"p0",-1,0).Accepted&&!Answer(s,"p0",3,0).Accepted,"only the open question takes an answer");
        Check(!Answer(s,"p0",0,4).Accepted&&!Answer(s,"p0",0,-2).Accepted,"only the four answers or Not sure");
        Check(!s.Execute("p0",new GameCommand{Id="drink-light",Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.DrinkCup,Amount=0,Direction=0}).Accepted,"another game's move is no answer");
        Check(Show(s).ToJsonString()==untouched,"rejected answers change nothing");
        var first=new GameCommand{Id="replayed",Kind=FestivalSimulation.MinigameKind,ItemId="Answer",Amount=2,Direction=0};
        Check(s.Execute("p0",first).Accepted&&s.Execute("p0",first).Accepted&&Answers(s,"p0").SequenceEqual(new[]{2}),"a resent command keeps its one answer");
        var changed=Answer(s,"p0",0,1);Check(!changed.Accepted&&Answers(s,"p0").SequenceEqual(new[]{2}),"a second answer to the same question cannot overwrite the first");
        Check(Answer(s,"p1",0,0).Accepted&&Slot(s)==1,"the spectator is never waited for");
        Check(!Answer(s,"p1",0,3).Accepted&&Answers(s,"p1").SequenceEqual(new[]{0}),"a late answer to a closed question changes nothing");
        foreach(var slot in new[]{1,2})foreach(string id in new[]{"p0","p1"})Check(Answer(s,id,slot,-1).Accepted,"setup: the crew answers");
        Check(Scored(s)&&!Answer(s,"p0",2,0).Accepted,"nothing goes in once the game is scored");
        s.Tick(FestivalSimulation.MinigameBannerSeconds+.1);Check(s.State.Phase=="Playing"&&!Answer(s,"p0",0,0).Accepted,"nor after play resumes");
    }
    static void DisconnectedPlayersCountAsNotSureWithoutLeavingTheRoster()
    {
        var s=Started(count:3);s.Tick(21);
        Check(Answer(s,"p0",0,1).Accepted&&Answer(s,"p1",0,1).Accepted&&Slot(s)==0,"setup: two answers wait for the third");
        s.Disconnect("p2");s.Tick(.1);
        Check(Slot(s)==1&&Answers(s,"p2").SequenceEqual(new[]{-1}),"a disconnect stops the wait and counts as Not sure");
        Check(s.State.Booth.PlayerIds.Count==3&&Show(s)["Players"].AsArray().Count==3,"the captured roster keeps the disconnected player");
        s.AddPlayer("p2","P2");Check(Answer(s,"p0",1,0).Accepted&&Answer(s,"p1",1,0).Accepted&&Slot(s)==1,"a returning player is waited for again");
        Check(Answer(s,"p2",1,0).Accepted&&Slot(s)==2,"and answers the open question");
        s.Disconnect("p1");s.Disconnect("p2");Check(Answer(s,"p0",2,0).Accepted&&Slot(s)==3&&Scored(s),"the only connected answer closes the last question");
        Check(Answers(s,"p1").SequenceEqual(new[]{1,0,-1})&&Answers(s,"p2").SequenceEqual(new[]{-1,0,-1}),"every captured player keeps one answer per question");
    }
    static void TheCrewsNetPointsPayThroughTheMinigameClock()
    {
        var dose=new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,RemainingSeconds=100};
        var s=Started(count:2,before:x=>x.Player("p0").Effects.Add(dose));s.Tick(21);double duration=s.State.DurationSeconds,remaining=dose.RemainingSeconds;
        for(int slot=0;slot<3;slot++){Check(Answer(s,"p0",slot,Correct(s,"p0",slot)).Accepted,"setup: the dosed player is right");Check(Answer(s,"p1",slot,(Correct(s,"p1",slot)+1)%4).Accepted,"setup: the sober player is wrong");}
        Check(Scored(s)&&s.State.Booth.ClockChange==48,"3 x (1+4) - 3 = 12 points pay +48 s, got "+s.State.Booth.ClockChange);
        Check(Near(s.State.DurationSeconds,duration+48)&&Near(dose.RemainingSeconds,remaining+48),"a gain lengthens the level and the dose with it");
        var top=Started(count:4,before:x=>{foreach(var p in x.State.Players)p.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=100});});top.Tick(21);
        for(int slot=0;slot<3;slot++)foreach(var p in top.State.Players)Answer(top,p.Id,slot,Correct(top,p.Id,slot));
        Check(Scored(top)&&top.State.Booth.ClockChange==60,"36 points clamp at +60 s, got "+top.State.Booth.ClockChange);
        var loss=Started(count:4);loss.State.ElapsedSeconds=loss.State.DurationSeconds-100;loss.Tick(21);
        for(int slot=0;slot<3;slot++)foreach(var p in loss.State.Players)Answer(loss,p.Id,slot,(Correct(loss,p.Id,slot)+1)%4);
        Check(Scored(loss)&&loss.State.Booth.ClockChange==-40,"twelve wrong answers clamp at -40 s, got "+loss.State.Booth.ClockChange);
        var floor=Started(count:2);floor.State.ElapsedSeconds=floor.State.DurationSeconds-30;floor.Tick(21);
        for(int slot=0;slot<3;slot++)foreach(string id in new[]{"p0","p1"})Answer(floor,id,slot,(Correct(floor,id,slot)+1)%4);
        Check(Scored(floor)&&floor.State.Booth.ClockChange==-15,"a 24 s loss keeps the existing 15 s floor, got "+floor.State.Booth.ClockChange);
    }
    static void ASnapshotMidQuestionKeepsEveryAnswer()
    {
        var s=Started(count:2);s.Tick(21);Check(Answer(s,"p0",0,Correct(s,"p0",0)).Accepted,"setup: one answer is in");
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        Check(Show(restored).ToJsonString()==Show(s).ToJsonString()&&Slot(restored)==0&&Answers(restored,"p0").SequenceEqual(Answers(s,"p0")),"restore keeps the open question and its answers");
        Check(!Answer(restored,"p0",0,0).Accepted,"a restored answer still cannot be replaced");
        foreach(var game in new[]{s,restored}){Check(Answer(game,"p1",0,-1).Accepted,"setup: the second answer");game.Tick(17);}
        Check(Scored(s)&&Scored(restored)&&s.State.Booth.ClockChange==restored.State.Booth.ClockChange&&Show(s).ToJsonString()==Show(restored).ToJsonString(),"both games finish the same way");
        var legacy=JsonSerializer.Deserialize<LightShowState>("{\"Players\":[{}]}",Json);var old=JsonSerializer.SerializeToNode(legacy,Json);
        Check(old["Slot"]?.GetValue<int>()==0&&Math.Abs((old["SlotAt"]?.GetValue<double>()??-1)-20)<1e-6&&old["Players"][0]["Answers"]?.AsArray().Count==0,"older records open the first question as the show ends, with no answers");
    }
    static void EachViewerSeesOnlyTheirOwnOpenQuestionsUntilTheReveal()
    {
        var s=Started(count:3,before:x=>{x.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=3,RemainingSeconds=100});x.Player("p2").Life="Spirit";});s.Tick(5);
        foreach(string viewer in new[]{"p0","p1"})
        {
            var view=Seen(s,viewer);var own=view["Players"].AsArray().Count==1?view["Players"][0]:null;
            Check(Ids(view).SequenceEqual(new[]{viewer}),"during the show "+viewer+" receives their own record alone");
            Check(own["Questions"].AsArray().Count==0&&own["Answers"].AsArray().Count==0,"no question shows before the show ends");
            Check(own["Fakes"].ToJsonString()==Record(s,viewer)["Fakes"].ToJsonString()&&own["Dose"].GetValue<int>()==Record(s,viewer)["Dose"].GetValue<int>(),"the viewer's own dose and fakes");
            Check(view["Events"].ToJsonString()==Show(s)["Events"].ToJsonString()&&view["Slot"].GetValue<int>()==0&&Math.Abs(view["SlotAt"].GetValue<double>()-20)<1e-6,"the public script and question timing");
        }
        Check(Seen(s,"p0")["Players"][0]["Fakes"].AsArray().Count==3,"the dosed viewer sees exactly their three fakes");
        foreach(string spectator in new[]{"p2",""})Check(Seen(s,spectator)["Players"].AsArray().Count==0,"a spectator receives no private record before the reveal");
        s.Tick(16);var key=Record(s,"p0")["Questions"][0];var first=Seen(s,"p0")["Players"][0]["Questions"].AsArray();
        Check(first.Count==1&&first[0]["CorrectIndex"].GetValue<int>()==-1&&first[0]["Prompt"].GetValue<string>()==key["Prompt"].GetValue<string>()&&first[0]["Choices"].ToJsonString()==key["Choices"].ToJsonString(),"the open question arrives without its answer key");
        Check(Answer(s,"p0",0,2).Accepted&&Seen(s,"p0")["Players"][0]["Answers"].ToJsonString()=="[2]","the viewer sees their own answer");
        var peer=Seen(s,"p1");Check(Ids(peer).SequenceEqual(new[]{"p1"})&&peer["Players"][0]["Answers"].AsArray().Count==0,"a peer never learns another player's question or answer");
        Check(Answer(s,"p1",0,-1).Accepted,"setup: the slot closes");var second=Seen(s,"p1")["Players"][0]["Questions"].AsArray();
        Check(second.Count==2&&second.All(q=>q["CorrectIndex"].GetValue<int>()==-1),"each question appears as it opens, still without its key");
        foreach(var slot in new[]{1,2})foreach(string id in new[]{"p0","p1"})Check(Answer(s,id,slot,slot-1).Accepted,"setup: the crew answers");
        Check(Scored(s),"setup: the game is scored");
        foreach(string viewer in new[]{"p0","p1","p2",""})
        {
            var view=Seen(s,viewer);Check(Ids(view).SequenceEqual(new[]{"p0","p1"}),"the reveal shows every captured player to "+(viewer==""?"nobody in particular":viewer)+" at once");
            foreach(var record in view["Players"].AsArray())
            {
                string id=record["PlayerId"].GetValue<string>();var host=Record(s,id);
                Check(record["Questions"].ToJsonString()==host["Questions"].ToJsonString()&&record["Answers"].ToJsonString()==host["Answers"].ToJsonString()&&record["Dose"].GetValue<int>()==host["Dose"].GetValue<int>(),"revealed answers come with their questions and keys");
                Check(record["Fakes"].ToJsonString()==(id==viewer?host["Fakes"].ToJsonString():"[]"),"another player's fakes stay theirs after the reveal");
            }
        }
    }
    static void RecipientViewsAreDetachedFromTheHostAndEachOther()
    {
        var s=Started(count:2,before:x=>x.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=100}));s.Tick(21);
        Check(Answer(s,"p0",0,1).Accepted&&Answer(s,"p1",0,1).Accepted&&Slot(s)==1,"setup: the second question is open");
        var booth=FestivalSimulation.VisibleBooth(s.State,"p0").LightShow;var hostShow=s.State.Booth.LightShow;
        Check(booth.Players.Count==0&&booth.Slot==hostShow.Slot&&booth.SlotAt==hostShow.SlotAt,"the public booth copy carries the question timing and no private record");
        string host=Show(s).ToJsonString();var one=Visible(s,"p0");string other=JsonSerializer.Serialize(Visible(s,"p1"),Json);
        var mine=one.Players[0];mine.Answers[0]=3;mine.Answers.Add(0);mine.Fakes[0].Event.Colour="Changed";mine.Fakes[0].Shadow=LightShowShadowTell.Goblin;mine.Fakes.Clear();
        mine.Questions[0].Choices[0]="Changed";mine.Questions[0].CorrectIndex=0;mine.Questions.Clear();one.Events[0].Colour="Changed";one.Events.Clear();one.Slot=3;one.Players.Clear();
        Check(Show(s).ToJsonString()==host,"recipient edits cannot rewrite the host's records");
        Check(JsonSerializer.Serialize(Visible(s,"p1"),Json)==other&&Seen(s,"p0")["Players"][0]["Answers"].ToJsonString()=="[1]","or another recipient's view");
    }
    static void PublicCopiesDetachTheRealScriptAndWithholdEveryPrivateDraw()
    {
        var s=Started(count:2,before:x=>x.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,RemainingSeconds=100}));
        string original=Show(s).ToJsonString();var owner=FestivalSimulation.VisibleBooth(s.State,"p0");var peer=FestivalSimulation.VisibleBooth(s.State,"p1");var spectator=FestivalSimulation.VisibleBooth(s.State,"");
        Check(owner.LightShow.Players.Count==0&&peer.LightShow.Players.Count==0&&spectator.LightShow.Players.Count==0,"the public booth copy sends no private fake, question, dose or answer records");
        Check(owner.LightShow.Events.Count==s.State.Booth.LightShow.Events.Count,"public real script is available");
        owner.LightShow.Events[0].Colour="Changed";owner.LightShow.Events.Clear();
        Check(Show(s).ToJsonString()==original&&peer.LightShow.Events.Count>0&&peer.LightShow.Events[0].Colour!="Changed","recipient mutations cannot alter host or another recipient's real script");
    }
    static void HostSnapshotsRetainRecordsAndOldSnapshotsDefaultQuietly()
    {
        var s=Started(count:4,before:x=>x.Player("p2").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=3,RemainingSeconds=100}));s.Tick(8);
        string records=Show(s).ToJsonString();var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        Check(Show(restored).ToJsonString()==records&&restored.State.Booth.Seconds==s.State.Booth.Seconds,"restore retains the exact real script, private draw and game time");
        restored.State.SpinSeed=999;restored.State.DealSeed=999;restored.Tick(5);Check(Show(restored).ToJsonString()==records,"restored play never regenerates from current seeds");
        var legacy=JsonSerializer.Deserialize<MinigameState>("{}",Json);Check(legacy.LightShow!=null&&legacy.LightShow.Events.Count==0&&legacy.LightShow.Players.Count==0,"old booth snapshots default to empty light-show records");
        var old=JsonSerializer.SerializeToNode(s.State,Json);old["Booth"].AsObject().Remove("LightShow");var prior=new FestivalSimulation();prior.Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),Json));
        Check(prior.State.Booth.LightShow.Events.Count==0&&prior.State.Booth.LightShow.Players.Count==0,"older round restores without a light-show field");
    }
    static void RealAnswersNeverCountPrivateFakes()
    {
        var real=new System.Collections.Generic.List<LightShowEvent>{
            new LightShowEvent{Kind=LightShowEventKind.Passer,StartsAt=1,EndsAt=5,Prop="Fan"},new LightShowEvent{Kind=LightShowEventKind.Passer,StartsAt=3,EndsAt=7},
            new LightShowEvent{Kind=LightShowEventKind.Sweep,StartsAt=6,EndsAt=8,Colour="Red"},new LightShowEvent{Kind=LightShowEventKind.Sweep,StartsAt=9,EndsAt=11,Colour="Green"},
            new LightShowEvent{Kind=LightShowEventKind.Sign,StartsAt=2,EndsAt=4,Shape="Star"},new LightShowEvent{Kind=LightShowEventKind.Sign,StartsAt=8,EndsAt=10,Shape="Circle"},
            new LightShowEvent{Kind=LightShowEventKind.Glover,StartsAt=0,EndsAt=5,Colour="Purple"},new LightShowEvent{Kind=LightShowEventKind.Glover,StartsAt=15,EndsAt=20,Colour="Blue"}};
        real.Reverse();var show=new LightShowState{Events=real};show.Players.Add(new LightShowPlayer{PlayerId="p",Dose=4});
        foreach(var e in new[]{new LightShowEvent{Kind=LightShowEventKind.Passer,Prop="Umbrella"},new LightShowEvent{Kind=LightShowEventKind.Sweep,Colour="Yellow"},new LightShowEvent{Kind=LightShowEventKind.Sign,Shape="Heart"},new LightShowEvent{Kind=LightShowEventKind.Glover,StartsAt=19,Colour="Orange"}})show.Players[0].Fakes.Add(new LightShowFake{Event=e});
        foreach(var expected in new[]{("Passers","2"),("Sweep","Red"),("Sign","Star"),("Prop","Fan"),("FinalLights","Blue")})
            Check(LightShowQuestions.Answer(show.Events,expected.Item1)==expected.Item2,"worked real script answer ignores private fakes and list order: "+expected.Item1);
    }
    static void FutureDoseDoesNotCountAsActive()
    {
        var s=Started(count:1,before:x=>x.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,StartSeconds=50,RemainingSeconds=100}));
        Check(s.State.Booth.LightShow.Players[0].Dose==0&&s.State.Booth.LightShow.Players[0].Fakes.Count==0,"a dose whose start is still in the future is not active");
    }
    static void EachViewerDrawsThreeCategoriesAndFourDistinctRealAnswers()
    {
        var categories=new System.Collections.Generic.HashSet<string>();var patterns=new System.Collections.Generic.HashSet<string>();
        for(int seed=1;seed<=40;seed++)
        {
            var s=Started(seed,8);var show=Show(s);var players=show["Players"].AsArray();
            foreach(var player in players)
            {
                Check(player.AsObject().ContainsKey("Questions"),"each viewer receives a persisted private question draw");
                var questions=player["Questions"].AsArray();Check(questions.Count==3&&questions.Select(q=>q["Category"].GetValue<string>()).Distinct().Count()==3,"three different categories per viewer");
                patterns.Add(string.Join(",",questions.Select(q=>q["Category"].GetValue<string>())));
                foreach(var q in questions)
                {
                    string category=q["Category"].GetValue<string>();categories.Add(category);var choices=q["Choices"].AsArray().Select(c=>c.GetValue<string>()).ToArray();
                    Check(choices.Length==4&&choices.Distinct().Count()==4&&!choices.Contains("Not sure")&&q["Unsure"].GetValue<string>()=="Not sure","four distinct answers and a separate unsure choice");
                    int correct=q["CorrectIndex"].GetValue<int>();Check(correct>=0&&correct<4,"host stores one correct answer index");
                    Check(q["Prompt"].GetValue<string>().Length>10,"question has authored clear text");
                    var events=show["Events"].AsArray();string expected;
                    switch(category)
                    {
                        case "Passers":expected=events.Count(e=>e["Kind"].GetValue<int>()==0).ToString(System.Globalization.CultureInfo.InvariantCulture);break;
                        case "Sweep":expected=events.First(e=>e["Kind"].GetValue<int>()==1)["Colour"].GetValue<string>();break;
                        case "Sign":expected=events.First(e=>e["Kind"].GetValue<int>()==2)["Shape"].GetValue<string>();break;
                        case "Prop":expected=events.Single(e=>e["Kind"].GetValue<int>()==0&&e["Prop"].GetValue<string>()!="")["Prop"].GetValue<string>();break;
                        case "FinalLights":expected=events.Last(e=>e["Kind"].GetValue<int>()==3)["Colour"].GetValue<string>();break;
                        default:throw new Exception("unknown category "+category);
                    }
                    Check(choices[correct]==expected,"correct answer comes only from the public real events for "+category);
                }
            }
            Check(players.Select(p0=>p0["Questions"].ToJsonString()).Distinct().Count()>1,"participants draw questions independently");
        }
        Check(categories.Count==5&&patterns.Count>20,"all five categories and varied orders are actually dealt");
        var sober=Started(count:1);var dosed=Started(count:1,before:x=>x.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,RemainingSeconds=100}));
        Check(Show(sober)["Players"][0]["Questions"].ToJsonString()==Show(dosed)["Players"][0]["Questions"].ToJsonString(),"private fake count cannot change questions or correct answers");
    }
    static void ActiveDoseIsCapturedWithExactlyThatManyStablePrivateFakes()
    {
        var s=Started(count:8,before:x=>
        {
            for(int k=1;k<=4;k++)x.Player("p"+k).Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=k,RemainingSeconds=100});
            x.Player("p5").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,RemainingSeconds=0});
            x.Player("p0").Effects.Add(new ActiveEffect{Id="lsd",Intensity=4,RemainingSeconds=100});
            x.Player("p6").Life="Spirit";x.Player("p7").Connected=false;
        });
        var show=Show(s);Check(show.ContainsKey("Players"),"start captures player-private dose and fake records");
        var players=show["Players"].AsArray();Check(players.Count==6,"only captured living participants receive private records");
        for(int k=0;k<6;k++)
        {
            var player=players.Single(x=>x["PlayerId"].GetValue<string>()=="p"+k);int expected=k<=4?k:0;
            Check(player["Dose"].GetValue<int>()==expected,"active dose is captured for p"+k);
            var fakes=player["Fakes"].AsArray();Check(fakes.Count==expected,"exactly captured dose fakes for p"+k);
            Check(fakes.All(f=>f["TintTell"].GetValue<bool>()&&f["MovementShimmer"].GetValue<bool>()&&(f["Shadow"].GetValue<int>()==0||f["Shadow"].GetValue<int>()==1)),"every fake has a wrong shadow, tint and movement-shimmer tell");
            Check(fakes.All(f=>f["Event"]["StartsAt"].GetValue<float>()>=0&&f["Event"]["EndsAt"].GetValue<float>()<=20),"private fake schedules stay inside the show");
        }
        Check(players[1]["Fakes"].ToJsonString()!=players[2]["Fakes"].ToJsonString(),"viewers have independent fake records");
        string captured=show.ToJsonString();foreach(var p0 in s.State.Players)p0.Effects.Clear();s.Player("p1").Connected=false;s.Tick(5);
        Check(Show(s).ToJsonString()==captured,"effect expiry and disconnect never reroll captured records");
        var clamped=Started(count:2,before:x=>{x.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=9,RemainingSeconds=100});x.Player("p1").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=-2,RemainingSeconds=100});});
        var dose=Show(clamped)["Players"].AsArray();Check(dose[0]["Dose"].GetValue<int>()==4&&dose[1]["Dose"].GetValue<int>()==0,"capture bounds dose to zero through four");
    }
    static void TheCommonStartPersistsTheRealTwentySecondScript()
    {
        var s=Started();var show=Show(s);var events=show["Events"].AsArray();
        Check(events.Count>0,"common start generates real events");
        Check(events.All(e=>e["StartsAt"].GetValue<float>()>=0&&e["EndsAt"].GetValue<float>()<=20&&e["EndsAt"].GetValue<float>()>e["StartsAt"].GetValue<float>()),"all real events live within the twenty-second show");
        Check(events.Any(e=>e["EndsAt"].GetValue<float>()==20),"the glover's final colour remains visible through twenty seconds");
        Check(show.ToJsonString()==Show(Started()).ToJsonString(),"same host seed and roster generate identical persisted records");
        Check(events.ToJsonString()!=Show(Started(124))["Events"].ToJsonString(),"a different level spin seed changes the real script");
        Check(FestivalSimulation.MinigameKinds.Contains("LightShow"),"the bag deals the Light Show now its real presentation and privacy consumers pass");
    }
}
