using System;
using System.Collections.Generic;
using System.Text.Json;
using Festival.Core;

public static class StageMusicTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("StageMusic: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",int amount=0,string item="")=>s.Execute(id,new GameCommand{Id="stage"+(sequence++),Kind=kind,TargetId=target,Amount=amount,ItemId=item});

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{TwoStagesNeverShareASongAndEveryLoopComesUp,ThePickHoldsThroughTheLevelARejoinAndARestore,EachLevelShufflesAgain,
            EachPerformanceChartsToTheNearerStage,ADeckSetFollowsTheStageNearerWhereItStarts,TheTwoStagesStandWellApart})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" stage music test(s) failed:\n"+string.Join("\n",failures));
    }

    static FestivalSimulation Leave(int seed,int festival=0,int level=0,int crew=2)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);Check(s.State.Phase=="Spinning","setup: the crew leaves camp");
        return s;
    }
    static void Arrive(FestivalSimulation s){s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");Check(s.State.Phase=="Playing","setup: the crew is at the festival");}
    static (int main,int small) Pick(RoundState s)=>(StageMusic.Track(s,StageMusic.Main),StageMusic.Track(s,StageMusic.Small));

    static void TwoStagesNeverShareASongAndEveryLoopComesUp()
    {
        int tracks=StageBeats.Tracks.Length;Check(tracks==7,"setup: the seven measured loops, got "+tracks);
        var mains=new int[tracks+1];var smalls=new int[tracks+1];
        for(int seed=1;seed<=200;seed++)
        {
            var s=Leave(seed,seed%Festivals.Count,seed/Festivals.Count%Festivals.LevelCount);var (main,small)=Pick(s.State);
            string where="seed "+seed+" ("+Festivals.Name(s.State.FestivalIndex)+" "+Festivals.For(s.State).Name+")";
            Check(main>=1&&main<=tracks&&small>=1&&small<=tracks,where+" picks two of the seven loops, got "+main+" and "+small);
            Check(main!=small,where+" never plays one song on both stages, got "+main+" twice");
            // A view carries the spin seed and nothing secret (FestivalSession.ViewFor): a copy holding only it picks the same.
            var view=new RoundState{SpinSeed=s.State.SpinSeed};
            Check(Pick(view)==(main,small),where+": a client's view picks the same songs");
            mains[main]++;smalls[small]++;
        }
        for(int track=1;track<=tracks;track++)
        {
            Check(mains[track]>0,"loop "+track+" ("+StageBeats.For(track).Clip+") comes up on the main stage: "+string.Join(",",mains));
            Check(smalls[track]>0,"loop "+track+" ("+StageBeats.For(track).Clip+") comes up on the small stage: "+string.Join(",",smalls));
        }
    }

    static void ThePickHoldsThroughTheLevelARejoinAndARestore()
    {
        var s=Leave(17,1,1);var picked=Pick(s.State);Arrive(s);
        Check(Pick(s.State)==picked,"arriving at the festival keeps the songs picked at the spin");
        for(int k=0;k<30;k++)s.Tick(1);
        Check(Pick(s.State)==picked,"30 s into the level the songs are the same");
        s.Disconnect("p1");s.AddPlayer("p1","P1");
        Check(s.Player("p1").Connected&&Pick(s.State)==picked,"a friend who drops and rejoins hears the same songs");
        var restored=new FestivalSimulation(99);restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        Check(Pick(restored.State)==picked,"a restored snapshot keeps the songs");
    }

    // By chance 1 pair in 42 repeats, so a few of the 50 can keep their songs.
    static void EachLevelShufflesAgain()
    {
        int changed=0;
        for(int seed=1;seed<=50;seed++)
        {
            var s=Leave(seed,0,0);var first=Pick(s.State);
            s.State.Phase="Shopping";s.State.LevelIndex=1;s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;s.Tick(7);
            foreach(var p in s.State.Players){p.Ready=false;p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies for Night 1");}
            s.Tick(5.2);Check(s.State.Phase=="Spinning","setup: the crew leaves camp for Night 1");
            if(Pick(s.State)!=first)changed++;
        }
        Check(changed>=45,"a new level picks new songs: "+changed+" of 50 changed");
    }

    static FestivalSimulation Floor(float x,float z)
    {
        var s=new FestivalSimulation(4);s.AddPlayer("p0","P0");s.State.Phase="Playing";s.State.SpinSeed=12345;s.State.Npcs.Clear();
        s.State.Npcs.Add(new NpcState{Id="partner",X=x,Z=z+1.4f,Yaw=180,CanTalk=true});var p=s.Player("p0");p.X=x;p.Z=z;
        p.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
        s.State.TripperId="p0";s.State.Visions.Add(new VisionState{Id="vision",Kind="Buyer",NpcId="partner"});
        return s;
    }
    static void EachPerformanceChartsToTheNearerStage()
    {
        var spots=new[]{("the small stage's front",StageMusic.SmallX,StageMusic.SmallZ+3,StageMusic.Small),("the main stage's front",StageMusic.MainX,StageMusic.MainZ-3,StageMusic.Main),
            ("the camp gate",Festivals.CampGateX,Festivals.CampGateZ,StageMusic.Small),("the effigy",Festivals.EffigyX,Festivals.EffigyZ,StageMusic.Main)};
        foreach(var (where,x,z,stage) in spots)foreach(var kind in new[]{"Dance","ConfirmDance","Conversation","Poi"})
        {
            var s=Floor(x,z);if(kind=="Conversation")s.State.Npcs[0].Observers.Add(new ObserverState{PlayerId="p0",Suspicion=FestivalSimulation.TrustDanceSuspicion});
            var started=Act(s,"p0",kind,kind=="Poi"?"":"partner",item:kind=="Poi"?"poi_practice":"");Check(started.Accepted,"setup: "+kind+" at "+where+" starts: "+started.Reason);
            var i=s.Interaction(s.Player("p0").InteractionId);Check(kind!="Conversation"||i.TrustDance,"setup: the wary festivalgoer asks for a dance first");
            int expected=StageMusic.Track(s.State,stage);
            Check(StageMusic.Nearer(x,z)==stage,"setup: "+where+" is nearer the "+(stage==StageMusic.Small?"small":"main")+" stage");
            Check(i.StageTrack==expected,kind+" at "+where+" rides the "+(stage==StageMusic.Small?"small":"main")+" stage's song "+expected+", got "+i.StageTrack);
            Check(RhythmChart.ValidStageAnchor(i)&&i.DurationSeconds==RhythmChart.For(i).DurationSeconds,kind+" at "+where+" has a valid chart on that song's beats");
        }
    }

    static void ADeckSetFollowsTheStageNearerWhereItStarts()
    {
        // Art car 0 starts its loop at the south end of the west lawn, and 10 s on it is north of the halfway line.
        foreach(var (seconds,stage) in new[]{(0.0,StageMusic.Small),(10.0,StageMusic.Main)})
        {
            var s=new FestivalSimulation(3);s.AddPlayer("p0","P0");s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=Festivals.PlayaFestival;
            s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;s.State.Phase="Playing";s.State.SpinSeed=12345;s.State.ElapsedSeconds=seconds;s.State.Npcs.Clear();
            var at=Festivals.ArtCarAt(0,seconds);var p=s.Player("p0");p.X=at.X;p.Z=at.Z;string where="the art car "+seconds+" s into the level";
            Check(StageMusic.Nearer(p.X,p.Z)==stage,"setup: "+where+" is nearer the "+(stage==StageMusic.Small?"small":"main")+" stage");
            Check(Act(s,"p0","RideCar").Accepted&&Act(s,"p0","DeckSet").Accepted,"setup: a deck set starts on "+where);
            int expected=StageMusic.Track(s.State,stage),got=s.Interaction(p.InteractionId).StageTrack;
            Check(got==expected,"a deck set on "+where+" rides the "+(stage==StageMusic.Small?"small":"main")+" stage's song "+expected+", got "+got);
        }
    }

    // About 40 m or more, so the two songs do not mush together.
    static void TheTwoStagesStandWellApart()
    {
        double apart=Math.Sqrt(Math.Pow(StageMusic.MainX-StageMusic.SmallX,2)+Math.Pow(StageMusic.MainZ-StageMusic.SmallZ,2));
        Check(apart>=40,"the stages' fronts stand 40 m or more apart, got "+apart);
        Check(Math.Abs(StageMusic.SmallX)<=38&&Math.Abs(StageMusic.SmallZ)<=38,"the small stage is inside the fence");
        Check(StageMusic.Nearer((StageMusic.MainX+StageMusic.SmallX)/2,(StageMusic.MainZ+StageMusic.SmallZ)/2)==StageMusic.Main,"halfway between the fronts the main stage wins the tie");
    }
}
