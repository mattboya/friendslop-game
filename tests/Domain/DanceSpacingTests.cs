using System;
using System.Collections.Generic;
using Festival.Core;

// DANCE-3: a dance (or a check dance) started closer than 1.4 m to the partner steps the dancer straight back to 1.4 m, facing
// them, so the two don't overlap in the dancer view. The step is checked like a move (the navigator, the grounds' edge, the VIP
// ropes, a carried body's reach); a blocked spot leaves the dancer where they stood. The partner never turns, and the dancer
// keeps facing them through the dance whatever the camera does.
// DANCE-2: a sale, a chat and a talk with security are danced too (the live dancer view shows every four-lane challenge), so
// they keep the same space and facing, and every note played shows as a step.
public static class DanceSpacingTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("DanceSpacing: "+message);}
    const float PartnerX=2,PartnerZ=3,PartnerYaw=200,DancerYaw=10;
    // p0 stands `metres` from a lone partner, on the given compass bearing from them, looking off at DancerYaw. For a check
    // dance p0 is the tripper with a vision of the partner still to check; for a sale p0 holds stock; security is a cop.
    static FestivalSimulation Pair(double metres,double bearing=60,string kind="Dance",float x=PartnerX,float z=PartnerZ)
    {
        var s=new FestivalSimulation(5);s.AddPlayer("p0","P0");s.State.Phase="Playing";s.State.Npcs.Clear();
        s.State.Npcs.Add(new NpcState{Id="partner",Kind=kind=="Police"?"Cop":"Wook",X=x,Z=z,Yaw=PartnerYaw,CanTalk=true});
        var p=s.Player("p0");double b=bearing*Math.PI/180;p.X=x+(float)(metres*Math.Sin(b));p.Z=z+(float)(metres*Math.Cos(b));p.Yaw=DancerYaw;
        if(kind=="ConfirmDance"){s.State.TripperId="p0";s.State.Visions.Add(new VisionState{Id="vision",Kind="Buyer",NpcId="partner"});}
        if(kind=="Sale")p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        return s;
    }
    static PlayerState Dancer(FestivalSimulation s)=>s.Player("p0");
    static NpcState Partner(FestivalSimulation s)=>s.State.Npcs.Find(n=>n.Id=="partner");
    static CommandResult Start(FestivalSimulation s,string kind="Dance")=>s.Execute("p0",new GameCommand{Id="spacing"+(sequence++),Kind=kind=="Sale"?"StartSale":kind,TargetId="partner",ItemId=kind=="Sale"?"stock_lsd":""});
    static void Starts(FestivalSimulation s,string kind="Dance"){var started=Start(s,kind);Check(started.Accepted,"setup: the "+kind+" starts: "+started.Reason);}
    static double Gap(FestivalSimulation s){var p=Dancer(s);var n=Partner(s);return Math.Sqrt((p.X-n.X)*(p.X-n.X)+(p.Z-n.Z)*(p.Z-n.Z));}
    // Degrees between where the dancer faces and the line to their partner (0 = facing them).
    static double OffFacing(FestivalSimulation s)
    {
        var p=Dancer(s);var n=Partner(s);double gap=Gap(s),yaw=p.Yaw*Math.PI/180;
        double dot=(Math.Sin(yaw)*(n.X-p.X)+Math.Cos(yaw)*(n.Z-p.Z))/gap;
        return Math.Acos(Math.Max(-1,Math.Min(1,dot)))*180/Math.PI;
    }
    static bool At(PlayerState p,float x,float z,double tolerance=1e-4)=>Math.Abs(p.X-x)<=tolerance&&Math.Abs(p.Z-z)<=tolerance;
    // A navigator that walks straight at the target but stops `shortBy` metres before it, as a wall in the way would.
    static Func<float,float,float,float,double,WorldPoint> StopsShort(double shortBy)=>(x,z,tx,tz,step)=>
    {
        double d=Math.Sqrt((tx-x)*(tx-x)+(tz-z)*(tz-z)),go=Math.Max(0,Math.Min(step,d-shortBy));
        return d<1e-9?new WorldPoint(x,z):new WorldPoint(x+(float)((tx-x)*go/d),z+(float)((tz-z)*go/d));
    };

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{OnlyPartnerChallengesKeepTheirDistance,ASaleAChatAndSecurityAreDancedToo,ACloseDancerStepsBackFacingTheirPartner,ADancerFurtherOffStaysPut,
            OnTopOfThePartnerTheyStepBackFromTheirFacing,AWallBehindKeepsTheDancerPut,TheNavigatorMustReachTheSpot,
            TheGroundsEdgeAndTheRopesKeepTheDancerPut,ACarrierKeepsTheBodyInReach,ACheckDanceKeepsTheSameSpace,
            TheWatchersSeeTheDancerWhereTheyDance,TheFacingHoldsUntilTheDanceEnds,ARefusedDanceMovesNobody})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" dance spacing test(s) failed:\n"+string.Join("\n",failures));
    }

    static void OnlyPartnerChallengesKeepTheirDistance()
    {
        Check(FestivalSimulation.DanceSpacing==1.4,"dancers keep 1.4 m from their partner");
        foreach(var kind in PartnerKinds)Check(FestivalSimulation.DancesVisibly(kind),"a "+kind+" shows the player dancing with their partner");
        foreach(var kind in new[]{"Poi","Dj","ConfirmChat","FindFriend",""})Check(!FestivalSimulation.DancesVisibly(kind),kind+" is not a partner dance");
    }

    static readonly string[] PartnerKinds={"Dance","ConfirmDance","Sale","Conversation","Police"};

    // DANCE-2: whoever the player sells to, chats with or answers to, they dance it: 1.4 m back, facing them, held there, and
    // each note they play shows as a step.
    static void ASaleAChatAndSecurityAreDancedToo()
    {
        foreach(var kind in new[]{"Sale","Conversation","Police"})
        {
            var s=Pair(.3,kind:kind);Starts(s,kind);var p=Dancer(s);
            Check(Math.Abs(Gap(s)-FestivalSimulation.DanceSpacing)<1e-3&&OffFacing(s)<.5,"a "+kind+" started at 0.3 m leaves the player 1.4 m away, facing their partner (gap "+Gap(s).ToString("0.###")+" m, "+OffFacing(s).ToString("0.#")+"° off)");
            Check(Partner(s).Yaw==PartnerYaw,"the "+kind+"'s partner doesn't turn");
            float yaw=p.Yaw;
            Check(s.TryMove("p0",p.X,p.Z,250,.1)&&p.Yaw==yaw,"the camera's yaw doesn't turn them away during the "+kind);
            s.Tick(2.1);int steps=p.VisualDanceStepSequence;
            var played=s.Execute("p0",new GameCommand{Id="spacing"+(sequence++),Kind="Rhythm",Direction=3,TimeSeconds=.1});
            Check(played.Accepted,"setup: the "+kind+" takes a note: "+played.Reason);
            Check(p.VisualDanceStepSequence==steps+1&&p.VisualDanceStepDirection==3,"a note played in the "+kind+" shows as a dance step");
        }
    }

    static void ACloseDancerStepsBackFacingTheirPartner()
    {
        var s=Pair(.3);Starts(s);var p=Dancer(s);var n=Partner(s);
        Check(Math.Abs(Gap(s)-FestivalSimulation.DanceSpacing)<1e-3,"a dance started at 0.3 m leaves the dancer 1.4 m away, not "+Gap(s).ToString("0.###"));
        double bearing=Math.Atan2(p.X-n.X,p.Z-n.Z)*180/Math.PI;
        Check(Math.Abs(bearing-60)<.1,"they step straight back from the partner (bearing "+bearing.ToString("0.#")+"°, was 60°)");
        Check(OffFacing(s)<.5,"they face their partner ("+OffFacing(s).ToString("0.#")+"° off)");
        Check(n.X==PartnerX&&n.Z==PartnerZ&&n.Yaw==PartnerYaw,"the partner neither moves nor turns");
    }

    static void ADancerFurtherOffStaysPut()
    {
        var s=Pair(2);var p=Dancer(s);float x=p.X,z=p.Z;Starts(s);
        Check(p.X==x&&p.Z==z,"a dance started at 2 m doesn't move the dancer");
        Check(OffFacing(s)<.5,"they still turn to face their partner ("+OffFacing(s).ToString("0.#")+"° off)");
        Check(Partner(s).Yaw==PartnerYaw,"the partner doesn't turn");
    }

    static void OnTopOfThePartnerTheyStepBackFromTheirFacing()
    {
        var s=Pair(0);var p=Dancer(s);p.Yaw=90;Starts(s);
        Check(At(p,PartnerX-1.4f,PartnerZ),"at 0 m, facing +x, the dancer steps back to 1.4 m on -x (now "+p.X+", "+p.Z+")");
        Check(OffFacing(s)<.5,"still facing their partner");
    }

    static void AWallBehindKeepsTheDancerPut()
    {
        var s=Pair(.3);s.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);var p=Dancer(s);float x0=p.X,z0=p.Z;Starts(s);
        Check(p.X==x0&&p.Z==z0,"with no way back the dancer stays where they were");
        Check(OffFacing(s)<.5,"but turns to face their partner");
    }

    static void TheNavigatorMustReachTheSpot()
    {
        var s=Pair(.3);s.Navigate=StopsShort(.06);var p=Dancer(s);float x0=p.X,z0=p.Z;Starts(s);
        Check(p.X==x0&&p.Z==z0,"a navigator that ends 6 cm short of the spot leaves the dancer put");
        s=Pair(.3);s.Navigate=StopsShort(.04);Starts(s);
        Check(Math.Abs(Gap(s)-FestivalSimulation.DanceSpacing)<1e-3,"one that ends 4 cm short still lets them take the spot, at exactly 1.4 m ("+Gap(s).ToString("0.###")+")");
    }

    static void TheGroundsEdgeAndTheRopesKeepTheDancerPut()
    {
        // The partner stands by the east edge and the dancer would step back past x = 39.
        var s=Pair(.3,90,x:38.5f,z:0);var p=Dancer(s);float x0=p.X;Starts(s);
        Check(p.X==x0,"the grounds' edge keeps the dancer put");
        // Palm Mirage: the west VIP zone's rope runs along x = -8, and the dancer would step back over it.
        s=Pair(.3,270,x:-7.4f,z:22);p=Dancer(s);x0=p.X;Starts(s);
        Check(s.State.FestivalIndex==Festivals.PoloFestival&&p.X==x0,"without a wristband the VIP rope keeps the dancer put");
        s=Pair(.3,270,x:-7.4f,z:22);p=Dancer(s);p.Inventory.Add(new ItemStack{ItemId=FestivalSimulation.VipWristband,Count=1});Starts(s);
        Check(Math.Abs(Gap(s)-FestivalSimulation.DanceSpacing)<1e-3,"with one they step back past it");
    }

    static void ACarrierKeepsTheBodyInReach()
    {
        // p0 alone carries the body of a friend who left, which won't budge for one carrier; it lies 1.5 m behind the partner.
        var s=Pair(.3,0);s.State.Players.Add(new PlayerState{Id="gone",Name="Gone",Life="Spirit",Connected=false});
        s.State.Bodies.Add(new BodyState{PlayerId="gone",X=PartnerX,Z=PartnerZ-1.5f});var p=Dancer(s);p.CarryBodyId="gone";float z0=p.Z;
        Starts(s);
        Check(p.Z==z0,"stepping back would leave the body out of reach, so the dancer stays put");
    }

    static void ACheckDanceKeepsTheSameSpace()
    {
        var s=Pair(.3,kind:"ConfirmDance");Starts(s,"ConfirmDance");
        Check(Math.Abs(Gap(s)-FestivalSimulation.DanceSpacing)<1e-3&&OffFacing(s)<.5,"a check dance started at 0.3 m leaves the tripper 1.4 m away, facing them");
        Check(Partner(s).Yaw==PartnerYaw,"the festivalgoer being checked doesn't turn");
        s=Pair(2,kind:"ConfirmDance");var p=Dancer(s);float x=p.X,z=p.Z;Starts(s,"ConfirmDance");
        Check(p.X==x&&p.Z==z,"one started at 2 m doesn't move");
        s=Pair(.3,kind:"ConfirmDance");s.Navigate=(fx,fz,tx,tz,step)=>new WorldPoint(fx,fz);p=Dancer(s);x=p.X;z=p.Z;Starts(s,"ConfirmDance");
        Check(p.X==x&&p.Z==z,"a blocked spot leaves the tripper where they were");
    }

    // A watcher who can't see the spot the dance starts from, only the one it steps back to, witnesses the dance.
    static void TheWatchersSeeTheDancerWhereTheyDance()
    {
        foreach(var kind in PartnerKinds)
        {
            var s=Pair(.3,0,kind);s.State.Npcs.Add(new NpcState{Id="watcher",X=PartnerX,Z=PartnerZ+6,Yaw=180});
            s.HasLineOfSight=(x,z,tx,tz)=>tz>PartnerZ+1;Starts(s,kind);
            Check(s.Interaction(Dancer(s).InteractionId).WitnessIds.Contains("watcher"),"the "+kind+"'s witnesses are counted from where the dancer ends up");
        }
    }

    static void TheFacingHoldsUntilTheDanceEnds()
    {
        var s=Pair(.3);Starts(s);var p=Dancer(s);float yaw=p.Yaw;
        Check(s.TryMove("p0",p.X,p.Z,250,.1)&&p.Yaw==yaw,"the camera's yaw doesn't turn a dancer away from their partner");
        var dance=s.Interaction(p.InteractionId);for(int guard=0;dance.Status=="Active"&&guard<200;guard++)s.Tick(.1);
        Check(dance.Status=="Complete","setup: the dance finishes");
        Check(s.TryMove("p0",p.X,p.Z,250,.1)&&p.Yaw==250,"once it ends the camera turns them again");
    }

    static void ARefusedDanceMovesNobody()
    {
        var s=Pair(.3);s.State.Interactions.Add(new InteractionState{Id="other",PlayerId="someone",TargetId="partner",Kind="Dance"});
        var p=Dancer(s);float x=p.X,z=p.Z;
        Check(!Start(s).Accepted,"setup: a busy partner refuses the dance");
        Check(p.X==x&&p.Z==z&&p.Yaw==DancerYaw,"a refused dance neither moves nor turns the player");
    }
}
