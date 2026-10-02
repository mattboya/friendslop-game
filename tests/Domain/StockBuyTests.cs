using System;
using System.Collections.Generic;
using System.Text;
using Festival.Core;

// ECON-2: the camp and night-market shelves sell gear only. Tongue Stamps and Fun Guys come from festivalgoers through a buy, talked
// through (DIALOG-3). Regulars (and clue holders at night) sell one kind each, a few units a level; anyone else turns the ask down the
// same way, and asking a narc is a bust.
public static class StockBuyTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("StockBuy: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="")=>s.Execute(id,new GameCommand{Id="stockbuy"+(sequence++),Kind=kind,TargetId=target,ItemId=item});
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{NoShelfSellsStock,ASellerSells,TheReplyNeverTellsThemApart,AskingANarcIsABust,ACopSeesTheBuy,TheDealIsUnchanged})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" stock buy test(s) failed:\n"+string.Join("\n",failures));
    }

    // The crew readies at camp for this level of Palm Mirage, the wheels land and everyone loads into the real crowd, dealt its
    // roles. The cops are sent home; a test that wants one adds its own.
    static FestivalSimulation Start(int seed,int level,int crew=2)
    {
        var s=new FestivalSimulation(seed);for(int i=0;i<crew;i++)s.AddPlayer("p"+i,"P"+i);s.State.LevelIndex=level;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew reaches the festival");
        s.State.Npcs.RemoveAll(n=>n.Kind=="Cop");return s;
    }
    static NpcState Wook(FestivalSimulation s,string role)=>s.State.Npcs.Find(n=>n.Role==role&&n.Twist=="");
    static int Count(PlayerState p,string item)=>p.Inventory.Find(i=>i.ItemId==item)?.Count??0;
    static double Suspicion(NpcState n,PlayerState p)=>n.Observers.Find(o=>o.PlayerId==p.Id)?.Suspicion??0;
    // p asks npc for the kind it sells (or `item`), standing a metre in front of it and looking off to the side.
    static CommandResult Ask(FestivalSimulation s,PlayerState p,NpcState npc,string item=null){p.X=npc.X;p.Z=npc.Z-1;p.Yaw=90;return Act(s,p.Id,"StartBuy",npc.Id,item??FestivalSimulation.SellerStock(npc.Id));}
    const int Smooth=ConversationLines.Smooth,SoSo=ConversationLines.SoSo,Silence=ConversationLines.Silence;
    // DIALOG-3: talks p's buy through, dancing every note first if the seller asks for a trust dance, then giving a reply of each
    // grade as its turn comes (Silence lets the turn run out), and waits for it to settle.
    static InteractionState Talk(FestivalSimulation s,PlayerState p,params int[] grades)
    {
        var i=s.Interaction(p.InteractionId);
        if(i.TrustDance)foreach(var note in RhythmChart.For(i).Notes)i.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
        foreach(int grade in grades)
        {
            for(int guard=0;i.Status=="Active"&&i.Turns.Count==0&&guard<100;guard++)s.Tick(.1);
            int turn=i.Turns.Count;
            if(grade==Silence){for(int guard=0;i.Status=="Active"&&i.Turns.Count==turn&&guard<100;guard++)s.Tick(.1);continue;}
            var t=i.Turns[turn-1];Check(s.Execute(p.Id,new GameCommand{Id="stockbuy"+(sequence++),Kind="Reply",Amount=Array.IndexOf(t.Grades,grade),Direction=turn-1}).Accepted,"setup: "+p.Id+" replies");
        }
        for(int guard=0;i.Status=="Active"&&guard<300;guard++)s.Tick(.1);
        return i;
    }

    // The shelves keep the three staples and the three extras each seed always drew, and never stock. Shelves restored from an
    // older snapshot that still list stock won't sell it either, and the camp hint says where stock comes from now.
    static void NoShelfSellsStock()
    {
        for(int seed=1;seed<=200;seed++)
        {
            var offers=Catalog.VendorOffers(seed);var old=OldOffers(seed);old.RemoveAll(id=>id=="stock_lsd"||id=="stock_mushrooms");
            Check(!offers.Exists(id=>Catalog.FindItem(id).Category=="Stock"),"seed "+seed+"'s shelves hold no stock: "+string.Join(",",offers));
            Check(string.Join(",",offers)==string.Join(",",old),"seed "+seed+"'s shelves keep the gear they drew: "+string.Join(",",offers)+", was "+string.Join(",",old));
        }
        var s=new FestivalSimulation(7);var p=s.AddPlayer("p0","P0");
        s.State.VendorOffers.Add("stock_lsd");s.State.ShopStock.Add(new ShopStockState{ItemId="stock_lsd",CampAvailable=2,MarketAvailable=2});
        int index=s.State.VendorOffers.IndexOf("stock_lsd");var shelf=Catalog.ShopPoint(true,index);p.X=shelf.X;p.Z=shelf.Z;
        var held=Act(s,"p0","HoldOffer","","stock_lsd");
        Check(!held.Accepted&&held.Reason=="Stock comes from festivalgoers, not shelves"&&p.HeldOfferId=="","the camp shelf won't hand over stock: "+held.Reason);
        // Picked up before the update.
        p.HeldOfferId="stock_lsd";p.X=0;p.Z=7;var paid=Act(s,"p0","Buy","","stock_lsd");
        Check(!paid.Accepted&&p.Cash==20&&Count(p,"stock_lsd")==0,"nor will the camp seller sell it: "+paid.Reason);
        p.HeldOfferId="";s.State.Phase="Playing";var market=Catalog.ShopPoint(false,index);p.X=market.X;p.Z=market.Z;
        var bought=Act(s,"p0","Buy","","stock_lsd");
        Check(!bought.Accepted&&p.Cash==20&&Count(p,"stock_lsd")==0&&s.State.ShopStock.Find(x=>x.ItemId=="stock_lsd").MarketAvailable==2,"nor will a night-market shelf: "+bought.Reason);
        // ECON-3: the hint says it only while you hold no stock for a day, not once you carry some, nor before a night's rescue.
        s.State.Phase="Shopping";p.X=-1;p.Z=-9;var hint=FestivalGuidance.Hint(s.State,p);
        Check(hint.Contains("Camp supplies")&&hint.Contains("look at shelf props")&&hint.Contains("Stock comes from festivalgoers."),"with no stock, the camp hint says stock comes from festivalgoers: "+hint);
        p.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});hint=FestivalGuidance.Hint(s.State,p);
        Check(hint.Contains("Camp supplies")&&!hint.Contains("Stock comes from"),"with stock in hand it doesn't: "+hint);
        p.Inventory.RemoveAll(i=>i.ItemId=="stock_mushrooms");s.State.LevelIndex=1;hint=FestivalGuidance.Hint(s.State,p);
        Check(hint.Contains("Camp supplies")&&!hint.Contains("Stock comes from"),"nor before a night: "+hint);
    }
    // Catalog.VendorOffers as it was before ECON-2, stock and all, so NoShelfSellsStock can tell the shuffle draws what it drew.
    static List<string> OldOffers(int seed)
    {
        var offers=new List<string>{"little_spoon","stock_lsd","stock_mushrooms","medical_voucher","poi_practice"};
        var extras=new[]{"stage_pass","confetti","merch_bag","stash_box","poi_led"};var random=new ContentRandom(seed);
        for(int i=extras.Length-1;i>0;i--){int j=random.Next(i+1);var t=extras[i];extras[i]=extras[j];extras[j]=t;}
        for(int i=0;i<3;i++)offers.Add(extras[i]);
        return offers;
    }

    // A regular by day and a clue holder by night each sell one unit of their kind for $5, talked through well or sloppily. A
    // botched buy, a cancelled one and one walked away from cost nothing. None of it is a sale, and a seller stops after
    // StockCopies(crew).
    static void ASellerSells()
    {
        Check(FestivalSimulation.SellerStock("wook_4")=="stock_lsd"&&FestivalSimulation.SellerStock("wook_7")=="stock_mushrooms","the number in a festivalgoer's id sets their kind: even Tongue Stamps, odd Fun Guys");
        foreach(int level in new[]{0,1})
        {
            const int crew=3;var s=Start(31,level,crew);s.HasLineOfSight=(ax,az,bx,bz)=>false;var p=s.Player("p1");string role=level==0?"Regular":"ClueHolder";
            var seller=Wook(s,role);Check(seller!=null,"setup: the crowd has a "+role);
            string kind=FestivalSimulation.SellerStock(seller.Id),who=Festivals.For(s.State).Name+"'s "+role+" "+seller.Id;
            Check(p.Cash==20&&Count(p,kind)==0,"setup: "+who+"'s customer has $20 and no stock");

            // A perfect buy: the $5 is held while they talk, then spent on one unit, in hand. The buyer talks where they stand,
            // looking off to the side, unless the seller asks for a dance first: then they face them, as any dancer.
            var asked=Ask(s,p,seller);Check(asked.Accepted,who+" starts a buy: "+asked.Reason);
            var buy=s.Interaction(p.InteractionId);
            Check(p.Yaw==(buy.TrustDance?0:90),who+": the buyer "+(buy.TrustDance?"dances facing the seller":"talks looking where they looked")+", got yaw "+p.Yaw);
            Check(p.Cash==15&&buy.ReservedCash==5,who+": the $5 is held while they talk, cash $"+p.Cash);
            var perfect=Talk(s,p,Smooth,Smooth,Smooth);
            Check(perfect.Status=="Complete"&&perfect.Score>.99,"setup: "+who+": a perfect buy, score "+perfect.Score);
            Check(p.Dialogue.Seen.Exists(key=>key.StartsWith("buy_")),who+": the seller speaks lines from the buy pool");
            Check(Count(p,kind)==1&&p.Cash==15&&p.EquippedItemId==kind&&seller.StockSold==1,who+": a perfect buy hands over one unit, in hand, for $5 (cash $"+p.Cash+", "+Count(p,kind)+" held)");

            // A botched buy, a cancelled one and one walked away from all give the $5 back and no unit.
            Check(Ask(s,p,seller).Accepted,who+": a second buy starts");var missed=Talk(s,p,Silence,Silence,Silence);
            Check(missed.Score<.4&&Count(p,kind)==1&&p.Cash==15&&seller.StockSold==1,who+": a botched buy refunds the $5 (cash $"+p.Cash+")");
            Check(Ask(s,p,seller).Accepted&&p.Cash==10,who+": a third buy holds the $5");
            Check(Act(s,p.Id,"Cancel").Accepted&&p.InteractionId==""&&p.Cash==15&&Count(p,kind)==1,who+": cancelling refunds it (cash $"+p.Cash+")");
            Check(Ask(s,p,seller).Accepted&&p.Cash==10,who+": a fourth buy holds the $5");
            p.X+=10;s.Tick(.1);
            Check(p.InteractionId==""&&p.Cash==15&&Count(p,kind)==1,who+": walking off refunds it (cash $"+p.Cash+")");

            // A sloppy buy that lands still buys.
            Check(Ask(s,p,seller).Accepted,who+": a sloppy buy starts");var sloppy=Talk(s,p,Smooth,SoSo,Silence);
            Check(sloppy.Score>=.4&&sloppy.Score<.75,"setup: "+who+": smooth, so-so and a silence is a sloppy buy, score "+sloppy.Score);
            Check(Count(p,kind)==2&&p.Cash==10&&seller.StockSold==2,who+": a sloppy buy hands over a unit too (cash $"+p.Cash+", "+Count(p,kind)+" held)");

            // Hands filled mid-buy: the unit lands at the buyer's feet.
            var friend=s.Player("p2");Check(Ask(s,friend,seller).Accepted,who+": a friend starts a buy");
            foreach(var gear in new[]{"confetti","merch_bag","poi_practice"})friend.Inventory.Add(new ItemStack{ItemId=gear,Count=1});
            Talk(s,friend,Smooth,Smooth,Smooth);
            Check(Count(friend,kind)==0&&friend.Cash==15&&s.State.Drops.Exists(d=>d.ItemId==kind&&d.Count==1&&d.X==friend.X&&d.Z==friend.Z),who+": with full hands the friend's unit drops at their feet (cash $"+friend.Cash+")");

            // That was the seller's last unit for a crew of three: they turn the next ask down.
            Check(seller.StockSold==Festivals.StockCopies(crew),"setup: "+who+" has sold StockCopies(3) units, got "+seller.StockSold);
            var dry=Ask(s,p,seller);
            Check(!dry.Accepted&&p.InteractionId==""&&p.Cash==10&&Count(p,kind)==2,who+" sells no more than StockCopies(crew) units a level: "+dry.Reason);
            Check(s.State.LevelSales==0&&s.State.GrossSales==0,who+": buying is never a sale (level $"+s.State.LevelSales+", gross $"+s.State.GrossSales+")");
        }
    }

    // A buyer, the VIP guard and a sold-out seller turn an ask down alike: the same reply and the same suspicion. Asking for the
    // wrong kind, short of the price or with full hands is refused before anyone's role counts, so even a narc busts no one.
    static void TheReplyNeverTellsThemApart()
    {
        var s=Start(32,0);s.HasLineOfSight=(ax,az,bx,bz)=>false;var p=s.Player("p1");
        var buyer=Wook(s,"Buyer");var guard=s.State.Npcs.Find(n=>n.Twist==FestivalSimulation.VipGuard);var dry=Wook(s,"Regular");
        Check(buyer!=null&&guard!=null&&dry!=null,"setup: the crowd has a buyer, the VIP guard and a regular");
        dry.StockSold=Festivals.StockCopies(2);
        var replies=new List<string>();
        foreach(var npc in new[]{buyer,guard,dry})
        {
            double before=Suspicion(npc,p);var reply=Ask(s,p,npc);
            Check(!reply.Accepted&&p.InteractionId==""&&p.Cash==20&&p.Life=="Alive","asking "+npc.Id+" ("+npc.Role+npc.Twist+") buys nothing");
            replies.Add(reply.Reason+" (+"+(Suspicion(npc,p)-before)+" suspicion)");
        }
        Check(replies[0]==replies[1]&&replies[1]==replies[2]&&replies[0].StartsWith("Not selling. They give you a funny look. (+")&&!replies[0].EndsWith("(+0 suspicion)"),"a buyer, the guard and a sold-out seller give the same reply and suspicion: "+string.Join(" | ",replies));

        var narc=Wook(s,"Narc");Check(narc!=null,"setup: the crowd has a narc");
        string kind=FestivalSimulation.SellerStock(narc.Id),other=kind=="stock_lsd"?"stock_mushrooms":"stock_lsd";
        void Refused(string why,string item)
        {
            var reply=Ask(s,p,narc,item);
            Check(!reply.Accepted&&p.Life=="Alive"&&p.InteractionId==""&&Suspicion(narc,p)==0,why+" is refused with no bust and no suspicion, even by a narc: "+reply.Reason+" ("+p.Life+")");
        }
        Refused("the wrong kind",other);
        p.Cash=4;Refused("a short wallet",kind);p.Cash=20;
        foreach(var gear in new[]{"confetti","merch_bag","poi_practice"})p.Inventory.Add(new ItemStack{ItemId=gear,Count=1});
        Refused("full hands",kind);
    }

    // Asking a narc to sell is the same bust as offering them stock: detained, stock confiscated and the $10 fine.
    static void AskingANarcIsABust()
    {
        var s=Start(33,0);var p=s.Player("p1");var narc=Wook(s,"Narc");Check(narc!=null,"setup: the crowd has a narc");
        p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});p.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
        var reply=Ask(s,p,narc);
        Check(reply.Reason=="Busted! That was an undercover narc.","the narc busts the buyer: "+reply.Reason);
        Check(p.Life=="Detained"&&!p.Inventory.Exists(i=>i.ItemId=="stock_lsd"||i.ItemId=="stock_mushrooms")&&p.Cash==10&&p.InteractionId==""&&s.State.LevelSales==0,"detained, stock confiscated and fined $10 ("+p.Life+", $"+p.Cash+")");
    }

    // A cop who sees a buy start records a witnessed deal and moves in to arrest; one looking away never hears of it. The bust
    // cancels the buy, so its $5 comes back before the $10 fine.
    static void ACopSeesTheBuy()
    {
        var s=Start(34,0);var p=s.Player("p1");var seller=Wook(s,"Regular");Check(seller!=null,"setup: the crowd has a regular");
        float x=seller.X,z=seller.Z-1;
        var watching=new NpcState{Id="cop_watching",Kind="Cop",X=x,Z=z-3,Yaw=0};var away=new NpcState{Id="cop_away",Kind="Cop",X=x+3,Z=z,Yaw=90};
        s.State.Npcs.Add(watching);s.State.Npcs.Add(away);
        Check(Ask(s,p,seller).Accepted&&p.Cash==15,"setup: the buy starts in front of a cop");
        Check(watching.Evidence.Exists(e=>e.PlayerId==p.Id&&e.Kind=="WitnessedDeal"),"the cop who sees the buy records a witnessed deal");
        Check(!away.Evidence.Exists(e=>e.PlayerId==p.Id),"the cop looking away records nothing");
        s.Tick(.1);
        Check(watching.Mode=="ArrestWarning"&&away.Mode!="ArrestWarning","the watching cop moves in to arrest, the other doesn't ("+watching.Mode+", "+away.Mode+")");
        s.Tick(4.1);
        Check(p.Life=="Detained"&&p.Cash==10&&seller.StockSold==0&&!p.Inventory.Exists(i=>i.ItemId==FestivalSimulation.SellerStock(seller.Id)),"the bust cancels the buy: refunded, then fined $10, and no unit changes hands ("+p.Life+", $"+p.Cash+")");
    }

    // The buy adds no random draw. For seeds 1-30 the tripper, the doses, every festivalgoer's role and twist, and the tripper's
    // visions a level deals as the crew leaves camp match what they were when ECON-2 began, after TRIP-12's 30/30/30/10 dose wheel.
    // Each seed varies the crew (1-8), the festival and the level.
    const string Recorded="32771aa7 45e9c53c 0dba7ad4 66cd7ca1 fcf8794f 657bf801 4e9aa279 efce5c75 0c46ce01 8c4b84f5 71c8f419 d5f91cea 156f503e 76b11567 9f72803d 9d0ad4b0 3d3e510a ff0bfe54 034a1448 bdf99895 3b07a2ab ac5d22db 95367a03 36f36db7 957bec03 64078cf0 aaa65bf2 3e4e0414 6571f0fb aac2d8eb";
    static void TheDealIsUnchanged()
    {
        var prints=new List<string>();
        for(int seed=1;seed<=30;seed++)
        {
            var s=new FestivalSimulation(seed);for(int i=0;i<1+seed%8;i++){var p=s.AddPlayer("p"+i,"P"+i);p.X=0;p.Z=19;}
            s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=seed%2;s.State.LevelIndex=seed/2%Festivals.LevelCount;
            foreach(var p in s.State.Players)Act(s,p.Id,"Ready");
            Check(Act(s,"p0","Start").Accepted,"setup: seed "+seed+"'s crew leaves camp");
            var deal=new StringBuilder(s.State.TripperId);
            foreach(var d in s.State.Doses)deal.Append('|').Append(d.PlayerId).Append(d.Substance).Append(d.Dose);
            foreach(var n in s.State.Npcs)deal.Append('|').Append(n.Id).Append(n.Role).Append(n.Twist);
            foreach(var v in s.State.Visions)deal.Append('|').Append(v.Kind).Append(v.NpcId).Append(v.IsTrue).Append((int)(v.X*100)).Append(',').Append((int)(v.Z*100));
            prints.Add(Fnv(deal.ToString()).ToString("x8"));
        }
        Check(string.Join(" ",prints)==Recorded,"the deal changed for seeds 1-30:\n got      "+string.Join(" ",prints)+"\n recorded "+Recorded);
    }
    // FNV-1a: string.GetHashCode is seeded per process.
    static uint Fnv(string text){uint hash=2166136261;foreach(char c in text)hash=unchecked((hash^c)*16777619);return hash;}
}
