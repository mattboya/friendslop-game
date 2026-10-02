using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;

namespace Festival.Tests
{
    // ECON-2: no shelf sells stock, so the native smokes (DevelopmentSmoke, run only at the final gates) buy their Tongue Stamps,
    // and the solo run its Fun Guys, from festivalgoers early on Day 1, and the two-player run shops at the night market where
    // the seed shelved the medical voucher. These hold the smoke's own picks to the host's rules on a real Day 1 deal, for any
    // seed, as the smokes run on a random one.
    public sealed class SmokeStockTests
    {
        static int sequence;
        static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="")=>s.Execute(id,new GameCommand{Id="smokestock"+(sequence++),Kind=kind,TargetId=target,ItemId=item});
        // The crew readies at camp, the reels land and everyone loads into Palm Mirage's Day 1 crowd, dealt its roles.
        static FestivalSimulation DayOne(int seed,int crew)
        {
            var s=new FestivalSimulation(seed);for(int i=0;i<crew;i++)s.AddPlayer("p"+i,"P"+i);
            foreach(var p in s.State.Players){p.X=0;p.Z=19;Assert.That(Act(s,p.Id,"Ready").Accepted,Is.True,"setup: "+p.Id+" readies");}
            s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
            Assert.That(s.State.Phase,Is.EqualTo("Playing"),"setup: seed "+seed+"'s crew reaches the festival");
            return s;
        }

        // The solo run buys Fun Guys and a Tongue Stamp; the two-player run a Tongue Stamp each, from two sellers. Each pick sells:
        // a perfect buy, out of the cops' sight, hands over one unit of the kind asked for. DIALOG-3: perfect is a talk with the
        // reply the smokes pick every turn (DevelopmentSmoke.SmoothReply), after a perfect dance if the seller asks for one first.
        [Test] public void TheSmokesBuyFromFestivalgoersWhoSell()
        {
            for(int seed=1;seed<=100;seed++)for(int crew=1;crew<=2;crew++)
            {
                var s=DayOne(seed,crew);
                var first=DevelopmentSmoke.Seller(s,"stock_lsd");var second=crew==1?DevelopmentSmoke.Seller(s,"stock_mushrooms"):DevelopmentSmoke.Seller(s,"stock_lsd",first);
                Assert.That(first!=null&&second!=null&&first!=second,Is.True,"seed "+seed+", crew "+crew+": the smoke finds two sellers");
                // As in the smoke, the rest of the crowd and security step away.
                s.State.Npcs.RemoveAll(n=>n!=first&&n!=second);
                foreach(var (seller,player) in new[]{(first,s.State.Players[0]),(second,s.State.Players[crew-1])})
                {
                    string kind=FestivalSimulation.SellerStock(seller.Id);player.X=seller.X;player.Z=seller.Z-1.5f;
                    var asked=Act(s,player.Id,"StartBuy",seller.Id,kind);
                    Assert.That(asked.Accepted&&player.InteractionId!="",Is.True,"seed "+seed+", crew "+crew+": "+seller.Id+" sells "+kind+": "+asked.Reason);
                    var buy=s.Interaction(player.InteractionId);
                    if(buy.TrustDance)foreach(var note in RhythmChart.For(buy).Notes)buy.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
                    for(int guard=0;buy.Status=="Active"&&guard<300;guard++)
                    {
                        if(buy.Turns.Count==0){s.Tick(.1);continue;}
                        var turn=buy.Turns[buy.Turns.Count-1];
                        Assert.That(s.Execute(player.Id,new GameCommand{Id="smokestock"+(sequence++),Kind="Reply",Amount=DevelopmentSmoke.SmoothReply(turn),Direction=buy.Turns.Count-1}).Accepted,Is.True,"seed "+seed+", crew "+crew+": the smoke's reply");
                    }
                    Assert.That(player.Inventory.Exists(i=>i.ItemId==kind),Is.True,"seed "+seed+", crew "+crew+": the buy from "+seller.Id+" hands over "+kind);
                }
            }
        }

        // The client stands at the smoke's market spot and buys a medical voucher off the night-market shelf there.
        [Test] public void TheSmokeShopsAtTheVouchersMarketShelf()
        {
            for(int seed=1;seed<=20;seed++)
            {
                var s=DayOne(seed,2);var client=s.State.Players[1];
                var spot=DevelopmentSmoke.MarketSpot(s.State);client.X=spot.X;client.Z=spot.Z;
                var bought=Act(s,client.Id,"Buy","","medical_voucher");
                Assert.That(bought.Accepted&&client.Inventory.Exists(i=>i.ItemId=="medical_voucher"),Is.True,"seed "+seed+": a voucher from the market spot: "+bought.Reason);
            }
        }
    }
}
