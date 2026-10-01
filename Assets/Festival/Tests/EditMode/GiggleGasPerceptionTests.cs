using System.Collections.Generic;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // GAS-1: a Giggle Balloon's gas on the hitter's own screen. Nothing while it kicks in, then a 1 s fade in, full strength and a
    // 3 s fade out, timed on the simulation clock. A wah-wah pulse about four times a second runs through the sound (a sweeping
    // low-pass and a tremolo) and the view (a pulsing vignette and a slight field-of-view throb); colours drain and the world
    // feels far off. Reduced motion drops the throb and halves the vignette's pulse; the sound stays.
    public sealed class GiggleGasPerceptionTests
    {
        // The hit kicks in at Kick, simulation time; Full is well into full strength.
        private const double Kick=100,Full=Kick+10;
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();AudioListener.volume=1;}

        private static string Describe(FestivalTrip.Perception p)=>
            $"tint {p.Tint} haze {p.Haze:F4} vignette {p.Vignette:F4} letterbox {p.Letterbox:F4} fov {p.Fov:F4} lag {p.Lag:F4} saturation {p.Saturation:F3} brightness {p.Brightness:F4} trails {p.Trails:F3} glow {p.Glow:F3} muffle {p.Muffle:F3} tremolo {p.Tremolo:F3}";
        private static readonly string Nothing=Describe(default);
        // The gas at simulation time now and local time t, for "me" at camp.
        private static FestivalTrip.Perception At(double now,float t=0,bool reduced=false)=>FestivalTrip.For(Camp(now),"me",reduced,t);
        // The amplitude of a setting over one local second.
        private static (float Low,float High) Range(System.Func<float,float> value){float low=float.MaxValue,high=float.MinValue;for(int i=0;i<=400;i++){float v=value(i/400f);low=Mathf.Min(low,v);high=Mathf.Max(high,v);}return (low,high);}

        [Test]public void NothingWhileItKicksInThenASecondInAndThreeSecondsOut()
        {
            foreach(double now in new[]{Kick-10,Kick-5,Kick-.01,Kick})Assert.That(Describe(At(now,.3f)),Is.EqualTo(Nothing),"nothing "+(now-Kick+10)+" s after the hit");
            float full=At(Full).Saturation;
            Assert.That(full,Is.LessThan(0),"setup: the gas drains colour at full strength");
            Assert.That(At(Kick+.5).Saturation,Is.EqualTo(full*.5f).Within(1e-3f),"half way through the 1 s fade in");
            Assert.That(At(Kick+1).Saturation,Is.EqualTo(full).Within(1e-3f),"full a second after it kicks in");
            Assert.That(At(Kick+27).Saturation,Is.EqualTo(full).Within(1e-3f),"full until the last 3 s");
            Assert.That(At(Kick+28.5).Saturation,Is.EqualTo(full*.5f).Within(1e-3f),"half way through the 3 s fade out");
            foreach(double now in new[]{Kick+30,Kick+35})Assert.That(Describe(At(now,.3f)),Is.EqualTo(Nothing),"nothing once it's over");
        }

        [Test]public void AWahWahPulsesFourTimesASecondInTheSoundAndTheView()
        {
            var vignette=new List<float>();var muffle=new List<float>();var tremolo=new List<float>();var fov=new List<float>();
            for(int i=0;i<200;i++){var look=At(Full,i/200f);vignette.Add(look.Vignette);muffle.Add(look.Muffle);tremolo.Add(look.Tremolo);fov.Add(look.Fov);}
            foreach(var (name,values) in new[]{("vignette",vignette),("low-pass",muffle),("tremolo",tremolo),("field of view",fov)})
            {
                int peaks=0;for(int i=1;i<values.Count-1;i++)if(values[i]>values[i-1]&&values[i]>=values[i+1])peaks++;
                Assert.That(peaks,Is.EqualTo(4),"the "+name+" pulses four times a second");
            }
            Assert.That(Range(t=>At(Full,t).Tremolo).High,Is.InRange(.2f,.6f),"the tremolo dips the sound clearly, never to silence");
            var throb=Range(t=>At(Full,t).Fov);
            Assert.That(throb.High,Is.InRange(.5f,2f),"a slight field-of-view throb, under 2 degrees");
            Assert.That(FestivalTrip.LowPassHz(Range(t=>At(Full,t).Muffle).High),Is.InRange(1500f,4000f),"the low-pass closes in on each pulse");
        }

        [Test]public void ColoursDrainAndTheWorldFeelsFarAway()
        {
            var look=At(Full,.1f);
            Assert.That(look.Saturation,Is.LessThan(-30),"colours drain: "+Describe(look));
            Assert.That(look.Haze,Is.GreaterThan(0),"a pale haze puts the world at a distance");
            Assert.That(look.Muffle,Is.GreaterThan(0),"and the sound is muffled");
            Assert.That(look.Tint.a,Is.Zero,"no colour cast: the gas drains colour, it adds none");
        }

        [Test]public void ReducedMotionDropsTheThrobAndHalvesTheVignettePulseButKeepsTheSound()
        {
            var full=Range(t=>At(Full,t).Vignette);var calm=Range(t=>At(Full,t,true).Vignette);
            Assert.That(full.High-full.Low,Is.GreaterThan(.1f),"setup: the vignette pulses");
            Assert.That(calm.High-calm.Low,Is.EqualTo((full.High-full.Low)*.5f).Within(1e-3f),"reduced motion halves the vignette's pulse");
            Assert.That(calm.Low,Is.EqualTo(full.Low).Within(1e-3f),"but keeps the vignette");
            Assert.That(Range(t=>Mathf.Abs(At(Full,t,true).Fov)).High,Is.Zero,"reduced motion drops the field-of-view throb");
            for(int i=0;i<20;i++)
            {
                float t=i/20f;var a=At(Full,t);var b=At(Full,t,true);
                Assert.That(b.Muffle,Is.EqualTo(a.Muffle),"the low-pass stays at "+t+" s");
                Assert.That(b.Tremolo,Is.EqualTo(a.Tremolo),"the tremolo stays at "+t+" s");
                Assert.That(b.Saturation,Is.EqualTo(a.Saturation),"the colours drain the same");
                Assert.That(b.Haze,Is.EqualTo(a.Haze),"the haze stays");
            }
        }

        [Test]public void OnlyTheHittersOwnScreen()
        {
            var state=Camp(Full);var friend=new PlayerState{Id="friend"};friend.Effects.Add(Gas());state.Players.Add(friend);
            state.Players[0].Effects.Clear();
            Assert.That(Describe(FestivalTrip.For(state,"me",false,.1f)),Is.EqualTo(Nothing),"a friend's gas never reaches your screen");
            Assert.That(Describe(FestivalTrip.For(state,"friend",false,.1f)),Is.Not.EqualTo(Nothing),"it reaches theirs");
            Assert.That(FestivalHud.EffectWash(new List<ActiveEffect>{Gas()},false,0).a,Is.Zero,"the HUD's wash stays clear: the gas has its own look");
        }

        // The single writers: the grade's saturation goes through FestivalNightLighting, and camp gets it back when the gas ends.
        [Test]public void TheColourGradeDrainsThroughTheOneGradeWriter()
        {
            var root=new GameObject("Gas world");made.Add(root);var world=root.AddComponent<FestivalWorld>();world.Build();
            Assert.That(root.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"world colour grade");
            float saturation=grade.saturation.value;
            world.SetLighting(Camp(Full),"me");
            Assert.That(grade.saturation.value,Is.LessThan(saturation-30),"the gas drains the camp's colours");
            Assert.That(grade.saturation.value,Is.EqualTo(saturation+At(Full).Saturation).Within(1e-3f),"by the gas's own drain, through the one grade");
            world.SetLighting(Camp(Kick+31),"me");
            Assert.That(grade.saturation.value,Is.EqualTo(saturation).Within(1e-5f),"and they come back when it ends");
        }

        // The overlay, the low-pass and the tremolo follow the gas, and let go when it ends and when the client leaves (no state).
        // Turning FestivalTrip off or destroying it lets go too (GiggleGasPlayTests: EditMode sends no OnDisable or OnDestroy).
        [Test]public void TheOverlayAndTheSoundFollowTheGasAndLetGo()
        {
            var trip=new GameObject("Gas trip");made.Add(trip);var component=trip.AddComponent<FestivalTrip>();
            var view=new GameObject("Gas view",typeof(AudioListener));made.Add(view);
            // A local time at the top of a pulse, where the tremolo dips furthest.
            float peak=0;for(int i=0;i<100;i++)if(At(Full,i/100f).Tremolo>At(Full,peak).Tremolo)peak=i/100f;
            void Expect(string when)
            {
                var look=At(Full,peak);var lowPass=view.GetComponent<AudioLowPassFilter>();
                Assert.That(component.GetComponentInChildren<Canvas>(true).gameObject.activeSelf,Is.True,when+": the overlay is up");
                Assert.That(Part<RawImage>(component,"Trip vignette").color.a,Is.EqualTo(look.Vignette).Within(1e-5f),when+": the vignette pulses");
                Assert.That(Part<Image>(component,"Trip haze").color.a,Is.EqualTo(look.Haze).Within(1e-5f),when+": the haze");
                Assert.That(lowPass!=null&&lowPass.enabled,when+": a low-pass on the listener");
                Assert.That(lowPass.cutoffFrequency,Is.EqualTo(FestivalTrip.LowPassHz(look.Muffle)).Within(1),when+": at the pulse's cutoff");
                Assert.That(AudioListener.volume,Is.EqualTo(1-look.Tremolo).Within(1e-5f),when+": the tremolo dips everything heard");
                Assert.That(AudioListener.volume,Is.LessThan(.9f),when+": setup: at the top of a pulse");
            }
            void LetGo(string when)
            {
                var lowPass=view.GetComponent<AudioLowPassFilter>();
                Assert.That(component.GetComponentInChildren<Canvas>(true).gameObject.activeSelf,Is.False,when+": the overlay is down");
                Assert.That(lowPass==null||!lowPass.enabled,when+": the low-pass is off");
                Assert.That(AudioListener.volume,Is.EqualTo(1),when+": the sound is back to full");
            }
            component.Apply(Camp(Full),"me",view.transform,null,false,peak,.02f);Expect("on the gas");
            component.Apply(Camp(Kick+31),"me",view.transform,null,false,peak,.02f);LetGo("once it has worn off");
            component.Apply(Camp(Full),"me",view.transform,null,false,peak,.02f);Expect("on another");
            component.Apply(null,"me",view.transform,null,false,peak,.02f);LetGo("after leaving the session");
        }

        // The station stands where the rules have it, at camp only: a Giggle Tank with balloons floating over head height, so the crew
        // finds it from across the mat. Nothing in it collides, and nothing solid stands within its reach, so a hit is taken from any side.
        [Test]public void TheBalloonsStandAtCampWithRoomAllRound()
        {
            var root=new GameObject("Balloon world");made.Add(root);var world=root.AddComponent<FestivalWorld>();world.Build();Physics.SyncTransforms();
            var camp=root.transform.Find(FestivalWorld.CampRootName);var station=camp.Find(FestivalGiggleTank.BalloonsName);
            Assert.That(station,Is.Not.Null,"the Giggle Balloons stand at camp");
            Assert.That(Vector2.Distance(new Vector2(station.position.x,station.position.z),new Vector2(CampFeatures.GiggleBalloonX,CampFeatures.GiggleBalloonZ)),Is.LessThan(1e-3f),"where the rules take a hit");
            Assert.That(station.position.y,Is.EqualTo(0).Within(1e-3f),"on the ground");
            Assert.That(station.Find(FestivalGiggleTank.Name),Is.Not.Null,"a Giggle Tank, like the festival's");
            var balloons=new List<Renderer>();foreach(var r in station.GetComponentsInChildren<Renderer>(true))if(r.name=="Balloon")balloons.Add(r);
            Assert.That(balloons.Count,Is.GreaterThanOrEqualTo(3),"a bunch of balloons");
            foreach(var balloon in balloons)Assert.That(balloon.bounds.min.y,Is.GreaterThan(1.65f),"each floats above eye height");
            foreach(var r in station.GetComponentsInChildren<Renderer>(true))Assert.That(r.sharedMaterial,Is.Not.Null,r.name+" has a material");
            Assert.That(station.GetComponentsInChildren<Collider>(true),Is.Empty,"nothing in it collides");
            var at=new Vector3(CampFeatures.GiggleBalloonX,1,CampFeatures.GiggleBalloonZ);int solid=0;
            foreach(var wall in camp.GetComponentsInChildren<Collider>())
            {
                if(!wall.enabled||wall.bounds.max.y<.1f)continue;solid++;
                Assert.That(Vector3.Distance(wall.bounds.ClosestPoint(at),at),Is.GreaterThan(CampFeatures.GiggleBalloonReach),"the balloons' reach is hemmed in by "+wall.name);
            }
            Assert.That(solid,Is.GreaterThan(10),"setup: camp's tents, cars and boundaries collide");
            world.SetPhase("Playing");
            Assert.That(station.gameObject.activeInHierarchy,Is.False,"the festival hides them with the rest of camp");
        }

        [Test]public void BesideTheBalloonsEOffersAHitOneAtATime()
        {
            var me=new PlayerState{Id="me",X=CampFeatures.GiggleBalloonX,Z=CampFeatures.GiggleBalloonZ+2.4f};
            Assert.That(FestivalHudText.GiggleBalloonPrompt(me),Is.EqualTo("E  TAKE A GIGGLE BALLOON"),"within reach, E takes one");
            me.Effects.Add(Gas());
            Assert.That(FestivalHudText.GiggleBalloonPrompt(me),Is.EqualTo("ONE GIGGLE BALLOON AT A TIME"),"while one is on, E does nothing: one at a time");
            me.Effects.Clear();me.Z=CampFeatures.GiggleBalloonZ+2.6f;
            Assert.That(FestivalHudText.GiggleBalloonPrompt(me),Is.Empty,"out of reach, no prompt");
        }

        // A Giggle Balloon's gas: it kicks in at Kick.
        private static ActiveEffect Gas()=>new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=Kick,RemainingSeconds=FestivalSimulation.GiggleGasSeconds};
        // Camp at simulation time now, with "me" on the gas.
        private static RoundState Camp(double now)
        {
            var state=new RoundState{Phase="Shopping",SimulationSeconds=now};
            var me=new PlayerState{Id="me"};me.Effects.Add(Gas());state.Players.Add(me);
            return state;
        }
        private static T Part<T>(FestivalTrip trip,string name) where T:Component{foreach(var c in trip.GetComponentsInChildren<T>(true))if(c.name==name)return c;return null;}
    }
}
