using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;
public static class DeathCashTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("DeathCash: "+message);}
    public static void Run()
    {
        SeededDeathsConserveCash();
        FullPocketsCanClaimCashOnce();
        EarlyPickupWaitsForLanding();
        ARealBoothPauseKeepsTheDeathAgeAndLanding();
        ARealBoothPauseKeepsTheDeathAgeAndLanding(true);
        FrozenIdentityAndAmountSurviveLaterState();
        InvalidDeathSnapshotsAreRefused();
        CashPilesRejectInvalidAmountsWithoutSpendingThePile();
        RefundsAndReturnedGearSettleBeforeDeath();
        ZeroCashAndRealRevivalLimits();
    }
    static FestivalSimulation Crew(int seed=1)
    {
        var s=new FestivalSimulation(43);s.AddPlayer("owner","Owner");s.AddPlayer("friend","Friend");
        s.State.Phase="Playing";s.State.Npcs.Clear();s.State.SpinSeed=seed;
        foreach(var p in s.State.Players){p.X=0;p.Z=0;}return s;
    }
    static void Die(FestivalSimulation s,PlayerState p){p.Life="Downed";p.DownedRemaining=.05;s.Tick(.1);Check(p.Life=="Spirit","setup: Tick expires the downed time");}
    static void SeededDeathsConserveCash()
    {
        int drops=0;
        for(int seed=0;seed<400;seed++)
        {
            var s=Crew(seed);var p=s.Player("owner");p.Cash=40;Die(s,p);
            var cash=s.State.Drops.FindAll(d=>d.ItemId=="cash");
            Check(cash.Count<=1,"one death creates at most one cash pile");
            if(cash.Count==1){drops++;Check(p.Cash==0&&cash[0].Count==40&&cash[0].OwnerId==p.Id,"DROP debits all frozen positive cash immediately");}
            else Check(p.Cash==40,"KEEP retains the same cash");
            int total=p.Cash;foreach(var d in cash)total+=d.Count;Check(total==40,"cash is conserved on every seed");
        }
        Check(drops>=84&&drops<=116,"400 seeded deaths drop cash 25% plus or minus 4%; got "+drops);
    }
    static void EarlyPickupWaitsForLanding()
    {
        var s=Crew(4);var p=s.Player("owner");var friend=s.Player("friend");p.Cash=40;friend.Cash=10;Die(s,p);
        var cash=s.State.Drops.Find(d=>d.ItemId=="cash");Check(cash!=null&&p.Cash==0,"setup: seed 4 drops 40 immediately");
        CommandResult Take(string id)=>s.Execute(friend.Id,new GameCommand{Id=id,Kind="Pickup",TargetId=cash.Id});
        Check(!FestivalSimulation.VisibleDrops(s.State).Contains(cash),"the visible-drop predicate hides the pending pile");
        Check(!Take("before").Accepted&&friend.Cash==10&&s.State.Drops.Contains(cash),"the host refuses a guessed cash ID before landing");
        s.State.SimulationSeconds=p.DeathSpinAt+2.5-1e-6;
        Check(!Take("just-before").Accepted&&friend.Cash==10,"a claim one microsecond before 2.5 seconds is refused");
        s.State.SimulationSeconds=p.DeathSpinAt+2.5;
        Check(FestivalSimulation.VisibleDrops(s.State).Contains(cash),"the visible-drop predicate reveals the exact landed pile");
        Check(Take("landed").Accepted&&friend.Cash==50&&!s.State.Drops.Contains(cash),"at the exact 2.5-second boundary the cash can be claimed");
    }
    static void ARealBoothPauseKeepsTheDeathAgeAndLanding(bool deathAtZero=false)
    {
        var s=Crew(4);var owner=s.Player("owner");var friend=s.Player("friend");owner.Cash=40;
        s.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;s.State.Booth.Spot=0;s.State.Booth.RingSeconds=2.95;
        var at=Festivals.BoothSpots[0];owner.X=friend.X=at.X;owner.Z=friend.Z=at.Z;Die(s,owner);
        Check(s.State.Phase==FestivalSimulation.MinigamePhase,"setup: remaining living crew enters the real booth as the death settles");
        var cash=s.State.Drops.Find(d=>d.ItemId=="cash");
        if(deathAtZero){owner.DeathSpinAt=0;cash.LandsAt=2.5;s.State.SimulationSeconds=0;var restored=new FestivalSimulation();restored.Restore(Saved(s.State));s=restored;owner=s.Player("owner");friend=s.Player("friend");cash=s.State.Drops.Find(d=>d.ItemId=="cash");}
        double death=owner.DeathSpinAt;int seed=owner.DeathSpinSeed;
        s.Tick(3);Check(!FestivalSimulation.VisibleDrops(s.State).Contains(cash),"cash cannot reveal its result while the booth pauses the world");
        Check(s.Execute(friend.Id,new GameCommand{Id="call-cups",Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.CallCups}).Accepted,"setup: the actual booth command ends the game");
        s.Tick(4.1);Check(s.State.Phase=="Playing","setup: the real four-second result banner clears");
        Check(Math.Abs((owner.DeathSpinAt-death)-s.State.Booth.Seconds)<1e-6&&Math.Abs(cash.LandsAt-owner.DeathSpinAt-2.5)<1e-6,"resume translates both simulation timestamps by the actual pause");
        Check(owner.DeathSpinSeed==seed&&owner.DeathSpinAmount==40&&owner.DeathCashDropped==40&&!FestivalSimulation.VisibleDrops(s.State).Contains(cash),"the pause never re-rolls, changes amount or lands the pile early");
        s.State.SimulationSeconds=cash.LandsAt;Check(FestivalSimulation.VisibleDrops(s.State).Contains(cash),"2.5 world seconds after death the resumed pile lands");
    }
    static void FrozenIdentityAndAmountSurviveLaterState()
    {
        var s=Crew(4);var owner=s.Player("owner");owner.Cash=40;Die(s,owner);
        var wire=JsonSerializer.SerializeToElement(owner,new JsonSerializerOptions{IncludeFields=true});
        Check(wire.TryGetProperty("DeathSpinName",out var name)&&name.GetString()=="Owner","the death freezes the owner's name independently of the live player");
        Check(wire.TryGetProperty("DeathSpinRoundId",out var round)&&round.GetString()==s.State.RoundId,"the death freezes its round identity");
        Check(wire.TryGetProperty("DeathSpinId",out var identity)&&identity.GetString()==s.State.RoundId+":death:owner:1","the event identity is stable for its owner and sequence");
        Check(owner.DeathSpinSeed==276&&owner.DeathSpinAt==.1&&owner.DeathSpinAmount==40&&owner.DeathCashDropped==40&&owner.DeathSpinSequence==1,"the authored seed/tick example freezes exact seed, time, positive amount and DROP outcome");
        owner.Name="Changed";owner.Cash=17;owner.Life="Alive";s.Disconnect(owner.Id);
        var after=JsonSerializer.SerializeToElement(owner,new JsonSerializerOptions{IncludeFields=true});
        Check(after.GetProperty("DeathSpinName").GetString()=="Owner"&&after.GetProperty("DeathSpinId").GetString()==identity.GetString()&&owner.DeathSpinAmount==40&&owner.DeathCashDropped==40,"revival, disconnect and later cash/name changes cannot rewrite a frozen death");
        var twin=Crew(4);twin.Player("owner").Cash=40;Die(twin,twin.Player("owner"));
        Check(twin.Player("owner").DeathSpinSeed==276&&twin.Player("owner").DeathCashDropped==40,"the same seed, tick and ordinal produces the same result on a fresh simulation");
    }
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static RoundState Saved(RoundState state)=>JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(state,Json),Json);
    static void InvalidDeathSnapshotsAreRefused()
    {
        var source=Crew(4);source.Player("owner").Cash=40;Die(source,source.Player("owner"));
        var corruptions=new Action<RoundState>[]
        {
            state=>state.Players[0].DeathSpinAt=double.NaN,
            state=>state.Players[0].DeathSpinAmount=-1,
            state=>state.Players[0].DeathCashDropped=39,
            state=>state.Players[0].DeathSpinSequence=long.MaxValue,
            state=>state.Players[0].DeathSpinRoundId="another-round",
            state=>state.Drops.Find(d=>d.ItemId=="cash").LandsAt=double.PositiveInfinity,
            state=>state.Drops.Find(d=>d.ItemId=="cash").LandsAt=-1,
            state=>state.Drops.Find(d=>d.ItemId=="cash").Count=0,
            state=>state.Drops.Add(state.Drops.Find(d=>d.ItemId=="cash")),
            state=>state.Players[0].GearLossSequence=-1,
        };
        for(int k=0;k<corruptions.Length;k++)
        {
            var saved=Saved(source.State);corruptions[k](saved);bool refused=false;
            try{new FestivalSimulation().Restore(saved);}catch(ArgumentException){refused=true;}
            Check(refused,"Restore refuses malformed death state case "+k);
        }
        var valid=Saved(source.State);new FestivalSimulation().Restore(valid);
        Check(valid.Players[0].DeathSpinAmount==40,"valid frozen state restores without changing the amount");
        var old=Crew();var legacy=JsonNode.Parse(JsonSerializer.Serialize(old.State,Json));
        foreach(var player in legacy["Players"].AsArray())foreach(var field in new[]{"DeathSpinAt","DeathSpinSeed","DeathSpinAmount","DeathCashDropped","DeathSpinSequence","DeathSpinId","DeathSpinRoundId","DeathSpinName","GearLossSequence","GearLossKind","GearLossNotice"})player.AsObject().Remove(field);
        var older=new FestivalSimulation();older.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(older.Player("owner").DeathSpinAt==-1&&older.Player("owner").DeathSpinSequence==0&&older.Player("owner").GearLossSequence==0,"older default state has no death or gear-loss event");
    }
    static void CashPilesRejectInvalidAmountsWithoutSpendingThePile()
    {
        foreach(int count in new[]{0,-1,int.MinValue,2})
        {
            var s=Crew();var friend=s.Player("friend");friend.Cash=count==2?int.MaxValue-1:10;
            var pile=new DropState{Id="invalid-cash",ItemId="cash",Count=count};s.State.Drops.Add(pile);int before=friend.Cash;
            var result=s.Execute(friend.Id,new GameCommand{Id="invalid",Kind="Pickup",TargetId=pile.Id});
            Check(!result.Accepted&&friend.Cash==before&&s.State.Drops.Contains(pile),"nonpositive or overflowing cash rejects without credit/removal: "+count);
        }
        var exact=Crew();exact.Player("friend").Cash=0;exact.State.Drops.Add(new DropState{Id="maximum",ItemId="cash",Count=int.MaxValue});
        Check(exact.Execute("friend",new GameCommand{Id="exact",Kind="Pickup",TargetId="maximum"}).Accepted&&exact.Player("friend").Cash==int.MaxValue,"the exact integer boundary still credits atomically");
    }
    static void RefundsAndReturnedGearSettleBeforeDeath()
    {
        var paid=Crew(4);paid.State.Phase="Shopping";paid.AddPlayer("free","Free");paid.State.Phase="Playing";
        var owner=paid.Player("owner");var friend=paid.Player("friend");owner.Cash=40;owner.X=friend.X=27;owner.Z=friend.Z=5;friend.Life="Detained";
        Check(paid.Execute(owner.Id,new GameCommand{Id="pay-release",Kind="BeginRelease",TargetId=friend.Id,Amount=1}).Accepted&&owner.Cash==30,"setup: the actual release reserves ten cash");
        Die(paid,owner);var cash=paid.State.Drops.Find(d=>d.ItemId=="cash");
        Check(owner.DeathSpinAmount==40&&cash!=null&&cash.Count==40&&owner.Cash==0&&paid.State.Interactions.Find(i=>i.Id!="").ReservedCash==0,"cancellation refunds before the 40-cash freeze and debit");

        var sale=Crew(4);owner=sale.Player("owner");owner.Cash=0;owner.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        sale.State.Npcs.Add(new NpcState{Id="buyer",Role="Buyer",X=1,Z=0,CanTalk=true});
        Check(sale.Execute(owner.Id,new GameCommand{Id="reserved-sale",Kind="StartSale",TargetId="buyer",ItemId="stock_lsd"}).Accepted&&owner.Inventory.Count==0,"setup: actual sale reserves the last stock");
        Die(sale,owner);
        Check(sale.State.Drops.Exists(d=>d.ItemId=="stock_lsd"&&d.Count==1)&&owner.GearLossSequence==1,"a refunded sale item actually drops and produces the loss notice");

        var handoff=Crew(4);owner=handoff.Player("owner");owner.Cash=0;owner.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
        Check(handoff.Execute(owner.Id,new GameCommand{Id="reserved-poi",Kind="Transfer",TargetId="friend",ItemId="poi_practice",Amount=1}).Accepted&&owner.Inventory.Count==0,"setup: actual handoff reserves all gear");
        Die(handoff,owner);
        Check(handoff.State.Transfers.Count==0&&handoff.State.Drops.Exists(d=>d.ItemId=="poi_practice")&&owner.GearLossSequence==1,"returned outgoing handoff drops at death and emits a loss");

        var recipient=Crew();owner=recipient.Player("owner");friend=recipient.Player("friend");owner.Cash=0;friend.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
        Check(recipient.Execute(friend.Id,new GameCommand{Id="incoming",Kind="Transfer",TargetId=owner.Id,ItemId="poi_practice",Amount=1}).Accepted,"setup: incoming handoff is pending");
        foreach(var id in new[]{"confetti","map","merch_bag"})friend.Inventory.Add(new ItemStack{ItemId=id,Count=1});
        Die(recipient,owner);
        Check(owner.GearLossSequence==0&&recipient.State.Drops.Exists(d=>d.ItemId=="poi_practice"),"an incoming offer returned to a full sender never claims the recipient lost gear");
    }
    static void ZeroCashAndRealRevivalLimits()
    {
        var empty=Crew(4);var owner=empty.Player("owner");owner.Cash=0;Die(empty,owner);
        Check(owner.DeathSpinAt==-1&&owner.DeathSpinSequence==0&&owner.DeathSpinAmount==0&&empty.State.Drops.TrueForAll(d=>d.ItemId!="cash"),"zero cash records no spin and no cash pile");
        var s=Crew(4);owner=s.Player("owner");var friend=s.Player("friend");
        for(int death=1;death<=3;death++)
        {
            owner.Cash=40;owner.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});friend.X=owner.X;friend.Z=owner.Z;Die(s,owner);
            Check(owner.DeathSpinSequence==death&&owner.GearLossSequence==death,"each of the three real deaths has a new independent sequence");
            new FestivalSimulation().Restore(Saved(s.State));
            var band=s.State.Drops.Find(d=>d.ItemId=="wristband"&&d.OwnerId==owner.Id);
            Check(s.Execute(friend.Id,new GameCommand{Id="band-"+death,Kind="Pickup",TargetId=band.Id}).Accepted,"setup: living friend claims the owner's wristband");
            friend.X=owner.X=24;friend.Z=owner.Z=-20;
            var revival=s.Execute(friend.Id,new GameCommand{Id="revive-"+death,Kind="BeginRevival",TargetId=owner.Id});
            if(death==3)Check(!revival.Accepted&&owner.Life=="Spirit","a third real revival is refused without a fourth death sequence");
            else{Check(revival.Accepted,"setup: actual free revival begins");s.Tick(10.1);Check(owner.Life=="Alive"&&owner.RevivalCount==death,"the real revival finishes and counts the use");}
        }
    }
    static void FullPocketsCanClaimCashOnce()
    {
        var s=new FestivalSimulation(43);var owner=s.AddPlayer("owner","Owner");var friend=s.AddPlayer("friend","Friend");
        s.State.Phase="Playing";s.State.Npcs.Clear();owner.X=friend.X=0;owner.Z=friend.Z=0;owner.Life="Spirit";friend.Cash=10;
        foreach(var id in new[]{"confetti","map","merch_bag"})friend.Inventory.Add(new ItemStack{ItemId=id,Count=1});
        var cash=new DropState{Id="death-cash",ItemId="cash",OwnerId=owner.Id,Count=40,X=0,Z=0};s.State.Drops.Add(cash);
        Check(!s.Execute(owner.Id,new GameCommand{Id="spirit-pick",Kind="Pickup",TargetId=cash.Id}).Accepted,"a spirit cannot collect the pile");
        var command=new GameCommand{Id="friend-pick",Kind="Pickup",TargetId=cash.Id};var taken=s.Execute(friend.Id,command);
        Check(taken.Accepted&&friend.Cash==50&&friend.Inventory.Count==3&&!s.State.Drops.Contains(cash),"living friend's cash pickup adds all 40 with three full inventory slots; accepted="+taken.Accepted+", cash="+friend.Cash+", reason="+taken.Reason);
        var duplicate=s.Execute(friend.Id,command);
        Check(ReferenceEquals(taken,duplicate)&&duplicate.Sequence==taken.Sequence&&friend.Cash==50,"replayed command returns same result without paying twice");
        owner.Life="Alive";
        Check(!s.Execute(owner.Id,new GameCommand{Id="racing-pick",Kind="Pickup",TargetId=cash.Id}).Accepted&&owner.Cash==20,"second living claimant receives no cash from an already taken pile");
    }
}
