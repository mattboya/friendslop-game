using Festival.Core;

namespace Festival.Network
{
    // Functional host setup remains present in release; only the enabling assignment is build-dependent.
    public static class FestivalHostBootstrap
    {
        public static void Configure(FestivalSimulation simulation)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            simulation.DevelopmentFestivalSelection=true;
#else
            simulation.DevelopmentFestivalSelection=false;
#endif
        }
    }
}
