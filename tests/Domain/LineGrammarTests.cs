using System;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;

// DIALOG-4: the engine festivalgoer lines are built with. A kind's table holds whole-line templates; each {slot} in one fills from
// that slot's own table, festival tags keep each map's parts to itself, and agreement keeps the joins grammatical: a/an by the
// next word's sound, verbs and nouns that agree with the part before them, and a capital at the start of every sentence.
public static class LineGrammarTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("LineGrammar: "+message);}

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{EverySlotFillsFromItsOwnTable,AKindDrawsEachOfItsTemplatesAndParts,TheSameSeedGivesTheSameLine,
            EachFestivalUsesOnlyItsOwnTaggedParts,AOrAnFollowsTheSoundOfTheNextWord,VerbsAndNounsAgreeWithThePartBeforeThem,
            EverySentenceStartsWithACapital,SpacesStayTidyAroundEmptyParts,DressingIsSaidButLeftOutOfTheBody,AuthoringMistakesFailLoudly,
            EveryTalkRoundHasItsOwnRepliesInEveryStyle})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" line grammar test(s) failed:\n"+string.Join("\n",failures));
    }

    static (string name,string[] entries) T(string name,params string[] entries)=>(name,entries);
    static LineGrammar G(params (string name,string[] entries)[] tables){var map=new Dictionary<string,string[]>();foreach(var t in tables)map[t.name]=t.entries;return new LineGrammar(map);}
    // One template, filled for Palm Mirage.
    static string One(string template,params (string name,string[] entries)[] parts)=>G(new[]{T("line",template)}.Concat(parts).ToArray()).Line("line",0,1);
    static HashSet<string> Lines(LineGrammar g,string kind,int festival,int draws){var lines=new HashSet<string>();for(int seed=0;seed<draws;seed++)lines.Add(g.Line(kind,festival,seed));return lines;}
    static string Failure(Action action){try{action();}catch(Exception error){return error.GetType().Name+": "+error.Message;}return "nothing thrown";}

    // Matt's example from the queue entry, built part by part.
    static void EverySlotFillsFromItsOwnTable()
    {
        var g=G(T("bark","{greeting} {who} {did} {what} {where}."),T("greeting","Hey legend,"),T("who","my left shoe"),T("did","just traded itself for"),T("what","a glow stick"),T("where","behind the {landmark}"),T("landmark","0:Ferris wheel","1:Man"));
        Check(g.Line("bark",0,1)=="Hey legend, my left shoe just traded itself for a glow stick behind the Ferris wheel.","Palm Mirage fills every slot, a part's own slots included, got "+g.Line("bark",0,1));
        Check(g.Line("bark",1,1)=="Hey legend, my left shoe just traded itself for a glow stick behind the Man.","Ember Playa fills in its own landmark, got "+g.Line("bark",1,1));
    }

    // Several templates per kind, so the shape of the sentence changes as well as its words.
    static void AKindDrawsEachOfItsTemplatesAndParts()
    {
        var g=G(T("line","{who} waved.","Look, {who} waved!","Did {who} wave?"),T("who","my friend","the dancer","a stranger"));
        var lines=Lines(g,"line",0,300); string all=string.Join(" / ",lines);
        Check(lines.Any(l=>l.EndsWith(" waved."))&&lines.Any(l=>l.StartsWith("Look, "))&&lines.Any(l=>l.StartsWith("Did ")),"every template's shape shows up in 300 draws: "+all);
        foreach(var who in new[]{"my friend","the dancer","a stranger"}) Check(lines.Any(l=>l.IndexOf(who,StringComparison.OrdinalIgnoreCase)>=0),"every part shows up, missing '"+who+"': "+all);
        Check(lines.Count==9,"3 templates x 3 parts make 9 different lines, got "+lines.Count+": "+all);
    }

    static void TheSameSeedGivesTheSameLine()
    {
        var g=G(T("line","{size} {colour} {thing} waved.","Look, a {thing}!"),T("size","big","small","tiny","huge"),T("colour","red","blue","green"),T("thing","hat","shoe","cooler","{colour} flag"));
        var seen=new HashSet<string>();
        for(int seed=-200;seed<200;seed++) for(int festival=0;festival<2;festival++)
        {
            string line=g.Line("line",festival,seed);
            Check(line==g.Line("line",festival,seed)&&line==g.Line("line",festival,new ContentRandom(seed)),"seed "+seed+" always gives '"+line+"', from the seed or its ContentRandom");
            seen.Add(line);
        }
        // 78 lines are possible. ContentRandom's first draws correlate across neighbouring seeds, so 400 of them reach about half;
        // a table big enough for real content (20,000 seeds into millions of lines) still gives a new line almost every seed.
        Check(seen.Count>=30,"different seeds give different lines, got "+seen.Count);
        // One stream can carry several lines (DialogueGrammar draws a chat's opener, questions and answers in a fixed order): the
        // second line carries on where the first left off, the same way every time.
        bool carriedOn=false;
        for(int seed=0;seed<50;seed++)
        {
            var once=new ContentRandom(seed); var again=new ContentRandom(seed);
            string first=g.Line("line",0,once),second=g.Line("line",0,once);
            Check(first==g.Line("line",0,again)&&second==g.Line("line",0,again),"a shared stream replays line for line, seed "+seed);
            Check(first==g.Line("line",0,seed),"the first line off a stream is the seed's line, seed "+seed);
            if(second!=first) carriedOn=true;
        }
        Check(carriedOn,"the second line off a shared stream draws on from the first, not from the start again");
    }

    static void EachFestivalUsesOnlyItsOwnTaggedParts()
    {
        var g=G(T("line","Meet me by the {place}.","0:Polo people meet by the {place}.","1:Dust people meet by the {place}."),T("place","0:Ferris wheel","0:VIP deck","1:Man","1:art car","gate"));
        string[][] own={new[]{"Ferris wheel","VIP deck","Polo"},new[]{"Man","art car","Dust"}};
        for(int festival=0;festival<2;festival++)
        {
            string all=string.Join("\n",Lines(g,"line",festival,400));
            foreach(var word in own[festival].Append("gate")) Check(all.Contains(word),"festival "+festival+" uses its own '"+word+"': "+all);
            foreach(var word in own[1-festival]) Check(!all.Contains(word),"festival "+festival+" never uses the other festival's '"+word+"': "+all);
            Check(all.IndexOf(':')<0,"the tags never show: "+all);
        }
        foreach(int festival in new[]{-1,2}) Check(Failure(()=>g.Line("line",festival,1)).StartsWith("ArgumentException"),"there is no festival "+festival+", got "+Failure(()=>g.Line("line",festival,1)));
        string empty=Failure(()=>G(T("line","By the {place}."),T("place","0:Ferris wheel")));
        Check(empty.StartsWith("ArgumentException")&&empty.Contains("{place}")&&empty.Contains("festival 1"),"a table with nothing for Ember Playa is refused when the grammar is built, got "+empty);
    }

    static void AOrAnFollowsTheSoundOfTheNextWord()
    {
        foreach(var pair in new[]{
            new[]{"Grab a apple, a hour and a 8-ball.","Grab an apple, an hour and an 8-ball."},
            new[]{"Grab an glow stick, an unicorn and an one-off.","Grab a glow stick, a unicorn and a one-off."},
            new[]{"Grab an UFO, an euro and an useful map.","Grab a UFO, a euro and a useful map."},
            new[]{"Grab a LED, a MC and a x-ray.","Grab an LED, an MC and an x-ray."},
            new[]{"Grab a uninvited guest and a honest answer.","Grab an uninvited guest and an honest answer."},
            new[]{"Grab a \"umbrella\" for me.","Grab an \"umbrella\" for me."},
            new[]{"a owl took a umbrella.","An owl took an umbrella."},
            // A capital A inside a sentence is the letter, not the article; a.m. is neither an article nor a sentence's end.
            new[]{"Plan A is a idea.","Plan A is an idea."},
            new[]{"We met at 3 a.m. and left a hour later.","We met at 3 a.m. and left an hour later."}})
            Check(One(pair[0])==pair[1],"'"+pair[0]+"' reads '"+pair[1]+"', got '"+One(pair[0])+"'");
        Check(One("Hand me a {thing}.",T("thing","umbrella"))=="Hand me an umbrella.","the article agrees with the part after it, got "+One("Hand me a {thing}.",T("thing","umbrella")));
        Check(One("Hand me an {thing}.",T("thing","{colour} cooler"),T("colour","blue"))=="Hand me a blue cooler.","and with a part's own part, got "+One("Hand me an {thing}.",T("thing","{colour} cooler"),T("colour","blue")));
    }

    // A part starting "+" is plural. {singular|plural} takes the form for the last part filled.
    static void VerbsAndNounsAgreeWithThePartBeforeThem()
    {
        var g=G(T("line","{who} {has|have} the snacks and keep{s|} waving."),T("who","my friend","+my friends"));
        var lines=Lines(g,"line",0,100);
        Check(lines.SetEquals(new[]{"My friend has the snacks and keeps waving.","My friends have the snacks and keep waving."}),"a singular and a plural part, each agreeing, got "+string.Join(" / ",lines));
        var counts=Lines(G(T("line","I lost {count} {glow stick|glow sticks}."),T("count","one","+three")),"line",0,100);
        Check(counts.SetEquals(new[]{"I lost one glow stick.","I lost three glow sticks."}),"a noun agrees with the count before it, got "+string.Join(" / ",counts));
        // A part's own number outlasts the slots inside it, and a verb part drawn after it agrees too.
        string nested=One("{who} {did}.",T("who","+the dancers by {where}"),T("where","the gate"),T("did","{has|have} been here since {when}"),T("when","sunrise"));
        Check(nested=="The dancers by the gate have been here since sunrise.","got "+nested);
        Check(One("{is|are} it {who}?",T("who","+my friends"))=="Is it my friends?","before any part is filled, the singular form, got "+One("{is|are} it {who}?",T("who","+my friends")));
    }

    static void EverySentenceStartsWithACapital()
    {
        Check(One("{who} waved.",T("who","my friend"))=="My friend waved.","a part that opens the line gets the capital, got "+One("{who} waved.",T("who","my friend")));
        Check(One("hey. the bass dropped! we ran? sure... we did.")=="Hey. The bass dropped! We ran? Sure... we did.","every sentence, but not after an ellipsis, got "+One("hey. the bass dropped! we ran? sure... we did."));
        Check(One("\"hey,\" said the dancer. \"over here.\"")=="\"Hey,\" said the dancer. \"Over here.\"","inside an opening quote too, got "+One("\"hey,\" said the dancer. \"over here.\""));
    }

    static void SpacesStayTidyAroundEmptyParts()
    {
        string line=One("  {greet} {who} waved {tail} , then left {tail} !  ",T("greet",""),T("who","my  friend"),T("tail",""));
        Check(line=="My friend waved, then left!","one space between words and none before punctuation, got '"+line+"'");
    }

    // A slot written {~name} is dressing: a hello or a tail said around what the line is about. The line says it; the line's body
    // (what the repeat memory and the line counts go by) leaves it out, with every part inside it. "Hey, Sam! My friend waved."
    // and "Oi! My friend waved." are one body, so a festivalgoer can't pass the same sentence off as new behind a new hello.
    static void DressingIsSaidButLeftOutOfTheBody()
    {
        var g=G(T("line","{~hi} {who} waved{~tail}."),T("hi","Hey, {name}!","Oi!"),T("name","Sam","Kit"),T("who","my friend","+my friends"),T("tail","",", again"));
        var lines=new HashSet<string>();var bodies=new HashSet<string>();
        for(int seed=0;seed<300;seed++)
        {
            string line=g.Line("line",0,new ContentRandom(seed),out var body);
            Check(line==g.Line("line",0,seed),"asking for the body changes nothing about the line, seed "+seed);
            lines.Add(line);bodies.Add(body);
        }
        Check(lines.Count>=8&&lines.Contains("Hey, Sam! My friend waved, again.")&&lines.Contains("Oi! My friend waved."),"the line says its dressing: "+string.Join(" / ",lines));
        Check(bodies.SetEquals(new[]{"My friend waved.","My friends waved."}),"the body is the line without its dressing, nor the dressing's own parts: "+string.Join(" / ",bodies));
    }

    // Speech bubbles hold 80 characters. A body shared between two rounds of one topic and style makes the rounds read alike.
    static readonly string[] TheirRoundLines={"line.talk.ask","line.talk.close","line.sale.small","line.sale.close","line.buy.small","line.buy.close","line.security.close"};
    static void EveryTalkRoundHasItsOwnRepliesInEveryStyle()
    {
        Check(ConversationLines.Rounds.Length==4,"a talk has four rounds, got "+ConversationLines.Rounds.Length);
        var faults=new List<string>();
        HashSet<string> Draw(string kind,int festival)
        {
            var bodies=new HashSet<string>();
            for(int seed=0;seed<200;seed++)
            {
                string line=ConversationLines.Grammar.Line(kind,festival,new ContentRandom(seed),out var body);bodies.Add(body);
                if(line.Length>80||line.IndexOfAny(new[]{'{','}'})>=0||!char.IsUpper(line[0])||".!?".IndexOf(line[line.Length-1])<0||DialogueGrammar.HasNarcTell(line))faults.Add(kind+" on festival "+festival+": \""+line+"\"");
            }
            if(bodies.Count<6)faults.Add(kind+" on festival "+festival+" says only "+bodies.Count+" different things");
            return bodies;
        }
        foreach(var topic in ConversationLines.Topics)foreach(var style in ConversationLines.Styles)for(int festival=0;festival<Festivals.Count;festival++)
        {
            var earlier=new List<HashSet<string>>();
            for(int round=0;round<ConversationLines.Rounds.Length;round++)
            {
                string kind=ConversationLines.ReplyKind(topic,round,style);
                Check(kind=="reply."+topic+"."+ConversationLines.Rounds[round]+"."+style,"a round's reply pool is named for its topic, round and style, got "+kind);
                var own=Draw(kind,festival);
                if(earlier.Exists(e=>e.Overlaps(own)))faults.Add(kind+" on festival "+festival+" repeats a sentence of an earlier round");
                earlier.Add(own);
            }
        }
        foreach(var kind in TheirRoundLines)for(int festival=0;festival<Festivals.Count;festival++)Draw(kind,festival);
        Check(faults.Count==0,faults.Count+" fault(s) in the talk rounds' lines:\n  "+string.Join("\n  ",faults.Take(20)));
    }

    static void AuthoringMistakesFailLoudly()
    {
        string kind=Failure(()=>G(T("line","Hi.")).Line("chat",0,1));
        Check(kind.StartsWith("ArgumentException")&&kind.Contains("{chat}"),"an unknown kind names itself, got "+kind);
        string slot=Failure(()=>One("Hi {nope}."));
        Check(slot.StartsWith("ArgumentException")&&slot.Contains("{nope}"),"an unknown slot names itself, got "+slot);
        string unclosed=Failure(()=>One("Hi {who.",T("who","you")));
        Check(unclosed.StartsWith("ArgumentException")&&unclosed.Contains("Hi {who."),"an unclosed slot shows its part, got "+unclosed);
        string loop=Failure(()=>One("{loop}",T("loop","and {loop}")));
        Check(loop.StartsWith("InvalidOperationException")&&loop.Contains("{loop}"),"a part that names its own table stops, instead of overflowing the stack, got "+loop);
    }
}
