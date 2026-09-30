using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Festival.Editor
{
    public static class Production03Builder
    {
        const string People="Assets/Festival/Art/ProductionPeople03";
        const string World="Assets/Festival/Art/ProductionSample03";
        const string ScenePath="Assets/Festival/Art/ProductionPeople03/ThirdProductionSample.unity";
        static readonly string[] Cast={"AttendeeLanky","AttendeeAverage","AttendeeStocky","VendorSupplies","VendorPerformance","VendorStock","Medic","Security","MissingFriend"};
        static readonly string[] Fits={"Fit_0_0","Fit_0_1","Fit_0_2","Fit_1_0","Fit_1_1","Fit_1_2"};
        static readonly string[] WorldNames={"Medical","Security","Shuttle","StallSupplies","StallStock","CampShop","Tent","DomeTent","CampCar","CampVan","CampShade","PortaPotty","TreeA","TreeB","TreeFir","GroveDetail","LittleSpoon","Confetti","MerchBag","Map","StagePass","Stock","Voucher","Stash","Bench","CrowdBarrier","RecyclingBin","WayfindingPost","StringLights","WaterBottle","RecoveryWristband","ShuttleStop"};

        public static void CreateAndBuild()
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(People+"/AH03P_Motion.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            if(clips.Length!=6)throw new Exception("Expected six motion clips, found "+clips.Length);
            var controllerPath=People+"/ThirdProductionMotion.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if(controller.layers.Length==0)controller.AddLayer("Base Layer");
            var sm=controller.layers[0].stateMachine;
            foreach(var clip in clips)
            {
                var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==clip.name);
                if(state==null)state=sm.AddState(clip.name);
                state.motion=clip;if(clip.name=="Idle")sm.defaultState=state;
            }
            var old=SceneManager.GetActiveScene();
            var mode=string.IsNullOrEmpty(old.path)?NewSceneMode.Single:NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);SceneManager.SetActiveScene(scene);
            try
            {
                for(int i=0;i<Cast.Length;i++)
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(People+"/AH03P_"+Cast[i]+".fbx");
                    if(asset==null)throw new Exception("Missing cast "+Cast[i]);
                    var root=new GameObject("Cast_"+Cast[i]);root.transform.position=new Vector3((i%3-1)*3.2f,0,-(i/3)*3.3f);
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);
                    model.transform.localPosition=Vector3.zero;
                    var rig=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="FestivalRig");
                    if(rig==null)throw new Exception("Cast rig missing "+Cast[i]);
                    var animator=rig.GetComponent<Animator>();if(animator==null)animator=rig.gameObject.AddComponent<Animator>();
                    animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                }
                var wardrobe=AssetDatabase.LoadAssetAtPath<GameObject>(People+"/AH03W_WardrobeLibrary.fbx");
                var master=Resources.Load<GameObject>("FestivalCharacter");
                if(wardrobe==null||master==null)throw new Exception("Wardrobe or base character master missing");
                var parts=wardrobe.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for(int fit=0;fit<6;fit++)
                {
                    int gender=fit/3,shape=fit%3;
                    var root=new GameObject("Fit_"+gender+"_"+shape);root.transform.position=new Vector3((fit-2.5f)*2.65f,0,-13);
                    var baseModel=(GameObject)PrefabUtility.InstantiatePrefab(master,root.transform);
                    baseModel.transform.localPosition=Vector3.zero;
                    var bones=baseModel.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"Hips","Spine","Head","ArmL","ArmR","ForearmL","ForearmR","HandL","HandR","LegL","LegR","ShinL","ShinR","FootL","FootR"}.Contains(t.name)).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                    foreach(var r in baseModel.GetComponentsInChildren<Renderer>(true))
                    {
                        r.enabled=r.name=="Body_"+gender+"_"+shape || r.name=="Face_"+gender+"_0";
                    }
                    foreach(var slot in new[]{"Headgear","Sunglasses","Shirt","Pants","Shoes","FacialHair","Hairstyle","Accessory"})
                    {
                        int index=slot=="Headgear"?fit%2==0?8:9:slot=="Sunglasses"?fit%2==0?8:9:slot=="Shirt"?5+fit%5:slot=="Pants"?5+fit%5:slot=="Shoes"?4+fit%6:slot=="FacialHair"?5+fit%5:slot=="Hairstyle"?6+fit%4:4+fit%6;
                        var selected=parts.FirstOrDefault(r=>r.name=="AH03W_"+slot+"_"+index);
                        if(selected==null)throw new Exception("Missing wardrobe "+slot+"_"+index);
                        ClonePart(selected,root.transform,bones,Fits[fit]);
                        if(slot=="Hairstyle" && fit%2==1)
                        {
                            var hair=parts.FirstOrDefault(r=>r.name=="AH03W_HairTop_"+index);
                            if(hair!=null)ClonePart(hair,root.transform,bones,Fits[fit]);
                        }
                    }
                    if(fit%2==0)
                    {
                        var hair=parts.FirstOrDefault(r=>r.name=="AH03W_HairUnderHat");
                        if(hair!=null)ClonePart(hair,root.transform,bones,Fits[fit]);
                    }
                    var rig=baseModel.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="FestivalRig");
                    if(rig!=null)
                    {
                        var animator=rig.GetComponent<Animator>();if(animator==null)animator=rig.gameObject.AddComponent<Animator>();
                        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                    }
                }
                {
                    var root=new GameObject("WardrobeSelector");root.transform.position=new Vector3(0,0,-20);
                    var baseModel=(GameObject)PrefabUtility.InstantiatePrefab(master,root.transform);baseModel.transform.localPosition=Vector3.zero;
                    foreach(var r in baseModel.GetComponentsInChildren<Renderer>(true))r.enabled=r.name=="Body_0_1"||r.name=="Face_0_0";
                    var bones=baseModel.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"Hips","Spine","Head","ArmL","ArmR","ForearmL","ForearmR","HandL","HandR","LegL","LegR","ShinL","ShinR","FootL","FootR"}.Contains(t.name)).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                    foreach(var source in parts)
                    {
                        var copy=ClonePart(source,root.transform,bones,"Fit_0_1");copy.enabled=false;
                    }
                    root.AddComponent<Festival.Presentation.Production03WardrobeSelector>();
                }
                for(int i=0;i<WorldNames.Length;i++)
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(World+"/AH03_"+WorldNames[i]+".fbx");
                    if(asset==null)throw new Exception("Missing world asset "+WorldNames[i]);
                    var root=new GameObject("World_"+WorldNames[i]);root.transform.position=new Vector3((i%5-2)*12,0,-30-(i/5)*14);
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);
                    model.transform.localPosition=Vector3.zero;
                }
                var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Review ground";
                ground.transform.position=new Vector3(0,-.05f,-55);ground.transform.localScale=new Vector3(12,1,14);
                var groundMat=AssetDatabase.LoadAssetAtPath<Material>(World+"/Materials/AH03_Ground.mat");if(groundMat!=null)ground.GetComponent<Renderer>().sharedMaterial=groundMat;
                var sun=new GameObject("Festival dusk sun").AddComponent<Light>();sun.type=LightType.Directional;
                sun.color=new Color(1,.67f,.48f);sun.intensity=.82f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(42,-35,0);
                RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.58f,.64f,.76f);RenderSettings.ambientEquatorColor=new Color(.45f,.47f,.54f);RenderSettings.ambientGroundColor=new Color(.29f,.26f,.30f);
                RenderSettings.skybox=Resources.Load<Material>("FestivalSky");
                var camera=new GameObject("Sample Camera").AddComponent<Camera>();camera.tag="MainCamera";
                camera.transform.position=new Vector3(0,4,17);camera.transform.LookAt(new Vector3(0,1,-5));camera.fieldOfView=50;
                camera.nearClipPlane=.05f;camera.farClipPlane=200;camera.gameObject.AddComponent<AudioListener>();
                camera.gameObject.AddComponent<Festival.Presentation.Production03Capture>();
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            }
            finally { if(old.IsValid()&&old.isLoaded){SceneManager.SetActiveScene(old);EditorSceneManager.CloseScene(scene,true);} }
            var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{ScenePath},locationPathName="Builds/macOS/ProductionSample03/ThirdProductionSample.app",
                target=BuildTarget.StandaloneOSX,options=BuildOptions.Development|BuildOptions.AllowDebugging
            });
            if(build.summary.result!=BuildResult.Succeeded)throw new Exception("AH03 build: "+build.summary.result);
            Debug.Log("[Festival.Production03] native sample build passed");
        }
        static SkinnedMeshRenderer ClonePart(SkinnedMeshRenderer source,Transform parent,System.Collections.Generic.Dictionary<string,Transform> bones,string fit)
        {
            var go=(GameObject)UnityEngine.Object.Instantiate(source.gameObject,parent);
            go.name=source.name;go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            var r=go.GetComponent<SkinnedMeshRenderer>();
            r.bones=source.bones.Select(b=>bones.TryGetValue(b.name,out var target)?target:null).ToArray();
            if(r.bones.Any(b=>b==null))throw new Exception("Wardrobe bone mapping incomplete: "+source.name);
            r.rootBone=bones["Hips"];
            for(int i=0;i<r.sharedMesh.blendShapeCount;i++)r.SetBlendShapeWeight(i,r.sharedMesh.GetBlendShapeName(i)==fit?100:0);
            return r;
        }
    }
}
