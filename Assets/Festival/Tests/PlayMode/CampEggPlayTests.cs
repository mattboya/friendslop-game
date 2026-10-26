using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using TMPro;

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
            // Your own view sits down with your body, the eye just over the seated Head bone; it stands back up when the seat ends.
            yield return new WaitForSeconds(.5f);
            Transform head=null;foreach(var bone in actor.GetComponentsInChildren<Transform>())if(bone.name=="Head")head=bone;
            Assert.That(session.EyePosition.y,Is.LessThan(1.65f-.3f),"seated, the eye drops well under standing height");
            Assert.That(session.EyePosition.y,Is.GreaterThan(head.position.y).And.LessThan(head.position.y+.4f),"seated, the eye is at the seated head, its Head bone at "+head.position.y.ToString("0.00")+" m");
            yield return new WaitForSeconds(3.8f);
            Assert.That(game.State.CampEggs[2].SeatedPlayerId,Is.Empty,"the host's real Update ticks expire the owner");
            Assert.That(session.LocalPlayer.VisualPose,Is.EqualTo("Idle"));Assert.That(actor.Pose,Is.EqualTo("Idle"));
            yield return Until(()=>session.EyePosition.y>1.64f,1);
            Assert.That(session.EyePosition.y,Is.EqualTo(1.65f).Within(.01f),"standing again, the eye is back at standing height");
            session.Leave();Object.Destroy(host);Object.Destroy(world);yield return null;
        }

        static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        static IEnumerator Until(Func<bool> done,float seconds){for(float until=Time.realtimeSinceStartup+seconds;!done()&&Time.realtimeSinceStartup<until;)yield return null;}
        static IEnumerator Hosted(FestivalSession session,ushort port=HostPort)
        {
            session.Host("Tester",port);
            yield return Until(()=>session.LocalPlayer!=null,30);
            Assert.That(session.LocalPlayer,Is.Not.Null,"setup: the host has a local player: "+session.Message);
        }
        // The gag line shown over a secret's prop, as one line of text, or null when none shows.
        static string Said(FestivalWorld world,string id)
        {
            var line=world.CampEggs.Prop(id).Find(FestivalCampEggView.LineName);
            return line!=null&&line.gameObject.activeInHierarchy?line.GetComponent<TextMeshPro>().text.Replace("\n"," "):null;
        }
        static AudioSource Voice(FestivalWorld world,string id){var voice=world.CampEggs.Prop(id).Find(FestivalCampEggView.VoiceName);return voice!=null?voice.GetComponent<AudioSource>():null;}
        static bool Sounding(FestivalWorld world,string id){var voice=Voice(world,id);return voice!=null&&voice.isPlaying;}
        static TextMeshPro Dust(FestivalWorld world)=>Array.Find(world.CampEggs.Prop("wash_me").GetComponentsInChildren<TextMeshPro>(true),t=>t.text.StartsWith("WASH ME",StringComparison.Ordinal));
        static Transform Air(FestivalWorld world)=>world.CampEggs.Prop("flamingo").Find("Flamingo air");
        static void StandAt(FestivalSimulation game,string player,string egg,float east=0)
        {
            var record=game.State.CampEggs.Find(e=>e.Id==egg);var p=game.Player(player);p.X=record.X+east;p.Z=record.Z;
        }
        // The host stands at a secret, waits for their eye to follow as it would after a walk there, and pokes it.
        static IEnumerator Poke(FestivalSession session,string egg)
        {
            var game=Simulation(session);StandAt(game,session.LocalPlayerId,egg);var p=game.Player(session.LocalPlayerId);
            yield return Until(()=>Vector3.Distance(session.EyePosition,new Vector3(p.X,1.65f,p.Z))<.5f,3);
            session.Command("CampEgg",egg);
        }

        // Your own poke's line is your notice. A friend's floats over the prop, a different line from the pool each time, and the button
        // pops confetti from its top with an air horn, spending no confetti cannon. The horn goes through the effects volume, in 3-D to
        // silence at 15 m, and follows its slider.
        [UnityTest] public IEnumerator EverySecretSaysItsLineAndTheButtonPopsConfettiAndAHornWithoutSpendingGear()
        {
            var world=new GameObject("impl:U66 gag world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U66 gags");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return Hosted(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);var pal=game.AddPlayer("pal","Pal");
                var confetti=host.GetComponent<FestivalConfettiBurst>();var view=world.CampEggs;
                session.Profile.Data.EffectsVolume=.5f;yield return Frames(2);
                Assert.That((view.Played,confetti.Played),Is.EqualTo((0,0)),"hosting plays no gag");
                yield return Poke(session,"gnome");
                Assert.That(session.Message,Is.EqualTo("The gnome whispers: the real festival was the snacks we hid along the way."),"your own poke's line is your notice");
                yield return Frames(2);
                Assert.That(Said(world,"gnome"),Is.Null,"and does not float over the prop for you");
                IEnumerator PalPokes(string id)
                {
                    var egg=game.State.CampEggs.Find(e=>e.Id==id);int before=egg.ActivationCount;StandAt(game,player.Id,id,2);
                    yield return Until(()=>Vector3.Distance(session.EyePosition,new Vector3(player.X,1.65f,player.Z))<.5f,3);
                    pal.X=egg.X;pal.Z=egg.Z;
                    Assert.That(game.Execute(pal.Id,new GameCommand{Id="pal_"+id+before,Kind="CampEgg",TargetId=id}).Accepted,Is.True,"setup: the host accepts the friend's "+id);
                    yield return Frames(2);
                }
                foreach(var id in new[]{"gnome","lost_found","hammock","cone","button","chair"})
                {
                    yield return PalPokes(id);var egg=game.State.CampEggs.Find(e=>e.Id==id);
                    Assert.That(Said(world,id),Is.EqualTo(egg.LastLine),"a friend's "+id+" says its line over its prop");
                    if(id=="gnome")Assert.That(egg.LastLine,Is.EqualTo("The gnome refuses to reveal its snack supplier."),"setup: the next line in the gnome's pool");
                    if(id!="button"){Assert.That(Sounding(world,id),Is.False,"the "+id+" has no sound of its own");continue;}
                    Assert.That(confetti.Played,Is.EqualTo(1),"the button pops confetti");
                    var paper=host.transform.Find(FestivalConfettiBurst.PaperName).GetComponent<ParticleSystem>();
                    var pieces=new ParticleSystem.Particle[paper.particleCount];paper.GetParticles(pieces);
                    var top=FestivalCampEggView.AimPoint(egg);float nearest=float.MaxValue;
                    foreach(var piece in pieces)nearest=Mathf.Min(nearest,Vector3.Distance(piece.position,top));
                    Assert.That(nearest,Is.LessThan(.6f),"from the button's top");
                }
                var horn=Voice(world,"button");
                Assert.That(horn!=null&&horn.isPlaying,"the button blares its air horn");
                Assert.That(horn.clip.length,Is.InRange(.8f,1.6f),"one blare");
                Assert.That((horn.spatialBlend,horn.dopplerLevel,horn.rolloffMode,horn.maxDistance),Is.EqualTo((1f,0f,AudioRolloffMode.Linear,FestivalCampEggView.Range)),"in 3-D, with no Doppler, to silence at 15 m");
                Assert.That(horn.volume,Is.EqualTo(.5f).Within(1e-5f),"at the effects volume");
                Assert.That(view.Played,Is.EqualTo(1),"one gag sound so far");
                Assert.That(pal.Inventory.Exists(i=>i.ItemId==FestivalSimulation.ConfettiItem),Is.False,"setup: the friend holds no cannon");
                Assert.That(pal.VisualConfettiBurstSequence,Is.Zero,"the button fires no cannon");
                session.Profile.Data.EffectsVolume=.2f;yield return null;
                Assert.That(horn.isPlaying,"setup: still blaring");Assert.That(horn.volume,Is.EqualTo(.2f).Within(1e-5f),"the slider moves the horn as it plays");
                yield return new WaitForSeconds(5.3f);
                Assert.That(Said(world,"lost_found"),Is.Null,"a line goes after a few seconds");
                yield return PalPokes("button");
                Assert.That(Said(world,"button"),Is.EqualTo("You were warned. Twice now."),"pressed again, the button says its next line");
                Assert.That((view.Played,confetti.Played),Is.EqualTo((2,2)),"and pops and blares again");
                session.Leave();yield return Frames(2);
                Assert.That(Sounding(world,"button")||Said(world,"button")!=null,Is.False,"leaving stops the horn and takes the line down");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world.gameObject);}
            yield return null;
        }

        // Development diagnostics name the view's session, each persistent change and each gag played, with no collection or profile
        // data in any of them.
        [UnityTest] public IEnumerator CampSecretDiagnosticsNameTheSessionStateAndGagsButNoCollection()
        {
            var lines=new List<string>();Application.LogCallback log=(message,stack,kind)=>{if(message.StartsWith("[Festival.CampEggs]",StringComparison.Ordinal))lines.Add(message);};
            Application.logMessageReceived+=log;
            LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=attach session=on$"));
            var world=new GameObject("impl:U66 diagnostics world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U66 diagnostics");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                // The view names each new round once, when it arms its first snapshot silently.
                LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=round round=[0-9a-f]{32} baseline=silent$"));
                yield return Hosted(session);
                yield return Frames(2);
                string round=Simulation(session).State.RoundId;
                Assert.That(lines.FindAll(l=>l.Contains(" action=round ")),Is.EqualTo(new[]{"[Festival.CampEggs] action=round round="+round+" baseline=silent"}),"one round line, for the hosted round");
                LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=persistent egg=flamingo deflated=True$"));
                LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=effect egg=flamingo line=own sound=squeak confetti=no$"));
                yield return Poke(session,"flamingo");yield return Frames(2);
                LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=persistent egg=wash_me word=(BASS|HELP|NARC\?|MOM SAYS HI)$"));
                yield return Poke(session,"wash_me");yield return Frames(2);
                LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=effect egg=button line=own sound=horn confetti=yes$"));
                yield return Poke(session,"button");yield return Frames(2);
                LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=attach session=off$"));
                session.Leave();Object.Destroy(host);yield return Frames(2);
                Assert.That(session.Profile.Data.FoundCampEggIds.Count,Is.EqualTo(3),"setup: three discoveries saved");
                Assert.That(lines.FindAll(l=>l.Contains(" action=round ")).Count,Is.EqualTo(1),"frames and pokes later, still one round line");
                foreach(string line in lines)Assert.That(line,Does.Not.Contain("EASTER").And.Not.Contain("Found").And.Not.Contain("mask").And.Not.Contain("Tester"),"no collection, profile or name in "+line);
            }
            finally{Application.logMessageReceived-=log;if(host!=null){session.Leave();Object.Destroy(host);}Object.Destroy(world.gameObject);}
            yield return null;
        }

        // A poked flamingo sags flat over a long sad squeak, or lies flat at once with reduced motion; the dust takes the host's word.
        // Leaving puts both back, as a new camp does.
        [UnityTest] public IEnumerator TheFlamingoSagsWithASqueakOrLiesFlatAtOnceAndTheDustKeepsItsWordForTheVisit()
        {
            var world=new GameObject("impl:U66 flamingo world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U66 flamingo");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return Hosted(session);
                var game=Simulation(session);var air=Air(world);
                session.Profile.Data.ReducedMotion=false;yield return Frames(2);
                Assert.That(air.localScale.y,Is.EqualTo(1),"setup: the flamingo stands full of air");
                yield return Poke(session,"flamingo");yield return Frames(2);
                Assert.That(Sounding(world,"flamingo"),"the flamingo squeaks");
                Assert.That(Voice(world,"flamingo").clip.length,Is.InRange(1.5f,3f),"a long squeak");
                Assert.That(session.Message,Is.EqualTo("The flamingo lets out a long, sad squeak."),"the one who squeezes it reads its line");
                yield return new WaitForSeconds(.5f);
                Assert.That(air.localScale.y,Is.InRange(.2f,.95f),"half a second on it is sagging, not flat at once");
                yield return new WaitForSeconds(1.5f);
                Assert.That(air.localScale.y,Is.LessThan(.2f),"then lies flat");
                yield return Poke(session,"wash_me");yield return Frames(2);
                string word=game.State.CampEggs.Find(e=>e.Id=="wash_me").Word;
                Assert.That(Dust(world).text,Is.EqualTo("WASH ME\n"+word),"the dust takes the host's word");
                Assert.That(session.Message,Is.EqualTo("You write "+word+" in the dust."));
                session.Leave();yield return Frames(2);
                Assert.That(air.localScale.y,Is.EqualTo(1),"leaving puts the camp's flamingo back");Assert.That(Dust(world).text,Is.EqualTo("WASH ME"),"and cleans its dust");

                session.Profile.Data.ReducedMotion=true;yield return Hosted(session);yield return Frames(2);
                Assert.That(air.localScale.y,Is.EqualTo(1),"setup: the next camp's flamingo is full of air");
                yield return Poke(session,"flamingo");yield return Frames(2);
                Assert.That(air.localScale.y,Is.LessThan(.2f),"with reduced motion it lies flat at once");
                Assert.That(Sounding(world,"flamingo"),"and still squeaks");
                session.Leave();yield return Frames(2);
                Assert.That(Sounding(world,"flamingo"),Is.False,"leaving stops the squeak mid-way");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world.gameObject);}
            yield return null;
        }

        // Two real clients. A friend who joins after the flamingo went flat and the dust took a word sees both at once and hears and
        // reads nothing of them. Then the host's button pops and blares for the friend nearby, who earns no discovery, and only pops for
        // the friend past 15 m. The friend's own poke saves only to the friend. After leaving and joining again it is the same.
        [UnityTest] public IEnumerator AFriendNearbySharesTheGagWithoutCreditAndALateJoinerSeesFlatAndDustSilently()
        {
            // The host has no world, so the one world shows only what the friend's session presents.
            var hostObject=new GameObject("impl:U66 late join host");var host=hostObject.AddComponent<FestivalSession>();yield return null;
            GameObject world=null,friendObject=null;FestivalSession friend=null;
            try
            {
                // In one process both sessions share one profile file, so each loads it before anything is saved.
                world=new GameObject("impl:U66 late join world");var view=world.AddComponent<FestivalWorld>();yield return null;
                friendObject=new GameObject("impl:U66 late join friend");friend=friendObject.AddComponent<FestivalSession>();yield return null;
                var friendConfetti=friendObject.GetComponent<FestivalConfettiBurst>();
                ushort port=FreePort();yield return Hosted(host,port);
                var game=Simulation(host);var me=game.Player(host.LocalPlayerId);
                StandAt(game,me.Id,"flamingo");host.Command("CampEgg","flamingo");StandAt(game,me.Id,"wash_me");host.Command("CampEgg","wash_me");
                string word=game.State.CampEggs.Find(e=>e.Id=="wash_me").Word;Assert.That(word,Is.Not.Empty,"setup: the host writes in the dust: "+host.Message);
                yield return Frames(3);
                Assert.That(Air(view).localScale.y,Is.EqualTo(1),"setup: the world shows nothing of a game the friend has not joined");
                friend.Join("Friend","127.0.0.1",port);
                yield return Until(()=>friend.LocalPlayer!=null&&friend.CampEggStates.Count==8,30);
                Assert.That(friend.LocalPlayer,Is.Not.Null,"setup: the friend joins: "+friend.Message);
                var pal=game.Player(friend.LocalPlayerId);StandAt(game,pal.Id,"button",3);yield return Frames(3);
                Assert.That(Air(view).localScale.y,Is.LessThan(.2f),"the late joiner's flamingo is already flat");
                Assert.That(Dust(view).text,Is.EqualTo("WASH ME\n"+word),"and the dust already carries the host's word");
                Assert.That((view.CampEggs.Played,friendConfetti.Played),Is.EqualTo((0,0)),"with no gag sound or paper from before they joined");
                Assert.That(Said(view,"flamingo")??Said(view,"wash_me"),Is.Null,"and no line from before");

                yield return Until(()=>Vector3.Distance(friend.EyePosition,new Vector3(pal.X,1.65f,pal.Z))<.2f,3);
                StandAt(game,me.Id,"button");host.Command("CampEgg","button");
                yield return Until(()=>view.CampEggs.Played>0,3);yield return null;
                Assert.That(view.CampEggs.Played,Is.EqualTo(1),"the friend 3 m away hears the horn once");Assert.That(Sounding(view,"button"),"setup: blaring");
                Assert.That(friendConfetti.Played,Is.EqualTo(1),"and sees the confetti");
                Assert.That(Said(view,"button"),Is.EqualTo("DO NOT PRESS. You press it. Confetti and an air horn happen."),"and reads the host's line");
                Assert.That(friend.Profile.Data.FoundCampEggIds,Is.Empty,"watching earns the friend nothing");
                Assert.That(host.Profile.Data.FoundCampEggIds,Is.EquivalentTo(new[]{"flamingo","wash_me","button"}),"the host keeps their own discoveries");

                pal.X=-20;pal.Z=18;yield return Until(()=>Vector3.Distance(friend.EyePosition,new Vector3(pal.X,1.65f,pal.Z))<.2f,3);
                Assert.That(Vector3.Distance(friend.EyePosition,FestivalCampEggView.AimPoint(game.State.CampEggs.Find(e=>e.Id=="button"))),Is.GreaterThan(FestivalCampEggView.Range),"setup: the friend is past 15 m");
                host.Command("CampEgg","button");
                yield return Until(()=>friendConfetti.Played>1,3);yield return Frames(2);
                Assert.That(friendConfetti.Played,Is.EqualTo(2),"the far friend still sees the paper fly");
                Assert.That(view.CampEggs.Played,Is.EqualTo(1),"but hears no horn");
                Assert.That(Said(view,"button"),Is.Not.EqualTo("You were warned. Twice now."),"and reads no line");

                StandAt(game,pal.Id,"cone");friend.Command("CampEgg","cone");
                yield return Until(()=>friend.Profile.Data.FoundCampEggIds.Count>0,3);
                Assert.That(friend.Profile.Data.FoundCampEggIds,Is.EqualTo(new[]{"cone"}),"the friend's own poke is the friend's discovery");
                Assert.That(host.Profile.Data.FoundCampEggIds,Is.EquivalentTo(new[]{"flamingo","wash_me","button"}),"and not the host's");

                friend.Leave();yield return Frames(2);
                Assert.That(Air(view).localScale.y,Is.EqualTo(1),"setup: leaving puts the camp back");
                friend.Join("Friend","127.0.0.1",port);
                yield return Until(()=>friend.LocalPlayer!=null&&friend.CampEggStates.Count==8,30);yield return Frames(3);
                Assert.That(Air(view).localScale.y,Is.LessThan(.2f),"joining again shows the flat flamingo at once");
                Assert.That(Dust(view).text,Is.EqualTo("WASH ME\n"+word),"and the word");
                Assert.That(view.CampEggs.Played,Is.EqualTo(1),"silently");
            }
            finally
            {
                if(friend!=null)friend.Leave();host.Leave();
                foreach(var go in new[]{friendObject,hostObject,world})if(go!=null)Object.Destroy(go);
            }
            yield return null;
        }

        // Every body shape the game draws sits on the headliner's low camp chair: the seat of its trousers on the canvas, feet on the
        // grass, nothing through the seat. Each wears the backpack, the deepest accessory: nothing shows through the chair's back or the
        // board above it, and the backpack is back on when they stand. Measured on the real chair prop and the real skinned bodies.
        [UnityTest] public IEnumerator EveryBodyShapeSitsOnTheHeadlinersChairWithItsFeetOnTheGrass()
        {
            var world=new GameObject("impl:U66 chair world").AddComponent<FestivalWorld>();yield return null;
            var bodies=new GameObject("impl:U66 seated bodies");var seated=new List<FestivalCharacter>();var shapes=new HashSet<string>();
            try
            {
                var chair=world.CampEggs.Prop("chair");var seat=chair.Find("Chair seat").GetComponent<Renderer>().bounds;
                Assert.That(seat.size.y,Is.LessThan(.06f),"setup: the seat is a thin canvas");
                for(int i=0;i<2000&&shapes.Count<6;i++)
                {
                    var look=FestivalAppearance.For("player_"+i);if(look.Accessory!=0||!shapes.Add(look.Gender+" "+look.Shape))continue;
                    var body=FestivalCharacter.Create(bodies.transform,"player_"+i,Color.white);
                    body.transform.position=new Vector3(seated.Count*3,0,40);body.transform.rotation=Quaternion.Euler(0,180,0);
                    body.transform.localScale=Vector3.Scale(Vector3.one*.82f,body.ShapeScale);body.Pose=FestivalSimulation.CampChairPose;seated.Add(body);
                }
                Assert.That(seated.Count,Is.EqualTo(6),"setup: every body shape, in a backpack");
                yield return new WaitForSeconds(1.5f);
                var mesh=new Mesh();var failures=new List<string>();
                // The seat's footprint about the chair's point, the way the host turns its headliner (yaw 180, facing -z).
                var footprint=new Rect(seat.min.x-chair.position.x,seat.min.z-chair.position.z,seat.size.x,seat.size.z);
                // The back and the RESERVED board over it, about the chair's point: a body reaches through them past their front face.
                var backs=new List<Bounds>();
                foreach(var name in new[]{"Chair back","Reserved board"}){var b=chair.Find(name).GetComponent<Renderer>().bounds;b.center-=chair.position;backs.Add(b);}
                foreach(var body in seated)
                {
                    var at=body.transform.position;float lowest=float.MaxValue,onSeat=float.MaxValue,through=float.MinValue;string reaching="";
                    foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if(!skin.enabled)continue;skin.BakeMesh(mesh,true);var toWorld=skin.transform.localToWorldMatrix;
                        foreach(var vertex in mesh.vertices)
                        {
                            var p=toWorld.MultiplyPoint3x4(vertex)-at;lowest=Mathf.Min(lowest,p.y);
                            // Under the hips, behind the knees: the seat of the trousers.
                            if(Mathf.Abs(p.x)<.18f&&p.z>-.05f&&footprint.Contains(new Vector2(p.x,p.z)))onSeat=Mathf.Min(onSeat,p.y);
                            foreach(var back in backs)
                                if(Mathf.Abs(p.x-back.center.x)<back.extents.x&&Mathf.Abs(p.y-back.center.y)<back.extents.y&&p.z-back.min.z>through){through=p.z-back.min.z;reaching=skin.name;}
                        }
                    }
                    string who=body.name+" ("+body.Appearance.Gender+" "+body.Appearance.Shape+")";
                    if(Mathf.Abs(onSeat-(seat.max.y-chair.position.y))>.03f)failures.Add(who+" sits at "+onSeat.ToString("0.000")+" m, the seat is at "+seat.max.y.ToString("0.000")+" m");
                    if(lowest<-.02f||lowest>.03f)failures.Add(who+"'s feet are at "+lowest.ToString("0.000")+" m, not on the grass");
                    if(through>0)failures.Add(who+"'s "+reaching+" reaches "+through.ToString("0.000")+" m into the chair's back");
                }
                Object.Destroy(mesh);
                Assert.That(failures,Is.Empty,"Seated on the headliner's chair:\n"+string.Join("\n",failures));
                foreach(var body in seated)body.Pose="Idle";
                yield return null;
                foreach(var body in seated)
                    Assert.That(Array.Find(body.GetComponentsInChildren<SkinnedMeshRenderer>(true),skin=>skin.name=="Accessory_0").enabled,Is.True,body.name+" stands up in their backpack");
            }
            finally{Object.Destroy(bodies);Object.Destroy(world.gameObject);}
            yield return null;
        }

        // In a hosted camp the headliner faces the chair's way (south) for the whole seat, whichever way they look, and
        // stands up off the chair's front when the seat ends. You walk up looking north at the chair and sit, still looking north.
        // Then a friend in a backpack, the deepest accessory, sits until their seat ends. Each body is measured on the real chair
        // prop where the session draws it: nothing reaches through the chair's back or the RESERVED board over it.
        [UnityTest] public IEnumerator AHeadlinerFacesTheChairsWayWhileSeatedAndStandsUpClearOfIt()
        {
            var world=new GameObject("CAMP-5 chair world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("CAMP-5 hosted chair");var session=host.AddComponent<FestivalSession>();yield return null;
            var mesh=new Mesh();
            try
            {
                yield return Hosted(session);
                var game=Simulation(session);var record=game.State.CampEggs.Find(e=>e.Id=="chair");var chair=world.CampEggs.Prop("chair");
                var actors=(Dictionary<string,Transform>)typeof(FestivalSession).GetField("actors",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var you=game.Player(session.LocalPlayerId);you.X=record.X;you.Z=record.Z-1.5f;
                foreach(var field in new[]{"yaw","viewYaw"})typeof(FestivalSession).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,0f);
                yield return new WaitForSeconds(.3f);
                session.Command("CampEgg","chair");
                yield return new WaitForSeconds(.6f);
                Assert.That(FestivalSimulation.CampSeated(game.State,you.Id),Is.True,"setup: you sit in the chair");
                var yours=actors[you.Id].GetComponent<FestivalCharacter>();
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(yours.transform.eulerAngles.y,180)),Is.LessThan(1f),"seated while you look north, your body faces the chair's way, south (host yaw "+you.Yaw+")");
                Assert.That(ThroughTheChair(yours,chair,mesh),Is.Empty,"seated, your body stays out of the chair's back and board");
                yield return Until(()=>!FestivalSimulation.CampSeated(game.State,you.Id),4.5f);
                yield return new WaitForSeconds(1f);
                Assert.That(yours.Pose,Is.EqualTo("Idle"),"setup: your seat has ended");
                Assert.That(ThroughTheChair(yours,chair,mesh),Is.Empty,"standing up, your body stays out of the chair's back and board, drawn at "+yours.transform.position.ToString("F2"));
                string friend=null;for(int i=2;friend==null;i++)if(FestivalAppearance.For("player_"+i).Accessory==0&&"player_"+i!=you.Id)friend="player_"+i;
                var pal=game.AddPlayer(friend,friend);pal.X=record.X;pal.Z=record.Z-1;you.X=record.X+3;
                yield return new WaitForSeconds(.3f);
                Assert.That(game.Execute(friend,new GameCommand{Id="sit",Kind="CampEgg",TargetId="chair"}).Accepted,Is.True,"setup: "+friend+" sits");
                yield return Until(()=>!FestivalSimulation.CampSeated(game.State,friend),4.5f);
                yield return new WaitForSeconds(1f);
                var theirs=actors[friend].GetComponent<FestivalCharacter>();
                Assert.That(theirs.Pose,Is.EqualTo("Idle"),"setup: "+friend+"'s seat has ended");
                Assert.That(Array.Find(theirs.GetComponentsInChildren<SkinnedMeshRenderer>(true),skin=>skin.name=="Accessory_0").enabled,Is.True,"setup: "+friend+" stands in their backpack");
                Assert.That(ThroughTheChair(theirs,chair,mesh),Is.Empty,"standing up, "+friend+"'s body and backpack stay out of the chair's back and board, drawn at "+theirs.transform.position.ToString("F2"));
            }
            finally{Object.Destroy(mesh);session.Leave();Object.Destroy(host);Object.Destroy(world.gameObject);}
            yield return null;
        }
        // Each skinned part of body that reaches past the front face of the chair's back or the RESERVED board, and how far.
        static List<string> ThroughTheChair(FestivalCharacter body,Transform chair,Mesh mesh)
        {
            var backs=new List<Bounds>();foreach(var name in new[]{"Chair back","Reserved board"})backs.Add(chair.Find(name).GetComponent<Renderer>().bounds);
            var deepest=new Dictionary<string,float>();
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!skin.enabled)continue;skin.BakeMesh(mesh,true);var toWorld=skin.transform.localToWorldMatrix;
                foreach(var vertex in mesh.vertices)
                {
                    var p=toWorld.MultiplyPoint3x4(vertex);
                    foreach(var back in backs)
                        if(Mathf.Abs(p.x-back.center.x)<back.extents.x&&Mathf.Abs(p.y-back.center.y)<back.extents.y&&p.z>back.min.z&&p.z<back.max.z+.5f)
                            deepest[skin.name]=Mathf.Max(deepest.TryGetValue(skin.name,out var was)?was:0,p.z-back.min.z);
                }
            }
            var parts=new List<string>();foreach(var pair in deepest)parts.Add(pair.Key+" "+pair.Value.ToString("0.000")+" m");
            return parts;
        }

        // The pass counts the secrets this machine has found, and says so when there are none left to find.
        [UnityTest] public IEnumerator ThePassCountsYourCampSecretsAndSaysWhenYouHaveFoundAllEight()
        {
            var world=new GameObject("impl:U66 pass world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U66 pass");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();yield return null;
            try
            {
                yield return Hosted(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
                string Count(){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name=="Easter eggs")return text.text;return null;}
                session.MenuOpen=true;yield return Frames(2);
                Assert.That(Count(),Is.EqualTo("EASTER EGGS 0 / 8"),"a new machine has found none");
                foreach(var id in new[]{"gnome","cone","flamingo"}){StandAt(game,player.Id,id);session.Command("CampEgg",id);}
                yield return Frames(3);
                Assert.That(Count(),Is.EqualTo("EASTER EGGS 3 / 8"),"the pass counts what you poked");
                session.Profile.Data.FoundCampEggIds=new List<string>{"gnome","button","chair","lost_found","hammock","flamingo","cone","wash_me"};yield return Frames(2);
                Assert.That(Count(),Is.EqualTo("EASTER EGGS 8 / 8\nYou found every dumb secret. Seek help."),"all eight earn the line");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world.gameObject);}
            yield return null;
        }
    }
}
