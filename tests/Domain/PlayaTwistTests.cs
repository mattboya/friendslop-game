using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// PLAYA-1: Ember Playa (festival 1) has four twists, and Palm Mirage has none of them. Money reads as odd objects, a different
// set for each player, over the same economy. Dust storms of 20-40 s blow on a seeded schedule and cut everyone's sight, cops'
// too (PLAYA-3), from 12 m to 5 m. Two art cars crawl round slow loops, and a rider moves with their car out of festivalgoers' sight
// (PLAYA-4: a swarm already after them trails the car for a few seconds, then gives up). For the
// last three minutes of Night 2 the effigy burns: the crowd gathers round it (a festivalgoer busy with someone first finishes
// with them), and within 12 m of it every step is x.7 and suspicion of anyone there cools twice as fast.
public static class PlayaTwistTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("PlayaTwist: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="")=>s.Execute(id,new GameCommand{Id="playa"+(sequence++),Kind=kind,TargetId=target,ItemId=item});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{EmberPlayaCountsInOddObjects,EachPlayerKeepsTheirOwnObjects,DustStormsBlowOnASeededSchedule,
            StormsCutEveryonesSightTo5m,ArtCarsCrawlRoundTheirLoops,APlainRiderStartsAnExclusiveDeckSet,DeckOwnershipIsRequired,ASecondDeckSetSaysTheSetIsAlreadyPlaying,DeckChartsUseTheSharedGridAndHolds,DeckWitnessesAccumulateByRangeAndLos,StormsLimitDeckWitnesses,DeckTipsSettleExactlyOnce,AnotherRiderCannotCollectOldTips,TheFullTipThresholdIsInclusive,TheMinimumTipThresholdIsInclusive,HalfAndFailedDecksKeepTheirRules,CancellingADeckNeverPays,AStaleCancelLeavesANewDeckSetAlone,PoliceAndLifecycleInterruptionsNeverPay,InvalidRestoredDecksAreRetired,OldAndTippedSnapshotsRestore,DecksStayOutsideGroupJudging,ArtCarsCarryRidersOutOfSight,ASwarmTrailsARiderThenGivesUp,ARiderWhoHopsOffMidTrailIsSwarmedAgain,
            TheEffigyBurnsForTheLastThreeMinutesOfNightTwo,TheCrowdGathersAtTheBurn,BurnWalkersKeepGapAcrossAnotherWalker,AnAccusationInTheCrushReachesOnlyNeighbours,AFestivalgoerFinishesAChatBeforeHeadingOver,
            TheCrushSlowsEveryStep,SuspicionCoolsFasterInTheCrush})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" playa twist test(s) failed:\n"+string.Join("\n",failures));
    }

    // A crew of `crew` readies up at camp, the wheels spin and they load into the crowd.
    static FestivalSimulation Start(int seed,int level,int festival,int crew=2)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew is at "+Festivals.Name(festival));
        return s;
    }

    static void EmberPlayaCountsInOddObjects()
    {
        for(int k=0;k<8;k++)Check(Festivals.CurrencyName(0,k,12)=="$12"&&Festivals.CurrencyName(0,k,1)=="$1","Palm Mirage counts dollars for everyone, got "+Festivals.CurrencyName(0,k,12));
        Check(Festivals.CurrencyName(1,1,12)=="12 ramen packets","the second player counts ramen packets, got "+Festivals.CurrencyName(1,1,12));
        Check(Festivals.CurrencyName(1,1,1)=="1 ramen packet","one of them is a ramen packet, got "+Festivals.CurrencyName(1,1,1));
        Check(Festivals.CurrencyName(1,0,3)=="3 buttons"&&Festivals.CurrencyName(1,2,0)=="0 bottle caps","the first counts buttons and the third bottle caps, got "+Festivals.CurrencyName(1,0,3)+", "+Festivals.CurrencyName(1,2,0));
        var kinds=new HashSet<string>();
        for(int k=0;k<8;k++)
        {
            string many=Festivals.CurrencyName(1,k,5),one=Festivals.CurrencyName(1,k,1);
            Check(many.StartsWith("5 ")&&!many.Contains("$")&&one.StartsWith("1 ")&&!one.Contains("$"),"player "+k+" counts objects, not dollars: "+many+", "+one);
            Check(one.Substring(2)!=many.Substring(2),"player "+k+" has one "+one.Substring(2)+" but several "+many.Substring(2));
            kinds.Add(many.Substring(2));
        }
        Check(kinds.Count==8,"a full crew of eight counts in eight different objects, got "+string.Join(", ",kinds));
    }

    static void EachPlayerKeepsTheirOwnObjects()
    {
        var s=Start(3,0,1,8);
        for(int k=0;k<8;k++)Check(s.Player("p"+k).Ordinal==k,"p"+k+" is crew member "+k+" in joining order, got "+s.Player("p"+k).Ordinal);
        var kinds=new HashSet<string>();foreach(var p in s.State.Players)kinds.Add(Festivals.CurrencyName(s.State.FestivalIndex,p.Ordinal,5));
        Check(kinds.Count==8,"so each of the eight counts in their own objects: "+string.Join(", ",kinds));
        s.Disconnect("p3");s.AddPlayer("p3","P3");Check(s.Player("p3").Ordinal==3,"a friend who drops and rejoins keeps theirs");
        s.State.Phase="Results";s.State.Result="Success";Check(Act(s,"p0","Reset").Accepted,"setup: the host heads back to camp after Day 1");
        Check(s.State.LevelIndex==1,"setup: Night 1 is next");
        for(int k=0;k<8;k++)Check(s.Player("p"+k).Ordinal==k,"p"+k+" keeps theirs through the weekend, got "+s.Player("p"+k).Ordinal);
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        foreach(var p in legacy["Players"].AsArray())p.AsObject().Remove("Ordinal");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.Players.Count==8,"a snapshot from before odd objects still restores");
    }

    // A level's storms, sampled every tenth of a second as (start, end) spells; a spell still blowing at `until` ends at infinity.
    static List<(double start,double end)> Storms(RoundState s,double until)
    {
        var spells=new List<(double,double)>();double began=-1;
        for(int tick=0;tick<=until*10;tick++)
        {
            s.ElapsedSeconds=tick/10.0;bool storm=FestivalSimulation.DustStorm(s);
            if(storm&&began<0)began=s.ElapsedSeconds;
            if(!storm&&began>=0){spells.Add((began,s.ElapsedSeconds));began=-1;}
        }
        if(began>=0)spells.Add((began,double.PositiveInfinity));
        return spells;
    }

    static void DustStormsBlowOnASeededSchedule()
    {
        var schedules=new HashSet<string>();
        for(int seed=1;seed<=40;seed++)
        {
            int spin=unchecked(seed*7919+13);var spells=Storms(new RoundState{FestivalIndex=1,SpinSeed=spin},Festivals.NightSeconds);
            Check(spells.Count>=3,"spin "+spin+": a night on Ember Playa brings at least three storms, got "+spells.Count);
            Check(spells[0].start>=60&&spells[0].start<=120,"spin "+spin+": the first storm rolls in 60-120 s into the level, got "+spells[0].start);
            for(int k=0;k<spells.Count;k++)
            {
                if(spells[k].end<double.PositiveInfinity)Check(spells[k].end-spells[k].start>=20&&spells[k].end-spells[k].start<=40,"spin "+spin+": storm "+k+" blows for 20-40 s, got "+(spells[k].end-spells[k].start));
                if(k>0)Check(spells[k].start-spells[k-1].end>=60&&spells[k].start-spells[k-1].end<=120,"spin "+spin+": 60-120 s of calm between storms, got "+(spells[k].start-spells[k-1].end));
            }
            schedules.Add(string.Join(";",spells));
            Check(Storms(new RoundState{FestivalIndex=0,SpinSeed=spin},Festivals.NightSeconds).Count==0,"spin "+spin+": no dust at Palm Mirage");
        }
        Check(schedules.Count==40,"each level's spin seeds its own schedule, got "+schedules.Count+" of 40");
        var live=Start(5,0,1);var seen=new List<bool>();
        for(int second=0;second<=Festivals.CalmMaxSeconds+1;second++){seen.Add(FestivalSimulation.DustStorm(live.State));live.Tick(1);}
        Check(!seen[0]&&seen.Contains(true),"a real level on Ember Playa calms at first, then a storm rolls in");
    }

    const int StormSpin=12345;
    // Some time well inside this spin's first storm.
    static double MidStorm(){var s=new RoundState{FestivalIndex=1,SpinSeed=StormSpin};return Storms(s,Festivals.NightSeconds)[0].start+5;}
    // A hand-built Day 1 at `seconds` into the level: a festivalgoer at the origin looking north (+Z) with p0 sprinting
    // `metres` in front of it, and a cop a metre east of it, also looking north, with p0's stock in plain sight.
    static FestivalSimulation Sighting(int festival,double seconds,double metres)
    {
        var s=new FestivalSimulation(3);s.AddPlayer("p0","P0");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.Phase="Playing";s.State.SpinSeed=StormSpin;s.State.ElapsedSeconds=seconds;
        s.State.Npcs.Clear();s.State.Npcs.Add(new NpcState{Id="wook"});s.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="Patrol",X=1});
        var p=s.Player("p0");p.X=0;p.Z=(float)metres;p.SprintUntil=1e9;p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});p.EquippedItemId="stock_lsd";
        return s;
    }
    // Whether, in a tenth of a second, the festivalgoer notices p0 sprinting and the cop spots the stock.
    static (bool wook,bool cop) Spotted(FestivalSimulation s)
    {
        s.Tick(.1);var wook=s.State.Npcs.Find(n=>n.Id=="wook");var cop=s.State.Npcs.Find(n=>n.Id=="cop");
        return ((wook.Observers.Find(o=>o.PlayerId=="p0")?.Suspicion??0)>0,cop.Evidence.Exists(e=>e.PlayerId=="p0"));
    }

    // PLAYA-3: the storm blinds cops as well as festivalgoers. The cop stands a metre east of the festivalgoer, so it is
    // sqrt(1+metres²) from p0: about 6.1 m at 6, about 4.9 m at 4.8.
    static void StormsCutEveryonesSightTo5m()
    {
        double storm=MidStorm();
        Check(!FestivalSimulation.DustStorm(Sighting(1,1,6).State)&&FestivalSimulation.DustStorm(Sighting(1,storm,6).State),"setup: calm a second in, a storm at "+storm+" s");
        Check(Spotted(Sighting(1,1,11.9)).wook,"in calm air a festivalgoer spots a sprinter 11.9 m away");
        Check(!Spotted(Sighting(1,1,12.1)).wook,"but not 12.1 m away");
        var dusty=Spotted(Sighting(1,storm,6));
        Check(!dusty.wook,"in a storm the festivalgoer loses a sprinter 6 m away");
        Check(!dusty.cop,"and the cop about 6.1 m away loses sight of the stock too");
        Check(Spotted(Sighting(1,storm,4.8)).cop,"a cop about 4.9 m away still spots the stock in a storm");
        Check(Spotted(Sighting(1,1,6)).cop,"in calm air the cop about 6.1 m away spots it");
        Check(Spotted(Sighting(1,storm,4.9)).wook,"a sprinter 4.9 m away is still spotted in a storm");
        Check(!Spotted(Sighting(1,storm,5.1)).wook,"5.1 m away is not");
        var mirage=Spotted(Sighting(0,storm,6));
        Check(mirage.wook,"Palm Mirage has no dust, so the same moment there is clear");
        Check(mirage.cop,"for its cops too");
    }

    static double Dist(WorldPoint a,WorldPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
    static bool At(PlayerState p,WorldPoint at)=>Dist(new WorldPoint(p.X,p.Z),at)<1e-3;

    static void ArtCarsCrawlRoundTheirLoops()
    {
        Check(Festivals.ArtCars==2,"two art cars roll round Ember Playa");
        Check(Festivals.ArtCarSpeed>0&&Festivals.ArtCarSpeed<=1.5,"at a slow crawl, not "+Festivals.ArtCarSpeed+" m/s");
        var starts=new List<WorldPoint>();
        for(int car=0;car<Festivals.ArtCars;car++)
        {
            var start=Festivals.ArtCarAt(car,0);var last=start;double lap=-1,travelled=0;starts.Add(start);
            for(int tick=1;tick<=1200&&lap<0;tick++)
            {
                var at=Festivals.ArtCarAt(car,tick/10.0);double step=Dist(last,at);travelled+=step;last=at;
                Check(step<=Festivals.ArtCarSpeed*.1+1e-4,"car "+car+" never jumps: "+step+" m in a tenth of a second at "+tick/10.0+" s");
                Check(Math.Abs(at.X)<35&&Math.Abs(at.Z)<35,"car "+car+" stays on the festival grounds, at ("+at.X+", "+at.Z+")");
                if(tick>100&&Dist(at,start)<=Festivals.ArtCarSpeed*.1)lap=tick/10.0;
            }
            Check(lap>0,"car "+car+" comes back round to where it started within two minutes");
            Check(travelled>=lap*Festivals.ArtCarSpeed*.97,"car "+car+" keeps rolling all the way round: "+travelled+" m in "+lap+" s");
            Check(Dist(Festivals.ArtCarAt(car,lap+7),Festivals.ArtCarAt(car,7))<=Festivals.ArtCarSpeed*.1+1e-3&&Dist(Festivals.ArtCarAt(car,lap+7),Festivals.ArtCarAt(car,lap))>1,"and rolls on round again");
        }
        Check(Dist(starts[0],starts[1])>10,"the two cars roll on separate loops");
    }

    // A hand-built Ember Playa Day 1, ten seconds in and well before any storm: p0, with stock in hand, stands a metre from art
    // car 0, and p1 stands a metre from art car 1. Nobody from the crowd is around yet.
    static FestivalSimulation Cars(int festival=1)
    {
        var s=new FestivalSimulation(3);s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.Phase="Playing";s.State.SpinSeed=StormSpin;s.State.ElapsedSeconds=10;
        s.State.Npcs.Clear();
        for(int k=0;k<2;k++){var car=Festivals.ArtCarAt(k,10);var p=s.Player("p"+k);p.X=car.X+1;p.Z=car.Z;}
        s.Player("p0").Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});s.Player("p0").EquippedItemId="stock_lsd";
        return s;
    }
    // A festivalgoer and a cop turn up 2 m north of p, looking straight at them, while p sprints.
    static void Watched(FestivalSimulation s,PlayerState p)
    {
        s.State.Npcs.Clear();p.SprintUntil=1e9;
        s.State.Npcs.Add(new NpcState{Id="wook",X=p.X,Z=p.Z+2,Yaw=180});s.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="Patrol",X=p.X+.5f,Z=p.Z+2,Yaw=180});
    }
    static double Heat(FestivalSimulation s,string npc,string player)=>s.State.Npcs.Find(n=>n.Id==npc).Observers.Find(o=>o.PlayerId==player)?.Suspicion??0;


    // Deck start authorization, one deck per car and rolling/movement ownership.
    static void APlainRiderStartsAnExclusiveDeckSet()
    {
        var s=Cars();var p=s.Player("p0");var q=s.Player("p1");
        Check(!Act(s,q.Id,"DeckSet").Accepted,"someone on the ground cannot play a deck set");
        Check(Act(s,p.Id,"RideCar").Accepted,"setup: board car 0");
        var ride=s.Interaction(p.InteractionId);
        Check(Act(s,p.Id,"DeckSet").Accepted,"a plain art-car rider must be able to replace the ride with a deck set");
        var deck=s.Interaction(p.InteractionId);
        Check(deck.Kind=="DeckSet"&&deck.TargetId=="0"&&ride.Status!="Active","the deck owns the same car and retires the plain ride");
        Check(FestivalSimulation.ArtCarOf(s.State,p.Id)==0,"playing the deck still counts as riding");
        Check(deck.NoteCount==16&&Math.Abs(deck.BeatSeconds-.4)<1e-9&&deck.ChartDifficulty==s.State.LevelIndex,"16 logical beats at this level's difficulty");
        var at=Festivals.ArtCarAt(0,s.State.ElapsedSeconds);q.X=at.X;q.Z=at.Z;
        Check(Act(s,q.Id,"RideCar").Accepted,"setup: second rider boards same car");
        var denied=Act(s,q.Id,"DeckSet");
        Check(!denied.Accepted&&denied.Reason=="Someone's already on this deck","one active player per car deck");
        Check(!s.TryMove(p.Id,p.X+.1f,p.Z,p.Yaw,.1),"playing the deck cannot walk off the moving car");
        s.Tick(.1);Check(At(p,Festivals.ArtCarAt(0,s.State.ElapsedSeconds)),"the deck rider keeps rolling with car 0");
        var polo=Cars(0);Check(!Act(polo,"p0","DeckSet").Accepted,"Palm Mirage refuses deck sets");
    }

    static FestivalSimulation DeckFloor(int level=0,int crew=2,int fans=2,int seed=3)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=1;s.State.LevelIndex=level;s.State.DurationSeconds=Festivals.For(s.State).DurationSeconds;
        s.State.Phase="Playing";s.State.SpinSeed=StormSpin;s.State.ElapsedSeconds=10;s.State.Npcs.Clear();
        var at=Festivals.ArtCarAt(0,10);var p=s.Player("p0");p.X=at.X;p.Z=at.Z;
        foreach(var q in s.State.Players)if(q!=p){q.X=30;q.Z=-25;}
        for(int k=0;k<fans;k++)s.State.Npcs.Add(new NpcState{Id="fan"+k,X=at.X+2+k*.1f,Z=at.Z,Yaw=90,Observers=new List<ObserverState>{new ObserverState{PlayerId=p.Id,Suspicion=40}}});
        Check(Act(s,p.Id,"RideCar").Accepted,"setup: rider boards");return s;
    }
    static InteractionState Deck(FestivalSimulation s){Check(Act(s,"p0","DeckSet").Accepted,"setup: start deck");return s.Interaction(s.Player("p0").InteractionId);}
    static bool Tipped(NpcState n)=>JsonNode.Parse(JsonSerializer.Serialize(n,Json))["Tipped"]?.GetValue<bool>()??false;
    static void Play(FestivalSimulation s,InteractionState i,double fraction=1)
    {
        var chart=RhythmChart.For(i);int hits=(int)Math.Floor(chart.Notes.Count*fraction);var edges=new List<RhythmInput>();
        for(int k=0;k<hits;k++){var n=chart.Notes[k];edges.Add(new RhythmInput{Direction=n.Direction,TimeSeconds=n.TimeSeconds});if(n.HoldSeconds>0)edges.Add(new RhythmInput{Direction=n.Direction,TimeSeconds=n.TimeSeconds+n.HoldSeconds,Release=true});}
        edges.Sort((a,b)=>a.TimeSeconds.CompareTo(b.TimeSeconds));
        foreach(var edge in edges){s.State.SimulationSeconds=i.StartSeconds+edge.TimeSeconds;var result=s.Execute(i.PlayerId,new GameCommand{Id="edge"+(sequence++),Kind="Rhythm",Direction=edge.Direction,TimeSeconds=edge.TimeSeconds,Amount=edge.Release?1:0});Check(result.Accepted,"real rhythm edge accepted: "+result.Reason);}
    }
    static void FinishDeck(FestivalSimulation s,InteractionState i){s.State.SimulationSeconds=i.StartSeconds+i.DurationSeconds+.75;s.Tick(.1);}
    static void DeckOwnershipIsRequired()
    {
        var s=DeckFloor();var p=s.Player("p0");var q=s.Player("p1");q.InteractionId=p.InteractionId;
        Check(!Act(s,q.Id,"DeckSet").Accepted,"a foreign ride ID never authorizes a deck");q.InteractionId="";
        p.Life="Downed";Check(!Act(s,p.Id,"DeckSet").Accepted,"downed riders cannot start");p.Life="Alive";
        var old=s.Interaction(p.InteractionId);Check(Act(s,p.Id,"DeckSet").Accepted,"own plain ride starts");Check(Heat(s,"fan0",p.Id)==40,"replacement adds no cancel penalty");
        Check(!Act(s,p.Id,"DeckSet").Accepted&&old.Status=="Complete","a deck cannot replace itself");
    }
    // A joined rider's view shows the plain ride for a moment after F, so a second F sends a second DeckSet. The host
    // refuses it in words that fit what the rider is doing, and the set plays on.
    static void ASecondDeckSetSaysTheSetIsAlreadyPlaying()
    {
        var s=DeckFloor();var i=Deck(s);var again=Act(s,"p0","DeckSet");
        Check(!again.Accepted&&again.Reason=="Your set is already playing","a second DeckSet says the set is playing, not \""+again.Reason+"\"");
        Check(i.Status=="Active"&&s.Player("p0").InteractionId==i.Id&&FestivalSimulation.ArtCarOf(s.State,"p0")==0,"and the set plays on, aboard the car");
    }
    static void DeckChartsUseTheSharedGridAndHolds()
    {
        foreach(int level in new[]{0,1,2,3})
        {
            var s=DeckFloor(level);double started=s.State.SimulationSeconds,world=FestivalSimulation.WorldSeconds(s.State);var i=Deck(s);
            var rider=s.Player(i.PlayerId);Check(i.StageTrack==StageMusic.TrackAt(s.State,rider.X,rider.Z),"deck uses the serialized one-based song of the stage nearer where it starts");
            Check(i.StartSeconds==started,"deck preparation belongs only to its chart, without a duplicate external countdown");
            var expected=new InteractionState{Kind="DeckSet",ChartSeed=i.ChartSeed,ChartDifficulty=level,NoteCount=16,BeatSeconds=.4};RhythmChart.InitializeStage(expected,i.StageTrack,world,2.4);
            Check(i.StageFirstHeadWorldSeconds>=world+2.4&&i.StageFirstHeadWorldSeconds<world+3.4,"one window admits the longest2.4s arrow look-ahead and the next authored beat");
            foreach(var effect in Catalog.Effects)Check(i.StageFirstHeadWorldSeconds-world>=effect.LeadSeconds,"the first head can enter every current substance lane");
            var chart=RhythmChart.For(i);var wanted=RhythmChart.For(expected);
            Check(i.StageFirstHeadWorldSeconds==expected.StageFirstHeadWorldSeconds&&i.StageBeatStride==expected.StageBeatStride,"shared initializer owns the first-head and nearest .4 grid stride");
            Check(chart.Notes.Count==wanted.Notes.Count&&i.DurationSeconds==wanted.DurationSeconds,"full mapped duration and logical difficulty");
            foreach(var n in chart.Notes){var other=wanted.Notes[n.Id];Check(n.Direction==other.Direction&&n.TimeSeconds==other.TimeSeconds&&n.HoldSeconds==other.HoldSeconds&&n.TimeSeconds+n.HoldSeconds<=i.DurationSeconds+1e-8,"every real head and hold tail survives the chart map");}
            if(level==1)Check(chart.Notes.Exists(n=>n.HoldSeconds>0),"setup: fixed seed deck has a real hold");
            Play(s,i);FinishDeck(s,i);Check(i.Score==1,"real presses/releases replay a perfect deck including holds");
        }
    }
    static void DeckWitnessesAccumulateByRangeAndLos()
    {
        var s=DeckFloor(fans:0);var p=s.Player("p0");var at=Festivals.ArtCarAt(0,10);
        s.State.Npcs.Add(new NpcState{Id="back",X=at.X+6,Z=at.Z,Yaw=90});s.State.Npcs.Add(new NpcState{Id="blocked",X=at.X+2,Z=at.Z+1});s.State.Npcs.Add(new NpcState{Id="later",X=at.X,Z=at.Z+9});s.State.Npcs.Add(new NpcState{Id="far",X=30,Z=-30});s.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",X=at.X+1,Z=at.Z});
        s.HasLineOfSight=(x,z,xx,zz)=>z!=at.Z+1;var i=Deck(s);
        Check(i.WitnessIds.Contains("back")&&!i.WitnessIds.Contains("blocked")&&!i.WitnessIds.Contains("later")&&!i.WitnessIds.Contains("cop"),"start witnesses use inclusive 6m LOS without a facing cone, excluding cops");
        s.Tick(3);Check(i.WitnessIds.Contains("later")&&!i.WitnessIds.Contains("far")&&i.WitnessIds.Count==2,"the moving car collects a new fan exactly once");
        var later=s.State.Npcs.Find(n=>n.Id=="later");later.X=30;later.Z=30;Play(s,i);FinishDeck(s,i);
        Check(Tipped(later)&&Heat(s,"later",p.Id)==0,"departed accumulated witnesses still settle score and tips");
        Check(s.Interaction(p.InteractionId).Kind=="RideCar","completion returns to a fresh hidden plain ride");
    }
    static void StormsLimitDeckWitnesses()
    {
        var s=DeckFloor(fans:0);s.State.ElapsedSeconds=MidStorm();var p=s.Player("p0");var at=Festivals.ArtCarAt(0,s.State.ElapsedSeconds);p.X=at.X;p.Z=at.Z;
        Check(FestivalSimulation.DustStorm(s.State),"setup: an actual seeded storm");
        s.State.Npcs.Add(new NpcState{Id="edge",X=p.X,Z=p.Z+5});s.State.Npcs.Add(new NpcState{Id="outside",X=p.X+5.1f,Z=p.Z});
        var i=Deck(s);Check(i.WitnessIds.Count==1&&i.WitnessIds.Contains("edge"),"storm includes 5m and excludes 5.1m");
    }
    static void DeckTipsSettleExactlyOnce()
    {
        var s=DeckFloor(crew:4,fans:3);s.State.EncoreTier=2;s.State.ConnectedCrewCount=3;var p=s.Player("p0");int paid=10;int cash=p.Cash;s.State.LevelSales=FestivalSimulation.DayQuota(s.State)-paid;s.State.GrossSales=27;var i=Deck(s);Play(s,i);FinishDeck(s,i);
        Check(i.Score==1&&p.Cash==cash+paid&&s.State.GrossSales==37&&FestivalSimulation.DayQuotaMet(s.State),"round(.75*3 fresh*live quota crew3*encore1.5) atomically credits cash/gross/day quota once");
        Check(s.State.Npcs.TrueForAll(Tipped),"successful fresh fans are globally tipped");var ride=s.Interaction(p.InteractionId);Check(ride.Kind=="RideCar"&&ride.TargetId=="0"&&ride.Id!=i.Id&&ride.StartSeconds==s.State.SimulationSeconds,"completion creates a fresh same-car ride");
        var next=Deck(s);Play(s,next);FinishDeck(s,next);s.Tick(.3);Check(p.Cash==cash+paid&&s.State.GrossSales==37,"no fan pays twice on a second set or later tick");
    }
    static void AnotherRiderCannotCollectOldTips()
    {
        var s=DeckFloor();var i=Deck(s);Play(s,i);FinishDeck(s,i);var q=s.Player("p1");var at=Festivals.ArtCarAt(0,s.State.ElapsedSeconds);q.X=at.X;q.Z=at.Z;
        Check(Act(s,q.Id,"RideCar").Accepted&&Act(s,q.Id,"DeckSet").Accepted,"another rider takes the same completed deck");var other=s.Interaction(q.InteractionId);Play(s,other);FinishDeck(s,other);
        Check(q.Cash==20&&s.State.LevelSales==3&&s.State.GrossSales==3,"the global tip flag also prevents another player collecting old fans");
    }
    static void TheFullTipThresholdIsInclusive()=>AtTheTipThreshold(.75,3);
    static void TheMinimumTipThresholdIsInclusive()=>AtTheTipThreshold(.4,2);
    static void AtTheTipThreshold(double score,int paid)
    {
        var s=DeckFloor(seed:9);var i=Deck(s);Check(i.ChartSeed==11&&RhythmChart.For(i).Notes.Count==20,"setup: fixed seed9 creates chart11 with20 real tap heads");
        Play(s,i,score);FinishDeck(s,i);Check(i.Score==score&&s.State.LevelSales==paid,"exact "+score+" score includes its tip threshold");
    }
    static void HalfAndFailedDecksKeepTheirRules()
    {
        var s=DeckFloor(fans:4);var p=s.Player("p0");int cash=p.Cash;var i=Deck(s);Play(s,i,.6);FinishDeck(s,i);
        Check(i.Score>=.4&&i.Score<.75&&p.Cash==cash+3&&s.State.LevelSales==3&&s.State.GrossSales==3,"a passing lower deck pays round(.75*4*2*.5)");
        var failed=DeckFloor();var f=Deck(failed);Play(failed,f,.25);FinishDeck(failed,f);
        Check(f.Score<.4&&failed.Player("p0").Cash==20&&failed.State.LevelSales==0&&!failed.State.Npcs.Exists(Tipped),"below .4 neither pays nor marks fans");
        var retry=Deck(failed);Play(failed,retry);FinishDeck(failed,retry);Check(failed.State.LevelSales==3&&failed.State.Npcs.TrueForAll(Tipped),"failed fans remain eligible for a later passing set");
    }
    static void CancellingADeckNeverPays()
    {
        var s=DeckFloor();var i=Deck(s);Play(s,i);Check(Act(s,"p0","Cancel").Accepted,"explicit stop accepted");
        Check(i.Status=="Cancelled"&&s.Player("p0").InteractionId==""&&FestivalSimulation.ArtCarOf(s.State,"p0")==-1,"cancel ends both deck and ride");
        Check(Heat(s,"fan0","p0")==60&&Heat(s,"fan1","p0")==60,"cancel adds20 to accumulated witnesses");FinishDeck(s,i);Check(s.State.LevelSales==0&&!s.State.Npcs.Exists(Tipped),"cancel never later pays or marks fans");
    }
    // A joined view lags the host: a rider's E, sent for the plain ride it saw, lands after F has replaced that ride with a set.
    static void AStaleCancelLeavesANewDeckSetAlone()
    {
        var s=DeckFloor();string ride=s.Player("p0").InteractionId;var i=Deck(s);
        var stale=Act(s,"p0","Cancel",ride);
        Check(!stale.Accepted&&stale.Reason=="That action already ended","the host refuses a Cancel for the ride the set replaced: "+stale.Reason);
        Check(i.Status=="Active"&&s.Player("p0").InteractionId==i.Id&&FestivalSimulation.ArtCarOf(s.State,"p0")==0,"the set plays on, still aboard car 0");
        Check(Heat(s,"fan0","p0")==40&&Heat(s,"fan1","p0")==40,"no witness grows suspicious");
        Check(Act(s,"p0","Cancel",i.Id).Accepted&&i.Status=="Cancelled"&&Heat(s,"fan0","p0")==60,"a Cancel naming the set stops it, with the usual cost");
    }
    static void PoliceAndLifecycleInterruptionsNeverPay()
    {
        foreach(string reason in new[]{"police","disconnect","down","round"})
        {
            var s=DeckFloor();var p=s.Player("p0");var i=Deck(s);Play(s,i);
            if(reason=="police")s.State.Npcs.Add(new NpcState{Id="arrest",Kind="Cop",X=p.X,Z=p.Z,Evidence=new List<EvidenceState>{new EvidenceState{PlayerId=p.Id,Kind="WitnessedDeal",DetainAt=i.StartSeconds+i.DurationSeconds+.75}}});
            if(reason=="disconnect")s.Disconnect(p.Id);if(reason=="down")p.Life="Downed";if(reason=="round")s.State.ElapsedSeconds=s.State.DurationSeconds;
            FinishDeck(s,i);Check(i.Status!="Active"&&p.InteractionId==""&&s.State.LevelSales==0&&!s.State.Npcs.Exists(Tipped),reason+" ends the deck before payout or ride recreation");
            if(reason=="police")Check(p.Life=="Detained"&&p.Cash==10,"police fine precedes financial settlement");
        }
    }
    static void InvalidRestoredDecksAreRetired()
    {
        foreach(string invalid in new[]{"missing","disconnected","downed","detained","foreign","car","phase"})
        {
            var s=DeckFloor();var i=Deck(s);Play(s,i);var p=s.Player("p0");
            if(invalid=="missing")s.State.Players.Remove(p);if(invalid=="disconnected")p.Connected=false;if(invalid=="downed")p.Life="Downed";if(invalid=="detained")p.Life="Detained";if(invalid=="foreign")p.InteractionId="other";if(invalid=="car")i.TargetId="99";if(invalid=="phase")s.State.Phase="Results";
            var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
            Check(restored.Interaction(i.Id).Status!="Active",invalid+" restored deck is retired immediately");restored.State.SimulationSeconds=i.StartSeconds+i.DurationSeconds+1;restored.Tick(.1);
            Check(restored.State.LevelSales==0&&!restored.State.Interactions.Exists(x=>x.Status=="Active"&&x.Kind=="RideCar"),invalid+" never pays or recreates ride");
        }
    }
    static void OldAndTippedSnapshotsRestore()
    {
        var s=DeckFloor();var i=Deck(s);Play(s,i);FinishDeck(s,i);var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        Check(restored.State.Npcs.TrueForAll(Tipped),"host snapshots retain the once-per-level flags");var repeat=Deck(restored);Play(restored,repeat);FinishDeck(restored,repeat);Check(restored.State.LevelSales==3,"restored flags prevent replay tips");
        var old=JsonNode.Parse(JsonSerializer.Serialize(DeckFloor().State,Json));foreach(var npc in old["Npcs"].AsArray())npc.AsObject().Remove("Tipped");restored.Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),Json));
        Check(!restored.State.Npcs.Exists(Tipped),"older snapshots default untipped");var fresh=Deck(restored);Play(restored,fresh);FinishDeck(restored,fresh);Check(restored.State.LevelSales==3,"older restored untipped fans may tip");
    }
    static void DecksStayOutsideGroupJudging()
    {
        var s=DeckFloor(fans:0);var p=s.Player("p0");var q=s.Player("p1");q.X=p.X;q.Z=p.Z+.5f;
        var n=new NpcState{Id="partner",X=p.X,Z=p.Z+2,Yaw=180,CanTalk=true,Observers=new List<ObserverState>{new ObserverState{PlayerId=p.Id,Suspicion=40},new ObserverState{PlayerId=q.Id,Suspicion=40}}};s.State.Npcs.Add(n);var deck=Deck(s);
        Check(!FestivalSimulation.DancesVisibly(deck.Kind)&&!FestivalSimulation.Dancing(deck),"deck retains riding identity outside spacing/steps");Check(Act(s,q.Id,"Dance",n.Id).Accepted,"setup: ground friend starts actual dance");var dance=s.Interaction(q.InteractionId);Play(s,dance);FinishDeck(s,dance);
        Check(deck.Status=="Active","setup: longer deck still active at friend's verdict");Check(Heat(s,n.Id,p.Id)==40&&Heat(s,n.Id,q.Id)==25,"deck rider neither sets idle worst nor receives a friend's group verdict");
    }

    static void ArtCarsCarryRidersOutOfSight()
    {
        var far=Cars();far.Player("p0").X+=2.2f;
        Check(!Act(far,"p0","RideCar").Accepted&&FestivalSimulation.ArtCarOf(far.State,"p0")<0,"an art car is boarded from beside it, not 3.2 m off");
        var reach=Cars();reach.Player("p0").X+=1.9f;
        Check(Act(reach,"p0","RideCar").Accepted,"2.9 m off is close enough");
        Check(!Act(Cars(0),"p0","RideCar").Accepted,"Palm Mirage has no art cars");
        var dragging=Cars();dragging.Player("p1").Life="Downed";dragging.Player("p0").DragTargetId="p1";
        Check(!Act(dragging,"p0","RideCar").Accepted,"nobody climbs aboard dragging a friend");
        var carrying=Cars();carrying.Player("p1").Life="Spirit";carrying.State.Bodies.Add(new BodyState{PlayerId="p1",X=carrying.Player("p0").X,Z=carrying.Player("p0").Z});carrying.Player("p0").CarryBodyId="p1";
        Check(!Act(carrying,"p0","RideCar").Accepted,"or carrying a body");

        var s=Cars();var p=s.Player("p0");var q=s.Player("p1");
        var boarded=Act(s,"p0","RideCar");
        Check(boarded.Accepted&&FestivalSimulation.ArtCarOf(s.State,"p0")==0&&At(p,Festivals.ArtCarAt(0,s.State.ElapsedSeconds)),"beside art car 0, p0 climbs aboard: "+boarded.Reason);
        Check(Act(s,"p1","RideCar").Accepted&&FestivalSimulation.ArtCarOf(s.State,"p1")==1,"beside art car 1, p1 climbs aboard that one");
        s.Tick(5);
        Check(At(p,Festivals.ArtCarAt(0,s.State.ElapsedSeconds))&&At(q,Festivals.ArtCarAt(1,s.State.ElapsedSeconds))&&!At(p,Festivals.ArtCarAt(0,10)),"five seconds on, each rider has rolled on with their car");
        float x=p.X,z=p.Z;
        Check(!s.TryMove("p0",x+.05f,z,0,.1)&&p.X==x&&p.Z==z,"a rider can't walk about on the moving car");
        Check(s.TryMove("p0",x,z,90,.1)&&p.Yaw==90,"but can look around");
        Check(!Act(s,"p0","Extract").Accepted,"and does nothing else up there");
        Watched(s,p);s.Tick(.1);
        Check(Heat(s,"wook","p0")==0,"a festivalgoer looking right at a rider sprinting 2 m away doesn't see them");
        Check(s.State.Npcs.Find(n=>n.Id=="cop").Evidence.Exists(e=>e.PlayerId=="p0"),"a cop does, and spots the stock");
        s.Tick(20);Check(FestivalSimulation.ArtCarOf(s.State,"p0")==0&&At(p,Festivals.ArtCarAt(0,s.State.ElapsedSeconds)),"the ride goes on until the rider hops off");
        var off=Act(s,"p0","Cancel");float offX=p.X,offZ=p.Z;
        Check(off.Accepted&&FestivalSimulation.ArtCarOf(s.State,"p0")<0,"p0 hops off: "+off.Reason);
        s.Tick(1);Check(p.X==offX&&p.Z==offZ,"and stays where they hopped off as the car rolls on");
        Check(s.TryMove("p0",p.X+.05f,p.Z,0,.1),"walking again");
        Watched(s,p);s.Tick(.1);
        Check(Heat(s,"wook","p0")>0,"back on foot, festivalgoers see them sprint again");
    }

    static double FromEffigy(float x,float z)=>Math.Sqrt((x-Festivals.EffigyX)*(x-Festivals.EffigyX)+(z-Festivals.EffigyZ)*(z-Festivals.EffigyZ));
    // How far (degrees) n's facing is from looking straight at the effigy.
    static double OffEffigy(NpcState n){double to=Math.Atan2(Festivals.EffigyX-n.X,Festivals.EffigyZ-n.Z)*180/Math.PI;return Math.Abs(((n.Yaw-to)%360+540)%360-180);}

    static void TheEffigyBurnsForTheLastThreeMinutesOfNightTwo()
    {
        var st=Start(6,3,1).State;
        st.ElapsedSeconds=st.DurationSeconds-180.1;Check(!FestivalSimulation.Burning(st),"3:00.1 before the end of Night 2 the effigy still stands");
        st.ElapsedSeconds=st.DurationSeconds-180;Check(FestivalSimulation.Burning(st),"from three minutes before the end it burns");
        st.ElapsedSeconds=st.DurationSeconds-.1;Check(FestivalSimulation.Burning(st),"until the end");
        Check(FestivalSimulation.InBurnCrowd(st,Festivals.EffigyX+11.9f,Festivals.EffigyZ)&&FestivalSimulation.InBurnCrowd(st,Festivals.EffigyX,Festivals.EffigyZ-11.9f),"11.9 m from the burning effigy is in the crush");
        Check(!FestivalSimulation.InBurnCrowd(st,Festivals.EffigyX+12.1f,Festivals.EffigyZ),"12.1 m is not");
        st.ElapsedSeconds=st.DurationSeconds-181;Check(!FestivalSimulation.InBurnCrowd(st,Festivals.EffigyX,Festivals.EffigyZ),"and before the burn there is no crush");
        for(int level=0;level<Festivals.LevelCount-1;level++)
        {
            var other=Start(6,level,1).State;other.ElapsedSeconds=other.DurationSeconds-1;
            Check(!FestivalSimulation.Burning(other),"no burn at the end of "+Festivals.Level(1,level,0).Name);
        }
        var palm=Start(6,3,0).State;palm.ElapsedSeconds=palm.DurationSeconds-1;
        Check(!FestivalSimulation.Burning(palm)&&!FestivalSimulation.InBurnCrowd(palm,Festivals.EffigyX,Festivals.EffigyZ),"Palm Mirage has no effigy");
    }

    // A festivalgoer 6 m behind p0 is swarming them when p0 climbs aboard art car 0.
    static FestivalSimulation Swarmed(out NpcState n)
    {
        var s=Cars();var p=s.Player("p0");s.State.SimulationSeconds=30;
        n=new NpcState{Id="wook",X=p.X,Z=p.Z-6};s.State.Npcs.Add(n);
        n.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=95,LastSeenSeconds=s.State.SimulationSeconds,AccusationSeconds=s.State.SimulationSeconds-10});
        s.Tick(.1);Check(n.Mode=="Swarming"&&n.TargetId=="p0","setup: a festivalgoer is swarming p0, got "+n.Mode);
        Check(Act(s,"p0","RideCar").Accepted,"setup: p0 climbs aboard art car 0");
        return s;
    }
    static double Apart(NpcState n,PlayerState p)=>Math.Sqrt((n.X-p.X)*(n.X-p.X)+(n.Z-p.Z)*(n.Z-p.Z));
    // How far (degrees) n's facing is from looking straight at p.
    static double Off(NpcState n,PlayerState p){double to=Math.Atan2(p.X-n.X,p.Z-n.Z)*180/Math.PI;return Math.Abs(((n.Yaw-to)%360+540)%360-180);}

    // PLAYA-4: the swarm can't see the rider up there, but for ArtCarTrailSeconds after the boarding it trails the car. It
    // walks after it, a little faster than the car rolls, holds off ArtCarReach from the deck, keeps facing the rider and stays
    // swarming. Then it loses them: it stops where it is and cools off like a festivalgoer left far behind. It never lands a blow.
    static void ASwarmTrailsARiderThenGivesUp()
    {
        var s=Swarmed(out var n);var p=s.Player("p0");double boarded=s.State.SimulationSeconds,start=Apart(n,p),closest=double.MaxValue,stride=0;
        while(s.State.SimulationSeconds+.1<boarded+Festivals.ArtCarTrailSeconds-.01)
        {
            float x=n.X,z=n.Z;s.Tick(.1);closest=Math.Min(closest,Apart(n,p));stride=Math.Max(stride,Math.Sqrt((n.X-x)*(n.X-x)+(n.Z-z)*(n.Z-z)));
            string at=(s.State.SimulationSeconds-boarded).ToString("0.0")+" s after the boarding";
            Check(n.Mode=="Swarming"&&Heat(s,"wook","p0")>=70,at+", it is still swarming: "+n.Mode+" at "+Heat(s,"wook","p0"));
            Check(Off(n,p)<1,at+", it faces the rider, not "+Off(n,p)+" degrees off");
            Check(p.Life=="Alive"&&p.Health==100&&FestivalSimulation.ArtCarOf(s.State,"p0")==0,at+", p0 rides car 0 unharmed: "+p.Life+", health "+p.Health);
        }
        Check(Apart(n,p)<start-1&&Apart(n,p)<=Festivals.ArtCarReach+.25,"it closes on the rider, from "+start+" m to "+Apart(n,p)+" m");
        Check(closest>=Festivals.ArtCarReach-.1,"but holds off the deck: it came within "+closest+" m");
        Check(stride<=1.7*.1+1e-3,"at a walk of 1.7 m/s, not "+stride*10+" m/s");
        s.Tick(.3);float stoodX=n.X,stoodZ=n.Z;s.Tick(.1);
        Check(n.X==stoodX&&n.Z==stoodZ,"once the "+Festivals.ArtCarTrailSeconds+" s are up, it stops where it is");
        double gaveUp=-1;
        for(int k=0;k<50&&gaveUp<0;k++){s.Tick(.1);if(n.Mode!="Swarming"&&Heat(s,"wook","p0")<70)gaveUp=s.State.SimulationSeconds-boarded;}
        Check(gaveUp>0,"and within 5 s more it gives up the chase: "+n.Mode+" at "+Heat(s,"wook","p0"));
        while(s.State.SimulationSeconds+.05<boarded+30){s.Tick(.1);Check(p.Life=="Alive"&&p.Health==100&&FestivalSimulation.ArtCarOf(s.State,"p0")==0,"p0 rides car 0 unharmed for 30 s: "+p.Life+", health "+p.Health);}
    }
    // A rider who hops off while the swarm still trails them is just next to an angry swarm again, and the chase picks them up.
    static void ARiderWhoHopsOffMidTrailIsSwarmedAgain()
    {
        var s=Swarmed(out var n);var p=s.Player("p0");s.Tick(4);
        Check(Act(s,"p0","Cancel").Accepted&&FestivalSimulation.ArtCarOf(s.State,"p0")<0,"setup: p0 hops off 4 s after boarding");
        Check(n.Mode=="Swarming"&&Heat(s,"wook","p0")>=70,"the swarm trailing the car is still swarming: "+n.Mode+" at "+Heat(s,"wook","p0"));
        double was=Apart(n,p),closest=was;
        for(int k=0;k<20;k++){s.Tick(.1);closest=Math.Min(closest,Apart(n,p));}
        Check(n.Mode=="Swarming"&&closest<=1.5,"and closes back in on p0, from "+was+" m to "+closest+" m ("+n.Mode+")");
    }
    static void TheCrowdGathersAtTheBurn()
    {
        var s=Start(6,3,1);var st=s.State;var crowd=st.Npcs.FindAll(n=>n.Kind=="Wook");
        st.ElapsedSeconds=st.DurationSeconds-Festivals.BurnSeconds-2;
        var before=crowd.ConvertAll(n=>(n.X,n.Z));s.Tick(1);
        Check(crowd.TrueForAll(n=>(n.X,n.Z)==before[crowd.IndexOf(n)]),"before the burn the crowd stays where it is");
        // The festivalgoer farthest from the effigy has someone in its sights: p0, 3 m in front of it.
        NpcState busy=crowd[0];foreach(var n in crowd)if(FromEffigy(n.X,n.Z)>FromEffigy(busy.X,busy.Z))busy=n;
        var p=s.Player("p0");double yaw=busy.Yaw*Math.PI/180;p.X=busy.X+(float)(3*Math.Sin(yaw));p.Z=busy.Z+(float)(3*Math.Cos(yaw));
        busy.Observers.Clear();busy.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=50,LastSeenSeconds=st.SimulationSeconds});
        s.Tick(1.5);var walking=crowd.ConvertAll(n=>(n.X,n.Z));s.Tick(.1);
        Check(Festivals.BurnWalkSpeed>0&&Festivals.BurnWalkSpeed<=1.7,"the crowd strolls over, no faster than a festivalgoer going after someone, not "+Festivals.BurnWalkSpeed+" m/s");
        foreach(var n in crowd)
        {
            double step=Math.Sqrt((n.X-walking[crowd.IndexOf(n)].X)*(n.X-walking[crowd.IndexOf(n)].X)+(n.Z-walking[crowd.IndexOf(n)].Z)*(n.Z-walking[crowd.IndexOf(n)].Z));
            Check(n==busy||step>0&&step<=Festivals.BurnWalkSpeed*.1+1e-4,n.Id+" strolls toward the effigy: "+step+" m in a tenth of a second");
        }
        s.Tick(59.4);
        Check(FestivalSimulation.Burning(st),"setup: the effigy has been burning a minute");
        foreach(var n in crowd)
        {
            if(n==busy)continue;
            Check(Math.Abs(FromEffigy(n.X,n.Z)-Festivals.BurnRingRadius)<.05,n.Id+" has gathered in the ring "+Festivals.BurnRingRadius+" m round the effigy, got "+FromEffigy(n.X,n.Z)+" m at "+n.X+","+n.Z);
            Check(OffEffigy(n)<1,n.Id+" watches the effigy burn, "+OffEffigy(n)+" degrees off");
            foreach(var m in crowd)if(m!=n&&m!=busy)Check(Math.Sqrt((m.X-n.X)*(m.X-n.X)+(m.Z-n.Z)*(m.Z-n.Z))>1,n.Id+" and "+m.Id+" each have their own place in the ring: "+n.X+","+n.Z+" / "+m.X+","+m.Z);
        }
        Check(busy.Mode=="Questioning"&&Math.Sqrt((busy.X-p.X)*(busy.X-p.X)+(busy.Z-p.Z)*(busy.Z-p.Z))<2&&FromEffigy(busy.X,busy.Z)>10,"a festivalgoer questioning someone stays on them");
        Check(st.Npcs.FindAll(n=>n.Kind=="Cop").TrueForAll(n=>FromEffigy(n.X,n.Z)>15),"cops keep to their patrol");
        var palm=Start(6,3,0);var palmCrowd=palm.State.Npcs.FindAll(n=>n.Kind=="Wook");palm.State.ElapsedSeconds=palm.State.DurationSeconds-Festivals.BurnSeconds+1;
        var still=palmCrowd.ConvertAll(n=>(n.X,n.Z));palm.Tick(30);
        Check(palmCrowd.TrueForAll(n=>(n.X,n.Z)==still[palmCrowd.IndexOf(n)]),"Palm Mirage's crowd has no effigy to gather at");
        var eyeing=Burn(60);var w=new NpcState{Id="wook",X=20,Z=-20};eyeing.State.Npcs.Add(w);
        w.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=40,LastSeenSeconds=eyeing.State.SimulationSeconds});eyeing.Tick(.1);
        Check(w.Mode=="Watching"&&FromEffigy(w.X,w.Z)<FromEffigy(20,-20)-.1,"a festivalgoer only keeping an eye on someone heads over too");
    }

    // p0, the tripper, stops to check a festivalgoer out on the lawn just as the effigy catches.
    static void AFestivalgoerFinishesAChatBeforeHeadingOver()
    {
        var s=Burn(Festivals.BurnSeconds);var p=s.Player("p0");p.X=20;p.Z=-20;var n=new NpcState{Id="wook",Role="Buyer",X=21,Z=-20};s.State.Npcs.Add(n);
        s.State.TripperId="p0";s.State.Visions.Add(new VisionState{Id="v",Kind="Buyer",NpcId="wook"});
        Check(Act(s,"p0","ConfirmChat","wook").Accepted,"setup: p0 starts a chat");
        s.Tick(FestivalSimulation.ConfirmChatSeconds-.5);
        Check(FestivalSimulation.Burning(s.State)&&n.X==21&&n.Z==-20&&p.InteractionId!="","a festivalgoer mid-chat stays to finish it, at ("+n.X+", "+n.Z+")");
        Check(Act(s,p.Id,"EndChat",p.InteractionId).Accepted,"explicitly finish before heading over");s.Tick(1);
        Check(p.InteractionId==""&&s.State.Visions[0].Confirmed,"setup: the chat is done");
        Check(FromEffigy(n.X,n.Z)<FromEffigy(21,-20)-.1,"then it heads for the burn");
    }

    // A hand-built Night 2, `left` seconds before the end, with nobody from the crowd around.
    static FestivalSimulation Burn(double left,int festival=1)
    {
        var s=new FestivalSimulation(3);s.AddPlayer("p0","P0");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=3;s.State.Phase="Playing";
        s.State.DurationSeconds=Festivals.NightSeconds;s.State.ElapsedSeconds=s.State.DurationSeconds-left;s.State.Npcs.Clear();
        return s;
    }
    // p0, `metres` north of the effigy, tries to go `step` metres further north in a tenth of a second: how far they get, -1 if refused.
    static double Walk(FestivalSimulation s,double metres,double step,string life="Alive")
    {
        var p=s.Player("p0");p.Life=life;p.X=Festivals.EffigyX;p.Z=Festivals.EffigyZ+(float)metres;float z=p.Z;
        return s.TryMove("p0",p.X,z+(float)step,0,.1)?p.Z-z:-1;
    }
    static bool Near(double a,double b)=>Math.Abs(a-b)<1e-4;

    static void TheCrushSlowsEveryStep()
    {
        double crushed=Walk(Burn(60),11.5,.3);
        Check(Near(crushed,.21),"in the burn's crush a 0.3 m step covers 0.21 m, got "+crushed);
        Check(Near(Walk(Burn(60),11.5,.6),.42),"a sprint too, got "+Walk(Burn(60),11.5,.6));
        Check(Walk(Burn(60),11.5,.65)<0,"and the usual speed limit still holds");
        Check(Near(Walk(Burn(60),12.5,.3),.3),"outside the crush a step is a step, got "+Walk(Burn(60),12.5,.3));
        Check(Near(Walk(Burn(181),11.5,.3),.3),"so it is before the burn, got "+Walk(Burn(181),11.5,.3));
        Check(Near(Walk(Burn(60,0),11.5,.3),.3),"and at Palm Mirage, got "+Walk(Burn(60,0),11.5,.3));
        Check(Near(Walk(Burn(60),11.5,.3,"Spirit"),.3),"spirits float through the crush, got "+Walk(Burn(60),11.5,.3,"Spirit"));
    }

    // A festivalgoer 5 m from the effigy looks straight at it, with its back to p0, who stands `metres` from the effigy right
    // behind it. It has had 20 suspicion of p0 since last seeing them 10 s ago: how much does that cool in a second?
    static double Cooled(FestivalSimulation s,double metres)
    {
        var n=s.State.Npcs.Find(x=>x.Kind=="Wook");var p=s.Player("p0");
        double d=FromEffigy(n.X,n.Z);Check(Math.Abs(d-5)<.05&&OffEffigy(n)<1,"setup: "+n.Id+" is 5 m from the effigy, looking at it");
        p.X=Festivals.EffigyX+(float)((n.X-Festivals.EffigyX)/d*metres);p.Z=Festivals.EffigyZ+(float)((n.Z-Festivals.EffigyZ)/d*metres);
        n.Observers.Clear();var o=new ObserverState{PlayerId="p0",Suspicion=20,LastSeenSeconds=s.State.SimulationSeconds-10};n.Observers.Add(o);
        s.Tick(1);return 20-o.Suspicion;
    }
    // At the burn the festivalgoer walks there itself; anywhere else it is put there.
    static FestivalSimulation Gathered(double left)
    {
        var s=Burn(left+40);s.State.Npcs.Add(new NpcState{Id="wook",X=10,Z=-20});var p=s.Player("p0");p.X=-30;p.Z=-30;
        s.Tick(40);return s;
    }
    static FestivalSimulation Placed(double left,int festival=1)
    {
        var s=Burn(left,festival);s.State.Npcs.Add(new NpcState{Id="wook",X=Festivals.EffigyX,Z=Festivals.EffigyZ+5,Yaw=180});return s;
    }

    // A festivalgoer questioning p0, who stands `metres` north of the effigy, is 32 m behind them and last saw them 10 s ago:
    // how much suspicion a second does it lose, out of sight and falling behind?
    static double Chased(double metres,int festival=1)
    {
        var s=Burn(60,festival);var p=s.Player("p0");p.X=Festivals.EffigyX;p.Z=Festivals.EffigyZ+(float)metres;
        var n=new NpcState{Id="wook",X=p.X,Z=p.Z-32};s.State.Npcs.Add(n);
        var o=new ObserverState{PlayerId="p0",Suspicion=65,LastSeenSeconds=s.State.SimulationSeconds-10};n.Observers.Add(o);
        s.Tick(.5);Check(n.Mode=="Questioning","setup: "+n.Id+" is still after p0");return (65-o.Suspicion)*2;
    }

    // The whole crowd stands in one ring round the burning effigy, 10 m across. A festivalgoer who turns on p0 there sets off
    // its neighbours, but the fire's roar keeps the shout from carrying round the ring: no more festivalgoers join in than the
    // most one accusation reaches in the day's crowd (the accuser and six), where 24 did.
    static void AnAccusationInTheCrushReachesOnlyNeighbours()
    {
        var s=Start(5,3,1,1);var st=s.State;st.ElapsedSeconds=st.DurationSeconds-Festivals.BurnSeconds+10;s.Tick(60);
        var crowd=st.Npcs.FindAll(n=>n.Kind=="Wook");var accuser=crowd[0];
        Check(FestivalSimulation.InBurnCrowd(st,accuser.X,accuser.Z)&&crowd.Count>=20,"setup: "+crowd.Count+" festivalgoers have gathered at the burn");
        var p=s.Player("p0");p.X=Festivals.EffigyX;p.Z=Festivals.EffigyZ-9;
        accuser.Observers.RemoveAll(o=>o.PlayerId=="p0");accuser.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=70,LastSeenSeconds=st.SimulationSeconds});
        s.Tick(.1);
        int accusing=crowd.FindAll(n=>n.Observers.Exists(o=>o.PlayerId=="p0"&&o.AccusationSeconds>=0)).Count;
        Check(accusing>=2&&accusing<=7,"one accusation in the crush reaches the accuser's neighbours, 2-7 festivalgoers, got "+accusing+" of "+crowd.Count);
    }
    static void SuspicionCoolsFasterInTheCrush()
    {
        double plain=Cooled(Placed(60),13);
        Check(Near(plain,2),"outside the crush, suspicion of someone out of sight cools 2 a second, got "+plain);
        Check(Near(Cooled(Gathered(60),8),2*plain),"in the burn's crush it cools twice as fast, got "+Cooled(Gathered(60),8));
        Check(Near(Cooled(Gathered(60),13),plain),"just outside it, as usual, got "+Cooled(Gathered(60),13));
        Check(Near(Cooled(Placed(200),8),plain),"before the burn, as usual, got "+Cooled(Placed(200),8));
        Check(Near(Cooled(Placed(60,0),8),plain),"and at Palm Mirage, got "+Cooled(Placed(60,0),8));
        double chase=Chased(13);
        Check(Near(chase,10),"outside the crush, a festivalgoer falling 30 m behind loses 10 a second, got "+chase);
        Check(Near(Chased(8),2*chase),"it loses someone in the crush twice as fast, got "+Chased(8));
        Check(Near(Chased(8,0),chase),"but not at Palm Mirage, got "+Chased(8,0));
    }
    static void BurnWalkersKeepGapAcrossAnotherWalker()
    {
        var s=Burn(120);s.Player("p0").X=-30;s.Player("p0").Z=-30;
        var walking=new NpcState{Id="burn walker",X=Festivals.EffigyX,Z=Festivals.EffigyZ+1};
        var held=new NpcState{Id="distracted neighbour",X=Festivals.EffigyX,Z=Festivals.EffigyZ+1.8f,DistractedUntil=1000};s.State.Npcs.Add(walking);s.State.Npcs.Add(held);
        int navigation=0;s.Navigate=(x,z,tx,tz,step)=>{navigation++;double d=Math.Sqrt((tx-x)*(tx-x)+(tz-z)*(tz-z)),travel=Math.Min(step,d);return d<=1e-6?new WorldPoint(x,z):new WorldPoint(x+(float)((tx-x)*travel/d),z+(float)((tz-z)*travel/d));};
        for(int k=0;k<80;k++){float x=walking.X,z=walking.Z;s.Tick(.1);Check(Dist(new WorldPoint(walking.X,walking.Z),new WorldPoint(held.X,held.Z))>=.7-1e-5,"burn gathering cannot enter another walker at step "+k);Check(Dist(new WorldPoint(x,z),new WorldPoint(walking.X,walking.Z))<=Festivals.BurnWalkSpeed*.1+1e-4,"burn step stays speed bounded");}
        Check(navigation>0&&Math.Abs(FromEffigy(walking.X,walking.Z)-Festivals.BurnRingRadius)<.05,"burn gathering reaches the ring by approved separated steps");
    }

}
