using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class RhythmInputContractTests
    {
        private string previousFolder,previousScope,ownedFolder;
        [SetUp]public void OwnInputStorage()
        {
            previousFolder=LocalProfile.Folder;previousScope=FestivalInput.PreferencesScope;
            ownedFolder=Path.Combine(Path.GetTempPath(),"impl-U35-rhythm-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ownedFolder);LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;
        }
        [TearDown]public void RestoreCallerStorage()
        {
            LocalProfile.Folder=previousFolder;FestivalInput.PreferencesScope=previousScope;
            if(Directory.Exists(ownedFolder))Directory.Delete(ownedFolder,true);
        }

        [UnityTearDown]public IEnumerator LeaveOwnedPlayMode()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator CalibratedPressAndReleaseReachTheHostJudgeWithoutMovingChartTime()
        {
            var rhythm=typeof(FestivalSession).GetMethod("Rhythm");Assert.That(rhythm,Is.Not.Null,"the real session accepts a raw timestamp and calibrates both input edges");
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U35-rhythm-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            LocalProfile.Folder=playFolder;FestivalInput.PreferencesScope=playFolder;
            var host=new GameObject("impl-U35 calibrated host");
            try
            {
                var session=host.AddComponent<FestivalSession>();session.enabled=false;
                session.Host("Calibration test",8641);Assert.That(session.Connected&&session.IsHost,Is.True,session.Message);
                foreach(var sample in new[]{(Offset:60f,Raw:2.06),(Offset:-80f,Raw:1.92),(Offset:350f,Raw:2.2)})
                {
                    var game=Floor();var interaction=game.Interaction(game.Player("p0").InteractionId);var before=RhythmChart.For(interaction).Notes[0].TimeSeconds;
                    game.State.SimulationSeconds=interaction.StartSeconds+2.2;
                    typeof(FestivalSession).GetField("simulation",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,game);
                    typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,"p0");typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(game,"p0"));
                    session.Profile.Data.TimingCalibrationMs=sample.Offset;
                    session.Rhythm(1,sample.Raw);session.Rhythm(1,sample.Raw,true);
                    Assert.That(interaction.Inputs.Count,Is.EqualTo(2),session.Message);
                    Assert.That(interaction.Inputs[0].TimeSeconds,Is.EqualTo(2).Within(.00001));Assert.That(interaction.Inputs[1].TimeSeconds,Is.EqualTo(2).Within(.00001));
                    Assert.That(interaction.Inputs[0].Release,Is.False);Assert.That(interaction.Inputs[1].Release,Is.True);
                    Assert.That(RhythmChart.For(interaction).Notes[0].TimeSeconds,Is.EqualTo(before));Assert.That(game.State.SimulationSeconds,Is.EqualTo(interaction.StartSeconds+2.2));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(host);LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;Directory.Delete(playFolder,true);}
            yield return new ExitPlayMode();
        }

        private static FestivalSimulation Floor()
        {
            var game=new FestivalSimulation(7);var player=game.AddPlayer("p0","Tester");game.AddPlayer("p1","Friend");
            game.State.Phase="Playing";game.State.LevelIndex=3;game.State.SimulationSeconds=10;game.State.Npcs.Clear();
            player.X=0;player.Z=0;game.State.Npcs.Add(new NpcState{Id="partner",X=0,Z=1.5f,Yaw=0,CanTalk=true});
            var result=game.Execute(player.Id,new GameCommand{Id=Guid.NewGuid().ToString("N"),Kind="Dance",TargetId="partner"});
            Assert.That(result.Accepted,Is.True,result.Reason);return game;
        }

        [Test]public void AReleaseRoundTripsToOnlyItsOwnerWithoutStepping()
        {
            var game=Floor();var player=game.Player("p0");var interaction=game.Interaction(player.InteractionId);
            game.State.SimulationSeconds=interaction.StartSeconds+2.25;player.VisualDanceStepDirection=3;
            var result=game.Execute(player.Id,new GameCommand{Id=Guid.NewGuid().ToString("N"),Kind="Rhythm",Amount=1,Direction=1,TimeSeconds=2.25});
            Assert.That(result.Accepted,Is.True,result.Reason);
            Assert.That(JsonUtility.ToJson(interaction.Inputs[0]),Does.Contain("\"Release\":true"),"accepted releases are public serializable fields");
            Assert.That((player.VisualDanceStepSequence,player.VisualDanceStepDirection),Is.EqualTo((0,3)));
            var owner=JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,player.Id)));
            Assert.That(JsonUtility.ToJson(owner.Interactions[0].Inputs[0]),Does.Contain("\"Release\":true"),"the real view whitelist and JSON transport retain the flag");
            Assert.That(FestivalSession.ViewFor(game,"p1").Interactions,Is.Empty,"a friend's private input log is never published");
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State)));
            Assert.That(JsonUtility.ToJson(restored.Interaction(interaction.Id).Inputs[0]),Does.Contain("\"Release\":true"),"saved rounds retain the release too");
        }

        [Test]public void OlderInputsStillScoreAsPressesAfterRestore()
        {
            var game=Floor();var interaction=game.Interaction(game.Player("p0").InteractionId);interaction.ChartDifficulty=RhythmChart.QuarterNotes;
            foreach(var note in RhythmChart.For(interaction).Notes)
            {
                var input=JsonUtility.FromJson<RhythmInput>("{\"Direction\":"+note.Direction+",\"TimeSeconds\":"+note.TimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
                Assert.That(JsonUtility.ToJson(input),Does.Contain("\"Release\":false"),"a missing field defaults to a press");
                interaction.Inputs.Add(input);
            }
            Assert.That(JsonUtility.ToJson(new RhythmInput()),Does.Contain("\"Release\":false"),"source-compatible defaults also remain presses");
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State)));
            restored.State.SimulationSeconds=interaction.StartSeconds+interaction.DurationSeconds+.75;restored.Tick(.1);
            Assert.That(restored.Interaction(interaction.Id).Score,Is.EqualTo(1),"legacy presses still call the production judge's head path");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]public void DebugReplayReportsTheRealHeadsAndOneDrop()
        {
            var game=Floor();var interaction=game.Interaction(game.Player("p0").InteractionId);
            RhythmChart chart=null;RhythmNote hold=null;
            for(int seed=0;seed<1000&&hold==null;seed++)
            {
                interaction.ChartSeed=seed;chart=RhythmChart.For(interaction);
                if(chart.Notes.Count>=14)hold=chart.Notes.Find(note=>note.HoldSeconds>0);
            }
            Assert.That(hold,Is.Not.Null);
            var messages=new List<string>();game.RhythmDiagnostic=messages.Add;
            foreach(var note in chart.Notes)
            {
                game.State.SimulationSeconds=Math.Max(game.State.SimulationSeconds,interaction.StartSeconds+note.TimeSeconds);
                var press=game.Execute("p0",new GameCommand{Id=Guid.NewGuid().ToString("N"),Kind="Rhythm",Direction=note.Direction,TimeSeconds=note.TimeSeconds});
                Assert.That(press.Accepted,Is.True,press.Reason);
                if(note.Id==hold.Id)
                {
                    game.State.SimulationSeconds=interaction.StartSeconds+hold.TimeSeconds+.01;
                    var release=game.Execute("p0",new GameCommand{Id=Guid.NewGuid().ToString("N"),Kind="Rhythm",Amount=1,Direction=hold.Direction,TimeSeconds=hold.TimeSeconds+.01});
                    Assert.That(release.Accepted,Is.True,release.Reason);
                }
            }
            game.State.SimulationSeconds=interaction.StartSeconds+interaction.DurationSeconds+.75;game.Tick(.1);
            Assert.That(messages.Count,Is.EqualTo(chart.Notes.Count+1),"one diagnostic per real head and one dropped head");
            Assert.That(messages.FindAll(message=>message.Contains("drop id="+hold.Id+" ")).Count,Is.EqualTo(1));
            foreach(var message in messages)
            {
                Assert.That(message,Does.StartWith("[Festival.RhythmJudge]"));
                Assert.That(message,Does.Not.Contain("Tester"),"the opt-in trace contains note timing, never player identity or private dialogue");
            }
        }
#endif

        [Test]public void EachDanceLaneAcceptsAnArrowAndItsMatchingWasdKey()
        {
            string[] arrows={"leftArrow","downArrow","upArrow","rightArrow"};
            string[] wasd={"a","s","w","d"};
            using(var input=new FestivalInput("rhythm-contract-test"))
            {
                for(int lane=0;lane<4;lane++)
                {
                    var bindings=input.Notes[lane].bindings;
                    Assert.That(bindings.Count,Is.EqualTo(2),"Lane "+lane);
                    Assert.That(bindings[0].path,Is.EqualTo("<Keyboard>/"+arrows[lane]));
                    Assert.That(bindings[1].path,Is.EqualTo("<Keyboard>/"+wasd[lane]));
                }
            }
        }
    }
}
