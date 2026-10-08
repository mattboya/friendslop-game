using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class DrumCircleSessionTests
    {
        static FestivalSimulation Started()
        {
            var game=new FestivalSimulation(123);game.AddPlayer("p0","First");game.AddPlayer("p1","Second");game.AddPlayer("ghost","Watcher").Life="Spirit";
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.Booth=new MinigameState{Kind="DrumCircle",Spot=1};var at=FestivalSimulation.BoothAt(game.State);
            foreach(var p in game.State.Players){p.X=at.X;p.Z=at.Z;}for(int k=0;k<35&&game.State.Phase!="Minigame";k++)game.Tick(.1);
            Assert.That(game.State.Phase,Is.EqualTo("Minigame"));return game;
        }
        static InteractionState Part(FestivalSimulation game,string id)
        {var part=game.State.Interactions.Find(i=>i.PlayerId==id&&i.Kind=="DrumPart");Assert.That(part,Is.Not.Null,"actual booth start creates a private drum part");return part;}
        static RoundState Wire(FestivalSimulation game,string id)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,id)));
        [Test]public void RecipientWireCarriesOnlyOwnChartAndInputsWithPublicNamesAndScalarAccuracy()
        {
            var game=Started();var part=Part(game,"p0");var note=RhythmChart.For(part).Notes[0];game.Tick(note.TimeSeconds);
            Assert.That(game.Execute("p0",new GameCommand{Id="drum-hit",Kind="Rhythm",Direction=note.Direction,TimeSeconds=note.TimeSeconds}).Accepted,Is.True);game.Tick(.2);
            var scalar=typeof(MinigameState).GetField("DrumAccuracies");Assert.That(scalar,Is.Not.Null);var host=(IList)scalar.GetValue(game.State.Booth);
            Assert.That(host.Count,Is.EqualTo(2));Assert.That(host[0].GetType().GetFields().Select(f=>f.Name),Is.EquivalentTo(new[]{"PlayerId","Accuracy"}),"public strip has no seed, chart or inputs");
            foreach(string id in new[]{"p0","p1","ghost"})
            {
                var view=Wire(game,id);Assert.That(view.Booth.Participants.Select(p=>p.Name),Is.EqualTo(new[]{"First","Second"}));Assert.That(((IList)scalar.GetValue(view.Booth)).Count,Is.EqualTo(2));
                Assert.That(view.Interactions.Select(i=>i.PlayerId),Is.EqualTo(id=="ghost"?Array.Empty<string>():new[]{id}),"only the owner gets their private chart/input detail");
                if(id!="ghost"){var own=view.Interactions[0];Assert.That(RhythmChart.For(own).Notes.Count,Is.EqualTo(24));Assert.That(own.Inputs.Count,Is.EqualTo(id=="p0"?1:0));}
                else Assert.That(view.Players.Select(p=>p.Id),Is.EqualTo(new[]{"ghost"}),"spirit visibility remains private");
            }
        }
        [Test]public void RecipientDrumSummariesAndOwnerInputsAreDetachedFromHostAndOtherViews()
        {
            var game=Started();var part=Part(game,"p0");var one=FestivalSession.ViewFor(game,"p0");var two=FestivalSession.ViewFor(game,"p0");
            Assert.That(one.Interactions[0],Is.Not.SameAs(part));one.Interactions[0].ChartSeed++;one.Interactions[0].Inputs.Add(new RhythmInput{Direction=0,TimeSeconds=2});
            Assert.That(part.Inputs,Is.Empty);Assert.That(two.Interactions[0].ChartSeed,Is.EqualTo(part.ChartSeed));Assert.That(two.Interactions[0].Inputs,Is.Empty);
            var field=typeof(MinigameState).GetField("DrumAccuracies");Assert.That(field,Is.Not.Null);var host=(IList)field.GetValue(game.State.Booth);var detached=(IList)field.GetValue(one.Booth);
            detached[0].GetType().GetField("Accuracy").SetValue(detached[0],1.0);detached.Clear();Assert.That(host.Count,Is.EqualTo(2));Assert.That(host[0].GetType().GetField("Accuracy").GetValue(host[0]),Is.EqualTo(0.0));
        }
        [Test]public void UnityHostSnapshotContinuesTheSamePartAndScoredViewsRetainOnlyPublicResult()
        {
            var game=Started();Part(game,"p0");game.Tick(4);var saved=JsonUtility.ToJson(game.State);var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(saved));
            var original=Part(game,"p0");var copy=Part(restored,"p0");Assert.That(copy.ChartSeed,Is.EqualTo(original.ChartSeed));Assert.That(copy.StartSeconds,Is.EqualTo(original.StartSeconds).Within(1e-9));Assert.That(copy.DurationSeconds,Is.EqualTo(original.DurationSeconds).Within(1e-9));Assert.That(RhythmChart.For(copy).Notes.Select(n=>n.Direction),Is.EqualTo(RhythmChart.For(original).Notes.Select(n=>n.Direction)));Assert.That(copy.Inputs.Count,Is.EqualTo(original.Inputs.Count));game.Tick(20);restored.Tick(20);
            Assert.That(restored.State.Booth.ClockChange,Is.EqualTo(-30));Assert.That(restored.State.Booth.ClockChange,Is.EqualTo(game.State.Booth.ClockChange));
            foreach(string id in new[]{"p0","p1","ghost"}){var view=Wire(restored,id);Assert.That(view.Interactions,Is.Empty);Assert.That(view.Booth.Participants.Count,Is.EqualTo(2));Assert.That(view.Booth.ClockChange,Is.EqualTo(-30));}
            var old=JsonUtility.FromJson<MinigameState>("{}");var field=typeof(MinigameState).GetField("DrumAccuracies");Assert.That(field,Is.Not.Null);Assert.That(((IList)field.GetValue(old)).Count,Is.Zero);Assert.That(typeof(MinigameState).GetField("DrumWorstScore").GetValue(old),Is.EqualTo(-1.0));
        }
        [Test]public void LiveDrumReplayEmitsNoPrivateOrRepeatedJudgeDiagnostics()
        {
            var game=Started();var events=new System.Collections.Generic.List<string>();game.RhythmDiagnostic=events.Add;var part=Part(game,"p0");var note=RhythmChart.For(part).Notes[0];game.Tick(note.TimeSeconds);
            Assert.That(game.Execute("p0",new GameCommand{Id="debug-drum-hit",Kind="Rhythm",Direction=note.Direction,TimeSeconds=note.TimeSeconds}).Accepted,Is.True);game.Tick(.5);
            Assert.That(events,Is.Empty,"live replay must not repeatedly format/private-log note inputs");game.Tick(20);Assert.That(events.Count,Is.EqualTo(1));Assert.That(events[0],Does.StartWith("[Festival.DrumCircle] scored participants="));
        }
        [UnityTearDown]public IEnumerator LeaveOwnedPlayMode(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest]public IEnumerator ActualHostRhythmRouteUsesRawCalibrationOnceWhileWorldTimeIsHeld()
        {
            yield return new EnterPlayMode();string previous=LocalProfile.Folder,scope=FestivalInput.PreferencesScope;string folder=Path.Combine(Path.GetTempPath(),"impl-U27-drum-host-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);LocalProfile.Folder=folder;FestivalInput.PreferencesScope=folder;var go=new GameObject("impl-U27 drum host");
            try
            {
                var session=go.AddComponent<FestivalSession>();session.enabled=false;session.Host("Drum test",8612);Assert.That(session.IsHost&&session.Connected,Is.True,session.Message);
                var game=Started();var part=Part(game,"p0");typeof(FestivalSession).GetField("simulation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,game);typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,"p0");typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(game,"p0"));
                double world=session.EstimatedWorldSeconds,anchor=part.StageFirstHeadWorldSeconds;var note=RhythmChart.For(part).Notes[0];session.Profile.Data.TimingCalibrationMs=60;game.Tick(note.TimeSeconds+.06);typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(game,"p0"));session.Rhythm(note.Direction,note.TimeSeconds+.06);
                Assert.That(session.LastLocalCommandResult.Accepted,Is.True,session.Message);Assert.That(part.Inputs.Count,Is.EqualTo(1));Assert.That(part.Inputs[0].TimeSeconds,Is.EqualTo(note.TimeSeconds).Within(1e-9));Assert.That(part.StageFirstHeadWorldSeconds,Is.EqualTo(anchor));Assert.That(session.EstimatedWorldSeconds,Is.EqualTo(world).Within(1e-9));
                var clock=typeof(FestivalSession).GetProperty("EstimatedMinigameSeconds");Assert.That(clock,Is.Not.Null,"recorded drum consumer gets advancing game-local transport");Assert.That((double)clock.GetValue(session),Is.GreaterThan(2));
                session.Command("Cancel");Assert.That(session.LastLocalCommandResult.Accepted,Is.True,session.Message);Assert.That(game.Player("p0").InteractionId,Is.Empty);Assert.That(Wire(game,"p1").Interactions[0].PlayerId,Is.EqualTo("p1"));
                session.Leave();Assert.That(session.State,Is.Null,"Leave clears private drum/session transport state");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);LocalProfile.Folder=previous;FestivalInput.PreferencesScope=scope;if(Directory.Exists(folder))Directory.Delete(folder,true);}
            yield return new ExitPlayMode();
        }
    }
}
