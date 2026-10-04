using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;
using Festival.Presentation;
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
}
