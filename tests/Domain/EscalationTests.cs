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
        foreach(var scenario in new Action[]{SuspicionBuildsFasterLaterInTheWeekend,OnlyGainsScale,CopsPatrolTheCrowdEdge,EvidenceStillComesFirst,CopsWaitForAChat,TwoCopsEveryLevel})
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
        var a=s.Player("a");a.X=Ahead(6,true);a.Z=Ahead(6,false);a.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
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
}
