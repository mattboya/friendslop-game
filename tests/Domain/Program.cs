using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var mode = args.Length > 0 ? args[0] : "all";
            foreach (var suite in Suites(mode))
            {
                suite.GetMethod("Run", Type.EmptyTypes).Invoke(null, null);
                Console.WriteLine(suite.Name + " PASSED");
            }
            if (mode == "all") Console.WriteLine("DOMAIN TESTS PASSED");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.InnerException ?? error); return 1; }
    }
    // A suite is any public static class named *Tests with public static void Run(); new files need no registration here.
    public static List<Type> Suites(string mode)
    {
        var all = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsPublic && t.IsAbstract && t.IsSealed && t.Name.EndsWith("Tests", StringComparison.Ordinal) && t.GetMethod("Run", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null)?.ReturnType == typeof(void))
            .OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        if (mode == "all") return all;
        var names = mode == "content" ? new[] { "ContentTests" } : mode == "simulation" ? new[] { "MissionTests", "SimulationTests" } : new[] { mode };
        var chosen = all.Where(t => names.Contains(t.Name)).ToList();
        if (chosen.Count != names.Length) throw new ArgumentException("Unknown test suite " + mode + "; expected all, content, simulation or one of " + string.Join(", ", all.Select(t => t.Name)));
        return chosen;
    }
}
