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
                actor.Pose="Poi";yield return new WaitForSeconds(.28f);
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
                    Assert.That(rope.GetPosition(0).y,Is.GreaterThan(1.1f*actor.transform.lossyScale.y),
                        "The poi grip should sit at upper-chest height so the high arc reaches the face");
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
        [UnityTest]public IEnumerator PoiButterflyHeadsRiseTogetherAndMirrorAcrossTheBody()
        {
            var root=new GameObject("Poi butterfly test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Butterfly poi performer",Color.white);
                actor.Pose="Poi";yield return new WaitForSeconds(.3f);
                var rigs=actor.GetComponentsInChildren<FestivalPoiRig>();
                Assert.That(rigs.Length,Is.EqualTo(2));
                var first=rigs[0].GetComponent<LineRenderer>();
                var second=rigs[1].GetComponent<LineRenderer>();
                int matchingVertical=0,mirroredHorizontal=0,samples=0;
                float highest=-1,lowest=1;
                float start=Time.time;
                while(Time.time-start<.8f)
                {
                    yield return null;
                    Vector3 a=(first.GetPosition(1)-first.GetPosition(0)).normalized;
                    Vector3 b=(second.GetPosition(1)-second.GetPosition(0)).normalized;
                    float height=(a.y+b.y)*.5f;
                    highest=Mathf.Max(highest,height);lowest=Mathf.Min(lowest,height);
                    if(a.y*b.y>.3f)matchingVertical++;
                    if(Vector3.Dot(a,actor.transform.right)*Vector3.Dot(b,actor.transform.right)<-.3f)mirroredHorizontal++;
                    Assert.That(Vector3.Distance(first.GetPosition(1),second.GetPosition(1)),
                        Is.GreaterThan(.16f*actor.transform.lossyScale.y),"Weighted heads should not collide");
                    samples++;
                }
                Assert.That(highest,Is.GreaterThan(.6f));Assert.That(lowest,Is.LessThan(-.6f));
                Assert.That(matchingVertical,Is.GreaterThan(samples*.4f),"Butterfly poi should rise and fall together");
                Assert.That(mirroredHorizontal,Is.GreaterThan(samples*.25f),"The heads should mirror across the performer");
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator FirstPersonPoiHeadsSpinWithCameraHands()
        {
            var root=new GameObject("First-person poi test");
            try
            {
                var rig=FestivalPoiRig.Create(root.transform,new Vector3(.18f,-.35f,.82f),1,true,true);
                rig.Spinning=true;
                var cord=rig.GetComponent<LineRenderer>();
                float low=1,high=-1,start=Time.time;
                while(Time.time-start<.9f)
                {
                    yield return null;
                    float vertical=(cord.GetPosition(1)-cord.GetPosition(0)).normalized.y;
                    low=Mathf.Min(low,vertical);high=Mathf.Max(high,vertical);
                }
                Assert.That(low,Is.LessThan(-.5f));Assert.That(high,Is.GreaterThan(.5f));
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
        [UnityTest]public IEnumerator DanceAlternatesLiftedFeetWithoutMovingTheActor()
        {
            var root=new GameObject("Dance support test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Dance support performer",Color.white);
                var left=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="FootL");
                var right=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="FootR");
                yield return null;
                float floor=Mathf.Min(left.position.y,right.position.y);
                var authorityPosition=actor.transform.position;
                actor.Pose="Dance";
                float leftLift=0,rightLift=0,start=Time.time;
                int support=0,samples=0;
                while(Time.time-start<1f)
                {
                    yield return null;
                    leftLift=Mathf.Max(leftLift,left.position.y-floor);
                    rightLift=Mathf.Max(rightLift,right.position.y-floor);
                    if(Mathf.Min(left.position.y,right.position.y)<floor+.065f)support++;
                    samples++;
                }
                Assert.That(leftLift,Is.GreaterThan(.045f));
                Assert.That(rightLift,Is.GreaterThan(.045f));
                Assert.That(support,Is.GreaterThan(samples*.6f),"Dance should retain a supporting foot");
                Assert.That(actor.transform.position,Is.EqualTo(authorityPosition));
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator ResidentDjHandsStayOnTheConsole()
        {
            var root=new GameObject("DJ contact test");
            try
            {
                var console=new GameObject("DJ console");console.transform.SetParent(root.transform,false);
                console.transform.position=new Vector3(0,1.62f,30.15f);
                var actor=FestivalCharacter.Create(root.transform,"resident_stage_dj",Color.white);
                actor.Pose="Dj";actor.DjConsole=console.transform;
                actor.transform.position=new Vector3(0,1.48f,30.84f);
                actor.transform.rotation=Quaternion.Euler(0,180,0);
                actor.transform.localScale=actor.ShapeScale;
                var left=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="HandL");
                var right=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="HandR");
                Assert.That(left,Is.Not.Null);Assert.That(right,Is.Not.Null);
                yield return new WaitForSeconds(.25f);
                var authorityPosition=actor.transform.position;
                var leftDeck=console.transform.TransformPoint(new Vector3(-.46f,.81f,.36f));
                var rightDeck=console.transform.TransformPoint(new Vector3(.38f,.81f,.33f));
                var arm=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="ArmL");
                var elbow=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="ForearmL");
                Debug.Log("[Festival.Test] dj reach shoulder="+arm.position.ToString("F3")+
                    " elbow="+elbow.position.ToString("F3")+" hand="+left.position.ToString("F3")+
                    " target="+leftDeck.ToString("F3")+" upper="+Vector3.Distance(arm.position,elbow.position).ToString("F3")+
                    " lower="+Vector3.Distance(elbow.position,left.position).ToString("F3"));
                Assert.That(Vector3.Distance(left.position,leftDeck),Is.LessThan(.13f),"Left hand misses the mixer");
                Assert.That(Vector3.Distance(right.position,rightDeck),Is.LessThan(.13f),"Right hand misses the mixer");
                Assert.That(actor.transform.position,Is.EqualTo(authorityPosition));
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
        [UnityTest]public IEnumerator StoppingAndTurningStepsAroundAPlantedFoot()
        {
            var root=new GameObject("Locomotion transition test");
            int previousFrameRate=Application.targetFrameRate;
            Application.targetFrameRate=60;
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Turning contact actor",Color.white);
                actor.transform.localScale=Vector3.one*.82f;
                var left=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="FootL");
                var right=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="FootR");
                Assert.That(left,Is.Not.Null);Assert.That(right,Is.Not.Null);
                yield return null;
                float start=Time.time;
                while(Time.time-start<.65f)
                {
                    actor.transform.position+=Vector3.forward*(1.6f*Time.deltaTime);
                    yield return null;
                }
                yield return new WaitForSeconds(.45f);
                float floorL=left.position.y,floorR=right.position.y;
                var fixedRoot=actor.transform.position;
                float lift=0,turnStart=Time.time;
                int supportFrames=0,samples=0;
                while(Time.time-turnStart<.72f)
                {
                    actor.transform.rotation=Quaternion.Euler(0,
                        Mathf.Min(90f,(Time.time-turnStart)/.52f*90f),0);
                    yield return null;
                    lift=Mathf.Max(lift,left.position.y-floorL,right.position.y-floorR);
                    if(left.position.y<floorL+.055f||right.position.y<floorR+.055f)supportFrames++;
                    samples++;
                }
                Assert.That(Vector3.Distance(actor.transform.position,fixedRoot),Is.LessThan(.001f));
                Assert.That(lift,Is.GreaterThan(.035f),"The stationary turn never lifts a repositioning foot");
                Assert.That(supportFrames,Is.GreaterThan(samples*.7f),"The turn loses both supporting feet");
            }
            finally{Application.targetFrameRate=previousFrameRate;Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator HeldBagGripTouchesHandleAndIgnoresImportedBoneScale()
        {
            var parent=new GameObject("Imported hand scale");parent.transform.localScale=Vector3.one*100;parent.layer=31;
            try
            {
                var bag=FestivalHeldItem.Create(parent.transform,"merch_bag",new Vector3(0,-.05f,0),false);
                yield return null;
                Assert.That(Vector3.Distance(bag.transform.position,new Vector3(0,-.05f,0)),Is.LessThan(.001f));
                foreach(var child in bag.GetComponentsInChildren<Transform>())Assert.That(child.gameObject.layer,Is.EqualTo(31),"Hidden local-world equipment must not leak into first person");
                var handle=System.Array.Find(bag.GetComponentsInChildren<Renderer>(),r=>r.name.EndsWith("__Gold"));
                Assert.That(handle,Is.Not.Null);
                var handleTop=new Vector3(handle.bounds.center.x,handle.bounds.max.y,handle.bounds.center.z);
                Assert.That(Vector3.Distance(handleTop,bag.transform.position),Is.LessThan(.025f),"The palm must meet the top of the handle");
                var body=bag.GetComponentsInChildren<Renderer>();
                var bounds=body[0].bounds;foreach(var renderer in body)bounds.Encapsulate(renderer.bounds);
                Assert.That(bounds.size.y,Is.InRange(.4f,.65f),"Bag size must be independent of imported bone scale");
                Assert.That(bounds.center.y,Is.LessThan(bag.transform.position.y-.15f),"Bag body must hang below its handle");
            }
            finally{Object.Destroy(parent);}
        }
        [UnityTest]public IEnumerator FirstPersonPropTracksHandMotionAndVisibility()
        {
            var root=new GameObject("Hand grip motion camera");
            try
            {
                var hands=FestivalHands.Create(root.AddComponent<Camera>(),"grip_motion");
                hands.SetState(new Festival.Core.PlayerState{EquippedItemId="merch_bag",Life="Alive"});
                yield return null;
                var prop=root.transform.Find("First-person hand attachments/Held grip merch_bag");
                Assert.That(prop,Is.Not.Null);
                yield return new WaitForSeconds(.9f);
                var localContact=hands.transform.InverseTransformPoint(prop.position);
                yield return new WaitForSeconds(.2f);
                Assert.That(Vector3.Distance(localContact,hands.transform.InverseTransformPoint(prop.position)),Is.LessThan(.001f));
                hands.gameObject.SetActive(false);yield return null;
                Assert.That(prop.gameObject.activeInHierarchy,Is.False);
                hands.gameObject.SetActive(true);yield return null;
                Assert.That(prop.gameObject.activeInHierarchy,Is.True);
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator EmptyArmsLowerAndUnusedArmStaysRelaxed()
        {
            var root=new GameObject("Arm rest camera");
            try
            {
                var hands=FestivalHands.Create(root.AddComponent<Camera>(),"arm_rest");
                yield return new WaitForSeconds(.8f);
                foreach(var renderer in hands.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!renderer.enabled)continue;
                    int left=-1,right=-1;
                    for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++)
                    {
                        var key=renderer.sharedMesh.GetBlendShapeName(i);
                        if(key.EndsWith("RestL"))left=i;
                        if(key.EndsWith("RestR"))right=i;
                    }
                    Assert.That(left,Is.GreaterThanOrEqualTo(0),renderer.name);
                    Assert.That(right,Is.GreaterThanOrEqualTo(0),renderer.name);
                    Assert.That(renderer.GetBlendShapeWeight(left),Is.GreaterThan(99));
                    Assert.That(renderer.GetBlendShapeWeight(right),Is.GreaterThan(99));
                    hands.SetState(new Festival.Core.PlayerState{EquippedItemId="merch_bag",Life="Alive"});
                    yield return new WaitForSeconds(.8f);
                    Assert.That(renderer.GetBlendShapeWeight(left),Is.GreaterThan(99),"Unused arm stays down");
                    Assert.That(renderer.GetBlendShapeWeight(right),Is.LessThan(1),"Carrying arm raises");
                    hands.SetState(null);yield return new WaitForSeconds(.8f);
                    Assert.That(renderer.GetBlendShapeWeight(right),Is.GreaterThan(99),"Arm lowers after release");
                }
                var cameraPosition=root.transform.position;
                root.transform.rotation=Quaternion.Euler(0,45,0);yield return null;
                Assert.That(root.transform.position,Is.EqualTo(cameraPosition),"Arm sway never moves camera");
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]public IEnumerator FirstPersonFingersDeformForEquipmentAndRelease()
        {
            var root=new GameObject("Finger deformation camera");
            var baked=new Mesh();
            try
            {
                var hands=FestivalHands.Create(root.AddComponent<Camera>(),"grip_deformation");
                yield return null;
                var skin=System.Array.Find(hands.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.enabled);
                Assert.That(skin,Is.Not.Null,"Hands must import with usable shape keys");
                int tin=-1,paper=-1;
                for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                {
                    string name=skin.sharedMesh.GetBlendShapeName(i);
                    if(name.EndsWith("TinR"))tin=i;
                    if(name.EndsWith("PaperR"))paper=i;
                }
                Assert.That(tin,Is.GreaterThanOrEqualTo(0));Assert.That(paper,Is.GreaterThanOrEqualTo(0));
                hands.SetState(new Festival.Core.PlayerState{EquippedItemId="stock_lsd",Life="Alive"});
                yield return new WaitForSeconds(.35f);
                Assert.That(skin.GetBlendShapeWeight(tin),Is.GreaterThan(98));
                // Compare the two shapes within one frame so breathing and imported
                // skin-root transforms cannot contaminate the deformation measurement.
                float weight=skin.GetBlendShapeWeight(tin);
                skin.SetBlendShapeWeight(tin,0);skin.BakeMesh(baked);var relaxed=baked.vertices;
                skin.SetBlendShapeWeight(tin,weight);skin.BakeMesh(baked);var wrapped=baked.vertices;
                float movement=0,emptyHandMovement=0;
                for(int i=0;i<wrapped.Length;i++)
                {
                    float delta=skin.transform.TransformVector(wrapped[i]-relaxed[i]).magnitude;
                    if(root.transform.InverseTransformPoint(skin.transform.TransformPoint(relaxed[i])).x>0)movement=Mathf.Max(movement,delta);
                    else emptyHandMovement=Mathf.Max(emptyHandMovement,delta);
                }
                Assert.That(emptyHandMovement,Is.LessThan(.001f),"Equipped right-hand gear must not deform the empty left hand");
                Assert.That(movement,Is.GreaterThan(.03f),"Imported fingers must actually move around the object");
                hands.SetState(new Festival.Core.PlayerState{EquippedItemId="stock_lsd",HeldOfferId="map",Life="Alive"});
                yield return new WaitForSeconds(.35f);
                Assert.That(skin.GetBlendShapeWeight(paper),Is.GreaterThan(98));
                Assert.That(skin.GetBlendShapeWeight(tin),Is.LessThan(2),"Unpaid carried paper takes precedence over equipped tin");
                hands.SetState(null);yield return new WaitForSeconds(.35f);
                Assert.That(skin.GetBlendShapeWeight(paper),Is.LessThan(2));
            }
            finally{Object.Destroy(baked);Object.Destroy(root);}
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
                    var kit=room.Find(site.Kind=="Car"?"FestivalCarInterior":site.Kind=="Tent"?"FestivalTentInterior":"FestivalPottyInterior");
                    Assert.That(kit,Is.Not.Null,site.Id+" interior kit missing");
                    var gag=System.Array.Find(kit.GetComponentsInChildren<Transform>(true),t=>t.name==(site.Kind=="Car"?"BobbleHead__Gold":site.Kind=="Tent"?"CoolerLid__Cream":"PottyLid__Cream"));
                    Assert.That(gag,Is.Not.Null,site.Id+" gag prop missing");
                    // The bobblehead sits on the passenger dash, the cooler and toilet at the rear wall; a flipped FBX axis moves them.
                    var expected=site.Kind=="Car"?new Vector3(1.55f,1.80f,1.75f):site.Kind=="Tent"?new Vector3(0,.72f,2.10f):new Vector3(0,1.15f,2.13f);
                    Assert.That(Vector3.Distance(kit.InverseTransformPoint(gag.position),expected),Is.LessThan(.05f),site.Id+" interior kit is mirrored or rotated");
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
                Assert.That(world.PlayerDjConsole,Is.Not.Null,"The player takeover needs a physical console");
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
                var takeover=FestivalCharacter.Create(generated,"Test player DJ",Color.white);
                takeover.Pose="Dj";takeover.DjConsole=world.PlayerDjConsole;
                takeover.transform.position=new Vector3(0,0,26);
                takeover.transform.localScale=Vector3.Scale(Vector3.one*.82f,takeover.ShapeScale);
                yield return new WaitForSeconds(.25f);
                var handL=System.Array.Find(takeover.GetComponentsInChildren<Transform>(),t=>t.name=="HandL");
                var handR=System.Array.Find(takeover.GetComponentsInChildren<Transform>(),t=>t.name=="HandR");
                Assert.That(Vector3.Distance(handL.position,world.PlayerDjConsole.TransformPoint(new Vector3(-.46f,.81f,.36f))),Is.LessThan(.17f),"Left hand misses the takeover mixer");
                Assert.That(Vector3.Distance(handR.position,world.PlayerDjConsole.TransformPoint(new Vector3(.38f,.81f,.33f))),Is.LessThan(.17f),"Right hand misses the takeover mixer");
                Object.Destroy(takeover.gameObject);
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
                Assert.That(camp.Find("FestivalTrailhead"),Is.Not.Null);
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
    }
}
