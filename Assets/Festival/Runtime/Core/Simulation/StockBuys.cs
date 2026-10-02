namespace Festival.Core
{
    /// <summary>ECON-2: Tongue Stamps and Fun Guys come from festivalgoers. A player asks one with StartBuy (TargetId the festivalgoer,
    /// ItemId the stock) and dances the buy like a sale (BeginChallenge). Regulars, and clue holders at night, sell one kind each,
    /// Festivals.StockCopies a level; the VIP guard never does. A buyer, the guard or a seller who has run out turn the ask down
    /// exactly alike, and a narc busts the asker. Sellers never buy, so a buy is never a sale.</summary>
    public sealed partial class FestivalSimulation
    {
        static bool Sells(NpcState n)=>(n.Role=="Regular"||n.Role=="ClueHolder")&&n.Twist!=VipGuard;
        /// <summary>The one kind a festivalgoer sells, by the number their id ends in: even Tongue Stamps, odd Fun Guys. It reads
        /// nothing but the id, so the HUD can name it on every festivalgoer without the label giving a role away.</summary>
        public static string SellerStock(string npcId)=>!string.IsNullOrEmpty(npcId)&&char.IsDigit(npcId[npcId.Length-1])&&(npcId[npcId.Length-1]-'0')%2==1?"stock_mushrooms":"stock_lsd";
        // BeginChallenge's checks for a buy, after its nearby and busy ones; null lets the buy go ahead. The role-free checks come
        // first, so they never probe a role: asking a narc for the wrong kind, short of the price or with full hands busts no one.
        CommandResult RefuseBuy(PlayerState p,GameCommand c,NpcState npc)
        {
            var item=Catalog.FindItem(SellerStock(npc.Id));
            if(c.ItemId!=item.Id)return Reject("Ask them for "+item.Name);
            if(p.Cash<item.Price)return Reject("Insufficient cash");
            if(!CanAdd(p,item.Id,1))return Reject("Three slots or stack limit reached");
            if(npc.Role=="Narc"){Detain(p);return Ok("Busted! That was an undercover narc.");}
            if(!Sells(npc)||npc.StockSold>=Festivals.StockCopies(QuotaCrew(State))){Adjust(npc,p,RefusedSaleSuspicion,packed:false);return Reject("Not selling. They give you a funny look.");}
            return null;
        }
        // The price is held while they dance; Cancel gives it back (a cancel, walking off, a disconnect or a bust).
        static void HoldPrice(PlayerState p,InteractionState i){int price=Catalog.FindItem(SellerStock(i.TargetId)).Price;p.Cash-=price;i.ReservedCash=price;}
        // Settled on the score alone, so DIALOG-3 can turn the buy into a conversation: .4 or more spends the held price on one
        // unit, equipped as a shop purchase is (at the buyer's feet if their hands filled meanwhile); below that it comes back.
        void SettleBuy(InteractionState i,PlayerState p)
        {
            if(i.Score<.4){p.Cash+=i.ReservedCash;return;}
            string item=SellerStock(i.TargetId);var seller=State.Npcs.Find(n=>n.Id==i.TargetId);if(seller!=null)seller.StockSold++;
            if(!CanAdd(p,item,1)){State.Drops.Add(new DropState{Id=Id("drop"),ItemId=item,Count=1,X=p.X,Z=p.Z});return;}
            Add(p,item,1);p.EquippedItemId=item;p.VisualReceivedItem=item;p.VisualReceiptSequence++;p.VisualReceiptAt=State.SimulationSeconds;
        }
    }
}
