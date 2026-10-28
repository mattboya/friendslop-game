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

        // PLAYA-2: a client's debrief formats its sales in the money of the festival the round was played at, so its view carries
        // that festival even once clearing a weekend has moved camp on to the next one.
        [Test] public void ClientsSeeWhichFestivalTheDebriefReviews()
        {
            var game=new FestivalSimulation(12);game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");
            game.State.LevelIndex=Festivals.LevelCount-1;game.State.Phase="Results";game.State.Result="Success";
            Assert.That(game.Execute("host",new GameCommand{Id="back-to-camp",Kind="Reset"}).Accepted,Is.True);
            var view=FestivalSession.ViewFor(game,"friend");
            Assert.That(view.FestivalIndex,Is.EqualTo(Festivals.PlayaFestival),"setup: clearing Palm Mirage moves camp on to Ember Playa");
            Assert.That(view.ReviewFestivalIndex,Is.EqualTo(Festivals.PoloFestival),"the friend's debrief knows it reviews Palm Mirage");
        }

        // A build from before festival weekends sends ReviewVote with no friend named. The host refuses those votes, and the campfire
        // waits for every connected player's vote, so one old client would hold the debrief open forever: it must not get in.
        [Test] public void ABuildFromBeforeWeekendsCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":1}"),Is.False,"a pre-weekend client is told its version is incompatible");
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"New\",\"Token\":\"\",\"Protocol\":"+FestivalSession.ProtocolVersion+"}"),Is.True,"this build's own clients join");
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"New\",\"Token\":\""+new string('t',65)+"\",\"Protocol\":"+FestivalSession.ProtocolVersion+"}"),Is.False,"an oversized token is still refused");
        }

        // A weekend build from before DANCE-5 draws a different rhythm chart from the same ChartSeed than the host scores, and times
        // the spin differently, so every challenge it plays scores low: it must be told its version is incompatible too.
        [Test] public void ABuildWithTheOldRhythmChartsCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":2}"),Is.False,"a pre-DANCE-5 client is told its version is incompatible");
        }

        // TRIP-12: each client paints and lands the dose wheel from its own table, so a build with the old 31/31/31/7 wheel would
        // show a different wheel and landing angle than the host's 30/30/30/10 for the same spin: it must not get in either.
        [Test] public void ABuildWithTheOldDoseWheelCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":3}"),Is.False,"a client with the 7% dose sliver is told its version is incompatible");
        }

        [Test] public void ThisBuildAdvertisesTheVersionTwelveBoundary()
        {
            Assert.That(FestivalSession.ProtocolVersion,Is.EqualTo(12),"the new semantic boundary is version 12");
        }

        [Test] public void ACheckpointVersionSixBuildCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Checkpoint\",\"Token\":\"\",\"Protocol\":6}"),Is.False,"the published checkpoint must not join the new semantic boundary");
        }

        [Test] public void AVersionSevenBuildCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":7}"),Is.False,"a version 7 peer sprints mid-spin and misreads the side spin");
        }

        [Test] public void AVersionEightBuildCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":8}"),Is.False,"a version 8 peer's late E would stop a deck set that just began");
        }

        [Test] public void AVersionNineBuildCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":9}"),Is.False,"a version 9 peer plays three-round talks and the old DJ set");
        }

        [Test] public void AVersionTenBuildCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":10}"),Is.False,"a version 10 peer shows other players the grade of every reply and plays the old music radius");
        }

        [Test] public void AVersionElevenBuildCannotJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":11}"),Is.False,"a version 11 peer takes a late stop from a trust dance as a walk-off and seats Ferris wheel riders from its own view");
        }

        [Test] public void AVersionTwelveBuildCanJoin()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Current\",\"Token\":\"\",\"Protocol\":12}"),Is.True,"version 12 peers agree on the new semantic boundary");
        }
    }
}
