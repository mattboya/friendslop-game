using System.Collections.Generic;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Festival.Tests
{
    // DIALOG-3: a sale, a buy, a chat and a talk with security are talks (FestivalSimulation.Talks), shown on the chat window DIALOG-5
    // built for every conversation. Its title line says what the talk is for, their line sits on the left, and your three replies are
    // the buttons on 1-3, over a bar for the time left to answer. Picking one says it on your side and sends the host that turn's
    // Reply; the next turn brings their reaction and their next line. A chat or a buy ends on the hunch it left you with. The window
    // follows the talker's own view (FestivalSession.ViewFor, through a snapshot), with time handed in, on the HUD's 1920x1080 canvas.
    public sealed class HudConversationTests
    {
        GameObject canvas;
        readonly List<CommandResult> sent=new List<CommandResult>();
        [TearDown] public void TakeTheCanvasDown(){if(canvas!=null)Object.DestroyImmediate(canvas);sent.Clear();}
        // The window as the HUD builds it, the commands it sends run on `game` as `you`'s (FestivalHud sends them through the session).
        FestivalChatWindow Window(FestivalSimulation game,PlayerState you)
        {
            canvas=new GameObject("Conversation test canvas",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
            var window=FestivalChatWindow.Build(canvas.transform,Resources.Load<Font>("FestivalDisplay"),null);
            window.Send=c=>{c.Id="hud_talk_"+sent.Count;sent.Add(game.Execute(you.Id,c));};
            return window;
        }

        // A real Day 1 for two, under way, and beside you a festivalgoer playing `role` who won't ask for a dance first: not a raver or a
        // burner, and not wary of you yet.
        static FestivalSimulation Day(out PlayerState you,out NpcState them,string role="Regular")
        {
            var game=new FestivalSimulation(29);
            foreach(var id in new[]{"host","friend"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
            Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: Day 1 under way");
            string talker="talker_0";for(int k=1;DialogueGrammar.PersonaFor(talker,"")=="Raver"||DialogueGrammar.PersonaFor(talker,"")=="Burner";k++)talker="talker_"+k;
            them=new NpcState{Id=talker,Kind="Wook",Role=role,X=6,Z=6,CanTalk=true};game.State.Npcs.Add(them);
            you=game.Player("host");you.X=them.X+.3f;you.Z=them.Z;
            return game;
        }
        static InteractionState Talk(FestivalSimulation game,PlayerState you,NpcState them,string kind="Conversation")
        {
            var started=game.Execute(you.Id,new GameCommand{Id="talk_"+kind,Kind=kind,TargetId=them.Id});
            Assert.That(started.Accepted,Is.True,"setup: the "+kind+" starts: "+started.Reason);
            var talk=game.Interaction(you.InteractionId);
            Assert.That(talk.Kind==kind&&!talk.TrustDance&&talk.Turns.Count==1,Is.True,"setup: the talk begins at once, no dance first");
            return talk;
        }
        static RoundState View(FestivalSimulation game,string id)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,id)));
        static PlayerState Me(RoundState view,string id)=>view.Players.Find(p=>p.Id==id);
        // The window follows `game` as `id`'s client sees it, `now` seconds in (the HUD's clock).
        static bool Follow(FestivalChatWindow window,FestivalSimulation game,string id,float now){var view=View(game,id);return window.Follow(view,Me(view,id),view.SimulationSeconds,now,false);}
        // The thread as drawn, oldest first: "< " for their bubbles, "> " for yours, "= " for a line of its own, then what it shows.
        static string[] Thread(FestivalChatWindow window){var lines=new List<string>();for(int i=0;i<window.Bubbles;i++)lines.Add((window.IsNote(i)?"= ":window.IsTheirs(i)?"< ":"> ")+window.Shown(i));return lines.ToArray();}
        static int Smooth(ConversationTurn turn)=>System.Array.IndexOf(turn.Grades,ConversationLines.Smooth);

        [Test] public void OfferedSlurredRepliesHaveAnExplanatoryPrivateLabel()
        {
            var game=Day(out var you,out var them);you.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=3});
            var talk=Talk(game,you,them);Assert.That(System.Array.Exists(talk.Turns[0].Garbled,g=>g),Is.True,"fixture offers actual slurred replies");var window=Window(game,you);Follow(window,game,you.Id,0);
            Assert.That(System.Array.Exists(window.GetComponentsInChildren<Text>(false),t=>t.text=="TRIPPING • SOME REPLIES SLUR"),Is.True);
        }

        [Test] public void RetainedOutcomeCarriesItsSourceNpcOnlyInTheOwnerSnapshot()
        {
            var game=Day(out var you,out var them);var talk=Talk(game,you,them);
            for(int t=0;t<FestivalSimulation.ConversationTurns;t++)Assert.That(game.Execute(you.Id,new GameCommand{Id="target_reply_"+t,Kind="Reply",Amount=Smooth(talk.Turns[t]),Direction=t}).Accepted,Is.True);
            var outcome=you.ConversationOutcomes[0];
            Assert.That(JsonUtility.ToJson(outcome),Does.Contain("\"TargetId\":\""+them.Id+"\""),"a retained outcome needs source identity after the active interaction is gone");
            string owner=JsonUtility.ToJson(View(game,you.Id)),other=JsonUtility.ToJson(View(game,"friend"));
            Assert.That(owner,Does.Contain(outcome.DeliveryId));Assert.That(other,Does.Not.Contain(outcome.DeliveryId));
            var older=JsonUtility.FromJson<ConversationOutcome>("{\"DeliveryId\":\"old\"}");
            Assert.That(JsonUtility.ToJson(older),Does.Contain("\"TargetId\":\"\""),"older snapshots default to no invented source");
        }

        [Test] public void OrdinaryChatsHideTheWholeCountdownTrackAndExplainSlurredChoices()
        {
            var game=Day(out var you,out var them);you.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=3});
            var talk=Talk(game,you,them);var window=Window(game,you);Follow(window,game,you.Id,0);
            var track=window.transform.Find("Time left track");
            Assert.That(track.gameObject.activeSelf,Is.False,"an untimed chat must hide the track, not show an empty deadline");
            var labels=window.GetComponentsInChildren<Text>(false);
            Assert.That(System.Array.Exists(labels,t=>t.text=="TRIPPING • SOME REPLIES SLUR"),Is.True,"the actual offered slurs need their private explanatory label");
            var first=talk.Turns[0];
            for(int i=0;i<3;i++)Assert.That(window.ReplyLabel(i),Is.EqualTo(first.Replies[i]),"no grade hint may be added to the choice");
        }

        [Test] public void DismissedLiveIdentityCannotReopenFromAStaleSnapshot()
        {
            var game=Day(out var you,out var them);Talk(game,you,them);var window=Window(game,you);
            var stale=View(game,you.Id);var me=Me(stale,you.Id);
            window.Follow(stale,me,stale.SimulationSeconds,0,false);window.Dismiss();
            Assert.That(window.Follow(stale,me,stale.SimulationSeconds,1,false),Is.False,"the same dismissed interaction must remain consumed while its host acknowledgment is in flight");
            Assert.That(window.Open,Is.False);
            stale.RoundId+="next";
            Assert.That(window.Follow(stale,me,stale.SimulationSeconds,2,false),Is.True,"an interaction id/start reused in another round is a new thread");
        }

        [Test] public void ASecurityTalkOpensOnTheirLineWithThreeNumberedRepliesAndACountdown()
        {
            var game=Day(out var you,out _);var them=game.State.Npcs.Find(n=>n.Kind=="Cop");you.X=them.X+.3f;you.Z=them.Z;
            var talk=Talk(game,you,them,"Police");var window=Window(game,you);var first=talk.Turns[0];
            Assert.That(Follow(window,game,you.Id,0),Is.True,"a talk opens the window");
            Assert.That(window.Header,Is.EqualTo(FestivalChatWindow.SecurityHeader),"its title line says what a security talk is for");
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+first.Line}),"their line, whole at once: the time to answer is already running");
            Assert.That(window.Replies,Is.EqualTo(3),"your three replies");
            for(int i=0;i<3;i++)
            {
                Assert.That(window.ReplyKey(i),Is.EqualTo((i+1).ToString()),"reply "+(i+1)+"'s key cap");
                Assert.That(window.ReplyLabel(i),Is.EqualTo(first.Replies[i]),"reply "+(i+1)+" reads as the host dealt it");
                Assert.That(window.CanReply(i),Is.True,"reply "+(i+1)+" can be picked");
            }
            Assert.That(window.TimeLeft,Is.EqualTo(1).Within(.01),"the whole time to answer is ahead");
            game.Tick(FestivalSimulation.ReplySeconds/2);Follow(window,game,you.Id,1);
            Assert.That(window.TimeLeft,Is.EqualTo(.5f).Within(.02),"halfway through the turn, the bar is half full");
            var view=View(game,you.Id);
            Assert.That(FestivalChatWindow.TakesReplies(view,Me(view,you.Id)),Is.True,"so 1-3 reply instead of equipping");
            Assert.That(FestivalChatWindow.TakesReplies(View(game,"friend"),Me(View(game,"friend"),"friend")),Is.False,"a friend's 1-3 still equip");
            Assert.That(FestivalChatWindow.LiveChat(View(game,"friend"),Me(View(game,"friend"),"friend")),Is.Null,"and their client can't read your talk");
        }

        [Test] public void PickingAReplySaysItAndSendsThatTurnsReply()
        {
            var game=Day(out var you,out var them);var talk=Talk(game,you,them);var window=Window(game,you);var first=talk.Turns[0];
            Follow(window,game,you.Id,0);
            Assert.That(window.Pick(1),Is.True,"2 picks the second reply");
            Assert.That(sent.Count,Is.EqualTo(1),"one command sent");
            Assert.That(sent[0].Accepted,Is.True,"the host takes it as the Reply for turn 1: "+sent[0].Reason);
            Assert.That(first.Pick,Is.EqualTo(1),"and records the second reply");
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+first.Line,"> "+first.Replies[1]}),"your reply on your side");
            for(int i=0;i<3;i++)Assert.That(window.CanReply(i),Is.False,"one reply a turn: reply "+(i+1)+" is dimmed");
            Assert.That(window.Pick(0),Is.False,"and 1 picks nothing more");
            Assert.That(sent.Count,Is.EqualTo(1),"nor sends anything");
            var next=talk.Turns[1];
            Follow(window,game,you.Id,.1f);
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+first.Line,"> "+first.Replies[1],"< "+next.Reaction,"< "+next.Line}),"then their reaction to it and their next line");
            for(int i=0;i<3;i++)Assert.That(window.CanReply(i)&&window.ReplyLabel(i)==next.Replies[i],Is.True,"and the next turn's reply "+(i+1)+" to pick");
            Assert.That(window.TimeLeft,Is.EqualTo(0),"the next ordinary-chat reply has no deadline");
            window.Pick(Smooth(next));
            Assert.That(sent.Count==2&&sent[1].Accepted&&next.Pick==Smooth(next),Is.True,"turn 2's pick is turn 2's Reply");
        }

        [Test] public void ATurnLeftUnansweredGoesBySilently()
        {
            var game=Day(out var you,out _);var them=game.State.Npcs.Find(n=>n.Kind=="Cop");you.X=them.X+.3f;you.Z=them.Z;
            var talk=Talk(game,you,them,"Police");var window=Window(game,you);var first=talk.Turns[0];
            Follow(window,game,you.Id,0);
            game.Tick(FestivalSimulation.ReplySeconds+.1);
            Assert.That(talk.Turns.Count==2&&first.Pick<0,Is.True,"setup: the turn ran out with no reply");
            var next=talk.Turns[1];
            Follow(window,game,you.Id,5);
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+first.Line,"< "+next.Reaction,"< "+next.Line}),"nothing on your side, then their reaction to the silence");
            for(int i=0;i<3;i++)Assert.That(window.CanReply(i)&&window.ReplyLabel(i)==next.Replies[i],Is.True,"the next turn's reply "+(i+1)+" to pick");
            Assert.That(sent,Is.Empty,"and nothing was sent");
        }

        [Test] public void AnOrdinaryTalkKeepsItsFirstRepliesAfterSixtySeconds()
        {
            var game=Day(out var you,out var them);var talk=Talk(game,you,them);var first=talk.Turns[0];var window=Window(game,you);
            Follow(window,game,you.Id,0);game.Tick(60);
            Assert.That(Follow(window,game,you.Id,60),Is.True,"the real client window still follows the talk");
            Assert.That(talk.Turns.Count,Is.EqualTo(1),"no silent turn was invented");
            for(int reply=0;reply<3;reply++)Assert.That(window.CanReply(reply)&&window.ReplyLabel(reply)==first.Replies[reply],Is.True,"the original choice remains available");
            Assert.That(window.Pick(0)&&sent[0].Accepted,Is.True,"a pick after reading reaches the authority");
            Assert.That(first.Pick,Is.EqualTo(0));
        }

        // V12 sets the hunch as a chat's or a buy's last turn begins; the window keeps it, and shows it once the talk is over, as the
        // line it ends on, kept up until read like any last line (DIALOG-6).
        [Test] public void AChatEndsOnTheHunchItLeft()
        {
            var game=Day(out var you,out var them,"Narc");var talk=Talk(game,you,them);var window=Window(game,you);
            string last="";
            for(int t=0;t<FestivalSimulation.ConversationTurns;t++)
            {
                Follow(window,game,you.Id,t);
                var turn=talk.Turns[t];last=turn.Replies[Smooth(turn)];
                Assert.That(window.Pick(Smooth(turn)),Is.True,"reply "+(t+1)+" picked");
                Assert.That(Thread(window),Has.None.StartsWith("= "),"no hunch while the talk is on");
            }
            Assert.That(talk.Status,Is.EqualTo("Complete"),"setup: the last reply ends the chat");
            Assert.That(talk.Hunch,Does.StartWith("You get the feeling"),"setup: a chat leaves a hunch");
            float over=FestivalSimulation.ConversationTurns;
            Assert.That(Follow(window,game,you.Id,over),Is.False,"the chat is over");
            Assert.That(window.Lingering,Is.True,"its window stays up to read");
            var thread=Thread(window);
            Assert.That(new[]{thread[thread.Length-2],thread[thread.Length-1]},Is.EqualTo(new[]{"> "+last,"= "+talk.Hunch}),"your last reply, then the hunch on a line of its own");
            Assert.That(talk.Hunch,Does.Not.Contain("TRUE").And.Not.Contain("FALSE"),"a hunch, not a verdict");
            Follow(window,game,you.Id,over+FestivalChatWindow.Lingers(talk.Hunch)-.05f);
            Assert.That(window.Open,Is.True,"the hunch stays until read");
            // It goes once every line in it has been up its reading time (DIALOG-6): the hunch, or a longer line said a turn before.
            float readBy=over+FestivalChatWindow.Lingers(talk.Hunch);
            for(int t=0;t<FestivalSimulation.ConversationTurns;t++){var turn=talk.Turns[t];foreach(var line in new[]{turn.Reaction,turn.Line,turn.Replies[Smooth(turn)]})readBy=Mathf.Max(readBy,t+FestivalChatWindow.Lingers(line));}
            Follow(window,game,you.Id,readBy+FestivalChatWindow.FadeSeconds+.01f);
            Assert.That(window.Open,Is.True,"the hunch stays until an explicit close");
            window.Dismiss();Assert.That(window.Open,Is.False);
        }

        // A talk with security leaves no hunch (security is security), so it ends on your last reply.
        [Test] public void ATalkWithSecurityEndsOnYourLastReply()
        {
            var game=Day(out var you,out _);var cop=game.State.Npcs.Find(n=>n.Kind=="Cop");Assert.That(cop,Is.Not.Null,"setup: Day 1 has security");
            you.X=cop.X+.3f;you.Z=cop.Z;var talk=Talk(game,you,cop,"Police");var window=Window(game,you);
            Follow(window,game,you.Id,0);
            Assert.That(window.Header,Is.EqualTo(FestivalChatWindow.SecurityHeader),"its title line says what talking to security is for");
            string last="";
            for(int t=0;t<FestivalSimulation.ConversationTurns;t++){Follow(window,game,you.Id,t);var turn=talk.Turns[t];last=turn.Replies[Smooth(turn)];window.Pick(Smooth(turn));}
            Assert.That(talk.Status==("Complete")&&talk.Hunch=="",Is.True,"setup: the talk is over, with no hunch");
            Follow(window,game,you.Id,4);
            var thread=Thread(window);
            Assert.That(window.Lingering&&thread[thread.Length-1]=="> "+last,Is.True,"it ends on your last reply: "+string.Join(" | ",thread));
        }

        // A wary festivalgoer asks for a dance first: that is danced on the lanes (HudRhythmTests), and the card waits for the talk.
        [Test] public void ADanceFirstIsDancedThenTheTalkComesToTheCard()
        {
            var game=Day(out var you,out var them);
            them.Observers.Add(new ObserverState{PlayerId=you.Id,Suspicion=FestivalSimulation.TrustDanceSuspicion});
            Assert.That(game.Execute(you.Id,new GameCommand{Id="wary_talk",Kind="Conversation",TargetId=them.Id}).Accepted,Is.True,"setup: the chat starts");
            var talk=game.Interaction(you.InteractionId);Assert.That(talk.TrustDance,Is.True,"setup: they ask for a dance first");
            var window=Window(game,you);var view=View(game,you.Id);
            Assert.That(FestivalChatWindow.LiveChat(view,Me(view,you.Id)),Is.Null,"while you dance there's nothing to reply to");
            Assert.That(FestivalChatWindow.TakesReplies(view,Me(view,you.Id)),Is.False,"so 1-3 don't reply");
            Assert.That(Follow(window,game,you.Id,0)||window.Open,Is.False,"and the window stays shut");
            foreach(var note in RhythmChart.For(talk).Notes)talk.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
            for(int guard=0;talk.Turns.Count==0&&guard<200;guard++)game.Tick(.1);
            Assert.That(talk.Status=="Active"&&talk.Turns.Count==1,Is.True,"setup: a good dance wins their trust, and the talk follows");
            Assert.That(Follow(window,game,you.Id,1),Is.True,"now the window opens");
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+talk.Turns[0].Line}),"on their first line");
            Assert.That(window.Replies,Is.EqualTo(3),"with your three replies");
        }

        [Test] public void EachTalkSaysWhatItIsFor()
        {
            var s=new RoundState();s.Npcs.Add(new NpcState{Id="npc_1"});
            string Header(string kind)=>FestivalChatWindow.HeaderFor(s,new InteractionState{Id="talk",Kind=kind,TargetId="npc_1"});
            Assert.That(Header("Conversation"),Is.EqualTo("TALK  •  READ THEM AND CALM THEM"));
            Assert.That(Header("Sale"),Is.EqualTo("SELL  •  CLOSE THE DEAL"));
            Assert.That(Header("Buy"),Is.EqualTo("BUY  •  READ THEM BEFORE YOU PAY"));
            Assert.That(Header("Police"),Is.EqualTo("SECURITY  •  TALK YOUR WAY CLEAR"));
            Assert.That(Header("ConfirmChat"),Is.EqualTo("FESTIVALGOER  •  WOOK"),"the tripper's chat check still names who they are");
        }

        // A sale's title line says its ask, at its price in your money, and the score it needs for that price, which the meter marks.
        [Test] public void ASaleSaysItsAskAndWhatItNeeds()
        {
            foreach(var (ask,header,need) in new[]{(FestivalSimulation.FairAsk,"SELL  •  FAIR $10  •  NEEDS 75%",.75f),(FestivalSimulation.HighAsk,"SELL  •  HIGH $15  •  NEEDS 75%",.75f),(FestivalSimulation.GreedyAsk,"SELL  •  GREEDY $20  •  NEEDS 88%",.875f)})
            {
                var game=Day(out var you,out var them,"Buyer");you.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});foreach(var dose in game.State.Doses)dose.Dose=1;
                var started=game.Execute(you.Id,new GameCommand{Id="sale_"+ask,Kind="StartSale",TargetId=them.Id,ItemId="stock_lsd",Amount=ask});
                Assert.That(started.Accepted,Is.True,"setup: the sale starts: "+started.Reason);
                var window=Window(game,you);Follow(window,game,you.Id,0);
                Assert.That(window.Header,Is.EqualTo(header),"ask "+ask+" at dose 1");
                Assert.That(window.Need,Is.EqualTo(need).Within(1e-6),"the meter marks what ask "+ask+" needs");
                Object.DestroyImmediate(canvas);
            }
            var s=new RoundState{FestivalIndex=1};s.Npcs.Add(new NpcState{Id="npc_1"});var sam=new PlayerState{Id="sam",Ordinal=1};s.Players.Add(sam);
            Assert.That(FestivalChatWindow.HeaderFor(s,new InteractionState{Id="talk",Kind="Sale",TargetId="npc_1",Ask=FestivalSimulation.HighAsk},sam),Is.EqualTo("SELL  •  HIGH 15 RAMEN PACKETS  •  NEEDS 75%"),"in Sam's ramen");
        }

        // A sale ends on how it ended, as a line of its own in the chat window, in your money (the HUD hands the window the notice
        // it read with FestivalHudText.MoneyText).
        [Test] public void ASaleEndsOnItsResultInYourMoney()
        {
            var game=Day(out var you,out var them,"Buyer");foreach(var dose in game.State.Doses)dose.Dose=1;you.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            Assert.That(game.Execute(you.Id,new GameCommand{Id="sale",Kind="StartSale",TargetId=them.Id,ItemId="stock_lsd"}).Accepted,Is.True,"setup: the sale starts");
            var talk=game.Interaction(you.InteractionId);var window=Window(game,you);
            for(int t=0;talk.Status=="Active";t++){Follow(window,game,you.Id,t);Assert.That(window.Pick(Smooth(talk.Turns[t])),Is.True,"reply "+(t+1));}
            var view=View(game,you.Id);var me=Me(view,you.Id);var outcome=me.ConversationOutcomes.Find(o=>o.InteractionId==talk.Id);
            Assert.That(outcome?.Notice,Is.EqualTo("SOLD Tongue Stamps  •  +$10"),"setup: the host's notice, in dollars");
            me.Ordinal=1;view.FestivalIndex=1;
            window.FollowOutcome(outcome,10,false,false,FestivalHudText.MoneyText(view,me,outcome.Notice));
            var thread=Thread(window);
            Assert.That(thread[thread.Length-1],Is.EqualTo("= SOLD Tongue Stamps  •  +10 ramen packets"),"the last line, in your money: "+string.Join(" | ",thread));
        }

        // DIALOG-3: a talk keeps you face to face, held still as a dance holds you (FestivalSession freezes the move on Roots), until it
        // ends or you walk out of it with Cancel; a chat check, a ride or a task leaves you free to move as before.
        [Test] public void ATalkHoldsYouStillAsADanceDoes()
        {
            foreach(var kind in new[]{"Sale","Buy","Conversation","Police","Dance","ConfirmDance","Poi","Dj"})Assert.That(FestivalInput.Roots(kind),Is.True,"a "+kind+" holds you still");
            foreach(var kind in new[]{null,"","ConfirmChat","FindFriend","Medical",FestivalSimulation.LieDownKind,FestivalSimulation.RideWheelKind})Assert.That(FestivalInput.Roots(kind),Is.False,"a "+(kind??"null")+" doesn't");
        }

        // K2 (batch call 9: "you can walk away (E-hold, or moving off)"): a talk holds you still, and walking out of it (Cancel) puts
        // every watcher on edge, so a tap of E must not end it: not a second E just after the one that offered, nor one pressed to go
        // on. While you talk (FestivalHudText.WalksOutByHold) a tap of E runs nothing, and holding it HoldToStopSeconds walks out, as
        // the prompt says (WalkAway); a talk's trust dance stops by holding G instead (StopsByHold). HudTripperPlayTests presses
        // the keys.
        [Test] public void EscapeGuidanceLeavesTrustDanceHoldGIntact()
        {
            var game=Day(out var you,out var them);
            bool Walks(string id){var view=View(game,id);return FestivalHudText.WalksOutByHold(view,Me(view,id));}
            bool Stops(string id){var view=View(game,id);return FestivalHudText.StopsByHold(view,Me(view,id));}
            Assert.That(Walks(you.Id),Is.False,"doing nothing, E runs the action listed first");
            Talk(game,you,them);
            Assert.That(Walks(you.Id),Is.True,"talking, a tap of E runs nothing: holding it walks out");
            Assert.That(Stops(you.Id),Is.False,"and holding G doesn't");
            Assert.That(Walks("friend"),Is.False,"a friend's E is their own");
            Assert.That(FestivalHudText.WalkAway,Is.EqualTo("ESC TO WALK AWAY"),"the prompt says so");
            Assert.That(game.Execute(you.Id,new GameCommand{Id="walk_away",Kind="Cancel"}).Accepted,Is.True,"setup: you walk out");
            Assert.That(Walks(you.Id),Is.False,"a talk over holds nothing back");
            var seen=them.Observers.Find(o=>o.PlayerId==you.Id);if(seen==null)them.Observers.Add(seen=new ObserverState{PlayerId=you.Id});
            seen.Suspicion=FestivalSimulation.TrustDanceSuspicion;
            var wary=game.Execute(you.Id,new GameCommand{Id="walk_wary",Kind="Conversation",TargetId=them.Id});
            Assert.That(wary.Accepted&&game.Interaction(you.InteractionId).TrustDance,Is.True,"setup: wary of you now, they ask for a dance first: "+wary.Reason);
            Assert.That(Walks(you.Id),Is.False,"dancing for their trust, E holds nothing back");
            Assert.That(Stops(you.Id),Is.True,"holding G stops the dance, as any dance");
        }

        // A meter under the title line shows how the talk is going. It starts in the middle and moves when the host's echo of your
        // pick comes back (one snapshot later on a joined client), never at the pick itself: up for a smooth reply and down
        // for an awkward one, with its word (COLD, IFFY, WARM or VIBING) and its colour, red to green, which their reaction bubble
        // takes too. The last pick has no echo, as the talk settles on it: the meter lands on the score the host settled
        // (ConversationOutcome.Score).
        [Test] public void TheMeterMovesOnTheHostsEchoOfEveryPick()
        {
            var game=Day(out var you,out var them);var talk=Talk(game,you,them);var window=Window(game,you);
            Follow(window,game,you.Id,0);
            Assert.That(window.MeterShown,Is.True,"a talk shows the meter");
            Assert.That(window.Going,Is.EqualTo(.5f),"it starts in the middle");
            Assert.That(window.GoingWord,Is.EqualTo(FestivalChatWindow.Iffy));
            Assert.That(window.Need,Is.EqualTo(-1f),"a chat needs no score, so the meter has no mark");
            // Smooth first and last, awkward between: WARM, IFFY, then COLD as each echo comes back.
            string[] words={FestivalChatWindow.Warm,FestivalChatWindow.Iffy,FestivalChatWindow.Cold};int final=FestivalSimulation.ConversationTurns-1;
            for(int t=0;t<=final;t++)
            {
                var turn=talk.Turns[t];bool smooth=t==0||t==final;float before=window.Going;
                Assert.That(window.Pick(System.Array.IndexOf(turn.Grades,smooth?ConversationLines.Smooth:ConversationLines.Awkward)),Is.True,"reply "+(t+1)+" picked");
                Assert.That(window.Going,Is.EqualTo(before),"the pick alone leaves the meter where it was (turn "+(t+1)+")");
                if(t==final)break;
                Follow(window,game,you.Id,t+1);int reaction=window.Bubbles-2;
                Assert.That(window.Going,smooth?Is.GreaterThan(before):Is.LessThan(before),"the host's echo moves it: "+(smooth?"up for a smooth reply":"down for an awkward one")+" (turn "+(t+1)+")");
                Assert.That(window.GoingWord,Is.EqualTo(words[t]),"turn "+(t+1)+"'s word");
                Assert.That(window.GoingColor,Is.EqualTo(FestivalChatWindow.Colour(window.Going)),"the meter's colour follows how it's going");
                Assert.That(window.IsTheirs(reaction)&&window.Shown(reaction)==talk.Turns[t+1].Reaction,Is.True,"setup: turn "+(t+2)+" opens on their reaction");
                Assert.That(window.BubbleColor(reaction),Is.EqualTo(FestivalChatWindow.ReactionColour(window.Going)),"their reaction to reply "+(t+1)+" takes the meter's colour");
            }
            Assert.That(talk.Status,Is.EqualTo("Complete"),"setup: the last reply ends the chat");
            float unsettled=window.Going;
            Assert.That(Follow(window,game,you.Id,FestivalSimulation.ConversationTurns),Is.False,"setup: the chat is over");
            Assert.That(window.Going,Is.EqualTo(unsettled),"no outcome yet, so the meter waits");
            var outcome=you.ConversationOutcomes[0];float settled=FestivalSimulation.ConversationTurns;
            window.FollowOutcome(outcome,settled,false,false);window.FollowOutcome(outcome,settled+FestivalChatWindow.Typed(outcome.Reaction)+.1f,false,false);
            Assert.That(window.Going,Is.EqualTo((float)talk.Score).Within(1e-6),"the meter lands on the score the host settled");
            Assert.That(window.GoingWord,Is.EqualTo(FestivalChatWindow.Word((float)talk.Score)));
            int last=-1;for(int i=0;i<window.Bubbles;i++)if(window.IsTheirs(i)&&window.Shown(i)==outcome.Reaction)last=i;
            Assert.That(last,Is.GreaterThanOrEqualTo(0),"setup: their closing reaction is shown");
            Assert.That(window.BubbleColor(last),Is.EqualTo(FestivalChatWindow.ReactionColour((float)talk.Score)),"their closing reaction takes the colour the talk landed on");
            Assert.That(window.MeterShown&&window.Going==(float)talk.Score,Is.True,"the meter stays where the talk landed while the window lingers");
            Assert.That(FestivalChatWindow.Colour(1).g>FestivalChatWindow.Colour(1).r&&FestivalChatWindow.Colour(0).r>FestivalChatWindow.Colour(0).g,Is.True,"it runs from red to green");
        }

        // A joined client's last pick in a security talk reaches the host after the turn's time ran out. The host
        // refuses it and counts a silence, and the meter lands on the host's score, not on the refused pick.
        [Test] public void ALatePickOnTheLastSecurityTurnLeavesTheMeterOnTheHostsScore()
        {
            var game=Day(out var you,out _);var cop=game.State.Npcs.Find(n=>n.Kind=="Cop");you.X=cop.X+.3f;you.Z=cop.Z;
            var talk=Talk(game,you,cop,"Police");var window=Window(game,you);bool late=false;
            window.Send=c=>{c.Id="hud_talk_"+sent.Count;if(!late)sent.Add(game.Execute(you.Id,c));};
            int[] wanted={ConversationLines.Smooth,ConversationLines.Smooth,ConversationLines.Awkward,ConversationLines.Smooth};
            for(int t=0;t<FestivalSimulation.ConversationTurns;t++)
            {
                Assert.That(Follow(window,game,you.Id,t),Is.True,"turn "+(t+1)+" shows");
                late=t==FestivalSimulation.ConversationTurns-1;
                Assert.That(window.Pick(System.Array.IndexOf(talk.Turns[t].Grades,wanted[t])),Is.True,"reply "+(t+1)+" picked");
                game.Tick(.1);
            }
            for(int guard=0;talk.Status=="Active"&&guard<100;guard++)game.Tick(.1);
            Assert.That(talk.Status=="Complete"&&talk.Turns[FestivalSimulation.ConversationTurns-1].Pick<0,Is.True,"setup: the last turn ran out on the host, a silence");
            var view=View(game,you.Id);var me=Me(view,you.Id);
            Assert.That(window.Follow(view,me,view.SimulationSeconds,10,false),Is.False,"setup: the talk is over");
            window.FollowOutcome(me.ConversationOutcomes.Find(o=>o.InteractionId==talk.Id),10,false,false);
            Assert.That(window.Going,Is.EqualTo((float)talk.Score).Within(1e-6),"the meter lands on the host's score, "+talk.Score+", not on the refused pick ("+window.GoingWord+")");
        }

        // A security turn's bar empty on your screen takes no pick, so the thread never shows a reply the host
        // will refuse.
        [Test] public void AnEmptyBarTakesNoPick()
        {
            var game=Day(out var you,out _);var cop=game.State.Npcs.Find(n=>n.Kind=="Cop");you.X=cop.X+.3f;you.Z=cop.Z;
            var talk=Talk(game,you,cop,"Police");var window=Window(game,you);var view=View(game,you.Id);
            window.Follow(view,Me(view,you.Id),view.SimulationSeconds,0,false);
            Assert.That(window.CanReply(0),Is.True,"setup: a fresh turn takes a pick");
            window.Follow(view,Me(view,you.Id),talk.StartSeconds+talk.DurationSeconds+.05,1,false);
            Assert.That(window.TimeLeft,Is.EqualTo(0),"setup: the bar reads empty");
            for(int i=0;i<3;i++)Assert.That(window.CanReply(i),Is.False,"reply "+(i+1)+" can't be picked once the bar is empty");
            Assert.That(window.Pick(0),Is.False,"and 1 picks nothing");
            Assert.That(sent,Is.Empty,"nor sends anything");
        }

        // A sale that a bust cuts short has no score, so the meter stays where it stood.
        [Test] public void ACutSaleLeavesTheMeterWhereItStood()
        {
            var game=Day(out var you,out var them,"Buyer");foreach(var dose in game.State.Doses)dose.Dose=1;you.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            Assert.That(game.Execute(you.Id,new GameCommand{Id="sale",Kind="StartSale",TargetId=them.Id,ItemId="stock_lsd"}).Accepted,Is.True,"setup: the sale starts");
            var talk=game.Interaction(you.InteractionId);var window=Window(game,you);
            for(int t=0;t<2;t++){Follow(window,game,you.Id,t);Assert.That(window.Pick(Smooth(talk.Turns[t])),Is.True,"reply "+(t+1));}
            Follow(window,game,you.Id,2);float before=window.Going;string word=window.GoingWord;
            game.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="ArrestWarning",X=you.X+1,Z=you.Z,Evidence=new List<EvidenceState>{new EvidenceState{PlayerId=you.Id,Kind="WitnessedDeal",DetainAt=game.State.SimulationSeconds}}});game.Tick(.1);
            var view=View(game,you.Id);var me=Me(view,you.Id);var outcome=me.ConversationOutcomes.Find(o=>o.InteractionId==talk.Id);
            Assert.That(outcome?.Result,Is.EqualTo("BUSTED"),"setup: a bust mid-sale");
            Assert.That(window.FollowOutcome(outcome,3,false,false,FestivalHudText.MoneyText(view,me,outcome.Notice)),Is.True,"setup: the window stays up on the bust");
            Assert.That(window.Going,Is.EqualTo(before).Within(1e-6),"a cut sale has no score: the meter stays at "+before+" ("+word+"), got "+window.Going+" ("+window.GoingWord+")");
        }

        // The meter reads only the replies already picked (the talker's view carries no other grade, SpeechPrivacyTests), so even
        // a view that carried them would give nothing away: change every grade of a reply not picked, and it reads the same.
        [Test] public void TheMeterReadsOnlyTheRepliesYouPicked()
        {
            var game=Day(out var you,out var them);var talk=Talk(game,you,them);var window=Window(game,you);
            Assert.That(game.Execute(you.Id,new GameCommand{Id="meter_first",Kind="Reply",Amount=Smooth(talk.Turns[0]),Direction=0}).Accepted,Is.True,"setup: a smooth first reply");
            Follow(window,game,you.Id,0);float going=window.Going;
            Assert.That(going,Is.GreaterThan(.5f),"setup: the smooth reply the host recorded moves the meter up");
            foreach(int grade in new[]{ConversationLines.Awkward,ConversationLines.Smooth})
            {
                var view=View(game,you.Id);var live=view.Interactions.Find(i=>i.Id==talk.Id);
                foreach(var turn in live.Turns)for(int r=0;r<3;r++)if(r!=turn.Pick)turn.Grades[r]=grade;
                Assert.That(window.Follow(view,Me(view,you.Id),view.SimulationSeconds,1,false),Is.True);
                Assert.That(window.Going,Is.EqualTo(going),"replies not picked, all "+(grade==ConversationLines.Smooth?"smooth":"awkward")+", don't move the meter");
            }
        }

        // A buy marks the score they need to sell to you (FestivalSimulation.BuyScore, .4) on the meter. A chat and security need none.
        [Test] public void ABuyMarksTheScoreItNeeds()
        {
            var game=Day(out var you,out var them);var window=Window(game,you);
            var started=game.Execute(you.Id,new GameCommand{Id="meter_buy",Kind="StartBuy",TargetId=them.Id,ItemId=FestivalSimulation.SellerStock(them.Id)});
            Assert.That(started.Accepted,Is.True,"setup: the buy starts: "+started.Reason);
            Assert.That(Follow(window,game,you.Id,0),Is.True,"setup: the window follows the buy");
            Assert.That(FestivalSimulation.BuyScore,Is.EqualTo(.4),"a buy needs .4");
            Assert.That(window.MeterShown,Is.True);
            Assert.That(window.Need,Is.EqualTo((float)FestivalSimulation.BuyScore).Within(1e-6),"the meter marks the .4 they need to sell to you");
            foreach(var kind in new[]{"Conversation","Police"})Assert.That(FestivalChatWindow.NeedFor(new InteractionState{Kind=kind}),Is.EqualTo(-1f),"a "+kind+" has no mark");
        }

        // At 1920x1080 every reply a talk can deal fits its button uncut, garbled or not, on every festival.
        [Test] public void EveryReplyFitsItsButton()
        {
            var window=Window(new FestivalSimulation(1),new PlayerState{Id="nobody"});window.Begin("fit","TALK","F",0);window.Offer(new[]{"a","b","c"},null);
            Text label=null;foreach(var t in window.GetComponentsInChildren<Text>(true))if(t.name=="Question"&&t.transform.parent.name=="Reply 1")label=t;
            Assert.That(label,Is.Not.Null,"setup: reply 1 has its label");
            var replies=new HashSet<string>();
            foreach(var topic in ConversationLines.Topics)foreach(var persona in new List<string>(DialogueGrammar.Personas){ConversationLines.Security})for(int festival=0;festival<Festivals.Count;festival++)for(int seed=0;seed<40;seed++)
                foreach(var reply in ConversationLines.Turn(topic,persona,"Regular",festival,seed%FestivalSimulation.ConversationTurns,seed%4==3?ConversationLines.Silence:seed%3,false,seed%3,new ContentRandom(seed)).Replies)replies.Add(reply);
            var settings=label.GetGenerationSettings(label.rectTransform.rect.size);var measure=new TextGenerator();var cut=new List<string>();
            foreach(var reply in replies){measure.Populate(reply,settings);if(measure.characterCountVisible<reply.Length)cut.Add(reply);}
            Assert.That(replies.Count,Is.GreaterThan(500),"setup: the talks' replies");
            Assert.That(cut,Is.Empty,"replies cut in a "+label.rectTransform.rect.size+" button");
        }
    }
}
