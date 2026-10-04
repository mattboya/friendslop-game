using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class ConversationConsumerPlayTests:FestivalPlayModeTest
    {
        [UnityTest] public IEnumerator HostedBuyWindowShowsTheSellerHunch()
        {
            const string hunch="You get the feeling they've got something to sell.";
            var world=new GameObject("impl:U77 seller world");world.AddComponent<FestivalWorld>();yield return null;
            var sessionObject=new GameObject("impl:U77 seller session");var session=sessionObject.AddComponent<FestivalSession>();yield return null;
            GameObject canvas=null;
            try
            {
                session.Host("Buyer",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
                game.State.Npcs.Clear();game.State.Seed=7;game.State.EntitySequence=0;player.X=0;player.Z=0;player.Effects.Clear();
                string id="seller_0";for(int n=1;DialogueGrammar.PersonaFor(id,"")!="Wook";n++)id="seller_"+n;
                game.State.Npcs.Add(new NpcState{Id=id,Kind="Wook",Role="Regular",X=0,Z=1.5f,Yaw=0,CanTalk=true});
                session.Command("StartBuy",target:id,item:FestivalSimulation.SellerStock(id));
                for(float deadline=Time.realtimeSinceStartup+5;player.InteractionId==""&&Time.realtimeSinceStartup<deadline;)yield return null;
                var talk=game.Interaction(player.InteractionId);Assert.That(talk,Is.Not.Null,session.Message);
                canvas=new GameObject("impl:U77 seller canvas",typeof(RectTransform),typeof(Canvas));
                ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
                var window=FestivalChatWindow.Build(canvas.transform,Resources.Load<Font>("FestivalDisplay"),null);
                window.Send=command=>session.Command(command.Kind,target:command.TargetId,item:command.ItemId,amount:command.Amount,direction:command.Direction);
                for(int turn=0;turn<3;turn++)
                {
                    InteractionState visible=null;
                    for(float deadline=Time.realtimeSinceStartup+5;Time.realtimeSinceStartup<deadline;)
                    {
                        visible=session.State.Interactions.Find(i=>i.Id==talk.Id);
                        if(visible!=null&&visible.Turns.Count==turn+1)break;
                        yield return null;
                    }
                    Assert.That(visible,Is.Not.Null);Assert.That(visible.Turns.Count,Is.EqualTo(turn+1));
                    if(turn==2)Assert.That(visible.Hunch,Is.EqualTo(hunch));
                    Assert.That(window.Follow(session.State,session.LocalPlayer,session.State.SimulationSeconds,turn,false),Is.True);
                    Assert.That(window.Pick(Array.IndexOf(visible.Turns[turn].Grades,ConversationLines.Smooth)),Is.True);
                }
                for(float deadline=Time.realtimeSinceStartup+5;talk.Status=="Active"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(talk.Status,Is.EqualTo("Complete"));yield return null;
                Assert.That(window.Follow(session.State,session.LocalPlayer,session.State.SimulationSeconds,3,false),Is.False,"the settled buy is no longer active");
                Assert.That(window.Lingering,Is.True,"the hosted window stays open to read the hunch");
                Assert.That(Enumerable.Range(0,window.Bubbles).Any(i=>window.IsNote(i)&&window.Shown(i)==hunch),Is.True,"the hosted Buy window renders the private seller hunch");
            }
            finally{session.Leave();if(canvas!=null)UnityEngine.Object.Destroy(canvas);UnityEngine.Object.Destroy(sessionObject);UnityEngine.Object.Destroy(world);}
        }
        [UnityTest] public IEnumerator HostedSlurredWindowRepliesStillCalmAPartnerFacingAway()
        {
            var world=new GameObject("impl:U13 conversation world");world.AddComponent<FestivalWorld>();yield return null;
            var sessionObject=new GameObject("impl:U13 conversation session");var session=sessionObject.AddComponent<FestivalSession>();yield return null;
            GameObject canvas=null;
            try
            {
                session.Host("Talker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
                game.State.Npcs.Clear();player.X=0;player.Z=0;player.Effects.Clear();
                player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=3,RemainingSeconds=600});
                string id="talker_0";for(int n=1;DialogueGrammar.PersonaFor(id,"")!="Wook";n++)id="talker_"+n;
                var partner=new NpcState{Id=id,Kind="Wook",Role="Regular",X=0,Z=1.5f,Yaw=0,CanTalk=true};
                partner.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=30,LastSeenSeconds=game.State.SimulationSeconds});game.State.Npcs.Add(partner);
                // A sober restored production simulation supplies the authored replies at this exact chart seed/history.
                var sober=new FestivalSimulation();sober.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State)));
                sober.Player(player.Id).Effects.Clear();
                Assert.That(sober.Execute(player.Id,new GameCommand{Id="plain_start",Kind="Conversation",TargetId=id}).Accepted,Is.True);
                var plain=sober.Interaction(sober.Player(player.Id).InteractionId);
                session.Command("Conversation",target:id);
                for(float deadline=Time.realtimeSinceStartup+5;player.InteractionId==""&&Time.realtimeSinceStartup<deadline;)yield return null;
                var talk=game.Interaction(player.InteractionId);Assert.That(talk,Is.Not.Null,session.Message);
                Assert.That(talk.ChartSeed,Is.EqualTo(plain.ChartSeed));
                canvas=new GameObject("impl:U13 hosted chat canvas",typeof(RectTransform),typeof(Canvas));
                ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
                var window=FestivalChatWindow.Build(canvas.transform,Resources.Load<Font>("FestivalDisplay"),null);
                window.Send=command=>session.Command(command.Kind,target:command.TargetId,item:command.ItemId,amount:command.Amount,direction:command.Direction);
                for(int turn=0;turn<3;turn++)
                {
                    InteractionState visible=null;
                    for(float deadline=Time.realtimeSinceStartup+5;Time.realtimeSinceStartup<deadline;)
                    {
                        visible=session.State.Interactions.Find(i=>i.Id==talk.Id);
                        if(visible!=null&&visible.Turns.Count==turn+1)break;
                        yield return null;
                    }
                    Assert.That(visible,Is.Not.Null);Assert.That(visible.Turns.Count,Is.EqualTo(turn+1));
                    Assert.That(window.Follow(session.State,session.LocalPlayer,session.State.SimulationSeconds,turn,false),Is.True);
                    var choices=visible.Turns[turn];Assert.That(choices.Garbled.Count(g=>g),Is.EqualTo(2));
                    for(int reply=0;reply<3;reply++)
                    {
                        string text=window.ReplyLabel(reply),original=plain.Turns[turn].Replies[reply];
                        Assert.That(text,Is.EqualTo(choices.Replies[reply]));Assert.That(text.Length,Is.EqualTo(original.Length));
                        foreach(Match boundary in Regex.Matches(original,@"[^\p{L}]"))Assert.That(text[boundary.Index],Is.EqualTo(original[boundary.Index]));
                        foreach(Match word in Regex.Matches(original,@"\p{L}+"))
                        {
                            string heard=text.Substring(word.Index,word.Length);
                            if(word.Length<5)Assert.That(heard,Is.EqualTo(word.Value));
                            else{Assert.That(heard[0],Is.EqualTo(word.Value[0]));Assert.That(heard[heard.Length-1],Is.EqualTo(word.Value[word.Length-1]));}
                        }
                    }
                    int pick=Array.IndexOf(choices.Grades,ConversationLines.Smooth);
                    Assert.That(window.Pick(pick),Is.True);
                    Assert.That(sober.Execute(player.Id,new GameCommand{Id="plain_reply_"+turn,Kind="Reply",Direction=turn,Amount=pick}).Accepted,Is.True);
                }
                for(float deadline=Time.realtimeSinceStartup+5;talk.Status=="Active"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(talk.Status,Is.EqualTo("Complete"));Assert.That(talk.Score,Is.EqualTo(1));
                Assert.That(partner.Observers[0].Suspicion,Is.EqualTo(15),"the hosted window's third reply calms its unseen partner once");
                Assert.That(partner.Yaw,Is.Zero,"settlement preserves the NPC's facing");
            }
            finally{session.Leave();if(canvas!=null)UnityEngine.Object.Destroy(canvas);UnityEngine.Object.Destroy(sessionObject);UnityEngine.Object.Destroy(world);}
        }
    }
}
