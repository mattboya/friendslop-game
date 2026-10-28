using System;
using System.Collections.Generic;

namespace Festival.Core
{
    // DIALOG-3: one turn of a talk (Conversations.cs): their Reaction to your last reply ("" on the first turn) and their Line,
    // then your three Replies. Grades holds each reply's grade (ConversationLines.Smooth, SoSo or Awkward); a Garbled reply's
    // longer words' interior letters are jumbled, as a dosed player says it. Pick is the reply chosen: -1 until one is,
    // and for good once the turn has run out, a silence.
    [Serializable] public sealed class ConversationTurn { public string Reaction="",Line=""; public string[] Replies=new string[3]; public int[] Grades=new int[3]; public bool[] Garbled=new bool[3]; public int Pick=-1; public List<string> GrammarKeys=new List<string>(); }

    // DIALOG-3: what's said in a talk, built by a LineGrammar (DIALOG-4) that holds FestivalLines' parts as well as these.
    // - Topics: a chat (Conversation) is "talk", a sale "sale", a buy "buy" and a talk with security "security".
    // - A talk's turns are its Rounds, each with its own replies (ReplyKind) and its own line for them (Body).
    // - Their first line is their persona's opener (FestivalLines' "opener.<persona>"), so it hints at who they are; security
    //   opens on one of its lines ("police"). Each later turn opens on their reaction to your reply, in their persona's voice
    //   (a silence gets its own), then their line for the round: a festivalgoer's small talk (a regular's chat-check answer), a
    //   buyer's or a seller's line ("sale", "buy") at the ask, security's, or the round's own ("line.<topic>.<round>"). In a chat
    //   or a buy, a reply that lands draws out a clear line with the person's tell (Body), and the first one shown sets the Hunch.
    // - Your replies come in five Styles. Each persona likes one (the smooth reply) and can't stand one (the awkward reply); the
    //   so-so reply is one of the other three. Security likes it plain and can't stand the cosmic.
    // - Only fictional goods are named, only a narc's lines carry a narc's cop-speak (DialogueGrammar.NarcTells), and every line
    //   stays within 80 characters for DIALOG-2's bubbles.
    public static class ConversationLines
    {
        public const int Smooth=0,SoSo=1,Awkward=2,Silence=3;
        public const string Security="Security";
        public static readonly string[] Topics={"talk","sale","buy","security"};
        public static readonly string[] Styles={"cosmic","hype","earnest","plur","plain"};
        // A talk's rounds, in order: the greeting, small talk, the ask (the pitch in a sale, the ask in a buy, the real question
        // in a chat, your story for security) and the close (sealing it, or a goodbye).
        public static readonly string[] Rounds={"greet","small","ask","close"};
        public static string ReplyKind(string topic,int round,string style)=>"reply."+topic+"."+Rounds[round]+"."+style;
        // Each persona's liked and hated style, as indexes into Styles.
        private static readonly Dictionary<string,(int liked,int hated)> Tastes=new Dictionary<string,(int liked,int hated)>{
            ["Wook"]=(0,1),["Influencer"]=(1,2),["Burner"]=(2,1),["Raver"]=(3,4),["DraggedAlongDad"]=(4,3),[Security]=(4,0)};
        public static string Topic(string kind)=>kind=="Sale"?"sale":kind=="Buy"?"buy":kind=="Police"?"security":"talk";

