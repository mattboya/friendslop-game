using System.Collections.Generic;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // ART-1: Palm Mirage's dressing. Palms stand in for the grounds' trees and the landmarks stand outside the walls, all shown only at
    // Palm Mirage, and none of it collides, so play is exactly what it was.
    public sealed class PoloDressingTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();FestivalCharacter.ViewTransform=null;}

        [Test]public void EveryModelKeepsItsPaletteColours()
        {
            var parent=Made("Models").transform;
            foreach(var model in FestivalPoloDressing.Models)
            {
                var go=FestivalArtView.Create(parent,model);
                Assert.That(go,Is.Not.Null,model+" loads from Resources");
                var renderers=go.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length,Is.GreaterThanOrEqualTo(2),model+" keeps two or more renderers, so Unity doesn't collapse it onto its root");
                foreach(var r in renderers)
                {
                    Assert.That(r.name.Contains("__"),Is.True,model+"/"+r.name+" names its palette colour");
                    Assert.That(r.sharedMaterial.color,Is.Not.EqualTo(Color.white),model+"/"+r.name+" has a known palette colour, not the white fallback");
                }
                Assert.That(go.GetComponentsInChildren<Collider>(true),Is.Empty,model+" never collides");
            }
        }

        [Test]public void PalmMirageSwapsTreesForPalmsBothWaysAndEmberPlayaKeepsItsTrees()
        {
            var world=World();var dressing=Dressing(world);var trees=Trees(world);
            Assert.That(trees.Count,Is.GreaterThan(60),"setup: the grounds have their trees");
            foreach(var festival in new[]{Festivals.PoloFestival,Festivals.PlayaFestival,Festivals.PoloFestival,Festivals.PlayaFestival})
            {
                world.SetTwists(Round(festival));bool polo=festival==Festivals.PoloFestival;
                Assert.That(dressing.gameObject.activeSelf,Is.EqualTo(polo),Festivals.Name(festival)+": the dressing shows only at Palm Mirage");
                foreach(var tree in trees)Assert.That(tree.activeSelf,Is.EqualTo(!polo),Festivals.Name(festival)+": "+tree.name+" hides only at Palm Mirage");
            }
        }

        [Test]public void PalmsStandOnTreeSpotsClearOfTheLandmarks()
        {
            var world=World();var dressing=Dressing(world);var spots=new List<Vector3>();
            foreach(var tree in Trees(world))if(!FestivalPoloDressing.InLandmark(tree.transform.localPosition))spots.Add(tree.transform.localPosition);
            var palms=new List<Transform>();foreach(Transform child in dressing)if(child.name.StartsWith("FestivalPalm"))palms.Add(child);
            Assert.That(palms.Count,Is.EqualTo(spots.Count),"one palm for each tree spot clear of a landmark");
            foreach(var palm in palms)
            {
                Assert.That(spots.Exists(s=>Vector3.Distance(s,palm.localPosition)<1e-3f),Is.True,palm.name+" stands on a tree spot");
                Assert.That(FestivalPoloDressing.InLandmark(palm.localPosition),Is.False,palm.name+" stands clear of the landmarks");
            }
        }

        [Test]public void LandmarksStandOutsideThePlayAreaAndTheCanopyStaysOverhead()
        {
            var world=World();var dressing=Dressing(world);world.SetTwists(Round(Festivals.PoloFestival));
            foreach(var name in new[]{"FestivalRainbowTower","FestivalAstronaut"})
            {
                var b=Bounds(dressing.Find(name));
                Assert.That(b.min.x>40||b.max.x<-40||b.min.z>40||b.max.z<-40,Is.True,name+" stands wholly outside the walls ("+b+")");
            }
            foreach(var r in dressing.Find("FestivalPetalCanopy").GetComponentsInChildren<Renderer>())
            {
                if(!r.name.EndsWith("__Metal"))Assert.That(r.bounds.min.y,Is.GreaterThanOrEqualTo(6f),r.name+" hangs above the crowd's heads");
                else
                {
                    // Both masts merge into one Metal renderer, so its bounds span from one speaker stack to the other.
                    Assert.That(-r.bounds.min.x,Is.InRange(9.5f,10.5f),"the west mast rises from its speaker stack");
                    Assert.That(r.bounds.max.x,Is.InRange(9.5f,10.5f),"the east mast rises from its speaker stack");
                    Assert.That(r.bounds.center.z,Is.InRange(30f,32f),"the masts stand in the speaker stacks' depth");
                }
            }
        }

        [Test]public void TheHorizonFollowsTheViewWithinTheFarClip()
        {
            var world=World();var dressing=Dressing(world);var eye=Made("Eye").transform;FestivalCharacter.ViewTransform=eye;
            foreach(var at in new[]{new Vector3(40,1.6f,40),new Vector3(-40,1.6f,-40),new Vector3(40,1.6f,-40),Vector3.zero})
            {
                eye.position=at;world.SetTwists(Round(Festivals.PoloFestival));
                var ridges=dressing.Find(FestivalPoloDressing.HorizonName).GetComponentsInChildren<Renderer>();
                Assert.That(ridges.Length,Is.GreaterThanOrEqualTo(12),"setup: the horizon has its ridges");
                foreach(var r in ridges)
                    Assert.That(Vector2.Distance(new Vector2(r.bounds.center.x,r.bounds.center.z),new Vector2(at.x,at.z)),Is.LessThan(125f),r.name+" stays inside the 130 m far clip from "+at);
            }
        }

        [Test]public void TheLawnLiesUnderThePaths()
        {
            var world=World();var dressing=Dressing(world);world.SetTwists(Round(Festivals.PoloFestival));
            var lawn=dressing.Find(FestivalPoloDressing.LawnName);
            Assert.That(lawn,Is.Not.Null,"Palm Mirage has its lawn");
            var b=lawn.GetComponent<Renderer>().bounds;
            Assert.That(b.center.y,Is.InRange(.0005f,.0029f),"just over the ground, under the paths (which start at 0.003 m)");
            Assert.That(b.size.x,Is.EqualTo(80).Within(.01f),"it covers the play area");
            Assert.That(lawn.GetComponentsInChildren<Collider>(true),Is.Empty,"it never collides");
        }

        private FestivalWorld World(){var world=Made("Polo world").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");return world;}
        private static Transform Dressing(FestivalWorld world)
        {
            var d=world.transform.Find(FestivalWorld.RootName).Find(FestivalPoloDressing.RootName);
            Assert.That(d,Is.Not.Null,"the grounds carry Palm Mirage's dressing");return d;
        }
        private static List<GameObject> Trees(FestivalWorld world)
        {
            var trees=new List<GameObject>();
            foreach(Transform child in world.transform.Find(FestivalWorld.RootName))if(child.name.StartsWith("FestivalTree"))trees.Add(child.gameObject);
            return trees;
        }
        private static RoundState Round(int festival)=>new RoundState{Phase="Playing",FestivalIndex=festival,LevelIndex=0,DurationSeconds=Festivals.Level(festival,0,0).DurationSeconds};
        private static Bounds Bounds(Transform part){var rs=part.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
    }
}
