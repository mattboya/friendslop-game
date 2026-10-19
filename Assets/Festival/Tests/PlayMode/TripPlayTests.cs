using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    // TRIP-5 in a running session: the local player's dose reaches their own screen through the single writers. FestivalHud adds
    // the swell to the field of view, FestivalSession trails the mouse, FestivalNightLighting lifts the colour grade, and
    // FestivalTrip draws its overlay and muffles the listener. A dose with nothing on the wheel puts every one of them back.
    // In a live dance, the rhythm arrows take the substance's look-ahead and the selected dose's travel twist.
    public sealed class TripPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator GraphicsChoicesReachTitleGameplayAndCampThroughTheExistingWriters()
        {
            int originalQuality=QualitySettings.GetQualityLevel(),originalVsync=QualitySettings.vSyncCount,originalCap=Application.targetFrameRate;
            var originalPipeline=QualitySettings.renderPipeline;
            var world=new GameObject("impl:U34 live graphics world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U34 live graphics session");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();yield return null;
            try
            {
                var data=session.Profile.Data;data.QualityPreset="Medium";data.VSyncCount=0;data.FrameCap=144;data.FieldOfView=90;data.Brightness=.4f;
                yield return null;yield return null;
                Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()],Is.EqualTo("Medium"));
                Assert.That(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale,Is.EqualTo(.9f).Within(1e-5f));
                Assert.That(Application.targetFrameRate,Is.EqualTo(144));Assert.That(session.Settings.FrameCapControlledByVsync,Is.False);
                Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(FestivalTitleScreen.Fov),"title framing stays authored");
                Assert.That(world.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True);
                Assert.That(grade.postExposure.value,Is.EqualTo(.7f).Within(1e-4f),"brightness applies before connecting");
                session.Host("Graphics tester",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
                var dose=Simulation(session).Player(session.LocalPlayerId).Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);
                Assert.That(dose,Is.Not.Null);dose.Substance="mushrooms";dose.Intensity=4;data.ReducedMotion=false;
                yield return null;yield return null;
                float offset=FestivalTrip.For(session.State,session.LocalPlayerId,false,Time.unscaledTime).Fov;
                Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(90+offset).Within(.01f),"the actual HUD adds the trip over the saved base");
                Assert.That(grade.postExposure.value,Is.EqualTo(1.3f).Within(1e-4f),"four doses and the user's brightness share one grade");
                data.FieldOfView=100;data.Brightness=-.2f;data.RenderScale=1.2f;data.VSyncCount=1;
                yield return null;yield return null;
                offset=FestivalTrip.For(session.State,session.LocalPlayerId,false,Time.unscaledTime).Fov;
                Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(100+offset).Within(.01f));
                Assert.That(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale,Is.EqualTo(1.2f).Within(1e-5f));
                Assert.That(session.Settings.FrameCapControlledByVsync,Is.True);Assert.That(Application.targetFrameRate,Is.EqualTo(-1));Assert.That(data.FrameCap,Is.EqualTo(144));
                Assert.That(grade.postExposure.value,Is.EqualTo(.7f).Within(1e-4f));
                session.Leave();yield return null;yield return null;
                Assert.That(session.ViewCamera.fieldOfView,Is.EqualTo(FestivalTitleScreen.Fov));
                Assert.That(grade.postExposure.value,Is.EqualTo(.1f).Within(1e-4f),"leaving removes the trip and retains chosen brightness");
                Assert.That(world.GetComponents<Volume>().Length,Is.EqualTo(1));
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;yield return null;
            Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(originalQuality));Assert.That(QualitySettings.renderPipeline,Is.SameAs(originalPipeline));
            Assert.That(QualitySettings.vSyncCount,Is.EqualTo(originalVsync));Assert.That(Application.targetFrameRate,Is.EqualTo(originalCap));
        }

        [UnityTest]public IEnumerator SettingsRouteTheLiveSourcesAndKeepMasterAfterLeaving()
        {
            float previousVolume=AudioListener.volume;
            var world=new GameObject("impl:U08 audio world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U08 audio session");var session=host.AddComponent<FestivalSession>();var hud=host.AddComponent<FestivalHud>();
            session.Profile.Data.MuteWhenUnfocused=false;yield return null;yield return null;
            try
            {
                var trip=host.GetComponent<FestivalTrip>();var camp=host.GetComponent<FestivalCampAudio>();
                var chime=(AudioSource)typeof(FestivalHud).GetField("shopAudio",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);
                var foley=(AudioSource)typeof(FestivalCampAudio).GetField("foley",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(camp);
                Assert.That(camp.Music.volume,Is.EqualTo(.21f).Within(1e-5f),"the title keeps the default music loudness");
                Assert.That(chime.volume,Is.EqualTo(.6f).Within(1e-5f),"the chime has the default effects gain");
                Assert.That(foley.volume,Is.EqualTo(.6f).Within(1e-5f),"camp antics have the default effects gain");
                var data=session.Profile.Data;data.MasterVolume=.4f;data.MusicVolume=.8f;data.EffectsVolume=.2f;
                yield return null;yield return null;
                Assert.That(AudioListener.volume,Is.EqualTo(.4f).Within(1e-5f));
                Assert.That(camp.Music.volume,Is.EqualTo(.48f).Within(1e-5f),"the music source applies bus and authored .6 gain only");
                Assert.That(camp.Music.volume*AudioListener.volume,Is.EqualTo(.192f).Within(1e-5f),"master multiplies music once");
                Assert.That(chime.volume*AudioListener.volume,Is.EqualTo(.08f).Within(1e-5f),"master multiplies effects once");
                Assert.That(foley.volume,Is.EqualTo(.2f).Within(1e-5f));

                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"the real audio consumer hosts: "+session.Message);
                yield return null;yield return null;
                Assert.That(camp.Music.volume,Is.EqualTo(.48f).Within(1e-5f),"camp uses the same music bus as the title");
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                int shelf=sim.State.VendorOffers.IndexOf("poi_practice");Assert.That(shelf,Is.GreaterThanOrEqualTo(0));
                var point=Catalog.ShopPoint(true,shelf);player.X=point.X;player.Z=point.Z;player.Cash=100;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="audio-hold",Kind="HoldOffer",ItemId="poi_practice"}).Accepted,Is.True);
                yield return null;yield return null;
                player.X=0;player.Z=7;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="audio-buy",Kind="Buy",ItemId="poi_practice"}).Accepted,Is.True);
                yield return null;yield return null;
                Assert.That(chime.isPlaying,Is.True,"a real purchase plays the routed chime");
                Assert.That(chime.volume,Is.EqualTo(.2f).Within(1e-5f));

                data.MusicVolume=1;data.EffectsVolume=.6f;
                yield return StartLevel(session);
                var stage=host.GetComponent<FestivalStageAudio>();yield return new WaitForSeconds(.4f);
                Assert.That(stage.Music.volume,Is.EqualTo(.6f).Within(1e-5f),"the stage uses authored gain and music, without another master");
                Assert.That(camp.Music,Is.Null,"the camp stays silent in the festival");
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook");float x=npc.X,z=npc.Z;player.X=x+.8f;player.Z=z;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="audio-dance",Kind="Dance",TargetId=npc.Id}).Accepted,Is.True);
                yield return null;yield return null;
                var dance=sim.Interaction(player.InteractionId);
                Assert.That(dance.Kind,Is.EqualTo("Dance"));Assert.That(dance.StageTrack,Is.GreaterThan(0),"the real dance uses the authored stage grid");
                Assert.That(RhythmChart.ValidStageAnchor(dance),Is.True,"the production command supplied a valid authored anchor");
                var rhythm=host.GetComponent<FestivalRhythmAudio>();Assert.That(rhythm,Is.Not.Null);
                var pulse=(AudioSource)typeof(FestivalRhythmAudio).GetField("beat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(rhythm);
                var other=(AudioSource)typeof(FestivalRhythmAudio).GetField("other",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(rhythm);
                Assert.That(pulse.clip.name,Is.EqualTo("Authored festival tick"),"a measured-stage dance uses its light Effects tick");
                Assert.That(other.clip,Is.SameAs(pulse.clip),"both scheduled tick sources share the authored clip");
                Assert.That(pulse.volume,Is.EqualTo(.132f).Within(1e-5f),"the authored tick applies its .22 gain to the Effects bus");
                Assert.That(other.volume,Is.EqualTo(.132f).Within(1e-5f));
                Assert.That(pulse.volume*AudioListener.volume,Is.EqualTo(.0528f).Within(1e-5f),"master multiplies the tick once");
                data.MusicVolume=0;data.EffectsVolume=.3f;npc.X=x;npc.Z=z;yield return null;yield return null;
                Assert.That(pulse.volume,Is.EqualTo(.066f).Within(1e-5f),"music mute leaves the authored tick on the Effects bus");
                Assert.That(other.volume,Is.EqualTo(.066f).Within(1e-5f));
                Assert.That(pulse.volume*AudioListener.volume,Is.EqualTo(.0264f).Within(1e-5f),"music mute still applies master once");
                Assert.That(stage.Music.volume,Is.Zero,"music mute silences the stage");
                data.EffectsVolume=0;npc.X=x;npc.Z=z;yield return null;yield return null;
                Assert.That(pulse.volume,Is.Zero,"effects mute silences rhythm feedback");Assert.That(other.volume,Is.Zero);
                Assert.That(chime.volume,Is.Zero);Assert.That(foley.volume,Is.Zero);
                session.Leave();yield return null;yield return null;
                Assert.That(AudioListener.volume,Is.EqualTo(.4f).Within(1e-5f),"leaving clears the trip while keeping master");
                trip.enabled=false;yield return null;
                Assert.That(AudioListener.volume,Is.EqualTo(.4f).Within(1e-5f),"the real disable callback keeps master");
                data.MuteWhenUnfocused=true;typeof(FestivalTrip).GetMethod("OnApplicationFocus",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(trip,new object[]{false});yield return null;
                Assert.That(AudioListener.volume,Is.Zero);
                Object.Destroy(trip);yield return null;
                Assert.That(AudioListener.volume,Is.Zero,"the real destroy callback keeps focus mute");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);AudioListener.volume=previousVolume;}
            yield return null;
        }

        [UnityTest]public IEnumerator TheLocalDoseReachesTheScreenThroughTheSingleWriters()
        {
            var world=new GameObject("Trip world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Trip session");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            yield return null;
            bool reduced=session.Profile.Data.ReducedMotion;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool diagnostics=DevelopmentDiagnostics.Visible;DevelopmentDiagnostics.Visible=false;int laneDiagnosticCount=0,lookDiagnosticCount=0;string laneDiagnostic="",lookDiagnostic="";
            Application.LogCallback laneLog=(message,stack,type)=>{if(message.StartsWith("[Festival.Rhythm] action=lane_flight ")){laneDiagnosticCount++;laneDiagnostic=message;}if(message.StartsWith("[Festival.Trip] action=look ")){lookDiagnosticCount++;lookDiagnostic=message;}};
            Application.logMessageReceived+=laneLog;
#endif
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);
                var dose=player.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);Assert.That(dose,Is.Not.Null,"setup: solo practice always trips");
                Assert.That(FestivalSimulation.Substances,Does.Contain(dose.Substance),"the spin gave the solo tripper a substance");
                var view=session.ViewCamera;var me=session.LocalPlayerId;
                FestivalTrip.Perception Now()=>FestivalTrip.For(session.State,me,session.Profile.Data.ReducedMotion,Time.unscaledTime);

                // Four Fun Guys at full motion: every frame the HUD writes the base view plus the trip's swell, which comes and goes.
                dose.Intensity=4;dose.Substance="mushrooms";session.Profile.Data.ReducedMotion=false;yield return null;yield return null;
                float widest=0;
                for(deadline=Time.realtimeSinceStartup+3;Time.realtimeSinceStartup<deadline&&widest<2;)
                {
                    yield return null;
                    Assert.That(view.fieldOfView,Is.EqualTo(75+Now().Fov).Within(.01f),"the HUD adds the trip's swell to the 75 degree view");
                    widest=Mathf.Max(widest,Mathf.Abs(view.fieldOfView-75));
                }
                Assert.That(widest,Is.GreaterThanOrEqualTo(2),"four Fun Guys make the view breathe by degrees");

                // Four Pony Dust: the camera trails a turn of the mouse, the edges close in and the sound is muffled.
                dose.Substance="ketamine";yield return null;yield return null;
                var aim=typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance);
                float from=view.transform.eulerAngles.y;aim.SetValue(session,(float)aim.GetValue(session)+90);
                yield return null;
                float turned=Mathf.Abs(Mathf.DeltaAngle(from,view.transform.eulerAngles.y));
                Assert.That(turned,Is.GreaterThan(0).And.LessThan(85),"the camera lags behind a 90 degree turn of the mouse");
                yield return new WaitForSeconds(1.5f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(from+90,view.transform.eulerAngles.y)),Is.LessThan(1),"and catches up");
                var overlay=host.transform.Find("Festival trip");
                Assert.That(overlay!=null&&overlay.gameObject.activeInHierarchy,"the trip's overlay is up");
                Assert.That(overlay.GetComponent<Canvas>().sortingOrder,Is.LessThan(100),"under the HUD's canvas (100)");
                Assert.That(overlay.Find("Trip vignette").GetComponent<RawImage>().color.a,Is.EqualTo(Now().Vignette).Within(1e-4f),"closing the view to a tunnel");
                var muffle=view.GetComponent<AudioLowPassFilter>();
                Assert.That(muffle!=null&&muffle.enabled,"the first-person listener hears it muffled");
                Assert.That(muffle.cutoffFrequency,Is.EqualTo(FestivalTrip.LowPassHz(Now().Muffle)).Within(1),"as muffled as four Pony Dust say");

                // Four Tongue Stamps at full motion: every frame the session's one camera writer tilts the view by the dose's own roll,
                // inside its 3.5 degree clamp, so the world wobbles.
                dose.Substance="lsd";yield return null;yield return null;
                Assert.That(player.Effects.Exists(e=>e.Id=="lsd"||e.Id=="mushrooms"),Is.False,"setup: no shop-bought effect rolls the view as well");
                float tilted=0;
                for(deadline=Time.realtimeSinceStartup+1.5f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    float roll=Mathf.DeltaAngle(0,view.transform.eulerAngles.z);
                    Assert.That(roll,Is.EqualTo(Now().Roll).Within(.01f),"the view tilts by this frame's dose roll");
                    Assert.That(Mathf.Abs(roll),Is.LessThanOrEqualTo(3.5f),"inside the camera's clamp");
                    tilted=Mathf.Max(tilted,Mathf.Abs(roll));
                }
                Assert.That(tilted,Is.GreaterThan(1),"four Tongue Stamps tilt the view by degrees");

                // Two doses: Tongue Stamps lift the world's one colour grade above a dose with no substance, and Rolly Pollies more.
                Assert.That(world.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"setup: the world's colour grade");
                dose.Intensity=2;dose.Substance="";yield return null;yield return null;float plain=grade.saturation.value;
                dose.Substance="lsd";yield return null;yield return null;float stamps=grade.saturation.value;
                Assert.That(stamps,Is.EqualTo(plain+FestivalTrip.Look("lsd",2,false,0).Saturation).Within(.01f),"Tongue Stamps add their own colour to the grade");
                Assert.That(stamps,Is.GreaterThan(plain),"so the saturation rises");
                dose.Substance="ecstasy";yield return null;yield return null;
                Assert.That(grade.saturation.value,Is.GreaterThan(stamps),"Rolly Pollies saturate the grade more");

                // A dose spun before the substance wheel looks plain: every writer puts its property back.
                dose.Substance="";yield return null;yield return null;
                Assert.That(view.fieldOfView,Is.EqualTo(75).Within(1e-3f),"the view stops breathing");
                Assert.That(grade.saturation.value,Is.EqualTo(plain).Within(1e-3f),"the grade is the dose's own");
                Assert.That(overlay.gameObject.activeSelf,Is.False,"the overlay goes");
                Assert.That(muffle.enabled,Is.False,"and the sound comes back");
                from=view.transform.eulerAngles.y;aim.SetValue(session,(float)aim.GetValue(session)+90);yield return null;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(from+90,view.transform.eulerAngles.y)),Is.LessThan(.5f),"the camera follows the mouse at once");

                // Pony Dust in a live dance: the HUD's arrows rise into view 2.4 s ahead of their beat, not the plain 2 s. Each
                // frame, how far ahead of its beat the farthest arrow on screen is; the partner is held in place so the dance runs on.
                dose.Substance="ketamine";dose.Intensity=4;session.Profile.Data.ReducedMotion=true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Assert.That(laneDiagnosticCount,Is.Zero,"lane diagnostics stay silent until opted in");
                Assert.That(lookDiagnosticCount,Is.Zero,"through every look so far the camera's trip trace stays silent until opted in");DevelopmentDiagnostics.Visible=true;
