using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class DeckSetSessionTests
    {
        static int sequence;
        static CommandResult Act(FestivalSimulation game,string kind,string id="p0")=>game.Execute(id,new GameCommand{Id="deck_wire_"+(sequence++),Kind=kind});
        static FestivalSimulation Deck(out InteractionState deck,int level=0)
        {
            var game=new FestivalSimulation(3);var rider=game.AddPlayer("p0","Rider");game.AddPlayer("p1","Friend");
            game.State.UnlockedFestivalCount=Festivals.Count;game.State.FestivalIndex=1;game.State.LevelIndex=level;game.State.Phase="Playing";game.State.ElapsedSeconds=10;game.State.Npcs.Clear();
            var at=Festivals.ArtCarAt(0,10);rider.X=at.X;rider.Z=at.Z;
            game.State.Npcs.Add(new NpcState{Id="fan",X=rider.X+2,Z=rider.Z,Yaw=90});
            Assert.That(Act(game,"RideCar").Accepted,Is.True,"setup: own plain ride");Assert.That(Act(game,"DeckSet").Accepted,Is.True,"setup: start deck");deck=game.Interaction(rider.InteractionId);return game;
        }
        static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        static void Settle(FestivalSimulation game,InteractionState deck)
        {
            var edges=new List<RhythmInput>();foreach(var note in RhythmChart.For(deck).Notes){edges.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});if(note.HoldSeconds>0)edges.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds+note.HoldSeconds,Release=true});}
            edges.Sort((a,b)=>a.TimeSeconds.CompareTo(b.TimeSeconds));foreach(var edge in edges){game.State.SimulationSeconds=deck.StartSeconds+edge.TimeSeconds;Assert.That(game.Execute(deck.PlayerId,new GameCommand{Id="deck_edge_"+(sequence++),Kind="Rhythm",Direction=edge.Direction,TimeSeconds=edge.TimeSeconds,Amount=edge.Release?1:0}).Accepted,Is.True);}
            game.State.SimulationSeconds=deck.StartSeconds+deck.DurationSeconds+.75;game.Tick(.1);Assert.That(deck.Score,Is.EqualTo(1));
        }
        [Test]public void TheOwnerGetsTheGridAndFriendsKeepTheLiteralDeckPose()
        {
            var game=Deck(out var deck,1);var mine=Wire(game,"p0");var theirs=Wire(game,"p1");
            Assert.That(mine.Interactions.Count,Is.EqualTo(1));Assert.That(mine.Interactions[0].StageTrack,Is.EqualTo(4),"the owner's actual serialized deck uses Ember's night grid");
            Assert.That(JsonUtility.ToJson(mine.Interactions[0]),Is.EqualTo(JsonUtility.ToJson(deck)),"all heads, tails, chart anchors and inputs travel through the actual owner whitelist");
            Assert.That(theirs.Interactions,Is.Empty,"private chart/input logs never travel to a friend");Assert.That(theirs.Players.Find(p=>p.Id=="p0").VisualPose,Is.EqualTo("DeckSet"));
            Assert.That(FestivalSimulation.ArtCarOf(mine,"p0"),Is.EqualTo(0));Assert.That(FestivalSimulation.Dancing(deck),Is.False);
        }
        [Test]public void SuccessfulTipReceiptsAreOwnerOnlyAndNeverPublicSpeech()
        {
            var game=Deck(out var deck);Settle(game,deck);var mine=Wire(game,"p0");var theirs=Wire(game,"p1");
            var owner=mine.Players.Find(p=>p.Id=="p0");var remote=theirs.Players.Find(p=>p.Id=="p0");
            Assert.That(owner.NpcSpeech,Does.Contain(Festivals.CurrencyName(1,0,2)),"round(.75*1*crew2)=2 buttons appears only for the paid rider");
            Assert.That(owner.NpcSpeaker,Is.EqualTo("FESTIVALGOERS"));Assert.That(remote.NpcSpeech,Is.Empty);Assert.That(mine.PublicSpeech,Is.Empty);Assert.That(theirs.PublicSpeech,Is.Empty);
            Assert.That(JsonUtility.ToJson(remote),Does.Not.Contain(deck.Id),"even receipt identity stays private");
            Assert.That(JsonUtility.ToJson(owner),Does.Contain("\"DeckTipDeliveryId\":\""+deck.Id+":tips\""),"stable owner receipt identity supports the existing private presentation adapter");
            foreach(var view in new[]{mine,theirs})Assert.That(JsonUtility.ToJson(view.Npcs[0]),Does.Contain("\"Tipped\":false"),"no recipient learns the host's tip flag");
            Assert.That(owner.Cash,Is.EqualTo(22));Assert.That(remote.Cash,Is.Zero);Assert.That(mine.LevelSales,Is.EqualTo(2));Assert.That(mine.GrossSales,Is.EqualTo(2));
        }
        [Test]public void NpcTipFlagsNeverLeaveAnyLivingView()
        {
            var game=Deck(out var deck);Settle(game,deck);Assert.That(game.State.Npcs[0].Tipped,Is.True,"setup: actual settlement marked the host's fan");
            foreach(string viewer in new[]{"p0","p1"})Assert.That(Wire(game,viewer).Npcs[0].Tipped,Is.False,"host flags are omitted even from the paying owner's actual JSON view");
        }
        [Test]public void HostJsonKeepsTipsAndOldJsonDefaultsUntipped()
        {
            var game=Deck(out var deck);Settle(game,deck);string host=JsonUtility.ToJson(game.State);
            Assert.That(host,Does.Contain("\"Tipped\":true"),"the persisted authoritative NPC owns the once-per-level flag");
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(host));Assert.That(JsonUtility.ToJson(restored.State.Npcs[0]),Does.Contain("\"Tipped\":true"));
            Assert.That(Act(restored,"DeckSet").Accepted,Is.True);Settle(restored,restored.Interaction(restored.Player("p0").InteractionId));Assert.That(restored.State.LevelSales,Is.EqualTo(2),"saved tips cannot pay again");
            var old=Deck(out _);string legacy=JsonUtility.ToJson(old.State).Replace("\"Tipped\":false,","");restored.Restore(JsonUtility.FromJson<RoundState>(legacy));Assert.That(JsonUtility.ToJson(restored.State.Npcs[0]),Does.Contain("\"Tipped\":false"));
            Settle(restored,restored.Interaction(restored.Player("p0").InteractionId));Assert.That(restored.State.LevelSales,Is.EqualTo(2),"missing field restores an eligible fan");
        }
        [Test]public void SavedDeckWithLostOwnershipIsRetiredWithoutReplacingAnotherTask()
        {
            var game=Deck(out var deck);game.Player("p0").InteractionId="other";var other=new InteractionState{Id="other",Kind="LostProperty",PlayerId="p0",DurationSeconds=100};game.State.Interactions.Add(other);
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State)));
            Assert.That(restored.Interaction(deck.Id).Status,Is.Not.EqualTo("Active"));Assert.That(restored.Player("p0").InteractionId,Is.EqualTo(other.Id),"retiring an orphan never cancels the actual current task");Assert.That(restored.State.LevelSales,Is.Zero);
        }
        [Test]public void ReceiptsRemainPrivateForCrewAndSpiritViews()
        {
            var game=Deck(out var deck);Settle(game,deck);var owner=game.Player("p0");var friend=game.Player("p1");
            var at=Festivals.ArtCarAt(1,game.State.ElapsedSeconds);friend.X=at.X;friend.Z=at.Z;game.State.Npcs.Add(new NpcState{Id="other_fan",X=at.X+2,Z=at.Z,Yaw=90});
            Assert.That(Act(game,"RideCar",friend.Id).Accepted,Is.True);Assert.That(Act(game,"DeckSet",friend.Id).Accepted,Is.True);Settle(game,game.Interaction(friend.InteractionId));Assert.That(friend.DeckTipAmount,Is.GreaterThan(0),"setup: the other player also earned a real receipt on another car");friend.Life="Spirit";
            var spirit=Wire(game,"p1");Assert.That(spirit.Players.Exists(p=>p.Id==owner.Id),Is.False);Assert.That(JsonUtility.ToJson(spirit),Does.Not.Contain(owner.DeckTipDeliveryId));Assert.That(spirit.Npcs,Is.Empty);
            owner.Life="Spirit";var own=Wire(game,owner.Id);Assert.That(own.Players.Find(p=>p.Id==owner.Id).DeckTipDeliveryId,Is.EqualTo(owner.DeckTipDeliveryId),"the owner alone retains their private receipt after downing/death");
            Assert.That(own.Players.Find(p=>p.Id=="p1").DeckTipAmount,Is.Zero);Assert.That(own.PublicSpeech,Is.Empty);
        }
        [Test]public void PhysicalBoothPauseKeepsReceiptAgeThenExpiryResumes()
        {
            var game=Deck(out var deck);Settle(game,deck);var player=game.Player("p0");Assert.That(Act(game,"Cancel").Accepted,Is.True,"setup: hop off the completed plain ride");
            game.State.Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=0};var at=FestivalSimulation.BoothAt(game.State);foreach(var p in game.State.Players){p.X=at.X;p.Z=at.Z;}
            for(int k=0;k<35&&game.State.Phase=="Playing";k++)game.Tick(.1);Assert.That(game.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"physical all-player ring starts the actual game");
            double started=player.DeckTipAtWorldSeconds,until=player.DeckTipUntilWorldSeconds,world=FestivalSimulation.WorldSeconds(game.State),age=world-started;
            Assert.That(age,Is.EqualTo(3).Within(.000001));game.Tick(6);Assert.That(FestivalSimulation.WorldSeconds(game.State)-player.DeckTipAtWorldSeconds,Is.EqualTo(age).Within(1e-6));Assert.That(Wire(game,"p0").Players.Find(p=>p.Id=="p0").NpcSpeech,Is.Not.Empty,"the held receipt remains alive past seven simulation seconds");var held=Wire(game,"p0");Assert.That(FestivalSimulation.SpeechRemainingSeconds(held,held.Players.Find(p=>p.Id=="p0")),Is.EqualTo(until-world).Within(1e-6),"owner compatibility lifetime uses the same held world age");
            var call=game.Execute(FestivalSimulation.CupsTurn(game.State),new GameCommand{Id="deck_call_"+(sequence++),Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.CallCups});Assert.That(call.Accepted,Is.True,call.Reason);
            for(int k=0;k<45&&game.State.Phase==FestivalSimulation.MinigamePhase;k++)game.Tick(.1);Assert.That(game.State.Phase,Is.EqualTo("Playing"));
            Assert.That(player.DeckTipAtWorldSeconds,Is.EqualTo(started));Assert.That(player.DeckTipUntilWorldSeconds,Is.EqualTo(until));Assert.That(FestivalSimulation.WorldSeconds(game.State),Is.EqualTo(world).Within(1e-6));
            Assert.That(Wire(game,"p0").Players.Find(p=>p.Id=="p0").NpcSpeech,Is.Not.Empty);game.Tick(until-world+.1);Assert.That(Wire(game,"p0").Players.Find(p=>p.Id=="p0").NpcSpeech,Is.Empty,"expiry runs on resumed world time");Assert.That(player.Cash,Is.EqualTo(22),"pause/resume never repeats the settlement");
        }
        [Test]public void PublicSpeechIdentityAndLifetimeKeepTheirOwnProjection()
        {
            var game=Deck(out var deck);Settle(game,deck);var rider=game.Player("p0");double now=FestivalSimulation.WorldSeconds(game.State);
            Assert.That(Act(game,"Cancel").Accepted,Is.True,"setup: hop off to chat");game.State.Npcs[0].CanTalk=true;
            Assert.That(game.Execute(rider.Id,new GameCommand{Id="deck_public_"+(sequence++),Kind="Talk",TargetId="fan"}).Accepted,Is.True,"real accepted Talk creates public speech in the same world instant as the tip");
            var speech=game.State.PublicSpeech[game.State.PublicSpeech.Count-1];Assert.That(speech.StartedAtWorldSeconds,Is.EqualTo(now));
            var view=Wire(game,rider.Id);var projected=view.Players.Find(p=>p.Id==rider.Id);Assert.That(projected.SpeechDeliveryId,Is.EqualTo(speech.DeliveryId));Assert.That(projected.SpeechSequence,Is.EqualTo(speech.Sequence));Assert.That(projected.NpcSpeech,Is.EqualTo(speech.Text),"separate private receipt never relabels a retained public delivery");Assert.That(projected.SpeechUntilWorldSeconds,Is.EqualTo(speech.ExpiresAtWorldSeconds));Assert.That(projected.DeckTipAmount,Is.EqualTo(2));
            game.Tick(7.1);Assert.That(Wire(game,rider.Id).Players.Find(p=>p.Id==rider.Id).NpcSpeech,Is.EqualTo(speech.Text),"an expired receipt cannot overwrite retained public speech");
        }
        [UnityTest]public IEnumerator ActualHostSessionCalibratesBothDeckEdgesOnce()
        {
            yield return new EnterPlayMode();string previous=LocalProfile.Folder,scope=FestivalInput.PreferencesScope;string folder=Path.Combine(Path.GetTempPath(),"impl-U26-deck-input-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);LocalProfile.Folder=folder;FestivalInput.PreferencesScope=folder;
            var go=new GameObject("impl-U26 deck host");
            try
            {
                var session=go.AddComponent<FestivalSession>();session.enabled=false;session.Host("Deck test",8611);Assert.That(session.Connected&&session.IsHost,Is.True,session.Message);
                var game=Deck(out var deck,1);var note=RhythmChart.For(deck).Notes.Find(n=>n.HoldSeconds>0);Assert.That(note,Is.Not.Null,"setup: this fixed real chart contains a hold");
                typeof(FestivalSession).GetField("simulation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,game);typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,"p0");typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(game,"p0"));session.Profile.Data.TimingCalibrationMs=60;
                double anchor=deck.StageFirstHeadWorldSeconds;game.State.SimulationSeconds=deck.StartSeconds+note.TimeSeconds+.06;session.Rhythm(note.Direction,note.TimeSeconds+.06);
                game.State.SimulationSeconds=deck.StartSeconds+note.TimeSeconds+note.HoldSeconds+.06;session.Rhythm(note.Direction,note.TimeSeconds+note.HoldSeconds+.06,true);
                Assert.That(deck.Inputs.Count,Is.EqualTo(2),session.Message);Assert.That(deck.Inputs[0].TimeSeconds,Is.EqualTo(note.TimeSeconds).Within(1e-9));Assert.That(deck.Inputs[1].TimeSeconds,Is.EqualTo(note.TimeSeconds+note.HoldSeconds).Within(1e-9));Assert.That(deck.Inputs[1].Release,Is.True);Assert.That(deck.StageFirstHeadWorldSeconds,Is.EqualTo(anchor));
                Assert.That(Wire(game,"p0").Interactions[0].Inputs.Count,Is.EqualTo(2));Assert.That(Wire(game,"p1").Interactions,Is.Empty);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);LocalProfile.Folder=previous;FestivalInput.PreferencesScope=scope;if(Directory.Exists(folder))Directory.Delete(folder,true);}
            yield return new ExitPlayMode();
        }
    }
}
