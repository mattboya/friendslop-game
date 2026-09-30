using System;
using Festival.Core;

namespace Festival.Presentation
{
    /// <summary>
    /// The weekend text the HUD shows, built only from the viewer's filtered round state so EditMode tests
    /// can read it without a scene. FestivalHud puts these strings on screen and wires the commands.
    /// </summary>
    public static class FestivalHudText
    {
        const string Dot="  •  ";

        /// <summary>"PALM MIRAGE  •  DAY 1", plus the encore lap once there is one. At camp it names the level coming up.</summary>
        public static string LevelBanner(RoundState s)
        {
            string banner=Festivals.Name(s.FestivalIndex).ToUpperInvariant()+Dot+Festivals.For(s).Name.ToUpperInvariant()+(s.EncoreTier>0?Dot+"ENCORE "+s.EncoreTier:"");
            return s.Phase=="Shopping"||s.Phase=="CampReview"?"UP NEXT"+Dot+banner:banner;
        }

        /// <summary>Time left while playing, in whole seconds rounded up so 0:00 means the level is over; one word in every other phase.</summary>
        public static string Clock(RoundState s)
        {
            if(s.Phase!="Playing")return s.Phase=="Shopping"?"CAMP":s.Phase=="CampReview"?"REVIEW":s.Phase=="Results"?"DONE":"WAIT";
            int left=(int)Math.Ceiling(Math.Max(0,s.DurationSeconds-s.ElapsedSeconds));
            return left/60+":"+(left%60).ToString("00");
        }

        /// <summary>Day levels: the level's sales against the whole crew's quota, until it is met. Nights have no quota.</summary>
        public static string Quota(RoundState s)
        {
            if(Festivals.For(s).Night)return "";
            return FestivalSimulation.DayQuotaMet(s)?"QUOTA MET — HEAD BACK TO CAMP":"DAY QUOTA  $"+s.LevelSales+" / $"+FestivalSimulation.DayQuota(s);
        }

        /// <summary>The line under the quota: how much is left to sell, or that anyone may now end the day.</summary>
        public static string QuotaHint(RoundState s)
        {
            if(Festivals.For(s).Night)return "";
            return FestivalSimulation.DayQuotaMet(s)?"Anyone can end the day at the way back to camp, or keep selling until sundown.":"Sell $"+(FestivalSimulation.DayQuota(s)-s.LevelSales)+" more before sundown.";
        }

        /// <summary>Objective card heading: the outcome at results, the quota for a free player on a day, otherwise the guidance headline.</summary>
        public static string ObjectiveTitle(RoundState s,PlayerState p)=>s.Phase=="Results"?OutcomeTitle(s):SellingToday(s,p)?Quota(s):FestivalGuidance.Headline(s,p);

        /// <summary>Objective card detail, matching ObjectiveTitle. At camp the host is offered the festival pick instead of the shopping line.</summary>
        public static string ObjectiveDetail(RoundState s,PlayerState p)
        {
            if(s.Phase=="Results")return OutcomeDetail(s);
            if(s.Phase=="Shopping"){string choice=FestivalChoice(s,p.Id);return choice!=""?choice:"Browse gear • pay the seller • meet at the trailhead";}
            return SellingToday(s,p)?QuotaHint(s):FestivalGuidance.Hint(s,p);
        }
        // Downed, detained and spirit players keep the guidance about getting back on their feet.
        static bool SellingToday(RoundState s,PlayerState p)=>s.Phase=="Playing"&&p.Life=="Alive"&&!Festivals.For(s).Night;

        /// <summary>The Extract action at the way back to camp, or "" while there is nothing to finish there.</summary>
        public static string ExtractAction(RoundState s)
        {
            if(!Festivals.For(s).Night)return FestivalSimulation.DayQuotaMet(s)?"End the day: head back to camp":"";
            return s.FriendFound?"Head back to camp with your friend":"";
        }

        /// <summary>
        /// Night 2 only: each connected crew member and whether they count as home, the dead by their body.
        /// Nobody waits for a player who left. A spirit's view lists only spirits, so the living a spirit cannot see
        /// are counted from ConnectedCrewCount and never guessed home: "HOME  ? / 3" and "2 LIVING  •  UNSEEN".
        /// </summary>
        public static string HomeChecklist(RoundState s,string localId)
        {
            if(s.Phase!="Playing"||s.LevelIndex!=Festivals.LevelCount-1)return "";
            int home=0,crew=0;string lines="";
            foreach(var p in s.Players)
            {
                if(!p.Connected)continue;
                bool isHome=FestivalSimulation.Home(s,p);crew++;if(isHome)home++;
                lines+="\n"+(p.Id==localId?"YOU":p.Name.ToUpperInvariant())+Dot+(isHome?"HOME":p.Life=="Spirit"?"BODY AWAY":p.Life=="Detained"?"DETAINED":"AWAY");
            }
            int unseen=s.ConnectedCrewCount-crew;
            return unseen>0?"HOME  ? / "+s.ConnectedCrewCount+lines+"\n"+unseen+" LIVING"+Dot+"UNSEEN":"HOME  "+home+" / "+crew+lines;
        }

