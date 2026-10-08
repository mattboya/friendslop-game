using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

public static class LightShowTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static void Check(bool value,string message){if(!value)throw new Exception("LightShow: "+message);}
    public static void Run(){TheCommonStartPersistsTheRealTwentySecondScript();ActiveDoseIsCapturedWithExactlyThatManyStablePrivateFakes();EachViewerDrawsThreeCategoriesAndFourDistinctRealAnswers();FutureDoseDoesNotCountAsActive();PublicCopiesDetachTheRealScriptAndWithholdEveryPrivateDraw();HostSnapshotsRetainRecordsAndOldSnapshotsDefaultQuietly();RealAnswersNeverCountPrivateFakes();}
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
    static void PublicCopiesDetachTheRealScriptAndWithholdEveryPrivateDraw()
    {
        var s=Started(count:2,before:x=>x.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,RemainingSeconds=100}));
        string original=Show(s).ToJsonString();var owner=FestivalSimulation.VisibleBooth(s.State,"p0");var peer=FestivalSimulation.VisibleBooth(s.State,"p1");var spectator=FestivalSimulation.VisibleBooth(s.State,"");
        Check(owner.LightShow.Players.Count==0&&peer.LightShow.Players.Count==0&&spectator.LightShow.Players.Count==0,"interim public copies send no private fake, question, dose or answer records");
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
        Check(!FestivalSimulation.MinigameKinds.Contains("LightShow"),"the booth bag stays disabled until real presentation and privacy consumers pass");
    }
}
