using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// TRIP-3: the tripper checks a vision in person. A chat takes 5 s, is safe, and the festivalgoer's answers carry their
// role's tells; a dance is four quick notes whose verdict lands sooner, but a score under .6 draws the watchers'
// suspicion. Either way the truth reaches only the tripper, and a true clue holder moves the night's trail on.
public static class ConfirmTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Confirm: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="")=>s.Execute(id,new GameCommand{Id="confirm"+(sequence++),Kind=kind,TargetId=target});
    static PlayerState Tripper(FestivalSimulation s)=>s.Player(s.State.TripperId);
    static PlayerState Friend(FestivalSimulation s)=>s.State.Players.Find(p=>p.Id!=s.State.TripperId);
    static int Dose(PlayerState p)=>p.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect)?.Intensity??0;
    static NpcState Npc(FestivalSimulation s,string id)=>s.State.Npcs.Find(n=>n.Id==id);
    static List<VisionState> About(FestivalSimulation s,NpcState n)=>s.State.Visions.FindAll(v=>v.NpcId==n.Id);
    static VisionState Seen(FestivalSimulation s,PlayerState viewer,VisionState v)=>FestivalSimulation.VisibleVisions(s.State,viewer.Id).Find(x=>x.Id==v.Id);
    static double Heat(NpcState n,PlayerState p)=>n.Observers.Find(o=>o.PlayerId==p.Id)?.Suspicion??0;
    static void Watch(FestivalSimulation s,NpcState n,PlayerState p,double suspicion){var o=n.Observers.Find(x=>x.PlayerId==p.Id);if(o==null)n.Observers.Add(o=new ObserverState{PlayerId=p.Id});o.Suspicion=suspicion;o.LastSeenSeconds=s.State.SimulationSeconds;}
    // Stand p in front of n (a metre away unless told otherwise), in n's view.
    static void Beside(PlayerState p,NpcState n,double metres=1){double yaw=n.Yaw*Math.PI/180;p.X=n.X+(float)(metres*Math.Sin(yaw));p.Z=n.Z+(float)(metres*Math.Cos(yaw));}

    // Two friends ready up at camp, the wheels spin and the crew loads into the crowd.
    static FestivalSimulation Start(int seed,int level,int festival=0)
    {
        var s=new FestivalSimulation(seed);s.AddPlayer("p0","P0");s.AddPlayer("p1","P1");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing"&&s.State.Visions.Exists(v=>v.NpcId!=""),"setup: the crew is at the festival and the tripper has visions of people");
        // POLO-1: nobody here plays a Palm Mirage twist (no camera, no VIP guard), so only the check itself moves suspicion.
        foreach(var n in s.State.Npcs)n.Twist="";
        return s;
    }
    // The first level from `seed` on whose spin the tripper takes exactly `dose`; `seed` moves past it.
    static FestivalSimulation Dosed(int dose,int level,ref int seed){for(;seed<5000;seed++){var s=Start(seed,level);if(Dose(Tripper(s))==dose){seed++;return s;}}throw new Exception("no seed spins dose "+dose);}
    // Tick until the interaction settles.
    static void Settle(FestivalSimulation s,InteractionState i){for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);}
    // The tripper chats with n from beside them until the chat ends.
    static InteractionState Chat(FestivalSimulation s,NpcState n)
    {
        var p=Tripper(s);Beside(p,n);var started=Act(s,p.Id,"ConfirmChat",n.Id);
        Check(started.Accepted,"setup: the tripper starts a chat with "+n.Id+": "+started.Reason);
        var chat=s.Interaction(p.InteractionId);Settle(s,chat);return chat;
    }
    // The tripper dances with n, hitting the first `perfect` notes on the beat through the Rhythm command, the next `good`
    // notes a tenth of a second late, and missing the rest.
    static InteractionState Dance(FestivalSimulation s,NpcState n,int perfect,int good)
    {
        var p=Tripper(s);Beside(p,n);var started=Act(s,p.Id,"ConfirmDance",n.Id);
        Check(started.Accepted,"setup: the tripper starts a dance with "+n.Id+": "+started.Reason);
        var dance=s.Interaction(p.InteractionId);var notes=RhythmChart.Create(dance.ChartSeed,dance.NoteCount,dance.BeatSeconds).Notes;int next=0;
        for(int guard=0;dance.Status=="Active"&&guard<200;guard++)
        {
            double now=s.State.SimulationSeconds-dance.StartSeconds;
            for(;next<Math.Min(notes.Count,perfect+good);next++)
            {
                double at=notes[next].TimeSeconds+(next<perfect?0:.1);if(at>now)break;
                var step=s.Execute(p.Id,new GameCommand{Id="confirm"+(sequence++),Kind="Rhythm",Direction=notes[next].Direction,TimeSeconds=at});
                Check(step.Accepted,"setup: the dance takes step "+next+": "+step.Reason);
            }
            s.Tick(.1);
        }
        return dance;
    }

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{OnlyTheTripperChecks,AChecksNeedsSomethingToCheck,AChatTakesFiveSafeSeconds,TheChatCarriesTheirTells,
            ADanceIsQuickButRisky,EveryStepShowsOnTheDancer,TheTrailMovesOnlyOnATrueHolder,WalkingAwayEndsTheChat,AChatSurvivesASnapshot})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" confirm test(s) failed:\n"+string.Join("\n",failures));
    }

    static void OnlyTheTripperChecks()
    {
        var s=Start(3,0);var friend=Friend(s);var npc=Npc(s,s.State.Visions.First(v=>v.NpcId!="").NpcId);Beside(friend,npc);
        foreach(var kind in new[]{"ConfirmChat","ConfirmDance"})
        {
            var refused=Act(s,friend.Id,kind,npc.Id);
            Check(!refused.Accepted&&refused.Reason.Contains("tripper")&&friend.InteractionId=="",kind+": a sober friend beside the festivalgoer cannot check the tripper's vision: "+refused.Reason);
        }
        s.Tick(6);Check(About(s,npc).TrueForAll(v=>!v.Confirmed),"so nothing is checked");
        var talker=s.State.Npcs.Find(n=>n.Kind=="Wook"&&n.CanTalk);Check(talker!=null,"setup: someone in the crowd wants to chat");Beside(friend,talker);
        var talk=Act(s,friend.Id,"Talk",talker.Id);
        Check(talk.Accepted&&friend.NpcSpeech!="","the sober friend keeps the festival barks: "+talk.Reason);
    }

    static void AChecksNeedsSomethingToCheck()
    {
        var s=Start(4,0);var tripper=Tripper(s);var friend=Friend(s);
        var nobody=s.State.Npcs.Find(n=>n.Kind=="Wook"&&About(s,n).Count==0);Check(nobody!=null,"setup: someone in the crowd has no vision on them");
        Beside(tripper,nobody);var refused=Act(s,tripper.Id,"ConfirmChat",nobody.Id);
        Check(!refused.Accepted&&tripper.InteractionId=="","the tripper has nothing to check on someone without a vision: "+refused.Reason);
        var cop=s.State.Npcs.Find(n=>n.Kind=="Cop");Beside(tripper,cop);
        Check(!Act(s,tripper.Id,"ConfirmDance",cop.Id).Accepted,"nor on a cop");

        var npc=Npc(s,s.State.Visions.First(v=>v.NpcId!="").NpcId);Beside(tripper,npc,2.6);
        Check(!Act(s,tripper.Id,"ConfirmChat",npc.Id).Accepted,"the tripper must stand within 2.5 m");
        Beside(tripper,npc);
        foreach(var mode in new[]{"Accusing","Swarming"}){npc.Mode=mode;Check(!Act(s,tripper.Id,"ConfirmChat",npc.Id).Accepted,"a festivalgoer who is "+mode+" won't stop to be checked");}
        npc.Mode="Blending";
        Beside(friend,npc);Check(Act(s,friend.Id,"Dance",npc.Id).Accepted,"setup: the friend dances with them first");
        var busy=Act(s,tripper.Id,"ConfirmChat",npc.Id);Check(!busy.Accepted&&tripper.InteractionId=="","they are busy with the friend: "+busy.Reason);
        Settle(s,s.Interaction(friend.InteractionId));friend.X=-30;

        Chat(s,npc);Check(About(s,npc).TrueForAll(v=>v.Confirmed),"setup: the tripper checks them");
        var again=Act(s,tripper.Id,"ConfirmDance",npc.Id);Check(!again.Accepted,"once checked there is nothing left to check: "+again.Reason);
    }

    static void AChatTakesFiveSafeSeconds()
    {
        var s=Start(5,0);var tripper=Tripper(s);var friend=Friend(s);
        foreach(var truth in new[]{true,false})
        {
            var vision=s.State.Visions.Find(v=>v.NpcId!=""&&v.IsTrue==truth&&!v.Confirmed);Check(vision!=null,"setup: a "+(truth?"true":"false")+" vision of someone");
            var npc=Npc(s,vision.NpcId);Beside(tripper,npc);Watch(s,npc,tripper,30);
            var started=Act(s,tripper.Id,"ConfirmChat",npc.Id);Check(started.Accepted,"the tripper chats with someone they have a vision about: "+started.Reason);
            var chat=s.Interaction(tripper.InteractionId);Check(chat.Kind=="ConfirmChat"&&chat.PlayerId==tripper.Id,"a chat under way");
            s.Tick(4.9);
            Check(chat.Status=="Active"&&!vision.Confirmed&&!Seen(s,tripper,vision).Confirmed,"4.9 s in, the chat is still going and nothing is revealed");
            s.Tick(.2);
            Check(chat.Status=="Complete"&&About(s,npc).TrueForAll(v=>v.Confirmed),"after 5 s every vision about them is checked");
            var seen=Seen(s,tripper,vision);
            Check(seen.Confirmed&&seen.IsTrue==truth,"and the tripper learns the "+(truth?"true":"false")+" vision is "+(truth?"true":"false"));
            Check(FestivalSimulation.VisibleVisions(s.State,friend.Id).Count==0,"the friend learns nothing");
            Check(Heat(npc,tripper)==30,"a chat is safe: suspicion stays at 30, got "+Heat(npc,tripper));
        }
    }

    static void TheChatCarriesTheirTells()
    {
        for(int festival=0;festival<Festivals.Count;festival++)
        {
            FestivalSimulation s=null;NpcState narc=null;
            for(int seed=0;narc==null;seed++){Check(seed<200,"setup: some level shows a narc");s=Start(seed,0,festival);narc=s.State.Npcs.Find(n=>n.Role=="Narc"&&About(s,n).Count>0);}
            var other=s.State.Npcs.Find(n=>n.Role!="Narc"&&About(s,n).Count>0);
            foreach(var npc in new[]{narc,other})
            {
                string where=Festivals.Name(festival)+" "+npc.Role;
                Beside(Tripper(s),npc);Check(Act(s,Tripper(s).Id,"ConfirmChat",npc.Id).Accepted,where+": setup: the tripper chats");
                var chat=s.Interaction(Tripper(s).InteractionId);var said=chat.Chat;
                Check(said!=null&&said.Opener!=""&&said.Questions.Length==3&&said.Answers.Length==3&&said.Questions.Concat(said.Answers).All(line=>!string.IsNullOrWhiteSpace(line)),where+": an opener, three questions and three answers");
                var expected=DialogueGrammar.Build(npc.Role,DialogueGrammar.PersonaFor(npc.Id),festival,chat.ChartSeed);
                Check(said.Opener==expected.Opener&&said.Questions.SequenceEqual(expected.Questions)&&said.Answers.SequenceEqual(expected.Answers),where+": DIALOG-1's conversation for their role, their persona and this festival");
                Check(said.Answers.Any(DialogueGrammar.HasNarcTell)==(npc.Role=="Narc"),where+": cop-speak slips out of narcs and nobody else: "+string.Join(" | ",said.Answers));
                Settle(s,chat);
            }
        }
    }

    static void ADanceIsQuickButRisky()
    {
        var s=Start(6,0);var tripper=Tripper(s);
        var vision=s.State.Visions.Find(v=>v.NpcId!="");var npc=Npc(s,vision.NpcId);Beside(tripper,npc);
        Check(Act(s,tripper.Id,"ConfirmDance",npc.Id).Accepted,"the tripper dances with someone they have a vision about");
        var dance=s.Interaction(tripper.InteractionId);var notes=RhythmChart.Create(dance.ChartSeed,dance.NoteCount,dance.BeatSeconds).Notes;
        Check(dance.Kind=="ConfirmDance"&&notes.Count==4&&Math.Abs(notes[3].TimeSeconds+dance.BeatSeconds-notes[0].TimeSeconds-2)<1e-9,"four steps taking 2 s");
        var witnesses=dance.WitnessIds.ConvertAll(id=>Npc(s,id));Check(witnesses.Contains(npc),"their dance partner watches");
        s.Tick(4.6);Check(dance.Status=="Active"&&!vision.Confirmed,"4.6 s in, the dance is still being judged");
        s.Tick(.3);Check(dance.Status=="Complete"&&vision.Confirmed&&Seen(s,tripper,vision).IsTrue==vision.IsTrue,"its verdict lands before a chat's 5 s and reveals the truth to the tripper");
        Check(witnesses.TrueForAll(n=>Heat(n,tripper)>0),"missing every step raises every witness's suspicion");

        double After(int perfect,int good,out InteractionState done)
        {
            var level=Start(6,0);var v=level.State.Visions.Find(x=>x.NpcId!="");var partner=Npc(level,v.NpcId);Beside(Tripper(level),partner);Watch(level,partner,Tripper(level),30);
            done=Dance(level,partner,perfect,good);Check(done.Status=="Complete"&&v.Confirmed,"a dance scoring "+done.Score+" still checks the vision");
            return Heat(partner,Tripper(level));
        }
        double heat=After(1,2,out var low);
        Check(Math.Abs(low.Score-.55)<1e-9&&heat>30,"a .55 dance (one step on the beat, two late, one missed) draws suspicion: 30 -> "+heat);
        heat=After(2,1,out var fair);
        Check(Math.Abs(fair.Score-.65)<1e-9&&heat<30,"a .65 dance calms the watchers like any good dance: 30 -> "+heat);
        heat=After(4,0,out var perfect);
        Check(perfect.Score==1&&heat<30,"a perfect dance calms them most: 30 -> "+heat);
    }

    // Friends watch a check dance like any other: each step the tripper takes shows on their character.
    static void EveryStepShowsOnTheDancer()
    {
        var s=Start(10,0);var tripper=Tripper(s);var npc=Npc(s,s.State.Visions.First(v=>v.NpcId!="").NpcId);
        var dance=Dance(s,npc,4,0);Check(dance.Score==1,"setup: a perfect check dance");
        var last=RhythmChart.Create(dance.ChartSeed,dance.NoteCount,dance.BeatSeconds).Notes[3];
        Check(tripper.VisualDanceStepSequence==4&&tripper.VisualDanceStepDirection==last.Direction,"all four steps showed, the last one "+last.Direction+"; got "+tripper.VisualDanceStepSequence+" steps, last "+tripper.VisualDanceStepDirection);
    }

    static void TheTrailMovesOnlyOnATrueHolder()
    {
        int seed=0;var s=Dosed(2,1,ref seed);var tripper=Tripper(s);var chain=new List<string>(s.State.ClueChain);
        Check(chain.Count==2,"setup: Palm Mirage Night 1 has a two-link trail");
        var fake=s.State.Visions.Find(v=>v.Kind=="Clue"&&!v.IsTrue);Check(fake!=null,"setup: at dose 2 a fake clue holder shows up");
        Chat(s,Npc(s,fake.NpcId));
        Check(s.State.CluesRead==0&&!s.State.GateOpened&&Seen(s,tripper,fake).Confirmed&&!Seen(s,tripper,fake).IsTrue,"checking a fake clue holder shows it false, and the trail stays put");
        Dance(s,Npc(s,chain[0]),4,0);
        Check(s.State.CluesRead==1&&!s.State.GateOpened,"dancing with the real first clue holder moves the trail on");
        // VISION-3: the found link's clue says TRUE a few seconds more, beside the next link's.
        var next=s.State.Visions.Find(v=>v.Kind=="Clue"&&v.IsTrue&&!v.Confirmed);Check(next!=null&&next.NpcId==chain[1],"to the next link's holder, unchecked");
        Check(s.State.Visions.Exists(v=>v.NpcId==chain[0]&&v.Kind=="Clue"&&v.IsTrue&&v.Confirmed),"while the first link's clue still says TRUE");
        Chat(s,Npc(s,chain[1]));
        Check(s.State.CluesRead==2&&s.State.GateOpened,"checking the last clue holder opens the way to the lost friend");
        var friend=Friend(s);friend.X=s.State.FriendPosition.X;friend.Z=s.State.FriendPosition.Z;
        Check(Act(s,friend.Id,"FindFriend").Accepted,"and the crew can find them");
    }

    static void WalkingAwayEndsTheChat()
    {
        var s=Start(8,0);var tripper=Tripper(s);var vision=s.State.Visions.Find(v=>v.NpcId!="");var npc=Npc(s,vision.NpcId);
        Beside(tripper,npc);Watch(s,npc,tripper,30);Check(Act(s,tripper.Id,"ConfirmChat",npc.Id).Accepted,"setup: the tripper starts a chat");
        var chat=s.Interaction(tripper.InteractionId);s.Tick(2);tripper.X=npc.X+5;tripper.Z=npc.Z;s.Tick(.1);
        Check(chat.Status!="Active"&&chat.Status!="Complete"&&tripper.InteractionId=="","walking 5 m away ends the chat ("+chat.Status+")");
        s.Tick(4);Check(!vision.Confirmed,"unfinished, it checks nothing");
        Check(Heat(npc,tripper)==30,"and walking off a chat is still safe");
    }

    static void AChatSurvivesASnapshot()
    {
        var s=Start(9,0);var tripper=Tripper(s);var vision=s.State.Visions.Find(v=>v.NpcId!="");var npc=Npc(s,vision.NpcId);
        Beside(tripper,npc);Check(Act(s,tripper.Id,"ConfirmChat",npc.Id).Accepted,"setup: the tripper starts a chat");s.Tick(2);
        var said=s.Interaction(tripper.InteractionId).Chat;
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        var chat=restored.Interaction(restored.Player(tripper.Id).InteractionId);
        Check(chat!=null&&chat.Chat.Opener==said.Opener&&chat.Chat.Answers.SequenceEqual(said.Answers),"a chat in progress keeps its conversation through a snapshot");
        restored.Tick(3.1);Check(chat.Status=="Complete"&&restored.State.Visions.Find(v=>v.Id==vision.Id).Confirmed,"and finishes after it");
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        foreach(var i in legacy["Interactions"].AsArray())i.AsObject().Remove("Chat");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.Interactions.TrueForAll(i=>i.Chat!=null&&i.Chat.Opener==""),"a snapshot from before chats restores with empty conversations");
    }
}
