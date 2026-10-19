using System;
using Festival.Core;

public static class DoseWalkTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("Dose walk: "+message);}
    static PlayerState With(string id,int dose,string substance,double start=3.7)
    {
        var p=new PlayerState();p.Effects.Add(new ActiveEffect{Id=id,Intensity=dose,Substance=substance,StartSeconds=start});return p;
    }
    public static void Run()
    {
        CouchLockAddsOnlyTheSpecifiedSlowdown();
        DoseSwayUsesEachEffectsOwnStartAndTheSharedClamp();
        RollyPolliesWalkEightPercentFaster();
        PonyDustStallsEveryTenSecondsAndEasesInAndOut();
        PonyDustStallsTheHostsActualMove();
    }
    static void CouchLockAddsOnlyTheSpecifiedSlowdown()
    {
        var plain=new[]{.92f,.88f,.84f,.80f};var couch=new[]{.828f,.792f,.756f,.72f};
        for(int dose=1;dose<=4;dose++)foreach(string substance in FestivalSimulation.Substances)
        {
            var p=With(FestivalSimulation.DoseEffect,dose,substance);
            float expected=substance=="weed"?couch[dose-1]:substance=="ecstasy"?plain[dose-1]*1.08f:plain[dose-1];
            Check(Math.Abs(Intoxication.MovementMultiplier(p)-expected)<1e-6,"dose "+dose+" "+substance+" walks at "+expected+", got "+Intoxication.MovementMultiplier(p));
        }
        var stacked=With(FestivalSimulation.DoseEffect,4,"weed");stacked.Effects.Add(new ActiveEffect{Id="weed"});
        Check(Intoxication.MovementMultiplier(stacked)==.65f,"shop and dose Couch Lock still share the .65 floor");
        foreach(int dose in new[]{0,-1})foreach(string substance in FestivalSimulation.Substances)
            Check(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,substance))==1,"nonpositive "+substance+" dose adds no slowdown");
        for(int dose=1;dose<=4;dose++)
            Check(Math.Abs(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,""))-plain[dose-1])<1e-6,"legacy unnamed dose keeps its speed");
    }
    static void DoseSwayUsesEachEffectsOwnStartAndTheSharedClamp()
    {
        for(int dose=1;dose<=4;dose++)foreach(string substance in new[]{"lsd","mushrooms"})
        {
            var dosed=With(FestivalSimulation.DoseEffect,dose,substance);var shop=With(substance,1,"");
            bool swayed=false;
            for(int i=0;i<10;i++)
            {
                double seconds=i*.31;float expected=Intoxication.LateralDrift(shop,seconds);swayed|=Math.Abs(expected)>.01f;
                Check(Intoxication.LateralDrift(dosed,seconds)==expected,substance+" dose "+dose+" matches shop sway at "+seconds);
                var differentStart=With(FestivalSimulation.DoseEffect,dose,substance,7.2);var shopStart=With(substance,1,"",7.2);
                Check(Intoxication.LateralDrift(differentStart,seconds)==Intoxication.LateralDrift(shopStart,seconds),"the dose owns its start time");
            }
            Check(swayed,"the shop comparison really sways");
        }
        // At time zero these two different starts put each Tongue Stamps term at its positive peak.
        var stacked=With(FestivalSimulation.DoseEffect,4,"lsd",Math.PI/2);stacked.Effects.Add(new ActiveEffect{Id="lsd",StartSeconds=5*Math.PI/2});
        Check(Intoxication.LateralDrift(stacked,0)==.24f,"shop and dose contribute separately, then clamp at +.24");
        stacked.Effects[0].StartSeconds=3*Math.PI/2;stacked.Effects[1].StartSeconds=7*Math.PI/2;
        Check(Intoxication.LateralDrift(stacked,0)==-.24f,"the lower clamp is -.24");
        foreach(string substance in new[]{"ecstasy","ketamine","weed",""})for(int dose=1;dose<=4;dose++)for(int i=0;i<10;i++)
            Check(Intoxication.LateralDrift(With(FestivalSimulation.DoseEffect,dose,substance),i*.31)==0,substance+" adds no sway");
        foreach(int dose in new[]{0,-1})foreach(string substance in FestivalSimulation.Substances)
            Check(Intoxication.LateralDrift(With(FestivalSimulation.DoseEffect,dose,substance),.7)==0,"nonpositive doses add no sway");
    }
    // TRIP-17: a Rolly Pollies dose walks 8% faster than the dose alone, so one dose walks at about sober speed.
    static void RollyPolliesWalkEightPercentFaster()
    {
        var plain=new[]{.92f,.88f,.84f,.80f};
        for(int dose=1;dose<=4;dose++)
        {
            float walk=Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,"ecstasy"));
            Check(walk==plain[dose-1]*1.08f,"Rolly Pollies dose "+dose+" walks at "+plain[dose-1]*1.08f+", got "+walk);
            for(int i=0;i<40;i++)Check(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,"ecstasy"),i*.37)==walk,"Rolly Pollies never stall, at "+i*.37+" s");
        }
        Check(Math.Abs(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,1,"ecstasy"))-1)<.01f,"one Rolly Pollies dose walks at about sober speed");
        foreach(int dose in new[]{0,-1})Check(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,"ecstasy"))==1,"a nonpositive Rolly Pollies dose walks at sober speed");
    }
    // TRIP-17: every 10 s of a Pony Dust dose, on the simulation clock from the dose's own start, the walk eases down over about
    // 0.3 s to a near stop, holds it for about a second and eases back up: never an abrupt freeze.
    static void PonyDustStallsEveryTenSecondsAndEasesInAndOut()
    {
        for(int dose=1;dose<=4;dose++)
        {
            const double start=3.7,step=.001;var p=With(FestivalSimulation.DoseEffect,dose,"ketamine",start);
            float walk=Intoxication.MovementMultiplier(p),least=walk,biggestStep=0;
            Check(walk==FestivalSimulation.DoseMovementMultiplier(dose),"setup: Pony Dust's untimed walk is the dose's own, got "+walk);
            double firstDown=-1,firstFloor=-1,lastFloor=-1,backUp=-1;float previous=Intoxication.MovementMultiplier(p,start);
            for(double t=start;t<start+10;t+=step)
            {
                float now=Intoxication.MovementMultiplier(p,t);
                Check(now<=walk+1e-6f&&now>0,"the stall only ever slows, never stops dead, at "+t+": "+now);
                Check(Math.Abs(Intoxication.MovementMultiplier(p,t+10)-now)<1e-5f,"it repeats every 10 s, at "+t);
                biggestStep=Math.Max(biggestStep,Math.Abs(now-previous));previous=now;least=Math.Min(least,now);
                if(firstDown<0&&now<walk-1e-5f)firstDown=t;
            }
            Check(firstDown>=0,"dose "+dose+": Pony Dust stalls once in every 10 s");
            Check(least<=.15f*walk,"to a near stop: "+least+" of "+walk);
            for(double t=firstDown;t<firstDown+3;t+=step)
            {
                float now=Intoxication.MovementMultiplier(p,t);
                if(now<=least+1e-5f){if(firstFloor<0)firstFloor=t;lastFloor=t;}
                if(firstFloor>=0&&backUp<0&&now>=walk-1e-5f)backUp=t;
            }
            Check(Math.Abs(firstFloor-firstDown-.3)<.02,"dose "+dose+": it eases in over about 0.3 s, took "+(firstFloor-firstDown));
            Check(Math.Abs(lastFloor-firstFloor-1)<.02,"holds the near stop for about one second, held "+(lastFloor-firstFloor));
            Check(Math.Abs(backUp-lastFloor-.3)<.02,"and eases out over about 0.3 s, took "+(backUp-lastFloor));
            // A smooth ease turns at most 1.5 times faster than a straight 0.3 s ramp, and starts and ends at rest.
            Check(biggestStep<=walk*1.5f*(float)step/.3f*1.05f,"never an abrupt freeze: the walk changed by "+biggestStep+" in "+step+" s");
            Check(walk-Intoxication.MovementMultiplier(p,firstDown+.01)<.01f*walk,"the ease starts gently");
            Check(Intoxication.MovementMultiplier(p,backUp-.01)>.99f*walk,"and ends gently");
            Check(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,"ketamine",start+2),firstDown+2)<walk-1e-5f,"the dose's own start sets when it stalls");
        }
        foreach(string substance in new[]{"lsd","mushrooms","weed",""})for(int i=0;i<40;i++)
            Check(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,2,substance),i*.37)==Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,2,substance)),substance+" never stalls");
        foreach(int dose in new[]{0,-1})for(int i=0;i<40;i++)Check(Intoxication.MovementMultiplier(With(FestivalSimulation.DoseEffect,dose,"ketamine"),i*.37)==1,"a nonpositive Pony Dust dose never stalls");
        var shop=With("ketamine",1,"");for(int i=0;i<40;i++)Check(Intoxication.MovementMultiplier(shop,i*.37)==1,"a shop effect id is no dose and never stalls");
    }
    static int sequence;
    static GameCommand C(string kind)=>new GameCommand{Id="dose_walk_"+(++sequence),Kind=kind};
    // The host's own step check (FestivalSimulation.TryMove) reads the stall on its simulation clock: a stride it takes freely is
    // refused at the near stop, where only a near-stop stride gets through, and a free stride is taken again once it lets go.
    static void PonyDustStallsTheHostsActualMove()
    {
        var s=new FestivalSimulation();var a=s.AddPlayer("a","A");var b=s.AddPlayer("b","B");a.X=b.X=0;a.Z=b.Z=19;
        s.Execute("a",C("Ready"));s.Execute("b",C("Ready"));Check(s.Execute("a",C("Start")).Accepted,"setup: start");s.Tick(FestivalSimulation.SpinSeconds+.1);
        s.Execute("a",C("MapReady"));s.Execute("b",C("MapReady"));Check(s.State.Phase=="Playing","setup: the crew reaches the festival");
        var p=s.State.Players.Find(x=>x.Id!=s.State.TripperId);s.State.Npcs.Clear();
        double start=s.State.SimulationSeconds;p.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance="ketamine",StartSeconds=start});
        float walk=Intoxication.MovementMultiplier(p);double floorAt=-1;
        for(double t=start;t<start+10&&floorAt<0;t+=.01)if(Intoxication.MovementMultiplier(p,t)<=.15f*walk)floorAt=t+.5;
        Check(floorAt>0,"setup: the stall's floor is found");
        float x=p.X,z=p.Z;
        bool Takes(double at,float stride){s.State.SimulationSeconds=at;p.X=x;p.Z=z;bool taken=s.TryMove(p.Id,x+stride,z,0,.1);p.X=x;p.Z=z;return taken;}
        float free=6*walk*.1f;
        Check(Takes(floorAt-5,free),"a free Pony Dust stride is taken between stalls");
        Check(!Takes(floorAt,free),"the same stride is refused at the near stop");
        Check(Takes(floorAt,6*Intoxication.MovementMultiplier(p,floorAt)*.1f),"where only a near-stop stride gets through");
        Check(Takes(floorAt+10,6*Intoxication.MovementMultiplier(p,floorAt)*.1f)&&!Takes(floorAt+10,free),"ten seconds on it stalls again");
        Check(Takes(floorAt+2,free),"and it lets go");
    }
}
