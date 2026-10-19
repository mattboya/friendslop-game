using System;

namespace Festival.Core
{
    /// <summary>Small, deterministic impairments applied by the host to movement.</summary>
    public static class Intoxication
    {
        public static float MovementMultiplier(PlayerState player)
        {
            if(player==null)return 1;
            float multiplier=1;
            foreach(var effect in player.Effects)
            {
                if(effect.Id=="weed")multiplier*=.8f;
                else if(effect.Id=="lsd")multiplier*=.9f;
                else if(effect.Id=="mushrooms")multiplier*=.88f;
                else if(effect.Id==FestivalSimulation.DoseEffect)
                {
                    multiplier*=FestivalSimulation.DoseMovementMultiplier(effect.Intensity);
                    if(effect.Intensity>0&&effect.Substance=="weed")multiplier*=.9f;
                    if(effect.Intensity>0&&effect.Substance=="ecstasy")multiplier*=1.08f;
                }
                else if(effect.Id=="shot")multiplier*=.9f;
            }
            return Math.Max(.65f,multiplier);
        }

        /// <summary>The walk at simulation time seconds (FestivalSimulation.MoveSpeed): a Pony Dust dose's Stall takes it below the
        /// .65 floor, down to StallFloor of its speed at the stall's depth.</summary>
        public static float MovementMultiplier(PlayerState player,double seconds)=>MovementMultiplier(player)*(1-(1-StallFloor)*Stall(player,seconds));

        // Each StallPeriod from a Pony Dust dose's own start ends with StallEase down, StallHold at the floor and StallEase back up.
        // Every dose stalls alike; the dose already sets the pace.
        public const double StallPeriod=10,StallEase=.3,StallHold=1;
        public const float StallFloor=.1f;
        /// <summary>How deep into Pony Dust's stall player is at simulation time seconds: 0 walking freely, 1 at the near stop,
        /// smoothstep between. It reads only the dose's StartSeconds, which the owner's own view carries, so a client can follow it.</summary>
        public static float Stall(PlayerState player,double seconds)
        {
            if(player==null)return 0;
            foreach(var effect in player.Effects)
            {
                if(effect.Id!=FestivalSimulation.DoseEffect||effect.Intensity<=0||effect.Substance!="ketamine")continue;
                double into=((seconds-effect.StartSeconds)%StallPeriod+StallPeriod)%StallPeriod-(StallPeriod-StallHold-2*StallEase);
                if(into<=0)return 0;
                double x=Math.Min(1,Math.Min(into,StallHold+2*StallEase-into)/StallEase);
                return (float)(x*x*(3-2*x));
            }
            return 0;
        }

        public static float LateralDrift(PlayerState player,double seconds)
        {
            if(player==null)return 0;
            float drift=0;
            foreach(var effect in player.Effects)
            {
                bool dose=effect.Id==FestivalSimulation.DoseEffect&&effect.Intensity>0;
                if(effect.Id=="lsd"||dose&&effect.Substance=="lsd")drift+=(float)Math.Sin(seconds*2.1+effect.StartSeconds)*.19f;
                // A debrief shot sways like the mushroom drift.
                else if(effect.Id=="mushrooms"||effect.Id=="shot"||dose&&effect.Substance=="mushrooms")drift+=(float)Math.Sin(seconds*1.3+effect.StartSeconds+1.1)*.14f;
            }
            return Math.Max(-.24f,Math.Min(.24f,drift));
        }
    }
}
