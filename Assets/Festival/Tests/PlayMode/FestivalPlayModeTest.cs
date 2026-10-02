using System.Collections;
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
    /// <summary>Base for every PlayMode fixture that builds a festival world or hosts a session.</summary>
    public abstract class FestivalPlayModeTest
    {
        // The port every fixture hosts its session on. 0 lets the OS pick a free one, so a PlayMode run in a snapshot and one in
        // the shared checkout can host at the same time. LevelStartTests, the one fixture that joins a client, probes a free port
        // of its own, as a client has to know the port it joins.
        protected const ushort HostPort=0;

        // TEST-2: how long a skipped spin still runs on the host's clock: at least two of its 10 Hz snapshots (FestivalSession.Update)
        // go out meanwhile, so a joined client sees the spin.
        protected const double SpinHoldSeconds=.25;

        protected static FestivalSimulation Simulation(FestivalSession session)=>(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);

        // TEST-2: a hosted level starts in about a second, not 22. The host still arms the launch countdown and runs StartRound
        // (the spin, roles and twists), the Spinning -> Loading -> Playing steps and every MapReady through its real Step code;
        // only the waits go. SpinnerTakeTests keeps the real countdown and spin.
        /// <summary>The host walks to the trailhead gate and readies up, and the level starts: the countdown, spin and loading
        /// each skipped. Every other player must be ready already.</summary>
        protected static IEnumerator StartLevel(FestivalSession session)
        {
            var player=Simulation(session).Player(session.LocalPlayerId);player.X=0;player.Z=19;session.Command("Ready");
            yield return SkipCountdown(session);yield return SkipSpin(session);yield return FinishLoading(session);
        }

        /// <summary>Once the whole crew has sent Ready, the round starts on the host's next step. The host's own Step arms the
        /// 5 s countdown; this pulls it down to now every frame, as Step re-arms it after any unready moment and every Ready
        /// resets it.</summary>
        protected static IEnumerator SkipCountdown(FestivalSession session)
        {
            var sim=Simulation(session);
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
            foreach(var session in Object.FindObjectsByType<FestivalSession>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.Destroy(session.gameObject);
            foreach(var world in Object.FindObjectsByType<FestivalWorld>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.Destroy(world.gameObject);
            yield return null;
        }
    }

    // HUD-2 (gate hazard): a PlayMode run in a snapshot and one in the shared checkout can run at the same time on one machine.
    public sealed class PlayModeIsolationTests:FestivalPlayModeTest
    {
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
