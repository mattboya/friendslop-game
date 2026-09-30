using System;

namespace Festival.Core
{
    /// <summary>POLO-1: Palm Mirage's twists, off on every other festival. Tuning lives in Festivals.cs.
    /// Influencers film the crowd, and a player on camera puts the wooks around them on alert.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string Influencer="Influencer";

        // As the crew leaves camp, once DealRoles has dealt the level's roles: on Palm Mirage a fresh few festivalgoers film.
        void DealTwists()
        {
            if(State.FestivalIndex!=Festivals.PoloFestival)return;
            var crowd=Shuffled(State.Npcs.FindAll(n=>n.Kind=="Wook"),new ContentRandom(unchecked(State.SpinSeed*11+3)));
            for(int i=0;i<Festivals.Influencers&&i<crowd.Count;i++)crowd[i].Twist=Influencer;
        }
        // Every Playing step, after the crowd has looked around: a live player in an influencer's frame puts every wook within
        // FilmWitnessRadius of them on alert, whether or not that wook can see them (the stream shows their face). It is a passive
        // gain, so it scales with the level and the player's pack like sprinting in view, and it counts as being seen.
        void Film(double dt)
        {
            double heat=Festivals.For(State).SuspicionMultiplier;
            foreach(var cam in State.Npcs)if(cam.Twist==Influencer)foreach(var p in State.Players)
            {
                if(!p.Connected||!InFrame(cam,p))continue;
                double gain=Festivals.FilmSuspicionPerSecond*PassiveGain(p,dt,heat);
                foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Distance(n.X,n.Z,p.X,p.Z)<=Festivals.FilmWitnessRadius){var o=Observe(n,p);o.Suspicion=Math.Min(100,o.Suspicion+gain);o.LastSeenSeconds=State.SimulationSeconds;}
            }
        }
        // The phone looks along the influencer's facing: FilmRange deep, FilmConeDegrees wide, blocked by walls.
        bool InFrame(NpcState cam,PlayerState p)
        {
            double d=Distance(cam.X,cam.Z,p.X,p.Z);if(p.Life!="Alive"||d>Festivals.FilmRange)return false;
            double yaw=cam.Yaw*Math.PI/180;
            if(d>.1&&(Math.Sin(yaw)*(p.X-cam.X)+Math.Cos(yaw)*(p.Z-cam.Z))/d<Math.Cos(Festivals.FilmConeDegrees*Math.PI/360))return false;
            return HasLineOfSight==null||HasLineOfSight(cam.X,cam.Z,p.X,p.Z);
        }
    }
}
