using System.Collections.Generic;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Festival.Tests
{
    // LIGHT-1: night levels go dark with neon on the stage, stalls and paths; the local player's dose brightens only their own view.
    public sealed class NightLightingTests
    {
        private GameObject root;
        [TearDown]public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);}

        [Test]public void OnlyNightLevelsAtTheFestivalAreLitForNight()
        {
            foreach(var phase in new[]{"Shopping","Spinning","Loading","Playing","Results","CampReview"})
                for(int level=0;level<Festivals.LevelCount;level++)
                {
                    // Levels run Day 1, Night 1, Day 2, Night 2; camp keeps its own look between them.
                    bool expected=(level==1||level==3)&&(phase=="Playing"||phase=="Results");
                    Assert.That(FestivalNightLighting.IsNight(Round(level,phase)),Is.EqualTo(expected),"level "+level+" in "+phase);
                }
        }

        [Test]public void DoseBrightensOnlyTheLocalPlayersViewCappedForReadability()
        {
            var perDose=new[]{0f,.15f,.30f,.45f,.60f};
            for(int dose=0;dose<perDose.Length;dose++)
                Assert.That(FestivalNightLighting.DoseExposure(Round(1,"Playing",dose),"me"),Is.EqualTo(perDose[dose]).Within(1e-5f),"dose "+dose);
            Assert.That(FestivalNightLighting.DoseExposure(Round(1,"Playing",7),"me"),Is.EqualTo(.6f).Within(1e-5f),"capped for readability");
            Assert.That(FestivalNightLighting.DoseExposure(Round(0,"Playing",2),"me"),Is.EqualTo(.3f).Within(1e-5f),"a day tripper sees their dose too");
            Assert.That(FestivalNightLighting.DoseExposure(Round(1,"Spinning",4),"me"),Is.Zero,"camp keeps its look while the wheels spin");
            Assert.That(FestivalNightLighting.DoseExposure(Round(1,"Playing",3),"stranger"),Is.Zero,"a friend's dose never brightens your view");
        }

        [Test]public void NightDimsSkyToAFifthAndLightsNeonOnExistingStageStallAndPathGeometry()
        {
            root=new GameObject("Night world");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var festival=root.transform.Find(FestivalWorld.RootName);
            var sun=festival.Find("Twilight sun").GetComponent<Light>();
            var day=Sky.Read(sun);
            var neon=new List<Light>();foreach(var light in festival.GetComponentsInChildren<Light>(true))if(light.name.StartsWith("Neon "))neon.Add(light);
            Assert.That(neon.Count,Is.InRange(12,20),"neon lights");
            Assert.That(neon.FindAll(l=>l.enabled),Is.Empty,"day keeps the current lighting: no neon");

            world.SetLighting(Round(0,"Playing"),"me");
            day.AssertMatches(Sky.Read(sun),1,"a day level");
            Assert.That(neon.FindAll(l=>l.enabled),Is.Empty,"no neon by day");

            world.SetLighting(Round(1,"Playing"),"me");
            day.AssertMatches(Sky.Read(sun),.2f,"Night 1");
            var hues=new HashSet<string>();
            foreach(var light in neon)
            {
                Assert.That(light.enabled,Is.True,light.name+" is lit at night");
                Assert.That(light.type,Is.EqualTo(LightType.Point),light.name);
                Assert.That(light.GetComponent<Renderer>(),Is.Null,light.name+" adds light, not new art");
                hues.Add(Hue(light.color));
                float nearest=float.MaxValue;
                foreach(var renderer in festival.GetComponentsInChildren<Renderer>())nearest=Mathf.Min(nearest,Mathf.Sqrt(renderer.bounds.SqrDistance(light.transform.position)));
                Assert.That(nearest,Is.LessThanOrEqualTo(1f),light.name+" sits on existing festival geometry");
            }
            Assert.That(hues,Is.EquivalentTo(new[]{"magenta","cyan","lime"}),"neon palette");
            foreach(var anchor in new[]{"stage","stall","path"})Assert.That(neon.Exists(l=>l.name.Contains(anchor)),anchor+" neon");

            world.SetLighting(Round(1,"CampReview"),"me");
            day.AssertMatches(Sky.Read(sun),1,"back at camp");
            Assert.That(neon.FindAll(l=>l.enabled),Is.Empty,"neon off at camp");
        }

        [Test]public void DoseRaisesExposureAndSaturationThenCampRestoresThem()
        {
            root=new GameObject("Dosed world");var world=root.AddComponent<FestivalWorld>();world.Build();
            Assert.That(root.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"world colour grade");
            float exposure=grade.postExposure.value,saturation=grade.saturation.value;

            world.SetLighting(Round(1,"Playing",0),"me");
            Assert.That(grade.postExposure.value,Is.EqualTo(exposure).Within(1e-5f),"sober players see normal night");
            Assert.That(grade.saturation.value,Is.EqualTo(saturation).Within(1e-5f),"sober saturation");

            world.SetLighting(Round(1,"Playing",2),"me");
            Assert.That(grade.postExposure.value,Is.EqualTo(exposure+.3f).Within(1e-5f),"dose 2");
            float dose2=grade.saturation.value;
            Assert.That(dose2,Is.GreaterThan(saturation),"a dose is more vivid");

            world.SetLighting(Round(3,"Results",4),"me");
            Assert.That(grade.postExposure.value,Is.EqualTo(exposure+.6f).Within(1e-5f),"dose 4 hits the cap");
            Assert.That(grade.saturation.value,Is.GreaterThan(dose2),"dose 4 is more vivid than dose 2");

            world.SetLighting(Round(3,"CampReview",4),"me");
            Assert.That(grade.postExposure.value,Is.EqualTo(exposure).Within(1e-5f),"camp exposure");
            Assert.That(grade.saturation.value,Is.EqualTo(saturation).Within(1e-5f),"camp saturation");
        }

        // J3: the HUD's effect wash is for what you took. The spinner's dose lasts the whole level (everyone's, on Night 2) and the
        // debrief's shot 90 s; a constant green wash under either swamped the neon night, and the dose already shows as exposure.
        [Test]public void OnlyWhatYouTookWashesTheScreen()
        {
            var effects=new List<ActiveEffect>{new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4},new ActiveEffect{Id="shot"}};
            Assert.That(FestivalHud.EffectWash(effects,false,0).a,Is.Zero,"a dose and a debrief shot leave the night unwashed");
            foreach(var taken in Catalog.Effects)
                Assert.That(FestivalHud.EffectWash(new List<ActiveEffect>{new ActiveEffect{Id=taken.Id}},false,0).a,Is.GreaterThan(0),taken.Name+" still washes the screen");
        }

        // A round at the given level and phase. "me" carries the spinner's dose when it is above 0; a friend is always on dose 4.
        private static RoundState Round(int level,string phase,int dose=0)
        {
            var state=new RoundState{LevelIndex=level,Phase=phase};
            var me=new PlayerState{Id="me"};if(dose>0)me.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose});
            var friend=new PlayerState{Id="friend"};friend.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=4});
            state.Players.Add(me);state.Players.Add(friend);
            return state;
        }
        private static string Hue(Color color)
        {
            Color.RGBToHSV(color,out float h,out float s,out _);float degrees=h*360;
            if(s<.6f)return "washed out "+color;
            return Mathf.Abs(degrees-305)<25?"magenta":Mathf.Abs(degrees-185)<25?"cyan":Mathf.Abs(degrees-95)<30?"lime":"hue "+degrees;
        }
        // The sky, ambient, fog and festival sun: everything that should drop to about a fifth of day at night.
        private sealed class Sky
        {
            Color ambientSky,ambientEquator,ambientGround,fog;float sun,skyExposure;
            public static Sky Read(Light sun)=>new Sky{ambientSky=RenderSettings.ambientSkyColor,ambientEquator=RenderSettings.ambientEquatorColor,
                ambientGround=RenderSettings.ambientGroundColor,fog=RenderSettings.fogColor,sun=sun.intensity,
                skyExposure=RenderSettings.skybox!=null&&RenderSettings.skybox.HasProperty("_Exposure")?RenderSettings.skybox.GetFloat("_Exposure"):-1};
            public void AssertMatches(Sky now,float share,string when)
            {
                Rgb(now.ambientSky,ambientSky,share,when+" ambient sky");Rgb(now.ambientEquator,ambientEquator,share,when+" ambient equator");
                Rgb(now.ambientGround,ambientGround,share,when+" ambient ground");Rgb(now.fog,fog,share,when+" fog");
                Assert.That(now.sun,Is.EqualTo(sun*share).Within(1e-4f),when+" sun");
                Assert.That(skyExposure,Is.GreaterThan(0),"setup: the world owns a sky material");
                Assert.That(now.skyExposure,Is.EqualTo(skyExposure*share).Within(1e-4f),when+" sky exposure");
            }
            static void Rgb(Color now,Color day,float share,string what)
            {
                Assert.That(new[]{now.r,now.g,now.b},Is.EqualTo(new[]{day.r*share,day.g*share,day.b*share}).Within(1e-4f),what);
            }
        }
    }
}
