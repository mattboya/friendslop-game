using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // SPIN-1 in a running scene: the slot machine (SPIN-3) covers the screen, then the camera cuts to the tripper taking the dose and reacting.
    public sealed class SpinnerTakeTests:FestivalPlayModeTest
    {
        static Transform Bone(FestivalCharacter actor,string name)=>actor.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static float Height(FestivalCharacter actor,string bone)=>actor.transform.InverseTransformPoint(Bone(actor,bone).position).y;

        [UnityTest]public IEnumerator TheTripperTakesTheDoseThenReactsByTheDose()
        {
            var root=new GameObject("Dose beat test");
            try
            {
                FestivalCharacter Actor(string name,float x)
                {
                    var actor=FestivalCharacter.Create(root.transform,name,Color.white);
                    actor.transform.localScale=Vector3.one*.82f;actor.transform.position=Vector3.right*x;actor.Pose="Intoxicated";return actor;
                }
                var sober=Actor("Beat reference",0);var taker=Actor("Beat taker",2.5f);var mild=Actor("Beat mild",5);var wild=Actor("Beat wild",7.5f);
                // POI-1: poi in hand (buying them equips them), even with a click held through the slot machine, never take the arms
                // off the take or the reaction. The pair is put away for the beat and comes back after it.
                var poiTaker=Actor("Beat poi taker",10);var spinTaker=Actor("Beat spinning taker",12.5f);var poiWild=Actor("Beat poi wild",15);
                foreach(var holder in new[]{poiTaker,spinTaker,poiWild})holder.SetEquippedItem("poi_led");
                spinTaker.SpinningPoi=true;
                int PoiShown(FestivalCharacter actor)=>actor.GetComponentsInChildren<FestivalPoiRig>().Length;
                taker.Beat=poiTaker.Beat=spinTaker.Beat="TakeDose";
                yield return new WaitForSeconds(.8f);
                float Reach(FestivalCharacter actor)=>Vector3.Distance(Bone(actor,"HandR").position,Bone(actor,"Head").position);
                foreach(var actor in new[]{taker,poiTaker,spinTaker})
                {
                    Debug.Log($"[Festival.Test] dose take {actor.name} reach={Reach(actor):F3} sober={Reach(sober):F3} hand={Height(actor,"HandR"):F3} sober hand={Height(sober,"HandR"):F3}");
                    Assert.That(Reach(actor),Is.LessThan(Reach(sober)*.65f),"the take brings a hand up to the face: "+actor.name);
                    Assert.That(Height(actor,"HandR"),Is.GreaterThan(Height(sober,"HandR")+.35f),"from hanging at the hip: "+actor.name);
                    Assert.That(PoiShown(actor),Is.Zero,"no poi in hand for the take: "+actor.name);
                }
                mild.Beat=wild.Beat=poiWild.Beat="DoseReaction";mild.BeatStrength=.25f;wild.BeatStrength=poiWild.BeatStrength=1;
                yield return new WaitForSeconds(.8f);
                float Hands(FestivalCharacter actor)=>(Height(actor,"HandL")+Height(actor,"HandR"))*.5f;
                Debug.Log($"[Festival.Test] dose reaction hands mild={Hands(mild):F3} wild={Hands(wild):F3} poi wild={Hands(poiWild):F3} sober={Hands(sober):F3} shoulders={Height(wild,"ArmL"):F3} head={Height(wild,"Head"):F3}");
                Assert.That(Hands(wild),Is.GreaterThan(Height(wild,"ArmL")-.1f),"four doses flings both arms out to shoulder height");
                Assert.That(Hands(poiWild),Is.GreaterThan(Height(poiWild,"ArmL")-.1f),"poi in hand too");
                Assert.That(PoiShown(poiWild),Is.Zero,"no poi in hand for the reaction");
                Assert.That(Hands(wild),Is.GreaterThan(Hands(mild)+.25f),"one dose is a much smaller reaction");
                Assert.That(Hands(mild),Is.GreaterThan(Hands(sober)),"but still shows");
                poiWild.Beat="";yield return null;
                Assert.That(PoiShown(poiWild),Is.EqualTo(2),"once the beat ends the dosed player holds their poi again");
            }
            finally{Object.Destroy(root);}
        }

        [UnityTest]public IEnumerator EveryoneWatchesTheWheelsThenTheCameraCutsToTheTripper()
        {
            var world=new GameObject("Spinner world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Spinner session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("spin_mate","Mate");
                player.X=0;player.Z=19;mate.X=1.5f;mate.Z=19;mate.Ready=true;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+30;
                while(session.State.Phase!="Spinning"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Spinning"),"setup: the ready countdown ends in the spin: "+session.Message);
                double start=session.State.SpinEndsAt-FestivalSimulation.SpinSeconds;
                string tripper=session.State.TripperId;var spun=session.State.Doses.Find(d=>d.PlayerId==tripper);int dose=spun.Dose;
                string took=Catalog.FindEffect(spun.Substance).Name.ToUpperInvariant();
                yield return new WaitForSeconds(.3f);

                var slotSound=host.GetComponents<AudioSource>().FirstOrDefault(source=>source.clip!=null&&source.clip.name.StartsWith("Original slot "));
                Assert.That(slotSound,Is.Not.Null,"the real session spinner follows its public clock into local audio");
                Assert.That(slotSound.volume,Is.EqualTo(FestivalSettings.EffectsGain(session.Profile.Data,.45f)).Within(.001),"the live source follows the effects slider");
                var overlay=host.transform.Find("Festival spinner");
                Assert.That(overlay!=null&&overlay.gameObject.activeInHierarchy,"the slot machine covers the screen during the spin");
                var shown=overlay.GetComponentsInChildren<Text>(false).Select(t=>t.text).ToList();
                Assert.That(shown,Is.SupersetOf(new[]{"WHO TRIPS?","HOW MANY DOSES?","WHAT DID THEY TAKE?"}),"each reel window is headed with its question");
                List<string> Tiles(string reel)
                {
                    var strip=overlay.Find("Spinner backdrop/Cabinet/"+reel+"/Window/Strip");
                    Assert.That(strip,Is.Not.Null,"the cabinet's "+reel+" scrolls a strip of tiles in its window");
                    return strip.GetComponentsInChildren<Text>(false).Select(t=>t.text).ToList();
                }
                Assert.That(Tiles("People reel"),Does.Contain("TESTER").And.Contain("MATE"),"the people reel has a tile for each friend");
                Assert.That(Tiles("Dose reel"),Does.Contain("1").And.Contain("4"),"the dose reel holds its doses, the rare 4 included");
                Assert.That(Tiles("Substance reel"),Is.SupersetOf(new[]{"TONGUE STAMPS","FUN GUYS","ROLLY POLLIES","PONY DUST","COUCH LOCK"}),"the substance reel holds all five");

                string[] Results()=>new[]{"People reel","Dose reel","Substance reel"}.Select(r=>overlay.Find("Spinner backdrop/Cabinet/"+r+"/Result").GetComponent<Text>().text).ToArray();
                Assert.That(Results(),Is.EqualTo(new[]{"","",""}),"nothing reads out while the reels spin");

                while(session.EstimatedSimulationSeconds<start+FestivalSpinner.LastReelStops+.2&&Time.realtimeSinceStartup<deadline)yield return null;
                string name=sim.Player(tripper).Name.ToUpperInvariant();
                Assert.That(Results(),Is.EqualTo(new[]{name+" TRIPS",dose+(dose==1?" DOSE":" DOSES"),took}),"all three results read out under their reels once the last reel stops");
                // The tile nearest the payline in each window is the result's own, resting centred on it.
                var payline=(RectTransform)overlay.Find("Spinner backdrop/Cabinet/Payline");
                void OnPayline(string reel,string label)
                {
                    var tile=overlay.Find("Spinner backdrop/Cabinet/"+reel+"/Window/Strip").Cast<Transform>().OrderBy(t=>Mathf.Abs(t.position.y-payline.position.y)).First();
                    Assert.That(tile.GetComponentInChildren<Text>().text,Is.EqualTo(label),"the "+reel+" rests on "+label);
                    Assert.That(Mathf.Abs(tile.position.y-payline.position.y),Is.LessThan(.5f),"centred on the payline");
                }
                OnPayline("People reel",name);OnPayline("Dose reel",dose.ToString());OnPayline("Substance reel",took);

                while(session.EstimatedSimulationSeconds<start+FestivalSpinner.TakeStarts+.4&&Time.realtimeSinceStartup<deadline)yield return null;
                var cut=host.GetComponentsInChildren<Camera>(true).First(c=>c.name=="Spinner take camera");
                var actor=session.WorldCharacter(tripper);
                Assert.That(cut.enabled,"the camera cuts to the tripper after the reels stop");
                var shot=overlay.Find("Take letterbox/Take shot").GetComponent<RawImage>();
                Assert.That(shot.enabled&&shot.texture!=null&&shot.texture==cut.targetTexture,"the shot fills the spinner's own canvas, over the HUD");
                Assert.That(actor.Beat,Is.EqualTo("TakeDose"),"the tripper takes the dose");
                Assert.That(overlay.Find("Take letterbox/Bottom bar/Take caption").GetComponent<Text>().text,Is.EqualTo(name+" TAKES "+dose+(dose==1?" DOSE":" DOSES")+" OF "+took),"the caption names what they take");
                var toFace=actor.transform.position+Vector3.up*1.25f-cut.transform.position;
                Assert.That(Vector3.Angle(cut.transform.forward,toFace),Is.LessThan(12f),"the shot frames the tripper");
                Assert.That(toFace.magnitude,Is.LessThan(3.5f),"close up");

                while(session.EstimatedSimulationSeconds<start+FestivalSpinner.ReactStarts+.3&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(actor.Beat,Is.EqualTo("DoseReaction"),"then reacts");
                Assert.That(actor.BeatStrength,Is.EqualTo(dose/4f),"sized by the dose");

                while(session.State.Phase=="Spinning"&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return null;yield return null;
                Assert.That(session.State.Phase,Is.Not.EqualTo("Spinning"),"setup: the spin ends");
                Assert.That(overlay.gameObject.activeSelf,Is.False,"the slot machine clears for Loading");
                Assert.That(cut.enabled,Is.False,"the first-person view returns");
                Assert.That(cut.targetTexture,Is.Null,"and the shot's texture is let go");
                Assert.That(actor.Beat,Is.Empty,"the tripper goes back to the session's pose");
            }
            finally
            {
                session.Leave();
                foreach(var leftover in new[]{"First-person camera","Authoritative actor presentation"}){var found=GameObject.Find(leftover);if(found!=null)Object.Destroy(found);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        // Everyone readies within a few metres of the gate, so a friend often stands between the tripper's face and a head-on camera.
        [UnityTest]public IEnumerator FriendsAtTheGateNeverBlockTheTake()
        {
            var world=new GameObject("Spinner world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Spinner session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                // The host and the mate face each other 1.5 m apart and the mate is picked; a third friend stands on the first swing's line.
                var me=sim.Player(session.LocalPlayerId);me.X=0;me.Z=19;
                var mate=sim.AddPlayer("spin_mate","Mate");mate.X=0;mate.Z=20.5f;mate.Yaw=180;
                var third=sim.AddPlayer("spin_third","Third");third.X=-.65f;third.Z=19.4f;
                yield return new WaitForSeconds(1f);
                var state=sim.State;state.Phase="Spinning";state.SpinSeed=4242;state.TripperId=mate.Id;
                state.Doses.Clear();state.Doses.Add(new PlayerDose{PlayerId=mate.Id,Dose=3});
                var cut=host.GetComponentsInChildren<Camera>(true).First(c=>c.name=="Spinner take camera");
                foreach(var at in new[]{FestivalSpinner.TakeStarts+.3,FestivalSpinner.ReactStarts+.5})
                {
                    state.SpinEndsAt=state.SimulationSeconds+FestivalSimulation.SpinSeconds-at;
                    yield return new WaitForSeconds(.3f);
                    Assert.That(cut.enabled,"setup: the camera cuts to the mate at "+at+" s");
                    var eye=cut.transform.position;var tripper=session.WorldCharacter(mate.Id).transform;var face=tripper.position+Vector3.up*1.4f;
                    Assert.That(Vector3.Angle(cut.transform.forward,face-eye),Is.LessThan(12f),"the shot frames the mate at "+at+" s");
                    Assert.That(Vector3.Dot(tripper.forward,eye-tripper.position),Is.GreaterThan(0),"from the front, so it shows their face");
                    foreach(var friend in new[]{me.Id,third.Id})
                    {
                        var body=session.WorldCharacter(friend).transform;var head=body.position+Vector3.up*1.4f;var line=face-eye;
                        var nearest=eye+line*Mathf.Clamp01(Vector3.Dot(head-eye,line)/line.sqrMagnitude);
                        Debug.Log($"[Festival.Test] take at {at} s: camera={eye} {friend} head off the line={Vector3.Distance(head,nearest):F2} m");
                        Assert.That(Vector3.Distance(head,nearest),Is.GreaterThan(.35f),friend+"'s head stays out of the shot at "+at+" s");
                        Assert.That(Vector2.Distance(new Vector2(eye.x,eye.z),new Vector2(body.position.x,body.position.z)),Is.GreaterThan(.35f),"the camera is not inside "+friend);
                    }
                }
            }
            finally
            {
                session.Leave();
                foreach(var leftover in new[]{"First-person camera","Authoritative actor presentation"}){var found=GameObject.Find(leftover);if(found!=null)Object.Destroy(found);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        // The trailhead posts (x = +-2, z = 19) stand inside the 3.2 m circle where everyone readies, so a tripper who stops
        // just in front of one, facing the gate, has a post on the head-on camera's line. In the last three the head-on line
        // misses the post by 1-2 cm, yet the post still covers the side of the face; the other friend stands well clear.
        // The live cabinet blinks its whole ring from the last stop until the take: fast, or under 3 times a second with reduced motion.
        [UnityTest]public IEnumerator TheRingBlinksAfterTheLastStopAndGentlyUnderReducedMotion()
        {
            var world=new GameObject("Spinner world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Spinner session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            bool reduced=false;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                reduced=session.Profile.Data.ReducedMotion;
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var mate=sim.AddPlayer("spin_mate","Mate");
                yield return new WaitForSeconds(.5f);
                var cabinet=host.transform.Find("Festival spinner/Spinner backdrop/Cabinet");
                var bulbs=Enumerable.Range(0,FestivalSpinner.Bulbs).Select(i=>cabinet.Find("Bulb "+i).GetComponent<Image>()).ToList();
                var state=sim.State;string idle=state.Phase;
                double last=FestivalSpinner.LastReelStops,take=FestivalSpinner.TakeStarts;
                foreach(bool gentle in new[]{false,true})
                {
                    session.Profile.Data.ReducedMotion=gentle;
                    state.Phase="Spinning";state.SpinSeed=gentle?4243:4242;state.TripperId=mate.Id;
                    state.Doses.Clear();state.Doses.Add(new PlayerDose{PlayerId=mate.Id,Dose=2});
                    state.SpinEndsAt=state.SimulationSeconds+FestivalSimulation.SpinSeconds-(last-.3);
                    double start=state.SpinEndsAt-FestivalSimulation.SpinSeconds;
                    int flashes=0,frames=0;bool on=false,dark=false;
                    while(session.EstimatedSimulationSeconds-start<take-.05&&Time.realtimeSinceStartup<deadline)
                    {
                        yield return null;
                        double elapsed=session.EstimatedSimulationSeconds-start;
                        if(elapsed<last+.05||elapsed>=take-.05)continue;
                        int lit=bulbs.Count(b=>b.color==bulbs[0].color);frames++;
                        Assert.That(lit,Is.EqualTo(bulbs.Count),"the whole ring blinks together after the last stop, at "+elapsed.ToString("F2")+" s");
                        bool gold=bulbs[0].color.r>.9f;if(gold&&!on)flashes++;on=gold;dark|=!gold;
                    }
                    string mode=gentle?"reduced motion: ":"";
                    Assert.That(frames,Is.GreaterThan(20),mode+"setup: the frames sample the blink");
                    Assert.That(dark,mode+"the ring blinks, not just stays lit");
                    if(gentle)Assert.That(flashes,Is.InRange(1,2),"reduced motion blinks the ring under 3 times a second");
                    else Assert.That(flashes,Is.GreaterThanOrEqualTo(5),"the ring blinks fast after the last stop");
                }
                state.Phase=idle;
            }
            finally
            {
                session.Profile.Data.ReducedMotion=reduced;session.Leave();
                foreach(var leftover in new[]{"First-person camera","Authoritative actor presentation"}){var found=GameObject.Find(leftover);if(found!=null)Object.Destroy(found);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        [UnityTest]public IEnumerator TrailheadPostsNeverBlockTheTake()
        {
            var world=new GameObject("Spinner world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Spinner session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var me=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("spin_mate","Mate");
                var state=sim.State;string idle=state.Phase;
                var cut=host.GetComponentsInChildren<Camera>(true).First(c=>c.name=="Spinner take camera");
                foreach(var (x,z,yaw,otherX,otherZ) in new[]{(1.95f,17.3f,0f,0f,17f),(2.6f,17.5f,-20f,-1f,18f),(-1.2f,17.6f,-30f,1f,18f),
                    (2.62f,17.52f,-17f,0f,12f),(-1.86f,17.23f,0f,0f,12f),(2.04f,16.81f,-4.5f,0f,12f)})
                {
                    state.Phase=idle;mate.X=x;mate.Z=z;mate.Yaw=yaw;me.X=otherX;me.Z=otherZ;me.Yaw=180;
                    yield return new WaitForSeconds(1f);
                    state.Phase="Spinning";state.SpinSeed=4242;state.TripperId=mate.Id;
                    state.Doses.Clear();state.Doses.Add(new PlayerDose{PlayerId=mate.Id,Dose=3});
                    foreach(var at in new[]{FestivalSpinner.TakeStarts+.3,FestivalSpinner.ReactStarts+.8})
                    {
                        state.SpinEndsAt=state.SimulationSeconds+FestivalSimulation.SpinSeconds-at;
                        yield return new WaitForSeconds(.3f);
                        string where=$"for a tripper at ({x},{z}) facing {yaw} at {at} s";
                        Assert.That(cut.enabled,"setup: the camera cuts to the mate "+where);
                        var eye=cut.transform.position;var face=session.WorldCharacter(mate.Id).transform.position+Vector3.up*1.4f;
                        Assert.That(Vector3.Angle(cut.transform.forward,face-eye),Is.LessThan(12f),"the shot frames the mate "+where);
                        string Blocker(Vector3 from,Vector3 to)=>Physics.Linecast(from,to,out var hit,~0,QueryTriggerInteraction.Ignore)?hit.collider.name:"nothing";
                        // The whole face, edges included: a line to its centre can clear a post that still covers half of it.
                        var right=cut.transform.right*.11f;var up=cut.transform.up*.13f;
                        foreach(var point in new[]{face,face-right,face+right,face-up,face+up})
                        {
                            Debug.Log($"[Festival.Test] post take {where}: camera={eye} face point={point} blocked by {Blocker(eye,point)} / {Blocker(point,eye)}");
                            Assert.That(Blocker(eye,point),Is.EqualTo("nothing"),"nothing stands between the camera and the mate's face at "+point+" "+where);
                            Assert.That(Blocker(point,eye),Is.EqualTo("nothing"),"and the camera is not inside anything "+where);
                        }
                    }
                }
                state.Phase=idle;
            }
            finally
            {
                session.Leave();
                foreach(var leftover in new[]{"First-person camera","Authoritative actor presentation"}){var found=GameObject.Find(leftover);if(found!=null)Object.Destroy(found);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
