using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Tests
{
    // TRIP-3: a check's conversation and its verdict reach only the tripper's client; everyone sees a check dance as a dance.
    public sealed class ConfirmSessionTests
    {
        static FestivalSimulation Day()
        {
            var game=new FestivalSimulation(29);
            foreach(var id in new[]{"host","friend","guest"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
            Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: Day 1 under way");
            return game;
        }
        // The tripper starts a check on someone they have a vision about, standing right in front of them.
        static InteractionState Check(FestivalSimulation game,string kind,out VisionState vision)
        {
            var tripper=game.Player(game.State.TripperId);var seen=game.State.Visions.Find(v=>v.NpcId!="");var npc=game.State.Npcs.Find(n=>n.Id==seen.NpcId);vision=seen;
            double yaw=npc.Yaw*Mathf.Deg2Rad;tripper.X=npc.X+(float)System.Math.Sin(yaw);tripper.Z=npc.Z+(float)System.Math.Cos(yaw);
            var started=game.Execute(tripper.Id,new GameCommand{Id="check_"+kind,Kind=kind,TargetId=npc.Id});
            Assert.That(started.Accepted,Is.True,"setup: the tripper starts a "+kind+": "+started.Reason);
            return game.Interaction(tripper.InteractionId);
        }

        [Test] public void OnlyTheTrippersClientReadsTheChat()
        {
            var game=Day();var chat=Check(game,"ConfirmChat",out var vision);var said=chat.Chat;
            var wire=JsonUtility.ToJson(FestivalSession.ViewFor(game,game.State.TripperId));var mine=JsonUtility.FromJson<RoundState>(wire);
            foreach(var answer in said.Answers)Assert.That(wire,Does.Contain(answer),"the answers travel as plain text, so searching a view for them finds them");
            var sent=mine.Interactions.Find(i=>i.Id==chat.Id);
            Assert.That(sent,Is.Not.Null,"the tripper's client gets their chat");
            Assert.That(sent.Chat.Opener,Is.EqualTo(said.Opener),"with the opener");
            Assert.That(sent.Chat.Questions,Is.EqualTo(said.Questions),"the three questions");
            Assert.That(sent.Chat.Answers,Is.EqualTo(said.Answers),"and the answers, over the wire");
            foreach(var viewer in game.State.Players)
            {
                if(viewer.Id==game.State.TripperId)continue;
                var json=JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer.Id));
                foreach(var answer in said.Answers)Assert.That(json,Does.Not.Contain(answer),viewer.Id+"'s client never hears the answers");
            }
            game.Tick(5.1);
            var verdict=FestivalSession.ViewFor(game,game.State.TripperId).Visions.Find(v=>v.Id==vision.Id);
            Assert.That(verdict.Confirmed&&verdict.IsTrue==vision.IsTrue,Is.True,"the chat's verdict reaches the tripper's client");
            foreach(var viewer in game.State.Players)if(viewer.Id!=game.State.TripperId)Assert.That(FestivalSession.ViewFor(game,viewer.Id).Visions,Is.Empty,viewer.Id+"'s client gets no verdict");
        }

        [Test] public void EveryoneSeesACheckDanceAsADance()
        {
            var game=Day();Check(game,"ConfirmDance",out _);
            foreach(var viewer in game.State.Players)
                Assert.That(FestivalSession.ViewFor(game,viewer.Id).Players.Find(p=>p.Id==game.State.TripperId).VisualPose,Is.EqualTo("Dance"),viewer.Id+" sees the tripper dance with the festivalgoer");
        }

        [Test] public void TheClientPlaysACheckDanceAsARhythmChallenge()
        {
            Assert.That(FestivalInput.IsRhythmKind("ConfirmDance"),Is.True,"a check dance shows the note lanes and holds the player still");
            Assert.That(FestivalInput.IsRhythmKind("ConfirmChat"),Is.False,"a chat has no notes");
        }
    }
}
