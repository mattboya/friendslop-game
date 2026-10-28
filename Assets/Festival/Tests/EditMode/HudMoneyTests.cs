using System.IO;
using System.Linq;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;

namespace Festival.Tests
{
    // HUD-3 with PLAYA-1: every amount of money a player reads goes through Festivals.CurrencyName: dollars at Palm Mirage, and on
    // Ember Playa the reader's own odd objects, a different kind for each crew member over the same dollar economy.
    public sealed class HudMoneyTests
    {
        static RoundState Day(int festival)=>new RoundState{FestivalIndex=festival,Phase="Playing",DurationSeconds=Festivals.Level(festival,0,0).DurationSeconds};
        static PlayerState Crew(RoundState s,string id,int ordinal){var p=new PlayerState{Id=id,Name=id,Ordinal=ordinal};s.Players.Add(p);return p;}

        [Test] public void EmberPlayasQuotaCountsInYourObjects()
        {
            // Ember Playa's Day 1 asks 22 from each of two.
            var s=Day(1);var ash=Crew(s,"ash",0);var sam=Crew(s,"sam",1);s.LevelSales=12;
            Assert.That(FestivalHudText.ObjectiveTitle(s,ash),Is.EqualTo("DAY QUOTA  12 BUTTONS / 44 BUTTONS"));
            Assert.That(FestivalHudText.ObjectiveDetail(s,ash),Is.EqualTo("Sell 32 buttons more before sundown. You have nothing to sell: buy from festivalgoers, and some are narcs."));
            Assert.That(FestivalHudText.ObjectiveTitle(s,sam),Is.EqualTo("DAY QUOTA  12 RAMEN PACKETS / 44 RAMEN PACKETS"),"the same quota in Sam's ramen");
            Assert.That(FestivalHudText.ObjectiveDetail(s,sam),Is.EqualTo("Sell 32 ramen packets more before sundown. You have nothing to sell: buy from festivalgoers, and some are narcs."));
        }

        [Test] public void ThePlayerCardCountsInYourObjects()
        {
            var palm=Day(0);var you=Crew(palm,"you",1);you.Cash=87;palm.StashCash=5;palm.LevelSales=120;
            Assert.That(FestivalHudText.Vitals(palm,you),Is.EqualTo("HP 100  •  $87  •  STASH $5\nSALES $120  •  ALIVE"));
            var playa=Day(1);var sam=Crew(playa,"sam",1);sam.Cash=87;playa.StashCash=1;playa.LevelSales=120;
            Assert.That(FestivalHudText.Vitals(playa,sam),Is.EqualTo("HP 100  •  87 RAMEN PACKETS  •  STASH 1 RAMEN PACKET\nSALES 120 RAMEN PACKETS  •  ALIVE"),
                "your pocket, the crew's stash and the level's sales, all in your ramen");
        }

