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
                    Assert.That(Vector3.Distance(rope.GetPosition(0),rope.GetPosition(1)),Is.InRange(.58f,.66f));
                    Assert.That(Vector3.Distance(actor.transform.position,rope.GetPosition(0)),Is.LessThan(2.5f),
                        "The imported bone scale must not move the grip away from the actor");
                    foreach(var renderer in prop.GetComponentsInChildren<MeshRenderer>())
                        Assert.That(renderer.bounds.size.magnitude,Is.LessThan(2f),renderer.name+" is oversized");
                }
                actor.Pose="Idle";yield return null;
                foreach(var prop in props)Assert.That(prop.gameObject.activeSelf,Is.False);
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator PoiHeadsCompleteVerticalCirclesWithTautRopes()
        {
            var root=new GameObject("Poi orbit test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Poi orbit performer",Color.white);
                actor.Pose="Poi";yield return null;
                var rig=actor.GetComponentInChildren<FestivalPoiRig>();
                Assert.That(rig,Is.Not.Null);
                var rope=rig.GetComponent<LineRenderer>();
                float minVertical=1,maxVertical=-1,minHorizontal=1,maxHorizontal=-1;
                float start=Time.time;
                while(Time.time-start<.9f)
                {
                    yield return null;
                    Vector3 offset=(rope.GetPosition(1)-rope.GetPosition(0)).normalized;
                    float vertical=Vector3.Dot(offset,actor.transform.up);
                    float horizontal=Vector3.Dot(offset,actor.transform.right);
                    minVertical=Mathf.Min(minVertical,vertical);maxVertical=Mathf.Max(maxVertical,vertical);
                    minHorizontal=Mathf.Min(minHorizontal,horizontal);maxHorizontal=Mathf.Max(maxHorizontal,horizontal);
                    Assert.That(Vector3.Distance(rope.GetPosition(0),rope.GetPosition(1)),Is.InRange(.58f,.66f));
                }
                Assert.That(minVertical,Is.LessThan(-.6f));Assert.That(maxVertical,Is.GreaterThan(.6f));
                Assert.That(minHorizontal,Is.LessThan(-.6f));Assert.That(maxHorizontal,Is.GreaterThan(.6f));
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator DanceMovesShinsAsWellAsArmsWithoutMovingGameplayRoot()
        {
            var root=new GameObject("Dance footwork test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Dance footwork performer",Color.white);
                actor.Pose="Dance";
                var shin=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="ShinL");
                Assert.That(shin,Is.Not.Null);
                yield return null;
                var first=shin.localRotation;var position=actor.transform.position;
                float maximum=0,start=Time.time;
                while(Time.time-start<.35f)
                {
                    yield return null;
                    maximum=Mathf.Max(maximum,Quaternion.Angle(first,shin.localRotation));
                }
                Assert.That(maximum,Is.GreaterThan(4f));
                Assert.That(actor.transform.position,Is.EqualTo(position));
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator WalkingPlantsEachFootThenClearsTheGround()
        {
            var root=new GameObject("Walking contact test");
            int previousFrameRate=Application.targetFrameRate;
            Application.targetFrameRate=60;
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Walking contact actor",Color.white);
                actor.transform.localScale=Vector3.one*.82f;
                var left=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="FootL");
                var right=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="FootR");
                Assert.That(left,Is.Not.Null);Assert.That(right,Is.Not.Null);
                yield return null;
                float floorL=left.position.y,floorR=right.position.y;
                int leftContacts=0,rightContacts=0;
                float leftLift=0,rightLift=0,start=Time.time;
                float bestLeft=100,bestRight=100,maxRootStep=0;
                int samples=0;
                while(Time.time-start<1.2f)
                {
                    var lastRoot=actor.transform.position;
                    var lastLeft=left.position;var lastRight=right.position;
                    actor.transform.position+=Vector3.forward*(1.6f*Time.deltaTime);
                    yield return null;
                    float rootStep=Vector3.Distance(lastRoot,actor.transform.position);
                    maxRootStep=Mathf.Max(maxRootStep,rootStep);
                    if(rootStep>.003f)
                    {
                        bestLeft=Mathf.Min(bestLeft,Vector3.Distance(lastLeft,left.position)/rootStep);
                        bestRight=Mathf.Min(bestRight,Vector3.Distance(lastRight,right.position)/rootStep);
                        if(Vector3.Distance(lastLeft,left.position)<rootStep*.40f&&
                            Mathf.Abs(left.position.y-floorL)<.04f)leftContacts++;
                        if(Vector3.Distance(lastRight,right.position)<rootStep*.40f&&
                            Mathf.Abs(right.position.y-floorR)<.04f)rightContacts++;
                    }
                    leftLift=Mathf.Max(leftLift,left.position.y-floorL);
                    rightLift=Mathf.Max(rightLift,right.position.y-floorR);
                    samples++;
                }
                var hips=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="Hips");
                Debug.Log("[Festival.Test] walking contacts="+leftContacts+"/"+rightContacts+
                    " samples="+samples+" bestRatio="+bestLeft.ToString("F2")+"/"+bestRight.ToString("F2")+
                    " maxRootStep="+maxRootStep.ToString("F3")+" lift="+leftLift.ToString("F3")+
                    "/"+rightLift.ToString("F3")+" footY="+left.position.y.ToString("F3")+
                    "/"+right.position.y.ToString("F3")+" floor="+floorL.ToString("F3")+
                    "/"+floorR.ToString("F3")+" hips="+hips.position.ToString("F3")+
                    " hipsLocal="+hips.localPosition.ToString("F4")+
                    " hipsParentScale="+hips.parent.lossyScale.ToString("F3"));
                Assert.That(leftContacts,Is.GreaterThan(4),"Left sole never carries a planted step");
                Assert.That(rightContacts,Is.GreaterThan(4),"Right sole never carries a planted step");
                Assert.That(leftLift,Is.GreaterThan(.035f),"Left foot never clears the floor");
                Assert.That(rightLift,Is.GreaterThan(.035f),"Right foot never clears the floor");
                var destination=actor.transform.position+Vector3.forward*8;
                actor.transform.position=destination;
                yield return null;
                Assert.That(Vector3.Distance(actor.transform.position,left.position),Is.LessThan(2f));
                Assert.That(Vector3.Distance(actor.transform.position,right.position),Is.LessThan(2f));
            }
            finally{Application.targetFrameRate=previousFrameRate;Object.Destroy(root);}
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
        [UnityTest]public IEnumerator CampInteriorsStayClearOfTheDecorativeWoodlandRise()
        {
            var root=new GameObject("Interior scenery clearance test");
            try
            {
                var world=root.AddComponent<FestivalWorld>();yield return null;
                var camp=root.transform.Find(FestivalWorld.CampRootName);
                var rise=camp.Find("Camp distant woodland rise").GetComponent<Renderer>();
                foreach(var site in Festival.Core.CampFeatures.Sites)
                {
                    world.SetInterior(site.Id);
                    var room=camp.Find(site.Kind+" interior room");
                    var floor=room.Find(site.Kind+" interior floor").GetComponent<Renderer>();
                    Assert.That(rise.bounds.Intersects(floor.bounds),Is.False,site.Id+" floor is hidden by the woodland rise");
                    Assert.That(room.GetComponentsInChildren<Collider>(true),Is.Empty,site.Id+" decoration blocks interior movement");
                }
            }
            finally{Object.Destroy(root);}
            yield return null;
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
                foreach(var name in new[]{"FestivalStage","FestivalDJDeck","FestivalStallSupplies","FestivalStallPerformance","FestivalStallStock","FestivalMedical","FestivalSecurity","FestivalShuttle","FestivalSun","FestivalMoon","FestivalTreeA","FestivalTreeB","FestivalTreeFir","FestivalGroveDetail"})
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
                foreach(var site in Festival.Core.CampFeatures.Sites)
                {
                    var doorApproach=new Vector3(site.X,0,site.Z-3.2f);
                    Assert.That(NavMesh.CalculatePath(Vector3.zero,doorApproach,NavMesh.AllAreas,path),Is.True,site.Id+" has no path query");
                    Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),site.Id+" is unreachable");
                }
                Assert.That(camp.Find("Trailhead crown"),Is.Not.Null);
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
    }
}
