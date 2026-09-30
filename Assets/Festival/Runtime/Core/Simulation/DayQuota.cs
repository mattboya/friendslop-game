using System;

namespace Festival.Core
{
    /// <summary>Day levels are the hustle: sell the crew's quota before sundown; once it is met anyone may head back to camp early.</summary>
    public sealed partial class FestivalSimulation
    {
        // A perfect sale pays PerfectSalePay before the dose's and the encore lap's multiplier (PayoutMultiplier).
        public const int PerfectSalePay=10;
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
