using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class StockVisibilitySessionTests
    {
        static FestivalSimulation Scene(out PlayerState player,out NpcState cop)
        {
            var game=new FestivalSimulation(1);player=game.AddPlayer("owner","Owner");game.AddPlayer("friend","Friend");
            game.Player("friend").X=30;game.Player("friend").Z=30;game.State.Phase="Playing";game.State.Npcs.Clear();
            player.X=0;player.Z=5;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            cop=new NpcState{Id="cop_0",Kind="Cop",X=0,Z=0,Yaw=0};game.State.Npcs.Add(cop);
            game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);return game;
        }
        static RoundState Wire(FestivalSimulation game,string recipient)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,recipient)));
        static string Card(FestivalSimulation game){var view=Wire(game,"owner");return FestivalHudText.Condition(view,view.Players.Find(p=>p.Id=="owner"));}

        [Test] public void OwnerCardClearsWhenStockIsStowedAndPeersKeepNoPrivateEvidence()
        {
            var game=Scene(out var player,out var cop);game.Tick(.1);
            Assert.That(Card(game),Does.Not.Contain("SECURITY STOP"),"pocketed stock cannot show a stop on the owner's serialized card");
            Assert.That(game.Execute(player.Id,new GameCommand{Id="equip",Kind="Equip",ItemId="stock_lsd"}).Accepted,Is.True);game.Tick(.1);
            Assert.That(Card(game),Does.Contain("SECURITY STOP"),"equipped stock drives the actual card consumer");
            var peer=Wire(game,"friend");Assert.That(peer.Npcs[0].Mode,Is.EqualTo("Patrol"),"a peer's view still masks a cop stopping somebody else");
            Assert.That(peer.Npcs[0].TargetId,Is.Empty,"a peer never learns whom the cop stopped");
            Assert.That(peer.Npcs[0].Evidence,Is.Empty,"the host's evidence remains private");
            Assert.That(peer.Players.Find(p=>p.Id==player.Id).Inventory,Is.Empty,"another player's pocket contents remain private");
            Assert.That(game.Execute(player.Id,new GameCommand{Id="stow",Kind="Equip"}).Accepted,Is.True);game.Tick(.1);
            Assert.That(Card(game),Does.Not.Contain("SECURITY STOP"));Assert.That(cop.Evidence,Is.Empty);
            Assert.That(Wire(game,player.Id).Players.Find(p=>p.Id==player.Id).Inventory[0].Count,Is.EqualTo(1),"concealment does not consume the unit");
        }

        [Test] public void ReservedStockSurvivesJsonRestoreAndFeedsTheStopCard()
        {
            var game=Scene(out var player,out var cop);game.State.Npcs.Remove(cop);
            var buyer=new NpcState{Id="buyer",X=1,Z=5,Yaw=-90};game.State.Npcs.Add(buyer);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="sale",Kind="StartSale",TargetId=buyer.Id,ItemId="stock_lsd"}).Accepted,Is.True);
            game.State.Npcs.Add(cop);
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State)));
            restored.Tick(.1);Assert.That(Card(restored),Does.Contain("SECURITY STOP"),"an active last-unit reservation stays out after a saved round restores");
            var owner=restored.Player(player.Id);owner.Inventory.Add(new ItemStack{ItemId="merch_bag",Count=1});restored.Tick(.1);
            Assert.That(Card(restored),Does.Not.Contain("SECURITY STOP"),"a merch bag conceals the restored reservation");
            Assert.That(restored.Interaction(owner.InteractionId).ReservedItemId,Is.EqualTo("stock_lsd"),"the visibility change preserves the reserved unit");
        }

        [Test] public void VisibilityDiagnosticsRecordTransitionsWithoutPerTickSpam()
        {
            var game=Scene(out var player,out var cop);var events=new List<string>();
            game.ChatDiagnostic=(kind,reason,round,seconds)=>{if(kind=="StockVisibility")events.Add(reason);};
            player.EquippedItemId="stock_lsd";game.Tick(1);
            Assert.That(events,Is.EqualTo(new[]{"stop:cop_0:owner"}),"debug logging records a stop once");
            Assert.That(game.Execute(player.Id,new GameCommand{Id="stow_debug",Kind="Equip"}).Accepted,Is.True);game.Tick(1);
            Assert.That(events,Is.EqualTo(new[]{"stop:cop_0:owner","cleared:cop_0:owner"}),"debug logging records a concealed stop clearing once");
        }
    }
}
