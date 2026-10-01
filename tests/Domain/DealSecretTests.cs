using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Festival.Core;

// TRIP-7: a client's view carries every public seed (Seed, SpinSeed, the weekend position, the tripper, the doses, the crew), and
// journey J2 rebuilt every level's hidden deal from them, 120 levels of 120. The host now mixes a secret of its own, which no view
// carries (the EditMode half: VisionSessionTests), into each level's deal: the roles, both clue trails and their fakes, a night's
// double buyer, the lost friends' spots and what the clue cloud pictures. With no secret (tests, previews and older snapshots)
// every draw is what it always was.
public static class DealSecretTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static void Check(bool pass,string message){if(!pass)throw new Exception("DealSecret: "+message);}
    static int sequence;
    static CommandResult Act(FestivalSimulation s,string id,string kind)=>s.Execute(id,new GameCommand{Id="deal"+(++sequence),Kind=kind});
    // A secret like the host's, any 32 bits: here a fixed spread of n, so every run deals the same levels.
    static int Secret(int n)=>unchecked((int)((uint)(n+1)*2654435761u));
    static readonly (int Festival,int Level)[] Nights={(0,1),(0,3),(1,1),(1,3)};

    // `crew` friends ready at camp for this level of this festival; the wheels land and everyone loads into the real crowd. The
    // host hands the simulation `secret` (null: none, as in tests and previews).
    // `before` changes the round before the crew sets off.
    static FestivalSimulation Start(int seed,int festival,int level,int crew,Func<int> secret,Action<RoundState> before=null)
    {
        var s=new FestivalSimulation(seed){DealSecret=secret};for(int i=0;i<crew;i++)s.AddPlayer("p"+i,"P"+i);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;before?.Invoke(s.State);Begin(s);return s;
    }
    static void Begin(FestivalSimulation s)
    {
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew reaches the festival");
    }
    static RoundState Copy(RoundState s)=>JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s,Json),Json);

    // What the J2 probe rebuilt the deal from, all of it in every view: the round's seed, the spin, the weekend position and the crew.
    static string Probe(RoundState s)=>string.Join("|",s.Seed,s.SpinSeed,s.FestivalIndex,s.LevelIndex,s.EncoreTier,s.TripperId,string.Join(",",s.Doses.Select(d=>d.PlayerId+":"+d.Dose+d.Substance)),s.Players.Count);
    // The hidden deal: every festivalgoer's role, both trails, where both friends are lost, the tripper's visions with their truth,
    // and what the clue cloud pictures.
    static string Roles(RoundState s)=>string.Join(",",s.Npcs.Select(n=>n.Role));
    static string Trails(RoundState s)=>string.Join(",",s.ClueChain)+"/"+string.Join(",",s.SecondFriend.ClueChain);
    static string Spots(RoundState s)=>s.FriendPosition.X+","+s.FriendPosition.Z+(s.SecondFriend.Active?"/"+s.SecondFriend.Position.X+","+s.SecondFriend.Position.Z:"");
    static string Deal(RoundState s)=>string.Join("|",Roles(s),Trails(s),Spots(s),string.Join(",",s.Visions.Select(v=>v.Id+v.Kind+v.NpcId+"@"+v.X+","+v.Z+v.IsTrue)),s.CloudClueLandmark);
    // Which of the night's buyers (in crowd order) pays double, or -1 for none.
    static int DoubleBuyer(RoundState s){var buyers=s.Npcs.FindAll(n=>n.Role=="Buyer");var v=s.Visions.Find(x=>x.Kind=="DoubleBuyer");return v==null?-1:buyers.FindIndex(n=>n.Id==v.NpcId);}
    // The fake clues the tripper sees, by who wears them.
    static string Fakes(RoundState s)=>string.Join(",",s.Visions.Where(v=>v.Kind=="Clue"&&!v.IsTrue).Select(v=>v.NpcId).OrderBy(id=>id,StringComparer.Ordinal));
    static void FindFirstLink(FestivalSimulation s)=>s.ConfirmVisionsOf(s.State.Npcs.Find(n=>n.Id==s.State.ClueChain[0]));

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{AClientCannotRebuildTheDeal,ASecretDealsItsOwnLevel,EachLevelHasAFreshSecret,TheNextLinksFakesComeFromTheSecret,SnapshotsKeepTheSecret,TheGuardPostGivesNoRoleAway,TheGuardSwapKeepsTheDeal})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" deal secret test(s) failed:\n"+string.Join("\n",failures));
    }

    // The J2 probe: a client replays the level's start from the public seeds in its view and lands on the host's deal. It still
    // has every seed the probe had, but no level's roles come out the same, and the trails, the friends' spots, the cloud's
    // landmark and a night's double buyer come out the same only about as often as a guess would.
    static void AClientCannotRebuildTheDeal()
    {
        int levels=0,roles=0,nights=0,trails=0,spots=0,days=0,landmarks=0,doubleNights=0,doubles=0;
        for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<Festivals.LevelCount;level++)foreach(var crew in new[]{2,5})for(int seed=0;seed<15;seed++)
        {
            var host=Start(seed,festival,level,crew,()=>Secret(seed)).State;var rebuilt=Start(seed,festival,level,crew,null).State;levels++;
            string where=Festivals.Name(festival)+" level "+level+", "+crew+" players, seed "+seed;
            Check(Probe(rebuilt)==Probe(host),where+": setup: the rebuild has every public seed the host's views carry");
            Check(rebuilt.CloudClueStart==host.CloudClueStart&&rebuilt.GiggleTankSpot==host.GiggleTankSpot,where+": the clue cloud's window and the Giggle Tank, in every view, stay public");
            if(Roles(rebuilt)==Roles(host))roles++;
            if(Festivals.For(host).Night)
            {
                nights++;if(Trails(rebuilt)==Trails(host))trails++;if(Spots(rebuilt)==Spots(host))spots++;
                if(DoubleBuyer(host)>=0&&DoubleBuyer(rebuilt)>=0){doubleNights++;if(DoubleBuyer(rebuilt)==DoubleBuyer(host))doubles++;}
            }
            else{days++;if(rebuilt.CloudClueLandmark==host.CloudClueLandmark)landmarks++;}
        }
        Check(levels==240&&nights==120&&days==120,"setup: 240 levels, half of them nights, got "+levels+" ("+nights+" nights)");
        Check(roles==0,"the rebuild deals the host's roles on "+roles+" of "+levels+" levels");
        // A two-link trail through 24 festivalgoers comes out the same about once in 550 guesses.
        Check(trails*20<nights,"the rebuild lays the host's clue trails on "+trails+" of "+nights+" nights, more than a fluke");
        // Three spots to lose a friend at: a guess lands on a third of them.
        Check(spots*2<nights,"the rebuild loses the friends where the host did on "+spots+" of "+nights+" nights, no better than a guess");
        // Six or seven landmarks to picture.
        Check(landmarks*3<days,"the rebuild pictures the host's landmark on "+landmarks+" of "+days+" days, no better than a guess");
        Check(doubleNights>=10,"setup: plenty of nights where both deal a double buyer, got "+doubleNights);
        Check(doubles*2<doubleNights,"the rebuild picks the host's double buyer on "+doubles+" of "+doubleNights+" nights, no better than a guess");
    }

    // TRIP-10: a client rebuilds where every festivalgoer stood as the level started (CreateRound's spots, from the public Seed).
    // The VIP guard was the regular nearest the post, so everyone who stood nearer was a buyer or a narc. Now it is whoever stood
    // nearest, whatever the deal gave them, and a guard dealt anything but a regular trades roles with a random regular: nobody
    // stood nearer the post, and the next nearest are regulars about as often as anyone. (Trading with the regular nearest the
    // post would only move the leak one rank down, to about 18% against a base near 37%; the rank checks catch that.)
    static void TheGuardPostGivesNoRoleAway()
    {
        const int Ranks=2;int levels=0,others=0,regulars=0;var rankRegulars=new int[Ranks];
        for(int level=0;level<Festivals.LevelCount;level++)foreach(var crew in new[]{2,5})for(int seed=0;seed<80;seed++)
        {
            var s=Start(seed,Festivals.PoloFestival,level,crew,()=>Secret(seed)).State;var start=new FestivalSimulation(seed).State.Npcs;levels++;
            string where="level "+level+", "+crew+" players, seed "+seed;
            double FromPost(NpcState n){var at=start.Find(o=>o.Id==n.Id);return Math.Sqrt((at.X-Festivals.VipGuardPostX)*(at.X-Festivals.VipGuardPostX)+(at.Z-Festivals.VipGuardPostZ)*(at.Z-Festivals.VipGuardPostZ));}
            var guard=s.Npcs.Find(n=>n.Twist==FestivalSimulation.VipGuard);Check(guard!=null&&guard.Role=="Regular",where+": a regular guards the VIP ropes");
            var rest=s.Npcs.Where(n=>n.Kind=="Wook"&&n!=guard).OrderBy(FromPost).ToList();
            Check(FromPost(rest[0])>=FromPost(guard),where+": nobody stood nearer the post than the guard, but "+rest[0].Id+" ("+rest[0].Role+") stood "+FromPost(rest[0]).ToString("0.0")+" m off and the guard "+FromPost(guard).ToString("0.0")+" m");
            for(int r=0;r<Ranks;r++)if(rest[r].Role=="Regular")rankRegulars[r]++;
            others+=rest.Count;regulars+=rest.Count(n=>n.Role=="Regular");
        }
        double share=(double)regulars/others;
        for(int r=0;r<Ranks;r++)Check(Math.Abs((double)rankRegulars[r]/levels-share)<.08,"the festivalgoer "+(r+2)+(r==0?"nd":"rd")+" nearest the post is a regular on "+rankRegulars[r]+" of "+levels+" levels, against "+(share*100).ToString("0")+"% of everyone else");
    }

    // The guard's trade changes two festivalgoers' roles and nothing else. The same level with one of the host's other regulars
    // moved onto the post first deals with nobody to trade (that regular stands nearest and drew a regular): the host's deal is
    // that one with the guard and one partner trading roles, a clue link included, so every role count is what it was.
    static void TheGuardSwapKeepsTheDeal()
    {
        int swaps=0;
        for(int level=0;level<Festivals.LevelCount;level++)foreach(var crew in new[]{2,5})for(int seed=0;seed<10;seed++)
        {
            var host=Start(seed,Festivals.PoloFestival,level,crew,()=>Secret(seed)).State;var guard=host.Npcs.Find(n=>n.Twist==FestivalSimulation.VipGuard);
            string where="level "+level+", "+crew+" players, seed "+seed;
            var stay=host.Npcs.Find(n=>n.Role=="Regular"&&n!=guard);Check(stay!=null,where+": setup: another regular");
            var plain=Start(seed,Festivals.PoloFestival,level,crew,()=>Secret(seed),s=>{var n=s.Npcs.Find(o=>o.Id==stay.Id);n.X=Festivals.VipGuardPostX;n.Z=Festivals.VipGuardPostZ;}).State;
            Check(plain.Npcs.Find(n=>n.Twist==FestivalSimulation.VipGuard).Id==stay.Id,where+": setup: the moved regular guards the other deal");
            Check(string.Join(",",host.Npcs.Select(n=>n.Role).OrderBy(r=>r))==string.Join(",",plain.Npcs.Select(n=>n.Role).OrderBy(r=>r)),where+": the same role counts");
            var traded=host.Npcs.Where(n=>plain.Npcs.Find(o=>o.Id==n.Id).Role!=n.Role).ToList();
            if(traded.Count==0){Check(Trails(host)==Trails(plain),where+": no trade, the same trails");continue;}
            swaps++;var partner=traded.Find(n=>n!=guard);string drawn=plain.Npcs.Find(n=>n.Id==guard.Id).Role;
            Check(traded.Count==2&&traded.Contains(guard)&&partner.Role==drawn&&plain.Npcs.Find(n=>n.Id==partner.Id).Role==guard.Role,where+": only the guard and one partner trade roles, got "+string.Join(",",traded.Select(n=>n.Id+":"+n.Role)));
            Check(Trails(host)==Trails(plain).Replace(guard.Id,partner.Id),where+": the partner takes the guard's place in its trail");
        }
        Check(swaps>=10,"setup: the guard post's nearest festivalgoer drew another role on plenty of levels, got "+swaps);
    }

    // The level keeps the host's secret as its deal seed. Two hosts with the same spin and different secrets deal different roles;
    // the same secret deals the same level again; and a secret of 0 deals exactly what no secret does, so every seed-pinned
    // premise in the other suites, which run with none, still holds.
    static void ASecretDealsItsOwnLevel()
    {
        foreach(var (festival,level) in new[]{(0,0),(0,1),(1,2),(1,3)})for(int seed=0;seed<5;seed++)
        {
            string where=Festivals.Name(festival)+" level "+level+", seed "+seed+": ";
            var host=Start(seed,festival,level,5,()=>Secret(seed)).State;
            Check(host.DealSeed==Secret(seed),where+"the level keeps the host's secret as its deal seed, got "+host.DealSeed);
            Check(Deal(Start(seed,festival,level,5,()=>Secret(seed)).State)==Deal(host),where+"the same secret deals the same level again");
            Check(Roles(Start(seed,festival,level,5,()=>Secret(seed+100)).State)!=Roles(host),where+"another secret deals other roles");
            var none=Start(seed,festival,level,5,null).State;
            Check(none.DealSeed==0&&Deal(Start(seed,festival,level,5,()=>0).State)==Deal(none),where+"a secret of 0 deals what no secret does");
        }
    }

    // Each level's spin asks the host for a new secret; camp, between levels, holds none.
    static void EachLevelHasAFreshSecret()
    {
        var asked=new List<int>();
        var s=new FestivalSimulation(8){DealSecret=()=>{asked.Add(Secret(50+asked.Count));return asked[asked.Count-1];}};s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        Begin(s);Check(asked.Count==1&&s.State.DealSeed==asked[0],"Day 1 deals from the secret its spin asked for");
        s.State.Phase="Results";s.State.Result="Success";Check(Act(s,"p0","Reset").Accepted&&s.State.Phase=="CampReview","setup: the crew is back at camp");
        Check(s.State.DealSeed==0,"camp holds no secret");
        s.State.Phase="Shopping";Begin(s);Check(s.State.LevelIndex==1,"setup: on to Night 1");
        Check(asked.Count==2&&s.State.DealSeed==asked[1]&&asked[1]!=asked[0],"Night 1 deals from a fresh secret");
    }

    // A found link's fakes come from the level's secret too. The same night, its deal seed changed after the deal, follows its
    // first link to other fakes: the trail stays the one already dealt, but nobody can work out the next link's decoys.
    static void TheNextLinksFakesComeFromTheSecret()
    {
        int follows=0,same=0;
        foreach(var (festival,level) in Nights)for(int seed=0;seed<20;seed++)
        {
            var host=Start(seed,festival,level,2,()=>Secret(seed));if(FestivalSimulation.TripperDose(host.State)<2||host.State.ClueChain.Count<2)continue;
            var other=new FestivalSimulation();other.Restore(Copy(host.State));other.State.DealSeed=Secret(seed+100);
            FindFirstLink(host);FindFirstLink(other);follows++;
            Check(host.State.CluesRead==1&&Fakes(host.State)!="","setup: "+Festivals.Name(festival)+" level "+level+", seed "+seed+": the next link has fakes");
            if(Fakes(other.State)==Fakes(host.State))same++;
        }
        Check(follows>=20,"setup: plenty of dose 2+ nights with two links or more, got "+follows);
        Check(same*2<follows,"another deal seed draws the next link's fakes the host drew on "+same+" of "+follows+" nights");
    }

    // A snapshot keeps the level's deal seed, so a restored host follows the trail to the fakes the host would have. One saved
    // before deal seeds restores with none, the seed it was dealt with.
    static void SnapshotsKeepTheSecret()
    {
        var host=Start(3,0,1,2,()=>Secret(3));Check(FestivalSimulation.TripperDose(host.State)>=2&&host.State.ClueChain.Count>=2,"setup: a night with fakes and two links");
        var json=JsonSerializer.SerializeToNode(host.State,Json).AsObject();Check(json.ContainsKey("DealSeed"),"the deal seed is a saved field");
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        json.Remove("DealSeed");var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),Json));
        Check(restored.State.DealSeed==Secret(3)&&old.State.DealSeed==0,"a snapshot keeps the deal seed; an older one restores with none");
        FindFirstLink(host);FindFirstLink(restored);FindFirstLink(old);
        Check(Fakes(restored.State)==Fakes(host.State),"the restored host follows the trail to the host's fakes");
        Check(Fakes(old.State)!=Fakes(host.State),"which come from the secret: with none they are other fakes");
    }
}
