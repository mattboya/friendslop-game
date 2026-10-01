using System.Collections;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // GAS-1 in a running session: at camp, beside the Giggle Balloons, the prompt offers one and E takes it. Once it kicks in, the
    // gas reaches the hitter's screen through the single writers: FestivalHud adds the throb to the field of view, FestivalNightLighting
    // drains the colour grade, and FestivalTrip pulses its overlay, closes a low-pass and dips the volume. All of it lets go when the
    // gas ends, when the player leaves the session, and when FestivalTrip is turned off or destroyed. Friends see the hitter giggle.
    public sealed class GiggleGasPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator EAtTheBalloonsTakesAHitThatPulsesThroughTheSingleWriters()
        {
            var world=new GameObject("Gas world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Gas session");var session=host.AddComponent<FestivalSession>();var hud=host.AddComponent<FestivalHud>();
            yield return null;
            bool reduced=session.Profile.Data.ReducedMotion;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);var me=session.LocalPlayerId;var view=session.ViewCamera;
                Assert.That(world.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"setup: the world's colour grade");
                float camp=grade.saturation.value;
                Text Find(string name){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
                var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
                FestivalTrip.Perception Now()=>FestivalTrip.For(session.State,me,session.Profile.Data.ReducedMotion,Time.unscaledTime);
                var overlay=host.transform.Find("Festival trip");
                void Sober(string when)
                {
                    var lowPass=view.GetComponent<AudioLowPassFilter>();
                    Assert.That(overlay==null||!overlay.gameObject.activeSelf,when+": no overlay");
                    Assert.That(lowPass==null||!lowPass.enabled,when+": no low-pass");
                    Assert.That(AudioListener.volume,Is.EqualTo(1),when+": the sound at full");
                }

                // Beside the balloons, E takes one.
                player.X=CampFeatures.GiggleBalloonX+1.2f;player.Z=CampFeatures.GiggleBalloonZ;
                yield return new WaitForSeconds(.5f);
                Assert.That(Find("Prompt")?.text,Is.EqualTo("TAKE A GIGGLE BALLOON"),"beside the balloons the prompt offers one, on E");
                ((System.Action)primary.GetValue(hud))();
                var gas=player.Effects.Find(e=>e.Id==FestivalSimulation.GiggleGasEffect);
                Assert.That(gas,Is.Not.Null,"E takes a hit: "+session.Message);
                Assert.That(session.Message,Is.EqualTo("Giggle Balloon: give it a few seconds…"),"and the host says so");
                yield return new WaitForSeconds(.5f);
                Assert.That(Find("Prompt")?.text,Is.EqualTo("ONE GIGGLE BALLOON AT A TIME"),"one at a time");
                Assert.That(primary.GetValue(hud),Is.Null,"so E does nothing");
                overlay=host.transform.Find("Festival trip");
                Sober("while it kicks in");
                Assert.That(view.fieldOfView,Is.EqualTo(75).Within(1e-3f),"while it kicks in: the view holds still");
                Assert.That(grade.saturation.value,Is.EqualTo(camp).Within(1e-3f),"while it kicks in: camp's colours");
                Assert.That(session.WorldCharacter(me).Giggling,Is.False,"while it kicks in: no giggle");

                // Kicked in (the wait skipped), at full motion: each writer moves its own property with the pulse, every frame. A test
                // resumes after the frame's Updates (the HUD's throb, the session's grade) and before its LateUpdates, so the overlay and
                // the sound FestivalTrip drew are last frame's moment.
                gas.StartSeconds=sim.State.SimulationSeconds-5;session.Profile.Data.ReducedMotion=false;
                yield return null;yield return null;
                float widest=0,quietest=1,drawnAt=Time.unscaledTime;
                for(deadline=Time.realtimeSinceStartup+1.5f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    var look=Now();var drawn=FestivalTrip.For(session.State,me,false,drawnAt);drawnAt=Time.unscaledTime;var lowPass=view.GetComponent<AudioLowPassFilter>();
                    Assert.That(view.fieldOfView,Is.EqualTo(75+look.Fov).Within(.01f),"the HUD adds the gas's throb to the 75 degree view");
                    Assert.That(grade.saturation.value,Is.EqualTo(camp+look.Saturation).Within(1e-3f),"the one grade drains camp's colours");
                    overlay=host.transform.Find("Festival trip");
                    Assert.That(overlay!=null&&overlay.gameObject.activeSelf,"the overlay is up");
                    Assert.That(overlay.Find("Trip vignette").GetComponent<RawImage>().color.a,Is.EqualTo(drawn.Vignette).Within(1e-4f),"its vignette pulses");
                    Assert.That(lowPass!=null&&lowPass.enabled,"the first-person listener is low-passed");
                    Assert.That(lowPass.cutoffFrequency,Is.EqualTo(FestivalTrip.LowPassHz(drawn.Muffle)).Within(1),"on the pulse");
                    Assert.That(AudioListener.volume,Is.EqualTo(1-drawn.Tremolo).Within(1e-4f),"and the tremolo dips it");
                    widest=Mathf.Max(widest,Mathf.Abs(view.fieldOfView-75));quietest=Mathf.Min(quietest,AudioListener.volume);
                }
                Assert.That(grade.saturation.value,Is.LessThan(camp-30),"the colours drain");
                Assert.That(session.WorldCharacter(me).Giggling,Is.True,"the hitter's body giggles, as friends see it");
                Assert.That(widest,Is.GreaterThan(.5f),"the view throbs");
                Assert.That(quietest,Is.LessThan(.8f),"the sound goes wah-wah");

                // Reduced motion: the view holds still; the sound still pulses.
                session.Profile.Data.ReducedMotion=true;quietest=1;
                yield return null;
                for(deadline=Time.realtimeSinceStartup+.6f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    Assert.That(view.fieldOfView,Is.EqualTo(75).Within(1e-3f),"reduced motion: no throb");
                    quietest=Mathf.Min(quietest,AudioListener.volume);
                }
                Assert.That(quietest,Is.LessThan(.8f),"reduced motion: the wah-wah stays");

                // The host ends it: every writer lets go, and the balloons offer another.
                gas.RemainingSeconds=.01;
                yield return new WaitForSeconds(.5f);
                Assert.That(player.Effects.Exists(e=>e.Id==FestivalSimulation.GiggleGasEffect),Is.False,"setup: the host removed it");
                Sober("once it has worn off");
                Assert.That(view.fieldOfView,Is.EqualTo(75).Within(1e-3f),"once it has worn off: the view");
                Assert.That(grade.saturation.value,Is.EqualTo(camp).Within(1e-3f),"once it has worn off: camp's colours");
                Assert.That(session.WorldCharacter(me).Giggling,Is.False,"once it has worn off: the giggling stops");
                Assert.That(Find("Prompt")?.text,Is.EqualTo("TAKE A GIGGLE BALLOON"),"and E takes another");

                // Another, kicked in at once, then the player leaves the session: the sound comes back for the menu.
                ((System.Action)primary.GetValue(hud))();
                gas=player.Effects.Find(e=>e.Id==FestivalSimulation.GiggleGasEffect);Assert.That(gas,Is.Not.Null,"setup: a second hit");
                gas.StartSeconds=sim.State.SimulationSeconds-5;
                for(deadline=Time.realtimeSinceStartup+1;Time.realtimeSinceStartup<deadline&&AudioListener.volume>.8f;)yield return null;
                Assert.That(AudioListener.volume,Is.LessThan(.8f),"setup: the second hit is dipping the sound");
                session.Leave();
                yield return null;
                Sober("after leaving the session");

                // FestivalTrip turned off, or destroyed, with the gas on: the global volume never stays dipped.
                var trip=host.GetComponent<FestivalTrip>();
                var on=new RoundState{Phase="Shopping",SimulationSeconds=100};var hitter=new PlayerState{Id="me"};
                hitter.Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=95,RemainingSeconds=25});on.Players.Add(hitter);
                trip.Apply(on,"me",view.transform,null,false,.0625f,.02f);
                Assert.That(AudioListener.volume,Is.LessThan(.8f),"setup: the gas at the top of a pulse");
                trip.enabled=false;
                Sober("with the trip layer turned off");
                trip.enabled=true;trip.Apply(on,"me",view.transform,null,false,.0625f,.02f);
                Assert.That(AudioListener.volume,Is.LessThan(.8f),"setup: on again");
                Object.DestroyImmediate(trip);
                Assert.That(AudioListener.volume,Is.EqualTo(1),"destroyed, it leaves the sound at full");
            }
            finally
            {
                if(session!=null){session.Profile.Data.ReducedMotion=reduced;session.Leave();}
                AudioListener.volume=1;
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
