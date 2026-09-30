using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// LOOP-3: Night 2 ends only when the friend is extracted and every connected player is home; the dead are carried back.
public static class FinaleTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool condition,string message){if(!condition)throw new Exception("Finale: "+message);}
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{AnyoneAwayBlocksTheFinale,DetainedBlocksTheFinale,DownedInsideTheGateIsHome,NightTwoDeathsLeaveABody,RevivalLiftsTheBody,
            OneCarrierDragsTheFirstBody,LaterBodiesNeedTwoCarriers,CarriersStayWithinReach,RevivingTheFirstBodyPromotesTheNext,CarriersLetGo,SnapshotsKeepBodies,
            AFriendWhoLeftDoesNotHoldTheFirstBody})
            try{test();}catch(Exception e){failures.Add(test.Method.Name+": "+e.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }

    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="")=>s.Execute(id,new GameCommand{Id="finale_"+(++sequence),Kind=kind,TargetId=target});
    // Mid-level (3 = Night 2) with the friend found and the whole crew at the camp gate; no crowd, so nobody gets attacked.
    static FestivalSimulation Finale(int crew,int level=3)
    {
        var s=new FestivalSimulation(12);
        for(int i=0;i<crew;i++)s.AddPlayer(((char)('a'+i)).ToString(),"P"+i);
        s.State.LevelIndex=level;s.State.Phase="Playing";s.State.Npcs.Clear();
        s.State.FriendFound=true;s.State.FriendPosition=new WorldPoint(Festivals.CampGateX,Festivals.CampGateZ);
        foreach(var p in s.State.Players){p.X=Festivals.CampGateX;p.Z=Festivals.CampGateZ;}
        return s;
    }
    static PlayerState Place(FestivalSimulation s,string id,float x,float z){var p=s.Player(id);p.X=x;p.Z=z;return p;}
    // Player a extracts at the gate; true when that ends the level with Success.
    static bool Extracts(FestivalSimulation s){if(!Act(s,"a","Extract").Accepted)return false;s.Tick(3.1);return s.State.Result=="Success";}
    static void Kill(FestivalSimulation s,string id,float x,float z){var p=Place(s,id,x,z);p.Life="Downed";p.DownedRemaining=.05;s.Tick(.1);Check(p.Life=="Spirit",id+" bleeds out");}
    static BodyState Body(FestivalSimulation s,string id)=>s.State.Bodies.Find(b=>b.PlayerId==id);
    // Half a second of walking toward the gate (-z).
    static bool Stride(FestivalSimulation s,string id,float metres){var p=s.Player(id);return s.TryMove(id,p.X,p.Z-metres,180,.5);}
    static bool Same(float a,float b)=>Math.Abs(a-b)<1e-4;

    static void AnyoneAwayBlocksTheFinale()
    {
        var s=Finale(3);Place(s,"c",0,-20);
        Check(!Act(s,"a","Extract").Accepted,"Night 2 refuses the extract while c is 12 m from the gate");
        Place(s,"c",3,-35);
        Check(Extracts(s),"with c inside the 5 m gate radius the crew goes home");
        s=Finale(3);Check(Act(s,"a","Extract").Accepted,"everyone home: the extract starts");Place(s,"c",0,-20);s.Tick(3.1);
        Check(s.State.Phase=="Playing","c wandering off before the extract finishes means no win yet");
        s=Finale(3);Place(s,"c",0,-20);s.Disconnect("c");
        Check(Extracts(s),"a player who left the game is not waited for");
        s=Finale(3,1);Place(s,"c",0,-20);
        Check(Extracts(s),"Night 1 keeps the old rescue: the friend at the gate is enough");
    }

    static void DetainedBlocksTheFinale()
    {
        var s=Finale(2);Place(s,"b",27,5).Life="Detained";
        Check(!Act(s,"a","Extract").Accepted,"a detained crew member is not home");
        s.Player("b").Life="Alive";Place(s,"b",1,-31);
        Check(Extracts(s),"released and back at the gate, the crew can leave");
    }

    static void DownedInsideTheGateIsHome()
    {
        var s=Finale(2);var b=Place(s,"b",2,-29);b.Life="Downed";b.DownedRemaining=30;
        Check(Extracts(s),"a downed player inside the gate radius counts as home");
        s=Finale(2);b=Place(s,"b",0,-20);b.Life="Downed";b.DownedRemaining=30;
        Check(!Act(s,"a","Extract").Accepted,"a downed player out in the field does not");
    }

    static void NightTwoDeathsLeaveABody()
    {
        var s=Finale(2);Kill(s,"b",6,-20);var body=Body(s,"b");
        Check(s.State.Bodies.Count==1&&body!=null&&body.X==6&&body.Z==-20,"b's body lies where b died");
        Check(s.State.Drops.Exists(d=>d.ItemId=="wristband"&&d.OwnerId=="b"),"b still drops the wristband that revives them");
        Check(!Act(s,"a","Extract").Accepted,"a body out in the field blocks the finale");
        s=Finale(2);Kill(s,"b",1,-30);
        Check(Extracts(s),"a body inside the gate radius is home");
        foreach(var level in new[]{0,2,1}){s=Finale(2,level);Kill(s,"b",6,-20);Check(s.State.Bodies.Count==0,"level "+level+" deaths leave no body");}
        Check(Extracts(s),"and Night 1 still ends on the rescue alone");
    }

    static void RevivalLiftsTheBody()
    {
        var s=Finale(2);Kill(s,"b",6,-20);
        var band=s.State.Drops.Find(d=>d.ItemId=="wristband"&&d.OwnerId=="b");Place(s,"a",band.X,band.Z);
        Check(Act(s,"a","Pickup",band.Id).Accepted,"a picks up b's wristband");
        Place(s,"a",24,-20);Check(Act(s,"a","BeginRevival","b").Accepted,"a starts b's revival at medical");s.Tick(10.2);
        Check(s.Player("b").Life=="Alive"&&s.State.Bodies.Count==0,"the revived player's body is gone");
        Place(s,"a",0,-32);Check(!Act(s,"a","Extract").Accepted,"b, revived at medical, still has to walk home");
        Place(s,"b",1,-31);Check(Extracts(s),"then the crew can leave");
    }

    static void OneCarrierDragsTheFirstBody()
    {
        var s=Finale(2);Kill(s,"b",0,-22);var a=Place(s,"a",2.5f,-22);var body=Body(s,"b");
        Check(double.IsPositiveInfinity(s.CarrySpeed(a)),"empty hands: no carry limit");
        Check(!Act(s,"a","CarryBody","b").Accepted,"a body 2.5 m away is out of reach");
        Check(!Act(s,"a","CarryBody","nobody").Accepted,"only a body can be carried");
        Place(s,"a",1.5f,-22);
        Check(Act(s,"a","CarryBody","b").Accepted&&a.CarryBodyId=="b","a picks up the first body alone");
        Check(s.CarrySpeed(a)==1.5,"one carrier drags at 1.5 m/s");
        Check(!Stride(s,"a",.8f),"a lone carrier cannot hurry (1.6 m/s)");
        while(a.Z>-31)Check(Stride(s,"a",.75f),"a drags the body at 1.5 m/s");
        Check(body.X==a.X&&body.Z==a.Z,"the body comes along with its carrier");
        Check(Extracts(s),"with the body home the crew finishes Night 2");
    }

    static void LaterBodiesNeedTwoCarriers()
    {
        var s=Finale(5);Kill(s,"b",-3,-26);Kill(s,"c",0,-22);
        var a=Place(s,"a",1,-22);var d=Place(s,"d",-1,-22);var e=Place(s,"e",0,-21);var body=Body(s,"c");
        Check(s.State.Bodies.Count==2&&s.State.Bodies[0].PlayerId=="b","two deaths, two bodies: the earliest death is the first");
        Check(Act(s,"a","CarryBody","c").Accepted,"a takes hold of the second body");
        Check(Stride(s,"a",.7f)&&body.X==0&&body.Z==-22,"one carrier cannot move a later body");
        Check(Act(s,"d","CarryBody","c").Accepted,"d takes the other end");
        Check(!Act(s,"e","CarryBody","c").Accepted,"two carry a body at most");
        Check(s.CarrySpeed(a)==1.2&&s.CarrySpeed(d)==1.2,"a pair carries at 1.2 m/s");
        Check(!Stride(s,"d",.65f),"neither carrier can outpace the pair (1.3 m/s)");
        Check(Stride(s,"d",.6f)&&Same(body.Z,(a.Z+d.Z)/2)&&Same(body.X,0),"the body rides between its carriers");
        while(body.Z>-30)Check(Stride(s,"a",.6f)&&Stride(s,"d",.6f),"the pair walks it home");
        Check(!Act(s,"a","Extract").Accepted,"the first body is still out in the field");
        Place(s,"e",-3,-25);Check(Act(s,"e","CarryBody","b").Accepted,"e drags the first body alone");
        while(e.Z>-31)Check(Stride(s,"e",.75f),"e drags the first body home");
        Check(Extracts(s),"both bodies home: Night 2 is cleared");
    }

    static void CarriersStayWithinReach()
    {
        var s=Finale(3);Kill(s,"c",0,-22);var a=Place(s,"a",1,-22);Place(s,"b",-1,-22);var body=Body(s,"c");
        Check(Act(s,"a","CarryBody","c").Accepted&&Act(s,"b","CarryBody","c").Accepted,"a and b carry the first body together");
        Check(s.CarrySpeed(a)==1.2,"a pair moves at 1.2 m/s even with the first body");
        int strides=0;while(strides<20&&Stride(s,"a",.6f))strides++;
        Check(strides==5,"a carrier walking off alone is held within 2 m of the body ("+strides+" strides)");
        Check(Act(s,"b","DropBody").Accepted&&Stride(s,"a",.6f)&&body.Z==a.Z,"once b lets go, a drags the first body alone");
        s=Finale(3);Kill(s,"b",5,-20);Kill(s,"c",0,-22);Place(s,"a",1,-22);
        Check(Act(s,"a","CarryBody","c").Accepted,"a holds the second body alone");
        strides=0;while(strides<20&&Stride(s,"a",.7f))strides++;
        Check(strides==2,"a lone carrier of a later body cannot stray 2 m from it ("+strides+" strides)");
    }

    static void RevivingTheFirstBodyPromotesTheNext()
    {
        var s=Finale(3);Kill(s,"b",5,-20);Kill(s,"c",0,-22);var a=Place(s,"a",1,-22);var body=Body(s,"c");
        Check(Act(s,"a","CarryBody","c").Accepted&&Stride(s,"a",.7f)&&body.Z==-22,"c's body is second: a alone cannot move it");
        Check(Act(s,"a","DropBody").Accepted,"a puts it down");
        a.Wristbands.Add("b");Place(s,"a",24,-20);Check(Act(s,"a","BeginRevival","b").Accepted,"a revives b at medical");s.Tick(10.2);
        Check(Body(s,"b")==null&&s.State.Bodies.Count==1&&s.State.Bodies[0]==body,"b's revival leaves c's body as the first");
        Place(s,"a",1,-22);
        Check(Act(s,"a","CarryBody","c").Accepted&&Stride(s,"a",.7f)&&body.Z==a.Z,"now a alone can drag c's body");
    }

    // Nobody waits for a friend who left, so their body does not count as the first either: the crew's own earliest death does.
    static void AFriendWhoLeftDoesNotHoldTheFirstBody()
    {
        var s=Finale(3);Kill(s,"a",5,-15);s.Disconnect("a");Kill(s,"b",0,-22);var c=Place(s,"c",1,-22);var body=Body(s,"b");
        Check(s.State.Bodies.Count==2&&s.State.Bodies[0].PlayerId=="a","setup: a died first, then left the game");
        Check(Act(s,"c","CarryBody","b").Accepted&&Stride(s,"c",.7f)&&body.Z==c.Z,"c alone drags b's body: with a gone it is the crew's first");
        while(c.Z>-31)Check(Stride(s,"c",.75f),"c drags b's body home");
        Check(Act(s,"c","Extract").Accepted,"with b's body home, the lone survivor can finish Night 2");s.Tick(3.1);
        Check(s.State.Result=="Success","the crew clears Night 2 without waiting for a friend who left");
    }

    static void CarriersLetGo()
    {
        var s=Finale(4);Kill(s,"c",0,-22);var a=Place(s,"a",1,-22);var b=Place(s,"b",-1,-22);var d=Place(s,"d",0,-21);
        Check(!Act(s,"a","DropBody").Accepted,"nothing to put down");
        Check(Act(s,"a","CarryBody","c").Accepted&&Act(s,"a","DropBody").Accepted&&a.CarryBodyId=="","DropBody lets go");
        Check(Act(s,"a","CarryBody","c").Accepted&&Act(s,"a","CarryBody","c").Accepted&&a.CarryBodyId=="","CarryBody on the held body toggles it off");
        Check(Act(s,"a","CarryBody","c").Accepted,"a picks it up again");
        a.Life="Downed";a.DownedRemaining=30;s.Tick(.1);
        Check(a.CarryBodyId=="","a carrier who goes down drops the body");
        Check(Act(s,"b","Drag","a").Accepted&&!Act(s,"b","CarryBody","c").Accepted,"hands full: dragging a downed friend rules out carrying a body");
        Check(Act(s,"b","Drag","a").Accepted&&Act(s,"b","CarryBody","c").Accepted&&!Act(s,"b","Drag","a").Accepted,"and a carrier cannot start dragging");
        Check(Act(s,"d","CarryBody","c").Accepted,"d takes the other end");s.Disconnect("d");s.Tick(.1);
        Check(d.CarryBodyId==""&&s.CarrySpeed(b)==1.5,"a carrier who disconnects lets go");
    }

    static void SnapshotsKeepBodies()
    {
        var s=Finale(2);Kill(s,"b",5,-20);Place(s,"a",4,-20);
        Check(Act(s,"a","CarryBody","b").Accepted,"setup: a carries b");
        var json=JsonSerializer.Serialize(s.State,Json);var copy=new FestivalSimulation();copy.Restore(JsonSerializer.Deserialize<RoundState>(json,Json));
        Check(copy.State.Bodies.Count==1&&copy.State.Bodies[0].PlayerId=="b"&&copy.State.Bodies[0].X==5&&copy.Player("a").CarryBodyId=="b","bodies and carriers survive a snapshot");
        var old=JsonNode.Parse(json).AsObject();Check(old.ContainsKey("Bodies"),"Bodies is a serialised public field");
        old.Remove("Bodies");foreach(var p in old["Players"].AsArray())p.AsObject().Remove("CarryBodyId");
        copy=new FestivalSimulation();copy.Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),Json));
        Check(copy.State.Bodies.Count==0&&copy.Player("a").CarryBodyId=="","a snapshot from before bodies restores with none");
        old["Bodies"]=null;bool refused=false;
        try{new FestivalSimulation().Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),Json));}catch(ArgumentException){refused=true;}
        Check(refused,"a snapshot whose body list is null is refused");
    }
}
