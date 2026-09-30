using System.Collections.Generic;
using Festival.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Festival.Presentation
{
    /// <summary>LIGHT-1: night levels dim the sky and light neon on the stage, stalls and paths; the local player's
    /// dose brightens and saturates only their own view. TWISTVIS-1: an Ember Playa dust storm closes the fog in. Driven
    /// from the round state, with no network calls. This is the only writer of the scene's fog, so night and storm never
    /// fight over it.</summary>
    public sealed class FestivalNightLighting
    {
        // Wave 2026-09-30 L9-L11: night sky and ambient at about a fifth of day; exposure +.15 per dose, capped at +.6.
        private const float NightSkyShare=.2f,ExposurePerDose=.15f,MaxDoseExposure=.6f;
        // A storm's dusty wall: you see a little beyond the festivalgoers' 5 m (Festivals.DustStormSightRange), no more.
        private const float StormFogStart=2,StormFogEnd=16;
        private static readonly Color StormDust=new Color(.66f,.52f,.36f);
        // Saturation rides the exposure boost (+6 per dose), so a dose reads more vivid, not only brighter.
        private const float SaturationPerExposure=40;
        private const float NeonRange=7,NeonIntensity=2.5f;
        private static readonly Color[] NeonColors={new Color(1f,.1f,.85f),new Color(.1f,.9f,1f),new Color(.55f,1f,.1f)}; // magenta, cyan, lime
        // In front of existing geometry: the stage's emissive frame and lip, the stall fronts and signs, and the
        // main path's glint strips. Lights only; no new art.
        private static readonly (string Anchor,Vector3 At)[] NeonSpots={
            ("stage frame left",new Vector3(-8.8f,4.4f,35.3f)),("stage frame right",new Vector3(8.8f,4.4f,35.3f)),
            ("stage frame crown",new Vector3(0,7.2f,35.3f)),("stage lip west",new Vector3(-6,1.7f,27.3f)),("stage lip east",new Vector3(6,1.7f,27.3f)),
            ("stall supplies",new Vector3(-24,2.6f,-19.2f)),("stall performance",new Vector3(-18,2.6f,-19.2f)),("stall stock",new Vector3(-12,2.6f,-19.2f)),
            ("stall medical",new Vector3(24,3.3f,-20.3f)),("stall security",new Vector3(27,3.3f,4.7f)),("stall lost property",new Vector3(-28,2.6f,17.3f)),
            ("path west",new Vector3(-3.65f,.6f,-23)),("path east",new Vector3(3.65f,.6f,-9)),("path west",new Vector3(-3.65f,.6f,5)),("path east",new Vector3(3.65f,.6f,19))};

        private readonly List<Light> neon=new List<Light>();
        private readonly Light sun;
        private readonly ColorAdjustments grade;
        private readonly Material sky;
        private readonly Color daySky,dayEquator,dayGround,dayFog;
        private readonly float daySun,daySkyExposure,dayExposure,daySaturation,dayFogStart,dayFogEnd;
        private bool night,storm;
        private float boost;

        public static bool IsNight(RoundState state)=>FestivalWorld.ShowsFestival(state.Phase)&&Festivals.For(state).Night;
        // Only the local player's own dose counts; a spirit's effects are cleared, so the dead see a sober night.
        public static float DoseExposure(RoundState state,string localPlayerId)
        {
            var me=FestivalWorld.ShowsFestival(state.Phase)?state.Players.Find(p=>p.Id==localPlayerId):null;
            int dose=me?.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect)?.Intensity??0;
            return Mathf.Min(MaxDoseExposure,ExposurePerDose*Mathf.Max(0,dose));
        }

        // Captures the day look the world has just set up and hangs the neon, switched off, on the festival anchors.
        public FestivalNightLighting(Transform festival,Light sun,ColorAdjustments grade,Material sky)
        {
            this.sun=sun;this.grade=grade;this.sky=sky;
            daySky=RenderSettings.ambientSkyColor;dayEquator=RenderSettings.ambientEquatorColor;dayGround=RenderSettings.ambientGroundColor;dayFog=RenderSettings.fogColor;
            dayFogStart=RenderSettings.fogStartDistance;dayFogEnd=RenderSettings.fogEndDistance;
            daySun=sun.intensity;daySkyExposure=sky!=null?sky.GetFloat("_Exposure"):0;
            dayExposure=grade.postExposure.value;daySaturation=grade.saturation.value;
            for(int i=0;i<NeonSpots.Length;i++)
            {
                var go=new GameObject("Neon "+NeonSpots[i].Anchor);go.transform.SetParent(festival,false);go.transform.localPosition=NeonSpots[i].At;
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=NeonColors[i%NeonColors.Length];
                light.range=NeonRange;light.intensity=NeonIntensity;light.shadows=LightShadows.None;light.enabled=false;
                neon.Add(light);
            }
        }

        public void Apply(RoundState state,string localPlayerId)
        {
            bool night=IsNight(state),storm=FestivalWorld.ShowsFestival(state.Phase)&&FestivalSimulation.DustStorm(state);float boost=DoseExposure(state,localPlayerId);
            if(night==this.night&&storm==this.storm&&boost==this.boost)return;
            this.night=night;this.storm=storm;this.boost=boost;
            float share=night?NightSkyShare:1;
            RenderSettings.ambientSkyColor=Dim(daySky,share);RenderSettings.ambientEquatorColor=Dim(dayEquator,share);
            RenderSettings.ambientGroundColor=Dim(dayGround,share);RenderSettings.fogColor=Dim(storm?StormDust:dayFog,share);
            RenderSettings.fogStartDistance=storm?StormFogStart:dayFogStart;RenderSettings.fogEndDistance=storm?StormFogEnd:dayFogEnd;
            sun.intensity=daySun*share;
            if(sky!=null)sky.SetFloat("_Exposure",daySkyExposure*share);
            grade.postExposure.Override(dayExposure+boost);grade.saturation.Override(daySaturation+boost*SaturationPerExposure);
            foreach(var light in neon)light.enabled=night;
        }
        private static Color Dim(Color color,float share)=>new Color(color.r*share,color.g*share,color.b*share,color.a);
    }
}
