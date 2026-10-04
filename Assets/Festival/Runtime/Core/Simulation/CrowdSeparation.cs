namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        // CROWD-6: shorten an approach step rather than enter another attendee.
        // All candidate steps still use Move, including the host's installed Navigate callback.
        WorldPoint MovePursuer(NpcState n,float tx,float tz,double step)
        {
            var next=Move(n.X,n.Z,tx,tz,step);
            if(CrowdPlaceClear(n,next))return next;
            double low=0,high=step;var best=new WorldPoint(n.X,n.Z);
            for(int attempt=0;attempt<12;attempt++)
            {
                double middle=(low+high)/2;var candidate=Move(n.X,n.Z,tx,tz,middle);
                if(CrowdPlaceClear(n,candidate)){low=middle;best=candidate;}else high=middle;
            }
            return best;
        }
        bool CrowdPlaceClear(NpcState n,WorldPoint at)
        {
            // Small float margin keeps the final gap at least 0.7 m after coordinate rounding.
            foreach(var other in State.Npcs)
                if(other!=n&&other.Kind=="Wook"&&Distance(at.X,at.Z,other.X,other.Z)<.7001)return false;
            return true;
        }
    }
}
