using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;

namespace Festival.Tests
{
    // HUD-2: what the whole crew reads about the spin, and what only the tripper reads: the checks, the chat and the game's one hint.
    // Wording comes from crafted round states the way a client's view holds them; privacy from real views (FestivalSession.ViewFor).
    public sealed class HudTripperTests
    {
        static RoundState Level(int level,string phase="Playing")=>new RoundState{LevelIndex=level,Phase=phase,DurationSeconds=Festivals.Level(0,level,0).DurationSeconds};
        static PlayerState Crew(RoundState s,string id,string name,string life="Alive"){var p=new PlayerState{Id=id,Name=name,Life=life};s.Players.Add(p);return p;}
        static void Dose(RoundState s,string id,int dose,string substance="")=>s.Doses.Add(new PlayerDose{PlayerId=id,Dose=dose,Substance=substance});
        static NpcState Npc(RoundState s,string id,float x,float z){var n=new NpcState{Id=id,X=x,Z=z};s.Npcs.Add(n);return n;}

        // A real Day 1 for three, under way: the spinners picked the tripper and dealt their visions.
        static FestivalSimulation Day()
        {
            var game=new FestivalSimulation(29);
            foreach(var id in new[]{"host","friend","guest"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
            Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: Day 1 under way");
            return game;
        }
        // The festivalgoer the tripper's first vision is about, with the tripper and a sober friend standing right beside them.
        static NpcState Beside(FestivalSimulation game,out PlayerState tripper,out PlayerState sober)
        {
            tripper=game.Player(game.State.TripperId);var me=tripper;sober=game.State.Players.Find(p=>p!=me);
            var seen=game.State.Visions.Find(v=>v.NpcId!="");Assert.That(seen,Is.Not.Null,"setup: the tripper has a vision about someone");
            var npc=game.State.Npcs.Find(n=>n.Id==seen.NpcId);
            tripper.X=npc.X+.3f;tripper.Z=npc.Z;sober.X=npc.X-.3f;sober.Z=npc.Z;
            return npc;
        }
        static PlayerState Me(RoundState view,string id)=>view.Players.Find(p=>p.Id==id);
        // DIALOG-5: the chat window on the HUD's 1920x1080 canvas, and its thread as drawn ("< " theirs, "> " yours), oldest first.
        UnityEngine.GameObject canvas;
        [TearDown] public void TakeTheCanvasDown(){if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas);}
        FestivalChatWindow ChatWindow()
        {
            canvas=new UnityEngine.GameObject("HUD tripper canvas",typeof(UnityEngine.RectTransform),typeof(UnityEngine.Canvas));canvas.GetComponent<UnityEngine.Canvas>().renderMode=UnityEngine.RenderMode.WorldSpace;
            ((UnityEngine.RectTransform)canvas.transform).sizeDelta=new UnityEngine.Vector2(1920,1080);
            return FestivalChatWindow.Build(canvas.transform,UnityEngine.Resources.Load<UnityEngine.Font>("FestivalDisplay"),null);
        }
        static string[] Thread(FestivalChatWindow window){var lines=new System.Collections.Generic.List<string>();for(int i=0;i<window.Bubbles;i++)lines.Add((window.IsTheirs(i)?"< ":"> ")+window.Shown(i));return lines.ToArray();}

        // TRIP-5: and what they took, as the substance wheel named it.
        [Test] public void EveryoneSeesWhoTripsAndOnHowManyDoses()
        {
            var s=Level(0);Crew(s,"you","You");Crew(s,"sam","Sam");s.TripperId="sam";Dose(s,"sam",2,"mushrooms");
            Assert.That(FestivalHudText.Tripping(s,"you"),Is.EqualTo("SAM TRIPS  •  2 DOSES  •  FUN GUYS"));
            Assert.That(FestivalHudText.Tripping(s,"sam"),Is.EqualTo("YOU TRIP  •  2 DOSES  •  FUN GUYS"),"the tripper reads it too");
            s.Doses[0].Dose=1;
            Assert.That(FestivalHudText.Tripping(s,"you"),Is.EqualTo("SAM TRIPS  •  1 DOSE  •  FUN GUYS"));
            s.Phase="Loading";
            Assert.That(FestivalHudText.Tripping(s,"you"),Is.EqualTo("SAM TRIPS  •  1 DOSE  •  FUN GUYS"),"shown from the moment the crew heads out");
            s.Doses[0].Substance="";
            Assert.That(FestivalHudText.Tripping(s,"you"),Is.EqualTo("SAM TRIPS  •  1 DOSE"),"a dose spun before the substance wheel names none");
            foreach(var phase in new[]{"Shopping","Spinning","Results","CampReview"})
            {
                s.Phase=phase;
                Assert.That(FestivalHudText.Tripping(s,"you"),Is.Empty,phase+": the spinner names the next tripper; camp still holds the last one");
            }
            var sober=Level(0);Crew(sober,"you","You");
            Assert.That(FestivalHudText.Tripping(sober,"you"),Is.Empty,"no spin, no tripper");
        }

        [Test] public void NightTwoListsEveryonesDose()
        {
            var s=Level(3);Crew(s,"you","You");Crew(s,"sam","Sam");Crew(s,"kim","Kim");s.TripperId="sam";
            Dose(s,"you",2,"mushrooms");Dose(s,"sam",4,"lsd");Dose(s,"kim",1,"ketamine");
            Assert.That(FestivalHudText.Tripping(s,"you"),Is.EqualTo("SAM TRIPS  •  4 DOSES  •  TONGUE STAMPS\nDOSED  YOU 2 FUN GUYS  •  KIM 1 PONY DUST"),"each friend's substance shows beside their dose");
            foreach(var (id,name,dose,took) in new[]{("alex","Alex",3,"ecstasy"),("lee","Lee",2,"weed"),("pat","Pat",1,"lsd"),("jo","Jo",4,"mushrooms"),("max","Max",1,"ketamine")}){Crew(s,id,name);Dose(s,id,dose,took);}
            Assert.That(FestivalHudText.Tripping(s,"you"),Is.EqualTo("SAM TRIPS  •  4 DOSES  •  TONGUE STAMPS\nDOSED  YOU 2 FUN GUYS  •  KIM 1 PONY DUST\nALEX 3 ROLLY POLLIES  •  LEE 2 COUCH LOCK\nPAT 1 TONGUE STAMPS  •  JO 4 FUN GUYS\nMAX 1 PONY DUST"),
                "a crew of eight reads two doses to a line");
            s.Players.Find(p=>p.Id=="max").Connected=false;
            Assert.That(FestivalHudText.Tripping(s,"you"),Does.Not.Contain("MAX"),"nobody lists a friend who left");

            var night=Level(1);Crew(night,"you","You");Crew(night,"sam","Sam","Spirit");Crew(night,"kim","Kim");
            night.TripperId="kim";Dose(night,"sam",3);Dose(night,"kim",1);
            Assert.That(FestivalHudText.Tripping(night,"you"),Is.EqualTo("KIM TRIPS  •  1 DOSE"),"before Night 2 only the tripper counts, even after a stand-in took over");
        }

        [Test] public void ASpiritNamesOnlyTheFriendsItCanSee()
        {
            // A spirit's view lists only spirits (FestivalSession.ViewFor): the living tripper is "a friend", the living's doses unlisted.
            var s=Level(3);Crew(s,"you","You","Spirit");Crew(s,"alex","Alex","Spirit");s.TripperId="sam";
            Dose(s,"you",2,"weed");Dose(s,"sam",4,"ecstasy");Dose(s,"alex",1,"lsd");Dose(s,"kim",3,"mushrooms");
            Assert.That(FestivalHudText.Tripping(s,"you"),Is.EqualTo("A FRIEND TRIPS  •  4 DOSES  •  ROLLY POLLIES\nDOSED  YOU 2 COUCH LOCK  •  ALEX 1 TONGUE STAMPS"));
        }

        [Test] public void TrustButVerifyShowsTheTripperOncePerLevel()
        {
            var s=Level(1);var you=Crew(s,"you","You");var sam=Crew(s,"sam","Sam");s.TripperId="sam";Dose(s,"sam",2);
            // HUD-7: the hint is folded into the tripper's job on the level briefing, which shows for the same window.
            Assert.That(FestivalHudText.Briefing(s,"sam"),Does.Contain("Trust, but verify"),"the tripper's one hint as the level starts");
            Assert.That(FestivalHudText.Briefing(s,"you"),Does.Not.Contain("Trust"),"the hint is the tripper's alone");
            s.ElapsedSeconds=FestivalHudText.BriefingSeconds-.1;
            Assert.That(FestivalHudText.Briefing(s,"sam"),Does.Contain("Trust, but verify"));
            s.ElapsedSeconds=FestivalHudText.BriefingSeconds;
            Assert.That(FestivalHudText.Briefing(s,"sam"),Does.Not.Contain("Trust"),"then it is gone for the rest of the level");
            s.Phase="Loading";s.ElapsedSeconds=0;
            Assert.That(FestivalHudText.Briefing(s,"sam"),Does.Not.Contain("Trust"),"it waits for play to start");
            // Once means once: the objective card does not repeat it, by night or by day.
            s.Phase="Playing";s.ElapsedSeconds=30;
            Assert.That(FestivalHudText.ObjectiveTitle(s,sam)+" "+FestivalHudText.ObjectiveDetail(s,sam),Does.Not.Contain("Trust"),"night");
            s.LevelIndex=0;
            Assert.That(FestivalHudText.ObjectiveTitle(s,sam)+" "+FestivalHudText.ObjectiveDetail(s,sam),Does.Not.Contain("Trust"),"day");
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Does.Not.Contain("Trust"));
        }

