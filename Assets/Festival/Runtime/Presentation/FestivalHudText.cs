using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        /// <summary>Day levels: the level's sales against the whole crew's quota in the viewer's money, until it is met. Nights have no quota.</summary>
        public static string Quota(RoundState s,PlayerState viewer)
        {
            if(Festivals.For(s).Night)return "";
            return FestivalSimulation.DayQuotaMet(s)?"QUOTA MET — HEAD BACK TO CAMP":("DAY QUOTA  "+Money(s,viewer,s.LevelSales)+" / "+Money(s,viewer,FestivalSimulation.DayQuota(s))).ToUpperInvariant();
        }

        /// <summary>The line under the quota: how much is left to sell, or that anyone may now end the day.</summary>
        public static string QuotaHint(RoundState s,PlayerState viewer)
        {
            if(Festivals.For(s).Night)return "";
            return FestivalSimulation.DayQuotaMet(s)?"Anyone can end the day at the way back to camp, or keep selling until sundown.":"Sell "+Money(s,viewer,FestivalSimulation.DayQuota(s)-s.LevelSales)+" more before sundown.";
        }

        /// <summary>Objective card heading: the outcome at results, the quota for a free player on a day, otherwise the guidance headline.</summary>
        public static string ObjectiveTitle(RoundState s,PlayerState p)=>s.Phase=="Results"?OutcomeTitle(s):SellingToday(s,p)?Quota(s,p):FestivalGuidance.Headline(s,p);

        /// <summary>Objective card detail, matching ObjectiveTitle. At camp the host is offered the festival pick instead of the shopping line.
        /// A Ferris wheel rider reads what they spot from up there instead of the quota's line (FestivalGuidance.Hint, POLO-1).</summary>
        public static string ObjectiveDetail(RoundState s,PlayerState p)
        {
            if(s.Phase=="Results")return OutcomeDetail(s);
            if(s.Phase=="Shopping"){string choice=FestivalChoice(s,p.Id);return choice!=""?choice:"Browse gear • pay the seller • meet at the trailhead";}
            return SellingToday(s,p)&&!FestivalSimulation.OnWheel(s,p.Id)?QuotaHint(s,p):FestivalGuidance.Hint(s,p);
        }
        // Downed, detained and spirit players keep the guidance about getting back on their feet.
        static bool SellingToday(RoundState s,PlayerState p)=>s.Phase=="Playing"&&p.Life=="Alive"&&!Festivals.For(s).Night;

        /// <summary>POLO-2: whether p's HUD offers Cancel for what they are doing. Nothing cancels a turn on the Ferris wheel (the
        /// host refuses it), so nothing offers to; an Ember Playa art car rider still hops off.</summary>
        public static bool CanCancel(RoundState s,PlayerState p)=>p.InteractionId!=""&&!FestivalSimulation.OnWheel(s,p.Id);
        /// <summary>POLO-2: a Ferris wheel rider's prompt, in place of an action: what the turn has left at `now` (the view's
        /// simulation clock), in whole seconds rounded up like Clock. "" off the wheel.</summary>
        public static string WheelPrompt(RoundState s,PlayerState p,double now)
        {
            var ride=s.Interactions.Find(i=>i.PlayerId==p.Id&&i.Kind==FestivalSimulation.RideWheelKind&&i.Status=="Active");
            return ride==null?"":"ON THE FERRIS WHEEL"+Dot+(int)Math.Ceiling(Math.Max(0,ride.StartSeconds+ride.DurationSeconds-now))+" s";
        }

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
            return (!level.Night?"QUOTA MET":finale?"EVERYONE HOME":s.SecondFriend.Active?"FRIENDS RESCUED":"FRIEND RESCUED")+Dot+(finale?Festivals.Name(s.FestivalIndex):level.Name).ToUpperInvariant()+" CLEARED";
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

        /// <summary>The debrief card's heading: the verdict on the round just played and how it went.</summary>
        public static string ReviewHeadline(RoundState s,PlayerState viewer)=>"THE VERY OFFICIAL ROUND REVIEW\n"+ReviewOutcome(s)+Dot+"SALES "+Money(s,viewer,s.ReviewSales).ToUpperInvariant()+Dot+"SURVIVORS "+s.ReviewSurvivors+Dot+"CAMP ANTICS "+s.ReviewAntics;

        /// <summary>The debrief vote is open: every award drawn, not every vote in. The pointer is free to click a friend.</summary>
        public static bool VoteOpen(RoundState s)=>s.Phase=="CampReview"&&!FestivalSimulation.ReviewRevealed(s);

        /// <summary>An award's row on the local player's ballot, with their own pick. A view carries no one else's picks before the reveal.</summary>
        public static string Ballot(RoundState s,string localId,int award)
        {
            var mine=s.ReviewVotes.Find(v=>v.PlayerId==localId&&v.Award==award);
            return (award+1)+"  "+s.ReviewAwards[award].ToUpperInvariant()+Dot+(mine==null?"PICK A FRIEND":"YOUR PICK: "+CrewName(s,localId,mine.TargetId));
        }

        /// <summary>
        /// The debrief card's foot: while the vote is open, how to vote and who has voted on every award (never whom they picked);
        /// once the verdict has played, the host's key to open the shop.
        /// </summary>
        public static string ReviewStatus(RoundState s,string localId,double revealSeconds)
        {
            if(VoteOpen(s))
            {
                var voted=new List<string>();var waiting=new List<string>();
                foreach(var p in s.Players)if(p.Connected)(FestivalSimulation.VotedOnEveryAward(s,p.Id)?voted:waiting).Add(CrewName(s,localId,p.Id));
                return "CLICK A FRIEND FOR EACH AWARD, OR PRESS 1–3\n"+(voted.Count>0?"VOTED  "+string.Join(", ",voted)+Dot:"")+"WAITING ON  "+string.Join(", ",waiting);
            }
            if(!RevealDone(s,revealSeconds))return "";
            return s.HostPlayerId==localId?"E  OPEN THE CAMP SHOP":"WAITING FOR THE HOST TO OPEN THE SHOP";
        }

        /// <summary>How long each award's winner waits for its drumroll, and then the shots.</summary>
        public const double RevealStepSeconds=2;
        /// <summary>
        /// The verdict, revealSeconds after this client saw every vote in: the awards, a winner every RevealStepSeconds, then who
        /// takes a shot (FestivalSimulation.TakesShot) as the next level starts. "" while the vote is open.
        /// </summary>
        public static string ReviewReveal(RoundState s,string localId,double revealSeconds)
        {
            if(s.Phase!="CampReview"||VoteOpen(s))return "";
            if(s.ReviewAwards.Count==0)return "SOLO PRACTICE: NO AWARDS TONIGHT";
            string text="AND THE AWARDS GO TO…";
            // A winner's shots, in the order their awards were read out.
            var drinkers=new List<string>();var shots=new List<int>();
            for(int slot=0;slot<s.ReviewAwards.Count;slot++)
            {
                string winner=s.ReviewWinners[slot];
                text+="\n"+s.ReviewAwards[slot].ToUpperInvariant()+Dot+(revealSeconds>=(slot+1)*RevealStepSeconds?CrewName(s,localId,winner):"?");
                if(!FestivalSimulation.TakesShot(s.ReviewAwards[slot]))continue;
                int at=drinkers.IndexOf(winner);if(at<0){drinkers.Add(winner);shots.Add(1);}else shots[at]++;
            }
            if(!RevealDone(s,revealSeconds))return text;
            for(int i=0;i<drinkers.Count;i++)text+="\n"+(drinkers[i]==localId?"YOU TAKE":CrewName(s,localId,drinkers[i])+" TAKES")+(shots[i]==1?" A SHOT":" "+shots[i]+" SHOTS");
            return text;
        }
        /// <summary>The verdict has played out to the shots, so the host may open the shop; straight away when solo practice drew no awards.</summary>
        public static bool RevealDone(RoundState s,double revealSeconds)=>s.ReviewAwards.Count==0||revealSeconds>=(s.ReviewAwards.Count+1)*RevealStepSeconds;

        /// <summary>
        /// A player's floating name, with the awards they won at the last debrief through the next level, until its results. Never at
        /// the campfire itself: the badges land with the last vote, and a tag must not give a winner away before the reveal reads it out.
        /// </summary>
        public static string Nameplate(RoundState s,PlayerState p)=>p.Badge==""||s.Phase=="CampReview"?p.Name:p.Name+Dot+p.Badge;

        /// <summary>An amount of money as the viewer reads it (PLAYA-1): "$12" at Palm Mirage, "12 buttons" or their own odd object on Ember Playa.</summary>
        public static string Money(RoundState s,PlayerState viewer,int amount)=>Festivals.CurrencyName(s.FestivalIndex,viewer.Ordinal,amount);
        /// <summary>A line the host wrote in dollars (a command's reply, an item's description), with every "$5" in it as the viewer reads it.</summary>
        public static string MoneyText(RoundState s,PlayerState viewer,string text)=>string.IsNullOrEmpty(text)?text:Dollars.Replace(text,m=>Money(s,viewer,int.Parse(m.Groups[1].Value)));
        // At most nine digits, so an amount always fits an int.
        static readonly Regex Dollars=new Regex(@"\$(\d{1,9})");

        /// <summary>A crew member as the local player reads them in the debrief: "YOU", or their name.</summary>
        internal static string CrewName(RoundState s,string localId,string id)=>id==localId?"YOU":s.Players.Find(p=>p.Id==id)?.Name.ToUpperInvariant()??"A FRIEND";

        /// <summary>
        /// Who trips this level and on how many doses, from the public spin result, while the crew is out (the spinner names them
        /// before that, and camp still holds the last level's). Night 2 doses everyone, so the rest of the crew's doses follow,
        /// three to a line. A spirit's view lists only spirits: a living tripper is "A FRIEND" and the living's doses go unlisted.
        /// </summary>
        public static string Tripping(RoundState s,string localId)
        {
            if(s.TripperId==""||s.Phase!="Loading"&&s.Phase!="Playing")return "";
            int dose=FestivalSimulation.TripperDose(s);
            string text=(s.TripperId==localId?"YOU TRIP":(s.Players.Find(p=>p.Id==s.TripperId)?.Name.ToUpperInvariant()??"A FRIEND")+" TRIPS")+Dot+dose+(dose==1?" DOSE":" DOSES");
            if(s.LevelIndex!=Festivals.LevelCount-1)return text;
            var rest=new List<string>();
            foreach(var d in s.Doses)
            {
                var p=s.Players.Find(x=>x.Id==d.PlayerId);
                if(p!=null&&p.Connected&&p.Id!=s.TripperId)rest.Add((p.Id==localId?"YOU":p.Name.ToUpperInvariant())+" "+d.Dose);
            }
            for(int i=0;i<rest.Count;i+=3)text+="\n"+(i==0?"DOSED  ":"")+string.Join(Dot,rest.GetRange(i,Math.Min(3,rest.Count-i)));
            return text;
        }

        /// <summary>How long the tripper's one hint stays up as each level starts.</summary>
        public const double TrustSeconds=10;
        /// <summary>The game's only hint that visions lie, "Trust, but verify.": the tripper's alone, once per level, as play starts.</summary>
        public static string TrustLine(RoundState s,string localId)=>s.Phase=="Playing"&&s.TripperId==localId&&s.ElapsedSeconds<TrustSeconds?"Trust, but verify.":"";

        /// <summary>
        /// The festivalgoer F chats with right now (FestivalHud.ChatKey), among those within FestivalSimulation.ConfirmReach that
        /// FestivalSimulation.MayConfirm lets p chat-check: the tripper's festivalgoers they still have an unchecked vision about, or,
        /// only with none of those in reach (VISION-3), Palm Mirage's VIP guard, who talks anyone with a free hand past the rope
        /// (POLO-1). A free one before one FestivalSimulation.Engaged, which the HUD says is busy, then the nearest; null otherwise.
        /// </summary>
        public static NpcState CheckTarget(RoundState s,PlayerState p)
        {
            if(s.Phase!="Playing"||p.Life!="Alive"||p.InteractionId!="")return null;
            NpcState best=null;double reach=0;int rank=int.MaxValue;
            foreach(var n in s.Npcs)
            {
                double dx=p.X-n.X,dz=p.Z-n.Z,distance=Math.Sqrt(dx*dx+dz*dz);
                if(distance>FestivalSimulation.ConfirmReach||!FestivalSimulation.MayConfirm(s,p,n,"ConfirmChat"))continue;
                int order=(FestivalSimulation.CanCheckVision(s,p,n)?0:2)+(FestivalSimulation.Engaged(s,n)?1:0);
                if(order<rank||order==rank&&distance<reach){best=n;rank=order;reach=distance;}
            }
            return best;
        }
        /// <summary>The checks, as beside anyone else: E dances (quick, but a miss draws the crowd's eye), F chats (safe).</summary>
        public static readonly string CheckDanceAction="Check by dancing"+Dot+"F CHECK BY CHAT ("+FestivalSimulation.ConfirmChatSeconds+" s)";
        public static readonly string CheckChatAction="Check by chatting ("+FestivalSimulation.ConfirmChatSeconds+" s, safe)";
        /// <summary>Beside someone the host won't let anyone check right now (FestivalSimulation.Engaged): said, not offered.</summary>
        public const string BusyAction="They're busy right now";
        /// <summary>Beside the VIP guard with no vision about them to check, E or F chats them into handing over a VIP wristband.</summary>
        public static readonly string RopeChatAction="Talk your way past the VIP rope ("+FestivalSimulation.ConfirmChatSeconds+" s)";

        /// <summary>
        /// The chat check under way: the festivalgoer's opener, then the tripper's three questions on 1-3, the picked one followed by
        /// their answer. "" without one; a view carries only its own player's chat, so no other client can read it.
        /// </summary>
        public static string ChatPicker(RoundState s,PlayerState p,int picked)
        {
            var chat=s.Interactions.Find(i=>i.Id==p.InteractionId&&i.Kind=="ConfirmChat"&&i.Status=="Active")?.Chat;
            if(chat==null)return "";
            string text="CHECKING BY CHAT"+Dot+"1–3 ASK\n\""+chat.Opener+"\"";
            for(int i=0;i<chat.Questions.Length;i++)text+="\n"+(i+1)+"  "+chat.Questions[i]+(i==picked?"\n      \""+chat.Answers[i]+"\"":"");
            return text;
        }

        /// <summary>
        /// The player card's warning: security stopping or about to detain p, else the crowd's worst suspicion, else "CROWD CLEAR";
        /// then p's effects.
        /// The warning always comes first: the spinner's dose lasts the whole level (everyone's on Night 2) and must never hide it.
        /// </summary>
        public static string Condition(RoundState s,PlayerState p)
        {
            double suspicion=0;string mode="clear";
            foreach(var npc in s.Npcs)if(npc.Kind=="Wook"&&npc.Suspicion>suspicion){suspicion=npc.Suspicion;mode=npc.Mode;}
            // Security on you outranks the crowd: an arrest warning detains you in seconds, and after a deal in view some festivalgoer
            // nearly always holds a little suspicion. The worse of two cops on you shows; a cop on patrol is no warning.
            bool Security(string copMode)=>s.Npcs.Exists(n=>n.Kind=="Cop"&&n.TargetId==p.Id&&n.Mode==copMode);
            string warning=Security("ArrestWarning")?"ARREST WARNING":Security("Stop")?"SECURITY STOP"
                :suspicion>0?"CROWD "+suspicion.ToString("0")+" / "+mode.ToUpperInvariant():"CROWD CLEAR";
            return p.Effects.Count>0?warning+Dot+Catalog.EffectsLine(p.Effects).ToUpperInvariant():warning;
        }

        /// <summary>
        /// The player card: health, pocket and the crew's stash, then the level's sales, life and the warning (Condition). The warning
        /// leads and the effects trail it, so a long effects list wraps off the card before the warning does.
        /// </summary>
        public static string Vitals(RoundState s,PlayerState p)=>("HP "+p.Health+Dot+Money(s,p,p.Cash)+Dot+"STASH "+Money(s,p,s.StashCash)+"\n"
            +"SALES "+Money(s,p,s.LevelSales)+Dot+p.Life).ToUpperInvariant()+Dot+Condition(s,p);

        /// <summary>The rhythm lane's title, naming a check dance as the check it is.</summary>
        public static string RhythmTitle(string kind)=>(kind=="ConfirmDance"?"CHECK DANCE":kind.ToUpperInvariant())+"  /  FOUR-LANE";
    }
}
