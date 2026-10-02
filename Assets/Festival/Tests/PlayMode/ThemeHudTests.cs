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
    public sealed class ThemeHudTests:FestivalPlayModeTest
    {
        // THEME-1's real-drug words (the same list as tests/Domain/ThemeTests.cs), matched in any case.
        private static readonly string[] Banned={"lsd","acid","mushroom","shroom","ecstasy","molly","mdma","ketamine","weed","marijuana","cannabis","cocaine","drug"};

        // THEME-1: the HUD joins item ids into its prompt and action labels at runtime, where a scan of source literals
        // cannot see them. This hosts a real round and reads the text the HUD actually shows, at camp and at the festival.
        [UnityTest]public IEnumerator HudShowsFictionalItemAndEffectNames()
        {
            var world=new GameObject("Theme HUD world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Theme HUD");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("theme_mate","Mate");mate.Ready=true;
                player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=3});

                // Camp: a friend beside you offers Fun Guys, and you can hand them your Tongue Stamps.
                mate.X=player.X+1;mate.Z=player.Z;
                sim.State.Transfers.Add(new TransferOffer{Id="theme_camp_offer",FromId=mate.Id,ToId=player.Id,ItemId="stock_mushrooms",Amount=1,ExpiresAt=sim.State.SimulationSeconds+600});
                yield return new WaitForSeconds(.6f);
                Check(hud,failures,"camp",null,null,"Accept Fun Guys ×1","Offer Tongue Stamps to Mate");

                yield return StartLevel(session);

                // Festival: take a Tongue Stamp, then a dropped stamp lies at your feet with a festivalgoer to sell to.
                sim.State.Npcs.Clear();mate.X=player.X+30;mate.Z=player.Z;
                session.Command("Use",item:"stock_lsd");
                Assert.That(player.Effects.Exists(e=>e.Id=="lsd"),"took a Tongue Stamp: "+session.Message);
                sim.State.Drops.Add(new DropState{Id="theme_drop",ItemId="stock_lsd",Count=1,X=player.X,Z=player.Z});
                sim.State.Npcs.Add(new NpcState{Id="theme_goer",Kind="Wook",X=player.X,Z=player.Z+1,Yaw=180,CanTalk=true});
                yield return new WaitForSeconds(.8f);
                Check(hud,failures,"festival","Pick up Tongue Stamps","TONGUE STAMPS","Pick up Tongue Stamps","Offer Tongue Stamps");

                // Then a shared stash holding Fun Guys, and the friend beside you offering Fun Guys again.
                sim.State.Stashes.Add(new StashState{Id="theme_stash",X=player.X,Z=player.Z,Items=new List<ItemStack>{new ItemStack{ItemId="stock_mushrooms",Count=1}}});
                mate.X=player.X+1;mate.Z=player.Z;
                sim.State.Transfers.Add(new TransferOffer{Id="theme_offer",FromId=mate.Id,ToId=player.Id,ItemId="stock_mushrooms",Amount=1,ExpiresAt=sim.State.SimulationSeconds+600});
                yield return new WaitForSeconds(.8f);
                Check(hud,failures,"festival crew","Accept Fun Guys ×1",null,"Accept Fun Guys ×1","Deposit Tongue Stamps","Withdraw Fun Guys","Offer Tongue Stamps to Mate");
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

        // A dropped item floats a world-space name tag that every player reads from 1.1 m to 7 m away. The tag must show the
        // display name while the drop keeps its own model (the id picks the model; the display name would give a grey cube).
        [UnityTest]public IEnumerator DroppedTongueStampsFloatTheirDisplayName()
        {
            var world=new GameObject("Theme drop world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Theme drop HUD");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=3});
                yield return StartLevel(session);

                // Drop a Tongue Stamp (G), then step 3 m away, where its tag shows.
                sim.State.Npcs.Clear();
                session.Command("Drop",item:"stock_lsd");
                yield return new WaitForSeconds(.4f);
                Assert.That(sim.State.Drops.Exists(d=>d.ItemId=="stock_lsd"),"dropped a Tongue Stamp: "+session.Message);
                player.X+=3;
                yield return new WaitForSeconds(1f);
                TextMesh tag=null;
                foreach(Transform actor in GameObject.Find("Authoritative actor presentation").transform)
                    if(actor.gameObject.activeSelf&&actor.name.StartsWith("FestivalStockPrism"))tag=actor.Find("Label/Text").GetComponent<TextMesh>();
                if(tag==null)failures.Add("no dropped stamp with the FestivalStockPrism model");
                else if(!tag.gameObject.activeInHierarchy||tag.text!="Tongue Stamps")failures.Add("the dropped stamp's tag reads \""+tag.text+"\" (shown: "+tag.gameObject.activeInHierarchy+"), expected \"Tongue Stamps\"");
                Check(hud,failures,"dropped stamp",null,null);
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"text a player reads around a dropped stamp:\n"+string.Join("\n",failures));
        }

        // The native two-client smoke checks each side's handoff button with DevelopmentSmoke.ShowsHandoffAction. THEME-1 made
        // the Accept label read the item's display name, so this holds that check to the buttons the HUD really builds.
        [UnityTest]public IEnumerator SmokeFindsTheHandoffButtonsTheHudShows()
        {
            var world=new GameObject("Smoke handoff world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Smoke handoff HUD");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("smoke_mate","Mate");mate.X=player.X+1;mate.Z=player.Z;

                // The smoke's client receives a merch bag...
                sim.State.Transfers.Add(new TransferOffer{Id="smoke_in",FromId=mate.Id,ToId=player.Id,ItemId="merch_bag",Amount=1,ExpiresAt=sim.State.SimulationSeconds+600});
                yield return new WaitForSeconds(.6f);
                Expect(hud,failures,"client, receiving",false);

                // ...and its host, who offered it, can cancel.
                sim.State.Transfers.Clear();
                sim.State.Transfers.Add(new TransferOffer{Id="smoke_out",FromId=player.Id,ToId=mate.Id,ItemId="merch_bag",Amount=1,ExpiresAt=sim.State.SimulationSeconds+600});
                yield return new WaitForSeconds(.6f);
                Expect(hud,failures,"host, sending",true);
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"handoff buttons the native smoke looks for:\n"+string.Join("\n",failures));
        }

        private static void Expect(GameObject hud,List<string> failures,string who,bool isHost)
        {
            if(DevelopmentSmoke.ShowsHandoffAction(hud.transform,isHost))return;
            var names=new List<string>();
            foreach(var button in hud.GetComponentsInChildren<Button>(true))if(button.gameObject.activeSelf&&button.name.StartsWith("Action:"))names.Add(button.name);
            failures.Add(who+": the smoke finds no handoff button among ["+string.Join(" / ",names)+"]");
        }

        // Records every expected label the HUD is missing and every real-drug word in any text it shows, including the
        // world-space tags (drops, players, shop prices) that float in the scene.
        private static void Check(GameObject hud,List<string> failures,string where,string prompt,string vitals,params string[] actions)
        {
            var shown=new List<string>();var labels=new List<string>();
            foreach(var text in hud.GetComponentsInChildren<Text>(false))if(!string.IsNullOrEmpty(text.text))shown.Add(text.name+": "+text.text.Replace("\n"," | "));
            foreach(var button in hud.GetComponentsInChildren<Button>(true))if(button.name.StartsWith("Action:")&&button.gameObject.activeSelf){var label=button.GetComponentInChildren<Text>(true).text;labels.Add(label);shown.Add("Action: "+label);}
            foreach(var mesh in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))if(!string.IsNullOrEmpty(mesh.text))shown.Add("World "+mesh.transform.parent?.parent?.name+": "+mesh.text.Replace("\n"," | "));
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "";}
            if(prompt!=null&&Read("Prompt")!=prompt)failures.Add(where+": prompt reads \""+Read("Prompt")+"\", expected \""+prompt+"\"");
            if(vitals!=null&&!Read("Vitals").Contains(vitals))failures.Add(where+": vitals read \""+Read("Vitals").Replace("\n"," | ")+"\", expected "+vitals);
            foreach(var action in actions)if(!labels.Contains(action))failures.Add(where+": no action \""+action+"\" among ["+string.Join(" / ",labels)+"]");
            foreach(var line in shown)foreach(var word in Banned)if(line.ToLowerInvariant().Contains(word)){failures.Add(where+": real-drug word '"+word+"' in "+line);break;}
        }
    }
}
