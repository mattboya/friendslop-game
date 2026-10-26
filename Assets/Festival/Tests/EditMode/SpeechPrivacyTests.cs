using System;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;
using Festival.Network;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class SpeechPrivacyTests
    {
        static FestivalSimulation Pair(out PlayerState player,out NpcState npc)
        {
            var game=new FestivalSimulation(7);player=game.AddPlayer("owner","Owner");game.AddPlayer("friend","Friend");game.AddPlayer("ghost","Ghost").Life="Spirit";
            game.State.Phase="Playing";game.State.SimulationSeconds=5;game.State.Npcs.Clear();
            npc=new NpcState{Id="goer_0",Role="Regular",CanTalk=true,Z=1.5f,Yaw=180};game.State.Npcs.Add(npc);player.X=0;player.Z=0;
            Assert.That(DialogueGrammar.PersonaFor(npc.Id,npc.Twist),Is.EqualTo("DraggedAlongDad"));
            game.HasLineOfSight=(ax,az,bx,bz)=>false;game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);return game;
        }
        static void Act(FestivalSimulation game,PlayerState player,string id,string kind,string target="",int amount=0,int direction=0)
        {
            var result=game.Execute(player.Id,new GameCommand{Id=id,Kind=kind,TargetId=target,Amount=amount,Direction=direction});Assert.That(result.Accepted,Is.True,result.Reason);
        }
        static RoundState View(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        static List<string> Finish(FestivalSimulation game,PlayerState player,NpcState npc)
        {
            Act(game,player,"start","Conversation",npc.Id);var talk=game.Interaction(player.InteractionId);var spoken=new List<string>();
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++){spoken.Add(talk.Turns[turn].Line);spoken.Add(talk.Turns[turn].Replies[0]);Act(game,player,"reply_"+turn,"Reply",amount:0,direction:turn);}
            return spoken;
        }
        [Test] public void AFriendReceivesEveryRapidUtteranceWithStableIdsAndAPublicLatestProjection()
        {
            var game=Pair(out var player,out var npc);var spoken=Finish(game,player,npc);
            player.Cash=123;player.Inventory.Add(new ItemStack{ItemId="private_inventory",Count=1});player.Effects.Add(new ActiveEffect{Id="private_effect"});player.Dialogue.Seen.Add("private_history");
            var friend=View(game,"friend");Assert.That(friend.PublicSpeech.Select(d=>d.Text),Is.EqualTo(spoken));
            int said=2*FestivalSimulation.ConversationTurns;Assert.That(friend.PublicSpeech.Select(d=>d.Sequence),Is.EqualTo(Enumerable.Range(1,said).Select(k=>(long)k)));
            Assert.That(friend.PublicSpeech.Select(d=>d.DeliveryId).Distinct().Count(),Is.EqualTo(said));
            Assert.That(friend.PublicSpeech.Select(d=>d.SpeakerId),Is.EqualTo(Enumerable.Range(0,said).Select(k=>k%2==0?npc.Id:player.Id)));
            Assert.That(friend.PublicSpeech.All(d=>d.OwnerId==""&&d.InteractionId==""),Is.True,"host routing is absent from every public DTO");
            var copy=friend.Players.Find(p=>p.Id==player.Id);Assert.That(copy.NpcSpeech,Is.EqualTo(spoken.Last()));Assert.That(copy.SpeechById,Is.EqualTo(player.Id));
            Assert.That(copy.SpeechDeliveryId,Is.EqualTo(friend.PublicSpeech.Last().DeliveryId));Assert.That(copy.ConversationOutcomes,Is.Empty);
            Assert.That(copy.Cash,Is.Zero);Assert.That(copy.Inventory,Is.Empty);Assert.That(copy.Effects,Is.Empty);Assert.That(copy.Dialogue.Seen,Is.Empty);
            Assert.That(friend.Npcs.Find(n=>n.Id==npc.Id).Role,Is.Empty);Assert.That(friend.Interactions,Is.Empty);
            Assert.That(JsonUtility.ToJson(friend),Does.Not.Contain("private_history").And.Not.Contain("private_inventory").And.Not.Contain("private_effect"));
            Assert.That(View(game,"friend").PublicSpeech.Select(d=>d.DeliveryId),Is.EqualTo(friend.PublicSpeech.Select(d=>d.DeliveryId)),"snapshot retransmission preserves delivery identities");
            var owner=View(game,player.Id);Assert.That(owner.Players.Find(p=>p.Id==player.Id).NpcSpeech,Is.EqualTo(spoken.Last()),"private outcomes never enter the public latest-line projection");
            Assert.That(owner.Players.Find(p=>p.Id==player.Id).ConversationOutcomes.Single().Reaction,Is.EqualTo(player.ConversationOutcomes.Single().Reaction));
        }
        // The talker's view, the host's own included, carries the grade of each reply they picked (the meter moves on that echo)
        // and no other, so no client can read the smooth reply before it picks. The host's talk keeps every grade.
        [Test] public void ATalkersViewCarriesOnlyThePickedRepliesGrades()
        {
            var game=Pair(out var player,out var npc);Act(game,player,"start","Conversation",npc.Id);var talk=game.Interaction(player.InteractionId);
            Act(game,player,"reply_0","Reply",amount:1,direction:0);
            var every=new[]{ConversationLines.Smooth,ConversationLines.SoSo,ConversationLines.Awkward};
            foreach(var view in new[]{FestivalSession.ViewFor(game,player.Id),View(game,player.Id)})
            {
                var shown=view.Interactions.Single(i=>i.Id==talk.Id);
                Assert.That(shown.Turns.Count,Is.EqualTo(2),"setup: the first reply picked, the second turn live");
                Assert.That(shown.Turns[0].Pick==1&&shown.Turns[0].Grades[1]==talk.Turns[0].Grades[1],Is.True,"the picked reply's grade comes back as the host's echo");
                foreach(int r in new[]{0,2})Assert.That(shown.Turns[0].Grades[r],Is.EqualTo(-1),"no grade for reply "+(r+1)+", not picked");
                Assert.That(shown.Turns[1].Grades,Is.All.EqualTo(-1),"nor for any reply of the live turn");
                Assert.That(shown.Turns[1].Replies,Is.EqualTo(talk.Turns[1].Replies),"the replies themselves still show");
            }
            foreach(var turn in talk.Turns)Assert.That(turn.Grades.OrderBy(g=>g),Is.EqualTo(every),"the host keeps every grade");
            FestivalSession.ViewFor(game,player.Id).Interactions.Single(i=>i.Id==talk.Id).Turns[1].Pick=2;
            Assert.That(talk.Turns[1].Pick,Is.EqualTo(-1),"the host's own view is a copy: changing it changes nothing on the host");
            Assert.That(View(game,"friend").Interactions,Is.Empty,"and nobody else gets the talk at all");
        }
        // A nailed trust dance hints the first round's smooth reply to the talker alone. Their view carries its index, and
        // still no grade; nobody else's view carries the talk at all.
        [Test] public void ANailedDancesHintReachesTheTalkerAlone()
        {
            var game=Pair(out var player,out var npc);npc.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=FestivalSimulation.TrustDanceSuspicion});
            Act(game,player,"start","Conversation",npc.Id);var talk=game.Interaction(player.InteractionId);Assert.That(talk.TrustDance,Is.True,"setup: a dance first");
            foreach(var note in RhythmChart.For(talk).Notes)talk.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
            for(int n=0;FestivalSimulation.TrustDancing(talk)&&n<100;n++)game.Tick(.1);
            int smooth=Array.IndexOf(talk.Turns[0].Grades,ConversationLines.Smooth);
            Assert.That(talk.Status=="Active"&&talk.HintedReply==smooth,Is.True,"setup: the nailed dance hints the smooth reply");
            var shown=View(game,player.Id).Interactions.Single(i=>i.Id==talk.Id);
            Assert.That(shown.HintedReply,Is.EqualTo(smooth),"the talker's view carries the hinted reply");
            Assert.That(shown.Turns[0].Grades,Is.All.EqualTo(-1),"and still no grade");
            foreach(var other in new[]{"friend","ghost"})
            {
                var view=View(game,other);Assert.That(view.Interactions,Is.Empty,other+" gets no talk");
                Assert.That(JsonUtility.ToJson(view),Does.Not.Contain("HintedReply"),other+"'s view carries no hint");
            }
        }
        [Test] public void EverySpeechViewIsDetachedFromTheHostAndFromOtherRecipients()
        {
            var game=Pair(out var player,out var npc);Act(game,player,"bark","Talk",npc.Id);
            var first=FestivalSession.ViewFor(game,"friend");var second=FestivalSession.ViewFor(game,player.Id);
            Assert.That(first.PublicSpeech.Count,Is.EqualTo(1));first.PublicSpeech[0].Text="client mutation";first.PublicSpeech[0].Retracted=true;
            Assert.That(game.State.PublicSpeech[0].Retracted,Is.False);Assert.That(second.PublicSpeech[0].Text,Is.EqualTo(game.State.PublicSpeech[0].Text));
        }
        [Test] public void SpiritsReceiveNoLivingOrNpcSpeechThroughAnyProjection()
        {
            var game=Pair(out var player,out var npc);Finish(game,player,npc);var ghost=View(game,"ghost");
            Assert.That(ghost.Npcs,Is.Empty);Assert.That(ghost.Players.All(p=>p.Life=="Spirit"),Is.True);Assert.That(ghost.PublicSpeech,Is.Empty);
            Assert.That(ghost.Players.All(p=>p.NpcSpeech==""&&p.SpeechById==""&&p.SpeechDeliveryId==""),Is.True);
            foreach(var utterance in game.State.PublicSpeech)Assert.That(JsonUtility.ToJson(ghost),Does.Not.Contain(utterance.Text));
        }
        [TestCase(false)] [TestCase(true)] public void PrivateChecksIncludingDoormanNeverPublish(bool doorman)
        {
            var game=Pair(out var player,out var npc);if(doorman)npc.Twist=FestivalSimulation.VipGuard;
            else{game.State.TripperId=player.Id;game.State.Visions.Add(new VisionState{Id="private_vision",NpcId=npc.Id,Kind="Buyer",IsTrue=false});}
            Act(game,player,"check","ConfirmChat",npc.Id);var interaction=game.Interaction(player.InteractionId);
            Assert.That(interaction.Chat.Opener,Is.Not.Empty);if(doorman)Assert.That(interaction.Chat.Doorman,Is.Not.Empty);
            var owner=View(game,player.Id);Assert.That(owner.Interactions.Single().Chat.Opener,Is.EqualTo(interaction.Chat.Opener));
            var friend=View(game,"friend");Assert.That(friend.PublicSpeech,Is.Empty);Assert.That(friend.Interactions,Is.Empty);Assert.That(friend.Visions,Is.Empty);
            foreach(var line in new[]{interaction.Chat.Opener,interaction.Chat.Doorman}.Concat(interaction.Chat.Questions).Concat(interaction.Chat.Answers).Where(line=>!string.IsNullOrEmpty(line)))Assert.That(JsonUtility.ToJson(friend),Does.Not.Contain(line));
            Act(game,player,"cancel_check","Cancel");Assert.That(View(game,player.Id).PublicSpeech,Is.Empty);Assert.That(View(game,"friend").PublicSpeech,Is.Empty);
            Assert.That(game.State.Visions.All(v=>!v.Confirmed),Is.True,"canceling a private check never confirms truth");
        }
        [Test] public void RealMinigameAndResultBannerFreezeSpeechWithoutChangingDeliveryIdentity()
        {
            var game=Pair(out var player,out var npc);game.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;game.State.Booth.Spot=0;
            var spot=FestivalSimulation.BoothAt(game.State);player.X=spot.X;player.Z=spot.Z;game.Player("friend").X=spot.X+1;game.Player("friend").Z=spot.Z;npc.X=spot.X;npc.Z=spot.Z+1;
            Act(game,player,"pause_bark","Talk",npc.Id);game.Tick(3.1);Assert.That(game.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));
            var before=View(game,"friend");Assert.That(before.PublicSpeech.Count,Is.EqualTo(1));var delivery=before.PublicSpeech.Single();string id=delivery.DeliveryId;
            Assert.That(FestivalSimulation.SpeechActive(before,delivery),Is.True);double world=FestivalSimulation.WorldSeconds(game.State),until=delivery.ExpiresAtWorldSeconds;
            double remaining=FestivalSimulation.SpeechRemainingSeconds(before,before.Players.Find(p=>p.Id==player.Id));
            game.Tick(5);var paused=View(game,"friend");Assert.That(FestivalSimulation.WorldSeconds(game.State),Is.EqualTo(world).Within(.001));
            Assert.That(FestivalSimulation.SpeechActive(paused,paused.PublicSpeech.Single()),Is.True);Assert.That(FestivalSimulation.SpeechRemainingSeconds(paused,paused.Players.Find(p=>p.Id==player.Id)),Is.EqualTo(remaining).Within(.001));
            Assert.That(paused.Players.Find(p=>p.Id==player.Id).NpcSpeech,Is.EqualTo(delivery.Text),"an active public projection remains visible after the simulation deadline passes during the game");
            var up=game.Player(FestivalSimulation.CupsTurn(game.State));
            var call=game.Execute(up.Id,new GameCommand{Id="pause_call",Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.CallCups});Assert.That(call.Accepted,Is.True,call.Reason);
            game.Tick(2);var banner=View(game,"friend");Assert.That(FestivalSimulation.SpeechActive(banner,banner.PublicSpeech.Single()),Is.True);Assert.That(FestivalSimulation.SpeechRemainingSeconds(banner,banner.Players.Find(p=>p.Id==player.Id)),Is.EqualTo(remaining).Within(.001));Assert.That(banner.PublicSpeech.Single().DeliveryId,Is.EqualTo(id));Assert.That(FestivalSimulation.WorldSeconds(game.State),Is.EqualTo(world).Within(.001));
            game.Tick(2.1);Assert.That(game.State.Phase,Is.EqualTo("Playing"));
            var resumed=View(game,"friend");Assert.That(FestivalSimulation.SpeechActive(resumed,resumed.PublicSpeech.Single()),Is.True);Assert.That(resumed.PublicSpeech.Single().ExpiresAtWorldSeconds,Is.EqualTo(until));Assert.That(resumed.Players.Find(p=>p.Id==player.Id).NpcSpeech,Is.EqualTo(delivery.Text));
            game.Tick(until-FestivalSimulation.WorldSeconds(game.State)+.1);
            var expired=View(game,"friend");Assert.That(FestivalSimulation.SpeechActive(expired,expired.PublicSpeech.Single()),Is.False,"speech expires only after its remaining world seconds run out");
            Assert.That(FestivalSimulation.SpeechRemainingSeconds(expired,expired.Players.Find(p=>p.Id==player.Id)),Is.Zero);
            Assert.That(View(game,"friend").PublicSpeech.Single().DeliveryId,Is.EqualTo(id),"expiry does not create another delivery");
        }
        [Test] public void ExpiredCompatibilityTextKeepsItsIdentityWithoutBecomingARetraction()
        {
            var game=Pair(out var player,out var npc);Act(game,player,"expire_bark","Talk",npc.Id);string text=player.NpcSpeech,id=player.SpeechDeliveryId;game.Tick(8);
            var view=View(game,"friend");var projection=view.Players.Find(p=>p.Id==player.Id);var delivery=view.PublicSpeech.Single();
            Assert.That(FestivalSimulation.SpeechActive(view,delivery),Is.False);Assert.That(delivery.Retracted,Is.False);Assert.That(FestivalSimulation.SpeechRemainingSeconds(view,projection),Is.Zero);
            Assert.That(projection.SpeechDeliveryId,Is.EqualTo(id));Assert.That(projection.NpcSpeechUntil,Is.LessThanOrEqualTo(view.SimulationSeconds));
            Assert.That(projection.NpcSpeech,Is.EqualTo(text),"an already-read legacy caption can distinguish ordinary expiry from authoritative empty-text retraction");
        }
        [Test] public void RealSecurityHushCarriesATombstoneWithoutReplayingItsText()
        {
            var game=Pair(out var player,out var npc);game.HasLineOfSight=(ax,az,bx,bz)=>true;npc.Kind="Cop";npc.Id="cop_0";npc.X=0;npc.Z=0;npc.Yaw=0;player.Z=5;
            player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});player.EquippedItemId="stock_lsd";game.Tick(.1);
            var before=View(game,"friend");Assert.That(before.PublicSpeech.Count,Is.EqualTo(1));string id=before.PublicSpeech.Single().DeliveryId;
            var stow=game.Execute(player.Id,new GameCommand{Id="hush_stow",Kind="Equip"});Assert.That(stow.Accepted,Is.True);game.Tick(.1);
            var after=View(game,"friend");Assert.That(after.PublicSpeech.Single().DeliveryId,Is.EqualTo(id));Assert.That(after.PublicSpeech.Single().Retracted,Is.True);Assert.That(after.PublicSpeech.Single().Text,Is.Empty);
            Assert.That(after.Players.Find(p=>p.Id==player.Id).NpcSpeech,Is.Empty);Assert.That(View(game,"friend").PublicSpeech.Single().Retracted,Is.True);
        }
    }
}
