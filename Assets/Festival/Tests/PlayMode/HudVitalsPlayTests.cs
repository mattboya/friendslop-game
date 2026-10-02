using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    public sealed class HudVitalsPlayTests:FestivalPlayModeTest
    {
        const string Stop="SECURITY STOP • TALK (E) • DITCH IT (G) • OR WALK AWAY";
        const string StopNotice="SECURITY SAW YOUR STOCK: talk, ditch it or walk away within 20 s, or you're arrested";

        // TRIP-1: the player card draws its crowd or security warning whole at 1920x1080, after the cash, stash, sales and life,
        // for a dosed tripper mid-day with a cop about to detain them and on Night 2 with a debrief shot on top. The effects after
        // the warning may run off the card; the warning may not.
        [UnityTest]public IEnumerator ThePlayerCardDrawsTheWholeWarningAt1080p()
        {
            var world=new GameObject("HUD vitals world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD vitals");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            var failures=new List<string>();
            // The warning runs from its lead ("ARREST ", "CROWD ") to the next separator, where the effects start.
            void WarningDrawn(string where,string lead)
            {
                Text vitals=null;foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Vitals")vitals=text;
                if(vitals==null){failures.Add(where+": the player card is hidden");return;}
                Canvas.ForceUpdateCanvases();
                string shown=vitals.text;int at=shown.IndexOf(lead),end=at<0?-1:shown.IndexOf("  •  ",at),drawn=vitals.cachedTextGenerator.characterCountVisible;
                if(end<0)end=shown.Length;
                if(at<0)failures.Add(where+": the card reads \""+shown.Replace("\n"," | ")+"\", with no \""+lead+"\" warning");
                else if(drawn<end)failures.Add(where+": the card's box "+vitals.rectTransform.rect.size+" cuts \""+shown.Replace("\n"," | ")+"\" after \""+shown.Substring(0,drawn).Replace("\n"," | ")+"\"");
            }
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
                var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("vitals_mate","Sam");mate.Ready=true;
                yield return StartLevel(session);

                // You trip on a dose with 431 s left, alone in the field with $87 made and $120 sold, and no crowd around.
                var wooks=sim.State.Npcs.FindAll(n=>n.Kind=="Wook");sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");
                sim.State.TripperId=player.Id;player.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
                player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="vitals_dose",Intensity=2,RemainingSeconds=431});
                player.Cash=87;sim.State.StashCash=0;sim.State.LevelSales=120;player.X=0;player.Z=0;mate.X=30;mate.Z=0;
                // A cop saw the deal and is about to detain you.
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");cop.X=1.5f;cop.Z=0;cop.Evidence.Add(new EvidenceState{PlayerId=player.Id,Kind="WitnessedDeal",DetainAt=sim.State.SimulationSeconds+300});
                yield return new WaitForSeconds(.4f);
                WarningDrawn("a dosed tripper mid-day, a cop about to detain them","ARREST WARNING");
                // Night 2's numbers, with the debrief's shot on top of the dose.
                player.Effects.Add(new ActiveEffect{Id="shot",InstanceId="vitals_shot",RemainingSeconds=90});player.Cash=187;sim.State.StashCash=45;sim.State.LevelSales=225;
                yield return new WaitForSeconds(.4f);
                WarningDrawn("dosed with a shot, $187 / $45 / $225","ARREST WARNING");
                // ESC-2: the cop stops you over the stock you carry instead, and the card names every way out, whole.
                cop.Evidence.Clear();player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                yield return new WaitForSeconds(.4f);
                WarningDrawn("dosed with a shot, a cop stopping you",Stop);
                // The crowd turns on you instead: a festivalgoer beside you at 80.
                player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");cop.Evidence.Clear();cop.X=35;cop.Z=35;cop.TargetId="";
                var watcher=wooks[0];watcher.X=3;watcher.Z=0;watcher.TargetId=player.Id;watcher.Suspicion=80;
                watcher.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=80,LastSeenSeconds=sim.State.SimulationSeconds});sim.State.Npcs.Add(watcher);
                yield return new WaitForSeconds(.4f);
                WarningDrawn("dosed with a shot, the crowd turning","CROWD ");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"The player card at 1080p:\n"+string.Join("\n",failures));
        }

        // ESC-2: a security stop on screen, at 1080p. The guard who sees your stock calls you out, the notice says why and what to
        // do about it, and the card's warning names the three ways out. Every stop gets its notice: one after a stop that was the
        // last message, and one that starts mid-dance once the dance is over. E talks to the guard, as the card says, with a
        // festivalgoer nearer than the guard and a Tongue Stamp you just ditched (G) at your feet.
        [UnityTest]public IEnumerator AStopSaysWhyAndETalksToTheGuard()
        {
            var world=new GameObject("HUD stop world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD stop");var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            void Expect(string where,string label,string expected)
            {
                Canvas.ForceUpdateCanvases();var text=Find(label);
                if(text==null)failures.Add(where+": "+label+" is hidden, expected \""+expected+"\"");
                else if(!text.text.Contains(expected))failures.Add(where+": "+label+" reads \""+text.text.Replace("\n"," | ")+"\", expected \""+expected+"\"");
                else if(text.cachedTextGenerator.characterCountVisible<text.text.IndexOf(expected)+expected.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" cuts \""+text.text.Replace("\n"," | ")+"\"");
            }
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
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

                // You carry two Tongue Stamps past security, 2 m off and facing you, with a festivalgoer at arm's length.
                sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");player.X=0;player.Z=0;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});
                sim.State.Npcs.Add(new NpcState{Id="stop_festivalgoer",X=-1,Z=0,Yaw=90});
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");cop.Evidence.Clear();cop.X=2;cop.Z=0;cop.Yaw=-90;
                yield return new WaitForSeconds(.4f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: security stops you over the stock ("+session.Message+")");
                Expect("stopped","Notice",StopNotice);
                Expect("stopped","Speaker","SECURITY");Expect("stopped","Dialogue line","HEY! What's in the bag?");
                Expect("stopped","Vitals",Stop);
                Expect("stopped","Prompt","Talk to security");

                // Each stop below ends without a word from you (the host takes the stock), and the next starts with the guard turned
                // back to you. The first notice's 5 s run out with nothing said since, so it is still the session's last message.
                void StopAgain(){cop.X=player.X+2;cop.Z=player.Z;cop.Yaw=-90;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});}
                player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");yield return new WaitForSeconds(5.2f);
                Assert.That(cop.Mode!="Stop"&&Find("Notice")==null&&session.Message==StopNotice,Is.True,"setup: the stop is over, its notice gone and still the last message ("+cop.Mode+", \""+session.Message+"\")");
                StopAgain();yield return new WaitForSeconds(.4f);
                Expect("stopped again","Notice",StopNotice);
                // The next starts as you dance, over 5 s before the dance ends: a notice shown then would be gone by the end.
                player.Inventory.RemoveAll(i=>i.ItemId=="stock_lsd");yield return new WaitForSeconds(.4f);
                Assert.That(cop.Mode,Is.Not.EqualTo("Stop"),"setup: with the stock gone the stop ends");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="stop_dance",Kind="Dance",TargetId="stop_festivalgoer"}).Accepted,Is.True,"setup: a dance starts");
                sim.Interaction(player.InteractionId).DurationSeconds=60;
                StopAgain();yield return new WaitForSeconds(5.5f);
                Assert.That(cop.Mode,Is.EqualTo("Stop"),"setup: security stops you mid-dance");
                Assert.That(sim.Execute(player.Id,new GameCommand{Id="stop_dance_over",Kind="Cancel"}).Accepted,Is.True,"setup: the dance ends");
                yield return new WaitForSeconds(.4f);
                Expect("stopped mid-dance, the dance over","Notice",StopNotice);

                // G ditches one at your feet; the stop holds over the other, and E still talks.
                session.Command("Drop",item:"stock_lsd");yield return new WaitForSeconds(.4f);
                Assert.That(sim.State.Drops.Exists(d=>d.ItemId=="stock_lsd")&&cop.Mode=="Stop",Is.True,"setup: one Tongue Stamp ditched, the stop holds ("+session.Message+", "+cop.Mode+")");
                Expect("one ditched at your feet","Prompt","Talk to security");
                ((System.Action)primary.GetValue(view))?.Invoke();yield return new WaitForSeconds(.4f);
                var talk=sim.Interaction(player.InteractionId);
                if(talk?.Kind!="Police"||talk.TargetId!=cop.Id)failures.Add("E starts "+(talk==null?"nothing":talk.Kind+" with "+talk.TargetId)+" ("+session.Message+"), not a talk with "+cop.Id);
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"A security stop on screen:\n"+string.Join("\n",failures));
        }
    }
}
