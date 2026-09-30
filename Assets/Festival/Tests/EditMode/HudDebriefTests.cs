using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;

namespace Festival.Tests
{
    // HUD-3: the campfire debrief a player reads: their own ballot, who has voted (never whom), the verdict played out award by
    // award with the shots it pours, and the award each winner wears through the next level. Wording comes from crafted round
    // states the way a client's view holds them; privacy and badges from real views (FestivalSession.ViewFor).
    public sealed class HudDebriefTests
    {
        const double Step=FestivalHudText.RevealStepSeconds;
        // Every debrief draws two worst awards, then one best (FestivalSimulation.DrawReviewAwards).
        static RoundState Campfire(params string[] winners)
        {
            var s=new RoundState{Phase="CampReview",HostPlayerId="ash",ReviewResult="Success",LevelIndex=1};
            s.ReviewAwards.AddRange(new[]{"Worst Dancer","Got Lost","MVP Tripper"});s.ReviewWinners.AddRange(winners);
            return s;
        }
        static PlayerState Crew(RoundState s,string id,string name){var p=new PlayerState{Id=id,Name=name};s.Players.Add(p);return p;}
        static void Vote(RoundState s,string voter,string pick,params int[] awards){foreach(var award in awards)s.ReviewVotes.Add(new CampReviewVote{PlayerId=voter,TargetId=pick,Award=award});}
        // A real campfire after a won Day 1, the awards drawn and nobody's vote in yet.
        static FestivalSimulation Game(params (string id,string name)[] crew)
        {
            var game=new FestivalSimulation(12);foreach(var (id,name) in crew)game.AddPlayer(id,name);
            game.State.Phase="Results";game.State.Result="Success";
            Assert.That(game.Execute(crew[0].id,new GameCommand{Id="back_to_camp",Kind="Reset"}).Accepted,Is.True,"setup: the host brings the crew back to camp");
            Assert.That(game.State.ReviewAwards,Has.Count.EqualTo(3),"setup: three awards are up for a vote");
            return game;
        }
        static void Ballot(FestivalSimulation game,string voter,string pick,params int[] awards)
        {
            foreach(var award in awards)Assert.That(game.Execute(voter,new GameCommand{Id=voter+"_votes_"+award,Kind="ReviewVote",TargetId=pick,Amount=award}).Accepted,Is.True,voter+" votes on award "+award);
        }

        [Test] public void YourBallotShowsYourOwnPickForEachAward()
        {
            var s=Campfire();Crew(s,"ash","Ash");Crew(s,"sam","Sam");
            Vote(s,"ash","sam",0);Vote(s,"ash","ash",2);Vote(s,"sam","",1);
            Assert.That(FestivalHudText.Ballot(s,"ash",0),Is.EqualTo("1  WORST DANCER  •  YOUR PICK: SAM"));
            Assert.That(FestivalHudText.Ballot(s,"ash",1),Is.EqualTo("2  GOT LOST  •  PICK A FRIEND"),"Sam's vote on this award is Sam's, not yours");
            Assert.That(FestivalHudText.Ballot(s,"ash",2),Is.EqualTo("3  MVP TRIPPER  •  YOUR PICK: YOU"),"you may vote for yourself");
        }

        [Test] public void EveryoneSeesWhoHasVotedButNeverWhom()
        {
            // Ash has picked Kim for every award and Sam has picked Kim for one; Pat left the crew before the debrief.
            var game=Game(("ash","Ash"),("sam","Sam"),("kim","Kim"),("pat","Pat"));game.Disconnect("pat");
            Ballot(game,"ash","kim",0,1,2);Ballot(game,"sam","kim",0);
            var kim=FestivalSession.ViewFor(game,"kim");
            Assert.That(FestivalHudText.ReviewStatus(kim,"kim",0),Is.EqualTo("CLICK A FRIEND FOR EACH AWARD, OR PRESS 1–3\nVOTED  ASH  •  WAITING ON  SAM, YOU"),
                "a half-done ballot still counts as waiting, and nobody waits for a player who left");
            Assert.That(FestivalHudText.ReviewReveal(kim,"kim",99),Is.Empty,"nothing of the verdict shows before the whole crew has voted");
            Assert.That(FestivalHudText.Ballot(FestivalSession.ViewFor(game,"sam"),"sam",0),Is.EqualTo("1  "+game.State.ReviewAwards[0].ToUpperInvariant()+"  •  YOUR PICK: KIM"),"Sam's own pick comes back to Sam");
        }

