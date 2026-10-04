using System;
using System.Collections;
using System.Linq;
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
    // These consumers must finish untimed checks through their shipped callbacks and coroutines.
    public sealed class ConfirmationFinishPlayTests:FestivalPlayModeTest
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [UnityTest]public IEnumerator TrippersMenuButtonFinishesTheCheck()=>FinishCheck(false,false);
        [UnityTest]public IEnumerator TrippersActionCallbackFinishesTheCheck()=>FinishCheck(false,true);
        [UnityTest]public IEnumerator VipMenuButtonFinishesTheRopeChat()=>FinishCheck(true,false);
        [UnityTest]public IEnumerator VipActionCallbackFinishesTheRopeChat()=>FinishCheck(true,true);

        IEnumerator FinishCheck(bool vip,bool action)
        {
            var world=new GameObject("jfix:CP01#1 confirmation world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("jfix:CP01#1 confirmation HUD");var session=holder.AddComponent<FestivalSession>();var hud=holder.AddComponent<FestivalHud>();
            yield return null;
            try
            {
                session.Host("Journey checker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);var peer=Simulation(session).AddPlayer("cp01_peer","Peer");peer.Ready=true;yield return StartLevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
                Prepare(game,player,vip,false);yield return new WaitForSeconds(.4f);
                session.Command("ConfirmChat","cp01_target");
                yield return new WaitForSeconds(.4f);
                var running=game.Interaction(player.InteractionId);Assert.That(running,Is.Not.Null,session.Message);Assert.That(running.Kind,Is.EqualTo("ConfirmChat"));
                var window=(FestivalChatWindow)typeof(FestivalHud).GetField("chatWindow",Private).GetValue(hud);
                Assert.That(window.Open,Is.True,"setup: the actual HUD follows the private check");
                if(!vip)
                {
                    Assert.That(window.Replies,Is.EqualTo(3));
                    for(int question=0;question<3;question++)Assert.That(window.Pick(question),Is.True,"the shipped callback answers question "+question);
                }
                game.Tick(60);yield return new WaitForSeconds(.3f);
                Assert.That(running.Status,Is.EqualTo("Active"),"setup: sixty seconds and all questions do not expire the untimed check");
                Assert.That(FestivalSession.ViewFor(game,"cp01_peer").Interactions,Is.Empty,"the peer receives no private check");
                Assert.That(FestivalSession.ViewFor(game,"cp01_peer").Visions,Is.Empty,"the peer receives no private vision");
                if(action)
                {
                    var callback=holder.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>(b.name=="Action:Cancel current action"||b.name=="Action:Finish chat check")&&b.gameObject.activeSelf);
                    Assert.That(callback,Is.Not.Null,"setup: shipped action callback exists");callback.onClick.Invoke();
                }
                else
                {
                    session.MenuOpen=true;yield return new WaitForSeconds(.3f);
                    var button=(GameObject)typeof(FestivalHud).GetField("cancelButton",Private).GetValue(hud);
                    Assert.That(button.activeInHierarchy,Is.True,"setup: shipped menu button is visible");button.GetComponent<Button>().onClick.Invoke();
                }
                yield return new WaitForSeconds(.3f);
                string status=running.Status;bool confirmed=game.State.Visions.Exists(v=>v.Id=="cp01_vision"&&v.Confirmed);
                bool band=player.Inventory.Exists(i=>i.ItemId==FestivalSimulation.VipWristband&&i.Count>0);
                TestContext.WriteLine("jfix:CP01#1 HUD_CALLBACK vip="+vip+" control="+(action?"action":"menu")+" status="+status+" confirmed="+confirmed+" wristband="+band+" interactionCleared="+(player.InteractionId==""));
                Assert.That(status=="Complete"&&(vip?band:confirmed),Is.True,"the shipped HUD finish control must complete ConfirmChat and grant its outcome; actual status="+status+" confirmed="+confirmed+" wristband="+band);
                Assert.That(player.InteractionId,Is.Empty);
                int bands=player.Inventory.Where(i=>i.ItemId==FestivalSimulation.VipWristband).Sum(i=>i.Count);
                session.Command("EndChat",running.Id);yield return new WaitForSeconds(.2f);
                Assert.That(player.Inventory.Where(i=>i.ItemId==FestivalSimulation.VipWristband).Sum(i=>i.Count),Is.EqualTo(bands),"a stale finish cannot duplicate the reward");
                Assert.That(FestivalSession.ViewFor(game,"cp01_peer").Visions,Is.Empty,"completion keeps the private truth private");

                // The same controls still cancel an ordinary sale and refund its reserved stock.
                session.MenuOpen=false;var target=game.State.Npcs[0];target.Twist="";target.Role="Buyer";target.Mode="Idle";
                player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});int salesBefore=game.State.LevelSales;
                session.Command("StartSale",target.Id,"stock_lsd");yield return new WaitForSeconds(.4f);
                var sale=game.Interaction(player.InteractionId);Assert.That(sale,Is.Not.Null,session.Message);Assert.That(sale.Kind,Is.EqualTo("Sale"));
                Assert.That(player.Inventory.Where(i=>i.ItemId=="stock_lsd").Sum(i=>i.Count),Is.EqualTo(0),"the actual sale reserved its stock");
                if(action)
                {
                    var cancel=holder.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="Action:Cancel current action"&&b.gameObject.activeSelf);
                    Assert.That(cancel,Is.Not.Null,"an ordinary action retains its cancel control");cancel.onClick.Invoke();
                }
                else
                {
                    session.MenuOpen=true;yield return new WaitForSeconds(.3f);
                    ((GameObject)typeof(FestivalHud).GetField("cancelButton",Private).GetValue(hud)).GetComponent<Button>().onClick.Invoke();
                }
                yield return new WaitForSeconds(.3f);
                Assert.That(sale.Status,Is.EqualTo("Cancelled"));
                Assert.That(player.Inventory.Where(i=>i.ItemId=="stock_lsd").Sum(i=>i.Count),Is.EqualTo(1),"ordinary cancellation still refunds the reserved stock");
                Assert.That(game.State.LevelSales,Is.EqualTo(salesBefore),"ordinary cancellation cannot pay a sale");
            }
            finally{session.Leave();UnityEngine.Object.Destroy(holder);UnityEngine.Object.Destroy(world);}
        }

        [UnityTest]public IEnumerator ActualSmokeDayCoroutineCompletesItsBuyerCheck()=>SmokeCheck(false);
        [UnityTest]public IEnumerator ActualSmokeNightCoroutineCompletesItsFirstClue()=>SmokeCheck(true);
        IEnumerator SmokeCheck(bool night)
        {
            var world=new GameObject("jfix:CP01#1 smoke world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("jfix:CP01#1 smoke session");var session=holder.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Journey smoke checker",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);var peer=Simulation(session).AddPlayer("cp01_peer","Peer");peer.Ready=true;yield return StartLevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);Prepare(game,player,false,night);
                if(!night)game.State.LevelSales=FestivalSimulation.DayQuota(game.State);
                player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});yield return new WaitForSeconds(.3f);
                var smoke=holder.AddComponent<DevelopmentSmoke>();smoke.enabled=false;
                var method=typeof(DevelopmentSmoke).GetMethod(night?"FollowTrail":"WorkTheDay",Private);
                float deadlineAt=Time.realtimeSinceStartup+5f;
                var routine=(IEnumerator)method.Invoke(smoke,night?new object[]{session,deadlineAt}:new object[]{session,"cp01_target",null,deadlineAt});
                yield return routine;
                var finished=(InteractionState)typeof(DevelopmentSmoke).GetField("finished",Private).GetValue(smoke);
                string failed=(string)typeof(DevelopmentSmoke).GetField("failed",Private).GetValue(smoke);
                var check=game.State.Interactions.LastOrDefault(i=>i.PlayerId==player.Id&&i.Kind=="ConfirmChat");
                Assert.That(check,Is.Not.Null,"setup: the real helper reached ConfirmChat");
                bool confirmed=game.State.Visions[0].Confirmed;int clues=game.State.CluesRead;
                TestContext.WriteLine("jfix:CP01#1 SMOKE_CONSUMER method="+method.Name+" helperFinished="+(finished!=null)+" failure="+failed+" status="+check.Status+" confirmed="+confirmed+" cluesRead="+clues);
                Assert.That(finished!=null&&failed==null&&confirmed&&(!night||clues>0),Is.True,"the actual smoke consumer must complete its untimed check; actual helperFinished="+(finished!=null)+" failure="+failed+" confirmed="+confirmed+" cluesRead="+clues);
                Assert.That(check.Status,Is.EqualTo("Complete"));
                Assert.That(player.InteractionId,Is.Empty);
            }
            finally{session.Leave();UnityEngine.Object.Destroy(holder);UnityEngine.Object.Destroy(world);}
        }
        static void Prepare(FestivalSimulation game,PlayerState player,bool vip,bool night)
        {
            game.State.Npcs.Clear();game.State.Visions.Clear();game.State.TripperId=player.Id;game.State.DurationSeconds=10000;
            player.X=0;player.Z=0;player.Life="Alive";player.Effects.Clear();player.Inventory.Clear();player.EquippedItemId="";
            // Keep the assigned tripper valid across the real host steps while the check stays untimed.
            player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=10000});game.State.Doses.Clear();game.State.Doses.Add(new PlayerDose{PlayerId=player.Id,Dose=1});
            Assert.That(game.Player("cp01_peer"),Is.Not.Null,"setup: peer joined before round launch");game.Player("cp01_peer").X=30;
            var target=new NpcState{Id="cp01_target",Kind="Wook",Role=night?"ClueHolder":"Buyer",CanTalk=true,X=0,Z=1.5f,Yaw=180,Twist=vip?FestivalSimulation.VipGuard:""};
            if(night){game.State.LevelIndex=1;target.Z=1;game.State.ClueChain.Clear();game.State.ClueChain.Add(target.Id);game.State.CluesRead=0;game.State.GateOpened=false;}
            game.State.Npcs.Add(target);
            if(!vip)game.State.Visions.Add(new VisionState{Id="cp01_vision",NpcId=target.Id,Kind=night?"Clue":"Buyer",IsTrue=true});
        }
    }
}
