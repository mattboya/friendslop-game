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
        [Test] public void DebugChatTransitionsDescribeCompletionAndPoliceTimeout()
        {
            var game=Day();var chat=Check(game,"ConfirmChat",out _);var events=new System.Collections.Generic.List<string>();
            System.Action<string,string,string,double> log=(action,reason,round,time)=>events.Add(action+"|"+reason);
            typeof(FestivalSimulation).GetField("ChatDiagnostic")?.SetValue(game,log);
            game.Execute(game.State.TripperId,new GameCommand{Id="debug_end",Kind="EndChat",TargetId=chat.Id});
            Assert.That(events,Does.Contain("ChatEnd|ConfirmChat Complete"),"debug completion reports kind and status without dialogue or vision truth");
            var player=game.Player(game.State.TripperId);var cop=new NpcState{Id="debug_cop",Kind="Cop",X=player.X+.5f,Z=player.Z};game.State.Npcs.Clear();game.State.Npcs.Add(cop);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="debug_police",Kind="Police",TargetId=cop.Id}).Accepted,Is.True);
            game.Tick(4.1);
            Assert.That(events,Does.Contain("ChatTimeout|Police"),"Police silence is diagnosable in editor/development builds");
        }

        [Test] public void ChatsRequireVersionSixAndRejectPreviousClients()
        {
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Old\",\"Token\":\"\",\"Protocol\":5}"),Is.False,"version five peers still time out chats and cannot join");
            Assert.That(FestivalSession.AcceptsHello("{\"Name\":\"Current\",\"Token\":\"\",\"Protocol\":6}"),Is.True,"the combined version-six wave accepts its peers");
        }

        [Test] public void TheOwnedClientEndsItsUntimedCheckAndAloneLearnsTruth()
        {
            var game=Day();game.State.Npcs.Clear();game.State.Visions.Clear();
            game.State.Npcs.Add(new NpcState{Id="quiet_target",Kind="Wook",Role="Regular",CanTalk=true,Z=1,Yaw=180});
            game.State.Visions.Add(new VisionState{Id="quiet_vision",NpcId="quiet_target",Kind="Buyer",IsTrue=false});
            var chat=Check(game,"ConfirmChat",out var vision);game.Tick(60);
            var mine=JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,game.State.TripperId)));
            Assert.That(mine.Interactions.Find(i=>i.Id==chat.Id)?.DurationSeconds,Is.EqualTo(0),"finite zero survives the wire");
            Assert.That(mine.Visions.Find(v=>v.Id==vision.Id).Confirmed,Is.False,"waiting discloses no truth");
            var ended=game.Execute(game.State.TripperId,new GameCommand{Id="owner_end",Kind="EndChat",TargetId=chat.Id});
            Assert.That(ended.Accepted,Is.True,ended.Reason);
            var verdict=FestivalSession.ViewFor(game,game.State.TripperId).Visions.Find(v=>v.Id==vision.Id);
            Assert.That(verdict.Confirmed&&verdict.IsTrue==vision.IsTrue,Is.True,"the check's verdict reaches its owner");
            foreach(var viewer in game.State.Players)if(viewer.Id!=game.State.TripperId)
            {
                var other=FestivalSession.ViewFor(game,viewer.Id);
                Assert.That(other.Visions,Is.Empty,"friends receive neither vision nor truth");
                Assert.That(other.Interactions,Is.Empty,"friends receive no private check");
            }
        }
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
            Assert.That(game.Execute(game.State.TripperId,new GameCommand{Id="end_check",Kind="EndChat",TargetId=chat.Id}).Accepted,Is.True);
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
