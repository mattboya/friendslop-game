using System.Collections;
using System.Collections.Generic;
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
    // gas reaches the hitter's screen through the single writers: FestivalHud adds the kick to the field of view, FestivalNightLighting
    // drains the colour grade, and FestivalTrip pulses its overlay, closes a low-pass, adds a big reverb (GAS-5) and dips the volume.
    // All of it lets go when the gas ends, when the player leaves the session, and when FestivalTrip is turned off or destroyed.
    // Friends see the hitter giggle. GAS-5: it pulses on the camp music's beat, and on a 120 BPM clock with the music off.
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
                float camp=grade.saturation.value;var campFog=(RenderSettings.fogStartDistance,RenderSettings.fogEndDistance,RenderSettings.fogColor);
                Text Find(string name){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
                var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
                var campAudio=host.GetComponent<FestivalCampAudio>();
                FestivalTrip.Perception Now()=>FestivalTrip.For(session.State,me,session.Profile.Data.ReducedMotion,Time.unscaledTime,campAudio.Beat);
                var overlay=host.transform.Find("Festival trip");
                void Sober(string when)
                {
                    var lowPass=view.GetComponent<AudioLowPassFilter>();var reverb=view.GetComponent<AudioReverbFilter>();
                    Assert.That(overlay==null||!overlay.gameObject.activeSelf,when+": no overlay");
                    Assert.That(lowPass==null||!lowPass.enabled,when+": no low-pass");
                    Assert.That(reverb==null||!reverb.enabled,when+": no reverb");
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
                // resumes after the frame's Updates (the HUD's kick, the session's grade) and before its LateUpdates, so the overlay and
                // the sound FestivalTrip drew are last frame's moment, on last frame's beat.
                gas.StartSeconds=sim.State.SimulationSeconds-5;session.Profile.Data.ReducedMotion=false;
                yield return null;yield return null;
                float widest=0,quietest=1,drawnAt=Time.unscaledTime;float? drawnBeat=campAudio.Beat;
                for(deadline=Time.realtimeSinceStartup+1.5f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    var look=Now();var drawn=FestivalTrip.For(session.State,me,false,drawnAt,drawnBeat);drawnAt=Time.unscaledTime;drawnBeat=campAudio.Beat;var lowPass=view.GetComponent<AudioLowPassFilter>();
                    Assert.That(view.fieldOfView,Is.EqualTo(75+look.Fov).Within(.01f),"the HUD adds the gas's kick to the 75 degree view");
                    Assert.That(grade.saturation.value,Is.EqualTo(camp+look.Saturation).Within(1e-3f),"the one grade drains camp's colours");
                    overlay=host.transform.Find("Festival trip");
                    Assert.That(overlay!=null&&overlay.gameObject.activeSelf,"the overlay is up");
                    Assert.That(overlay.Find("Trip vignette").GetComponent<RawImage>().color.a,Is.EqualTo(drawn.Vignette).Within(1e-4f),"its vignette pulses");
                    Assert.That(lowPass!=null&&lowPass.enabled,"the first-person listener is low-passed");
                    Assert.That(lowPass.cutoffFrequency,Is.EqualTo(FestivalTrip.LowPassHz(drawn.Muffle)).Within(1),"on the pulse");
                    Assert.That(AudioListener.volume,Is.EqualTo(1-drawn.Tremolo).Within(1e-4f),"and the tremolo dips it");
                    var reverb=view.GetComponent<AudioReverbFilter>();
                    Assert.That(reverb!=null&&reverb.enabled,"a big reverb on the first-person listener");
                    Assert.That(reverb.room,Is.EqualTo(FestivalTrip.ReverbRoom(drawn.Reverb)).Within(1),"as wet as the gas is strong");
                    widest=Mathf.Max(widest,Mathf.Abs(view.fieldOfView-75));quietest=Mathf.Min(quietest,AudioListener.volume);
                }
                Assert.That(grade.saturation.value,Is.LessThan(camp-30),"the colours drain");
                Assert.That(session.WorldCharacter(me).Giggling,Is.True,"the hitter's body giggles, as friends see it");
                Assert.That(widest,Is.GreaterThan(2),"the view kicks wide on the beat");
                Assert.That(quietest,Is.LessThan(.8f),"the sound goes wah-wah");

                // GAS-5: on the camp music's beat. A new camp plays TECH HOUSE, about 129 BPM and drifting, not the 120 BPM clock. Each
                // frame the vignette drawn last frame is read with where the music was then. Its peaks land on the track's kicks (its
                // measured beat grid, which FestivalMusicTests holds to the onsets heard) within 60 ms, one a kick, beat after beat.
                var techHouse=FestivalMusic.For(1);
                Assert.That(campAudio.Music!=null&&campAudio.Music.isPlaying&&campAudio.Music.clip==techHouse.Audio,"setup: TECH HOUSE plays at camp");
                var vignette=overlay.Find("Trip vignette").GetComponent<RawImage>();
                var drawnVignette=new List<float>();var heardAt=new List<int>();int was=campAudio.Music.timeSamples;
                for(deadline=Time.realtimeSinceStartup+2.5f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    drawnVignette.Add(vignette.color.a);heardAt.Add(was);was=campAudio.Music.timeSamples;
                }
                int kicks=0,peaks=0;var late=new List<string>();
                for(int i=1;i<drawnVignette.Count-1;i++)
                {
                    if(techHouse.BeatPhase(heardAt[i])<techHouse.BeatPhase(heardAt[i-1]))kicks++;
                    if(!(drawnVignette[i]>drawnVignette[i-1]&&drawnVignette[i]>=drawnVignette[i+1]))continue;
                    peaks++;float phase=techHouse.BeatPhase(heardAt[i]),off=Mathf.Min(phase,1-phase)*60/(float)techHouse.Bpm;
                    if(off>.06f)late.Add($"{off*1000:F0} ms");
                }
                Assert.That(kicks,Is.GreaterThanOrEqualTo(4),"setup: four beats or more of TECH HOUSE");
                Assert.That(late,Is.Empty,"the vignette peaks within 60 ms of each of the music's kicks ("+peaks+" peaks, "+kicks+" kicks)");
                Assert.That(peaks,Is.InRange(kicks-1,kicks+1),"one peak a kick");

                // GAS-5 decision A: with the DJ on SILENCE there is no beat to follow, and the gas keeps pulsing on a 120 BPM clock.
                sim.State.CampMusicTrack=0;
                for(deadline=Time.realtimeSinceStartup+2;campAudio.Music!=null&&Time.realtimeSinceStartup<deadline;)yield return null;
                yield return null;
                Assert.That(campAudio.Beat,Is.Null,"setup: SILENCE, no beat");
                float dimmest=1,deepest=0;
                for(deadline=Time.realtimeSinceStartup+.6f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    Assert.That(view.fieldOfView,Is.EqualTo(75+Now().Fov).Within(.01f),"the zoom keeps the clock's beat");
                    dimmest=Mathf.Min(dimmest,vignette.color.a);deepest=Mathf.Max(deepest,vignette.color.a);
                }
                Assert.That(deepest-dimmest,Is.GreaterThan(.2f),"the vignette still pulses with the music off");
                sim.State.CampMusicTrack=1;

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
                // The camp's own grade and fog are back as Leave returns, before the HUD's next frame could put them back.
                Assert.That(grade.saturation.value,Is.EqualTo(camp).Within(1e-3f),"as Leave returns: camp's colours");
                Assert.That((RenderSettings.fogStartDistance,RenderSettings.fogEndDistance,RenderSettings.fogColor),Is.EqualTo(campFog),"as Leave returns: camp's fog");
                yield return null;
                Sober("after leaving the session");
                Assert.That(grade.saturation.value,Is.EqualTo(camp).Within(1e-3f),"after leaving the session: camp's colours");
                Assert.That((RenderSettings.fogStartDistance,RenderSettings.fogEndDistance,RenderSettings.fogColor),Is.EqualTo(campFog),"after leaving the session: camp's fog");

                // FestivalTrip turned off, or destroyed, with the gas on: the global volume never stays dipped.
                var trip=host.GetComponent<FestivalTrip>();
                var on=new RoundState{Phase="Shopping",SimulationSeconds=100};var hitter=new PlayerState{Id="me"};
                hitter.Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=95,RemainingSeconds=25});on.Players.Add(hitter);
                trip.Apply(on,"me",view.transform,null,false,0,.02f);
                Assert.That(AudioListener.volume,Is.LessThan(.8f),"setup: the gas on a kick");
                trip.enabled=false;
                Sober("with the trip layer turned off");
                trip.enabled=true;trip.Apply(on,"me",view.transform,null,false,0,.02f);
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
