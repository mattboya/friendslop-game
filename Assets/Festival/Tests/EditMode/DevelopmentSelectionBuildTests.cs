using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Festival.Tests
{
    // The player scripts as each build variant compiles them, put through the check the final gates run on built apps.
    public sealed class DevelopmentSelectionBuildTests
    {
        [Test,Timeout(900000)] public void ReleasePlayerScriptsKeepTheHostBootstrapButRefuseLockedFestivalsAndTestMode()
        {
            string folder=Path.Combine(Path.GetTempPath(),"festival-dev-selection-build-"+Guid.NewGuid().ToString("N")),noisy=null;
            try
            {
                string development=PlayerScriptBuild.Compile(folder,"Development",ScriptCompilationOptions.DevelopmentBuild),release=PlayerScriptBuild.Compile(folder,"Release",ScriptCompilationOptions.None);
                var start=new ProcessStartInfo(PlayerScriptBuild.Node("scripts/verify-development-selection.mjs"),"scripts/verify-development-selection.mjs \""+development+"\" \""+release+"\"")
                    {WorkingDirectory=Path.GetDirectoryName(Application.dataPath),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var verify=Process.Start(start))
                {
                    string output=verify.StandardOutput.ReadToEnd()+verify.StandardError.ReadToEnd();verify.WaitForExit();
                    Assert.That(verify.ExitCode,Is.Zero,output);
                    Assert.That(output,Does.Contain("Development: capability=True, locked pick accepted, test mode accepted").And.Contain("Release: capability=False, locked pick refused"));
                    Assert.That(output,Does.Contain(", test mode refused, earned count 1"),"the release host refuses TEST MODE");
                }
            }
            finally{if(Directory.Exists(folder))Directory.Delete(folder,true);noisy=PlayerScriptBuild.WaitForQuietGc();}
            Assert.That(noisy,Is.Null,"later fixtures' garbage probes need this thread quiet again");
        }
    }
}
