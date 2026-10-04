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

namespace Festival.Tests
{
    public sealed class SettingsTests
    {
        private string previousFolder,ownedFolder;
        [SetUp]public void OwnATemporaryProfile()
        {
            previousFolder=LocalProfile.Folder;
            ownedFolder=Path.Combine(Path.GetTempPath(),"impl-U04-settings-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ownedFolder);LocalProfile.Folder=ownedFolder;
        }
        [TearDown]public void RemoveOnlyThisTestsProfile()
        {
            LocalProfile.Folder=previousFolder;
            if(Directory.Exists(ownedFolder))Directory.Delete(ownedFolder,true);
        }
        private void Write(string json)=>File.WriteAllText(Path.Combine(ownedFolder,"settings.json"),json);

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
                case "Controls":expected.MouseSensitivity=.12f;expected.InvertY=false;expected.ThirdPerson=false;expected.TimingCalibrationMs=0;expected.BindingOverridesJson="";break;
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
}
