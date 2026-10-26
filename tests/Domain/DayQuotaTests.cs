using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// LOOP-2: a day is the hustle. The crew sells its quota before sundown; once it is met anyone can head back to camp early.
public static class DayQuotaTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("DayQuota: "+message);}
    static CommandResult Act(FestivalSimulation s,string player,string kind,string target="",string item="")=>s.Execute(player,new GameCommand{Id="quota_"+(++sequence),Kind=kind,TargetId=target,ItemId=item});
    // A crew playing the given level of the first festival, with three willing buyers 10 m apart and no cops or watchers.
    static FestivalSimulation Crew(int players,int level=0)
    {
        var s=new FestivalSimulation(11);for(int i=0;i<players;i++)s.AddPlayer("p"+i,"P"+i);
        s.State.LevelIndex=level;s.State.Phase="Playing";s.State.Npcs.Clear();
        for(int i=0;i<3;i++)s.State.Npcs.Add(new NpcState{Id="buyer_"+i,X=10*i-10,Z=0,Yaw=180});
        return s;
    }
    static void AtGate(PlayerState p){p.X=Festivals.CampGateX;p.Z=Festivals.CampGateZ;}
    static void Sundown(FestivalSimulation s){s.State.ElapsedSeconds=s.State.DurationSeconds-.05;s.Tick(.1);}
    // Sell one stock to a buyer, played perfectly (perform false picks every awkward reply, a botched sale). Returns the cash it paid.
    static int Sell(FestivalSimulation s,string id,string buyer,bool perform=true,int ask=FestivalSimulation.FairAsk)
    {
        var p=s.Player(id);var npc=s.State.Npcs.Find(n=>n.Id==buyer);p.X=npc.X;p.Z=npc.Z-1;p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        int before=p.Cash;Check(s.Execute(id,new GameCommand{Id="quota_"+(++sequence),Kind="StartSale",TargetId=buyer,ItemId="stock_lsd",Amount=ask}).Accepted,id+" starts a sale to "+buyer);
        var sale=s.Interaction(p.InteractionId);
        for(int guard=0;sale.Status=="Active"&&guard<200;guard++){if(perform)Perfectly(s,sale);else if(sale.Turns.Count>0){var turn=sale.Turns[sale.Turns.Count-1];Check(s.Execute(id,new GameCommand{Id="quota_"+(++sequence),Kind="Reply",Amount=Array.IndexOf(turn.Grades,ConversationLines.Awkward),Direction=sale.Turns.Count-1}).Accepted,"reply awkwardly to the buyer");}s.Tick(.1);}
        Check(sale.Status=="Complete","the sale to "+buyer+" settles");return p.Cash-before;
    }
    // DIALOG-3: plays a live talk perfectly as far as it has got: every note of a trust dance, then the smooth reply each turn.
    static void Perfectly(FestivalSimulation s,InteractionState i)
    {
        if(i.Status=="Active"&&i.TrustDance&&i.Turns.Count==0&&i.Inputs.Count==0)foreach(var note in RhythmChart.For(i).Notes)i.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
        while(i.Status=="Active"&&i.Turns.Count>0)
        {
            var t=i.Turns[i.Turns.Count-1];
            Check(s.Execute(i.PlayerId,new GameCommand{Id="quota_"+(++sequence),Kind="Reply",Amount=Array.IndexOf(t.Grades,ConversationLines.Smooth),Direction=i.Turns.Count-1}).Accepted,"a smooth reply");
        }
    }

    public static void Run()
    {
        QuotaScalesWithCrewAndTable();
        SalesCountTowardTheQuota();
        PushedAsksCountWhatTheyPaid();
        EverySaleEndsOneClearWay();
        ExtractWaitsForTheQuota();
        SundownSettlesTheDay();
        NobodyIsLostByDay();
        SalesResetEachLevel();
        SnapshotsKeepTheDaysSales();
        APerfectCrewCanMeetEveryQuota();
        EveryDayAsksTheTablesQuota();
        EncoreLapsPayLikeTheirQuota();
        TestModeLiftsTheLimitsInATestBuild();
        TestModeIsTheTestBuildHostsAlone();
    }

    static void QuotaScalesWithCrewAndTable()
    {
        Check(FestivalSimulation.DayQuota(Crew(1).State)==15,"a solo Day 1 at the first festival owes $15");
        Check(FestivalSimulation.DayQuota(Crew(2).State)==30,"two friends owe $15 each");
        var s=Crew(3);Check(FestivalSimulation.DayQuota(s.State)==45,"three friends owe $45");
        s.Disconnect("p2");Check(FestivalSimulation.DayQuota(s.State)==30,"the quota follows the crew that is still connected");
        Check(FestivalSimulation.DayQuota(new FestivalSimulation(3).State)==15,"a crew of nobody still owes one share");
        Check(FestivalSimulation.DayQuota(Crew(2,2).State)==40,"Day 2 raises the table quota to $20 each");
        s=Crew(4);s.State.FestivalIndex=1;s.State.EncoreTier=1;
        Check(FestivalSimulation.DayQuota(s.State)==112,"Ember Playa Day 1 on an encore lap: round(22 x 1.25) = $28 each, times four");
        s.State.LevelIndex=2;Check(FestivalSimulation.DayQuota(s.State)==152,"Day 2 on that lap: round(30 x 1.25) = $38 each, times four");
        // A spirit's view lists only spirits, so a client reads the crew count the host sends with it.
        var view=new RoundState{ConnectedCrewCount=3};view.Players.Add(new PlayerState{Id="ghost",Life="Spirit"});
        Check(FestivalSimulation.DayQuota(view)==45,"a client's view counts the crew the host reports");
    }

    static void SalesCountTowardTheQuota()
    {
        var s=Crew(2);var a=s.Player("p0");
        Check(s.State.LevelSales==0,"a day starts with no sales");
        int paid=Sell(s,"p0","buyer_0");Check(paid>0&&s.State.LevelSales==paid,"a sale's payout counts toward the quota");
        paid+=Sell(s,"p1","buyer_1");Check(s.State.LevelSales==paid,"every crew member's sales count");
        Check(Sell(s,"p0","buyer_2",perform:false)==0&&s.State.LevelSales==paid&&a.Inventory.Exists(i=>i.ItemId=="stock_lsd"),"a botched sale pays nothing, counts nothing and hands the stock back");
        int cash=a.Cash;a.X=-28;a.Z=16;Check(Act(s,"p0","LostProperty").Accepted,"lost-property task starts");s.Tick(5.1);
        Check(a.Cash==cash+5&&s.State.LevelSales==paid,"lost-property cash is not a sale");
    }

    // A High or Greedy sale counts what it paid toward the quota: $15 and $20 at dose 1, played perfectly.
    static void PushedAsksCountWhatTheyPaid()
    {
        var s=Crew(1);
        int high=Sell(s,"p0","buyer_0",ask:FestivalSimulation.HighAsk);Check(high==15&&s.State.LevelSales==15,"a perfect High sale pays $15 and counts $15, got "+high+" and "+s.State.LevelSales);
        int greedy=Sell(s,"p0","buyer_1",ask:FestivalSimulation.GreedyAsk);Check(greedy==20&&s.State.LevelSales==35,"a perfect Greedy sale pays $20 and counts it too, got "+greedy+" and "+s.State.LevelSales);
        Check(FestivalSimulation.DayQuotaMet(s.State),"two pushed sales meet a solo Day 1's $15 quota twice over");
    }

    // Random sales on a real Palm Mirage Day 1, on fixed seeds: its cops, narcs and buyers, wary festivalgoers' trust dances
    // danced perfectly or skipped (a talk follows either way), random asks and replies with time between them, walk-offs, and hands
    // that fill mid-talk. Every sale ends one way, says so and leaves nothing active: paid (cash and SALES up by what the notice says,
    // the unit gone), handed back (the unit in your hand again, equipped if it was), dropped at your feet, or busted (detained, the
    // stock gone).
    static void EverySaleEndsOneClearWay()
    {
        var endings=new Dictionary<string,int>{["paid"]=0,["back"]=0,["walked off"]=0,["dropped"]=0,["busted"]=0};
        foreach(int seed in new[]{3,17,29,41,53,67})
        {
            var s=new FestivalSimulation(seed);var p=s.AddPlayer("p0","P0");p.X=0;p.Z=19;Act(s,"p0","Ready");s.Tick(5.2+FestivalSimulation.SpinSeconds+.1);Act(s,"p0","MapReady");
            Check(s.State.Phase=="Playing","setup: seed "+seed+"'s Day 1 is under way");s.State.DurationSeconds=1e6; // no sundown mid-fuzz
            var random=new Random(seed);
            for(int sale=0;sale<150&&s.State.Phase=="Playing";sale++)
            {
                // Back on your feet with a clean slate (buyers take any number of sales here): one unit or two of a kind, in hand or
                // not, and a merch bag.
                p.Life="Alive";p.Health=100;p.InteractionId="";foreach(var n in s.State.Npcs){n.Sales=0;n.Evidence.RemoveAll(e=>e.PlayerId==p.Id);foreach(var o in n.Observers)o.Suspicion=random.Next(50);}
                string item=random.Next(2)==0?"stock_lsd":"stock_mushrooms";p.Inventory.Clear();p.Inventory.Add(new ItemStack{ItemId=item,Count=1+random.Next(2)});p.Inventory.Add(new ItemStack{ItemId="merch_bag",Count=1});
                p.EquippedItemId=random.Next(3)==0?"merch_bag":item;
                // Half the offers go to a buyer, the rest to anyone: a regular turns it down, a narc busts you.
                var npcs=s.State.Npcs.FindAll(n=>n.Kind=="Wook"&&(random.Next(2)==0||n.Role=="Buyer"));var npc=npcs[random.Next(npcs.Count)];p.X=npc.X;p.Z=npc.Z-1;
                int cash=p.Cash,sales=s.State.LevelSales,units=p.Inventory.Find(x=>x.ItemId==item).Count,drops=s.State.Drops.FindAll(d=>d.ItemId==item).Count;string held=p.EquippedItemId;
                var started=s.Execute("p0",new GameCommand{Id="quota_"+(++sequence),Kind="StartSale",TargetId=npc.Id,ItemId=item,Amount=random.Next(3)});
                if(!started.Accepted)continue;
                var i=s.Interaction(p.InteractionId);bool dance=random.Next(3)>0;
                if(i==null){Check(p.Life=="Detained"&&!p.Inventory.Exists(x=>x.ItemId==item),"seed "+seed+": a sale that ends at the offer is a narc's bust");endings["busted"]++;continue;}
                for(int guard=0;i.Status=="Active"&&guard<400;guard++)
                {
                    int roll=random.Next(24);
                    if(FestivalSimulation.TrustDancing(i)){if(i.Inputs.Count==0&&dance)foreach(var note in RhythmChart.For(i).Notes)i.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});}
                    else if(roll==0)Act(s,"p0","Cancel");
                    else if(roll==1){foreach(var gear in new[]{"map","confetti","stage_pass"})if(p.Inventory.FindAll(x=>x.ItemId!="little_spoon").Count<3&&!p.Inventory.Exists(x=>x.ItemId==gear))p.Inventory.Add(new ItemStack{ItemId=gear,Count=1});}
                    else if(i.Turns.Count>0&&i.Turns[i.Turns.Count-1].Pick<0&&roll<18)s.Execute("p0",new GameCommand{Id="quota_"+(++sequence),Kind="Reply",Amount=random.Next(3),Direction=i.Turns.Count-1});
                    if(i.Status=="Active")s.Tick(.1+random.Next(5)*.2);
                }
                Check(i.Status!="Active"&&p.InteractionId!=i.Id,"seed "+seed+" sale "+sale+": the sale ends ("+i.Status+")");
                var outcome=p.ConversationOutcomes?.Find(o=>o.InteractionId==i.Id);string notice=outcome?.Notice??"";
                int left=p.Inventory.Find(x=>x.ItemId==item)?.Count??0;bool dropped=s.State.Drops.FindAll(d=>d.ItemId==item).Count>drops;
                string where="seed "+seed+" sale "+sale+" ("+i.Status+", \""+notice+"\")";
                if(p.Life=="Detained"){Check(left==0&&!dropped&&outcome?.Result=="BUSTED"&&notice.StartsWith("BUSTED"),where+": a bust takes the stock and says BUSTED");endings["busted"]++;}
                else if(p.Cash>cash)
                {
                    Check(p.Cash-cash==s.State.LevelSales-sales&&left==units-1&&!dropped,where+": a paid sale counts its pay and takes one unit");
                    Check(outcome?.Result=="SOLD +$"+(p.Cash-cash)&&notice.StartsWith("SOLD ")&&notice.Contains("+$"+(p.Cash-cash)),where+": a paid sale says SOLD and its pay, +$"+(p.Cash-cash));endings["paid"]++;
                }
                else if(dropped){Check(left==units-1&&s.State.LevelSales==sales&&notice.EndsWith("dropped at your feet")&&outcome.Result=="NO SALE",where+": a unit with no room is at your feet, and it says so");endings["dropped"]++;}
                else
                {
                    Check(left==units&&p.Cash==cash&&s.State.LevelSales==sales&&notice.EndsWith("back in your hand")&&outcome.Result=="NO SALE",where+": otherwise the unit is back, and it says so");
                    Check(held!=item||p.EquippedItemId==item,where+": a unit offered from your hand is in your hand again, not "+p.EquippedItemId);endings[notice.StartsWith("You walked off")||notice.StartsWith("They walk off")?"walked off":"back"]++;
                }
            }
        }
        foreach(var ending in endings)Check(ending.Value>0,"the fuzz reaches every ending, but none was "+ending.Key+": "+string.Join(", ",endings));
    }

    static void ExtractWaitsForTheQuota()
    {
        var s=Crew(2);var a=s.Player("p0");AtGate(a);
        var refused=Act(s,"p0","Extract");Check(!refused.Accepted&&refused.Reason.Contains("$30"),"no early return before the quota, and the crew hears it is $30 short");
        Sell(s,"p1","buyer_0");Sell(s,"p1","buyer_0");
        refused=Act(s,"p0","Extract");Check(!refused.Accepted&&refused.Reason.Contains("$10"),"still $10 short after two sales");
        Sell(s,"p1","buyer_1");Check(s.State.LevelSales==30,"setup: exactly the quota");
        a.X=Festivals.CampGateX+4;Check(!Act(s,"p0","Extract").Accepted,"the early return starts at the way back to camp");
        AtGate(a);Check(Act(s,"p0","Extract").Accepted,"with the quota met, a friend who sold nothing can head back");
        s.Tick(3.1);Check(s.State.Phase=="Results"&&s.State.Result=="Success","heading back ends the day with Success");
        var night=Crew(2,1);night.State.LevelSales=500;AtGate(night.Player("p0"));
        Check(!Act(night,"p0","Extract").Accepted,"a night still needs the lost friend at the gate, whatever was sold");
    }

    static void SundownSettlesTheDay()
    {
        var s=Crew(2);s.State.LevelSales=30;Sundown(s);
        Check(s.State.Phase=="Results"&&s.State.Result=="Success","sundown with the quota met is a Success");
        s=Crew(2);s.State.LevelSales=29;s.Tick(1);Check(s.State.Phase=="Playing","a short day keeps going until sundown");
        Sundown(s);Check(s.State.Phase=="Results"&&s.State.Result=="Missed the quota","sundown $1 short misses the quota");
        s=Crew(2,1);s.State.LevelSales=500;Sundown(s);Check(s.State.Result=="Time expired","a night still ends when time runs out");
        s=Crew(2);s.State.LevelSales=30;foreach(var p in s.State.Players)p.Life="Spirit";s.Tick(.1);
        Check(s.State.Phase=="Results"&&s.State.Result!="Success","a crew with nobody left standing loses the day, quota or not");
    }

    static void NobodyIsLostByDay()
    {
        for(int level=0;level<Festivals.LevelCount;level++)
        {
            var s=Crew(2,level);var a=s.Player("p0");s.State.GateOpened=true;a.X=s.State.FriendPosition.X;a.Z=s.State.FriendPosition.Z;
            bool night=Festivals.For(s.State).Night;string where=Festivals.For(s.State).Name;
            Check(Act(s,"p0","FindFriend").Accepted==night,where+(night?": the lost friend can be found":": nobody is lost by day, even with the old gate open"));
        }
    }

    static void SalesResetEachLevel()
    {
        foreach(bool met in new[]{true,false})
        {
            var s=Crew(2);int paid=Sell(s,"p0","buyer_0");int cash=s.Player("p0").Cash;s.State.LevelSales=met?30:paid;Sundown(s);
            var back=s.Execute("p0",new GameCommand{Id="quota_"+(++sequence),Kind="Reset"});
            Check(back.Accepted&&s.State.Phase=="CampReview","host brings the crew back");
            // The review card says where the crew is; a notice saying it again covered the card's heading.
            Check(back.Reason=="Accepted","bringing the crew back leaves no notice over the review card: "+back.Reason);
            Check(s.State.LevelSales==0,"the next level starts with no sales ("+(met?"after a win":"after a miss")+")");
            if(met)Check(s.Player("p0").Cash==cash&&Festivals.For(s.State).Name=="Night 1","the cash itself carries into Night 1");
            else Check(s.Player("p0").Cash==20&&Festivals.For(s.State).Name=="Day 1","missing the quota restarts Day 1 with a fresh $20");
        }
    }

    static void SnapshotsKeepTheDaysSales()
    {
        var s=Crew(1);s.State.LevelSales=12;
        var saved=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();Check(saved.ContainsKey("LevelSales"),"LevelSales is a serialised public field");
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(saved.ToJsonString(),Json));
        Check(restored.State.LevelSales==12,"a snapshot keeps the day's sales");
        saved.Remove("LevelSales");restored.Restore(JsonSerializer.Deserialize<RoundState>(saved.ToJsonString(),Json));
        Check(restored.State.LevelSales==0,"a snapshot from before quotas restores with no sales yet");
        var bad=JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json);bad.LevelSales=-1;
        bool refused=false;try{new FestivalSimulation().Restore(bad);}catch(ArgumentException){refused=true;}
        Check(refused,"a snapshot with negative sales is refused");
    }

    // Every day can be won at any crew size and encore lap. ECON-2: a crew that leaves camp with its $20 each and no stock, buys
    // every unit from the level's festivalgoers and sells it perfectly to the level's real buyers at dose 1 meets the quota.
    // Nobody is watching, and nobody sells inside a VIP zone, so nothing pays extra. Lists every day that cannot be won.
    static void APerfectCrewCanMeetEveryQuota()
    {
        var misses=new List<string>();
        for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<Festivals.LevelCount;level+=2)for(int tier=0;tier<=4;tier++)for(int crew=1;crew<=8;crew++)
        {
            var s=Launch(crew,festival,level,tier);SellTheQuota(s);int quota=FestivalSimulation.DayQuota(s.State);
            if(s.State.LevelSales<quota)misses.Add(Festivals.Name(festival)+" "+Festivals.For(s.State).Name+", crew of "+crew+", encore "+tier+": sold $"+s.State.LevelSales+" of a $"+quota+" quota");
        }
        Check(misses.Count==0,"a perfect crew at dose 1 cannot meet "+misses.Count+" day quotas:\n"+string.Join("\n",misses));
    }

    // The binding rule: a day asks the table's quota per crew member, times the crew, times the encore lap's scale, for every
    // crew the game allows (one to eight) and every lap. Nothing caps it; the pay scales instead (EncoreLapsPayLikeTheirQuota).
    static void EveryDayAsksTheTablesQuota()
    {
        for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<Festivals.LevelCount;level+=2)for(int tier=0;tier<=4;tier++)for(int crew=1;crew<=8;crew++)
        {
            var s=Crew(crew,level);s.State.FestivalIndex=festival;s.State.EncoreTier=tier;
            Check(FestivalSimulation.DayQuota(s.State)==Festivals.For(s.State).QuotaPerCrew*crew,Festivals.Name(festival)+" "+Festivals.For(s.State).Name+" on encore "+tier+" asks the table's quota of a crew of "+crew+", got $"+FestivalSimulation.DayQuota(s.State));
        }
        var eight=Crew(8,2);eight.State.FestivalIndex=1;
        Check(FestivalSimulation.DayQuota(eight.State)==240,"eight friends on Ember Playa's Day 2 owe $30 each");
    }

    // An encore lap asks x(1 + .25 t) of each crew member and a sale pays x(1 + .25 t) too, so every lap stays winnable
    // (APerfectCrewCanMeetEveryQuota) while the lap's narcs, suspicion and trails make it harder.
    static void EncoreLapsPayLikeTheirQuota()
    {
        Check(Sell(Crew(2),"p0","buyer_0")==10,"a perfect sale pays $10 on the first lap");
        var s=Crew(2);s.State.EncoreTier=1;Check(Sell(s,"p0","buyer_0")==13,"and round(10 x 1.25) = $13 on the first encore lap");
        s=Crew(2);s.State.EncoreTier=4;Check(Sell(s,"p0","buyer_0")==20,"and $20 on the fourth, twice the first lap's pay");
    }

    // A real launch from camp onto a day level, every player with the $20 they start with and no stock (no shelf sells it), the
    // tripper at dose 1 with no double buyer, no cops, and anyone standing in a VIP zone walked out just south of its rope.
    static FestivalSimulation Launch(int crew,int festival,int level,int tier)
    {
        var s=new FestivalSimulation(21+crew);for(int i=0;i<crew;i++)s.AddPlayer("p"+i,"P"+i);
        s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.EncoreTier=tier;s.State.UnlockedFestivalCount=Festivals.Count;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,p.Id+" readies at the trailhead");}
        s.Tick(5.2+FestivalSimulation.SpinSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing"&&Festivals.For(s.State).Name==(level==0?"Day 1":"Day 2"),"setup: the day is under way");
        Check(s.State.Players.TrueForAll(p=>p.Cash==20&&p.Inventory.Count==0),"setup: the crew leaves camp with $20 each and nothing else");
        foreach(var d in s.State.Doses)d.Dose=1;s.State.Visions.RemoveAll(v=>v.Kind=="DoubleBuyer");
        s.HasLineOfSight=(ax,az,bx,bz)=>false;s.State.Npcs.RemoveAll(n=>n.Kind=="Cop");
        foreach(var n in s.State.Npcs)if(Festivals.InVipZone(festival,n.X,n.Z))n.Z=Festivals.VipZones[0].MinZ-1.5f;
        return s;
    }
    // Rounds of every friend with nothing to sell perfectly buying one unit from a free festivalgoer, then each perfectly selling
    // it to a free buyer, until the quota is met or nobody sells: the sellers ran out, or every buyer they can reach has bought
    // all they will. The crew asks anyone it knows is neither a narc nor a buyer, and the host turns down whoever doesn't sell.
    static void SellTheQuota(FestivalSimulation s)
    {
        var done=new HashSet<string>();var dry=new HashSet<string>();
        for(int round=0;round<100&&!FestivalSimulation.DayQuotaMet(s.State);round++)
        {
            var buys=new List<InteractionState>();
            foreach(var p in s.State.Players)
            {
                if(p.Inventory.Count>0)continue;
                foreach(var seller in s.State.Npcs)
                {
                    if(seller.Kind!="Wook"||seller.Role=="Narc"||seller.Role=="Buyer"||dry.Contains(seller.Id)||buys.Exists(x=>x.TargetId==seller.Id))continue;
                    p.X=seller.X;p.Z=seller.Z-.5f;
                    if(!Act(s,p.Id,"StartBuy",seller.Id,FestivalSimulation.SellerStock(seller.Id)).Accepted){dry.Add(seller.Id);continue;}
                    buys.Add(Perfect(s,p));break;
                }
            }
            Settle(s,buys,"buy");
            var sales=new List<InteractionState>();
            foreach(var p in s.State.Players)
            {
                var stock=p.Inventory.Find(i=>i.ItemId=="stock_lsd"||i.ItemId=="stock_mushrooms");if(stock==null)continue;
                foreach(var buyer in s.State.Npcs)
                {
                    if(buyer.Role!="Buyer"||done.Contains(buyer.Id)||sales.Exists(x=>x.TargetId==buyer.Id))continue;
                    p.X=buyer.X;p.Z=buyer.Z-.5f;if(Festivals.InVipZone(s.State.FestivalIndex,p.X,p.Z)){done.Add(buyer.Id);continue;}
                    if(!Act(s,p.Id,"StartSale",buyer.Id,stock.ItemId).Accepted){done.Add(buyer.Id);continue;}
                    sales.Add(Perfect(s,p));break;
                }
            }
            if(sales.Count==0)return;
            Settle(s,sales,"sale");
        }
    }
    // p's talk, to be played perfectly.
    static InteractionState Perfect(FestivalSimulation s,PlayerState p)=>s.Interaction(p.InteractionId);
    static void Settle(FestivalSimulation s,List<InteractionState> challenges,string what)
    {
        for(int guard=0;challenges.Exists(x=>x.Status=="Active")&&guard<200;guard++){foreach(var x in challenges)Perfectly(s,x);s.Tick(.1);}
        Check(!challenges.Exists(x=>x.Status!="Complete"),"every perfect "+what+" settles");
    }

    // Read through the JSON a snapshot is, so TEST MODE must be a public field that a view and a save both carry.
    static bool TestMode(FestivalSimulation s)=>JsonNode.Parse(JsonSerializer.Serialize(s.State,Json))["TestMode"]?.GetValue<bool>()==true;
    // DevelopmentFestivalSelection is the test build's capability: only FestivalHostBootstrap turns it on, in Editor and development builds.
    static void TestModeLiftsTheLimitsInATestBuild()
    {
        var s=Crew(2);s.DevelopmentFestivalSelection=true;Check(s.State.HostPlayerId=="p0","setup: p0 hosts");
        Check(!TestMode(s),"a test build's game starts with test mode off");
        Check(Act(s,"p0","TestMode").Accepted&&TestMode(s),"the host turns test mode on");
        double length=s.State.DurationSeconds;s.State.ElapsedSeconds=length-.05;s.Tick(.1);s.Tick(30);
        Check(s.State.Phase=="Playing"&&s.State.ElapsedSeconds>length+29,"30 s past the level's length the clock still runs and the level plays on ("+s.State.Phase+", "+s.State.Result+")");
        var held=s.Player("p1");held.Life="Detained";held.X=Festivals.SecurityBuilding.CentreX;held.Z=Festivals.SecurityBuilding.CentreZ;held.EscapeProgress=2;
        s.State.Npcs.Add(new NpcState{Id="cop_test",Kind="Cop",Mode="Patrol",X=-30,Z=-30});s.State.Npcs[s.State.Npcs.Count-1].Evidence.Add(new EvidenceState{PlayerId="p1",Kind="WitnessedDeal"});
        Check(Act(s,"p1","TestFree").Accepted,"a detained friend frees themselves, not only the host");
        Check(held.Life=="Alive"&&held.X==24&&held.Z==2&&held.EscapeProgress==0&&held.RecoveryUntil>s.State.SimulationSeconds,"freed as a release frees: alive at 24,2 with a short recovery ("+held.Life+" at "+held.X+", "+held.Z+")");
        Check(!s.State.Npcs.Exists(n=>n.Evidence.Exists(e=>e.PlayerId=="p1")),"and the evidence against them is cleared");
        Check(!Act(s,"p1","TestFree").Accepted,"a free player has nothing to be freed from");
        held.Life="Downed";Check(!Act(s,"p1","TestFree").Accepted&&held.Life=="Downed","a downed player is not freed");held.Life="Alive";
        Check(s.State.LevelSales==0&&s.State.LevelIndex==0,"setup: Day 1 with no sales");
        Check(Act(s,"p0","TestClearLevel").Accepted&&s.State.Phase=="Results"&&s.State.Result=="Success","the host clears the day as a success, quota or not ("+s.State.Result+")");
        Check(!s.State.Players.Exists(p=>p.HasCosmetic),"a level cleared in test mode earns no cosmetic");
        Check(!Act(s,"p0","TestClearLevel").Accepted,"a cleared level is not cleared again");
        Check(Act(s,"p0","Reset").Accepted&&s.State.LevelIndex==1,"the crew moves on to the next level, Night 1 (level "+s.State.LevelIndex+")");
        Check(TestMode(s),"test mode carries on into the next level");
        Check(!Act(s,"p0","TestClearLevel").Accepted&&s.State.Phase=="CampReview","the host cannot clear a level from camp ("+s.State.Phase+")");
        s.Player("p1").Life="Detained";Check(!Act(s,"p1","TestFree").Accepted,"nobody is freed at camp");
        var off=Crew(2);off.DevelopmentFestivalSelection=true;Act(off,"p0","TestMode");off.State.ElapsedSeconds=off.State.DurationSeconds+20;off.Tick(.1);
        Check(off.State.Phase=="Playing","setup: past the limit in test mode, still playing");
        Check(Act(off,"p0","TestMode").Accepted&&!TestMode(off),"the host turns test mode off");
        off.Tick(.1);Check(off.State.Phase=="Results"&&off.State.Result=="Missed the quota","then the time limit is back: the day ends, short of its quota ("+off.State.Result+")");
    }
    // The commands are the host's security boundary: a release build's host refuses all three, a client may only free themselves,
    // and a snapshot with test mode on restores only in a test build.
    static void TestModeIsTheTestBuildHostsAlone()
    {
        var release=Crew(2);release.Player("p1").Life="Detained";
        foreach(var (player,kind) in new[]{("p0","TestMode"),("p1","TestFree"),("p0","TestClearLevel")})
            Check(!Act(release,player,kind).Accepted,"a release build refuses "+player+"'s "+kind);
        Check(!TestMode(release)&&release.Player("p1").Life=="Detained"&&release.State.Phase=="Playing","nothing changed in the release build");
        Sundown(release);Check(release.State.Phase=="Results","and its clock still ends the level");
        var test=Crew(2);test.DevelopmentFestivalSelection=true;
        Check(!Act(test,"p1","TestMode").Accepted&&!TestMode(test),"a client cannot turn test mode on");
        Check(!Act(test,"p0","TestClearLevel").Accepted&&test.State.Phase=="Playing","with test mode off the host cannot clear the level");
        test.Player("p1").Life="Detained";Check(!Act(test,"p1","TestFree").Accepted,"with test mode off nobody frees themselves");
        Act(test,"p0","TestMode");Check(TestMode(test),"setup: the host turns it on");
        Check(!Act(test,"p1","TestMode").Accepted&&TestMode(test),"a client cannot turn it off");
        Check(!Act(test,"p1","TestClearLevel").Accepted&&test.State.Phase=="Playing","a client cannot clear the level");
        var saved=JsonNode.Parse(JsonSerializer.Serialize(test.State,Json)).AsObject();
        var dev=new FestivalSimulation{DevelopmentFestivalSelection=true};dev.Restore(JsonSerializer.Deserialize<RoundState>(saved.ToJsonString(),Json));
        Check(TestMode(dev),"a test build restores a snapshot in test mode");
        bool refused=false;try{new FestivalSimulation().Restore(JsonSerializer.Deserialize<RoundState>(saved.ToJsonString(),Json));}catch(ArgumentException){refused=true;}
        Check(refused,"a release build refuses a snapshot in test mode");
        saved.Remove("TestMode");var older=new FestivalSimulation();older.Restore(JsonSerializer.Deserialize<RoundState>(saved.ToJsonString(),Json));
        Check(!TestMode(older),"a snapshot from before test mode restores in a release build, with it off");
    }
}
