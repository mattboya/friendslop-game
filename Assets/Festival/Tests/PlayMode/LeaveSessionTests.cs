using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        // MENU-2 (and TRIP-8's item 3) on a real hosted Day 1: leaving a game while lying on the grass, turned away and just refused
        // by the host, the title's PLAY card shows the opening line, not that reply, and the next game you host looks straight
        // ahead, as the first did, not up at the sky or off where you last turned.
        [UnityTest]public IEnumerator LeavingWhileLyingDownTheNextGameStartsFresh()
        {
            var world=new GameObject("Leave world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Leave HUD");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            var pitchField=typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance);
            var yawField=typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance);
            string Status(){foreach(var text in hud.GetComponentsInChildren<Text>(true))if(text.name=="Status")return text.text;return "(none)";}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"setup: host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("leave_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                // Sam trips at the way back to camp, so you are sober and no dose turns or trails your view (TRIP-5).
                sim.State.TripperId=mate.Id;mate.X=Festivals.CampGateX;mate.Z=Festivals.CampGateZ;player.Effects.Clear();
                mate.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);mate.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                sim.State.Npcs.Clear();
                yawField.SetValue(session,90f);pitchField.SetValue(session,7f);
                session.Command(FestivalSimulation.LieDownKind);
                yield return new WaitForSeconds(.4f);
                var eye=session.ViewCamera.transform;
                Assert.That(FestivalSimulation.LyingDown(sim.State,player.Id)&&eye.forward.y>.9f,Is.True,"setup: lying down, you look up at the sky ("+session.Message+")");
                // Something the host refuses lying there, as anything in game would leave a reply behind.
                session.Command("Pickup","nothing");
                Assert.That(session.Message,Is.Not.Empty,"setup: the host answers the pickup");
                string reply=session.Message;

                session.Leave();yield return null;
                const string Opening="Gather your friends. Make it back to camp.";
                if(session.Message!=Opening||Status()!=Opening)failures.Add("after LEAVE SESSION: the PLAY card reads \""+Status()+"\" (message \""+session.Message+"\"), not \""+Opening+"\" (the last reply was \""+reply+"\")");
                session.Host("Tester",HostPort);
                for(deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"the next game hosts: "+session.Message);
                yield return new WaitForSeconds(.3f);
                if(eye.forward.z<.95f)failures.Add("the next game: the view looks along "+eye.forward+", not straight ahead");
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;
        }
    }
}
