using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// DIALOG-3: selling, buying, chatting and talking to security are conversations, not dances. Each is four turns (the greeting,
// small talk, the ask and the close, each with replies of its own): they say a line and you pick one of three replies (1-3).
// Only Police replies time out after ReplySeconds. One reply is smooth, one so-so and one
// awkward, by the taste of the person you're talking to, which their opener hints at; a dose garbles some. The replies'
// points are the talk's score, and settle it as the dance did. A wary festivalgoer asks for a dance first.
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
        foreach(var test in new Action[]{EachTalkIsFourRoundsOfThreeReplies,TheirLineFollowsTheRound,ASeedRepeatsItsTalk,TheOpenerAndTheRepliesFollowThePerson,
            ATalkScoresItsRepliesPoints,ALateOrPastReplyIsRefused,TheScoreSettlesTheTalkAsTheDanceDid,ADoseGarblesReplies,
            ATrustDanceGivesTheFirstArrowTimeToEnter,AWaryFestivalgoerAsksForADanceFirst,SomeRaversAndBurnersAlwaysDanceFirst,EveryLineIsCleanAndShort,AnOlderSnapshotRestores,
            ATalkReadsThePerson,ARegularTellLeavesASellerHunch,AClueHolderNamesALandmark,ASellerHunchUsesTheHostCategoryNotRemainingStock,ANoTellTalkKeepsMusicAndOtherTellsKeepTheirHunches,
            OnlyAFestivalgoerTalkLeavesAHunch,ANarcSellerTalksAndFinishingTheBuyBusts,
            AWorkedUpFestivalgoerWontTalk,TheirReactionsRarelyRepeat,ASmoothTalkCalmsItsPartnerEvenFacingAway,ASlurKeepsWordsAndTheirReadableEdges,
            ATalkSettlesItsPartnerAndWitnessesOnce,GrammarDrawsWaitForOwnerAcknowledgment,TheLastReplyHasAClosingReaction,
            EachAskPaysByItsTable,APushedAskThatFailsRaisesSuspicion,ASaleKeepsItsAskThroughASnapshot,AHandedBackUnitGoesBackInYourHand,ASaleSaysHowItEnded,AnOutcomeCarriesItsSettledScore})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" conversation test(s) failed:\n"+string.Join("\n",failures));
    }

    static void GrammarDrawsWaitForOwnerAcknowledgment()
    {
        var game=Pair("Conversation");Start(game,"Conversation");
        Check(game.Player("p0").Dialogue.Seen.Count==0,"drawing a turn must not acknowledge unread choices");
    }
    static void TheLastReplyHasAClosingReaction()
    {
        foreach(int grade in new[]{Smooth,SoSo,Awkward})
        {
            var game=Pair("Conversation");var talk=Start(game,"Conversation");
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)Check(Say(game,talk,grade).Accepted,"the reply is accepted");
            var player=game.Player("p0");
            Check(talk.Turns.Count==FestivalSimulation.ConversationTurns&&talk.Status=="Complete","the final reaction creates no fifth turn");
            Check(player.NpcSpeech!="","the final reply of grade "+grade+" gets its authored closing reaction");
            Check(player.ConversationOutcomes[0].GrammarKeys.Single().StartsWith("react.Wook."+grade+"_",StringComparison.Ordinal),"the actual closing reaction uses the chosen reply grade's authored grammar family");
            var wire=JsonSerializer.Serialize(player,new JsonSerializerOptions{IncludeFields=true});
            Check(JsonNode.Parse(wire)["ConversationOutcomes"]?.AsArray().Count==1&&JsonNode.Parse(wire)["ConversationOutcomes"][0]["Reaction"].GetValue<string>()==player.NpcSpeech,"the closing reaction stays durable after InteractionId clears");
        }
    }

    // DIALOG-8: the person talking with you hears the replies even when their layout yaw faces away.
    static void ASmoothTalkCalmsItsPartnerEvenFacingAway()
    {
        foreach(float yaw in new[]{0f,180f})
        {
            var s=Pair("Conversation");var npc=Partner(s);npc.Yaw=yaw;
            npc.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=30});
            var i=Start(s,"Conversation");
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)Check(Say(s,i,Smooth).Accepted,"each smooth reply is accepted");
            Check(i.Status=="Complete"&&i.Score==1,"four smooth replies complete the talk");
            Check(npc.Observers[0].Suspicion==15,"the partner calms exactly once regardless of facing: yaw "+yaw+", suspicion "+npc.Observers[0].Suspicion);
        }
    }

    static void ASlurKeepsWordsAndTheirReadableEdges()
    {
        foreach(var text in new[]{"Honestly?  Festival friends, community!", "\tPLUR! I'm in.\r\nA five-star neighbor's gift…", "  aérodrome — καλημέρα; 12345!", "", "\t \r\n", "aaaaa xyz I go!"})
        {
            var versions=new HashSet<string>();
            for(int seed=0;seed<32;seed++)
            {
                string slurred=ConversationLines.Garble(text,new ContentRandom(seed));
                Check(slurred==ConversationLines.Garble(text,new ContentRandom(seed)),"the same seed repeats its slur");
                ReadableSlur(text,slurred);versions.Add(slurred);
            }
            if(text.StartsWith("Honestly?"))Check(versions.Count>1&&!versions.SetEquals(new[]{text}),"long words really slur, with seeded variation");
        }
    }
    // Independent readability contract: compare positions and alphabetic runs, never the shuffle's chosen permutation.
    static void ReadableSlur(string plain,string slurred)
    {
        Check(slurred.Length==plain.Length,"a slur keeps the reply's length");
        for(int at=0;at<plain.Length;)
        {
            if(!char.IsLetter(plain[at])){Check(slurred[at]==plain[at],"whitespace and punctuation stay at "+at+": "+plain+" -> "+slurred);at++;continue;}
            int start=at;while(at<plain.Length&&char.IsLetter(plain[at]))at++;
            string word=plain.Substring(start,at-start),heard=slurred.Substring(start,at-start);
            if(word.Length<5)Check(word==heard,"short words stay readable: "+word+" -> "+heard);
            else
            {
                Check(word[0]==heard[0]&&word[word.Length-1]==heard[heard.Length-1],"first and last letters stay: "+word+" -> "+heard);
                Check(string.Concat(word.OrderBy(c=>c))==string.Concat(heard.OrderBy(c=>c)),"a word keeps its own letters: "+word+" -> "+heard);
            }
        }
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
    // The talk, a grade a turn (only Police Silence lets the turn run out), then ticks until it settles.
    static InteractionState Talk(FestivalSimulation s,InteractionState i,params int[] grades)
    {
        foreach(int grade in grades)
        {
            int turn=i.Turns.Count;
            if(grade==Silence&&i.Kind=="Police"){Check(Act(s,"p0","TalkAck",i.Id,direction:turn-1).Accepted,"the displayed Police turn is acknowledged");for(int guard=0;i.Status=="Active"&&i.Turns.Count==turn&&guard<100;guard++)s.Tick(.1);continue;}
            var said=Say(s,i,grade);Check(said.Accepted,"setup: reply "+grade+" on turn "+turn+": "+said.Reason);
        }
        for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);
        Check(i.Status=="Complete"||i.Status=="Police bust","setup: the "+i.Kind+" settles ("+i.Status+")");return i;
    }
    static int[] Every(int grade)=>Enumerable.Repeat(grade,FestivalSimulation.ConversationTurns).ToArray();
    static string Text(ConversationTurn t)=>t.Reaction+"|"+t.Line+"|"+string.Join("|",t.Replies)+"|"+string.Join(",",t.Grades)+"|"+string.Join(",",t.Garbled);

    // Every talk opens on their line and your three replies, one of each grade, timed only for Police. Each reply brings the next
    // turn, their reaction then their line; the fourth ends the talk. Each turn is a round of its own (the greeting,
    // small talk, the ask and the close), and draws its replies from that round's pools, so no reply pool serves two rounds. A talk
    // takes no notes.
    static void EachTalkIsFourRoundsOfThreeReplies()
    {
        Check(FestivalSimulation.ConversationTurns==4&&ConversationLines.Rounds.Length==FestivalSimulation.ConversationTurns&&FestivalSimulation.ReplySeconds==4,"a talk is 4 turns, one a round; Police replies keep 4 s");
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);var i=Start(s,kind);string topic=ConversationLines.Topic(kind);
            Check(FestivalSimulation.Talks(kind)&&!i.TrustDance&&i.Turns.Count==1,"a "+kind+" opens on a turn, not a dance");
            var first=Live(i);
            Check(first.Line!=""&&first.Reaction==""&&first.Replies.Length==3&&first.Replies.All(r=>!string.IsNullOrWhiteSpace(r))&&first.Pick==-1,"a "+kind+"'s first turn: their line and three replies, nothing picked ("+Text(first)+")");
            Check(string.Join(",",first.Grades.OrderBy(g=>g))=="0,1,2"&&first.Garbled.All(g=>!g),"one smooth, one so-so and one awkward reply, none garbled sober ("+Text(first)+")");
            Check(kind=="Police"?Math.Abs(i.StartSeconds+i.DurationSeconds-(s.State.SimulationSeconds+FestivalSimulation.ReplySeconds))<1e-9:i.DurationSeconds==0,"only Police replies are due in ReplySeconds; other talks have finite zero duration");
            Check(!Act(s,"p0","Rhythm",direction:0,amount:0).Accepted,"a "+kind+" takes no notes");
            var pools=new List<string>();
            int[] picks={SoSo,Smooth,Smooth,Smooth};
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                var live=Live(i);var replies=live.GrammarKeys.Where(k=>k.StartsWith("reply.",StringComparison.Ordinal)).ToList();
                Check(replies.Count==3&&replies.All(k=>k.StartsWith("reply."+topic+"."+ConversationLines.Rounds[turn]+".",StringComparison.Ordinal)),"a "+kind+"'s turn "+(turn+1)+" draws its replies from the "+ConversationLines.Rounds[turn]+" round's pools ("+string.Join(", ",replies)+")");
                foreach(var key in replies)pools.Add(key.Substring(0,key.LastIndexOf('_')));
                if(turn>0)Check(live.Reaction!=""&&live.Line!="","turn "+(turn+1)+" opens on their reaction and a line ("+Text(live)+")");
                Check(Say(s,i,picks[turn]).Accepted,"reply "+(turn+1)+" is accepted");
                if(turn==0)Check(first.Pick>=0&&first.Grades[first.Pick]==SoSo,"a reply is kept");
                bool last=turn==FestivalSimulation.ConversationTurns-1;
                Check(last?i.Status=="Complete"&&i.Turns.Count==FestivalSimulation.ConversationTurns&&s.Player("p0").InteractionId=="":i.Status=="Active"&&i.Turns.Count==turn+2,last?"the fourth reply ends the "+kind+" ("+i.Status+")":"reply "+(turn+1)+" brings turn "+(turn+2));
            }
            Check(pools.GroupBy(pool=>pool).All(g=>g.Select(pool=>pool.Split('.')[2]).Distinct().Count()==1),"no reply pool is used in two rounds: "+string.Join(", ",pools));
        }
    }

    // Their line follows the round too: their opener (security's line), then small talk, the ask and the close, each from
    // its own pool, while a line that lands still carries their tell. Each turn's line is the first key it draws after its reaction.
    static void TheirLineFollowsTheRound()
    {
        var expected=new Dictionary<string,string[]>{
            ["Conversation"]=new[]{"opener.Wook","answer.Regular.0","line.talk.ask","line.talk.close"},
            ["Sale"]=new[]{"opener.Wook","line.sale.small","sale","line.sale.close"},
            ["Buy"]=new[]{"opener.Wook","line.buy.small","buy","line.buy.close"},
            ["Police"]=new[]{"police","police","police","line.security.close"}};
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);var i=Start(s,kind);
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                var live=Live(i);string line=live.GrammarKeys[turn==0?0:1];
                Check(line.StartsWith(expected[kind][turn]+"_",StringComparison.Ordinal),"a "+kind+"'s turn "+(turn+1)+" ("+ConversationLines.Rounds[turn]+") has them say a line of "+expected[kind][turn]+", got "+line);
                Check(Say(s,i,Smooth).Accepted,"setup: a smooth reply");
            }
        }
    }

    // K2: someone worked up about you (accusing you, or swarming you) is too agitated to chat, sell or buy, as Talk's bark and the
    // chat check already said: a talk roots you, and they would hit you while you stand in it. Nothing is taken or held.
    static void AWorkedUpFestivalgoerWontTalk()
    {
        foreach(var kind in new[]{"Conversation","Sale","Buy"})foreach(bool swarming in new[]{false,true})
        {
            var s=Pair(kind);var npc=Partner(s);var p=s.Player("p0");p.Cash=20;
            npc.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=95,LastSeenSeconds=0});
            s.Tick(.1);if(swarming)s.Tick(4.5);
            string mode=npc.Mode;Check(mode==(swarming?"Swarming":"Accusing"),"setup: they are "+(swarming?"swarming":"accusing")+" p0 (mode "+mode+")");
            int cash=p.Cash,stock=p.Inventory.Find(x=>x.ItemId=="stock_lsd")?.Count??0;
            var started=Act(s,"p0",kind=="Sale"?"StartSale":kind=="Buy"?"StartBuy":kind,npc.Id,kind=="Sale"?"stock_lsd":kind=="Buy"?FestivalSimulation.SellerStock(npc.Id):"");
            Check(!started.Accepted&&started.Reason=="They're too agitated to chat","a "+kind+" with someone "+mode+" is refused as too agitated (accepted "+started.Accepted+", \""+started.Reason+"\")");
            Check(p.InteractionId==""&&p.Cash==cash&&(p.Inventory.Find(x=>x.ItemId=="stock_lsd")?.Count??0)==stock,"and nothing starts, held or taken ("+p.InteractionId+", $"+p.Cash+")");
        }
    }

    // K2: their reaction to your reply is built from parts too (DIALOG-4), and drawn past what you heard lately, so it rarely comes
    // back. 20 talks in a row with one kind of person, the same grade of reply every turn (or none), say at least 55 different
    // reactions in their 60, and none comes back within five. Before, each persona's reaction to a grade was one of five lines.
    static void TheirReactionsRarelyRepeat()
    {
        var samey=new List<string>();
        foreach(var persona in DialogueGrammar.Personas.Concat(new[]{ConversationLines.Security}))foreach(int grade in new[]{Smooth,SoSo,Awkward,Silence})
        {
            if(grade==Silence&&persona!=ConversationLines.Security)continue;
            var heard=Reactions(persona,grade,60);int soonest=int.MaxValue;
            for(int k=1;k<heard.Count;k++){int last=heard.LastIndexOf(heard[k],k-1);if(last>=0)soonest=Math.Min(soonest,k-last);}
            int distinct=heard.Distinct().Count();
            if(distinct<55||soonest<5)samey.Add(persona+" after a "+(grade==Silence?"silence":"grade "+grade+" reply")+": "+distinct+" different in "+heard.Count+", one back "+(soonest==int.MaxValue?"never":"after "+soonest)+" (e.g. \""+heard.GroupBy(r=>r).OrderByDescending(g=>g.Count()).First().Key+"\")");
        }
        Check(samey.Count==0,"their reactions repeat:\n  "+string.Join("\n  ",samey));
    }
    // One player's chats (security: talks) with someone of `persona`, fresh and at ease each time, a reply of `grade` every turn
    // (Silence: none): their reactions each time, in order, until there are `count`.
    static List<string> Reactions(string persona,int grade,int count)
    {
        bool security=persona==ConversationLines.Security;string kind=security?"Police":"Conversation";
        // Only POLO-1's influencer twist talks like an influencer.
        string twist=persona=="Influencer"?FestivalSimulation.Influencer:"";
        var s=Pair(kind,11);var heard=new List<string>();int n=0;
        string Next(){for(;;n++)if(DialogueGrammar.PersonaFor("goer_"+n,twist)==persona)return "goer_"+n++;}
        string id=security?Partner(s).Id:Next();
        while(heard.Count<count)
        {
            s.State.Npcs[0]=new NpcState{Id=id,Kind=security?"Cop":"Wook",Twist=twist,Role="Regular",X=0,Z=1.5f,Yaw=180,CanTalk=true};
            var i=Start(s,kind);
            // Some ravers and burners always want a dance first: talk with another.
            if(i.TrustDance){Act(s,"p0","Cancel");id=Next();continue;}
            Talk(s,i,Every(grade));foreach(var turn in i.Turns.Skip(1))heard.Add(turn.Reaction);
        }
        return heard.Take(count).ToList();
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
                for(int seed=0;seed<300;seed++)for(int round=0;round<ConversationLines.Rounds.Length;round++)
                {
                    var t=ConversationLines.Turn(topic,person,"Regular",festival,round,round==0?-1:SoSo,false,0,new ContentRandom(seed));if(round==0)opens[person].Add(t.Line);
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

    // Smooth scores 2 points, so-so 1 and awkward or silence -1, and the score is the total over 8, never below 0. Only Police
    // silence lets each turn run its 4 s.
    static void ATalkScoresItsRepliesPoints()
    {
        Check(FestivalSimulation.Points(Smooth)==2&&FestivalSimulation.Points(SoSo)==1&&FestivalSimulation.Points(Awkward)==-1&&FestivalSimulation.Points(Silence)==-1&&FestivalSimulation.TalkPoints==8,"replies are worth 2, 1, -1 and -1, out of 8");
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);var all=Talk(s,Start(s,kind),Every(Smooth));Check(all.Score==1,"an all-smooth "+kind+" scores 8 of 8, got "+all.Score);
            s=Pair(kind);var i=Start(s,kind);double began=s.State.SimulationSeconds;Talk(s,i,Every(Awkward));
            Check(i.Score==0,"an all-awkward "+kind+" scores -4, held at 0, got "+i.Score);
            if(kind=="Police"){s=Pair(kind);i=Start(s,kind);began=s.State.SimulationSeconds;Talk(s,i,Every(Silence));Check(i.Score==0&&i.Turns.Count==4&&i.Turns.All(t=>t.Pick==-1)&&Math.Abs(s.State.SimulationSeconds-began-4*FestivalSimulation.ReplySeconds)<.11,"Police alone scores four silent turns after 16 seconds");}
            s=Pair(kind);var mixed=Talk(s,Start(s,kind),Smooth,SoSo,Awkward,Smooth);Check(Math.Abs(mixed.Score-.5)<1e-9,"smooth, so-so, awkward and smooth score 4 of 8, got "+mixed.Score);
            s=Pair(kind);var poor=Talk(s,Start(s,kind),Smooth,Awkward,SoSo,SoSo);Check(Math.Abs(poor.Score-.375)<1e-9,"smooth, awkward and two so-so score 3 of 8, got "+poor.Score);
            s=Pair(kind);var split=Talk(s,Start(s,kind),Smooth,Smooth,Awkward,Awkward);Check(Math.Abs(split.Score-.25)<1e-9,"two smooth and two awkward score 2 of 8, got "+split.Score);
            s=Pair(kind);var sure=Talk(s,Start(s,kind),Smooth,Smooth,Smooth,SoSo);Check(Math.Abs(sure.Score-.875)<1e-9,"three smooth and a so-so score 7 of 8, as the old average did, got "+sure.Score);
            Check(s.Player("p0").LastRhythmScore==sure.Score,"the talk's score is the player's last result");
        }
    }

    // A reply is taken once per turn, for the live turn only, while its timer runs, and only as 1, 2 or 3.
    static void ALateOrPastReplyIsRefused()
    {
        var s=Pair("Police");
        Check(!Act(s,"p0","Reply",amount:0,direction:0).Accepted,"no reply with no talk");
        var i=Start(s,"Police");
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

    // Totals at and just under each line settle a talk as the dance's score did: a sale's pay (the stock back only on no sale), a
    // buy at 4 points, security waving off a stop at 6, and the watchers' RhythmDelta.
    static void TheScoreSettlesTheTalkAsTheDanceDid()
    {
        int Fair=FestivalSimulation.FairAsk,High=FestivalSimulation.HighAsk,Greedy=FestivalSimulation.GreedyAsk;
        int[] seven={Smooth,Smooth,Smooth,SoSo},six={Smooth,Smooth,SoSo,SoSo},five={Smooth,Smooth,Smooth,Awkward},four={Smooth,Smooth,SoSo,Awkward},three={Smooth,SoSo,SoSo,Awkward},two={Smooth,Smooth,Awkward,Awkward};
        foreach(var (ask,grades,paid) in new[]{(Fair,six,10),(Fair,five,5),(Fair,four,5),(Fair,three,0),(High,six,15),(High,five,10),(High,four,10),(High,three,0),(Greedy,seven,20),(Greedy,six,10),(Greedy,five,10),(Greedy,four,0),(Fair,two,0)})
        {
            var s=Pair("Sale");var p=s.Player("p0");int cash=p.Cash;var i=Talk(s,Sale(s,ask),grades);
            Check(p.Cash-cash==paid,"ask "+ask+" at "+(i.Score*FestivalSimulation.TalkPoints)+" points pays "+paid+", got "+(p.Cash-cash));
            Check(p.Inventory.Exists(x=>x.ItemId=="stock_lsd")==(paid==0),"ask "+ask+" at "+i.Score+": only a no-sale hands the stock back");
        }
        foreach(var (grades,bought) in new[]{(four,true),(three,false)})
        {
            var s=Pair("Buy");var p=s.Player("p0");string kind=FestivalSimulation.SellerStock(Partner(s).Id);var i=Talk(s,Start(s,"Buy"),grades);
            Check(p.Inventory.Exists(x=>x.ItemId==kind)==bought&&p.Cash==(bought?15:20),"a buy at "+(i.Score*FestivalSimulation.TalkPoints)+" points"+(bought?" hands over a unit for $5":" refunds the $5")+" ($"+p.Cash+")");
        }
        foreach(var (grades,waved) in new[]{(six,true),(five,false),(new[]{Smooth,Smooth,Smooth,Silence},false)})
        {
            var s=Pair("Police");var cop=Partner(s);s.Player("p0").Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});s.Player("p0").EquippedItemId="stock_lsd";cop.Evidence.Add(new EvidenceState{PlayerId="p0",Kind="VisibleStock",StartSeconds=0,SeenSeconds=0});
            var i=Talk(s,Start(s,"Police"),grades);
            Check(cop.Evidence.Exists(e=>e.Kind=="VisibleStock")!=waved,"security at "+(i.Score*FestivalSimulation.TalkPoints)+" points"+(waved?" waves the stop off":" keeps the stop"));
        }
        foreach(var (grades,moved) in new[]{(new[]{Smooth,Smooth,Smooth,Smooth},-15),(new[]{Awkward,Awkward,Awkward,Awkward},20),(new[]{Smooth,Smooth,Awkward,SoSo},3)})
        {
            var s=Pair("Conversation");var watcher=new NpcState{Id="watcher",X=0,Z=6,Yaw=180};watcher.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=20});s.State.Npcs.Add(watcher);
            var i=Talk(s,Start(s,"Conversation"),grades);
            Check(i.WitnessIds.Contains("watcher")&&watcher.Observers[0].Suspicion==20+moved,"a watcher of a talk scoring "+i.Score+" moves by RhythmDelta, "+moved+" (now "+watcher.Observers[0].Suspicion+")");
        }
    }

    // A dosed player slurs their replies: one a turn at dose 1-2, two at dose 3-4. Only interiors of long words jumble;
    // a selected reply made of short words may stay unchanged. Its grade and the selected choices still follow the seed.
    static void ADoseGarblesReplies()
    {
        for(int seed=1;seed<=12;seed++)foreach(var kind in Kinds)for(int dose=0;dose<=4;dose++)
        {
            var sober=Pair(kind,seed);var dosed=Pair(kind,seed);var repeat=Pair(kind,seed);
            if(dose>0)dosed.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose,RemainingSeconds=600});
            if(dose>0)repeat.Player("p0").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose,RemainingSeconds=600});
            var plain=Start(sober,kind);var jumbled=Start(dosed,kind);var replay=Start(repeat,kind);
            for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                var a=Live(plain);var b=Live(jumbled);int want=dose>=3?2:dose>=1?1:0;
                Check(b.Garbled.Count(g=>g)==want,"dose "+dose+": "+want+" garbled a turn, got "+b.Garbled.Count(g=>g));
                Check(a.Line==b.Line&&string.Join(",",a.Grades)==string.Join(",",b.Grades),"setup: the same seed says the same line with the replies in the same order");
                Check(Text(b)==Text(Live(replay)),"the same seed repeats the selected choices, slurred text and grades");
                for(int r=0;r<3;r++)
                {
                    if(!b.Garbled[r])Check(a.Replies[r]==b.Replies[r],"dose "+dose+": an unselected reply stays unchanged");
                    ReadableSlur(a.Replies[r],b.Replies[r]);
                }
                int pick=Array.IndexOf(b.Garbled,true);if(pick<0)pick=0;
                Act(sober,"p0","Reply",amount:pick,direction:turn);Check(Act(dosed,"p0","Reply",amount:pick,direction:turn).Accepted,"a garbled reply can be picked");
                Check(Act(repeat,"p0","Reply",amount:pick,direction:turn).Accepted,"the repeated seeded choice is accepted");
            }
            Check(plain.Score==jumbled.Score,"a garbled reply counts at its grade ("+plain.Score+" vs "+jumbled.Score+")");
        }
    }

    static void ATalkSettlesItsPartnerAndWitnessesOnce()
    {
        foreach(var kind in Kinds)foreach(float yaw in new[]{0f,180f})
            foreach(var (grades,moved) in new[]{(new[]{Smooth,Smooth,Smooth,Smooth},-15),(new[]{Awkward,Awkward,Awkward,Awkward},20),(new[]{Smooth,Smooth,Awkward,SoSo},3)})
            {
                var s=Pair(kind);var partner=Partner(s);partner.Yaw=yaw;
                partner.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=30});
                var watcher=new NpcState{Id="watcher",Kind="Wook",X=0,Z=6,Yaw=180};watcher.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=30});s.State.Npcs.Add(watcher);
                var unseen=new NpcState{Id="unseen",Kind="Wook",X=0,Z=6,Yaw=0};unseen.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=30});s.State.Npcs.Add(unseen);
                var i=Start(s,kind);
                Check(i.WitnessIds.Contains("watcher")&&!i.WitnessIds.Contains("unseen"),"only the facing bystander witnesses the talk");
                Check(i.WitnessIds.Contains(partner.Id)==(kind!="Police"&&yaw==180),"the partner's original visibility still decides witness membership");
                Talk(s,i,grades);
                Check(partner.Yaw==yaw,"settling never rotates the partner");
                Check(partner.Observers[0].Suspicion==30+moved&&watcher.Observers[0].Suspicion==30+moved,"partner or witness settles once for "+kind+" at yaw "+yaw+" with delta "+moved);
                Check(unseen.Observers[0].Suspicion==30,"an unseen bystander is unaffected");
                Check(!Say(s,i,Smooth).Accepted&&partner.Observers[0].Suspicion==30+moved&&watcher.Observers[0].Suspicion==30+moved,"a completed talk never settles twice");
            }
    }

    // TRIP-9: only a talk's trust dance gets the shared countdown. Its chart and the .75 s verdict window stay relative to start.
    static void ATrustDanceGivesTheFirstArrowTimeToEnter()
    {
        foreach(var kind in new[]{"Sale","Buy","Conversation"})
        {
            var s=Pair(kind);var p=s.Player("p0");p.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance="ketamine",RemainingSeconds=100});
            Partner(s).Observers.Add(new ObserverState{PlayerId=p.Id,Suspicion=40});
            double pressed=s.State.SimulationSeconds;var i=Start(s,kind);var chart=RhythmChart.For(i);
            Check(i.StartSeconds-pressed==2,kind+": a trust dance gives the lane its shared 2 s countdown");
            Check(i.NoteCount==8&&chart.Notes.Count==RhythmChart.Create(i.ChartSeed,8,i.BeatSeconds,i.ChartDifficulty).Notes.Count&&chart.Notes[0].TimeSeconds>=2&&i.StageTrack==StageMusic.TrackAt(s.State,p.X,p.Z)&&chart.DurationSeconds==i.DurationSeconds,kind+": the eight logical beats and patterns map to the nearer stage's song with their actual duration");
            Check(i.StartSeconds+chart.Notes[0].TimeSeconds-pressed>=Catalog.FindEffect("ketamine").LeadSeconds,kind+": the first Pony Dust arrow can enter at the bottom");
            Check(!Act(s,p.Id,"Rhythm",direction:chart.Notes[0].Direction).Accepted,kind+": the countdown does not admit an early press");
            int next=0;
            while(s.State.SimulationSeconds<i.StartSeconds+i.DurationSeconds+.7-1e-9)
            {
                double now=s.State.SimulationSeconds-i.StartSeconds;
                while(next<chart.Notes.Count&&chart.Notes[next].TimeSeconds<=now+1e-9)
                {
                    var note=chart.Notes[next++];
                    Check(s.Execute(p.Id,new GameCommand{Id="talk"+(sequence++),Kind="Rhythm",Direction=note.Direction,TimeSeconds=note.TimeSeconds}).Accepted,kind+": the original hit time is accepted through Rhythm");
                }
                s.Tick(Math.Min(.05,i.StartSeconds+i.DurationSeconds+.7-s.State.SimulationSeconds));
            }
            Check(i.Status=="Active"&&i.Turns.Count==0,kind+": the .75 s verdict window has not ended");
            s.Tick(.1);Check(i.Status=="Active"&&i.Turns.Count==1&&i.DurationSeconds==0,kind+": a perfect trust dance opens an untimed reply");
            s.Tick(30);Check(i.Turns.Count==1&&Live(i).Pick==-1,kind+": the countdown never becomes a chat deadline");
            Talk(s,i,Every(Smooth));Check(i.Score==1,kind+": the following talk scores only its replies");
        }
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);double pressed=s.State.SimulationSeconds;var i=Start(s,kind);
            Check(i.StartSeconds==pressed&&!i.TrustDance,kind+": an ordinary talk begins immediately");
            Check(i.DurationSeconds==(kind=="Police"?4:0),kind+": its existing reply timing is preserved");
        }
    }

    // A festivalgoer whose suspicion of you is 40 or more asks you to dance first: TrustAsk, then eight beats on the talk's own
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
            Check(i.NoteCount==8&&i.DurationSeconds==RhythmChart.For(i).DurationSeconds,"eight beats on the talk's chart");
            var notes=RhythmChart.For(i).Notes;s.Tick(i.StartSeconds-s.State.SimulationSeconds+notes[0].TimeSeconds);
            var hit=s.Execute("p0",new GameCommand{Id="talk"+(sequence++),Kind="Rhythm",Direction=notes[0].Direction,TimeSeconds=notes[0].TimeSeconds});
            Check(hit.Accepted&&p.VisualDanceStepSequence>0,"the trust dance takes notes, each a step");
            foreach(var note in notes.Skip(1))i.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
            for(int guard=0;i.Turns.Count==0&&i.Status=="Active"&&guard<100;guard++)s.Tick(.1);
            Check(i.Status=="Active"&&i.Turns.Count==1&&i.DialogueText==""&&!FestivalSimulation.Dancing(i),"danced well, the "+kind+" follows ("+i.Status+")");
            Check(Partner(s).Observers[0].Suspicion==25,"they trust you: 40 drops by RhythmDelta(1) to 25, got "+Partner(s).Observers[0].Suspicion);
            Check(i.DurationSeconds==0,"the first reply waits after the timed trust dance");
            Talk(s,i,Every(Smooth));Check(i.Score==1,"the talk after the dance scores itself, got "+i.Score);
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
            Check(p.NpcSpeech=="Nah. Not feeling it."&&p.NpcSpeaker=="FESTIVALGOER"&&p.NpcSpeechUntil>s.State.SimulationSeconds,"failed trust dance gives the owner a readable partner rejection for "+kind+": speech=\""+p.NpcSpeech+"\"");
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
            for(int festival=0;festival<Festivals.Count;festival++)for(int seed=0;seed<300;seed++)for(int turn=0;turn<FestivalSimulation.ConversationTurns;turn++)
            {
                var t=ConversationLines.Turn(topic,person,DialogueGrammar.Roles[seed%4],festival,turn,turn==0?-1:seed%4,seed%2==0,seed%3,new ContentRandom(seed*7+turn));
                foreach(var reply in t.Replies)Check(!DialogueGrammar.HasNarcTell(reply),"a player's reply carries a narc tell: "+reply);
                foreach(var line in t.Replies.Concat(new[]{t.Line,t.Reaction}))Check(line.Length<=80,topic+" "+person+" line over 80 characters: "+line);
            }
    }

    // A chat with someone of this role on festival 0 (night 1 for a clue holder), a reply of `grade` every turn: what it left
    // you feeling, and whether any of their lines carried a narc's cop-speak.
    static (string hunch,bool copSpeak,string lines) Read(string role,int seed,int grade,string kind="Conversation",string twist="",int stockSold=0,int level=-1,int festival=0)
    {
        var s=Pair(kind,seed);Partner(s).Role=role;Partner(s).Twist=twist;Partner(s).StockSold=stockSold;
        s.State.LevelIndex=level<0?(role=="ClueHolder"?1:0):level;s.State.FestivalIndex=festival;
        var i=Talk(s,Start(s,kind),Every(grade));var lines=string.Join(" / ",i.Turns.Select(t=>t.Line));
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
        double buyerBuys=Share("Buyer",Smooth,r=>r.hunch==ConversationLines.HunchBuyer),regularSells=Share("Regular",Smooth,r=>r.hunch=="You get the feeling they've got something to sell.");
        Check(buyerBuys>=.7&&regularSells>=.7,"after a smooth chat a buyer feels like they'd buy "+buyerBuys.ToString("0.00")+" of the time, and a regular feels like they sell "+regularSells.ToString("0.00"));
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
    static void ARegularTellLeavesASellerHunch()
    {
        foreach(var kind in new[]{"Conversation","Buy"})
        {
            var read=Read("Regular",7,Smooth,kind);
            Check(read.hunch=="You get the feeling they've got something to sell.","a regular's clear "+kind+" leaves the exact seller hunch, got: "+read.hunch);
        }
    }
    static void ASellerHunchUsesTheHostCategoryNotRemainingStock()
    {
        foreach(int festival in new[]{0,1})foreach(int level in new[]{0,1,2,3})
        {
            var dry=Read("Regular",7,Smooth,stockSold:99,level:level,festival:festival);
            Check(dry.hunch=="You get the feeling they've got something to sell.","a sold-out regular still belongs to the seller category");
            var guard=Read("Regular",7,Smooth,twist:FestivalSimulation.VipGuard,level:level,festival:festival);
            Check(guard.hunch=="You get the feeling they're just here for the music.","the regular-role VIP guard is never a seller");
            var clue=Read("ClueHolder",7,Smooth,level:level,festival:festival);
            Check(clue.hunch==(level%2==1?"You get the feeling they've got something to sell.":"You get the feeling they're just here for the music."),"a clue holder is a seller only at night");
            if(level%2==1)
            {
                var buy=Read("ClueHolder",7,Smooth,"Buy",level:level,festival:festival);
                Check(buy.hunch=="You get the feeling they've got something to sell.","a nighttime clue holder's actual buy leaves the seller hunch");
            }
        }
    }
    static void ANoTellTalkKeepsMusicAndOtherTellsKeepTheirHunches()
    {
        foreach(var role in new[]{"Regular","ClueHolder","Narc","Buyer"})
        {
            var vague=Read(role,7,Awkward);
            Check(vague.hunch=="You get the feeling they're just here for the music.","no tell leaves music for "+role);
            if(role!="Buyer")Check(Read(role,7,Awkward,"Buy").hunch==vague.hunch,"a no-tell buy leaves music for "+role);
        }
        Check(Read("Narc",7,Smooth).hunch=="You get the feeling they might be a cop.","a narc's hunch is unchanged");
        Check(Read("Buyer",7,Smooth).hunch=="You get the feeling they'd buy if you offered.","a buyer's hunch is unchanged");
    }
    // ECON-7 replaces only the landmark hunch: the spoken random landmark remains, without exposing the real clue trail.
    static void AClueHolderNamesALandmark()
    {
        int named=0;
        for(int seed=1;seed<=200;seed++)
        {
            var read=Read("ClueHolder",seed,Smooth);if(read.hunch==ConversationLines.HunchMusic)continue;named++;
            Check(read.hunch=="You get the feeling they've got something to sell.","a night clue holder leaves the seller hunch, got: "+read.hunch);
            Check(FestivalLines.Parts["landmark"].Any(entry=>read.lines.IndexOf(entry.Length>1&&entry[1]==':'?entry.Substring(2):entry,StringComparison.OrdinalIgnoreCase)>=0),"the clue holder still speaks an authored landmark: "+read.lines);
        }
        Check(named>=140,"a smooth chat with a clue holder names a landmark at least .70 of the time, got "+named/200.0);
    }
    // The hunch comes with the last turn, once every line it reads is said, and only from a chat or a buy: a sale's buyer is
    // buying, and security is security.
    static void OnlyAFestivalgoerTalkLeavesAHunch()
    {
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);var i=Start(s,kind);for(int turn=0;turn<FestivalSimulation.ConversationTurns-2;turn++)Say(s,i,Smooth);
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
        foreach(var grades in new[]{Every(Smooth),Every(Awkward)})
        {
            var s=Pair("Buy");Partner(s).Role="Narc";var p=s.Player("p0");p.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});
            var i=Start(s,"Buy");Check(p.Life=="Alive"&&p.Cash==15&&i.Turns.Count==1,"asking a narc to sell starts a talk, the $5 held, no bust yet");
            CommandResult last=null;
            foreach(int grade in grades)last=Say(s,i,grade);
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

    // A sale makes an ask (StartSale's Amount): Fair $10, High $15 or Greedy $20 before the multipliers. Fair pays in full at .75
    // and half at .4; High in full at .75, and between .5 and .75 they talk you down to Fair's $10; Greedy in full at .875,
    // talked down to $10 from .6; less is no sale. The dose's multiplier follows, and the offer's price is what a full sale pays.
    static InteractionState Sale(FestivalSimulation s,int ask){var started=Act(s,"p0","StartSale",Partner(s).Id,"stock_lsd",amount:ask);Check(started.Accepted,"setup: the sale at ask "+ask+" starts: "+started.Reason);return s.Interaction(s.Player("p0").InteractionId);}
    static void EachAskPaysByItsTable()
    {
        int Fair=FestivalSimulation.FairAsk,High=FestivalSimulation.HighAsk,Greedy=FestivalSimulation.GreedyAsk;
        foreach(var (score,fair,high,greedy) in new[]{(1.0,10,15,20),(.875,10,15,20),(.75,10,15,10),(.6,5,10,10),(.5,5,10,0),(.25,0,0,0)})
        {
            int[] paid={FestivalSimulation.SalePay(Fair,score),FestivalSimulation.SalePay(High,score),FestivalSimulation.SalePay(Greedy,score)};
            Check(paid[0]==fair&&paid[1]==high&&paid[2]==greedy,"at "+score+" Fair, High and Greedy pay "+fair+", "+high+", "+greedy+" before multipliers, got "+string.Join(", ",paid));
        }
        Check(FestivalSimulation.SalePay(Fair,.4)==5&&FestivalSimulation.SalePay(Fair,.3999)==0&&FestivalSimulation.SalePay(Fair,.7499)==5&&FestivalSimulation.SalePay(High,.4999)==0&&FestivalSimulation.SalePay(High,.7499)==10
            &&FestivalSimulation.SalePay(Greedy,.5999)==0&&FestivalSimulation.SalePay(Greedy,.8749)==10,"each floor and need is its own edge");
        Check(FestivalSimulation.SaleNeeds(Fair)==.75&&FestivalSimulation.SaleNeeds(High)==.75&&FestivalSimulation.SaleNeeds(Greedy)==.875,"the score each ask needs for its full price");
        foreach(int bad in new[]{-1,3})
        {
            var s=Pair("Sale");var refused=Act(s,"p0","StartSale",Partner(s).Id,"stock_lsd",amount:bad);
            Check(!refused.Accepted&&refused.Reason=="Ask Fair, High or Greedy"&&s.Player("p0").InteractionId==""&&s.Player("p0").Inventory.Exists(x=>x.ItemId=="stock_lsd"),"ask "+bad+" is refused and keeps the stock: "+refused.Reason);
            s.Player("p0").Inventory.Clear();
            Check(Act(s,"p0","StartSale",Partner(s).Id,"stock_lsd",amount:bad).Reason=="Ask Fair, High or Greedy","the ask is checked before the stock");
        }
        // At dose 2 (x1.25), whole talks: all smooth (1) pays the ask's price, all so-so (.5) Fair's half, High talked down to Fair's
        // price, Greedy nothing; all awkward (0) is no sale at any ask.
        foreach(var (ask,smooth,soso) in new[]{(Fair,13,6),(High,19,13),(Greedy,25,0)})
            foreach(var (grade,expected) in new[]{(Smooth,smooth),(SoSo,soso),(Awkward,0)})
            {
                var s=Pair("Sale");s.State.TripperId="tripper";s.State.Doses.Add(new PlayerDose{PlayerId="tripper",Dose=2});var p=s.Player("p0");int cash=p.Cash,sales=s.State.LevelSales;
                var i=Sale(s,ask);Check(i.Ask==ask,"the interaction keeps its ask");
                if(grade==Smooth)Check(FestivalSimulation.SalePrice(s.State,p,Partner(s),ask)==expected,"ask "+ask+"'s offer names what a full sale pays, "+expected+", got "+FestivalSimulation.SalePrice(s.State,p,Partner(s),ask));
                Talk(s,i,Every(grade));
                Check(p.Cash-cash==expected&&s.State.LevelSales-sales==expected,"ask "+ask+" at "+i.Score+" pays "+expected+" at dose 2, got "+(p.Cash-cash));
                Check(p.Inventory.Exists(x=>x.ItemId=="stock_lsd")==(expected==0),"only a no-sale hands the stock back");
            }
    }

    // A High or Greedy ask that ends in no sale, or that you walk away from, adds 15 or 30 suspicion of you to the buyer and to
    // every watcher, on top of what the talk or the walk-off adds, and that buyer won't buy from you again this level. A failed
    // Fair sale moves them by RhythmDelta only, and the buyer still hears your next offer. A talk holds you still, so a buyer who
    // walks out of reach costs you nothing.
    static void APushedAskThatFailsRaisesSuspicion()
    {
        foreach(string end in new[]{"fail","end","leave"})
            foreach(var (ask,pushed) in new[]{(FestivalSimulation.FairAsk,0),(FestivalSimulation.HighAsk,15),(FestivalSimulation.GreedyAsk,30)})
            {
                var s=Pair("Sale");var buyer=Partner(s);var watcher=new NpcState{Id="watcher",X=0,Z=6,Yaw=180};watcher.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=20});s.State.Npcs.Add(watcher);
                var i=Sale(s,ask);Check(i.WitnessIds.Contains("watcher")&&i.WitnessIds.Contains(buyer.Id),"setup: the watcher and the buyer see the sale");
                double Of(NpcState n)=>n.Observers.Find(o=>o.PlayerId=="p0")?.Suspicion??0;
                if(end=="end"){Say(s,i,Awkward);Check(Act(s,"p0","EndChat",i.Id).Accepted,"setup: you walk away from the sale");}
                else if(end=="leave"){Say(s,i,Awkward);buyer.X=6;s.Tick(.1);Check(i.Status=="Moved away or target changed","setup: the buyer walks out of reach of the sale ("+i.Status+")");buyer.X=0;}
                else Talk(s,i,Every(Awkward));
                // Ending the talk moves its watchers, the buyer facing you among them, 20 (Cancel); a talk that ends at 0 moves them by
                // RhythmDelta(0), 20 too. A buyer who walks off moves them by nothing.
                int extra=end=="leave"?0:pushed;double moved=end=="leave"?0:20,watched=20+moved+extra,bought=moved+extra;
                Check(Of(watcher)==watched&&Of(buyer)==bought,end+" at ask "+ask+" leaves the watcher at "+watched+" and the buyer at "+bought+", got "+Of(watcher)+" and "+Of(buyer));
                var p=s.Player("p0");p.X=0;var again=Act(s,"p0","StartSale",buyer.Id,"stock_lsd");
                Check(again.Accepted==(extra==0)&&(extra==0||again.Reason=="Not after that ask. They won't buy from you again."),end+" at ask "+ask+": the buyer "+(extra==0?"hears your next offer":"won't buy from you again")+": "+again.Reason);
                if(extra>0)Check(p.Inventory.Exists(x=>x.ItemId=="stock_lsd"),"a refused offer keeps the stock");
            }
        // Another seller is still welcome.
        var crew=Pair("Sale");crew.State.Phase="Shopping";crew.AddPlayer("p1","P1");crew.State.Phase="Playing";var friend=crew.Player("p1");friend.X=0;friend.Z=0;friend.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
        Talk(crew,Sale(crew,FestivalSimulation.GreedyAsk),Every(Awkward));
        Check(crew.Execute("p1",new GameCommand{Id="talk"+(sequence++),Kind="StartSale",TargetId=Partner(crew).Id,ItemId="stock_lsd"}).Accepted,"a friend can still sell to them");
    }

    // A snapshot keeps a sale's ask; one from before asks restores it as Fair, and an ask outside Fair to Greedy is refused.
    static void ASaleKeepsItsAskThroughASnapshot()
    {
        var s=Pair("Sale");var i=Sale(s,FestivalSimulation.GreedyAsk);
        RoundState Saved(Action<JsonObject> edit){var json=JsonNode.Parse(JsonSerializer.Serialize(s.State,new JsonSerializerOptions{IncludeFields=true}));edit(json["Interactions"].AsArray().Single(x=>x["Id"].GetValue<string>()==i.Id).AsObject());return JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),new JsonSerializerOptions{IncludeFields=true});}
        var kept=new FestivalSimulation();kept.Restore(Saved(_=>{}));Check(kept.Interaction(i.Id).Ask==FestivalSimulation.GreedyAsk,"a saved Greedy sale restores Greedy");
        var older=new FestivalSimulation();older.Restore(Saved(o=>o.Remove("Ask")));Check(older.Interaction(i.Id).Ask==FestivalSimulation.FairAsk,"a sale saved before asks restores Fair");
        foreach(int bad in new[]{-1,3})
        {
            string failure="nothing thrown";try{new FestivalSimulation().Restore(Saved(o=>o["Ask"]=bad));}catch(ArgumentException error){failure=error.Message;}
            Check(failure=="Incomplete snapshot","a saved ask of "+bad+" is refused, got "+failure);
        }
    }

    // Offering your last unit from your hand puts your next gear in your hand for the talk. A talk that fails, or that you walk
    // away from, gives the unit back to the hand it left, equipped again. A unit offered from the pocket goes back there.
    static void AHandedBackUnitGoesBackInYourHand()
    {
        foreach(bool walk in new[]{false,true})foreach(bool held in new[]{true,false})
        {
            var s=Pair("Sale");var p=s.Player("p0");p.Inventory.Add(new ItemStack{ItemId="merch_bag",Count=1});p.EquippedItemId=held?"stock_lsd":"merch_bag";
            var i=Sale(s,FestivalSimulation.FairAsk);
            Check(p.EquippedItemId=="merch_bag"&&!p.Inventory.Exists(x=>x.ItemId=="stock_lsd"),"setup: the last unit is on offer and the merch bag is in your hand");
            if(walk)Check(Act(s,"p0","EndChat",i.Id).Accepted,"setup: you walk away from the sale");else Talk(s,i,Every(Awkward));
            Check(p.Inventory.Exists(x=>x.ItemId=="stock_lsd"&&x.Count==1),"the unit comes back");
            Check(p.EquippedItemId==(held?"stock_lsd":"merch_bag"),(walk?"walking away":"a failed talk")+(held?" puts the unit back in your hand":" leaves the pocketed unit in your pocket")+", holding "+p.EquippedItemId);
        }
    }

    // Every sale says how it ended, with the amount, in its outcome (the chat window's last line and the HUD's message): SOLD with
    // what it paid, NO SALE with where the unit went, and a walk-off too. Result is the short stamp the HUD shows. A pushed ask
    // talked down says so.
    static void ASaleSaysHowItEnded()
    {
        ConversationOutcome Ended(FestivalSimulation s,InteractionState i)=>s.Player("p0").ConversationOutcomes.Find(o=>o.InteractionId==i.Id);
        var s=Pair("Sale");var i=Talk(s,Sale(s,FestivalSimulation.FairAsk),Every(Smooth));var o=Ended(s,i);
        Check(o.Notice=="SOLD Tongue Stamps  •  +$10"&&o.Result=="SOLD +$10","a paid sale says SOLD and what it paid: \""+o.Notice+"\" / \""+o.Result+"\"");
        s=Pair("Sale");i=Talk(s,Sale(s,FestivalSimulation.HighAsk),Every(SoSo));o=Ended(s,i);
        Check(o.Notice=="SOLD Tongue Stamps  •  +$10  •  talked down from $15"&&o.Result=="SOLD +$10","a High ask talked down says so: \""+o.Notice+"\"");
        s=Pair("Sale");i=Talk(s,Sale(s,FestivalSimulation.FairAsk),Every(Awkward));o=Ended(s,i);
        Check(o.Notice=="NO SALE  •  Tongue Stamps back in your hand"&&o.Result=="NO SALE","a refused sale says the unit is back in your hand: \""+o.Notice+"\"");
        // Hands that filled meanwhile (a friend's handoff, say) leave no room for it.
        s=Pair("Sale");var p=s.Player("p0");i=Sale(s,FestivalSimulation.FairAsk);foreach(var gear in new[]{"merch_bag","map","confetti"})p.Inventory.Add(new ItemStack{ItemId=gear,Count=1});
        Talk(s,i,Every(Awkward));o=Ended(s,i);
        Check(o.Notice=="NO SALE  •  Tongue Stamps dropped at your feet"&&o.Result=="NO SALE"&&s.State.Drops.Exists(d=>d.ItemId=="stock_lsd"),"with full hands it says the unit dropped at your feet: \""+o.Notice+"\"");
        s=Pair("Sale");i=Sale(s,FestivalSimulation.FairAsk);Check(Act(s,"p0","EndChat",i.Id).Accepted,"setup: you walk away");o=Ended(s,i);
        Check(o!=null&&o.Notice=="You walked off  •  Tongue Stamps back in your hand"&&o.Result=="NO SALE"&&o.Reaction==""&&s.Player("p0").NpcSpeaker=="","walking away says so, with no reaction of theirs: \""+o?.Notice+"\"");
        // A buyer who walks out of reach mid-talk walks off.
        s=Pair("Sale");i=Sale(s,FestivalSimulation.GreedyAsk);Partner(s).X=6;s.Tick(.1);o=Ended(s,i);
        Check(o!=null&&o.Notice=="They walk off  •  Tongue Stamps back in your hand"&&o.Result=="NO SALE","a buyer who leaves walks off, and it says so: \""+o?.Notice+"\"");
        // A sale cut short for any other reason (here the seller drops out of the game) is off.
        s=Pair("Sale");i=Sale(s,FestivalSimulation.FairAsk);s.Disconnect("p0");o=Ended(s,i);
        Check(o!=null&&o.Notice=="SALE OFF  •  Tongue Stamps back in your hand"&&o.Result=="NO SALE","a sale cut off says SALE OFF: \""+o?.Notice+"\"");
        // A cop who saw the deal busts you mid-sale, and the stock on offer goes with the rest.
        s=Pair("Sale");p=s.Player("p0");i=Sale(s,FestivalSimulation.FairAsk);
        s.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="ArrestWarning",X=p.X+1,Z=p.Z,Evidence=new List<EvidenceState>{new EvidenceState{PlayerId="p0",Kind="WitnessedDeal",DetainAt=s.State.SimulationSeconds}}});s.Tick(.1);o=Ended(s,i);
        Check(p.Life=="Detained"&&!p.Inventory.Exists(x=>x.ItemId=="stock_lsd")&&o!=null&&o.Notice=="BUSTED  •  security took your stock"&&o.Result=="BUSTED"&&o.Reaction=="","a bust mid-sale says BUSTED: \""+o?.Notice+"\"");
        // A wary buyer you lose in the dance first walks off, and it says so.
        s=Pair("Sale");Partner(s).Observers.Add(new ObserverState{PlayerId="p0",Suspicion=FestivalSimulation.TrustDanceSuspicion});i=Sale(s,FestivalSimulation.FairAsk);Check(i.TrustDance,"setup: they ask for a dance first");
        for(int guard=0;i.Status=="Active"&&guard<200;guard++)s.Tick(.1);o=Ended(s,i);
        Check(o.Notice=="They walk off  •  Tongue Stamps back in your hand"&&o.Result=="NO SALE","a lost trust dance says they walk off, and where the unit went: \""+o.Notice+"\"");
        Check(FestivalSimulation.MoneyNotice("LOST PROPERTY RETURNED",5)=="LOST PROPERTY RETURNED  •  +$5"&&FestivalSimulation.MoneyNotice("SECURITY FINE",-10)=="SECURITY FINE  •  −$10","the money notice every change of cash uses");
    }

    // A settled talk's outcome carries its Score, so the chat window's meter lands where the host settled it. A talk
    // cut short (walked away from, busted) has no score, -1, so the meter stays where it stood; so has an outcome from before.
    static void AnOutcomeCarriesItsSettledScore()
    {
        ConversationOutcome Ended(FestivalSimulation s,InteractionState i)=>s.Player("p0").ConversationOutcomes.Find(o=>o.InteractionId==i.Id);
        foreach(var kind in Kinds)
        {
            var s=Pair(kind);var i=Talk(s,Start(s,kind),Smooth,SoSo,Smooth,Awkward);var o=Ended(s,i);
            Check(i.Score>0&&o!=null&&o.Score==i.Score,"a settled "+kind+"'s outcome carries its score, "+i.Score+", got "+o?.Score);
        }
        var cut=Pair("Sale");var sale=Sale(cut,FestivalSimulation.FairAsk);Check(Say(cut,sale,Smooth).Accepted,"setup: a smooth first reply");
        Check(Act(cut,"p0","EndChat",sale.Id).Accepted,"setup: you walk away");
        Check(Ended(cut,sale)?.Score==-1,"a sale walked away from has no score, got "+Ended(cut,sale)?.Score);
        var older=JsonSerializer.Deserialize<ConversationOutcome>("{\"DeliveryId\":\"old\"}",new JsonSerializerOptions{IncludeFields=true});
        Check(older.Score==-1,"an outcome from before scores has none, got "+older.Score);
    }

    // A talk in a snapshot from before conversations (no turns, no trust dance) restores, and carries on as a conversation.
    static void AnOlderSnapshotRestores()
    {
        var s=Pair("Conversation");var i=Start(s,"Conversation");
        var json=JsonNode.Parse(JsonSerializer.Serialize(s.State,new JsonSerializerOptions{IncludeFields=true}));
        foreach(var talk in json["Interactions"].AsArray()){var o=talk.AsObject();o.Remove("Turns");o.Remove("TrustDance");o.Remove("Hunch");}
        foreach(var player in json["Players"].AsArray()){var o=player.AsObject();foreach(var field in new[]{"DialogueRevision","ConversationOutcomeSequence","ConversationDismissedSequence","ConversationThreadId","ConversationDismissedId","ConversationOutcomes"})o.Remove(field);}
        var saved=JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),new JsonSerializerOptions{IncludeFields=true});
        var restored=new FestivalSimulation();restored.Restore(saved);var old=restored.Interaction(i.Id);
        Check(restored.Player("p0").ConversationOutcomes!=null&&restored.Player("p0").ConversationOutcomes.Count==0&&restored.Player("p0").ConversationDismissedSequence==0,"older owner outcome fields keep safe defaults");
        Check(old.Turns!=null&&old.Turns.Count==1&&!old.TrustDance,"an old talk restores at its first turn ("+old.Turns?.Count+")");
        restored.Tick(60);Check(old.Status=="Active"&&old.Turns.Count==1&&Live(old).Pick==-1,"a restored legacy talk waits for its first reply");
        Talk(restored,old,Every(Awkward));Check(old.Status=="Complete"&&old.Turns.Count==FestivalSimulation.ConversationTurns&&old.Score==0,"four accepted awkward replies settle the restored talk");
    }
}
