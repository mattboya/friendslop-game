using System;

namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Opt-in counters only: no text, identities, or per-frame log allocation.
        public bool CrowdDiagnosticsEnabled;
        public long CrowdSlotsAssigned,CrowdBlockedSteps,CrowdRecoverySteps;
#endif
        static void ReleaseApproach(NpcState n){if(n.ApproachTargetId!=""||n.ApproachSlot>=0)n.CrowdDetourSide=0;n.ApproachTargetId="";n.ApproachEpoch=0;n.ApproachSlot=-1;}
        ObserverState HighestApproachObserver(NpcState n)
        {
            ObserverState highest=null;
            foreach(var p in State.Players)
            {
                if(!p.Connected||(p.Life!="Alive"&&p.Life!="Downed"))continue;
                foreach(var o in n.Observers)if(o.PlayerId==p.Id){if(highest==null||o.Suspicion>highest.Suspicion)highest=o;break;}
            }
            return highest;
        }
        void RefreshCrowdApproaches()
        {
            foreach(var n in State.Npcs)
            {
                if(n.ApproachSlot<0)continue;
                var highest=HighestApproachObserver(n);
                var target=highest==null?null:Player(highest.PlayerId);
                if(n.Kind!="Wook"||n.DistractedUntil>State.SimulationSeconds||highest==null||highest.Suspicion<45
                    ||highest.PlayerId!=n.ApproachTargetId||Hidden(n,target)||n.ApproachSlot>4095)ReleaseApproach(n);
            }
        }
        bool ApproachSlotTaken(NpcState n,string target,int slot)
        {
            foreach(var other in State.Npcs)if(other!=n&&other.ApproachTargetId==target&&other.ApproachSlot==slot)return true;
            return false;
        }
        bool SwarmApproach(NpcState n,string target)
        {
            if(n.Kind!="Wook"||n.DistractedUntil>State.SimulationSeconds||n.Mode==Scattering)return false;
            var highest=HighestApproachObserver(n);
            return highest!=null&&highest.PlayerId==target&&highest.Suspicion>=70&&highest.AccusationSeconds>=0
                &&(State.SimulationSeconds-highest.AccusationSeconds>=8||highest.Suspicion>=90&&State.SimulationSeconds-highest.AccusationSeconds>=4);
        }
        bool HasSwarmApproach(PlayerState target)
        {
            foreach(var n in State.Npcs)if(SwarmApproach(n,target.Id))return true;
            return false;
        }
        bool OlderSwarmWaiter(NpcState n,string target)
        {
            foreach(var other in State.Npcs)
                if(other!=n&&other.Kind=="Wook"&&other.ApproachTargetId==target&&other.ApproachEpoch>0&&other.ApproachEpoch<n.ApproachEpoch
                    &&other.ApproachSlot>=6&&SwarmApproach(other,target))return true;
            return false;
        }
        WorldPoint PursuitPlace(NpcState n,PlayerState target)
        {
            bool swarm=n.Mode=="Swarming",outer=!swarm&&HasSwarmApproach(target);
            if(n.ApproachTargetId!=target.Id)ReleaseApproach(n);
            if(n.ApproachEpoch==0){n.ApproachEpoch=++State.CrowdArrivalEpoch;n.ApproachTargetId=target.Id;}
            int slot=n.ApproachSlot;
            // Questioners may use an empty inner ring, but yield it when swarmers pursue this target.
            // Promote a waiting swarmer only when an inner place is vacated.
            if(slot<0||ApproachSlotTaken(n,target.Id,slot)||outer&&slot<6||swarm&&slot>=6)
            {
                int first=outer?6:0,free=first;
                while(ApproachSlotTaken(n,target.Id,free))free++;
                if(swarm&&free<6&&OlderSwarmWaiter(n,target.Id))
                {if(slot<0){free=6;while(ApproachSlotTaken(n,target.Id,free))free++;}else free=slot;}
                if(slot<0||outer&&slot<6||ApproachSlotTaken(n,target.Id,slot)||free<6)slot=free;
            }
            if(slot!=n.ApproachSlot)
            {
                n.ApproachSlot=slot;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(CrowdDiagnosticsEnabled)CrowdSlotsAssigned++;
#endif
            }
            double radius=slot<6?1.3:2.2+.9*(slot/6-1),angle=(slot%6)*Math.PI/3;
            var reach=CrowdReach(target);
            return new WorldPoint(reach.X+(float)(radius*Math.Sin(angle)),reach.Z+(float)(radius*Math.Cos(angle)));
        }
    }
}
