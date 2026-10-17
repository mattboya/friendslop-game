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
        DevelopmentSelectionKeepsCampAndHostBoundaries();
        DevelopmentCapabilityIsNotRestoredOrCommandSettable();
        DevelopmentSessionNeverAdvancesEarnedUnlocksOnRealWins();
        DevelopmentHostCanPickWithoutEarningUnlocks();
        DroppedGearNamesTheItem();
        DeathGearLossHasAnOwnerSequence();
        FestivalTable();
        EncoreScaling();
        NewCrewStartsDayOne();
        SnapshotsKeepTheWeekendPosition();
        WeekendCarriesCashGearAndStash();
        StashesCarryThroughTheWeekend();
        DyingKeepsTheWeekendsCash();
        FullHandsCanLeaveGearAtCamp();
        OldCampSnapshotsLaunchTheTablesLength();
        OldRescueSnapshotsResumeAsANight();
        FailureRestartsTheFestival();
        HostChoosesAnUnlockedFestival();
        WinningNightTwoUnlocksOnTheResultsScreen();
    }

    static void DevelopmentSelectionKeepsCampAndHostBoundaries()
    {
        var s=Crew(95);s.DevelopmentFestivalSelection=true;
        foreach(var phase in new[]{"Lobby","Spinning","Loading","Playing","Results","CampReview",FestivalSimulation.MinigamePhase})
        {
            s.State.Phase=phase;
            Check(!Act(s,"a","ChooseFestival",1).Accepted&&s.State.FestivalIndex==0,"development selection remains camp-only during "+phase);
        }
        s.State.Phase="Shopping";
        for(int level=1;level<4;level++)
        {
            s.State.LevelIndex=level;
            Check(!Act(s,"a","ChooseFestival",1).Accepted&&s.State.FestivalIndex==0,"development selection stays before Day 1, not level "+level);
        }
        s.State.LevelIndex=0;
        Check(!Act(s,"b","ChooseFestival",1).Accepted&&s.State.FestivalIndex==0,"a connected client cannot select a locked festival");
        s.Disconnect("a");
        Check(!Act(s,"a","ChooseFestival",1).Accepted&&s.State.FestivalIndex==0,"a disconnected host cannot select festivals");
        s.AddPlayer("a","A");
        Check(Act(s,"a","ChooseFestival",1).Accepted&&s.State.UnlockedFestivalCount==1,"reconnected host selects before Day 1 without unlocking");
        var before=s.State;
        foreach(var mutate in new Action<RoundState>[] {x=>x.FestivalIndex=-1,x=>x.FestivalIndex=2,x=>x.LevelIndex=-1,x=>x.LevelIndex=4,x=>x.EncoreTier=-1,x=>x.UnlockedFestivalCount=0,x=>x.UnlockedFestivalCount=-1,x=>x.UnlockedFestivalCount=3})
        {
            var invalid=JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(before,Json),Json);mutate(invalid);
            bool rejected=false;try{s.Restore(invalid);}catch(ArgumentException){rejected=true;}
            Check(rejected&&ReferenceEquals(s.State,before),"invalid development snapshots are rejected without replacing the current round");
        }
    }

    static void DevelopmentCapabilityIsNotRestoredOrCommandSettable()
    {
        var s=Crew(94);
        Check(!s.DevelopmentFestivalSelection,"new sessions default to earned-only festival selection");
        foreach(var player in new[]{"a","b"})
        {
            Check(!Act(s,player,"DevelopmentFestivalSelection",1).Accepted,"commands cannot enable the session capability for "+player);
            Check(!s.DevelopmentFestivalSelection&&!Act(s,"a","ChooseFestival",1).Accepted,"a refused capability command leaves Ember Playa locked");
        }
        s.DevelopmentFestivalSelection=true;
        var saved=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        Check(!saved.ContainsKey("DevelopmentFestivalSelection"),"round snapshots never carry the development capability");
        saved["DevelopmentFestivalSelection"]=true;
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(saved.ToJsonString(),Json));
        Check(!restored.DevelopmentFestivalSelection&&!Act(restored,"a","ChooseFestival",1).Accepted,"even an injected snapshot flag cannot enable development selection");
        s.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        Check(s.DevelopmentFestivalSelection&&Act(s,"a","ChooseFestival",1).Accepted,"restoration preserves the receiving host's enabled session capability");
        Finish(s,"Time expired");s.State.Phase="Shopping";
        Check(s.DevelopmentFestivalSelection&&s.State.UnlockedFestivalCount==1&&Act(s,"a","ChooseFestival",0).Accepted&&Act(s,"a","ChooseFestival",1).Accepted&&s.State.FestivalIndex==1,"a failed weekend preserves the capability without granting unlocks");
    }

    static void DevelopmentSessionNeverAdvancesEarnedUnlocksOnRealWins()
    {
        var s=Crew(92);s.DevelopmentFestivalSelection=true;
        for(int festival=0;festival<2;festival++)
        {
            Check(Act(s,"a","ChooseFestival",festival).Accepted,"test host selects festival "+festival);
            for(int level=0;level<3;level++){Finish(s,"Success");s.State.Phase="Shopping";}
            Check(s.State.LevelIndex==3,"setup: Night 2 at festival "+festival);
            foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,p.Id+" readies for Night 2");}
            s.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in s.State.Players)Check(Act(s,p.Id,"MapReady").Accepted,p.Id+" loads Night 2");
            Check(s.State.Phase=="Playing","setup: Night 2 launched at festival "+festival);
            s.State.FriendFound=true;s.State.FriendPosition=new WorldPoint(Festivals.CampGateX,Festivals.CampGateZ);
            foreach(var p in s.State.Players){p.X=Festivals.CampGateX;p.Z=Festivals.CampGateZ;}
            Check(Act(s,"a","Extract").Accepted,"the crew extracts at festival "+festival);s.Tick(3.1);
            Check(s.State.Phase=="Results"&&s.State.Result=="Success","Night 2 ends through the real extraction at festival "+festival);
            Check(s.State.UnlockedFestivalCount==1,"test session earns no unlock at real Night 2 Results, even in Palm Mirage");
            var old=s.State;Check(Act(s,"a","Reset").Accepted,"the host returns to camp after Night 2");
            Check(!ReferenceEquals(s.State,old)&&s.State.UnlockedFestivalCount==1&&s.DevelopmentFestivalSelection,"fresh camp preserves the session capability and earned count");
            s.State.Phase="Shopping";
        }
        var earned=Crew(93);earned.State.UnlockedFestivalCount=2;earned.DevelopmentFestivalSelection=true;
        for(int level=0;level<4;level++)Finish(earned,"Success");
        Check(earned.State.UnlockedFestivalCount==2,"test sessions retain already-earned unlocks");
    }

    static void DevelopmentHostCanPickWithoutEarningUnlocks()
    {
        var s=Crew(91);s.DevelopmentFestivalSelection=true;
        var pick=Act(s,"a","ChooseFestival",1);
        Check(pick.Accepted&&s.State.FestivalIndex==1,"test host can pick locked Ember Playa; got: "+pick.Reason);
        Check(s.State.UnlockedFestivalCount==1,"test selection does not earn an unlock");
        Check(!Act(s,"b","ChooseFestival",0).Accepted,"client cannot pick even in test mode");
        Check(!Act(s,"a","ChooseFestival",-1).Accepted&&!Act(s,"a","ChooseFestival",Festivals.Count).Accepted,"test mode rejects invalid festival indices");
        var restored=new FestivalSimulation();restored.DevelopmentFestivalSelection=true;
        restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        Check(restored.State.FestivalIndex==1&&restored.State.UnlockedFestivalCount==1,"test host restores a selected locked festival without earning it");
        foreach(int count in new[]{0,-1,Festivals.Count+1})
        {
            var invalid=JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json);invalid.UnlockedFestivalCount=count;
            bool rejected=false;
            try{restored.Restore(invalid);}catch(ArgumentException){rejected=true;}
            Check(rejected,"test mode still rejects invalid earned unlock count "+count);
        }
        bool releaseRejected=false;
        try{new FestivalSimulation().Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));}
        catch(ArgumentException){releaseRejected=true;}
        Check(releaseRejected,"default release rules reject a snapshot beyond earned unlocks");
        for(int level=0;level<4;level++)Finish(s,"Success");
        Check(s.State.UnlockedFestivalCount==1,"clearing test-selected Night 2 never earns a persistent unlock");
        s.State.Phase="Shopping";
        Check(Act(s,"a","ChooseFestival",1).Accepted,"test flag survives the camp round rebuild");
    }

    static int sequence;
    static CommandResult Act(FestivalSimulation s,string player,string kind,int amount=0)=>s.Execute(player,new GameCommand{Id="weekend_"+(++sequence),Kind=kind,Amount=amount});
    static string Where(FestivalSimulation s)=>s.State.FestivalIndex+"/"+s.State.LevelIndex+"/"+s.State.EncoreTier;
    static FestivalSimulation Crew(int seed){var s=new FestivalSimulation(seed);s.AddPlayer("a","A");s.AddPlayer("b","B");return s;}
    // The level just ended with this result; the host brings the crew back to camp, which opens the debrief.
    static void Finish(FestivalSimulation s,string result){s.State.Phase="Results";s.State.Result=result;Check(Act(s,"a","Reset").Accepted&&s.State.Phase=="CampReview","host brings the crew back after "+result);}
    // What a player walked out of the level holding, plus the level's shared stash and scars that must not carry.
    static void Earn(FestivalSimulation s,int cash,int confetti,int stash)
    {
        var a=s.Player("a");a.Cash=cash;a.Inventory.Clear();a.Inventory.Add(new ItemStack{ItemId="confetti",Count=confetti});a.EquippedItemId="confetti";a.HasCosmetic=true;s.State.StashCash=stash;
        var b=s.Player("b");b.Cash=cash+7;b.Inventory.Clear();b.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});b.EquippedItemId="poi_practice";b.HasCosmetic=true;
        a.Effects.Add(new ActiveEffect{Id="weed",InstanceId="effect_x",RemainingSeconds=30});a.Wristbands.Add("b");a.Life="Detained";a.Health=40;
        s.State.Npcs[0].Observers.Add(new ObserverState{PlayerId="a",Suspicion=80});
    }
    static void Fresh(FestivalSimulation s,string why)
    {
        foreach(var a in s.State.Players)
        {
            Check(a.Cash==20&&a.Inventory.Count==0&&a.EquippedItemId==""&&s.State.StashCash==0,why+": "+a.Id+" has fresh $20, no gear, empty stash");
            Check(a.HasCosmetic,why+": "+a.Id+" keeps cosmetics forever");
            Check(a.DeathSpinAt==-1&&a.DeathSpinSequence==0&&a.GearLossSequence==0,why+": death and loss events belong to the previous round");
        }
        Check(s.State.Drops.Count==0,why+": ground drops never enter the next round");
        Check(s.State.DurationSeconds==480&&Festivals.For(s.State).Name=="Day 1",why+": back to an eight-minute Day 1");
    }

    static void WeekendCarriesCashGearAndStash()
    {
        var s=Crew(40);var next=new[]{"Night 1","Day 2","Night 2"};var seconds=new[]{600.0,480,600};
        for(int level=1;level<4;level++)
        {
            int seed=s.State.Seed;Earn(s,30+level,level,5*level);
            Finish(s,"Success");var a=s.Player("a");string where=next[level-1];
            Check(Where(s)=="0/"+level+"/0"&&Festivals.For(s.State).Name==where,"clearing a level moves on to "+where);
            Check(a.Cash==30+level&&a.Inventory.Count==1&&a.Inventory[0].ItemId=="confetti"&&a.Inventory[0].Count==level&&a.EquippedItemId=="confetti","cash and gear carry into "+where);
            var b=s.Player("b");Check(b.Cash==37+level&&Count(b,"poi_practice")==1&&b.EquippedItemId=="poi_practice","the other player also carries cash and gear into "+where);
            Check(s.State.StashCash==5*level&&a.HasCosmetic,"stash cash and cosmetics carry into "+where);
            Check(a.Effects.Count==0&&a.Wristbands.Count==0&&a.Life=="Alive"&&a.Health==100,"effects, wristbands and injuries stay behind ("+where+")");
            Check(s.State.Seed==seed+1&&s.State.Npcs[0].Observers.Count==0,"the grounds reseed and forget suspicion ("+where+")");
            Check(s.State.DurationSeconds==seconds[level-1]&&s.State.UnlockedFestivalCount==1,where+" length, and nothing unlocks mid-weekend");
        }
        Earn(s,90,2,15);Finish(s,"Success");
        Check(Where(s)=="1/0/0"&&s.State.UnlockedFestivalCount==2,"clearing Night 2 unlocks and moves to the next festival");
        Fresh(s,"a new festival");
        for(int level=0;level<4;level++){Earn(s,50,1,9);Finish(s,"Success");}
        Check(Where(s)=="0/0/1"&&s.State.UnlockedFestivalCount==2,"clearing the last festival starts an encore lap at the first festival");
        Fresh(s,"an encore lap");
        Check(Festivals.For(s.State).QuotaPerCrew==19&&Festivals.For(s.State).Narcs==3,"the encore lap reads the harder row");
        for(int level=0;level<4;level++)Finish(s,"Success");
        Check(Where(s)=="1/0/1","the encore continues to the next festival at the same lap");
    }

    // Stock hidden from the cops is gear too: what the crew stashed, and the stash boxes it placed, carry on with the weekend.
    static void StashesCarryThroughTheWeekend()
    {
        CommandResult Do(FestivalSimulation game,string kind,string item,int amount=1,string target="")=>game.Execute("a",new GameCommand{Id="weekend_"+(++sequence),Kind=kind,ItemId=item,Amount=amount,TargetId=target});
        var s=Crew(42);var a=s.Player("a");s.State.Phase="Playing";
        a.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=3});a.Inventory.Add(new ItemStack{ItemId="stash_box",Count=2});a.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
        a.X=-25;a.Z=-8;Check(Do(s,"Deposit","stock_lsd",3).Accepted,"a hides three stock in the camp-side stash");
        a.X=-10;a.Z=0;Check(Do(s,"Use","stash_box").Accepted,"a places a stash box");var box=s.State.Stashes.Find(x=>x.Id!="stash");
        Check(Do(s,"Deposit","confetti",1,box.Id).Accepted,"a hides confetti in the new box");
        Finish(s,"Success");
        var stash=s.State.Stashes.Find(x=>x.Id=="stash");var carried=s.State.Stashes.Find(x=>x.Id==box.Id);
        Check(s.State.Stashes.Count==2&&stash!=null&&stash.Items.Count==1&&stash.Items[0].ItemId=="stock_lsd"&&stash.Items[0].Count==3,"the stashed stock is still there on Night 1");
        Check(carried!=null&&carried.X==-10&&carried.Z==0&&carried.Items.Count==1&&carried.Items[0].ItemId=="confetti","the placed box and what is in it stay put");
        s.State.Phase="Playing";a=s.Player("a");a.X=5;a.Z=0;
        Check(Do(s,"Use","stash_box").Accepted&&s.State.Stashes.Count==3&&s.State.Stashes.FindAll(x=>x.Id==box.Id).Count==1,"a box placed on Night 1 gets its own id");
        a.X=-25;a.Z=-8;Check(Do(s,"Withdraw","stock_lsd",2).Accepted&&Count(a,"stock_lsd")==2,"the crew takes its stock back out");
        Finish(s,"Time expired");
        Check(s.State.Stashes.Count==1&&s.State.Stashes[0].Id=="stash"&&s.State.Stashes[0].Items.Count==0,"a failed level restarts Day 1 with one empty stash");
    }
    static int Count(PlayerState p,string item)=>p.Inventory.Find(i=>i.ItemId==item)?.Count??0;

    // This seed keeps the weekend savings while death still drops the gear.
    static void DyingKeepsTheWeekendsCash()
    {
        var s=Crew(43);var a=s.Player("a");s.State.Phase="Playing";s.State.SpinSeed=0;a.Cash=87;a.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
        a.Life="Downed";a.DownedRemaining=.05;s.Tick(.1);
        Check(a.Life=="Spirit"&&s.State.Drops.Exists(d=>d.ItemId=="confetti"),"setup: a dies and drops their gear");
        Check(a.Cash==87&&a.DeathCashDropped==0&&a.DeathSpinAmount==87,"the explicit KEEP seed retains the $87 carried this weekend");
        Finish(s,"Success");Check(s.Player("a").Cash==87,"and starts Night 1 with it");
    }

    // Gear carries between levels, so a player can reach camp with all three hands full. G there leaves an item in the crew's
    // shared stash, which carries through the weekend, so they can still buy gear and have the item back later.
    static void FullHandsCanLeaveGearAtCamp()
    {
        var s=Crew(45);var a=s.Player("a");a.Cash=50;
        foreach(var item in new[]{"confetti","merch_bag","poi_practice"})a.Inventory.Add(new ItemStack{ItemId=item,Count=1});
        CommandResult Do(string kind,string item)=>s.Execute("a",new GameCommand{Id="weekend_"+(++sequence),Kind=kind,ItemId=item});
        var shelf=Catalog.ShopPoint(true,s.State.VendorOffers.IndexOf("medical_voucher"));
        void TakeVoucher(){a.X=shelf.X;a.Z=shelf.Z;Check(Do("HoldOffer","medical_voucher").Accepted,"a takes a medical voucher off the shelf");a.X=0;a.Z=7;}
        TakeVoucher();Check(!Do("Buy","medical_voucher").Accepted,"setup: with three things in hand a cannot buy it");
        Check(Do("ReturnOffer","").Accepted,"setup: a puts the voucher back");
        var dropped=Do("Drop","confetti");Check(dropped.Accepted,"at camp a can put the confetti down (got \""+dropped.Reason+"\")");
        var stash=s.State.Stashes.Find(x=>x.Id=="stash");
        Check(!a.Inventory.Exists(i=>i.ItemId=="confetti")&&stash.Items.Exists(i=>i.ItemId=="confetti"&&i.Count==1),"the confetti waits in the crew's shared stash");
        TakeVoucher();Check(Do("Buy","medical_voucher").Accepted,"with a hand free a buys the voucher");
        Finish(s,"Success");Check(s.State.Stashes.Find(x=>x.Id=="stash").Items.Exists(i=>i.ItemId=="confetti"),"and the stash still holds the confetti on Night 1");
    }

    // A camp snapshot saved before weekends holds the old 600 s round length; the level it launches still runs the table's length.
    static void OldCampSnapshotsLaunchTheTablesLength()
    {
        var s=Crew(44);var old=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        foreach(var field in PreWeekendFields)old.Remove(field);
        old["SchemaVersion"]=1;old["DurationSeconds"]=600;
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),Json));
        Check(restored.State.Phase=="Shopping"&&Festivals.For(restored.State).Name=="Day 1","setup: the old camp restores at Day 1");
        foreach(var p in restored.State.Players){p.X=0;p.Z=19;Check(Act(restored,p.Id,"Ready").Accepted,p.Id+" readies");}
        restored.Tick(5.2);var tripper=restored.Player(restored.State.TripperId);
        Check(restored.State.Phase=="Spinning"&&tripper!=null,"setup: the crew leaves camp");
        Check(restored.State.DurationSeconds==480,"the day runs eight minutes, not the old snapshot's ten");
        Check(tripper.Effects.Exists(e=>e.Id==FestivalSimulation.DoseEffect&&e.RemainingSeconds==480),"and the tripper's dose lasts that day");
        restored.Tick(FestivalSimulation.SpinSeconds+.1);foreach(var p in restored.State.Players)Act(restored,p.Id,"MapReady");
        var again=new FestivalSimulation();again.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(restored.State,Json),Json));
        Check(again.State.Phase=="Playing"&&Festivals.For(again.State).Name=="Day 1","once launched, it saves as a current round: its Day 1 stays a day");
    }

    // The fields festival weekends added; a snapshot saved before them has none, and says SchemaVersion 1.
    static readonly string[] PreWeekendFields={"FestivalIndex","LevelIndex","EncoreTier","UnlockedFestivalCount","Doses","TripperBag","Bodies","ReviewAwards","ReviewWinners","Visions","ClueChain","LevelSales","SecondFriend"};

    // A round saved mid-level before weekends was a rescue. It resumes as Night 1, the night it was, not as a quota day, and
    // the crew can still find the friend and win it.
    static void OldRescueSnapshotsResumeAsANight()
    {
        var s=Crew(46);s.State.Phase="Playing";var old=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        foreach(var field in PreWeekendFields)old.Remove(field);
        old["SchemaVersion"]=1;old["DurationSeconds"]=600;old["GateOpened"]=false;
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),Json));
        Check(Festivals.For(restored.State).Name=="Night 1","an old rescue in progress resumes as Night 1, not a quota day");
        var a=restored.Player("a");var friend=restored.State.FriendPosition;a.X=friend.X;a.Z=friend.Z;
        var found=Act(restored,"a","FindFriend");Check(found.Accepted,"its lost friend can be found: an old save holds no tripper's trail to follow (got \""+found.Reason+"\")");
        restored.Tick(2.1);Check(restored.State.FriendFound,"the friend joins the crew");
        friend.X=Festivals.CampGateX;friend.Z=Festivals.CampGateZ;a.X=friend.X;a.Z=friend.Z;
        Check(Act(restored,"a","Extract").Accepted,"with the friend at the way back to camp, the night can end");
        restored.Tick(3.1);Check(restored.State.Phase=="Results"&&restored.State.Result=="Success","and it is won");
    }

    static void FailureRestartsTheFestival()
    {
        var s=Crew(60);Earn(s,44,2,10);Finish(s,"Success");Earn(s,61,3,12);Finish(s,"Success");
        Check(Where(s)=="0/2/0","setup: Day 2 of the first festival");
        Earn(s,70,1,8);Finish(s,"Time expired");
        Check(Where(s)=="0/0/0","failing Day 2 restarts the same festival at Day 1");
        Fresh(s,"a failed weekend");
        Finish(s,"No living free teammate can complete a rescue");
        Check(Where(s)=="0/0/0","failing Day 1 stays on Day 1");
        for(int level=0;level<4;level++)Finish(s,"Success");
        for(int level=0;level<3;level++)Finish(s,"Success");
        Check(Where(s)=="1/3/0"&&s.State.UnlockedFestivalCount==2,"setup: Night 2 of the second festival");
        Earn(s,120,2,30);Finish(s,"Missed the quota");
        Check(Where(s)=="1/0/0"&&s.State.UnlockedFestivalCount==2,"failing the second festival restarts it and keeps it unlocked");
        Fresh(s,"a failed second festival");
    }

    static void HostChoosesAnUnlockedFestival()
    {
        var s=Crew(70);
        Check(!Act(s,"a","ChooseFestival",1).Accepted&&s.State.FestivalIndex==0,"a locked festival cannot be chosen");
        Check(Act(s,"a","ChooseFestival",0).Accepted&&s.State.FestivalIndex==0,"the unlocked first festival can be chosen");
        for(int level=0;level<4;level++)Finish(s,"Success");
        s.State.Phase="Shopping";
        Check(Where(s)=="1/0/0","setup: first festival cleared, at camp before Day 1");
        Check(!Act(s,"b","ChooseFestival",0).Accepted&&s.State.FestivalIndex==1,"only the host chooses");
        Check(!Act(s,"a","ChooseFestival",2).Accepted&&!Act(s,"a","ChooseFestival",-1).Accepted&&s.State.FestivalIndex==1,"no such festival");
        Check(Act(s,"a","ChooseFestival",0).Accepted&&Where(s)=="0/0/0","the host can replay a cleared festival");
        Check(Act(s,"a","ChooseFestival",1).Accepted&&Where(s)=="1/0/0","and switch back to an unlocked one");
        Finish(s,"Success");
        Check(!Act(s,"a","ChooseFestival",0).Accepted,"no switching during the debrief");
        s.State.Phase="Shopping";
        Check(!Act(s,"a","ChooseFestival",0).Accepted&&Where(s)=="1/1/0","no switching mid-weekend");
        s.State.LevelIndex=0;s.State.Phase="Playing";
        Check(!Act(s,"a","ChooseFestival",0).Accepted&&s.State.FestivalIndex==1,"no switching once the level is under way");
    }

    // The host's profile saves unlocks every frame, so the unlock must land the moment Night 2 is won: a host who leaves from
    // the results screen never presses NEXT CAMP. Reaches Results through a real launch and extract, not the Finish poke.
    static void WinningNightTwoUnlocksOnTheResultsScreen()
    {
        var s=Crew(80);
        for(int level=0;level<3;level++)Finish(s,"Success");
        s.State.Phase="Shopping";Check(Where(s)=="0/3/0","setup: debrief over, at camp before Night 2");
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Act(s,p.Id,"Ready");}
        s.Tick(5.2+FestivalSimulation.SpinSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: Night 2 under way");
        s.State.FriendFound=true;s.State.FriendPosition=new WorldPoint(Festivals.CampGateX,Festivals.CampGateZ);
        foreach(var p in s.State.Players){p.X=Festivals.CampGateX;p.Z=Festivals.CampGateZ;}
        Check(Act(s,"a","Extract").Accepted,"the whole crew and the friend are at the gate");s.Tick(3.1);
        Check(s.State.Phase=="Results"&&s.State.Result=="Success","setup: Night 2 is won");
        Check(s.State.UnlockedFestivalCount==2,"winning Night 2 unlocks the next festival on the results screen, before anyone presses NEXT CAMP");
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
    static long LossSequence(PlayerState p)
    {
        var wire=JsonSerializer.SerializeToElement(p,Json);
        return wire.TryGetProperty("GearLossSequence",out var sequence)?sequence.GetInt64():0;
    }
    static void DeathGearLossHasAnOwnerSequence()
    {
        var s=Crew(60);s.State.Phase="Playing";s.State.Npcs.Clear();var a=s.Player("a");var b=s.Player("b");a.Cash=0;
        a.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});a.EquippedItemId="poi_practice";
        a.Life="Downed";a.DownedRemaining=.05;s.Tick(.1);
        Check(a.Inventory.Count==0&&s.State.Drops.Exists(d=>d.ItemId=="poi_practice"),"setup: real death drops the poi");
        Check(LossSequence(a)==1&&LossSequence(b)==0,"only the dying owner receives a persistent first gear-loss sequence, even with zero cash");
        var notice=JsonSerializer.SerializeToElement(a,Json);Check(notice.TryGetProperty("GearLossNotice",out var line)&&line.GetString()=="Your gear dropped where you fell: a friend can grab it","the semantic loss carries the promised owner notice");
        s.Tick(.2);Check(LossSequence(a)==1,"spirit ticks do not repeat the loss event");
        a.Life="Alive";a.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});a.Life="Downed";a.DownedRemaining=.05;s.Tick(.1);
        Check(LossSequence(a)==2,"a second actual gear loss has a distinct owner sequence");
        a.Life="Downed";a.DownedRemaining=.05;s.Tick(.1);
        Check(LossSequence(a)==2,"empty inventory and wristbands alone produce no new gear-loss notice");
    }
    static void DroppedGearNamesTheItem()
    {
        var s=new FestivalSimulation(60);var p=s.AddPlayer("a","A");s.State.Phase="Playing";
        p.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});p.EquippedItemId="poi_practice";
        var result=s.Execute(p.Id,new GameCommand{Id="loop7_drop_notice",Kind="Drop",ItemId="poi_practice"});
        Check(result.Accepted&&s.State.Drops.Exists(d=>d.ItemId=="poi_practice"),"setup: production Drop leaves the poi on the ground");
        Check(result.Reason=="Dropped Practice poi","a successful gear drop names the lost item: got \""+result.Reason+"\"");
    }

}
