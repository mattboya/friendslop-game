using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// PLAYA-1: Ember Playa (festival 1) has four twists, and Palm Mirage has none of them. Money reads as odd objects, a different
// set for each player, over the same economy. Dust storms of 20-40 s blow on a seeded schedule and cut festivalgoers' sight
// from 12 m to 5 m. Two art cars crawl round slow loops, and a rider moves with their car out of festivalgoers' sight.
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
        foreach(var test in new Action[]{EmberPlayaCountsInOddObjects,EachPlayerKeepsTheirOwnObjects,DustStormsBlowOnASeededSchedule,
            StormsCutFestivalgoersSightTo5m,ArtCarsCrawlRoundTheirLoops,ArtCarsCarryRidersOutOfSight})
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

    // A level's storms, sampled every tenth of a second as (start, end) spells; a spell still blowing at `until` ends at infinity.
    static List<(double start,double end)> Storms(RoundState s,double until)
    {
        var spells=new List<(double,double)>();double began=-1;
        for(int tick=0;tick<=until*10;tick++)
        {
            s.ElapsedSeconds=tick/10.0;bool storm=FestivalSimulation.DustStorm(s);
            if(storm&&began<0)began=s.ElapsedSeconds;
            if(!storm&&began>=0){spells.Add((began,s.ElapsedSeconds));began=-1;}
        }
        if(began>=0)spells.Add((began,double.PositiveInfinity));
        return spells;
    }

    static void DustStormsBlowOnASeededSchedule()
    {
        var schedules=new HashSet<string>();
        for(int seed=1;seed<=40;seed++)
        {
            int spin=unchecked(seed*7919+13);var spells=Storms(new RoundState{FestivalIndex=1,SpinSeed=spin},Festivals.NightSeconds);
            Check(spells.Count>=3,"spin "+spin+": a night on Ember Playa brings at least three storms, got "+spells.Count);
            Check(spells[0].start>=60&&spells[0].start<=120,"spin "+spin+": the first storm rolls in 60-120 s into the level, got "+spells[0].start);
            for(int k=0;k<spells.Count;k++)
            {
                if(spells[k].end<double.PositiveInfinity)Check(spells[k].end-spells[k].start>=20&&spells[k].end-spells[k].start<=40,"spin "+spin+": storm "+k+" blows for 20-40 s, got "+(spells[k].end-spells[k].start));
                if(k>0)Check(spells[k].start-spells[k-1].end>=60&&spells[k].start-spells[k-1].end<=120,"spin "+spin+": 60-120 s of calm between storms, got "+(spells[k].start-spells[k-1].end));
            }
            schedules.Add(string.Join(";",spells));
            Check(Storms(new RoundState{FestivalIndex=0,SpinSeed=spin},Festivals.NightSeconds).Count==0,"spin "+spin+": no dust at Palm Mirage");
        }
        Check(schedules.Count==40,"each level's spin seeds its own schedule, got "+schedules.Count+" of 40");
        var live=Start(5,0,1);var seen=new List<bool>();
        for(int second=0;second<=Festivals.CalmMaxSeconds+1;second++){seen.Add(FestivalSimulation.DustStorm(live.State));live.Tick(1);}
        Check(!seen[0]&&seen.Contains(true),"a real level on Ember Playa calms at first, then a storm rolls in");
    }

    const int StormSpin=12345;
    // Some time well inside this spin's first storm.
    static double MidStorm(){var s=new RoundState{FestivalIndex=1,SpinSeed=StormSpin};return Storms(s,Festivals.NightSeconds)[0].start+5;}
    // A hand-built Day 1 at `seconds` into the level: a festivalgoer at the origin looking north (+Z) with p0 sprinting
    // `metres` in front of it, and a cop a metre east of it, also looking north, with p0's stock in plain sight.
    static FestivalSimulation Sighting(int festival,double seconds,double metres)
    {
        var s=new FestivalSimulation(3);s.AddPlayer("p0","P0");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.Phase="Playing";s.State.SpinSeed=StormSpin;s.State.ElapsedSeconds=seconds;
        s.State.Npcs.Clear();s.State.Npcs.Add(new NpcState{Id="wook"});s.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="Patrol",X=1});
        var p=s.Player("p0");p.X=0;p.Z=(float)metres;p.SprintUntil=1e9;p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        return s;
    }
    // Whether, in a tenth of a second, the festivalgoer notices p0 sprinting and the cop spots the stock.
    static (bool wook,bool cop) Spotted(FestivalSimulation s)
    {
        s.Tick(.1);var wook=s.State.Npcs.Find(n=>n.Id=="wook");var cop=s.State.Npcs.Find(n=>n.Id=="cop");
        return ((wook.Observers.Find(o=>o.PlayerId=="p0")?.Suspicion??0)>0,cop.Evidence.Exists(e=>e.PlayerId=="p0"));
    }

    static void StormsCutFestivalgoersSightTo5m()
    {
        double storm=MidStorm();
        Check(!FestivalSimulation.DustStorm(Sighting(1,1,6).State)&&FestivalSimulation.DustStorm(Sighting(1,storm,6).State),"setup: calm a second in, a storm at "+storm+" s");
        Check(Spotted(Sighting(1,1,11.9)).wook,"in calm air a festivalgoer spots a sprinter 11.9 m away");
        Check(!Spotted(Sighting(1,1,12.1)).wook,"but not 12.1 m away");
        var dusty=Spotted(Sighting(1,storm,6));
        Check(!dusty.wook,"in a storm the festivalgoer loses a sprinter 6 m away");
        Check(dusty.cop,"cops keep their eyes: the cop 6 m away still spots the stock");
        Check(Spotted(Sighting(1,storm,4.9)).wook,"a sprinter 4.9 m away is still spotted in a storm");
        Check(!Spotted(Sighting(1,storm,5.1)).wook,"5.1 m away is not");
        Check(Spotted(Sighting(0,storm,6)).wook,"Palm Mirage has no dust, so the same moment there is clear");
    }

    static double Dist(WorldPoint a,WorldPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
    static bool At(PlayerState p,WorldPoint at)=>Dist(new WorldPoint(p.X,p.Z),at)<1e-3;

    static void ArtCarsCrawlRoundTheirLoops()
    {
        Check(Festivals.ArtCars==2,"two art cars roll round Ember Playa");
        Check(Festivals.ArtCarSpeed>0&&Festivals.ArtCarSpeed<=1.5,"at a slow crawl, not "+Festivals.ArtCarSpeed+" m/s");
        var starts=new List<WorldPoint>();
        for(int car=0;car<Festivals.ArtCars;car++)
        {
            var start=Festivals.ArtCarAt(car,0);var last=start;double lap=-1,travelled=0;starts.Add(start);
            for(int tick=1;tick<=1200&&lap<0;tick++)
            {
                var at=Festivals.ArtCarAt(car,tick/10.0);double step=Dist(last,at);travelled+=step;last=at;
                Check(step<=Festivals.ArtCarSpeed*.1+1e-4,"car "+car+" never jumps: "+step+" m in a tenth of a second at "+tick/10.0+" s");
                Check(Math.Abs(at.X)<35&&Math.Abs(at.Z)<35,"car "+car+" stays on the festival grounds, at ("+at.X+", "+at.Z+")");
                if(tick>100&&Dist(at,start)<=Festivals.ArtCarSpeed*.1)lap=tick/10.0;
            }
            Check(lap>0,"car "+car+" comes back round to where it started within two minutes");
            Check(travelled>=lap*Festivals.ArtCarSpeed*.97,"car "+car+" keeps rolling all the way round: "+travelled+" m in "+lap+" s");
            Check(Dist(Festivals.ArtCarAt(car,lap+7),Festivals.ArtCarAt(car,7))<=Festivals.ArtCarSpeed*.1+1e-3&&Dist(Festivals.ArtCarAt(car,lap+7),Festivals.ArtCarAt(car,lap))>1,"and rolls on round again");
        }
        Check(Dist(starts[0],starts[1])>10,"the two cars roll on separate loops");
    }

    // A hand-built Ember Playa Day 1, ten seconds in and well before any storm: p0, with stock in hand, stands a metre from art
    // car 0, and p1 stands a metre from art car 1. Nobody from the crowd is around yet.
    static FestivalSimulation Cars(int festival=1)
    {
        var s=new FestivalSimulation(3);s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.Phase="Playing";s.State.SpinSeed=StormSpin;s.State.ElapsedSeconds=10;
        s.State.Npcs.Clear();
        for(int k=0;k<2;k++){var car=Festivals.ArtCarAt(k,10);var p=s.Player("p"+k);p.X=car.X+1;p.Z=car.Z;}
        s.Player("p0").Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        return s;
    }
    // A festivalgoer and a cop turn up 2 m north of p, looking straight at them, while p sprints.
    static void Watched(FestivalSimulation s,PlayerState p)
    {
        s.State.Npcs.Clear();p.SprintUntil=1e9;
        s.State.Npcs.Add(new NpcState{Id="wook",X=p.X,Z=p.Z+2,Yaw=180});s.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="Patrol",X=p.X+.5f,Z=p.Z+2,Yaw=180});
    }
    static double Heat(FestivalSimulation s,string npc,string player)=>s.State.Npcs.Find(n=>n.Id==npc).Observers.Find(o=>o.PlayerId==player)?.Suspicion??0;

    static void ArtCarsCarryRidersOutOfSight()
    {
        var far=Cars();far.Player("p0").X+=2.2f;
        Check(!Act(far,"p0","RideCar").Accepted&&FestivalSimulation.ArtCarOf(far.State,"p0")<0,"an art car is boarded from beside it, not 3.2 m off");
        var reach=Cars();reach.Player("p0").X+=1.9f;
        Check(Act(reach,"p0","RideCar").Accepted,"2.9 m off is close enough");
        Check(!Act(Cars(0),"p0","RideCar").Accepted,"Palm Mirage has no art cars");
        var dragging=Cars();dragging.Player("p1").Life="Downed";dragging.Player("p0").DragTargetId="p1";
        Check(!Act(dragging,"p0","RideCar").Accepted,"nobody climbs aboard dragging a friend");
        var carrying=Cars();carrying.Player("p1").Life="Spirit";carrying.State.Bodies.Add(new BodyState{PlayerId="p1",X=carrying.Player("p0").X,Z=carrying.Player("p0").Z});carrying.Player("p0").CarryBodyId="p1";
        Check(!Act(carrying,"p0","RideCar").Accepted,"or carrying a body");

        var s=Cars();var p=s.Player("p0");var q=s.Player("p1");
        var boarded=Act(s,"p0","RideCar");
        Check(boarded.Accepted&&FestivalSimulation.ArtCarOf(s.State,"p0")==0&&At(p,Festivals.ArtCarAt(0,s.State.ElapsedSeconds)),"beside art car 0, p0 climbs aboard: "+boarded.Reason);
        Check(Act(s,"p1","RideCar").Accepted&&FestivalSimulation.ArtCarOf(s.State,"p1")==1,"beside art car 1, p1 climbs aboard that one");
        s.Tick(5);
        Check(At(p,Festivals.ArtCarAt(0,s.State.ElapsedSeconds))&&At(q,Festivals.ArtCarAt(1,s.State.ElapsedSeconds))&&!At(p,Festivals.ArtCarAt(0,10)),"five seconds on, each rider has rolled on with their car");
        float x=p.X,z=p.Z;
        Check(!s.TryMove("p0",x+.05f,z,0,.1)&&p.X==x&&p.Z==z,"a rider can't walk about on the moving car");
        Check(s.TryMove("p0",x,z,90,.1)&&p.Yaw==90,"but can look around");
        Check(!Act(s,"p0","Extract").Accepted,"and does nothing else up there");
        Watched(s,p);s.Tick(.1);
        Check(Heat(s,"wook","p0")==0,"a festivalgoer looking right at a rider sprinting 2 m away doesn't see them");
        Check(s.State.Npcs.Find(n=>n.Id=="cop").Evidence.Exists(e=>e.PlayerId=="p0"),"a cop does, and spots the stock");
        s.Tick(20);Check(FestivalSimulation.ArtCarOf(s.State,"p0")==0&&At(p,Festivals.ArtCarAt(0,s.State.ElapsedSeconds)),"the ride goes on until the rider hops off");
        var off=Act(s,"p0","Cancel");float offX=p.X,offZ=p.Z;
        Check(off.Accepted&&FestivalSimulation.ArtCarOf(s.State,"p0")<0,"p0 hops off: "+off.Reason);
        s.Tick(1);Check(p.X==offX&&p.Z==offZ,"and stays where they hopped off as the car rolls on");
        Check(s.TryMove("p0",p.X+.05f,p.Z,0,.1),"walking again");
        Watched(s,p);s.Tick(.1);
        Check(Heat(s,"wook","p0")>0,"back on foot, festivalgoers see them sprint again");
    }
}
