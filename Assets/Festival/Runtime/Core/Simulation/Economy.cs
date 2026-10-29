using System;
using System.Collections.Generic;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    // ECON-6: camp has a limited starter allowance; festival market shelves still sell gear only.
    const string StockOffShelves="Stock comes from festivalgoers, not shelves";
    CommandResult HoldOffer(PlayerState p,GameCommand c)
    {
        if(State.Phase!="Shopping"||p.Ready||p.HeldOfferId!="")return Reject("Return your held item before choosing another");
        string starterRefusal=StarterShelfRefusal(p,c.ItemId);if(starterRefusal!="")return Reject(starterRefusal);
        int index=State.VendorOffers.IndexOf(c.ItemId);
        if(index<0)return Reject("That item is not on the shelf");
        var point=Catalog.ShopPoint(true,index);
        if(!Near(p,point.X,point.Z,2.8))return Reject("Move closer to the shelf");
        var stock=State.ShopStock.Find(s=>s.ItemId==c.ItemId);
        if(stock==null||stock.CampAvailable<=0)return Reject("Sold out or currently held by the crew");
        stock.CampAvailable--;p.HeldOfferId=c.ItemId;
        return Ok("Holding "+Catalog.FindItem(c.ItemId).Name+". Bring it to the seller, or press G to return it."+(IsPoi(c.ItemId)?" "+SpinWords:""));
    }
    CommandResult ReturnHeldOffer(PlayerState p)
    {
        if(p.HeldOfferId=="")return Reject("Nothing unpaid to return");
        var stock=State.ShopStock.Find(s=>s.ItemId==p.HeldOfferId);
        if(stock!=null)stock.CampAvailable++;
        p.HeldOfferId="";
        return Ok("Returned to the shelf");
    }
    CommandResult Buy(PlayerState p,GameCommand c)
    {
        if(c.ItemId==VipWristband)return BuyVipWristband(p);
        int index=State.VendorOffers.IndexOf(c.ItemId);
        var item=Catalog.FindItem(c.ItemId);
        string starterRefusal=StarterShelfRefusal(p,c.ItemId);if(starterRefusal!="")return Reject(starterRefusal);
        if(index<0||item==null||c.Amount>1||c.Amount<0)return Reject("Invalid offer or amount");
        var stock=State.ShopStock.Find(s=>s.ItemId==c.ItemId);
        if(stock==null)return Reject("Offer unavailable");
        bool camp=State.Phase=="Shopping";
        if(camp)
        {
            if(p.HeldOfferId!=c.ItemId)return Reject("Pick up that item from the shelf first");
            if(!Near(p,0,7,2.7))return Reject("Bring the item to the seller");
        }
        else
        {
            var point=Catalog.ShopPoint(false,index);
            if(!Near(p,point.X,point.Z,2.8))return Reject("Buy at the night-market shelf");
            if(stock.MarketAvailable<=0)return Reject("Sold out at the night market");
        }
        if(p.Cash<item.Price)return Reject("Insufficient cash");
        if(!CanAdd(p,c.ItemId,1))return Reject("Three slots or stack limit reached");
        p.Cash-=item.Price;Add(p,c.ItemId,1);SpendStarterAllowance(p,c.ItemId);
        if(c.ItemId!="little_spoon")p.EquippedItemId=c.ItemId;
        if(camp)p.HeldOfferId="";else stock.MarketAvailable--;
        p.VisualReceivedItem=c.ItemId;p.VisualReceiptSequence++;p.VisualReceiptAt=State.SimulationSeconds;
        return Ok(item.Name+" bought for $"+item.Price+(IsPoi(c.ItemId)?". "+SpinWords:""));
    }
    CommandResult Equip(PlayerState p,GameCommand c)
    {
        if(string.IsNullOrEmpty(c.ItemId)) {p.EquippedItemId="";return Ok("Hands free");}
        if(Count(p,c.ItemId)<1)return Reject("Item not owned");
        if(c.ItemId=="little_spoon")return Reject("Little Spoon is worn automatically as a necklace");
        p.EquippedItemId=c.ItemId;
        // HUD-9: 1-3 and the wheel send this on every switch. Q on stock takes it yourself (ECON-3), so stock never says Q. POI-1: poi spin too.
        return Ok("Equipped "+Catalog.FindItem(c.ItemId).Name+". "+(IsPoi(c.ItemId)?(State.Phase=="Shopping"?SpinWords:"Press Q to perform. "+SpinWords):Stock(c.ItemId)?"Walk up to a festivalgoer to offer it.":c.ItemId==GiggleTankItem?Catalog.FindItem(c.ItemId).Description:"Press Q to use."));
    }
    CommandResult Consume(PlayerState p,GameCommand c){var d=Catalog.FindItem(c.ItemId);if(d==null||string.IsNullOrEmpty(d.EffectId)||Count(p,c.ItemId)<1)return Reject("No consumable stock");if(p.Effects.FindAll(Treatable).Count>=2||p.Effects.Exists(e=>e.Id==d.EffectId))return Reject("Maximum two distinct effects; item retained");var effect=Catalog.FindEffect(d.EffectId);if(effect==null)return Reject("Unknown effect");Take(p,c.ItemId,1);News(p,"Items","TOOK "+d.Name+"  •  "+Count(p,c.ItemId)+" left");p.Effects.Add(new ActiveEffect{Id=effect.Id,InstanceId=Id("effect"),SourceCommandId=c.Id,StartSeconds=State.SimulationSeconds,RemainingSeconds=effect.DurationSeconds});foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Sees(n,p))Adjust(n,p,-15);return Ok();}
    CommandResult Use(PlayerState p,GameCommand c){if(c.ItemId=="map")return Ok("Map is free");if(Count(p,c.ItemId)<1)return Reject("Item not owned");switch(c.ItemId){case "stock_lsd":case "stock_mushrooms":return Consume(p,c);case "poi_practice":case "poi_led":return BeginPoi(p,c);case "stage_pass":return BeginDj(p,c);case "merch_bag":return Ok("Carried bag conceals casual stock visibility");case "medical_voucher":if(!Near(p,24,-20)||!p.Effects.Exists(Treatable))return Reject("Medical tent and active effect required");return BeginTask(p,"Medical","medical",3);case ConfettiItem:return Confetti(p);case "stash_box":if(Math.Abs(p.X)>35||p.Z>18||Distance(p.X,p.Z,27,5)<6||Festivals.SecurityBuilding.Contains(p.X,p.Z,1.5f))return Reject("Place on open festival ground away from stage and holding");Take(p,c.ItemId,1);State.Stashes.Add(new StashState{Id=Id("stash"),X=p.X,Z=p.Z});News(p,"Items","STASH BOX PLACED  •  the crew can stash here");return Ok();case GiggleTankItem:return Reject(Catalog.FindItem(c.ItemId).Description);default:return Reject("Unsupported use context");}}
    // ESC-2: during a security stop G ditches stock first, whatever is in hand (SecurityStops.cs). ESC-3: one unit a G, saying how
    // many are left, since the stop holds until the last is gone.
    CommandResult Drop(PlayerState p,GameCommand c){bool stopped=Stopped(p);string item=stopped&&!Stock(c.ItemId)?p.Inventory.Find(i=>Stock(i.ItemId))?.ItemId??c.ItemId:c.ItemId;if(Count(p,item)<1)return Reject("Item not owned");Take(p,item,1);State.Drops.Add(new DropState{Id=Id("drop"),ItemId=item,Count=1,X=p.X,Z=p.Z});return stopped&&Stock(item)?Ok("DITCHED 1 "+Catalog.FindItem(item).Name+"  •  "+Count(p,item)+" left"):Ok("Dropped "+Catalog.FindItem(item).Name);}
    // Gear carries between levels, so hands can be full at camp. There is no festival ground to drop on there: G leaves the item
    // in the crew's shared stash, which carries through the weekend, so full hands never keep anyone from the shelf.
    CommandResult LeaveAtCamp(PlayerState p,GameCommand c){var stash=State.Stashes.Find(s=>s.Id=="stash");if(stash==null||Count(p,c.ItemId)<1)return Reject("Item not owned");Take(p,c.ItemId,1);var item=stash.Items.Find(i=>i.ItemId==c.ItemId);if(item==null)stash.Items.Add(new ItemStack{ItemId=c.ItemId,Count=1});else item.Count++;CrewNews(p,"Stash","STASH  •  +1 "+Catalog.FindItem(c.ItemId).Name+"  •  "+p.Name);return Ok(Catalog.FindItem(c.ItemId).Name+" left in the shared stash");}
    // The same authoritative landing boundary controls views and claims; old drops land immediately.
    static bool CashLanded(RoundState s,DropState d)=>Finite(d.LandsAt)&&WorldSeconds(s)>=WorldSeconds(s,d.LandsAt);
    public static List<DropState> VisibleDrops(RoundState s)=>s.Drops.FindAll(d=>d!=null&&(d.ItemId!="cash"||CashLanded(s,d)));
    CommandResult Pickup(PlayerState p,GameCommand c){var d=State.Drops.Find(x=>x.Id==c.TargetId);if(d==null||!Near(p,d.X,d.Z))return Reject("Drop unavailable or too far away");if(d.ItemId=="cash"){if(!CashLanded(State,d))return Reject("Cash has not landed");if(d.Count<=0||p.Cash>int.MaxValue-d.Count)return Reject("Invalid cash pile");p.Cash+=d.Count;if(!d.CashFind)News(p,"Cash",MoneyNotice("PICKED UP CASH",d.Count));}else if(d.ItemId=="wristband"){if(p.Wristbands.Contains(d.OwnerId))return Reject("Band already held");p.Wristbands.Add(d.OwnerId);News(p,"Items","PICKED UP "+(Player(d.OwnerId)?.Name??"a friend")+"'s wristband");}else{if(!CanAdd(p,d.ItemId,d.Count))return Reject("Inventory full");Add(p,d.ItemId,d.Count);News(p,"Items","PICKED UP "+(d.Count>1?d.Count+" ":"")+Catalog.FindItem(d.ItemId)?.Name);}State.Drops.Remove(d);return Ok();}
    public static bool CanHandoff(PlayerState a,PlayerState b){if(a==null||b==null||a.CampVisitId!=b.CampVisitId)return false;return a.CampVisitId!=""?Distance(a.CampInteriorX,a.CampInteriorZ,b.CampInteriorX,b.CampInteriorZ)<=2.5:Near(a,b.X,b.Z);}
    CommandResult Transfer(PlayerState p,GameCommand c){var other=Player(c.TargetId);if(other==null||other==p||!other.Connected||other.Life!="Alive"||!CanHandoff(p,other)||c.Amount<1||c.Amount>5||Count(p,c.ItemId)<c.Amount)return Reject("Invalid handoff");if(State.Transfers.Exists(t=>t.FromId==p.Id))return Reject("Resolve existing handoff first");Take(p,c.ItemId,c.Amount);State.Transfers.Add(new TransferOffer{Id=Id("offer"),FromId=p.Id,ToId=other.Id,ItemId=c.ItemId,Amount=c.Amount,ExpiresAt=State.SimulationSeconds+15});return Ok("Recipient must accept handoff");}
    CommandResult AcceptTransfer(PlayerState p,GameCommand c){var offer=State.Transfers.Find(x=>x.Id==c.TargetId&&x.ToId==p.Id);var from=offer==null?null:Player(offer.FromId);if(offer==null||from==null||!from.Connected||from.Life!="Alive"||!CanHandoff(p,from)||!CanAdd(p,offer.ItemId,offer.Amount))return Reject("Handoff expired, distant or full");Add(p,offer.ItemId,offer.Amount);string given=Catalog.FindItem(offer.ItemId)?.Name;News(p,"Items","GOT "+offer.Amount+" "+given+"  •  from "+from.Name);News(from,"Items","HANDED OVER "+given+"  •  "+p.Name+" took it");p.VisualReceivedItem=offer.ItemId;p.VisualReceiptSequence++;p.VisualReceiptAt=State.SimulationSeconds;State.Transfers.Remove(offer);return Ok();}
    CommandResult CancelTransfer(PlayerState p,GameCommand c){var offer=State.Transfers.Find(x=>x.FromId==p.Id&&x.Id==c.TargetId);if(offer==null)return Reject("No handoff to cancel");ReturnOffer(offer);return Ok("Handoff cancelled");}
    void ReturnOffer(TransferOffer t){var p=Player(t.FromId);string name=Catalog.FindItem(t.ItemId)?.Name;if(p!=null&&CanAdd(p,t.ItemId,t.Amount)){Add(p,t.ItemId,t.Amount);News(p,"Items","HANDOFF ENDED  •  "+name+" back in your hand");}else if(p!=null){State.Drops.Add(new DropState{Id=Id("drop"),ItemId=t.ItemId,Count=t.Amount,X=p.X,Z=p.Z});News(p,"Items","HANDOFF ENDED  •  "+name+" dropped at your feet");}State.Transfers.Remove(t);}
    void ReturnOffers(string id){foreach(var t in State.Transfers.ToArray())if(t.FromId==id||t.ToId==id)ReturnOffer(t);}
    void StashItemNews(PlayerState p,GameCommand c,bool deposit){string name=Catalog.FindItem(c.ItemId)?.Name;News(p,"Items",(deposit?"INTO":"FROM")+" THE STASH  •  "+c.Amount+" "+name);CrewNews(p,"Stash","STASH  •  "+(deposit?"+":"−")+c.Amount+" "+name+"  •  "+p.Name);}
    CommandResult Stash(PlayerState p,GameCommand c){var stash=State.Stashes.Find(s=>(c.TargetId==""||s.Id==c.TargetId)&&Near(p,s.X,s.Z));if(stash==null||c.Amount<1||c.Amount>1000)return Reject("Choose nearby stash and positive amount");bool deposit=c.Kind=="Deposit";if(c.ItemId==""){if(deposit){if(p.Cash<c.Amount)return Reject("Insufficient cash");p.Cash-=c.Amount;State.StashCash+=c.Amount;News(p,"Cash",MoneyNotice("INTO THE STASH",-c.Amount));CrewNews(p,"Stash",MoneyNotice("STASH",c.Amount)+"  •  "+p.Name);}else{if(State.StashCash<c.Amount)return Reject("Insufficient shared cash");State.StashCash-=c.Amount;p.Cash+=c.Amount;News(p,"Cash",MoneyNotice("FROM THE STASH",c.Amount));CrewNews(p,"Stash",MoneyNotice("STASH",-c.Amount)+"  •  "+p.Name);}return Ok();}var item=stash.Items.Find(i=>i.ItemId==c.ItemId);if(deposit){if(Count(p,c.ItemId)<c.Amount)return Reject("Insufficient inventory");Take(p,c.ItemId,c.Amount);if(item==null)stash.Items.Add(new ItemStack{ItemId=c.ItemId,Count=c.Amount});else item.Count+=c.Amount;StashItemNews(p,c,true);}else{if(item==null||item.Count<c.Amount||!CanAdd(p,c.ItemId,c.Amount))return Reject("Stock unavailable or inventory full");item.Count-=c.Amount;Add(p,c.ItemId,c.Amount);if(item.Count==0)stash.Items.Remove(item);StashItemNews(p,c,false);}return Ok();}
}
}
