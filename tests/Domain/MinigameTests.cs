using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Festival.Core;

// MINI-1: every level has one minigame booth, a stranger with a sign at one of Festivals.BoothSpots on a crowd's edge, its game
// drawn from a weekend bag. Every connected living player standing in its 4 m ring for 3 s in a row starts the game, unless
// anyone is downed or busy. The round's phase is then "Minigame": the level clock and every danger timer stop, nobody moves,
// and only the game's own commands go through. It runs at most 90 s, its result shows for 4 s, and then every deadline on the
// simulation clock moves on by the pause, the result's time is paid (a loss never leaves under 15 s) and the booth is spent.
// The bag deals MINI-4's Mystery Cups (MysteryCupsTests), the one game so far: the player up calls it at once for +0 s. The
// pause's own tests that need a game nobody ends turn it into one this build doesn't know (Endless), which only the cap ends.
public static class MinigameTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Minigame: "+message);}
    static bool Same(double a,double b)=>Math.Abs(a-b)<1e-6;
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="",int amount=0)=>s.Execute(id,new GameCommand{Id="mini"+(sequence++),Kind=kind,TargetId=target,ItemId=item,Amount=amount});
    static bool Running(FestivalSimulation s)=>s.State.Phase==FestivalSimulation.MinigamePhase;
    // The player up in the Mystery Cups game under way calls it, ending it at once for +0 s.
    static CommandResult CallIt(FestivalSimulation s)=>Act(s,FestivalSimulation.CupsTurn(s.State),FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups);
    // The game under way becomes one no move ends, as a game from a build that knows other games reads here: only the 90 s cap,
    // or scoring it outright, ends it. A turn of Mystery Cups runs out in 10 s, and the pause's tests need longer.
    static void Endless(FestivalSimulation s)=>s.State.Booth.Kind="Endless";

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{EveryLevelPlacesOneBoothOnASeededSpot,BoothGamesComeOutOfAWeekendBag,TheBoothSpotsKeepClearOfEveryRoute,
            TheCountdownNeedsEveryLivingPlayerInTheRing,NobodyDownedOrBusyMayStartIt,TheClockStopsForEverything,AHandoffKeepsItsSecondsThroughAMinuteLongGame,
            EveryClockDeadlineMovesOnByThePause,TheGameEndsAtNinetySecondsAndItsResultShowsForFour,ALossNeverLeavesUnderFifteenSeconds,ThePlayedBoothIsSpent,
            SnapshotsKeepTheBoothAndOlderOnesHaveNone,TheWorldsClockStandsStillThroughAGame})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" minigame test(s) failed:\n"+string.Join("\n",failures));
    }

    // A crew readies up at the trailhead and the wheels spin: the level, and its booth, are rolled as they leave camp.
    static FestivalSimulation Leave(int seed,int crew=2,int festival=0,int level=0)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;
        Ready(s);return s;
    }
    static void Ready(FestivalSimulation s){foreach(var p in s.State.Players)if(p.Connected){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}s.Tick(5.2);Check(s.State.Phase=="Spinning","setup: the crew leaves camp");}
    // The crew reaches the festival with the crowd cleared, so nobody but the test's own festivalgoers moves a thing.
    static FestivalSimulation Play(int seed,int crew=2)
    {
        var s=Leave(seed,crew);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew is at the festival");s.State.Npcs.Clear();Check(FestivalSimulation.BoothAt(s.State)!=null,"setup: the level has a booth");return s;
    }
    // The level is won; the host brings everyone back and the next level's camp opens.
    static void Win(FestivalSimulation s){s.State.Phase="Results";s.State.Result="Success";Check(Act(s,s.State.HostPlayerId,"Reset").Accepted,"setup: the host brings the crew back");s.State.Phase="Shopping";}
    // Stands the players in the booth's ring, a metre apart, all within 4 m of it.
    static void Ring(FestivalSimulation s,params string[] ids){var at=FestivalSimulation.BoothAt(s.State);for(int k=0;k<ids.Length;k++){var p=s.Player(ids[k]);p.X=at.X+k-.5f*(ids.Length-1);p.Z=at.Z;}}
    // Ticks in tenths until the game starts, for at most `seconds`; whether it did.
    static bool StartsWithin(FestivalSimulation s,double seconds){for(double t=0;t<seconds-1e-9;t+=.1){s.Tick(.1);if(Running(s))return true;}return false;}
    // The crew of the given size, all of it in the ring and the game under way.
    static FestivalSimulation Started(int seed,int crew=2)
    {
        var s=Play(seed,crew);Ring(s,s.State.Players.ConvertAll(p=>p.Id).ToArray());Check(StartsWithin(s,3.5),"setup: the crew starts the booth's game");return s;
    }
    // Ticks in tenths until play resumes, for at most `seconds`; whether it did.
    static bool ResumesWithin(FestivalSimulation s,double seconds){for(double t=0;t<seconds-1e-9;t+=.1){s.Tick(.1);if(s.State.Phase=="Playing")return true;}return false;}
    static double Left(FestivalSimulation s)=>s.State.DurationSeconds-s.State.ElapsedSeconds;

    // 300 Day 1s of both festivals: each places one booth, on a listed spot, every spot turns up, and each plays a listed game.
    // Camp has none, and every level of the weekend, Night 2 too, has its own.
    static void EveryLevelPlacesOneBoothOnASeededSpot()
    {
        Check(Festivals.BoothSpots.Length>=3,"at least three listed spots, got "+Festivals.BoothSpots.Length);
        var camp=new FestivalSimulation(1);camp.AddPlayer("p0","P0");
        Check(camp.State.Booth.Spot==-1&&FestivalSimulation.BoothAt(camp.State)==null,"camp has no booth");
        var used=new int[Festivals.BoothSpots.Length];
        for(int seed=1;seed<=300;seed++)
        {
            var s=Leave(seed,1,seed%Festivals.Count);var b=s.State.Booth;var at=FestivalSimulation.BoothAt(s.State);
            Check(b.Spot>=0&&b.Spot<Festivals.BoothSpots.Length&&at!=null&&at.X==Festivals.BoothSpots[b.Spot].X&&at.Z==Festivals.BoothSpots[b.Spot].Z,"seed "+seed+"'s booth stands on a listed spot, got spot "+b.Spot);
            Check(FestivalSimulation.MinigameKinds.Contains(b.Kind)&&!b.Spent,"seed "+seed+"'s booth plays a listed game, unplayed, got '"+b.Kind+"'");
            Check(b.Kind==FestivalSimulation.MysteryCupsGame,"for now that is Mystery Cups, no stub, got '"+b.Kind+"'");
            used[b.Spot]++;
        }
        Check(Array.IndexOf(used,0)<0,"every spot turns up: "+string.Join(", ",used));
        var weekend=Leave(7,2);
        for(int level=0;level<Festivals.LevelCount;level++)
        {
            Check(weekend.State.LevelIndex==level&&FestivalSimulation.BoothAt(weekend.State)!=null,Festivals.For(weekend.State).Name+" has a booth");
            Win(weekend);if(level<Festivals.LevelCount-1)Ready(weekend);
        }
    }

    // A weekend offers every game before any repeats: a bag carried level to level, refilled once empty, and a lost level
    // restarts the weekend with a full bag. Seeded with two made-up games to show the carry.
    static void BoothGamesComeOutOfAWeekendBag()
    {
        var s=Leave(21,2);var all=new List<string>(s.State.MinigameBag){s.State.Booth.Kind};all.Sort();
        Check(string.Join(",",all)==string.Join(",",FestivalSimulation.MinigameKinds.OrderBy(k=>k)),"Day 1 draws from a full bag of every game: drawn plus left "+string.Join(",",all));
        var t=Leave(22,2);Win(t);t.State.MinigameBag=new List<string>{"x","y"};Ready(t);
        string first=t.State.Booth.Kind;
        Check((first=="x"||first=="y")&&t.State.MinigameBag.Count==1&&t.State.MinigameBag[0]!=first,"Night 1 draws from the bag the weekend carries, got '"+first+"', left "+string.Join(",",t.State.MinigameBag));
        Win(t);Ready(t);
        Check(t.State.Booth.Kind==(first=="x"?"y":"x")&&t.State.MinigameBag.Count==0,"Day 2 takes the game the bag still held, got '"+t.State.Booth.Kind+"'");
        Win(t);Ready(t);
        Check(FestivalSimulation.MinigameKinds.Contains(t.State.Booth.Kind),"an empty bag refills with every game, got '"+t.State.Booth.Kind+"'");
        var lost=Leave(23,2);lost.State.MinigameBag=new List<string>{"x","y"};lost.State.Phase="Results";lost.State.Result="Time expired";Check(Act(lost,"p0","Reset").Accepted,"setup: the crew comes back from a lost level");lost.State.Phase="Shopping";Ready(lost);
        Check(lost.State.LevelIndex==0&&FestivalSimulation.MinigameKinds.Contains(lost.State.Booth.Kind),"a lost level restarts the weekend with a full bag, got '"+lost.State.Booth.Kind+"'");
    }

    // A crew opts in by standing in the ring, so nobody walking their usual routes may do so by accident: every spot keeps its
    // 4 m ring clear of where players routinely stand (and the PlayMode spawn row), south of the backstage line where standing
    // draws suspicion, off the main path, the art cars' loops, the Ferris wheel and the VIP ropes.
    static void TheBoothSpotsKeepClearOfEveryRoute()
    {
        double ring=FestivalSimulation.MinigameRingRadius;
        var stands=new List<(string Name,float X,float Z,double Reach)>{("the crew stash",-25,-8,2.5),("the medical tent",24,-20,2.5),("security",27,5,2.5),("lost property",-28,16,2.5),
            ("the way back to camp",Festivals.CampGateX,Festivals.CampGateZ,Festivals.CampGateRadius),("the DJ takeover",Catalog.StageTakeoverX,Catalog.StageTakeoverZ,2.5),
            ("the VIP stall",Festivals.VipStallX,Festivals.VipStallZ,Festivals.VipStallRange),("the VIP guard",Festivals.VipGuardPostX,Festivals.VipGuardPostZ,2.5),("the effigy",Festivals.EffigyX,Festivals.EffigyZ,Festivals.BurnRingRadius+2.5),
            ("the Ferris wheel",Festivals.WheelX,Festivals.WheelZ,8),
            // Visions.cs's secret spots, which take in SplitObjective.cs's lost friends' spots.
            ("a secret spot",16,-4,2.5),("a secret spot",-16,5,2.5),("a secret spot",-24,25,2.5),("a secret spot",25,24,2.5),("a secret spot",18,5,2.5)};
        for(int i=0;i<8;i++){var shelf=Catalog.ShopPoint(false,i);stands.Add(("a night-market shelf",shelf.X,shelf.Z,2.5));stands.Add(("the level's spawn row",-7+2*i,-29,1));}
        foreach(var tank in Festivals.GiggleTankSpots)stands.Add(("a Giggle Tank spot",tank.X,tank.Z,Festivals.GiggleTankReach));
        foreach(var landmark in CloudShapes.Landmarks)stands.Add(("the "+landmark.Name+"'s cloud stash",landmark.Stash.X,landmark.Stash.Z,2.5));
        // Festivalgoers start up to .3 m off their places, and a sale or a dance is made within 2.5 m of them.
        for(int i=0;i<FestivalCrowdLayout.Count;i++){var at=FestivalCrowdLayout.Get(i,0);stands.Add(("festivalgoer "+i+"'s place",at.X,at.Z,2.8));}
        foreach(var spot in Festivals.BoothSpots)
        {
            string where="("+spot.X+", "+spot.Z+")";
            Check(Math.Abs(spot.X)+ring<=39&&Math.Abs(spot.Z)+ring<=39,where+"'s ring is inside the fence");
            Check(spot.Z+ring<=30,where+"'s ring stays south of the backstage line (z 30), where standing draws suspicion");
            Check(Math.Abs(spot.X)-ring>3.5,where+"'s ring is off the main path");
            for(float x=spot.X-(float)ring;x<=spot.X+ring;x+=.5f)for(float z=spot.Z-(float)ring;z<=spot.Z+ring;z+=.5f)Check(!Festivals.InVipZone(Festivals.PoloFestival,x,z),where+"'s ring is outside the VIP ropes");
            // A lap of either loop takes under 40 s; the booth's stranger stands well off it.
            for(double t=0;t<40;t+=.25)for(int k=0;k<Festivals.ArtCars;k++){var car=Festivals.ArtCarAt(k,t);Check(Math.Sqrt((spot.X-car.X)*(spot.X-car.X)+(spot.Z-car.Z)*(spot.Z-car.Z))>ring+2,where+" is well off art car "+k+"'s loop");}
            foreach(var stand in stands){double dx=spot.X-stand.X,dz=spot.Z-stand.Z;Check(Math.Sqrt(dx*dx+dz*dz)>ring+stand.Reach,where+"'s ring keeps clear of "+stand.Name+" ("+stand.X+", "+stand.Z+")");}
            foreach(var other in Festivals.BoothSpots){double dx=spot.X-other.X,dz=spot.Z-other.Z;Check(other==spot||Math.Sqrt(dx*dx+dz*dz)>10,where+" is a place of its own, not beside ("+other.X+", "+other.Z+")");}
        }
    }

    // The countdown runs while every connected living player stands in the ring, unbroken: one outside holds it, leaving resets
    // it. The dead and the detained aren't needed, and a crew of one starts it alone.
    static void TheCountdownNeedsEveryLivingPlayerInTheRing()
    {
        var s=Play(31,3);var at=FestivalSimulation.BoothAt(s.State);var c=s.Player("p2");
        Ring(s,"p0","p1");c.X=at.X+FestivalSimulation.MinigameRingRadius+.5f;c.Z=at.Z;
        Check(!StartsWithin(s,6),"one living player outside the ring holds it, however long the rest wait");
        Ring(s,"p0","p1","p2");Check(!StartsWithin(s,2.4),"three seconds have not passed with everyone in");
        c.X=at.X+FestivalSimulation.MinigameRingRadius+.5f;s.Tick(.2);Ring(s,"p0","p1","p2");
        Check(!StartsWithin(s,2.4),"stepping out resets the countdown: 2.4 s after coming back, nothing yet");
        Check(StartsWithin(s,1)&&s.State.Booth.PlayerIds.Count==3,"3 s in a row with everyone in starts the game for all three, got "+s.State.Phase);
        var t=Play(32,3);t.Player("p2").Life="Spirit";t.Player("p1").Life="Detained";Ring(t,"p0");
        Check(StartsWithin(t,3.5)&&string.Join(",",t.State.Booth.PlayerIds)=="p0","a spirit and a detained friend aren't needed, and only the living play");
        var u=Play(33,2);u.Disconnect("p1");Ring(u,"p0");
        Check(StartsWithin(u,3.5),"a friend who left isn't needed");
        var solo=Play(34,1);Ring(solo,"p0");Check(StartsWithin(solo,3.5),"a crew of one starts it alone");
    }

    // Nobody downed, nor anyone in the middle of something (lying down, a ride, a talk, a dance), may start it.
    static void NobodyDownedOrBusyMayStartIt()
    {
        var s=Play(41,3);Ring(s,"p0","p1");var down=s.Player("p2");down.Life="Downed";down.DownedRemaining=60;
        Check(!StartsWithin(s,5),"a downed friend, even out of the ring, keeps the game from starting");
        var t=Play(42,2);Ring(t,"p0","p1");
        Check(Act(t,"p1",FestivalSimulation.LieDownKind).Accepted,"setup: p1 lies down in the ring");
        Check(!StartsWithin(t,5),"someone lying down is busy, so nothing starts");
        Check(Act(t,"p1","Cancel").Accepted&&StartsWithin(t,3.5),"once they get up, it starts");
    }

    // While the game runs the level clock, bleed-out, effects, festivalgoers and their suspicion all hold still; nobody moves, and
    // every command but the game's own is refused, from players and watchers alike.
    static void TheClockStopsForEverything()
    {
        var s=Play(51,4);var watcher=s.Player("p2");watcher.Life="Spirit";watcher.X=24;watcher.Z=-20;
        var down=s.Player("p3");down.Life="Downed";down.DownedRemaining=30;s.Disconnect(down.Id);
        Ring(s,"p0","p1");Check(StartsWithin(s,3.5),"setup: the game starts, a spirit watching and a friend who left bleeding out");Endless(s);
        var tripper=s.Player(s.State.TripperId);var dose=tripper.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);
        var p=s.Player("p0");
        // Questioning p0 from 8 m away and out of sight of them for 6 s: in play it would walk over and cool off.
        var wook=new NpcState{Id="wook_watcher",X=p.X,Z=p.Z-8,Yaw=180};wook.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=60,LastSeenSeconds=s.State.SimulationSeconds-6,AccusationSeconds=-1});s.State.Npcs.Add(wook);
        double elapsed=s.State.ElapsedSeconds,bleed=down.DownedRemaining,effect=dose.RemainingSeconds;float wx=wook.X,wz=wook.Z;
        s.Tick(30);
        Check(Running(s),"thirty seconds in, the game still runs");
        Check(s.State.ElapsedSeconds==elapsed,"the level clock stops, moved "+(s.State.ElapsedSeconds-elapsed));
        Check(down.DownedRemaining==bleed&&down.Life=="Downed","bleed-out stops, moved "+(bleed-down.DownedRemaining));
        Check(dose.RemainingSeconds==effect,"effects stop, moved "+(effect-dose.RemainingSeconds));
        Check(wook.Observers[0].Suspicion==60&&wook.X==wx&&wook.Z==wz,"festivalgoers and their suspicion stop, got "+wook.Observers[0].Suspicion+" at ("+wook.X+", "+wook.Z+")");
        Check(!s.TryMove("p0",p.X+.1f,p.Z,0,.1),"nobody moves");
        // Cancel would otherwise be accepted with nothing to cancel, and a spirit's chime at the memorial too.
        var refusals=new List<string>();
        foreach(var kind in new[]{"Cancel","Drop","Dance","LieDown","Ready","DialogueAck","Talk"})
        {var result=Act(s,"p0",kind,item:"poi_practice");Check(!result.Accepted,kind+" is refused during the game");refusals.Add(result.Reason);}
        Check(refusals.Distinct().Count()==1,"each is refused because the game is on, got: "+string.Join(" | ",refusals.Distinct()));
        Check(!Act(s,"p2","Chime").Accepted&&watcher.ChimeUntil==0,"a spirit's memorial chime is refused too");
        Check(!Act(s,"p2",FestivalSimulation.MinigameKind).Accepted,"a spirit watches but can't play");
    }

    // A handoff offer's seconds stop with the clock: one with about 10 s left as a 60 s game starts has them all when it ends.
    static void AHandoffKeepsItsSecondsThroughAMinuteLongGame()
    {
        var s=Play(61,2);s.Player("p0").Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
        Check(Act(s,"p0","Transfer","p1","poi_practice",1).Accepted,"setup: p0 offers p1 their poi where the crew arrived");
        s.Tick(2);Ring(s,"p0","p1");Check(StartsWithin(s,3.5),"setup: the game starts");Endless(s);
        var offer=s.State.Transfers[0];double left=offer.ExpiresAt-s.State.SimulationSeconds;
        Check(left>9.5&&left<10.5,"setup: the offer has about 10 s left, got "+left);
        s.Tick(60);Check(Running(s)&&s.State.Transfers.Contains(offer),"a minute in, the game runs and the offer is still open");
        s.ScoreMinigame(0);Check(s.State.Booth.ScoredAt>=0,"the game ends, for +0 s");
        Check(ResumesWithin(s,FestivalSimulation.MinigameBannerSeconds+.5),"play resumes once the result has shown");
        Check(s.State.Transfers.Contains(offer)&&Same(offer.ExpiresAt-s.State.SimulationSeconds,left),"the offer still has its "+left.ToString("F2")+" s, got "+(offer.ExpiresAt-s.State.SimulationSeconds).ToString("F2"));
        s.Tick(left-.3);Check(s.State.Transfers.Contains(offer),"so it is still open just before they run out");
        s.Tick(.5);Check(!s.State.Transfers.Contains(offer),"and expires when they do");
    }

    // Every double on the round's state, by type and field, that is not a time on the simulation clock, and why. Every other
    // double is such a time: the pause moves it on, or this test fails naming it. Times at 0 or below are "never" or "not yet"
    // (AttackAt 0, ShowUntil 0, AccusationSeconds -1, LastTalkSeconds -100, ...) and stay put.
    static readonly Dictionary<string,string> NotSimulationTimes=new Dictionary<string,string>{
        {"RoundState.SimulationSeconds","the simulation clock itself"},{"RoundState.ElapsedSeconds","the level clock, stopped for the game"},
        {"RoundState.DurationSeconds","the level's length"},{"RoundState.CloudClueStart","on the level clock"},
        {"RoundState.LaunchAtSeconds","camp's ready countdown, over before a level plays"},{"RoundState.SpinEndsAt","the spin, over before a level plays"},
        {"PlayerState.DownedRemaining","a countdown that runs only in play"},{"PlayerState.LastRhythmScore","a score"},
        {"PlayerState.VisualWheelSince","view-only: ViewFor copies the ride's StartSeconds"},{"ActiveEffect.RemainingSeconds","a countdown that runs only in play"},
        {"ObserverState.Suspicion","suspicion"},{"NpcState.Suspicion","suspicion"},
        {"InteractionState.DurationSeconds","a length"},{"InteractionState.GoodWindowSeconds","a length"},{"InteractionState.Score","a score"},{"InteractionState.BeatSeconds","a length"},
        {"RhythmInput.TimeSeconds","seconds into its interaction"},
        {"MinigameState.RingSeconds","the ring's own countdown"},{"MinigameState.Seconds","the game's own clock, the pause's length"},
        {"MinigameState.ScoredAt","on the game's own clock"},{"MinigameState.ClockChange","seconds paid to the level's length"},
        {"MinigameState.TurnAt","Mystery Cups: on the game's own clock"},
    };
    static bool StateType(Type t)=>t.Namespace=="Festival.Core"&&t.IsClass&&t.IsSerializable;
    // Every double field reachable from o, filling each empty list of state with one fresh entry so that its fields are reached.
    static void Doubles(object o,List<(object Owner,FieldInfo Field)> found)
    {
        foreach(var f in o.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))
        {
            var value=f.GetValue(o);
            if(f.FieldType==typeof(double))found.Add((o,f));
            else if(value is IList list&&f.FieldType.IsGenericType&&StateType(f.FieldType.GetGenericArguments()[0])){if(list.Count==0)list.Add(Activator.CreateInstance(f.FieldType.GetGenericArguments()[0]));foreach(var item in list)Doubles(item,found);}
            else if(value!=null&&StateType(f.FieldType))Doubles(value,found);
        }
    }
    static string Name(FieldInfo f)=>f.DeclaringType.Name+"."+f.Name;
    // A game that starts, has every simulation time on the state set to `at`, and ends; each time's value after the pause.
    static List<(string Name,double After,double Pause)> PauseWith(double at)
    {
        var s=Started(71,2);var found=new List<(object Owner,FieldInfo Field)>();Doubles(s.State,found);
        var times=found.FindAll(x=>!NotSimulationTimes.ContainsKey(Name(x.Field)));
        foreach(var x in times)x.Field.SetValue(x.Owner,at);
        s.Tick(6);Check(CallIt(s).Accepted&&ResumesWithin(s,FestivalSimulation.MinigameBannerSeconds+.5),"setup: the game ends and play resumes");
        return times.ConvertAll(x=>(Name(x.Field),(double)x.Field.GetValue(x.Owner),s.State.Booth.Seconds));
    }
    static void EveryClockDeadlineMovesOnByThePause()
    {
        var reached=new List<(object Owner,FieldInfo Field)>();Doubles(Started(70,2).State,reached);var names=new HashSet<string>(reached.ConvertAll(x=>Name(x.Field)));
        foreach(var listed in NotSimulationTimes.Keys)Check(names.Contains(listed),listed+" is listed as no simulation time but is no double on the state: drop it from the list");
        var moved=PauseWith(100);var stuck=moved.FindAll(x=>!Same(x.After,100+x.Pause)).ConvertAll(x=>x.Name+" ("+x.After+")").Distinct().ToList();
        Check(moved.Count>0&&moved[0].Pause>5,"setup: the pause lasted a few seconds, got "+(moved.Count>0?moved[0].Pause:0));
        Check(stuck.Count==0,"every time on the simulation clock moves on by the pause ("+moved[0].Pause.ToString("F2")+" s from 100); these did not, so shift them in Minigames.cs or list why they are no such time: "+string.Join(", ",stuck));
        foreach(double never in new[]{0,-1,-100})
        {
            var kept=PauseWith(never).FindAll(x=>x.After!=never).ConvertAll(x=>x.Name+" ("+x.After+")").Distinct().ToList();
            Check(kept.Count==0,"a time of "+never+" means never or not yet, and stays put: "+string.Join(", ",kept));
        }
    }

    // A game nobody ends is scored at 90 s and its result shows for 4 s before play resumes. Mystery Cups, called by the player
    // up, scores +0 s the same way.
    static void TheGameEndsAtNinetySecondsAndItsResultShowsForFour()
    {
        var s=Started(81,2);Endless(s);double left=Left(s);
        s.Tick(60);s.Tick(FestivalSimulation.MinigameMaxSeconds-60.2);Check(Running(s)&&s.State.Booth.ScoredAt<0,"just before 90 s the game still runs, unscored");
        s.Tick(.3);Check(Running(s)&&s.State.Booth.ScoredAt>=FestivalSimulation.MinigameMaxSeconds-1e-6&&s.State.Booth.ScoredAt<FestivalSimulation.MinigameMaxSeconds+.15,"at 90 s the host ends it and scores what's in, at "+s.State.Booth.ScoredAt);
        Check(!Act(s,"p0",FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups).Accepted,"a scored game takes no more moves");
        s.Tick(FestivalSimulation.MinigameBannerSeconds-.3);Check(Running(s),"its result shows for 4 s, the world still stopped");
        Check(ResumesWithin(s,.5),"then play resumes");
        Check(Same(Left(s),left)&&s.State.Booth.ClockChange==0,"the cap pays +0 s, left "+Left(s)+" of "+left);
        var t=Started(82,2);t.Tick(5);string up=FestivalSimulation.CupsTurn(t.State);
        Check(CallIt(t).Accepted&&Same(t.State.Booth.ScoredAt,t.State.Booth.Seconds),"the player up calls Mystery Cups at once");
        Check(!Act(t,up,FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups).Accepted,"only once");
        Check(!ResumesWithin(t,FestivalSimulation.MinigameBannerSeconds-.3)&&ResumesWithin(t,.5),"its result shows for 4 s, then play resumes");
        var u=Play(83,1);Check(!Act(u,"p0",FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups).Accepted,"with no game running there is nothing to play");
    }

    // A game's result changes the level's length: a gain in full (and the tripper's dose, which lasts the level, with it), a loss
    // never below 15 s left, and never below what was left already.
    static void ALossNeverLeavesUnderFifteenSeconds()
    {
        var s=Started(91,2);s.State.ElapsedSeconds=s.State.DurationSeconds-40;s.ScoreMinigame(-30);
        Check(Same(Left(s),15)&&Same(s.State.Booth.ClockChange,-25),"-30 s with 40 s left leaves 15 s, got "+Left(s)+" (change "+s.State.Booth.ClockChange+")");
        var t=Started(92,2);t.State.ElapsedSeconds=t.State.DurationSeconds-10;t.ScoreMinigame(-30);
        Check(Same(Left(t),10)&&t.State.Booth.ClockChange==0,"-30 s with 10 s left takes nothing, got "+Left(t));
        var u=Started(93,2);u.State.ElapsedSeconds=u.State.DurationSeconds-100;u.ScoreMinigame(-30);
        Check(Same(Left(u),70)&&u.State.Booth.ClockChange==-30,"-30 s with 100 s left takes it all, got "+Left(u));
        var v=Started(94,2);double left=Left(v);var dose=v.Player(v.State.TripperId).Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);double lasts=dose.RemainingSeconds;
        v.ScoreMinigame(45);
        Check(Same(Left(v),left+45)&&v.State.Booth.ClockChange==45,"+45 s adds it all, got "+(Left(v)-left));
        Check(Same(dose.RemainingSeconds,lasts+45),"and the dose, which lasts the level, lasts the 45 s more too, got "+(dose.RemainingSeconds-lasts));
        Check(ResumesWithin(v,FestivalSimulation.MinigameBannerSeconds+.5)&&Same(Left(v),left+45),"the gain holds as play resumes");
    }

    // A booth plays once: afterwards it is gone, and a crew standing where it was starts nothing.
    static void ThePlayedBoothIsSpent()
    {
        var s=Started(101,2);var at=FestivalSimulation.BoothAt(s.State);
        Check(CallIt(s).Accepted&&ResumesWithin(s,FestivalSimulation.MinigameBannerSeconds+.5),"setup: the game ends");
        Check(s.State.Booth.Spent&&FestivalSimulation.BoothAt(s.State)==null,"the booth is spent and gone");
        foreach(var p in s.State.Players){p.X=at.X;p.Z=at.Z;}
        Check(!StartsWithin(s,10),"the crew standing where it was starts nothing more");
    }

    // A snapshot keeps the booth, and one taken mid-game resumes it; one from before booths has none, so nothing starts.
    static void SnapshotsKeepTheBoothAndOlderOnesHaveNone()
    {
        var s=Started(111,2);s.Tick(5);var json=JsonSerializer.SerializeToNode(s.State,Json).AsObject();
        var saved=new FestivalSimulation();saved.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        Check(saved.State.Phase==FestivalSimulation.MinigamePhase&&saved.State.Booth.Spot==s.State.Booth.Spot&&saved.State.Booth.Kind==s.State.Booth.Kind&&Same(saved.State.Booth.Seconds,s.State.Booth.Seconds)&&saved.State.Booth.PlayerIds.Count==2,"a snapshot keeps the game under way");
        Check(CallIt(saved).Accepted&&ResumesWithin(saved,FestivalSimulation.MinigameBannerSeconds+.5)&&saved.State.Booth.Spent,"and it plays on to the end");
        var p=Play(112,2);var spots=Festivals.BoothSpots;var legacy=JsonSerializer.SerializeToNode(p.State,Json).AsObject();legacy.Remove("Booth");legacy.Remove("MinigameBag");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.Booth!=null&&old.State.Booth.Spot==-1&&FestivalSimulation.BoothAt(old.State)==null&&old.State.MinigameBag!=null,"a snapshot from before booths restores with none");
        foreach(var spot in spots){foreach(var q in old.State.Players){q.X=spot.X;q.Z=spot.Z;}Check(!StartsWithin(old,4),"so nothing starts at ("+spot.X+", "+spot.Z+")");}
    }

    // The world a client draws on the simulation clock (the Ferris wheel) runs on WorldSeconds: the simulation clock until a game,
    // standing still while one runs, then running on from where it stopped. A time stamped on the simulation clock reads the same
    // on it before the game, during it (not yet moved on) and after it (moved on by the pause); one stamped later reads as far on.
    static void TheWorldsClockStandsStillThroughAGame()
    {
        var s=Play(121,2);s.Tick(2);var p=s.Player("p0");p.SprintUntil=s.State.SimulationSeconds+4;
        double sprint=FestivalSimulation.WorldSeconds(s.State,p.SprintUntil);
        Check(Same(FestivalSimulation.WorldSeconds(s.State),s.State.SimulationSeconds)&&Same(sprint,s.State.SimulationSeconds+4),"before a game it is the simulation clock");
        Ring(s,"p0","p1");Check(StartsWithin(s,3.5),"setup: the game starts");
        double stopped=FestivalSimulation.WorldSeconds(s.State);
        s.Tick(7);
        Check(Same(FestivalSimulation.WorldSeconds(s.State),stopped),"it stands still while the game runs, moved "+(FestivalSimulation.WorldSeconds(s.State)-stopped));
        Check(Same(FestivalSimulation.WorldSeconds(s.State,p.SprintUntil),sprint),"a time stamped before the game reads the same during it, got "+(FestivalSimulation.WorldSeconds(s.State,p.SprintUntil)-sprint)+" off");
        Check(CallIt(s).Accepted&&ResumesWithin(s,FestivalSimulation.MinigameBannerSeconds+.5),"setup: the game ends and play resumes");
        Check(Same(FestivalSimulation.WorldSeconds(s.State),stopped),"play resumes on it where the game stopped it, "+(FestivalSimulation.WorldSeconds(s.State)-stopped)+" off");
        Check(Same(FestivalSimulation.WorldSeconds(s.State,p.SprintUntil),sprint),"a time stamped before the game, moved on by the pause, reads the same after it, got "+(FestivalSimulation.WorldSeconds(s.State,p.SprintUntil)-sprint)+" off");
        s.Tick(2);
        Check(Same(FestivalSimulation.WorldSeconds(s.State),stopped+2),"then it runs on with play");
        p.SprintUntil=s.State.SimulationSeconds+4;
        Check(Same(FestivalSimulation.WorldSeconds(s.State,p.SprintUntil),FestivalSimulation.WorldSeconds(s.State)+4),"a time stamped after the game reads as far on as it is");
    }
}
