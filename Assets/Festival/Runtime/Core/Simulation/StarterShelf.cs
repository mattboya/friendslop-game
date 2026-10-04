using System;

namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        const string StarterLimit="One starter each: buy more from festivalgoers";
        string StarterShelfRefusal(PlayerState p,string item)
        {
            if(Catalog.FindItem(item)?.Category!="Stock")return "";
            if(State.Phase!="Shopping"||!Catalog.StarterStock(item))return StockOffShelves;
            return p.StarterStockBought>0?StarterLimit:"";
        }
        // Only missing camp records are filled. Paid kinds may already be consumed or traded, so each missing
        // kind reserves capacity for the remaining unpaid players; existing recorded supply is never refilled.
        static void RestoreStarterShelf(RoundState state)
        {
            if(state.Phase!="Shopping"&&state.Phase!="CampReview")return;
            int paid=state.Players.FindAll(p=>p.StarterStockBought>0).Count;
            foreach(var item in new[]{"stock_lsd","stock_mushrooms"})
            {
                if(!state.VendorOffers.Contains(item))state.VendorOffers.Add(item);
                var stock=state.ShopStock.Find(x=>x.ItemId==item);
                if(stock==null)
                {
                    int held=state.Players.FindAll(p=>p.HeldOfferId==item).Count;
                    stock=new ShopStockState{ItemId=item,CampAvailable=Math.Max(0,Catalog.ShopCopies(item,8)-paid-held)};state.ShopStock.Add(stock);
                }
                stock.MarketAvailable=0;
            }
        }
        void SpendStarterAllowance(PlayerState p,string item){if(Catalog.StarterStock(item))p.StarterStockBought++;}
    }
}