        [Test] public void TheVerdictPlaysOutAwardByAwardThenPoursTheShots()
        {
            var s=Campfire("sam","ash","kim");Crew(s,"ash","Ash");Crew(s,"sam","Sam");Crew(s,"kim","Kim");
            Assert.That(FestivalHudText.ReviewReveal(s,"ash",0),Is.EqualTo("AND THE AWARDS GO TO…\nWORST DANCER  •  ?\nGOT LOST  •  ?\nMVP TRIPPER  •  ?"),"every vote is in: the drumroll");
            Assert.That(FestivalHudText.ReviewReveal(s,"ash",Step),Is.EqualTo("AND THE AWARDS GO TO…\nWORST DANCER  •  SAM\nGOT LOST  •  ?\nMVP TRIPPER  •  ?"),"one award at a time");
            Assert.That(FestivalHudText.ReviewReveal(s,"ash",3*Step-.01),Is.EqualTo("AND THE AWARDS GO TO…\nWORST DANCER  •  SAM\nGOT LOST  •  YOU\nMVP TRIPPER  •  ?"));
            Assert.That(FestivalHudText.ReviewReveal(s,"ash",3*Step),Is.EqualTo("AND THE AWARDS GO TO…\nWORST DANCER  •  SAM\nGOT LOST  •  YOU\nMVP TRIPPER  •  KIM"),"the last winner, the shots still to come");
            Assert.That(FestivalHudText.RevealDone(s,4*Step-.01),Is.False);
            Assert.That(FestivalHudText.ReviewReveal(s,"ash",4*Step),Is.EqualTo("AND THE AWARDS GO TO…\nWORST DANCER  •  SAM\nGOT LOST  •  YOU\nMVP TRIPPER  •  KIM\nSAM TAKES A SHOT\nYOU TAKE A SHOT"),
                "each worst winner takes a shot; the best one does not");
            Assert.That(FestivalHudText.RevealDone(s,4*Step),Is.True);
            Assert.That(FestivalHudText.ReviewReveal(s,"kim",4*Step),Does.EndWith("MVP TRIPPER  •  YOU\nSAM TAKES A SHOT\nASH TAKES A SHOT"),"everyone watches the same verdict");
            s.ReviewWinners[1]="sam";
            Assert.That(FestivalHudText.ReviewReveal(s,"ash",4*Step),Does.EndWith("\nSAM TAKES 2 SHOTS"),"both worst awards: two shots");
        }

        [Test] public void OnlyTheHostOpensTheShopOnceTheVerdictHasPlayed()
        {
            var s=Campfire("sam","sam","ash");Crew(s,"ash","Ash");Crew(s,"sam","Sam");
            Assert.That(FestivalHudText.ReviewStatus(s,"ash",4*Step-.01),Is.Empty,"the verdict is still playing");
            Assert.That(FestivalHudText.ReviewStatus(s,"ash",4*Step),Is.EqualTo("E  OPEN THE CAMP SHOP"));
            Assert.That(FestivalHudText.ReviewStatus(s,"sam",4*Step),Is.EqualTo("WAITING FOR THE HOST TO OPEN THE SHOP"));
            var solo=new RoundState{Phase="CampReview",HostPlayerId="ash",ReviewResult="Success"};Crew(solo,"ash","Ash");
            Assert.That(FestivalHudText.ReviewReveal(solo,"ash",0),Is.EqualTo("SOLO PRACTICE: NO AWARDS TONIGHT"));
            Assert.That(FestivalHudText.RevealDone(solo,0),Is.True,"solo practice skips straight to the shop");
            Assert.That(FestivalHudText.ReviewStatus(solo,"ash",0),Is.EqualTo("E  OPEN THE CAMP SHOP"));
        }

