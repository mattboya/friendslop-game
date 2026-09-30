using System.Collections;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class PatrolNavigationTests
    {
        // ESC-1: the host moves cops over the festival's navigation mesh. A patrol waypoint off the walkable ground, or a leg
        // with no complete path, would leave a cop pinned against the scenery; a detour through the middle would walk it
        // through the dense crowd the loop is meant to skirt.
        [UnityTest]public IEnumerator CopPatrolLoopIsWalkableAroundTheCrowd()
        {
            var root=new GameObject("Patrol navigation test");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;
                Assert.That(world.NavigationReady,Is.True);
                world.SetPhase("Playing");
                var centre=FestivalCrowdLayout.Centroid();var path=new NavMeshPath();
                for(int k=0;k<FestivalSimulation.PatrolPoints;k++)
                {
                    var a=FestivalSimulation.PatrolPoint(k);var b=FestivalSimulation.PatrolPoint(k+1);
                    var from=new Vector3(a.X,0,a.Z);var to=new Vector3(b.X,0,b.Z);
                    Assert.That(NavMesh.SamplePosition(from,out var ground,.5f,NavMesh.AllAreas),Is.True,"Patrol point "+k+" "+from+" is off the walkable ground");
                    Assert.That(Mathf.Abs(ground.position.y),Is.LessThan(.3f),"Patrol point "+k+" samples a raised surface");
                    Assert.That(NavMesh.CalculatePath(from,to,NavMesh.AllAreas,path),Is.True,"No path for patrol leg "+k);
                    Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"Incomplete patrol leg "+k+" "+from+" to "+to);
                    foreach(var corner in path.corners)
                        Assert.That(Vector2.Distance(new Vector2(corner.x,corner.z),new Vector2(centre.X,centre.Z)),Is.GreaterThan(20f),"Patrol leg "+k+" detours into the crowd at "+corner);
                }
            }
            finally{Object.Destroy(root);}
        }
    }
}
