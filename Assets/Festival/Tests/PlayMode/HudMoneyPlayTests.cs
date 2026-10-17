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
using UnityEngine.TestTools;
using UnityEngine.UI;

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
                // The host's camp card, which now also says where stock comes from beside the festival pick (ECON-2), still draws whole.
                // ECON-6: the camp card names the starter allowance and festivalgoer prices, both in this festival's money.
                Expect("at the shelf","Objective detail","F  switch to Palm Mirage (1 of 2 unlocked) • One starter each at camp: Tongue Stamps or Fun Guys for 5 friendship bracelets. Buy more from festivalgoers (5 friendship bracelets each; some are narcs). Keep some money for more stock.");
                Fits("at the shelf","Prompt","Objective detail");NoDollars("at the shelf");
                // The price tags on both shelves, each lettered within its painted card.
                int tags=0;
                foreach(var tag in world.GetComponentsInChildren<TextMesh>(true))
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
                yield return new WaitForSeconds(.4f);
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
                    string label=actor.GetComponentInChildren<TextMesh>(true)?.text;
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
