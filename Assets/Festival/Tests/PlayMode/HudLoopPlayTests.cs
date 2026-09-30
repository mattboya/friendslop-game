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
    public sealed class HudLoopPlayTests
    {
        // HUD-1: the running HUD puts FestivalHudText's lines on screen. Hosts a real two-player round and
        // reads the labels a player sees on Day 1, on Night 2 with a body out in the field, and at results.
        [UnityTest]public IEnumerator HudShowsTheWeekendQuotaChecklistAndBodies()
        {
            var world=new GameObject("HUD loop world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD loop");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            try
            {
                session.Host("Tester",8580);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("hud_mate","Sam");mate.Ready=true;
                player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)
                {
                    // Sam has no client of their own to report the map loaded.
                    if(sim.State.Phase=="Loading"&&!mate.MapReady)sim.Execute(mate.Id,new GameCommand{Id="hud_mate_loaded",Kind="MapReady"});
                    yield return null;
                }
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"round starts: "+session.Message);

                // Day 1 of Palm Mirage asks $15 from each of the two.
                sim.State.Npcs.Clear();sim.State.LevelSales=12;player.X=0;player.Z=0;mate.X=30;mate.Z=0;
                yield return new WaitForSeconds(.6f);
                Expect("day","Level banner","PALM MIRAGE  •  DAY 1");
                Expect("day","Objective title","DAY QUOTA  $12 / $30");
                Expect("day","Objective detail","Sell $18 more before sundown.");
                if(!System.Text.RegularExpressions.Regex.IsMatch(Read("Time"),@"^[0-8]:[0-5]\d$"))failures.Add("day: Time reads \""+Read("Time")+"\", expected m:ss");
                sim.State.LevelSales=30;player.X=0;player.Z=-32;
                yield return new WaitForSeconds(.6f);
                Expect("day, quota met","Objective title","QUOTA MET — HEAD BACK TO CAMP");
                Expect("day, quota met at the way back to camp","Prompt","End the day: head back to camp");

                // Night 2: Sam died out in the field and their body lies beside you.
                sim.State.LevelIndex=3;sim.State.DurationSeconds=600;mate.Life="Spirit";player.X=0;player.Z=0;
                sim.State.Bodies.Add(new BodyState{PlayerId=mate.Id,X=1,Z=0});
                yield return new WaitForSeconds(.6f);
                Expect("night 2","Level banner","PALM MIRAGE  •  NIGHT 2");
                Expect("night 2","Roster","HOME  0 / 2\nYOU  •  AWAY\nSAM  •  BODY AWAY");
                Expect("night 2 beside a body","Prompt","Carry Sam's body");

                // Results after a missed quota: the weekend restarts, and the host brings the crew back.
                sim.State.LevelIndex=0;sim.State.Result="Missed the quota";sim.State.Phase="Results";
                yield return new WaitForSeconds(.6f);
                Expect("results","Objective title","WEEKEND OVER  •  BACK TO DAY 1");
                Expect("results","Objective detail","Missed the quota. Palm Mirage restarts at Day 1 with fresh cash.");
                Expect("results","Prompt","ESC MENU  •  NEXT CAMP");
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
    }
}
