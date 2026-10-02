using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    // TEST-2: the level start every hosted PlayMode test uses (FestivalPlayModeTest.StartLevel) skips the launch countdown and
    // the spin, yet the host still walks every step of a real start, and a friend joined over the network sees each one.
    public sealed class LevelStartTests:FestivalPlayModeTest
    {
        static readonly string[] EveryStep={"Shopping","Spinning","Loading","Playing"};

        // Each phase a session's view shows, in order, and the real time it first showed.
        static IEnumerator Record(FestivalSession session,List<string> phases,List<float> at)
        {
            while(true)
            {
                string phase=session.State?.Phase;
                if(phase!=null&&(phases.Count==0||phases[phases.Count-1]!=phase)){phases.Add(phase);at.Add(Time.realtimeSinceStartup);}
                yield return null;
            }
        }

        // A port nobody holds right now: the OS picks one for the probe, which lets it go for the host.
        static ushort FreePort(){using(var probe=new UdpClient(new IPEndPoint(IPAddress.Any,0)))return (ushort)((IPEndPoint)probe.Client.LocalEndPoint).Port;}

        // The host's view goes through the spin and loading in order, well inside the countdown's own 5 s. The spin deals the
        // tripper their dose, and the friend without a client of their own is reported loaded.
        [UnityTest]public IEnumerator TheHelperWalksEveryStepOfALevelStartWithoutTheWaits()
        {
            var world=new GameObject("Level start world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Level start session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                // Sam holds the festival in Loading until the helper reports their map loaded.
                var mate=sim.AddPlayer("start_mate","Sam");mate.Ready=true;
                var phases=new List<string>();var at=new List<float>();session.StartCoroutine(Record(session,phases,at));
                float start=Time.realtimeSinceStartup;
                yield return StartLevel(session);
                Assert.That(phases,Is.EqualTo(EveryStep),"the host's view passes through the spin and loading, in order");
                Assert.That(at[2]-start,Is.LessThan(5f),"the 5 s countdown and the "+FestivalSimulation.SpinSeconds+" s spin are skipped");
                var dose=sim.State.Doses.Find(d=>d.PlayerId==sim.State.TripperId);
                Assert.That(dose,Is.Not.Null,"the spin picked a tripper and dealt their dose");
                Assert.That(sim.Player(dose.PlayerId).Effects.Exists(e=>e.Id==FestivalSimulation.DoseEffect&&e.Intensity==dose.Dose),Is.True,"the tripper took it");
                Assert.That(mate.MapReady&&sim.Player(session.LocalPlayerId).MapReady,Is.True,"both maps were reported loaded");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }

        // A friend joined over the network sees the same steps: the skipped spin still runs SpinHoldSeconds on the host's clock,
        // so at least two of its 10 Hz snapshots carry it, and the friend's own client reports its map loaded.
        [UnityTest]public IEnumerator AJoinedFriendSeesEveryStepToo()
        {
            var world=new GameObject("Level start world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Level start host");var host=hostObject.AddComponent<FestivalSession>();
            var friendObject=new GameObject("Level start friend");var friend=friendObject.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                ushort port=FreePort();
                host.Host("Tester",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,"host has a local player: "+host.Message);
                friend.Join("Friend","127.0.0.1",port);
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,"the friend joins the host on port "+port+": "+friend.Message);
                var sim=Simulation(host);string friendId=friend.LocalPlayerId;
                var phases=new List<string>();var at=new List<float>();friend.StartCoroutine(Record(friend,phases,at));
                foreach(var p in sim.State.Players){p.X=0;p.Z=19;}
                host.Command("Ready");friend.Command("Ready");
                yield return SkipCountdown(host);yield return SkipSpin(host);yield return FinishLoading(host,friendId);
                for(deadline=Time.realtimeSinceStartup+5;friend.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(phases,Is.EqualTo(EveryStep),"the friend's view passes through the spin and loading, in order");
                Assert.That(friend.State.TripperId,Is.EqualTo(sim.State.TripperId),"and shows who the spin picked");
                Assert.That(sim.Player(friendId).MapReady,Is.True,"the friend's own client reported its map loaded");
                // FestivalSession.Leave asks NGO to shut down, which it does on its next update, and destroys the transport at once,
                // so a client's late disconnect reaches a disposed driver and logs an exception. Let the friend's shutdown finish first.
                var manager=typeof(FestivalSession).GetField("manager",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(friend);
                manager.GetType().GetMethod("Shutdown").Invoke(manager,new object[]{false});
                var listening=manager.GetType().GetProperty("IsListening");
                for(deadline=Time.realtimeSinceStartup+10;(bool)listening.GetValue(manager)&&Time.realtimeSinceStartup<deadline;)yield return null;
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }
    }
}
