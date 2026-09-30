using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>
    /// Speed- and leg-length-dependent gait. Uses dynamic similarity (Alexander 1984):
    /// relative stride grows with the Froude number v²/(gL), and time spent on each foot
    /// shrinks as a walk becomes a run. Short-legged festivalgoers therefore take quicker,
    /// shorter steps than an adult human at the same speed, but never the 7–10 steps a
    /// second a fixed one-metre stride produced at the game's 4–6 m/s movement.
    /// </summary>
    public readonly struct FestivalGait
    {
        public readonly float Stride;   // metres travelled per full two-step cycle
        public readonly float Stance;   // fraction of a cycle each foot stays planted
        public readonly float Run;      // 0 walking .. 1 running posture
        FestivalGait(float stride,float stance,float run){Stride=stride;Stance=stance;Run=run;}

        public static FestivalGait For(float speed,float legLength)
        {
            float leg=Mathf.Max(.2f,legLength);
            float froude=Mathf.Max(0,speed)*Mathf.Max(0,speed)/(9.81f*leg);
            float stride=Mathf.Max(.6f*leg,2.3f*leg*Mathf.Pow(Mathf.Max(froude,1e-4f),.3f));
            float run=Smooth((froude-.5f)/1.5f);
            // Both feet leave the ground once stance drops below half a cycle; the
            // shorter stance also keeps each planted foot inside these short legs' reach.
            float stance=Mathf.Lerp(.60f,.27f,Smooth((froude-.5f)/3f));
            return new FestivalGait(stride,stance,run);
        }

        public float StepsPerSecond(float speed)=>2*Mathf.Max(0,speed)/Stride;
        static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
    }
}
