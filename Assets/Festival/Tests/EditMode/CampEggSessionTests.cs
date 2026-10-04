using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using System.Collections.Generic;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class CampEggSessionTests
    {
        static CommandResult Poke(FestivalSimulation game,string player,string egg,string command)
        {
            var record=game.State.CampEggs.Find(e=>e.Id==egg);var p=game.Player(player);p.X=record.X;p.Z=record.Z;
            return game.Execute(player,new GameCommand{Id=command,Kind="CampEgg",TargetId=egg});
        }
        static RoundState View(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));

        [Test] public void FirstNewRoundAndReconnectedSnapshotsExposePersistentPropsWithoutOldPlayback()
        {
            var host=new GameObject("impl:U31 persistent camp follower");var session=host.AddComponent<FestivalSession>();
            var played=new List<string>();session.CampEggActivated+=egg=>played.Add(egg.Id);
            try
            {
                var game=new FestivalSimulation(0);game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");
                Assert.That(Poke(game,"host","flamingo","old_flame").Accepted,Is.True);
                Assert.That(Poke(game,"host","wash_me","old_dust").Accepted,Is.True);
                session.FollowCampEggs(View(game,"friend"));
                Assert.That(played,Is.Empty,"first snapshots never replay even very recent one-shots");
                Assert.That(session.CampEggStates[5].Deflated,Is.True,"persistent deflation is available on that first snapshot");
                Assert.That(session.CampEggStates[7].Word,Is.EqualTo(game.State.CampEggs[7].Word),"host-picked dust is available immediately");
                Assert.That(Poke(game,"host","button","new_button").Accepted,Is.True);session.FollowCampEggs(View(game,"friend"));
                Assert.That(played,Is.EqualTo(new[]{"button"}));
                var next=new FestivalSimulation(1);next.AddPlayer("host","Host");next.AddPlayer("friend","Friend");
                Assert.That(Poke(next,"host","flamingo","next_flame").Accepted,Is.True);session.FollowCampEggs(View(next,"friend"));
                Assert.That(played.Count,Is.EqualTo(1),"new rounds baseline their own sequences instead of comparing with the prior visit");
                Assert.That(session.CampEggStates[5].Deflated,Is.True);Assert.That(session.CampEggStates[7].Word,Is.Empty,"dust resets with the new round");
                session.FollowCampEggs(null);Assert.That(session.CampEggStates,Is.Empty);
                session.FollowCampEggs(View(next,"friend"));Assert.That(played.Count,Is.EqualTo(1),"reconnect arms a fresh baseline");
                Assert.That(Poke(next,"host","wash_me","next_dust").Accepted,Is.True);session.FollowCampEggs(View(next,"friend"));
                Assert.That(played,Is.EqualTo(new[]{"button","wash_me"}));
            }
            finally{Object.DestroyImmediate(host);}
        }

        [Test] public void DelayedRepeatedAndRegressingSnapshotsDoNotReplayTransientGags()
        {
            var host=new GameObject("impl:U31 delayed camp follower");var session=host.AddComponent<FestivalSession>();
            var played=new List<string>();session.CampEggActivated+=egg=>played.Add(egg.Id);
            try
            {
                var game=new FestivalSimulation(0);game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");
                session.FollowCampEggs(View(game,"friend"));
                Assert.That(Poke(game,"host","flamingo","delayed_flame").Accepted,Is.True);game.Tick(3);
                var delayed=View(game,"friend");session.FollowCampEggs(delayed);
                Assert.That(played,Is.Empty,"old one-shots expire after the two-second gag window");Assert.That(session.CampEggStates[5].Deflated,Is.True);
                Assert.That(Poke(game,"host","button","fresh_button").Accepted,Is.True);var current=View(game,"friend");session.FollowCampEggs(current);
                Assert.That(played,Is.EqualTo(new[]{"button"}));
                session.FollowCampEggs(current);session.FollowCampEggs(delayed);session.FollowCampEggs(current);
                Assert.That(played.Count,Is.EqualTo(1),"duplicate and older snapshots never move a playback cursor backwards");
                Assert.That(Poke(game,"host","wash_me","fresh_dust").Accepted,Is.True);game.Tick(2);session.FollowCampEggs(View(game,"friend"));
                Assert.That(played,Is.EqualTo(new[]{"button","wash_me"}),"the two-second boundary is inclusive");
                Assert.That(Poke(game,"host","button","regressed_time").Accepted,Is.True);var badClock=View(game,"friend");badClock.SimulationSeconds=3;badClock.CampEggs[1].LastActivationAt=2.5;
                session.FollowCampEggs(badClock);Assert.That(played.Count,Is.EqualTo(2),"a higher sequence cannot replay an activation whose clock moved backwards");
                badClock.CampEggs[1].Sequence++;badClock.CampEggs[1].LastActivationAt=2.75;
                session.FollowCampEggs(badClock);Assert.That(played.Count,Is.EqualTo(2),"rejected timestamps cannot move the activation cursor backwards either");
                Assert.That(Poke(game,"host","cone","future_cone").Accepted,Is.True);var future=View(game,"friend");future.CampEggs[6].LastActivationAt=future.SimulationSeconds+1;
                session.FollowCampEggs(future);Assert.That(played.Count,Is.EqualTo(2),"future activation timestamps never play early");
            }
            finally{Object.DestroyImmediate(host);}
        }

        [Test] public void DebugEggDiagnosticsExplainAcceptanceAndRefusalWithoutCollectionData()
        {
            var game=new FestivalSimulation(0);game.AddPlayer("host","Host");var events=new System.Collections.Generic.List<string>();
            System.Action<string,string,string,double> log=(action,reason,round,time)=>events.Add(action+"|"+reason+"|"+round);
            typeof(FestivalSimulation).GetField("CampEggDiagnostic")?.SetValue(game,log);
            Assert.That(Poke(game,"host","button","button").Accepted,Is.True);
            game.Player("host").Ready=true;Assert.That(Poke(game,"host","button","blocked").Accepted,Is.False);
            Assert.That(events,Does.Contain("CampEgg|button Accepted|"+game.State.RoundId),"debug acceptance names the shared egg and round");
            Assert.That(events.Exists(e=>e.StartsWith("CampEgg|Refused: Camp secrets need")),Is.True,"debug refusal reports the eligibility reason");
        }

        [Test] public void SharedEggsAndWordsSurviveTheRealWireWhileDiscoveryIsOwnerOnly()
        {
            var game=new FestivalSimulation(0);var host=game.AddPlayer("host","Host");var friend=game.AddPlayer("friend","Friend");
            game.State.DealSeed=489;game.State.Npcs[0].Role="Narc";
            host.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});host.Dialogue.Seen.Add("private_dialogue");
            game.State.Visions.Add(new VisionState{Id="private_vision",NpcId=game.State.Npcs[0].Id,Kind="Narc",IsTrue=true});
            Assert.That(Poke(game,host.Id,"button","button").Accepted,Is.True);
            Assert.That(Poke(game,host.Id,"flamingo","flamingo").Accepted,Is.True);
            Assert.That(Poke(game,host.Id,"wash_me","dust").Accepted,Is.True);
            var mine=View(game,host.Id);var theirs=View(game,friend.Id);
            Assert.That(mine.CampEggs.Count,Is.EqualTo(8),"the whitelist must publish all eight shared records");
            Assert.That(JsonUtility.ToJson(mine.CampEggs[7]),Is.EqualTo(JsonUtility.ToJson(theirs.CampEggs[7])),"everyone reads the host-picked word, activation and time");
            Assert.That(new[]{"BASS","HELP","NARC?","MOM SAYS HI"},Does.Contain(theirs.CampEggs[7].Word));Assert.That(theirs.CampEggs[5].Deflated,Is.True);
            Assert.That(mine.Players.Find(p=>p.Id==host.Id).FoundCampEggMask,Is.EqualTo(162),"an owner recovers discoveries missed between snapshots");
            Assert.That(theirs.Players.Find(p=>p.Id==host.Id).FoundCampEggMask,Is.Zero,"friends never receive another player's collection");
            Assert.That(theirs.Players.Find(p=>p.Id==friend.Id).FoundCampEggMask,Is.Zero,"watching does not earn credit");
            Assert.That(theirs.Players.Find(p=>p.Id==host.Id).LastCampEggId,Is.EqualTo("wash_me"),"the last gag is public");
            Assert.That(theirs.Players.Find(p=>p.Id==host.Id).CampEggSequence,Is.EqualTo(3));
            Assert.That(theirs.Players.Find(p=>p.Id==host.Id).Inventory,Is.Empty);Assert.That(theirs.Players.Find(p=>p.Id==host.Id).Cash,Is.Zero);
            Assert.That(theirs.Players.Find(p=>p.Id==host.Id).Dialogue.Seen,Is.Empty);Assert.That(theirs.DealSeed,Is.Zero);
            Assert.That(theirs.Npcs[0].Role,Is.Empty);Assert.That(theirs.Visions,Is.Empty);Assert.That(theirs.Commands,Is.Empty);
        }

        [Test] public void TheGnomeHasOnePublishedPointAndViewsCannotMutateHostProps()
        {
            var game=new FestivalSimulation(1);game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");
            var mine=FestivalSession.ViewFor(game,"host");var theirs=View(game,"friend");
            Assert.That(mine.CampEggs.Count,Is.EqualTo(8));Assert.That(theirs.CampEggs[0].X,Is.EqualTo(11));Assert.That(theirs.CampEggs[0].Z,Is.EqualTo(19));
            Assert.That(mine.CampEggs[0].X,Is.EqualTo(theirs.CampEggs[0].X),"the host and client use the same selected gnome point");
            mine.CampEggs[0].X=999;mine.CampEggs[5].Deflated=true;mine.CampEggs[7].Word="tampered";
            Assert.That(game.State.CampEggs[0].X,Is.EqualTo(11));Assert.That(game.State.CampEggs[5].Deflated,Is.False);Assert.That(game.State.CampEggs[7].Word,Is.Empty);
        }

        [Test] public void ChairOwnershipPublishesAShortPoseAndMovementReturnsToIdle()
        {
            var game=new FestivalSimulation(0);game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");
            Assert.That(Poke(game,"host","chair","sit").Accepted,Is.True);
            var theirs=View(game,"friend");var chair=theirs.CampEggs.Find(e=>e.Id=="chair");
            Assert.That(chair,Is.Not.Null,"chair ownership must survive the real snapshot whitelist");
            Assert.That(chair.SeatedPlayerId,Is.EqualTo("host"));Assert.That(chair.SeatedUntil,Is.EqualTo(4));
            Assert.That(theirs.Players.Find(p=>p.Id=="host").VisualPose,Is.EqualTo(FestivalSimulation.CampChairPose));
            var host=game.Player("host");Assert.That(game.TryMove(host.Id,host.X+.2f,host.Z,0,.1),Is.True);
            Assert.That(View(game,"friend").Players.Find(p=>p.Id=="host").VisualPose,Is.EqualTo("Idle"));
            Assert.That(Poke(game,"host","chair","sit_again").Accepted,Is.True);game.Tick(4.1);
            Assert.That(View(game,"friend").Players.Find(p=>p.Id=="host").VisualPose,Is.EqualTo("Idle"));
        }

        [Test] public void OlderJsonWithoutEggFieldsRestoresWithNoDiscoveries()
        {
            var game=new FestivalSimulation(0);game.AddPlayer("host","Host");
            string json=JsonUtility.ToJson(game.State);
            json=System.Text.RegularExpressions.Regex.Replace(json,"\\\"CampEggs\\\":\\[.*?\\],","");
            json=System.Text.RegularExpressions.Regex.Replace(json,"\\\"(?:LastCampEggId|LastCampEggLine|CampEggSequence|FoundCampEggMask|LastCampEggAt)\\\":(?:\\\"[^\\\"]*\\\"|[-0-9.]+),","");
            Assert.That(json,Does.Not.Contain("CampEggs"),"the fixture represents a snapshot from before this feature");
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(json));
            Assert.That(restored.State.CampEggs.Count,Is.EqualTo(8));Assert.That(View(restored,"host").Players[0].FoundCampEggMask,Is.Zero);
            Assert.That(Poke(restored,"host","gnome","legacy_poke").Accepted,Is.True,"old snapshots remain interactive");
        }
    }
}
