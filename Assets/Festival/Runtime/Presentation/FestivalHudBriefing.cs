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

        // The view's player with id, or null. Its own method, so Briefing makes no closure on the frames it says nothing.
        static PlayerState Crew(RoundState s,string id)=>s.Players.Find(x=>x.Id==id);
    }
}
