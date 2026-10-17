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
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // SET-1: SETTINGS on the title and on the pass open one five-tab screen. Each change applies and saves at once, a save that
    // fails says so and keeps the saved value, and RESET TAB restores only the tab on show. Every test owns its profile folder.
    public sealed class SettingsPlayTests:FestivalPlayModeTest
    {
        private static readonly Dictionary<string,string[]> TabRows=new Dictionary<string,string[]>
        {
            {"AUDIO",new[]{"MASTER VOLUME","MUSIC VOLUME","EFFECTS VOLUME","VOICE VOLUME","MUTE WHEN UNFOCUSED"}},
            {"GRAPHICS",new[]{"DISPLAY MODE","RESOLUTION","QUALITY","RENDER SCALE","VSYNC","FRAME CAP","FIELD OF VIEW","BRIGHTNESS"}},
            {"CONTROLS",new[]{"LOOK SPEED","CAMERA VIEW"}},
            {"ACCESSIBILITY",new[]{"REDUCED MOTION","HIGH CONTRAST"}},
            {"VOICE",new string[0]},
        };
        private GameObject host;
        private FestivalSession session;

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
        private IEnumerator OpenTab(string tab){yield return Press(Pressable(tab,"Pass settings"));}
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
                        yield return OpenTab(tab.Key);
                        var screen=Screen();Canvas.ForceUpdateCanvases();
                        string where=tab.Key+" at "+Mathf.RoundToInt(scale*100)+"%";
                        var shown=screen.GetComponentsInChildren<Text>(false).Where(t=>t.name.EndsWith(" value")).Select(t=>t.name.Substring(0,t.name.Length-6)).ToArray();
                        if(!shown.SequenceEqual(tab.Value))failures.Add(where+": shows rows "+string.Join(", ",shown)+", expected "+string.Join(", ",tab.Value));
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
                        if(tab.Key=="VOICE"&&status?.text!=session.Voice.Status)failures.Add(where+": the voice status reads \""+status?.text+"\"");
                        if(tab.Key=="GRAPHICS"&&(status==null||!status.text.StartsWith("NOW ")))failures.Add(where+": no effective display status");
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
