using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// TRIP-1: at camp, a people spinner picks the tripper and a dose spinner picks how hard they trip.
public static class TripperTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Tripper: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string item="",string target="")=>s.Execute(id,new GameCommand{Id="trip"+(sequence++),Kind=kind,ItemId=item,TargetId=target});
    static FestivalSimulation Crew(int seed,int size){var s=new FestivalSimulation(seed);for(int i=0;i<size;i++)s.AddPlayer("p"+i,"P"+i);return s;}
    static int Dose(PlayerState p)=>p.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect)?.Intensity??0;
    static PlayerState Tripper(FestivalSimulation s)=>s.Player(s.State.TripperId);
    // The connected crew readies at the trailhead and the 5 s countdown runs out.
    static void Spin(FestivalSimulation s){foreach(var p in s.State.Players)if(p.Connected){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,p.Id+" readies");}s.Tick(5.2);Check(s.State.Phase=="Spinning","the ready countdown hands over to the spinners, not straight to Loading");}
    // The wheels land, everyone loads the map, and the level starts with an empty crowd.
    static void Land(FestivalSimulation s)=>s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);
    static void Play(FestivalSimulation s){Land(s);foreach(var p in s.State.Players)if(p.Connected)Act(s,p.Id,"MapReady");Check(s.State.Phase=="Playing","the crew reaches the festival");s.State.Npcs.Clear();}
    // The level is won; the host brings everyone back and the next level's camp opens.
    static void Win(FestivalSimulation s){s.State.Phase="Results";s.State.Result="Success";Check(Act(s,s.State.HostPlayerId,"Reset").Accepted,"the host brings the crew back");s.State.Phase="Shopping";}
    static void Die(FestivalSimulation s,PlayerState p){p.Life="Downed";p.DownedRemaining=.1;s.Tick(.3);Check(p.Life=="Spirit","setup: "+p.Id+" dies");}

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{SpinRunsAtCampBeforeLoading,EveryoneTripsOnceAWeekend,SmallCrewsAlternate,DoseSpinnerWeights,SoloAlwaysTrips,NightTwoDosesEveryone,SpinResultSurvivesSnapshots,
            DoseSlowsTheTripperAllLevel,RepickWhenTheTripperDiesOrLeaves,ClueTastingIsGone,NightTwoCrewFollowsTheTrail,DoseLeavesBothConsumableSlots,MedicalCannotCureTheDose,GuidanceFollowsTheSpin})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" tripper test(s) failed:\n"+string.Join("\n",failures));
    }

    static void SpinRunsAtCampBeforeLoading()
    {
        var s=Crew(11,2);Spin(s);
        var tripper=Tripper(s);Check(tripper!=null,"the people spinner lands on a crew member");
        var friend=s.State.Players.Find(p=>p!=tripper);double left=s.State.SpinEndsAt-s.State.SimulationSeconds;
        Check(left>12.1&&left<=FestivalSimulation.SpinSeconds,"each wheel spins 4 s and rests .6 s on its result, then the take and the reaction: at least 12 s");
        Check(s.State.Doses.Count==1&&s.State.Doses[0].PlayerId==tripper.Id&&s.State.Doses[0].Dose==Dose(tripper)&&Dose(tripper)>=1&&Dose(tripper)<=4,"the tripper takes the 1-4 doses the dose spinner landed on");
        Check(Dose(friend)==0,"everyone else stays sober on a normal level");
        Check(!s.TryMove(friend.Id,friend.X+.1f,friend.Z,0,.1),"the crew stands still at camp while the wheels spin");
        Check(!Act(s,friend.Id,"MapReady").Accepted,"nobody loads the festival before the wheels land");
        s.Tick(left-.2);Check(s.State.Phase=="Spinning","still spinning just before the wheels land");
        s.Tick(.3);Check(s.State.Phase=="Loading","then the crew heads to the festival");
        var started=Crew(12,1);var host=started.Player("p0");host.X=0;host.Z=19;Act(started,"p0","Ready");
        Check(Act(started,"p0","Start").Accepted&&started.State.Phase=="Spinning"&&started.State.TripperId=="p0","the host's Start spins too");
    }

    static void EveryoneTripsOnceAWeekend()
    {
        FestivalSimulation s=null;
        for(int seed=0;seed<20;seed++)
        {
            s=Crew(seed,4);var turns=new List<string>();
            for(int level=0;level<4;level++){Spin(s);turns.Add(s.State.TripperId);Win(s);}
            Check(s.State.FestivalIndex==1&&s.State.LevelIndex==0,"setup: the weekend is over");
            Check(string.Join(",",turns.OrderBy(id=>id))=="p0,p1,p2,p3","each of four friends trips exactly once a weekend, got "+string.Join(",",turns)+" (seed "+seed+")");
        }
        Check(s.State.TripperBag.Count==4,"a new weekend puts everyone back in the bag");
        Spin(s);s.State.Phase="Results";s.State.Result="Time expired";Act(s,"p0","Reset");s.State.Phase="Shopping";
        Check(s.State.LevelIndex==0&&s.State.TripperBag.Count==4,"losing restarts the weekend with everyone due again");
        // The crew changes at camp: a newcomer is due a turn and a friend who left is skipped.
        Spin(s);var picked=new List<string>{s.State.TripperId};Win(s);
        s.AddPlayer("p4","P4");var leaver=s.State.Players.Find(p=>p.Id!=picked[0]&&p.Id!="p4"&&p.Id!=s.State.HostPlayerId);s.Disconnect(leaver.Id);
        for(int level=1;level<4;level++){Spin(s);picked.Add(s.State.TripperId);Win(s);}
        Check(picked.Contains("p4")&&!picked.Contains(leaver.Id)&&picked.Distinct().Count()==4,"a newcomer gets a turn, a leaver is skipped, nobody repeats: "+string.Join(",",picked));
    }

    // Two friends over four levels: the bag refills once empty, so nobody trips twice before the other has.
    static void SmallCrewsAlternate()
    {
        for(int seed=0;seed<20;seed++)
        {
            var s=Crew(seed,2);var turns=new List<string>();
            for(int level=0;level<4;level++){Spin(s);turns.Add(s.State.TripperId);Win(s);}
            Check(turns[0]!=turns[1]&&turns[2]!=turns[3],"two friends take turns about, got "+string.Join(",",turns)+" (seed "+seed+")");
        }
    }

    static void DoseSpinnerWeights()
    {
        const int spins=10000;var counts=new int[5];
        for(int seed=0;seed<spins;seed++)
        {
            var s=Crew(seed,1);var solo=s.Player("p0");solo.X=0;solo.Z=19;Act(s,"p0","Ready");Act(s,"p0","Start");
            Check(s.State.TripperId=="p0"&&Dose(solo)>=1&&Dose(solo)<=4,"seed "+seed+" spins a dose for the solo tripper");counts[Dose(solo)]++;
        }
        var weights=new[]{0,40,30,22,8};
        for(int dose=1;dose<=4;dose++){double share=100.0*counts[dose]/spins;Check(Math.Abs(share-weights[dose])<=1.5,"dose "+dose+" landed on "+share+"% of 10k spins; the slice is "+weights[dose]+"%");}
    }

    static void SoloAlwaysTrips()
    {
        var s=Crew(41,1);
        for(int level=0;level<4;level++){Spin(s);Check(s.State.TripperId=="p0"&&Dose(s.Player("p0"))>=1,"solo practice: always the tripper (level "+level+")");Win(s);}
    }

    static void NightTwoDosesEveryone()
    {
        for(int seed=0;seed<200;seed++)
        {
            // Night 2 of a four-friend weekend: only p2 is still due a turn.
            var s=Crew(seed,4);s.State.LevelIndex=3;s.State.TripperBag.Clear();s.State.TripperBag.Add("p2");
            Spin(s);var tripper=Tripper(s);
            Check(s.State.Doses.Count==4&&s.State.Players.TrueForAll(p=>Dose(p)>=1&&Dose(p)<=4),"Night 2 doses everyone (seed "+seed+")");
            Check(s.State.Doses.TrueForAll(d=>d.Dose==Dose(s.Player(d.PlayerId))),"every dose is public spin state");
            Check(tripper.Id=="p2","the people spinner still picks the friend due a turn");
            Check(s.State.Players.TrueForAll(p=>Dose(p)<=Dose(tripper)),"the tripper holds the highest dose; on a tie, the people spinner's pick");
        }
    }

    static void SpinResultSurvivesSnapshots()
    {
        var s=Crew(61,3);Spin(s);
        var json=JsonSerializer.Serialize(s.State,Json);var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(json,Json));var r=restored.State;
        Check(r.TripperId==s.State.TripperId&&r.SpinSeed==s.State.SpinSeed&&r.SpinEndsAt==s.State.SpinEndsAt&&r.Doses.Count==1&&r.Doses[0].Dose==s.State.Doses[0].Dose&&r.TripperBag.Count==2&&Dose(restored.Player(r.TripperId))==r.Doses[0].Dose,"the spin result and the bag survive a snapshot");
        Land(restored);Check(restored.State.Phase=="Loading","a restored spin still lands");
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(Crew(62,2).State,Json)).AsObject();
        foreach(var key in new[]{"SpinSeed","SpinEndsAt","TripperId","Doses","TripperBag"})legacy.Remove(key);
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.TripperId==""&&old.State.Doses.Count==0&&old.State.TripperBag.Count==0,"a snapshot from before the spinners restores with no spin");
        Spin(old);Check(old.Player(old.State.TripperId)!=null,"and its crew can still spin");
        legacy["Doses"]=null;bool refused=false;try{new FestivalSimulation().Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));}catch(ArgumentException){refused=true;}
        Check(refused,"a snapshot with a missing dose list is refused");
    }

    static void DoseSlowsTheTripperAllLevel()
    {
        var speeds=new[]{0,.92f,.88f,.84f,.80f};
        for(int dose=1;dose<=4;dose++)
        {
            var p=new PlayerState();p.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose});
            Check(Math.Abs(Intoxication.MovementMultiplier(p)-speeds[dose])<1e-6,"dose "+dose+" walks at x"+speeds[dose]+", got x"+Intoxication.MovementMultiplier(p));
        }
        var s=Crew(31,2);Spin(s);var tripper=Tripper(s);int taken=Dose(tripper);Play(s);
        for(double left=s.State.DurationSeconds-1;left>0;left-=60)s.Tick(Math.Min(60,left));
        Check(s.State.Phase=="Playing"&&Dose(tripper)==taken,"the dose lasts the whole level");
    }

    static void RepickWhenTheTripperDiesOrLeaves()
    {
        var s=Crew(51,3);Spin(s);var first=Tripper(s);var due=new List<string>(s.State.TripperBag);Play(s);
        Die(s,first);var second=Tripper(s);
        Check(second!=null&&second!=first&&due.Contains(second.Id)&&!s.State.TripperBag.Contains(second.Id),"a dead tripper hands over to a living friend still due a turn");
        Check(Dose(second)==1&&s.State.Doses.Exists(d=>d.PlayerId==second.Id&&d.Dose==1),"the stand-in trips at dose 1, and everyone can see it");
        s.Disconnect(second.Id);s.Tick(.2);var third=Tripper(s);
        Check(third!=null&&third!=first&&third!=second&&Dose(third)==1,"a tripper who leaves is replaced too");

        var solo=Crew(52,1);Spin(solo);Play(solo);var p=solo.Player("p0");Die(solo,p);
        p.X=24;p.Z=-20;Check(Act(solo,"p0","BeginRevival",target:"p0").Accepted,"setup: the solo spirit starts a self-revival");solo.Tick(15.3);
        Check(p.Life=="Alive"&&solo.State.TripperId=="p0"&&Dose(p)==1,"a revived solo player trips again at dose 1");

        var finale=Crew(53,3);finale.State.LevelIndex=3;Spin(finale);Play(finale);var gone=Tripper(finale);
        var spun=finale.State.Players.ToDictionary(q=>q.Id,Dose);Die(finale,gone);var heir=Tripper(finale);
        Check(heir!=gone&&Dose(heir)==spun[heir.Id],"on Night 2 the stand-in keeps the dose they already took");
    }

    static void ClueTastingIsGone()
    {
        var s=Crew(71,2);s.State.LevelIndex=1;Spin(s);Play(s);var tripper=Tripper(s);var friend=s.State.Players.Find(p=>p!=tripper);
        friend.X=-18;friend.Z=-22;
        Check(!Act(s,friend.Id,"ClueSupply").Accepted&&friend.Effects.Count==0,"the free clue tasting is gone: the spin decides who sees the clue trail");
        Check(FestivalSimulation.VisibleVisions(s.State,tripper.Id).Count>0&&FestivalSimulation.VisibleVisions(s.State,friend.Id).Count==0,"only the tripper sees the visions");
    }

    // Night 2 doses everyone, so nobody is sober. The old totems needed a sober friend; the tripper's clue trail does not.
    static void NightTwoCrewFollowsTheTrail()
    {
        for(int seed=0;seed<20;seed++)
        {
            var s=Crew(seed,2);s.State.LevelIndex=3;Spin(s);var holders=s.State.ClueChain.ConvertAll(id=>s.State.Npcs.Find(n=>n.Id==id));
            Play(s);var tripper=Tripper(s);var friend=s.State.Players.Find(p=>p!=tripper);
            Check(Dose(friend)>=1&&holders.Count>0,"setup: Night 2 doses the friend too and lays a clue trail (seed "+seed+")");
            foreach(var holder in holders)s.ConfirmVisionsOf(holder);
            friend.X=s.State.FriendPosition.X;friend.Z=s.State.FriendPosition.Z;var find=Act(s,friend.Id,"FindFriend");
            Check(s.State.GateOpened&&find.Accepted,"with nobody sober, the tripper's checked trail still leads the dosed friend to the lost one (seed "+seed+"): "+find.Reason);
        }
    }

    // The old stock effects keep working as optional consumables: the spinner's dose does not use up one of the two slots.
    static void DoseLeavesBothConsumableSlots()
    {
        var s=Crew(21,1);Spin(s);Play(s);var p=s.Player("p0");
        p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});p.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
        Check(Act(s,"p0","Consume","stock_lsd").Accepted,"the tripper takes a Prism tab on top of the dose");
        var caps=Act(s,"p0","Consume","stock_mushrooms");
        Check(caps.Accepted&&Dose(p)>=1&&p.Effects.Exists(e=>e.Id=="mushrooms"),"and Moon caps too, as a sober player could: "+caps.Reason);
        var shot=Crew(22,1);Spin(shot);Play(shot);var q=shot.Player("p0");q.Effects.Add(new ActiveEffect{Id="shot",InstanceId="debrief-shot",RemainingSeconds=60});
        q.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});q.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
        Check(Act(shot,"p0","Consume","stock_lsd").Accepted&&!Act(shot,"p0","Consume","stock_mushrooms").Accepted,"two effects besides the dose is still the limit (a debrief shot plus a Prism tab)");
    }

    static void MedicalCannotCureTheDose()
    {
        var s=Crew(81,1);Spin(s);Play(s);var p=s.Player("p0");int dose=Dose(p);
        p.X=24;p.Z=-20;p.Inventory.Add(new ItemStack{ItemId="medical_voucher",Count=1});
        Check(!Act(s,"p0","Use","medical_voucher").Accepted&&Dose(p)==dose,"the medical tent cannot talk the tripper down");
        p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});Check(Act(s,"p0","Consume","stock_lsd").Accepted,"setup: a Prism tab on top of the dose");
        Check(Act(s,"p0","Use","medical_voucher").Accepted,"the tent treats the extra effect");s.Tick(3.1);
        Check(Dose(p)==dose&&!p.Effects.Exists(e=>e.Id=="lsd")&&p.Inventory.Count==0,"the voucher removes the Prism tab and leaves the dose");
        // A stand-in who took a Prism tab before inheriting the dose carries the tab first; the tent still treats only the tab.
        var crew=Crew(51,3);Spin(crew);Play(crew);var first=Tripper(crew);
        foreach(var friend in crew.State.Players)if(friend!=first){friend.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});Check(Act(crew,friend.Id,"Consume","stock_lsd").Accepted,"setup: "+friend.Id+" takes a Prism tab");}
        Die(crew,first);var heir=Tripper(crew);Check(heir.Effects[0].Id=="lsd"&&Dose(heir)==1,"setup: the stand-in took the tab before the dose");
        heir.X=24;heir.Z=-20;heir.Inventory.Add(new ItemStack{ItemId="medical_voucher",Count=1});
        Check(Act(crew,heir.Id,"Use","medical_voucher").Accepted,"the tent treats the stand-in's tab");crew.Tick(3.1);
        Check(Dose(heir)==1&&!heir.Effects.Exists(e=>e.Id=="lsd"),"the stand-in keeps the dose and loses the earlier tab");
    }

    static void GuidanceFollowsTheSpin()
    {
        var s=Crew(91,2);s.State.LevelIndex=1;Spin(s);var tripper=Tripper(s);var friend=s.State.Players.Find(p=>p!=tripper);
        string headline=FestivalGuidance.Headline(s.State,friend),hint=FestivalGuidance.Hint(s.State,friend);
        Check(headline.Contains("SPIN")&&!headline.Contains("TOTEM")&&hint.Contains("wheel"),"camp guidance follows the spin, not the festival: "+headline+" / "+hint);
        Play(s);friend.X=-18;friend.Z=-22;hint=FestivalGuidance.Hint(s.State,friend);
        Check(!hint.ToLowerInvariant().Contains("tasting")&&hint.Contains(tripper.Name),"a sober friend is sent to the tripper, not to a clue tasting: "+hint);
    }
}
