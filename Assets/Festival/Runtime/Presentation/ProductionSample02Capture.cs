#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace Festival.Presentation
{
    // Attached only to the isolated AH02 review scene.
    public sealed class ProductionSample02Capture : MonoBehaviour
    {
        IEnumerator Start()
        {
            var args=System.Environment.GetCommandLineArgs();string output=null;
            for(int i=0;i<args.Length-1;i++)if(args[i]=="--sample-output")output=args[i+1];
            if(string.IsNullOrEmpty(output))yield break;
            Directory.CreateDirectory(output);
            var camera=GetComponent<Camera>();
            var views=new[] {
                new Vector3(0,7,23),new Vector3(0,2,16),new Vector3(-14,5,4),
                new Vector3(0,6,-17),new Vector3(0,4.3f,3.1f),new Vector3(-6.66f,2.2f,12),new Vector3(6.66f,2.2f,12)
            };
            var targets=new[] {
                new Vector3(0,3,0),new Vector3(0,2,0),new Vector3(0,3,0),
                new Vector3(0,3,0),new Vector3(0,2.25f,-.1f),new Vector3(-6.66f,1.2f,7.3f),new Vector3(6.66f,1.2f,7.3f)
            };
            string[] names={"overview","approach","side","rear","dj-controls","sun","moon"};
            for(int i=0;i<views.Length;i++)
            {
                camera.transform.position=views[i];camera.transform.LookAt(targets[i]);
                yield return new WaitForSeconds(.35f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,names[i]+"-emission.png"));
            }
            var glowing=Resources.FindObjectsOfTypeAll<Material>().Where(m=>m.name.StartsWith("AH02_")&&m.IsKeywordEnabled("_EMISSION")).ToArray();
            foreach(var material in glowing){material.DisableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);}
            for(int i=0;i<views.Length;i++)
            {
                camera.transform.position=views[i];camera.transform.LookAt(targets[i]);
                yield return new WaitForSeconds(.35f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,names[i]+"-no-emission.png"));
            }
            float total=0,max=0;
            for(int i=0;i<180;i++){yield return null;float ms=Time.unscaledDeltaTime*1000;total+=ms;max=Mathf.Max(max,ms);}
            var result=$"frames=180 mean_ms={total/180:F2} max_ms={max:F2} allocated_mb={Profiler.GetTotalAllocatedMemoryLong()/1048576f:F1} emission_materials={glowing.Length}";
            File.WriteAllText(Path.Combine(output,"runtime-metrics.txt"),result+"\n");
            Debug.Log("[Festival.ProductionSample02] "+result);
            Application.Quit();
        }
    }
}
#endif
