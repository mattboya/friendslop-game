using System.Collections.Generic;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;

namespace Festival.Tests
{
    // HUD-1: the weekend text a player reads, built from crafted round states the way a client's view holds them.
    public sealed class HudLoopTests
    {
        static RoundState Level(int festival,int level,string phase="Playing",int encore=0)
        {
            return new RoundState{FestivalIndex=festival,LevelIndex=level,EncoreTier=encore,Phase=phase,HostPlayerId="you",DurationSeconds=Festivals.Level(festival,level,encore).DurationSeconds};
        }
        static PlayerState Crew(RoundState s,string id,string name,float x=0,float z=0,string life="Alive")
        {
            var p=new PlayerState{Id=id,Name=name,X=x,Z=z,Life=life};s.Players.Add(p);return p;
        }

        [Test] public void BannerNamesTheFestivalAndLevel()
        {
            Assert.That(FestivalHudText.LevelBanner(Level(0,0)),Is.EqualTo("PALM MIRAGE  •  DAY 1"));
            Assert.That(FestivalHudText.LevelBanner(Level(1,3)),Is.EqualTo("EMBER PLAYA  •  NIGHT 2"));
            Assert.That(FestivalHudText.LevelBanner(Level(0,1,encore:2)),Is.EqualTo("PALM MIRAGE  •  NIGHT 1  •  ENCORE 2"),"an encore lap says which lap it is");
            Assert.That(FestivalHudText.LevelBanner(Level(1,2,"Shopping")),Is.EqualTo("UP NEXT  •  EMBER PLAYA  •  DAY 2"),"at camp the banner names the level the crew is about to play");
        }

        [Test] public void ClockCountsDownWholeSecondsLeft()
        {
            var s=Level(0,0);
            s.ElapsedSeconds=.6;Assert.That(FestivalHudText.Clock(s),Is.EqualTo("8:00"),"a second that has started still counts as left");
            s.ElapsedSeconds=420.4;Assert.That(FestivalHudText.Clock(s),Is.EqualTo("1:00"),"59.6 s left reads 1:00, never 0:60");
            s.ElapsedSeconds=405;Assert.That(FestivalHudText.Clock(s),Is.EqualTo("1:15"));
            s.ElapsedSeconds=500;Assert.That(FestivalHudText.Clock(s),Is.EqualTo("0:00"));
            Assert.That(FestivalHudText.Clock(Level(0,0,"Shopping")),Is.EqualTo("CAMP"));
            Assert.That(FestivalHudText.Clock(Level(0,0,"Results")),Is.EqualTo("DONE"));
        }

