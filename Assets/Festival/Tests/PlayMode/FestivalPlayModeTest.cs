using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
    /// <summary>Base for every PlayMode fixture that builds a festival world or hosts a session.</summary>
    public abstract class FestivalPlayModeTest
    {
        private string previousProfileFolder,ownedProfileFolder;
        private string previousPreferencesScope,ownedBindingKey;
        private readonly List<FestivalInput> ownedControls=new List<FestivalInput>();
        private readonly HashSet<string> ownedControlKeys=new HashSet<string>();
        protected Keyboard TestKeyboard;
        protected Mouse TestMouse;
        protected InputSettings.BackgroundBehavior InputBackgroundBeforeTest;
        protected InputSettings.EditorInputBehaviorInPlayMode InputEditorBeforeTest;
        private bool inputConfigured;

        private void UseUnfocusedInput()
        {
            if(inputConfigured)return;
            var input=InputSystem.settings;InputBackgroundBeforeTest=input.backgroundBehavior;InputEditorBeforeTest=input.editorInputBehaviorInPlayMode;
            inputConfigured=true;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        protected void UseAVirtualKeyboard(string name){UseUnfocusedInput();if(TestKeyboard==null)TestKeyboard=InputSystem.AddDevice<Keyboard>(name);}
        protected void UseAVirtualMouse(string name){UseUnfocusedInput();if(TestMouse==null)TestMouse=InputSystem.AddDevice<Mouse>(name);}
        protected FestivalInput NewTestControls(string profile)
        {
            var controls=new FestivalInput(profile);ownedControls.Add(controls);
            string scope=FestivalInput.PreferencesScope;
            ownedControlKeys.Add("festival-bindings-"+(string.IsNullOrEmpty(scope)?"":scope+"-")+profile);
            return controls;
        }
        // The events reach the real InputActions and HUD consumers. Two frames cover both input and the consumer's Update.
        protected IEnumerator Tap(Key key,float releaseWait=0)
        {
            InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());
            if(releaseWait>0)yield return new WaitForSeconds(releaseWait);else{yield return null;yield return null;}
        }
        [SetUp]public void UseThisTestsProfileFolder()
        {
            previousProfileFolder=LocalProfile.Folder;
            ownedProfileFolder=Path.Combine(previousProfileFolder,"impl-U01-test-"+System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ownedProfileFolder);LocalProfile.Folder=ownedProfileFolder;
            previousPreferencesScope=FestivalInput.PreferencesScope;FestivalInput.PreferencesScope=ownedProfileFolder;
            var args=System.Environment.GetCommandLineArgs();int at=System.Array.IndexOf(args,"--profile");
            string profile=at>=0&&at+1<args.Length?args[at+1]:"default";
            ownedBindingKey="festival-bindings-"+ownedProfileFolder+"-"+profile;
        }

        // The port every fixture hosts its session on. 0 lets the OS pick a free one, so a PlayMode run in a snapshot and one in
        // the shared checkout can host at the same time. A fixture that joins a client hosts on FreePort instead, as a client has
        // to know the port it joins.
        protected const ushort HostPort=0;

        /// <summary>A port nobody holds right now: the OS picks one for the probe, which lets it go for the host.</summary>
        protected static ushort FreePort(){using(var probe=new UdpClient(new IPEndPoint(IPAddress.Any,0)))return (ushort)((IPEndPoint)probe.Client.LocalEndPoint).Port;}

        // TEST-2: how long a skipped spin still runs on the host's clock: at least two of its 10 Hz snapshots (FestivalSession.Update)
        // go out meanwhile, so a joined client sees the spin.
        protected const double SpinHoldSeconds=.25;

        protected static FestivalSimulation Simulation(FestivalSession session)=>(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);

        // TEST-2: a hosted level starts in about a second, not 22. The host still arms the launch countdown and runs StartRound
        // (the spin, roles and twists), the Spinning -> Loading -> Playing steps and every MapReady through its real Step code;
        // only the waits go. SpinnerTakeTests keeps the real countdown and spin.
        /// <summary>The host walks to the trailhead gate and readies up, and the level starts: the countdown, spin and loading
        /// each skipped. Every other player must be ready already. A real joined client's player (realClients) reports its own
        /// map loaded.</summary>
        protected static IEnumerator StartLevel(FestivalSession session,params string[] realClients)
        {
            var player=Simulation(session).Player(session.LocalPlayerId);player.X=0;player.Z=19;session.Command("Ready");
            yield return SkipCountdown(session);yield return SkipSpin(session);yield return FinishLoading(session,realClients);
        }

        /// <summary>Once the whole crew has sent Ready, the round starts on the host's next step. The host's own Step arms the
        /// 5 s countdown; this pulls it down to now every frame, as Step re-arms it after any unready moment and every Ready
        /// resets it. The level starts in first person (CAM-1).</summary>
        protected static IEnumerator SkipCountdown(FestivalSession session)
        {
            var sim=Simulation(session);
            // CAM-1: every hosted level starts in first person, whatever this machine's saved profile says; a test that wants the view
            // over the shoulder turns it on after. What a test saves goes to its own disposable profile folder.
            session.Profile.Data.ThirdPerson=false;
            for(float deadline=Time.realtimeSinceStartup+15;session.State.Phase=="Shopping"&&Time.realtimeSinceStartup<deadline;)
            {
                if(sim.State.Phase=="Shopping"&&sim.State.LaunchAtSeconds>0)sim.State.LaunchAtSeconds=sim.State.SimulationSeconds;
                yield return null;
            }
            Assert.That(session.State.Phase,Is.EqualTo("Spinning"),"the ready countdown ends in the spin: "+session.Message);
        }

        /// <summary>The spin ends SpinHoldSeconds from now, and the round moves on to loading.</summary>
        protected static IEnumerator SkipSpin(FestivalSession session)
        {
            var sim=Simulation(session);
            Assert.That(sim.State.Phase,Is.EqualTo("Spinning"),"the spin is on: "+session.Message);
            sim.State.SpinEndsAt=sim.State.SimulationSeconds+SpinHoldSeconds;
            for(float deadline=Time.realtimeSinceStartup+30;session.State.Phase=="Spinning"&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(session.State.Phase,Is.EqualTo("Loading").Or.EqualTo("Playing"),"the spin ends in loading: "+session.Message);
        }

        /// <summary>Every player without a client of their own reports their map loaded, as the host's own client does, and the
        /// festival opens. A real joined client's player (realClients) reports for itself.</summary>
        protected static IEnumerator FinishLoading(FestivalSession session,params string[] realClients)
        {
            var sim=Simulation(session);
            for(float deadline=Time.realtimeSinceStartup+60;session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline;)
            {
                if(sim.State.Phase=="Loading")foreach(var p in sim.State.Players)
                    if(p.Connected&&!p.MapReady&&p.Id!=session.LocalPlayerId&&System.Array.IndexOf(realClients,p.Id)<0)sim.Execute(p.Id,new GameCommand{Id=System.Guid.NewGuid().ToString("N"),Kind="MapReady"});
                yield return null;
            }
            Assert.That(session.State.Phase,Is.EqualTo("Playing"),"the festival opens: "+session.Message);
        }

        // Unity's test runner stops a UnityTest at the first unexpected error log (or its timeout) without running the test's
        // own finally, so the world and session it built would survive into every later test. This runs after every test,
        // however it ended, and waits a frame so they are gone before the next test starts.
        [UnityTearDown]public IEnumerator DestroyWhatTheTestLeft()
        {
            try
            {
                foreach(var session in Object.FindObjectsByType<FestivalSession>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.Destroy(session.gameObject);
                foreach(var world in Object.FindObjectsByType<FestivalWorld>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.Destroy(world.gameObject);
                yield return null;
            }
            finally
            {
                foreach(var controls in ownedControls)controls.Dispose();ownedControls.Clear();
                if(TestMouse!=null){InputSystem.RemoveDevice(TestMouse);TestMouse=null;}
                if(TestKeyboard!=null){InputSystem.RemoveDevice(TestKeyboard);TestKeyboard=null;}
                if(inputConfigured)
                {
                    InputSystem.settings.backgroundBehavior=InputBackgroundBeforeTest;InputSystem.settings.editorInputBehaviorInPlayMode=InputEditorBeforeTest;
                    inputConfigured=false;
                }
                LocalProfile.Folder=previousProfileFolder;
                FestivalInput.PreferencesScope=previousPreferencesScope;
                if(ownedBindingKey!=null){PlayerPrefs.DeleteKey(ownedBindingKey);ownedBindingKey=null;}
                foreach(string key in ownedControlKeys)PlayerPrefs.DeleteKey(key);ownedControlKeys.Clear();PlayerPrefs.Save();
                if(ownedProfileFolder!=null&&Directory.Exists(ownedProfileFolder))Directory.Delete(ownedProfileFolder,true);
                ownedProfileFolder=null;
            }
        }
    }

    // HUD-2 (gate hazard): a PlayMode run in a snapshot and one in the shared checkout can run at the same time on one machine.
    public sealed class PlayModeIsolationTests:FestivalPlayModeTest
    {
        private string savedFolder;
        private FestivalInput leftControls;
        private string leftProfileId,leftProfileFolder;
        private InputSettings.BackgroundBehavior leftBackground;
        private InputSettings.EditorInputBehaviorInPlayMode leftEditor;

        [UnityTest,Order(7)]public IEnumerator AStoppedTestLeavesItsVirtualInputAndSavedControls()
        {
            leftBackground=InputSystem.settings.backgroundBehavior;leftEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            UseAVirtualKeyboard("impl-U01 cleanup keyboard");UseAVirtualMouse("impl-U01 cleanup mouse");
            UseAVirtualKeyboard("impl-U01 cleanup keyboard"); // Repeated setup must retain the original settings and device.
            string id="impl-U01-cleanup-"+System.Guid.NewGuid().ToString("N");
            leftControls=NewTestControls(id);leftProfileId=id;leftProfileFolder=LocalProfile.Folder;
            leftControls.Rebind(leftControls.Interact,0,null);yield return null;yield return Tap(Key.K,.3f);
            Assert.That(leftControls.Rebinding,Is.False,"the real key completes the rebind");
            Assert.That(leftControls.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/k"));
            Assert.That(new LocalProfile(leftProfileId).Data.BindingOverridesJson,Is.Not.Empty,"the real rebind saves the authoritative profile");
            Assert.That(NewTestControls(leftProfileId).Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/k"),"production controls reload the persisted binding");
        }

        [UnityTest,Order(8)]public IEnumerator TheNextTestHasNoVirtualInputOrSavedControlsLeft()
        {
            Assert.That(leftControls.Map.enabled,Is.False,"teardown disposes controls even when the test never reaches its own cleanup");
            Assert.That(Directory.Exists(leftProfileFolder),Is.False,"teardown retires the preceding test's owned profile folder");
            foreach(var device in InputSystem.devices)Assert.That(device.name.StartsWith("impl-U01 cleanup"),Is.False,"a keyboard or mouse leaked");
            Assert.That((InputSystem.settings.backgroundBehavior,InputSystem.settings.editorInputBehaviorInPlayMode),Is.EqualTo((leftBackground,leftEditor)),"repeated setup still restores the original input settings");
            yield break;
        }

        [UnityTest,Order(6)]public IEnumerator BindingsSaveOnlyInsideTheirDisposableScope()
        {
            string id="impl-U01-bindings-"+System.Guid.NewGuid().ToString("N");
            string previousScope=FestivalInput.PreferencesScope;
            UseAVirtualKeyboard("impl-U01 binding keyboard");
            string profileFolder=LocalProfile.Folder;
            try
            {
                FestivalInput.PreferencesScope=LocalProfile.Folder;
                var controls=NewTestControls(id);
                controls.Rebind(controls.Interact,0,null);yield return null;yield return Tap(Key.K,.3f);
                Assert.That(controls.Rebinding,Is.False,"the real keyboard completes the rebind");
                Assert.That(controls.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/k"),"the real key changes the action");
                Assert.That(new LocalProfile(id).Data.BindingOverridesJson,Is.Not.Empty,"the rebind saves inside the disposable authoritative profile store");
                string[] files=Directory.GetFiles(profileFolder,"*.json");Assert.That(files.Length,Is.EqualTo(1));
                string saved=File.ReadAllText(files[0]);
                Assert.That(NewTestControls(id).Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/k"),"recreated production controls reload the same identity");
                Assert.That(File.ReadAllText(files[0]),Is.EqualTo(saved),"recreating controls preserves the same identity's saved bytes");
                Assert.That(PlayerPrefs.HasKey("festival-bindings-"+id),Is.False,"the unscoped identity is untouched");
                string nextFolder=Path.Combine(profileFolder,"impl-U36-next-profile");Directory.CreateDirectory(nextFolder);
                LocalProfile.Folder=nextFolder;FestivalInput.PreferencesScope=nextFolder;
                var fresh=NewTestControls(id);
                Assert.That(fresh.Interact.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/e"),"another owned profile folder starts from the default binding");
                Assert.That(new LocalProfile(id).Data.BindingOverridesJson,Is.Empty);
                Assert.That(File.ReadAllText(files[0]),Is.EqualTo(saved),"the nested scope never changes its parent's saved identity");
            }
            finally
            {
                // The unscoped probe identity also belongs to this GUID; the fixture retires the scoped identities.
                PlayerPrefs.DeleteKey("festival-bindings-"+id);
                LocalProfile.Folder=profileFolder;FestivalInput.PreferencesScope=previousScope;
            }
        }

        // Saving during one hosted test still persists within that test, including after the session is rebuilt.
        [UnityTest,Order(4)]public IEnumerator AHostedTestCanReloadWhatItSaved()
        {
            var host=new GameObject("Profile saving session");var session=host.AddComponent<FestivalSession>();
            session.Host("Tester",HostPort);yield return null;
            savedFolder=LocalProfile.Folder;
            session.Profile.Data.ReducedMotion=!new ProfileData().ReducedMotion;
            session.Profile.Data.HighContrast=!new ProfileData().HighContrast;
            session.Profile.Data.ThirdPerson=true;session.Profile.Data.SeenTips.Add("test3-saved-tip");
            string saved=JsonUtility.ToJson(session.Profile.Data);
            Assert.That(session.Profile.Save(),Is.Empty,"the hosted session saves its profile");
            Object.Destroy(host);yield return null;
            session=new GameObject("Profile reloaded session").AddComponent<FestivalSession>();
            Assert.That(JsonUtility.ToJson(session.Profile.Data),Is.EqualTo(saved),"a new session in this test loads the saved settings");
        }

        // The previous test deliberately leaves its session alive, so teardown must close it before retiring its folder.
        [UnityTest,Order(5)]public IEnumerator TheNextHostedTestStartsFromProfileDefaults()
        {
            var session=new GameObject("Fresh profile session").AddComponent<FestivalSession>();
            Assert.That(JsonUtility.ToJson(session.Profile.Data),Is.EqualTo(JsonUtility.ToJson(new ProfileData())),"saved comfort settings, tips and camera view must not leak into another test");
            Assert.That(LocalProfile.Folder,Is.Not.EqualTo(savedFolder),"every hosted test owns a distinct subfolder");
            Assert.That(Directory.Exists(savedFolder),Is.False,"the preceding test's folder was retired");
            session.Host("Tester",HostPort);yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,"a fresh profile still hosts");
        }

        // The other run holds the port this run's fixtures host on; this run's session still opens its lobby.
        [UnityTest,Order(1)]public IEnumerator ASessionHostsWhileAnotherRunHoldsItsPort()
        {
            var otherRun=new UdpClient(new IPEndPoint(IPAddress.Any,HostPort));
            var world=new GameObject("Isolation world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Isolation session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
            }
            finally{otherRun.Close();session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }

        // A test stopped by an error log or a timeout never runs its own finally: it leaves its world and hosted session behind.
        [UnityTest,Order(2)]public IEnumerator ATestStoppedMidwayLeavesItsWorldAndSession()
        {
            var world=new GameObject("Stopped test world");world.AddComponent<FestivalWorld>();yield return null;
            var session=new GameObject("Stopped test session").AddComponent<FestivalSession>();
            yield return null;
            session.Host("Tester",HostPort);
            float deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,"setup: host has a local player: "+session.Message);
        }

        // The next test still starts clean, so one failure cannot cascade into every later test.
        [UnityTest,Order(3)]public IEnumerator TheNextTestStartsWithNoWorldOrSession()
        {
            Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsInactive.Include,FindObjectsSortMode.None),Is.Empty,"a world leaked from an earlier test");
            Assert.That(Object.FindObjectsByType<FestivalSession>(FindObjectsInactive.Include,FindObjectsSortMode.None),Is.Empty,"a session leaked from an earlier test");
            yield break;
        }
    }
}
