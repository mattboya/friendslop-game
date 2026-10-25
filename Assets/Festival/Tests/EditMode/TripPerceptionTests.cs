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
            $"tint {p.Tint} haze {p.Haze:F4} vignette {p.Vignette:F4} eye {p.Eye:F4} fov {p.Fov:F4} roll {p.Roll:F4} lag {p.Lag:F4} saturation {p.Saturation:F3} brightness {p.Brightness:F4} trails {p.Trails:F3} glow {p.Glow:F3} muffle {p.Muffle:F3} lattice {p.Lattice:F3} cells {p.Cells:F3} drift {p.Drift:F4} breathe {p.Breathe:F4}";
        // How strongly each setting is felt.
        private static float[] Amounts(FestivalTrip.Perception p)=>new[]{p.Tint.a,p.Haze,p.Vignette,p.Eye,Mathf.Abs(p.Fov),Mathf.Abs(p.Roll),p.Lag,p.Saturation,p.Brightness,p.Trails,p.Glow,p.Muffle,p.Lattice,p.Cells,Mathf.Abs(p.Drift),Mathf.Abs(p.Breathe)};
        // Everything but motion: colour, haze, vignette, the eyes' rest, the grade, trails, glow, sound and the surface
        // patterns themselves.
        private static float[] Colour(FestivalTrip.Perception p)=>new[]{p.Tint.r,p.Tint.g,p.Tint.b,p.Tint.a,p.Haze,p.Vignette,p.Eye,p.Saturation,p.Brightness,p.Trails,p.Glow,p.Muffle,p.Lattice,p.Cells};
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
                Assert.That(couch.Haze>0&&couch.Eye>0,"Couch Lock is a soft haze seen through sleepy eyes: "+Describe(couch)+at);
                Assert.That(couch.Tint.g>couch.Tint.r&&couch.Tint.g>couch.Tint.b,"with a green cast"+at);
                // TRIP-16: Pony Dust and Couch Lock look out through eyes resting as far closed as the dose; the rest keep them open.
                foreach(var id in new[]{"ketamine","weed"})Assert.That(Of(id).Eye,Is.EqualTo(FestivalTrip.EyeRest(id,dose)).And.GreaterThan(0),Name(id)+" rests the eyes the dose's way"+at);
                foreach(var id in new[]{"lsd","mushrooms","ecstasy"})Assert.That(Of(id).Eye,Is.Zero,Name(id)+" keeps the eyes open"+at);
                foreach(var id in new[]{"ecstasy","weed"})Assert.That(Of(id).Roll,Is.Zero,Name(id)+" never tilts the view"+at);
            }
            foreach(float strength in new[]{.5f,1,2})Assert.That(FestivalTrip.Gas(strength,false,0).Eye,Is.Zero,"Giggle Gas keeps its round vignette, with no eye, at "+strength);
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
                Assert.That(Colour(calm),Is.EqualTo(Colour(full)),Name(id)+" keeps its colour, haze, vignette, resting eyes, grade, trails, glow and sound");
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

        // TRIP-16: over a minute, Pony Dust sways the view to and fro, smooth and slow, further with each dose and never past 1.5 degrees.
        // TRIP-17: while the walk stalls the view also wobbles, as far at every dose, eased in and out with the stall and gone once it
        // ends. Reduced motion keeps a quarter of both.
        [Test]public void PonyDustSwaysTheViewAndWobblesItThroughTheStall()
        {
            const float Tick=.01f;float before=0;
            for(int dose=1;dose<=4;dose++)
            {
                var state=Round("Playing",dose,"ketamine");var me=state.Players[0];float s=(float)EffectPresentation.Strength(dose);string at=" at dose "+dose;
                float peak=0,least=0,most=0,floor=0,last=0;int stalled=0;
                for(int i=0;i<6000;i++)
                {
                    float t=i*Tick;state.SimulationSeconds=t;string when=", t="+t+at;
                    float stall=Intoxication.Stall(me,t),roll=FestivalTrip.For(state,"me",false,t).Roll,sway=FestivalTrip.Look("ketamine",dose,false,t).Roll;
                    Assert.That(FestivalTrip.For(state,"me",true,t).Roll,Is.EqualTo(roll*FestivalTrip.ReducedMotionShare).Within(1e-5f),"a quarter under reduced motion, through the stall too"+when);
                    if(i>0)Assert.That(Mathf.Abs(roll-last),Is.LessThan(.1f),"smooth: no jump from one moment to the next"+when);
                    last=roll;
                    if(stall<=0)
                    {
                        Assert.That(roll,Is.EqualTo(sway),"walking freely, the view only sways"+when);
                        Assert.That(Mathf.Abs(roll),Is.LessThanOrEqualTo(1.5f*s+1e-5f),"by at most 1.5 degrees at full strength"+when);
                        peak=Mathf.Max(peak,Mathf.Abs(roll));least=Mathf.Min(least,roll);most=Mathf.Max(most,roll);
                    }
                    else
                    {
                        stalled++;
                        Assert.That(Mathf.Abs(roll-sway),Is.LessThanOrEqualTo(stall+1e-5f),"the stall's wobble is a degree at most, as deep as the stall"+when);
                        if(stall>=1)floor=Mathf.Max(floor,Mathf.Abs(roll-sway));
                    }
                }
                Assert.That(stalled,Is.GreaterThan(0),"setup: the walk stalls"+at);
                Assert.That(least<0&&most>0,"Pony Dust sways the view one way and back"+at);
                Assert.That(peak,Is.GreaterThan(before),"further with each dose"+at);before=peak;
                Assert.That(floor,Is.GreaterThan(.9f),"at the stall's floor the view wobbles by about a degree, whatever the dose"+at);
            }
            Assert.That(before,Is.GreaterThan(1.4f),"four doses sway the view nearly the whole 1.5 degrees");

            // A client's clock runs on past its last snapshot (FestivalSession.EstimatedSimulationSeconds), and the stall follows it.
            float hold=(float)(Intoxication.StallPeriod-Intoxication.StallEase-Intoxication.StallHold/2);
            var behind=Round("Playing",4,"ketamine");
            Assert.That(Intoxication.Stall(behind.Players[0],hold),Is.EqualTo(1),"setup: the stall's floor");
            Assert.That(FestivalTrip.For(behind,"me",false,hold,simulationSeconds:hold).Roll,Is.EqualTo(FestivalTrip.Look("ketamine",4,false,hold,1).Roll)
                .And.Not.EqualTo(FestivalTrip.For(behind,"me",false,hold).Roll),"the stall follows the clock it is given, not the snapshot's");
            var sober=Round("Playing",0);sober.SimulationSeconds=hold;
            Assert.That(Intoxication.Stall(sober.Players[1],hold),Is.EqualTo(1),"setup: the friend on Pony Dust stalls");
            Assert.That(FestivalTrip.For(sober,"me",false,hold).Roll,Is.Zero,"a sober player's view stays level meanwhile");
            var couch=Round("Playing",4,"weed");couch.SimulationSeconds=hold;
            Assert.That(FestivalTrip.For(couch,"me",false,hold).Roll,Is.Zero,"Couch Lock neither sways nor wobbles");
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
            Assert.That(For(Round("Playing",2,life:"Spirit")),Is.EqualTo(Describe(FestivalTrip.Spirit(false))),"a spirit sees no dose, only the spirit look");
            foreach(var phase in new[]{"Shopping","Spinning","Loading","CampReview"})Assert.That(For(Round(phase,4)),Is.EqualTo(Nothing),"none at camp ("+phase+")");
            var shop=Round("Playing",0);shop.Players[0].Effects.Add(new ActiveEffect{Id="lsd",Intensity=1,RemainingSeconds=60});
            Assert.That(For(shop),Is.EqualTo(Nothing),"keyed on the dose alone, not on a Tongue Stamp bought at the shop");
            Assert.That(For(null),Is.EqualTo(Nothing),"a client that left sees none");
        }

        // A spirit's own view looks like a spirit's, so walking through walls never reads as a bug. It holds still, so reduced
        // motion and flashing have nothing to cut, and a dose left on a spirit shows none of itself.
        [Test]public void ASpiritSeesAGhostlyViewThatIsNoDose()
        {
            var spirit=Round("Playing",4,"lsd",life:"Spirit");var look=FestivalTrip.For(spirit,"me",false,Moment);
            Assert.That(look.Saturation,Is.LessThan(-30),"drained of colour");
            Assert.That(look.Tint.a,Is.GreaterThan(0),"under a cast");Assert.That(look.Tint.b,Is.GreaterThan(look.Tint.r),"a cool one");
            Assert.That(look.Vignette,Is.GreaterThan(0).And.LessThan(.5f),"with soft dark edges, not a tunnel");
            var still=look;still.Saturation=still.Vignette=still.Tint.a=0;
            Assert.That(Describe(still),Is.EqualTo(Describe(new FestivalTrip.Perception{Tint=new Color(look.Tint.r,look.Tint.g,look.Tint.b,0)})),"and nothing of a dose: "+Describe(look));
            Assert.That(Describe(FestivalTrip.For(spirit,"me",true,Moment)),Is.EqualTo(Describe(look)),"reduced motion leaves it as it is");
            Assert.That(Describe(FestivalTrip.For(spirit,"me",false,Moment+7.3f)),Is.EqualTo(Describe(look)),"it never moves or flashes");
            spirit.LevelIndex=1;Assert.That(FestivalTrip.For(spirit,"me",false,Moment).Tint.a,Is.LessThanOrEqualTo(FestivalTrip.NightTint),"at night the cast stays a whisper");
            var trip=Trip();var view=View();spirit.LevelIndex=0;
            trip.Apply(spirit,"me",view,NoActors,false,Moment,.02f);
            Assert.That(trip.GetComponentInChildren<Canvas>(true).gameObject.activeSelf,"the overlay draws it");
            Assert.That(Part<RawImage>(trip,"Trip vignette").color.a,Is.EqualTo(look.Vignette));Assert.That(Part<Image>(trip,"Trip tint").color,Is.EqualTo(look.Tint));
            Assert.That(Lids(trip).enabled,Is.False,"through open eyes");
            Assert.That(view.GetComponent<AudioLowPassFilter>(),Is.Null,"and the sound stays clear");
            spirit.Players[0].Life="Alive";spirit.Players[0].Effects.Clear();trip.Apply(spirit,"me",view,NoActors,false,Moment,.02f);
            Assert.That(Describe(FestivalTrip.For(spirit,"me",false,Moment)),Is.EqualTo(Nothing),"revived, the view is plain again");
            Assert.That(trip.GetComponentInChildren<Canvas>(true).gameObject.activeSelf,Is.False,"and the overlay goes");
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
            Assert.That(canvas.sortingOrder,Is.LessThan(100),"under the HUD (100), so the HUD stays readable over the eyes");
            var look=FestivalTrip.For(couch,"me",false,Moment);
            Assert.That(canvas.gameObject.activeSelf,"the overlay is up for a dose");
            Assert.That(Part<Image>(trip,"Trip tint").color,Is.EqualTo(look.Tint),"tinted");
            Assert.That(Part<Image>(trip,"Trip haze").color.a,Is.EqualTo(look.Haze),"hazed");
            // TRIP-16: an eye's opening across the screen, as tall as the lids rest open, with black bars flush above and below it.
            Assert.That(look.Eye,Is.GreaterThan(0),"setup: Couch Lock looks through sleepy eyes");
            Eye(trip,1-look.Eye,"Couch Lock");
            foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(true))Assert.That(graphic.raycastTarget,Is.False,graphic.name+" never catches a click");
            var pony=Round("Playing",4,"ketamine");trip.Apply(pony,"me",view,NoActors,false,Moment,.02f);
            Assert.That(Part<RawImage>(trip,"Trip vignette").color.a,Is.EqualTo(FestivalTrip.For(pony,"me",false,Moment).Vignette),"Pony Dust's tunnel");
            Eye(trip,1-FestivalTrip.For(pony,"me",false,Moment).Eye,"Pony Dust");
            var muffle=view.GetComponent<AudioLowPassFilter>();
            Assert.That(muffle!=null&&muffle.enabled,"Pony Dust muffles what the view's listener hears");
            Assert.That(muffle.cutoffFrequency,Is.EqualTo(FestivalTrip.LowPassHz(FestivalTrip.For(pony,"me",false,Moment).Muffle)).Within(1),"by its own cutoff");
            trip.Apply(Round("Playing",0),"me",view,NoActors,false,Moment,.02f);
            Assert.That(canvas.gameObject.activeSelf,Is.False,"sober: no overlay");
            Assert.That(muffle.enabled,Is.False,"and the sound comes back");
            Assert.That(Lids(trip).enabled,Is.False,"no eye");
            Assert.That(Part<Image>(trip,"Trip letterbox top").rectTransform.anchorMin.y,Is.EqualTo(1),"and the bars fold away");
            Assert.That(Part<Image>(trip,"Trip letterbox bottom").rectTransform.anchorMax.y,Is.EqualTo(0));
        }
        // The eye drawn open to aperture: its opening runs side to side, centred, and the black bars meet it flush above and below.
        private static void Eye(FestivalTrip trip,float aperture,string who)
        {
            var eye=Lids(trip).rectTransform;
            Assert.That(eye.GetComponent<RawImage>().enabled,who+": the lids are drawn");
            Assert.That(eye.anchorMin.x==0&&eye.anchorMax.x==1,who+": from one side of the screen to the other");
            Assert.That(eye.anchorMin.y,Is.EqualTo(.5f-aperture/2).Within(1e-5f),who+": the opening's foot");
            Assert.That(eye.anchorMax.y,Is.EqualTo(.5f+aperture/2).Within(1e-5f),who+": and its top");
            Assert.That(Part<Image>(trip,"Trip letterbox top").rectTransform.anchorMin.y,Is.EqualTo(eye.anchorMax.y).Within(1e-6f),who+": black flush above the eye");
            Assert.That(Part<Image>(trip,"Trip letterbox bottom").rectTransform.anchorMax.y,Is.EqualTo(eye.anchorMin.y).Within(1e-6f),who+": and below it, never over it");
        }
        // The trip's eye, and its opening as drawn: its share of the screen's height.
        private static RawImage Lids(FestivalTrip trip){var eye=trip.GetComponentsInChildren<RawImage>(true).FirstOrDefault(c=>c.name=="Trip eye");Assert.That(eye,Is.Not.Null,"the trip draws an eye");return eye;}
        private static float Opening(FestivalTrip trip){var eye=Lids(trip).rectTransform;return eye.anchorMax.y-eye.anchorMin.y;}

        // Pony Dust and Couch Lock stay sleepy but leave more of a 16:9 screen clear, by day and at night: Pony Dust about 40% at three
        // doses and 35% at four; Couch Lock, with no tunnel, about 55% and 50%.
        [Test]public void SleepyEyesLeaveMoreOfTheScreenClearAndTightenWithEveryDose()
        {
            var floors=new Dictionary<string,(float Three,float Four)>{{"ketamine",(.40f,.35f)},{"weed",(.55f,.50f)}};
            foreach(int level in new[]{0,1})
            {
                var shares=new Dictionary<string,float[]>();string when=Festivals.Night(level)?" at night":" by day";
                foreach(var substance in floors.Keys)
                {
                    var trip=Trip();var view=View();trip.BlinkWaits=()=>60;var share=shares[substance]=new float[5];
                    for(int dose=1;dose<=4;dose++)
                    {
                        var state=Round("Playing",dose,substance);state.LevelIndex=level;
                        trip.Apply(state,"me",view,NoActors,false,Moment,.02f);share[dose]=Clear(trip);
                    }
                    string all=Name(substance)+when+", clear at 1-4 doses: "+string.Join(", ",share.Skip(1).Select(c=>c.ToString("P1")));
                    TestContext.WriteLine(all);
                    Assert.That(share[3],Is.GreaterThanOrEqualTo(floors[substance].Three),all);
                    Assert.That(share[4],Is.GreaterThanOrEqualTo(floors[substance].Four),all);
                    for(int dose=2;dose<=4;dose++)Assert.That(share[dose],Is.LessThan(share[dose-1]-.01f),"each dose a little tighter than the last: "+all);
                    Assert.That(share[1],Is.LessThan(.7f),"one dose still clearly sleepy: "+all);
                }
                for(int dose=1;dose<=4;dose++)Assert.That(shares["weed"][dose],Is.GreaterThan(shares["ketamine"][dose]),"Couch Lock, with no tunnel, is the lighter of the two at dose "+dose+when);
            }
        }
        // The share of a 16:9 screen the overlay leaves clear, read from its own textures where its rects lay them: clear where the black
        // layers together (the vignette, the lids and the bars past them) shade it less than half. The pale tint and haze do not hide it.
        private static float Clear(FestivalTrip trip)
        {
            const int width=160,height=90;
            var eye=Lids(trip);var lids=(Texture2D)eye.texture;var rect=eye.rectTransform;float low=rect.anchorMin.y,high=rect.anchorMax.y;
            var vignette=Part<RawImage>(trip,"Trip vignette");var ramp=(Texture2D)vignette.texture;
            int clear=0;
            for(int j=0;j<height;j++)for(int i=0;i<width;i++)
            {
                float u=(i+.5f)/width,v=(j+.5f)/height;
                float lid=!eye.enabled?0:v<=low||v>=high?1:lids.GetPixelBilinear(u,(v-low)/(high-low)).a;
                float tunnel=vignette.color.a*ramp.GetPixelBilinear(u,v).a;
                if(1-(1-lid)*(1-tunnel)<.5f)clear++;
            }
            return clear/(float)(width*height);
        }

        // TRIP-16, through the trip's own Apply on a controlled clock: the lids are one cached almond texture. A blink shuts them in
        // the middle and brings them back to the same resting opening one second after it began, smoothly; reduced motion moves them a
        // quarter of the way. A frame makes no garbage, resting or blinking.
        [Test]public void TheEyeBlinksShutAndBackInOneSecondFromOneCachedTextureWithoutGarbage()
        {
            var trip=Trip();var view=View();var pony=Round("Playing",4,"ketamine");int draws=0;trip.BlinkWaits=()=>{draws++;return 2;};
            float open=1-FestivalTrip.EyeRest("ketamine",4),time=0;bool reduced=false;
            void Frame(){time+=.02f;trip.Apply(pony,"me",view,NoActors,reduced,time,.02f);}
            Frame();
            var eye=Lids(trip);var texture=(Texture2D)eye.texture;
            var top=Part<Image>(trip,"Trip letterbox top").rectTransform;var bottom=Part<Image>(trip,"Trip letterbox bottom").rectTransform;
            Assert.That(texture,Is.Not.Null,"the lids are a texture");
            Assert.That(texture.width,Is.EqualTo(2*texture.height),"twice as wide as tall");
            Assert.That(texture.wrapMode==TextureWrapMode.Clamp&&texture.filterMode==FilterMode.Bilinear,"clamped and smooth");
            var pixels=texture.GetPixels();float worst=0;
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                worst=Mathf.Max(worst,Mathf.Abs(pixels[y*texture.width+x].a-FestivalTrip.EyeShade((x+.5f)*2/texture.width-1,(y+.5f)*2/texture.height-1,1)));
            Assert.That(worst,Is.LessThan(.003f),"pixel for pixel, the lids open their widest");
            Assert.That(Opening(trip),Is.EqualTo(open).Within(1e-5f),"setup: resting open");

            float started=-1,ended=-1,shut=-1,last=open,jump=0;
            for(int frame=0;frame<200&&ended<0;frame++)
            {
                Frame();float now=Opening(trip);jump=Mathf.Max(jump,Mathf.Abs(now-last));last=now;
                if(started<0&&now<open-1e-4f)started=time;
                if(now<1e-5f&&shut<0){shut=time;Assert.That(top.anchorMin.y==.5f&&bottom.anchorMax.y==.5f,"shut, the black meets in the middle");}
                if(started>=0&&now>=open-1e-5f)ended=time;
            }
            Assert.That(started,Is.EqualTo(2.04f).Within(.03f),"the first blink comes its drawn wait after the eye opened");
            Assert.That(shut,Is.GreaterThan(started),"and shuts the eyes");
            Assert.That(ended-started,Is.EqualTo(FestivalTrip.BlinkSeconds).Within(.03f),"back at the same resting opening a second after it began");
            Assert.That(jump,Is.LessThan(.06f),"with no pop on the way");
            // A frame on, past the blink's last float-sized sliver of closure.
            Frame();Assert.That(draws,Is.EqualTo(2),"one wait drawn as the eye opened and one as it reopened");

            reduced=true;float least=open;
            for(int frame=0;frame<200;frame++){Frame();least=Mathf.Min(least,Opening(trip));}
            Assert.That(least,Is.EqualTo(open*(1-FestivalTrip.ReducedMotionShare)).Within(1e-4f),"reduced motion: a blink moves the lids a quarter of the way, never shut");

            reduced=false;TestDelegate step=Frame;Frame();
            Assert.That(step,Is.Not.AllocatingGCMemory(),"a resting frame makes no garbage");
            for(int frame=0;frame<200&&Opening(trip)>=open-1e-4f;frame++)Frame();
            Assert.That(Opening(trip),Is.LessThan(open-1e-4f),"setup: blinking");
            Assert.That(step,Is.Not.AllocatingGCMemory(),"nor a blinking one");
            Assert.That(eye.texture,Is.SameAs(texture),"the lids' texture is built once and kept");
        }

        // TRIP-16: no blink closes over something timed or protected. Each moment here, begun as a blink closes, eases the lids open from
        // where they are and holds them open while it lasts; once it ends, the next blink waits a freshly drawn interval. A friend's
        // dance or stop holds nothing.
        [Test]public void TimedAndProtectedMomentsHoldTheEyesOpen()
        {
            void Busy(RoundState s,string kind,bool on){var me=s.Players[0];s.Interactions.Clear();me.InteractionId="";if(on){s.Interactions.Add(new InteractionState{Id="busy",PlayerId="me",Kind=kind});me.InteractionId="busy";}}
            void Cop(RoundState s,string mode,bool on){s.Npcs.Clear();if(on)s.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode=mode,TargetId="me"});}
            var moments=new (string Name,Action<RoundState,bool> Set)[]
            {
                ("a dance",(s,on)=>Busy(s,"Dance",on)),("a talk",(s,on)=>Busy(s,"Conversation",on)),
                ("a security stop",(s,on)=>Cop(s,"Stop",on)),("an arrest warning",(s,on)=>Cop(s,"ArrestWarning",on)),
                ("a minigame",(s,on)=>s.Phase=on?FestivalSimulation.MinigamePhase:"Playing"),("the level's results",(s,on)=>s.Phase=on?"Results":"Playing"),
            };
            foreach(var (name,set) in moments)foreach(var substance in new[]{"ketamine","weed"})
            {
                var trip=Trip();var view=View();var state=Round("Playing",3,substance);int draws=0;trip.BlinkWaits=()=>{draws++;return 1;};
                float open=1-FestivalTrip.EyeRest(substance,3),time=0;string at=name+", "+Name(substance);
                float Frame(){time+=.02f;trip.Apply(state,"me",view,NoActors,false,time,.02f);return Opening(trip);}
                for(int frame=0;frame<61;frame++)Frame();
                float previous=Opening(trip);
                Assert.That(previous,Is.LessThan(open-.1f).And.GreaterThan(0),"setup: "+at+" begins as a blink closes");
                set(state,true);
                for(int frame=0;frame<1500;frame++)
                {
                    float now=Frame();
                    Assert.That(now,Is.GreaterThanOrEqualTo(previous-1e-5f),at+": the lids only open");
                    if(frame>=25)Assert.That(now,Is.EqualTo(open).Within(1e-5f),at+": open within half a second, and no blink for the 30 s it lasts");
                    previous=now;
                }
                Assert.That(draws,Is.EqualTo(1),at+": no wait drawn while it lasts");
                set(state,false);
                float back=-1;for(int frame=0;frame<150&&back<0;frame++)if(Frame()<open-1e-4f)back=frame*.02f;
                Assert.That(draws,Is.EqualTo(2),at+": letting go draws a fresh wait");
                Assert.That(back,Is.EqualTo(1).Within(.05f),at+": and the next blink comes that wait after it ends");
            }
            var friendly=Round("Playing",3,"ketamine");friendly.Interactions.Add(new InteractionState{Id="theirs",PlayerId="friend",Kind="Dance"});friendly.Players[1].InteractionId="theirs";
            friendly.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="Stop",TargetId="friend"});
            var mine=Trip();mine.BlinkWaits=()=>1;var eyes=View();float shut=1;
            for(float t=.02f;t<2;t+=.02f){mine.Apply(friendly,"me",eyes,NoActors,false,t,.02f);shut=Mathf.Min(shut,Opening(mine));}
            Assert.That(shut,Is.Zero,"a friend's dance and a stop on a friend leave your blink alone");
        }

        // TRIP-16: a shut eye never outlives its dose. Mid-blink, going sober, a dose that ends, a spirit, camp or a new substance
        // throws the blink away: the eye comes back at its resting opening, and the next blink waits a whole freshly drawn interval.
        [Test]public void AShutEyeNeverOutlivesItsDose()
        {
            var ways=new (string Name,Action<RoundState> Break,Action<RoundState> Mend)[]
            {
                ("a dose worn to nothing",s=>s.Players[0].Effects[0].Intensity=0,s=>s.Players[0].Effects[0].Intensity=4),
                ("an ended dose",s=>s.Players[0].Effects.Clear(),s=>s.Players[0].Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance="ketamine"})),
                ("a spirit",s=>s.Players[0].Life="Spirit",s=>s.Players[0].Life="Alive"),
                ("camp",s=>s.Phase="Shopping",s=>s.Phase="Playing"),
                ("a new substance",s=>s.Players[0].Effects[0].Substance="weed",null),
            };
            foreach(var (name,brk,mend) in ways)
            {
                var trip=Trip();var view=View();var state=Round("Playing",4,"ketamine");int draws=0;trip.BlinkWaits=()=>{draws++;return 1;};
                float time=0;
                void Frame(){time+=.02f;trip.Apply(state,"me",view,NoActors,false,time,.02f);}
                // A new substance rests the eyes its own way.
                float Open()=>1-FestivalTrip.EyeRest(state.Players[0].Effects[0].Substance,4);
                for(int frame=0;frame<73;frame++)Frame();
                var eye=Lids(trip);
                Assert.That(Opening(trip),Is.Zero,"setup: shut mid-blink before "+name);
                brk(state);Frame();
                if(mend!=null)
                {
                    Assert.That(eye.enabled,Is.False,name+": the eye goes at once");
                    Assert.That(Part<Image>(trip,"Trip letterbox top").rectTransform.anchorMin.y,Is.EqualTo(1),name+": and its black with it");
                    mend(state);Frame();
                }
                Assert.That(eye.enabled,name+": back on a dose, the eye shows");
                Assert.That(Opening(trip),Is.EqualTo(Open()).Within(1e-5f),name+": at its resting opening, not shut");
                Assert.That(draws,Is.EqualTo(2),name+": with a freshly drawn wait");
                float back=-1;for(int frame=0;frame<100&&back<0;frame++){Frame();if(Opening(trip)<Open()-1e-4f)back=(frame+1)*.02f;}
                Assert.That(back,Is.EqualTo(1).Within(.05f),name+": so the next blink comes that whole wait later");
            }
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
