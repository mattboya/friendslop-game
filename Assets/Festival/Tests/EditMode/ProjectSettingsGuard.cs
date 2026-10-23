using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Tests
{
    // URP writes GraphicsSettings' light flags on every render and a quality level's anti-aliasing when it builds a pipeline, and
    // the editor saves both into ProjectSettings. In this namespace it wraps every EditMode test: a second SetUpFixture outside
    // any namespace would take TestProfiles' tests from it in Unity's runner.
    [SetUpFixture]
    public sealed class ProjectSettingsGuard
    {
        // EnterPlayMode reloads the domain, and the runner then runs this OneTimeSetUp again on a new instance: keep the first record.
        const string Recorded="ProjectSettingsGuard.recorded",AntiAliasing="ProjectSettingsGuard.antiAliasing",Linear="ProjectSettingsGuard.linear",Temperature="ProjectSettingsGuard.temperature";
        [OneTimeSetUp]public void Record()
        {
            if(SessionState.GetBool(Recorded,false))return;
            int level=QualitySettings.GetQualityLevel();var antiAliasing=new int[QualitySettings.names.Length];
            for(int i=0;i<antiAliasing.Length;i++){QualitySettings.SetQualityLevel(i,false);antiAliasing[i]=QualitySettings.antiAliasing;}
            QualitySettings.SetQualityLevel(level,false);
            SessionState.SetIntArray(AntiAliasing,antiAliasing);
            SessionState.SetBool(Linear,GraphicsSettings.lightsUseLinearIntensity);SessionState.SetBool(Temperature,GraphicsSettings.lightsUseColorTemperature);
            SessionState.SetBool(Recorded,true);
        }
        // The quality level stays as the run left it, so scripts/unity.mjs still fails a run that leaks it.
        [OneTimeTearDown]public void Restore()
        {
            if(!SessionState.GetBool(Recorded,false))return;
            int level=QualitySettings.GetQualityLevel();var antiAliasing=SessionState.GetIntArray(AntiAliasing,new int[0]);
            for(int i=0;i<antiAliasing.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.antiAliasing=antiAliasing[i];}
            QualitySettings.SetQualityLevel(level,false);
            GraphicsSettings.lightsUseLinearIntensity=SessionState.GetBool(Linear,false);GraphicsSettings.lightsUseColorTemperature=SessionState.GetBool(Temperature,false);
            SessionState.EraseBool(Recorded);
        }
    }
}
