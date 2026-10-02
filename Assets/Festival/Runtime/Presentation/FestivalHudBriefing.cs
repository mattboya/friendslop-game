using System.Collections.Generic;
using Festival.Core;

namespace Festival.Presentation
{
    // HUD-7 (Matt: "if i had never played the game, it would be very unclear what i am supposed to do and what the objective is"):
    // the text that tells a first-time player what the weekend is, what this level asks and what their own job is. Built only
    // from the viewer's filtered round state, like the rest of FestivalHudText; FestivalHud puts it on screen.
    public static partial class FestivalHudText
    {
        /// <summary>How long the level briefing stays up as each level starts, in level clock (a key hides it sooner). The tripper's
        /// TrustLine window: the briefing folds that line in.</summary>
        public const double BriefingSeconds=10;

        /// <summary>
        /// The level briefing, three lines for the first BriefingSeconds of play: the goal with real numbers (the crew's quota in the
        /// viewer's money by day, the rescue by night, two friends for a big crew), the viewer's job (the tripper's carries the game's
        /// one hint, "Trust, but verify."; anyone else is told whose lead to follow), and how the crew loses. "" in every other phase,
        /// after the window, and for anyone not Alive, a spirit above all.
        /// </summary>
        public static string Briefing(RoundState s,string localId)
        {
            if(s.Phase!="Playing"||s.ElapsedSeconds>=BriefingSeconds)return "";
            var p=Crew(s,localId);if(p==null||p.Life!="Alive")return "";
            var level=Festivals.For(s);bool two=s.SecondFriend.Active;
            string goal=!level.Night?"SELL "+Money(s,p,FestivalSimulation.DayQuota(s)).ToUpperInvariant()+" BEFORE SUNDOWN"
                :s.LevelIndex==Festivals.LevelCount-1?(two?"RESCUE BOTH FRIENDS":"RESCUE YOUR FRIEND")+", THEN GET EVERYONE HOME"
                :"FIND YOUR "+(two?"TWO LOST FRIENDS":"LOST FRIEND")+" AND BRING THEM BACK TO CAMP";
            string sees=level.Night?"the clue trail":"who's buying";
            string job=s.TripperId==localId?"You're tripping: only you can see "+sees+". Trust, but verify: some visions lie, so check one with F first."
                :(Crew(s,s.TripperId)?.Name??"A friend")+" is tripping and can see "+sees+". Stay close and act on what they call out.";
            return level.Name.ToUpperInvariant()+Dot+goal+"\n"+job+"\n"+LoseLine;
        }
        // A level is lost at its clock (a day's sundown short of the quota) or once nobody is left standing: a detained friend still
        // counts (FestivalSimulation.LevelEndCheck), so it says "lose the whole crew", never "get caught".
        const string LoseLine="Run out of time or lose the whole crew, and the festival starts over at Day 1.";

        /// <summary>
        /// How to Play, the panel the main menu and the Escape menu open: six short titled blocks. Keys read as bound right now
        /// (FestivalInput.Label), so a rebind shows, and KEYS lists every action in FestivalInput. The tripper's legend and what a
        /// check says it found are VISION-5's own words (FestivalVisionMarkers.ClaimText, VerdictText). Like the rest of the game it
        /// never names a fake's tells: the design's one hint about them is "Trust, but verify." (Briefing).
        /// </summary>
        public static (string Title,string Text)[] HowToPlay(FestivalInput input)
        {
            string act=input.Label(input.Interact),chat=input.Label(input.Chat),drop=input.Label(input.Drop);
            var keys=new List<string>();
            foreach(var action in input.Map.actions)keys.Add((action==input.Look?"Mouse":input.Label(action))+"  "+KeyUse(action.name));
            return new[]
            {
                ("THE WEEKEND","A weekend is four levels: Day 1, Night 1, Day 2, Night 2. By day the crew sells its quota before sundown; by night it"
                    +" finds a lost friend and brings them back to camp. On Night 2 everyone has to make it home, carrying the fallen. Lose a"
                    +" level and the festival starts over at Day 1. Cash and gear carry through the weekend."),
                ("THE TRIPPER","Each level the slot machine picks one friend to trip. Only they see visions: buyers and narcs by day, the clue trail"
                    +" by night, and some of them lie. Check one by chatting ("+chat+") or dancing ("+act+") with them: it gets a green ring if"
                    +" true, a red one if fake, and says what it found, like "+FestivalVisionMarkers.VerdictText("Narc",false)+".\n"
                    +"green ball "+Claim("Buyer")+Dot+"red cube "+Claim("Narc")+Dot+"blue pill "+Claim("Clue")+Dot+"gold drum "+Claim("DoubleBuyer")+" or "+Claim("Stash")),
                ("EVERYONE ELSE","Stick with the tripper and act on what they call out. Buy stock from festivalgoers and offer it to the buyers the"
                    +" tripper spots. Dance together to blend in: the worst dancer near the crowd sets everyone's suspicion."),
                ("DANGER","Some festivalgoers are narcs: buy from or sell to one and you're busted. One who suspects you turns zombie-green; if"
                    +" a crowd swarms you, your card says SWARMING and their hits can down you. Security stops anyone it sees carrying stock"
                    +" (a merch bag hides it): talk ("+act+"), ditch it ("+drop+") or walk away out of its sight. A downed friend can be rescued or"
                    +" dragged; a spirit walks to the medical tent. In an Ember Playa dust storm nobody sees past "+Festivals.DustStormSightRange+" m."),
                ("CAMP","The shelves sell gear only: pick it up ("+act+"), carry it to the seller and pay. At the campfire, vote the silly"
                    +" awards; the worst take a shot. Ready up at the lit trailhead ("+act+")."),
                ("KEYS",string.Join("\n",keys)),
            };
        }
        static string Claim(string kind)=>FestivalVisionMarkers.ClaimText(kind);
        // What each FestivalInput action does, by its name. ponytail: an action added without a line here reads its bare key, which
        // HudBriefingTests catches.
        static string KeyUse(string action)=>action switch
        {
            "Move"=>"move","Look"=>"look around","Sprint"=>"run (the crowd notices)","Interact"=>"the top action on your list",
            "Chat"=>"chat (checks a vision)","Use"=>"use what's in your hand","Drop"=>"drop it • in a stop, ditch your stock • hold to stop dancing",
            "Objectives"=>"hold for the map","Menu"=>"menu and settings","View"=>"first or third person",
            "Slot1"=>"hand 1 • ask 1 in a chat • vote 1 at the campfire","Slot2"=>"hand 2 • ask 2 in a chat • vote 2 at the campfire",
            "Slot3"=>"hand 3 • ask 3 in a chat • vote 3 at the campfire",
            "Rhythm0"=>"dance lane: left","Rhythm1"=>"dance lane: down","Rhythm2"=>"dance lane: up","Rhythm3"=>"dance lane: right",_=>"",
        };

        // The view's player with id, or null. Its own method, so Briefing makes no closure on the frames it says nothing.
        static PlayerState Crew(RoundState s,string id)=>s.Players.Find(x=>x.Id==id);
    }
}
