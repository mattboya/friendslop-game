using System;
using System.Collections.Generic;

namespace Festival.Core
{
    // DIALOG-3: one turn of a talk (Conversations.cs): their Reaction to your last reply ("" on the first turn) and their Line,
    // then your three Replies. Grades holds each reply's grade (ConversationLines.Smooth, SoSo or Awkward); a Garbled reply's
    // words and letters are jumbled, as a dosed player says it. Pick is the reply chosen: -1 until one is, and for good once the
    // turn has run out, a silence.
    [Serializable] public sealed class ConversationTurn { public string Reaction="",Line=""; public string[] Replies=new string[3]; public int[] Grades=new int[3]; public bool[] Garbled=new bool[3]; public int Pick=-1; }

    // DIALOG-3: what's said in a talk, built by a LineGrammar (DIALOG-4) that holds FestivalLines' parts as well as these.
    // - Topics: a chat (Conversation) is "talk", a sale "sale", a buy "buy" and a talk with security "security".
    // - Their first line is their persona's opener (FestivalLines' "opener.<persona>"), so it hints at who they are; security
    //   opens on one of its lines ("police"). Each later turn opens on their reaction to your reply, in their persona's voice
    //   (a silence gets its own), then the topic's line: a festivalgoer's small talk (a regular's chat-check answer), a buyer's
    //   or a seller's line ("sale", "buy"), or security's.
    // - Your replies come in five Styles. Each persona likes one (the smooth reply) and can't stand one (the awkward reply); the
    //   so-so reply is one of the other three. Security likes it plain and can't stand the cosmic.
    // - Only fictional goods are named, no reply carries a narc's cop-speak (DialogueGrammar.NarcTells), and every line stays
    //   within 80 characters for DIALOG-2's bubbles.
    public static class ConversationLines
    {
        public const int Smooth=0,SoSo=1,Awkward=2,Silence=3;
        public const string Security="Security";
        public static readonly string[] Topics={"talk","sale","buy","security"};
        public static readonly string[] Styles={"cosmic","hype","earnest","plur","plain"};
        // Each persona's liked and hated style, as indexes into Styles.
        private static readonly Dictionary<string,(int liked,int hated)> Tastes=new Dictionary<string,(int liked,int hated)>{
            ["Wook"]=(0,1),["Influencer"]=(1,2),["Burner"]=(2,1),["Raver"]=(3,4),["DraggedAlongDad"]=(4,3),[Security]=(4,0)};
        public static string Topic(string kind)=>kind=="Sale"?"sale":kind=="Buy"?"buy":kind=="Police"?"security":"talk";

        // A turn of a talk on `topic` with `persona` on `festival`: turn 0 opens it; a later one follows a reply of grade `heard`
        // (Silence for none). `garbled` of the replies come out jumbled. Drawn from `random` in order (their reaction, their
        // line, the so-so style, the replies' order, the replies, the garbling), and given `history` each line heard lately is
        // drawn again and each line drawn is remembered (DialogueHistory.Fresh), as DialogueGrammar's chats are.
        public static ConversationTurn Turn(string topic,string persona,int festival,int turn,int heard,int garbled,ContentRandom random,DialogueHistory history=null)
        {
            if(Array.IndexOf(Topics,topic)<0||!Tastes.TryGetValue(persona,out var taste)) throw new ArgumentException("Unknown talk topic or persona: "+topic+", "+persona);
            var said=new ConversationTurn();
            if(turn>0) said.Reaction=Grammar.Line(heard==Silence?"react.silence":"react."+persona+"."+heard,festival,random);
            said.Line=Say(turn>0?Body(topic):persona==Security?"police":"opener."+persona,festival,random,history);
            int skip=random.Next(3),soso=-1;
            for(int style=0;soso<0;style++) if(style!=taste.liked&&style!=taste.hated&&skip--==0) soso=style;
            var styles=new[]{taste.liked,soso,taste.hated};var grades=new[]{Smooth,SoSo,Awkward};Shuffle(grades,random);
            for(int r=0;r<3;r++) { said.Grades[r]=grades[r]; said.Replies[r]=Say("reply."+topic+"."+Styles[styles[grades[r]]],festival,random,history); }
            if(garbled>0)
            {
                var spots=new[]{0,1,2};Shuffle(spots,random);
                for(int g=0;g<Math.Min(garbled,3);g++) { said.Garbled[spots[g]]=true; said.Replies[spots[g]]=Garble(said.Replies[spots[g]],random); }
            }
            return said;
        }
        // What a festivalgoer or security says after the first turn: small talk, a buyer's or a seller's line, or security's.
        private static string Body(string topic)=>topic=="sale"?"sale":topic=="buy"?"buy":topic=="security"?"police":"answer.Regular.0";
        private static string Say(string kind,int festival,ContentRandom random,DialogueHistory history)
        {
            if(history==null) return Grammar.Line(kind,festival,random);
            string line=history.Fresh(Grammar,kind,festival,random,out var key); history.Acknowledge(key); return line;
        }
        // A dosed player's reply: its words in a new order and each word's letters jumbled, so its tone can't be read.
        public static string Garble(string text,ContentRandom random)
        {
            var words=text.Split(' ');Shuffle(words,random);
            for(int w=0;w<words.Length;w++) { var letters=words[w].ToCharArray(); Shuffle(letters,random); words[w]=new string(letters); }
            return string.Join(" ",words);
        }
        private static void Shuffle<T>(T[] items,ContentRandom random) { for(int k=items.Length-1;k>0;k--) { int j=random.Next(k+1); var swap=items[k]; items[k]=items[j]; items[j]=swap; } }

