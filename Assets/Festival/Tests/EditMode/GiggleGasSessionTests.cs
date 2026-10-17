using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // Each accepted Giggle Balloon take reaches every living friend's view as a public count, never as their gas. A session reports
    // each new take once, only for a player it draws, and never one from before it first saw that player in this round.
    public sealed class GiggleGasSessionTests
    {
        static int commands;
        static bool Take(FestivalSimulation game,string player)
        {
            var p=game.Player(player);p.X=CampFeatures.GiggleBalloonX;p.Z=CampFeatures.GiggleBalloonZ;
            return game.Execute(player,new GameCommand{Id="take"+commands++,Kind=FestivalSimulation.TakeGiggleBalloonKind}).Accepted;
        }
        static RoundState View(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        static FestivalSimulation Camp(int seed){var game=new FestivalSimulation(seed);game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");return game;}
        // Every hit has worn off, so the next two takes are accepted.
        static void WearOff(FestivalSimulation game)=>game.Tick(FestivalSimulation.GiggleGasOnsetSeconds+FestivalSimulation.GiggleGasSeconds+.2);

        GameObject host;FestivalSession session;readonly List<string> heard=new List<string>();
        [SetUp]public void Follow()
        {
            host=new GameObject("impl:U20 giggle take follower");session=host.AddComponent<FestivalSession>();heard.Clear();
            session.GiggleBalloonTaken+=(player,count)=>heard.Add(player+" x"+count);
        }
        [TearDown]public void Drop()=>Object.DestroyImmediate(host);

        [Test]public void AFirstSnapshotOnlyBaselinesThenEachNewTakeIsReportedOnce()
        {
            var game=Camp(0);Assert.That(Take(game,"host"),Is.True,"setup: a take before this view first looks");
            session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.Empty,"a first snapshot never replays an older take");
            Assert.That(Take(game,"host"),Is.True);var current=View(game,"friend");
            session.FollowPlayerEvents(current,"friend");session.FollowPlayerEvents(current,"friend");
            Assert.That(heard,Is.EqualTo(new[]{"host x1"}),"a new take is reported once, however often its snapshot is read");
            Assert.That(Take(game,"friend"),Is.True);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.EqualTo(new[]{"host x1","friend x1"}),"the viewer's own take is reported too");
            WearOff(game);Assert.That(Take(game,"host")&&Take(game,"host"),Is.True,"setup: two takes between snapshots");
            session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.EqualTo(new[]{"host x1","friend x1","host x2"}),"two takes in one snapshot are reported together");
        }

        [Test]public void PlayersTheViewDoesNotDrawAdvanceSilently()
        {
            var game=Camp(0);var friend=game.Player("friend");
            session.FollowPlayerEvents(View(game,"friend"),"friend");
            friend.CampVisitId="tent_1";Assert.That(Take(game,"host"),Is.True);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.Empty,"a take outside is not reported inside a tent");
            friend.CampVisitId="";session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.Empty,"nor once the viewer comes back out");
            Assert.That(Take(game,"host"),Is.True);game.Disconnect("host");session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.Empty,"a take just before its taker drops is not reported");
            game.AddPlayer("host","Host");session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.Empty,"nor when they reconnect");
            WearOff(game);friend.Life="Spirit";Assert.That(Take(game,"host"),Is.True);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.Empty,"a spirit's view carries no living taker");
            friend.Life="Alive";session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.Empty,"a player back in the view is seen afresh, with no replay");
            Assert.That(Take(game,"host"),Is.True);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.EqualTo(new[]{"host x1"}),"the next drawn take is reported");
        }

        [Test]public void NewRoundsReconnectsAndLowerCountsNeverReplay()
        {
            var game=Camp(0);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(Take(game,"host"),Is.True);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(heard,Is.EqualTo(new[]{"host x1"}),"setup: one reported take");
            var next=Camp(1);Assert.That(Take(next,"host")&&Take(next,"host"),Is.True);
            session.FollowPlayerEvents(View(next,"friend"),"friend");
            Assert.That(heard.Count,Is.EqualTo(1),"a new round baselines its own counts instead of comparing with the last");
            session.FollowPlayerEvents(null,"");Assert.That(Take(next,"friend"),Is.True,"setup: a take while this view is away");
            session.FollowPlayerEvents(View(next,"friend"),"friend");
            Assert.That(heard.Count,Is.EqualTo(1),"leaving and joining again baselines afresh");
            var current=View(next,"friend");var lower=View(next,"friend");lower.Players.Find(p=>p.Id=="host").VisualGiggleTakeSequence=0;
            session.FollowPlayerEvents(lower,"friend");session.FollowPlayerEvents(current,"friend");
            Assert.That(heard.Count,Is.EqualTo(1),"a lower count reports nothing, and the count before it never replays");
            WearOff(next);Assert.That(Take(next,"host"),Is.True);session.FollowPlayerEvents(View(next,"friend"),"friend");
            Assert.That(heard,Is.EqualTo(new[]{"host x1","host x1"}),"and the next take is reported once");
        }

        [Test]public void FriendsSeeTheTakeCountButNeverTheGas()
        {
            var game=Camp(0);Assert.That(Take(game,"host")&&Take(game,"host"),Is.True,"setup: two hits");
            var seen=View(game,"friend").Players.Find(p=>p.Id=="host");var own=View(game,"host").Players.Find(p=>p.Id=="host");
            Assert.That(seen.VisualGiggleTakeSequence,Is.EqualTo(2),"a friend's view counts both takes through the real wire");
            Assert.That(seen.Effects,Is.Empty,"but carries none of the taker's gas");
            Assert.That(seen.VisualGiggling,Is.False,"nor a giggle while it kicks in");
            Assert.That(own.VisualGiggleTakeSequence,Is.EqualTo(2));Assert.That(own.Effects.Count,Is.EqualTo(2),"the taker's own view keeps both hits");
        }

        [Test]public void ASpiritsViewCarriesNoLivingTakersCount()
        {
            var game=Camp(0);Assert.That(Take(game,"host"),Is.True);game.Player("friend").Life="Spirit";
            var view=View(game,"friend");
            Assert.That(view.Players.Exists(p=>p.Id=="host"),Is.False,"a spirit receives no living player's record");
            Assert.That(view.Players.Find(p=>p.Id=="friend").VisualGiggleTakeSequence,Is.Zero);
        }

        [Test]public void OlderJsonWithoutTheTakeCountRestoresAtZero()
        {
            var game=Camp(0);Assert.That(Take(game,"host"),Is.True);
            string json=System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(game.State),"\"VisualGiggleTakeSequence\":-?[0-9]+,","");
            Assert.That(json,Does.Not.Contain("VisualGiggleTakeSequence"),"the fixture represents a snapshot from before the count");
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(json));
            Assert.That(restored.Player("host").VisualGiggleTakeSequence,Is.Zero);Assert.That(restored.Player("host").Effects.Count,Is.EqualTo(1),"the older hit survives");
            Assert.That(Take(restored,"host"),Is.True);Assert.That(restored.Player("host").VisualGiggleTakeSequence,Is.EqualTo(1),"and the count goes on from zero");
        }
    }
}
