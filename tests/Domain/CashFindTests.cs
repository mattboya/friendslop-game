using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Festival.Core;

public static class CashFindTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool condition,string message){if(!condition)throw new Exception("CashFind: "+message);}
    static CommandResult Act(FestivalSimulation game,string player,string kind,string target="")=>game.Execute(player,new GameCommand{Id="find"+(sequence++),Kind=kind,TargetId=target});
    static FestivalSimulation Play(int seed=12,int secret=98765,int festival=0,int level=0)
    {
        var game=new FestivalSimulation(seed);game.DealSecret=()=>secret;game.State.UnlockedFestivalCount=Festivals.Count;game.State.FestivalIndex=festival;game.State.LevelIndex=level;
        foreach(var id in new[]{"host","friend"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;Check(Act(game,id,"Ready").Accepted,"setup: ready");}
        game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
        foreach(var p in game.State.Players)Check(Act(game,p.Id,"MapReady").Accepted,"setup: map loads");
        Check(game.State.Phase=="Playing","setup: level plays");game.State.Npcs.Clear();return game;
    }
    static FestivalSimulation Copy(FestivalSimulation game){var copy=new FestivalSimulation();copy.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(game.State,Json),Json));return copy;}
    static List<DropState> Finds(FestivalSimulation game)=>game.State.Drops.FindAll(d=>d.CashFind);
    static string Trace(FestivalSimulation game,double until)
    {
        var trace=new List<string>();double last=game.State.ElapsedSeconds;bool first=true;
        while(game.State.ElapsedSeconds<until-1e-6)
        {
            game.Tick(.1);Check(game.State.Phase=="Playing","setup: the schedule fixture remains alive");
            foreach(var d in Finds(game).ToArray())
            {
                double gap=game.State.ElapsedSeconds-last;Check(first&&last>0||gap>=45-1e-6&&gap<=90+.1,"uncapped arrivals are 45-90 seconds apart, got "+gap);first=false;
                Check(d.Count>=2&&d.Count<=5&&d.ItemId=="cash"&&d.LandsAt==0,"find amount and immediate landing");
                Check(Festivals.CashFindSpots[game.State.FestivalIndex].Any(p=>p.X==d.X&&p.Z==d.Z),"find lies on an authored spot");
                trace.Add(game.State.ElapsedSeconds.ToString("F1")+":"+d.Count+":"+d.X+":"+d.Z);last=game.State.ElapsedSeconds;
                var p=game.Player("host");p.X=d.X;p.Z=d.Z;Check(Act(game,p.Id,"Pickup",d.Id).Accepted,"pick up actual scheduled cash");
            }
            Check(Finds(game).Count<=2,"at most two random finds");
        }
        return string.Join("|",trace);
    }
    public static void Run()
    {
        var failures=new List<string>();foreach(var test in new Action[]{FirstFindAppearsByNinetySeconds,LevelEndRemovesFinds,MalformedCashSnapshotsRejectAtomically,TenMinuteNightUsesTheActualLevelDuration,SecretsDeterminismAndRestoreContinueTheSchedule,CapConsumesAttemptsWithoutBacklogAndIgnoresDeathCash,BoothPauseAndLegacyInitializationKeepIndependentIntervals,PickupIsPersonalAtomicAndRequiresLivingFreeNearbyPlay})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }
    static void FirstFindAppearsByNinetySeconds(){var game=Play();game.Tick(44);Check(Finds(game).Count==0,"no find before 45 seconds");game.Tick(46);Check(Finds(game).Count>0,"a $2-$5 cash find appears by 90 seconds of play");}
    static void LevelEndRemovesFinds()
    {
        foreach(bool success in new[]{false,true})
        {
            var game=Play();game.Tick(60);game.Tick(40);Check(Finds(game).Count>0,"setup: actual find exists");
            if(success)game.State.LevelSales=FestivalSimulation.DayQuota(game.State);
            while(game.State.Phase=="Playing")game.Tick(60);
            Check(Finds(game).Count==0,"random finds vanish on either level result");
            Check(Act(game,"host","Reset").Accepted&&game.State.NextCashFindAt==0&&game.State.CashFindIndex==0,"new camp resets its host schedule");
        }
    }
    static void MalformedCashSnapshotsRejectAtomically()
    {
        var original=Play();original.Tick(60);original.Tick(40);
        foreach(var mutate in new Action<RoundState>[] {s=>s.NextCashFindAt=double.NaN,s=>s.NextCashFindAt=double.PositiveInfinity,s=>s.NextCashFindAt=-1,s=>s.NextCashFindAt=50,s=>s.CashFindIndex=-1,s=>s.CashFindIndex=int.MaxValue,s=>{s.NextCashFindAt=0;s.CashFindIndex=1;},s=>s.Drops.Find(d=>d.CashFind).Count=6,s=>s.Drops.Find(d=>d.CashFind).Count=1,s=>s.Drops.Find(d=>d.CashFind).ItemId="poi",s=>s.Drops.Find(d=>d.CashFind).X=0,s=>s.Drops.Find(d=>d.CashFind).OwnerId="host",s=>s.Drops.Find(d=>d.CashFind).LandsAt=1})
        {
            var bad=Copy(original).State;mutate(bad);var target=new FestivalSimulation();var before=target.State;bool rejected=false;
            try{target.Restore(bad);}catch(ArgumentException){rejected=true;}
            Check(rejected&&ReferenceEquals(before,target.State),"invalid schedule/tagged cash rejected before replacement");
        }
    }
    static void TenMinuteNightUsesTheActualLevelDuration()
    {
        var game=Play(level:1);Check(game.State.DurationSeconds==600,"Night 1 legitimately runs ten minutes");
        string trace=Trace(game,599.9);Check(trace.Split('|').Length>=6,"at least six windfalls across the real ten-minute night");
        game.Tick(.2);Check(game.State.Phase=="Results"&&Finds(game).Count==0,"actual night deadline clears its finds");
    }
    static void SecretsDeterminismAndRestoreContinueTheSchedule()
    {
        string a=Trace(Play(),400),b=Trace(Play(),400),c=Trace(Play(secret:11111),400);Check(a==b&&a!=c,"same seeds replay, a different host secret changes finds");
        var game=Play();Trace(game,200);Check(game.State.CashFindIndex>=2,"setup: save after consumed attempts");var restored=Copy(game);
        Check(Trace(game,400)==Trace(restored,400),"save/restore preserves future amount, spot and deadline");
    }
    static void CapConsumesAttemptsWithoutBacklogAndIgnoresDeathCash()
    {
        var game=Play();game.State.Drops.Add(new DropState{Id="death",ItemId="cash",Count=200,X=0,Z=0});game.Tick(60);game.Tick(60);game.Tick(60);game.Tick(60);
        Check(Finds(game).Count==2&&game.State.CashFindIndex>=3,"death cash does not consume cap, capped attempts advance");
        double next=game.State.NextCashFindAt;var victim=Finds(game)[0];var p=game.Player("host");p.X=victim.X;p.Z=victim.Z;Check(Act(game,p.Id,"Pickup",victim.Id).Accepted,"claim one while capped");
        game.Tick(.1);Check(Finds(game).Count==1&&game.State.NextCashFindAt==next,"opening capacity emits no backlog");
        while(game.State.ElapsedSeconds+1e-6<next)game.Tick(.1);Check(Finds(game).Count==2,"next independent scheduled attempt fills the space");
    }
    static void BoothPauseAndLegacyInitializationKeepIndependentIntervals()
    {
        var game=Play();game.Tick(20);double next=game.State.NextCashFindAt;var at=FestivalSimulation.BoothAt(game.State);foreach(var p in game.State.Players){p.X=at.X;p.Z=at.Z;}
        game.Tick(3.5);Check(game.State.Phase==FestivalSimulation.MinigamePhase,"setup: actual paused booth");double elapsed=game.State.ElapsedSeconds;game.State.Booth.Kind="Endless";game.Tick(60);
        Check(game.State.ElapsedSeconds==elapsed&&game.State.NextCashFindAt==next&&Finds(game).Count==0,"booth wall time consumes no schedule interval");
        game.ScoreMinigame(0);game.Tick(5);Check(game.State.NextCashFindAt==next,"resume never translates elapsed deadlines");
        var old=Play();old.Tick(60);old.Tick(40);var json=JsonSerializer.Serialize(old.State,Json);using var document=JsonDocument.Parse(json);
        var fields=new Dictionary<string,JsonElement>();foreach(var field in document.RootElement.EnumerateObject())if(field.Name!="NextCashFindAt"&&field.Name!="CashFindIndex"&&field.Name!="Drops")fields[field.Name]=field.Value;
        var legacy=JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(fields),Json);var recovered=new FestivalSimulation();recovered.Restore(legacy);recovered.Tick(.1);
        Check(recovered.State.NextCashFindAt>=recovered.State.ElapsedSeconds+45-1e-6&&recovered.State.NextCashFindAt<=recovered.State.ElapsedSeconds+90+1e-6&&Finds(recovered).Count==0,"missing legacy schedule lazily arms a fresh interval");
    }
    internal static void PickupIsPersonalAtomicAndRequiresLivingFreeNearbyPlay()
    {
        var game=Play();game.Tick(60);game.Tick(40);var d=Finds(game)[0];var p=game.Player("host");var friend=game.Player("friend");int cash=p.Cash,other=friend.Cash,sales=game.State.LevelSales,stash=game.State.StashCash;
        p.X=friend.X=d.X;p.Z=friend.Z=d.Z;p.Inventory.AddRange(new[]{new ItemStack{ItemId="poi",Count=1},new ItemStack{ItemId="confetti",Count=1},new ItemStack{ItemId="stock_lsd",Count=1}});
        var claim=new GameCommand{Id="cash-race",Kind="Pickup",TargetId=d.Id};Check(game.Execute(p.Id,claim).Accepted,"full inventory still collects cash");Check(game.Execute(p.Id,claim).Accepted,"duplicate receives original receipt");
        Check(!Act(game,friend.Id,"Pickup",d.Id).Accepted&&!Act(game,p.Id,"Pickup",d.Id).Accepted,"stale and competing claims fail");
        Check(p.Cash==cash+d.Count&&friend.Cash==other&&p.Inventory.Count==3&&game.State.LevelSales==sales&&game.State.StashCash==stash,"cash credits once, only the winner's pocket");
        foreach(var reject in new Action[]{()=>p.Life="Spirit",()=>p.Life="Downed",()=>p.Life="Detained",()=>p.InteractionId="busy",()=>p.X=0,()=>game.State.Phase="Shopping"})
        {
            game.State.Drops.Add(new DropState{Id="refused"+(sequence++),ItemId="cash",Count=3,X=35,Z=23,CashFind=true});var drop=game.State.Drops.Last();p.Life="Alive";p.InteractionId="";p.X=35;p.Z=23;game.State.Phase="Playing";reject();
            Check(!Act(game,p.Id,"Pickup",drop.Id).Accepted&&game.State.Drops.Contains(drop),"nonliving, busy, distant and non-playing claims preserve the cash");
        }
    }
}

public static class CashFindPickupTests { public static void Run()=>CashFindTests.PickupIsPersonalAtomicAndRequiresLivingFreeNearbyPlay(); }
