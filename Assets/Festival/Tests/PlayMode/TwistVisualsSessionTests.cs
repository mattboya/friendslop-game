using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;

namespace Festival.Tests
{
    public sealed class TwistVisualsSessionTests:FestivalPlayModeTest
    {
        // TWISTVIS-1: in a hosted Palm Mirage level the running game raises the festival's twists. POLO-6: every influencer holds a
        // phone up in its right hand, its red light blinking, and draws no frame on the ground. Once you step into one's shot, that
        // one's light stays lit and "• REC" floats over its head on your screen; the others' don't.
        [UnityTest]public IEnumerator APalmMirageLevelShowsItsTwistsInTheRunningGame()
        {
            var world=new GameObject("Twist world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Twist session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                // The session drives the first world it finds, so a world leaked by an earlier test would take the twists.
                Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsSortMode.None),Has.Length.EqualTo(1),"setup: a world leaked from an earlier test");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                Assert.That(sim.State.FestivalIndex,Is.EqualTo(Festivals.PoloFestival),"setup: a new game starts at Palm Mirage");

                yield return StartLevel(session);
                yield return null;yield return null;

                var grounds=world.transform.Find(FestivalWorld.RootName);
                var polo=grounds.Find(FestivalTwistVisuals.PoloRootName);
                Assert.That(polo!=null&&polo.gameObject.activeInHierarchy,Is.True,"Palm Mirage's ropes and wheel stand at the festival");
                Assert.That(grounds.Find(FestivalTwistVisuals.PlayaRootName).gameObject.activeInHierarchy,Is.False,"Ember Playa's art cars and effigy are not here");
                var influencers=session.State.Npcs.FindAll(n=>n.Twist==FestivalSimulation.Influencer);
                Assert.That(influencers.Count,Is.EqualTo(Festivals.Influencers),"setup: the host's view carries every influencer");
                foreach(Transform child in grounds)Assert.That(child.name,Does.Not.StartWith("Influencer frame "),"no frame on the ground");
                Transform Part(string actor,string name){var body=GameObject.Find(actor);if(body!=null)foreach(var t in body.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
                foreach(var n in session.State.Npcs)
                {
                    var phone=Part(n.Id,FestivalFilmingPhone.PhoneName);bool influencer=n.Twist==FestivalSimulation.Influencer;
                    Assert.That(phone!=null&&phone.gameObject.activeInHierarchy,Is.EqualTo(influencer),n.Id+(influencer?" films with a phone":" has no phone out"));
                    if(!influencer)continue;
                    Assert.That(phone.parent.name,Is.EqualTo("HandR"),n.Id+" holds it in the right hand");
                    Assert.That(phone.position.y,Is.GreaterThan(Part(n.Id,"ArmR").position.y),n.Id+" holds it up above the shoulder");
                    Assert.That(Part(n.Id,FestivalFilmingPhone.LabelName)?.gameObject.activeInHierarchy??false,Is.False,n.Id+" doesn't have you on camera yet");
                }

                // You step into an influencer's shot, close in front of its phone where nothing stands between you.
                var me=sim.Player(session.LocalPlayerId);NpcState cam=null;
                foreach(var n in influencers)foreach(float ahead in new[]{1.5f,2.5f,4f})
                {
                    if(cam!=null)break;
                    float yaw=n.Yaw*Mathf.Deg2Rad;me.X=n.X+Mathf.Sin(yaw)*ahead;me.Z=n.Z+Mathf.Cos(yaw)*ahead;
                    if(sim.Films(n,me))cam=n;
                }
                Assert.That(cam,Is.Not.Null,"setup: you stand in an influencer's shot");
                var other=influencers.Find(n=>!sim.Films(n,me));
                Assert.That(other,Is.Not.Null,"setup: another influencer doesn't have you");
                deadline=Time.realtimeSinceStartup+5;
                while(!session.LocalPlayer.VisualFilmedBy.Contains(cam.Id)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer.VisualFilmedBy,Does.Contain(cam.Id),"setup: your view says "+cam.Id+" has you on camera");
                yield return null;
                var light=Part(cam.Id,FestivalFilmingPhone.LightName).GetComponent<Renderer>();var otherLight=Part(other.Id,FestivalFilmingPhone.LightName).GetComponent<Renderer>();
                var label=Part(cam.Id,FestivalFilmingPhone.LabelName);var otherLabel=Part(other.Id,FestivalFilmingPhone.LabelName);
                bool steady=true,shown=true,otherOn=false,otherOff=false,otherShown=false;
                for(float until=Time.time+1.2f;Time.time<until;)
                {
                    steady&=light.enabled;shown&=label.gameObject.activeInHierarchy;
                    otherOn|=otherLight.enabled;otherOff|=!otherLight.enabled;otherShown|=otherLabel!=null&&otherLabel.gameObject.activeInHierarchy;
                    yield return null;
                }
                Assert.That(steady,Is.True,cam.Id+"'s light stays lit while it has you on camera");
                Assert.That(shown,Is.True,"and REC shows over it");
                Assert.That(label.GetComponent<TextMeshPro>().text,Is.EqualTo("• REC"));
                Assert.That(otherOn&&otherOff,Is.True,other.Id+", not filming you, still blinks");
                Assert.That(otherShown,Is.False,"and shows no REC");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        // MINI-5: the running recipient view keeps the phone recording throughout the game and its result banner.
        [UnityTest]public IEnumerator AFilmingPhoneKeepsRecordingDuringTheGameAndItsBanner()
        {
            var world=new GameObject("impl:U23 camera world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U23 camera host");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
                var sim=Simulation(session);var me=sim.Player(session.LocalPlayerId);var cam=sim.State.Npcs.Find(n=>n.Twist==FestivalSimulation.Influencer);
                Assert.That(cam,Is.Not.Null);sim.State.Npcs.RemoveAll(n=>n!=cam);me.Effects.Clear();
                sim.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;var at=FestivalSimulation.BoothAt(sim.State);Assert.That(at,Is.Not.Null);
                me.X=at.X+2;me.Z=at.Z;cam.X=me.X;cam.Z=me.Z-1.5f;cam.Yaw=0;
                Assert.That(sim.Films(cam,me),Is.True,"setup: the production frame and line of sight put you on camera");
                sim.State.Booth.RingSeconds=FestivalSimulation.MinigameRingSeconds-.01;sim.Tick(.02);cam.Yaw=0;
                Assert.That(sim.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"the real ring countdown starts the game");
                yield return new WaitForSecondsRealtime(.4f);
                Transform Part(string name){var body=GameObject.Find(cam.Id);if(body!=null)foreach(var t in body.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
                void Recording(string stage)
                {
                    var label=Part(FestivalFilmingPhone.LabelName);Assert.That(label,Is.Not.Null);
                    Assert.That(label.gameObject.activeInHierarchy,Is.True,stage+": the phone still shows REC in the frozen frame");
                    Assert.That(label.GetComponent<TextMeshPro>().text,Is.EqualTo("• REC"));
                    Assert.That(Part(FestivalFilmingPhone.LightName).GetComponent<Renderer>().enabled,Is.True,stage+": the recording light stays lit");
                    Assert.That(session.LocalPlayer.VisualFilmedBy,Does.Contain(cam.Id));
                    Assert.That(FestivalHudText.Condition(session.State,session.LocalPlayer),Does.StartWith("ON CAMERA • "));
                    Assert.That(sim.Films(cam,me),Is.False,stage+": the camera flag never reactivates danger");
                }
                Recording("game");var heat=cam.Observers.ConvertAll(o=>o.Suspicion);
                sim.Execute(me.Id,new GameCommand{Id="impl:U23-call",Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.CallCups});
                Assert.That(sim.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0),"the production command starts the result banner");
                yield return new WaitForSecondsRealtime(.4f);Recording("result banner");
                Assert.That(cam.Observers.ConvertAll(o=>o.Suspicion),Is.EqualTo(heat),"danger stays frozen through the game and banner");
                Assert.That(Time.timeScale,Is.Zero);
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }

        // POLO-5: friends watch a ride from the ground, so every rider sits inside their gondola as it goes round, under its roof,
        // alone or beside a friend, whatever their body's shape (FestivalAppearance.Shape 0, 1 or 2, the last the widest and deepest).
        // Three crews of two put each shape on each side of a friend (riders sharing a gondola sit in id order), and the deepest look
        // rides alone: shape 2 in a skirt (deeper than the tutu, and longer), with a backpack. Player_1 wears the tallest seated
        // look (shape 0 in hat 1), and the widest (shape 2 in a tutu, wider than the skirt, with accessory 1, which reaches
        // furthest out) sits beside them. About 1 in 20 wears a skirt or tutu (LOOK-1), so those two are found by their look
        // (Rider). Each crew boards together, a gondola apart, and the turn is moved on by starting their rides earlier, so two
        // crews are at the top.
        [UnityTest]public IEnumerator EveryBodySitsInsideItsGondolaAloneOrBesideAFriend()
        {
            var world=new GameObject("Wheel world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Wheel session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            var failures=new List<string>();
            try
            {
                Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsSortMode.None),Has.Length.EqualTo(1),"setup: a world leaked from an earlier test");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                // "wide_rider_…" sorts after "player_1", so the widest takes the second seat, as shape 2 does in no other crew.
                string wideId=Rider("wide_rider_",3,1),deepId=Rider("deep_rider_",2,0);
                var crews=new[]{new[]{"player_1",wideId},new[]{"player_11","twist_mate"},new[]{"look_b2","player_9"},new[]{deepId}};
                var shapes=new[]{new[]{0,2},new[]{2,1},new[]{1,0},new[]{2}};
                for(int c=0;c<crews.Length;c++)for(int r=0;r<crews[c].Length;r++)
                {
                    Assert.That(FestivalAppearance.For(crews[c][r]).Shape,Is.EqualTo(shapes[c][r]),"setup: "+crews[c][r]+"'s body shape");
                    sim.AddPlayer(crews[c][r],crews[c][r]).Ready=true;
                }
                var deep=FestivalAppearance.For(deepId);
                Assert.That(deep.Pants==2&&deep.Accessory==0,Is.True,"setup: the lone rider wears a skirt and a backpack");
                var tall=FestivalAppearance.For("player_1");var wide=FestivalAppearance.For(wideId);
                Assert.That(tall.Headgear==1&&wide.Pants==3&&wide.Accessory==1,Is.True,"setup: the tallest look wears hat 1, and the widest a tutu and accessory 1");
                yield return StartLevel(session);
                for(int c=0;c<crews.Length;c++)foreach(var id in crews[c])
                {
                    var rider=sim.Player(id);rider.X=Festivals.WheelX;rider.Z=Festivals.WheelZ+1.5f;
                    Assert.That(sim.Execute(id,new GameCommand{Id=id+" boards",Kind=FestivalSimulation.RideWheelKind}).Accepted,Is.True,"setup: "+id+" boards the wheel");
                }
                // Crew c boarded (c-1.5)/8 of a turn either side of half a turn ago: the wheel hangs eight gondolas, so each crew has
                // its own, and crews 1 and 2 are just either side of the top. The level runs until the first crew can have boarded
                // that long ago, as a ride starts on the rules' clock (a VisualWheelSince under 0 is off the wheel).
                double earliest=Festivals.WheelRideSeconds*(.5+1.5/8);
                for(deadline=Time.realtimeSinceStartup+30;sim.State.SimulationSeconds<earliest&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(sim.State.SimulationSeconds,Is.GreaterThanOrEqualTo(earliest),"setup: the level has run "+earliest+" s");
                foreach(var ride in sim.State.Interactions)
                    for(int c=0;c<crews.Length;c++)
                        if(ride.Kind==FestivalSimulation.RideWheelKind&&ride.Status=="Active"&&System.Array.IndexOf(crews[c],ride.PlayerId)>=0)
                            ride.StartSeconds=sim.State.SimulationSeconds-Festivals.WheelRideSeconds*(.5+(c-1.5)/8);
                yield return new WaitForSeconds(1.2f);
                foreach(var crew in crews)foreach(var id in crew)Assert.That(FestivalSimulation.OnWheel(sim.State,id),Is.True,"setup: "+id+" is still on the wheel");
                Transform Cabin(Vector3 at){foreach(var g in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(g.name=="Gondola"&&g.gameObject.activeInHierarchy&&Encased(g).Contains(at))return g;return null;}
                var taken=new List<Transform>();
                for(int c=0;c<crews.Length;c++)
                {
                    Transform theirs=null;
                    foreach(var id in crews[c])
                    {
                        string who=id+" (body shape "+FestivalAppearance.For(id).Shape+(crews[c].Length>1?", beside a friend)":", alone)");
                        var body=GameObject.Find(id);
                        if(body==null){failures.Add(who+" is not drawn");continue;}
                        var pose=body.GetComponent<FestivalCharacter>().Pose;if(pose!=FestivalSimulation.RideWheelKind)failures.Add(who+" is posed "+pose);
                        Transform hips=null;foreach(var t in body.GetComponentsInChildren<Transform>())if(t.name=="Hips")hips=t;
                        var cabin=hips==null?null:Cabin(hips.position);
                        if(cabin==null){failures.Add(who+": the hips are in no gondola");continue;}
                        if(theirs==null){Assert.That(taken.Contains(cabin),Is.False,"setup: each crew rides its own gondola");theirs=cabin;taken.Add(cabin);}
                        else if(cabin!=theirs)failures.Add(who+" is not in their crew's gondola");
                        foreach(var part in OutsideItsGondola(body,cabin))failures.Add(who+": "+part);
                    }
                }
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Riders seen from outside their gondolas:\n"+string.Join("\n",failures));
        }

        /// <summary>The first id from prefix whose look is body shape 2 in the given pants (2 skirt, 3 tutu) and accessory.</summary>
        static string Rider(string prefix,int pants,int accessory)
        {
            for(int i=0;i<10000;i++){var look=FestivalAppearance.For(prefix+i);if(look.Shape==2&&look.Pants==pants&&look.Accessory==accessory)return prefix+i;}
            throw new System.InvalidOperationException("No shape-2 look in pants "+pants+" with accessory "+accessory);
        }

        // POLO-7: on a slow machine a rider's body and eye still sit on their seat. Each glides there (Actor's body at 15 a second,
        // UpdateCamera's eye at 20), aimed FestivalWorld.WheelSeat ahead of it. A friend and the host ride the wheel with frames held
        // near 30 a second, and once the glides settle, every frame for 3 s, the friend's body and the host's eye are measured
        // against FestivalTwistVisuals.Seat at the wheel as it is drawn (its WheelClock). Their median must be under 5 mm, so one
        // hitched frame cannot fail it. Aimed a fixed 1/rate ahead, both ran about 3 cm ahead; so does a WheelSeat rate that is not
        // its glide's.
        [UnityTest]public IEnumerator ARidersBodyAndEyeSitOnTheirSeatAtThirtyFramesASecond()
        {
            var world=new GameObject("Seat world");var festival=world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Seat session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            GameObject slow=null;
            try
            {
                Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsSortMode.None),Has.Length.EqualTo(1),"setup: a world leaked from an earlier test");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                const string friend="player_4";
                sim.AddPlayer(friend,friend).Ready=true;
                yield return StartLevel(session);
                var riders=new[]{friend,session.LocalPlayerId};
                foreach(var id in riders)
                {
                    var rider=sim.Player(id);rider.X=Festivals.WheelX;rider.Z=Festivals.WheelZ+1.5f;
                    Assert.That(sim.Execute(id,new GameCommand{Id=id+" boards",Kind=FestivalSimulation.RideWheelKind}).Accepted,Is.True,"setup: "+id+" boards the wheel");
                }
                slow=new GameObject("Slow frames");slow.AddComponent<SlowFrames>();
                yield return new WaitForSeconds(1.5f);
                var twists=(FestivalTwistVisuals)typeof(FestivalWorld).GetField("twists",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(festival);
                var body=GameObject.Find(friend);Assert.That(body,Is.Not.Null,"setup: "+friend+" is drawn");
                var bodyOff=new List<float>();var eyeOff=new List<float>();var frames=new List<float>();
                for(float end=Time.realtimeSinceStartup+3;Time.realtimeSinceStartup<end;)
                {
                    yield return null;
                    var state=session.State;
                    bodyOff.Add(Vector3.Distance(body.transform.position,twists.Seat(state,state.Players.Find(p=>p.Id==friend),twists.WheelClock)));
                    eyeOff.Add(Vector3.Distance(session.EyePosition,twists.Seat(state,session.LocalPlayer,twists.WheelClock)+Vector3.up*FestivalTwistVisuals.SeatedEyeHeight));
                    frames.Add(Time.unscaledDeltaTime);
                }
                foreach(var id in riders)Assert.That(FestivalSimulation.OnWheel(sim.State,id),Is.True,"setup: "+id+" is still on the wheel");
                float Median(List<float> values){values.Sort();return values[values.Count/2];}
                string Off(List<float> values)=>"median "+(Median(values)*1000).ToString("0.0")+" mm, worst "+(values[values.Count-1]*1000).ToString("0.0")+" mm";
                float frame=Median(frames);string fps=(1/frame).ToString("0")+" fps: ";
                Assert.That(frame,Is.GreaterThan(1/40f),"setup: frames are held near 30 a second, not "+fps);
                var off=new List<string>();
                if(Median(bodyOff)>=.005f)off.Add(friend+"'s body: "+Off(bodyOff));
                if(Median(eyeOff)>=.005f)off.Add("the host's eye: "+Off(eyeOff));
                Assert.That(off,Is.Empty,fps+"riders sit off their seat:\n"+string.Join("\n",off));
            }
            finally
            {
                if(slow!=null)Object.Destroy(slow);
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }
        // Holds this machine's frames near 30 a second, as a slow one runs them.
        private sealed class SlowFrames:MonoBehaviour{private void Update()=>System.Threading.Thread.Sleep(30);}

        // Seen from the grass beside the wheel, a riding friend's name tag floats clear of their gondola's roof. Solids hide tags, so
        // this counts the pixels the tag lights with that gondola drawn against those it lights with the gondola hidden.
        [UnityTest]public IEnumerator ARidingFriendsNameTagShowsAboveTheirGondolasRoof()
        {
            var world=new GameObject("Tag world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Tag session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            var target=new RenderTexture(640,360,24);var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);
            try
            {
                Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsSortMode.None),Has.Length.EqualTo(1),"setup: a world leaked from an earlier test");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                const string friend="player_4";
                sim.AddPlayer(friend,friend).Ready=true;
                yield return StartLevel(session);
                var rider=sim.Player(friend);rider.X=Festivals.WheelX;rider.Z=Festivals.WheelZ+1.5f;
                Assert.That(sim.Execute(friend,new GameCommand{Id=friend+" boards",Kind=FestivalSimulation.RideWheelKind}).Accepted,Is.True,"setup: "+friend+" boards the wheel");
                // Out of the wheel's plane, so its rim does not stand between the host and the gondola.
                var you=sim.Player(session.LocalPlayerId);you.X=Festivals.WheelX+3.5f;you.Z=Festivals.WheelZ;
                yield return new WaitForSeconds(1f);
                var body=GameObject.Find(friend);Assert.That(body,Is.Not.Null,"setup: "+friend+" is drawn");
                var label=body.transform.Find("Label");var text=label.GetComponentInChildren<TextMeshPro>(true);
                Transform hips=null;foreach(var t in body.GetComponentsInChildren<Transform>())if(t.name=="Hips")hips=t;
                Transform cabin=null;foreach(var g in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(g.name=="Gondola"&&g.gameObject.activeInHierarchy&&Encased(g).Contains(hips.position))cabin=g;
                Assert.That(cabin,Is.Not.Null,"setup: "+friend+" sits in a gondola");
                // The host looks at the tag; the tag turns to the host's view (FestivalSession.Actor).
                var look=label.position-session.EyePosition;
                foreach(var field in new[]{"yaw","viewYaw"})typeof(FestivalSession).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg);
                foreach(var field in new[]{"pitch","viewPitch"})typeof(FestivalSession).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,-Mathf.Asin(look.normalized.y)*Mathf.Rad2Deg);
                yield return null;yield return null;
                Assert.That(label.gameObject.activeInHierarchy,Is.True,"setup: the tag shows "+Vector3.Distance(session.ViewCamera.transform.position,body.transform.position).ToString("0.0")+" m away");
                // Narrowed for these shots alone, so the small far tag covers enough pixels to count.
                var camera=session.ViewCamera;var previous=RenderTexture.active;float zoom=camera.fieldOfView;camera.fieldOfView=20;
                Color[] Shot(){camera.targetTexture=target;camera.Render();camera.targetTexture=null;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();RenderTexture.active=previous;return pixels.GetPixels();}
                int Lit()
                {
                    var on=Shot();text.renderer.enabled=false;var off=Shot();text.renderer.enabled=true;
                    int lit=0;for(int i=0;i<on.Length;i++)if(Mathf.Abs(on[i].r-off[i].r)+Mathf.Abs(on[i].g-off[i].g)+Mathf.Abs(on[i].b-off[i].b)>.15f)lit++;
                    return lit;
                }
                int held=Lit();
                var parts=cabin.GetComponentsInChildren<Renderer>();foreach(var part in parts)part.enabled=false;
                int alone=Lit();
                foreach(var part in parts)part.enabled=true;camera.fieldOfView=zoom;
                Assert.That(alone,Is.GreaterThan(30),"setup: the tag is in view");
                Assert.That(held,Is.GreaterThanOrEqualTo(alone*.9f),friend+"'s gondola hides "+(alone-held)+" of the "+alone+" pixels their name tag lights");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);target.Release();Object.Destroy(target);Object.Destroy(pixels);
            }
            yield return null;
        }

        // POLO-5: a rider is drawn at FestivalTwistVisuals.RiderScale, and what they hold goes round at their size, whenever it was
        // made. The rules take Equip on the wheel (the HUD's number keys send it), so a friend equips the map at the base and boards,
        // then equips their poi and their confetti mid-ride, and after the turn shows the poi again. Each held item is measured
        // against the body (Size over the body's): the map made on the ground sets the size, and every other reading, on the wheel
        // and after the turn, must match it.
        [UnityTest]public IEnumerator HandItemsGoRoundAtTheRidersSizeWheneverTheyAreMade()
        {
            var world=new GameObject("Held world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Held session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            var failures=new List<string>();
            try
            {
                Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsSortMode.None),Has.Length.EqualTo(1),"setup: a world leaked from an earlier test");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                const string id="player_4";
                sim.AddPlayer(id,id).Ready=true;
                yield return StartLevel(session);
                var friend=sim.Player(id);friend.X=Festivals.WheelX;friend.Z=Festivals.WheelZ+1.5f;
                foreach(var item in new[]{"map","poi_practice","confetti"})friend.Inventory.Add(new ItemStack{ItemId=item,Count=1});
                IEnumerator Equip(string item)
                {
                    Assert.That(sim.Execute(id,new GameCommand{Id=id+" equips "+item+" "+sim.State.SimulationSeconds,Kind="Equip",ItemId=item}).Accepted,Is.True,"setup: "+id+" equips the "+item);
                    yield return new WaitForSeconds(.6f);
                }
                var body=GameObject.Find(id);Assert.That(body,Is.Not.Null,"setup: "+id+" is drawn");
                Transform Held(string name){foreach(var t in body.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
                float AgainstTheBody(Transform held)=>Size(held)/Size(body.transform);
                float size=0;
                void Check(string when,string name)
                {
                    var held=Held(name);
                    if(held==null){failures.Add(when+": no "+name);return;}
                    float against=AgainstTheBody(held);
                    if(Mathf.Abs(against/size-1)>.1f)failures.Add(when+": "+name+" is drawn "+(against/size).ToString("0.00")+"x its size against the body (size "+Size(held).ToString("0.000")+" on a body at "+Size(body.transform).ToString("0.000")+")");
                }

                yield return Equip("map");
                var map=Held("Held grip map");Assert.That(map,Is.Not.Null,"setup: the map is in "+id+"'s hand");
                size=AgainstTheBody(map);float standing=body.transform.lossyScale.y;
                Assert.That(sim.Execute(id,new GameCommand{Id=id+" boards",Kind=FestivalSimulation.RideWheelKind}).Accepted,Is.True,"setup: "+id+" boards the wheel");
                yield return new WaitForSeconds(1.2f);
                Assert.That(body.transform.lossyScale.y,Is.LessThan(standing*.6f),"setup: "+id+" is drawn smaller in the gondola");
                Check("on the wheel, equipped at the base","Held grip map");
                yield return Equip("poi_practice");
                Check("on the wheel, equipped mid-ride","Poi grip and tether");
                yield return Equip("confetti");
                Check("on the wheel, equipped mid-ride","Held grip confetti");
                Assert.That(FestivalSimulation.OnWheel(sim.State,id),Is.True,"setup: "+id+" is still on the wheel");

                foreach(var ride in sim.State.Interactions)if(ride.PlayerId==id&&ride.Kind==FestivalSimulation.RideWheelKind&&ride.Status=="Active")ride.StartSeconds=sim.State.SimulationSeconds-Festivals.WheelRideSeconds+.3;
                yield return new WaitForSeconds(1.5f);
                Assert.That(FestivalSimulation.OnWheel(sim.State,id),Is.False,"setup: the turn has ended");
                Check("after the turn, equipped mid-ride","Held grip confetti");
                // The poi pair made mid-ride is the one that shows again: a pair is remade only for the other kind of poi.
                yield return Equip("poi_practice");
                Check("after the turn, equipped mid-ride","Poi grip and tether");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Held items drawn at the wrong size:\n"+string.Join("\n",failures));
        }

        // POLO-5: what of a seated body shows outside its gondola: each skinned part's furthest vertex past ART-1's gondola (its
        // "gond" in generate_polo_dressing.py), from the gondola's middle: the floor's underside .45 m down, the walls' outer faces .63 m
        // either side and .53 m ahead and behind, their tops .01 m up, and the canopy roof's underside .37 m up. Below the wall tops
        // nothing may come through; above them, in the open air under the roof, a hand, a hat brim or a backpack's top may overhang
        // the walls by up to Overhang; and nothing, not even a hat, may come up through the roof. Vertices, not the renderers'
        // bounds: a skinned renderer's bounds join a box per bone, which with the arms bent stand up to 10 cm wider than the mesh.
        internal static List<string> OutsideItsGondola(GameObject body,Transform gondola)
        {
            const float Underside=.45f,Side=.63f,End=.53f,WallTop=.01f,Roof=.37f,Overhang=.05f;
            var outside=new List<string>();var mesh=new Mesh();var middle=gondola.position;
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!skin.enabled)continue;
                skin.BakeMesh(mesh,true);var toWorld=skin.transform.localToWorldMatrix;float worst=0;var at=Vector3.zero;
                foreach(var vertex in mesh.vertices)
                {
                    var p=toWorld.MultiplyPoint3x4(vertex)-middle;
                    float past=Mathf.Max(Mathf.Max(Mathf.Abs(p.x)-Side,Mathf.Abs(p.z)-End)-(p.y>WallTop?Overhang:0),-Underside-p.y,p.y-Roof);
                    if(past>worst){worst=past;at=p;}
                }
                if(worst>0)outside.Add(skin.name+" shows "+worst.ToString("0.000")+" m past the gondola at "+at.ToString("F2")+" from its middle");
            }
            Object.Destroy(mesh);
            return outside;
        }
        // How many times its own size a transform is drawn in the world: the cube root of the volume it scales by. Unlike lossyScale,
        // the bones' turns leave it alone, so a hand item reads the same in any pose.
        private static float Size(Transform t)=>Mathf.Pow(Mathf.Abs(t.localToWorldMatrix.determinant),1/3f);
        // Everything a part of the world is drawn with, in one box.
        private static Bounds Encased(Transform part){var renderers=part.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
    }
}
