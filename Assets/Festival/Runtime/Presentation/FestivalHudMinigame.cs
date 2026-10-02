using System;
using Festival.Core;

namespace Festival.Presentation
{
    /// <summary>MINI-1 (docs/MINIGAMES_2026-09-30.md): the minigame booth's lines on the HUD, from the viewer's round state: the card
    /// under the aim dot (FestivalHud's minigame card) and what E does while a game runs.</summary>
    public static partial class FestivalHudText
    {
        /// <summary>MINI-1: the minigame card's title and line, or ("","") for none. Standing alive in the booth's ring: how many of
        /// the connected living crew are in it, who keeps it from starting (down or busy, anywhere), then the countdown to the game.
        /// While the game runs, for everyone: the festival is paused, and for a player in it how long the game has left; then, for its
        /// banner's seconds, what its result did to the clock and when the festival starts again.</summary>
        public static (string Title,string Detail) MinigameCard(RoundState s,PlayerState p)
        {
            var b=s.Booth;
            if(s.Phase==FestivalSimulation.MinigamePhase)
            {
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

        /// <summary>MINI-1: a game's result on the clock, as its banner reads it: "+45 s", "−30 s", "+0 s".</summary>
        static string ClockChange(double seconds){int whole=(int)Math.Round(Math.Abs(seconds));return (seconds<0&&whole>0?"−":"+")+whole+" s";}

        /// <summary>MINI-1: E while a game runs, for a player in it until it is scored: the stub's FINISH; "" for anyone else, and once
        /// it is scored. ponytail: the stub is the only game until MINI-2..4, each of which says here what E does in it.</summary>
        public static string MinigamePrompt(RoundState s,PlayerState p)=>s.Phase==FestivalSimulation.MinigamePhase&&s.Booth.ScoredAt<0&&s.Booth.Kind==FestivalSimulation.StubGame&&s.Booth.PlayerIds.Contains(p.Id)?"E  FINISH THE GAME":"";

        // Seconds left, in whole seconds rounded up, as the clock counts them.
        static int WholeSeconds(double seconds)=>(int)Math.Ceiling(Math.Max(0,seconds));
    }
}
