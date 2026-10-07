using System.Collections;
using System.Collections.Generic;
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
    public sealed class CampSellerPlayTests:FestivalPlayModeTest
    {
        // CAMP-1 (Matt, 2026-10-01: "i should be able to talk to the camp vendor."): at camp, beside Marigold the strip under the
        // prompt says F talks to her, and F opens her talk on the chat window: 1-3 ask her three questions and she answers, turned to
        // face you in the Talk pose; Escape closes it and she turns back. Holding an unpaid item shows the pay prompt and no talk, and
        // the host's Day 1 festival switch is still F anywhere else at camp. Hosts a real round; the keys go through a virtual
        // keyboard, so through the HUD's own input handling (FestivalHud.UpdateKeyboard).
        [UnityTest]public IEnumerator FTalksToMarigoldBesideHerAndSwitchesTheFestivalAnywhereElse()
        {
            var input=InputSystem.settings;oldBackground=input.backgroundBehavior;oldEditor=input.editorInputBehaviorInPlayMode;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Camp seller keyboard");
            var worldObject=new GameObject("Camp seller world");var world=worldObject.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Camp seller HUD");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();bool reduced=false;
            // A HUD label as drawn, "(hidden)" while it isn't up.
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            var window=hud.GetComponentInChildren<FestivalChatWindow>(true);
            string Last()=>window.Bubbles==0?"(none)":(window.IsTheirs(window.Bubbles-1)?"< ":"> ")+window.Shown(window.Bubbles-1);
            // How far, in degrees, Marigold faces from straight at `you`.
            float OffYou(PlayerState you){var seller=world.CampSeller.transform;return Vector3.Angle(seller.forward,new Vector3(you.X-seller.position.x,0,you.Z-seller.position.z));}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                Assert.That(world.CampSeller,Is.Not.Null,"the world has Marigold behind the supplies table");
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                // Answers show whole after the "…" with reduced motion (in memory only), so the thread reads at once.
                reduced=session.Profile.Data.ReducedMotion;session.Profile.Data.ReducedMotion=true;
                // Two festivals unlocked, so the host picks one with F before Day 1; two items in hand, the first one held.
                sim.State.UnlockedFestivalCount=2;
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");
                player.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});player.Inventory.Add(new ItemStack{ItemId="medical_voucher",Count=1});player.EquippedItemId="confetti";

                // Away from her, F switches the festival, as before.
                player.X=0;player.Z=0;yield return new WaitForSeconds(.4f);
                if(Read("Nearby")!="(hidden)")failures.Add("away from her: the strip under the prompt reads \""+Read("Nearby")+"\"");
                int festival=sim.State.FestivalIndex;
                yield return Tap(Key.F);yield return new WaitForSeconds(.3f);
                if(sim.State.FestivalIndex!=(festival+1)%2)failures.Add("away from her: F leaves the festival at "+sim.State.FestivalIndex+" ("+session.Message+")");
                if(window.Open)failures.Add("away from her: F opens the chat window");

                // Beside her, off to her side: the strip says F talks to her, and F does, never switching the festival.
                player.X=1.8f;player.Z=6;yield return new WaitForSeconds(.4f);
                if(Read("Nearby")!=CampSellerTalk.TalkPrompt)failures.Add("beside her: the strip under the prompt reads \""+Read("Nearby")+"\", not \""+CampSellerTalk.TalkPrompt+"\"");
                festival=sim.State.FestivalIndex;
                yield return Tap(Key.F);yield return new WaitForSeconds(.3f);
                if(sim.State.FestivalIndex!=festival)failures.Add("beside her: F switched the festival");
                Assert.That(window.Open,Is.True,"beside her F opens her talk on the chat window");
                if(window.Header!=CampSellerTalk.Header)failures.Add("her talk is headed \""+window.Header+"\"");
                if(window.Replies!=3)failures.Add("her talk offers "+window.Replies+" questions, not 3");
                else for(int i=0;i<3;i++)if(window.ReplyLabel(i)!=CampSellerTalk.Questions[i])failures.Add("question "+(i+1)+" reads \""+window.ReplyLabel(i)+"\"");
                if(Read("Nearby")!="(hidden)")failures.Add("mid-talk the strip still says to press F: \""+Read("Nearby")+"\"");
                yield return new WaitForSeconds(.7f);
                if(OffYou(player)>10)failures.Add("mid-talk Marigold faces "+OffYou(player).ToString("0")+" degrees off you");
                if(world.CampSeller.Pose!="Conversation")failures.Add("mid-talk Marigold's pose is "+world.CampSeller.Pose+", not the Talk pose");

                // 1-3 ask her questions, and none of them switches what you hold.
                yield return Tap(Key.Digit1);yield return new WaitForSeconds(.7f);
                string expected="< "+CampSellerTalk.Grab(session.State,session.LocalPlayer);
                if(Last()!=expected)failures.Add("1: her answer reads \""+Last()+"\", not \""+expected+"\"");
                yield return Tap(Key.Digit2);yield return new WaitForSeconds(.7f);
                expected="< "+CampSellerTalk.Today(session.State,session.LocalPlayer);
                if(Last()!=expected)failures.Add("2: her answer reads \""+Last()+"\", not \""+expected+"\"");
                if(player.EquippedItemId!="confetti")failures.Add("2 switched your hand to "+player.EquippedItemId+" mid-talk");
                yield return Tap(Key.Digit3);yield return new WaitForSeconds(.7f);
                if(window.Bubbles<2||window.Shown(window.Bubbles-2)!=CampSellerTalk.Questions[2]||!window.IsTheirs(window.Bubbles-1)||window.Shown(window.Bubbles-1).Length<20)
                    failures.Add("3: the thread ends \""+(window.Bubbles<2?"":window.Shown(window.Bubbles-2))+"\" then \""+Last()+"\", not your question and a tip");

                // Escape closes her talk, and she turns back to the camp and idles.
                int packed=player.Inventory.Count;
                yield return Tap(Key.Escape);yield return new WaitForSeconds(1f);
                if(window.Open)failures.Add("Escape leaves her talk open");
                if(session.MenuOpen)failures.Add("Escape opens the menu instead of dismissing Marigold");
                if(player.Inventory.Count!=packed)failures.Add("Escape dropped something instead of closing her talk ("+session.Message+")");
                if(Quaternion.Angle(world.CampSeller.transform.localRotation,Quaternion.Euler(0,CampSellerTalk.RestYaw,0))>1)failures.Add("after the talk Marigold still faces "+world.CampSeller.transform.localEulerAngles.y.ToString("0")+" degrees");
                if(world.CampSeller.Pose!="Idle")failures.Add("after the talk Marigold's pose is "+world.CampSeller.Pose);

                // Holding an unpaid item beside her: the pay prompt, not the talk, and F neither talks nor switches the festival.
                string item=sim.State.VendorOffers.Find(id=>id!="little_spoon");player.HeldOfferId=item;
                yield return new WaitForSeconds(.4f);
                expected="SHOW "+Catalog.FindItem(item).Name.ToUpperInvariant()+" TO SELLER";
                if(Read("Prompt")!=expected)failures.Add("holding an unpaid item: the prompt reads \""+Read("Prompt")+"\", not \""+expected+"\"");
                if(Read("Nearby")!="(hidden)")failures.Add("holding an unpaid item: the strip reads \""+Read("Nearby")+"\"");
                festival=sim.State.FestivalIndex;
                yield return Tap(Key.F);yield return new WaitForSeconds(.3f);
                if(window.Open)failures.Add("holding an unpaid item: F opens her talk");
                if(sim.State.FestivalIndex!=festival)failures.Add("holding an unpaid item beside her: F switched the festival");
            }
            finally
            {
                session.Profile.Data.ReducedMotion=reduced;session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(worldObject);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Talking to Marigold:\n"+string.Join("\n",failures));
        }

        // A key pressed and let go, a couple of frames each.
        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        // The virtual keyboard and the input settings go back however the test ended (an error log skips its own finally).
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        [UnityTearDown]public IEnumerator RestoreTheKeyboard()
        {
            if(keyboard!=null)
            {
                InputSystem.RemoveDevice(keyboard);keyboard=null;
                InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
            }
            yield return null;
        }
    }
}
