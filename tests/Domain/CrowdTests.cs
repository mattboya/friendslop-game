using System;
using System.Collections.Generic;
using Festival.Core;
// CROWD-1: a crew bunched around a player a wook sees draws suspicion faster: x(1 + .35 per friend within 5 m beyond the first),
// capped at x2.5, and at x3.0 once stacked with the level's suspicion multiplier.
public static class CrowdTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Crowd: "+message);}
    static bool Same(double a,double b)=>Math.Abs(a-b)<1e-9;
    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var scenario in new Action[]{PairsDoNotDrawAttention,FourBunchedDrawMore,SpreadingOutRemovesIt,BiggerPacksUpToTheCap,FiveMetresAndNoFurther,OnlyStandingCrewCount,BackstageScalesToo,StacksWithTheLevelUpToThree,CoolingOffIsNotScaled,MissedChatScales,GoodChatIsNotScaled,QuitScales,GroupDanceFailureScales,AnIdleFriendIsJudgedOnceForOverlappingDances})
            try{scenario();}catch(Exception e){failures.Add(e.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }

    // One wook at (0, z-5) looks north (+Z) at player a at (0, z); the other crew stand in a ring of the given radius around a,
    // all in the wook's view. Everyone starts at suspicion 20 so a rhythm result can move either way.
    static FestivalSimulation Pack(int crew,double ring=1.5,int festival=0,int level=0,int encore=0,float z=5)
    {
        var s=new FestivalSimulation(3);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.EncoreTier=encore;
        s.State.Phase="Playing";s.State.Npcs.Clear();var watcher=new NpcState{Id="watcher",Z=z-5};s.State.Npcs.Add(watcher);
        var a=s.Player("p0");a.X=0;a.Z=z;
        for(int k=1;k<crew;k++){double angle=2*Math.PI*k/(crew-1);Place(s,k,ring*Math.Sin(angle),z+ring*Math.Cos(angle));}
        foreach(var p in s.State.Players)watcher.Observers.Add(new ObserverState{PlayerId=p.Id,Suspicion=20});
        // a's partner for chats and dances stands just beyond the ring, facing away, so the watcher is the only witness.
        s.State.Npcs.Add(new NpcState{Id="partner",Z=z+2.2f,CanTalk=true});
        return s;
    }
    static void Place(FestivalSimulation s,int k,double x,double z){var p=s.Player("p"+k);p.X=(float)x;p.Z=(float)z;}
    static double Heat(FestivalSimulation s,int k)=>s.State.Npcs[0].Observers.Find(o=>o.PlayerId=="p"+k).Suspicion;
    // Suspicion the watcher gains on a in one second of sprinting in view.
    static double Sprint(FestivalSimulation s){double before=Heat(s,0);s.Player("p0").SprintUntil=s.State.SimulationSeconds+1.05;s.Tick(1);return Heat(s,0)-before;}
    static double Sprint(int crew,double ring=1.5,int festival=0,int level=0,int encore=0)=>Sprint(Pack(crew,ring,festival,level,encore));
    static double Alone=>Sprint(1);

    // Done when: one or two players bunched = x1.
    static void PairsDoNotDrawAttention()
    {
        Check(Alone>0,"a lone sprinting player in view gains suspicion ("+Alone+")");
        Check(Same(Sprint(2)/Alone,1),"a pair standing together gains x1 of a lone player's sprint (got x"+Sprint(2)/Alone+")");
    }

    // Done when: four bunched = x1.7.
    static void FourBunchedDrawMore(){Check(Same(Sprint(4)/Alone,1.7),"four bunched: a sprint in view gains x1.7 (got x"+Sprint(4)/Alone+")");}

    // Done when: spreading out removes it, and bunching up again brings it back, second by second.
    static void SpreadingOutRemovesIt()
    {
        var s=Pack(4);double bunched=Sprint(s);
        for(int k=1;k<4;k++)Place(s,k,6*k,5);double spread=Sprint(s);
        for(int k=1;k<4;k++)Place(s,k,k-2,5.5);double again=Sprint(s);
        Check(Same(bunched/Alone,1.7)&&Same(spread/Alone,1)&&Same(again/Alone,1.7),"four bunched x1.7, spread 6 m apart x1, bunched again x1.7 (got x"+bunched/Alone+", x"+spread/Alone+", x"+again/Alone+")");
    }

    // +0.35 for each friend beyond the first, up to x2.5: 3 = x1.35, 5 = x2.05, 6 = x2.4, 7 and 8 = x2.5.
    static void BiggerPacksUpToTheCap()
    {
        var expected=new Dictionary<int,double>{{3,1.35},{5,2.05},{6,2.4},{7,2.5},{8,2.5}};var got=new List<string>();bool pass=true;
        foreach(var e in expected){double gain=Sprint(e.Key)/Alone;pass&=Same(gain,e.Value);got.Add(e.Key+" = x"+gain);}
        Check(pass,"packs of 3, 5, 6, 7 and 8 gain x1.35, x2.05, x2.4, x2.5 and x2.5 (got "+string.Join(", ",got)+")");
    }

    // A friend 4.9 m from a is in the pack; at 5.1 m they are not.
    static void FiveMetresAndNoFurther()
    {
        double At(double d){var s=Pack(3);Place(s,1,1,5);Place(s,2,d,5);return Sprint(s)/Alone;}
        Check(Same(At(4.9),1.35)&&Same(At(5.1),1),"a third friend 4.9 m away joins the pack (x"+At(4.9)+"), 5.1 m away does not (x"+At(5.1)+")");
    }

    // Only connected crew standing up count: a disconnected friend, a downed one, a spirit or a detained one does not.
    static void OnlyStandingCrewCount()
    {
        var s=Pack(4);s.Disconnect("p3");double gain=Sprint(s)/Alone;bool pass=Same(gain,1.35);var got=new List<string>{"disconnected x"+gain};
        foreach(var life in new[]{"Downed","Spirit","Detained"}){s=Pack(4);s.Player("p3").Life=life;s.Player("p3").DownedRemaining=35;gain=Sprint(s)/Alone;pass&=Same(gain,1.35);got.Add(life+" x"+gain);}
        Check(pass,"four bunched with one friend disconnected, downed, a spirit or detained counts three: x1.35 (got "+string.Join(", ",got)+")");
    }

    // Standing backstage (z > 30) is a passive gain too.
    static void BackstageScalesToo()
    {
        double Backstage(int crew){var s=Pack(crew,1.5,0,0,0,35);s.Tick(1);return Heat(s,0)-20;}
        Check(Backstage(1)>0&&Same(Backstage(4)/Backstage(1),1.7),"four bunched backstage: x1.7 of a lone player's backstage gain (got x"+Backstage(4)/Backstage(1)+")");
    }

    // The pack factor multiplies the level's suspicion multiplier, and the two together stop at x3.0.
    static void StacksWithTheLevelUpToThree()
    {
        double night2=Festivals.Level(1,3,0).SuspicionMultiplier,encore=Festivals.Level(1,3,4).SuspicionMultiplier;
        Check(Same(Sprint(4,1.5,1,3)/Alone,night2*1.7),"four bunched on Ember Playa Night 2: x"+night2+" x 1.7 (got x"+Sprint(4,1.5,1,3)/Alone+")");
        Check(Same(Sprint(8,1.5,1,3)/Alone,3),"eight bunched on Ember Playa Night 2: x"+night2+" x 2.5 stops at x3.0 (got x"+Sprint(8,1.5,1,3)/Alone+")");
        Check(Same(Sprint(4,1.5,1,3,4)/Alone,3),"four bunched on a fourth encore's Night 2: x"+encore+" x 1.7 stops at x3.0 (got x"+Sprint(4,1.5,1,3,4)/Alone+")");
    }

    // Cooling off is not a gain: an unseen player in a pack cools at a lone player's rate.
    static void CoolingOffIsNotScaled()
    {
        double Cooled(int crew){var s=Pack(crew);s.State.Npcs[0].Yaw=180;foreach(var o in s.State.Npcs[0].Observers)o.LastSeenSeconds=-100;s.Tick(1);return Heat(s,0);}
        Check(Cooled(1)<20&&Same(Cooled(1),Cooled(8)),"an unseen player cools at the same rate alone ("+Cooled(1)+") and in a pack of eight ("+Cooled(8)+")");
    }

    static InteractionState Start(FestivalSimulation s,string kind){Check(s.Execute("p0",new GameCommand{Id="crowd"+(sequence++),Kind=kind,TargetId="partner"}).Accepted,"p0 starts "+kind);return s.Interaction(s.Player("p0").InteractionId);}
    static void Hit(InteractionState i){foreach(var n in RhythmChart.Create(i.ChartSeed,i.NoteCount,i.BeatSeconds).Notes)i.Inputs.Add(new RhythmInput{Direction=n.Direction,TimeSeconds=n.TimeSeconds});}
    static void Finish(FestivalSimulation s,InteractionState i){for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);Check(i.Status=="Complete",i.Kind+" completes");}

    // A missed chat (+20) counts x1.7 in a pack of four.
    static void MissedChatScales()
    {
        double Missed(int crew){var s=Pack(crew);Finish(s,Start(s,"Conversation"));return Heat(s,0)-20;}
        Check(Missed(1)==20&&Missed(2)==20&&Same(Missed(4),34),"a missed chat: +20 alone or in a pair, +34 in a pack of four (got "+Missed(1)+", "+Missed(2)+", "+Missed(4)+")");
    }
    // A good chat (-15) is not a gain, so a pack does not change it.
    static void GoodChatIsNotScaled()
    {
        double Good(int crew){var s=Pack(crew);var chat=Start(s,"Conversation");Hit(chat);Finish(s,chat);return Heat(s,0)-20;}
        Check(Good(1)==-15&&Good(4)==-15,"a good chat: -15 alone and in a pack of four (got "+Good(1)+", "+Good(4)+")");
    }
    // Quitting a chat (+20) counts x1.7 in a pack of four.
    static void QuitScales()
    {
        double Quit(int crew){var s=Pack(crew);Start(s,"Conversation");Check(s.Execute("p0",new GameCommand{Id="crowd"+(sequence++),Kind="Cancel"}).Accepted,"p0 quits");return Heat(s,0)-20;}
        Check(Quit(1)==20&&Same(Quit(4),34),"quitting a chat: +20 alone, +34 in a pack of four (got "+Quit(1)+", "+Quit(4)+")");
    }

    // A perfect dancer whose three bunched friends stand idle: the group fails (+20), and each of the four gains +34.
    static void GroupDanceFailureScales()
    {
        var s=Pack(4);var dance=Start(s,"Dance");Hit(dance);Finish(s,dance);
        var gains=new List<double>();for(int k=0;k<4;k++)gains.Add(Heat(s,k)-20);
        Check(gains.TrueForAll(g=>Same(g,34)),"a failed group dance in a pack of four: +34 each (got "+string.Join(", ",gains)+")");
    }
    // Three of the pack dance perfectly on the same beat beside a fourth who stands idle. Every group fails (+20 x1.7), and the
    // idle friend takes that verdict once, like each dancer, not once per dancer: +34 each (not +102 for the idle one).
    static void AnIdleFriendIsJudgedOnceForOverlappingDances()
    {
        var s=Pack(4);var dances=new List<InteractionState>{Start(s,"Dance")};
        // p1 and p2 each get a partner 1 m to their outer side, facing away from the crew.
        for(int k=1;k<3;k++)
        {
            var p=s.Player("p"+k);float side=p.X>0?1:-1;s.State.Npcs.Add(new NpcState{Id="partner"+k,X=p.X+side,Z=p.Z,Yaw=side*90,CanTalk=true});
            Check(s.Execute(p.Id,new GameCommand{Id="crowd"+(sequence++),Kind="Dance",TargetId="partner"+k}).Accepted,p.Id+" starts Dance");dances.Add(s.Interaction(p.InteractionId));
        }
        foreach(var d in dances)Hit(d);foreach(var d in dances)Finish(s,d);
        var gains=new List<double>();for(int k=0;k<4;k++)gains.Add(Heat(s,k)-20);
        Check(gains.TrueForAll(g=>Same(g,34)),"three perfect dancers beside an idle friend in a pack of four: +34 each, the idle friend last (got "+string.Join(", ",gains)+")");
    }
}
