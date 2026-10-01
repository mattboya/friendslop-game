using System;
using System.Collections.Generic;
using System.Text.Json;
using Festival.Core;

// GAS-2: about one level in five hides a Giggle Tank at one of six listed spots off the beaten path, the same on every festival.
// It is public, not a vision: any living, free player within 1.5 m grabs it, which banks $25 in the crew's shared stash at once
// (never sale cash, so a day's quota doesn't move) and takes the tank away for everyone. The stash carries through the weekend,
// and withdrawing is a festival action at the crew stash, so the crew spends it there on the weekend's next level.
public static class GiggleTankTests
{
    const string Grab="Grab the Giggle Tank";
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("GiggleTank: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",int amount=0)=>s.Execute(id,new GameCommand{Id="tank"+(sequence++),Kind=kind,TargetId=target,Amount=amount});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{AboutOneLevelInFiveHidesATank,TheSpotsAreOffTheBeatenPath,AGrabBanksTheCrewStashOnce,OnlyTheLivingAndFreeGrabIt,
            TheNextLevelWithdrawsItAtTheCrewStash,SnapshotsKeepTheTankAndOlderOnesHaveNone})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" giggle tank test(s) failed:\n"+string.Join("\n",failures));
    }

    // A crew readies up at the trailhead and the wheels spin: the level is rolled as they leave camp.
    static FestivalSimulation Leave(int seed,int festival=0,int level=0,int crew=2)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);Check(s.State.Phase=="Spinning","setup: the crew leaves camp");
        return s;
    }
    static void Arrive(FestivalSimulation s){s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");Check(s.State.Phase=="Playing","setup: the crew is at the festival");}
    // The crew at the festival on the first Day 1 from `from` on whose roll a tank turns up.
    static FestivalSimulation WithATank(int from,int crew=2)
    {
        for(int seed=from;seed<from+100;seed++){var s=Leave(seed,crew:crew);if(FestivalSimulation.GiggleTankAt(s.State)!=null){Arrive(s);return s;}}
        throw new Exception("GiggleTank: setup: no level from seed "+from+" to "+(from+99)+" turned up a tank");
    }
    static string Listed(FestivalSimulation s,PlayerState p){var labels=new List<string>();foreach(var a in FestivalGuidance.TwistActions(s.State,p))labels.Add(a.Label);return "["+string.Join(" | ",labels)+"]";}
    // The command the HUD would send for the grab, or null when it isn't offered.
    static GameCommand Offered(FestivalSimulation s,PlayerState p)=>FestivalGuidance.TwistActions(s.State,p).Find(a=>a.Label==Grab).Command;
    static bool Far(WorldPoint a,float x,float z,double range){double dx=a.X-x,dz=a.Z-z;return Math.Sqrt(dx*dx+dz*dz)>range;}

    // 1,000 levels across both festivals and all four levels of a weekend. A level holds one spot, so never two tanks.
    static void AboutOneLevelInFiveHidesATank()
    {
        Check(Festivals.GiggleTankSpots.Length==6,"six listed spots, got "+Festivals.GiggleTankSpots.Length);
        int tanks=0;var used=new int[Festivals.GiggleTankSpots.Length];
        for(int seed=1;seed<=1000;seed++)
        {
            var s=Leave(seed,seed%Festivals.Count,seed/Festivals.Count%Festivals.LevelCount,1);var at=FestivalSimulation.GiggleTankAt(s.State);
            if(at==null)continue;
            int spot=Array.FindIndex(Festivals.GiggleTankSpots,w=>w.X==at.X&&w.Z==at.Z);
            Check(spot>=0&&spot==s.State.GiggleTankSpot,"seed "+seed+"'s tank sits on a listed spot, got ("+at.X+", "+at.Z+")");
            tanks++;used[spot]++;
        }
        Check(tanks>=170&&tanks<=230,"20% ±3% of 1,000 levels hide a tank, got "+tanks);
        Check(Array.IndexOf(used,0)<0,"every spot turns up: "+string.Join(", ",used));
    }

    // On both festivals: inside the fence, outside the VIP ropes, well off the art cars' loops and the Ferris wheel, and more than
    // 2.5 m from anywhere players routinely stand, so E there grabs the tank and nothing else. (EditMode's GiggleTankWorldTests
    // checks each spot has standing room among the walls.)
    static void TheSpotsAreOffTheBeatenPath()
    {
        Check(Festivals.GiggleTankSpots.Length==6,"six listed spots, got "+Festivals.GiggleTankSpots.Length);
        var stands=new List<(string Name,float X,float Z)>{("the crew stash",-25,-8),("the medical tent",24,-20),("security",27,5),("lost property",-28,16),
            ("the way back to camp",Festivals.CampGateX,Festivals.CampGateZ),("the DJ takeover",Catalog.StageTakeoverX,Catalog.StageTakeoverZ),
            ("the VIP stall",Festivals.VipStallX,Festivals.VipStallZ),("the VIP guard",Festivals.VipGuardPostX,Festivals.VipGuardPostZ),("the effigy",Festivals.EffigyX,Festivals.EffigyZ),
            // Visions.cs's secret spots, which take in SplitObjective.cs's lost friends' spots.
            ("a secret spot",16,-4),("a secret spot",-16,5),("a secret spot",-24,25),("a secret spot",25,24),("a secret spot",18,5)};
        for(int i=0;i<8;i++){var shelf=Catalog.ShopPoint(false,i);stands.Add(("a night-market shelf",shelf.X,shelf.Z));}
        // Festivalgoers start up to .3 m off their places.
        for(int i=0;i<FestivalCrowdLayout.Count;i++){var at=FestivalCrowdLayout.Get(i,0);stands.Add(("festivalgoer "+i+"'s place",at.X,at.Z));}
        foreach(var spot in Festivals.GiggleTankSpots)
        {
            string where="("+spot.X+", "+spot.Z+")";
            Check(Math.Abs(spot.X)<=38&&Math.Abs(spot.Z)<=38,where+" is inside the fence");
            Check(!Festivals.InVipZone(Festivals.PoloFestival,spot.X,spot.Z),where+" is outside the VIP ropes");
            Check(Far(spot,Festivals.WheelX,Festivals.WheelZ,9),where+" is clear of the Ferris wheel");
            // A lap of either loop takes under 40 s.
            for(double t=0;t<40;t+=.25)for(int k=0;k<Festivals.ArtCars;k++){var car=Festivals.ArtCarAt(k,t);Check(Far(spot,car.X,car.Z,5),where+" is well off art car "+k+"'s loop");}
            foreach(var stand in stands)Check(Far(spot,stand.X,stand.Z,stand.Name.StartsWith("festivalgoer")?2.8:2.5),where+" is more than 2.5 m from "+stand.Name);
            foreach(var other in Festivals.GiggleTankSpots)Check(other==spot||Far(spot,other.X,other.Z,10),where+" is a place of its own, not beside ("+other.X+", "+other.Z+")");
        }
    }

    static void AGrabBanksTheCrewStashOnce()
    {
        var s=WithATank(1);var at=FestivalSimulation.GiggleTankAt(s.State);var p=s.Player("p1");var mate=s.Player("p0");
        int stash=s.State.StashCash,cash=p.Cash,sales=s.State.LevelSales,gross=s.State.GrossSales;bool quota=FestivalSimulation.DayQuotaMet(s.State);
        p.X=at.X+1.6f;p.Z=at.Z;
        Check(Offered(s,p)==null,"1.6 m away the tank isn't offered: "+Listed(s,p));
        Check(!Act(s,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&s.State.StashCash==stash,"and the rules refuse a grab from there");
        p.X=at.X+1.05f;p.Z=at.Z+1.05f;
        var grab=Offered(s,p);Check(grab!=null,"within 1.5 m the HUD offers the tank: "+Listed(s,p));
        grab.Id="tank"+(sequence++);var result=s.Execute(p.Id,grab);
        Check(result.Accepted&&result.Reason=="Giggle Tank! +$25 for camp","the grab pays out with a notice, got "+result.Accepted+" \""+result.Reason+"\"");
        Check(s.State.StashCash==stash+25,"$25 lands in the crew's shared stash, got "+(s.State.StashCash-stash));
        Check(p.Cash==cash&&s.State.LevelSales==sales&&s.State.GrossSales==gross&&FestivalSimulation.DayQuotaMet(s.State)==quota,"it is camp money, not sale cash: the day's quota doesn't move");
        Check(FestivalSimulation.GiggleTankAt(s.State)==null&&s.State.GiggleTankFound,"the tank is gone for everyone");
        Check(Offered(s,p)==null,"so it isn't offered any more: "+Listed(s,p));
        mate.X=at.X;mate.Z=at.Z;var again=Act(s,mate.Id,FestivalSimulation.GrabGiggleTankKind);
        Check(!again.Accepted&&s.State.StashCash==stash+25,"a second grab, right on the spot, pays nothing, got \""+again.Reason+"\"");
        s.Tick(1);Check(s.State.StashCash==stash+25,"it pays once");
    }

    static void OnlyTheLivingAndFreeGrabIt()
    {
        var s=WithATank(1);var at=FestivalSimulation.GiggleTankAt(s.State);var p=s.Player("p1");int stash=s.State.StashCash;
        p.X=at.X;p.Z=at.Z;
        foreach(var life in new[]{"Spirit","Detained","Downed"})
        {
            p.Life=life;
            Check(Offered(s,p)==null,"a player who is "+life+" isn't offered the tank: "+Listed(s,p));
            var refused=Act(s,p.Id,FestivalSimulation.GrabGiggleTankKind);
            Check(!refused.Accepted&&s.State.StashCash==stash&&FestivalSimulation.GiggleTankAt(s.State)!=null,"a player who is "+life+" can't grab it, got \""+refused.Reason+"\"");
        }
        p.Life="Alive";
        Check(Offered(s,p)!=null&&Act(s,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&s.State.StashCash==stash+25,"back on their feet, they grab it");
    }

    static void TheNextLevelWithdrawsItAtTheCrewStash()
    {
        var s=WithATank(1,1);var p=s.Player("p0");var at=FestivalSimulation.GiggleTankAt(s.State);p.X=at.X;p.Z=at.Z;
        Check(Act(s,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&s.State.StashCash==25,"setup: the tank banks $25");
        s.State.Phase="Results";s.State.Result="Success";
        Check(Act(s,p.Id,"Reset").Accepted&&Act(s,p.Id,"FinishReview").Accepted&&s.State.Phase=="Shopping","setup: Day 1 is won and the crew is back at camp");
        Check(s.State.StashCash==25&&Festivals.For(s.State).Name=="Night 1","the $25 waits in the stash for Night 1, got "+s.State.StashCash);
        p=s.Player("p0");p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: ready for Night 1");
        s.Tick(5.2);Check(!s.State.GiggleTankFound,"Night 1 rolls a tank of its own");
        Arrive(s);
        int cash=p.Cash;var stash=s.State.Stashes.Find(x=>x.Id=="stash");p.X=stash.X;p.Z=stash.Z;
        Check(Act(s,p.Id,"Withdraw","stash",25).Accepted&&p.Cash==cash+25&&s.State.StashCash==0,"at the crew stash the crew withdraws all $25 to spend, got cash +"+(p.Cash-cash)+", stash "+s.State.StashCash);
    }

    static void SnapshotsKeepTheTankAndOlderOnesHaveNone()
    {
        var s=WithATank(1);var json=JsonSerializer.SerializeToNode(s.State,Json).AsObject();
        var saved=new FestivalSimulation();saved.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        Check(saved.State.GiggleTankSpot==s.State.GiggleTankSpot&&FestivalSimulation.GiggleTankAt(saved.State)!=null,"a snapshot keeps the level's tank");
        json.Remove("GiggleTankSpot");json.Remove("GiggleTankFound");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        Check(old.State.GiggleTankSpot==-1&&FestivalSimulation.GiggleTankAt(old.State)==null,"a snapshot from before Giggle Tanks has none");
        var p=old.Player("p0");int stash=old.State.StashCash;
        foreach(var spot in Festivals.GiggleTankSpots){p.X=spot.X;p.Z=spot.Z;Check(!Act(old,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&old.State.StashCash==stash,"so there is nothing to grab at ("+spot.X+", "+spot.Z+")");}
    }
}
