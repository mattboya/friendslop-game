#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Festival.Presentation
{
    // Isolated native review harness; absent from release builds and gameplay scenes.
    public sealed class Production03Capture : MonoBehaviour
    {
        static readonly string[] Expressions={"Neutral","Blink","Delight","Alarm","Concern","Suspicious","Exhausted","Confused"};
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();string output=null;
            for(int i=0;i<args.Length-1;i++)if(args[i]=="--sample-output")output=args[i+1];
            if(string.IsNullOrEmpty(output))yield break;
            Directory.CreateDirectory(output);
            var camera=GetComponent<Camera>();var log=new StringBuilder();
            var cast=FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.parent==null&&t.name.StartsWith("Cast_",StringComparison.Ordinal)).OrderBy(t=>t.name).ToArray();
            log.AppendLine("cast-count="+cast.Length);
            foreach(var actor in cast)
            {
                var face=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name.EndsWith("_Face",StringComparison.Ordinal));
                var body=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name.Contains("_Body_"));
                var animator=actor.GetComponentInChildren<Animator>();
                var spine=actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Spine");
                if(face==null||body==null||animator==null||spine==null){log.AppendLine("ERROR missing rig/face/body "+actor.name);continue;}
                animator.Play("Idle",0,.25f);yield return null;var idle=spine.localRotation;
                animator.Play("Dance",0,.25f);yield return null;var dance=spine.localRotation;
                log.AppendLine($"motion {actor.name} idle-dance-spine-angle={Quaternion.Angle(idle,dance):F3} state={animator.GetCurrentAnimatorStateInfo(0).shortNameHash}");
                for(int pose=0;pose<Expressions.Length;pose++)
                {
                    for(int shape=0;shape<face.sharedMesh.blendShapeCount;shape++)face.SetBlendShapeWeight(shape,0);
                    int index=pose==0?-1:face.sharedMesh.GetBlendShapeIndex(Expressions[pose]);
                    if(pose>0&&index<0){log.AppendLine("ERROR missing "+Expressions[pose]+" "+actor.name);continue;}
                    if(index>=0)face.SetBlendShapeWeight(index,100);
                    float requested=index<0?0:face.GetBlendShapeWeight(index);
                    yield return null;yield return new WaitForEndOfFrame();
                    float observed=index<0?0:face.GetBlendShapeWeight(index);
                    log.AppendLine($"expression {actor.name} {Expressions[pose]} requested={requested:F1} observed-after-Animator={observed:F1}");
                    for(int distance=0;distance<2;distance++)
                    {
                        camera.fieldOfView=distance==0?34:50;
                        camera.transform.position=actor.position+(distance==0?new Vector3(0,1.95f,2.7f):new Vector3(0,2.4f,7.8f));
                        camera.transform.LookAt(actor.position+(distance==0?new Vector3(0,1.92f,0):new Vector3(0,1.15f,0)));
                        yield return new WaitForSeconds(.04f);yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(output,$"face-{actor.name.Substring(5)}-{Expressions[pose]}-{(distance==0?"conversation":"gameplay")}.png"));
                    }
                }
                animator.Play("Dance",0,.1f);
                int left=body.sharedMesh.GetBlendShapeIndex("GripL"),right=body.sharedMesh.GetBlendShapeIndex("GripR");
                if(left>=0)body.SetBlendShapeWeight(left,100);
                if(right>=0)body.SetBlendShapeWeight(right,100);
                yield return new WaitForSeconds(.25f);
                log.AppendLine($"grip {actor.name} left={(left>=0?body.GetBlendShapeWeight(left):-1):F1} right={(right>=0?body.GetBlendShapeWeight(right):-1):F1}");
                for(int shape=0;shape<face.sharedMesh.blendShapeCount;shape++)face.SetBlendShapeWeight(shape,0);
            }
            var fits=FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.parent==null&&t.name.StartsWith("Fit_",StringComparison.Ordinal)).OrderBy(t=>t.name).ToArray();
            foreach(var fit in fits)
            {
                camera.fieldOfView=43;camera.transform.position=fit.position+new Vector3(0,2.4f,5.2f);camera.transform.LookAt(fit.position+new Vector3(0,1.12f,0));
                yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"wardrobe-combination-"+fit.name+".png"));
                log.AppendLine("wardrobe-fit "+fit.name+" renderers="+fit.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled));
            }
            var selector=FindFirstObjectByType<Production03WardrobeSelector>();
            if(selector!=null)
            {
                selector.SetStyle("Headgear",-1);
                foreach(var slot in Production03WardrobeSelector.Categories)
                for(int style=0;style<10;style++)
                {
                    if(slot=="Hairstyle")selector.SetStyle("Headgear",-1);
                    selector.SetStyle(slot,style);
                    camera.fieldOfView=42;camera.transform.position=selector.transform.position+new Vector3(0,2.35f,4.8f);
                    camera.transform.LookAt(selector.transform.position+new Vector3(0,1.15f,0));
                    yield return new WaitForSeconds(.025f);yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,$"wardrobe-{slot}-{style:D2}.png"));
                    log.AppendLine($"wardrobe-slot {slot} {style} visible={selector.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(r=>r.enabled)}");
                }
            }
            camera.fieldOfView=50;camera.transform.position=new Vector3(0,4,17);camera.transform.LookAt(new Vector3(0,1,-5));
            yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"people-gallery.png"));
            yield return Measure(log,"people-gallery");
            for(int row=0;row<7;row++)
            {
                float z=-30-row*14;
                camera.fieldOfView=58;camera.transform.position=new Vector3(0,11,z+39);camera.transform.LookAt(new Vector3(0,1.6f,z));
                yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,$"world-row-{row}.png"));
            }
            foreach(var name in new[]{"Medical","Security","Shuttle","CampShop","PortaPotty","Stash","TreeA","LittleSpoon","ShuttleStop"})
            {
                var world=FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t=>t.parent==null&&t.name=="World_"+name);
                if(world==null)continue;
                float distance=name=="LittleSpoon"?1.2f:8f;
                camera.fieldOfView=48;camera.transform.position=world.position+new Vector3(0,name=="LittleSpoon"?1.0f:2.6f,distance);
                camera.transform.LookAt(world.position+new Vector3(0,name=="LittleSpoon" ? .45f : 1.7f,0));
                yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"world-"+name+"-front.png"));
                if(name=="Medical"||name=="Shuttle"||name=="Stash")
                {
                    camera.transform.position=world.position+new Vector3(7,2.7f,-5);camera.transform.LookAt(world.position+new Vector3(0,1.5f,0));
                    yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"world-"+name+"-rear-side.png"));
                }
            }
            camera.fieldOfView=68;camera.transform.position=new Vector3(0,60,35);camera.transform.LookAt(new Vector3(0,0,-55));
            yield return Measure(log,"world-overview");
            var active=FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r=>r.enabled);
            log.AppendLine($"active-renderers={active} allocated-mb={Profiler.GetTotalAllocatedMemoryLong()/1048576f:F1}");
            File.WriteAllText(Path.Combine(output,"native-observations.txt"),log.ToString());
            Debug.Log("[Festival.Production03] capture complete "+output);
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
        static IEnumerator Measure(StringBuilder log,string label)
        {
            float total=0,max=0;
            for(int i=0;i<180;i++){yield return null;float ms=Time.unscaledDeltaTime*1000;total+=ms;max=Mathf.Max(max,ms);}
            log.AppendLine($"frame-sample {label} frames=180 mean-ms={total/180:F2} max-ms={max:F2}");
        }
    }
}
#endif
