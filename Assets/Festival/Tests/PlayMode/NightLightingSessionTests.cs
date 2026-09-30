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

namespace Festival.Tests
{
    public sealed class NightLightingSessionTests:FestivalPlayModeTest
    {
        // LIGHT-1: a hosted game switches the world to night lighting when Night 1 starts, brightened by the host's own dose.
        [UnityTest]public IEnumerator NightLevelLightsNeonAndTheHostsDoseInTheRunningGame()
        {
            var world=new GameObject("Night lighting world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Night lighting session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                // The session drives the first world it finds, so a world leaked by an earlier test would take the lighting.
                Assert.That(Object.FindObjectsByType<FestivalWorld>(FindObjectsSortMode.None),Has.Length.EqualTo(1),"setup: a world leaked from an earlier test");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                Assert.That(world.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"world colour grade");
                float dayExposure=grade.postExposure.value;var daySky=RenderSettings.ambientSkyColor;

                sim.State.LevelIndex=1;
                var player=sim.Player(session.LocalPlayerId);player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"Night 1 starts: "+session.Message);
                yield return null;

                int dose=player.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect)?.Intensity??0;
                Assert.That(dose,Is.InRange(1,4),"setup: the solo host is the tripper");
                Assert.That(RenderSettings.ambientSkyColor.b,Is.EqualTo(daySky.b*.2f).Within(1e-3f),"the night sky");
                int lit=0;
                foreach(var light in world.transform.Find(FestivalWorld.RootName).GetComponentsInChildren<Light>())if(light.name.StartsWith("Neon ")&&light.enabled)lit++;
                Assert.That(lit,Is.InRange(12,20),"neon lit at night");
                Assert.That(grade.postExposure.value,Is.EqualTo(dayExposure+.15f*dose).Within(1e-4f),"the host's dose brightens their own view");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
