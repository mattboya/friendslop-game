using System;
using System.Collections.Generic;
using Festival.Core;

// The stage-pass DJ set: a three-lane ride on the stage song's beat, played at the riser's main mixer, with a cheer for each phrase.
public static class DjSetTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("DjSet: "+message);}
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{TheSetPutsYouOnTheRiserAndItsEndPutsYouBack,TheRideRidesTheStageSongsBeatInThreeLanes,
            GreysComeFromNightOneAndLaterPhrasesGetBusier,CatchingEveryBlockAndDodgingEveryGreyScoresOne,StayingInOneLaneScoresAboutAThird,
            AGreyHitCostsATenthAndOnlyLaneChangesCount,AGoodPhraseDistractsTheCrowdAndEveryPhraseIsCheered,TheDjIsNotTrespassingOnStage})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" DJ set test(s) failed:\n"+string.Join("\n",failures));
    }
    static FestivalSimulation Floor(int festival=0,int level=0,int seed=7)
    {
        var game=new FestivalSimulation(seed);var p=game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");
        game.State.Phase="Playing";game.State.UnlockedFestivalCount=Festivals.Count;game.State.FestivalIndex=festival;game.State.LevelIndex=level;game.State.SimulationSeconds=10.123;
        game.State.Npcs.Clear();p.X=Catalog.StageTakeoverX;p.Z=Catalog.StageTakeoverZ;p.Yaw=37;p.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});
        return game;
    }
    static InteractionState Start(FestivalSimulation game)
    {
        var result=game.Execute("p0",new GameCommand{Id="dj-start"+(sequence++),Kind="Dj"});Check(result.Accepted,"setup: the set starts: "+result.Reason);
        return game.Interaction(game.Player("p0").InteractionId);
    }
    static void Lane(FestivalSimulation game,InteractionState i,int lane,double time)
    {
        game.State.SimulationSeconds=Math.Max(game.State.SimulationSeconds,i.StartSeconds+time);
        var result=game.Execute("p0",new GameCommand{Id="dj-lane"+(sequence++),Kind="Rhythm",Direction=lane,TimeSeconds=time});
        Check(result.Accepted,"a lane change at "+time+" is accepted: "+result.Reason);
    }
    static void Verdict(FestivalSimulation game,InteractionState i){game.State.SimulationSeconds=Math.Max(game.State.SimulationSeconds,i.StartSeconds+i.DurationSeconds+.75);game.Tick(.01);}
    // The inputs a perfect rider sends: into each block's lane as it arrives, and out of each grey's lane just before it, toward
    // the next block (a grey never shares a beat with a block, and the next block is always in another lane).
    static List<(int lane,double time)> Perfect(RhythmChart chart)
    {
        var inputs=new List<(int,double)>();int lane=DjRide.StartLane;
        for(int n=0;n<chart.Notes.Count;n++)
        {
            var note=chart.Notes[n];
            if(!note.Grey){if(note.Direction!=lane){lane=note.Direction;inputs.Add((lane,note.TimeSeconds));}continue;}
            var next=chart.Notes.FindIndex(n+1,x=>!x.Grey);Check(next>n,"a grey is followed by a block");
            int to=chart.Notes[next].Direction;Check(to!=note.Direction,"the block after a grey is in another lane");
            if(lane==note.Direction){lane=to;inputs.Add((lane,note.TimeSeconds-.05));}
        }
        return inputs;
    }

    static void TheSetPutsYouOnTheRiserAndItsEndPutsYouBack()
    {
        var game=Floor();var p=game.Player("p0");var i=Start(game);
        Check(p.X==Catalog.StageDjX&&p.Z==Catalog.StageDjZ,"the set puts you at the main mixer, got "+p.X+","+p.Z);
        Check(p.Yaw==180,"facing the crowd, got "+p.Yaw);
        Check(Catalog.StageDjZ>30&&Catalog.StageDjZ<31.5f,"the mixer spot is up on the riser, behind the stage lip");
        Check(!p.Inventory.Exists(x=>x.ItemId=="stage_pass"),"the pass is spent");
        for(int phrase=0;phrase<5;phrase++){Check(i.Phrase==phrase&&p.InteractionId==i.Id,"phrase "+phrase+" plays");Check(p.X==Catalog.StageDjX&&p.Z==Catalog.StageDjZ,"you stay on stage through phrase "+phrase);Verdict(game,i);}
        Check(i.Status=="Complete"&&p.InteractionId=="","the fifth verdict ends the set");
        Check(p.X==Catalog.StageTakeoverX&&p.Z==Catalog.StageTakeoverZ,"the set over, you are back on the lawn at the stage front, got "+p.X+","+p.Z);
        // Stopping early (held G) and being interrupted put you back too.
        game=Floor();p=game.Player("p0");i=Start(game);
        Check(game.Execute("p0",new GameCommand{Id="dj-stop"+(sequence++),Kind="Cancel"}).Accepted,"setup: the set stops");
        Check(i.Status!="Active"&&p.X==Catalog.StageTakeoverX&&p.Z==Catalog.StageTakeoverZ,"a stopped set puts you back on the lawn");
        game=Floor();p=game.Player("p0");i=Start(game);p.Connected=false;game.Tick(.01);
        Check(i.Status=="Interrupted"&&p.X==Catalog.StageTakeoverX&&p.Z==Catalog.StageTakeoverZ,"an interrupted set puts you back on the lawn");
    }

    static void TheRideRidesTheStageSongsBeatInThreeLanes()
    {
        foreach(int festival in new[]{0,1})for(int level=0;level<Festivals.LevelCount;level++)
        {
            var game=Floor(festival,level);double world=FestivalSimulation.WorldSeconds(game.State);var i=Start(game);
            int choice=RhythmChart.StageTrackFor(festival,level);
            Check(i.StageTrack==choice,"the set rides the stage's song, track "+choice+", got "+i.StageTrack);
            Check(Math.Abs(i.StartSeconds-(game.State.SimulationSeconds+2))<1e-9,"a 2 s countdown, as before");
            var track=StageBeats.For(i.StageTrack);
            for(int phrase=0;phrase<5;phrase++)
            {
                var chart=RhythmChart.For(i);double origin=i.StageFirstHeadWorldSeconds-i.StageFirstHeadDelaySeconds;
                Check(i.DurationSeconds==chart.DurationSeconds,"the phrase lasts its chart");
                Check(origin>=world-1e-9,"phrase "+phrase+" starts after the one before");
                Check(chart.Notes.Exists(n=>!n.Grey),"there are blocks");
                double first=RhythmChart.StageBeatAtWorld(track,i.StageFirstHeadWorldSeconds);
                for(int n=0;n<chart.Notes.Count;n++)
                {
                    var note=chart.Notes[n];
                    Check(note.Direction>=0&&note.Direction<DjRide.Lanes,"note "+n+" rides lane 0-2, got "+note.Direction);
                    Check(note.Id==n&&(n==0||note.TimeSeconds>=chart.Notes[n-1].TimeSeconds),"notes count up in time order");
                    Check(note.HoldSeconds==0,"no holds on the ride");
                    double half=(RhythmChart.StageBeatAtWorld(track,origin+note.TimeSeconds)-first)/(i.StageBeatStride/2);
                    Check(Math.Abs(half-Math.Round(half))<1e-6,track.Clip+" phrase "+phrase+" note "+n+" lands on the song's beat grid (half-beats for an off-beat), at "+half/2+" beats");
                }
                Check(Math.Abs(chart.Notes[0].TimeSeconds-i.StageFirstHeadDelaySeconds)<1e-9,"the first block arrives on the anchored beat");
                world=origin+i.DurationSeconds;Verdict(game,i);
            }
        }
    }

    static void GreysComeFromNightOneAndLaterPhrasesGetBusier()
    {
        for(int level=0;level<Festivals.LevelCount;level++)
        {
            int greys=0;var perPhrase=new double[5];
            for(int seed=0;seed<40;seed++)
            {
                var game=Floor(0,level,seed);var i=Start(game);
                for(int phrase=0;phrase<5;phrase++){var chart=RhythmChart.For(i);greys+=chart.Notes.FindAll(n=>n.Grey).Count;perPhrase[phrase]+=chart.Notes.FindAll(n=>!n.Grey).Count;Verdict(game,i);}
            }
            if(level==0)Check(greys==0,"Day 1 has no greys, got "+greys);
            else Check(greys>=40&&greys<=40*5*6,"level "+level+" has a few greys a phrase, got "+greys+" over 200 phrases");
            // Night 2 rides the busiest step from its first phrase.
            if(level<Festivals.LevelCount-1)Check(perPhrase[4]>perPhrase[0],"level "+level+": the last phrase has more blocks than the first ("+perPhrase[4]+" against "+perPhrase[0]+")");
            else Check(perPhrase[4]>=perPhrase[0]*.9,"Night 2's last phrase is as busy as its first");
        }
        double Busy(int level){double blocks=0;for(int seed=0;seed<40;seed++){var i=Start(Floor(0,level,seed));blocks+=RhythmChart.For(i).Notes.FindAll(n=>!n.Grey).Count;}return blocks;}
        Check(Busy(3)>Busy(0),"Night 2's first phrase is busier than Day 1's");
    }

    static void CatchingEveryBlockAndDodgingEveryGreyScoresOne()
    {
        int greys=0;
        foreach(int level in new[]{0,1,3})for(int seed=0;seed<20;seed++)
        {
            var game=Floor(seed%2,level,seed);var i=Start(game);
            for(int phrase=0;phrase<5;phrase++)
            {
                var chart=RhythmChart.For(i);greys+=chart.Notes.FindAll(n=>n.Grey).Count;
                foreach(var (lane,time) in Perfect(chart))Lane(game,i,lane,time);
                Verdict(game,i);
                Check(Math.Abs(i.Score-1)<1e-9,"level "+level+" seed "+seed+" phrase "+phrase+": catching every block and dodging every grey scores 1, got "+i.Score);
                Check(game.Player("p0").LastRhythmScore==i.Score,"the player's stored score is the verdict");
            }
        }
        Check(greys>0,"setup: the perfect rides dodged greys");
    }

    static void StayingInOneLaneScoresAboutAThird()
    {
        foreach(int stay in new[]{0,1,2})
        {
            double total=0;const int Seeds=60;
            for(int seed=0;seed<Seeds;seed++)
            {
                var game=Floor(0,0,seed);var i=Start(game);
                if(stay!=DjRide.StartLane)Lane(game,i,stay,0);
                Verdict(game,i);total+=i.Score;
            }
            Check(total/Seeds>.25&&total/Seeds<.42,"staying in lane "+stay+" on Day 1 scores about a third, got "+total/Seeds);
        }
    }

    static void AGreyHitCostsATenthAndOnlyLaneChangesCount()
    {
        // A hand-made phrase: blocks on beats 0-3 in lanes 0, 0, 2, 1, a grey on beat 0.5 in lane 2, all on a .5 s grid.
        var chart=new RhythmChart{DurationSeconds=4};
        void Note(double t,int lane,bool grey=false)=>chart.Notes.Add(new RhythmNote{Id=chart.Notes.Count,Direction=lane,TimeSeconds=t,Grey=grey});
        Note(2,0);Note(2.25,2,true);Note(2.5,0);Note(3,2);Note(3.5,1);
        List<RhythmInput> In(params (int lane,double t,bool release)[] changes){var list=new List<RhythmInput>();foreach(var c in changes)list.Add(new RhythmInput{Direction=c.lane,TimeSeconds=c.t,Release=c.release});return list;}
        Check(DjRide.Replay(chart,In(),.15).Score==.25,"no inputs: the middle lane catches the last block only");
        Check(DjRide.Replay(chart,In((0,2,false),(2,3,false),(1,3.5,false)),.15).Score==1,"every block caught on time, no grey hit");
        Check(Math.Abs(DjRide.Replay(chart,In((0,1.9,false),(2,2.1,false),(0,2.4,false),(2,3,false),(1,3.6,false)),.15).Score-(1-.1))<1e-9,"sitting in the grey's lane as it arrives costs a tenth; a block caught within the window still counts");
        Check(DjRide.Replay(chart,In((0,2.16,false),(2,3,false),(1,3.5,false)),.15).Score==.75,"a lane change too late for its block misses it");
        Check(DjRide.Replay(chart,In((3,1,false),(0,2,true),(0,2,false),(2,3,false),(1,3.5,false)),.15).Score==1,"a lane outside 0-2 and a release change nothing");
        Check(DjRide.Replay(chart,In((2,3,false),(0,2,false),(1,3.5,false)),.15).Score==1,"inputs replay in time order, whatever order they arrived in");
        var live=DjRide.Replay(chart,In((0,2,false)),.15,2.7);
        Check(live.Blocks==2&&live.Caught==2&&live.Combo==2&&live.GreysHit==0,"a live replay counts only the notes due so far, with the combo");
    }

    static void AGoodPhraseDistractsTheCrowdAndEveryPhraseIsCheered()
    {
        var game=Floor(0,1);var near=new NpcState{Id="near",Kind="Wook",X=8,Z=14,Yaw=-90};var far=new NpcState{Id="far",Kind="Wook",X=-20,Z=-30,Yaw=-90};
        var wary=new NpcState{Id="wary",Kind="Wook",X=-8,Z=14,Yaw=-90,Mode="Watching"};wary.Observers.Add(new ObserverState{PlayerId="p1",Suspicion=30});
        game.State.Npcs.Add(near);game.State.Npcs.Add(far);game.State.Npcs.Add(wary);
        Check(game.State.StageCheerScore==-1&&game.State.StageCheerUntilWorldSeconds==-1,"no cheer before any set");
        var i=Start(game);
        foreach(var (lane,time) in Perfect(RhythmChart.For(i)))Lane(game,i,lane,time);
        Verdict(game,i);double now=game.State.SimulationSeconds,world=FestivalSimulation.WorldSeconds(game.State);
        Check(i.Score==1,"setup: a perfect phrase");
        Check(near.DistractedUntil>now&&far.DistractedUntil>now,"a phrase at .75 or more distracts the crowd within 120 m, as before");
        Check(game.State.StageCheerScore==1&&game.State.StageCheerUntilWorldSeconds>world&&game.State.StageCheerUntilWorldSeconds<=world+10,"the crowd is told to cheer that phrase's score for a few seconds");
        double toStage=Math.Atan2(Catalog.StageDjX-near.X,Catalog.StageDjZ-near.Z)*180/Math.PI;
        Check(Math.Abs(near.Yaw-toStage)<.01,"a festivalgoer within 20 m of the stage turns to face it, got "+near.Yaw);
        Check(far.Yaw==-90,"one far off keeps their facing");
        Check(wary.Yaw==-90,"one watching someone keeps watching");
        game.Tick(.01);near.DistractedUntil=far.DistractedUntil=0;
        Verdict(game,i);
        Check(i.Phrase==2&&i.Score<.75,"setup: a phrase with no lane changes scores under .75, got "+i.Score);
        Check(near.DistractedUntil<=game.State.SimulationSeconds,"a weak phrase distracts nobody");
        Check(game.State.StageCheerScore==i.Score,"and the crowd reacts to that weak score");
    }

    static void TheDjIsNotTrespassingOnStage()
    {
        var game=Floor();var watcher=new NpcState{Id="watcher",Kind="Wook",X=0,Z=20,Yaw=0};game.State.Npcs.Add(watcher);
        var p=game.Player("p0");Start(game);
        for(int t=0;t<60;t++)game.Tick(.05);
        var seen=watcher.Observers.Find(o=>o.PlayerId=="p0");
        Check(seen!=null&&seen.LastSeenSeconds>0,"setup: the crowd sees the DJ on stage");
        Check(seen.Suspicion<5,"three seconds on stage as the DJ raise no stage-trespass suspicion, got "+seen.Suspicion);
        Check(game.Execute("p0",new GameCommand{Id="dj-off"+(sequence++),Kind="Cancel"}).Accepted,"setup: the set stops");
        p.X=Catalog.StageDjX;p.Z=Catalog.StageDjZ;var before=seen.Suspicion;for(int t=0;t<20;t++)game.Tick(.05);
        Check(seen.Suspicion>before+1,"anyone else up there is still trespassing");
    }
}