        private static readonly Dictionary<string,string[]> Parts=new Dictionary<string,string[]>{
            // Their reaction to your reply: "react.<persona>.<grade>", and to a silence.
            ["react.Wook.0"]=new[]{"Duuude, yes.","Oh, that's beautiful.","Far out, friend.","You get it. You really get it.","Mmm. That landed."},
            ["react.Wook.1"]=new[]{"Mm, sure.","Okay, okay.","Heh. Alright.","Right on, I guess.","Cool, cool."},
            ["react.Wook.2"]=new[]{"Whoa. Harsh vibe.","That felt crunchy.","Hm. My aura flinched.","Yikes, man.","Brother, no."},
            ["react.Influencer.0"]=new[]{"Obsessed.","Okay, you get it.","That's so content.","Iconic answer.","Wait, say that on camera."},
            ["react.Influencer.1"]=new[]{"Cute, I guess.","Sure, babe.","Mm, fine.","Okay. Noted.","That's fine. Ish."},
            ["react.Influencer.2"]=new[]{"That's not the vibe.","Ew, cringe.","Not for the feed.","Babe, no.","I'm muting that."},
            ["react.Burner.0"]=new[]{"That's beautiful.","Welcome home.","I feel that.","Thank you for that.","Now that's the spirit."},
            ["react.Burner.1"]=new[]{"Okay, sure.","Hm. Fair.","I hear you.","Alright.","That's one way to see it."},
            ["react.Burner.2"]=new[]{"That's very default.","Wow. Very commercial.","Hm. Not the spirit.","That stings a little.","Very sponsored of you."},
            ["react.Raver.0"]=new[]{"YES, fam!","Ahh, I love you!","Same energy!","That's so PLUR!","Bestie!"},
            ["react.Raver.1"]=new[]{"Haha, sure!","Okay, cool!","Totally. I guess!","Ha, yeah!","Fair, fam!"},
            ["react.Raver.2"]=new[]{"Oh. Okay, grandpa.","Wow, buzzkill.","Um. Less talk, more bass?","That's not it, fam.","Yikes. Hug it out?"},
            ["react.DraggedAlongDad.0"]=new[]{"Ha! Good one.","Now that's sensible.","Well said, champ.","Exactly my thinking.","Finally, a normal answer."},
            ["react.DraggedAlongDad.1"]=new[]{"Sure, sure.","If you say so.","Hm. Okay.","Right-o.","I suppose."},
            ["react.DraggedAlongDad.2"]=new[]{"I don't follow, kiddo.","Hoo boy.","That's a lot of words.","Is that a young person thing?","My ears hurt now."},
            ["react.Security.0"]=new[]{"Good. Appreciated.","Alright. That's fair.","Okay. Thank you.","Fine. Noted.","That's what I like to hear."},
            ["react.Security.1"]=new[]{"Mm.","Okay.","Go on.","Sure.","If you say so."},
            ["react.Security.2"]=new[]{"Excuse me?","Don't get smart.","That's not helping.","Try again.","Not the answer I wanted."},
            ["react.silence"]=new[]{"Hello? Anyway.","Cat got your tongue? Okay.","Okay. Moving on.","Not a talker. Fine.","Silent type, huh."},

            // Each style's hello and tail, dressing round the reply.
            ["cosmic_hi"]=new[]{"Far out.","Whoa.","Mmm.","Oh, wow.","Duuude.","Breathe."},
            ["cosmic_tail"]=new[]{"It's all one.","Feel that?","The trees agree.","Namaste.","Magic, man.","Cosmic."},
            ["hype_hi"]=new[]{"Okay, iconic.","Wait, obsessed.","Literally.","Babe.","Stop it.","Love that."},
            ["hype_tail"]=new[]{"Tag me.","Main character energy.","So aesthetic.","Going on my story.","Iconic.","No filter."},
            ["earnest_hi"]=new[]{"Honestly?","Real talk.","Hey, neighbor.","With love.","Radical honesty?","Truly."},
            ["earnest_tail"]=new[]{"No strings.","That's my gift to you.","Leave no trace.","Community, right?","From the heart.","Thank you."},
            ["plur_hi"]=new[]{"PLUR!","Fam!","Bestie!","Ayyy!","Bass face!","Hug first!"},
            ["plur_tail"]=new[]{"PLUR forever.","Bass is life.","Love you, fam.","Kandi for you!","Feel the drop!","Hands up!"},
            ["plain_hi"]=new[]{"Sure thing.","Well.","Okay.","Fair enough.","Good question.","Right."},
            ["plain_tail"]=new[]{"Simple as that.","No fuss.","Thanks for asking.","That's all.","Nice and easy.","Have a good one."},

            // Small talk.
            ["talk.cosmic"]=new[]{"This place has such a {warm} aura today.","I've heard {landmark} breathing {when}.","Your energy feels like {song} at sunrise.",
                "I traded {thing} for a feeling. Worth it.","The bass told me we'd meet. Here you are.","I followed a cloud here from {landmark}.",
                "We're all just stardust in a field, friend.","Time moves slower by {landmark}. Try it."},
            ["talk.hype"]=new[]{"The light on you right now is unreal.","Can we take one by {landmark}? For the story.","Your {wearable} {is|are} giving main character.",
                "I'm live. Say hi to my {count} viewers.","This whole moment is content. Hold that pose.","Okay, {song} is about to trend. Called it.",
                "Rate my fit, honestly. Out of {count}.","Is {stranger} following me? Love that for me."},
            ["talk.earnest"]=new[]{"I made {thing} for a stranger. Want it?","I'm here to connect, not to consume.","Can I hold space for your weekend?",
                "I gave {snack} to a stranger. Felt amazing.","Thank you for being exactly who you are.","No money out here, just stories. Got one?",
                "I've been practicing radical presence {when}.","I hugged {stranger} today. We both cried."},
            ["talk.plur"]=new[]{"That drop earlier changed my whole DNA!","Want a kandi bracelet? I made {count}!","We're family now. That's how it works!",
                "I've been front row at {landmark} {when}!","Your vibe matches my bass face perfectly!","Peace, love, unity, respect, and {snack}!",
                "Hug? It's mandatory. I don't make the rules!","I lost {friend} in the pit. Worth it!"},
            ["talk.plain"]=new[]{"Nice weather for it. Not too hot.","I found the water tent. Very reasonable lines.","Long walk from parking, but worth it.",
                "My feet hurt, but the music's fine.","Have you tried {snack}? Decent value.","I'm waiting on {friend}. Always late.",
                "Sound's a bit loud by {landmark}, if you ask me.","Good crowd today. Very polite."},
            // Selling to a buyer.
            ["sale.cosmic"]=new[]{"The universe wants you to have this. I just carry it.","Hold it like a seed. It knows what to do.","Let's pass it during {song}. The bass will hide us.",
                "This one found me at {landmark}. Now it's yours.","It's not a sale. It's an energy exchange.","Feel the price instead of thinking it."},
            ["sale.hype"]=new[]{"Limited drop. Only {count} in the whole festival.","Trust me, this is about to be everywhere.","Collab price, just for you. Don't tell my followers.",
                "Unboxing this by {landmark} would go viral.","It's giving premium. You're giving buyer.","Get it before {stranger} posts about it."},
            ["sale.earnest"]=new[]{"I'll be honest: it's five, and it's good.","No pressure. If it's not right for you, that's okay.","Fair price, fair trade, and a hug if you want one.",
                "I'd rather you trust me than buy from me.","Small and kind, that's how I like to trade.","Straight answer: one unit, five, no games."},
            ["sale.plur"]=new[]{"For you, fam? Friend price, always!","Pass it like kandi. Peace, love, unity!","We're family now, so it's basically a gift!",
                "Swap it on the drop. Nobody sees a thing!","One for you, and a hug for free!","My bestie from {landmark} loved these!"},
            ["sale.plain"]=new[]{"It's five. Cash is fine.","One unit, as discussed. Nothing fancy.","Quick and simple, then we both get on with our day.",
                "Here you go. Keep it in your pocket.","Fair price. I'm not here to haggle.","Same as last time, if there was a last time."},
            // Buying from a seller.
            ["buy.cosmic"]=new[]{"The moon told me to come find you.","Whatever you carry, I'm open to receiving it.","My aura's been asking for a little treat all day.",
                "Let's trade like two leaves in the wind.","I sense you carry a little joy. Can I have some?","Hand it over slow, like a sunset."},
            ["buy.hype"]=new[]{"I need one for the vlog. Strictly content.","Are you the one everyone's posting about?","Price check? My followers will want to know.",
                "Can I say I got it from you? Great for both of us.","This is going to look so good by {landmark}.","I've seen {count} stories about you. Let's do it."},
            ["buy.earnest"]=new[]{"Honestly, I'd just like one. No haggling.","I'll pay fair and say thank you. That's the deal.","I trust you. I hope you can trust me.",
                "Just one, just for me, just for tonight.","Folks at {landmark} said you're kind.","I can offer cash and genuine gratitude."},
            ["buy.plur"]=new[]{"Fam! Can I get one? I'll throw in kandi!","One for me and one hug for you!","You're the best! Seriously, PLUR price?",
                "I'll pay and give you a hug. Win-win!","My crew said you're family. Hook me up!","Let's do it before the drop! Hurry!"},
            ["buy.plain"]=new[]{"I'd like one, please. Cash is ready.","What's the price? Five? Fine.","Just the one, thanks. Then I'll be on my way.",
                "No need to make a show of it. Quick swap.","Exact change, folded. Saves us both time.","I'll take one if you've got one."},
            // Answering to security.
            ["security.cosmic"]=new[]{"Have you ever really looked at the sky?","I'm not holding anything but good vibes, man.","We're all just energy, you and me both.",
                "I was meditating by {landmark}. Ask the trees.","Your aura looks stressed. Want a hug?","Time isn't real, so I couldn't have been anywhere."},
            ["security.hype"]=new[]{"Do you know who I am? I have {count}K followers.","Can we do this somewhere with better lighting?","This is going on my story. Smile!",
                "I'm literally a brand ambassador here.","Okay, but your uniform is kind of iconic.","My manager can sort this out. Want her number?"},
            ["security.earnest"]=new[]{"I'll be honest with you, I'm just here for the art.","I respect the work you do out here. Truly.","Ask me anything. I've got nothing to hide.",
                "I'm with a camp that cleans up after the crowd.","Let's keep this calm and kind for both of us.","I hear you. I want everyone safe too."},
            ["security.plur"]=new[]{"You're family too! PLUR!","I was just dancing by {landmark}, I swear!","Want a kandi bracelet? It's a friendship thing!",
                "No trouble here! Just bass and hugs!","Wanna see my shuffle? It's really good!","Respect! That's the R in PLUR!"},
            ["security.plain"]=new[]{"Of course. What do you need?","I was just heading to the water tent.","No problem. I'll keep it moving.",
                "I understand. I'm only here for the music.","Happy to help however I can.","I'm with {friend}. We're heading back to camp."},
        };
        // FestivalLines' parts and these, and each reply: its style's body, bare or dressed with the style's hello or tail
        // ("reply.<topic>.<style>").
        private static Dictionary<string,string[]> Tables()
        {
            var all=new Dictionary<string,string[]>(FestivalLines.Parts);
            foreach(var part in Parts) all.Add(part.Key,part.Value);
            foreach(var topic in Topics) foreach(var style in Styles) all.Add("reply."+topic+"."+style,new[]{"{"+topic+"."+style+"}","{~"+style+"_hi} {"+topic+"."+style+"}","{"+topic+"."+style+"} {~"+style+"_tail}"});
            return all;
        }
        public static readonly LineGrammar Grammar=new LineGrammar(Tables());
    }
}
