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

namespace Festival.Tests
{
    // MUSIC-2 at camp, in a hosted session: the camp plays TECH HOUSE to start, the DJ table's E steps through the seven recorded
    // tracks and SILENCE, and SILENCE stops the music. The loop joins on the audio clock: two decks take turns, and at the loop's end
    // the other deck is already playing from the first beat.
    public sealed class CampMusicPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TheDjTableStepsThroughTheRecordedTracksAndSilenceStopsThem()
        {
            var world=new GameObject("Camp music world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Camp music session");var session=host.AddComponent<FestivalSession>();var hud=host.AddComponent<FestivalHud>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var camp=host.GetComponent<FestivalCampAudio>();var player=Simulation(session).Player(session.LocalPlayerId);
                Text Find(string name){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
                var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
                string Playing()=>camp.Music==null?"nothing":camp.Music.clip==null?"no clip":camp.Music.clip.name+(camp.Music.isPlaying?"":" (stopped)");
                // A track starts a moment after it is picked, once its stream is open.
                yield return new WaitForSeconds(.3f);
                Assert.That(session.State.CampMusicTrack,Is.EqualTo(1),"setup: a new camp is on track 1");
                Assert.That(Playing(),Is.EqualTo("tech-house-loop"),"the camp plays TECH HOUSE to start");

                // At the DJ table, E steps to the next track each time, round to SILENCE.
                player.X=CampFeatures.DjX;player.Z=CampFeatures.DjZ;
                for(int step=2;step<=CampFeatures.Tracks.Length;step++)
                {
                    int next=step%CampFeatures.Tracks.Length;
                    yield return new WaitForSeconds(.3f);
                    Assert.That(Find("Prompt")?.text,Is.EqualTo("DJ: "+CampFeatures.Tracks[next]),"the DJ table offers the next track");
                    ((Action)primary.GetValue(hud))();
                    for(deadline=Time.realtimeSinceStartup+2;session.State.CampMusicTrack!=next&&Time.realtimeSinceStartup<deadline;)yield return null;
                    yield return new WaitForSeconds(.3f);
                    Assert.That(session.State.CampMusicTrack,Is.EqualTo(next),"E picks "+CampFeatures.Tracks[next]+": "+session.Message);
                    Assert.That(Playing(),Is.EqualTo(next==0?"nothing":FestivalMusic.For(next).Clip),"the camp plays "+CampFeatures.Tracks[next]);
                }
                foreach(var source in host.GetComponents<AudioSource>())
                    if(source.clip!=null&&Array.Exists(FestivalMusic.Tracks,t=>t.Clip==source.clip.name))Assert.That(source.isPlaying,Is.False,"SILENCE stops every deck ("+source.clip.name+")");
                Assert.That(FestivalMusic.BeatPhase(camp.Music),Is.Null,"in SILENCE there is no beat to follow");

                // Each loop: a track cued half a second before its loop's end. At the end the other deck takes over, already playing
                // from the first beat, as far in as the audio clock has run since the turn (within a mixer buffer or two).
                yield return new WaitForSeconds(.3f);
                ((Action)primary.GetValue(hud))();
                for(deadline=Time.realtimeSinceStartup+2;session.State.CampMusicTrack!=1&&Time.realtimeSinceStartup<deadline;)yield return null;
                yield return null;
                foreach(int choice in new[]{1,6,7})
                {
                    player.X=CampFeatures.DjX;player.Z=CampFeatures.DjZ;
                    session.Command("ChooseCampTrack",amount:choice);
                    for(deadline=Time.realtimeSinceStartup+2;session.State.CampMusicTrack!=choice&&Time.realtimeSinceStartup<deadline;)yield return null;
                    Assert.That(session.State.CampMusicTrack,Is.EqualTo(choice),"host accepts the camp choice");
                    yield return new WaitForSeconds(.3f);
                    var track=FestivalMusic.For(choice);
                    Assert.That(Playing(),Is.EqualTo(track.Clip),"the chosen recording plays from its introduction");
                    Assert.That(camp.Music.timeSamples,Is.LessThan(FestivalMusic.Rate),"first pass starts at file sample zero, retaining the intro");
                    typeof(FestivalCampAudio).GetMethod("Play",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(camp,new object[]{choice,track.LoopEnd-FestivalMusic.Rate/2});
                    var turn=typeof(FestivalCampAudio).GetField("turnAt",BindingFlags.NonPublic|BindingFlags.Instance);
                    double turnAt=(double)turn.GetValue(camp);
                    yield return new WaitForSeconds(.3f);
                    var cued=camp.Music;
                    Assert.That(AudioSettings.dspTime,Is.LessThan(turnAt),"setup: before the loop's end");
                    Assert.That(Playing(),Is.EqualTo(track.Clip),"setup: selected track is cued");
                    for(deadline=Time.realtimeSinceStartup+5;AudioSettings.dspTime<turnAt+.3&&Time.realtimeSinceStartup<deadline;)yield return null;
                    Assert.That(AudioSettings.dspTime,Is.GreaterThan(turnAt+.3),"setup: the audio clock runs past the loop's end");
                    var looped=camp.Music;double since=AudioSettings.dspTime-turnAt;
                    Assert.That(looped,Is.Not.SameAs(cued),"the other deck plays after the loop's end");
                    Assert.That(Playing(),Is.EqualTo(track.Clip),"still the selected recording");
                    Assert.That((looped.timeSamples-track.FirstBeat)/(double)FestivalMusic.Rate,Is.EqualTo(since).Within(.05),"from the first beat, on the audio clock");
                    Assert.That(FestivalMusic.BeatPhase(looped),Is.EqualTo(track.BeatPhase(looped.timeSamples)).Within(.02f),"the beat API follows the deck that is playing");
                    Assert.That((double)turn.GetValue(camp),Is.EqualTo(turnAt+track.LoopSeconds).Within(1e-6),"and the next turn is one loop later");
                }
            }
            finally
            {
                if(session!=null)session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)UnityEngine.Object.Destroy(leftover);}
                UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(world);
            }
            yield return null;
        }
    }
}
