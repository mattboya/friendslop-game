using System;
using System.Collections.Generic;
using Festival.Core;
// CROWD-3: while a cop runs in to make an arrest, the festivalgoers round it scatter, but for the quarter of the crowd that holds
// its ground and anyone a player is busy with; they carry on where they stopped once the arrest is made.
public static class CopScatterTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("CopScatter: "+message);}
    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var scenario in new Action[]{ThreeInFourRunFromTheArrest,OnlyAnArrestScattersThem,TwelveAndAHalfMetresOffIsUntouched,ScatteringTheyNeitherWatchNorStrike,
            ASalePartnerStaysAndTheSaleGoesOn,AfterTheBustTheyCarryOnWhereTheyStopped,AQuarterOfEveryLevelsCrowdHoldsItsGround})
            try{scenario();}catch(Exception e){failures.Add(e.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }

    static double Dist(float x,float z,float tx,float tz)=>Math.Sqrt((x-tx)*(x-tx)+(z-tz)*(z-tz));
    // How far yaw turns from facing straight from (x, z) out through (tx, tz), in degrees.
    static double OffAway(float yaw,float x,float z,float tx,float tz){double away=Math.Atan2(tx-x,tz-z)*180/Math.PI,off=Math.Abs(yaw-away)%360;return Math.Min(off,360-off);}
    static NpcState Cop(FestivalSimulation s)=>s.State.Npcs.Find(n=>n.Id=="cop_0");
    // A level under way, its clock well past the start: cop_0 stands at the origin facing south, player a 1 m south of it in its
    // view, player b 60 m off and the other cop 80 m off. The crowd's first `near` festivalgoers stand in a ring `ring` m round
    // cop_0, in crowd order from due north; the rest stand 50 m off. The crowd keeps CreateRound's order, before the cops.
    static FestivalSimulation Scene(int seed,int near=8,float ring=4)
    {
        var s=new FestivalSimulation(seed);var a=s.AddPlayer("a","A");var b=s.AddPlayer("b","B");s.State.Phase="Playing";s.State.SimulationSeconds=100;
        a.X=0;a.Z=-1;b.X=-60;b.Z=-60;
        var crowd=s.State.Npcs.FindAll(n=>n.Kind=="Wook");
        for(int k=0;k<crowd.Count;k++){double bearing=2*Math.PI*k/Math.Max(1,near);crowd[k].X=k<near?(float)(ring*Math.Sin(bearing)):50+k;crowd[k].Z=k<near?(float)(ring*Math.Cos(bearing)):50;}
        foreach(var cop in s.State.Npcs.FindAll(n=>n.Kind=="Cop")){cop.X=cop.Id=="cop_0"?0:80;cop.Z=0;cop.Yaw=180;}
        return s;
    }
    // cop_0 saw a deal (RecordDeal): an arrest warning, a detained 4 s from now.
    static NpcState Witness(FestivalSimulation s){var cop=Cop(s);cop.Evidence.Add(new EvidenceState{PlayerId="a",Kind="WitnessedDeal",DetainAt=s.State.SimulationSeconds+4});return cop;}
    static bool Scattering(NpcState n)=>n.Mode==FestivalSimulation.Scattering;
    // Ticks until a is detained (at most 6 s), calling each(step) after every step.
    static void UntilTheBust(FestivalSimulation s,Action<int> each){for(int step=1;s.Player("a").Life=="Alive"&&step<=60;step++){s.Tick(.1);each(step);}Check(s.Player("a").Life=="Detained","setup: a is detained within 6 s ("+s.Player("a").Life+")");}

    // Eight festivalgoers 4 m round the cop: the six that don't hold their ground scatter for the whole run-in, from the step after
    // the cop starts it, straight away from the cop and facing away, and end at least ScatterRadius out; the two that do stay put.
    static void ThreeInFourRunFromTheArrest()
    {
        var s=Scene(5);var cop=Witness(s);var near=s.State.Npcs.GetRange(0,8);
        var runners=near.FindAll(n=>!s.HoldsGround(n));var holders=near.FindAll(n=>s.HoldsGround(n));
        Check(runners.Count==6&&holders.Count==2,"setup: 2 of 8 in a row hold their ground ("+holders.Count+")");
        var start=near.ConvertAll(n=>(n.X,n.Z,n.Mode));
        var wrong=new List<string>();
        UntilTheBust(s,step=>{if(step==1)Check(cop.Mode=="ArrestWarning","setup: the cop runs in to arrest a ("+cop.Mode+")");else foreach(var n in near)if(Scattering(n)==s.HoldsGround(n)&&wrong.Count<8)wrong.Add(n.Id+" "+n.Mode+" at step "+step);});
        Check(wrong.Count==0,"the 6 scatter and the 2 don't, every step of the run-in after the cop starts it: "+string.Join(", ",wrong));
        foreach(var n in runners)
        {
            double far=Dist(n.X,n.Z,cop.X,cop.Z);
            Check(far>=FestivalSimulation.ScatterRadius-.01,n.Id+" ends "+FestivalSimulation.ScatterRadius+" m or more from the cop ("+far+" m)");
            var was=start[near.IndexOf(n)];Check(OffAway(n.Yaw,cop.X,cop.Z,n.X,n.Z)<1,n.Id+" faces away from the cop (yaw "+n.Yaw+")");
            Check(OffAway(n.Yaw,cop.X,cop.Z,was.X,was.Z)<1,n.Id+" ran straight away from the cop, from ("+was.X+", "+was.Z+") to ("+n.X+", "+n.Z+")");
        }
        foreach(var n in holders){var was=start[near.IndexOf(n)];Check(n.X==was.X&&n.Z==was.Z&&n.Mode==was.Mode,n.Id+" holds its ground ("+n.Mode+" at "+n.X+", "+n.Z+")");}
    }

    // A cop stopping a over visible stock, or one on patrol, scatters nobody.
    static void OnlyAnArrestScattersThem()
    {
        void Quiet(string mode,bool stock)
        {
            var s=Scene(5);var cop=Cop(s);var near=s.State.Npcs.GetRange(0,8);var start=near.ConvertAll(n=>(n.X,n.Z));
            if(stock){s.Player("a").Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});s.Player("a").EquippedItemId="stock_lsd";}
            for(int step=0;step<20;step++){s.Tick(.1);Check(cop.Mode==mode,"setup: the cop is in "+mode+" ("+cop.Mode+")");Check(!near.Exists(Scattering),"a cop in "+mode+" scatters nobody (step "+step+")");}
            for(int k=0;k<near.Count;k++)Check(near[k].X==start[k].X&&near[k].Z==start[k].Z,near[k].Id+" stays put beside a cop in "+mode);
        }
        Quiet("Stop",true);Quiet("Patrol",false);
    }

    // Of festivalgoers that would scatter, the one 9.5 m from the cop does and the one 12.5 m off never notices. One 11 m off
    // doesn't start to, but the first, once out at ScatterRadius, stands there still scattering out to ScatterHoldRadius.
    static void TwelveAndAHalfMetresOffIsUntouched()
    {
        var s=Scene(5,near:0);var runners=s.State.Npcs.FindAll(n=>n.Kind=="Wook"&&!s.HoldsGround(n));var inside=runners[0];var outside=runners[1];var edge=runners[2];
        inside.X=9.5f;inside.Z=0;outside.X=-12.5f;outside.Z=0;edge.X=0;edge.Z=11;var mode=outside.Mode;Witness(s);bool scattered=false,nudged=false;
        UntilTheBust(s,step=>{
            Check(!Scattering(outside)&&!Scattering(edge),"12.5 m and 11 m from the cop, "+outside.Id+" and "+edge.Id+" don't start to scatter (step "+step+": "+outside.Mode+", "+edge.Mode+")");
            if(scattered&&!nudged&&inside.X>=FestivalSimulation.ScatterRadius-.01){nudged=true;inside.X=11.5f;}
            else if(nudged)Check(Scattering(inside)&&inside.X==11.5f,"11.5 m out, "+inside.Id+" stands still scattering (step "+step+": "+inside.Mode+" at "+inside.X+")");
            scattered|=Scattering(inside);});
        Check(scattered&&nudged,"setup: 9.5 m from the cop, "+inside.Id+" scatters out to "+FestivalSimulation.ScatterRadius+" m");
        Check(outside.X==-12.5f&&outside.Z==0&&outside.Mode==mode,"12.5 m off, "+outside.Id+" is untouched ("+outside.Mode+" at "+outside.X+", "+outside.Z+")");
    }

    // A festivalgoer swarming a, beside the cop, lands a blow within a second (the control). Once the run-in starts it breaks off
    // and scatters: no blow lands while it lasts, and player b sprinting in its view as it runs and stands raises nothing. Once
    // the arrest is made, the same sprint raises its suspicion of b again.
    static void ScatteringTheyNeitherWatchNorStrike()
    {
        NpcState Swarmer(FestivalSimulation s)
        {
            var n=s.State.Npcs.Find(x=>x.Kind=="Wook"&&!s.HoldsGround(x));n.X=1.2f;n.Z=-1;n.Yaw=-90;
            n.Observers.Add(new ObserverState{PlayerId="a",Suspicion=95,AccusationSeconds=90,LastSeenSeconds=100});return n;
        }
        var control=Scene(5,near:0);var lunging=Swarmer(control);control.Tick(1);
        Check(lunging.Mode=="Swarming"&&control.Player("a").Health<100,"setup: with no arrest the swarm lands a blow on a within a second ("+lunging.Mode+", HP "+control.Player("a").Health+")");
        var s=Scene(5,near:0);var n=Swarmer(s);
        var b=s.Player("b");float along=(float)(14/Dist(n.X,n.Z,0,0));b.X=n.X*along;b.Z=n.Z*along;b.SprintUntil=1e9;
        Witness(s);double Of(string id)=>n.Observers.Find(o=>o.PlayerId==id)?.Suspicion??0;
        UntilTheBust(s,step=>{
            if(step>1){Check(Scattering(n)&&n.AttackAt==0,"the swarm on a breaks: "+n.Id+" scatters at step "+step+" ("+n.Mode+", attack at "+n.AttackAt+")");Check(Of("b")==0,"b sprinting in view raises nothing at step "+step+" ("+Of("b")+")");}
            if(s.Player("a").Life=="Alive")Check(s.Player("a").Health==100,"no blow lands on a during the run-in (HP "+s.Player("a").Health+" at step "+step+")");});
        Check(Dist(n.X,n.Z,b.X,b.Z)<FestivalSimulation.ScatterRadius&&OffAway(n.Yaw,n.X,n.Z,b.X,b.Z)<30,"setup: b is in "+n.Id+"'s view where it stands");
        s.Tick(1);Check(!Scattering(n)&&Of("b")>0,"once the arrest is made, b sprinting in view raises "+n.Id+"'s suspicion again ("+n.Mode+", "+Of("b")+")");
    }

    // The festivalgoer a is selling to, one who would otherwise scatter, stays put through the run-in the sale itself set off, and
    // the sale goes on until the bust ends it; the rest still scatter.
    static void ASalePartnerStaysAndTheSaleGoesOn()
    {
        var s=Scene(5);var cop=Cop(s);var near=s.State.Npcs.GetRange(0,8);var partner=near.Find(n=>!s.HoldsGround(n));partner.X=0;partner.Z=-2.5f;
        var a=s.Player("a");a.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        var sold=s.Execute("a",new GameCommand{Id="scatter_sale",Kind="StartSale",TargetId=partner.Id,ItemId="stock_lsd"});
        Check(sold.Accepted&&cop.Evidence.Exists(e=>e.PlayerId=="a"&&e.Kind=="WitnessedDeal"),"setup: a starts a sale in front of the cop ("+sold.Reason+")");
        var sale=s.Interaction(a.InteractionId);float x=partner.X,z=partner.Z;bool othersRan=false;
        UntilTheBust(s,step=>{
            if(a.Life=="Alive")Check(sale.Status=="Active","the sale goes on through the run-in (step "+step+": "+sale.Status+")");
            Check(!Scattering(partner)&&partner.X==x&&partner.Z==z,"the sale partner stays put (step "+step+": "+partner.Mode+" at "+partner.X+", "+partner.Z+")");
            othersRan|=near.FindAll(n=>n!=partner&&!s.HoldsGround(n)).TrueForAll(Scattering);});
        Check(othersRan,"the rest of the crowd that would scatter does");
        Check(sale.Status=="Police bust","the bust ends the sale ("+sale.Status+")");
    }

    // A distracted festivalgoer and one watching b scatter like the rest. Through the run-in nobody's suspicion of anyone or
    // accusation clock moves; the step after the arrest each carries on by its own rules from where it stopped: distracted,
    // watching b, the rest blending, none walking back.
    static void AfterTheBustTheyCarryOnWhereTheyStopped()
    {
        var s=Scene(5);var near=s.State.Npcs.GetRange(0,8);var runners=near.FindAll(n=>!s.HoldsGround(n));
        var distracted=runners[0];distracted.DistractedUntil=1000;
        var watching=runners[1];watching.Observers.Add(new ObserverState{PlayerId="b",Suspicion=30,AccusationSeconds=-1,LastSeenSeconds=100});
        s.Tick(.1);Check(distracted.Mode=="Distracted"&&watching.Mode=="Watching","setup: one is distracted, one watches b ("+distracted.Mode+", "+watching.Mode+")");
        string Clocks()=>string.Join(" ",runners.ConvertAll(n=>n.Id+":"+string.Join(",",n.Observers.ConvertAll(o=>o.PlayerId+"="+o.Suspicion+"/"+o.AccusationSeconds+"/"+o.LastSeenSeconds))));
        Witness(s);for(int step=0;step<10&&!runners.TrueForAll(Scattering);step++)s.Tick(.1);
        Check(runners.TrueForAll(Scattering),"setup: the distracted one and the watcher scatter with the rest");
        string before=Clocks();UntilTheBust(s,step=>{});
        Check(Clocks()==before,"nobody's suspicion or accusation clock moves through the run-in:\n  "+before+"\n  "+Clocks());
        var stopped=runners.ConvertAll(n=>(n.X,n.Z));s.Tick(.1);
        Check(distracted.Mode=="Distracted"&&watching.Mode=="Watching","the step after the arrest each carries on by its own rules ("+distracted.Mode+", "+watching.Mode+")");
        Check(watching.Observers.Find(o=>o.PlayerId=="b").Suspicion==30,"the watcher's suspicion of b is unchanged");
        for(int k=0;k<runners.Count;k++)Check(!Scattering(runners[k])&&runners[k].X==stopped[k].X&&runners[k].Z==stopped[k].Z,runners[k].Id+" carries on from where it stopped ("+runners[k].Mode+" at "+runners[k].X+", "+runners[k].Z+")");
    }

    // Over 200 levels, with the whole crowd within ScatterRadius of the run-in, 75% +/-3% scatters, and which quarter holds its
    // ground changes from level to level: everyone scatters in some levels and holds in others.
    static void AQuarterOfEveryLevelsCrowdHoldsItsGround()
    {
        int scattered=0,crowd=0;var ran=new HashSet<int>();var held=new HashSet<int>();
        for(int seed=0;seed<200;seed++)
        {
            var s=Scene(seed,near:FestivalCrowdLayout.Count,ring:6);Witness(s);s.Tick(.3);
            var wooks=s.State.Npcs.FindAll(n=>n.Kind=="Wook");crowd+=wooks.Count;
            for(int k=0;k<wooks.Count;k++)if(Scattering(wooks[k])){scattered++;ran.Add(k);}else held.Add(k);
        }
        double share=(double)scattered/crowd;
        Check(Math.Abs(share-.75)<=.03,"75% +/-3% of the crowd scatters over 200 levels ("+(share*100).ToString("0.0")+"%)");
        Check(ran.Count==FestivalCrowdLayout.Count&&held.Count==FestivalCrowdLayout.Count,"each festivalgoer scatters in some levels and holds its ground in others ("+ran.Count+" ran, "+held.Count+" held)");
    }
}
