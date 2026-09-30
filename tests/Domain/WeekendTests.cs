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
