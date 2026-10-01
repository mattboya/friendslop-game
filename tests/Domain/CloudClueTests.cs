using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Festival.Core;

// TRIP-4: anyone at the festival can lie on their back and watch the clouds, and get up again; lying still is the only cost.
// Each day level has one clue cloud, up for 90 s from a seeded 60-240 s into the level. The tripper, and only the tripper, reads
// it by lying down while it is up and staying down for 3 s: it pictures a landmark, and a $15 cash stash waits 5-8 m from that
// landmark until someone walks up to it. There is no marker, and only the tripper's view ever holds the landmark. Nights have none.
public static class CloudClueTests
{
    const string Lie=FestivalSimulation.LieDownKind;
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("CloudClue: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="")=>s.Execute(id,new GameCommand{Id="cloud"+(sequence++),Kind=kind,TargetId=target});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{ADayLevelHasOneClueCloudForNinetySecondsAndANightHasNone,LyingDownStopsMovementAndGettingUpRestoresIt,
            OnlyAFreePlayerAtTheFestivalLiesDown,TheTripperReadsItAfterThreeSecondsDown,TheThreeSecondsCountFromWhenTheCloudComesUp,
            AFriendLyingDownNeverReadsIt,NoReadingWhileADustStormHidesIt,ReadingPlacesTheStashBesideTheLandmarkAndItPaysOnce,
            NothingWaitsUntilItIsRead,OnlyTheTripperIsShownTheLandmarkAndOnlyOnceRead,TheStashSpotsAreOffTheBeatenPath,
            SnapshotsKeepTheClueAndOlderOnesHaveNone})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" cloud clue test(s) failed:\n"+string.Join("\n",failures));
    }

    // A crew readies up at the trailhead and the wheels spin: the level is set up as they leave camp.
    static FestivalSimulation Leave(int seed,int festival=0,int level=0,int crew=2)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);Check(s.State.Phase=="Spinning","setup: the crew leaves camp");
        return s;
    }
    static FestivalSimulation Arrive(FestivalSimulation s){s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");Check(s.State.Phase=="Playing","setup: the crew is at the festival");return s;}
    static FestivalSimulation Day(int seed,int festival=0,int crew=2)=>Arrive(Leave(seed,festival,0,crew));
    // Runs the level clock on to `at` seconds in, or until the level ends.
    static void TickTo(FestivalSimulation s,double at){while(s.State.Phase=="Playing"&&s.State.ElapsedSeconds<at-1e-9)s.Tick(Math.Min(30,at-s.State.ElapsedSeconds));}
    // The tripper lies down at the first moment the cloud is up with no dust storm for the next 3.5 s, and stays down 3.2 s. False
    // when a storm outlasts the cloud. Calms last a minute or more, so a storm that starts and stops in those 3.5 s can't happen.
    static bool ReadIt(FestivalSimulation s)
    {
        var probe=new RoundState{FestivalIndex=s.State.FestivalIndex,SpinSeed=s.State.SpinSeed};
        bool Storm(double at){probe.ElapsedSeconds=at;return FestivalSimulation.DustStorm(probe);}
        TickTo(s,s.State.CloudClueStart+.5);while(s.State.Phase=="Playing"&&(Storm(s.State.ElapsedSeconds)||Storm(s.State.ElapsedSeconds+3.5)))s.Tick(1);
        if(!FestivalSimulation.CloudClueUp(s.State))return false;
        Check(Act(s,Tripper(s).Id,Lie).Accepted,"setup: the tripper lies down");s.Tick(3.2);Check(Read(s),"setup: the tripper reads the cloud");return true;
    }
    static PlayerState Tripper(FestivalSimulation s)=>s.Player(s.State.TripperId);
    static PlayerState Friend(FestivalSimulation s)=>s.State.Players.Find(p=>p.Id!=s.State.TripperId);
    static bool Read(FestivalSimulation s)=>s.State.CloudClueReadAt>=0;
    static double Distance(float x,float z,float xx,float zz){double a=x-xx,b=z-zz;return Math.Sqrt(a*a+b*b);}
    static string At(WorldPoint p)=>"("+p.X+", "+p.Z+")";

    // 600 levels across both festivals and the whole weekend. A day's window opens on a whole second 60-240 s in and stays up 90 s
    // (CloudClueUp), well before the day ends; its landmark is one of the festival's. Nights, and the camp before a level, have none.
    static void ADayLevelHasOneClueCloudForNinetySecondsAndANightHasNone()
    {
        Check(CloudShapes.ClueSeconds==90,"the clue cloud is up 90 s, got "+CloudShapes.ClueSeconds);
        var starts=new SortedSet<double>();var drawn=new HashSet<(int,int)>();
        for(int seed=1;seed<=600;seed++)
        {
            int festival=seed%Festivals.Count,level=seed/Festivals.Count%Festivals.LevelCount;
            var s=Leave(seed,festival,level,1);var st=s.State;string where="seed "+seed+" ("+Festivals.Name(festival)+" "+Festivals.For(st).Name+")";
            if(Festivals.For(st).Night)
            {
                st.Phase="Playing";
                Check(st.CloudClueStart<0&&st.CloudClueLandmark<0,where+": a night has no clue cloud, got "+st.CloudClueStart+" / "+st.CloudClueLandmark);
                for(st.ElapsedSeconds=0;st.ElapsedSeconds<st.DurationSeconds;st.ElapsedSeconds+=7)Check(!FestivalSimulation.CloudClueUp(st),where+": none is ever up");
                continue;
            }
            double start=st.CloudClueStart;starts.Add(start);
            Check(start>=60&&start<=240&&start==Math.Floor(start),where+": the window opens on a whole second 60-240 s in, got "+start);
            Check(start+90<=st.DurationSeconds,where+": and closes before the day ends");
            Check(st.CloudClueLandmark>=0&&st.CloudClueLandmark<CloudShapes.LandmarkCount(festival),where+": it pictures one of the festival's landmarks, got "+st.CloudClueLandmark);
            drawn.Add((festival,st.CloudClueLandmark));
            Check(st.CloudClueReadAt<0&&!st.CloudStashFound,where+": nobody has read it yet");
            st.Phase="Playing";st.ElapsedSeconds=start-.05;Check(!FestivalSimulation.CloudClueUp(st),where+": not up just before it opens");
            st.ElapsedSeconds=start;Check(FestivalSimulation.CloudClueUp(st),where+": up as it opens");
            st.ElapsedSeconds=start+89.95;Check(FestivalSimulation.CloudClueUp(st),where+": up 89.95 s on");
            st.ElapsedSeconds=start+90;Check(!FestivalSimulation.CloudClueUp(st),where+": gone 90 s on");
            st.ElapsedSeconds=start+45;st.Phase="Shopping";Check(!FestivalSimulation.CloudClueUp(st),where+": only while the level is playing");
        }
        Check(starts.Min<=75&&starts.Max>=225,"windows open all through 60-240 s, got "+starts.Min+"-"+starts.Max);
        Check(CloudShapes.LandmarkCount(Festivals.PoloFestival)==CloudShapes.Landmarks.Length&&CloudShapes.LandmarkCount(Festivals.PlayaFestival)==CloudShapes.Landmarks.Length-1&&CloudShapes.Landmarks[CloudShapes.Landmarks.Length-1].Name=="Ferris wheel",
            "the last landmark, the Ferris wheel, is pictured only on Palm Mirage, which has one");
        for(int festival=0;festival<Festivals.Count;festival++)for(int k=0;k<CloudShapes.LandmarkCount(festival);k++)Check(drawn.Contains((festival,k)),Festivals.Name(festival)+" pictures "+CloudShapes.Landmarks[k].Name+" on some day");
    }

    // Lying down stops your feet but not your eyes: steps are refused, looking round is not, and your body keeps facing where it
    // lay down. It lasts until you get up (Cancel) or the level ends. Neither lying nor getting up moves anyone's suspicion.
    static void LyingDownStopsMovementAndGettingUpRestoresIt()
    {
        // Nobody films them, so a minute in plain view draws no crowd.
        var s=Day(3);var p=Friend(s);var wook=s.State.Npcs.Find(n=>n.Kind=="Wook");foreach(var n in s.State.Npcs)n.Twist="";
        p.X=0;p.Z=0;p.Yaw=90;wook.X=0;wook.Z=-3;wook.Yaw=0;var o=wook.Observers.Find(x=>x.PlayerId==p.Id);
        if(o==null){o=new ObserverState{PlayerId=p.Id};wook.Observers.Add(o);}o.Suspicion=30;
        Check(s.TryMove(p.Id,.1f,0,90,.1),"setup: standing, they can walk");
        var lie=Act(s,p.Id,Lie);
        Check(lie.Accepted&&FestivalSimulation.LyingDown(s.State,p.Id),"they lie down, got \""+lie.Reason+"\"");
        var lying=s.State.Interactions.Find(i=>i.PlayerId==p.Id&&i.Status=="Active");
        Check(lying!=null&&lying.Kind==Lie&&p.InteractionId==lying.Id,"lying down is their interaction");
        Check(o.Suspicion==30,"lying down moves no suspicion, got "+o.Suspicion);
        // A step slow enough for the 1 m/s anyone busy with something may walk at.
        Check(!s.TryMove(p.Id,.2f,0,90,.1)&&p.X==.1f&&p.Z==0,"lying down, even a slow step is refused");
        Check(s.TryMove(p.Id,.1f,0,200,.1)&&p.X==.1f&&p.Yaw==90,"they can look round, but their body keeps facing where they lay down");
        s.Tick(60);Check(FestivalSimulation.LyingDown(s.State,p.Id)&&p.X==.1f,"a minute on they are still lying there");
        double before=o.Suspicion;var up=Act(s,p.Id,"Cancel");
        Check(up.Accepted&&!FestivalSimulation.LyingDown(s.State,p.Id)&&p.InteractionId=="","getting up ends it, got \""+up.Reason+"\"");
        Check(o.Suspicion==before,"getting up moves no suspicion either, got "+before+" -> "+o.Suspicion);
        Check(s.TryMove(p.Id,.4f,0,200,.1)&&p.X==.4f&&p.Yaw==200,"on their feet they walk and turn again");
        Check(Act(s,p.Id,Lie).Accepted,"setup: they lie down again");
        TickTo(s,s.State.DurationSeconds+1);Check(!FestivalSimulation.LyingDown(s.State,p.Id),"the level's end gets everyone up");
    }

    // The rule and the HUD's offer (CanLieDown) agree: only a living player at the festival who is doing nothing else, and has
    // let go of any friend or body, lies down.
    static void OnlyAFreePlayerAtTheFestivalLiesDown()
    {
        var s=Day(4);var p=Friend(s);var mate=Tripper(s);
        void Refused(string why){Check(!FestivalSimulation.CanLieDown(s.State,p),why+": not offered");var r=Act(s,p.Id,Lie);Check(!r.Accepted&&!FestivalSimulation.LyingDown(s.State,p.Id),why+": refused, got \""+r.Reason+"\"");}
        p.DragTargetId=mate.Id;Refused("dragging a friend");p.DragTargetId="";
        p.CarryBodyId=mate.Id;Refused("carrying a body");
        var carrying=Act(s,p.Id,Lie);Check(carrying.Reason=="Let go of your friend before you lie down","carrying, the refusal says why, got \""+carrying.Reason+"\"");p.CarryBodyId="";
        foreach(var life in new[]{"Downed","Detained","Spirit"}){p.Life=life;Refused("a player who is "+life);}p.Life="Alive";
        p.X=-28;p.Z=16;Check(Act(s,p.Id,"LostProperty").Accepted,"setup: busy with a lost-property task");
        Refused("busy with something else");Act(s,p.Id,"Cancel");
        Check(FestivalSimulation.CanLieDown(s.State,p)&&Act(s,p.Id,Lie).Accepted,"free again, they lie down");
        Check(!FestivalSimulation.CanLieDown(s.State,p)&&!Act(s,p.Id,Lie).Accepted&&s.State.Interactions.FindAll(i=>i.PlayerId==p.Id&&i.Status=="Active").Count==1,"already lying down, they can't lie down again");
        var camp=new FestivalSimulation(4);var c=camp.AddPlayer("c0","C0");
        Check(!FestivalSimulation.CanLieDown(camp.State,c)&&!Act(camp,c.Id,Lie).Accepted,"at camp, shopping, nobody lies down");
    }

    // The tripper lies down while the clue cloud is up: 2.9 s is not enough, and getting up starts the count again; 3 s reads it.
    static void TheTripperReadsItAfterThreeSecondsDown()
    {
        var s=Day(5);var t=Tripper(s);TickTo(s,s.State.CloudClueStart+20);
        Check(Act(s,t.Id,Lie).Accepted,"setup: the tripper lies down");
        s.Tick(2.9);Check(!Read(s)&&FestivalSimulation.CloudStashAt(s.State)==null,"2.9 s down reads nothing");
        Check(Act(s,t.Id,"Cancel").Accepted,"setup: they get up");s.Tick(1);Check(!Read(s),"nor does getting up");
        Check(Act(s,t.Id,Lie).Accepted,"setup: they lie down again");
        s.Tick(2.9);Check(!Read(s),"the count starts again: 2.9 s down reads nothing");
        s.Tick(.15);
        Check(Read(s),"3 s down reads it");
        Check(Math.Abs(s.State.CloudClueReadAt-(s.State.SimulationSeconds-.05))<.06,"read on the step that reached 3 s, got "+s.State.CloudClueReadAt+" at "+s.State.SimulationSeconds);
        Check(FestivalSimulation.CloudStashAt(s.State)!=null,"reading it puts the stash out");
    }

    // A tripper already lying as the cloud comes up reads it 3 s after it does, not as it does; one that has 3 s down only after
    // it has gone reads nothing.
    static void TheThreeSecondsCountFromWhenTheCloudComesUp()
    {
        var s=Day(6);var t=Tripper(s);double start=s.State.CloudClueStart;TickTo(s,start-10);
        Check(Act(s,t.Id,Lie).Accepted,"setup: the tripper lies down 10 s early");
        TickTo(s,start+2.9);Check(!Read(s),"2.9 s after it comes up: nothing yet, got read at "+s.State.CloudClueReadAt);
        TickTo(s,start+3.05);Check(Read(s),"3 s after it comes up: read");
        var late=Day(6);t=Tripper(late);TickTo(late,start+88);
        Check(Act(late,t.Id,Lie).Accepted,"setup: the tripper lies down 2 s before it goes");
        late.Tick(5);Check(!Read(late)&&FestivalSimulation.CloudStashAt(late.State)==null,"it goes before they have been down 3 s: nothing read");
        TickTo(late,late.State.DurationSeconds-1);Check(!Read(late),"and nothing is ever read after it has gone");
    }

    static void AFriendLyingDownNeverReadsIt()
    {
        var s=Day(7);var f=Friend(s);double start=s.State.CloudClueStart;TickTo(s,start-1);
        Check(Act(s,f.Id,Lie).Accepted,"setup: the tripper's friend lies down just before it comes up");
        TickTo(s,start+95);
        Check(!Read(s)&&FestivalSimulation.CloudStashAt(s.State)==null&&FestivalSimulation.VisibleCloudLandmark(s.State,f.Id)<0,"they watch it the whole time and read nothing");
    }

    // On Ember Playa a dust storm hides the sky: the tripper lying under it reads nothing while it blows. Once it blows over, the
    // cloud is back, and a tripper who has been down 3 s while it is up reads it.
    static void NoReadingWhileADustStormHidesIt()
    {
        for(int seed=1;seed<400;seed++)
        {
            var s=Leave(seed,Festivals.PlayaFestival);double start=s.State.CloudClueStart;
            var probe=new RoundState{FestivalIndex=Festivals.PlayaFestival,SpinSeed=s.State.SpinSeed};
            bool Storm(double at){probe.ElapsedSeconds=at;return FestivalSimulation.DustStorm(probe);}
            // A storm that starts at least 3 s after the cloud comes up, blows at least 4 s, and is over 5 s before the cloud goes.
            double from=-1;for(double at=start+3.5;at<start+80;at+=1)if(!Storm(at-1)&&Storm(at)&&Storm(at+4.5)){from=at;break;}
            if(from<0)continue;
            double until=from;while(Storm(until))until+=1;if(until>start+84)continue;
            Arrive(s);var t=Tripper(s);TickTo(s,from+.5);Check(FestivalSimulation.DustStorm(s.State),"setup: a dust storm blows at "+(from+.5));
            Check(Act(s,t.Id,Lie).Accepted,"setup: the tripper lies down in the storm");
            s.Tick(3.5);Check(FestivalSimulation.DustStorm(s.State),"setup: the storm is still blowing");
            Check(!Read(s)&&FestivalSimulation.CloudStashAt(s.State)==null,"seed "+seed+": nothing is read in a dust storm");
            TickTo(s,until+.5);Check(!FestivalSimulation.DustStorm(s.State)&&FestivalSimulation.CloudClueUp(s.State),"setup: the storm is over and the cloud still up");
            Check(Read(s),"seed "+seed+": once it blows over, the tripper, down all along, reads it");
            return;
        }
        throw new Exception("CloudClue: setup: no Ember Playa day from seed 1 to 399 has a storm inside its clue window");
    }

    // The stash waits at the read landmark's stash spot. Anyone living within 2.5 m of it, as with a vision's stash, banks $15 in
    // the crew's shared stash, once; a step further off, or a spirit, finds nothing.
    static void ReadingPlacesTheStashBesideTheLandmarkAndItPaysOnce()
    {
        int read=0;
        for(int seed=1;seed<=24;seed++)
        {
            var s=Day(seed,seed%Festivals.Count);var t=Tripper(s);var f=Friend(s);if(!ReadIt(s))continue;read++;
            var landmark=CloudShapes.Landmarks[s.State.CloudClueLandmark];var stash=FestivalSimulation.CloudStashAt(s.State);string where="seed "+seed+" ("+landmark.Name+")";
            Check(stash!=null&&stash.X==landmark.Stash.X&&stash.Z==landmark.Stash.Z,where+": the stash waits at "+At(landmark.Stash)+", got "+(stash==null?"none":At(stash)));
            int banked=s.State.StashCash,cash=f.Cash,sales=s.State.LevelSales;
            f.X=stash.X+2.6f;f.Z=stash.Z;s.Tick(.2);Check(s.State.StashCash==banked,where+": 2.6 m off finds nothing");
            f.Life="Spirit";f.X=stash.X;f.Z=stash.Z;s.Tick(.2);Check(s.State.StashCash==banked,where+": a spirit on the spot finds nothing");
            f.Life="Alive";f.X=stash.X+2.4f;s.Tick(.2);
            Check(s.State.StashCash==banked+15,where+": 2.4 m off banks $15 in the crew stash, got +"+(s.State.StashCash-banked));
            Check(f.Cash==cash&&s.State.LevelSales==sales,where+": camp money, not sale cash");
            Check(s.State.CloudStashFound&&FestivalSimulation.CloudStashAt(s.State)==null,where+": the stash is gone");
            f.X+=10;s.Tick(.2);f.X-=10;t.X=stash.X;t.Z=stash.Z;s.Tick(.5);Check(s.State.StashCash==banked+15,where+": it pays once");
        }
        Check(read>=20,"setup: most of 24 days are read in a calm moment, got "+read);
    }

    // Before the read there is nothing at any stash spot, and a window that goes unread leaves nothing behind.
    static void NothingWaitsUntilItIsRead()
    {
        var s=Day(9);var f=Friend(s);int banked=s.State.StashCash;double start=s.State.CloudClueStart;
        for(double at=0;at<start+120;at+=20)
        {
            TickTo(s,at);Check(FestivalSimulation.CloudStashAt(s.State)==null,"no stash "+at+" s in, before any read");
            foreach(var landmark in CloudShapes.Landmarks){f.X=landmark.Stash.X;f.Z=landmark.Stash.Z;s.Tick(.1);}
            Check(s.State.StashCash==banked,"standing on every stash spot "+at+" s in finds nothing");
        }
        Check(!FestivalSimulation.CloudClueUp(s.State)&&!Read(s),"setup: the window went unread");
        Check(Act(s,Tripper(s).Id,Lie).Accepted,"setup: the tripper lies down after it has gone");
        s.Tick(10);Check(!Read(s)&&FestivalSimulation.CloudStashAt(s.State)==null&&s.State.StashCash==banked,"nothing is left behind");
    }

    // The landmark leaves the host for the tripper alone, once they have read it; their friends, and a level without a tripper,
    // see an ordinary cloud.
    static void OnlyTheTripperIsShownTheLandmarkAndOnlyOnceRead()
    {
        var s=Day(10,crew:3);var t=Tripper(s);TickTo(s,s.State.CloudClueStart+5);
        foreach(var p in s.State.Players)Check(FestivalSimulation.VisibleCloudLandmark(s.State,p.Id)<0,"before the read nobody is shown it, "+p.Id+" is");
        Check(ReadIt(s),"setup: read");
        Check(FestivalSimulation.VisibleCloudLandmark(s.State,t.Id)==s.State.CloudClueLandmark,"the tripper is shown the landmark they read");
        foreach(var p in s.State.Players)if(p!=t)Check(FestivalSimulation.VisibleCloudLandmark(s.State,p.Id)<0,"their friend "+p.Id+" is not");
        Check(FestivalSimulation.VisibleCloudLandmark(s.State,"stranger")<0,"nor is anyone else");
        s.State.TripperId="";Check(FestivalSimulation.VisibleCloudLandmark(s.State,"")<0,"a level without a tripper shows nobody");
    }

    // Each stash spot is 5-8 m from its landmark, inside the fence, off the paths, outside the VIP ropes, well off the art cars'
    // loops, more than 2.5 m from anywhere players routinely stand, and a place of its own, apart from the Giggle Tanks' spots, so
    // reaching it is a choice. (EditMode's CloudClueWorldTests checks each has standing room among the walls.)
    static void TheStashSpotsAreOffTheBeatenPath()
    {
        Check(CloudShapes.Landmarks.Length==7,"seven landmarks: the stage, the night market, the medical tent, security, the shuttle, lost property and the Ferris wheel, got "+CloudShapes.Landmarks.Length);
        var named=new[]{"stage","night market","medical tent","security","shuttle","lost property","Ferris wheel"};
        Check(CloudShapes.Landmarks.Select(l=>l.Name).SequenceEqual(named),"named "+string.Join(", ",CloudShapes.Landmarks.Select(l=>l.Name)));
        var at=new[]{(0f,32f),(-18f,-22f),(24f,-20f),(27f,5f),(0f,-36f),(-28f,16f),(Festivals.WheelX,Festivals.WheelZ)};
        // The festival's walked paths (FestivalWorld), with a metre's margin: x from, x to, z from, z to.
        var paths=new[]{(-2.75f,2.75f,-32f,28f),(-23.5f,1.5f,-25f,-19f),(-1f,25f,-23f,-17f),(-29f,1f,2.5f,7.5f),(-1f,27f,-6.5f,-1.5f)};
        var stands=new List<(string Name,float X,float Z)>{("the crew stash",-25,-8),("the medical tent",24,-20),("security",27,5),("lost property",-28,16),
            ("the way back to camp",Festivals.CampGateX,Festivals.CampGateZ),("the DJ takeover",Catalog.StageTakeoverX,Catalog.StageTakeoverZ),
            ("the VIP stall",Festivals.VipStallX,Festivals.VipStallZ),("the VIP guard",Festivals.VipGuardPostX,Festivals.VipGuardPostZ),("the effigy",Festivals.EffigyX,Festivals.EffigyZ),
            ("the Ferris wheel's base",Festivals.WheelX,Festivals.WheelZ),
            // Visions.cs's secret spots, which take in SplitObjective.cs's lost friends' spots.
            ("a secret spot",16,-4),("a secret spot",-16,5),("a secret spot",-24,25),("a secret spot",25,24),("a secret spot",18,5)};
        for(int i=0;i<8;i++){var shelf=Catalog.ShopPoint(false,i);stands.Add(("a night-market shelf",shelf.X,shelf.Z));}
        // Festivalgoers start up to .3 m off their places.
        for(int i=0;i<FestivalCrowdLayout.Count;i++){var place=FestivalCrowdLayout.Get(i,0);stands.Add(("festivalgoer "+i+"'s place",place.X,place.Z));}
        for(int k=0;k<CloudShapes.Landmarks.Length;k++)
        {
            var landmark=CloudShapes.Landmarks[k];var spot=landmark.Stash;string where=landmark.Name+"'s stash "+At(spot);
            Check(landmark.At.X==at[k].Item1&&landmark.At.Z==at[k].Item2,landmark.Name+" stands at ("+at[k].Item1+", "+at[k].Item2+"), got "+At(landmark.At));
            double reach=Distance(spot.X,spot.Z,landmark.At.X,landmark.At.Z);
            Check(reach>=5&&reach<=8,where+" is 5-8 m from it, got "+reach.ToString("0.0"));
            Check(Math.Abs(spot.X)<=38&&Math.Abs(spot.Z)<=38,where+" is inside the fence");
            for(int f=0;f<Festivals.Count;f++)Check(!Festivals.InVipZone(f,spot.X,spot.Z),where+" is outside the VIP ropes");
            foreach(var (x0,x1,z0,z1) in paths)Check(spot.X<x0||spot.X>x1||spot.Z<z0||spot.Z>z1,where+" is off the path from ("+x0+", "+z0+") to ("+x1+", "+z1+")");
            for(double t=0;t<40;t+=.25)for(int c=0;c<Festivals.ArtCars;c++){var car=Festivals.ArtCarAt(c,t);Check(Distance(spot.X,spot.Z,car.X,car.Z)>5,where+" is well off art car "+c+"'s loop");}
            foreach(var stand in stands)Check(Distance(spot.X,spot.Z,stand.X,stand.Z)>(stand.Name.StartsWith("festivalgoer")?2.8:2.5),where+" is more than 2.5 m from "+stand.Name);
            foreach(var tank in Festivals.GiggleTankSpots)Check(Distance(spot.X,spot.Z,tank.X,tank.Z)>=8,where+" is well apart from the Giggle Tank's spot "+At(tank));
            foreach(var other in CloudShapes.Landmarks)Check(other==landmark||Distance(spot.X,spot.Z,other.Stash.X,other.Stash.Z)>=10,where+" is a place of its own, not beside "+other.Name+"'s");
        }
    }

    static void SnapshotsKeepTheClueAndOlderOnesHaveNone()
    {
        var s=Day(11);var t=Tripper(s);Check(ReadIt(s),"setup: read");
        var json=JsonSerializer.SerializeToNode(s.State,Json).AsObject();
        var saved=new FestivalSimulation();saved.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        Check(saved.State.CloudClueStart==s.State.CloudClueStart&&saved.State.CloudClueLandmark==s.State.CloudClueLandmark&&saved.State.CloudClueReadAt==s.State.CloudClueReadAt
            &&FestivalSimulation.CloudStashAt(saved.State)!=null&&FestivalSimulation.LyingDown(saved.State,t.Id),"a snapshot keeps the clue, its read, its stash and who is lying down");
        foreach(var field in new[]{"CloudClueStart","CloudClueLandmark","CloudClueReadAt","CloudStashFound"})json.Remove(field);
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        Check(old.State.CloudClueStart<0&&old.State.CloudClueLandmark<0&&old.State.CloudClueReadAt<0&&FestivalSimulation.CloudStashAt(old.State)==null,"a snapshot from before clue clouds has none");
        old.State.ElapsedSeconds=s.State.CloudClueStart+5;Check(!FestivalSimulation.CloudClueUp(old.State),"so none is ever up");
        int banked=old.State.StashCash;var p=old.Player(t.Id);Act(old,p.Id,"Cancel");
        foreach(var landmark in CloudShapes.Landmarks){p.X=landmark.Stash.X;p.Z=landmark.Stash.Z;old.Tick(.1);}
        Check(old.State.StashCash==banked,"and there is nothing to find");
    }
}
