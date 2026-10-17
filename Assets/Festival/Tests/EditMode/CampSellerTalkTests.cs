using System.Collections.Generic;
using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;
using UnityEngine;

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

        GameObject canvas,worldObject;
        [TearDown] public void TakeItDown(){if(canvas!=null)Object.DestroyImmediate(canvas);if(worldObject!=null)Object.DestroyImmediate(worldObject);}
        // The HUD's chat window on a 1920x1080 canvas, as ChatWindowTests builds it.
        FestivalChatWindow Window()
        {
            canvas=new GameObject("Camp seller test canvas",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
            return FestivalChatWindow.Build(canvas.transform,Resources.Load<Font>("FestivalDisplay"),null);
        }
        // The thread as drawn, oldest first: "< " for her bubbles, "> " for yours, then what the bubble shows.
        static string[] Thread(FestivalChatWindow window){var lines=new List<string>();for(int i=0;i<window.Bubbles;i++)lines.Add((window.IsTheirs(i)?"< ":"> ")+window.Shown(i));return lines.ToArray();}
        static string Last(FestivalChatWindow window)=>Thread(window)[window.Bubbles-1];

        [Test] public void GrabNamesOnePaidStarterAndTheFestivalgoerSource()
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);
            string grab=CampSellerTalk.Grab(s,you);
            Assert.That(grab,Does.Contain("one starter").And.Contain("at camp"),"Marigold must explain the paid starter already supported by the host");
            Assert.That(grab,Does.Contain("Tongue Stamps").And.Contain("Fun Guys").And.Contain("$5"));
            Assert.That(grab,Does.Contain("festivalgoers").And.Contain("narcs"));
            Assert.That(grab,Does.Not.Contain("can't stock them"));
        }

        [Test] public void UsedStarterAndBothReplacementTipsReadInTheViewersMoney()
        {
            var s=Camp(Festivals.PlayaFestival,1,out var you,ordinal:1);you.StarterStockBought=1;
            Assert.That(CampSellerTalk.Grab(s,you),Does.Contain("Starter bought this camp").And.Contain("5 ramen packets").And.Contain("rescue").And.Not.Contain("$"));
            var talk=new CampSellerTalk(7);var tips=new HashSet<string>();
            for(int n=0;n<500;n++)tips.Add(talk.Tip(s,you));
            string all=string.Join("\n",tips);
            Assert.That(all,Does.Not.Contain("gear only").And.Not.Contain("Nobody at camp sells stock").And.Not.Contain("Stock only comes"));
            Assert.That(all,Does.Contain("one starter").And.Contain("5 ramen packets").And.Contain("Keep some money"));
        }

        [Test] public void NightMarketHasNoStarterHoldersOrTagsAndGearKeepsItsOriginalSlots()
        {
            worldObject=new GameObject("Shelf presentation world");var world=worldObject.AddComponent<FestivalWorld>();world.Build();
            var s=Camp(0,0,out var you);s.RoundId="shelf-presentation";
            foreach(string id in new[]{"stock_lsd","stock_mushrooms"}){s.VendorOffers.Add(id);s.ShopStock.Add(new ShopStockState{ItemId=id,CampAvailable=8});}
            world.UpdateShop(s,you);
            var camp=world.transform.Find("Festival campsite lobby/Campsite shelf goods");
            var market=world.GetComponentsInChildren<Transform>(true);
            Transform shelves=null;foreach(var t in market)if(t.name=="Night market goods")shelves=t;
            Assert.That(camp,Is.Not.Null);Assert.That(shelves,Is.Not.Null,"setup: production market display root");
            for(int i=0;i<s.VendorOffers.Count;i++)
            {
                string id=s.VendorOffers[i];Assert.That(camp.Find("Shop item "+id),Is.Not.Null,"camp retains all offers");
                var holder=shelves.Find("Shop item "+id);
                if(Catalog.FindItem(id).Category=="Stock")Assert.That(holder,Is.Null,"stock has no market holder or price tag");
                else{Assert.That(holder,Is.Not.Null);Assert.That(holder.localPosition.x,Is.EqualTo(Catalog.ShopPoint(false,i).X));Assert.That(holder.GetComponentInChildren<TextMesh>().text,Does.Contain(Catalog.ShopTag(id)));}
            }
        }

        [Test] public void FTalksBesideHerAtCampWithNothingUnpaidInHand()
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);
            Assert.That(CampSellerTalk.Beside(s,you)&&CampSellerTalk.CanTalk(s,you),Is.True,"2 m in front of her table, at camp");
            you.HeldOfferId="merch_bag";
            Assert.That(CampSellerTalk.CanTalk(s,you),Is.False,"holding an unpaid item: the pay prompt, not the talk");
            Assert.That(CampSellerTalk.Beside(s,you),Is.True,"but still beside her, so F there is never the festival switch");
            you.HeldOfferId="";you.X=CampSellerTalk.Reach*.75f;you.Z=CampSellerTalk.Z-CampSellerTalk.Reach*.75f;
            Assert.That(CampSellerTalk.CanTalk(s,you),Is.False,"more than 2.7 m off");
            you.X=0;you.Z=5;you.CampVisitId="tent_1";
            Assert.That(CampSellerTalk.Beside(s,you),Is.False,"inside a tent");
            you.CampVisitId="";s.Phase="Playing";
            Assert.That(CampSellerTalk.Beside(s,you),Is.False,"only at camp");
        }

        [Test] public void FOpensOnHerGreetingThenEachQuestionIsAnsweredAndCanBeAskedAgain()
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);var talk=new CampSellerTalk(5);var twin=new CampSellerTalk(5);var window=Window();
            talk.Begin(window,s,you,0);
            Assert.That(talk.Open&&window.Open,Is.True,"her talk is on the chat window");
            Assert.That(window.Header,Is.EqualTo(CampSellerTalk.Header));
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+FestivalChatWindow.Ellipsis}),"she starts typing her greeting");
            Assert.That(window.Replies,Is.EqualTo(3),"the three questions to ask with 1-3");
            for(int i=0;i<3;i++)Assert.That(window.ReplyLabel(i),Is.EqualTo(CampSellerTalk.Questions[i]));
            Assert.That(talk.Follow(s,you,10,false),Is.True,"the talk goes on while you stand there");
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+twin.Greeting(s.FestivalIndex)}),"her greeting, typed out");
            string[] answers={CampSellerTalk.Grab(s,you),CampSellerTalk.Today(s,you),twin.Tip(s,you)};float answeredAt=10;
            for(int ask=0;ask<3;ask++)
            {
                Assert.That(window.Pick(ask),Is.True,"question "+(ask+1)+" can be asked");
                Assert.That(Thread(window)[window.Bubbles-2],Is.EqualTo("> "+CampSellerTalk.Questions[ask]),"you ask it");
                answeredAt+=FestivalChatWindow.Typed(answers[ask])+.1f;talk.Follow(s,you,answeredAt,false);
                Assert.That(Last(window),Is.EqualTo("< "+answers[ask]),"she answers question "+(ask+1));
                for(int i=0;i<3;i++)Assert.That(window.CanReply(i),Is.True,"question "+(i+1)+" can be asked again after question "+(ask+1));
            }
            s.ShopStock.Find(x=>x.ItemId=="merch_bag").CampAvailable=0;
            window.Pick(0);talk.Follow(s,you,60,false);
            Assert.That(Last(window),Is.EqualTo("< "+CampSellerTalk.Grab(s,you)).And.Contain("Sold out"),"asked again, she answers from the view as it is now");
        }

        [Test] public void MarigoldWaitsWithoutAClockAndNamesEscape()
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);var talk=new CampSellerTalk(8);var window=Window();
            talk.Begin(window,s,you,0);talk.Follow(s,you,60,false);
            Assert.That(window.Header,Does.Contain("ESC CLOSE"));
            Assert.That(window.transform.Find("Time left track").gameObject.activeSelf,Is.False,"Marigold has no timed reply");
            Assert.That(window.Open&&talk.Open,Is.True);
            talk.End();Assert.That(talk.Follow(s,you,61,false)||window.Open,Is.False);
        }

        [TestCase(false,false)] [TestCase(false,true)] [TestCase(true,false)] [TestCase(true,true)]
        public void NewActionsClearHerTalkAndRetainedSignOff(bool lingering,bool heldOffer)
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);var talk=new CampSellerTalk(8);var window=Window();
            talk.Begin(window,s,you,0);talk.Follow(s,you,1,true);
            if(lingering){you.Z=CampSellerTalk.Z-FestivalChatWindow.LeaveReach-.3f;talk.Follow(s,you,2,true);}
            Assert.That(talk.Visible&&window.Open,Is.True,"setup: her live talk or walk-off is displayed");
            if(heldOffer)you.HeldOfferId="merch_bag";else you.InteractionId="next_action";
            Assert.That(talk.Follow(s,you,3,true)||window.Open||talk.Visible,Is.False,"a new purchase or authoritative action owns the screen");
        }

        [Test] public void WalkingOffEndsTheTalkOnHerSignOffWhichStaysUpUntilRead()
        {
            var s=Camp(Festivals.PlayaFestival,0,out var you);var talk=new CampSellerTalk(8);var twin=new CampSellerTalk(8);var window=Window();
            talk.Begin(window,s,you,0);talk.Follow(s,you,5,false);twin.Greeting(s.FestivalIndex);
            you.Z=CampSellerTalk.Z-(FestivalChatWindow.LeaveReach-.3f);
            Assert.That(talk.Follow(s,you,6,false)&&talk.Open,Is.True,"stepping back, still within reach, the talk goes on");
            you.Z=CampSellerTalk.Z-(FestivalChatWindow.LeaveReach+.3f);
            // With reduced motion her sign-off shows whole after the "…" (FestivalChatWindow.TypingSeconds).
            Assert.That(talk.Follow(s,you,7,true),Is.True,"walking off, the window stays up");
            Assert.That(talk.Open,Is.False,"the talk is over, so 1-3 equip again");
            Assert.That(talk.Follow(s,you,8,true)&&window.Lingering,Is.True,"it lingers, read-only");
            Assert.That(Last(window),Is.EqualTo("< "+twin.SignOff(s.FestivalIndex)),"her sign-off, called after you");
            for(int i=0;i<3;i++)Assert.That(window.CanReply(i),Is.False,"nothing to ask once it is over");
            Assert.That(talk.Follow(s,you,60,true),Is.True,"the sign-off stays until an explicit close");
            talk.End();
            Assert.That(window.Open,Is.False);
        }

        [Test] public void GoingInsideTheSpinStartingOrEscapeClosesItAtOnce()
        {
            var s=Camp(Festivals.PoloFestival,0,out var you);var talk=new CampSellerTalk(2);var window=Window();
            void Talk(float now){talk.Begin(window,s,you,now);Assert.That(talk.Follow(s,you,now+.1f,false)&&talk.Open&&window.Open,Is.True,"setup: talking to her");}
            Talk(0);you.CampVisitId="tent_1";
            Assert.That(talk.Follow(s,you,1,false)||window.Open||talk.Open,Is.False,"inside a tent");
            you.CampVisitId="";Talk(2);s.Phase="Spinning";
            Assert.That(talk.Follow(s,you,3,false)||window.Open||talk.Open,Is.False,"the spin starting");
            s.Phase="Shopping";Talk(4);talk.End();
            Assert.That(window.Open||talk.Open,Is.False,"Escape");
            Assert.That(talk.Follow(s,you,5,false),Is.False,"closed stays closed");
        }

        [Test] public void SheTurnsToFaceYouAndTalksThenTurnsBackAndIdles()
        {
            worldObject=new GameObject("Camp seller world");var world=worldObject.AddComponent<FestivalWorld>();world.Build();
            var seller=world.CampSeller;var rest=Quaternion.Euler(0,CampSellerTalk.RestYaw,0);
            Assert.That(seller,Is.Not.Null,"the world exposes Marigold");
            Assert.That(Vector3.Distance(seller.transform.localPosition,new Vector3(CampSellerTalk.X,0,CampSellerTalk.Z)),Is.LessThan(.001f),"in front of the supplies table");
            Assert.That(Quaternion.Angle(seller.transform.localRotation,rest),Is.LessThan(.1f),"facing the camp");
            var s=Camp(Festivals.PoloFestival,0,out var you);you.X=CampSellerTalk.X+2;you.Z=CampSellerTalk.Z;
            var talk=new CampSellerTalk(4);
            talk.Face(seller,you,1);
            Assert.That(Quaternion.Angle(seller.transform.localRotation,rest),Is.LessThan(.1f),"nobody talking: she keeps facing the camp");
            Assert.That(seller.Pose,Is.EqualTo("Idle"));
            talk.Begin(Window(),s,you,0);
            talk.Face(seller,you,.1f);
            Assert.That(Quaternion.Angle(seller.transform.localRotation,rest),Is.EqualTo(CampSellerTalk.TurnDegreesPerSecond*.1f).Within(.5f),"she turns toward you, not in a snap");
            talk.Face(seller,you,1);
            Assert.That(Vector3.Angle(seller.transform.localRotation*Vector3.forward,Vector3.right),Is.LessThan(.5f),"she faces you");
            Assert.That(seller.Pose,Is.EqualTo("Conversation"),"in the Talk pose");
            talk.End();talk.Face(seller,you,1);
            Assert.That(Quaternion.Angle(seller.transform.localRotation,rest),Is.LessThan(.1f),"then back to the camp");
            Assert.That(seller.Pose,Is.EqualTo("Idle"),"and idle");
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
            Assert.That(grab,Does.Contain("one starter").And.Contain("at camp"),"stock already held does not spend this camp allowance");
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
                Assert.That(grab,Does.Contain("one starter").And.Contain("at camp"),"night camp still has its entitlement, "+why);
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
            Assert.That(all,Does.Contain("one starter").And.Not.Contain("gear only").And.Not.Contain("only comes from festivalgoers"),"both old gear-only tips are gone");
            Assert.That(all,Does.Contain("dance").And.Contain("hunch"),"talks, their trust dances and hunches");
            Assert.That(all,Does.Contain(FestivalVisionMarkers.VerdictText("Narc",false)),"what a checked vision says, in VISION-5's words");
            Assert.That(all,Does.Contain("ditch").And.Contain("walk").And.Contain("talk"),"a security stop's three ways out");
            for(int track=1;track<CampFeatures.Tracks.Length;track++)Assert.That(all,Does.Contain(CampFeatures.Tracks[track].ToLowerInvariant()),"the camp DJ's "+CampFeatures.Tracks[track]);
            Assert.That(all,Does.Contain("nfluencer").And.Contain("dust storm"),"each festival's own tips");
        }

        // A grabbed Giggle Tank pays nothing until someone carries it home, and her tip says so, in the viewer's money.
        [Test] public void TheGiggleTankTipSaysToCarryItHomeToTheCampGate()
        {
            var s=Camp(Festivals.PlayaFestival,0,out var you,ordinal:1);var talk=new CampSellerTalk(5);var tank=new HashSet<string>();
            for(int ask=0;ask<2000;ask++){string tip=talk.Tip(s,you);if(tip.Contains("Giggle Tank"))tank.Add(tip);}
            Assert.That(tank.Count,Is.EqualTo(1),"one Giggle Tank tip: "+string.Join(" | ",tank));
            foreach(var tip in tank)
            {
                Assert.That(tip,Does.Contain("carry it home to the camp gate").And.Contain("25 ramen packets").And.Not.Contain("$"),"the gate pays it, in your money: "+tip);
                Assert.That(tip,Does.Contain("free hand").And.Contain("arrested or die"),"what it takes and what loses it: "+tip);
                Assert.That(tip,Does.Not.Contain("Grab it and the crew stash gets"),"a grab alone no longer pays: "+tip);
            }
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