        /// <summary>
        /// The CarryBody action p gets for body b (the command toggles), or "" when there is none:
        /// pick it up, take the other end, or put it down, saying when it is too heavy to move alone.
        /// </summary>
        public static string BodyAction(RoundState s,PlayerState p,BodyState b)
        {
            if(p.Life!="Alive")return "";
            string body=(s.Players.Find(x=>x.Id==b.PlayerId)?.Name??"a friend")+"'s body";
            if(p.CarryBodyId==b.PlayerId)return "Put down "+body+(FestivalSimulation.BodyMoves(s,b)?"":Dot+"too heavy alone: get a second carrier");
            double dx=p.X-b.X,dz=p.Z-b.Z;
            if(p.CarryBodyId!=""||p.DragTargetId!=""||Math.Sqrt(dx*dx+dz*dz)>FestivalSimulation.CarryReach)return "";
            int carriers=FestivalSimulation.CarrierCount(s,b);
            // CarryBody refuses a third carrier.
            return carriers>=2?"":carriers==1?"Take the other end of "+body:"Carry "+body;
        }

        /// <summary>The host's festival pick at camp before Day 1, once a second festival is unlocked. F moves to the next unlocked one.</summary>
        public static string FestivalChoice(RoundState s,string localId)
        {
            if(s.Phase!="Shopping"||s.LevelIndex!=0||s.HostPlayerId!=localId||s.UnlockedFestivalCount<2)return "";
            int next=NextFestival(s);
            return "F  switch to "+Festivals.Name(next)+" ("+(next+1)+" of "+s.UnlockedFestivalCount+" unlocked)";
        }
        /// <summary>The festival F picks: the next unlocked one, wrapping round to the first.</summary>
        public static int NextFestival(RoundState s)=>(s.FestivalIndex+1)%s.UnlockedFestivalCount;

        /// <summary>Results heading: the level the crew cleared, or that the weekend is over.</summary>
        public static string OutcomeTitle(RoundState s)
        {
            if(s.Result!="Success")return "WEEKEND OVER"+Dot+"BACK TO DAY 1";
            var level=Festivals.For(s);bool finale=s.LevelIndex==Festivals.LevelCount-1;
            return (!level.Night?"QUOTA MET":finale?"EVERYONE HOME":"FRIEND RESCUED")+Dot+(finale?Festivals.Name(s.FestivalIndex):level.Name).ToUpperInvariant()+" CLEARED";
        }

        /// <summary>Results detail: why the weekend ended, or what comes next and what the crew keeps.</summary>
        public static string OutcomeDetail(RoundState s)
        {
            if(s.Result!="Success")return s.Result+". "+Festivals.Name(s.FestivalIndex)+" restarts at Day 1 with fresh cash.";
            if(s.LevelIndex<Festivals.LevelCount-1)return "Next up: "+Festivals.Level(s.FestivalIndex,s.LevelIndex+1,s.EncoreTier).Name+". Cash and gear carry over.";
            return (s.FestivalIndex+1<Festivals.Count?"Next up: "+Festivals.Name(s.FestivalIndex+1):"Next up: an encore at "+Festivals.Name(0)+", harder")+". A new weekend starts with fresh cash.";
        }

        /// <summary>The prompt at results: only the host can bring the crew back to camp.</summary>
        public static string ResultsPrompt(RoundState s,string localId)=>s.HostPlayerId==localId?"ESC MENU  •  NEXT CAMP":"WAITING FOR THE HOST";

        /// <summary>The debrief's verdict. The campfire already holds the next round, so a win names the level before this one.</summary>
        public static string ReviewOutcome(RoundState s)
        {
            if(s.ReviewResult!="Success")return "A GLORIOUS DISASTER";
            return s.LevelIndex==0?"WEEKEND CLEARED":Festivals.Level(s.FestivalIndex,s.LevelIndex-1,s.EncoreTier).Name.ToUpperInvariant()+" CLEARED";
        }
    }
}
