namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        // HUD-12: a sale holds its unit out for the whole talk, including any trust dance. Pocket stock is concealed.
        bool ReservedStockOut(PlayerState p){var i=Interaction(p.InteractionId);return i!=null&&i.PlayerId==p.Id&&i.Status=="Active"&&i.Kind=="Sale"&&Stock(i.ReservedItemId);}
        bool StockVisible(PlayerState p)=>Count(p,"merch_bag")==0&&((Stock(p.EquippedItemId)&&Count(p,p.EquippedItemId)>0)||ReservedStockOut(p));
    }
}
