using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>TRIP-11: the dose's pattern (Perception's Lattice, Cells, Drift and Breathe) redrawn over the renderers Mark tagged, in
    /// one camera only. The pass is queued from code, so no renderer asset or generate step; without the shader the view stays plain.</summary>
    public sealed class FestivalTripSurfaces : IDisposable
    {
        /// <summary>Light layers are off in the Festival pipeline, so this rendering layer changes no lighting.</summary>
        public const uint SurfaceLayer=1u<<7;
        private const string ShaderName="FestivalTripSurface";
        private static readonly int Look=Shader.PropertyToID("_FestivalTripSurface");

        /// <summary>Tags every renderer under root, except people (a FestivalCharacter and all it holds): they carry the clue and
        /// security reads, so they stay clean.</summary>
        public static void Mark(Transform root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                if(renderer.GetComponentInParent<FestivalCharacter>(true)==null)renderer.renderingLayerMask|=SurfaceLayer;
        }
        /// <summary>For scenery that moves: the pattern lies on world planes, so it would stay put while the thing slid through it.</summary>
        public static void Unmark(Transform root){foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.renderingLayerMask&=~SurfaceLayer;}

        private readonly Pass pass;
        private Camera view;
        private bool showing;

        /// <summary>shader names the pattern's shader in a Resources folder; a missing or unsupported one leaves every view plain.</summary>
        public FestivalTripSurfaces(string shader=ShaderName)
        {
            bool off=false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // The same build with the pattern off, so the stage benchmark can measure the pattern's own GPU cost.
            off=Environment.GetEnvironmentVariable("FESTIVAL_TRIP_SURFACES")=="off";
#endif
            var found=off?null:Resources.Load<Shader>(shader);
            if(found!=null&&found.isSupported)pass=new Pass(new Material(found){name="Festival trip surfaces",hideFlags=HideFlags.HideAndDontSave});
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("Trip","surfaces","available="+Available+" reason="+(off?"switched_off":found==null?"missing_shader":!found.isSupported?"unsupported":"ok"));
#endif
        }

        public bool Available=>pass!=null;

        /// <summary>Draws look's pattern in camera from its next frame; a look with no pattern, or no camera, draws nothing.</summary>
        public void Show(Camera camera,FestivalTrip.Perception look)
        {
            view=camera;
            bool show=pass!=null&&camera!=null&&(look.Lattice>0||look.Cells>0||look.Breathe!=0);
            // Hooked only while a pattern shows, so a sober view, or an owner torn down without Dispose, leaves nothing behind.
            if(show!=showing){if(show)RenderPipelineManager.beginCameraRendering+=Queue;else RenderPipelineManager.beginCameraRendering-=Queue;showing=show;}
            if(show)pass.Material.SetVector(Look,new Vector4(look.Lattice,look.Cells,look.Drift,look.Breathe));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)Trace(show,look);
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private (bool,Camera,float,float) traced;
        // Drift and Breathe move every frame, so only the camera and the strengths make a change worth a line.
        private void Trace(bool show,FestivalTrip.Perception look)
        {
            var now=show?(true,view,look.Lattice,look.Cells):(false,(Camera)null,0f,0f);
            if(now==traced)return;
            string detail=!show?"state=off":"state="+(traced.Item1?"change":"on")+" camera="+view.name+" lattice="+look.Lattice.ToString("F2")
                +" cells="+look.Cells.ToString("F2")+" drift="+look.Drift.ToString("F2")+" breathe="+look.Breathe.ToString("F2");
            traced=now;
            DevelopmentDiagnostics.GraphicsEvent("Trip","surfaces",detail);
        }
#endif

        private void Queue(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=view)return;
            var data=camera.GetUniversalAdditionalCameraData();
            if(data!=null&&data.scriptableRenderer!=null)data.scriptableRenderer.EnqueuePass(pass);
        }

        public void Dispose()
        {
            Show(null,default);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("Trip","surfaces","state=teardown");
#endif
            if(pass!=null){if(Application.isPlaying)Object.Destroy(pass.Material);else Object.DestroyImmediate(pass.Material);}
        }

        private sealed class Pass : ScriptableRenderPass
        {
            public readonly Material Material;
            private static readonly ShaderTagId Forward=new ShaderTagId("UniversalForward");
            private sealed class Data { public RendererListHandle List; }
            // After the opaques it multiplies, and before text, sprites and trails, so they stay clean.
            public Pass(Material material){Material=material;renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;}

            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var resources=frame.Get<UniversalResourceData>();var rendering=frame.Get<UniversalRenderingData>();
                var camera=frame.Get<UniversalCameraData>();var lights=frame.Get<UniversalLightData>();
                using(var builder=graph.AddRasterRenderPass<Data>("Festival trip surfaces",out var data))
                {
                    var drawing=RenderingUtils.CreateDrawingSettings(Forward,rendering,camera,lights,SortingCriteria.CommonOpaque);
                    drawing.overrideMaterial=Material;drawing.overrideMaterialPassIndex=0;
                    var filtering=new FilteringSettings(RenderQueueRange.opaque,~0,SurfaceLayer);
                    data.List=graph.CreateRendererList(new RendererListParams(rendering.cullResults,drawing,filtering));
                    builder.UseRendererList(data.List);
                    builder.SetRenderAttachment(resources.activeColorTexture,0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.Read);
                    builder.SetRenderFunc(static (Data pass,RasterGraphContext context)=>context.cmd.DrawRendererList(pass.List));
                }
            }
        }
    }
}
