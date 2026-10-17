using System;
using System.Collections.Generic;
using System.Text.Json;
using Festival.Core;

// About one level in five hides a Giggle Tank at one of six listed spots off the beaten path, the same on every festival. It is
// public, not a vision: any living, free player within 1.5 m with a free hand slot grabs it, and it goes for everyone. It pays
// nothing yet: carried home inside the camp gate, alive or downed, it banks $25 in the crew's shared stash (never sale cash, so
// a day's quota doesn't move). Detention, death and the next level lose a tank still out. The stash carries through the
// weekend, and the crew withdraws it at the crew stash on the next level. The weekend's last level has no next level and the
// weekend's end empties the stash, so no tank turns up there.
public static class GiggleTankTests
{
    const string Grab="Grab the Giggle Tank";
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("GiggleTank: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",int amount=0,string item="")=>s.Execute(id,new GameCommand{Id="tank"+(sequence++),Kind=kind,TargetId=target,Amount=amount,ItemId=item});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{AboutOneLevelInFiveHidesATank,TheWeekendsLastLevelHidesNone,TheSpotsAreOffTheBeatenPath,AGrabFillsAHandSlot,CarryingItHomePaysOnce,
            DetentionOrDeathLosesIt,ItChangesHandsLikeAnyGear,TheBankReceiptNamesWhoCarriedItHome,OnlyTheLivingAndFreeGrabIt,ALeftoverTankIsGoneNextLevel,SnapshotsKeepTheTankAndOlderOnesHaveNone})
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

    // 1,000 levels across both festivals and every level of a weekend but the last. A level holds one spot, so never two tanks.
    static void AboutOneLevelInFiveHidesATank()
    {
        Check(Festivals.GiggleTankSpots.Length==6,"six listed spots, got "+Festivals.GiggleTankSpots.Length);
        int tanks=0;var used=new int[Festivals.GiggleTankSpots.Length];
        for(int seed=1;seed<=1000;seed++)
        {
            var s=Leave(seed,seed%Festivals.Count,seed/Festivals.Count%(Festivals.LevelCount-1),1);var at=FestivalSimulation.GiggleTankAt(s.State);
            if(at==null)continue;
            int spot=Array.FindIndex(Festivals.GiggleTankSpots,w=>w.X==at.X&&w.Z==at.Z);
            Check(spot>=0&&spot==s.State.GiggleTankSpot,"seed "+seed+"'s tank sits on a listed spot, got ("+at.X+", "+at.Z+")");
            tanks++;used[spot]++;
        }
        Check(tanks>=170&&tanks<=230,"20% ±3% of 1,000 levels hide a tank, got "+tanks);
        Check(Array.IndexOf(used,0)<0,"every spot turns up: "+string.Join(", ",used));
    }

    // ECON-1: "+$25 for camp" must stay true, and the stash a tank fills on Night 2 is wiped when the weekend ends. The other
    // levels turn one up one time in five, so 200 last levels across both festivals without one is no fluke.
    static void TheWeekendsLastLevelHidesNone()
    {
        for(int seed=1;seed<=200;seed++)
        {
            var s=Leave(seed,seed%Festivals.Count,Festivals.LevelCount-1,1);
            Check(s.State.GiggleTankSpot==-1&&FestivalSimulation.GiggleTankAt(s.State)==null,"seed "+seed+": "+Festivals.For(s.State).Name+" at "+Festivals.Name(s.State.FestivalIndex)+" hides no tank, got spot "+s.State.GiggleTankSpot);
        }
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

    const string Tank="giggle_tank";
    static int Tanks(PlayerState p)=>p.Inventory.Find(i=>i.ItemId==Tank)?.Count??0;
    static bool Dropped(FestivalSimulation s)=>s.State.Drops.Exists(d=>d.ItemId==Tank)||s.State.Transfers.Exists(t=>t.ItemId==Tank);
    // Inside the camp gate's radius, just short of its edge, and just past it.
    static void Home(PlayerState p){p.X=Festivals.CampGateX+Festivals.CampGateRadius-.2f;p.Z=Festivals.CampGateZ;}
    static void NearlyHome(PlayerState p){p.X=Festivals.CampGateX+Festivals.CampGateRadius+.3f;p.Z=Festivals.CampGateZ;}
    static void Fill(PlayerState p){foreach(var id in new[]{"map","confetti","poi_practice"})if(p.Inventory.Find(i=>i.ItemId==id)==null)p.Inventory.Add(new ItemStack{ItemId=id,Count=1});}
    // A cop who saw p deal and detains them on the next tick.
    static void Busted(FestivalSimulation s,PlayerState p)=>s.State.Npcs.Add(new NpcState{Id="cop_tank",Kind="Cop",Mode="ArrestWarning",X=p.X+1,Z=p.Z,Evidence=new List<EvidenceState>{new EvidenceState{PlayerId=p.Id,Kind="WitnessedDeal",DetainAt=s.State.SimulationSeconds}}});
    // The crew at the festival with the tank in p1's hands, grabbed where it stood.
    static FestivalSimulation Grabbed(int from,out PlayerState p)
    {
        var s=WithATank(from);p=s.Player("p1");var at=FestivalSimulation.GiggleTankAt(s.State);p.X=at.X;p.Z=at.Z;
        Check(Act(s,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&Tanks(p)==1,"setup: "+p.Id+" grabs the tank");
        return s;
    }

    static void AGrabFillsAHandSlot()
    {
        var s=WithATank(1);var at=FestivalSimulation.GiggleTankAt(s.State);var p=s.Player("p1");var mate=s.Player("p0");
        int stash=s.State.StashCash,cash=p.Cash,sales=s.State.LevelSales,gross=s.State.GrossSales;bool quota=FestivalSimulation.DayQuotaMet(s.State);
        p.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});p.EquippedItemId="confetti";
        p.X=at.X+1.6f;p.Z=at.Z;
        Check(Offered(s,p)==null,"1.6 m away the tank isn't offered: "+Listed(s,p));
        Check(!Act(s,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&Tanks(p)==0,"and the rules refuse a grab from there");
        p.X=at.X+1.05f;p.Z=at.Z+1.05f;
        var grab=Offered(s,p);Check(grab!=null,"within 1.5 m the HUD offers the tank: "+Listed(s,p));
        grab.Id="tank"+(sequence++);var result=s.Execute(p.Id,grab);
        Check(result.Accepted&&result.Reason=="Giggle Tank! Carry it home to the camp gate: +$25 for camp","the grab says to carry it home, got "+result.Accepted+" \""+result.Reason+"\"");
        Check(Tanks(p)==1,"one Giggle Tank fills a hand slot");
        Check(p.EquippedItemId=="confetti","the grab leaves whatever was in hand there, got "+p.EquippedItemId);
        Check(s.State.StashCash==stash&&p.Cash==cash&&s.State.LevelSales==sales&&s.State.GrossSales==gross&&FestivalSimulation.DayQuotaMet(s.State)==quota,"nothing is paid at the grab: stash +"+(s.State.StashCash-stash));
        Check(s.State.GiggleTankGrabberId==p.Id&&s.State.GiggleTankGrabberName==p.Name,"the round names its grabber, got \""+s.State.GiggleTankGrabberId+"\" \""+s.State.GiggleTankGrabberName+"\"");
        Check(FestivalSimulation.GiggleTankAt(s.State)==null&&s.State.GiggleTankFound,"the tank is gone for everyone");
        Check(Offered(s,p)==null,"so it isn't offered any more: "+Listed(s,p));
        mate.X=at.X;mate.Z=at.Z;var again=Act(s,mate.Id,FestivalSimulation.GrabGiggleTankKind);
        Check(!again.Accepted&&again.Reason=="Someone beat you to the Giggle Tank"&&Tanks(mate)==0&&s.State.GiggleTankGrabberId==p.Id,"a mate's grab, right on the spot, is refused, got \""+again.Reason+"\"");
        Check(Act(s,p.Id,"Use",item:Tank).Reason=="Carry it home to the camp gate: +$25 for camp.","Q on the tank says where it goes");
        var held=Act(s,p.Id,"Equip",item:Tank);
        Check(held.Accepted&&held.Reason=="Equipped Giggle Tank. Carry it home to the camp gate: +$25 for camp."&&p.EquippedItemId==Tank,"switching to it says where it goes, got \""+held.Reason+"\"");

        var full=WithATank(1);var q=full.Player("p1");at=FestivalSimulation.GiggleTankAt(full.State);
        Fill(q);q.Inventory.Add(new ItemStack{ItemId="little_spoon",Count=1});q.X=at.X;q.Z=at.Z;
        var refused=Act(full,q.Id,FestivalSimulation.GrabGiggleTankKind);
        Check(!refused.Accepted&&refused.Reason=="Hands full: free a slot to carry the Giggle Tank","three hand items refuse the grab, got \""+refused.Reason+"\"");
        Check(FestivalSimulation.GiggleTankAt(full.State)!=null&&!full.State.GiggleTankFound&&full.State.GiggleTankGrabberId==""&&Tanks(q)==0,"and the tank stays where it is");
        Check(Act(full,q.Id,"Drop",item:"map").Accepted&&Act(full,q.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&Tanks(q)==1,"a freed slot takes it");
    }

    static void CarryingItHomePaysOnce()
    {
        var s=Grabbed(1,out var p);var mate=s.Player("p0");
        int stash=s.State.StashCash,cash=p.Cash,sales=s.State.LevelSales,gross=s.State.GrossSales;
        s.Tick(1);NearlyHome(p);s.Tick(1);
        Check(s.State.StashCash==stash&&Tanks(p)==1,"away from the gate, and just outside it, the stash waits");
        Home(p);s.Tick(.1);
        Check(s.State.StashCash==stash+Festivals.GiggleTankCash,"one tick inside the gate banks exactly $25, got +"+(s.State.StashCash-stash));
        Check(Tanks(p)==0,"and takes the tank out of their hands");
        Check(p.Cash==cash&&s.State.LevelSales==sales&&s.State.GrossSales==gross,"it is camp money, not sale cash");
        Check(p.GiggleTankBankSequence==1&&mate.GiggleTankBankSequence==0,"the carrier's own receipt counts it");
        s.Tick(2);Check(s.State.StashCash==stash+25&&p.GiggleTankBankSequence==1,"further ticks add nothing");

        var downed=Grabbed(2,out var q);int before=downed.State.StashCash;
        q.Life="Downed";q.DownedRemaining=30;downed.Tick(.5);Check(Tanks(q)==1,"a downed carrier keeps the tank");
        Home(q);downed.Tick(.1);
        Check(downed.State.StashCash==before+25&&Tanks(q)==0,"a downed carrier inside the gate pays too");

        var late=Grabbed(3,out var r);before=late.State.StashCash;Home(r);late.State.ElapsedSeconds=late.State.DurationSeconds-.05;
        late.Tick(.1);
        Check(late.State.Phase=="Results"&&late.State.StashCash==before+25&&Tanks(r)==0,"a carrier home on the level's last tick still banks it, got "+late.State.Phase+" +"+(late.State.StashCash-before));

        var gone=Grabbed(4,out var t);before=gone.State.StashCash;Home(t);gone.Disconnect(t.Id);gone.Tick(.5);
        Check(gone.State.StashCash==before&&Tanks(t)==1,"a carrier who has left the game banks nothing");
    }

    static void DetentionOrDeathLosesIt()
    {
        var s=Grabbed(1,out var p);int stash=s.State.StashCash;
        Busted(s,p);s.Tick(.1);
        Check(p.Life=="Detained"&&Tanks(p)==0&&!Dropped(s),"a carrier detained by a cop loses the tank, and it lands nowhere");
        p.Life="Alive";Home(p);s.Tick(.5);
        Check(s.State.StashCash==stash,"so standing at the gate afterwards pays nothing");

        var d=Grabbed(2,out var q);stash=d.State.StashCash;
        q.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});q.Life="Downed";q.DownedRemaining=.05;d.Tick(.1);
        Check(q.Life=="Spirit"&&Tanks(q)==0,"a carrier who bleeds out loses it");
        Check(!Dropped(d)&&d.State.Drops.Exists(x=>x.ItemId=="confetti"),"other gear drops where they fell, the tank never does");
        Home(q);d.Tick(.5);Check(d.State.StashCash==stash,"and a spirit at the gate pays nothing");
        var alone=Grabbed(3,out var lone);lone.Life="Downed";lone.DownedRemaining=.05;alone.Tick(.1);
        Check(lone.Life=="Spirit"&&lone.GearLossSequence==0,"a tank alone is no gear that dropped where they fell");

        var gate=Grabbed(4,out var r);stash=gate.State.StashCash;Home(r);Busted(gate,r);gate.Tick(.1);
        Check(r.Life=="Detained"&&gate.State.StashCash==stash&&Tanks(r)==0,"an arrest due as the carrier reaches the gate comes first");
        var bleed=Grabbed(5,out var b);stash=bleed.State.StashCash;Home(b);b.Life="Downed";b.DownedRemaining=.05;bleed.Tick(.1);
        Check(b.Life=="Spirit"&&bleed.State.StashCash==stash,"so does a death due as the carrier reaches it");

        foreach(var loss in new[]{"Detained","Spirit"})
        {
            var o=Grabbed(6,out var giver);var taker=o.Player("p0");taker.X=giver.X+1;taker.Z=giver.Z;
            Check(Act(o,giver.Id,"Transfer",taker.Id,1,Tank).Accepted&&Tanks(giver)==0,"setup: the carrier offers a mate the tank");
            Fill(giver);
            if(loss=="Detained")Busted(o,giver);else{giver.Life="Downed";giver.DownedRemaining=.05;}
            o.Tick(.1);
            Check(giver.Life==loss&&Tanks(giver)==0&&Tanks(taker)==0&&!Dropped(o),"a "+loss+" carrier's tank still on offer goes too, never onto the ground beside full hands");
        }
    }

    // A tank is gear in a hand slot: dropped, picked up, handed over or stashed like any other, and whoever carries it home banks it.
    static void ItChangesHandsLikeAnyGear()
    {
        var s=Grabbed(1,out var p);var mate=s.Player("p0");int stash=s.State.StashCash;
        Check(Act(s,p.Id,"Drop",item:Tank).Accepted&&Tanks(p)==0,"the carrier drops it");
        var drop=s.State.Drops.Find(d=>d.ItemId==Tank);Check(drop!=null,"it lies where they dropped it");
        mate.X=drop.X;mate.Z=drop.Z;Check(Act(s,mate.Id,"Pickup",drop.Id).Accepted&&Tanks(mate)==1,"a mate picks it up");
        p.X=mate.X+1;p.Z=mate.Z;
        var offer=Act(s,mate.Id,"Transfer",p.Id,1,Tank);Check(offer.Accepted&&Tanks(mate)==0,"the mate hands it back");
        Check(Act(s,p.Id,"AcceptTransfer",s.State.Transfers.Find(t=>t.ItemId==Tank).Id).Accepted&&Tanks(p)==1,"the carrier takes it");
        var box=s.State.Stashes.Find(x=>x.Id=="stash");p.X=box.X;p.Z=box.Z;
        Check(Act(s,p.Id,"Deposit","stash",1,Tank).Accepted&&Tanks(p)==0&&box.Items.Exists(i=>i.ItemId==Tank),"it goes into the crew stash");
        Home(p);s.Tick(.5);Check(s.State.StashCash==stash,"a stashed tank isn't carried home");
        p.X=box.X;p.Z=box.Z;Check(Act(s,p.Id,"Withdraw","stash",1,Tank).Accepted&&Tanks(p)==1,"and comes back out");
        mate.X=p.X+1;mate.Z=p.Z;Check(Act(s,p.Id,"Transfer",mate.Id,1,Tank).Accepted,"setup: offered to the mate");
        Check(Act(s,mate.Id,"AcceptTransfer",s.State.Transfers.Find(t=>t.ItemId==Tank).Id).Accepted,"setup: the mate takes it");
        Home(mate);s.Tick(.1);
        Check(s.State.StashCash==stash+25&&mate.GiggleTankBankSequence==1&&p.GiggleTankBankSequence==0,"whoever carries it home banks it, once");
    }

    // Snapshots coalesce: a carrier at the gate can lose the tank in the same window as an unrelated rise in the stash.
    static void TheBankReceiptNamesWhoCarriedItHome()
    {
        var s=Grabbed(1,out var p);var mate=s.Player("p0");Home(p);int stash=s.State.StashCash;
        // No tick between these: the carrier drops it at the gate as the mate deposits $25 at the crew stash.
        Check(Act(s,p.Id,"Drop",item:Tank).Accepted,"setup: the carrier drops the tank at the gate");
        var box=s.State.Stashes.Find(x=>x.Id=="stash");mate.X=box.X;mate.Z=box.Z;mate.Cash=25;
        Check(Act(s,mate.Id,"Deposit","stash",25).Accepted,"setup: the mate deposits $25");
        s.Tick(.1);
        Check(Tanks(p)==0&&s.State.StashCash==stash+25&&p.GiggleTankBankSequence==0&&s.State.Drops.Exists(d=>d.ItemId==Tank),"a dropped tank beside an unrelated payout is no bank receipt, and a tank on the ground banks nothing");

        var h=Grabbed(2,out var giver);var taker=h.Player("p0");Home(giver);taker.X=giver.X-1;taker.Z=giver.Z;stash=h.State.StashCash;
        Check(Act(h,giver.Id,"Transfer",taker.Id,1,Tank).Accepted,"setup: a gate handoff");
        Check(Act(h,taker.Id,"AcceptTransfer",h.State.Transfers.Find(t=>t.ItemId==Tank).Id).Accepted,"setup: accepted");
        h.Tick(.1);
        Check(h.State.StashCash==stash+25&&taker.GiggleTankBankSequence==1&&giver.GiggleTankBankSequence==0,"after a gate handoff the receipt names whoever carried it in");
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
            Check(!refused.Accepted&&Tanks(p)==0&&FestivalSimulation.GiggleTankAt(s.State)!=null,"a player who is "+life+" can't grab it, got \""+refused.Reason+"\"");
        }
        p.Life="Alive";
        Check(Offered(s,p)!=null&&Act(s,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&Tanks(p)==1&&s.State.StashCash==stash,"back on their feet, they grab it into their hands");
    }

    static void ALeftoverTankIsGoneNextLevel()
    {
        var s=WithATank(1,1);var p=s.Player("p0");var at=FestivalSimulation.GiggleTankAt(s.State);
        void Again(){s.State.GiggleTankFound=false;p.X=at.X;p.Z=at.Z;Check(Act(s,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted,"setup: a tank forced at the spot is grabbed");}
        Again();Home(p);s.Tick(.1);Check(s.State.StashCash==25,"setup: Day 1 banks one tank");
        var box=s.State.Stashes.Find(x=>x.Id=="stash");
        Again();p.X=box.X;p.Z=box.Z;Check(Act(s,p.Id,"Deposit","stash",1,Tank).Accepted,"setup: one goes into the crew stash");
        var placed=new StashState{Id="stash_box_tank",X=10,Z=-10};placed.Items.Add(new ItemStack{ItemId="confetti",Count=1});s.State.Stashes.Add(placed);
        Again();p.X=placed.X;p.Z=placed.Z;Check(Act(s,p.Id,"Deposit",placed.Id,1,Tank).Accepted,"setup: one goes into a placed stash box");
        p.Inventory.Add(new ItemStack{ItemId="map",Count=1});Again();Check(Act(s,p.Id,"Equip",item:Tank).Accepted,"setup: one is still in hand");
        s.State.Phase="Results";s.State.Result="Success";
        Check(Act(s,p.Id,"Reset").Accepted&&Act(s,p.Id,"FinishReview").Accepted&&s.State.Phase=="Shopping","setup: Day 1 is won and the crew is back at camp");
        p=s.Player("p0");
        Check(Festivals.For(s.State).Name=="Night 1"&&Tanks(p)==0&&p.EquippedItemId!=Tank,"the tank still in hand is gone on Night 1, got equipped \""+p.EquippedItemId+"\"");
        Check(s.State.Stashes.TrueForAll(x=>!x.Items.Exists(i=>i.ItemId==Tank)),"so are the ones in the stash boxes");
        Check(p.Inventory.Exists(i=>i.ItemId=="map")&&s.State.Stashes.Exists(x=>x.Items.Exists(i=>i.ItemId=="confetti")),"other gear carries on");
        Check(s.State.StashCash==25&&s.State.GiggleTankGrabberId==""&&s.State.GiggleTankGrabberName=="","the banked $25 waits in the stash, and no one has grabbed Night 1's tank, got "+s.State.StashCash);
        p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: ready for Night 1");
        s.Tick(5.2);Check(!s.State.GiggleTankFound&&s.State.GiggleTankGrabberId=="","Night 1 rolls a tank of its own");
        Arrive(s);
        int cash=p.Cash;box=s.State.Stashes.Find(x=>x.Id=="stash");p.X=box.X;p.Z=box.Z;
        Check(Act(s,p.Id,"Withdraw","stash",25).Accepted&&p.Cash==cash+25&&s.State.StashCash==0,"at the crew stash the crew withdraws all $25 to spend, got cash +"+(p.Cash-cash)+", stash "+s.State.StashCash);
    }

    static void SnapshotsKeepTheTankAndOlderOnesHaveNone()
    {
        var s=Grabbed(1,out var grabber);var mate=s.Player("p0");mate.GiggleTankBankSequence=2;
        var json=JsonSerializer.SerializeToNode(s.State,Json).AsObject();
        var saved=new FestivalSimulation();saved.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        Check(saved.State.GiggleTankSpot==s.State.GiggleTankSpot&&saved.State.GiggleTankFound&&Tanks(saved.Player("p1"))==1,"a snapshot keeps the level's tank, in its carrier's hands");
        Check(saved.State.GiggleTankGrabberId==grabber.Id&&saved.State.GiggleTankGrabberName==grabber.Name&&saved.Player("p0").GiggleTankBankSequence==2,"and who grabbed it, and each bank receipt");
        json.Remove("GiggleTankSpot");json.Remove("GiggleTankFound");json.Remove("GiggleTankGrabberId");json.Remove("GiggleTankGrabberName");
        foreach(var player in json["Players"].AsArray())player.AsObject().Remove("GiggleTankBankSequence");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        Check(old.State.GiggleTankSpot==-1&&FestivalSimulation.GiggleTankAt(old.State)==null,"a snapshot from before Giggle Tanks has none");
        Check(old.State.GiggleTankGrabberId==""&&old.State.GiggleTankGrabberName==""&&old.Player("p0").GiggleTankBankSequence==0,"and no grabber or receipt");
        var p=old.Player("p0");int stash=old.State.StashCash;
        foreach(var spot in Festivals.GiggleTankSpots){p.X=spot.X;p.Z=spot.Z;Check(!Act(old,p.Id,FestivalSimulation.GrabGiggleTankKind).Accepted&&old.State.StashCash==stash,"so there is nothing to grab at ("+spot.X+", "+spot.Z+")");}
    }
}
