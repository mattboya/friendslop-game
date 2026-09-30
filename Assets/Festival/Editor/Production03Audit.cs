using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Festival.Editor
{
    public static class Production03Audit
    {
        const string World="Assets/Festival/Art/ProductionSample03";
        const string People="Assets/Festival/Art/ProductionPeople03";
        [Serializable] sealed class Palette { public Entry[] entries; }
        [Serializable] sealed class Entry { public string name; public float[] rgb; public float roughness,metallic,emission; }

        public static void ImportAndAudit()
        {
            Directory.CreateDirectory("artifacts/third-production-sample");
            foreach(var folder in new[]{World,People})Directory.CreateDirectory(folder+"/Materials");
            AssetDatabase.Refresh();
            MakeMaterials(World,World+"/palette.json");
            MakeMaterials(People,People+"/palette.json");
            MakeMaterials(People,People+"/wardrobe-palette.json");
            var report=new StringBuilder();
            var files=Directory.GetFiles(World,"AH03_*.fbx").Concat(Directory.GetFiles(People,"AH03*.fbx")).OrderBy(p=>p).ToArray();
            foreach(var file in files)
            {
                var path=file.Replace('\\','/');
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                bool motion=path.EndsWith("AH03P_Motion.fbx",StringComparison.Ordinal);
                bool cast=path.Contains("AH03P_")&&!motion;
                bool wardrobe=path.Contains("AH03W_");
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.animationType=motion||cast||wardrobe?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;
                importer.importAnimation=motion;
                importer.importBlendShapes=true;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;
                importer.SaveAndReimport();
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var folder=path.StartsWith(World,StringComparison.Ordinal)?World:People;
                foreach(var source in model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
                {
                    var replacement=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Materials/"+source.name+".mat");
                    if(replacement!=null)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),replacement);
                }
                importer.SaveAndReimport();
                model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderers=model.GetComponentsInChildren<Renderer>(true);
                var skinned=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var filters=model.GetComponentsInChildren<MeshFilter>(true);
                var meshes=skinned.Select(s=>s.sharedMesh).Concat(filters.Select(f=>f.sharedMesh)).Where(m=>m!=null).ToArray();
                report.AppendLine($"ASSET {Path.GetFileName(path)} renderers={renderers.Length} skinned={skinned.Length} triangles={meshes.Sum(m=>m.triangles.Length/3)} shapes={meshes.Sum(m=>m.blendShapeCount)} type={importer.animationType}");
                foreach(var r in renderers)
                {
                    var mesh=r is SkinnedMeshRenderer s?s.sharedMesh:r.GetComponent<MeshFilter>()?.sharedMesh;
                    report.AppendLine($"  R {r.name} bounds={r.bounds} materials={string.Join(",",r.sharedMaterials.Select(m=>m==null?"NULL":m.name+":"+m.shader.name))} normals={mesh?.normals.Length??0}/{mesh?.vertexCount??0}");
                    if(r is SkinnedMeshRenderer skin)
                    {
                        report.AppendLine($"    bones={skin.bones.Length} rootBone={skin.rootBone?.name??"NULL"} shapes={string.Join(",",Enumerable.Range(0,skin.sharedMesh.blendShapeCount).Select(i=>skin.sharedMesh.GetBlendShapeName(i)+":"+skin.GetBlendShapeWeight(i)))}");
                    }
                }
                if(motion||cast)
                {
                    report.AppendLine("  hierarchy="+string.Join("/",model.GetComponentsInChildren<Transform>(true).Take(18).Select(t=>t.name)));
                    var animator=model.GetComponent<Animator>();
                    report.AppendLine("  avatar="+(animator==null||animator.avatar==null?"NULL":animator.avatar.name));
                }
                foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")))
                {
                    var curves=AnimationUtility.GetCurveBindings(clip);
                    report.AppendLine($"  CLIP {clip.name} length={clip.length:F2} curves={curves.Length} shapeCurves={curves.Count(c=>c.propertyName.Contains("blendShape"))}");
                    foreach(var binding in curves.Take(12))report.AppendLine($"    C {binding.path} {binding.type.Name} {binding.propertyName}");
                }
            }
            File.WriteAllText("artifacts/third-production-sample/import-audit.txt",report.ToString());
            Debug.Log("[Festival.Production03] import audit saved, assets="+files.Length);
        }
        public static void AuditPivots()
        {
            Directory.CreateDirectory("artifacts/third-production-sample");
            var output=new StringBuilder();
            foreach(var item in new[]{
                (file:"AH03_Shuttle.fbx",node:"AH03_DoorLeft",axis:Vector3.up),
                (file:"AH03_Shuttle.fbx",node:"AH03_DoorRight",axis:Vector3.up),
                (file:"AH03_Stash.fbx",node:"AH03_StashLid",axis:Vector3.right),
                (file:"AH03_RecoveryWristband.fbx",node:"AH03_WristSocket",axis:Vector3.up)})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(World+"/"+item.file);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var pivot=instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==item.node);
                    if(pivot==null){output.AppendLine($"{item.file} {item.node} MISSING");continue;}
                    var renderers=pivot.GetComponentsInChildren<Renderer>(true);
                    var before=renderers.Select(r=>r.bounds.center).ToArray();
                    pivot.localRotation=Quaternion.AngleAxis(30,item.axis)*pivot.localRotation;
                    var displacement=renderers.Select((r,i)=>Vector3.Distance(before[i],r.bounds.center)).DefaultIfEmpty(0).Max();
                    output.AppendLine($"{item.file} {item.node} local={pivot.localPosition:F3} renderers={renderers.Length} max-center-move-30deg={displacement:F3}m");
                }
                finally{UnityEngine.Object.DestroyImmediate(instance);}
            }
            File.WriteAllText("artifacts/third-production-sample/pivot-audit.txt",output.ToString());
            Debug.Log("[Festival.Production03] pivot audit saved");
        }
        static void MakeMaterials(string folder,string palettePath)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("URP Lit missing");
            var palette=JsonUtility.FromJson<Palette>(File.ReadAllText(palettePath));
            foreach(var entry in palette.entries)
            {
                var path=folder+"/Materials/"+entry.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                mat.shader=shader;mat.color=new Color(entry.rgb[0],entry.rgb[1],entry.rgb[2]);
                mat.SetFloat("_Smoothness",1-entry.roughness);mat.SetFloat("_Metallic",entry.metallic);
                if(entry.emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",mat.color*entry.emission);}
                else {mat.DisableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Color.black);}
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
