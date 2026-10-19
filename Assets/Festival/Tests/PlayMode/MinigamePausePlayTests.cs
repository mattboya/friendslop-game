using System.Collections;
using System.Reflection;
using System.Linq;
using TMPro;
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
    public sealed class MinigamePausePlayTests : FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator ActualWorldRigsHoldThroughTurningBannerAndJoinedLifecycle()
        {
            UseAVirtualMouse("impl-U59 pause mouse");
            var world=new GameObject("impl-U59 actual pause world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl-U59 rig host").AddComponent<FestivalSession>();host.gameObject.AddComponent<FestivalHud>();
            var peer=new GameObject("impl-U59 rig peer").AddComponent<FestivalSession>();peer.gameObject.AddComponent<FestivalHud>();yield return null;
            ushort port=FreePort();host.Host("Host",port);yield return null;peer.Join("Peer","127.0.0.1",port);
            for(float end=Time.realtimeSinceStartup+30;peer.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
            Assert.That(peer.LocalPlayer,Is.Not.Null,peer.Message);var sim=Simulation(host);var joined=sim.Player(peer.LocalPlayerId);joined.X=0;joined.Z=19;peer.Command("Ready");
            for(float end=Time.realtimeSinceStartup+10;!joined.Ready&&Time.realtimeSinceStartup<end;)yield return null;
            Assert.That(joined.Ready,Is.True);yield return StartLevel(host,peer.LocalPlayerId);Simulation(host).State.Booth.Kind=FestivalSimulation.MysteryCupsGame;yield return null;
            for(float end=Time.realtimeSinceStartup+5;peer.State.Phase!="Playing"&&Time.realtimeSinceStartup<end;)yield return null;
            Assert.That(peer.State.Phase,Is.EqualTo("Playing"));yield return null;yield return null;
            var owner=sim.Player(host.LocalPlayerId);sim.State.Npcs.Clear();owner.Effects.Clear();joined.Effects.Clear();
            owner.Inventory.Add(new ItemStack{ItemId="poi_led",Count=1});joined.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
            host.Command("Equip",item:"poi_led");peer.Command("Equip",item:"poi_practice");host.MenuOpen=false;peer.MenuOpen=true;
            InputSystem.QueueStateEvent(TestMouse,new MouseState().WithButton(MouseButton.Left,true));yield return new WaitForSecondsRealtime(.5f);
            Assert.That(owner.VisualPoiSpin,Is.True,"setup: owner click is already held before entry");Assert.That(joined.VisualPoiSpin,Is.False,"setup: joined player has not requested a spin");
            var crowd=world.GetComponentInChildren<FestivalAmbientCrowd>();var actualView=FestivalCharacter.ViewTransform;
            var eye=new GameObject("impl-U59 rig culling eye").transform;eye.position=new Vector3(0,1.6f,-12);FestivalCharacter.ViewTransform=eye;
            try
            {
                eye.rotation=Quaternion.Euler(0,180,0);yield return new WaitForSecondsRealtime(.35f);Assert.That(StageShown(crowd),Is.Zero);
                var at=FestivalSimulation.BoothAt(sim.State);foreach(var p in sim.State.Players){p.X=at.X+1;p.Z=at.Z;}
                for(float end=Time.realtimeSinceStartup+6;peer.State.Phase!=FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(peer.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));yield return null;yield return null;Assert.That(Time.timeScale,Is.Zero);
                Assert.That(FestivalSession.WorldAnimationPaused,Is.True,"actual hosted phase freezes world rigs");
                var hostPose=new FrozenRigPose(host.WorldCharacter(owner.Id));var joinedPose=new FrozenRigPose(peer.WorldCharacter(joined.Id));var walker=crowd.FirstWalkerPosition;
                var game=FestivalCharacter.Create(world.transform,"impl-U59 explicit game rig",Color.white);game.Pose="Poi";game.SetEquippedItem("poi_led");
                var wrist=new Bounds(game.transform.position,Vector3.zero);
                for(int frame=0;frame<60;frame++)
                {
                    game.SetAnimationClock(10+frame/60f,1/60f);yield return null;
                    var hand=game.GetComponentsInChildren<Transform>().First(t=>t.name=="HandR");if(frame==0)wrist=new Bounds(hand.position,Vector3.zero);else wrist.Encapsulate(hand.position);
                }
                Assert.That(wrist.size.magnitude,Is.GreaterThan(.05f),"explicit game rig animates inside the actual paused session");game.ClearAnimationClock();hostPose.AssertHeld();joinedPose.AssertHeld();
                eye.rotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.35f);
                Assert.That(StageShown(crowd),Is.GreaterThan(0),"real enabled characters safely wake during camera turn");
                foreach(var actor in crowd.GetComponentsInChildren<FestivalCharacter>(true))AssertFinite(actor);
                InputSystem.QueueStateEvent(TestMouse,new MouseState());yield return new WaitForSecondsRealtime(.25f);
                Assert.That(owner.VisualPoiSpin,Is.True,"host preserves preheld authoritative intent after release");
                Assert.That(host.WorldCharacter(owner.Id).SpinningPoi,Is.True,"local body holds authoritative preheld state during pause");
                peer.MenuOpen=false;InputSystem.QueueStateEvent(TestMouse,new MouseState().WithButton(MouseButton.Left,true));yield return new WaitForSecondsRealtime(.25f);
                Assert.That(joined.VisualPoiSpin,Is.False,"fresh joined intent is blocked by host");
                Assert.That(peer.WorldCharacter(joined.Id).SpinningPoi,Is.False,"fresh local click cannot bypass the host freeze");
                hostPose.AssertHeld();joinedPose.AssertHeld();Assert.That(crowd.FirstWalkerPosition,Is.EqualTo(walker));
                peer.enabled=false;yield return null;yield return null;joinedPose.AssertHeld();peer.enabled=true;yield return new WaitForSecondsRealtime(.2f);joinedPose.AssertHeld();
                string rejoinId=joined.Id;peer.Leave();
                Assert.That(FestivalSession.WorldAnimationPaused,Is.True,"a peer leaving cannot thaw the host's paused world");
                for(float end=Time.realtimeSinceStartup+5;joined.Connected&&Time.realtimeSinceStartup<end;)yield return null;
                peer.Join("Peer","127.0.0.1",port);for(float end=Time.realtimeSinceStartup+30;peer.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(peer.LocalPlayerId,Is.EqualTo(rejoinId));yield return null;yield return null;
                var rejoined=new FrozenRigPose(peer.WorldCharacter(rejoinId));yield return new WaitForSecondsRealtime(.25f);rejoined.AssertHeld();AssertFinite(peer.WorldCharacter(rejoinId));
                var turn=FestivalSimulation.CupsTurn(sim.State)==host.LocalPlayerId?host:peer;
                turn.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups);
                for(float end=Time.realtimeSinceStartup+3;sim.State.Booth.ScoredAt<0&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(sim.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0));
                eye.rotation=Quaternion.Euler(0,180,0);yield return new WaitForSecondsRealtime(.35f);Assert.That(StageShown(crowd),Is.Zero);
                eye.rotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.35f);Assert.That(StageShown(crowd),Is.GreaterThan(0));hostPose.AssertHeld();rejoined.AssertHeld();
                InputSystem.QueueStateEvent(TestMouse,new MouseState());
                for(float end=Time.realtimeSinceStartup+6;peer.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(peer.State.Phase,Is.EqualTo("Playing"));yield return new WaitForSecondsRealtime(.3f);Assert.That(Time.timeScale,Is.EqualTo(1));
                Assert.That(hostPose.Changed,Is.True,"owner animation resumes");Assert.That(rejoined.Changed,Is.True,"rejoined rig resumes");AssertFinite(host.WorldCharacter(owner.Id));AssertFinite(peer.WorldCharacter(rejoinId));
            }
            finally{FestivalCharacter.ViewTransform=actualView;Object.Destroy(eye.gameObject);peer.Leave();host.Leave();Object.Destroy(peer.gameObject);Object.Destroy(host.gameObject);Object.Destroy(world.gameObject);}
            yield return null;Assert.That(Time.timeScale,Is.EqualTo(1));
            Assert.That(FestivalSession.WorldAnimationPaused,Is.False,"retired sessions leave no world-rig freeze behind");
        }
        sealed class FrozenRigPose
        {
            readonly Transform[] bones;readonly Vector3[] positions;readonly Quaternion[] rotations;readonly LineRenderer[] tethers;readonly Vector3[] starts,ends;
            public FrozenRigPose(FestivalCharacter actor)
            {
                Assert.That(actor,Is.Not.Null);AssertFinite(actor);
                bones=FestivalMotionLibrary.Bones.Select(name=>actor.GetComponentsInChildren<Transform>(true).First(t=>t.name==name)).ToArray();
                positions=bones.Select(b=>b.localPosition).ToArray();rotations=bones.Select(b=>b.localRotation).ToArray();
                tethers=actor.GetComponentsInChildren<FestivalPoiRig>(true).Select(r=>r.GetComponent<LineRenderer>()).ToArray();Assert.That(tethers.Length,Is.EqualTo(2));
                starts=tethers.Select(t=>t.GetPosition(0)).ToArray();ends=tethers.Select(t=>t.GetPosition(1)).ToArray();
            }
            public bool Changed=>!bones.Select(b=>b.localRotation).SequenceEqual(rotations);
            public void AssertHeld()
            {
                for(int i=0;i<bones.Length;i++){Assert.That(bones[i].localPosition,Is.EqualTo(positions[i]),"frozen IK "+bones[i].name);Assert.That(bones[i].localRotation,Is.EqualTo(rotations[i]),"frozen bone "+bones[i].name);}
                for(int i=0;i<tethers.Length;i++){Assert.That(tethers[i].GetPosition(0),Is.EqualTo(starts[i]),"frozen grip");Assert.That(tethers[i].GetPosition(1),Is.EqualTo(ends[i]),"frozen head");}
            }
        }
        static void AssertFinite(FestivalCharacter actor)
        {
            foreach(var bone in actor.GetComponentsInChildren<Transform>(true))
            {
                var p=bone.localPosition;var q=bone.localRotation;
                Assert.That(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z),Is.False,"finite position "+bone.name);
                Assert.That(float.IsNaN(q.x)||float.IsNaN(q.y)||float.IsNaN(q.z)||float.IsNaN(q.w)||float.IsInfinity(q.x)||float.IsInfinity(q.y)||float.IsInfinity(q.z)||float.IsInfinity(q.w),Is.False,"finite rotation "+bone.name);
            }
        }
        [UnityTest]public IEnumerator DisabledAndLeftPausedSessionsReleaseActualWorldRigAnimation()
        {
            var world=new GameObject("impl-U59 registration world").AddComponent<FestivalWorld>();yield return null;
            var session=new GameObject("impl-U59 registration host").AddComponent<FestivalSession>();session.gameObject.AddComponent<FestivalHud>();yield return null;
            session.Host("Tester",HostPort);yield return null;yield return StartLevel(session);Simulation(session).State.Booth.Kind=FestivalSimulation.MysteryCupsGame;yield return null;
            var actor=FestivalCharacter.Create(world.transform,"impl-U59 surviving world rig",Color.white);actor.Pose="Poi";actor.SetEquippedItem("poi_led");
            var sim=Simulation(session);var at=FestivalSimulation.BoothAt(sim.State);var owner=sim.Player(session.LocalPlayerId);
            try
            {
                yield return new WaitForSecondsRealtime(.2f);owner.X=at.X+1;owner.Z=at.Z;
                for(float end=Time.realtimeSinceStartup+6;session.State.Phase=="Playing"&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));yield return null;yield return null;
                var held=new FrozenRigPose(actor);yield return new WaitForSecondsRealtime(.2f);held.AssertHeld();
                session.gameObject.SetActive(false);yield return new WaitForSecondsRealtime(.25f);
                Assert.That(held.Changed,Is.True,"a disabled sole session no longer holds actual world bones frozen");
                Assert.That(FestivalSession.WorldAnimationPaused,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));AssertFinite(actor);
                session.gameObject.SetActive(true);yield return null;yield return null;
                Assert.That(FestivalSession.WorldAnimationPaused,Is.True,"re-enabled active game restores the world pause");
                held=new FrozenRigPose(actor);yield return new WaitForSecondsRealtime(.2f);held.AssertHeld();
                session.Leave();yield return new WaitForSecondsRealtime(.25f);
                Assert.That(held.Changed,Is.True,"actual Leave releases the phase freeze even while the session component remains enabled");
                Assert.That(FestivalSession.WorldAnimationPaused,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));AssertFinite(actor);
            }
            finally{session.Leave();Object.Destroy(session.gameObject);Object.Destroy(world.gameObject);}
            yield return null;Assert.That(FestivalSession.WorldAnimationPaused,Is.False);
        }
        [UnityTest]public IEnumerator TurningDuringGameAndBannerRecullsCrowdWithoutMovingWalkers()
        {
            var world=new GameObject("impl-U58 pause world").AddComponent<FestivalWorld>();yield return null;
            var session=new GameObject("impl-U58 pause session").AddComponent<FestivalSession>();session.gameObject.AddComponent<FestivalHud>();yield return null;
            session.Host("Tester",HostPort);yield return null;yield return StartLevel(session);Simulation(session).State.Booth.Kind=FestivalSimulation.MysteryCupsGame;yield return null;
            // This regression owns visibility. The separate rig-pause slice covers bone/IK evolution on newly woken actors.
            foreach(var actor in world.GetComponentsInChildren<FestivalCharacter>(true))actor.enabled=false;
            var sim=Simulation(session);var crowd=world.GetComponentInChildren<FestivalAmbientCrowd>();Assert.That(crowd,Is.Not.Null);
            var actualView=FestivalCharacter.ViewTransform;var eye=new GameObject("impl-U58 culling eye").transform;eye.position=new Vector3(0,1.6f,-12);FestivalCharacter.ViewTransform=eye;
            try
            {
                eye.rotation=Quaternion.Euler(0,180,0);yield return new WaitForSecondsRealtime(.35f);
                Assert.That(StageShown(crowd),Is.Zero,"setup: stage behind camera is culled before game entry");
                var at=FestivalSimulation.BoothAt(sim.State);var player=sim.Player(session.LocalPlayerId);player.X=at.X+1;player.Z=at.Z;
                for(float end=Time.realtimeSinceStartup+6;session.State.Phase=="Playing"&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));yield return null;yield return null;
                var walker=crowd.FirstWalkerPosition;
                eye.rotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.35f);
                Assert.That(StageShown(crowd),Is.GreaterThan(0),"turning toward stage during game wakes its attendees");
                Assert.That(crowd.FirstWalkerPosition,Is.EqualTo(walker),"walker motion remains frozen on scaled clock");
                session.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups);yield return null;
                Assert.That(session.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0));
                eye.rotation=Quaternion.Euler(0,180,0);yield return new WaitForSecondsRealtime(.35f);Assert.That(StageShown(crowd),Is.Zero);
                eye.rotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.35f);Assert.That(StageShown(crowd),Is.GreaterThan(0),"banner still reculls");
                Assert.That(crowd.FirstWalkerPosition,Is.EqualTo(walker));
                for(float end=Time.realtimeSinceStartup+5;session.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"));yield return new WaitForSecondsRealtime(.3f);Assert.That(crowd.FirstWalkerPosition,Is.Not.EqualTo(walker),"walkers resume");
            }
            finally{FestivalCharacter.ViewTransform=actualView;Object.Destroy(eye.gameObject);session.Leave();Object.Destroy(session.gameObject);Object.Destroy(world.gameObject);}
            yield return null;Assert.That(Time.timeScale,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator JoinedWorldClockHoldsCloudAndSpeechThenRetiresAcrossDisableLeaveAndRejoin()
        {
            var world=new GameObject("impl-U58 joined world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl-U58 clock host").AddComponent<FestivalSession>();
            var peer=new GameObject("impl-U58 clock peer").AddComponent<FestivalSession>();peer.gameObject.AddComponent<FestivalHud>();yield return null;
            ushort port=FreePort();host.Host("Host",port);yield return null;peer.Join("Peer","127.0.0.1",port);
            for(float end=Time.realtimeSinceStartup+30;peer.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
            Assert.That(peer.LocalPlayer,Is.Not.Null,peer.Message);var sim=Simulation(host);var owner=sim.Player(peer.LocalPlayerId);owner.X=0;owner.Z=19;peer.Command("Ready");
            for(float end=Time.realtimeSinceStartup+10;!owner.Ready&&Time.realtimeSinceStartup<end;)yield return null;
            Assert.That(owner.Ready,Is.True);yield return StartLevel(host,peer.LocalPlayerId);Simulation(host).State.Booth.Kind=FestivalSimulation.MysteryCupsGame;yield return null;
            foreach(var actor in world.GetComponentsInChildren<FestivalCharacter>(true))actor.enabled=false;
            sim.State.ElapsedSeconds=193;sim.State.SimulationSeconds=193;sim.State.TripperId=owner.Id;sim.State.CloudClueStart=150;sim.State.CloudClueReadAt=190;sim.State.CloudClueLandmark=2;
            var at=FestivalSimulation.BoothAt(sim.State);foreach(var player in sim.State.Players){player.X=at.X+1;player.Z=at.Z;}
            for(float end=Time.realtimeSinceStartup+6;peer.State.Phase!=FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<end;)yield return null;
            Assert.That(peer.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));yield return null;
            // Isolate transported world-clock consumers from the independently reproduced first-pose rig defect.
            // The earlier test exercises actual timeScale=0; authoritative Minigame world time stays frozen here too.
            host.GetComponent<FestivalMinigameView>().enabled=false;peer.GetComponent<FestivalMinigameView>().enabled=false;
            var cop=new NpcState{Id="impl-U58-clock-cop",Kind="Cop",X=owner.X+2,Z=owner.Z};sim.State.Npcs.Add(cop);
            typeof(FestivalSimulation).GetMethod("Bark",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{cop,owner,"Hey!"});
            string id=owner.SpeechDeliveryId;
            for(float end=Time.realtimeSinceStartup+3;peer.SpeechDeliveries.Find(id)==null&&Time.realtimeSinceStartup<end;)yield return null;
            var delivery=peer.SpeechDeliveries.Find(id);Assert.That(delivery,Is.Not.Null,"real joined recipient sees stable accepted ID");
            double age=delivery.AgeSeconds;world.SetLighting(peer.State,peer.LocalPlayerId);
            var clue=world.transform.Find(FestivalWorld.RootName+"/"+FestivalClouds.RootName+"/"+FestivalClouds.ClueName);Assert.That(clue,Is.Not.Null);
            var vertices=clue.GetComponent<MeshFilter>().sharedMesh.vertices;var block=new MaterialPropertyBlock();clue.GetComponent<Renderer>().GetPropertyBlock(block);float alpha=block.GetColor("_Color").a;
            sim.State.Booth.TurnAt=sim.State.Booth.Seconds+30;sim.Tick(30);yield return new WaitForSecondsRealtime(.3f);world.SetLighting(peer.State,peer.LocalPlayerId);
            Assert.That(peer.State.Booth.Seconds,Is.GreaterThan(29),"game clock advances");Assert.That(delivery.AgeSeconds,Is.EqualTo(age).Within(.02),"speech age stays on frozen world clock");
            Assert.That(clue.gameObject.activeInHierarchy,Is.True);Assert.That(clue.GetComponent<MeshFilter>().sharedMesh.vertices,Is.EqualTo(vertices));clue.GetComponent<Renderer>().GetPropertyBlock(block);Assert.That(block.GetColor("_Color").a,Is.EqualTo(alpha).Within(.001f));
            typeof(FestivalSimulation).GetMethod("ScoreMinigame",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{60});yield return new WaitForSecondsRealtime(.3f);Assert.That(peer.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0));world.SetLighting(peer.State,peer.LocalPlayerId);
            Assert.That(clue.GetComponent<MeshFilter>().sharedMesh.vertices,Is.EqualTo(vertices),"banner holds picture after time payout");
            typeof(FestivalSimulation).GetMethod("Hush",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sim,new object[]{owner});
            for(float end=Time.realtimeSinceStartup+3;!delivery.Retracted&&Time.realtimeSinceStartup<end;)yield return null;
            Assert.That(delivery.Retracted,Is.True,"same accepted ID carries Hush cancellation to joined client");yield return null;yield return null;
            Assert.That(SpeechTexts(peer).Any(t=>t.text=="Hey!"),Is.False,"Hush removes actual latest-line bubble");
            peer.enabled=false;yield return null;Assert.That(peer.SpeechDeliveries.Deliveries,Is.Empty,"disable retires active delivery references");peer.enabled=true;
            yield return new WaitForSecondsRealtime(.3f);Assert.That(peer.SpeechDeliveries.Find(id),Is.Null,"same-round re-enable cannot replay retired delivery");
            string playerId=owner.Id;peer.Leave();for(float end=Time.realtimeSinceStartup+5;owner.Connected&&Time.realtimeSinceStartup<end;)yield return null;
            peer.Join("Peer","127.0.0.1",port);for(float end=Time.realtimeSinceStartup+30;peer.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
            Assert.That(peer.LocalPlayerId,Is.EqualTo(playerId));yield return new WaitForSecondsRealtime(.2f);Assert.That(peer.SpeechDeliveries.Find(id),Is.Null,"rejoin never resurrects cancelled speech");
            sim.Tick(FestivalSimulation.MinigameBannerSeconds+.2);yield return new WaitForSecondsRealtime(.3f);Assert.That(peer.State.Phase,Is.EqualTo("Playing"));
            peer.Leave();host.Leave();Object.Destroy(peer.gameObject);Object.Destroy(host.gameObject);Object.Destroy(world.gameObject);yield return null;Assert.That(Time.timeScale,Is.EqualTo(1));
        }

        static int StageShown(FestivalAmbientCrowd crowd)
        {
            int shown=0;foreach(var actor in crowd.GetComponentsInChildren<FestivalCharacter>(true))
                if(actor.name.Contains("Stage")&&actor.gameObject.activeSelf)shown++;
            return shown;
        }
    }
}
