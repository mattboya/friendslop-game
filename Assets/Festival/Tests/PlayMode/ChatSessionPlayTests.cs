using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    public sealed class ChatSessionPlayTests:FestivalPlayModeTest
    {
        [UnityTest] public IEnumerator AJoinedClientCanEndOnlyItsOwnChatCheck()
        {
            var world=new GameObject("Chat session world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Chat session host");var host=hostObject.AddComponent<FestivalSession>();
            var clientObject=new GameObject("Chat session client");var client=clientObject.AddComponent<FestivalSession>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Host",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);client.Join("Client","127.0.0.1",port);
                while(client.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(client.LocalPlayer,Is.Not.Null,client.Message);
                var sim=Simulation(host);var player=sim.Player(client.LocalPlayerId);player.X=0;player.Z=19;client.Command("Ready");
                while(!player.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(player.Ready,Is.True,"client's Ready reached the authority");yield return StartLevel(host,client.LocalPlayerId);
                sim.State.TripperId=player.Id;sim.State.Npcs.Clear();sim.State.Visions.Clear();player.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                player.X=0;player.Z=0;sim.State.Npcs.Add(new NpcState{Id="check_target",Role="Regular",CanTalk=true,Z=1,Yaw=180});
                sim.State.Visions.Add(new VisionState{Id="private_vision",NpcId="check_target",Kind="Buyer",IsTrue=false});
                client.MenuOpen=false;host.MenuOpen=false;
                client.Command("ConfirmChat","check_target");deadline=Time.realtimeSinceStartup+10;
                while(player.InteractionId==""&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(player.InteractionId,Is.Not.Empty,"client started its check: "+client.Message);var chat=sim.Interaction(player.InteractionId);
                sim.Tick(60);yield return null;
                Assert.That(chat.Status,Is.EqualTo("Active"),"the check remains active after 60 simulation seconds");
                host.Command("EndChat",chat.Id);yield return null;
                Assert.That(chat.Status,Is.EqualTo("Active"),"the host's player cannot end someone else's check");
                client.Command("EndChat",chat.Id);deadline=Time.realtimeSinceStartup+10;
                while((chat.Status=="Active"||client.State.Visions.Find(v=>v.Id=="private_vision")?.Confirmed!=true)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(chat.Status,Is.EqualTo("Complete"),"the client's command ends its check: "+client.Message);
                Assert.That(client.State.Visions.Find(v=>v.Id=="private_vision")?.Confirmed,Is.True,"the owner receives its verdict");
                Assert.That(host.State.Visions,Is.Empty,"a joined player's truth stays private");
                Assert.That(client.MenuOpen,Is.False,"ending via command leaves the menu alone");
                client.Command("EndChat",chat.Id);yield return null;
                Assert.That(chat.Status,Is.EqualTo("Complete"),"a stale command cannot run completion again");
            }
            finally{client.Leave();host.Leave();Object.Destroy(clientObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }

        // The host's refusal of the stale EndChat arrives after the snapshot with the sale's result, and the notice line is the
        // only place that tells where a handed-back unit went.
        [UnityTest] public IEnumerator EscJustAfterTheLastReplyKeepsTheSaleResult()
        {
            var world=new GameObject("Esc world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Esc host");var host=hostObject.AddComponent<FestivalSession>();
            var clientObject=new GameObject("Esc client");var client=clientObject.AddComponent<FestivalSession>();clientObject.AddComponent<FestivalHud>();
            typeof(FestivalSession).GetProperty("Profile").SetValue(host,new LocalProfile("esc-host-"+System.Guid.NewGuid().ToString("N")));
            typeof(FestivalSession).GetProperty("Profile").SetValue(client,new LocalProfile("esc-client-"+System.Guid.NewGuid().ToString("N")));
            yield return null;
            string Read(string name){foreach(var t in clientObject.GetComponentsInChildren<Text>(false))if(t.name==name)return t.text;return "(hidden)";}
            var next=typeof(FestivalSession).GetField("nextSnapshot",BindingFlags.NonPublic|BindingFlags.Instance);
            // Each refusal of the stale EndChat, with the message the session shows once it has handled it.
            var refused=new List<string>();System.Action<CommandResult> heard=r=>{if(!r.Accepted&&r.Reason=="No active chat to end")refused.Add(client.Message);};
            var tries=new List<string>();string detail="";bool pressed=false;
            try
            {
                ushort port=FreePort();host.Host("Host",port);float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);client.Join("Seller","127.0.0.1",port);
                while(client.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(client.LocalPlayer,Is.Not.Null,client.Message);
                var game=Simulation(host);var owner=game.Player(client.LocalPlayerId);
                owner.X=0;owner.Z=19;client.Command("Ready");
                while(!owner.Ready&&Time.realtimeSinceStartup<deadline)yield return null;yield return StartLevel(host,owner.Id);
                game.State.Npcs.Clear();game.HasLineOfSight=(ax,az,bx,bz)=>false;owner.X=0;owner.Z=0;owner.Effects.Clear();host.LocalPlayer.X=30;host.LocalPlayer.Z=30;
                host.MenuOpen=client.MenuOpen=false;game.State.ElapsedSeconds=FestivalHudText.BriefingSeconds+1;
                client.LocalCommandResultReceived+=heard;
                string Buyer(string stem){string id=stem+"0";for(int k=1;DialogueGrammar.PersonaFor(id,"")=="Raver"||DialogueGrammar.PersonaFor(id,"")=="Burner";k++)id=stem+k;return id;}
                // The press must land between the host's broadcast of the result and the client's receipt of it; a try that misses
                // that frame sells again.
                for(int attempt=0;attempt<6&&!pressed;attempt++)
                {
                    string buyer=Buyer("esc_buyer_"+attempt+"_");
                    game.State.Npcs.RemoveAll(n=>n.Role=="Buyer");game.State.Npcs.Add(new NpcState{Id=buyer,Role="Buyer",X=0,Z=1.5f,Yaw=180});owner.X=0;owner.Z=0;
                    owner.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");owner.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                    yield return new WaitForSeconds(6f);
                    client.Command("StartSale",target:buyer,item:"stock_lsd");deadline=Time.realtimeSinceStartup+20;
                    while(owner.InteractionId==""&&Time.realtimeSinceStartup<deadline)yield return null;
                    var talk=game.Interaction(owner.InteractionId);Assert.That(talk!=null&&talk.Kind=="Sale",Is.True,"setup: the sale starts ("+client.Message+")");
                    for(int turn=0;turn<FestivalSimulation.ConversationTurns&&talk.Status=="Active";turn++)
                    {
                        InteractionState visible=null;
                        while(Time.realtimeSinceStartup<deadline){visible=client.State.Interactions.Find(i=>i.Id==talk.Id);if(visible!=null&&visible.Turns.Count==turn+1)break;yield return null;}
                        Assert.That(visible?.Turns.Count,Is.EqualTo(turn+1),"setup: turn "+(turn+1)+" reaches the client");
                        yield return new WaitForSeconds(.2f);
                        client.Command("Reply",amount:System.Array.IndexOf(talk.Turns[turn].Grades,ConversationLines.Smooth),direction:turn);
                        if(turn<FestivalSimulation.ConversationTurns-1){while(talk.Status=="Active"&&talk.Turns.Count==turn+1&&Time.realtimeSinceStartup<deadline)yield return null;continue;}
                        // The last reply: wait for the frame the host has settled and has just broadcast, while the client still shows the talk.
                        double before=(double)next.GetValue(host);
                        while(Time.realtimeSinceStartup<deadline)
                        {
                            double now=(double)next.GetValue(host);bool broadcast=now!=before;before=now;
                            bool liveOnClient=client.State.Interactions.Exists(i=>i.Id==talk.Id&&i.Status=="Active")&&client.LocalPlayer.InteractionId==talk.Id;
                            if(talk.Status!="Active"&&broadcast&&liveOnClient){client.ConsumeMenuPress?.Invoke();pressed=true;break;}
                            if(talk.Status!="Active"&&!liveOnClient)break;
                            yield return null;
                        }
                        if(!pressed){tries.Add("attempt "+attempt+": missed the window");break;}
                        var seen=new List<string>();var at=new List<float>();float start=Time.realtimeSinceStartup;string stamp="";
                        for(float end=Time.realtimeSinceStartup+2.5f;Time.realtimeSinceStartup<end;){if(Read("Sale result")!="(hidden)")stamp=Read("Sale result");string shown=Read("Notice");if(seen.Count==0||seen[seen.Count-1]!=shown){seen.Add(shown);at.Add(Time.realtimeSinceStartup-start);}yield return null;}
                        var log=new List<string>();for(int i=0;i<seen.Count;i++)log.Add(at[i].ToString("0.00")+"s "+seen[i]);
                        detail=string.Join(" -> ",log)+" | message \""+client.Message+"\" | stamp \""+stamp+"\"";tries.Add("attempt "+attempt+": "+detail);
                        Assert.That(seen.Count>0&&seen[seen.Count-1].StartsWith("SOLD"),Is.True,"the SOLD notice stays after Esc: "+detail);
                    }
                }
                Assert.That(pressed,Is.True,"setup: Esc lands while the client still shows the settled talk:\n"+string.Join("\n",tries));
                Assert.That(refused.Count,Is.GreaterThan(0),"setup: the host refuses the stale EndChat: "+detail);
                Assert.That(refused,Has.None.EqualTo("No active chat to end"),"the refusal leaves the sale's result as the message: "+detail);
            }
            finally{client.LocalCommandResultReceived-=heard;client.Leave();host.Leave();Object.Destroy(clientObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }
        // A held G for the trust dance a joined seller's lagging view still shows can reach the host after the dance became its talk.
        // The host refuses it and the talk goes on at no ask cost; so does the menu's CANCEL ACTION, which reads the same view.
        [UnityTest] public IEnumerator AJoinedSellersLateHeldGLeavesTheTalkOn()
        {
            UseAVirtualKeyboard("Late stop keyboard");
            var world=new GameObject("Late stop world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Late stop host");var host=hostObject.AddComponent<FestivalSession>();
            var clientObject=new GameObject("Late stop client");var client=clientObject.AddComponent<FestivalSession>();clientObject.AddComponent<FestivalHud>();
            typeof(FestivalSession).GetProperty("Profile").SetValue(host,new LocalProfile("late-stop-host-"+System.Guid.NewGuid().ToString("N")));
            typeof(FestivalSession).GetProperty("Profile").SetValue(client,new LocalProfile("late-stop-client-"+System.Guid.NewGuid().ToString("N")));
            yield return null;
            var sends=typeof(FestivalSession).GetField("nextSnapshot",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                ushort port=FreePort();host.Host("Host",port);float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);client.Join("Seller","127.0.0.1",port);
                while(client.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(client.LocalPlayer,Is.Not.Null,client.Message);
                var game=Simulation(host);var seller=game.Player(client.LocalPlayerId);
                seller.X=0;seller.Z=19;client.Command("Ready");
                while(!seller.Ready&&Time.realtimeSinceStartup<deadline)yield return null;yield return StartLevel(host,seller.Id);
                game.State.Npcs.Clear();seller.X=0;seller.Z=0;seller.Effects.Clear();host.LocalPlayer.X=30;host.LocalPlayer.Z=30;
                host.MenuOpen=client.MenuOpen=false;game.State.ElapsedSeconds=FestivalHudText.BriefingSeconds+1;
                string buyerId="late_stop_0";for(int k=1;DialogueGrammar.PersonaFor(buyerId,"")!="Wook";k++)buyerId="late_stop_"+k;
                var buyer=new NpcState{Id=buyerId,Kind="Wook",Role="Buyer",X=0,Z=1.5f,Yaw=180,CanTalk=true};buyer.Observers.Add(new ObserverState{PlayerId=seller.Id,Suspicion=FestivalSimulation.TrustDanceSuspicion});game.State.Npcs.Add(buyer);
                seller.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");seller.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});seller.EquippedItemId="stock_lsd";
                Assert.That(game.Execute(seller.Id,new GameCommand{Id="late-stop-sale",Kind="StartSale",TargetId=buyer.Id,ItemId="stock_lsd",Amount=FestivalSimulation.GreedyAsk}).Accepted,Is.True,"setup: a Greedy sale starts");
                var talk=game.Interaction(seller.InteractionId);Assert.That(FestivalSimulation.TrustDancing(talk),Is.True,"setup: the wary buyer asks for a dance first");
                deadline=Time.realtimeSinceStartup+10;
                while(!(client.State.Interactions.Find(i=>i.Id==talk.Id) is InteractionState shown&&FestivalSimulation.TrustDancing(shown))&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(FestivalHudText.StopsByHold(client.State,client.LocalPlayer),Is.True,"setup: the seller's view shows the dance");

                // The seller's view stops here, still on the dance, while the host's dance becomes the talk.
                sends.SetValue(host,Time.realtimeSinceStartupAsDouble+1000);
                for(int guard=0;FestivalSimulation.TrustDancing(talk)&&guard<400;guard++)game.Tick(.05);
                Assert.That(talk.Status=="Active"&&talk.Turns.Count==1,Is.True,"setup: the host's talk is at its first turn ("+talk.Status+")");
                double heat=buyer.Observers[0].Suspicion;
                bool Refused(int after)=>game.State.Commands.Skip(after).Any(c=>c.PlayerId==seller.Id&&c.Result.Reason=="That action already ended");
                int sent=game.State.Commands.Count;
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(Key.G));yield return new WaitForSeconds(FestivalHudText.HoldToStopSeconds+.25f);
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());
                for(deadline=Time.realtimeSinceStartup+3;talk.Status=="Active"&&!Refused(sent)&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(talk.Status,Is.EqualTo("Active"),"the late held G leaves the talk on: "+client.Message);
                Assert.That(Refused(sent),Is.True,"the host refused the stop from the dance");
                Assert.That(buyer.Observers[0].Suspicion==heat&&!buyer.Observers[0].RefusesSales,Is.True,"no walk-off: the buyer stays at "+heat+" and still buys, got "+buyer.Observers[0].Suspicion);
                sent=game.State.Commands.Count;
                clientObject.GetComponentsInChildren<Button>(true).First(b=>b.name=="CANCEL ACTION").onClick.Invoke();
                for(deadline=Time.realtimeSinceStartup+3;talk.Status=="Active"&&!Refused(sent)&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(talk.Status,Is.EqualTo("Active"),"nor does the menu's CANCEL ACTION, which reads the same view");
                Assert.That(Refused(sent),Is.True,"the host refused the menu's stop");
                Assert.That(buyer.Observers[0].Suspicion==heat&&!buyer.Observers[0].RefusesSales,Is.True,"no walk-off from the menu either: the buyer stays at "+heat+", got "+buyer.Observers[0].Suspicion);

                // Once the view catches up, the talk is on the chat window.
                sends.SetValue(host,0d);
                for(deadline=Time.realtimeSinceStartup+3;FestivalChatWindow.LiveChat(client.State,client.LocalPlayer)==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(FestivalChatWindow.LiveChat(client.State,client.LocalPlayer)?.Id,Is.EqualTo(talk.Id),"the seller's view catches up to the talk");
            }
            finally{client.Leave();host.Leave();Object.Destroy(clientObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }
    }
}
