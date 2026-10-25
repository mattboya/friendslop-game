using System.IO;
using System.Linq;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
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
            Assert.That(FestivalHudText.ObjectiveDetail(s,ash),Is.EqualTo("Sell 32 buttons more before sundown. You have nothing to sell: buy from festivalgoers, and some are narcs."));
            Assert.That(FestivalHudText.ObjectiveTitle(s,sam),Is.EqualTo("DAY QUOTA  12 RAMEN PACKETS / 44 RAMEN PACKETS"),"the same quota in Sam's ramen");
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo("Sell 32 ramen packets more before sundown. You have nothing to sell: buy from festivalgoers, and some are narcs."));
        }

        [Test] public void ThePlayerCardCountsInYourObjects()
        {
            var palm=Day(0);var you=Crew(palm,"you",1);you.Cash=87;palm.StashCash=5;palm.LevelSales=120;
            Assert.That(FestivalHudText.Vitals(palm,you),Is.EqualTo("HP 100  •  $87  •  STASH $5\nSALES $120  •  ALIVE"));
            var playa=Day(1);var sam=Crew(playa,"sam",1);sam.Cash=87;playa.StashCash=1;playa.LevelSales=120;
            Assert.That(FestivalHudText.Vitals(playa,sam),Is.EqualTo("HP 100  •  87 RAMEN PACKETS  •  STASH 1 RAMEN PACKET\nSALES 120 RAMEN PACKETS  •  ALIVE"),
                "your pocket, the crew's stash and the level's sales, all in your ramen");
        }

        [Test] public void MessagesAndPricesFromTheHostReadInYourMoney()
        {
            // The host's rules write money as "$5": a purchase, a refused Extract, a paid release, an item's description.
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Tongue Stamps bought for $5"),Is.EqualTo("Tongue Stamps bought for 5 ramen packets"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,Catalog.FindItem("stock_lsd").Description),Does.StartWith("Clean sales pay 10 ramen packets and up; ask High or Greedy for more. Or take"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Release: $10 at holding desk, $1 tip"),Is.EqualTo("Release: 10 ramen packets at holding desk, 1 ramen packet tip"),"every amount in a line");
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Inside the tent"),Is.EqualTo("Inside the tent"));
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.MoneyText(palm,you,"Tongue Stamps bought for $5"),Is.EqualTo("Tongue Stamps bought for $5"),"dollars stay dollars at Palm Mirage");
        }

        // ECON-2: beside any festivalgoer the HUD offers to buy the one kind they would sell, read off their id alone (an id ending in
        // an even digit sells Tongue Stamps, an odd one Fun Guys), at its price in your money. A buy that hands you a unit says so
        // in dollars, as the host's replies do, for MoneyText to turn into your objects; a missed or cancelled one says nothing.
        [Test] public void ABuyFromAFestivalgoerReadsInYourMoney()
        {
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.BuyAction(palm,you,"wook_4"),Is.EqualTo("Buy Tongue Stamps  •  $5"));
            Assert.That(FestivalHudText.BuyAction(palm,you,"wook_17"),Is.EqualTo("Buy Fun Guys  •  $5"));
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.BuyAction(playa,sam,"wook_4"),Is.EqualTo("Buy Tongue Stamps  •  5 ramen packets"),"the price in Sam's ramen");
            Assert.That(FestivalHudText.BuyReply("stock_lsd",15,15),Is.EqualTo("Bought Tongue Stamps for $5"),"the held price stayed spent");
            Assert.That(FestivalHudText.BuyReply("stock_mushrooms",0,0),Is.EqualTo("Bought Fun Guys for $5"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,FestivalHudText.BuyReply("stock_lsd",15,15)),Is.EqualTo("Bought Tongue Stamps for 5 ramen packets"));
            Assert.That(FestivalHudText.BuyReply("stock_lsd",15,20),Is.EqualTo(""),"a missed or cancelled buy gave the price back");
        }

        // LOOP-4: the corner reel's frozen amount and result, in the viewer's money.
        [Test] public void ADeathsCornerReelReadsInYourMoney()
        {
            var dropped=new DeathSpinState{Id="r:death:sam:1",RoundId="r",PlayerId="sam",Name="Sam",Sequence=1,Amount=40,CashDropped=40,AtWorldSeconds=3};
            var kept=new DeathSpinState{Id="r:death:sam:2",RoundId="r",PlayerId="sam",Name="Sam",Sequence=2,Amount=40,AtWorldSeconds=9};
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.DeathSpinTitle(dropped),Is.EqualTo("SAM DIED"));
            Assert.That(FestivalHudText.DeathSpinStake(palm,you,dropped),Is.EqualTo("$40"));
            Assert.That(FestivalHudText.DeathSpinResult(palm,you,dropped),Is.EqualTo("DROPPED $40"));
            Assert.That(FestivalHudText.DeathSpinResult(palm,you,kept),Is.EqualTo("KEPT $40"));
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.DeathSpinStake(playa,sam,dropped),Is.EqualTo("40 RAMEN PACKETS"),"Sam reads the stake in ramen");
            Assert.That(FestivalHudText.DeathSpinResult(playa,sam,dropped),Is.EqualTo("DROPPED 40 RAMEN PACKETS"));
            Assert.That(FestivalHudText.DeathSpinResult(playa,sam,kept),Is.EqualTo("KEPT 40 RAMEN PACKETS"));
            Assert.That(FestivalHudText.DeathSpinTitle(new DeathSpinState{Name="Bartholomew the Great"}),Is.EqualTo("BARTHOLOME DIED"),"a long name keeps the spinner's ten letters");
        }

        // LOOP-4: a cash pile's floating label and its pickup line read its amount in your money; other drops keep their names.
        [Test] public void ACashPileAndItsPickupReadInYourMoney()
        {
            var pile=new DropState{Id="cash_1",ItemId="cash",Count=40,X=1,Z=2};var stock=new DropState{Id="drop_2",ItemId="stock_lsd",Count=1};
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.DropLabel(palm,you,pile),Is.EqualTo("$40"));
            Assert.That(FestivalHudText.PickupAction(palm,you,pile),Is.EqualTo("Pick up $40"));
            Assert.That(FestivalHudText.PickupAction(palm,you,stock),Is.EqualTo("Pick up Tongue Stamps"),"other drops keep their display names");
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.DropLabel(playa,sam,pile),Is.EqualTo("40 ramen packets"));
            Assert.That(FestivalHudText.PickupAction(playa,sam,pile),Is.EqualTo("Pick up 40 ramen packets"));
            Assert.That(FestivalSession.DropModel("cash"),Is.EqualTo("FestivalVoucher"),"a cash pile reuses the voucher model");
        }

        // ECON-8: a random find's notice, in your money.
        [Test] public void AFindsNoticeReadsInYourMoney()
        {
            var palm=Day(0);var you=Crew(palm,"you",0);var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.MoneyText(palm,you,FestivalHudText.FoundCashNotice(4)),Is.EqualTo("Found $4 on the ground!"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,FestivalHudText.FoundCashNotice(4)),Is.EqualTo("Found 4 ramen packets on the ground!"));
        }

        // A sale's outcome reads in your money wherever it shows, from what the host wrote in dollars: its notice in the HUD's message
        // and the chat window, its result on the stamp. A paid sale flashes your cash and SALES on the player card, lit every other
        // quarter-second for the stamp's 1.5 s.
        [Test] public void ASalesResultReadsInYourMoneyAndFlashesTheCard()
        {
            var game=new FestivalSimulation(7);game.AddPlayer("ash","Ash");var sam=game.AddPlayer("sam","Sam");game.State.FestivalIndex=1;game.State.Phase="Playing";game.State.Npcs.Clear();
            string buyer="buyer_0";for(int k=1;DialogueGrammar.PersonaFor(buyer,"")=="Raver"||DialogueGrammar.PersonaFor(buyer,"")=="Burner";k++)buyer="buyer_"+k;
            game.State.Npcs.Add(new NpcState{Id=buyer,Role="Buyer",X=0,Z=1.5f,Yaw=180});sam.X=0;sam.Z=0;sam.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            Assert.That(game.Execute("sam",new GameCommand{Id="greedy",Kind="StartSale",TargetId=buyer,ItemId="stock_lsd",Amount=FestivalSimulation.GreedyAsk}).Accepted,Is.True,"setup: a Greedy sale");
            var sale=game.Interaction(sam.InteractionId);
            for(int t=0;sale.Status=="Active";t++){var turn=sale.Turns[sale.Turns.Count-1];Assert.That(game.Execute("sam",new GameCommand{Id="reply_"+t,Kind="Reply",Amount=System.Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=sale.Turns.Count-1}).Accepted,Is.True,"setup: reply "+(t+1));}
            var outcome=sam.ConversationOutcomes.Find(o=>o.InteractionId==sale.Id);
            Assert.That(FestivalHudText.MoneyText(game.State,sam,outcome.Notice),Is.EqualTo("SOLD Tongue Stamps  •  +20 ramen packets"));
            Assert.That(FestivalHudText.MoneyText(game.State,sam,outcome.Result),Is.EqualTo("SOLD +20 ramen packets"));
            var palm=Day(0);var you=Crew(palm,"you",1);you.Cash=87;palm.StashCash=5;palm.LevelSales=120;
            Assert.That(FestivalHudText.Vitals(palm,you,flashMoney:true),Is.EqualTo("HP 100  •  <color=#FC7741>$87</color>  •  STASH $5\n<color=#FC7741>SALES $120</color>  •  ALIVE"),"cash and SALES lit");
            Assert.That(FestivalHudText.Vitals(palm,you,flashMoney:false),Is.EqualTo(FestivalHudText.Vitals(palm,you)),"unlit as before");
            Assert.That(new[]{0f,.1f,.3f,.5f,1.4f,1.5f,-1f}.Select(FestivalHudText.MoneyFlashOn),Is.EqualTo(new[]{true,true,false,true,false,false,false}),"lit on the even quarter-seconds of the stamp's 1.5 s");
            Assert.That(FestivalHudText.SaleStampSeconds,Is.EqualTo(1.5f));
        }

        // The stamp follows your own outcomes, not the chat window, so a sale whose window Esc shut is stamped too. Each sale is
        // stamped once, and a rejoin or a level's first view only takes note of the sales already there.
        [Test] public void TheStampShowsEachSaleOnceAndNoneFromBeforeYouJoined()
        {
            var s=Day(0);s.RoundId="day_1";var you=Crew(s,"you",0);var news=new FestivalHudText.SaleNews();
            ConversationOutcome Ended(string kind,string result){var o=new ConversationOutcome{RoundId=s.RoundId,InteractionId="talk_"+you.ConversationOutcomes.Count,Kind=kind,Result=result,Sequence=++you.ConversationOutcomeSequence};you.ConversationOutcomes.Add(o);return o;}
            Ended("Sale","SOLD +$10");
            Assert.That(news.Follow(s,you,0),Is.Null,"a sale from before your first view");
            var sold=Ended("Sale","SOLD +$15");
            Assert.That(news.Follow(s,you,0),Is.SameAs(sold),"a sale that ends while you watch");
            Assert.That(news.Follow(s,you,0),Is.Null,"stamped once");
            Ended("Buy","");Ended("Conversation","");Ended("Sale","");
            Assert.That(news.Follow(s,you,0),Is.Null,"a buy, a chat and a sale with no result");
            var back=Ended("Sale","NO SALE");
            Assert.That(news.Follow(s,you,0),Is.SameAs(back),"a sale that comes back");
            Ended("Sale","NO SALE");
            Assert.That(news.Follow(s,you,1),Is.Null,"a rejoin's first view");
            var busted=Ended("Sale","BUSTED");
            Assert.That(news.Follow(s,you,1),Is.SameAs(busted),"a sale that ends after the rejoin");
            s.RoundId="night_1";you.ConversationOutcomes.Add(new ConversationOutcome{RoundId=s.RoundId,InteractionId="talk_night",Kind="Sale",Result="SOLD +$10",Sequence=++you.ConversationOutcomeSequence});
            Assert.That(news.Follow(s,you,1),Is.Null,"a new level's first view");
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
