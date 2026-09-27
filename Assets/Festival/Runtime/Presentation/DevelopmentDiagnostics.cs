#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Festival.Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Festival.Presentation
{
    public static class DevelopmentDiagnostics
    {
        public static bool Visible;
        public static void Transition(string action, string reason, string roundId, double tick)
        {
            // Transient round IDs only. Never log names, session credentials or voice data.
            Debug.Log($"[Festival.InteractionVisuals] action={action} round={roundId} time={tick:F3} reason={reason}");
        }
        public static void GraphicsEvent(string category,string action,string detail)
        {
            Debug.Log($"[Festival.{category}] action={action} {detail}");
        }
    }

    /// <summary>Opt-in graphics sampling. This entire type is absent from release players.</summary>
    public sealed class DevelopmentGraphicsDiagnostics : MonoBehaviour
    {
        public FestivalSession Session;
        readonly float[] frameMs=new float[240];
        readonly float[] sortedMs=new float[240];
        readonly FrameTiming[] timings=new FrameTiming[1];
        int sampleCount,sampleIndex,visibleRenderers,skinnedRenderers,shadowCasters,lights,characters,distantCharacters;
        int lastAnimationUpdates,animationUpdates;
        float nextSummary,nextInventory,nextLog,cpuMs,gpuMs,p95,p99;
        bool enabledByUser,showOverlay;
        string summary="",cameraMode="first_person";
        GUIStyle style;

        void Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--graphics-profile")<0)return;
            enabledByUser=true;showOverlay=false;
            nextLog=Time.unscaledTime+10f;
            lastAnimationUpdates=FestivalCharacter.DevelopmentAnimationUpdates;
            DevelopmentDiagnostics.GraphicsEvent("Rendering","profile_start","build="+Application.buildGUID);
        }

        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.f9Key.wasPressedThisFrame)
            {
                enabledByUser=!enabledByUser;
                showOverlay=enabledByUser;
                sampleCount=0;sampleIndex=0;nextSummary=0;nextInventory=0;
                nextLog=Time.unscaledTime+10f;
                lastAnimationUpdates=FestivalCharacter.DevelopmentAnimationUpdates;
                DevelopmentDiagnostics.GraphicsEvent("Rendering",enabledByUser?"diagnostics_on":"diagnostics_off",
                    "build="+Application.buildGUID+" unity="+Application.unityVersion);
            }
            if(!enabledByUser)return;
            frameMs[sampleIndex]=Time.unscaledDeltaTime*1000f;
            sampleIndex=(sampleIndex+1)%frameMs.Length;
            sampleCount=Mathf.Min(sampleCount+1,frameMs.Length);
            FrameTimingManager.CaptureFrameTimings();
            if(FrameTimingManager.GetLatestTimings(1,timings)>0)
            {
                cpuMs=(float)timings[0].cpuFrameTime;
                gpuMs=(float)timings[0].gpuFrameTime;
            }
            if(Time.unscaledTime<nextSummary)return;
            nextSummary=Time.unscaledTime+1f;
            Array.Copy(frameMs,sortedMs,sampleCount);
            Array.Sort(sortedMs,0,sampleCount);
            p95=Percentile(.95f);p99=Percentile(.99f);
            animationUpdates=FestivalCharacter.DevelopmentAnimationUpdates-lastAnimationUpdates;
            lastAnimationUpdates=FestivalCharacter.DevelopmentAnimationUpdates;
            if(Time.unscaledTime>=nextInventory){CountScene();nextInventory=Time.unscaledTime+2f;}
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var quality=QualitySettings.names[QualitySettings.GetQualityLevel()];
            var phase=Session?.State?.Phase??"Disconnected";
            var seed=Session?.State?.Seed??0;
            summary="GRAPHICS / F9  •  "+Application.version+"  "+Application.buildGUID+"\n"
                +"phase "+phase+"  seed "+seed+"  quality "+quality+"  URP "+(pipeline==null?"missing":pipeline.name)+"\n"
                +"screen "+Screen.width+"x"+Screen.height+"  scale "+(pipeline==null?0:pipeline.renderScale).ToString("0.00")
                +"  MSAA "+(pipeline==null?0:pipeline.msaaSampleCount)+"  camera "+cameraMode+"\n"
                +"frame p95 "+p95.ToString("0.0")+"  p99 "+p99.ToString("0.0")+" ms ("+sampleCount+" samples)"
                +"  CPU "+cpuMs.ToString("0.0")+"  GPU "+(gpuMs>0?gpuMs.ToString("0.0"):"unavailable")+" ms\n"
                +"visible renderers "+visibleRenderers+"  skinned "+skinnedRenderers+"  shadow casters "+shadowCasters
                +"  lights "+lights+"\ncharacters "+characters+"  distant mesh "+distantCharacters+"  animation updates/s "+animationUpdates
                +"  main shadows "+(pipeline!=null&&pipeline.supportsMainLightShadows)
                +"  soft "+(pipeline!=null&&pipeline.supportsSoftShadows)
                +"  cascades "+(pipeline==null?0:pipeline.shadowCascadeCount)
                +"  extra shadows "+(pipeline!=null&&pipeline.supportsAdditionalLightShadows);
            // Sampled summaries are useful in logs without paying for per-frame logging.
            if(Time.unscaledTime>=nextLog)
            {
                nextLog=Time.unscaledTime+10f;
                DevelopmentDiagnostics.GraphicsEvent("Rendering","sample",
                    "phase="+phase+" seed="+seed+" quality="+quality+" p95_ms="+p95.ToString("0.0")
                    +" cpu_ms="+cpuMs.ToString("0.0")+" gpu_ms="+(gpuMs>0?gpuMs.ToString("0.0"):"unavailable")
                    +" visible="+visibleRenderers+" distant="+distantCharacters+" animation_updates="+animationUpdates);
            }
        }

        float Percentile(float fraction)
        {
            return sampleCount==0?0:sortedMs[Mathf.Clamp(Mathf.CeilToInt(sampleCount*fraction)-1,0,sampleCount-1)];
        }

        void CountScene()
        {
            visibleRenderers=0;skinnedRenderers=0;shadowCasters=0;lights=0;characters=0;distantCharacters=0;
            foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if(!renderer.isVisible)continue;
                visibleRenderers++;
                if(renderer is SkinnedMeshRenderer)skinnedRenderers++;
                if(renderer.shadowCastingMode!=ShadowCastingMode.Off)shadowCasters++;
            }
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.isActiveAndEnabled)lights++;
            foreach(var actor in FindObjectsByType<FestivalCharacter>(FindObjectsSortMode.None))
            {
                characters++;
                if(actor.UsesDistantMesh)distantCharacters++;
            }
            cameraMode="first_person";
            foreach(var preview in FindObjectsByType<FestivalDancePreview>(FindObjectsSortMode.None))
                if(preview.IsVisible){cameraMode="rhythm_split";break;}
        }

        void OnGUI()
        {
            if(!showOverlay||string.IsNullOrEmpty(summary))return;
            if(style==null)style=new GUIStyle(GUI.skin.box){alignment=TextAnchor.UpperLeft,fontSize=14,
                normal={textColor=Color.white},padding=new RectOffset(12,12,10,10)};
            GUI.Box(new Rect(16,16,Mathf.Min(Screen.width-32,750),154),summary,style);
        }
    }
}
#endif
