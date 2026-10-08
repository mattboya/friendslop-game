using System;

namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        const double CrowdGap=.7001;
        // Every candidate goes through the same host Move/Navigate authorization. No positional correction follows navigation.
        WorldPoint MovePursuer(NpcState n,float tx,float tz,double step,float cx=0,float cz=0,bool ring=false)
        {
            var stand=new WorldPoint(n.X,n.Z);double minimum=CrowdMinimumGap(n,stand);
            var next=Move(n.X,n.Z,tx,tz,step,out bool approved);
            if(!approved)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(CrowdDiagnosticsEnabled)CrowdBlockedSteps++;
#endif
                return stand;
            }
            if(minimum>=CrowdGap&&Distance(n.X,n.Z,tx,tz)<1e-6)return stand;
            if(ring&&minimum>=CrowdGap&&CrowdBlocker(n,tx,tz)!=null)
            {
                double angle=Math.Atan2(n.X-cx,n.Z-cz),end=Math.Atan2(tx-cx,tz-cz),difference=end-angle;
                while(difference>Math.PI)difference-=2*Math.PI;while(difference< -Math.PI)difference+=2*Math.PI;
                int side=n.CrowdDetourSide!=0?n.CrowdDetourSide:difference<0?-1:1;
                double radius=Math.Max(Distance(n.X,n.Z,cx,cz),Distance(tx,tz,cx,cz)+.85);
                double tangent=angle+side*Math.Min(.2,Math.Abs(difference));
                var around=Move(n.X,n.Z,cx+(float)(radius*Math.Sin(tangent)),cz+(float)(radius*Math.Cos(tangent)),step,out approved);
                if(approved&&CrowdStepSafe(n,around,minimum)){n.CrowdDetourSide=side;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CrowdMoved(n,around,minimum);
#endif
                return around;}
            }
            if(CrowdStepSafe(n,next,minimum))
            {
                if(minimum>=CrowdGap&&CrowdStepSafe(n,new WorldPoint(tx,tz),minimum))n.CrowdDetourSide=0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CrowdMoved(n,next,minimum);
#endif
                return next;
            }
            double heading=Math.Atan2(tx-n.X,tz-n.Z),bestGap=minimum,bestDistance=double.MaxValue;
            WorldPoint best=null;
            // Follow a consistent side of the nearest attendee blocking the destination ray.
            // Tangents around the blocker avoid alternating left/right at a filled arrival ring.
            if(minimum>=CrowdGap)
            {
                var blocker=CrowdBlocker(n,tx,tz);
                if(blocker!=null)
                {
                    double away=Math.Atan2(n.X-blocker.X,n.Z-blocker.Z);
                    for(int side=-1;side<=1;side+=2)
                    {
                        if(n.CrowdDetourSide!=0&&side!=n.CrowdDetourSide)continue;
                        for(int turn=0;turn<3;turn++)
                        {
                            double angle=away+side*(Math.PI/2-turn*Math.PI/6);
                            var candidate=Move(n.X,n.Z,n.X+(float)(Math.Sin(angle)*step),n.Z+(float)(Math.Cos(angle)*step),step,out approved);
                            if(approved&&CrowdStepSafe(n,candidate,minimum)){n.CrowdDetourSide=side;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CrowdMoved(n,candidate,minimum);
#endif
                return candidate;}
                        }
                    }
                }
            }
            // Fixed scalar candidates avoid allocating a direction array on each NPC step.
            int turns=minimum<CrowdGap?6:3;
            for(int turn=turns;turn>=1;turn--)for(int side=-1;side<=1;side+=2)
            {
                if(minimum>=CrowdGap&&n.CrowdDetourSide!=0&&side!=n.CrowdDetourSide)continue;
                double angle=heading+side*turn*Math.PI/6;
                var candidate=Move(n.X,n.Z,n.X+(float)(Math.Sin(angle)*step),n.Z+(float)(Math.Cos(angle)*step),step,out approved);
                if(!approved||!CrowdStepSafe(n,candidate,minimum))continue;
                if(minimum>=CrowdGap){n.CrowdDetourSide=side;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CrowdMoved(n,candidate,minimum);
#endif
                return candidate;}
                double gap=CrowdMinimumGap(n,candidate),distance=Distance(candidate.X,candidate.Z,tx,tz);
                if(best==null||gap>bestGap+1e-6||Math.Abs(gap-bestGap)<1e-6&&distance<bestDistance)
                {best=candidate;bestGap=gap;bestDistance=distance;}
            }
            if(best!=null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CrowdMoved(n,best,minimum);
#endif
                return best;
            }
            double low=0,high=step;best=stand;
            for(int attempt=0;attempt<12;attempt++)
            {
                double middle=(low+high)/2;var candidate=Move(n.X,n.Z,tx,tz,middle,out approved);
                if(approved&&CrowdStepSafe(n,candidate,minimum)){low=middle;best=candidate;}else high=middle;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(CrowdDiagnosticsEnabled&&Distance(n.X,n.Z,best.X,best.Z)<1e-6)CrowdBlockedSteps++;
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CrowdMoved(n,best,minimum);
#endif
            return best;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void CrowdMoved(NpcState n,WorldPoint at,double minimum)
        {
            if(CrowdDiagnosticsEnabled&&minimum<CrowdGap&&CrowdMinimumGap(n,at)>minimum+1e-6)CrowdRecoverySteps++;
        }
#endif
        double CrowdMinimumGap(NpcState n,WorldPoint at)
        {
            double gap=double.MaxValue;
            foreach(var other in State.Npcs)if(other!=n&&other.Kind=="Wook")gap=Math.Min(gap,Distance(at.X,at.Z,other.X,other.Z));
            return gap;
        }
        NpcState CrowdBlocker(NpcState n,float tx,float tz)
        {
            NpcState blocker=null;double nearest=double.MaxValue,dx=tx-n.X,dz=tz-n.Z,length=dx*dx+dz*dz;
            if(length<1e-10)return null;
            foreach(var other in State.Npcs)
            {
                if(other==n||other.Kind!="Wook")continue;
                double fraction=((other.X-n.X)*dx+(other.Z-n.Z)*dz)/length;
                if(fraction<=0||fraction>1)continue;
                double across=Distance(n.X+(float)(fraction*dx),n.Z+(float)(fraction*dz),other.X,other.Z),from=Distance(n.X,n.Z,other.X,other.Z);
                if(across<CrowdGap&&from<nearest){nearest=from;blocker=other;}
            }
            return blocker;
        }
        bool CrowdStepSafe(NpcState n,WorldPoint at,double minimum)
        {
            if(Distance(n.X,n.Z,at.X,at.Z)<1e-6)return false;
            double nextMinimum=double.MaxValue,dx=at.X-n.X,dz=at.Z-n.Z,length=dx*dx+dz*dz;
            foreach(var other in State.Npcs)
            {
                if(other==n||other.Kind!="Wook")continue;
                double before=Distance(n.X,n.Z,other.X,other.Z),after=Distance(at.X,at.Z,other.X,other.Z);
                nextMinimum=Math.Min(nextMinimum,after);
                // Endpoints alone can cut through a neighbour, even when an overlap ends wider than it started.
                double fraction=Math.Max(0,Math.Min(1,((other.X-n.X)*dx+(other.Z-n.Z)*dz)/length));
                double closest=Distance(n.X+(float)(fraction*dx),n.Z+(float)(fraction*dz),other.X,other.Z);
                if(before<CrowdGap){if(after+1e-6<before||closest+1e-6<before)return false;}
                else if(closest<CrowdGap)return false;
            }
            return minimum>=CrowdGap||nextMinimum>minimum+1e-6;
        }
    }
}
