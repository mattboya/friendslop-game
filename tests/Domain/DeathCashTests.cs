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
        var failures=new List<string>();
        foreach(var test in new Action[]{PublicHistoryRetainsEveryActualDeathAndResets,PublicHistoryUsesImmutableWorldTimeThroughARealBooth,LegacySnapshotsDoNotSynthesizePublicDeaths})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+": "+error.Message);}
        Check(failures.Count==0,string.Join("\n",failures));
        EveryDeathEmitsItsFrozenPublicRecord();
        PublicHistoryRejectsMalformedSnapshots();
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
        AloneWithNoRevivalsLeftTheDownedHintSaysTheLevelEnds();
        TheDownedCardCountsDownAndAloneNamesTheTent();
    }
    static void EveryDeathEmitsItsFrozenPublicRecord()
    {
        var s=Crew(4);var owner=s.Player("owner");owner.Cash=40;Die(s,owner);
        var wire=JsonSerializer.SerializeToElement(s.State,new JsonSerializerOptions{IncludeFields=true});
        Check(wire.TryGetProperty("DeathSpins",out var spins)&&spins.GetArrayLength()==1,"a real positive-cash death emits one persisted public record immediately, before any viewer polls");
        var spin=spins[0];
        Check(spin.GetProperty("Id").GetString()==s.State.RoundId+":death:owner:1"&&spin.GetProperty("RoundId").GetString()==s.State.RoundId,"the public record carries stable round and event identity");
        Check(spin.GetProperty("PlayerId").GetString()=="owner"&&spin.GetProperty("Name").GetString()=="Owner"&&spin.GetProperty("Amount").GetInt32()==40,"only frozen owner identity, name and amount are published");
        Check(spin.GetProperty("AtWorldSeconds").GetDouble()==.1&&spin.GetProperty("Seed").GetInt32()==276&&spin.GetProperty("CashDropped").GetInt32()==40,"the independent death emission retains its exact world time, seed and DROP outcome");
    }
    static void PublicHistoryRejectsMalformedSnapshots()
    {
        var s=Crew(4);s.Player("owner").Cash=40;Die(s,s.Player("owner"));
        var corruptions=new Action<RoundState>[]
        {
            state=>state.DeathSpins=null,
            state=>state.DeathSpins[0]=null,
            state=>state.DeathSpins[0].RoundId="older-round",
            state=>state.DeathSpins[0].Id="wrong-identity",
            state=>state.DeathSpins[0].PlayerId="unknown-player",
            state=>state.DeathSpins[0].Name=null,
            state=>state.DeathSpins[0].Name=new string('n',25),
            state=>state.DeathSpins[0].Sequence=0,
            state=>state.DeathSpins[0].Sequence=4,
            state=>state.DeathSpins[0].Amount=0,
            state=>state.DeathSpins[0].CashDropped=39,
            state=>state.DeathSpins[0].AtWorldSeconds=-1,
            state=>state.DeathSpins[0].AtWorldSeconds=double.NaN,
            state=>state.DeathSpins[0].AtWorldSeconds=double.PositiveInfinity,
            state=>state.DeathSpins[0].AtWorldSeconds=1,
            state=>state.DeathSpins.Add(state.DeathSpins[0]),
            state=>state.DeathSpins[0].Name="Rewritten",
            state=>state.DeathSpins[0].Amount=state.DeathSpins[0].CashDropped=41,
            state=>state.DeathSpins[0].Seed=0,
            state=>{for(int k=1;k<25;k++)state.DeathSpins.Add(state.DeathSpins[0]);},
        };
        for(int k=0;k<corruptions.Length;k++)
        {
            var bad=Saved(s.State);corruptions[k](bad);var target=new FestivalSimulation();var before=target.State;
            bool refused=false;try{target.Restore(bad);}catch(ArgumentException){refused=true;}
            Check(refused&&ReferenceEquals(target.State,before),"Restore refuses malformed public death history before replacing state, case "+k);
        }
        var zero=Saved(s.State);zero.DeathSpins[0].AtWorldSeconds=zero.Players[0].DeathSpinAt=zero.SimulationSeconds=0;zero.Drops.Find(d=>d.ItemId=="cash").LandsAt=2.5;
        var restored=new FestivalSimulation();restored.Restore(zero);
        Check(restored.State.DeathSpins.Count==1&&restored.State.DeathSpins[0].AtWorldSeconds==0&&restored.State.DeathSpins[0].Seed==276,"a valid time-zero public death restores without normalization or a new RNG draw");
    }
    static void PublicHistoryRetainsEveryActualDeathAndResets()
    {
        var s=new FestivalSimulation(43);
        for(int k=0;k<8;k++)s.AddPlayer("player-"+k,"Friend "+k);
        s.State.Phase="Playing";s.State.Npcs.Clear();s.State.SpinSeed=4;
        string firstId="";double firstAt=0;int firstSeed=0,firstOutcome=0;
        // Each friend dies once more than a level's revivals allow, so the history fills whatever RevivalsPerLevel is.
        int deaths=FestivalGuidance.RevivalsPerLevel+1,all=8*deaths;
        for(int death=1;death<=deaths;death++)for(int k=0;k<8;k++)
        {
            // A multiplayer wipe ends before the last downed expiry. Disconnect the seven spirits before the final solo death.
            if(death==deaths&&k==7)for(int gone=0;gone<7;gone++)s.Disconnect("player-"+gone);
            var owner=s.Player("player-"+k);var helper=s.Player(k==0?"player-1":"player-0");owner.Cash=40;owner.X=helper.X=0;owner.Z=helper.Z=0;
            Die(s,owner);
            Check(s.State.DeathSpins.Count==(death-1)*8+k+1,"each real death is appended even when no view polls between deaths");
            if(death==1&&k==0){firstId=s.State.DeathSpins[0].Id;firstAt=s.State.DeathSpins[0].AtWorldSeconds;firstSeed=s.State.DeathSpins[0].Seed;firstOutcome=s.State.DeathSpins[0].CashDropped;owner.Name="Changed";}
            if(death<deaths)
            {
                var band=s.State.Drops.Find(d=>d.ItemId=="wristband"&&d.OwnerId==owner.Id);
                Check(s.Execute(helper.Id,new GameCommand{Id="band-"+death+"-"+k,Kind="Pickup",TargetId=band.Id}).Accepted,"setup: living helper claims the actual wristband");
                helper.X=owner.X=24;helper.Z=owner.Z=-20;
                Check(s.Execute(helper.Id,new GameCommand{Id="revive-"+death+"-"+k,Kind="BeginRevival",TargetId=owner.Id}).Accepted,"setup: the actual free revival starts");
                s.Tick(10.1);Check(owner.Life=="Alive"&&owner.RevivalCount==death,"the actual revival completes without consuming death history");
            }
        }
        Check(s.State.DeathSpins.Count==all,"eight players with "+(deaths-1)+" actual revivals each retain the full bounded "+all+"-event history");
        var first=s.State.DeathSpins[0];Check(first.Id==firstId&&first.AtWorldSeconds==firstAt&&first.Name=="Friend 0"&&first.Amount==40&&first.Seed==firstSeed&&first.CashDropped==firstOutcome,"later positive deaths and name changes cannot rewrite earlier immutable records");
        s.Disconnect("player-0");s.AddPlayer("player-0","Rejoined");
        var restored=new FestivalSimulation();restored.Restore(Saved(s.State));
        Check(restored.State.DeathSpins.Count==all&&restored.State.DeathSpins[0].Id==firstId,"disconnect, reconnect and System.Text.Json restore retain every emitted record");
        string round=s.State.RoundId;s.State.Phase="Results";s.State.Result="Failure";
        Check(s.Execute("player-0",new GameCommand{Id="new-round",Kind="Reset"}).Accepted&&s.State.RoundId!=round&&s.State.DeathSpins.Count==0,"the actual round reset discards old public deaths");
    }
    static void PublicHistoryUsesImmutableWorldTimeThroughARealBooth()
    {
        var s=Crew(4);var owner=s.Player("owner");var friend=s.Player("friend");owner.Cash=40;
        s.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;s.State.Booth.Spot=0;s.State.Booth.RingSeconds=2.95;
        var at=Festivals.BoothSpots[0];owner.X=friend.X=at.X;owner.Z=friend.Z=at.Z;Die(s,owner);
        Check(s.State.Phase==FestivalSimulation.MinigamePhase,"setup: the real physical booth starts on the death tick");
        var spin=s.State.DeathSpins[0];string frozen=JsonSerializer.Serialize(spin,Json);double world=FestivalSimulation.WorldSeconds(s.State);
        s.Tick(3);Check(Math.Abs(FestivalSimulation.WorldSeconds(s.State)-world)<1e-6&&JsonSerializer.Serialize(spin,Json)==frozen,"the actual booth freezes public death age and never rewrites the summary");
        Check(s.Execute(friend.Id,new GameCommand{Id="end-public-cups",Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.CallCups}).Accepted,"setup: actual booth command scores the game");
        s.Tick(4.1);Check(s.State.Phase=="Playing"&&JsonSerializer.Serialize(spin,Json)==frozen,"resuming translates deadlines without translating immutable public world time");
        var restored=new FestivalSimulation();restored.Restore(Saved(s.State));
        Check(JsonSerializer.Serialize(restored.State.DeathSpins[0],Json)==frozen,"the same immutable event restores after the physical pause");
        var cash=s.State.Drops.Find(d=>d.ItemId=="cash");s.State.SimulationSeconds=cash.LandsAt-1e-6;
        Check(FestivalSimulation.WorldSeconds(s.State)-spin.AtWorldSeconds<2.5&&!FestivalSimulation.VisibleDrops(s.State).Contains(cash),"public world age and authoritative pile visibility agree just before landing");
        s.State.SimulationSeconds=cash.LandsAt;
        Check(Math.Abs(FestivalSimulation.WorldSeconds(s.State)-spin.AtWorldSeconds-2.5)<1e-6&&FestivalSimulation.VisibleDrops(s.State).Contains(cash),"the exact landing occurs at public event world age 2.5 seconds");
    }
    static void LegacySnapshotsDoNotSynthesizePublicDeaths()
    {
        var s=Crew(4);s.Player("owner").Cash=40;Die(s,s.Player("owner"));
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json));legacy.AsObject().Remove("DeathSpins");
        var old=JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json);var restored=new FestivalSimulation();restored.Restore(old);
        Check(restored.State.DeathSpins!=null&&restored.State.DeathSpins.Count==0&&restored.Player("owner").DeathSpinSequence==1,"a missing public collection defaults quiet even when an old latest owner record exists; never fabricate partial history");
        var owner=restored.Player("owner");var friend=restored.Player("friend");
        var band=restored.State.Drops.Find(d=>d.ItemId=="wristband"&&d.OwnerId==owner.Id);
        Check(restored.Execute(friend.Id,new GameCommand{Id="legacy-band",Kind="Pickup",TargetId=band.Id}).Accepted,"setup: legacy saved owner can still be recovered");
        owner.X=friend.X=24;owner.Z=friend.Z=-20;
        Check(restored.Execute(friend.Id,new GameCommand{Id="legacy-revive",Kind="BeginRevival",TargetId=owner.Id}).Accepted,"setup: actual revival works after additive restore");
        restored.Tick(10.1);owner.Cash=55;Die(restored,owner);
        Check(restored.State.DeathSpins.Count==1&&restored.State.DeathSpins[0].Sequence==2&&restored.State.DeathSpins[0].Amount==55,"the later real death emits only its new event; it never backfills the missing old death");
        new FestivalSimulation().Restore(Saved(restored.State));
        var zero=Crew(4);zero.Player("owner").Cash=0;Die(zero,zero.Player("owner"));
        Check(zero.State.DeathSpins.Count==0,"zero cash deaths publish no event");
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
        if(deathAtZero){s.State.DeathSpins[0].AtWorldSeconds=0;owner.DeathSpinAt=0;cash.LandsAt=2.5;s.State.SimulationSeconds=0;var restored=new FestivalSimulation();restored.Restore(Saved(s.State));s=restored;owner=s.Player("owner");friend=s.Player("friend");cash=s.State.Drops.Find(d=>d.ItemId=="cash");}
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
    // Alone, nobody can help a downed player up, so the hint says the time runs out into a spirit who comes back at the medical tent.
    static void TheDownedCardCountsDownAndAloneNamesTheTent()
    {
        var s=Crew(4);var owner=s.Player("owner");var friend=s.Player("friend");
        owner.Life="Downed";owner.DownedRemaining=27.2;
        Check(FestivalGuidance.Headline(s.State,owner)=="DOWNED • 28 s","the card counts the downed seconds up: \""+FestivalGuidance.Headline(s.State,owner)+"\"");
        Check(FestivalGuidance.Hint(s.State,owner)=="Press E to distract attackers; a teammate can rescue or drag you.","with a friend the hint is as before");
        s.Disconnect(friend.Id);
        Check(FestivalGuidance.Hint(s.State,owner)=="Nobody can help you up alone. When the time runs out, you become a spirit and can come back at the medical tent.","alone, the hint names the way back: \""+FestivalGuidance.Hint(s.State,owner)+"\"");
        s.Tick(.1);Check(FestivalGuidance.Headline(s.State,owner)=="DOWNED • 28 s","a tenth of a second on, still 28 s");
        owner.DownedRemaining=.05;s.Tick(.1);
        Check(owner.Life=="Spirit"&&s.State.Phase=="Playing","setup: alone, the time runs out into a spirit and the level goes on");
        Check(FestivalGuidance.Headline(s.State,owner)=="SPIRIT • REACH THE MEDICAL TENT"&&FestivalGuidance.RevivalsLeft(owner)==2,"a first death leaves both revivals");
        owner.RevivalCount=1;Check(FestivalGuidance.RevivalsLeft(owner)==1,"one used, one left");
        owner.RevivalCount=2;
        Check(FestivalGuidance.RevivalsLeft(owner)==0&&FestivalGuidance.Headline(s.State,owner)=="SPIRIT • NO REVIVALS LEFT","both used: \""+FestivalGuidance.Headline(s.State,owner)+"\"");
        Check(FestivalGuidance.Hint(s.State,owner)=="No revivals left this level: you stay a spirit until it ends.","and nobody is sent to the tent for nothing");
    }
    // Alone with every revival used, the downed time running out ends the level (Weekend.LevelEndCheck), not a trip to the tent.
    static void AloneWithNoRevivalsLeftTheDownedHintSaysTheLevelEnds()
    {
        var s=Crew(4);var owner=s.Player("owner");s.Disconnect("friend");
        owner.Life="Downed";owner.DownedRemaining=27.2;owner.RevivalCount=FestivalGuidance.RevivalsPerLevel-1;
        Check(FestivalGuidance.Hint(s.State,owner)=="Nobody can help you up alone. When the time runs out, you become a spirit and can come back at the medical tent.","one revival left: the hint names the tent");
        owner.DownedRemaining=.05;s.Tick(.1);
        Check(owner.Life=="Spirit"&&s.State.Phase=="Playing","one revival left: alone, the spirit can still come back and the level goes on: phase "+s.State.Phase+", result \""+s.State.Result+"\"");
        var last=Crew(4);owner=last.Player("owner");last.Disconnect("friend");
        owner.Life="Downed";owner.DownedRemaining=27.2;owner.RevivalCount=FestivalGuidance.RevivalsPerLevel;
        string hint=FestivalGuidance.Hint(last.State,owner);
        Check(hint=="Nobody can help you up alone, and you have no revivals left: when the time runs out, the level ends.","none left: the hint tells the truth: \""+hint+"\"");
        Check(FestivalGuidance.Headline(last.State,owner)=="DOWNED • 28 s","the card still counts the downed seconds");
        owner.DownedRemaining=.05;last.Tick(.1);
        Check(owner.Life=="Spirit"&&last.State.Phase=="Results"&&last.State.Result==FestivalSimulation.WipedOut,"none left: when the time runs out, the level ends: phase "+last.State.Phase+", result \""+last.State.Result+"\"");
    }
    static void ZeroCashAndRealRevivalLimits()
    {
        var empty=Crew(4);var owner=empty.Player("owner");owner.Cash=0;Die(empty,owner);
        Check(owner.DeathSpinAt==-1&&owner.DeathSpinSequence==0&&owner.DeathSpinAmount==0&&empty.State.Drops.TrueForAll(d=>d.ItemId!="cash"),"zero cash records no spin and no cash pile");
        var s=Crew(4);owner=s.Player("owner");var friend=s.Player("friend");
        // A player can die once more than the level's revivals.
        int limit=FestivalGuidance.RevivalsPerLevel+1;
        for(int death=1;death<=limit;death++)
        {
            owner.Cash=40;owner.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});friend.X=owner.X;friend.Z=owner.Z;Die(s,owner);
            Check(owner.DeathSpinSequence==death&&owner.GearLossSequence==death,"each real death has a new independent sequence");
            new FestivalSimulation().Restore(Saved(s.State));
            var band=s.State.Drops.Find(d=>d.ItemId=="wristband"&&d.OwnerId==owner.Id);
            Check(s.Execute(friend.Id,new GameCommand{Id="band-"+death,Kind="Pickup",TargetId=band.Id}).Accepted,"setup: living friend claims the owner's wristband");
            friend.X=owner.X=24;friend.Z=owner.Z=-20;
            var revival=s.Execute(friend.Id,new GameCommand{Id="revive-"+death,Kind="BeginRevival",TargetId=owner.Id});
            if(death==limit)Check(!revival.Accepted&&owner.Life=="Spirit","a revival past the level's "+FestivalGuidance.RevivalsPerLevel+" is refused without another death sequence");
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
