using System;
using System.Collections.Generic;

namespace Festival.Core
{
    // DIALOG-3: one turn of a talk (Conversations.cs): their Reaction to your last reply ("" on the first turn) and their Line,
    // then your three Replies. Grades holds each reply's grade (ConversationLines.Smooth, SoSo or Awkward); a Garbled reply's
    // longer words' interior letters are jumbled, as a dosed player says it. Pick is the reply chosen: -1 until one is,
    // and for good once the turn has run out, a silence.
    [Serializable] public sealed class ConversationTurn { public string Reaction="",Line=""; public string[] Replies=new string[3]; public int[] Grades=new int[3]; public bool[] Garbled=new bool[3]; public int Pick=-1; }

    // DIALOG-3: what's said in a talk, built by a LineGrammar (DIALOG-4) that holds FestivalLines' parts as well as these.
    // - Topics: a chat (Conversation) is "talk", a sale "sale", a buy "buy" and a talk with security "security".
    // - Their first line is their persona's opener (FestivalLines' "opener.<persona>"), so it hints at who they are; security
    //   opens on one of its lines ("police"). Each later turn opens on their reaction to your reply, in their persona's voice
    //   (a silence gets its own), then the topic's line: a festivalgoer's small talk (a regular's chat-check answer), a buyer's
    //   or a seller's line ("sale", "buy"), or security's. In a chat or a buy, a reply that lands draws out a clear line with
    //   the person's tell (Body), and the first one shown sets the Hunch.
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
        // Each persona's liked and hated style, as indexes into Styles.
        private static readonly Dictionary<string,(int liked,int hated)> Tastes=new Dictionary<string,(int liked,int hated)>{
            ["Wook"]=(0,1),["Influencer"]=(1,2),["Burner"]=(2,1),["Raver"]=(3,4),["DraggedAlongDad"]=(4,3),[Security]=(4,0)};
        public static string Topic(string kind)=>kind=="Sale"?"sale":kind=="Buy"?"buy":kind=="Police"?"security":"talk";

