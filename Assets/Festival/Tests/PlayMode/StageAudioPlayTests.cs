using System;
using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class StageAudioPlayTests:FestivalPlayModeTest
    {
        static IEnumerator Wait(double seconds){double until=Time.realtimeSinceStartupAsDouble+seconds;while(Time.realtimeSinceStartupAsDouble<until)yield return null;}
        static void MatchesWorld(FestivalSession session,FestivalStageAudio stage,string reason)
        {
            var state=session.State;var track=FestivalStageAudio.For(state);
            double world=FestivalSimulation.WorldSeconds(state)+(state.Phase=="Playing"?session.EstimatedSimulationSeconds-state.SimulationSeconds:0);
            double expected=RhythmChart.StageSampleAtWorld(track.Beats,world),miss=Math.Abs(stage.Music.timeSamples-expected);
            miss=Math.Min(miss,Math.Abs(track.LoopEnd-track.FirstBeat-miss));
            Assert.That(miss/FestivalMusic.Rate,Is.LessThan(.075),reason);
        }
        IEnumerator Open(FestivalSession session)
        {
            session.Host("Tester",HostPort);double deadline=Time.realtimeSinceStartupAsDouble+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
            session.Profile.Data.MusicVolume=1;yield return StartLevel(session);yield return Wait(.45);
        }
        static void Close(FestivalSession session,GameObject host,GameObject world)
        {
            session.Leave();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(world);
        }
        // The inputs a perfect rider sends: into each block's lane as it arrives, out of each grey's lane just before it.
        static void RidePerfectly(FestivalSimulation sim,PlayerState player,InteractionState set)
        {
            var chart=RhythmChart.For(set);int lane=DjRide.StartLane;
            for(int n=0;n<chart.Notes.Count;n++)
            {
                var note=chart.Notes[n];int to=lane;double at=note.TimeSeconds;
                if(!note.Grey)to=note.Direction;
                else if(lane==note.Direction){to=chart.Notes.Find(x=>x.Id>note.Id&&!x.Grey).Direction;at-=.05;}
                if(to==lane)continue;lane=to;
                sim.State.SimulationSeconds=Math.Max(sim.State.SimulationSeconds,set.StartSeconds+at);
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="ride_"+set.Phrase+"_"+n,Kind="Rhythm",Direction=lane,TimeSeconds=at}).Accepted,Is.True,"setup: a lane change is taken");
            }
        }
        [UnityTest]public IEnumerator ARealDanceAndAWholeDjSetLeaveTheSongFullAndThePerfectPhraseRoars()
        {
            var world=new GameObject("Stage dance world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Stage dance session");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return Open(session);var stage=host.GetComponent<FestivalStageAudio>();var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                float full=stage.Music.volume;Assert.That(full,Is.GreaterThan(0));
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook");player.X=npc.X+.8f;player.Z=npc.Z;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="song_dance",Kind="Dance",TargetId=npc.Id}).Accepted,Is.True);
                yield return Wait(.5);
                Assert.That(stage.Music.volume,Is.EqualTo(full).Within(1e-4f),"the stage song leads the dance at full volume");
                var pulse=host.GetComponent<FestivalRhythmAudio>();var sources=pulse.GetComponents<AudioSource>();
                Assert.That(Array.Exists(sources,s=>s.clip!=null&&s.clip.name=="Authored festival tick"),Is.True,"the real rhythm consumer schedules the quiet authored tick");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="song_cancel",Kind="Cancel"}).Accepted,Is.True);
                player.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});player.X=Catalog.StageTakeoverX;player.Z=Catalog.StageTakeoverZ;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="song_dj",Kind="Dj"}).Accepted,Is.True);
                var set=sim.Interaction(player.InteractionId);
                // The whole set: five phrases, the song audible at the stage front at full volume through each, with no pulse over it.
                for(int phrase=0;phrase<5;phrase++)
                {
                    yield return Wait(1.3);
                    Assert.That(stage.Music!=null&&stage.Music.isPlaying,Is.True,"phrase "+phrase+": the stage song plays on");
                    Assert.That(stage.Music.volume,Is.EqualTo(full).Within(1e-4f),"phrase "+phrase+": at full volume for the DJ");
                    Assert.That(Array.Exists(sources,s=>s.isPlaying),Is.False,"phrase "+phrase+": no synthetic pulse or tick plays over the set");
                    MatchesWorld(session,stage,"phrase "+phrase+": the song keeps world time through the set");
                    if(phrase==0)RidePerfectly(sim,player,set);
                    sim.State.SimulationSeconds=Math.Max(sim.State.SimulationSeconds,set.StartSeconds+set.DurationSeconds+.75);
                    if(phrase==0)
                    {
                        yield return Wait(.6);
                        Assert.That(sim.State.StageCheerScore,Is.EqualTo(1),"setup: the first phrase was perfect");
                        var roar=(AudioSource)typeof(FestivalStageAudio).GetField("roar",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(stage);
                        Assert.That(roar!=null&&roar.isPlaying&&roar.volume>0,Is.True,"the crowd roars after a perfect phrase");
                    }
                }
                yield return Wait(1.3);
                Assert.That(set.Status,Is.EqualTo("Complete"),"the set ends after five phrases");
                Assert.That(stage.Music.volume,Is.EqualTo(full).Within(1e-4f));MatchesWorld(session,stage,"the set's end does not restart the song");
            }
            finally{Close(session,host,world);}yield return null;
        }
        [UnityTest]public IEnumerator LateWorldTimeSameTrackEpochAndMaterialDriftUseTheScheduledTransport()
        {
            var world=new GameObject("Stage clock world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Stage clock session");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return Open(session);var stage=host.GetComponent<FestivalStageAudio>();var sim=Simulation(session);
                sim.State.SimulationSeconds=120.125;yield return Wait(1.3);
                MatchesWorld(session,stage,"a late snapshot projects its shared world position instead of local sample zero");
                var first=stage.Music;var track=FestivalStageAudio.For(session.State);
                first.timeSamples+=FestivalMusic.Rate/2;yield return Wait(1.3);
                MatchesWorld(session,stage,"a material half-second stream error is corrected at a bounded check");
                var steady=stage.Music;yield return Wait(.25);Assert.That(stage.Music,Is.SameAs(steady),"steady playback does not seek or swap decks every frame");
                sim.State.RoundId="same-track-new-round";sim.State.SimulationSeconds=3.25;yield return Wait(.65);
                MatchesWorld(session,stage,"a new round resets even when it reuses the same recording");
                // Use the actual production scheduler near a click-free join without waiting an entire song.
                stage.enabled=false;var clock=new RoundState{Phase="Playing",RoundId="join-proof",LevelIndex=0,SimulationSeconds=track.LoopSeconds-.7};
                double start=AudioSettings.dspTime;stage.Follow(clock,clock.SimulationSeconds,start);yield return Wait(.35);var before=stage.Music;
                yield return Wait(.5);stage.Follow(clock,clock.SimulationSeconds+AudioSettings.dspTime-start,AudioSettings.dspTime);
                Assert.That(stage.Music,Is.Not.SameAs(before),"the already scheduled other deck takes over at the authored sample join");
                double expected=RhythmChart.StageSampleAtWorld(track.Beats,clock.SimulationSeconds+AudioSettings.dspTime-start);
                Assert.That(stage.Music.timeSamples,Is.EqualTo(expected).Within(FestivalMusic.Rate*.075),"the loop uses its exact sample join offsets");
            }
            finally{Close(session,host,world);}yield return null;
        }
        [UnityTest]public IEnumerator TheBoothAndResultsHoldTheSongAndResumeFromTheHeldPosition()
        {
            var world=new GameObject("Stage pause world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Stage pause session");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return Open(session);var sim=Simulation(session);var stage=host.GetComponent<FestivalStageAudio>();var player=sim.Player(session.LocalPlayerId);
                sim.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;var at=FestivalSimulation.BoothAt(sim.State);player.X=at.X+1;player.Z=at.Z;
                double deadline=Time.realtimeSinceStartupAsDouble+6;
                while(session.State.Phase=="Playing"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));yield return Wait(.2);
                Assert.That(stage.Music.isPlaying,Is.False,"the world freezes, so the stage holds its sample instead of playing past the chart");
                MatchesWorld(session,stage,"the held stage sample is the exact paused world position");int held=stage.Music.timeSamples;
                yield return Wait(.5);Assert.That(stage.Music.timeSamples,Is.EqualTo(held),"game time never advances stage playback");
                session.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups);yield return Wait(.2);
                Assert.That(session.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0));Assert.That(stage.Music.timeSamples,Is.EqualTo(held),"the game result banner also holds");
                deadline=Time.realtimeSinceStartupAsDouble+6;while(session.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"));yield return Wait(.5);
                Assert.That(stage.Music.isPlaying,Is.True);MatchesWorld(session,stage,"resume continues from the held world position without sample-zero restart");
                sim.State.ElapsedSeconds=sim.State.DurationSeconds;deadline=Time.realtimeSinceStartupAsDouble+5;
                while(session.State.Phase!="Results"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Results"));yield return Wait(.2);held=stage.Music.timeSamples;
                Assert.That(stage.Music.isPlaying,Is.False);yield return Wait(.3);Assert.That(stage.Music.timeSamples,Is.EqualTo(held),"level results hold too");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="stage_back_to_camp",Kind="Reset"}).Accepted,Is.True);yield return Wait(.3);
                Assert.That(session.State.Phase,Is.EqualTo("CampReview"));Assert.That(stage.Music,Is.Null,"camp owns the music again");
            }
            finally{Close(session,host,world);}yield return null;
        }
    }
}
