using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    public sealed class HudTripperPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator HostInputExplicitlyCancelsAndFinishesUntimedTalks(){yield return UntimedTalkInput(false);}
        [UnityTest]public IEnumerator JoinedInputExplicitlyCancelsAndFinishesUntimedTalks(){yield return UntimedTalkInput(true);}

        IEnumerator UntimedTalkInput(bool joined)
        {
            UseAVirtualKeyboard("impl-U54 untimed keyboard");
            var world=new GameObject("impl-U54 untimed world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("impl-U54 untimed host");var host=hostObject.AddComponent<FestivalSession>();
            var ownerObject=joined?new GameObject("impl-U54 untimed joined"):hostObject;var owner=joined?ownerObject.AddComponent<FestivalSession>():host;ownerObject.AddComponent<FestivalHud>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Talk host",port);yield return null;
                if(joined){host.Controls.Map.Disable();owner.Join("Talk owner","127.0.0.1",port);}
                for(float deadline=Time.realtimeSinceStartup+15;owner.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(owner.LocalPlayer,Is.Not.Null,owner.Message);owner.Profile.Data.ReducedMotion=true;
                var game=Simulation(host);var player=game.Player(owner.LocalPlayerId);
                if(joined){player.X=0;player.Z=19;owner.Command("Ready");yield return new WaitForSeconds(.3f);yield return StartLevel(host,owner.LocalPlayerId);}else yield return StartLevel(host);
                game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);game.HasLineOfSight=(x,z,tx,tz)=>true;game.State.Npcs.Clear();game.State.Visions.Clear();game.State.Doses.Clear();
                player.Effects.Clear();player.X=16.3f;player.Z=16;
                const string id="escape_talker2";Assert.That(DialogueGrammar.PersonaFor(id,""),Is.EqualTo("Wook"),"legitimate safe persona retains normal talks rather than a trust dance");
                var npc=new NpcState{Id=id,Kind="Wook",Role="Regular",CanTalk=true,X=16,Z=16};game.State.Npcs.Add(npc);
                var window=ownerObject.GetComponentInChildren<FestivalChatWindow>(true);
                foreach(string kind in new[]{"Conversation","Sale","Buy"})foreach(bool finish in new[]{false,true})
                {
                    npc.Role=kind=="Sale"?"Buyer":"Regular";npc.StockSold=0;npc.Observers.Clear();npc.Mode="Blending";npc.Suspicion=0;player.Cash=50;player.Inventory.Clear();player.EquippedItemId="";
                    if(kind=="Sale"){player.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});player.EquippedItemId="stock_mushrooms";}
                    yield return new WaitForSeconds(.4f);
                    yield return Tap(kind=="Conversation"?Key.F:Key.E,.25f);
                    for(float deadline=Time.realtimeSinceStartup+5;player.InteractionId==""&&Time.realtimeSinceStartup<deadline;)yield return null;
                    var talk=game.Interaction(player.InteractionId);Assert.That(talk?.Kind,Is.EqualTo(kind),"actual bound input starts the intended talk: "+owner.Message);
                    yield return new WaitForSeconds(.4f);Assert.That(window.Open,Is.True);Assert.That(window.transform.Find("Time left track").gameObject.activeSelf,Is.False,"ordinary talks show no answer deadline");
                    Assert.That(talk.DurationSeconds,Is.Zero,"the authoritative talk is untimed");
                    if(!finish)
                    {
                        if(kind=="Conversation")yield return new WaitForSeconds(13.2f);
                        Assert.That(talk.Status,Is.EqualTo("Active"),"silence never settles the untimed talk");
                        yield return Tap(Key.E,.15f);yield return Tap(Key.G,.15f);Assert.That(talk.Status,Is.EqualTo("Active"),"E and G leave the owned live talk open");
                        owner.MenuOpen=true;yield return null;yield return null;
                        if(!ownerObject.GetComponentsInChildren<Button>(false).Any(b=>b.name.StartsWith("Action:"))){var crew=ownerObject.GetComponentsInChildren<Button>(false).Single(b=>b.name=="CREW + NEARBY");crew.onClick.Invoke();yield return null;yield return null;}
                        var listed=ownerObject.GetComponentsInChildren<Button>(false).Where(b=>b.name.StartsWith("Action:")).Select(b=>b.name).ToArray();
                        Assert.That(listed,Is.EqualTo(new[]{"Action:Cancel current action"}),"active talks retain only their exit in the nearby list");owner.MenuOpen=false;yield return null;
                        yield return Tap(Key.Escape,.25f);yield return new WaitForSeconds(.3f);
                        Assert.That(talk.Status,Is.EqualTo("Cancelled"),"actual effective Menu explicitly cancels on authority");Assert.That(owner.MenuOpen||window.Open,Is.False,"the same press dismisses before opening the menu");
                        if(kind=="Sale")Assert.That(player.Inventory.Find(i=>i.ItemId=="stock_mushrooms")?.Count,Is.EqualTo(1),"cancel returns the last reserved unit");
                        if(kind=="Buy")Assert.That(player.Cash,Is.EqualTo(50),"cancel returns held payment");
                    }
                    else
                    {
                        for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
                        {
                            Assert.That(talk.Turns.Count,Is.EqualTo(turn+1));int choice=System.Array.IndexOf(talk.Turns[turn].Grades,ConversationLines.Smooth);Assert.That(choice,Is.InRange(0,2));
                            Key[] replies={Key.Digit1,Key.Digit2,Key.Digit3};yield return Tap(replies[choice],.2f);yield return new WaitForSeconds(.3f);
                            Assert.That(talk.Turns[turn].Pick,Is.EqualTo(choice),"the actual reply key reaches this exact owned turn");
                        }
                        Assert.That(talk.Status,Is.EqualTo("Complete"),"only the last explicit answer completes the talk");Assert.That(player.InteractionId,Is.Empty);
                        if(kind=="Sale")Assert.That(player.Inventory.Exists(i=>i.ItemId=="stock_mushrooms"),Is.False,"finished sale consumes its reserved last unit");
                        if(kind=="Buy")Assert.That(player.Cash,Is.EqualTo(45));
                        yield return Tap(Key.Escape,.25f);yield return new WaitForSeconds(.3f);Assert.That(owner.MenuOpen||window.Open,Is.False,"the first effective Menu dismisses the completed outcome");
                    }
                }
            }
            finally{owner.Leave();if(joined){host.Leave();Object.Destroy(ownerObject);}Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator PrivateOwnVoiceConsumesHiddenGlyphsAndStopsForASpirit()
        {
            var world=new GameObject("Private voice world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Private voice HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            IDictionary Cache()=>(IDictionary)typeof(FestivalBabble).GetField("speakers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session.SpeechBubbles.Babble);
            try
            {
                session.Host("Owner",HostPort);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);var npc=new NpcState{Id="private_voice_npc",Kind="Wook",Role="Buyer",CanTalk=true,X=18,Z=18};
                sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();sim.State.Visions.Add(new VisionState{Id="private_voice_vision",Kind="Buyer",NpcId=npc.Id});
                player.X=18.6f;player.Z=18;sim.State.TripperId=player.Id;player.Effects.Clear();player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                session.Command("ConfirmChat",npc.Id);var check=sim.Interaction(player.InteractionId);Assert.That(check,Is.Not.Null,session.Message);
                yield return null;yield return null;
                var window=hud.GetComponentInChildren<FestivalChatWindow>(true);session.MenuOpen=true;yield return null;yield return null;
                Assert.That(window.Pick(0),Is.True);yield return new WaitForSeconds(check.Chat.Questions[0].Length/30f+.3f);
                Assert.That(Cache().Contains(player.Id),Is.False,"hidden own-head speech never creates a voice cache");
                Assert.That(SpeechTexts(hud),Is.Empty,"menus suppress the local private NPC bubbles too");
                session.MenuOpen=false;yield return null;yield return null;
                Assert.That(Cache().Contains(player.Id),Is.False,"reopening after a fully revealed question never plays its backlog");
                Assert.That(window.Pick(1),Is.True);yield return new WaitForSeconds(.25f);
                Assert.That(Cache().Contains(player.Id),Is.True,"continuously visible newly revealed own-head question glyphs use the current Babble engine");
                Assert.That(SpeechTexts(hud).Any(t=>t.text==check.Chat.Questions[1]),Is.False,"own-head speech still has no world bubble");
                player.Life="Spirit";yield return null;yield return null;
                Assert.That(session.SpeechDeliveries.Deliveries.Where(d=>!d.IsPublic).All(d=>d.Retracted&&d.Text==""),Is.True,"a spirit retires all private check literals immediately");
                Assert.That(SpeechTexts(hud),Is.Empty);
                Assert.That(Cache().Contains(player.Id),Is.False,"private generated voice resources retire with the check owner");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator TheDoormanSpeaksOnlyThroughTheOwnedLocalCheckAdapter()
        {
            var world=new GameObject("Doorman speech world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Doorman speech HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Owner",HostPort);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);var sim=Simulation(session);sim.AddPlayer("doorman_friend","Friend").Ready=true;yield return StartLevel(session);
                var player=sim.Player(session.LocalPlayerId);var npc=new NpcState{Id="private_doorman",Kind="Wook",Twist=FestivalSimulation.VipGuard,Role="Regular",CanTalk=true,X=18,Z=18};
                sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();player.X=18.6f;player.Z=18;player.Effects.Clear();
                session.Command("ConfirmChat",npc.Id);var check=sim.Interaction(player.InteractionId);Assert.That(check,Is.Not.Null,session.Message);Assert.That(check.Chat.Questions,Is.Empty);Assert.That(check.Chat.Doorman,Is.Not.Empty);
                yield return null;yield return null;
                var shown=SpeechTexts(hud);Assert.That(shown.Select(t=>t.text),Is.EquivalentTo(new[]{check.Chat.Opener,check.Chat.Doorman}));
                Assert.That(session.SpeechDeliveries.Deliveries.Where(d=>!d.IsPublic).Select(d=>d.Speaker),Is.All.EqualTo("VIP GUARD"));
                string friendWire=JsonUtility.ToJson(FestivalSession.ViewFor(sim,"doorman_friend"));
                Assert.That(friendWire,Does.Not.Contain(check.Chat.Opener).And.Not.Contain(check.Chat.Doorman));Assert.That(sim.State.PublicSpeech,Is.Empty);
                session.Command("EndChat",check.Id);yield return null;yield return null;
                Assert.That(SpeechTexts(hud),Is.Empty);Assert.That(hud.GetComponentInChildren<FestivalChatWindow>(true).Open,Is.True);
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator AJoinedOwnerCanSelectPrivateAnswersAfterFreshSnapshots()
        {
            var world=new GameObject("Joined private world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Private friend HUD");var host=hostObject.AddComponent<FestivalSession>();hostObject.AddComponent<FestivalHud>();
            var ownerObject=new GameObject("Private owner HUD");var owner=ownerObject.AddComponent<FestivalSession>();ownerObject.AddComponent<FestivalHud>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Friend",port);yield return null;owner.Join("Owner","127.0.0.1",port);
                float deadline=Time.realtimeSinceStartup+15;while(owner.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(owner.LocalPlayer,Is.Not.Null,owner.Message);var joinedPlayer=Simulation(host).Player(owner.LocalPlayerId);joinedPlayer.X=0;joinedPlayer.Z=19;owner.Command("Ready");yield return new WaitForSeconds(.2f);yield return StartLevel(host,owner.LocalPlayerId);
                var sim=Simulation(host);var player=sim.Player(owner.LocalPlayerId);var npc=new NpcState{Id="joined_private_npc",Kind="Wook",Role="Buyer",CanTalk=true,X=18,Z=18};
                sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();sim.State.Visions.Add(new VisionState{Id="joined_private_vision",Kind="Buyer",NpcId=npc.Id});
                sim.State.TripperId=player.Id;player.X=18.6f;player.Z=18;player.Effects.Clear();player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                yield return new WaitForSeconds(.3f);owner.Command("ConfirmChat",npc.Id);
                deadline=Time.realtimeSinceStartup+5;while(owner.LocalPlayer.InteractionId==""&&Time.realtimeSinceStartup<deadline)yield return null;
                var check=FestivalChatWindow.LiveChat(owner.State,owner.LocalPlayer);Assert.That(check,Is.Not.Null,owner.Message);
                yield return new WaitForSeconds(.7f);
                Assert.That(SpeechTexts(ownerObject).Any(t=>t.text==check.Chat.Opener),Is.True);
                var window=ownerObject.GetComponentInChildren<FestivalChatWindow>(true);Assert.That(window.Pick(1),Is.True);
                yield return null;yield return null;
                Assert.That(SpeechTexts(ownerObject).Any(t=>t.text==check.Chat.Answers[1]),Is.True,"a UI closure from an earlier snapshot resolves the current owned check by stable identity");
                Assert.That(SpeechTexts(hostObject),Is.Empty,"the real joined friend's presenter never receives private text");
                Assert.That(host.SpeechDeliveries.Deliveries.Any(d=>!d.IsPublic),Is.False);
                Assert.That(sim.State.PublicSpeech,Is.Empty);Assert.That(host.State.Interactions,Is.Empty);
                var accepted=owner.SpeechDeliveries.Deliveries.Single(d=>d.Text==check.Chat.Answers[1]);
                yield return new WaitForSeconds(.3f);
                Assert.That(owner.SpeechDeliveries.Find(accepted.Id),Is.SameAs(accepted),"retransmitted owner snapshots never reset delivery identity or typing");
                owner.Command("EndChat",owner.LocalPlayer.InteractionId);yield return new WaitForSeconds(.3f);
                Assert.That(SpeechTexts(ownerObject),Is.Empty);
                Assert.That(accepted.Retracted,Is.True);
            }
            finally{owner.Leave();host.Leave();Object.Destroy(ownerObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator PrivateCheckSpeechUsesTheOwnerTrackerAndRetiresAtCheckEnd()
        {
            var world=new GameObject("Private speech world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Private speech HUD");var session=hud.AddComponent<FestivalSession>();var screen=hud.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Owner",HostPort);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);sim.AddPlayer("private_speech_friend","Friend").Ready=true;
                yield return StartLevel(session);
                var npc=new NpcState{Id="private_speech_npc",Kind="Wook",Role="Buyer",CanTalk=true,X=18,Z=18};
                sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();sim.State.Visions.Add(new VisionState{Id="private_speech_vision",Kind="Buyer",NpcId=npc.Id});
                player.X=18.6f;player.Z=18;sim.State.TripperId=player.Id;player.Effects.Clear();player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                session.Command("ConfirmChat",npc.Id);var check=sim.Interaction(player.InteractionId);
                Assert.That(check,Is.Not.Null,session.Message);Assert.That(check.Chat.Questions,Has.Length.EqualTo(3));
                yield return null;yield return null;
                var opener=SpeechTexts(hud).SingleOrDefault(t=>t.text==check.Chat.Opener);
                Assert.That(opener,Is.Not.Null,"the actual owned check opener renders over its festivalgoer");
                var delivery=session.SpeechDeliveries.Deliveries.Single(d=>!d.IsPublic&&d.Text==check.Chat.Opener);
                Assert.That(delivery.SpeakerId,Is.EqualTo(npc.Id));
                session.SpeechBubbles.Apply(session.State,id=>session.WorldCharacter(id)?.transform,session.ViewCamera.transform,session.EyePosition,session.LocalPlayerId,false,session.ConnectionGeneration,false,session.EstimatedWorldSeconds,session.Profile.Data);
                Assert.That(opener.maxVisibleCharacters,Is.EqualTo(delivery.VisibleCharacters));
                var window=hud.GetComponentInChildren<FestivalChatWindow>(true);Assert.That(window.Pick(0),Is.True);
                yield return null;yield return null;
                Assert.That(SpeechTexts(hud).Any(t=>t.text==check.Chat.Answers[0]),Is.True,"a selected answer is said by the checked NPC");
                Assert.That(SpeechTexts(hud).Any(t=>t.text==check.Chat.Questions[0]),Is.False,"your own head has no bubble");
                Assert.That(session.SpeechDeliveries.Deliveries.Any(d=>!d.IsPublic&&d.SpeakerId==player.Id&&d.Text==check.Chat.Questions[0]),Is.True,"own question uses the same local tracker");
                var friend=FestivalSession.ViewFor(sim,"private_speech_friend");string wire=JsonUtility.ToJson(friend);
                Assert.That(friend.PublicSpeech,Is.Empty);Assert.That(friend.Interactions,Is.Empty);Assert.That(wire,Does.Not.Contain(check.Chat.Opener).And.Not.Contain(check.Chat.Answers[0]));
                session.enabled=false;yield return null;yield return null;
                Assert.That(window.Pick(2),Is.True,"the stale visible thread exercises its existing click route during a disabled session");
                yield return null;yield return null;
                Assert.That(session.SpeechDeliveries.Deliveries,Is.Empty,"a disabled real session cannot admit new private presentation deliveries from stale reply buttons");
                session.enabled=true;yield return null;yield return null;
                Assert.That(SpeechTexts(hud),Is.Empty,"reenabling the session cannot replay the disabled selection or earlier check literals");
                session.Command("EndChat",check.Id);yield return null;yield return null;
                Assert.That(SpeechTexts(hud),Is.Empty,"private world text retires while the terminal chat remains readable");
                Assert.That(window.Open&&window.Lingering,Is.True);
                Assert.That(delivery.Retracted,Is.True,"lifecycle erases the private literal rather than waiting for world expiry");
                screen.enabled=false;yield return null;screen.enabled=true;yield return null;yield return null;
                Assert.That(SpeechTexts(hud),Is.Empty,"reenabling HUD never replays the closed check");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator CoalescedTalkAndDanceKeepTheFirstNpcLiteralUntilRead()
        {
            var world=new GameObject("Ordered speech world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Ordered speech HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            Text Caption(string name)=>hud.GetComponentsInChildren<Text>(false).SingleOrDefault(t=>t.name==name);
            try
            {
                session.Host("Owner",HostPort);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);var npc=new NpcState{Id="ordered_speech_npc",Kind="Wook",Role="Regular",CanTalk=true,X=20,Z=20,LastTalkSeconds=-10};
                sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();player.X=20.6f;player.Z=20;player.Effects.Clear();player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                session.Command("Talk",npc.Id);string first=player.NpcSpeech;string firstId=player.SpeechDeliveryId;
                session.Command("Dance",npc.Id);var dance=sim.Interaction(player.InteractionId);Assert.That(dance,Is.Not.Null,session.Message);dance.DurationSeconds=60;
                session.Profile.Data.SeenDialogue.RemoveAll(id=>id==dance.DialogueId);
                yield return new WaitForSeconds(2);
                Assert.That(Caption("Speaker").text,Is.EqualTo("FESTIVALGOER"));
                Assert.That(Caption("Dialogue line").text,Is.EqualTo(first),"the first accepted NPC delivery survives a later player delivery in the same snapshot");
                Assert.That(session.SpeechDeliveries.Find(firstId).Text,Is.EqualTo(first));
                Assert.That(session.Profile.Data.SeenDialogue,Does.Not.Contain(dance.DialogueId),"a challenge behind unread speech has not been displayed");
                yield return new WaitForSeconds(FestivalTalkLine.ReadFor(first)-2+.4f);
                Assert.That(Caption("Dialogue line").text,Is.EqualTo(dance.DialogueText));
                Assert.That(session.Profile.Data.SeenDialogue,Does.Contain(dance.DialogueId));
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator AChallengeRevealsItsFullLiteralOnTheSharedThirtyGlyphClock()
        {
            var world=new GameObject("Challenge reveal world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Challenge reveal HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Owner",HostPort);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);var npc=new NpcState{Id="typed_speech_npc",Kind="Wook",Role="Regular",CanTalk=true,X=22,Z=22};
                sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();player.X=22.6f;player.Z=22;player.Effects.Clear();
                session.Command("Dance",npc.Id);var dance=sim.Interaction(player.InteractionId);Assert.That(dance,Is.Not.Null,session.Message);dance.DurationSeconds=60;
                yield return null;yield return null;
                var line=hud.GetComponentsInChildren<Text>(false).Single(t=>t.name=="Dialogue line");Canvas.ForceUpdateCanvases();
                var mesh=line.canvasRenderer.GetMesh();var colours=mesh.colors32;
                Assert.That(line.text,Is.EqualTo(dance.DialogueText),"full literal remains laid out while unrevealed glyphs keep their place");
                Assert.That(colours.Any(c=>c.a==0)&&colours.Any(c=>c.a>0),Is.True,"the challenge uses the shared partial reveal rather than showing every glyph immediately");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest] public IEnumerator EscapeConsumesLiveAndTerminalPrivateThreadsBeforeOpeningTheMenu()
        {
            UseAVirtualKeyboard();
            var world=new GameObject("Escape lifecycle world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Escape lifecycle HUD");var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Escape tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);session.Profile.Data.ReducedMotion=true;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);sim.AddPlayer("escape_friend","Friend").Ready=true;
                yield return StartLevel(session);
                string id="escape_talker2";Assert.That(DialogueGrammar.PersonaFor(id,""),Is.EqualTo("Wook"),"fixed safe persona fixture");
                var npc=new NpcState{Id=id,Kind="Wook",Role="Regular",CanTalk=true,X=12,Z=12};sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();
                player.X=12.3f;player.Z=12;sim.State.Doses.Clear();
                session.Command("Conversation",npc.Id);yield return null;yield return null;
                var talk=sim.Interaction(player.InteractionId);var window=hud.GetComponentInChildren<FestivalChatWindow>(true);
                Assert.That(window.Open,Is.True);
                yield return Tap(Key.Escape);
                Assert.That(talk.Status,Is.EqualTo("Cancelled"),"effective Menu must send the owned EndChat before toggling menu");
                Assert.That(session.MenuOpen||window.Open,Is.False,"the first Escape ends and dismisses");
                yield return Tap(Key.Escape);Assert.That(session.MenuOpen,Is.True,"the next press opens the menu");
                yield return Tap(Key.Escape);Assert.That(session.MenuOpen,Is.False);
                npc.Observers.Clear();session.Command("Conversation",npc.Id);yield return null;yield return null;
                talk=sim.Interaction(player.InteractionId);
                for(int t=0;t<FestivalSimulation.ConversationTurns;t++){var turn=talk.Turns[t];window.Pick(System.Array.IndexOf(turn.Grades,ConversationLines.Smooth));yield return null;yield return null;}
                yield return new WaitForSeconds(.8f);
                var outcome=player.ConversationOutcomes.Last();
                Assert.That(window.Lingering,Is.True);
                Assert.That(Enumerable.Range(0,window.Bubbles).Any(i=>window.Shown(i)==outcome.Reaction),Is.True,"the retained authoritative last-reply reaction is shown without a fifth reply");
                Assert.That(window.Replies,Is.EqualTo(0));Assert.That(talk.Turns.Count,Is.EqualTo(FestivalSimulation.ConversationTurns));
                Assert.That(player.Dialogue.Seen,Is.Not.Empty,"actually displayed turns/outcome acknowledge private grammar");
                yield return Tap(Key.G);Assert.That(window.Open,Is.True,"G no longer owns chat dismissal");
                yield return Tap(Key.Escape);Assert.That(window.Open||session.MenuOpen,Is.False);
                Assert.That(player.ConversationDismissedId,Is.EqualTo(outcome.InteractionId),"Escape dismisses authoritatively for reconnect");
                yield return Tap(Key.Escape);Assert.That(session.MenuOpen,Is.True);
                view.enabled=false;session.MenuOpen=false;yield return Tap(Key.Escape);
                Assert.That(session.MenuOpen,Is.True,"disabled HUD unregisters the Menu consumer");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;yield return null;
        }

        [UnityTest] public IEnumerator MissedActiveViewsStillShowPrivateReactionsWalkOffAndNarcReason()
        {
            UseAVirtualKeyboard();
            var world=new GameObject("Retained outcome world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Retained outcome HUD");var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Outcome tester",HostPort);float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);sim.AddPlayer("private_friend","Friend").Ready=true;
                yield return StartLevel(session);session.Profile.Data.ReducedMotion=true;
                session.Controls.Menu.ApplyBindingOverride("<Keyboard>/f10");
                string id="retained_talker";Assert.That(DialogueGrammar.PersonaFor(id,""),Is.Not.EqualTo("Raver").And.Not.EqualTo("Burner"),"fixed safe persona fixture");
                var npc=new NpcState{Id=id,Kind="Wook",Role="Regular",CanTalk=true,X=14,Z=14};sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();sim.State.Doses.Clear();
                var window=hud.GetComponentInChildren<FestivalChatWindow>(true);
                foreach(string scenario in new[]{"smooth","so-so","awkward","Sale trust","Buy trust","Conversation trust","narc"})
                {
                    view.enabled=false;player.Life="Alive";player.X=14.3f;player.Z=14;player.Cash=50;player.Inventory.Clear();player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});npc.StockSold=0;npc.Observers.Clear();npc.Role=scenario=="narc"?"Narc":scenario=="Sale trust"?"Buyer":"Regular";
                    bool trust=scenario.EndsWith("trust");if(trust)npc.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=FestivalSimulation.TrustDanceSuspicion});
                    string kind=scenario=="narc"||scenario=="Buy trust"?"Buy":scenario=="Sale trust"?"Sale":"Conversation";
                    session.Command(kind=="Sale"?"StartSale":kind=="Buy"?"StartBuy":kind,npc.Id,item:kind=="Sale"?"stock_lsd":kind=="Buy"?FestivalSimulation.SellerStock(npc.Id):"");yield return null;
                    var talk=sim.Interaction(player.InteractionId);Assert.That(talk,Is.Not.Null,scenario+": "+session.Message);
                    if(trust){Assert.That(talk.TrustDance,Is.True);for(int n=0;talk.Status=="Active"&&n<400;n++)sim.Tick(.1);}
                    else for(int t=0;t<FestivalSimulation.ConversationTurns;t++){int grade=scenario=="awkward"?ConversationLines.Awkward:scenario=="so-so"?ConversationLines.SoSo:ConversationLines.Smooth;session.Command("Reply",amount:System.Array.IndexOf(talk.Turns[t].Grades,grade),direction:t);}
                    Assert.That(player.InteractionId,Is.Empty);var outcome=player.ConversationOutcomes.Last();
                    if(trust){Assert.That(outcome.Notice,Is.EqualTo(kind=="Sale"?"They walk off  •  Tongue Stamps back in your hand":"They walk off."));Assert.That(outcome.Reaction,Is.EqualTo("Nah. Not feeling it."));}
                    if(scenario=="narc")Assert.That(player.Life,Is.EqualTo("Detained"));
                    view.enabled=true;yield return null;yield return new WaitForSeconds(.7f);
                    Assert.That(window.Open&&window.Lingering,Is.True,scenario+": a terminal delivery survives an entirely missed live view");
                    var lines=Enumerable.Range(0,window.Bubbles).Select(i=>window.Shown(i)).ToArray();
                    Assert.That(lines,Does.Contain(outcome.Reaction),scenario+": authored final reaction");
                    if(outcome.Notice!="")Assert.That(lines,Does.Contain(outcome.Notice));
                    Assert.That(window.Header,Is.EqualTo("CHAT OVER  •  F10 CLOSE"));Assert.That(window.Replies,Is.EqualTo(0));
                    Assert.That(session.Profile.Data.SeenDialogue,Is.SupersetOf(outcome.GrammarKeys),"displayed terminal grammar is remembered privately");
                    var friendView=FestivalSession.ViewFor(sim,"private_friend");string friendJson=JsonUtility.ToJson(friendView);
                    Assert.That(friendJson,Does.Not.Contain(outcome.DeliveryId));
                    if(trust)Assert.That(friendView.PublicSpeech.Any(d=>d.SpeakerId==npc.Id&&d.Text==outcome.Reaction&&!d.Retracted),Is.True,"the neutral failed-trust rejection is intentionally spoken publicly");
                    else Assert.That(friendJson,Does.Not.Contain(outcome.Reaction),"authored final grades remain private");
                    var remoteOwner=friendView.Players.Find(p=>p.Id==player.Id);Assert.That(remoteOwner.ConversationOutcomes,Is.Empty);Assert.That(remoteOwner.Dialogue.Seen,Is.Empty);
                    foreach(string key in outcome.GrammarKeys)Assert.That(friendJson,Does.Not.Contain(key),"private terminal grammar is never public speech metadata");
                    if(outcome.Notice!="")Assert.That(friendJson,Does.Not.Contain(outcome.Notice));
                    if(outcome.Hunch!="")Assert.That(friendJson,Does.Not.Contain(outcome.Hunch));
                    if(scenario=="smooth")
                    {
                        player.X=24;yield return null;yield return null;
                        Assert.That(window.Open,Is.False,"retained source identity enforces reach after a missed active snapshot");
                    }
                    else{yield return Tap(Key.F10);Assert.That(session.MenuOpen||window.Open,Is.False);}
                    Assert.That(player.ConversationDismissedId,Is.EqualTo(outcome.InteractionId));
                }
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;yield return null;
        }

        [UnityTest] public IEnumerator EffectiveMenuEndsChecksRefundsTalksAndDismissesMarigold()
        {
            UseAVirtualKeyboard();var world=new GameObject("Menu route world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Menu route HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Menu tester",HostPort);float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);player.X=0;player.Z=5;
                var window=hud.GetComponentInChildren<FestivalChatWindow>(true);yield return null;yield return null;
                yield return Tap(Key.F);Assert.That(window.Open,Is.True,"Marigold opens through the actual chat binding");
                yield return Tap(Key.G);Assert.That(window.Open,Is.True,"G leaves Marigold open");
                yield return Tap(Key.Escape);Assert.That(window.Open||session.MenuOpen,Is.False,"one Escape dismisses Marigold");
                yield return Tap(Key.Escape);Assert.That(session.MenuOpen,Is.True);yield return Tap(Key.Escape);
                sim.AddPlayer("menu_friend","Friend").Ready=true;yield return StartLevel(session);
                session.Controls.Menu.ApplyBindingOverride("<Keyboard>/f10");
                var npc=new NpcState{Id="escape_talker2",Kind="Wook",Role="Regular",CanTalk=true,X=16,Z=16,Yaw=90};sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);sim.State.Visions.Clear();
                var witness=new NpcState{Id="menu_witness",Kind="Wook",Role="Regular",X=16,Z=16.5f,Yaw=150};sim.State.Npcs.Add(witness);
                foreach(string kind in new[]{"Sale","Buy","Conversation","Police","ConfirmChat"})
                {
                    player.Life="Alive";player.X=16.3f;player.Z=16;player.Cash=50;player.Inventory.Clear();player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});npc.StockSold=0;npc.Observers.Clear();npc.Role=kind=="Sale"?"Buyer":"Regular";npc.Kind=kind=="Police"?"Cop":"Wook";
                    npc.Observers.Add(new ObserverState{PlayerId=player.Id});witness.Observers.Clear();witness.Observers.Add(new ObserverState{PlayerId=player.Id});player.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
                    VisionState vision=null;
                    if(kind=="ConfirmChat")
                    {
                        sim.State.TripperId=player.Id;player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1});
                        vision=new VisionState{Id="menu_check",Kind="Buyer",NpcId=npc.Id};sim.State.Visions.Add(vision);
                    }
                    session.Command(kind=="Sale"?"StartSale":kind=="Buy"?"StartBuy":kind,npc.Id,item:kind=="Sale"?"stock_lsd":kind=="Buy"?FestivalSimulation.SellerStock(npc.Id):"");yield return null;yield return null;
                    var action=sim.Interaction(player.InteractionId);Assert.That(action,Is.Not.Null,kind+": "+session.Message);
                    Assert.That(window.Open,Is.True);Assert.That(window.transform.Find("Time left track").gameObject.activeSelf,Is.EqualTo(kind=="Police"));
                    yield return Tap(Key.Escape);Assert.That(action.Status,Is.EqualTo("Active"),"the old physical Escape is no longer the effective binding");
                    yield return Tap(Key.F10);
                    Assert.That(action.Status,Is.EqualTo(kind=="ConfirmChat"?"Complete":"Cancelled"));
                    Assert.That(window.Open||session.MenuOpen,Is.False,kind+": the effective Menu ends and dismisses before opening the menu");
                    if(kind=="Sale")Assert.That(player.Inventory.Find(i=>i.ItemId=="stock_lsd")?.Count,Is.EqualTo(1),"reserved stock is returned");
                    if(kind=="Buy")Assert.That(player.Cash,Is.EqualTo(50),"held payment is returned");
                    if(vision!=null)Assert.That(vision.Confirmed,Is.True,"ending a check confirms it");
                    else Assert.That(witness.Observers.Find(o=>o.PlayerId==player.Id).Suspicion,Is.GreaterThanOrEqualTo(20),"walk-away witness cost stays authoritative");
                    yield return Tap(Key.F10);Assert.That(session.MenuOpen,Is.True);yield return Tap(Key.F10);
                }
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;yield return null;
        }

        // HUD-2: the running HUD puts FestivalHudText's tripper lines on screen. Hosts a real two-player Day 1, pins the spin,
        // and reads what a player sees: who trips and on how many doses, the tripper's one hint, the checks beside a festivalgoer
        // they have a vision about (E dances, F chats), the chat picker, and nothing to check for the sober friend.
        // DANCE-2: the check dance and a trust dance show the live dancer beside the lanes; the menu and the end of the challenge
        // hide it. DIALOG-3: a chat is a talk on the chat window, replied to on 1-3, standing in the talk pose; F starts one.
        // Labels are laid out on a 1920x1080 canvas, so a line its box cuts off fails.
        [UnityTest]public IEnumerator TheCrewSeesTheSpinAndOnlyTheTripperChecks()
        {
            // 1-3 are pressed on a virtual keyboard, so they go through the HUD's own input handling (FestivalHud.UpdateKeyboard).
            UseAVirtualKeyboard();
            var world=new GameObject("HUD tripper world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD tripper");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            void Fits(string where,string label)
            {
                var text=Find(label);if(text==null){failures.Add(where+": "+label+" is hidden");return;}
                Canvas.ForceUpdateCanvases();
                var drawn=text.cachedTextGenerator;int shown=drawn.characterCountVisible;
                if(shown<text.text.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" (font "+text.fontSize+") cuts \""+text.text.Replace("\n"," | ")+"\" after \""+text.text.Substring(0,shown).Replace("\n"," | ")+"\"");
            }
            // Whether each text would fit, uncut, in the box of the shown label `name`, measured as Fits measures what it shows.
            void FitsAll(string where,string name,IEnumerable<string> texts)
            {
                var label=Find(name);if(label==null){failures.Add(where+": "+name+" is hidden");return;}
                Canvas.ForceUpdateCanvases();
                var box=label.rectTransform.rect.size;var settings=label.GetGenerationSettings(box);var measure=new TextGenerator();
                foreach(var text in texts)
                {
                    measure.Populate(text,settings);
                    if(measure.characterCountVisible<text.Length)failures.Add(where+": "+name+" box "+box+" (font "+label.fontSize+") cuts \""+text+"\" after \""+text.Substring(0,measure.characterCountVisible)+"\"");
                }
            }
            // DANCE-2: the live dancer beside the lanes shows your own character dancing, under this heading and caption, and the
            // first-person hands step aside as they do for any dance.
            var preview=view.GetComponent<FestivalDancePreview>();
            var hands=typeof(FestivalSession).GetField("firstPersonHands",BindingFlags.NonPublic|BindingFlags.Instance);
            void Dancing(string where,string heading,string caption)
            {
                Expect(where,"Dancer heading",heading);Expect(where,"Dancer caption",caption);Fits(where,"Dancer heading");
                var you=session.LocalWorldCharacter;
                if(preview==null||!preview.IsVisible||you==null||preview.Dancer!=you)failures.Add(where+": the live dancer view doesn't show your character");
                else if(you.Pose!="Dance")failures.Add(where+": your character's pose is "+you.Pose+", not Dance");
                var shownHands=hands?.GetValue(session) as Component;
                if(shownHands==null||shownHands.gameObject.activeSelf)failures.Add(where+": the first-person hands "+(shownHands==null?"are missing":"still show"));
            }
            // E runs the HUD's primary action and F its chat key (FestivalHud.UpdateKeyboard); 1-3 pick a chat question.
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            var chatKey=typeof(FestivalHud).GetMethod("ChatKey",BindingFlags.NonPublic|BindingFlags.Instance);
            void PressE()=>((System.Action)primary.GetValue(view))?.Invoke();
            void PressF(){if(chatKey==null)failures.Add("the HUD has no F key handler (ChatKey)");else chatKey.Invoke(view,new object[]{session.State,session.LocalPlayer});}
            // The actions the HUD lists (NEARBY / CREW), first to last; E runs the first.
            string Actions(){var listed=new List<string>();foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Action:")&&b.gameObject.activeSelf)listed.Add(b.name.Substring(7));return string.Join(" | ",listed);}
            const string CheckDance="Check by dancing  •  F CHECK BY CHAT (8 s)",CheckChat="Check by chatting (8 s, safe)",FChats="  •  F CHAT";
            // G drops nothing while the chat window shows, even its ended transcript, so the gear bar leaves G DROP out.
            const string GearKeys="GEAR   /   1–3 EQUIP     Q USE",GearKeysInChat="GEAR   /   Q USE";
            // DIALOG-5: the chat window and its thread as drawn, oldest first: "< " their bubbles, "> " yours.
            var window=hud.GetComponentInChildren<FestivalChatWindow>(true);
            void ExpectThread(string where,params string[] lines)
            {
                if(window==null||!window.Open){failures.Add(where+": the chat window is hidden");return;}
                var shown=new List<string>();for(int i=0;i<window.Bubbles;i++)shown.Add((window.IsTheirs(i)?"< ":"> ")+window.Shown(i));
                if(!shown.SequenceEqual(lines))failures.Add(where+": the chat thread reads \""+string.Join(" | ",shown)+"\", expected \""+string.Join(" | ",lines)+"\"");
            }
            void ExpectReplies(string where,string[] questions,params bool[] open)
            {
                if(window==null||!window.Open)return;
                for(int i=0;i<3;i++)if(window.ReplyKey(i)!=(i+1).ToString()||window.ReplyLabel(i)!=questions[i]||window.CanReply(i)!=open[i])
                    failures.Add(where+": reply "+(i+1)+" reads \""+window.ReplyKey(i)+"  "+window.ReplyLabel(i)+"\" and can"+(window.CanReply(i)?"":"'t")+" be picked");
            }
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);
                var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("hud_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                // Pin the spin: who trips, on how many doses of what (the dose effect keeps the sim from re-picking).
                void Trip(PlayerState who,int dose,string substance="")
                {
                    foreach(var p in sim.State.Players)p.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
                    who.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="hud_dose_"+who.Id,Intensity=dose,Substance=substance,RemainingSeconds=600});
                    sim.State.TripperId=who.Id;sim.State.Doses.Clear();sim.State.Doses.Add(new PlayerDose{PlayerId=who.Id,Dose=dose,Substance=substance});
                }

                // You trip on two doses as Day 1 starts: the crew reads it, and you get the one hint for a while, folded into your job on
                // the level briefing (HUD-7).
                Trip(player,2);sim.State.ElapsedSeconds=0;mate.X=30;mate.Z=0;player.X=0;player.Z=0;
                yield return new WaitForSeconds(.6f);
                Expect("day 1, you trip","Tripping","YOU TRIP  •  2 DOSES");
                Expect("day 1, you trip, level start","Briefing job","You're tripping: only you can see who's buying. Trust, but verify: some visions lie, so check one with F first.");
                Fits("day 1, you trip","Tripping");Fits("day 1, you trip, level start","Briefing job");
                sim.State.ElapsedSeconds=30;
                yield return new WaitForSeconds(.6f);
                Expect("day 1, you trip, 30 s in","Briefing job","(hidden)");
                // Sam trips on three Fun Guys: you read who, on what, and the hint is not yours.
                Trip(mate,3,"mushrooms");sim.State.ElapsedSeconds=0;
                yield return new WaitForSeconds(.6f);
                Expect("day 1, Sam trips","Tripping","SAM TRIPS  •  3 DOSES  •  FUN GUYS");
                Expect("day 1, Sam trips, level start","Briefing job","Sam is tripping and can see who's buying. Stay close and act on what they call out.");

                // Beside a festivalgoer you have a vision about: E checks by dancing, F by chatting.
                Trip(player,2);sim.State.ElapsedSeconds=30;
                var seen=sim.State.Visions.Find(v=>v.NpcId!=""&&!v.Confirmed);Assert.That(seen,Is.Not.Null,"setup: the day deals visions about festivalgoers");
                var npc=sim.State.Npcs.Find(n=>n.Id==seen.NpcId);sim.State.Npcs.RemoveAll(n=>n!=npc);
                npc.X=0;npc.Z=0;npc.Mode="Blending";npc.Suspicion=0;npc.CanTalk=true;player.X=.6f;player.Z=0;
                // With gear in hand and Sam in reach (the sober crew sticks by the tripper), the checks still come first.
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");
                player.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});player.Inventory.Add(new ItemStack{ItemId="map",Count=1});player.EquippedItemId="confetti";
                mate.X=.6f;mate.Z=1.5f;
                yield return new WaitForSeconds(.6f);
                Expect("tripper beside a vision, gear in hand, Sam in reach","Prompt",CheckDance);
                // F checks here, so no festivalgoer action offers F as a chat.
                string listed=Actions();
                if(!listed.StartsWith(CheckDance+" | "+CheckChat)||listed.Contains(FChats))failures.Add("tripper beside a vision: the actions read \""+listed+"\"");
                PressE();yield return null;
                if(!sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="ConfirmDance"&&i.Status=="Active"))failures.Add("tripper beside a vision: E starts no check dance ("+session.Message+")");
                yield return new WaitForSeconds(.4f);
                Expect("check dance","Challenge title","CHECK DANCE  /  FOUR-LANE");
                // DANCE-2: the check dance opens the live dancer beside the lanes, as every four-lane challenge does, showing your
                // own character dancing; the menu hides it.
                Dancing("check dance","YOU  /  ON THE FLOOR","YOUR LOOK • YOUR MOVES");
                FitsAll("check dance","Dancer caption",DancerKinds.Select(FestivalHudText.DancerCaption));
                // DANCE-4: the dancer camera moves with the music: it sits where the camera path puts it this far into the check
                // dance at its beat, with reduced motion off and on. (Read after the HUD's Update, which places it.)
                var danceCamera=preview.transform.Find("Live festival dance camera");var onPath=new GameObject("HUD tripper dance camera path").transform;
                bool motion=session.Profile.Data.ReducedMotion;
                foreach(bool reduced in new[]{false,true})
                {
                    session.Profile.Data.ReducedMotion=reduced;yield return null;yield return null;
                    string where="check dance, reduced motion "+(reduced?"on":"off");
                    var dance=session.State.Interactions.Find(i=>i.Id==session.LocalPlayer.InteractionId&&i.Status=="Active");var you=session.LocalWorldCharacter;
                    if(dance==null||you==null||danceCamera==null){failures.Add(where+": no dance, dancer or dancer camera to check");continue;}
                    double t=session.EstimatedSimulationSeconds-dance.StartSeconds;
                    FestivalDancePreview.Frame(onPath,you.transform,false,t,dance.BeatSeconds,reduced);
                    float off=Vector3.Distance(danceCamera.position,onPath.position);
                    if(off>.05f)failures.Add(where+": the dancer camera is "+off.ToString("0.00")+" m from where the camera path puts it "+t.ToString("0.00")+" s in");
                }
                session.Profile.Data.ReducedMotion=motion;Object.Destroy(onPath.gameObject);
                session.MenuOpen=true;yield return null;yield return null;
                Expect("check dance, menu open","Dancer heading","(hidden)");
                if(preview.IsVisible)failures.Add("check dance, menu open: the live dancer view still renders");
                session.MenuOpen=false;
                session.Command("Cancel");
                // DIALOG-3: a chat is a conversation, danced only when a wary festivalgoer asks for a dance first. That trust dance
                // is danced like any other, under its own title and heading, with its ask in the dancer panel and the chat window
                // shut: at 1920x1080 every line a four-lane challenge can say fits there, as does every heading.
                npc.Observers.RemoveAll(o=>o.PlayerId==player.Id);npc.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=FestivalSimulation.TrustDanceSuspicion});
                session.Command("Conversation",npc.Id);
                if(!sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="Conversation"&&i.Status=="Active"))failures.Add("trust dance: no chat with the festivalgoer starts ("+session.Message+")");
                yield return new WaitForSeconds(.4f);
                Expect("trust dance","Challenge title","TRUST DANCE  /  FOUR-LANE");
                Dancing("trust dance","YOU  /  DANCING FOR TRUST","(hidden)");
                Expect("trust dance","Dialogue line",FestivalSimulation.TrustAsk);
                if(window!=null&&window.Open)failures.Add("trust dance: the chat window is open");
                // DIALOG-4: the lines are built from parts, so 400 of every four-lane context's lines and awkward twins on each festival.
                var lines=new List<string>{FestivalSimulation.TrustAsk};
                foreach(var context in new[]{"dance","sale","buy","suspicion","police"})for(int festival=0;festival<Festivals.Count;festival++)for(int seed=0;seed<400;seed++)
                {var line=new DialogueHistory().Select(context,festival,seed);lines.Add(line.Text);lines.Add(line.AwkwardText);}
                FitsAll("trust dance","Dialogue line",lines);
                FitsAll("trust dance","Dancer heading",DancerKinds.Select(FestivalHudText.DancerHeading));
                session.Command("Cancel");npc.Suspicion=0;npc.Observers.RemoveAll(o=>o.PlayerId==player.Id);
                // A chat with a festivalgoer who trusts you is a talk on the chat window, not the lanes: their line, your three replies,
                // and you stand talking. 2 replies with the second, as the host hears it, and equips nothing. (Holding you to the spot is
                // FestivalInput.Roots, in HudConversationTests: in this batch-mode test a held W moves nobody, talking or not.)
                player.X=npc.X+.6f;player.Z=npc.Z;player.EquippedItemId="confetti";
                session.Command("Conversation",npc.Id);
                var talk=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Kind=="Conversation"&&i.Status=="Active");
                if(talk==null)failures.Add("talk: no chat with the festivalgoer starts ("+session.Message+")");
                else
                {
                    // A raver or burner may want a dance first whoever you are (1 in 4): dance it perfectly, and the talk follows.
                    if(talk.TrustDance)foreach(var note in RhythmChart.For(talk).Notes)talk.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
                    float talking=Time.realtimeSinceStartup+15;
                    while(talk.Turns.Count==0&&talk.Status=="Active"&&Time.realtimeSinceStartup<talking)yield return null;
                    yield return new WaitForSeconds(.3f);
                    var turn=talk.Turns.Count>0?talk.Turns[talk.Turns.Count-1]:null;
                    if(turn==null||talk.Status!="Active")failures.Add("talk: no turn to reply to ("+talk.Status+")");
                    else
                    {
                        ExpectThread("talk","< "+turn.Line);
                        ExpectReplies("talk",turn.Replies,true,true,true);
                        Expect("talk","Chat header",FestivalChatWindow.TalkHeader);Fits("talk","Chat header");
                        Expect("talk","Challenge title","(hidden)");Expect("talk","Dancer heading","(hidden)");
                        Expect("talk","Equipment heading",GearKeysInChat);
                        var you=session.LocalWorldCharacter;
                        if(you==null||you.Pose!="Conversation")failures.Add("talk: your character's pose is "+(you==null?"missing":you.Pose)+", not the talk's");
                        yield return Tap(Key.Digit2);
                        if(turn.Pick!=1)failures.Add("talk: 2 didn't reply with the second reply (picked "+turn.Pick+"; "+session.Message+")");
                        if(player.EquippedItemId!="confetti")failures.Add("talk: 2 equipped a slot ("+player.EquippedItemId+")");
                        bool said=false;for(int i=0;i<window.Bubbles;i++)said|=!window.IsTheirs(i)&&window.Shown(i)==turn.Replies[1];
                        if(!said)failures.Add("talk: your reply isn't in the thread");
                    }
                    session.Command("Cancel");
                }
                npc.Observers.RemoveAll(o=>o.PlayerId==player.Id);
                player.X=npc.X+.6f;player.Z=npc.Z;
                yield return new WaitForSeconds(.6f);
                Expect("no challenge","Challenge title","(hidden)");
                Expect("no challenge","Dancer heading","(hidden)");
                if(preview.IsVisible)failures.Add("no challenge: the live dancer view still renders");
                PressF();yield return null;
                var chat=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Kind=="ConfirmChat"&&i.Status=="Active");
                if(chat==null)failures.Add("tripper beside a vision: F starts no chat check ("+session.Message+")");
                else
                {
                    // DIALOG-5: the chat window opens on their opener, the three questions as reply buttons on 1-3.
                    var said=chat.Chat;
                    yield return new WaitForSeconds(.6f);
                    ExpectThread("chat check","< "+said.Opener);
                    ExpectReplies("chat check",said.Questions,true,true,true);
                    Fits("chat check","Chat header");
                    // 1-3 ask while the chat runs, so the gear bar stops offering them; holding the map, 1 would equip the confetti.
                    Expect("chat check","Equipment heading",GearKeysInChat);
                    player.EquippedItemId="map";
                    // 1, then 3 while they still type their answer to 1: that answer shows whole, then theirs to 3 types out.
                    yield return Tap(Key.Digit1);
                    yield return Tap(Key.Digit3);
                    yield return new WaitForSeconds(FestivalChatWindow.Typed(said.Answers[2])+.2f);
                    ExpectThread("chat check, 1 then 3 asked","< "+said.Opener,"> "+said.Questions[0],"< "+said.Answers[0],"> "+said.Questions[2],"< "+said.Answers[2]);
                    ExpectReplies("chat check, 1 then 3 asked",said.Questions,false,true,false);
                    Canvas.ForceUpdateCanvases();
                    foreach(var text in window.GetComponentsInChildren<Text>(false))
                        if((text.name=="Line"||text.name=="Question")&&text.cachedTextGenerator.characterCountVisible<text.text.Length)failures.Add("chat check, 1 then 3 asked: a "+text.name+" box "+text.rectTransform.rect.size+" cuts \""+text.text+"\"");
                    if(player.EquippedItemId!="map")failures.Add("chat check, 1 then 3 asked: 1 or 3 equipped a slot ("+player.EquippedItemId+")");
                    // With the menu open the pointer is free: the window sits beside the pass, over its dimmer, and a click asks 2.
                    session.MenuOpen=true;yield return null;yield return null;
                    var pass=hud.GetComponentsInChildren<Image>(false).FirstOrDefault(i=>i.name=="Festival pass");var dimmer=hud.GetComponentsInChildren<Image>(false).FirstOrDefault(i=>i.name=="Festival pass dimmer");
                    if(window==null||!window.Open||pass==null||dimmer==null)failures.Add("chat check, menu open: the chat window or the pass is hidden");
                    else
                    {
                        Rect Box(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
                        if(Box((RectTransform)window.transform).Overlaps(Box(pass.rectTransform)))failures.Add("chat check, menu open: the chat window covers the pass");
                        if(window.transform.GetSiblingIndex()<dimmer.transform.GetSiblingIndex())failures.Add("chat check, menu open: the chat window is under the pass's dimmer");
                        window.GetComponentsInChildren<Button>().First(b=>b.name=="Reply 2").onClick.Invoke();yield return null;
                        if(window.Bubbles<2||window.Shown(window.Bubbles-2)!=said.Questions[1])failures.Add("chat check, menu open: clicking reply 2 doesn't ask question 2");
                    }
                    session.MenuOpen=false;
                    // DIALOG-6: once the chat is over, the window stays only until its last answer has been read.
                    session.Command("EndChat",player.InteractionId);
                    float gone=Time.realtimeSinceStartup+15;
                    while(window!=null&&window.Open&&Time.realtimeSinceStartup<gone)yield return null;
                    if(window==null||!window.Open)failures.Add("chat check done: the terminal transcript disappeared");
                    Expect("chat check done","Equipment heading",GearKeys);
                    // With no chat under way, 1 equips slot 1 again (and shows the keyboard reaches the HUD).
                    yield return Tap(Key.Digit1);
                    yield return new WaitForSeconds(.3f);
                    if(player.EquippedItemId!="confetti")failures.Add("chat check done: 1 did not equip slot 1 ("+player.EquippedItemId+")");
                    if(!seen.Confirmed)failures.Add("chat check done: the vision is still unchecked ("+session.Message+")");
                    if(Read("Prompt")==CheckDance)failures.Add("chat check done: a checked vision is offered again");
                }

                // Sam trips: you are sober beside a festivalgoer Sam has an unchecked vision about, and have nothing to check. F chats,
                // as their first action says (by day with no stock that is buying from them, ECON-3): a talk with them (DIALOG-3).
                mate.X=30;mate.Z=0;Trip(mate,3);sim.State.Visions.Add(new VisionState{Id="hud_vision",Kind="Buyer",NpcId=npc.Id});
                player.X=npc.X+.6f;player.Z=npc.Z;
                yield return new WaitForSeconds(.6f);
                listed=Actions();
                if(listed.Contains("Check by")||!listed.Contains(FestivalHudText.BuyAction(sim.State,player,npc.Id)+FChats))failures.Add("sober beside Sam's vision: the actions read \""+listed+"\"");
                PressF();yield return null;
                if(sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind.StartsWith("Confirm")&&i.Status=="Active"))failures.Add("sober beside Sam's vision: F starts a check");
                if(!sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="Conversation"&&i.Status=="Active"))failures.Add("sober beside Sam's vision: F starts no talk with them ("+session.Message+")");

                // You trip again and check the same festivalgoer by chat: the new chat starts with no question asked.
                session.Command("Cancel");npc.Observers.RemoveAll(o=>o.PlayerId==player.Id);Trip(player,2);player.X=npc.X+.6f;player.Z=npc.Z;
                yield return new WaitForSeconds(.6f);
                PressF();yield return null;
                var again=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Kind=="ConfirmChat"&&i.Status=="Active");
                if(again==null)failures.Add("tripper's second check: F starts no chat check ("+session.Message+")");
                else
                {
                    var said=again.Chat;
                    yield return new WaitForSeconds(.6f);
                    ExpectThread("second chat check","< "+said.Opener);
                    ExpectReplies("second chat check",said.Questions,true,true,true);
                    // DIALOG-6: 1 asked .2 s before the chat ends leaves their answer readable 1.5 s after it ended, in the window headed
                    // CHAT OVER with nothing left to ask, while 2 equips; once it is whole, G closes the window and drops nothing.
                    double end=sim.State.SimulationSeconds+.2;string answer=said.Answers[0];
                    yield return Tap(Key.Digit1);
                    while(sim.State.SimulationSeconds<end)yield return null;
                    session.Command("EndChat",again.Id);
                    while(sim.State.SimulationSeconds<end+1.5)yield return null;
                    string where="1.5 s after a chat ended .2 s after 1 asked";
                    if(again.Status=="Active")failures.Add(where+": the chat is still on");
                    if(window==null||!window.Open)failures.Add(where+": the chat window is hidden");
                    else
                    {
                        string last=window.Bubbles<3?"":window.Shown(window.Bubbles-1);
                        if(!window.Header.StartsWith("CHAT OVER"))failures.Add(where+": the chat window is headed \""+window.Header+"\"");
                        if(window.Bubbles!=3||window.Shown(1)!=said.Questions[0]||!window.IsTheirs(2)||!last.StartsWith(answer.Substring(0,Mathf.Min(8,answer.Length))))failures.Add(where+": their answer isn't there to read: \""+last+"\"");
                        if(window.CanReply(1)||window.CanReply(2))failures.Add(where+": questions are still on offer");
                        yield return Tap(Key.Digit2);
                        if(player.EquippedItemId!="map"||window.Bubbles!=3)failures.Add(where+": 2 didn't equip slot 2 ("+player.EquippedItemId+", "+window.Bubbles+" bubbles)");
                        float typed=Time.realtimeSinceStartup+FestivalChatWindow.Typed(answer);
                        while(window.Open&&window.Shown(window.Bubbles-1)!=answer&&Time.realtimeSinceStartup<typed)yield return null;
                        if(!window.Open||window.Shown(window.Bubbles-1)!=answer)failures.Add(where+": their whole answer never shows");
                        int carried=player.Inventory.Sum(i=>i.Count),dropped=sim.State.Drops.Count;
                        yield return Tap(Key.Escape);
                        if(window.Open||session.MenuOpen)failures.Add(where+": Escape did not dismiss before the menu");
                        if(player.Inventory.Sum(i=>i.Count)!=carried||sim.State.Drops.Count!=dropped)failures.Add(where+": Escape closing the chat window dropped something");
                    }
                    // That chat was ended explicitly and checked them, so you get a new vision about them to check.
                    sim.State.Visions.Add(new VisionState{Id="hud_vision_again",Kind="Buyer",NpcId=npc.Id});
                }

                // VISION-3: Sam dances with that festivalgoer, so the host would refuse your check: the HUD says they're busy, and
                // neither E nor F starts one.
                session.Command("Cancel");mate.X=npc.X-.6f;mate.Z=npc.Z;
                var mateDance=sim.Execute(mate.Id,new GameCommand{Id="hud_mate_dance",Kind="Dance",TargetId=npc.Id});
                if(!mateDance.Accepted)failures.Add("setup: Sam dances with the festivalgoer: "+mateDance.Reason);
                yield return new WaitForSeconds(.6f);
                Expect("tripper beside a busy vision","Prompt","They're busy right now");
                PressE();PressF();yield return null;
                if(sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind.StartsWith("Confirm")&&i.Status=="Active"))failures.Add("tripper beside a busy vision: E or F starts a check ("+session.Message+")");
                sim.Execute(mate.Id,new GameCommand{Id="hud_mate_stop",Kind="Cancel"});mate.X=30;mate.Z=0;

                // DIALOG-6: a festivalgoer's line stays up long enough to read. Talked to and danced with at once, their line is still
                // the dance's dialogue 2 s in; once read, the dance's own line takes over, and only then counts as seen.
                session.Command("Cancel");player.X=npc.X+.6f;player.Z=npc.Z;npc.Mode="Blending";npc.Suspicion=0;npc.CanTalk=true;npc.LastTalkSeconds=-10;
                session.Command("Talk",npc.Id);string spoke=player.NpcSpeech;
                session.Command("Dance",npc.Id);var talkDance=sim.Interaction(player.InteractionId);
                if(spoke==""||talkDance?.Kind!="Dance"||talkDance.DialogueId=="")failures.Add("setup: a Talk then a Dance with the festivalgoer ("+session.Message+")");
                else
                {
                    talkDance.DurationSeconds=60;session.Profile.Data.SeenDialogue.RemoveAll(id=>id==talkDance.DialogueId);
                    yield return new WaitForSeconds(2);
                    Expect("2 s into a dance begun as they spoke","Speaker","FESTIVALGOER");Expect("2 s into a dance begun as they spoke","Dialogue line",spoke);
                    if(session.Profile.Data.SeenDialogue.Contains(talkDance.DialogueId))failures.Add("2 s into a dance begun as they spoke: the dance's unshown line counts as seen");
                    yield return new WaitForSeconds(FestivalTalkLine.ReadFor(spoke)-2+.4f);
                    Expect("their line read, mid-dance","Dialogue line",talkDance.DialogueText);
                    if(!session.Profile.Data.SeenDialogue.Contains(talkDance.DialogueId))failures.Add("their line read, mid-dance: the dance's line on the card doesn't count as seen");
                }
                // The retired corner bark card stays hidden. A menu consumes world speech silently, retaining the literal and ID without replay.
                session.Command("Cancel");npc.Suspicion=0;npc.LastTalkSeconds=-10;
                session.Command("Talk",npc.Id);spoke=player.NpcSpeech;string hiddenId=player.SpeechDeliveryId;session.MenuOpen=true;
                float hostDrops=Time.realtimeSinceStartup+10;
                while(sim.State.SimulationSeconds<player.NpcSpeechUntil+.1&&Time.realtimeSinceStartup<hostDrops)yield return null;
                Expect("the menu open as they spoke","Dialogue line","(hidden)");
                session.MenuOpen=false;yield return new WaitForSeconds(.3f);
                Expect("the menu closed after the host dropped their line","Dialogue line","(hidden)");
                if(SpeechTexts(hud).Any(t=>t.text==spoke))failures.Add("the menu closed after expiry: the world bubble replayed");
                var hiddenDelivery=session.SpeechDeliveries.Find(hiddenId);
                if(hiddenDelivery==null||hiddenDelivery.Text!=spoke||hiddenDelivery.WorldActive||hiddenDelivery.Retracted)failures.Add("expired menu speech lost its literal/identity or became a retraction");
                if(hud.GetComponentsInChildren<AudioSource>(false).Any(a=>a.name=="Festival babble"&&a.isPlaying))failures.Add("the menu closed after expiry: babble backlog replayed");

                // Night 2 doses everyone: the crew reads every dose, and what each friend took.
                session.Command("Cancel");Trip(mate,3,"lsd");sim.State.LevelIndex=3;sim.State.DurationSeconds=600;
                sim.State.Doses.Add(new PlayerDose{PlayerId=player.Id,Dose=1,Substance="ketamine"});
                yield return new WaitForSeconds(.6f);
                Expect("night 2","Tripping","SAM TRIPS  •  3 DOSES  •  TONGUE STAMPS\nDOSED  YOU 1 PONY DUST");
                Fits("night 2","Tripping");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Tripper HUD a player reads:\n"+string.Join("\n",failures));
        }

        // Every challenge the live dancer can head: the four-lane kinds, and (DIALOG-3) a sale's, a buy's and a chat's trust dance.
        private static readonly string[] DancerKinds={"Dance","ConfirmDance","Poi","Dj","Sale","Buy","Conversation"};

        private const string VirtualKeyboard="HUD tripper keyboard";
        private void UseAVirtualKeyboard()=>base.UseAVirtualKeyboard(VirtualKeyboard);

        // TEST-1: a test that stops with the virtual keyboard in (here it simply never takes it out) still leaves the next test the
        // real keyboard's input: no virtual keyboard, and the settings as they were.
        [UnityTest,Order(1)]public IEnumerator ATestStoppedWithTheVirtualKeyboardInLeavesIt()
        {
            UseAVirtualKeyboard();yield return null;
            Assert.That(InputSystem.devices.Any(d=>d.name==VirtualKeyboard),Is.True,"setup: the virtual keyboard is in");
        }
        [UnityTest,Order(2)]public IEnumerator TheNextTestHasTheRealKeyboardBack()
        {
            Assert.That(InputSystem.devices.Where(d=>d.name.StartsWith(VirtualKeyboard)).Select(d=>d.name),Is.Empty,"a virtual keyboard left by an earlier test");
            var input=InputSystem.settings;
            Assert.That((input.backgroundBehavior,input.editorInputBehaviorInPlayMode),Is.EqualTo((InputBackgroundBeforeTest,InputEditorBeforeTest)),"the input settings an earlier test changed");
            yield break;
        }

        // DANCE-12: E sits between the W and D lanes, so a slip onto it mid-dance must never walk you out. In a hosted dance beside
        // a festivalgoer you could buy from, with gear in hand and a friend to offer it to, a stray E does nothing and a tap of G
        // drops nothing; holding G for a second stops the dance, as the lane's bottom line says. DIALOG-3: a buy is a talk, replied
        // to on 1-3. K2 (batch call 9): a tap of E never walks out of it either, even the moment it starts; the prompt says to hold E,
        // and holding it a second walks out. The dance a wary seller asks for first is on the lanes and held by G, and a buy stopped
        // either way hands its held price back. The Escape menu's CANCEL ACTION still stops a dance. E and G go through the HUD's
        // own input handling (FestivalHud.UpdateKeyboard) on a virtual keyboard.
        [UnityTest]public IEnumerator AStrayEOrATapOfGNeverEndsADance()
        {
            UseAVirtualKeyboard();
            var world=new GameObject("HUD dance stop world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD dance stop");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            var refresh=typeof(FestivalHud).GetField("nextActionRefresh",BindingFlags.NonPublic|BindingFlags.Instance);
            T Shown<T>(string name) where T:Component{foreach(var c in hud.GetComponentsInChildren<T>(false))if(c.name==name)return c;return null;}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("hud_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                // A festivalgoer who sells, right beside you, so by day E would otherwise buy from them (ECON-3: you hold no stock); with
                // confetti in hand and Sam in reach, offering it to Sam is listed too (with no vision to check, whoever trips, or E would
                // check by dancing instead).
                // Not a raver or a burner, who may ask for a dance first whatever you've done (DIALOG-3).
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook"&&n.Twist==""&&DialogueGrammar.PersonaFor(n.Id,"")!="Raver"&&DialogueGrammar.PersonaFor(n.Id,"")!="Burner");sim.State.Npcs.RemoveAll(n=>n!=npc);sim.State.Visions.Clear();
                npc.X=0;npc.Z=0;npc.Mode="Blending";npc.Suspicion=0;npc.CanTalk=true;npc.Role="Regular";npc.StockSold=0;
                player.X=.6f;player.Z=0;player.Cash=50;mate.X=.6f;mate.Z=1.5f;
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});player.EquippedItemId="confetti";
                InteractionState Doing()=>sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Status=="Active");
                bool Confetti()=>player.Inventory.Exists(i=>i.ItemId=="confetti");
                // Holds G for 1.1 s, checking halfway that the dance goes on with the fill under the prompt part grown.
                IEnumerator HoldG(string where,InteractionState doing)
                {
                    InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(Key.G));
                    yield return new WaitForSeconds(.5f);
                    if(Doing()?.Id!=doing.Id)failures.Add(where+": half a second into holding G, it already stopped");
                    var fill=Shown<Image>("Stop hold fill");var prompt=Shown<Text>("Stop hold");
                    float filled=fill==null||prompt==null?0:fill.rectTransform.rect.width/prompt.rectTransform.rect.width;
                    if(filled<.2f||filled>.9f)failures.Add(where+": half a second into holding G, the fill under the prompt is "+(fill==null?"hidden":filled.ToString("0.00")+" full"));
                    yield return new WaitForSeconds(.6f);yield return null;yield return null;
                    if(Doing()!=null||sim.State.Interactions.Find(i=>i.Id==doing.Id)?.Status!="Cancelled")failures.Add(where+": holding G for 1.1 s doesn't stop it ("+session.Message+")");
                    InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());yield return null;yield return null;
                    if(!Confetti())failures.Add(where+": holding G dropped your confetti");
                    npc.Suspicion=0;
                }
                // Holds E for 1.1 s, checking halfway that the talk goes on with the fill across the prompt grown.
                IEnumerator EndWithEscape(string where,InteractionState doing)
                {
                    yield return Tap(Key.Escape);
                    if(Doing()!=null||sim.State.Interactions.Find(i=>i.Id==doing.Id)?.Status!="Cancelled")failures.Add(where+": Escape does not walk out ("+session.Message+")");
                    if(session.MenuOpen)failures.Add(where+": ending the conversation opens the menu");
                }

                // A dance: a stray E and a tap of G leave it going, and nothing is bought, offered or dropped.
                // Even an E the moment it starts, while the action list still leads with the buy: on a joined client the snapshot
                // that starts it lands up to .2 s before the list next refreshes, and beside a night-market shelf it never does.
                yield return new WaitForSeconds(.4f);
                Assert.That(Shown<Text>("Prompt")?.text,Does.StartWith("Buy "),"setup: E buys from the festivalgoer");
                refresh.SetValue(view,float.MaxValue);
                session.Command("Dance",npc.Id);
                var dance=Doing();
                Assert.That(dance?.Kind,Is.EqualTo("Dance"),"setup: you dance with the festivalgoer: "+session.Message);
                string danced=session.Message;
                yield return Tap(Key.E);
                refresh.SetValue(view,0f);
                if(Doing()?.Id!=dance.Id)failures.Add("dancing, E as it starts: it ended the dance ("+session.Message+")");
                if(session.Message!=danced)failures.Add("dancing, E as it starts: E asked the host for something (\""+session.Message+"\")");
                if(sim.State.Transfers.Count>0)failures.Add("dancing, E as it starts: E offered Sam your confetti");
                yield return new WaitForSeconds(.4f);
                if(primary.GetValue(view)!=null)failures.Add("dancing: E still runs an action");
                var stop=Shown<Text>("Stop hold");
                if(stop?.text!="HOLD G TO STOP DANCING")failures.Add("dancing: the lane's bottom line reads \""+(stop?.text??"(hidden)")+"\", not HOLD G TO STOP DANCING");
                else{Canvas.ForceUpdateCanvases();if(stop.cachedTextGenerator.characterCountVisible<stop.text.Length)failures.Add("dancing: the box "+stop.rectTransform.rect.size+" cuts \""+stop.text+"\"");}
                yield return Tap(Key.E);
                if(Doing()?.Id!=dance.Id)failures.Add("dancing: a stray E ended the dance ("+session.Message+")");
                if(sim.State.Transfers.Count>0)failures.Add("dancing: a stray E offered Sam your confetti");
                // The host refuses a drop mid-dance anyway; the HUD never asks, so no refusal pops up either.
                string notice=session.Message;
                yield return Tap(Key.G);
                if(Doing()?.Id!=dance.Id)failures.Add("dancing: a tap of G ended the dance ("+session.Message+")");
                if(!Confetti())failures.Add("dancing: a tap of G dropped your confetti");
                if(session.Message!=notice)failures.Add("dancing: a tap of G asked to drop your confetti (\""+session.Message+"\")");
                if(Doing()?.Id==dance.Id)yield return HoldG("dancing",dance);

                // A buy is a talk. A tap of E never walks out of it, not even the moment it starts, while the list still leads with
                // the buy (a second E just after the one that asked); the prompt says to hold E, and a second's hold walks out, the
                // price held for it back.
                int cash=player.Cash;
                refresh.SetValue(view,float.MaxValue);
                session.Command("StartBuy",npc.Id,FestivalSimulation.SellerStock(npc.Id));
                var buy=Doing();
                Assert.That(buy?.Kind,Is.EqualTo("Buy"),"setup: you ask the festivalgoer to sell: "+session.Message);
                Assert.That(buy.TrustDance,Is.False,"setup: they talk straight away");
                string asked=session.Message;
                yield return Tap(Key.E);
                refresh.SetValue(view,0f);
                if(Doing()?.Id!=buy.Id)failures.Add("buying, E as it starts: it walked out of the talk ("+session.Message+")");
                if(session.Message!=asked)failures.Add("buying, E as it starts: E asked the host for something (\""+session.Message+"\")");
                yield return new WaitForSeconds(.4f);
                if(primary.GetValue(view)!=null)failures.Add("buying: E still runs an action");
                var walk=Shown<Text>("Prompt");
                if(walk?.text!=FestivalHudText.WalkAway)failures.Add("buying: the prompt reads \""+(walk?.text??"(hidden)")+"\", not "+FestivalHudText.WalkAway);
                yield return Tap(Key.E);
                if(Doing()?.Id!=buy.Id)failures.Add("buying: a tap of E walked out of the talk ("+session.Message+")");
                if(Doing()?.Id==buy.Id)yield return EndWithEscape("buying",buy);
                if(player.Cash!=cash)failures.Add("buying, walked out by holding E: you hold $"+player.Cash+", not the $"+cash+" you had");
                // A seller wary of you asks for a dance first, on the lanes: there a stray E does nothing, and holding G for a second
                // stops it, the price back.
                npc.Observers.RemoveAll(o=>o.PlayerId==player.Id);npc.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=FestivalSimulation.TrustDanceSuspicion});
                session.Command("StartBuy",npc.Id,FestivalSimulation.SellerStock(npc.Id));
                var wary=Doing();
                Assert.That(wary?.Kind=="Buy"&&wary.TrustDance,Is.True,"setup: a wary seller asks for a dance first: "+session.Message);
                yield return new WaitForSeconds(.4f);
                yield return Tap(Key.E);
                if(Doing()?.Id!=wary.Id)failures.Add("buying, dancing first: a stray E ended it ("+session.Message+")");
                if(Doing()?.Id==wary.Id)yield return HoldG("buying, dancing first",wary);
                if(player.Cash!=cash)failures.Add("buying, stopped by holding G: you hold $"+player.Cash+", not the $"+cash+" you had");
                npc.Observers.RemoveAll(o=>o.PlayerId==player.Id);

                // The Escape menu's CANCEL ACTION still stops a dance.
                session.Command("Dance",npc.Id);
                var again=Doing();
                Assert.That(again?.Kind,Is.EqualTo("Dance"),"setup: you dance again: "+session.Message);
                session.MenuOpen=true;yield return null;yield return null;
                var cancel=Shown<Button>("CANCEL ACTION");
                if(cancel==null)failures.Add("dancing, menu open: no CANCEL ACTION");
                else{cancel.onClick.Invoke();yield return null;if(Doing()!=null)failures.Add("dancing, menu open: CANCEL ACTION doesn't stop the dance ("+session.Message+")");}
                session.MenuOpen=false;
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"A stray E or G mid-dance:\n"+string.Join("\n",failures));
        }

        // Every HUD card a player can see at once; none may cover another.
        private static readonly string[] Cards={"Objective card","Round clock","Tripper card","Player card","Crew card","Action prompt","Session notice","Chat window","Held item label","Equipment bar","Interaction dialogue","Counter price confirmation"};

        // HUD-2 on Night 2 with a full crew of 8, the busiest the HUD gets: the tripper checks by chat while the everyone-home
        // checklist lists all 8 (its lowest), the dose card lists everyone's dose and substance (TRIP-5: two to a line, so 5
        // lines, pushing the notice down) and a notice is up. Laid out at 1920x1080, no card may cover another or cut its text.
        [UnityTest]public IEnumerator NightTwoChatCheckCoversNoOtherCard()
        {
            var world=new GameObject("HUD night 2 world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD night 2");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);
                var player=sim.Player(session.LocalPlayerId);
                var mates=new List<PlayerState>();
                foreach(var name in new[]{"Sam","Kim","Alexandria Longname","Jo","Riya","Kai","Lu"}){var mate=sim.AddPlayer("hud_"+name.Split(' ')[0].ToLowerInvariant(),name);mate.Ready=true;mates.Add(mate);}
                yield return StartLevel(session);

                // Night 2: you trip on four doses, the crew is dosed too and away from home.
                sim.State.LevelIndex=3;sim.State.DurationSeconds=600;sim.State.ElapsedSeconds=30;
                foreach(var p in sim.State.Players)p.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
                player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="hud_night2_dose",Intensity=4,Substance="ecstasy",RemainingSeconds=600});
                sim.State.TripperId=player.Id;sim.State.Doses.Clear();sim.State.Doses.Add(new PlayerDose{PlayerId=player.Id,Dose=4,Substance="ecstasy"});
                // The longest names on the wheel sit beside the longest friend's name.
                var took=new[]{"lsd","mushrooms","ecstasy","ketamine","weed","lsd","ecstasy"};
                for(int i=0;i<mates.Count;i++){sim.State.Doses.Add(new PlayerDose{PlayerId=mates[i].Id,Dose=i%4+1,Substance=took[i]});mates[i].X=-30+i*2;mates[i].Z=-30;}
                // Beside a festivalgoer you have a vision about, you check by chat, and a notice comes up meanwhile.
                var seen=sim.State.Visions.Find(v=>v.NpcId!=""&&!v.Confirmed);Assert.That(seen,Is.Not.Null,"setup: the day deals visions about festivalgoers");
                var npc=sim.State.Npcs.Find(n=>n.Id==seen.NpcId);sim.State.Npcs.RemoveAll(n=>n!=npc);
                npc.X=0;npc.Z=0;npc.Mode="Blending";npc.Suspicion=0;npc.CanTalk=true;player.X=.6f;player.Z=0;
                yield return new WaitForSeconds(.6f);
                session.Command("ConfirmChat",npc.Id);
                Assert.That(sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="ConfirmChat"&&i.Status=="Active"),"setup: a chat check starts: "+session.Message);
                session.Command("Talk","nobody_here");
                yield return new WaitForSeconds(.6f);

                Canvas.ForceUpdateCanvases();
                var shown=new List<(string name,Rect rect)>();
                foreach(var image in hud.GetComponentsInChildren<Image>(false))
                {
                    if(System.Array.IndexOf(Cards,image.name)<0)continue;
                    var corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);
                    var min=canvas.transform.InverseTransformPoint(corners[0]);var max=canvas.transform.InverseTransformPoint(corners[2]);
                    shown.Add((image.name,Rect.MinMaxRect(min.x,min.y,max.x,max.y)));
                }
                foreach(var card in new[]{"Chat window","Crew card","Session notice","Tripper card"})if(!shown.Exists(c=>c.name==card))failures.Add("setup: the "+card+" is hidden");
                string roster="",tripping="";
                foreach(var text in hud.GetComponentsInChildren<Text>(false))
                {
                    if(text.name=="Roster")roster=text.text;if(text.name!="Tripping")continue;tripping=text.text;
                    if(text.cachedTextGenerator.characterCountVisible<tripping.Length)failures.Add("the dose card "+text.rectTransform.rect.size+" cuts \""+tripping.Replace("\n"," | ")+"\"");
                }
                if(roster.Split('\n').Length!=9||tripping.Split('\n').Length!=5)failures.Add("setup: expected the 9-line checklist and 5-line dose card, got \""+roster.Replace("\n"," | ")+"\" and \""+tripping.Replace("\n"," | ")+"\"");
                for(int i=0;i<shown.Count;i++)for(int j=i+1;j<shown.Count;j++)
                {
                    var a=shown[i].rect;var b=shown[j].rect;if(!a.Overlaps(b))continue;
                    failures.Add(shown[i].name+" and "+shown[j].name+" overlap by "+(Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)).ToString("F0")+"x"+(Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)).ToString("F0")+" px");
                }
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Night 2, 8 crew, a chat check and a notice at 1920x1080:\n"+string.Join("\n",failures));
        }

        // Night 2's dose card for a crew of 8, every friend named as long as the host allows (24 letters) and on the longest
        // substances. At each screen shape and size, at both ends of the text size, the card keeps its top under the clock and grows
        // down to show all of its wrapped text at full size, with the session notice (and on a day, the sundown warning) below it.
        // A resize, a new text size or font and friends leaving each measure it again; a one-line card leaves the notice in its place.
        [UnityTest]public IEnumerator TheDoseCardGrowsDownToShowEveryLongNameAndPushesTheNoticeBelowIt()
        {
            var world=new GameObject("impl-U46 dose card world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("impl-U46 dose card HUD");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();Font wide=null;Text tripping=null;Font authored=null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool diagnostics=DevelopmentDiagnostics.Visible;DevelopmentDiagnostics.Visible=false;int layoutLogs=0;string layoutLog="";
            Application.LogCallback layoutTrace=(text,stack,type)=>{if(text.StartsWith("[Festival.HudLayout] action=dose_card ")){layoutLogs++;layoutLog=text;}};
            Application.logMessageReceived+=layoutTrace;
#endif
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mates=new List<PlayerState>();
                foreach(var name in new[]{"Maximiliana Wolfensteins","Bartholomew Winterbottom","Gwendolyn Montgomeryshaw","Wilhelmina Featherstones","Montgomery Wolverhampton","Marguerite Hammerschmidt","Wolfgang Mountbattenwood"})
                {
                    Assert.That(name.Length,Is.EqualTo(24),"setup: "+name+" is as long as a name can be");
                    var mate=sim.AddPlayer("impl_u46_"+mates.Count,name);mate.Ready=true;mates.Add(mate);
                }
                yield return StartLevel(session);
                // Everyone on the dose list carries that dose, and nobody else any, so Core's KeepTripper never
                // hands the trip, and the card, to someone else mid-measure.
                void Dosed()
                {
                    foreach(var p in sim.State.Players)
                    {
                        p.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);var d=sim.State.Doses.Find(x=>x.PlayerId==p.Id);
                        if(d!=null)p.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=d.Dose,Substance=d.Substance,RemainingSeconds=600});
                    }
                }
                // Night 2: the first friend trips on four Rolly Pollies, everyone else is on four Tongue Stamps or Rolly Pollies, away from home.
                sim.State.LevelIndex=3;sim.State.DurationSeconds=600;sim.State.ElapsedSeconds=30;sim.State.Npcs.Clear();
                sim.State.TripperId=mates[0].Id;sim.State.Doses.Clear();
                sim.State.Doses.Add(new PlayerDose{PlayerId=mates[0].Id,Dose=4,Substance="ecstasy"});
                sim.State.Doses.Add(new PlayerDose{PlayerId=player.Id,Dose=4,Substance="lsd"});
                for(int i=1;i<mates.Count;i++)sim.State.Doses.Add(new PlayerDose{PlayerId=mates[i].Id,Dose=4,Substance=i%2==0?"lsd":"ecstasy"});
                Dosed();
                for(int i=0;i<mates.Count;i++){mates[i].X=-30+i*2;mates[i].Z=-30;}

                var canvas=hud.GetComponentInChildren<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.localScale=Vector3.one;
                tripping=hud.GetComponentsInChildren<Text>(true).First(t=>t.name=="Tripping");authored=tripping.font;
                RectTransform Card(string name)=>hud.GetComponentsInChildren<Image>(true).First(i=>i.name==name).rectTransform;
                Rect Bounds(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);var min=canvasRect.InverseTransformPoint(corners[0]);var max=canvasRect.InverseTransformPoint(corners[2]);return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
                float Height(float y)=>y/canvasRect.rect.height+canvasRect.pivot.y;
                // The canvas size and scale CanvasScaler gives the HUD (1920x1080 reference, width and height matched equally) on this screen.
                void ShowOn(int width,int height){float scale=Mathf.Pow(2,Mathf.Lerp(Mathf.Log(width/1920f,2),Mathf.Log(height/1080f,2),scaler.matchWidthOrHeight));scaler.dynamicPixelsPerUnit=scale;canvasRect.sizeDelta=new Vector2(width/scale,height/scale);}
                int message=0;
                IEnumerator Settle(){session.SetMessage((message++%2==0?"Your friend found":"A friend found")+" the missing festivalgoer. Get everyone home together.");yield return null;yield return null;yield return null;Canvas.ForceUpdateCanvases();}
                // What the drawn card shows: every line, full size, under the clock, no taller than it needs; the notice in its own place or
                // just under the card, the sundown warning in its own place or just under the notice, and no card over another.
                float Check(string where,bool sundown=false)
                {
                    var cardRect=Card("Tripper card");var card=Bounds(cardRect);var notice=Bounds(Card("Session notice"));
                    if(!cardRect.gameObject.activeInHierarchy||!Card("Session notice").gameObject.activeInHierarchy){failures.Add(where+": the dose card or the notice is hidden");return 0;}
                    var drawn=tripping.cachedTextGenerator;int visible=Mathf.Max(0,drawn.characterCountVisible);float ppu=tripping.pixelsPerUnit,lines=0,smallest=float.MaxValue,h=canvasRect.rect.height;
                    foreach(var line in drawn.lines){lines+=line.height/ppu;smallest=Mathf.Min(smallest,line.height/ppu);}
                    var unshrunk=tripping.GetGenerationSettings(Vector2.zero);unshrunk.resizeTextForBestFit=false;unshrunk.horizontalOverflow=HorizontalWrapMode.Overflow;unshrunk.verticalOverflow=VerticalWrapMode.Overflow;
                    var measure=new TextGenerator();measure.Populate("X",unshrunk);float fullLine=measure.lines[0].height/ppu;
                    string seen=where+" (card "+card+", text box "+tripping.rectTransform.rect.size+", font "+tripping.fontSize+", "+drawn.lineCount+" lines "+lines.ToString("F1")+" tall, full-size line "+fullLine.ToString("F1")+", notice "+notice+")";
                    if(visible<tripping.text.Length&&tripping.text.Substring(visible).Trim().Length>0)failures.Add(seen+": the dose card cuts \""+tripping.text.Substring(visible).Replace("\n"," | ")+"\"");
                    if(smallest<fullLine-.5f)failures.Add(seen+": a line is drawn "+smallest.ToString("F1")+" tall, smaller than its full size");
                    if(Mathf.Abs(Height(card.yMax)-.906f)*h>.5f)failures.Add(seen+": the card's top moved from under the clock to "+Height(card.yMax).ToString("F4"));
                    if(tripping.rectTransform.rect.height-lines>=2*smallest)failures.Add(seen+": the card has room for another line past its padding");
                    void Below(string name,Rect upper,Rect lower,float own)
                    {
                        float gap=upper.yMin-lower.yMax;
                        if(gap<-.5f)failures.Add(seen+": the "+name+" reaches "+(-gap).ToString("F0")+" px up into the card above it");
                        else if(Mathf.Abs(Height(lower.yMax)-own)*h>.5f&&gap>12)failures.Add(seen+": the "+name+" is "+gap.ToString("F0")+" px under the card above it, not in its own place ("+own+") or just under");
                    }
                    Below("notice",card,notice,.849f);
                    var shown=new List<(string name,Rect rect)>();
                    foreach(var image in hud.GetComponentsInChildren<Image>(false))if(System.Array.IndexOf(Cards,image.name)>=0||image.name=="Sundown warning")shown.Add((image.name,Bounds(image.rectTransform)));
                    if(sundown)
                    {
                        var warning=shown.Find(c=>c.name=="Sundown warning");
                        if(warning.name==null)failures.Add(where+": setup: the sundown warning is hidden");else Below("sundown warning",notice,warning.rect,.775f);
                    }
                    for(int i=0;i<shown.Count;i++)for(int j=i+1;j<shown.Count;j++)if(shown[i].rect.Overlaps(shown[j].rect))failures.Add(seen+": "+shown[i].name+" and "+shown[j].name+" overlap");
                    return card.height;
                }
                float NoticeTop()=>Height(Bounds(Card("Session notice")).yMax);

                var screens=new[]{(1280,720),(1920,1080),(2560,1440),(1440,900),(2560,1600),(1024,768),(2560,1080),(3440,1440)};
                foreach(float textScale in new[]{1f,1.3f})foreach(var (width,height) in screens)
                {
                    ShowOn(width,height);session.Profile.Data.TextScale=textScale;
                    yield return Settle();
                    if(tripping.text.Split('\n').Length!=5)failures.Add("setup: expected the 5-line dose list, got \""+tripping.text.Replace("\n"," | ")+"\"");
                    Check(width+"x"+height+" at "+textScale.ToString("P0")+" text");
                }

                ShowOn(1920,1080);session.Profile.Data.TextScale=1;yield return Settle();
                float full=Check("1920x1080, the whole crew");
                var installed=Font.GetOSInstalledFontNames();string monospace=new[]{"Courier New","Courier","Menlo"}.FirstOrDefault(f=>System.Array.IndexOf(installed,f)>=0);
                if(monospace!=null)
                {
                    wide=Font.CreateDynamicFontFromOSFont(monospace,tripping.fontSize);tripping.font=wide;yield return Settle();
                    if(Mathf.Abs(Check("1920x1080 in "+monospace)-full)<1)failures.Add("the card is measured again for another font ("+monospace+")");
                    tripping.font=authored;yield return Settle();
                }
                foreach(var mate in mates.GetRange(3,4))mate.Connected=false;
                yield return Settle();
                if(Check("1920x1080, four friends gone")>=full)failures.Add("with four friends gone the card shrinks");
                sim.State.TripperId=player.Id;sim.State.Doses.Clear();sim.State.Doses.Add(new PlayerDose{PlayerId=player.Id,Dose=4,Substance="ecstasy"});Dosed();
                foreach(float textScale in new[]{1f,1.3f})
                {
                    session.Profile.Data.TextScale=textScale;yield return Settle();
                    Check("one line at "+textScale.ToString("P0")+" text");
                    if(textScale==1&&Mathf.Abs(NoticeTop()-.849f)*canvasRect.rect.height>.5f)failures.Add("one line: the notice's top is "+NoticeTop().ToString("F4")+", not its own .849");
                }

                // Day 2's last minute, the longest friend tripping: the sundown warning comes up under the notice at either text size.
                sim.State.LevelIndex=2;sim.State.TripperId=mates[1].Id;sim.State.Doses.Clear();sim.State.Doses.Add(new PlayerDose{PlayerId=mates[1].Id,Dose=4,Substance="lsd"});Dosed();
                string card=FestivalHudText.Tripping(sim.State,player.Id);
                sim.State.ElapsedSeconds=sim.State.DurationSeconds-50;
                foreach(float textScale in new[]{1f,1.3f})
                {
                    session.Profile.Data.TextScale=textScale;yield return Settle();
                    Check("Day 2 sundown at "+textScale.ToString("P0")+" text",true);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // The layout trace is opt-in, once per new layout, and never names anyone.
                if(layoutLogs!=0)failures.Add("the dose card's layout trace logs "+layoutLogs+" times before it is opted in");
                DevelopmentDiagnostics.Visible=true;session.Profile.Data.TextScale=1;yield return Settle();yield return Settle();
                if(layoutLogs!=1)failures.Add("opted in, one new layout and steady frames log the dose card "+layoutLogs+" times: "+layoutLog);
                if(layoutLog.ToUpperInvariant().Contains("BARTHOLOMEW"))failures.Add("the layout trace names a player: "+layoutLog);
#endif
                for(long tick=sim.State.Tick;sim.State.Tick==tick;)yield return null;
                yield return Settle();
                if(tripping.text!=card)failures.Add("a sim tick later the dose card reads \""+tripping.text.Replace("\n"," | ")+"\", not the test's \""+card.Replace("\n"," | ")+"\"");
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Application.logMessageReceived-=layoutTrace;DevelopmentDiagnostics.Visible=diagnostics;
#endif
                if(tripping!=null&&authored!=null)tripping.font=authored;
                if(wide!=null)Object.Destroy(wide);
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Night 2's dose card for 8 crew with the longest names:\n"+string.Join("\n",failures));
        }
    }
}
