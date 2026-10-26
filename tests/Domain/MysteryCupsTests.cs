using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// MINI-4: Mystery Cups, a minigame booth's game (docs/MINIGAMES_2026-09-30.md). A shady bartender sets out two cups more than the
// crew, at least 4, one spiked by seed. Turns go round the players in a seeded order, 10 s each: drink a cup or call it, and a turn
// that runs out calls it. The nth safe cup adds $5 × n to the pot. The spiked cup loses the pot and gives the drinker a dose for the
// rest of the level, or one stronger, up to 4, without touching the spin's doses, so the tripper's visions stay as they are. Calling
// it pays the pot as sales: by day split across the players' cash, toward the quota; by night into the stash. Once only the spiked
// cup is left, the game calls it. Only the tripper is shown a hunch cup, right at their vision reliability, one step less when
// the tripper plays alone, and the first turn's clock starts after the card's 3 s rules intro.
public static class MysteryCupsTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Mystery Cups: "+message);}
    static bool Running(FestivalSimulation s)=>s.State.Phase==FestivalSimulation.MinigamePhase;
    static CommandResult Act(FestivalSimulation s,string id,string move,int cup=0)=>s.Execute(id,new GameCommand{Id="cups"+(sequence++),Kind=FestivalSimulation.MinigameKind,ItemId=move,Amount=cup});
    // The player whose turn it is drinks a cup, or calls it.
    static CommandResult Drink(FestivalSimulation s,int cup)=>Act(s,FestivalSimulation.CupsTurn(s.State),FestivalSimulation.DrinkCup,cup);
    static CommandResult Call(FestivalSimulation s)=>Act(s,FestivalSimulation.CupsTurn(s.State),FestivalSimulation.CallCups);
    // A cup still on the table that isn't the spiked one (the host knows which that is).
    static int Safe(FestivalSimulation s){var b=s.State.Booth;for(int i=0;i<b.Cups;i++)if(i!=b.Spiked&&!b.Drunk.Contains(i))return i;return -1;}
    // The walk of a player with only this dose: Couch Lock and Rolly Pollies change it, so compare per substance.
    static float DosedWalk(ActiveEffect dose)=>Intoxication.MovementMultiplier(new PlayerState{Effects={new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose.Intensity,Substance=dose.Substance}}});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{OneCupIsSpikedBySeedAmongTwoMoreThanTheCrew,TurnsGoRoundInASeededOrder,ATurnThatRunsOutCallsIt,
            ThePotGrowsByFiveDollarsTimesTheCupsDrunk,TheSpikedCupLosesThePotAndDosesTheDrinker,CallingItPaysTheCrewByDayAndTheStashByNight,
            TheLastSafeCupCallsIt,TheCapCallsIt,TheFirstTurnStartsAfterTheRulesIntro,TheHintIsRightAtTheTrippersReliability,ASoloHunchIsOneStepLessSure,ADetainedTrippersHunchKeepsTheCrewsOdds,OnlyTheTrippersViewIsShownTheHint,ASnapshotKeepsTheCupsGame,AVisibleBoothCannotRewriteHostOrAnotherViewer})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" Mystery Cups test(s) failed:\n"+string.Join("\n",failures));
    }

    static void AVisibleBoothCannotRewriteHostOrAnotherViewer()
    {
        var s=Poured(1201,3);Check(Drink(s,Safe(s)).Accepted,"setup: safe cup");
        string before=JsonSerializer.Serialize(s.State.Booth,Json);var other=FestivalSimulation.VisibleBooth(s.State,s.State.TripperId);
        string otherBefore=JsonSerializer.Serialize(other,Json);var view=FestivalSimulation.VisibleBooth(s.State,"p1");
        view.PlayerIds.Clear();view.Turns.Clear();view.Drunk.Clear();
        Check(JsonSerializer.Serialize(s.State.Booth,Json)==before,"a detached public booth cannot rewrite host lists or secrets");
        Check(JsonSerializer.Serialize(other,Json)==otherBefore,"one viewer cannot rewrite another viewer's booth");
    }

    // A crew of `crew` readies up and the wheels spin; level 0 is Day 1 and 1 Night 1. `deal` is the host's secret deal seed.
    static FestivalSimulation Leave(int seed,int crew,int level,int deal)
    {
        var s=new FestivalSimulation(seed){DealSecret=()=>deal};for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.LevelIndex=level;s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(s.Execute(p.Id,new GameCommand{Id="ready"+(sequence++),Kind="Ready"}).Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);Check(s.State.Phase=="Spinning","setup: the crew leaves camp");return s;
    }
    // The crew at the festival with the crowd cleared, its booth playing Mystery Cups: everyone stands in the ring and the cups are
    // poured.
    static FestivalSimulation Poured(int seed,int crew=2,int level=0,int deal=0)
    {
        var s=Leave(seed,crew,level,deal);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);
        foreach(var p in s.State.Players)s.Execute(p.Id,new GameCommand{Id="map"+(sequence++),Kind="MapReady"});
        Check(s.State.Phase=="Playing","setup: the crew is at the festival");s.State.Npcs.Clear();
        var at=FestivalSimulation.BoothAt(s.State);Check(at!=null,"setup: the level has a booth");s.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;
        for(int k=0;k<crew;k++){var p=s.State.Players[k];p.X=at.X+k-.5f*(crew-1);p.Z=at.Z;}
        for(double t=0;t<3.5&&!Running(s);t+=.1)s.Tick(.1);
        Check(Running(s),"setup: the crew starts the booth's game");return s;
    }

    // The table holds two cups more than the crew, at least 4, and exactly one of them is spiked: the seed's pick, the same every
    // time for the same seed, any of them across seeds, and mixed with the host's secret deal seed, so no client can pour it again.
    static void OneCupIsSpikedBySeedAmongTwoMoreThanTheCrew()
    {
        foreach(var (crew,cups) in new[]{(1,4),(2,4),(3,5),(5,7),(8,10)})
        {
            var b=Poured(10+crew,crew).State.Booth;
            Check(b.Cups==cups&&FestivalSimulation.CupsFor(crew)==cups,"a crew of "+crew+" plays "+cups+" cups, got "+b.Cups);
            Check(b.Spiked>=0&&b.Spiked<b.Cups,"one of them is spiked, got cup "+b.Spiked+" of "+b.Cups);
            Check(b.Drunk.Count==0&&b.Pot==0&&b.SpikedBy=="","none drunk yet, an empty pot");
        }
        var first=Poured(31,3).State.Booth;var again=Poured(31,3).State.Booth;
        Check(first.Spiked==again.Spiked&&first.Hint==again.Hint&&string.Join(",",first.Turns)==string.Join(",",again.Turns),"the same seed pours the same cups");
        var spiked=new HashSet<int>();for(int seed=40;seed<100;seed++)spiked.Add(Poured(seed,2).State.Booth.Spiked);
        Check(spiked.SetEquals(new[]{0,1,2,3}),"any cup may be the spiked one, got "+string.Join(",",spiked.OrderBy(c=>c)));
        var dealt=new HashSet<int>();for(int deal=0;deal<24;deal++)dealt.Add(Poured(100,2,deal:deal*7919+1).State.Booth.Spiked);
        Check(dealt.SetEquals(new[]{0,1,2,3}),"the host's secret deal seed alone moves it to any cup, so the public seed can't pour it again, got "+string.Join(",",dealt.OrderBy(c=>c)));
    }

    // Turns go round every player in the game in a seeded order. Only the player whose turn it is moves; a safe cup passes the turn
    // on. Watchers, other players, a cup off the table or one already drunk, and any other move are refused.
    static void TurnsGoRoundInASeededOrder()
    {
        var firsts=new HashSet<string>();
        for(int seed=200;seed<230;seed++){var b=Poured(seed,3).State.Booth;Check(b.Turns.OrderBy(id=>id).SequenceEqual(new[]{"p0","p1","p2"}),"every player takes turns, got "+string.Join(",",b.Turns));firsts.Add(b.Turns[0]);}
        Check(firsts.Count==3,"in a seeded order: anyone may go first, got "+string.Join(",",firsts));
        var s=Poured(240,3);var turns=s.State.Booth.Turns;s.State.Players.Add(new PlayerState{Id="ghost",Name="Ghost",Life="Spirit"});
        Check(FestivalSimulation.CupsTurn(s.State)==turns[0],"the first in the order is up, got "+FestivalSimulation.CupsTurn(s.State));
        string other=turns[1];
        Check(!Act(s,other,FestivalSimulation.DrinkCup,Safe(s)).Accepted&&!Act(s,other,FestivalSimulation.CallCups).Accepted,"nobody else drinks or calls it");
        Check(!Act(s,"ghost",FestivalSimulation.DrinkCup,Safe(s)).Accepted,"a watching spirit can't");
        Check(!Drink(s,-1).Accepted&&!Drink(s,s.State.Booth.Cups).Accepted&&!Act(s,turns[0],"Dance").Accepted,"only a cup on the table, or calling it");
        int cup=Safe(s);Check(Drink(s,cup).Accepted,"the player up drinks a safe cup");
        Check(!Act(s,turns[0],FestivalSimulation.DrinkCup,Safe(s)).Accepted,"and their turn is over");
        Check(FestivalSimulation.CupsTurn(s.State)==turns[1]&&!Drink(s,cup).Accepted,"the next is up, and a drunk cup is gone from the table");
        Check(Drink(s,Safe(s)).Accepted&&FestivalSimulation.CupsTurn(s.State)==turns[2],"then the next");
        Check(Drink(s,Safe(s)).Accepted&&FestivalSimulation.CupsTurn(s.State)==turns[0],"and round again");
        Check(Running(s)&&s.State.Booth.ScoredAt<0,"the game runs on");
    }

    // Each turn has 10 s, from when it began. One that runs out calls it for the crew, paying the pot.
    static void ATurnThatRunsOutCallsIt()
    {
        var s=Poured(300,2);s.Tick(4);Check(Drink(s,Safe(s)).Accepted,"setup: a safe cup 4 s in");
        string up=FestivalSimulation.CupsTurn(s.State);int sales=s.State.LevelSales;
        s.Tick(FestivalSimulation.CupTurnSeconds-.3);
        Check(Running(s)&&s.State.Booth.ScoredAt<0&&FestivalSimulation.CupsTurn(s.State)==up,"the next turn has its own 10 s: still up just before they run out");
        s.Tick(.5);
        Check(s.State.Booth.ScoredAt>=0&&s.State.Booth.SpikedBy=="","a turn that runs out calls it");
        Check(s.State.LevelSales==sales+5,"and pays the pot, got $"+(s.State.LevelSales-sales));
        Check(FestivalSimulation.CupsTurn(s.State)==""&&!Act(s,up,FestivalSimulation.DrinkCup,Safe(s)).Accepted,"nobody is up any more");
    }

    // The first safe cup adds $5, the second $10, the third $15.
    static void ThePotGrowsByFiveDollarsTimesTheCupsDrunk()
    {
        var s=Poured(400,3);var b=s.State.Booth;
        foreach(var pot in new[]{5,15,30})
        {
            int before=b.Pot,pays=FestivalSimulation.NextCupPays(b);Check(Drink(s,Safe(s)).Accepted,"setup: a safe cup");
            Check(b.Pot==pot&&pays==pot-before,"the pot reads $"+pot+", got $"+b.Pot+" (the cup was to pay $"+pays+")");
        }
    }

    // The spiked cup ends the game and the pot is lost: nobody is paid. Its drinker gets a dose for the rest of the level, the
    // substance rolled from its own stream, and walks like any player on that dose; one already dosed takes one stronger, up to 4. The
    // spin's doses, and so the tripper's visions, stay as they were.
    static void TheSpikedCupLosesThePotAndDosesTheDrinker()
    {
        var s=Poured(500,3);var b=s.State.Booth;
        while(FestivalSimulation.CupsTurn(s.State)==s.State.TripperId)Check(Drink(s,Safe(s)).Accepted,"setup: the tripper drinks a safe cup");
        Check(Drink(s,Safe(s)).Accepted,"setup: a safe cup");
        if(FestivalSimulation.CupsTurn(s.State)==s.State.TripperId)Check(Drink(s,Safe(s)).Accepted,"setup: the tripper passes");
        var drinker=s.Player(FestivalSimulation.CupsTurn(s.State));
        Check(drinker.Id!=s.State.TripperId&&!drinker.Effects.Exists(e=>e.Id==FestivalSimulation.DoseEffect),"setup: a sober friend is up, the pot at $"+b.Pot);
        int pot=b.Pot;string doses=JsonSerializer.Serialize(s.State.Doses,Json),visions=JsonSerializer.Serialize(s.State.Visions,Json);
        string money=string.Join(",",s.State.Players.Select(p=>p.Cash))+"|"+s.State.LevelSales+"|"+s.State.GrossSales+"|"+s.State.StashCash;
        Check(Drink(s,b.Spiked).Accepted,"they drink the spiked cup");
        Check(b.ScoredAt>=0&&b.SpikedBy==drinker.Id&&b.ClockChange==0,"which ends the game, theirs the spiked cup");
        Check(string.Join(",",s.State.Players.Select(p=>p.Cash))+"|"+s.State.LevelSales+"|"+s.State.GrossSales+"|"+s.State.StashCash==money&&b.Pot==pot,"the $"+pot+" pot is lost: nobody is paid");
        var dose=drinker.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);
        Check(dose!=null&&dose.Intensity==1&&FestivalSimulation.Substances.Contains(dose.Substance),"the drinker is dosed, got "+(dose==null?"nothing":dose.Intensity+" of '"+dose.Substance+"'"));
        Check(Intoxication.MovementMultiplier(drinker)==DosedWalk(dose),"and walks like any player on one dose of '"+dose.Substance+"'");
        Check(JsonSerializer.Serialize(s.State.Doses,Json)==doses&&JsonSerializer.Serialize(s.State.Visions,Json)==visions,"the spin's doses and the tripper's visions stay as they were");
        for(double t=0;t<5&&Running(s);t+=.1)s.Tick(.1);
        Check(s.State.Phase=="Playing","setup: play resumes");s.Tick(30);
        Check(drinker.Effects.Exists(e=>e.Id==FestivalSimulation.DoseEffect&&e.RemainingSeconds>=s.State.DurationSeconds-s.State.ElapsedSeconds),"the dose lasts the rest of the level");
        var substances=new HashSet<string>();
        for(int seed=510;seed<540;seed++)
        {
            var t=Poured(seed,2);
            while(FestivalSimulation.CupsTurn(t.State)==t.State.TripperId)Check(Drink(t,Safe(t)).Accepted,"setup: the tripper drinks a safe cup");
            var sober=t.Player(FestivalSimulation.CupsTurn(t.State));
            Check(Drink(t,t.State.Booth.Spiked).Accepted,"setup: a sober friend drinks the spiked cup");
            var rolled=sober.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);substances.Add(rolled?.Substance??"none");
            Check(rolled!=null&&Intoxication.MovementMultiplier(sober)==DosedWalk(rolled),"seed "+seed+": the drinker walks like any player on one dose of '"+rolled?.Substance+"'");
        }
        Check(substances.Count>=3,"its substance is rolled, got "+string.Join(",",substances));
        Check(substances.Contains("ecstasy")&&substances.Contains("weed"),"setup: Rolly Pollies and Couch Lock, whose walks differ, are among them, got "+string.Join(",",substances));

        var u=Poured(550,2);var tripper=u.Player(u.State.TripperId);var spun=tripper.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);
        int was=spun.Intensity,listed=u.State.Doses.Find(d=>d.PlayerId==tripper.Id).Dose;
        while(FestivalSimulation.CupsTurn(u.State)!=tripper.Id)Check(Drink(u,Safe(u)).Accepted,"setup: a safe cup until the tripper is up");
        Check(Drink(u,u.State.Booth.Spiked).Accepted&&spun.Intensity==Math.Min(4,was+1),"a dosed drinker takes one stronger, "+was+" to "+spun.Intensity);
        Check(tripper.Effects.Count(e=>e.Id==FestivalSimulation.DoseEffect)==1&&u.State.Doses.Find(d=>d.PlayerId==tripper.Id).Dose==listed,"the same dose, stronger; the spin's dose stays "+listed);
        var v=Poured(551,1);var maxed=v.Player("p0").Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);maxed.Intensity=4;
        Check(Act(v,"p0",FestivalSimulation.DrinkCup,v.State.Booth.Spiked).Accepted&&maxed.Intensity==4,"never past 4, got "+maxed.Intensity);
    }

    // Calling it pays the pot as sales, toward the day's quota. By day it is split evenly across the players' cash, any odd dollars
    // to the stash; by night it all goes to the stash. An empty pot pays nothing. The clock is unchanged.
    static void CallingItPaysTheCrewByDayAndTheStashByNight()
    {
        var s=Poured(600,2);Check(Drink(s,Safe(s)).Accepted&&Drink(s,Safe(s)).Accepted&&s.State.Booth.Pot==15,"setup: two safe cups, $15 in the pot");
        s.State.LevelSales=FestivalSimulation.DayQuota(s.State)-10;int sales=s.State.LevelSales,gross=s.State.GrossSales,stash=s.State.StashCash;
        var cash=s.State.Players.ConvertAll(p=>p.Cash);double left=s.State.DurationSeconds-s.State.ElapsedSeconds;
        Check(!FestivalSimulation.DayQuotaMet(s.State),"setup: $10 short of the day's quota");
        Check(Call(s).Accepted&&s.State.Booth.ScoredAt>=0,"the player up calls it");
        Check(s.State.Players.ConvertAll(p=>p.Cash).SequenceEqual(cash.ConvertAll(c=>c+7))&&s.State.StashCash==stash+1,"by day $15 splits $7 each, the odd $1 to the stash");
        Check(s.State.LevelSales==sales+15&&s.State.GrossSales==gross+15&&FestivalSimulation.DayQuotaMet(s.State),"and counts as sales: the quota is met");
        Check(s.State.Booth.ClockChange==0&&s.State.DurationSeconds-s.State.ElapsedSeconds==left,"the clock is unchanged");

        var n=Poured(610,2,level:1);Check(Festivals.For(n.State).Night,"setup: Night 1");
        Check(Drink(n,Safe(n)).Accepted,"setup: a safe cup");
        cash=n.State.Players.ConvertAll(p=>p.Cash);stash=n.State.StashCash;sales=n.State.LevelSales;gross=n.State.GrossSales;
        Check(Call(n).Accepted&&n.State.StashCash==stash+5&&n.State.Players.ConvertAll(p=>p.Cash).SequenceEqual(cash),"by night the $5 goes to the stash");
        Check(n.State.LevelSales==sales+5&&n.State.GrossSales==gross+5,"still counted as sales");

        var e=Poured(620,2);sales=e.State.LevelSales;stash=e.State.StashCash;
        Check(Call(e).Accepted&&e.State.Booth.ScoredAt>=0&&e.State.LevelSales==sales&&e.State.StashCash==stash,"calling an empty pot pays nothing");
    }

    // When only the spiked cup is left, the game calls it: a solo player's three safe cups pay $5 + $10 + $15.
    static void TheLastSafeCupCallsIt()
    {
        var s=Poured(700,1);var me=s.Player("p0");int cash=me.Cash;
        for(int k=0;k<2;k++)Check(Drink(s,Safe(s)).Accepted&&s.State.Booth.ScoredAt<0,"setup: safe cup "+(k+1)+" of 3");
        Check(Drink(s,Safe(s)).Accepted,"the last safe cup");
        Check(s.State.Booth.ScoredAt>=0&&s.State.Booth.SpikedBy==""&&me.Cash==cash+30,"only the spiked cup is left, so the game calls it and pays $30, got $"+(me.Cash-cash));
    }

    // At the minigame cap (90 s) the host calls it, paying what's in the pot.
    static void TheCapCallsIt()
    {
        var s=Poured(800,2);Check(Drink(s,Safe(s)).Accepted,"setup: $5 in the pot");
        var b=s.State.Booth;b.Seconds=FestivalSimulation.MinigameMaxSeconds-1;b.TurnAt=b.Seconds-1;int sales=s.State.LevelSales;
        s.Tick(.8);Check(b.ScoredAt<0,"setup: the game runs just before the cap");
        s.Tick(.4);Check(b.ScoredAt>=0&&s.State.LevelSales==sales+5,"at the cap it is called and the pot paid, got $"+(s.State.LevelSales-sales));
    }

    // The card reads the rules for 3 s before the first turn's clock starts, so the first player up has the intro and then
    // a whole turn. They may still drink or call it during the intro.
    static void TheFirstTurnStartsAfterTheRulesIntro()
    {
        var s=Poured(320,2);string first=FestivalSimulation.CupsTurn(s.State);var b=s.State.Booth;
        Check(FestivalSimulation.CupsIntroSeconds==3&&Math.Abs(b.TurnAt-b.Seconds-3)<1e-9,"the first turn's clock starts 3 s after the pour, got "+(b.TurnAt-b.Seconds)+" s");
        s.Tick(3+FestivalSimulation.CupTurnSeconds-.3);
        Check(Running(s)&&s.State.Booth.ScoredAt<0&&FestivalSimulation.CupsTurn(s.State)==first,"the first turn has the 3 s intro and its own 10 s: still up just before they run out");
        s.Tick(.5);Check(s.State.Booth.ScoredAt>=0,"then it runs out and calls it");
        var t=Poured(321,2);Check(Drink(t,Safe(t)).Accepted,"the player up may drink during the intro");
    }

    // The tripper is shown one cup as spiked: the spiked cup at their vision reliability for their spun dose (75 / 50 / 25 / 10%),
    // and otherwise a safe cup.
    static void TheHintIsRightAtTheTrippersReliability()
    {
        var s=Poured(900,2);var b=s.State.Booth;var spun=s.State.Doses.Find(d=>d.PlayerId==s.State.TripperId);
        foreach(var (dose,reliability) in new[]{(1,.75),(2,.50),(3,.25),(4,.10)})
        {
            spun.Dose=dose;Check(FestivalSimulation.VisionReliability(s.State)==reliability&&FestivalSimulation.HunchReliability(s.State)==reliability,"dose "+dose+"'s visions, and a crew's hunch, are true "+reliability+" of the time");
            int right=0,trials=600;
            for(int k=0;k<trials;k++)
            {
                s.State.DealSeed=1000+k;s.PourCups();
                Check(b.Hint>=0&&b.Hint<b.Cups,"the hint marks a cup on the table, got "+b.Hint);
                if(b.Hint==b.Spiked)right++;
            }
            double rate=(double)right/trials;
            Check(Math.Abs(rate-reliability)<.06,"at dose "+dose+" the hint is right "+reliability+" of the time, got "+rate.ToString("F3"));
        }
    }

    // A tripper alone at the table has nobody to argue the hunch over, so it is one reliability step less sure than their visions
    // (50 / 25 / 10 / 10% by dose), and a lone player cannot skip the marked cup for a free pot. A crew game keeps the visions' odds.
    static void ASoloHunchIsOneStepLessSure()
    {
        var s=Poured(950,1);var b=s.State.Booth;var spun=s.State.Doses.Find(d=>d.PlayerId==s.State.TripperId);
        Check(b.PlayerIds.Count==1&&s.State.TripperId=="p0","setup: a solo game, its player the tripper");
        foreach(var (dose,reliability) in new[]{(1,.50),(2,.25),(3,.10),(4,.10)})
        {
            spun.Dose=dose;int right=0,trials=600;
            Check(FestivalSimulation.HunchReliability(s.State)==reliability,"solo at dose "+dose+" the card names "+reliability+", got "+FestivalSimulation.HunchReliability(s.State));
            for(int k=0;k<trials;k++){s.State.DealSeed=2000+k;s.PourCups();if(b.Hint==b.Spiked)right++;}
            double rate=(double)right/trials;
            Check(Math.Abs(rate-reliability)<.06,"solo at dose "+dose+" the hunch is right "+reliability+" of the time, got "+rate.ToString("F3"));
        }
    }

    // A busted tripper waits in the security building while the friend alone at the table plays. The tripper is not at the
    // table, so it is no solo game: the hunch keeps the visions' odds (75 / 50 / 25 / 10% by dose) and still goes to the tripper alone.
    static void ADetainedTrippersHunchKeepsTheCrewsOdds()
    {
        var s=Leave(960,2,0,0);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);
        foreach(var p in s.State.Players)s.Execute(p.Id,new GameCommand{Id="map"+(sequence++),Kind="MapReady"});
        Check(s.State.Phase=="Playing","setup: the crew is at the festival");s.State.Npcs.Clear();
        var at=FestivalSimulation.BoothAt(s.State);s.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;
        var tripper=s.Player(s.State.TripperId);var friend=s.State.Players.Find(p=>p.Id!=s.State.TripperId);
        // What Danger.Detain leaves on a bust: no interaction, Life "Detained", held in the security building.
        tripper.Life="Detained";tripper.InteractionId="";tripper.X=Festivals.SecurityBuilding.CentreX;tripper.Z=Festivals.SecurityBuilding.CentreZ;
        friend.X=at.X;friend.Z=at.Z;
        for(double t=0;t<3.5&&!Running(s);t+=.1)s.Tick(.1);
        var b=s.State.Booth;var spun=s.State.Doses.Find(d=>d.PlayerId==tripper.Id);
        Check(Running(s)&&b.PlayerIds.Count==1&&b.PlayerIds[0]==friend.Id,"setup: the friend plays alone while the tripper is detained");
        foreach(var (dose,reliability) in new[]{(1,.75),(2,.50),(3,.25),(4,.10)})
        {
            spun.Dose=dose;int right=0,trials=600;
            Check(FestivalSimulation.HunchReliability(s.State)==reliability,"a detained tripper at dose "+dose+": the card names the crew's "+reliability+", got "+FestivalSimulation.HunchReliability(s.State));
            for(int k=0;k<trials;k++){s.State.DealSeed=3000+k;s.PourCups();if(b.Hint==b.Spiked)right++;}
            double rate=(double)right/trials;
            Check(Math.Abs(rate-reliability)<.06,"a detained tripper at dose "+dose+": the hunch is right "+reliability+" of the time, got "+rate.ToString("F3"));
        }
        Check(FestivalSimulation.VisibleBooth(s.State,tripper.Id).Hint==b.Hint&&FestivalSimulation.VisibleBooth(s.State,friend.Id).Hint==-1,"the hint still goes to the tripper alone");
    }

    // What each player's view carries of the cups: the spiked cup never; the hint only in the tripper's. The host keeps both.
    static void OnlyTheTrippersViewIsShownTheHint()
    {
        var s=Poured(1000,3);Check(Drink(s,Safe(s)).Accepted,"setup: a safe cup");var b=s.State.Booth;int spiked=b.Spiked,hint=b.Hint;
        foreach(var p in s.State.Players)
        {
            var seen=FestivalSimulation.VisibleBooth(s.State,p.Id);bool tripper=p.Id==s.State.TripperId;
            Check(seen.Spiked==-1,p.Id+"'s view never carries the spiked cup");
            Check(seen.Hint==(tripper?hint:-1),p.Id+(tripper?", the tripper, is shown the hint":" is not shown the hint")+", got "+seen.Hint);
            Check(seen.Cups==b.Cups&&seen.Pot==b.Pot&&seen.Drunk.SequenceEqual(b.Drunk)&&seen.Turns.SequenceEqual(b.Turns)&&seen.Turn==b.Turn&&seen.TurnAt==b.TurnAt&&seen.Kind==b.Kind,"and carries the rest of the game");
        }
        Check(FestivalSimulation.VisibleBooth(s.State,"").Hint==-1,"a view for nobody in particular has no hint");
        Check(b.Spiked==spiked&&b.Hint==hint,"the host's own game keeps both");
    }

    // A snapshot mid-game keeps the cups and plays on; one from before Mystery Cups restores with none poured; a snapshot with
    // its drunk cups or turns missing outright is refused, like every other list.
    static void ASnapshotKeepsTheCupsGame()
    {
        var s=Poured(1100,2);Check(Drink(s,Safe(s)).Accepted,"setup: a safe cup");var b=s.State.Booth;
        var saved=new FestivalSimulation();saved.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));var r=saved.State.Booth;
        Check(r.Cups==b.Cups&&r.Spiked==b.Spiked&&r.Hint==b.Hint&&r.Pot==5&&r.Drunk.SequenceEqual(b.Drunk)&&r.Turns.SequenceEqual(b.Turns)&&r.Turn==1&&r.TurnAt==b.TurnAt,"a snapshot keeps the cups game");
        int sales=saved.State.LevelSales;Check(Call(saved).Accepted&&saved.State.LevelSales==sales+5,"and it plays on");
        var legacy=JsonSerializer.SerializeToNode(s.State,Json).AsObject();var booth=legacy["Booth"].AsObject();
        foreach(var field in new[]{"Cups","Turn","Pot","Spiked","Hint","TurnAt","SpikedBy","Drunk","Turns"})Check(booth.Remove(field),"setup: the booth has "+field);
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.Booth.Cups==0&&old.State.Booth.Spiked==-1&&old.State.Booth.Hint==-1&&old.State.Booth.Drunk.Count==0&&old.State.Booth.Turns.Count==0,"a snapshot from before Mystery Cups has none poured");
        foreach(var field in new[]{"Drunk","Turns"})
        {
            var broken=JsonSerializer.SerializeToNode(s.State,Json).AsObject();broken["Booth"].AsObject()[field]=null;
            bool refused=false;try{new FestivalSimulation().Restore(JsonSerializer.Deserialize<RoundState>(broken.ToJsonString(),Json));}catch(ArgumentException){refused=true;}
            Check(refused,"a snapshot with no "+field+" list is refused");
        }
    }
}
