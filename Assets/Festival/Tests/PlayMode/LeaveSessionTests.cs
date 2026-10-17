using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

        // Leaving puts the camp's own daylight, colour grade and fog back before Leave returns, whatever the game showed last: a
        // Night 2 of Rolly Pollies, an Ember Playa dust storm, a minigame's freeze-frame or the camp's gas. So do failing, closing
        // the game and a joined friend leaving; the saved brightness stays. No HUD runs here to put the camp back a frame later. A
        // session that never connected leaves a preview's light alone.
        [UnityTest]public IEnumerator LeavingRestoresTheCampLightBeforeLeaveReturns()
        {
            var worldObject=new GameObject("impl-U46 leave world");var world=worldObject.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("impl-U46 leave host");var host=hostObject.AddComponent<FestivalSession>();
            GameObject friendObject=null,previewObject=null;
            yield return null;
            var failures=new List<string>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool diagnostics=DevelopmentDiagnostics.Visible;DevelopmentDiagnostics.Visible=false;int resets=0;string reset="";
            Application.LogCallback trace=(text,stack,type)=>{if(text.StartsWith("[Festival.WorldLifecycle] action=leave_reset ")){resets++;reset=text;}};
            Application.logMessageReceived+=trace;
#endif
            try
            {
                Assert.That(worldObject.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"setup: the world's colour grade");
                string Light()=>$"saturation {grade.saturation.value:F3} exposure {grade.postExposure.value:F3} fog {RenderSettings.fogStartDistance:F2}-{RenderSettings.fogEndDistance:F2} {RenderSettings.fogColor} ambient {RenderSettings.ambientSkyColor} sun {(RenderSettings.sun!=null?RenderSettings.sun.name:"none")}";
                host.Profile.Data.Brightness=.3f;yield return null;
                string camp=Light();
                RoundState Shown(string phase,int festival,int level,string substance)
                {
                    var state=new RoundState{Phase=phase,FestivalIndex=festival,LevelIndex=level,SpinSeed=12345,DurationSeconds=Festivals.NightSeconds};
                    var me=new PlayerState{Id="me"};state.Players.Add(me);
                    if(substance!="")me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance=substance});
                    return state;
                }
                var storm=Shown("Playing",Festivals.PlayaFestival,1,"");
                for(int second=0;second<Festivals.NightSeconds&&!FestivalSimulation.DustStorm(storm);second++)storm.ElapsedSeconds=second;
                var gas=Shown("Shopping",0,0,"");gas.SimulationSeconds=100;gas.Players[0].Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=95,RemainingSeconds=20});
                var shows=new (string name,RoundState state)[]{("Night 2 on Rolly Pollies",Shown("Playing",0,3,"ecstasy")),("an Ember Playa dust storm",storm),
                    ("a minigame's freeze-frame",Shown(FestivalSimulation.MinigamePhase,0,1,"lsd")),("the camp's gas",gas)};
                var fail=typeof(FestivalSession).GetMethod("Fail",BindingFlags.NonPublic|BindingFlags.Instance);
                IEnumerator Hosted(FestivalSession session)
                {
                    session.Host("Tester",HostPort);
                    for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                    Assert.That(session.LocalPlayer,Is.Not.Null,"setup: hosts: "+session.Message);yield return null;
                }
                bool Show(string where,RoundState state){world.SetPhase(state.Phase);world.SetLighting(state,"me");if(Light()!=camp)return true;failures.Add(where+": setup: the game's light looks like camp's");return false;}
                void Restored(string where){if(Light()!=camp)failures.Add(where+": the world shows "+Light()+", not camp's "+camp);}

                foreach(var (name,state) in shows)
                {
                    yield return Hosted(host);
                    if(!Show(name,state))continue;
                    host.Leave();Restored(name+", as Leave returns");
                    yield return null;Restored(name+", a frame later");
                }
                yield return Hosted(host);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(resets!=0)failures.Add("the leave trace logs "+resets+" times before it is opted in");
                DevelopmentDiagnostics.Visible=true;
#endif
                if(Show("failing",shows[0].state))
                {
                    fail.Invoke(host,new object[]{"impl-U46 connection lost"});
                    Restored("as a failure leaves");
                    if(host.Message!="impl-U46 connection lost")failures.Add("the failure's reason is replaced by \""+host.Message+"\"");
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(resets!=1||!reset.Contains("from_phase=Shopping"))failures.Add("opted in, one failure traces one leave reset from camp, not "+resets+": "+reset);
                DevelopmentDiagnostics.Visible=false;
#endif
                yield return null;

                // A joined friend leaving the host's game, at night: the world both sessions draw goes back to camp at once.
                ushort port=FreePort();host.Host("Tester",port);
                for(float deadline=Time.realtimeSinceStartup+30;host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                friendObject=new GameObject("impl-U46 leave friend");var friend=friendObject.AddComponent<FestivalSession>();friend.Profile.Data.Brightness=.3f;yield return null;
                friend.Join("Friend","127.0.0.1",port);
                for(float deadline=Time.realtimeSinceStartup+30;friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,"setup: the friend joins: "+friend.Message);yield return null;
                if(Show("a joined friend",shows[0].state)){friend.Leave();Restored("as a joined friend's Leave returns");}
                host.Leave();yield return null;

                // Closing the game while it shows a dust storm: the session's own teardown puts camp back.
                yield return Hosted(host);
                if(Show("closing the game",storm)){Object.Destroy(hostObject);yield return null;Restored("once the closed game's session is gone");}

                // A session that never connected, beside a preview the world shows for itself, leaves that preview as it is.
                previewObject=new GameObject("impl-U46 preview session");var preview=previewObject.AddComponent<FestivalSession>();preview.Profile.Data.Brightness=.3f;yield return null;
                world.SetPhase("Playing");world.SetLighting(shows[0].state,"me");string previewed=Light();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DevelopmentDiagnostics.Visible=true;int traced=resets;
#endif
                preview.Leave();
                if(Light()!=previewed)failures.Add("an unconnected Leave reset the preview to "+Light());
                Object.Destroy(previewObject);yield return null;
                if(Light()!=previewed)failures.Add("an unconnected session's teardown reset the preview to "+Light());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(resets!=traced)failures.Add("an unconnected session traces a leave reset it never made");
#endif
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Application.logMessageReceived-=trace;DevelopmentDiagnostics.Visible=diagnostics;
#endif
                if(friendObject!=null)Object.Destroy(friendObject);if(previewObject!=null)Object.Destroy(previewObject);
                if(hostObject!=null){host.Leave();Object.Destroy(hostObject);}
                Object.Destroy(worldObject);
            }
            yield return null;
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }
    }
}
