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
        [UnityTest] public IEnumerator DiscoverySaveErrorsSurfaceAndRetryWithoutWritingEveryFrame()
        {
            var host=new GameObject("impl:U31 discovery retry");var session=host.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);
            float deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(session.Profile.Save(),Is.Empty);
            string path=System.IO.Path.Combine(LocalProfile.Folder,"default.json");string previous=System.IO.File.ReadAllText(path);
            System.IO.File.Delete(path);System.IO.Directory.CreateDirectory(path);
            int failures=0;
            Application.LogCallback log=(message,stack,kind)=>{if(message.StartsWith("[Settings] Save failed:"))failures++;};
            Application.logMessageReceived+=log;
            try
            {
                var player=Simulation(session).Player(session.LocalPlayerId);player.X=23;player.Z=-6;session.Command("CampEgg","flamingo");
                yield return new WaitForSeconds(.3f);
                Assert.That(session.Message,Does.Contain("could not be saved"),"the normal session message surfaces a failed save");
                Assert.That(failures,Is.EqualTo(1),"a blocked disk does not trigger save I/O and diagnostic spam every frame");
                Assert.That(session.Profile.Data.FoundCampEggIds,Is.Empty);
                System.IO.Directory.Delete(path);System.IO.File.WriteAllText(path,previous);
                yield return new WaitForSeconds(2.1f);
                Assert.That(new LocalProfile("default").Data.FoundCampEggIds,Is.EqualTo(new[]{"flamingo"}),"the live cumulative mask retries without another poke");
                Assert.That(session.Message,Is.EqualTo("Camp discoveries saved."),"recovery replaces the still-visible failure with a truthful success");
                Assert.That(session.Profile.LastError,Is.Empty);
                previous=System.IO.File.ReadAllText(path);System.IO.File.Delete(path);System.IO.Directory.CreateDirectory(path);
                player.X=-14;player.Z=-11.2f;session.Command("CampEgg","wash_me");yield return new WaitForSeconds(.3f);
                Assert.That(failures,Is.EqualTo(2),"a new storage failure has its own first attempt");
                session.SetMessage("A newer camp reply.");
                System.IO.Directory.Delete(path);System.IO.File.WriteAllText(path,previous);yield return new WaitForSeconds(2.1f);
                Assert.That(new LocalProfile("default").Data.FoundCampEggIds,Is.EquivalentTo(new[]{"flamingo","wash_me"}));
                Assert.That(session.Message,Is.EqualTo("A newer camp reply."),"save recovery never replaces a newer gameplay reply");
            }
            finally{Application.logMessageReceived-=log;session.Leave();Object.Destroy(host);}
            yield return null;
        }

        [UnityTest] public IEnumerator TheLiveCampFollowerDeliversNewGagsOnceAndDoesNotReplayOnReconnect()
        {
            var host=new GameObject("impl:U31 live egg follower");var session=host.AddComponent<FestivalSession>();yield return null;
            var played=new System.Collections.Generic.List<string>();
            System.Action<CampEggState> follow=egg=>played.Add(egg.Id+"|"+egg.Word+"|"+egg.Deflated);
            typeof(FestivalSession).GetEvent("CampEggActivated")?.AddEventHandler(session,follow);
            session.Host("Tester",HostPort);
            float deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            Assert.That(played,Is.Empty,"a first snapshot arms playback silently");
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);var friend=game.AddPlayer("friend","Friend");
            var flamingo=game.State.CampEggs.Find(e=>e.Id=="flamingo");friend.X=flamingo.X;friend.Z=flamingo.Z;
            var command=new GameCommand{Id="follower_friend",Kind="CampEgg",TargetId="flamingo"};
            Assert.That(game.Execute(friend.Id,command).Accepted,Is.True);yield return null;yield return null;
            Assert.That(played,Is.EqualTo(new[]{"flamingo||True"}),"the real session delivers a friend's new public gag once");
            Assert.That(session.Profile.Data.FoundCampEggIds,Is.Empty,"public playback never awards local credit");
            game.Execute(friend.Id,command);yield return null;yield return null;
            Assert.That(played.Count,Is.EqualTo(1),"a repeated command and repeated views cannot replay the gag");
            player.X=-14;player.Z=-11.2f;session.Command("CampEgg","wash_me");yield return null;yield return null;
            Assert.That(played.Count,Is.EqualTo(2));Assert.That(played[1],Is.EqualTo("wash_me|"+session.State.CampEggs[7].Word+"|False"));
            session.Leave();yield return null;
            session.Host("Tester",HostPort);
            deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            game=Simulation(session);player=game.Player(session.LocalPlayerId);player.X=23;player.Z=-6;
            session.Command("CampEgg","flamingo");yield return null;yield return null;
            Assert.That(played.Count,Is.EqualTo(2),"reconnecting baselines existing one-shots even when recent");
            Assert.That(session.State.CampEggs[5].Deflated,Is.True,"the same baseline still contains current persistent deflation");
            player.X=-14;player.Z=-11.2f;session.Command("CampEgg","wash_me");yield return null;yield return null;
            Assert.That(played.Count,Is.EqualTo(3),"new events work again after reconnect");
            session.Leave();Object.Destroy(host);yield return null;
        }

        [UnityTest] public IEnumerator OnlyTheLocalOwnersAcceptedDiscoveriesSaveAcrossReloadsAndVisits()
        {
            var host=new GameObject("impl:U31 discovery saving");var session=host.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);
            float deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);var friend=game.AddPlayer("friend","Friend");
            var button=game.State.CampEggs.Find(e=>e.Id=="button");friend.X=button.X;friend.Z=button.Z;
            Assert.That(game.Execute(friend.Id,new GameCommand{Id="friend_button",Kind="CampEgg",TargetId="button"}).Accepted,Is.True);
            yield return null;yield return null;
            Assert.That(session.Profile.Data.FoundCampEggIds,Is.Empty,"watching a friend's accepted gag grants no discovery");
            session.Command("CampEgg","button");yield return null;yield return null;
            Assert.That(session.Profile.Data.FoundCampEggIds,Is.Empty,"a refused out-of-reach command grants no discovery");
            player.X=23;player.Z=-6;session.Command("CampEgg","flamingo");
            player.X=-14;player.Z=-11.2f;session.Command("CampEgg","wash_me");
            yield return null;yield return null;
            Assert.That(session.Profile.Data.FoundCampEggIds,Is.EquivalentTo(new[]{"flamingo","wash_me"}),"the cumulative owner mask recovers both discoveries between snapshots");
            Assert.That(new LocalProfile("default").Data.FoundCampEggIds,Is.EquivalentTo(new[]{"flamingo","wash_me"}),"the live session atomically saves the collection");
            session.Command("CampEgg","wash_me");yield return null;yield return null;
            Assert.That(session.Profile.Data.FoundCampEggIds.Count,Is.EqualTo(2),"repeat pokes do not inflate the collection");
            session.Leave();Object.Destroy(host);yield return null;
            var next=new GameObject("impl:U31 reloaded collection").AddComponent<FestivalSession>();yield return null;
            Assert.That(next.Profile.Data.FoundCampEggIds,Is.EquivalentTo(new[]{"flamingo","wash_me"}),"leaving and rebuilding the session keeps lifetime discoveries");
            next.Host("Tester",HostPort);yield return null;yield return null;
            Assert.That(next.LocalPlayer.FoundCampEggMask,Is.Zero,"a new camp starts with an empty session mask");
            Assert.That(next.Profile.Data.FoundCampEggIds.Count,Is.EqualTo(2),"an empty new mask cannot erase saved discoveries");
            next.Leave();Object.Destroy(next.gameObject);yield return null;
        }

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
