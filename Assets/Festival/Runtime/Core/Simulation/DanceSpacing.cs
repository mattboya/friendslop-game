using System;
namespace Festival.Core
{
    /// <summary>DANCE-3: a dancer keeps DanceSpacing from their partner, so the two don't overlap in the dancer view. A dance started
    /// closer steps the dancer straight back from the partner to DanceSpacing (from their own facing when they stand on the
    /// partner), and every dance starts with the dancer facing their partner. The step is checked as a move is; a blocked spot
    /// leaves the dancer where they stood. The partner never turns, and TryMove keeps the dancer's facing until the dance ends.
    /// DANCE-9: the step only fixes the dancer view, so it is also refused if it would cross a VIP rope (wristband or not) or
    /// change which cops see the player: it never decides a sale's pay or a stop.</summary>
    public sealed partial class FestivalSimulation
    {
        public const double DanceSpacing=1.4;
        /// <summary>Whether a challenge of this kind shows the player dancing with a partner: spaced from them, facing them, and
        /// stepping on each note (Submit). DANCE-2: a sale, a chat and a talk with security are danced too, so the live dancer
        /// view beside their four lanes shows the player dancing.</summary>
        public static bool DancesVisibly(string kind)=>kind=="Dance"||kind=="ConfirmDance"||kind=="Sale"||kind=="Conversation"||kind=="Police";
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
                if(Distance(reached.X,reached.Z,x,z)<=.05&&InBounds(x,z)&&MayStep(p,x,z)&&JudgedTheSame(p,x,z)&&CarryTo(p,x,z)){p.X=x;p.Z=z;gap=DanceSpacing;}
            }
            if(gap>=.01)p.Yaw=(float)((Math.Atan2(partner.X-p.X,partner.Z-p.Z)*180/Math.PI+360)%360);
        }
        // Whether a player at (x,z) is judged as where they stand: VipPayout pays a sale by the VIP ropes, a cop who sees a deal
        // busts them (RecordDeal), and one who sees stock stops them (PoliceTick), whatever challenge they are in. p stands at
        // (x,z) only while the cops look.
        // ponytail: lists the position rules by hand; a new one joins here.
        bool JudgedTheSame(PlayerState p,float x,float z)
        {
            float x0=p.X,z0=p.Z;var seen=State.Npcs.FindAll(n=>n.Kind=="Cop"&&Sees(n,p));
            p.X=x;p.Z=z;bool same=State.Npcs.TrueForAll(n=>n.Kind!="Cop"||Sees(n,p)==seen.Contains(n));p.X=x0;p.Z=z0;
            return same&&InVip(x,z)==InVip(x0,z0);
        }
    }
}
