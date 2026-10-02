using System.Collections;
using System.Linq;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    // NET-3: a friend leaving a game logs no network error. Leave used to destroy the network manager in the frame it asked NGO to
    // shut down, so the transport went first and the manager's own shutdown then sent the friend's disconnect through a disposed
    // driver, logging an ObjectDisposedException. The test runner fails a UnityTest on any logged error or exception.
    public sealed class LeaveSessionTests:FestivalPlayModeTest
    {
        // The GameObjects FestivalSession.Configure builds to hold each session's NetworkManager and transport.
        static int NetworkSessions()=>Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>t.name=="Festival native session");

        static IEnumerator HostAndJoin(FestivalSession host,FestivalSession friend)
        {
            ushort port=FreePort();
            host.Host("Tester",port);
            float deadline=Time.realtimeSinceStartup+30;
            while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.LocalPlayer,Is.Not.Null,"setup: host has a local player: "+host.Message);
            friend.Join("Friend","127.0.0.1",port);
            while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(friend.LocalPlayer,Is.Not.Null,"setup: the friend joins the host on port "+port+": "+friend.Message);
        }

        // A friend who calls Leave (the menu's LEAVE SESSION) logs nothing. The host sees them go, their network manager goes once
        // its shutdown has finished, and the session they left can host straight away, before that.
        [UnityTest]public IEnumerator AFriendWhoLeavesLogsNoNetworkError()
        {
            var world=new GameObject("Leave world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Leave host");var host=hostObject.AddComponent<FestivalSession>();
            var friendObject=new GameObject("Leave friend");var friend=friendObject.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                yield return HostAndJoin(host,friend);
                var sim=Simulation(host);string friendId=friend.LocalPlayerId;
                friend.Leave();
                friend.Host("Friend",FreePort());
                Assert.That(friend.IsHost,Is.True,"the session the friend left hosts again in the same frame: "+friend.Message);
                for(float deadline=Time.realtimeSinceStartup+10;(sim.Player(friendId).Connected||NetworkSessions()>2)&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(sim.Player(friendId).Connected,Is.False,"the host saw the friend go");
                Assert.That(NetworkSessions(),Is.EqualTo(2),"the friend's old network manager is gone; the host's and the friend's new one stay");
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }

        // The PlayMode fixtures' own pattern: Leave, then destroy the session at once. The network manager's teardown does not
        // hang on the session, so it still finishes after the session is gone, and nothing is logged.
        [UnityTest]public IEnumerator AFriendWhoLeavesAndIsDestroyedAtOnceStillShutsDownCleanly()
        {
            var world=new GameObject("Leave world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Leave host");var host=hostObject.AddComponent<FestivalSession>();
            var friendObject=new GameObject("Leave friend");var friend=friendObject.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                yield return HostAndJoin(host,friend);
                var sim=Simulation(host);string friendId=friend.LocalPlayerId;
                friend.Leave();Object.Destroy(friendObject);
                for(float deadline=Time.realtimeSinceStartup+10;(sim.Player(friendId).Connected||NetworkSessions()>1)&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(friend==null,Is.True,"setup: the friend's session is gone");
                Assert.That(sim.Player(friendId).Connected,Is.False,"the host saw the friend go");
                Assert.That(NetworkSessions(),Is.EqualTo(1),"the friend's network manager is gone too; the host's stays");
            }
            finally{host.Leave();if(friendObject!=null)Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }
    }
}
