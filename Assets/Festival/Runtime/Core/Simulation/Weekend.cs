using System;

namespace Festival.Core
{
    /// <summary>Festival weekends: how each level ends and who may head back to camp.</summary>
    public sealed partial class FestivalSimulation
    {
        // DEV-1: session-only capability; Unity enables it only in Editor/development hosted games.
        public bool DevelopmentFestivalSelection;
        // TEST MODE, for the host's own playthroughs, needs that capability too, so a release host refuses all three commands.
        public const string TestModeKind="TestMode",TestFreeKind="TestFree",TestClearLevelKind="TestClearLevel";
        bool TestModeOn=>DevelopmentFestivalSelection&&State.TestMode;
        CommandResult TestCommand(PlayerState p,GameCommand c)
        {
            if(!DevelopmentFestivalSelection)return Reject("Test mode is only in test builds");
            if(c.Kind==TestFreeKind)
            {
                if(!State.TestMode)return Reject("Test mode is off");
                if(State.Phase!="Playing"||p.Life!="Detained")return Reject("Free me is for a detained player");
                Free(p);return Ok("Freed (test)");
            }
            if(p.Id!=State.HostPlayerId)return Reject("Only the host runs test mode");
            // The results heading reads TestMode, so the mode a level ended in holds until camp.
            if(c.Kind==TestModeKind){if(State.Phase=="Results")return Reject("Switch test mode while a level plays or at camp");State.TestMode=!State.TestMode;return Ok(State.TestMode?"TEST MODE on":"TEST MODE off");}
            if(!State.TestMode)return Reject("Test mode is off");
            if(State.Phase!="Playing")return Reject("Clear a level while it plays");
            End("Success");return Ok("Level cleared (test)");
        }

        // Per-level outcome, checked at the start and end of every Playing step: a Result string ends the level, "" keeps
        // playing. Days settle on the sales quota (DayQuota.cs), nights are the rescue, and a wiped crew loses any level.
        // Night 2 also brings everyone home: CanExtractNow waits for the whole crew (Bodies.cs).
        string LevelEndCheck()
        {
            // TEST MODE lifts only the time limit: the clock runs on, so the schedules that read ElapsedSeconds keep their times.
            if(!TestModeOn&&State.ElapsedSeconds>=State.DurationSeconds)return DayLevel?SundownResult():"Time expired";
            var connected=State.Players.FindAll(p=>p.Connected);
            bool soloCanRecover=connected.Count==1&&(connected[0].Life=="Downed"||connected[0].Life=="Spirit"&&FestivalGuidance.RevivalsLeft(connected[0])>0);
            if(!connected.Exists(p=>p.Life=="Alive"||p.Life=="Detained")&&!soloCanRecover)return WipedOut;
            return "";
        }
        /// <summary>The result a level ends on when nobody living and free is left to finish it; the HUD calls it a wipe (LOOP-5).</summary>
        public const string WipedOut="No living free teammate can complete a rescue";
        // Gate for starting and completing Extract at the camp gate; finishing it while this holds ends the level with Success.
        // A night needs every lost friend back (SplitObjective.cs). Night 2 is all-or-nothing: every connected player must be
        // home, the dead carried inside the gate radius.
        // Public and static so the HUD offers Extract, first under E, exactly where the sim takes it (HUD-1). Reads a living player's view.
        public static bool CanExtract(RoundState s,PlayerState p)=>!Festivals.For(s).Night?CanLeaveDayEarly(s,p):FriendsBack(s)&&Near(p,Festivals.CampGateX,Festivals.CampGateZ)&&(s.LevelIndex!=Festivals.LevelCount-1||EveryoneHome(s));
        bool CanExtractNow(PlayerState p)=>CanExtract(State,p);
        bool EndIfLevelOver(){var result=LevelEndCheck();if(result!="")End(result);return result!="";}

        // BeginCampReview has built the next, reseeded round (fresh $20, no gear, no effects, calm crowd).
        // A cleared level moves on and keeps cash, gear, stash cash and the stashes (the boxes placed and what is hidden in them,
        // with the entity counter so later boxes get fresh ids), but no Giggle Tank still out; clearing Night 2 moves to the next
        // festival (encore lap after the last) with a fresh start; any failure restarts this festival at Day 1.
        void AdvanceWeekend(RoundState old)
        {
            var next=State;bool cleared=old.Result=="Success";bool continuing=cleared&&old.LevelIndex<Festivals.LevelCount-1;
            if(cleared)UnlockNextFestival(old); // End already did it; also handles older Results snapshots. Development sessions never earn unlocks.
            next.FestivalIndex=old.FestivalIndex;next.EncoreTier=old.EncoreTier;next.UnlockedFestivalCount=old.UnlockedFestivalCount;
            if(continuing)next.LevelIndex=old.LevelIndex+1;
            else if(cleared)
            {
                if(old.FestivalIndex+1<Festivals.Count)next.FestivalIndex=old.FestivalIndex+1;else{next.FestivalIndex=0;next.EncoreTier++;}
            }
            next.DurationSeconds=Festivals.For(next).DurationSeconds;
            if(!continuing)return;
            next.StashCash=old.StashCash;next.TripperBag=old.TripperBag;next.MinigameBag=old.MinigameBag;next.Stashes=old.Stashes;next.EntitySequence=Math.Max(next.EntitySequence,old.EntitySequence);
            foreach(var before in old.Players){var after=Player(before.Id);after.Cash=before.Cash;after.Inventory=before.Inventory;after.EquippedItemId=before.EquippedItemId;LoseGiggleTank(after);}
            int stashed=0;foreach(var stash in next.Stashes)stashed+=stash.Items.RemoveAll(x=>x.ItemId==GiggleTankItem);if(stashed>0)CrewNews(null,"Stash","STASH  •  −"+stashed+" Giggle Tank  •  tanks never leave the level");
        }
        // Clearing Night 2 of festival k unlocks k+1. End runs this on the win itself, so the host's profile saves the unlock
        // even when they leave from the results screen instead of pressing NEXT CAMP.
        void UnlockNextFestival(RoundState s){if(!DevelopmentFestivalSelection&&s.LevelIndex==Festivals.LevelCount-1)s.UnlockedFestivalCount=Math.Min(Festivals.Count,Math.Max(s.UnlockedFestivalCount,s.FestivalIndex+2));}
        CommandResult ChooseFestival(PlayerState p,GameCommand c)
        {
            if(p.Id!=State.HostPlayerId)return Reject("Only the host picks the festival");
            if(State.Phase!="Shopping"||State.LevelIndex!=0)return Reject("Pick the festival at camp before Day 1");
            if(c.Amount<0||c.Amount>=(DevelopmentFestivalSelection?Festivals.Count:State.UnlockedFestivalCount))return Reject("Clear the earlier festivals to unlock that one");
            State.FestivalIndex=c.Amount;return Ok("This weekend: "+Festivals.Name(c.Amount));
        }
    }
}
