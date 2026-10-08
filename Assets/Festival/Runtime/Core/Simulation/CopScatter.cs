using System;

namespace Festival.Core
{
    /// <summary>CROWD-3: while a cop runs in to make an arrest (ArrestWarning: a deal it saw, or a stop ignored too long, detained 4 s
    /// on), the festivalgoers within ScatterRadius of it scatter. A fixed quarter of each level's crowd holds its ground, and
    /// anyone a player is busy with stays, so the scatter never cancels a sale or a chat. The rest run straight away from the cop
    /// at the swarm's 3.6 m/s, facing away, until ScatterRadius out, then stand, still Scattering, out to ScatterHoldRadius so the
    /// mode never flickers at the edge. Like a distracted festivalgoer one gains no suspicion, accuses no one and never strikes,
    /// its observers left as they were. The step after the arrest, or the run-in's end, it carries on by its own rules from where
    /// it stands; nobody walks back, just as chasers never do.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string Scattering="Scattering";
        public const float ScatterRadius=10,ScatterHoldRadius=12;
        const double ScatterSpeed=3.6;

        // WookTick's first check, ahead of Distracted: true while n scatters this step. Ceiling: it reads each cop's Mode as
        // PoliceTick last left it, and CreateRound lists the crowd before the cops, so the whole crowd reacts together one step
        // (0.1 s) after a run-in starts or ends.
        bool Scatter(NpcState n,double dt)
        {
            NpcState cop=null;double dist=0;
            foreach(var c in State.Npcs)if(c.Kind=="Cop"&&c.Mode=="ArrestWarning"){double d=Distance(c.X,c.Z,n.X,n.Z);if(cop==null||d<dist){cop=c;dist=d;}}
            if(cop==null||dist>(n.Mode==Scattering?ScatterHoldRadius:ScatterRadius)||HoldsGround(n)||State.Interactions.Exists(i=>i.Status=="Active"&&i.TargetId==n.Id))return false;
            // Straight away from the cop; one standing right on it runs the way it faces.
            double yaw=dist>.01?Math.Atan2(n.X-cop.X,n.Z-cop.Z):n.Yaw*Math.PI/180;
            if(dist<ScatterRadius){var to=MovePursuer(n,cop.X+(float)(ScatterRadius*Math.Sin(yaw)),cop.Z+(float)(ScatterRadius*Math.Cos(yaw)),ScatterSpeed*dt);n.X=to.X;n.Z=to.Z;}
            ReleaseApproach(n);n.Mode=Scattering;n.Yaw=(float)(yaw*180/Math.PI);n.AttackAt=0;return true;
        }
        // The quarter of the crowd that holds its ground, by place in the crowd and the level's seed, as CreateRound picks talkers.
        internal bool HoldsGround(NpcState n)=>unchecked((uint)(State.Npcs.IndexOf(n)+State.Seed))%4==0;
    }
}
