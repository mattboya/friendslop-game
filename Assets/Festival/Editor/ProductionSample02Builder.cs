using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Festival.Editor
{
    public static class ProductionSample02Builder
    {
        const string Folder="Assets/Festival/Art/ProductionSample02";
        const string ScenePath=Folder+"/SecondProductionSample.unity";
        [Serializable] sealed class Palette { public Entry[] entries; }
        [Serializable] sealed class Entry { public string name; public float[] rgb; public float roughness,metallic,emission; }

        [MenuItem("Festival/Create second production sample")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Create outside Play Mode.");
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("URP Lit is required.");
            Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
            var palette=JsonUtility.FromJson<Palette>(File.ReadAllText(Folder+"/palette.json"));
            foreach(var entry in palette.entries)
            {
                var path=Folder+"/Materials/"+entry.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                mat.shader=shader;mat.color=new Color(entry.rgb[0],entry.rgb[1],entry.rgb[2]);
                mat.SetFloat("_Smoothness",1-entry.roughness);mat.SetFloat("_Metallic",entry.metallic);
                if(entry.emission>0)
                {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",mat.color*entry.emission);}
                else {mat.DisableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Color.black);}
                mat.enableInstancing=true;EditorUtility.SetDirty(mat);
            }
            foreach(var file in Directory.GetFiles(Folder,"AH02_*.fbx"))
            {
                var path=file.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.animationType=ModelImporterAnimationType.None;importer.importAnimation=false;
                importer.addCollider=false;importer.importCameras=false;importer.importLights=false;
                importer.SaveAndReimport();
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(var source in model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
                {
                    var replacement=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+source.name+".mat");
                    if(replacement!=null)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),replacement);
                }
                importer.SaveAndReimport();
            }
            var old=SceneManager.GetActiveScene();
            var mode=string.IsNullOrEmpty(old.path)?NewSceneMode.Single:NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                Place("Stage",Vector3.zero);
                Place("SpeakerStack",new Vector3(-9.9f,0,0));Place("SpeakerStack",new Vector3(9.9f,0,0));
                Place("DJStation",new Vector3(0,1.4f,-.1f));
                Place("FlightCase",new Vector3(-5.58f,1.4f,-.3f));
                Place("SunTotem",new Vector3(-6.66f,0,7.3f));Place("MoonTotem",new Vector3(6.66f,0,7.3f));
                var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Review ground";
                ground.transform.localScale=new Vector3(4,1,3);
                ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/AH02_Ground.mat");
                var sun=new GameObject("Festival dusk sun").AddComponent<Light>();sun.type=LightType.Directional;
                sun.color=new Color(1,.67f,.48f);sun.intensity=.82f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(42,-35,0);
                RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.58f,.64f,.76f);
                RenderSettings.ambientEquatorColor=new Color(.45f,.47f,.54f);
                RenderSettings.ambientGroundColor=new Color(.29f,.26f,.30f);
                RenderSettings.skybox=Resources.Load<Material>("FestivalSky");
                var camera=new GameObject("Sample Camera").AddComponent<Camera>();camera.tag="MainCamera";
                camera.transform.position=new Vector3(0,7,23);camera.transform.LookAt(new Vector3(0,3,0));
                camera.fieldOfView=52;camera.nearClipPlane=.05f;camera.farClipPlane=100;
                camera.gameObject.AddComponent<AudioListener>();
                camera.gameObject.AddComponent<Festival.Presentation.ProductionSample02Capture>();
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                Debug.Log("[Festival.ProductionSample02] sample scene created at "+ScenePath);
            }
            finally { if(old.IsValid()&&old.isLoaded){SceneManager.SetActiveScene(old);EditorSceneManager.CloseScene(scene,true);} }
        }
        static GameObject Place(string name,Vector3 position)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/AH02_"+name+".fbx");
            if(asset==null)throw new InvalidOperationException("Missing AH02_"+name);
            var root=new GameObject(name);root.transform.position=position;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);model.transform.localPosition=Vector3.zero;
            return root;
        }
        public static void AuditAndBuild()
        {
            Create();
            const string output="artifacts/second-production-sample";Directory.CreateDirectory(output);
            var report=new StringBuilder();
            foreach(var name in new[]{"Stage","SpeakerStack","DJStation","FlightCase","SunTotem","MoonTotem"})
            {
                var path=Folder+"/AH02_"+name+".fbx";
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderers=model.GetComponentsInChildren<Renderer>(true);
                var meshes=model.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).Where(m=>m!=null).ToArray();
                report.AppendLine($"{name}: renderers={renderers.Length} triangles={meshes.Sum(m=>m.triangles.Length/3)} localScale={model.transform.localScale}");
                foreach(var r in renderers)report.AppendLine($"  renderer={r.name} bounds={r.bounds} materials={string.Join(",",r.sharedMaterials.Select(m=>m==null?"NULL":m.name+":"+m.shader.name))}");
                foreach(var t in model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Platter")||t.name.Contains("Fader")||t.name.Contains("Pad")||t.name.Contains("EQ_")||t.name.Contains("Anchor")||t.name.Contains("Contact")||t.name.Contains("PerformerFeet")))
                    report.AppendLine($"  control={t.name} local={t.localPosition} children={t.childCount} renderers={t.GetComponentsInChildren<Renderer>(true).Length}");
            }
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var roots=scene.GetRootGameObjects();var sceneRenderers=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).ToArray();
            report.AppendLine($"scene: renderers={sceneRenderers.Length} triangles={sceneRenderers.Sum(r=>r.GetComponent<MeshFilter>()?.sharedMesh.triangles.Length/3??0)} lights={roots.Sum(r=>r.GetComponentsInChildren<Light>(true).Length)}");
            var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach(var t in all.Where(t=>t.name.Contains("HandContact")||t.name.Contains("PerformerFeet")||t.name.Contains("PrivateClueAnchor")||t.name.Contains("InteractAnchor")))
                report.AppendLine($"scene-anchor {t.name}: world={t.position}");
            foreach(var name in new[]{"AH02_PlatterLeft","AH02_ChannelFader0","AH02_LeftPad0","AH02_EQ_0_0"})
            {
                var pivot=all.FirstOrDefault(t=>t.name==name);
                if(pivot==null)throw new Exception("Missing controller pivot "+name);
                var filter=pivot.GetComponentInChildren<MeshFilter>();
                if(filter==null)throw new Exception("Pivot has no geometry "+name);
                var vertex=filter.sharedMesh.vertices.OrderByDescending(v=>v.x*v.x+v.z*v.z).First();
                var before=filter.transform.TransformPoint(vertex);
                var originalPosition=pivot.localPosition;var originalRotation=pivot.localRotation;
                if(name.Contains("Platter")||name.Contains("EQ_"))pivot.localRotation=originalRotation*Quaternion.Euler(0,30,0);
                else pivot.localPosition=originalPosition+new Vector3(0,.05f,0);
                var delta=Vector3.Distance(before,filter.transform.TransformPoint(vertex));
                report.AppendLine($"pivot-motion {name}: moved-vertex-metres={delta:F4}");
                if(delta<.01f)throw new Exception("Controller geometry did not follow pivot "+name);
                pivot.localPosition=originalPosition;pivot.localRotation=originalRotation;
            }
            File.WriteAllText(output+"/import-audit.txt",report.ToString());
            var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{ScenePath},locationPathName="Builds/macOS/ProductionSample02/SecondProductionSample.app",
                target=BuildTarget.StandaloneOSX,options=BuildOptions.Development|BuildOptions.AllowDebugging
            });
            if(build.summary.result!=BuildResult.Succeeded)throw new Exception("AH02 build: "+build.summary.result);
            Debug.Log("[Festival.ProductionSample02] native sample build passed");
        }
    }
}
