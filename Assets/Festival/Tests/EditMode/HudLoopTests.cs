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
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo("Sell $18 more before sundown."));
            s.LevelSales=31;
            Assert.That(FestivalHudText.ObjectiveTitle(s,you),Is.EqualTo("QUOTA MET — HEAD BACK TO CAMP"));
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo("Anyone can end the day at the way back to camp, or keep selling until sundown."));
            you.Life="Downed";
            Assert.That(FestivalHudText.ObjectiveTitle(s,you),Is.EqualTo(FestivalGuidance.Headline(s,you)),"a downed player still reads how to get help");
            var night=Level(0,1);var rescuer=Crew(night,"you","You");
            Assert.That(FestivalHudText.ObjectiveTitle(night,rescuer),Is.EqualTo(FestivalGuidance.Headline(night,rescuer)),"nights keep the rescue's directions");
            Assert.That(FestivalHudText.Quota(night,rescuer),Is.Empty,"a night has no quota");
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

        [Test] public void OnlyTheHostIsOfferedTheFestivalBeforeDayOne()
        {
            var s=Level(0,0,"Shopping");var you=Crew(s,"you","You");var sam=Crew(s,"sam","Sam");s.UnlockedFestivalCount=2;
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo("F  switch to Ember Playa (2 of 2 unlocked)"),"F names the festival it moves to; the banner names this one");
            Assert.That(FestivalHudText.NextFestival(s),Is.EqualTo(1),"F sends the festival it names");
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo("Browse gear • pay the seller • meet at the trailhead"),"only the host picks the festival");
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
            Assert.That(FestivalHudText.ReviewOutcome(s),Is.EqualTo("A GLORIOUS DISASTER"));
        }
    }
}
