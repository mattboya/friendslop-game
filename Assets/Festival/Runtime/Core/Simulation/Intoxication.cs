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
                else if(effect.Id=="shot")multiplier*=.9f;
            }
            return Math.Max(.65f,multiplier);
        }

        public static float LateralDrift(PlayerState player,double seconds)
        {
            if(player==null)return 0;
            float drift=0;
            foreach(var effect in player.Effects)
            {
                if(effect.Id=="lsd")drift+=(float)Math.Sin(seconds*2.1+effect.StartSeconds)*.19f;
                // A debrief shot sways like the mushroom drift.
                else if(effect.Id=="mushrooms"||effect.Id=="shot")drift+=(float)Math.Sin(seconds*1.3+effect.StartSeconds+1.1)*.14f;
            }
            return Math.Max(-.24f,Math.Min(.24f,drift));
        }
    }
}
