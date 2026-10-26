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
    public static partial class FestivalHudText
    {
        const string Dot="  •  ";

        /// <summary>"PALM MIRAGE  •  DAY 1", plus the encore lap once there is one. At camp it names the level coming up, or, after a
        /// lost level (LOOP-5), that the weekend went back to Day 1: "BACK TO DAY 1  •  PALM MIRAGE", until the crew readies.</summary>
        public static string LevelBanner(RoundState s)
        {
            string festival=Festivals.Name(s.FestivalIndex).ToUpperInvariant(),encore=s.EncoreTier>0?Dot+"ENCORE "+s.EncoreTier:"",banner=festival+Dot+Festivals.For(s).Name.ToUpperInvariant()+encore;
            if(s.Phase!="Shopping"&&s.Phase!="CampReview")return banner;
            // A new crew's first camp has no round behind it, so it reads as the fresh start it is.
            return s.ReviewResult!=""&&s.ReviewResult!="Success"?"BACK TO DAY 1"+Dot+festival+encore:"UP NEXT"+Dot+banner;
        }

        public const string NoLimit="NO LIMIT",TestModeTag="TEST MODE",FreeMeAction="Free me (test)",ClearLevelButton="CLEAR LEVEL (TEST)";
        /// <summary>Time left while playing, in whole seconds rounded up so 0:00 means the level is over; one word in every other phase.
        /// NO LIMIT while test mode is on.
        /// MINI-1: a minigame stops it, so while one runs it stands at the time left.</summary>
        public static string Clock(RoundState s)
        {
            if(s.Phase!="Playing"&&s.Phase!=FestivalSimulation.MinigamePhase)return s.Phase=="Shopping"?"CAMP":s.Phase=="CampReview"?"REVIEW":s.Phase=="Results"?"DONE":"WAIT";
            // Test mode's level never ends on time, though its clock runs on past the level's length.
            if(s.TestMode)return NoLimit;
            int left=(int)Math.Ceiling(Math.Max(0,s.DurationSeconds-s.ElapsedSeconds));
            return left/60+":"+(left%60).ToString("00");
        }

        /// <summary>The festival map's side text: the mission, the crew, where help is, and each stage's song.</summary>
        public static string MapText(RoundState state,PlayerState player)
        {
            string team="";foreach(var p in state.Players)team+=(p.Id==player.Id?"YOU":p.Name)+"  /  "+p.Life.ToUpperInvariant()+"\n";
            string mission=!Festivals.For(state).Night?Quota(state,player)+"\n"+QuotaHint(state,player):state.FriendFound?"FRIEND FOUND\nEscort them back to camp.":"FRIEND MISSING\nSearch the grounds. Their exact location is unknown.";
            return mission+"\n\nCREW\n"+team+"\nYOU ARE THE ORANGE DOT.\n\nMEDIC  /  HOLDING\nHelp a downed or detained friend.\n\n"+StageSongs(state);
        }
        /// <summary>Each stage's song this level, named as the camp DJ names them.</summary>
        public static string StageSongs(RoundState s)=>"MAIN STAGE  /  "+CampFeatures.Tracks[StageMusic.Track(s,StageMusic.Main)]+"\nSMALL STAGE  /  "+CampFeatures.Tracks[StageMusic.Track(s,StageMusic.Small)];

        /// <summary>Day levels: the level's sales against the whole crew's quota in the viewer's money, until it is met. Nights have no quota.</summary>
        public static string Quota(RoundState s,PlayerState viewer)
        {
            if(Festivals.For(s).Night)return "";
            return FestivalSimulation.DayQuotaMet(s)?"QUOTA MET — HEAD BACK TO CAMP":("DAY QUOTA  "+Money(s,viewer,s.LevelSales)+" / "+Money(s,viewer,FestivalSimulation.DayQuota(s))).ToUpperInvariant();
        }

        /// <summary>The line under the quota: how much is left to sell, or that anyone may now end the day. In the last minute
        /// (LOOP-5) it says what missing it costs.</summary>
        public static string QuotaHint(RoundState s,PlayerState viewer)
        {
            if(Festivals.For(s).Night)return "";
            return FestivalSimulation.DayQuotaMet(s)?"Anyone can end the day at the way back to camp, or keep selling until sundown.":"Sell "+Money(s,viewer,FestivalSimulation.DayQuota(s)-s.LevelSales)+" more before sundown"+(SundownStage(s)>0?" or the weekend restarts":"")+".";
        }

        /// <summary>LOOP-5: how close a day with the quota unmet is to sundown, which restarts the weekend at Day 1: 1 in its last
        /// minute (as the clock turns 1:00), 2 in its last 15 s, otherwise 0 (and on nights, once the quota is met, and off the
        /// festival). The HUD pops each stage's warning once.</summary>
        public static int SundownStage(RoundState s)
        {
            if((s.Phase!="Playing"&&s.Phase!=FestivalSimulation.MinigamePhase)||Festivals.For(s).Night||FestivalSimulation.DayQuotaMet(s)||s.TestMode)return 0;
            double left=s.DurationSeconds-s.ElapsedSeconds;
            return left<=15?2:left<=60?1:0;
        }
        /// <summary>LOOP-5: the warning for SundownStage, with what the crew still has to sell in the viewer's money; "" at stage 0.</summary>
        public static string SundownWarning(RoundState s,PlayerState viewer)
        {
            int stage=SundownStage(s);if(stage==0)return "";
            string more=Money(s,viewer,FestivalSimulation.DayQuota(s)-s.LevelSales);
            return stage==1?"1 MINUTE TO SUNDOWN: sell "+more+" more or the weekend restarts at Day 1":"15 SECONDS: sell "+more+" more or back to Day 1";
        }

        /// <summary>Mid-dance the clock and these warnings make way for the lanes, so for a day's last minute (SundownStage) the
        /// lanes' title line says it: "0:14 TO SUNDOWN  •  SELL $12 MORE", in the viewer's money; "" otherwise.</summary>
        public static string SundownLine(RoundState s,PlayerState viewer)=>SundownStage(s)==0?"":(Clock(s)+" TO SUNDOWN"+Dot+"SELL "+Money(s,viewer,FestivalSimulation.DayQuota(s)-s.LevelSales)+" MORE").ToUpperInvariant();

        /// <summary>Objective card heading: the outcome at results, the quota for a free player on a day, otherwise the guidance headline.</summary>
        public static string ObjectiveTitle(RoundState s,PlayerState p)=>s.Phase=="Results"?OutcomeTitle(s):SellingToday(s,p)?Quota(s,p):FestivalGuidance.Headline(s,p);

        /// <summary>Objective card detail, matching ObjectiveTitle. At camp the host is offered the festival pick instead of the shopping line;
        /// either way the card says the viewer's paid starter allowance and the crowd stock source. This card, not FestivalGuidance.Hint, is what camp reads. By day the quota's line is followed
        /// by p's next step to a sale (NextStep). A Ferris wheel rider reads what they spot from up there instead (FestivalGuidance.Hint, POLO-1).</summary>
        public static string ObjectiveDetail(RoundState s,PlayerState p,bool developmentSelection=false,string chat="F",string interact="E",string slot1="1",string slot2="2",string slot3="3")
        {
            if(s.Phase=="Results")return OutcomeDetail(s,p);
            if(s.Phase=="Shopping"){string choice=FestivalChoice(s,p.Id,developmentSelection,chat),stock=" • "+(Festivals.For(s).Night?"Tonight is a rescue. ":"")+CampStockGuidance(s,p);string camp=choice!=""?choice+stock:"Browse shelves • pay the seller"+stock+" • meet at the trailhead";string reset=CampResetDisclosure(s,p);return reset!=""?reset+"\n"+camp:camp;}
            if(!SellingToday(s,p)||FestivalSimulation.OnWheel(s,p.Id))return PressKeys(FestivalGuidance.Hint(s,p),interact,chat,slot1:slot1,slot2:slot2,slot3:slot3);
            // A paused game refuses every next step, and E is the game's own key then.
            return FestivalSimulation.DayQuotaMet(s)||s.Phase==FestivalSimulation.MinigamePhase?QuotaHint(s,p):QuotaHint(s,p)+" "+NextStep(s,p,interact);
        }
        /// <summary>Shared camp allowance/source teaching, always in the viewer's money, even before a rescue.</summary>
        public static string CampStockGuidance(RoundState s,PlayerState p)
        {
            string price=s!=null&&p!=null?Money(s,p,5):"$5";
            return (p!=null&&p.StarterStockBought>0?"Starter bought this camp.":"One starter each at camp: Tongue Stamps or Fun Guys for "+price+".")
                +" Buy more from festivalgoers ("+price+" each; some are narcs). Keep some money for more stock.";
        }
        // ECON-3 (Matt: "i dont even know how to sell"): with no stock, where it comes from (ECON-2: festivalgoers, some of them narcs).
        // Holding some, the tripper is pointed at a buyer vision (a green ball) whose check came back true (a green ring, VISION-5): a
        // true narc vision is ringed green too, so the ring alone would send them to a narc. The crew stick with the tripper.
        const string AskMore=", or ask High or Greedy for more.";
        static string NextStep(RoundState s,PlayerState p,string interact)
        {
            if(FestivalGuidance.HasReservedSale(s,p))return "Finish this deal: your offered unit is reserved until it ends.";
            if(FestivalGuidance.NeedsStock(s,p))
            {
                if(p.Cash<5)return s.StashCash>=5&&s.Stashes.Count>0
                    ?"You have nothing to sell. Withdraw "+Money(s,p,5)+" from the shared stash, then buy from festivalgoers; some are narcs."
                    :"You have nothing to sell: return lost property at the lost-property desk for "+Money(s,p,5)+", then buy from festivalgoers; some are narcs.";
                return "You have nothing to sell: buy from festivalgoers, and some are narcs.";
            }
            if(p.Id==s.TripperId)return "A green ball in a green ring is a real buyer: walk up and press "+interact+" to offer"+AskMore;
            return "Stick with "+(s.Players.Find(x=>x.Id==s.TripperId)?.Name??"the tripper")+": they see the buyers. Walk up to one and press "+interact+" to offer"+AskMore;
        }
        // Downed, detained and spirit players keep the guidance about getting back on their feet. A minigame's pot counts toward
        // the quota, so the quota stays up while one runs.
        static bool SellingToday(RoundState s,PlayerState p)=>(s.Phase=="Playing"||s.Phase==FestivalSimulation.MinigamePhase)&&p.Life=="Alive"&&!Festivals.For(s).Night;

        /// <summary>POLO-2: whether p's HUD offers Cancel for what they are doing. Nothing cancels a turn on the Ferris wheel (the
        /// host refuses it), so nothing offers to; an Ember Playa art car rider still hops off.</summary>
        public static bool CanCancel(RoundState s,PlayerState p)=>p.InteractionId!=""&&!FestivalSimulation.OnWheel(s,p.Id);
        /// <summary>The cancel E runs. PLAYA-5: on a plain art car ride it also says F plays a set on the car's deck while that is free.</summary>
        public static string CancelAction(RoundState s,PlayerState p,string chat="F")=>"Cancel current action"+(FestivalSimulation.CanPlayDeckSet(s,p)?Dot+chat+" PLAY A DECK SET":"");
        /// <summary>DANCE-12: whether p is mid four-lane challenge (FestivalInput.IsRhythm: a dance, a check dance, the poi circle, the
        /// deck, a Drum Circle part, or a talk's trust dance). E sits between the W and D lanes, so then E runs nothing and a tap of G
        /// drops nothing: only holding G for HoldToStopSeconds stops it, as HoldToStopFor says. Anything else keeps its cancel on E, and
        /// lying down gets up first. DIALOG-3: a talk is replied to on 1-3, and the effective Menu binding walks out of it.</summary>
        public static bool StopsByHold(RoundState s,PlayerState p){if(p.InteractionId!="")foreach(var i in s.Interactions)if(i.Id==p.InteractionId)return i.Status=="Active"&&FestivalInput.IsRhythm(i);return false;}
        public static readonly string HoldToStop=HoldToStopFor("");
        /// <summary>The line under the lanes: holding G withdraws a Drum Circle part, ends a deck set by hopping off the art car (PLAYA-5),
        /// ends the stage DJ set, and stops every dance.</summary>
        public static string HoldToStopFor(string kind,string drop="G")=>"HOLD "+drop+(kind==FestivalSimulation.DrumPartKind?" TO STOP DRUMMING":kind==FestivalSimulation.DeckSetKind?" TO HOP OFF":kind=="Dj"?" TO END THE SET":" TO STOP DANCING");
        public const float HoldToStopSeconds=1;
        /// <summary>K2 (batch call 9: "you can walk away (E-hold, or moving off)"): whether p is mid talk, replying on 1-3 (its trust
        /// dance is on the lanes: StopsByHold). A talk holds you still and walking out of it (Cancel) puts every watcher on edge, so a
        /// tap of E runs nothing then, not even the cancel listed first. The effective Menu binding walks out, as WalkAway
        /// says on the prompt. The legacy method name remains for callers that suppress E during a talk.</summary>
        public static bool WalksOutByHold(RoundState s,PlayerState p){if(p.InteractionId!="")foreach(var i in s.Interactions)if(i.Id==p.InteractionId)return i.Status=="Active"&&FestivalSimulation.Talks(i.Kind)&&!FestivalSimulation.TrustDancing(i);return false;}
        public const string WalkAway="ESC TO WALK AWAY";
        /// <summary>POLO-2: a Ferris wheel rider's prompt, in place of an action: what the turn has left at `now` (the view's
        /// simulation clock), in whole seconds rounded up like Clock. "" off the wheel.</summary>
        public static string WheelPrompt(RoundState s,PlayerState p,double now)
        {
            var ride=s.Interactions.Find(i=>i.PlayerId==p.Id&&i.Kind==FestivalSimulation.RideWheelKind&&i.Status=="Active");
            return ride==null?"":"ON THE FERRIS WHEEL"+Dot+(int)Math.Ceiling(Math.Max(0,ride.StartSeconds+ride.DurationSeconds-now))+" s";
        }

        /// <summary>GAS-1: the camp prompt beside the Giggle Balloons: E takes one (TakeGiggleBalloon), or, GAS-4, while two are on, a
        /// reminder with nothing to press. "" out of their reach.</summary>
        public static string GiggleBalloonPrompt(PlayerState p)=>!FestivalSimulation.AtGiggleBalloons(p)?"":FestivalSimulation.TwoGiggleBalloonsOn(p)?"TWO GIGGLE BALLOONS AT A TIME":TakeGiggleBalloon;
        public const string TakeGiggleBalloon="E  TAKE A GIGGLE BALLOON";

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
            if((s.Phase!="Playing"&&s.Phase!=FestivalSimulation.MinigamePhase)||s.LevelIndex!=Festivals.LevelCount-1)return "";
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
        /// None in the middle of anything (lying down, a talk, a task): the host refuses a carry or put-down then.
        /// </summary>
        public static string BodyAction(RoundState s,PlayerState p,BodyState b)
        {
            if(p.Life!="Alive"||p.InteractionId!="")return "";
            string body=(s.Players.Find(x=>x.Id==b.PlayerId)?.Name??"a friend")+"'s body";
            if(p.CarryBodyId==b.PlayerId)return "Put down "+body+(FestivalSimulation.BodyMoves(s,b)?"":Dot+"too heavy alone: get a second carrier");
            double dx=p.X-b.X,dz=p.Z-b.Z;
            if(p.CarryBodyId!=""||p.DragTargetId!=""||Math.Sqrt(dx*dx+dz*dz)>FestivalSimulation.CarryReach)return "";
            int carriers=FestivalSimulation.CarrierCount(s,b);
            // CarryBody refuses a third carrier.
            return carriers>=2?"":carriers==1?"Take the other end of "+body:"Carry "+body;
        }

        /// <summary>The host's festival pick at camp before Day 1, once a second festival is unlocked. F moves to the next unlocked one.
        /// A test-build host (developmentSelection: FestivalSession.DevelopmentFestivalSelection) picks from every festival, earned or not.</summary>
        public static string FestivalChoice(RoundState s,string localId,bool developmentSelection=false,string chat="F")
        {
            if(s.Phase!="Shopping"||s.LevelIndex!=0||s.HostPlayerId!=localId||Pickable(s,developmentSelection)<2)return "";
            int next=NextFestival(s,developmentSelection);
            return chat+"  switch to "+Festivals.Name(next)+" ("+(developmentSelection?"test build: all festivals":(next+1)+" of "+s.UnlockedFestivalCount+" unlocked")+")";
        }
        /// <summary>The festival F picks: the next pickable one, wrapping round to the first.</summary>
        public static int NextFestival(RoundState s,bool developmentSelection=false)=>(s.FestivalIndex+1)%Pickable(s,developmentSelection);
        static int Pickable(RoundState s,bool developmentSelection)=>developmentSelection?Festivals.Count:s.UnlockedFestivalCount;

        /// <summary>The existing twenty-unit restart rule, read in the viewer's money.</summary>
        public static string FreshPocketsDisclosure(RoundState s,PlayerState viewer=null)=>"EVERYONE STARTS OVER WITH "+(s!=null&&viewer!=null?Money(s,viewer,20):"$20").ToUpperInvariant()+" AND EMPTY POCKETS";
        public static string ResetDisclosure(RoundState s,PlayerState viewer=null)=>"BACK TO DAY 1"+Dot+FreshPocketsDisclosure(s,viewer);
        private static string CampResetDisclosure(RoundState s,PlayerState viewer)=>s.ReviewResult==""?"":s.ReviewResult!="Success"?ResetDisclosure(s,viewer):s.LevelIndex==0?"NEW WEEKEND"+Dot+FreshPocketsDisclosure(s,viewer):"";
        /// <summary>The host's words name today's letters ("press E"); each "press X" here names the key bound to that action instead.
        /// Core names the other rebindable keys by action ("{Slot1}", "{Spin}", "{SpinSides}"), and each reads as its bound key, a mouse
        /// button as a click ("left click").</summary>
        public static string PressKeys(string text,string interact="E",string chat="F",string drop="G",string use="Q",string slot1="1",string slot2="2",string slot3="3",string spin="LMB",string sides="RMB")
        {
            // The HUD runs this every frame, and the regex allocates.
            if(text.IndexOf("ress ",StringComparison.Ordinal)>=0&&!(interact=="E"&&chat=="F"&&drop=="G"&&use=="Q"))
                text=PressedKey.Replace(text,m=>m.Groups[1].Value+Spoken(m.Groups[2].Value=="E"?interact:m.Groups[2].Value=="F"?chat:m.Groups[2].Value=="G"?drop:use));
            // After the letters, so a slot bound to E is not read again as Interact.
            if(text.IndexOf('{')<0)return text;
            return ActionToken.Replace(text,m=>Spoken(m.Groups[1].Value=="Slot1"?slot1:m.Groups[1].Value=="Slot2"?slot2:m.Groups[1].Value=="Slot3"?slot3:m.Groups[1].Value=="Spin"?spin:sides));
        }
        static readonly Regex PressedKey=new Regex(@"\b([Pp]ress )([EFGQ])\b");
        static readonly Regex ActionToken=new Regex(@"\{(Slot1|Slot2|Slot3|Spin|SpinSides)\}");
        // A mouse button's short name, as CONTROLS lists it ("LMB", "MOUSE BACK": FestivalInput.Label), reads as a click or a button
        // in a sentence.
        static string Spoken(string key)=>key=="LMB"?"left click":key=="RMB"?"right click":key=="MMB"?"middle click"
            :key=="MOUSE BACK"?"the mouse back button":key=="MOUSE FORWARD"?"the mouse forward button":key;
        // A range of rebound keys would hide the middle one, so only today's 1, 2, 3 read as a range.
        static string SlotKeys(string slot1,string slot2,string slot3,string or)=>slot1=="1"&&slot2=="2"&&slot3=="3"?"1–3":slot1+", "+slot2+or+slot3;
        /// <summary>Only a ground gear drop offers pickup; stop ditch and shared-stash results keep their destination.</summary>
        public static string DropNotice(string message,FestivalInput input)=>message.StartsWith("Dropped ",StringComparison.Ordinal)?message+Dot+input.Label(input.Interact).ToUpperInvariant()+" to pick it up":message;

        /// <summary>Results heading: the level the crew cleared, or that the weekend is over.</summary>
        public static string OutcomeTitle(RoundState s)
        {
            if(s.Result!="Success")return "WEEKEND OVER"+Dot+"BACK TO DAY 1";
            return (!Festivals.For(s).Night?"QUOTA MET":s.LevelIndex==Festivals.LevelCount-1?"EVERYONE HOME":s.SecondFriend.Active?"FRIENDS RESCUED":"FRIEND RESCUED")+Dot+Cleared(s);
        }
        // A win's "DAY 1 CLEARED", or the whole festival's once Night 2 is.
        static string Cleared(RoundState s)=>(s.LevelIndex==Festivals.LevelCount-1?Festivals.Name(s.FestivalIndex):Festivals.For(s).Name).ToUpperInvariant()+" CLEARED";
        // What a win leads to: the next level, the next festival, or the encore lap after the last.
        static string NextUp(RoundState s)=>s.LevelIndex<Festivals.LevelCount-1?Festivals.Level(s.FestivalIndex,s.LevelIndex+1,s.EncoreTier).Name:s.FestivalIndex+1<Festivals.Count?Festivals.Name(s.FestivalIndex+1):"an encore at "+Festivals.Name(0);

        /// <summary>Results detail: why the weekend ended, or what comes next and what the crew keeps.</summary>
        public static string OutcomeDetail(RoundState s,PlayerState viewer=null)
        {
            if(s.Result!="Success")return s.Result+". "+ResetDisclosure(s,viewer);
            if(s.LevelIndex<Festivals.LevelCount-1)return "Next up: "+NextUp(s)+". Cash and gear carry over.";
            return "Next up: "+NextUp(s)+(s.FestivalIndex+1<Festivals.Count?"":", harder")+". A new weekend starts. "+FreshPocketsDisclosure(s,viewer);
        }

        /// <summary>LOOP-5: the results banner's heading, in the middle of the screen for the whole results: why the weekend is over
        /// ("MISSED THE QUOTA", "TIME EXPIRED", "THE CREW WAS WIPED OUT"), or the level or festival cleared. "" until results.</summary>
        public static string ResultsBannerTitle(RoundState s)
        {
            if(s.Phase!="Results")return "";
            if(s.Result=="Success")return Cleared(s);
            return (s.Result==FestivalSimulation.WipedOut?"The crew was wiped out":s.Result).ToUpperInvariant();
        }
        /// <summary>LOOP-5: under the banner's heading, a loss says what a day sold of its quota in the viewer's money and that the
        /// festival restarts at Day 1; a win names what comes next.</summary>
        public static string ResultsBannerDetail(RoundState s,PlayerState viewer)
        {
            if(s.Phase!="Results")return "";
            if(s.Result=="Success")return ("Next: "+NextUp(s)).ToUpperInvariant()+(s.LevelIndex==Festivals.LevelCount-1?"\nNEW WEEKEND"+Dot+FreshPocketsDisclosure(s,viewer):"");
            string sold=Festivals.For(s).Night?"":"You sold "+Money(s,viewer,s.LevelSales)+" of "+Money(s,viewer,FestivalSimulation.DayQuota(s))+"\n";
            return sold+ResetDisclosure(s,viewer);
        }

        /// <summary>The prompt at results: only the host can bring the crew back to camp.</summary>
        public static string ResultsPrompt(RoundState s,string localId,string menu="ESC")=>s.HostPlayerId==localId?menu+" MENU  •  NEXT CAMP":"WAITING FOR THE HOST";

        /// <summary>The debrief's verdict. The campfire already holds the next round, so a win names the level before this one; a loss
        /// says it cost the weekend (LOOP-5).</summary>
        public static string ReviewOutcome(RoundState s)
        {
            if(s.ReviewResult!="Success")return "A GLORIOUS DISASTER"+Dot+"BACK TO DAY 1";
            return s.LevelIndex==0?"WEEKEND CLEARED":Festivals.Level(s.FestivalIndex,s.LevelIndex-1,s.EncoreTier).Name.ToUpperInvariant()+" CLEARED";
        }

        /// <summary>The debrief card's heading: the verdict on the round just played and how it went. Its sales read in the money of the
        /// festival it was played at (PLAYA-2: clearing a weekend has already moved camp on), or camp's own for an older host's view.</summary>
        public static string ReviewHeadline(RoundState s,PlayerState viewer)
        {
            string reset=CampResetDisclosure(s,viewer);
            return "THE VERY OFFICIAL ROUND REVIEW\n"+ReviewOutcome(s)+Dot+"SALES "+Festivals.CurrencyName(s.ReviewFestivalIndex>=0?s.ReviewFestivalIndex:s.FestivalIndex,viewer.Ordinal,s.ReviewSales).ToUpperInvariant()+Dot+"SURVIVORS "+s.ReviewSurvivors+Dot+"CAMP ANTICS "+s.ReviewAntics+(reset==""?"":"\n"+reset);
        }

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
        public static string ReviewStatus(RoundState s,string localId,double revealSeconds,string slot1="1",string slot2="2",string slot3="3",string interact="E")
        {
            if(VoteOpen(s))
            {
                var voted=new List<string>();var waiting=new List<string>();
                foreach(var p in s.Players)if(p.Connected)(FestivalSimulation.VotedOnEveryAward(s,p.Id)?voted:waiting).Add(CrewName(s,localId,p.Id));
                return "CLICK A FRIEND FOR EACH AWARD, OR PRESS "+SlotKeys(slot1,slot2,slot3," OR ")+"\n"+(voted.Count>0?"VOTED  "+string.Join(", ",voted)+Dot:"")+"WAITING ON  "+string.Join(", ",waiting);
            }
            if(!RevealDone(s,revealSeconds))return "";
            return s.HostPlayerId==localId?interact+"  OPEN THE CAMP SHOP":"WAITING FOR THE HOST TO OPEN THE SHOP";
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

        /// <summary>LOOP-4: who a corner death reel is for, from its frozen summary, cut to the spinner's ten letters: "SAM DIED".</summary>
        public static string DeathSpinTitle(DeathSpinState spin){string name=spin.Name.ToUpperInvariant();return (name.Length>10?name.Substring(0,10):name)+" DIED";}
        /// <summary>LOOP-4: the frozen cash a corner reel turns for, in the viewer's money: "$40".</summary>
        public static string DeathSpinStake(RoundState s,PlayerState viewer,DeathSpinState spin)=>Money(s,viewer,spin.Amount).ToUpperInvariant();
        /// <summary>LOOP-4: where a corner reel lands: "KEPT $40", or "DROPPED $40" where they fell.</summary>
        public static string DeathSpinResult(RoundState s,PlayerState viewer,DeathSpinState spin)=>(spin.CashDropped>0?"DROPPED ":"KEPT ")+DeathSpinStake(s,viewer,spin);

        /// <summary>LOOP-4: a drop's floating label: a cash pile reads its amount in the viewer's money ("$40"), anything else its name.</summary>
        public static string DropLabel(RoundState s,PlayerState viewer,DropState drop)=>drop.ItemId=="cash"?Money(s,viewer,drop.Count):Catalog.FindItem(drop.ItemId)?.Name??drop.ItemId;
        /// <summary>LOOP-4: the pickup line for a drop: "Pick up $40", "Pick up Tongue Stamps".</summary>
        public static string PickupAction(RoundState s,PlayerState viewer,DropState drop)=>"Pick up "+DropLabel(s,viewer,drop);

        /// <summary>A crew member as the local player reads them in the debrief: "YOU", or their name.</summary>
        internal static string CrewName(RoundState s,string localId,string id)=>id==localId?"YOU":s.Players.Find(p=>p.Id==id)?.Name.ToUpperInvariant()??"A FRIEND";

        /// <summary>
        /// Who trips this level, on how many doses and of what (TRIP-5's substance wheel), from the public spin result, while the crew
        /// is out, through a minigame and its banner too (the spinner names them before that, and camp still holds the last level's).
        /// Night 2 doses everyone, so the rest of the crew's doses and substances follow, two to a line. A spirit's view lists only spirits: a living tripper is "A FRIEND"
        /// and the living's doses go unlisted. A dose spun before the substance wheel names no substance.
        /// </summary>
        public static string Tripping(RoundState s,string localId)
        {
            if(s.TripperId==""||s.Phase!="Loading"&&s.Phase!="Playing"&&s.Phase!=FestivalSimulation.MinigamePhase)return "";
            int dose=FestivalSimulation.TripperDose(s);string took=FestivalSpinner.SubstanceName(s.Doses.Find(d=>d.PlayerId==s.TripperId)?.Substance);
            string text=(s.TripperId==localId?"YOU TRIP":(s.Players.Find(p=>p.Id==s.TripperId)?.Name.ToUpperInvariant()??"A FRIEND")+" TRIPS")+Dot+dose+(dose==1?" DOSE":" DOSES")+(took==""?"":Dot+took);
            if(s.LevelIndex!=Festivals.LevelCount-1)return text;
            var rest=new List<string>();
            foreach(var d in s.Doses)
            {
                var p=s.Players.Find(x=>x.Id==d.PlayerId);string theirs=FestivalSpinner.SubstanceName(d.Substance);
                if(p!=null&&p.Connected&&p.Id!=s.TripperId)rest.Add((p.Id==localId?"YOU":p.Name.ToUpperInvariant())+" "+d.Dose+(theirs==""?"":" "+theirs));
            }
            for(int i=0;i<rest.Count;i+=2)text+="\n"+(i==0?"DOSED  ":"")+string.Join(Dot,rest.GetRange(i,Math.Min(2,rest.Count-i)));
            return text;
        }

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
        public static readonly string CheckDanceAction=CheckDanceActionFor("F");
        public static string CheckDanceActionFor(string chat)=>"Check by dancing"+Dot+chat+" CHECK BY CHAT ("+FestivalSimulation.ConfirmChatSeconds+" s)";
        public static readonly string CheckChatAction="Check by chatting ("+FestivalSimulation.ConfirmChatSeconds+" s, safe)";
        /// <summary>Beside someone the host won't let anyone check right now (FestivalSimulation.Engaged): said, not offered.</summary>
        public const string BusyAction="They're busy right now";
        /// <summary>TRIP-4: anyone free at the festival can lie on the grass and watch the clouds. The HUD lists it last of all, so E
        /// never lies down instead of anything else; lying there, getting up (Cancel) comes first.</summary>
        public const string LieDownAction="Lie down and watch the clouds",GetUpAction="Get up";
        /// <summary>Beside the VIP guard with no vision about them to check, E or F chats them into handing over a VIP wristband.</summary>
        public static readonly string RopeChatAction="Talk your way past the VIP rope ("+FestivalSimulation.ConfirmChatSeconds+" s)";

        /// <summary>ESC-2: the cop stopping p over visible stock, or null. A view shows a cop's real mode only to the player it targets.</summary>
        public static NpcState StoppedBy(RoundState s,PlayerState p)=>s.Npcs.Find(n=>n.Kind=="Cop"&&n.TargetId==p.Id&&n.Mode=="Stop");
        /// <summary>ESC-2: the session notice as security stops you: why, the three ways out, and what ignoring it costs.</summary>
        public static readonly string StopNotice="SECURITY SAW YOUR STOCK: talk, ditch it or walk away within "+FestivalSimulation.StopArrestSeconds+" s, or you're arrested";
        /// <summary>TRIP-8: the session notice for whoever finds the cloud's stash (RoundState.CloudStashFinderId), in dollars like the
        /// host's replies, for MoneyText.</summary>
        public static readonly string CloudStashNotice="Found the cloud's stash! +$"+FestivalSimulation.SecretStashCash+" for camp";
        /// <summary>The same for whoever finds a stash the tripper's vision showed (RoundState.VisionStashFinderId).</summary>
        public static readonly string VisionStashNotice="Found a hidden stash! +$"+FestivalSimulation.SecretStashCash+" for camp";
        /// <summary>ECON-8: what whoever picks up a random find is told, in dollars like the host's replies, for MoneyText.</summary>
        public static string FoundCashNotice(int amount)=>"Found $"+amount+" on the ground!";
        /// <summary>What a friend or a spirit is told as someone grabs the level's Giggle Tank, by the name the grab carries
        /// (RoundState.GiggleTankGrabberName), in dollars like the host's replies, for MoneyText.</summary>
        public static string GiggleTankGrabbed(string grabber)=>grabber+" grabbed a Giggle Tank! Get it home: +$"+Festivals.GiggleTankCash+" for camp";
        /// <summary>What the carrier is told once the camp gate banks their Giggle Tank.</summary>
        public static readonly string GiggleTankHome="Giggle Tank home! +$"+Festivals.GiggleTankCash+" for camp";
        /// <summary>Follows a client's view for the level's Giggle Tank. Follow is true the frame someone grabs it, when the whole
        /// crew hears the oh-yeah. A friend's grab, or the viewer's own tank banked at the camp gate (their owner-only receipt), leaves
        /// a notice for Take. The first view of a connection or a round only takes note, so a grab from before it stays silent.</summary>
        public sealed class GiggleTankNews
        {
            string round,grabber="",notice="";
            int connection=-1,banked;
            public bool Follow(RoundState s,PlayerState viewer,int connectionGeneration)
            {
                if(round!=s.RoundId||connection!=connectionGeneration)
                {
                    round=s.RoundId;connection=connectionGeneration;grabber=s.GiggleTankGrabberId;banked=viewer.GiggleTankBankSequence;notice="";
                    return false;
                }
                bool grabbed=s.GiggleTankGrabberId!=""&&s.GiggleTankGrabberId!=grabber;grabber=s.GiggleTankGrabberId;
                if(grabbed&&grabber!=viewer.Id)notice=GiggleTankGrabbed(s.GiggleTankGrabberName);
                if(viewer.GiggleTankBankSequence>banked){banked=viewer.GiggleTankBankSequence;notice=GiggleTankHome;}
                return grabbed;
            }
            /// <summary>The notice still to show, once; "" for none.</summary>
            public string Take(){string taken=notice;notice="";return taken;}
        }
        /// <summary>How long each money notice holds the notice line before the next one queued behind it.</summary>
        public const float MoneyNewsSeconds=2.5f;
        /// <summary>The camp shelf's thank-you, with what the buy cost, in dollars for MoneyText.</summary>
        public static string PocketUpdated(int spent)=>"Oh, hey, thanks!  •  −$"+spent+"  •  POCKET UPDATED";
        /// <summary>Whether a notice lights the card's cash and SALES, as a paid sale does (MoneyFlashOn). The stash and items have no flash.</summary>
        public static bool FlashesMoney(MoneyNews n)=>n.Kind=="Cash"||n.Kind=="Sales";
        /// <summary>Follows the viewer's own money news (PlayerState.MoneyNewsLog, owner-only in views). The first view of a
        /// connection only takes note, so a join or a rejoin never replays old news; a new round's news all shows, since a level's
        /// start can itself lose a Giggle Tank. Take gives each new notice once, oldest first, and Watch gives back one cut short.</summary>
        public sealed class MoneyNewsFollower
        {
            string round;
            int connection=-1;
            long seen;
            readonly List<MoneyNews> due=new List<MoneyNews>();
            MoneyNews line;
            float lineUntil;
            public void Follow(RoundState s,PlayerState viewer,int connectionGeneration)
            {
                if(connection!=connectionGeneration){connection=connectionGeneration;round=s.RoundId;seen=viewer.MoneyNewsSequence;due.Clear();line=null;return;}
                if(round!=s.RoundId){round=s.RoundId;seen=0;due.Clear();line=null;}
                if(viewer.MoneyNewsLog==null)return;
                foreach(var n in viewer.MoneyNewsLog)if(n.Sequence>seen){due.Add(n);seen=n.Sequence;}
            }
            /// <summary>The oldest notice still to show, once; null for none. It holds the notice line for MoneyNewsSeconds from now.</summary>
            public MoneyNews Take(float now)
            {
                if(due.Count==0)return null;
                line=due[0];due.RemoveAt(0);lineUntil=now+MoneyNewsSeconds;return line;
            }
            /// <summary>Reads the notice line each frame. When another line replaces the notice Take gave before its MoneyNewsSeconds
            /// end, that notice goes back to the head of the queue. A clear ("", an accepted command's reply) alone gives nothing back.</summary>
            public void Watch(string message,float now)
            {
                // A clear keeps the watch: a 1-3 reply key, or a client's result, clears the line a frame before the sale's notice.
                if(line==null||message==line.Text||string.IsNullOrEmpty(message))return;
                if(now<lineUntil)due.Insert(0,line);
                line=null;
            }
        }
        /// <summary>The life banner's headings: as you become a spirit, and as a revival brings you back.</summary>
        public const string DeathTitle="YOU DIED",BackTitle="YOU'RE BACK";
        /// <summary>Under YOU DIED, what being a spirit means and how many revivals p has left this level.</summary>
        public static string DeathDetail(PlayerState p)
        {
            int left=FestivalGuidance.RevivalsLeft(p);
            return "You're a spirit: you float through anything, and only other spirits see you. "+(left==0?"No revivals left this level.":"Reach the medical tent to come back ("+left+(left==1?" revival":" revivals")+" left).");
        }
        /// <summary>Under YOU'RE BACK: walking through things ended with the spirit.</summary>
        public const string BackDetail="Back on your feet at the medical tent. Walls and the stage stop you again.";
        /// <summary>Follows the viewer's own life in their view for the HUD's life banner. Follow gives the banner a change earns,
        /// once: YOU DIED as they become a spirit, YOU'RE BACK as a spirit comes back to life; ("","") otherwise. The first view of a
        /// connection or a round only takes note, so joining as a spirit, or a new level's fresh start, stays quiet.</summary>
        public sealed class LifeNews
        {
            string round,life="";
            int connection=-1;
            public (string Title,string Detail) Follow(RoundState s,PlayerState viewer,int connectionGeneration)
            {
                string was=life;life=viewer.Life;
                if(round!=s.RoundId||connection!=connectionGeneration){round=s.RoundId;connection=connectionGeneration;return ("","");}
                if(was!="Spirit"&&life=="Spirit")return (DeathTitle,DeathDetail(viewer));
                return was=="Spirit"&&life!="Spirit"?(BackTitle,BackDetail):("","");
            }
        }
        /// <summary>ESC-2: E during a stop while the guard is still walking over, out of talking reach (the host's 2.5 m): said, not
        /// offered, so until the talk is in reach E does nothing, never a sale or a buy in the guard's sight, which is an arrest.</summary>
        public const string StopApproachAction="Security is coming over";

        /// <summary>
        /// The player card's warning: security about to detain p, else security stopping p (with the ways out of it), else ON CAMERA
        /// and an Ember Playa dust storm, joined as one; "" with nothing to report. Then p's effects. The crowd's suspicion is on the
        /// threat bar (Threat), not this line. The warning always comes first: the spinner's dose lasts the whole level
        /// (everyone's on Night 2) and must never hide it.
        /// </summary>
        public static string Condition(RoundState s,PlayerState p,string talk="E",string ditch="G")
        {
            // An arrest warning detains you in seconds, so it always wins; security on you then outranks the rest. The worse of two
            // cops on you shows; a cop on patrol is no warning. ESC-2: a stop names its three ways out, joined by single-spaced dots
            // so it reads as one warning before the effects. PLAYA-3: a dust storm says how far everyone, security too, can see; the
            // view's festival, spin seed and clock are enough to know it. Only the living get it, as nothing sees anyone else at any
            // range (Danger.Sees). POLO-6: an influencer filming you (your own view's VisualFilmedBy) puts the crowd round you on
            // alert, so ON CAMERA leads the storm line, joined as one warning; the two above still take the whole line.
            bool arrest=s.Npcs.Exists(n=>n.Kind=="Cop"&&n.TargetId==p.Id&&n.Mode=="ArrestWarning");
            string storm=FestivalWorld.ShowsFestival(s.Phase)&&p.Life=="Alive"&&FestivalSimulation.DustStorm(s)?"DUST STORM: SEEN ONLY WITHIN "+Festivals.DustStormSightRange+" M":"";
            string warning=arrest?"ARREST WARNING":StoppedBy(s,p)!=null?"SECURITY STOP • TALK ("+talk+") • DITCH IT ("+ditch+") • OR WALK AWAY"
                :p.VisualFilmedBy.Count>0?(storm==""?"ON CAMERA":"ON CAMERA • "+storm):storm;
            if(p.Effects.Count==0)return warning;
            string effects=Catalog.EffectsLine(p.Effects).ToUpperInvariant();
            return warning==""?effects:warning+Dot+effects;
        }

        /// <summary>The threat bar's band words, calm to swarming (Threat's Band).</summary>
        public static readonly string[] ThreatBands={"CALM","WATCHING","QUESTIONING","ACCUSING","SWARMING"};
        /// <summary>
        /// The player card's threat bar, how sketchy the crowd thinks p is: the worst festivalgoer's suspicion of p as a 0-1
        /// fill, and its band (ThreatBands) by the thresholds the crowd acts on: it watches at 25, questions at 45 and accuses at 70
        /// (Danger.WookTick). A festivalgoer swarming p fills the bar. A view carries each festivalgoer's suspicion of its viewer
        /// alone, and its real mode only to the player it targets. One scattering from an arrest isn't after you while it flees, so
        /// it never counts. Security isn't the crowd: its stops keep their own warnings (Condition).
        /// </summary>
        public static (float Fill,int Band) Threat(RoundState s,PlayerState p)
        {
            double worst=0;bool swarm=false;
            foreach(var npc in s.Npcs)if(npc.Kind=="Wook"&&npc.Mode!=FestivalSimulation.Scattering){if(npc.Suspicion>worst)worst=npc.Suspicion;swarm|=npc.Mode=="Swarming"&&npc.TargetId==p.Id;}
            if(swarm)return (1,4);
            return ((float)Math.Min(1,worst/100),worst>=70?3:worst>=45?2:worst>=25?1:0);
        }
        /// <summary>One frame of the bar easing toward fill, never past it; reduced motion jumps straight there.</summary>
        public static float EaseThreat(float shown,float fill,float deltaTime,bool reducedMotion)=>reducedMotion?fill:shown+(fill-shown)*(1-(float)Math.Exp(-ThreatEaseRate*deltaTime));
        // About a third of a second to close most of the gap: quick enough to warn, slow enough to read as a change.
        const float ThreatEaseRate=6;

        /// <summary>
        /// The player card: health, pocket and the crew's stash, then the level's sales, life and the warning (Condition). The warning
        /// leads and the effects trail it, so a long effects list wraps off the card before the warning does. flashMoney lights your
        /// cash and the level's SALES in the HUD's orange, as a paid sale flashes them (MoneyFlashOn).
        /// </summary>
        public static string Vitals(RoundState s,PlayerState p,string talk="E",string ditch="G",bool flashMoney=false)
        {
            string condition=Condition(s,p,talk,ditch);
            string Lit(string text)=>flashMoney?"<color=#FC7741>"+text+"</color>":text;
            return "HP "+p.Health+Dot+Lit(Money(s,p,p.Cash).ToUpperInvariant())+Dot+("STASH "+Money(s,p,s.StashCash)).ToUpperInvariant()+"\n"
                +Lit(("SALES "+Money(s,p,s.LevelSales)).ToUpperInvariant())+Dot+p.Life.ToUpperInvariant()+(condition==""?"":Dot+condition);
        }
        /// <summary>How long a sale's result stays stamped mid-screen, and whether a paid one's flash is lit `since` seconds after it:
        /// on the even quarter-seconds of the stamp.</summary>
        public const float SaleStampSeconds=1.5f;
        public static bool MoneyFlashOn(float since)=>since>=0&&since<SaleStampSeconds&&(int)(since*4)%2==0;
        /// <summary>Follows the viewer's own conversation outcomes for the sale stamp. Follow gives the newest sale that ended with a
        /// Result since the last call, once; null otherwise. It reads the outcomes, not the chat window, so a sale whose window Esc
        /// shut is stamped too. The first view of a connection or a round only takes note, so a rejoin replays no sale.</summary>
        public sealed class SaleNews
        {
            string round;
            int connection=-1;
            long seen;
            public ConversationOutcome Follow(RoundState s,PlayerState viewer,int connectionGeneration)
            {
                var outcomes=viewer.ConversationOutcomes;if(outcomes==null)return null;
                if(round!=s.RoundId||connection!=connectionGeneration)
                {
                    round=s.RoundId;connection=connectionGeneration;seen=0;foreach(var o in outcomes)seen=Math.Max(seen,o.Sequence);
                    return null;
                }
                var sale=outcomes.FindLast(o=>o.RoundId==s.RoundId&&o.Kind=="Sale"&&o.Result!=""&&o.Sequence>seen);
                if(sale!=null)seen=sale.Sequence;
                return sale;
            }
        }
        /// <summary>The gear bar's heading: Slot1-Slot3 equip (left out while they reply in a chat), Use, Drop (left out while held Drop
        /// drops nothing).</summary>
        public static string GearHeading(bool equip,string slot1="1",string slot2="2",string slot3="3",string use="Q",string drop="G",bool drops=true)
            =>"GEAR   /   "+(equip?SlotKeys(slot1,slot2,slot3,", ")+" EQUIP     ":"")+use+" USE"+(drops?"     "+drop+" DROP":"");

        /// <summary>The rhythm lane's title, naming a check dance as the check it is. DIALOG-3: a talk is on the lanes only for the dance
        /// a wary festivalgoer asks for first, so it reads as that. MINI-2: a Drum Circle part is the player's own. PLAYA-5: an art
        /// car's deck set reads as a set.</summary>
        public static string RhythmTitle(string kind)=>kind==FestivalSimulation.DrumPartKind?"DRUM CIRCLE  /  YOUR PART":kind==FestivalSimulation.DeckSetKind?"DECK SET  /  FOUR-LANE"
            :(kind=="ConfirmDance"?"CHECK DANCE":FestivalSimulation.Talks(kind)?"TRUST DANCE":kind.ToUpperInvariant())+"  /  FOUR-LANE";

        /// <summary>DANCE-2: every four-lane challenge (FestivalInput.IsRhythm, a talk's trust dance included) shows the live dancer
        /// beside its lanes, unless the menu covers it. MINI-2: nobody dances in the Drum Circle, so its parts show the crew's
        /// accuracy there instead (FestivalHud).</summary>
        public static bool ShowsDancer(InteractionState i,bool menuOpen)=>FestivalInput.IsRhythm(i)&&i.Kind!=FestivalSimulation.DrumPartKind&&i.Kind!="Dj"&&!menuOpen;
        /// <summary>The stage DJ set rides the highway (FestivalDjRide), not the arrow lanes. The view from behind the decks shows the DJ,
        /// so no dancer panel either. It still roots you and stops by holding G (FestivalInput.IsRhythm).</summary>
        public static bool ShowsRide(InteractionState i)=>FestivalInput.IsRhythm(i)&&i.Kind=="Dj";
        public const string RideTitle="DJ SET  /  RIDE THE BEAT";
        public static string RideControls(string left,string right)=>"STEER  "+left+"  "+right;
        /// <summary>The live dancer's heading. A check dance reads as a dance. DIALOG-3: a talk shows the dancer only in its trust dance,
        /// whatever the talk, so it says what that dance is for.</summary>
        public static string DancerHeading(string kind)=>FestivalSimulation.Talks(kind)?"YOU  /  DANCING FOR TRUST":kind switch{"Poi"=>"YOU  /  POI CIRCLE",FestivalSimulation.DeckSetKind=>"YOU  /  ON THE ART CAR",_=>"YOU  /  ON THE FLOOR"};
        /// <summary>The caption under the live dancer, shown while nobody is saying a line.</summary>
        public static string DancerCaption(string kind)=>FestivalSimulation.Talks(kind)?"YOUR LOOK • THEIR TRUST":kind switch{"Poi"=>"YOUR POI • YOUR FLOW",FestivalSimulation.DeckSetKind=>"YOUR LOOK • YOUR SET",_=>"YOUR LOOK • YOUR MOVES"};

        /// <summary>What p can do beside festivalgoer n, as the HUD lists them; E runs the first. ECON-3 (Matt: "there is no button to
        /// actually sell to people"): by day that is p's next step to a sale, the offer while holding stock and otherwise a buy from
        /// them (FestivalGuidance.NeedsStock). The dance follows either way, and still leads at night. The first says "F CHAT" while
        /// F chats with them, not while it checks a vision (CheckTarget). DIALOG-3: chatting is a talk ("Talk with festivalgoer", the
        /// one way to chat now that the bark-only chat is gone), and F starts it. K1: soldToYou says n has sold you a unit this level, which only
        /// your HUD knows; a seller never buys, so by day E buys again from them and the offer, a sure refusal, goes last.</summary>
        public static List<(string Label,GameCommand Command)> FestivalgoerActions(RoundState s,PlayerState p,NpcState n,bool soldToYou=false,string chat="F")
        {
            var actions=new List<(string Label,GameCommand Command)>{("Dance with festivalgoer",new GameCommand{Kind="Dance",TargetId=n.Id})};
            if(n.CanTalk)actions.Add(("Talk with festivalgoer",new GameCommand{Kind="Conversation",TargetId=n.Id}));
            var stock=p.Inventory.Find(i=>i.ItemId==p.EquippedItemId&&i.Count>0&&Catalog.FindItem(i.ItemId)?.Category=="Stock")
                ??p.Inventory.Find(i=>i.Count>0&&Catalog.FindItem(i.ItemId)?.Category=="Stock");
            bool day=!Festivals.For(s).Night;
            if(stock!=null){int at=day&&!soldToYou?0:actions.Count;for(int ask=FestivalSimulation.FairAsk;ask<=FestivalSimulation.GreedyAsk;ask++)actions.Insert(at+ask,(OfferAction(s,p,n,stock.ItemId,ask),new GameCommand{Kind="StartSale",TargetId=n.Id,ItemId=stock.ItemId,Amount=ask}));}
            actions.Insert(FestivalGuidance.NeedsStock(s,p)||day&&soldToYou?0:actions.Count,(BuyAction(s,p,n.Id),new GameCommand{Kind="StartBuy",TargetId=n.Id,ItemId=FestivalSimulation.SellerStock(n.Id)}));
            if(n.CanTalk&&CheckTarget(s,p)==null)actions[0]=(actions[0].Label+Dot+chat+" CHAT",actions[0].Command);
            return actions;
        }

        /// <summary>A sale's three asks by name, as the offer and the talk's title line say them.</summary>
        public static readonly string[] AskNames={"Fair","High","Greedy"};
        /// <summary>"Offer Tongue Stamps  •  Fair $10", "Ask high  •  $15" or "Ask greedy  •  $20", in the viewer's money.</summary>
        public static string OfferAction(RoundState s,PlayerState viewer,NpcState buyer,string item,int ask){string price=Money(s,viewer,FestivalSimulation.SalePrice(s,viewer,buyer,ask));return ask==FestivalSimulation.FairAsk?"Offer "+Catalog.FindItem(item).Name+Dot+AskNames[ask]+" "+price:"Ask "+AskNames[ask].ToLowerInvariant()+Dot+price;}
        /// <summary>ECON-2: beside any festivalgoer, "Buy Tongue Stamps  •  $5": the one kind they would sell, read off their id alone
        /// (FestivalSimulation.SellerStock) so the label gives no role away, at its price in the viewer's money.</summary>
        public static string BuyAction(RoundState s,PlayerState viewer,string npcId){var item=Catalog.FindItem(FestivalSimulation.SellerStock(npcId));return "Buy "+item.Name+Dot+Money(s,viewer,item.Price);}
        /// <summary>ECON-2: what a finished buy says, in dollars like the host's replies, for MoneyText: "Bought Tongue Stamps for $5"
        /// when the price it held stays spent (cash as it ends equals cash on its last frame, whatever a handoff did to the stack,
        /// and wherever the unit landed); "" when a miss, a cancel or a bust handed the price back. A bust's fine or a death's dropped
        /// cash can cancel that refund out exactly, so a buyer no longer Alive bought nothing whatever the cash says.</summary>
        public static string BuyReply(string item,int cashWhileBuying,PlayerState now){var bought=Catalog.FindItem(item);return now.Cash==cashWhileBuying&&now.Life=="Alive"?"Bought "+bought.Name+" for $"+bought.Price:"";}
    }
}
