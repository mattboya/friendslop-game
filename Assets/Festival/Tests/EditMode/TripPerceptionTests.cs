using System;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Constraints;
using UnityEngine.UI;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // TRIP-5: each substance looks and feels different on the dosed player's own screen, and grows with the dose. Reduced motion
    // keeps the colour but cuts camera motion to a quarter; a sober player or a spirit sees none of it.
    public sealed class TripPerceptionTests
    {
        // A moment when the Fun Guys swell is near its peak.
        private const float Moment=2;
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        private static IReadOnlyList<string> Substances=>FestivalSimulation.Substances;
        private static string Name(string id)=>Catalog.FindEffect(id).Name;
        // Every setting, so two looks can be told apart and a failure shows them.
        private static string Describe(FestivalTrip.Perception p)=>
            $"tint {p.Tint} haze {p.Haze:F4} vignette {p.Vignette:F4} letterbox {p.Letterbox:F4} fov {p.Fov:F4} lag {p.Lag:F4} saturation {p.Saturation:F3} brightness {p.Brightness:F4} trails {p.Trails:F3} glow {p.Glow:F3} muffle {p.Muffle:F3}";
        // How strongly each setting is felt.
        private static float[] Amounts(FestivalTrip.Perception p)=>new[]{p.Tint.a,p.Haze,p.Vignette,p.Letterbox,Mathf.Abs(p.Fov),p.Lag,p.Saturation,p.Brightness,p.Trails,p.Glow,p.Muffle};
        // Everything but camera motion: colour, haze, vignette, letterbox, the grade, trails, glow and sound.
        private static float[] Colour(FestivalTrip.Perception p)=>new[]{p.Tint.r,p.Tint.g,p.Tint.b,p.Tint.a,p.Haze,p.Vignette,p.Letterbox,p.Saturation,p.Brightness,p.Trails,p.Glow,p.Muffle};
        private static readonly string Nothing=Describe(default);

        [Test]public void EachSubstanceLooksDifferentAtOneDose()
        {
            foreach(var a in Substances)foreach(var b in Substances)
                if(string.CompareOrdinal(a,b)<0)
                    Assert.That(Describe(FestivalTrip.Look(a,1,false,Moment)),Is.Not.EqualTo(Describe(FestivalTrip.Look(b,1,false,Moment))),Name(a)+" and "+Name(b)+" look different at one dose");
            foreach(var id in Substances)Assert.That(Describe(FestivalTrip.Look(id,1,false,Moment)),Is.Not.EqualTo(Nothing),Name(id)+" shows at one dose");
            Assert.That(Describe(FestivalTrip.Look("",4,false,Moment)),Is.EqualTo(Nothing),"a dose of nothing on the wheel (a host from before it) looks plain");
        }

        // The queue's picture of each one, as the settings that carry it.
        [Test]public void EachSubstanceFeelsTheWayItsNameSays()
        {
            FestivalTrip.Perception Of(string id,float time=Moment)=>FestivalTrip.Look(id,2,false,time);
            var stamps=Of("lsd");
            Assert.That(stamps.Trails,Is.GreaterThan(0),"Tongue Stamps leave colour trails behind moving people");
            Assert.That(stamps.Tint.a,Is.GreaterThan(0),"under a rainbow tint");
            Color.RGBToHSV(Of("lsd",0).Tint,out float before,out _,out _);Color.RGBToHSV(Of("lsd",5).Tint,out float after,out _,out _);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before*360,after*360)),Is.GreaterThan(30),"that slowly cycles");
            Assert.That(Mathf.Abs(Of("mushrooms").Fov),Is.GreaterThan(1),"Fun Guys make the world breathe: the field of view swells");
            Assert.That(Of("mushrooms",0).Fov,Is.Not.EqualTo(Of("mushrooms").Fov),"and settles, over and over");
            var rolly=Of("ecstasy");
            Assert.That(rolly.Saturation>0&&rolly.Brightness>0&&rolly.Tint.r>rolly.Tint.b,"Rolly Pollies are warm, bright and saturated: "+Describe(rolly));
            Assert.That(rolly.Glow,Is.GreaterThan(0),"with a soft glow around other people");
            var pony=Of("ketamine");
            Assert.That(pony.Vignette>0&&pony.Lag>0&&pony.Muffle>0,"Pony Dust closes the view to a tunnel, drags the camera behind the mouse and muffles the sound: "+Describe(pony));
            var couch=Of("weed");
            Assert.That(couch.Haze>0&&couch.Letterbox>0,"Couch Lock is a soft haze between letterbox bars: "+Describe(couch));
            Assert.That(couch.Tint.g>couch.Tint.r&&couch.Tint.g>couch.Tint.b,"with a green cast");
        }

        [Test]public void StrengthGrowsWithTheDose()
        {
            foreach(var id in Substances)
                for(int dose=2;dose<=4;dose++)
                {
                    var less=FestivalTrip.Look(id,dose-1,false,Moment);var more=FestivalTrip.Look(id,dose,false,Moment);
                    float[] a=Amounts(less),b=Amounts(more);
                    for(int i=0;i<a.Length;i++)Assert.That(b[i],Is.GreaterThanOrEqualTo(a[i]),Name(id)+": nothing fades from dose "+(dose-1)+" to "+dose+"\n"+Describe(less)+"\n"+Describe(more));
                    Assert.That(b.Sum(),Is.GreaterThan(a.Sum()),Name(id)+" is stronger at dose "+dose+" than at "+(dose-1));
                }
            // Muffled, not deaf: the rhythm's beat stays audible under the heaviest Pony Dust.
            Assert.That(FestivalTrip.LowPassHz(FestivalTrip.Look("ketamine",4,false,Moment).Muffle),Is.InRange(2000f,8000f),"Pony Dust's low-pass stays mild");
            Assert.That(FestivalTrip.LowPassHz(0),Is.GreaterThanOrEqualTo(20000f),"no muffle, no filtering");
        }

        [Test]public void ReducedMotionCutsCameraMotionToAQuarterButKeepsTheColour()
        {
            Assert.That(Mathf.Abs(FestivalTrip.Look("mushrooms",4,false,Moment).Fov),Is.GreaterThan(0),"setup: Fun Guys swell the view");
            Assert.That(FestivalTrip.Look("ketamine",4,false,Moment).Lag,Is.GreaterThan(0),"setup: Pony Dust drags the camera");
            foreach(var id in Substances)
            {
                var full=FestivalTrip.Look(id,4,false,Moment);var calm=FestivalTrip.Look(id,4,true,Moment);
                Assert.That(Colour(calm),Is.EqualTo(Colour(full)),Name(id)+" keeps its colour, haze, vignette, letterbox, trails, glow and sound");
                Assert.That(calm.Fov,Is.EqualTo(full.Fov*.25f).Within(1e-5f),Name(id)+": the swell is cut to a quarter");
                Assert.That(calm.Lag,Is.EqualTo(full.Lag*.25f).Within(1e-5f),Name(id)+": the lag is cut to a quarter");
            }
        }

        // Cross-cutting call 3: a dose layer shows only while the festival is shown, for the local player's own dose of 1 or more,
        // and never for a spirit; never another effect (a shop's Tongue Stamps), never a friend's dose.
        [Test]public void ASoberPlayerOrASpiritGetsNone()
        {
            string For(RoundState state)=>Describe(FestivalTrip.For(state,"me",false,Moment));
            Assert.That(For(Round("Playing",2)),Is.Not.EqualTo(Nothing),"setup: a dosed player trips");
            Assert.That(For(Round("Results",2)),Is.Not.EqualTo(Nothing),"through the level's results");
            Assert.That(For(Round("Playing",0)),Is.EqualTo(Nothing),"a sober player sees none, though a friend is on four doses");
            Assert.That(For(Round("Playing",2,life:"Spirit")),Is.EqualTo(Nothing),"a spirit sees none");
            foreach(var phase in new[]{"Shopping","Spinning","Loading","CampReview"})Assert.That(For(Round(phase,4)),Is.EqualTo(Nothing),"none at camp ("+phase+")");
            var shop=Round("Playing",0);shop.Players[0].Effects.Add(new ActiveEffect{Id="lsd",Intensity=1,RemainingSeconds=60});
            Assert.That(For(shop),Is.EqualTo(Nothing),"keyed on the dose alone, not on a Tongue Stamp bought at the shop");
            Assert.That(For(null),Is.EqualTo(Nothing),"a client that left sees none");
        }

        // The rhythm arrows follow the dose's substance: Tongue Stamps trail, Fun Guys curve, and the look-ahead is fixed and mild per
        // substance (Rolly Pollies later, Pony Dust earlier), the same for every lane.
        [Test]public void TheArrowsFollowTheSubstance()
        {
            var me=new PlayerState{Id="me"};
            Assert.That(FestivalTrip.LaneEffect(me),Is.Empty,"sober arrows are plain");
            me.Effects.Add(new ActiveEffect{Id="shot",RemainingSeconds=60});me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,Substance="mushrooms"});
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("mushrooms"),"a dose's substance sets the arrows, ahead of anything else taken");
            Assert.That(Catalog.FindEffect("ecstasy").LeadSeconds,Is.EqualTo(1.6).Within(1e-9),"Rolly Pollies' arrows appear a little later");
            Assert.That(Catalog.FindEffect("ketamine").LeadSeconds,Is.EqualTo(2.4).Within(1e-9),"Pony Dust's a little earlier");
            foreach(var id in new[]{"lsd","mushrooms","weed"})Assert.That(Catalog.FindEffect(id).LeadSeconds,Is.EqualTo(2).Within(1e-9),Name(id)+" keeps the usual 2 s");
        }

        // Pony Dust's camera trails the mouse: no lag follows at once, a lag closes most of the gap within a few lags, never overshoots,
        // and goes the short way round.
        [Test]public void TheLaggingCameraCatchesUpTheShortWayRound()
        {
            Assert.That(FestivalTrip.Follow(10,50,0,.016f),Is.EqualTo(50),"no lag, no trailing");
            float angle=10;for(int frame=0;frame<6;frame++)angle=FestivalTrip.Follow(angle,50,.25f,.016f);
            Assert.That(angle,Is.GreaterThan(10).And.LessThan(30),"a quarter-second lag is still well behind after 0.1 s");
            for(int frame=0;frame<90;frame++)angle=FestivalTrip.Follow(angle,50,.25f,.016f);
            Assert.That(angle,Is.EqualTo(50).Within(.5f).And.LessThanOrEqualTo(50),"and has caught up 1.5 s later, without overshooting");
            Assert.That(FestivalTrip.Follow(350,10,.25f,.1f),Is.GreaterThan(350).And.LessThan(370),"from 350 towards 10 degrees it turns forward through 360, not back round");
        }

        [Test]public void TheOverlaySitsUnderTheHudAndDrawsTheLook()
        {
            var trip=Trip();var view=View();var couch=Round("Playing",4,"weed");
            trip.Apply(couch,"me",view,NoActors,false,Moment,.02f);
            var canvas=trip.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder,Is.LessThan(100),"under the HUD (100), so the HUD stays readable over the bars");
            var look=FestivalTrip.For(couch,"me",false,Moment);
            Assert.That(canvas.gameObject.activeSelf,"the overlay is up for a dose");
            Assert.That(Part<Image>(trip,"Trip tint").color,Is.EqualTo(look.Tint),"tinted");
            Assert.That(Part<Image>(trip,"Trip haze").color.a,Is.EqualTo(look.Haze),"hazed");
            Assert.That(Part<Image>(trip,"Trip letterbox top").rectTransform.anchorMin.y,Is.EqualTo(1-look.Letterbox).Within(1e-5f),"a bar along the top");
            Assert.That(Part<Image>(trip,"Trip letterbox bottom").rectTransform.anchorMax.y,Is.EqualTo(look.Letterbox).Within(1e-5f),"and one along the bottom");
            foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(true))Assert.That(graphic.raycastTarget,Is.False,graphic.name+" never catches a click");
            var pony=Round("Playing",4,"ketamine");trip.Apply(pony,"me",view,NoActors,false,Moment,.02f);
            Assert.That(Part<RawImage>(trip,"Trip vignette").color.a,Is.EqualTo(FestivalTrip.For(pony,"me",false,Moment).Vignette),"Pony Dust's tunnel");
            var muffle=view.GetComponent<AudioLowPassFilter>();
            Assert.That(muffle!=null&&muffle.enabled,"Pony Dust muffles what the view's listener hears");
            Assert.That(muffle.cutoffFrequency,Is.EqualTo(FestivalTrip.LowPassHz(FestivalTrip.For(pony,"me",false,Moment).Muffle)).Within(1),"by its own cutoff");
            trip.Apply(Round("Playing",0),"me",view,NoActors,false,Moment,.02f);
            Assert.That(canvas.gameObject.activeSelf,Is.False,"sober: no overlay");
            Assert.That(muffle.enabled,Is.False,"and the sound comes back");
        }

        [Test]public void TrailsAndGlowsArePooledCappedUnlitAndFree()
        {
            var trip=Trip();var view=View();var bodies=new Dictionary<string,Transform>();
            var state=Round("Playing",4,"lsd");
            for(int i=0;i<40;i++)
            {
                state.Npcs.Add(new NpcState{Id="n"+i,Kind="Wook"});
                var body=new GameObject("Body n"+i).transform;made.Add(body.gameObject);body.position=new Vector3(-10+i*.5f,0,8+i%5);bodies["n"+i]=body;
            }
            Func<string,Transform> actorFor=id=>bodies.TryGetValue(id,out var t)?t:null;
            float time=0;
            void Frame(RoundState round){foreach(var body in bodies.Values)body.position+=Vector3.right*.05f;time+=.05f;trip.Apply(round,"me",view,actorFor,false,time,.05f);}
            for(int i=0;i<30;i++)Frame(state);
            var trails=Renderers(trip,"Trip trail");
            Assert.That(trails.Count(r=>r.enabled),Is.InRange(1,FestivalTrip.MaxTrails),"Tongue Stamps leave colour trails behind moving people, capped");
            Assert.That(trails.Count,Is.LessThanOrEqualTo(FestivalTrip.MaxTrails),"from a fixed pool");
            TestDelegate frame=()=>Frame(state);frame();
            Assert.That(frame,Is.Not.AllocatingGCMemory(),"a frame of trails makes no garbage");
            for(int i=0;i<30;i++){time+=.05f;trip.Apply(state,"me",view,actorFor,false,time,.05f);}
            Assert.That(Renderers(trip,"Trip trail").Count(r=>r.enabled),Is.Zero,"people standing still leave no trail, and old ones fade away");

            var rolly=Round("Playing",4,"ecstasy");rolly.Npcs.AddRange(state.Npcs);
            Frame(rolly);
            var glows=Renderers(trip,"Trip glow");
            Assert.That(glows.Count(r=>r.enabled),Is.InRange(1,FestivalTrip.MaxGlows),"Rolly Pollies glow around the people in view, capped");
            var glow=glows.First(r=>r.enabled);var eye=Flat(view.position);
            Assert.That(bodies.Values.Any(b=>Vector3.Distance(Flat(b.position),Flat(glow.transform.position))<.5f&&Vector3.Distance(eye,Flat(glow.transform.position))>Vector3.Distance(eye,Flat(b.position))),"each glow sits just behind its person, so they stand in front of it");
            TestDelegate glowing=()=>Frame(rolly);glowing();
            Assert.That(glowing,Is.Not.AllocatingGCMemory(),"a frame of glows makes no garbage");
            Assert.That(Renderers(trip,"Trip trail").Count(r=>r.enabled),Is.Zero,"Rolly Pollies leave no trails");

            foreach(var r in Renderers(trip,"Trip"))
            {
                Assert.That(r.sharedMaterial.shader.name,Is.EqualTo("Sprites/Default"),r.name+" is drawn with the always-included unlit, fog-free sprite shader");
                Assert.That(r.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off),r.name+" casts no shadow");
                Assert.That(r.GetComponent<Collider>(),Is.Null,r.name+" never collides");
            }
            Frame(Round("Playing",0));
            Assert.That(Renderers(trip,"Trip").Count(r=>r.enabled),Is.Zero,"sobering up clears every trail and glow");
        }

        private static readonly Func<string,Transform> NoActors=_=>null;
        private static Vector3 Flat(Vector3 v)=>new Vector3(v.x,0,v.z);
        private FestivalTrip Trip(){var go=new GameObject("Trip test");made.Add(go);return go.AddComponent<FestivalTrip>();}
        // The first-person view looking up the field, with the listener the game's camera carries.
        private Transform View(){var go=new GameObject("Trip view",typeof(AudioListener));made.Add(go);go.transform.position=new Vector3(0,1.65f,0);return go.transform;}
        private static T Part<T>(FestivalTrip trip,string name) where T:Component=>trip.GetComponentsInChildren<T>(true).First(c=>c.name==name);
        private static List<SpriteRenderer> Renderers(FestivalTrip trip,string prefix)=>trip.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith(prefix)).ToList();
        // A level in the given phase: "me" on the given dose of substance (sober at 0), and a friend on four doses of Pony Dust.
        private static RoundState Round(string phase,int dose,string substance="mushrooms",string life="Alive")
        {
            var state=new RoundState{Phase=phase};
            var me=new PlayerState{Id="me",Life=life};if(dose>0)me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose,Substance=substance});
            var friend=new PlayerState{Id="friend"};friend.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance="ketamine"});
            state.Players.Add(me);state.Players.Add(friend);return state;
        }
    }
}