        [Test] public void OnlyTheTripperIsOfferedChecks()
        {
            var game=Day();var npc=Beside(game,out var tripper,out var sober);
            var mine=FestivalSession.ViewFor(game,tripper.Id);
            Assert.That(FestivalHudText.CheckTarget(mine,Me(mine,tripper.Id))?.Id,Is.EqualTo(npc.Id),"the tripper beside someone they have a vision about may check it");
            var theirs=FestivalSession.ViewFor(game,sober.Id);
            Assert.That(FestivalHudText.CheckTarget(theirs,Me(theirs,sober.Id)),Is.Null,"a sober friend beside the same festivalgoer is offered nothing");
            Assert.That(FestivalHudText.CheckTarget(mine,Me(mine,sober.Id)),Is.Null,"checks are the tripper's even where visions are known");
            var started=game.Execute(tripper.Id,new GameCommand{Id="hud_check",Kind="ConfirmChat",TargetId=npc.Id});
            Assert.That(started.Accepted,Is.True,"the sim takes the check the HUD offers: "+started.Reason);
            mine=FestivalSession.ViewFor(game,tripper.Id);
            Assert.That(FestivalHudText.CheckTarget(mine,Me(mine,tripper.Id)),Is.Null,"nothing more to offer while checking");
            game.Tick(FestivalSimulation.ConfirmChatSeconds+.1);
            mine=FestivalSession.ViewFor(game,tripper.Id);
            Assert.That(FestivalHudText.CheckTarget(mine,Me(mine,tripper.Id))?.Id,Is.Not.EqualTo(npc.Id),"a checked vision is not offered again");
        }

