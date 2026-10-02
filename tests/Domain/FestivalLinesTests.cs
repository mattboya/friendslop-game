using System;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;

// DIALOG-4: festivalgoers almost never repeat themselves. Every line they say (chat barks, challenge lines and their awkward
// twins, and the chat check's openers, questions, answers and doorman lines) is built by FestivalLines' grammar from parts, at
// least 1,000 different lines per kind of moment on each festival, and each player's DialogueHistory remembers what they've
// heard, so a line they heard lately is drawn again.
public static class FestivalLinesTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("FestivalLines: "+message);}

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{EveryKindHasAThousandLinesOnEachFestival,EveryLineReadsCleanly,EachFestivalKeepsToItsOwnPlaces,OnlyANarcsAnswersCarryATell,
            TheSevenChallengeContextsAreBuiltFromTheGrammar,AChallengeLineIsNotRepeatedWithinThirty,ALineHeardBehindOneHelloIsNotNewBehindAnother,AnAffectedPlayersAwkwardLineIsNotRepeatedWithinThirty,ARedrawGivesUpAfterFive,AFestivalgoersChatIsNotRepeatedWithinThirty})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" festival line test(s) failed:\n"+string.Join("\n",failures));
    }

    // 20,000 seeded draws of each kind: 10,000 on each festival, and each festival on its own gives at least 1,000 different bodies.
    const int Draws=10000;
    // DIALOG-2's bubbles wrap at 26 letters and show 3 rows, so a line stays within about 80 characters.
    const int Longest=80;
    // Places that exist on one map only; the other festival never mentions them.
    static readonly string[][] Places={new[]{"polo field","Ferris wheel","iced matcha","matcha"},new[]{"playa","art car","the Man","The Man"}};
    static readonly Dictionary<string,string[][]> drawn=new Dictionary<string,string[][]>();
    // The first Draws lines of a kind on a festival, seeds 0 up: each seed is a fresh stream, as every draw in the game is.
    static string[] Lines(string kind,int festival)=>Drawn(kind,festival)[0];
    // And their bodies: each line without its dressing (a hello or a tail), what the line is about.
    static string[] Bodies(string kind,int festival)=>Drawn(kind,festival)[1];
    static string[][] Drawn(string kind,int festival)
    {
        string key=kind+"@"+festival;
        if(!drawn.TryGetValue(key,out var both)){both=new[]{new string[Draws],new string[Draws]};for(int seed=0;seed<Draws;seed++)both[0][seed]=FestivalLines.Grammar.Line(kind,festival,new ContentRandom(seed),out both[1][seed]);drawn[key]=both;}
        return both;
    }
    // Each kind on each festival, with the first line that breaks `rule` (null when none does).
    static void EveryLine(string what,Func<string,int,string,bool> rule)
    {
        var broken=new List<string>();
        foreach(var kind in FestivalLines.Kinds) for(int festival=0;festival<Festivals.Count;festival++)
        {
            var bad=Lines(kind,festival).FirstOrDefault(line=>!rule(kind,festival,line));
            if(bad!=null) broken.Add(kind+" on festival "+festival+": \""+bad+"\"");
        }
        Check(broken.Count==0,what+", but "+broken.Count+" kind(s) don't:\n  "+string.Join("\n  ",broken));
    }

    static void EveryKindHasAThousandLinesOnEachFestival()
    {
        Check(FestivalLines.Kinds.Contains("bark")&&DialogueCatalog.Contexts.All(c=>FestivalLines.Kinds.Contains(c)&&FestivalLines.Kinds.Contains(c+"_awkward")),"the grammar has the chat barks and each challenge context's line and awkward twin: "+string.Join(", ",FestivalLines.Kinds));
        // DIALOG-1's chat check: each persona's opener, the three questions, each role's three answers, and POLO-4's doorman.
        var chat=DialogueGrammar.Personas.Select(p=>"opener."+p).Concat(new[]{"question.0","question.1","question.2","doorman"}).Concat(DialogueGrammar.Roles.SelectMany(r=>new[]{0,1,2}.Select(i=>"answer."+r+"."+i)));
        Check(chat.All(FestivalLines.Kinds.Contains)&&FestivalLines.Kinds.Length==15+chat.Count(),"the grammar has every chat kind and nothing else: "+string.Join(", ",FestivalLines.Kinds));
        // Counted by body: the same sentence behind another "Hey!" or "Anyway." is not a new line.
        var few=new List<string>();
        foreach(var kind in FestivalLines.Kinds) for(int festival=0;festival<Festivals.Count;festival++)
        {
            int distinct=new HashSet<string>(Bodies(kind,festival)).Count;
            if(distinct<1000) few.Add(kind+" on festival "+festival+": "+distinct);
        }
        Check(few.Count==0,"every kind gives at least 1,000 different bodies in "+Draws+" draws on each festival, but:\n  "+string.Join("\n  ",few));
    }

    static void EveryLineReadsCleanly()
    {
        EveryLine("every line starts with a capital",(kind,festival,line)=>line.TrimStart('"','\'','(').Length>0&&char.IsUpper(line.TrimStart('"','\'','(')[0]));
        EveryLine("every line ends with . ! or ?",(kind,festival,line)=>".!?".IndexOf(line.TrimEnd('"','\'',')').LastOrDefault())>=0);
        EveryLine("every line has single spaces and no unfilled {slot}",(kind,festival,line)=>!line.Contains("  ")&&line.Trim()==line&&line.IndexOf('{')<0&&line.IndexOf('}')<0&&line.IndexOf('|')<0&&!line.Contains(" ,")&&!line.Contains(" ."));
        EveryLine("no 'a' before a vowel sound and no 'an' before a consonant",(kind,festival,line)=>BadArticle(line)==null);
        EveryLine("every line fits a bubble: "+Longest+" characters at most",(kind,festival,line)=>line.Length<=Longest);
    }
    // The article in front of a word that sounds otherwise, if any. A word spelt with a vowel can start with a "y" or "w" sound
    // (unicorn, one); one spelt with a consonant can start with a vowel sound (hour, an MC).
    static readonly string[] ConsonantSound={"one","once","uni","use","usu","eu","ufo","ukulele"},VowelSound={"hour","honest","honor","heir","mc","led","mp3","npc","rv","sos","suv","x-ray","8"};
    static string BadArticle(string line)
    {
        var words=line.Split(' ');
        for(int i=0;i+1<words.Length;i++)
        {
            string article=words[i].TrimStart('"','\'','(').ToLowerInvariant(),next=words[i+1].TrimStart('"','\'','(').ToLowerInvariant();
            if(article!="a"&&article!="an"||next.Length==0)continue;
            bool vowel="aeiou".IndexOf(next[0])>=0;
            if(ConsonantSound.Any(next.StartsWith))vowel=false;
            if(VowelSound.Any(next.StartsWith))vowel=true;
            if((article=="an")!=vowel)return words[i]+" "+words[i+1];
        }
        return null;
    }

    static void EachFestivalKeepsToItsOwnPlaces()
    {
        EveryLine("each festival names only its own places",(kind,festival,line)=>!Places[1-festival].Any(line.Contains));
        foreach(int festival in new[]{0,1})
        {
            string all=string.Join("\n",FestivalLines.Kinds.SelectMany(kind=>Lines(kind,festival)));
            foreach(var place in Places[festival].Take(3)) Check(all.Contains(place),Festivals.Name(festival)+"'s lines mention its own "+place);
        }
    }

    // Cop-speak is how a narc gives themselves away (DialogueGrammar.NarcTells): every line of a narc's answers carries some, and
    // nothing else does, least of all the player's own lines (their chat questions and awkward replies).
    static void OnlyANarcsAnswersCarryATell()=>EveryLine("a narc's answers always carry a narc tell and no other line ever does",(kind,festival,line)=>DialogueGrammar.HasNarcTell(line)==kind.StartsWith("answer.Narc.",StringComparison.Ordinal));

    static void TheSevenChallengeContextsAreBuiltFromTheGrammar()
    {
        Check(string.Join(",",DialogueCatalog.Contexts)=="greeting,dance,suspicion,sale,buy,police,medical","the seven challenge contexts, ECON-2's buy among them: "+string.Join(",",DialogueCatalog.Contexts));
        foreach(var context in DialogueCatalog.Contexts) for(int festival=0;festival<Festivals.Count;festival++) for(int seed=0;seed<50;seed++)
        {
            var line=new DialogueHistory().Select(context,festival,seed);var random=new ContentRandom(seed);
            string where=context+" on festival "+festival+", seed "+seed;
            // With nothing heard yet, the seed's first line stands, and its awkward twin is the next line off the same stream.
            string text=FestivalLines.Grammar.Line(context,festival,random,out var body),awkward=FestivalLines.Grammar.Line(context+"_awkward",festival,random,out var awkwardBody);
            Check(line.Text==text&&line.AwkwardText==awkward,where+": the line and its awkward twin come from the grammar, got "+line.Text+" / "+line.AwkwardText);
            // Each is known by its kind and its body, the words without their hello or tail.
            Check(line.Id==DialogueHistory.Key(context,body)&&line.AwkwardId==DialogueHistory.Key(context+"_awkward",awkwardBody)&&line.Id.StartsWith(context+"_")&&line.Id.Length<=64&&line.AwkwardId.Length<=64,where+": the line and its twin are known by their kind and body, got "+line.Id+" / "+line.AwkwardId);
            Check(line.Context==context&&line.Status=="Draft"&&line.Speaker==(context=="police"?"Cop":context=="medical"?"Medic":"Wook")&&line.Difficulty==(context=="suspicion"||context=="police"?2:1),where+": the context's speaker and difficulty");
        }
        Check(DialogueHistory.Key("sale","Deal?")==DialogueHistory.Key("sale","Deal?")&&DialogueHistory.Key("sale","Deal?")!=DialogueHistory.Key("sale","Deal!")&&DialogueHistory.Key("sale","Deal?")!=DialogueHistory.Key("buy","Deal?"),"a line's key is its kind and its exact words");
        Check(new DialogueHistory().Select("karaoke",0,1)==null,"an unknown context has no line");
    }

    // Matt: "it kept repeating". 200 challenges in a row, each line shown and acknowledged as the HUD does, on the seeds a level
    // gives (each interaction's ChartSeed is the next one up). Not even the same sentence behind another hello or tail.
    static void AChallengeLineIsNotRepeatedWithinThirty()
    {
        foreach(var context in DialogueCatalog.Contexts) for(int festival=0;festival<Festivals.Count;festival++)
        {
            var history=new DialogueHistory();var shown=new List<string>();
            for(int i=0;i<200;i++)
            {
                var line=history.Select(context,festival,4100+i);string body=Body(context,festival,4100+i,line.Text);
                int last=shown.LastIndexOf(body);
                Check(last<0||shown.Count-last>=30,context+" on festival "+festival+": \""+line.Text+"\" says \""+body+"\" again, "+(shown.Count-last)+" lines after it was shown");
                shown.Add(body);history.Acknowledge(line.Id,"interaction_"+i);
            }
        }
    }
    // The body of a line Select drew from `seed`: whichever of the stream's draws it is. An awkward twin is drawn off the same
    // stream after the line's own one to six draws.
    static string Body(string kind,int festival,int seed,string text)
    {
        bool twin=kind.EndsWith("_awkward",StringComparison.Ordinal);
        for(int before=twin?1:0;before<=(twin?DialogueHistory.Redraws+1:0);before++)
        {
            var random=new ContentRandom(seed);
            for(int i=0;i<before;i++)FestivalLines.Grammar.Line(kind.Substring(0,kind.Length-8),festival,random,out _);
            for(int draw=0;draw<=DialogueHistory.Redraws;draw++) if(FestivalLines.Grammar.Line(kind,festival,random,out var body)==text) return body;
        }
        throw new Exception("FestivalLines: setup: \""+text+"\" is none of seed "+seed+"'s "+kind+" draws");
    }

    // The memory goes by the body. A sentence heard behind one hello is drawn again when it comes up behind another.
    static void ALineHeardBehindOneHelloIsNotNewBehindAnother()
    {
        var firstBody=new Dictionary<string,int>();int pairs=0;
        for(int seed=0;seed<2000&&pairs<20;seed++)
        {
            string text=FestivalLines.Grammar.Line("sale",0,new ContentRandom(seed),out var body);
            if(!firstBody.TryGetValue(body,out int earlier)){firstBody[body]=seed;continue;}
            var heardLine=new DialogueHistory().Select("sale",0,earlier);
            if(heardLine.Text==text)continue;
            pairs++;
            var history=new DialogueHistory();history.Acknowledge(heardLine.Id,"interaction_0");
            string again=history.Select("sale",0,seed).Text;
            Check(again!=text,"heard \""+heardLine.Text+"\", then seed "+seed+" still says \""+text+"\": the same sentence behind another hello or tail");
        }
        Check(pairs==20,"setup: 20 sale draws whose sentence an earlier seed says behind other words, got "+pairs);
    }

    // An affected player blurts the awkward twin instead (the tripper all level, everyone on the last level). What's shown is what's
    // remembered: the dance acknowledges the shown line's own key, so its sentence doesn't come back within 30, not even behind
    // another hello or tail. Driven as the HUD does it: start, acknowledge, walk away. And the level's festival picks the words,
    // so Ember Playa's dances never mention the Ferris wheel.
    static void AnAffectedPlayersAwkwardLineIsNotRepeatedWithinThirty()
    {
        foreach(bool affected in new[]{false,true}) for(int festival=0;festival<Festivals.Count;festival++)
        {
            var s=new FestivalSimulation(11);var p=s.AddPlayer("a","A");s.State.Phase="Playing";s.State.FestivalIndex=festival;
            p.X=0;p.Z=0;s.State.Npcs.Clear();s.State.Npcs.Add(new NpcState{Id="wook",X=0,Z=1,CanTalk=true});
            if(affected)p.Effects.Add(new ActiveEffect{Id="dose",Intensity=1,RemainingSeconds=1e9});
            string who=(affected?"an affected":"a sober")+" player on "+Festivals.Name(festival);var shown=new List<string>();
            for(int i=0;i<200;i++)
            {
                var start=s.Execute(p.Id,new GameCommand{Id="dance"+i,Kind="Dance",TargetId="wook"});
                Check(start.Accepted,who+", dance "+i+": "+start.Reason);
                var dance=s.Interaction(p.InteractionId);string text=dance.DialogueText,kind=affected?"dance_awkward":"dance",body=Body(kind,festival,dance.ChartSeed,text);
                Check(dance.DialogueId==DialogueHistory.Key(kind,body),who+", dance "+i+": shows \""+text+"\" but acknowledges "+dance.DialogueId+", not its key "+DialogueHistory.Key(kind,body));
                int last=shown.LastIndexOf(body);
                Check(last<0||shown.Count-last>=30,who+": \""+text+"\" says \""+body+"\" again, "+(shown.Count-last)+" dances after it was shown");
                Check(!Places[1-festival].Any(text.Contains),who+" names only its own places: "+text);
                shown.Add(body);
                Check(s.Execute(p.Id,new GameCommand{Id="ack"+i,Kind="DialogueAck",TargetId=dance.DialogueId}).Accepted&&s.Execute(p.Id,new GameCommand{Id="stop"+i,Kind="Cancel"}).Accepted,who+", dance "+i+": acknowledged and walked away");
                s.Tick(.1);
            }
        }
    }

    // A line already heard is drawn again from the same stream, five times at most; then the last draw stands, heard or not.
    static void ARedrawGivesUpAfterFive()
    {
        Check(DialogueHistory.Redraws==5,"up to five redraws");
        for(int seed=0;seed<20;seed++)
        {
            var stream=new ContentRandom(seed);var texts=new List<string>();var bodies=new List<string>();
            for(int i=0;i<8;i++){texts.Add(FestivalLines.Grammar.Line("sale",0,stream,out var body));bodies.Add(body);}
            if(bodies.Distinct().Count()<8)continue;
            foreach(int heard in new[]{1,2,5,6,7})
            {
                var history=new DialogueHistory();
                foreach(var body in bodies.Take(heard))history.Acknowledge(DialogueHistory.Key("sale",body));
                string got=history.Select("sale",0,seed).Text;
                Check(got==texts[Math.Min(heard,5)],"seed "+seed+", the first "+heard+" draws heard: the line is draw "+Math.Min(heard,5)+" off the stream, got draw "+texts.IndexOf(got));
            }
            var reserved=new HashSet<string>{DialogueHistory.Key("sale",bodies[0])};
            Check(new DialogueHistory().Select("sale",0,seed,reserved).Text==texts[1],"seed "+seed+": a line another player's challenge holds right now is drawn again too");
            return;
        }
        Check(false,"setup: no seed under 20 draws eight different sale lines");
    }

    // Talk (F beside a festivalgoer who wants to chat) shows a bark. Seeded by the round, the festivalgoer and how often they've
    // talked, so a replay hears the same lines; and the player's history keeps them from coming back soon.
    static void AFestivalgoersChatIsNotRepeatedWithinThirty()
    {
        foreach(int festival in new[]{0,1})
        {
            var heard=Chat(77,festival,200);
            for(int i=0;i<heard.Count;i++)
            {
                int last=i==0?-1:heard.LastIndexOf(heard[i],i-1);
                Check(last<0||i-last>=30,Festivals.Name(festival)+": \""+heard[i]+"\" came back "+(i-last)+" chats after it was said");
                Check(!Places[1-festival].Any(heard[i].Contains),Festivals.Name(festival)+" chat names only its own places: "+heard[i]);
            }
            Check(heard.SequenceEqual(Chat(77,festival,200)),Festivals.Name(festival)+": the same round replays the same chat");
            Check(new HashSet<string>(heard).Count>=190,Festivals.Name(festival)+": 200 chats say nearly 200 different things, got "+new HashSet<string>(heard).Count);
        }
    }
    // One player chats `count` times with three festivalgoers in turn, on a round of `seed` at `festival`. What they heard.
    static List<string> Chat(int seed,int festival,int count)
    {
        var s=new FestivalSimulation(seed);var p=s.AddPlayer("a","A");s.State.Phase="Playing";s.State.FestivalIndex=festival;
        p.X=0;p.Z=0;s.State.Npcs.Clear();
        for(int i=0;i<3;i++)s.State.Npcs.Add(new NpcState{Id="wook_"+i,X=i-1,Z=1,CanTalk=true});
        var heard=new List<string>();
        for(int i=0;i<count;i++)
        {
            var npc=s.State.Npcs[i%3];npc.LastTalkSeconds=s.State.SimulationSeconds-5;
            var talk=s.Execute(p.Id,new GameCommand{Id="talk"+i,Kind="Talk",TargetId=npc.Id});
            Check(talk.Accepted&&p.NpcSpeaker=="FESTIVALGOER",Festivals.Name(festival)+" chat "+i+": "+talk.Reason);
            heard.Add(p.NpcSpeech);
        }
        return heard;
    }
}
