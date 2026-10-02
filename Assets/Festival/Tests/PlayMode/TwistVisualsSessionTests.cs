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

                yield return StartLevel(session);
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

        // POLO-5: friends watch a ride from the ground, so every rider sits inside their gondola as it goes round, alone or beside a
        // friend, whatever their body's shape (FestivalAppearance.Shape 0, 1 or 2, the last the widest and deepest). Three crews of
        // two put each shape on each side of a friend (riders sharing a gondola sit in id order), and the deepest look rides alone:
        // shape 2 in a tutu, with a backpack. Each crew boards together, a gondola apart, and the turn is moved on by starting their
        // rides earlier, so two crews are at the top. Not covered, as it does not fit: friends sharing a gondola sit .35 m apart, and
        // a shape-2 skirt, tutu or crossbody pouch reaches .49-.55 m out from its rider's middle, so beside a friend it shows 3-10 cm
        // past a side wall. The crews here wear neither; how many friends a gondola seats is a product call.
        [UnityTest]public IEnumerator EveryBodySitsInsideItsGondolaAloneOrBesideAFriend()
        {
            var world=new GameObject("Wheel world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Wheel session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            var failures=new List<string>();
            try
            {
                Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsSortMode.None),Has.Length.EqualTo(1),"setup: a world leaked from an earlier test");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                var crews=new[]{new[]{"player_1","player_4"},new[]{"player_11","twist_mate"},new[]{"look_b2","player_9"},new[]{"player_81"}};
                var shapes=new[]{new[]{0,2},new[]{2,1},new[]{1,0},new[]{2}};
                for(int c=0;c<crews.Length;c++)for(int r=0;r<crews[c].Length;r++)
                {
                    Assert.That(FestivalAppearance.For(crews[c][r]).Shape,Is.EqualTo(shapes[c][r]),"setup: "+crews[c][r]+"'s body shape");
                    sim.AddPlayer(crews[c][r],crews[c][r]).Ready=true;
                }
                var deep=FestivalAppearance.For("player_81");
                Assert.That(deep.Pants>=2&&deep.Accessory==0,Is.True,"setup: the lone rider wears a skirt or tutu and a backpack");
                yield return StartLevel(session);
                for(int c=0;c<crews.Length;c++)foreach(var id in crews[c])
                {
                    var rider=sim.Player(id);rider.X=Festivals.WheelX;rider.Z=Festivals.WheelZ+1.5f;
                    Assert.That(sim.Execute(id,new GameCommand{Id=id+" boards",Kind=FestivalSimulation.RideWheelKind}).Accepted,Is.True,"setup: "+id+" boards the wheel");
                }
                // Crew c boarded (c-1.5)/8 of a turn either side of half a turn ago: the wheel hangs eight gondolas, so each crew has
                // its own, and crews 1 and 2 are just either side of the top. The level runs until the first crew can have boarded
                // that long ago, as a ride starts on the rules' clock (a VisualWheelSince under 0 is off the wheel).
                double earliest=Festivals.WheelRideSeconds*(.5+1.5/8);
                for(deadline=Time.realtimeSinceStartup+30;sim.State.SimulationSeconds<earliest&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(sim.State.SimulationSeconds,Is.GreaterThanOrEqualTo(earliest),"setup: the level has run "+earliest+" s");
                foreach(var ride in sim.State.Interactions)
                    for(int c=0;c<crews.Length;c++)
                        if(ride.Kind==FestivalSimulation.RideWheelKind&&ride.Status=="Active"&&System.Array.IndexOf(crews[c],ride.PlayerId)>=0)
                            ride.StartSeconds=sim.State.SimulationSeconds-Festivals.WheelRideSeconds*(.5+(c-1.5)/8);
                yield return new WaitForSeconds(1.2f);
                foreach(var crew in crews)foreach(var id in crew)Assert.That(FestivalSimulation.OnWheel(sim.State,id),Is.True,"setup: "+id+" is still on the wheel");
                Transform Cabin(Vector3 at){foreach(var g in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(g.name=="Gondola"&&g.gameObject.activeInHierarchy&&Encased(g).Contains(at))return g;return null;}
                var taken=new List<Transform>();
                for(int c=0;c<crews.Length;c++)
                {
                    Transform theirs=null;
                    foreach(var id in crews[c])
                    {
                        string who=id+" (body shape "+FestivalAppearance.For(id).Shape+(crews[c].Length>1?", beside a friend)":", alone)");
                        var body=GameObject.Find(id);
                        if(body==null){failures.Add(who+" is not drawn");continue;}
                        var pose=body.GetComponent<FestivalCharacter>().Pose;if(pose!=FestivalSimulation.RideWheelKind)failures.Add(who+" is posed "+pose);
                        Transform hips=null;foreach(var t in body.GetComponentsInChildren<Transform>())if(t.name=="Hips")hips=t;
                        var cabin=hips==null?null:Cabin(hips.position);
                        if(cabin==null){failures.Add(who+": the hips are in no gondola");continue;}
                        if(theirs==null){Assert.That(taken.Contains(cabin),Is.False,"setup: each crew rides its own gondola");theirs=cabin;taken.Add(cabin);}
                        else if(cabin!=theirs)failures.Add(who+" is not in their crew's gondola");
                        foreach(var part in OutsideItsGondola(body,cabin))failures.Add(who+": "+part);
                    }
                }
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Riders seen from outside their gondolas:\n"+string.Join("\n",failures));
        }

        // POLO-5: what of a seated body shows outside its gondola: each skinned part's furthest vertex past ART-1's gondola (its
        // "gond" in generate_polo_dressing.py), from the gondola's middle: the floor's underside .45 m down, the walls' outer faces .63 m
        // either side and .53 m ahead and behind, their tops .01 m up. Below the wall tops nothing may come through; above them, in
        // the open air under the roof, a hand, a hat brim or a backpack's top may overhang by up to Overhang. Vertices, not the
        // renderers' bounds: a skinned renderer's bounds join a box per bone, which with the arms bent stand up to 10 cm wider than
        // the mesh.
        internal static List<string> OutsideItsGondola(GameObject body,Transform gondola)
        {
            const float Underside=.45f,Side=.63f,End=.53f,WallTop=.01f,Overhang=.05f;
            var outside=new List<string>();var mesh=new Mesh();var middle=gondola.position;
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!skin.enabled)continue;
                skin.BakeMesh(mesh,true);var toWorld=skin.transform.localToWorldMatrix;float worst=0;var at=Vector3.zero;
                foreach(var vertex in mesh.vertices)
                {
                    var p=toWorld.MultiplyPoint3x4(vertex)-middle;
                    float past=Mathf.Max(Mathf.Abs(p.x)-Side,Mathf.Abs(p.z)-End,-Underside-p.y)-(p.y>WallTop?Overhang:0);
                    if(past>worst){worst=past;at=p;}
                }
                if(worst>0)outside.Add(skin.name+" shows "+worst.ToString("0.000")+" m past the gondola at "+at.ToString("F2")+" from its middle");
            }
            Object.Destroy(mesh);
            return outside;
        }
        // Everything a part of the world is drawn with, in one box.
        private static Bounds Encased(Transform part){var renderers=part.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
    }
}
