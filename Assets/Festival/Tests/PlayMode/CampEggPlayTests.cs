using System;
using System.Collections;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Festival.Tests
{
    public sealed class CampEggPlayTests:FestivalPlayModeTest
    {
        // In a hosted camp with the real world and HUD, the aim dot on each secret's prop offers its own E, and E sends the host
        // command that pokes it. Aimed elsewhere, the DJ table and car_1's door keep E, and an E offered a moment ago checks the aim again.
        [UnityTest] public IEnumerator EAimedAtEachSecretsPropPokesItWithoutStealingTheDjOrTheCarDoor()
        {
            var world=new GameObject("impl:U65 aimed eggs world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U65 aimed eggs");var session=host.AddComponent<FestivalSession>();var hud=host.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
                var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                var primary=typeof(FestivalHud).GetField("primaryAction",flags);var yaw=typeof(FestivalSession).GetField("yaw",flags);var pitch=typeof(FestivalSession).GetField("pitch",flags);
                string Prompt(){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name=="Prompt")return text.text;return null;}
                IEnumerator StandAndLook(Vector3 stand,Vector3 at)
                {
                    player.X=stand.x;player.Z=stand.z;var look=at-new Vector3(stand.x,1.65f,stand.z);
                    yaw.SetValue(session,Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg);pitch.SetValue(session,-Mathf.Asin(look.normalized.y)*Mathf.Rad2Deg);
                    yield return new WaitForSeconds(.5f);
                }
                CampEggState Egg(string id)=>game.State.CampEggs.Find(e=>e.Id==id);
                // Each standing point is 1.6 m from the secret, toward the middle of camp; the chair comes last, as sitting holds the player.
                foreach(var id in new[]{"gnome","lost_found","hammock","flamingo","cone","wash_me","button","chair"})
                {
                    var egg=Egg(id);var toward=new Vector2(-egg.X,-egg.Z).normalized*1.6f;
                    yield return StandAndLook(new Vector3(egg.X+toward.x,0,egg.Z+toward.y),FestivalCampEggView.AimPoint(egg));
                    Assert.That(Prompt(),Is.EqualTo(FestivalCampEggView.Prompt(id).Substring(3)),"aimed at the "+id+" prop, the prompt offers it");
                    Assert.That(session.ViewCamera.WorldToViewportPoint(world.CampEggs.Prop(id).GetComponentInChildren<Renderer>().bounds.center).z,Is.GreaterThan(0),"setup: the "+id+" prop is in front of the view");
                    ((Action)primary.GetValue(hud))();
                    for(deadline=Time.realtimeSinceStartup+2;Egg(id).ActivationCount==0&&Time.realtimeSinceStartup<deadline;)yield return null;
                    Assert.That(Egg(id).ActivationCount,Is.EqualTo(1),"E pokes the "+id+": "+session.Message);
                    Assert.That(player.LastCampEggId,Is.EqualTo(id));
                }
                Assert.That(player.FoundCampEggMask,Is.EqualTo(255),"all eight secrets were found through the HUD");
                yield return new WaitForSeconds(4.3f);
                Assert.That(FestivalSimulation.CampSeated(game.State,player.Id),Is.False,"setup: the headliner's four seconds are over");

                // Beside the DJ table: aimed at the deck, E is the DJ's; aimed at the button, the button's. An E offered on the button
                // and pressed once the aim is back on the deck does nothing.
                var button=Egg("button");var deck=new Vector3(CampFeatures.DjX,.9f,CampFeatures.DjZ);var besideDj=new Vector3(CampFeatures.DjX+1,0,CampFeatures.DjZ+1.4f);
                yield return StandAndLook(besideDj,deck);
                Assert.That(Prompt(),Does.StartWith("DJ: "),"aimed at the deck, the DJ keeps E");
                yield return StandAndLook(besideDj,FestivalCampEggView.AimPoint(button));
                Assert.That(Prompt(),Is.EqualTo(FestivalCampEggView.Prompt("button").Substring(3)));
                var offered=(Action)primary.GetValue(hud);int track=game.State.CampMusicTrack;
                var look=deck-new Vector3(besideDj.x,1.65f,besideDj.z);yaw.SetValue(session,Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg);pitch.SetValue(session,-Mathf.Asin(look.normalized.y)*Mathf.Rad2Deg);
                yield return null;yield return null;
                offered();yield return new WaitForSeconds(.3f);
                Assert.That(button.ActivationCount,Is.EqualTo(1),"E checks the aim again when pressed");Assert.That(game.State.CampMusicTrack,Is.EqualTo(track));

                // Beside car_1's back window: aimed at the door, E enters the car; aimed at the dust, E writes in it.
                var dust=Egg("wash_me");var car=CampFeatures.Find("car_1");var besideCar=new Vector3(car.X+1.6f,0,car.Z+3.1f);
                yield return StandAndLook(besideCar,new Vector3(car.X+1.7f,1,car.Z));
                Assert.That(Prompt(),Is.EqualTo("ENTER CAR"),"aimed at the door, the car keeps E");
                yield return StandAndLook(besideCar,FestivalCampEggView.AimPoint(dust));
                Assert.That(Prompt(),Is.EqualTo(FestivalCampEggView.Prompt("wash_me").Substring(3)));
                ((Action)primary.GetValue(hud))();
                for(deadline=Time.realtimeSinceStartup+2;dust.ActivationCount<2&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(dust.ActivationCount,Is.EqualTo(2),"E writes in the dust: "+session.Message);Assert.That(player.CampVisitId,Is.Empty,"and does not open the car");

                // A ready player is not offered a secret.
                player.Ready=true;yield return StandAndLook(besideCar,FestivalCampEggView.AimPoint(dust));
                Assert.That(Prompt(),Is.Not.EqualTo(FestivalCampEggView.Prompt("wash_me").Substring(3)),"a ready player keeps E for camp");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world.gameObject);}
            yield return null;
        }

        // The gnome stands where the host's published visit says, in the running session.
        [UnityTest] public IEnumerator TheLiveSessionShowsTheGnomeAtThePublishedSpot()
        {
            var world=new GameObject("impl:U65 gnome world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U65 gnome session");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                yield return null;yield return null;
                var gnome=world.CampEggs.Prop("gnome");var record=Simulation(session).State.CampEggs.Find(e=>e.Id=="gnome");
                Assert.That(gnome.localPosition,Is.EqualTo(new Vector3(record.X,0,record.Z)),"the gnome stands at this visit's spot");
                var other=CampFeatures.GnomeSpots[record.X==CampFeatures.GnomeSpots[0].X?1:0];record.X=other.X;record.Z=other.Z;
                yield return null;yield return null;
                Assert.That(gnome.localPosition,Is.EqualTo(new Vector3(other.X,0,other.Z)),"a newly published spot moves the gnome");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world.gameObject);}
            yield return null;
        }

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
