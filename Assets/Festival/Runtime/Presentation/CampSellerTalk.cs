using System.Collections.Generic;
using Festival.Core;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>
    /// CAMP-1 (Matt, 2026-10-01: "i should be able to talk to the camp vendor."): what Marigold, the camp seller, says. She is a
    /// weathered festival regular who has seen it all, a bit too relaxed and always helpful (the entry's decision A). Asked
    /// "What should I grab?" she answers for the viewer's next level from their own hands and her shelves (Grab); "What's today
    /// like?" names that level, its quota in the viewer's money and its festival's twists (Today); "Any tips?" draws one true tip,
    /// never the one she gave last (Tip). Her greetings and sign-offs are built on DIALOG-4's LineGrammar. Everything reads only
    /// the view the client already has, and none of it changes the game.
    /// The talk itself runs on DIALOG-5's chat window, on the client alone (FestivalHud): beside her at camp F opens it (Begin),
    /// 1-3 ask, Escape closes it (End), walking off ends it on her sign-off (Follow), and she faces you while you talk (Face). The host
    /// never hears of it, so no other player sees her turn.
    /// </summary>
    public sealed class CampSellerTalk
    {
        public const string Name="Marigold";
        public static readonly string[] Questions={"What should I grab?","What's today like?","Any tips?"};
        readonly ContentRandom random;
        string lastTip="";
        /// <summary>A talker whose greetings, sign-offs and tips are drawn from `seed`'s stream.</summary>
        public CampSellerTalk(int seed){random=new ContentRandom(seed);}

        // What the HUD shows beside her (the strip under the prompt), her talk's title and the initial on her bubbles. The title says
        // Escape closes it, as an ended chat's does (FestivalChatWindow.OverHeader).
        public const string TalkPrompt="F  TALK TO MARIGOLD",Header="MARIGOLD  •  CAMP SUPPLIES  •  ESC CLOSE",Mark="M";
        // Where she stands, in front of the supplies table (FestivalWorld puts her there), and which way she faces when nobody talks
        // to her: the camp. F reaches her from as far as the checkout takes payment (Economy.cs: 2.7 m from 0, 7). She turns to
        // face whoever talks to her at TurnDegreesPerSecond.
        public const float X=0,Z=7,RestYaw=180,Reach=2.7f,TurnDegreesPerSecond=240;
        /// <summary>Whether a talk with her is under way: 1-3 ask her questions, Escape closes it.</summary>
        public bool Open{get;private set;}
        public bool Visible=>Open||lingering;
        // An ended talk still up on the window with her sign-off; which talk this is, for the window's thread; the window it is on,
        // and the view and player her answers read, as of this frame.
        bool lingering;int talks;string thread="";FestivalChatWindow window;RoundState state;PlayerState player;

        /// <summary>Whether p stands beside her at camp: within Reach, out in the open (not inside a tent, car or potty). F there is
        /// hers, never the host's festival switch.</summary>
        public static bool Beside(RoundState s,PlayerState p)=>s.Phase=="Shopping"&&p.CampVisitId==""&&Within(p,Reach);
        /// <summary>Whether F talks to her: beside her with nothing unpaid in hand, which shows the pay prompt instead.</summary>
        public static bool CanTalk(RoundState s,PlayerState p)=>Beside(s,p)&&p.HeldOfferId=="";
        static bool Within(PlayerState p,float range){float x=p.X-X,z=p.Z-Z;return x*x+z*z<=range*range;}

        /// <summary>F beside her: the window opens on a new thread, her greeting typed out, and her three questions to ask with 1-3.
        /// Each one asked says it on your side and types her answer, from the view as it is then; all three stay to ask again.</summary>
        public void Begin(FestivalChatWindow window,RoundState s,PlayerState p,float now)
        {
            this.window=window;state=s;player=p;Open=true;lingering=false;thread="Marigold "+(++talks);
            window.Begin(thread,Header.Replace("ESC CLOSE",window.CloseKey+" CLOSE"),Mark,now);
            window.TheySay(Greeting(s.FestivalIndex),true);
            Ask();
        }
        void Ask()=>window.Offer(Questions,Asked);
        void Asked(int i){window.YouSay(Questions[i]);window.TheySay(i==0?Grab(state,player):i==1?Today(state,player):Tip(state,player),true);Ask();}

        /// <summary>
        /// Each frame at `now`: keeps her talk on the window while p stays within FestivalChatWindow.LeaveReach of her, the reach at
        /// which the host ends a chat. Walking off past it ends the talk on her sign-off, which stays up read-only until dismissed
        /// (FestivalChatWindow.Linger); the spin starting, a new purchase/action or going inside a tent, car or potty closes it at once. False once nothing
        /// of the talk is up, which leaves the window to the host's chats (FestivalChatWindow.Follow).
        /// </summary>
        public bool Follow(RoundState s,PlayerState p,float now,bool reducedMotion)
        {
            bool gone=s.Phase!="Shopping"||p.CampVisitId!=""||p.HeldOfferId!=""||p.InteractionId!="";
            if(lingering)return lingering=window.Linger(now,reducedMotion,gone);
            if(!Open)return false;
            if(gone){End();return false;}
            state=s;player=p;
            if(!Within(p,FestivalChatWindow.LeaveReach)){Open=false;window.TheySay(SignOff(s.FestivalIndex),true);return lingering=window.Linger(now,reducedMotion,false);}
            window.Begin(thread,Header.Replace("ESC CLOSE",window.CloseKey+" CLOSE"),Mark,now);window.Advance(now,1,reducedMotion);
            return true;
        }
        /// <summary>Closes her talk at once, with no sign-off (Escape, or the session going).</summary>
        public void End(){if(Open||lingering)window.Dismiss();Open=lingering=false;}

        /// <summary>Each frame: while you talk to her, or her sign-off is still up, she turns to face p (TurnDegreesPerSecond, dt
        /// seconds) in the Talk pose; otherwise she turns back to face the camp (RestYaw) and idles.</summary>
        public void Face(FestivalCharacter seller,PlayerState p,float dt)
        {
            bool talking=Open||lingering;var toYou=new Vector3(p.X-X,0,p.Z-Z);
            var aim=talking&&toYou.sqrMagnitude>.01f?Quaternion.LookRotation(toYou):Quaternion.Euler(0,RestYaw,0);
            var turn=seller.transform;turn.localRotation=Quaternion.RotateTowards(turn.localRotation,aim,TurnDegreesPerSecond*dt);
            seller.Pose=talking?"Conversation":"Idle";
        }

        /// <summary>"What should I grab?" for p's next level. Before a day with no stock in hand: something to sell, both kinds at
        /// their price in p's money, one paid camp starter plus more from festivalgoers, some of them narcs. Nights explain the rescue
        /// and retain the camp allowance status. Then the first three extras on her shelf that are left, and why. Then whatever on it is sold out.</summary>
        public static string Grab(RoundState s,PlayerState p)
        {
            bool night=Festivals.For(s).Night,needsStock=FestivalGuidance.NeedsStock(s,p);
            string line=(night?"No selling tonight, hon: rescue your missing friend and bring them home. "
                :needsStock?"Start here, sugar. ":"You're stocked up, sugar. ")
                +FestivalHudText.CampStockGuidance(s,p).Replace("One starter each","I sell one starter each")
                +(night||!needsStock?Extras(s):"");
            var soldOut=new List<string>();
            foreach(var id in s.VendorOffers)if((s.ShopStock.Find(x=>x.ItemId==id)?.CampAvailable??0)<=0)soldOut.Add(Catalog.FindItem(id).Name);
            return soldOut.Count==0?line:line+" Sold out: "+string.Join(", ",soldOut)+".";
        }
        // Her pitch for each shelf item, most useful first.
        static readonly (string Id,string Pitch)[] Pitches={
            ("merch_bag","a merch bag hides your stock from security"),
            ("medical_voucher","a med voucher clears your head at the medical tent"),
            ("poi_practice","practice poi make the dances easier"),
            ("poi_led","LED poi calm a crowd faster"),
            ("confetti","a confetti cannon turns heads away from you for a few seconds"),
            ("stash_box","a stash box keeps the crew's cash and stock"),
            ("stage_pass","a stage pass gets you on the decks at the main stage"),
            ("little_spoon","the little spoon makes the crowd trust you a touch more"),
        };
        // " From my shelf: a, b and c." for the first three pitches on today's shelf with some left; "" with none.
        static string Extras(RoundState s)
        {
            var pitches=new List<string>();
            foreach(var (id,pitch) in Pitches)if(pitches.Count<3&&s.VendorOffers.Contains(id)&&(s.ShopStock.Find(x=>x.ItemId==id)?.CampAvailable??0)>0)pitches.Add(pitch);
            if(pitches.Count==0)return "";
            string last=pitches[pitches.Count-1];pitches.RemoveAt(pitches.Count-1);
            return " From my shelf: "+(pitches.Count==0?last:string.Join(", ",pitches)+" and "+last)+".";
        }

        /// <summary>"What's today like?": p's next level by festival and name, its job (a day's crew quota in p's money, a night's
        /// rescue, Night 2's everyone home), and that festival's twists in plain words.</summary>
        public static string Today(RoundState s,PlayerState p)
        {
            var level=Festivals.For(s);bool finale=s.LevelIndex==Festivals.LevelCount-1;
            string job=!level.Night?"the crew has to sell "+FestivalHudText.Money(s,p,FestivalSimulation.DayQuota(s))+" before sundown."
                :finale?"the big one. Rescue whoever's missing, then get every one of you home, carrying anyone who falls."
                :"no selling, just the tripper's clue trail to whoever's gone missing, and getting them back to camp.";
            string twists=s.FestivalIndex==Festivals.PoloFestival
                ?"Influencers film the crowd, and being in their shot gets folks suspicious. Buyers past the VIP ropes pay double, and the Ferris wheel shows you every cop on the field."
                :"Your money comes out as odd stuff, dust storms cut everyone's sight to "+Festivals.DustStormSightRange+" m, and the art cars take riders."+(finale?" "+Effigy:"");
            return Festivals.Name(s.FestivalIndex)+", "+level.Name+": "+job+" "+twists;
        }
        static readonly string Effigy="The effigy burns for the last "+(int)(Festivals.BurnSeconds/60)+" minutes, and the whole crowd packs in round it.";

        /// <summary>"Any tips?": one tip for p's festival, never the one she gave last, any money in it in p's money.</summary>
        public string Tip(RoundState s,PlayerState p)
        {
            string tip;
            do tip=Lines.Line("tip",s.FestivalIndex,random);while(tip==lastTip);
            lastTip=tip;
            return FestivalHudText.MoneyText(s,p,tip);
        }
        /// <summary>How she greets you as a talk opens.</summary>
        public string Greeting(int festival)=>Lines.Line("greeting",festival,random);
        /// <summary>What she calls after you as you walk off.</summary>
        public string SignOff(int festival)=>Lines.Line("signoff",festival,random);

        // Every line she says, by LineGrammar's rules: "0:" is Palm Mirage only, "1:" Ember Playa only. Each tip is true of the game
        // as it plays (ECON-2's festivalgoer stock and narcs, DIALOG-3's talks, VISION-5's verdicts, ESC-2's stops, MUSIC-2's tracks).
        static readonly LineGrammar Lines=new LineGrammar(new Dictionary<string,string[]>
        {
            ["greeting"]=new[]{"{~hello} {who} {offer}"},
            ["hello"]=new[]{"Well, hey there.","Look who wandered in.","Howdy, sunshine.","Oh hey, you made it.","Welcome back, hon.","Mornin'. Or evenin'. Who's counting?",
                "Hey hey, kiddo.","There's my favorite customer.","Hi, sugar.","Well, aren't you a sight.","0:Hot one today, huh?","1:Hey there, dusty."},
            ["who"]=new[]{"Marigold's the name, and camp supplies are my game.","Marigold here, running this table longer than you've been alive.",
                "I'm Marigold. Twenty-odd summers of festivals, and I still love it.","Marigold. I've seen it all, twice.","It's me, Marigold, same as ever.",
                "Marigold, at your service, more or less.","0:Marigold here, working on my Palm Mirage tan.","1:Marigold here, still finding playa dust in my socks."},
            ["offer"]=new[]{"What can I do you for?","Ask me anything.","Go on, ask away.","What do you need?","Need a hand?","What'll it be?",
                "Questions? I've got answers.","Take your time, nobody's rushing."},
            ["signoff"]=new[]{"{~bye} {parting}"},
            ["bye"]=new[]{"Alright then.","Off you go.","See ya, hon.","Bye now.","Take care, sugar.","Go get 'em."},
            ["parting"]=new[]{"Drink some water.","Stay out of trouble. Or at least out of sight.","Don't do anything I wouldn't do. Which isn't much.","Have fun out there.",
                "Bring everybody home.","Come back and tell me everything.","Mind the narcs.","0:Smile for the influencers. Or don't.","1:Keep your goggles handy."},
            ["tip"]=new[]{
                "Buy more stock from festivalgoers for $5 each. Some of them are narcs, and finishing a buy with a narc gets you busted.",
                "My shelves sell gear and one starter each at camp: Tongue Stamps or Fun Guys for $5. Keep some money for more stock from festivalgoers.",
                "Talk to folks before you deal. Answer the way they like to be talked to, and a smooth talk leaves you a hunch about who they are.",
                "Somebody wary of you wants a dance before they'll talk, and so do some ravers and burners. Dance it well and they open right up.",
                "Chat with folks before you buy, and listen close: narcs slip into cop talk.",
                "Only the tripper sees the visions, and some of them lie. They check one by chatting or dancing with that person.",
                "A checked vision gets a green ring if it's true and a red one if it's fake. Aim at it and it says what it found, like "+FestivalVisionMarkers.VerdictText("Narc",false)+".",
                "If security stops you over your stock, talk them down, ditch the stock, or walk out of their sight. Stand there and they'll arrest you.",
                "A merch bag keeps your stock out of security's sight. Best $"+Catalog.FindItem("merch_bag").Price+" you'll spend.",
                "When the crew dances together, the worst dancer near the crowd sets everyone's suspicion. Practice, kids.",
                "Don't sprint where folks can see you. Nothing looks guiltier.",
                "Somebody gone zombie-green is onto you. Give them some room.",
                "If a crowd swarms you, run. Their hits can knock you flat.",
                "Nights are rescues. The tripper sees the clue trail to whoever's missing, so stick close and follow their lead.",
                "On Night 2 everybody goes home. One of you can drag the first body back alone, but every other one takes two carriers.",
                "A downed friend can be rescued or dragged clear. A spirit has to walk to the medical tent.",
                "The camp DJ takes requests: "+Tracks()+". Walk on up.",
                "Sometimes there's a Giggle Tank hidden at the festival. It takes a free hand: carry it home to the camp gate and the crew stash gets $"+Festivals.GiggleTankCash+". Get arrested or die on the way and it's gone.",
                "Lose a level and the whole festival starts over at Day 1. Win, and your cash and gear come with you.",
                "At the campfire you vote on each other's silly awards, and the worst of you take a shot.",
                "The little spoon's a necklace. Wear one and the crowd trusts you a touch more.",
                "Heavier doses make more of the tripper's visions lie, but they show secrets too: a hidden stash, or a buyer who pays double.",
                "The Giggle Balloons wear off before the festival. Have a laugh.",
                "Ready up at the lit trailhead when you're set. The crew heads out together.",
                "0:Influencers film the crowd here. Stay out of their shot, or everyone around you gets suspicious.",
                "0:Buyers past the VIP ropes pay double. Get a wristband at the night market, or talk your way past the guard.",
                "0:Ride the Ferris wheel and you'll spot every cop on the field, and at night roughly where your missing friend is. You're stuck up there for the whole turn, mind.",
                "1:Out on the playa your money comes out as odd stuff: buttons, ramen packets, bottle caps. It spends the same.",
                "1:When a dust storm blows through, nobody sees past "+Festivals.DustStormSightRange+" m. Good cover, if you're quick.",
                "1:The art cars crawl round the playa. Climb aboard for a ride.",
                "1:On Night 2 the effigy burns for the last "+(int)(Festivals.BurnSeconds/60)+" minutes, and the whole crowd packs in round it.",
            },
        });
        // MUSIC-2's tracks on the camp DJ, as she'd say them: "tech house, disco house, … and dub reggae".
        static string Tracks()
        {
            var names=new List<string>();for(int i=1;i<CampFeatures.Tracks.Length;i++)names.Add(CampFeatures.Tracks[i].ToLowerInvariant());
            string last=names[names.Count-1];names.RemoveAt(names.Count-1);
            return string.Join(", ",names)+" and "+last;
        }
    }
}
