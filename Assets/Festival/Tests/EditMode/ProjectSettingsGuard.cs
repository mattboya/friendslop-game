using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(Festival.Tests.ProjectSettingsGuard))]

namespace Festival.Tests
{
    // URP writes GraphicsSettings' light flags on every render and a quality level's anti-aliasing when it builds a pipeline, and
    // the editor saves both into ProjectSettings. In this namespace it wraps every EditMode test: a second SetUpFixture outside
    // any namespace would take TestProfiles' tests from it in Unity's runner.
    [SetUpFixture]
    public sealed class ProjectSettingsGuard:ITestRunCallback
    {
        // EnterPlayMode reloads the domain, and the runner then runs this OneTimeSetUp again on a new instance: keep the first record.
        internal const string Recorded="ProjectSettingsGuard.recorded",AntiAliasing="ProjectSettingsGuard.antiAliasing",Linear="ProjectSettingsGuard.linear",Temperature="ProjectSettingsGuard.temperature",Level="ProjectSettingsGuard.level";
        [OneTimeSetUp]public void Record()
        {
            if(SessionState.GetBool(Recorded,false))return;
            int level=QualitySettings.GetQualityLevel();var antiAliasing=new int[QualitySettings.names.Length];
            for(int i=0;i<antiAliasing.Length;i++){QualitySettings.SetQualityLevel(i,false);antiAliasing[i]=QualitySettings.antiAliasing;}
            QualitySettings.SetQualityLevel(level,false);
            SessionState.SetInt(Level,level);SessionState.SetIntArray(AntiAliasing,antiAliasing);
            SessionState.SetBool(Linear,GraphicsSettings.lightsUseLinearIntensity);SessionState.SetBool(Temperature,GraphicsSettings.lightsUseColorTemperature);
            SessionState.SetBool(Recorded,true);
        }
        // The quality level stays as the run left it, so scripts/unity.mjs still fails a run that leaks it. The assert fails it too, in a
        // copy with no .git of its own, where scripts/unity.mjs has nothing to compare against.
        [OneTimeTearDown]public void Restore()
        {
            if(!SessionState.GetBool(Recorded,false))return;
            int level=QualitySettings.GetQualityLevel();var antiAliasing=SessionState.GetIntArray(AntiAliasing,new int[0]);
            for(int i=0;i<antiAliasing.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.antiAliasing=antiAliasing[i];}
            QualitySettings.SetQualityLevel(level,false);
            GraphicsSettings.lightsUseLinearIntensity=SessionState.GetBool(Linear,false);GraphicsSettings.lightsUseColorTemperature=SessionState.GetBool(Temperature,false);
            SessionState.EraseBool(Recorded);
            Assert.That(QualitySettings.names[level],Is.EqualTo(QualitySettings.names[SessionState.GetInt(Level,level)]),"the run leaves the editor quality level it found");
        }
        // A run stopped before OneTimeTearDown (in an open editor) leaves the record, and Restore would put back stale settings.
        // Unlike OneTimeSetUp, this runs once a run, not again after the domain reload of EnterPlayMode.
        public void RunStarted(ITest testsToRun)=>SessionState.EraseBool(Recorded);
        public void RunFinished(ITestResult testResults){}
        public void TestStarted(ITest test){}
        public void TestFinished(ITestResult result){}
    }
}
