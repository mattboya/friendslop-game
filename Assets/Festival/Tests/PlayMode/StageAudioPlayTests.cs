using System.Collections;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    // MUSIC-1 in a hosted session: the main stage is silent at camp, plays Palm Mirage's day track from the stage once the festival
    // opens, ducks to a quarter while you dance and comes back after, plays on through the results and stops back at camp. Its loop
    // joins on the audio clock, as the camp DJ's does.
    public sealed class StageAudioPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TheStagePlaysTheFestivalsTrackDucksForADanceAndStopsAtCamp()
        {
            var world=new GameObject("Stage music world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Stage music session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            // This machine's saved profile may have the music down; only in memory, the saved profile is left alone.
            float volume=session.Profile.Data.MusicVolume;session.Profile.Data.MusicVolume=1;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var stage=host.GetComponent<FestivalStageAudio>();var sim=Simulation(session);string me=session.LocalPlayerId;
                Assert.That(stage,Is.Not.Null,"every session has the stage's sound");
                string Playing()=>stage.Music==null?"nothing":stage.Music.clip==null?"no clip":stage.Music.clip.name+(stage.Music.isPlaying?"":" (stopped)");
                yield return new WaitForSeconds(.3f);
                Assert.That(Playing(),Is.EqualTo("nothing"),"at camp the stage is silent");
                Assert.That(host.GetComponent<FestivalCampAudio>().Music,Is.Not.Null,"setup: the camp DJ plays");

                yield return StartLevel(session);
                // A track starts a moment after it is picked, once its stream is open.
                yield return new WaitForSeconds(.4f);
                Assert.That((session.State.FestivalIndex,session.State.LevelIndex),Is.EqualTo((0,0)),"setup: Palm Mirage, Day 1");
                Assert.That(Playing(),Is.EqualTo("disco-house-loop"),"the stage plays Palm Mirage's day track");
                Assert.That(FestivalMusic.BeatPhase(stage.Music),Is.Not.Null,"a recording with a beat to follow");
                Assert.That(stage.Music.transform.position.z,Is.GreaterThan(25),"from the stage, at the far end of the grounds");
                Assert.That(host.GetComponent<FestivalCampAudio>().Music,Is.Null,"the camp DJ has stopped");
                float full=stage.Music.volume;
                Assert.That(full,Is.GreaterThan(0),"setup: the music is on");

                // A dance with a festivalgoer: the stage ducks to a quarter within 0.4 s. The partner is held in place.
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook");float npcX=npc.X,npcZ=npc.Z;var player=sim.Player(me);player.X=npcX+.8f;player.Z=npcZ;
                Assert.That(sim.Execute(me,new GameCommand{Id="stage_dance",Kind="Dance",TargetId=npc.Id}).Accepted,Is.True,"setup: a dance with a festivalgoer starts");
                for(deadline=Time.realtimeSinceStartup+2;session.LocalPlayer.InteractionId==""&&Time.realtimeSinceStartup<deadline;)yield return null;
                float started=Time.realtimeSinceStartup;
                while(stage.Music.volume>full*.26f&&Time.realtimeSinceStartup<started+2){npc.X=npcX;npc.Z=npcZ;yield return null;}
                Assert.That(stage.Music.volume,Is.EqualTo(full*.25f).Within(full*.01f),"the stage ducks to a quarter while you dance");
                Assert.That(Time.realtimeSinceStartup-started,Is.LessThan(.5f),"within about 0.4 s");
                Assert.That(Playing(),Is.EqualTo("disco-house-loop"),"ducked, not stopped");

                // The dance over, it comes back over about a second.
                Assert.That(sim.Execute(me,new GameCommand{Id="stage_dance_over",Kind="Cancel"}).Accepted,Is.True,"setup: the dance ends");
                yield return new WaitForSeconds(.4f);
                Assert.That(stage.Music.volume,Is.GreaterThan(full*.25f).And.LessThan(full*.9f),"coming back gently, not at once");
                yield return new WaitForSeconds(1);
                Assert.That(stage.Music.volume,Is.EqualTo(full).Within(1e-4f),"back to full");

                // The loop: Palm Mirage's track cued half a second before its loop's end. At the end the other deck takes over, already
                // playing from the first beat.
                var track=FestivalStageAudio.TrackFor(0,0);
                typeof(FestivalStageAudio).GetMethod("Play",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(stage,new object[]{track,track.LoopEnd-FestivalMusic.Rate/2});
                double turnAt=(double)typeof(FestivalStageAudio).GetField("turnAt",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(stage);
                yield return new WaitForSeconds(.3f);
                var cued=stage.Music;
                Assert.That(AudioSettings.dspTime,Is.LessThan(turnAt),"setup: before the loop's end");
                for(deadline=Time.realtimeSinceStartup+5;AudioSettings.dspTime<turnAt+.3&&Time.realtimeSinceStartup<deadline;)yield return null;
                var looped=stage.Music;
                Assert.That(looped,Is.Not.SameAs(cued),"the other deck plays after the loop's end");
                Assert.That(Playing(),Is.EqualTo("disco-house-loop"),"still Palm Mirage's day track");
                Assert.That((looped.timeSamples-track.FirstBeat)/(double)FestivalMusic.Rate,Is.EqualTo(AudioSettings.dspTime-turnAt).Within(.05),"from the first beat, on the audio clock");

                // Sundown: the results show over the festival, and the stage plays on. Back at camp it stops.
                sim.State.ElapsedSeconds=sim.State.DurationSeconds;
                for(deadline=Time.realtimeSinceStartup+5;session.State.Phase!="Results"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Results"),"setup: the day ends: "+session.Message);
                yield return null;
                Assert.That(Playing(),Is.EqualTo("disco-house-loop"),"the stage plays on under the results");
                Assert.That(sim.Execute(me,new GameCommand{Id="stage_back_to_camp",Kind="Reset"}).Accepted,Is.True,"setup: the host brings the crew back");
                for(deadline=Time.realtimeSinceStartup+5;session.State.Phase=="Results"&&Time.realtimeSinceStartup<deadline;)yield return null;
                yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("CampReview"),"setup: back at camp");
                Assert.That(Playing(),Is.EqualTo("nothing"),"the stage stops at camp");
                foreach(var deck in cued.GetComponents<AudioSource>())Assert.That(deck.isPlaying,Is.False,"neither stage deck plays on at camp");
            }
            finally
            {
                session.Profile.Data.MusicVolume=volume;session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation","Main stage speakers"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
