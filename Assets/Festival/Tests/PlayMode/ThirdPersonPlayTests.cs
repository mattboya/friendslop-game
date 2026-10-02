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
    // person. Lying down still looks up from the grass, and the view goes back over your shoulder when you get up. The CAMERA VIEW
    // setting says which view you have and toggles it, and a camp shelf is still picked by where your eye aims, not the camera.
    // V and the toggle save the machine's own profile, so each test puts the setting back as it found it.
    public sealed class ThirdPersonPlayTests:FestivalPlayModeTest
    {
        const int Body=1<<31;

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
                void FirstPerson(string where,float height)
                {
                    var at=view.transform.position;
                    if(session.ThirdPersonView)failures.Add(where+": ThirdPersonView is still true");
                    if(Vector3.Distance(at,session.EyePosition)>.01f)failures.Add(where+": the camera at "+at.ToString("0.00")+" is not at the eye "+session.EyePosition.ToString("0.00"));
                    if(Mathf.Abs(at.y-height)>.1f)failures.Add(where+": the view is "+at.y.ToString("0.00")+" m up, not "+height+" m");
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

                // V again: first person, the hands back and your body hidden.
                yield return PressV();
                if(session.Profile.Data.ThirdPerson)failures.Add("V again does not switch the setting back to first person");
                FirstPerson("after V again",1.65f);
                if(!HandsShown())failures.Add("after V again: the first-person hands are hidden");
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{session.Profile.Data.ThirdPerson=wanted;session.Profile.Save();session.Leave();Object.Destroy(host);Object.Destroy(world);}
        }

        // At camp, before the level: the settings row, then a shelf picked from the eye while the camera hangs over the shoulder.
        [UnityTest]public IEnumerator TheSettingTogglesTheViewAndShelvesAreStillPickedByTheEye()
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

                // The pass's settings: CAMERA VIEW says which view you have, and its TOGGLE flips it.
                session.MenuOpen=true;yield return null;
                Assert.That(Click("SETTINGS"),Is.Not.Null,"setup: the pass has a SETTINGS button");
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

                // At camp's first two shelves, the eye aimed half a metre right of the first one's item. From the eye that is still the
                // first item; the camera, .45 m further right over your shoulder, looks nearer the second.
                var first=Catalog.FindItem(sim.State.VendorOffers[0]);var second=Catalog.FindItem(sim.State.VendorOffers[1]);
                sim.State.ShopStock.Find(s=>s.ItemId==first.Id).CampAvailable=1;
                var spot=Catalog.ShopPoint(true,0);player.X=spot.X;player.Z=6.3f;
                var aim=new Vector3(spot.X+.5f,1.17f,9f)-new Vector3(spot.X,1.65f,6.3f);
                typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg);
                typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,-Mathf.Asin(aim.normalized.y)*Mathf.Rad2Deg);
                yield return new WaitForSeconds(.6f);
                if(!session.ThirdPersonView)failures.Add("at the shelves: the view is not over the shoulder");
                string prompt=Find("Prompt")?.text??"(hidden)";
                if(!prompt.StartsWith("PICK UP "+first.Name.ToUpperInvariant()+"  "))failures.Add("at the shelves, the eye on the "+first.Name+" beside the "+second.Name+": the prompt reads \""+prompt+"\"");
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{session.Profile.Data.ThirdPerson=wanted;session.Profile.Save();session.Leave();Object.Destroy(host);Object.Destroy(world);}
        }

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
