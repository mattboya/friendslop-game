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
    public sealed class HudTripperPlayTests:FestivalPlayModeTest
    {
        // HUD-2: the running HUD puts FestivalHudText's tripper lines on screen. Hosts a real two-player Day 1, pins the spin,
        // and reads what a player sees: who trips and on how many doses, the tripper's one hint, the checks beside a festivalgoer
        // they have a vision about (E dances, F chats), the chat picker, and nothing to check for the sober friend.
        // DANCE-2: the check dance and a chat show the live dancer beside the lanes; the menu and the end of the challenge hide it.
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
            IEnumerator Press(Key key)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            }
            // The actions the HUD lists (NEARBY / CREW), first to last; E runs the first.
            string Actions(){var listed=new List<string>();foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Action:")&&b.gameObject.activeSelf)listed.Add(b.name.Substring(7));return string.Join(" | ",listed);}
            const string CheckDance="Check by dancing  •  F CHECK BY CHAT (5 s)",CheckChat="Check by chatting (5 s, safe)",DanceAndChat="Dance with festivalgoer  •  F CHAT";
            const string GearKeys="GEAR   /   1–3 EQUIP     Q USE     G DROP",GearKeysInChat="GEAR   /   Q USE     G DROP";
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("hud_mate","Sam");mate.Ready=true;
                player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)
                {
                    if(sim.State.Phase=="Loading"&&!mate.MapReady)sim.Execute(mate.Id,new GameCommand{Id="hud_mate_loaded",Kind="MapReady"});
                    yield return null;
                }
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"round starts: "+session.Message);
                // Pin the spin: who trips, on how many doses of what (the dose effect keeps the sim from re-picking).
                void Trip(PlayerState who,int dose,string substance="")
                {
                    foreach(var p in sim.State.Players)p.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
                    who.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="hud_dose_"+who.Id,Intensity=dose,Substance=substance,RemainingSeconds=600});
                    sim.State.TripperId=who.Id;sim.State.Doses.Clear();sim.State.Doses.Add(new PlayerDose{PlayerId=who.Id,Dose=dose,Substance=substance});
                }

                // You trip on two doses as Day 1 starts: the crew reads it, and you get the one hint for a while.
                Trip(player,2);sim.State.ElapsedSeconds=0;mate.X=30;mate.Z=0;player.X=0;player.Z=0;
                yield return new WaitForSeconds(.6f);
                Expect("day 1, you trip","Tripping","YOU TRIP  •  2 DOSES");
                Expect("day 1, you trip, level start","Trust line","Trust, but verify.");
                Fits("day 1, you trip","Tripping");Fits("day 1, you trip, level start","Trust line");
                sim.State.ElapsedSeconds=30;
                yield return new WaitForSeconds(.6f);
                Expect("day 1, you trip, 30 s in","Trust line","(hidden)");
                // Sam trips on three Fun Guys: you read who, on what, and the hint is not yours.
                Trip(mate,3,"mushrooms");sim.State.ElapsedSeconds=0;
                yield return new WaitForSeconds(.6f);
                Expect("day 1, Sam trips","Tripping","SAM TRIPS  •  3 DOSES  •  FUN GUYS");
                Expect("day 1, Sam trips, level start","Trust line","(hidden)");

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
                // F checks here, so the ordinary dance no longer offers F as a chat.
                string listed=Actions();
                if(!listed.StartsWith(CheckDance+" | "+CheckChat)||listed.Contains(DanceAndChat))failures.Add("tripper beside a vision: the actions read \""+listed+"\"");
                PressE();yield return null;
                if(!sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="ConfirmDance"&&i.Status=="Active"))failures.Add("tripper beside a vision: E starts no check dance ("+session.Message+")");
                yield return new WaitForSeconds(.4f);
                Expect("check dance","Challenge title","CHECK DANCE  /  FOUR-LANE");
                // DANCE-2: the check dance opens the live dancer beside the lanes, as every four-lane challenge does, showing your
                // own character dancing; the menu hides it.
                Dancing("check dance","YOU  /  ON THE FLOOR","YOUR LOOK • YOUR MOVES");
                FitsAll("check dance","Dancer caption",FourLaneKinds.Select(FestivalHudText.DancerCaption));
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
                // A chat with them is danced too, and its line moves into the dancer panel: at 1920x1080 every line a four-lane
                // challenge can say fits there, as does every heading.
                session.Command("Conversation",npc.Id);
                if(!sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="Conversation"&&i.Status=="Active"))failures.Add("chat: no chat with the festivalgoer starts ("+session.Message+")");
                yield return new WaitForSeconds(.4f);
                Expect("chat","Challenge title","CONVERSATION  /  FOUR-LANE");
                Dancing("chat","YOU  /  CHATTING","(hidden)");
                var lines=new List<string>();
                foreach(var line in DialogueCatalog.Lines)if(System.Array.IndexOf(new[]{"dance","sale","suspicion","police"},line.Context)>=0){lines.Add(line.Text);lines.Add(line.AwkwardText);}
                FitsAll("chat","Dialogue line",lines);
                FitsAll("chat","Dancer heading",FourLaneKinds.Select(FestivalHudText.DancerHeading));
                session.Command("Cancel");npc.Suspicion=0;
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
                    var said=chat.Chat;string asking="CHECKING BY CHAT  •  1–3 ASK\n\""+said.Opener+"\"\n1  "+said.Questions[0]+"\n2  "+said.Questions[1];
                    yield return new WaitForSeconds(.6f);
                    Expect("chat check","Chat check",asking+"\n3  "+said.Questions[2]);
                    Fits("chat check","Chat check");
                    // 1-3 ask while the chat runs, so the gear bar stops offering them.
                    Expect("chat check","Equipment heading",GearKeysInChat);
                    yield return Press(Key.Digit2);
                    yield return new WaitForSeconds(.3f);
                    Expect("chat check, 2 asked","Chat check",asking+"\n      \""+said.Answers[1]+"\"\n3  "+said.Questions[2]);
                    Fits("chat check, 2 asked","Chat check");
                    if(player.EquippedItemId!="confetti")failures.Add("chat check, 2 asked: 2 equipped slot 2 ("+player.EquippedItemId+")");
                    yield return new WaitForSeconds((float)FestivalSimulation.ConfirmChatSeconds);
                    Expect("chat check done","Chat check","(hidden)");
                    Expect("chat check done","Equipment heading",GearKeys);
                    // With no chat under way, 2 equips slot 2 again (and shows the keyboard reaches the HUD).
                    yield return Press(Key.Digit2);
                    yield return new WaitForSeconds(.3f);
                    if(player.EquippedItemId!="map")failures.Add("chat check done: 2 did not equip slot 2 ("+player.EquippedItemId+")");
                    if(!seen.Confirmed)failures.Add("chat check done: the vision is still unchecked ("+session.Message+")");
                    if(Read("Prompt")==CheckDance)failures.Add("chat check done: a checked vision is offered again");
                }

                // Sam trips: you are sober beside a festivalgoer Sam has an unchecked vision about, and have nothing to check.
                mate.X=30;mate.Z=0;Trip(mate,3);sim.State.Visions.Add(new VisionState{Id="hud_vision",Kind="Buyer",NpcId=npc.Id});
                player.X=npc.X+.6f;player.Z=npc.Z;
                yield return new WaitForSeconds(.6f);
                listed=Actions();
                if(listed.Contains("Check by")||!listed.Contains(DanceAndChat))failures.Add("sober beside Sam's vision: the actions read \""+listed+"\"");
                PressF();yield return null;
                if(sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind.StartsWith("Confirm")&&i.Status=="Active"))failures.Add("sober beside Sam's vision: F starts a check");

                // You trip again and check the same festivalgoer by chat: the new chat starts with no question asked.
                session.Command("Cancel");Trip(player,2);player.X=npc.X+.6f;player.Z=npc.Z;
                yield return new WaitForSeconds(.6f);
                PressF();yield return null;
                var again=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Kind=="ConfirmChat"&&i.Status=="Active");
                if(again==null)failures.Add("tripper's second check: F starts no chat check ("+session.Message+")");
                else
                {
                    var said=again.Chat;
                    yield return new WaitForSeconds(.6f);
                    Expect("second chat check","Chat check","CHECKING BY CHAT  •  1–3 ASK\n\""+said.Opener+"\"\n1  "+said.Questions[0]+"\n2  "+said.Questions[1]+"\n3  "+said.Questions[2]);
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

        private static readonly string[] FourLaneKinds={"Dance","ConfirmDance","Poi","Dj","Sale","Conversation","Police"};

        // The virtual keyboard a test presses keys on, and the input settings it changed to let the keys through without focus.
        private const string VirtualKeyboard="HUD tripper keyboard";
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private void UseAVirtualKeyboard()
        {
            var input=InputSystem.settings;oldBackground=input.backgroundBehavior;oldEditor=input.editorInputBehaviorInPlayMode;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>(VirtualKeyboard);
        }
        // TEST-1: Unity stops a test at an unexpected error log without running the test's own finally, so the fixture puts the
        // real keyboard's input back after every test, however it ended.
        [UnityTearDown]public IEnumerator PutTheRealKeyboardBack()
        {
            if(keyboard==null)yield break;
            InputSystem.RemoveDevice(keyboard);keyboard=null;
            var input=InputSystem.settings;input.backgroundBehavior=oldBackground;input.editorInputBehaviorInPlayMode=oldEditor;
        }

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
            Assert.That((input.backgroundBehavior,input.editorInputBehaviorInPlayMode),Is.EqualTo((oldBackground,oldEditor)),"the input settings an earlier test changed");
            yield break;
        }

        // Every HUD card a player can see at once; none may cover another.
        private static readonly string[] Cards={"Objective card","Round clock","Tripper card","Player card","Crew card","Action prompt","Session notice","Chat check card","Held item label","Equipment bar","Interaction dialogue","Counter price confirmation"};

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
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                var mates=new List<PlayerState>();
                foreach(var name in new[]{"Sam","Kim","Alexandria Longname","Jo","Riya","Kai","Lu"}){var mate=sim.AddPlayer("hud_"+name.Split(' ')[0].ToLowerInvariant(),name);mate.Ready=true;mates.Add(mate);}
                player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)
                {
                    if(sim.State.Phase=="Loading")foreach(var mate in mates)if(!mate.MapReady)sim.Execute(mate.Id,new GameCommand{Id="hud_loaded_"+mate.Id,Kind="MapReady"});
                    yield return null;
                }
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"round starts: "+session.Message);

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
                foreach(var card in new[]{"Chat check card","Crew card","Session notice","Tripper card"})if(!shown.Exists(c=>c.name==card))failures.Add("setup: the "+card+" is hidden");
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
    }
}
