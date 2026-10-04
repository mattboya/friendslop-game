using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class CampEggPlayTests:FestivalPlayModeTest
    {
        [UnityTest] public IEnumerator HostedEggCommandsReachTheLiveSnapshotAndKeepFriendDiscoveriesPrivate()
        {
            var host=new GameObject("impl:U30 hosted eggs");var session=host.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);
            float deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);game.AddPlayer("friend","Friend");
            player.X=23;player.Z=-6;session.Command("CampEgg","flamingo");
            player.X=-14;player.Z=-11.2f;session.Command("CampEgg","wash_me");
            yield return null;yield return null;
            Assert.That(session.State.CampEggs.Count,Is.EqualTo(8),"the real session Update publishes the records");
            Assert.That(session.State.CampEggs[5].Deflated,Is.True);Assert.That(session.State.CampEggs[7].Word,Is.Not.Empty);
            Assert.That(session.LocalPlayer.FoundCampEggMask,Is.EqualTo(160),"the live owner snapshot accumulates both commands");
            Assert.That(session.LocalPlayer.LastCampEggId,Is.EqualTo("wash_me"));
            Assert.That(FestivalSession.ViewFor(game,"friend").Players.Find(p=>p.Id==player.Id).FoundCampEggMask,Is.Zero);
            session.Leave();Object.Destroy(host);yield return null;
        }

        [UnityTest] public IEnumerator HostedChairOwnershipExpiresOnTheLiveTickAndActorPoseResumes()
        {
            var world=new GameObject("impl:U30 chair world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U30 hosted chair");var session=host.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);
            float deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);player.X=-10;player.Z=3.5f;
            session.Command("CampEgg","chair");yield return null;yield return null;
            Assert.That(session.LocalPlayer.VisualPose,Is.EqualTo(FestivalSimulation.CampChairPose),"the running session receives the chair pose");
            var actors=(System.Collections.Generic.Dictionary<string,Transform>)typeof(FestivalSession).GetField("actors",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(session);
            var actor=actors[player.Id].GetComponent<FestivalCharacter>();
            Assert.That(actor,Is.Not.Null,"the real actor presenter creates the local attendee");
            Assert.That(actor.Pose,Is.EqualTo(FestivalSimulation.CampChairPose),"the live actor consumes the snapshot pose");
            yield return new WaitForSeconds(4.3f);
            Assert.That(game.State.CampEggs[2].SeatedPlayerId,Is.Empty,"the host's real Update ticks expire the owner");
            Assert.That(session.LocalPlayer.VisualPose,Is.EqualTo("Idle"));Assert.That(actor.Pose,Is.EqualTo("Idle"));
            session.Leave();Object.Destroy(host);Object.Destroy(world);yield return null;
        }
    }
}
