using System;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;

public static class DialogueGrammarTests
{
    private static void Check(bool condition,string message) { if(!condition) throw new Exception("DialogueGrammar: "+message); }
    // THEME-1's real-drug words. Matched inside other words too, so "shrooms" or "drugstore" also fail.
    private static readonly string[] Banned={"lsd","acid","mushroom","shroom","ecstasy","molly","mdma","ketamine","weed","marijuana","cannabis","cocaine","drug"};
    // Wave doc, L3 Dialogue: each festival's vocabulary.
    private static readonly string[][] Vocabulary={new[]{"polo field","Ferris wheel","VIP","headliner","iced matcha"},new[]{"playa","art car","dust","the Man","gifting"}};
    // Places that exist on one map only; the other festival must never mention them.
    private static readonly string[][] Places={new[]{"polo field","Ferris wheel","iced matcha"},new[]{"playa","art car","the Man"}};
    private static string[] Lines(Conversation c) { var lines=new List<string>{c.Opener}; lines.AddRange(c.Questions); lines.AddRange(c.Answers); return lines.ToArray(); }
    private static string Text(Conversation c) => string.Join("\n",Lines(c));
    public static void Run()
    {
        Check(string.Join(",",DialogueGrammar.Roles)=="Buyer,Narc,Regular,ClueHolder","the four roles");
        Check(string.Join(",",DialogueGrammar.Personas)=="Wook,Influencer,Burner,Raver,DraggedAlongDad","the five personas");
        for(int festival=0;festival<2;festival++)
        {
            var distinct=new HashSet<string>(); var used=new HashSet<string>();
            foreach(var role in DialogueGrammar.Roles) foreach(var persona in DialogueGrammar.Personas) for(int seed=0;seed<150;seed++)
            {
                var c=DialogueGrammar.Build(role,persona,festival,seed); var lines=Lines(c); string text=Text(c);
                Check(c.Questions.Length==3&&c.Answers.Length==3,"three questions and three answers");
                foreach(var line in lines) Check(!string.IsNullOrWhiteSpace(line)&&line.IndexOf('{')<0&&line.IndexOf('}')<0&&!line.StartsWith("0:")&&!line.StartsWith("1:"),"every line filled in: "+line);
                Check(new HashSet<string>(c.Questions).Count==3,"three different questions");
                Check(text==Text(DialogueGrammar.Build(role,persona,festival,seed)),"same seed, same conversation");
                var buyer=DialogueGrammar.Build("Buyer",persona,festival,seed);
                Check(c.Opener==buyer.Opener&&string.Join("|",c.Questions)==string.Join("|",buyer.Questions),"the opener and questions never give the role away");
                if(role=="Narc") foreach(var answer in c.Answers) Check(DialogueGrammar.HasNarcTell(answer),"narc answer without a tell: "+answer);
                else foreach(var line in lines) Check(!DialogueGrammar.HasNarcTell(line),role+" line with a narc tell: "+line);
                if(role=="ClueHolder") Check(Array.Exists(Vocabulary[festival],word=>c.Answers[2].Contains(word)),"clue holder names a landmark: "+c.Answers[2]);
                string lower=text.ToLowerInvariant();
                foreach(var word in Banned) Check(!lower.Contains(word),"real-drug word '"+word+"' in: "+text);
                foreach(var word in Places[1-festival]) Check(!text.Contains(word),"festival "+festival+" mentions '"+word+"': "+text);
                foreach(var word in Vocabulary[festival]) if(text.Contains(word)) used.Add(word);
                distinct.Add(text);
            }
            Check(distinct.Count>=2000,"festival "+festival+" has "+distinct.Count+" distinct conversations, want at least 2000");
            Check(used.Count==Vocabulary[festival].Length,"festival "+festival+" uses its whole vocabulary, got "+string.Join(", ",used));
        }
        // DIALOG-4: over 5,000 chats, a narc's answers always carry a tell, and no other role's line, nor any of the tripper's
        // own questions, ever does.
        for(int i=0;i<5000;i++)
        {
            string role=DialogueGrammar.Roles[i%4],persona=DialogueGrammar.Personas[i/4%5];int festival=i/20%2;
            var c=DialogueGrammar.Build(role,persona,festival,1000+i);
            foreach(var question in c.Questions) Check(!DialogueGrammar.HasNarcTell(question),"the tripper's question carries a narc tell: "+question);
            Check(!DialogueGrammar.HasNarcTell(c.Opener),role+"'s opener carries a narc tell: "+c.Opener);
            foreach(var answer in c.Answers) Check(DialogueGrammar.HasNarcTell(answer)==(role=="Narc"),role+(role=="Narc"?" answer without a tell: ":" answer with a narc tell: ")+answer);
        }
        ARepeatIsDrawnAgainAndTheDrawOrderHolds();
        Check(DialogueGrammar.HasNarcTell("So what's the STREET VALUE of a hug?")&&!DialogueGrammar.HasNarcTell("Nice hat.")&&!DialogueGrammar.HasNarcTell(null)&&!DialogueGrammar.HasNarcTell(""),"tells match in any case; plain and empty lines have none");
        foreach(var bad in new[]{new object[]{"Cop","Wook",0},new object[]{null,"Wook",0},new object[]{"Buyer","Clown",0},new object[]{"Buyer","Wook",2},new object[]{"Buyer","Wook",-1}})
        { bool refused=false; try { DialogueGrammar.Build((string)bad[0],(string)bad[1],(int)bad[2],1); } catch(ArgumentException) { refused=true; } Check(refused,"unknown role, persona or festival is refused"); }
        // POLO-2: whoever films (FestivalSimulation.Influencer) talks like an influencer; nobody else does, and the rest of a crowd
        // of 40 has all four other personas.
        var crowd=new HashSet<string>();
        for(int i=0;i<40;i++)
        {
            string persona=DialogueGrammar.PersonaFor("wook_"+i,"");
            Check(Array.IndexOf(DialogueGrammar.Personas,persona)>=0&&persona!="Influencer","a known persona, and not filming, so no influencer: "+persona);crowd.Add(persona);
            Check(DialogueGrammar.PersonaFor("wook_"+i,FestivalSimulation.Influencer)=="Influencer","wook_"+i+" films, so talks like an influencer");
            Check(DialogueGrammar.PersonaFor("wook_"+i,FestivalSimulation.VipGuard)==persona,"guarding the VIP rope doesn't change how wook_"+i+" talks");
        }
        Check(crowd.Count==4,"a crowd of 40 has all four other personas, got "+string.Join(", ",crowd));
        // Pinned values: host and clients in different processes must agree, which string.GetHashCode does not promise.
        Check(DialogueGrammar.PersonaFor("wook_0","")=="Raver"&&DialogueGrammar.PersonaFor("wook_1","")=="Burner","persona comes from a fixed hash of the NPC id, got "+DialogueGrammar.PersonaFor("wook_0","")+", "+DialogueGrammar.PersonaFor("wook_1",""));
    }
    // DIALOG-4: the tripper's history. A chat built with it remembers what it said, and a line the tripper heard lately is drawn
    // again; whatever the history, the opener, questions and interjections are still drawn before the role's answers.
    static void ARepeatIsDrawnAgainAndTheDrawOrderHolds()
    {
        for(int seed=0;seed<40;seed++) foreach(var persona in DialogueGrammar.Personas)
        {
            int festival=seed%2;var plain=DialogueGrammar.Build("Narc",persona,festival,seed);
            var heard=new DialogueHistory();var c=DialogueGrammar.Build("Narc",persona,festival,seed,heard);
            Check(Text(c)==Text(plain),"with nothing heard yet, the seed's own chat");
            // In the order drawn, each known by its kind (and its body: the words without a hello or tail, FestivalLinesTests).
            var kinds=new List<string>{"opener."+persona,"question.0","question.1","question.2","answer.Narc.0","answer.Narc.1","answer.Narc.2"};
            Check(heard.Seen.Count==7&&kinds.Select((kind,i)=>heard.Seen[i].StartsWith(kind+"_",StringComparison.Ordinal)).All(x=>x),"the chat remembers its opener, three questions and three answers as heard, got "+string.Join(", ",heard.Seen));
            // Heard them all: the same seed now draws a new opener and new questions, and every role still gets the same ones.
            var again=new Dictionary<string,Conversation>();
            foreach(var role in DialogueGrammar.Roles) again[role]=DialogueGrammar.Build(role,persona,festival,seed,new DialogueHistory{Seen=new List<string>(heard.Seen)});
            var buyer=again["Buyer"];
            Check(buyer.Opener!=c.Opener&&!buyer.Questions.Intersect(c.Questions).Any(),"seed "+seed+": a line heard lately is drawn again, got "+buyer.Opener+" | "+string.Join(" | ",buyer.Questions));
            foreach(var role in DialogueGrammar.Roles) Check(again[role].Opener==buyer.Opener&&again[role].Questions.SequenceEqual(buyer.Questions),"seed "+seed+": a "+role+" redraws the same opener and questions as a buyer");
        }
        // Thirty chats in a row for one tripper never ask the same question or hear the same opener or answer twice.
        var tripper=new DialogueHistory();var said=new List<string>();
        for(int i=0;i<30;i++)
        {
            var c=DialogueGrammar.Build(DialogueGrammar.Roles[i%4],DialogueGrammar.Personas[i%5],0,500+i,tripper);
            said.Add(c.Opener);said.AddRange(c.Questions);said.AddRange(c.Answers);
        }
        var repeats=said.GroupBy(line=>line).Where(g=>g.Count()>1).Select(g=>g.Key).ToList();
        Check(repeats.Count==0,"30 chats in a row repeat: "+string.Join(" | ",repeats));
    }
}