        [Test] public void AChecksReachAndTargetMatchTheSim()
        {
            var s=Level(0);var you=Crew(s,"you","You");s.TripperId="you";Dose(s,"you",2);
            var far=Npc(s,"far",2.4f,0);var near=Npc(s,"near",0,1.5f);Npc(s,"plain",0,-.5f);
            s.Visions.Add(new VisionState{Id="v1",Kind="Buyer",NpcId="far"});
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.SameAs(far),"within "+FestivalSimulation.ConfirmReach+" m, though someone without a vision stands nearer");
            far.X=2.6f;
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.Null,"out of reach");
            s.Visions.Add(new VisionState{Id="v2",Kind="Narc",NpcId="near"});far.X=2.4f;
            Npc(s,"mid",0,-2);s.Visions.Add(new VisionState{Id="v3",Kind="Buyer",NpcId="mid"});
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.SameAs(near),"the nearest of three, wherever the crowd lists them");
            s.Visions[1].Confirmed=true;s.Visions[2].Confirmed=true;
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.SameAs(far),"checked ones drop out");
            you.Life="Downed";
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.Null,"only on your feet");
            you.Life="Alive";s.Phase="Results";
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.Null,"only while playing");
        }

        // VISION-3: a client's view hides why a festivalgoer can't stop (whom they accuse, other players' interactions), so it
        // carries Busy. Beside them the HUD says they're busy instead of offering a check the host would refuse.
        [Test] public void AFestivalgoerTooBusyToCheckIsSaidNotOffered()
        {
            var game=Day();var npc=Beside(game,out var tripper,out var sober);
            bool Offered(string why)
            {
                var mine=FestivalSession.ViewFor(game,tripper.Id);var check=FestivalHudText.CheckTarget(mine,Me(mine,tripper.Id));
                Assert.That(check?.Id,Is.EqualTo(npc.Id),why+": the HUD points at the festivalgoer beside the tripper");
                return !FestivalSimulation.Engaged(mine,check);
            }
            npc.Mode="Accusing";npc.TargetId=sober.Id;
            var seen=FestivalSession.ViewFor(game,tripper.Id).Npcs.Find(n=>n.Id==npc.Id);
            Assert.That(seen.Mode=="Blending"&&seen.TargetId=="",Is.True,"setup: the tripper's view hides whom they accuse");
            Assert.That(Offered("accusing a friend"),Is.False,"accusing a friend, they're busy");
            Assert.That(game.Execute(tripper.Id,new GameCommand{Id="busy_accusing",Kind="ConfirmChat",TargetId=npc.Id}).Accepted,Is.False,"as the host says too");
            npc.Mode="Blending";npc.TargetId="";
            var dance=game.Execute(sober.Id,new GameCommand{Id="busy_dance",Kind="Dance",TargetId=npc.Id});
            Assert.That(dance.Accepted,Is.True,"setup: the sober friend dances with them: "+dance.Reason);
            Assert.That(Offered("dancing with a friend"),Is.False,"dancing with a friend, they're busy");
            Assert.That(game.Execute(tripper.Id,new GameCommand{Id="busy_dancing",Kind="ConfirmChat",TargetId=npc.Id}).Accepted,Is.False,"as the host says too");
            game.Execute(sober.Id,new GameCommand{Id="busy_stop",Kind="Cancel"});
            Assert.That(Offered("free again"),Is.True,"free again, the check is offered");
            Assert.That(game.Execute(tripper.Id,new GameCommand{Id="busy_free",Kind="ConfirmChat",TargetId=npc.Id}).Accepted,Is.True,"and the host takes it");
            Assert.That(FestivalHudText.BusyAction,Is.EqualTo("They're busy right now"));
        }

        // VISION-3: standing nearer Palm Mirage's VIP guard than someone you have a vision about no longer hides that check: the
        // guard's chat is offered only with no vision target in reach. A free vision target comes before a busy one.
        [Test] public void AVisionTargetComesBeforeTheGuardAndAFreeOneBeforeABusyOne()
        {
            var s=Level(0);var you=Crew(s,"you","You");s.TripperId="you";Dose(s,"you",2);
            var guard=Npc(s,"guard",.5f,0);guard.Twist=FestivalSimulation.VipGuard;
            var target=Npc(s,"target",0,2);s.Visions.Add(new VisionState{Id="v1",Kind="Buyer",NpcId="target"});
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.SameAs(target),"a vision target in reach before the nearer VIP guard");
            target.X=2.6f;
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.SameAs(guard),"with none in reach, the guard talks you past the rope");
            target.X=0;target.Busy=true;
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.SameAs(target),"a busy vision target still comes before the guard, and the HUD says they're busy");
            var free=Npc(s,"free",0,-2.2f);s.Visions.Add(new VisionState{Id="v2",Kind="Narc",NpcId="free"});
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.SameAs(free),"a free vision target before a nearer busy one");
            target.Busy=false;
            Assert.That(FestivalHudText.CheckTarget(s,you),Is.SameAs(target),"then the nearest free one");
        }

        // DIALOG-5: the chat check is a thread in the chat window: their opener, then the three questions as replies on 1-3.
        [Test] public void TheChatWindowAsksTheTrippersQuestions()
        {
            var game=Day();var npc=Beside(game,out var tripper,out _);
            var started=game.Execute(tripper.Id,new GameCommand{Id="hud_chat",Kind="ConfirmChat",TargetId=npc.Id});
            Assert.That(started.Accepted,Is.True,"setup: the tripper chats to check: "+started.Reason);
            var said=game.Interaction(tripper.InteractionId).Chat;
            var mine=FestivalSession.ViewFor(game,tripper.Id);var me=Me(mine,tripper.Id);var window=ChatWindow();
            Assert.That(window.Follow(mine,me,mine.SimulationSeconds,0,false),Is.True,"the tripper's chat check opens the window");
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener}),"their opener");
            Assert.That(window.Replies,Is.EqualTo(3),"then the three questions, as buttons");
            for(int i=0;i<3;i++)Assert.That(window.ReplyKey(i)+"  "+window.ReplyLabel(i),Is.EqualTo((i+1)+"  "+said.Questions[i]),"on 1-3");
            Assert.That(FestivalChatWindow.TakesReplies(mine,me),Is.True,"so 1-3 ask them");
            window.Pick(1);window.Follow(mine,me,mine.SimulationSeconds,FestivalChatWindow.Typed(said.Answers[1])+.01f,false);
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener,"> "+said.Questions[1],"< "+said.Answers[1]}),"the picked question gets their answer");
            foreach(var viewer in game.State.Players)
            {
                if(viewer.Id==tripper.Id)continue;
                var view=FestivalSession.ViewFor(game,viewer.Id);
                Assert.That(FestivalChatWindow.LiveChat(view,Me(view,viewer.Id)),Is.Null,viewer.Id+" has no chat to read");
                Assert.That(FestivalChatWindow.TakesReplies(view,Me(view,viewer.Id)),Is.False,viewer.Id+"'s 1-3 still equip");
            }
            game.Tick(FestivalSimulation.ConfirmChatSeconds+.1);
            mine=FestivalSession.ViewFor(game,tripper.Id);
            // DIALOG-6: an ended chat stays up only until its last answer is read (below), long before 30 s.
            Assert.That(window.Follow(mine,Me(mine,tripper.Id),mine.SimulationSeconds,30,false)||window.Open,Is.False,"the window closes after the chat");
            var dance=game.Execute(tripper.Id,new GameCommand{Id="hud_dance",Kind="Dance",TargetId=npc.Id});
            Assert.That(dance.Accepted,Is.True,"setup: the tripper dances with them: "+dance.Reason);
            mine=FestivalSession.ViewFor(game,tripper.Id);
            Assert.That(FestivalChatWindow.TakesReplies(mine,Me(mine,tripper.Id))||window.Follow(mine,Me(mine,tripper.Id),mine.SimulationSeconds,31,false),Is.False,"a dance has no questions to pick");
        }

        // DIALOG-6: what they say stays up long enough to read. Matt, 2026-10-01: "when talking to people, sometimes their text
        // disappears immediately." The window follows the tripper's own view (FestivalSession.ViewFor), which drops a chat the
        // moment it ends, on the HUD's clock (`now`).
        static bool Follow(FestivalChatWindow window,FestivalSimulation game,PlayerState tripper,float now){var view=FestivalSession.ViewFor(game,tripper.Id);return window.Follow(view,Me(view,tripper.Id),view.SimulationSeconds,now,false);}
        // The tripper chats with npc and asks question 1 at `now`: their answer.
        static string Ask(FestivalSimulation game,PlayerState tripper,NpcState npc,FestivalChatWindow window,float now,string id)
        {
            var started=game.Execute(tripper.Id,new GameCommand{Id=id,Kind="ConfirmChat",TargetId=npc.Id});
            Assert.That(started.Accepted,Is.True,"setup: the tripper chats to check: "+started.Reason);
            var said=game.Interaction(tripper.InteractionId).Chat;
            Assert.That(Follow(window,game,tripper,now),Is.True,"setup: the chat window follows the chat");
            Assert.That(window.Pick(0),Is.True,"setup: 1 asks the first question");
            return said.Answers[0];
        }
        static void Leave(FestivalSimulation game,PlayerState tripper,string id)=>Assert.That(game.Execute(tripper.Id,new GameCommand{Id=id,Kind="Cancel"}).Accepted,Is.True,"setup: the tripper walks off the chat");
        // After the chat ended, the window from `over` on: up read-only, headed CHAT OVER, while 1-3 equip, until `answer` (asked
        // at `asked`) has been whole on screen for its reading time; then it fades out and hides for good.
        void ExpectItStaysUntilRead(FestivalSimulation game,PlayerState tripper,FestivalChatWindow window,string answer,float asked,float over)
        {
            var view=FestivalSession.ViewFor(game,tripper.Id);var me=Me(view,tripper.Id);
            Assert.That(FestivalChatWindow.LiveChat(view,me),Is.Null,"setup: the tripper's view has no chat");
            Assert.That(window.Follow(view,me,view.SimulationSeconds,over,false),Is.False,"the chat is over");
            Assert.That(window.Open,Is.True,"but the window stays up");
            Assert.That(window.Header,Does.StartWith("CHAT OVER"),"headed as over");
            Assert.That(window.Replies==3&&!window.CanReply(1)&&!window.CanReply(2),Is.True,"read-only: no question left to ask");
            Assert.That(window.Pick(1)||window.Pick(2),Is.False,"so 2 and 3 pick nothing");
            Assert.That(FestivalChatWindow.TakesReplies(view,me),Is.False,"and 1-3 equip again");
            float read=asked+FestivalChatWindow.Typed(answer)+FestivalChatWindow.Lingers(answer);
            window.Follow(view,me,view.SimulationSeconds,read-.05f,false);
            Assert.That(window.Open&&window.Shown(window.Bubbles-1)==answer,Is.True,"their answer is up, whole, until it has been for its reading time");
            window.Follow(view,me,view.SimulationSeconds,read+FestivalChatWindow.FadeSeconds/2,false);
            var fade=window.GetComponent<UnityEngine.CanvasGroup>();
            Assert.That(window.Open&&fade!=null&&fade.alpha>.1f&&fade.alpha<.9f,Is.True,"then the window fades");
            window.Follow(view,me,view.SimulationSeconds,read+FestivalChatWindow.FadeSeconds+.01f,false);
            Assert.That(window.Open,Is.False,"and hides");
            window.Follow(view,me,view.SimulationSeconds,read+10,false);
            Assert.That(window.Open,Is.False,"for good");
        }

        // A chat check that completes .1 s after a question was asked leaves the answer up to read.
        [Test] public void AChatThatEndsStaysUpUntilItsLastAnswerIsRead()
        {
            var game=Day();var npc=Beside(game,out var tripper,out _);var window=ChatWindow();
            var started=game.Execute(tripper.Id,new GameCommand{Id="hud_linger",Kind="ConfirmChat",TargetId=npc.Id});
            Assert.That(started.Accepted,Is.True,"setup: the tripper chats to check: "+started.Reason);
            string id=tripper.InteractionId;var said=game.Interaction(id).Chat;
            game.Tick(FestivalSimulation.ConfirmChatSeconds-.1);
            Assert.That(Follow(window,game,tripper,20),Is.True,"setup: .1 s before the chat ends");
            Assert.That(window.Pick(0),Is.True,"setup: 1 asks the first question");
            game.Tick(.12);
            Assert.That(game.Interaction(id).Status,Is.EqualTo("Complete"),"setup: the chat ended .1 s after the question");
            ExpectItStaysUntilRead(game,tripper,window,said.Answers[0],20,20.1f);
        }

        [Test] public void ACancelledChatStaysUpTheSameWay()
        {
            var game=Day();var npc=Beside(game,out var tripper,out _);var window=ChatWindow();
            string answer=Ask(game,tripper,npc,window,20,"hud_linger_cancel");
            game.Tick(.1);Leave(game,tripper,"hud_linger_leave");
            ExpectItStaysUntilRead(game,tripper,window,answer,20,20.1f);
        }

        // Walking more than 4 m from them (as the host ends a chat), starting anything else, or G (FestivalHud) closes it at once.
        [Test] public void WalkingAwayOrDoingAnythingElseClosesAnEndedChat()
        {
            var game=Day();var npc=Beside(game,out var tripper,out _);var window=ChatWindow();
            Ask(game,tripper,npc,window,20,"hud_walk");Leave(game,tripper,"hud_walk_leave");
            tripper.X=npc.X+3.9f;tripper.Z=npc.Z;Follow(window,game,tripper,20.2f);
            Assert.That(window.Open,Is.True,"setup: the ended chat is up, 3.9 m from them");
            tripper.X=npc.X+4.1f;Follow(window,game,tripper,20.3f);
            Assert.That(window.Open,Is.False,"4.1 m from them, it closes");
            tripper.X=npc.X+.3f;Follow(window,game,tripper,20.4f);
            Assert.That(window.Open,Is.False,"and stays closed when you step back");

            Ask(game,tripper,npc,window,30,"hud_next");Leave(game,tripper,"hud_next_leave");Follow(window,game,tripper,30.1f);
            Assert.That(window.Open,Is.True,"setup: the next ended chat is up");
            var dance=game.Execute(tripper.Id,new GameCommand{Id="hud_next_dance",Kind="Dance",TargetId=npc.Id});
            Assert.That(dance.Accepted,Is.True,"setup: the tripper dances with them: "+dance.Reason);
            Follow(window,game,tripper,30.2f);
            Assert.That(window.Open,Is.False,"starting a dance closes it");
            Leave(game,tripper,"hud_next_dance_leave");

            tripper.X=npc.X+.3f;tripper.Z=npc.Z;Ask(game,tripper,npc,window,40,"hud_last");Leave(game,tripper,"hud_last_leave");Follow(window,game,tripper,40.1f);
            Assert.That(window.Open&&window.Lingering,Is.True,"setup: the last ended chat is up");
            window.Dismiss();
            Assert.That(window.Open||window.Lingering,Is.False,"G closes it");
            Follow(window,game,tripper,40.2f);
            Assert.That(window.Open,Is.False,"for good");
        }

        // POLO-4: talking your way past the VIP rope is its own chat, the guard's opener and doorman line with nothing to ask, so
        // it never reads like a check. DIALOG-5: in the chat window, headed for the rope, with no reply buttons, and 1-3 still
        // equip. The tripper checking a vision of the guard still gets the check's questions.
        [Test] public void ASoberChatWithTheGuardIsItsOwnCard()
        {
            var game=Day();var guard=game.State.Npcs.Find(n=>n.Twist==FestivalSimulation.VipGuard);
            Assert.That(guard,Is.Not.Null,"setup: Palm Mirage's VIP rope has a guard");
            var tripper=game.Player(game.State.TripperId);var sober=game.State.Players.Find(p=>p!=tripper);
            sober.X=guard.X+.6f;sober.Z=guard.Z;
            var started=game.Execute(sober.Id,new GameCommand{Id="rope_chat",Kind="ConfirmChat",TargetId=guard.Id});
            Assert.That(started.Accepted,Is.True,"setup: the sober friend talks to the guard: "+started.Reason);
            var said=game.Interaction(sober.InteractionId).Chat;Assert.That(said.Doorman,Is.Not.Empty,"setup: the guard has a doorman line");
            // Through a snapshot, as the sober player's client gets it.
            var view=UnityEngine.JsonUtility.FromJson<RoundState>(UnityEngine.JsonUtility.ToJson(FestivalSession.ViewFor(game,sober.Id)));var me=Me(view,sober.Id);
            var window=ChatWindow();
            Assert.That(window.Follow(view,me,view.SimulationSeconds,0,false),Is.True,"the rope chat opens the chat window");
            Assert.That(window.Header,Is.EqualTo("VIP GUARD  •  TALKING YOUR WAY IN"),"headed as the rope chat");
            window.Follow(view,me,view.SimulationSeconds,FestivalChatWindow.Typed(said.Doorman)+.01f,false);
            Assert.That(Thread(window),Is.EqualTo(new[]{"< "+said.Opener,"< "+said.Doorman}),"the guard's opener, then their doorman line");
            Assert.That(window.Replies,Is.Zero,"nothing to ask, so no reply buttons");
            Assert.That(FestivalChatWindow.TakesReplies(view,me),Is.False,"and 1-3 still equip");
            Assert.That(window.Pick(0)||window.Pick(1)||window.Pick(2),Is.False,"1-3 pick nothing");
            Assert.That(window.Bubbles,Is.EqualTo(2));

            game.Tick(FestivalSimulation.ConfirmChatSeconds+.1);
            game.State.Visions.Add(new VisionState{Id="planted",Kind="Buyer",NpcId=guard.Id,Tell=true});
            tripper.X=guard.X+.6f;tripper.Z=guard.Z;
            started=game.Execute(tripper.Id,new GameCommand{Id="rope_check",Kind="ConfirmChat",TargetId=guard.Id});
            Assert.That(started.Accepted,Is.True,"setup: the tripper chats to check their vision of the guard: "+started.Reason);
            var mine=FestivalSession.ViewFor(game,tripper.Id);
            Assert.That(window.Follow(mine,Me(mine,tripper.Id),mine.SimulationSeconds,10,false),Is.True);
            Assert.That(window.Header+" / "+window.Replies,Is.EqualTo("VIP GUARD / 3"),"a chat that checks a vision is still a check");
        }

        [Test] public void ACheckDanceSaysItIsACheck()
        {
            Assert.That(FestivalHudText.RhythmTitle("ConfirmDance"),Is.EqualTo("CHECK DANCE  /  FOUR-LANE"));
            Assert.That(FestivalHudText.RhythmTitle("Dance"),Is.EqualTo("DANCE  /  FOUR-LANE"));
            Assert.That(FestivalHudText.CheckDanceAction,Is.EqualTo("Check by dancing  •  F CHECK BY CHAT ("+FestivalSimulation.ConfirmChatSeconds+" s)"),"E dances, F chats, as beside anyone else");
            Assert.That(FestivalHudText.CheckChatAction,Is.EqualTo("Check by chatting ("+FestivalSimulation.ConfirmChatSeconds+" s, safe)"));
        }
    }
}
