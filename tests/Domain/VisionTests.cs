using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// TRIP-2: each level deals the crowd hidden roles, and only the tripper sees visions of them: buyers and narcs by day, the
// clue trail by night, partly false by dose, with real secrets mixed in at doses 3 and 4.
public static class VisionTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    // The design's share of true visions for doses 1-4.
    static readonly double[] Reliability={0,.75,.50,.25,.10};
    static readonly string[] SecretKinds={"Stash","DoubleBuyer","Shortcut"};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Vision: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="")=>s.Execute(id,new GameCommand{Id="vision"+(sequence++),Kind=kind,TargetId=target,ItemId=item});
    static PlayerState Tripper(FestivalSimulation s)=>s.Player(s.State.TripperId);
    static int Dose(PlayerState p)=>p.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect)?.Intensity??0;
    static NpcState Npc(FestivalSimulation s,string id)=>s.State.Npcs.Find(n=>n.Id==id);
    static List<NpcState> Wooks(FestivalSimulation s)=>s.State.Npcs.FindAll(n=>n.Kind=="Wook");
    static bool Secret(VisionState v)=>Array.IndexOf(SecretKinds,v.Kind)>=0;
    static bool DoubleBuyer(FestivalSimulation s,NpcState n)=>s.State.Visions.Exists(v=>v.Kind=="DoubleBuyer"&&v.NpcId==n.Id);

    // The crew readies at camp for this level, the wheels land and everyone loads into the real crowd.
    static FestivalSimulation Start(int seed,int level,int festival=0,int encore=0,int crew=2)
    {
        var s=new FestivalSimulation(seed);for(int i=0;i<crew;i++)s.AddPlayer("p"+i,"P"+i);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.EncoreTier=encore;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);Check(s.State.Phase=="Spinning","setup: the wheels spin");
        s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew reaches the festival");return s;
    }
    // The first level from `seed` on whose spin the tripper takes exactly `dose`; `seed` moves past it.
    static FestivalSimulation Dosed(int dose,int level,ref int seed){for(;seed<5000;seed++){var s=Start(seed,level);if(Dose(Tripper(s))==dose){seed++;return s;}}throw new Exception("no seed spins dose "+dose);}
    // Sell one stock to npc, hitting every note, with no cops around. Returns the cash it paid.
    static int Sell(FestivalSimulation s,string id,NpcState npc)
    {
        var p=s.Player(id);p.X=npc.X;p.Z=npc.Z-1;p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});int before=p.Cash;
        Check(Act(s,id,"StartSale",npc.Id,"stock_lsd").Accepted,"setup: "+id+" starts a sale to buyer "+npc.Id);
        var sale=s.Interaction(p.InteractionId);
        foreach(var note in RhythmChart.Create(sale.ChartSeed,sale.NoteCount,sale.BeatSeconds).Notes)sale.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
        for(int guard=0;sale.Status=="Active"&&guard<200;guard++)s.Tick(.1);
        Check(sale.Status=="Complete","setup: the sale to "+npc.Id+" settles ("+sale.Status+")");return p.Cash-before;
    }

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{RolesMatchTheTable,DayMarksFollowTheDose,NightShowsTheNextClueHolder,SecretsOnlyAtDoseThreeAndUp,
            SalesDependOnTheRole,SecretsPay,OnlyTheTripperSeesVisions,VisionsSurviveSnapshots,TheClueTrailLeadsToTheFriend,TheTotemsAreGone,GuidanceFollowsTheTrail})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" vision test(s) failed:\n"+string.Join("\n",failures));
    }

    static void RolesMatchTheTable()
    {
        void Deal(FestivalSimulation s,string where)
        {
            var t=Festivals.For(s.State);var wooks=Wooks(s);int Count(string role)=>wooks.Count(n=>n.Role==role);
            Check(wooks.Count==24,"setup: the full crowd of 24");
            Check(Count("Narc")==t.Narcs,where+": the table's "+t.Narcs+" narcs hide in the crowd, got "+Count("Narc"));
            Check(Count("ClueHolder")==t.ChainLength,where+": one clue holder per link of the "+t.ChainLength+"-link chain, got "+Count("ClueHolder"));
            Check(Count("Buyer")==10,where+": about 40% of the crowd buys (10 of 24), got "+Count("Buyer"));
            Check(Count("Regular")==24-t.Narcs-t.ChainLength-10,where+": everyone else is a regular, got "+Count("Regular"));
            var holders=wooks.FindAll(n=>n.Role=="ClueHolder").ConvertAll(n=>n.Id);
            Check(s.State.ClueChain.Count==t.ChainLength&&s.State.ClueChain.All(holders.Contains),where+": the clue chain runs through the clue holders");
            Check(s.State.Npcs.TrueForAll(n=>n.Kind=="Wook"||n.Role==""),where+": the cops are not dealt a role");
        }
        for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<Festivals.LevelCount;level++)
            Deal(Start(7+level,level,festival),Festivals.Name(festival)+" level "+level);
        Deal(Start(9,3,1,encore:3),"Ember Playa Night 2 on the third encore");
        var a=Start(21,0);var b=Start(22,0);
        Check(string.Join(",",Wooks(a).ConvertAll(n=>n.Role))!=string.Join(",",Wooks(b).ConvertAll(n=>n.Role)),"each spin deals the roles afresh");
    }

    static void DayMarksFollowTheDose()
    {
        var orders=new HashSet<string>();
        for(int dose=1;dose<=4;dose++)for(int seed=0,sample=0;sample<5;sample++)
        {
            var s=Dosed(dose,0,ref seed);var marks=s.State.Visions.FindAll(v=>!Secret(v));string where="dose "+dose+" seed "+(seed-1);
            if(dose==2)orders.Add(string.Join("",s.State.Visions.ConvertAll(v=>v.IsTrue?"T":"F")));
            Check(marks.Count==12&&marks.TrueForAll(v=>v.Kind=="Buyer"||v.Kind=="Narc"),where+": by day the tripper sees 12 buyer or narc marks, got "+string.Join(",",marks.ConvertAll(v=>v.Kind)));
            Check(marks.Select(v=>v.NpcId).Distinct().Count()==marks.Count,where+": at most one mark per festivalgoer");
            int truths=marks.Count(v=>v.IsTrue);
            Check(Math.Abs(truths-Reliability[dose]*marks.Count)<=1,where+": "+truths+" of "+marks.Count+" marks are true; the dose's share is "+Reliability[dose]);
            foreach(var v in marks)
            {
                string role=Npc(s,v.NpcId).Role;
                Check(v.IsTrue?role==v.Kind:role=="Regular"&&v.Kind=="Buyer"||role=="Buyer"&&v.Kind=="Narc"||role=="Narc"&&v.Kind=="Buyer",where+": a "+(v.IsTrue?"true":"fake")+" "+v.Kind+" mark on a "+role);
            }
            if(dose>=2)Check(marks.Exists(v=>!v.IsTrue&&v.Kind=="Buyer"&&Npc(s,v.NpcId).Role=="Narc"),where+": from dose 2 a narc passes as a buyer, so trusting blindly is dangerous");
            Check(s.State.Visions.TrueForAll(v=>v.Tell==!v.IsTrue&&!v.Confirmed&&v.Id!=""),where+": every fake carries a tell and nothing is confirmed yet");
        }
        Check(orders.Count>1,"the visions come in no telling order: "+string.Join(" ",orders));
    }

    static void NightShowsTheNextClueHolder()
    {
        var shown=new[]{0,1,2,4,10};
        for(int dose=1;dose<=4;dose++)for(int seed=0,sample=0;sample<5;sample++)
        {
            var s=Dosed(dose,1,ref seed);var clues=s.State.Visions.FindAll(v=>!Secret(v));string where="night dose "+dose+" seed "+(seed-1);
            Check(clues.Count==shown[dose]&&clues.TrueForAll(v=>v.Kind=="Clue"),where+": the next link shows among round(1/r)-1 fakes, "+shown[dose]+" clue visions in all, got "+clues.Count);
            Check(clues.Count(v=>v.IsTrue)==1&&clues.Find(v=>v.IsTrue).NpcId==s.State.ClueChain[0],where+": the real first clue holder is always among them");
            Check(Math.Abs(1-Reliability[dose]*clues.Count)<=1,where+": the true share stays within one vision of "+Reliability[dose]);
            Check(clues.Select(v=>v.NpcId).Distinct().Count()==clues.Count,where+": each fake points at someone else");
        }
    }

    static void SecretsOnlyAtDoseThreeAndUp()
    {
        for(int level=0;level<2;level++)for(int dose=1;dose<=4;dose++)for(int seed=0,sample=0;sample<5;sample++)
        {
            var s=Dosed(dose,level,ref seed);var secrets=s.State.Visions.FindAll(Secret);string where=(level==0?"day":"night")+" dose "+dose+" seed "+(seed-1);
            Check(secrets.Count==Math.Max(0,dose-2),where+": secret sights only at doses 3 (one) and 4 (two), got "+secrets.Count);
            Check(secrets.TrueForAll(v=>v.IsTrue&&!v.Tell),where+": secrets are always real and look like any true vision");
            Check(secrets.Select(v=>v.Kind).Distinct().Count()==secrets.Count,where+": two different secrets");
            foreach(var v in secrets)
            {
                Check(level==0?v.Kind=="Stash"||v.Kind=="DoubleBuyer":v.Kind=="Stash"||v.Kind=="Shortcut",where+": a "+v.Kind+" secret");
                Check(v.Kind=="DoubleBuyer"?Npc(s,v.NpcId)?.Role=="Buyer":v.NpcId==""&&(v.X!=0||v.Z!=0),where+": a double buyer is a real buyer; a stash or shortcut is a spot on the grounds");
            }
        }
    }

    static void SalesDependOnTheRole()
    {
        var multiplier=new[]{0,1,1.25,1.5,1.75};var paid=new[]{0,10,13,15,18};
        for(int dose=1;dose<=4;dose++)
        {
            int seed=0;var s=Dosed(dose,0,ref seed);s.State.Npcs.RemoveAll(n=>n.Kind=="Cop");
            Check(FestivalSimulation.PayoutMultiplier(s.State)==multiplier[dose],"dose "+dose+" pays x"+multiplier[dose]+", got x"+FestivalSimulation.PayoutMultiplier(s.State));
            // POLO-1: a buyer out on open ground, since a sale inside Palm Mirage's VIP zones pays double.
            var buyer=Wooks(s).Find(n=>n.Role=="Buyer"&&!DoubleBuyer(s,n)&&!Festivals.InVipZone(s.State.FestivalIndex,n.X,n.Z));int sales=s.State.LevelSales;
            Check(buyer!=null,"dose "+dose+": the crowd has buyers");
            int got=Sell(s,"p0",buyer);
            Check(got==paid[dose]&&s.State.LevelSales==sales+got,"a perfect sale to a buyer pays $10 x the dose's multiplier: $"+paid[dose]+" at dose "+dose+", got $"+got);
        }
        {
            int seed=0;var s=Dosed(1,0,ref seed);s.State.Npcs.RemoveAll(n=>n.Kind=="Cop");var p=s.Player("p1");
            var regular=Wooks(s).Find(n=>n.Role=="Regular");Check(regular!=null,"the crowd has regulars");p.X=regular.X;p.Z=regular.Z-1;p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});int cash=p.Cash;
            Check(!Act(s,p.Id,"StartSale",regular.Id,"stock_lsd").Accepted,"a regular turns the sale down");
            Check(p.InteractionId==""&&p.Inventory.Exists(i=>i.ItemId=="stock_lsd")&&p.Cash==cash&&p.Life=="Alive","the seller keeps the stock and walks away");
            Check((regular.Observers.Find(o=>o.PlayerId==p.Id)?.Suspicion??0)>0,"with a little suspicion from the regular");
            var narc=Wooks(s).Find(n=>n.Role=="Narc");Check(narc!=null,"the crowd has narcs");p.X=narc.X;p.Z=narc.Z-1;int sales=s.State.LevelSales;
            Act(s,p.Id,"StartSale",narc.Id,"stock_lsd");
            Check(p.Life=="Detained"&&!p.Inventory.Exists(i=>i.ItemId=="stock_lsd")&&s.State.LevelSales==sales,"offering stock to a narc is a bust: detained, stock confiscated ("+p.Life+")");
        }
    }

    static void SecretsPay()
    {
        int seed=0;var s=Dosed(4,0,ref seed);s.State.Npcs.RemoveAll(n=>n.Kind=="Cop");
        Check(s.State.Visions.Exists(v=>v.Kind=="DoubleBuyer")&&s.State.Visions.Exists(v=>v.Kind=="Stash"),"a dose-4 day shows a double buyer and a stash");
        var doubler=Npc(s,s.State.Visions.Find(v=>v.Kind=="DoubleBuyer").NpcId);
        Check(Sell(s,"p0",doubler)==35,"the secret double buyer pays twice: $17.50 x 2 at dose 4");
        var stash=s.State.Visions.Find(v=>v.Kind=="Stash");var p=s.Player("p1");int banked=s.State.StashCash;
        s.Tick(.1);Check(s.State.StashCash==banked&&!stash.Confirmed,"nobody has reached the stash yet");
        p.X=stash.X;p.Z=stash.Z;s.Tick(.1);
        Check(s.State.StashCash==banked+15&&stash.Confirmed,"reaching the secret stash banks $15 for the crew");
        p.X+=10;s.Tick(.1);p.X-=10;s.Tick(.2);Check(s.State.StashCash==banked+15,"a stash pays once");
        var night=Dosed(4,1,ref seed);var shortcut=night.State.Visions.Find(v=>v.Kind=="Shortcut");var q=night.Player("p0");banked=night.State.StashCash;
        q.X=shortcut.X;q.Z=shortcut.Z;night.Tick(.1);Check(night.State.StashCash==banked,"a shortcut is a way through, not cash");
    }

    static void OnlyTheTripperSeesVisions()
    {
        int seed=0;var s=Dosed(3,0,ref seed);var tripper=Tripper(s);var friend=s.State.Players.Find(p=>p!=tripper);
        var seen=FestivalSimulation.VisibleVisions(s.State,tripper.Id);
        Check(seen.Count==s.State.Visions.Count&&seen.Count>0,"the tripper sees every vision");
        for(int i=0;i<seen.Count;i++){var v=s.State.Visions[i];Check(seen[i].Id==v.Id&&seen[i].Kind==v.Kind&&seen[i].NpcId==v.NpcId&&seen[i].X==v.X&&seen[i].Z==v.Z&&seen[i].Tell==v.Tell,"the tripper's copy keeps where and what, and the tell");}
        Check(seen.TrueForAll(v=>!v.IsTrue&&!v.Confirmed),"no vision's truth leaves the host before it is checked");
        foreach(var life in new[]{"Alive","Detained","Downed","Spirit"}){friend.Life=life;Check(FestivalSimulation.VisibleVisions(s.State,friend.Id).Count==0,"a "+life+" friend who is not tripping sees no visions");}
        Check(FestivalSimulation.VisibleVisions(s.State,"").Count==0&&FestivalSimulation.VisibleVisions(new RoundState(),"").Count==0,"nobody else does either");
        var truth=s.State.Visions.Find(v=>v.IsTrue);var fake=s.State.Visions.Find(v=>!v.IsTrue);truth.Confirmed=fake.Confirmed=true;
        seen=FestivalSimulation.VisibleVisions(s.State,tripper.Id);
        Check(seen.Find(v=>v.Id==truth.Id).IsTrue&&!seen.Find(v=>v.Id==fake.Id).IsTrue&&seen.FindAll(v=>v.Confirmed).Count==2,"once checked, the truth reaches the tripper");
        Check(seen.FindAll(v=>v.IsTrue).Count==1,"and only for the visions they checked");
        seen[0].Kind="tampered";Check(s.State.Visions[0].Kind!="tampered","the view is a copy");
    }

    static void TheClueTrailLeadsToTheFriend()
    {
        var shown=new[]{0,1,2,4,10};
        for(int dose=1;dose<=4;dose++)
        {
            int seed=0;var s=Dosed(dose,1,ref seed);var chain=new List<string>(s.State.ClueChain);var p=s.Player("p0");string where="dose "+dose+": ";
            Check(chain.Count==2,"setup: Palm Mirage Night 1 has a two-link trail");
            var secrets=s.State.Visions.FindAll(Secret).ConvertAll(v=>v.Id);
            p.X=s.State.FriendPosition.X;p.Z=s.State.FriendPosition.Z;
            Check(!Act(s,p.Id,"FindFriend").Accepted,where+"nobody reaches the friend before the trail ends");
            for(int link=0;link<chain.Count;link++)
            {
                var clues=s.State.Visions.FindAll(v=>v.Kind=="Clue");
                Check(clues.Count==shown[dose]&&clues.Count(v=>v.IsTrue)==1&&clues.Find(v=>v.IsTrue).NpcId==chain[link],where+"link "+link+" shows its real clue holder among the dose's fakes");
                Check(clues.TrueForAll(v=>!v.Confirmed&&v.Tell==!v.IsTrue&&v.Id!=""),where+"link "+link+"'s visions are fresh, with their tells");
                var fake=clues.Find(v=>!v.IsTrue);
                if(fake!=null){s.ConfirmVisionsOf(Npc(s,fake.NpcId));Check(fake.Confirmed&&s.State.CluesRead==link&&!s.State.GateOpened,where+"checking a fake shows it false and the trail stays put");}
                if(link+1<chain.Count){s.ConfirmVisionsOf(Npc(s,chain[link+1]));Check(s.State.CluesRead==link,where+"the trail is followed in order");}
                s.ConfirmVisionsOf(Npc(s,chain[link]));
                Check(s.State.CluesRead==link+1,where+"finding the real clue holder moves the trail on to link "+(link+1));
            }
            Check(s.State.GateOpened&&!s.State.Visions.Exists(v=>v.Kind=="Clue"),where+"the last link reveals the lost friend, and the trail's visions clear");
            Check(secrets.TrueForAll(id=>s.State.Visions.Exists(v=>v.Id==id)),where+"secret sights stay for the whole night");
            Check(Act(s,p.Id,"FindFriend").Accepted,where+"then the crew can reach the friend");
        }
    }

    static void TheTotemsAreGone()
    {
        int seed=0;var s=Dosed(1,1,ref seed);var tripper=Tripper(s);tripper.X=16;tripper.Z=-4;
        Check(Act(s,tripper.Id,"ReadClue").Reason=="Unknown action","the totem clue read is gone");
        // The old rule opened the way after two clues and a good dance; now only the trail's last link does.
        s.State.CluesRead=2;var npc=Wooks(s)[0];tripper.X=npc.X;tripper.Z=npc.Z-1;
        Check(Act(s,tripper.Id,"Dance",npc.Id).Accepted,"setup: the tripper dances with a festivalgoer");
        var dance=s.Interaction(tripper.InteractionId);
        foreach(var note in RhythmChart.Create(dance.ChartSeed,dance.NoteCount,dance.BeatSeconds).Notes)dance.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
        for(int guard=0;dance.Status=="Active"&&guard<200;guard++)s.Tick(.1);
        Check(dance.Score>.99&&!s.State.GateOpened,"a perfect dance no longer opens the way to the friend");
    }

    static void GuidanceFollowsTheTrail()
    {
        string Say(RoundState state,PlayerState p)=>FestivalGuidance.Headline(state,p)+" / "+FestivalGuidance.Hint(state,p);
        int seed=0;var s=Dosed(1,1,ref seed);var tripper=Tripper(s);var friend=s.State.Players.Find(p=>p!=tripper);
        Check(FestivalGuidance.Headline(s.State,tripper).Contains("CLUE TRAIL 0 / 2"),"the night's headline counts the trail: "+Say(s.State,tripper));
        // The one hint, "Trust, but verify.", is the HUD's once per level (HudTripperTests); the guidance points at the visions.
        Check(FestivalGuidance.Hint(s.State,tripper)=="Your visions mark the next clue holder.","the tripper is pointed at their visions: "+Say(s.State,tripper));
        Check(FestivalGuidance.Hint(s.State,friend).Contains(tripper.Name)&&!Say(s.State,friend).Contains("Trust"),"a sober friend is sent to the tripper: "+Say(s.State,friend));
        string night=Say(s.State,tripper)+" "+Say(s.State,friend);
        s.ConfirmVisionsOf(Npc(s,s.State.ClueChain[0]));
        Check(FestivalGuidance.Headline(s.State,friend).Contains("CLUE TRAIL 1 / 2"),"the headline follows the trail: "+Say(s.State,friend));
        var day=Dosed(1,0,ref seed);var dayTripper=Tripper(day);string daytime=Say(day.State,dayTripper);
        Check(FestivalGuidance.Hint(day.State,dayTripper)=="Your visions mark buyers and narcs."&&!FestivalGuidance.Headline(day.State,dayTripper).Contains("TRAIL"),"by day the tripper reads buyers and narcs, not a trail: "+daytime);
        Check(!(night+daytime).ToLowerInvariant().Contains("totem"),"no totems left in the guidance: "+night+" "+daytime);
    }

    static void VisionsSurviveSnapshots()
    {
        int seed=0;var s=Dosed(4,1,ref seed);Check(s.State.Visions.Count>0,"setup: a dose-4 night has visions");s.State.Visions[0].Confirmed=true;
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));var r=restored.State;
        string Visions(RoundState x)=>string.Join(";",x.Visions.ConvertAll(v=>v.Id+v.Kind+v.NpcId+v.X+v.Z+v.IsTrue+v.Tell+v.Confirmed));
        Check(Visions(r)==Visions(s.State)&&string.Join(",",r.ClueChain)==string.Join(",",s.State.ClueChain)&&string.Join(",",r.Npcs.ConvertAll(n=>n.Role))==string.Join(",",s.State.Npcs.ConvertAll(n=>n.Role)),"visions, the clue chain and the roles survive a snapshot");
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(new FestivalSimulation(5).State,Json)).AsObject();legacy.Remove("Visions");legacy.Remove("ClueChain");
        foreach(var npc in legacy["Npcs"].AsArray())npc.AsObject().Remove("Role");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.Visions.Count==0&&old.State.ClueChain.Count==0&&old.State.Npcs.TrueForAll(n=>n.Role==""),"a snapshot from before roles restores with none dealt");
        foreach(var key in new[]{"Visions","ClueChain"})
        {
            var broken=JsonNode.Parse(legacy.ToJsonString()).AsObject();broken[key]=null;bool refused=false;
            try{new FestivalSimulation().Restore(JsonSerializer.Deserialize<RoundState>(broken.ToJsonString(),Json));}catch(ArgumentException){refused=true;}
            Check(refused,"a snapshot with a missing "+key+" list is refused");
        }
    }
}
