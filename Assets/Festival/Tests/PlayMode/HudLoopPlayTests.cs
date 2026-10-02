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
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
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

        // HUD-4: a menu left open at camp closes as the wheels start, so the spin is seen. While the wheels spin and the festival
        // loads, the host refuses every action, so the HUD offers none and 1, Q and G send nothing. A key being rebound keeps the
        // menu open as the festival opens. Hosts a real two-player round; the keys are pressed on a virtual keyboard, so they go
        // through the HUD's own input handling (FestivalHud.UpdateKeyboard).
        [UnityTest]public IEnumerator TheSpinClosesTheMenuAndOffersNothingToPress()
        {
            var input=InputSystem.settings;oldBackground=input.backgroundBehavior;oldEditor=input.editorInputBehaviorInPlayMode;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("HUD spin keyboard");
            var world=new GameObject("HUD spin world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD spin");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            string Prompt(){foreach(var text in hud.GetComponentsInChildren<Text>(true))if(text.name=="Prompt")return text.text;return "(no prompt)";}
            IEnumerator Press(Key key)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            }
            // No prompt, nothing for E, and the gear keys send nothing for the host to refuse.
            IEnumerator NothingToPress(string where)
            {
                yield return new WaitForSeconds(.3f);
                if(Prompt()!="")failures.Add(where+": the prompt reads \""+Prompt()+"\"");
                if(primary.GetValue(view)!=null)failures.Add(where+": E still runs an action");
                foreach(var key in new[]{Key.Digit1,Key.Q,Key.G})
                {
                    yield return Press(key);
                    if(session.Message=="Round is not interactive")failures.Add(where+": "+key+" sent a command the host refused");
                }
            }
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
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

        // The virtual keyboard and the input settings go back however the test ended (an error log skips its own finally).
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        [UnityTearDown]public IEnumerator RestoreTheKeyboard()
        {
            if(keyboard!=null)
            {
                InputSystem.RemoveDevice(keyboard);keyboard=null;
                InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
            }
            yield return null;
        }
    }
}
