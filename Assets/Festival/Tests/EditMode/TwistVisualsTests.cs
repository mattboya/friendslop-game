using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace Festival.Tests
{
    // TWISTVIS-1: each festival's twists get stand-ins built from primitives and existing props. They show only on their own
    // festival, follow the round (influencers' phones, the wheel, the art cars, the burn, the storms), never collide, and keep
    // clear of the scenery they stand and drive among.
    public sealed class TwistVisualsTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        [Test]public void EachFestivalShowsOnlyItsOwnTwistsAndNoneOfThemCollide()
        {
            foreach(int festival in new[]{Festivals.PoloFestival,Festivals.PlayaFestival})
            {
                var game=Level(festival);var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);
                twists.Apply(Wire(game,"p0"),.02f);
                bool polo=festival==Festivals.PoloFestival;string where=Festivals.Name(festival)+": ";
                Assert.That(Part(grounds,FestivalTwistVisuals.PoloRootName).gameObject.activeSelf,Is.EqualTo(polo),where+"Palm Mirage's ropes and wheel");
                Assert.That(Part(grounds,FestivalTwistVisuals.PlayaRootName).gameObject.activeSelf,Is.EqualTo(!polo),where+"Ember Playa's art cars and effigy");
                Assert.That(Frames(grounds).Count,Is.Zero,where+"POLO-6: no influencer films through a frame on the ground");
                Assert.That(grounds.GetComponentsInChildren<Renderer>(true).Length,Is.GreaterThan(20),where+"the stand-ins are built");
                foreach(var collider in grounds.GetComponentsInChildren<Collider>(true))
                    Assert.That(collider.enabled,Is.False,where+collider.name+" would block sight lines, steps or paths the rules don't know about");
            }
        }

        [Test]public void VipRopesRingEachZoneAndTheFerrisWheelTurnsOncePerRide()
        {
            var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);var round=Round(Festivals.PoloFestival);twists.Apply(round,0);
            var polo=Part(grounds,FestivalTwistVisuals.PoloRootName);
            for(int i=0;i<Festivals.VipZones.Length;i++)
            {
                var zone=Festivals.VipZones[i];var rope=Bounds(Part(polo,"VIP rope "+i));
                Assert.That(new[]{rope.min.x,rope.min.z,rope.max.x,rope.max.z},Is.EqualTo(new[]{zone.MinX,zone.MinZ,zone.MaxX,zone.MaxZ}).Within(.25f),"rope "+i+" rings its VIP zone");
                Assert.That(rope.max.y,Is.InRange(.8f,1.4f),"rope "+i+" hangs at waist height");
            }
            var wheel=Part(polo,"Ferris wheel");
            Assert.That(Vector2.Distance(Flat(wheel.position),new Vector2(Festivals.WheelX,Festivals.WheelZ)),Is.LessThan(1e-3f),"the wheel's base is where the rules board it");
            Assert.That(Bounds(wheel).max.y,Is.GreaterThan(10f),"the wheel stands over the festival");
            var rotor=Part(wheel,"Wheel rotor");
            int gondolas=0;
            foreach(Transform child in wheel)if(child.name=="Gondola")
            {
                gondolas++;Assert.That(child.GetComponentInChildren<Renderer>(),Is.Not.Null,"each gondola carries its model");
                foreach(var r in child.GetComponentsInChildren<Renderer>())Assert.That(r.shadowCastingMode,Is.EqualTo(ShadowCastingMode.On),"gondola "+gondolas+"/"+r.name+" casts a shadow, as the primitive did");
                Assert.That(Vector3.Distance(child.localPosition+Vector3.up*.75f,rotor.localPosition),Is.EqualTo(6).Within(1e-3f),"gondola "+gondolas+" hangs .75 m below a point 6 m from the axle");
            }
            Assert.That(gondolas,Is.EqualTo(8),"eight gondolas hang from the rim");
            Assert.That(Painted(wheel,"StageGlowMint"),Is.Not.Null,"the rim glows mint and rose, as before");
            // POLO-5: it turns on the rules' clock, not each client's frames.
            var start=rotor.rotation;round.SimulationSeconds=Festivals.WheelRideSeconds/4;
            twists.Apply(round,.02f);
            Assert.That(Quaternion.Angle(start,rotor.rotation),Is.EqualTo(90).Within(.5f),"a quarter of a ride on the rules' clock turns it a quarter");
            round.SimulationSeconds=Festivals.WheelRideSeconds;twists.Apply(round,.02f);
            Assert.That(Quaternion.Angle(start,rotor.rotation),Is.LessThan(.5f),"one ride is one full turn");
        }

        // POLO-5: each client turned the wheel on its own frames, so no two drew a gondola at the same angle and none could seat a
        // rider. It turns on the rules' clock: a client that has watched it for a while and one that has just joined draw every
        // gondola alike, at the clock's angle. Between snapshots (10 a second) it turns on with the frame rather than stopping dead.
        [Test]public void EveryClientDrawsTheWheelAtTheSameAngle()
        {
            var round=Round(Festivals.PoloFestival);var first=Grounds();var watching=new FestivalTwistVisuals(first);var early=Rotor(first);
            for(int frame=0;frame<90;frame++){round.SimulationSeconds=3+frame/6*.1;watching.Apply(round,1/60f);}
            var second=Grounds();var joined=new FestivalTwistVisuals(second);var late=Rotor(second);
            round.SimulationSeconds=13.37;watching.Apply(round,.02f);joined.Apply(round,.02f);
            Assert.That(Quaternion.Angle(early.rotation,late.rotation),Is.LessThan(.01f),"both clients draw the wheel at the same angle");
            Assert.That(Quaternion.Angle(late.localRotation,Quaternion.Euler((float)(13.37/Festivals.WheelRideSeconds*360),0,0)),Is.LessThan(.05f),"the angle the rules' clock says");
            var hung=Gondolas(first);var alike=Gondolas(second);
            for(int k=0;k<hung.Count;k++)Assert.That(Vector3.Distance(hung[k].position,alike[k].position),Is.LessThan(1e-3f),"gondola "+k+" hangs in the same place on both");
            var before=late.localRotation;
            for(int frame=0;frame<3;frame++)joined.Apply(round,1/60f);
            Assert.That(Quaternion.Angle(before,late.localRotation),Is.InRange(.6f,1.2f),"between snapshots the wheel turns on with the frame (18° a second)");
            before=late.localRotation;round.SimulationSeconds+=.1;joined.Apply(round,1/60f);
            Assert.That(Quaternion.Angle(before,late.localRotation),Is.LessThan(1f),"and eases onto the next snapshot rather than jumping to it");
        }

        // MINI-1: a minigame holds the festival as a freeze-frame. The rules' clock runs on through the game, but the wheel stands still
        // all game, frame after frame, and once it ends turns on from where it stopped rather than jumping to where the rules' clock got
        // to. A rider boarding after a 10 s game (half a turn) takes the gondola hanging at the platform as the wheel is drawn, not the
        // one at the top.
        [Test]public void TheWheelStandsStillThroughAMinigameAndTurnsOnFromThere()
        {
            var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);var round=Round(Festivals.PoloFestival);var rotor=Rotor(grounds);
            const float Frame=1/60f;
            // A frame on the host: the rules step on, and the game's own clock with them while one runs (MinigameTick).
            void Step(){round.SimulationSeconds+=Frame;if(round.Phase==FestivalSimulation.MinigamePhase)round.Booth.Seconds+=Frame;twists.Apply(round,Frame);}
            round.SimulationSeconds=7;twists.Apply(round,Frame);for(int f=0;f<60;f++)Step();
            round.Phase=FestivalSimulation.MinigamePhase;round.Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=0};
            var stopped=rotor.localRotation;float drift=0;
            for(int f=0;f<600;f++){Step();drift=Mathf.Max(drift,Quaternion.Angle(stopped,rotor.localRotation));}
            Assert.That(drift,Is.LessThan(.01f),"the wheel holds still through a 10 s game, though the rules' clock runs on");
            round.Phase="Playing";var before=rotor.localRotation;Step();
            Assert.That(Quaternion.Angle(before,rotor.localRotation),Is.LessThan(1f),"as play resumes it turns on from where it stopped, with no jump");
            for(int f=1;f<60;f++)Step();
            Assert.That(Quaternion.Angle(stopped,rotor.localRotation),Is.EqualTo(18).Within(.5f),"a second of play turns it a second's worth, 18°");
            var rider=new PlayerState{Id="rider",VisualWheelSince=round.SimulationSeconds};round.Players.Add(rider);
            var seat=FestivalTwistVisuals.Seat(round,rider,twists.WheelClock);
            Assert.That(seat.y,Is.LessThan(2.5f),"a rider boarding after the game sits in the gondola down at the platform, at "+seat);
            Assert.That(Vector2.Distance(Flat(seat),new Vector2(Festivals.WheelX,Festivals.WheelZ)),Is.LessThanOrEqualTo(Festivals.WheelReach),"beside it");
        }

        // POLO-5: boarding seats the rider in the gondola nearest the platform, and Seat keeps them in it all the way round: the eye
        // in its open cabin under the roof, more than 10 m up at the top, and back by the platform when the turn comes round. A
        // head's room round the eye stays clear of the rotor model and of the gondola itself. The rotor's rims, spokes, hangers, hub
        // and bulbs are merged into one renderer per colour, so its meshes, not its renderers' boxes, are what is measured. Friends
        // who board together (one, two, three, or the whole crew of eight) never sit in one another: riders who share a gondola sit
        // side by side at least a seated body's width apart (SeatedWidth, drawn at RiderScale), so once it is full the rest ride the
        // gondolas round from it, each in one gondola all the way round and back where they boarded. A rider who boards later takes
        // no place in theirs.
        [Test]public void RidersSitInTheGondolaTheyBoardedAllTheWayRound()
        {
            var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);var round=Round(Festivals.PoloFestival);
            var wheel=Part(Part(grounds,FestivalTwistVisuals.PoloRootName),"Ferris wheel");var rotor=Rotor(grounds);var gondolas=Gondolas(grounds);
            // The models' meshes made solid, for this test alone.
            foreach(var part in new List<Transform>(gondolas){rotor})foreach(var mesh in part.GetComponentsInChildren<MeshFilter>())mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
            Assert.That(rotor.GetComponentsInChildren<MeshCollider>().Length,Is.GreaterThan(0),"setup: the rotor model's meshes are measured");
            const float Head=.15f;var touching=new Collider[8];
            foreach(int crew in new[]{1,2,3,8})foreach(double since in new[]{0,3.7,11.2})
            {
                round.Players.Clear();for(int r=0;r<crew;r++)round.Players.Add(new PlayerState{Id="rider "+r,VisualWheelSince=since});
                round.Players.Add(new PlayerState{Id="later rider",VisualWheelSince=since+5});round.Players.Add(new PlayerState{Id="walker"});
                var theirs=new int[crew];var start=new Vector3[crew];
                foreach(double part in new[]{0,.25,.5,.75,1})
                {
                    double now=since+part*Festivals.WheelRideSeconds;round.SimulationSeconds=now;twists.Apply(round,0);Physics.SyncTransforms();
                    string when=crew+" aboard, boarded "+since+" s, "+part*100+"% round: ";var eyes=new List<Vector3>();
                    for(int r=0;r<crew;r++)eyes.Add(FestivalTwistVisuals.Seat(round,round.Players[r],now)+Vector3.up*FestivalTwistVisuals.SeatedEyeHeight);
                    for(int r=0;r<crew;r++)
                    {
                        var eye=eyes[r];int inside=gondolas.FindIndex(g=>Bounds(g).Contains(eye));
                        Assert.That(inside,Is.GreaterThanOrEqualTo(0),when+"rider "+r+"'s eye "+eye+" is in a gondola");
                        if(part==0){theirs[r]=inside;start[r]=eye;}
                        Assert.That(inside,Is.EqualTo(theirs[r]),when+"rider "+r+" stays in the gondola they boarded");
                        int hits=Physics.OverlapSphereNonAlloc(eye,Head,touching);var hit=new List<string>();
                        for(int h=0;h<hits;h++)if(touching[h].transform.IsChildOf(wheel))hit.Add(touching[h].transform.parent.name+"/"+touching[h].name);
                        Assert.That(hit,Is.Empty,when+"a head's room round the eye "+eye+" is clear of the wheel's rims, spokes and hangers and the gondola's walls");
                        Assert.That(Physics.Raycast(eye,Vector3.up,out var roof,.5f)&&roof.transform.IsChildOf(gondolas[inside]),Is.True,when+"the eye sits under the gondola's roof");
                        if(part==1)Assert.That(Vector3.Distance(eye,start[r]),Is.LessThan(1e-3f),when+"one turn brings rider "+r+" back where they boarded");
                        // The first to board, and whoever shares their gondola, ride the one at the platform.
                        if(inside!=theirs[0])continue;
                        if(part==.5)Assert.That(eye.y,Is.GreaterThan(10f),when+"the top of the turn is more than 10 m up");
                        if(part==1)
                        {
                            Assert.That(Vector2.Distance(Flat(eye),new Vector2(Festivals.WheelX,Festivals.WheelZ)),Is.LessThanOrEqualTo(Festivals.WheelReach),when+"the turn ends back by the platform");
                            Assert.That(eye.y,Is.LessThan(2.5f),when+"down at the platform");
                        }
                    }
                    if(part==0)foreach(var g in gondolas)Assert.That(gondolas[theirs[0]].position.y,Is.LessThanOrEqualTo(g.position.y+1e-4f),when+"the first to board takes the gondola nearest the platform");
                    for(int a=0;a<crew;a++)for(int b=a+1;b<crew;b++)
                        if(theirs[a]==theirs[b])Assert.That(Vector3.Distance(eyes[a],eyes[b]),Is.GreaterThanOrEqualTo(SeatedWidth*FestivalTwistVisuals.RiderScale),when+"riders "+a+" and "+b+" share a gondola, so they sit a body's width apart, not in one another");
                    for(int g=0;g<gondolas.Count;g++)
                    {
                        var middle=Vector3.zero;int sharing=0;
                        for(int r=0;r<crew;r++)if(theirs[r]==g){middle+=eyes[r];sharing++;}
                        if(sharing>0)Assert.That(middle.x/sharing,Is.EqualTo(Festivals.WheelX).Within(1e-3f),when+"riders spread evenly across gondola "+g+", and the later rider takes no place in it");
                    }
                }
            }
        }
        // The widest seated body at full size, from either side: shape 2's accessory 1 reaches .56 m out from its middle (measured
        // over 240 looks), so two of them side by side need 1.12 m between their middles.
        private const float SeatedWidth=1.12f;

        // POLO-7: a rider's body glides onto their seat at 15 a second and their eye at 20 (FestivalSession's Actor and UpdateCamera),
        // each aimed ahead of the seat by FestivalWorld.GlideLead. Aimed a fixed 1/rate ahead, the glide ran 1.5 cm ahead of the
        // seat at 60 fps and 2.9 cm at 30, where a seated body clears the gondola's walls by 2-3 cm. Aimed GlideLead ahead, it stays
        // within 3 mm of the seat at any frame rate, a whole turn long once it has settled.
        [Test]public void AGlidingRiderStaysOnTheirSeatAtAnyFrameRate()
        {
            var round=Round(Festivals.PoloFestival);var rider=new PlayerState{Id="rider",VisualWheelSince=0};round.Players.Add(rider);
            const double Settle=1;var off=new List<string>();
            foreach(int fps in new[]{30,60,144})foreach(float rate in new[]{15f,20f})
            {
                float dt=1f/fps,worst=0;var glided=FestivalTwistVisuals.Seat(round,rider,0);
                for(int frame=1;frame*(double)dt<=Settle+Festivals.WheelRideSeconds;frame++)
                {
                    double now=frame*(double)dt;var target=FestivalTwistVisuals.Seat(round,rider,now+FestivalWorld.GlideLead(rate,dt));
                    // The session's glide, as Actor and UpdateCamera move the body and the eye each frame.
                    glided=Vector3.Lerp(glided,target,1-Mathf.Exp(-rate*dt));
                    if(now>=Settle)worst=Mathf.Max(worst,Vector3.Distance(glided,FestivalTwistVisuals.Seat(round,rider,now)));
                }
                if(worst>=.003f)off.Add(fps+" fps, gliding at "+rate+": "+(worst*1000).ToString("0.0")+" mm off the seat");
            }
            Assert.That(off,Is.Empty,"The glided rider leaves their seat:\n"+string.Join("\n",off));
        }

        // POLO-7: GlideLead(rate,dt) is how far ahead to aim a glide at rate, a frame of dt seconds at a time, so it keeps up with a
        // target moving at a steady pace: dt·q/(1−q), q=exp(−rate·dt). It falls short of 1/rate by under half a frame, so it nears
        // 1/rate as frames shorten, and is 1/rate before there is a frame time, or when a frame is too short to measure.
        [Test]public void TheGlideLeadNearsOneOverTheRateAsFramesShorten()
        {
            foreach(float rate in new[]{15f,20f})
            {
                Assert.That(FestivalWorld.GlideLead(rate,0),Is.EqualTo(1/rate),"gliding at "+rate+" with no frame time yet");
                Assert.That(FestivalWorld.GlideLead(rate,1e-30f),Is.EqualTo(1/rate),"gliding at "+rate+" through a frame too short to measure, not an endless lead");
                foreach(float dt in new[]{1/30f,1/60f,1/144f,1e-3f})
                    Assert.That(FestivalWorld.GlideLead(rate,dt),Is.LessThan(1/rate).And.GreaterThan(1/rate-dt/2),"gliding at "+rate+" in "+dt+" s frames: under 1/rate by less than half a frame");
            }
        }

        // POLO-2: a gold-trimmed sign over the night market's stock stall says where the rules sell wristbands, and for how much.
        // Its lettering is depth-tested, so its board hides it from behind (WorldTextTests).
        [Test]public void AGoldSignMarksTheNightMarketsVipStall()
        {
            var world=Made("VIP stall world").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var grounds=world.transform.Find(FestivalWorld.RootName);var round=Round(Festivals.PoloFestival);world.SetTwists(round);
            var sign=Part(Part(grounds,FestivalTwistVisuals.PoloRootName),FestivalTwistVisuals.VipSignName);
            var text=sign.GetComponentInChildren<TextMeshPro>(true);
            Assert.That(text!=null?text.text:"(no text)",Is.EqualTo("VIP WRISTBANDS $"+Catalog.FindItem(FestivalSimulation.VipWristband).Price),"the sign says what the stall sells, for what the rules charge");
            Assert.That(Painted(sign,"StageGlowGold").sharedMaterial,Is.EqualTo(FestivalArtView.MaterialFor("StageGlowGold")),"its trim is gold, and glows so it reads at night");
            Transform stall=null;foreach(Transform child in grounds)if(child.name=="FestivalStallStock")stall=child;
            Assert.That(stall,Is.Not.Null,"setup: the night market has its stock stall");
            var under=Bounds(stall);var board=Painted(sign,"Dark").bounds;
            Assert.That(board.min.y,Is.GreaterThan(under.max.y),"the board hangs over the stall's awning, clear of it");
            Assert.That(board.center.x,Is.InRange(under.min.x,under.max.x),"over the stall, not beside it");
            Assert.That(board.center.z,Is.InRange(under.min.z,under.max.z),"over the stall, not in front of or behind it");
            Assert.That(Vector2.Distance(Flat(board.center),new Vector2(Festivals.VipStallX,Festivals.VipStallZ)),Is.LessThanOrEqualTo(Festivals.VipStallRange),"where the rules sell wristbands");
            text.ForceMeshUpdate();var renderer=text.GetComponent<Renderer>();
            Assert.That(new[]{renderer.bounds.min.x,renderer.bounds.max.x},Is.EqualTo(new[]{board.center.x,board.center.x}).Within(board.extents.x),"the lettering fits across the board");
            Assert.That(text.fontSharedMaterial,Is.SameAs(FestivalWorldText.Font.material),"the lettering is the world's depth-tested text");
        }

        // POLO-6 (Matt, 2026-10-01: "what is this pink cone in front of someone?"): an influencer no longer films through a frame drawn
        // on the ground. It holds a phone up beside its face in its "right" hand, screen to itself and camera to the crowd the rules
        // have it film, and the red light by the camera blinks once a second, glowing so it reads at night. On the screen of a player it
        // has on camera, the light stays lit and "• REC" floats over its head. The running game's influencers hold theirs, and only
        // the one filming you shows REC, in TwistVisualsSessionTests.
        [Test]public void InfluencersHoldUpAPhoneAndDrawNoFrame()
        {
            var view=Wire(Level(Festivals.PoloFestival),"p0");var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);
            Assert.That(view.Npcs.FindAll(n=>n.Twist==FestivalSimulation.Influencer).Count,Is.EqualTo(Festivals.Influencers),"setup: the view carries every influencer");
            twists.Apply(view,.02f);
            Assert.That(Frames(grounds).Count,Is.Zero,"no frame on the ground in front of an influencer");
            foreach(var r in grounds.GetComponentsInChildren<Renderer>(true))
                Assert.That(r.name.StartsWith("Frame")||r.name=="Phone",Is.False,"no frame edge, arc or phone floating on the grounds: "+PathOf(r.transform,grounds));

            var body=FestivalCharacter.Create(Made("Influencer").transform,"wook_7",new Color(.8f,.5f,.3f));var actor=body.transform;
            Transform Bone(string name){foreach(var t in actor.GetComponentsInChildren<Transform>())if(t.name==name)return t;Assert.Fail(name+" bone");return null;}
            var shoulder=Bone("ArmR");var hand=Bone("HandR");var head=Bone("Head");
            // The body as drawn, hat and hair included, and its face.
            Bounds face=default,look=default;bool drawn=false;
            foreach(var r in actor.GetComponentsInChildren<Renderer>())if(r.enabled)
            {
                if(r.name.StartsWith("Face_"))face=r.bounds;
                if(!drawn){look=r.bounds;drawn=true;}else look.Encapsulate(r.bounds);
            }
            Assert.That(face.size.y,Is.GreaterThan(.1f),"setup: the face is drawn");
            var phone=new FestivalFilmingPhone(actor,shoulder,Bone("ForearmR"),hand,head);
            phone.Show(true,false);phone.Hold(.25f,null);
            var held=Part(hand,FestivalFilmingPhone.PhoneName);
            Assert.That(held.gameObject.activeInHierarchy,Is.True,"the phone is out");
            Assert.That(Vector3.Distance(held.position,hand.position),Is.LessThan(.15f),"in the right hand");
            Assert.That(hand.position.y,Is.GreaterThan(shoulder.position.y),"held up above the shoulder");
            Assert.That(held.position.y,Is.InRange(face.min.y,face.max.y),"at face height");
            Assert.That(Vector3.Dot(held.position-face.center,actor.forward),Is.GreaterThan(.12f),"held out ahead of the face");
            Assert.That(Mathf.Abs(Vector3.Dot(held.position-face.center,actor.right)),Is.GreaterThan(face.extents.x),"beside the face, not over it, so from afar it reads as filming");
            Assert.That(Vector3.Angle(held.forward,actor.forward),Is.LessThan(20),"its camera faces the way the influencer films");
            Assert.That(held.GetComponentsInChildren<Collider>(true),Is.Empty,"it blocks no sight line or step");
            var light=Part(held,FestivalFilmingPhone.LightName).GetComponent<Renderer>();
            var glow=light.sharedMaterial.GetColor("_EmissionColor");
            Assert.That(light.sharedMaterial.IsKeywordEnabled("_EMISSION")&&glow.r>.5f&&glow.r>3*glow.g&&glow.r>3*glow.b,Is.True,"the light glows red, so it reads at night: "+glow);
            var label=Part(actor,FestivalFilmingPhone.LabelName);
            bool Blinks(out bool labelled)
            {
                bool on=false,off=false;labelled=false;
                for(float t=0;t<1;t+=.1f){phone.Hold(t,null);on|=light.enabled;off|=!light.enabled;labelled|=label.gameObject.activeInHierarchy;}
                return on&&off;
            }
            Assert.That(Blinks(out bool rec),Is.True,"its light blinks within a second");
            Assert.That(rec,Is.False,"and no REC shows while it doesn't have you on camera");

            phone.Show(true,true);
            Assert.That(Blinks(out rec),Is.False,"with you on camera the light stays lit");
            Assert.That(light.enabled&&rec,Is.True,"and REC shows");
            Assert.That(label.GetComponent<TextMeshPro>().text,Is.EqualTo("• REC"));
            Assert.That(label.position.y,Is.GreaterThan(look.max.y),"over its head, hat and all");
            Assert.That(Vector2.Distance(Flat(label.position),Flat(head.position)),Is.LessThan(.1f),"right over it");

            phone.Show(false,false);phone.Hold(.25f,null);
            Assert.That(held.gameObject.activeInHierarchy||label.gameObject.activeInHierarchy,Is.False,"no longer an influencer (a new level deals others), the phone and REC go");
        }

        [Test]public void ArtCarsRollRoundTheirLoopsWithTheLevelClock()
        {
            var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);var state=Round(Festivals.PlayaFestival);
            var playa=Part(grounds,FestivalTwistVisuals.PlayaRootName);
            // Clock readings clear of the loops' corners, so each car's heading is its leg's.
            foreach(double t in new[]{0.0,30,50})
            {
                state.ElapsedSeconds=t;twists.Apply(state,.02f);
                for(int k=0;k<Festivals.ArtCars;k++)
                {
                    var car=Part(playa,"Art car "+k);var at=Festivals.ArtCarAt(k,t);var ahead=Festivals.ArtCarAt(k,t+.5);
                    Assert.That(Vector2.Distance(Flat(car.position),new Vector2(at.X,at.Z)),Is.LessThan(1e-3f),"car "+k+" is where the rules have it "+t+" s in");
                    Assert.That(Vector3.Angle(car.forward,new Vector3(ahead.X-at.X,0,ahead.Z-at.Z)),Is.LessThan(1f),"car "+k+" faces the way it rolls "+t+" s in");
                }
            }
            var glider=Part(playa,"Art car 0");state.ElapsedSeconds=51;twists.Apply(state,1/60f);
            var next=Festivals.ArtCarAt(0,51);float left=Vector2.Distance(Flat(glider.position),new Vector2(next.X,next.Z));
            Assert.That(left,Is.InRange(.2f,1.1f),"a car glides to the next snapshot like its rider's body does, rather than jumping ("+left+" m to go)");

            var van=Part(glider,"FestivalCampVan");var eye=glider.position+Vector3.up*1.65f;
            foreach(var renderer in van.GetComponentsInChildren<Renderer>())
                Assert.That(renderer.bounds.Contains(eye),Is.False,"a rider stands on the deck, clear of the camp van pulling it ("+renderer.name+")");
            Assert.That(Vector3.Dot(van.position-glider.position,glider.forward),Is.GreaterThan(0),"the van leads the deck");
            Assert.That(Vector3.Dot(Centre(van,"__Cream")-Centre(van,"__Rose"),glider.forward),Is.GreaterThan(0),"the van drives nose first: its headlamps lead its tail lights");
            Assert.That(Bounds(van).max.y,Is.LessThan(eye.y),"a rider sees over the van's roof, not into its back");
        }

        [Test]public void ArtCarsKeepClearOfTheSceneryAllTheWayRound()
        {
            var (grounds,twists)=OnTheGrounds();var scenery=Scenery(grounds);
            foreach(var landmark in new[]{"FestivalMoon","FestivalBuntingPole","FestivalTreeB","ambient_PoiPerformer"})
                Assert.That(scenery.Exists(thing=>thing.Name.StartsWith(landmark)),Is.True,"setup: the grounds have a "+landmark);
            var playa=Part(twists.Root,FestivalTwistVisuals.PlayaRootName);var state=Round(Festivals.PlayaFestival);var hits=new SortedDictionary<string,double>();
            // A lap is 46 m at ArtCarSpeed, 38.3 s, so 40 s takes each car all the way round, every corner included.
            for(double t=0;t<40;t+=.25)
            {
                state.ElapsedSeconds=t;twists.Visuals.Apply(state,10);
                for(int k=0;k<Festivals.ArtCars;k++)Hits(Part(playa,"Art car "+k),scenery,hits,"car "+k,t);
            }
            Assert.That(hits.Keys,Is.Empty,"the rules route each loop clear of trees, totems and the crowd, so the car on it must clear them too:"+Listed(hits));
        }

        // PLAYA-2: the rules route each loop past the crowd's standing spots, but a car drawn 2.8 m wide behind a van reaching
        // 3.6 m ahead drove through the festivalgoers standing beside its loop on every lap. Over 40 crews' crowds (each seed
        // jitters where they stand), nothing of either car comes within a standing festivalgoer's reach on a full lap. A real
        // round's seed is random, so every spot's whole jitter square is checked too, not just the jitter these 40 seeds draw.
        [Test]public void ArtCarsDriveRoundTheStandingCrowdNotThroughIt()
        {
            var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);var state=Round(Festivals.PlayaFestival);
            var playa=Part(grounds,FestivalTwistVisuals.PlayaRootName);
            Assert.That(Part(Part(playa,"Art car 0"),"FestivalCampVan").GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0),"setup: the real camp van pulls each deck");
            // A lap is 46 m at ArtCarSpeed, 38.3 s, so 40 s takes each car all the way round, every corner included. It runs in
            // 60 fps frames, so each car is checked where it is drawn as it glides along, not at the rules' point; a frame moves a
            // car 2 cm, so even its swing through a corner is caught.
            var lap=new List<(double When,int Car,Vector2 At,List<Box> Parts)>();const float frame=1/60f;
            for(int f=0;f*frame<40;f++)
            {
                double t=f*(double)frame;state.ElapsedSeconds=t;twists.Apply(state,frame);
                for(int k=0;k<Festivals.ArtCars;k++)
                {
                    var car=Part(playa,"Art car "+k);var parts=new List<Box>();
                    foreach(var renderer in car.GetComponentsInChildren<Renderer>())parts.Add(Box.Of(renderer,PathOf(renderer.transform,car)));
                    lap.Add((t,k,Flat(car.position),parts));
                }
            }
            var hits=new SortedDictionary<string,(float Gap,string Where)>();
            void Check(Vector2 feet,string who,string where)
            {
                foreach(var (when,k,at,parts) in lap)
                {
                    if(Vector2.Distance(at,feet)>CarReach+Shoulders)continue;
                    foreach(var part in parts)
                    {
                        float gap=part.Gap(feet,Standing,Shoulders);if(gap>=Shoulders)continue;
                        string hit="car "+k+"'s "+part.Name.Split('/')[0]+" runs through "+who;
                        if(!hits.TryGetValue(hit,out var worst)||gap<worst.Gap)hits[hit]=(gap,where+", "+when.ToString("0.00")+" s in");
                    }
                }
            }
            int crowd=0;
            for(int seed=0;seed<40;seed++)
                foreach(var n in new FestivalSimulation(seed).State.Npcs)
                    if(n.Kind=="Wook"){crowd++;Check(new Vector2(n.X,n.Z),n.Id,"seed "+seed);}
            Assert.That(crowd,Is.EqualTo(40*FestivalCrowdLayout.Count),"setup: every crew's whole standing crowd is checked");
            // Each part's shadow on the ground is convex, so the nearest it comes to a jitter square is on the square's edge: walk
            // every edge in 3 cm steps (a car part's gap moves no faster than the feet, so a step hides under 1.5 cm).
            const int steps=14;float jitter=FestivalCrowdLayout.MaxJitter;
            for(int i=0;i<FestivalCrowdLayout.Count;i++)
            {
                var spot=FestivalCrowdLayout.Spot(i);
                for(int s=0;s<=steps;s++)
                {
                    float along=-jitter+2*jitter*s/steps;
                    foreach(var off in new[]{new Vector2(along,-jitter),new Vector2(along,jitter),new Vector2(-jitter,along),new Vector2(jitter,along)})
                        Check(new Vector2(spot.X,spot.Z)+off,"wook_"+i,"jittered ("+off.x.ToString("0.00")+", "+off.y.ToString("0.00")+")");
                }
            }
            var list="";foreach(var hit in hits)list+="\n  "+hit.Key+" (nearest "+hit.Value.Gap.ToString("0.000")+" m from their middle, "+hit.Value.Where+")";
            Assert.That(hits.Keys,Is.Empty,"the rules route each loop clear of where the crowd stands, however a crew's jitter falls, so the car drawn on it must pass them by too:"+list);
        }
        // The crowd clock and level clock are independent. Combine every sampled walker position over 100 seconds
        // (longer than the slowest authored lap) with every rendered car position over a full 40-second lap.
        [Test]public void ArtCarsClearAmbientWalkersAndEveryFixedSpotAtIndependentClockPhases()
        {
            var grounds=Grounds();var crowd=grounds.gameObject.AddComponent<FestivalAmbientCrowd>();crowd.Build();
            Assert.That(crowd.MemberCount,Is.EqualTo(62));Assert.That(crowd.WalkerCount,Is.EqualTo(12));
            var actors=new List<Transform>();foreach(Transform child in grounds)if(child.name.StartsWith("ambient_walk_"))actors.Add(child);
            Assert.That(actors.Count,Is.EqualTo(12),"sample the actual twelve walker actors");
            var people=new List<(Vector2 Feet,string Who,float Clock)>();
            foreach(var spot in FestivalAmbientCrowd.FixedSpots)people.Add((Flat(spot.Position),spot.Zone,0));
            for(int frame=0;frame<=1000;frame++)
            {
                float clock=frame*.1f;UpdateCrowd(crowd,clock);
                foreach(var actor in actors)people.Add((Flat(actor.position),actor.name,clock));
            }
            var twists=new FestivalTwistVisuals(grounds);var state=Round(Festivals.PlayaFestival);
            var playa=Part(grounds,FestivalTwistVisuals.PlayaRootName);var hits=new SortedDictionary<string,double>();float nearest=100;
            for(int frame=0;frame<=400;frame++)
            {
                float clock=frame*.1f;state.ElapsedSeconds=clock;twists.Apply(state,.1f);
                for(int k=0;k<Festivals.ArtCars;k++)
                {
                    var car=Part(playa,"Art car "+k);var parts=new List<Box>();
                    foreach(var renderer in car.GetComponentsInChildren<Renderer>())parts.Add(Box.Of(renderer,PathOf(renderer.transform,car)));
                    Assert.That(parts.Exists(part=>part.Name.StartsWith("Party deck")),Is.True,"actual deck bounds");
                    Assert.That(parts.Exists(part=>part.Name.StartsWith("FestivalCampVan")),Is.True,"actual van bounds");
                    foreach(var person in people)
                    {
                        if(Vector2.Distance(Flat(car.position),person.Feet)>CarReach+Shoulders+.1f)continue;
                        foreach(var part in parts)
                        {
                            float gap=part.Gap(person.Feet,Standing,Shoulders+.1f);nearest=Mathf.Min(nearest,gap);
                            if(gap>=Shoulders+.1f)continue;
                            string hit="car "+k+" "+part.Name.Split('/')[0]+" / "+person.Who;
                            if(!hits.ContainsKey(hit))hits[hit]=clock;
                        }
                    }
                }
            }
            TestContext.WriteLine("Ambient/car nearest sampled body-axis gap: "+nearest.ToString("0.000")+" m");
            Assert.That(hits.Keys,Is.Empty,"cars must clear all independent crowd phases, with 10 cm sampling margin:"+Listed(hits));
        }

        [TestCase(Festivals.PoloFestival)][TestCase(Festivals.PlayaFestival)]
        public void EastWalkersClearRenderedSceneryAndAllFixedSpotsThroughFullLaps(int festival)
        {
            var world=Made("Clearance world").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");world.SetTwists(Round(festival));
            var grounds=Part(world.transform,FestivalWorld.RootName);var crowd=grounds.GetComponent<FestivalAmbientCrowd>();
            var east=new List<Transform>();foreach(Transform child in grounds)if(child.name.StartsWith("ambient_walk_east_lane_"))east.Add(child);
            Assert.That(east.Count,Is.EqualTo(2));Assert.That(crowd.MemberCount,Is.EqualTo(62));Assert.That(crowd.WalkerCount,Is.EqualTo(12));
            UpdateCrowd(crowd,137.5f);
            typeof(FestivalAmbientCrowd).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(crowd,null);
            var live=new List<Vector3>();foreach(var actor in east)live.Add(actor.position);
            UpdateCrowd(crowd,Time.time);
            for(int i=0;i<east.Count;i++)Assert.That(east[i].position,Is.EqualTo(live[i]),"live Update uses the same authored movement at the current clock");
            var scenery=new List<Box>();
            foreach(var renderer in grounds.GetComponentsInChildren<Renderer>())
            {
                var b=renderer.bounds;string path=PathOf(renderer.transform,grounds);
                if(!renderer.enabled||path.StartsWith("ambient_")||b.size.x>20||b.size.z>20||b.max.y<.12f||b.min.y>Standing)continue;
                scenery.Add(Box.Of(renderer,path));
            }
            Assert.That(scenery.Exists(part=>part.Name.StartsWith("FestivalTree")||part.Name.Contains("Palm")),Is.True,"actual trees, including their low branches");
            Assert.That(scenery.Exists(part=>part.Name.StartsWith("FestivalGroveDetail")),Is.True,"actual fern and rock detail is included");
            var hits=new SortedDictionary<string,double>();float nearest=100;
            for(int frame=0;frame<=1000;frame++)
            {
                float clock=frame*.1f;UpdateCrowd(crowd,clock);
                foreach(var actor in east)
                {
                    var feet=Flat(actor.position);
                    foreach(var part in scenery)
                    {
                        float gap=part.Gap(feet,Standing,Shoulders+.1f);nearest=Mathf.Min(nearest,gap);
                        if(gap<Shoulders+.1f&&!hits.ContainsKey(part.Name))
                        {
                            hits[part.Name]=clock;TestContext.WriteLine("Obstacle "+part.Name+" centre="+part.Centre.ToString("F3")+" extents="+part.Extents.ToString("F3")+" bounds="+part.Aabb.ToString("F3"));
                        }
                    }
                    foreach(var spot in FestivalAmbientCrowd.FixedSpots)
                        if(Vector2.Distance(feet,Flat(spot.Position))<2*Shoulders+.1f&&!hits.ContainsKey("fixed "+spot.Zone))hits["fixed "+spot.Zone]=clock;
                }
            }
            TestContext.WriteLine(Festivals.Name(festival)+" east/scenery nearest sampled body-axis gap: "+nearest.ToString("0.000")+" m");
            Assert.That(hits.Keys,Is.Empty,"the east route must clear scenery and all fixed social placements:"+Listed(hits));
        }

        internal static void UpdateCrowd(FestivalAmbientCrowd crowd,float elapsed)=>crowd.UpdateWalkers(elapsed);

        internal static float RendererBodyGap(Renderer renderer,Vector3 feet)=>Box.Of(renderer,renderer.name).Gap(Flat(feet),Standing,Shoulders+.1f);

        // A standing festivalgoer, as the rules place them: this tall, and this far round their middle (shoulders and hanging arms).
        // Nothing of a car reaches further than CarReach from its point.
        private const float Standing=1.8f,Shoulders=.3f,CarReach=5;

        [Test]public void TheBurningEffigyKeepsClearOfTheFestoonStrungOverThePath()
        {
            var (grounds,twists)=OnTheGrounds();var scenery=Scenery(grounds);
            Assert.That(scenery.Exists(thing=>thing.Name.StartsWith("Festoon cable")),Is.True,"setup: festoons hang over the grounds");
            var effigy=Part(Part(twists.Root,FestivalTwistVisuals.PlayaRootName),"Effigy");var hits=new SortedDictionary<string,double>();
            var burn=Round(Festivals.PlayaFestival,3);burn.ElapsedSeconds=burn.DurationSeconds-10;
            // The flames stretch as they flicker, so watch a second of it.
            for(int i=0;i<20;i++){twists.Visuals.Apply(burn,.05f);Hits(effigy,scenery,hits,"the effigy",i*.05);}
            Assert.That(hits.Keys,Is.Empty,"the lights strung over the path pass the effigy by, burning or not:"+Listed(hits));
        }

        [Test]public void TheEffigyStandsOverThePathAndBurnsForNightTwosLastThreeMinutes()
        {
            var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);var playa=Part(grounds,FestivalTwistVisuals.PlayaRootName);
            var effigy=Part(playa,"Effigy");var fire=Part(effigy,"Effigy fire");
            foreach(var (level,left,burning) in new[]{(3,Festivals.BurnSeconds+1,false),(3,Festivals.BurnSeconds-1,true),(1,60.0,false),(0,60.0,false)})
            {
                var state=Round(Festivals.PlayaFestival,level);state.ElapsedSeconds=state.DurationSeconds-left;twists.Apply(state,.02f);
                string when="level "+level+", "+left+" s left: ";
                Assert.That(effigy.gameObject.activeInHierarchy,Is.True,when+"the effigy stands all weekend");
                Assert.That(fire.gameObject.activeInHierarchy,Is.EqualTo(burning),when+(burning?"it burns":"it has not caught yet"));
            }
            Assert.That(Vector2.Distance(Flat(effigy.position),new Vector2(Festivals.EffigyX,Festivals.EffigyZ)),Is.LessThan(1e-3f),"the effigy stands where the crowd gathers");
            Assert.That(Bounds(effigy).max.y,Is.GreaterThan(8f),"it towers over the crowd");

            var burn=Round(Festivals.PlayaFestival,3);burn.ElapsedSeconds=burn.DurationSeconds-10;twists.Apply(burn,.05f);
            var light=fire.GetComponentInChildren<Light>();
            Assert.That(light!=null&&light.isActiveAndEnabled&&light.intensity>0,Is.True,"the fire lights the crowd");
            var flames=new List<float>();foreach(Transform flame in fire)flames.Add(flame.localScale.y);
            twists.Apply(burn,.05f);
            int flickered=0,f=0;foreach(Transform flame in fire)if(Mathf.Abs(flame.localScale.y-flames[f++])>1e-3f)flickered++;
            Assert.That(flickered,Is.GreaterThan(1),"the flames flicker");
            var path=new Vector3(Festivals.EffigyX,1,Festivals.EffigyZ);
            foreach(var renderer in effigy.GetComponentsInChildren<Renderer>())
                Assert.That(renderer.bounds.Contains(path),Is.False,"burning or not, the main path runs between its legs, clear of "+renderer.name);
        }

        [Test]public void DustStormsCloseInTheFogOnlyWhileOneBlows()
        {
            var world=Made("Stormy world").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            float dayStart=RenderSettings.fogStartDistance,dayEnd=RenderSettings.fogEndDistance;var dayFog=RenderSettings.fogColor;
            var state=Round(Festivals.PlayaFestival);double storm=-1,calm=-1;
            for(double t=0;t<state.DurationSeconds&&(storm<0||calm<0);t++){state.ElapsedSeconds=t;if(FestivalSimulation.DustStorm(state)){if(storm<0)storm=t;}else if(calm<0)calm=t;}
            Assert.That(storm>=0&&calm>=0,Is.True,"setup: this level has calm and storm");

            state.ElapsedSeconds=calm;world.SetLighting(state,"me");
            Fog(dayStart,dayEnd,dayFog,"calm");
            state.ElapsedSeconds=storm;world.SetLighting(state,"me");
            float stormEnd=RenderSettings.fogEndDistance;var dust=RenderSettings.fogColor;
            Assert.That(stormEnd,Is.InRange(Festivals.DustStormSightRange*2,Festivals.DustStormSightRange*4),"in a storm you see a little further than the festivalgoers' 5 m, and no more");
            Assert.That(RenderSettings.fogStartDistance,Is.LessThan(stormEnd),"storm fog thickens with distance");
            Assert.That(dust.r>dust.g&&dust.g>dust.b,Is.True,"storm fog is dusty, not the evening haze ("+dust+")");

            state.LevelIndex=1;world.SetLighting(state,"me");
            Assert.That(RenderSettings.fogEndDistance,Is.EqualTo(stormEnd).Within(1e-4f),"a night storm is as thick");
            Assert.That(new[]{RenderSettings.fogColor.r,RenderSettings.fogColor.g,RenderSettings.fogColor.b},Is.EqualTo(new[]{dust.r*.2f,dust.g*.2f,dust.b*.2f}).Within(1e-4f),"and as dark as the rest of the night");
            state.LevelIndex=0;state.FestivalIndex=Festivals.PoloFestival;world.SetLighting(state,"me");
            Fog(dayStart,dayEnd,dayFog,"Palm Mirage has no dust");
            state.FestivalIndex=Festivals.PlayaFestival;state.Phase="CampReview";world.SetLighting(state,"me");
            Fog(dayStart,dayEnd,dayFog,"camp");
        }

        [Test]public void TheWorldRaisesTheTwistsOnTheFestivalGrounds()
        {
            var root=Made("Twist world");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var grounds=root.transform.Find(FestivalWorld.RootName);
            foreach(int festival in new[]{Festivals.PoloFestival,Festivals.PlayaFestival})
            {
                world.SetTwists(Round(festival));
                Assert.That(Part(grounds,FestivalTwistVisuals.PoloRootName).gameObject.activeInHierarchy,Is.EqualTo(festival==Festivals.PoloFestival),Festivals.Name(festival)+": Palm Mirage's twists");
                Assert.That(Part(grounds,FestivalTwistVisuals.PlayaRootName).gameObject.activeInHierarchy,Is.EqualTo(festival==Festivals.PlayaFestival),Festivals.Name(festival)+": Ember Playa's twists");
            }
            world.SetPhase("Shopping");
            Assert.That(Part(grounds,FestivalTwistVisuals.PlayaRootName).gameObject.activeInHierarchy,Is.False,"camp hides the twists with the rest of the festival");
        }

        // A crew of three at the start of Day 1 at a festival, played through the real rules.
        private static FestivalSimulation Level(int festival)
        {
            var game=new FestivalSimulation(31);game.State.UnlockedFestivalCount=Festivals.Count;game.State.FestivalIndex=festival;
            for(int k=0;k<3;k++){var id="p"+k;var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
            Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: the crew is at "+Festivals.Name(festival));
            return game;
        }
        // What viewer's client receives, through the wire format.
        private static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        private static RoundState Round(int festival,int level=0)=>new RoundState{Phase="Playing",FestivalIndex=festival,LevelIndex=level,DurationSeconds=Festivals.Level(festival,level,0).DurationSeconds};

        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
        private Transform Grounds()=>Made("Festival grounds").transform;
        private static Transform Part(Transform parent,string name){var part=parent.Find(name);Assert.That(part,Is.Not.Null,name+" is built under "+parent.name);return part;}
        private static Renderer Painted(Transform root,string key)
        {
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.name.EndsWith("__"+key,System.StringComparison.Ordinal))return r;
            Assert.Fail("no __"+key+" renderer under "+root.name);return null;
        }
        private static Transform Rotor(Transform grounds)=>Part(Part(Part(grounds,FestivalTwistVisuals.PoloRootName),"Ferris wheel"),"Wheel rotor");
        private static List<Transform> Gondolas(Transform grounds){var gondolas=new List<Transform>();foreach(Transform child in Part(Part(grounds,FestivalTwistVisuals.PoloRootName),"Ferris wheel"))if(child.name=="Gondola")gondolas.Add(child);return gondolas;}
        // POLO-1's ground frames, which POLO-6 replaced with a phone in the influencer's hand.
        private static List<Transform> Frames(Transform grounds){var frames=new List<Transform>();foreach(Transform child in grounds)if(child.name.StartsWith("Influencer frame "))frames.Add(child);return frames;}
        private static Bounds Bounds(Transform part){var renderers=part.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
        // Where the parts of an art prop in one material sit, e.g. the van's "__Cream" headlamps.
        private static Vector3 Centre(Transform prop,string material)
        {
            var found=new List<Renderer>();foreach(var r in prop.GetComponentsInChildren<Renderer>())if(r.name.Contains(material))found.Add(r);
            Assert.That(found.Count,Is.GreaterThan(0),prop.name+" has "+material+" parts");
            var centre=Vector3.zero;foreach(var r in found)centre+=r.bounds.center;return centre/found.Count;
        }

        // The real festival grounds, with a second set of stand-ins raised on them for the test to drive.
        private (Transform Grounds,(Transform Root,FestivalTwistVisuals Visuals) Twists) OnTheGrounds()
        {
            var world=Made("Playa world").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var grounds=world.transform.Find(FestivalWorld.RootName);
            var root=new GameObject("Stand-ins under test").transform;root.SetParent(grounds,false);
            return (grounds,(root,new FestivalTwistVisuals(root)));
        }
        // Everything standing on the grounds that a stand-in could run into. Left out: the stand-ins themselves, the ground and
        // the flat paths, the ambient walkers (their lanes cross the loops by design) and the fern-and-rock patches, which the
        // rules' loops themselves run over (one lies on car 1's loop at (13,-6)).
        private static List<Box> Scenery(Transform grounds)
        {
            var scenery=new List<Box>();
            foreach(var renderer in grounds.GetComponentsInChildren<Renderer>())
            {
                var bounds=renderer.bounds;string name=PathOf(renderer.transform,grounds);
                if(!renderer.enabled||StandIn(renderer.transform)||bounds.size.x>20||bounds.size.z>20||bounds.max.y<.12f)continue;
                if(name.StartsWith("ambient_walk_")||name.StartsWith("FestivalGroveDetail"))continue;
                scenery.Add(Box.Of(renderer,name));
            }
            return scenery;
        }
        private static bool StandIn(Transform part)
        {
            for(;part!=null;part=part.parent)if(part.name==FestivalTwistVisuals.PoloRootName||part.name==FestivalTwistVisuals.PlayaRootName)return true;
            return false;
        }
        private static string PathOf(Transform part,Transform top){var path=part.name;for(part=part.parent;part!=null&&part!=top;part=part.parent)path=part.name+"/"+path;return path;}
        // Records each stand-in part that runs into a piece of scenery, and when it first did.
        private static void Hits(Transform standIn,List<Box> scenery,SortedDictionary<string,double> hits,string who,double when)
        {
            foreach(var renderer in standIn.GetComponentsInChildren<Renderer>())
            {
                var box=Box.Of(renderer,PathOf(renderer.transform,standIn));
                foreach(var thing in scenery)
                {
                    if(!box.Aabb.Intersects(thing.Aabb)||box.Overlap(thing)<=.02f)continue;
                    string hit=who+"'s "+box.Name.Split('/')[0]+" runs through "+thing.Name.Split('/')[0];
                    if(!hits.ContainsKey(hit))hits[hit]=when;
                }
            }
        }
        private static string Listed(SortedDictionary<string,double> hits){var list="";foreach(var hit in hits)list+="\n  "+hit.Key+" (first at "+hit.Value+" s)";return list;}
        // A renderer's oriented box: its mesh's bounds turned and scaled with it (skinned people and text keep their world box).
        private struct Box
        {
            public Vector3 Centre,Extents;public Vector3[] Axes;public Bounds Aabb;public string Name;
            public static Box Of(Renderer renderer,string name)
            {
                var filter=renderer.GetComponent<MeshFilter>();var t=renderer.transform;
                if(renderer is SkinnedMeshRenderer||filter==null||filter.sharedMesh==null)
                    return new Box{Centre=renderer.bounds.center,Extents=renderer.bounds.extents,Axes=new[]{Vector3.right,Vector3.up,Vector3.forward},Aabb=renderer.bounds,Name=name};
                var mesh=filter.sharedMesh.bounds;var scale=t.lossyScale;
                return new Box{Centre=t.TransformPoint(mesh.center),Extents=Vector3.Scale(mesh.extents,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z))),Axes=new[]{t.right,t.up,t.forward},Aabb=renderer.bounds,Name=name};
            }
            // How deep two boxes run into each other along the axis that best separates them; zero or less means they don't.
            public float Overlap(Box other)
            {
                var axes=new List<Vector3>(Axes);axes.AddRange(other.Axes);
                foreach(var a in Axes)foreach(var b in other.Axes){var c=Vector3.Cross(a,b);if(c.sqrMagnitude>1e-6f)axes.Add(c.normalized);}
                float least=float.MaxValue;var apart=other.Centre-Centre;
                foreach(var axis in axes)least=Mathf.Min(least,Reach(axis)+other.Reach(axis)-Mathf.Abs(Vector3.Dot(apart,axis)));
                return least;
            }
            // The nearest this box comes to an upright body standing at feet, from the ground to height; when even its world box
            // is further than within, that box's distance, which is never more than the real one.
            public float Gap(Vector2 feet,float height,float within)
            {
                float far=Mathf.Sqrt(Aabb.SqrDistance(new Vector3(feet.x,Mathf.Clamp(Aabb.center.y,0,height),feet.y)));
                if(far>=within)return far;
                float best=float.MaxValue;
                // The body's axis is upright and a box's nearest point to it moves smoothly, so sampling it every 5 cm is close enough.
                for(float h=0;h<=height;h+=.05f)
                {
                    var apart=new Vector3(feet.x,h,feet.y)-Centre;var outside=Vector3.zero;
                    for(int i=0;i<3;i++)outside[i]=Mathf.Max(0,Mathf.Abs(Vector3.Dot(apart,Axes[i]))-Extents[i]);
                    best=Mathf.Min(best,outside.magnitude);
                }
                return best;
            }
            private float Reach(Vector3 axis)=>Extents.x*Mathf.Abs(Vector3.Dot(Axes[0],axis))+Extents.y*Mathf.Abs(Vector3.Dot(Axes[1],axis))+Extents.z*Mathf.Abs(Vector3.Dot(Axes[2],axis));
        }
        // A frame edge is a cube stretched along its forward axis from the influencer's feet; this is its far end.
        private static Vector2 Flat(Vector3 at)=>new Vector2(at.x,at.z);
        private static void Fog(float start,float end,Color color,string when)
        {
            Assert.That(RenderSettings.fogStartDistance,Is.EqualTo(start).Within(1e-4f),when+": fog starts where it always does");
            Assert.That(RenderSettings.fogEndDistance,Is.EqualTo(end).Within(1e-4f),when+": the view reaches as far as ever");
            Assert.That(new[]{RenderSettings.fogColor.r,RenderSettings.fogColor.g,RenderSettings.fogColor.b},Is.EqualTo(new[]{color.r,color.g,color.b}).Within(1e-4f),when+": the fog keeps its colour");
        }
    }
}
