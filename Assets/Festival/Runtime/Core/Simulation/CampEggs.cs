using System;
using System.Collections.Generic;

namespace Festival.Core
{
    [Serializable] public sealed class CampEggState
    {
        public string Id="",LastActorId="",LastLine="",Word="",SeatedPlayerId="";
        public float X,Z; public int ActivationCount; public long Sequence; public double LastActivationAt=-1,SeatedUntil;
        public bool Deflated;
    }
    public sealed partial class FestivalSimulation
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnostic callbacks carry only the shared gag and round, never collection masks or profile data.
        public Action<string,string,string,double> CampEggDiagnostic;
#endif
        public const string CampChairPose="CampChair";
        public static bool CampSeated(RoundState state,string playerId)
        {
            var chair=state.CampEggs?.Find(e=>e.Id=="chair");var player=state.Players.Find(p=>p.Id==playerId);
            return chair!=null&&chair.SeatedPlayerId==playerId&&chair.SeatedUntil>state.SimulationSeconds&&player!=null&&player.Connected&&player.Life=="Alive"&&!player.Ready&&player.CampVisitId==""&&player.InteractionId==""&&(state.Phase=="Shopping"||state.Phase=="CampReview");
        }
        void ReleaseCampSeat(string playerId)
        {
            var chair=State.CampEggs.Find(e=>e.Id=="chair");if(chair!=null&&chair.SeatedPlayerId==playerId){chair.SeatedPlayerId="";chair.SeatedUntil=0;}
        }
        void CampEggTick()
        {
            var chair=State.CampEggs.Find(e=>e.Id=="chair");if(chair!=null&&chair.SeatedPlayerId!=""&&!CampSeated(State,chair.SeatedPlayerId))ReleaseCampSeat(chair.SeatedPlayerId);
        }
        static List<CampEggState> CreateCampEggs(int seed)
        {
            var records=new List<CampEggState>();
            foreach(var egg in CampFeatures.Eggs){var point=CampFeatures.EggPoint(egg,seed);records.Add(new CampEggState{Id=egg.Id,X=point.X,Z=point.Z});}
            return records;
        }
        static void RestoreCampEggs(RoundState state)
        {
            var records=CreateCampEggs(state.Seed);
            if(state.CampEggs!=null)
            {
                var ids=new HashSet<string>();
                foreach(var record in state.CampEggs)
                {
                    if(record==null||!ids.Add(record.Id))throw new ArgumentException("Invalid camp secret snapshot");
                    int index=Array.FindIndex(CampFeatures.Eggs,e=>e.Id==record.Id);
                    if(index<0||!Finite(record.X)||!Finite(record.Z)||record.X!=records[index].X||record.Z!=records[index].Z||record.ActivationCount<0||record.ActivationCount>50000||record.Sequence<0||!Finite(record.LastActivationAt)||record.LastActivationAt< -1||!Finite(record.SeatedUntil)||record.SeatedUntil<0||record.Word==null||record.Word!=""&&!Array.Exists(CampFeatures.DustWords,w=>w==record.Word)||record.LastActorId==null||record.LastLine==null||record.SeatedPlayerId==null)throw new ArgumentException("Invalid camp secret snapshot");
                    records[index]=record;
                }
            }
            foreach(var p in state.Players)if(p.FoundCampEggMask<0||p.FoundCampEggMask>255||p.CampEggSequence<0||p.CampEggSequence>50000||!Finite(p.LastCampEggAt)||p.LastCampEggAt< -1||p.LastCampEggId==null||p.LastCampEggId!=""&&CampFeatures.FindEgg(p.LastCampEggId)==null||p.LastCampEggLine==null)throw new ArgumentException("Invalid camp discovery snapshot");
            state.CampEggs=records;
        }
        CommandResult CampEgg(PlayerState p,GameCommand c)
        {
            var result=ActivateCampEgg(p,c);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CampEggDiagnostic?.Invoke("CampEgg",result.Accepted?c.TargetId+" Accepted":"Refused: "+result.Reason,State.RoundId,State.SimulationSeconds);
#endif
            return result;
        }
        CommandResult ActivateCampEgg(PlayerState p,GameCommand c)
        {
            if(State.Phase!="Shopping"&&State.Phase!="CampReview")return Reject("Camp secrets wait until you're back at camp");
            if(p.Life!="Alive"||p.Ready||p.InteractionId!=""||p.DragTargetId!=""||p.CarryBodyId!=""||p.HeldOfferId!=""||p.CampVisitId!="")return Reject("Camp secrets need a living, unready friend with free hands outdoors");
            var egg=CampFeatures.FindEgg(c.TargetId);if(egg==null)return Reject("Unknown camp secret");
            var record=State.CampEggs.Find(e=>e.Id==egg.Id);
            if(record==null||!Near(p,record.X,record.Z))return Reject("Stand within 2.5 m of a camp secret");
            if(CampSeated(State,p.Id))return Reject("Stand up to find another camp secret");
            if(egg.Id=="chair"&&record.SeatedPlayerId!=""&&CampSeated(State,record.SeatedPlayerId))return Reject("The headliner's chair already has a headliner");
            if(egg.Id=="chair"){record.SeatedPlayerId=p.Id;record.SeatedUntil=State.SimulationSeconds+4;p.X=record.X;p.Z=record.Z;p.Yaw=180;}
            record.ActivationCount++;record.LastActorId=p.Id;record.LastActivationAt=State.SimulationSeconds;record.Sequence=State.TransactionSequence+1;
            if(egg.Id=="flamingo")record.Deflated=true;
            // Seed each host draw by visit and activation so saving/restoring needs no separate RNG state.
            if(egg.Id=="wash_me")record.Word=CampFeatures.DustWords[new Random(unchecked(State.Seed^record.ActivationCount*7919)).Next(CampFeatures.DustWords.Length)];
            record.LastLine=egg.Lines[(record.ActivationCount-1)%egg.Lines.Length].Replace("{word}",record.Word);
            p.LastCampEggId=egg.Id;p.LastCampEggLine=record.LastLine;p.LastCampEggAt=record.LastActivationAt;p.CampEggSequence++;
            p.FoundCampEggMask|=1<<Array.IndexOf(CampFeatures.Eggs,egg);
            return Ok(record.LastLine);
        }
    }
}
