#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Festival.Presentation;
using NUnit.Framework;
using UnityEditor.Build.Player;
using UnityEngine;
namespace Festival.Tests
{
    public sealed class StageBenchmarkTests
    {
        [Test] public void ExplicitOptInPreparesAnEmptyDisposableProfileBeforeSessionAwake()
        {
            var previous=LocalProfile.Folder;
            var output=Path.Combine(Path.GetTempPath(),"festival-benchmark-test-"+Guid.NewGuid().ToString("N"));
            var profile="stage-benchmark-test";
            try
            {
                Assert.That(DevelopmentStageBenchmark.PrepareProfile(new[]{"game"}),Is.Null);
                Assert.That(LocalProfile.Folder,Is.EqualTo(previous),"normal game must remain untouched");
                Assert.That(DevelopmentStageBenchmark.PrepareProfile(new[]{"game","--stage-benchmark","--stage-benchmark-output",output,"--profile",profile}),Is.EqualTo(output));
                Assert.That(LocalProfile.Folder,Is.EqualTo(Path.Combine(output,"profile")));
                var saved=new LocalProfile(profile).Data;
                Assert.That(saved.ReducedMotion,Is.False);
                Assert.That(saved.HighContrast,Is.False);
                Assert.That(saved.ThirdPerson,Is.False);
                // The older comparison build has no dose roll, so shake stays off to keep both views still.
                Assert.That(saved.CameraShake,Is.False,"the dose roll stays off, so the fixed-camera check holds");
                Assert.That(saved.UnlockedFestivalCount,Is.EqualTo(2));
                Assert.Throws<IOException>(()=>DevelopmentStageBenchmark.PrepareProfile(new[]{"game","--stage-benchmark","--stage-benchmark-output",output,"--profile",profile}),"never reuse/overwrite an existing profile directory");
            }
            finally{LocalProfile.Folder=previous;if(Directory.Exists(output))Directory.Delete(output,true);}
        }
        [Test] public void CrewOffHidesTheStageCrewForTheWholeRun()
        {
            var world=new GameObject("crew-off world").AddComponent<FestivalWorld>();
            try
            {
                world.Build();var crew=world.GetComponentInChildren<FestivalStageCrew>(true);
                Assert.That(crew,Is.Not.Null,"setup: the festival has its stage crew");
                Assert.That(DevelopmentStageBenchmark.HideStageCrew(),Is.EqualTo(6),"all six performers go");
                Assert.That(crew.enabled,Is.False,"and their culling never shows them again");
                for(int i=0;i<crew.Count;i++)Assert.That(crew.Member(i).gameObject.activeSelf,Is.False,crew.Member(i).name+" is hidden");
            }
            finally{UnityEngine.Object.DestroyImmediate(world.gameObject);}
        }
        [Test] public void CannotOptInUsingTheRealDefaultProfile()
        {
            Assert.Throws<ArgumentException>(()=>DevelopmentStageBenchmark.PrepareProfile(new[]{"game","--stage-benchmark","--profile","default"}));
        }
        [Test] public void ReportsNearestRankTailLatencyWithoutChangingTheNativeSamples()
        {
            var frames=Enumerable.Range(1,100).Reverse().Select(x=>(double)x).ToArray();
            Assert.That(DevelopmentStageBenchmark.Percentile(frames,.5),Is.EqualTo(50));
            Assert.That(DevelopmentStageBenchmark.Percentile(frames,.95),Is.EqualTo(95));
            Assert.That(DevelopmentStageBenchmark.Percentile(frames,.99),Is.EqualTo(99));
            Assert.That(frames[0],Is.EqualTo(100),"metrics must not reorder the recorded frames");
            Assert.That(DevelopmentStageBenchmark.Percentile(Array.Empty<double>(),.95),Is.EqualTo(-1),"unsupported timing is unavailable, never zero cost");
        }
        // The player scripts as each build variant compiles them, put through the check the final gates run on built apps.
        [Test,Timeout(900000)] public void ReleasePlayerScriptsLeaveTheStageBenchmarkOut()
        {
            string folder=Path.Combine(Path.GetTempPath(),"festival-stage-benchmark-build-"+Guid.NewGuid().ToString("N")),noisy=null;
            try
            {
                string development=PlayerScriptBuild.Compile(folder,"Development",ScriptCompilationOptions.DevelopmentBuild),release=PlayerScriptBuild.Compile(folder,"Release",ScriptCompilationOptions.None);
                Assert.That(Verify(development,release),Does.StartWith("exit 0").And.Contain("MAC RELEASE DIAGNOSTIC EXCLUSION PASSED"));
                string leaked=Path.Combine(folder,"Leaked");Directory.CreateDirectory(leaked);
                File.Copy(Path.Combine(release,"Festival.Runtime.dll"),Path.Combine(leaked,"Festival.Runtime.dll"));File.AppendAllText(Path.Combine(leaked,"Festival.Runtime.dll"),nameof(DevelopmentStageBenchmark));
                Assert.That(Verify(development,leaked),Does.Not.StartWith("exit 0").And.Contain("Release assembly contains DevelopmentStageBenchmark"),"a Release that kept the benchmark driver is refused");
            }
            finally{if(Directory.Exists(folder))Directory.Delete(folder,true);noisy=PlayerScriptBuild.WaitForQuietGc();}
            Assert.That(noisy,Is.Null,"later fixtures' garbage probes need this thread quiet again");
        }

        static string Verify(string development,string release)
        {
            var start=new ProcessStartInfo(PlayerScriptBuild.Node("scripts/verify-release-diagnostics.mjs"),"scripts/verify-release-diagnostics.mjs \""+development+"\" \""+release+"\"")
                {WorkingDirectory=Path.GetDirectoryName(Application.dataPath),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var verify=Process.Start(start))
            {
                string output=verify.StandardOutput.ReadToEnd()+verify.StandardError.ReadToEnd();verify.WaitForExit();
                return "exit "+verify.ExitCode+"\n"+output;
            }
        }
    }
}
#endif
