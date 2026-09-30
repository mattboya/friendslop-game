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
    // Sell one stock to a buyer, hitting every note (inputs are optional: none botches the sale). Returns the cash it paid.
    static int Sell(FestivalSimulation s,string id,string buyer,bool perform=true)
    {
        var p=s.Player(id);var npc=s.State.Npcs.Find(n=>n.Id==buyer);p.X=npc.X;p.Z=npc.Z-1;p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        int before=p.Cash;Check(Act(s,id,"StartSale",buyer,"stock_lsd").Accepted,id+" starts a sale to "+buyer);
        var sale=s.Interaction(p.InteractionId);
        if(perform)foreach(var note in RhythmChart.Create(sale.ChartSeed,sale.NoteCount,sale.BeatSeconds).Notes)sale.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
        for(int guard=0;sale.Status=="Active"&&guard<200;guard++)s.Tick(.1);
        Check(sale.Status=="Complete","the sale to "+buyer+" settles");return p.Cash-before;
    }

    public static void Run()
    {
        QuotaScalesWithCrewAndTable();
        SalesCountTowardTheQuota();
        ExtractWaitsForTheQuota();
        SundownSettlesTheDay();
        NobodyIsLostByDay();
        SalesResetEachLevel();
        SnapshotsKeepTheDaysSales();
        APerfectCrewCanMeetEveryQuota();
        TheTableHoldsForTheCrewItWasTunedFor();
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
        s.State.LevelIndex=2;Check(FestivalSimulation.DayQuota(s.State)<152,"Day 2's $38 each on that lap is more than four can sell, so the day asks less");
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
            Check(s.Execute("p0",new GameCommand{Id="quota_"+(++sequence),Kind="Reset"}).Accepted&&s.State.Phase=="CampReview","host brings the crew back");
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

    // Every day can be won at any crew size and encore lap. A crew that buys all the stock the camp shelf and the night market
    // carry (restocking from its takings) and sells it perfectly to the level's real buyers at dose 1 meets the quota. Nobody is
    // watching, and nobody sells inside a VIP zone, so nothing pays extra. Lists every day that cannot be won.
    static void APerfectCrewCanMeetEveryQuota()
    {
        var misses=new List<string>();
        for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<Festivals.LevelCount;level+=2)for(int tier=0;tier<=4;tier++)for(int crew=1;crew<=8;crew++)
        {
            var s=Launch(crew,festival,level,tier);SellEverything(s);int quota=FestivalSimulation.DayQuota(s.State);
            if(s.State.LevelSales<quota)misses.Add(Festivals.Name(festival)+" "+Festivals.For(s.State).Name+", crew of "+crew+", encore "+tier+": sold $"+s.State.LevelSales+" of a $"+quota+" quota");
        }
        Check(misses.Count==0,"a perfect crew at dose 1 cannot meet "+misses.Count+" day quotas:\n"+string.Join("\n",misses));
    }

    // The festival table was tuned for four and allows up to eight: at the first lap its quota stands as written for crews up to seven.
    static void TheTableHoldsForTheCrewItWasTunedFor()
    {
        for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<Festivals.LevelCount;level+=2)for(int crew=1;crew<=7;crew++)
        {
            var s=Crew(crew,level);s.State.FestivalIndex=festival;
            Check(FestivalSimulation.DayQuota(s.State)==Festivals.For(s.State).QuotaPerCrew*crew,Festivals.Name(festival)+" "+Festivals.For(s.State).Name+" asks the table's quota of a crew of "+crew);
        }
    }

    // A real launch from camp onto a day level, every player having bought what the camp shelf lets them, the tripper at dose 1
    // with no double buyer, no cops, and anyone standing in a VIP zone walked out just south of its rope.
    static FestivalSimulation Launch(int crew,int festival,int level,int tier)
    {
        var s=new FestivalSimulation(21+crew);for(int i=0;i<crew;i++)s.AddPlayer("p"+i,"P"+i);
        s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.EncoreTier=tier;s.State.UnlockedFestivalCount=Festivals.Count;
        foreach(var p in s.State.Players){BuyStock(s,p,true);p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,p.Id+" readies at the trailhead");}
        s.Tick(5.2+FestivalSimulation.SpinSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing"&&Festivals.For(s.State).Name==(level==0?"Day 1":"Day 2"),"setup: the day is under way");
        foreach(var d in s.State.Doses)d.Dose=1;s.State.Visions.RemoveAll(v=>v.Kind=="DoubleBuyer");
        s.HasLineOfSight=(ax,az,bx,bz)=>false;s.State.Npcs.RemoveAll(n=>n.Kind=="Cop");
        foreach(var n in s.State.Npcs)if(Festivals.InVipZone(festival,n.X,n.Z))n.Z=Festivals.VipZones[0].MinZ-1.5f;
        return s;
    }
    // Buys every stock item the shelf (camp) or the night market has left, while the player can pay and carry it.
    static void BuyStock(FestivalSimulation s,PlayerState p,bool camp)
    {
        foreach(var item in new[]{"stock_lsd","stock_mushrooms"})for(int guard=0;guard<20;guard++)
        {
            var shelf=Catalog.ShopPoint(camp,s.State.VendorOffers.IndexOf(item));p.X=shelf.X;p.Z=shelf.Z;
            if(camp){if(!Act(s,p.Id,"HoldOffer","",item).Accepted)break;p.X=0;p.Z=7;}
            if(!Act(s,p.Id,"Buy","",item).Accepted){if(camp)Act(s,p.Id,"ReturnOffer");break;}
        }
    }
    // Rounds of the whole crew restocking at the night market, then each seller perfectly selling one stock to a free buyer,
    // until nobody sells: stock ran out, or every buyer they can reach has bought all they will.
    static void SellEverything(FestivalSimulation s)
    {
        var done=new HashSet<string>();
        for(int round=0;round<100;round++)
        {
            var sales=new List<InteractionState>();
            foreach(var p in s.State.Players)
            {
                BuyStock(s,p,false);var stock=p.Inventory.Find(i=>i.ItemId=="stock_lsd"||i.ItemId=="stock_mushrooms");if(stock==null)continue;
                foreach(var buyer in s.State.Npcs)
                {
                    if(buyer.Role!="Buyer"||done.Contains(buyer.Id)||sales.Exists(x=>x.TargetId==buyer.Id))continue;
                    p.X=buyer.X;p.Z=buyer.Z-.5f;if(Festivals.InVipZone(s.State.FestivalIndex,p.X,p.Z)){done.Add(buyer.Id);continue;}
                    if(!Act(s,p.Id,"StartSale",buyer.Id,stock.ItemId).Accepted){done.Add(buyer.Id);continue;}
                    var sale=s.Interaction(p.InteractionId);sales.Add(sale);
                    foreach(var note in RhythmChart.Create(sale.ChartSeed,sale.NoteCount,sale.BeatSeconds).Notes)sale.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
                    break;
                }
            }
            if(sales.Count==0)return;
            for(int guard=0;sales.Exists(x=>x.Status=="Active")&&guard<200;guard++)s.Tick(.1);
            Check(!sales.Exists(x=>x.Status!="Complete"),"every perfect sale settles");
        }
    }
}
