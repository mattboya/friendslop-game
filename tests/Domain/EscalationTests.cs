using System;
using System.Collections.Generic;
using Festival.Core;
// ESC-1: wook suspicion builds faster later in the weekend.
public static class EscalationTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("Escalation: "+message);}
    static bool Same(double a,double b)=>Math.Abs(a-b)<1e-9;
    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var scenario in new Action[]{SuspicionBuildsFasterLaterInTheWeekend,OnlyGainsScale})
            try{scenario();}catch(Exception e){failures.Add(e.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }

    // One wook stands at the origin looking north (+Z); player a stands in its view.
    static FestivalSimulation Watched(int festival,int level,int encore,float z)
    {
        var s=new FestivalSimulation(3);s.AddPlayer("a","A");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.EncoreTier=encore;
        s.State.Phase="Playing";s.State.Npcs.Clear();s.State.Npcs.Add(new NpcState{Id="watcher",Z=z-5});
        var a=s.Player("a");a.X=0;a.Z=z;return s;
    }
    static ObserverState Watch(FestivalSimulation s)=>s.State.Npcs[0].Observers.Find(o=>o.PlayerId=="a");
    // Suspicion the watcher gains in one second of the same exposure: sprinting in view, or standing backstage (z > 30).
    static double Sprinting(int festival,int level,int encore=0){var s=Watched(festival,level,encore,5);s.Player("a").SprintUntil=1e9;s.Tick(1);return Watch(s).Suspicion;}
    static double Backstage(int festival,int level,int encore=0){var s=Watched(festival,level,encore,35);s.Tick(1);return Watch(s).Suspicion;}

    static void SuspicionBuildsFasterLaterInTheWeekend()
    {
        double day1=Sprinting(0,0),night2=Sprinting(1,3);
        Check(day1>0,"a sprinting player in view raises suspicion on Palm Mirage Day 1 ("+day1+")");
        Check(night2>day1,"the same sprint raises suspicion faster on Ember Playa Night 2 ("+night2+") than on Palm Mirage Day 1 ("+day1+")");
        string[] names={"Day 1","Night 1","Day 2","Night 2"};
        for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<Festivals.LevelCount;level++)
        {
            double heat=Festivals.Level(festival,level,0).SuspicionMultiplier;string where=Festivals.Name(festival)+" "+names[level];
            Check(Same(Sprinting(festival,level)/day1,heat),where+": sprinting gains x"+heat+" of Day 1's (got x"+Sprinting(festival,level)/day1+")");
            Check(Same(Backstage(festival,level)/Backstage(0,0),heat),where+": standing backstage gains x"+heat+" of Day 1's (got x"+Backstage(festival,level)/Backstage(0,0)+")");
        }
        double capped=Festivals.Level(1,3,4).SuspicionMultiplier;
        Check(Same(Sprinting(1,3,4)/day1,capped),"a fourth encore lap reads the capped x"+capped+" (got x"+Sprinting(1,3,4)/day1+")");
    }

    // Cooling off is not a gain: an unseen player's suspicion fades at the same rate on every level.
    static void OnlyGainsScale()
    {
        double Cooled(int festival,int level)
        {
            var s=Watched(festival,level,0,5);s.State.Npcs[0].Yaw=180;var o=new ObserverState{PlayerId="a",Suspicion=30,LastSeenSeconds=-100};s.State.Npcs[0].Observers.Add(o);
            s.Tick(1);return o.Suspicion;
        }
        double day1=Cooled(0,0),night2=Cooled(1,3);
        Check(day1<30&&Same(day1,night2),"an unseen player cools off at the same rate on Day 1 ("+day1+") and Night 2 ("+night2+")");
    }
}
