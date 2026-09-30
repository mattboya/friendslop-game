using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// PLAYA-1: Ember Playa (festival 1) has four twists, and Palm Mirage has none of them. Money reads as odd objects, a different
// set for each player, over the same economy.
public static class PlayaTwistTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("PlayaTwist: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="")=>s.Execute(id,new GameCommand{Id="playa"+(sequence++),Kind=kind,TargetId=target,ItemId=item});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{EmberPlayaCountsInOddObjects,EachPlayerKeepsTheirOwnObjects})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" playa twist test(s) failed:\n"+string.Join("\n",failures));
    }

    // A crew of `crew` readies up at camp, the wheels spin and they load into the crowd.
    static FestivalSimulation Start(int seed,int level,int festival,int crew=2)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew is at "+Festivals.Name(festival));
        return s;
    }

    static void EmberPlayaCountsInOddObjects()
    {
        for(int k=0;k<8;k++)Check(Festivals.CurrencyName(0,k,12)=="$12"&&Festivals.CurrencyName(0,k,1)=="$1","Palm Mirage counts dollars for everyone, got "+Festivals.CurrencyName(0,k,12));
        Check(Festivals.CurrencyName(1,1,12)=="12 ramen packets","the second player counts ramen packets, got "+Festivals.CurrencyName(1,1,12));
        Check(Festivals.CurrencyName(1,1,1)=="1 ramen packet","one of them is a ramen packet, got "+Festivals.CurrencyName(1,1,1));
        Check(Festivals.CurrencyName(1,0,3)=="3 buttons"&&Festivals.CurrencyName(1,2,0)=="0 bottle caps","the first counts buttons and the third bottle caps, got "+Festivals.CurrencyName(1,0,3)+", "+Festivals.CurrencyName(1,2,0));
        var kinds=new HashSet<string>();
        for(int k=0;k<8;k++)
        {
            string many=Festivals.CurrencyName(1,k,5),one=Festivals.CurrencyName(1,k,1);
            Check(many.StartsWith("5 ")&&!many.Contains("$")&&one.StartsWith("1 ")&&!one.Contains("$"),"player "+k+" counts objects, not dollars: "+many+", "+one);
            Check(one.Substring(2)!=many.Substring(2),"player "+k+" has one "+one.Substring(2)+" but several "+many.Substring(2));
            kinds.Add(many.Substring(2));
        }
        Check(kinds.Count==8,"a full crew of eight counts in eight different objects, got "+string.Join(", ",kinds));
    }

    static void EachPlayerKeepsTheirOwnObjects()
    {
        var s=Start(3,0,1,8);
        for(int k=0;k<8;k++)Check(s.Player("p"+k).Ordinal==k,"p"+k+" is crew member "+k+" in joining order, got "+s.Player("p"+k).Ordinal);
        var kinds=new HashSet<string>();foreach(var p in s.State.Players)kinds.Add(Festivals.CurrencyName(s.State.FestivalIndex,p.Ordinal,5));
        Check(kinds.Count==8,"so each of the eight counts in their own objects: "+string.Join(", ",kinds));
        s.Disconnect("p3");s.AddPlayer("p3","P3");Check(s.Player("p3").Ordinal==3,"a friend who drops and rejoins keeps theirs");
        s.State.Phase="Results";s.State.Result="Success";Check(Act(s,"p0","Reset").Accepted,"setup: the host heads back to camp after Day 1");
        Check(s.State.LevelIndex==1,"setup: Night 1 is next");
        for(int k=0;k<8;k++)Check(s.Player("p"+k).Ordinal==k,"p"+k+" keeps theirs through the weekend, got "+s.Player("p"+k).Ordinal);
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        foreach(var p in legacy["Players"].AsArray())p.AsObject().Remove("Ordinal");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.Players.Count==8,"a snapshot from before odd objects still restores");
    }
}