        [Test] public void DayObjectiveIsTheCrewsQuotaUntilItIsMet()
        {
            var s=Level(0,0);var you=Crew(s,"you","You");Crew(s,"sam","Sam");s.LevelSales=12;
            Assert.That(FestivalHudText.ObjectiveTitle(s,you),Is.EqualTo("DAY QUOTA  $12 / $30"),"Palm Mirage Day 1 asks $15 from each of two");
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo("Sell $18 more before sundown. You have nothing to sell: buy from festivalgoers, and some are narcs."),"what is left, then your next step (ECON-3)");
            s.LevelSales=31;
            Assert.That(FestivalHudText.ObjectiveTitle(s,you),Is.EqualTo("QUOTA MET — HEAD BACK TO CAMP"));
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo("Anyone can end the day at the way back to camp, or keep selling until sundown."));
            you.Life="Downed";
            Assert.That(FestivalHudText.ObjectiveTitle(s,you),Is.EqualTo(FestivalGuidance.Headline(s,you)),"a downed player still reads how to get help");
            var night=Level(0,1);var rescuer=Crew(night,"you","You");
            Assert.That(FestivalHudText.ObjectiveTitle(night,rescuer),Is.EqualTo(FestivalGuidance.Headline(night,rescuer)),"nights keep the rescue's directions");
            Assert.That(FestivalHudText.Quota(night,rescuer),Is.Empty,"a night has no quota");
        }

        // ECON-3 (Matt: "i dont even know how to sell"): by day the card says what this player needs next, after what the crew has
        // left to sell. With no stock, where stock comes from. With stock, the tripper reads their visions: a green ball (a buyer
        // vision) in a green ring (checked true, VISION-5) is a real buyer; a true narc vision is ringed green too, so the ring alone
        // is not enough. The rest of the crew stick with the tripper, by name. Once the quota is met, only the way home.
        [Test] public void TheDayCardSaysWhatYouNeedNextToSell()
        {
            var s=Level(0,0);var you=Crew(s,"you","You");var sam=Crew(s,"sam","Sam");s.TripperId="sam";s.LevelSales=12;
            const string Left="Sell $18 more before sundown. ",NoStock="You have nothing to sell: buy from festivalgoers, and some are narcs.";
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo(Left+NoStock));
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo(Left+NoStock),"the tripper buys stock like anyone");
            you.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo(Left+NoStock),"gear is not stock");
            you.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});sam.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=2});
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo(Left+"Stick with Sam: they see the buyers. Walk up to one and press E to offer."));
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo(Left+"A green ball in a green ring is a real buyer: walk up and press E to offer."));
            s.LevelSales=30;
            const string Met="Anyone can end the day at the way back to camp, or keep selling until sundown.";
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo(Met));
            you.Inventory.Clear();
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo(Met),"quota met with no stock left: nothing more to buy for");
        }

        // ECON-3 (Matt, three times on 2026-10-01: "there is no button to actually sell to people"): beside a festivalgoer E runs the
        // HUD's first action, so by day it is your next step to a sale. Holding stock, the offer ("E  Offer Tongue Stamps  •  F CHAT");
        // holding none, a buy from them. The dance follows either way, and leads at night as before. While F checks a vision about
        // them (the tripper's CheckTarget), no action says F chats.
        [Test] public void BesideAFestivalgoerEIsYourNextStepToASale()
        {
            var s=Level(0,0);var you=Crew(s,"you","You");Crew(s,"sam","Sam");s.TripperId="sam";
            var goer=new NpcState{Id="goer_2",CanTalk=true};s.Npcs.Add(goer);
            string Listed(RoundState state){var labels=new List<string>();foreach(var (label,_) in FestivalHudText.FestivalgoerActions(state,you,goer))labels.Add(label);return string.Join(" | ",labels);}
            const string Buy="Buy Tongue Stamps  •  $5",Talk="Chat with festivalgoer | Talk and keep the beat",Chat="  •  F CHAT";
            Assert.That(Listed(s),Is.EqualTo(Buy+Chat+" | Dance with festivalgoer | "+Talk),"by day with no stock, E buys from them");
            var buy=FestivalHudText.FestivalgoerActions(s,you,goer)[0].Command;
            Assert.That(new[]{buy.Kind,buy.TargetId,buy.ItemId},Is.EqualTo(new[]{"StartBuy","goer_2",FestivalSimulation.SellerStock("goer_2")}),"the buy asks them for the kind they sell");
            you.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
            Assert.That(Listed(s),Does.StartWith(Buy+Chat),"gear is not stock");
            you.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
            Assert.That(Listed(s),Is.EqualTo("Offer Fun Guys"+Chat+" | Dance with festivalgoer | "+Talk+" | "+Buy),"holding stock by day, E offers it; buying more stays listed");
            var offer=FestivalHudText.FestivalgoerActions(s,you,goer)[0].Command;
            Assert.That(new[]{offer.Kind,offer.TargetId,offer.ItemId},Is.EqualTo(new[]{"StartSale","goer_2","stock_mushrooms"}),"E starts the sale of what you hold, with them");
            goer.CanTalk=false;
            Assert.That(Listed(s),Is.EqualTo("Offer Fun Guys | Dance with festivalgoer | "+Buy),"someone keeping to themselves has no chat");
            goer.CanTalk=true;s.TripperId="you";s.Visions.Add(new VisionState{Id="goer_vision",Kind="Buyer",NpcId=goer.Id});
            Assert.That(Listed(s),Is.EqualTo("Offer Fun Guys | Dance with festivalgoer | "+Talk+" | "+Buy),"F checks the tripper's vision about them, so nothing says F chats");
            var night=Level(0,1);night.Players.AddRange(s.Players);night.Npcs.Add(goer);
            Assert.That(Listed(night),Is.EqualTo("Dance with festivalgoer"+Chat+" | "+Talk+" | Offer Fun Guys | "+Buy),"at night E dances, stock or not");
            you.Inventory.Clear();
            Assert.That(Listed(night),Is.EqualTo("Dance with festivalgoer"+Chat+" | "+Talk+" | "+Buy));
        }

        // ECON-3: Q uses what you hold, and using stock is taking it yourself: the unit is gone and you trip for a minute. A new player
        // presses Q beside a buyer to "use" their stock on them, so on stock the first Q asks and only a second Q within
        // TakeStockSeconds of it takes the stock. Every other item's Q is as before.
        [Test] public void QTakesStockOnlyOnASecondPress()
        {
            Assert.That(FestivalHud.TakesOnQ("confetti","",0,10),Is.True,"gear: one Q, as ever");
            Assert.That(FestivalHud.TakesOnQ("stock_lsd","",0,10),Is.False,"the first Q on stock asks");
            Assert.That(FestivalHud.TakesOnQ("stock_lsd","stock_lsd",10,10+FestivalHud.TakeStockSeconds-.1f),Is.True,"a second Q in time takes it");
            Assert.That(FestivalHud.TakesOnQ("stock_lsd","stock_lsd",10,10+FestivalHud.TakeStockSeconds+.1f),Is.False,"too late: it asks again");
            Assert.That(FestivalHud.TakesOnQ("stock_mushrooms","stock_lsd",10,11),Is.False,"a Q on the other kind asks about that one");
            Assert.That(FestivalHud.TakeStockSeconds,Is.EqualTo(3),"within 3 s");
            Assert.That(FestivalHud.TakeStockAgain,Is.EqualTo("Q again to take it yourself (it's stock for selling)"));
        }

        [Test] public void ASpiritSeesTheWholeCrewsQuota()
        {
            // A spirit's view lists only spirits; the crew count the session sends keeps the target right.
            var s=Level(0,2);var spirit=Crew(s,"you","You",life:"Spirit");s.ConnectedCrewCount=3;s.LevelSales=20;
            Assert.That(FestivalHudText.Quota(s,spirit),Is.EqualTo("DAY QUOTA  $20 / $60"));
        }

        [Test] public void TheWayBackToCampOffersTheExtractOnlyWithSomethingToFinish()
        {
            var day=Level(0,0);Crew(day,"you","You");day.LevelSales=5;
            Assert.That(FestivalHudText.ExtractAction(day),Is.Empty,"no early exit before the quota");
            day.LevelSales=15;
            Assert.That(FestivalHudText.ExtractAction(day),Is.EqualTo("End the day: head back to camp"));
            var night=Level(0,1);Crew(night,"you","You");
            Assert.That(FestivalHudText.ExtractAction(night),Is.Empty,"no extract before the friend is found");
            night.FriendFound=true;
            Assert.That(FestivalHudText.ExtractAction(night),Is.EqualTo("Head back to camp with your friend"));
        }

        [Test] public void NightTwoChecklistShowsWhoIsHome()
        {
            var s=Level(0,3);
            Crew(s,"you","You",0,-32);
            Crew(s,"sam","Sam",40,40,"Spirit");s.Bodies.Add(new BodyState{PlayerId="sam",X=1,Z=-30});
            Crew(s,"alex","Alex",0,-32,"Spirit");s.Bodies.Add(new BodyState{PlayerId="alex",X=10,Z=5});
            Crew(s,"kim","Kim",20,0);
            Crew(s,"lee","Lee",0,-32,"Detained");
            Crew(s,"pat","Pat",20,0).Connected=false;
            Assert.That(FestivalHudText.HomeChecklist(s,"you"),Is.EqualTo("HOME  2 / 5\nYOU  •  HOME\nSAM  •  HOME\nALEX  •  BODY AWAY\nKIM  •  AWAY\nLEE  •  DETAINED"),
                "the dead count by their body, detained never counts, and nobody waits for a player who left");
            Assert.That(FestivalHudText.HomeChecklist(Level(0,1),"you"),Is.Empty,"only Night 2 needs everyone home");
        }

        [Test] public void ASpiritsChecklistNeverCallsTheUnseenLivingHome()
        {
            // A spirit's view lists only spirits (FestivalSession.ViewFor), so the living it cannot see are counted, never guessed home.
            var game=new FestivalSimulation(9);game.AddPlayer("ash","Ash");game.AddPlayer("sam","Sam");game.AddPlayer("kim","Kim");
            game.State.Phase="Playing";game.State.LevelIndex=3;
            game.Player("ash").Life="Spirit";game.State.Bodies.Add(new BodyState{PlayerId="ash",X=Festivals.CampGateX,Z=Festivals.CampGateZ});
            game.Player("sam").X=30;game.Player("sam").Z=0;
            game.Player("kim").X=Festivals.CampGateX;game.Player("kim").Z=Festivals.CampGateZ;
            Assert.That(FestivalHudText.HomeChecklist(FestivalSession.ViewFor(game,"ash"),"ash"),Is.EqualTo("HOME  ? / 3\nYOU  •  HOME\n2 LIVING  •  UNSEEN"),
                "the dead see their own body home and a crew of three, not \"HOME 1 / 1\" while Sam is still out");
            Assert.That(FestivalHudText.HomeChecklist(FestivalSession.ViewFor(game,"kim"),"kim"),Is.EqualTo("HOME  2 / 3\nASH  •  HOME\nSAM  •  AWAY\nYOU  •  HOME"),
                "the living see everyone");
        }

        [Test] public void BodyPromptsSayWhoCanCarryWhom()
        {
            var s=Level(0,3);var you=Crew(s,"you","You");var kim=Crew(s,"kim","Kim",5,5);
            Crew(s,"sam","Sam",40,40,"Spirit");Crew(s,"alex","Alex",40,40,"Spirit");
            var first=new BodyState{PlayerId="sam",X=1,Z=0};var second=new BodyState{PlayerId="alex",X=5,Z=6};s.Bodies.Add(first);s.Bodies.Add(second);
            Assert.That(FestivalHudText.BodyAction(s,you,first),Is.EqualTo("Carry Sam's body"));
            Assert.That(FestivalHudText.BodyAction(s,you,second),Is.Empty,"a body more than 2 m away is out of reach");
            you.CarryBodyId="sam";
            Assert.That(FestivalHudText.BodyAction(s,you,first),Is.EqualTo("Put down Sam's body"),"the first body moves with one carrier");
            you.CarryBodyId="";you.X=5;you.Z=5;
            Assert.That(FestivalHudText.BodyAction(s,you,second),Is.EqualTo("Carry Alex's body"));
            you.CarryBodyId="alex";
            Assert.That(FestivalHudText.BodyAction(s,you,second),Is.EqualTo("Put down Alex's body  •  too heavy alone: get a second carrier"),"a later body stays put with one carrier");
            Assert.That(FestivalHudText.BodyAction(s,kim,second),Is.EqualTo("Take the other end of Alex's body"));
            kim.CarryBodyId="alex";
            Assert.That(FestivalHudText.BodyAction(s,you,second),Is.EqualTo("Put down Alex's body"),"with two carriers it moves");
            kim.Life="Downed";
            Assert.That(FestivalHudText.BodyAction(s,you,second),Does.EndWith("too heavy alone: get a second carrier"),"a carrier who goes down lets go");
            kim.Life="Alive";
            Assert.That(FestivalHudText.BodyAction(s,Crew(s,"lee","Lee",5,5),second),Is.Empty,"a body takes two carriers at most");
            first.X=5;first.Z=4;
            Assert.That(FestivalHudText.BodyAction(s,kim,first),Is.Empty,"hands full: one body at a time");
            kim.CarryBodyId="";kim.DragTargetId="lee";
            Assert.That(FestivalHudText.BodyAction(s,kim,first),Is.Empty,"no carrying while dragging a downed friend");
        }

        // ECON-2: no shelf sells stock, so whoever is at camp reads where it comes from: the host beside Day 1's festival pick,
        // everyone else on the camp line. ECON-3: only while they hold none and a day is next; a night has no quota to sell.
        const string CampLine="Browse gear • pay the seller • stock comes from festivalgoers • meet at the trailhead";
        [Test] public void CampSaysStockComesFromFestivalgoers()
        {
            var s=Level(0,0,"Shopping");var you=Crew(s,"you","You");var sam=Crew(s,"sam","Sam");s.UnlockedFestivalCount=2;
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo("F  switch to Ember Playa (2 of 2 unlocked) • stock comes from festivalgoers"));
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo(CampLine));
            you.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});sam.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo("F  switch to Ember Playa (2 of 2 unlocked)"),"stock in hand: the host reads only the pick");
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo("Browse gear • pay the seller • meet at the trailhead"),"stock in hand");
            you.Inventory.Clear();s.LevelIndex=2;
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo(CampLine),"mid-weekend the host has no festival to pick");
            s.LevelIndex=1;
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo("Browse gear • pay the seller • meet at the trailhead"),"before a night");
        }

        [Test] public void OnlyTheHostIsOfferedTheFestivalBeforeDayOne()
        {
            var s=Level(0,0,"Shopping");var you=Crew(s,"you","You");var sam=Crew(s,"sam","Sam");s.UnlockedFestivalCount=2;
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Does.StartWith("F  switch to Ember Playa (2 of 2 unlocked)"),"F names the festival it moves to; the banner names this one");
            Assert.That(FestivalHudText.NextFestival(s),Is.EqualTo(1),"F sends the festival it names");
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo(CampLine),"only the host picks the festival");
            s.FestivalIndex=1;
            Assert.That(FestivalHudText.FestivalChoice(s,"you"),Is.EqualTo("F  switch to Palm Mirage (1 of 2 unlocked)"),"F wraps round to the first festival");
            Assert.That(FestivalHudText.NextFestival(s),Is.EqualTo(0));
            s.LevelIndex=1;
            Assert.That(FestivalHudText.FestivalChoice(s,"you"),Is.Empty,"mid-weekend the festival is set");
            s.LevelIndex=0;s.FestivalIndex=0;s.UnlockedFestivalCount=1;
            Assert.That(FestivalHudText.FestivalChoice(s,"you"),Is.Empty,"nothing to pick until a second festival is unlocked");
        }

        [Test] public void ResultsSayHowTheLevelEndedAndWhatComesNext()
        {
            var day=Level(0,0,"Results");day.Result="Success";
            Assert.That(FestivalHudText.ObjectiveTitle(day,Crew(day,"you","You")),Is.EqualTo("QUOTA MET  •  DAY 1 CLEARED"));
            Assert.That(FestivalHudText.ObjectiveDetail(day,day.Players[0]),Is.EqualTo("Next up: Night 1. Cash and gear carry over."));
            var night=Level(0,1,"Results");night.Result="Success";
            Assert.That(FestivalHudText.OutcomeTitle(night),Is.EqualTo("FRIEND RESCUED  •  NIGHT 1 CLEARED"));
            night.SecondFriend.Active=true;
            Assert.That(FestivalHudText.OutcomeTitle(night),Is.EqualTo("FRIENDS RESCUED  •  NIGHT 1 CLEARED"),"a big crew's night rescues two friends");
            var finale=Level(0,3,"Results");finale.Result="Success";
            Assert.That(FestivalHudText.OutcomeTitle(finale),Is.EqualTo("EVERYONE HOME  •  PALM MIRAGE CLEARED"));
            Assert.That(FestivalHudText.OutcomeDetail(finale),Is.EqualTo("Next up: Ember Playa. A new weekend starts with fresh cash."));
            var last=Level(1,3,"Results");last.Result="Success";
            Assert.That(FestivalHudText.OutcomeDetail(last),Is.EqualTo("Next up: an encore at Palm Mirage, harder. A new weekend starts with fresh cash."));
            var missed=Level(1,2,"Results");missed.Result="Missed the quota";
            Assert.That(FestivalHudText.OutcomeTitle(missed),Is.EqualTo("WEEKEND OVER  •  BACK TO DAY 1"));
            Assert.That(FestivalHudText.OutcomeDetail(missed),Is.EqualTo("Missed the quota. Ember Playa restarts at Day 1 with fresh cash."));
            Assert.That(FestivalHudText.ResultsPrompt(missed,"you"),Is.EqualTo("ESC MENU  •  NEXT CAMP"),"the host brings the crew back");
            Assert.That(FestivalHudText.ResultsPrompt(missed,"sam"),Is.EqualTo("WAITING FOR THE HOST"));
        }

        [Test] public void TheDebriefNamesTheLevelTheCrewJustCleared()
        {
            // At the campfire the round has already moved on, so LevelIndex is the level coming up.
            var s=Level(0,1,"CampReview");s.ReviewResult="Success";
            Assert.That(FestivalHudText.ReviewOutcome(s),Is.EqualTo("DAY 1 CLEARED"));
            s.LevelIndex=0;
            Assert.That(FestivalHudText.ReviewOutcome(s),Is.EqualTo("WEEKEND CLEARED"),"a win that lands on Day 1 cleared Night 2");
            s.ReviewResult="Missed the quota";
            Assert.That(FestivalHudText.ReviewOutcome(s),Is.EqualTo("A GLORIOUS DISASTER  •  BACK TO DAY 1"),"a loss says what it cost (LOOP-5)");
        }

        // LOOP-5: the last minute of a day with the quota unmet warns that sundown restarts the weekend: once at a minute (as the
        // clock turns 1:00) and again at 15 s, in the viewer's money, and the objective card says so for that whole minute.
        [Test] public void SundownWarnsInTheLastMinuteOfADayWithTheQuotaUnmet()
        {
            var s=Level(0,0);var you=Crew(s,"you","You");s.LevelSales=3;
            s.ElapsedSeconds=419.5;
            Assert.That(FestivalHudText.Clock(s),Is.EqualTo("1:01"));
            Assert.That(FestivalHudText.SundownWarning(s,you),Is.Empty,"60.5 s left: no warning yet");
            Assert.That(FestivalHudText.QuotaHint(s,you),Is.EqualTo("Sell $12 more before sundown."));
            s.ElapsedSeconds=420;
            Assert.That(FestivalHudText.Clock(s),Is.EqualTo("1:00"));
            Assert.That(FestivalHudText.SundownWarning(s,you),Is.EqualTo("1 MINUTE TO SUNDOWN: sell $12 more or the weekend restarts at Day 1"));
            Assert.That(FestivalHudText.QuotaHint(s,you),Is.EqualTo("Sell $12 more before sundown or the weekend restarts."));
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Does.StartWith("Sell $12 more before sundown or the weekend restarts."),"the objective card says it for the whole last minute");
            s.ElapsedSeconds=464.9;
            Assert.That(FestivalHudText.SundownWarning(s,you),Is.EqualTo("1 MINUTE TO SUNDOWN: sell $12 more or the weekend restarts at Day 1"),"15.1 s left");
            s.ElapsedSeconds=465;
            Assert.That(FestivalHudText.SundownWarning(s,you),Is.EqualTo("15 SECONDS: sell $12 more or back to Day 1"));
            Assert.That(FestivalHudText.QuotaHint(s,you),Is.EqualTo("Sell $12 more before sundown or the weekend restarts."));
            s.LevelSales=15;
            Assert.That(FestivalHudText.SundownWarning(s,you),Is.Empty,"nothing once the quota is met");
            Assert.That(FestivalHudText.QuotaHint(s,you),Is.EqualTo("Anyone can end the day at the way back to camp, or keep selling until sundown."));
            s.LevelSales=3;s.Phase="Results";s.ElapsedSeconds=480;
            Assert.That(FestivalHudText.SundownWarning(s,you),Is.Empty,"at results the banner says what happened instead");
            var night=Level(0,1);var rescuer=Crew(night,"you","You");night.ElapsedSeconds=night.DurationSeconds-10;
            Assert.That(FestivalHudText.SundownWarning(night,rescuer),Is.Empty,"a night has no quota to miss");
            var playa=Level(1,0);var ash=new PlayerState{Id="ash",Name="Ash",Ordinal=0};playa.Players.Add(ash);playa.ElapsedSeconds=playa.DurationSeconds-30;
            Assert.That(FestivalHudText.SundownWarning(playa,ash),Is.EqualTo("1 MINUTE TO SUNDOWN: sell 22 buttons more or the weekend restarts at Day 1"),"in the viewer's own odd objects");
        }

        // LOOP-5: results take the middle of the screen. A loss says why, what the crew sold of its quota on a day, and that the
        // festival restarts at Day 1; a win says what was cleared and what comes next. The objective card keeps its own lines.
        [Test] public void TheResultsBannerSaysTheWeekendRestartsOrWhatComesNext()
        {
            var missed=Level(0,0,"Results");var you=Crew(missed,"you","You");missed.Result="Missed the quota";
            Assert.That(FestivalHudText.ResultsBannerTitle(missed),Is.EqualTo("MISSED THE QUOTA"));
            Assert.That(FestivalHudText.ResultsBannerDetail(missed,you),Is.EqualTo("You sold $0 of $15\nPALM MIRAGE RESTARTS AT DAY 1  •  FRESH CASH"));
            Assert.That(FestivalHudText.ObjectiveTitle(missed,you),Is.EqualTo("WEEKEND OVER  •  BACK TO DAY 1"),"the objective card keeps its lines");
            Crew(missed,"sam","Sam");missed.LevelSales=9;
            Assert.That(FestivalHudText.ResultsBannerDetail(missed,you),Does.StartWith("You sold $9 of $30\n"),"the whole crew's sales against its quota");
            var playaDay=Level(1,0,"Results");var sam=new PlayerState{Id="sam",Name="Sam",Ordinal=1};playaDay.Players.Add(sam);playaDay.Result="Missed the quota";playaDay.LevelSales=4;
            Assert.That(FestivalHudText.ResultsBannerDetail(playaDay,sam),Is.EqualTo("You sold 4 ramen packets of 22 ramen packets\nEMBER PLAYA RESTARTS AT DAY 1  •  FRESH CASH"),"in the viewer's money");
            var night=Level(1,1,"Results");Crew(night,"you","You");night.Result="Time expired";
            Assert.That(FestivalHudText.ResultsBannerTitle(night),Is.EqualTo("TIME EXPIRED"));
            Assert.That(FestivalHudText.ResultsBannerDetail(night,night.Players[0]),Is.EqualTo("EMBER PLAYA RESTARTS AT DAY 1  •  FRESH CASH"),"a night has no sales to count");
            night.Result=FestivalSimulation.WipedOut;
            Assert.That(FestivalHudText.ResultsBannerTitle(night),Is.EqualTo("THE CREW WAS WIPED OUT"));

            var day=Level(0,0,"Results");Crew(day,"you","You");day.Result="Success";
            Assert.That(FestivalHudText.ResultsBannerTitle(day),Is.EqualTo("DAY 1 CLEARED"));
            Assert.That(FestivalHudText.ResultsBannerDetail(day,day.Players[0]),Is.EqualTo("NEXT: NIGHT 1"));
            var finale=Level(0,3,"Results");Crew(finale,"you","You");finale.Result="Success";
            Assert.That(FestivalHudText.ResultsBannerTitle(finale),Is.EqualTo("PALM MIRAGE CLEARED"));
            Assert.That(FestivalHudText.ResultsBannerDetail(finale,finale.Players[0]),Is.EqualTo("NEXT: EMBER PLAYA"));
            var last=Level(1,3,"Results");Crew(last,"you","You");last.Result="Success";
            Assert.That(FestivalHudText.ResultsBannerDetail(last,last.Players[0]),Is.EqualTo("NEXT: AN ENCORE AT PALM MIRAGE"));

            var playing=Level(0,0);Crew(playing,"you","You");
            Assert.That(FestivalHudText.ResultsBannerTitle(playing),Is.Empty,"only at results");
            Assert.That(FestivalHudText.ResultsBannerDetail(playing,playing.Players[0]),Is.Empty);
        }

        // LOOP-5: back at camp after a lost level, the debrief and the level banner say the weekend went back to Day 1, until the
        // crew readies. A camp after a win, or a new crew's first, names the level up next.
        [Test] public void CampAfterALostLevelSaysBackToDayOne()
        {
            var lost=Level(0,0,"CampReview");lost.ReviewResult="Missed the quota";
            Assert.That(FestivalHudText.ReviewOutcome(lost),Is.EqualTo("A GLORIOUS DISASTER  •  BACK TO DAY 1"));
            Assert.That(FestivalHudText.LevelBanner(lost),Is.EqualTo("BACK TO DAY 1  •  PALM MIRAGE"));
            lost.Phase="Shopping";
            Assert.That(FestivalHudText.LevelBanner(lost),Is.EqualTo("BACK TO DAY 1  •  PALM MIRAGE"),"still at camp, shopping");
            lost.Phase="Spinning";
            Assert.That(FestivalHudText.LevelBanner(lost),Is.EqualTo("PALM MIRAGE  •  DAY 1"),"once the crew readies, the level itself");
            var encore=Level(1,0,"Shopping",encore:1);encore.ReviewResult="Time expired";
            Assert.That(FestivalHudText.LevelBanner(encore),Is.EqualTo("BACK TO DAY 1  •  EMBER PLAYA  •  ENCORE 1"));

            var won=Level(0,1,"CampReview");won.ReviewResult="Success";
            Assert.That(FestivalHudText.ReviewOutcome(won),Is.EqualTo("DAY 1 CLEARED"));
            Assert.That(FestivalHudText.LevelBanner(won),Is.EqualTo("UP NEXT  •  PALM MIRAGE  •  NIGHT 1"));
            Assert.That(FestivalHudText.LevelBanner(Level(0,0,"Shopping")),Is.EqualTo("UP NEXT  •  PALM MIRAGE  •  DAY 1"),"a new crew's first camp is a fresh start, not a restart");
        }

        // HUD-4: players act at camp and at the festival, nowhere else. The host refuses anything else and the HUD offers nothing
        // and sends no gear keys then (FestivalHud.UpdateActions, UpdateKeyboard), by this one rule; HudLoopPlayTests watches it live.
        [Test] public void NobodyActsWhileTheWheelsSpinOrTheFestivalLoads()
        {
            foreach(var phase in new[]{"Lobby","Spinning","Loading","CampReview","Results"})Assert.That(FestivalSimulation.Interactive(phase),Is.False,phase+" takes no actions");
            foreach(var phase in new[]{"Shopping","Playing"})Assert.That(FestivalSimulation.Interactive(phase),Is.True,phase+" does");
            var game=new FestivalSimulation(5);var you=game.AddPlayer("you","You");you.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
            int sent=0;
            foreach(var phase in new[]{"Spinning","Loading"})
            {
                game.State.Phase=phase;
                foreach(var kind in new[]{"Equip","Use","Drop"})
                {
                    var result=game.Execute("you",new GameCommand{Id="hud4-"+sent++,Kind=kind,ItemId="confetti"});
                    Assert.That(result.Accepted,Is.False,kind+" while "+phase);Assert.That(result.Reason,Is.EqualTo("Round is not interactive"),kind+" while "+phase);
                }
            }
        }

        // HUD-4: a menu left open closes itself as the wheels start, so nobody misses the spin, as it does when the festival opens
        // or the debrief begins. Opened again in the same phase, it stays (FestivalSession keeps it open while a key is rebound).
        [Test] public void TheMenuClosesAsTheWheelsStart()
        {
            Assert.That(FestivalSession.ClosesMenu("Spinning","Shopping"),Is.True,"the ready countdown ends in the spin");
            Assert.That(FestivalSession.ClosesMenu("Playing","Loading"),Is.True,"the festival opens");
            Assert.That(FestivalSession.ClosesMenu("CampReview","Results"),Is.True,"the debrief begins");
            foreach(var phase in new[]{"Spinning","Playing","CampReview"})Assert.That(FestivalSession.ClosesMenu(phase,phase),Is.False,"opened again during "+phase+", it stays open");
            Assert.That(FestivalSession.ClosesMenu("Loading","Spinning"),Is.False,"loading leaves it as it was");
            Assert.That(FestivalSession.ClosesMenu("Results","Playing"),Is.False,"results leave it open for NEXT ROUND");
            Assert.That(FestivalSession.ClosesMenu("Shopping","CampReview"),Is.False,"so does the camp shop opening");
        }
    }
}
