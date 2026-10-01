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
            Assert.That(FestivalHudText.TrustLine(s,"sam"),Is.EqualTo("Trust, but verify."),"the tripper's one hint as the level starts");
            Assert.That(FestivalHudText.TrustLine(s,"you"),Is.Empty,"the hint is the tripper's alone");
            s.ElapsedSeconds=FestivalHudText.TrustSeconds-.1;
            Assert.That(FestivalHudText.TrustLine(s,"sam"),Is.EqualTo("Trust, but verify."));
            s.ElapsedSeconds=FestivalHudText.TrustSeconds;
            Assert.That(FestivalHudText.TrustLine(s,"sam"),Is.Empty,"then it is gone for the rest of the level");
            s.Phase="Loading";s.ElapsedSeconds=0;
            Assert.That(FestivalHudText.TrustLine(s,"sam"),Is.Empty,"it waits for play to start");
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

        [Test] public void TheChatPickerAsksTheTrippersQuestions()
        {
            var game=Day();var npc=Beside(game,out var tripper,out _);
            var started=game.Execute(tripper.Id,new GameCommand{Id="hud_chat",Kind="ConfirmChat",TargetId=npc.Id});
            Assert.That(started.Accepted,Is.True,"setup: the tripper chats to check: "+started.Reason);
            var said=game.Interaction(tripper.InteractionId).Chat;
            var mine=FestivalSession.ViewFor(game,tripper.Id);var me=Me(mine,tripper.Id);
            string asking="CHECKING BY CHAT  •  1–3 ASK\n\""+said.Opener+"\"\n1  "+said.Questions[0]+"\n2  "+said.Questions[1]+"\n3  "+said.Questions[2];
            Assert.That(FestivalHudText.ChatPicker(mine,me,-1),Is.EqualTo(asking),"their opener, then the three questions on 1-3");
            Assert.That(FestivalHudText.ChatPicker(mine,me,1),Is.EqualTo("CHECKING BY CHAT  •  1–3 ASK\n\""+said.Opener+"\"\n1  "+said.Questions[0]+"\n2  "+said.Questions[1]+"\n      \""+said.Answers[1]+"\"\n3  "+said.Questions[2]),
                "the picked question gets their answer");
            foreach(var viewer in game.State.Players)
            {
                if(viewer.Id==tripper.Id)continue;
                var view=FestivalSession.ViewFor(game,viewer.Id);
                Assert.That(FestivalHudText.ChatPicker(view,Me(view,viewer.Id),0),Is.Empty,viewer.Id+" has no chat to read");
            }
            game.Tick(FestivalSimulation.ConfirmChatSeconds+.1);
            mine=FestivalSession.ViewFor(game,tripper.Id);
            Assert.That(FestivalHudText.ChatPicker(mine,Me(mine,tripper.Id),1),Is.Empty,"the picker closes with the chat");
            var dance=game.Execute(tripper.Id,new GameCommand{Id="hud_dance",Kind="Dance",TargetId=npc.Id});
            Assert.That(dance.Accepted,Is.True,"setup: the tripper dances with them: "+dance.Reason);
            mine=FestivalSession.ViewFor(game,tripper.Id);
            Assert.That(FestivalHudText.ChatPicker(mine,Me(mine,tripper.Id),0),Is.Empty,"a dance has no questions to pick");
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