#endif
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook");float npcX=npc.X,npcZ=npc.Z;player.X=npcX+.8f;player.Z=npcZ;
                Assert.That(sim.Execute(me,new GameCommand{Id="trip_dance",Kind="Dance",TargetId=npc.Id}).Accepted,Is.True,"setup: a dance with a festivalgoer starts");
                var notes=host.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Notes");
                double farthest=0,danced=0;int stalledFrames=0;
                for(deadline=Time.realtimeSinceStartup+6;Time.realtimeSinceStartup<deadline&&(danced<1||stalledFrames<2);)
                {
                    npc.X=npcX;npc.Z=npcZ;yield return null;
                    var dance=session.State.Interactions.Find(i=>i.Id==session.LocalPlayer.InteractionId&&i.Status=="Active");if(dance==null)continue;
                    var chart=RhythmChart.For(dance);danced=session.EstimatedSimulationSeconds-dance.StartSeconds;
                    foreach(Transform note in notes)if(note.gameObject.activeSelf)
                    {
                        double remaining=chart.Notes[int.Parse(note.name.Substring(5))].TimeSeconds-danced;
                        farthest=System.Math.Max(farthest,remaining);
                        double progress=1-remaining/2.4;
                        if(progress>=.38&&progress<=.58)
                        {
                            Assert.That(((RectTransform)note).anchorMin.y,Is.EqualTo(.3501667f).Within(.0001f),"the live HUD supplies dose four: Pony Dust stalls a third of the way up even with reduced motion");
                            stalledFrames++;
                        }
                    }
                }
                Assert.That(danced,Is.GreaterThanOrEqualTo(1),"setup: the dance runs a second past its countdown");
                Assert.That(stalledFrames,Is.GreaterThanOrEqualTo(2),"the actual live arrows are observed inside the dose-four plateau on multiple frames");
                Assert.That(farthest,Is.GreaterThan(2.1).And.LessThanOrEqualTo(Catalog.FindEffect("ketamine").LeadSeconds+.05),"Pony Dust's arrows show further ahead than the plain 2 s");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Assert.That(laneDiagnosticCount,Is.EqualTo(1),"the live lane diagnostic samples once, then stays throttled during this dance");
                Assert.That(laneDiagnostic,Does.Contain("effect=ketamine dose=4 lead=2.400"),"the opt-in diagnostic reports the selection actually consumed by the live HUD");
                Assert.That(lookDiagnosticCount,Is.EqualTo(1),"opted in, the camera traces the look it holds once, not every frame: "+lookDiagnostic);
                Assert.That(lookDiagnostic,Does.Contain("substance=ketamine dose=4 reduced=True share=0.25"),"with the substance, dose and reduced-motion share the camera writer used");