        [Test] public void ThePointerIsFreeOnlyWhileTheVoteIsOpen()
        {
            var s=Campfire();Crew(s,"ash","Ash");Crew(s,"sam","Sam");
            Assert.That(FestivalHudText.VoteOpen(s),Is.True,"click a friend for each award");
            s.ReviewWinners.AddRange(new[]{"sam","sam","ash"});
            Assert.That(FestivalHudText.VoteOpen(s),Is.False,"the verdict is in: back to looking around");
            s.ReviewWinners.Clear();s.Phase="Shopping";
            Assert.That(FestivalHudText.VoteOpen(s),Is.False,"the shop is open");
            var solo=new RoundState{Phase="CampReview"};Crew(solo,"ash","Ash");
            Assert.That(FestivalHudText.VoteOpen(solo),Is.False,"solo practice has nothing to vote on");
        }

        [Test] public void TheReviewSaysHowTheRoundWent()
        {
            var s=Campfire();var ash=Crew(s,"ash","Ash");s.ReviewSales=45;s.ReviewSurvivors=2;s.ReviewAntics=3;
            Assert.That(FestivalHudText.ReviewHeadline(s,ash),Is.EqualTo("THE VERY OFFICIAL ROUND REVIEW\nDAY 1 CLEARED  •  SALES $45  •  SURVIVORS 2  •  CAMP ANTICS 3"));
            s.FestivalIndex=1;ash.Ordinal=1;
            Assert.That(FestivalHudText.ReviewHeadline(s,ash),Does.Contain("SALES 45 RAMEN PACKETS"),"Ember Playa's camp counts your odd objects");
        }

        [Test] public void WinnersWearTheirAwardsOverTheirHeads()
        {
            var game=Game(("ash","Ash"),("sam","Sam"));
            Ballot(game,"ash","sam",0,1);Ballot(game,"ash","ash",2);Ballot(game,"sam","sam",0,1);Ballot(game,"sam","ash",2);
            var awards=game.State.ReviewAwards;
            // The verdict is in but still being read out at the campfire: a tag must not give a winner away before the drumroll does.
            var campfire=FestivalSession.ViewFor(game,"ash");
            Assert.That(FestivalHudText.Nameplate(campfire,campfire.Players.Find(p=>p.Id=="sam")),Is.EqualTo("Sam"),"no award over anyone's head during the reveal");
            Assert.That(FestivalHudText.Nameplate(campfire,campfire.Players.Find(p=>p.Id=="ash")),Is.EqualTo("Ash"));
            // The next level starts at the camp shop, and the winners wear their awards from there.
            Assert.That(game.Execute("ash",new GameCommand{Id="open_shop",Kind="FinishReview"}).Accepted,Is.True,"setup: the host opens the shop");
            var view=FestivalSession.ViewFor(game,"ash");
            Assert.That(FestivalHudText.Nameplate(view,view.Players.Find(p=>p.Id=="sam")),Is.EqualTo("Sam  •  "+awards[0]+", "+awards[1]),"a double winner wears both awards");
            Assert.That(FestivalHudText.Nameplate(view,view.Players.Find(p=>p.Id=="ash")),Is.EqualTo("Ash  •  "+awards[2]),"the best award is worn too");
            Assert.That(FestivalHudText.Nameplate(view,new PlayerState{Name="Kim"}),Is.EqualTo("Kim"),"no award, just the name");
        }
    }
}
