using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>Every change to a player's cash or carried items, and to the crew's stash or sales, tells the players it affects,
    /// with the amount (Matt, 2026-10-23: "100% obvious"). News reaches one player; CrewNews every connected crew member but the
    /// one it names, who gets their own line. Each lands in the player's own MoneyNewsLog, which only their view carries
    /// (FestivalSession.ViewFor).</summary>
    public sealed partial class FestivalSimulation
    {
        public const int MoneyNewsCap=8;
        void News(PlayerState p,string kind,string text)
        {
            if(p==null)return;if(p.MoneyNewsLog==null)p.MoneyNewsLog=new List<MoneyNews>();
            p.MoneyNewsLog.Add(new MoneyNews{Sequence=++p.MoneyNewsSequence,Kind=kind,Text=text});
            while(p.MoneyNewsLog.Count>MoneyNewsCap)p.MoneyNewsLog.RemoveAt(0);
        }
        void CrewNews(PlayerState except,string kind,string text){foreach(var q in State.Players)if(q.Connected&&q!=except)News(q,kind,text);}
        // A held price coming back: a buy talked down or walked off, a paid release or revival called off.
        void Refund(PlayerState p,InteractionState i){if(i.ReservedCash<=0)return;p.Cash+=i.ReservedCash;News(p,"Cash",MoneyNotice(i.Kind=="Buy"?"NO DEAL":i.Kind.ToUpperInvariant()+" CALLED OFF",i.ReservedCash));i.ReservedCash=0;}
    }
}
