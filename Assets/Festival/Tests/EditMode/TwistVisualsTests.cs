using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // TWISTVIS-1: each festival's twists get stand-ins built from primitives and existing props. They show only on their own
    // festival, follow the round (influencers' frames, the wheel, the art cars, the burn, the storms) and never collide.
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
                var light=frame.GetComponentInChildren<Light>();
                Assert.That(light,Is.Not.Null,n.Id+"'s phone lights its frame");
                Assert.That(light.type,Is.EqualTo(LightType.Spot),n.Id+"'s phone light is a cone");
                Assert.That(light.spotAngle,Is.EqualTo(Festivals.FilmConeDegrees).Within(.01f),n.Id+"'s phone light is as wide as the frame");
                Assert.That(light.range,Is.GreaterThanOrEqualTo(Festivals.FilmRange),n.Id+"'s phone light reaches the end of the frame");
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
                Assert.That(renderer.bounds.Contains(eye),Is.False,"a rider stands on the deck, clear of the camp van towing it ("+renderer.name+")");
            Assert.That(Vector3.Dot(van.position-glider.position,glider.forward),Is.GreaterThan(0),"the van leads the deck");
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
