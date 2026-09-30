using System;
using System.Linq;

public static class DomainRunnerTests
{
    private static void Check(bool condition,string message) { if(!condition) throw new Exception("Runner: "+message); }
    private static string Names(string mode)=>string.Join(",",Program.Suites(mode).Select(t=>t.Name));
    public static void Run()
    {
        var all=Program.Suites("all").Select(t=>t.Name).ToList();
        Check(all.SequenceEqual(all.OrderBy(n=>n,StringComparer.Ordinal).Distinct()),"every suite once, sorted by name");
        Check(new[]{"ContentTests","DomainRunnerTests","MissionTests","SimulationTests"}.All(all.Contains),"public static *Tests classes with Run() are discovered");
        Check(!all.Contains(nameof(RunnerFixtureTests)),"a Run that takes arguments is not a suite entry");
        Check(Names("content")=="ContentTests","content mode keeps its suite");
        Check(Names("simulation")=="MissionTests,SimulationTests","simulation mode keeps the mission suite it used to run");
        Check(Names("MissionTests")=="MissionTests","a suite class name runs just that suite");
        foreach(var unknown in new[]{"NopeTests",nameof(RunnerFixtureTests),"missiontests"})
        {
            bool refused=false; try { Program.Suites(unknown); } catch(ArgumentException) { refused=true; }
            Check(refused,"unknown suite "+unknown+" is refused");
        }
    }
}

// Named like a suite, but Run takes an argument, so discovery must skip it.
public static class RunnerFixtureTests { public static void Run(string mode) { throw new InvalidOperationException(mode); } }
