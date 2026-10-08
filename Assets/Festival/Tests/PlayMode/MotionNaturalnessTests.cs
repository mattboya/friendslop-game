using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using Festival.Network;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class MotionNaturalnessTests
    {
        static Transform Bone(FestivalCharacter actor,string name)=>actor.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static float Local(FestivalCharacter actor,string bone,int axis)=>actor.transform.InverseTransformPoint(Bone(actor,bone).position)[axis];
        static float Correlation(List<float> a,List<float> b)
        {
            float ma=a.Average(),mb=b.Average(),num=0,da=0,db=0;
            for(int i=0;i<a.Count;i++){num+=(a[i]-ma)*(b[i]-mb);da+=(a[i]-ma)*(a[i]-ma);db+=(b[i]-mb)*(b[i]-mb);}
            return num/Mathf.Sqrt(Mathf.Max(1e-9f,da*db));
        }
        static FestivalCharacter Walker(GameObject root,string name)
        {
            var actor=FestivalCharacter.Create(root.transform,name,Color.white);
            actor.transform.localScale=Vector3.one*.82f;
            return actor;
        }

        [UnityTest]public IEnumerator PausedWorldHoldsBonesIkAndPoiWithoutFrameCreep()
        {
            var root=new GameObject("impl-U59 frozen rig");var actor=Walker(root,"impl-U59 frozen performer");actor.SetEquippedItem("poi_led");
            float scale=Time.timeScale;
            try
            {
                actor.SpinningPoi=true;yield return new WaitForSeconds(.12f);
                Time.timeScale=0;yield return null;yield return null;
                var bones=FestivalMotionLibrary.Bones.Select(name=>Bone(actor,name)).ToArray();
                var positions=bones.Select(b=>b.localPosition).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
                var rigs=actor.GetComponentsInChildren<FestivalPoiRig>();Assert.That(rigs.Length,Is.EqualTo(2));
                var ends=rigs.Select(r=>r.GetComponent<LineRenderer>().GetPosition(1)).ToArray();
                for(int frame=0;frame<90;frame++)yield return null;
                for(int i=0;i<bones.Length;i++)
                {
                    Assert.That(bones[i].localPosition,Is.EqualTo(positions[i]),"paused IK holds "+bones[i].name);
                    Assert.That(bones[i].localRotation,Is.EqualTo(rotations[i]),"paused bones never ease by an invented delta: "+bones[i].name);
                }
                for(int i=0;i<rigs.Length;i++)Assert.That(rigs[i].GetComponent<LineRenderer>().GetPosition(1),Is.EqualTo(ends[i]),"paired head holds its world pose");
                rigs[0].gameObject.SetActive(false);yield return null;rigs[0].gameObject.SetActive(true);yield return null;yield return null;
                Assert.That(rigs[0].GetComponent<LineRenderer>().GetPosition(1),Is.EqualTo(ends[0]),"reactivation during pause preserves the tether pose");
                Time.timeScale=1;yield return new WaitForSeconds(.2f);
                Assert.That(bones.Select(b=>b.localRotation).SequenceEqual(rotations),Is.False,"real rig animation resumes");
            }
            finally{Time.timeScale=scale;Object.Destroy(root);}
            yield return null;
        }

        [UnityTest]public IEnumerator GameLocalClockAnimatesOneRigWithoutUnfreezingWorld()
        {
            var root=new GameObject("impl-U59 local clock rigs");var world=Walker(root,"impl-U59 frozen world");world.SetEquippedItem("poi_practice");world.SpinningPoi=true;
            var game=Walker(root,"impl-U59 game performer");game.transform.position=Vector3.right*3;game.Pose="Poi";game.SetEquippedItem("poi_led");
            float scale=Time.timeScale;
            // Invoke the optional public seam when available; the pre-feature source still compiles and fails on actual absent motion.
            var setClock=typeof(FestivalCharacter).GetMethod("SetAnimationClock",BindingFlags.Public|BindingFlags.Instance);
            var clearClock=typeof(FestivalCharacter).GetMethod("ClearAnimationClock",BindingFlags.Public|BindingFlags.Instance);
            try
            {
                yield return new WaitForSeconds(.15f);Time.timeScale=0;yield return null;yield return null;
                var worldBones=FestivalMotionLibrary.Bones.Select(name=>Bone(world,name)).ToArray();var frozen=worldBones.Select(b=>b.localRotation).ToArray();
                var worldRigs=world.GetComponentsInChildren<FestivalPoiRig>();var worldEnds=worldRigs.Select(r=>r.GetComponent<LineRenderer>().GetPosition(1)).ToArray();
                var gameRigs=game.GetComponentsInChildren<FestivalPoiRig>();Assert.That(gameRigs.Length,Is.EqualTo(2));
                var wrist=new Bounds(Bone(game,"HandR").position,Vector3.zero);var head=new Bounds(gameRigs[0].GetComponent<LineRenderer>().GetPosition(1),Vector3.zero);
                var leg=Bone(game,"LegR").localRotation;float legMotion=0;
                for(int frame=0;frame<90;frame++)
                {
                    setClock?.Invoke(game,new object[]{100+frame/60f,1/60f});yield return null;
                    wrist.Encapsulate(Bone(game,"HandR").position);head.Encapsulate(gameRigs[0].GetComponent<LineRenderer>().GetPosition(1));
                    legMotion=Mathf.Max(legMotion,Quaternion.Angle(leg,Bone(game,"LegR").localRotation));
                }
                Assert.That(wrist.size.magnitude,Is.GreaterThan(.05f),"game-local wrists animate while the world clock is stopped");
                Assert.That(head.size.y,Is.GreaterThan(.3f),"attached paired poi consume that same explicit clock");
                Assert.That(legMotion,Is.GreaterThan(2),"game-local lower-body pose and foot planting advance too");
                Assert.That(worldBones.Select(b=>b.localRotation),Is.EqualTo(frozen),"one game performer never unfreezes ordinary world bones");
                for(int i=0;i<worldRigs.Length;i++)Assert.That(worldRigs[i].GetComponent<LineRenderer>().GetPosition(1),Is.EqualTo(worldEnds[i]));
                setClock.Invoke(game,new object[]{101.5f,0f});yield return null;yield return null;
                var gameBones=FestivalMotionLibrary.Bones.Select(name=>Bone(game,name)).ToArray();var stopped=gameBones.Select(b=>b.localRotation).ToArray();
                for(int frame=0;frame<20;frame++)yield return null;
                Assert.That(gameBones.Select(b=>b.localRotation),Is.EqualTo(stopped),"zero game-local delta holds its pose exactly");
                clearClock.Invoke(game,null);yield return null;yield return null;
                Assert.That(gameBones.Select(b=>b.localRotation),Is.EqualTo(stopped),"clearing the override returns to frozen world time");
                setClock.Invoke(game,new object[]{102f,1/60f});yield return null;yield return null;
                game.gameObject.SetActive(false);yield return null;game.gameObject.SetActive(true);yield return null;yield return null;
                stopped=gameBones.Select(b=>b.localRotation).ToArray();for(int frame=0;frame<20;frame++)yield return null;
                Assert.That(gameBones.Select(b=>b.localRotation),Is.EqualTo(stopped),"disable clears the game-local override before reactivation");
                Time.timeScale=1;yield return new WaitForSeconds(.2f);Assert.That(worldBones.Select(b=>b.localRotation).SequenceEqual(frozen),Is.False,"world animation resumes normally");
            }
            finally{Time.timeScale=scale;Object.Destroy(root);}
            yield return null;
        }

        [UnityTest]public IEnumerator PausedCameraHandsHoldTheirLocalPoseWhileTheViewTurns()
        {
            var cameraObject=new GameObject("impl-U59 paused hands camera");var camera=cameraObject.AddComponent<Camera>();
            var hands=FestivalHands.Create(camera,"impl-U59 paused hands");Assert.That(hands,Is.Not.Null);
            var player=new PlayerState{Id="impl-U59-hands",Life="Alive",EquippedItemId="poi_led"};
            float scale=Time.timeScale,capture=Time.captureDeltaTime;Time.captureDeltaTime=1/60f;
            try
            {
                hands.SetState(player,10,spinning:true);
                for(int frame=0;frame<30;frame++){camera.transform.position+=Vector3.forward*.04f;yield return null;}
                Time.timeScale=0;yield return null;yield return null;
                var attachment=camera.transform.Find("First-person hand attachments");Assert.That(attachment,Is.Not.Null);
                var position=hands.transform.localPosition;var grip=attachment.localPosition;
                for(int frame=0;frame<60;frame++)
                {
                    camera.transform.position+=Vector3.forward*.01f;camera.transform.rotation=Quaternion.Euler(0,frame*3,0);yield return null;
                    Assert.That(hands.transform.localPosition,Is.EqualTo(position),"camera-relative arms cannot creep during pause");
                    Assert.That(attachment.localPosition,Is.EqualTo(grip),"camera-relative attachment pose holds through view movement");
                }
                Time.timeScale=1;yield return new WaitForSeconds(.2f);
                Assert.That(hands.transform.localPosition,Is.Not.EqualTo(position),"normal hand breathing resumes");
            }
            finally{Time.timeScale=scale;Time.captureDeltaTime=capture;Object.Destroy(cameraObject);}
            yield return null;
        }

        // Sample after both the real character (order 0) and tether (order 100) finish LateUpdate.
        [UnityTest]public IEnumerator WalkingPracticePoiHeadsClearEveryGeneratedBodyThroughStartupAndTurns(){yield return CarryClearance("poi_practice");}
        [UnityTest]public IEnumerator WalkingLedPoiHeadsClearEveryGeneratedBodyThroughStartupAndTurns(){yield return CarryClearance("poi_led");}
        IEnumerator CarryClearance(string item)
        {
            var root=new GameObject("impl:U57 carry clearance");float capture=Time.captureDeltaTime;Time.captureDeltaTime=1/60f;
            var actors=new List<FestivalCharacter>();var rigs=new Dictionary<FestivalCharacter,FestivalPoiRig[]>();
            var sides=new Dictionary<FestivalPoiRig,float>();var radii=new Dictionary<FestivalPoiRig,float>();
            var samplesByRig=new Dictionary<FestivalPoiRig,int>();
            int samples=0;float minimum=float.MaxValue,leastReach=float.MaxValue;string failure=null;
            var probe=root.AddComponent<HandMotionFrameProbe>();
            try
            {
                var names=new Dictionary<int,string>();
                for(int n=0;n<256&&names.Count<6;n++)
                {
                    string name="impl:U57 carry shape "+n;var look=FestivalAppearance.For(name);
                    names[look.Gender*3+look.Shape]=name;
                }
                Assert.That(names.Count,Is.EqualTo(6),"setup: both genders and all three generated shapes");
                foreach(var pair in names.OrderBy(p=>p.Key))
                {
                    var actor=FestivalCharacter.Create(root.transform,pair.Value,Color.white);
                    actor.transform.localScale=Vector3.Scale(Vector3.one*.82f,actor.ShapeScale);
                    actor.transform.position=new Vector3(pair.Key*4,0,0);actor.transform.rotation=Quaternion.Euler(0,pair.Key*57,0);
                    actor.SetEquippedItem(item);actors.Add(actor);
                }
                yield return null;yield return null;
                foreach(var actor in actors)
                {
                    rigs[actor]=actor.GetComponentsInChildren<FestivalPoiRig>().ToArray();Assert.That(rigs[actor].Length,Is.EqualTo(2));
                    foreach(var rig in rigs[actor])
                    {
                        sides[rig]=Mathf.Sign(Vector3.Dot(rig.GetComponent<LineRenderer>().GetPosition(0)-actor.transform.position,actor.transform.right));
                        var weighted=(Transform)typeof(FestivalPoiRig).GetField("head",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(rig);
                        float radius=0;
                        foreach(var renderer in weighted.GetComponentsInChildren<Renderer>())
                            radius=Mathf.Max(radius,Vector3.Distance(renderer.bounds.center,weighted.position)+renderer.bounds.extents.magnitude);
                        Assert.That(radius,Is.GreaterThan(.01f),"setup: measured actual weighted head geometry");radii[rig]=radius;samplesByRig[rig]=0;
                    }
                }
                probe.Observe=()=>
                {
                    foreach(var actor in actors)
                    {
                        var left=Bone(actor,"ArmL");var right=Bone(actor,"ArmR");
                        float radius=Mathf.Abs(Vector3.Dot(left.position-right.position,actor.transform.right))*.5f;
                        float bottom=Mathf.Min(Bone(actor,"FootL").position.y,Bone(actor,"FootR").position.y)+radius;
                        float top=Mathf.Max(left.position.y,right.position.y)-radius;
                        foreach(var rig in rigs[actor])
                        {
                            if(!rig.Carrying)continue;
                            samplesByRig[rig]++;samples++;
                            var rope=rig.GetComponent<LineRenderer>();var grip=rope.GetPosition(0);var head=rope.GetPosition(1);
                            float dot=Vector3.Dot(grip-actor.transform.position,actor.transform.right);
                            if(Mathf.Abs(dot)>.001f)sides[rig]=Mathf.Sign(dot);
                            var nearest=new Vector3(actor.transform.position.x,Mathf.Clamp(head.y,bottom,Mathf.Max(bottom,top)),actor.transform.position.z);
                            float clearance=Vector3.Distance(head,nearest)-radius-radii[rig];minimum=Mathf.Min(minimum,clearance);
                            leastReach=Mathf.Min(leastReach,Mathf.Abs(dot)+.62f-radius-radii[rig]);
                            if(failure==null&&clearance<-.001f)failure=actor.name+" "+item+" head enters shoulder-to-feet capsule at sample "+samples+" clearance="+clearance;
                            if(failure==null&&Vector3.Dot(head-actor.transform.position,actor.transform.right)*sides[rig]<radii[rig]-.001f)failure="head crosses actual grip side at sample "+samples;
                            if(failure==null&&Mathf.Abs(Vector3.Distance(grip,head)-.62f)>.001f)failure="carry changes the .62m cord at sample "+samples;
                        }
                    }
                };
                for(int frame=0;frame<184;frame++)
                {
                    for(int n=0;n<actors.Count;n++)
                    {
                        var actor=actors[n];
                        actor.transform.Rotate(0,(n%2==0?95:-130)*Time.deltaTime,0,Space.World);
                        if(frame==60||frame==120)actor.transform.Rotate(0,n%2==0?90:-90,0,Space.World);
                        actor.transform.position+=actor.transform.forward*(1.6f*Time.deltaTime);
                    }
                    yield return null;
                }
                probe.Observe=null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Festival.Test] impl:U57 item="+item+" samples="+samples+" clearance="+minimum+" available lateral reach="+leastReach);
#endif
                Assert.That(samples,Is.GreaterThan(2000),"three seconds of every actual carry pair, including startup");
                Assert.That(samplesByRig.Values.Min(),Is.GreaterThanOrEqualTo(180),"every rig was observed from its first completed carry frame through at least three seconds");
                Assert.That(failure,Is.Null,failure);
            }
            finally{probe.Observe=null;Object.Destroy(root);Time.captureDeltaTime=capture;}
            yield return null;
        }

        [UnityTest]public IEnumerator CarryKeepsItsLastGripSideAtTheMidlineAndFollowsAMovedGrip()
        {
            var root=new GameObject("impl:U57 carry midline");float capture=Time.captureDeltaTime;Time.captureDeltaTime=1/60f;
            try
            {
                var actor=Walker(root,"impl:U57 midline actor");actor.SetEquippedItem("poi_led");
                for(int frame=0;frame<35;frame++){actor.transform.position+=actor.transform.forward*(1.6f*Time.deltaTime);yield return null;}
                var rigs=actor.GetComponentsInChildren<FestivalPoiRig>();Assert.That(rigs.Length,Is.EqualTo(2));
                // Freeze only the arm writer after real walking frames. Move each actual grip to
                // its midline, then across it, and execute the unchanged production tether writer.
                actor.enabled=false;
                var late=typeof(FestivalPoiRig).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
                var remembered=typeof(FestivalPoiRig).GetField("carrySide",BindingFlags.NonPublic|BindingFlags.Instance);
                foreach(var rig in rigs)
                {
                    Assert.That(rig.Carrying,Is.True);float side=(float)remembered.GetValue(rig);
                    var centre=actor.transform.position+Vector3.up;
                    rig.transform.position=centre+rig.transform.up*.085f;late.Invoke(rig,null);
                    var cord=rig.GetComponent<LineRenderer>();
                    Assert.That(Vector3.Dot(cord.GetPosition(0)-actor.transform.position,actor.transform.right),Is.EqualTo(0).Within(.0001f),"setup: actual grip at the midline");
                    Assert.That(Vector3.Dot(cord.GetPosition(1)-actor.transform.position,actor.transform.right)*side,Is.GreaterThan(.1f),"zero dot retains the last actual grip side");
                    rig.transform.position=centre-actor.transform.right*(side*.12f)+rig.transform.up*.085f;late.Invoke(rig,null);
                    Assert.That(Vector3.Dot(cord.GetPosition(1)-actor.transform.position,actor.transform.right)*-side,Is.GreaterThan(.1f),"a nonzero moved grip replaces remembered handedness");
                    Assert.That(Vector3.Distance(cord.GetPosition(0),cord.GetPosition(1)),Is.EqualTo(.62f).Within(.001f));
                }
            }
            finally{Object.Destroy(root);Time.captureDeltaTime=capture;}
            yield return null;
        }

        [UnityTest]public IEnumerator WalkingArmsSwingOppositeTheForwardFootAndShouldersCounterRotate()
        {
            var root=new GameObject("Gait opposition test");int rate=Application.targetFrameRate;Application.targetFrameRate=60;
            try
            {
                var actor=Walker(root,"Gait opposition actor");
                var feet=new List<float>();var hands=new List<float>();var hips=new List<float>();var shoulders=new List<float>();
                float start=Time.time;
                while(Time.time-start<2.2f)
                {
                    actor.transform.position+=Vector3.forward*(1.6f*Time.deltaTime);
                    yield return null;
                    if(Time.time-start<.6f)continue;
                    feet.Add(Local(actor,"FootL",2)-Local(actor,"FootR",2));
                    hands.Add(Local(actor,"HandR",2)-Local(actor,"HandL",2));
                    hips.Add(Local(actor,"LegL",2)-Local(actor,"LegR",2));
                    shoulders.Add(Local(actor,"ArmL",2)-Local(actor,"ArmR",2));
                }
                float arms=Correlation(feet,hands),pelvis=Correlation(feet,hips),chest=Correlation(feet,shoulders);
                Debug.Log($"[Festival.Test] gait opposition arms={arms:F2} pelvis={pelvis:F2} shoulders={chest:F2}");
                Assert.That(arms,Is.GreaterThan(.5f),"The hand opposite the forward foot should lead");
                Assert.That(pelvis,Is.GreaterThan(.3f),"The pelvis should turn with the forward leg");
                Assert.That(chest,Is.LessThan(-.3f),"The shoulders should counter-rotate against the pelvis");
            }
            finally{Object.Destroy(root);Application.targetFrameRate=rate;}
        }

        [UnityTest]public IEnumerator JoggingCadenceFitsShortLegs()
        {
            var root=new GameObject("Jog cadence test");int rate=Application.targetFrameRate;Application.targetFrameRate=60;
            try
            {
                var actor=Walker(root,"Jog cadence actor");
                var left=Bone(actor,"FootL");var right=Bone(actor,"FootR");
                yield return null;
                float floor=Mathf.Min(left.position.y,right.position.y);
                bool wasL=false,wasR=false;int landings=0;float start=Time.time,counted=0;
                while(Time.time-start<2.4f)
                {
                    var lastL=left.position;var lastR=right.position;var lastRoot=actor.transform.position;
                    actor.transform.position+=Vector3.forward*(4f*Time.deltaTime);
                    yield return null;
                    float step=Vector3.Distance(lastRoot,actor.transform.position);
                    bool plantedL=Vector3.Distance(lastL,left.position)<step*.35f&&left.position.y<floor+.05f;
                    bool plantedR=Vector3.Distance(lastR,right.position)<step*.35f&&right.position.y<floor+.05f;
                    if(Time.time-start>.5f)
                    {
                        if(plantedL&&!wasL)landings++;
                        if(plantedR&&!wasR)landings++;
                        counted+=Time.deltaTime;
                    }
                    wasL=plantedL;wasR=plantedR;
                }
                float stepsPerSecond=landings/counted;
                Debug.Log($"[Festival.Test] jog cadence steps/s={stepsPerSecond:F2} landings={landings} over {counted:F2}s");
                Assert.That(stepsPerSecond,Is.InRange(3.2f,6f),"A 4 m/s jog should land 3-6 steps a second, not a 7+ step blur");
            }
            finally{Object.Destroy(root);Application.targetFrameRate=rate;}
        }

        [UnityTest]public IEnumerator RunningLeansForwardAndBendsTheElbows()
        {
            var root=new GameObject("Run posture test");int rate=Application.targetFrameRate;Application.targetFrameRate=60;
            try
            {
                var walker=Walker(root,"Posture walker");var runner=Walker(root,"Posture runner");
                runner.transform.position=Vector3.right*4;
                float walkElbow=0,runElbow=0,walkLean=0,runLean=0;int samples=0;float start=Time.time;
                float Elbow(FestivalCharacter a)=>Vector3.Angle(Bone(a,"ForearmL").position-Bone(a,"ArmL").position,Bone(a,"HandL").position-Bone(a,"ForearmL").position);
                float Lean(FestivalCharacter a)=>Local(a,"Head",2)-Local(a,"Hips",2);
                while(Time.time-start<2f)
                {
                    walker.transform.position+=Vector3.forward*(1.2f*Time.deltaTime);
                    runner.transform.position+=Vector3.forward*(5.5f*Time.deltaTime);
                    yield return null;
                    if(Time.time-start<.8f)continue;
                    walkElbow+=Elbow(walker);runElbow+=Elbow(runner);walkLean+=Lean(walker);runLean+=Lean(runner);samples++;
                }
                walkElbow/=samples;runElbow/=samples;walkLean/=samples;runLean/=samples;
                Debug.Log($"[Festival.Test] posture elbow walk={walkElbow:F1} run={runElbow:F1} lean walk={walkLean:F3} run={runLean:F3}");
                Assert.That(runElbow,Is.GreaterThan(walkElbow+25f),"Runners pump bent elbows");
                Assert.That(runLean,Is.GreaterThan(walkLean+.05f),"Runners lean into the run");
            }
            finally{Object.Destroy(root);Application.targetFrameRate=rate;}
        }

        [UnityTest]public IEnumerator StandingActorsShiftWeightAndGlanceWithFeetPlanted()
        {
            var root=new GameObject("Idle life test");
            try
            {
                var actor=Walker(root,"Idle life actor");
                var head=Bone(actor,"Head");var hips=Bone(actor,"Hips");var left=Bone(actor,"FootL");var right=Bone(actor,"FootR");
                yield return new WaitForSeconds(.5f);
                var footL=left.position;var footR=right.position;
                float minYaw=999,maxYaw=-999,minHip=999,maxHip=-999,start=Time.time;
                while(Time.time-start<6f)
                {
                    yield return null;
                    var forward=actor.transform.InverseTransformDirection(head.forward);
                    float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
                    minYaw=Mathf.Min(minYaw,yaw);maxYaw=Mathf.Max(maxYaw,yaw);
                    float x=actor.transform.InverseTransformPoint(hips.position).x;
                    minHip=Mathf.Min(minHip,x);maxHip=Mathf.Max(maxHip,x);
                }
                float feetMoved=Mathf.Max(Vector3.Distance(footL,left.position),Vector3.Distance(footR,right.position));
                Debug.Log($"[Festival.Test] idle head yaw range={maxYaw-minYaw:F1} hip sway={maxHip-minHip:F3} feet moved={feetMoved:F3}");
                Assert.That(maxYaw-minYaw,Is.GreaterThan(12f),"Standing actors should glance around");
                Assert.That(maxHip-minHip,Is.GreaterThan(.008f),"Standing actors should shift their weight");
                Assert.That(feetMoved,Is.LessThan(.03f),"Weight shifts must not slide planted feet");
            }
            finally{Object.Destroy(root);}
        }

        // PLAYA-2: an Ember Playa art car carries its rider round at ArtCarSpeed, and the gait read the car's motion as their own,
        // so they walked in place on the deck. Beside them a walker at the car's pace shows the stride this looks for.
        [UnityTest]public IEnumerator ARiderStandsStillOnTheArtCarWhileItCarriesThem()
        {
            var root=new GameObject("Art car rider test");int rate=Application.targetFrameRate;Application.targetFrameRate=60;
            try
            {
                var rider=Walker(root,"Art car rider");rider.Pose=FestivalSimulation.RideCarKind;
                var walker=Walker(root,"Art car pace walker");walker.transform.position=Vector3.right*4;
                var riderFeet=new List<float>[]{new List<float>(),new List<float>()};var walkerFeet=new List<float>[]{new List<float>(),new List<float>()};
                float start=Time.time;
                while(Time.time-start<2.2f)
                {
                    var roll=Vector3.forward*(Festivals.ArtCarSpeed*Time.deltaTime);rider.transform.position+=roll;walker.transform.position+=roll;
                    yield return null;
                    if(Time.time-start<.6f)continue;
                    for(int f=0;f<2;f++){var foot=f==0?"FootL":"FootR";riderFeet[f].Add(Local(rider,foot,2));walkerFeet[f].Add(Local(walker,foot,2));}
                }
                float Swing(List<float>[] feet)=>Mathf.Max(feet[0].Max()-feet[0].Min(),feet[1].Max()-feet[1].Min());
                float ridden=Swing(riderFeet),walked=Swing(walkerFeet);
                Debug.Log($"[Festival.Test] art car rider foot swing={ridden:F3} walker at the same pace={walked:F3}");
                Assert.That(walked,Is.GreaterThan(.2f),"setup: at the car's pace a walker's feet swing back and forth");
                Assert.That(ridden,Is.LessThan(.04f),"a rider stands on the deck while the car carries them, feet still under them");
            }
            finally{Object.Destroy(root);Application.targetFrameRate=rate;}
        }

        [UnityTest]public IEnumerator DanceStylesHaveDistinctUpperBodies()
        {
            var root=new GameObject("Dance style test");
            try
            {
                var dancers=new Dictionary<int,FestivalCharacter>();
                for(int i=0;dancers.Count<3&&i<60;i++)
                {
                    var actor=FestivalCharacter.Create(root.transform,"Style dancer "+i,Color.white);
                    if(dancers.ContainsKey(actor.DanceStyle)){Object.Destroy(actor.gameObject);continue;}
                    actor.transform.position=Vector3.right*(3*dancers.Count);actor.Pose="Dance";dancers[actor.DanceStyle]=actor;
                }
                Assert.That(dancers.Count,Is.EqualTo(3));
                var features=dancers.ToDictionary(d=>d.Key,d=>Vector4.zero);
                int samples=0;float start=Time.time;
                while(Time.time-start<4f)
                {
                    yield return null;
                    if(Time.time-start<.5f)continue;
                    foreach(var pair in dancers)
                    {
                        var a=pair.Value;
                        features[pair.Key]+=new Vector4(Local(a,"HandL",1),Local(a,"HandR",1),Local(a,"HandL",2)+Local(a,"HandR",2),Mathf.Abs(Local(a,"HandL",0)-Local(a,"HandR",0)));
                    }
                    samples++;
                }
                var keys=features.Keys.ToList();
                foreach(var k in keys)features[k]/=samples;
                Debug.Log("[Festival.Test] dance upper-body features "+string.Join(" ",keys.Select(k=>k+"="+features[k].ToString("F3"))));
                for(int i=0;i<keys.Count;i++)for(int j=i+1;j<keys.Count;j++)
                    Assert.That(Vector4.Distance(features[keys[i]],features[keys[j]]),Is.GreaterThan(.08f),$"Dance styles {keys[i]} and {keys[j]} move their arms alike");
            }
            finally{Object.Destroy(root);}
        }

        [UnityTest]public IEnumerator ActingStatesPlayAuthoredClipsWithoutMovingTheActor()
        {
            Assert.That(FestivalMotionLibrary.Count,Is.GreaterThanOrEqualTo(20),"Package motion clips should be baked for runtime use");
            var root=new GameObject("Acting clip test");
            try
            {
                var standing=Walker(root,"Acting reference");
                var rest=new[]{"Spine","Head","ArmL","ArmR"}.ToDictionary(n=>n,n=>Bone(standing,n).localRotation);
                var cast=new Dictionary<string,FestivalCharacter>();
                int index=1;
                foreach(var pose in new[]{"Downed","Detained","Questioning","Accusing","Swarming","Watching","Extract","Rescue","Drag"})
                {
                    var actor=Walker(root,"Acting "+pose);actor.transform.position=Vector3.right*(2.5f*index++);actor.Pose=pose;cast[pose]=actor;
                }
                yield return new WaitForSeconds(1.4f);
                float standingHips=Local(standing,"Hips",1);
                foreach(var pair in cast)
                {
                    float acting=rest.Max(r=>Quaternion.Angle(r.Value,Bone(pair.Value,r.Key).localRotation));
                    Debug.Log($"[Festival.Test] acting {pair.Key} upper={acting:F1} hips={Local(pair.Value,"Hips",1):F3} handsZ={Local(pair.Value,"HandL",2):F3}/{Local(pair.Value,"HandR",2):F3} hipsZ={Local(pair.Value,"Hips",2):F3}");
                    // Watching is the subtle first hostility stage: a turned, locked head.
                    Assert.That(acting,Is.GreaterThan(pair.Key=="Watching"?6f:12f),pair.Key+" should act, not hold the rest pose");
                    Assert.That(pair.Value.transform.position.y,Is.EqualTo(0),pair.Key+" must not move the gameplay actor");
                }
                Assert.That(Local(cast["Downed"],"Hips",1),Is.LessThan(standingHips*.5f),"A downed festivalgoer should lie on the ground");
                Assert.That(Local(cast["Detained"],"HandL",2)+Local(cast["Detained"],"HandR",2),Is.LessThan(2*Local(cast["Detained"],"Spine",2)),"Detained hands belong behind the back");
            }
            finally{Object.Destroy(root);}
        }

        // DIALOG-3: a player selling, buying, chatting or talking to security talks: FestivalSession.ViewFor shows them in their talk's
        // own kind, and their upper body plays the Talk clip a questioning festivalgoer uses, never the bare standing pose. Actors of
        // one name share their phase, so the same clip shows the same frame on each.
        [UnityTest]public IEnumerator ATalkerPlaysTheTalkClip()
        {
            var root=new GameObject("Talk pose test");
            try
            {
                var questioning=Walker(root,"Talker");questioning.Pose="Questioning";
                var talkers=new Dictionary<string,FestivalCharacter>();int index=1;
                foreach(var kind in new[]{"Sale","Buy","Conversation","Police"})
                {
                    Assert.That(FestivalSimulation.Talks(kind),Is.True,"setup: a "+kind+" is a talk");
                    var talker=Walker(root,"Talker");talker.transform.position=Vector3.right*(2.5f*index++);talker.Pose=kind;talkers[kind]=talker;
                }
                yield return new WaitForSeconds(1);
                foreach(var pair in talkers)
                {
                    float off=0;
                    for(int i=1;i<=8;i++){string bone=FestivalMotionLibrary.Bones[i];off=Mathf.Max(off,Quaternion.Angle(Bone(questioning,bone).localRotation,Bone(pair.Value,bone).localRotation));}
                    Assert.That(off,Is.LessThan(2f),"a "+pair.Key+" talks with the Talk clip: its upper body is "+off.ToString("F1")+" degrees off it");
                }
            }
            finally{Object.Destroy(root);}
        }

        // Largest angle any cord swings away from straight down, sampled over a window.
        static IEnumerator HangSwing(LineRenderer[] cords,float seconds,float[] result)
        {
            result[0]=0;float start=Time.time;
            while(Time.time-start<seconds)
            {
                yield return null;
                foreach(var cord in cords)result[0]=Mathf.Max(result[0],Vector3.Angle(cord.GetPosition(1)-cord.GetPosition(0),Vector3.down));
            }
        }

        [UnityTest]public IEnumerator EquippedPoiAreAPairThatWheelsUnderGravityWhileWalking()
        {
            var root=new GameObject("Carried poi test");int rate=Application.targetFrameRate;Application.targetFrameRate=60;
            try
            {
                var actor=Walker(root,"Carried poi actor");
                actor.SetEquippedItem("poi_practice");
                yield return null;yield return null;
                var rigs=actor.GetComponentsInChildren<FestivalPoiRig>().Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Assert.That(rigs.Length,Is.EqualTo(2),"Carried poi should be a pair, one per hand");
                var cords=rigs.Select(r=>r.GetComponent<LineRenderer>()).ToArray();
                var previous=cords.Select(c=>c.GetPosition(1)-c.GetPosition(0)).ToArray();
                float low=1,high=-1,bottom=0,top=0;int bottoms=0,tops=0;float start=Time.time;
                while(Time.time-start<2f)
                {
                    actor.transform.position+=Vector3.forward*(1.4f*Time.deltaTime);
                    yield return null;
                    for(int i=0;i<2;i++)
                    {
                        var offset=cords[i].GetPosition(1)-cords[i].GetPosition(0);
                        Assert.That(offset.magnitude,Is.InRange(.58f,.66f),"The cord should stay taut");
                        float angular=Vector3.Angle(previous[i],offset)/Time.deltaTime;previous[i]=offset;
                        if(Time.time-start<.6f)continue; // spin-up from hanging
                        var dir=offset.normalized;low=Mathf.Min(low,dir.y);high=Mathf.Max(high,dir.y);
                        if(dir.y<-.75f){bottom+=angular;bottoms++;}else if(dir.y>.75f){top+=angular;tops++;}
                    }
                }
                bottom/=Mathf.Max(1,bottoms);top/=Mathf.Max(1,tops);
                float left=Vector3.Dot(cords[0].GetPosition(0)-actor.transform.position,actor.transform.right);
                float right=Vector3.Dot(cords[1].GetPosition(0)-actor.transform.position,actor.transform.right);
                Debug.Log($"[Festival.Test] carried poi vertical={low:F2}..{high:F2} bottom={bottom:F0}deg/s top={top:F0}deg/s grips={left:F2}/{right:F2}");
                Assert.That(low,Is.LessThan(-.6f));Assert.That(high,Is.GreaterThan(.6f),"Carried heads should wheel, not hang or float");
                Assert.That(bottom,Is.GreaterThan(top*1.25f),"Weighted heads whip through the bottom and slow over the top");
                Assert.That(left*right,Is.LessThan(0),"One poi in each hand");
                // Standing still, the heads stop wheeling and settle as pendulums below the hands.
                var swing=new float[1];
                yield return new WaitForSeconds(2f);
                yield return HangSwing(cords,.6f,swing);
                float ground=rigs.Min(r=>r.GetComponent<LineRenderer>().GetPosition(1).y);
                Debug.Log($"[Festival.Test] carried poi standing swing={swing[0]:F0}deg lowest head={ground:F2}");
                Assert.That(swing[0],Is.LessThan(20f),"Poi should hang, not spin, while standing still");
                Assert.That(ground,Is.GreaterThan(.05f),"Hanging heads stay above the ground");
            }
            finally{Object.Destroy(root);Application.targetFrameRate=rate;}
        }

        [UnityTest]public IEnumerator FirstPersonEquippedPoiArePairedAndWheelInsteadOfFloating()
        {
            var cameraObject=new GameObject("First-person carried poi camera");
            try
            {
                var camera=cameraObject.AddComponent<Camera>();
                var hands=FestivalHands.Create(camera,"carry_hands_player");
                Assert.That(hands,Is.Not.Null);
                hands.SetState(new Festival.Core.PlayerState{Id="carry_hands_player",EquippedItemId="poi_practice"});
                yield return null;yield return null;
                var rigs=Object.FindObjectsByType<FestivalPoiRig>(FindObjectsSortMode.None).Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Assert.That(rigs.Length,Is.EqualTo(2),"First-person poi should be a pair, one per hand");
                var cords=rigs.Select(r=>r.GetComponent<LineRenderer>()).ToArray();
                var swing=new float[1];
                yield return HangSwing(cords,1f,swing);
                Assert.That(swing[0],Is.LessThan(25f),"Standing still, first-person poi hang below the hands");
                float low=1,high=-1,start=Time.time;
                while(Time.time-start<1.8f)
                {
                    cameraObject.transform.position+=Vector3.forward*(4f*Time.deltaTime);
                    yield return null;
                    // Skip the first second: starting to walk kicks even a hanging pendulum up.
                    if(Time.time-start<1f)continue;
                    foreach(var cord in cords){float y=(cord.GetPosition(1)-cord.GetPosition(0)).normalized.y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
                }
                Debug.Log($"[Festival.Test] first-person carried poi standing swing={swing[0]:F0}deg walking vertical={low:F2}..{high:F2}");
                Assert.That(low,Is.LessThan(-.5f));Assert.That(high-low,Is.GreaterThan(1f),"First-person heads should wheel beside the hands while walking, not hover");
            }
            finally{Object.Destroy(cameraObject);}
        }

        // POI-1: hold left click with poi in hand and you spin them. Friends see both heads circle up over the grips with the arms up
        // as in the performance, the wrists riding their loops; walking keeps stepping; let go and the poi hang again. Each window
        // spans at least one full turn (9 rad/s is .7 s), so the actor's own phase cannot flake it.
        [UnityTest]public IEnumerator HoldingClickSpinsCarriedPoiThenTheyHangOnRelease()
        {
            var root=new GameObject("Poi spin test");Time.captureDeltaTime=1/60f;
            try
            {
                var actor=Walker(root,"Poi spin actor");
                actor.SetEquippedItem("poi_led");actor.SpinningPoi=true;
                yield return new WaitForSeconds(.5f);
                var rigs=actor.GetComponentsInChildren<FestivalPoiRig>().Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Assert.That(rigs.Length,Is.EqualTo(2),"Spun poi are the pair in your hands");
                var cords=rigs.Select(r=>r.GetComponent<LineRenderer>()).ToArray();
                float low=1,high=-1,spinHands=0;int samples=0;var sweep=new Bounds(actor.transform.InverseTransformPoint(rigs[1].transform.parent.position),Vector3.zero);
                for(float start=Time.time;Time.time-start<1.5f;)
                {
                    yield return null;
                    foreach(var cord in cords){float y=(cord.GetPosition(1)-cord.GetPosition(0)).normalized.y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
                    spinHands+=Local(actor,"HandL",1)+Local(actor,"HandR",1);samples++;
                    sweep.Encapsulate(actor.transform.InverseTransformPoint(rigs[1].transform.parent.position));
                }
                spinHands/=2*samples;
                // Walking while spinning still steps.
                var leg=new List<float>();float walkLow=1,walkHigh=-1;
                for(float start=Time.time;Time.time-start<1.5f;)
                {
                    actor.transform.position+=Vector3.forward*(1.4f*Time.deltaTime);
                    yield return null;
                    if(Time.time-start<.5f)continue;
                    leg.Add(Local(actor,"FootL",2)-Local(actor,"FootR",2));
                    foreach(var cord in cords){float y=(cord.GetPosition(1)-cord.GetPosition(0)).normalized.y;walkLow=Mathf.Min(walkLow,y);walkHigh=Mathf.Max(walkHigh,y);}
                }
                bool spinning=rigs.All(r=>r.Spinning&&!r.Carrying);
                // Let go: standing, the heads settle and hang again, the hands back down at the waist.
                actor.SpinningPoi=false;
                var swing=new float[1];
                yield return new WaitForSeconds(2f);
                float hangHands=Local(actor,"HandL",1)+Local(actor,"HandR",1);hangHands/=2;
                yield return HangSwing(cords,.8f,swing);
                Debug.Log($"[Festival.Test] poi spin vertical={low:F2}..{high:F2} hands={spinHands:F2} hanging={hangHands:F2} right hand sweep={sweep.size.x:F3}/{sweep.size.y:F3}/{sweep.size.z:F3} walking vertical={walkLow:F2}..{walkHigh:F2} stride={leg.Max()-leg.Min():F2} released swing={swing[0]:F0}deg");
                Assert.That(low,Is.LessThan(-.5f));Assert.That(high,Is.GreaterThan(.5f),"Held, both heads circle up over the grips and down");
                Assert.That(spinHands-hangHands,Is.GreaterThan(.15f),"Spinning raises the arms, as in the performance");
                int directions=(sweep.size.x>=.1f?1:0)+(sweep.size.y>=.1f?1:0)+(sweep.size.z>=.1f?1:0);
                Assert.That(directions,Is.GreaterThanOrEqualTo(2),"The wrist swings round its loop with the head: "+sweep.size.ToString("F3"));
                Assert.That(spinning,Is.True,"Walking while held, the pair spin rather than wheel");
                Assert.That(walkLow,Is.LessThan(-.5f));Assert.That(walkHigh,Is.GreaterThan(.5f),"Walking while held, the heads keep circling");
                Assert.That(leg.Max()-leg.Min(),Is.GreaterThan(.2f),"Walking while spinning still steps");
                Assert.That(swing[0],Is.LessThan(20f),"Let go, standing, the poi hang again");
            }
            finally{Time.captureDeltaTime=0;Object.Destroy(root);}
        }

        [UnityTest]public IEnumerator ReceivingHandoffSuppressesHiddenPoiHandLoops()
        {
            var cameraObject=new GameObject("POI-4 hidden pair camera");Time.captureDeltaTime=1/60f;
            try
            {
                var hands=FestivalHands.Create(cameraObject.AddComponent<Camera>(),"hidden_pair_player");
                Assert.That(hands,Is.Not.Null,"actual first-person hands are available");
                var player=new PlayerState{Id="hidden_pair_player",Life="Alive",EquippedItemId="poi_practice"};
                hands.SetState(player,receiving:true,spinning:true);
                yield return new WaitForSeconds(.8f);
                var rigs=Object.FindObjectsByType<FestivalPoiRig>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                Assert.That(rigs.Length,Is.EqualTo(2),"the paired rig exists");
                Assert.That(rigs.All(r=>!r.gameObject.activeInHierarchy),Is.True,"pending handoff hides both poi");
                var sweep=new Bounds(hands.transform.localPosition,Vector3.zero);
                for(float start=Time.time;Time.time-start<1.5f;){yield return null;sweep.Encapsulate(hands.transform.localPosition);}
                Assert.That(sweep.size.x,Is.LessThan(.012f),"POI-4: hidden poi must not circle empty first-person hands");
                hands.SetState(player,spinning:true);
                yield return new WaitForSeconds(.8f);
                rigs=Object.FindObjectsByType<FestivalPoiRig>(FindObjectsSortMode.None);
                Assert.That(rigs.Length,Is.EqualTo(2));
                Assert.That(rigs.All(r=>r.gameObject.activeInHierarchy&&r.Spinning),Is.True,"visible poi resume their ordinary spin after receipt ends");
            }
            finally{Object.Destroy(cameraObject);Time.captureDeltaTime=0;}
            yield return null;
        }

        [UnityTest]public IEnumerator RidingFreezesWalkingPhaseAndKeepsDismountMotionBaselinesCurrent()
        {
            var ridingCamera=new GameObject("impl:U55 phase rider");var standingCamera=new GameObject("impl:U55 phase reference");float capture=Time.captureDeltaTime;Time.captureDeltaTime=1/60f;
            try
            {
                var rider=FestivalHands.Create(ridingCamera.AddComponent<Camera>(),"impl:U55-phase");var reference=FestivalHands.Create(standingCamera.AddComponent<Camera>(),"impl:U55-phase");
                var player=new PlayerState{Id="impl:U55-phase",EquippedItemId="poi_practice"};rider.SetState(player);reference.SetState(player);
                for(float start=Time.time;Time.time-start<.8f;)
                {var step=Vector3.forward*(1.2f*Time.deltaTime);ridingCamera.transform.position+=step;standingCamera.transform.position+=step;yield return null;}
                rider.SetState(player,riding:true);
                for(float start=Time.time;Time.time-start<.6f;)
                {
                    ridingCamera.transform.position+=Vector3.forward*(1.2f*Time.deltaTime);
                    ridingCamera.transform.rotation=standingCamera.transform.rotation=Quaternion.Euler(0,(Time.time-start)*40,0);
                    yield return null;
                }
                rider.SetState(player);yield return null;yield return null;
                float difference=Vector3.Distance(rider.transform.localPosition,reference.transform.localPosition);
                for(float start=Time.time;Time.time-start<1f;)
                {
                    var step=Vector3.forward*(1.2f*Time.deltaTime);ridingCamera.transform.position+=step;standingCamera.transform.position+=step;
                    yield return null;difference=Mathf.Max(difference,Vector3.Distance(rider.transform.localPosition,reference.transform.localPosition));
                }
                Assert.That(difference,Is.LessThan(.00005f),"riding preserves the stationary reference's walking phase and current camera baselines through dismount");
            }
            finally{Object.Destroy(ridingCamera);Object.Destroy(standingCamera);Time.captureDeltaTime=capture;}
            yield return null;
        }

        [UnityTest]public IEnumerator RidingPreservesDeliberatePoiSpinPerformanceAndReducedMotion()
        {
            var cameraObject=new GameObject("impl:U55 deliberate rider spin camera");float capture=Time.captureDeltaTime;Time.captureDeltaTime=1/60f;
            try
            {
                var hands=FestivalHands.Create(cameraObject.AddComponent<Camera>(),"impl:U55-deliberate");
                var player=new PlayerState{Id="impl:U55-deliberate",EquippedItemId="poi_practice"};
                var spans=new float[2];
                for(int mode=0;mode<2;mode++)
                {
                    hands.ReducedMotion=mode==1;hands.SetState(player,spinning:true,riding:true);yield return new WaitForSeconds(.8f);
                    var sweep=new Bounds(hands.transform.localPosition,Vector3.zero);
                    for(float start=Time.time;Time.time-start<1.2f;)
                    {cameraObject.transform.position+=Vector3.forward*(1.2f*Time.deltaTime);yield return null;sweep.Encapsulate(hands.transform.localPosition);}
                    spans[mode]=sweep.size.x;
                    Assert.That(cameraObject.GetComponentsInChildren<FestivalPoiRig>().All(r=>r.Spinning&&!r.Carrying),Is.True,"deliberate spin remains independent of riding locomotion");
                    Assert.That(spans[mode],Is.GreaterThan(.04f),"riding may suppress walking, while visible spinning still swings the hands");
                }
                Assert.That(spans[1],Is.InRange(spans[0]*.6f,spans[0]*.9f),"reduced motion still shrinks deliberate riding hand loops");
                player.VisualPose="Poi";player.EquippedItemId="";hands.SetState(player,receiving:true,riding:true);yield return new WaitForSeconds(.8f);
                var performance=new Bounds(hands.transform.localPosition,Vector3.zero);
                for(float start=Time.time;Time.time-start<1f;){yield return null;performance.Encapsulate(hands.transform.localPosition);}
                Assert.That(cameraObject.GetComponentsInChildren<FestivalPoiRig>().All(r=>r.Spinning),Is.True,"the legitimate Q performance remains visible independently of held-click intent");
                Assert.That(performance.size.x,Is.GreaterThan(.04f),"the Q performance still drives its deliberate hand loop");
            }
            finally{Object.Destroy(cameraObject);Time.captureDeltaTime=capture;}
            yield return null;
        }

        [UnityTest]public IEnumerator LittleSpoonReceiptSuppressesHiddenPoiUntilThePairReturns()
        {
            var cameraObject=new GameObject("impl:U55 receipt camera");float capture=Time.captureDeltaTime;Time.captureDeltaTime=1/60f;
            try
            {
                var hands=FestivalHands.Create(cameraObject.AddComponent<Camera>(),"impl:U55-receipt");
                var player=new PlayerState{Id="impl:U55-receipt",EquippedItemId="poi_practice"};
                hands.SetState(player,spinning:true);yield return new WaitForSeconds(.8f);
                player.VisualReceiptSequence=1;player.VisualReceivedItem="little_spoon";player.VisualReceiptAt=10;
                hands.SetState(player,10,spinning:true);yield return new WaitForSeconds(.25f);
                var rigs=cameraObject.GetComponentsInChildren<FestivalPoiRig>(true);
                Assert.That(rigs.Length,Is.EqualTo(2));Assert.That(rigs.All(r=>!r.gameObject.activeInHierarchy),Is.True,"the passive neck receipt hides equipped poi for .85 seconds");
                var sweep=new Bounds(hands.transform.localPosition,Vector3.zero);
                for(float start=Time.time;Time.time-start<.4f;){yield return null;sweep.Encapsulate(hands.transform.localPosition);}
                Assert.That(sweep.size.x,Is.LessThan(.012f),"a Little Spoon receipt cannot spin empty first-person hands");
                yield return new WaitForSeconds(.3f);hands.SetState(player,11,spinning:true);yield return null;
                rigs=cameraObject.GetComponentsInChildren<FestivalPoiRig>(true);
                Assert.That(rigs.All(r=>r.gameObject.activeInHierarchy&&r.Spinning),Is.True,"the visible equipped pair resumes after the exact receipt window");
            }
            finally{Object.Destroy(cameraObject);Time.captureDeltaTime=capture;}
            yield return null;
        }

        // POI-1: your own hands spin the poi while you hold left click, with the camera still, and they hang again once you let go.
        // Measured on a new profile's reduced-motion setting, what players get (POI-3).
        [UnityTest]public IEnumerator FirstPersonPoiSpinWhileHeld()
        {
            var cameraObject=new GameObject("First-person poi spin camera");Time.captureDeltaTime=1/60f;
            try
            {
                var hands=FestivalHands.Create(cameraObject.AddComponent<Camera>(),"spin_hands_player");
                Assert.That(hands,Is.Not.Null);
                hands.ReducedMotion=new ProfileData().ReducedMotion;
                var player=new PlayerState{Id="spin_hands_player",Life="Alive",EquippedItemId="poi_practice"};
                hands.SetState(player,spinning:true);
                yield return new WaitForSeconds(.8f);
                var rigs=Object.FindObjectsByType<FestivalPoiRig>(FindObjectsSortMode.None).Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Assert.That(rigs.Length,Is.EqualTo(2),"First-person poi are a pair, one per hand");
                var cords=rigs.Select(r=>r.GetComponent<LineRenderer>()).ToArray();
                float low=1,high=-1;var sweep=new Bounds(hands.transform.localPosition,Vector3.zero);
                for(float start=Time.time;Time.time-start<1.5f;)
                {
                    yield return null;
                    sweep.Encapsulate(hands.transform.localPosition);
                    foreach(var cord in cords){float y=(cord.GetPosition(1)-cord.GetPosition(0)).normalized.y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
                }
                hands.SetState(player);
                var swing=new float[1];
                yield return new WaitForSeconds(2f);
                yield return HangSwing(cords,1f,swing);
                float reach=Mathf.Max(sweep.size.x,sweep.size.y,sweep.size.z);
                Debug.Log($"[Festival.Test] first-person poi spin vertical={low:F2}..{high:F2} hand sweep={reach:F3} released swing={swing[0]:F0}deg");
                Assert.That(low,Is.LessThan(-.5f));Assert.That(high,Is.GreaterThan(.5f),"Held, the heads circle with the camera still");
                Assert.That(reach,Is.GreaterThanOrEqualTo(.05f),"Held, your hands swing the poi round");
                Assert.That(swing[0],Is.LessThan(25f),"Let go, standing, the poi hang again");
            }
            finally{Time.captureDeltaTime=0;Object.Destroy(cameraObject);}
        }

        [UnityTest]public IEnumerator DanceWithEquippedPoiSpinsThePairAsPartOfTheDance()
        {
            var root=new GameObject("Poi dance test");
            try
            {
                var dancer=Walker(root,"Poi dance actor");
                var plain=Walker(root,"Poi dance actor");plain.transform.position=Vector3.right*3;
                dancer.SetEquippedItem("poi_led");dancer.Pose="Dance";plain.Pose="Dance";
                yield return null;yield return null;
                var rigs=dancer.GetComponentsInChildren<FestivalPoiRig>().Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Assert.That(rigs.Length,Is.EqualTo(2),"A dancer with equipped poi spins both of them");
                var cords=rigs.Select(r=>r.GetComponent<LineRenderer>()).ToArray();
                float low=1,high=-1,feet=0,start=Time.time;
                while(Time.time-start<2f)
                {
                    yield return null;
                    foreach(var cord in cords){float y=(cord.GetPosition(1)-cord.GetPosition(0)).normalized.y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
                    // Same name, same style and phase: the footwork should match the plain dancer's.
                    feet=Mathf.Max(feet,Quaternion.Angle(Bone(dancer,"LegL").localRotation,Bone(plain,"LegL").localRotation));
                }
                float hands=Local(dancer,"HandL",1)+Local(dancer,"HandR",1),plainHands=Local(plain,"HandL",1)+Local(plain,"HandR",1);
                Debug.Log($"[Festival.Test] poi dance vertical={low:F2}..{high:F2} leg difference={feet:F1}deg hands={hands:F2} plain={plainHands:F2}");
                Assert.That(low,Is.LessThan(-.5f));Assert.That(high,Is.GreaterThan(.5f),"The poi should circle through the dance");
                Assert.That(feet,Is.LessThan(8f),"Poi change the arms, not the dance footwork");
            }
            finally{Object.Destroy(root);}
        }

        [UnityTest]public IEnumerator PoseChangesEaseInInsteadOfSnapping()
        {
            var root=new GameObject("Pose ease test");int rate=Application.targetFrameRate;Application.targetFrameRate=60;
            try
            {
                var actor=Walker(root,"Pose ease actor");
                var arm=Bone(actor,"ArmR");
                yield return new WaitForSeconds(.3f);
                var before=arm.localRotation;
                actor.Pose="Extract";
                yield return null;
                float first=Quaternion.Angle(before,arm.localRotation);
                yield return new WaitForSeconds(.12f);
                float later=Quaternion.Angle(before,arm.localRotation);
                Debug.Log($"[Festival.Test] pose ease first={first:F2} later={later:F2}");
                Assert.That(later,Is.GreaterThan(5f));
                Assert.That(later,Is.GreaterThan(first*8f),"A new pose should start slowly and accelerate");
            }
            finally{Object.Destroy(root);Application.targetFrameRate=rate;}
        }
    }

    [DefaultExecutionOrder(200)]
    public sealed class HandMotionFrameProbe:MonoBehaviour
    {
        public System.Action Observe;
        void LateUpdate(){Observe?.Invoke();}
    }

    public sealed class HandSessionMotionTests:FestivalPlayModeTest
    {
        static FestivalPoiRig[] Pair(FestivalSession session)=>session.ViewCamera.GetComponentsInChildren<FestivalPoiRig>(true);
        static FestivalHands Hands(FestivalSession session)=>session.ViewCamera.GetComponentInChildren<FestivalHands>(true);
        static IEnumerator Sweep(FestivalHands hands,float seconds,Bounds[] result)
        {
            result[0]=new Bounds(hands.transform.localPosition,Vector3.zero);
            for(float until=Time.realtimeSinceStartup+seconds;Time.realtimeSinceStartup<until;)
            {yield return null;result[0].Encapsulate(hands.transform.localPosition);}
        }

        [UnityTest]public IEnumerator MovingArtCarRideKeepsHandsAndPoiAtRestThenWalkingRecovers(){yield return ArtCarHands(false);}
        [UnityTest]public IEnumerator MovingDeckSetKeepsHandsAndPoiAtRestThenDismountRecovers(){yield return ArtCarHands(true);}
        IEnumerator ArtCarHands(bool deck)
        {
            var world=new GameObject("impl:U55 rider world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("impl:U55 rider host");var session=root.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Rider tester",HostPort);
                for(float until=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);var sim=Simulation(session);var p=sim.Player(session.LocalPlayerId);
                sim.State.FestivalIndex=Festivals.PlayaFestival;sim.State.UnlockedFestivalCount=2;
                yield return StartLevel(session);sim.State.Npcs.Clear();p.Effects.Clear();p.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});session.Command("Equip",item:"poi_practice");
                var at=Festivals.ArtCarAt(0,sim.State.ElapsedSeconds);p.X=at.X;p.Z=at.Z;session.Command(FestivalSimulation.RideCarKind);
                Assert.That(FestivalSimulation.ArtCarOf(sim.State,p.Id),Is.EqualTo(0),session.Message);
                if(deck){session.Command(FestivalSimulation.DeckSetKind);Assert.That(sim.Interaction(p.InteractionId)?.Kind,Is.EqualTo(FestivalSimulation.DeckSetKind),session.Message);}
                yield return new WaitForSeconds(.8f);
                var hands=Hands(session);Assert.That(hands.gameObject.activeInHierarchy,Is.True,"rider's actual first-person hands remain visible");
                var startEye=session.ViewCamera.transform.position;float start=Time.realtimeSinceStartup;var sweep=new Bounds[1];yield return Sweep(hands,1.8f,sweep);
                float travelled=Vector3.Distance(startEye,session.ViewCamera.transform.position);float speed=travelled/(Time.realtimeSinceStartup-start);
                Assert.That(speed,Is.InRange(.95f,1.4f),"the actual authority and session camera travel at the car's 1.2 m/s, without fixture teleports");
                Assert.That(sweep[0].size.x,Is.LessThan(.002f),"car travel must not become camera-local walking bob");
                Assert.That(Pair(session).All(r=>r.gameObject.activeInHierarchy&&!r.Carrying&&!r.Spinning),Is.True,"ridden poi hang instead of wheeling");
                var cords=Pair(session).Select(r=>r.GetComponent<LineRenderer>()).ToArray();
                Assert.That(cords.Max(c=>Vector3.Angle(c.GetPosition(1)-c.GetPosition(0),Vector3.down)),Is.LessThan(25f),"rider's weighted heads settle under gravity");
                // A completed riding frame updates both motion baselines, so stopping and looking on dismount creates no stale travel kick.
                session.ApplyLook(new Vector2(80,0));yield return new WaitForSeconds(.8f);session.Command("Cancel");yield return null;yield return null;
                Assert.That(FestivalSimulation.ArtCarOf(sim.State,p.Id),Is.EqualTo(-1));
                yield return Sweep(hands,.4f,sweep);Assert.That(sweep[0].size.x,Is.LessThan(.002f),"dismount at rest has no accumulated travel or yaw spike");
                Assert.That(Pair(session).All(r=>!r.Carrying),Is.True,"dismount at rest does not invent walking");
                // Frame-sized authoritative ground steps exercise the same camera writer at walking speed.
                var walking=new Bounds(hands.transform.localPosition,Vector3.zero);
                for(float until=Time.realtimeSinceStartup+1.8f;Time.realtimeSinceStartup<until;)
                {p.Z+=1.2f*Time.unscaledDeltaTime;yield return null;walking.Encapsulate(hands.transform.localPosition);}
                Assert.That(walking.size.x,Is.GreaterThan(.007f),"ordinary camera travel still bobs at the same 1.2 m/s after dismount");
                Assert.That(Pair(session).All(r=>r.Carrying),Is.True,"walking poi resume their ordinary carry wheels");
            }
            finally{session.Leave();Object.Destroy(root);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator JoinedPendingEquipmentClearsOnTheFirstRenderedRideWithoutWalkingPoi()
        {
            UseAVirtualKeyboard("impl:U55 pending rider keyboard");
            var world=new GameObject("impl:U55 pending rider world");world.AddComponent<FestivalWorld>();yield return null;
            var hostRoot=new GameObject("impl:U55 pending rider authority");var host=hostRoot.AddComponent<FestivalSession>();yield return null;
            var friendRoot=new GameObject("impl:U55 pending rider owner");var friend=friendRoot.AddComponent<FestivalSession>();var hud=friendRoot.AddComponent<FestivalHud>();yield return null;
            var nextSnapshot=typeof(FestivalSession).GetField("nextSnapshot",BindingFlags.NonPublic|BindingFlags.Instance);
            var pending=typeof(FestivalHud).GetField("pendingEquipment",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                ushort port=FreePort();host.Host("Host",port);
                for(float until=Time.realtimeSinceStartup+30;host.LocalPlayer==null&&Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);host.Controls.Map.Disable();
                var sim=Simulation(host);sim.State.FestivalIndex=Festivals.PlayaFestival;sim.State.UnlockedFestivalCount=2;
                // A joined owner uses a real issued Equip, result, stale view and subsequent riding snapshot.
                friend.Join("Rider","127.0.0.1",port);
                for(float until=Time.realtimeSinceStartup+30;friend.LocalPlayer==null&&Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,friend.Message);var p=sim.Player(friend.LocalPlayerId);p.X=0;p.Z=19;friend.Command("Ready");yield return new WaitForSeconds(.2f);yield return StartLevel(host,friend.LocalPlayerId);
                sim.State.Npcs.Clear();p.Effects.Clear();p.Inventory.Clear();p.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});p.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});p.EquippedItemId="confetti";
                var at=Festivals.ArtCarAt(0,sim.State.ElapsedSeconds);p.X=at.X;p.Z=at.Z;yield return new WaitForSeconds(.3f);
                for(float until=Time.realtimeSinceStartup+1;Time.realtimeSinceStartup<until;)
                {at=Festivals.ArtCarAt(0,sim.State.ElapsedSeconds);p.X=at.X;p.Z=at.Z;yield return null;}
                Assert.That(friend.LocalPlayer.Inventory.Count,Is.EqualTo(2));Assert.That(friend.LocalPlayer.EquippedItemId,Is.EqualTo("confetti"));
                nextSnapshot.SetValue(host,Time.realtimeSinceStartupAsDouble+1000);yield return Tap(Key.Digit2,.1f);
                Assert.That(p.EquippedItemId,Is.EqualTo("poi_practice"));Assert.That(friend.LocalPlayer.EquippedItemId,Is.EqualTo("confetti"));Assert.That(pending.GetValue(hud),Is.True,"real accepted intent remains pending against the stale view");
                Assert.That(sim.Execute(p.Id,new GameCommand{Id="impl:U55-pending-board",Kind=FestivalSimulation.RideCarKind}).Accepted,Is.True);
                bool sampled=false,carrying=false,cleared=false;var probe=friendRoot.AddComponent<HandMotionFrameProbe>();
                probe.Observe=()=>
                {
                    if(sampled||friend.State==null||FestivalSimulation.ArtCarOf(friend.State,p.Id)<0)return;
                    sampled=true;carrying=Pair(friend).Any(r=>r.Carrying);cleared=!(bool)pending.GetValue(hud);
                };
                nextSnapshot.SetValue(host,0d);
                for(float until=Time.realtimeSinceStartup+3;!sampled&&Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(sampled,Is.True,"the real joined ride reaches a completed production LateUpdate frame");
                Assert.That(cleared,Is.True,"the actual HUD retires pending intent on the first ride view");
                Assert.That(carrying,Is.False,"the same rendered frame keeps riding context when HUD clears the pending hand override");
                probe.Observe=null;
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendRoot);Object.Destroy(hostRoot);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator HeldMouseHandoffAndLittleSpoonReceiptKeepHiddenHandsStill()
        {
            UseAVirtualMouse("impl:U55 handoff mouse");
            var world=new GameObject("impl:U55 handoff world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("impl:U55 handoff host");var session=root.AddComponent<FestivalSession>();root.AddComponent<FestivalHud>();yield return null;
            void Hold(bool down)=>InputSystem.QueueStateEvent(TestMouse,new MouseState().WithButton(MouseButton.Left,down));
            try
            {
                session.Host("Hands tester",HostPort);
                for(float until=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);var friend=sim.AddPlayer("impl:U55-donor","Sam");friend.Ready=true;
                yield return StartLevel(session);sim.State.Npcs.Clear();player.Effects.Clear();player.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});session.Command("Equip",item:"poi_practice");
                friend.X=player.X+1;friend.Z=player.Z;friend.Inventory.Add(new ItemStack{ItemId="little_spoon",Count=1});
                Hold(true);yield return new WaitForSeconds(.8f);
                Assert.That(player.VisualPoiSpin,Is.True,"the real input/host path sees the held mouse");Assert.That(Pair(session).All(r=>r.gameObject.activeInHierarchy&&r.Spinning),Is.True);
                Assert.That(sim.Execute(friend.Id,new GameCommand{Id="impl:U55-offer-spoon",Kind="Transfer",TargetId=player.Id,ItemId="little_spoon",Amount=1}).Accepted,Is.True);
                yield return new WaitForSeconds(.3f);
                Assert.That(session.State.Transfers.Count,Is.EqualTo(1));Assert.That(Pair(session).All(r=>!r.gameObject.activeInHierarchy),Is.True,"actual pending transfer hides the pair");
                var sweep=new Bounds[1];yield return Sweep(Hands(session),.6f,sweep);
                Assert.That(sweep[0].size.x,Is.LessThan(.012f),"real pending input cannot animate hidden poi");
                session.Command("AcceptTransfer",sim.State.Transfers[0].Id);yield return new WaitForSeconds(.25f);
                Assert.That(player.VisualReceivedItem,Is.EqualTo("little_spoon"));Assert.That(player.EquippedItemId,Is.EqualTo("poi_practice"));
                Assert.That(Pair(session).All(r=>!r.gameObject.activeInHierarchy),Is.True,"real Little Spoon receipt temporarily wins over equipped poi");
                yield return Sweep(Hands(session),.35f,sweep);
                Assert.That(sweep[0].size.x,Is.LessThan(.012f),"real receipt input cannot animate hidden poi");
                yield return new WaitForSeconds(.5f);
                Assert.That(Pair(session).All(r=>r.gameObject.activeInHierarchy&&r.Spinning),Is.True,"the visible pair resumes while the mouse is still held");
                Hold(false);yield return new WaitForSeconds(.2f);Assert.That(Pair(session).All(r=>!r.Spinning),Is.True,"release still stops the real spin");
            }
            finally{Hold(false);session.Leave();Object.Destroy(root);Object.Destroy(world);}
            yield return null;
        }
    }
}
