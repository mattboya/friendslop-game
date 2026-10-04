using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class CrowdNavigationTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator PursuersKeepTheirGapThroughHostNavigation()
        {
            var root=new GameObject("Crowd navigation test");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;
                world.SetPhase("Playing");yield return null;Physics.SyncTransforms();
                Assert.That(world.NavigationReady,Is.True);
                foreach(bool downed in new[]{false,true})
                {
                    var sim=new FestivalSimulation(3){Navigate=FestivalSession.Navigate,HasLineOfSight=FestivalSession.LineOfSight};
                    var target=sim.AddPlayer("target","Target");target.X=0;target.Z=7;target.RecoveryUntil=100;
                    if(downed){target.Life="Downed";target.DownedRemaining=35;}
                    sim.State.Phase="Playing";sim.State.SimulationSeconds=10;sim.State.Npcs.Clear();
                    foreach(float z in new[]{8.35f,9.25f})
                    {
                        var npc=new NpcState{Id="pursuer"+sim.State.Npcs.Count,Z=z,Yaw=180};
                        npc.Observers.Add(new ObserverState{PlayerId=target.Id,Suspicion=95,AccusationSeconds=0,LastSeenSeconds=10});
                        sim.State.Npcs.Add(npc);
                    }
                    float start=sim.State.Npcs[1].Z;
                    for(int step=0;step<20;step++)
                    {
                        sim.Tick(.1);var a=sim.State.Npcs[0];var b=sim.State.Npcs[1];
                        Assert.That(Vector2.Distance(new Vector2(a.X,a.Z),new Vector2(b.X,b.Z)),Is.GreaterThanOrEqualTo(.7f-1e-5f),"host navigation preserves the gap at step "+step+", downed="+downed);
                    }
                    Assert.That(sim.State.Npcs[1].Z,Is.LessThan(start-.1f),"the pursuer makes a shortened step on the real navigation mesh");
                    Assert.That(target.Health,Is.EqualTo(100),"a recovering or downed target is not struck");
                }
            }
            finally{Object.Destroy(root);}
        }
    }
}
