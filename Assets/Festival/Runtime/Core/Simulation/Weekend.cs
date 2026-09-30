using System;

namespace Festival.Core
{
    /// <summary>Festival weekends: how each level ends and who may head back to camp.</summary>
    public sealed partial class FestivalSimulation
    {
        // Per-level outcome, checked at the start and end of every Playing step: a Result string ends the level, "" keeps
        // playing. Days settle on the sales quota (DayQuota.cs), nights are the rescue, and a wiped crew loses any level.
        // Night 2 also brings everyone home: CanExtractNow waits for the whole crew (Bodies.cs).
        string LevelEndCheck()
        {
            if(State.ElapsedSeconds>=State.DurationSeconds)return DayLevel?SundownResult():"Time expired";
            var connected=State.Players.FindAll(p=>p.Connected);
            bool soloCanRecover=connected.Count==1&&(connected[0].Life=="Downed"||connected[0].Life=="Spirit"&&connected[0].RevivalCount<2);
            if(!connected.Exists(p=>p.Life=="Alive"||p.Life=="Detained")&&!soloCanRecover)return "No living free teammate can complete a rescue";
            return "";
        }
        // Gate for starting and completing Extract at the camp gate; finishing it while this holds ends the level with Success.
        // Night 2 is all-or-nothing: every connected player must be home, the dead carried inside the gate radius.
        bool CanExtractNow(PlayerState p)=>DayLevel?CanLeaveDayEarly(p):State.FriendFound&&Distance(State.FriendPosition.X,State.FriendPosition.Z,Festivals.CampGateX,Festivals.CampGateZ)<=3&&Near(p,Festivals.CampGateX,Festivals.CampGateZ)&&(!Finale||EveryoneHome());
        bool EndIfLevelOver(){var result=LevelEndCheck();if(result!="")End(result);return result!="";}

        // BeginCampReview has built the next, reseeded round (fresh $20, no gear, no effects, calm crowd).
        // A cleared level moves on and keeps cash, gear and stash cash; clearing Night 2 moves to the next
        // festival (encore lap after the last) with a fresh start; any failure restarts this festival at Day 1.
        void AdvanceWeekend(RoundState old)
        {
            var next=State;bool cleared=old.Result=="Success";bool continuing=cleared&&old.LevelIndex<Festivals.LevelCount-1;
            next.FestivalIndex=old.FestivalIndex;next.EncoreTier=old.EncoreTier;next.UnlockedFestivalCount=old.UnlockedFestivalCount;
            if(continuing)next.LevelIndex=old.LevelIndex+1;
            else if(cleared)
            {
                next.UnlockedFestivalCount=Math.Min(Festivals.Count,Math.Max(old.UnlockedFestivalCount,old.FestivalIndex+2));
                if(old.FestivalIndex+1<Festivals.Count)next.FestivalIndex=old.FestivalIndex+1;else{next.FestivalIndex=0;next.EncoreTier++;}
            }
            next.DurationSeconds=Festivals.For(next).DurationSeconds;
            if(!continuing)return;
            next.StashCash=old.StashCash;
            foreach(var before in old.Players){var after=Player(before.Id);after.Cash=before.Cash;after.Inventory=before.Inventory;after.EquippedItemId=before.EquippedItemId;}
        }
        CommandResult ChooseFestival(PlayerState p,GameCommand c)
        {
            if(p.Id!=State.HostPlayerId)return Reject("Only the host picks the festival");
            if(State.Phase!="Shopping"||State.LevelIndex!=0)return Reject("Pick the festival at camp before Day 1");
            if(c.Amount<0||c.Amount>=State.UnlockedFestivalCount)return Reject("Clear the earlier festivals to unlock that one");
            State.FestivalIndex=c.Amount;return Ok("This weekend: "+Festivals.Name(c.Amount));
        }
    }
}
