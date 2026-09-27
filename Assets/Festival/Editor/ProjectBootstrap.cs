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
    public static class ProjectBootstrap
    {
        public const string ScenePath="Assets/Festival/Generated/Bootstrap.unity";
        public const string PipelinePath="Assets/Festival/Generated/FestivalPipeline.asset";
        [MenuItem("Festival/Generate starter content")]
        public static void EnsureGeneratedContent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Generate outside Play Mode.");
            Directory.CreateDirectory("Assets/Festival/Generated");AssetDatabase.Refresh();
            PlayerSettings.companyName="Festival Co-op Team";
            PlayerSettings.productName="Festival Co-op Prototype";
            PlayerSettings.runInBackground=true;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.meshDeformation=MeshDeformation.GPU;
            // Input System Package only. The next batch invocation runs after this setting reloads.
            // ProjectSettings is not a normal AssetDatabase asset. Unity exposes
            // its serialized singleton to editor tooling for project bootstrap.
            var projectSettings=Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if(projectSettings==null)throw new InvalidOperationException("Unity PlayerSettings singleton is unavailable.");
            var serializedSettings=new SerializedObject(projectSettings);
            var inputHandler=serializedSettings.FindProperty("activeInputHandler");
            if(inputHandler==null)throw new InvalidOperationException("Unity active input setting is unavailable.");
            inputHandler.intValue=1;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if(pipeline==null)
            {
                var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer,"Assets/Festival/Generated/FestivalRenderer.asset");
                ResourceReloader.ReloadAllNullIn(renderer,"Packages/com.unity.render-pipelines.universal");
                pipeline=UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline,PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            // Shader.Find at runtime requires an explicit asset reference in a player build.
            if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Festival/Generated/Resources/FestivalLit.mat")==null)
            {
                Directory.CreateDirectory("Assets/Festival/Generated/Resources");AssetDatabase.Refresh();
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)throw new InvalidOperationException("URP Lit shader is unavailable; restore packages first.");
                AssetDatabase.CreateAsset(new Material(shader),"Assets/Festival/Generated/Resources/FestivalLit.mat");
            }
            const string skyPath="Assets/Festival/Generated/Resources/FestivalSky.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(skyPath)==null)
            {
                var sky=new Material(Shader.Find("Skybox/Procedural"));
                sky.SetColor("_SkyTint",new Color(.48f,.49f,.63f));
                sky.SetColor("_GroundColor",new Color(.2f,.18f,.22f));
                sky.SetFloat("_AtmosphereThickness",1.3f);sky.SetFloat("_Exposure",.8f);
                AssetDatabase.CreateAsset(sky,skyPath);
            }
            if(!File.Exists(ScenePath))
            {
                // Additive generation preserves open scenes and unsaved human work.
                var previous=SceneManager.GetActiveScene();
                // A fresh batch process has an untitled default scene which Unity
                // refuses to supplement additively. It contains no interactive work.
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);
                try
                {
                    var root=new GameObject("Festival Bootstrap");SceneManager.MoveGameObjectToScene(root,scene);
                    Add(root,"Festival.Network.FestivalSession");Add(root,"Festival.Presentation.FestivalWorld");Add(root,"Festival.Presentation.FestivalHud");
                    if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Could not save generated bootstrap scene.");
                }
                finally{EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);}
            }
            var scenes=EditorBuildSettings.scenes.ToList();
            int index=scenes.FindIndex(s=>s.path==ScenePath);
            if(index>=0)scenes.RemoveAt(index);
            scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Festival.Editor] Starter content ready. Open Assets/Festival/Generated/Bootstrap.unity.");
        }
        private static void Add(GameObject root,string name)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
            if(type==null)throw new InvalidOperationException("Required runtime component missing: "+name);
            root.AddComponent(type);
        }
    }
}
