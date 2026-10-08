using System.Collections;
using System.Reflection;
using System.Linq;
using TMPro;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class MinigamePausePlayTests : FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TurningDuringGameAndBannerRecullsCrowdWithoutMovingWalkers()
        {
            var world=new GameObject("impl-U58 pause world").AddComponent<FestivalWorld>();yield return null;
            var session=new GameObject("impl-U58 pause session").AddComponent<FestivalSession>();session.gameObject.AddComponent<FestivalHud>();yield return null;
            session.Host("Tester",HostPort);yield return null;yield return StartLevel(session);yield return null;
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
            Assert.That(owner.Ready,Is.True);yield return StartLevel(host,peer.LocalPlayerId);yield return null;
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
            Assert.That(peer.GetComponentsInChildren<TextMeshPro>(false).Any(t=>t.text=="Hey!"),Is.False,"Hush removes actual latest-line bubble");
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
