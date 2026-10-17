using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Festival.Integrations;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // SET-1: the microphone test opens a device only from TEST and closes it at every way out. A device fixture stands in for
    // Unity's Microphone, so no automated run ever records the room.
    public sealed class MicrophoneMonitorTests
    {
        public sealed class Fixture:IFestivalMicrophone
        {
            public string[] Devices {get;set;}={"Desk Mic","Headset"};
            public string Blocked {get;set;}="";
            public bool Authorized {get;set;}=true;
            public bool AuthorizationPending {get;set;}
            public bool FailToStart;
            public float Loudness=.5f;
            public int Requests,Starts,Ends,Position;
            // The device being recorded, or null.
            public string Recording;
            public AudioClip Clip;
            public void RequestAuthorization(){Requests++;AuthorizationPending=true;}
            public AudioClip Start(string device)
            {
                Starts++;if(FailToStart)return null;
                Recording=device;
                var samples=new float[1600];for(int i=0;i<samples.Length;i++)samples[i]=i%2==0?Loudness:-Loudness;
                Clip=AudioClip.Create("impl-U73 fixture microphone",samples.Length,1,16000,false);Clip.SetData(samples,0);Position=800;
                return Clip;
            }
            public int GetPosition(string device)=>Position;
            public bool IsRecording(string device)=>Recording!=null&&Recording==device;
            public void End(string device){Ends++;if(Recording==device)Recording=null;}
        }
        private sealed class VoiceSpy:IConfigurableVoiceSession
        {
            public readonly List<string> Calls=new List<string>();
            public bool Available=>false;
            public string Status=>"impl-U73 voice unavailable";
            public void Configure(VoiceConfiguration configuration){}
            public void Join(string sessionId)=>Calls.Add("Join");
            public void Leave()=>Calls.Add("Leave");
            public void SetLocalMute(bool muted)=>Calls.Add("Mute "+muted);
            public void SetRemoteMute(string playerId,bool muted)=>Calls.Add("RemoteMute");
            public void UpdatePosition(string playerId,float x,float z,bool spirit)=>Calls.Add("Position");
        }
        private string previousFolder,ownedFolder;
        [SetUp]public void OwnATemporaryProfile()
        {
            previousFolder=LocalProfile.Folder;
            ownedFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"impl-U73-microphone-"+Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(ownedFolder);LocalProfile.Folder=ownedFolder;
        }
        [TearDown]public void RemoveOnlyThisTestsProfile()
        {
            LocalProfile.Folder=previousFolder;
            if(System.IO.Directory.Exists(ownedFolder))System.IO.Directory.Delete(ownedFolder,true);
        }

        [Test]public void NothingRecordsUntilStartAndTheLevelFollowsTheSavedGain()
        {
            var mic=new Fixture();var monitor=new FestivalMicrophoneMonitor(mic);
            Assert.That((monitor.State,mic.Starts,mic.Requests),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Off,0,0)),"making the monitor opens nothing");
            Assert.That(monitor.Start("Headset"),Is.Empty);
            Assert.That((monitor.State,mic.Recording),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Listening,"Headset")),"the saved device is the one tested");
            Assert.That(monitor.Update(1),Is.Empty);Assert.That(monitor.Level,Is.EqualTo(.5f).Within(1e-4f),"the meter reads the device's real samples");
            monitor.Update(.5f);Assert.That(monitor.Level,Is.EqualTo(.25f).Within(1e-4f),"the saved mic volume scales the meter");
            var clip=mic.Clip;
            monitor.Stop("dismissed");
            Assert.That((monitor.State,mic.Recording,mic.Ends,monitor.Level),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Off,(string)null,1,0f)));
            Assert.That(clip==null,Is.True,"the recorded clip is released, never kept for playback");
            monitor.Stop("dismissed");Assert.That(mic.Ends,Is.EqualTo(1),"stopping twice ends the device once");
        }

        [Test]public void AMissingSavedDeviceFallsBackToTheSystemDefaultAndNoDeviceNeverStarts()
        {
            var mic=new Fixture();var monitor=new FestivalMicrophoneMonitor(mic);
            Assert.That(monitor.Start("Old USB Mic"),Is.Empty);
            Assert.That(mic.Recording,Is.EqualTo(""),"an unplugged saved mic falls back to the system default");
            monitor.Stop("dismissed");
            mic.Devices=new string[0];
            Assert.That(monitor.Start(""),Is.EqualTo("No microphone found."));
            Assert.That((monitor.State,mic.Starts),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Off,1)));
        }

        [Test]public void PermissionIsAskedForAndADenialOrABlockedBuildNeverOpensTheDevice()
        {
            var mic=new Fixture{Authorized=false};var monitor=new FestivalMicrophoneMonitor(mic);
            Assert.That(monitor.Start(""),Is.Empty);
            Assert.That((monitor.State,mic.Requests,mic.Starts),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Asking,1,0)),"the platform is asked first");
            Assert.That(monitor.Update(1),Is.Empty);Assert.That(monitor.State,Is.EqualTo(FestivalMicrophoneMonitor.Phase.Asking),"while the answer is pending");
            mic.AuthorizationPending=false;
            StringAssert.Contains("permission",monitor.Update(1));
            Assert.That((monitor.State,mic.Starts),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Off,0)),"a denial never opens the device");

            monitor.Start("");mic.Authorized=true;mic.AuthorizationPending=false;
            Assert.That(monitor.Update(1),Is.Empty);
            Assert.That((monitor.State,mic.Starts),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Listening,1)),"a grant opens it");
            mic.Authorized=false;
            StringAssert.Contains("permission",monitor.Update(1));
            Assert.That((monitor.State,mic.Recording,mic.Ends),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Off,(string)null,1)),"withdrawn permission stops the test");

            mic.Authorized=true;mic.Blocked="This build cannot ask for the microphone yet.";
            Assert.That(monitor.Start(""),Is.EqualTo(mic.Blocked));
            Assert.That((mic.Requests,mic.Starts),Is.EqualTo((2,1)),"a build that cannot ask neither asks nor opens");
        }

        [Test]public void AFailedStoppedOrUnpluggedDeviceEndsTheTest()
        {
            var mic=new Fixture{FailToStart=true};var monitor=new FestivalMicrophoneMonitor(mic);
            StringAssert.Contains("could not start",monitor.Start(""));
            Assert.That(monitor.State,Is.EqualTo(FestivalMicrophoneMonitor.Phase.Off));
            mic.FailToStart=false;
            monitor.Start("Headset");mic.Devices=new[]{"Desk Mic"};
            StringAssert.Contains("stopped",monitor.Update(1));
            Assert.That((monitor.State,mic.Recording,mic.Ends),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Off,(string)null,1)),"an unplugged device ends the test");
            monitor.Start("Desk Mic");mic.Recording=null;
            StringAssert.Contains("stopped",monitor.Update(1));
            Assert.That(monitor.State,Is.EqualTo(FestivalMicrophoneMonitor.Phase.Off),"a device the system stopped ends the test");
            monitor.Start("Desk Mic");monitor.Dispose();
            Assert.That((monitor.State,mic.Recording),Is.EqualTo((FestivalMicrophoneMonitor.Phase.Off,(string)null)),"teardown ends recording");
        }

        [Test]public void AnAutomatedRunCanSubstituteTheDeviceForEveryNewMonitor()
        {
            var previous=FestivalMicrophoneMonitor.Substitute;var mic=new Fixture();
            try
            {
                FestivalMicrophoneMonitor.Substitute=mic;
                var monitor=new FestivalMicrophoneMonitor();
                Assert.That(monitor.Devices,Is.EqualTo(mic.Devices));
                monitor.Start("");Assert.That(mic.Starts,Is.EqualTo(1),"the substitute, not the room's microphone, records");
                monitor.Dispose();
            }
            finally{FestivalMicrophoneMonitor.Substitute=previous;}
        }

        private static FestivalSettingsScreen.Row Row(string label)=>FestivalSettingsScreen.Rows.First(r=>r.Tab==FestivalSettingsScreen.Tab.Voice&&r.Label==label);

        [Test]public void TheVoiceTabTestsOnlyFromItsButtonAndStopsAtEveryWayOut()
        {
            var mic=new Fixture();var voice=new VoiceSpy();var profile=new LocalProfile("impl-U73-voice");
            using(var settings=new FestivalSettings(profile))
            {
                var screen=new FestivalSettingsScreen(profile,settings,voice,null,null,mic);
                screen.Open(FestivalSettingsScreen.Tab.Voice);screen.Update();
                var test=Row("TEST MICROPHONE");
                Assert.That((mic.Starts,mic.Requests,screen.Value(test)),Is.EqualTo((0,0,"OFF")),"opening the tab opens no microphone");
                Assert.That(screen.ButtonLabel(test,0),Is.EqualTo("TEST"));

                Assert.That(screen.Change(test,1),Is.Empty);screen.Update();
                Assert.That((mic.Starts,screen.Value(test),screen.ButtonLabel(test,0)),Is.EqualTo((1,"50%","STOP")),"TEST starts the meter");
                StringAssert.Contains("Nothing is recorded, sent or played back",screen.Message);
                Assert.That(screen.MicrophoneLevel,Is.EqualTo(.5f).Within(1e-4f));
                screen.Change(Row("MIC VOLUME"),-1);screen.Change(Row("MIC VOLUME"),-1);screen.Update();
                Assert.That(screen.Value(test),Is.EqualTo("45%"),"the saved mic volume reaches the meter at once");
                Assert.That(new LocalProfile("impl-U73-voice").Data.InputGain,Is.EqualTo(.9f).Within(1e-4f));

                screen.Change(test,1);
                Assert.That((mic.Ends,mic.Recording,screen.Value(test)),Is.EqualTo((1,(string)null,"OFF")),"STOP dismisses the test");
                screen.Change(test,1);screen.Open(FestivalSettingsScreen.Tab.Audio);
                Assert.That((mic.Ends,mic.Recording),Is.EqualTo((2,(string)null)),"another tab stops it");
                screen.Open(FestivalSettingsScreen.Tab.Voice);screen.Update();
                Assert.That(mic.Starts,Is.EqualTo(2),"coming back does not restart it");
                screen.Change(test,1);screen.FocusChanged(false);
                Assert.That((mic.Ends,mic.Recording),Is.EqualTo((3,(string)null)),"losing focus stops it");
                screen.Change(test,1);screen.Close();
                Assert.That((mic.Ends,mic.Recording),Is.EqualTo((4,(string)null)),"closing the screen stops it");

                screen.Change(test,1);mic.Devices=new string[0];screen.Update();
                Assert.That((screen.Failed,mic.Recording),Is.EqualTo((true,(string)null)));StringAssert.Contains("stopped",screen.Message);
                mic.Devices=new[]{"Desk Mic"};mic.Authorized=false;screen.Change(test,1);screen.Update();
                Assert.That(screen.Value(test),Is.EqualTo("ASKING PERMISSION"));
                mic.AuthorizationPending=false;screen.Update();
                Assert.That(screen.Failed,Is.True);StringAssert.Contains("permission",screen.Message);
                Assert.That(mic.Recording,Is.Null,"a denial shows why and records nothing");
                Assert.That(voice.Calls,Is.Empty,"the test never reaches the voice provider: nothing is sent");
            }
        }

        [Test]public void TheMicrophoneRowListsTheReportedDevicesOrNoMicrophoneFound()
        {
            var mic=new Fixture();var profile=new LocalProfile("impl-U73-devices");
            using(var settings=new FestivalSettings(profile))
            {
                var screen=new FestivalSettingsScreen(profile,settings,new UnavailableVoiceSession(),null,null,mic);
                screen.Open(FestivalSettingsScreen.Tab.Voice);
                var device=Row("MICROPHONE");var seen=new List<string>{screen.Value(device)};
                for(int i=0;i<3;i++){Assert.That(screen.Change(device,1),Is.Empty);seen.Add(screen.Value(device));}
                Assert.That(seen,Is.EqualTo(new[]{"SYSTEM DEFAULT","Desk Mic","Headset","Headset"}),"the system default, then each device the platform reports");
                Assert.That(new LocalProfile("impl-U73-devices").Data.MicrophoneDevice,Is.EqualTo("Headset"),"the chosen device is saved");
                screen.Change(Row("TEST MICROPHONE"),1);Assert.That(mic.Recording,Is.EqualTo("Headset"));
                screen.Change(device,-1);Assert.That(mic.Recording,Is.EqualTo("Desk Mic"),"a running test follows the chosen device");
                screen.Change(Row("TEST MICROPHONE"),1);
                mic.Devices=new[]{"Desk Mic"};profile.Data.MicrophoneDevice="Headset";
                Assert.That(screen.Value(device),Is.EqualTo("SYSTEM DEFAULT (SAVED MIC MISSING)"));
                mic.Devices=new string[0];
                Assert.That(screen.Value(device),Is.EqualTo("NO MICROPHONE FOUND"));
                Assert.That((screen.Usable(device),screen.Usable(Row("TEST MICROPHONE"))),Is.EqualTo((false,false)),"there is nothing to choose or test");
                Assert.That(screen.Value(Row("VOICE CHAT")),Is.EqualTo(FestivalSettingsScreen.VoiceExplanation),"the tab is honest that voice chat is not here yet");
            }
            Assert.That(new FestivalMicrophoneMonitor().Devices,Is.EqualTo(Microphone.devices),"the real adapter lists Unity's devices; listing records nothing");
        }

        [Test]public void ABuildThatCannotUseTheMicrophoneSaysSoRatherThanNoMicrophoneFound()
        {
            var mic=new Fixture{Devices=new string[0],Blocked="This build cannot ask for the microphone yet, so nothing was recorded."};
            var profile=new LocalProfile("final-gates-blocked-mic");
            using(var settings=new FestivalSettings(profile))
            {
                var screen=new FestivalSettingsScreen(profile,settings,new UnavailableVoiceSession(),null,null,mic);
                screen.Open(FestivalSettingsScreen.Tab.Voice);screen.Update();
                var device=Row("MICROPHONE");
                Assert.That(screen.Value(device),Is.EqualTo("NOT IN THIS BUILD YET"),"a Mac build without a usage description cannot list microphones");
                Assert.That((screen.Usable(device),screen.Usable(Row("TEST MICROPHONE"))),Is.EqualTo((false,false)),"there is nothing to choose or test");
                Assert.That((mic.Starts,mic.Requests),Is.EqualTo((0,0)));
            }
        }

        [Test]public void ATapTestWithTooFewTapsSaysWhyAndLeavesTheTimingAndAudioAlone()
        {
            var source=new GameObject("impl-U73 tap audio").AddComponent<AudioSource>();
            var profile=new LocalProfile("impl-U73-tap");profile.Data.TimingCalibrationMs=37;Assert.That(profile.Save(),Is.Empty);
            try
            {
                using(var input=new FestivalInput("impl-U73-tap",profile))
                using(var settings=new FestivalSettings(profile))
                {
                    var screen=new FestivalSettingsScreen(profile,settings,new UnavailableVoiceSession(),input,source,new Fixture());
                    screen.Open(FestivalSettingsScreen.Tab.Controls);
                    var timing=FestivalSettingsScreen.Rows.First(r=>r.Label=="RHYTHM TIMING");
                    Assert.That((screen.Shows(timing),screen.Value(timing),screen.ButtonLabel(timing,0)),Is.EqualTo((true,"+37 MS","TAP TEST")));
                    screen.Change(timing,1);
                    Assert.That((source.clip?.name,screen.Value(timing),screen.ButtonLabel(timing,0)),Is.EqualTo(("Festival calibration pulses","TAP 0 / 8","STOP")),"TAP TEST schedules the real pulses");
                    screen.OpenSection("MOVE");
                    Assert.That(source.clip,Is.Null,"another section ends the test and gives the source back");
                    screen.OpenSection("GENERAL");screen.Change(timing,1);
                    Thread.Sleep(4900);screen.Update();
                    Assert.That(screen.Failed,Is.True);StringAssert.Contains("four beats",screen.Message);
                    Assert.That((source.clip,screen.ButtonLabel(timing,0)),Is.EqualTo(((AudioClip)null,"TAP TEST")),"the finished test lets the source go");
                    Assert.That(new LocalProfile("impl-U73-tap").Data.TimingCalibrationMs,Is.EqualTo(37),"too few taps never change the timing");
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(source.gameObject);}
        }
    }
}
