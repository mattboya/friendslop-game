using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class SmokeProfileSessionTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        string previousFolder,previousScope,caller,callerKey;
        byte[] callerBytes;
        readonly List<GameObject> holders=new List<GameObject>();
        const string Profile="smoke_solo",Bindings="caller-owned malformed controls";
        string CallerPath=>Path.Combine(caller,Profile+".json");

        [SetUp]public void SeedOnlyDisposableCallerData()
        {
            previousFolder=LocalProfile.Folder;previousScope=FestivalInput.PreferencesScope;
            caller=Path.Combine(previousFolder,"jfix-CP01-caller-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(caller);LocalProfile.Folder=caller;FestivalInput.PreferencesScope=caller;
            var seed=new LocalProfile(Profile);seed.Data.PlayerName="Caller sentinel";seed.Data.SeenTips.Add("caller-only-tip");
            Assert.That(seed.Save(),Is.Empty);callerBytes=File.ReadAllBytes(CallerPath);
            callerKey="festival-bindings-"+caller+"-"+Profile;PlayerPrefs.SetString(callerKey,Bindings);PlayerPrefs.Save();
        }
        [TearDown]public void ReleaseOnlyThisTestsData()
        {
            foreach(var holder in holders)if(holder!=null)Destroy(holder.GetComponent<FestivalSession>());holders.Clear();
            LocalProfile.Folder=previousFolder;FestivalInput.PreferencesScope=previousScope;
            PlayerPrefs.DeleteKey(callerKey);PlayerPrefs.Save();
            if(Directory.Exists(caller))Directory.Delete(caller,true);
        }
        FestivalSession Session(string flag,string profile=Profile)
        {
            var holder=new GameObject("jfix:CP01#1 smoke preferences");holders.Add(holder);
            var session=holder.AddComponent<FestivalSession>();
            // EditMode does not call Awake. The production initialization route is also used by Awake.
            var initialize=typeof(FestivalSession).GetMethod("InitializePreferences",Private);
            if(initialize!=null)initialize.Invoke(session,new object[]{flag==""?new[]{"--profile",profile}:new[]{flag,"--profile",profile}});
            else
            {
                Assert.That(Environment.GetCommandLineArgs(),Does.Contain("--solo-smoke-test"),"red runner supplies the real native flag to the existing Awake");
                typeof(FestivalSession).GetMethod("Awake",Private).Invoke(session,null);
            }
            return session;
        }
        string AssertSmokeOwnsStorage(FestivalSession session)
        {
            string owned=LocalProfile.Folder;
            TestContext.WriteLine("jfix:CP01#1 SMOKE_BOOTSTRAP name="+session.Profile.Data.PlayerName+" sameCallerRoot="+(owned==caller)+" sameCallerScope="+(FestivalInput.PreferencesScope==caller)+" callerKeyPreserved="+(PlayerPrefs.GetString(callerKey)==Bindings));
            Assert.That(owned,Is.Not.EqualTo(caller),"smoke must select owned storage before the real profile/input constructors");
            Assert.That(FestivalInput.PreferencesScope,Is.EqualTo(owned),"smoke preferences share its unique owned identity");
            Assert.That(session.Profile.Data.PlayerName,Is.EqualTo(ProfileData.DefaultName),"smoke cannot read the caller profile");
            Assert.That(Directory.Exists(owned),Is.True);AssertCallerUnchanged();return owned;
        }
        void AssertCallerUnchanged()
        {
            Assert.That(File.ReadAllBytes(CallerPath),Is.EqualTo(callerBytes),"the real acknowledgement/save cannot change caller bytes");
            Assert.That(PlayerPrefs.GetString(callerKey),Is.EqualTo(Bindings),"input load cannot delete caller controls");
        }
        static void Acknowledge(FestivalSession session,string id)
        {
            var game=new FestivalSimulation(19);var owner=game.AddPlayer("smoke_owner","Owner");game.AddPlayer("smoke_friend","Friend");
            game.State.Phase="Playing";game.State.Npcs.Clear();owner.X=owner.Z=0;
            game.State.Npcs.Add(new NpcState{Id="smoke_partner",Kind="Wook",Z=1.5f,Yaw=180,CanTalk=true});
            Assert.That(game.Execute(owner.Id,new GameCommand{Id="dance",Kind="Dance",TargetId="smoke_partner"}).Accepted,Is.True);
            var interaction=game.Interaction(owner.InteractionId);
            // A stable storage sentinel on a real active dance; no disconnected UI call can save it optimistically.
            interaction.DialogueId="line-"+id;
            Assert.That(game.Execute("smoke_friend",new GameCommand{Id="foreign_ack",Kind="DialogueAck",TargetId=interaction.DialogueId}).Accepted,Is.False);
            Assert.That(game.Execute(owner.Id,new GameCommand{Id="owner_ack",Kind="DialogueAck",TargetId=interaction.DialogueId}).Accepted,Is.True);
            string epoch=Guid.NewGuid().ToString("N");typeof(FestivalSession).GetField("historyEpoch",Private).SetValue(session,epoch);
            typeof(FestivalSession).GetField("historyRevision",Private).SetValue(session,-1L);
            Type packetType=typeof(FestivalSession).Assembly.GetType("Festival.Network.DialogueHistoryPacket");var packet=Activator.CreateInstance(packetType);
            packetType.GetField("Epoch").SetValue(packet,epoch);packetType.GetField("Revision").SetValue(packet,owner.DialogueRevision);packetType.GetField("Seen").SetValue(packet,new List<string>(owner.Dialogue.Seen));
            var receive=typeof(FestivalSession).GetMethod("ReceiveAcknowledgedHistory",Private);
            Assert.That(receive.Invoke(session,new object[]{1UL,packet,false}),Is.False,"foreign sender cannot save the fixture marker");
            Assert.That(receive.Invoke(session,new object[]{0UL,packet,false}),Is.True,"only validated host acknowledgment reaches persistence");
            typeof(FestivalSession).GetMethod("SaveAcknowledgedHistory",Private).Invoke(session,null);
        }
        static string SeedOwnedKey(string folder,string profile)
        {
            string key="festival-bindings-"+folder+"-"+profile;PlayerPrefs.SetString(key,"owned-smoke-controls");PlayerPrefs.Save();return key;
        }
        void Destroy(FestivalSession session)
        {
            // EditMode never entered Unity's Awake lifecycle, so it does not schedule OnDestroy automatically.
            // Remove the probe's camera/root immediately before the production teardown uses runtime Destroy.
            if(session.ViewCamera!=null)UnityEngine.Object.DestroyImmediate(session.ViewCamera.gameObject);
            var root=(Transform)typeof(FestivalSession).GetField("actorRoot",Private).GetValue(session);
            if(root!=null)UnityEngine.Object.DestroyImmediate(root.gameObject);
            typeof(FestivalSession).GetMethod("OnDestroy",Private).Invoke(session,null);
            UnityEngine.Object.DestroyImmediate(session.gameObject);
        }
        void AssertRestored(string owned,params string[] keys)
        {
            Assert.That(LocalProfile.Folder,Is.EqualTo(caller));Assert.That(FestivalInput.PreferencesScope,Is.EqualTo(caller));
            Assert.That(Directory.Exists(owned),Is.False,"only the owned temporary folder is retired");
            foreach(string key in keys)Assert.That(PlayerPrefs.HasKey(key),Is.False,"the owned controls identity is retired");
            AssertCallerUnchanged();
        }

        [Test,Explicit("Requires an existing native smoke flag and --profile smoke_solo in an owned snapshot.")]public void NativeSmokeArgumentsReachTheRealAwakeBeforeAnyPreferencesLoad()
        {
            var args=Environment.GetCommandLineArgs();
            Assert.That(args.Contains("--smoke-test")||args.Contains("--solo-smoke-test")||args.Contains("--ui-screens"),Is.True,"setup: the explicit native probe requires an actual smoke argument");
            int at=Array.IndexOf(args,"--profile");Assert.That(at,Is.GreaterThanOrEqualTo(0));Assert.That(args[at+1],Is.EqualTo(Profile));
            var holder=new GameObject("jfix:CP01#1 actual smoke Awake");holders.Add(holder);
            var session=holder.AddComponent<FestivalSession>();typeof(FestivalSession).GetMethod("Awake",Private).Invoke(session,null);
            string owned=AssertSmokeOwnsStorage(session);Acknowledge(session,"actual-awake");AssertCallerUnchanged();
            string key=SeedOwnedKey(owned,Profile);Destroy(session);AssertRestored(owned,key);
        }

        [TestCase("--smoke-test"),TestCase("--solo-smoke-test"),TestCase("--ui-screens")]
        public void EveryNativeSmokeFlagIsolatesBeforeLoadingAndRestoresAfterRealSave(string flag)
        {
            var session=Session(flag);string owned=AssertSmokeOwnsStorage(session);
            Acknowledge(session,"smoke-ack");AssertCallerUnchanged();
            Assert.That(new LocalProfile(Profile).Data.SeenDialogue,Does.Contain("line-smoke-ack"),"the production consumer saved to owned storage");
            string key=SeedOwnedKey(owned,Profile);Destroy(session);AssertRestored(owned,key);
            var next=Session(flag);string nextOwned=AssertSmokeOwnsStorage(next);Assert.That(nextOwned,Is.Not.EqualTo(owned),"a later run owns fresh storage");
            Assert.That(next.Profile.Data.SeenDialogue,Is.Empty,"retired smoke data cannot leak into a repeated run");Destroy(next);AssertRestored(nextOwned);
            // Ordinary bootstrap retains its existing caller load/save behavior.
            var ordinary=Session("");Assert.That(LocalProfile.Folder,Is.EqualTo(caller));Assert.That(FestivalInput.PreferencesScope,Is.EqualTo(caller));
            Assert.That(ordinary.Profile.Data.PlayerName,Is.EqualTo("Caller sentinel"));Assert.That(PlayerPrefs.GetString(callerKey),Is.EqualTo(Bindings),"normal input preserves legacy preferences byte-for-byte");
            Acknowledge(ordinary,"normal-ack");Assert.That(File.ReadAllBytes(CallerPath).SequenceEqual(callerBytes),Is.False,"ordinary acknowledgement still persists normally");Destroy(ordinary);
        }

        [TestCase(false),TestCase(true)]
        public void OverlappingReplacementSessionsKeepTheLiveLeaseInEitherDestructionOrder(bool newestFirst)
        {
            var first=Session("--ui-screens");string owned=AssertSmokeOwnsStorage(first);string firstKey=SeedOwnedKey(owned,Profile);
            var second=Session("--ui-screens","smoke_client");Assert.That(LocalProfile.Folder,Is.EqualTo(owned),"replacement Awake shares the process-owned lease");
            string secondKey=SeedOwnedKey(owned,"smoke_client");
            Destroy(newestFirst?second:first);
            Assert.That(LocalProfile.Folder,Is.EqualTo(owned),"an old or nested teardown cannot clobber the live root");
            Assert.That(FestivalInput.PreferencesScope,Is.EqualTo(owned));Assert.That(Directory.Exists(owned),Is.True);
            var survivor=newestFirst?first:second;Acknowledge(survivor,"survivor");AssertCallerUnchanged();
            Assert.That(new LocalProfile(newestFirst?Profile:"smoke_client").Data.SeenDialogue,Does.Contain("line-survivor"));
            Destroy(survivor);AssertRestored(owned,firstKey,secondKey);
        }

        [Test]public void RepeatedRealTeardownReleasesTheLeaseOnce()
        {
            var session=Session("--solo-smoke-test");string owned=AssertSmokeOwnsStorage(session);string key=SeedOwnedKey(owned,Profile);
            typeof(FestivalSession).GetMethod("OnDestroy",Private).Invoke(session,null);
            typeof(FestivalSession).GetMethod("OnDestroy",Private).Invoke(session,null);
            Destroy(session);AssertRestored(owned,key);
        }

        [TestCase(false),TestCase(true)]
        public void PreferenceConstructionFailureReleasesOnlyItsOwnLease(bool overlapping)
        {
            var baseline=Session("--smoke-test");string owned=AssertSmokeOwnsStorage(baseline);
            if(!overlapping){Destroy(baseline);AssertRestored(owned);}
            string failedOwned=null;
            var type=typeof(FestivalSession).Assembly.GetType("Festival.Presentation.DevelopmentSmokeProfileScope");
            Assert.That(type,Is.Not.Null,"the same exception guard wraps the real session preference constructors");
            var load=type.GetMethod("Load",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            var thrown=Assert.Throws<TargetInvocationException>(()=>load.Invoke(null,new object[]{new[]{"--solo-smoke-test"},"smoke_failed",(Action)(()=>{failedOwned=LocalProfile.Folder;throw new InvalidOperationException("intentional preference construction failure");})}));
            Assert.That(thrown.InnerException,Is.TypeOf<InvalidOperationException>());
            if(overlapping)
            {
                Assert.That(LocalProfile.Folder,Is.EqualTo(owned));Assert.That(Directory.Exists(owned),Is.True);
                Acknowledge(baseline,"after-failure");AssertCallerUnchanged();Destroy(baseline);AssertRestored(owned);
            }
            else AssertRestored(failedOwned);
        }

        [Test]public void LaterCallerOverrideIsPreservedWhileOnlyOwnedStorageIsCleaned()
        {
            var session=Session("--smoke-test");string owned=AssertSmokeOwnsStorage(session);
            string replacement=Path.Combine(caller,"caller-later");Directory.CreateDirectory(replacement);
            LocalProfile.Folder=replacement;FestivalInput.PreferencesScope=replacement;Destroy(session);
            Assert.That(LocalProfile.Folder,Is.EqualTo(replacement));Assert.That(FestivalInput.PreferencesScope,Is.EqualTo(replacement));
            Assert.That(Directory.Exists(owned),Is.False);Assert.That(Directory.Exists(replacement),Is.True);AssertCallerUnchanged();
        }
    }
}
