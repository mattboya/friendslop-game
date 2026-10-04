using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class StarterShelfPlayTests:FestivalPlayModeTest
    {
        // Commands and receipts cross the real hosted session; positions are fixtures for authoritative reach checks.
        [UnityTest] public IEnumerator HostAndJoinedFriendPayForTheirOwnStartersAndSeePrivateCounters()
        {
            var world=new GameObject("Starter session world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Starter host");var host=hostObject.AddComponent<FestivalSession>();
            var friendObject=new GameObject("Starter friend");var friend=friendObject.AddComponent<FestivalSession>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Starter host",port);
                for(float deadline=Time.realtimeSinceStartup+30;host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,"host connects: "+host.Message);
                friend.Join("Starter friend","127.0.0.1",port);
                for(float deadline=Time.realtimeSinceStartup+30;friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,"friend joins: "+friend.Message);
                var game=Simulation(host);game.State.LevelIndex=1;
                foreach(var owner in new[]{host,friend})
                {
                    var player=game.Player(owner.LocalPlayerId);string item=owner==host?"stock_lsd":"stock_mushrooms";
                    var shelf=Catalog.ShopPoint(true,game.State.VendorOffers.IndexOf(item));player.X=shelf.X;player.Z=shelf.Z;
                    yield return new WaitForSeconds(.3f);
                    owner.Command("HoldOffer",item:item);
                    for(float deadline=Time.realtimeSinceStartup+5;player.HeldOfferId!=item&&Time.realtimeSinceStartup<deadline;)yield return null;
                    Assert.That(player.HeldOfferId,Is.EqualTo(item),"the session reserves a starter before a night: "+owner.Message);
                    Assert.That(player.Cash,Is.EqualTo(20));Assert.That(player.StarterStockBought,Is.Zero);
                    player.X=0;player.Z=7;yield return new WaitForSeconds(.3f);
                    owner.Command("Buy",item:item);
                    string receipt=Catalog.FindItem(item).Name+" bought for $5";
                    for(float deadline=Time.realtimeSinceStartup+5;(owner.LocalPlayer.StarterStockBought!=1||owner.Message!=receipt)&&Time.realtimeSinceStartup<deadline;)yield return null;
                    Assert.That(owner.LocalPlayer.StarterStockBought,Is.EqualTo(1),"the paid starter allowance reaches its owner");
                    Assert.That(owner.Message,Is.EqualTo(receipt),"the real session delivers the purchase reply");
                    Assert.That(owner.LocalPlayer.Cash,Is.EqualTo(15));Assert.That(owner.LocalPlayer.EquippedItemId,Is.EqualTo(item));
                    Assert.That(owner.LocalPlayer.Inventory.Find(x=>x.ItemId==item).Count,Is.EqualTo(1));
                    string other=item=="stock_lsd"?"stock_mushrooms":"stock_lsd";
                    shelf=Catalog.ShopPoint(true,game.State.VendorOffers.IndexOf(other));player.X=shelf.X;player.Z=shelf.Z;
                    yield return new WaitForSeconds(.3f);owner.Command("HoldOffer",item:other);
                    for(float deadline=Time.realtimeSinceStartup+5;owner.Message!="One starter each: buy more from festivalgoers"&&Time.realtimeSinceStartup<deadline;)yield return null;
                    Assert.That(owner.Message,Is.EqualTo("One starter each: buy more from festivalgoers"));
                    Assert.That(player.Cash,Is.EqualTo(15));Assert.That(player.HeldOfferId,Is.Empty);
                }
                yield return new WaitForSeconds(.3f);
                Assert.That(host.State.Players.Find(p=>p.Id==friend.LocalPlayerId).StarterStockBought,Is.Zero);
                Assert.That(friend.State.Players.Find(p=>p.Id==host.LocalPlayerId).StarterStockBought,Is.Zero);
                Assert.That(game.Player(host.LocalPlayerId).StarterStockBought,Is.EqualTo(1));
                Assert.That(game.Player(friend.LocalPlayerId).StarterStockBought,Is.EqualTo(1));
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
        }
    }
}
