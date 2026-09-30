using System;

namespace Festival.Core
{
    /// <summary>ESC-1: two cops every level walk a loop around the edge of the crowd whenever they have no evidence.</summary>
    public sealed partial class FestivalSimulation
    {
        // PatrolPoints waypoints on a PatrolRadius ring around the crowd centroid, walked clockwise at PatrolSpeed. The straight
        // legs stay about 22.5 m out (radius x cos 30 deg): clear of the dense centre, yet across every route from the camp gate
        // to the lost friend. Evidence (stops, witnessed deals, detaining) always comes first; see PoliceTick.
        public const int PatrolPoints=6;
        const double PatrolRadius=26,PatrolSpeed=1.6,PatrolLeadDegrees=1;
        static readonly WorldPoint CrowdCentre=FestivalCrowdLayout.Centroid();
        public static WorldPoint PatrolPoint(int k){double a=2*Math.PI*k/PatrolPoints;return new WorldPoint(CrowdCentre.X+(float)(PatrolRadius*Math.Sin(a)),CrowdCentre.Z+(float)(PatrolRadius*Math.Cos(a)));}
        // The two cops start on opposite sides of the loop, so every stretch of it sees a cop about as often.
        static void AddCops(RoundState s){for(int i=0;i<2;i++){var at=PatrolPoint(2+i*PatrolPoints/2);s.Npcs.Add(new NpcState{Id="cop_"+i,Kind="Cop",Mode="Patrol",X=at.X,Z=at.Z,Yaw=180});}}
        // Head for the waypoint just ahead of the cop's bearing from the centroid, so a cop pulled off the loop by a stop rejoins
        // it going the same way; the small lead sends a cop standing on a waypoint on to the next one. A cop someone is talking
        // to stands and faces them until the chat ends.
        void Patrol(NpcState cop,double dt)
        {
            cop.Mode="Patrol";cop.TargetId="";
            var talk=State.Interactions.Find(i=>i.Status=="Active"&&i.TargetId==cop.Id);var talker=talk==null?null:Player(talk.PlayerId);
            if(talker!=null){cop.Yaw=Bearing(cop.X,cop.Z,talker.X,talker.Z);return;}
            double ahead=Bearing(CrowdCentre.X,CrowdCentre.Z,cop.X,cop.Z)+PatrolLeadDegrees;if(ahead<0)ahead+=360;
            var next=PatrolPoint((int)Math.Floor(ahead*PatrolPoints/360)+1);
            var point=Move(cop.X,cop.Z,next.X,next.Z,PatrolSpeed*dt);
            if(Distance(cop.X,cop.Z,point.X,point.Z)>0)cop.Yaw=Bearing(cop.X,cop.Z,point.X,point.Z);
            cop.X=point.X;cop.Z=point.Z;
        }
        static float Bearing(float x,float z,float tx,float tz)=>(float)(Math.Atan2(tx-x,tz-z)*180/Math.PI);
    }
}
