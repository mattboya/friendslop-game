using System;
using System.Collections;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class PatrolNavigationTests
    {
        const double HostTick=1.0/30;
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
            finally{UnityEngine.Object.Destroy(root);}
        }

        // ESC-1: a stop, a chase or a bust can leave a cop anywhere. Moved exactly as the host moves it (FestivalSession.Navigate:
        // a navmesh path, then a capsule slide against the real scenery) at the host's 30 Hz tick, a cop dropped at any landmark
        // walks back to the loop and keeps lapping it: at least 2 laps in 300 s, and at least 1 m in every 10 s.
        [UnityTest,Timeout(600000)]public IEnumerator CopsWalkBackToTheLoopFromEveryLandmark()
        {
            var root=new GameObject("Patrol movement test");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;
                world.SetPhase("Playing");yield return null;Physics.SyncTransforms();
                var centre=FestivalCrowdLayout.Centroid();var failures=new List<string>();
                var landmarks=new List<(string name,float x,float z)>{("camp gate",0,-32),("backstage",0,34),("night market",-18,-22),("medical",24,-20),
                    ("lost property",-28,16),("holding desk",27,5),("stash",-25,-8),("crowd centre",centre.X,centre.Z)};
                for(int seed=0;seed<3;seed++){var friend=new FestivalSimulation(seed).State.FriendPosition;landmarks.Add(("lost friend "+seed,friend.X,friend.Z));}
                foreach(var landmark in landmarks)
                {
                    var sim=HostedLevel();var cops=sim.State.Npcs.FindAll(n=>n.Kind=="Cop");
                    foreach(var cop in cops){cop.X=landmark.x;cop.Z=landmark.z;}
                    var laps=new double[cops.Count];var stalled=new string[cops.Count];
                    for(int window=1;window<=30;window++)
                    {
                        var marks=cops.ConvertAll(c=>new Vector2(c.X,c.Z));
                        for(int i=0;i<300;i++)
                        {
                            var bearings=cops.ConvertAll(c=>Bearing(centre,c));sim.Tick(HostTick);
                            for(int c=0;c<cops.Count;c++)laps[c]+=Mathf.DeltaAngle(bearings[c],Bearing(centre,cops[c]))/360;
                        }
                        for(int c=0;c<cops.Count;c++)
                            if(stalled[c]==null&&Vector2.Distance(marks[c],new Vector2(cops[c].X,cops[c].Z))<1)stalled[c]="stalled in the "+(window*10)+" s window at "+At(cops[c])+" (mode "+cops[c].Mode+")";
                    }
                    for(int c=0;c<cops.Count;c++)
                        if(laps[c]<2||stalled[c]!=null)failures.Add(landmark.name+" "+At(landmark.x,landmark.z)+": "+cops[c].Id+" walked "+laps[c].ToString("F2")+" laps in 300 s"+(stalled[c]==null?"":", "+stalled[c])+", ends at "+At(cops[c]));
                    yield return null;
                }
                Assert.That(failures,Is.Empty,"Cops that do not find their way back to the loop:\n"+string.Join("\n",failures));
            }
            finally{UnityEngine.Object.Destroy(root);}
        }

        // ESC-1: the same from every walkable 3 m grid point of the festival ground: moved the way the host moves it, a lone cop
        // walks at least 1 m in every 10 s for 90 s. Any prop the navmesh routes over but the capsule cannot cross, or a path
        // corner the cop can stop on, shows up here as a cop parked for good.
        [UnityTest,Timeout(600000)]public IEnumerator ACopNeverParksAnywhereOnTheFestivalGround()
        {
            var root=new GameObject("Patrol sweep test");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;
                world.SetPhase("Playing");yield return null;Physics.SyncTransforms();
                var parked=new List<string>();int starts=0;
                for(float x=-39;x<=39;x+=3)for(float z=-39;z<=39;z+=3)
                {
                    if(!NavMesh.SamplePosition(new Vector3(x,0,z),out _,.3f,NavMesh.AllAreas))continue;
                    starts++;var sim=HostedLevel();sim.State.Npcs.RemoveAll(n=>n.Id!="cop_0");var cop=sim.State.Npcs[0];cop.X=x;cop.Z=z;
                    for(int window=1;window<=9;window++)
                    {
                        var mark=new Vector2(cop.X,cop.Z);for(int i=0;i<300;i++)sim.Tick(HostTick);
                        if(Vector2.Distance(mark,new Vector2(cop.X,cop.Z))<1){parked.Add("from "+At(x,z)+": parked at "+At(cop)+" in the "+(window*10)+" s window");break;}
                    }
                    if(starts%40==0)yield return null;
                }
                Assert.That(starts,Is.GreaterThan(500),"walkable start points on the festival ground");
                Assert.That(parked,Is.Empty,parked.Count+" of "+starts+" start points leave the cop parked:\n"+string.Join("\n",parked));
            }
            finally{UnityEngine.Object.Destroy(root);}
        }

        // ESC-1: the review's end-to-end case. A patrolling cop stops a player carrying stock on the south-west leg, follows them
        // to a spot, witnesses a sale there and detains them. Back on patrol, that cop walks a full lap within 120 s.
        [UnityTest,Timeout(600000)]public IEnumerator ACopWalksAFullLapAfterABust()
        {
            var root=new GameObject("Patrol bust test");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;
                world.SetPhase("Playing");yield return null;Physics.SyncTransforms();
                var centre=FestivalCrowdLayout.Centroid();var failures=new List<string>();int commands=0;
                foreach(var spot in new[]{new Vector2(-25,-6),new Vector2(-18,-20),new Vector2(-28,-2)})
                {
                    var sim=HostedLevel();sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");var cop=sim.State.Npcs.Find(n=>n.Id=="cop_0");var a=sim.Player("a");
                    for(int i=0;i<3000&&!(Bearing(centre,cop)>205&&Bearing(centre,cop)<225);i++)sim.Tick(HostTick);
                    string where="bust at "+At(spot.x,spot.y)+": ";
                    Assert.That(Bearing(centre,cop),Is.InRange(205f,225f),where+"setup: cop_0 reaches the south-west leg, at "+At(cop));
                    double heading=cop.Yaw*Math.PI/180;a.X=cop.X+(float)(5*Math.Sin(heading));a.Z=cop.Z+(float)(5*Math.Cos(heading));
                    a.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});sim.Tick(HostTick);
                    Assert.That(cop.Mode,Is.EqualTo("Stop"),where+"setup: the cop stops a player carrying stock");
                    for(int i=0;i<4000&&Vector2.Distance(new Vector2(a.X,a.Z),spot)>.1f;i++){var step=Vector2.ClampMagnitude(spot-new Vector2(a.X,a.Z),2.5f*(float)HostTick);a.X+=step.x;a.Z+=step.y;sim.Tick(HostTick);}
                    for(int i=0;i<90;i++)sim.Tick(HostTick);
                    sim.State.Npcs.Add(new NpcState{Id="buyer",X=a.X+1,Z=a.Z,Yaw=-90});
                    Assert.That(sim.Execute("a",new GameCommand{Id="bust_"+(++commands),Kind="StartSale",TargetId="buyer",ItemId="stock_lsd"}).Accepted,Is.True,where+"setup: the sale starts");
                    for(int i=0;i<150;i++)sim.Tick(HostTick);
                    Assert.That(a.Life,Is.EqualTo("Detained"),where+"setup: the cop witnessed the sale and detained the seller");
                    sim.State.Npcs.RemoveAll(n=>n.Id=="buyer");
                    double laps=0;
                    for(int i=0;i<3600;i++){float before=Bearing(centre,cop);sim.Tick(HostTick);laps+=Mathf.DeltaAngle(before,Bearing(centre,cop))/360;}
                    if(laps<1)failures.Add(where+"after the arrest the cop walked "+laps.ToString("F2")+" laps in 120 s and ends at "+At(cop)+" (mode "+cop.Mode+")");
                    yield return null;
                }
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{UnityEngine.Object.Destroy(root);}
        }

        // A level driven the way a hosted game drives it: the host's navigation and line of sight, one player far off the map.
        static FestivalSimulation HostedLevel()
        {
            var sim=new FestivalSimulation(1){Navigate=FestivalSession.Navigate,HasLineOfSight=FestivalSession.LineOfSight};
            sim.AddPlayer("a","A");var a=sim.Player("a");a.X=0;a.Z=-60;sim.State.Phase="Playing";sim.State.DurationSeconds=1e6;return sim;
        }
        static float Bearing(WorldPoint centre,NpcState n){float b=Mathf.Atan2(n.X-centre.X,n.Z-centre.Z)*Mathf.Rad2Deg;return b<0?b+360:b;}
        static string At(NpcState n)=>At(n.X,n.Z);
        static string At(float x,float z)=>"("+x.ToString("F2")+", "+z.ToString("F2")+")";
    }
}