        // A turn of a talk on `topic` with `persona` on `festival`: turn 0 opens it; a later one follows a reply of grade `heard`
        // (Silence for none), and is `clear` when the reply drew their `role`'s tell out. `garbled` of the replies come out jumbled. Drawn from `random` in order (their reaction, their
        // line, the so-so style, the replies' order, the replies, the garbling), and given `history` each line heard lately is
        // drawn again and each line drawn is remembered (DialogueHistory.Fresh), as DialogueGrammar's chats are.
        // What a chat or a buy leaves you feeling (Conversations.cs): a hunch, never a verdict.
        public const string HunchCop="You get the feeling they might be a cop.",HunchBuyer="You get the feeling they'd buy if you offered.",HunchMusic="You get the feeling they're just here for the music.";
        public static string HunchClue(string landmark)=>"You get the feeling they know something about "+landmark+".";
        public static ConversationTurn Turn(string topic,string persona,string role,int festival,int turn,int heard,bool clear,int garbled,ContentRandom random,DialogueHistory history=null)
        {
            if(Array.IndexOf(Topics,topic)<0||!Tastes.TryGetValue(persona,out var taste)) throw new ArgumentException("Unknown talk topic or persona: "+topic+", "+persona);
            var said=new ConversationTurn();
            if(turn>0) said.Reaction=Say(heard==Silence?"react.silence":"react."+persona+"."+heard,festival,random,history);
            said.Line=Say(turn>0?Body(topic,role,clear):persona==Security?"police":"opener."+persona,festival,random,history);
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
        // What a festivalgoer or security says after the first turn: small talk, a buyer's or a seller's line, or security's. A
        // clear line in a chat or a buy carries the person's tell, as the chat check's answers do: a narc's cop-speak (their small
        // talk, or a seller's line round a slip), a buyer's wish to buy, a clue holder's landmark; anyone else's is plain.
        private static string Body(string topic,string role,bool clear)
        {
            if(topic=="sale") return "sale";
            if(topic=="security") return "police";
            if(clear&&role=="ClueHolder") return "talk.clue";
            if(topic=="buy") return clear&&role=="Narc"?"buy.narc":"buy";
            return clear&&(role=="Narc"||role=="Buyer")?"answer."+role+".0":"answer.Regular.0";
        }
        // The hunch a chat or a buy leaves, from its first clear line (`told`, null if none came through): their role's tell, or
        // nothing to go on.
        public static string Hunch(string role,string told,int festival)
        {
            if(told==null) return HunchMusic;
            if(role=="Narc") return HunchCop;
            if(role=="Buyer") return HunchBuyer;
            string landmark=role=="ClueHolder"?LandmarkIn(told,festival):"";
            return landmark==""?HunchMusic:HunchClue(landmark);
        }
        // The landmark (FestivalLines' "landmark" part) a line names on this festival, or "".
        private static string LandmarkIn(string line,int festival)
        {
            foreach(var entry in FestivalLines.Parts["landmark"])
            {
                bool tagged=entry.Length>1&&char.IsDigit(entry[0])&&entry[1]==':';string name=tagged?entry.Substring(2):entry;
                if((!tagged||entry[0]-'0'==festival)&&line.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0) return name;
            }
            return "";
        }
        private static string Say(string kind,int festival,ContentRandom random,DialogueHistory history)
        {
            if(history==null) return Grammar.Line(kind,festival,random);
            string line=history.Fresh(Grammar,kind,festival,random,out var key); history.Acknowledge(key); return line;
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

        private static readonly Dictionary<string,string[]> Parts=new Dictionary<string,string[]>{
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
                "Quick and {quiet}. Nobody looks twice.","Just the one? Hold it like a {souvenir}.","Pay me in fives. {count} of them."},
            ["narc_sell"]=new[]{"I can provide a controlled substance. Hypothetically.","Ten-four. I mean, sure. For my records, how many?","Let's conclude this proceeding on foot to {landmark}.",
                "Copy that. One unit, cash only. Nothing illicit.","As the kids call it, a little treat? I carry several.","What street value would you put on one of these?",
                "Fellow festival-goer, it's five. Hypothetically speaking.","Is anyone in the vicinity? Good. Hand me the cash."},
            // A clue holder at night, drawn out: someone lost, and a landmark.
            ["talk.clue"]=new[]{"{clue_line}","{~clue_lead} {clue_line}","{clue_line} {~clue_tail}"},
            ["clue_line"]=new[]{"Someone lost asked the way to {landmark}.","A lost-looking person was near {landmark}. Yours?","Someone was calling a name by {landmark}.",
                "A confused soul spun circles at {landmark}.","I sent someone very lost to {landmark}.","Someone's waiting by {landmark}. Looks lonely.",
                "A lost one borrowed my map near {landmark}.","A lost one asked me about {landmark}. Twice."},

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
        // FestivalLines' parts and these, each reply: its style's body, bare or dressed with the style's hello or tail
        // ("reply.<topic>.<style>"), and each reaction: its lead, then its comment ("react.<persona>.<grade>", "react.silence").
        private static Dictionary<string,string[]> Tables()
        {
            var all=new Dictionary<string,string[]>(FestivalLines.Parts);
            foreach(var part in Parts) all.Add(part.Key,part.Value);
            foreach(var topic in Topics) foreach(var style in Styles) all.Add("reply."+topic+"."+style,new[]{"{"+topic+"."+style+"}","{~"+style+"_hi} {"+topic+"."+style+"}","{"+topic+"."+style+"} {~"+style+"_tail}"});
            foreach(var persona in Tastes.Keys) for(int grade=Smooth;grade<=Awkward;grade++) all.Add("react."+persona+"."+grade,new[]{"{react_lead."+persona+"."+grade+"} {react_more."+persona+"."+grade+"}"});
            all.Add("react.silence",new[]{"{react_lead.silence} {react_more.silence}"});
            return all;
        }
        public static readonly LineGrammar Grammar=new LineGrammar(Tables());
    }
}
