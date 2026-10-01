using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // TWISTVIS-1: each festival's twists get stand-ins built from primitives and existing props. They show only on their own
    // festival, follow the round (influencers' frames, the wheel, the art cars, the burn, the storms), never collide, and keep
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
                Assert.That(Frames(grounds).Count,Is.EqualTo(polo?Festivals.Influencers:0),where+"influencers film only at Palm Mirage");
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
            var rotor=Part(wheel,"Wheel rotor");var start=rotor.rotation;float quarter=(float)Festivals.WheelRideSeconds/4;
            twists.Apply(round,quarter);
            Assert.That(Quaternion.Angle(start,rotor.rotation),Is.EqualTo(90).Within(.5f),"a quarter of a ride turns it a quarter");
            for(int k=0;k<3;k++)twists.Apply(round,quarter);
            Assert.That(Quaternion.Angle(start,rotor.rotation),Is.LessThan(.5f),"one ride is one full turn");
        }

        // POLO-2: a gold-trimmed sign over the night market's stock stall says where the rules sell wristbands, and for how much.
        // TextMesh draws through its own board, so like the world's signs it shows only from the market's side and within range.
        [Test]public void AGoldSignMarksTheNightMarketsVipStall()
        {
            var world=Made("VIP stall world").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var grounds=world.transform.Find(FestivalWorld.RootName);var round=Round(Festivals.PoloFestival);world.SetTwists(round);
            var sign=Part(Part(grounds,FestivalTwistVisuals.PoloRootName),FestivalTwistVisuals.VipSignName);
            var text=sign.GetComponentInChildren<TextMesh>(true);
            Assert.That(text!=null?text.text:"(no text)",Is.EqualTo("VIP WRISTBANDS $"+Catalog.FindItem(FestivalSimulation.VipWristband).Price),"the sign says what the stall sells, for what the rules charge");
            Assert.That(Part(sign,"Gold trim").GetComponent<Renderer>().sharedMaterial,Is.EqualTo(FestivalArtView.MaterialFor("StageGlowGold")),"its trim is gold, and glows so it reads at night");
            Transform stall=null;foreach(Transform child in grounds)if(child.name=="FestivalStallStock")stall=child;
            Assert.That(stall,Is.Not.Null,"setup: the night market has its stock stall");
            var under=Bounds(stall);var board=Part(sign,"Board").GetComponent<Renderer>().bounds;
            Assert.That(board.min.y,Is.GreaterThan(under.max.y),"the board hangs over the stall's awning, clear of it");
            Assert.That(board.center.x,Is.InRange(under.min.x,under.max.x),"over the stall, not beside it");
            Assert.That(board.center.z,Is.InRange(under.min.z,under.max.z),"over the stall, not in front of or behind it");
            Assert.That(Vector2.Distance(Flat(board.center),new Vector2(Festivals.VipStallX,Festivals.VipStallZ)),Is.LessThanOrEqualTo(Festivals.VipStallRange),"where the rules sell wristbands");
            var renderer=text.GetComponent<Renderer>();
            Assert.That(new[]{renderer.bounds.min.x,renderer.bounds.max.x},Is.EqualTo(new[]{board.center.x,board.center.x}).Within(board.extents.x),"the lettering fits across the board");var previous=FestivalCharacter.ViewTransform;var eye=Made("Eye").transform;
            try
            {
                FestivalCharacter.ViewTransform=eye;
                foreach(var (at,shown,where) in new[]{(new Vector3(Festivals.VipStallX,1.65f,-24),true,"from the market's footpath"),(new Vector3(Festivals.VipStallX+6,1.65f,-30),true,"from down the path"),
                    (new Vector3(Festivals.VipStallX,1.65f,-14),false,"from behind, where it would read mirrored"),(new Vector3(Festivals.VipStallX,1.65f,-50),false,"from across the grounds, through everything between")})
                {
                    eye.position=at;world.SetTwists(round);
                    Assert.That(renderer.enabled,Is.EqualTo(shown),"the lettering "+(shown?"shows ":"hides ")+where);
                }
            }
            finally{FestivalCharacter.ViewTransform=previous;}
        }

        [Test]public void InfluencersFilmThroughAFrameShapedLikeTheRules()
        {
            var view=Wire(Level(Festivals.PoloFestival),"p0");var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);
            twists.Apply(view,.02f);
            var influencers=view.Npcs.FindAll(n=>n.Twist==FestivalSimulation.Influencer);
            Assert.That(influencers.Count,Is.EqualTo(Festivals.Influencers),"setup: the view carries every influencer");
            foreach(var n in influencers)
            {
                var frame=Part(grounds,FestivalTwistVisuals.FramePrefix+n.Id);
                Assert.That(Vector2.Distance(Flat(frame.position),new Vector2(n.X,n.Z)),Is.LessThan(1e-3f),n.Id+"'s frame starts at them");
                foreach(float side in new[]{-1f,1f})
                {
                    var edge=End(Part(frame,side<0?"Frame edge left":"Frame edge right"))-frame.position;
                    Assert.That(new Vector2(edge.x,edge.z).magnitude,Is.EqualTo(Festivals.FilmRange).Within(.05f),n.Id+": the frame reaches as far as filming does");
                    Assert.That(Mathf.DeltaAngle(n.Yaw,Mathf.Atan2(edge.x,edge.z)*Mathf.Rad2Deg),Is.EqualTo(side*Festivals.FilmConeDegrees/2).Within(.5f),n.Id+": the frame is as wide as the phone sees");
                }
                // URP lights one ground mesh with only 4 additional lights, so a phone spot light changed 0.5% of pixels by an
                // invisible 0.2/255: the glowing outline alone carries the frame, without three realtime lights.
                Assert.That(frame.GetComponentInChildren<Light>(),Is.Null,n.Id+"'s frame reads by its glowing outline, not a light that doesn't show");
            }

            // Frames follow the view: an influencer walks off and turns, another stops filming.
            var walker=influencers[0];walker.X+=10;walker.Yaw+=90;var quitter=influencers[1];quitter.Twist="";
            twists.Apply(view,.02f);
            var moved=Part(grounds,FestivalTwistVisuals.FramePrefix+walker.Id);
            Assert.That(Vector2.Distance(Flat(moved.position),new Vector2(walker.X,walker.Z)),Is.LessThan(1e-3f),"a frame follows its influencer across the grounds");
            Assert.That(Mathf.DeltaAngle(moved.eulerAngles.y,walker.Yaw),Is.EqualTo(0).Within(.5f),"and turns with them");
            Assert.That(grounds.Find(FestivalTwistVisuals.FramePrefix+quitter.Id),Is.Null,"an influencer who stops filming loses their frame");
            Assert.That(Frames(grounds).Count,Is.EqualTo(Festivals.Influencers-1),"the others keep filming");
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
        // jitters where they stand), nothing of either car comes within a standing festivalgoer's reach on a full lap.
        [Test]public void ArtCarsDriveRoundTheStandingCrowdNotThroughIt()
        {
            var grounds=Grounds();var twists=new FestivalTwistVisuals(grounds);var state=Round(Festivals.PlayaFestival);
            var playa=Part(grounds,FestivalTwistVisuals.PlayaRootName);
            Assert.That(Part(Part(playa,"Art car 0"),"FestivalCampVan").GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0),"setup: the real camp van pulls each deck");
            // A lap is 46 m at ArtCarSpeed, 38.3 s, so 40 s takes each car all the way round, every corner included.
            var lap=new List<(double When,int Car,Vector2 At,List<Box> Parts)>();
            for(double t=0;t<40;t+=.1)
            {
                state.ElapsedSeconds=t;twists.Apply(state,10);
                for(int k=0;k<Festivals.ArtCars;k++)
                {
                    var car=Part(playa,"Art car "+k);var parts=new List<Box>();
                    foreach(var renderer in car.GetComponentsInChildren<Renderer>())parts.Add(Box.Of(renderer,PathOf(renderer.transform,car)));
                    lap.Add((t,k,Flat(car.position),parts));
                }
            }
            var hits=new SortedDictionary<string,string>();int crowd=0;
            for(int seed=0;seed<40;seed++)
                foreach(var n in new FestivalSimulation(seed).State.Npcs)
                {
                    if(n.Kind!="Wook")continue;
                    crowd++;var feet=new Vector2(n.X,n.Z);
                    foreach(var (when,k,at,parts) in lap)
                    {
                        if(Vector2.Distance(at,feet)>CarReach+Shoulders)continue;
                        foreach(var part in parts)
                        {
                            float gap=part.Gap(feet,Standing,Shoulders);if(gap>=Shoulders)continue;
                            string hit="car "+k+"'s "+part.Name.Split('/')[0]+" runs through "+n.Id;
                            if(!hits.ContainsKey(hit))hits[hit]="seed "+seed+", "+when.ToString("0.0")+" s in, "+gap.ToString("0.00")+" m from their middle";
                        }
                    }
                }
            Assert.That(crowd,Is.EqualTo(40*FestivalCrowdLayout.Count),"setup: every crew's whole standing crowd is checked");
            var list="";foreach(var hit in hits)list+="\n  "+hit.Key+" (first "+hit.Value+")";
            Assert.That(hits.Keys,Is.Empty,"the rules route each loop clear of where the crowd stands, so the car drawn on it must pass them by too:"+list);
        }
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
        private static List<Transform> Frames(Transform grounds){var frames=new List<Transform>();foreach(Transform child in grounds)if(child.name.StartsWith(FestivalTwistVisuals.FramePrefix))frames.Add(child);return frames;}
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
            for(;part!=null;part=part.parent)if(part.name==FestivalTwistVisuals.PoloRootName||part.name==FestivalTwistVisuals.PlayaRootName||part.name.StartsWith(FestivalTwistVisuals.FramePrefix))return true;
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
        private static Vector3 End(Transform edge)=>edge.position+edge.forward*edge.lossyScale.z*.5f;
        private static Vector2 Flat(Vector3 at)=>new Vector2(at.x,at.z);
        private static void Fog(float start,float end,Color color,string when)
        {
            Assert.That(RenderSettings.fogStartDistance,Is.EqualTo(start).Within(1e-4f),when+": fog starts where it always does");
            Assert.That(RenderSettings.fogEndDistance,Is.EqualTo(end).Within(1e-4f),when+": the view reaches as far as ever");
            Assert.That(new[]{RenderSettings.fogColor.r,RenderSettings.fogColor.g,RenderSettings.fogColor.b},Is.EqualTo(new[]{color.r,color.g,color.b}).Within(1e-4f),when+": the fog keeps its colour");
        }
    }
}
