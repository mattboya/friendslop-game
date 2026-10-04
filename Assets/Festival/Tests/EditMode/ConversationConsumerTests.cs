using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class ConversationConsumerTests
    {
        GameObject canvas;
        [TearDown] public void TearDown(){if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas);}

        static FestivalSimulation Pair(int dose,out PlayerState player,out NpcState partner)
        {
            var game=new FestivalSimulation(21);player=game.AddPlayer("owner","Owner");game.AddPlayer("friend","Friend");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.TripperId=player.Id;
            player.X=0;player.Z=0;game.Player("friend").X=30;
            string id="talker_0";for(int n=1;DialogueGrammar.PersonaFor(id,"")!="Wook";n++)id="talker_"+n;
            partner=new NpcState{Id=id,Kind="Wook",Role="Regular",X=0,Z=1.5f,Yaw=0,CanTalk=true};game.State.Npcs.Add(partner);
            if(dose>0)player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose,RemainingSeconds=600});
            return game;
        }
        static InteractionState Start(FestivalSimulation game,PlayerState player,NpcState partner)
        {
            var result=game.Execute(player.Id,new GameCommand{Id="start",Kind="Conversation",TargetId=partner.Id});
            Assert.That(result.Accepted,Is.True,result.Reason);return game.Interaction(player.InteractionId);
        }
        FestivalChatWindow Window(FestivalSimulation game,PlayerState player)
        {
            canvas=new GameObject("impl:U13 chat canvas",typeof(RectTransform),typeof(Canvas));
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
            var window=FestivalChatWindow.Build(canvas.transform,Resources.Load<Font>("FestivalDisplay"),null);
            int sequence=0;window.Send=command=>{command.Id="reply_"+(sequence++);Assert.That(game.Execute(player.Id,command).Accepted,Is.True);};
            return window;
        }
        static RoundState View(FestivalSimulation game,string player)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,player)));
        static void Follow(FestivalChatWindow window,FestivalSimulation game,PlayerState player,float now)
        {
            var view=View(game,player.Id);Assert.That(window.Follow(view,view.Players.Find(p=>p.Id==player.Id),view.SimulationSeconds,now,false),Is.True);
        }

        [TestCase(0f)] [TestCase(180f)]
        public void WindowRepliesCalmThePartnerOnceWithoutTurningThem(float yaw)
        {
            var game=Pair(0,out var player,out var partner);partner.Yaw=yaw;
            partner.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=30});
            var interaction=Start(game,player,partner);var window=Window(game,player);
            for(int turn=0;turn<3;turn++)
            {
                Follow(window,game,player,turn);
                Assert.That(window.Pick(Array.IndexOf(interaction.Turns[turn].Grades,ConversationLines.Smooth)),Is.True);
            }
            Assert.That(interaction.Status,Is.EqualTo("Complete"));Assert.That(interaction.Score,Is.EqualTo(1));
            Assert.That(partner.Observers[0].Suspicion,Is.EqualTo(15),"the window's third reply settles partner or witness once");
            Assert.That(partner.Yaw,Is.EqualTo(yaw),"the visibility cone stays unchanged");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ReadableSlurredChoicesSurviveThePrivateSnapshotAndWindow(int dose)
        {
            var game=Pair(dose,out var player,out var partner);var talk=Start(game,player,partner);var window=Window(game,player);
            var sober=Pair(0,out var plainPlayer,out var plainPartner);var plain=Start(sober,plainPlayer,plainPartner);
            for(int turn=0;turn<3;turn++)
            {
                Follow(window,game,player,turn);var choices=talk.Turns[turn];var originals=plain.Turns[turn];
                Assert.That(choices.Garbled.Count(g=>g),Is.EqualTo(dose>=3?2:dose>=1?1:0));
                Assert.That(choices.Grades,Is.EqualTo(originals.Grades));Assert.That(window.Replies,Is.EqualTo(3));
                Assert.That(View(game,"friend").Interactions,Is.Empty,"reply text and grades belong to the talker");
                for(int reply=0;reply<3;reply++)
                {
                    string shown=window.ReplyLabel(reply),original=originals.Replies[reply];
                    Assert.That(shown,Is.EqualTo(choices.Replies[reply]),"the real window renders the host's choice");
                    Assert.That(shown.Length,Is.EqualTo(original.Length));
                    foreach(Match boundary in Regex.Matches(original,@"[^\p{L}]"))Assert.That(shown[boundary.Index],Is.EqualTo(original[boundary.Index]),"punctuation and spaces retain their positions");
                    foreach(Match word in Regex.Matches(original,@"\p{L}+"))
                    {
                        string heard=shown.Substring(word.Index,word.Length);
                        if(word.Length<5)Assert.That(heard,Is.EqualTo(word.Value),"short words stay readable");
                        else{Assert.That(heard[0],Is.EqualTo(word.Value[0]));Assert.That(heard[heard.Length-1],Is.EqualTo(word.Value[word.Length-1]));}
                    }
                    if(!choices.Garbled[reply])Assert.That(shown,Is.EqualTo(original));
                }
                int pick=Array.IndexOf(choices.Grades,ConversationLines.Smooth);
                Assert.That(window.Pick(pick),Is.True);
                Assert.That(sober.Execute(plainPlayer.Id,new GameCommand{Id="plain_"+turn,Kind="Reply",Direction=turn,Amount=pick}).Accepted,Is.True);
            }
            Assert.That(talk.Score,Is.EqualTo(plain.Score),"slurring never changes the grade");
        }

        [Test] public void SettlementDiagnosticFiresOnlyAfterTheThirdAcceptedReply()
        {
            var game=Pair(0,out var player,out var partner);var talk=Start(game,player,partner);
            var events=new List<string>();game.ChatDiagnostic=(eventKind,status,round,seconds)=>events.Add(eventKind+" "+status);
            for(int turn=0;turn<3;turn++)
            {
                Assert.That(events.Count,Is.Zero,"there is no settlement before the final reply");
                Assert.That(game.Execute(player.Id,new GameCommand{Id="reply_"+turn,Kind="Reply",Direction=turn,Amount=0}).Accepted,Is.True);
            }
            Assert.That(events,Is.EqualTo(new[]{"TalkSettled Conversation"}),"debugging emits only the event and kind, without dialogue, grade or identity");
            Assert.That(game.Execute(player.Id,new GameCommand{Id="late",Kind="Reply",Direction=2,Amount=0}).Accepted,Is.False);
            Assert.That(events.Count,Is.EqualTo(1));Assert.That(talk.Status,Is.EqualTo("Complete"));
        }
    }
}
