using System;
using System.Collections.Generic;
using Festival.Core;

// POI-1 (Matt, 2026-10-01): hold left click with poi in hand and you spin them, and your friends see it. It is only for show: the
// one rule is who may spin (FestivalSimulation.CanSpinPoi), which the host turns into PlayerState.VisualPoiSpin each tick. HUD-9:
// Equip's reply says what the item in hand does: poi name Q's performance and the spin, and stock never says Q, since Q on stock
// takes it yourself (ECON-3).
public static class PoiSpinTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("PoiSpin: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string item="")=>s.Execute(id,new GameCommand{Id="poispin"+(sequence++),Kind=kind,ItemId=item});
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{PoiInHandSpin,AnUnpaidShelfItemWinsOverWhatIsEquipped,OnlyTheLivingSpin,NobodyBusySpins,EquipSaysWhatTheItemDoes,CampPoiEquipOnlyOffersTheSpin,HoldingShelfPoiExplainsTheSpin,BuyingPoiExplainsTheSpin})
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

    static void BuyingPoiExplainsTheSpin()
    {
        var s=new FestivalSimulation(7);var p=s.AddPlayer("buyer","Buyer");
        foreach(var item in new[]{"poi_practice","poi_led"})
        {
            p.Cash=100;var point=Catalog.ShopPoint(true,s.State.VendorOffers.IndexOf(item));p.X=point.X;p.Z=point.Z;
            Check(Act(s,p.Id,"HoldOffer",item).Accepted,"setup: take poi to seller");p.X=0;p.Z=7;
            var reply=Act(s,p.Id,"Buy",item);
            Check(reply.Accepted,"setup: pay for poi");
            Check(reply.Reason==Catalog.FindItem(item).Name+" bought for $"+Catalog.FindItem(item).Price+". Hold left click to spin them.","bought poi explain the spin, got \""+reply.Reason+"\"");
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
            Check(reply.Reason=="Holding "+Catalog.FindItem(item).Name+". Bring it to the seller, or press G to return it. Hold left click to spin them.","shelf poi explain the spin, got \""+reply.Reason+"\"");
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
            Check(reply.Reason=="Equipped "+Catalog.FindItem(item).Name+". Hold left click to spin them.","camp offers only the supported spin, got \""+reply.Reason+"\"");
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
        Check(poi=="Equipped LED poi. Press Q to perform, hold left click to spin.","poi name the performance and the spin, got \""+poi+"\"");
        Check(gear=="Equipped Confetti cannon. Press Q to use.","other gear still says Q, got \""+gear+"\"");
    }
}
