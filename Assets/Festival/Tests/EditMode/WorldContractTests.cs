using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class WorldContractTests
    {
        private GameObject root;
        [TearDown]public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);}
        [Test]public void BuildPreservesManualChildrenAndDoesNotDuplicateGeometry()
        {
            root=new GameObject("Test world");var manual=new GameObject("Manual decoration");manual.transform.SetParent(root.transform);
            var world=root.AddComponent<FestivalWorld>();world.Build();int count=root.GetComponentsInChildren<Transform>().Length;
            world.Build();Assert.That(root.GetComponentsInChildren<Transform>().Length,Is.EqualTo(count));Assert.That(manual,Is.Not.Null);
            Assert.That(root.transform.childCount,Is.EqualTo(3));
        }
        [Test]public void WorldHasSolidPerimeterAndNonBlockingCosmeticCrowd()
        {
            root=new GameObject("Test world");root.AddComponent<FestivalWorld>().Build();var generated=root.transform.Find(FestivalWorld.RootName);
            foreach(string side in new[]{"North","South","West","East"})Assert.That(generated.Find(side+" boundary").GetComponent<BoxCollider>().enabled,Is.True);
            var crowd=generated.GetComponent<FestivalAmbientCrowd>();
            Assert.That(crowd,Is.Not.Null);
            Assert.That(crowd.MemberCount,Is.EqualTo(62));
            Assert.That(crowd.WalkerCount,Is.EqualTo(12));
            int count=0;foreach(Transform child in generated)if(child.name.StartsWith("ambient_")){count++;Assert.That(child.GetComponentsInChildren<Collider>(true),Is.Empty);}
            Assert.That(count,Is.EqualTo(crowd.MemberCount));
        }
        [Test]public void InteractionLandmarksHaveStandingRoom()
        {
            root=new GameObject("Test world");var world=root.AddComponent<FestivalWorld>();world.SetPhase("Playing");Physics.SyncTransforms();
            foreach(var point in new[]{new Vector3(-18,1,-22),new Vector3(24,1,-20),new Vector3(27,1,5),new Vector3(-25,1,-8),new Vector3(0,1,-32),new Vector3(-28,1,16),new Vector3(16,1,-4),new Vector3(-16,1,5)})
                foreach(var collider in root.GetComponentsInChildren<Collider>())
                    if(collider.enabled)Assert.That(Vector3.Distance(collider.ClosestPoint(point),point),Is.GreaterThan(.4f),"Blocked interaction landmark "+point);
        }
    }
}
