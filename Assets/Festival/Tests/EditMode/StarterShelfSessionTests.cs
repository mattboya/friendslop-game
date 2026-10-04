using Festival.Core;
using Festival.Network;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class StarterShelfSessionTests
    {
        static int sequence;
        static void BuyStarter(FestivalSimulation game,PlayerState player,string item)
        {
            var shelf=Catalog.ShopPoint(true,game.State.VendorOffers.IndexOf(item));player.X=shelf.X;player.Z=shelf.Z;
            Assert.That(game.Execute(player.Id,new GameCommand{Id="view-hold-"+(sequence++),Kind="HoldOffer",ItemId=item}).Accepted,Is.True);
            player.X=0;player.Z=7;
            Assert.That(game.Execute(player.Id,new GameCommand{Id="view-buy-"+(sequence++),Kind="Buy",ItemId=item}).Accepted,Is.True);
        }

        [Test] public void StarterCountersSerializeOnlyInTheirOwnersViews()
        {
            var game=new FestivalSimulation(7);var owner=game.AddPlayer("owner","Owner");var friend=game.AddPlayer("friend","Friend");
            BuyStarter(game,owner,"stock_lsd");BuyStarter(game,friend,"stock_mushrooms");
            foreach(var recipient in new[]{owner,friend})
            {
                var wire=JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,recipient.Id)));
                Assert.That(wire.Players.Find(p=>p.Id==recipient.Id).StarterStockBought,Is.EqualTo(1),"the owner sees their paid allowance over the wire");
                Assert.That(wire.Players.Find(p=>p.Id!=recipient.Id).StarterStockBought,Is.Zero,"a peer's private starter allowance is redacted");
                wire.Players.Find(p=>p.Id==recipient.Id).StarterStockBought=0;
            }
            Assert.That(owner.StarterStockBought,Is.EqualTo(1));Assert.That(friend.StarterStockBought,Is.EqualTo(1),"filtering and recipient edits never alter host counters");
        }

        [Test] public void OldJsonCampRestoresItsUnpaidStarterAndMissingCounter()
        {
            var old=new FestivalSimulation(7);var player=old.AddPlayer("legacy","Legacy");
            old.State.VendorOffers.RemoveAll(Catalog.StarterStock);old.State.ShopStock.RemoveAll(x=>Catalog.StarterStock(x.ItemId));
            player.HeldOfferId="stock_lsd";
            string json=JsonUtility.ToJson(old.State);
            Assert.That(json,Does.Contain("\"StarterStockBought\":0,"),"the fixture removes an actual serialized field");
            json=json.Replace("\"StarterStockBought\":0,","");
            Assert.That(json,Does.Not.Contain("StarterStockBought"));
            var restored=new FestivalSimulation(99);restored.Restore(JsonUtility.FromJson<RoundState>(json));player=restored.Player(player.Id);
            Assert.That(restored.State.VendorOffers,Does.Contain("stock_lsd"),"old camp snapshots gain missing starter offers");
            Assert.That(player.StarterStockBought,Is.Zero,"missing counters default to an unused allowance");
            Assert.That(restored.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable,Is.EqualTo(7),"the held legacy copy stays reserved");
            Assert.That(restored.Execute(player.Id,new GameCommand{Id="legacy-return",Kind="ReturnOffer"}).Accepted,Is.True);
            Assert.That(restored.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").CampAvailable,Is.EqualTo(8));
            BuyStarter(restored,player,"stock_mushrooms");
            Assert.That(player.Cash,Is.EqualTo(15));Assert.That(player.StarterStockBought,Is.EqualTo(1));
        }
    }
}
