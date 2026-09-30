using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Festival.Editor
{
    public static class ProductionSampleAudit
    {
        public static void AuditAndBuild()
        {
            ProductionSampleBuilder.Create();
            var output="artifacts/first-production-sample";
            Directory.CreateDirectory(output);
            var report=new StringBuilder();
            foreach(var name in new[]{"Attendee","Vendor","Stall","PoiPractice","PoiLED","Path"})
            {
                var path=ProductionSampleBuilder.Folder+"/AH01_"+name+".fbx";
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                var renderers=model.GetComponentsInChildren<Renderer>(true);
                var meshes=model.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh)
                    .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(m=>m.sharedMesh))
                    .Where(m=>m!=null).ToArray();
                report.AppendLine($"{name}: renderers={renderers.Length} meshes={meshes.Length} triangles={meshes.Sum(m=>m.triangles.Length/3)} blendshapes={meshes.Sum(m=>m.blendShapeCount)} animationType={importer.animationType}");
                foreach(var renderer in renderers)
                {
                    var mats=string.Join(",",renderer.sharedMaterials.Select(m=>m==null?"NULL":m.name+":"+m.shader.name));
                    report.AppendLine($"  renderer={renderer.name} bounds={renderer.bounds} materials={mats}");
                    if(renderer is SkinnedMeshRenderer skin)
                    {
                        for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                            report.AppendLine($"    shape={skin.sharedMesh.GetBlendShapeName(i)} default={skin.GetBlendShapeWeight(i)}");
                    }
                }
                foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")))
                    report.AppendLine($"  clip={clip.name} length={clip.length:F2} loop={clip.isLooping}");
            }
            var scene=EditorSceneManager.OpenScene(ProductionSampleBuilder.ScenePath);
            var sceneRenderers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).ToArray();
            report.AppendLine($"scene: renderers={sceneRenderers.Length} triangles={sceneRenderers.Sum(r=>r is SkinnedMeshRenderer s?s.sharedMesh.triangles.Length/3:r.GetComponent<MeshFilter>()?.sharedMesh.triangles.Length/3??0)} lights={scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Light>(true).Length)}");
            File.WriteAllText(output+"/import-audit.txt",report.ToString());
            Debug.Log("[Festival.ProductionSample] import audit written to "+output);
            var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{ProductionSampleBuilder.ScenePath},
                locationPathName="Builds/macOS/ProductionSample/FirstProductionSample.app",
                target=BuildTarget.StandaloneOSX,
                options=BuildOptions.Development|BuildOptions.AllowDebugging
            });
            if(build.summary.result!=BuildResult.Succeeded)throw new Exception("Production sample build: "+build.summary.result);
            Debug.Log("[Festival.ProductionSample] native sample build passed");
        }
    }
}
