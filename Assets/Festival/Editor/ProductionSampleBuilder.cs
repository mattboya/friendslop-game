using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Festival.Editor
{
    public static class ProductionSampleBuilder
    {
        public const string Folder="Assets/Festival/Art/ProductionSample";
        public const string ScenePath=Folder+"/FirstProductionSample.unity";
        [Serializable] sealed class Palette { public Entry[] entries; }
        [Serializable] sealed class Entry { public string name; public float[] rgb; public float roughness; }

        [MenuItem("Festival/Create first production sample")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Create the sample outside Play Mode.");
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("The existing URP Lit shader is required.");
            Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
            var palette=JsonUtility.FromJson<Palette>(File.ReadAllText(Folder+"/palette.json"));
            foreach(var entry in palette.entries)
            {
                var path=Folder+"/Materials/"+entry.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                mat.shader=shader;mat.color=new Color(entry.rgb[0],entry.rgb[1],entry.rgb[2]);
                mat.SetFloat("_Smoothness",1-entry.roughness);
                mat.SetFloat("_Metallic",entry.name.EndsWith("Metal",StringComparison.Ordinal)?.55f:0);
                if(entry.name.EndsWith("Glow",StringComparison.Ordinal))
                {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",mat.color*2);}
                mat.enableInstancing=true;EditorUtility.SetDirty(mat);
            }
            foreach(var file in Directory.GetFiles(Folder,"AH01_*.fbx"))
            {
                var path=file.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                bool actor=path.Contains("Attendee")||path.Contains("Vendor");
                importer.animationType=actor?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;
                importer.importAnimation=actor;importer.importBlendShapes=true;importer.addCollider=false;
                importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(var source in model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
                {
                    var replacement=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+source.name+".mat");
                    if(replacement!=null)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),replacement);
                }
                if(actor)
                {
                    var clips=importer.defaultClipAnimations;
                    foreach(var clip in clips)clip.loopTime=true;
                    importer.clipAnimations=clips;
                }
                importer.SaveAndReimport();
            }
            var old=SceneManager.GetActiveScene();
            // Batch mode starts in an unsaved untitled scene, which Unity cannot keep open additively.
            var mode=string.IsNullOrEmpty(old.path)?NewSceneMode.Single:NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                Place("Stall",Vector3.zero,Vector3.one);
                Place("Path",new Vector3(0,0,3.8f),Vector3.one);
                var attendee=Place("Attendee",new Vector3(-1.4f,0,1.55f),new Vector3(.94f,1.04f,.94f));
                var vendor=Place("Vendor",new Vector3(.4f,0,-.65f),new Vector3(1.08f,.97f,1.08f));
                Animate(attendee,"Attendee","Dance");Animate(vendor,"Vendor","Welcome");
                foreach(var style in new[]{"Practice","LED"})
                for(int side=0;side<2;side++)
                {
                    float x=style=="Practice"?-1.55f:-.90f;
                    var poi=Place("Poi"+style,new Vector3(x+side*.23f,2.38f,1.18f),Vector3.one);
                    poi.transform.rotation=Quaternion.Euler(180,0,0);
                }
                var sun=new GameObject("Festival dusk sun").AddComponent<Light>();sun.type=LightType.Directional;
                sun.color=new Color(1,.67f,.48f);sun.intensity=.82f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(42,-35,0);
                RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.58f,.64f,.76f);
                RenderSettings.ambientEquatorColor=new Color(.45f,.47f,.54f);
                RenderSettings.ambientGroundColor=new Color(.29f,.26f,.30f);
                RenderSettings.skybox=Resources.Load<Material>("FestivalSky");
                for(int side=-1;side<=1;side+=2)
                {
                    var lamp=new GameObject("Stall practical light").AddComponent<Light>();lamp.type=LightType.Point;
                    lamp.transform.position=new Vector3(side*1.7f,2.40f,1.64f);lamp.color=new Color(1,.66f,.25f);lamp.intensity=1.5f;lamp.range=6;
                }
                var camera=new GameObject("Sample Camera").AddComponent<Camera>();camera.tag="MainCamera";
                camera.transform.position=new Vector3(7,4.5f,10);camera.transform.LookAt(new Vector3(0,1.4f,0));camera.fieldOfView=48;
                camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.backgroundColor=new Color(.12f,.17f,.24f);
                camera.gameObject.AddComponent<AudioListener>();
                camera.gameObject.AddComponent<Festival.Presentation.ProductionSampleCapture>();
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                Debug.Log("[Festival.ProductionSample] Authored sample scene created at "+ScenePath+". No tests or native acceptance performed.");
            }
            finally { if(old.IsValid() && old.isLoaded) { SceneManager.SetActiveScene(old);EditorSceneManager.CloseScene(scene,true); } }
        }
        static GameObject Place(string name,Vector3 position,Vector3 scale)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/AH01_"+name+".fbx");
            if(asset==null)throw new InvalidOperationException("Missing production sample "+name);
            var root=new GameObject(name);root.transform.position=position;root.transform.localScale=scale;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);model.transform.localPosition=Vector3.zero;
            return root;
        }
        static void Animate(GameObject root,string name,string preferred)
        {
            var model=root.transform.GetChild(0).gameObject;
            var clips=AssetDatabase.LoadAllAssetsAtPath(Folder+"/AH01_"+name+".fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__",StringComparison.Ordinal)).ToArray();
            if(clips.Length==0){Debug.LogWarning("[Festival.ProductionSample] No imported clips for "+name);return;}
            var path=Folder+"/"+name+".controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            if(controller.layers.Length==0)controller.AddLayer("Base Layer");
            var sm=controller.layers[0].stateMachine;
            foreach(var clip in clips)
            {
                var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==clip.name)??sm.AddState(clip.name);
                state.motion=clip;if(clip.name.Contains(preferred))sm.defaultState=state;
            }
            var animator=model.GetComponent<Animator>();
            if(animator==null)animator=model.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            EditorUtility.SetDirty(controller);
        }
    }
}
