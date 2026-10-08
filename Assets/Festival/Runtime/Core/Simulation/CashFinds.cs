using System;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    // A separate event stream leaves roles, visions, death spins and all existing content draws unchanged.
    static ContentRandom CashFindRandom(RoundState s,int index)=>new ContentRandom(unchecked((s.SpinSeed*79+43)^s.DealSeed^(index*104729)));
    void ResetCashFinds(){State.Drops.RemoveAll(d=>d.CashFind);State.CashFindIndex=0;State.NextCashFindAt=0;}
    void ArmCashFinds(){State.NextCashFindAt=State.ElapsedSeconds+45+CashFindRandom(State,State.CashFindIndex).Next(46);}
    void CashFindTick()
    {
        if(State.NextCashFindAt==0)ArmCashFinds();
        if(State.ElapsedSeconds+1e-9<State.NextCashFindAt)return;
        var random=CashFindRandom(State,State.CashFindIndex);random.Next(46);
        var spots=Festivals.CashFindSpots[State.FestivalIndex];int amount=2+random.Next(4);var spot=spots[random.Next(spots.Length)];
        if(State.Drops.FindAll(d=>d.CashFind).Count<2)State.Drops.Add(new DropState{Id=Id("cash"),ItemId="cash",Count=amount,X=spot.X,Z=spot.Z,CashFind=true});
        State.CashFindIndex++;State.NextCashFindAt+=45+CashFindRandom(State,State.CashFindIndex).Next(46);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ChatDiagnostic?.Invoke("CashFind","Attempt",State.RoundId,State.SimulationSeconds);
#endif
    }
    static void ValidateCashFindState(RoundState s)
    {
        if(!Finite(s.ElapsedSeconds)||s.ElapsedSeconds<0||!Finite(s.NextCashFindAt)||s.NextCashFindAt<0||s.CashFindIndex<0||s.CashFindIndex>1000000
            ||s.NextCashFindAt==0&&s.CashFindIndex!=0||s.NextCashFindAt>0&&(s.NextCashFindAt<45||s.NextCashFindAt<s.ElapsedSeconds-1e-6||s.NextCashFindAt>s.ElapsedSeconds+90+1e-6||s.CashFindIndex>Math.Floor(s.ElapsedSeconds/45)))throw new ArgumentException("Invalid snapshot cash schedule");
        int finds=0;
        foreach(var drop in s.Drops)
        {
            if(drop==null||!drop.CashFind)continue;
            if(++finds>2||drop.ItemId!="cash"||drop.Count<2||drop.Count>5||drop.OwnerId!=""||drop.ClaimedBy!=""||drop.ClaimAt!=0||drop.LandsAt!=0
                ||!Array.Exists(Festivals.CashFindSpots[s.FestivalIndex],p=>p.X==drop.X&&p.Z==drop.Z))throw new ArgumentException("Invalid snapshot random cash pile");
        }
    }
}
}
