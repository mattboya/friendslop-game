#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Collections;
using Festival.Core;
using Festival.Network;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine;
namespace Festival.Presentation
{
    [DefaultExecutionOrder(10000)]
    public sealed class DevelopmentStageBenchmark : MonoBehaviour
    {
        const int Seed=66500502,Capacity=32768;
        const double WarmSeconds=10,MeasureSeconds=16;
        static string preparedOutput;
        FestivalSession session;
        UniversalRenderPipelineAsset pipeline;
        readonly double[] frames=new double[Capacity],cpu=new double[Capacity],gpu=new double[Capacity],gc=new double[Capacity];
        readonly FrameTiming[] timing=new FrameTiming[1];
        ProfilerRecorder allocations;
        int frameCount,cpuCount,gpuCount,gcCount;
        ulong lastTiming;
        double windowStart,started,simStart;
        bool measuring,failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareBeforeSessionAwake()
        {
            var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"--stage-benchmark")<0)return;
            // Even malformed opt-in cannot fall through into the real profile folder.
            LocalProfile.Folder=Path.Combine(Path.GetTempPath(),"stage-benchmark-rejected-"+Guid.NewGuid().ToString("N"));
            try{preparedOutput=PrepareProfile(args);SceneManager.sceneLoaded+=Install;}
            catch(Exception e){Debug.LogError("FESTIVAL STAGE BENCHMARK FAILED: "+e.Message);Application.Quit(2);}
        }
        static void Install(Scene scene,LoadSceneMode mode)
        {
            SceneManager.sceneLoaded-=Install;
            new GameObject("Development stage benchmark").AddComponent<DevelopmentStageBenchmark>();
        }

        public static string PrepareProfile(string[] args)
        {
            if(Array.IndexOf(args,"--stage-benchmark")<0)return null;
            var output=Argument(args,"--stage-benchmark-output");var profile=Argument(args,"--profile");
            if(!Path.IsPathRooted(output)||!profile.StartsWith("stage-benchmark-",StringComparison.Ordinal)||profile.Length>40
                ||System.Text.RegularExpressions.Regex.IsMatch(profile,"[^a-zA-Z0-9_-]"))
                throw new ArgumentException("Stage benchmark needs an absolute output and a unique stage-benchmark- profile.");
            output=Path.GetFullPath(output);var folder=Path.Combine(output,"profile");
            if(Directory.Exists(folder))throw new IOException("Refusing to reuse an existing benchmark profile directory.");
            Directory.CreateDirectory(folder);LocalProfile.Folder=folder;
            var isolated=new LocalProfile(profile);
            isolated.Data.ReducedMotion=false;isolated.Data.HighContrast=false;isolated.Data.ThirdPerson=false;
            isolated.Data.MusicVolume=.35f;isolated.Data.UnlockedFestivalCount=2;
            // The older comparison build has no dose roll, so shake stays off to keep both views still.
            isolated.Data.CameraShake=false;
            if(isolated.Save()!="")throw new IOException("Could not prepare disposable benchmark profile.");
            return output;
        }
        // The crew's own culling would show them again, so it is switched off too.
        public static int HideStageCrew()
        {
            int hidden=0;
            foreach(var crew in FindObjectsByType<FestivalStageCrew>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                crew.enabled=false;
                for(int i=0;i<crew.Count;i++){crew.Member(i).gameObject.SetActive(false);hidden++;}
            }
            return hidden;
        }
        static string Argument(string[] args,string key)
        {
            int at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:"";
        }
        public static double Percentile(double[] samples,double fraction)
        {
            if(samples.Length==0)return -1;
            var sorted=(double[])samples.Clone();System.Array.Sort(sorted);
            return sorted[System.Math.Max(0,System.Math.Min(sorted.Length-1,(int)System.Math.Ceiling(sorted.Length*fraction)-1))];
        }
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartupAsDouble;Application.logMessageReceived+=OnLog;
            session=FindFirstObjectByType<FestivalSession>();
            if(session==null){Fail("no baseline session");yield break;}
            session.Controls.Map.Disable();
            int quality=Array.IndexOf(QualitySettings.names,"Ultra");
            if(quality<0){Fail("Ultra quality unavailable");yield break;}
            QualitySettings.SetQualityLevel(quality,true);
            pipeline=Instantiate(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            if(pipeline==null){Fail("URP pipeline unavailable");yield break;}
            pipeline.renderScale=1;pipeline.msaaSampleCount=4;
            if(!pipeline.supportsMainLightShadows||!pipeline.supportsSoftShadows||pipeline.supportsAdditionalLightShadows)
            {Fail("baseline shadow feature configuration changed");yield break;}
            pipeline.mainLightShadowmapResolution=2048;pipeline.shadowDistance=45;pipeline.shadowCascadeCount=2;
            pipeline.cascade2Split=.3f;pipeline.cascadeBorder=.2f;pipeline.shadowDepthBias=.7f;pipeline.shadowNormalBias=.45f;
            QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);session.ViewCamera.fieldOfView=75;
            if(!ushort.TryParse(Argument(Environment.GetCommandLineArgs(),"--port"),out var port)||port==0)
            {Fail("explicit valid port required");yield break;}
            if(session.Connected){Fail("benchmark must create its own host");yield break;}
            session.Host("Stage benchmark",port);
            while(session.LocalPlayer==null&&Time.realtimeSinceStartupAsDouble-started<10&&!failed)yield return null;
            if(session.LocalPlayer==null||!session.IsHost){Fail("isolated host did not connect");yield break;}
            allocations=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            for(int festival=0;festival<2&&!failed;festival++)
            {
                var fixture=new FestivalSimulation(Seed);var viewer=fixture.AddPlayer(session.LocalPlayerId,"Stage benchmark");
                viewer.X=0;viewer.Z=18;viewer.MapReady=true;
                var state=fixture.State;state.RoundId="stage-benchmark-"+festival;
                state.FestivalIndex=festival;state.UnlockedFestivalCount=2;state.LevelIndex=1;state.Phase="Playing";
                state.DurationSeconds=600;state.GateOpened=true;state.SpinSeed=Seed;state.TripperId=viewer.Id;
                session.DevelopmentSimulation.Restore(state);session.MenuOpen=false;
                if(Environment.GetEnvironmentVariable("FESTIVAL_STAGE_BENCHMARK_CREW_OFF")=="1")Debug.Log("[Festival.StageBenchmark] crew_off hidden="+HideStageCrew());
                double warmStart=Time.realtimeSinceStartupAsDouble;
                Debug.Log("[Festival.StageBenchmark] warm_start map="+Festivals.Name(festival)+" seed="+Seed);
                while(Time.realtimeSinceStartupAsDouble-warmStart<WarmSeconds&&!failed)yield return null;
                if(failed)yield break;
                if(!Stable(festival)){Fail("camera/world/settings fixture drift before measurement");yield break;}
                frameCount=cpuCount=gpuCount=gcCount=0;lastTiming=0;
                simStart=session.State.SimulationSeconds;windowStart=Time.realtimeSinceStartupAsDouble;measuring=true;
                while(Time.realtimeSinceStartupAsDouble-windowStart<MeasureSeconds&&!failed)yield return null;
                measuring=false;if(failed)yield break;
                if(!Stable(festival)){Fail("camera/world/settings fixture drift after measurement");yield break;}
                var report=new Report{festival=Festivals.Name(festival),buildGuid=Application.buildGUID,unity=Application.unityVersion,
                    gpuDevice=SystemInfo.graphicsDeviceName,gpuApi=SystemInfo.graphicsDeviceType.ToString(),seed=Seed,
                    warmSeconds=WarmSeconds,measuredSeconds=Time.realtimeSinceStartupAsDouble-windowStart,
                    simulationStart=simStart,simulationEnd=session.State.SimulationSeconds,frames=frameCount,
                    npcCount=session.State.Npcs.Count,settings=new Settings{quality=QualitySettings.names[QualitySettings.GetQualityLevel()]},
                    nativeFrameMs=Copy(frames,frameCount),frameMs=Stats(frames,frameCount,"Time.unscaledDeltaTime; native player frame intervals"),
                    cpuMs=Stats(cpu,cpuCount,"FrameTimingManager.cpuFrameTime"),gpuMs=Stats(gpu,gpuCount,"FrameTimingManager.gpuFrameTime"),
                    gcBytes=Stats(gc,gcCount,"ProfilerRecorder GC Allocated In Frame; bytes, one-frame delayed"),
                    frameTimingEnabled=FrameTimingManager.IsFeatureEnabled()};
                Inventory(report);
                string name=festival==0?"PalmMirage":"EmberPlaya";
                string path=Path.Combine(preparedOutput,name+".json");File.WriteAllText(path,JsonUtility.ToJson(report,true));
                Debug.Log("[Festival.StageBenchmark] metrics="+path);
                ScreenCapture.CaptureScreenshot(Path.Combine(preparedOutput,name+".png"));
                yield return new WaitForSecondsRealtime(.5f);
            }
            if(!failed){Debug.Log("FESTIVAL STAGE BENCHMARK COMPLETE");Application.Quit(0);}
        }
        void LateUpdate()
        {
            if(failed)return;
            if(Time.realtimeSinceStartupAsDouble-started>85){Fail("driver timeout");return;}
            if(!measuring)return;
            if(frameCount>=Capacity){Fail("native sample capacity exceeded");return;}
            frames[frameCount++]=Time.unscaledDeltaTime*1000;
            if(allocations.Valid)gc[gcCount++]=allocations.LastValue;
            FrameTimingManager.CaptureFrameTimings();
            if(FrameTimingManager.GetLatestTimings(1,timing)==0||timing[0].frameStartTimestamp==lastTiming)return;
            lastTiming=timing[0].frameStartTimestamp;
            if(timing[0].cpuFrameTime>0)cpu[cpuCount++]=timing[0].cpuFrameTime;
            if(timing[0].gpuFrameTime>0)gpu[gpuCount++]=timing[0].gpuFrameTime;
        }
        bool Stable(int festival)
        {
            var world=FindFirstObjectByType<FestivalWorld>();var p=session.LocalPlayer;var view=session.ViewCamera;
            return Screen.width==1280&&Screen.height==720&&world!=null&&world.IsReady&&world.NavigationReady
                &&session.State.Phase=="Playing"&&session.State.FestivalIndex==festival&&session.State.Seed==Seed
                &&p!=null&&Math.Abs(p.X)<.01&&Math.Abs(p.Z-18)<.01&&!session.ThirdPersonView
                &&Vector3.Distance(view.transform.position,new Vector3(0,1.65f,18))<.01
                &&Quaternion.Angle(view.transform.rotation,Quaternion.identity)<.01
                &&Math.Abs(view.fieldOfView-75)<.01&&pipeline.renderScale==1&&pipeline.msaaSampleCount==4
                &&QualitySettings.vSyncCount==0&&Application.targetFrameRate==-1
                &&!session.Profile.Data.ReducedMotion&&!session.Profile.Data.HighContrast;
        }
        void Inventory(Report report)
        {
            var planes=GeometryUtility.CalculateFrustumPlanes(session.ViewCamera);
            var nearPoint=session.ViewCamera.transform.position+session.ViewCamera.transform.forward*session.ViewCamera.nearClipPlane;
            var near=new Plane(session.ViewCamera.transform.forward,nearPoint);
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if(!r.enabled||!r.gameObject.activeInHierarchy)continue;
                report.activeRenderers++;if(r is SkinnedMeshRenderer)report.activeSkinned++;
                if((session.ViewCamera.cullingMask&(1<<r.gameObject.layer))==0||!GeometryUtility.TestPlanesAABB(planes,r.bounds))continue;
                report.frustumRenderers++;if(r is SkinnedMeshRenderer)report.frustumSkinned++;
                if(Math.Abs(near.GetDistanceToPoint(r.bounds.center))<=Vector3.Dot(r.bounds.extents,new Vector3(Math.Abs(near.normal.x),Math.Abs(near.normal.y),Math.Abs(near.normal.z))))report.boundsIntersectNearPlane++;
            }
            foreach(var actor in FindObjectsByType<FestivalCharacter>(FindObjectsSortMode.None))
            {
                if(!actor.gameObject.activeInHierarchy)continue;report.activeCharacters++;
                if(actor.name.StartsWith("stage_crew",StringComparison.Ordinal))report.stageCrewCharacters++;
            }
        }
        static double[] Copy(double[] data,int count){var copy=new double[count];Array.Copy(data,copy,count);return copy;}
        static Metric Stats(double[] data,int count,string source)
        {
            var values=Copy(data,count);return new Metric{available=count>0,samples=count,source=source,
                p50=Percentile(values,.5),p95=Percentile(values,.95),p99=Percentile(values,.99)};
        }
        void OnLog(string message,string trace,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Fail("player error: "+message);
        }
        void Fail(string reason)
        {
            if(failed)return;failed=true;measuring=false;Application.logMessageReceived-=OnLog;
            Debug.LogError("FESTIVAL STAGE BENCHMARK FAILED: "+reason);Application.Quit(2);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;allocations.Dispose();if(pipeline!=null)Destroy(pipeline);}
        [Serializable] public sealed class Metric{public bool available;public int samples;public string source;public double p50,p95,p99;}
        [Serializable] public sealed class Settings
        {
            public string quality;public int width=1280,height=720,msaa=4,cascades=2,shadowResolution=2048,frameCap=-1,vSync=0;
            public float renderScale=1,shadowDistance=45,cascadeSplit=.3f,cascadeBorder=.2f,shadowDepthBias=.7f,shadowNormalBias=.45f,fov=75;
            public bool mainShadows=true,softShadows=true,additionalShadows=false,reducedMotion=false,highContrast=false,thirdPerson=false;
            public Vector3 cameraPosition=new Vector3(0,1.65f,18),cameraEuler=Vector3.zero;
        }
        [Serializable] public sealed class Report
        {
            public string festival,buildGuid,unity,gpuDevice,gpuApi;
            public int seed,frames,npcCount,activeRenderers,activeSkinned,frustumRenderers,frustumSkinned,activeCharacters,stageCrewCharacters,boundsIntersectNearPlane;
            public bool frameTimingEnabled;public double warmSeconds,measuredSeconds,simulationStart,simulationEnd;
            public Settings settings;public Metric frameMs,cpuMs,gpuMs,gcBytes;public double[] nativeFrameMs;
            public string fixture="Deterministic host simulation restored via baseline DevelopmentSimulation; real session camera and world; no end-to-end gameplay claim";
            public string clipping="boundsIntersectNearPlane is an AABB proxy, not proof of visible clipping; inspect saved native PNG";
        }
    }
}
#endif
