using System;
using System.Collections.Generic;
using System.Text.Json;
using Festival.Core;

public static class StarterShelfTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("StarterShelf: "+message);}
    static CommandResult Act(FestivalSimulation s,PlayerState p,string kind,string item="",int amount=0)=>s.Execute(p.Id,new GameCommand{Id="starter"+(sequence++),Kind=kind,ItemId=item,Amount=amount});
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{StartersFollowTheUnchangedGearOffers,EightPlayersCanChooseTheSameStarterWithoutMarketSupply,EachPlayerPaysFiveForOneCombinedStarter,UnpaidAndFailedPurchasesPreserveTheAllowance,PaidAllowanceSurvivesReconnectAndRestoreUntilAFreshCamp,LegacyCampRestoreAddsMissingShelfEntriesWithoutDuplicatingReservations,InvalidStarterCountersCannotRestoreABypass,CampGuidanceNamesTheStarterBeforeDaysAndNights,AllEightPlayersCanPayForTheSameStarter})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" starter shelf test(s) failed:\n"+string.Join("\n",failures));
    }

    static void StartersFollowTheUnchangedGearOffers()
    {
        for(int seed=-100;seed<=200;seed++)
        {
            var offers=Catalog.VendorOffers(seed);
            Check(offers.Count==8,"seed "+seed+" has six gear offers and two starter offers, got "+offers.Count);
            Check(string.Join(",",offers.GetRange(0,3))=="little_spoon,medical_voucher,poi_practice","staples keep their positions");
            Check(string.Join(",",offers.GetRange(6,2))=="stock_lsd,stock_mushrooms","starter kinds occupy slots six and seven");
            Check(new HashSet<string>(offers).Count==8,"all eight offers are distinct");
        }
    }

    static void EightPlayersCanChooseTheSameStarterWithoutMarketSupply()
    {
        var s=new FestivalSimulation(7);
        for(int crew=0;crew<=8;crew++)
        {
            foreach(var item in new[]{"stock_lsd","stock_mushrooms"})
            {
                var supply=s.State.ShopStock.Find(x=>x.ItemId==item);
                Check(supply.CampAvailable==8&&supply.MarketAvailable==0,"crew "+crew+" has eight camp copies and zero market copies of "+item+", got "+supply.CampAvailable+"/"+supply.MarketAvailable);
            }
            if(crew<8)s.AddPlayer("p"+crew,"P"+crew);
        }
        Check(s.State.ShopStock.Find(x=>x.ItemId=="little_spoon").CampAvailable==4,"eight-player gear supply retains its existing scaling");
        Check(s.State.ShopStock.Find(x=>x.ItemId=="poi_led").CampAvailable==1,"rare gear remains one copy");
    }

    static int Count(PlayerState p,string item)=>p.Inventory.Find(x=>x.ItemId==item)?.Count??0;
    static CommandResult Hold(FestivalSimulation s,PlayerState p,string item)
    {
        var point=Catalog.ShopPoint(true,s.State.VendorOffers.IndexOf(item));p.X=point.X;p.Z=point.Z;
        return Act(s,p,"HoldOffer",item);
    }
    static CommandResult Pay(FestivalSimulation s,PlayerState p,string item,int amount=0){p.X=0;p.Z=7;return Act(s,p,"Buy",item,amount);}

    static void EachPlayerPaysFiveForOneCombinedStarter()
    {
        foreach(var chosen in new[]{"stock_lsd","stock_mushrooms"})
        {
            string other=chosen=="stock_lsd"?"stock_mushrooms":"stock_lsd";
            var s=new FestivalSimulation(7);var first=s.AddPlayer("first","First");var second=s.AddPlayer("second","Second");
            foreach(var p in new[]{first,second})
            {
                var hold=Hold(s,p,chosen);Check(hold.Accepted,"camp allows a starter pickup: "+hold.Reason);
                Check(p.Cash==20&&Count(p,chosen)==0,"unpaid pickup changes neither wallet nor inventory");
                var paid=Pay(s,p,chosen);Check(paid.Accepted,"camp seller accepts starter payment: "+paid.Reason);
                Check(p.Cash==15&&Count(p,chosen)==1&&p.EquippedItemId==chosen&&p.HeldOfferId=="","payment charges five and equips exactly one unit");
                Check(s.State.LevelSales==0&&s.State.GrossSales==0,"a starter purchase adds no sales credit");
                foreach(var refused in new[]{chosen,other})
                {
                    var repeat=Hold(s,p,refused);
                    Check(!repeat.Accepted&&repeat.Reason=="One starter each: buy more from festivalgoers","the combined allowance refuses another pickup: "+repeat.Reason);
                }
                // Payment revalidates entitlement even when a restored or forged reservation bypasses the pickup.
                p.HeldOfferId=other;var extra=Pay(s,p,other);
                Check(!extra.Accepted&&extra.Reason=="One starter each: buy more from festivalgoers"&&p.Cash==15&&Count(p,other)==0,"a second payment cannot bypass the combined allowance");
                p.HeldOfferId="";
            }
            Check(s.State.ShopStock.Find(x=>x.ItemId==chosen).CampAvailable==6,"another player retains their own starter allowance");
        }
    }

    static void UnpaidAndFailedPurchasesPreserveTheAllowance()
    {
        var s=new FestivalSimulation(7);var p=s.AddPlayer("payer","Payer");
        Check(Hold(s,p,"stock_lsd").Accepted,"an unpaid starter can be reserved");
        Check(Act(s,p,"ReturnOffer").Accepted&&p.StarterStockBought==0&&s.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable==8,"returning unpaid stock restores supply without spending entitlement");
        Check(Hold(s,p,"stock_mushrooms").Accepted,"returning a starter allows changing kind");
        p.Cash=4;var failed=Pay(s,p,"stock_mushrooms");
        Check(!failed.Accepted&&failed.Reason=="Insufficient cash"&&p.Cash==4&&p.StarterStockBought==0&&p.HeldOfferId=="stock_mushrooms"&&Count(p,"stock_mushrooms")==0,"failed cash check keeps the reservation and allowance");
        p.Cash=20;foreach(var item in new[]{"map","poi_practice","confetti"})p.Inventory.Add(new ItemStack{ItemId=item,Count=1});
        Check(!Pay(s,p,"stock_mushrooms").Accepted&&p.Cash==20&&p.StarterStockBought==0,"full pockets consume neither cash nor entitlement");
        p.Inventory.Clear();p.X=10;p.Z=7;
        Check(!Act(s,p,"Buy","stock_mushrooms").Accepted&&p.Cash==20&&p.StarterStockBought==0,"payment outside seller range consumes nothing");
        Check(!Pay(s,p,"stock_mushrooms",2).Accepted&&p.Cash==20&&p.StarterStockBought==0,"a multi-unit request consumes nothing");
        Check(!Pay(s,p,"stock_lsd").Accepted&&p.Cash==20&&p.StarterStockBought==0,"payment must name the held starter");
        var command=new GameCommand{Id="starter-paid-once",Kind="Buy",ItemId="stock_mushrooms",Amount=1};
        var paid=s.Execute(p.Id,command);Check(paid.Accepted,"a corrected payment succeeds");
        var repeat=s.Execute(p.Id,command);
        Check(repeat.Accepted&&repeat.Sequence==paid.Sequence&&p.Cash==15&&p.StarterStockBought==1&&Count(p,"stock_mushrooms")==1,"retrying the payment delivers exactly one receipt, unit and charge");
        Check(s.State.ShopStock.Find(x=>x.ItemId=="stock_mushrooms").CampAvailable==7,"only the paid reservation consumes a shelf copy");
        var guest=s.AddPlayer("guest","Guest");Check(Hold(s,guest,"stock_lsd").Accepted,"another player reserves a starter");s.Disconnect(guest.Id);
        Check(guest.HeldOfferId==""&&guest.StarterStockBought==0&&s.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable==8,"disconnect returns unpaid stock and preserves entitlement");
        guest=s.AddPlayer(guest.Id,guest.Name);Check(Hold(s,guest,"stock_lsd").Accepted,"the unpaid reconnect can choose a starter again");
        foreach(var member in s.State.Players){member.X=0;member.Z=19;Check(Act(s,member,"Ready").Accepted,"ready with unpaid stock");}
        Check(Act(s,p,"Start").Accepted&&guest.HeldOfferId==""&&guest.StarterStockBought==0&&s.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable==8,"launch returns unpaid stock without spending the allowance");
    }

    static RoundState Saved(RoundState state)=>JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true}),new JsonSerializerOptions{IncludeFields=true});
    static void PaidAllowanceSurvivesReconnectAndRestoreUntilAFreshCamp()
    {
        foreach(int level in new[]{0,1,2,3})foreach(string outcome in new[]{"Success","Time expired"})
        {
            var s=new FestivalSimulation(7);var p=s.AddPlayer("traveller","Traveller");s.State.LevelIndex=level;
            Check(Hold(s,p,"stock_lsd").Accepted&&Pay(s,p,"stock_lsd").Accepted,"every camp, including camps before nights, sells one starter");
            s.Disconnect(p.Id);p=s.AddPlayer(p.Id,p.Name);
            Check(p.StarterStockBought==1&&!Hold(s,p,"stock_mushrooms").Accepted,"reconnect keeps the paid allowance spent");
            var resumed=new FestivalSimulation(99);resumed.Restore(Saved(s.State));s=resumed;p=s.Player(p.Id);
            Check(p.StarterStockBought==1&&!Hold(s,p,"stock_mushrooms").Accepted,"save restoration keeps the paid allowance spent");
            var tent=CampFeatures.Find("tent_1");p.X=tent.X;p.Z=tent.Z;
            Check(s.Execute(p.Id,new GameCommand{Id="enter-after-starter",Kind="EnterCamp",TargetId=tent.Id}).Accepted&&Act(s,p,"ExitCamp").Accepted&&p.StarterStockBought==1,"entering and leaving a camp tent does not create a fresh camp");
            s.State.Phase="Results";s.State.Result=outcome;Check(Act(s,p,"Reset").Accepted,"the host returns to a fresh camp");p=s.Player(p.Id);
            Check(p.StarterStockBought==0&&s.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable==8,"every fresh camp resets the allowance and shelf supply");
            Check(Act(s,p,"FinishReview").Accepted&&Hold(s,p,"stock_mushrooms").Accepted&&Pay(s,p,"stock_mushrooms").Accepted,"after debrief the next camp allows a new paid starter");
            Check(p.StarterStockBought==1,"the next camp spends its own allowance exactly once");
        }
    }

    static void LegacyCampRestoreAddsMissingShelfEntriesWithoutDuplicatingReservations()
    {
        foreach(var phase in new[]{"Shopping","CampReview"})
        {
            var original=new FestivalSimulation(7);var holder=original.AddPlayer("holder","Holder");var gear=original.AddPlayer("gear","Gear");var paid=original.AddPlayer("paid","Paid");
            original.State.Phase=phase;original.State.VendorOffers.RemoveAll(Catalog.StarterStock);original.State.ShopStock.RemoveAll(x=>Catalog.StarterStock(x.ItemId));
            holder.HeldOfferId="stock_lsd";gear.HeldOfferId="poi_practice";paid.StarterStockBought=1;
            original.State.ShopStock.Find(x=>x.ItemId=="poi_practice").CampAvailable=1;
            var restored=new FestivalSimulation(99);restored.Restore(Saved(original.State));
            Check(restored.State.VendorOffers.Count==8&&restored.State.VendorOffers[6]=="stock_lsd"&&restored.State.VendorOffers[7]=="stock_mushrooms",phase+" migration appends missing starter offers");
            Check(restored.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable==6&&restored.State.ShopStock.Find(x=>x.ItemId=="stock_mushrooms").CampAvailable==7,"missing supply accounts conservatively for paid allowance and the held kind");
            Check(restored.Player(holder.Id).HeldOfferId=="stock_lsd"&&restored.Player(gear.Id).HeldOfferId=="poi_practice"&&restored.State.ShopStock.Find(x=>x.ItemId=="poi_practice").CampAvailable==1,"migration retains existing gear and unpaid reservations");
            Check(restored.State.ShopStock.FindAll(x=>Catalog.StarterStock(x.ItemId)).TrueForAll(x=>x.MarketAvailable==0),"migrated starters never enter market supply");
            restored.Restore(Saved(restored.State));Check(restored.State.VendorOffers.Count==8&&restored.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable==6,"restoring twice neither duplicates nor refills starters");
            restored.State.Phase="Shopping";holder=restored.Player(holder.Id);
            Check(Act(restored,holder,"ReturnOffer").Accepted&&restored.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable==7&&holder.StarterStockBought==0,"a migrated unpaid reservation returns exactly one copy and keeps its entitlement");
            Check(Hold(restored,holder,"stock_mushrooms").Accepted&&Pay(restored,holder,"stock_mushrooms").Accepted,"a legacy unpaid player still buys their starter");
        }
        var partial=new FestivalSimulation(7);partial.AddPlayer("old","Old");partial.State.VendorOffers.Remove("stock_lsd");
        var existing=partial.State.ShopStock.Find(x=>x.ItemId=="stock_lsd");existing.CampAvailable=3;existing.MarketAvailable=2;
        partial.Restore(Saved(partial.State));existing=partial.State.ShopStock.Find(x=>x.ItemId=="stock_lsd");
        Check(partial.State.VendorOffers.Count==8&&existing.CampAvailable==3&&existing.MarketAvailable==0,"missing offer migration preserves recorded camp supply and removes legacy market stock");
        partial.State.Phase="Playing";partial.State.VendorOffers.RemoveAll(Catalog.StarterStock);partial.State.ShopStock.RemoveAll(x=>Catalog.StarterStock(x.ItemId));
        partial.Restore(Saved(partial.State));Check(partial.State.VendorOffers.Count==6,"restoring a running level never creates a fresh camp shelf");
    }

    static void InvalidStarterCountersCannotRestoreABypass()
    {
        var s=new FestivalSimulation(7);s.AddPlayer("saved","Saved");var before=s.State;
        foreach(int invalid in new[]{-1,2,int.MaxValue})
        {
            var snapshot=Saved(before);snapshot.Players[0].StarterStockBought=invalid;
            bool refused=false;try{s.Restore(snapshot);}catch(ArgumentException){refused=true;}
            Check(refused&&ReferenceEquals(s.State,before),"invalid starter counters cannot replace the active round: "+invalid);
        }
    }

    static void CampGuidanceNamesTheStarterBeforeDaysAndNights()
    {
        foreach(int level in new[]{0,1,2,3})
        {
            var s=new FestivalSimulation(7);var p=s.AddPlayer("reader","Reader");s.State.LevelIndex=level;
            foreach(bool carrying in new[]{false,true})
            {
                if(carrying)p.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
                string hint=FestivalGuidance.Hint(s.State,p);
                Check(hint.Contains("One starter each at camp")&&hint.Contains("Tongue Stamps or Fun Guys for $5")&&hint.Contains("festivalgoers ($5 each; some are narcs)"),"every camp advertises the starter, price and crowd risk, level "+level+": "+hint);
            }
            Check(Hold(s,p,"stock_lsd").Accepted&&Pay(s,p,"stock_lsd").Accepted,"the advertised starter can be bought");
            string used=FestivalGuidance.Hint(s.State,p);
            Check(used.Contains("Starter bought this camp")&&used.Contains("festivalgoers ($5 each; some are narcs)")&&!used.Contains("One starter each at camp"),"after payment the hint explains the spent allowance: "+used);
            p.X=0;p.Z=19;
            Check(FestivalGuidance.Hint(s.State,p).Contains("Starter bought this camp"),"trailhead guidance retains the starter status");
        }
    }

    static void AllEightPlayersCanPayForTheSameStarter()
    {
        foreach(var item in new[]{"stock_lsd","stock_mushrooms"})
        {
            var s=new FestivalSimulation(7);for(int index=0;index<8;index++)s.AddPlayer("same"+index,"Same"+index);
            foreach(var p in s.State.Players)
            {
                var held=Hold(s,p,item);Check(held.Accepted,"all eight players can reserve "+item+", "+p.Id+": "+held.Reason);
                Check(Pay(s,p,item).Accepted&&p.Cash==15&&Count(p,item)==1&&p.StarterStockBought==1,"all eight players can pay for the same kind");
            }
            Check(s.State.ShopStock.Find(x=>x.ItemId==item).CampAvailable==0,"exactly eight purchased copies exhaust their kind's shelf");
        }
    }
}
