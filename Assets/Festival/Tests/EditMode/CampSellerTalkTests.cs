using System.Collections.Generic;
using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;

namespace Festival.Tests
{
    // CAMP-1 (Matt, 2026-10-01: "i should be able to talk to the camp vendor."): what Marigold, the camp seller, says, built from
    // crafted camp states the way a client's view holds them.
    public sealed class CampSellerTalkTests
    {
        // THEME-1's real-drug words and THEME-2's retired names (the same lists as tests/Domain/ThemeTests.cs), matched in any case.
        static readonly string[] Banned={"lsd","acid","mushroom","shroom","ecstasy","molly","mdma","ketamine","weed","marijuana","cannabis","cocaine","drug","prism","moon cap","hug drop","snack leaf","couch lock"};
        // Today's shelves: the three staples and three extras (Catalog.VendorOffers), two of each left.
        static readonly string[] Shelf={"little_spoon","medical_voucher","poi_practice","merch_bag","confetti","stage_pass"};

        // Camp before `level` of `festival`, for a crew of `crew`, with `you` (crew member `ordinal`) holding nothing.
        static RoundState Camp(int festival,int level,out PlayerState you,int crew=2,int ordinal=0)
        {
            var s=new RoundState{Phase="Shopping",FestivalIndex=festival,LevelIndex=level,HostPlayerId="you",ConnectedCrewCount=crew};
            foreach(var id in Shelf){s.VendorOffers.Add(id);s.ShopStock.Add(new ShopStockState{ItemId=id,CampAvailable=2,MarketAvailable=2});}
            you=new PlayerState{Id="you",Name="You",Ordinal=ordinal,X=0,Z=5};s.Players.Add(you);
            for(int i=1;i<crew;i++)s.Players.Add(new PlayerState{Id="mate"+i,Name="Mate "+i,Ordinal=i});
            return s;
        }

