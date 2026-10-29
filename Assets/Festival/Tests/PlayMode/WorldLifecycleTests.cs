using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;
using TMPro;

namespace Festival.Tests
{
    public sealed class WorldLifecycleTests:FestivalPlayModeTest
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
        // The circular mean of a run of angle differences in degrees, and how far apart its extremes lie.
        static (float Mean,float Spread) Steadiness(List<float> differences)
        {
            float sin=0,cos=0;
            foreach(var d in differences){sin+=Mathf.Sin(d*Mathf.Deg2Rad);cos+=Mathf.Cos(d*Mathf.Deg2Rad);}
            float mean=Mathf.Atan2(sin,cos)*Mathf.Rad2Deg,low=0,high=0;
            foreach(var d in differences){float off=Mathf.DeltaAngle(mean,d);low=Mathf.Min(low,off);high=Mathf.Max(high,off);}
            return (mean,high-low);
        }
        // Where a point sits on its loop, in degrees: across its own sweep (x) and up it (y), each scaled to the sweep's size.
        static float LoopAngle(Vector3 point,Bounds sweep)
        {
            var p=point-sweep.center;
            return Mathf.Atan2(p.x/Mathf.Max(.0001f,sweep.extents.x),p.y/Mathf.Max(.0001f,sweep.extents.y))*Mathf.Rad2Deg;
        }
        // POI-2: Matt saw the heads whirl while the arms never drove them. In a performance each hand swings round a loop of its
        // own, about 15-25 cm across, a steady step ahead of its head, so the arms read as swinging the poi.
        [UnityTest]public IEnumerator PoiPerformanceHandsLeadTheirHeads()
        {
            var root=new GameObject("Poi hands test");Time.captureDeltaTime=1/60f;
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"Poi hands performer",Color.white);
                actor.Pose="Poi";yield return new WaitForSeconds(.5f);
                var rigs=actor.GetComponentsInChildren<FestivalPoiRig>();
                Assert.That(rigs.Length,Is.EqualTo(2));
                var hands=new[]{new List<Vector3>(),new List<Vector3>()};var heads=new[]{new List<float>(),new List<float>()};
                for(float start=Time.time;Time.time-start<2f;)
                {
                    yield return null;
                    for(int i=0;i<2;i++)
                    {
                        // Each rig hangs from the hand bone that holds it.
                        hands[i].Add(actor.transform.InverseTransformPoint(rigs[i].transform.parent.position));
                        var rope=rigs[i].GetComponent<LineRenderer>();var cord=rope.GetPosition(1)-rope.GetPosition(0);
                        heads[i].Add(Mathf.Atan2(Vector3.Dot(cord,actor.transform.right),Vector3.Dot(cord,actor.transform.up))*Mathf.Rad2Deg);
                    }
                }
                for(int i=0;i<2;i++)
                {
                    string hand=rigs[i].transform.parent.name;
                    var sweep=new Bounds(hands[i][0],Vector3.zero);foreach(var p in hands[i])sweep.Encapsulate(p);
                    var leads=new List<float>();float turning=0;
                    for(int k=0;k<hands[i].Count;k++)
                    {
                        leads.Add(Mathf.DeltaAngle(heads[i][k],LoopAngle(hands[i][k],sweep)));
                        if(k>0)turning+=Mathf.DeltaAngle(heads[i][k-1],heads[i][k]);
                    }
                    var (lead,spread)=Steadiness(leads);
                    Debug.Log($"[Festival.Test] {hand} poi hand sweep={sweep.size.x:F3}/{sweep.size.y:F3}/{sweep.size.z:F3} centre={sweep.center} lead={lead*Mathf.Sign(turning):F0}deg spread={spread:F0}deg");
                    int directions=(sweep.size.x>=.12f?1:0)+(sweep.size.y>=.12f?1:0)+(sweep.size.z>=.12f?1:0);
                    Assert.That(directions,Is.GreaterThanOrEqualTo(2),hand+" should swing round a loop, not sit still: "+sweep.size.ToString("F3"));
                    Assert.That(Mathf.Max(sweep.size.x,sweep.size.y,sweep.size.z),Is.LessThan(.3f),hand+" should work its poi close in front, not flail: "+sweep.size.ToString("F3"));
                    Assert.That(spread,Is.LessThan(45f),hand+" should keep a steady step on its own head");
                    // Ahead by under a quarter turn: the hand pulls its head round. Half a turn out, it fights it.
                    Assert.That(lead*Mathf.Sign(turning),Is.InRange(0f,90f),hand+" should lead its head round, not trail or fight it");
                }
            }
            finally{Time.captureDeltaTime=0;Object.Destroy(root);}
        }
        static Vector3[] Baked(SkinnedMeshRenderer skin,Mesh baked)
        {
            skin.BakeMesh(baked,true);var points=baked.vertices;
            for(int i=0;i<points.Length;i++)points[i]=skin.transform.TransformPoint(points[i]);
            return points;
        }
        // POI-2: in first person your own hands swing round with the poi too, each rope still starting at the palm that holds it,
        // and they settle back to rest when the performance ends. POI-3: measured on a new profile's reduced-motion setting, what
        // players actually get.
        [UnityTest]public IEnumerator FirstPersonPoiHandsMoveWithTheSpin()
        {
            var root=new GameObject("First-person poi performance camera");var baked=new Mesh();Time.captureDeltaTime=1/60f;
            try
            {
                var hands=FestivalHands.Create(root.AddComponent<Camera>(),"poi_performance_hands");
                hands.ReducedMotion=new ProfileData().ReducedMotion;
                var rest=hands.transform.localPosition;
                hands.SetState(new Festival.Core.PlayerState{Id="poi_performance_hands",Life="Alive",VisualPose="Poi"});
                yield return new WaitForSeconds(.8f);
                var skin=System.Array.Find(hands.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.enabled&&r.name.StartsWith("HandsSkin_"));
                Assert.That(skin,Is.Not.Null);
                var rigs=root.GetComponentsInChildren<FestivalPoiRig>();
                Assert.That(rigs.Length,Is.EqualTo(2));
                // Left hand's poi first, then the right's.
                System.Array.Sort(rigs,(a,b)=>root.transform.InverseTransformPoint(a.transform.position).x.CompareTo(root.transform.InverseTransformPoint(b.transform.position).x));
                var cords=System.Array.ConvertAll(rigs,r=>r.GetComponent<LineRenderer>());
                // Each rope starts beside a point of the hand holding it, and has to ride along with that point.
                var mesh=Baked(skin,baked);var palms=new int[2];var grips=new Vector3[2];
                for(int i=0;i<2;i++)
                {
                    var start=cords[i].GetPosition(0);float nearest=float.MaxValue;
                    for(int v=0;v<mesh.Length;v++){float d=(mesh[v]-start).sqrMagnitude;if(d<nearest){nearest=d;palms[i]=v;}}
                    grips[i]=start-mesh[palms[i]];
                }
                var sweep=new Bounds(hands.transform.localPosition,Vector3.zero);var heads=new List<float>();var loop=new List<Vector3>();
                float drift=0;
                for(float start=Time.time;Time.time-start<2f;)
                {
                    yield return null;
                    sweep.Encapsulate(hands.transform.localPosition);loop.Add(hands.transform.localPosition);
                    // The camera hands ride one loop together, in step with the right hand's poi.
                    var cord=cords[1].GetPosition(1)-cords[1].GetPosition(0);
                    heads.Add(Mathf.Atan2(Vector3.Dot(cord,root.transform.right),Vector3.Dot(cord,root.transform.up))*Mathf.Rad2Deg);
                    mesh=Baked(skin,baked);
                    for(int i=0;i<2;i++)drift=Mathf.Max(drift,Vector3.Distance(cords[i].GetPosition(0)-mesh[palms[i]],grips[i]));
                }
                var leads=new List<float>();for(int k=0;k<loop.Count;k++)leads.Add(Mathf.DeltaAngle(heads[k],LoopAngle(loop[k],sweep)));
                var (lead,spread)=Steadiness(leads);
                Debug.Log($"[Festival.Test] first-person poi hands sweep={sweep.size.x:F3}/{sweep.size.y:F3}/{sweep.size.z:F3} lead={lead:F0}deg spread={spread:F0}deg grip drift={drift:F4}");
                Assert.That(Mathf.Max(sweep.size.x,sweep.size.y,sweep.size.z),Is.GreaterThanOrEqualTo(.05f),"The hands should swing round with the poi: "+sweep.size.ToString("F3"));
                Assert.That(drift,Is.LessThan(.01f),"Each rope should still start at its palm");
                Assert.That(spread,Is.LessThan(45f),"The hands should keep a steady step on the right hand's poi");
                hands.SetState(new Festival.Core.PlayerState{Id="poi_performance_hands",Life="Alive"});
                yield return new WaitForSeconds(.8f);
                Assert.That(Vector3.Distance(hands.transform.localPosition,rest),Is.LessThan(.01f),"The hands settle back once the performance ends");
                for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                    if(skin.sharedMesh.GetBlendShapeName(i).EndsWith("RestL")||skin.sharedMesh.GetBlendShapeName(i).EndsWith("RestR"))
                        Assert.That(skin.GetBlendShapeWeight(i),Is.GreaterThan(99),"Both arms lower once the performance ends");
            }
            finally{Time.captureDeltaTime=0;Object.Destroy(baked);Object.Destroy(root);}
        }
        // How far the camera hands travel over two seconds, along their widest direction.
        static IEnumerator HandSweep(FestivalHands hands,float[] result)
        {
            var sweep=new Bounds(hands.transform.localPosition,Vector3.zero);
            for(float start=Time.time;Time.time-start<2f;){yield return null;sweep.Encapsulate(hands.transform.localPosition);}
            result[0]=Mathf.Max(sweep.size.x,sweep.size.y,sweep.size.z);
        }
        // POI-2: reduced motion keeps the first-person poi loop but shrinks it. POI-3: never under the 5 cm that reads as hands
        // swinging the poi, or they look still while the heads whirl.
        [UnityTest]public IEnumerator ReducedMotionShrinksTheFirstPersonPoiLoop()
        {
            var root=new GameObject("Reduced-motion poi camera");Time.captureDeltaTime=1/60f;
            try
            {
                var hands=FestivalHands.Create(root.AddComponent<Camera>(),"poi_reduced_hands");
                hands.SetState(new Festival.Core.PlayerState{Id="poi_reduced_hands",Life="Alive",VisualPose="Poi"});
                var full=new float[1];var reduced=new float[1];
                yield return new WaitForSeconds(.8f);yield return HandSweep(hands,full);
                hands.ReducedMotion=true;
                yield return new WaitForSeconds(.8f);yield return HandSweep(hands,reduced);
                Debug.Log($"[Festival.Test] first-person poi loop full={full[0]:F3} reduced={reduced[0]:F3}");
                Assert.That(reduced[0],Is.InRange(full[0]*.6f,full[0]*.9f),"Reduced motion should shrink the hands' loop, not stop it");
                Assert.That(reduced[0],Is.GreaterThanOrEqualTo(.05f),"The reduced loop still visibly swings the poi");
            }
            finally{Time.captureDeltaTime=0;Object.Destroy(root);}
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
        static Dictionary<string,Transform> Actors(FestivalSession s)=>(Dictionary<string,Transform>)typeof(FestivalSession).GetField("actors",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(s);
        // A stage-pass set is played up on the riser at the main mixer: the host's view and a joined friend's both show the
        // DJ there, facing the crowd in the Dj pose, hands on the mixer, with the resident stepped aside. After a perfect phrase the
        // festivalgoers near the stage and the scenery pods in front of it cheer; after a flop none do.
        [UnityTest]public IEnumerator TheStageDjPlaysAtTheMainMixerInEveryViewAndTheCrowdCheersAGoodPhrase()
        {
            var world=new GameObject("Stage DJ world");var built=world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Stage DJ host");var host=hostObject.AddComponent<FestivalSession>();
            var friendObject=new GameObject("Stage DJ friend");var friend=friendObject.AddComponent<FestivalSession>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Host",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);friend.Join("Friend","127.0.0.1",port);
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,friend.Message);
                var sim=Simulation(host);var mate=sim.Player(friend.LocalPlayerId);mate.X=0;mate.Z=19;friend.Command("Ready");
                while(!mate.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(mate.Ready,Is.True,"setup: the friend's Ready reached the host");yield return StartLevel(host,friend.LocalPlayerId);
                var dj=sim.Player(host.LocalPlayerId);mate.X=0;mate.Z=20;sim.State.Npcs.RemoveAll(n=>n.Kind=="Cop");
                dj.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});dj.X=Catalog.StageTakeoverX;dj.Z=Catalog.StageTakeoverZ;
                Assert.That(sim.Execute(dj.Id,new GameCommand{Id="stage_dj",Kind="Dj"}).Accepted,Is.True,"setup: the set starts");
                var set=sim.Interaction(dj.InteractionId);
                yield return new WaitForSeconds(.6f);
                var mixer=built.StageDjConsole;Assert.That(mixer,Is.Not.Null,"setup: the main mixer");
                foreach(var view in new[]{host,friend})
                {
                    string who=view==host?"the host's view":"the friend's view";var actor=Actors(view)[dj.Id];var body=actor.GetComponent<FestivalCharacter>();
                    Assert.That(Vector3.Distance(actor.position,new Vector3(Catalog.StageDjX,FestivalWorld.StageDjHeight,Catalog.StageDjZ)),Is.LessThan(.05f),who+": the DJ stands on the riser at the mixer, at "+actor.position);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(actor.eulerAngles.y,180)),Is.LessThan(1),who+": facing the crowd");
                    Assert.That(body.Pose,Is.EqualTo("Dj"),who+": in the Dj pose");Assert.That(body.DjConsole,Is.SameAs(mixer),who+": at the main mixer");
                    var handL=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="HandL");var handR=System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name=="HandR");
                    Assert.That(Vector3.Distance(handL.position,mixer.TransformPoint(new Vector3(-.46f,.81f,.36f))),Is.LessThan(.17f),who+": the left hand works the mixer");
                    Assert.That(Vector3.Distance(handR.position,mixer.TransformPoint(new Vector3(.38f,.81f,.33f))),Is.LessThan(.17f),who+": the right hand too");
                }
                var song=friend.GetComponent<FestivalStageAudio>().Music;
                Assert.That(song!=null&&song.isPlaying,Is.True,"the friend hears the stage song through the set");
                Assert.That(song.volume,Is.EqualTo(FestivalSettings.MusicGain(friend.Profile.Data,.6f*FestivalStageAudio.Mix(mate.X,mate.Z,StageMusic.Main))).Within(1e-4f).And.GreaterThan(0),"at the main stage's full share where the friend stands");
                var resident=world.transform.Find(FestivalWorld.RootName+"/resident_stage_dj").GetComponent<FestivalCharacter>();
                Assert.That(resident.Pose,Is.Not.EqualTo("Dj"),"the resident has stepped aside");
                var near=new NpcState{Id="stage_fan",Kind="Wook",X=4,Z=18,Yaw=0};var far=new NpcState{Id="far_fan",Kind="Wook",X=-25,Z=-25,Yaw=0};sim.State.Npcs.Add(near);sim.State.Npcs.Add(far);
                var chart=RhythmChart.For(set);int lane=DjRide.StartLane;
                for(int n=0;n<chart.Notes.Count;n++)
                {
                    var note=chart.Notes[n];int to=lane;double at=note.TimeSeconds;
                    if(!note.Grey)to=note.Direction;else if(lane==note.Direction){to=chart.Notes.Find(x=>x.Id>note.Id&&!x.Grey).Direction;at-=.05;}
                    if(to==lane)continue;lane=to;sim.State.SimulationSeconds=System.Math.Max(sim.State.SimulationSeconds,set.StartSeconds+at);
                    Assert.That(sim.Execute(dj.Id,new GameCommand{Id="fan_ride_"+n,Kind="Rhythm",Direction=lane,TimeSeconds=at}).Accepted,Is.True,"setup: a lane change is taken");
                }
                sim.State.SimulationSeconds=System.Math.Max(sim.State.SimulationSeconds,set.StartSeconds+set.DurationSeconds+.75);
                yield return new WaitForSeconds(.6f);
                Assert.That(sim.State.StageCheerScore,Is.EqualTo(1),"setup: a perfect phrase");
                foreach(var view in new[]{host,friend})
                {
                    string who=view==host?"the host's view":"the friend's view";
                    Assert.That(Actors(view)[near.Id].GetComponent<FestivalCharacter>().Pose,Is.EqualTo("Cheer"),who+": a festivalgoer near the stage cheers");
                    Assert.That(Actors(view)[far.Id].GetComponent<FestivalCharacter>().Pose,Is.Not.EqualTo("Cheer"),who+": one far off does not");
                }
                // The scenery pods fill the lawn in front of the stage, the DJ's whole view.
                var pods=built.GetComponentsInChildren<FestivalCharacter>(true).Where(c=>c.name.StartsWith("ambient_Stage_",System.StringComparison.Ordinal)).ToArray();
                Assert.That(pods.Length,Is.EqualTo(FestivalAmbientCrowd.FixedSpots.Count(s=>s.Zone=="Stage")),"setup: every stage pod fan");
                Assert.That(pods.Where(c=>c.Pose!="Cheer").Select(c=>c.name+" "+c.Pose),Is.Empty,"every stage pod fan cheers a perfect phrase");
                var spinner=built.GetComponentsInChildren<FestivalCharacter>(true).First(c=>c.name.StartsWith("ambient_PoiPerformer_",System.StringComparison.Ordinal)&&Vector2.Distance(new Vector2(c.transform.position.x,c.transform.position.z),new Vector2(Catalog.StageTakeoverX,Catalog.StageTakeoverZ))<Catalog.StageCheerRange);
                Assert.That(spinner.Pose,Is.EqualTo("Poi"),"a poi performer within the cheer's range keeps spinning for their own circle");
                sim.State.StageCheerScore=.2;
                yield return new WaitForSeconds(.6f);
                foreach(var view in new[]{host,friend})
                    Assert.That(Actors(view)[near.Id].GetComponent<FestivalCharacter>().Pose,Is.Not.EqualTo("Cheer"),(view==host?"the host's view":"the friend's view")+": nobody cheers a flop");
                Assert.That(pods.Where(c=>c.Pose!="Idle").Select(c=>c.name+" "+c.Pose),Is.Empty,"the stage pods shrug and idle at a flop");
                sim.State.StageCheerUntilWorldSeconds=FestivalSimulation.WorldSeconds(sim.State);
                yield return new WaitForSeconds(.6f);
                foreach(var pod in pods)Assert.That(pod.Pose,Is.EqualTo(FestivalAmbientCrowd.FixedSpots[int.Parse(pod.name.Substring("ambient_Stage_".Length))].Pose),pod.name+" goes back to its own pose when the cheer ends");
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }
        [UnityTest]public IEnumerator AJoinedFriendSeesTheHostInTheCrewScarf()
        {
            var host=new GameObject("Crew scarf host").AddComponent<FestivalSession>();
            var friend=new GameObject("Crew scarf friend").AddComponent<FestivalSession>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Host",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);friend.Join("Friend","127.0.0.1",port);
                while((friend.LocalPlayer==null||!Actors(friend).ContainsKey(host.LocalPlayerId)||!Actors(host).ContainsKey(friend.LocalPlayerId))&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,friend.Message);
                foreach(var (view,id,who) in new[]{(friend,host.LocalPlayerId,"the friend's view of the host"),(host,friend.LocalPlayerId,"the host's view of the friend")})
                {
                    Assert.That(Actors(view).ContainsKey(id),Is.True,who+" draws the body");
                    var scarf=System.Array.Find(Actors(view)[id].GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.name=="Accessory_2");
                    Assert.That(scarf!=null&&scarf.enabled&&scarf.gameObject.activeInHierarchy,Is.True,who+" wears the crew scarf");
                    Assert.That(scarf.sharedMaterial.color==FestivalCharacter.CrewColor,Is.True,who+": in the crew colour, not "+scarf.sharedMaterial.color);
                }
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friend.gameObject);Object.Destroy(host.gameObject);}
            yield return null;
        }
        // Following or searching for a friend, you mostly see their back, and past name-tag range only the scarf says who they are.
        [UnityTest]public IEnumerator TheCrewScarfShowsFromBehindAt15MetresWhateverShirtTheIdDraws()
        {
            const int width=1280,height=720;
            var root=new GameObject("Crew scarf from behind");float timeScale=Time.timeScale;var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;
            var target=new RenderTexture(width,height,24);var picture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                var sun=new GameObject("Sun").AddComponent<Light>();sun.transform.SetParent(root.transform);sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(50,-30,0);
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.55f);
                var eye=new GameObject("Eye").AddComponent<Camera>();eye.transform.SetParent(root.transform);eye.enabled=false;
                eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=new Color(.36f,.43f,.58f);eye.fieldOfView=75;eye.aspect=16f/9f;eye.targetTexture=target;
                eye.transform.position=new Vector3(0,1.5f,15);eye.transform.LookAt(new Vector3(0,1.25f,0));
                Color32[] Shot(){eye.Render();var was=RenderTexture.active;RenderTexture.active=target;picture.ReadPixels(new Rect(0,0,width,height),0,0);RenderTexture.active=was;return picture.GetPixels32();}
                Time.timeScale=0;
                for(int shirt=0;shirt<4;shirt++)
                {
                    string id=null;for(int i=0;id==null;i++)if(FestivalAppearance.For("player_"+i).Shirt==shirt)id="player_"+i;
                    var friend=FestivalCharacter.Create(root.transform,id,Color.white,FestivalCharacter.CrewRole);
                    friend.transform.rotation=Quaternion.Euler(0,180,0);friend.transform.localScale=Vector3.Scale(Vector3.one*.82f,friend.ShapeScale);friend.UpdateDetailForDistance(15);
                    var scarf=System.Array.Find(friend.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.enabled&&r.name=="Accessory_2");
                    yield return null;yield return null;var worn=Shot();
                    scarf.enabled=false;yield return null;yield return null;var bare=Shot();
                    int seen=0;for(int p=0;p<worn.Length;p++)if(Mathf.Abs(worn[p].r-bare[p].r)+Mathf.Abs(worn[p].g-bare[p].g)+Mathf.Abs(worn[p].b-bare[p].b)>24)seen++;
                    Assert.That(seen,Is.GreaterThanOrEqualTo(12),id+", who draws shirt "+shirt+" in the crowd: scarf pixels from 15 m behind");
                    Object.Destroy(friend.gameObject);
                }
            }
            finally{Time.timeScale=timeScale;RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;Object.Destroy(root);Object.Destroy(target);Object.Destroy(picture);}
            yield return null;
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
                // In the crowd this id draws the hoodie, which no crew body wears.
                const string id="hands_player_0";
                var look=FestivalAppearance.For(id,FestivalCharacter.CrewRole);
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
        // HUD-6: once a level starts on either festival, no text in the world says shuttle, and the sign over the bus at the camp
        // gate reads BACK TO CAMP. Camp's signs are read too, hidden as they are while the festival plays.
        [UnityTest]public IEnumerator NoWorldTextSaysShuttleOnEitherFestival()
        {
            for(int festival=0;festival<Festival.Core.Festivals.Count;festival++)
            {
                var world=new GameObject("No shuttle world");world.AddComponent<FestivalWorld>();yield return null;
                var host=new GameObject("No shuttle session");var session=host.AddComponent<Festival.Network.FestivalSession>();yield return null;
                try
                {
                    session.Host("Tester",HostPort);
                    float deadline=Time.realtimeSinceStartup+30;
                    while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                    Assert.That(session.LocalPlayer,Is.Not.Null,"setup: host has a local player: "+session.Message);
                    Simulation(session).State.FestivalIndex=festival;
                    yield return StartLevel(session);
                    string name=Festival.Core.Festivals.Name(festival);
                    Assert.That(session.State.FestivalIndex,Is.EqualTo(festival),"setup: the level is on "+name);
                    var texts=new List<string>();
                    foreach(var text in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include,FindObjectsSortMode.None))texts.Add(text.text);
                    Assert.That(texts.FindAll(t=>t.ToLowerInvariant().Contains("shuttle")),Is.Empty,name+": world text says shuttle");
                    Assert.That(texts,Does.Contain("BACK TO CAMP"),name+": no sign reads BACK TO CAMP among "+string.Join(" | ",texts));
                }
                finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
                yield return null;
            }
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
                Assert.That(crowd.MemberCount,Is.EqualTo(67),"62, and five at the small stage");
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
        // CROWD-7: the stage crew stand with the festival only. There they dance, no two alike at any one moment, the photographer
        // turns between the crowd and the DJ, and they are culled like the ambient crowd. The resident DJ keeps both hands on the mixer.
        [UnityTest]public IEnumerator StageCrewShowOnlyAtTheFestivalWhereNoTwoMoveAlike()
        {
            var root=new GameObject("impl-U68 stage crew world");var actualView=FestivalCharacter.ViewTransform;
            var eye=new GameObject("impl-U68 stage crew eye").transform;eye.position=new Vector3(0,1.6f,18);
            try
            {
                var world=root.AddComponent<FestivalWorld>();FestivalCharacter.ViewTransform=eye;yield return null;
                var crew=root.GetComponentInChildren<FestivalStageCrew>(true);Assert.That(crew,Is.Not.Null,"the festival has its stage crew");
                Assert.That(crew.Count,Is.EqualTo(6));var members=new List<FestivalCharacter>();for(int i=0;i<crew.Count;i++)members.Add(crew.Member(i));
                Assert.That(world.IsCampVisible,Is.True,"setup: camp");
                foreach(var member in members)Assert.That(member.gameObject.activeInHierarchy,Is.False,member.name+" is hidden at camp");
                world.SetPhase("Playing");yield return new WaitForSeconds(.6f);
                foreach(var member in members)Assert.That(member.gameObject.activeInHierarchy,Is.True,member.name+" is shown at the festival");
                var poses=members.Select(Pose).ToList();
                for(int i=0;i<poses.Count;i++)for(int j=i+1;j<poses.Count;j++)
                    Assert.That(Difference(poses[i],poses[j]),Is.GreaterThan(5),members[i].name+" and "+members[j].name+" strike different poses at the same moment");
                var photographer=Photographer(members);Assert.That(photographer,Is.Not.Null,"a photographer holds a phone up");
                var dj=root.transform.Find(FestivalWorld.RootName+"/resident_stage_dj").GetComponent<FestivalCharacter>();
                var toDj=dj.transform.position-photographer.transform.position;toDj.y=0;float crowd=180,stage=180;
                for(float end=Time.time+4.2f;Time.time<end;)
                {
                    yield return null;var facing=photographer.transform.forward;
                    crowd=Mathf.Min(crowd,Vector3.Angle(facing,Vector3.back));stage=Mathf.Min(stage,Vector3.Angle(facing,toDj));
                }
                Assert.That(crowd,Is.LessThan(25),"the photographer shoots out over the crowd");Assert.That(stage,Is.LessThan(3),"and turns to shoot the DJ");
                for(int i=0;i<members.Count;i++)if(members[i]!=photographer)Assert.That(Difference(poses[i],Pose(members[i])),Is.GreaterThan(1),members[i].name+" keeps moving");
                Assert.That(float.IsNaN(dj.DanceBeat),Is.True,"the resident DJ keeps its own clock");
                var console=dj.DjConsole;var left=Bone(dj,"HandL");var right=Bone(dj,"HandR");
                Assert.That(Vector3.Distance(left.position,console.TransformPoint(new Vector3(-.46f,.81f,.36f))),Is.LessThan(.13f),"the DJ's left hand stays on the mixer");
                Assert.That(Vector3.Distance(right.position,console.TransformPoint(new Vector3(.38f,.81f,.33f))),Is.LessThan(.13f),"the DJ's right hand stays on the mixer");
                eye.rotation=Quaternion.Euler(0,180,0);yield return new WaitForSecondsRealtime(.35f);
                foreach(var member in members)Assert.That(member.gameObject.activeSelf,Is.False,member.name+" behind the view is culled");
                eye.rotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.35f);
                foreach(var member in members)Assert.That(member.gameObject.activeSelf,Is.True,member.name+" wakes in view");
                eye.position=new Vector3(0,1.6f,-30);yield return new WaitForSecondsRealtime(.35f);
                foreach(var member in members)Assert.That(member.gameObject.activeSelf,Is.False,member.name+" far away is culled");
                world.SetPhase("Shopping");
                foreach(var member in members)Assert.That(member.gameObject.activeInHierarchy,Is.False,member.name+" is hidden back at camp");
            }
            finally{FestivalCharacter.ViewTransform=actualView;Object.Destroy(eye.gameObject);Object.Destroy(root);}
            yield return null;
            Assert.That(Object.FindObjectsByType<FestivalStageCrew>(FindObjectsInactive.Include,FindObjectsSortMode.None),Is.Empty,"the crew go with the world");
            Assert.That(System.Array.Exists(Object.FindObjectsByType<FestivalCharacter>(FindObjectsInactive.Include,FindObjectsSortMode.None),
                c=>c.name.StartsWith(FestivalStageCrew.Prefix,System.StringComparison.Ordinal)),Is.False,"and no performer is left behind");
        }
        // CROWD-7: in a hosted level the crew dance on the stage song's beat. Through a booth game they hold still with the rest of
        // the world, a friend leaving and rejoining included, and turning still culls them; after it they dance on.
        [UnityTest]public IEnumerator StageCrewDanceOnTheStageSongAndHoldThroughAGameAndARejoin()
        {
            var world=new GameObject("impl-U68 crew stage world").AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl-U68 crew host").AddComponent<FestivalSession>();
            var peer=new GameObject("impl-U68 crew peer").AddComponent<FestivalSession>();yield return null;
            var actualView=FestivalCharacter.ViewTransform;var eye=new GameObject("impl-U68 crew eye").transform;eye.position=new Vector3(0,1.6f,18);
            try
            {
                ushort port=FreePort();host.Host("Host",port);yield return null;peer.Join("Peer","127.0.0.1",port);
                for(float end=Time.realtimeSinceStartup+30;peer.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(peer.LocalPlayer,Is.Not.Null,peer.Message);var sim=Simulation(host);var joined=sim.Player(peer.LocalPlayerId);joined.X=0;joined.Z=19;peer.Command("Ready");
                for(float end=Time.realtimeSinceStartup+10;!joined.Ready&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(joined.Ready,Is.True);yield return StartLevel(host,peer.LocalPlayerId);sim.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;
                for(float end=Time.realtimeSinceStartup+5;peer.State.Phase!="Playing"&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(peer.State.Phase,Is.EqualTo("Playing"));FestivalCharacter.ViewTransform=eye;
                var crew=world.GetComponentInChildren<FestivalStageCrew>(true);Assert.That(crew,Is.Not.Null,"the festival has its stage crew");
                var members=new List<FestivalCharacter>();for(int i=0;i<crew.Count;i++)members.Add(crew.Member(i));
                yield return new WaitForSecondsRealtime(1.2f);var stage=crew.Song;
                Assert.That(stage==host.GetComponent<FestivalStageAudio>()||stage==peer.GetComponent<FestivalStageAudio>(),Is.True,"the crew follow a session's stage song");
                OnTheSong(stage,members,"the crew dance on the stage song");
                sim.State.SimulationSeconds+=30/stage.Track.Beats.Bpm;yield return new WaitForSecondsRealtime(1.5f);
                OnTheSong(crew.Song,members,"the song jumped half a beat and the crew caught it");
                var at=FestivalSimulation.BoothAt(sim.State);foreach(var p in sim.State.Players){p.X=at.X+1;p.Z=at.Z;}
                for(float end=Time.realtimeSinceStartup+6;peer.State.Phase!=FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(peer.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));yield return null;yield return null;
                Assert.That(FestivalSession.WorldAnimationPaused,Is.True,"setup: the game holds the world");
                var held=members.Select(Frozen).ToList();double beats=crew.Beats;yield return new WaitForSecondsRealtime(.4f);
                AssertHeld(members,held,"through the game");Assert.That(crew.Beats,Is.EqualTo(beats),"the crew's beat holds with the song");
                eye.rotation=Quaternion.Euler(0,180,0);yield return new WaitForSecondsRealtime(.35f);
                foreach(var member in members)Assert.That(member.gameObject.activeSelf,Is.False,member.name+" behind the view is culled during the game");
                eye.rotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.35f);
                foreach(var member in members)Assert.That(member.gameObject.activeSelf,Is.True,member.name+" wakes during the game");
                AssertHeld(members,held,"after waking in the game");
                string rejoinId=joined.Id;peer.Leave();
                for(float end=Time.realtimeSinceStartup+5;joined.Connected&&Time.realtimeSinceStartup<end;)yield return null;
                peer.Join("Peer","127.0.0.1",port);for(float end=Time.realtimeSinceStartup+30;peer.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(peer.LocalPlayerId,Is.EqualTo(rejoinId));yield return new WaitForSecondsRealtime(.3f);
                Assert.That(FestivalSession.WorldAnimationPaused,Is.True,"setup: still in the game");AssertHeld(members,held,"through a friend's rejoin");
                var turn=FestivalSimulation.CupsTurn(sim.State)==host.LocalPlayerId?host:peer;turn.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups);
                for(float end=Time.realtimeSinceStartup+6;peer.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(peer.State.Phase,Is.EqualTo("Playing"));yield return new WaitForSecondsRealtime(1.2f);
                var photographer=Photographer(members);
                for(int i=0;i<members.Count;i++)if(members[i]!=photographer)Assert.That(Difference(held[i].Rotations,Pose(members[i])),Is.GreaterThan(1),members[i].name+" dances on after the game");
                Assert.That(crew.Beats,Is.GreaterThan(beats+1),"the crew's beat, and the photographer's turn with it, moves on");
                OnTheSong(crew.Song,members,"back on the song after the game");
            }
            finally{FestivalCharacter.ViewTransform=actualView;Object.Destroy(eye.gameObject);peer.Leave();host.Leave();Object.Destroy(peer.gameObject);Object.Destroy(host.gameObject);Object.Destroy(world.gameObject);}
            yield return null;Assert.That(Time.timeScale,Is.EqualTo(1));
        }
        // Each dancer lands a step on the song's beat: its step count's phase is the song's beat phase times its steps a beat.
        static void OnTheSong(FestivalStageAudio stage,List<FestivalCharacter> members,string reason)
        {
            Assert.That(stage.Track,Is.Not.Null,"setup: the stage song plays");Assert.That(stage.Beat,Is.Not.Null);
            int steps=FestivalStageCrew.StepsPerBeat(stage.Track.Beats.Bpm);int dancers=0;
            foreach(var member in members)
            {
                if(member.Pose!="Dance")continue;dancers++;
                float off=Mathf.Repeat(member.DanceBeat-steps*stage.Beat.Value+.5f,1)-.5f;
                Assert.That(Mathf.Abs(off),Is.LessThan(.1f),reason+": "+member.name+" is "+off+" of a step off the beat");
            }
            Assert.That(dancers,Is.EqualTo(4),"setup: four dancers");
        }
        static FestivalCharacter Photographer(List<FestivalCharacter> members)=>members.Find(m=>System.Array.Exists(m.GetComponentsInChildren<Transform>(),t=>t.name==FestivalFilmingPhone.PhoneName));
        sealed class FrozenPose{public Quaternion[] Rotations;public Vector3[] Positions;}
        static FrozenPose Frozen(FestivalCharacter actor)=>new FrozenPose{Rotations=Pose(actor),Positions=FestivalMotionLibrary.Bones.Select(b=>Bone(actor,b).localPosition).ToArray()};
        static void AssertHeld(List<FestivalCharacter> members,List<FrozenPose> held,string when)
        {
            for(int i=0;i<members.Count;i++)for(int b=0;b<FestivalMotionLibrary.Bones.Length;b++)
            {
                var bone=Bone(members[i],FestivalMotionLibrary.Bones[b]);string who=members[i].name+" "+bone.name+" "+when;
                Assert.That(bone.localRotation,Is.EqualTo(held[i].Rotations[b]),who);Assert.That(bone.localPosition,Is.EqualTo(held[i].Positions[b]),who);
            }
        }
        static Quaternion[] Pose(FestivalCharacter actor)=>FestivalMotionLibrary.Bones.Select(b=>Bone(actor,b).localRotation).ToArray();
        static float Difference(Quaternion[] a,Quaternion[] b){float most=0;for(int i=0;i<a.Length;i++)most=Mathf.Max(most,Quaternion.Angle(a[i],b[i]));return most;}
        static Transform Bone(FestivalCharacter actor,string name)=>System.Array.Find(actor.GetComponentsInChildren<Transform>(true),t=>t.name==name);
    }
}
