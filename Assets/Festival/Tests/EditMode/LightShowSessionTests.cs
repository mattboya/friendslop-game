using System;
using System.Collections;
using System.Collections.Generic;
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
    // MINI-3: the Light Show's script and question timing are public; each viewer's dose, fakes, open questions and answers reach
    // that viewer alone, without answer keys, until the game scores and every captured player's answers are revealed at once.
    public sealed class LightShowSessionTests
    {
        static int sequence;
        static FestivalSimulation Started(Action<FestivalSimulation> before=null)
        {
            var game=new FestivalSimulation(123);game.AddPlayer("p0","First").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=100});game.AddPlayer("p1","Second");game.AddPlayer("ghost","Watcher").Life="Spirit";
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.Booth=new MinigameState{Kind=FestivalSimulation.LightShowGame,Spot=1};var at=FestivalSimulation.BoothAt(game.State);
            foreach(var p in game.State.Players){p.X=at.X;p.Z=at.Z;}before?.Invoke(game);for(int k=0;k<35&&game.State.Phase!="Minigame";k++)game.Tick(.1);
            Assert.That(game.State.Phase,Is.EqualTo("Minigame"));return game;
        }
        static RoundState Wire(FestivalSimulation game,string id)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,id)));
        static CommandResult Answer(FestivalSimulation game,string id,int slot,int choice)=>game.Execute(id,new GameCommand{Id="light"+(sequence++),Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.LightShowAnswer,Amount=choice,Direction=slot});
        static LightShowPlayer Host(FestivalSimulation game,string id)=>game.State.Booth.LightShow.Players.Find(p=>p.PlayerId==id);
        [Test]public void WireViewsCarryEachViewersOwnRecordUntilTheSimultaneousReveal()
        {
            var game=Started();game.Tick(5);
            foreach(string id in new[]{"p0","p1","ghost"})
            {
                var show=Wire(game,id).Booth.LightShow;Assert.That(show.Events.Count,Is.EqualTo(game.State.Booth.LightShow.Events.Count),"every viewer gets the public real script");
                Assert.That(show.Players.Select(p=>p.PlayerId),Is.EqualTo(id=="ghost"?Array.Empty<string>():new[]{id}),"only the viewer's own private record");
                if(id=="ghost")continue;var own=show.Players[0];
                Assert.That(own.Questions,Is.Empty,"no question shows during the show");Assert.That(own.Dose,Is.EqualTo(Host(game,id).Dose));Assert.That(own.Fakes.Count,Is.EqualTo(Host(game,id).Fakes.Count));
            }
            Assert.That(Wire(game,"p0").Booth.LightShow.Players[0].Fakes.Count,Is.EqualTo(2),"the dosed viewer's two fakes reach them");
            game.Tick(16);Assert.That(Answer(game,"p0",0,1).Accepted,Is.True);
            var mine=Wire(game,"p0").Booth.LightShow.Players.Single();
            Assert.That(mine.Questions.Select(q=>q.CorrectIndex),Is.EqualTo(new[]{-1}),"the open question has no answer key");Assert.That(mine.Questions[0].Prompt,Is.EqualTo(Host(game,"p0").Questions[0].Prompt));Assert.That(mine.Answers,Is.EqualTo(new[]{1}));
            var peer=Wire(game,"p1").Booth.LightShow.Players.Single();Assert.That(peer.PlayerId,Is.EqualTo("p1"));Assert.That(peer.Answers,Is.Empty,"no peer submission leaks");
            Assert.That(Wire(game,"ghost").Booth.LightShow.Players,Is.Empty);
            Assert.That(Answer(game,"p1",0,0).Accepted,Is.True);foreach(int slot in new[]{1,2})foreach(string id in new[]{"p0","p1"})Assert.That(Answer(game,id,slot,-1).Accepted,Is.True);
            Assert.That(game.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0));
            foreach(string id in new[]{"p0","p1","ghost"})
            {
                var view=Wire(game,id);Assert.That(view.Booth.LightShow.Players.Select(p=>p.PlayerId),Is.EqualTo(new[]{"p0","p1"}),"one simultaneous reveal for every viewer");
                foreach(var record in view.Booth.LightShow.Players)
                {
                    var host=Host(game,record.PlayerId);Assert.That(record.Questions.Select(q=>q.CorrectIndex),Is.EqualTo(host.Questions.Select(q=>q.CorrectIndex)));Assert.That(record.Answers,Is.EqualTo(host.Answers));
                    Assert.That(record.Fakes.Count,Is.EqualTo(record.PlayerId==id?host.Fakes.Count:0),"fakes never reach another player");
                }
                Assert.That(view.Booth.ClockChange,Is.EqualTo(game.State.Booth.ClockChange));
            }
        }
        [Test]public void CupsSecretsStayMaskedBesideTheLightShowFilter()
        {
            var game=new FestivalSimulation(7);game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");game.State.Phase=FestivalSimulation.MinigamePhase;game.State.TripperId="p0";
            var b=game.State.Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=1,Cups=4,Spiked=2,Hint=3};b.PlayerIds.AddRange(new[]{"p0","p1"});
            var tripper=Wire(game,"p0");var friend=Wire(game,"p1");
            Assert.That(new[]{tripper.Booth.Spiked,tripper.Booth.Hint,friend.Booth.Spiked,friend.Booth.Hint},Is.EqualTo(new[]{-1,3,-1,-1}),"the spiked cup stays on the host and the hint reaches the tripper alone");
            Assert.That(tripper.Booth.LightShow.Players,Is.Empty);Assert.That(new[]{b.Spiked,b.Hint},Is.EqualTo(new[]{2,3}));
        }
        [Test]public void LightShowDiagnosticsNameOnlyQuestionSlotsAndTheResult()
        {
            var events=new List<string>();var game=Started(g=>g.RhythmDiagnostic=events.Add);game.Tick(21);
            Assert.That(Answer(game,"p0",0,0).Accepted&&Answer(game,"p1",0,0).Accepted,Is.True);game.Tick(8.1);
            Assert.That(Answer(game,"p0",2,1).Accepted&&Answer(game,"p1",2,1).Accepted,Is.True);game.Tick(FestivalSimulation.MinigameBannerSeconds+1);
            Assert.That(events.Take(4),Is.EqualTo(new[]{"[Festival.LightShow] start participants=2","[Festival.LightShow] question=0 closed=answered","[Festival.LightShow] question=1 closed=timeout","[Festival.LightShow] question=2 closed=answered"}));
            Assert.That(events.Count,Is.EqualTo(5),"each transition is reported once");Assert.That(events[4],Does.StartWith("[Festival.LightShow] reveal participants=2 clock_change="));
            var secrets=game.State.Booth.LightShow.Players.SelectMany(p=>p.Questions).SelectMany(q=>q.Choices.Append(q.Prompt).Append(q.Category)).Where(t=>!t.All(char.IsDigit)).ToList();
            Assert.That(events.Where(e=>secrets.Any(e.Contains)||e.Contains("Correct")),Is.Empty,"no question, choice or answer key is logged");
        }
        [UnityTearDown]public IEnumerator LeaveOwnedPlayMode(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest]public IEnumerator HostedAnswerGoesThroughTheSessionAndTheLocalViewStaysPrivate()
        {
            yield return new EnterPlayMode();string previous=LocalProfile.Folder,scope=FestivalInput.PreferencesScope;string folder=Path.Combine(Path.GetTempPath(),"impl-U29-light-host-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);LocalProfile.Folder=folder;FestivalInput.PreferencesScope=folder;var go=new GameObject("impl-U29 light host");
            try
            {
                var session=go.AddComponent<FestivalSession>();session.enabled=false;session.Host("Light test",0);Assert.That(session.IsHost&&session.Connected,Is.True,session.Message);
                var game=Started();game.Tick(21);typeof(FestivalSession).GetField("simulation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,game);typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,"p0");typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(game,"p0"));
                session.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.LightShowAnswer,amount:2,direction:0);Assert.That(session.LastLocalCommandResult.Accepted,Is.True,session.Message);Assert.That(Host(game,"p0").Answers,Is.EqualTo(new[]{2}));
                session.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.LightShowAnswer,amount:1,direction:0);Assert.That(session.LastLocalCommandResult.Accepted,Is.False,"the session cannot overwrite a sent answer");Assert.That(Host(game,"p0").Answers,Is.EqualTo(new[]{2}));
                var local=FestivalSession.ViewFor(game,"p0").Booth.LightShow;Assert.That(local.Players.Select(p=>p.PlayerId),Is.EqualTo(new[]{"p0"}),"the host's own view holds its player's record alone");Assert.That(local.Players[0].Questions.Select(q=>q.CorrectIndex),Is.EqualTo(new[]{-1}));
                local.Players[0].Answers.Clear();Assert.That(Host(game,"p0").Answers,Is.EqualTo(new[]{2}),"the host's local view is detached from its simulation");
                session.Leave();Assert.That(session.State,Is.Null);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);LocalProfile.Folder=previous;FestivalInput.PreferencesScope=scope;if(Directory.Exists(folder))Directory.Delete(folder,true);}
            yield return new ExitPlayMode();
        }
    }
}
