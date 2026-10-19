using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;

namespace Festival.Tests
{
    // The session calls UpdateShop every frame, so an unchanged shelf must cost nothing while a change still shows at once.
    public sealed class ShopTagTests
    {
        GameObject worldObject;
        [TearDown] public void TakeItDown(){if(worldObject!=null)Object.DestroyImmediate(worldObject);}

        [Test] public void AnUnchangedShelfFrameMakesNoGarbageAndAChangeStillShows()
        {
            worldObject=new GameObject("Shop tag world");var world=worldObject.AddComponent<FestivalWorld>();world.Build();
            var s=new RoundState{RoundId="shop-tags",Phase="Playing",FestivalIndex=Festivals.PlayaFestival,LevelIndex=1};
            var you=new PlayerState{Id="you",Name="You",Ordinal=1};s.Players.Add(you);
            foreach(var id in new[]{"little_spoon","medical_voucher","poi_practice","merch_bag","stock_lsd"}){s.VendorOffers.Add(id);s.ShopStock.Add(new ShopStockState{ItemId=id,CampAvailable=2,MarketAvailable=2});}
            world.UpdateShop(s,you);
            var tag=Tag(worldObject,"Night market goods","merch_bag");
            Assert.That(tag.text,Does.Contain(Catalog.ShopTag("merch_bag")).And.Contain("2 LEFT"),"setup: the market tag in the viewer's money");
            TestDelegate frame=()=>world.UpdateShop(s,you);frame();
            Assert.That(frame,Is.Not.AllocatingGCMemory(),"a frame with the same stock and money makes no garbage");
            s.ShopStock.Find(x=>x.ItemId=="merch_bag").MarketAvailable=0;world.UpdateShop(s,you);
            Assert.That(tag.text,Does.Contain("SOLD OUT"),"a sale shows on the next frame");
            s.FestivalIndex=0;world.UpdateShop(s,you);
            Assert.That(tag.text,Does.Contain("$"+Catalog.FindItem("merch_bag").Price),"and so does the festival's money");
        }

        static TextMeshPro Tag(GameObject world,string shelf,string id)
        {
            foreach(var t in world.GetComponentsInChildren<Transform>(true))
                if(t.name==shelf){var holder=t.Find("Shop item "+id);Assert.That(holder,Is.Not.Null,"setup: "+id+" has a holder on "+shelf);return holder.GetComponentInChildren<TextMeshPro>(true);}
            Assert.Fail("setup: no "+shelf);return null;
        }
    }
}
