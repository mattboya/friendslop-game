using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using System.Linq;
using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class SettingsTests
    {
        private string previousFolder,ownedFolder,previousPreferencesScope;
        private int qualityLevel;
        private readonly List<string> ownedBindingKeys=new List<string>();
        [SetUp]public void OwnATemporaryProfile()
        {
            qualityLevel=QualitySettings.GetQualityLevel();previousFolder=LocalProfile.Folder;
            ownedFolder=Path.Combine(Path.GetTempPath(),"impl-U04-settings-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ownedFolder);LocalProfile.Folder=ownedFolder;
            previousPreferencesScope=FestivalInput.PreferencesScope;FestivalInput.PreferencesScope="impl-U35-"+Guid.NewGuid().ToString("N");
            ownedBindingKeys.Add("festival-bindings-"+FestivalInput.PreferencesScope+"-settings");
        }
        [TearDown]public void RemoveOnlyThisTestsProfile()
        {
            int left=QualitySettings.GetQualityLevel();QualitySettings.SetQualityLevel(qualityLevel,false);
            LocalProfile.Folder=previousFolder;FestivalInput.PreferencesScope=previousPreferencesScope;
            foreach(string key in ownedBindingKeys)PlayerPrefs.DeleteKey(key);ownedBindingKeys.Clear();
            if(Directory.Exists(ownedFolder))Directory.Delete(ownedFolder,true);
            // The editor saves its quality level into ProjectSettings, so each test must leave the level it found.
            Assert.That(QualitySettings.names[left],Is.EqualTo(QualitySettings.names[qualityLevel]),"the test leaves the editor quality level it found");
        }
        [UnityTearDown]public IEnumerator LeaveOwnedPlayMode()
        {
            if(Application.isPlaying)yield return new ExitPlayMode();
        }
        private void Write(string json)=>File.WriteAllText(Path.Combine(ownedFolder,"settings.json"),json);

        [Test]public void AuthoredGrammarKeysSurviveSaveWithoutWideningOtherIds()
        {
            const string key="reply.talk.cosmic_0123456789abcdef";
            var profile=new LocalProfile("settings");profile.Data.SeenDialogue=new List<string>{key,key,"bad/key",new string('x',65),"with space"};
            profile.Data.SeenTips=new List<string>{key,"safe_tip"};Assert.That(profile.Save(),Is.Empty);
            var reloaded=new LocalProfile("settings");Assert.That(reloaded.Data.SeenDialogue,Is.EqualTo(new[]{key}),"actual authored dotted grammar keys survive Save and reload");
            Assert.That(reloaded.Data.SeenTips,Is.EqualTo(new[]{"safe_tip"}),"only dialogue keys admit dots");
        }

        [Test]public void SavedProfileBindingsAreAuthoritativeAcrossInputRecreation()
        {
            var profile=new LocalProfile("settings");
            using(var input=new FestivalInput("settings"))
            {
                input.Move.ApplyBindingOverride(1,"<Keyboard>/i");
                input.Interact.ApplyBindingOverride(0,"<Keyboard>/x");
                input.Notes[2].ApplyBindingOverride(0,"<Keyboard>/u");
                input.Notes[2].ApplyBindingOverride(1,"<Keyboard>/j");
                input.Cycle.ApplyBindingOverride(0,"<Mouse>/scroll/x");
                input.View.ApplyBindingOverride(0,"<Keyboard>/b");
                profile.Data.BindingOverridesJson=input.Map.SaveBindingOverridesAsJson();Assert.That(profile.Save(),Is.Empty);
            }
            string key="festival-bindings-"+FestivalInput.PreferencesScope+"-settings";ownedBindingKeys.Add(key);
            using(var legacy=new FestivalInput("unused"))
            {
                legacy.Interact.ApplyBindingOverride(0,"<Keyboard>/f");PlayerPrefs.SetString(key,legacy.Map.SaveBindingOverridesAsJson());
            }
            using(var input=new FestivalInput("settings"))
            {
                Assert.That(input.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/x"),"profile JSON takes priority over stale machine preferences");
                Assert.That(input.Move.bindings[1].effectivePath,Is.EqualTo("<Keyboard>/i"));
                Assert.That(input.Notes[2].bindings[0].effectivePath,Is.EqualTo("<Keyboard>/u"));
                Assert.That(input.NoteKey(2),Is.EqualTo("J"));
                Assert.That(input.Cycle.bindings[0].effectivePath,Is.EqualTo("<Mouse>/scroll/x"));
                Assert.That(input.View.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/b"));
                Assert.That(FestivalHudText.HowToPlay(input)[1].Text,Does.Contain("(X)"),"the actual how-to consumer names the effective action");
            }
        }

        [Test]public void AuthoredActionAndBindingIdentitiesRemainStableAndUnique()
        {
            using(var first=new FestivalInput("settings"))using(var second=new FestivalInput("settings"))
            {
                var identities=new HashSet<Guid>();
                foreach(var action in first.Map.actions)
                {
                    var next=second.Map.FindAction(action.name,true);
                    Assert.That(next.id,Is.EqualTo(action.id),"authored action "+action.name);
                    Assert.That(identities.Add(action.id),Is.True);
                    for(int index=0;index<action.bindings.Count;index++)
                    {
                        Assert.That(next.bindings[index].id,Is.EqualTo(action.bindings[index].id),action.name+" binding "+index);
                        Assert.That(identities.Add(action.bindings[index].id),Is.True);
                    }
                }
            }
        }

        [Test]public void LegacyPreferencesImportOnlyUnambiguousActionsAndRemainUnchanged()
        {
            string key="festival-bindings-"+FestivalInput.PreferencesScope+"-settings";ownedBindingKeys.Add(key);
            string legacy;
            using(var map=new InputActionMap("Festival"))
            {
                map.AddAction("Interact",InputActionType.Button,"<Keyboard>/e").ApplyBindingOverride("<Keyboard>/x");
                var move=map.AddAction("Move",InputActionType.Value);move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w");move.ApplyBindingOverride(1,"<Keyboard>/i");
                var rhythm=map.AddAction("Rhythm2",InputActionType.Button,"<Keyboard>/upArrow");rhythm.AddBinding("<Keyboard>/w");rhythm.ApplyBindingOverride(1,"<Keyboard>/j");
                map.AddAction("Cycle",InputActionType.Value,"<Mouse>/scroll/y").ApplyBindingOverride("<Mouse>/scroll/x");
                legacy=map.SaveBindingOverridesAsJson();
            }
            PlayerPrefs.SetString(key,legacy);
            using(var input=new FestivalInput("settings"))
            {
                Assert.That(input.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/x"));
                Assert.That(input.Cycle.bindings[0].effectivePath,Is.EqualTo("<Mouse>/scroll/x"));
                Assert.That(input.Move.bindings[1].effectivePath,Is.EqualTo("<Keyboard>/w"),"old random IDs do not identify a composite part");
                Assert.That(input.NoteKey(2),Is.EqualTo("W"),"old rhythm IDs cannot identify which alternate to replace");
            }
            Assert.That(PlayerPrefs.GetString(key),Is.EqualTo(legacy),"migration is read-only for legacy storage");
            Assert.That(new LocalProfile("settings").Data.BindingOverridesJson,Is.Not.Empty,"the official map JSON becomes authoritative");
            var profile=new LocalProfile("settings");profile.Data.BindingOverridesJson="{\"bindings\":[]}";Assert.That(profile.Save(),Is.Empty);
            using(var input=new FestivalInput("settings"))Assert.That(input.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/e"),"an explicit empty profile override blocks legacy reimport");
            PlayerPrefs.SetString(key,"not json");
            using(var input=new FestivalInput("other")) { }
            Assert.That(PlayerPrefs.GetString(key),Is.EqualTo("not json"));
        }

        [Test]public void InteractiveRebindSavesTheProfileAndCancelPreservesIt()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                bool completed=false;
                using(var input=new FestivalInput("settings"))
                {
                    input.Rebind(input.Interact,0,()=>completed=true);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.X));InputSystem.Update();
                    System.Threading.Thread.Sleep(160);InputSystem.Update();
                    Assert.That(completed,Is.True,"a real injected keyboard completes the production rebind");
                    Assert.That(input.Rebinding,Is.False);Assert.That(input.Map.enabled,Is.True);
                }
                var profile=new LocalProfile("settings");Assert.That(profile.Data.BindingOverridesJson,Is.Not.Empty,"completion saves into the profile, not PlayerPrefs");
                string saved=profile.Data.BindingOverridesJson;
                using(var input=new FestivalInput("settings"))
                {
                    Assert.That(input.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/x"));
                    input.Map.Disable();input.Rebind(input.Chat,0,null);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();
                    Assert.That(input.Rebinding,Is.False);Assert.That(input.Map.enabled,Is.False,"cancel restores the caller's map state");
                }
                Assert.That(new LocalProfile("settings").Data.BindingOverridesJson,Is.EqualTo(saved));
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }

        [Test]public void BindingClashesRespectMovementSharingAndControlsResetIsLive()
        {
            var profile=new LocalProfile("settings");using(var input=new FestivalInput("settings",profile))
            {
                var clashes=typeof(FestivalInput).GetMethod("BindingClashes");Assert.That(clashes,Is.Not.Null,"the settings consumer needs real overlap warnings");
                string Warn(InputAction action,int index,string path)=>(string)clashes.Invoke(input,new object[]{action,index,path});
                Assert.That(Warn(input.Interact,0,"<Keyboard>/f"),Does.Contain("Chat"));
                Assert.That(Warn(input.Move,1,"<Keyboard>/w"),Is.Empty,"movement and lanes are intentionally separate contexts");
                Assert.That(Warn(input.Notes[0],1,"<Keyboard>/s"),Does.Contain("Rhythm1"));
                input.Interact.ApplyBindingOverride("<Keyboard>/x");profile.Data.BindingOverridesJson=input.Map.SaveBindingOverridesAsJson();
                var settings=new FestivalSettings(profile);
                var attach=typeof(FestivalSettings).GetMethod("AttachInput");Assert.That(attach,Is.Not.Null);attach.Invoke(settings,new object[]{input});
                Assert.That(settings.Save(),Is.Empty);Assert.That(settings.ResetTab("Controls"),Is.Empty);
                Assert.That(input.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/e"),"reset reaches the actual live action map");
                Assert.That(profile.Data.BindingOverridesJson,Is.EqualTo("{\"bindings\":[]}"),"persist empty official JSON to prevent reimport");
                using(var recreated=new FestivalInput("settings"))Assert.That(recreated.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/e"));
            }
        }

        // The Light Show's four choices and Not sure are five rebindable keys, 1-5 by default, each with a row in CONTROLS'
        // LIGHT SHOW section. They share 1-3 with the hand slots, which do nothing while a question is up, so that is no clash; any
        // other action on one of those keys is.
        [Test]public void TheLightShowAnswersAreFiveRebindableKeysWithSettingsRows()
        {
            var profile=new LocalProfile("settings");
            using(var input=new FestivalInput("settings",profile))
            using(var settings=new FestivalSettings(profile))
            {
                var screen=new FestivalSettingsScreen(profile,settings,new Festival.Integrations.UnavailableVoiceSession(),input,null,new MicrophoneMonitorTests.Fixture());
                string[] names={"Answer1","Answer2","Answer3","Answer4","AnswerUnsure"},labels={"ANSWER 1","ANSWER 2","ANSWER 3","ANSWER 4","NOT SURE"};
                for(int k=0;k<names.Length;k++)
                {
                    var action=input.Map.FindAction(names[k]);
                    Assert.That(action,Is.Not.Null,names[k]+" is an action of its own");
                    Assert.That(action.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/"+(k+1)),names[k]+" starts on "+(k+1));
                    var row=FestivalSettingsScreen.Rows.FirstOrDefault(r=>r.Tab==FestivalSettingsScreen.Tab.Controls&&r.Label==labels[k]);
                    Assert.That(row?.Section,Is.EqualTo("LIGHT SHOW"),labels[k]+" has a row in the LIGHT SHOW section");
                    Assert.That((screen.Value(row),screen.ButtonLabel(row,0)),Is.EqualTo(((k+1).ToString(),"REBIND")));
                }
                Assert.That(FestivalSettingsScreen.Sections,Does.Contain("LIGHT SHOW"));
                Assert.That(input.BindingClashes(input.Slot1,0),Is.Empty,"hand slot 1 and answer 1 share 1 by design");
                Assert.That(input.BindingClashes(input.Map.FindAction("Answer4"),0,"<Keyboard>/e"),Does.Contain("Interact"),"any other overlap is shown");
            }
        }

        [Test]public void RebindingInteractToFOnTheControlsTabWarnsThatChatAlreadyUsesF()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();var profile=new LocalProfile("settings");
            try
            {
                using(var input=new FestivalInput("settings",profile))
                using(var settings=new FestivalSettings(profile))
                {
                    settings.AttachInput(input);
                    var screen=new FestivalSettingsScreen(profile,settings,new Festival.Integrations.UnavailableVoiceSession(),input,null,new MicrophoneMonitorTests.Fixture());
                    var interact=FestivalSettingsScreen.Rows.First(r=>r.Tab==FestivalSettingsScreen.Tab.Controls&&r.Label=="INTERACT");
                    screen.Open(FestivalSettingsScreen.Tab.Controls);
                    Assert.That(screen.Shows(interact),Is.False,"keys wait in their own section");
                    screen.OpenSection("ACTIONS");
                    Assert.That((screen.Shows(interact),screen.Value(interact),screen.ButtonLabel(interact,0)),Is.EqualTo((true,"E","REBIND")));
                    Assert.That(screen.Change(interact,1),Is.Empty);
                    Assert.That((input.Rebinding,screen.Value(interact)),Is.EqualTo((true,"PRESS A KEY")));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));InputSystem.Update();
                    System.Threading.Thread.Sleep(160);InputSystem.Update();
                    Assert.That((input.Rebinding,screen.Value(interact)),Is.EqualTo((false,"F")),"the real key press completes the rebind");
                    Assert.That(screen.Message,Is.EqualTo("CHAT already uses F. Rebind one of them."));Assert.That(screen.Failed,Is.False);
                    using(var recreated=new FestivalInput("settings"))Assert.That(recreated.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/f"),"the rebind is saved");
                    Assert.That(screen.ResetTab(),Is.Empty);
                    Assert.That(screen.Value(interact),Is.EqualTo("E"),"RESET TAB restores the default key");
                }
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }

        [Test]public void ScheduledTapCalibrationMeasuresAudioAndRestoresItsSource()
        {
            var source=new GameObject("impl-U35 calibration audio").AddComponent<AudioSource>();
            var original=AudioClip.Create("impl-U35 caller audio",240,1,24000,false);source.clip=original;source.volume=.3f;source.pitch=.7f;source.loop=true;
            var profile=new LocalProfile("settings");profile.Data.TimingCalibrationMs=-20;
            using(var settings=new FestivalSettings(profile))
            {
                var start=typeof(FestivalSettings).GetMethod("StartTapCalibration");Assert.That(start,Is.Not.Null,"calibration must schedule actual audio, not ask for a manual guess");
                IDisposable calibration=null;
                try
                {
                    double before=AudioSettings.dspTime;calibration=(IDisposable)start.Invoke(settings,new object[]{source});
                    var type=calibration.GetType();double at=(double)type.GetProperty("ScheduledStartDspSeconds").GetValue(calibration);
                    Assert.That(at,Is.GreaterThan(before));Assert.That(source.clip,Is.Not.SameAs(original));Assert.That(source.loop,Is.False);Assert.That(source.pitch,Is.EqualTo(1));
                    var samples=new float[source.clip.samples];source.clip.GetData(samples,0);
                    Assert.That(samples[24],Is.Not.Zero,"the scheduled clip contains a real first pulse");Assert.That(samples[12024],Is.Not.Zero,"a second pulse is half a second later");Assert.That(samples[6000],Is.Zero,"the space between pulses stays silent");
                    var tap=type.GetMethod("Tap");
                    Assert.That((bool)tap.Invoke(calibration,new object[]{double.NaN}),Is.False);
                    Assert.That((bool)tap.Invoke(calibration,new object[]{at-.5}),Is.False);
                    Assert.That((string)type.GetMethod("Complete").Invoke(calibration,null),Is.Not.Empty,"one accidental tap cannot set calibration");
                    foreach(double time in new[]{at+.06,at+.56,at+1.06,at+1.56,at+2.06,at+2.56,at+3.06,at+3.56})Assert.That((bool)tap.Invoke(calibration,new object[]{time}),Is.True);
                    Assert.That((bool)tap.Invoke(calibration,new object[]{at+3.56}),Is.False,"a beat counts once");
                    Assert.That((string)type.GetMethod("Complete").Invoke(calibration,null),Is.Empty);
                    Assert.That(new LocalProfile("settings").Data.TimingCalibrationMs,Is.EqualTo(60).Within(.01));
                }
                finally{calibration?.Dispose();UnityEngine.Object.DestroyImmediate(source.gameObject);UnityEngine.Object.DestroyImmediate(original);}
            }
        }

        [Test]public void CalibrationCancellationRestoresCallerAudioAndNeverSaves()
        {
            var source=new GameObject("impl-U35 cancelled calibration").AddComponent<AudioSource>();
            var original=AudioClip.Create("impl-U35 original",240,1,24000,false);source.clip=original;source.pitch=.8f;source.volume=.2f;source.loop=true;source.spatialBlend=.4f;
            var profile=new LocalProfile("settings");profile.Data.TimingCalibrationMs=37;Assert.That(profile.Save(),Is.Empty);
            try
            {
                var settings=new FestivalSettings(profile);var start=typeof(FestivalSettings).GetMethod("StartTapCalibration");Assert.That(start,Is.Not.Null);
                start.Invoke(settings,new object[]{source});settings.Dispose();
                Assert.That(source.clip,Is.SameAs(original));Assert.That(source.pitch,Is.EqualTo(.8f));Assert.That(source.volume,Is.EqualTo(.2f));Assert.That(source.loop,Is.True);Assert.That(source.spatialBlend,Is.EqualTo(.4f));
                Assert.That(new LocalProfile("settings").Data.TimingCalibrationMs,Is.EqualTo(37));
            }
            finally{UnityEngine.Object.DestroyImmediate(source.gameObject);UnityEngine.Object.DestroyImmediate(original);}
        }

        [TestCase(false,.173648f)] [TestCase(true,-.173648f)]
        public void SavedInvertYChangesTheActualViewCamera(bool inverted,float expectedHeight)
        {

            var host=new GameObject("impl-U35 inverted camera");var session=host.AddComponent<FestivalSession>();
            try
            {
                if(session.Profile==null)typeof(FestivalSession).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,null);
                session.Profile.Data.InvertY=inverted;session.Profile.Data.MouseSensitivity=.1f;session.Profile.Data.ThirdPerson=false;
                session.ApplyLook(new Vector2(0,100));
                typeof(FestivalSession).GetMethod("UpdateCamera",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new object[]{new PlayerState{Id="me"}});
                Assert.That(session.ViewCamera.transform.forward.y,Is.EqualTo(expectedHeight).Within(.001));
            }
            finally{GraphicsApplicationTests.Close(host,session);}
        }

        [UnityTest]public IEnumerator SavedPushToTalkUsesTAndStopsAtFocusMenuAndRebindBoundaries()
        {
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U35-voice-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            LocalProfile.Folder=playFolder;FestivalInput.PreferencesScope=playFolder;
            var previousMode=InputSystem.settings.updateMode;InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            var previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;var previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var host=new GameObject("impl-U35 local voice input");
            try
            {
                var session=host.AddComponent<FestivalSession>();if(session.Profile==null)typeof(FestivalSession).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,null);
                session.enabled=false;session.Host("Input test",8641);Assert.That(session.Connected,Is.True,session.Message);session.MenuOpen=false;
                session.Profile.Data.VoiceMode="PushToTalk";
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.T));InputSystem.Update();
                Assert.That(keyboard.tKey.isPressed,Is.True,"fixture delivered the player T key");Assert.That(session.Controls.PushToTalk.IsPressed(),Is.True);
                Assert.That(session.UpdateLocalVoice(true),Is.True);
                Assert.That(session.UpdateLocalVoice(false),Is.False);
                session.MenuOpen=true;Assert.That(session.UpdateLocalVoice(true),Is.False);session.MenuOpen=false;
                var talk=session.Controls.PushToTalk;
                Assert.That(session.Controls.Label(talk),Is.EqualTo("T"));talk.ApplyBindingOverride("<Keyboard>/y");
                session.Profile.Data.BindingOverridesJson=session.Controls.Map.SaveBindingOverridesAsJson();Assert.That(session.Profile.Save(),Is.Empty);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.T));InputSystem.Update();Assert.That(session.UpdateLocalVoice(true),Is.False);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Y));InputSystem.Update();Assert.That(session.UpdateLocalVoice(true),Is.True);
                session.Controls.Rebind(session.Controls.Interact,0,null);Assert.That(session.UpdateLocalVoice(true),Is.False);session.Controls.CancelRebind();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();session.Profile.Data.VoiceMode="OpenMic";
                Assert.That(session.UpdateLocalVoice(true),Is.True);Assert.That(session.UpdateLocalVoice(false),Is.False);
                Assert.That(session.Voice.Available,Is.False,"input never makes the unconfigured voice provider available");
                foreach(string device in Microphone.devices)Assert.That(Microphone.IsRecording(device),Is.False,"push-to-talk config never captures room audio");
                using(var recreated=new FestivalInput("default"))Assert.That(recreated.Map.FindAction("PushToTalk").bindings[0].effectivePath,Is.EqualTo("<Keyboard>/y"));
            }
            finally{UnityEngine.Object.DestroyImmediate(host);InputSystem.RemoveDevice(keyboard);InputSystem.settings.updateMode=previousMode;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;InputSystem.settings.backgroundBehavior=previousBackground;LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;Directory.Delete(playFolder,true);}
            yield return new ExitPlayMode();
        }

        [UnityTest]public IEnumerator TextScaleReflowsHostedBriefingAndActionHitAreasWithoutMovingRhythm()
        {
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U36-text-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            LocalProfile.Folder=playFolder;FestivalInput.PreferencesScope=playFolder;
            var host=new GameObject("impl-U36 text reflow HUD");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            UnityEngine.UI.Text Label(string name)=>host.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t=>t.name==name);
            RectTransform Card(string name)=>host.GetComponentsInChildren<RectTransform>(true).First(t=>t.name==name);
            void Fits(UnityEngine.UI.Text label)
            {
                Canvas.ForceUpdateCanvases();int visible=label.cachedTextGenerator.characterCountVisible;
                Assert.That(visible>=label.text.Length||label.text.Substring(visible).Trim().Length==0,Is.True,label.name+" clips at "+visible+"/"+label.text.Length+" in "+label.rectTransform.rect.size);
            }
            try
            {
                yield return null;session.Host("Text reader",0);yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var game=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=game.Player(session.LocalPlayerId);game.State.Phase="Playing";game.State.LevelIndex=3;game.State.ElapsedSeconds=1;game.State.DurationSeconds=600;game.State.TripperId=player.Id;
                player.X=0;player.Z=0;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                game.State.Npcs.Clear();
                var canvas=host.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);canvas.transform.localScale=Vector3.one;
                session.MenuOpen=false;yield return new WaitForSeconds(.3f);
                Assert.That(Card("Level briefing").gameObject.activeInHierarchy,Is.True,"actual hosted HUD shows its level briefing");
                int font=Label("Briefing job").fontSize;float height=Card("Level briefing").rect.height;
                var well=Card("Receptor well 0");var originalWell=(well.anchorMin,well.anchorMax,well.sizeDelta);
                var note=Card("Notes");var originalNotes=(note.anchorMin,note.anchorMax,note.sizeDelta);
                session.MenuOpen=true;yield return new WaitForSeconds(.3f);
                var button=host.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="RESUME FESTIVAL");
                Assert.That(button.gameObject.activeInHierarchy,Is.True,"the actual hosted pass exposes its resume action");
                float hitHeight=((RectTransform)button.transform).rect.height;
                session.Profile.Data.TextScale=1.3f;Assert.That(session.Settings.Save(),Is.Empty);yield return null;yield return null;
                Assert.That(((RectTransform)button.transform).rect.height,Is.GreaterThan(hitHeight),"real menu action hit areas grow with the saved text size");
                session.MenuOpen=false;yield return null;yield return null;
                Assert.That(Label("Briefing job").fontSize,Is.GreaterThan(font),"the actual authored briefing consumes text scale");
                Assert.That(Card("Level briefing").rect.height,Is.GreaterThan(height),"the card reflows for larger text");
                Fits(Label("Briefing goal"));Fits(Label("Briefing job"));Fits(Label("Briefing stakes"));
                Assert.That((well.anchorMin,well.anchorMax,well.sizeDelta),Is.EqualTo(originalWell),"receptor geometry is unchanged");
                Assert.That((note.anchorMin,note.anchorMax,note.sizeDelta),Is.EqualTo(originalNotes),"note geometry is unchanged");
                session.Profile.Data.TextScale=1;Assert.That(session.Settings.Save(),Is.Empty);yield return null;yield return null;
                Assert.That(Label("Briefing job").fontSize,Is.EqualTo(font),"returning to 100 percent is idempotent");
                Assert.That(Card("Level briefing").rect.height,Is.EqualTo(height).Within(.1f));
            }
            finally
            {
                host.SetActive(false);session.Leave();UnityEngine.Object.DestroyImmediate(host);LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;Directory.Delete(playFolder,true);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]public IEnumerator LargerTextKeepsTheBusyHostedCardsAndEveryHelpBlockReadable()
        {
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U36-cards-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            LocalProfile.Folder=playFolder;FestivalInput.PreferencesScope=playFolder;
            var host=new GameObject("impl-U36 busy card HUD");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            var failures=new List<string>();
            void Fits(UnityEngine.UI.Text text)
            {
                Canvas.ForceUpdateCanvases();int visible=Mathf.Max(0,text.cachedTextGenerator.characterCountVisible);
                if(visible<text.text.Length&&text.text.Substring(visible).Trim().Length>0)failures.Add(text.name+" clips at "+visible+"/"+text.text.Length+" in "+text.rectTransform.rect.size+" at font "+text.cachedTextGenerator.fontSizeUsedForBestFit);
            }
            try
            {
                yield return null;session.Host("A Long Reader Name",0);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var game=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=game.Player(session.LocalPlayerId);
                for(int i=1;i<8;i++)game.AddPlayer("impl_U36_busy_"+i,"Long Festival Friend "+i);
                game.State.Phase="Playing";game.State.FestivalIndex=1;game.State.LevelIndex=3;game.State.DurationSeconds=600;game.State.ElapsedSeconds=30;game.State.TripperId=player.Id;
                game.State.Doses.Clear();foreach(var p in game.State.Players){p.X=20;p.Z=20;game.State.Doses.Add(new PlayerDose{PlayerId=p.Id,Dose=4,Substance="mushrooms"});}
                player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=3});player.EquippedItemId="stock_lsd";
                game.State.Npcs.Clear();
                var canvas=host.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);canvas.transform.localScale=Vector3.one;
                session.Profile.Data.TextScale=1.3f;Assert.That(session.Settings.Save(),Is.Empty);session.MenuOpen=false;
                session.SetMessage("Your friend found the missing festivalgoer. Get everyone home together.");yield return new WaitForSeconds(.3f);
                var names=new[]{"Objective title","Objective detail","Vitals","Tripping","Roster","Equipment heading","Slot label","Notice"};
                foreach(var text in host.GetComponentsInChildren<UnityEngine.UI.Text>(false))if(names.Contains(text.name))Fits(text);
                session.MenuOpen=true;yield return null;
                host.GetComponentsInChildren<UnityEngine.UI.Button>(false).First(b=>b.name=="HOW TO PLAY").onClick.Invoke();yield return null;yield return null;
                foreach(var text in host.GetComponentsInChildren<UnityEngine.UI.Text>(false))if(text.name.StartsWith("How to play text"))Fits(text);
                Assert.That(failures,Is.Empty,"actual rendered 130 percent text:\n"+string.Join("\n",failures));
            }
            finally{if(host!=null){host.SetActive(false);session?.Leave();UnityEngine.Object.DestroyImmediate(host);}LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;if(Directory.Exists(playFolder))Directory.Delete(playFolder,true);}
            yield return new ExitPlayMode();
        }

        private static ushort ComfortPort()
        {
            using(var probe=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Any,0)))
                return (ushort)((System.Net.IPEndPoint)probe.Client.LocalEndPoint).Port;
        }
        private static UnityEngine.UI.Button ComfortButton(GameObject root,string name)
        {
            var button=root.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(b=>b.name==name);
            Assert.That(button,Is.Not.Null,"actual title widget "+name+" exists before invoking its callback");return button;
        }
        private static void SetComfortPort(GameObject root,ushort port)
        {
            foreach(var field in root.GetComponentsInChildren<UnityEngine.UI.InputField>(true))if(field.name=="PORT")field.text=port.ToString();
        }

        [UnityTest]public IEnumerator TheTitleSavesEitherFirstLaunchChoiceBeforeHostingAndKeepsItOnReload()
        {
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U36-choice-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            GameObject host=null;
            try
            {
                foreach(bool reduce in new[]{false,true})
                {
                    string folder=Path.Combine(playFolder,reduce?"reduced":"full");Directory.CreateDirectory(folder);LocalProfile.Folder=folder;FestivalInput.PreferencesScope=folder;
                    host=new GameObject("impl-U36 first-launch title");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();yield return null;
                    Assert.That(session.Profile.Data.ReducedMotion||session.Profile.Data.HighContrast||session.Profile.Data.ComfortPromptComplete,Is.False,"new-profile defaults remain the shipped full-effects choice until informed consent");
                    SetComfortPort(host,ComfortPort());ComfortButton(host,"PLAY").onClick.Invoke();yield return null;
                    ComfortButton(host,"HOST A GAME").onClick.Invoke();yield return null;
                    Assert.That(session.Connected||session.Connecting,Is.False,"the actual title callback cannot start gameplay before the comfort choice is saved");
                    var choice=ComfortButton(host,reduce?"YES, REDUCE MOTION":"NO, KEEP FULL EFFECTS");Assert.That(choice,Is.Not.Null,"the title presents both explicit first-launch choices");
                    Assert.That(choice.gameObject.activeInHierarchy,Is.True);choice.onClick.Invoke();yield return null;
                    Assert.That(session.Connected,Is.True,session.Message);
                    var saved=new LocalProfile("default");Assert.That(saved.Data.ComfortPromptComplete,Is.True);Assert.That(saved.Data.ReducedMotion,Is.EqualTo(reduce));Assert.That(saved.Data.HighContrast,Is.False);
                    host.SetActive(false);session.Leave();UnityEngine.Object.DestroyImmediate(host);host=null;yield return null;
                    host=new GameObject("impl-U36 saved-choice title");session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();yield return null;
                    Assert.That(session.Profile.Data.ReducedMotion,Is.EqualTo(reduce));
                    Assert.That(ComfortButton(host,"YES, REDUCE MOTION").gameObject.activeInHierarchy,Is.False,"saved choices are never asked again");
                    UnityEngine.Object.DestroyImmediate(host);host=null;yield return null;
                }
            }
            finally{if(host!=null){host.SetActive(false);UnityEngine.Object.DestroyImmediate(host);}LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;Directory.Delete(playFolder,true);}
            yield return new ExitPlayMode();
        }

        [UnityTest]public IEnumerator AFirstLaunchSaveErrorStaysVisibleAndBlocksThePendingTitleHost()
        {
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U36-choice-error-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            LocalProfile.Folder=playFolder;FestivalInput.PreferencesScope=playFolder;
            GameObject host=null;FestivalSession session=null;
            try
            {
                host=new GameObject("impl-U36 comfort save error");
                session=host.AddComponent<FestivalSession>();
                Assert.That(session,Is.Not.Null,"fixture session component was created");
                Assert.That(session.Profile,Is.Not.Null,"fixture session Awake initialized its owned profile");
                var hud=host.AddComponent<FestivalHud>();
                Assert.That(hud,Is.Not.Null,"fixture HUD component was created");
                Assert.That(host.GetComponentInChildren<Canvas>(true),Is.Not.Null,"fixture HUD Awake built real widgets");
                yield return null;Assert.That(session.Profile,Is.Not.Null);Assert.That(host.GetComponentInChildren<Canvas>(true),Is.Not.Null);SetComfortPort(host,ComfortPort());ComfortButton(host,"PLAY").onClick.Invoke();yield return null;ComfortButton(host,"HOST A GAME").onClick.Invoke();yield return null;
                Assert.That(session.Connected||session.Connecting,Is.False,"title connection waits for a saved choice");
                string blocked=Path.Combine(playFolder,"default.json");Directory.CreateDirectory(blocked);
                var choice=ComfortButton(host,"YES, REDUCE MOTION");Assert.That(choice,Is.Not.Null);choice.onClick.Invoke();yield return null;
                Assert.That(session.Settings.LastError,Is.Not.Empty);Assert.That(session.Connected||session.Connecting,Is.False);
                Assert.That(session.Profile.Data.ComfortPromptComplete,Is.False,"failed persistence cannot mark the prompt complete");
                Assert.That(session.Profile.Data.ReducedMotion,Is.False,"failed choices restore the previous in-memory preference");
                bool errorVisible=false;
                foreach(var text in host.GetComponentsInChildren<UnityEngine.UI.Text>(false))if(text.text.Contains(session.Settings.LastError))errorVisible=true;
                Assert.That(errorVisible,Is.True,"the actual visible prompt explains why play is waiting");
                Directory.Delete(blocked);choice.onClick.Invoke();yield return null;
                Assert.That(session.Connected,Is.True,session.Message);Assert.That(new LocalProfile("default").Data.ReducedMotion,Is.True);
            }
            finally{if(host!=null){host.SetActive(false);session?.Leave();UnityEngine.Object.DestroyImmediate(host);}LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;if(Directory.Exists(playFolder))Directory.Delete(playFolder,true);}
            yield return new ExitPlayMode();
        }

        [UnityTest]public IEnumerator TheTitleJoinWaitsForItsOwnSavedChoiceWithoutGatingTheHostTransport()
        {
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U36-join-choice-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            string serverFolder=Path.Combine(playFolder,"server"),clientFolder=Path.Combine(playFolder,"client");Directory.CreateDirectory(serverFolder);Directory.CreateDirectory(clientFolder);
            LocalProfile.Folder=serverFolder;FestivalInput.PreferencesScope=serverFolder;var server=new GameObject("impl-U36 choice server");var owner=server.AddComponent<FestivalSession>();
            LocalProfile.Folder=clientFolder;FestivalInput.PreferencesScope=clientFolder;var client=new GameObject("impl-U36 first-launch join title");var session=client.AddComponent<FestivalSession>();client.AddComponent<FestivalHud>();
            try
            {
                ushort port=ComfortPort();owner.Host("Transport owner",port);yield return null;Assert.That(owner.Connected,Is.True,"headless transport retains its existing compatible seam");
                SetComfortPort(client,port);ComfortButton(client,"PLAY").onClick.Invoke();ComfortButton(client,"JOIN A FRIEND").onClick.Invoke();ComfortButton(client,"JOIN").onClick.Invoke();yield return null;
                Assert.That(session.Connected||session.Connecting,Is.False,"the actual join callback waits for this player's informed choice");
                ComfortButton(client,"NO, KEEP FULL EFFECTS").onClick.Invoke();
                for(float deadline=Time.realtimeSinceStartup+15;!session.Connected&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.Connected,Is.True,session.Message);Assert.That(new LocalProfile("default").Data.ComfortPromptComplete,Is.True);Assert.That(new LocalProfile("default").Data.ReducedMotion,Is.False);
                Assert.That(owner.Profile.Data.ComfortPromptComplete,Is.False,"local consent never becomes a network-wide preference gate");
            }
            finally{client.SetActive(false);server.SetActive(false);session.Leave();owner.Leave();UnityEngine.Object.DestroyImmediate(client);UnityEngine.Object.DestroyImmediate(server);LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;Directory.Delete(playFolder,true);}
            yield return new ExitPlayMode();
        }

        [UnityTest]public IEnumerator MigratedComfortChoicesReachTheTitleUnchangedWithoutAnotherPrompt()
        {
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U36-migrated-choice-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            LocalProfile.Folder=playFolder;FestivalInput.PreferencesScope=playFolder;File.WriteAllText(Path.Combine(playFolder,"default.json"),"{\"SchemaVersion\":1,\"ReducedMotion\":true,\"HighContrast\":false}");
            var host=new GameObject("impl-U36 migrated title");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            try
            {
                yield return null;var choice=ComfortButton(host,"YES, REDUCE MOTION");Assert.That(choice,Is.Not.Null);
                Assert.That(choice.gameObject.activeInHierarchy,Is.False);Assert.That(session.Profile.Data.ReducedMotion,Is.True);Assert.That(session.Profile.Data.HighContrast,Is.False);
                SetComfortPort(host,ComfortPort());ComfortButton(host,"PLAY").onClick.Invoke();ComfortButton(host,"HOST A GAME").onClick.Invoke();yield return null;
                Assert.That(session.Connected,Is.True,session.Message);Assert.That(new LocalProfile("default").Data.ReducedMotion,Is.True);Assert.That(new LocalProfile("default").Data.HighContrast,Is.False);
            }
            finally{if(host!=null){host.SetActive(false);session?.Leave();UnityEngine.Object.DestroyImmediate(host);}LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;if(Directory.Exists(playFolder))Directory.Delete(playFolder,true);}
            yield return new ExitPlayMode();
        }

        [Test]public void HowToPlayNamesTheEffectiveReboundPointerAction()
        {
            using(var input=new FestivalInput("settings"))
            {
                input.Look.ApplyBindingOverride("<Gamepad>/rightStick");
                Assert.That(FestivalHudText.HowToPlay(input)[5].Text,Does.Contain(input.Label(input.Look)+"  look"),"the live how-to consumer must use the effective pointer binding");
            }
        }

        [UnityTest]public IEnumerator PointerAxesAndCompositeRebindsReadRealInjectedDevices()
        {
            yield return new EnterPlayMode();
            string playFolder=Path.Combine(Path.GetTempPath(),"impl-U35-pointer-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(playFolder);
            LocalProfile.Folder=playFolder;FestivalInput.PreferencesScope=playFolder;
            var previousMode=InputSystem.settings.updateMode;InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            var previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;var previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                using(var input=new FestivalInput("settings"))
                {
                    bool completed=false;input.Rebind(input.Look,0,()=>completed=true);
                    InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(20,10)});InputSystem.Update();System.Threading.Thread.Sleep(160);InputSystem.Update();
                    Assert.That(completed,Is.True,"look rebinding must accept actual mouse delta instead of excluding it");
                    completed=false;input.Rebind(input.Cycle,0,()=>completed=true);
                    InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(120,0)});InputSystem.Update();System.Threading.Thread.Sleep(160);InputSystem.Update();
                    Assert.That(completed,Is.True);Assert.That(input.Cycle.bindings[0].effectivePath,Is.EqualTo("<Mouse>/scroll/x"));
                    InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(60,0)});InputSystem.Update();Assert.That(mouse.scroll.x.ReadValue(),Is.EqualTo(60),"fixture delivered the player scroll event");Assert.That(input.Cycle.ReadValue<float>(),Is.EqualTo(60));
                    completed=false;input.Rebind(input.Move,1,()=>completed=true);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.I));InputSystem.Update();System.Threading.Thread.Sleep(160);InputSystem.Update();
                    Assert.That(completed,Is.True);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.I));InputSystem.Update();
                    Assert.That(input.Move.ReadValue<Vector2>().y,Is.EqualTo(1),"the real composite reads the rebound Up key");
                }
                using(var input=new FestivalInput("settings"))
                {
                    Assert.That(input.Move.bindings[1].effectivePath,Is.EqualTo("<Keyboard>/i"));Assert.That(input.Cycle.bindings[0].effectivePath,Is.EqualTo("<Mouse>/scroll/x"));
                }
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);InputSystem.settings.updateMode=previousMode;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;InputSystem.settings.backgroundBehavior=previousBackground;
                LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;Directory.Delete(playFolder,true);}
            yield return new ExitPlayMode();
        }

        [Test]public void DiscoveryMergeFiltersDeduplicatesAndDoesNotWriteAnUnchangedCollection()
        {
            var profile=new LocalProfile("settings");profile.Data.FoundCampEggIds.AddRange(new[]{"gnome","gnome","unknown",null});
            var game=new FestivalSimulation(0);var owner=game.AddPlayer("owner","Owner");var friend=game.AddPlayer("friend","Friend");
            owner.FoundCampEggMask=1;friend.FoundCampEggMask=254;
            Assert.That(profile.RememberCampEggs(FestivalSession.ViewFor(game,owner.Id),owner.Id),Is.Empty);
            Assert.That(profile.Data.FoundCampEggIds,Is.EqualTo(new[]{"gnome"}),"even an already-known mask normalizes only the owner's collection");
            Assert.That(new LocalProfile("settings").Data.FoundCampEggIds,Is.EqualTo(new[]{"gnome"}));
            string path=Path.Combine(ownedFolder,"settings.json");var stamp=new DateTime(2001,2,3,4,5,6,DateTimeKind.Utc);File.SetLastWriteTimeUtc(path,stamp);
            Assert.That(profile.RememberCampEggs(FestivalSession.ViewFor(game,owner.Id),owner.Id),Is.Empty);
            Assert.That(File.GetLastWriteTimeUtc(path),Is.EqualTo(stamp),"following the same discovery does not save every frame");
            owner.FoundCampEggMask=162;
            Assert.That(profile.RememberCampEggs(FestivalSession.ViewFor(game,owner.Id),owner.Id),Is.Empty);
            Assert.That(new LocalProfile("settings").Data.FoundCampEggIds,Is.EquivalentTo(new[]{"gnome","button","flamingo","wash_me"}),"a newer mask unions discoveries with earlier visits");
            profile.Data.FoundCampEggIds=null;owner.FoundCampEggMask=1;
            Assert.That(profile.RememberCampEggs(FestivalSession.ViewFor(game,owner.Id),owner.Id),Is.Empty);
            Assert.That(profile.Data.FoundCampEggIds,Is.EqualTo(new[]{"gnome"}),"a missing collection uses the schema-two empty default");
        }

        [Test]public void AFailedDiscoverySaveKeepsThePreviousCollectionAndCanRetry()
        {
            var profile=new LocalProfile("settings");profile.Data.FoundCampEggIds.Add("gnome");Assert.That(profile.Save(),Is.Empty);
            string path=Path.Combine(ownedFolder,"settings.json");string previous=File.ReadAllText(path);
            File.Delete(path);Directory.CreateDirectory(path);
            var game=new FestivalSimulation(0);var owner=game.AddPlayer("owner","Owner");owner.FoundCampEggMask=2;
            Assert.That(profile.RememberCampEggs(FestivalSession.ViewFor(game,owner.Id),owner.Id),Is.Not.Empty,"I/O failure must be visible to the session");
            Assert.That(profile.Data.FoundCampEggIds,Is.EqualTo(new[]{"gnome"}),"failed atomic save cannot mark the discovery as already persisted");
            Directory.Delete(path);File.WriteAllText(path,previous);
            Assert.That(profile.RememberCampEggs(FestivalSession.ViewFor(game,owner.Id),owner.Id),Is.Empty,"the same cumulative mask retries once storage works again");
            Assert.That(new LocalProfile("settings").Data.FoundCampEggIds,Is.EqualTo(new[]{"gnome","button"}));
            Assert.That(Directory.GetFiles(ownedFolder,"*.tmp"),Is.Empty,"each save cleans only its own staging file");
            File.WriteAllText(path,"{\"SchemaVersion\":3,\"FoundCampEggIds\":[\"flamingo\"]}");string future=File.ReadAllText(path);
            owner.FoundCampEggMask=255;
            Assert.That(profile.RememberCampEggs(FestivalSession.ViewFor(game,owner.Id),owner.Id),Does.Contain("newer build"));
            Assert.That(File.ReadAllText(path),Is.EqualTo(future),"discovery cannot overwrite a future schema");
            Assert.That(profile.Data.FoundCampEggIds,Is.EqualTo(new[]{"gnome","button"}));
        }

        [Test]public void ThePassCollectionContractCountsOnlyKnownIdsAndUsesTheExactCompletionLine()
        {
            var profile=new LocalProfile("settings");
            string Text(string name)=>(string)(typeof(LocalProfile).GetProperty(name)?.GetValue(profile)??"");
            Assert.That(Text("CampEggCountText"),Is.EqualTo("EASTER EGGS 0 / 8"),"the pass has an exact empty-collection contract");
            Assert.That(Text("CampEggCompletionText"),Is.Empty);
            profile.Data.FoundCampEggIds.AddRange(new[]{"gnome","button","chair","gnome","unknown",null});
            Assert.That(Text("CampEggCountText"),Is.EqualTo("EASTER EGGS 3 / 8"));Assert.That(Text("CampEggCompletionText"),Is.Empty);
            var game=new FestivalSimulation(0);var owner=game.AddPlayer("owner","Owner");owner.FoundCampEggMask=255;
            Assert.That(profile.RememberCampEggs(FestivalSession.ViewFor(game,owner.Id),owner.Id),Is.Empty);
            profile=new LocalProfile("settings");
            Assert.That(Text("CampEggCountText"),Is.EqualTo("EASTER EGGS 8 / 8"));
            Assert.That(Text("CampEggCompletionText"),Is.EqualTo("You found every dumb secret. Seek help."));
            Write("{\"SchemaVersion\":1,\"FoundCampEggIds\":[\"gnome\",\"button\"]}");profile=new LocalProfile("settings");
            Assert.That(Text("CampEggCountText"),Is.EqualTo("EASTER EGGS 0 / 8"),"the single SET-1 migration initializes legacy profiles empty");
            Assert.That(Text("CampEggCompletionText"),Is.Empty);Assert.That(profile.Data.SchemaVersion,Is.EqualTo(2));
        }

        [Test]public void SessionTeardownRetainsTheLastConfiguredMasterAndFocusChoice()
        {
            float previousVolume=AudioListener.volume;var host=new GameObject("impl:U08 session audio teardown");
            try
            {
                var session=host.AddComponent<FestivalSession>();var profile=new LocalProfile("audio");
                typeof(FestivalSession).GetProperty("Profile").SetValue(session,profile);
                profile.Data.MasterVolume=.3f;profile.Data.MuteWhenUnfocused=false;
                var trip=host.AddComponent<FestivalTrip>();trip.Apply(null,"me",null,null,false,0,0);
                Assert.That(AudioListener.volume,Is.EqualTo(.3f).Within(1e-5f));
                UnityEngine.Object.DestroyImmediate(session);
                typeof(FestivalTrip).GetMethod("OnDisable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(trip,null);
                Assert.That(AudioListener.volume,Is.EqualTo(.3f).Within(1e-5f),"trip teardown after its session still retains master");
                profile.Data.MuteWhenUnfocused=true;
                typeof(FestivalTrip).GetMethod("OnApplicationFocus",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(trip,new object[]{false});
                Assert.That(AudioListener.volume,Is.Zero,"the retained profile also retains focus mute");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);AudioListener.volume=previousVolume;}
        }

        [Test]public void SavedVoiceConfigurationReachesTheProviderWithoutStartingVoice()
        {
            float previousVolume=AudioListener.volume;
            var host=new GameObject("impl:U08 saved voice settings");
            try
            {
                var session=host.AddComponent<FestivalSession>();var profile=new LocalProfile("audio");
                typeof(FestivalSession).GetProperty("Profile").SetValue(session,profile);
                var data=profile.Data;data.MasterVolume=.5f;data.VoiceVolume=.4f;data.InputGain=.2f;data.MicrophoneDevice="impl:U08 chosen microphone";data.VoiceMode="OpenMic";
                string status=session.Voice.Status;var devices=Microphone.devices;
                var trip=host.AddComponent<FestivalTrip>();trip.Apply(null,"me",null,null,false,0,0);
                var configured=session.Voice.GetType().GetProperty("Configuration");
                Assert.That(configured,Is.Not.Null,"the provider must receive saved voice configuration");
                object value=configured.GetValue(session.Voice);
                object Setting(string name)=>value.GetType().GetField(name).GetValue(value);
                Assert.That(Setting("MicrophoneDevice"),Is.EqualTo("impl:U08 chosen microphone"));
                Assert.That(Setting("InputGain"),Is.EqualTo(.2f));Assert.That(Setting("Mode"),Is.EqualTo("OpenMic"));
                Assert.That(Setting("OutputVolume"),Is.EqualTo(.4f),"voice receives only its bus; master is already on the listener");
                Assert.That(session.Voice.Available,Is.False);Assert.That(session.Voice.Status,Is.EqualTo(status));
                foreach(string device in devices)Assert.That(Microphone.IsRecording(device),Is.False,"configuration never starts microphone capture");
                data.VoiceVolume=0;data.MicrophoneDevice="";data.VoiceMode="PushToTalk";trip.Apply(null,"me",null,null,false,0,0);
                value=configured.GetValue(session.Voice);
                Assert.That(Setting("OutputVolume"),Is.EqualTo(0));Assert.That(Setting("MicrophoneDevice"),Is.EqualTo(""));Assert.That(Setting("Mode"),Is.EqualTo("PushToTalk"));
            }
            finally{UnityEngine.Object.DestroyImmediate(host);AudioListener.volume=previousVolume;}
        }

        [Test]public void CreaturePointersUseEffectsAndFollowChangesWhileTheyAreAlive()
        {
            var host=new GameObject("impl:U08 creature audio");var eye=new GameObject("impl:U08 creature view").AddComponent<Camera>();
            eye.transform.position=new Vector3(0,1.65f,0);eye.fieldOfView=75;
            try
            {
                var session=host.AddComponent<FestivalSession>();var profile=new LocalProfile("audio");
                typeof(FestivalSession).GetProperty("Profile").SetValue(session,profile);
                profile.Data.MasterVolume=.4f;profile.Data.MusicVolume=1;profile.Data.EffectsVolume=.2f;
                var creatures=host.AddComponent<FestivalCreatures>();var state=new RoundState{Phase="Playing",Seed=7};var me=new PlayerState{Id="me"};state.Players.Add(me);
                me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                state.Npcs.Add(new NpcState{Id="clue",X=6,Z=30});state.Visions.Add(new VisionState{Id="hint",Kind="Clue",NpcId="clue",Hinted=true});
                AudioSource pointer=null;
                for(int step=0;step<2400&&pointer==null;step++)
                {
                    state.ElapsedSeconds=step*.05;creatures.Apply(state,"me",eye,step*.05f,.05f);
                    pointer=host.GetComponentInChildren<AudioSource>();
                }
                Assert.That(pointer,Is.Not.Null,"a real hinted creature arrived and squeaked");
                Assert.That(pointer.spatialBlend,Is.EqualTo(1),"the pointer still squeaks from its world position");
                Assert.That(pointer.volume,Is.EqualTo(.2f).Within(1e-5f),"a creature applies effects, without another master");
                profile.Data.MusicVolume=0;profile.Data.EffectsVolume=.7f;
                creatures.Apply(state,"me",eye,120,.05f);
                Assert.That(pointer.volume,Is.EqualTo(.7f).Within(1e-5f),"music mute does not mute the pointer");
                profile.Data.EffectsVolume=0;creatures.Apply(state,"me",eye,120,.05f);
                Assert.That(pointer.volume,Is.Zero,"effects mute applies to an existing pointer");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(eye.gameObject);UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();}
        }

        [TestCase(0f)][TestCase(.2f)][TestCase(1f)]public void TheShopChimeUsesEffectsEvenWhenMusicIsMuted(float effects)
        {
            var host=new GameObject("impl:U08 shop audio");
            try
            {
                var session=host.AddComponent<FestivalSession>();var profile=new LocalProfile("audio");
                typeof(FestivalSession).GetProperty("Profile").SetValue(session,profile);
                profile.Data.MasterVolume=.4f;profile.Data.MusicVolume=0;profile.Data.EffectsVolume=effects;
                var hud=host.AddComponent<FestivalHud>();
                typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
                var feedback=typeof(FestivalHud).GetMethod("UpdatePurchaseFeedback",BindingFlags.NonPublic|BindingFlags.Instance);
                var state=new RoundState{RoundId="audio"};var me=new PlayerState{Cash=20};
                feedback.Invoke(hud,new object[]{state,me});
                me.Cash=15;me.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});feedback.Invoke(hud,new object[]{state,me});
                var chime=(AudioSource)typeof(FestivalHud).GetField("shopAudio",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);
                Assert.That(chime.volume,Is.EqualTo(effects).Within(1e-5f),"the shop's real source applies effects; master is the listener's job");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();}
        }

        [Test]public void MasterAndFocusMultiplyTheTripOnceAndSurviveItsTeardown()
        {
            float previousVolume=AudioListener.volume;
            var host=new GameObject("impl:U08 listener settings");
            var view=new GameObject("impl:U08 listener");view.AddComponent<AudioListener>();
            try
            {
                var session=host.AddComponent<FestivalSession>();var profile=new LocalProfile("audio");
                typeof(FestivalSession).GetProperty("Profile").SetValue(session,profile);
                profile.Data.MasterVolume=.4f;
                var trip=host.AddComponent<FestivalTrip>();
                typeof(FestivalTrip).GetMethod("OnApplicationFocus",BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(trip,new object[]{true});
                trip.Apply(null,"me",view.transform,null,false,0,0);
                Assert.That(AudioListener.volume,Is.EqualTo(.4f).Within(1e-5f),"the saved master reaches the listener at the title");
                var state=new RoundState{Phase="Shopping",SimulationSeconds=110};var me=new PlayerState{Id="me"};state.Players.Add(me);
                me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=100,RemainingSeconds=FestivalSimulation.GiggleGasSeconds});
                trip.Apply(state,"me",view.transform,null,false,0,0,0);
                Assert.That(AudioListener.volume,Is.EqualTo(.24f).Within(1e-5f),"master .4 times the existing .6 kick gain");
                Assert.That(view.GetComponent<AudioReverbFilter>().enabled,Is.True,"gas still reverbs");
                typeof(FestivalTrip).GetMethod("OnApplicationFocus",BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(trip,new object[]{false});
                Assert.That(AudioListener.volume,Is.Zero,"losing focus mutes immediately");
                trip.enabled=false;
                typeof(FestivalTrip).GetMethod("OnDisable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(trip,null); // EditMode does not run lifecycle callbacks.
                Assert.That(AudioListener.volume,Is.Zero,"disabling the trip retains focus mute");
                Assert.That(view.GetComponent<AudioReverbFilter>().enabled,Is.False,"disable clears trip processing");
                typeof(FestivalTrip).GetMethod("OnApplicationFocus",BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(trip,new object[]{true});
                Assert.That(AudioListener.volume,Is.EqualTo(.4f).Within(1e-5f),"focus returns to configured master without the trip");
                profile.Data.MasterVolume=.2f;profile.Data.MuteWhenUnfocused=false;
                typeof(FestivalTrip).GetMethod("OnApplicationFocus",BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(trip,new object[]{false});
                Assert.That(AudioListener.volume,Is.EqualTo(.2f).Within(1e-5f),"focus-mute opt-out and master changes apply immediately");
                typeof(FestivalTrip).GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(trip,null);
                UnityEngine.Object.DestroyImmediate(trip);
                Assert.That(AudioListener.volume,Is.EqualTo(.2f).Within(1e-5f),"destroying the trip keeps configured volume");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(view);AudioListener.volume=previousVolume;}
        }
        private static void Field(ProfileData data,string name,object expected)
        {
            var field=typeof(ProfileData).GetField(name);
            Assert.That(field,Is.Not.Null,"schema field: "+name);
            Assert.That(field.GetValue(data),Is.EqualTo(expected),name);
        }

        [Test]public void ANewProfileUsesSchemaTwoAndShowsTheFullEffectsByDefault()
        {
            var profile=new LocalProfile("settings");
            Assert.That(profile.Data.SchemaVersion,Is.EqualTo(2),"new profiles use the single settings/collection schema");
            Assert.That(profile.Data.ReducedMotion,Is.False,"first-launch choice is handled by the comfort consumer");
            Assert.That(profile.Data.HighContrast,Is.False);
            Assert.That(profile.Save(),Is.Empty);
            Assert.That(new LocalProfile("settings").Data.SchemaVersion,Is.EqualTo(2));
        }

        [Test]public void SchemaOneRetainsThePlayersChoicesAndInitializesEveryNewSetting()
        {
            Write("{\"SchemaVersion\":1,\"PlayerName\":\"Saved Friend\",\"LastHost\":\"10.0.0.8\",\"ReducedMotion\":true,\"HighContrast\":false,\"ThirdPerson\":true,\"TimingCalibrationMs\":42,\"MouseSensitivity\":0.2,\"MusicVolume\":0.5,\"UnlockedFestivalCount\":2,\"Cosmetics\":[\"first_shuttle\"],\"SeenDialogue\":[\"sale_0123456789abcdef\"],\"SeenTips\":[\"swarm\"]}");
            var profile=new LocalProfile("settings");var data=profile.Data;
            Assert.That(data.SchemaVersion,Is.EqualTo(2));
            Assert.That(data.PlayerName,Is.EqualTo("Saved Friend"));Assert.That(data.LastHost,Is.EqualTo("10.0.0.8"));
            Assert.That(data.ReducedMotion,Is.True);Assert.That(data.HighContrast,Is.False);Assert.That(data.ThirdPerson,Is.True);
            Assert.That(data.TimingCalibrationMs,Is.EqualTo(42));Assert.That(data.MouseSensitivity,Is.EqualTo(.2f));Assert.That(data.MusicVolume,Is.EqualTo(.5f));
            Assert.That(data.UnlockedFestivalCount,Is.EqualTo(2));
            Assert.That(data.Cosmetics,Is.EqualTo(new[]{"first_shuttle"}));Assert.That(data.SeenDialogue,Is.EqualTo(new[]{"sale_0123456789abcdef"}));Assert.That(data.SeenTips,Is.EqualTo(new[]{"swarm"}));
            Field(data,"ComfortPromptComplete",true);
            Field(data,"MasterVolume",1f);Field(data,"EffectsVolume",.6f);Field(data,"VoiceVolume",1f);Field(data,"MuteWhenUnfocused",true);
            Field(data,"DisplayMode","Current");Field(data,"ResolutionWidth",0);Field(data,"ResolutionHeight",0);Field(data,"VSyncCount",-1);Field(data,"FrameCap",60);
            Field(data,"QualityPreset","High");Field(data,"RenderScale",1f);Field(data,"FieldOfView",75f);Field(data,"Brightness",0f);
            Field(data,"InvertY",false);Field(data,"BindingOverridesJson","");Field(data,"CameraShake",true);Field(data,"TextScale",1f);
            Field(data,"MicrophoneDevice","");Field(data,"InputGain",1f);Field(data,"VoiceMode","PushToTalk");
            Assert.That(profile.Save(),Is.Empty);
            Field(new LocalProfile("settings").Data,"ComfortPromptComplete",true);
        }

        [Test]public void EverySettingSurvivesASchemaTwoSaveAndReload()
        {
            var profile=new LocalProfile("settings");var d=profile.Data;
            d.PlayerName="Another Friend";d.LastHost="10.0.0.9";d.Cosmetics.Add("first_shuttle");d.SeenDialogue.Add("buy_0123456789abcdef");d.SeenTips.Add("vision");d.UnlockedFestivalCount=2;
            d.MasterVolume=.8f;d.MusicVolume=.93f;d.EffectsVolume=.7f;d.VoiceVolume=.4f;d.MuteWhenUnfocused=false;
            d.DisplayMode="Windowed";d.ResolutionWidth=1920;d.ResolutionHeight=1080;d.VSyncCount=0;d.FrameCap=144;d.QualityPreset="Medium";d.RenderScale=.9f;d.FieldOfView=90;d.Brightness=.3f;
            d.MouseSensitivity=.25f;d.InvertY=true;d.ThirdPerson=true;d.TimingCalibrationMs=-120;d.BindingOverridesJson="{\"bindings\":[]}";
            d.ReducedMotion=true;d.HighContrast=true;d.CameraShake=false;d.TextScale=1.3f;d.ComfortPromptComplete=true;
            d.MicrophoneDevice="Saved microphone";d.InputGain=.8f;d.VoiceMode="OpenMic";
            string chosen=JsonUtility.ToJson(d);
            Assert.That(profile.Save(),Is.Empty);
            Assert.That(JsonUtility.ToJson(new LocalProfile("settings").Data),Is.EqualTo(chosen),"the chosen values, including music above the old 70% cap");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AProfileFromANewerBuildCannotBeOverwritten(bool upgradedAfterLoading)
        {
            const string future="{\"SchemaVersion\":99,\"PlayerName\":\"Keep me\",\"Cosmetics\":null,\"future_setting\":\"unknown data\"}";
            LocalProfile profile;
            if(upgradedAfterLoading){profile=new LocalProfile("settings");Write(future);}
            else{Write(future);profile=new LocalProfile("settings");}
            profile.Data.PlayerName="Older build";
            Assert.That(profile.Save(),Is.Not.Empty,"tell the caller why this older build cannot save");
            Assert.That(File.ReadAllText(Path.Combine(ownedFolder,"settings.json")),Is.EqualTo(future),"preserve every byte of the newer file");
            Assert.That(File.Exists(Path.Combine(ownedFolder,"settings.json.tmp")),Is.False,"no temporary replacement was created");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LoadingAndSavingRepairNonfiniteNumbersRangesAndUnknownModes(bool validateOnSave)
        {
            var malformed=new ProfileData{MasterVolume=float.NaN,MusicVolume=float.PositiveInfinity,EffectsVolume=-.5f,VoiceVolume=3,
                MouseSensitivity=float.NaN,TimingCalibrationMs=350,RenderScale=2,FieldOfView=110,Brightness=-2,TextScale=float.NegativeInfinity,InputGain=-5,
                DisplayMode="Hologram",QualityPreset="Ultra",VSyncCount=9,FrameCap=47,ResolutionWidth=1920,ResolutionHeight=-1,VoiceMode="Broadcast",
                BindingOverridesJson="not json",MicrophoneDevice=null,PlayerName=" ",LastHost=new string('9',129),UnlockedFestivalCount=99};
            LocalProfile profile;
            if(validateOnSave)
            {
                profile=new LocalProfile("settings");JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(malformed),profile.Data);
                Assert.That(profile.Save(),Is.Empty);
            }
            else{Write(JsonUtility.ToJson(malformed));profile=new LocalProfile("settings");}
            var d=profile.Data;
            Assert.That(d.MasterVolume,Is.EqualTo(1));Assert.That(d.MusicVolume,Is.EqualTo(.35f));Assert.That(d.EffectsVolume,Is.Zero);Assert.That(d.VoiceVolume,Is.EqualTo(1));
            Assert.That(d.MouseSensitivity,Is.EqualTo(.12f));Assert.That(d.TimingCalibrationMs,Is.EqualTo(200));
            Assert.That(d.RenderScale,Is.EqualTo(1.5f));Assert.That(d.FieldOfView,Is.EqualTo(100));Assert.That(d.Brightness,Is.EqualTo(-1));Assert.That(d.TextScale,Is.EqualTo(1));Assert.That(d.InputGain,Is.Zero);
            Assert.That(d.DisplayMode,Is.EqualTo("Current"));Assert.That(d.QualityPreset,Is.EqualTo("High"));Assert.That(d.VSyncCount,Is.EqualTo(-1));Assert.That(d.FrameCap,Is.EqualTo(60));
            Assert.That(d.ResolutionWidth,Is.Zero);Assert.That(d.ResolutionHeight,Is.Zero);Assert.That(d.VoiceMode,Is.EqualTo("PushToTalk"));
            Assert.That(d.BindingOverridesJson,Is.Empty);Assert.That(d.MicrophoneDevice,Is.Empty);
            Assert.That(d.PlayerName,Is.EqualTo("Friend"));Assert.That(d.LastHost,Is.EqualTo("127.0.0.1"));Assert.That(d.UnlockedFestivalCount,Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidCollectionsAreRepairedWithoutDiscardingIdentityOrRecentDialogue(bool validateOnSave)
        {
            var history=new List<string>();for(int i=0;i<514;i++)history.Add("sale_"+i.ToString("x16"));
            string json="{\"SchemaVersion\":2,\"PlayerName\":\"Still Friend\",\"Cosmetics\":[\"first_shuttle\",\"first_shuttle\",\"unknown\",null],\"SeenTips\":null,\"SeenDialogue\":["+string.Join(",",history.ConvertAll(id=>"\""+id+"\""))+"],\"FoundCampEggIds\":[\"gnome\",\"gnome\",\"wash_me\",\"unknown\",null]}";
            LocalProfile profile;
            if(validateOnSave){profile=new LocalProfile("settings");JsonUtility.FromJsonOverwrite(json,profile.Data);Assert.That(profile.Save(),Is.Empty);}
            else{Write(json);profile=new LocalProfile("settings");}
            Field(profile.Data,"FoundCampEggIds",new[]{"gnome","wash_me"});
            Assert.That(profile.Data.PlayerName,Is.EqualTo("Still Friend"),"one invalid collection never wipes the rest of the profile");
            Assert.That(profile.Data.Cosmetics,Is.EqualTo(new[]{"first_shuttle"}));Assert.That(profile.Data.SeenTips,Is.Not.Null.And.Empty);
            Assert.That(profile.Data.SeenDialogue.Count,Is.EqualTo(512));Assert.That(profile.Data.SeenDialogue[0],Is.EqualTo("sale_0000000000000002"));
            Assert.That(profile.Data.SeenDialogue[511],Is.EqualTo("sale_0000000000000201"));
            Assert.That(profile.Save(),Is.Empty);Field(new LocalProfile("settings").Data,"FoundCampEggIds",new[]{"gnome","wash_me"});
            Write("{\"SchemaVersion\":1,\"PlayerName\":\"Old Friend\"}");
            Field(new LocalProfile("settings").Data,"FoundCampEggIds",new string[0]);
        }

        [TestCase("Audio")]
        [TestCase("Graphics")]
        [TestCase("Controls")]
        [TestCase("Accessibility")]
        [TestCase("Voice")]
        public void ResetTabRestoresOnlyItsSettingsAndSavesThem(string tab)
        {
            var profile=new LocalProfile("settings");var d=profile.Data;
            d.PlayerName="Keep identity";d.LastHost="10.0.0.9";d.Cosmetics.Add("first_shuttle");d.SeenDialogue.Add("buy_0123456789abcdef");d.SeenTips.Add("vision");d.FoundCampEggIds.Add("gnome");d.UnlockedFestivalCount=2;
            d.MasterVolume=.8f;d.MusicVolume=.9f;d.EffectsVolume=.7f;d.VoiceVolume=.4f;d.MuteWhenUnfocused=false;
            d.DisplayMode="Windowed";d.ResolutionWidth=1920;d.ResolutionHeight=1080;d.VSyncCount=0;d.FrameCap=144;d.QualityPreset="Medium";d.RenderScale=.9f;d.FieldOfView=90;d.Brightness=.3f;
            d.MouseSensitivity=.25f;d.InvertY=true;d.ThirdPerson=true;d.TimingCalibrationMs=-120;d.BindingOverridesJson="{\"bindings\":[]}";
            d.ReducedMotion=true;d.HighContrast=true;d.CameraShake=false;d.TextScale=1.3f;d.ComfortPromptComplete=true;
            d.MicrophoneDevice="Saved microphone";d.InputGain=.8f;d.VoiceMode="OpenMic";
            string before=JsonUtility.ToJson(d);
            var expected=JsonUtility.FromJson<ProfileData>(before);
            switch(tab)
            {
                case "Audio":expected.MasterVolume=1;expected.MusicVolume=.35f;expected.EffectsVolume=.6f;expected.VoiceVolume=1;expected.MuteWhenUnfocused=true;break;
                case "Graphics":expected.DisplayMode="Current";expected.ResolutionWidth=0;expected.ResolutionHeight=0;expected.VSyncCount=-1;expected.FrameCap=60;expected.QualityPreset="High";expected.RenderScale=1;expected.FieldOfView=75;expected.Brightness=0;break;
                case "Controls":expected.MouseSensitivity=.12f;expected.InvertY=false;expected.ThirdPerson=false;expected.TimingCalibrationMs=0;expected.BindingOverridesJson="{\"bindings\":[]}";break;
                case "Accessibility":expected.ReducedMotion=false;expected.HighContrast=false;expected.CameraShake=true;expected.TextScale=1;break;
                case "Voice":expected.MicrophoneDevice="";expected.InputGain=1;expected.VoiceMode="PushToTalk";break;
            }
            var controller=new FestivalSettings(profile);
            Assert.That(controller.ResetTab(tab),Is.Empty);
            Assert.That(profile.Data,Is.SameAs(d),"consumers keep the same profile object");
            Assert.That(JsonUtility.ToJson(d),Is.EqualTo(JsonUtility.ToJson(expected)),"only this tab changes; progression, tips and the informed flag survive");
            Assert.That(JsonUtility.ToJson(new LocalProfile("settings").Data),Is.EqualTo(JsonUtility.ToJson(expected)),"reset is persisted");
        }

        [TestCase("missing")]
        [TestCase("zero")]
        [TestCase("malformed")]
        [TestCase("oversized")]
        [TestCase("oversized-future")]
        public void UnreadableProfilesReportAnErrorAndRemainUntouched(string kind)
        {
            string source=kind=="missing"?"{\"PlayerName\":\"Keep me\"}":kind=="zero"?"{\"SchemaVersion\":0}":kind=="malformed"?"{":
                "{\"SchemaVersion\":"+(kind=="oversized"?"2":"99")+",\"extra\":\""+new string('x',65536)+"\"}";
            Write(source);
            var profile=new LocalProfile("settings");var controller=new FestivalSettings(profile);
            Assert.That(controller.LastError,Is.Not.Empty,"surface the load error to the settings consumer");
            Assert.That(profile.ReadOnly,Is.True,"defaults are available, but cannot replace unreadable saved data");
            Assert.That(controller.ResetTab("Audio"),Is.Not.Empty);
            Assert.That(File.ReadAllText(Path.Combine(ownedFolder,"settings.json")),Is.EqualTo(source));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AFailedSaveReportsTheErrorPreservesThePreviousProfileAndCleansItsTempFile(bool blockReplacement)
        {
            var profile=new LocalProfile("settings");var controller=new FestivalSettings(profile);
            profile.Data.PlayerName="Before failure";Assert.That(controller.Save(),Is.Empty);
            string file=Path.Combine(ownedFolder,"settings.json"),previous=file+".previous";
            if(blockReplacement){File.Move(file,previous);Directory.CreateDirectory(file);profile.Data.PlayerName="Failed replacement";}
            else profile.Data.PlayerName=new string('x',70000);
            Assert.That(controller.Save(),Is.Not.Empty,"surface an IO failure or an oversized write instead of reporting success");
            Assert.That(controller.LastError,Is.EqualTo(profile.LastError));
            Assert.That(Directory.GetFiles(ownedFolder,"*.tmp"),Is.Empty,"a failed rename leaves no half-written replacement behind");
            if(blockReplacement){Directory.Delete(file);File.Move(previous,file);}
            Assert.That(new LocalProfile("settings").Data.PlayerName,Is.EqualTo("Before failure"),"the previous complete profile remains readable");
            profile.Data.PlayerName="Recovered";Assert.That(controller.Save(),Is.Empty);
            Assert.That(controller.LastError,Is.Empty);Assert.That(new LocalProfile("settings").Data.PlayerName,Is.EqualTo("Recovered"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BindingIdsAreValidatedAndRealInputSystemOverridesRoundTrip(bool invalidId)
        {
            using(var map=new InputActionMap("Festival"))
            {
                var action=map.AddAction("Interact",InputActionType.Button,"<Keyboard>/e");action.ApplyBindingOverride("<Keyboard>/x");
                string json=map.SaveBindingOverridesAsJson();
                if(invalidId)json=json.Replace(action.bindings[0].id.ToString(),"not-a-binding-id");
                var profile=new LocalProfile("settings");profile.Data.BindingOverridesJson=json;
                Assert.That(profile.Save(),Is.Empty);
                Assert.That(new LocalProfile("settings").Data.BindingOverridesJson,Is.EqualTo(invalidId?"":json),"validate saved binding identity without losing real Input System JSON");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void MissingFieldsKeepTheDefaultsOfTheProfilesOwnSchema(int version)
        {
            string source="{\"SchemaVersion\":"+version+",\"MouseSensitivity\":0.2}";Write(source);
            var profile=new LocalProfile("settings");
            Assert.That(profile.Data.ReducedMotion,Is.EqualTo(version==1),"omitted legacy comfort was on; omitted new comfort is off");
            Assert.That(profile.Data.HighContrast,Is.EqualTo(version==1));Assert.That(profile.Data.ComfortPromptComplete,Is.EqualTo(version==1));
            Assert.That(profile.Data.MasterVolume,Is.EqualTo(1));Assert.That(profile.Data.EffectsVolume,Is.EqualTo(.6f));Assert.That(profile.Data.FoundCampEggIds,Is.Not.Null.And.Empty);
            Assert.That(File.ReadAllText(Path.Combine(ownedFolder,"settings.json")),Is.EqualTo(source),"loading upgrades in memory only");
            Assert.That(profile.Save(),Is.Empty);Assert.That(new LocalProfile("settings").Data.ReducedMotion,Is.EqualTo(version==1));
        }

        [Test]public void EveryKnownEggSurvivesAndUnknownIdsCannotInflateTheCollection()
        {
            string[] ids={"gnome","button","chair","lost_found","hammock","flamingo","cone","wash_me"};
            var profile=new LocalProfile("settings");profile.Data.FoundCampEggIds.AddRange(ids);
            profile.Data.FoundCampEggIds.AddRange(new[]{"gnome","unknown",null});Assert.That(profile.Save(),Is.Empty);
            var found=new LocalProfile("settings").Data.FoundCampEggIds;
            Assert.That(found.Count,Is.EqualTo(8));Assert.That(found,Is.EquivalentTo(ids));
        }

        [Test]public void AnUnknownTabCannotChangeOrSaveTheProfile()
        {
            var profile=new LocalProfile("settings");profile.Data.MusicVolume=.9f;string before=JsonUtility.ToJson(profile.Data);
            var controller=new FestivalSettings(profile);Assert.That(controller.ResetTab("NotATab"),Is.Not.Empty);
            Assert.That(JsonUtility.ToJson(profile.Data),Is.EqualTo(before));Assert.That(File.Exists(Path.Combine(ownedFolder,"settings.json")),Is.False);
        }

        [Test]public void AnUnsupportedInMemorySchemaCannotReplaceTheSavedProfile()
        {
            var profile=new LocalProfile("settings");profile.Data.PlayerName="Keep me";Assert.That(profile.Save(),Is.Empty);
            string before=File.ReadAllText(Path.Combine(ownedFolder,"settings.json"));profile.Data.SchemaVersion=99;
            Assert.That(profile.Save(),Is.Not.Empty);Assert.That(File.ReadAllText(Path.Combine(ownedFolder,"settings.json")),Is.EqualTo(before));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void NullCollectionsAreRepairedWithoutLosingThePlayersIdentity(int version)
        {
            Write("{\"SchemaVersion\":"+version+",\"PlayerName\":\"Still me\",\"Cosmetics\":null,\"SeenDialogue\":null,\"SeenTips\":null,\"FoundCampEggIds\":null}");
            var profile=new LocalProfile("settings");Assert.That(profile.Data.PlayerName,Is.EqualTo("Still me"));
            // JsonUtility can treat JSON null as an omitted collection; also exercise callers assigning null before saving.
            profile.Data.Cosmetics=null;profile.Data.SeenDialogue=null;profile.Data.SeenTips=null;profile.Data.FoundCampEggIds=null;
            Assert.That(profile.Save(),Is.Empty);
            Assert.That(profile.Data.Cosmetics,Is.Not.Null.And.Empty);Assert.That(profile.Data.SeenDialogue,Is.Not.Null.And.Empty);
            Assert.That(profile.Data.SeenTips,Is.Not.Null.And.Empty);Assert.That(profile.Data.FoundCampEggIds,Is.Not.Null.And.Empty);
        }

        [Test]public void InvalidHistoryIdsAreRemovedAndRecognizedLegacyKeysSurvive()
        {
            var profile=new LocalProfile("settings");
            profile.Data.SeenDialogue.AddRange(new[]{null," ","unsafe / id",new string('x',65),"sale_0123456789abcdef","sale_0123456789abcdef"});
            profile.Data.SeenTips.AddRange(new[]{null,"unsafe / id","vision","test3-saved-tip","vision"});
            Assert.That(profile.Save(),Is.Empty);var data=new LocalProfile("settings").Data;
            Assert.That(data.SeenDialogue,Is.EqualTo(new[]{"sale_0123456789abcdef"}));Assert.That(data.SeenTips,Is.EquivalentTo(new[]{"vision","test3-saved-tip"}));
        }
    }
    public sealed class GraphicsApplicationTests
    {
        [TestCase(RuntimePlatform.WindowsPlayer,GraphicsDeviceType.Direct3D11,true)]
        [TestCase(RuntimePlatform.WindowsPlayer,GraphicsDeviceType.Direct3D12,true)]
        [TestCase(RuntimePlatform.WindowsPlayer,GraphicsDeviceType.Vulkan,false)]
        [TestCase(RuntimePlatform.OSXPlayer,GraphicsDeviceType.Metal,false)]
        [TestCase(RuntimePlatform.LinuxPlayer,GraphicsDeviceType.Vulkan,false)]
        public void ExclusiveFullscreenRequiresASupportedPlatformAndGraphicsApi(RuntimePlatform platform,GraphicsDeviceType graphics,bool supported)
        {
            Assert.That(FestivalSettings.SupportsExclusiveDisplay(platform,graphics),Is.EqualTo(supported));
        }
        private string previousFolder,ownedFolder,previousPreferencesScope;
        private int qualityLevel;
        [SetUp]public void OwnGraphicsProfile()
        {
            qualityLevel=QualitySettings.GetQualityLevel();previousPreferencesScope=FestivalInput.PreferencesScope;previousFolder=LocalProfile.Folder;ownedFolder=Path.Combine(Path.GetTempPath(),"impl-U34-graphics-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ownedFolder);LocalProfile.Folder=ownedFolder;FestivalInput.PreferencesScope=ownedFolder;
        }
        [TearDown]public void RestoreGraphicsProfile()
        {
            int left=QualitySettings.GetQualityLevel();QualitySettings.SetQualityLevel(qualityLevel,false);
            FestivalInput.PreferencesScope=previousPreferencesScope;LocalProfile.Folder=previousFolder;if(Directory.Exists(ownedFolder))Directory.Delete(ownedFolder,true);
            Assert.That(QualitySettings.names[left],Is.EqualTo(QualitySettings.names[qualityLevel]),"the test leaves the editor quality level it found");
        }
        private static void Update(FestivalSession session)=>typeof(FestivalSession).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,null);
        private static FestivalSession Session(GameObject host)
        {
            var session=host.AddComponent<FestivalSession>();
            if(session.Profile==null)typeof(FestivalSession).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,null);
            return session;
        }
        internal static void Close(GameObject host,FestivalSession session)
        {
            // EditMode does not drive MonoBehaviour teardown after our explicit Awake.
            var settings=typeof(FestivalSession).GetProperty("Settings")?.GetValue(session) as System.IDisposable;
            settings?.Dispose();session.Controls?.Dispose();
            var camera=session.ViewCamera;var actors=typeof(FestivalSession).GetField("actorRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session) as Transform;
            UnityEngine.Object.DestroyImmediate(host);
            if(camera!=null)UnityEngine.Object.DestroyImmediate(camera.gameObject);if(actors!=null)UnityEngine.Object.DestroyImmediate(actors.gameObject);
        }

        [TestCase("Low",.75f)] [TestCase("Medium",.9f)]
        public void PresetScaleIsSavedWithThePresetAndCustomScaleSurvivesRestart(string preset,float scale)
        {
            var profile=new LocalProfile("impl-U34-preset");var settings=new FestivalSettings(profile);
            try
            {
                settings.ApplyGraphics();profile.Data.QualityPreset=preset;Assert.That(settings.Save(),Is.Empty);
                Assert.That(new LocalProfile("impl-U34-preset").Data.RenderScale,Is.EqualTo(scale).Within(1e-5f),"the reset scale must be persisted before the save completes");
                profile.Data.RenderScale=1.27f;Assert.That(settings.Save(),Is.Empty);settings.Dispose();
                var loaded=new LocalProfile("impl-U34-preset");
                using(var restarted=new FestivalSettings(loaded))
                {
                    restarted.ApplyGraphics();Assert.That(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale,Is.EqualTo(1.27f).Within(1e-5f),"startup retains the independently saved scale");
                }
            }
            finally{settings.Dispose();}
        }

        [Test]public void TeardownRestoresEveryTouchedQualityPipelineAndFramePacing()
        {
            int initialLevel=QualitySettings.GetQualityLevel(),initialVsync=QualitySettings.vSyncCount,initialCap=Application.targetFrameRate;
            var before=Enumerable.Range(0,QualitySettings.names.Length).Select(QualitySettings.GetRenderPipelineAssetAt).ToArray();
            var source=(QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            var owned=new Dictionary<int,UniversalRenderPipelineAsset>();var settings=new FestivalSettings(new LocalProfile("impl-U34-restore"));
            try
            {
                foreach(string name in new[]{"Low","Medium","High"})
                {
                    int level=Array.IndexOf(QualitySettings.names,name);var pipeline=UnityEngine.Object.Instantiate(source);pipeline.hideFlags=HideFlags.HideAndDontSave;
                    owned.Add(level,pipeline);QualitySettings.SetQualityLevel(level,false);QualitySettings.renderPipeline=pipeline;
                }
                QualitySettings.SetQualityLevel(initialLevel,false);QualitySettings.vSyncCount=initialVsync;
                settings.ApplyGraphics();settings.Data.VSyncCount=0;settings.Data.FrameCap=144;
                foreach(string name in new[]{"Low","Medium","High"}){settings.Data.QualityPreset=name;settings.ApplyGraphics();}
                settings.Dispose();
                foreach(var entry in owned)Assert.That(QualitySettings.GetRenderPipelineAssetAt(entry.Key),Is.SameAs(entry.Value),QualitySettings.names[entry.Key]+" restores its own pipeline, not the last selected one");
                Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(initialLevel));Assert.That(QualitySettings.vSyncCount,Is.EqualTo(initialVsync));Assert.That(Application.targetFrameRate,Is.EqualTo(initialCap));
            }
            finally
            {
                settings.Dispose();
                foreach(var entry in owned){QualitySettings.SetQualityLevel(entry.Key,false);QualitySettings.renderPipeline=before[entry.Key];UnityEngine.Object.DestroyImmediate(entry.Value);}
                QualitySettings.SetQualityLevel(initialLevel,false);QualitySettings.vSyncCount=initialVsync;Application.targetFrameRate=initialCap;
            }
        }

        // The newer session's teardown leaves the shared High slot on its destroyed pipeline, so the older one takes the slot back on
        // its next ApplyGraphics (FestivalSession.Update runs it every frame). The older teardown must leave the live graphics at once.
        [TestCase(false)][TestCase(true)]
        public void OverlappingSettingsKeepTheLiveGraphicsAndTheLastRestoresTheFirstCapture(bool newerFirst)
        {
            int initialLevel=QualitySettings.GetQualityLevel(),initialCap=Application.targetFrameRate,start=Array.IndexOf(QualitySettings.names,"Very High"),high=Array.IndexOf(QualitySettings.names,"High");
            var before=Enumerable.Range(0,QualitySettings.names.Length).Select(QualitySettings.GetRenderPipelineAssetAt).ToArray();
            var older=new FestivalSettings(new LocalProfile("overlapping-older"),new Display());var newer=new FestivalSettings(new LocalProfile("overlapping-newer"),new Display());
            try
            {
                QualitySettings.SetQualityLevel(start,false);int startVsync=QualitySettings.vSyncCount;Application.targetFrameRate=37;
                older.ApplyGraphics();var olderPipeline=QualitySettings.renderPipeline;newer.ApplyGraphics();var newerPipeline=QualitySettings.renderPipeline;
                Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(high),"setup: both apply the saved High preset");
                Assert.That(newerPipeline,Is.Not.SameAs(olderPipeline),"setup: each session has its own pipeline");
                var first=newerFirst?newer:older;var last=newerFirst?older:newer;var live=newerFirst?olderPipeline:newerPipeline;
                first.Dispose();if(newerFirst)last.ApplyGraphics();
                Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()],Is.EqualTo("High"),"the first teardown leaves the live session's level");
                Assert.That(QualitySettings.renderPipeline,Is.SameAs(live),"and its own pipeline");Assert.That(QualitySettings.renderPipeline!=null,Is.True,"a pipeline that still exists");
                last.Dispose();
                Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()],Is.EqualTo("Very High"),"the last teardown restores the level that the first one found");
                Assert.That(QualitySettings.vSyncCount,Is.EqualTo(startVsync),"and its vsync");Assert.That(Application.targetFrameRate,Is.EqualTo(37),"and its frame cap");
                for(int i=0;i<before.Length;i++)Assert.That(QualitySettings.GetRenderPipelineAssetAt(i),Is.SameAs(before[i]),QualitySettings.names[i]+" gets its own pipeline back");
            }
            finally
            {
                older.Dispose();newer.Dispose();
                for(int i=0;i<before.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=before[i];}
                QualitySettings.SetQualityLevel(initialLevel,false);Application.targetFrameRate=initialCap;
            }
        }

        [Test]public void SessionAppliesSavedBrightnessAtTheTitleBeforeConnecting()
        {
            var worldObject=new GameObject("impl:U34 title brightness");var world=worldObject.AddComponent<FestivalWorld>();world.Build();
            var host=new GameObject("impl:U34 title graphics session");var session=Session(host);
            try
            {
                var volume=worldObject.GetComponent<Volume>();Assert.That(volume.sharedProfile.TryGet(out ColorAdjustments grade),Is.True);
                session.Profile.Data.Brightness=.6f;Update(session);Assert.That(grade.postExposure.value,Is.EqualTo(.9f).Within(1e-5f));
                session.Profile.Data.Brightness=-.3f;Update(session);Assert.That(grade.postExposure.value,Is.Zero.Within(1e-5f));
            }
            finally{Close(host,session);UnityEngine.Object.DestroyImmediate(worldObject);}
        }

        [TestCase(70f)] [TestCase(90f)] [TestCase(100f)]
        public void HudUsesTheProfileBaseFovWithDoseAndGasOffsetsAndKeepsTitleFraming(float fov)
        {
            var host=new GameObject("impl:U34 profile FOV");var session=Session(host);var hud=host.AddComponent<FestivalHud>();
            if(typeof(FestivalHud).GetField("effectWash",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud)==null)
                typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            try
            {
                session.Profile.Data.FieldOfView=fov;session.Profile.Data.ReducedMotion=false;
                var state=new Festival.Core.RoundState{Phase="Playing",SimulationSeconds=110};var me=new Festival.Core.PlayerState{Id="me"};state.Players.Add(me);
                typeof(FestivalSession).GetProperty("State").SetValue(session,state);
                var apply=typeof(FestivalHud).GetMethod("ApplyEffects",BindingFlags.NonPublic|BindingFlags.Instance);
                apply.Invoke(hud,new object[]{me});Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(fov),"the sober view uses the saved profile FOV");
                me.Effects.Add(new Festival.Core.ActiveEffect{Id=Festival.Core.FestivalSimulation.DoseEffect,Substance="mushrooms",Intensity=4});
                me.Effects.Add(new Festival.Core.ActiveEffect{Id=Festival.Core.FestivalSimulation.GiggleGasEffect,StartSeconds=100,RemainingSeconds=Festival.Core.FestivalSimulation.GiggleGasSeconds});
                var camp=session.GetComponent<FestivalCampAudio>();
                float offset=FestivalTrip.For(state,me.Id,false,Time.unscaledTime,camp!=null?camp.Beat:null).Fov;
                apply.Invoke(hud,new object[]{me});Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(fov+offset).Within(.001f),"dose and gas compose over the chosen base");
                typeof(FestivalHud).GetMethod("FrameTitle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
                Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(FestivalTitleScreen.Fov),"the title keeps its authored shot");
            }
            finally{Close(host,session);}
        }

        [Test]public void CameraShakeOffKeepsTheProfileFovAndColour()
        {
            var host=new GameObject("impl-U36 camera comfort");var session=Session(host);var hud=host.AddComponent<FestivalHud>();
            if(typeof(FestivalHud).GetField("effectWash",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud)==null)
                typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            try
            {
                session.Profile.Data.FieldOfView=90;session.Profile.Data.ReducedMotion=false;session.Profile.Data.CameraShake=false;
                var state=new RoundState{Phase="Playing",SimulationSeconds=110};var me=new PlayerState{Id="me"};state.Players.Add(me);
                me.Effects.Add(new ActiveEffect{Id="mushrooms",RemainingSeconds=10});
                me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Substance="mushrooms",Intensity=4});
                typeof(FestivalSession).GetProperty("State").SetValue(session,state);
                typeof(FestivalHud).GetMethod("ApplyEffects",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,new object[]{me});
                Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(90).Within(.0001f),"camera shake off removes shop/dose swell while preserving the chosen base FOV");
                var wash=(UnityEngine.UI.Image)typeof(FestivalHud).GetField("effectWash",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);
                Assert.That(wash.color.a,Is.GreaterThan(0),"comfort removes camera motion while keeping the trip colour");
            }
            finally{Close(host,session);}
        }

        [Test]public void CameraShakeOffRemovesShopRollAndGasKickWithoutRemovingTheGasLook()
        {
            var host=new GameObject("impl-U36 roll and gas comfort");var session=Session(host);var hud=host.AddComponent<FestivalHud>();
            if(typeof(FestivalHud).GetField("effectWash",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud)==null)
                typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            try
            {
                session.Profile.Data.FieldOfView=86;session.Profile.Data.ReducedMotion=false;session.Profile.Data.CameraShake=false;
                var state=new RoundState{Phase="Shopping",SimulationSeconds=1};var me=new PlayerState{Id="me"};state.Players.Add(me);
                me.Effects.Add(new ActiveEffect{Id="lsd",RemainingSeconds=10});
                me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=0,RemainingSeconds=FestivalSimulation.GiggleGasSeconds});
                typeof(FestivalSession).GetProperty("State").SetValue(session,state);
                typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,me.Id);
                var camera=typeof(FestivalSession).GetMethod("UpdateCamera",BindingFlags.NonPublic|BindingFlags.Instance);
                camera.Invoke(session,new object[]{me});
                Assert.That(Mathf.DeltaAngle(0,session.ViewCamera.transform.eulerAngles.z),Is.EqualTo(0).Within(.0001f),"disabled camera shake removes the actual shop roll");
                typeof(FestivalHud).GetMethod("ApplyEffects",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,new object[]{me});
                Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(86).Within(.0001f),"the gas cannot add a camera kick");
                var trip=session.GetComponent<FestivalTrip>();
                trip.Apply(state,me.Id,session.ViewCamera.transform,null,false,1,0,0);
                var vignette=(UnityEngine.UI.RawImage)typeof(FestivalTrip).GetField("vignette",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(trip);
                Assert.That(vignette.color.a,Is.GreaterThan(0),"the actual gas overlay stays visible");
                session.Profile.Data.CameraShake=true;camera.Invoke(session,new object[]{me});
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0,session.ViewCamera.transform.eulerAngles.z)),Is.GreaterThan(.1f),"reenabling the setting restores the shipped roll");
            }
            finally{Close(host,session);}
        }

        private sealed class Display:IFestivalDisplay
        {
            public int Width {get;private set;}=800;public int Height {get;private set;}=600;
            public FullScreenMode Mode {get;private set;}=FullScreenMode.Windowed;
            public bool SupportsExclusiveFullscreen=>false;
            public Resolution[] Resolutions {get;set;}=new[]{new Resolution{width=1920,height=1080},new Resolution{width=1280,height=720},new Resolution{width=1920,height=1080}};
            public int Changes;
            public bool Deferred;
            private int pendingWidth,pendingHeight;
            private FullScreenMode pendingMode;
            public void SetResolution(int width,int height,FullScreenMode mode)
            {
                pendingWidth=width;pendingHeight=height;pendingMode=mode;Changes++;if(!Deferred)Acknowledge();
            }
            public void Acknowledge(){Width=pendingWidth;Height=pendingHeight;Mode=pendingMode;}
        }
        [TestCase(false)] [TestCase(true)]
        public void EffectiveDisplayFollowsThePlatformAndTeardownCancelsPendingChanges(bool acknowledge)
        {
            var profile=new LocalProfile("impl-U34-deferred-display");var display=new Display{Deferred=true};var settings=new FestivalSettings(profile,display);
            try
            {
                profile.Data.DisplayMode="Fullscreen";profile.Data.ResolutionWidth=1280;profile.Data.ResolutionHeight=720;settings.ApplyGraphics();
                Assert.That(settings.EffectiveDisplayMode,Is.EqualTo(FullScreenMode.Windowed),"a request is not yet the effective platform mode");
                Assert.That(settings.EffectiveResolution.width,Is.EqualTo(800));
                if(acknowledge)
                {
                    display.Acknowledge();Assert.That(settings.EffectiveDisplayMode,Is.EqualTo(FullScreenMode.FullScreenWindow));
                    Assert.That(settings.EffectiveResolution.width,Is.EqualTo(1280));Assert.That(settings.EffectiveResolution.height,Is.EqualTo(720));
                }
                settings.ApplyGraphics();Assert.That(display.Changes,Is.EqualTo(1),"pending requests are not submitted every frame");
                settings.Dispose();display.Acknowledge();
                Assert.That(display.Mode,Is.EqualTo(FullScreenMode.Windowed));Assert.That(display.Width,Is.EqualTo(800));Assert.That(display.Height,Is.EqualTo(600));
            }
            finally{settings.Dispose();}
        }
        [Test]public void GraphicsResetCancelsAnUnacknowledgedDisplayRequest()
        {
            var profile=new LocalProfile("impl-U34-pending-reset");var display=new Display{Deferred=true};var settings=new FestivalSettings(profile,display);
            try
            {
                settings.ApplyGraphics();profile.Data.DisplayMode="Fullscreen";profile.Data.ResolutionWidth=1280;profile.Data.ResolutionHeight=720;
                settings.ApplyGraphics();Assert.That(settings.ResetTab("Graphics"),Is.Empty);display.Acknowledge();
                Assert.That(display.Mode,Is.EqualTo(FullScreenMode.Windowed),"reset supersedes the queued fullscreen request even while the platform still reports its original mode");
                Assert.That(display.Width,Is.EqualTo(800));Assert.That(display.Height,Is.EqualTo(600));
            }
            finally{settings.Dispose();}
        }
        [Test]public void DisplayFallbackDeduplicatesModesAndGraphicsResetRestoresOnlyGraphics()
        {
            var profile=new LocalProfile("impl-U34-display");var display=new Display();var settings=new FestivalSettings(profile,display);
            int quality=QualitySettings.GetQualityLevel(),vsync=QualitySettings.vSyncCount,cap=Application.targetFrameRate;
            try
            {
                settings.ApplyGraphics();Assert.That(display.Changes,Is.Zero,"new defaults retain the current platform display");
                Assert.That(settings.AvailableResolutions.Select(r=>r.width),Is.EquivalentTo(new[]{1920,1280,800}),"deduplicate reported modes and include the current window");
                profile.Data.DisplayMode="Fullscreen";profile.Data.ResolutionWidth=1280;profile.Data.ResolutionHeight=720;profile.Data.MasterVolume=.37f;
                settings.ApplyGraphics();Assert.That(display.Mode,Is.EqualTo(FullScreenMode.FullScreenWindow),"unsupported exclusive fullscreen falls back to borderless");
                Assert.That(settings.EffectiveDisplayMode,Is.EqualTo(FullScreenMode.FullScreenWindow));Assert.That(display.Width,Is.EqualTo(1280));Assert.That(display.Height,Is.EqualTo(720));
                settings.ApplyGraphics();Assert.That(display.Changes,Is.EqualTo(1),"unchanged graphics do not reapply the display every frame");
                Assert.That(settings.ResetTab("Graphics"),Is.Empty);
                Assert.That(display.Mode,Is.EqualTo(FullScreenMode.Windowed));Assert.That(display.Width,Is.EqualTo(800));Assert.That(display.Height,Is.EqualTo(600));
                Assert.That(profile.Data.MasterVolume,Is.EqualTo(.37f));Assert.That(profile.Data.FrameCap,Is.EqualTo(60));Assert.That(profile.Data.FieldOfView,Is.EqualTo(75));
                profile.Data.DisplayMode="Borderless";settings.ApplyGraphics();settings.Dispose();
                Assert.That(display.Mode,Is.EqualTo(FullScreenMode.Windowed));Assert.That(display.Width,Is.EqualTo(800));Assert.That(display.Height,Is.EqualTo(600));
                Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(quality));Assert.That(QualitySettings.vSyncCount,Is.EqualTo(vsync));Assert.That(Application.targetFrameRate,Is.EqualTo(cap));
            }
            finally{settings.Dispose();}
        }
        [Test]public void MissingReportedResolutionsKeepTheCurrentModeAndRejectUnsupportedSavedSizes()
        {
            var profile=new LocalProfile("impl-U34-no-modes");var display=new Display{Resolutions=new Resolution[0]};var settings=new FestivalSettings(profile,display);
            try
            {
                settings.ApplyGraphics();Assert.That(settings.AvailableResolutions.Count,Is.EqualTo(1));Assert.That(settings.AvailableResolutions[0].width,Is.EqualTo(800));
                profile.Data.ResolutionWidth=9999;profile.Data.ResolutionHeight=9999;settings.ApplyGraphics();
                Assert.That(display.Width,Is.EqualTo(800));Assert.That(display.Height,Is.EqualTo(600));Assert.That(display.Changes,Is.Zero);
            }
            finally{settings.Dispose();}
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)] [TestCase(144)] [TestCase(-1)]
        public void FrameCapAppliesWithoutVsyncAndVsyncTakesPriority(int cap)
        {
            int originalLevel=QualitySettings.GetQualityLevel(),originalVsync=QualitySettings.vSyncCount,originalCap=Application.targetFrameRate;
            var originalPipeline=QualitySettings.renderPipeline;
            var host=new GameObject("impl:U34 frame pacing");var session=Session(host);
            try
            {
                session.Profile.Data.VSyncCount=0;session.Profile.Data.FrameCap=cap;Update(session);
                Assert.That(QualitySettings.vSyncCount,Is.Zero);Assert.That(Application.targetFrameRate,Is.EqualTo(cap));
                session.Profile.Data.VSyncCount=1;Update(session);
                Assert.That(QualitySettings.vSyncCount,Is.EqualTo(1));Assert.That(Application.targetFrameRate,Is.EqualTo(-1),"vsync controls frame pacing while the saved cap is retained");
                Assert.That(session.Profile.Data.FrameCap,Is.EqualTo(cap));
                session.Profile.Data.VSyncCount=0;Update(session);Assert.That(Application.targetFrameRate,Is.EqualTo(cap));
                session.Profile.Data.RenderScale=1.23f;Update(session);
                Assert.That(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale,Is.EqualTo(1.23f).Within(1e-5f),"the independent render-scale choice survives later frames");
                Close(host,session);host=null;
                Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(originalLevel));Assert.That(QualitySettings.renderPipeline,Is.SameAs(originalPipeline));
                Assert.That(QualitySettings.vSyncCount,Is.EqualTo(originalVsync));Assert.That(Application.targetFrameRate,Is.EqualTo(originalCap));
            }
            finally
            {
                if(host!=null)Close(host,session);
                QualitySettings.SetQualityLevel(originalLevel,false);QualitySettings.renderPipeline=originalPipeline;QualitySettings.vSyncCount=originalVsync;Application.targetFrameRate=originalCap;
            }
        }

        [TestCase("Low",.75f,0,1,1)]
        [TestCase("Medium",.9f,30,1,2)]
        [TestCase("High",1f,45,2,4)]
        public void SessionAppliesAuditedQualityWithoutChangingTheSourceAsset(string name,float scale,float shadows,int cascades,int samples)
        {
            int originalLevel=QualitySettings.GetQualityLevel(),originalVsync=QualitySettings.vSyncCount,originalCap=Application.targetFrameRate;
            var originalPipeline=QualitySettings.renderPipeline;
            var source=(originalPipeline??GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            Assert.That(source,Is.Not.Null,"the authored URP pipeline is loaded");
            float sourceScale=source.renderScale,sourceDistance=source.shadowDistance;int sourceSamples=source.msaaSampleCount;
            var host=new GameObject("impl:U34 graphics application");var session=Session(host);
            try
            {
                session.Profile.Data.QualityPreset="High";Update(session);
                session.Profile.Data.QualityPreset=name;Update(session);
                Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()],Is.EqualTo(name),"the selected quality name must reach the production session");
                var applied=QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                Assert.That(applied,Is.Not.Null);Assert.That(applied,Is.Not.SameAs(source),"quality changes use a disposable pipeline");
                Assert.That(applied.renderScale,Is.EqualTo(scale).Within(1e-5f));
                Assert.That(applied.shadowDistance,Is.EqualTo(shadows));Assert.That(applied.shadowCascadeCount,Is.EqualTo(cascades));
                Assert.That(applied.supportsMainLightShadows,Is.EqualTo(shadows>0));Assert.That(applied.msaaSampleCount,Is.EqualTo(samples));
                Assert.That(source.renderScale,Is.EqualTo(sourceScale));Assert.That(source.shadowDistance,Is.EqualTo(sourceDistance));Assert.That(source.msaaSampleCount,Is.EqualTo(sourceSamples));
            }
            finally
            {
                Close(host,session);QualitySettings.SetQualityLevel(originalLevel,false);QualitySettings.renderPipeline=originalPipeline;
                QualitySettings.vSyncCount=originalVsync;Application.targetFrameRate=originalCap;
            }
        }
    }
    public sealed class GraphicsPreferencesIsolationTests
    {
        [Test]public void ARunStartErasesAStaleProjectSettingsRecord()
        {
            bool recorded=UnityEditor.SessionState.GetBool(ProjectSettingsGuard.Recorded,false);
            try
            {
                UnityEditor.SessionState.SetBool(ProjectSettingsGuard.Recorded,true);new ProjectSettingsGuard().RunStarted(null);
                Assert.That(UnityEditor.SessionState.GetBool(ProjectSettingsGuard.Recorded,false),Is.False,"the run start erases the old record");
            }
            finally{UnityEditor.SessionState.SetBool(ProjectSettingsGuard.Recorded,recorded);}
        }
        [Test]public void GraphicsFixturePreservesCallerPreferencesAndRestoresItsScope()
        {
            string previousScope=FestivalInput.PreferencesScope,previousFolder=LocalProfile.Folder;
            string callerScope="fix:U34#1-caller-preferences-"+Guid.NewGuid().ToString("N");
            string callerKey="festival-bindings-"+callerScope+"-default",savedBindings="{invalid";
            var fixture=new GraphicsApplicationTests();bool ownsProfile=false;
            try
            {
                FestivalInput.PreferencesScope=callerScope;PlayerPrefs.SetString(callerKey,savedBindings);
                fixture.OwnGraphicsProfile();ownsProfile=true;
                string ownedScope=FestivalInput.PreferencesScope,ownedFolder=LocalProfile.Folder;
                fixture.FrameCapAppliesWithoutVsyncAndVsyncTakesPriority(60);
                Assert.That(PlayerPrefs.HasKey(callerKey),Is.True,"the real graphics session must not delete its caller's malformed saved bindings");
                Assert.That(PlayerPrefs.GetString(callerKey),Is.EqualTo(savedBindings),"caller data survives unchanged, even when production input rejects it");
                Assert.That(ownedScope,Is.Not.EqualTo(callerScope),"graphics sessions use their fixture's own preferences scope");
                Assert.That(FestivalInput.PreferencesScope,Is.EqualTo(ownedScope),"the fixture keeps its scope until the real session has disposed");
                fixture.RestoreGraphicsProfile();ownsProfile=false;
                Assert.That(FestivalInput.PreferencesScope,Is.EqualTo(callerScope),"teardown restores the scope owned by the caller");
                Assert.That(LocalProfile.Folder,Is.EqualTo(previousFolder));Assert.That(Directory.Exists(ownedFolder),Is.False);
                Assert.That(PlayerPrefs.GetString(callerKey),Is.EqualTo(savedBindings),"teardown leaves the caller's saved controls intact");
            }
            finally
            {
                if(ownsProfile)fixture.RestoreGraphicsProfile();
                PlayerPrefs.DeleteKey(callerKey);FestivalInput.PreferencesScope=previousScope;LocalProfile.Folder=previousFolder;
            }
        }
    }
}
