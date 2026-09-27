using System;
using System.Reflection;
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var mode = args.Length > 0 ? args[0] : "all";
            if (mode != "content" && mode != "simulation" && mode != "all") throw new ArgumentException("Unknown test suite");
            if (mode == "content" || mode == "all") Run("ContentTests", "CONTENT TESTS PASSED");
            if (mode == "simulation" || mode == "all") Run("SimulationTests", "SIMULATION TESTS PASSED");
            if (mode == "all") Console.WriteLine("DOMAIN TESTS PASSED");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.InnerException ?? error); return 1; }
    }
    private static void Run(string name, string marker)
    {
        var type = Assembly.GetExecutingAssembly().GetType(name, true);
        var method = type.GetMethod("Run", BindingFlags.Static | BindingFlags.Public);
        if (method == null) throw new InvalidOperationException("Missing test entry " + name);
        method.Invoke(null, null);
        Console.WriteLine(marker);
    }
}
