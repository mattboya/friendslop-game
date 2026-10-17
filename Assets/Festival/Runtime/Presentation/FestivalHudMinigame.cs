using System;
using Festival.Core;

namespace Festival.Presentation
{
    /// <summary>MINI-1 (docs/MINIGAMES_2026-09-30.md): the minigame booth's lines on the HUD, from the viewer's round state: the card
    /// under the aim dot (FestivalHud's minigame card) and what E does while a game runs. MINI-4: Mystery Cups' own lines.</summary>
    public static partial class FestivalHudText
    {
        /// <summary>MINI-1: the minigame card's title and line, or ("","") for none. Standing alive in the booth's ring: how many of
        /// the connected living crew are in it, who keeps it from starting (down or busy, anywhere), then the countdown to the game.
        /// While the game runs, for everyone: the festival is paused, and for a player in it how long the game has left; then, for its
        /// banner's seconds, what its result did to the clock and when the festival starts again. A game with lines of its own
        /// (MINI-4's Mystery Cups) shows those instead while it runs.</summary>
        public static (string Title,string Detail) MinigameCard(RoundState s,PlayerState p)
        {
            var b=s.Booth;
            if(s.Phase==FestivalSimulation.MinigamePhase)
            {
                if(b.Kind==FestivalSimulation.MysteryCupsGame)return CupsCard(s,p);
                if(b.ScoredAt>=0)return (ClockChange(b.ClockChange),(b.ClockChange>0?"Added to the clock.":b.ClockChange<0?"Taken off the clock.":"The clock is unchanged.")
                    +" The festival starts again in "+WholeSeconds(b.ScoredAt+FestivalSimulation.MinigameBannerSeconds-b.Seconds)+".");
                return ("MINIGAME",b.PlayerIds.Contains(p.Id)?"The festival is paused while you play: "+WholeSeconds(FestivalSimulation.MinigameMaxSeconds-b.Seconds)+" s left.":"The festival is paused while the crew plays.");
            }
            if(s.Phase!="Playing"||p.Life!="Alive"||!FestivalSimulation.InRing(s,p))return ("","");
            if(b.RingSeconds>0)return ("MINIGAME IN "+WholeSeconds(FestivalSimulation.MinigameRingSeconds-b.RingSeconds),"Stay in the ring. The clock stops while you play.");
            var holding=FestivalSimulation.HoldsBooth(s);
            if(holding!=null)return ("MINIGAME BOOTH","It can't start while "+(holding.Id==p.Id?"you're":holding.Name+" is")+(holding.Life=="Downed"?" down.":" busy."));
            int crew=0,inRing=0;
            foreach(var x in s.Players)if(x.Connected&&x.Life=="Alive"){crew++;if(FestivalSimulation.InRing(s,x))inRing++;}
            return ("MINIGAME BOOTH",inRing+" / "+crew+" in the ring. Everyone alive steps in to play, and the clock stops.");
        }

