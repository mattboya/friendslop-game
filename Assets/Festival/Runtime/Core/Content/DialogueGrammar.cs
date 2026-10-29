using System;

namespace Festival.Core
{
    [Serializable] public sealed class Conversation
    {
        public string Opener=""; public string[] Questions=new string[3],Answers=new string[3]; public string Doorman="";
        public Conversation Copy(){var c=(Conversation)MemberwiseClone();c.Questions=(string[])Questions.Clone();c.Answers=(string[])Answers.Clone();return c;}
    }
    // A chat is an opener (persona), three questions the tripper asks (small talk, the sales pitch, the lost friend)
    // and one answer to each: a persona interjection plus a line for the NPC's role. A rope chat with the VIP guard (Rope)
    // is the opener and a Doorman line instead, with no questions and no answers. DIALOG-4: every line but the interjections
    // is built by FestivalLines' grammar ("opener.<persona>", "question.<n>", "answer.<role>.<n>", "doorman"), so a chat
    // almost never says the same thing twice.
    public static class DialogueGrammar
    {
        public static readonly string[] Roles={"Buyer","Narc","Regular","ClueHolder"};
        public static readonly string[] Personas={"Wook","Influencer","Burner","Raver","DraggedAlongDad"};
        // Cop-speak only narcs use. Every narc answer carries one; no other line in the grammar may contain any.
        public static readonly string[] NarcTells={"fellow festival-goer","street value","hypothetically speaking","where might one acquire","for my records","in the vicinity","ten-four","copy that","the suspect","the perpetrator","civilian","youths","the kids call","controlled substance","illicit","hundred hours","proceeding on foot","exit the vehicle"};
        public static bool HasNarcTell(string text) { if(string.IsNullOrEmpty(text)) return false; foreach(var tell in NarcTells) if(text.IndexOf(tell,StringComparison.OrdinalIgnoreCase)>=0) return true; return false; }
        // POLO-2: whoever films (FestivalSimulation.DealTwists) talks like an influencer, and nobody else does; the rest of the crowd
        // takes one of the other four by an FNV-1a hash of their id, so host and clients agree (string.GetHashCode differs between processes).
        private static readonly string[] Unfilmed=Array.FindAll(Personas,p=>p!="Influencer");
        public static string PersonaFor(string npcId,string twist) { if(twist==FestivalSimulation.Influencer) return "Influencer"; uint hash=2166136261; foreach(char c in npcId??"") hash=unchecked((hash^c)*16777619); return Unfilmed[hash%(uint)Unfilmed.Length]; }
        // DIALOG-4: given the tripper's history (heard), a line they've heard lately is drawn again (DialogueHistory.Fresh), and
        // each line drawn is remembered as heard; without one, the seed alone decides.
        public static Conversation Build(string role,string persona,int festival,int seed,DialogueHistory heard=null)
        {
            int r=Array.IndexOf(Roles,role),p=Array.IndexOf(Personas,persona);
            if(r<0||p<0||festival<0||festival>=Festivals.Count) throw new ArgumentException("Unknown dialogue role, persona or festival: "+role+", "+persona+", "+festival);
            var random=new ContentRandom(seed); var result=new Conversation { Opener=Line("opener."+persona,festival,random,heard) };
            // Everything before the role's answers is drawn first, so the opener, questions and interjections are the same for every role.
            for(int i=0;i<3;i++) result.Questions[i]=Line("question."+i,festival,random,heard);
            // Two of the three answers open with a persona interjection; three in a row reads like a tic.
            var interjections=Draw(Interjections[p],random,3); int plain=random.Next(3);
            for(int i=0;i<3;i++) result.Answers[i]=(i==plain?"":interjections[i]+" ")+Line("answer."+role+"."+i,festival,random,heard);
            return result;
        }
        // POLO-4: Palm Mirage's VIP guard talking someone past the rope who isn't checking a vision about them. Their persona's
        // opener and one doorman line, and nothing to ask: no role goes in, so nothing in it can hint at one.
        public static Conversation Rope(string persona,int festival,int seed,DialogueHistory heard=null)
        {
            int p=Array.IndexOf(Personas,persona);
            if(p<0||festival<0||festival>=Festivals.Count) throw new ArgumentException("Unknown dialogue persona or festival: "+persona+", "+festival);
            var random=new ContentRandom(seed);
            return new Conversation { Opener=Line("opener."+persona,festival,random,heard), Doorman=Line("doorman",festival,random,heard), Questions=Array.Empty<string>(), Answers=Array.Empty<string>() };
        }
        private static string Line(string kind,int festival,ContentRandom random,DialogueHistory heard)
        {
            if(heard==null) return FestivalLines.Grammar.Line(kind,festival,random);
            string line=heard.Fresh(FestivalLines.Grammar,kind,festival,random,out var key); heard.Acknowledge(key); return line;
        }
        // `count` different entries of the pool, by a partial shuffle.
        private static string[] Draw(string[] pool,ContentRandom random,int count)
        {
            var lines=(string[])pool.Clone();
            for(int i=0;i<count;i++) { int j=i+random.Next(lines.Length-i); var swap=lines[i]; lines[i]=lines[j]; lines[j]=swap; }
            return lines;
        }
        private static readonly string[][] Interjections={
            new[]{"Duuude.","Oh, maaan.","Heyyy.","Whoa. Okay. Okay.","Mmm, right on.","Brother.","Far out.","Sister, listen.","Oh, heck yeah.","Hold on, my hair's talking. Okay.","Sorry, I was listening to a tree.","Totally, totally."},
            new[]{"Okay, so, literally.","Not me getting asked this on camera.","Hold on, let me find my angle.","Babe.","Honestly? Iconic question.","Wait, say that again for the story.","Okay, this is giving mystery.","Obsessed with this energy.","Sorry, I'm live right now.","Chat, you're going to love this.","Wait, is my ring light on?","Love that for us."},
            new[]{"Radical honesty time.","Welcome home.","Let me gift you an answer.","Hold on, my goggles are fogging.","Let me hold space for that.","Radical self-expression incoming.","I say this with immediacy.","Let's decommodify this moment.","Hey, neighbor.","Leave no trace, but okay.","Breathe with me. Okay.","Consent check: can I answer? Thanks."},
            new[]{"PLUR, fam.","Bestie!","Bro, that drop earlier. Anyway.","Hold on, trading kandi. Okay.","Oh my gosh, hi!","Vibes check out.","Fam, fam, fam.","Wait, is this song about us?","Bass face. Sorry. Okay.","I love you. I just met you. Anyway.","Hug first. Okay.","Ugh, this bassline. Sorry."},
            new[]{"Well, sport.","Hoo boy.","Let me put my sunglasses on for this one.","Champ, I'll level with you.","Funny you should ask.","Hang on, my fanny pack is buzzing.","Kiddo.","Is this where I say 'groovy'? Groovy.","Let me check with my daughter. She's gone. Okay.","Back in my day we had one stage and it was a truck.","Now, I'm just here for my kid.","I parked in lot G. Remember that for me."}
        };
    }
}
