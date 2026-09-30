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
    public sealed class TwistVisualsSessionTests:FestivalPlayModeTest
    {
        // TWISTVIS-1: in a hosted Palm Mirage level the running game raises the festival's twists and films through every
        // influencer's frame.
        [UnityTest]public IEnumerator APalmMirageLevelShowsItsTwistsInTheRunningGame()
        {
            var world=new GameObject("Twist world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Twist session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                // The session drives the first world it finds, so a world leaked by an earlier test would take the twists.
                Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsSortMode.None),Has.Length.EqualTo(1),"setup: a world leaked from an earlier test");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                Assert.That(sim.State.FestivalIndex,Is.EqualTo(Festivals.PoloFestival),"setup: a new game starts at Palm Mirage");

                var player=sim.Player(session.LocalPlayerId);player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"Day 1 starts: "+session.Message);
                yield return null;yield return null;

                var grounds=world.transform.Find(FestivalWorld.RootName);
                var polo=grounds.Find(FestivalTwistVisuals.PoloRootName);
                Assert.That(polo!=null&&polo.gameObject.activeInHierarchy,Is.True,"Palm Mirage's ropes and wheel stand at the festival");
                Assert.That(grounds.Find(FestivalTwistVisuals.PlayaRootName).gameObject.activeInHierarchy,Is.False,"Ember Playa's art cars and effigy are not here");
                var influencers=session.State.Npcs.FindAll(n=>n.Twist==FestivalSimulation.Influencer);
                Assert.That(influencers.Count,Is.EqualTo(Festivals.Influencers),"setup: the host's view carries every influencer");
                var frames=new List<string>();foreach(Transform child in grounds)if(child.name.StartsWith(FestivalTwistVisuals.FramePrefix)&&child.gameObject.activeInHierarchy)frames.Add(child.name);
                Assert.That(frames.Count,Is.EqualTo(Festivals.Influencers),"every influencer films through a frame");
                foreach(var n in influencers)
                {
                    var frame=grounds.Find(FestivalTwistVisuals.FramePrefix+n.Id);
                    Assert.That(frame,Is.Not.Null,n.Id+" films through a frame");
                    Assert.That(Vector2.Distance(new Vector2(frame.position.x,frame.position.z),new Vector2(n.X,n.Z)),Is.LessThan(.5f),n.Id+"'s frame stays with them");
                }
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
