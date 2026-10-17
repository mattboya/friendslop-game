using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    public sealed class DevelopmentFestivalHostTests:FestivalPlayModeTest
    {
        const string PickEmber="F  switch to Ember Playa (test build: all festivals)";

        [UnityTest] public IEnumerator ActualDevelopmentHostEnablesSelectionWithoutEarningUnlocks()
        {
            var root=new GameObject("DEV-1 actual host");
            var session=root.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Picker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+20;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"actual Host creates a local player: "+session.Message);
                var simulation=Simulation(session);
                Assert.That(simulation.State.UnlockedFestivalCount,Is.EqualTo(1),"owned fresh profile has earned only Palm Mirage");
                Assert.That(simulation.DevelopmentFestivalSelection,Is.True,"DEV-1: actual Editor/development Host enables the session-only picker");
                Assert.That(session.DevelopmentFestivalSelection,Is.True,"the local host reads its own capability");
                var selected=simulation.Execute(session.LocalPlayerId,new GameCommand{Id="dev-host-pick",Kind="ChooseFestival",Amount=1});
                Assert.That(selected.Accepted,Is.True,selected.Reason);
                session.Profile.RememberUnlocks(simulation.State);
                Assert.That(session.Profile.Data.UnlockedFestivalCount,Is.EqualTo(1),"development selection never saves an earned unlock");
            }
            finally{session.Leave();Object.Destroy(root);}
            yield return null;
        }

        // The fresh host reads the test-build hint and F picks locked Ember Playa, but only where F means nothing else: the open
        // menu keeps it, a tent's antic and Marigold's talk come first.
        [UnityTest] public IEnumerator FreshHostPicksEmberPlayaWithFAfterMenuTentAndMarigold()
        {
            UseAVirtualKeyboard("DEV-1 picker keyboard");
            var world=new GameObject("DEV-1 picker world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("DEV-1 picker HUD");var session=root.AddComponent<FestivalSession>();root.AddComponent<FestivalHud>();yield return null;
            string Read(string name){foreach(var text in root.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            try
            {
                session.Host("Picker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                player.X=0;player.Z=0;yield return new WaitForSeconds(.4f);
                Assert.That(Read("Objective detail"),Is.EqualTo(PickEmber+" • One starter each at camp: Tongue Stamps or Fun Guys for $5. Buy more from festivalgoers ($5 each; some are narcs). Keep some money for more stock."));
                Assert.That(Read("Level banner"),Is.EqualTo("UP NEXT  •  PALM MIRAGE  •  DAY 1"));

                session.MenuOpen=true;yield return Tap(Key.F,.3f);
                Assert.That(sim.State.FestivalIndex,Is.Zero,"the open menu keeps F");
                session.MenuOpen=false;yield return null;

                var tent=CampFeatures.Find("tent_1");player.X=tent.X;player.Z=tent.Z;session.Command("EnterCamp",tent.Id);yield return new WaitForSeconds(.2f);
                Assert.That(player.CampVisitId,Is.EqualTo(tent.Id),"setup: inside the tent: "+session.Message);
                int antics=player.CampAntics;yield return Tap(Key.F,.3f);
                Assert.That(player.CampAntics,Is.EqualTo(antics+1),"inside a tent F plays its antic");
                Assert.That(sim.State.FestivalIndex,Is.Zero,"inside a tent F never switches the festival");
                session.Command("ExitCamp");yield return new WaitForSeconds(.2f);
                Assert.That(player.CampVisitId,Is.Empty,"setup: out of the tent");

                var window=root.GetComponentInChildren<FestivalChatWindow>(true);
                player.X=1.8f;player.Z=6;yield return new WaitForSeconds(.4f);
                yield return Tap(Key.F,.3f);
                Assert.That(window.Open,Is.True,"beside Marigold F talks to her");
                Assert.That(sim.State.FestivalIndex,Is.Zero,"beside Marigold F never switches the festival");
                yield return Tap(Key.Escape,.5f);
                Assert.That(window.Open,Is.False,"setup: her talk is closed");

                player.X=0;player.Z=0;yield return new WaitForSeconds(.4f);
                yield return Tap(Key.F,.3f);
                Assert.That(sim.State.FestivalIndex,Is.EqualTo(1),"F picks locked Ember Playa: "+session.Message);
                Assert.That(sim.State.UnlockedFestivalCount,Is.EqualTo(1),"the pick earns nothing");
                yield return new WaitForSeconds(.3f);
                Assert.That(Read("Level banner"),Is.EqualTo("UP NEXT  •  EMBER PLAYA  •  DAY 1"));
                Assert.That(Read("Objective detail"),Does.StartWith("F  switch to Palm Mirage (test build: all festivals) • "));
                yield return Tap(Key.F,.3f);
                Assert.That(sim.State.FestivalIndex,Is.Zero,"F wraps round to Palm Mirage");
            }
            finally{session.Leave();Object.Destroy(root);Object.Destroy(world);}
            yield return null;
        }

        // Clearing a test-picked Night 2, going back to camp, failing a level and hosting again from the reloaded profile never
        // saves an unlock; an earned second festival stays earned and the test-build pick still comes with it.
        [UnityTest] public IEnumerator NightTwoCampFailureAndRehostKeepTheEarnedCount()
        {
            var world=new GameObject("DEV-1 lifecycle world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("DEV-1 lifecycle host");var session=root.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return Host(session);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                session.Command("ChooseFestival",amount:1);yield return new WaitForSeconds(.2f);
                Assert.That(sim.State.FestivalIndex,Is.EqualTo(1),"setup: the test host picks Ember Playa: "+session.Message);
                sim.State.LevelIndex=Festivals.LevelCount-1;
                yield return StartLevel(session);
                sim.State.FriendFound=true;sim.State.FriendLeaderId=player.Id;sim.State.FriendPosition=new WorldPoint(Festivals.CampGateX,Festivals.CampGateZ);
                player.X=Festivals.CampGateX;player.Z=Festivals.CampGateZ;yield return null;
                session.Command("Extract");
                for(float deadline=Time.realtimeSinceStartup+10;session.State.Phase!="Results"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(sim.State.Result,Is.EqualTo("Success"),"Night 2 ends through the real extraction: "+session.Message);
                yield return null;
                Assert.That(sim.State.UnlockedFestivalCount,Is.EqualTo(1),"a test-picked Night 2 earns no unlock");
                Assert.That(session.Profile.Data.UnlockedFestivalCount,Is.EqualTo(1),"the host saves no unlock at Results");

                session.Command("Reset");yield return new WaitForSeconds(.2f);
                Assert.That(sim.State.Phase,Is.EqualTo("CampReview"),"back at camp: "+session.Message);
                for(int award=0;award<sim.State.ReviewAwards.Count;award++)sim.Execute(player.Id,new GameCommand{Id="dev-vote-"+award,Kind="ReviewVote",TargetId=player.Id,Amount=award});
                session.Command("FinishReview");yield return new WaitForSeconds(.2f);
                Assert.That(sim.State.Phase,Is.EqualTo("Shopping"),"camp opens: "+session.Message);
                Assert.That(sim.DevelopmentFestivalSelection&&sim.State.UnlockedFestivalCount==1,Is.True,"camp keeps the pick and the earned count");
                Assert.That(FestivalHudText.FestivalChoice(session.State,session.LocalPlayerId,session.DevelopmentFestivalSelection),Does.Contain("test build"));

                session.Command("ChooseFestival",amount:1);yield return new WaitForSeconds(.2f);
                yield return StartLevel(session);
                sim.State.ElapsedSeconds=sim.State.DurationSeconds;
                for(float deadline=Time.realtimeSinceStartup+10;session.State.Phase!="Results"&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(sim.State.Result,Is.Not.EqualTo("Success"),"setup: the day fails");
                session.Command("Reset");yield return new WaitForSeconds(.2f);
                Assert.That(sim.State.FestivalIndex==1&&sim.State.LevelIndex==0&&sim.DevelopmentFestivalSelection,Is.True,"a failed test weekend restarts Ember Playa at Day 1 with the pick");
                session.Leave();Object.Destroy(root);yield return null;

                root=new GameObject("DEV-1 rehost");session=root.AddComponent<FestivalSession>();yield return null;
                Assert.That(session.Profile.Data.UnlockedFestivalCount,Is.EqualTo(1),"the reloaded profile still has only Palm Mirage");
                yield return Host(session);sim=Simulation(session);
                Assert.That(sim.DevelopmentFestivalSelection&&sim.State.UnlockedFestivalCount==1,Is.True,"rehosting enables the pick again from the earned count");
                Assert.That(sim.Execute(session.LocalPlayerId,new GameCommand{Id="dev-rehost-pick",Kind="ChooseFestival",Amount=1}).Accepted,Is.True);
                session.Profile.Data.UnlockedFestivalCount=2;Assert.That(session.Profile.Save(),Is.Empty);
                session.Leave();Object.Destroy(root);yield return null;

                root=new GameObject("DEV-1 earned host");session=root.AddComponent<FestivalSession>();yield return null;
                yield return Host(session);sim=Simulation(session);
                Assert.That(sim.State.UnlockedFestivalCount,Is.EqualTo(2),"an earned unlock still applies");
                Assert.That(sim.DevelopmentFestivalSelection,Is.True);
                Assert.That(FestivalHudText.FestivalChoice(session.State,session.LocalPlayerId,session.DevelopmentFestivalSelection),Is.EqualTo(PickEmber));
            }
            finally{session.Leave();Object.Destroy(root);Object.Destroy(world);}
            yield return null;
        }

        static IEnumerator Host(FestivalSession session)
        {
            session.Host("Picker",HostPort);
            for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
        }
    }
}
