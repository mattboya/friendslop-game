using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// GAS-1: a Giggle Balloon station stands at camp, off the west edge of the gathering mat. Beside it, between levels, anyone takes a
// free hit: nothing for 10 s, then 30 s of Giggle Gas (1 s in, full, 3 s out) that only the hitter feels, while friends see them
// giggle. Two at a time: no third hit until one has worn off, which the host times at camp too. It never leaves camp.
// GAS-6: each accepted take counts once on the player, in public, so every client can play its inhale once.
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
        foreach(var test in new Action[]{AHitBesideTheBalloonsKicksInTenSecondsLaterForThirty,TwoAtATimeUntilOneHasWornOff,OnlyBesideTheBalloonsAndOnlyAtCamp,
            TheHostEndsItFortySecondsAfterTheHitEvenAtCamp,LeavingCampClearsIt,BackToBackHitsStackToTwoAndEaseBack,FriendsSeeAGiggleOnlyWhileTheGasIsOn,ASnapshotKeepsTheGasGoing,
            EachAcceptedTakeCountsOnceInPublic,OlderSnapshotsCountTakesFromZero})
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
    static double Strength(PlayerState p,double now)=>FestivalSimulation.GiggleGasStrength(p,now);
    static int Takes(PlayerState p)=>p.VisualGiggleTakeSequence;
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
        Check(FestivalSimulation.GiggleGasStrength((ActiveEffect)null,hit+20)==0&&FestivalSimulation.GiggleGasStrength(new ActiveEffect{Id=FestivalSimulation.DoseEffect,StartSeconds=hit,RemainingSeconds=60},hit+20)==0,"only Giggle Gas has a gas strength");
    }

    // Free and unlimited, two at a time: a third hit waits until one has worn off, then it's another free one.
    static void TwoAtATimeUntilOneHasWornOff()
    {
        foreach(double gap in new[]{5.0,15.0})
        {
            var s=Camp(out var p);int cash=p.Cash;
            Check(Act(s,p.Id,Take).Accepted,"first hit is accepted");s.Tick(gap);
            var second=Act(s,p.Id,Take);
            Check(second.Accepted&&second.Reason==Hit&&Hits(p)==2,"second hit is accepted during onset or strength; gap="+gap+", got "+second.Accepted+" \""+second.Reason+"\"");
            Check(About(p.Effects[0].RemainingSeconds,40-gap)&&About(p.Effects[1].RemainingSeconds,40),"each hit counts down on its own: "+p.Effects[0].RemainingSeconds+" and "+p.Effects[1].RemainingSeconds+" s");
            var third=Act(s,p.Id,Take);
            Check(!third.Accepted&&third.Reason=="Two Giggle Balloons at a time: let one wear off"&&Hits(p)==2,"third is refused without adding an effect, got "+third.Accepted+" \""+third.Reason+"\"");
            s.Tick(40-gap+.1);Check(Hits(p)==1,"only the first hit wears off");
            Check(Act(s,p.Id,Take).Accepted&&Hits(p)==2,"another hit is accepted when one wears off");
            Check(p.Cash==cash,"all accepted hits are free");
        }
    }

    // Hit 1 at h and hit 2 at h+15: each keeps its own 1 s rise and 3 s fall, and the player feels their sum.
    static void BackToBackHitsStackToTwoAndEaseBack()
    {
        var s=Camp(out var p);double hit=s.State.SimulationSeconds;
        Check(Act(s,p.Id,Take).Accepted,"first hit");s.Tick(15);Check(Act(s,p.Id,Take).Accepted,"second hit");
        foreach(var sample in new[]{(5.0,0.0),(20.0,1.0),(25.5,1.5),(30.0,2.0),(38.5,1.5),(45.0,1.0),(56.0,0.0)})
        {
            double now=hit+sample.Item1;double strength=Strength(p,now);
            Check(About(strength,sample.Item2),"sum at "+sample.Item1+" is "+sample.Item2+", got "+strength);
            foreach(var e in p.Effects)Check(FestivalSimulation.GiggleGasStrength(e,now)<=1,"individual hits remain at most one");
        }
        var mate=s.Player("p1");mate.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,StartSeconds=hit,RemainingSeconds=60});
        Check(Strength(mate,hit+20)==0,"a dose is no gas");
        for(int extra=0;extra<3;extra++)mate.Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=hit,RemainingSeconds=40});
        Check(About(Strength(mate,hit+20),2),"the sum is capped at two, got "+Strength(mate,hit+20));
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
        var s=Camp(out var p);var mate=s.Player("p1");Check(Act(s,p.Id,Take).Accepted&&Act(s,p.Id,Take).Accepted&&Hits(p)==2,"setup: two hits");
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
        var s=Camp(out var p);Check(Act(s,p.Id,Take).Accepted,"setup: a hit");s.Tick(5);Check(Act(s,p.Id,Take).Accepted,"setup: second hit");s.Tick(10);
        var saved=new FestivalSimulation();saved.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        var q=saved.Player("p0");
        Check(FestivalSimulation.Giggling(q,saved.State.SimulationSeconds),"a snapshot keeps the gas on");
        Check(About(Strength(q,saved.State.SimulationSeconds),Strength(p,s.State.SimulationSeconds))&&About(Strength(q,saved.State.SimulationSeconds),1),"and their summed strength, got "+Strength(q,saved.State.SimulationSeconds));
        Check(!Act(saved,q.Id,Take).Accepted,"and refuses a third hit");
        Check(Hits(q)==2,"restored snapshot keeps both hits");saved.Tick(25.2);Check(Hits(q)==1,"the first ends on time");saved.Tick(5);Check(Gas(q)==null,"and so does the second");
        Check(Takes(q)==2,"and it keeps the take count, got "+Takes(q));
    }

    // Both of the two allowed hits count, each once; a replayed command, a refusal or the gas wearing off never moves the count.
    static void EachAcceptedTakeCountsOnceInPublic()
    {
        var s=Camp(out var p);var take=new GameCommand{Id="gas6-accepted",Kind=Take};
        Check(s.Execute(p.Id,take).Accepted&&Takes(p)==1,"an accepted take counts one, got "+Takes(p));
        Check(s.Execute(p.Id,take).Accepted&&Takes(p)==1&&Hits(p)==1,"a replayed command counts nothing more, got "+Takes(p));
        s.Tick(5);Check(Act(s,p.Id,Take).Accepted&&Takes(p)==2,"the second hit counts too, got "+Takes(p));
        Check(!Act(s,p.Id,Take).Accepted&&Takes(p)==2,"a refused third counts nothing");
        s.Tick(40.2);Check(Hits(p)==0&&Takes(p)==2,"the gas wearing off keeps the count");
        void Refused(string why){var result=Act(s,p.Id,Take);Check(!result.Accepted&&Takes(p)==2&&Hits(p)==0,why+" counts nothing, got "+result.Accepted+" \""+result.Reason+"\" and "+Takes(p));}
        p.X=CampFeatures.GiggleBalloonX+3;Refused("too far");p.X=CampFeatures.GiggleBalloonX;
        p.CampVisitId="tent_2";Refused("inside a tent");p.CampVisitId="";
        p.Life="Downed";Refused("downed");p.Life="Alive";
        s.State.Phase="Playing";Refused("at the festival");s.State.Phase="CampReview";Refused("in the review");s.State.Phase="Shopping";
        // A minigame pauses the festival, and the pause refuses the take before the balloons' own camp-only rule can.
        s.State.Phase=FestivalSimulation.MinigamePhase;var paused=Act(s,p.Id,Take);
        Check(!paused.Accepted&&paused.Reason=="Wait: the festival is paused for a minigame"&&Takes(p)==2&&Hits(p)==0,"a take during a minigame is paused and counts nothing, got "+paused.Accepted+" \""+paused.Reason+"\" and "+Takes(p));
        s.State.Phase="Shopping";
        s.Disconnect(p.Id);Refused("disconnected");s.AddPlayer(p.Id,p.Name);
        Check(Act(s,p.Id,Take).Accepted&&Takes(p)==3,"back at the balloons a take counts again, got "+Takes(p));
        Check(Takes(s.Player("p1"))==0,"a friend who took none counts none");
    }

    // A snapshot from before the count restores at zero and counts on from there.
    static void OlderSnapshotsCountTakesFromZero()
    {
        var s=Camp(out var p);Check(Act(s,p.Id,Take).Accepted,"setup: a hit");
        var json=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json));
        foreach(var player in json["Players"].AsArray())player.AsObject().Remove("VisualGiggleTakeSequence");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        var q=old.Player(p.Id);Check(Takes(q)==0&&Hits(q)==1,"the older snapshot keeps its hit and counts no take");
        Check(Act(old,q.Id,Take).Accepted&&Takes(q)==1,"its next take counts one, got "+Takes(q));
    }
}
