using System.Collections;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;

namespace Festival.Tests
{
    public sealed class WorldLifecycleTests
    {
        [UnityTest]public IEnumerator BodyAnimationPreservesAuthoritativeFacing()
        {
            var root=new GameObject("Character facing test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Test actor",Color.white);
                actor.Pose="Dance";var facing=Quaternion.Euler(0,123,0);actor.transform.rotation=facing;
                yield return null;yield return null;
                Assert.That(Quaternion.Angle(actor.transform.rotation,facing),Is.LessThan(.01f));
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator ImportedBoneScaleDoesNotEnlargeCharacterAccessories()
        {
            var root=new GameObject("Accessory bounds test");
            try
            {
                for(int i=0;i<12;i++)
                {
                    var actor=FestivalCharacter.Create(root.transform,"Festival visitor "+i,Color.white);
                    foreach(var accessory in actor.GetComponentsInChildren<MeshRenderer>())
                    {
                        var bounds=accessory.bounds;
                        Assert.That(bounds.size.magnitude,Is.LessThan(1f),accessory.name+" is oversized");
                        Assert.That(bounds.center.y,Is.InRange(0f,3f),accessory.name+" is away from the head");
                        Assert.That(Mathf.Abs(bounds.center.x),Is.LessThan(1f),accessory.name+" moved sideways");
                        Assert.That(Mathf.Abs(bounds.center.z),Is.LessThan(1f),accessory.name+" moved forward");
                    }
                }
                yield return null;
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator RescuePoseEasesInWithoutMovingTheActorRoot()
        {
            var root=new GameObject("Pose transition test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Rescue pose actor",Color.white);
                var arm=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="ArmL");
                Assert.That(arm,Is.Not.Null);
                yield return null;yield return null;
                var atRest=arm.localRotation;var authorityPosition=actor.transform.position;
                actor.Pose="Rescue";
                yield return null;
                float firstFrame=Quaternion.Angle(atRest,arm.localRotation);
                yield return new WaitForSeconds(.2f);
                float settled=Quaternion.Angle(atRest,arm.localRotation);
                Assert.That(firstFrame,Is.GreaterThan(0));
                Assert.That(settled,Is.GreaterThan(firstFrame+5),"Rescue motion should blend across frames");
                Assert.That(actor.transform.position,Is.EqualTo(authorityPosition),"Visual poses must not move the gameplay actor");
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator PoiPoseKeepsBothGripsAndWeightedHeadsNearHands()
        {
            var root=new GameObject("Poi attachment test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Poi performer",Color.white);
                actor.Pose="Poi";yield return null;yield return null;
                var props=actor.GetComponentsInChildren<FestivalPoiRig>(true);
                Assert.That(props.Length,Is.EqualTo(2));
                foreach(var prop in props)
                {
                    Assert.That(prop.gameObject.activeSelf,Is.True);
                    var rope=prop.GetComponent<LineRenderer>();
                    Assert.That(rope,Is.Not.Null);
                    Assert.That(Vector3.Distance(rope.GetPosition(0),rope.GetPosition(1)),Is.InRange(.35f,.5f));
                    foreach(var renderer in prop.GetComponentsInChildren<MeshRenderer>())
                        Assert.That(renderer.bounds.size.magnitude,Is.LessThan(2f),renderer.name+" is oversized");
                }
                actor.Pose="Idle";yield return null;
                foreach(var prop in props)Assert.That(prop.gameObject.activeSelf,Is.False);
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator FirstPersonHandsSelectTheSameBodyAndSleeveAsPlayer()
        {
            var cameraObject=new GameObject("First-person hand test camera");
            try
            {
                var camera=cameraObject.AddComponent<Camera>();
                const string id="hands_player_7";
                var look=FestivalAppearance.For(id);
                var hands=FestivalHands.Create(camera,id);
                Assert.That(hands,Is.Not.Null);
                yield return null;
                var renderers=System.Array.FindAll(hands.GetComponentsInChildren<Renderer>(true),r=>r.enabled&&r.gameObject.activeInHierarchy);
                Assert.That(System.Array.Exists(renderers,r=>r.name=="HandsSkin_"+look.Shape),Is.True);
                Assert.That(System.Array.Exists(renderers,r=>r.name=="HandsSleeve_"+look.Shirt),Is.True);
                Assert.That(renderers.Length,Is.EqualTo(2));
            }
            finally{Object.Destroy(cameraObject);}
        }
        [UnityTest]public IEnumerator AwakeBuildsWorldAndCrowdWalksWithoutBlockingNavigation()
        {
            var root=new GameObject("Play test world");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;
                var generated=root.transform.Find(FestivalWorld.RootName);Assert.That(generated,Is.Not.Null);
                var camp=root.transform.Find(FestivalWorld.CampRootName);Assert.That(camp,Is.Not.Null);
                Assert.That(world.IsReady,Is.True);Assert.That(world.NavigationReady,Is.True);Assert.That(world.CampNavigationReady,Is.True);
                Assert.That(world.IsCampVisible,Is.True);
                foreach(var name in new[]{"FestivalCampShade","FestivalCampCar","FestivalCampVan","FestivalTent","FestivalDomeTent","FestivalPortaPotty","camp_seller_2"})
                    Assert.That(camp.Find(name),Is.Not.Null,name+" campsite object missing");
                world.SetPhase("Playing");
                Assert.That(world.IsCampVisible,Is.False);
                var path=new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(new Vector3(0,0,-29),new Vector3(24,0,-20),NavMesh.AllAreas,path),Is.True);
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
                var entry=new Vector3(-7,0,-29);
                var market=new Vector3(-18,0,-22);
                var sun=new Vector3(16,0,-4);
                var moon=new Vector3(-16,0,5);
                var stage=new Vector3(0,0,26);
                var shuttle=new Vector3(0,0,-32);
                foreach(var route in new[]{
                    new[]{entry,market},new[]{market,sun},new[]{market,moon},
                    new[]{sun,moon},new[]{moon,sun},new[]{sun,stage},new[]{moon,stage},
                    new[]{stage,new Vector3(-24,0,25)},new[]{stage,new Vector3(25,0,24)},new[]{stage,new Vector3(18,0,5)},
                    new[]{new Vector3(-24,0,25),shuttle},new[]{new Vector3(25,0,24),shuttle},new[]{new Vector3(18,0,5),shuttle}})
                {
                    Assert.That(NavMesh.CalculatePath(route[0],route[1],NavMesh.AllAreas,path),Is.True,"Missing route "+route[0]+" to "+route[1]);
                    Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"Incomplete route "+route[0]+" to "+route[1]);
                }
                // Medical booth blocks the direct route: navigation must turn around it.
                Assert.That(NavMesh.CalculatePath(new Vector3(24,0,-22),new Vector3(24,0,-14),NavMesh.AllAreas,path),Is.True);
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(path.corners.Length,Is.GreaterThan(2));
                int count=generated.childCount;world.Build();Assert.That(generated.childCount,Is.EqualTo(count));
                var crowd=generated.GetComponent<FestivalAmbientCrowd>();Assert.That(crowd,Is.Not.Null);
                Assert.That(crowd.MemberCount,Is.EqualTo(62));
                var performer=generated.Find("ambient_PoiPerformer_26");Assert.That(performer,Is.Not.Null);
                Assert.That(performer.GetComponentsInChildren<Collider>(true),Is.Empty);
                foreach(var name in new[]{"FestivalStage","FestivalDJDeck","FestivalStallSupplies","FestivalStallPerformance","FestivalStallStock","FestivalMedical","FestivalSecurity","FestivalShuttle","FestivalSun","FestivalMoon","FestivalTreeA","FestivalTreeB"})
                    Assert.That(generated.Find(name),Is.Not.Null,name+" Blender visual missing");
                var walkerStart=crowd.FirstWalkerPosition;
                yield return new WaitForSeconds(.15f);
                Assert.That(Vector3.Distance(walkerStart,crowd.FirstWalkerPosition),Is.GreaterThan(.1f));
                world.SetPhase("Shopping");
                Assert.That(world.IsCampVisible,Is.True);
                Assert.That(NavMesh.CalculatePath(new Vector3(-7,0,-7),new Vector3(0,0,7),NavMesh.AllAreas,path),Is.True);
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(NavMesh.CalculatePath(new Vector3(0,0,7),new Vector3(0,0,19),NavMesh.AllAreas,path),Is.True);
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"Seller to ready trailhead must remain walkable");
                Assert.That(camp.Find("Trailhead crown"),Is.Not.Null);
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
    }
}
