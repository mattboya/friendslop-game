using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class DoseMovementSessionTests:FestivalPlayModeTest
    {
        static int sequence=1000000;
        const float OriginX=0,OriginZ=7;
        // Submit the real host intent and run its fixed-step consumer deterministically; batchmode's unfocused keyboard is
        // suppressed by the gameplay input reader. This exercises the host's sway, speed, collision and TryMove together.
        static void Step(FestivalSession session,float x,float z)
        {
            var type=typeof(FestivalSession).Assembly.GetType("Festival.Network.MoveIntent");var intent=Activator.CreateInstance(type);
            type.GetField("X").SetValue(intent,x);type.GetField("Z").SetValue(intent,z);
            type.GetField("Sequence").SetValue(intent,++sequence);
            typeof(FestivalSession).GetMethod("AcceptInput",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new[]{(object)session.LocalPlayerId,intent});
            var inputs=(IDictionary)typeof(FestivalSession).GetField("inputs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
            var peer=inputs[session.LocalPlayerId];var accepted=peer.GetType().GetField("Intent").GetValue(peer);
            Assert.That(type.GetField("Sequence").GetValue(accepted),Is.EqualTo(sequence),"setup: the host accepts this fresh input intent");
            typeof(FestivalSession).GetMethod("ApplyMovement",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new object[]{.1});
        }
        static void Place(PlayerState player)
        {
            var clear=FestivalSession.Navigate(OriginX,OriginZ,OriginX,OriginZ+1,.4);
            Assert.That(clear.Z,Is.GreaterThan(OriginZ+.3f),"setup: this production walking path is clear of market counters and other colliders");
            player.X=OriginX;player.Z=OriginZ;player.Effects.Clear();
        }

        [UnityTest]public IEnumerator CouchLockShortensTheActualHostStepAndKeepsTheFloor()
        {
            var world=new GameObject("impl:U12 walk world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U12 walk host");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Walker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);session.enabled=false;
                var player=Simulation(session).Player(session.LocalPlayerId);
                var expected=new[]{.3312f,.3168f,.3024f,.288f};
                for(int dose=1;dose<=4;dose++)
                {
                    Place(player);player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose,Substance="weed"});
                    Step(session,0,1);
                    Assert.That(player.Z-OriginZ,Is.EqualTo(expected[dose-1]).Within(1e-5f),"Couch Lock dose "+dose+" reaches the actual host position");
                    Assert.That(player.X,Is.EqualTo(OriginX),"Couch Lock adds no sideways motion");
                }
                player.X=OriginX;player.Z=OriginZ;player.Effects.Add(new ActiveEffect{Id="weed"});Step(session,0,1);
                Assert.That(player.Z-OriginZ,Is.EqualTo(.26f).Within(1e-5f),"dose four plus shop Couch Lock uses the .65 floor");
                var view=FestivalSession.ViewFor(Simulation(session),player.Id);
                Assert.That(view.Players.Find(p=>p.Id==player.Id).Z,Is.EqualTo(player.Z),"the authoritative walk reaches the recipient snapshot");
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator DoseSwayReachesHostWalkingButNeverIdleOrARootedDance()
        {
            var world=new GameObject("impl:U12 sway world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U12 sway host");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Walker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);session.enabled=false;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);sim.State.SimulationSeconds=0;
                foreach(string substance in new[]{"lsd","mushrooms"})
                {
                    double peak=Math.PI/2-(substance=="mushrooms"?1.1:0);float ratio=substance=="lsd"?.19f:.14f;
                    Place(player);var effect=new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance=substance,StartSeconds=peak};player.Effects.Add(effect);
                    Step(session,0,1);Assert.That((player.X-OriginX)/(player.Z-OriginZ),Is.EqualTo(ratio).Within(1e-4f),substance+" uses its own positive sway peak");
                    effect.StartSeconds+=Math.PI;player.X=OriginX;player.Z=OriginZ;Step(session,0,1);
                    Assert.That((player.X-OriginX)/(player.Z-OriginZ),Is.EqualTo(-ratio).Within(1e-4f),"changing only the dose's start reverses its sway");
                    player.X=OriginX;player.Z=OriginZ;Step(session,0,0);
                    Assert.That(new Vector2(player.X,player.Z),Is.EqualTo(new Vector2(OriginX,OriginZ)),"sway never moves an idle player");
                }
                var npc=sim.State.Npcs.Find(n=>n.Role=="Regular");Assert.That(npc,Is.Not.Null);
                player.X=npc.X+.8f;player.Z=npc.Z;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="impl:U12-rooted-dance",Kind="Dance",TargetId=npc.Id}).Accepted,Is.True);
                var from=new Vector2(player.X,player.Z);Step(session,0,1);
                Assert.That(new Vector2(player.X,player.Z),Is.EqualTo(from),"an active production dance stays rooted despite dose sway");
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(world);}
            yield return null;
        }

        // TRIP-17: the host's own mover eases a Pony Dust walker into the stall's near stop and out again, never snapping, on the
        // simulation clock; a Rolly Pollies host step is 8% longer than the dose alone.
        [UnityTest]public IEnumerator PonyDustStallsAndRecoversSmoothlyThroughTheHostMover()
        {
            var world=new GameObject("TRIP-17 stall world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("TRIP-17 stall host");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Walker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);session.enabled=false;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                Place(player);player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance="ketamine",StartSeconds=0});
                float free=4*FestivalSimulation.DoseMovementMultiplier(1)*.1f,least=free,previous=-1;
                for(double at=7.5;at<=11;at+=.05)
                {
                    sim.State.SimulationSeconds=at;player.X=OriginX;player.Z=OriginZ;Step(session,0,1);
                    float stride=player.Z-OriginZ;
                    Assert.That(stride,Is.EqualTo(free*(1-(1-Intoxication.StallFloor)*Intoxication.Stall(player,at))).Within(1e-4f),"the host's step follows the stall at "+at+" s");
                    if(previous>=0)Assert.That(Mathf.Abs(stride-previous),Is.LessThan(.3f*free),"no snap between .05 s steps at "+at+" s: "+previous+" to "+stride);
                    least=Mathf.Min(least,stride);previous=stride;
                }
                Assert.That(least,Is.LessThanOrEqualTo(.15f*free).And.GreaterThan(0),"Pony Dust drags the walker to a near stop, never a dead one");
                Assert.That(previous,Is.EqualTo(free).Within(1e-4f),"and lets go");
                Place(player);player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance="ecstasy"});Step(session,0,1);
                Assert.That(player.Z-OriginZ,Is.EqualTo(free*1.08f).Within(1e-4f),"a Rolly Pollies step is 8% longer than the dose alone");
            }
            finally{session.Leave();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(world);}
            yield return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [UnityTest]public IEnumerator HostMovementSamplingIsOptInAndThrottled()
        {
            bool previous=DevelopmentDiagnostics.Visible;var samples=new List<string>();
            Application.LogCallback capture=(message,trace,type)=>{if(message.StartsWith("[Festival.Movement]"))samples.Add(message);};
            Application.logMessageReceived+=capture;DevelopmentDiagnostics.Visible=false;
            var world=new GameObject("impl:U12 sampled world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U12 sampled host");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Walker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);session.enabled=false;
                var player=Simulation(session).Player(session.LocalPlayerId);Place(player);
                player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance="weed"});
                Step(session,0,1);Assert.That(samples,Is.Empty,"ordinary play never logs movement");
                DevelopmentDiagnostics.Visible=true;Step(session,0,1);
                Assert.That(samples.Count,Is.EqualTo(1),"opting in samples the actual host consumer");
                Assert.That(samples[0],Does.Contain("dose=4").And.Contain("substance=weed").And.Contain("speed=2.880").And.Contain("drift=0.000").And.Contain("accepted=True"));
                Assert.That(samples[0],Does.Not.Contain(player.Id).And.Not.Contain(player.Name),"no player identity is logged");
                Step(session,0,1);Assert.That(samples.Count,Is.EqualTo(1),"fixed steps in the same second do not flood the log");
            }
            finally
            {
                DevelopmentDiagnostics.Visible=previous;Application.logMessageReceived-=capture;
                session.Leave();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(world);
            }
            yield return null;
        }
#endif
    }
}
