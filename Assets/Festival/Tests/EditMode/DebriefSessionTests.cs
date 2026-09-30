using NUnit.Framework;
using Festival.Core;
using Festival.Network;

namespace Festival.Tests
{
    // DEBRIEF-1: what each client's snapshot carries during the campfire debrief.
    public sealed class DebriefSessionTests
    {
        static void Vote(FestivalSimulation game,string voter,string pick)
        {
            for(int award=0;award<3;award++)Assert.That(game.Execute(voter,new GameCommand{Id=voter+"-vote-"+award,Kind="ReviewVote",TargetId=pick,Amount=award}).Accepted,Is.True,voter+" votes for award "+award);
        }

        [Test] public void ClientsSeeTheAwardsButNoOnesPicksUntilTheReveal()
        {
            var game=new FestivalSimulation(12);game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");
            game.State.Phase="Results";game.State.Result="Success";
            Assert.That(game.Execute("host",new GameCommand{Id="back-to-camp",Kind="Reset"}).Accepted,Is.True);
            Vote(game,"host","friend");

            var early=FestivalSession.ViewFor(game,"friend");
            Assert.That(game.State.ReviewAwards,Has.Count.EqualTo(3),"setup: a crew of two draws three awards");
            Assert.That(early.ReviewAwards,Is.EqualTo(game.State.ReviewAwards),"everyone sees tonight's three awards");
            Assert.That(early.ReviewVotes,Has.Count.EqualTo(3),"everyone sees who has voted");
            Assert.That(early.ReviewVotes.TrueForAll(v=>v.TargetId==""),Is.True,"nobody sees whom another player picked before the reveal");
            Assert.That(early.ReviewWinners,Is.Empty);
            Assert.That(FestivalSession.ViewFor(game,"host").ReviewVotes.TrueForAll(v=>v.TargetId=="friend"),Is.True,"your own picks come back to you");

            Vote(game,"friend","friend");
            var revealed=FestivalSession.ViewFor(game,"host");
            Assert.That(revealed.ReviewWinners,Is.EqualTo(new[]{"friend","friend","friend"}),"the verdict is public");
            Assert.That(revealed.ReviewVotes.Exists(v=>v.PlayerId=="friend"&&v.TargetId=="friend"),Is.True,"after the reveal every pick is public");
            Assert.That(revealed.Players.Find(p=>p.Id=="friend").Badge,Is.EqualTo(string.Join(", ",game.State.ReviewAwards)),"everyone sees the winner's badge");
        }
    }
}
