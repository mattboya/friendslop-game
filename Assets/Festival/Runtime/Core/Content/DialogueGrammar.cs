using System;
using System.Collections.Generic;

namespace Festival.Core
{
    [Serializable] public sealed class Conversation { public string Opener=""; public string[] Questions=new string[3],Answers=new string[3]; }
    // A chat is an opener (persona), three questions the tripper asks (small talk, the sales pitch, the lost friend)
    // and one answer to each: a persona interjection plus a line for the NPC's role. A line tagged "0:" is
    // Palm Mirage only and "1:" is Ember Playa only; untagged lines suit both. {ground} names the festival's
    // grounds and {where} a landmark on its map. Only fictional goods (Tongue Stamps, Fun Guys) are ever named.
    public static class DialogueGrammar
    {
        public static readonly string[] Roles={"Buyer","Narc","Regular","ClueHolder"};
        public static readonly string[] Personas={"Wook","Influencer","Burner","Raver","DraggedAlongDad"};
        // Cop-speak only narcs use. Every narc answer carries one; no other line in the grammar may contain any.
        public static readonly string[] NarcTells={"fellow festival-goer","street value","hypothetically speaking","where might one acquire","for my records","in the vicinity","ten-four","copy that","the suspect","the perpetrator","civilian","youths","the kids call","controlled substance","illicit","hundred hours","proceeding on foot","exit the vehicle"};
        public static bool HasNarcTell(string text) { if(string.IsNullOrEmpty(text)) return false; foreach(var tell in NarcTells) if(text.IndexOf(tell,StringComparison.OrdinalIgnoreCase)>=0) return true; return false; }
        // FNV-1a, so host and clients agree on an NPC's persona; string.GetHashCode differs between processes.
        public static string PersonaFor(string npcId) { uint hash=2166136261; foreach(char c in npcId??"") hash=unchecked((hash^c)*16777619); return Personas[hash%(uint)Personas.Length]; }
        public static Conversation Build(string role,string persona,int festival,int seed)
        {
            int r=Array.IndexOf(Roles,role),p=Array.IndexOf(Personas,persona);
            if(r<0||p<0||festival<0||festival>=Ground.Length) throw new ArgumentException("Unknown dialogue role, persona or festival: "+role+", "+persona+", "+festival);
            var random=new ContentRandom(seed); var result=new Conversation { Opener=Draw(Openers[p],festival,random,1)[0] };
            // Everything before the role's answers is drawn first, so the opener, questions and interjections are the same for every role.
            for(int i=0;i<3;i++) result.Questions[i]=Draw(Questions[i],festival,random,1)[0];
            // Two of the three answers open with a persona interjection; three in a row reads like a tic.
            var interjections=Draw(Interjections[p],festival,random,3); int plain=random.Next(3);
            for(int i=0;i<3;i++) result.Answers[i]=(i==plain?"":interjections[i]+" ")+Draw(Answers[r][i],festival,random,1)[0];
            return result;
        }
        private static List<string> Draw(string[] pool,int festival,ContentRandom random,int count)
        {
            var lines=new List<string>(); string tag=festival+":";
            foreach(var line in pool) if(!(line.Length>1&&char.IsDigit(line[0])&&line[1]==':')) lines.Add(line); else if(line.StartsWith(tag,StringComparison.Ordinal)) lines.Add(line.Substring(2));
            for(int i=0;i<count;i++) { int j=i+random.Next(lines.Count-i); var swap=lines[i]; lines[i]=lines[j]; lines[j]=swap; }
            lines.RemoveRange(count,lines.Count-count);
            for(int i=0;i<count;i++) { lines[i]=lines[i].Replace("{ground}",Ground[festival]); if(lines[i].Contains("{where}")) lines[i]=lines[i].Replace("{where}",Draw(Where,festival,random,1)[0]); }
            return lines;
        }
        private static readonly string[] Ground={"polo field","playa"};
        private static readonly string[] Where={
            "0:toward the Ferris wheel","0:around behind the Ferris wheel","0:toward the lights on the Ferris wheel","0:past the VIP deck","0:along the VIP fence","0:under the palms by the VIP gate",
            "0:toward the headliner stage","0:behind the headliner stage","0:past the iced matcha cart","0:toward the iced matcha line","0:across the polo field","0:along the far edge of the polo field",
            "1:toward the Man","1:out past the Man","1:around the back of the Man","1:into the dust","1:straight into the dust, no goggles","1:into the dust toward the drums",
            "1:after an art car shaped like a fish","1:toward the art car with the disco ball","1:across the open playa","1:out onto the deep playa","1:past the gifting tent","1:toward the camp that's gifting pancakes"
        };
        private static readonly string[][] Openers={
            new[]{ // Wook
                "Heyyy. You've got a real warm aura. Kind of teal.",
                "Oh, hey, friend. Wanna see a rock that looks like a smaller rock?",
                "Brother! I've been waiting for you. Or someone. You'll do.",
                "Whoa. Did you just walk out of the bass?",
                "Oh, good, a face. I was running low on faces.",
                "Hey. I've been here since Thursday. The festival started Friday.",
                "Duuude. Your shadow has incredible posture.",
                "Careful, you're standing where my feelings were. It's cool, they moved.",
                "Hey, want half this sandwich? It's been with me since the parking lot.",
                "Friend! Quick question: are my shoes on? I can't look down, I'm in a moment.",
                "0:Hey. The Ferris wheel told me you'd come. It tells me a lot of things.",
                "0:Mmm. The polo field has such good grass energy today. Sit with me.",
                "0:I snuck into VIP, but they have the same sky. Total scam.",
                "1:Welcome home, dust sibling.",
                "1:Hey. The Man told me you were coming. Well. He pointed.",
                "1:Hop on my art car. It's a shopping cart with a lamp in it."},
            new[]{ // Influencer
                "Oh my gosh, hi! Don't move. The lighting on you is unreal.",
                "Wait, are you somebody? You have somebody energy.",
                "Hi! Quick, say something candid. Go.",
                "Okay, you're in my shot, but make it fashion.",
                "Hey! I'm doing a get-ready-with-me, but it's a talk-to-strangers.",
                "Hi! Sorry, I'm live. Say hi to all nine of my viewers.",
                "Oh, you're so authentic. I need that for my feed.",
                "Hiii. Does this hat say 'effortless' or 'lost'? Say effortless.",
                "Hi! Can you take forty photos of me not noticing the camera?",
                "Hey! I'm a brand ambassador for this exact spot. Stand in it with me.",
                "0:Hi! Can you hold my iced matcha? It's technically in the shot.",
                "0:Oh, hi! Are you VIP? You give VIP. You give VIP-adjacent.",
                "0:Hey! When does the headliner go on? I need golden hour to cooperate.",
                "1:Hi! Is this dust or a filter? I genuinely can't tell anymore.",
                "1:Hey! Film me looking mysterious on this art car? It's parked, but act like it isn't.",
                "1:Hi! I'm gifting shout-outs today. Shout-out to you. You're welcome."},
            new[]{ // Burner
                "Welcome home. Here, have a hug. It's a gift, no receipt.",
                "Hey, neighbor. Want to join a spontaneous parade? So far it's just me.",
                "Oh, hi. I made this necklace out of a spoon and a promise.",
                "Greetings. I've been radically self-reliant for nine hours and I'd love some help.",
                "Hey. I'm practicing immediacy. This is it. This is the immediacy.",
                "Hi. Want a sticker? It has no words. That's the point.",
                "Hey there. I'm doing a leave-no-trace sweep. You're a trace.",
                "Oh, hello. I love your outfit. Very bold. Very beige.",
                "Hey! I'm running a hugging workshop. Enrollment is open. Enrollment is you.",
                "Hi. I renamed myself for the weekend. It's a sound. You can't spell it.",
                "0:Nice polo field. Could really use a forty-foot flaming octopus.",
                "0:Hi. I've been gifting at a VIP festival all day. Nobody knows what to do with me.",
                "0:I tried to build a shade dome, but security says it's 'a tent' and 'not allowed'.",
                "1:Welcome home! First time on the playa? Get dusty. Get so dusty.",
                "1:Hey! Want to see my art car? It's this bike with a lampshade on it.",
                "1:I've been staring at the Man for an hour. He hasn't blinked either."},
            new[]{ // Raver
                "Hi! Wanna trade kandi? I only have one left, and it just says 'BASS'.",
                "Bestie! I don't know you. Bestie!",
                "Heyyy! Your vibe is like ninety BPM. Let's get that up.",
                "Hi! Hug? Hug. Okay, now we're family.",
                "Where's your totem? You don't have a totem? Be my totem.",
                "Hey! I've been dancing since sunrise. Whose sunrise? Unclear.",
                "Fam! Did you feel that drop? My knees felt that drop.",
                "Hi! I've got glitter, water and absolutely no sense of direction.",
                "Hey! I'm on my fourth wind. The third one left with my shoes.",
                "Oh, hi! Your hat is doing the thing. You know the thing. The good thing.",
                "0:Wanna watch the headliner with me? I'm going to cry. It's fine.",
                "0:Hey! I've been doing laps of the Ferris wheel line for the step count.",
                "0:Hiii! I traded kandi for a sip of iced matcha and my heart is going so fast.",
                "1:Hey! That art car had lasers and now I have feelings about it.",
                "1:Hiii! I rave in dust now. The dust is my fog machine.",
                "1:I've been dancing at the Man all night. No rhythm, great presence."},
            new[]{ // DraggedAlongDad
                "Well, hello there. You haven't seen a teenager in a bucket hat? All of them? Right.",
                "Howdy. I've been told this is a 'vibe'. Am I doing it right?",
                "Hey there, sport. Is this the music, or are they still setting up?",
                "Hi. My kid said to 'stay here' four hours ago. I'm staying here.",
                "Hello! Great weather for it. For whatever this is.",
                "Oh, good, an adult. Or close enough. You'll do.",
                "Evening. I brought a folding chair and I'm not afraid to use it.",
                "Hi. I'm the designated driver. I'm also the designated snack bag.",
                "Hey, sport. I've got sunscreen, a whistle and four kinds of granola. Need anything?",
                "Hello! I asked where the quiet stage was. They laughed for a while.",
                "0:Well, howdy. Eighteen dollars for an iced matcha. Eighteen.",
                "0:Hi. I paid for VIP and the only perk is this lanyard. Look at this lanyard.",
                "0:My daughter says the headliner is 'generational'. I've heard two songs and a lot of clapping.",
                "1:Hello. I was told there'd be a 'burn', so I brought aloe.",
                "1:A stranger gifted me a grilled cheese. Is that a trap? It was delicious.",
                "1:Hey, sport. The playa's great, but I've been shaking sand out of my wallet since Tuesday."}
        };
        private static readonly string[][] Questions={
            new[]{ // Small talk
                "How's your weekend going?",
                "What brings you out here?",
                "Having a good time?",
                "How's the {ground} treating you?",
                "Is it me, or is that speaker breathing? Anyway, how are you?",
                "Do you know what day it is? No? Same. How's it going?",
                "Love the outfit. How's your weekend been?",
                "Rate your weekend so far, out of ten.",
                "You look like you're having fun. Are you?",
                "How are you holding up out here?",
                "0:Did you pay eighteen dollars for an iced matcha too? How's the rest of it going?",
                "0:Is that a real VIP wristband? How's the VIP life?",
                "0:Excited for the headliner? How's your day been?",
                "1:Is that dust on your face, or just your face now? Anyway, how's it going?",
                "1:Gifted anything good yet? How's the weekend?",
                "1:How long have you been on the playa? Is it going well?"},
            new[]{ // The sales pitch
                "Are you, uh, in the market for anything sparkly?",
                "Would a Fun Guy improve your evening at all?",
                "What would you pay for a very good mood?",
                "Do you like colors? Like, more colors?",
                "My friend has extra Tongue Stamps and not enough pockets. Any interest?",
                "Is this a good spot to talk about snacks? The special kind?",
                "Quick question: buyer, or just a very nice person?",
                "I've got Tongue Stamps. You've got a face that says 'maybe'. Well?",
                "If someone had Fun Guys, would you be someone who wants Fun Guys?",
                "Can I interest you in a small, colorful business opportunity?",
                "0:Want something stronger than that iced matcha?",
                "0:Does your VIP package include Fun Guys? Because mine could.",
                "0:Can I interest you in a little pre-headliner pick-me-up?",
                "1:Would you accept a Tongue Stamp as a gift? A gift with a price tag?",
                "1:I know it's a gifting economy, but is it also a selling economy?",
                "1:Is anyone on your art car shopping for Fun Guys?"},
            new[]{ // The lost friend
                "Have you seen my friend? Lost, confused, smells like sunscreen.",
                "I'm looking for someone about this tall with a lot of opinions. Seen them?",
                "Did a very lost person come through here?",
                "Seen anyone wandering around like they misplaced a whole weekend?",
                "My friend wandered off an hour ago. Any sightings?",
                "Which way did the confused one go?",
                "Has anyone asked you for directions and then ignored them?",
                "Have you seen someone in our camp colors? It's a very specific teal.",
                "Did someone come by here asking where 'here' is?",
                "Seen anyone who looks like they need a friend? I'm the friend.",
                "0:Did someone ride past on the Ferris wheel yelling my name?",
                "0:Has anyone tried to sneak into VIP? Our friend has a history.",
                "0:Did you see someone in a flower crown with no sense of direction?",
                "1:Did anyone fall off an art car around here? Friend-shaped?",
                "1:Seen anyone wandering into the dust like they meant it?",
                "1:Have you seen someone in goggles and absolutely nothing else practical?"}
        };
        private static readonly string[][] Interjections={
            new[]{"Duuude.","Oh, maaan.","Heyyy.","Whoa. Okay. Okay.","Mmm, right on.","Brother.","Far out.","Sister, listen.","Oh, heck yeah.","Hold on, my hair's talking. Okay.","Sorry, I was listening to a tree.","Totally, totally."},
            new[]{"Okay, so, literally.","Not me getting asked this on camera.","Hold on, let me find my angle.","Babe.","Honestly? Iconic question.","Wait, say that again for the story.","Okay, this is giving mystery.","Obsessed with this energy.","Sorry, I'm live right now.","Chat, you're going to love this.","Wait, is my ring light on?","Love that for us."},
            new[]{"Radical honesty time.","Welcome home.","Let me gift you an answer.","Hold on, my goggles are fogging.","Let me hold space for that.","Radical self-expression incoming.","I say this with immediacy.","Let's decommodify this moment.","Hey, neighbor.","Leave no trace, but okay.","Breathe with me. Okay.","Consent check: can I answer? Thanks."},
            new[]{"PLUR, fam.","Bestie!","Bro, that drop earlier. Anyway.","Hold on, trading kandi. Okay.","Oh my gosh, hi!","Vibes check out.","Fam, fam, fam.","Wait, is this song about us?","Bass face. Sorry. Okay.","I love you. I just met you. Anyway.","Hug first. Okay.","Ugh, this bassline. Sorry."},
            new[]{"Well, sport.","Hoo boy.","Let me put my sunglasses on for this one.","Champ, I'll level with you.","Funny you should ask.","Hang on, my fanny pack is buzzing.","Kiddo.","Is this where I say 'groovy'? Groovy.","Let me check with my daughter. She's gone. Okay.","Back in my day we had one stage and it was a truck.","Now, I'm just here for my kid.","I parked in lot G. Remember that for me."}
        };
        // [role][question]: small talk, the sales pitch, the lost friend.
        private static readonly string[][][] Answers={
            new[]{ // Buyer: keen, discreet, a little desperate.
                new[]{
                    "Good, but it still needs a stamp of approval, if you catch my drift.",
                    "Honestly? I'm one Fun Guy short of a perfect weekend.",
                    "I'm here for the music. And a small, colorful purchase.",
                    "Great! My wallet's full and my pockets are tragically empty.",
                    "I budgeted for merch and I am not buying merch.",
                    "Better, if someone around here sells what I think they sell.",
                    "I've been looking for a friendly salesperson for three stages now.",
                    "Fine. Finer with a Tongue Stamp or two.",
                    "Good. Ask me the next question. Ask me the business question.",
                    "Great, but my evening is missing a Fun Guy. Not a guy. A Fun Guy.",
                    "0:I skipped the iced matcha to save room in my budget for something better.",
                    "0:I've got VIP money and general admission feelings.",
                    "0:I promised myself something fun before the headliner. Clock's ticking.",
                    "1:I've gifted everything I own. Now I'd like to receive, commercially.",
                    "1:I've ridden three art cars looking for someone like you.",
                    "1:Dusty. Thirsty. Shopping, discreetly."},
                new[]{
                    "Yes. Quietly yes. Enthusiastically quietly yes.",
                    "I have exact change and zero follow-up questions.",
                    "Keep your voice down and your prices reasonable.",
                    "Is that a Tongue Stamp in your pocket? Please say it's a Tongue Stamp.",
                    "Wonderful. I've been rehearsing a casual reaction all day. Oh. Neat.",
                    "Name your price. Now name a slightly lower price.",
                    "I'm a Fun Guys loyalist, but I'd start a stamp collection for you.",
                    "Oh, thank goodness. I almost asked a cop.",
                    "Yes, but act natural. More natural. Less natural. Perfect.",
                    "I've been waiting all day for someone to ask me that exact question.",
                    "0:I'll trade my VIP lanyard for the right answer to that question.",
                    "0:Can I pay you in iced matcha? No? Cash, then. Fine.",
                    "0:Please. I need something that makes this headliner make sense.",
                    "1:A gift with a price tag? Most honest gift I've been offered all week.",
                    "1:I'll gift you money, you gift me joy, and nobody says the word 'sale'.",
                    "1:Yes. I can pay in the usual or in slightly used glow sticks."},
                new[]{
                    "No idea, but if you find them, bring the extras back here.",
                    "Haven't seen anyone. I've been busy looking for a seller.",
                    "Not a clue. Are they the one holding the Fun Guys? Because I'll help look.",
                    "I haven't looked at a face in hours. I've been scanning pockets.",
                    "Sorry. But there's a finder's fee if they turn up with Tongue Stamps.",
                    "Nope. Everyone here looks lost. Nobody looks like they're selling.",
                    "No, but I'll wait right here in case they come back with inventory.",
                    "I only notice people with business energy. Does your friend have business energy?",
                    "Haven't seen them. Have you seen a seller? We could trade leads.",
                    "No. But if they're selling, I'm their biggest fan and first customer.",
                    "0:No. I've been staking out the headliner crowd. Lots of wallets, no sellers.",
                    "0:Try the VIP side. Everyone over there has lost something and bought something.",
                    "0:No, but everyone at the iced matcha cart is shopping. Just saying.",
                    "1:Out here? Everyone's lost. Few are selling. You're both, apparently.",
                    "1:Nope. I've been chasing an art car with a rumor of Fun Guys on it.",
                    "1:I can't see past the dust, but I can smell a sale. Is that you?"}},
            new[]{ // Narc: an undercover cop doing an impression of a festival-goer. Every line carries a tell.
                new[]{
                    "Excellent, fellow festival-goer. I am enjoying the music at a normal volume.",
                    "Great. I've logged, I mean enjoyed, four sets so far. For my records. My personal records.",
                    "Very normal. I arrived at approximately fourteen hundred hours, like the youths do.",
                    "I'm just a regular civilian enjoying the local rhythms.",
                    "Great weekend. Nothing suspicious in the vicinity. Not that I'm looking.",
                    "Ten-four. I mean, awesome. Radical, even.",
                    "I'm here for the bands. What do the kids call them now? Still bands?",
                    "Copy that. I mean, same to you. Good weekend to you, citizen.",
                    "I'm having fun, which is what I, a civilian, do on weekends.",
                    "Wonderful. I've been proceeding on foot from stage to stage, like a fan.",
                    "0:I've been enjoying the headliner in the vicinity of the stage, as a fan.",
                    "0:This iced matcha is well within legal limits. I checked. For my records.",
                    "0:I'm VIP. It stands for Very Innocent Person, fellow festival-goer.",
                    "1:I arrived on the playa at approximately oh-six-hundred hours. Naturally.",
                    "1:I've been gifting, as is customary among you civilians.",
                    "1:That art car was exceeding the posted speed limit. Not that I care, fellow festival-goer."},
                new[]{
                    "Interesting. And what's the street value of one of those, roughly?",
                    "Hypothetically speaking, where might one acquire several of them?",
                    "I would like to purchase one illicit, I mean, one fun item, please.",
                    "Is that a controlled substance? Asking as a fun person.",
                    "Yes. For my records, please state the quantity clearly, toward my shirt pocket.",
                    "What do the kids call those these days? Say it slowly. And clearly.",
                    "Ten-four. I mean, yes. I would enjoy that, fellow festival-goer.",
                    "And who is your supplier? Just curious. As a civilian.",
                    "Copy that. How many units are currently in your possession?",
                    "Absolutely. Could you repeat the price for the youths in the back?",
                    "0:Before the headliner, I'd like to acquire one. What's the street value?",
                    "0:Does the VIP area have more of these? How many? Which tent? For my records.",
                    "0:Would you accept payment in iced matcha? Hypothetically speaking?",
                    "1:A gift, you say. And what's the street value of a gift, roughly?",
                    "1:Is that art car transporting additional units? Asking as a civilian.",
                    "1:Excellent. And where might one acquire more, out here on the playa?"},
                new[]{
                    "Can you describe the suspect? I mean, your friend.",
                    "When was your friend last seen, and were they proceeding on foot?",
                    "I haven't seen them, but I'll need their full name for my records.",
                    "Negative. No persons of interest in the vicinity.",
                    "Copy that. I'll put out a, uh, friendly vibe for them.",
                    "Did the perpetrator, uh, your pal, say where they were headed?",
                    "Ten-four. Was your friend carrying anything? Anything at all? Itemize it.",
                    "No, fellow festival-goer, but I'd love to know what they had on them.",
                    "Missing youths are very concerning to me, a normal civilian.",
                    "Not since approximately twenty-one hundred hours. Not that I was watching.",
                    "0:Negative. But if they turn up on the Ferris wheel, I'll ask them to exit the vehicle slowly.",
                    "0:Did they have a VIP wristband? What's the street value of one of those?",
                    "0:No. I've been monitoring the iced matcha line for illicit activity. For fun.",
                    "1:Nobody's proceeding on foot out here. The suspect probably took an art car.",
                    "1:Negative. Visibility in the dust is poor, fellow festival-goer. Which is fine. For festivals.",
                    "1:If they're on an art car, tell them to exit the vehicle. Just a friendly civilian tip."}},
            new[]{ // Regular: happily oblivious; nothing to sell, nothing to tell.
                new[]{
                    "Amazing. I've lost my shoes, my friends and my sense of time, in that order.",
                    "I came for one band and accidentally joined a drum circle. I'm the drum now.",
                    "Great! I've been dancing for six hours and I've moved about four feet.",
                    "I found a patch of {ground} that really understands me.",
                    "Fantastic. I made a friend, made that friend a bracelet, then lost the friend.",
                    "I'm doing great. My phone died, so now I live here.",
                    "Honestly? Best time ever, and I have no idea why.",
                    "I'm vibing. Aggressively vibing. I can't stop now, I've come too far.",
                    "Good! I've been in the bathroom line so long I've made lifelong friends.",
                    "Wonderful. I've been following a flag all day. I don't know whose. Great flag.",
                    "0:Great. I spent my entire budget on one iced matcha and I regret nothing.",
                    "0:I've ridden the Ferris wheel nine times. I live up there now.",
                    "0:I'm not VIP, but I've stood next to the VIP fence all day, which is basically VIP.",
                    "1:I've been on the playa nine days. I think. Days are a rumor.",
                    "1:I followed an art car shaped like a snail. It was slow. It was worth it.",
                    "1:I've got dust in places I didn't know I had places.",
                    "1:I gifted a stranger my hat and got a pickle. Great trade."},
                new[]{
                    "Sparkly? I'm already wearing forty grams of glitter. I'm at capacity.",
                    "No thanks, I'm on a strict diet of vibes and electrolytes.",
                    "Is that like a raffle? I love raffles. I never win raffles.",
                    "I don't know what that is, but I'd love a sticker of it.",
                    "I'll pass. The last time I tried something new here, I joined a choir.",
                    "No thanks. My mom packed me a sandwich and I'm committed to it.",
                    "More colors? I'm seeing plenty. Most of them are sunscreen.",
                    "Sorry, the speaker ate half of that. Yes to the vibe, no to whatever it was.",
                    "Oh, I don't buy stuff at festivals. I just stand near stuff.",
                    "No thanks. I'm saving up for a hat shaped like a bigger hat.",
                    "0:No thanks, I've had three iced matchas. I can hear colors already.",
                    "0:Is it VIP? If it's not VIP, I'm not allowed to want it.",
                    "0:I'm saving myself for the headliner. Emotionally and financially.",
                    "1:Selling? Out here? Someone should tell the gifting council.",
                    "1:No thanks. Would you like a hand-knit dust mask, though? It's a gift.",
                    "1:I don't buy things. I haven't seen money since the second dust storm."},
                new[]{
                    "Haven't seen anyone. I've been staring at the same speaker for an hour.",
                    "Everybody looks lost to me. That's kind of the point, right?",
                    "Nope. But if you find mine, send them back too.",
                    "I saw a lot of people. None of them were yours, probably.",
                    "Sorry, I've been looking at the sky. It's doing a lot tonight.",
                    "Your friend? No. My friend? Also no. We should start a club.",
                    "I thought I saw someone, but it was a flag.",
                    "A person in a hat? That describes everyone here. Sorry.",
                    "I don't keep track of people. I barely keep track of me.",
                    "No, but a guy in a dinosaur suit asked me the same thing. Maybe ask him.",
                    "0:No. I've been guarding my spot for the headliner since breakfast.",
                    "0:Everyone near the Ferris wheel looks lost. It's the spinning.",
                    "0:I only notice people holding iced matcha. Were they holding iced matcha?",
                    "1:Out here, everyone's somebody's lost friend. Welcome home.",
                    "1:I haven't seen past my own goggles since the last dust storm.",
                    "1:No, but an art car just went by playing whale sounds. Could be related."}},
            new[]{ // ClueHolder: saw the lost friend; the last answer names a landmark.
                new[]{
                    "Weird, actually. Somebody came through here looking really lost.",
                    "Pretty good. I've been helping a confused person look for their crew. Could be yours.",
                    "Eventful. Someone just asked me which way 'away' was.",
                    "Great, except a very lost person borrowed my water and my sense of purpose.",
                    "I've been people-watching. One person in particular was very watchable.",
                    "Fine! I've been giving directions all day. Mostly correct ones.",
                    "Honestly, I was hoping someone would come asking about your friend.",
                    "I'm having a great time. Your friend didn't look like they were.",
                    "Good. Somebody in your camp colors high-fived me and ran off. Rude, but effective.",
                    "Oh, you're with the lost one, aren't you? Same worried eyebrows.",
                    "0:Good! Your friend tried to pay for my iced matcha with a wristband.",
                    "0:Busy. Someone kept asking where the headliner was. It was on the stage. Loudly.",
                    "0:Nice. A lost person asked me if the Ferris wheel was 'going anywhere'. Then they did.",
                    "1:Dusty. Someone in your camp colors walked straight into it.",
                    "1:Good. A lost person gifted me a single sock and ran off.",
                    "1:Great. Somebody asked me if the Man was their ride home."},
                new[]{
                    "No thanks. Your friend already tried to sell me some. They were very bad at it.",
                    "Not buying. But I know which way your friend went, if that's worth anything.",
                    "No, but that reminds me of someone. Someone lost.",
                    "I'm not shopping. I'm just the person who knows things tonight.",
                    "I'll pass, but I'll trade you a sighting for a smile.",
                    "No thanks. Ask me the other question. The one about your friend.",
                    "Save it. You'll want your pockets full when you find your pal.",
                    "Not my thing. Finding your friend might be, though.",
                    "No, thanks. Funny, somebody asked me the same thing an hour ago, then got lost.",
                    "I'm good. Your friend's the one who needs something. Mostly directions.",
                    "0:No thanks. Your friend offered me one by the VIP fence, then wandered off.",
                    "0:Save them for your friend. They looked like they needed a treat and a map.",
                    "0:I'm just here for the headliner. But I saw someone who looked like you, only lost.",
                    "1:No, but your friend gifted me a Fun Guy and wandered into the dust.",
                    "1:I don't need one. I need you to find your friend before the Man burns.",
                    "1:No thanks, I'm gifting tonight, not buying. Your friend tried gifting too. Mostly confusion."},
                new[]{
                    "Yes! They went {where}, walking like the ground owed them money.",
                    "Last I saw them, they were heading {where}, humming the wrong song.",
                    "They asked me for directions, then went {where} instead.",
                    "Oh, them? They went {where}, following a balloon.",
                    "They were here ten minutes ago. Headed {where}. Very confident. Very wrong.",
                    "I pointed them {where}. They waved at my finger and went.",
                    "Your friend? Definitely went {where}. Might still be explaining something to a bin.",
                    "Yeah. They went {where}. Tell them I want my sunglasses back.",
                    "They sprinted {where} shouting 'I've got it!' They did not have it.",
                    "Right, the lost one. Went {where}, holding someone else's snack.",
                    "0:They tried to get into VIP, got turned around and went {where}.",
                    "0:They asked the Ferris wheel for directions. Then they went {where}.",
                    "0:They said they'd 'find the headliner themselves' and went {where}.",
                    "1:They hitched an art car for about ten feet, then walked {where}.",
                    "1:They said 'welcome home' to me and wandered {where}.",
                    "1:They followed a bike bell {where}. Good instincts, honestly."}}
        };
    }
}
