using System;
using Festival.Core;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

public static class StageRhythmPlanningTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("StageRhythmPlanning: "+message);}
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{PrototypeMapping,ProductionForUsesSerializedAnchor,CommandsShareOneStageInitializer,RealLoopJoinsAndStrideTies,LegacyAndMalformedSnapshots,DjPhrasesDoNotOverlapTheirVerdicts,ActiveStageAnchorSurvivesTheProductionPause,MeasuredPacingKeepsAllLogicalPatterns})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+": "+(error.InnerException??error).Message);}
        Check(failures.Count==0,string.Join("\n",failures));
    }
    static void PrototypeMapping()
    {
        int holds=0;
        foreach(int choice in new[]{1,2,4,5})foreach(double stride in new[]{.5,1.0,2.0})for(int seed=0;seed<30;seed++)
        {
            var track=StageBeats.For(choice);const double firstBeat=31,tuned=.5;
            var logical=RhythmChart.Create(seed,16,tuned,3);
            var mapped=RhythmChart.CreateOnStage(seed,16,tuned,3,track,firstBeat,stride);
            Check(mapped.Notes.Count==logical.Notes.Count,"mapping retains the tuned draw count");
            for(int index=0;index<logical.Notes.Count;index++)
            {
                var old=logical.Notes[index];var note=mapped.Notes[index];
                double startBeat=firstBeat+(old.TimeSeconds-2)/tuned*stride;
                double head=2+(track.Sample(startBeat)-track.Sample(firstBeat))/StageBeats.Rate;
                Check(Math.Abs(note.TimeSeconds-head)<.001,track.Clip+" head "+index+" must follow fractional grid across phrase drift; got "+note.TimeSeconds+", expected "+head);
                Check(note.Id==old.Id&&note.Direction==old.Direction,"mapping preserves ids and lanes");
                if(old.HoldSeconds>0)
                {
                    holds++;double endBeat=startBeat+old.HoldSeconds/tuned*stride;
                    double duration=(track.Sample(endBeat)-track.Sample(startBeat))/StageBeats.Rate;
                    Check(Math.Abs(note.HoldSeconds-duration)<.001,"hold tail lands on the same authored fractional grid");
                }
            }
            double end=2+(track.Sample(firstBeat+16*stride)-track.Sample(firstBeat))/StageBeats.Rate;
            Check(Math.Abs(mapped.DurationSeconds-end)<.001,"chart duration follows its mapped end, rather than count times tuned seconds");
        }
        Check(holds>0,"representative seeds exercise actual DANCE-10 hold tails");
    }

    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static string Number(double n)=>n.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    static object Field(InteractionState i,string name){var field=typeof(InteractionState).GetField(name);Check(field!=null,"serialized "+name+" is missing");return field.GetValue(i);}
    static double MathCall(string name,params object[] args){var method=typeof(RhythmChart).GetMethod(name);Check(method!=null,name+" is missing from the shared production chart");return (double)method.Invoke(null,args);}
    static InteractionState Anchored(int track,double anchor,double delay,double stride,int seed=17,int count=16,int difficulty=3)=>JsonSerializer.Deserialize<InteractionState>("{\"Kind\":\"Dance\",\"ChartSeed\":"+seed+",\"NoteCount\":"+count+",\"ChartDifficulty\":"+difficulty+",\"BeatSeconds\":0.5,\"StageTrack\":"+track+",\"StageFirstHeadWorldSeconds\":"+Number(anchor)+",\"StageFirstHeadDelaySeconds\":"+Number(delay)+",\"StageBeatStride\":"+Number(stride)+"}",Json);
    static void ProductionForUsesSerializedAnchor()
    {
        foreach(int choice in new[]{1,2,4,5})foreach(double stride in new[]{.5,1.0,2.0})
        {
            var track=StageBeats.For(choice);double anchor=(track.Sample(31)-track.FirstBeat)/StageBeats.Rate;
            var i=Anchored(choice,anchor,2.23,stride);var mapped=RhythmChart.For(i);var logical=RhythmChart.Create(i.ChartSeed,16,.5,3);
            Check(mapped.Notes.Count==logical.Notes.Count,"For retains all generated patterns");
            for(int n=0;n<logical.Notes.Count;n++)
            {
                var old=logical.Notes[n];var note=mapped.Notes[n];double beat=31+(old.TimeSeconds-2)/.5*stride;
                double expected=2.23+(track.Sample(beat)-track.Sample(31))/StageBeats.Rate;
                Check(Math.Abs(note.TimeSeconds-expected)<.001,"For maps "+track.Clip+" head "+n+" through the serialized first head");
                Check(note.Id==old.Id&&note.Direction==old.Direction,"For preserves logical lanes and IDs");
                Check(Math.Abs(note.HoldSeconds-(track.Sample(beat+old.HoldSeconds/.5*stride)-track.Sample(beat))/StageBeats.Rate)<.001,"For maps hold endpoints");
            }
            var round=new FestivalSimulation();round.AddPlayer("p0","P0");i.Id="grid";i.PlayerId="p0";round.State.Interactions.Add(i);
            var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(round.State,Json),Json));
            Check(JsonSerializer.Serialize(RhythmChart.For(restored.Interaction("grid")),Json)==JsonSerializer.Serialize(mapped,Json),"host and restored chart agree exactly");
        }
    }
    static FestivalSimulation Floor(int festival=0,int level=0,double at=10.123)
    {
        var game=new FestivalSimulation(7);var p=game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");
        game.State.Phase="Playing";game.State.UnlockedFestivalCount=Festivals.Count;game.State.FestivalIndex=festival;game.State.LevelIndex=level;game.State.SimulationSeconds=at;
        game.State.Npcs.Clear();p.X=0;p.Z=0;p.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
        game.State.Npcs.Add(new NpcState{Id="grid_partner",Kind="Wook",Role="Buyer",CanTalk=true,X=0,Z=1.5f,Yaw=180});
        game.State.TripperId=p.Id;game.State.Visions.Add(new VisionState{Id="grid_vision",NpcId="grid_partner",IsTrue=true});return game;
    }
    static InteractionState Start(FestivalSimulation game,string kind,string who="p0",string target="grid_partner")
    {
        var result=game.Execute(who,new GameCommand{Id=Guid.NewGuid().ToString("N"),Kind=kind,TargetId=target});Check(result.Accepted,"setup: "+kind+": "+result.Reason);return game.Interaction(game.Player(who).InteractionId);
    }
    static void CommandsShareOneStageInitializer()
    {
        foreach(int festival in new[]{0,1})foreach(int level in new[]{0,1,2,3})foreach(string kind in new[]{"Dance","Poi","ConfirmDance","Conversation"})
        {
            var game=Floor(festival,level);var partner=game.State.Npcs[0];if(kind=="Conversation")partner.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=40});
            double press=game.State.SimulationSeconds;var i=Start(game,kind);int choice=festival==0?(Festivals.Night(level)?1:2):(Festivals.Night(level)?4:5);
            Check((int)Field(i,"StageTrack")==choice,kind+" uses the stage's stable catalog choice");
            Check(i.StartSeconds==press+2,kind+" preserves its simulation countdown");
            double anchor=(double)Field(i,"StageFirstHeadWorldSeconds"),delay=(double)Field(i,"StageFirstHeadDelaySeconds");
            var chart=RhythmChart.For(i);Check(anchor>=FestivalSimulation.WorldSeconds(game.State)+4-1e-9,"first head follows preparation");
            Check(Math.Abs(chart.Notes[0].TimeSeconds-delay)<1e-9&&Math.Abs(anchor-(FestivalSimulation.WorldSeconds(game.State)+2+delay))<1e-9,"first head and countdown are separate coherent clocks");
            Check(i.DurationSeconds==chart.DurationSeconds,"duration is the actual mapped chart");
            double sample=StageBeats.For(choice).FirstBeat+(anchor%StageBeats.For(choice).LoopSeconds)*StageBeats.Rate,beat=StageBeats.For(choice).Beat(sample);
            Check(Math.Abs(beat-Math.Round(beat))<1e-7,"between-beat starts choose an authored whole beat");
            Check(i.StartSeconds+chart.Notes[0].TimeSeconds-press>=Catalog.FindEffect("ketamine").LeadSeconds,"longest arrow lead fits");
        }
        var pair=Floor();var a=Start(pair,"Poi");var b=pair.Player("p1");b.X=pair.Player("p0").X+1;b.Z=pair.Player("p0").Z;b.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
        pair.State.SimulationSeconds+=.3;var other=Start(pair,"Poi","p1","p0");
        Check(a.PartnerId=="p1"&&other.PartnerId=="p0","setup: paired poi");
        foreach(var field in new[]{"StageTrack","StageFirstHeadWorldSeconds","StageFirstHeadDelaySeconds","StageBeatStride"})Check(Field(a,field).Equals(Field(other,field)),"paired poi copies "+field);
        Check(JsonSerializer.Serialize(RhythmChart.For(a),Json)==JsonSerializer.Serialize(RhythmChart.For(other),Json),"paired poi has identical heads and tails");
    }
    static void RealLoopJoinsAndStrideTies()
    {
        foreach(int choice in new[]{1,2,4,5})
        {
            var t=StageBeats.For(choice);double endBeat=t.Beat(t.LoopEnd),firstBeat=t.Beat(t.FirstBeat),span=endBeat-firstBeat;
            double headBeat=Math.Floor(endBeat)-3,anchor=(t.Sample(headBeat)-t.FirstBeat)/StageBeats.Rate;
            var i=Anchored(choice,anchor,2.1,1,0);var chart=RhythmChart.For(i);var logical=RhythmChart.Create(0,16,.5,3);int crossingHolds=0;
            for(int n=0;n<chart.Notes.Count;n++)
            {
                double beat=headBeat+(logical.Notes[n].TimeSeconds-2)/.5,cycle=Math.Floor((beat-firstBeat)/span),local=beat-cycle*span;
                double expected=2.1+cycle*t.LoopSeconds+(t.Sample(local)-t.FirstBeat)/StageBeats.Rate-anchor;
                Check(Math.Abs(chart.Notes[n].TimeSeconds-expected)<.001,"head crosses exact sample loop join for "+t.Clip);
                if(logical.Notes[n].HoldSeconds>0&&beat<endBeat&&beat+logical.Notes[n].HoldSeconds/.5>endBeat)crossingHolds++;
                double tailBeat=beat+logical.Notes[n].HoldSeconds/.5,tailCycle=Math.Floor((tailBeat-firstBeat)/span),tailLocal=tailBeat-tailCycle*span;
                double tail=2.1+tailCycle*t.LoopSeconds+(t.Sample(tailLocal)-t.FirstBeat)/StageBeats.Rate-anchor;
                Check(Math.Abs(chart.Notes[n].TimeSeconds+chart.Notes[n].HoldSeconds-tail)<.001,"tail crosses exact sample loop join");
            }
            Check(crossingHolds>0,"fixed production seed 0 exercises a hold with head before and tail after the real loop join");
            Check(Math.Abs(MathCall("StageSampleAtWorld",t,t.LoopSeconds)-t.FirstBeat)<1e-6,"world transport wraps at the exact authored sample");
            foreach(double world in new[]{0.0,anchor,t.LoopSeconds-.0001,t.LoopSeconds,t.LoopSeconds*2+.12})Check(Math.Abs(MathCall("StageWorldAtBeat",t,MathCall("StageBeatAtWorld",t,world))-world)<1e-8,"unrounded loop inverse");
            double one=(t.Sample(21)-t.Sample(20))/StageBeats.Rate;
            Check(MathCall("NearestStageStride",t,20.0,one*.75)==1,"half/one tie chooses slower");
            Check(MathCall("NearestStageStride",t,20.0,one*1.5)==2,"one/two tie chooses slower");
        }
    }
    static void LegacyAndMalformedSnapshots()
    {
        var legacy=JsonSerializer.Deserialize<InteractionState>("{\"Kind\":\"Dance\",\"ChartSeed\":17,\"NoteCount\":16,\"ChartDifficulty\":3,\"BeatSeconds\":0.5}",Json);
        Check(JsonSerializer.Serialize(RhythmChart.For(legacy),Json)==JsonSerializer.Serialize(RhythmChart.Create(17,16,.5,3),Json),"old snapshot retains fixed timing");
        foreach(string field in new[]{"StageFirstHeadWorldSeconds","StageFirstHeadDelaySeconds","StageBeatStride"})foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1.0})
        {
            var game=Floor();var i=Anchored(1,4,2,1);i.Id="bad";game.State.Interactions.Add(i);typeof(InteractionState).GetField(field)?.SetValue(i,invalid);
            bool refused=false;try{new FestivalSimulation().Restore(game.State);}catch(ArgumentException){refused=true;}Check(refused,"restore refuses malformed "+field);
        }
        foreach(int choice in new[]{-1,3,6,99})
        {
            var game=Floor();game.State.Interactions.Add(Anchored(choice,4,2,1));bool refused=false;try{new FestivalSimulation().Restore(game.State);}catch(ArgumentException){refused=true;}Check(refused,"restore refuses non-stage track "+choice);
        }
    }
    static void DjPhrasesDoNotOverlapTheirVerdicts()
    {
        var game=Floor();var p=game.Player("p0");p.X=Catalog.StageTakeoverX;p.Z=Catalog.StageTakeoverZ;p.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});
        var i=Start(game,"Dj");double previousEnd=i.StartSeconds+i.DurationSeconds+.75;
        Check((int)Field(i,"StageTrack")==RhythmChart.StageTrackFor(0,0),"takeover rides the stage song");
        game.State.SimulationSeconds=previousEnd;game.Tick(.01);
        Check(i.Phrase==1&&i.StartSeconds>=previousEnd-1e-9,"next DJ phrase begins after the full preceding chart and late verdict");
        Check(i.DurationSeconds==RhythmChart.For(i).DurationSeconds,"next DJ phrase has real chart duration");
    }

    static void ActiveStageAnchorSurvivesTheProductionPause()
    {
        var game=Floor(0,3);var i=Start(game,"Dance");double start=i.StartSeconds,world=FestivalSimulation.WorldSeconds(game.State);
        var before=JsonSerializer.Serialize(RhythmChart.For(i),Json);double anchor=(double)Field(i,"StageFirstHeadWorldSeconds");
        // Reconnect fixture with an already active stage chart during the booth pause; the real minigame tick/banner resumes it.
        game.State.Phase=FestivalSimulation.MinigamePhase;game.State.Booth.Kind="future_game";game.State.Booth.ScoredAt=-1;
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(game.State,Json),Json));
        restored.Tick(60);restored.Tick(31);Check(restored.State.Phase==FestivalSimulation.MinigamePhase,"the result banner still holds the world");
        Check(Math.Abs(FestivalSimulation.WorldSeconds(restored.State)-world)<1e-7,"real paused world transport is held");
        restored.Tick(5);Check(restored.State.Phase=="Playing","production minigame cap and banner resumed the round");
        var resumed=restored.Interaction(i.Id);
        Check(Math.Abs(resumed.StartSeconds-start-restored.State.Booth.Seconds)<1e-7,"simulation countdown moves by the completed pause");
        Check((double)Field(resumed,"StageFirstHeadWorldSeconds")==anchor&&JsonSerializer.Serialize(RhythmChart.For(resumed),Json)==before,"first head, every mapped head and hold tail survive the pause exactly");
        var first=RhythmChart.For(resumed).Notes[0];restored.State.SimulationSeconds=resumed.StartSeconds+first.TimeSeconds;
        var hit=restored.Execute("p0",new GameCommand{Id="pause_hit",Kind="Rhythm",Direction=first.Direction,TimeSeconds=first.TimeSeconds});Check(hit.Accepted,"resumed mapped timestamp remains in the real input envelope: "+hit.Reason);
    }
    static void MeasuredPacingKeepsAllLogicalPatterns()
    {
        foreach(int choice in new[]{1,2,4,5})foreach(var kind in new[]{"Dance","Poi","ConfirmDance","Conversation"})
        {
            double tuned=kind=="Poi"?.4:.5;int count=kind=="ConfirmDance"?4:kind=="Poi"?12:8;
            double min=double.MaxValue,max=double.MinValue,total=0;int measured=0;
            foreach(double at in new[]{0.0,30.123,80.321,150.654})for(int seed=0;seed<30;seed++)
            {
                var i=new InteractionState{Kind=kind,ChartSeed=seed,NoteCount=count,BeatSeconds=tuned,ChartDifficulty=3};
                RhythmChart.InitializeStage(i,choice,at);var chart=RhythmChart.For(i);var logical=RhythmChart.Create(seed,count,tuned,kind=="ConfirmDance"?RhythmChart.QuarterNotes:3);
                Check(chart.Notes.Count==logical.Notes.Count,"same pattern draw count at every tuning and track");
                for(int n=0;n<chart.Notes.Count;n++)Check(chart.Notes[n].Id==logical.Notes[n].Id&&chart.Notes[n].Direction==logical.Notes[n].Direction&&(chart.Notes[n].HoldSeconds>0)==(logical.Notes[n].HoldSeconds>0),"same IDs, lanes, jumps, runs, holds and covered rests");
                double pace=100*((chart.DurationSeconds-chart.Notes[0].TimeSeconds)/(count*tuned)-1);min=Math.Min(min,pace);max=Math.Max(max,pace);total+=pace;measured++;
            }
            Console.WriteLine("STAGE PACE "+choice+" "+StageBeats.For(choice).Clip+" "+kind+" tuned="+Number(tuned)+" mean="+Number(total/measured)+" min="+Number(min)+" max="+Number(max)+" percent-duration ("+measured+" charts)");
        }
    }
}
