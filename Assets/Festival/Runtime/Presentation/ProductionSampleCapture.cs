#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace Festival.Presentation
{
    // Isolated review-scene harness. It is never attached to the gameplay scene.
    public sealed class ProductionSampleCapture : MonoBehaviour
    {
        IEnumerator Start()
        {
            var args=System.Environment.GetCommandLineArgs();
            string output=null;
            for(int i=0;i<args.Length-1;i++)if(args[i]=="--sample-output")output=args[i+1];
            if(string.IsNullOrEmpty(output))yield break;
            Directory.CreateDirectory(output);
            var camera=GetComponent<Camera>();
            var attendee=GameObject.Find("Attendee");
            var vendor=GameObject.Find("Vendor");
            var views=new[] {
                new Vector3(7,4.5f,10), new Vector3(0,2.2f,6.5f),
                new Vector3(-5,2.5f,2), new Vector3(4,2.5f,-4),
                new Vector3(-1.1f,1.65f,3.9f)
            };
            string[] names={"overview","front","left","back","conversation"};
            for(int i=0;i<views.Length;i++)
            {
                camera.transform.position=views[i];
                camera.transform.LookAt(i==4?new Vector3(-.5f,1.45f,.3f):new Vector3(0,1.3f,.1f));
                yield return new WaitForSeconds(1f);
                yield return new WaitForEndOfFrame();
                var path=Path.Combine(output,names[i]+".png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log("[Festival.ProductionSample] capture "+path);
            }
            var a=attendee.GetComponentInChildren<Animator>();
            var v=vendor.GetComponentInChildren<Animator>();
            a.Play("Idle",0,0);v.Play("Offer",0,0);
            camera.transform.position=new Vector3(0,2.2f,6.5f);
            camera.transform.LookAt(new Vector3(0,1.3f,0));
            yield return new WaitForSeconds(.8f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"idle-offer.png"));
            a.Play("Dance",0,0);v.Play("Welcome",0,0);
            yield return new WaitForSeconds(.8f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"dance-welcome.png"));
            for(int frame=0;frame<24;frame++)
            {
                yield return new WaitForSeconds(.083f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,$"motion-{frame:D2}.png"));
            }
            foreach(var actor in new[]{attendee,vendor})
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            for(int shape=0;shape<skin.sharedMesh.blendShapeCount;shape++)
                if(skin.sharedMesh.GetBlendShapeName(shape)=="Delight"||skin.sharedMesh.GetBlendShapeName(shape)=="Blink")
                    skin.SetBlendShapeWeight(shape,100);
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"expressions-100.png"));
            float total=0,max=0;
            for(int i=0;i<180;i++)
            {
                yield return null;
                float ms=Time.unscaledDeltaTime*1000;
                total+=ms;max=Mathf.Max(max,ms);
            }
            var perf=$"frames=180 mean_ms={total/180:F2} max_ms={max:F2} allocated_mb={Profiler.GetTotalAllocatedMemoryLong()/1048576f:F1}";
            File.WriteAllText(Path.Combine(output,"runtime-metrics.txt"),perf+"\n");
            Debug.Log("[Festival.ProductionSample] "+perf);
            Application.Quit();
        }
    }
}
#endif
