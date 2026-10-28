using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace Festival.Tests
{
    public sealed class HudMoneyPlayTests:FestivalPlayModeTest
    {
        // HUD-3 with PLAYA-1: on Ember Playa the running HUD never shows a dollar. Hosts a real round as the crew member who counts
        // in friendship bracelets, the longest odd object: at camp they eye the practice poi on the gear shelf (ECON-2: no shelf
        // sells stock), pick it up, show it to the seller and pay; out on Day 1 they read their pocket, the stash, the sales and the
        // quota with security closing in, try to leave early, which the host refuses in dollars, and buy a Tongue Stamp from a
        // festivalgoer, whose price and reply read in bracelets too. Labels are laid out on a
        // 1920x1080 canvas and must draw whole, and the shelf's price tags must stay within their slot.
        [UnityTest]public IEnumerator EmberPlayaCountsInYourOddObjects()
        {
            var world=new GameObject("HUD money world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD money");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            void Fits(string where,params string[] labels)
            {
                Canvas.ForceUpdateCanvases();
                foreach(var label in labels)
                {
                    var text=Find(label);if(text==null){failures.Add(where+": "+label+" is hidden");continue;}
                    int shown=text.cachedTextGenerator.characterCountVisible;
                    if(shown<text.text.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" (font "+text.fontSize+") cuts \""+text.text.Replace("\n"," | ")+"\" after \""+text.text.Substring(0,shown).Replace("\n"," | ")+"\"");
                }
            }
            // Nothing on screen names a dollar here.
            void NoDollars(string where){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.text.Contains("$"))failures.Add(where+": "+text.name+" reads \""+text.text.Replace("\n"," | ")+"\"");}
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            void PressE()=>((System.Action)primary.GetValue(view))?.Invoke();
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);
                var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("money_mate","Sam");mate.Ready=true;
                // The weekend is at Ember Playa, so both festivals are unlocked, and you are the crew member who counts in friendship bracelets.
                sim.State.FestivalIndex=Festivals.PlayaFestival;sim.State.UnlockedFestivalCount=2;player.Ordinal=3;
                Assert.That(Festivals.CurrencyName(Festivals.PlayaFestival,3,2),Is.EqualTo("2 friendship bracelets"),"setup: the longest odd object");
                int shelf=sim.State.VendorOffers.IndexOf("poi_practice");Assert.That(shelf,Is.GreaterThanOrEqualTo(0),"setup: the practice poi is on the shelf");

                // At the shelf, eyeing the practice poi.
                var spot=Catalog.ShopPoint(true,shelf);player.X=spot.X;player.Z=6.3f;
                typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,0f);
                typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,-Mathf.Atan2((shelf<4?1.17f:2.05f)-1.65f,9f-6.3f)*Mathf.Rad2Deg);
                yield return new WaitForSeconds(.5f);
                Expect("at the shelf","Prompt","PICK UP PRACTICE POI  •  5 FRIENDSHIP BRACELETS  •  Reusable dance tool: 15% wider good window, capped at 175 ms. Pair with a friend for rescue.");
                // The host's camp card, which now also says where stock comes from beside the test-build festival pick (ECON-2, DEV-1), still draws whole.
                // ECON-6: the camp card names the starter allowance and festivalgoer prices, both in this festival's money.
                Expect("at the shelf","Objective detail","F  switch to Palm Mirage (test build: all festivals) • One starter each at camp: Tongue Stamps or Fun Guys for 5 friendship bracelets. Buy more from festivalgoers (5 friendship bracelets each; some are narcs). Keep some money for more stock.");
                Fits("at the shelf","Prompt","Objective detail");NoDollars("at the shelf");
                // The price tags on both shelves, each lettered within its painted card.
                int tags=0;
                foreach(var tag in world.GetComponentsInChildren<TextMeshPro>(true))
                {
                    if(tag.name!="Name and price")continue;tags++;
                    if(tag.text.Contains("$")||!tag.text.Contains("FRIENDSHIP BRACELET"))failures.Add("at the shelf: a price tag reads \""+tag.text.Replace("\n"," | ")+"\"");
                    var card=tag.transform.parent.Find("Painted price tag");if(card==null){failures.Add("at the shelf: the tag \""+tag.text.Replace("\n"," | ")+"\" has no painted card");continue;}
                    float width=tag.GetComponent<MeshRenderer>().bounds.size.x,cardWidth=card.GetComponentInChildren<Renderer>().bounds.size.x;
                    if(width>cardWidth)failures.Add("at the shelf: the tag \""+tag.text.Replace("\n"," | ")+"\" is "+width.ToString("0.00")+" m wide, past its "+cardWidth.ToString("0.00")+" m card");
                }
                if(tags==0)failures.Add("at the shelf: no price tags");

                // Picked up, shown to the seller, paid.
                PressE();yield return new WaitForSeconds(.4f);
                Assert.That(player.HeldOfferId,Is.EqualTo("poi_practice"),"setup: E picks up the practice poi ("+session.Message+"), after:\n"+string.Join("\n",failures));
                player.X=0;player.Z=7;yield return new WaitForSeconds(.4f);
                Expect("holding it","Held item price","5 FRIENDSHIP BRACELETS");
                Expect("holding it","Held item details","Reusable dance tool: 15% wider good window, capped at 175 ms. Pair with a friend for rescue.");
                Fits("holding it","Held item title","Held item price","Held item details");
                PressE();yield return new WaitForSeconds(.4f);
                Expect("at the counter","Handoff price","E  PAY 5 FRIENDSHIP BRACELETS  •  PRACTICE POI    G CANCEL");
                Expect("at the counter","Prompt","PAY 5 FRIENDSHIP BRACELETS FOR PRACTICE POI");
                Fits("at the counter","Handoff price","Prompt","Held item price");NoDollars("at the counter");
                PressE();yield return new WaitForSeconds(.4f);
                Assert.That(player.Inventory.Exists(i=>i.ItemId=="poi_practice"),Is.True,"setup: E pays for the practice poi ("+session.Message+")");
                Fits("paid","Vitals");NoDollars("paid");

                // Out on Day 1.
                yield return StartLevel(session);
                // Mid-day with money in every pot and security about to detain you; no effects trail the warning.
                player.Effects.Clear();player.Cash=187;sim.State.StashCash=45;sim.State.LevelSales=12;player.X=0;player.Z=0;mate.X=30;mate.Z=0;
                sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");cop.X=1.5f;cop.Z=0;cop.Evidence.Add(new EvidenceState{PlayerId=player.Id,Kind="WitnessedDeal",DetainAt=sim.State.SimulationSeconds+300});
                // The card lights each figure the setup just moved, so read it once the flash is over.
                yield return new WaitForSeconds(FestivalHudText.SaleStampSeconds+.2f);
                Expect("Day 1","Vitals","HP 100  •  187 FRIENDSHIP BRACELETS  •  STASH 45 FRIENDSHIP BRACELETS\nSALES 12 FRIENDSHIP BRACELETS  •  ALIVE  •  ARREST WARNING");
                int quota=FestivalSimulation.DayQuota(sim.State);
                Expect("Day 1","Objective title","DAY QUOTA  12 FRIENDSHIP BRACELETS / "+quota+" FRIENDSHIP BRACELETS");
                // ECON-3: with no stock yet, the card's next step says where it comes from, after the longest money in the game.
                Expect("Day 1","Objective detail","Sell "+(quota-12)+" friendship bracelets more before sundown. You have nothing to sell: buy from festivalgoers, and some are narcs.");
                Fits("Day 1","Vitals","Objective title","Objective detail");NoDollars("Day 1");
                // Heading home before the quota is met: the host's refusal names the shortfall in dollars.
                session.Command("Extract");yield return new WaitForSeconds(.4f);
                Expect("leaving early","Notice","Sell "+(quota-12)+" friendship bracelets more to meet the day's quota");
                Fits("leaving early","Notice");NoDollars("leaving early");
                // At every larger text size the card still draws the warning whole after the longest money in the game, and a stop's
                // ways out too.
                foreach(float scale in new[]{1.1f,1.2f,1.3f})
                {
                    session.Profile.Data.TextScale=scale;string at=" at "+Mathf.RoundToInt(scale*100)+"% text";
                    cop.Evidence.Clear();cop.Evidence.Add(new EvidenceState{PlayerId=player.Id,Kind="WitnessedDeal",DetainAt=sim.State.SimulationSeconds+300});
                    yield return new WaitForSeconds(.4f);
                    Expect("Day 1"+at,"Vitals","HP 100  •  187 FRIENDSHIP BRACELETS  •  STASH 45 FRIENDSHIP BRACELETS\nSALES 12 FRIENDSHIP BRACELETS  •  ALIVE  •  ARREST WARNING");
                    Fits("Day 1"+at,"Vitals");
                    cop.Evidence.Clear();player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
                    yield return new WaitForSeconds(.4f);
                    Expect("stopped"+at,"Vitals","HP 100  •  187 FRIENDSHIP BRACELETS  •  STASH 45 FRIENDSHIP BRACELETS\nSALES 12 FRIENDSHIP BRACELETS  •  ALIVE  •  SECURITY STOP • TALK (E) • DITCH IT (G) • OR WALK AWAY");
                    Fits("stopped"+at,"Vitals");
                    player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");player.EquippedItemId="";
                }
                session.Profile.Data.TextScale=1;

                // ECON-2: beside a festivalgoer the HUD offers their kind of stock at its price in your bracelets. Bought out of the
                // cops' sight, the talk says it's a buy, and the reply names the price in bracelets too.
                sim.State.Npcs.Remove(cop);
                var seller=new NpcState{Id="money_seller_2",Role="Regular",X=0,Z=1.5f,Yaw=180};sim.State.Npcs.Add(seller);
                yield return new WaitForSeconds(.4f);
                const string BuyLabel="Buy Tongue Stamps  •  5 friendship bracelets";
                Button buy=null;foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.gameObject.activeSelf&&b.name=="Action:"+BuyLabel)buy=b;
                if(buy==null){var listed=new List<string>();foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.gameObject.activeSelf&&b.name.StartsWith("Action:"))listed.Add(b.name.Substring(7));failures.Add("beside a festivalgoer: no \""+BuyLabel+"\" among \""+string.Join(" | ",listed)+"\"");}
                else
                {
                    buy.onClick.Invoke();yield return new WaitForSeconds(.4f);
                    var dance=sim.Interaction(player.InteractionId);
                    Assert.That(dance?.Kind,Is.EqualTo("Buy"),"setup: the Buy action starts a buy ("+session.Message+"), after:\n"+string.Join("\n",failures));
                    // DIALOG-3: talked through with the smooth reply every turn, after a perfect dance (over by the next tick) if the
                    // seller asks for one first. The talk is on the chat window, which says it's a buy.
                    if(dance.TrustDance)
                    {
                        foreach(var note in RhythmChart.For(dance).Notes)dance.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
                        dance.StartSeconds=sim.State.SimulationSeconds-dance.DurationSeconds-1;
                        yield return new WaitForSeconds(.4f);
                    }
                    Expect("buying","Chat header",FestivalChatWindow.BuyHeader);
                    while(dance.Status=="Active"&&dance.Turns.Count>0)
                    {
                        var turn=dance.Turns[dance.Turns.Count-1];
                        Assert.That(sim.Execute(player.Id,new GameCommand{Id="money_reply_"+dance.Turns.Count,Kind="Reply",Amount=System.Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=dance.Turns.Count-1}).Accepted,Is.True,"setup: a smooth reply");
                    }
                    yield return new WaitForSeconds(.4f);
                    Assert.That(player.Inventory.Exists(i=>i.ItemId=="stock_lsd"),Is.True,"setup: the buy hands over a Tongue Stamp ("+session.Message+")");
                    Expect("bought","Notice","Bought Tongue Stamps for 5 friendship bracelets");
                    Fits("bought","Notice");NoDollars("bought");

                    // That unit offered to a buyer at the Greedy ask from the list, talked through smoothly. The talk names
                    // the ask; the sale stamps its result mid-screen for 1.5 s, says it in the notice and the chat window, and flashes the
                    // card's cash and SALES, all in your bracelets.
                    sim.State.Npcs.Remove(seller);
                    string buyerId="money_buyer_0";for(int k=1;DialogueGrammar.PersonaFor(buyerId,"")=="Raver"||DialogueGrammar.PersonaFor(buyerId,"")=="Burner";k++)buyerId="money_buyer_"+k;
                    var buyer=new NpcState{Id=buyerId,Role="Buyer",X=0,Z=1.5f,Yaw=180};sim.State.Npcs.Add(buyer);
                    yield return new WaitForSeconds(.4f);
                    string greedy=FestivalHudText.OfferAction(sim.State,player,buyer,"stock_lsd",FestivalSimulation.GreedyAsk);
                    Button ask=null;foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.gameObject.activeSelf&&b.name=="Action:"+greedy)ask=b;
                    if(ask==null)failures.Add("beside a buyer: no \""+greedy+"\"");
                    else
                    {
                        int cash=player.Cash;ask.onClick.Invoke();yield return new WaitForSeconds(.4f);
                        var sale=sim.Interaction(player.InteractionId);
                        Assert.That(sale!=null&&sale.Kind=="Sale"&&sale.Ask==FestivalSimulation.GreedyAsk,Is.True,"setup: the Greedy ask starts a Greedy sale ("+session.Message+")");
                        Expect("selling","Chat header",FestivalChatWindow.HeaderFor(sim.State,sale,player));
                        while(sale.Status=="Active"&&sale.Turns.Count>0)
                        {
                            var turn=sale.Turns[sale.Turns.Count-1];
                            Assert.That(sim.Execute(player.Id,new GameCommand{Id="money_sale_"+sale.Turns.Count,Kind="Reply",Amount=System.Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=sale.Turns.Count-1}).Accepted,Is.True,"setup: a smooth reply");
                        }
                        bool lit=false;float shownAt=Time.unscaledTime;
                        while(Time.unscaledTime-shownAt<1f){lit|=Read("Vitals").Contains("<color=");yield return null;}
                        int paid=player.Cash-cash;Assert.That(paid,Is.GreaterThan(0),"setup: the Greedy sale paid ("+session.Message+")");
                        string hostNotice=player.ConversationOutcomes.Find(o=>o.InteractionId==sale.Id)?.Notice;
                        string amount=FestivalHudText.Money(sim.State,player,paid);
                        Expect("sold","Notice","SOLD Tongue Stamps  •  +"+amount);
                        Expect("sold","Sale result",("SOLD +"+amount).ToUpperInvariant());
                        if(!lit)failures.Add("sold: the card's cash and SALES never flash");
                        var chat=hud.GetComponentInChildren<FestivalChatWindow>(true);var lines=new List<string>();for(int i=0;i<chat.Bubbles;i++)lines.Add(chat.Shown(i));
                        if(lines.Count==0||lines[lines.Count-1]!="SOLD Tongue Stamps  •  +"+amount)failures.Add("sold: the chat window doesn't end on the sale: "+string.Join(" | ",lines));
                        Fits("sold","Notice","Sale result");NoDollars("sold");
                        yield return new WaitForSeconds(FestivalHudText.SaleStampSeconds);
                        if(Find("Sale result")!=null)failures.Add("sold: the stamp stays up past its "+FestivalHudText.SaleStampSeconds+" s");
                        Expect("after the stamp","Notice","SOLD Tongue Stamps  •  +"+amount);
                        // The window acknowledges the outcome once it shows whole, after its reaction types out. That must leave the notice.
                        float typed=Time.realtimeSinceStartup+10;while(!chat.OutcomeDisplayed&&Time.realtimeSinceStartup<typed)yield return null;yield return null;
                        if(!chat.OutcomeDisplayed)failures.Add("sold: the chat window never shows the whole outcome");
                        else if(session.Message!=hostNotice)failures.Add("sold: the outcome's acknowledgement turns the notice \""+hostNotice+"\" into \""+session.Message+"\"");
                    }
                }
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Ember Playa's money on screen:\n"+string.Join("\n",failures));
        }

        // Lost property handed in through a real hosted session pays $5: the notice names the amount, and the card's cash flashes.
        // Only the task's wait is skipped.
        [UnityTest]public IEnumerator ReturningLostPropertySaysWhatItPaid()
        {
            var world=new GameObject("Lost property world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Lost property HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                yield return StartLevel(session);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);sim.State.Npcs.Clear();player.X=-28;player.Z=16;int cash=player.Cash;
                yield return null;
                session.Command("LostProperty");yield return null;
                var task=sim.Interaction(player.InteractionId);
                Assert.That(task?.Kind,Is.EqualTo("LostProperty"),"setup: the task starts ("+session.Message+")");
                task.StartSeconds=sim.State.SimulationSeconds-task.DurationSeconds;
                for(float end=Time.realtimeSinceStartup+5;player.Cash==cash&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(player.Cash,Is.EqualTo(cash+5),"setup: the task pays ("+session.Message+")");
                const string Paid="LOST PROPERTY RETURNED  •  +$5";bool lit=false;
                for(float end=Time.realtimeSinceStartup+1;Time.realtimeSinceStartup<end;){lit|=Read("Vitals").Contains("<color=");yield return null;}
                Assert.That(Read("Notice"),Is.EqualTo(Paid));
                Assert.That(lit,Is.True,"the card's cash flashes");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
        }

        // Dying with gear and a Giggle Tank loses the tank in the step that drops the gear. The gear notice writes the line itself
        // for its 5 s, so the tank's line must wait and show after it, not under it.
        [UnityTest]public IEnumerator ATankLostAsYouDieShowsAfterTheGearNotice()
        {
            var world=new GameObject("Tank death world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Tank death HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                yield return StartLevel(session);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                sim.State.Npcs.Clear();sim.State.ElapsedSeconds=FestivalHudText.BriefingSeconds+1;player.Cash=0;
                player.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});player.Inventory.Add(new ItemStack{ItemId=FestivalSimulation.GiggleTankItem,Count=1});
                yield return new WaitForSeconds(.3f);
                player.Life="Downed";player.DownedRemaining=.01;yield return new WaitForSeconds(.3f);
                Assert.That(player.Life,Is.EqualTo("Spirit"),"setup: the player dies");
                Assert.That(player.MoneyNewsLog.Exists(n=>n.Text=="GIGGLE TANK LOST"),Is.True,"setup: the death loses the tank");
                Assert.That(Read("Notice"),Is.EqualTo(FestivalSimulation.DeathGearLossNotice));
                yield return new WaitForSeconds(5.2f);
                Assert.That(Read("Notice"),Is.EqualTo("GIGGLE TANK LOST"),"the tank's line shows once the gear notice ends");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
        }

        // A money line from the same moment waits MoneyNewsSeconds behind the line that just appeared: a buy that ends in full hands
        // still names its price, and an offer to an undercover narc still says why you were busted.
        [UnityTest]public IEnumerator AMoneyLineWaitsBehindTheLineThatJustAppeared()
        {
            var world=new GameObject("Money line world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Money line HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            var failures=new List<string>();
            IEnumerator Expect(string what,string first,string then)
            {
                float drawn=0;var seen=new List<string>();
                for(float end=Time.realtimeSinceStartup+6;Time.realtimeSinceStartup<end&&!seen.Contains(then);)
                {
                    string shown=Read("Notice");if(shown==first)drawn+=Time.unscaledDeltaTime;
                    if(seen.Count==0||seen[seen.Count-1]!=shown)seen.Add(shown);yield return null;
                }
                if(drawn<FestivalHudText.MoneyNewsSeconds-.5f||!seen.Contains(then))failures.Add(what+": \""+first+"\" drawn for "+drawn.ToString("0.00")+" s; the line showed "+string.Join(" | ",seen));
            }
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                yield return StartLevel(session);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                sim.State.Npcs.Clear();sim.State.ElapsedSeconds=FestivalHudText.BriefingSeconds+1;player.Effects.Clear();player.Cash=20;player.X=0;player.Z=0;
                int sequence=0;
                bool Do(string kind,string target="",string item="",int amount=0,int direction=0)=>
                    sim.Execute(player.Id,new GameCommand{Id="line_"+(sequence++),Kind=kind,TargetId=target,ItemId=item,Amount=amount,Direction=direction}).Accepted;
                string seller="";for(int n=0;seller=="";n++)if(DialogueGrammar.PersonaFor("line_seller_"+n,"")=="Wook"&&FestivalSimulation.SellerStock("line_seller_"+n)=="stock_lsd")seller="line_seller_"+n;
                sim.State.Npcs.Add(new NpcState{Id=seller,Kind="Wook",Role="Regular",X=0,Z=1.5f,Yaw=180,CanTalk=true});
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";
                foreach(var item in new[]{"poi_practice","stage_pass"})player.Inventory.Add(new ItemStack{ItemId=item,Count=1});
                session.SetMessage("");yield return new WaitForSeconds(.3f);
                Assert.That(Do("StartBuy",seller,"stock_lsd"),Is.True,"setup: the buy starts");
                yield return new WaitForSeconds(.3f);
                var talk=sim.Interaction(player.InteractionId);Assert.That(talk?.Kind,Is.EqualTo("Buy"),"setup: buying");
                // A buy needs a free hand to start; a handoff accepted mid-buy (the host allows it) fills the last one.
                player.Inventory.Add(new ItemStack{ItemId="medical_voucher",Count=1});
                while(talk.Status=="Active"&&talk.Turns.Count>0)
                    Assert.That(Do("Reply",amount:System.Array.IndexOf(talk.Turns[talk.Turns.Count-1].Grades,ConversationLines.Smooth),direction:talk.Turns.Count-1),Is.True,"setup: a smooth reply");
                yield return Expect("a buy into full hands","Bought Tongue Stamps for $5","HANDS FULL  •  Tongue Stamps dropped at your feet");

                sim.State.Npcs.Clear();player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
                string narc="line_narc_0";for(int n=1;DialogueGrammar.PersonaFor(narc,"")!="Wook";n++)narc="line_narc_"+n;
                sim.State.Npcs.Add(new NpcState{Id=narc,Kind="Wook",Role="Narc",X=0,Z=1.5f,Yaw=180,CanTalk=true});
                // The buy's own money line holds the notice for its MoneyNewsSeconds first.
                session.SetMessage("");yield return new WaitForSeconds(FestivalHudText.MoneyNewsSeconds+.5f);
                session.Command("StartSale",target:narc,item:"stock_lsd",amount:FestivalSimulation.FairAsk);
                yield return Expect("an offer to an undercover narc","Busted! That was an undercover narc.","SECURITY FINE  •  −$10  •  SECURITY TOOK 1 Tongue Stamps");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;
            Assert.That(failures,Is.Empty,"A money line and the line that just appeared:\n"+string.Join("\n",failures));
        }

        // A crewmate's sale line that your own sale's notice replaces .4 s later shows again once your sale's notice has had its
        // MoneyNewsSeconds, so both sales read in full. Your last reply goes in by its 1-3 key, so its accepted clear lands inside the
        // HUD's frame, before the sale's notice.
        [UnityTest]public IEnumerator AFriendsSaleLineComesBackAfterYourOwnSale()
        {
            UseAVirtualKeyboard("Two sales keyboard");
            var world=new GameObject("Two sales world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Two sales HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            var log=new List<string>();float friendFor=0,mineFor=0;
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("two_sales_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                sim.State.Npcs.Clear();sim.State.ElapsedSeconds=FestivalHudText.BriefingSeconds+1;player.X=0;player.Z=0;mate.X=20;mate.Z=0;
                foreach(var who in new[]{player,mate}){who.Inventory.Clear();who.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});who.EquippedItemId="stock_lsd";}
                string Buyer(string stem){string id=stem+"0";for(int k=1;DialogueGrammar.PersonaFor(id,"")=="Raver"||DialogueGrammar.PersonaFor(id,"")=="Burner";k++)id=stem+k;return id;}
                var mine=new NpcState{Id=Buyer("two_sales_mine_"),Role="Buyer",X=0,Z=1.5f,Yaw=180};var theirs=new NpcState{Id=Buyer("two_sales_theirs_"),Role="Buyer",X=20,Z=1.5f,Yaw=180};
                sim.State.Npcs.Add(mine);sim.State.Npcs.Add(theirs);
                int sequence=0;
                bool Reply(PlayerState who)
                {
                    var talk=sim.Interaction(who.InteractionId);var turn=talk.Turns[talk.Turns.Count-1];
                    return sim.Execute(who.Id,new GameCommand{Id="two_sales_"+(sequence++),Kind="Reply",Amount=System.Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=talk.Turns.Count-1}).Accepted;
                }
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="two_sales_mine",Kind="StartSale",TargetId=mine.Id,ItemId="stock_lsd"}).Accepted,Is.True,"setup: your sale");
                for(int t=1;t<FestivalSimulation.ConversationTurns;t++)Assert.That(Reply(player),Is.True,"setup: your reply "+t);
                var mySale=sim.Interaction(player.InteractionId);var lastTurn=mySale.Turns[mySale.Turns.Count-1];
                int smooth=System.Array.IndexOf(lastTurn.Grades,ConversationLines.Smooth);
                var chat=hud.GetComponentInChildren<FestivalChatWindow>(true);
                for(float end=Time.realtimeSinceStartup+8;!(chat.Open&&chat.CanReply(smooth))&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(chat.Open&&chat.CanReply(smooth),Is.True,"setup: the chat window offers your last reply");
                session.SetMessage("");yield return null;
                Assert.That(sim.Execute(mate.Id,new GameCommand{Id="two_sales_theirs",Kind="StartSale",TargetId=theirs.Id,ItemId="stock_lsd"}).Accepted,Is.True,"setup: their sale");
                for(var talk=sim.Interaction(mate.InteractionId);talk.Status=="Active";)Assert.That(Reply(mate),Is.True,"setup: their reply");
                int pressed=-1;string last=null;
                for(float start=Time.realtimeSinceStartup,end=start+8;Time.realtimeSinceStartup<end;)
                {
                    if(pressed<0&&Time.realtimeSinceStartup-start>.4f){pressed=0;InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState((Key)((int)Key.Digit1+smooth)));}
                    else if(pressed>=0&&++pressed==3)InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());
                    string shown=Read("Notice");
                    if(shown.StartsWith("SALES"))friendFor+=Time.unscaledDeltaTime;else if(shown.StartsWith("SOLD"))mineFor+=Time.unscaledDeltaTime;
                    if(shown!=last){log.Add((Time.realtimeSinceStartup-start).ToString("0.00")+"s "+shown);last=shown;}
                    yield return null;
                }
                Assert.That(lastTurn.Pick,Is.EqualTo(smooth),"setup: your last reply went in by its key");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
            yield return null;
            string timeline=string.Join(" -> ",log);
            Assert.That(friendFor,Is.GreaterThanOrEqualTo(FestivalHudText.MoneyNewsSeconds),"the friend's sale line shows "+friendFor.ToString("0.00")+" s in all: "+timeline);
            Assert.That(mineFor,Is.GreaterThanOrEqualTo(FestivalHudText.MoneyNewsSeconds-.5f),"your sale's notice shows "+mineFor.ToString("0.00")+" s: "+timeline);
        }

        // Esc opens the menu on the CREW + NEARBY list. A deposit clicked there keeps it open, and the list's heading reads
        // the deposit's line with its amount, since the notice line hides under the pass; a withdrawal's line follows it. A dance
        // clicked there shuts the menu, so the dance has your keys.
        [UnityTest]public IEnumerator TheMenusListStaysOpenForADepositAndReadsItsLine()
        {
            UseAVirtualKeyboard("Menu list keyboard");
            var world=new GameObject("Menu list world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Menu list HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            Button Row(string label)=>hud.GetComponentsInChildren<Button>(false).FirstOrDefault(b=>b.name=="Action:"+label);
            string Listed()=>string.Join(" | ",hud.GetComponentsInChildren<Button>(false).Where(b=>b.name.StartsWith("Action:")).Select(b=>b.name.Substring(7)));
            string Heading()=>hud.GetComponentsInChildren<Text>(false).FirstOrDefault(t=>t.name=="Actions heading")?.text??"(hidden)";
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                yield return StartLevel(session);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                sim.State.Npcs.Clear();sim.State.Visions.Clear();sim.State.ElapsedSeconds=FestivalHudText.BriefingSeconds+1;player.X=0;player.Z=0;player.Cash=40;
                sim.State.Stashes.Add(new StashState{Id="menu_box",X=0,Z=1});sim.State.Npcs.Add(new NpcState{Id="menu_buyer_2",Role="Buyer",X=0,Z=1.5f,Yaw=180});
                yield return new WaitForSeconds(.6f);
                yield return Tap(Key.Escape);yield return new WaitForSeconds(.3f);
                Assert.That(session.MenuOpen,Is.True,"setup: Esc opens the menu");
                Assert.That(Heading(),Is.EqualTo("NEARBY / CREW"),"setup: the list's heading");
                foreach(var (label,line,cash) in new[]{("Deposit $5 in shared stash","INTO THE STASH  •  −$5",35),("Withdraw $5 from shared stash","FROM THE STASH  •  +$5",40)})
                {
                    var row=Row(label);for(float end=Time.realtimeSinceStartup+1;row==null&&Time.realtimeSinceStartup<end;){yield return null;row=Row(label);}
                    Assert.That(row,Is.Not.Null,"the list offers \""+label+"\" (listed: \""+Listed()+"\")");
                    row.onClick.Invoke();
                    string heading=Heading();
                    for(float end=Time.realtimeSinceStartup+FestivalHudText.MoneyNewsSeconds+1;heading!=line&&Time.realtimeSinceStartup<end;){yield return null;heading=Heading();}
                    Assert.That(session.MenuOpen,Is.True,label+" keeps the menu open");
                    Assert.That(player.Cash,Is.EqualTo(cash),"setup: "+label+" moved your cash");
                    Assert.That(heading,Is.EqualTo(line),"the list's heading reads what "+label+" did");
                }
                var dance=Row("Dance with festivalgoer");Assert.That(dance,Is.Not.Null,"the list offers the dance (listed: \""+Listed()+"\")");
                dance.onClick.Invoke();yield return null;
                Assert.That(session.MenuOpen,Is.False,"a dance shuts the menu");
                Assert.That(sim.Interaction(player.InteractionId)?.Kind,Is.EqualTo("Dance"),"setup: the dance started ("+session.Message+")");
                session.Command("Cancel");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
        }

        // A friend's sale lights SALES on the running card, never your cash, which it did not change.
        [UnityTest]public IEnumerator AFriendsSaleLightsSalesButNotYourCash()
        {
            var world=new GameObject("Friend's sale world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Friend's sale HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("lit_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                sim.State.Npcs.Clear();sim.State.ElapsedSeconds=FestivalHudText.BriefingSeconds+1;player.X=0;player.Z=0;mate.X=20;mate.Z=0;
                mate.Inventory.Clear();mate.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});mate.EquippedItemId="stock_lsd";
                string buyer="lit_buyer_0";for(int k=1;DialogueGrammar.PersonaFor(buyer,"")=="Raver"||DialogueGrammar.PersonaFor(buyer,"")=="Burner";k++)buyer="lit_buyer_"+k;
                sim.State.Npcs.Add(new NpcState{Id=buyer,Role="Buyer",X=20,Z=1.5f,Yaw=180});
                yield return new WaitForSeconds(.6f);
                int cash=player.Cash;
                Assert.That(sim.Execute(mate.Id,new GameCommand{Id="lit_sale",Kind="StartSale",TargetId=buyer,ItemId="stock_lsd",Amount=FestivalSimulation.FairAsk}).Accepted,Is.True,"setup: their sale");
                var talk=sim.Interaction(mate.InteractionId);
                for(int n=0;talk.Status=="Active";n++){var turn=talk.Turns[talk.Turns.Count-1];Assert.That(sim.Execute(mate.Id,new GameCommand{Id="lit_reply_"+n,Kind="Reply",Amount=System.Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=talk.Turns.Count-1}).Accepted,Is.True,"setup: their reply");}
                string cashLit="",salesLit="";
                for(float end=Time.realtimeSinceStartup+3;Time.realtimeSinceStartup<end;)
                {
                    string card=Read("Vitals");
                    if(card.Contains("<color=#FC7741>$"+cash+"</color>"))cashLit=card;
                    if(card.Contains("<color=#FC7741>SALES "))salesLit=card;
                    yield return null;
                }
                Assert.That(player.Cash,Is.EqualTo(cash),"setup: your cash is unchanged");
                Assert.That(cashLit,Is.Empty,"a friend's sale lit your unchanged cash: "+cashLit.Replace("\n"," | "));
                Assert.That(salesLit,Is.Not.Empty,"a friend's sale lights SALES");
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
        }

        // LOOP-4: a real joined friend dies with $40 and the roll drops it. The host and the dead friend both see the corner reel name
        // them. The host keeps walking and works the menu while it turns; the menu offers no pickup until the reel lands, then offers
        // the pile, read in dollars over the voucher model, and taking it adds the $40 to the host's cash.
        [UnityTest]public IEnumerator EveryoneSeesTheDeathReelAndTheLandedCashGoesToAFriend()
        {
            var world=new GameObject("Death cash world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Death cash host");var host=hostObject.AddComponent<FestivalSession>();var hud=hostObject.AddComponent<FestivalHud>();
            var friendObject=new GameObject("Death cash friend");var friend=friendObject.AddComponent<FestivalSession>();yield return null;
            var failures=new List<string>();
            Text Reel(GameObject owner,string part)=>owner.GetComponentsInChildren<Text>(false).FirstOrDefault(t=>t.name==part&&t.GetComponentInParent<Canvas>().name==FestivalSpinner.DeathCanvasName);
            Button Action(string label)=>hud.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeSelf&&b.name=="Action:"+label);
            string Listed()=>string.Join(" | ",hud.GetComponentsInChildren<Button>(true).Where(b=>b.gameObject.activeSelf&&b.name.StartsWith("Action:")).Select(b=>b.name.Substring(7)));
            try
            {
                ushort port=FreePort();host.Host("Host",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);friend.Join("Sam","127.0.0.1",port);
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,friend.Message);
                var sim=Simulation(host);var sam=sim.Player(friend.LocalPlayerId);sam.X=0;sam.Z=19;friend.Command("Ready");
                while(!sam.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(sam.Ready,Is.True,"setup: the friend's Ready reached the host");yield return StartLevel(host,friend.LocalPlayerId);
                var me=sim.Player(host.LocalPlayerId);sim.State.Npcs.Clear();sim.State.Drops.Clear();
                me.X=0;me.Z=2;me.Yaw=180;sam.X=0;sam.Z=0;sam.Inventory.Clear();sam.EquippedItemId="";sam.Cash=40;int cash=me.Cash;
                yield return new WaitForSeconds(.4f);
                // Sam dies on the host's own step, on a spin seed found by running that very step on a copy of the round.
                sim.State.SpinSeed=DroppingSeed(sim,sam.Id);sam.Life="Downed";sam.DownedRemaining=.05;sim.Tick(.1);
                var spin=sim.State.DeathSpins.Find(s=>s.PlayerId==sam.Id);
                Assert.That(spin?.CashDropped,Is.EqualTo(40),"setup: Sam's $40 dropped where they fell");
                double Age()=>FestivalSimulation.WorldSeconds(sim.State)-spin.AtWorldSeconds;
                while((Reel(hostObject,"Title")==null||Reel(friendObject,"Title")==null)&&Age()<1)yield return null;
                foreach(var (who,owner) in new[]{("the host",hostObject),("Sam, now a spirit",friendObject)})
                {
                    if(Reel(owner,"Title")?.text!="SAM DIED")failures.Add(who+": the corner reel reads \""+(Reel(owner,"Title")?.text??"(hidden)")+"\"");
                    if(Reel(owner,"Result")?.text!="$40")failures.Add(who+": the reel turns for \""+(Reel(owner,"Result")?.text??"(hidden)")+"\"");
                }
                var reelCanvas=hostObject.GetComponentsInChildren<Canvas>(true).First(c=>c.name==FestivalSpinner.DeathCanvasName);
                var hudCanvas=hostObject.GetComponentsInChildren<Canvas>(true).First(c=>c.name=="Festival HUD");
                if(reelCanvas.sortingOrder>=hudCanvas.sortingOrder||reelCanvas.GetComponent<GraphicRaycaster>()!=null)failures.Add("the reel's canvas sorts at "+reelCanvas.sortingOrder+" against the HUD's "+hudCanvas.sortingOrder+" or takes clicks");
                // The host keeps walking while it turns: the host's own movement input, as the host's keyboard gives it.
                float from=me.X;
                for(float until=Time.realtimeSinceStartup+.3f;Time.realtimeSinceStartup<until;){Walk(host,1,0);yield return null;}
                if(me.X<from+.4f)failures.Add("the host walked "+(me.X-from).ToString("F2")+" m while the reel turned");
                yield return new WaitForSeconds(.3f);me.X=0;me.Z=2;
                // The menu opens, its RESUME takes the click, and it lists no pickup before the reel lands.
                host.MenuOpen=true;yield return null;yield return null;
                var resume=hud.GetComponentsInChildren<Button>(false).FirstOrDefault(b=>b.name=="RESUME FESTIVAL");
                if(resume==null)failures.Add("the open menu has no RESUME FESTIVAL");
                else
                {
                    var hits=new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,resume.transform.position)},hits);
                    if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=resume)failures.Add("a click on RESUME lands on "+(hits.Count==0?"nothing":hits[0].gameObject.name));
                }
                if(Age()>=FestivalSimulation.DeathWheelSeconds)failures.Add("setup: the menu was checked "+Age().ToString("F2")+" s in, after the landing");
                else if(Action("Pick up $40")!=null)failures.Add("the pile is offered "+Age().ToString("F2")+" s in, before the reel lands");
                resume?.onClick.Invoke();yield return null;
                if(host.MenuOpen)failures.Add("RESUME did not close the menu while the reel turned");
                // Landed: the menu offers the pile, read in dollars over the voucher, and taking it pays the host.
                while(Age()<FestivalSimulation.DeathWheelSeconds+.2)yield return null;
                host.MenuOpen=true;yield return new WaitForSeconds(.2f);
                if(Reel(hostObject,"Result")?.text!="DROPPED $40")failures.Add("landed: the host's reel reads \""+(Reel(hostObject,"Result")?.text??"(hidden)")+"\"");
                var pile=sim.State.Drops.Find(d=>d.ItemId=="cash");
                var actors=(Dictionary<string,Transform>)typeof(FestivalSession).GetField("actors",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(host);
                if(pile==null||!actors.TryGetValue(pile.Id,out var actor))failures.Add("landed: the host shows no pile");
                else
                {
                    if(actor.name!="FestivalVoucher")failures.Add("landed: the pile is a "+actor.name+", not the voucher model");
                    string label=actor.GetComponentInChildren<TextMeshPro>(true)?.text;
                    if(label!="$40")failures.Add("landed: the pile's label reads \""+label+"\"");
                }
                var take=Action("Pick up $40");
                if(take==null)failures.Add("landed: the menu offers \""+Listed()+"\", not \"Pick up $40\"");
                else
                {
                    take.onClick.Invoke();yield return new WaitForSeconds(.4f);
                    if(me.Cash!=cash+40)failures.Add("the host has "+me.Cash+" after taking it, not "+(cash+40));
                    if(sim.State.Drops.Exists(d=>d.ItemId=="cash"))failures.Add("the pile is still there");
                }
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
            Assert.That(failures,Is.Empty,"A friend's death cash:\n"+string.Join("\n",failures));
        }

        static int walkSequence=2000000;
        // The host's real movement intent, as its keyboard sends it each frame; batchmode's unfocused keyboard sends none.
        static void Walk(FestivalSession session,float x,float z)
        {
            var type=typeof(FestivalSession).Assembly.GetType("Festival.Network.MoveIntent");var intent=System.Activator.CreateInstance(type);
            type.GetField("X").SetValue(intent,x);type.GetField("Z").SetValue(intent,z);type.GetField("Sequence").SetValue(intent,++walkSequence);
            typeof(FestivalSession).GetMethod("AcceptInput",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new[]{(object)session.LocalPlayerId,intent});
        }
        // The first spin seed on which this player's death on the host's next step drops their cash, by taking that step on a copy.
        static int DroppingSeed(FestivalSimulation sim,string id)
        {
            for(int seed=1;seed<200;seed++)
            {
                var copy=new FestivalSimulation();copy.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(sim.State)));
                copy.State.SpinSeed=seed;var dying=copy.Player(id);dying.Life="Downed";dying.DownedRemaining=.05;copy.Tick(.1);
                if(copy.State.DeathSpins.Exists(s=>s.PlayerId==id&&s.CashDropped>0))return seed;
            }
            Assert.Fail("setup: no seed drops");return 0;
        }
    }
}
