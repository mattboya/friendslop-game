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
        [UnityTest] public IEnumerator JoinedOwnerReceivesReliableHistoryAndDurableNarcOutcome()
        {
            var world=new GameObject("impl-U14 joined world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("impl-U14 host");var host=hostObject.AddComponent<FestivalSession>();
            var clientObject=new GameObject("impl-U14 client");var client=clientObject.AddComponent<FestivalSession>();yield return null;
            string hostProfile="impl-U14-host-"+Guid.NewGuid().ToString("N"),clientProfile="impl-U14-client-"+Guid.NewGuid().ToString("N");
            typeof(FestivalSession).GetProperty("Profile").SetValue(host,new LocalProfile(hostProfile));
            typeof(FestivalSession).GetProperty("Profile").SetValue(client,new LocalProfile(clientProfile));
            try
            {
                ushort port=FreePort();host.Host("Host",port);float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);client.Join("Owner","127.0.0.1",port);
                while(client.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(client.LocalPlayer,Is.Not.Null,client.Message);
                var game=Simulation(host);var owner=game.Player(client.LocalPlayerId);owner.X=0;owner.Z=19;client.Command("Ready");
                while(!owner.Ready&&Time.realtimeSinceStartup<deadline)yield return null;yield return StartLevel(host,owner.Id);
                game.State.Npcs.Clear();game.HasLineOfSight=(ax,az,bx,bz)=>false;owner.X=0;owner.Z=0;owner.Effects.Clear();
                string id="narc_0";for(int n=1;DialogueGrammar.PersonaFor(id,"")!="Wook";n++)id="narc_"+n;
                game.State.Npcs.Add(new NpcState{Id=id,Kind="Wook",Role="Narc",Z=1.5f,CanTalk=true});
                host.MenuOpen=client.MenuOpen=false;client.Command("StartBuy",target:id,item:FestivalSimulation.SellerStock(id));deadline=Time.realtimeSinceStartup+10;
                while(owner.InteractionId==""&&Time.realtimeSinceStartup<deadline)yield return null;
                var talk=game.Interaction(owner.InteractionId);Assert.That(talk,Is.Not.Null,client.Message);
                for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
                {
                    InteractionState visible=null;
                    while(Time.realtimeSinceStartup<deadline)
                    {
                        visible=client.State.Interactions.Find(i=>i.Id==talk.Id);if(visible!=null&&visible.Turns.Count==turn+1)break;yield return null;
                    }
                    Assert.That(visible,Is.Not.Null);Assert.That(visible.Turns.Count,Is.EqualTo(turn+1));
                    client.AcknowledgeConversationTurn(visible);
                    client.Command("Reply",amount:Array.IndexOf(visible.Turns[turn].Grades,ConversationLines.Smooth),direction:turn);
                    while(talk.Status=="Active"&&talk.Turns.Count==turn+1&&Time.realtimeSinceStartup<deadline)yield return null;
                }
                Assert.That(owner.Life,Is.EqualTo("Detained"));game.State.Interactions.Clear();
                while((client.PendingConversationOutcome==null||client.Profile.Data.SeenDialogue.Count==0)&&Time.realtimeSinceStartup<deadline)yield return null;
                var outcome=client.PendingConversationOutcome;Assert.That(outcome,Is.Not.Null,"the skipped terminal interaction still reaches its owner's real session consumer");
                Assert.That(outcome.Notice,Is.EqualTo("Busted! That was an undercover narc."));Assert.That(Festivals.InHolding(client.LocalPlayer.X,client.LocalPlayer.Z),Is.True,"the joined owner sees themself held inside the security building, at "+client.LocalPlayer.X+", "+client.LocalPlayer.Z);
                Assert.That(new LocalProfile(clientProfile).Data.SeenDialogue,Is.Not.Empty,"joined owner's reliable history reaches the isolated real profile");
                Assert.That(host.Profile.Data.SeenDialogue,Is.Empty,"host player receives no remote-owner grammar payload");
                Assert.That(JsonUtility.ToJson(host.State),Does.Not.Contain(outcome.Notice));
                foreach(string key in owner.Dialogue.Seen)Assert.That(JsonUtility.ToJson(host.State),Does.Not.Contain(key));
                string ownerId=owner.Id,deliveryId=outcome.DeliveryId;client.Leave();deadline=Time.realtimeSinceStartup+10;
                while(owner.Connected&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(owner.Connected,Is.False);yield return null;
                client.Join("Owner","127.0.0.1",port);deadline=Time.realtimeSinceStartup+30;
                while((client.LocalPlayer==null||client.PendingConversationOutcome==null)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(client.LocalPlayerId,Is.EqualTo(ownerId),"the real reconnect retains authenticated ownership");outcome=client.PendingConversationOutcome;
                Assert.That(outcome?.DeliveryId,Is.EqualTo(deliveryId),"an undismissed terminal outcome survives real reconnect");
                client.AcknowledgeConversationOutcome(outcome);client.DismissConversationOutcome(outcome.DeliveryId);
                Assert.That(client.PendingConversationOutcome,Is.Null,"Escape's production dismissal consumer suppresses a stale snapshot immediately");
                while(owner.ConversationOutcomes.Count>0&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(owner.ConversationOutcomes,Is.Empty,"the authority remembers dismissal for reconnect");
                client.Leave();deadline=Time.realtimeSinceStartup+10;while(owner.Connected&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
                client.Join("Owner","127.0.0.1",port);deadline=Time.realtimeSinceStartup+30;
                while(client.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(client.LocalPlayerId,Is.EqualTo(ownerId));Assert.That(client.PendingConversationOutcome,Is.Null,"dismissed outcomes stay closed across real reconnect");
            }
            finally{client.Leave();host.Leave();UnityEngine.Object.Destroy(clientObject);UnityEngine.Object.Destroy(hostObject);UnityEngine.Object.Destroy(world);}
            yield return null;
        }

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
                for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
                {
                    InteractionState visible=null;
                    for(float deadline=Time.realtimeSinceStartup+5;Time.realtimeSinceStartup<deadline;)
                    {
                        visible=session.State.Interactions.Find(i=>i.Id==talk.Id);
                        if(visible!=null&&visible.Turns.Count==turn+1)break;
                        yield return null;
                    }
                    Assert.That(visible,Is.Not.Null);Assert.That(visible.Turns.Count,Is.EqualTo(turn+1));
                    if(turn==FestivalSimulation.ConversationTurns-1)Assert.That(visible.Hunch,Is.EqualTo(hunch));
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
                for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
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
                for(float deadline=Time.realtimeSinceStartup+5;session.Profile.Data.SeenDialogue.Count==0&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.Profile.Data.SeenDialogue,Is.Not.Empty,"host-acknowledged reply grammar reaches the real owning profile");
                Assert.That(player.NpcSpeech,Is.Not.Empty,"the hosted third reply leaves a final reaction");
                var reload=new LocalProfile("default");Assert.That(reload.Data.SeenDialogue,Is.Not.Empty,"acknowledged grammar survives profile reload");
                Assert.That(partner.Observers[0].Suspicion,Is.EqualTo(15),"the hosted window's third reply calms its unseen partner once");
                Assert.That(partner.Yaw,Is.Zero,"settlement preserves the NPC's facing");
            }
            finally{session.Leave();if(canvas!=null)UnityEngine.Object.Destroy(canvas);UnityEngine.Object.Destroy(sessionObject);UnityEngine.Object.Destroy(world);}
        }

        // The HUD's "Bought ..." notice follows the buy's money, not the stack, so a handoff in the middle of a buy (the host
        // takes those mid-buy) neither fakes nor hides it. A friend's unit accepted mid-buy, then the buy cancelled: the price came
        // back, so no notice. One of your own handed away mid-buy, then the buy talked through: the price was spent, so the notice.
        [UnityTest] public IEnumerator TheBuyNoticeFollowsTheMoneyThroughAHandoff()
        {
            var world=new GameObject("Buy notice world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Buy notice HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            var failures=new System.Collections.Generic.List<string>();
            try
            {
                session.Host("Buyer",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);var mate=game.AddPlayer("econ4_mate","Sam");mate.Ready=true;
                game.State.FestivalIndex=Festivals.PoloFestival;
                yield return StartLevel(session);
                game.State.Npcs.Clear();player.Effects.Clear();player.Cash=20;player.X=0;player.Z=0;mate.X=1;mate.Z=0;
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";mate.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");
                int sequence=0;
                bool Do(PlayerState who,string kind,string target="",string item="",int amount=0,int direction=0)=>
                    game.Execute(who.Id,new GameCommand{Id="econ4_"+(sequence++),Kind=kind,TargetId=target,ItemId=item,Amount=amount,Direction=direction}).Accepted;
                // Two Wook sellers (no dance first) of the same kind.
                var sellers=new System.Collections.Generic.List<string>();
                for(int n=0;sellers.Count<2;n++){string id="econ4_seller_"+n;if(DialogueGrammar.PersonaFor(id,"")=="Wook"&&FestivalSimulation.SellerStock(id)=="stock_lsd")sellers.Add(id);}
                foreach(var id in sellers)game.State.Npcs.Add(new NpcState{Id=id,Kind="Wook",Role="Regular",X=0,Z=1.5f,Yaw=180,CanTalk=true});
                const string Bought="Bought Tongue Stamps for $5";

                // 1. Sam's Tongue Stamp accepted mid-buy, then the buy cancelled.
                player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");mate.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});session.SetMessage("");
                Assert.That(Do(player,"StartBuy",sellers[0],"stock_lsd"),Is.True,"setup: the first buy starts");
                yield return new WaitForSeconds(.3f);
                Assert.That(Do(mate,"Transfer",player.Id,"stock_lsd",1),Is.True,"setup: Sam offers one");
                Assert.That(Do(player,"AcceptTransfer",game.State.Transfers.Find(t=>t.ToId==player.Id).Id),Is.True,"setup: accepted mid-buy");
                yield return new WaitForSeconds(.3f);
                Assert.That(Do(player,"Cancel"),Is.True,"setup: the buy cancelled");
                yield return new WaitForSeconds(.4f);
                Assert.That(player.Cash,Is.EqualTo(20),"setup: the price came back");
                if(session.Message==Bought)failures.Add("a cancelled buy, with a friend's unit taken mid-buy, says \""+session.Message+"\"");

                // 2. That Tongue Stamp handed back to Sam mid-buy, then the buy talked through.
                session.SetMessage("");
                Assert.That(Do(player,"StartBuy",sellers[1],"stock_lsd"),Is.True,"setup: the second buy starts");
                yield return new WaitForSeconds(.3f);
                Assert.That(Do(player,"Transfer",mate.Id,"stock_lsd",1),Is.True,"setup: one handed to Sam mid-buy");
                yield return new WaitForSeconds(.3f);
                var talk=game.Interaction(player.InteractionId);Assert.That(talk?.Kind,Is.EqualTo("Buy"),"setup: still buying");
                while(talk.Status=="Active"&&talk.Turns.Count>0)
                {
                    var turn=talk.Turns[talk.Turns.Count-1];
                    Assert.That(Do(player,"Reply",amount:Array.IndexOf(turn.Grades,ConversationLines.Smooth),direction:talk.Turns.Count-1),Is.True,"setup: a smooth reply");
                }
                yield return new WaitForSeconds(.4f);
                Assert.That(player.Cash,Is.EqualTo(15),"setup: the price was spent");
                if(session.Message!=Bought)failures.Add("a bought unit, with one handed away mid-buy, says \""+session.Message+"\", not \""+Bought+"\"");
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{session.Leave();UnityEngine.Object.Destroy(hud);UnityEngine.Object.Destroy(world);}
        }
    }
}
