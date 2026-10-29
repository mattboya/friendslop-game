using System;
using System.Collections.Generic;
using Festival.Core;

// POI-1 (Matt, 2026-10-01): hold left click with poi in hand and you spin them, and your friends see it. Who may spin is
// FestivalSimulation.CanSpinPoi, which the host turns into PlayerState.VisualPoiSpin each tick. POI-7 (Matt, 2026-10-07): a spinner
// blends in, so festivalgoers watching grow suspicious 10% slower (19% with the Little Spoon), and Sprint does nothing mid-spin. HUD-9:
// Equip's reply says what the item in hand does: poi name Q's performance and the spin, and stock never says Q, since Q on stock
// takes it yourself. The replies name the spin buttons by action ({Spin}, {SpinSides}); the HUD reads in the bound ones.
public static class PoiSpinTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("PoiSpin: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string item="")=>s.Execute(id,new GameCommand{Id="poispin"+(sequence++),Kind=kind,ItemId=item});
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{PoiInHandSpin,ASwapOnItsWaySpinsWhatItBrings,AnUnpaidShelfItemWinsOverWhatIsEquipped,OnlyTheLivingSpin,NobodyBusySpins,EquipSaysWhatTheItemDoes,CampPoiEquipOnlyOffersTheSpin,HoldingShelfPoiExplainsTheSpin,BuyingPoiExplainsTheSpin,SpinnersBlendIn,NoSprintingWhileSpinning})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" poi spin test(s) failed:\n"+string.Join("\n",failures));
    }

    // A crew of two at the festival (Palm Mirage, or Ember Playa's art cars), p0 owning both poi and some gear.
    static FestivalSimulation Festival(int festival=0)
    {
        var s=new FestivalSimulation(7);s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew reaches the festival");
        s.State.Npcs.Clear();
        foreach(var item in new[]{"poi_practice","poi_led","confetti"})s.Player("p0").Inventory.Add(new ItemStack{ItemId=item,Count=1});
        return s;
    }

    static void PoiInHandSpin()
    {
        var s=Festival();var p=s.Player("p0");
        Check(!FestivalSimulation.CanSpinPoi(p),"empty hands spin nothing");
        foreach(var poi in new[]{"poi_practice","poi_led"}){Check(Act(s,p.Id,"Equip",poi).Accepted,"setup: equip "+poi);Check(FestivalSimulation.CanSpinPoi(p),poi+" in hand spin");}
        Check(Act(s,p.Id,"Equip","confetti").Accepted,"setup: equip confetti");Check(!FestivalSimulation.CanSpinPoi(p),"confetti in hand doesn't");
        Check(Act(s,p.Id,"Equip").Accepted,"setup: hands free");Check(!FestivalSimulation.CanSpinPoi(p),"hands free, nothing spins");
    }

    // Until the host's view confirms a gear swap, the HUD shows the item on its way, not the one the view still holds.
    static void ASwapOnItsWaySpinsWhatItBrings()
    {
        var s=Festival();var p=s.Player("p0");Check(Act(s,p.Id,"Equip","confetti").Accepted,"setup: equip confetti");
        Check(FestivalSimulation.CanSpinPoi(p,"poi_practice"),"a swap to poi spins them before the view has them");
        Check(Act(s,p.Id,"Equip","poi_led").Accepted,"setup: equip LED poi");
        Check(!FestivalSimulation.CanSpinPoi(p,"confetti")&&!FestivalSimulation.CanSpinPoi(p,""),"a swap away from poi, or to free hands, spins nothing");
        p.Life="Downed";Check(!FestivalSimulation.CanSpinPoi(p,"poi_practice"),"the other rules still hold");
    }

    // At camp, an unpaid item off the shelf is what's in hand (FestivalHands shows it over the equipped one).
    static void AnUnpaidShelfItemWinsOverWhatIsEquipped()
    {
        var s=new FestivalSimulation(7);var p=s.AddPlayer("c0","C0");
        void Hold(string item){var shelf=Catalog.ShopPoint(true,s.State.VendorOffers.IndexOf(item));p.X=shelf.X;p.Z=shelf.Z;Check(Act(s,p.Id,"HoldOffer",item).Accepted,"setup: hold "+item);}
        Hold("poi_practice");Check(FestivalSimulation.CanSpinPoi(p),"unpaid poi off the shelf spin");
        Check(Act(s,p.Id,"ReturnOffer").Accepted,"setup: put it back");
        p.Inventory.Add(new ItemStack{ItemId="poi_led",Count=1});Check(Act(s,p.Id,"Equip","poi_led").Accepted,"setup: equip LED poi");
        Hold("medical_voucher");Check(!FestivalSimulation.CanSpinPoi(p),"an unpaid voucher in hand hides the equipped poi");
    }

    static void OnlyTheLivingSpin()
    {
        var s=Festival();var p=s.Player("p0");Check(Act(s,p.Id,"Equip","poi_led").Accepted,"setup: equip LED poi");
        foreach(var life in new[]{"Downed","Detained","Spirit"}){p.Life=life;Check(!FestivalSimulation.CanSpinPoi(p),"a player who is "+life+" doesn't spin");}
    }

    // Doing anything else stops the spin: Q's poi performance, lying down, an art car, the Ferris wheel's kind of ride, a friend
    // dragged or a body carried.
    static void NobodyBusySpins()
    {
        var s=Festival();var p=s.Player("p0");var mate=s.Player("p1");Check(Act(s,p.Id,"Equip","poi_practice").Accepted,"setup: equip poi");
        Check(Act(s,p.Id,"Use","poi_practice").Accepted&&s.Interaction(p.InteractionId)?.Kind=="Poi","setup: Q starts the performance");
        Check(!FestivalSimulation.CanSpinPoi(p),"not during the poi performance");Act(s,p.Id,"Cancel");
        Check(FestivalSimulation.CanSpinPoi(p),"free again, they spin");
        Check(Act(s,p.Id,FestivalSimulation.LieDownKind).Accepted,"setup: lie down");Check(!FestivalSimulation.CanSpinPoi(p),"not lying down");Act(s,p.Id,"Cancel");
        mate.Life="Downed";p.DragTargetId=mate.Id;Check(!FestivalSimulation.CanSpinPoi(p),"not dragging a friend");p.DragTargetId="";
        p.CarryBodyId=mate.Id;Check(!FestivalSimulation.CanSpinPoi(p),"not carrying a body");p.CarryBodyId="";mate.Life="Alive";
        var playa=Festival(1);var rider=playa.Player("p0");Check(Act(playa,rider.Id,"Equip","poi_led").Accepted,"setup: equip on the playa");
        var car=Festivals.ArtCarAt(0,playa.State.ElapsedSeconds);rider.X=car.X+1;rider.Z=car.Z;
        Check(Act(playa,rider.Id,"RideCar").Accepted,"setup: board an art car");Check(!FestivalSimulation.CanSpinPoi(rider),"not riding an art car");
    }

    // POI-7: one festivalgoer at the origin looks north at player a standing backstage (z 35), where being seen raises suspicion
    // each second. The suspicion gained in one second, spinning or not, with or without the Little Spoon.
    static double Seen(bool spinning,bool spoon)
    {
        var s=new FestivalSimulation(3);var a=s.AddPlayer("a","A");s.State.Phase="Playing";
        s.State.Npcs.Clear();s.State.Npcs.Add(new NpcState{Id="watcher",Z=30});a.X=0;a.Z=35;a.VisualPoiSpin=spinning;
        if(spoon)a.Inventory.Add(new ItemStack{ItemId="little_spoon",Count=1});
        s.Tick(1);return s.State.Npcs[0].Observers.Find(o=>o.PlayerId=="a").Suspicion;
    }
    static void SpinnersBlendIn()
    {
        double still=Seen(false,false),spinning=Seen(true,false),spoon=Seen(false,true),both=Seen(true,true);
        Check(still>0,"setup: standing backstage in view raises suspicion ("+still+")");
        Check(Math.Abs(spinning-.9*still)<1e-9,"spinning poi, the watcher grows suspicious 10% slower: "+spinning+" against "+still);
        Check(Math.Abs(spoon-.9*still)<1e-9,"the Little Spoon alone is unchanged: "+spoon+" against "+still);
        Check(Math.Abs(both-.81*still)<1e-9,"spinning with the Little Spoon stacks to .81: "+both+" against "+still);
    }

    // POI-7: the host's own speed rule (FestivalSession.ApplyMovement and TryMove both read MoveSpeed) ignores Sprint mid-spin, so
    // a sprint-length step is refused and a walk-length one taken; let go and sprinting works again.
    static void NoSprintingWhileSpinning()
    {
        var s=Festival();var p=s.Player("p0");p.Effects.Clear();Check(Act(s,p.Id,"Equip","poi_led").Accepted,"setup: equip poi");
        Check(s.MoveSpeed(p,true)==6&&s.MoveSpeed(p,false)==4,"setup: sober and free, Sprint runs at 6 and a walk is 4");
        p.VisualPoiSpin=true;
        Check(s.MoveSpeed(p,true)==4,"spinning, Sprint walks at 4, got "+s.MoveSpeed(p,true));
        float x=p.X;
        Check(!s.TryMove(p.Id,x+.6f,p.Z,90,.1),"the host refuses a sprint-length step mid-spin");
        Check(s.TryMove(p.Id,x+.4f,p.Z,90,.1),"a walk-length step mid-spin is taken");
        Check(p.SprintUntil<=s.State.SimulationSeconds,"a spinner is never seen sprinting");
        p.VisualPoiSpin=false;
        Check(s.MoveSpeed(p,true)==6&&s.TryMove(p.Id,p.X+.6f,p.Z,90,.1),"let go, and Sprint runs again");
    }

    static void BuyingPoiExplainsTheSpin()
    {
        var s=new FestivalSimulation(7);var p=s.AddPlayer("buyer","Buyer");
        foreach(var item in new[]{"poi_practice","poi_led"})
        {
            p.Cash=100;var point=Catalog.ShopPoint(true,s.State.VendorOffers.IndexOf(item));p.X=point.X;p.Z=point.Z;
            Check(Act(s,p.Id,"HoldOffer",item).Accepted,"setup: take poi to seller");p.X=0;p.Z=7;
            var reply=Act(s,p.Id,"Buy",item);
            Check(reply.Accepted,"setup: pay for poi");
            Check(reply.Reason==Catalog.FindItem(item).Name+" bought for $"+Catalog.FindItem(item).Price+". Hold {Spin} to spin them in front of you, {SpinSides} at your sides.","bought poi explain both spins, got \""+reply.Reason+"\"");
            Check(p.EquippedItemId==item&&p.HeldOfferId=="","purchase still equips paid poi");
        }
    }

    static void HoldingShelfPoiExplainsTheSpin()
    {
        var s=new FestivalSimulation(7);var p=s.AddPlayer("shelf","Shelf");
        foreach(var item in new[]{"poi_practice","poi_led"})
        {
            var point=Catalog.ShopPoint(true,s.State.VendorOffers.IndexOf(item));p.X=point.X;p.Z=point.Z;
            var reply=Act(s,p.Id,"HoldOffer",item);
            Check(reply.Accepted,"setup: take poi from shelf");
            Check(reply.Reason=="Holding "+Catalog.FindItem(item).Name+". Bring it to the seller, or press G to return it. Hold {Spin} to spin them in front of you, {SpinSides} at your sides.","shelf poi explain both spins, got \""+reply.Reason+"\"");
            Check(Act(s,p.Id,"ReturnOffer").Accepted,"setup: return shelf poi");
        }
    }

    static void CampPoiEquipOnlyOffersTheSpin()
    {
        var s=new FestivalSimulation(7);var p=s.AddPlayer("camp","Camp");
        foreach(var item in new[]{"poi_practice","poi_led"})
        {
            p.Inventory.Add(new ItemStack{ItemId=item,Count=1});
            var reply=Act(s,p.Id,"Equip",item);
            Check(reply.Accepted,"camp equips owned poi");
            Check(reply.Reason=="Equipped "+Catalog.FindItem(item).Name+". Hold {Spin} to spin them in front of you, {SpinSides} at your sides.","camp offers only the spins, got \""+reply.Reason+"\"");
            Check(Act(s,p.Id,"Use",item).Reason=="Start the round first","camp still refuses Q performance");
        }
    }

    // HUD-9: 1-3 and the wheel send Equip on every switch, and its reply shows for 5 s.
    static void EquipSaysWhatTheItemDoes()
    {
        var s=Festival();var p=s.Player("p0");p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});
        string Reply(string item)=>Act(s,p.Id,"Equip",item).Reason;
        string stock=Reply("stock_lsd"),poi=Reply("poi_led"),gear=Reply("confetti");
        Check(stock=="Equipped Tongue Stamps. Walk up to a festivalgoer to offer it.","stock points at a buyer and never says Q (that takes it yourself), got \""+stock+"\"");
        Check(poi=="Equipped LED poi. Press Q to perform. Hold {Spin} to spin them in front of you, {SpinSides} at your sides.","poi name the performance and both spins, got \""+poi+"\"");
        Check(gear=="Equipped Confetti cannon. Press Q to use.","other gear still says Q, got \""+gear+"\"");
    }
}
