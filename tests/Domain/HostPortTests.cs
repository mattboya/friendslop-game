using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// A Unity test that hosts on a fixed UDP port fails when a parallel run holds that port. Paths are from the repo root.
public static class HostPortTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("HostPort: "+message);}
    // A Host( call whose last argument is a number other than 0. Inner brackets are skipped whole, so a name such as
    // new string('W',24) is not read as a port.
    static readonly Regex Fixed=new Regex(@"\.Host\((?:[^();]|\([^()]*\))*,\s*[1-9][0-9]*\s*\)");
    public static void Run()
    {
        Check(Fixed.IsMatch("session.Host(\"Light show test\",8732);")&&Fixed.IsMatch("s.Host(Name(),\n 8641 );"),"the scan finds a literal port");
        foreach(var free in new[]{"session.Host(\"Text reader\",0);","host.Host(\"Host\",port);","session.Host(\"Tester\",HostPort);","session.Host(new string('W',24),HostPort);","friend.Host(\"Friend\",FreePort());"})
            Check(!Fixed.IsMatch(free),"no port is read in "+free);
        var found=Directory.GetFiles("Assets/Festival/Tests","*.cs",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal)
            .SelectMany(p=>Fixed.Matches(File.ReadAllText(p)).Cast<Match>().Select(m=>Path.GetFileName(p)+": "+m.Value)).ToList();
        Check(found.Count==0,found.Count+" Unity test(s) host on a fixed port; host on 0, or on FreePort() when a client joins:\n  "+string.Join("\n  ",found));
    }
}
