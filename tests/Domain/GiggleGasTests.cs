using System;
using System.Collections.Generic;
using System.Text.Json;
using Festival.Core;

// GAS-1: a Giggle Balloon station stands at camp, off the west edge of the gathering mat. Beside it, between levels, anyone takes a
// free hit: nothing for 10 s, then 30 s of Giggle Gas (1 s in, full, 3 s out) that only the hitter feels, while friends see them
// giggle. One at a time: no new hit until the last one has worn off, which the host times at camp too. It never leaves camp.
public static class GiggleGasTests
{
    const string Take=FestivalSimulation.TakeGiggleBalloonKind;
    const string Hit="Giggle Balloon: give it a few seconds…";
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("GiggleGas: "+message);}
    static bool About(double value,double expected)=>Math.Abs(value-expected)<1e-6;
    static CommandResult Act(FestivalSimulation s,string id,string kind)=>s.Execute(id,new GameCommand{Id="gas"+(sequence++),Kind=kind});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{AHitBesideTheBalloonsKicksInTenSecondsLaterForThirty,OneAtATimeUntilTheLastHasWornOff,OnlyBesideTheBalloonsAndOnlyAtCamp,
            TheHostEndsItFortySecondsAfterTheHitEvenAtCamp,LeavingCampClearsIt,FriendsSeeAGiggleOnlyWhileTheGasIsOn,ASnapshotKeepsTheGasGoing})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" giggle gas test(s) failed:\n"+string.Join("\n",failures));
    }

    // Two friends at camp; p0 stands at the balloons.
    static FestivalSimulation Camp(out PlayerState p)
    {
        var s=new FestivalSimulation(7);p=s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        p.X=CampFeatures.GiggleBalloonX;p.Z=CampFeatures.GiggleBalloonZ;return s;
    }
    static ActiveEffect Gas(PlayerState p)=>p.Effects.Find(e=>e.Id==FestivalSimulation.GiggleGasEffect);
    static int Hits(PlayerState p)=>p.Effects.FindAll(e=>e.Id==FestivalSimulation.GiggleGasEffect).Count;
    // The crew readies up at the trailhead and leaves camp: the level starts.
    static void LeaveCamp(FestivalSimulation s)
    {
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);Check(s.State.Phase=="Spinning","setup: the crew leaves camp");
    }

    static void AHitBesideTheBalloonsKicksInTenSecondsLaterForThirty()
    {
        Check(Catalog.FindEffect(FestivalSimulation.GiggleGasEffect)?.Name=="Giggle Gas","the effect reads as Giggle Gas");
        var s=Camp(out var p);s.Tick(3);double hit=s.State.SimulationSeconds;int cash=p.Cash;
        var result=Act(s,p.Id,Take);
        Check(result.Accepted&&result.Reason==Hit,"a hit beside the balloons is taken with \""+Hit+"\", got "+result.Accepted+" \""+result.Reason+"\"");
        var gas=Gas(p);Check(gas!=null,"a hit adds Giggle Gas");
        Check(About(gas.StartSeconds,hit+10),"it kicks in 10 s after the hit, got "+(gas.StartSeconds-hit)+" s");
        Check(About(gas.RemainingSeconds,40),"and the host ends it 40 s after the hit, 30 s after it kicks in, got "+gas.RemainingSeconds+" s");
        Check(p.Cash==cash,"a balloon is free");
        double At(double t)=>FestivalSimulation.GiggleGasStrength(gas,hit+t);
        foreach(double t in new[]{0,5,9.99,10})Check(At(t)==0,"nothing for the first 10 s: "+At(t)+" at "+t+" s");
        Check(About(At(10.5),.5)&&About(At(11),1),"then a 1 s fade in: "+At(10.5)+" at 10.5 s, "+At(11)+" at 11 s");
        Check(About(At(20),1)&&About(At(37),1),"full strength: "+At(20)+" at 20 s, "+At(37)+" at 37 s");
        Check(About(At(38.5),.5)&&About(At(39.7),.1),"a 3 s fade out: "+At(38.5)+" at 38.5 s, "+At(39.7)+" at 39.7 s");
        foreach(double t in new[]{40,45})Check(At(t)==0,"over at 40 s: "+At(t)+" at "+t+" s");
        Check(FestivalSimulation.GiggleGasStrength(null,hit+20)==0&&FestivalSimulation.GiggleGasStrength(new ActiveEffect{Id=FestivalSimulation.DoseEffect,StartSeconds=hit,RemainingSeconds=60},hit+20)==0,"only Giggle Gas has a gas strength");
    }

    // Free and unlimited, one at a time: a second hit waits until the last one has worn off, then it's another free one.
    static void OneAtATimeUntilTheLastHasWornOff()
    {
        var s=Camp(out var p);int cash=p.Cash;
        for(int round=1;round<=3;round++)
        {
            Check(Act(s,p.Id,Take).Accepted&&Hits(p)==1,"hit "+round+" is taken");
            s.Tick(5);var early=Act(s,p.Id,Take);
            Check(!early.Accepted&&Hits(p)==1,"no second hit before hit "+round+" kicks in, got "+early.Accepted+" \""+early.Reason+"\"");
            s.Tick(30);Check(!Act(s,p.Id,Take).Accepted&&Hits(p)==1,"nor while hit "+round+" is on");
            s.Tick(5.2);Check(Hits(p)==0,"setup: hit "+round+" has worn off");
        }
        Check(p.Cash==cash,"three hits, all free");
    }

    // A hit away from the balloons, inside a tent, or out of the shopping phase is refused, and adds nothing.
    static void OnlyBesideTheBalloonsAndOnlyAtCamp()
    {
        var s=Camp(out var p);
        p.X=CampFeatures.GiggleBalloonX+2.6f;var far=Act(s,p.Id,Take);
        Check(!far.Accepted&&Hits(p)==0,"2.6 m from the balloons a hit is refused, got "+far.Accepted+" \""+far.Reason+"\"");
        Check(!FestivalSimulation.AtGiggleBalloons(p),"2.6 m away is not at the balloons");
        p.X=CampFeatures.GiggleBalloonX+2.4f;Check(FestivalSimulation.AtGiggleBalloons(p),"2.4 m away is");
        Check(Act(s,p.Id,Take).Accepted&&Hits(p)==1,"and a hit is taken there");
        var mate=s.Player("p1");mate.X=CampFeatures.GiggleBalloonX;mate.Z=CampFeatures.GiggleBalloonZ;mate.CampVisitId="tent_2";
        Check(!Act(s,mate.Id,Take).Accepted&&Hits(mate)==0,"not from inside a tent");
        mate.CampVisitId="";

        LeaveCamp(s);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);
        foreach(var x in s.State.Players)Act(s,x.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew is at the festival");
        mate.X=CampFeatures.GiggleBalloonX;mate.Z=CampFeatures.GiggleBalloonZ;var level=Act(s,mate.Id,Take);
        Check(!level.Accepted&&Hits(mate)==0,"during a level a hit is refused, got "+level.Accepted+" \""+level.Reason+"\"");

        s.State.Phase="Results";s.State.Result="Success";Check(Act(s,"p0","Reset").Accepted&&s.State.Phase=="CampReview","setup: back at camp for the review");
        mate=s.Player("p1");mate.X=CampFeatures.GiggleBalloonX;mate.Z=CampFeatures.GiggleBalloonZ;
        Check(!Act(s,mate.Id,Take).Accepted&&Hits(mate)==0,"not during the review: the balloons open with the shop");
        for(int slot=0;slot<s.State.ReviewAwards.Count;slot++)foreach(var voter in s.State.Players)s.Execute(voter.Id,new GameCommand{Id="gas"+(sequence++),Kind="ReviewVote",TargetId="p0",Amount=slot});
        Check(Act(s,"p0","FinishReview").Accepted&&s.State.Phase=="Shopping","setup: the shop opens");
        Check(Act(s,mate.Id,Take).Accepted&&Hits(mate)==1,"then a hit is taken");
    }

    // Effects only ticked while a level played. The host times Giggle Gas at camp too, so the vitals line counts it down and it ends.
    static void TheHostEndsItFortySecondsAfterTheHitEvenAtCamp()
    {
        var s=Camp(out var p);Check(Act(s,p.Id,Take).Accepted,"setup: a hit");
        s.Tick(39.9);var gas=Gas(p);
        Check(gas!=null&&Math.Abs(gas.RemainingSeconds-.1)<1e-6,"39.9 s on it has 0.1 s left, got "+(gas==null?"none":gas.RemainingSeconds+" s"));
        s.Tick(.2);Check(Gas(p)==null,"40 s after the hit the host has removed it");
        Check(s.State.Phase=="Shopping","all at camp");
    }

    static void LeavingCampClearsIt()
    {
        var s=Camp(out var p);var mate=s.Player("p1");Check(Act(s,p.Id,Take).Accepted,"setup: a hit");
        mate.X=CampFeatures.GiggleBalloonX;mate.Z=CampFeatures.GiggleBalloonZ;s.Tick(25);Check(Act(s,mate.Id,Take).Accepted,"setup: a friend's hit, still kicking in");
        Check(FestivalSimulation.Giggling(p,s.State.SimulationSeconds),"setup: p0 is giggling");
        LeaveCamp(s);
        foreach(var x in s.State.Players)Check(Hits(x)==0,x.Id+"'s Giggle Gas is gone as the level starts");
    }

    // ViewFor sends the flag (EditMode GiggleGasPerceptionTests); this is the rule it reads: giggling only while the gas has strength.
    static void FriendsSeeAGiggleOnlyWhileTheGasIsOn()
    {
        var s=Camp(out var p);var mate=s.Player("p1");Check(Act(s,p.Id,Take).Accepted,"setup: a hit");
        bool Giggling(PlayerState x)=>FestivalSimulation.Giggling(x,s.State.SimulationSeconds);
        s.Tick(9.9);Check(!Giggling(p),"no giggle while it kicks in");
        s.Tick(.4);Check(Giggling(p),"a giggle once it has");
        s.Tick(29.5);Check(Gas(p)!=null&&Giggling(p),"still giggling as it fades");
        s.Tick(.3);Check(Gas(p)==null&&!Giggling(p),"no giggle once it's over");
        Check(!Giggling(mate),"a friend who took none never giggles");
    }

    static void ASnapshotKeepsTheGasGoing()
    {
        var s=Camp(out var p);Check(Act(s,p.Id,Take).Accepted,"setup: a hit");s.Tick(15);
        var saved=new FestivalSimulation();saved.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        var q=saved.Player("p0");
        Check(FestivalSimulation.Giggling(q,saved.State.SimulationSeconds),"a snapshot keeps the gas on");
        Check(!Act(saved,q.Id,Take).Accepted,"and keeps it one at a time");
        saved.Tick(25.2);Check(Gas(q)==null,"and it ends on time");
    }
}
