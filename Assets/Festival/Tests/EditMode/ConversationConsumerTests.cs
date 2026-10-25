using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class ConversationConsumerTests
    {
        GameObject canvas;
        [TearDown] public void TearDown(){if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas);}

        static FestivalSimulation Pair(int dose,out PlayerState player,out NpcState partner,int seed=21)
        {
            var game=new FestivalSimulation(seed);player=game.AddPlayer("owner","Owner");game.AddPlayer("friend","Friend");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.TripperId=player.Id;
            player.X=0;player.Z=0;game.Player("friend").X=30;
            string id="talker_0";for(int n=1;DialogueGrammar.PersonaFor(id,"")!="Wook";n++)id="talker_"+n;
            partner=new NpcState{Id=id,Kind="Wook",Role="Regular",X=0,Z=1.5f,Yaw=0,CanTalk=true};game.State.Npcs.Add(partner);
            if(dose>0)player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose,RemainingSeconds=600});
            return game;
        }
        static InteractionState Start(FestivalSimulation game,PlayerState player,NpcState partner)
        {
            var result=game.Execute(player.Id,new GameCommand{Id="start",Kind="Conversation",TargetId=partner.Id});
            Assert.That(result.Accepted,Is.True,result.Reason);return game.Interaction(player.InteractionId);
        }
        FestivalChatWindow Window(FestivalSimulation game,PlayerState player)
        {
            canvas=new GameObject("impl:U13 chat canvas",typeof(RectTransform),typeof(Canvas));
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
            var window=FestivalChatWindow.Build(canvas.transform,Resources.Load<Font>("FestivalDisplay"),null);
            int sequence=0;window.Send=command=>{command.Id="reply_"+(sequence++);Assert.That(game.Execute(player.Id,command).Accepted,Is.True);};
            return window;
        }
        static RoundState View(FestivalSimulation game,string player)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,player)));
        static void Follow(FestivalChatWindow window,FestivalSimulation game,PlayerState player,float now)
        {
            var view=View(game,player.Id);Assert.That(window.Follow(view,view.Players.Find(p=>p.Id==player.Id),view.SimulationSeconds,now,false),Is.True);
        }

        static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static FestivalSession Consumer(FestivalSimulation game,PlayerState player,out GameObject root)
        {
            root=new GameObject("impl-U14 terminal consumer");var session=root.AddComponent<FestivalSession>();
            typeof(FestivalSession).GetProperty("State").SetValue(session,View(game,player.Id));
            typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,player.Id);return session;
        }
        static void Finish(FestivalSimulation game,PlayerState player,InteractionState talk)
        {
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)Assert.That(game.Execute(player.Id,new GameCommand{Id="finish_"+talk.Id+"_"+turn,Kind="Reply",Direction=turn,Amount=Array.IndexOf(talk.Turns[turn].Grades,ConversationLines.Smooth)}).Accepted,Is.True);
        }
        [Test] public void ANewCancelledThreadCannotReopenThePreviousOutcome()
        {
            var game=Pair(0,out var player,out var partner);var first=Start(game,player,partner);Finish(game,player,first);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="new_thread",Kind="Conversation",TargetId=partner.Id}).Accepted,Is.True);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="cancel_thread",Kind="Cancel"}).Accepted,Is.True);
            var session=Consumer(game,player,out var root);
            try{Assert.That(session.PendingConversationOutcome,Is.Null,"even a skipped new-thread snapshot must not reopen an old outcome");}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void AcknowledgmentsAreOwnedIdempotentAndDismissalSurvivesReconnect()
        {
            var game=Pair(0,out var player,out var partner);var talk=Start(game,player,partner);
            Assert.That(game.Execute("friend",new GameCommand{Id="foreign_ack",Kind="TalkAck",TargetId=talk.Id,Direction=0}).Accepted,Is.False);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="oversized_ack",Kind="TalkAck",TargetId=talk.Id,Direction=512}).Accepted,Is.False);
            Assert.That(player.Dialogue.Seen,Is.Empty);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="owned_ack",Kind="TalkAck",TargetId=talk.Id,Direction=0}).Accepted,Is.True);
            var seen=player.Dialogue.Seen.ToArray();long revision=player.DialogueRevision;
            Assert.That(seen,Is.EqualTo(talk.Turns[0].GrammarKeys));
            Assert.That(game.Execute(player.Id,new GameCommand{Id="duplicate_ack",Kind="TalkAck",TargetId=talk.Id,Direction=0}).Accepted,Is.True);
            Assert.That(player.DialogueRevision,Is.EqualTo(revision));Assert.That(player.Dialogue.Seen,Is.EqualTo(seen));Finish(game,player,talk);
            var outcome=player.ConversationOutcomes.Single();game.State.Interactions.Clear();
            Assert.That(game.Execute(player.Id,new GameCommand{Id="terminal_ack",Kind="TalkAck",TargetId=outcome.InteractionId,Direction=FestivalSimulation.ConversationTurns}).Accepted,Is.True,"terminal acknowledgment survives interaction retention loss");
            Assert.That(player.Dialogue.Seen,Does.Contain(outcome.GrammarKeys.Single()));
            Assert.That(game.Execute(player.Id,new GameCommand{Id="dismiss",Kind="TalkAck",TargetId=outcome.InteractionId,Direction=FestivalSimulation.ConversationTurns,Amount=1}).Accepted,Is.True);
            game.Disconnect(player.Id);game.AddPlayer(player.Id,"Owner");
            var session=Consumer(game,player,out var root);
            try{Assert.That(session.PendingConversationOutcome,Is.Null,"host acknowledgment keeps the dismissed outcome closed after reconnect");}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [TestCase("Sale")] [TestCase("Buy")] [TestCase("Conversation")]
        public void FailedTrustRefundsAndRetainsThePrivateWalkOff(string kind)
        {
            var game=Pair(0,out var player,out var partner);partner.Role=kind=="Sale"?"Buyer":"Regular";
            partner.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=40});game.HasLineOfSight=(ax,az,bx,bz)=>false;
            if(kind=="Sale")player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});int cash=player.Cash;
            var result=game.Execute(player.Id,new GameCommand{Id="trust_start",Kind=kind=="Conversation"?kind:"Start"+kind,TargetId=partner.Id,ItemId=kind=="Sale"?"stock_lsd":FestivalSimulation.SellerStock(partner.Id)});
            Assert.That(result.Accepted,Is.True,result.Reason);var talk=game.Interaction(player.InteractionId);Assert.That(talk.TrustDance,Is.True);
            for(int n=0;talk.Status=="Active"&&n<100;n++)game.Tick(.1);
            Assert.That(talk.Status,Is.EqualTo("Complete"));Assert.That(player.Cash,Is.EqualTo(cash));
            Assert.That(player.Inventory.Exists(i=>i.ItemId=="stock_lsd"),Is.EqualTo(kind=="Sale"));
            var outcome=player.ConversationOutcomes.Single();Assert.That(outcome.Reaction,Is.EqualTo("Nah. Not feeling it."));Assert.That(outcome.Notice,Is.EqualTo("They walk off."));
            game.State.Interactions.Clear();game.Tick(20);Assert.That(View(game,player.Id).Players.Find(p=>p.Id==player.Id).ConversationOutcomes.Single().Notice,Is.EqualTo(outcome.Notice));
            Assert.That(JsonUtility.ToJson(View(game,"friend")),Does.Not.Contain(outcome.Notice));
        }
        [Test] public void ExplicitNarcBustKeepsItsExplanationAfterDetentionTeleport()
        {
            var game=Pair(0,out var player,out var partner);partner.Role="Narc";
            Assert.That(game.Execute(player.Id,new GameCommand{Id="narc",Kind="StartBuy",TargetId=partner.Id,ItemId=FestivalSimulation.SellerStock(partner.Id)}).Accepted,Is.True);
            var talk=game.Interaction(player.InteractionId);Finish(game,player,talk);
            Assert.That(player.Life,Is.EqualTo("Detained"));Assert.That(player.X,Is.EqualTo(27));Assert.That(player.Z,Is.EqualTo(5));
            var outcome=player.ConversationOutcomes.Single();Assert.That(outcome.Notice,Is.EqualTo("Busted! That was an undercover narc."));
            game.State.Interactions.Clear();game.Tick(20);
            Assert.That(View(game,player.Id).Players.Find(p=>p.Id==player.Id).ConversationOutcomes.Single().Notice,Is.EqualTo(outcome.Notice));
            Assert.That(JsonUtility.ToJson(View(game,"friend")),Does.Not.Contain(outcome.Notice));
        }
        [Test] public void RetainedOutcomesAndAcknowledgedKeysStayBounded()
        {
            var game=Pair(0,out var player,out var partner);game.HasLineOfSight=(ax,az,bx,bz)=>false;
            for(int key=0;key<512;key++)player.Dialogue.Seen.Add("prior_"+key);
            for(int thread=0;thread<12;thread++)
            {
                var result=game.Execute(player.Id,new GameCommand{Id="bounded_"+thread,Kind="Conversation",TargetId=partner.Id});Assert.That(result.Accepted,Is.True,result.Reason);
                var talk=game.Interaction(player.InteractionId);Finish(game,player,talk);
                Assert.That(player.Dialogue.Seen.Count,Is.LessThanOrEqualTo(512));Assert.That(player.Dialogue.Seen.Distinct().Count(),Is.EqualTo(player.Dialogue.Seen.Count));
            }
            Assert.That(player.ConversationOutcomes.Count,Is.EqualTo(8));Assert.That(player.ConversationOutcomes.Select(o=>o.DeliveryId).Distinct().Count(),Is.EqualTo(8));
            Assert.That(View(game,"friend").Players.Find(p=>p.Id==player.Id).ConversationOutcomes,Is.Empty);
        }

        [Test] public void AuthenticatedHistoryRejectsForeignDelayedAndUnsafePacketsAndRetriesSaving()
        {
            string previous=LocalProfile.Folder,owned=Path.Combine(Path.GetTempPath(),"impl-U14-history-"+Guid.NewGuid().ToString("N"));GameObject root=null;FestivalSession session=null;
            try
            {
                Directory.CreateDirectory(owned);LocalProfile.Folder=Path.Combine(owned,"blocked");File.WriteAllText(LocalProfile.Folder,"owned blocker");
                root=new GameObject("impl-U14 history consumer");session=root.AddComponent<FestivalSession>();Call(session,"InitializePreferences",(object)new[]{"--profile","default"});
                typeof(FestivalSession).GetField("historyEpoch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,"epoch");
                Type packetType=typeof(FestivalSession).Assembly.GetType("Festival.Network.DialogueHistoryPacket");
                object Packet(long revision,params string[] keys)=>JsonUtility.FromJson(JsonUtility.ToJson(new History{Revision=revision,Seen=keys.ToList()}),packetType);
                var packet=Packet(1,"reply.talk.cosmic_0123456789abcdef");
                Assert.That(Call(session,"ReceiveAcknowledgedHistory",1UL,packet,false),Is.False,"only the authenticated host can update history");
                var oldEpoch=JsonUtility.FromJson(JsonUtility.ToJson(new History{Epoch="old_epoch",Revision=2,Seen=new List<string>{"older"}}),packetType);
                Assert.That(Call(session,"ReceiveAcknowledgedHistory",0UL,oldEpoch,false),Is.False,"an earlier session epoch cannot alter this profile");
                Assert.That(Call(session,"ReceiveAcknowledgedHistory",0UL,Packet(2,"bad/key"),false),Is.False);
                Assert.That(Call(session,"ReceiveAcknowledgedHistory",0UL,Packet(2,Enumerable.Repeat("safe",513).ToArray()),false),Is.False);
                Assert.That(Call(session,"ReceiveAcknowledgedHistory",0UL,packet,false),Is.True);Assert.That(Call(session,"ReceiveAcknowledgedHistory",0UL,packet,false),Is.False,"duplicate revision cannot save twice");
                Call(session,"SaveAcknowledgedHistory");Assert.That(session.Profile.Data.SeenDialogue,Is.Empty,"failed Save rolls back only the attempted merge");
                File.Delete(LocalProfile.Folder);Directory.CreateDirectory(LocalProfile.Folder);
                typeof(FestivalSession).GetField("nextHistorySave",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,0d);Call(session,"SaveAcknowledgedHistory");
                Assert.That(new LocalProfile("default").Data.SeenDialogue,Is.EqualTo(new[]{"reply.talk.cosmic_0123456789abcdef"}));
                Assert.That(Call(session,"ReceiveAcknowledgedHistory",0UL,Packet(0,"older"),false),Is.False,"delayed history never replaces newer acknowledged history");
                var player=new PlayerState();player.Dialogue.Seen.Add("live_key");player.DialogueRevision=4;
                Assert.That(typeof(FestivalSession).GetMethod("MergeHistorySeed",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{player,new List<string>{"seed_key","seed_key"}}),Is.True);
                Assert.That(player.Dialogue.Seen,Is.EqualTo(new[]{"seed_key","live_key"}),"late seed merges behind newer live history");
            }
            finally
            {
                // EditMode never runs OnDestroy, so give back the quality level and input that the preferences took.
                if(session!=null){session.Settings?.Dispose();session.Controls?.Dispose();}
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);LocalProfile.Folder=previous;if(Directory.Exists(owned))Directory.Delete(owned,true);
            }
        }
        [Serializable] sealed class History { public string Epoch="epoch"; public long Revision; public List<string> Seen=new List<string>(); }

        [TestCase(ConversationLines.Smooth)] [TestCase(ConversationLines.SoSo)] [TestCase(ConversationLines.Awkward)]
        public void ClosingReactionIsDurableAndPrivateAfterTheActiveThreadDisappears(int grade)
        {
            var game=Pair(0,out var player,out var partner);var talk=Start(game,player,partner);
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)Assert.That(game.Execute(player.Id,new GameCommand{Id="close_"+turn,Kind="Reply",Direction=turn,Amount=Array.IndexOf(talk.Turns[turn].Grades,grade)}).Accepted,Is.True);
            Assert.That(player.InteractionId,Is.Empty);Assert.That(talk.Turns.Count,Is.EqualTo(FestivalSimulation.ConversationTurns));
            Assert.That(player.NpcSpeech,Is.Not.Empty,"the actual last reply has a closing reaction");
            Assert.That(player.ConversationOutcomes.Single().GrammarKeys.Single(),Does.StartWith("react.Wook."+grade+"_"),"the private final delivery uses the actual chosen reply grade's authored family");
            game.State.Interactions.Clear();game.Tick(20);
            string owner=JsonUtility.ToJson(View(game,player.Id)),friend=JsonUtility.ToJson(View(game,"friend"));
            Assert.That(owner,Does.Contain("ConversationOutcomes"));Assert.That(owner,Does.Contain(player.NpcSpeech));
            Assert.That(friend,Does.Not.Contain(player.NpcSpeech),"a wire JSON search finds no remote owner's private outcome");
        }
        [Test] public void GeneratingAReplyTurnDoesNotSaveUnreadGrammar()
        {
            var game=Pair(0,out var player,out var partner);Start(game,player,partner);
            Assert.That(player.Dialogue.Seen,Is.Empty,"unseen generated choices are not host acknowledgments");
        }

        [TestCase("Conversation","Regular",0)] [TestCase("Buy","Regular",0)]
        [TestCase("Conversation","ClueHolder",1)] [TestCase("Buy","ClueHolder",1)]
        public void SellerHunchReachesOnlyTheTalkersSnapshotAndWindow(string kind,string role,int level)
        {
            const string hunch="You get the feeling they've got something to sell.";
            var game=Pair(0,out var player,out var partner,7);partner.Role=role;partner.StockSold=1;game.State.LevelIndex=level;
            var result=game.Execute(player.Id,new GameCommand{Id="seller_start",Kind=kind=="Buy"?"StartBuy":kind,TargetId=partner.Id,ItemId=FestivalSimulation.SellerStock(partner.Id)});
            Assert.That(result.Accepted,Is.True,result.Reason);var talk=game.Interaction(player.InteractionId);var window=Window(game,player);
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                var owner=View(game,player.Id);var friend=View(game,"friend");
                Assert.That(friend.Interactions,Is.Empty,"the hunch and conversation remain owner-only");
                foreach(var view in new[]{owner,friend})
                {
                    Assert.That(view.Npcs.Find(n=>n.Id==partner.Id).Role,Is.Empty,"no public role is added for seller discovery");
                    Assert.That(view.Npcs.Find(n=>n.Id==partner.Id).StockSold,Is.Zero,"remaining stock stays on the host");
                }
                if(turn==FestivalSimulation.ConversationTurns-1)Assert.That(owner.Interactions.Find(i=>i.Id==talk.Id).Hunch,Is.EqualTo(hunch),"the last active snapshot carries the exact private hunch");
                Follow(window,game,player,turn);
                Assert.That(window.Pick(Array.IndexOf(talk.Turns[turn].Grades,ConversationLines.Smooth)),Is.True);
            }
            var settled=View(game,player.Id);
            Assert.That(window.Follow(settled,settled.Players.Find(p=>p.Id==player.Id),settled.SimulationSeconds,3,false),Is.False,"the settled talk is no longer active");
            Assert.That(window.Lingering,Is.True,"the window stays open to read the hunch");
            Assert.That(talk.Status,Is.EqualTo("Complete"));
            Assert.That(Enumerable.Range(0,window.Bubbles).Any(i=>window.IsNote(i)&&window.Shown(i)==hunch),Is.True,"the real chat window renders the cached seller hunch after settlement");
        }

        [TestCase(0f)] [TestCase(180f)]
        public void WindowRepliesCalmThePartnerOnceWithoutTurningThem(float yaw)
        {
            var game=Pair(0,out var player,out var partner);partner.Yaw=yaw;
            partner.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=30});
            var interaction=Start(game,player,partner);var window=Window(game,player);
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                Follow(window,game,player,turn);
                Assert.That(window.Pick(Array.IndexOf(interaction.Turns[turn].Grades,ConversationLines.Smooth)),Is.True);
            }
            Assert.That(interaction.Status,Is.EqualTo("Complete"));Assert.That(interaction.Score,Is.EqualTo(1));
            Assert.That(partner.Observers[0].Suspicion,Is.EqualTo(15),"the window's last reply settles partner or witness once");
            Assert.That(partner.Yaw,Is.EqualTo(yaw),"the visibility cone stays unchanged");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ReadableSlurredChoicesSurviveThePrivateSnapshotAndWindow(int dose)
        {
            var game=Pair(dose,out var player,out var partner);var talk=Start(game,player,partner);var window=Window(game,player);
            var sober=Pair(0,out var plainPlayer,out var plainPartner);var plain=Start(sober,plainPlayer,plainPartner);
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                Follow(window,game,player,turn);var choices=talk.Turns[turn];var originals=plain.Turns[turn];
                Assert.That(choices.Garbled.Count(g=>g),Is.EqualTo(dose>=3?2:dose>=1?1:0));
                Assert.That(choices.Grades,Is.EqualTo(originals.Grades));Assert.That(window.Replies,Is.EqualTo(3));
                Assert.That(View(game,"friend").Interactions,Is.Empty,"reply text and grades belong to the talker");
                for(int reply=0;reply<3;reply++)
                {
                    string shown=window.ReplyLabel(reply),original=originals.Replies[reply];
                    Assert.That(shown,Is.EqualTo(choices.Replies[reply]),"the real window renders the host's choice");
                    Assert.That(shown.Length,Is.EqualTo(original.Length));
                    foreach(Match boundary in Regex.Matches(original,@"[^\p{L}]"))Assert.That(shown[boundary.Index],Is.EqualTo(original[boundary.Index]),"punctuation and spaces retain their positions");
                    foreach(Match word in Regex.Matches(original,@"\p{L}+"))
                    {
                        string heard=shown.Substring(word.Index,word.Length);
                        if(word.Length<5)Assert.That(heard,Is.EqualTo(word.Value),"short words stay readable");
                        else{Assert.That(heard[0],Is.EqualTo(word.Value[0]));Assert.That(heard[heard.Length-1],Is.EqualTo(word.Value[word.Length-1]));}
                    }
                    if(!choices.Garbled[reply])Assert.That(shown,Is.EqualTo(original));
                }
                int pick=Array.IndexOf(choices.Grades,ConversationLines.Smooth);
                Assert.That(window.Pick(pick),Is.True);
                Assert.That(sober.Execute(plainPlayer.Id,new GameCommand{Id="plain_"+turn,Kind="Reply",Direction=turn,Amount=pick}).Accepted,Is.True);
            }
            Assert.That(talk.Score,Is.EqualTo(plain.Score),"slurring never changes the grade");
        }

        [Test] public void SettlementDiagnosticFiresOnlyAfterTheLastAcceptedReply()
        {
            var game=Pair(0,out var player,out var partner);var talk=Start(game,player,partner);
            var events=new List<string>();game.ChatDiagnostic=(eventKind,status,round,seconds)=>events.Add(eventKind+" "+status);
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                Assert.That(events.Any(e=>e.StartsWith("TalkSettled")),Is.False,"there is no settlement before the final reply");
                Assert.That(game.Execute(player.Id,new GameCommand{Id="reply_"+turn,Kind="Reply",Direction=turn,Amount=0}).Accepted,Is.True);
            }
            Assert.That(events.Where(e=>e.StartsWith("TalkSettled")),Is.EqualTo(new[]{"TalkSettled Conversation"}),"debugging emits only the event and kind, without dialogue, grade or identity");
            Assert.That(game.Execute(player.Id,new GameCommand{Id="late",Kind="Reply",Direction=FestivalSimulation.ConversationTurns-1,Amount=0}).Accepted,Is.False);
            Assert.That(events.Count(e=>e.StartsWith("TalkSettled")),Is.EqualTo(1));Assert.That(events.All(e=>e=="TalkSettled Conversation"||e=="HistoryAcknowledged Talk"||e=="OutcomeDelivered Conversation"),Is.True,"lifecycle diagnostics carry no private contents");Assert.That(talk.Status,Is.EqualTo("Complete"));
        }
    }
}
