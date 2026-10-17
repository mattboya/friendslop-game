using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Festival.Tests
{
    // The player scripts as each build variant compiles them, put through the check the final gates run on built apps.
    public sealed class DevelopmentSelectionBuildTests
    {
        [Test,Timeout(900000)] public void ReleasePlayerScriptsKeepTheHostBootstrapButRefuseLockedFestivals()
        {
            string folder=Path.Combine(Path.GetTempPath(),"festival-dev-selection-build-"+Guid.NewGuid().ToString("N"));
            try
            {
                string Compile(string variant,ScriptCompilationOptions options)
                {
                    string output=Path.Combine(folder,variant);Directory.CreateDirectory(output);
                    var result=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{target=BuildTarget.StandaloneOSX,group=BuildTargetGroup.Standalone,options=options},output);
                    Assert.That(result.assemblies,Does.Contain("Festival.Runtime.dll"),variant+" player scripts compile");
                    return output;
                }
                string development=Compile("Development",ScriptCompilationOptions.DevelopmentBuild),release=Compile("Release",ScriptCompilationOptions.None);
                var start=new ProcessStartInfo(Node(),"scripts/verify-development-selection.mjs \""+development+"\" \""+release+"\"")
                    {WorkingDirectory=Path.GetDirectoryName(Application.dataPath),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var verify=Process.Start(start))
                {
                    string output=verify.StandardOutput.ReadToEnd()+verify.StandardError.ReadToEnd();verify.WaitForExit();
                    Assert.That(verify.ExitCode,Is.Zero,output);
                    Assert.That(output,Does.Contain("Development: capability=True, locked pick accepted").And.Contain("Release: capability=False, locked pick refused"));
                }
            }
            finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }

        // An Editor opened from the Hub may not have node on its PATH.
        static string Node()
        {
            foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(':'))if(folder!=""&&File.Exists(Path.Combine(folder,"node")))return Path.Combine(folder,"node");
            foreach(string candidate in new[]{"/opt/homebrew/bin/node","/usr/local/bin/node"})if(File.Exists(candidate))return candidate;
            Assert.Fail("node is needed to run scripts/verify-development-selection.mjs");return null;
        }
    }
}
