using System;
using System.Collections.Generic;
using Festival.Core;

// POLO-2: Palm Mirage polish. Festivalgoers filming talk like influencers, and nobody else does. The stock's text names what
// a clean sale pays and no ceiling, since dose, VIP zones, double buyers and encores all raise it.
public static class PoloPolishTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("PoloPolish: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="")=>s.Execute(id,new GameCommand{Id="polish"+(sequence++),Kind=kind,TargetId=target});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{OnlyFilmersTalkLikeInfluencers,AFilmersChatOpensLikeAnInfluencer,StockSaysCleanSalesStartAtPerfectSalePay})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" polo polish test(s) failed:\n"+string.Join("\n",failures));
    }

    // Two friends ready up at camp, the wheels spin and the crew loads into the crowd, twists and all.
    static FestivalSimulation Start(int seed,int level,int festival)
    {
        var s=new FestivalSimulation(seed);s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew is at "+Festivals.Name(festival));
        return s;
    }

    // On both festivals, by day and by night: a festivalgoer talks like an influencer exactly when they film, and the rest of
    // the crowd still mixes the other four personas.
    static void OnlyFilmersTalkLikeInfluencers()
    {
        var others=new HashSet<string>();
        for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<2;level++)for(int seed=0;seed<4;seed++)
        {
            var s=Start(seed,level,festival);string where=Festivals.Name(festival)+" level "+level+" seed "+seed+": ";
            Check(s.State.Npcs.FindAll(n=>n.Twist==FestivalSimulation.Influencer).Count==(festival==Festivals.PoloFestival?Festivals.Influencers:0),where+"setup: influencers film at Palm Mirage only");
            foreach(var n in s.State.Npcs)
            {
                string persona=DialogueGrammar.PersonaFor(n.Id,n.Twist);bool filming=n.Twist==FestivalSimulation.Influencer;
                Check((persona=="Influencer")==filming,where+n.Id+(filming?" films but talks like a ":" doesn't film but talks like an ")+persona);
                if(!filming)others.Add(persona);
            }
        }
        Check(others.Count==DialogueGrammar.Personas.Length-1,"the rest of the crowd mixes the other four personas, got "+string.Join(", ",others));
    }

    // The tripper's chat with someone filming, about whom they have a vision, is an influencer's conversation: four filmers,
    // from the first levels that offer one.
    static void AFilmersChatOpensLikeAnInfluencer()
    {
        int chats=0;
        for(int seed=0;seed<200&&chats<4;seed++)
        {
            var s=Start(seed,0,Festivals.PoloFestival);var tripper=s.Player(s.State.TripperId);
            var filmer=s.State.Npcs.Find(n=>n.Twist==FestivalSimulation.Influencer&&FestivalSimulation.CanCheckVision(s.State,tripper,n));
            if(filmer==null)continue;
            double yaw=filmer.Yaw*Math.PI/180;tripper.X=filmer.X+(float)Math.Sin(yaw);tripper.Z=filmer.Z+(float)Math.Cos(yaw);
            var started=Act(s,tripper.Id,"ConfirmChat",filmer.Id);Check(started.Accepted,"setup: the tripper chats with "+filmer.Id+": "+started.Reason);
            var chat=s.Interaction(tripper.InteractionId);var expected=DialogueGrammar.Build(filmer.Role,"Influencer",Festivals.PoloFestival,chat.ChartSeed);
            Check(chat.Chat.Opener==expected.Opener&&string.Join("|",chat.Chat.Answers)==string.Join("|",expected.Answers),"seed "+seed+": "+filmer.Id+" films, so opens like an influencer, not: "+chat.Chat.Opener);
            chats++;
        }
        Check(chats==4,"setup: only "+chats+" levels give the tripper a vision about someone filming");
    }

    // A clean sale at dose 1 pays PerfectSalePay; a VIP sale pays double that and more with dose, a double buyer and an encore.
    // ECON-1: a sloppy sale that still lands pays less ($5 at dose 1), so the floor is a clean sale's, and the text says so.
    // The "$N" token stays, so Ember Playa's odd-object money (FestivalHudText.MoneyText) converts it.
    static void StockSaysCleanSalesStartAtPerfectSalePay()
    {
        foreach(var id in new[]{"stock_lsd","stock_mushrooms"})
        {
            string text=Catalog.FindItem(id).Description;
            Check(text.StartsWith("Clean sales pay $"+FestivalSimulation.PerfectSalePay+" and up, or take: "),id+" names a clean sale's pay as the floor of clean sales: "+text);
            Check(!text.Contains("up to"),id+" names no ceiling: "+text);
        }
    }
}
