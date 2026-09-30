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
        // TRIP-1: the player card draws its crowd or security warning whole at 1920x1080, after the cash, stash, sales and life,
        // for a dosed tripper mid-day with a cop about to detain them and on Night 2 with a debrief shot on top. The effects after
        // the warning may run off the card; the warning may not.
        [UnityTest]public IEnumerator ThePlayerCardDrawsTheWholeWarningAt1080p()
        {
            var world=new GameObject("HUD vitals world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD vitals");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            var failures=new List<string>();
            // The warning runs from its lead ("SECURITY ", "CROWD ") to the next separator, where the effects start.
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
                player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)
                {
                    // Sam has no client of their own to report the map loaded.
                    if(sim.State.Phase=="Loading"&&!mate.MapReady)sim.Execute(mate.Id,new GameCommand{Id="vitals_mate_loaded",Kind="MapReady"});
                    yield return null;
                }
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"round starts: "+session.Message);

                // You trip on a dose with 431 s left, alone in the field with $87 made and $120 sold, and no crowd around.
                var wooks=sim.State.Npcs.FindAll(n=>n.Kind=="Wook");sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");
                sim.State.TripperId=player.Id;player.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
                player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="vitals_dose",Intensity=2,RemainingSeconds=431});
                player.Cash=87;sim.State.StashCash=0;sim.State.LevelSales=120;player.X=0;player.Z=0;mate.X=30;mate.Z=0;
                // A cop saw the deal and is about to detain you.
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");cop.X=1.5f;cop.Z=0;cop.Evidence.Add(new EvidenceState{PlayerId=player.Id,Kind="WitnessedDeal",DetainAt=sim.State.SimulationSeconds+300});
                yield return new WaitForSeconds(.4f);
                WarningDrawn("a dosed tripper mid-day, a cop about to detain them","SECURITY ARRESTWARNING");
                // Night 2's numbers, with the debrief's shot on top of the dose.
                player.Effects.Add(new ActiveEffect{Id="shot",InstanceId="vitals_shot",RemainingSeconds=90});player.Cash=187;sim.State.StashCash=45;sim.State.LevelSales=225;
                yield return new WaitForSeconds(.4f);
                WarningDrawn("dosed with a shot, $187 / $45 / $225","SECURITY ARRESTWARNING");
                // The crowd turns on you instead: a festivalgoer beside you at 80.
                cop.Evidence.Clear();cop.X=35;cop.Z=35;cop.TargetId="";
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
    }
}
