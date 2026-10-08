using System;
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
            finally{UnityEngine.Object.Destroy(root);}
        }

        static FestivalSimulation Eight(float targetZ=7,bool downed=false)
        {
            var sim=new FestivalSimulation(3){Navigate=FestivalSession.Navigate,HasLineOfSight=FestivalSession.LineOfSight};
            var target=sim.AddPlayer("target","Target");target.X=0;target.Z=targetZ;target.RecoveryUntil=1000;
            if(downed){target.Life="Downed";target.DownedRemaining=35;}
            sim.State.Phase="Playing";sim.State.SimulationSeconds=10;sim.State.Npcs.Clear();
            for(int k=0;k<8;k++){var n=new NpcState{Id="nav ring"+k,Z=targetZ+3+k*.85f,Yaw=180};n.Observers.Add(new ObserverState{PlayerId=target.Id,Suspicion=95,AccusationSeconds=0,LastSeenSeconds=10});sim.State.Npcs.Add(n);}
            return sim;
        }
        static void SeparateAndBounded(FestivalSimulation sim,Vector2[] before,int step)
        {
            for(int a=0;a<sim.State.Npcs.Count;a++)
            {
                var n=sim.State.Npcs[a];var at=new Vector2(n.X,n.Z);
                Assert.That(Vector2.Distance(at,before[a]),Is.LessThanOrEqualTo(.4101f),"host navigation step stays bounded at "+step);
                for(int b=a+1;b<sim.State.Npcs.Count;b++){var other=sim.State.Npcs[b];Assert.That(Vector2.Distance(at,new Vector2(other.X,other.Z)),Is.GreaterThanOrEqualTo(.7f-1e-5f),"all eight keep their gap at "+step+": "+a+" / "+b);}
            }
        }
        [UnityTest]public IEnumerator EightStationaryDownedAndMovingPursuersFillPlacesThroughHostNavigation()
        {
            var root=new GameObject("Eight crowd navigation test");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;world.SetPhase("Playing");yield return null;Physics.SyncTransforms();Assert.That(world.NavigationReady,Is.True);
                for(int variant=0;variant<3;variant++)
                {
                    var sim=Eight(downed:variant==1);var target=sim.Player("target");
                    for(int step=0;step<240;step++)
                    {
                        var before=sim.State.Npcs.ConvertAll(n=>new Vector2(n.X,n.Z)).ToArray();
                        if(variant==2){target.X+=.015f;target.Z-=.01f;}
                        sim.Tick(.1);SeparateAndBounded(sim,before,step);
                    }
                    int inner=0,outer=0;foreach(var n in sim.State.Npcs){float d=Vector2.Distance(new Vector2(n.X,n.Z),new Vector2(target.X,target.Z));if(d<=1.5f)inner++;if(Math.Abs(d-2.2)<.15)outer++;}
                    Assert.That(inner,Is.EqualTo(6),"six inner places through real navigation, variant "+variant);Assert.That(outer,Is.EqualTo(2),"two outer places through real navigation, variant "+variant);
                    Assert.That(target.Health,Is.EqualTo(100));
                }
            }
            finally{UnityEngine.Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator BlockedWallNeverAuthorizesACrowdTeleport()
        {
            var root=new GameObject("Crowd blocked wall test");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;world.SetPhase("Playing");yield return null;
                var wall=new GameObject("Physical crowd wall");wall.transform.SetParent(root.transform);wall.transform.position=new Vector3(0,1,9.5f);wall.AddComponent<BoxCollider>().size=new Vector3(100,2,.3f);Physics.SyncTransforms();
                var sim=Eight();foreach(var n in sim.State.Npcs)n.Z+=1;
                for(int step=0;step<100;step++)
                {
                    var before=sim.State.Npcs.ConvertAll(n=>new Vector2(n.X,n.Z)).ToArray();sim.Tick(.1);SeparateAndBounded(sim,before,step);
                    foreach(var n in sim.State.Npcs)Assert.That(n.Z,Is.GreaterThan(9.95f),"a legitimate blocked standstill cannot cross the collision wall");
                }
                Assert.That(sim.Player("target").Health,Is.EqualTo(100));
            }
            finally{UnityEngine.Object.Destroy(root);}
        }

        [Test]public void ReservedPlacesSurviveJsonRestoreAndStayHostOnly()
        {
            var sim=Eight();sim.Navigate=null;sim.Tick(.1);var before=sim.State.Npcs.ConvertAll(n=>(n.ApproachTargetId,n.ApproachEpoch,n.ApproachSlot));
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(sim.State)));restored.Tick(.1);
            for(int k=0;k<8;k++){var n=restored.State.Npcs[k];Assert.That((n.ApproachTargetId,n.ApproachEpoch,n.ApproachSlot),Is.EqualTo(before[k]),"host JSON preserves a stable reservation");}
            foreach(string viewer in new[]{"target","unknown"})
            {
                var view=FestivalSession.ViewFor(restored,viewer);Assert.That(view.CrowdArrivalEpoch,Is.Zero,"arrival sequence stays host-only");
                foreach(var n in view.Npcs){Assert.That(n.ApproachTargetId,Is.Empty);Assert.That(n.ApproachEpoch,Is.Zero);Assert.That(n.ApproachSlot,Is.EqualTo(-1));Assert.That(n.CrowdDetourSide,Is.Zero);}
            }
            var old=JsonUtility.FromJson<NpcState>("{\"Id\":\"legacy\"}");Assert.That(old.ApproachTargetId,Is.Empty);Assert.That(old.ApproachEpoch,Is.Zero);Assert.That(old.ApproachSlot,Is.EqualTo(-1));Assert.That(old.CrowdDetourSide,Is.Zero);
        }
    }
}
