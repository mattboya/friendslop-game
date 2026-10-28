using System.Collections;
using System.Linq;
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
    // PLAYA-5 through the real keys, HUD and host: aboard an Ember Playa art car, E hops off and F plays a four-lane set on its deck.
    // Mid-set E, F and a tap of G do nothing, a lane key and a held arrow reach the set, and only holding G stops it, which hops off
    // the car too. A list built just before the set began can't stop it, and neither can a joined rider's E while their view still
    // shows the plain ride. A friend sees the rider dance the set on the car, and only the rider is told what the crowd tipped.
    public sealed class DeckSetPlayTests:FestivalPlayModeTest
    {
        static readonly Key[] ArrowKeys={Key.LeftArrow,Key.DownArrow,Key.UpArrow,Key.RightArrow};
        const string HopOffOrDeck="Cancel current action  •  F PLAY A DECK SET",PlayDeck="Play a deck set";
        GameObject world,root;FestivalSession session;FestivalHud hud;FestivalSimulation sim;PlayerState player;

        static Transform Hud(GameObject at,string path)=>at.transform.Find("Festival HUD/"+path);
        static bool Shown(GameObject at,string path){var t=Hud(at,path);return t!=null&&t.gameObject.activeInHierarchy;}
        static string Text(GameObject at,string path)=>Hud(at,path)?.GetComponent<Text>()?.text;
        static bool CashAndSalesLit(GameObject at){string card=at.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.name=="Vitals")?.text??"";return System.Text.RegularExpressions.Regex.IsMatch(card,"^HP \\d+  •  <color=#FC7741>")&&card.Contains("\n<color=#FC7741>SALES ");}
        static Button Listed(GameObject at,string label)=>at.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="Action:"+label&&b.gameObject.activeSelf);
        InteractionState Doing=>sim.Interaction(player.InteractionId);
        IEnumerator Until(System.Func<bool> done,float seconds){for(float until=Time.realtimeSinceStartup+seconds;!done()&&Time.realtimeSinceStartup<until;)yield return null;}

        // A hosted Ember Playa level with the HUD: the crowd cleared and your hands empty.
        IEnumerator Ember(string name,int level=0)
        {
            UseAVirtualKeyboard(name+" keyboard");
            world=new GameObject(name+" world");world.AddComponent<FestivalWorld>();yield return null;
            root=new GameObject(name+" host");session=root.AddComponent<FestivalSession>();hud=root.AddComponent<FestivalHud>();yield return null;
            session.Host("Rider",HostPort);
            yield return Until(()=>session.LocalPlayer!=null,30);
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
            sim=Simulation(session);player=sim.Player(session.LocalPlayerId);
            sim.State.FestivalIndex=Festivals.PlayaFestival;sim.State.UnlockedFestivalCount=2;sim.State.LevelIndex=level;
            yield return StartLevel(session);
            sim.State.Npcs.Clear();player.Effects.Clear();player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";
        }
        // Aboard art car 0 on a plain ride, once the HUD has listed what there is to do up there (it lists five times a second).
        IEnumerator Board()
        {
            var at=Festivals.ArtCarAt(0,sim.State.ElapsedSeconds);player.X=at.X;player.Z=at.Z;session.Command(FestivalSimulation.RideCarKind);
            Assert.That(FestivalSimulation.ArtCarOf(sim.State,player.Id),Is.EqualTo(0),"setup: aboard art car 0: "+session.Message);
            yield return new WaitForSeconds(.4f);
        }
        IEnumerator HoldG(){InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(Key.G));yield return new WaitForSeconds(FestivalHudText.HoldToStopSeconds+.25f);InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());yield return null;yield return null;}

        [UnityTest]public IEnumerator FPlaysTheSetOnFourLanesAndOnlyAHeldGStopsItAndHopsOff()
        {
            yield return Ember("PLAYA-5 deck");
            try
            {
                yield return Board();var ride=Doing;
                Assert.That(Text(root,"Action prompt/Prompt"),Is.EqualTo(HopOffOrDeck),"aboard, E hops off and F plays the deck");
                Assert.That(Listed(root,PlayDeck),Is.Not.Null,"the set is a click away in the list too");

                yield return Tap(Key.F);var deck=Doing;
                Assert.That(deck?.Kind,Is.EqualTo(FestivalSimulation.DeckSetKind),"F plays a set on the deck: "+session.Message);
                Assert.That(ride.Status,Is.EqualTo("Complete"),"the plain ride becomes the set, with no cancel");
                Assert.That(FestivalSimulation.ArtCarOf(sim.State,player.Id),Is.EqualTo(0),"still on art car 0");
                yield return new WaitForSeconds(.3f);

                // Four lanes beside the dancer, at 1080p, each line whole.
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;yield return null;
                Assert.That(Shown(root,"Rhythm lane")&&Shown(root,"Live dancer"),Is.True,"the set plays on the lanes beside the live dancer");
                Assert.That(Text(root,"Rhythm lane/Challenge title"),Is.EqualTo("DECK SET  /  FOUR-LANE"));
                Assert.That(Text(root,"Live dancer/Dancer heading"),Is.EqualTo("YOU  /  ON THE ART CAR"));
                Assert.That(Text(root,"Live dancer/Dancer caption"),Is.EqualTo("YOUR LOOK • YOUR SET"));
                Assert.That(Text(root,"Rhythm lane/Stop hold"),Is.EqualTo("HOLD G TO HOP OFF"));
                Canvas.ForceUpdateCanvases();
                foreach(var path in new[]{"Rhythm lane/Challenge title","Live dancer/Dancer heading","Live dancer/Dancer caption","Rhythm lane/Stop hold"})
                {var text=Hud(root,path).GetComponent<Text>();Assert.That(text.cachedTextGenerator.characterCountVisible,Is.EqualTo(text.text.Length),path+" fits its box");}

                // The car carries the set: your first-person hands stay up and still.
                var hands=session.ViewCamera.GetComponentInChildren<FestivalHands>(true);Assert.That(hands.gameObject.activeInHierarchy,Is.True,"the rider's hands stay in view");
                var sweep=new Bounds(hands.transform.localPosition,Vector3.zero);
                for(float until=Time.realtimeSinceStartup+.8f;Time.realtimeSinceStartup<until;){yield return null;sweep.Encapsulate(hands.transform.localPosition);}
                Assert.That(sweep.size.x,Is.LessThan(.002f),"the rolling car is no walking bob");

                // Mid-set E (between W and D), F and a tap of G do nothing; a lane key reaches the set.
                yield return Tap(Key.E,.1f);yield return Tap(Key.F,.1f);yield return Tap(Key.G,.1f);
                Assert.That(deck.Status,Is.EqualTo("Active"),"E, F and a tap of G leave the set playing: "+session.Message);Assert.That(player.InteractionId,Is.EqualTo(deck.Id));
                yield return Until(()=>session.EstimatedSimulationSeconds-deck.StartSeconds>.2,3);
                yield return Tap(Key.LeftArrow);
                yield return Until(()=>deck.Inputs.Count>0,2);
                Assert.That(deck.Inputs.Count,Is.EqualTo(1),"the lane key reaches the set: "+session.Message);

                yield return HoldG();
                Assert.That(deck.Status,Is.EqualTo("Cancelled"),"holding G stops the set");
                Assert.That(FestivalSimulation.ArtCarOf(sim.State,player.Id),Is.EqualTo(-1),"and hops off the car");
                Assert.That(Shown(root,"Rhythm lane"),Is.False,"the lanes go");
            }
            finally{session.Leave();Object.Destroy(root);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator EHopsOffAPlainRideAndAListFromBeforeTheSetCannotStopIt()
        {
            yield return Ember("PLAYA-5 stale");
            try
            {
                yield return Board();
                var hopOff=Listed(root,HopOffOrDeck);var play=Listed(root,PlayDeck);
                Assert.That(hopOff!=null&&play!=null,Is.True,"setup: aboard, the list offers both");
                // The list as it stood aboard: it is not refreshed again while the set begins.
                var refresh=typeof(FestivalHud).GetField("nextActionRefresh",BindingFlags.NonPublic|BindingFlags.Instance);refresh.SetValue(hud,float.MaxValue);
                yield return Tap(Key.F);var deck=Doing;
                Assert.That(deck?.Kind,Is.EqualTo(FestivalSimulation.DeckSetKind),"setup: F plays the set: "+session.Message);
                string message=session.Message;
                hopOff.onClick.Invoke();play.onClick.Invoke();yield return null;yield return null;
                Assert.That(deck.Status,Is.EqualTo("Active"),"the way off listed for the plain ride can't stop the set");
                Assert.That(player.InteractionId,Is.EqualTo(deck.Id));
                Assert.That(session.Message,Is.EqualTo(message),"nor does the listed set ask the host for a second one");
                yield return Tap(Key.E,.1f);
                Assert.That(deck.Status,Is.EqualTo("Active"),"mid-set E does nothing, though the stale way off is still first on the list");
                refresh.SetValue(hud,0f);

                // On a plain ride, E hops off.
                session.Command("Cancel");yield return Board();var ride=Doing;
                Assert.That(ride?.Kind,Is.EqualTo(FestivalSimulation.RideCarKind),"setup: a plain ride again");
                yield return Tap(Key.E,.1f);
                Assert.That(ride.Status,Is.EqualTo("Cancelled"),"E hops off the plain ride: "+session.Message);
                Assert.That(FestivalSimulation.ArtCarOf(sim.State,player.Id),Is.EqualTo(-1));
            }
            finally{session.Leave();Object.Destroy(root);Object.Destroy(world);}
            yield return null;
        }

        // A joined view lags the host. F turns the plain ride into a set on the host while the rider's view still shows the ride,
        // so E there still offers the way off. That Cancel names the ride, and the host refuses it: the set plays on, at no cost.
        // The menu's CANCEL ACTION, a held G and Get up also name what the view shows. A second F there asks the host for a
        // second set, and the rider's notice says the set is already playing.
        [UnityTest]public IEnumerator AJoinedRidersLateECannotStopASetTheirViewHasNotShownYet()
        {
            UseAVirtualKeyboard("PLAYA-8 late keyboard");
            world=new GameObject("PLAYA-8 late world");world.AddComponent<FestivalWorld>();yield return null;
            var hostRoot=new GameObject("PLAYA-8 late host");var host=hostRoot.AddComponent<FestivalSession>();hostRoot.AddComponent<FestivalHud>();yield return null;
            root=new GameObject("PLAYA-8 late rider");var rider=root.AddComponent<FestivalSession>();root.AddComponent<FestivalHud>();yield return null;
            var sends=typeof(FestivalSession).GetField("nextSnapshot",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                ushort port=FreePort();host.Host("Host",port);yield return null;Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);host.Controls.Map.Disable();
                sim=Simulation(host);sim.State.FestivalIndex=Festivals.PlayaFestival;sim.State.UnlockedFestivalCount=2;
                rider.Join("Rider","127.0.0.1",port);
                yield return Until(()=>rider.LocalPlayer!=null,30);Assert.That(rider.LocalPlayer,Is.Not.Null,rider.Message);
                player=sim.Player(rider.LocalPlayerId);player.X=0;player.Z=19;rider.Command("Ready");
                yield return Until(()=>player.Ready,5);Assert.That(player.Ready,Is.True,rider.Message);
                yield return StartLevel(host,rider.LocalPlayerId);
                sim.State.Npcs.Clear();player.Effects.Clear();player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";
                var at=Festivals.ArtCarAt(0,sim.State.ElapsedSeconds);player.X=at.X;player.Z=at.Z;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="playa8-board",Kind=FestivalSimulation.RideCarKind}).Accepted,Is.True,"setup: the rider climbs aboard");
                var ride=Doing;
                yield return Until(()=>rider.LocalPlayer?.InteractionId==ride.Id,3);yield return new WaitForSeconds(.4f);
                Assert.That(Text(root,"Action prompt/Prompt"),Is.EqualTo(HopOffOrDeck),"setup: aboard, the rider's E hops off and F plays the deck");

                // The rider's view stops here: the host sends no snapshot while F and then E go out.
                sends.SetValue(host,Time.realtimeSinceStartupAsDouble+1000);
                yield return Tap(Key.F);
                yield return Until(()=>Doing?.Kind==FestivalSimulation.DeckSetKind,3);var deck=Doing;
                Assert.That(deck?.Kind,Is.EqualTo(FestivalSimulation.DeckSetKind),"setup: the rider's F plays the set on the host: "+rider.Message);
                Assert.That(rider.LocalPlayer.InteractionId,Is.EqualTo(ride.Id),"setup: the rider's view still shows the plain ride");
                yield return Tap(Key.F,.1f);
                yield return Until(()=>rider.Message=="Your set is already playing",3);
                Assert.That(deck.Status=="Active"&&player.InteractionId==deck.Id&&FestivalSimulation.ArtCarOf(sim.State,player.Id)==0,Is.True,"a second F leaves the set playing, aboard: "+rider.Message);
                yield return null;
                Assert.That(Shown(root,"Session notice")?Text(root,"Session notice/Notice"):"(no notice)",Is.EqualTo("Your set is already playing"),"the rider's notice says the set is already playing");
                // Setup: a fan who has watched the set from the start, a little suspicious, and far enough off to see nothing new.
                var fan=new NpcState{Id="playa8_fan",X=player.X+40,Z=player.Z};fan.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=40});
                sim.State.Npcs.Add(fan);deck.WitnessIds.Add(fan.Id);

                yield return Tap(Key.E,.1f);
                yield return Until(()=>deck.Status!="Active"||rider.Message=="That action already ended",3);
                Assert.That(deck.Status,Is.EqualTo("Active"),"the late E leaves the set playing: "+rider.Message);
                Assert.That(player.InteractionId,Is.EqualTo(deck.Id));Assert.That(FestivalSimulation.ArtCarOf(sim.State,player.Id),Is.EqualTo(0),"still aboard art car 0");
                Assert.That(rider.Message,Is.EqualTo("That action already ended"),"the host refused a Cancel that named the ride");
                bool Refused(int after)=>sim.State.Commands.Skip(after).Any(c=>c.PlayerId==player.Id&&c.Result.Reason=="That action already ended");
                int sent=sim.State.Commands.Count;
                root.GetComponentsInChildren<Button>(true).First(b=>b.name=="CANCEL ACTION").onClick.Invoke();
                yield return Until(()=>deck.Status!="Active"||Refused(sent),3);
                Assert.That(deck.Status,Is.EqualTo("Active"),"nor does the menu's CANCEL ACTION, which reads the same view: "+rider.Message);
                Assert.That(Refused(sent),Is.True,"the host refused the menu's Cancel");
                Assert.That(fan.Observers.Find(o=>o.PlayerId==player.Id)?.Suspicion??0,Is.LessThan(41),"and the set's witness grows no more suspicious");

                // Once the view catches up, the rider sees the set.
                sends.SetValue(host,0d);
                yield return Until(()=>rider.LocalPlayer?.InteractionId==deck.Id,3);
                Assert.That(rider.LocalPlayer?.InteractionId,Is.EqualTo(deck.Id),"the rider's view catches up to the set");

                // Behind a view that shows the set, the set ends on the host and the rider boards again: a held G names the set.
                sends.SetValue(host,Time.realtimeSinceStartupAsDouble+1000);
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="playa8-off",Kind="Cancel"}).Accepted,Is.True,"setup: the set ends on the host");
                at=Festivals.ArtCarAt(0,sim.State.ElapsedSeconds);player.X=at.X;player.Z=at.Z;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="playa8-reboard",Kind=FestivalSimulation.RideCarKind}).Accepted,Is.True,"setup: the rider boards again");
                var again=Doing;sent=sim.State.Commands.Count;
                yield return HoldG();
                yield return Until(()=>again.Status!="Active"||Refused(sent),3);
                Assert.That(again.Status,Is.EqualTo("Active"),"a held G for the set the view still shows leaves the new ride alone: "+rider.Message);
                Assert.That(Refused(sent),Is.True,"the host refused the held G's Cancel");
                Assert.That(FestivalSimulation.ArtCarOf(sim.State,player.Id),Is.EqualTo(0),"still aboard art car 0");

                // On the grass, the view shows one lie-down while the host has swapped it for another: E's Get up names the first.
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="playa8-hop",Kind="Cancel"}).Accepted,Is.True,"setup: the rider hops off");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="playa8-lie",Kind=FestivalSimulation.LieDownKind}).Accepted,Is.True,"setup: the rider lies down");
                var lie=Doing;sends.SetValue(host,0d);
                yield return Until(()=>rider.LocalPlayer?.InteractionId==lie.Id,3);yield return new WaitForSeconds(.4f);
                Assert.That(Text(root,"Action prompt/Prompt"),Is.EqualTo(FestivalHudText.GetUpAction),"setup: lying down, the rider's E gets up");
                sends.SetValue(host,Time.realtimeSinceStartupAsDouble+1000);
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="playa8-up",Kind="Cancel"}).Accepted,Is.True,"setup: the rider gets up on the host");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="playa8-lie-again",Kind=FestivalSimulation.LieDownKind}).Accepted,Is.True,"setup: and lies down again");
                var relie=Doing;sent=sim.State.Commands.Count;
                yield return Tap(Key.E,.1f);
                yield return Until(()=>relie.Status!="Active"||Refused(sent),3);
                Assert.That(relie.Status,Is.EqualTo("Active"),"E gets up only from the lie-down the view shows: "+rider.Message);
                Assert.That(Refused(sent),Is.True,"the host refused the Get up");
            }
            finally{sends.SetValue(host,0d);rider.Leave();host.Leave();Object.Destroy(root);Object.Destroy(hostRoot);Object.Destroy(world);}
            yield return null;
        }

        // DANCE-10 on the deck: from Night 1 a set has hold arrows. A real arrow key held from the head and let go partway along the
        // tail reaches the set as a press and a release.
        [UnityTest]public IEnumerator AHeldArrowOnTheDeckReachesTheSetAsAPressAndARelease()
        {
            yield return Ember("PLAYA-5 hold",1);
            try
            {
                yield return Board();yield return Tap(Key.F);var deck=Doing;
                Assert.That(deck?.Kind,Is.EqualTo(FestivalSimulation.DeckSetKind),"setup: F plays the set: "+session.Message);
                // Setup: the chart seed alone picks the arrows; take one whose set has a hold arrow well inside it.
                double now=session.EstimatedSimulationSeconds-deck.StartSeconds;RhythmNote hold=null;
                for(int tries=0;tries<200&&hold==null;tries++)
                {
                    hold=RhythmChart.For(deck).Notes.Find(n=>n.HoldSeconds>.2&&n.TimeSeconds>now+1&&n.TimeSeconds+n.HoldSeconds<deck.DurationSeconds-.5);
                    if(hold==null)deck.ChartSeed++;
                }
                Assert.That(hold,Is.Not.Null,"setup: a Night 1 set with a hold arrow");
                yield return Until(()=>session.EstimatedSimulationSeconds-deck.StartSeconds>=hold.TimeSeconds-.01,15);
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(ArrowKeys[hold.Direction]));
                yield return Until(()=>session.EstimatedSimulationSeconds-deck.StartSeconds>=hold.TimeSeconds+hold.HoldSeconds*.5,5);
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());
                yield return Until(()=>deck.Inputs.Exists(i=>i.Release),2);
                Assert.That(deck.Inputs.Exists(i=>!i.Release&&i.Direction==hold.Direction&&System.Math.Abs(i.TimeSeconds-hold.TimeSeconds)<=deck.GoodWindowSeconds),Is.True,"the press lands on the head: "+session.Message);
                Assert.That(deck.Inputs.Exists(i=>i.Release&&i.Direction==hold.Direction&&i.TimeSeconds>hold.TimeSeconds&&i.TimeSeconds<hold.TimeSeconds+hold.HoldSeconds),Is.True,"the release reaches the set partway along the tail");
            }
            finally{session.Leave();Object.Destroy(root);Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator OnlyTheJoinedRiderIsToldTheTipsWhileTheHostSeesTheSetOnTheCar()
        {
            UseAVirtualKeyboard("PLAYA-5 joined keyboard");
            world=new GameObject("PLAYA-5 joined world");world.AddComponent<FestivalWorld>();yield return null;
            var hostRoot=new GameObject("PLAYA-5 joined host");var host=hostRoot.AddComponent<FestivalSession>();hostRoot.AddComponent<FestivalHud>();yield return null;
            root=new GameObject("PLAYA-5 joined rider");var rider=root.AddComponent<FestivalSession>();root.AddComponent<FestivalHud>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Host",port);yield return null;Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);host.Controls.Map.Disable();
                sim=Simulation(host);sim.State.FestivalIndex=Festivals.PlayaFestival;sim.State.UnlockedFestivalCount=2;
                rider.Join("Rider","127.0.0.1",port);
                yield return Until(()=>rider.LocalPlayer!=null,30);Assert.That(rider.LocalPlayer,Is.Not.Null,rider.Message);
                player=sim.Player(rider.LocalPlayerId);player.X=0;player.Z=19;rider.Command("Ready");
                yield return Until(()=>player.Ready,5);Assert.That(player.Ready,Is.True,rider.Message);
                yield return StartLevel(host,rider.LocalPlayerId);
                sim.State.Npcs.Clear();player.Effects.Clear();player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";
                var at=Festivals.ArtCarAt(0,sim.State.ElapsedSeconds);player.X=at.X;player.Z=at.Z;
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="playa5-board",Kind=FestivalSimulation.RideCarKind}).Accepted,Is.True,"setup: the rider climbs aboard");
                yield return Until(()=>FestivalSimulation.ArtCarOf(rider.State,player.Id)==0,3);

                // The rider's own F plays the set; the host sees them dance it on the car.
                yield return Tap(Key.F);
                yield return Until(()=>Doing?.Kind==FestivalSimulation.DeckSetKind,3);var deck=Doing;
                Assert.That(deck?.Kind,Is.EqualTo(FestivalSimulation.DeckSetKind),"the joined rider's F plays the set: "+rider.Message);
                yield return new WaitForSeconds(.4f);
                Assert.That(host.WorldCharacter(player.Id)?.Pose,Is.EqualTo(FestivalSimulation.DeckSetKind),"the host sees the rider in the deck set's own pose");
                Assert.That(Shown(root,"Rhythm lane"),Is.True,"the rider's lanes are up");Assert.That(Shown(hostRoot,"Rhythm lane"),Is.False,"the host has none");

                // Setup: one fan in the set's crowd, and a perfect set.
                var fan=new NpcState{Id="playa5_fan",X=player.X+8,Z=player.Z};sim.State.Npcs.Add(fan);deck.WitnessIds.Add(fan.Id);
                var edges=new System.Collections.Generic.List<RhythmInput>();
                foreach(var note in RhythmChart.For(deck).Notes){edges.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});if(note.HoldSeconds>0)edges.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds+note.HoldSeconds,Release=true});}
                edges.Sort((a,b)=>a.TimeSeconds.CompareTo(b.TimeSeconds));int sent=0;
                foreach(var edge in edges){sim.State.SimulationSeconds=deck.StartSeconds+edge.TimeSeconds;if(sim.Execute(player.Id,new GameCommand{Id="playa5-edge-"+(sent++),Kind="Rhythm",Direction=edge.Direction,TimeSeconds=edge.TimeSeconds,Amount=edge.Release?1:0}).Accepted)continue;Assert.Fail("setup: edge "+sent+" refused");}
                sim.State.SimulationSeconds=deck.StartSeconds+deck.DurationSeconds+.75;
                yield return Until(()=>player.DeckTipAmount>0,3);
                Assert.That(player.DeckTipAmount,Is.GreaterThan(0),"setup: the crowd tipped the perfect set");
                Assert.That(Doing?.Kind,Is.EqualTo(FestivalSimulation.RideCarKind),"the set ends on a plain ride");

                string tips="The crowd tipped",hostNotice="";bool riderTold=false,riderLit=false;
                for(float until=Time.realtimeSinceStartup+1.5f;Time.realtimeSinceStartup<until;)
                {
                    yield return null;
                    riderTold|=Shown(root,"Session notice")&&(Text(root,"Session notice/Notice")??"").Contains(tips);riderLit|=CashAndSalesLit(root);
                    if(Shown(hostRoot,"Session notice")&&(Text(hostRoot,"Session notice/Notice")??"").Contains(tips))hostNotice=Text(hostRoot,"Session notice/Notice");
                }
                for(float until=Time.realtimeSinceStartup+.5f;riderTold&&!riderLit&&Time.realtimeSinceStartup<until;){yield return null;riderLit|=CashAndSalesLit(root);}
                Assert.That(riderTold,Is.True,"the rider is told what the crowd tipped: "+Text(root,"Session notice/Notice"));
                Assert.That(riderLit,Is.True,"the tips light the rider's cash and SALES: "+root.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.name=="Vitals")?.text);
                Assert.That(Text(root,"Session notice/Notice"),Does.Contain(Festivals.CurrencyName(Festivals.PlayaFestival,player.Ordinal,player.DeckTipAmount)));
                Assert.That(hostNotice,Is.Empty,"nobody else hears it");
            }
            finally{rider.Leave();host.Leave();Object.Destroy(root);Object.Destroy(hostRoot);Object.Destroy(world);}
            yield return null;
        }
    }
}
