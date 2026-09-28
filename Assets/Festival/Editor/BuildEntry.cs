using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Festival.Editor
{
    public static class BuildEntry
    {
        [MenuItem("Festival/Build/Windows Development")]
        public static void BuildWindowsDevelopment(){Build(BuildTarget.StandaloneWindows64,"Builds/Windows/Development/FestivalCoop.exe",true);}
        [MenuItem("Festival/Build/Windows Release")]
        public static void BuildWindowsRelease(){Build(BuildTarget.StandaloneWindows64,"Builds/Windows/Release/FestivalCoop.exe",false);}
        [MenuItem("Festival/Build/macOS Development")]
        public static void BuildMacDevelopment()
        {
            var output=Environment.GetEnvironmentVariable("FESTIVAL_MAC_DEV_OUTPUT");
            Build(BuildTarget.StandaloneOSX,string.IsNullOrWhiteSpace(output)
                ? "Builds/macOS/Development/FestivalCoop.app" : output,true);
        }
        [MenuItem("Festival/Build/macOS Release")]
        public static void BuildMacRelease(){Build(BuildTarget.StandaloneOSX,"Builds/macOS/Release/FestivalCoop.app",false);}
        private static void Build(BuildTarget target,string path,bool development)
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target))throw new InvalidOperationException("Install Unity platform build support for "+target);
            ProjectBootstrap.EnsureGeneratedContent();ProjectValidation.Validate();Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ProjectBootstrap.ScenePath},locationPathName=path,target=target,options=development?BuildOptions.Development|BuildOptions.AllowDebugging:BuildOptions.None});
            if(report==null||report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Native build failed: "+(report==null?"no report":report.summary.result.ToString()));
            UnityEngine.Debug.Log("FESTIVAL BUILD PASSED: "+path);
        }
    }
}
