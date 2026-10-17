using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    // A joined friend's real snapshots carry the host's confetti burst and the festivalgoer it fooled, dancing and facing the burst,
    // but neither the host's gear nor whom that festivalgoer suspected.
    public sealed class ConfettiWirePlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator AJoinedFriendSeesTheBurstAndTheDanceButNotTheGearOrTheSuspect()
        {
            var world=new GameObject("Confetti wire world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Confetti wire host");var host=hostObject.AddComponent<FestivalSession>();
            var friendObject=new GameObject("Confetti wire friend");var friend=friendObject.AddComponent<FestivalSession>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Host",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);friend.Join("Friend","127.0.0.1",port);
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,friend.Message);
                var sim=Simulation(host);var mate=sim.Player(friend.LocalPlayerId);mate.X=0;mate.Z=19;friend.Command("Ready");
                while(!mate.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(mate.Ready,Is.True,"setup: the friend's Ready reached the host");yield return StartLevel(host,friend.LocalPlayerId);
                var shooter=sim.Player(host.LocalPlayerId);sim.State.Npcs.Clear();shooter.X=0;shooter.Z=0;mate.X=-20;mate.Z=-20;
                shooter.Inventory.Add(new ItemStack{ItemId=FestivalSimulation.ConfettiItem,Count=2});
                var fooled=new NpcState{Id="confetti_fooled",X=0,Z=3,Yaw=0,Mode="Questioning",TargetId=shooter.Id};
                fooled.Observers.Add(new ObserverState{PlayerId=shooter.Id,Suspicion=50,LastSeenSeconds=sim.State.SimulationSeconds});sim.State.Npcs.Add(fooled);
                host.MenuOpen=false;host.Command("Use",item:FestivalSimulation.ConfettiItem);
                Assert.That(host.Message,Is.EqualTo("Confetti! 1 festivalgoer looks away for 4 s"),"setup: the host fires");
                NpcState Seen()=>friend.State.Npcs.Find(n=>n.Id==fooled.Id);
                deadline=Time.realtimeSinceStartup+10;
                while((friend.State.Players.Find(p=>p.Id==shooter.Id)?.VisualConfettiBurstSequence!=1||Seen()?.Mode!="Distracted")&&Time.realtimeSinceStartup<deadline)yield return null;
                var seenShooter=friend.State.Players.Find(p=>p.Id==shooter.Id);
                Assert.That(seenShooter.VisualConfettiBurstSequence,Is.EqualTo(1),"the friend's snapshot counts the host's burst");
                Assert.That(seenShooter.Inventory,Is.Empty,"but carries none of the host's gear");
                Assert.That(Seen()?.Mode,Is.EqualTo("Distracted"),"the friend sees the fooled festivalgoer dance");
                Assert.That(Seen().Yaw,Is.EqualTo(180).Within(.01),"facing the burst");
                Assert.That(Seen().TargetId,Is.Empty,"never whom it suspected");
                Assert.That(Seen().Suspicion,Is.Zero,"nor the host's suspicion");
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }
    }
}
