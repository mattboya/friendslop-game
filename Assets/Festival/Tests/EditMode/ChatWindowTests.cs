using System.Collections.Generic;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Festival.Tests
{
    // DIALOG-5: talking to someone reads as a chat. The window follows the tripper's chat check as their client's view holds it
    // (FestivalSession.ViewFor, through a snapshot): their lines on the left, yours on the right, the questions as reply buttons.
    // Time is handed in, so typing and fading are read at exact moments; the window sits on the HUD's 1920x1080 canvas.
    public sealed class ChatWindowTests
    {
        GameObject canvas;
        [TearDown] public void TakeTheCanvasDown(){if(canvas!=null)Object.DestroyImmediate(canvas);}
        FestivalChatWindow Window()
        {
            canvas=new GameObject("Chat window test canvas",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
            return FestivalChatWindow.Build(canvas.transform,Resources.Load<Font>("FestivalDisplay"),null);
        }

        // A real Day 1 for three, under way, with the tripper beside someone they have a vision about. Chat() starts the check.
        static FestivalSimulation Day(out PlayerState tripper)
        {
            var game=new FestivalSimulation(29);
            foreach(var id in new[]{"host","friend","guest"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
            Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: Day 1 under way");
            tripper=game.Player(game.State.TripperId);
            return game;
        }
        static Conversation Chat(FestivalSimulation game,PlayerState tripper,string id="chat")
        {
            var seen=game.State.Visions.Find(v=>v.NpcId!=""&&!v.Confirmed);Assert.That(seen,Is.Not.Null,"setup: the tripper has a vision to check");
            var npc=game.State.Npcs.Find(n=>n.Id==seen.NpcId);tripper.X=npc.X+.3f;tripper.Z=npc.Z;
            var started=game.Execute(tripper.Id,new GameCommand{Id=id,Kind="ConfirmChat",TargetId=npc.Id});
            Assert.That(started.Accepted,Is.True,"setup: the tripper chats to check a vision: "+started.Reason);
            return game.Interaction(tripper.InteractionId).Chat;
        }
        static RoundState View(FestivalSimulation game,string id)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,id)));
        static PlayerState Me(RoundState view,string id)=>view.Players.Find(p=>p.Id==id);
        // The window follows `game` as `id`'s client sees it, `now` seconds in (the HUD's clock).
        static bool Follow(FestivalChatWindow window,FestivalSimulation game,string id,float now,bool reducedMotion=false){var view=View(game,id);return window.Follow(view,Me(view,id),view.SimulationSeconds,now,reducedMotion);}
        // The thread as drawn, oldest first: "< " for their bubbles, "> " for yours, then what the bubble shows.
        static string[] Thread(FestivalChatWindow window){var lines=new List<string>();for(int i=0;i<window.Bubbles;i++)lines.Add((window.IsTheirs(i)?"< ":"> ")+window.Shown(i));return lines.ToArray();}
        static Button ReplyButton(FestivalChatWindow window,int i){foreach(var b in window.GetComponentsInChildren<Button>(true))if(b.name=="Reply "+(i+1))return b;return null;}

        [Test] public void ItOpensOnTheirOpenerWithTheQuestionsAsReplies()
        {
            var game=Day(out var tripper);var said=Chat(game,tripper);var window=Window();
            Assert.That(Follow(window,game,tripper.Id,0),Is.True,"the tripper's chat check opens the window");
            Assert.That(window.Open,Is.True);
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener}),"one bubble on their side: their opener");
            Assert.That(window.Replies,Is.EqualTo(3),"the three questions as reply buttons");
            for(int i=0;i<3;i++)
            {
                Assert.That(window.ReplyKey(i),Is.EqualTo((i+1).ToString()),"reply "+(i+1)+"'s key cap");
                Assert.That(window.ReplyLabel(i),Is.EqualTo(said.Questions[i]),"reply "+(i+1)+" asks question "+(i+1));
                Assert.That(window.CanReply(i),Is.True,"reply "+(i+1)+" can be picked");
            }
            Assert.That(window.Header,Does.StartWith("FESTIVALGOER  •  "),"the header names who you're talking to");
            Assert.That(window.TimeLeft,Is.EqualTo(1).Within(.01),"the whole chat is still ahead");
            var view=View(game,tripper.Id);var chat=FestivalChatWindow.LiveChat(view,Me(view,tripper.Id));
            window.Follow(view,Me(view,tripper.Id),chat.StartSeconds+FestivalSimulation.ConfirmChatSeconds/2,0,false);
            Assert.That(window.TimeLeft,Is.EqualTo(.5f).Within(.01),"halfway through the chat, the bar is half full");
        }

        [Test] public void PickingAQuestionAsksItThenTheyTypeTheirAnswer()
        {
            var game=Day(out var tripper);var said=Chat(game,tripper);var window=Window();
            Follow(window,game,tripper.Id,0);
            Assert.That(window.Pick(1),Is.True,"2 asks the second question");
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener,"> "+said.Questions[1],"< "+FestivalChatWindow.Ellipsis}),"your question on your side, then them typing");
            Follow(window,game,tripper.Id,FestivalChatWindow.TypingSeconds-.05f);
            Assert.That(window.Shown(2),Is.EqualTo(FestivalChatWindow.Ellipsis),"they type for about half a second");
            Follow(window,game,tripper.Id,FestivalChatWindow.TypingSeconds+10/FestivalChatWindow.LettersPerSecond+.001f);
            string answer=said.Answers[1];
            Assert.That(window.Shown(2),Does.StartWith(answer.Substring(0,10)).And.Not.StartWith(answer.Substring(0,11)),"then their answer types out, "+FestivalChatWindow.LettersPerSecond+" letters a second");
            Follow(window,game,tripper.Id,FestivalChatWindow.Typed(answer)+.01f);
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener,"> "+said.Questions[1],"< "+answer}),"until it is all there");
        }

        [Test] public void ExchangesStayInOrderAndAnAskedQuestionCannotBeAskedAgain()
        {
            var game=Day(out var tripper);var said=Chat(game,tripper);var window=Window();
            Follow(window,game,tripper.Id,0);window.Pick(1);
            // Asking again while they still type: their answer shows whole, then your question and their next "…".
            Follow(window,game,tripper.Id,1);
            Assert.That(window.Pick(0),Is.True,"1 asks the first question");
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener,"> "+said.Questions[1],"< "+said.Answers[1],"> "+said.Questions[0],"< "+FestivalChatWindow.Ellipsis}),
                "the answer they were typing shows whole before your next question");
            Follow(window,game,tripper.Id,1+FestivalChatWindow.Typed(said.Answers[0])+.01f);
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener,"> "+said.Questions[1],"< "+said.Answers[1],"> "+said.Questions[0],"< "+said.Answers[0]}),"every exchange, in order");
            Assert.That(window.CanReply(1),Is.False,"question 2 is dimmed");
            Assert.That(window.Pick(1),Is.False,"and 2 doesn't ask it again");
            ReplyButton(window,1).onClick.Invoke();
            Assert.That(window.Bubbles,Is.EqualTo(5),"nor does clicking it");
            Assert.That(window.CanReply(2),Is.True,"question 3 is still there to ask");
            ReplyButton(window,2).onClick.Invoke();
            var thread=Thread(window);
            Assert.That(new[]{thread[thread.Length-2],thread[thread.Length-1]},Is.EqualTo(new[]{"> "+said.Questions[2],"< "+FestivalChatWindow.Ellipsis}),"a click on reply 3 asks it, as 3 does");
        }

        [Test] public void TheThreadKeepsTheLastSixBubblesAndFadesOlderOnes()
        {
            var window=Window();
            window.Begin("talk","FESTIVALGOER  •  WOOK","W",0);window.TheySay("Opener",false);
            for(int i=1;i<=3;i++){window.YouSay("Question "+i);window.TheySay("Answer "+i,false);}
            Assert.That(window.Bubbles,Is.EqualTo(FestivalChatWindow.MaxBubbles),"three exchanges and the opener: the last six");
            window.Advance(FestivalChatWindow.FadeSeconds/2,1,false);
            Assert.That(window.ShownBubbles,Is.EqualTo(7),"the opener is still fading out");
            window.Advance(FestivalChatWindow.FadeSeconds+.01f,1,false);
            Assert.That(window.ShownBubbles,Is.EqualTo(6),"and then it is gone");
            window.YouSay("Question 4");window.TheySay("Answer 4",false);window.Advance(20,1,false);window.Advance(30,1,false);
            Assert.That(Thread(window),Is.EqualTo(new[]{"> Question 2","< Answer 2","> Question 3","< Answer 3","> Question 4","< Answer 4"}),"after four exchanges, only the last six show");
            Assert.That(window.ShownBubbles,Is.EqualTo(6));
        }

        [Test] public void TheHeaderNamesWhoYouAreTalkingTo()
        {
            var s=new RoundState();var p=new PlayerState{Id="you",InteractionId="chat"};s.Players.Add(p);
            var chat=new InteractionState{Id="chat",PlayerId="you",Kind="ConfirmChat",TargetId="npc_0"};s.Interactions.Add(chat);
            string Header(string npc,string twist,int questions)
            {
                s.Npcs.Clear();s.Npcs.Add(new NpcState{Id=npc,Twist=twist});chat.TargetId=npc;chat.Chat=new Conversation{Questions=new string[questions],Answers=new string[questions]};
                return FestivalChatWindow.HeaderFor(s,chat);
            }
            Assert.That(DialogueGrammar.PersonaFor("npc_0","")+" "+DialogueGrammar.PersonaFor("npc_1",""),Is.EqualTo("DraggedAlongDad Wook"),"setup: these ids talk as a dad and a wook");
            Assert.That(Header("npc_1","",3),Is.EqualTo("FESTIVALGOER  •  WOOK"));
            Assert.That(Header("npc_0","",3),Is.EqualTo("FESTIVALGOER  •  DRAGGED-ALONG DAD"));
            Assert.That(Header("npc_0",FestivalSimulation.Influencer,3),Is.EqualTo("FESTIVALGOER  •  INFLUENCER"),"whoever films talks like an influencer");
            Assert.That(Header("npc_0",FestivalSimulation.VipGuard,3),Is.EqualTo("VIP GUARD"),"the tripper checking the VIP guard");
            Assert.That(Header("npc_0",FestivalSimulation.VipGuard,0),Is.EqualTo("VIP GUARD  •  TALKING YOUR WAY IN"),"anyone talking their way past the rope");
        }

        [Test] public void WithNoChatTheWindowHidesAndOneToThreeEquipAgain()
        {
            var game=Day(out var tripper);Chat(game,tripper);var window=Window();
            var view=View(game,tripper.Id);
            Assert.That(FestivalChatWindow.TakesReplies(view,Me(view,tripper.Id)),Is.True,"while the chat runs, 1-3 ask its questions");
            Assert.That(Follow(window,game,tripper.Id,0),Is.True);
            foreach(var friend in game.State.Players)
            {
                if(friend.Id==tripper.Id)continue;
                var theirs=View(game,friend.Id);
                Assert.That(FestivalChatWindow.TakesReplies(theirs,Me(theirs,friend.Id)),Is.False,friend.Id+" has no chat, so 1-3 equip");
                Assert.That(FestivalChatWindow.LiveChat(theirs,Me(theirs,friend.Id)),Is.Null,friend.Id+"'s client can't read the tripper's chat");
            }
            game.Tick(FestivalSimulation.ConfirmChatSeconds+.1);
            view=View(game,tripper.Id);
            Assert.That(FestivalChatWindow.TakesReplies(view,Me(view,tripper.Id)),Is.False,"the chat is over: 1-3 equip again, on the frame the view says so");
            Assert.That(Follow(window,game,tripper.Id,9),Is.False,"no chat, no window");
            Assert.That(window.Open,Is.False);
        }

        // ViewFor sends only Active interactions, so the thread lives in the window: it outlasts the chat (DIALOG-6 shows it
        // lingering), and the next chat starts afresh.
        [Test] public void TheThreadIsKeptOnTheClientAndANewChatStartsAfresh()
        {
            var game=Day(out var tripper);var said=Chat(game,tripper);var window=Window();
            Follow(window,game,tripper.Id,0);window.Pick(0);Follow(window,game,tripper.Id,FestivalChatWindow.Typed(said.Answers[0])+.01f);
            game.Tick(FestivalSimulation.ConfirmChatSeconds+.1);Follow(window,game,tripper.Id,9);
            Assert.That(window.Open,Is.False,"setup: the chat is over");
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener,"> "+said.Questions[0],"< "+said.Answers[0]}),"the thread is still on this client");
            var next=Chat(game,tripper,"next_chat");
            Follow(window,game,tripper.Id,10);
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+next.Opener}),"a new chat opens on its own opener");
            for(int i=0;i<3;i++)Assert.That(window.CanReply(i)&&window.ReplyLabel(i)==next.Questions[i],Is.True,"with all three of its questions to ask");
        }

        [Test] public void ReducedMotionShowsTheirLineWholeAfterTheTypingBubble()
        {
            var game=Day(out var tripper);var said=Chat(game,tripper);var window=Window();
            Follow(window,game,tripper.Id,0,true);window.Pick(2);
            Follow(window,game,tripper.Id,FestivalChatWindow.TypingSeconds-.05f,true);
            Assert.That(window.Shown(2),Is.EqualTo(FestivalChatWindow.Ellipsis),"the typing bubble stays");
            Follow(window,game,tripper.Id,FestivalChatWindow.TypingSeconds+.01f,true);
            Assert.That(window.Shown(2),Is.EqualTo(said.Answers[2]),"then their answer appears whole");
        }

        // At 1920x1080 every question the chat grammar can ask fits its reply button uncut.
        [Test] public void EveryQuestionFitsItsReplyButton()
        {
            var window=Window();window.Begin("fit","FESTIVALGOER  •  WOOK","W",0);window.Offer(new[]{"a","b","c"},null);
            var questions=new HashSet<string>();
            foreach(var persona in DialogueGrammar.Personas)for(int festival=0;festival<Festivals.Count;festival++)for(int seed=0;seed<200;seed++)foreach(var q in DialogueGrammar.Build("Regular",persona,festival,seed).Questions)questions.Add(q);
            Text label=null;foreach(var t in ReplyButton(window,0).GetComponentsInChildren<Text>())if(t.name=="Question")label=t;
            Assert.That(label,Is.Not.Null,"setup: reply 1 has its question label");
            var settings=label.GetGenerationSettings(label.rectTransform.rect.size);var measure=new TextGenerator();var cut=new List<string>();
            foreach(var q in questions){measure.Populate(q,settings);if(measure.characterCountVisible<q.Length)cut.Add(q);}
            Assert.That(questions.Count,Is.GreaterThan(40),"setup: the grammar's questions");
            Assert.That(cut,Is.Empty,"questions cut in a "+label.rectTransform.rect.size+" reply");
        }
    }
}
