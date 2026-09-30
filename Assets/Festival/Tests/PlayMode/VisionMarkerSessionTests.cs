using System.Collections;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class VisionMarkerSessionTests
    {
        // VISION-1: in a hosted game the solo host is the tripper, so their client draws every vision, over the festivalgoers'
        // drawn bodies; leaving the game clears them.
        [UnityTest]public IEnumerator TheTrippersGameDrawsVisionsOverTheCrowd()
        {
            var world=new GameObject("Vision marker world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Vision marker session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",8605);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"Day 1 starts: "+session.Message);
                yield return null;

                Assert.That(session.State.TripperId,Is.EqualTo(session.LocalPlayerId),"setup: the solo host is the tripper");
                var markers=host.GetComponent<FestivalVisionMarkers>();
                Assert.That(markers,Is.Not.Null,"the game draws the tripper's visions");
                Assert.That(session.State.Visions,Is.Not.Empty,"setup: the tripper has visions");
                Assert.That(markers.Shown,Is.EqualTo(session.State.Visions.Count),"every vision is drawn");
                var bodies=GameObject.Find("Authoritative actor presentation").transform;
                foreach(var vision in session.State.Visions)
                {
                    if(vision.NpcId=="")continue;
                    Vector3 marker=host.transform.Find("Vision "+vision.Id).position,body=bodies.Find(vision.NpcId).position;
                    Assert.That(Vector2.Distance(new Vector2(marker.x,marker.z),new Vector2(body.x,body.z)),Is.LessThan(1e-3f),vision.Kind+" hangs over "+vision.NpcId+"'s drawn body");
                    Assert.That(marker.y-body.y,Is.GreaterThan(2f),vision.Kind+" floats over "+vision.NpcId+"'s head");
                }

                session.Leave();yield return null;
                Assert.That(markers.Shown,Is.Zero,"leaving the game clears the markers");
                foreach(Transform child in host.transform)Assert.That(child.name,Does.Not.StartWith("Vision "),"leaving the game removes the markers");
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
