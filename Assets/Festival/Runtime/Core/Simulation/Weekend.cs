using System;

namespace Festival.Core
{
    /// <summary>Festival weekends: how each level ends and who may head back to camp.</summary>
    public sealed partial class FestivalSimulation
    {
        // Per-level outcome, checked at the start and end of every Playing step: a Result string ends the level, "" keeps
        // playing. Every level is the rescue for now; LOOP-2 adds the day branch and LOOP-3 the Night 2 branch.
        string LevelEndCheck()
        {
            if(State.ElapsedSeconds>=State.DurationSeconds)return "Time expired";
            var connected=State.Players.FindAll(p=>p.Connected);
            bool soloCanRecover=connected.Count==1&&(connected[0].Life=="Downed"||connected[0].Life=="Spirit"&&connected[0].RevivalCount<2);
            if(!connected.Exists(p=>p.Life=="Alive"||p.Life=="Detained")&&!soloCanRecover)return "No living free teammate can complete a rescue";
            return "";
        }
        // Gate for starting and completing Extract at the camp gate; finishing it while this holds ends the level with Success.
        bool CanExtractNow(PlayerState p)=>State.FriendFound&&Distance(State.FriendPosition.X,State.FriendPosition.Z,Festivals.CampGateX,Festivals.CampGateZ)<=3&&Near(p,Festivals.CampGateX,Festivals.CampGateZ);
        bool EndIfLevelOver(){var result=LevelEndCheck();if(result!="")End(result);return result!="";}
    }
}
