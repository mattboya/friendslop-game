using System;
using System.Collections.Generic;
using Festival.Core;

// POLO-1 and PLAYA-1 (journey J2): the twists a player can use where they stand reach the HUD. FestivalGuidance.TwistActions
// offers the Ferris wheel at its base, the VIP wristband at the night market's VIP stall and a passing art car, exactly where
// the rules take them, and a Ferris wheel rider's objective card reads what they spot from up there (FestivalGuidance.Hint).
public static class TwistPromptTests
{
    const string Wheel="Ride the Ferris wheel (20 s)",Car="Climb aboard the art car",Band="Buy VIP wristband  •  $15";
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("TwistPrompt: "+message);}

    // Every scenario runs, so one red run shows each missing prompt.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{TheWheelIsOfferedAtItsBase,AnArtCarIsOfferedAsItRollsPast,TheVipStallSellsAWristband,TheRiderReadsWhatTheySpot})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" twist prompt test(s) failed:\n"+string.Join("\n",failures));
    }

    // Two friends ready up at camp, the wheels spin and the crew loads into the crowd.
    static FestivalSimulation Start(int level,int festival)
    {
        var s=new FestivalSimulation(7);for(int k=0;k<2;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(s.Execute(p.Id,new GameCommand{Id="ready_"+p.Id,Kind="Ready"}).Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)s.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
        Check(s.State.Phase=="Playing","setup: the crew is at "+Festivals.Name(festival));
        return s;
    }
    static string Listed(FestivalSimulation s,PlayerState p){var labels=new List<string>();foreach(var a in FestivalGuidance.TwistActions(s.State,p))labels.Add(a.Label);return "["+string.Join(" | ",labels)+"]";}
    // The command the HUD would send for the action labelled so, or null when it isn't offered.
    static GameCommand Offered(FestivalSimulation s,PlayerState p,string label)=>FestivalGuidance.TwistActions(s.State,p).Find(a=>a.Label==label).Command;
    static bool Taken(FestivalSimulation s,PlayerState p,GameCommand c){if(c==null)return false;c.Id="prompt"+(sequence++);return s.Execute(p.Id,c).Accepted;}

    static void TheWheelIsOfferedAtItsBase()
    {
        var s=Start(0,Festivals.PoloFestival);var p=s.Player("p1");
        p.X=Festivals.WheelX+3;p.Z=Festivals.WheelZ;
        Check(Offered(s,p,Wheel)==null,"3 m from the wheel's base no ride is offered: "+Listed(s,p));
        p.X=Festivals.WheelX;p.Z=Festivals.WheelZ+2;
        Check(Offered(s,p,Wheel)!=null,"at the wheel's base the HUD offers a ride: "+Listed(s,p));
        Check(Taken(s,p,Offered(s,p,Wheel))&&FestivalSimulation.OnWheel(s.State,p.Id),"and the rules board it");
        Check(FestivalGuidance.TwistActions(s.State,p).Count==0,"up there nothing more is offered: "+Listed(s,p));
        var playa=Start(0,Festivals.PlayaFestival);var q=playa.Player("p1");q.X=Festivals.WheelX;q.Z=Festivals.WheelZ+2;
        Check(Offered(playa,q,Wheel)==null,"Ember Playa has no Ferris wheel to offer: "+Listed(playa,q));
    }

    static void AnArtCarIsOfferedAsItRollsPast()
    {
        var s=Start(0,Festivals.PlayaFestival);var p=s.Player("p1");var other=s.Player("p0");other.X=0;other.Z=19;
        var at=Festivals.ArtCarAt(1,s.State.ElapsedSeconds);p.X=at.X+1;p.Z=at.Z;
        Check(Offered(s,other,Car)==null,"away from both cars no ride is offered: "+Listed(s,other));
        Check(Offered(s,p,Car)!=null,"beside a passing art car the HUD offers a ride: "+Listed(s,p));
        Check(Taken(s,p,Offered(s,p,Car))&&FestivalSimulation.ArtCarOf(s.State,p.Id)==1,"and the rules put them aboard that car");
        Check(FestivalGuidance.TwistActions(s.State,p).Count==0,"aboard, nothing more is offered: "+Listed(s,p));
        var polo=Start(0,Festivals.PoloFestival);var q=polo.Player("p1");var there=Festivals.ArtCarAt(1,polo.State.ElapsedSeconds);q.X=there.X+1;q.Z=there.Z;
        Check(Offered(polo,q,Car)==null,"Palm Mirage has no art cars to offer: "+Listed(polo,q));
    }

    static void TheVipStallSellsAWristband()
    {
        var s=Start(1,Festivals.PoloFestival);var p=s.Player("p1");p.Cash=20;p.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");
        p.X=Festivals.VipStallX+4;p.Z=Festivals.VipStallZ;
        Check(Offered(s,p,Band)==null,"4 m from the VIP stall no wristband is offered: "+Listed(s,p));
        p.X=Festivals.VipStallX;p.Z=Festivals.VipStallZ+1;
        Check(Offered(s,p,Band)!=null,"at the VIP stall the HUD offers a wristband: "+Listed(s,p));
        Check(Taken(s,p,Offered(s,p,Band))&&p.Inventory.Exists(i=>i.ItemId==FestivalSimulation.VipWristband)&&p.Cash==5,"and the rules sell it for $15");
        Check(Offered(s,p,Band)==null,"one is enough, so it isn't offered again: "+Listed(s,p));
        var playa=Start(1,Festivals.PlayaFestival);var q=playa.Player("p1");q.X=Festivals.VipStallX;q.Z=Festivals.VipStallZ+1;
        Check(Offered(playa,q,Band)==null,"Ember Playa has no VIP stall: "+Listed(playa,q));
    }

    // A rider's view of Palm Mirage's Night 1, their trail not yet followed: both cops, as every view has them, and the lost
    // friend's spot, which FestivalSession.ViewFor sends to the rider alone while they ride.
    static void TheRiderReadsWhatTheySpot()
    {
        var s=new RoundState{Phase="Playing",FestivalIndex=Festivals.PoloFestival,LevelIndex=1,TripperId="sam"};
        var me=new PlayerState{Id="you",Name="You",X=Festivals.WheelX,Z=Festivals.WheelZ+1};s.Players.Add(me);s.Players.Add(new PlayerState{Id="sam",Name="Sam"});
        s.Npcs.Add(new NpcState{Id="cop_0",Kind="Cop",X=me.X,Z=me.Z+12});
        s.Npcs.Add(new NpcState{Id="cop_1",Kind="Cop",X=me.X+30,Z=me.Z});
        s.Npcs.Add(new NpcState{Id="wook",Kind="Wook",X=me.X-5,Z=me.Z});
        s.FriendPosition=new WorldPoint(me.X-30,me.Z+40);
        string ground=FestivalGuidance.Hint(s,me);
        s.Interactions.Add(new InteractionState{Id="ride",PlayerId=me.Id,Kind=FestivalSimulation.RideWheelKind,Status="Active"});me.InteractionId="ride";
        Check(FestivalGuidance.Hint(s,me)=="From the wheel you spot your friend 50 m NW and security 12 m N, 30 m E.","at night the rider reads where the friend and security are, trail or no trail: \""+FestivalGuidance.Hint(s,me)+"\"");
        // A big crew's second friend, lost too (CROWD-2).
        s.SecondFriend.Active=true;s.SecondFriend.Position=new WorldPoint(me.X,me.Z-33);
        Check(FestivalGuidance.Hint(s,me)=="From the wheel you spot your friends 50 m NW, 33 m S and security 12 m N, 30 m E.","both lost friends: \""+FestivalGuidance.Hint(s,me)+"\"");
        // Found friends follow their escort; they aren't lost out there any more.
        s.FriendFound=true;s.SecondFriend.Found=true;
        Check(FestivalGuidance.Hint(s,me)=="From the wheel you spot security 12 m N, 30 m E.","found friends aren't spotted: \""+FestivalGuidance.Hint(s,me)+"\"");
        // Off the wheel the rider's hint is what it was on the ground.
        s.FriendFound=false;s.SecondFriend=new LostFriendState();s.Interactions.Clear();me.InteractionId="";
        Check(FestivalGuidance.Hint(s,me)==ground&&!ground.StartsWith("From the wheel"),"on the ground: \""+FestivalGuidance.Hint(s,me)+"\"");
    }
}
