using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    public sealed class HudLoopPlayTests:FestivalPlayModeTest
    {
        // HUD-1: the running HUD puts FestivalHudText's lines on screen. Hosts a real two-player round and
        // reads the labels a player sees on Day 1, on Night 2 with a body out in the field, and at results.
        // Labels are laid out on a 1920x1080 canvas, so a line its box cuts off fails, and E runs the HUD's first action.
        [UnityTest]public IEnumerator HudShowsTheWeekendQuotaChecklistAndBodies()
        {
            var world=new GameObject("HUD loop world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD loop");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            void Fits(string where,string label)
            {
                var text=Find(label);if(text==null){failures.Add(where+": "+label+" is hidden");return;}
                Canvas.ForceUpdateCanvases();
                var drawn=text.cachedTextGenerator;int shown=drawn.characterCountVisible;
                if(shown<text.text.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" (font "+text.fontSize+", line "+(drawn.lineCount>0?drawn.lines[0].height:0)+" px) cuts \""+text.text.Replace("\n"," | ")+"\" after \""+text.text.Substring(0,shown).Replace("\n"," | ")+"\"");
            }
            // E runs the HUD's primary action (FestivalHud.UpdateKeyboard), the first action it lists.
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            void PressE()=>((System.Action)primary.GetValue(view))?.Invoke();
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                // The size a 1080p player sees: a 1920x1080 canvas at scale 1.
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);
                var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("hud_mate","Sam");mate.Ready=true;
                // ECON-2: at camp, with no festival to pick, the card says where stock comes from, and draws whole.
                sim.State.UnlockedFestivalCount=1;yield return new WaitForSeconds(.4f);
                Expect("camp","Objective detail","Browse gear • pay the seller • stock comes from festivalgoers • meet at the trailhead");
                Fits("camp","Objective detail");
                yield return StartLevel(session);

                // Day 1 of Palm Mirage asks $15 from each of the two.
                sim.State.Npcs.Clear();sim.State.LevelSales=12;player.X=0;player.Z=0;mate.X=30;mate.Z=0;
                yield return new WaitForSeconds(.6f);
                Expect("day","Level banner","PALM MIRAGE  •  DAY 1");
                Expect("day","Objective title","DAY QUOTA  $12 / $30");
                // ECON-3: what is left to sell, then this player's next step: with no stock, where it comes from; holding some, Sam is
                // the tripper who sees the buyers. Both draw whole.
                Expect("day","Objective detail","Sell $18 more before sundown. You have nothing to sell: buy from festivalgoers, and some are narcs.");
                Fits("day","Objective detail");
                string tripperId=sim.State.TripperId;sim.State.TripperId=mate.Id;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                yield return new WaitForSeconds(.6f);
                Expect("day, holding stock","Objective detail","Sell $18 more before sundown. Stick with Sam: they see the buyers. Walk up to one and press E to offer.");
                Fits("day, holding stock","Objective detail");
                sim.State.TripperId=tripperId;player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");
                if(!System.Text.RegularExpressions.Regex.IsMatch(Read("Time"),@"^[0-8]:[0-5]\d$"))failures.Add("day: Time reads \""+Read("Time")+"\", expected m:ss");
                sim.State.LevelSales=30;player.X=0;player.Z=-32;
                yield return new WaitForSeconds(.6f);
                Expect("day, quota met","Objective title","QUOTA MET — HEAD BACK TO CAMP");
                Expect("day, quota met at the way back to camp","Prompt","End the day: head back to camp");
                Fits("day, quota met","Objective detail");
                PressE();yield return null;
                if(!sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="Extract"&&i.Status=="Active"))failures.Add("day, quota met: E at the way back to camp starts no Extract ("+session.Message+")");
                session.Command("Cancel");
                // The sim takes Extract within 2.5 m of the gate; the HUD offers it no further out.
                player.Z=Festivals.CampGateZ+2.6f;
                yield return new WaitForSeconds(.6f);
                if(Read("Prompt")=="End the day: head back to camp")failures.Add("day, 2.6 m from the gate: the HUD offers an Extract the sim refuses");

                // Night 2: Sam died out in the field and their body lies beside you.
                sim.State.LevelIndex=3;sim.State.DurationSeconds=600;mate.Life="Spirit";player.X=0;player.Z=0;
                sim.State.Bodies.Add(new BodyState{PlayerId=mate.Id,X=1,Z=0});
                yield return new WaitForSeconds(.6f);
                Expect("night 2","Level banner","PALM MIRAGE  •  NIGHT 2");
                Expect("night 2","Roster","HOME  0 / 2\nYOU  •  AWAY\nSAM  •  BODY AWAY");
                Expect("night 2 beside a body","Prompt","Carry Sam's body");
                Fits("night 2","Roster");
                PressE();yield return new WaitForSeconds(.6f);
                if(player.CarryBodyId!=mate.Id)failures.Add("night 2 beside a body: E does not pick it up ("+session.Message+")");
                Expect("night 2 carrying a body","Prompt","Put down Sam's body");
                PressE();yield return new WaitForSeconds(.6f);
                if(player.CarryBodyId!="")failures.Add("night 2 carrying a body: E does not put it down ("+session.Message+")");

                // HUD-4: the friend is found but nobody escorts them; the longest escort line still fits its box at 1080p.
                sim.State.FriendFound=true;sim.State.FriendLeaderId="";player.X=0;player.Z=0;
                yield return new WaitForSeconds(.6f);
                Expect("night 2, friend without an escort","Objective title","ESCORT FRIEND BACK TO CAMP");
                Expect("night 2, friend without an escort","Objective detail","Friend needs an escort. Reach them and press E, then lead them to the way back to camp 32 m S.");
                Fits("night 2, friend without an escort","Objective detail");

                // Night 2 won: you lead the friend to the way back to camp, Sam's body lies home right beside you.
                sim.State.FriendFound=true;sim.State.FriendLeaderId=player.Id;sim.State.FriendPosition=new WorldPoint(Festivals.CampGateX,Festivals.CampGateZ);
                sim.State.Bodies[0].X=Festivals.CampGateX+.5f;sim.State.Bodies[0].Z=Festivals.CampGateZ;player.X=Festivals.CampGateX;player.Z=Festivals.CampGateZ;
                yield return new WaitForSeconds(.6f);
                Expect("night 2, everyone home, beside Sam's body","Prompt","Head back to camp with your friend");
                player.CarryBodyId=mate.Id;
                yield return new WaitForSeconds(.6f);
                Expect("night 2, everyone home, carrying Sam's body","Prompt","Head back to camp with your friend");
                PressE();yield return null;
                if(!sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="Extract"&&i.Status=="Active"))failures.Add("night 2, everyone home: E at the way back to camp starts no Extract ("+session.Message+")");
                session.Command("Cancel");player.CarryBodyId="";

                // The dead read the checklist too, though they cannot see the living: you died and your body is home, Sam is alive and out.
                sim.State.Bodies.Clear();sim.State.FriendFound=false;sim.State.FriendLeaderId="";
                mate.Life="Alive";mate.X=30;mate.Z=0;player.Life="Spirit";sim.State.Bodies.Add(new BodyState{PlayerId=player.Id,X=Festivals.CampGateX,Z=Festivals.CampGateZ});
                yield return new WaitForSeconds(.6f);
                Expect("night 2 as a spirit","Roster","HOME  ? / 2\nYOU  •  HOME\n1 LIVING  •  UNSEEN");
                Fits("night 2 as a spirit","Roster");
                // Alone on Night 2 (Sam left): the one crew row still shows.
                player.Life="Alive";sim.State.Bodies.Clear();player.X=0;player.Z=0;mate.Connected=false;
                yield return new WaitForSeconds(.6f);
                Expect("night 2 alone","Roster","HOME  0 / 1\nYOU  •  AWAY");
                Fits("night 2 alone","Roster");
                mate.Connected=true;

                // Results after a missed quota: the weekend restarts, and the host brings the crew back.
                sim.State.LevelIndex=0;sim.State.Result="Missed the quota";sim.State.Phase="Results";
                yield return new WaitForSeconds(.6f);
                Expect("results","Objective title","WEEKEND OVER  •  BACK TO DAY 1");
                Expect("results","Objective detail","Missed the quota. Palm Mirage restarts at Day 1 with fresh cash.");
                Expect("results","Prompt","ESC MENU  •  NEXT CAMP");
                sim.State.FestivalIndex=1;sim.State.LevelIndex=3;sim.State.Result="Success";
                yield return new WaitForSeconds(.6f);
                Expect("results, last festival cleared","Objective detail","Next up: an encore at Palm Mirage, harder. A new weekend starts with fresh cash.");
                Fits("results, last festival cleared","Objective detail");
                sim.State.LevelIndex=1;sim.State.Result="No living free teammate can complete a rescue";
                yield return new WaitForSeconds(.6f);
                Fits("results, crew wiped","Objective detail");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"HUD text a player reads:\n"+string.Join("\n",failures));
        }

        // ECON-3 (Matt, three times on 2026-10-01: "there is no button to actually sell to people"): beside a festivalgoer the prompt
        // and E are your next step to a sale. Hosts a real two-player round on Palm Mirage Day 1, with Sam tripping beside you as the
        // crew's day card tells you to stay, and E runs the HUD's primary action (FestivalHud.UpdateKeyboard).
        [UnityTest]public IEnumerator BesideAFestivalgoerEIsYourNextStepToASale()
        {
            var world=new GameObject("HUD sale world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD sale");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            string Prompt(){foreach(var text in hud.GetComponentsInChildren<Text>(true))if(text.name=="Prompt")return text.text;return "(no prompt)";}
            void Expect(string where,string expected){if(Prompt()!=expected)failures.Add(where+": the prompt reads \""+Prompt()+"\", expected \""+expected+"\"");}
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("sale_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                sim.State.TripperId=mate.Id;sim.State.Npcs.Clear();
                var buyer=new NpcState{Id="sale_buyer_2",Role="Buyer",CanTalk=true};sim.State.Npcs.Add(buyer);
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";
                // E runs the primary action; what it started, with whom, is cancelled again.
                IEnumerator PressE(string where,string kind)
                {
                    ((System.Action)primary.GetValue(view))?.Invoke();yield return null;
                    var started=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Status=="Active");
                    if(started?.Kind!=kind||started.TargetId!=buyer.Id)failures.Add(where+": E starts "+(started==null?"nothing":started.Kind+" with "+started.TargetId)+", not a "+kind+" with them ("+session.Message+")");
                    session.Command("Cancel");yield return null;
                }
                // You and the festivalgoer face each other, calm (each cancel above put them on edge), Sam at your side.
                IEnumerator Stand(){player.X=0;player.Z=0;buyer.X=0;buyer.Z=1.5f;buyer.Yaw=180;buyer.Suspicion=0;buyer.Mode="Blending";mate.X=1.2f;mate.Z=0;yield return new WaitForSeconds(.6f);}

                // By day with no stock, E buys from them (a seller this once: a buyer turns the ask down).
                buyer.Role="Regular";
                yield return Stand();
                Expect("by day, no stock",FestivalHudText.BuyAction(sim.State,player,buyer.Id)+"  •  F CHAT");
                yield return PressE("by day, no stock","Buy");
                // Holding stock, E offers it to the buyer, not to Sam beside you.
                buyer.Role="Buyer";player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
                yield return Stand();
                Expect("by day, holding stock, Sam in reach","Offer Tongue Stamps  •  F CHAT");
                yield return PressE("by day, holding stock, Sam in reach","Sale");
                // The tripper beside someone they have an unchecked vision about still checks first.
                sim.State.TripperId=player.Id;sim.State.Visions.Add(new VisionState{Id="sale_vision",Kind="Buyer",NpcId=buyer.Id});
                yield return Stand();
                Expect("the tripper beside a vision, holding stock",FestivalHudText.CheckDanceAction);
                yield return PressE("the tripper beside a vision, holding stock","ConfirmDance");
                sim.State.TripperId=mate.Id;sim.State.Visions.Clear();
                // K1: once someone has sold to you, E beside them buys again. A seller never buys, so E offering the unit back was a
                // sure refusal and +10 suspicion. The buy is talked through with the smooth reply every turn, after a perfect dance
                // (over by the next tick) if they ask for one first.
                buyer=new NpcState{Id="sale_seller_4",Role="Regular",CanTalk=true};sim.State.Npcs.Clear();sim.State.Npcs.Add(buyer);
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";
                yield return Stand();
                ((System.Action)primary.GetValue(view))?.Invoke();yield return new WaitForSeconds(.4f);
                var bought=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Status=="Active");
                Assert.That(bought?.Kind,Is.EqualTo("Buy"),"setup: E starts a buy from them ("+session.Message+"), after:\n"+string.Join("\n",failures));
                if(bought.TrustDance)
                {
                    foreach(var note in RhythmChart.For(bought).Notes)bought.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
                    bought.StartSeconds=sim.State.SimulationSeconds-bought.DurationSeconds-1;
                    yield return new WaitForSeconds(.4f);
                }
                while(bought.Status=="Active"&&bought.Turns.Count>0)
                {
                    var turn=bought.Turns[bought.Turns.Count-1];
                    Assert.That(sim.Execute(player.Id,new GameCommand{Id="sale_reply_"+bought.Turns.Count,Kind="Reply",Amount=System.Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=bought.Turns.Count-1}).Accepted,Is.True,"setup: a smooth reply");
                }
                yield return new WaitForSeconds(.4f);
                Assert.That(player.Inventory.Exists(i=>i.ItemId=="stock_lsd"),Is.True,"setup: the buy hands over a Tongue Stamp ("+session.Message+"), after:\n"+string.Join("\n",failures));
                yield return Stand();
                Expect("by day, beside who just sold to you",FestivalHudText.BuyAction(sim.State,player,buyer.Id)+"  •  F CHAT");
                yield return PressE("by day, beside who just sold to you","Buy");
                // At night E dances, stock or not.
                sim.State.LevelIndex=1;
                yield return Stand();
                Expect("night, holding stock","Dance with festivalgoer  •  F CHAT");
                yield return PressE("night, holding stock","Dance");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Beside a festivalgoer:\n"+string.Join("\n",failures));
        }

        // LOOP-5: nobody misses sundown or the restart. A day's last minute pops a warning once, on a card of its own that a later
        // reply never replaces and that waits out a dance hiding it; 15 s out it pops once more. At results a centred banner says
        // the weekend went back to Day 1, under the menus and catching no clicks, and NEXT CAMP brings the crew back to a camp that
        // says so. Hosts a real two-player Day 1 of Palm Mirage, which asks $30; labels are laid out on a 1920x1080 canvas.
        [UnityTest]public IEnumerator NobodyMissesSundownOrTheRestart()
        {
            var world=new GameObject("Sundown world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Sundown HUD");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            void Fits(string where,string label)
            {
                var text=Find(label);if(text==null){failures.Add(where+": "+label+" is hidden");return;}
                Canvas.ForceUpdateCanvases();
                int shown=text.cachedTextGenerator.characterCountVisible;
                if(shown<text.text.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" cuts \""+text.text.Replace("\n"," | ")+"\" after \""+text.text.Substring(0,shown).Replace("\n"," | ")+"\"");
            }
            const string Minute="1 MINUTE TO SUNDOWN: sell $30 more or the weekend restarts at Day 1";
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("sundown_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                sim.State.Npcs.Clear();double sundown=sim.State.DurationSeconds;

                sim.State.ElapsedSeconds=sundown-62;
                yield return new WaitForSeconds(.4f);
                Expect("61 s left","Sundown","(hidden)");

                // The last minute starts while the player spins poi, a long set (its lead-in pushed back): the dance hides the HUD, and
                // the warning waits longer than its time on screen for the dance to end.
                player.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
                session.Command("Poi");yield return null;
                var poi=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Kind=="Poi"&&i.Status=="Active");
                Assert.That(poi,Is.Not.Null,"setup: the player spins poi: "+session.Message);
                poi.StartSeconds+=20;
                sim.State.ElapsedSeconds=sundown-59.5;
                yield return new WaitForSeconds(FestivalHud.SundownNoticeSeconds+1);
                Expect("dancing through the last minute's start","Sundown","(hidden)");
                session.Command("Cancel");
                yield return new WaitForSeconds(.4f);
                Expect("the dance over","Sundown",Minute);
                Fits("the dance over","Sundown");
                if(!Read("Objective detail").StartsWith("Sell $30 more before sundown or the weekend restarts."))failures.Add("the last minute: Objective detail reads \""+Read("Objective detail")+"\"");
                // The objective card's longest last-minute line still draws whole: on Ember Playa, in friendship bracelets, holding stock
                // while a friend with the longest name (24 letters) trips.
                string tripper=sim.State.TripperId,mateName=mate.Name;int ordinal=player.Ordinal;
                sim.State.FestivalIndex=1;player.Ordinal=3;sim.State.TripperId=mate.Id;mate.Name="Bartholomew Fizzlewidget";player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                yield return new WaitForSeconds(.4f);
                Fits("the last minute's longest line","Objective detail");
                sim.State.FestivalIndex=0;player.Ordinal=ordinal;sim.State.TripperId=tripper;mate.Name=mateName;player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");

                // A reply to a later command lands in the session notice, beside the warning, not over it.
                session.SetMessage("Bought Tongue Stamps for $5");
                yield return new WaitForSeconds(.3f);
                Expect("after a reply","Sundown",Minute);
                Expect("after a reply","Notice","Bought Tongue Stamps for $5");
                // Once: gone after its seconds on screen, and it stays gone.
                yield return new WaitForSeconds(FestivalHud.SundownNoticeSeconds);
                Expect("after its seconds on screen","Sundown","(hidden)");
                session.SetMessage("Ready when you are");
                yield return new WaitForSeconds(.5f);
                Expect("a while later","Sundown","(hidden)");

                // 15 s out it pops once more.
                sim.State.ElapsedSeconds=sundown-14;
                yield return new WaitForSeconds(.4f);
                Expect("14 s left","Sundown","15 SECONDS: sell $30 more or back to Day 1");

                // Sundown with nothing sold: the weekend restarts.
                sim.State.ElapsedSeconds=sundown;
                for(deadline=Time.realtimeSinceStartup+10;session.State.Phase!="Results"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Results"),"setup: sundown ends the day: "+session.Message);
                yield return new WaitForSeconds(.4f);
                Expect("results","Sundown","(hidden)");
                Expect("results","Results title","MISSED THE QUOTA");
                Expect("results","Results detail","You sold $0 of $30\nPALM MIRAGE RESTARTS AT DAY 1  •  FRESH CASH");
                Fits("results","Results title");Fits("results","Results detail");
                Expect("results","Objective title","WEEKEND OVER  •  BACK TO DAY 1");
                // Under the menus, catching no clicks: the host opens the menu over it and clicks NEXT CAMP.
                var banner=hud.transform.Find("Festival HUD/Results banner");var pass=hud.transform.Find("Festival HUD/Festival pass");
                if(banner==null)failures.Add("results: there is no results banner");
                else
                {
                    foreach(var graphic in banner.GetComponentsInChildren<Graphic>(true))if(graphic.raycastTarget)failures.Add("results: the banner's "+graphic.name+" catches clicks");
                    if(banner.GetSiblingIndex()>pass.GetSiblingIndex())failures.Add("results: the banner draws over the menu");
                }
                session.MenuOpen=true;yield return null;
                pass.Find("Pass home/NEXT CAMP").GetComponent<Button>().onClick.Invoke();
                for(deadline=Time.realtimeSinceStartup+10;session.State.Phase!="CampReview"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("CampReview"),"setup: NEXT CAMP brings the crew back to camp: "+session.Message);
                yield return new WaitForSeconds(.4f);
                Expect("camp after a lost day","Level banner","BACK TO DAY 1  •  PALM MIRAGE");
                Expect("camp after a lost day","Results title","(hidden)");
                Fits("camp after a lost day","Review text");
                if(!Read("Review text").Contains("A GLORIOUS DISASTER  •  BACK TO DAY 1"))failures.Add("camp after a lost day: Review text reads \""+Read("Review text").Replace("\n"," | ")+"\"");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Sundown and the restart, as a player reads them:\n"+string.Join("\n",failures));
        }

        // ECON-3: Q uses what you hold, and on stock that is taking it yourself. The first Q leaves your stock and says how to take
        // it; a second within 3 s takes it. Hosts a real round; Q goes through the HUD's own input handling (FestivalHud.UpdateKeyboard)
        // on a virtual keyboard.
        [UnityTest]public IEnumerator QAsksBeforeYouTakeYourOwnStock()
        {
            UseAVirtualKeyboard("HUD stock keyboard");
            var world=new GameObject("HUD stock world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD stock");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            string Notice(){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Notice")return text.text;return "(hidden)";}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);
                sim.State.Npcs.Clear();
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.Effects.Clear();
                player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});player.EquippedItemId="stock_lsd";
                int Stock()=>player.Inventory.Find(i=>i.ItemId=="stock_lsd")?.Count??0;
                yield return new WaitForSeconds(.4f);
                yield return Tap(Key.Q);
                yield return new WaitForSeconds(.2f);
                if(Stock()!=2||player.Effects.Exists(e=>e.Id=="lsd"))failures.Add("one Q on your stock: you took it ("+Stock()+" left)");
                if(Notice()!=FestivalHud.TakeStockAgain)failures.Add("one Q on your stock: the notice reads \""+Notice()+"\"");
                yield return Tap(Key.Q);
                yield return new WaitForSeconds(.2f);
                if(Stock()!=1||!player.Effects.Exists(e=>e.Id=="lsd"))failures.Add("a second Q within 3 s: you didn't take one ("+Stock()+" left, "+session.Message+")");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Q on your stock:\n"+string.Join("\n",failures));
        }

        // ECON-3 (Matt, 2026-10-01: "there is no button to actually sell to people"; the offer sat behind Esc and then CREW + NEARBY):
        // beside a festivalgoer the prompt is what E does, and a strip under it lists everything else there, each on Esc. Esc opens the
        // menu on that list, so one click sells, unless a chat is under way: its replies keep that spot. TAB is still the map. Hosts a
        // real round at 1080p; the keys go through the HUD's own input handling on a virtual keyboard.
        [UnityTest]public IEnumerator EverythingYouCanDoBesideSomeoneIsOnScreenOrOneEscAway()
        {
            UseAVirtualKeyboard("HUD nearby keyboard");
            var world=new GameObject("HUD nearby world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD nearby");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            void Fits(string where,string label)
            {
                var text=Find(label);if(text==null){failures.Add(where+": "+label+" is hidden");return;}
                Canvas.ForceUpdateCanvases();
                int shown=text.cachedTextGenerator.characterCountVisible;
                if(shown<text.text.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" cuts \""+text.text.Replace("\n"," | ")+"\" after \""+text.text.Substring(0,shown).Replace("\n"," | ")+"\"");
            }
            bool Shows(string name,string parent=""){foreach(var t in hud.GetComponentsInChildren<Transform>(false))if(t.name==name&&(parent==""||t.parent.name==parent))return true;return false;}
            string Listed(){var listed=new List<string>();foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.name.StartsWith("Action:"))listed.Add(b.name.Substring(7));return string.Join(" | ",listed);}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);
                // Day 1, a Tongue Stamp in hand, a buyer in front of you on the open lawn (so lying down is listed last), nobody else about.
                sim.State.Npcs.Clear();sim.State.Visions.Clear();
                var buyer=new NpcState{Id="nearby_buyer_2",Role="Buyer",X=0,Z=1.5f,Yaw=180,CanTalk=true};sim.State.Npcs.Add(buyer);
                player.X=0;player.Z=0;player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");
                player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";
                yield return new WaitForSeconds(.6f);
                const string Offer="Offer Tongue Stamps  •  F CHAT";
                Expect("beside a buyer, holding stock","Prompt",Offer);
                if(!Shows("E key","Action prompt"))failures.Add("beside a buyer, holding stock: the prompt shows no E key");
                Expect("beside a buyer, holding stock","Nearby","ESC  Dance with festivalgoer\nESC  Talk with festivalgoer\nESC  "+FestivalHudText.BuyAction(sim.State,player,buyer.Id)+"\nESC  "+FestivalHudText.LieDownAction);
                Fits("beside a buyer, holding stock","Nearby");
                // In the last minute of a day the sundown card (LOOP-5) comes up beside it, never over it.
                RectTransform Card(string name){foreach(var r in hud.GetComponentsInChildren<RectTransform>(true))if(r.name==name)return r;return null;}
                var strip=Card("Nearby actions");var sundown=Card("Sundown warning");
                if(strip==null||sundown==null||strip.anchorMax.x>sundown.anchorMin.x&&strip.anchorMin.y<sundown.anchorMax.y)failures.Add("the strip overlaps the sundown card");
                // Esc opens the menu on the list, and a click there sells, closing the menu so the sale has your keys.
                yield return Tap(Key.Escape);yield return new WaitForSeconds(.3f);
                if(!session.MenuOpen)failures.Add("Esc: the menu doesn't open");
                Button offer=null;foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.name=="Action:"+Offer)offer=b;
                if(offer==null)failures.Add("Esc: the menu's list doesn't show \""+Offer+"\" (listed: \""+Listed()+"\")");
                else
                {
                    offer.onClick.Invoke();yield return null;
                    var sale=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Status=="Active");
                    if(sale?.Kind!="Sale"||sale.TargetId!=buyer.Id)failures.Add("Esc, then the offer: it starts "+(sale?.Kind??"nothing")+" ("+session.Message+")");
                    if(session.MenuOpen)failures.Add("Esc, then the offer: the menu stays open over the sale");
                    session.Command("Cancel");yield return null;yield return null;
                    yield return Tap(Key.Escape);
                    if(session.MenuOpen||hud.GetComponentInChildren<FestivalChatWindow>(true).Open)failures.Add("Esc after cancelling the sale: terminal chat or menu remains open");
                }
                // K1: a dance chosen there takes your arrow keys. By day it is the list's only way to the dance How to Play asks for;
                // left open, the menu dropped every press and the pass offered only CANCEL ACTION, which costs suspicion.
                session.MenuOpen=false;yield return null;
                yield return Tap(Key.Escape);yield return new WaitForSeconds(.3f);
                Button dance=null;foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.name=="Action:Dance with festivalgoer")dance=b;
                if(dance==null)failures.Add("Esc: the menu's list doesn't show the dance (listed: \""+Listed()+"\")");
                else
                {
                    dance.onClick.Invoke();yield return null;
                    var danced=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Status=="Active");
                    Assert.That(danced?.Kind,Is.EqualTo("Dance"),"setup: the dance clicked from the list starts ("+session.Message+"), after:\n"+string.Join("\n",failures));
                    var first=RhythmChart.For(danced).Notes[0];Key[] arrows={Key.LeftArrow,Key.DownArrow,Key.UpArrow,Key.RightArrow};
                    while(danced.Status=="Active"&&session.EstimatedSimulationSeconds-danced.StartSeconds<first.TimeSeconds-.02)yield return null;
                    yield return Tap(arrows[first.Direction]);
                    if(session.MenuOpen||danced.Inputs.Count!=1)failures.Add("Esc, then the dance: the menu is "+(session.MenuOpen?"still open":"shut")+" and "+danced.Inputs.Count+" of 1 press reached the host");
                    session.Command("Cancel");
                }
                session.MenuOpen=false;yield return null;
                // Esc opens the menu and Esc again shuts it.
                yield return Tap(Key.Escape);yield return new WaitForSeconds(.3f);
                if(!session.MenuOpen)failures.Add("Esc after the dance: the menu doesn't open");
                yield return Tap(Key.Escape);yield return new WaitForSeconds(.3f);
                if(session.MenuOpen)failures.Add("Esc again: the menu stays open");
                // Away from anyone, no strip; TAB still holds the map open.
                player.X=10;
                yield return new WaitForSeconds(.6f);
                Expect("away from anyone","Nearby","(hidden)");
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(Key.Tab));yield return null;yield return null;
                if(!Shows("Map"))failures.Add("holding TAB: no map");
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());yield return null;yield return null;
                // You trip and chat to check your vision: Escape finishes and dismisses it, then the next Escape opens nearby actions.
                player.X=0;buyer.X=0;buyer.Z=1.5f;buyer.Suspicion=0;buyer.Mode="Blending";
                sim.State.TripperId=player.Id;sim.State.Visions.Add(new VisionState{Id="nearby_vision",Kind="Buyer",NpcId=buyer.Id});
                session.Command("ConfirmChat",buyer.Id);
                var check=sim.State.Interactions.Find(i=>i.PlayerId==player.Id&&i.Status=="Active");
                Assert.That(check?.Kind,Is.EqualTo("ConfirmChat"),"setup: you chat to check your vision ("+session.Message+"), after:\n"+string.Join("\n",failures));
                yield return new WaitForSeconds(.4f);
                yield return Tap(Key.Escape);yield return new WaitForSeconds(.3f);
                var window=hud.GetComponentInChildren<FestivalChatWindow>(true);
                if(session.MenuOpen||window==null||window.Open||Shows("Actions"))failures.Add("Esc mid-chat: menu "+(session.MenuOpen?"open":"shut")+", chat window "+(window!=null&&window.Open?"open":"shut")+", action list "+(Shows("Actions")?"over it":"hidden"));
                if(check.Status!="Complete"||!sim.State.Visions.Find(v=>v.Id=="nearby_vision").Confirmed)failures.Add("Esc mid-check: the check did not complete and confirm the vision");
                yield return Tap(Key.Escape);yield return new WaitForSeconds(.3f);
                if(!session.MenuOpen||!Listed().Contains("Dance with festivalgoer"))failures.Add("Esc after the check: nearby menu did not open (listed: "+Listed()+")");
                session.MenuOpen=false;
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Beside someone:\n"+string.Join("\n",failures));
        }

        // HUD-4: a menu left open at camp closes as the wheels start, so the spin is seen. While the wheels spin and the festival
        // loads, the host refuses every action, so the HUD offers none and 1, Q and G send nothing. A key being rebound keeps the
        // menu open as the festival opens. Hosts a real two-player round; the keys are pressed on a virtual keyboard, so they go
        // through the HUD's own input handling (FestivalHud.UpdateKeyboard).
        [UnityTest]public IEnumerator TheSpinClosesTheMenuAndOffersNothingToPress()
        {
            UseAVirtualKeyboard("HUD spin keyboard");
            var world=new GameObject("HUD spin world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD spin");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            string Prompt(){foreach(var text in hud.GetComponentsInChildren<Text>(true))if(text.name=="Prompt")return text.text;return "(no prompt)";}
            // No prompt, nothing for E, and the gear keys send nothing for the host to refuse.
            IEnumerator NothingToPress(string where)
            {
                yield return new WaitForSeconds(.3f);
                if(Prompt()!="")failures.Add(where+": the prompt reads \""+Prompt()+"\"");
                if(primary.GetValue(view)!=null)failures.Add(where+": E still runs an action");
                foreach(var key in new[]{Key.Digit1,Key.Q,Key.G})
                {
                    yield return Tap(key);
                    if(session.Message=="Round is not interactive")failures.Add(where+": "+key+" sent a command the host refused");
                }
            }
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("spin_mate","Sam");
                // Gear in hand and Sam within reach: at the festival the HUD would offer to hand it over.
                player.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
                player.X=0;player.Z=19;mate.X=1.5f;mate.Z=19;mate.Ready=true;
                yield return new WaitForSeconds(.3f);
                Assert.That(Prompt(),Is.Not.Empty,"setup: at the trailhead the HUD offers something to press");
                session.MenuOpen=true;session.Command("Ready");
                yield return SkipCountdown(session);
                yield return null;yield return null;
                if(session.MenuOpen)failures.Add("spinning: the menu left open at camp still covers the wheels");
                session.MenuOpen=false;yield return null;
                var wheels=hud.transform.Find("Festival spinner");
                if(wheels==null||!wheels.gameObject.activeInHierarchy)failures.Add("spinning: the wheels are not on screen");
                yield return NothingToPress("spinning");

                // Sam has no client of their own, so the festival stays loading until they are marked ready below.
                yield return SkipSpin(session);
                deadline=Time.realtimeSinceStartup+40;
                while(!(session.State.Phase=="Loading"&&player.MapReady)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Loading"),"setup: the spin ends in loading, and this client's map is ready: "+session.Message);
                yield return NothingToPress("loading");

                session.MenuOpen=true;session.Controls.Rebind(session.Controls.Use,0,null);
                Assert.That(session.Controls.Rebinding,"setup: a key is being rebound");
                sim.Execute(mate.Id,new GameCommand{Id="spin_mate_loaded",Kind="MapReady"});
                deadline=Time.realtimeSinceStartup+10;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"setup: the festival opens: "+session.Message);
                yield return null;yield return null;
                if(!session.MenuOpen)failures.Add("the festival opens while a key is being rebound: the menu closed under the player");
                ((InputActionRebindingExtensions.RebindingOperation)typeof(FestivalInput).GetField("rebind",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session.Controls)).Cancel();
                Assert.That(session.Controls.Rebinding,Is.False,"setup: the rebind is cancelled");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"The spin a player sees:\n"+string.Join("\n",failures));
        }

        // HUD-7 (Matt, 2026-10-01: "if i had never played the game, it would be very unclear what i am supposed to do and what the
        // objective is"): HOW TO PLAY sits on the title, between PLAY and SETTINGS (MENU-1: the title replaced the connection card
        // with its CREATE GAME and JOIN GAME), and on the Escape menu's home. It opens FestivalHudText.HowToPlay's six blocks, as this
        // machine's keys are bound, each drawn whole at 1080p; BACK or Esc puts it away. Hosts a real round; Esc goes through the
        // HUD's own input handling on a virtual keyboard.
        [UnityTest]public IEnumerator HowToPlayOpensFromTheMainMenuAndTheEscapeMenu()
        {
            UseAVirtualKeyboard("HUD how to play keyboard");
            var world=new GameObject("HUD how to play world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD how to play");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
            bool Shows(string name){foreach(var t in hud.GetComponentsInChildren<Transform>(false))if(t.name==name)return true;return false;}
            Button Labelled(string label,string on){foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.name==label&&b.transform.parent.name==on)return b;return null;}
            // Every block's title and text on screen, as How to Play reads with the keys bound now, each drawn whole.
            void Reads(string where)
            {
                if(!Shows("How to play")){failures.Add(where+": How to Play is not up");return;}
                Canvas.ForceUpdateCanvases();
                foreach(var (title,text) in FestivalHudText.HowToPlay(session.Controls))
                {
                    Text heading=null,body=null;
                    foreach(var label in hud.GetComponentsInChildren<Text>(false)){if(label.text==title)heading=label;if(label.text==text)body=label;}
                    if(heading==null||body==null){failures.Add(where+": the "+title+" block has no "+(heading==null?"title":"text"));continue;}
                    foreach(var label in new[]{heading,body})
                    {
                        int shown=label.cachedTextGenerator.characterCountVisible;
                        if(shown<label.text.Length)failures.Add(where+": "+title+"'s "+label.name+" box "+label.rectTransform.rect.size+" cuts \""+label.text.Replace("\n"," | ")+"\" after \""+label.text.Substring(0,shown).Replace("\n"," | ")+"\"");
                    }
                }
            }
            try
            {
                // The title, before joining anyone.
                var open=Labelled("HOW TO PLAY","Title menu");
                if(open==null||Labelled("PLAY","Title menu")==null||Labelled("SETTINGS","Title menu")==null)failures.Add("main menu: no HOW TO PLAY between PLAY and SETTINGS on the title");
                else
                {
                    open.onClick.Invoke();yield return null;
                    Reads("main menu");
                    var back=Labelled("BACK","How to play card");
                    if(back==null)failures.Add("main menu: How to Play has no BACK");else{back.onClick.Invoke();yield return null;}
                    if(Shows("How to play"))failures.Add("main menu: BACK leaves How to Play up");
                    open.onClick.Invoke();yield return null;
                    yield return Tap(Key.Escape);
                    if(Shows("How to play"))failures.Add("main menu: Esc leaves How to Play up");
                }
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                // At camp, Esc opens the pass; HOW TO PLAY on its home opens the panel over it, and Esc puts both away.
                yield return Tap(Key.Escape);
                var fromPass=Labelled("HOW TO PLAY","Pass home");
                if(!session.MenuOpen||fromPass==null)failures.Add("Esc menu: "+(session.MenuOpen?"no HOW TO PLAY on its home":"Esc opens no menu"));
                else
                {
                    fromPass.onClick.Invoke();yield return null;
                    Reads("Esc menu");
                    yield return Tap(Key.Escape);
                    if(session.MenuOpen||Shows("How to play"))failures.Add("Esc menu, How to Play up: Esc leaves "+(session.MenuOpen?"the menu":"How to Play")+" open");
                    yield return Tap(Key.Escape);
                    if(!session.MenuOpen||Shows("How to play"))failures.Add("Esc menu opened again: "+(session.MenuOpen?"How to Play is still up over it":"no menu"));
                    session.MenuOpen=false;
                }
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"How to Play:\n"+string.Join("\n",failures));
        }

        // MENU-1 (Matt, 2026-10-01: "we need a real main menu screen, and actual game settings ... and we need to tell players what
        // the goal of the game actually is"): unconnected, the HUD is the title, each line and each field's text drawn whole at
        // 1080p. SETTINGS opens the settings before you join anyone: the rows read this machine's values, a change shows at once,
        // and its way out says BACK. Esc puts the settings away, steps out of PLAY's pages one at a time, and on the title itself
        // does nothing. PLAY fills in the name and the last host this machine saved; HOST A GAME hosts under the name typed, on the
        // port under ADVANCED, as CREATE GAME did, and saves the name. LEAVE SESSION comes back to the title, and so does a game
        // that drops with the settings left up, saying why. Esc goes through the HUD's own input handling on a virtual keyboard.
        // This machine's profile is put back as it was.
        [UnityTest]public IEnumerator TheGameOpensOnTheTitleAndLeavingComesBackToIt()
        {
            UseAVirtualKeyboard("HUD title keyboard");
            var world=new GameObject("HUD title world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD title");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            string saved=JsonUtility.ToJson(session.Profile.Data);
            // What PLAY fills in, as this machine saved it; the HUD reads it as it starts.
            session.Profile.Data.PlayerName="Saved Name";session.Profile.Data.LastHost="10.0.0.7";session.Profile.Data.MusicVolume=.35f;
            yield return null;
            var failures=new List<string>();
            var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
            bool Shows(string name){foreach(var t in hud.GetComponentsInChildren<Transform>(false))if(t.name==name)return true;return false;}
            T Named<T>(string name,string on) where T:Component{foreach(var c in hud.GetComponentsInChildren<T>(false))if(c.name==name&&c.transform.parent.name==on)return c;return null;}
            string Buttons(string on){var listed=new List<string>();foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.transform.parent.name==on)listed.Add(b.name);return string.Join(" | ",listed);}
            Text Line(string text){foreach(var t in hud.GetComponentsInChildren<Text>(false))if(t.text==text)return t;return null;}
            string Value(string row){foreach(var t in hud.GetComponentsInChildren<Text>(false))if(t.name==row+" value")return t.text;return "(hidden)";}
            void Whole(string where,Text label)
            {
                Canvas.ForceUpdateCanvases();int shown=Mathf.Max(0,label.cachedTextGenerator.characterCountVisible);
                if(shown<label.text.Length)failures.Add(where+": "+label.transform.parent.name+"'s "+label.name+" box "+label.rectTransform.rect.size+" cuts \""+label.text+"\" after \""+label.text.Substring(0,shown)+"\"");
            }
            // Every button the title shows, its label drawn whole.
            void Labels(string where){foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.transform.IsChildOf(hud.transform.Find("Festival HUD/Title screen")))Whole(where,b.GetComponentInChildren<Text>());}
            const string Home="PLAY | HOW TO PLAY | SETTINGS | QUIT";
            try
            {
                // The title, before anyone connects: the name and the tagline, and the four buttons, all drawn whole.
                foreach(var line in new[]{FestivalTitleScreen.GameName,FestivalTitleScreen.Tagline})
                {
                    var label=Line(line);
                    if(label==null)failures.Add("title: \""+line+"\" is not on screen");else Whole("title",label);
                }
                if(Buttons("Title menu")!=Home)failures.Add("title: the buttons are \""+Buttons("Title menu")+"\", expected \""+Home+"\"");
                Labels("title");
                yield return Tap(Key.Escape);
                if(Buttons("Title menu")!=Home||Shows("Play"))failures.Add("title: Esc on the title moves off it");

                // SETTINGS, before connecting.
                var settings=Named<Button>("SETTINGS","Title menu");
                if(settings==null)failures.Add("title: no SETTINGS");
                else
                {
                    settings.onClick.Invoke();yield return null;
                    if(!Shows("Pass settings"))failures.Add("settings: SETTINGS opens no settings while unconnected");
                    else
                    {
                        if(Value("MUSIC VOLUME")!="35%")failures.Add("settings: MUSIC VOLUME reads \""+Value("MUSIC VOLUME")+"\", expected this machine's 35%");
                        // The first + is the music's.
                        Named<Button>("+","Pass settings").onClick.Invoke();yield return null;
                        if(Value("MUSIC VOLUME")!="40%"||Mathf.Abs(session.Profile.Data.MusicVolume-.4f)>1e-4f)failures.Add("settings: after + MUSIC VOLUME reads \""+Value("MUSIC VOLUME")+"\" at a volume of "+session.Profile.Data.MusicVolume+", expected 40%");
                        var back=Named<Button>("BACK TO PASS","Pass settings");
                        string backLabel=back==null?"(none)":back.GetComponentInChildren<Text>().text;
                        if(backLabel!="BACK")failures.Add("settings: its way out reads \""+backLabel+"\" before connecting, expected \"BACK\"");
                        if(Shows("Title menu"))failures.Add("settings: the title's buttons stay up under the settings");
                    }
                    yield return Tap(Key.Escape);
                    if(Shows("Pass settings")||Buttons("Title menu")!=Home)failures.Add("settings: Esc doesn't put the settings away, back to the title");
                }

                // PLAY fills in the saved name, JOIN A FRIEND the saved host; Esc steps back a page at a time.
                var play=Named<Button>("PLAY","Title menu");
                Assert.That(play,Is.Not.Null,"the title has PLAY, after:\n"+string.Join("\n",failures));
                play.onClick.Invoke();yield return null;
                var name=Named<InputField>("YOUR NAME","Play");
                if(name==null||name.text!="Saved Name")failures.Add("PLAY: YOUR NAME reads \""+(name==null?"(none)":name.text)+"\", expected the saved \"Saved Name\"");
                else Whole("PLAY",name.textComponent);
                Named<Button>("ADVANCED","Play choices").onClick.Invoke();yield return null;
                var hostPort=Named<InputField>("PORT","Advanced");
                if(hostPort==null)failures.Add("PLAY: ADVANCED shows no port");else Whole("PLAY, ADVANCED",hostPort.textComponent);
                Labels("PLAY, ADVANCED");
                Named<Button>("ADVANCED","Play choices").onClick.Invoke();
                Named<Button>("JOIN A FRIEND","Play choices").onClick.Invoke();yield return null;
                var address=Named<InputField>("HOST ADDRESS","Join a friend");
                if(address==null||address.text!="10.0.0.7")failures.Add("JOIN A FRIEND: HOST ADDRESS reads \""+(address==null?"(none)":address.text)+"\", expected the saved \"10.0.0.7\"");
                else Whole("JOIN A FRIEND",address.textComponent);
                var port=Named<InputField>("PORT","Join a friend");
                if(port==null)failures.Add("JOIN A FRIEND: no port");else Whole("JOIN A FRIEND",port.textComponent);
                Labels("JOIN A FRIEND");
                yield return Tap(Key.Escape);
                if(Shows("Join a friend")||!Shows("Play choices"))failures.Add("JOIN A FRIEND: Esc doesn't step back to host or join");
                yield return Tap(Key.Escape);
                if(Shows("Play")||Buttons("Title menu")!=Home)failures.Add("PLAY: Esc doesn't step back to the title");

                // HOST A GAME hosts under the name typed, on the port under ADVANCED, and saves the name.
                play.onClick.Invoke();yield return null;
                Named<InputField>("YOUR NAME","Play").text="Title Tester";
                Named<Button>("ADVANCED","Play choices").onClick.Invoke();yield return null;
                Named<InputField>("PORT","Advanced").text=FreePort().ToString();
                Named<Button>("HOST A GAME","Play choices").onClick.Invoke();
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"HOST A GAME hosts: "+session.Message+" "+failures.Count+" failures:\n"+string.Join("\n",failures));
                yield return null;
                if(session.LocalPlayer.Name!="Title Tester")failures.Add("host: playing as \""+session.LocalPlayer.Name+"\", not the name typed");
                if(session.State.Phase!="Shopping"||session.MenuOpen)failures.Add("host: in "+session.State.Phase+(session.MenuOpen?" with the menu open":"")+", not at camp, playing");
                if(Shows("Title screen"))failures.Add("host: the title stays up in the game");
                if(session.Profile.Data.PlayerName!="Title Tester")failures.Add("host: the profile saves \""+session.Profile.Data.PlayerName+"\" as your name");

                // LEAVE SESSION, from the Escape menu, comes back to the title itself.
                yield return Tap(Key.Escape);
                var leave=Named<Button>("LEAVE SESSION","Pass home");
                if(!session.MenuOpen||leave==null)failures.Add("in game: Esc opens no LEAVE SESSION");
                else
                {
                    leave.onClick.Invoke();yield return null;
                    if(session.Connected||Buttons("Title menu")!=Home||Shows("Play"))failures.Add("LEAVE SESSION: "+(session.Connected?"still connected":"the buttons are \""+Buttons("Title menu")+"\"")+", not the title");
                }

                // The game drops (the host stops answering: FestivalSession.Fail) with the settings left up under a pass Esc closed.
                // The title comes back on the page you hosted from, its status line saying why, not the settings.
                play.onClick.Invoke();yield return null;
                Named<InputField>("PORT","Advanced").text=FreePort().ToString();
                Named<Button>("HOST A GAME","Play choices").onClick.Invoke();
                deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"HOST A GAME hosts again: "+session.Message);
                yield return Tap(Key.Escape);
                Named<Button>("SETTINGS","Pass home").onClick.Invoke();yield return null;
                yield return Tap(Key.Escape);
                if(session.MenuOpen)failures.Add("setup: Esc doesn't close the pass over its settings");
                const string dropped="Host stopped responding. No host migration is available in this prototype.";
                typeof(FestivalSession).GetMethod("Fail",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new object[]{dropped});
                yield return null;
                var status=Named<Text>("Status","Play");
                if(Shows("Pass settings")||status==null||status.text!=dropped)failures.Add("dropped: "+(Shows("Pass settings")?"the settings come up":"the status line reads \""+(status==null?"(hidden)":status.text)+"\"")+", not the title saying why");
            }
            finally
            {
                session.Leave();
                JsonUtility.FromJsonOverwrite(saved,session.Profile.Data);session.Profile.Save();
                foreach(var leftover in new[]{"First-person camera","Authoritative actor presentation"}){var found=GameObject.Find(leftover);if(found!=null)Object.Destroy(found);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"The title:\n"+string.Join("\n",failures));
        }

        // MENU-1: the native smokes' two ways in, through the title's buttons by the paths DevelopmentSmoke presses. A JOIN with
        // port 0 says so and joins nobody. HOST A GAME hosts on the port under ADVANCED, and a friend's JOIN A FRIEND joins that
        // host by its address and port, under the name typed, and saves the address for next time. Two HUDs in one scene, each
        // with its own session; this machine's profile is put back as it was.
        [UnityTest]public IEnumerator JoinAFriendJoinsTheGameHostAGameOpens()
        {
            var world=new GameObject("HUD join world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("HUD join host");var host=hostObject.AddComponent<FestivalSession>();hostObject.AddComponent<FestivalHud>();
            var friendObject=new GameObject("HUD join friend");var friend=friendObject.AddComponent<FestivalSession>();friendObject.AddComponent<FestivalHud>();
            string saved=JsonUtility.ToJson(friend.Profile.Data);
            yield return null;
            var failures=new List<string>();
            T At<T>(GameObject hud,string path) where T:Component{var found=hud.transform.Find("Festival HUD/Title screen/"+path);return found==null?null:found.GetComponent<T>();}
            void Press(GameObject hud,string path){var button=At<Button>(hud,path);Assert.That(button,Is.Not.Null,path+" is a button");button.onClick.Invoke();}
            try
            {
                ushort port=FreePort();
                Press(hostObject,"Title menu/PLAY");
                Press(hostObject,"Play/Play choices/ADVANCED");
                At<InputField>(hostObject,"Play/Play choices/Advanced/PORT").text=port.ToString();
                Press(hostObject,"Play/Play choices/HOST A GAME");
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,"HOST A GAME hosts on port "+port+": "+host.Message);

                Press(friendObject,"Title menu/PLAY");
                At<InputField>(friendObject,"Play/YOUR NAME").text="Sam";
                Press(friendObject,"Play/Play choices/JOIN A FRIEND");
                At<InputField>(friendObject,"Play/Join a friend/HOST ADDRESS").text="127.0.0.1";
                At<InputField>(friendObject,"Play/Join a friend/PORT").text="0";
                Press(friendObject,"Play/Join a friend/JOIN");yield return null;
                string status=At<Text>(friendObject,"Play/Status")?.text;
                if(status!="Port must be 1–65535."||friend.Connecting)failures.Add("JOIN on port 0: "+(friend.Connecting?"connects":"the status reads \""+status+"\""));
                At<InputField>(friendObject,"Play/Join a friend/PORT").text=port.ToString();
                Press(friendObject,"Play/Join a friend/JOIN");
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,"JOIN A FRIEND joins the host on port "+port+": "+friend.Message);
                if(friend.LocalPlayer.Name!="Sam")failures.Add("the friend plays as \""+friend.LocalPlayer.Name+"\", not the name typed");
                if(Simulation(host).State.Players.Count!=2)failures.Add("the host has "+Simulation(host).State.Players.Count+" players, not the two");
                if(friend.Profile.Data.LastHost!="127.0.0.1")failures.Add("the friend's profile saves \""+friend.Profile.Data.LastHost+"\" as the last host");
            }
            finally
            {
                friend.Leave();host.Leave();
                JsonUtility.FromJsonOverwrite(saved,friend.Profile.Data);friend.Profile.Save();
                Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"The title's ways in:\n"+string.Join("\n",failures));
        }

        // MENU-1: the title is the game from its first frame. All the way round a slow drift (FestivalTitleScreen.Shot) the camera
        // looks over the campsite from inside it, clear of anything solid and of every tree, tent and prop by 3 m, the camp's
        // middle right of the title's column; the live camera follows it, and holds still with reduced motion. The camp plays
        // ORGANIC HOUSE (MUSIC-2's decision A) until a game starts, then the camp's own first track, and ORGANIC HOUSE again once
        // you leave. Leaving in the middle of a night level brings back the campsite in its own light, framed the same way. This
        // machine's profile is put back as it was.
        [UnityTest]public IEnumerator TheTitleDriftsOverTheCampsiteToItsOwnMusic()
        {
            var world=new GameObject("HUD title camp world");var built=world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD title camp");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            string saved=JsonUtility.ToJson(session.Profile.Data);
            var failures=new List<string>();
            var camp=hud.GetComponent<FestivalCampAudio>();
            string Playing()=>camp.Music==null?"nothing":camp.Music.clip==null?"no clip":camp.Music.clip.name+(camp.Music.isPlaying?"":" (stopped)");
            var view=session.ViewCamera;
            Renderer[] scenery=null;
            // Where a pose leaves the camera, and what's wrong with it as a view of the camp: from inside the camp, over head
            // height, clear of anything solid and 3 m from anything drawn, with the middle of the gathering mat on screen right of
            // the title's column.
            string Framing(Vector3 at,Quaternion turn)
            {
                view.transform.SetPositionAndRotation(at,turn);
                var middle=view.WorldToViewportPoint(new Vector3(0,1,0));
                if(Mathf.Abs(at.x)>CampFeatures.CampHalfWidth-1||Mathf.Abs(at.z)>CampFeatures.CampHalfDepth-1||at.y<3)return "the camera at "+at+" is not over the campsite";
                if(Physics.CheckSphere(at,1))return "the camera at "+at+" is within a metre of something solid";
                // The ground and the far woodland rise reach round the whole camp: only what stands in it counts.
                foreach(var drawn in scenery)if(drawn!=null&&drawn.bounds.size.x<2*CampFeatures.CampHalfWidth&&drawn.bounds.SqrDistance(at)<9)return "the camera at "+at+" is within 3 m of "+drawn.name;
                if(middle.z<=0||middle.x<.55f||middle.x>.9f||middle.y<.2f||middle.y>.8f)return "from "+at+" the camp's middle shows at "+middle+", not right of the title";
                return "";
            }
            try
            {
                session.Profile.Data.ReducedMotion=false;
                yield return new WaitForSeconds(.3f);
                if(!built.IsCampVisible)failures.Add("the campsite isn't the scene behind the title");
                scenery=built.GetComponentsInChildren<Renderer>(false);var daySky=RenderSettings.ambientSkyColor;
                // A second at a time round the whole drift: framed as above, wide, and slow.
                FestivalTitleScreen.Shot(0,false,out var first,out _);
                float widest=0,fastest=0;var last=first;
                for(float t=0;t<=FestivalTitleScreen.DriftSeconds;t++)
                {
                    FestivalTitleScreen.Shot(t,false,out var at,out var turn);
                    string wrong=Framing(at,turn);if(wrong!=""&&failures.Count<4)failures.Add(t.ToString("0")+" s into the drift: "+wrong);
                    widest=Mathf.Max(widest,Vector3.Distance(first,at));fastest=Mathf.Max(fastest,Vector3.Distance(last,at));last=at;
                }
                if(widest<2||fastest>1)failures.Add("the drift spans "+widest.ToString("0.0")+" m at up to "+fastest.ToString("0.00")+" m a second, not a slow drift");
                FestivalTitleScreen.Shot(0,true,out var restAt,out var restTurn);
                string still=Framing(restAt,restTurn);if(still!="")failures.Add("held still: "+still);
                // The live camera follows the drift, now and a second later, and holds still with reduced motion. It starts from
                // somewhere else, so only the title can have put it back.
                view.transform.SetPositionAndRotation(Vector3.up*100,Quaternion.identity);view.fieldOfView=75;
                for(int look=0;look<2;look++)
                {
                    yield return look==0?null:new WaitForSeconds(1);
                    FestivalTitleScreen.Shot(Time.unscaledTime,false,out var now,out _);
                    if(Vector3.Distance(view.transform.position,now)>.05f)failures.Add("the live camera is at "+view.transform.position+", not on the drift at "+now);
                }
                if(Mathf.Abs(view.fieldOfView-FestivalTitleScreen.Fov)>.01f)failures.Add("the live camera's view is "+view.fieldOfView+"°, not the title's "+FestivalTitleScreen.Fov+"°");
                session.Profile.Data.ReducedMotion=true;yield return null;
                var held=view.transform.position;yield return new WaitForSeconds(.5f);
                if(view.transform.position!=held||Vector3.Distance(held,restAt)>.001f)failures.Add("with reduced motion the camera is at "+view.transform.position+", not held at "+restAt);

                if(Playing()!="organic-house-loop")failures.Add("the title plays "+Playing()+", not ORGANIC HOUSE");
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                yield return new WaitForSeconds(.3f);
                if(Playing()!="tech-house-loop")failures.Add("at camp in a game the camp plays "+Playing()+", not its own first track, TECH HOUSE");
                session.Leave();
                yield return new WaitForSeconds(.3f);
                if(Playing()!="organic-house-loop")failures.Add("back on the title the camp plays "+Playing()+", not ORGANIC HOUSE");

                // LEAVE SESSION in the middle of Night 1, by the pass's own button: the title is back over the campsite in the light
                // it opened in, not the festival and its night, the live camera framed as above.
                session.Host("Tester",HostPort);
                deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host again: "+session.Message);
                Simulation(session).State.LevelIndex=1;
                yield return StartLevel(session);
                if(built.IsCampVisible||RenderSettings.ambientSkyColor==daySky)failures.Add("setup: Night 1 shows "+(built.IsCampVisible?"the campsite":"the day's light"));
                session.MenuOpen=true;yield return null;
                hud.transform.Find("Festival HUD/Festival pass/Pass home/LEAVE SESSION").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.3f);
                if(!built.IsCampVisible)failures.Add("after leaving Night 1 the title shows the festival, not the campsite");
                if(RenderSettings.ambientSkyColor!=daySky)failures.Add("after leaving Night 1 the title's sky light is "+RenderSettings.ambientSkyColor+", not the campsite's "+daySky);
                scenery=built.GetComponentsInChildren<Renderer>(false);
                string left=Framing(view.transform.position,view.transform.rotation);if(left!="")failures.Add("after leaving Night 1: "+left);
            }
            finally
            {
                session.Leave();
                JsonUtility.FromJsonOverwrite(saved,session.Profile.Data);session.Profile.Save();
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"The title's campsite and music:\n"+string.Join("\n",failures));
        }

        // HUD-7: as each level opens, a card under the aim dot says what it asks, your job and how the crew loses
        // (FestivalHudText.Briefing; the tripper's "Trust, but verify." is folded into their job), each line drawn whole at 1080p.
        // It goes after BriefingSeconds of level clock, or sooner at any key that doesn't move you, for the rest of that level;
        // walking and running keep it up. The next level opens on its own. Hosts a real round of two through StartLevel; the keys
        // go through the HUD on a virtual keyboard.
        [UnityTest]public IEnumerator EachLevelOpensOnABriefingUntilTenSecondsOrAKey()
        {
            UseAVirtualKeyboard("HUD briefing keyboard");
            var world=new GameObject("HUD briefing world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD briefing");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            // The card reads the briefing this player's view holds now, a label a line, each drawn whole.
            void Briefs(string where)
            {
                var lines=FestivalHudText.Briefing(session.State,session.LocalPlayerId).Split('\n');
                Assert.That(lines.Length,Is.EqualTo(3),"setup, "+where+": a briefing to show, after:\n"+string.Join("\n",failures));
                Expect(where,"Briefing goal",lines[0]);Expect(where,"Briefing job",lines[1]);Expect(where,"Briefing stakes",lines[2]);
                Canvas.ForceUpdateCanvases();
                foreach(var name in new[]{"Briefing goal","Briefing job","Briefing stakes"})
                {
                    var text=Find(name);if(text==null)continue;
                    int shown=text.cachedTextGenerator.characterCountVisible;
                    if(shown<text.text.Length)failures.Add(where+": "+name+" box "+text.rectTransform.rect.size+" cuts \""+text.text+"\" after \""+text.text.Substring(0,shown)+"\"");
                }
            }
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("briefing_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                // Day 1 opens, and you trip: your job carries the one hint.
                sim.State.TripperId=player.Id;sim.State.ElapsedSeconds=1;
                yield return new WaitForSeconds(.4f);
                Briefs("day 1, you trip");
                if(!Read("Briefing job").Contains("Trust, but verify"))failures.Add("day 1, you trip: your job has no \"Trust, but verify\"");
                // Sam trips: your job is to stick with them.
                sim.State.TripperId=mate.Id;
                yield return new WaitForSeconds(.4f);
                Briefs("day 1, Sam trips");
                // Ten seconds of level clock in, it is gone.
                sim.State.ElapsedSeconds=FestivalHudText.BriefingSeconds;
                yield return new WaitForSeconds(.4f);
                Expect("ten seconds in","Briefing goal","(hidden)");
                // Back in the window: walking and running keep it up, and any other key puts it away for the rest of the level.
                sim.State.ElapsedSeconds=2;
                yield return new WaitForSeconds(.4f);
                Briefs("two seconds in");
                yield return Tap(Key.W);yield return Tap(Key.LeftShift);
                if(Find("Briefing goal")==null)failures.Add("walking or running put the briefing away");
                yield return Tap(Key.Space);
                Expect("Space pressed","Briefing goal","(hidden)");
                sim.State.ElapsedSeconds=3;
                yield return new WaitForSeconds(.4f);
                Expect("Space pressed, still in the window","Briefing goal","(hidden)");
                // The next level, Night 1, opens on its own briefing.
                sim.State.RoundId="hud_briefing_night";sim.State.LevelIndex=1;sim.State.ElapsedSeconds=1;
                yield return new WaitForSeconds(.4f);
                Briefs("night 1");
                if(!Read("Briefing goal").StartsWith("NIGHT 1"))failures.Add("night 1: the goal reads \""+Read("Briefing goal")+"\"");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"The level briefing:\n"+string.Join("\n",failures));
        }

        // HUD-7: the first time a moment comes on this machine (FestivalHudText.Tip), a two-line tip shows down the left rail for
        // TipSeconds, drawn whole at 1080p, and the profile remembers it, so it never comes again; SHOW TIPS AGAIN in the settings
        // brings it back. Hosts a real round of two through StartLevel, on the run's own profile (TestProfiles), as every hosted test does.
        [UnityTest]public IEnumerator AFirstTimeTipShowsOnceUntilShowTipsAgain()
        {
            var world=new GameObject("HUD tip world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD tip");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            Button Labelled(string label,string on){foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.name==label&&b.transform.parent.name==on)return b;return null;}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("tip_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);
                // A first time on this machine: no tip seen yet.
                var seen=session.Profile.Data.SeenTips;seen.Clear();
                sim.State.Npcs.Clear();sim.State.TripperId=mate.Id;sim.State.ElapsedSeconds=FestivalHudText.BriefingSeconds;
                yield return new WaitForSeconds(.4f);
                const string Friend="Only Sam can see the visions.\nStick close and ask what they see.";
                Expect("Sam trips","First-time tip",Friend);
                var tip=Find("First-time tip");
                if(tip!=null)
                {
                    Canvas.ForceUpdateCanvases();int shown=tip.cachedTextGenerator.characterCountVisible;
                    if(shown<tip.text.Length)failures.Add("Sam trips: the tip's box "+tip.rectTransform.rect.size+" cuts it after \""+tip.text.Substring(0,shown).Replace("\n"," | ")+"\"");
                }
                if(!seen.Contains("friend"))failures.Add("Sam trips: the profile does not remember the tip it showed (seen: "+string.Join(", ",seen)+")");
                // It goes after TipSeconds, and does not come back.
                yield return new WaitForSeconds((float)FestivalHudText.TipSeconds+.3f);
                Expect("Sam trips, "+FestivalHudText.TipSeconds+" s later","First-time tip","(hidden)");
                // SHOW TIPS AGAIN in the settings brings it back.
                session.MenuOpen=true;yield return null;
                Labelled("SETTINGS","Pass home")?.onClick.Invoke();yield return null;
                var again=Labelled("SHOW TIPS AGAIN","Pass settings");
                if(again==null)failures.Add("settings: no SHOW TIPS AGAIN");else{again.onClick.Invoke();yield return null;}
                if(seen.Count>0)failures.Add("SHOW TIPS AGAIN: the profile still holds "+string.Join(", ",seen));
                Labelled("BACK TO PASS","Pass settings")?.onClick.Invoke();session.MenuOpen=false;
                yield return new WaitForSeconds(.4f);
                Expect("after SHOW TIPS AGAIN","First-time tip",Friend);
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"First-time tips:\n"+string.Join("\n",failures));
        }

        // HUD-8 (Matt, 2026-10-01: "pressing 1 2 3 did not switch between my items. pressing them should or using the scroll
        // wheel"): at camp and at the festival, 1, 2 and 3 each move what you hold, the gear bar's ● and the first-person hand to
        // that slot, and its number and item flash over the gear bar for a second; the wheel steps round the filled slots. Where
        // nothing switches, the line over the bar says why and nothing moves. Hosts a real round; the keys and the wheel go
        // through a virtual keyboard and mouse, so through the HUD's own input handling (FestivalHud.UpdateKeyboard).
        [UnityTest]public IEnumerator OneTwoThreeAndTheWheelSwitchWhatYouHold()
        {
            UseAVirtualKeyboard("HUD gear keyboard");UseAVirtualMouse("HUD gear mouse");
            var world=new GameObject("HUD gear world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD gear");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            PlayerState player=null;
            // One notch of the wheel, down (-1) or up (+1); the wait outlasts the wheel's repeat window.
            IEnumerator Scroll(float notch)
            {
                InputSystem.QueueStateEvent(TestMouse,new MouseState{scroll=new Vector2(0,notch)});yield return null;yield return null;
                yield return new WaitForSeconds(.3f);
            }
            // The slots the gear bar marks held (●), and what the first-person hand holds up.
            string Marked(){string marked="";foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Slot label"&&text.text.EndsWith("●"))marked+=text.text.Substring(0,1);return marked;}
            string Hand()
            {
                var held=new List<string>();var attachments=GameObject.Find("First-person hand attachments");
                if(attachments!=null)foreach(Transform grip in attachments.transform)if(grip.gameObject.activeInHierarchy&&grip.name.StartsWith("Held grip "))held.Add(grip.name.Substring("Held grip ".Length));
                return string.Join(",",held);
            }
            // The line over the gear bar, "(hidden)" while it isn't up; one that a 1080p box cuts off fails.
            string Toast(string where)
            {
                Text line=null;foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Gear switch")line=text;
                if(line==null)return "(hidden)";
                Canvas.ForceUpdateCanvases();
                if(line.cachedTextGenerator.characterCountVisible<line.text.Length)failures.Add(where+": the line over the gear bar cuts \""+line.text+"\"");
                return line.text;
            }
            void Holds(string where,string item,string slot,string toast,string hand=null)
            {
                if(player.EquippedItemId!=item)failures.Add(where+": holding \""+player.EquippedItemId+"\", not "+item+" ("+session.Message+")");
                if(Marked()!=slot)failures.Add(where+": the gear bar marks slot \""+Marked()+"\", not "+slot);
                if(Hand()!=(hand??item))failures.Add(where+": the hand holds up \""+Hand()+"\", not "+(hand??item));
                string shown=Toast(where);if(shown!=toast)failures.Add(where+": over the gear bar reads \""+shown+"\", not \""+toast+"\"");
            }
            // Hand items, given out (shelves sell gear only and stock comes from festivalgoers), with nothing yet in hand.
            void Pack(params string[] items){player.Inventory.Clear();player.EquippedItemId="";foreach(var item in items)player.Inventory.Add(new ItemStack{ItemId=item,Count=1});}
            const string Unpaid="Holding an unpaid item: pay or put it back (G)";
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);player=sim.Player(session.LocalPlayerId);
                // First person whatever this machine's saved profile says (in memory only), so the hand shows.
                session.Profile.Data.ThirdPerson=false;

                // At camp, 1, 2 and 3 each switch, and the line goes after a second.
                Pack("stock_mushrooms","confetti","medical_voucher");
                yield return new WaitForSeconds(.3f);
                yield return Tap(Key.Digit1,.3f);Holds("camp, 1","stock_mushrooms","1","1  •  FUN GUYS");
                yield return Tap(Key.Digit2,.3f);Holds("camp, 2","confetti","2","2  •  CONFETTI CANNON");
                yield return Tap(Key.Digit3,.3f);Holds("camp, 3","medical_voucher","3","3  •  MEDICAL TENT VOUCHER");
                yield return new WaitForSeconds(FestivalHudGear.ToastSeconds);
                string after=Toast("camp, a second after 3");if(after!="(hidden)")failures.Add("camp, a second after 3: over the gear bar still reads \""+after+"\"");
                // An unpaid shelf item in hand covers what you hold: 1 and the wheel say to pay or put it back, and nothing switches.
                player.HeldOfferId="merch_bag";
                yield return Tap(Key.Digit1,.3f);Holds("camp, unpaid item, 1","medical_voucher","3",Unpaid,"merch_bag");
                yield return Scroll(-1);Holds("camp, unpaid item, wheel","medical_voucher","3",Unpaid,"merch_bag");
                player.HeldOfferId="";
                // Inside a tent the host takes no Equip, and 2 says so.
                var tent=CampFeatures.Find("tent_1");player.X=tent.X;player.Z=tent.Z;session.Command("EnterCamp",tent.Id);
                yield return new WaitForSeconds(.3f);
                Assert.That(player.CampVisitId,Is.EqualTo(tent.Id),"setup: inside the tent: "+session.Message);
                yield return Tap(Key.Digit2,.3f);Holds("camp, in a tent, 2","medical_voucher","3","Not inside the tent");
                session.Command("ExitCamp");

                yield return StartLevel(session);
                // The festival, with nobody about but the VIP guard at the rope.
                sim.State.Npcs.RemoveAll(n=>n.Twist!=FestivalSimulation.VipGuard);player.X=0;player.Z=0;
                Pack("stock_mushrooms","confetti","medical_voucher");
                yield return new WaitForSeconds(.3f);
                yield return Tap(Key.Digit1,.3f);Holds("festival, 1","stock_mushrooms","1","1  •  FUN GUYS");
                yield return Tap(Key.Digit2,.3f);Holds("festival, 2","confetti","2","2  •  CONFETTI CANNON");
                yield return Tap(Key.Digit3,.3f);Holds("festival, 3","medical_voucher","3","3  •  MEDICAL TENT VOUCHER");
                // The wheel: down twice from slot 1 lands on 3, once more wraps round to 1, and up steps back to 3.
                yield return Tap(Key.Digit1,.3f);
                yield return Scroll(-1);Holds("festival, wheel down from 1","confetti","2","2  •  CONFETTI CANNON");
                yield return Scroll(-1);Holds("festival, wheel down twice","medical_voucher","3","3  •  MEDICAL TENT VOUCHER");
                yield return Scroll(-1);Holds("festival, wheel down a third time","stock_mushrooms","1","1  •  FUN GUYS");
                yield return Scroll(1);Holds("festival, wheel up from 1","medical_voucher","3","3  •  MEDICAL TENT VOUCHER");
                // One hand item: 3 is empty and says so, and the wheel has nothing to switch to.
                Pack("confetti");
                yield return Tap(Key.Digit1,.3f);
                yield return Tap(Key.Digit3,.3f);Holds("festival, one item, 3","confetti","1","3  •  EMPTY");
                yield return new WaitForSeconds(FestivalHudGear.ToastSeconds);
                yield return Scroll(-1);Holds("festival, one item, wheel","confetti","1","(hidden)");
                // POLO-4's VIP rope chat has no questions, so it never swallows 1-3: they still switch while it's up.
                Pack("stock_mushrooms","confetti");
                var guard=sim.State.Npcs.Find(n=>n.Twist==FestivalSimulation.VipGuard);
                Assert.That(guard,Is.Not.Null,"setup: Palm Mirage's VIP rope has a guard");
                // This solo host is the tripper: with no vision of the guard to check, F's chat is the rope chat.
                sim.State.Visions.RemoveAll(v=>v.NpcId==guard.Id);player.X=guard.X+.6f;player.Z=guard.Z;
                yield return Tap(Key.Digit1,.3f);
                session.Command("ConfirmChat",guard.Id);
                yield return new WaitForSeconds(.3f);
                Assert.That(sim.State.Interactions.Exists(i=>i.Id==player.InteractionId&&i.Kind=="ConfirmChat"&&i.Status=="Active"&&i.Chat.Questions.Length==0),"setup: talking your way past the rope: "+session.Message);
                yield return Tap(Key.Digit2,.3f);Holds("festival, in the rope chat, 2","confetti","2","2  •  CONFETTI CANNON");
                session.Command("Cancel");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"What 1-3 and the wheel switch:\n"+string.Join("\n",failures));
        }

    }
}
