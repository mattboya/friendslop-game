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
        foreach(var scenario in new Action[]{EightSameSidePursuersReachSeparateRings,SettledInnerSwarmStrikesWhileOuterPairWaits,()=>EightPursuersFollow(true,false),()=>EightPursuersFollow(false,true),StableReservationsSurviveListAndMembershipChanges,OldestSwarmWaiterOwnsVacatedInnerPlace,ReturningSwarmWaiterKeepsArrivalPriority,SwarmsTakeInnerPlacesBeforeQuestioners,EightyPursuersHaveDistinctOverflowPlaces,DenseSeparatedCrowdsRemainSafeWithBlockedGeometry,ExistingOverlapRecoversThroughNavigation,RecoveryCannotCutThroughAnOverlappedNeighbour,RejectedNavigationStandsStill,AttackCapWindupCooldownAndPreMoveDistanceArePreserved,ApproachesReleaseOnDistractionSpiritAndDisconnect,PhysicalBoothPauseFreezesAndResumesReservations,()=>PursuersDoNotStepInsideEachOther(false,false),()=>PursuersDoNotStepInsideEachOther(false,true),()=>PursuersDoNotStepInsideEachOther(true,false),()=>PursuersDoNotStepInsideEachOther(true,true),PairsDoNotDrawAttention,FourBunchedDrawMore,SpreadingOutRemovesIt,BiggerPacksUpToTheCap,FiveMetresAndNoFurther,OnlyStandingCrewCount,BackstageScalesToo,StacksWithTheLevelUpToThree,CoolingOffIsNotScaled,AwkwardChatScales,GoodChatIsNotScaled,QuitScales,GroupDanceFailureScales,AnIdleFriendIsJudgedOnceForOverlappingDances,ARefusedSaleIsNotScaled})
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
    static void Hit(InteractionState i){foreach(var n in RhythmChart.For(i).Notes)i.Inputs.Add(new RhythmInput{Direction=n.Direction,TimeSeconds=n.TimeSeconds});}
    static void Finish(FestivalSimulation s,InteractionState i){for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);Check(i.Status=="Complete",i.Kind+" completes");}
    // DIALOG-3: p0 gives the smooth reply on every turn of a chat.
    static void Smooth(FestivalSimulation s,InteractionState i){while(i.Status=="Active"&&i.Turns.Count>0){var t=i.Turns[i.Turns.Count-1];Check(s.Execute("p0",new GameCommand{Id="crowd"+(sequence++),Kind="Reply",Amount=Array.IndexOf(t.Grades,ConversationLines.Smooth),Direction=i.Turns.Count-1}).Accepted,"p0 replies smoothly");}}

    // An all-awkward chat (+20) counts x1.7 in a pack of four.
    static void AwkwardChatScales()
    {
        double Missed(int crew){var s=Pack(crew);var chat=Start(s,"Conversation");while(chat.Status=="Active"){var turn=chat.Turns[chat.Turns.Count-1];Check(s.Execute("p0",new GameCommand{Id="crowd"+(sequence++),Kind="Reply",Amount=Array.IndexOf(turn.Grades,ConversationLines.Awkward),Direction=chat.Turns.Count-1}).Accepted,"p0 replies awkwardly");}Finish(s,chat);return Heat(s,0)-20;}
        Check(Missed(1)==20&&Missed(2)==20&&Same(Missed(4),34),"an all-awkward chat: +20 alone or in a pair, +34 in a pack of four (got "+Missed(1)+", "+Missed(2)+", "+Missed(4)+")");
    }
    // A smooth chat (-15) is not a gain, so a pack does not change it.
    static void GoodChatIsNotScaled()
    {
        double Good(int crew){var s=Pack(crew);var chat=Start(s,"Conversation");Smooth(s,chat);Finish(s,chat);return Heat(s,0)-20;}
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
    // A regular turning a sale down (+10) is neither a passive gain nor a rhythm result, so a pack does not change it.
    static void ARefusedSaleIsNotScaled()
    {
        double Refused(int crew)
        {
            var s=Pack(crew);var regular=s.State.Npcs.Find(n=>n.Id=="partner");regular.Role="Regular";
            s.Player("p0").Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            Check(!s.Execute("p0",new GameCommand{Id="crowd"+(sequence++),Kind="StartSale",TargetId="partner",ItemId="stock_lsd"}).Accepted,"the regular turns p0's sale down");
            return regular.Observers.Find(o=>o.PlayerId=="p0").Suspicion;
        }
        Check(Refused(1)==10&&Refused(8)==10,"a refused sale: +10 alone and in a pack of eight (got "+Refused(1)+", "+Refused(8)+")");
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
    // CROWD-6 small prototype: exercise the actual Tick -> WookTick -> Move/Navigate path.
    // Two already separated swarmers approaching from one side must not merge at strike range.
    static void PursuersDoNotStepInsideEachOther(bool downed,bool navigation)
    {
        {
            var s=new FestivalSimulation(3);s.AddPlayer("target","Target");
            s.State.Phase="Playing";s.State.SimulationSeconds=10;s.State.Npcs.Clear();
            var p=s.Player("target");p.X=0;p.Z=0;p.RecoveryUntil=100;
            if(downed){p.Life="Downed";p.DownedRemaining=35;}
            int navigated=0;
            if(navigation)s.Navigate=(x,z,tx,tz,step)=>
            {
                navigated++;double d=Math.Sqrt((tx-x)*(tx-x)+(tz-z)*(tz-z));
                double travel=Math.Min(step,d);
                return new WorldPoint(x+(float)((tx-x)*travel/d),z+(float)((tz-z)*travel/d));
            };
            foreach(float z in new[]{1.35f,2.25f})
            {
                var n=new NpcState{Id="pursuer"+s.State.Npcs.Count,Z=z,Yaw=180,Mode="Swarming",TargetId=p.Id};
                n.Observers.Add(new ObserverState{PlayerId=p.Id,Suspicion=95,AccusationSeconds=0,LastSeenSeconds=10});
                s.State.Npcs.Add(n);
            }
            for(int step=0;step<20;step++)
            {
                s.Tick(.1);var a=s.State.Npcs[0];var b=s.State.Npcs[1];
                double gap=Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
                Check(gap>=.7-1e-5,"same-side swarmers stay 0.7 m apart at every step: gap="+gap+", downed="+downed+", Navigate="+navigation);
            }
            Check(!navigation||navigated>0,"pursuit calls the installed navigation delegate");
        }
    }

    static void EightSameSidePursuersReachSeparateRings()
    {
        var s=new FestivalSimulation(3);var p=s.AddPlayer("target","Target");p.X=0;p.Z=0;p.RecoveryUntil=100;
        s.State.Phase="Playing";s.State.SimulationSeconds=10;s.State.Npcs.Clear();
        for(int k=0;k<8;k++)
        {
            var n=new NpcState{Id="ring"+k,Z=3+k*.85f,Yaw=180,Mode="Swarming",TargetId=p.Id};
            n.Observers.Add(new ObserverState{PlayerId=p.Id,Suspicion=95,AccusationSeconds=0,LastSeenSeconds=10});s.State.Npcs.Add(n);
        }
        for(int step=0;step<200;step++)
        {
            s.Tick(.1);
            for(int a=0;a<8;a++)for(int b=a+1;b<8;b++)Check(Gap(s.State.Npcs[a],s.State.Npcs[b])>=.7-1e-5,"eight same-side pursuers keep the gap at step "+step);
        }
        int inner=0,outer=0;foreach(var n in s.State.Npcs){double d=Math.Sqrt(n.X*n.X+n.Z*n.Z);if(d<=1.5)inner++;if(Math.Abs(d-2.2)<.15)outer++;}
        Check(inner==6&&outer==2,"eight same-side pursuers fill six inner and two outer slots: inner="+inner+", outer="+outer+" places "+string.Join(";",s.State.Npcs.ConvertAll(n=>n.Id+"="+n.X+","+n.Z)));
    }
    static double Gap(NpcState a,NpcState b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));

    static FestivalSimulation Pursuit(int count=8,bool downed=false)
    {
        var s=new FestivalSimulation(3);var p=s.AddPlayer("target","Target");s.AddPlayer("other","Other");
        p.X=0;p.Z=0;p.RecoveryUntil=1000;s.Player("other").X=60;s.Player("other").Z=60;
        if(downed){p.Life="Downed";p.DownedRemaining=35;}
        s.State.Phase="Playing";s.State.SimulationSeconds=10;s.State.Npcs.Clear();
        for(int k=0;k<count;k++)
        {
            var n=new NpcState{Id="arrival"+k,Z=3+k*.85f,Yaw=180};
            n.Observers.Add(new ObserverState{PlayerId=p.Id,Suspicion=95,AccusationSeconds=0,LastSeenSeconds=10});s.State.Npcs.Add(n);
        }
        return s;
    }
    static void Separate(FestivalSimulation s,string at)
    {
        for(int a=0;a<s.State.Npcs.Count;a++)for(int b=a+1;b<s.State.Npcs.Count;b++)
            if(s.State.Npcs[a].Kind=="Wook"&&s.State.Npcs[b].Kind=="Wook")Check(Gap(s.State.Npcs[a],s.State.Npcs[b])>=.7-1e-5,at+": "+s.State.Npcs[a].Id+" / "+s.State.Npcs[b].Id+" gap="+Gap(s.State.Npcs[a],s.State.Npcs[b]));
    }
    static double From(NpcState n,PlayerState p)=>Math.Sqrt((n.X-p.X)*(n.X-p.X)+(n.Z-p.Z)*(n.Z-p.Z));
    static void Rings(FestivalSimulation s)
    {
        var p=s.Player("target");int inner=0,outer=0;
        foreach(var n in s.State.Npcs){double d=From(n,p);if(d<=1.5)inner++;if(Math.Abs(d-2.2)<.15)outer++;}
        Check(inner==6&&outer==2,"six inner and two outer arrivals: "+inner+" / "+outer+" places "+string.Join(";",s.State.Npcs.ConvertAll(n=>n.Id+"="+n.X+","+n.Z)));
    }
    static void EightPursuersFollow(bool downed,bool moving)
    {
        var s=Pursuit(downed:downed);var p=s.Player("target");
        for(int step=0;step<220;step++){if(moving){p.X+=.025f;p.Z-=.015f;}s.Tick(.1);Separate(s,"eight target downed="+downed+", moving="+moving+", step="+step);}
        Rings(s);Check(p.Health==100&&(!downed||p.Life=="Downed"),"downed/recovering target remains unharmed before death");
    }
    static void StableReservationsSurviveListAndMembershipChanges()
    {
        var s=Pursuit();for(int k=0;k<200;k++)s.Tick(.1);
        var before=s.State.Npcs.ConvertAll(n=>(n.Id,n.X,n.Z));s.State.Npcs.Reverse();s.Tick(.1);
        foreach(var at in before){var n=s.State.Npcs.Find(x=>x.Id==at.Id);Check(Math.Abs(n.X-at.X)<.01&&Math.Abs(n.Z-at.Z)<.01,"list reversal cannot reshuffle "+n.Id+"'s reserved place");}
        var departed=s.State.Npcs.Find(n=>n.Id=="arrival7");s.State.Npcs.Remove(departed);s.Tick(.1);
        foreach(var at in before){var n=s.State.Npcs.Find(x=>x.Id==at.Id);if(n!=null)Check(Math.Abs(n.X-at.X)<.01&&Math.Abs(n.Z-at.Z)<.01,"outer departure does not reshuffle "+n.Id);}
        var first=s.State.Npcs.Find(n=>n.Id=="arrival0");first.Observers[0].Suspicion=0;
        var other=first.Observers.Find(o=>o.PlayerId=="other");other.Suspicion=95;other.AccusationSeconds=0;other.LastSeenSeconds=s.State.SimulationSeconds;
        s.Player("other").X=5;s.Player("other").Z=0;s.Tick(.1);Check(first.TargetId=="other","changed suspicion switches actual target");
        for(int k=0;k<120;k++){s.Tick(.1);Separate(s,"target switch "+k);}Check(From(first,s.Player("other"))<1.5,"new target gets a new pursuit place");
    }
    static void SwarmsTakeInnerPlacesBeforeQuestioners()
    {
        var s=Pursuit(7);var q=s.State.Npcs[0];q.Observers[0].Suspicion=50;q.Observers[0].AccusationSeconds=-1;
        for(int k=0;k<220;k++){s.Tick(.1);Separate(s,"swarm priority "+k);}
        Check(q.Mode=="Questioning"&&Math.Abs(From(q,s.Player("target"))-2.2)<.15,"a questioner waits outside the six eligible swarmers");
        foreach(var n in s.State.Npcs)if(n!=q)Check(n.Mode=="Swarming"&&From(n,s.Player("target"))<1.5,"all six swarmers have an inner place: "+n.Id+" "+n.Mode+" d="+From(n,s.Player("target"))+" at="+n.X+","+n.Z);
    }
    static void EightyPursuersHaveDistinctOverflowPlaces()
    {
        var s=Pursuit(80);
        // Feasible separated geometry near each expected ring, including the thirteenth outer ring.
        for(int k=0;k<80;k++){double radius=(k<6?1.3:2.2+.9*(k/6-1))+.3,angle=k%6*Math.PI/3;s.State.Npcs[k].X=(float)(radius*Math.Sin(angle));s.State.Npcs[k].Z=(float)(radius*Math.Cos(angle));}
        for(int k=0;k<120;k++){s.Tick(.1);Separate(s,"80 overflow step "+k);}
        for(int k=0;k<80;k++){double radius=k<6?1.3:2.2+.9*(k/6-1);Check(Math.Abs(From(s.State.Npcs[k],s.Player("target"))-radius)<.1,"overflow arrival "+k+" reaches its distinct ring radius "+radius+", got "+From(s.State.Npcs[k],s.Player("target")));}
    }
    static void ExistingOverlapRecoversThroughNavigation()
    {
        var s=Pursuit(3);s.State.Npcs[0].Z=3;s.State.Npcs[1].Z=3.2f;s.State.Npcs[2].Z=4;s.State.Npcs[2].DistractedUntil=1000;
        int approved=0;s.Navigate=(x,z,tx,tz,step)=>{approved++;double d=Math.Sqrt((tx-x)*(tx-x)+(tz-z)*(tz-z)),travel=Math.Min(step,d);return d<=1e-6?new WorldPoint(x,z):new WorldPoint(x+(float)((tx-x)*travel/d),z+(float)((tz-z)*travel/d));};
        double previous=Gap(s.State.Npcs[0],s.State.Npcs[1]);
        for(int k=0;k<20;k++){var before=s.State.Npcs.ConvertAll(n=>(n.X,n.Z));s.Tick(.1);double gap=Gap(s.State.Npcs[0],s.State.Npcs[1]);Check(gap+1e-5>=Math.Min(.7,previous),"recovery never worsens an existing overlap");previous=gap;for(int j=0;j<3;j++){var n=s.State.Npcs[j];Check(Math.Sqrt((n.X-before[j].X)*(n.X-before[j].X)+(n.Z-before[j].Z)*(n.Z-before[j].Z))<=.3601,"recovery remains speed bounded");}Check(Gap(s.State.Npcs[0],s.State.Npcs[2])>=.7-1e-5&&Gap(s.State.Npcs[1],s.State.Npcs[2])>=.7-1e-5,"recovery cannot create another overlap");}
        Check(previous>=.7-1e-5&&approved>0,"navigation-approved bounded recovery separates existing overlap, gap="+previous);
    }
    static void RejectedNavigationStandsStill()
    {
        foreach(int bad in new[]{0,1,2})
        {
            var s=Pursuit(2);s.Navigate=(x,z,tx,tz,step)=>bad==0?null:bad==1?new WorldPoint(float.NaN,z):new WorldPoint(x+10,z);
            var before=s.State.Npcs.ConvertAll(n=>(n.X,n.Z));s.Tick(1);
            for(int k=0;k<2;k++)Check((s.State.Npcs[k].X,s.State.Npcs[k].Z)==before[k],"null, nonfinite or overbudget navigation stands still");
        }
    }

    static void AttackCapWindupCooldownAndPreMoveDistanceArePreserved()
    {
        var s=Pursuit();var p=s.Player("target");p.Health=1000;p.RecoveryUntil=0;
        for(int k=0;k<8;k++){double angle=k*Math.PI/4;s.State.Npcs[k].X=(float)(1.3*Math.Sin(angle));s.State.Npcs[k].Z=(float)(1.3*Math.Cos(angle));}
        s.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);s.Tick(.1);
        Check(s.State.Npcs.FindAll(n=>n.AttackAt>0).Count==6&&p.Health==1000,"six windups begin, and a blocked outer pursuit cannot bypass the six-attacker cap");
        double attack=s.State.Npcs[0].AttackAt;Check(Math.Abs(attack-s.State.SimulationSeconds-.75)<1e-8,"windup is still .75 world seconds");
        s.Tick(.7);Check(p.Health==1000,"no damage before the original windup deadline");s.Tick(.1);Check(p.Health==880,"six original twenty-point strikes land");
        double cooldown=s.State.Npcs[0].AttackCooldownUntil;Check(Math.Abs(cooldown-s.State.SimulationSeconds-2)<1e-8,"each strike still has a two-second cooldown");
        s.Tick(1.9);Check(p.Health==880&&s.State.Npcs.TrueForAll(n=>n.AttackAt==0),"cooldown cannot start another windup early");
        var pre=Pursuit(1);pre.Player("target").RecoveryUntil=0;pre.State.Npcs[0].Z=1.6f;pre.Tick(.1);
        Check(From(pre.State.Npcs[0],pre.Player("target"))<=1.5&&pre.State.Npcs[0].AttackAt==0,"arrival cannot retroactively attack using post-move distance");pre.Tick(.1);Check(pre.State.Npcs[0].AttackAt>0,"the following pre-move in-range step starts the windup");
        pre.HasLineOfSight=(x,z,tx,tz)=>false;pre.Tick(.8);Check(pre.Player("target").Health==100,"the existing strike line-of-sight check remains authoritative");
    }
    static void ApproachesReleaseOnDistractionSpiritAndDisconnect()
    {
        foreach(int lifecycle in new[]{0,1,2})
        {
            var s=Pursuit(2);s.Tick(.1);long epoch=s.State.Npcs[0].ApproachEpoch;Check(epoch>0&&s.State.Npcs[1].ApproachEpoch>epoch,"each arrival has a unique host epoch");
            if(lifecycle==0)foreach(var n in s.State.Npcs)n.DistractedUntil=100;
            else if(lifecycle==1)s.Player("target").Life="Spirit";
            else s.Disconnect("target");
            s.Tick(.1);foreach(var n in s.State.Npcs)Check(n.ApproachEpoch==0&&n.ApproachSlot==-1&&n.ApproachTargetId=="","lifecycle releases the host reservation: "+lifecycle);
            if(lifecycle==0)foreach(var n in s.State.Npcs)n.DistractedUntil=0;
            else{s.Player("target").Life="Alive";s.Player("target").Connected=true;}
            s.Tick(.1);Check(s.State.Npcs[0].ApproachEpoch>epoch,"resumed pursuit receives a fresh arrival epoch");
        }
    }
    static void PhysicalBoothPauseFreezesAndResumesReservations()
    {
        var s=Pursuit(2);var booth=Festivals.BoothSpots[0];
        foreach(var p in s.State.Players){p.X=booth.X;p.Z=booth.Z;}
        foreach(var n in s.State.Npcs){n.X=booth.X;n.Z+=booth.Z+12;}
        s.State.Booth=new MinigameState{Spot=0,Kind=FestivalSimulation.MysteryCupsGame};
        for(int k=0;k<40&&s.State.Phase=="Playing";k++)s.Tick(.1);
        Check(s.State.Phase==FestivalSimulation.MinigamePhase,"actual physical booth ring starts a pause");var before=s.State.Npcs.ConvertAll(n=>(n.X,n.Z,n.ApproachEpoch,n.ApproachSlot));s.Tick(6);
        for(int k=0;k<2;k++){var n=s.State.Npcs[k];Check((n.X,n.Z,n.ApproachEpoch,n.ApproachSlot)==before[k],"positions and reservations freeze during a booth pause");}
        s.ScoreMinigame(0);s.Tick(FestivalSimulation.MinigameBannerSeconds+.1);Check(s.State.Phase=="Playing","actual banner resume restores Playing");
        foreach(var n in s.State.Npcs)Check(n.ApproachEpoch==before[s.State.Npcs.IndexOf(n)].ApproachEpoch,"a pause does not reassign pursuit membership");Separate(s,"booth resume");
    }

    static void OldestSwarmWaiterOwnsVacatedInnerPlace()
    {
        var s=Pursuit();for(int k=0;k<200;k++)s.Tick(.1);
        var oldest=s.State.Npcs.Find(n=>n.Id=="arrival6");var later=s.State.Npcs.Find(n=>n.Id=="arrival7");long epoch=oldest.ApproachEpoch;
        s.State.Npcs.Find(n=>n.Id=="arrival0").DistractedUntil=1000;s.State.Npcs.Reverse();s.Tick(.1);
        Check(oldest.ApproachSlot<6&&later.ApproachSlot>=6&&oldest.ApproachEpoch==epoch,"oldest reserved swarm arrival, rather than list order, inherits a vacated inner place");
    }

    static void DenseSeparatedCrowdsRemainSafeWithBlockedGeometry()
    {
        for(int variant=0;variant<3;variant++)
        {
            var s=Pursuit(80);
            for(int k=0;k<80;k++){s.State.Npcs[k].X=(k%10-4.5f)*.9f;s.State.Npcs[k].Z=(k/10-3.5f)*.9f+variant*.1f;}
            if(variant==1)s.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);
            else if(variant==2)s.Navigate=(x,z,tx,tz,step)=>{double d=Math.Sqrt((tx-x)*(tx-x)+(tz-z)*(tz-z)),travel=Math.Min(step,d);float xx=x+(float)((tx-x)*travel/d),zz=z+(float)((tz-z)*travel/d);return xx>4?new WorldPoint(x,z):new WorldPoint(xx,zz);};
            Separate(s,"feasible dense start "+variant);
            for(int step=0;step<80;step++)
            {
                var before=s.State.Npcs.ConvertAll(n=>(n.X,n.Z));s.Tick(.1);Separate(s,"dense separated property "+variant+" step "+step);
                for(int k=0;k<80;k++){var n=s.State.Npcs[k];Check(!float.IsNaN(n.X)&&!float.IsInfinity(n.X)&&!float.IsNaN(n.Z)&&!float.IsInfinity(n.Z),"dense geometry always stays finite");Check(Math.Sqrt((n.X-before[k].X)*(n.X-before[k].X)+(n.Z-before[k].Z)*(n.Z-before[k].Z))<=.3601,"dense geometry remains bounded and may legitimately stand still");}
            }
        }
    }

    static void RecoveryCannotCutThroughAnOverlappedNeighbour()
    {
        var s=Pursuit(3);var moving=s.State.Npcs[0];moving.Z=3;
        var held=s.State.Npcs[1];held.Z=2.9f;held.DistractedUntil=1000;s.State.Npcs[2].Z=4;s.State.Npcs[2].DistractedUntil=1000;
        float x=moving.X,z=moving.Z;double old=Gap(moving,held);s.Tick(.1);
        double dx=moving.X-x,dz=moving.Z-z,length=dx*dx+dz*dz,fraction=length<1e-12?0:Math.Max(0,Math.Min(1,((held.X-x)*dx+(held.Z-z)*dz)/length));
        double closest=Math.Sqrt((x+fraction*dx-held.X)*(x+fraction*dx-held.X)+(z+fraction*dz-held.Z)*(z+fraction*dz-held.Z));
        Check(closest+1e-6>=old,"approved recovery cannot cut through an already overlapping neighbour, closest="+closest+", original="+old);
    }

    static void ReturningSwarmWaiterKeepsArrivalPriority()
    {
        var s=Pursuit();for(int k=0;k<200;k++)s.Tick(.1);
        var oldest=s.State.Npcs.Find(n=>n.Id=="arrival6");var later=s.State.Npcs.Find(n=>n.Id=="arrival7");
        foreach(var n in new[]{oldest,later}){n.Observers[0].Suspicion=75;n.Observers[0].AccusationSeconds=s.State.SimulationSeconds-4;}s.Tick(.1);
        Check(oldest.Mode=="Accusing"&&later.Mode=="Accusing","actual rules briefly return the waiting pair to accusations");
        foreach(var n in new[]{oldest,later})n.Observers[0].Suspicion=95;
        s.State.Npcs.Find(n=>n.Id=="arrival0").DistractedUntil=1000;s.State.Npcs.Reverse();s.Tick(.1);
        Check(oldest.ApproachSlot<6&&later.ApproachSlot>=6,"the older eligible returning swarmer keeps priority before its mode is refreshed this step");
    }

    static void SettledInnerSwarmStrikesWhileOuterPairWaits()
    {
        var s=Pursuit();for(int k=0;k<200;k++)s.Tick(.1);var p=s.Player("target");p.Health=1000;p.RecoveryUntil=0;
        s.Tick(.1);int inner=0,outer=0;
        foreach(var n in s.State.Npcs)if(From(n,p)<=1.5){inner++;Check(n.AttackAt>0,"each settled inner swarmer starts the original windup");}else{outer++;Check(n.AttackAt==0,"the two waiting outer swarmers cannot attack");}
        Check(inner==6&&outer==2,"six actual inner windups leave the outer pair waiting");s.Tick(.7);Check(p.Health==1000,"settled windups still cannot hit early");s.Tick(.1);Check(p.Health==880,"all six settled inner strikes retain twenty-point damage");
        foreach(var n in s.State.Npcs)if(From(n,p)>1.5)Check(n.AttackAt==0&&n.AttackCooldownUntil==0,"outer waits neither strike nor consume attack cooldown");
    }

}
