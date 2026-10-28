using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
using UnityEngine.TestTools.Constraints;

namespace Festival.Tests
{
    static class PlayerScriptBuild
    {
        public static string Compile(string folder,string variant,ScriptCompilationOptions options)
        {
            string output=Path.Combine(folder,variant);Directory.CreateDirectory(output);
            var result=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{target=BuildTarget.StandaloneOSX,group=BuildTargetGroup.Standalone,options=options},output);
            Assert.That(result.assemblies,Does.Contain("Festival.Runtime.dll"),variant+" player scripts compile");
            return output;
        }

        // An Editor opened from the Hub may not have node on its PATH.
        public static string Node(string script)
        {
            foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(':'))if(folder!=""&&File.Exists(Path.Combine(folder,"node")))return Path.Combine(folder,"node");
            foreach(string candidate in new[]{"/opt/homebrew/bin/node","/usr/local/bin/node"})if(File.Exists(candidate))return candidate;
            Assert.Fail("node is needed to run "+script);return null;
        }

        // Editor work after the compile puts GC.Alloc samples on this thread for about 0.3 s, and no collect or Editor event ends it.
        // Later Is.Not.AllocatingGCMemory probes would see them, so this polls that same check. Returns null, or why not at the limit.
        const double QuietSeconds=.2,LimitSeconds=30;
        static float sink;
        // About a millisecond of work that makes no garbage.
        static void Busy(){long end=Stopwatch.GetTimestamp()+Stopwatch.Frequency/1000;while(Stopwatch.GetTimestamp()<end)sink+=Mathf.Sin(sink);}
        public static string WaitForQuietGc()
        {
            var probe=new AllocatingGCMemoryConstraint();TestDelegate busy=Busy;
            long start=Stopwatch.GetTimestamp(),quiet=start,now;int blocks=0,noisy=0;
            while((now=Stopwatch.GetTimestamp())-quiet<QuietSeconds*Stopwatch.Frequency)
            {
                if(now-start>LimitSeconds*Stopwatch.Frequency)return "GC.Alloc samples still show on this thread "+LimitSeconds+" s after the player-script compile ("+noisy+" of "+blocks+" probe blocks), so later garbage probes would fail";
                blocks++;if(probe.ApplyTo(busy).IsSuccess){noisy++;quiet=Stopwatch.GetTimestamp();}
            }
            TestContext.WriteLine("GC.Alloc samples on this thread stopped "+Ms(quiet-start)+" ms after the compile test ("+noisy+" of "+blocks+" probe blocks); waited "+Ms(now-start)+" ms");
            return null;
        }
        static long Ms(long ticks)=>ticks*1000/Stopwatch.Frequency;
    }
}
