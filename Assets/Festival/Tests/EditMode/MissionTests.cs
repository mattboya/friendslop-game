using System;
using Festival.Core;
public static class MissionTests
{
    static int sequence;
    static CommandResult Act(FestivalSimulation s,string p,string kind,string target="",string item="")=>s.Execute(p,new GameCommand{Id="mission"+(sequence++),Kind=kind,TargetId=target,ItemId=item});
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    public static void Run()
    {
        var s=new FestivalSimulation(3);var a=s.AddPlayer("a","A");var b=s.AddPlayer("b","B");
        s.State.Phase="Playing";s.State.Npcs.Clear();
        Check(s.State.DurationSeconds==480&&Festivals.For(s.State).Name=="Day 1","a new crew plays the eight minute Day 1");
        // The lost friend is a night rescue; days are the sales quota (DayQuotaTests).
        s.State.LevelIndex=1;
        Check(!Act(s,"a","FindFriend").Accepted,"cannot skip clues");
        a.X=-18;a.Z=-22;Check(Act(s,"a","ClueSupply").Accepted,"free voluntary perception");
        Check(a.Inventory.Count==0,"free tasting cannot be resold");
        Check(FestivalSimulation.ClueHint(s.State,a)!=""&&FestivalSimulation.ClueHint(s.State,b)=="","clue is private");
        for(int n=0;n<2;n++)
        {
            var point=FestivalSimulation.CluePoint(s.State.Seed,n);a.X=point.X;a.Z=point.Z;
            Check(!Act(s,"a","ReadClue").Accepted,"sober support required");
            b.X=a.X;b.Z=a.Z;Check(!Act(s,"b","ReadClue").Accepted,"sober cannot interpret");
            Check(Act(s,"a","ReadClue").Accepted,"pair reads clue");
            b.X=38;s.Tick(.1);Check(a.InteractionId==""&&s.State.CluesRead==n,"support must remain");
            b.X=a.X;Check(Act(s,"a","ReadClue").Accepted,"can retry clue");s.Tick(3.1);
            Check(s.State.CluesRead==n+1,"clue advances exactly once");b.X=-38;
        }
        var npc=new NpcState{Id="dance",X=a.X,Z=a.Z};s.State.Npcs.Add(npc);
        Check(Act(s,"a","Dance","dance").Accepted,"dance unlock challenge");
        var i=s.Interaction(a.InteractionId);var chart=RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds);
        foreach(var note in chart.Notes)i.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
        s.Tick(10);Check(s.State.GateOpened,"successful dance reveals friend");
        a.X=s.State.FriendPosition.X;a.Z=s.State.FriendPosition.Z;Act(s,"a","FindFriend");s.Tick(2.1);
        Check(s.State.ObjectiveReward==20&&s.State.StashCash==20,"objective reward committed once");
        a.X=b.X=0;a.Z=b.Z=-32;s.State.FriendPosition=new WorldPoint(0,-32);
        Check(Act(s,"a","Extract").Accepted,"extraction");s.Tick(3.1);
        Check(s.State.Survivors==2&&s.State.SurvivorBonus==10&&s.State.StashCash==30,"extra survivor bonus");
        s.Tick(1);Check(s.State.StashCash==30,"reward not duplicated");
        s=new FestivalSimulation();a=s.AddPlayer("a","A");b=s.AddPlayer("b","B");s.State.Phase="Playing";s.State.Npcs.Clear();
        a.Life=b.Life="Detained";s.Tick(.1);Check(s.State.Phase=="Playing","detained team can escape");
        for(int n=0;n<4;n++){Check(Act(s,"a","HelpSelf").Accepted,"active escape");if(n<3){Check(!Act(s,"a","HelpSelf").Accepted,"escape cooldown");s.Tick(4.1);}}
        Check(a.Life=="Alive","self escape completes");
        b.Life="Downed";b.DownedRemaining=30;
        Check(s.TryMove("b",b.X+.07f,b.Z,0,.1),"downed crawl");
        Check(!s.TryMove("b",b.X+1,b.Z,0,.1),"crawl speed enforced");
        Check(Act(s,"b","HelpSelf").Accepted,"downed distraction");
        Check(RhythmChart.Create(1,12,.4).DurationSeconds<RhythmChart.Create(1,12,.5).DurationSeconds,"optional faster rhythm");

