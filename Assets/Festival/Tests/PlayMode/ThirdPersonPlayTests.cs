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
    // CAM-1 on a real hosted festival: V puts the camera over your shoulder and shows your own body, and V again goes back to first
    // person. Lying down, a camp visit and the Ferris wheel are first person, and the view goes back over your shoulder after each.
    // The CAMERA VIEW setting says which view you have and toggles it, and over the shoulder the aim dot still picks what it is on.
    // V and the toggle save the machine's own profile, so each test puts the setting back as it found it.
    public sealed class ThirdPersonPlayTests:FestivalPlayModeTest
    {
        const int Body=1<<31;
        [UnityTest]public IEnumerator ManualGearKeysOverrideReceiptsInFirstAndThirdPerson()
        {
            UseAVirtualKeyboard("impl-U52 receipt keyboard");
            var world=new GameObject("impl-U52 receipt world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("impl-U52 receipt HUD");var session=root.AddComponent<FestivalSession>();root.AddComponent<FestivalHud>();yield return null;
            string Props(Transform parent){var items=new List<string>();foreach(var t in parent.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("Held grip ")&&t.gameObject.activeSelf)items.Add(t.name.Substring(10));return string.Join(",",items);}
            var failures=new List<string>();
            void Check(string expected,string where)
            {
                var hands=session.ViewCamera.transform.Find("First-person hand attachments");
                if(hands==null||Props(hands)!=expected)failures.Add(where+": first-person props="+(hands==null?"missing":Props(hands))+", expected "+expected);
                var body=session.LocalWorldCharacter;
                if(body==null||Props(body.transform)!=expected)failures.Add(where+": third-person props="+(body==null?"missing":Props(body.transform))+", expected "+expected);
            }
            try
            {
                session.Host("Receipt tester",HostPort);for(float until=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);var game=Simulation(session);var p=game.Player(session.LocalPlayerId);
                p.Inventory.Clear();p.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});p.EquippedItemId="confetti";p.X=0;p.Z=0;
                var mate=game.AddPlayer("receipt_peer","Sam");mate.X=1;mate.Z=0;mate.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=4});mate.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=4});
                IEnumerator Receive(string item)
                {
                    Assert.That(game.Execute(mate.Id,new GameCommand{Id=System.Guid.NewGuid().ToString("N"),Kind="Transfer",TargetId=p.Id,ItemId=item,Amount=1}).Accepted,Is.True);
                    yield return new WaitForSeconds(.12f);
                    Check("", "incoming offer has empty receiving hands");
                    var offer=game.State.Transfers.Find(t=>t.ToId==p.Id);Assert.That(offer,Is.Not.Null);
                    session.Command("AcceptTransfer",offer.Id);
                    for(float until=Time.realtimeSinceStartup+.5f;session.LocalPlayer.VisualReceiptSequence!=p.VisualReceiptSequence&&Time.realtimeSinceStartup<until;)yield return null;
                    Assert.That(session.LocalPlayer.VisualReceiptSequence,Is.EqualTo(p.VisualReceiptSequence),"actual owner snapshot publishes the receipt");
                    yield return null;yield return null;Check(item,"new receipt "+item);
                }
                yield return new WaitForSeconds(.3f);
                yield return Receive("stock_lsd");
                yield return Tap(Key.Digit1);yield return new WaitForSeconds(.12f);Assert.That(p.EquippedItemId,Is.Empty,"the held filled key stows during a receipt");Check("","manual first-person stow suppresses receipt in both presenters");
                session.Profile.Data.ThirdPerson=true;p.EquippedItemId="confetti";yield return new WaitForSeconds(1);
                yield return Receive("stock_mushrooms");
                yield return Tap(Key.Digit2);yield return new WaitForSeconds(.12f);Assert.That(p.EquippedItemId,Is.EqualTo("stock_lsd"));Check("stock_lsd","manual third-person switch suppresses receipt in both presenters");
                // Repeated snapshots of the same receipt must not reinstate it.
                yield return new WaitForSeconds(.12f);Check("stock_lsd","suppression survives the next owner snapshot");
                p.EquippedItemId="";p.Inventory.RemoveAll(i=>i.ItemId=="stock_mushrooms");yield return new WaitForSeconds(1);yield return Receive("stock_lsd");
                yield return Tap(Key.Digit3);yield return new WaitForSeconds(.12f);Assert.That(p.EquippedItemId,Is.Empty);Check("","empty-to-empty manual selection suppresses a receipt without an equipped change");
                p.EquippedItemId="stock_lsd";session.Profile.Data.ThirdPerson=false;p.HeldOfferId="merch_bag";yield return new WaitForSeconds(.12f);
                yield return Tap(Key.Digit3);Assert.That(p.EquippedItemId,Is.EqualTo("stock_lsd"));Check("merch_bag","unpaid shelf prop still outranks manual gear");
                p.HeldOfferId="";yield return new WaitForSeconds(1);
                session.Command("Transfer",mate.Id,"stock_lsd",1);yield return new WaitForSeconds(.12f);Check("stock_lsd","outgoing handoff still presents its offered item");
            }
            finally{session.Leave();Object.Destroy(root);Object.Destroy(world);}
            yield return null;Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }


        // The keys are pressed on a virtual keyboard, so they go through the session's own input (FestivalSession.Update).
        [UnityTest]public IEnumerator PressingVShowsYourOwnBodyFromOverTheShoulder()
        {
            var input=InputSystem.settings;oldBackground=input.backgroundBehavior;oldEditor=input.editorInputBehaviorInPlayMode;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Camera view keyboard");
            var world=new GameObject("Camera view world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Camera view");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            yield return null;
            bool wanted=session.Profile.Data.ThirdPerson;
            var failures=new List<string>();
            IEnumerator PressV()
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.V));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
                yield return new WaitForSeconds(.4f);
            }
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                sim.State.FestivalIndex=Festivals.PoloFestival;sim.State.LevelIndex=0;
                yield return StartLevel(session);
                Assert.That(session.Profile.Data.ThirdPerson,Is.False,"setup: every hosted level starts in first person");
                // Alone and sober on the open lawn, looking north and level, with nothing behind you.
                sim.State.Npcs.Clear();player.Effects.Clear();player.X=-20;player.Z=-2;
                typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,0f);
                typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,0f);
                yield return new WaitForSeconds(.6f);
                var view=session.ViewCamera;var body=session.LocalWorldCharacter!=null?session.LocalWorldCharacter.transform:null;
                Assert.That(body,Is.Not.Null,"setup: your own body is drawn");
                float Flat(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
                void OverTheShoulder(string where)
                {
                    var at=view.transform.position;float back=Flat(at,body.position);
                    if(!session.ThirdPersonView)failures.Add(where+": ThirdPersonView is false");
                    if(back<1.8f||back>2.8f)failures.Add(where+": the camera at "+at.ToString("0.00")+" is "+back.ToString("0.00")+" m from your body at "+body.position.ToString("0.00")+", not 1.8-2.8 m");
                    if(Vector3.Dot(body.position-at,view.transform.forward)<=0)failures.Add(where+": the camera at "+at.ToString("0.00")+" is not behind your body along the view");
                    var seen=view.WorldToViewportPoint(body.position+Vector3.up);
                    if(seen.z<=0||seen.x<0||seen.x>1||seen.y<0||seen.y>1)failures.Add(where+": your body is off screen, at viewport "+seen.ToString("0.00"));
                    if((view.cullingMask&Body)==0)failures.Add(where+": the view still culls your body's layer");
                    if(HandsShown())failures.Add(where+": the first-person hands still show");
                    var eye=session.EyePosition;
                    if(Mathf.Abs(eye.y-body.position.y-1.65f)>.05f||Flat(eye,body.position)>.15f)failures.Add(where+": the eye is at "+eye.ToString("0.00")+", not 1.65 m over your body at "+body.position.ToString("0.00"));
                }
                void FirstPerson(string where,float? height=null)
                {
                    var at=view.transform.position;
                    if(session.ThirdPersonView)failures.Add(where+": ThirdPersonView is still true");
                    if(Vector3.Distance(at,session.EyePosition)>.01f)failures.Add(where+": the camera at "+at.ToString("0.00")+" is not at the eye "+session.EyePosition.ToString("0.00"));
                    if(height!=null&&Mathf.Abs(at.y-height.Value)>.1f)failures.Add(where+": the view is "+at.y.ToString("0.00")+" m up, not "+height+" m");
                    if((view.cullingMask&Body)!=0)failures.Add(where+": the view shows your own body's layer, from inside your head");
                }

                // The menu is open: V does nothing.
                session.MenuOpen=true;yield return PressV();
                if(session.Profile.Data.ThirdPerson)failures.Add("with the menu open, V switched the view");
                session.MenuOpen=false;yield return null;

                // V: over the shoulder, your body in view and no hands.
                yield return PressV();
                if(!session.Profile.Data.ThirdPerson)failures.Add("V does not switch the setting to third person");
                OverTheShoulder("after V");

                // Lying down looks up from the grass, in first person; the setting stays.
                session.Command(FestivalSimulation.LieDownKind);yield return new WaitForSeconds(.6f);
                Assert.That(FestivalSimulation.LyingDown(sim.State,player.Id),Is.True,"setup: you lie down ("+session.Message+"), after:\n"+string.Join("\n",failures));
                FirstPerson("lying down",.3f);
                if(!session.Profile.Data.ThirdPerson)failures.Add("lying down turned the third-person setting off");

                // Getting up puts the view back over your shoulder.
                session.Command("Cancel");yield return new WaitForSeconds(.6f);
                Assert.That(FestivalSimulation.LyingDown(sim.State,player.Id),Is.False,"setup: you get up ("+session.Message+"), after:\n"+string.Join("\n",failures));
                OverTheShoulder("got up");

                // The Ferris wheel is first person from the seat (POLO-5); when the turn ends the view goes back over your shoulder.
                player.X=Festivals.WheelX;player.Z=Festivals.WheelZ+1.5f;yield return new WaitForSeconds(.6f);
                session.Command(FestivalSimulation.RideWheelKind);yield return new WaitForSeconds(.6f);
                Assert.That(FestivalSimulation.OnWheel(sim.State,player.Id),Is.True,"setup: you board the wheel ("+session.Message+"), after:\n"+string.Join("\n",failures));
                FirstPerson("on the wheel");
                foreach(var ride in sim.State.Interactions)if(ride.Kind==FestivalSimulation.RideWheelKind&&ride.Status=="Active")ride.StartSeconds=sim.State.SimulationSeconds-Festivals.WheelRideSeconds+.2;
                yield return new WaitForSeconds(1);
                Assert.That(FestivalSimulation.OnWheel(sim.State,player.Id),Is.False,"setup: the turn ends, after:\n"+string.Join("\n",failures));
                OverTheShoulder("off the wheel");

                // V again: first person, the hands back and your body hidden.
                yield return PressV();
                if(session.Profile.Data.ThirdPerson)failures.Add("V again does not switch the setting back to first person");
                FirstPerson("after V again",1.65f);
                if(!HandsShown())failures.Add("after V again: the first-person hands are hidden");
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{session.Profile.Data.ThirdPerson=wanted;session.Profile.Save();session.Leave();Object.Destroy(host);Object.Destroy(world);}
        }

        // At camp, before the level: the settings row; over the shoulder the aim dot picks the shelf item it is on; a camp visit is first
        // person until you leave it.
        [UnityTest]public IEnumerator TheSettingTogglesTheViewAndTheDotPicksTheShelfItIsOn()
        {
            var world=new GameObject("Camera setting world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Camera setting");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            yield return null;
            bool wanted=session.Profile.Data.ThirdPerson;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            Button Click(string label){foreach(var b in host.GetComponentsInChildren<Button>(false))if(b.name==label){b.onClick.Invoke();return b;}return null;}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);

                // The pass's settings: on CONTROLS, CAMERA VIEW says which view you have, and its TOGGLE flips it.
                session.MenuOpen=true;yield return null;
                Assert.That(Click("SETTINGS"),Is.Not.Null,"setup: the pass has a SETTINGS button");
                yield return null;yield return null;
                Assert.That(Click("CONTROLS"),Is.Not.Null,"setup: the settings have a CONTROLS tab");
                yield return null;yield return null;
                var value=Find("CAMERA VIEW value");
                Assert.That(value,Is.Not.Null,"the settings have a CAMERA VIEW row");
                RectTransform rect=value.rectTransform;
                var toggle=host.GetComponentsInChildren<Button>(false).FirstOrDefault(b=>b.name=="TOGGLE"&&Overlaps((RectTransform)b.transform,rect));
                Assert.That(toggle,Is.Not.Null,"the CAMERA VIEW row has a TOGGLE");
                for(int i=0;i<2;i++)
                {
                    bool before=session.Profile.Data.ThirdPerson;
                    string reads=before?"THIRD PERSON":"FIRST PERSON";
                    if(value.text!=reads)failures.Add("CAMERA VIEW reads \""+value.text+"\" with ThirdPerson "+before+", not \""+reads+"\"");
                    Canvas.ForceUpdateCanvases();
                    if(value.cachedTextGenerator.characterCountVisible<value.text.Length)failures.Add("CAMERA VIEW's box "+rect.rect.size+" cuts \""+value.text+"\"");
                    toggle.onClick.Invoke();yield return null;yield return null;
                    if(session.Profile.Data.ThirdPerson==before)failures.Add("TOGGLE leaves ThirdPerson "+before);
                }
                if(!session.Profile.Data.ThirdPerson){toggle.onClick.Invoke();yield return null;}
                Assert.That(session.Profile.Data.ThirdPerson,Is.True,"setup: third person, after:\n"+string.Join("\n",failures));
                session.MenuOpen=false;

                // In front of camp's first shelf, the aim dot on the second shelf's item, then 60% of the way over to it from the first:
                // both pick the second. Your eye, .45 m left of the dot's line, would pick the first.
                var first=Catalog.FindItem(sim.State.VendorOffers[0]);var second=Catalog.FindItem(sim.State.VendorOffers[1]);
                foreach(var offer in new[]{first,second})sim.State.ShopStock.Find(s=>s.ItemId==offer.Id).CampAvailable=1;
                var spot=Catalog.ShopPoint(true,0);var next=Catalog.ShopPoint(true,1);player.X=spot.X;player.Z=6.3f;
                yield return new WaitForSeconds(.6f);
                if(!session.ThirdPersonView)failures.Add("at the shelves: the view is not over the shoulder");
                foreach(float over in new[]{1f,.6f})
                {
                    var target=new Vector3(Mathf.Lerp(spot.X,next.X,over),1.17f,9f);
                    DotOn(session,target);yield return new WaitForSeconds(.4f);
                    Assert.That(FromDot(session,target),Is.LessThan(.01f),"setup: the aim dot is on "+target.ToString("0.00"));
                    string prompt=Find("Prompt")?.text??"(hidden)";
                    if(!prompt.StartsWith("PICK UP "+second.Name.ToUpperInvariant()+"  "))failures.Add("at the shelves, the dot "+(over*100)+"% of the way from the "+first.Name+" to the "+second.Name+": the prompt reads \""+prompt+"\"");
                }

                // A camp visit is first person (its rooms have no walls to stop the boom); leaving it goes back over the shoulder.
                var site=CampFeatures.Find("tent_1");player.X=site.X;player.Z=site.Z;yield return null;
                session.Command("EnterCamp","tent_1");yield return new WaitForSeconds(.6f);
                Assert.That(player.CampVisitId,Is.EqualTo("tent_1"),"setup: you enter the tent ("+session.Message+"), after:\n"+string.Join("\n",failures));
                if(session.ThirdPersonView||Vector3.Distance(session.ViewCamera.transform.position,session.EyePosition)>.01f)failures.Add("in the tent: the view is not first person, camera at "+session.ViewCamera.transform.position.ToString("0.00")+", eye at "+session.EyePosition.ToString("0.00"));
                session.Command("ExitCamp");yield return new WaitForSeconds(.6f);
                Assert.That(player.CampVisitId,Is.Empty,"setup: you leave the tent ("+session.Message+"), after:\n"+string.Join("\n",failures));
                if(!session.ThirdPersonView||Vector3.Distance(session.ViewCamera.transform.position,session.EyePosition)<1)failures.Add("out of the tent: the view is not back over the shoulder, camera at "+session.ViewCamera.transform.position.ToString("0.00")+", eye at "+session.EyePosition.ToString("0.00"));
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{session.Profile.Data.ThirdPerson=wanted;session.Profile.Save();session.Leave();Object.Destroy(host);Object.Destroy(world);}
        }

        // VISION-5 under CAM-1, on a real hosted Day 1 (the solo host trips): over the shoulder, the aim dot on the left side of a
        // festivalgoer with a checked vision reads it, and the dot in the air .85 m to their right reads nothing, as in first person.
        // Your eye's own line, .45 m left of the dot's, would do the opposite.
        [UnityTest]public IEnumerator OverTheShoulderTheAimDotReadsTheVisionItIsOn()
        {
            var world=new GameObject("Camera vision world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Camera vision");var session=host.AddComponent<FestivalSession>();
            yield return null;
            bool wanted=session.Profile.Data.ThirdPerson;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                sim.State.FestivalIndex=Festivals.PoloFestival;sim.State.LevelIndex=0;
                yield return StartLevel(session);
                Assert.That(session.State.TripperId,Is.EqualTo(session.LocalPlayerId),"setup: the solo host is the tripper");
                var vision=sim.State.Visions.Find(v=>v.NpcId!="");
                Assert.That(vision,Is.Not.Null,"setup: a vision hangs over a festivalgoer");
                // Only that festivalgoer is about, the vision over them checked, 6 m north of you on the open lawn.
                var npc=sim.State.Npcs.Find(n=>n.Id==vision.NpcId);sim.State.Npcs.RemoveAll(n=>n!=npc);vision.Confirmed=true;
                player.Effects.Clear();player.X=-20;player.Z=-2;
                session.Profile.Data.ThirdPerson=true;
                IEnumerator Hold(float seconds){for(float end=Time.realtimeSinceStartup+seconds;Time.realtimeSinceStartup<end;){npc.X=-20;npc.Z=4;npc.Mode="Blending";yield return null;}}
                yield return Hold(.6f);
                Assert.That(session.ThirdPersonView,Is.True,"setup: the view is over your shoulder");
                var label=host.transform.Find("Vision "+vision.Id+"/Label")?.GetComponent<TextMesh>();
                Assert.That(label,Is.Not.Null,"setup: the vision is drawn");
                foreach(float right in new[]{-.1f,.85f})
                {
                    var target=new Vector3(-20+right,1,4);DotOn(session,target);yield return Hold(.4f);
                    Assert.That(FromDot(session,target),Is.LessThan(.01f),"setup: the aim dot is on "+target.ToString("0.00"));
                    bool on=right<0;
                    Assert.That(label.text!="",Is.EqualTo(on),"the aim dot "+(on?"on the left side of":".85 m right of")+" the festivalgoer with the checked "+vision.Kind+" "+(on?"reads it":"reads nothing")+" (label \""+label.text+"\", eye "+session.EyePosition.ToString("0.00")+", camera "+session.ViewCamera.transform.position.ToString("0.00")+")");
                }
            }
            finally{session.Profile.Data.ThirdPerson=wanted;session.Leave();Object.Destroy(host);Object.Destroy(world);}
        }

        // Turns the view so the camera's centre, the aim dot, is on target over the shoulder: the boom turns with the view, so look
        // again from where it puts the camera until that settles. FromDot says how far off the dot target ended up, as a share of the screen.
        static void DotOn(FestivalSession session,Vector3 target)
        {
            var look=Quaternion.LookRotation(target-session.EyePosition).eulerAngles;
            for(int i=0;i<30;i++)look=Quaternion.LookRotation(target-FestivalThirdPerson.Place(session.EyePosition,look.y,look.x,out _)).eulerAngles;
            typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,look.y);
            typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,Mathf.DeltaAngle(0,look.x));
        }
        static float FromDot(FestivalSession session,Vector3 target){var at=session.ViewCamera.WorldToViewportPoint(target);return new Vector2(at.x-.5f,at.y-.5f).magnitude;}

        // Whether two of the pass's controls share a row: their anchored spans overlap up the panel.
        static bool Overlaps(RectTransform a,RectTransform b)=>a.anchorMin.y<b.anchorMax.y&&b.anchorMin.y<a.anchorMax.y;
        static bool HandsShown()=>Object.FindObjectsByType<FestivalHands>(FindObjectsSortMode.None).Any(h=>h.gameObject.activeInHierarchy);

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
