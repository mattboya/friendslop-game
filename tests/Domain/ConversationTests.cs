using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// DIALOG-3: selling, buying, chatting and talking to security are conversations, not dances. Each is three turns: they say a
// line and you pick one of three replies (1-3) within ReplySeconds, or say nothing. One reply is smooth, one so-so and one
// awkward, by the taste of the person you're talking to, which their opener hints at; a dose garbles some. The average of
// the turns is the talk's score, and settles it as the dance did. A wary festivalgoer asks for a dance first.
public static class ConversationTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Conversation: "+message);}
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="",int amount=0,int direction=0)=>s.Execute(id,new GameCommand{Id="talk"+(sequence++),Kind=kind,TargetId=target,ItemId=item,Amount=amount,Direction=direction});
    const int Smooth=ConversationLines.Smooth,SoSo=ConversationLines.SoSo,Awkward=ConversationLines.Awkward,Silence=ConversationLines.Silence;
    static readonly string[] Kinds={"Sale","Buy","Conversation","Police"};

    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{EachTalkIsThreeTurnsOfThreeReplies,ASeedRepeatsItsTalk,TheOpenerAndTheRepliesFollowThePerson,
            ATalkScoresTheAverageOfItsReplies,ALateOrPastReplyIsRefused,TheScoreSettlesTheTalkAsTheDanceDid,ADoseGarblesReplies,
            AWaryFestivalgoerAsksForADanceFirst,SomeRaversAndBurnersAlwaysDanceFirst,EveryLineIsCleanAndShort,AnOlderSnapshotRestores,
            ATalkReadsThePerson,AClueHolderNamesALandmark,OnlyAFestivalgoerTalkLeavesAHunch,ANarcSellerTalksAndFinishingTheBuyBusts})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" conversation test(s) failed:\n"+string.Join("\n",failures));
    }

    // A festivalgoer id that talks like this persona (DialogueGrammar.PersonaFor goes by the id alone).
    static string Goer(string persona){for(int n=0;;n++)if(DialogueGrammar.PersonaFor("goer_"+n,"")==persona)return "goer_"+n;}
    // p0 alone on Palm Mirage's Day 1, 1.5 m in front of `id`, who faces them: a cop for security, a buyer to sell to, a regular to
    // buy from, a regular to chat with. A wook never asks for a dance first unless wary, so the talks start talking.
    static FestivalSimulation Pair(string kind,int seed=7,string id=null)
    {
        var s=new FestivalSimulation(seed);s.AddPlayer("p0","P0");s.State.Phase="Playing";s.State.Npcs.Clear();
        s.State.Npcs.Add(new NpcState{Id=id??Goer("Wook"),Kind=kind=="Police"?"Cop":"Wook",Role=kind=="Sale"?"Buyer":"Regular",X=0,Z=1.5f,Yaw=180,CanTalk=true});
        var p=s.Player("p0");p.X=0;p.Z=0;if(kind=="Sale")p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        return s;
    }
    static NpcState Partner(FestivalSimulation s)=>s.State.Npcs[0];
    static InteractionState Start(FestivalSimulation s,string kind)
    {
        var npc=Partner(s);var started=Act(s,"p0",kind=="Sale"?"StartSale":kind=="Buy"?"StartBuy":kind,npc.Id,kind=="Sale"?"stock_lsd":kind=="Buy"?FestivalSimulation.SellerStock(npc.Id):"");
        Check(started.Accepted,"setup: the "+kind+" starts: "+started.Reason);return s.Interaction(s.Player("p0").InteractionId);
    }
    static ConversationTurn Live(InteractionState i)=>i.Turns[i.Turns.Count-1];
    // Picks the live turn's reply of this grade.
    static CommandResult Say(FestivalSimulation s,InteractionState i,int grade)=>Act(s,"p0","Reply",amount:Array.IndexOf(Live(i).Grades,grade),direction:i.Turns.Count-1);
    // The talk, a grade a turn (Silence lets the turn run out), then ticks until it settles.
    static InteractionState Talk(FestivalSimulation s,InteractionState i,params int[] grades)
    {
        foreach(int grade in grades)
        {
            int turn=i.Turns.Count;
            if(grade==Silence){for(int guard=0;i.Status=="Active"&&i.Turns.Count==turn&&guard<100;guard++)s.Tick(.1);continue;}
            var said=Say(s,i,grade);Check(said.Accepted,"setup: reply "+grade+" on turn "+turn+": "+said.Reason);
        }
        for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);
        Check(i.Status=="Complete"||i.Status=="Police bust","setup: the "+i.Kind+" settles ("+i.Status+")");return i;
    }
    static string Text(ConversationTurn t)=>t.Reaction+"|"+t.Line+"|"+string.Join("|",t.Replies)+"|"+string.Join(",",t.Grades)+"|"+string.Join(",",t.Garbled);

    // Every talk opens on their line and your three replies, one of each grade, timed by ReplySeconds. Each reply brings the next
    // turn, their reaction then their line; the third ends the talk. A talk takes no notes.
    static void EachTalkIsThreeTurnsOfThreeReplies()
    {
        Check(FestivalSimulation.ConversationTurns==3&&FestivalSimulation.ReplySeconds==4,"a talk is 3 turns of 4 s");
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);var i=Start(s,kind);
            Check(FestivalSimulation.Talks(kind)&&!i.TrustDance&&i.Turns.Count==1,"a "+kind+" opens on a turn, not a dance");
            var first=Live(i);
            Check(first.Line!=""&&first.Reaction==""&&first.Replies.Length==3&&first.Replies.All(r=>!string.IsNullOrWhiteSpace(r))&&first.Pick==-1,"a "+kind+"'s first turn: their line and three replies, nothing picked ("+Text(first)+")");
            Check(string.Join(",",first.Grades.OrderBy(g=>g))=="0,1,2"&&first.Garbled.All(g=>!g),"one smooth, one so-so and one awkward reply, none garbled sober ("+Text(first)+")");
            Check(Math.Abs(i.StartSeconds+i.DurationSeconds-(s.State.SimulationSeconds+FestivalSimulation.ReplySeconds))<1e-9,"the reply is due in ReplySeconds");
            Check(!Act(s,"p0","Rhythm",direction:0,amount:0).Accepted,"a "+kind+" takes no notes");
            Check(Say(s,i,SoSo).Accepted&&i.Turns.Count==2&&first.Pick>=0&&first.Grades[first.Pick]==SoSo,"a reply is kept and brings the second turn");
            Check(Live(i).Reaction!=""&&Live(i).Line!="","the second turn opens on their reaction and a line ("+Text(Live(i))+")");
            Check(Say(s,i,Smooth).Accepted&&i.Turns.Count==3,"the second reply brings the third turn");
            Check(Say(s,i,Smooth).Accepted&&i.Status=="Complete"&&i.Turns.Count==3&&s.Player("p0").InteractionId=="","the third reply ends the "+kind+" ("+i.Status+")");
        }
    }

    // The same seed and the same picks give the same talk, line for line and in the same order; another seed another talk.
    static void ASeedRepeatsItsTalk()
    {
        foreach(var kind in Kinds)
        {
            string Played(int seed){var s=Pair(kind,seed);var i=Start(s,kind);var said=new List<string>();foreach(int grade in new[]{Smooth,Awkward,SoSo}){said.Add(Text(Live(i)));Say(s,i,grade);}return string.Join("\n",said);}
            Check(Played(11)==Played(11),"a "+kind+" repeats with its seed");
            Check(Played(11)!=Played(12),"another seed is another "+kind);
        }
    }

    // Their opener hints at who they are: each persona opens in its own way. Each likes its own style of reply: two personas'
    // smooth replies never coincide unless they like the same (a dad and security both like it plain), and what charms one can
    // grate on another (a wook's smooth replies are security's awkward ones). A so-so reply is neither.
    static void TheOpenerAndTheRepliesFollowThePerson()
    {
        var people=DialogueGrammar.Personas.Concat(new[]{ConversationLines.Security}).ToArray();
        foreach(var topic in ConversationLines.Topics)for(int festival=0;festival<Festivals.Count;festival++)
        {
            var opens=new Dictionary<string,HashSet<string>>();var smooth=new Dictionary<string,HashSet<string>>();var soso=new Dictionary<string,HashSet<string>>();var awkward=new Dictionary<string,HashSet<string>>();
            foreach(var person in people)
            {
                opens[person]=new HashSet<string>();smooth[person]=new HashSet<string>();soso[person]=new HashSet<string>();awkward[person]=new HashSet<string>();
                for(int seed=0;seed<300;seed++)
                {
                    var t=ConversationLines.Turn(topic,person,"Regular",festival,0,-1,false,0,new ContentRandom(seed));opens[person].Add(t.Line);
                    for(int r=0;r<3;r++)(t.Grades[r]==Smooth?smooth:t.Grades[r]==SoSo?soso:awkward)[person].Add(t.Replies[r]);
                }
            }
            string where=topic+" on festival "+festival;
            foreach(var a in people)foreach(var b in people)if(a!=b)
            {
                bool shared=(a=="DraggedAlongDad"||a==ConversationLines.Security)&&(b=="DraggedAlongDad"||b==ConversationLines.Security);
                if(topic!="security"&&a!=ConversationLines.Security&&b!=ConversationLines.Security)Check(!opens[a].Overlaps(opens[b]),where+": a "+a+" and a "+b+" never open alike");
                Check(smooth[a].Overlaps(smooth[b])==shared,where+": a "+a+"'s and a "+b+"'s smooth replies "+(shared?"are the same style":"never coincide"));
            }
            Check(smooth["Wook"].Overlaps(awkward[ConversationLines.Security]),where+": a wook's smooth replies are security's awkward ones");
            foreach(var person in people)Check(!soso[person].Overlaps(smooth[person])&&!soso[person].Overlaps(awkward[person]),where+": a "+person+"'s so-so replies are neither smooth nor awkward to them");
        }
    }

    // Smooth scores 1, so-so .5 and awkward or silence 0, averaged over the three turns. Silence lets each turn run its 4 s.
    static void ATalkScoresTheAverageOfItsReplies()
    {
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);var all=Talk(s,Start(s,kind),Smooth,Smooth,Smooth);Check(all.Score==1,"an all-smooth "+kind+" scores 1, got "+all.Score);
            s=Pair(kind);var i=Start(s,kind);double began=s.State.SimulationSeconds;Talk(s,i,Silence,Silence,Silence);
            Check(i.Score==0&&i.Turns.All(t=>t.Pick==-1),"an all-silent "+kind+" scores 0, got "+i.Score);
            Check(Math.Abs(s.State.SimulationSeconds-began-3*FestivalSimulation.ReplySeconds)<.11,"three silent turns run 12 s ("+(s.State.SimulationSeconds-began)+")");
            s=Pair(kind);var mixed=Talk(s,Start(s,kind),Smooth,SoSo,Silence);Check(Math.Abs(mixed.Score-.5)<1e-9,"smooth, so-so and silence average .5, got "+mixed.Score);
            s=Pair(kind);var good=Talk(s,Start(s,kind),Smooth,Awkward,SoSo);Check(Math.Abs(good.Score-.5)<1e-9,"smooth, awkward and so-so average .5, got "+good.Score);
            s=Pair(kind);var sure=Talk(s,Start(s,kind),Smooth,Smooth,SoSo);Check(Math.Abs(sure.Score-5/6.0)<1e-9,"two smooth and a so-so average 5/6, got "+sure.Score);
            Check(s.Player("p0").LastRhythmScore==sure.Score,"the talk's score is the player's last result");
        }
    }

    // A reply is taken once per turn, for the live turn only, while its timer runs, and only as 1, 2 or 3.
    static void ALateOrPastReplyIsRefused()
    {
        var s=Pair("Conversation");
        Check(!Act(s,"p0","Reply",amount:0,direction:0).Accepted,"no reply with no talk");
        var i=Start(s,"Conversation");
        Check(!Act(s,"p0","Reply",amount:3,direction:0).Accepted&&!Act(s,"p0","Reply",amount:-1,direction:0).Accepted,"only reply 0-2");
        Check(!Act(s,"p0","Reply",amount:0,direction:1).Accepted,"no reply for a turn still to come");
        Check(Say(s,i,Smooth).Accepted,"setup: the first reply");
        Check(!Act(s,"p0","Reply",amount:0,direction:0).Accepted&&i.Turns.Count==2&&Live(i).Pick==-1,"no second reply to a past turn");
        for(int t=0;t<40;t++)s.Tick(.1);
        Check(i.Turns.Count==3&&i.Turns[1].Pick==-1,"setup: the second turn ran out, a silence");
        Check(!Act(s,"p0","Reply",amount:0,direction:1).Accepted&&i.Turns[1].Pick==-1,"a reply after its turn ran out is refused");
        s.Tick(3.85);Check(i.Status=="Active","setup: the third turn still runs 3.9 s in");
        i.DurationSeconds=s.State.SimulationSeconds-i.StartSeconds;
        Check(!Act(s,"p0","Reply",amount:0,direction:2).Accepted&&Live(i).Pick==-1,"a reply as its timer runs out is refused");
        s=Pair("Dance");Check(Act(s,"p0","Dance",Partner(s).Id).Accepted,"setup: a dance");
        Check(!Act(s,"p0","Reply",amount:0,direction:0).Accepted,"a dance takes no reply");
    }

    // The score settles a talk as the dance's did: a sale pays in full at .75 and half at .4, and returns the stock below; a buy
    // hands over a unit at .4 and refunds below; security at .75 waves off a stop; and the watchers move by RhythmDelta.
    static void TheScoreSettlesTheTalkAsTheDanceDid()
    {
        int Sold(params int[] grades){var s=Pair("Sale");var p=s.Player("p0");int cash=p.Cash;var i=Talk(s,Start(s,"Sale"),grades);Check(p.Inventory.Exists(x=>x.ItemId=="stock_lsd")==(i.Score<.4),"a sale scoring "+i.Score+" keeps the stock only below .4");return p.Cash-cash;}
        Check(Sold(Smooth,Smooth,SoSo)==FestivalSimulation.PerfectSalePay,"a sale at 5/6 pays in full");
        Check(Sold(Smooth,SoSo,Silence)==5,"a sale at .5 pays half");
        Check(Sold(Smooth,Silence,Silence)==0,"a sale at 1/3 pays nothing and returns the stock");
        foreach(var (grades,bought) in new[]{(new[]{Smooth,SoSo,Awkward},true),(new[]{SoSo,Silence,Silence},false)})
        {
            var s=Pair("Buy");var p=s.Player("p0");string kind=FestivalSimulation.SellerStock(Partner(s).Id);var i=Talk(s,Start(s,"Buy"),grades);
            Check(p.Inventory.Exists(x=>x.ItemId==kind)==bought&&p.Cash==(bought?15:20),"a buy scoring "+i.Score+(bought?" hands over a unit for $5":" refunds the $5")+" ($"+p.Cash+")");
        }
        foreach(var (grades,waved) in new[]{(new[]{Smooth,Smooth,SoSo},true),(new[]{Smooth,SoSo,SoSo},false)})
        {
            var s=Pair("Police");var cop=Partner(s);s.Player("p0").Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});cop.Evidence.Add(new EvidenceState{PlayerId="p0",Kind="VisibleStock",StartSeconds=0,SeenSeconds=0});
            var i=Talk(s,Start(s,"Police"),grades);
            Check(cop.Evidence.Exists(e=>e.Kind=="VisibleStock")!=waved,"security at "+i.Score+(waved?" waves the stop off":" keeps the stop"));
        }
        foreach(var (grades,moved) in new[]{(new[]{Smooth,Smooth,Smooth},-15),(new[]{Silence,Silence,Silence},20),(new[]{Smooth,SoSo,Silence},3)})
        {
            var s=Pair("Conversation");var watcher=new NpcState{Id="watcher",X=0,Z=6,Yaw=180};watcher.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=20});s.State.Npcs.Add(watcher);
            var i=Talk(s,Start(s,"Conversation"),grades);
            Check(i.WitnessIds.Contains("watcher")&&watcher.Observers[0].Suspicion==20+moved,"a watcher of a talk scoring "+i.Score+" moves by RhythmDelta, "+moved+" (now "+watcher.Observers[0].Suspicion+")");
        }
    }

    // A dosed player garbles their replies: one a turn at dose 1-2, two at dose 3-4. A garbled reply is the same reply with its
    // words and their letters jumbled, so its tone can't be read; it still counts at its grade.
    static void ADoseGarblesReplies()
    {
        for(int dose=0;dose<=4;dose++)
        {
            var sober=Pair("Conversation",21);var dosed=Pair("Conversation",21);
            if(dose>0)dosed.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose,RemainingSeconds=600});
            var plain=Start(sober,"Conversation");var jumbled=Start(dosed,"Conversation");
            for(int turn=0;turn<3;turn++)
            {
                var a=Live(plain);var b=Live(jumbled);int want=dose>=3?2:dose>=1?1:0;
                Check(b.Garbled.Count(g=>g)==want,"dose "+dose+": "+want+" garbled a turn, got "+b.Garbled.Count(g=>g));
                Check(a.Line==b.Line&&string.Join(",",a.Grades)==string.Join(",",b.Grades),"setup: the same seed says the same line with the replies in the same order");
                for(int r=0;r<3;r++)
                {
                    Check(b.Garbled[r]==(a.Replies[r]!=b.Replies[r]),"dose "+dose+": only a garbled reply reads differently: \""+b.Replies[r]+"\"");
                    Check(string.Concat(a.Replies[r].OrderBy(c=>c))==string.Concat(b.Replies[r].OrderBy(c=>c)),"a garbled reply keeps its letters: \""+a.Replies[r]+"\" -> \""+b.Replies[r]+"\"");
                }
                int pick=Array.IndexOf(b.Garbled,true);if(pick<0)pick=0;
                Act(sober,"p0","Reply",amount:pick,direction:turn);Check(Act(dosed,"p0","Reply",amount:pick,direction:turn).Accepted,"a garbled reply can be picked");
            }
            Check(plain.Score==jumbled.Score,"a garbled reply counts at its grade ("+plain.Score+" vs "+jumbled.Score+")");
        }
    }

    // A festivalgoer whose suspicion of you is 40 or more asks you to dance first: TrustAsk, then eight notes on the talk's own
    // chart, danced and witnessed like a dance. At .6 or more they trust you: everyone watching, and they, move by the dance's
    // RhythmDelta, and the talk follows. Below .6 they walk off: the talk ends at 0, a sale's stock and a buy's cash come back,
    // and the watchers move once, for the dance. Security never asks.
    static void AWaryFestivalgoerAsksForADanceFirst()
    {
        Check(FestivalSimulation.TrustDanceSuspicion==40&&FestivalSimulation.TrustScore==.6,"the trust dance comes at 40 and passes at .6");
        double Wary(FestivalSimulation s,double suspicion){Partner(s).Observers.Add(new ObserverState{PlayerId="p0",Suspicion=suspicion});return suspicion;}
        var calm=Pair("Conversation");Wary(calm,39);Check(!Start(calm,"Conversation").TrustDance,"at 39 they just talk");
        var cop=Pair("Police");Wary(cop,90);Check(!Start(cop,"Police").TrustDance,"security never asks for a dance");
        foreach(var kind in new[]{"Sale","Buy","Conversation"})
        {
            var s=Pair(kind);Wary(s,40);var p=s.Player("p0");p.Z=.5f;p.Yaw=90;var i=Start(s,kind);
            Check(i.TrustDance&&i.Turns.Count==0&&i.DialogueText==FestivalSimulation.TrustAsk&&FestivalSimulation.TrustDancing(i)&&FestivalSimulation.Dancing(i),"a wary partner opens a "+kind+" with \""+FestivalSimulation.TrustAsk+"\" and a dance, got \""+i.DialogueText+"\"");
            Check(Math.Abs(1.5-p.Z-FestivalSimulation.DanceSpacing)<1e-3&&p.X==0&&p.Yaw==0,"the trust dance gives the dancer room and turns them to face their partner, as any dance (at "+p.X+", "+p.Z+", yaw "+p.Yaw+")");
            Check(i.NoteCount==8&&i.DurationSeconds==RhythmChart.For(i).DurationSeconds,"eight notes on the talk's chart");
            var notes=RhythmChart.For(i).Notes;s.Tick(2);
            var hit=s.Execute("p0",new GameCommand{Id="talk"+(sequence++),Kind="Rhythm",Direction=notes[0].Direction,TimeSeconds=notes[0].TimeSeconds});
            Check(hit.Accepted&&p.VisualDanceStepSequence>0,"the trust dance takes notes, each a step");
            foreach(var note in notes.Skip(1))i.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
            for(int guard=0;i.Turns.Count==0&&i.Status=="Active"&&guard<100;guard++)s.Tick(.1);
            Check(i.Status=="Active"&&i.Turns.Count==1&&i.DialogueText==""&&!FestivalSimulation.Dancing(i),"danced well, the "+kind+" follows ("+i.Status+")");
            Check(Partner(s).Observers[0].Suspicion==25,"they trust you: 40 drops by RhythmDelta(1) to 25, got "+Partner(s).Observers[0].Suspicion);
            Check(Math.Abs(i.StartSeconds+i.DurationSeconds-(s.State.SimulationSeconds+FestivalSimulation.ReplySeconds))<.11,"the first reply is due 4 s after the dance");
            Talk(s,i,Smooth,Smooth,Smooth);Check(i.Score==1,"the talk after the dance scores itself, got "+i.Score);
        }
        // They trust you by the dance even when they can't see you dance, as no watcher does.
        var blind=Pair("Conversation");blind.HasLineOfSight=(ax,az,bx,bz)=>false;Partner(blind).Observers.Add(new ObserverState{PlayerId="p0",Suspicion=40,LastSeenSeconds=100});
        var unseen=Start(blind,"Conversation");Check(unseen.TrustDance&&unseen.WitnessIds.Count==0,"setup: a wary partner who can't see you asks for a dance");
        foreach(var note in RhythmChart.For(unseen).Notes)unseen.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
        for(int guard=0;unseen.Turns.Count==0&&guard<100;guard++)blind.Tick(.1);
        Check(Partner(blind).Observers[0].Suspicion==25,"they trust you, unseen or not: 40 drops to 25, got "+Partner(blind).Observers[0].Suspicion);
        foreach(var kind in new[]{"Sale","Buy","Conversation"})
        {
            var s=Pair(kind);Wary(s,40);var p=s.Player("p0");int cash=p.Cash;
            var watcher=new NpcState{Id="watcher",X=0,Z=6,Yaw=180};watcher.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=20});s.State.Npcs.Add(watcher);
            var i=Start(s,kind);var notes=RhythmChart.For(i).Notes;int under=(int)Math.Ceiling(TrustScoreNotes(notes.Count))-1;
            foreach(var note in notes.Take(under))i.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
            for(int guard=0;i.Status=="Active"&&guard<100;guard++)s.Tick(.1);
            Check(i.Status=="Complete"&&i.Score==0&&i.Turns.Count==0,"a "+kind+" danced under .6 ends at 0 with no talk ("+i.Status+", "+i.Score+")");
            Check(p.Cash==cash&&p.Inventory.Exists(x=>x.ItemId=="stock_lsd")==(kind=="Sale")&&p.Inventory.Count==(kind=="Sale"?1:0),"they walk off: the stock or the cash comes back");
            double score=(double)under/notes.Count;int moved=(int)Math.Round(20-35*score,MidpointRounding.AwayFromZero);
            Check(watcher.Observers[0].Suspicion==20+moved&&Partner(s).Observers[0].Suspicion==40+moved,"the watcher and they move once, by the dance's RhythmDelta "+moved+" (watcher "+watcher.Observers[0].Suspicion+", partner "+Partner(s).Observers[0].Suspicion+")");
        }
    }
    // The fewest of `count` perfect notes that score .6.
    static double TrustScoreNotes(int count)=>FestivalSimulation.TrustScore*count;

    // Seeded per festivalgoer, 1 in 4 ravers and burners want a dance before any talk, however calm; no one else does.
    static void SomeRaversAndBurnersAlwaysDanceFirst()
    {
        var asked=new Dictionary<string,int>();var met=new Dictionary<string,int>();var dancers=new Dictionary<string,string>();
        for(int seed=1;seed<=4;seed++)for(int n=0;n<300;n++)
        {
            string id="goer_"+n,persona=DialogueGrammar.PersonaFor(id,"");bool dances=Start(Pair("Conversation",seed,id),"Conversation").TrustDance;
            met[persona]=met.TryGetValue(persona,out int m)?m+1:1;if(dances)asked[persona]=asked.TryGetValue(persona,out int a)?a+1:1;
            Check(Start(Pair("Conversation",seed,id),"Conversation").TrustDance==dances,"the same festivalgoer asks alike every time in a round");
            dancers[id]=(dancers.TryGetValue(id,out var rounds)?rounds:"")+(dances?"D":"-");
        }
        Check(dancers.Values.Count(rounds=>rounds.Contains("D")&&rounds.Contains("-"))>50,"who dances first changes from round to round");
        foreach(var persona in met.Keys)
        {
            double rate=(asked.TryGetValue(persona,out int a)?a:0)/(double)met[persona];
            if(persona=="Raver"||persona=="Burner")Check(rate>.15&&rate<.35,"about 1 in 4 "+persona+"s dance first, got "+rate.ToString("0.00"));
            else Check(rate==0,"no "+persona+" asks for a dance unless wary, got "+rate.ToString("0.00"));
        }
    }

    // What anyone says in a talk stays clean: no player reply carries a narc's cop-speak, garbled or not, and every line, reaction
    // and reply fits within 80 characters.
    static void EveryLineIsCleanAndShort()
    {
        foreach(var topic in ConversationLines.Topics)foreach(var person in DialogueGrammar.Personas.Concat(new[]{ConversationLines.Security}))
            for(int festival=0;festival<Festivals.Count;festival++)for(int seed=0;seed<300;seed++)for(int turn=0;turn<3;turn++)
            {
                var t=ConversationLines.Turn(topic,person,DialogueGrammar.Roles[seed%4],festival,turn,turn==0?-1:seed%4,seed%2==0,seed%3,new ContentRandom(seed*7+turn));
                foreach(var reply in t.Replies)Check(!DialogueGrammar.HasNarcTell(reply),"a player's reply carries a narc tell: "+reply);
                foreach(var line in t.Replies.Concat(new[]{t.Line,t.Reaction}))Check(line.Length<=80,topic+" "+person+" line over 80 characters: "+line);
            }
    }

    // A chat with someone of this role on festival 0 (night 1 for a clue holder), a reply of `grade` every turn: what it left
    // you feeling, and whether any of their lines carried a narc's cop-speak.
    static (string hunch,bool copSpeak,string lines) Read(string role,int seed,int grade,string kind="Conversation")
    {
        var s=Pair(kind,seed);Partner(s).Role=role;if(role=="ClueHolder")s.State.LevelIndex=1;
        var i=Talk(s,Start(s,kind),grade,grade,grade);var lines=string.Join(" / ",i.Turns.Select(t=>t.Line));
        return (i.Hunch,i.Turns.Exists(t=>DialogueGrammar.HasNarcTell(t.Line)),lines);
    }
    static double Share(string role,int grade,Func<(string hunch,bool copSpeak,string lines),bool> what){int hits=0;for(int seed=1;seed<=200;seed++)if(what(Read(role,seed,grade)))hits++;return hits/200.0;}

    // A chat reads the person: a narc slips in cop-speak, a buyer hints they're after something, and a smooth chat draws the
    // tell out where an awkward one leaves it vague. It ends on a hunch built from what their lines showed: usually right
    // after a smooth chat, wrong more often after an awkward one, and never TRUE or FALSE (only the tripper's check says that).
    static void ATalkReadsThePerson()
    {
        double narcSpeak=Share("Narc",Smooth,r=>r.copSpeak),regularSpeak=Share("Regular",Smooth,r=>r.copSpeak);
        Check(narcSpeak>=.7&&regularSpeak<=.1,"after a smooth chat a narc's lines carry cop-speak "+narcSpeak.ToString("0.00")+" of the time (at least .70), a regular's "+regularSpeak.ToString("0.00")+" (at most .10)");
        double narcCop=Share("Narc",Smooth,r=>r.hunch==ConversationLines.HunchCop),regularCop=Share("Regular",Smooth,r=>r.hunch==ConversationLines.HunchCop);
        Check(narcCop>=.7&&regularCop<=.1,"after a smooth chat a narc leaves you feeling they might be a cop "+narcCop.ToString("0.00")+" of the time, a regular "+regularCop.ToString("0.00"));
        double buyerBuys=Share("Buyer",Smooth,r=>r.hunch==ConversationLines.HunchBuyer),regularMusic=Share("Regular",Smooth,r=>r.hunch==ConversationLines.HunchMusic);
        Check(buyerBuys>=.7&&regularMusic>=.9,"after a smooth chat a buyer feels like they'd buy "+buyerBuys.ToString("0.00")+" of the time, and a regular is just here for the music "+regularMusic.ToString("0.00"));
        foreach(var (role,right) in new[]{("Narc",ConversationLines.HunchCop),("Buyer",ConversationLines.HunchBuyer)})
        {
            double smooth=Share(role,Smooth,r=>r.hunch==right),soso=Share(role,SoSo,r=>r.hunch==right),awkward=Share(role,Awkward,r=>r.hunch==right);
            Check(awkward<soso&&soso<smooth,"the smoother the chat, the more often a "+role+" reads right: awkward "+awkward.ToString("0.00")+", so-so "+soso.ToString("0.00")+", smooth "+smooth.ToString("0.00"));
        }
        foreach(var role in DialogueGrammar.Roles)foreach(int grade in new[]{Smooth,SoSo,Awkward})for(int seed=1;seed<=40;seed++)
        {
            var read=Read(role,seed,grade);
            Check(read.hunch.StartsWith("You get the feeling ")&&read.hunch.IndexOf("true",StringComparison.OrdinalIgnoreCase)<0&&read.hunch.IndexOf("false",StringComparison.OrdinalIgnoreCase)<0,"a "+role+"'s hunch is a feeling, never TRUE or FALSE: "+read.hunch);
            if(role!="Narc")Check(!read.copSpeak,"only a narc ever slips into cop-speak: "+read.lines);
        }
    }
    // At night a clue holder's chat, drawn out smoothly, names a landmark, and leaves you feeling they know something about it.
    static void AClueHolderNamesALandmark()
    {
        int named=0;
        for(int seed=1;seed<=200;seed++)
        {
            var read=Read("ClueHolder",seed,Smooth);if(read.hunch==ConversationLines.HunchMusic)continue;named++;
            const string Lead="You get the feeling they know something about ";
            Check(read.hunch.StartsWith(Lead)&&read.hunch.EndsWith("."),"a clue holder's hunch: "+read.hunch);
            string landmark=read.hunch.Substring(Lead.Length,read.hunch.Length-Lead.Length-1);
            Check(landmark.StartsWith("the ")&&read.lines.IndexOf(landmark,StringComparison.OrdinalIgnoreCase)>=0,"the landmark ("+landmark+") is one their lines named: "+read.lines);
        }
        Check(named>=140,"a smooth chat with a clue holder names a landmark at least .70 of the time, got "+named/200.0);
    }
    // The hunch comes with the last turn, once every line it reads is said, and only from a chat or a buy: a sale's buyer is
    // buying, and security is security.
    static void OnlyAFestivalgoerTalkLeavesAHunch()
    {
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);var i=Start(s,kind);Say(s,i,Smooth);
            Check(i.Hunch=="","no hunch before the last turn ("+i.Hunch+")");
            Say(s,i,Smooth);bool reads=kind=="Conversation"||kind=="Buy";
            Check((i.Hunch!="")==reads,"a "+kind+(reads?" leaves a hunch as its last turn begins":" leaves no hunch")+" ("+i.Hunch+")");
        }
    }
    // A narc asked to sell talks, as a seller does, its lines carrying cop-speak when drawn out. Finishing the buy busts you
    // whatever your replies: detained, stock confiscated, the held $5 back and then the $10 fine. Walking off or stopping first
    // is safe, and so is a narc who walks off after a poor trust dance.
    static void ANarcSellerTalksAndFinishingTheBuyBusts()
    {
        int told=0;
        for(int seed=1;seed<=100;seed++){var read=Read("Narc",seed,Smooth,"Buy");if(read.copSpeak)told++;Check(read.hunch==ConversationLines.HunchCop==read.copSpeak,"a narc seller's hunch follows their cop-speak: "+read.hunch+" / "+read.lines);}
        Check(told>=70,"a smoothly talked narc seller slips into cop-speak at least 70 times in 100, got "+told);
        foreach(var grades in new[]{new[]{Smooth,Smooth,Smooth},new[]{Silence,Silence,Silence}})
        {
            var s=Pair("Buy");Partner(s).Role="Narc";var p=s.Player("p0");p.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
            var i=Start(s,"Buy");Check(p.Life=="Alive"&&p.Cash==15&&i.Turns.Count==1,"asking a narc to sell starts a talk, the $5 held, no bust yet");
            CommandResult last=null;
            foreach(int grade in grades){if(grade==Silence){for(int guard=0;i.Status=="Active"&&guard<50;guard++)s.Tick(.1);continue;}last=Say(s,i,grade);}
            for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);
            Check(p.Life=="Detained"&&p.Cash==10&&!p.Inventory.Exists(x=>x.ItemId.StartsWith("stock_"))&&Partner(s).StockSold==0&&i.Status=="Police bust","finishing the buy with a narc ("+string.Join(",",grades)+") busts you: detained, stock gone, $10 fine ("+p.Life+", $"+p.Cash+", "+i.Status+")");
            if(last!=null)Check(last.Reason=="Busted! That was an undercover narc.","the last reply says so: "+last.Reason);
        }
        var walk=Pair("Buy");Partner(walk).Role="Narc";var w=walk.Player("p0");var off=Start(walk,"Buy");Say(walk,off,Smooth);w.Z-=10;walk.Tick(.1);
        Check(w.Life=="Alive"&&w.Cash==20&&off.Status!="Active","walking off a narc's buy is safe, the $5 back ("+w.Life+", $"+w.Cash+")");
        var stop=Pair("Buy");Partner(stop).Role="Narc";var q=stop.Player("p0");Start(stop,"Buy");
        Check(Act(stop,"p0","Cancel").Accepted&&q.Life=="Alive"&&q.Cash==20,"stopping a narc's buy is safe, the $5 back");
        var shy=Pair("Buy");Partner(shy).Role="Narc";Partner(shy).Observers.Add(new ObserverState{PlayerId="p0",Suspicion=40});var r=shy.Player("p0");var walked=Start(shy,"Buy");
        for(int guard=0;walked.Status=="Active"&&guard<100;guard++)shy.Tick(.1);
        Check(walked.TrustDance&&walked.Turns.Count==0&&r.Life=="Alive"&&r.Cash==20,"a narc who walks off after a poor trust dance busts no one ("+r.Life+", $"+r.Cash+")");
    }

    // A talk in a snapshot from before conversations (no turns, no trust dance) restores, and carries on as a conversation.
    static void AnOlderSnapshotRestores()
    {
        var s=Pair("Conversation");var i=Start(s,"Conversation");
        var json=JsonNode.Parse(JsonSerializer.Serialize(s.State,new JsonSerializerOptions{IncludeFields=true}));
        foreach(var talk in json["Interactions"].AsArray()){var o=talk.AsObject();o.Remove("Turns");o.Remove("TrustDance");o.Remove("Hunch");}
        var saved=JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),new JsonSerializerOptions{IncludeFields=true});
        var restored=new FestivalSimulation();restored.Restore(saved);var old=restored.Interaction(i.Id);
        Check(old.Turns!=null&&old.Turns.Count==0&&!old.TrustDance,"an old talk restores with no turns ("+old.Turns?.Count+")");
        for(int guard=0;old.Status=="Active"&&guard<300;guard++)restored.Tick(.1);
        Check(old.Status=="Complete"&&old.Turns.Count==3&&old.Score==0,"it carries on as a three-turn talk, left silent ("+old.Status+", "+old.Turns.Count+" turns)");
    }
}
