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
    // A hosted crew of two out on Day 1. The friend grabs a Giggle Tank: the host hears the oh-yeah and is told who grabbed
    // it, and the friend carrying it inside the camp gate raises the stash. The host grabbing the next one hears it again and reads
    // their own reply, then carries it home and is told so.
    public sealed class GiggleTankPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TheCrewHearsEachGrabAndTheGatePaysTheCarrier()
        {
            var world=new GameObject("Giggle Tank world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Giggle Tank host");var session=hostObject.AddComponent<FestivalSession>();var hud=hostObject.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            // Up to a second of frames, until done.
            IEnumerator Until(System.Func<bool> done){for(float until=Time.realtimeSinceStartup+1;!done()&&Time.realtimeSinceStartup<until;)yield return null;}
            try
            {
                session.Host("Tester",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var mate=sim.AddPlayer("tank_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                var me=sim.Player(session.LocalPlayerId);sim.State.Npcs.Clear();me.X=0;me.Z=0;
                // A tank forced at spot 0, with `at` standing on it.
                void Force(PlayerState at){sim.State.GiggleTankSpot=0;sim.State.GiggleTankFound=false;at.X=Festivals.GiggleTankSpots[0].X;at.Z=Festivals.GiggleTankSpots[0].Z;}
                Force(mate);yield return new WaitForSeconds(.3f);
                int plays=hud.OhYeahPlays,stash=sim.State.StashCash;

                Assert.That(sim.Execute(mate.Id,new GameCommand{Id="tank_grab",Kind=FestivalSimulation.GrabGiggleTankKind}).Accepted,Is.True,"setup: the friend grabs the tank");
                const string Crew="Sam grabbed a Giggle Tank! Get it home: +$25 for camp";
                yield return Until(()=>session.Message==Crew);
                if(session.Message!=Crew)failures.Add("after the friend's grab the host reads \""+session.Message+"\"");
                if(hud.OhYeahPlays!=plays+1)failures.Add("the friend's grab played the oh-yeah "+(hud.OhYeahPlays-plays)+" times");
                yield return new WaitForSeconds(.3f);
                if(hud.OhYeahPlays!=plays+1)failures.Add("one grab played the oh-yeah "+(hud.OhYeahPlays-plays)+" times");

                mate.X=Festivals.CampGateX;mate.Z=Festivals.CampGateZ;
                yield return Until(()=>session.State.StashCash==stash+Festivals.GiggleTankCash);
                if(session.State.StashCash!=stash+Festivals.GiggleTankCash)failures.Add("the friend home at the gate leaves the host's stash at "+session.State.StashCash+", not "+(stash+Festivals.GiggleTankCash));
                yield return new WaitForSeconds(.3f);
                if(session.Message!=Crew)failures.Add("the friend's payout tells the host \""+session.Message+"\"");

                // A level has one tank, so the host's view first sees nobody holding one.
                sim.State.GiggleTankGrabberId=sim.State.GiggleTankGrabberName="";yield return new WaitForSeconds(.3f);
                Force(me);yield return new WaitForSeconds(.3f);
                session.Command(FestivalSimulation.GrabGiggleTankKind);
                const string Own="Giggle Tank! Carry it home to the camp gate: +$25 for camp";
                yield return Until(()=>hud.OhYeahPlays==plays+2);
                if(hud.OhYeahPlays!=plays+2)failures.Add("the host's grab left the oh-yeah at "+(hud.OhYeahPlays-plays)+" plays");
                if(session.Message!=Own)failures.Add("after their own grab the host reads \""+session.Message+"\"");
                me.X=Festivals.CampGateX;me.Z=Festivals.CampGateZ;
                const string Home="Giggle Tank home! +$25 for camp";
                yield return Until(()=>session.Message==Home);
                if(session.Message!=Home)failures.Add("home at the gate the host reads \""+session.Message+"\"");
                if(hud.OhYeahPlays!=plays+2)failures.Add("after banking the oh-yeah has played "+(hud.OhYeahPlays-plays)+" times, not 2");
            }
            finally{session.Leave();Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
            Assert.That(failures,Is.Empty,"A Giggle Tank, grabbed by a friend, then by the host:\n"+string.Join("\n",failures));
        }
    }
}
