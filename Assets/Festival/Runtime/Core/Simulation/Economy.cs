using System;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    CommandResult HoldOffer(PlayerState p,GameCommand c)
    {
        if(State.Phase!="Shopping"||p.Ready||p.HeldOfferId!="")return Reject("Return your held item before choosing another");
        int index=State.VendorOffers.IndexOf(c.ItemId);
        if(index<0)return Reject("That item is not on the shelf");
        var point=Catalog.ShopPoint(true,index);
        if(!Near(p,point.X,point.Z,2.8))return Reject("Move closer to the shelf");
        var stock=State.ShopStock.Find(s=>s.ItemId==c.ItemId);
        if(stock==null||stock.CampAvailable<=0)return Reject("Sold out or currently held by the crew");
        stock.CampAvailable--;p.HeldOfferId=c.ItemId;
        return Ok("Holding "+Catalog.FindItem(c.ItemId).Name+". Bring it to the seller, or press G to return it.");
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
        int index=State.VendorOffers.IndexOf(c.ItemId);
        var item=Catalog.FindItem(c.ItemId);
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
        p.Cash-=item.Price;Add(p,c.ItemId,1);
        if(c.ItemId!="little_spoon")p.EquippedItemId=c.ItemId;
        if(camp)p.HeldOfferId="";else stock.MarketAvailable--;
        return Ok(item.Name+" bought for $"+item.Price);
    }
    CommandResult Equip(PlayerState p,GameCommand c)
    {
        if(string.IsNullOrEmpty(c.ItemId)) {p.EquippedItemId="";return Ok("Hands free");}
        if(Count(p,c.ItemId)<1)return Reject("Item not owned");
        if(c.ItemId=="little_spoon")return Reject("Little Spoon is worn automatically as a necklace");
        p.EquippedItemId=c.ItemId;
        return Ok("Equipped "+Catalog.FindItem(c.ItemId).Name+". Press Q to use.");
    }
    CommandResult Consume(PlayerState p,GameCommand c){var d=Catalog.FindItem(c.ItemId);if(d==null||string.IsNullOrEmpty(d.EffectId)||Count(p,c.ItemId)<1)return Reject("No consumable stock");if(p.Effects.Count>=2||p.Effects.Exists(e=>e.Id==d.EffectId))return Reject("Maximum two distinct effects; item retained");var effect=Catalog.FindEffect(d.EffectId);if(effect==null)return Reject("Unknown effect");Take(p,c.ItemId,1);p.Effects.Add(new ActiveEffect{Id=effect.Id,InstanceId=Id("effect"),SourceCommandId=c.Id,StartSeconds=State.SimulationSeconds,RemainingSeconds=effect.DurationSeconds});foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Sees(n,p))Adjust(n,p,-15);return Ok();}
    CommandResult Use(PlayerState p,GameCommand c){if(c.ItemId=="map")return Ok("Map is free");if(Count(p,c.ItemId)<1)return Reject("Item not owned");switch(c.ItemId){case "stock_lsd":case "stock_mushrooms":return Consume(p,c);case "poi_practice":case "poi_led":return BeginPoi(p,c);case "stage_pass":return BeginDj(p,c);case "merch_bag":return Ok("Carried bag conceals casual stock visibility");case "medical_voucher":if(!Near(p,24,-20)||p.Effects.Count==0)return Reject("Medical tent and active effect required");return BeginTask(p,"Medical","medical",3);case "confetti":Take(p,c.ItemId,1);foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Distance(p.X,p.Z,n.X,n.Z)<=8&&n.Mode!="Swarming"&&n.Mode!="Accusing")n.DistractedUntil=Math.Max(n.DistractedUntil,State.SimulationSeconds+4);return Ok();case "stash_box":if(Math.Abs(p.X)>35||p.Z>18||Distance(p.X,p.Z,27,5)<6)return Reject("Place on open festival ground away from stage and holding");Take(p,c.ItemId,1);State.Stashes.Add(new StashState{Id=Id("stash"),X=p.X,Z=p.Z});return Ok();default:return Reject("Unsupported use context");}}
    CommandResult Drop(PlayerState p,GameCommand c){if(Count(p,c.ItemId)<1)return Reject("Item not owned");Take(p,c.ItemId,1);State.Drops.Add(new DropState{Id=Id("drop"),ItemId=c.ItemId,Count=1,X=p.X,Z=p.Z});return Ok();}
    CommandResult Pickup(PlayerState p,GameCommand c){var d=State.Drops.Find(x=>x.Id==c.TargetId);if(d==null||!Near(p,d.X,d.Z))return Reject("Drop unavailable or too far away");if(d.ItemId=="wristband"){if(p.Wristbands.Contains(d.OwnerId))return Reject("Band already held");p.Wristbands.Add(d.OwnerId);}else{if(!CanAdd(p,d.ItemId,d.Count))return Reject("Inventory full");Add(p,d.ItemId,d.Count);}State.Drops.Remove(d);return Ok();}
    CommandResult Transfer(PlayerState p,GameCommand c){var other=Player(c.TargetId);if(other==null||other==p||!other.Connected||other.Life!="Alive"||!Near(p,other.X,other.Z)||c.Amount<1||c.Amount>5||Count(p,c.ItemId)<c.Amount)return Reject("Invalid handoff");if(State.Transfers.Exists(t=>t.FromId==p.Id))return Reject("Resolve existing handoff first");Take(p,c.ItemId,c.Amount);State.Transfers.Add(new TransferOffer{Id=Id("offer"),FromId=p.Id,ToId=other.Id,ItemId=c.ItemId,Amount=c.Amount,ExpiresAt=State.SimulationSeconds+15});return Ok("Recipient must accept handoff");}
    CommandResult AcceptTransfer(PlayerState p,GameCommand c){var offer=State.Transfers.Find(x=>x.Id==c.TargetId&&x.ToId==p.Id);var from=offer==null?null:Player(offer.FromId);if(offer==null||from==null||!from.Connected||from.Life!="Alive"||!Near(p,from.X,from.Z)||!CanAdd(p,offer.ItemId,offer.Amount))return Reject("Handoff expired, distant or full");Add(p,offer.ItemId,offer.Amount);State.Transfers.Remove(offer);return Ok();}
    void ReturnOffer(TransferOffer t){var p=Player(t.FromId);if(p!=null&&CanAdd(p,t.ItemId,t.Amount))Add(p,t.ItemId,t.Amount);else if(p!=null)State.Drops.Add(new DropState{Id=Id("drop"),ItemId=t.ItemId,Count=t.Amount,X=p.X,Z=p.Z});State.Transfers.Remove(t);}
    void ReturnOffers(string id){foreach(var t in State.Transfers.ToArray())if(t.FromId==id||t.ToId==id)ReturnOffer(t);}
    CommandResult Stash(PlayerState p,GameCommand c){var stash=State.Stashes.Find(s=>(c.TargetId==""||s.Id==c.TargetId)&&Near(p,s.X,s.Z));if(stash==null||c.Amount<1||c.Amount>1000)return Reject("Choose nearby stash and positive amount");bool deposit=c.Kind=="Deposit";if(c.ItemId==""){if(deposit){if(p.Cash<c.Amount)return Reject("Insufficient cash");p.Cash-=c.Amount;State.StashCash+=c.Amount;}else{if(State.StashCash<c.Amount)return Reject("Insufficient shared cash");State.StashCash-=c.Amount;p.Cash+=c.Amount;}return Ok();}var item=stash.Items.Find(i=>i.ItemId==c.ItemId);if(deposit){if(Count(p,c.ItemId)<c.Amount)return Reject("Insufficient inventory");Take(p,c.ItemId,c.Amount);if(item==null)stash.Items.Add(new ItemStack{ItemId=c.ItemId,Count=c.Amount});else item.Count+=c.Amount;}else{if(item==null||item.Count<c.Amount||!CanAdd(p,c.ItemId,c.Amount))return Reject("Stock unavailable or inventory full");item.Count-=c.Amount;Add(p,c.ItemId,c.Amount);if(item.Count==0)stash.Items.Remove(item);}return Ok();}
}
}
