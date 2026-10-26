using System;
using System.Collections.Generic;
using Festival.Core;
// ESC-1: wook suspicion builds faster later in the weekend; two cops walk a loop around the edge of the crowd.
public static class EscalationTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("Escalation: "+message);}
    static bool Same(double a,double b)=>Math.Abs(a-b)<1e-9;
    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var scenario in new Action[]{SuspicionBuildsFasterLaterInTheWeekend,OnlyGainsScale,CopsPatrolTheCrowdEdge,EvidenceStillComesFirst,CopsWaitForAChat,TwoCopsEveryLevel,
            AStopCallsYouOutAndHolds,AnIgnoredStopEndsInAnArrest,DitchingTheStockEndsTheStop,BoltingOutOfSightEndsTheStop,TalkingYourWayOut,TheWarningWaitsForATalk,SellingTheLastStockUnseenEndsTheStop,AnOldSnapshotsStopStartsAtLoad,StopsEndWhenDownedOrWithTheLevel,
            DetentionHoldsYouInsideTheSecurityBuilding,TheSecurityBuildingStandsClearOfTheGrounds,ADraggedFriendStaysOutsideTheSecurityBuilding,
            ADraggedFriendStaysInsideTheFence})
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

    // ---- Cop patrol ----
    static double Dist(double x,double z,double tx,double tz)=>Math.Sqrt((x-tx)*(x-tx)+(z-tz)*(z-tz));
    // The centre of the attendees actually standing in this round.
    static (double X,double Z) CrowdCentre(FestivalSimulation s){double x=0,z=0;var wooks=s.State.Npcs.FindAll(n=>n.Kind=="Wook");foreach(var n in wooks){x+=n.X;z+=n.Z;}return (x/wooks.Count,z/wooks.Count);}
    static int Sector(FestivalSimulation s,NpcState cop){var c=CrowdCentre(s);double bearing=Math.Atan2(cop.X-c.X,cop.Z-c.Z)*180/Math.PI;return (int)Math.Floor((bearing+360)%360/60);}
    static double ToSegment(double x,double z,double ax,double az,double bx,double bz){double dx=bx-ax,dz=bz-az,t=Math.Max(0,Math.Min(1,((x-ax)*dx+(z-az)*dz)/(dx*dx+dz*dz)));return Dist(x,z,ax+t*dx,az+t*dz);}
    static FestivalSimulation Level(int seed){var s=new FestivalSimulation(seed);s.AddPlayer("a","A");var a=s.Player("a");a.X=Festivals.CampGateX;a.Z=Festivals.CampGateZ;s.State.Phase="Playing";return s;}

    // With no evidence the two cops walk a loop around the edge of the crowd: 1.6 m/s, all the way round in 100 s, never inside
    // the 22 m where the dense crowd stands, on opposite sides of the loop, and across the camp gate's route to the lost friend
    // (each seed below puts the friend at a different spot).
    static void CopsPatrolTheCrowdEdge()
    {
        for(int seed=0;seed<3;seed++)
        {
            var s=Level(seed);var cops=s.State.Npcs.FindAll(n=>n.Kind=="Cop");var centre=CrowdCentre(s);var friend=s.State.FriendPosition;
            Check(cops.Count==2,"two cops ("+cops.Count+")");
            double nearest=double.MaxValue,farthest=0,apart=double.MaxValue,route=double.MaxValue;var travelled=new double[2];var sectors=new[]{new HashSet<int>(),new HashSet<int>()};
            for(int step=0;step<1000;step++)
            {
                var before=cops.ConvertAll(c=>(c.X,c.Z));s.Tick(.1);
                for(int i=0;i<2;i++)
                {
                    var c=cops[i];double moved=Dist(before[i].X,before[i].Z,c.X,c.Z);travelled[i]+=moved;
                    Check(moved<=1.6*.1+1e-4,c.Id+" walks no faster than 1.6 m/s (moved "+moved+" m in 0.1 s)");
                    Check(c.Mode=="Patrol"&&c.TargetId=="",c.Id+" is on patrol (mode "+c.Mode+")");
                    double out_=Dist(c.X,c.Z,centre.X,centre.Z);nearest=Math.Min(nearest,out_);farthest=Math.Max(farthest,out_);
                    route=Math.Min(route,ToSegment(c.X,c.Z,Festivals.CampGateX,Festivals.CampGateZ,friend.X,friend.Z));sectors[i].Add(Sector(s,c));
                }
                apart=Math.Min(apart,Dist(cops[0].X,cops[0].Z,cops[1].X,cops[1].Z));
            }
            string where="seed "+seed+": ";
            for(int i=0;i<2;i++)
            {
                Check(travelled[i]>=.95*1.6*100,where+cops[i].Id+" keeps walking at 1.6 m/s (walked "+travelled[i]+" m in 100 s)");
                Check(sectors[i].Count==6,where+cops[i].Id+" goes all the way round the crowd in 100 s (visited "+sectors[i].Count+" of 6 sixths)");
            }
            Check(nearest>=22,where+"the cops keep out of the dense crowd: nearest "+nearest+" m from its centre");
            Check(farthest<=27,where+"the cops keep to the crowd's edge, not the festival's: farthest "+farthest+" m from its centre");
            Check(apart>=40,where+"the two cops patrol opposite sides of the loop (closest "+apart+" m apart)");
            Check(route<=1,where+"the loop crosses the route from the camp gate to the lost friend at ("+friend.X+", "+friend.Z+") (closest "+route+" m)");
        }
    }

    static int commands;
    static GameCommand Command(string kind,string target="",string item="")=>new GameCommand{Id="escalation_"+(++commands),Kind=kind,TargetId=target,ItemId=item};

    // A patrolling cop who sees stock stops the player, witnesses the deal and detains 4 s later, exactly as before; then the cop
    // walks on, and back to the loop.
    static void EvidenceStillComesFirst()
    {
        var s=Level(1);s.State.Npcs.RemoveAll(n=>n.Kind=="Wook");var cop=s.State.Npcs.Find(n=>n.Id=="cop_0");
        s.Tick(1);Check(cop.Mode=="Patrol","a cop with no evidence patrols (mode "+cop.Mode+")");
        double heading=cop.Yaw*Math.PI/180;float Ahead(double m,bool x)=>(float)((x?cop.X:cop.Z)+m*(x?Math.Sin(heading):Math.Cos(heading)));
        var a=s.Player("a");a.X=Ahead(6,true);a.Z=Ahead(6,false);a.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});a.EquippedItemId="stock_lsd";
        var buyer=new NpcState{Id="buyer",X=Ahead(7,true),Z=Ahead(7,false),Yaw=(float)(cop.Yaw+180)};s.State.Npcs.Add(buyer);
        s.Tick(.1);Check(cop.Mode=="Stop"&&cop.TargetId=="a","the cop stops a player carrying stock in view (mode "+cop.Mode+")");
        s.Tick(2);Check(Dist(cop.X,cop.Z,a.X,a.Z)<=2.05,"the stop walks the cop over to the player ("+Dist(cop.X,cop.Z,a.X,a.Z)+" m away)");
        Check(s.Execute("a",Command("StartSale","buyer","stock_lsd")).Accepted,"a sells in front of the cop");
        s.Tick(.1);Check(cop.Mode=="ArrestWarning","the cop witnessed the deal (mode "+cop.Mode+")");
        s.Tick(4);Check(a.Life=="Detained","a witnessed deal still detains 4 s later ("+a.Life+")");
        float x=cop.X,z=cop.Z;s.Tick(1);
        Check(cop.Mode=="Patrol"&&cop.TargetId==""&&Math.Abs(Dist(x,z,cop.X,cop.Z)-1.6)<.01,"after the arrest the cop walks on at 1.6 m/s (mode "+cop.Mode+", moved "+Dist(x,z,cop.X,cop.Z)+" m)");
        // Pulled into the middle of the crowd, a cop walks back out to the loop.
        var c=CrowdCentre(s);cop.X=(float)c.X;cop.Z=(float)c.Z;s.Tick(20);
        Check(Dist(cop.X,cop.Z,c.X,c.Z)>=22,"a cop in the middle of the crowd walks back out to the loop ("+Dist(cop.X,cop.Z,c.X,c.Z)+" m from the crowd's centre after 20 s)");
    }

    // Security waits for a player who starts a chat instead of walking out of it, then walks on.
    static void CopsWaitForAChat()
    {
        var s=Level(1);var cop=s.State.Npcs.Find(n=>n.Id=="cop_0");var a=s.Player("a");a.X=cop.X+1;a.Z=cop.Z;
        Check(s.Execute("a",Command("Police",cop.Id)).Accepted,"a starts talking to security");
        var chat=s.Interaction(a.InteractionId);float x=cop.X,z=cop.Z;
        for(int guard=0;chat.Status=="Active"&&guard<200;guard++)s.Tick(.1);
        Check(chat.Status=="Complete","the chat runs to the end (status "+chat.Status+")");
        Check(cop.X==x&&cop.Z==z,"the cop stood still for it");
        s.Tick(1);Check(Dist(x,z,cop.X,cop.Z)>1.5,"then walks on ("+Dist(x,z,cop.X,cop.Z)+" m in 1 s)");
    }

    // Every level of both festivals, and the encore, puts exactly two cops on opposite sides of the loop.
    static void TwoCopsEveryLevel()
    {
        var s=new FestivalSimulation(7);s.AddPlayer("a","A");
        for(int level=0;level<=Festivals.Count*Festivals.LevelCount;level++)
        {
            string where=Festivals.Name(s.State.FestivalIndex)+" level "+s.State.LevelIndex+" tier "+s.State.EncoreTier;
            var cops=s.State.Npcs.FindAll(n=>n.Kind=="Cop");
            Check(cops.Count==2,where+": exactly two cops ("+cops.Count+")");
            Check(Dist(cops[0].X,cops[0].Z,cops[1].X,cops[1].Z)>=40,where+": the cops start on opposite sides of the loop ("+Dist(cops[0].X,cops[0].Z,cops[1].X,cops[1].Z)+" m apart)");
            s.State.Phase="Results";s.State.Result="Success";Check(s.Execute("a",Command("Reset")).Accepted,where+": the host brings the crew back");
        }
        Check(s.State.EncoreTier==1,"the walk reached the encore lap");
    }

    // ---- ESC-2: security stops ----
    // cop_0 alone at the origin looking north (+Z), the crowd and the other cop gone; a stands 5 m in front of it holding a
    // Tongue Stamp, so the first tick starts a stop. `sight` stands in for the host's walls: the cop sees a only while it
    // returns true.
    static FestivalSimulation Stopped(out NpcState cop,out PlayerState a,Func<bool> sight=null)
    {
        var s=Level(1);s.State.Npcs.RemoveAll(n=>n.Id!="cop_0");cop=s.State.Npcs[0];cop.X=0;cop.Z=0;cop.Yaw=0;
        if(sight!=null)s.HasLineOfSight=(x,z,tx,tz)=>sight();
        a=s.Player("a");a.X=0;a.Z=5;a.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});a.EquippedItemId="stock_lsd";
        s.Tick(.1);return s;
    }
    static int Count(PlayerState p,string item)=>p.Inventory.Find(i=>i.ItemId==item)?.Count??0;
    static bool Says(PlayerState p,string line)=>p.NpcSpeaker=="SECURITY"&&p.NpcSpeech==line;
    // Puts a `metres` straight ahead of the cop, in its view.
    static void InFront(NpcState cop,PlayerState a,double metres){double h=cop.Yaw*Math.PI/180;a.X=cop.X+(float)(metres*Math.Sin(h));a.Z=cop.Z+(float)(metres*Math.Cos(h));}
    // Ember Playa, a second into a dust storm (storms blow for 20 s or more).
    static void IntoADustStorm(FestivalSimulation s)
    {
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=Festivals.PlayaFestival;s.State.SpinSeed=12345;
        while(!FestivalSimulation.DustStorm(s.State))s.State.ElapsedSeconds+=1;
        s.State.ElapsedSeconds+=1;Check(FestivalSimulation.DustStorm(s.State),"setup: a dust storm blows");
    }
    // DIALOG-3: a talks to the guard, giving a reply of each grade as its turn comes; from the first Silence on, the turns are
    // left to run out on their own.
    static InteractionState Talk(FestivalSimulation s,PlayerState a,NpcState cop,params int[] grades)
    {
        var started=s.Execute("a",Command("Police",cop.Id));Check(started.Accepted,"setup: a talks to security (\""+started.Reason+"\")");
        var i=s.Interaction(a.InteractionId);
        foreach(int grade in grades){if(grade==ConversationLines.Silence)break;Reply(s,a,i,grade);}
        return i;
    }
    static void Reply(FestivalSimulation s,PlayerState a,InteractionState i,int grade){var t=i.Turns[i.Turns.Count-1];Check(s.Execute(a.Id,new GameCommand{Id="escalation_"+(++commands),Kind="Reply",Amount=Array.IndexOf(t.Grades,grade),Direction=i.Turns.Count-1}).Accepted,"setup: "+a.Id+" replies");}
    const int Smooth=ConversationLines.Smooth,SoSo=ConversationLines.SoSo,Awkward=ConversationLines.Awkward,Silence=ConversationLines.Silence;
    static void Finish(FestivalSimulation s,InteractionState i){for(int guard=0;i.Status=="Active"&&guard<300;guard++)s.Tick(.1);}

    // A cop who sees stock calls the player out and stops them, and the stop holds while they stay in sight with it.
    static void AStopCallsYouOutAndHolds()
    {
        var s=Stopped(out var cop,out var a);
        Check(cop.Mode=="Stop"&&cop.TargetId=="a","a cop who sees stock stops the player (mode "+cop.Mode+")");
        Check(Says(a,"HEY! What's in the bag?")&&a.NpcSpeechUntil>s.State.SimulationSeconds,"the guard calls it out (\""+a.NpcSpeaker+": "+a.NpcSpeech+"\")");
        s.Tick(11);
        Check(cop.Mode=="Stop"&&cop.TargetId=="a"&&Dist(cop.X,cop.Z,a.X,a.Z)<=2.05,"11 s on, still in sight with the stock, the stop holds with the cop beside them (mode "+cop.Mode+", "+Dist(cop.X,cop.Z,a.X,a.Z)+" m)");
    }

    // Ignored, a stop is warned at 12 s ("Last chance. Empty your pockets.") and becomes an arrest warning at 20 s; the usual
    // 4 s later the player is detained, and the cop walks on.
    static void AnIgnoredStopEndsInAnArrest()
    {
        var s=Stopped(out var cop,out var a);double began=s.State.SimulationSeconds;double Held()=>s.State.SimulationSeconds-began;
        s.Tick(11.85);Check(Says(a,"HEY! What's in the bag?"),"no warning before 12 s (\""+a.NpcSpeech+"\" at "+Held()+" s)");
        s.Tick(.3);Check(Says(a,"Last chance. Empty your pockets."),"at 12 s the guard warns them (\""+a.NpcSpeech+"\" at "+Held()+" s)");
        s.Tick(7.7);Check(cop.Mode=="Stop","the stop holds until 20 s (mode "+cop.Mode+" at "+Held()+" s)");
        s.Tick(.3);Check(cop.Mode=="ArrestWarning"&&cop.TargetId=="a","at 20 s, still in sight with the stock, it is an arrest warning (mode "+cop.Mode+" at "+Held()+" s)");
        s.Tick(3.75);Check(a.Life=="Alive","the usual 4 s run before the arrest ("+a.Life+" at "+Held()+" s)");
        s.Tick(.3);Check(a.Life=="Detained"&&Count(a,"stock_lsd")==0,"4 s after the warning they are detained and the stock taken ("+a.Life+" at "+Held()+" s)");
        s.Tick(1);Check(cop.Mode=="Patrol"&&cop.TargetId=="","then the cop walks on (mode "+cop.Mode+")");

        // Out of sight at 20 s (and not yet for the 6 s that loses the guard), the arrest waits for the guard to see them again.
        bool open=true;s=Stopped(out cop,out a,()=>open);began=s.State.SimulationSeconds;s.Tick(17);
        open=false;s.Tick(4);Check(cop.Mode=="Stop","out of the guard's sight at 21 s, no arrest yet (mode "+cop.Mode+" at "+Held()+" s)");
        open=true;s.Tick(.1);Check(cop.Mode=="ArrestWarning","back in its sight with the stock, an arrest warning at once (mode "+cop.Mode+" at "+Held()+" s)");
    }

    // Ditching: during a stop G drops stock first, whatever is in hand. With the last of it on the ground in the guard's sight
    // the stop ends ("Fine. Move along."). The stock lies there for anyone for 10 s, then the guard walks over and takes it;
    // picked back up and held out in their sight, it starts a fresh stop. Out of a stop, G drops what's in hand, as ever.
    static void DitchingTheStockEndsTheStop()
    {
        var s=Stopped(out var cop,out var a);
        a.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});a.EquippedItemId="poi_practice";
        var ditched=s.Execute("a",Command("Drop",item:"poi_practice"));
        Check(ditched.Accepted&&Count(a,"stock_lsd")==0&&Count(a,"poi_practice")==1,"G during a stop drops the stock, not the poi in hand (\""+ditched.Reason+"\")");
        var drop=s.State.Drops.Find(d=>d.ItemId=="stock_lsd");
        s.Tick(.1);double ditchedAt=s.State.SimulationSeconds;
        Check(cop.Mode=="Patrol"&&cop.TargetId==""&&cop.Evidence.Count==0,"with the last stock dropped in sight the stop ends within a tick (mode "+cop.Mode+", "+cop.Evidence.Count+" evidence)");
        Check(Says(a,"Fine. Move along."),"and the guard waves them on (\""+a.NpcSpeech+"\")");
        float x=cop.X,z=cop.Z;s.Tick(9.8);
        Check(drop!=null&&s.State.Drops.Contains(drop)&&cop.X==x&&cop.Z==z,"for 10 s the stock lies where it fell, the guard standing by (moved "+Dist(x,z,cop.X,cop.Z)+" m)");
        for(int guard=0;s.State.Drops.Contains(drop)&&guard<50;guard++)s.Tick(.1);
        Check(!s.State.Drops.Contains(drop)&&s.State.SimulationSeconds-ditchedAt>=10&&Dist(x,z,cop.X,cop.Z)>1&&Dist(cop.X,cop.Z,drop.X,drop.Z)<=2.5,"then the guard walks over and takes it ("+(s.State.SimulationSeconds-ditchedAt)+" s on, walked "+Dist(x,z,cop.X,cop.Z)+" m, "+Dist(cop.X,cop.Z,drop.X,drop.Z)+" m from it)");
        x=cop.X;z=cop.Z;s.Tick(1);Check(cop.Mode=="Patrol"&&Dist(x,z,cop.X,cop.Z)>1,"and walks on (mode "+cop.Mode+")");

        s=Stopped(out cop,out a);
        Check(s.Execute("a",Command("Drop",item:"stock_lsd")).Accepted,"setup: a ditches the stock");s.Tick(.1);drop=s.State.Drops.Find(d=>d.ItemId=="stock_lsd");
        Check(cop.Mode=="Patrol"&&s.Execute("a",Command("Pickup",drop.Id)).Accepted,"anyone can pick it back up before the guard does (mode "+cop.Mode+")");
        s.Tick(.1);Check(cop.Mode=="Patrol","picked up into a pocket, the stock stays concealed (mode "+cop.Mode+")");
        InFront(cop,a,3);Check(s.Execute("a",Command("Equip",item:"stock_lsd")).Accepted,"setup: a equips the picked-up stock in sight");
        s.Tick(.1);Check(cop.Mode=="Stop"&&Says(a,"HEY! What's in the bag?"),"and held out in the guard's sight, it starts a fresh stop (mode "+cop.Mode+")");

        s=Stopped(out cop,out a,()=>false);
        a.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});a.EquippedItemId="poi_practice";
        Check(cop.Mode=="Patrol"&&s.Execute("a",Command("Drop",item:"poi_practice")).Accepted&&Count(a,"poi_practice")==0&&Count(a,"stock_lsd")==1,"unstopped, G drops the poi in hand and keeps the stock (mode "+cop.Mode+")");
    }

    // Bolting: out of the guard's sight for 6 s, behind a wall or beyond their sight, and they give up; 5 s isn't enough. Within
    // sight range they follow at their 3 m/s; beyond it they stand. Sight is Sees': 12 m, or 5 m in a dust storm.
    static void BoltingOutOfSightEndsTheStop()
    {
        bool open=true;var s=Stopped(out var cop,out var a,()=>open);
        open=false;a.Z=cop.Z+10;float z=cop.Z;s.Tick(1);
        Check(Math.Abs(cop.Z-z-3)<.01,"behind a wall but within sight range the cop follows at 3 m/s ("+(cop.Z-z)+" m in 1 s)");
        s.Tick(4);Check(cop.Mode=="Stop","5 s out of sight isn't enough (mode "+cop.Mode+")");
        s.Tick(1.1);Check(cop.Mode=="Patrol"&&cop.TargetId==""&&cop.Evidence.Count==0,"6 s out of sight and the cop gives up (mode "+cop.Mode+")");

        s=Stopped(out cop,out a);a.Z=cop.Z+13;z=cop.Z;s.Tick(5);
        Check(cop.Mode=="Stop"&&cop.Z==z,"13 m off, beyond sight, the cop stands, and 5 s isn't enough (mode "+cop.Mode+", moved "+(cop.Z-z)+" m)");
        a.Z=cop.Z+11;s.Tick(.1);a.Z=cop.Z+13;s.Tick(5);Check(cop.Mode=="Stop","back in sight for a moment, the 6 s start over (mode "+cop.Mode+")");
        s.Tick(1.1);Check(cop.Mode=="Patrol"&&cop.Evidence.Count==0,"then 6 s beyond sight and the cop gives up (mode "+cop.Mode+")");

        foreach(bool storm in new[]{false,true})
        {
            s=Stopped(out cop,out a);if(storm)IntoADustStorm(s);a.Z=cop.Z+6;s.Tick(6.2);
            Check(storm?cop.Mode=="Patrol":cop.Mode=="Stop",storm?"in a dust storm the guard sees 5 m, so 6 s at 6 m loses them (mode "+cop.Mode+")":"in clear air 6 m is in sight, and the stop holds (mode "+cop.Mode+")");
        }
    }

    // Talking: a talk scoring .75 clears the stop, and the guard lets them be until it has lost sight of them. One scoring .5
    // keeps it with its clock running. The guard hears a talk out: past 20 s mid-talk, the arrest warning waits for its end.
    static void TalkingYourWayOut()
    {
        bool open=true;var s=Stopped(out var cop,out var a,()=>open);s.Tick(1);
        var talk=Talk(s,a,cop,Smooth,Smooth,SoSo,SoSo);Finish(s,talk);
        Check(talk.Status=="Complete"&&talk.Score>=.75&&talk.Score<.85,"setup: the talk scores .75 or a little more (got "+talk.Score+")");
        for(int tick=0;tick<20;tick++){InFront(cop,a,3);s.Tick(.1);}
        Check(cop.Mode=="Patrol"&&cop.TargetId==""&&!cop.Evidence.Exists(e=>e.Kind=="VisibleStock"),"a talk scoring .75 clears the stop: 2 s on, stock right in front of the guard, it lets them be (mode "+cop.Mode+")");
        open=false;s.Tick(.1);open=true;InFront(cop,a,3);s.Tick(.1);
        Check(cop.Mode=="Stop","once the guard has lost sight of them, stock in view starts a fresh stop (mode "+cop.Mode+")");

        s=Stopped(out cop,out a);double began=s.State.SimulationSeconds;s.Tick(1);
        talk=Talk(s,a,cop,Smooth,SoSo,Awkward,SoSo);Finish(s,talk);
        Check(talk.Status=="Complete"&&talk.Score>=.5&&talk.Score<.75,"setup: the talk scores .5 or a little more, under .75 (got "+talk.Score+")");
        s.Tick(.1);Check(cop.Mode=="Stop","a talk scoring .5 keeps the stop (mode "+cop.Mode+")");
        s.Tick(19.85-(s.State.SimulationSeconds-began));Check(cop.Mode=="Stop","its clock runs on: a stop until 20 s (mode "+cop.Mode+")");
        s.Tick(.3);Check(cop.Mode=="ArrestWarning","and an arrest warning at 20 s (mode "+cop.Mode+")");

        s=Stopped(out cop,out a);began=s.State.SimulationSeconds;s.Tick(17);
        talk=Talk(s,a,cop,Silence);bool warned=false,free=true;
        for(int guard=0;talk.Status=="Active"&&guard<300;guard++){s.Tick(.1);warned|=cop.Mode=="ArrestWarning";free&=a.Life=="Alive";}
        Check(s.State.SimulationSeconds-began>20.5&&!warned,"a talk running past 20 s holds the arrest warning (talk over at "+(s.State.SimulationSeconds-began)+" s, warned "+warned+")");
        // Four timed turns of silence run the talk from 17 s to 33 s, past the 20 s warning and the 4 s after it, and still
        // nobody is detained mid-talk.
        Check(free&&talk.Turns.Count==FestivalSimulation.ConversationTurns&&s.State.SimulationSeconds-began>=17+FestivalSimulation.ConversationTurns*FestivalSimulation.ReplySeconds-.2,"four silent turns run the talk to "+(s.State.SimulationSeconds-began)+" s with no arrest mid-talk ("+a.Life+", "+talk.Turns.Count+" turns)");
        s.Tick(.1);Check(cop.Mode=="ArrestWarning","then the poor talk is over and it comes (mode "+cop.Mode+")");
    }

    // K2: the guard hears a talk out, its 12 s "Last chance" too. A talk running past 12 s that waves them on never says it, and
    // what the guard said stands no longer: the stop is over. A poor talk ending between 12 and 20 s says it then, the stop still
    // on; one ending past 20 s goes straight to the arrest warning, too late for a last chance. A stop that ends unseen (bolted,
    // or ditched out of the guard's sight) also takes back what the guard said.
    const string LastChance="Last chance. Empty your pockets.";
    static void TheWarningWaitsForATalk()
    {
        var s=Stopped(out var cop,out var a);double began=s.State.SimulationSeconds;double Held()=>s.State.SimulationSeconds-began;
        s.Tick(8);var talk=Talk(s,a,cop);s.Tick(2);Reply(s,a,talk,Smooth);s.Tick(2.5);
        Check(Held()>12.4&&talk.Status=="Active"&&!Says(a,LastChance),"mid-talk at "+Held()+" s the guard doesn't warn (\""+a.NpcSpeech+"\")");
        Reply(s,a,talk,Smooth);Reply(s,a,talk,Smooth);Reply(s,a,talk,Smooth);s.Tick(.1);
        Check(talk.Score>=.75&&cop.Evidence.Exists(e=>e.Kind=="WavedOn"),"setup: the talk waves them on (score "+talk.Score+")");
        Check(a.NpcSpeech=="","waved on, nothing the guard said stands (\""+a.NpcSpeaker+": "+a.NpcSpeech+"\")");

        s=Stopped(out cop,out a);began=s.State.SimulationSeconds;
        s.Tick(8);talk=Talk(s,a,cop);s.Tick(2);Reply(s,a,talk,Smooth);s.Tick(2.5);Reply(s,a,talk,Awkward);Reply(s,a,talk,Awkward);Reply(s,a,talk,Awkward);
        Check(talk.Status=="Complete"&&talk.Score<.75,"setup: a poor talk (score "+talk.Score+")");
        Check(Says(a,LastChance)&&a.NpcSpeechUntil>s.State.SimulationSeconds,"a poor talk over at "+Held()+" s, past 12 s: the guard warns them as it ends (\""+a.NpcSpeech+"\")");
        s.Tick(.1);Check(cop.Mode=="Stop","and the stop holds (mode "+cop.Mode+")");

        s=Stopped(out cop,out a);began=s.State.SimulationSeconds;
        s.Tick(11);talk=Talk(s,a,cop,Silence);Finish(s,talk);
        Check(Held()>20&&!Says(a,LastChance),"a talk from 11 s left to run out past 20 s ("+Held()+" s) never warns (\""+a.NpcSpeech+"\")");
        s.Tick(.1);Check(cop.Mode=="ArrestWarning","it ends in the arrest warning (mode "+cop.Mode+")");

        bool open=true;s=Stopped(out cop,out a,()=>open);s.Tick(12.2);
        Check(Says(a,LastChance),"setup: ignored for 12 s, the guard warns them");
        open=false;s.Tick(6.2);
        Check(cop.Mode=="Patrol"&&a.NpcSpeech=="","bolted out of sight, the stop is over and the warning taken back (mode "+cop.Mode+", \""+a.NpcSpeech+"\")");
        open=true;s=Stopped(out cop,out a,()=>open);open=false;
        Check(s.Execute("a",Command("Drop",item:"stock_lsd")).Accepted,"setup: a ditches the stock out of the guard's sight");s.Tick(.1);
        Check(cop.Mode=="Patrol"&&a.NpcSpeech=="","ditched unseen, the stop is over and its call-out taken back (mode "+cop.Mode+", \""+a.NpcSpeech+"\")");
    }

    // A player who sells their last stock out of the guard's sight is let go, and walking past the guard later starts nothing.
    static void SellingTheLastStockUnseenEndsTheStop()
    {
        bool open=true;var s=Stopped(out var cop,out var a,()=>open);
        s.State.Npcs.Add(new NpcState{Id="buyer",X=a.X+1,Z=a.Z,Yaw=-90});
        open=false;var sold=s.Execute("a",Command("StartSale","buyer","stock_lsd"));Check(sold.Accepted,"setup: a sells out of the guard's sight (\""+sold.Reason+"\")");
        var sale=s.Interaction(a.InteractionId);
        s.Tick(.1);Check(cop.Mode=="Stop"&&cop.Evidence.Exists(e=>e.Kind=="VisibleStock"),"the last unit stays out as a sale reservation, so the unseen stop still holds (mode "+cop.Mode+")");
        s.Tick(6.2);Check(cop.Mode=="Patrol"&&cop.Evidence.Count==0&&!Says(a,"Fine. Move along."),"six seconds out of sight ends the stop unremarked, even while the sale holds the last unit (mode "+cop.Mode+", "+cop.Evidence.Count+" evidence, \""+a.NpcSpeech+"\")");
        for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)Reply(s,a,sale,Smooth);
        Finish(s,sale);Check(sale.Status=="Complete"&&sale.Score>=.4&&Count(a,"stock_lsd")==0,"setup: the sale goes through (score "+sale.Score+")");
        open=true;InFront(cop,a,3);s.Tick(.1);
        Check(cop.Mode=="Patrol"&&cop.Evidence.Count==0,"walking past the guard with nothing on them starts nothing (mode "+cop.Mode+")");
    }

    // A stop saved before stops kept a start time is timed from the load: no arrest at once, an arrest warning 20 s on.
    static void AnOldSnapshotsStopStartsAtLoad()
    {
        var saved=Level(1);saved.State.Npcs.RemoveAll(n=>n.Id!="cop_0");var cop=saved.State.Npcs[0];cop.X=0;cop.Z=0;cop.Yaw=0;cop.Mode="Stop";cop.TargetId="a";
        var a=saved.Player("a");a.X=0;a.Z=2;a.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});a.EquippedItemId="stock_lsd";saved.State.SimulationSeconds=500;
        cop.Evidence.Add(new EvidenceState{PlayerId="a",Kind="VisibleStock"});
        var s=new FestivalSimulation();s.Restore(saved.State);
        s.Tick(19.8);Check(cop.Mode=="Stop","19.8 s after the load the old stop holds (mode "+cop.Mode+")");
        s.Tick(.3);Check(cop.Mode=="ArrestWarning","20 s after the load it is an arrest warning (mode "+cop.Mode+")");
    }

    // Being downed ends a stop: back on their feet with the stock, the player gets a fresh one. The level's end ends it too.
    static void StopsEndWhenDownedOrWithTheLevel()
    {
        var s=Stopped(out var cop,out var a);s.Tick(15);
        a.Life="Downed";a.DownedRemaining=35;s.Tick(.1);
        Check(cop.Mode=="Patrol"&&cop.Evidence.Count==0,"a downed player's stop ends (mode "+cop.Mode+", "+cop.Evidence.Count+" evidence)");
        a.Life="Alive";InFront(cop,a,3);s.Tick(.1);s.Tick(6);
        Check(cop.Mode=="Stop","back on their feet in sight with the stock, a fresh stop: 21 s after the first began it is no arrest (mode "+cop.Mode+")");
        s.State.ElapsedSeconds=s.State.DurationSeconds;s.Tick(.1);
        Check(s.State.Phase=="Results"&&cop.Mode=="Patrol"&&cop.TargetId==""&&cop.Evidence.Count==0,"when the level ends so does the stop (phase "+s.State.Phase+", mode "+cop.Mode+")");
    }

    // The security building's outer wall faces, written out so a moved wall fails here. The pay desk (27,5) and the release
    // spot (24,2) stay outside.
    const float West=24,Front=5.5f,East=33,Back=12.2f;
    static bool InsideTheWalls(PlayerState p,float clear)=>p.X>West+clear&&p.X<East-clear&&p.Z>Front+clear&&p.Z<Back-clear;
    // A bust puts you inside the building, clear of its walls, and the host keeps you there: a step across the front, a side or
    // the back is refused, a step about the room is taken. A paid release, a free one and the four-step escape all let you out
    // at 24,2, alive. Nobody can put a stash box against the building, as nobody could at the old front.
    static void DetentionHoldsYouInsideTheSecurityBuilding()
    {
        var s=Stopped(out var cop,out var a);s.Tick(24.3);Check(a.Life=="Detained","setup: an ignored stop ends in an arrest ("+a.Life+")");
        Check(InsideTheWalls(a,.5f),"a detained player stands inside the security building, clear of its walls, not at the pay desk ("+a.X+", "+a.Z+")");
        bool Step(float fromX,float fromZ,float toX,float toZ){a.X=fromX;a.Z=fromZ;return s.TryMove("a",toX,toZ,0,.1);}
        float midX=(West+East)/2,midZ=(Front+Back)/2;
        Check(Step(midX,midZ,midX+.3f,midZ),"a step about the room is taken");
        Check(Step(midX,midZ,midX,midZ-.3f)&&a.Z==midZ-.3f,"a step toward the bars is taken");
        Check(!Step(midX,Front+.9f,midX,Front+.4f),"a step through the bars of the front is refused");
        Check(!Step(27,Front+.9f,27,Front+.4f),"so is one through the bars right by the pay desk");
        Check(!Step(West+.9f,midZ,West+.4f,midZ),"a step through the west wall is refused");
        Check(!Step(East-.9f,midZ,East-.4f,midZ),"a step through the east wall is refused");
        Check(!Step(midX,Back-.9f,midX,Back-.4f),"a step through the back wall is refused");
        Check(a.X==midX&&a.Z==Back-.9f,"a refused step leaves the player where they stood ("+a.X+", "+a.Z+")");
        // Released, a player walks free again, anywhere on the grounds.
        var r=new FestivalSimulation(1);r.AddPlayer("a","A");var friend=r.AddPlayer("b","B");r.State.Phase="Playing";
        a=r.Player("a");a.Life="Detained";a.X=midX;a.Z=midZ;friend.X=27;friend.Z=5;friend.Cash=20;
        var paid=new GameCommand{Id="escalation_paid",Kind="BeginRelease",TargetId="a",Amount=1};
        Check(r.Execute("b",paid).Accepted,"a friend at the pay desk pays the release, however far inside their friend stands");r.Tick(3.1);
        Check(a.Life=="Alive"&&a.X==24&&a.Z==2,"a paid release lets them out at 24,2, alive ("+a.Life+" at "+a.X+", "+a.Z+")");
        Check(r.TryMove("a",24.3f,2,0,.1),"out of the building they walk free");
        // The four-step escape lets them out at the same spot.
        s=Stopped(out cop,out a);s.Tick(24.3);Check(a.Life=="Detained","setup: arrested again ("+a.Life+")");
        for(int i=0;i<4;i++){a.HelpUntil=0;Check(s.Execute("a",Command("HelpSelf")).Accepted,"escape step "+(i+1)+" is taken");}
        Check(a.Life=="Alive"&&a.X==24&&a.Z==2,"the four-step escape lets them out at 24,2, alive ("+a.Life+" at "+a.X+", "+a.Z+")");
        // A stash box can't go against the building's east wall, any more than at the old front by the pay desk.
        var box=Level(1);var b=box.Player("a");b.Inventory.Add(new ItemStack{ItemId="stash_box",Count=3});
        b.X=East+.8f;b.Z=(Front+Back)/2;Check(!box.Execute("a",Command("Use","","stash_box")).Accepted,"a stash box against the east wall is refused");
        b.X=27;b.Z=4;Check(!box.Execute("a",Command("Use","","stash_box")).Accepted,"a stash box at the pay desk is refused");
        b.X=-10;b.Z=0;Check(box.Execute("a",Command("Use","","stash_box")).Accepted,"a stash box on open ground is placed");
    }
    // The bigger building takes no festivalgoer's place, Giggle Tank or friend's spot, and leaves the pay desk, the release spot,
    // the gathering tree beside it and the fence outside it.
    static void TheSecurityBuildingStandsClearOfTheGrounds()
    {
        bool Clear(float x,float z,float room)=>x<West-room||x>East+room||z<Front-room||z>Back+room;
        // A festivalgoer starts up to .21 m off their place and stands about .3 m wide.
        for(int i=0;i<FestivalCrowdLayout.Count;i++){var at=FestivalCrowdLayout.Spot(i);Check(Clear(at.X,at.Z,.6f),"festivalgoer "+i+"'s place ("+at.X+", "+at.Z+") stands clear of the security building");}
        foreach(var tank in Festivals.GiggleTankSpots)Check(Clear(tank.X,tank.Z,Festivals.GiggleTankReach+.2f),"the Giggle Tank spot ("+tank.X+", "+tank.Z+") keeps its reach clear of the building");
        for(int seed=0;seed<40;seed++){var f=new FestivalSimulation(seed).State.FriendPosition;Check(Clear(f.X,f.Z,1),"seed "+seed+"'s lost friend ("+f.X+", "+f.Z+") stands clear of the building");}
        Check(Clear(27,5,.4f)&&Clear(24,2,.4f),"the pay desk and the release spot stay outside");
        // The gathering tree's trunk is .76 m across, and the building's roof reaches .37 m past its back wall.
        Check(Clear(23,13.5f,.38f+.37f),"the gathering tree's trunk stands clear of the building and its roof");
        Check(East<39-2,"the building stands well inside the fence (x 39)");
    }
    // A dragged friend lies at the dragger's west side. Along the outside of the east wall they wait in front of the building,
    // never inside its closed walls (nobody but the detained gets out), and past its back they follow again.
    static void ADraggedFriendStaysOutsideTheSecurityBuilding()
    {
        var s=new FestivalSimulation(1);s.AddPlayer("a","A");s.AddPlayer("b","B");s.State.Phase="Playing";s.State.Npcs.Clear();
        var a=s.Player("a");var b=s.Player("b");b.Life="Downed";b.DownedRemaining=35;b.X=East+1.4f;b.Z=Front+1;a.X=East+1.4f;a.Z=Front-.5f;
        Check(s.Execute("a",Command("Drag","b")).Accepted,"setup: a drags their downed friend");
        // A body is .32 m wide (FestivalSession.Slide's capsule), so the dragger can walk as close as East+.35.
        void Walk(float toX,float toZ)
        {
            while(a.X!=toX||a.Z!=toZ)
            {
                float x=a.X+Math.Max(-.15f,Math.Min(.15f,toX-a.X)),z=a.Z+Math.Max(-.15f,Math.Min(.15f,toZ-a.Z));
                Check(s.TryMove("a",x,z,0,.1),"the dragger's step to "+x+", "+z+" is taken");
                if(Festivals.SecurityBuilding.Contains(b.X,b.Z,.32f))throw new Exception("Escalation: the dragged friend lands inside the security building's walls at "+b.X+", "+b.Z+" (dragger at "+a.X+", "+a.Z+")");
            }
        }
        Walk(East+.35f,a.Z);Walk(East+.35f,(Front+Back)/2);
        Check(b.Life=="Downed"&&a.DragTargetId=="b","the friend is still downed and dragged beside the east wall");
        Walk(East+.35f,Back+1.5f);
        Check(b.X==a.X-1&&b.Z==a.Z,"past the back the friend follows at the dragger's side again ("+b.X+", "+b.Z+")");
    }
    // ESC-5: a dragger walking along the west fence keeps the friend inside the grounds (|x| <= 39), and once rescued the friend
    // walks off east.
    static void ADraggedFriendStaysInsideTheFence()
    {
        var s=new FestivalSimulation(1);s.AddPlayer("a","A");s.AddPlayer("b","B");s.State.Phase="Playing";s.State.Npcs.Clear();
        var a=s.Player("a");var b=s.Player("b");b.Life="Downed";b.DownedRemaining=35;a.X=-38.4f;a.Z=0;b.X=-37.4f;b.Z=0;
        Check(s.Execute("a",Command("Drag","b")).Accepted,"setup: a drags their downed friend");
        for(float x=-38.4f;x>-38.68f;){x=Math.Max(-38.68f,x-2f/30);Check(s.TryMove("a",x,0,0,1.0/30),"the dragger's step to "+x+" is taken");}
        Check(Math.Abs(b.X)<=39&&Math.Abs(b.Z)<=39,"the dragged friend stays inside the fence ("+b.X+", "+b.Z+"; dragger at "+a.X+")");
        Check(s.Execute("a",Command("Drag","b")).Accepted,"setup: a lets go");
        Check(s.Execute("a",Command("Rescue","b")).Accepted,"setup: a rescues them");s.Tick(3.1);
        Check(b.Life=="Alive","setup: the friend is back on their feet ("+b.Life+")");
        Check(s.TryMove("b",b.X+4f/30,b.Z,0,1.0/30),"the rescued friend's step east is taken (from "+b.X+")");
    }
}
