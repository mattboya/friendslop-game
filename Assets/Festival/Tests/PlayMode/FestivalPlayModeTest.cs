using System.Collections;
using System.Net;
using System.Net.Sockets;
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
        // the shared checkout can host at the same time. No PlayMode test joins a client, so none needs to know the port.
        protected const ushort HostPort=0;

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
