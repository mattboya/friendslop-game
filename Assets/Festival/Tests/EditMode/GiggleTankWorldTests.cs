using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Tests
{
    // GAS-2: a Giggle Tank stand-in, built from primitives, stands on the festival grounds wherever a living player's view has a
    // tank, with room all round it to grab it, and goes once someone has. It collides with nothing, and a spirit's view has none.
    public sealed class GiggleTankWorldTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        [Test]public void ThePlaceholderIsATankThatNeverCollides()
        {
            var parent=Made("Tank parent").transform;var tank=FestivalGiggleTank.Build(parent);
            Assert.That(tank.parent,Is.EqualTo(parent),"it is built where it is asked for");
            var renderers=tank.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length,Is.GreaterThanOrEqualTo(4),"a tank, its valve and a glow to find it by");
            Assert.That(System.Array.Exists(renderers,r=>r.name.StartsWith("GiggleTank",System.StringComparison.Ordinal)),Is.True,"the tank is ART-1's model");
            foreach(var renderer in renderers)Assert.That(renderer.sharedMaterial,Is.Not.Null,renderer.name+" has a material");
            foreach(var renderer in renderers)Assert.That(renderer.shadowCastingMode,Is.EqualTo(ShadowCastingMode.On),renderer.name+" casts a shadow, as the primitive tank did");
            var bounds=Bounds(tank);
            Assert.That(bounds.min.y,Is.EqualTo(0).Within(.01f),"it stands on the ground");
            Assert.That(bounds.max.y,Is.InRange(.9f,1.6f),"about waist high");
            Assert.That(new Vector2(bounds.center.x,bounds.center.z).magnitude,Is.LessThan(.01f),"centred on its spot");
            Assert.That(tank.GetComponentsInChildren<Collider>(true),Is.Empty,"it never blocks a step, a sight line or the navmesh");
        }

        [Test]public void TheWorldShowsTheTankWhereTheViewHasOneWithRoomToGrabIt()
        {
            var root=Made("Tank world");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");Physics.SyncTransforms();
            var grounds=root.transform.Find(FestivalWorld.RootName);
            var walls=new List<Collider>();foreach(var collider in grounds.GetComponentsInChildren<Collider>())if(collider.enabled&&collider.name!="Ground")walls.Add(collider);
            Assert.That(walls.Count,Is.GreaterThan(20),"setup: the grounds' walls, booths and props collide");
            var tank=grounds.Find(FestivalGiggleTank.Name);
            Assert.That(tank,Is.Not.Null,"the tank is built on the festival grounds");
            var state=new RoundState{Phase="Playing"};world.SetTwists(state);
            Assert.That(tank.gameObject.activeInHierarchy,Is.False,"no tank this level, none shown");
            for(int i=0;i<Festivals.GiggleTankSpots.Length;i++)
            {
                var spot=Festivals.GiggleTankSpots[i];string where="spot "+i+" ("+spot.X+", "+spot.Z+")";
                state.GiggleTankSpot=i;world.SetTwists(state);
                Assert.That(tank.gameObject.activeInHierarchy,Is.True,where+": the tank shows");
                Assert.That(Vector2.Distance(new Vector2(tank.position.x,tank.position.z),new Vector2(spot.X,spot.Z)),Is.LessThan(1e-3f),where+": it stands where the rules have it");
                // Nothing solid within its reach, so it can be grabbed from any side.
                var at=new Vector3(spot.X,1,spot.Z);
                foreach(var wall in walls)Assert.That(Vector3.Distance(wall.ClosestPoint(at),at),Is.GreaterThan(Festivals.GiggleTankReach),where+" is hemmed in by "+wall.name);
            }
            state.GiggleTankFound=true;world.SetTwists(state);
            Assert.That(tank.gameObject.activeInHierarchy,Is.False,"once grabbed it is gone");
            state.GiggleTankFound=false;state.GiggleTankSpot=0;world.SetTwists(state);world.SetPhase("Shopping");
            Assert.That(tank.gameObject.activeInHierarchy,Is.False,"camp hides it with the rest of the festival");
        }

        [Test]public void LivingPlayersSeeTheTankAndSpiritsDoNot()
        {
            var game=new FestivalSimulation(5);game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");
            game.State.Phase="Playing";game.State.GiggleTankSpot=2;
            var living=Wire(game,"p0");
            Assert.That(living.GiggleTankSpot,Is.EqualTo(2),"a living player's view carries the level's tank");
            Assert.That(FestivalSimulation.GiggleTankAt(living),Is.Not.Null,"so their client shows it, and offers it within reach");
            game.Player("p1").Life="Spirit";
            Assert.That(FestivalSimulation.GiggleTankAt(Wire(game,"p1")),Is.Null,"a spirit's view carries none");
            game.State.GiggleTankFound=true;
            var after=Wire(game,"p0");
            Assert.That(after.GiggleTankFound&&FestivalSimulation.GiggleTankAt(after)==null,Is.True,"once grabbed it is gone from every view");
            Assert.That(JsonUtility.FromJson<RoundState>("{\"Phase\":\"Playing\",\"StashCash\":5}").GiggleTankSpot,Is.EqualTo(-1),"a snapshot from before tanks reads as none");
        }

        // What viewer's client receives, through the wire format.
        private static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
        private static Bounds Bounds(Transform part){var renderers=part.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
    }
}
