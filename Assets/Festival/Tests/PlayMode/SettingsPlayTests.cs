using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Festival.Integrations;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // SET-1: SETTINGS on the title and on the pass open one five-tab screen. Each change applies and saves at once, a save that
    // fails says so and keeps the saved value, and RESET TAB restores only the tab on show. Every test owns its profile folder.
    public sealed class SettingsPlayTests:FestivalPlayModeTest
    {
        // Each tab, or CONTROLS section, and the rows it shows in order.
        private static readonly (string Tab,string Section,string[] Rows)[] TabRows=
        {
            ("AUDIO","",new[]{"MASTER VOLUME","MUSIC VOLUME","EFFECTS VOLUME","VOICE VOLUME","MUTE WHEN UNFOCUSED"}),
            ("GRAPHICS","",new[]{"DISPLAY MODE","RESOLUTION","QUALITY","RENDER SCALE","VSYNC","FRAME CAP","FIELD OF VIEW","BRIGHTNESS"}),
            ("CONTROLS","GENERAL",new[]{"LOOK SPEED","INVERT Y","CAMERA VIEW","RHYTHM TIMING"}),
            ("CONTROLS","MOVE",new[]{"MOVE FORWARD","MOVE BACK","MOVE LEFT","MOVE RIGHT","SPRINT","LOOK","SWITCH VIEW"}),
            ("CONTROLS","ACTIONS",new[]{"INTERACT","CHAT","USE","DROP","MAP","PUSH TO TALK"}),
            ("CONTROLS","HANDS",new[]{"SLOT 1","SLOT 2","SLOT 3","NEXT SLOT","SPIN IN FRONT","SPIN AT SIDES"}),
            ("CONTROLS","DANCE",new[]{"DANCE LEFT","DANCE DOWN","DANCE UP","DANCE RIGHT","DANCE LEFT ALT","DANCE DOWN ALT","DANCE UP ALT","DANCE RIGHT ALT"}),
            ("CONTROLS","LIGHT SHOW",new[]{"ANSWER 1","ANSWER 2","ANSWER 3","ANSWER 4","NOT SURE"}),
            ("ACCESSIBILITY","",new[]{"REDUCED MOTION","HIGH CONTRAST","CAMERA SHAKE","TEXT SIZE"}),
            ("VOICE","",new[]{"VOICE CHAT","MICROPHONE","MIC VOLUME","VOICE MODE","PUSH TO TALK","TEST MICROPHONE"}),
        };
        private GameObject host;
        private FestivalSession session;
        // No automated run opens the room's microphone: every HUD these tests build tests this fixture instead.
        private sealed class MicrophoneFixture:IFestivalMicrophone
        {
            public string[] Devices {get;}={"Desk Mic","Headset"};
            public string Blocked=>"";
            public bool Authorized=>true;
            public bool AuthorizationPending=>false;
            public int Requests,Starts,Ends;
            // The device being recorded, or null.
            public string Recording;
            public AudioClip Clip;
            public void RequestAuthorization()=>Requests++;
            public AudioClip Start(string device)
            {
                Starts++;Recording=device;
                var samples=new float[1600];for(int i=0;i<samples.Length;i++)samples[i]=i%2==0?.5f:-.5f;
                Clip=AudioClip.Create("impl-U73 fixture microphone",samples.Length,1,16000,false);Clip.SetData(samples,0);
                return Clip;
            }
            public int GetPosition(string device)=>800;
            public bool IsRecording(string device)=>Recording!=null&&Recording==device;
            public void End(string device){Ends++;if(Recording==device)Recording=null;}
        }
        private MicrophoneFixture microphone;
        private IFestivalMicrophone previousMicrophone;
        [SetUp]public void UseAMicrophoneFixture(){previousMicrophone=FestivalMicrophoneMonitor.Substitute;microphone=new MicrophoneFixture();FestivalMicrophoneMonitor.Substitute=microphone;}
        [TearDown]public void RestoreTheMicrophone(){FestivalMicrophoneMonitor.Substitute=previousMicrophone;}

        private IEnumerator Title()
        {
            host=new GameObject("impl-U72 settings HUD");session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();yield return null;
            var canvas=host.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);canvas.transform.localScale=Vector3.one;
        }
        private static string ProfileId()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--profile");
            return at>=0&&at+1<args.Length?args[at+1]:"default";
        }
        private Transform Screen()=>host.GetComponentsInChildren<Transform>(false).FirstOrDefault(t=>t.name=="Pass settings");
        private Button Pressable(string label,string on)
        {
            var button=host.GetComponentsInChildren<Button>(false).FirstOrDefault(b=>b.name==label&&b.transform.parent.name==on);
            Assert.That(button,Is.Not.Null,"a visible "+label+" button on "+on);return button;
        }
        private Text Value(string row)=>host.GetComponentsInChildren<Text>(false).FirstOrDefault(t=>t.name==row+" value"&&t.transform.parent.name=="Pass settings");
        // The row's own − / + / TOGGLE: the button beside its value.
        private Button RowButton(string row,string label)
        {
            var value=Value(row);Assert.That(value,Is.Not.Null,row+" is on the open tab");var band=value.rectTransform;
            var button=Screen().GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name==label&&b.transform.parent==band.parent&&b.gameObject.activeInHierarchy
                &&((RectTransform)b.transform).anchorMin.y<band.anchorMax.y&&band.anchorMin.y<((RectTransform)b.transform).anchorMax.y);
            Assert.That(button,Is.Not.Null,row+" has a "+label);return button;
        }
        private IEnumerator Press(Button button){button.onClick.Invoke();yield return null;yield return null;}
        private IEnumerator OpenTab(string tab,string section=""){yield return Press(Pressable(tab,"Pass settings"));if(section!="")yield return Press(Pressable(section,"Pass settings"));}
        private string Message()=>host.GetComponentsInChildren<Text>(false).First(t=>t.name=="Settings values").text;

        [UnityTest]public IEnumerator TheTitleAndThePassOpenTheSameScreenAndAChangeInOneShowsInTheOther()
        {
            yield return Title();
            try
            {
                yield return Press(Pressable("SETTINGS","Title menu"));
                var fromTitle=Screen();Assert.That(fromTitle,Is.Not.Null,"SETTINGS on the title opens the settings");
                yield return OpenTab("AUDIO");
                yield return Press(RowButton("MASTER VOLUME","−"));
                Assert.That(session.Profile.Data.MasterVolume,Is.EqualTo(.95f).Within(1e-4f));Assert.That(Value("MASTER VOLUME").text,Is.EqualTo("95%"));
                Assert.That(new LocalProfile(ProfileId()).Data.MasterVolume,Is.EqualTo(.95f).Within(1e-4f),"the title's change is saved at once");
                yield return Press(Pressable("BACK TO PASS","Pass settings"));
                Assert.That(Screen(),Is.Null,"BACK puts the title's settings away");

                session.Host("Settings tester",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                session.MenuOpen=true;yield return null;
                yield return Press(Pressable("SETTINGS","Pass home"));
                Assert.That(Screen(),Is.SameAs(fromTitle),"the pass opens the very screen the title opened");
                yield return OpenTab("AUDIO");
                Assert.That(Value("MASTER VOLUME").text,Is.EqualTo("95%"),"the title's change shows on the pass");
                yield return OpenTab("GRAPHICS");
                yield return Press(RowButton("FIELD OF VIEW","+"));
                Assert.That(Value("FIELD OF VIEW").text,Is.EqualTo("80°"));
                session.MenuOpen=false;yield return null;yield return null;
                Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(80).Within(.01f),"the pass's change applies to the view at once");

                session.Leave();yield return null;yield return null;
                yield return Press(Pressable("SETTINGS","Title menu"));
                Assert.That(Screen(),Is.SameAs(fromTitle));
                yield return OpenTab("GRAPHICS");
                Assert.That(Value("FIELD OF VIEW").text,Is.EqualTo("80°"),"the pass's change shows on the title");
                Assert.That(new LocalProfile(ProfileId()).Data.FieldOfView,Is.EqualTo(80));
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);}
        }

        [UnityTest]public IEnumerator EachTabShowsOnlyItsOwnRowsAndEveryLabelAndButtonFitsAt100And130Percent()
        {
            yield return Title();
            var failures=new List<string>();
            try
            {
                yield return Press(Pressable("SETTINGS","Title menu"));
                foreach(float scale in new[]{1f,1.3f})
                {
                    session.Profile.Data.TextScale=scale;Assert.That(session.Settings.Save(),Is.Empty);yield return null;
                    foreach(var tab in TabRows)
                    {
                        yield return OpenTab(tab.Tab,tab.Section);
                        var screen=Screen();Canvas.ForceUpdateCanvases();
                        string where=(tab.Tab+" "+tab.Section).Trim()+" at "+Mathf.RoundToInt(scale*100)+"%";
                        var shown=screen.GetComponentsInChildren<Text>(false).Where(t=>t.name.EndsWith(" value")).Select(t=>t.name.Substring(0,t.name.Length-6)).ToArray();
                        if(!shown.SequenceEqual(tab.Rows))failures.Add(where+": shows rows "+string.Join(", ",shown)+", expected "+string.Join(", ",tab.Rows));
                        bool sections=screen.GetComponentsInChildren<Button>(false).Any(b=>b.name=="DANCE");
                        if(sections!=(tab.Tab=="CONTROLS"))failures.Add(where+(sections?": shows":": hides")+" the CONTROLS sections");
                        foreach(var label in new[]{"RESET TAB","SHOW TIPS AGAIN","BACK TO PASS"})
                            if(!screen.GetComponentsInChildren<Button>(false).Any(b=>b.name==label))failures.Add(where+": no "+label);
                        foreach(var text in screen.GetComponentsInChildren<Text>(false))
                        {
                            int visible=Mathf.Max(0,text.cachedTextGenerator.characterCountVisible);
                            if(visible<text.text.Length&&text.text.Substring(visible).Trim().Length>0)failures.Add(where+": "+text.name+" cuts \""+text.text+"\" in "+text.rectTransform.rect.size);
                        }
                        var panel=((RectTransform)screen).rect;
                        var buttons=screen.GetComponentsInChildren<Button>(false).Select(b=>(RectTransform)b.transform).ToArray();
                        foreach(var button in buttons)
                        {
                            var corners=new Vector3[4];button.GetWorldCorners(corners);
                            foreach(var corner in corners)
                            {
                                var local=screen.InverseTransformPoint(corner);
                                if(local.x<panel.xMin-1||local.x>panel.xMax+1||local.y<panel.yMin-1||local.y>panel.yMax+1){failures.Add(where+": "+button.name+" leaves the screen");break;}
                            }
                            if(button.rect.height<36)failures.Add(where+": "+button.name+" is only "+button.rect.height+" px tall");
                            foreach(var other in buttons)
                                if(other!=button&&button.parent==other.parent&&Overlap(button,other))failures.Add(where+": "+button.name+" overlaps "+other.name);
                        }
                        var status=screen.GetComponentsInChildren<Text>(false).FirstOrDefault(t=>t.name=="Settings status");
                        if(tab.Tab=="VOICE"&&status?.text!=session.Voice.Status)failures.Add(where+": the voice status reads \""+status?.text+"\"");
                        if(tab.Tab=="GRAPHICS"&&(status==null||!status.text.StartsWith("NOW ")))failures.Add(where+": no effective display status");
                    }
                }
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);}
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }
        private static bool Overlap(RectTransform a,RectTransform b)
        {
            var ac=new Vector3[4];var bc=new Vector3[4];a.GetWorldCorners(ac);b.GetWorldCorners(bc);
            return ac[0].x<bc[2].x-.5f&&bc[0].x<ac[2].x-.5f&&ac[0].y<bc[2].y-.5f&&bc[0].y<ac[2].y-.5f;
        }

        [UnityTest]public IEnumerator AudioAndGraphicsButtonsApplyAndSaveAtOnceAndResetOnlyTheirTab()
        {
            yield return Title();
            try
            {
                var data=session.Profile.Data;
                yield return Press(Pressable("SETTINGS","Title menu"));
                yield return OpenTab("AUDIO");
                yield return Press(RowButton("MUSIC VOLUME","+"));yield return Press(RowButton("EFFECTS VOLUME","−"));
                yield return Press(RowButton("VOICE VOLUME","−"));yield return Press(RowButton("MUTE WHEN UNFOCUSED","TOGGLE"));
                Assert.That((data.MusicVolume,data.EffectsVolume,data.VoiceVolume,data.MuteWhenUnfocused),Is.EqualTo((.4f,.55f,.95f,false)));
                Assert.That(Value("MUSIC VOLUME").text,Is.EqualTo("40%"));Assert.That(Value("MUTE WHEN UNFOCUSED").text,Is.EqualTo("OFF"));
                var saved=new LocalProfile(ProfileId()).Data;
                Assert.That((saved.MusicVolume,saved.EffectsVolume,saved.VoiceVolume,saved.MuteWhenUnfocused),Is.EqualTo((.4f,.55f,.95f,false)),"every audio change is saved at once");

                yield return OpenTab("GRAPHICS");
                Assert.That(Value("QUALITY").text,Is.EqualTo("HIGH"));
                yield return Press(RowButton("QUALITY","−"));
                Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()],Is.EqualTo("Medium"),"the quality applies at once");
                Assert.That(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale,Is.EqualTo(.9f).Within(1e-4f));
                Assert.That(Value("RENDER SCALE").text,Is.EqualTo("90%"),"the preset's render scale shows beside it");
                yield return Press(RowButton("RENDER SCALE","+"));
                Assert.That(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale,Is.EqualTo(.95f).Within(1e-4f));
                yield return Press(RowButton("VSYNC","+"));yield return Press(RowButton("VSYNC","+"));
                Assert.That(Value("VSYNC").text,Is.EqualTo("ON"));Assert.That(QualitySettings.vSyncCount,Is.EqualTo(1));
                Assert.That(RowButton("FRAME CAP","+").interactable,Is.False,"vsync paces the frames, so the cap waits");
                StringAssert.Contains("FRAME RATE SET BY VSYNC",host.GetComponentsInChildren<Text>(false).First(t=>t.name=="Settings status").text);
                yield return Press(RowButton("VSYNC","−"));
                Assert.That(RowButton("FRAME CAP","+").interactable,Is.True);
                yield return Press(RowButton("FRAME CAP","+"));
                Assert.That(Value("FRAME CAP").text,Is.EqualTo("120 FPS"));Assert.That(Application.targetFrameRate,Is.EqualTo(120));
                StringAssert.Contains("CAPPED AT 120 FPS",host.GetComponentsInChildren<Text>(false).First(t=>t.name=="Settings status").text);
                yield return Press(RowButton("BRIGHTNESS","+"));
                Assert.That(data.Brightness,Is.EqualTo(.1f).Within(1e-4f));Assert.That(Value("BRIGHTNESS").text,Is.EqualTo("+0.1"));
                saved=new LocalProfile(ProfileId()).Data;
                Assert.That((saved.QualityPreset,saved.VSyncCount,saved.FrameCap,saved.Brightness),Is.EqualTo(("Medium",0,120,.1f)));

                yield return Press(Pressable("RESET TAB","Pass settings"));
                Assert.That((data.QualityPreset,data.VSyncCount,data.FrameCap,data.Brightness,data.RenderScale),Is.EqualTo(("High",-1,60,0f,1f)),"RESET TAB restores the graphics defaults");
                Assert.That(data.MusicVolume,Is.EqualTo(.4f).Within(1e-4f),"RESET TAB on GRAPHICS keeps the audio choices");
                Assert.That(Message(),Does.Contain("GRAPHICS reset"));
                yield return OpenTab("AUDIO");
                yield return Press(Pressable("RESET TAB","Pass settings"));
                var defaults=new ProfileData();
                Assert.That((data.MusicVolume,data.EffectsVolume,data.VoiceVolume,data.MuteWhenUnfocused),Is.EqualTo((defaults.MusicVolume,defaults.EffectsVolume,defaults.VoiceVolume,defaults.MuteWhenUnfocused)));
                Assert.That(new LocalProfile(ProfileId()).Data.MusicVolume,Is.EqualTo(defaults.MusicVolume),"the reset is saved");
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);}
        }

        [UnityTest]public IEnumerator AFailedSaveShowsItsErrorAndKeepsTheSavedValue()
        {
            yield return Title();
            string blocked=Path.Combine(LocalProfile.Folder,ProfileId()+".json");
            try
            {
                yield return Press(Pressable("SETTINGS","Title menu"));
                yield return OpenTab("AUDIO");
                if(File.Exists(blocked))File.Delete(blocked);
                Directory.CreateDirectory(blocked);
                yield return Press(RowButton("MUSIC VOLUME","+"));
                Assert.That(session.Profile.Data.MusicVolume,Is.EqualTo(.35f).Within(1e-4f),"a change that cannot be saved is undone");
                Assert.That(Value("MUSIC VOLUME").text,Is.EqualTo("35%"));
                var message=host.GetComponentsInChildren<Text>(false).First(t=>t.name=="Settings values");
                Assert.That(message.text,Is.EqualTo("Settings could not be saved."),"the screen says why");
                Assert.That(message.color.r,Is.GreaterThan(message.color.g+.2f),"the error reads as an error");
                yield return OpenTab("GRAPHICS");
                Assert.That(Message(),Is.EqualTo("Settings could not be saved."),"the error stays up until the next change");
                yield return Press(Pressable("RESET TAB","Pass settings"));
                Assert.That(Message(),Is.EqualTo("Settings could not be saved."),"a reset that cannot be saved says so too");
                Directory.Delete(blocked);
                yield return OpenTab("AUDIO");
                yield return Press(RowButton("MUSIC VOLUME","+"));
                Assert.That(Message(),Is.EqualTo(FestivalSettingsScreen.SavedMessage));
                Assert.That(new LocalProfile(ProfileId()).Data.MusicVolume,Is.EqualTo(.4f).Within(1e-4f));
            }
            finally{if(Directory.Exists(blocked))Directory.Delete(blocked);session.Leave();UnityEngine.Object.Destroy(host);}
        }

        [UnityTest]public IEnumerator TheControlsTabShowsEveryEffectiveKeyRebindsARealOneAndResetRestoresIt()
        {
            UseAVirtualKeyboard("impl-U73 settings keyboard");
            yield return Title();
            try
            {
                yield return Press(Pressable("SETTINGS","Title menu"));
                yield return OpenTab("CONTROLS","MOVE");
                Assert.That(new[]{"MOVE FORWARD","MOVE BACK","MOVE LEFT","MOVE RIGHT","LOOK"}.Select(r=>Value(r).text),Is.EqualTo(new[]{"W","S","A","D","MOUSE"}),"each movement part and the pointer axis shows its binding");
                yield return OpenTab("CONTROLS","DANCE");
                // The keyboard layout names the arrow key ("LEFT" here, "LEFT ARROW" on others).
                StringAssert.StartsWith("LEFT",Value("DANCE LEFT").text,"a lane shows its arrow");
                Assert.That(Value("DANCE LEFT ALT").text,Is.EqualTo("A"),"and its second key");
                yield return OpenTab("CONTROLS","HANDS");
                Assert.That(new[]{"SPIN IN FRONT","SPIN AT SIDES"}.Select(r=>Value(r)?.text),Is.EqualTo(new[]{"LMB","RMB"}),"left click spins the poi in front, right click at your sides");
                yield return OpenTab("CONTROLS","ACTIONS");
                Assert.That(Value("PUSH TO TALK").text,Is.EqualTo("T"));
                yield return Press(RowButton("INTERACT","REBIND"));
                Assert.That(Value("INTERACT").text,Is.EqualTo("PRESS A KEY"));
                yield return Tap(Key.F,.3f);
                Assert.That(session.Controls.Rebinding,Is.False,"the real key completes the rebind");
                Assert.That((Value("INTERACT").text,Message()),Is.EqualTo(("F","CHAT already uses F. Rebind one of them.")));
                Assert.That(session.Controls.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/f"),"the new key works at once");
                Assert.That(NewTestControls(ProfileId()).Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/f"),"and is saved");
                yield return Press(RowButton("INTERACT","REBIND"));yield return Tap(Key.Escape,.3f);
                Assert.That((Value("INTERACT").text,session.Controls.Rebinding),Is.EqualTo(("F",false)),"Esc cancels a rebind");
                Assert.That(Screen(),Is.Not.Null,"and leaves the settings open");
                yield return OpenTab("CONTROLS","GENERAL");
                yield return Press(RowButton("INVERT Y","TOGGLE"));
                Assert.That((Value("INVERT Y").text,new LocalProfile(ProfileId()).Data.InvertY),Is.EqualTo(("ON",true)));
                yield return Press(Pressable("RESET TAB","Pass settings"));
                Assert.That((session.Controls.Interact.bindings[0].effectivePath,session.Profile.Data.InvertY),Is.EqualTo(("<Keyboard>/e",false)),"RESET TAB restores the keys and the look");
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);}
        }

        [UnityTest]public IEnumerator TheTapTestTimesRealDanceKeysAgainstItsScheduledBeatsAndSavesTheResult()
        {
            UseAVirtualKeyboard("impl-U73 tap keyboard");
            yield return Title();
            try
            {
                yield return Press(Pressable("SETTINGS","Title menu"));
                yield return OpenTab("CONTROLS","GENERAL");
                Assert.That(Value("RHYTHM TIMING").text,Is.EqualTo("0 MS"));
                double start=AudioSettings.dspTime+.25;
                yield return Press(RowButton("RHYTHM TIMING","TAP TEST"));
                var pulses=host.GetComponents<AudioSource>().FirstOrDefault(a=>a.clip!=null&&a.clip.name=="Festival calibration pulses");
                Assert.That(pulses,Is.Not.Null,"TAP TEST schedules the pulses on the HUD's own source");
                Assert.That(RowButton("RHYTHM TIMING","TAP TEST").GetComponentInChildren<Text>().text,Is.EqualTo("STOP"));
                for(int beat=0;beat<5;beat++)
                {
                    for(float deadline=Time.realtimeSinceStartup+3;AudioSettings.dspTime<start+beat*FestivalTapCalibration.BeatSeconds+.02&&Time.realtimeSinceStartup<deadline;)yield return null;
                    yield return Tap(beat%2==0?Key.LeftArrow:Key.D);
                }
                Assert.That(Value("RHYTHM TIMING").text,Is.EqualTo("TAP 5 / 8"),"a dance key on each beat counts once");
                for(float deadline=Time.realtimeSinceStartup+6;pulses.clip!=null&&Time.realtimeSinceStartup<deadline;)yield return null;
                StringAssert.StartsWith("Rhythm timing saved",Message());
                float saved=new LocalProfile(ProfileId()).Data.TimingCalibrationMs;
                Assert.That(saved,Is.InRange(-60f,160f),"the median of real presses against the scheduled beats");
                StringAssert.EndsWith(Mathf.Abs(Mathf.RoundToInt(saved))+" MS",Value("RHYTHM TIMING").text);
                yield return Press(RowButton("RHYTHM TIMING","TAP TEST"));
                Assert.That(pulses.clip,Is.Not.Null);
                yield return OpenTab("CONTROLS","MOVE");
                Assert.That(pulses.clip,Is.Null,"another section ends the test");
                Assert.That(new LocalProfile(ProfileId()).Data.TimingCalibrationMs,Is.EqualTo(saved),"an unfinished test changes nothing");
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);}
        }

        [UnityTest]public IEnumerator AccessibilityAddsShakeAndTextSizeAndKeepsTheFirstLaunchChoice()
        {
            yield return Title();
            try
            {
                Assert.That(session.Settings.CompleteComfortChoice(true),Is.Empty,"the first-launch YES");
                yield return Press(Pressable("SETTINGS","Title menu"));
                yield return OpenTab("ACCESSIBILITY");
                Assert.That(Value("REDUCED MOTION").text,Is.EqualTo("ON"),"the first-launch choice shows where its prompt says");
                var heading=host.GetComponentsInChildren<Text>(false).First(t=>t.name=="Settings heading");int before=heading.fontSize;
                yield return Press(RowButton("CAMERA SHAKE","TOGGLE"));
                yield return Press(RowButton("TEXT SIZE","+"));yield return Press(RowButton("TEXT SIZE","+"));
                Assert.That((Value("CAMERA SHAKE").text,Value("TEXT SIZE").text),Is.EqualTo(("OFF","120%")));
                Assert.That(heading.fontSize,Is.GreaterThan(before),"larger text applies at once");
                var saved=new LocalProfile(ProfileId()).Data;
                Assert.That((saved.CameraShake,saved.TextScale,saved.ReducedMotion),Is.EqualTo((false,1.2f,true)));
                yield return Press(Pressable("RESET TAB","Pass settings"));
                saved=new LocalProfile(ProfileId()).Data;var defaults=new ProfileData();
                Assert.That((saved.CameraShake,saved.TextScale,saved.ReducedMotion),Is.EqualTo((defaults.CameraShake,defaults.TextScale,defaults.ReducedMotion)));
                Assert.That(saved.ComfortPromptComplete,Is.True,"a reset never asks the first-launch question again");
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);}
        }

        [UnityTest]public IEnumerator TheMicrophoneTestStartsOnlyFromTestAndStopsWhenItsTabScreenFocusOrHudGoes()
        {
            yield return Title();
            try
            {
                yield return Press(Pressable("SETTINGS","Title menu"));
                yield return OpenTab("VOICE");
                Assert.That(Value("VOICE CHAT").text,Is.EqualTo(FestivalSettingsScreen.VoiceExplanation));
                Assert.That((microphone.Starts,microphone.Requests,Value("TEST MICROPHONE").text),Is.EqualTo((0,0,"OFF")),"opening the tab opens nothing");
                yield return Press(RowButton("TEST MICROPHONE","TEST"));
                Assert.That((microphone.Starts,Value("TEST MICROPHONE").text),Is.EqualTo((1,"50%")));
                Assert.That(RowButton("TEST MICROPHONE","TEST").GetComponentInChildren<Text>().text,Is.EqualTo("STOP"));
                var meter=host.GetComponentsInChildren<Image>(false).FirstOrDefault(i=>i.name=="Microphone level");
                Assert.That(meter,Is.Not.Null,"a level meter shows");Assert.That(meter.rectTransform.rect.width,Is.GreaterThan(1));
                Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.clip==microphone.Clip),Is.False,"the meter never plays the microphone back");
                yield return OpenTab("AUDIO");
                Assert.That((microphone.Ends,microphone.Recording),Is.EqualTo((1,(string)null)),"another tab stops it");
                yield return OpenTab("VOICE");yield return Press(RowButton("TEST MICROPHONE","TEST"));
                yield return Press(Pressable("BACK TO PASS","Pass settings"));
                Assert.That((microphone.Ends,microphone.Recording),Is.EqualTo((2,(string)null)),"closing the settings stops it");
                yield return Press(Pressable("SETTINGS","Title menu"));yield return OpenTab("VOICE");
                Assert.That(microphone.Starts,Is.EqualTo(2),"reopening does not restart it");
                yield return Press(RowButton("TEST MICROPHONE","TEST"));
                host.SendMessage("OnApplicationFocus",false);yield return null;
                Assert.That((microphone.Ends,microphone.Recording),Is.EqualTo((3,(string)null)),"losing focus stops it");
                host.SendMessage("OnApplicationFocus",true);
                yield return Press(Pressable("BACK TO PASS","Pass settings"));

                session.Host("Microphone tester",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                session.MenuOpen=true;yield return null;
                yield return Press(Pressable("SETTINGS","Pass home"));yield return OpenTab("VOICE");
                yield return Press(RowButton("TEST MICROPHONE","TEST"));Assert.That(microphone.Recording,Is.EqualTo(""));
                session.MenuOpen=false;yield return null;yield return null;
                Assert.That((microphone.Ends,microphone.Recording),Is.EqualTo((4,(string)null)),"putting the pass away stops it");
                Assert.That(session.Voice.Available,Is.False,"the test never makes voice available");
                session.MenuOpen=true;yield return null;yield return null;
                yield return Press(RowButton("TEST MICROPHONE","TEST"));Assert.That(microphone.Recording,Is.EqualTo(""));
                UnityEngine.Object.Destroy(host);yield return null;
                Assert.That((microphone.Ends,microphone.Recording),Is.EqualTo((5,(string)null)),"tearing the HUD down stops it");
            }
            finally{if(host!=null){session.Leave();UnityEngine.Object.Destroy(host);}}
        }

        private sealed class Display:IFestivalDisplay
        {
            public int Width {get;private set;}=800;public int Height {get;private set;}=600;
            public FullScreenMode Mode {get;private set;}=FullScreenMode.Windowed;
            public bool SupportsExclusiveFullscreen=>false;
            public Resolution[] Resolutions=>new[]{new Resolution{width=1920,height=1080},new Resolution{width=1280,height=720},new Resolution{width=1920,height=1080}};
            public bool Deferred;
            private (int,int,FullScreenMode) pending;
            public void SetResolution(int width,int height,FullScreenMode mode){pending=(width,height,mode);if(!Deferred)Acknowledge();}
            public void Acknowledge(){(Width,Height,Mode)=pending;}
        }
        [Test]public void DisplayRowsStepThroughReportedChoicesAndTheStatusShowsWhatThePlatformApplied()
        {
            var profile=new LocalProfile("impl-U72-display");var display=new Display();var settings=new FestivalSettings(profile,display);
            var screen=new FestivalSettingsScreen(profile,settings,new UnavailableVoiceSession());
            FestivalSettingsScreen.Row Row(string label)=>FestivalSettingsScreen.Rows.First(r=>r.Label==label);
            try
            {
                settings.ApplyGraphics();screen.Open(FestivalSettingsScreen.Tab.Graphics);
                var resolution=Row("RESOLUTION");var mode=Row("DISPLAY MODE");
                Assert.That(screen.Value(resolution),Is.EqualTo("CURRENT"));
                var seen=new List<string>();
                for(int i=0;i<4;i++){Assert.That(screen.Change(resolution,1),Is.Empty);seen.Add(screen.Value(resolution));}
                Assert.That(seen,Is.EqualTo(new[]{"1920 × 1080","1280 × 720","800 × 600","800 × 600"}),"the reported sizes once each, then the window's own");
                Assert.That((display.Width,display.Height),Is.EqualTo((800,600)));
                screen.Change(resolution,-1);Assert.That((display.Width,display.Height),Is.EqualTo((1280,720)),"a size applies at once");
                StringAssert.StartsWith("NOW 1280 × 720 WINDOWED",screen.Status);
                display.Deferred=true;
                screen.Change(mode,1);screen.Change(mode,1);screen.Change(mode,1);
                Assert.That(screen.Value(mode),Is.EqualTo("FULLSCREEN"),"the saved choice");
                StringAssert.StartsWith("NOW 1280 × 720 WINDOWED",screen.Status,"until the platform answers, the status shows what it still does");
                display.Acknowledge();
                StringAssert.StartsWith("NOW 1280 × 720 BORDERLESS",screen.Status,"exclusive fullscreen falls back to borderless here, and the status says so");
                Assert.That(new LocalProfile("impl-U72-display").Data.DisplayMode,Is.EqualTo("Fullscreen"));
                screen.Open(FestivalSettingsScreen.Tab.Audio);Assert.That(screen.Status,Is.Empty);
                screen.Open(FestivalSettingsScreen.Tab.Voice);Assert.That(screen.Status,Is.EqualTo(new UnavailableVoiceSession().Status),"the voice tab is honest that voice is unavailable");
            }
            finally{settings.Dispose();}
        }
    }
}
