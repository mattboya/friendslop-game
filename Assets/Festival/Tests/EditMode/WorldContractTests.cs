using System.Collections.Generic;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class WorldContractTests
    {
        private GameObject root;
        // Every camp secret is a visible prop at the host's point. No prop collides, so the camp navmesh and routes are unchanged.
        [Test]public void EveryCampSecretHasAVisibleNonBlockingPropAtTheAuthoritativePoint()
        {
            root=new GameObject("impl:U65 camp egg placement");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Shopping");
            var secrets=root.transform.Find(FestivalWorld.CampRootName).Find(FestivalCampEggView.Name);
            Assert.That(secrets,Is.Not.Null,"the camp builds its secrets");Assert.That(world.CampEggs,Is.Not.Null);
            var published=new FestivalSimulation(0).State.CampEggs;
            foreach(var egg in CampFeatures.Eggs)
            {
                var prop=secrets.Find(FestivalCampEggView.PropName(egg.Id));Assert.That(prop,Is.Not.Null,"the shipped host interaction "+egg.Id+" has no discoverable prop");
                Assert.That(world.CampEggs.Prop(egg.Id),Is.SameAs(prop));
                var point=CampFeatures.EggPoint(egg,0);
                Assert.That(prop.localPosition.x,Is.EqualTo(point.X));Assert.That(prop.localPosition.z,Is.EqualTo(point.Z));Assert.That(prop.localPosition.y,Is.Zero);
                var renderers=Drawn(prop);Assert.That(renderers,Is.Not.Empty,egg.Id+" draws something");
                foreach(var r in renderers){Assert.That(r.sharedMaterial,Is.Not.Null,egg.Id+" "+r.name+" has a material");Assert.That(r.gameObject.layer,Is.Not.EqualTo(31),egg.Id+" is not on the hidden local-body layer");}
                Assert.That(prop.GetComponentsInChildren<Collider>(true),Is.Empty,"leave the standing point accessible");
                var bounds=Bounds(renderers);Assert.That(bounds.min.y,Is.GreaterThan(-.05f),egg.Id+" stands on or above the ground");
                var aim=FestivalCampEggView.AimPoint(published.Find(e=>e.Id==egg.Id));bounds.Expand(.1f);
                Assert.That(bounds.Contains(aim),Is.True,egg.Id+" aim point "+aim+" is on its rendered prop "+bounds);
                Assert.That(FestivalCampEggView.AimRadius(egg.Id),Is.InRange(.1f,1f),egg.Id+" is aimed at within 1 m");
            }
            Assert.That(secrets.childCount,Is.EqualTo(CampFeatures.Eggs.Length),"one prop for each secret");
        }
        // Each host point (both gnome spots) is inside camp, has standing room on the walkable camp, and stays 1 m clear of every
        // door, path, shelf, seller, trailhead, DJ and Giggle Balloon zone. The button sits on the DJ table and the dust on car_1 on purpose.
        [Test]public void EveryCampSecretStandsInsideCampWithRoomAndClearOfRoutesDoorsShelvesAndBalloons()
        {
            root=new GameObject("impl:U65 camp egg clearance");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Shopping");Physics.SyncTransforms();
            Assert.That(world.CampNavigationReady,Is.True,"setup: the camp navmesh");
            var camp=root.transform.Find(FestivalWorld.CampRootName);
            var colliders=new List<Collider>();foreach(var c in camp.GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger&&!c.name.StartsWith("Camp earth",System.StringComparison.Ordinal))colliders.Add(c);
            Assert.That(colliders.Count,Is.GreaterThan(20),"setup: camp tents, cars, posts and tables collide");
            // Zone centre and the host or HUD reach that offers its action there.
            var zones=new List<(string Name,Vector2 At,float Reach)>{("DJ",new Vector2(CampFeatures.DjX,CampFeatures.DjZ),3),("seller",new Vector2(CampSellerTalk.X,CampSellerTalk.Z),CampSellerTalk.Reach),
                ("trailhead",new Vector2(0,19),3.2f),("balloons",new Vector2(CampFeatures.GiggleBalloonX,CampFeatures.GiggleBalloonZ),CampFeatures.GiggleBalloonReach)};
            foreach(var site in CampFeatures.Sites)zones.Add((site.Id,new Vector2(site.X,site.Z),3.5f));
            for(int i=0;i<4;i++){var shelf=Catalog.ShopPoint(true,i);zones.Add(("shelf "+i,new Vector2(shelf.X,shelf.Z),3.3f));}
            var paths=new List<Bounds>();foreach(var name in new[]{"Camp footpath","Camp shade lane","Trailhead path"})paths.Add(camp.Find(name).GetComponent<Renderer>().bounds);
            paths.Add(Bounds(camp.Find(FestivalGiggleTank.BalloonsName).GetComponentsInChildren<Renderer>()));
            foreach(var shelf in camp.GetComponentsInChildren<Transform>())if(shelf.name=="FestivalCampShelf")paths.Add(Bounds(shelf.GetComponentsInChildren<Renderer>()));
            var points=new List<(string Id,Vector2 At)>();
            foreach(var egg in CampFeatures.Eggs)if(egg.Id!="gnome")points.Add((egg.Id,new Vector2(egg.X,egg.Z)));
            foreach(var spot in CampFeatures.GnomeSpots)points.Add(("gnome",new Vector2(spot.X,spot.Z)));
            var path=new NavMeshPath();
            foreach(var (id,at) in points)
            {
                string where=id+" at "+at;
                Assert.That(Mathf.Abs(at.x)<=CampFeatures.CampHalfWidth-1&&Mathf.Abs(at.y)<=CampFeatures.CampHalfDepth-1,Is.True,where+" is inside camp");
                var standing=new Vector3(at.x,1,at.y);
                foreach(var c in colliders)Assert.That(Vector3.Distance(c.ClosestPoint(standing),standing),Is.GreaterThan(.4f),where+" is blocked by "+c.name);
                Assert.That(NavMesh.SamplePosition(new Vector3(at.x,0,at.y),out var ground,.3f,NavMesh.AllAreas),Is.True,where+" is off the walkable camp");
                Assert.That(NavMesh.CalculatePath(Vector3.zero,ground.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True,where+" cannot be walked to");
                foreach(var zone in zones)
                {
                    if(id=="button"&&zone.Name=="DJ"||id=="wash_me"&&zone.Name=="car_1")continue;
                    Assert.That(Vector2.Distance(at,zone.At),Is.GreaterThanOrEqualTo(zone.Reach+1),where+" is within 1 m of the "+zone.Name+" zone");
                }
                foreach(var b in paths){var near=new Vector2(Mathf.Clamp(at.x,b.min.x,b.max.x),Mathf.Clamp(at.y,b.min.z,b.max.z));Assert.That(Vector2.Distance(near,at),Is.GreaterThanOrEqualTo(1),where+" is within 1 m of a path, shelf or the balloons "+b);}
            }
            foreach(var egg in CampFeatures.Eggs)
            {
                if(egg.Id=="wash_me")continue;
                var drawn=Bounds(Drawn(world.CampEggs.Prop(egg.Id)));
                foreach(var c in colliders)Assert.That(drawn.Intersects(c.bounds),Is.False,egg.Id+" prop stands inside "+c.name);
            }
        }
        // The gnome stands where the host's published visit put it, and moves when a visit picks the other spot.
        [Test]public void TheGnomePropFollowsThePublishedSpotOfEachVisit()
        {
            root=new GameObject("impl:U65 gnome visits");var world=root.AddComponent<FestivalWorld>();world.Build();
            var gnome=world.CampEggs.Prop("gnome");var box=world.CampEggs.Prop("lost_found");var rest=box.localPosition;
            foreach(int seed in new[]{1,0,1})
            {
                var eggs=new FestivalSimulation(seed).State.CampEggs;var spot=eggs.Find(e=>e.Id=="gnome");
                if(new Vector2(gnome.localPosition.x,gnome.localPosition.z)!=new Vector2(spot.X,spot.Z))
                    LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=prop_moved egg=gnome "));
                world.CampEggs.Show(eggs);
                Assert.That(gnome.localPosition,Is.EqualTo(new Vector3(spot.X,0,spot.Z)),"visit "+seed+" shows the gnome at its published spot");
                Assert.That(box.localPosition,Is.EqualTo(rest),"the other props keep their fixed points");
            }
            world.CampEggs.Show(null);world.CampEggs.Show(new List<CampEggState>{null,new CampEggState{Id="not_an_egg",X=3,Z=3}});
            Assert.That(gnome.localPosition,Is.EqualTo(new Vector3(CampFeatures.GnomeSpots[1].X,0,CampFeatures.GnomeSpots[1].Z)),"no records or unknown records move nothing");
        }
        // The editor's asset unload destroys shared art materials that no object holds. A destroyed one is made again, never handed out.
        [Test]public void AnUnloadedArtMaterialIsMadeAgain()
        {
            var rose=FestivalArtView.MaterialFor("Rose");Assert.That(rose!=null,Is.True,"setup: the shared material");
            Object.DestroyImmediate(rose);
            var again=FestivalArtView.MaterialFor("Rose");
            Assert.That(again!=null,Is.True,"a destroyed shared material is not handed out");Assert.That(again.name,Is.EqualTo("Festival art Rose"));
        }
        private static Renderer[] Drawn(Transform prop)=>System.Array.FindAll(prop.GetComponentsInChildren<Renderer>(),r=>r.enabled);
        private static Bounds Bounds(Renderer[] renderers){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
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