        [Test] public void MessagesAndPricesFromTheHostReadInYourMoney()
        {
            // The host's rules write money as "$5": a purchase, a refused Extract, a paid release, an item's description.
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Tongue Stamps bought for $5"),Is.EqualTo("Tongue Stamps bought for 5 ramen packets"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,Catalog.FindItem("stock_lsd").Description),Does.StartWith("Clean sales pay 10 ramen packets and up; ask High or Greedy for more. Or take"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Release: $10 at holding desk, $1 tip"),Is.EqualTo("Release: 10 ramen packets at holding desk, 1 ramen packet tip"),"every amount in a line");
            Assert.That(FestivalHudText.MoneyText(playa,sam,"Inside the tent"),Is.EqualTo("Inside the tent"));
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.MoneyText(palm,you,"Tongue Stamps bought for $5"),Is.EqualTo("Tongue Stamps bought for $5"),"dollars stay dollars at Palm Mirage");
        }

        // ECON-2: beside any festivalgoer the HUD offers to buy the one kind they would sell, read off their id alone (an id ending in
        // an even digit sells Tongue Stamps, an odd one Fun Guys), at its price in your money. A buy that hands you a unit says so
        // in dollars, as the host's replies do, for MoneyText to turn into your objects; a missed or cancelled one says nothing.
        [Test] public void ABuyFromAFestivalgoerReadsInYourMoney()
        {
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.BuyAction(palm,you,"wook_4"),Is.EqualTo("Buy Tongue Stamps  •  $5"));
            Assert.That(FestivalHudText.BuyAction(palm,you,"wook_17"),Is.EqualTo("Buy Fun Guys  •  $5"));
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.BuyAction(playa,sam,"wook_4"),Is.EqualTo("Buy Tongue Stamps  •  5 ramen packets"),"the price in Sam's ramen");
            Assert.That(FestivalHudText.BuyReply("stock_lsd",15,new PlayerState{Cash=15}),Is.EqualTo("Bought Tongue Stamps for $5"),"the held price stayed spent");
            Assert.That(FestivalHudText.BuyReply("stock_mushrooms",0,new PlayerState{Cash=0}),Is.EqualTo("Bought Fun Guys for $5"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,FestivalHudText.BuyReply("stock_lsd",15,new PlayerState{Cash=15})),Is.EqualTo("Bought Tongue Stamps for 5 ramen packets"));
            Assert.That(FestivalHudText.BuyReply("stock_lsd",15,new PlayerState{Cash=20}),Is.EqualTo(""),"a missed or cancelled buy gave the price back");
        }

        // From exactly $5, a narc's bust or an arrest mid-buy gives the held $5 back and then fines $5, so the cash matches the buy's
        // last frame with nothing bought. The real rules end each buy here.
        [Test] public void ABuyThatEndsInABustNeverSaysItBought()
        {
            var game=Hosted(0);var ash=game.Player("ash");int sequence=0;
            string Buy(string role,bool arrested)
            {
                string seller=role+"_0";for(int n=1;DialogueGrammar.PersonaFor(seller,"")!="Wook"||FestivalSimulation.SellerStock(seller)!="stock_lsd";n++)seller=role+"_"+n;
                game.State.Npcs.Clear();game.State.Npcs.Add(new NpcState{Id=seller,Kind="Wook",Role=role,X=0,Z=1.5f,Yaw=180,CanTalk=true});
                ash.Life="Alive";ash.X=0;ash.Z=0;ash.Cash=5;ash.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");ash.EquippedItemId="";
                Assert.That(game.Execute("ash",new GameCommand{Id="bust-buy-"+(sequence++),Kind="StartBuy",TargetId=seller,ItemId="stock_lsd"}).Accepted,Is.True,"setup: the buy from a "+role+" starts");
                var talk=game.Interaction(ash.InteractionId);int cashWhileBuying=ash.Cash;
                if(arrested)typeof(FestivalSimulation).GetMethod("Detain",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,new object[]{ash});
                for(int guard=0;talk.Status=="Active"&&guard<300;guard++)
                {
                    var turn=talk.Turns.Count>0?talk.Turns[talk.Turns.Count-1]:null;
                    if(turn==null||turn.Pick>=0){game.Tick(.1);continue;}
                    Assert.That(game.Execute("ash",new GameCommand{Id="bust-buy-"+(sequence++),Kind="Reply",Amount=System.Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=talk.Turns.Count-1}).Accepted,Is.True,"setup: a smooth reply");
                }
                Assert.That(talk.Status,Is.Not.EqualTo("Active"),"setup: the buy from a "+role+" ended");
                return FestivalHudText.BuyReply("stock_lsd",cashWhileBuying,ash);
            }
            Assert.That(Buy("Regular",false),Is.EqualTo("Bought Tongue Stamps for $5"),"setup: a regular sells for the held $5");
            Assert.That(Buy("Narc",false),Is.EqualTo(""),"a narc's bust bought nothing ($"+ash.Cash+", "+ash.Life+")");
            Assert.That(Buy("Regular",true),Is.EqualTo(""),"an arrest mid-buy bought nothing ($"+ash.Cash+", "+ash.Life+")");
        }

        // LOOP-4: the corner reel's frozen amount and result, in the viewer's money.
        [Test] public void ADeathsCornerReelReadsInYourMoney()
        {
            var dropped=new DeathSpinState{Id="r:death:sam:1",RoundId="r",PlayerId="sam",Name="Sam",Sequence=1,Amount=40,CashDropped=40,AtWorldSeconds=3};
            var kept=new DeathSpinState{Id="r:death:sam:2",RoundId="r",PlayerId="sam",Name="Sam",Sequence=2,Amount=40,AtWorldSeconds=9};
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.DeathSpinTitle(dropped),Is.EqualTo("SAM DIED"));
            Assert.That(FestivalHudText.DeathSpinStake(palm,you,dropped),Is.EqualTo("$40"));
            Assert.That(FestivalHudText.DeathSpinResult(palm,you,dropped),Is.EqualTo("DROPPED $40"));
            Assert.That(FestivalHudText.DeathSpinResult(palm,you,kept),Is.EqualTo("KEPT $40"));
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.DeathSpinStake(playa,sam,dropped),Is.EqualTo("40 RAMEN PACKETS"),"Sam reads the stake in ramen");
            Assert.That(FestivalHudText.DeathSpinResult(playa,sam,dropped),Is.EqualTo("DROPPED 40 RAMEN PACKETS"));
            Assert.That(FestivalHudText.DeathSpinResult(playa,sam,kept),Is.EqualTo("KEPT 40 RAMEN PACKETS"));
            Assert.That(FestivalHudText.DeathSpinTitle(new DeathSpinState{Name="Bartholomew the Great"}),Is.EqualTo("BARTHOLOME DIED"),"a long name keeps the spinner's ten letters");
        }

        // LOOP-4: a cash pile's floating label and its pickup line read its amount in your money; other drops keep their names.
        [Test] public void ACashPileAndItsPickupReadInYourMoney()
        {
            var pile=new DropState{Id="cash_1",ItemId="cash",Count=40,X=1,Z=2};var stock=new DropState{Id="drop_2",ItemId="stock_lsd",Count=1};
            var palm=Day(0);var you=Crew(palm,"you",0);
            Assert.That(FestivalHudText.DropLabel(palm,you,pile),Is.EqualTo("$40"));
            Assert.That(FestivalHudText.PickupAction(palm,you,pile),Is.EqualTo("Pick up $40"));
            Assert.That(FestivalHudText.PickupAction(palm,you,stock),Is.EqualTo("Pick up Tongue Stamps"),"other drops keep their display names");
            var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.DropLabel(playa,sam,pile),Is.EqualTo("40 ramen packets"));
            Assert.That(FestivalHudText.PickupAction(playa,sam,pile),Is.EqualTo("Pick up 40 ramen packets"));
            Assert.That(FestivalSession.DropModel("cash"),Is.EqualTo("FestivalVoucher"),"a cash pile reuses the voucher model");
        }

        // ECON-8: a random find's notice, in your money.
        [Test] public void AFindsNoticeReadsInYourMoney()
        {
            var palm=Day(0);var you=Crew(palm,"you",0);var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.MoneyText(palm,you,FestivalHudText.FoundCashNotice(4)),Is.EqualTo("Found $4 on the ground!"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,FestivalHudText.FoundCashNotice(4)),Is.EqualTo("Found 4 ramen packets on the ground!"));
        }

        // The host's real rules make each change; the HUD's follower hands each notice over once, oldest first, in each reader's money.
        static FestivalSimulation Hosted(int festival)
        {
            var game=new FestivalSimulation(12);game.DealSecret=()=>98765;game.State.UnlockedFestivalCount=Festivals.Count;game.State.FestivalIndex=festival;
            foreach(var id in new[]{"ash","sam"}){var p=game.AddPlayer(id,id=="ash"?"Ash":"Sam");p.X=0;p.Z=19;Assert.That(game.Execute(id,new GameCommand{Id="ready-"+id,Kind="Ready"}).Accepted,Is.True);}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)Assert.That(game.Execute(p.Id,new GameCommand{Id="map-"+p.Id,Kind="MapReady"}).Accepted,Is.True);
            game.State.Npcs.Clear();return game;
        }
        static System.Collections.Generic.List<string> Shown(FestivalSimulation game,FestivalHudText.MoneyNewsFollower news,string viewer)
        {
            var shown=new System.Collections.Generic.List<string>();var p=game.Player(viewer);news.Follow(game.State,p,1);
            for(var n=news.Take(0);n!=null;n=news.Take(0))shown.Add(FestivalHudText.MoneyText(game.State,p,n.Text));
            return shown;
        }
        [TestCase(0,"LOST PROPERTY RETURNED  •  +$5","INTO THE STASH  •  −$5","STASH  •  +$5  •  Ash","SECURITY FINE  •  −$10  •  SECURITY TOOK 1 Tongue Stamps")]
        [TestCase(1,"LOST PROPERTY RETURNED  •  +5 buttons","INTO THE STASH  •  −5 buttons","STASH  •  +5 ramen packets  •  Ash","SECURITY FINE  •  −10 buttons  •  SECURITY TOOK 1 Tongue Stamps")]
        public void EveryChangeShowsItsAmountOnce(int festival,string lostProperty,string deposit,string crewDeposit,string detained)
        {
            var game=Hosted(festival);var ash=game.Player("ash");ash.Cash=40;
            var mine=new FestivalHudText.MoneyNewsFollower();var theirs=new FestivalHudText.MoneyNewsFollower();
            Assert.That(Shown(game,mine,"ash"),Is.Empty,"setup: the first view only takes note");Assert.That(Shown(game,theirs,"sam"),Is.Empty);
            ash.X=-28;ash.Z=16;Assert.That(game.Execute("ash",new GameCommand{Id="lost",Kind="LostProperty"}).Accepted,Is.True);game.Tick(5.1);
            game.State.Stashes.Add(new StashState{Id="box",X=ash.X,Z=ash.Z});
            Assert.That(game.Execute("ash",new GameCommand{Id="deposit",Kind="Deposit",TargetId="box",Amount=5}).Accepted,Is.True);
            Assert.That(Shown(game,mine,"ash"),Is.EqualTo(new[]{lostProperty,deposit}),"your own changes, oldest first");
            Assert.That(Shown(game,theirs,"sam"),Is.EqualTo(new[]{crewDeposit}),"the crew sees the stash move, in their own money");
            Assert.That(Shown(game,mine,"ash"),Is.Empty,"each notice shows once");
            ash.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            typeof(FestivalSimulation).GetMethod("Detain",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,new object[]{ash});
            Assert.That(Shown(game,mine,"ash"),Is.EqualTo(new[]{detained}));
        }
        [Test] public void ANewConnectionNeverReplaysOldNewsButANewRoundShowsAllOfIts()
        {
            var game=Hosted(0);var ash=game.Player("ash");var news=new FestivalHudText.MoneyNewsFollower();
            ash.MoneyNewsLog.Add(new MoneyNews{Sequence=1,Kind="Cash",Text="LOST PROPERTY RETURNED  •  +$5"});ash.MoneyNewsSequence=1;
            news.Follow(game.State,ash,1);Assert.That(news.Take(0),Is.Null,"a join takes note of what was already there");
            ash.MoneyNewsLog.Add(new MoneyNews{Sequence=2,Kind="Items",Text="GIGGLE TANK LOST"});ash.MoneyNewsSequence=2;
            news.Follow(game.State,ash,1);Assert.That(news.Take(0)?.Text,Is.EqualTo("GIGGLE TANK LOST"));
            news.Follow(game.State,ash,2);Assert.That(news.Take(0),Is.Null,"a rejoin replays nothing");
            game.State.RoundId="next-level";ash.MoneyNewsLog.Clear();ash.MoneyNewsLog.Add(new MoneyNews{Sequence=1,Kind="Items",Text="GIGGLE TANK LOST"});ash.MoneyNewsSequence=1;
            news.Follow(game.State,ash,2);Assert.That(news.Take(0)?.Text,Is.EqualTo("GIGGLE TANK LOST"),"a new level's news is all new, the loss its start made included");
        }
        // A money line that another line replaces inside its MoneyNewsSeconds comes back before the rest of the queue. A line that had
        // its slot does not. A clear (your last reply's key) followed by another line inside the slot brings it back; a clear alone
        // does not. A new level forgets it.
        [Test] public void AMoneyLineCutShortComesBackFirst()
        {
            var game=Hosted(0);var ash=game.Player("ash");var news=new FestivalHudText.MoneyNewsFollower();news.Follow(game.State,ash,1);
            const string Friend="SALES  •  +$15  •  Sam sold Tongue Stamps",Stash="STASH  •  +$5  •  Sam",Sold="SOLD Tongue Stamps  •  +$15";
            void Log(params string[] texts){foreach(var text in texts)ash.MoneyNewsLog.Add(new MoneyNews{Sequence=++ash.MoneyNewsSequence,Kind="Sales",Text=text});news.Follow(game.State,ash,1);}
            Log(Friend,Stash);
            Assert.That(news.Take(10)?.Text,Is.EqualTo(Friend));
            news.Watch(Friend,10.2f);news.Watch(Sold,10.4f);
            Assert.That(news.Take(12.9f)?.Text,Is.EqualTo(Friend),"the friend's line cut at .4 s shows again, before the stash line");
            news.Watch(Friend,13f);news.Watch(Sold,12.9f+FestivalHudText.MoneyNewsSeconds+.1f);
            Assert.That(news.Take(16)?.Text,Is.EqualTo(Stash),"a line that had its slot does not show again");
            news.Watch("",16.5f);news.Watch(Sold,16.6f);
            Assert.That(news.Take(19)?.Text,Is.EqualTo(Stash),"a line cleared by your last reply's key and then cut by your sale shows again");
            news.Watch("",19.5f);news.Watch(Sold,19+FestivalHudText.MoneyNewsSeconds+.1f);
            Assert.That(news.Take(23),Is.Null,"a clear with no other line inside the slot is not another line");
            Log(Friend);Assert.That(news.Take(24)?.Text,Is.EqualTo(Friend),"setup: the line is up");
            game.State.RoundId="next-level";ash.MoneyNewsLog.Clear();news.Follow(game.State,ash,1);news.Watch(Sold,24.4f);
            Assert.That(news.Take(27),Is.Null,"a new level forgets the last level's line");
        }
        // The card lights each figure whose value moved, whoever moved it and whatever told you, and nothing else. A change made
        // while the card is hidden lights as it comes back; a join, a rejoin and a new level only take note.
        [Test] public void TheCardLightsEachFigureThatMovedAndNothingElse()
        {
            var game=Hosted(0);var ash=game.Player("ash");var sam=game.Player("sam");ash.Cash=sam.Cash=40;
            game.State.Stashes.Add(new StashState{Id="box",X=ash.X,Z=ash.Z});ash.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            var mine=new FestivalHudText.MoneyFlash();var theirs=new FestivalHudText.MoneyFlash();float now=10;int command=0;
            // The card's money, up to the life (a tripper's dose follows it).
            string Card(FestivalHudText.MoneyFlash flash,PlayerState p,bool shown=true,int connection=1){flash.Watch(game.State,p,connection,shown,now);string card=FestivalHudText.Vitals(game.State,p,lit:flash.Lit(now,true)).Replace("\n"," | ");return card.Substring(0,card.IndexOf("  •  ALIVE",System.StringComparison.Ordinal));}
            void Run(string id,string kind,string item,int amount){now+=2;Assert.That(game.Execute(id,new GameCommand{Id="flash-"+(command++),Kind=kind,TargetId="box",ItemId=item,Amount=amount}).Accepted,Is.True,"setup: "+id+" "+kind+" "+item+amount);}
            const string Money="HP 100  •  {0}  •  {1} | {2}";
            string Lit(string figure)=>"<color=#FC7741>"+figure+"</color>";
            Assert.That(Card(mine,ash),Is.EqualTo(string.Format(Money,"$40","STASH $0","SALES $0")),"a first view only takes note");Card(theirs,sam);
            Run("ash","Deposit","stock_lsd",1);
            Assert.That(Card(theirs,sam),Is.EqualTo(string.Format(Money,"$40","STASH $0","SALES $0")),"a friend's stock into the stash lights nothing on Sam's card");
            Assert.That(Card(mine,ash),Is.EqualTo(string.Format(Money,"$40","STASH $0","SALES $0")),"nor on Ash's");
            Run("ash","Deposit","",5);
            Assert.That(Card(mine,ash),Is.EqualTo(string.Format(Money,Lit("$35"),Lit("STASH $5"),"SALES $0")),"your own cash into the stash lights your cash and the STASH");
            Assert.That(Card(theirs,sam),Is.EqualTo(string.Format(Money,"$40",Lit("STASH $5"),"SALES $0")),"a friend's lights only the STASH");
            now+=FestivalHudText.SaleStampSeconds;
            Assert.That(Card(mine,ash),Is.EqualTo(string.Format(Money,"$35","STASH $5","SALES $0")),"each for the stamp's 1.5 s");
            Run("ash","Withdraw","",5);
            Assert.That(Card(theirs,sam,shown:false),Does.Not.Contain("<color"),"setup: Sam's card is under the pass");now+=5;
            Assert.That(Card(theirs,sam),Is.EqualTo(string.Format(Money,"$40",Lit("STASH $0"),"SALES $0")),"a change made under the pass lights as the card comes back");
            now+=2;game.State.LevelSales+=10;
            Assert.That(Card(theirs,sam),Is.EqualTo(string.Format(Money,"$40","STASH $0",Lit("SALES $10"))),"a friend's sale lights only SALES");
            now+=2;ash.Cash+=5;
            Assert.That(Card(mine,ash,connection:2),Does.Not.Contain("<color"),"a rejoin takes note");
            now+=2;game.State.RoundId="next-level";game.State.LevelSales=0;
            Assert.That(Card(theirs,sam),Does.Not.Contain("<color"),"a new level's fresh SALES takes note");
        }
        [Test] public void TheShelfsThankYouNamesThePrice()
        {
            var palm=Day(0);var you=Crew(palm,"you",0);var playa=Day(1);var sam=Crew(playa,"sam",1);
            Assert.That(FestivalHudText.MoneyText(palm,you,FestivalHudText.PocketUpdated(5)),Is.EqualTo("Oh, hey, thanks!  •  −$5  •  POCKET UPDATED"));
            Assert.That(FestivalHudText.MoneyText(playa,sam,FestivalHudText.PocketUpdated(5)),Is.EqualTo("Oh, hey, thanks!  •  −5 ramen packets  •  POCKET UPDATED"));
        }

        // A sale's outcome reads in your money wherever it shows, from what the host wrote in dollars: its notice in the HUD's message
        // and the chat window, its result on the stamp. A paid sale flashes your cash and SALES on the player card, lit every other
        // quarter-second for the stamp's 1.5 s; with Reduced Motion on they stay lit for the 1.5 s, with no blink.
        [Test] public void ASalesResultReadsInYourMoneyAndFlashesTheCard()
        {
            var game=new FestivalSimulation(7);game.AddPlayer("ash","Ash");var sam=game.AddPlayer("sam","Sam");game.State.FestivalIndex=1;game.State.Phase="Playing";game.State.Npcs.Clear();
            string buyer="buyer_0";for(int k=1;DialogueGrammar.PersonaFor(buyer,"")=="Raver"||DialogueGrammar.PersonaFor(buyer,"")=="Burner";k++)buyer="buyer_"+k;
            game.State.Npcs.Add(new NpcState{Id=buyer,Role="Buyer",X=0,Z=1.5f,Yaw=180});sam.X=0;sam.Z=0;sam.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            Assert.That(game.Execute("sam",new GameCommand{Id="greedy",Kind="StartSale",TargetId=buyer,ItemId="stock_lsd",Amount=FestivalSimulation.GreedyAsk}).Accepted,Is.True,"setup: a Greedy sale");
            var sale=game.Interaction(sam.InteractionId);
            for(int t=0;sale.Status=="Active";t++){var turn=sale.Turns[sale.Turns.Count-1];Assert.That(game.Execute("sam",new GameCommand{Id="reply_"+t,Kind="Reply",Amount=System.Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=sale.Turns.Count-1}).Accepted,Is.True,"setup: reply "+(t+1));}
            var outcome=sam.ConversationOutcomes.Find(o=>o.InteractionId==sale.Id);
            Assert.That(FestivalHudText.MoneyText(game.State,sam,outcome.Notice),Is.EqualTo("SOLD Tongue Stamps  •  +20 ramen packets"));
            Assert.That(FestivalHudText.MoneyText(game.State,sam,outcome.Result),Is.EqualTo("SOLD +20 ramen packets"));
            var palm=Day(0);var you=Crew(palm,"you",1);you.Cash=87;palm.StashCash=5;palm.LevelSales=120;
            const string Lit="HP 100  •  <color=#FC7741>$87</color>  •  STASH $5\n<color=#FC7741>SALES $120</color>  •  ALIVE",Unlit="HP 100  •  $87  •  STASH $5\nSALES $120  •  ALIVE";
            Assert.That(FestivalHudText.Vitals(palm,you,lit:(true,false,true)),Is.EqualTo(Lit),"cash and SALES lit");
            Assert.That(FestivalHudText.Vitals(palm,you),Is.EqualTo(Unlit),"unlit as before");
            Assert.That(new[]{0f,.1f,.3f,.5f,1.4f,1.5f,-1f}.Select(t=>FestivalHudText.MoneyFlashOn(t)),Is.EqualTo(new[]{true,true,false,true,false,false,false}),"lit on the even quarter-seconds of the stamp's 1.5 s");
            Assert.That(new[]{0f,.1f,.3f,.5f,1.4f,1.5f,-1f}.Select(t=>FestivalHudText.MoneyFlashOn(t,true)),Is.EqualTo(new[]{true,true,true,true,true,false,false}),"Reduced Motion: lit through the 1.5 s, no blink");
            var sold=new FestivalHudText.MoneyFlash();you.Cash-=10;palm.LevelSales-=10;sold.Watch(palm,you,0,true,9);you.Cash+=10;palm.LevelSales+=10;sold.Watch(palm,you,0,true,10);
            foreach(float t in new[]{10f,10.3f,10.8f,11.3f,11.45f})Assert.That(FestivalHudText.Vitals(palm,you,lit:sold.Lit(t,true)),Is.EqualTo(Lit),"Reduced Motion keeps the sale's cash and SALES lit at "+(t-10)+" s");
            Assert.That(FestivalHudText.Vitals(palm,you,lit:sold.Lit(11.5f,true)),Is.EqualTo(Unlit),"and unlit once the 1.5 s end");
            Assert.That(FestivalHudText.SaleStampSeconds,Is.EqualTo(1.5f));
        }

        // The stamp follows your own outcomes, not the chat window, so a sale whose window Esc shut is stamped too. Each sale is
        // stamped once, and a rejoin or a level's first view only takes note of the sales already there.
        [Test] public void TheStampShowsEachSaleOnceAndNoneFromBeforeYouJoined()
        {
            var s=Day(0);s.RoundId="day_1";var you=Crew(s,"you",0);var news=new FestivalHudText.SaleNews();
            ConversationOutcome Ended(string kind,string result){var o=new ConversationOutcome{RoundId=s.RoundId,InteractionId="talk_"+you.ConversationOutcomes.Count,Kind=kind,Result=result,Sequence=++you.ConversationOutcomeSequence};you.ConversationOutcomes.Add(o);return o;}
            Ended("Sale","SOLD +$10");
            Assert.That(news.Follow(s,you,0),Is.Null,"a sale from before your first view");
            Assert.That(news.Follow(s,you,0),Is.Null,"nor on the next frame");
            var sold=Ended("Sale","SOLD +$15");
            Assert.That(news.Follow(s,you,0),Is.SameAs(sold),"a sale that ends while you watch");
            Assert.That(news.Follow(s,you,0),Is.Null,"stamped once");
            Ended("Buy","");Ended("Conversation","");Ended("Sale","");
            Assert.That(news.Follow(s,you,0),Is.Null,"a buy, a chat and a sale with no result");
            var back=Ended("Sale","NO SALE");
            Assert.That(news.Follow(s,you,0),Is.SameAs(back),"a sale that comes back");
            Ended("Sale","NO SALE");
            Assert.That(news.Follow(s,you,1),Is.Null,"a rejoin's first view");
            Assert.That(news.Follow(s,you,1),Is.Null,"nor the rejoin's next frame");
            var busted=Ended("Sale","BUSTED");
            Assert.That(news.Follow(s,you,1),Is.SameAs(busted),"a sale that ends after the rejoin");
            s.RoundId="night_1";you.ConversationOutcomes.Add(new ConversationOutcome{RoundId=s.RoundId,InteractionId="talk_night",Kind="Sale",Result="SOLD +$10",Sequence=++you.ConversationOutcomeSequence});
            Assert.That(news.Follow(s,you,1),Is.Null,"a new level's first view");
            Assert.That(news.Follow(s,you,1),Is.Null,"nor the new level's next frame");
        }

        // The chat window is open on the sale's last line while the stamp shows, and the stamp overlaps its top-left corner. Drawn
        // over it, the stamp hides while the pass, the map or a dance covers the screen. It catches no clicks, and it waits while a
        // card it would draw over shows. Its 1.5 s run only while it shows, so a card up longer than that (a first-time tip, 6 s)
        // holds the whole stamp until it goes; a level that ends under a card drops it.
        [Test] public void TheSaleStampDrawsOverTheChatWindowAndHidesUnderThePass()
        {
            const System.Reflection.BindingFlags Private=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var go=new UnityEngine.GameObject("Sale stamp HUD");
            try
            {
                var hud=go.AddComponent<FestivalHud>();typeof(FestivalHud).GetMethod("Awake",Private).Invoke(hud,null);
                var stamp=hud.transform.Find("Festival HUD/Sale stamp");var chat=hud.transform.Find("Festival HUD/Chat window");
                Assert.That(stamp!=null&&chat!=null,Is.True,"setup: the HUD builds both");
                Assert.That(stamp.GetSiblingIndex(),Is.GreaterThan(chat.GetSiblingIndex()),"the stamp is drawn after the chat window");
                var show=typeof(FestivalHud).GetMethod("ShowSaleStamp",Private);var track=typeof(FestivalHud).GetMethod("TrackSale",Private);
                var day=Day(0);day.RoundId="day_1";var you=Crew(day,"you",0);
                void Sell(){track.Invoke(hud,new object[]{day,you});you.ConversationOutcomes.Add(new ConversationOutcome{RoundId=day.RoundId,InteractionId="talk_"+you.ConversationOutcomes.Count,Kind="Sale",Result="SOLD +$10",Sequence=++you.ConversationOutcomeSequence});track.Invoke(hud,new object[]{day,you});}
                bool Shows(bool uncovered,float seconds){show.Invoke(hud,new object[]{day,uncovered,seconds});return stamp.gameObject.activeSelf;}
                Assert.That(stamp.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Where(g=>g.raycastTarget).Select(g=>g.name),Is.Empty,"the stamp catches no clicks");
                var covered=new[]{"Light Show questions","First-time tip card","Counter price confirmation","Life banner","Held item label"}.Select(name=>hud.transform.Find("Festival HUD/"+name)).ToArray();
                Assert.That(covered,Has.None.Null,"setup: the HUD builds each card the stamp would cover");
                foreach(var card in covered)card.gameObject.SetActive(false);
                Assert.That(Shows(true,0),Is.False,"no stamp before a sale");
                Sell();Assert.That(Shows(true,0),Is.True,"a sale just stamped, with nothing over it");
                foreach(var card in covered)
                {
                    card.gameObject.SetActive(true);Assert.That(Shows(true,0),Is.False,"hidden while the "+card.name+" shows");
                    card.gameObject.SetActive(false);
                }
                Assert.That(Shows(true,0),Is.True,"back once the card goes");
                Assert.That(Shows(false,0),Is.False,"under the pass");
                var tip=covered[1].gameObject;Sell();tip.SetActive(true);
                for(int frame=0;frame<4;frame++)Assert.That(Shows(true,.5f),Is.False,"setup: under the tip for "+(frame+1)*.5f+" s");
                tip.SetActive(false);
                Assert.That(new[]{Shows(true,.5f),Shows(true,.5f),Shows(true,.5f),Shows(true,.5f)},Is.EqualTo(new[]{true,true,true,false}),"a stamp a tip held for 2 s shows its whole 1.5 s once the tip goes");
                Sell();tip.SetActive(true);Shows(true,.5f);day.Phase="Results";Shows(true,.5f);tip.SetActive(false);day.Phase="Playing";
                Assert.That(Shows(true,0),Is.False,"a level that ends under the tip drops its stamp");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();}
        }

        // In the Esc menu's CREW + NEARBY list, a deposit keeps the menu open for the next click, and a dance shuts it so the dance
        // has your keys.
        [Test] public void TheMenusListStaysOpenForADepositAndShutsForADance()
        {
            const System.Reflection.BindingFlags Private=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var go=new UnityEngine.GameObject("Menu list HUD");FestivalSession session=null;
            try
            {
                session=go.AddComponent<FestivalSession>();typeof(FestivalSession).GetMethod("Awake",Private).Invoke(session,null);
                var hud=go.AddComponent<FestivalHud>();typeof(FestivalHud).GetMethod("Awake",Private).Invoke(hud,null);
                var day=Day(0);var you=Crew(day,"you",0);you.Cash=40;
                day.Stashes.Add(new StashState{Id="box",X=0,Z=1});day.Npcs.Add(new NpcState{Id="buyer_1",Role="Buyer",X=0,Z=1.5f,Yaw=180});
                typeof(FestivalHud).GetMethod("UpdateActions",Private).Invoke(hud,new object[]{day,you});
                var rows=go.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b=>b.gameObject.activeSelf&&b.name.StartsWith("Action:")).ToList();
                var deposit=rows.Find(b=>b.name=="Action:Deposit $5 in shared stash");var dance=rows.Find(b=>b.name=="Action:Dance with festivalgoer");
                Assert.That(deposit!=null&&dance!=null,Is.True,"setup: the list offers both: "+string.Join(" | ",rows.Select(b=>b.name)));
                session.MenuOpen=true;deposit.onClick.Invoke();
                Assert.That(session.MenuOpen,Is.True,"a deposit keeps the menu open for the next click");
                dance.onClick.Invoke();
                Assert.That(session.MenuOpen,Is.False,"a dance shuts it, so the dance has your keys");
            }
            finally
            {
                if(session!=null){session.Controls?.Dispose();(typeof(FestivalSession).GetProperty("Settings")?.GetValue(session) as System.IDisposable)?.Dispose();
                    if(session.ViewCamera!=null)UnityEngine.Object.DestroyImmediate(session.ViewCamera.gameObject);
                    var actors=typeof(FestivalSession).GetField("actorRoot",Private).GetValue(session) as UnityEngine.Transform;if(actors!=null)UnityEngine.Object.DestroyImmediate(actors.gameObject);}
                UnityEngine.Object.DestroyImmediate(go);UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            }
        }
        // Every command the list sends: a click on one that starts something shuts the menu, and any other keeps it open.
        [TestCase("Dance",true)][TestCase("Conversation",true)][TestCase("StartSale",true)][TestCase("StartBuy",true)]
        [TestCase("ConfirmDance",true)][TestCase("ConfirmChat",true)][TestCase("Police",true)]
        [TestCase(FestivalSimulation.RideWheelKind,true)][TestCase(FestivalSimulation.RideCarKind,true)][TestCase(FestivalSimulation.DeckSetKind,true)][TestCase("Dj",true)]
        [TestCase("Extract",true)][TestCase("LostProperty",true)][TestCase("FindFriend",true)][TestCase("BeginRelease",true)][TestCase("BeginRevival",true)]
        [TestCase("Rescue",true)][TestCase("Drag",true)][TestCase("CarryBody",true)][TestCase("HelpSelf",true)][TestCase("Use",true)]
        [TestCase(FestivalSimulation.LieDownKind,true)][TestCase("Cancel",true)][TestCase(FestivalSimulation.TestFreeKind,true)]
        [TestCase("Deposit",false)][TestCase("Withdraw",false)][TestCase("Pickup",false)][TestCase("Transfer",false)][TestCase("AcceptTransfer",false)]
        [TestCase("CancelTransfer",false)][TestCase("Chime",false)][TestCase("Buy",false)][TestCase(FestivalSimulation.GrabGiggleTankKind,false)][TestCase("",false)]
        public void AListedCommandShutsTheMenuOnlyWhenItStartsSomething(string kind,bool closes)=>Assert.That(FestivalHudText.ClosesMenu(kind),Is.EqualTo(closes),kind==""?"a line that does nothing":kind);

        [Test] public void TheHudLeavesEveryAmountToTheFormatter()
        {
            // The HUD and the shelves' price tags never write a dollar sign themselves: every amount, down to the stash, release and
            // revive buttons and the night market's prompts, goes through FestivalHudText.Money or MoneyText. Paths are from the
            // project root, the test runner's working directory.
            // ponytail: a whole-file scan, so an interpolated log line would trip it too; scan string literals only if that bites.
            foreach(var path in new[]{"Assets/Festival/Runtime/Presentation/FestivalHud.cs","Assets/Festival/Runtime/Presentation/FestivalWorld.cs"})
                Assert.That(File.ReadAllText(path),Does.Not.Contain("$"),path+" writes a dollar sign; use FestivalHudText.Money");
        }
    }
}
