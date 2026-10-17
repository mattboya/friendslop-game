using System.Collections;
using System.Reflection;
using System.Threading;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    // The native smokes' gameplay steps, run by the smoke's own coroutines on a real hosted session.
    public sealed class SmokeWaitPlayTests:FestivalPlayModeTest
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        // A loaded machine: every frame takes .4 real seconds, and the host's per-frame cap gives the simulation only .2 of them.
        sealed class LoadedFrames:MonoBehaviour{void Update()=>Thread.Sleep(400);}

        IEnumerator HostALevel(FestivalSession session)
        {
            session.Host("Smoke waiter",HostPort);
            for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
            yield return StartLevel(session);
            var game=Simulation(session);game.State.Npcs.Clear();game.State.DurationSeconds=100000;
        }
        static string Failed(DevelopmentSmoke smoke)=>(string)typeof(DevelopmentSmoke).GetField("failed",Private).GetValue(smoke);

        [UnityTest]public IEnumerator ASoloSelfRevivalOnLoadedFramesGetsItsFullFifteenSimulationSeconds()
        {
            var world=new GameObject("impl:U74 smoke wait world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 smoke wait session");var session=holder.AddComponent<FestivalSession>();yield return null;
            GameObject load=null;
            try
            {
                yield return HostALevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
                var smoke=holder.AddComponent<DevelopmentSmoke>();smoke.enabled=false;
                load=new GameObject("impl:U74 loaded frames",typeof(LoadedFrames));
                double real=Time.realtimeSinceStartupAsDouble,simulation=game.State.SimulationSeconds;
                yield return (IEnumerator)typeof(DevelopmentSmoke).GetMethod("SoloRevival",Private).Invoke(smoke,new object[]{session});
                real=Time.realtimeSinceStartupAsDouble-real;simulation=game.State.SimulationSeconds-simulation;
                string failed=Failed(smoke);
                TestContext.WriteLine("impl:U74 SOLO_REVIVAL real="+real.ToString("F2")+" sim="+simulation.ToString("F2")+" failed="+failed+" life="+player.Life+" revivals="+player.RevivalCount);
                Assert.That(real,Is.GreaterThan(1.5*simulation),"setup: loaded frames spend real time faster than simulation time");
                Assert.That(failed,Is.Null,"the revival's wait counts simulation seconds from the host's acceptance");
                Assert.That(player.Life,Is.EqualTo("Alive"));Assert.That(player.RevivalCount,Is.EqualTo(1));
            }
            finally{if(load!=null)Object.Destroy(load);session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }

        [UnityTest]public IEnumerator ACommandTheHostRefusesFailsTheStepByName()
        {
            var world=new GameObject("impl:U74 smoke refusal world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 smoke refusal session");var session=holder.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return HostALevel(session);
                var player=Simulation(session).Player(session.LocalPlayerId);player.X=0;player.Z=0;
                var smoke=holder.AddComponent<DevelopmentSmoke>();smoke.enabled=false;yield return null;
                yield return (IEnumerator)typeof(DevelopmentSmoke).GetMethod("Interact",Private).Invoke(smoke,new object[]{session,"Extract","","",float.PositiveInfinity});
                string failed=Failed(smoke);var finished=typeof(DevelopmentSmoke).GetField("finished",Private).GetValue(smoke);
                TestContext.WriteLine("impl:U74 REFUSED failed="+failed+" message="+session.Message);
                Assert.That(failed,Does.StartWith("Extract refused: "),"an extraction away from the gate never starts, and the step says so");
                Assert.That(failed.Length,Is.GreaterThan("Extract refused: ".Length),"the host's reason is kept");
                Assert.That(finished,Is.Null);
            }
            finally{session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }
    }
}
