using System.Collections;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    // Real sessions follow their own snapshots: the host and a joined friend each report an accepted take once, and a friend who
    // leaves and joins again hears nothing old.
    public sealed class GiggleTakePlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator HostAndFriendEachReportATakeOnceAndARejoinReplaysNothing()
        {
            var world=new GameObject("Giggle take world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Giggle take host");var host=hostObject.AddComponent<FestivalSession>();
            var friendObject=new GameObject("Giggle take friend");var friend=friendObject.AddComponent<FestivalSession>();
            var hostHeard=new List<string>();var friendHeard=new List<string>();
            host.GiggleBalloonTaken+=(player,count)=>hostHeard.Add(player+" x"+count);friend.GiggleBalloonTaken+=(player,count)=>friendHeard.Add(player+" x"+count);
            yield return null;
            try
            {
                ushort port=FreePort();host.Host("Tester",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,"setup: host has a local player: "+host.Message);
                friend.Join("Friend","127.0.0.1",port);
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,"setup: the friend joins: "+friend.Message);
                var sim=Simulation(host);string me=host.LocalPlayerId;var player=sim.Player(me);
                player.X=CampFeatures.GiggleBalloonX+1.2f;player.Z=CampFeatures.GiggleBalloonZ;
                IEnumerator Settle(){for(float until=Time.realtimeSinceStartup+.6f;Time.realtimeSinceStartup<until;)yield return null;}
                yield return Settle();
                Assert.That(hostHeard.Count+friendHeard.Count,Is.Zero,"hosting and joining report nothing");

                host.Command(FestivalSimulation.TakeGiggleBalloonKind);
                Assert.That(player.VisualGiggleTakeSequence,Is.EqualTo(1),"setup: the host takes a balloon: "+host.Message);
                yield return Settle();
                Assert.That(hostHeard,Is.EqualTo(new[]{me+" x1"}),"the host reports their own take once");
                Assert.That(friendHeard,Is.EqualTo(new[]{me+" x1"}),"the friend reports it once, from the host's snapshots");

                friend.Leave();Assert.That(Take(sim,me),Is.True,"setup: a second take while the friend is away");
                friend.Join("Friend","127.0.0.1",port);
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,"setup: the friend joins again: "+friend.Message);
                yield return Settle();
                Assert.That(friendHeard.Count,Is.EqualTo(1),"a rejoined friend hears no take from before it joined");
                Assert.That(hostHeard,Is.EqualTo(new[]{me+" x1",me+" x1"}),"the host reports the second take");
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }

        static bool Take(FestivalSimulation sim,string id)=>sim.Execute(id,new GameCommand{Id="away-take",Kind=FestivalSimulation.TakeGiggleBalloonKind}).Accepted;
    }
}
