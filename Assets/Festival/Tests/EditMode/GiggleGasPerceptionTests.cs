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
    // 3 s fade out, timed on the simulation clock. GAS-5: a wah-wah pulse on every beat of the camp music (on a 120 BPM clock with
    // the music off), sharp on the kick, runs through the sound (a sweeping low-pass and a tremolo) and the view (a deep vignette
    // closing in and a strong field-of-view kick); colours drain, and a big reverb and a pale haze put the world far off. Reduced
    // motion drops the kick and halves the vignette's pulse; the sound stays.
    public sealed class GiggleGasPerceptionTests
    {
        // The hit kicks in at Kick, simulation time; Full is well into full strength.
        private const double Kick=100,Full=Kick+10;
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();AudioListener.volume=1;}

        private static string Describe(FestivalTrip.Perception p)=>
            $"tint {p.Tint} haze {p.Haze:F4} vignette {p.Vignette:F4} letterbox {p.Letterbox:F4} fov {p.Fov:F4} lag {p.Lag:F4} saturation {p.Saturation:F3} brightness {p.Brightness:F4} trails {p.Trails:F3} glow {p.Glow:F3} muffle {p.Muffle:F3} tremolo {p.Tremolo:F3} reverb {p.Reverb:F3}";
        private static readonly string Nothing=Describe(default);
        // The gas at simulation time now and local time t, for "me" at camp, on the music's beat (none: the music is off).
        private static FestivalTrip.Perception At(double now,float t=0,bool reduced=false,float? beat=null)=>FestivalTrip.For(Camp(now),"me",reduced,t,beat);
        // The amplitude of a setting over one local second.
        private static (float Low,float High) Range(System.Func<float,float> value){float low=float.MaxValue,high=float.MinValue;for(int i=0;i<=400;i++){float v=value(i/400f);low=Mathf.Min(low,v);high=Mathf.Max(high,v);}return (low,high);}
        // The frames of a second's 200 where a setting peaks: higher than the frame before, and no lower than the next.
        private static List<int> Peaks(System.Func<int,float> value){var peaks=new List<int>();for(int i=0;i<200;i++)if(value(i)>value(i-1)&&value(i)>=value(i+1))peaks.Add(i);return peaks;}

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

        // GAS-5: one pulse a beat. Each setting peaks on the frame each kick falls in, wherever the music's beat is (here a drum and
        // bass track's 174 BPM, its first kick .302 s in, between two frames), and with the music off on a 120 BPM clock: on the
        // half seconds.
        [Test]public void AWahWahPulsesOnEveryBeatInTheSoundAndTheView()
        {
            float Music(int frame)=>Mathf.Repeat((frame/200f-.302f)*174.37f/60,1);
            var kicks=new List<int>();for(int i=0;i<200;i++)if(Music(i)<Music(i-1))kicks.Add(i);
            Assert.That(kicks,Is.EqualTo(new[]{61,130,199}),"setup: three kicks in the second");
            foreach(var (name,value) in new (string,System.Func<FestivalTrip.Perception,float>)[]{("vignette",p=>p.Vignette),("low-pass",p=>p.Muffle),("tremolo",p=>p.Tremolo),("field of view",p=>p.Fov)})
            {
                Assert.That(Peaks(i=>value(At(Full,i/200f,false,Music(i)))),Is.EqualTo(kicks),"the "+name+" peaks on each of the music's kicks");
                Assert.That(Peaks(i=>value(At(Full,i/200f))),Is.EqualTo(new[]{0,100}),"with the music off, the "+name+" pulses on a 120 BPM clock");
            }
            Assert.That(Range(t=>At(Full,t).Tremolo).High,Is.InRange(.2f,.6f),"the tremolo dips the sound clearly, never to silence");
            Assert.That(Range(t=>At(Full,t).Fov).High,Is.InRange(3.5f,4.5f),"a strong field-of-view kick, about 4 degrees");
            Assert.That(FestivalTrip.LowPassHz(Range(t=>At(Full,t).Muffle).High),Is.InRange(1500f,4000f),"the low-pass closes in on each pulse");
        }

        // GAS-5: on the kick the vignette closes in deep and the view kicks wide; both ease off through the beat, so each peaks once.
        [Test]public void OnTheKickTheViewClosesInAndKicksWideThenEasesOff()
        {
            var kick=FestivalTrip.Gas(1,false,0);var half=FestivalTrip.Gas(1,false,.5f);
            Assert.That(kick.Vignette,Is.GreaterThanOrEqualTo(.8f),"on the kick the vignette closes in deep: "+Describe(kick));
            Assert.That(kick.Fov,Is.GreaterThanOrEqualTo(3.5f),"and the view kicks wide");
            Assert.That(half.Vignette,Is.LessThanOrEqualTo(.55f),"half a beat on, the vignette has opened: "+Describe(half));
            Assert.That(half.Fov,Is.LessThanOrEqualTo(1f),"and the view is nearly back");
            var before=kick;
            for(int i=1;i<100;i++)
            {
                var look=FestivalTrip.Gas(1,false,i/100f);
                Assert.That(look.Vignette<before.Vignette&&look.Fov<before.Fov,"they only ease off through the beat, so they peak once, on the kick ("+i+"% of the beat)");
                before=look;
            }
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
                Assert.That(b.Reverb,Is.EqualTo(a.Reverb),"the reverb stays");
            }
        }

        // GAS-5: a big reverb as strong as the gas: in with the fade in, full at full strength, gone once it ends. Only the gas reverbs.
        [Test]public void TheGasAddsABigReverbAsStrongAsItIs()
        {
            Assert.That(At(Full).Reverb,Is.EqualTo(1),"full at full strength");
            Assert.That(At(Kick+.5).Reverb,Is.EqualTo(.5f).Within(1e-3f),"half way through the fade in");
            Assert.That(At(Kick+28.5).Reverb,Is.EqualTo(.5f).Within(1e-3f),"half way through the fade out");
            Assert.That(At(Kick+31).Reverb,Is.Zero,"none once it's over");
            foreach(var substance in new[]{"lsd","mushrooms","ecstasy","ketamine","weed"})Assert.That(FestivalTrip.Look(substance,4,false,.3f).Reverb,Is.Zero,"only the gas reverbs, not "+substance);
            // The wet mix in millibels: off at 0, louder as the gas grows, and under the dry sound even at full, so the beat stays clear.
            Assert.That(FestivalTrip.ReverbRoom(0),Is.LessThanOrEqualTo(-10000),"no reverb, nothing wet");
            Assert.That(FestivalTrip.ReverbRoom(.25f),Is.LessThan(FestivalTrip.ReverbRoom(.5f)),"wetter as it grows");
            Assert.That(FestivalTrip.ReverbRoom(.5f),Is.LessThan(FestivalTrip.ReverbRoom(1)),"wettest at full");
            Assert.That(FestivalTrip.ReverbRoom(1),Is.InRange(-1500f,-300f),"a big hall at full, still under the dry sound");
        }

        [Test]public void OnlyTheHittersOwnScreen()
        {
            var state=Camp(Full);var friend=new PlayerState{Id="friend"};friend.Effects.Add(Gas());state.Players.Add(friend);
            state.Players[0].Effects.Clear();
            Assert.That(Describe(FestivalTrip.For(state,"me",false,.1f)),Is.EqualTo(Nothing),"a friend's gas never reaches your screen");
            Assert.That(Describe(FestivalTrip.For(state,"friend",false,.1f)),Is.Not.EqualTo(Nothing),"it reaches theirs");
            Assert.That(FestivalHud.EffectWash(new List<ActiveEffect>{Gas()},false,0).a,Is.Zero,"the HUD's wash stays clear: the gas has its own look");
        }

        // GAS-4: two hits on at once add up, capped at two. Strength 1 is GAS-5's profile; from 1 to 2 the kick ramps 4 to 8 degrees,
        // the drain 55 to 95 and the tremolo .4 to .7, while the vignette, haze, low-pass, reverb and the pulse's shape stay a hit's.
        [Test]public void BackToBackHitsBuildToTheStrongerProfile()
        {
            float Peak(System.Func<FestivalTrip.Perception,float> value,RoundState state,bool reduced=false)=>Range(t=>value(FestivalTrip.For(state,"me",reduced,t))).High;
            var one=Camp(Full);var two=Camp(Full);two.Players[0].Effects.Add(Gas());
            Assert.That(Peak(p=>p.Fov,one),Is.InRange(3.5f,4.5f),"setup: one hit kicks about 4 degrees");
            Assert.That(Peak(p=>p.Tremolo,one),Is.EqualTo(.4f).Within(.02f),"setup: one hit's tremolo dips .4");
            Assert.That(FestivalTrip.For(one,"me",false,.1f).Saturation,Is.EqualTo(-55).Within(1e-3f),"setup: one hit drains 55");

            Assert.That(FestivalTrip.For(two,"me",false,.1f).Saturation,Is.EqualTo(-95).Within(1e-3f),"two hits at full drain 95: "+Describe(FestivalTrip.For(two,"me",false,.1f)));
            Assert.That(Peak(p=>p.Fov,two),Is.InRange(7.5f,8.5f),"the kick doubles, about 8 degrees");
            float tremolo=Peak(p=>p.Tremolo,two);
            Assert.That(tremolo,Is.AtLeast(.6f),"the tremolo dips deeper");
            Assert.That(tremolo,Is.LessThan(1),"never to silence");
            for(int i=0;i<20;i++)
            {
                float t=i/20f;var a=FestivalTrip.For(one,"me",false,t);var b=FestivalTrip.For(two,"me",false,t);
                Assert.That(b.Vignette,Is.EqualTo(a.Vignette).Within(1e-5f),"the vignette stays a hit's at "+t+" s");
                Assert.That(b.Haze,Is.EqualTo(a.Haze).Within(1e-5f),"the haze stays a hit's");
                Assert.That(b.Muffle,Is.EqualTo(a.Muffle).Within(1e-5f),"the low-pass stays a hit's");
                Assert.That(b.Reverb,Is.EqualTo(a.Reverb).Within(1e-5f),"the reverb stays a hit's");
                Assert.That(b.Fov,Is.EqualTo(a.Fov*2).Within(1e-4f),"the kick is a hit's pulse, twice as deep, at "+t+" s");
                Assert.That(b.Tremolo,Is.EqualTo(a.Tremolo*.7f/.4f).Within(1e-4f),"the tremolo is a hit's pulse, .7 deep, at "+t+" s");
            }

            // The second hit half way through its fade in: 1.5 in all, half way from the gentle profile to the stronger one.
            var half=Camp(Full);half.Players[0].Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=Full-.5,RemainingSeconds=FestivalSimulation.GiggleGasSeconds});
            Assert.That(FestivalTrip.For(half,"me",false,.1f).Saturation,Is.EqualTo(-75).Within(1e-3f),"at 1.5 the drain is half way, 75");
            Assert.That(Peak(p=>p.Fov,half),Is.EqualTo(Peak(p=>p.Fov,one)*1.5f).Within(.02f),"the kick half way, about 6 degrees");
            Assert.That(Peak(p=>p.Tremolo,half),Is.EqualTo(.55f).Within(.02f),"the tremolo half way, .55");

            Assert.That(Peak(p=>Mathf.Abs(p.Fov),two,true),Is.Zero,"reduced motion still has no throb");
            var calm=Range(t=>FestivalTrip.For(two,"me",true,t).Vignette);var single=Range(t=>FestivalTrip.For(one,"me",true,t).Vignette);
            Assert.That(calm.High-calm.Low,Is.EqualTo(single.High-single.Low).Within(1e-3f),"and halves the vignette's pulse as for one hit");
        }

        // The single writers: the grade's saturation goes through FestivalNightLighting, and camp gets it back when the gas ends.
        // GAS-4: two hits drain camp's -5 grade to -100, full grey, and it comes back as each wears off.
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

            // A second hit 5 s after the first: both on, then the first wears off, then the second.
            RoundState Two(double now){var state=Camp(now);state.Players[0].Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=Kick+5,RemainingSeconds=FestivalSimulation.GiggleGasSeconds});return state;}
            world.SetLighting(Two(Full),"me");
            Assert.That(grade.saturation.value,Is.EqualTo(-100).Within(1e-3f),"two hits drain camp's grade to full grey");
            world.SetLighting(Two(Kick+31),"me");
            Assert.That(grade.saturation.value,Is.EqualTo(saturation+At(Full).Saturation).Within(1e-3f),"one hit's drain once the first wears off");
            world.SetLighting(Two(Kick+36),"me");
            Assert.That(grade.saturation.value,Is.EqualTo(saturation).Within(1e-5f),"camp's colours once both have");
        }

        // The overlay, the low-pass, the reverb and the tremolo follow the gas, and let go when it ends and when the client leaves (no state).
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
                var reverb=view.GetComponent<AudioReverbFilter>();
                Assert.That(reverb!=null&&reverb.enabled,when+": a reverb on the listener");
                Assert.That(reverb.room,Is.EqualTo(FestivalTrip.ReverbRoom(look.Reverb)).Within(1),when+": as wet as the gas is strong");
                Assert.That(reverb.decayTime,Is.InRange(2f,3f),when+": a big hall's tail, about 2.5 s");
                Assert.That(AudioListener.volume,Is.EqualTo(1-look.Tremolo).Within(1e-5f),when+": the tremolo dips everything heard");
                Assert.That(AudioListener.volume,Is.LessThan(.9f),when+": setup: at the top of a pulse");
            }
            void LetGo(string when)
            {
                var lowPass=view.GetComponent<AudioLowPassFilter>();
                Assert.That(component.GetComponentInChildren<Canvas>(true).gameObject.activeSelf,Is.False,when+": the overlay is down");
                Assert.That(lowPass==null||!lowPass.enabled,when+": the low-pass is off");
                var reverb=view.GetComponent<AudioReverbFilter>();
                Assert.That(reverb==null||!reverb.enabled,when+": the reverb is off");
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
            var balloons=new List<Renderer>();foreach(var r in station.GetComponentsInChildren<Renderer>(true))if(r.name.StartsWith("GiggleBalloon",System.StringComparison.Ordinal)&&r.name.EndsWith("__Rose",System.StringComparison.Ordinal))balloons.Add(r);
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

        // GAS-4: E takes a hit with none or one on, kicking in or felt; with two on there is nothing to press.
        [Test]public void BesideTheBalloonsEOffersUpToTwoHits()
        {
            var me=new PlayerState{Id="me",X=CampFeatures.GiggleBalloonX,Z=CampFeatures.GiggleBalloonZ+2.4f};
            Assert.That(FestivalHudText.GiggleBalloonPrompt(me),Is.EqualTo("E  TAKE A GIGGLE BALLOON"),"within reach, E takes one");
            me.Effects.Add(Gas());
            Assert.That(FestivalHudText.GiggleBalloonPrompt(me),Is.EqualTo("E  TAKE A GIGGLE BALLOON"),"with one on, E takes another");
            me.Effects.Add(Gas());
            Assert.That(FestivalHudText.GiggleBalloonPrompt(me),Is.EqualTo("TWO GIGGLE BALLOONS AT A TIME"),"with two on, E does nothing: two at a time");
            me.Z=CampFeatures.GiggleBalloonZ+2.6f;
            Assert.That(FestivalHudText.GiggleBalloonPrompt(me),Is.Empty,"out of reach with two on, no prompt");
            me.Effects.Clear();
            Assert.That(FestivalHudText.GiggleBalloonPrompt(me),Is.Empty,"out of reach, no prompt");
        }

        // Friends see the hitter giggle: ViewFor sends a giggling flag, like the wide-eyes flag, only while the gas has strength (not
        // while it kicks in). The gas is no intoxication to look at: no "Intoxicated" pose, no wide eyes, and still no effect list.
        [Test]public void FriendsSeeTheHitterGiggleOnlyWhileTheGasIsOn()
        {
            var game=new FestivalSimulation(3);var hitter=game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");
            hitter.X=CampFeatures.GiggleBalloonX;hitter.Z=CampFeatures.GiggleBalloonZ;
            Assert.That(game.Execute("p0",new GameCommand{Id="hit",Kind=FestivalSimulation.TakeGiggleBalloonKind}).Accepted,Is.True,"setup: a hit");
            PlayerState Seen(string viewer)=>Festival.Network.FestivalSession.ViewFor(game,viewer).Players.Find(p=>p.Id=="p0");
            game.Tick(9.9);
            Assert.That(Seen("p1").VisualGiggling,Is.False,"no giggle while it kicks in");
            game.Tick(.4);
            Assert.That(Seen("p1").VisualGiggling,Is.True,"a friend sees the giggle once it has");
            Assert.That(Seen("p0").VisualGiggling,Is.True,"so does the hitter's own view");
            var seen=Seen("p1");
            Assert.That(seen.Effects,Is.Empty,"a friend still sees no effect list");
            Assert.That(seen.VisualPose,Is.EqualTo("Idle"),"the gas is no Intoxicated pose");
            Assert.That(seen.VisualWideEyes||seen.VisualRedEyes,Is.False,"nor wide or red eyes");
            game.Tick(29.8);
            Assert.That(game.State.Players[0].Effects,Is.Empty,"setup: the host has ended it");
            Assert.That(Seen("p1").VisualGiggling,Is.False,"no giggle once it's over");
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
