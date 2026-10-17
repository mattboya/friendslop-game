using System.Collections;
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
    }
}
