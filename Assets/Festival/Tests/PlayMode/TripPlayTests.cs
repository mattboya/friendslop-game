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
    // In a live dance, the rhythm arrows take the substance's look-ahead.
    public sealed class TripPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TheLocalDoseReachesTheScreenThroughTheSingleWriters()
        {
            var world=new GameObject("Trip world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Trip session");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            yield return null;
            bool reduced=session.Profile.Data.ReducedMotion;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"setup: the solo host reaches the festival: "+session.Message);
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

                // Two Rolly Pollies: the world's one colour grade is more saturated than the same dose of Tongue Stamps.
                Assert.That(world.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"setup: the world's colour grade");
                dose.Intensity=2;dose.Substance="lsd";yield return null;yield return null;float plain=grade.saturation.value;
                dose.Substance="ecstasy";yield return null;yield return null;
                Assert.That(grade.saturation.value,Is.GreaterThan(plain+10),"Rolly Pollies saturate the grade");

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
                dose.Substance="ketamine";
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook");float npcX=npc.X,npcZ=npc.Z;player.X=npcX+.8f;player.Z=npcZ;
                Assert.That(sim.Execute(me,new GameCommand{Id="trip_dance",Kind="Dance",TargetId=npc.Id}).Accepted,Is.True,"setup: a dance with a festivalgoer starts");
                var notes=host.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Notes");
                double farthest=0,danced=0;
                for(deadline=Time.realtimeSinceStartup+6;Time.realtimeSinceStartup<deadline&&danced<1;)
                {
                    npc.X=npcX;npc.Z=npcZ;yield return null;
                    var dance=session.State.Interactions.Find(i=>i.Id==session.LocalPlayer.InteractionId&&i.Status=="Active");if(dance==null)continue;
                    var chart=RhythmChart.For(dance);danced=session.EstimatedSimulationSeconds-dance.StartSeconds;
                    foreach(Transform note in notes)if(note.gameObject.activeSelf)farthest=System.Math.Max(farthest,chart.Notes[int.Parse(note.name.Substring(5))].TimeSeconds-danced);
                }
                Assert.That(danced,Is.GreaterThanOrEqualTo(1),"setup: the dance runs a second past its countdown");
                Assert.That(farthest,Is.GreaterThan(2.1).And.LessThanOrEqualTo(Catalog.FindEffect("ketamine").LeadSeconds+.05),"Pony Dust's arrows show further ahead than the plain 2 s");
            }
            finally
            {
                session.Profile.Data.ReducedMotion=reduced;session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
