using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// POLO-1: Palm Mirage (festival 0) has three twists, and Ember Playa has none of them. Influencers film along their phone's
// 8 m, 40 degree cone, and a player in frame puts every wook within 10 m of them on alert.
public static class PoloTwistTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("PoloTwist: "+message);}
    static bool Same(double a,double b)=>Math.Abs(a-b)<1e-9;
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="")=>s.Execute(id,new GameCommand{Id="polo"+(sequence++),Kind=kind,TargetId=target,ItemId=item});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{ThreeInfluencersFilmOnPalmMirageOnly,APlayerInFrameAlertsEveryWookNearby,OnlyTheFrameCounts,
            FramesAreAPassiveGain,TwistTagsSurviveASnapshot})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" polo twist test(s) failed:\n"+string.Join("\n",failures));
    }

    // Two friends ready up at camp, the wheels spin and the crew loads into the crowd.
    static FestivalSimulation Start(int seed,int level,int festival)
    {
        var s=new FestivalSimulation(seed);s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew is at "+Festivals.Name(festival));
        return s;
    }
    static List<NpcState> Tagged(FestivalSimulation s,string twist)=>s.State.Npcs.FindAll(n=>n.Twist==twist);

    // A hand-built Day 1 on Palm Mirage: an influencer at the origin filming north (+Z) and player p0 on camera 4 m in front
    // of them. A second wook stands 7 m from p0 with its back to them, one more stands 11 m away, and a cop is right beside
    // p0. Nobody is suspicious yet, and nobody is sprinting, so the only thing that can raise suspicion is the camera.
    static FestivalSimulation Filming(int crew=1,int level=0,int encore=0)
    {
        var s=new FestivalSimulation(3);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.LevelIndex=level;s.State.EncoreTier=encore;s.State.Phase="Playing";
        s.State.Npcs.Clear();
        s.State.Npcs.Add(new NpcState{Id="cam",Twist=FestivalSimulation.Influencer});
        s.State.Npcs.Add(new NpcState{Id="near",Z=-3,Yaw=180});
        s.State.Npcs.Add(new NpcState{Id="far",Z=15});
        s.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="Patrol",X=1,Z=4,Yaw=90});
        Place(s,0,0,4);
        // The rest of the crew, if any, bunches up just beside p0, on camera too.
        for(int k=1;k<crew;k++)Place(s,k,k%2==0?-1:1,4.5);
        return s;
    }
    static void Place(FestivalSimulation s,int k,double x,double z){var p=s.Player("p"+k);p.X=(float)x;p.Z=(float)z;}
    static NpcState Npc(FestivalSimulation s,string id)=>s.State.Npcs.Find(n=>n.Id==id);
    static double Heat(FestivalSimulation s,string npc,int k=0)=>Npc(s,npc).Observers.Find(o=>o.PlayerId=="p"+k)?.Suspicion??0;
    // What the near wook, who cannot see p0, gains on them in one second.
    static double Filmed(FestivalSimulation s){double before=Heat(s,"near");s.Tick(1);return Heat(s,"near")-before;}

    static void ThreeInfluencersFilmOnPalmMirageOnly()
    {
        var casts=new HashSet<string>();
        for(int seed=1;seed<=12;seed++)
        {
            var palm=Start(seed,seed%Festivals.LevelCount,0);var cast=Tagged(palm,FestivalSimulation.Influencer);
            Check(cast.Count==Festivals.Influencers&&Festivals.Influencers==3,"seed "+seed+": three festivalgoers film on Palm Mirage, got "+cast.Count);
            Check(cast.TrueForAll(n=>n.Kind=="Wook"),"seed "+seed+": influencers are festivalgoers, never cops");
            casts.Add(string.Join(",",cast.ConvertAll(n=>n.Id)));
            var playa=Start(seed,seed%Festivals.LevelCount,1);
            Check(playa.State.Npcs.TrueForAll(n=>n.Twist==""),"seed "+seed+": nobody films on Ember Playa");
        }
        Check(casts.Count>1,"different levels cast different influencers");
    }

    static void APlayerInFrameAlertsEveryWookNearby()
    {
        var s=Filming();double gained=Filmed(s);
        Check(Festivals.FilmSuspicionPerSecond>0&&Same(gained,Festivals.FilmSuspicionPerSecond),"a wook 7 m from p0 who can't see them gains "+Festivals.FilmSuspicionPerSecond+" a second while p0 is on camera, got "+gained);
        Check(Same(Heat(s,"cam"),Festivals.FilmSuspicionPerSecond),"so does the influencer, got "+Heat(s,"cam"));
        Check(Heat(s,"far")==0,"a wook 11 m away does not, got "+Heat(s,"far"));
        Check(Npc(s,"cop").Observers.Count==0,"and cops ignore the influencers");
        Check(Heat(s,"near")<25&&Npc(s,"near").Mode=="Blending","one second on camera is not enough to be watched");
        s.Tick(9);Check(Npc(s,"near").Mode=="Watching","ten seconds is ("+Heat(s,"near")+")");
    }

    static void OnlyTheFrameCounts()
    {
        // The influencer is always within 10 m of anyone they could film, so their own suspicion shows whether p0 was on camera.
        double Cam(Action<FestivalSimulation> arrange){var s=Filming();arrange(s);s.Tick(1);return Heat(s,"cam");}
        void Nothing(string why,Action<FestivalSimulation> arrange){double gained=Cam(arrange);Check(gained==0,why+": no suspicion, got "+gained);}
        void Something(string why,Action<FestivalSimulation> arrange){double gained=Cam(arrange);Check(Same(gained,Festivals.FilmSuspicionPerSecond),why+": on camera, got "+gained);}
        double Rad(double degrees)=>degrees*Math.PI/180;
        Something("7.9 m straight ahead",s=>Place(s,0,0,7.9));
        Nothing("8.1 m straight ahead is out of range",s=>Place(s,0,0,8.1));
        Something("5 m away, 19 degrees off the phone's axis",s=>Place(s,0,5*Math.Sin(Rad(19)),5*Math.Cos(Rad(19))));
        Nothing("5 m away, 21 degrees off the phone's axis",s=>Place(s,0,5*Math.Sin(Rad(21)),5*Math.Cos(Rad(21))));
        Nothing("behind the influencer",s=>Place(s,0,0,-2));
        Something("where the influencer has turned to",s=>{Place(s,0,4,0);Npc(s,"cam").Yaw=90;});
        Nothing("behind a wall",s=>s.HasLineOfSight=(x,z,xx,zz)=>false);
        Nothing("downed",s=>s.Player("p0").Life="Downed");
        Nothing("when nobody is an influencer",s=>Npc(s,"cam").Twist="");
    }

    // L4 Crowd: influencer frames are a passive gain, so they scale with the level's multiplier and the filmed player's pack,
    // the two together capped at x3.
    static void FramesAreAPassiveGain()
    {
        double alone=Filmed(Filming());Check(alone>0,"setup: a lone player on camera draws suspicion");
        double bunched=Filmed(Filming(crew:3));
        Check(Same(bunched,alone*1.35),"three bunched on camera: x1.35 (got x"+bunched/alone+")");
        double night=Filmed(Filming(level:3));
        Check(Same(night,alone*Festivals.Level(0,3,0).SuspicionMultiplier)&&night>alone,"Night 2 films hotter: x"+Festivals.Level(0,3,0).SuspicionMultiplier+" (got x"+night/alone+")");
        double capped=Filmed(Filming(crew:3,level:3,encore:5));
        Check(Same(capped,alone*3),"a bunched crew on an encore Night 2 hits the x3 cap (got x"+capped/alone+")");
        var spooned=Filming();spooned.Player("p0").Inventory.Add(new ItemStack{ItemId="little_spoon",Count=1});
        Check(Same(Filmed(spooned),alone*.9),"the little spoon softens it like any passive gain");
    }

    static void TwistTagsSurviveASnapshot()
    {
        var s=Start(7,0,0);var cast=string.Join(",",Tagged(s,FestivalSimulation.Influencer).ConvertAll(n=>n.Id));Check(cast!="","setup: someone is filming");
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        Check(string.Join(",",Tagged(restored,FestivalSimulation.Influencer).ConvertAll(n=>n.Id))==cast,"the influencers survive a snapshot");
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        foreach(var npc in legacy["Npcs"].AsArray())npc.AsObject().Remove("Twist");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.Npcs.TrueForAll(n=>n.Twist==""),"a snapshot from before twists restores with nobody filming");
        old.Tick(1);Check(old.State.Phase=="Playing","and plays on");
    }
}
