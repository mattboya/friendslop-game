namespace Festival.Core
{
    /// <summary>ECON-2: Tongue Stamps and Fun Guys come from festivalgoers. A player asks one with StartBuy (TargetId the festivalgoer,
    /// ItemId the stock) and talks the buy through like a sale (DIALOG-3, Conversations.cs). Regulars, and clue holders at night, sell one kind each,
    /// Festivals.StockCopies a level; the VIP guard never does. A buyer, the guard or a seller who has run out turn the ask down
    /// exactly alike. DIALOG-3: a narc plays along, its lines carrying cop-speak, and busts whoever finishes the buy. Sellers
    /// never buy, so a buy is never a sale.</summary>
    public sealed partial class FestivalSimulation
    {
        bool Sells(NpcState n)=>SellerCategory(n)||n.Role=="Narc"&&n.Twist!=VipGuard;
        const string NarcBust="Busted! That was an undercover narc.";
        /// <summary>The one kind a festivalgoer sells, by the number their id ends in: even Tongue Stamps, odd Fun Guys. It reads
        /// nothing but the id, so the HUD can name it on every festivalgoer without the label giving a role away.</summary>
        public static string SellerStock(string npcId)=>!string.IsNullOrEmpty(npcId)&&char.IsDigit(npcId[npcId.Length-1])&&(npcId[npcId.Length-1]-'0')%2==1?"stock_mushrooms":"stock_lsd";
        // BeginChallenge's checks for a buy, after its nearby and busy ones; null lets the buy go ahead. Only the role-free checks
        // refuse a narc, who never runs out: it never sells a unit (SettleBuy).
        CommandResult RefuseBuy(PlayerState p,GameCommand c,NpcState npc)
        {
            var item=Catalog.FindItem(SellerStock(npc.Id));
            if(c.ItemId!=item.Id)return Reject("Ask them for "+item.Name);
            if(p.Cash<item.Price)return Reject("Insufficient cash");
            if(!CanAdd(p,item.Id,1))return Reject("Three slots or stack limit reached");
            if(!Sells(npc)||npc.StockSold>=Festivals.StockCopies(QuotaCrew(State)))return Reject("Not selling. They give you a funny look.");
            return null;
        }
        // The price is held while they talk; Cancel gives it back (a cancel, walking off, a disconnect or a bust).
        static void HoldPrice(PlayerState p,InteractionState i){int price=Catalog.FindItem(SellerStock(i.TargetId)).Price;p.Cash-=price;i.ReservedCash=price;}
        // Settled on the talk's score alone: .4 or more spends the held price on one unit, equipped as a shop purchase is (at the
        // buyer's feet if their hands filled meanwhile); below that it comes back. A buy talked through with a narc, however it
        // went, is a bust (Detain gives the price back first); one whose trust dance failed never got that far.
        void SettleBuy(InteractionState i,PlayerState p)
        {
            var seller=State.Npcs.Find(n=>n.Id==i.TargetId);
            if(seller?.Role=="Narc"&&i.Turns.Count>0){Detain(p);return;}
            if(i.Score<.4){p.Cash+=i.ReservedCash;return;}
            string item=SellerStock(i.TargetId);if(seller!=null)seller.StockSold++;
            if(!CanAdd(p,item,1)){State.Drops.Add(new DropState{Id=Id("drop"),ItemId=item,Count=1,X=p.X,Z=p.Z});return;}
            Add(p,item,1);p.EquippedItemId=item;p.VisualReceivedItem=item;p.VisualReceiptSequence++;p.VisualReceiptAt=State.SimulationSeconds;
        }
    }
}