        /// <summary>MINI-4: Mystery Cups on the card. While it runs: the pot, who is up and their seconds, what the next safe cup adds,
        /// and the cup the tripper's hunch marks while it is on the table (only the tripper's view carries it). Then what became of the
        /// pot: lost to the spiked cup, split across the crew by day with the odd amount to the stash, or banked by night; and when
        /// the festival starts again.</summary>
        static (string Title,string Detail) CupsCard(RoundState s,PlayerState p)
        {
            var b=s.Booth;
            if(b.ScoredAt>=0)
            {
                string again=" The festival starts again in "+WholeSeconds(b.ScoredAt+FestivalSimulation.MinigameBannerSeconds-b.Seconds)+".";
                if(b.SpikedBy!="")return ("SPIKED!",(b.SpikedBy==p.Id?"You":NameOf(s,b.SpikedBy))+" drank the spiked cup"+(b.Pot>0?" and lost the "+Money(s,p,b.Pot)+" pot.":".")+again);
                string paid=b.Pot==0?"Called it with nothing in the pot."
                    :Festivals.For(s).Night?"Called it: "+Money(s,p,b.Pot)+" to the stash."
                    :DayPayout(s,p,b)+", counted as sales.";
                return ("+"+Money(s,p,b.Pot),paid+again);
            }
            string up=FestivalSimulation.CupsTurn(s),seconds=WholeSeconds(b.TurnAt+FestivalSimulation.CupTurnSeconds-b.Seconds)+" s",next=Money(s,p,FestivalSimulation.NextCupPays(b));
            string hunch=b.Hint>=0&&!b.Drunk.Contains(b.Hint)?" Your hunch: cup "+(b.Hint+1)+" is spiked.":"";
            return ((MinigameName(b.Kind)+Dot+"POT "+Money(s,p,b.Pot)).ToUpperInvariant(),
                (up==p.Id?"Your turn: "+seconds+". Drink a cup for "+next+", or call it. One is spiked.":NameOf(s,up)+"'s turn: "+seconds+". A safe cup adds "+next+"; the spiked one loses the pot.")+hunch);
        }
        /// <summary>A game's name as its booth sign and card read it; "MINIGAME" for one this build cannot name.</summary>
        public static string MinigameName(string kind)=>kind==FestivalSimulation.MysteryCupsGame?"MYSTERY CUPS":kind==FestivalSimulation.DrumCircleGame?"DRUM CIRCLE":kind==FestivalSimulation.LightShowGame?"LIGHT SHOW":"MINIGAME";
        // The day's split as MysteryCups.CallIt pays it: an even share to each player, the odd amount to the stash.
        static string DayPayout(RoundState s,PlayerState p,MinigameState b)
        {
            int crew=Math.Max(1,b.PlayerIds.Count),share=b.Pot/crew,stash=b.Pot-share*crew;
            if(share==0)return "Called it: the whole "+Money(s,p,b.Pot)+" pot goes to the stash";
            return "Called it: "+Money(s,p,share)+" each"+(stash>0?", "+Money(s,p,stash)+" to the stash":"");
        }
        // A player's name as the view has it, else as the game captured it at its start: a spirit's view lists only spirits.
        static string NameOf(RoundState s,string id)=>s.Players.Find(x=>x.Id==id)?.Name??s.Booth.Participants?.Find(x=>x.Id==id)?.Name??"A friend";
        /// <summary>Whether the minigame card's result reads as a win (mint, not orange): scored, nothing taken off the clock, and in
        /// Mystery Cups no spiked cup drunk.</summary>
        public static bool MinigameWon(RoundState s)=>s.Phase==FestivalSimulation.MinigamePhase&&s.Booth.ScoredAt>=0&&s.Booth.ClockChange>=0&&s.Booth.SpikedBy=="";

        /// <summary>MINI-1: a game's result on the clock, as its banner reads it: "+45 s", "−30 s", "+0 s".</summary>
        static string ClockChange(double seconds){int whole=(int)Math.Round(Math.Abs(seconds));return (seconds<0&&whole>0?"−":"+")+whole+" s";}

        /// <summary>The keys while a game runs, for the player whose move it is until it is scored; "" for anyone else, once it is scored,
        /// and in a game with no moves of its own. MINI-4, Mystery Cups: E drinks `cup`, the one under the aim dot
        /// (FestivalMinigameView.AimedCup, -1 none), and F calls it.</summary>
        public static string MinigamePrompt(RoundState s,PlayerState p,int cup)=>FestivalSimulation.CupsTurn(s)!=p.Id?""
            :(cup>=0?"E  DRINK CUP "+(cup+1):"LOOK AT A CUP TO DRINK IT")+"   •   F CALL IT";

        // Seconds left, in whole seconds rounded up, as the clock counts them.
        static int WholeSeconds(double seconds)=>(int)Math.Ceiling(Math.Max(0,seconds));
    }
}
