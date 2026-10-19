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
            $"tint {p.Tint} haze {p.Haze:F4} vignette {p.Vignette:F4} letterbox {p.Letterbox:F4} fov {p.Fov:F4} roll {p.Roll:F4} lag {p.Lag:F4} saturation {p.Saturation:F3} brightness {p.Brightness:F4} trails {p.Trails:F3} glow {p.Glow:F3} muffle {p.Muffle:F3} lattice {p.Lattice:F3} cells {p.Cells:F3} drift {p.Drift:F4} breathe {p.Breathe:F4}";
        // How strongly each setting is felt.
        private static float[] Amounts(FestivalTrip.Perception p)=>new[]{p.Tint.a,p.Haze,p.Vignette,p.Letterbox,Mathf.Abs(p.Fov),Mathf.Abs(p.Roll),p.Lag,p.Saturation,p.Brightness,p.Trails,p.Glow,p.Muffle,p.Lattice,p.Cells,Mathf.Abs(p.Drift),Mathf.Abs(p.Breathe)};
        // Everything but motion: colour, haze, vignette, letterbox, the grade, trails, glow, sound and the surface patterns themselves.
        private static float[] Colour(FestivalTrip.Perception p)=>new[]{p.Tint.r,p.Tint.g,p.Tint.b,p.Tint.a,p.Haze,p.Vignette,p.Letterbox,p.Saturation,p.Brightness,p.Trails,p.Glow,p.Muffle,p.Lattice,p.Cells};
        private static readonly string Nothing=Describe(default);

        [Test]public void EachSubstanceLooksDifferentAtOneDose()
        {
            foreach(var a in Substances)foreach(var b in Substances)
                if(string.CompareOrdinal(a,b)<0)
                    Assert.That(Describe(FestivalTrip.Look(a,1,false,Moment)),Is.Not.EqualTo(Describe(FestivalTrip.Look(b,1,false,Moment))),Name(a)+" and "+Name(b)+" look different at one dose");
            foreach(var id in Substances)Assert.That(Describe(FestivalTrip.Look(id,1,false,Moment)),Is.Not.EqualTo(Nothing),Name(id)+" shows at one dose");
            Assert.That(Describe(FestivalTrip.Look("",4,false,Moment)),Is.EqualTo(Nothing),"a dose of nothing on the wheel (a host from before it) looks plain");
        }

        // The queue's picture of each one, as the settings that carry it, at every dose.
        [Test]public void EachSubstanceFeelsTheWayItsNameSays()
        {
            for(int dose=1;dose<=4;dose++)
            {
                FestivalTrip.Perception Of(string id,float time=Moment)=>FestivalTrip.Look(id,dose,false,time);
                string at=" at dose "+dose;
                var stamps=Of("lsd");
                Assert.That(stamps.Trails,Is.GreaterThan(0),"Tongue Stamps leave colour trails behind moving people"+at);
                Assert.That(stamps.Tint.a,Is.GreaterThan(0),"under a rainbow tint"+at);
                Color.RGBToHSV(Of("lsd",0).Tint,out float before,out _,out _);Color.RGBToHSV(Of("lsd",5).Tint,out float after,out _,out _);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(before*360,after*360)),Is.GreaterThan(30),"that slowly cycles"+at);
                Assert.That(Mathf.Abs(Of("mushrooms").Fov),Is.GreaterThan(1),"Fun Guys make the world breathe: the field of view swells"+at);
                Assert.That(Of("mushrooms",0).Fov,Is.Not.EqualTo(Of("mushrooms").Fov),"and settles, over and over"+at);
                // Both tilt the view to and fro and turn the world more colourful, but no brighter.
                foreach(var id in new[]{"lsd","mushrooms"})
                {
                    float least=float.MaxValue,most=float.MinValue;
                    for(float t=0;t<6;t+=.05f){float roll=Of(id,t).Roll;least=Mathf.Min(least,roll);most=Mathf.Max(most,roll);}
                    Assert.That(most,Is.GreaterThan(0),Name(id)+" tilts the view one way"+at);
                    Assert.That(least,Is.LessThan(0),"and back the other"+at);
                    Assert.That(Mathf.Max(most,-least),Is.LessThanOrEqualTo(3.5f),"a sway, never more than the camera's 3.5 degrees"+at);
                    Assert.That(Of(id).Saturation,Is.GreaterThan(0),Name(id)+" turns the world more colourful"+at);
                    Assert.That(Of(id).Brightness,Is.Zero,Name(id)+" adds no brightness"+at);
                }
                var rolly=Of("ecstasy");
                Assert.That(rolly.Saturation>0&&rolly.Brightness>0&&rolly.Tint.r>rolly.Tint.b,"Rolly Pollies are warm, bright and saturated: "+Describe(rolly)+at);
                Assert.That(rolly.Glow,Is.GreaterThan(0),"with a soft glow around other people"+at);
                foreach(var id in Substances)if(id!="ecstasy")Assert.That(rolly.Brightness,Is.GreaterThan(Of(id).Brightness),"Rolly Pollies are brighter than "+Name(id)+at);
                Assert.That(rolly.Saturation,Is.GreaterThan(Mathf.Max(Of("lsd").Saturation,Of("mushrooms").Saturation)),"and the most saturated"+at);
                var pony=Of("ketamine");
                Assert.That(pony.Vignette>0&&pony.Lag>0&&pony.Muffle>0,"Pony Dust closes the view to a tunnel, drags the camera behind the mouse and muffles the sound: "+Describe(pony)+at);
                var couch=Of("weed");
                Assert.That(couch.Haze>0&&couch.Letterbox>0,"Couch Lock is a soft haze between letterbox bars: "+Describe(couch)+at);
                Assert.That(couch.Tint.g>couch.Tint.r&&couch.Tint.g>couch.Tint.b,"with a green cast"+at);
                foreach(var id in new[]{"ecstasy","ketamine","weed"})Assert.That(Of(id).Roll,Is.Zero,Name(id)+" never tilts the view"+at);
            }
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
                    if(id=="lsd"||id=="mushrooms")for(float t=.25f;t<6;t+=.5f)
                        Assert.That(Mathf.Abs(FestivalTrip.Look(id,dose,false,t).Roll),Is.GreaterThan(Mathf.Abs(FestivalTrip.Look(id,dose-1,false,t).Roll)),Name(id)+" tilts further at dose "+dose+" than at "+(dose-1)+", t="+t);
                }
            // Muffled, not deaf: the rhythm's beat stays audible under the heaviest Pony Dust.
            Assert.That(FestivalTrip.LowPassHz(FestivalTrip.Look("ketamine",4,false,Moment).Muffle),Is.InRange(2000f,8000f),"Pony Dust's low-pass stays mild");
            Assert.That(FestivalTrip.LowPassHz(0),Is.GreaterThanOrEqualTo(20000f),"no muffle, no filtering");
        }

        [Test]public void ReducedMotionCutsCameraMotionToAQuarterButKeepsTheColour()
        {
            Assert.That(Mathf.Abs(FestivalTrip.Look("mushrooms",4,false,Moment).Fov),Is.GreaterThan(0),"setup: Fun Guys swell the view");
            Assert.That(FestivalTrip.Look("ketamine",4,false,Moment).Lag,Is.GreaterThan(0),"setup: Pony Dust drags the camera");
            foreach(var id in new[]{"lsd","mushrooms"})
            {
                Assert.That(Mathf.Abs(FestivalTrip.Look(id,4,false,Moment).Roll),Is.GreaterThan(0),"setup: "+Name(id)+" tilts the view");
                Assert.That(FestivalTrip.Look(id,4,false,Moment).Saturation,Is.GreaterThan(0),"setup: "+Name(id)+" saturates the grade, which counts as colour");
            }
            foreach(var id in Substances)
            {
                var full=FestivalTrip.Look(id,4,false,Moment);var calm=FestivalTrip.Look(id,4,true,Moment);
                Assert.That(Colour(calm),Is.EqualTo(Colour(full)),Name(id)+" keeps its colour, haze, vignette, letterbox, grade, trails, glow and sound");
                Assert.That(calm.Fov,Is.EqualTo(full.Fov*.25f).Within(1e-5f),Name(id)+": the swell is cut to a quarter");
                Assert.That(calm.Roll,Is.EqualTo(full.Roll*.25f).Within(1e-5f),Name(id)+": the tilt is cut to a quarter");
                Assert.That(calm.Lag,Is.EqualTo(full.Lag*.25f).Within(1e-5f),Name(id)+": the lag is cut to a quarter");
            }
            // TRIP-11: the surface pattern stays, but its drift and its breathing move a quarter as far, all through the cycle.
            Assert.That(Mathf.Abs(FestivalTrip.Look("lsd",4,false,Moment).Drift),Is.GreaterThan(0),"setup: Tongue Stamps' lattice drifts");
            Assert.That(Mathf.Abs(FestivalTrip.Look("mushrooms",4,false,Moment).Breathe),Is.GreaterThan(0),"setup: Fun Guys' surfaces breathe");
            foreach(var id in new[]{"lsd","mushrooms"})for(int dose=1;dose<=4;dose++)for(float t=0;t<Cycle;t+=1)
            {
                var full=FestivalTrip.Look(id,dose,false,t);var calm=FestivalTrip.Look(id,dose,true,t);string at=Name(id)+" dose "+dose+" t="+t;
                Assert.That(calm.Drift,Is.EqualTo(full.Drift*FestivalTrip.ReducedMotionShare).Within(1e-5f),at+": the drift is cut to a quarter");
                Assert.That(calm.Breathe,Is.EqualTo(full.Breathe*FestivalTrip.ReducedMotionShare).Within(1e-5f),at+": the breathing is cut to a quarter");
                Assert.That(calm.Lattice,Is.EqualTo(full.Lattice),at+": the lattice stays");Assert.That(calm.Cells,Is.EqualTo(full.Cells),at+": the cells stay");
            }
        }

        // TRIP-11: each part of the surface pattern at its peak over a whole cycle (two minutes, not one moment), per substance and dose.
        private const float Cycle=120,Step=.05f;
        private static float[] Peaks(string id,int dose)
        {
            var most=new float[4];
            for(float t=0;t<Cycle;t+=Step)
            {
                var p=FestivalTrip.Look(id,dose,false,t);float[] now={p.Lattice,p.Cells,Mathf.Abs(p.Drift),Mathf.Abs(p.Breathe)};
                for(int i=0;i<4;i++)most[i]=Mathf.Max(most[i],now[i]);
            }
            return most;
        }
        private const int Lattice=0,Cells=1,Drift=2,Breathe=3;

        // Tongue Stamps lay a crisp lattice on the scenery, subtle at one dose and clearer with each, which starts to drift at three.
        // Fun Guys make surfaces breathe from one dose and grow curved cells from two. Neither borrows the other's, the other three
        // substances draw none, and no part's peak over the cycle shrinks as the dose grows.
        [Test]public void TheSurfacePatternGrowsWithTheDoseOverAWholeCycle()
        {
            var peaks=new Dictionary<string,float[][]>();
            foreach(var id in Substances){peaks[id]=new float[5][];peaks[id][0]=new float[4];for(int dose=1;dose<=4;dose++)peaks[id][dose]=Peaks(id,dose);}
            string[] parts={"lattice","cells","drift","breathing"};
            foreach(var id in Substances)for(int dose=1;dose<=4;dose++)for(int i=0;i<4;i++)
                Assert.That(peaks[id][dose][i],Is.GreaterThanOrEqualTo(peaks[id][dose-1][i]),Name(id)+"'s "+parts[i]+" never fades from dose "+(dose-1)+" to "+dose);
            for(int dose=1;dose<=4;dose++)
            {
                string at=" at dose "+dose;float[] stamps=peaks["lsd"][dose],less=peaks["lsd"][dose-1],guys=peaks["mushrooms"][dose],fewer=peaks["mushrooms"][dose-1];
                Assert.That(stamps[Lattice],Is.GreaterThan(less[Lattice]),"Tongue Stamps' lattice shows, clearer with each dose"+at);
                if(dose<3)Assert.That(stamps[Drift],Is.Zero,"and holds still below three doses"+at);
                else Assert.That(stamps[Drift],Is.GreaterThan(less[Drift]),"and drifts further from three"+at);
                Assert.That(stamps[Cells]+stamps[Breathe],Is.Zero,"Tongue Stamps never grow Fun Guys' cells or breathing"+at);
                Assert.That(guys[Breathe],Is.GreaterThan(fewer[Breathe]),"Fun Guys' surfaces breathe, deeper with each dose"+at);
                if(dose<2)Assert.That(guys[Cells],Is.Zero,"with no cells yet"+at);
                else Assert.That(guys[Cells],Is.GreaterThan(fewer[Cells]),"curved cells grow from two doses"+at);
                Assert.That(guys[Lattice]+guys[Drift],Is.Zero,"Fun Guys never draw Tongue Stamps' lattice or drift"+at);
                foreach(var id in new[]{"ecstasy","ketamine","weed"})Assert.That(peaks[id][dose].Sum(),Is.Zero,Name(id)+" draws no surface pattern"+at);
                Assert.That(Mathf.Max(stamps[Lattice],guys[Cells]),Is.LessThanOrEqualTo(1),"a pattern's strength is a share"+at);
                Assert.That(stamps[Drift],Is.LessThanOrEqualTo(1),"the drift stays within a metre"+at);
                Assert.That(guys[Breathe],Is.LessThanOrEqualTo(.2f),"and the breathing within a fifth of the pattern's size"+at);
            }
            // Both move to and fro, and Fun Guys' breathing is irregular: its swells are not all the same depth.
            float least=0,most=0,shallowest=float.MaxValue,deepest=0,before=0,last=0;
            for(float t=0;t<Cycle;t+=Step)
            {
                float drift=FestivalTrip.Look("lsd",4,false,t).Drift,breathe=FestivalTrip.Look("mushrooms",4,false,t).Breathe;
                least=Mathf.Min(least,drift);most=Mathf.Max(most,drift);
                if(t>=2*Step&&last>before&&last>=breathe&&last>0){shallowest=Mathf.Min(shallowest,last);deepest=Mathf.Max(deepest,last);}
                before=last;last=breathe;
            }
            Assert.That(least<0&&most>0,"Tongue Stamps' lattice drifts one way and back");
            Assert.That(deepest-shallowest,Is.GreaterThan(.2f*deepest),"Fun Guys' swells vary in depth, slow and irregular rather than one steady sine");
        }

        // TRIP-11: the surface pattern is the local player's own dose, day and night (it is on the scenery, not a murk over the view,
        // so the night's tint cap leaves it alone), and clears the moment the dose, the level or the session goes.
        [Test]public void TheSurfacePatternIsYourOwnDoseAndClearsAtOnce()
        {
            string Pattern(FestivalTrip.Perception p)=>$"lattice {p.Lattice:F4} cells {p.Cells:F4} drift {p.Drift:F4} breathe {p.Breathe:F4}";
            string None=Pattern(default);
            foreach(var id in new[]{"lsd","mushrooms"})for(int dose=1;dose<=4;dose++)foreach(int level in new[]{0,1,2,3})
            {
                var state=Round("Playing",dose,id);state.LevelIndex=level;
                Assert.That(Pattern(FestivalTrip.For(state,"me",false,Moment)),Is.EqualTo(Pattern(FestivalTrip.Look(id,dose,false,Moment))).And.Not.EqualTo(None),Name(id)+" dose "+dose+" level "+level+": the dose's own pattern");
            }
            string For(RoundState state)=>Pattern(FestivalTrip.For(state,"me",false,Moment));
            Assert.That(For(Round("Playing",0,"lsd")),Is.EqualTo(None),"sober, though a friend is on four doses");
            Assert.That(For(Round("Playing",4,"lsd",life:"Spirit")),Is.EqualTo(None),"a spirit");
            foreach(var phase in new[]{"Shopping","Spinning","Loading","CampReview"})Assert.That(For(Round(phase,4,"mushrooms")),Is.EqualTo(None),"at camp ("+phase+")");
            var shop=Round("Playing",0);shop.Players[0].Effects.Add(new ActiveEffect{Id="lsd",Intensity=4,RemainingSeconds=60});shop.Players[0].Effects.Add(new ActiveEffect{Id="mushrooms",Intensity=4,RemainingSeconds=60});
            Assert.That(For(shop),Is.EqualTo(None),"shop-bought effects draw no pattern");
            Assert.That(Pattern(FestivalTrip.Gas(2,false,.1f)),Is.EqualTo(None),"nor does Giggle Gas");
            var changing=Round("Playing",4,"lsd");var dosed=changing.Players[0].Effects[0];
            dosed.Substance="mushrooms";
            Assert.That(For(changing),Is.EqualTo(Pattern(FestivalTrip.Look("mushrooms",4,false,Moment))),"a new substance swaps the pattern at once, no lattice left behind");
            dosed.Intensity=0;Assert.That(For(changing),Is.EqualTo(None),"a dose worn down to nothing clears it");
            changing.Players[0].Effects.Clear();Assert.That(For(changing),Is.EqualTo(None),"and so does an ended dose");
            Assert.That(For(null),Is.EqualTo(None),"a client that left");
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

        // The tilt and the extra colour are the local player's own dose, day and night alike, and nothing else's: a sober
        // player, a spirit, camp, a friend's dose, a shop-bought Tongue Stamp or a client that left see none of them.
        [Test]public void TheTiltAndColourComeFromYourOwnDoseDayAndNight()
        {
            foreach(var id in new[]{"lsd","mushrooms"})
                for(int dose=1;dose<=4;dose++)
                    foreach(int level in new[]{0,1,2,3})
                    {
                        var state=Round("Playing",dose,id);state.LevelIndex=level;
                        var look=FestivalTrip.For(state,"me",false,Moment);var own=FestivalTrip.Look(id,dose,false,Moment);
                        string where=Name(id)+" dose "+dose+" level "+level;
                        Assert.That(look.Roll,Is.EqualTo(own.Roll).And.Not.EqualTo(0),where+": the dose's own tilt");
                        Assert.That(look.Saturation,Is.EqualTo(own.Saturation).And.GreaterThan(0),where+": the dose's own colour, night included");
                        Assert.That(look.Brightness,Is.Zero,where+": no brightness");
                        if(FestivalNightLighting.IsNight(state))Assert.That(look.Tint.a,Is.LessThanOrEqualTo(FestivalTrip.NightTint),where+": the night's tint stays a whisper");
                        Assert.That(FestivalTrip.For(state,"me",true,Moment).Roll,Is.EqualTo(own.Roll*FestivalTrip.ReducedMotionShare).Within(1e-5f),where+": a quarter with reduced motion");
                    }
            float Roll(RoundState state)=>FestivalTrip.For(state,"me",false,Moment).Roll;
            Assert.That(Roll(Round("Playing",0,"lsd")),Is.Zero,"sober, though a friend is on four doses");
            Assert.That(Roll(Round("Playing",4,"lsd",life:"Spirit")),Is.Zero,"a spirit");
            foreach(var phase in new[]{"Shopping","Spinning","Loading","CampReview"})Assert.That(Roll(Round(phase,4,"lsd")),Is.Zero,"at camp ("+phase+")");
            Assert.That(Roll(Round(FestivalSimulation.MinigamePhase,4,"lsd")),Is.EqualTo(FestivalTrip.Look("lsd",4,false,Moment).Roll),"through a minigame's freeze-frame");
            Assert.That(Roll(Round("Results",4,"mushrooms")),Is.EqualTo(FestivalTrip.Look("mushrooms",4,false,Moment).Roll),"through the level's results");
            var shop=Round("Playing",0);shop.Players[0].Effects.Add(new ActiveEffect{Id="lsd",Intensity=4,RemainingSeconds=60});shop.Players[0].Effects.Add(new ActiveEffect{Id="mushrooms",Intensity=4,RemainingSeconds=60});
            Assert.That(Describe(FestivalTrip.For(shop,"me",false,Moment)),Is.EqualTo(Nothing),"shop-bought effects keep their own camera roll, outside the dose's look");
            var stacked=Round("Playing",2,"mushrooms");stacked.Players[0].Effects.Add(new ActiveEffect{Id="lsd",Intensity=4,RemainingSeconds=60});
            Assert.That(Describe(FestivalTrip.For(stacked,"me",false,Moment)),Is.EqualTo(Describe(FestivalTrip.Look("mushrooms",2,false,Moment))),"a shop Tongue Stamp on top of a dose of Fun Guys leaves the dose's look alone");
            Assert.That(Roll(null),Is.Zero,"a client that left");
        }

        // The rhythm arrows follow the dose's substance: Tongue Stamps trail, Fun Guys curve, and the look-ahead is fixed and mild per
        // substance (Rolly Pollies later, Pony Dust earlier), the same for every lane.
        [Test]public void TheArrowsFollowTheSubstance()
        {
            var me=new PlayerState{Id="me"};
            Assert.That(FestivalTrip.LaneEffect(me),Is.Empty,"sober arrows are plain");
            Assert.That(FestivalTrip.LaneDose(me),Is.Zero,"sober arrows have no dose twist");
            me.Effects.Add(new ActiveEffect{Id="shot",RemainingSeconds=60});me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,Substance="mushrooms"});
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("mushrooms"),"a dose's substance sets the arrows, ahead of anything else taken");
            Assert.That(FestivalTrip.LaneDose(me),Is.EqualTo(2),"the selected substance carries its actual dose");
            me.Effects.Add(new ActiveEffect{Id="lsd",Intensity=4,RemainingSeconds=60});
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("mushrooms"),"a newer shop effect cannot override the named dose");
            Assert.That(FestivalTrip.LaneDose(me),Is.EqualTo(2),"shop intensity cannot replace the selected dose");
            Assert.That(Catalog.FindEffect("ecstasy").LeadSeconds,Is.EqualTo(1.6).Within(1e-9),"Rolly Pollies' arrows appear a little later");
            Assert.That(Catalog.FindEffect("ketamine").LeadSeconds,Is.EqualTo(2.4).Within(1e-9),"Pony Dust's a little earlier");
            foreach(var id in new[]{"lsd","mushrooms","weed"})Assert.That(Catalog.FindEffect(id).LeadSeconds,Is.EqualTo(2).Within(1e-9),Name(id)+" keeps the usual 2 s");
        }

        [Test]public void ShopAndLegacyDosesKeepTheExistingEffectSelection()
        {
            var me=new PlayerState{Id="me"};
            me.Effects.Add(new ActiveEffect{Id="lsd",Intensity=4,RemainingSeconds=60});
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("lsd"));
            Assert.That(FestivalTrip.LaneDose(me),Is.Zero,"a shop Tongue Stamp keeps its original flight, regardless of shop intensity");
            var unnamed=new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance=""};me.Effects.Add(unnamed);
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("lsd"),"an old unnamed dose still falls back to the catalog effect");
            Assert.That(FestivalTrip.LaneDose(me),Is.Zero,"the fallback shop look never borrows an unnamed dose's strength");
            me.Effects.Add(new ActiveEffect{Id="mushrooms",Intensity=1});
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("mushrooms"),"legacy fallback still chooses the newest catalog effect");
            Assert.That(FestivalTrip.LaneDose(me),Is.Zero);
            unnamed.Substance="ketamine";
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("ketamine"),"a named dose still precedes the newest shop effect");
            Assert.That(FestivalTrip.LaneDose(me),Is.EqualTo(4));
            foreach(int intensity in new[]{0,-1})
            {
                unnamed.Intensity=intensity;
                Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("ketamine"),"nonpositive legacy intensity does not change look-ahead selection");
                Assert.That(FestivalTrip.LaneDose(me),Is.Zero,"a nonpositive dose introduces no travel twist");
            }
            unnamed.Substance="";me.Effects.Clear();me.Effects.Add(unnamed);
            Assert.That(FestivalTrip.LaneEffect(me),Is.Empty);Assert.That(FestivalTrip.LaneDose(me),Is.Zero);
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