        s=new FestivalSimulation(8);a=s.AddPlayer("a","A");b=s.AddPlayer("b","B");
        s.State.Phase="Playing";s.State.LevelIndex=1;s.State.Npcs.Clear();s.State.GateOpened=true;s.State.FriendFound=true;
        s.State.FriendLeaderId=a.Id;s.State.FriendPosition=new WorldPoint(10,10);
        s.State.ObjectiveReward=20;s.State.StashCash=20;
        s.Disconnect(a.Id);b.X=10;b.Z=10;
        Check(Act(s,b.Id,"FindFriend").Accepted,"new escort can reach stranded friend");
        s.Tick(2.1);
        Check(s.State.FriendLeaderId==b.Id,"escort transfers to living teammate");
        Check(s.State.ObjectiveReward==20&&s.State.StashCash==20,"escort handoff cannot duplicate reward");
        b.X=14;s.Tick(.5);
        Check(s.State.FriendPosition.X>10,"friend follows replacement escort");
        SoloFirstRunAndRecovery();
        Console.WriteLine("MISSION TESTS PASSED");
    }

    static void SoloFirstRunAndRecovery()
    {
        var game=new FestivalSimulation(5);var solo=game.AddPlayer("solo","Solo");
        var shelf=Catalog.ShopPoint(true,game.State.VendorOffers.IndexOf("stock_mushrooms"));solo.X=shelf.X;solo.Z=shelf.Z;
        Check(Act(game,solo.Id,"HoldOffer",item:"stock_mushrooms").Accepted,"solo takes camp item");
        solo.X=0;solo.Z=7;
        Check(game.Execute(solo.Id,new GameCommand{Id="solo-buy",Kind="Buy",ItemId="stock_mushrooms",Amount=1}).Accepted,"solo pays seller");
        solo.Z=19;Check(Act(game,solo.Id,"Ready").Accepted,"solo readies");game.Tick(5.2);
        Check(game.State.Phase=="Loading","solo countdown launches");
        Check(Act(game,solo.Id,"MapReady").Accepted&&game.State.Phase=="Playing","solo enters festival");
        game.State.Npcs.Clear();game.State.LevelIndex=1;solo.X=-18;solo.Z=-22;
        Check(Act(game,solo.Id,"ClueSupply").Accepted,"solo can take clue tasting");
        Check(Intoxication.MovementMultiplier(solo)<1&&Math.Abs(Intoxication.LateralDrift(solo,1))>.01,"intoxication changes walking");
        for(int index=0;index<2;index++)
        {
            var point=FestivalSimulation.CluePoint(game.State.Seed,index);solo.X=point.X;solo.Z=point.Z;
            Check(Act(game,solo.Id,"ReadClue").Accepted,"solo can interpret clue");
            game.Tick(6.1);Check(game.State.CluesRead==index+1,"solo clue completes");
        }
        game.State.Npcs.Add(new NpcState{Id="dancer",X=solo.X,Z=solo.Z});
        Check(Act(game,solo.Id,"Dance","dancer").Accepted,"solo starts dance");
        var challenge=game.Interaction(solo.InteractionId);
        foreach(var note in RhythmChart.Create(challenge.ChartSeed,challenge.NoteCount,challenge.BeatSeconds).Notes)challenge.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
        game.Tick(10);Check(game.State.GateOpened,"dance reveals friend");
        solo.Life="Downed";solo.DownedRemaining=.1;game.Tick(.2);
        Check(solo.Life=="Spirit"&&game.State.Phase=="Playing","solo death leaves recovery window");
        solo.X=24;solo.Z=-20;Check(Act(game,solo.Id,"BeginRevival","solo").Accepted,"solo starts medical revival");
        game.Tick(15.1);Check(solo.Life=="Alive"&&solo.RevivalCount==1,"solo revival completes");
        solo.X=game.State.FriendPosition.X;solo.Z=game.State.FriendPosition.Z;
        Check(Act(game,solo.Id,"FindFriend").Accepted,"solo recruits friend");game.Tick(2.1);
        solo.X=0;solo.Z=-32;game.State.FriendPosition=new WorldPoint(0,-32);
        Check(Act(game,solo.Id,"Extract").Accepted,"solo starts shuttle extraction");game.Tick(3.1);
        Check(game.State.Phase=="Results"&&game.State.Result=="Success","solo wins the timed night rescue");
    }
}
