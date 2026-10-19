using System;
using System.Collections.Generic;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // Each part is a paint the looks swap by its __P suffix, so a smoother model must keep the same part set
    // or the looks, the hue shift, the shimmer and the shadows break.
    public sealed class CreatureModelTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        [Test]public void EachKindIsASmoothModelWithTheSamePaintedParts()
        {
            var parent=new GameObject("Models");made.Add(parent);
            foreach(var (model,paints) in new[]{("FestivalCreatureGnome","0 1 2 3 8 10"),("FestivalCreaturePixie","2 4 5 10"),("FestivalCreatureDragon","1 3 6 7 10"),
                ("FestivalCreatureMushroomSprite","1 3 10"),("FestivalCreatureJackalope","3 8 9 10")})
            {
                var go=FestivalArtView.Create(parent.transform,model,true);
                Assert.That(go,Is.Not.Null,"setup: "+model+" loads");
                long triangles=0;var parts=new List<int>();
                foreach(var part in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh=part.sharedMesh;for(int sub=0;sub<mesh.subMeshCount;sub++)triangles+=mesh.GetIndexCount(sub)/3;
                    parts.Add(int.Parse(part.name.Substring(part.name.LastIndexOf("__P",StringComparison.Ordinal)+3).Split('.')[0]));
                }
                parts.Sort();
                Assert.That(triangles,Is.InRange(2000,4000),model+" is a smooth model of 2,000-4,000 triangles");
                Assert.That(string.Join(" ",parts),Is.EqualTo(paints),model+" keeps one part per paint its looks swap");
            }
        }
    }
}
