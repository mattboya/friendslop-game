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
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace Festival.Tests
{
    public sealed class HudVitalsPlayTests:FestivalPlayModeTest
    {
        const string Stop="SECURITY STOP • TALK (E) • DITCH IT (G) • OR WALK AWAY";
        const string StopNotice="SECURITY SAW YOUR STOCK: talk, ditch it or walk away within 20 s, or you're arrested";

        // TRIP-1: the player card draws its security warning whole at 1920x1080, after the cash, stash, sales and life, for a dosed
        // tripper mid-day with a cop about to detain them and on Night 2 with a debrief shot on top, at 100% and 130% text. The
        // effects after the warning may run off the card; the warning may not. The crowd turning on you shows whole on the threat bar's word.
        [UnityTest]public IEnumerator ThePlayerCardDrawsTheWholeWarningAt1080p()
        {
            var world=new GameObject("HUD vitals world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD vitals");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            var failures=new List<string>();float textScale=session.Profile.Data.TextScale;
            // The warning runs from its lead ("ARREST ", "SECURITY ") to the next separator, where the effects start.
            void WarningDrawn(string where,string lead)
            {
                Text vitals=null;foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Vitals")vitals=text;
                if(vitals==null){failures.Add(where+": the player card is hidden");return;}
                Canvas.ForceUpdateCanvases();
                string shown=vitals.text;int at=shown.IndexOf(lead),end=at<0?-1:shown.IndexOf("  •  ",at),drawn=vitals.cachedTextGenerator.characterCountVisible;
                if(end<0)end=shown.Length;
                if(at<0)failures.Add(where+": the card reads \""+shown.Replace("\n"," | ")+"\", with no \""+lead+"\" warning");
                else if(drawn<end)failures.Add(where+": the card's box "+vitals.rectTransform.rect.size+" cuts \""+shown.Replace("\n"," | ")+"\" after \""+shown.Substring(0,drawn).Replace("\n"," | ")+"\"");
            }
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                // The size a 1080p player sees: a 1920x1080 canvas at scale 1.
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);
                var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("vitals_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                var wooks=sim.State.Npcs.FindAll(n=>n.Kind=="Wook");var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");
                foreach(float scale in new[]{1f,1.3f})
                {
                    session.Profile.Data.TextScale=scale;string at=" at "+Mathf.RoundToInt(scale*100)+"% text";
                    // You trip on a dose with 431 s left, alone in the field with $87 made and $120 sold, and no crowd around.
                    sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");
                    sim.State.TripperId=player.Id;player.Effects.Clear();
                    player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="vitals_dose",Intensity=2,RemainingSeconds=431});
                    player.Cash=87;sim.State.StashCash=0;sim.State.LevelSales=120;player.X=0;player.Z=0;mate.X=30;mate.Z=0;
                    // A cop saw the deal and is about to detain you.
                    cop.X=1.5f;cop.Z=0;cop.Evidence.Add(new EvidenceState{PlayerId=player.Id,Kind="WitnessedDeal",DetainAt=sim.State.SimulationSeconds+300});
                    yield return new WaitForSeconds(.4f);
                    WarningDrawn("a dosed tripper mid-day, a cop about to detain them"+at,"ARREST WARNING");
                    // Night 2's numbers, with the debrief's shot on top of the dose.
                    player.Effects.Add(new ActiveEffect{Id="shot",InstanceId="vitals_shot",RemainingSeconds=90});player.Cash=187;sim.State.StashCash=45;sim.State.LevelSales=225;
                    yield return new WaitForSeconds(.4f);
                    WarningDrawn("dosed with a shot, $187 / $45 / $225"+at,"ARREST WARNING");
                    // ESC-2: the cop stops you over the stock you carry instead, and the card names every way out, whole.
                    cop.Evidence.Clear();player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
                    yield return new WaitForSeconds(.4f);
                    WarningDrawn("dosed with a shot, a cop stopping you"+at,Stop);
                    // The crowd turns on you instead: a festivalgoer beside you at 80.
                    player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");player.EquippedItemId="";cop.Evidence.Clear();cop.X=35;cop.Z=35;cop.TargetId="";
                    var watcher=wooks[0];watcher.X=3;watcher.Z=0;watcher.TargetId=player.Id;watcher.Suspicion=80;watcher.Observers.Clear();
                    watcher.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=80,LastSeenSeconds=sim.State.SimulationSeconds});sim.State.Npcs.Add(watcher);
                    yield return new WaitForSeconds(.4f);
                    Canvas.ForceUpdateCanvases();Text word=null,card=null;foreach(var text in hud.GetComponentsInChildren<Text>(false)){if(text.name=="Threat level")word=text;if(text.name=="Vitals")card=text;}
                    if(word==null||word.text!="ACCUSING"||word.cachedTextGenerator.characterCountVisible<word.text.Length)failures.Add("the crowd turning"+at+": the threat bar reads \""+word?.text+"\" drawn "+word?.cachedTextGenerator.characterCountVisible);
                    if(card!=null&&card.text.Contains("CROWD"))failures.Add("the crowd turning"+at+": the vitals still read \""+card.text.Replace("\n"," | ")+"\"");
                }
            }
            finally
            {
                session.Profile.Data.TextScale=textScale;session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"The player card at 1080p:\n"+string.Join("\n",failures));
        }

        // An offer a festivalgoer turns down in front of you (Visions.RefuseSale) raises their suspicion of you, and the threat bar on
        // the player card shows it at 1080p: from CALM at 20 to WATCHING at 30, the fill easing up to it.
        [UnityTest]public IEnumerator ADealInAFestivalgoersSightRaisesTheThreatBar()
        {
            var world=new GameObject("HUD threat world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD threat");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            RectTransform Part(string name){foreach(var rect in hud.GetComponentsInChildren<RectTransform>(false))if(rect.name==name)return rect;return null;}
            bool reduced=session.Profile.Data.ReducedMotion;
            try
            {
                session.Host("Tester",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);session.Profile.Data.ReducedMotion=false;
                // You hold a Tongue Stamp beside a festivalgoer who isn't buying and already eyes you at 20, with security far off.
                sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");foreach(var cop in sim.State.Npcs){cop.X=35;cop.Z=35;cop.Evidence.Clear();}
                player.X=0;player.Z=0;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                var regular=new NpcState{Id="threat_regular",Kind="Wook",Role="Regular",X=1.5f,Z=0,Yaw=-90,CanTalk=true};
                regular.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=20,LastSeenSeconds=sim.State.SimulationSeconds});sim.State.Npcs.Add(regular);
                yield return new WaitForSeconds(.6f);
                Assert.That(Find("Threat level")?.text,Is.EqualTo("CALM"),"setup: 20 is still calm");
                float before=Part("Threat fill").anchorMax.x;
                Assert.That(before,Is.EqualTo(.2f).Within(.03f),"setup: the bar has eased to 20");
                var offer=sim.Execute(player.Id,new GameCommand{Id="threat_offer",Kind="StartSale",TargetId=regular.Id,ItemId="stock_lsd"});
                Assert.That(offer.Accepted,Is.False,"setup: a regular turns the offer down ("+offer.Reason+")");
                Assert.That(regular.Observers.Find(o=>o.PlayerId==player.Id).Suspicion,Is.EqualTo(30).Within(.5),"setup: in their sight, a turned-down offer adds 10");
                yield return null;yield return null;
                float rising=Part("Threat fill").anchorMax.x;
                yield return new WaitForSeconds(1f);
                Canvas.ForceUpdateCanvases();var word=Find("Threat level");
                Assert.That(word?.text,Is.EqualTo("WATCHING"),"the bar names the new band");
                Assert.That(word.cachedTextGenerator.characterCountVisible,Is.EqualTo(word.text.Length),"its word is drawn whole");
                Assert.That(rising,Is.GreaterThan(before).And.LessThan(.3f),"the fill eases up rather than jumping");
                Assert.That(Part("Threat fill").anchorMax.x,Is.EqualTo(.3f).Within(.02f),"and settles at 30");
                Assert.That(Find("Vitals").text,Does.Not.Contain("CROWD"),"the vitals line no longer carries the crowd");
                Assert.That(Part("Player card").anchorMin.y,Is.GreaterThanOrEqualTo(FestivalHud.TopCardsBottom),"the player card keeps inside the top band");
                session.Profile.Data.ReducedMotion=true;regular.Observers[0].Suspicion=60;yield return new WaitForSeconds(.3f);
                Assert.That(Part("Threat fill").anchorMax.x,Is.EqualTo(.6f).Within(.01f),"with reduced motion the fill jumps straight to it");
                Assert.That(Find("Threat level").text,Is.EqualTo("QUESTIONING"));
            }
            finally
            {
                session.Profile.Data.ReducedMotion=reduced;session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
        }

        // ESC-2: a security stop on screen, at 1080p. The guard who sees your stock calls you out, the notice says why and what to
        // do about it, and the card's warning names the three ways out. Every stop gets its notice: one after a stop that was the
        // last message, and one that starts mid-dance, under the map or under the menu once that is closed. E talks to the guard,
        // as the card says, with a festivalgoer nearer than the guard and a Tongue Stamp you just ditched (G) at your feet. A guard
        // still walking over is out of talking reach: E says so and does nothing, never offering the festivalgoer your stock.
        [UnityTest]public IEnumerator AStopSaysWhyAndETalksToTheGuard()
        {
            var world=new GameObject("HUD stop world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD stop");var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            void Expect(string where,string label,string expected)
            {
                Canvas.ForceUpdateCanvases();var text=Find(label);
                if(text==null)failures.Add(where+": "+label+" is hidden, expected \""+expected+"\"");
                else if(!text.text.Contains(expected))failures.Add(where+": "+label+" reads \""+text.text.Replace("\n"," | ")+"\", expected \""+expected+"\"");
                else if(text.cachedTextGenerator.characterCountVisible<text.text.IndexOf(expected)+expected.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" cuts \""+text.text.Replace("\n"," | ")+"\"");
            }
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);

                // You carry two Tongue Stamps past security, 2 m off and facing you, with a festivalgoer at arm's length.
                sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");player.X=0;player.Z=0;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});player.EquippedItemId="stock_lsd";
                var festivalgoer=new NpcState{Id="stop_festivalgoer",X=-1,Z=0,Yaw=90};sim.State.Npcs.Add(festivalgoer);
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");cop.Evidence.Clear();cop.X=2;cop.Z=0;cop.Yaw=-90;
                yield return new WaitForSeconds(.4f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: security stops you over the stock ("+session.Message+")");
                Expect("stopped","Notice",StopNotice);
                // Security now speaks through its identified public TMP delivery, on the shared reveal clock.
                FestivalSpeechDeliveryTracker.Delivery stopSpeech=null;
                foreach(var delivery in session.SpeechDeliveries.Deliveries)if(delivery.IsPublic&&delivery.SpeakerId==cop.Id&&delivery.Text=="HEY! What's in the bag?")stopSpeech=delivery;
                Assert.That(stopSpeech,Is.Not.Null,"the actual stopping cop owns the public warning delivery");
                Assert.That(stopSpeech.Speaker,Is.EqualTo("SECURITY"));Assert.That(stopSpeech.Retracted,Is.False);
                float unread=(float)System.Math.Max(0,stopSpeech.CharacterCount/FestivalSpeechDeliveryTracker.LettersPerSecond-stopSpeech.AgeSeconds);
                if(unread>0)yield return new WaitForSeconds(unread);
                yield return null;yield return null;
                TextMeshPro stopBubble=null;
                foreach(var text in hud.GetComponentsInChildren<TextMeshPro>(false))if(text.text==stopSpeech.Text)stopBubble=text;
                Assert.That(stopBubble,Is.Not.Null,"the cop's literal renders in the current world bubble");
                Assert.That(stopBubble.enabled&&stopBubble.gameObject.activeInHierarchy,Is.True);
                Assert.That(stopBubble.fontSharedMaterial.shader.isSupported,Is.True);
                Assert.That(stopBubble.maxVisibleCharacters,Is.EqualTo(stopSpeech.VisibleCharacters));
                Assert.That(stopSpeech.VisibleCharacters,Is.EqualTo(stopSpeech.CharacterCount),"the authored 30-glyph read duration reveals the whole warning");
                stopBubble.ForceMeshUpdate();int visibleGlyphs=0;
                foreach(var character in stopBubble.textInfo.characterInfo)
                {
                    if(!character.isVisible)continue;visibleGlyphs++;
                    Assert.That(character.bottomLeft.x,Is.GreaterThanOrEqualTo(stopBubble.rectTransform.rect.xMin-.01f));
                    Assert.That(character.topRight.x,Is.LessThanOrEqualTo(stopBubble.rectTransform.rect.xMax+.01f));
                    Assert.That(character.bottomLeft.y,Is.GreaterThanOrEqualTo(stopBubble.rectTransform.rect.yMin-.01f));
                    Assert.That(character.topRight.y,Is.LessThanOrEqualTo(stopBubble.rectTransform.rect.yMax+.01f));
                }
                Assert.That(visibleGlyphs,Is.GreaterThan(10),"rendered warning glyphs fit the readable bubble bounds");
                Assert.That(stopBubble.GetComponent<MeshRenderer>().enabled,Is.True);
                Expect("stopped","Vitals",Stop);
                Expect("stopped","Prompt","Talk to security");

                // Each stop below ends without a word from you (the host takes the stock), and the next starts with the guard turned
                // back to you. The first notice's 5 s run out with nothing said since, so it is still the session's last message.
                void StopAgain(){cop.X=player.X+2;cop.Z=player.Z;cop.Yaw=-90;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});player.EquippedItemId="stock_lsd";}
                player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");yield return new WaitForSeconds(5.2f);
                // A failure, not a setup Assert: without the first notice, "stopped" above has already said why.
                if(cop.Mode=="Stop"||Find("Notice")!=null||session.Message!=StopNotice)failures.Add("setup: the stop is over, its notice gone and still the last message ("+cop.Mode+", \""+session.Message+"\")");
                StopAgain();yield return new WaitForSeconds(.4f);
                Expect("stopped again","Notice",StopNotice);
                // The next starts as you dance, over 5 s before the dance ends: a notice shown then would be gone by the end.
                player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");yield return new WaitForSeconds(.4f);
                Assert.That(cop.Mode,Is.Not.EqualTo("Stop"),"setup: with the stock gone the stop ends");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="stop_dance",Kind="Dance",TargetId="stop_festivalgoer"}).Accepted,Is.True,"setup: a dance starts");
                sim.Interaction(player.InteractionId).DurationSeconds=60;
                StopAgain();yield return new WaitForSeconds(5.5f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: security stops you mid-dance");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="stop_dance_over",Kind="Cancel"}).Accepted,Is.True,"setup: the dance ends");
                yield return new WaitForSeconds(.4f);
                Expect("stopped mid-dance, the dance over","Notice",StopNotice);
                // The next starts under the map, then the one after under the menu, each held open over 5 s.
                player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");yield return new WaitForSeconds(.4f);
                view.DevelopmentMapVisible=true;StopAgain();yield return new WaitForSeconds(5.5f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: security stops you under the map");
                view.DevelopmentMapVisible=false;yield return new WaitForSeconds(.4f);
                Expect("stopped under the map, the map closed","Notice",StopNotice);
                player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");yield return new WaitForSeconds(.4f);
                session.MenuOpen=true;StopAgain();yield return new WaitForSeconds(5.5f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: security stops you under the menu");
                session.MenuOpen=false;yield return new WaitForSeconds(.4f);
                Expect("stopped under the menu, the menu closed","Notice",StopNotice);

                // G ditches one at your feet; the stop holds over the other, and E still talks.
                session.Command("Drop",item:"stock_lsd");yield return new WaitForSeconds(.4f);
                Assert.That(sim.State.Drops.Exists(d=>d.ItemId=="stock_lsd")&&cop.Mode=="Stop",Is.True,"setup: one Tongue Stamp ditched, the stop holds ("+session.Message+", "+cop.Mode+")");
                Expect("one ditched at your feet","Prompt","Talk to security");
                ((System.Action)primary.GetValue(view))?.Invoke();yield return new WaitForSeconds(.4f);
                var talk=sim.Interaction(player.InteractionId);
                if(talk?.Kind!="Police"||talk.TargetId!=cop.Id)failures.Add("E starts "+(talk==null?"nothing":talk.Kind+" with "+talk.TargetId)+" ("+session.Message+"), not a talk with "+cop.Id);

                // Last, a stop from 8 m, a festivalgoer at arm's length: the guard takes about 2 s to walk over. Until then E says so
                // and does nothing, for a sale (the first thing E does beside them with stock by day) in the guard's sight is an arrest.
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="stop_talk_over",Kind="Cancel"}).Accepted,Is.True,"setup: the talk ends");
                sim.State.Drops.RemoveAll(d=>d.ItemId=="stock_lsd");player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");yield return new WaitForSeconds(.4f);
                Assert.That(cop.Mode,Is.Not.EqualTo("Stop"),"setup: with the stock gone the stop ends");
                festivalgoer.X=player.X-1;festivalgoer.Z=player.Z;StopAgain();cop.X=player.X+8;yield return new WaitForSeconds(.3f);
                Expect("stopped from 8 m, the guard walking over","Prompt",FestivalHudText.StopApproachAction);
                ((System.Action)primary.GetValue(view))?.Invoke();yield return new WaitForSeconds(.4f);
                var started=sim.Interaction(player.InteractionId);
                if(started!=null)failures.Add("E with the guard walking over starts "+started.Kind+" with "+started.TargetId+" ("+session.Message+")");
                if(!cop.Evidence.Exists(e=>e.PlayerId==player.Id&&e.Kind=="VisibleStock"))failures.Add("E with the guard walking over turns the stop into "+string.Join(",",cop.Evidence.ConvertAll(e=>e.Kind)));
                yield return new WaitForSeconds(4.1f);
                if(player.Life!="Alive")failures.Add("4.5 s after that E you are "+player.Life);
                Expect("the guard walked over","Prompt","Talk to security");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"A security stop on screen:\n"+string.Join("\n",failures));
        }

        // A security talk that ends with the stop still on leaves its chat up to read. G belongs to the card's ditch then, not to the
        // chat, and the gear bar stops offering G DROP until the Menu key closes the chat.
        [UnityTest]public IEnumerator TheFirstGAfterATalkWithSecurityDitchesTheStock()
        {
            UseAVirtualKeyboard("Ditch keyboard");
            var world=new GameObject("Ditch world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Ditch HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);session.MenuOpen=false;
                sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");player.X=0;player.Z=0;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});player.EquippedItemId="stock_lsd";
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");cop.Evidence.Clear();cop.X=2;cop.Z=0;cop.Yaw=-90;
                yield return new WaitForSeconds(.4f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: security stops you over the stock ("+session.Message+")");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="ditch_talk",Kind="Police",TargetId=cop.Id}).Accepted,Is.True,"setup: you talk to security");
                yield return new WaitForSeconds(.5f);
                var window=hud.GetComponentInChildren<FestivalChatWindow>(true);
                Assert.That(window.Open,Is.True,"setup: the talk is on the chat window");
                // The talk ends without waving you on (walked out of, here on the host), so the stop holds and the chat lingers.
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="ditch_over",Kind="Cancel"}).Accepted,Is.True,"setup: the talk ends");
                yield return new WaitForSeconds(.3f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: the stop holds");Assert.That(window.Open,Is.True,"setup: the ended chat stays up to read");
                Text heading=null;foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Equipment heading")heading=text;
                Assert.That(heading.text,Does.EndWith("Q USE"),"held G drops nothing while a chat is up, so the gear bar does not offer it");
                int held=player.Inventory.Find(i=>i.ItemId=="stock_lsd").Count;
                yield return Tap(Key.G);yield return new WaitForSeconds(.3f);
                Assert.That(player.Inventory.Find(i=>i.ItemId=="stock_lsd")?.Count??0,Is.EqualTo(held-1),"the first G ditches a Tongue Stamp ("+session.Message+")");
                Assert.That(sim.State.Drops.Exists(d=>d.ItemId=="stock_lsd"),Is.True,"at your feet");
                yield return Tap(Key.Escape);yield return new WaitForSeconds(.3f);
                Assert.That(window.Open,Is.False,"the Menu key closes the ended chat");
                Assert.That(heading.text,Does.EndWith("G DROP"),"with the chat closed, the gear bar offers G DROP again");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
        }
        // A talk with security that is over stays up to read, but security speaking to you closes it, so nothing covers the
        // guard: the last chance said while it lingers, the last chance said as a poor talk ends, and an arrest warning, which says
        // nothing.
        [UnityTest]public IEnumerator SecuritySpeakingClosesTheChatItLeft()
        {
            const string LastChance="Last chance. Empty your pockets.";
            var world=new GameObject("Cut in world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Cut in HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);session.MenuOpen=false;
                // The stop's clocks are moved back below; a stop that starts before 0 is timed from the load, so the level runs a while first.
                sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");sim.Tick(25);player.X=0;player.Z=0;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});player.EquippedItemId="stock_lsd";
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");sim.State.Npcs.RemoveAll(n=>n.Kind=="Cop"&&n!=cop);cop.Evidence.Clear();cop.X=2;cop.Z=0;cop.Yaw=-90;
                yield return new WaitForSeconds(.4f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: security stops you over the stock ("+session.Message+")");
                var stop=cop.Evidence.Find(e=>e.PlayerId==player.Id&&e.Kind=="VisibleStock");
                var window=hud.GetComponentInChildren<FestivalChatWindow>(true);
                int commands=0;
                IEnumerator Talk(){Assert.That(sim.Execute(player.Id,new GameCommand{Id="cut_in_"+(++commands),Kind="Police",TargetId=cop.Id}).Accepted,Is.True,"setup: you talk to security");yield return new WaitForSeconds(.3f);Assert.That(window.Open,Is.True,"setup: the talk is on the chat window");}
                bool Shown(string line)=>SpeechTexts(hud).Any(t=>t.text==line);

                // The talk is walked out of at 1 s, too soon for a last chance, so its chat lingers; at 12 s the guard says it.
                yield return Talk();var talk=sim.Interaction(player.InteractionId);
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="cut_in_"+(++commands),Kind="Cancel"}).Accepted,Is.True,"setup: the talk ends");
                yield return new WaitForSeconds(.3f);
                Assert.That(window.Lingering,Is.True,"setup: the ended chat stays up to read");
                stop.StartSeconds=sim.State.SimulationSeconds-12.05;
                for(deadline=Time.realtimeSinceStartup+2;player.NpcSpeech!=LastChance&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(player.NpcSpeech,Is.EqualTo(LastChance),"setup: at 12 s the guard gives the last chance");
                yield return new WaitForSeconds(.2f);
                Assert.That(window.Open,Is.False,"the last chance closes the chat it left");
                Assert.That(Shown(LastChance),Is.True,"and the guard's line is on screen");

                // A poor talk over at 13 s: the guard says the last chance as it ends, and the chat never stays over it.
                yield return Talk();talk=sim.Interaction(player.InteractionId);
                for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
                    Assert.That(sim.Execute(player.Id,new GameCommand{Id="cut_in_"+(++commands),Kind="Reply",Amount=System.Array.IndexOf(talk.Turns[turn].Grades,ConversationLines.Awkward),Direction=turn}).Accepted,Is.True,"setup: an awkward reply on turn "+turn);
                Assert.That(talk.Status=="Complete"&&player.NpcSpeech==LastChance,Is.True,"setup: the poor talk ends on the last chance ("+talk.Status+", \""+player.NpcSpeech+"\")");
                yield return new WaitForSeconds(.3f);
                Assert.That(window.Open,Is.False,"a last chance said as the talk ends closes its chat at once");
                Assert.That(Shown(LastChance),Is.True,"and the guard's line is on screen");

                // Walked out of past the last chance, a talk's chat lingers until the 20 s arrest warning, which closes it.
                yield return Talk();
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="cut_in_"+(++commands),Kind="Cancel"}).Accepted,Is.True,"setup: the talk ends");
                yield return new WaitForSeconds(.3f);
                Assert.That(window.Lingering,Is.True,"setup: the ended chat stays up to read, the last chance already said");
                stop.StartSeconds=sim.State.SimulationSeconds-20.05;
                for(deadline=Time.realtimeSinceStartup+2;cop.Mode!="ArrestWarning"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(cop.Mode,Is.EqualTo("ArrestWarning"),"setup: at 20 s it is an arrest warning");
                yield return new WaitForSeconds(.2f);
                Assert.That(window.Open,Is.False,"the arrest warning closes the chat it left");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
        }
        // A guard's call-out during a sale's trust dance comes before the chat, so the SALE OFF card a worked-up buyer's cut opens
        // stays up, and the stop's notice waits its turn behind the sale's own line, which says where the unit went.
        [UnityTest]public IEnumerator AGuardsLineDuringATrustDanceLeavesTheCutSalesCard()
        {
            const string StopBark="HEY! What's in the bag?",HandedBack="SALE OFF  •  Tongue Stamps back in your hand";
            var world=new GameObject("Cut sale world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Cut sale HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            var failures=new List<string>();
            string Notice(){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Notice")return text.text;return "(hidden)";}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);session.MenuOpen=false;sim.State.ElapsedSeconds=FestivalHudText.BriefingSeconds+1;
                sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");sim.Tick(25);player.X=0;player.Z=0;player.Effects.Clear();
                player.Inventory.Clear();player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});player.EquippedItemId="stock_lsd";
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");sim.State.Npcs.RemoveAll(n=>n.Kind=="Cop"&&n!=cop);cop.Evidence.Clear();cop.X=60;cop.Z=60;
                var buyer=new NpcState{Id="cut_sale_buyer",Kind="Wook",Role="Buyer",X=0,Z=1.5f,Yaw=180,CanTalk=true};sim.State.Npcs.Add(buyer);
                buyer.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=FestivalSimulation.TrustDanceSuspicion,LastSeenSeconds=sim.State.SimulationSeconds});
                var window=hud.GetComponentInChildren<FestivalChatWindow>(true);
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="cut_sale",Kind="StartSale",TargetId=buyer.Id,ItemId="stock_lsd",Amount=FestivalSimulation.FairAsk}).Accepted,Is.True,"setup: the sale starts");
                var sale=sim.Interaction(player.InteractionId);
                Assert.That(FestivalSimulation.TrustDancing(sale),Is.True,"setup: the wary buyer asks for the trust dance");
                yield return new WaitForSeconds(.3f);
                cop.X=2;cop.Z=0;cop.Yaw=-90;
                for(deadline=Time.realtimeSinceStartup+2;player.NpcSpeech!=StopBark&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(player.NpcSpeech,Is.EqualTo(StopBark),"setup: the guard calls you out mid-dance");
                yield return new WaitForSeconds(.3f);
                var shouter=new NpcState{Id="cut_sale_shouter",Kind="Wook",X=3,Z=1.5f,Yaw=-90};sim.State.Npcs.Add(shouter);
                shouter.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=95,LastSeenSeconds=sim.State.SimulationSeconds});
                for(deadline=Time.realtimeSinceStartup+2;sale.Status=="Active"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(sale.Status,Is.EqualTo("They're too agitated to chat"),"setup: the worked-up buyer cuts the trust dance");
                yield return new WaitForSeconds(.4f);
                Assert.That(player.NpcSpeech,Is.EqualTo(StopBark),"setup: no guard spoke since the call-out");
                var notes=new List<string>();for(int i=0;i<window.Bubbles;i++)if(window.IsNote(i))notes.Add(window.Shown(i));
                if(!window.Lingering||!notes.Contains(HandedBack))failures.Add("the cut sale's card stays up with \""+HandedBack+"\": open "+window.Open+", header \""+window.Header+"\", notes ["+string.Join(" ; ",notes)+"]");
                if(Notice()!=HandedBack)failures.Add("the notice says where the unit went: \""+Notice()+"\"");
                yield return new WaitForSeconds(FestivalHudText.MoneyNewsSeconds);
                if(Notice()!=StopNotice)failures.Add("the stop's notice follows the sale's line: \""+Notice()+"\"");
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
