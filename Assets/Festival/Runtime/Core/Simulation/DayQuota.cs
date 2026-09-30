using System;

namespace Festival.Core
{
    /// <summary>Day levels are the hustle: sell the crew's quota before sundown; once it is met anyone may head back to camp early.</summary>
    public sealed partial class FestivalSimulation
    {
        // A perfect sale pays PerfectSalePay before the dose's bonus. A buyer takes two sales a level, and one more once the crew
        // is five or more, so a big crew still finds enough.
        public const int PerfectSalePay=10;
        public static int SalesPerBuyer(RoundState s)=>2+(QuotaCrew(s)-1)/4;
        // A day never asks more than this share of what it can pay (DaySupply). The table's hardest day, Ember Playa's Day 2 at
        // $30 a head, asks exactly this share, so the cap bites only on encore laps and for a crew of eight.
        const double QuotaShare=.75;
        // The table's encore-scaled quota per crew member, times the crew connected right now (at least one), capped by the supply.
        // ponytail: a live count, so a friend who drops lowers the target; lock it at level start if crews exploit that.
        // Also reads a client view: a spirit's view lists only spirits, so it uses the crew count ViewFor sends.
        public static int DayQuota(RoundState s)=>Math.Min(Festivals.For(s).QuotaPerCrew*QuotaCrew(s),(int)(QuotaShare*DaySupply(s)));
        /// <summary>What a day pays at dose 1 if every sale is perfect: all the stock the camp shelf and the night market carry for
        /// this crew, or every sale the level's buyers take, whichever runs out first. Built from the rules alone, so a client
        /// works it out from its own view.</summary>
        public static int DaySupply(RoundState s)
        {
            int stock=0;foreach(var item in Catalog.Items)if(Stock(item.Id))stock+=2*Catalog.ShopCopies(item.Id,QuotaCrew(s)); // camp shelf and night market
            return PerfectSalePay*Math.Min(stock,Buyers(FestivalCrowdLayout.Count,Festivals.For(s).Narcs)*SalesPerBuyer(s));
        }
        static int QuotaCrew(RoundState s)=>Math.Max(1,s.ConnectedCrewCount>0?s.ConnectedCrewCount:s.Players.FindAll(p=>p.Connected).Count);
        // Day levels only: a night has no quota.
        public static bool DayQuotaMet(RoundState s)=>s.LevelSales>=DayQuota(s);
        bool DayLevel=>!Festivals.For(State).Night;
        string SundownResult()=>DayQuotaMet(State)?"Success":"Missed the quota";
        static bool CanLeaveDayEarly(RoundState s,PlayerState p)=>DayQuotaMet(s)&&Near(p,Festivals.CampGateX,Festivals.CampGateZ);
        string DayExtractRefusal()=>DayQuotaMet(State)?"Quota met: head to the way back to camp to end the day":"Sell $"+(DayQuota(State)-State.LevelSales)+" more to meet the day's quota";
    }
}
