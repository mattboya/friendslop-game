using System;
using System.Collections.Generic;
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
}
