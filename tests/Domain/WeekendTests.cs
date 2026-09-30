using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

public static class WeekendTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static void Check(bool condition,string message){if(!condition)throw new Exception("Weekend: "+message);}
    static bool Same(double a,double b)=>Math.Abs(a-b)<1e-9;
    public static void Run()
    {
        FestivalTable();
        EncoreScaling();
        NewCrewStartsDayOne();
        SnapshotsKeepTheWeekendPosition();
        WeekendCarriesCashGearAndStash();
        FailureRestartsTheFestival();
        HostChoosesAnUnlockedFestival();
        WinningNightTwoUnlocksOnTheResultsScreen();
    }

    static int sequence;
    static CommandResult Act(FestivalSimulation s,string player,string kind,int amount=0)=>s.Execute(player,new GameCommand{Id="weekend_"+(++sequence),Kind=kind,Amount=amount});
    static string Where(FestivalSimulation s)=>s.State.FestivalIndex+"/"+s.State.LevelIndex+"/"+s.State.EncoreTier;
    static FestivalSimulation Crew(int seed){var s=new FestivalSimulation(seed);s.AddPlayer("a","A");s.AddPlayer("b","B");return s;}
    // The level just ended with this result; the host brings the crew back to camp, which opens the debrief.
    static void Finish(FestivalSimulation s,string result){s.State.Phase="Results";s.State.Result=result;Check(Act(s,"a","Reset").Accepted&&s.State.Phase=="CampReview","host brings the crew back after "+result);}
    // What a player walked out of the level holding, plus the level's shared stash and scars that must not carry.
    static void Earn(FestivalSimulation s,int cash,int confetti,int stash)
    {
        var a=s.Player("a");a.Cash=cash;a.Inventory.Clear();a.Inventory.Add(new ItemStack{ItemId="confetti",Count=confetti});a.EquippedItemId="confetti";a.HasCosmetic=true;s.State.StashCash=stash;
        a.Effects.Add(new ActiveEffect{Id="weed",InstanceId="effect_x",RemainingSeconds=30});a.Wristbands.Add("b");a.Life="Detained";a.Health=40;
        s.State.Npcs[0].Observers.Add(new ObserverState{PlayerId="a",Suspicion=80});
    }
    static void Fresh(FestivalSimulation s,string why)
    {
        var a=s.Player("a");
        Check(a.Cash==20&&a.Inventory.Count==0&&a.EquippedItemId==""&&s.State.StashCash==0,why+": fresh $20, no gear, empty stash");
        Check(a.HasCosmetic,why+": cosmetics are kept forever");
        Check(s.State.DurationSeconds==480&&Festivals.For(s.State).Name=="Day 1",why+": back to an eight-minute Day 1");
    }

    static void WeekendCarriesCashGearAndStash()
    {
        var s=Crew(40);var next=new[]{"Night 1","Day 2","Night 2"};var seconds=new[]{600.0,480,600};
        for(int level=1;level<4;level++)
        {
            int seed=s.State.Seed;Earn(s,30+level,level,5*level);
            Finish(s,"Success");var a=s.Player("a");string where=next[level-1];
            Check(Where(s)=="0/"+level+"/0"&&Festivals.For(s.State).Name==where,"clearing a level moves on to "+where);
            Check(a.Cash==30+level&&a.Inventory.Count==1&&a.Inventory[0].ItemId=="confetti"&&a.Inventory[0].Count==level&&a.EquippedItemId=="confetti","cash and gear carry into "+where);
            Check(s.State.StashCash==5*level&&a.HasCosmetic,"stash cash and cosmetics carry into "+where);
            Check(a.Effects.Count==0&&a.Wristbands.Count==0&&a.Life=="Alive"&&a.Health==100,"effects, wristbands and injuries stay behind ("+where+")");
            Check(s.State.Seed==seed+1&&s.State.Npcs[0].Observers.Count==0,"the grounds reseed and forget suspicion ("+where+")");
            Check(s.State.DurationSeconds==seconds[level-1]&&s.State.UnlockedFestivalCount==1,where+" length, and nothing unlocks mid-weekend");
        }
        Earn(s,90,2,15);Finish(s,"Success");
        Check(Where(s)=="1/0/0"&&s.State.UnlockedFestivalCount==2,"clearing Night 2 unlocks and moves to the next festival");
        Fresh(s,"a new festival");
        for(int level=0;level<4;level++){Earn(s,50,1,9);Finish(s,"Success");}
        Check(Where(s)=="0/0/1"&&s.State.UnlockedFestivalCount==2,"clearing the last festival starts an encore lap at the first festival");
        Fresh(s,"an encore lap");
        Check(Festivals.For(s.State).QuotaPerCrew==19&&Festivals.For(s.State).Narcs==3,"the encore lap reads the harder row");
        for(int level=0;level<4;level++)Finish(s,"Success");
        Check(Where(s)=="1/0/1","the encore continues to the next festival at the same lap");
    }

    static void FailureRestartsTheFestival()
    {
        var s=Crew(60);Earn(s,44,2,10);Finish(s,"Success");Earn(s,61,3,12);Finish(s,"Success");
        Check(Where(s)=="0/2/0","setup: Day 2 of the first festival");
        Earn(s,70,1,8);Finish(s,"Time expired");
        Check(Where(s)=="0/0/0","failing Day 2 restarts the same festival at Day 1");
        Fresh(s,"a failed weekend");
        Finish(s,"No living free teammate can complete a rescue");
        Check(Where(s)=="0/0/0","failing Day 1 stays on Day 1");
        for(int level=0;level<4;level++)Finish(s,"Success");
        for(int level=0;level<3;level++)Finish(s,"Success");
        Check(Where(s)=="1/3/0"&&s.State.UnlockedFestivalCount==2,"setup: Night 2 of the second festival");
        Earn(s,120,2,30);Finish(s,"Missed the quota");
        Check(Where(s)=="1/0/0"&&s.State.UnlockedFestivalCount==2,"failing the second festival restarts it and keeps it unlocked");
        Fresh(s,"a failed second festival");
    }

    static void HostChoosesAnUnlockedFestival()
    {
        var s=Crew(70);
        Check(!Act(s,"a","ChooseFestival",1).Accepted&&s.State.FestivalIndex==0,"a locked festival cannot be chosen");
        Check(Act(s,"a","ChooseFestival",0).Accepted&&s.State.FestivalIndex==0,"the unlocked first festival can be chosen");
        for(int level=0;level<4;level++)Finish(s,"Success");
        s.State.Phase="Shopping";
        Check(Where(s)=="1/0/0","setup: first festival cleared, at camp before Day 1");
        Check(!Act(s,"b","ChooseFestival",0).Accepted&&s.State.FestivalIndex==1,"only the host chooses");
        Check(!Act(s,"a","ChooseFestival",2).Accepted&&!Act(s,"a","ChooseFestival",-1).Accepted&&s.State.FestivalIndex==1,"no such festival");
        Check(Act(s,"a","ChooseFestival",0).Accepted&&Where(s)=="0/0/0","the host can replay a cleared festival");
        Check(Act(s,"a","ChooseFestival",1).Accepted&&Where(s)=="1/0/0","and switch back to an unlocked one");
        Finish(s,"Success");
        Check(!Act(s,"a","ChooseFestival",0).Accepted,"no switching during the debrief");
        s.State.Phase="Shopping";
        Check(!Act(s,"a","ChooseFestival",0).Accepted&&Where(s)=="1/1/0","no switching mid-weekend");
        s.State.LevelIndex=0;s.State.Phase="Playing";
        Check(!Act(s,"a","ChooseFestival",0).Accepted&&s.State.FestivalIndex==1,"no switching once the level is under way");
    }

    // The host's profile saves unlocks every frame, so the unlock must land the moment Night 2 is won: a host who leaves from
    // the results screen never presses NEXT CAMP. Reaches Results through a real launch and extract, not the Finish poke.
    static void WinningNightTwoUnlocksOnTheResultsScreen()
    {
        var s=Crew(80);
        for(int level=0;level<3;level++)Finish(s,"Success");
        s.State.Phase="Shopping";Check(Where(s)=="0/3/0","setup: debrief over, at camp before Night 2");
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Act(s,p.Id,"Ready");}
        s.Tick(5.2+FestivalSimulation.SpinSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: Night 2 under way");
        s.State.FriendFound=true;s.State.FriendPosition=new WorldPoint(Festivals.CampGateX,Festivals.CampGateZ);
        foreach(var p in s.State.Players){p.X=Festivals.CampGateX;p.Z=Festivals.CampGateZ;}
        Check(Act(s,"a","Extract").Accepted,"the whole crew and the friend are at the gate");s.Tick(3.1);
        Check(s.State.Phase=="Results"&&s.State.Result=="Success","setup: Night 2 is won");
        Check(s.State.UnlockedFestivalCount==2,"winning Night 2 unlocks the next festival on the results screen, before anyone presses NEXT CAMP");
    }

    static void FestivalTable()
    {
        Check(Festivals.Count==2&&Festivals.Name(0)=="Palm Mirage"&&Festivals.Name(1)=="Ember Playa","two fictional festivals, polo field then playa");
        Check(Festivals.LevelCount==4,"a weekend is four levels");
        var names=new[]{"Day 1","Night 1","Day 2","Night 2"};var night=new[]{false,true,false,true};var seconds=new[]{480.0,600,480,600};
        var quota=new[]{new[]{15,0,20,0},new[]{22,0,30,0}};
        var narcs=new[]{new[]{2,2,3,3},new[]{3,4,5,5}};
        var heat=new[]{new[]{1.0,1.1,1.2,1.3},new[]{1.25,1.35,1.5,1.6}};
        var chain=new[]{new[]{0,2,0,3},new[]{0,3,0,4}};
        for(int festival=0;festival<2;festival++)for(int level=0;level<4;level++)
        {
            var t=Festivals.Level(festival,level,0);string where=Festivals.Name(festival)+" "+names[level];
            Check(t.Name==names[level]&&t.Night==night[level]&&t.DurationSeconds==seconds[level],where+" is named and timed from the table");
            Check(t.QuotaPerCrew==quota[festival][level],where+" quota per crew member");
            Check(t.Narcs==narcs[festival][level],where+" narc count");
            Check(Same(t.SuspicionMultiplier,heat[festival][level]),where+" suspicion multiplier");
            Check(t.ChainLength==chain[festival][level],where+" clue-chain length");
        }
        var s=new FestivalSimulation(2);s.State.FestivalIndex=1;s.State.LevelIndex=3;s.State.EncoreTier=1;
        var current=Festivals.For(s.State);Check(current.Name=="Night 2"&&current.ChainLength==5&&current.Narcs==6,"a round reads its own row");
    }

    static void EncoreScaling()
    {
        var lap=Festivals.Level(0,0,1);
        Check(lap.QuotaPerCrew==19&&lap.Narcs==3&&Same(lap.SuspicionMultiplier,1.25)&&lap.DurationSeconds==480,"one encore lap: quota and suspicion x1.25 (rounded), one more narc");
        Check(Festivals.Level(0,0,3).QuotaPerCrew==26,"quota rounds to the nearest dollar (15 x 1.75 = 26.25)");
        var night=Festivals.Level(0,1,2);
        Check(night.ChainLength==4&&night.Narcs==4&&Same(night.SuspicionMultiplier,1.65)&&night.QuotaPerCrew==0,"two laps: chain +2, narcs +2, suspicion x1.5, nights keep no quota");
        var capped=Festivals.Level(1,3,4);
        Check(Same(capped.SuspicionMultiplier,2.5)&&capped.Narcs==8&&capped.ChainLength==5,"encore caps suspicion at 2.5, narcs at 8 and the chain at 5");
        Check(Festivals.Level(1,2,4).QuotaPerCrew==60&&Festivals.Level(1,2,4).ChainLength==0,"quota doubles by lap four; days have no chain");
    }

    static void NewCrewStartsDayOne()
    {
        var s=new FestivalSimulation(4);
        Check(s.State.FestivalIndex==0&&s.State.LevelIndex==0&&s.State.EncoreTier==0,"a new crew starts Day 1 of the first festival");
        Check(s.State.UnlockedFestivalCount==1,"only the first festival is unlocked");
        Check(s.State.DurationSeconds==480,"Day 1 lasts eight minutes");
    }

    static void SnapshotsKeepTheWeekendPosition()
    {
        var s=new FestivalSimulation(6);s.AddPlayer("a","A");
        var old=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        foreach(var field in new[]{"FestivalIndex","LevelIndex","EncoreTier","UnlockedFestivalCount"}){Check(old.ContainsKey(field),field+" is a serialised public field");old.Remove(field);}
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),Json));
        Check(restored.State.FestivalIndex==0&&restored.State.LevelIndex==0&&restored.State.UnlockedFestivalCount==1,"a snapshot from before weekends restores as Day 1 with one festival unlocked");
        var tamper=new Action<RoundState>[]{x=>x.LevelIndex=4,x=>x.LevelIndex=-1,x=>x.FestivalIndex=2,x=>x.EncoreTier=-1,x=>x.UnlockedFestivalCount=0,x=>x.UnlockedFestivalCount=3,x=>x.FestivalIndex=1};
        for(int i=0;i<tamper.Length;i++)
        {
            var bad=JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json);tamper[i](bad);
            bool refused=false;try{new FestivalSimulation().Restore(bad);}catch(ArgumentException){refused=true;}
            Check(refused,"snapshot with an impossible weekend position is refused (case "+i+")");
        }
    }
}
