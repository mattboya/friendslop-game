using System;

namespace Festival.Core
{
    /// <summary>Day levels are the hustle: sell the crew's quota before sundown; once it is met anyone may head back to camp early.</summary>
    public sealed partial class FestivalSimulation
    {
        // A perfect sale pays PerfectSalePay before the dose's and the encore lap's multiplier (PayoutMultiplier).
        public const int PerfectSalePay=10;
        // A sale's ask (StartSale's Amount): each pays its full price at its need or better; from its floor they talk you down, Fair
        // to half and High or Greedy to Fair's price; below the floor it is no sale, and a pushed ask that fails costs AskSuspicion.
        public const int FairAsk=0,HighAsk=1,GreedyAsk=2;
        static readonly int[] AskFull={PerfectSalePay,PerfectSalePay*3/2,PerfectSalePay*2},AskDown={PerfectSalePay/2,PerfectSalePay,PerfectSalePay},AskSuspicion={0,15,30};
        static readonly double[] AskNeed={.75,.75,.875},AskFloor={.4,.5,.6};
        const string RefusedAfterAsk="Not after that ask. They won't buy from you again.";
        /// <summary>How a change of cash reads: what happened, then the signed amount in dollars for FestivalHudText.MoneyText,
        /// "SOLD Tongue Stamps  •  +$10" or "SECURITY FINE  •  −$10".</summary>
        public static string MoneyNotice(string what,int amount)=>what+"  •  "+(amount<0?"−$":"+$")+Math.Abs(amount);
        /// <summary>The score a sale at this ask needs for its full price.</summary>
        public static double SaleNeeds(int ask)=>AskNeed[ask];
        /// <summary>What a sale at this ask pays at this score, before SalePayout's multipliers.</summary>
        public static int SalePay(int ask,double score)=>score>=AskNeed[ask]?AskFull[ask]:score>=AskFloor[ask]?AskDown[ask]:0;
        /// <summary>The price the offer names for this ask: a full sale's pay with the dose, the encore lap and a VIP zone, but never
        /// a double buyer's, as that is a vision's truth.</summary>
        public static int SalePrice(RoundState s,PlayerState seller,NpcState buyer,int ask)=>(int)Math.Round(SalePay(ask,1)*VipPayout(s,seller,buyer)*PayoutMultiplier(s),MidpointRounding.AwayFromZero);
        // A High or Greedy ask that ends in no sale or is walked away from: the buyer and each watcher, once, grow that much more
        // suspicious of the seller, and the buyer refuses them for the rest of the level.
        void PushedAskFailed(InteractionState i,PlayerState p)
        {
            var buyer=State.Npcs.Find(n=>n.Id==i.TargetId);if(buyer==null||i.Ask==FairAsk)return;
            foreach(var n in State.Npcs)if(n==buyer||i.WitnessIds.Contains(n.Id))Adjust(n,p,AskSuspicion[i.Ask],packed:false);
            Observe(buyer,p).RefusesSales=true;
        }
        // The weekend table sets how many sales a buyer takes for this crew (Festivals.SalesPerBuyer).
        public static int SalesPerBuyer(RoundState s)=>Festivals.SalesPerBuyer(QuotaCrew(s));
        // The table's encore-scaled quota per crew member, times the crew connected right now (at least one).
        // ponytail: a live count, so a friend who drops lowers the target; lock it at level start if crews exploit that.
        // Also reads a client view: a spirit's view lists only spirits, so it uses the crew count ViewFor sends.
        public static int DayQuota(RoundState s)=>Festivals.For(s).QuotaPerCrew*QuotaCrew(s);
        static int QuotaCrew(RoundState s)=>Math.Max(1,s.ConnectedCrewCount>0?s.ConnectedCrewCount:s.Players.FindAll(p=>p.Connected).Count);
        // Day levels only: a night has no quota.
        public static bool DayQuotaMet(RoundState s)=>s.LevelSales>=DayQuota(s);
        bool DayLevel=>!Festivals.For(State).Night;
        string SundownResult()=>DayQuotaMet(State)?"Success":"Missed the quota";
        static bool CanLeaveDayEarly(RoundState s,PlayerState p)=>DayQuotaMet(s)&&Near(p,Festivals.CampGateX,Festivals.CampGateZ);
        string DayExtractRefusal()=>DayQuotaMet(State)?"Quota met: head to the way back to camp to end the day":"Sell $"+(DayQuota(State)-State.LevelSales)+" more to meet the day's quota";
    }
}