#endif
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Application.logMessageReceived-=laneLog;DevelopmentDiagnostics.Visible=diagnostics;
#endif
                session.Profile.Data.ReducedMotion=reduced;session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        // The session's one camera writer adds the dose's roll, on the unscaled clock, to any shop-bought effect's roll, on the
        // simulation clock, and clamps the sum to 3.5 degrees. Reduced motion keeps a quarter of the dose's roll and none of the shop's;
        // camera shake off keeps neither; sober, a spirit or camp roll nothing. Each check runs inside one frame, so the dose's clock
        // holds still across its calls.
        [UnityTest]public IEnumerator TheDoseRollJoinsTheShopRollInsideTheCameraClamp()
        {
            var host=new GameObject("impl-U46 roll session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                var camera=typeof(FestivalSession).GetMethod("UpdateCamera",BindingFlags.NonPublic|BindingFlags.Instance);
                var state=new RoundState{Phase="Playing",LevelIndex=1};var me=new PlayerState{Id="me"};state.Players.Add(me);
                typeof(FestivalSession).GetProperty("State").SetValue(session,state);typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,me.Id);
                var dose=new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance="lsd"};
                var stamp=new ActiveEffect{Id="lsd",RemainingSeconds=60};var guys=new ActiveEffect{Id="mushrooms",RemainingSeconds=60};
                var data=session.Profile.Data;data.ReducedMotion=false;data.CameraShake=true;
                float Roll(params ActiveEffect[] effects){me.Effects.Clear();me.Effects.AddRange(effects);camera.Invoke(session,new object[]{me});return Mathf.DeltaAngle(0,session.ViewCamera.transform.eulerAngles.z);}

                float doseRoll=0;
                for(float deadline=Time.realtimeSinceStartup+5;Time.realtimeSinceStartup<deadline&&Mathf.Abs(doseRoll)<=1.5f;){yield return null;doseRoll=Roll(dose);}
                Assert.That(Mathf.Abs(doseRoll),Is.GreaterThan(1.5f),"four Tongue Stamps on Night 1 tilt the camera by degrees");
                Assert.That(doseRoll,Is.EqualTo(FestivalTrip.For(state,me.Id,false,Time.unscaledTime).Roll).Within(1e-3f),"by the dose's own roll on the unscaled clock");
                int composed=0,clamped=0;
                for(int step=0;step<1500;step++)
                {
                    state.SimulationSeconds=step*.01;
                    float shop=Roll(stamp,guys),both=Roll(stamp,guys,dose);
                    Assert.That(Mathf.Abs(both),Is.LessThanOrEqualTo(3.5f+1e-4f),"the clamp holds at "+state.SimulationSeconds+" s");
                    if(Mathf.Abs(shop)>=3.5f-1e-3f)continue;
                    composed++;if(Mathf.Abs(shop+doseRoll)>3.6f)clamped++;
                    Assert.That(both,Is.EqualTo(Mathf.Clamp(shop+doseRoll,-3.5f,3.5f)).Within(1e-3f),"the shop's "+shop+" and the dose's "+doseRoll+" degrees add up at "+state.SimulationSeconds+" s");
                }
                Assert.That(composed,Is.GreaterThan(1000),"setup: the shop's own roll stays inside the clamp most of the time");
                Assert.That(clamped,Is.GreaterThan(0),"stacked, they reach past 3.5 degrees and the clamp holds them there");

                data.ReducedMotion=true;
                foreach(double at in new[]{.3,1.7,4.1})
                {
                    state.SimulationSeconds=at;
                    Assert.That(Roll(stamp,guys,dose),Is.EqualTo(FestivalTrip.For(state,me.Id,true,Time.unscaledTime).Roll).Within(1e-3f),"reduced motion: only the dose's quarter tilt, at "+at+" s");
                    Assert.That(Roll(stamp,guys,dose),Is.EqualTo(doseRoll*FestivalTrip.ReducedMotionShare).Within(1e-3f),"a quarter of the full tilt");
                }
                data.ReducedMotion=false;data.CameraShake=false;
                foreach(double at in new[]{.3,1.7,4.1}){state.SimulationSeconds=at;Assert.That(Roll(stamp,guys,dose),Is.EqualTo(0).Within(1e-4f),"camera shake off: no tilt at all, at "+at+" s");}
                data.CameraShake=true;
                Assert.That(Roll(),Is.EqualTo(0).Within(1e-4f),"sober: no tilt");
                me.Life="Spirit";Assert.That(Roll(dose),Is.EqualTo(0).Within(1e-4f),"a spirit: no tilt");me.Life="Alive";
                state.Phase="Shopping";Assert.That(Roll(dose),Is.EqualTo(0).Within(1e-4f),"at camp: no tilt");
            }
            finally{session.Leave();Object.Destroy(host);}
            yield return null;
        }

        // TRIP-16: Pony Dust and Couch Lock look out through sleepy eyes on the dosed player's own live screen. Each dose rests the lids
        // further closed, with the black bars flush outside them, and the real per-frame trip blinks them shut and back to the same
        // opening; reduced motion moves them a quarter of the way. On a clock the test holds, a blink lasts one second. Opted in, the
        // trip traces the eye's changes, never its frames.
        [UnityTest]public IEnumerator PonyDustAndCouchLockLookThroughSleepyEyesThatBlink()
        {
            var world=new GameObject("Sleepy eyes world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Sleepy eyes session");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();yield return null;
            var trip=host.GetComponent<FestivalTrip>();float wait=60;int draws=0;trip.BlinkWaits=()=>{draws++;return wait;};
            bool reduced=session.Profile.Data.ReducedMotion;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool diagnostics=DevelopmentDiagnostics.Visible;DevelopmentDiagnostics.Visible=false;var traced=new List<string>();
            Application.LogCallback eyeLog=(message,stack,type)=>{if(message.StartsWith("[Festival.Trip] action=eye "))traced.Add(message);};
            Application.logMessageReceived+=eyeLog;
#endif
            try
            {
                yield return HostALevel(session);
                var dose=Simulation(session).Player(session.LocalPlayerId).Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);
                Assert.That(dose,Is.Not.Null,"setup: solo practice always trips");
                session.Profile.Data.ReducedMotion=false;
                var top=Bar(host,"top");var bottom=Bar(host,"bottom");
                foreach(var substance in new[]{"ketamine","weed"})for(int doses=1;doses<=4;doses++)
                {
                    dose.Substance=substance;dose.Intensity=doses;yield return null;yield return null;
                    string at=Catalog.FindEffect(substance).Name+" at dose "+doses;var eye=EyeOf(host).rectTransform;
                    Assert.That(Opening(host),Is.EqualTo(1-FestivalTrip.EyeRest(doses)).Within(1e-4f),at+": the live screen looks through eyes resting the dose's way");
                    Assert.That(top.anchorMin.y,Is.EqualTo(eye.anchorMax.y).Within(1e-6f),at+": black flush above the eye");
                    Assert.That(bottom.anchorMax.y,Is.EqualTo(eye.anchorMin.y).Within(1e-6f),at+": and below it");
                }

                // The real per-frame trip: a fresh blink wait on each new substance, then a whole blink shuts the eyes and opens them
                // again to the same rest.
                float open=1-FestivalTrip.EyeRest(4);
                foreach(var substance in new[]{"ketamine","weed"})
                {
                    wait=.25f;dose.Substance=substance;dose.Intensity=4;yield return null;yield return null;
                    float least=1;bool reopened=false;
                    for(float deadline=Time.realtimeSinceStartup+4;Time.realtimeSinceStartup<deadline&&!reopened;)
                    {
                        yield return null;float now=Opening(host);least=Mathf.Min(least,now);
                        reopened=least<.05f&&Mathf.Abs(now-open)<1e-4f;
                    }
                    Assert.That(least,Is.LessThan(.05f).And.GreaterThanOrEqualTo(0),Catalog.FindEffect(substance).Name+": a blink shuts the eyes on the live screen");
                    Assert.That(reopened,Catalog.FindEffect(substance).Name+": and opens them again to the same rest");
                }
                session.Profile.Data.ReducedMotion=true;float calm=1;
                for(float deadline=Time.realtimeSinceStartup+3;Time.realtimeSinceStartup<deadline;){yield return null;calm=Mathf.Min(calm,Opening(host));}
                Assert.That(calm,Is.LessThan(open-.05f),"reduced motion: the live eyes still blink");
                Assert.That(calm,Is.GreaterThanOrEqualTo(open*(1-FestivalTrip.ReducedMotionShare)-1e-4f),"but only a quarter of the way, never shut");
                session.Profile.Data.ReducedMotion=false;

                // On the test's clock: a fresh two-second wait on Pony Dust, then one blink, measured frame by frame.
                wait=2;dose.Substance="ketamine";yield return null;yield return null;
                float started=-1,shut=-1,ended=-1,clock=0;
                for(int frame=0;frame<200&&ended<0;frame++)
                {
                    float now=Step(session,1);clock+=.02f;
                    if(started<0&&now<open-1e-4f){started=clock;wait=60;}
                    if(started>=0&&shut<0&&now<1e-5f)shut=clock;
                    if(started>=0&&now>=open-1e-5f)ended=clock;
                }
                Assert.That(started,Is.GreaterThan(0),"setup: a blink came");
                Assert.That(shut,Is.GreaterThan(started),"it shuts the eyes");
                Assert.That(ended-started,Is.EqualTo(FestivalTrip.BlinkSeconds).Within(.03f),"and they are back at rest a second after it began");
                Step(session,1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Assert.That(traced,Is.Empty,"the eye stays silent until diagnostics are opted in");
                DevelopmentDiagnostics.Visible=true;Step(session,50);
                Assert.That(traced.Count,Is.EqualTo(1),"opted in, the eye is traced once as it stands, not every frame:\n"+string.Join("\n",traced));
                Assert.That(traced[0],Does.Contain("shown=True substance=ketamine rest=0.40 reduced=False blinking=False held=False"));
                session.MenuOpen=true;Step(session,30);session.MenuOpen=false;Step(session,1);
                Assert.That(traced.Count,Is.EqualTo(3),"a held moment is traced as it begins and as it ends:\n"+string.Join("\n",traced));
                Assert.That(traced[1],Does.Contain("held=True"));Assert.That(traced[2],Does.Contain("held=False").And.Contain("next_blink=60.0"));
#endif
                dose.Substance="";yield return null;yield return null;
                Assert.That(Opening(host),Is.EqualTo(-1),"a dose spun before the substance wheel: no eye");
                Assert.That(host.transform.Find("Festival trip").gameObject.activeSelf,Is.False,"and no overlay at all");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Assert.That(traced[traced.Count-1],Does.Contain("shown=False"),"the trace says the eye went");
#endif
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Application.logMessageReceived-=eyeLog;DevelopmentDiagnostics.Visible=diagnostics;
#endif
                session.Profile.Data.ReducedMotion=reduced;session.Leave();Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        // TRIP-16: on the live screen no blink closes over something timed or protected. The menu, a real dance, a real security stop
        // and a talk, each begun as a blink closes, ease the lids open from where they are and hold them open through the real per-frame
        // trip while they last; the talk's chat card holds them too while it lingers after the talk. Once one ends, the next blink waits
        // a freshly drawn interval.
        [UnityTest]public IEnumerator TimedMomentsHoldTheLiveEyesOpen()
        {
            var world=new GameObject("Held eyes world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Held eyes session");var session=host.AddComponent<FestivalSession>();var hud=host.AddComponent<FestivalHud>();yield return null;
            var trip=host.GetComponent<FestivalTrip>();int draws=0;trip.BlinkWaits=()=>{draws++;return 1;};
            bool reduced=session.Profile.Data.ReducedMotion;
            try
            {
                yield return HostALevel(session);
                var sim=Simulation(session);var me=session.LocalPlayerId;var player=sim.Player(me);
                var dose=player.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);dose.Substance="ketamine";dose.Intensity=4;
                session.Profile.Data.ReducedMotion=false;sim.State.Npcs.Clear();sim.HasLineOfSight=(ax,az,bx,bz)=>true;
                yield return null;yield return null;
                Assert.That(session.MenuOpen,Is.False,"setup: the menu is closed");
                var chat=(FestivalChatWindow)typeof(FestivalHud).GetField("chatWindow",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);
                float open=1-FestivalTrip.EyeRest(4);
                // The host's view of its own simulation right now, as its next frame would show it.
                void Refresh()=>typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(sim,me));
                bool Busy()=>session.State.Interactions.Exists(i=>i.Id==session.LocalPlayer.InteractionId&&i.Status=="Active");
                NpcState Beside(string id,string kind){var npc=new NpcState{Id=id,Kind=kind,Role="Regular",CanTalk=true,X=player.X,Z=player.Z+1.5f,Yaw=180};sim.State.Npcs.Add(npc);return npc;}
                // On the test's clock: from rest into a blink, closing and not yet shut.
                void Closing(string name)
                {
                    float now=Step(session,1);
                    for(int frame=0;frame<200&&now<open-1e-5f;frame++)now=Step(session,1);
                    for(int frame=0;frame<200&&!(now<open-.1f&&now>.05f);frame++)now=Step(session,1);
                    Assert.That(now,Is.LessThan(open-.1f).And.GreaterThan(.05f),"setup: "+name+" begins as a blink closes");
                }
                // Begun mid-close: the lids only open, are open within half a second and stay so, with no wait drawn.
                void Opens(string name,int drawn)
                {
                    float previous=Opening(host);
                    for(int frame=0;frame<250;frame++)
                    {
                        float now=Step(session,1);
                        Assert.That(now,Is.GreaterThanOrEqualTo(previous-1e-5f),name+": the lids only open");
                        if(frame>=25)Assert.That(now,Is.EqualTo(open).Within(1e-5f),name+": open within half a second, and no blink while it lasts");
                        previous=now;
                    }
                    Assert.That(draws,Is.EqualTo(drawn),name+": no wait drawn while it lasts");
                }
                // Ended: the next blink comes a whole freshly drawn wait later.
                void Freed(string name,int drawn)
                {
                    float back=-1;for(int frame=0;frame<150&&back<0;frame++)if(Step(session,1)<open-1e-4f)back=frame*.02f;
                    Assert.That(draws,Is.EqualTo(drawn+1),name+": letting go draws a fresh wait");
                    Assert.That(back,Is.EqualTo(1).Within(.05f),name+": and the next blink comes that wait after it ends");
                }
                // Real frames of the live screen, each held open while the moment stands.
                IEnumerator Holds(string name,float seconds,System.Func<bool> stands)
                {
                    for(float until=Time.realtimeSinceStartup+seconds;Time.realtimeSinceStartup<until;)
                    {
                        yield return null;
                        Assert.That(stands(),"setup: "+name+" still stands");
                        Assert.That(Opening(host),Is.EqualTo(open).Within(1e-5f),name+": held open on the live screen");
                    }
                }

                Closing("the menu");int drawn=draws;session.MenuOpen=true;Opens("the menu",drawn);
                yield return Holds("the menu",.4f,()=>session.MenuOpen);
                session.MenuOpen=false;Freed("the menu",drawn);

                var partner=Beside("held_dance_partner","Wook");
                Closing("a dance");drawn=draws;session.Command("Dance",partner.Id);Refresh();
                Assert.That(Busy(),"setup: a real dance starts: "+session.Message);Opens("a dance",drawn);
                yield return Holds("a dance",.4f,()=>{partner.X=player.X;partner.Z=player.Z+1.5f;return Busy();});
                session.Command("Cancel");Refresh();Freed("a dance",drawn);sim.State.Npcs.Clear();

                player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
                Closing("a security stop");drawn=draws;var cop=Beside("held_cop","Cop");cop.Mode="Stop";cop.TargetId=me;Refresh();Opens("a security stop",drawn);
                yield return Holds("a security stop",.4f,()=>session.State.Npcs.Exists(n=>n.Kind=="Cop"&&n.TargetId==me&&n.Mode=="Stop"));
                sim.State.Npcs.Clear();player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");player.EquippedItemId="";Refresh();Freed("a security stop",drawn);

                string talkerId="held_talker_0";for(int n=1;n<256&&DialogueGrammar.PersonaFor(talkerId,"")!="Wook";n++)talkerId="held_talker_"+n;
                var talker=Beside(talkerId,"Wook");
                Closing("a talk");drawn=draws;session.Command("Conversation",talker.Id);Refresh();
                Assert.That(Busy(),"setup: a real talk starts: "+session.Message);Opens("a talk",drawn);
                for(float until=Time.realtimeSinceStartup+5;!chat.Open&&Time.realtimeSinceStartup<until;)
                {
                    yield return null;Assert.That(Opening(host),Is.EqualTo(open).Within(1e-5f),"a talk: held open on the live screen");
                }
                Assert.That(chat.Open,"setup: the HUD shows the talk's chat card");
                session.Command("Cancel");yield return null;
                // Two and a half seconds with a one-second wait: only the card holds the blink off.
                yield return Holds("the talk's lingering chat card",2.5f,()=>chat.Open&&!Busy()&&!session.MenuOpen);
                session.HandleMenuPress();
                Assert.That(chat.Open,Is.False,"the menu key closes the card");Assert.That(session.MenuOpen,Is.False,"without opening the menu");
                Freed("the talk's chat card",drawn);
            }
            finally{session.Profile.Data.ReducedMotion=reduced;session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }

        // TRIP-16: a shut eye never leaks into the next screen. Mid-blink, the dose running out, a new substance, a spirit, the trip
        // turned off and leaving the session each throw the blink away on the live screen; back on a dose the eyes open at their rest,
        // with a freshly drawn wait. A stand-in friend trips, so the host's own dose is the test's to end.
        [UnityTest]public IEnumerator AShutEyeNeverLeaksIntoTheNextScreen()
        {
            var world=new GameObject("Leaked eye world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Leaked eye session");var session=host.AddComponent<FestivalSession>();yield return null;
            var trip=host.GetComponent<FestivalTrip>();int draws=0;trip.BlinkWaits=()=>{draws++;return 1;};
            bool reduced=session.Profile.Data.ReducedMotion;
            try
            {
                yield return HostALevel(session,besideATripper:true);
                // A new session hosts a new simulation, so each look goes through the session's own.
                PlayerState Me()=>Simulation(session).Player(session.LocalPlayerId);
                ActiveEffect Dose()=>Me().Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);
                session.Profile.Data.ReducedMotion=false;yield return null;yield return null;
                float open=1-FestivalTrip.EyeRest(4);
                // On the test's clock: on into the .1 s the lids stay shut.
                void Shut(string name){float now=Step(session,1);for(int frame=0;frame<200&&now>1e-5f;frame++)now=Step(session,1);Assert.That(now,Is.Zero,"setup: shut mid-blink before "+name);}
                IEnumerator Gone(string name){yield return null;yield return null;Assert.That(Opening(host),Is.EqualTo(-1),name+": the eye goes at once");}
                IEnumerator Back(string name,int drawn)
                {
                    yield return null;yield return null;
                    Assert.That(Opening(host),Is.EqualTo(open).Within(1e-5f),name+": back on a dose, the eyes open at their rest, not shut");
                    Assert.That(draws,Is.GreaterThan(drawn),name+": with a freshly drawn wait");
                }

                Shut("the dose runs out");int drawn=draws;Dose().RemainingSeconds=.01;
                for(float until=Time.realtimeSinceStartup+3;Dose()!=null&&Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(Dose(),Is.Null,"setup: the host's own clock ends the dose");yield return Gone("the dose runs out");
                Me().Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance="ketamine",RemainingSeconds=600,StartSeconds=Simulation(session).State.SimulationSeconds});
                yield return Back("a dose after one ran out",drawn);

                Shut("a new substance");drawn=draws;Dose().Substance="weed";yield return Back("a new substance",drawn);

                Shut("a spirit");drawn=draws;Me().Life="Spirit";yield return Gone("a spirit");
                Me().Life="Alive";yield return Back("alive again",drawn);

                Shut("the trip turned off");drawn=draws;trip.enabled=false;
                Assert.That(Opening(host),Is.EqualTo(-1),"the trip turned off: the eye goes at once");
                Assert.That(host.transform.Find("Festival trip").gameObject.activeSelf,Is.False,"with the whole overlay");
                yield return null;Assert.That(Opening(host),Is.EqualTo(-1),"and stays gone while it is off");
                trip.enabled=true;yield return Back("the trip turned on again",drawn);

                Shut("leaving the session");drawn=draws;session.Leave();yield return Gone("leaving the session");
                Assert.That(host.transform.Find("Festival trip").gameObject.activeSelf,Is.False,"the title screen has no overlay");
                yield return HostALevel(session,besideATripper:true);yield return Back("the next session",drawn);
            }
            finally{session.Profile.Data.ReducedMotion=reduced;session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }

        // TRIP-16: the eyes are the dosed player's own. A sober friend joined to the session sees none while the host's blink. Given a
        // dose of their own, the friend's own screen rests their eyes its way, and a friend who leaves mid-blink and joins again comes
        // back with them open at that rest.
        [UnityTest]public IEnumerator ASoberFriendSeesNoEyesAndAJoinedTripperBlinksOnTheirOwn()
        {
            var world=new GameObject("Friend eyes world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Friend eyes host");var host=hostObject.AddComponent<FestivalSession>();
            var friendObject=new GameObject("Friend eyes friend");var friend=friendObject.AddComponent<FestivalSession>();yield return null;
            hostObject.GetComponent<FestivalTrip>().BlinkWaits=()=>.5f;
            int friendDraws=0;friendObject.GetComponent<FestivalTrip>().BlinkWaits=()=>{friendDraws++;return 60;};
            try
            {
                ushort port=FreePort();host.Host("Host",port);float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);friend.Join("Friend","127.0.0.1",port);
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,friend.Message);
                var sim=Simulation(host);var mate=sim.Player(friend.LocalPlayerId);mate.X=0;mate.Z=19;friend.Command("Ready");
                while(!mate.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(mate.Ready,Is.True,"setup: the friend's Ready reached the host");yield return StartLevel(host,friend.LocalPlayerId);
                var me=sim.Player(host.LocalPlayerId);string mateId=mate.Id;
                // The host trips, so the host keeps no friend dosed in their place.
                sim.State.TripperId=me.Id;foreach(var p in new[]{me,mate})p.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
                me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance="ketamine",RemainingSeconds=600,StartSeconds=sim.State.SimulationSeconds});
                host.Profile.Data.ReducedMotion=friend.Profile.Data.ReducedMotion=false;
                // Until both screens draw the doses as they stand now: the host's next frame, the friend's next snapshots.
                yield return new WaitForSeconds(.5f);
                float hostShut=1;
                for(float until=Time.realtimeSinceStartup+2.5f;Time.realtimeSinceStartup<until;)
                {
                    yield return null;hostShut=Mathf.Min(hostShut,Opening(hostObject));
                    Assert.That(Opening(friendObject),Is.EqualTo(-1),"a sober friend sees no eyes while the host blinks");
                    var overlay=friendObject.transform.Find("Festival trip");
                    Assert.That(overlay==null||!overlay.gameObject.activeSelf,"and no trip overlay at all");
                }
                Assert.That(hostShut,Is.LessThan(.05f).And.GreaterThanOrEqualTo(0),"setup: the host's own eyes blinked meanwhile");

                int sober=friendDraws;mate.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,Substance="weed",RemainingSeconds=600,StartSeconds=sim.State.SimulationSeconds});
                for(deadline=Time.realtimeSinceStartup+5;Opening(friendObject)<0&&Time.realtimeSinceStartup<deadline;)yield return null;
                float rest=1-FestivalTrip.EyeRest(2);
                Assert.That(Opening(friendObject),Is.EqualTo(rest).Within(1e-4f),"a joined friend's own dose rests their own eyes its way");
                Assert.That(friendDraws,Is.EqualTo(sober+1),"with a wait their own screen draws");

                float now=Step(friend,1);for(int frame=0;frame<4000&&now>1e-5f;frame++)now=Step(friend,1);
                Assert.That(now,Is.Zero,"setup: the friend's eyes shut mid-blink, on the test's clock");
                int drawn=friendDraws;friend.Leave();yield return null;yield return null;
                Assert.That(Opening(friendObject),Is.EqualTo(-1),"the friend leaves mid-blink: no shut eye on their title screen");
                friend.Join("Friend","127.0.0.1",port);
                for(deadline=Time.realtimeSinceStartup+30;(friend.LocalPlayer==null||Opening(friendObject)<0)&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(friend.LocalPlayerId,Is.EqualTo(mateId),"setup: the friend joins back as themselves: "+friend.Message);
                Assert.That(Opening(friendObject),Is.EqualTo(rest).Within(1e-4f),"back in the festival, their eyes open at their dose's rest, not shut");
                Assert.That(friendDraws,Is.GreaterThan(drawn),"with a freshly drawn wait");
            }
            finally{friend.Leave();host.Leave();foreach(var go in new[]{friendObject,hostObject,world})Object.Destroy(go);}
            yield return null;
        }

        // TRIP-16: a hosted level. Beside a stand-in friend, Sam (no client of their own), who trips, the host is never dosed in their
        // place, and the host's own dose is the test's: four doses of Pony Dust, lasting the level.
        private static IEnumerator HostALevel(FestivalSession session,bool besideATripper=false)
        {
            session.Host("Tester",HostPort);
            for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,"setup: the host has a local player: "+session.Message);
            var sim=Simulation(session);var mate=besideATripper?sim.AddPlayer("eye_mate","Sam"):null;if(mate!=null)mate.Ready=true;
            yield return StartLevel(session);
            if(mate==null)yield break;
            var me=sim.Player(session.LocalPlayerId);sim.State.TripperId=mate.Id;
            foreach(var p in new[]{me,mate})p.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
            mate.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance="lsd",RemainingSeconds=600,StartSeconds=sim.State.SimulationSeconds});
            me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4,Substance="ketamine",RemainingSeconds=600,StartSeconds=sim.State.SimulationSeconds});
        }
        // TRIP-16: the trip's eye on owner's screen, the bars beside it, and the eye's opening: its share of the screen's height, -1 with
        // no eye drawn.
        private static RawImage EyeOf(GameObject owner)=>owner.transform.Find("Festival trip/Trip eye")?.GetComponent<RawImage>();
        private static RectTransform Bar(GameObject owner,string side)=>(RectTransform)owner.transform.Find("Festival trip/Trip letterbox "+side);
        private static float Opening(GameObject owner){var eye=EyeOf(owner);if(eye==null||!eye.isActiveAndEnabled)return -1;var r=eye.rectTransform;return r.anchorMax.y-r.anchorMin.y;}
        // TRIP-16: frames of dt seconds through the trip's own Apply on the live session's state, view and comfort setting, all inside
        // one of the test's frames, so the clock is the test's alone; the eye's opening after the last.
        private static float Step(FestivalSession session,int frames,float dt=.02f)
        {
            var trip=session.GetComponent<FestivalTrip>();
            for(int i=0;i<frames;i++)trip.Apply(session.State,session.LocalPlayerId,session.ViewCamera.transform,null,session.Profile.Data.ReducedMotion,Time.unscaledTime,dt);
            return Opening(session.gameObject);
        }
    }
}
