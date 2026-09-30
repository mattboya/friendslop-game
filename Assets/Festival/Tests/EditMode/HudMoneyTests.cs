using System.IO;
using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;

namespace Festival.Tests
{
    // HUD-3 with PLAYA-1: every amount of money a player reads goes through Festivals.CurrencyName: dollars at Palm Mirage, and on
    // Ember Playa the reader's own odd objects, a different kind for each crew member over the same dollar economy.
    public sealed class HudMoneyTests
    {
        static RoundState Day(int festival)=>new RoundState{FestivalIndex=festival,Phase="Playing",DurationSeconds=Festivals.Level(festival,0,0).DurationSeconds};
        static PlayerState Crew(RoundState s,string id,int ordinal){var p=new PlayerState{Id=id,Name=id,Ordinal=ordinal};s.Players.Add(p);return p;}

        [Test] public void EmberPlayasQuotaCountsInYourObjects()
        {
            // Ember Playa's Day 1 asks 22 from each of two.
            var s=Day(1);var ash=Crew(s,"ash",0);var sam=Crew(s,"sam",1);s.LevelSales=12;
            Assert.That(FestivalHudText.ObjectiveTitle(s,ash),Is.EqualTo("DAY QUOTA  12 BUTTONS / 44 BUTTONS"));
            Assert.That(FestivalHudText.ObjectiveDetail(s,ash),Is.EqualTo("Sell 32 buttons more before sundown."));
            Assert.That(FestivalHudText.ObjectiveTitle(s,sam),Is.EqualTo("DAY QUOTA  12 RAMEN PACKETS / 44 RAMEN PACKETS"),"the same quota in Sam's ramen");
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo("Sell 32 ramen packets more before sundown."));
        }

        [Test] public void ThePlayerCardCountsInYourObjects()
        {
            var palm=Day(0);var you=Crew(palm,"you",1);you.Cash=87;palm.StashCash=5;palm.LevelSales=120;
            Assert.That(FestivalHudText.Vitals(palm,you),Is.EqualTo("HP 100  •  $87  •  STASH $5\nSALES $120  •  ALIVE  •  CROWD CLEAR"));
            var playa=Day(1);var sam=Crew(playa,"sam",1);sam.Cash=87;playa.StashCash=1;playa.LevelSales=120;
            Assert.That(FestivalHudText.Vitals(playa,sam),Is.EqualTo("HP 100  •  87 RAMEN PACKETS  •  STASH 1 RAMEN PACKET\nSALES 120 RAMEN PACKETS  •  ALIVE  •  CROWD CLEAR"),
                "your pocket, the crew's stash and the level's sales, all in your ramen");
        }

        [Test] public void MessagesAndPricesFromTheHostReadInYourMoney()
        {
            // The host's rules write money as "$5": a purchase, a refused Extract, a paid release, an item's description.
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Prism tabs bought for $5"),Is.EqualTo("Prism tabs bought for 5 ramen packets"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,Catalog.FindItem("stock_lsd").Description),Does.StartWith("Sell for up to 10 ramen packets or take"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Release: $10 at holding desk, $1 tip"),Is.EqualTo("Release: 10 ramen packets at holding desk, 1 ramen packet tip"),"every amount in a line");
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Inside the tent"),Is.EqualTo("Inside the tent"));
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.MoneyText(palm,you,"Prism tabs bought for $5"),Is.EqualTo("Prism tabs bought for $5"),"dollars stay dollars at Palm Mirage");
        }

        [Test] public void TheHudLeavesEveryAmountToTheFormatter()
        {
            // The HUD and the shelves' price tags never write a dollar sign themselves: every amount, down to the stash, release and
            // revive buttons and the night market's prompts, goes through FestivalHudText.Money or MoneyText. Paths are from the
            // project root, the test runner's working directory.
            // ponytail: a whole-file scan, so an interpolated log line would trip it too; scan string literals only if that bites.
            foreach(var path in new[]{"Assets/Festival/Runtime/Presentation/FestivalHud.cs","Assets/Festival/Runtime/Presentation/FestivalWorld.cs"})
                Assert.That(File.ReadAllText(path),Does.Not.Contain("$"),path+" writes a dollar sign; use FestivalHudText.Money");
        }
    }
}
