using System;

namespace Festival.Core
{
    /// <summary>GAS-1: Giggle Balloons at camp, beside CampFeatures' station. Between levels, any living player there takes a free hit:
    /// nothing for GiggleGasOnsetSeconds, then GiggleGasSeconds of Giggle Gas that fades in over a second and out over the last
    /// three. One at a time: no new hit until the last one is gone. It changes no rule; the hitter's own screen feels it
    /// (FestivalTrip) and friends see them giggle (Giggling, which ViewFor sends). The host counts it down at camp (GiggleGasTick)
    /// and clears it as the crew leaves for a level (ClearGiggleGas), so it never reaches the festival.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string GiggleGasEffect="giggle_gas",TakeGiggleBalloonKind="TakeGiggleBalloon";
        public const double GiggleGasOnsetSeconds=10,GiggleGasSeconds=30,GiggleGasFadeInSeconds=1,GiggleGasFadeOutSeconds=3;
        /// <summary>How strongly e is felt at simulation time now (0-1): 0 for anything but Giggle Gas, while it kicks in, and once
        /// it's over. StartSeconds is when it kicks in, GiggleGasOnsetSeconds after the hit.</summary>
        public static double GiggleGasStrength(ActiveEffect e,double now)
        {
            if(e==null||e.Id!=GiggleGasEffect)return 0;
            double t=now-e.StartSeconds;if(t<=0||t>=GiggleGasSeconds)return 0;
            return Math.Min(1,Math.Min(t/GiggleGasFadeInSeconds,(GiggleGasSeconds-t)/GiggleGasFadeOutSeconds));
        }
        /// <summary>Whether friends see p giggle at simulation time now: only while their Giggle Gas has strength, never while it kicks in.</summary>
        public static bool Giggling(PlayerState p,double now)=>p.Effects.Exists(e=>GiggleGasStrength(e,now)>0);
        /// <summary>Whether p stands within reach of the camp's Giggle Balloons, where TakeGiggleBalloon takes a hit (the HUD offers it there).</summary>
        public static bool AtGiggleBalloons(PlayerState p)=>Near(p,CampFeatures.GiggleBalloonX,CampFeatures.GiggleBalloonZ,CampFeatures.GiggleBalloonReach);
        // Apply lets only a living player outside the camp's tents, cars and potties this far, at camp or at the festival.
        CommandResult TakeGiggleBalloon(PlayerState p)
        {
            if(State.Phase!="Shopping")return Reject("Giggle Balloons stay at camp");
            if(!AtGiggleBalloons(p))return Reject("Stand by the Giggle Balloons to take one");
            if(p.Effects.Exists(e=>e.Id==GiggleGasEffect))return Reject("One Giggle Balloon at a time: let this one wear off");
            p.Effects.Add(new ActiveEffect{Id=GiggleGasEffect,InstanceId=Id("effect"),StartSeconds=State.SimulationSeconds+GiggleGasOnsetSeconds,RemainingSeconds=GiggleGasOnsetSeconds+GiggleGasSeconds});
            return Ok("Giggle Balloon: give it a few seconds…");
        }
        // Each camp step (Step's shopping branch): level effects only tick while a level plays, so Giggle Gas counts down here.
        void GiggleGasTick(double dt){foreach(var p in State.Players)foreach(var e in p.Effects.ToArray())if(e.Id==GiggleGasEffect){e.RemainingSeconds-=dt;if(e.RemainingSeconds<=0)p.Effects.Remove(e);}}
        // As the crew leaves camp (StartRound): the gas stays behind.
        void ClearGiggleGas(){foreach(var p in State.Players)p.Effects.RemoveAll(e=>e.Id==GiggleGasEffect);}
    }
}