        // A turn of a talk on `topic` with `persona` on `festival`, in the round of the same number: turn 0 opens it; a later one
        // follows a reply of grade `heard` (Silence for none), and is `clear` when the reply drew their `role`'s tell out.
        // `garbled` of the replies come out jumbled. Drawn from `random` in order (their reaction, their
        // line, the so-so style, the replies' order, the replies, the garbling), and given `history` each line heard lately is
        // drawn again and each line drawn is remembered (DialogueHistory.Fresh), as DialogueGrammar's chats are.
        // What a chat or a buy leaves you feeling (Conversations.cs): a hunch, never a verdict.
        public const string HunchCop="You get the feeling they might be a cop.",HunchBuyer="You get the feeling they'd buy if you offered.",HunchMusic="You get the feeling they're just here for the music.",HunchSeller="You get the feeling they've got something to sell.";
        public static ConversationTurn Turn(string topic,string persona,string role,int festival,int turn,int heard,bool clear,int garbled,ContentRandom random,DialogueHistory history=null)
        {
            if(Array.IndexOf(Topics,topic)<0||!Tastes.TryGetValue(persona,out var taste)) throw new ArgumentException("Unknown talk topic or persona: "+topic+", "+persona);
            var said=new ConversationTurn();
            if(turn>0) said.Reaction=Say(heard==Silence?"react.silence":"react."+persona+"."+heard,festival,random,history,said.GrammarKeys);
            int round=Math.Min(turn,Rounds.Length-1);
            said.Line=Say(turn>0?Body(topic,role,clear,Rounds[round]):persona==Security?"police":"opener."+persona,festival,random,history,said.GrammarKeys);
            int skip=random.Next(3),soso=-1;
            for(int style=0;soso<0;style++) if(style!=taste.liked&&style!=taste.hated&&skip--==0) soso=style;
            var styles=new[]{taste.liked,soso,taste.hated};var grades=new[]{Smooth,SoSo,Awkward};Shuffle(grades,random);
            for(int r=0;r<3;r++) { said.Grades[r]=grades[r]; said.Replies[r]=Reply(ReplyKind(topic,round,Styles[styles[grades[r]]]),festival,random,history,said.GrammarKeys); }
            if(garbled>0)
            {
                var spots=new[]{0,1,2};Shuffle(spots,random);
                for(int g=0;g<Math.Min(garbled,3);g++) { said.Garbled[spots[g]]=true; said.Replies[spots[g]]=Garble(said.Replies[spots[g]],random); }
            }
            return said;
        }
        // What a festivalgoer or security says after the first turn, in `round`: small talk, then the ask (a buyer's or a seller's
        // line, or a chat's real question), then the close; security questions you until it wraps up. A clear line in a chat or a
        // buy carries the person's tell in any round, as the chat check's answers do: a narc's cop-speak (their small talk, or a
        // seller's line round a slip), a buyer's wish to buy, a clue holder's landmark; anyone else's is plain.
        private static string Body(string topic,string role,bool clear,string round)
        {
            if(topic=="sale") return round=="ask"?"sale":"line.sale."+round;
            if(topic=="security") return round=="close"?"line.security.close":"police";
            if(clear&&role=="ClueHolder") return "talk.clue";
            if(topic=="buy") return clear&&role=="Narc"?"buy.narc":round=="ask"?"buy":"line.buy."+round;
            if(clear&&(role=="Narc"||role=="Buyer")) return "answer."+role+".0";
            return round=="small"?"answer.Regular.0":"line.talk."+round;
        }
        // The hunch a chat or a buy leaves, from its first clear line (`told`, null if none came through): their role's tell, or
        // nothing to go on.
        public static string Hunch(string role,string told,int festival,bool seller=false)
        {
            if(told==null) return HunchMusic;
            if(role=="Narc") return HunchCop;
            if(role=="Buyer") return HunchBuyer;
            return seller?HunchSeller:HunchMusic;
        }
        /// <summary>Their word on the trust dance, as the talk starts: `how` 0 under FestivalSimulation.TrustScore, 1 between, 2 at
        /// FestivalSimulation.NailedScore or better.</summary>
        public static string DanceReaction(int how,int festival,ContentRandom random,DialogueHistory history,out string key)
        {
            if(how<0||how>2)throw new ArgumentException("Unknown dance verdict "+how);
            var keys=new List<string>();string line=Say("react.dance."+how,festival,random,history,keys);key=keys[0];return line;
        }
        public static string Reaction(string persona,int grade,int festival,ContentRandom random,DialogueHistory history,out string key)
        {
            if(!Tastes.ContainsKey(persona)||grade<Smooth||grade>Silence)throw new ArgumentException("Unknown reaction persona or grade");
            string kind=grade==Silence?"react.silence":"react."+persona+"."+grade;
            var keys=new List<string>();string line=Say(kind,festival,random,history,keys);key=keys[0];return line;
        }
        // Many bodies already open with their style's hello or close on its tail, and "Bestie! Bestie, you came!" reads as a slip.
        private static string Reply(string kind,int festival,ContentRandom random,DialogueHistory history,ICollection<string> keys)
        {
            history=history??new DialogueHistory();
            string line=history.Fresh(Grammar,kind,festival,random,out var key,out var body);history.Acknowledge(key);keys?.Add(key);
            if(line==body) return line;
            var words=Words(body);var dressing=Words(line);foreach(var word in words) dressing.Remove(word);
            return dressing.Exists(word=>word.Length>=3&&Array.IndexOf(SmallWords,word)<0&&words.Contains(word))?body:line;
        }
        private static readonly string[] SmallWords={"the","and","you","for","are","but","not","all","was","can","got","get","now","one","out","how","who","its","his","her","our","too"};
        private static List<string> Words(string text)
        {
            var words=new List<string>();int start=-1;
            for(int at=0;at<=text.Length;at++)
            {
                bool letter=at<text.Length&&char.IsLetter(text[at]);
                if(letter&&start<0) start=at; else if(!letter&&start>=0) { words.Add(text.Substring(start,at-start).ToLowerInvariant()); start=-1; }
            }
            return words;
        }
        private static string Say(string kind,int festival,ContentRandom random,DialogueHistory history,ICollection<string> keys=null)
        {
            history=history??new DialogueHistory();
            string line=history.Fresh(Grammar,kind,festival,random,out var key);history.Acknowledge(key);keys?.Add(key);return line;
        }
        // DIALOG-8: a dosed reply stays readable. Keep word order, whitespace, punctuation, short alphabetic runs and every
        // run's first/last letter; only shuffle the interior of runs with five or more letters, on the same seeded stream.
        public static string Garble(string text,ContentRandom random)
        {
            var letters=text.ToCharArray();
            for(int at=0;at<letters.Length;)
            {
                if(!char.IsLetter(letters[at])){at++;continue;}
                int start=at;while(at<letters.Length&&char.IsLetter(letters[at]))at++;
                if(at-start<5)continue;
                for(int k=at-2;k>start+1;k--){int j=start+1+random.Next(k-start);var swap=letters[k];letters[k]=letters[j];letters[j]=swap;}
            }
            return new string(letters);
        }
        private static void Shuffle<T>(T[] items,ContentRandom random) { for(int k=items.Length-1;k>0;k--) { int j=random.Next(k+1); var swap=items[k]; items[k]=items[j]; items[j]=swap; } }

