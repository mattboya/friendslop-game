using System;
namespace Festival.Core
{
    /// <summary>DANCE-3: a dancer keeps DanceSpacing from their partner, so the two don't overlap in the dancer view. A dance started
    /// closer steps the dancer straight back from the partner to DanceSpacing (from their own facing when they stand on the
    /// partner), and every dance starts with the dancer facing their partner. The step is checked as a move is; a blocked spot
    /// leaves the dancer where they stood. The partner never turns, and TryMove keeps the dancer's facing until the dance ends.</summary>
    public sealed partial class FestivalSimulation
    {
        public const double DanceSpacing=1.4;
        /// <summary>Whether a challenge of this kind shows the player dancing with a partner: spaced from them, facing them, and
        /// stepping on each note (Submit).</summary>
        public static bool DancesVisibly(string kind)=>kind=="Dance"||kind=="ConfirmDance";
        // TryMove leaves p's yaw alone while this holds, so the camera can't turn a dancer away from their partner. TRIP-4: nor
        // spin someone lying on the grass round on their back as they look about.
        bool HoldsFacing(PlayerState p){var i=p.InteractionId==""?null:Interaction(p.InteractionId);return i!=null&&i.Status=="Active"&&(DancesVisibly(i.Kind)||i.Kind==LieDownKind);}
        // Called once a dance is accepted and before its witnesses are counted, so they judge the dancer where they dance.
        void GiveDanceRoom(PlayerState p,NpcState partner,string kind)
        {
            if(!DancesVisibly(kind))return;
            double gap=Distance(partner.X,partner.Z,p.X,p.Z);
            if(gap<DanceSpacing)
            {
                // Within a centimetre of the partner the line from them is float noise, so the dancer steps back from their facing.
                double yaw=p.Yaw*Math.PI/180,backX=gap<.01?-Math.Sin(yaw):(p.X-partner.X)/gap,backZ=gap<.01?-Math.Cos(yaw):(p.Z-partner.Z)/gap;
                float x=partner.X+(float)(backX*DanceSpacing),z=partner.Z+(float)(backZ*DanceSpacing);
                var reached=Move(p.X,p.Z,x,z,DanceSpacing-gap);
                if(Distance(reached.X,reached.Z,x,z)<=.05&&InBounds(x,z)&&MayStep(p,x,z)&&CarryTo(p,x,z)){p.X=x;p.Z=z;gap=DanceSpacing;}
            }
            if(gap>=.01)p.Yaw=(float)((Math.Atan2(partner.X-p.X,partner.Z-p.Z)*180/Math.PI+360)%360);
        }
    }
}
