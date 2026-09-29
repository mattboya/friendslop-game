using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Festival.Editor
{
    public static class ProjectValidation
    {
        [MenuItem("Festival/Validate project")]
        public static void Validate()
        {
            if(!File.Exists(ProjectBootstrap.ScenePath))throw new InvalidOperationException("Generate starter content first.");
            if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Festival/Generated/Resources/FestivalLit.mat")==null)throw new InvalidOperationException("Runtime Lit material is missing; generate starter content.");
            if(GraphicsSettings.defaultRenderPipeline==null)throw new InvalidOperationException("No render pipeline configured.");
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Festival/Generated/FestivalRenderer.asset");
            if(renderer==null||renderer.postProcessData==null)
                throw new InvalidOperationException("Festival renderer is missing post-processing resources; regenerate content before building.");
            if(renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Count(feature=>feature.isActive)!=1)
                throw new InvalidOperationException("Festival renderer must have one active contact-shading feature.");
            if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==ProjectBootstrap.ScenePath))throw new InvalidOperationException("Bootstrap is not enabled in build scenes.");
            var scene=SceneManager.GetSceneByPath(ProjectBootstrap.ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(ProjectBootstrap.ScenePath,OpenSceneMode.Additive);
            try
            {
                var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).ToArray();
                if(components.Any(c=>c==null))throw new InvalidOperationException("Bootstrap has missing scripts.");
                foreach(var name in new[]{"Festival.Network.FestivalSession","Festival.Presentation.FestivalWorld","Festival.Presentation.FestivalHud"})
                    if(components.Count(c=>c.GetType().FullName==name)!=1)throw new InvalidOperationException("Bootstrap needs exactly one "+name);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            Debug.Log("FESTIVAL VALIDATION PASSED");
        }
    }
}
