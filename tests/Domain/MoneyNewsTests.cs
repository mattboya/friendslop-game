using System;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;

// Each change to a player's cash or carried items, and to the crew's stash or sales, leaves news with the amount for the
// players it affects (PlayerState.MoneyNewsLog): the player who did it reads their own line, the rest of the crew a STASH or
// SALES line. A player's own change never reaches a friend.
public static class MoneyNewsTests
{
    static int sequence;
    static void Check(bool condition,string message){if(!condition)throw new Exception("MoneyNews: "+message);}
    static CommandResult Act(FestivalSimulation game,string player,string kind,string target="",int amount=0)=>game.Execute(player,new GameCommand{Id="news"+(sequence++),Kind=kind,TargetId=target,Amount=amount});
    static object Call(FestivalSimulation game,string method,params object[] args)=>typeof(FestivalSimulation).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,args);
    static FestivalSimulation Play()
    {
        var game=new FestivalSimulation(12);game.DealSecret=()=>98765;
        foreach(var id in new[]{"host","friend"}){var p=game.AddPlayer(id,id=="host"?"Ash":"Sam");p.X=0;p.Z=19;Check(Act(game,id,"Ready").Accepted,"setup: ready");}
        game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
        foreach(var p in game.State.Players)Check(Act(game,p.Id,"MapReady").Accepted,"setup: map loads");
        Check(game.State.Phase=="Playing","setup: level plays");game.State.Npcs.Clear();
        foreach(var p in game.State.Players){p.MoneyNewsLog.Clear();p.Effects.Clear();}
        return game;
    }
    static string Last(PlayerState p)=>p.MoneyNewsLog.Count==0?"(none)":p.MoneyNewsLog[p.MoneyNewsLog.Count-1].Kind+": "+p.MoneyNewsLog[p.MoneyNewsLog.Count-1].Text;
    static void Heard(PlayerState p,string expected,string what){Check(Last(p)==expected,what+": expected '"+expected+"', got '"+Last(p)+"'");}
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{LostPropertyPaysWithNews,TheStashSaysWhoMovedWhat,DetentionNamesTheFineAndTheStock,APaidReleaseSaysSoAndACancelGivesItBack,
            AFailedBuyGivesThePriceBack,ACashPileSaysWhatItHeldButAFindKeepsItsOwnNotice,CrewWideFindsReachTheWholeCrew,ASaleTellsTheCrewWhatItMade,ADeckSetsTipsReachTheCrew,OnlyTheNewestNewsIsKept,
            CarriedItemsSayWhatMoved,HandoffsTellBothSides,SecurityTakingDitchedStockTellsTheCrew,UsedGearSaysWhatItSpent,TheCampStashAndTheNextLevelTellTheCrew,
            AFriendsWristbandDropsWithTheGearNotice})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" money news test(s) failed:\n"+string.Join("\n",failures));
    }
    static void LostPropertyPaysWithNews()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");int cash=ash.Cash;
        ash.X=-28;ash.Z=16;Check(Act(game,"host","LostProperty").Accepted,"setup: the task starts");game.Tick(5.1);
        Check(ash.Cash==cash+5,"the task pays $5");
        Heard(ash,"Cash: LOST PROPERTY RETURNED  •  +$5","the doer");
        Check(sam.MoneyNewsLog.Count==0,"a friend hears nothing of your pocket");
    }
    static void TheStashSaysWhoMovedWhat()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");
        game.State.Stashes.Add(new StashState{Id="box",X=ash.X,Z=ash.Z});
        Check(Act(game,"host","Deposit","box",5).Accepted,"setup: deposit");
        Heard(ash,"Cash: INTO THE STASH  •  −$5","the depositor");Heard(sam,"Stash: STASH  •  +$5  •  Ash","the crew");
        Check(Act(game,"host","Withdraw","box",5).Accepted,"setup: withdraw");
        Heard(ash,"Cash: FROM THE STASH  •  +$5","the withdrawer");Heard(sam,"Stash: STASH  •  −$5  •  Ash","the crew");
        game.Disconnect("friend");Check(Act(game,"host","Deposit","box",5).Accepted,"setup: deposit again");
        Heard(sam,"Stash: STASH  •  −$5  •  Ash","a friend who left hears nothing more");
    }
    static void DetentionNamesTheFineAndTheStock()
    {
        var game=Play();var ash=game.Player("host");ash.Cash=30;
        ash.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});ash.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
        Call(game,"Detain",ash);
        Check(ash.Life=="Detained"&&ash.Cash==20,"setup: detained with the $10 fine");
        Heard(ash,"Cash: SECURITY FINE  •  −$10  •  SECURITY TOOK 2 Tongue Stamps, 1 Fun Guys","the detainee");
        var broke=Play();var sam=broke.Player("friend");sam.Cash=0;Call(broke,"Detain",sam);
        Check(sam.MoneyNewsLog.Count==0,"nothing taken, nothing to say: the detention itself shows, got '"+Last(sam)+"'");
        broke=Play();sam=broke.Player("friend");sam.Cash=0;sam.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});Call(broke,"Detain",sam);
        Heard(sam,"Items: SECURITY TOOK 1 Tongue Stamps","a broke detainee loses only the stock");
    }
    static void APaidReleaseSaysSoAndACancelGivesItBack()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");sam.Life="Detained";ash.Cash=20;ash.X=27;ash.Z=5;
        Check(Act(game,"host","BeginRelease","friend",1).Accepted,"setup: a paid release starts");
        Check(ash.Cash==10,"setup: the $10 is held");Heard(ash,"Cash: RELEASE FOR Sam  •  −$10","the payer");
        Check(Act(game,"host","Cancel").Accepted,"setup: cancel");
        Check(ash.Cash==20,"the cancel gives it back");Heard(ash,"Cash: RELEASE CALLED OFF  •  +$10","the payer");
        sam.Life="Spirit";ash.Wristbands.Add("friend");ash.X=24;ash.Z=-20;
        Check(Act(game,"host","BeginRevival","friend",1).Accepted,"setup: a paid revival starts");
        Check(ash.Cash==10,"setup: the $10 is held");Heard(ash,"Cash: REVIVAL FOR Sam  •  −$10","the payer");
        Check(Act(game,"host","Cancel").Accepted,"setup: cancel the revival");
        Check(ash.Cash==20,"the cancel gives it back");Heard(ash,"Cash: REVIVAL CALLED OFF  •  +$10","the payer");
        game.Disconnect("friend");ash.Life="Spirit";
        Check(Act(game,"host","BeginRevival","host",1).Accepted,"setup: a lone spirit pays for their own revival");Heard(ash,"Cash: YOUR REVIVAL  •  −$10","the lone spirit");
    }
    static void AFailedBuyGivesThePriceBack()
    {
        var game=Play();var ash=game.Player("host");ash.Cash=15;
        var buy=new InteractionState{Id="buy_1",PlayerId="host",Kind="Buy",TargetId="wook_4",Score=.2,ReservedCash=5,Status="Active"};
        Call(game,"SettleBuy",buy,ash);
        Check(ash.Cash==20,"a talk under .4 hands the price back");Heard(ash,"Cash: NO DEAL  •  +$5","the buyer");
    }
    static void ACashPileSaysWhatItHeldButAFindKeepsItsOwnNotice()
    {
        var game=Play();var ash=game.Player("host");int cash=ash.Cash;
        game.State.Drops.Add(new DropState{Id="cash_pile",ItemId="cash",Count=40,X=ash.X,Z=ash.Z});
        Check(Act(game,"host","Pickup","cash_pile").Accepted,"setup: pick up a friend's dropped cash");
        Check(ash.Cash==cash+40,"setup: paid");Heard(ash,"Cash: PICKED UP CASH  •  +$40","the picker");
        game.State.Drops.Add(new DropState{Id="cash_find",ItemId="cash",Count=4,X=ash.X,Z=ash.Z,CashFind=true});int before=ash.MoneyNewsLog.Count;
        Check(Act(game,"host","Pickup","cash_find").Accepted,"setup: pick up a find");
        Check(ash.MoneyNewsLog.Count==before,"a find keeps the HUD's own Found $4 notice, not a second line");
    }
    static void CrewWideFindsReachTheWholeCrew()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");
        game.State.Visions.Add(new VisionState{Id="v_stash",Kind="Stash",X=ash.X,Z=ash.Z});Call(game,"FindStashes");
        Check(sam.MoneyNewsLog.Count>0&&sam.MoneyNewsLog[sam.MoneyNewsLog.Count-1].Text=="STASH  •  +$15  •  Ash found a hidden stash","the crew hears of a vision stash, got '"+Last(sam)+"'");
        Check(ash.MoneyNewsLog.Count==0,"the finder keeps their VisionStashNotice, not a second line");
        game.State.CloudClueLandmark=0;game.State.CloudClueReadAt=game.State.SimulationSeconds;var cloud=FestivalSimulation.CloudStashAt(game.State);ash.X=cloud.X;ash.Z=cloud.Z;Call(game,"WatchClouds");
        Check(game.State.CloudStashFinderId=="host","setup: Ash finds the cloud's stash");
        Heard(sam,"Stash: STASH  •  +$15  •  Ash found the cloud's stash","the crew");Check(ash.MoneyNewsLog.Count==0,"the finder keeps their CloudStashNotice, not a second line");
        Call(game,"Recruited",new InteractionState{TargetId="friend"},sam);
        Heard(ash,"Stash: STASH  •  +$20  •  Sam found a lost friend","the crew");Heard(sam,"Stash: STASH  •  +$20  •  Sam found a lost friend","the recruiter too");
        ash.Inventory.Add(new ItemStack{ItemId=FestivalSimulation.GiggleTankItem,Count=1});ash.X=Festivals.CampGateX;ash.Z=Festivals.CampGateZ;Call(game,"BankGiggleTanks");
        Heard(sam,"Stash: STASH  •  +$"+Festivals.GiggleTankCash+"  •  Ash brought the Giggle Tank home","the crew");
        sam.X=ash.X;sam.Z=ash.Z;Call(game,"End","Success");
        Check(game.State.SurvivorBonus==10,"setup: two survivors bank $10");
        Heard(ash,"Stash: STASH  •  +$10  •  survivor bonus","everyone");Heard(sam,"Stash: STASH  •  +$10  •  survivor bonus","everyone");
    }
    // The seller reads the sale's own outcome ("SOLD Tongue Stamps  •  +$10"); the rest of the crew read what it added to SALES.
    static void ASaleTellsTheCrewWhatItMade()
    {
        foreach(bool sold in new[]{true,false})
        {
            var game=new FestivalSimulation(7);game.AddPlayer("host","Ash");game.AddPlayer("friend","Sam");game.State.Phase="Playing";
            var ash=game.Player("host");var sam=game.Player("friend");ash.X=0;ash.Z=0;ash.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            string goer="";for(int n=0;goer=="";n++)if(DialogueGrammar.PersonaFor("goer_"+n,"")=="Wook")goer="goer_"+n;
            game.State.Npcs.Clear();game.State.Npcs.Add(new NpcState{Id=goer,Kind="Wook",Role="Buyer",X=0,Z=1.5f,Yaw=180,CanTalk=true});
            Check(game.Execute("host",new GameCommand{Id="news"+(sequence++),Kind="StartSale",TargetId=goer,ItemId="stock_lsd",Amount=FestivalSimulation.FairAsk}).Accepted,"setup: the sale starts");
            var talk=game.Interaction(ash.InteractionId);int grade=sold?ConversationLines.Smooth:ConversationLines.Awkward;
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                var live=talk.Turns[talk.Turns.Count-1];
                Check(game.Execute("host",new GameCommand{Id="news"+(sequence++),Kind="Reply",Amount=Array.IndexOf(live.Grades,grade),Direction=talk.Turns.Count-1}).Accepted,"setup: reply "+turn);
            }
            for(int guard=0;talk.Status=="Active"&&guard<200;guard++)game.Tick(.1);
            var outcome=ash.ConversationOutcomes.Find(o=>o.InteractionId==talk.Id);
            Check(outcome!=null&&outcome.Notice==(sold?"SOLD Tongue Stamps  •  +$10":"NO SALE  •  Tongue Stamps back in your hand"),"setup: the sale ends, got '"+outcome?.Notice+"'");
            Check(ash.MoneyNewsLog.Count==0,"the seller reads the sale's outcome, not a second line, got '"+Last(ash)+"'");
            if(sold)Heard(sam,"Sales: SALES  •  +$10  •  Ash sold Tongue Stamps","the crew");
            else Check(sam.MoneyNewsLog.Count==0,"no sale, no news for the crew, got '"+Last(sam)+"'");
        }
    }
    // The rider reads the crowd's tip (DeckTipSpeech); the rest of the crew read what it added to SALES.
    static void ADeckSetsTipsReachTheCrew()
    {
        var game=new FestivalSimulation(3);var rider=game.AddPlayer("p0","Ash");var friend=game.AddPlayer("p1","Sam");
        game.State.UnlockedFestivalCount=Festivals.Count;game.State.FestivalIndex=1;game.State.Phase="Playing";game.State.ElapsedSeconds=10;game.State.Npcs.Clear();
        var at=Festivals.ArtCarAt(0,10);rider.X=at.X;rider.Z=at.Z;game.State.Npcs.Add(new NpcState{Id="fan",X=rider.X+2,Z=rider.Z,Yaw=90});
        Check(Act(game,"p0","RideCar").Accepted&&Act(game,"p0","DeckSet").Accepted,"setup: a deck set starts");var deck=game.Interaction(rider.InteractionId);
        var edges=new List<RhythmInput>();foreach(var note in RhythmChart.For(deck).Notes){edges.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});if(note.HoldSeconds>0)edges.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds+note.HoldSeconds,Release=true});}
        edges.Sort((a,b)=>a.TimeSeconds.CompareTo(b.TimeSeconds));
        foreach(var edge in edges){game.State.SimulationSeconds=deck.StartSeconds+edge.TimeSeconds;Check(game.Execute("p0",new GameCommand{Id="news"+(sequence++),Kind="Rhythm",Direction=edge.Direction,TimeSeconds=edge.TimeSeconds,Amount=edge.Release?1:0}).Accepted,"setup: a note");}
        game.State.SimulationSeconds=deck.StartSeconds+deck.DurationSeconds+.75;game.Tick(.1);
        Check(deck.Score==1&&rider.DeckTipAmount>0,"setup: a perfect set earns tips, got score "+deck.Score+" and $"+rider.DeckTipAmount);
        Heard(friend,"Sales: SALES  •  +$"+rider.DeckTipAmount+"  •  Ash's deck set","the crew");
        Check(rider.MoneyNewsLog.Count==0,"the rider reads the crowd's tip, not a second line, got '"+Last(rider)+"'");
    }
    static void Give(PlayerState p,string item,int count=1){var s=p.Inventory.Find(x=>x.ItemId==item);if(s==null)p.Inventory.Add(new ItemStack{ItemId=item,Count=count});else s.Count+=count;}
    static CommandResult UseItem(FestivalSimulation game,string player,string kind,string item,string target="",int amount=0)=>game.Execute(player,new GameCommand{Id="news"+(sequence++),Kind=kind,ItemId=item,TargetId=target,Amount=amount});
    static void CarriedItemsSayWhatMoved()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");
        Give(ash,"stock_lsd",2);
        Check(UseItem(game,"host","Use","stock_lsd").Accepted,"setup: take a dose");
        Heard(ash,"Items: TOOK Tongue Stamps  •  1 left","the taker");
        game.State.Stashes.Add(new StashState{Id="box",X=ash.X,Z=ash.Z});
        Check(UseItem(game,"host","Deposit","stock_lsd","box",1).Accepted,"setup: stash a unit");
        Heard(ash,"Items: INTO THE STASH  •  1 Tongue Stamps","the depositor");Heard(sam,"Stash: STASH  •  +1 Tongue Stamps  •  Ash","the crew");
        Check(UseItem(game,"friend","Withdraw","stock_lsd","box",1).Accepted,"setup: the friend takes it out");
        Heard(sam,"Items: FROM THE STASH  •  1 Tongue Stamps","the withdrawer");Heard(ash,"Stash: STASH  •  −1 Tongue Stamps  •  Sam","the crew");
        game.State.Drops.Add(new DropState{Id="drop_x",ItemId="stock_mushrooms",Count=1,X=ash.X,Z=ash.Z});
        Check(Act(game,"host","Pickup","drop_x").Accepted,"setup: pick up a unit");Heard(ash,"Items: PICKED UP Fun Guys","the picker");
        game.State.Drops.Add(new DropState{Id="drop_y",ItemId="stock_mushrooms",Count=2,X=ash.X,Z=ash.Z});
        Check(Act(game,"host","Pickup","drop_y").Accepted,"setup: pick up a stack");Heard(ash,"Items: PICKED UP 2 Fun Guys","the picker");
        game.State.Drops.Add(new DropState{Id="band_x",ItemId="wristband",OwnerId="friend",Count=1,X=ash.X,Z=ash.Z});
        Check(Act(game,"host","Pickup","band_x").Accepted,"setup: pick up a band");Heard(ash,"Items: PICKED UP Sam's wristband","the picker");
        Give(ash,FestivalSimulation.GiggleTankItem);Call(game,"LoseGiggleTank",ash);Heard(ash,"Items: GIGGLE TANK LOST","the carrier");
        int before=ash.MoneyNewsLog.Count;Call(game,"LoseGiggleTank",ash);Check(ash.MoneyNewsLog.Count==before,"no tank, no news");
        var buy=new InteractionState{Id="buy_2",PlayerId="host",Kind="Buy",TargetId="wook_4",Score=.9,ReservedCash=5,Status="Active"};
        Give(ash,"poi_practice");Give(ash,"stage_pass");Give(ash,"medical_voucher");
        Check(!(bool)typeof(FestivalSimulation).GetMethod("CanAdd",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{ash,"stock_lsd",1}),"setup: hands full");
        Call(game,"SettleBuy",buy,ash);Heard(ash,"Items: HANDS FULL  •  Tongue Stamps dropped at your feet","a buy into full hands");
    }
    static void HandoffsTellBothSides()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");Give(ash,"stock_lsd",2);sam.X=ash.X;sam.Z=ash.Z;
        Check(UseItem(game,"host","Transfer","stock_lsd","friend",1).Accepted,"setup: offer");
        var offer=game.State.Transfers[0];
        Check(Act(game,"friend","AcceptTransfer",offer.Id).Accepted,"setup: accept");
        Heard(sam,"Items: GOT 1 Tongue Stamps  •  from Ash","the receiver");Heard(ash,"Items: HANDED OVER Tongue Stamps  •  Sam took it","the giver");
        Check(UseItem(game,"host","Transfer","stock_lsd","friend",1).Accepted,"setup: offer again");
        game.Tick(16);Heard(ash,"Items: HANDOFF ENDED  •  Tongue Stamps back in your hand","an offer that runs out");
        Give(ash,"poi_practice");Check(UseItem(game,"host","Transfer","poi_practice","friend",1).Accepted,"setup: offer the poi");
        Give(ash,"stage_pass");Give(ash,"medical_voucher");Give(ash,"stock_mushrooms");Give(ash,"confetti");
        game.Tick(16);Check(game.State.Drops.Exists(d=>d.ItemId=="poi_practice"),"setup: full hands drop the poi");
        Heard(ash,"Items: HANDOFF ENDED  •  Practice poi dropped at your feet","an offer back into full hands");
    }
    static void SecurityTakingDitchedStockTellsTheCrew()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");
        var cop=new NpcState{Id="cop_9",Kind="Cop",X=ash.X,Z=ash.Z};game.State.Npcs.Add(cop);
        game.State.Drops.Add(new DropState{Id="ditched",ItemId="stock_lsd",Count=1,X=ash.X,Z=ash.Z,ClaimedBy="cop_9",ClaimAt=0});
        Call(game,"Confiscate",cop,.1);
        Check(!game.State.Drops.Exists(d=>d.Id=="ditched"),"setup: security took it");
        Heard(ash,"Items: SECURITY TOOK the ditched Tongue Stamps","the crew");Heard(sam,"Items: SECURITY TOOK the ditched Tongue Stamps","the crew");
    }
    static void UsedGearSaysWhatItSpent()
    {
        var game=Play();var ash=game.Player("host");Give(ash,"stash_box");ash.X=-10;ash.Z=0;
        Check(UseItem(game,"host","Use","stash_box").Accepted,"setup: place a stash box");Heard(ash,"Items: STASH BOX PLACED  •  the crew can stash here","the placer");
        game=Play();ash=game.Player("host");Give(ash,"stage_pass");ash.X=Catalog.StageTakeoverX;ash.Z=Catalog.StageTakeoverZ;
        Check(UseItem(game,"host","Use","stage_pass").Accepted,"setup: take the stage");Heard(ash,"Items: STAGE PASS USED","the DJ");
        game=Play();ash=game.Player("host");Give(ash,"stock_lsd");Give(ash,"medical_voucher");ash.X=24;ash.Z=-20;
        Check(UseItem(game,"host","Use","stock_lsd").Accepted&&UseItem(game,"host","Use","medical_voucher").Accepted,"setup: dose, then the tent");
        game.Tick(3.1);Check(!ash.Effects.Exists(FestivalSimulation.Treatable),"setup: treated");Heard(ash,"Items: MEDICAL VOUCHER USED  •  Tongue Stamps treated","the patient");
        game=Play();ash=game.Player("host");var sam=game.Player("friend");sam.Life="Spirit";ash.Wristbands.Add("friend");ash.X=24;ash.Z=-20;
        Check(Act(game,"host","BeginRevival","friend").Accepted,"setup: a revival with Sam's wristband");
        game.Tick(10.1);Check(sam.Life=="Alive","setup: Sam is back");Heard(ash,"Items: WRISTBAND USED  •  Sam is back","the reviver");
        game=Play();ash=game.Player("host");game.Disconnect("friend");ash.Life="Spirit";ash.X=24;ash.Z=-20;
        Check(Act(game,"host","BeginRevival","host").Accepted,"setup: a lone spirit revives themself");
        game.Tick(15.1);Check(ash.Life=="Alive","setup: back");Check(ash.MoneyNewsLog.Count==0,"a self-revival spends no wristband, got '"+Last(ash)+"'");
        game=Play();ash=game.Player("host");
        Call(game,"PassTheRope",new InteractionState{PlayerId="host",Kind="ConfirmChat"},new NpcState{Id="vip",Twist=FestivalSimulation.VipGuard});
        Check(ash.Inventory.Exists(x=>x.ItemId==FestivalSimulation.VipWristband),"setup: the guard hands over a band");Heard(ash,"Items: VIP WRISTBAND  •  you're on the list","the guest");
    }
    static void TheCampStashAndTheNextLevelTellTheCrew()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");Give(ash,"stock_lsd");game.State.Phase="Shopping";
        Check(UseItem(game,"host","Drop","stock_lsd").Accepted,"setup: leave a unit at camp");
        Heard(sam,"Stash: STASH  •  +1 Tongue Stamps  •  Ash","the crew");Check(ash.MoneyNewsLog.Count==0,"the actor keeps the reply, got '"+Last(ash)+"'");
        game=Play();game.State.Stashes.Find(x=>x.Id=="stash").Items.Add(new ItemStack{ItemId=FestivalSimulation.GiggleTankItem,Count=1});
        game.State.Phase="Results";game.State.Result="Success";
        Check(Act(game,"host","Reset").Accepted,"setup: on to the next level");
        Heard(game.Player("host"),"Stash: STASH  •  −1 Giggle Tank  •  tanks never leave the level","everyone");
        Heard(game.Player("friend"),"Stash: STASH  •  −1 Giggle Tank  •  tanks never leave the level","everyone");
        game=Play();game.State.Phase="Results";game.State.Result="Success";
        Check(Act(game,"host","Reset").Accepted&&game.Player("friend").MoneyNewsLog.Count==0,"no tank in the stash, no news at the next level, got '"+Last(game.Player("friend"))+"'");
    }
    static void AFriendsWristbandDropsWithTheGearNotice()
    {
        var game=Play();var ash=game.Player("host");var sam=game.Player("friend");
        ash.Inventory.Clear();ash.Wristbands.Add("friend");ash.Life="Downed";Call(game,"Die",ash);
        Check(game.State.Drops.Exists(d=>d.ItemId=="wristband"&&d.OwnerId=="friend"),"setup: the band drops at the body");
        Check(ash.GearLossSequence==1&&ash.GearLossNotice==FestivalSimulation.DeathGearLossNotice,"a band alone still says the gear dropped, got #"+ash.GearLossSequence+" '"+ash.GearLossNotice+"'");
        sam.Inventory.Clear();sam.Life="Downed";Call(game,"Die",sam);Check(sam.GearLossSequence==0,"empty hands drop nothing to say");
    }
    static void OnlyTheNewestNewsIsKept()
    {
        var game=Play();var ash=game.Player("host");game.State.Stashes.Add(new StashState{Id="box",X=ash.X,Z=ash.Z});ash.Cash=100;
        for(int k=0;k<FestivalSimulation.MoneyNewsCap+1;k++)Check(Act(game,"host","Deposit","box",1).Accepted,"setup: deposit "+k);
        Check(ash.MoneyNewsLog.Count==FestivalSimulation.MoneyNewsCap&&ash.MoneyNewsSequence==FestivalSimulation.MoneyNewsCap+1&&ash.MoneyNewsLog[0].Sequence==2,"the oldest drops out; the sequence keeps counting");
    }
}