        [Test] public void WithNoStockBeforeADayGrabNamesBothStockItemsAndTheirPriceInYourMoney()
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);string grab=CampSellerTalk.Grab(s,you);
            Assert.That(grab,Does.Contain("Tongue Stamps").And.Contain("Fun Guys").And.Contain("$5"),"both stock items and their price");
            Assert.That(grab,Does.Contain("festivalgoers").And.Contain("narc"),"ECON-2: only festivalgoers sell stock, and some are narcs");
            s=Camp(Festivals.PlayaFestival,2,out you,ordinal:1);grab=CampSellerTalk.Grab(s,you);
            Assert.That(FestivalHudText.Money(s,you,5),Is.EqualTo("5 ramen packets"),"setup: Ember Playa's money for crew member 1");
            Assert.That(grab,Does.Contain("Tongue Stamps").And.Contain("Fun Guys").And.Contain("5 ramen packets"),"Day 2 on Ember Playa, in your own money");
            Assert.That(grab,Does.Not.Contain("$"),"never in dollars on Ember Playa");
        }

        [Test] public void WithStockGrabPointsAtTheShelfExtrasAndWhatIsSoldOut()
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);you.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});
            string grab=CampSellerTalk.Grab(s,you);
            Assert.That(grab,Does.Not.Contain("Tongue Stamps or Fun Guys"),"stocked up, so no stock pitch");
            Assert.That(grab,Does.Contain("merch bag").And.Contain("security"),"a merch bag hides stock from security");
            Assert.That(grab,Does.Contain("medical tent"),"a voucher for the medical tent");
            Assert.That(grab,Does.Contain("poi"),"poi for dances");
            Assert.That(grab,Does.Not.Contain("Sold out"),"nothing is sold out yet");
            s.ShopStock.Find(x=>x.ItemId=="merch_bag").CampAvailable=0;grab=CampSellerTalk.Grab(s,you);
            Assert.That(grab,Does.Contain("Sold out: Official merch bag"),"what's sold out");
            Assert.That(grab,Does.Not.Contain("hides your stock"),"a sold-out extra isn't offered");
            Assert.That(grab,Does.Contain("confetti"),"the next extra on the shelf takes its place");
        }

        [Test] public void BeforeANightGrabPushesNoStockAndSaysTheNightsJob()
        {
            foreach(bool stocked in new[]{false,true})
            {
                var s=Camp(Festivals.PlayaFestival,1,out var you);if(stocked)you.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
                string grab=CampSellerTalk.Grab(s,you),why=stocked?"holding stock":"holding nothing";
                Assert.That(grab,Does.Not.Contain("Tongue Stamps").And.Not.Contain("Fun Guys"),"no stock pitch before a night, "+why);
                Assert.That(grab,Does.Contain("rescue"),"the night's job, "+why);
                Assert.That(grab,Does.Contain("medical tent"),"the shelf's extras still, "+why);
            }
        }

        [Test] public void TodayNamesTheFestivalDayOrNightTheQuotaAndTheTwists()
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);string today=CampSellerTalk.Today(s,you);
            Assert.That(FestivalSimulation.DayQuota(s),Is.EqualTo(30),"setup: Day 1 at Palm Mirage asks $15 each of two");
            Assert.That(today,Does.Contain("Palm Mirage").And.Contain("Day 1").And.Contain("$30"),"the festival, the day and the crew's quota");
            Assert.That(today,Does.Contain("nfluencers").And.Contain("VIP").And.Contain("Ferris wheel"),"Palm Mirage's twists");
            s=Camp(Festivals.PlayaFestival,2,out you,crew:3,ordinal:2);today=CampSellerTalk.Today(s,you);
            Assert.That(today,Does.Contain("Ember Playa").And.Contain("Day 2").And.Contain(FestivalHudText.Money(s,you,FestivalSimulation.DayQuota(s))),"Day 2's quota for three, in your own money");
            Assert.That(today,Does.Contain("dust storm").And.Contain("art car"),"Ember Playa's twists");
            s=Camp(Festivals.PlayaFestival,1,out you);today=CampSellerTalk.Today(s,you);
            Assert.That(today,Does.Contain("Ember Playa").And.Contain("Night 1").And.Contain("clue trail"),"a night's job");
            Assert.That(today,Does.Not.Contain(FestivalHudText.Money(s,you,Festivals.Level(Festivals.PlayaFestival,0,0).QuotaPerCrew*2)),"a night has no quota");
            s=Camp(Festivals.PlayaFestival,3,out you);today=CampSellerTalk.Today(s,you);
            Assert.That(today,Does.Contain("Night 2").And.Contain("home").And.Contain("effigy"),"the finale: everyone home, and the effigy burns");
        }

        [Test] public void TipsNeverRepeatBackToBackForTwentyAsks()
        {
            for(int festival=0;festival<Festivals.Count;festival++)
            {
                var s=Camp(festival,0,out var you);var talk=new CampSellerTalk(festival+11);string last="";var heard=new HashSet<string>();
                for(int ask=0;ask<20;ask++)
                {
                    string tip=talk.Tip(s,you);
                    Assert.That(tip,Is.Not.Empty,"a tip on ask "+(ask+1));
                    Assert.That(tip,Is.Not.EqualTo(last),"ask "+(ask+1)+" on "+Festivals.Name(festival)+" repeats the tip before it");
                    last=tip;heard.Add(tip);
                }
                Assert.That(heard.Count,Is.GreaterThan(10),"twenty asks hear plenty of different tips on "+Festivals.Name(festival));
            }
        }

        [Test] public void TheTipsCoverWhatShipped()
        {
            var tips=new HashSet<string>();
            for(int festival=0;festival<Festivals.Count;festival++){var s=Camp(festival,0,out var you);var talk=new CampSellerTalk(3);for(int ask=0;ask<2000;ask++)tips.Add(talk.Tip(s,you));}
            string all=string.Join("\n",tips);
            Assert.That(tips.Count,Is.GreaterThanOrEqualTo(20),"a pool of at least 20 tips");
            bool stock=false;foreach(var tip in tips)stock|=tip.Contains("festivalgoers")&&tip.Contains("narc");
            Assert.That(stock,Is.True,"stock comes from festivalgoers, some of them narcs");
            Assert.That(all,Does.Contain("gear only"),"the shelves sell gear only");
            Assert.That(all,Does.Contain("dance").And.Contain("hunch"),"talks, their trust dances and hunches");
            Assert.That(all,Does.Contain(FestivalVisionMarkers.VerdictText("Narc",false)),"what a checked vision says, in VISION-5's words");
            Assert.That(all,Does.Contain("ditch").And.Contain("walk").And.Contain("talk"),"a security stop's three ways out");
            for(int track=1;track<CampFeatures.Tracks.Length;track++)Assert.That(all,Does.Contain(CampFeatures.Tracks[track].ToLowerInvariant()),"the camp DJ's "+CampFeatures.Tracks[track]);
            Assert.That(all,Does.Contain("nfluencer").And.Contain("dust storm"),"each festival's own tips");
        }

        [Test] public void EverythingSheSaysIsFreeOfRealDrugAndRetiredWords()
        {
            var lines=new List<string>(CampSellerTalk.Questions){CampSellerTalk.Name};
            for(int festival=0;festival<Festivals.Count;festival++)
            {
                var greetings=new HashSet<string>();var talk=new CampSellerTalk(festival);
                for(int i=0;i<500;i++){string greeting=talk.Greeting(festival);greetings.Add(greeting);lines.Add(greeting);lines.Add(talk.SignOff(festival));}
                Assert.That(greetings.Count,Is.GreaterThanOrEqualTo(12),"at least 12 greetings on "+Festivals.Name(festival));
                for(int level=0;level<Festivals.LevelCount;level++)foreach(bool stocked in new[]{false,true})
                {
                    var s=Camp(festival,level,out var you);if(stocked)you.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                    lines.Add(CampSellerTalk.Grab(s,you));lines.Add(CampSellerTalk.Today(s,you));
                    for(int i=0;i<100;i++)lines.Add(talk.Tip(s,you));
                }
            }
            foreach(var line in lines)
            {
                Assert.That(line,Is.Not.Empty,"every line says something");
                foreach(var word in Banned)Assert.That(line.ToLowerInvariant(),Does.Not.Contain(word),"real-drug or retired word '"+word+"' in: "+line);
            }
        }
    }
}
