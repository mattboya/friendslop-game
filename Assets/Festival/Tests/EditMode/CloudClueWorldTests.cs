using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // TRIP-4: a read clue cloud leaves a cash stash beside a landmark, with room to stand at it among the festival's walls. Every
    // view has the clue cloud's window, as everyone sees the cloud, but only the tripper's holds the landmark, and only once they
    // have read it. Everyone sees a player lying on the grass lying there.
    public sealed class CloudClueWorldTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        // Edit mode sends a world no OnDestroy, so call it as play mode and builds do: it destroys the meshes, materials and textures
        // the world made, which would otherwise outlive the test.
        [TearDown]public void Cleanup()
        {
            foreach(var go in made)if(go!=null)
            {
                if(go.TryGetComponent<FestivalWorld>(out var world))typeof(FestivalWorld).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(world,null);
                Object.DestroyImmediate(go);
            }
            made.Clear();
        }

        // A player stands within a metre of the spot, all round, with nothing solid there: the stash is never inside a booth, a wall
        // or a prop, and walking up to it is never blocked at the last step.
        [Test]public void EveryStashSpotHasRoomToStandAtIt()
        {
            var root=Made("Cloud clue world");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");Physics.SyncTransforms();
            var grounds=root.transform.Find(FestivalWorld.RootName);
            var walls=new List<Collider>();foreach(var collider in grounds.GetComponentsInChildren<Collider>())if(collider.enabled&&collider.name!="Ground")walls.Add(collider);
            Assert.That(walls.Count,Is.GreaterThan(20),"setup: the grounds' walls, booths and props collide");
            foreach(var landmark in CloudShapes.Landmarks)
            {
                string where=landmark.Name+"'s stash ("+landmark.Stash.X+", "+landmark.Stash.Z+")";
                foreach(float height in new[]{.3f,1f,1.6f})
                {
                    var at=new Vector3(landmark.Stash.X,height,landmark.Stash.Z);
                    foreach(var wall in walls)Assert.That(Vector3.Distance(wall.ClosestPoint(at),at),Is.GreaterThan(1f),where+" is hemmed in by "+wall.name+" at "+height+" m up");
                }
            }
        }

        [Test]public void OnlyTheTrippersViewHoldsTheLandmarkAndOnlyOnceRead()
        {
            var game=new FestivalSimulation(5);foreach(var id in new[]{"trip","sober","ghost","held"})game.AddPlayer(id,id);
            var s=game.State;s.Phase="Playing";s.TripperId="trip";s.CloudClueStart=75;s.CloudClueLandmark=3;s.ElapsedSeconds=80;
            game.Player("ghost").Life="Spirit";game.Player("held").Life="Detained";
            foreach(var id in new[]{"trip","sober","ghost","held"})
            {
                var view=Wire(game,id);
                Assert.That(view.CloudClueStart,Is.EqualTo(75),id+"'s view has the clue cloud's window: everyone sees the cloud");
                Assert.That(view.CloudClueLandmark,Is.EqualTo(-1),id+"'s view holds no landmark before the read");
                Assert.That(view.CloudClueReadAt,Is.EqualTo(-1),id+"'s view holds no read before it");
                Assert.That(FestivalSimulation.CloudStashAt(view),Is.Null,id+"'s view has no stash to show");
            }
            s.CloudClueReadAt=42.5;
            var tripper=Wire(game,"trip");
            Assert.That(tripper.CloudClueLandmark,Is.EqualTo(3),"the tripper's view holds the landmark they read");
            Assert.That(tripper.CloudClueReadAt,Is.EqualTo(42.5),"and when they read it, so their sky can draw the picture coming");
            foreach(var id in new[]{"sober","ghost","held"})
            {
                var view=Wire(game,id);
                Assert.That(view.CloudClueLandmark,Is.EqualTo(-1),id+"'s view never holds the landmark");
                Assert.That(view.CloudClueReadAt,Is.EqualTo(-1),id+"'s view can't tell it was read");
                Assert.That(view.CloudStashFound,Is.False,id+"'s view says nothing of the stash");
            }
            s.TripperId="sober";
            Assert.That(Wire(game,"trip").CloudClueLandmark,Is.EqualTo(-1),"a tripper replaced by a stand-in no longer holds it");
            var old=JsonUtility.FromJson<RoundState>("{\"Phase\":\"Playing\",\"StashCash\":5}");
            Assert.That(old.CloudClueStart==-1&&old.CloudClueLandmark==-1&&old.CloudClueReadAt==-1&&!old.CloudStashFound,Is.True,"a snapshot from before clue clouds reads as none");
        }

        [Test]public void EveryoneSeesAPlayerLyingOnTheGrassLyingThere()
        {
            var game=new FestivalSimulation(6);game.AddPlayer("lying","Lee");game.AddPlayer("friend","Fay");
            game.State.Phase="Playing";var lee=game.Player("lying");lee.X=3;lee.Z=4;
            Assert.That(game.Execute("lying",new GameCommand{Id="lie",Kind=FestivalSimulation.LieDownKind}).Accepted,Is.True,"setup: Lee lies down");
            foreach(var viewer in new[]{"lying","friend"})
                Assert.That(Wire(game,viewer).Players.Find(p=>p.Id=="lying").VisualPose,Is.EqualTo(FestivalSimulation.LieDownKind),viewer+"'s view shows Lee lying down");
            Assert.That(FestivalSimulation.LyingDown(Wire(game,"lying"),"lying"),Is.True,"Lee's own view knows they are lying down, so their camera looks up");
            Assert.That(FestivalSimulation.LyingDown(Wire(game,"friend"),"lying"),Is.False,"a friend's view carries only its own player's interactions");
        }

        // What viewer's client receives, through the wire format.
        private static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
    }
}