        internal static readonly Dictionary<string,string[]> Parts=new Dictionary<string,string[]>{
            // Their reaction to your reply ("react.<persona>.<grade>", Tables): a lead and a comment, both what the line is about, so
            // each persona's reaction to a grade comes in a hundred-odd bodies; some leads name you as that persona would ("pal.").
            // A silence gets one of its own, the same from anyone.
            ["pal.Wook"]=new[]{"friend","brother","sister","star child","my dude","fellow traveler","cosmic cousin","sunshine"},
            ["pal.Influencer"]=new[]{"babe","bestie","hun","queen","icon","love","gorgeous","angel"},
            ["pal.Burner"]=new[]{"neighbor","friend","camp mate","dusty one","fellow burner","stranger","traveler","kin"},
            ["pal.Raver"]=new[]{"fam","bestie","kandi kid","love","friend","party person","bass buddy","rave fam"},
            ["pal.DraggedAlongDad"]=new[]{"champ","kiddo","sport","chief","pal","buddy","young fella","partner"},
            ["pal.Security"]=new[]{"pal","buddy","friend","chief","boss","sir","mate","champ"},
            ["react_lead.Wook.0"]=new[]{"Duuude, yes.","Oh, that's beautiful, {pal.Wook}.","Far out, {pal.Wook}.","Mmm.","Whoa. Yes."},
            ["react_more.Wook.0"]=new[]{"You get it. You really get it.","That landed.","I felt that in my spine.","The trees heard that.","My aura's purring.","That's the frequency.","Pure vibes.","Say it again, slower."},
            ["react_lead.Wook.1"]=new[]{"Mm, sure.","Okay, okay.","Heh. Alright, {pal.Wook}.","Right on, I guess.","Cool, cool, {pal.Wook}."},
            ["react_more.Wook.1"]=new[]{"Half the vibe's there.","I'm floating on it.","Close to the frequency.","My chakras are undecided.","The bass shrugged.","We'll see where it drifts.","Not quite cosmic.","I'll let it simmer."},
            ["react_lead.Wook.2"]=new[]{"Whoa. Harsh vibe.","Yikes, {pal.Wook}.","Brother, no.","Hm.","Easy there, {pal.Wook}."},
            ["react_more.Wook.2"]=new[]{"That felt crunchy.","My aura flinched.","Too much hustle, man.","That's very loud.","The trees didn't like that.","Let's breathe that one out.","Bad energy, friend.","My crystals went cold."},
            ["react_lead.Influencer.0"]=new[]{"Obsessed.","Okay, {pal.Influencer}, you get it.","Iconic answer.","Wait, {pal.Influencer}.","Stop it."},
            ["react_more.Influencer.0"]=new[]{"That's so content.","Say that on camera.","I'm clipping that.","That's going in my caption.","The comments will love you.","Main character answer.","My followers felt that.","Pinning that one."},
            ["react_lead.Influencer.1"]=new[]{"Cute, I guess.","Sure, {pal.Influencer}.","Mm, fine.","Okay. Noted.","Hm, {pal.Influencer}."},
            ["react_more.Influencer.1"]=new[]{"That's fine. Ish.","Not quite on brand.","It's giving okay.","Maybe for my close friends story.","Like, a three out of five.","I'd need a better filter for that.","I'd post it. Maybe.","Not my best angle."},
            ["react_lead.Influencer.2"]=new[]{"Ew.","{pal.Influencer}, no.","That's not the vibe.","Cringe.","Um, {pal.Influencer}?"},
            ["react_more.Influencer.2"]=new[]{"Not for the feed.","I'm muting that.","That's so off brand.","Delete that from my memory.","My engagement just dropped.","Don't tag me in that.","Unfollow.","That's a no from my audience."},
            ["react_lead.Burner.0"]=new[]{"That's beautiful.","Welcome home, {pal.Burner}.","I feel that.","Thank you, {pal.Burner}.","Oh, wow."},
            ["react_more.Burner.0"]=new[]{"Now that's the spirit.","That's a real gift.","Radical honesty. Love it.","You'd fit right in at our camp.","That came from the heart.","Leave no trace but that.","The dust approves.","That's some real community."},
            ["react_lead.Burner.1"]=new[]{"Okay, sure.","Hm. Fair.","I hear you, {pal.Burner}.","Alright.","Mm, {pal.Burner}."},
            ["react_more.Burner.1"]=new[]{"That's one way to see it.","Not quite gifting, but okay.","I'll sit with that.","Some of that rang true.","Half in the spirit, maybe.","We all start somewhere.","Bit of a tourist answer.","The desert will teach you."},
            ["react_lead.Burner.2"]=new[]{"Hm.","Oh, {pal.Burner}.","Wow.","Yikes.","Easy, {pal.Burner}."},
            ["react_more.Burner.2"]=new[]{"That's very default.","Very commercial.","Not the spirit.","That stings a little.","Very sponsored of you.","Who's selling who here?","Go back to your hotel.","That's not how we do it here."},
            ["react_lead.Raver.0"]=new[]{"YES, {pal.Raver}!","Ahh, I love you!","Same energy!","Bestie!","Okay, {pal.Raver}!"},
            ["react_more.Raver.0"]=new[]{"That's so PLUR!","Hug me right now!","You get the bass!","Let's trade kandi!","Front row energy!","My heart just dropped!","Kandi for life!","That's a whole vibe!"},
            ["react_lead.Raver.1"]=new[]{"Haha, sure!","Okay, cool!","Ha, yeah, {pal.Raver}!","Totally!","Fair, {pal.Raver}!"},
            ["react_more.Raver.1"]=new[]{"I guess!","Kinda vibing with it!","Not a drop, but okay!","I'll take it!","Half a hug for that!","Let's see where the set goes!","Sure, I'll dance to it!","Mid-set energy!"},
            ["react_lead.Raver.2"]=new[]{"Oh. Okay, grandpa.","Wow.","Um, {pal.Raver}.","Yikes.","Oof, {pal.Raver}."},
            ["react_more.Raver.2"]=new[]{"Buzzkill.","Less talk, more bass?","That's not it.","Hug it out?","That killed the drop.","Somebody's not dancing.","Where's your rave spirit?","My kandi just snapped."},
            ["react_lead.DraggedAlongDad.0"]=new[]{"Ha! Good one.","Well said, {pal.DraggedAlongDad}.","Now that's sensible.","Exactly.","Thank you, {pal.DraggedAlongDad}."},
            ["react_more.DraggedAlongDad.0"]=new[]{"Finally, a normal answer.","That's my kind of thinking.","You'd get on with my brother-in-law.","Short and sweet.","Somebody raised you right.","I'll tell the wife you said that.","Common sense at last.","You'd do well in sales."},
            ["react_lead.DraggedAlongDad.1"]=new[]{"Sure, sure.","If you say so.","Hm. Okay, {pal.DraggedAlongDad}.","Right-o.","I suppose, {pal.DraggedAlongDad}."},
            ["react_more.DraggedAlongDad.1"]=new[]{"Can't argue with that.","Close enough.","I've heard worse.","That's a maybe from me.","Sounds about right.","We'll see, won't we?","I'll take your word for it.","That's about what I figured."},
            ["react_lead.DraggedAlongDad.2"]=new[]{"Hoo boy.","Now, {pal.DraggedAlongDad}.","Huh?","Oh dear.","Steady on, {pal.DraggedAlongDad}."},
            ["react_more.DraggedAlongDad.2"]=new[]{"I don't follow.","That's a lot of words.","Is that a young person thing?","My ears hurt now.","I'm too old for this.","Say that in plain English?","Ask your mother.","Where's the nearest bench?"},
            ["react_lead.Security.0"]=new[]{"Good.","Alright, {pal.Security}.","Okay.","Fine.","Thank you, {pal.Security}."},
            ["react_more.Security.0"]=new[]{"That's fair.","Appreciated.","Noted.","That's what I like to hear.","Clear enough.","That helps.","Good answer.","I'll make a note of that."},
            ["react_lead.Security.1"]=new[]{"Mm.","Okay, {pal.Security}.","Go on.","Sure.","Uh-huh, {pal.Security}."},
            ["react_more.Security.1"]=new[]{"If you say so.","I'm listening.","Keep going.","We'll see.","Not convinced yet.","Hm. Maybe.","Hm. Okay.","Carry on."},
            ["react_lead.Security.2"]=new[]{"Excuse me?","Listen, {pal.Security}.","Come on.","Really?","Look, {pal.Security}."},
            ["react_more.Security.2"]=new[]{"Don't get smart.","That's not helping.","Try again.","Not the answer I wanted.","Let's keep it simple.","You're not making this easier.","Don't make this harder.","That's not what I asked."},
            ["react_lead.silence"]=new[]{"Hello?","Um.","Okay then.","Cat got your tongue?","Nothing? Huh.","Hm.","Still with me?","No answer?","Tough crowd.","Huh."},
            ["react_more.silence"]=new[]{"Anyway.","Moving on.","Not a talker. Fine.","Silent type, I see.","I'll keep going.","Guess I'll fill the silence.","That's okay.","Where was I?","Thinking it over? Okay.","Right. So."},

            // A narc selling: a seller's line round a slip of cop-speak, or a line with cop-speak in it.
            ["buy.narc"]=new[]{"{narc_buy} {~narc_slip}","{~narc_slip} {narc_buy}","{narc_sell}","{~narc_lead} {narc_sell}"},
            ["narc_buy"]=new[]{"One {unit}, one {exchange2}, nobody {nobody}.","Keep the {money} low and the eye contact {friendly}.",
                "Quick and {quiet}. Nobody looks twice.","Just the one? Hold it like a {souvenir}.","Pay me quietly. Fold it nice and small."},
            ["narc_sell"]=new[]{"I can provide a controlled substance. Hypothetically.","Ten-four. I mean, sure. For my records, how many?","Let's conclude this proceeding on foot to {landmark}.",
                "Copy that. One unit, cash only. Nothing illicit.","As the kids call it, a little treat? I carry several.","What street value would you put on one of these?",
                "Fellow festival-goer, the usual rate. Hypothetically speaking.","Is anyone in the vicinity? Good. Hand me the cash."},
            // A clue holder at night, drawn out: someone lost, and a landmark.
            ["talk.clue"]=new[]{"{clue_line}","{~clue_lead} {clue_line}","{clue_line} {~clue_tail}"},
            ["clue_line"]=new[]{"Someone lost asked the way to {landmark}.","A lost-looking person was near {landmark}. Yours?","Someone was calling a name by {landmark}.",
                "A confused soul spun circles at {landmark}.","I sent someone very lost to {landmark}.","Someone's waiting by {landmark}. Looks lonely.",
                "A lost one borrowed my map near {landmark}.","A lost one asked me about {landmark}. Twice."},

            // Their word on your trust dance before the talk: a flop, all right, nailed.
            ["react.dance.0"]=new[]{"Ha, okay, you can't dance. Go on…","Oof. Those were not moves. Talk, then.","Wow, two left feet. Fine, I'm listening.",
                "That was rough. Make the talk better.","Okay, dancing's not your thing. Go on…","Yikes. Well, you tried. What's up?"},
            ["react.dance.1"]=new[]{"Not bad. Okay, let's talk.","Alright, you can move. What's up?","Fair enough. You've earned a chat.",
                "Decent. Go on, then.","Okay, okay. I'm listening.","That'll do. So, what's up?"},
            ["react.dance.2"]=new[]{"You've got moves! Okay, talk to me.","Whoa, you can really dance! What's up?","Now that's a dance! I like you already.",
                "Wow. Okay, you've got my attention.","Those moves! Alright, I'm all ears.","Okay, that was amazing. Talk to me."},
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

            // Small talk: a chat's second round.
            ["talk.small.cosmic"]=new[]{"This place has such a {warm} aura today.","I've heard {landmark} breathing {when}.","Your energy feels like {song} at sunrise.",
                "I traded {thing} for a feeling. Worth it.","The bass told me we'd meet. Here you are.","I followed a cloud here from {landmark}.",
                "We're all just stardust in a field, friend.","Time moves slower by {landmark}. Try it."},
            ["talk.small.hype"]=new[]{"The light on you right now is unreal.","Can we take one by {landmark}? For the story.","Your {wearable} {is|are} giving main character.",
                "I'm live. Say hi to my {count} viewers.","This whole moment is content. Hold that pose.","Okay, {song} is about to trend. Called it.",
                "Rate my fit, honestly. Out of {count}.","Is {stranger} following me? Love that for me."},
            ["talk.small.earnest"]=new[]{"I made {thing} for a stranger. Want it?","I'm here to connect, not to consume.","Can I hold space for your weekend?",
                "I gave {snack} to a stranger. Felt amazing.","Thank you for being exactly who you are.","No money out here, just stories. Got one?",
                "I've been practicing radical presence {when}.","I hugged {stranger} today. We both cried."},
            ["talk.small.plur"]=new[]{"That drop earlier changed my whole DNA!","Want a kandi bracelet? I made {count}!","We're family now. That's how it works!",
                "I've been front row at {landmark} {when}!","Your vibe matches my bass face perfectly!","Peace, love, unity, respect, and {snack}!",
                "Hug? It's mandatory. I don't make the rules!","I lost {friend} in the pit. Worth it!"},
            ["talk.small.plain"]=new[]{"Nice weather for it. Not too hot.","I found the water tent. Very reasonable lines.","Long walk from parking, but worth it.",
                "My feet hurt, but the music's fine.","Have you tried {snack}? Decent value.","I'm waiting on {friend}. Always late.",
                "Sound's a bit loud by {landmark}, if you ask me.","Good crowd today. Very polite."},
            // Selling to a buyer: the pitch, a sale's third round.
            ["sale.ask.cosmic"]=new[]{"The universe wants you to have this. I just carry it.","Hold it like a seed. It knows what to do.","Let's pass it during {song}. The bass will hide us.",
                "This one found me at {landmark}. Now it's yours.","It's not a sale. It's an energy exchange.","Feel the price instead of thinking it."},
            ["sale.ask.hype"]=new[]{"Limited drop. Only {count} in the whole festival.","Trust me, this is about to be everywhere.","Collab price, just for you. Don't tell my followers.",
                "Unboxing this by {landmark} would go viral.","It's giving premium. You're giving buyer.","Get it before {stranger} posts about it."},
            ["sale.ask.earnest"]=new[]{"I'll be honest: it's good, and it's worth it.","No pressure. If it's not right for you, that's okay.","Fair price, fair trade, and a hug if you want one.",
                "I'd rather you trust me than buy from me.","Small and kind, that's how I like to trade.","Straight answer: one unit, no games."},
            ["sale.ask.plur"]=new[]{"For you, fam? Friend price, always!","Pass it like kandi. Peace, love, unity!","We're family now, so it's basically a gift!",
                "Swap it on the drop. Nobody sees a thing!","One for you, and a hug for free!","My bestie from {landmark} loved these!"},
            ["sale.ask.plain"]=new[]{"Usual rate. Cash is fine.","One unit, as discussed. Nothing fancy.","Quick and simple, then we both get on with our day.",
                "Here you go. Keep it in your pocket.","Fair price. I'm not here to haggle.","Same as last time, if there was a last time."},
            // Buying from a seller: the ask, a buy's third round.
            ["buy.ask.cosmic"]=new[]{"The moon told me to come find you.","Whatever you carry, I'm open to receiving it.","My aura's been asking for a little treat all day.",
                "Let's trade like two leaves in the wind.","I sense you carry a little joy. Can I have some?","Hand it over slow, like a sunset."},
            ["buy.ask.hype"]=new[]{"I need one for the vlog. Strictly content.","Are you the one everyone's posting about?","Price check? My followers will want to know.",
                "Can I say I got it from you? Great for both of us.","This is going to look so good by {landmark}.","I've seen {count} stories about you. Let's do it."},
            ["buy.ask.earnest"]=new[]{"Honestly, I'd just like one. No haggling.","I'll pay fair and say thank you. That's the deal.","I trust you. I hope you can trust me.",
                "Just one, just for me, just for tonight.","Folks at {landmark} said you're kind.","I can offer cash and genuine gratitude."},
            ["buy.ask.plur"]=new[]{"Fam! Can I get one? I'll throw in kandi!","One for me and one hug for you!","You're the best! Seriously, PLUR price?",
                "I'll pay and give you a hug. Win-win!","My crew said you're family. Hook me up!","Let's do it before the drop! Hurry!"},
            ["buy.ask.plain"]=new[]{"I'd like one, please. Cash is ready.","What's the price? Fine, I'll take it.","Just the one, thanks. Then I'll be on my way.",
                "No need to make a show of it. Quick swap.","Exact change, folded. Saves us both time.","I'll take one if you've got one."},
            // Answering to security: your story, the third round.
            ["security.ask.cosmic"]=new[]{"Have you ever really looked at the sky?","I'm not holding anything but good vibes, man.","We're all just energy, you and me both.",
                "I was meditating by {landmark}. Ask the trees.","Your aura looks stressed. Want a hug?","Time isn't real, so I couldn't have been anywhere."},
            ["security.ask.hype"]=new[]{"Do you know who I am? I have {count}K followers.","Can we do this somewhere with better lighting?","This is going on my story. Smile!",
                "I'm literally a brand ambassador here.","Okay, but your uniform is kind of iconic.","My manager can sort this out. Want her number?"},
            ["security.ask.earnest"]=new[]{"I'll be honest with you, I'm just here for the art.","I respect the work you do out here. Truly.","Ask me anything. I've got nothing to hide.",
                "I'm with a camp that cleans up after the crowd.","Let's keep this calm and kind for both of us.","I hear you. I want everyone safe too."},
            ["security.ask.plur"]=new[]{"You're family too! PLUR!","I was just dancing by {landmark}, I swear!","Want a kandi bracelet? It's a friendship thing!",
                "No trouble here! Just bass and hugs!","Wanna see my shuffle? It's really good!","Respect! That's the R in PLUR!"},
            ["security.ask.plain"]=new[]{"Of course. What do you need?","I was just heading to the water tent.","No problem. I'll keep it moving.",
                "I understand. I'm only here for the music.","Happy to help however I can.","I'm with {friend}. We're heading back to camp."},
            // The other rounds' replies ("<topic>.<round>.<style>"). Where a round gives the other person a new line, it is
            // "line.<topic>.<round>".
            // A chat: hello, small talk, what they're really about, goodbye.
            ["talk.greet.cosmic"]=new[]{"Hey, star child. Our paths crossed for a reason.","Hello, fellow traveler. Your aura waved first.","I felt you before I saw you. Hi.",
                "The wind told me to say hi to you. So, hi.","Greetings from the far side of {landmark}.","Hi. You look like a rainbow in human form."},
            ["talk.greet.hype"]=new[]{"Hi! Sorry, your fit stopped me in my tracks.","Hey, you're giving main character. Hi!","Hiii. Can I say you're iconic? You're iconic.",
                "Hey! Quick hello before {song} starts trending.","Okay, hi! I had to come say hello to that look.","Hey you! My {count} viewers say hi too."},
            ["talk.greet.earnest"]=new[]{"Hi. I just wanted to say hello, no agenda.","Hey, neighbor. Welcome home, whoever you are.","Hello! I'm here to meet people. You're people.",
                "Hi there. Mind if I share this moment with you?","Hey. I saw you smile at {landmark}. Hello!","Hello, friend. How's your heart doing?"},
            ["talk.greet.plur"]=new[]{"Hiii, fam! Hug hello? Hug hello!","Hey bestie! You look like a kandi kid!","Hi hi hi! New friend unlocked!",
                "Hey! I love your energy already!","Fam! Did we meet at {landmark}? Hi anyway!","Hey you! PLUR hello! That's a thing now!"},
            ["talk.greet.plain"]=new[]{"Hi there. How's it going?","Hello. Mind if I stand here a minute?","Hi. Nice to see a friendly face.",
                "Hi. Nice spot you've found here.","Hey. Having a good time so far?","Hello. Bit busy by {landmark}, isn't it?"},
            ["talk.ask.cosmic"]=new[]{"So what brings your spirit out here?","What are you seeking out here, truly?","Tell me what your soul came here for.",
                "Is your journey about the music, or something more?","What's the universe been handing you lately?","Be honest, friend. What's your frequency?"},
            ["talk.ask.hype"]=new[]{"Okay, real talk: what's your whole deal here?","So what's the plan? Content, music or chaos?","Be honest. Are you the main event or the crowd?",
                "What's the move? I need the inside scoop.","Who do I tag when I post about you?","Spill it. What are you really here for?"},
            ["talk.ask.earnest"]=new[]{"Can I ask what you're really here for?","What would make this weekend special for you?","Is there anything you need? I'm happy to help.",
                "What's something true about you nobody asks?","What brought you here? I'd love to know.","Honestly, what are you hoping to find out here?"},
            ["talk.ask.plur"]=new[]{"So what's your festival mission, fam?","Are you here for the bass or the people?","What's your favorite thing here so far?",
                "Need anything? Water? Kandi? A hug?","Tell me your vibe in one word! Go!","Who are you here with? Bring them over!"},
            ["talk.ask.plain"]=new[]{"So what brings you out here?","Here for anyone in particular?","What's your plan for the rest of it?",
                "Do you come to these every year?","Anything you're after while you're here?","Seen anything worth the ticket yet?"},
            ["talk.close.cosmic"]=new[]{"May your path glow, friend. Until next time.","We'll meet again. The universe loops.","Go gently. I'll see you in the next song.",
                "Thank you for the energy. Float on.","Our orbits will cross again. Peace.","Carry this moment with you. Bye for now."},
            ["talk.close.hype"]=new[]{"Okay, bye! Follow me, I'll follow back.","This was iconic. Let's collab later.","Gotta go film. You were a highlight!",
                "Bye! You're definitely in my recap.","Catch you by {landmark}. Stay gorgeous.","Okay, I'm obsessed. Bye for now!"},
            ["talk.close.earnest"]=new[]{"Thank you for this. Truly. Take care.","That meant something. See you around, neighbor.","Go well. Drink water and be kind.",
                "I'm glad we met. Look after yourself.","Thanks for listening. That's a real gift.","Bye for now. Our camp's door is always open."},
            ["talk.close.plur"]=new[]{"Bye bye, fam! Hug for the road!","Love you! See you at the drop!","Stay PLUR, bestie! Bye!",
                "Here, kandi for the road! Bye!","See you in the front row, fam!","Bye! Don't forget to hydrate!"},
            ["talk.close.plain"]=new[]{"Well, I'll let you get on. Take care.","Nice chatting. Enjoy the rest of it.","Good to meet you. See you around.",
                "Right, I'm off to find {friend}. Bye.","Take it easy. Mind the crowds.","Thanks for the chat. All the best."},
            ["line.talk.ask"]=new[]{"So what are you actually after out here?","Okay, what's your story? Everyone's got one.","Wait, why are you really talking to me?",
                "So what's the plan for the rest of the weekend?","You looking for something? Or someone?","Tell me something real. I can take it.",
                "Be honest. Do I know you from {landmark}?","What's the one thing you came here for?"},
            ["line.talk.close"]=new[]{"Anyway, I should find {friend}. This was nice.","Okay, I'm off to {landmark}. Catch you later.","Well! That was a conversation. Bye!",
                "I think {song} is starting. Gotta run.","Good talk. Really. Go enjoy it.","Alright, I'll let you go. Take care.",
                "Hey, it was good meeting you. Stay safe.","Right, my snack break is over. Bye!"},
            // Selling to a buyer: an opening, feeling them out, the pitch, sealing it.
            ["sale.greet.cosmic"]=new[]{"Hey. I sensed someone seeking something special.","Greetings. The universe nudged me your way.","Hello, kindred spirit. Got a minute for magic?",
                "Hi. I carry little wonders for open hearts.","Your aura said you were looking. Hello.","Hey there. Something tells me we should talk."},
            ["sale.greet.hype"]=new[]{"Hey! I've got something you'll want to post.","Hi! You look like an early adopter.","Okay, hi, I have an exclusive for you.",
                "Hey, quick one: want first look at the drop?","Hi! I'm doing a little pop-up. Interested?","Hey you! Collab opportunity. Got a sec?"},
            ["sale.greet.earnest"]=new[]{"Hi. I'll be upfront: I have something to offer.","Hello! No pressure, but I might have what you need.","Hey, neighbor. I've got a little something to share.",
                "Hi there. Can I offer you something, honestly?","Hello. I carry a few treats. Just asking.","Hey. If you're looking, I might be able to help."},
            ["sale.greet.plur"]=new[]{"Hey fam! Got something fun for you!","Hiii! Want a little treat for the drop?","Bestie! I think you need what I've got!",
                "Hey hey! Friend deals, only for friends!","Hi! Wanna make your set even better?","Fam! Come here, I've got a surprise!"},
            ["sale.greet.plain"]=new[]{"Hi. I hear you might be looking for something.","Hello. I've got one if you want one.","Hi there. Quick question, if you've got a moment.",
                "Hello. I might have what you're after.","Hi. I'll keep this short. Interested in one?","Hey. I've got a spare. Want to hear about it?"},
            ["sale.small.cosmic"]=new[]{"Have you been feeling the music in your bones?","Tell me, what's your soul craving right now?","How has the festival been treating your spirit?",
                "Do you feel this whole place opening up?","What kind of journey are you hoping for?","Is your energy rising or drifting right now?"},
            ["sale.small.hype"]=new[]{"So who are you here to see? Asking for content.","What's your vibe? Chill or full send?","Have you been to {landmark}? It's so aesthetic.",
                "Rate your weekend so far. Be honest.","Your friends here too? Love a squad.","Are you posting today or living in the moment?"},
            ["sale.small.earnest"]=new[]{"How's your weekend been, honestly?","Are you looking after yourself out here?","Have you found your people here yet?",
                "What's been the best part so far?","Do you feel safe out here? That matters to me.","Is this your first time? No judgment."},
            ["sale.small.plur"]=new[]{"Who's your favorite set so far, fam?","Have you been dancing all day? Me too!","Are you rolling with a crew? Bring them!",
                "Did you catch {song}? Insane, right?","How many kandi trades so far? I'm at {count}!","Are you feeling the bass yet, bestie?"},
            ["sale.small.plain"]=new[]{"Having a good time so far?","Long day on your feet, I bet.","Who did you come to see?",
                "Been here long? I just got in.","Holding up alright out here?","Found the water tent yet? The lines are fine."},
            ["sale.close.cosmic"]=new[]{"Let the exchange happen. Slow and easy.","Hold it close. It knows the way.","And so the circle closes. Peace.",
                "Take it during the drop. The bass will bless it.","There. Energy given, energy received.","Breathe in, pass it on. Done."},
            ["sale.close.hype"]=new[]{"Done deal. Don't forget to tag me.","Pleasure. You've got the exclusive now.","And that's a wrap. Iconic transaction.",
                "Okay, sold. Your story's going to pop.","Thank you! You've got taste.","Sealed. You're in the inner circle now."},
            ["sale.close.earnest"]=new[]{"Thank you. Enjoy it, and be safe.","Fair trade, fair hearts. Thank you.","There you go. Look after yourself.",
                "Thanks for trusting me. Truly.","That's it. No strings, just thanks.","All yours. Have a beautiful time."},
            ["sale.close.plur"]=new[]{"Yay! Enjoy, fam! Hug to seal it!","Done! Peace, love, unity, respect!","Ahh! Have the best drop ever!",
                "All yours, bestie! Stay safe!","Deal! Kandi handshake? Kandi handshake!","Woo! Now go dance!"},
            ["sale.close.plain"]=new[]{"There we go. Pleasure doing business.","That's that. Enjoy the rest of it.","All done. Keep it in your pocket.",
                "Thanks. Nice and simple.","Right, that's sorted. Take care.","Cheers. Off you go, then."},
            ["line.sale.small"]=new[]{"So, have you been here all weekend?","You don't look like a regular. Or do you?","I've been watching {song} from {landmark}. You?",
                "{friend} said someone around here was friendly.","Busy out here. Good cover, I guess.","I've had {snack}. Now I want something better.",
                "You've got that look. Like you know things.","Is it always this loud by {landmark}?"},
            // Their last line asks whether there's a deal: the talk settles it, so it never agrees before the score is in.
            ["line.sale.close"]=new[]{"So, what do you say?","Well? Are we doing this or not?","Alright. Do we have a deal?",
                "Before {stranger} looks over: yes or no?","Okay. Do we do this during {song}?","Last chance. Is it worth it?",
                "So where does that leave us?","So, are we doing this or what?"},
            // Buying from a seller: an opening, sizing each other up, the ask, sealing it.
            ["buy.greet.cosmic"]=new[]{"Hi. The stars said you might carry some joy.","Hello, gentle soul. I'm open to receiving.","Hey. Your energy feels like a shop for dreams.",
                "Greetings. I followed a feeling to you.","Hi, friend. Got a minute for a seeker?","Hey. The trees pointed at you. Weird, right?"},
            ["buy.greet.hype"]=new[]{"Hi! Everyone's talking about you. Like, everyone.","Hey! Are you the one from the stories?","Okay, hi, I need to talk to you.",
                "Hey you! I think you're my new favorite vendor.","Hi! Quick question, totally for the vlog.","Hey! I'm a fan already. Can we talk?"},
            ["buy.greet.earnest"]=new[]{"Hi. I'll be honest: I'm hoping you can help me.","Hello, neighbor. Mind if I ask you something?","Hey. Folks said you're kind. I'm hoping so.",
                "Hi there. I'd like to talk, if that's okay.","Hello. I come in good faith, I promise.","Hey. Can I trust you with a question?"},
            ["buy.greet.plur"]=new[]{"Hey fam! I heard you're the one to see!","Hiii! You look like you've got the good stuff!","Bestie! Help a raver out?",
                "Hey hey! Friend of a friend! Hi!","Hi! My crew sent me! Please be nice!","Fam! Can we talk real quick?"},
            ["buy.greet.plain"]=new[]{"Hi. Someone said you might be selling.","Hello. Got a minute?","Hi there. I'm looking for one, if you've got it.",
                "Hey. I won't take much of your time.","Hello. I was told to ask you.","Hi. Quick question, if that's alright."},
            ["buy.small.cosmic"]=new[]{"How's the universe been to you this weekend?","You seem grounded. Been out here long?","Do you feel the bass in the ground too?",
                "Which songs have been moving you lately?","Your vibe is calm. I like calm.","Has the festival shown you anything strange?"},
            ["buy.small.hype"]=new[]{"Okay, how long have you been doing this?","Your setup is so low-key. I love it.","Do you have a lot of regulars? I want in.",
                "Is it busy for you? You must be famous.","Love your {wearable}. Where'd you get it?","Have you seen how packed {landmark} is?"},
            ["buy.small.earnest"]=new[]{"How's your weekend going, really?","Are you taking breaks? You deserve them.","I don't want to rush you. How are you?",
                "Have you eaten? I've got {snack}.","It's nice to meet someone calm out here.","Is this your first year, or are you a regular?"},
            ["buy.small.plur"]=new[]{"Have you caught any good sets, fam?","Love your kandi! Trade later?","Are you dancing at all, or working?",
                "Who's your crew? They seem fun!","Did you hear {song}? I screamed!","How's your energy holding up, bestie?"},
            ["buy.small.plain"]=new[]{"Busy out here?","Been here all weekend, then?","Good spot you've got here.",
                "Hope the crowd's treating you alright.","Long lines everywhere, aren't there?","The weather's holding up, at least."},
            ["buy.close.cosmic"]=new[]{"Thank you. I'll treat it like a little star.","The exchange is complete. Blessings.","I'll carry it gently. Peace, friend.",
                "May your pockets never run dry. Thank you.","Done. The universe smiles on us both.","Thank you for the magic. Float on."},
            ["buy.close.hype"]=new[]{"Amazing. You're going in my favorites.","Done! Best purchase of the weekend.","Thank you! I'll leave a glowing review.",
                "Okay, iconic. Pleasure doing business.","Got it! I'll say you're the best around.","Perfect. You're officially my go-to."},
            ["buy.close.earnest"]=new[]{"Thank you. That means a lot.","Fair and kind. Thank you, truly.","Thanks for trusting me. I'll be careful.",
                "I appreciate you. Take care of yourself.","That's all I needed. Thank you.","Thank you, neighbor. Be well."},
            ["buy.close.plur"]=new[]{"Yay! Thank you, fam! Hug?","You're the best! PLUR forever!","Thank you! Kandi for you!",
                "Ahh, thank you! See you at the drop!","Best deal ever! Love you!","Thank you, bestie! Stay safe!"},
            ["buy.close.plain"]=new[]{"Thanks. That's all I needed.","Appreciate it. I'll be on my way.","Cheers. Pleasure.",
                "Great, thanks. Take care.","That's sorted, then. Thank you.","Thanks. Enjoy the rest of the weekend."},
            ["line.buy.small"]=new[]{"You new around here? I like to know my people.","Who sent you? Just curious.","Having a good weekend? You look like you are.",
                "I don't usually talk to strangers. Go on.","You've been by {landmark} a lot, huh?","Relax. Small talk first. How's the music?",
                "You seem alright. Tell me about yourself.","Hm. You look like you can keep a secret."},
            ["line.buy.close"]=new[]{"So. Still want one?","Alright. What's it going to be?","Hm. Can I trust you with one?",
                "Okay. Are we doing this quietly?","Right then. Yes or no?","Last thing: you won't make a scene, right?",
                "So, are we doing this?","Okay. Deal or no deal?"},
            // Answering to security: a hello, small talk, your story, signing off.
            ["security.greet.cosmic"]=new[]{"Hello, guardian. Your aura is very official.","Hey. I come in peace, like a leaf.","Hi. Did the stars send you to me?",
                "Greetings, protector of the vibes.","Whoa, hi. Your badge is so shiny.","Hello. I feel a lot of tension. Breathe?"},
            ["security.greet.hype"]=new[]{"Oh hi! Are you filming? No? Okay.","Hey! Is this a photo op? I'm ready.","Hi! Love the uniform, honestly.",
                "Oh my gosh, hi. Is something happening?","Hey! My followers love security content.","Hi! Quick, which is my good side?"},
            ["security.greet.earnest"]=new[]{"Hi. Of course, how can I help?","Hello. I'm happy to talk. What's up?","Hey, thanks for keeping us safe out here.",
                "Hi there. Is everything alright?","Hello. I'll answer anything you need.","Hey. I appreciate you checking in."},
            ["security.greet.plur"]=new[]{"Hiii! Security fam! What's up?","Hey! Want a hug? No? Okay!","Hi! Are you having fun too?",
                "Oh hey! I love your vest!","Hey! PLUR to you too!","Hi! Did I win something?"},
            ["security.greet.plain"]=new[]{"Hello. What seems to be the problem?","Hi. Something I can help with?","Hello. Is there an issue?",
                "Hi there. Yes, how can I help?","Sure, I've got a minute. What's up?","Hello. Am I in the way?"},
            ["security.small.cosmic"]=new[]{"Long shift? Your feet must feel the earth.","Do you ever just listen to the bass up close?","The sky out here is wild, isn't it?",
                "Have you tried breathing with the music?","I'm just floating between stages, man.","Being security must be a heavy energy."},
            ["security.small.hype"]=new[]{"How many people do you stop a day? For a post.","Do you get to see the headliners up close?","Your job is so main character. Love it.",
                "Is it true security gets the best views?","I was just heading to {landmark} for photos.","Can I get a pic with you after? For the vibes."},
            ["security.small.earnest"]=new[]{"It must be a long day for you. Thank you.","I'm just here for the art and the people.","I've been volunteering at our camp all weekend.",
                "Is it hard, keeping everyone safe out here?","I'm happy to step aside, no trouble at all.","I just want everyone to have a good time."},
            ["security.small.plur"]=new[]{"I've been dancing all day! It's been amazing!","Do you like the music? It's so good!","I'm just with my rave fam by {landmark}!",
                "Everyone's so nice here! You too!","I just traded {count} kandi! Look!","Do you ever get to dance on shift?"},
            ["security.small.plain"]=new[]{"Busy day for you, I imagine.","I'm just on my way to the water tent.","The crowd's been well behaved, mostly.",
                "I'm meeting {friend} by {landmark}.","No trouble from me. Just walking.","Long day. My feet are killing me."},
            ["security.close.cosmic"]=new[]{"Peace be with your patrol, guardian.","We're good, right? The vibes say we're good.","I'll float along now. Namaste.",
                "Thank you for seeing me. Truly seeing me.","May your shift pass like a gentle breeze.","Alright. I'll go hug a tree. Peacefully."},
            ["security.close.hype"]=new[]{"So we're cool? Great. Not posting this.","Thanks! Five stars for security today.","Okay, love that for us. Bye!",
                "We good? Perfect. My followers were worried.","Thanks. You're kind of iconic, you know.","Bye! I'll tag the festival in a nice post."},
            ["security.close.earnest"]=new[]{"Thank you for hearing me out.","I appreciate your patience. Stay safe.","Thanks. I'll keep it calm and kind.",
                "Understood. Thank you for being fair.","I'll be on my way. Have a good shift.","Thank you. I know you're just doing your job."},
            ["security.close.plur"]=new[]{"Thank you! PLUR, security fam!","Okay! Bye! You're the best!","Yay, we're friends now! Bye!",
                "Thanks! Here, kandi! No? Okay, bye!","Love you! Stay hydrated on shift!","Bye! I'll be good, promise!"},
            ["security.close.plain"]=new[]{"Understood. I'll be on my way.","Right. Thanks for your time.","Okay. Anything else, or am I free to go?",
                "Fair enough. I'll keep moving.","Thanks. Have a quiet shift.","Alright. Sorry for the trouble."},
            // Security's last line says where things stand, for your goodbye to answer, and asks nothing.
            ["line.security.close"]=new[]{"Alright. I've heard enough for now.","Okay. Here's where we land on this.","Let me decide what to do with you.",
                "Fine. That'll do for now.","Alright. I'll be keeping an eye on you.","Okay. I'll make up my mind now.",
                "Right. I think we're done here.","Right. Let's wrap this up."},
        };
        // FestivalLines' parts and these, each reply: its round's body in its style, bare or dressed with the style's hello or tail
        // ("reply.<topic>.<round>.<style>", ReplyKind), and each reaction: its lead, then its comment ("react.<persona>.<grade>",
        // "react.silence").
        private static Dictionary<string,string[]> Tables()
        {
            var all=new Dictionary<string,string[]>(FestivalLines.Parts);
            foreach(var part in Parts) all.Add(part.Key,part.Value);
            string[] Dressed(string body,string style)=>new[]{"{"+body+"}","{~"+style+"_hi} {"+body+"}","{"+body+"} {~"+style+"_tail}"};
            foreach(var topic in Topics) foreach(var style in Styles) for(int round=0;round<Rounds.Length;round++) all.Add(ReplyKind(topic,round,style),Dressed(topic+"."+Rounds[round]+"."+style,style));
            foreach(var persona in Tastes.Keys) for(int grade=Smooth;grade<=Awkward;grade++) all.Add("react."+persona+"."+grade,new[]{"{react_lead."+persona+"."+grade+"} {react_more."+persona+"."+grade+"}"});
            all.Add("react.silence",new[]{"{react_lead.silence} {react_more.silence}"});
            return all;
        }
        public static readonly LineGrammar Grammar=new LineGrammar(Tables());
    }
}
