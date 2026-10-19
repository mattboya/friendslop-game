using System;
using Festival.Core;

namespace Festival.Presentation
{
    /// <summary>MINI-1 (docs/MINIGAMES_2026-09-30.md): the minigame booth's lines on the HUD, from the viewer's round state: the card
    /// under the aim dot (FestivalHud's minigame card) and what E does while a game runs. MINI-4: Mystery Cups' own lines. MINI-2: the
    /// Drum Circle's, which a drummer reads beside their lanes too. MINI-3: the Light Show's, and its questions and reveal.</summary>
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
                if(b.Kind==FestivalSimulation.DrumCircleGame)return DrumCard(s,p);
                if(b.Kind==FestivalSimulation.LightShowGame)return LightShowCard(s,p);
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
        /// <summary>MINI-2: the Drum Circle on the card. While it runs, every part's live accuracy in roster order, the viewer's own as
        /// "You". Once scored: the worst part's verdict, whose part it was, what the result did to the clock and the crowd, and when the
        /// festival starts again. The verdict follows the worst part's band (DrumCircle.ClockChange), which the fifteen-second floor
        /// can shrink on the clock but never on the crowd.</summary>
        static (string Title,string Detail) DrumCard(RoundState s,PlayerState p)
        {
            var b=s.Booth;
            if(b.ScoredAt<0)
            {
                var parts=new System.Collections.Generic.List<string>();
                foreach(string id in b.PlayerIds)parts.Add((id==p.Id?"You":NameOf(s,id))+" "+Percent(DrumAccuracy(b,id)));
                return (MinigameName(b.Kind),string.Join(Dot,parts)+"\nThe worst drummer sets the clock.");
            }
            int band=DrumBand(b);
            string worst="";double low=2;
            foreach(string id in b.PlayerIds){double accuracy=DrumAccuracy(b,id);if(accuracy<low){low=accuracy;worst=id;}}
            return (ClockChange(b.ClockChange),(band>0?"In the pocket.":band<0?"Off the beat.":"Decent.")
                +(worst==""?"":" Worst part: "+(worst==p.Id?"you":NameOf(s,worst))+", "+Percent(low)+".")
                +(b.ClockChange>0?" Added to the clock.":b.ClockChange<0?" Taken off the clock.":" The clock is unchanged.")
                +(band>0?" Nearby wooks ease off.":band<0?" Nearby wooks grow suspicious.":"")
                +" The festival starts again in "+WholeSeconds(b.ScoredAt+FestivalSimulation.MinigameBannerSeconds-b.Seconds)+".");
        }
        /// <summary>MINI-3: the Light Show on the card. Through the show, for a player in it, when the questions come. Then each
        /// question's number and seconds, the player's own locked in once sent, nobody's shown. Once scored: every player's points and
        /// the crew's at once, what the result did to the clock, and when the festival starts again. A spirit watches the crew's
        /// progress and reads the same reveal.</summary>
        static (string Title,string Detail) LightShowCard(RoundState s,PlayerState p)
        {
            var b=s.Booth;var show=b.LightShow??new LightShowState();bool playing=b.PlayerIds.Contains(p.Id);
            if(b.ScoredAt>=0)return (ClockChange(b.ClockChange),LightShowTally(s,p)+(b.ClockChange>0?" Added to the clock.":b.ClockChange<0?" Taken off the clock.":" The clock is unchanged.")
                +" The festival starts again in "+WholeSeconds(b.ScoredAt+FestivalSimulation.MinigameBannerSeconds-b.Seconds)+".");
            if(b.Seconds<LightShow.ShowSeconds)return (MinigameName(b.Kind),playing?"Watch the glover and everything around them. Questions in "+WholeSeconds(LightShow.ShowSeconds-b.Seconds)+" s."
                :"The crew watches a light show. The festival is paused.");
            string question="QUESTION "+(Math.Min(show.Slot,LightShow.QuestionSlots-1)+1)+" / "+LightShow.QuestionSlots,left=WholeSeconds(show.SlotAt+LightShow.QuestionSeconds-b.Seconds)+" s";
            if(!playing)return (MinigameName(b.Kind)+Dot+question,"The crew answers in private: "+left+".");
            return (question,LightShowOpen(s,p.Id).Answer!=NoAnswer?"Locked in. Waiting for the crew: "+left+".":left+" to answer. Nobody sees an answer until everyone has.");
        }
        /// <summary>MINI-3: what LightShowOpen gives for a question not answered yet; -1 is Not sure.</summary>
        public const int NoAnswer=-2;
        /// <summary>MINI-3: the Light Show question open to viewerId now, from their own record (only their view carries it): its slot,
        /// the question, and the answer they sent, NoAnswer before. Slot -1 and no question through the show, once scored, for anyone
        /// not playing and in any other game. Asked every frame, it allocates nothing.</summary>
        public static (int Slot,LightShowQuestion Question,int Answer) LightShowOpen(RoundState s,string viewerId)
        {
            var b=s?.Booth;var mine=b==null||s.Phase!=FestivalSimulation.MinigamePhase||b.Kind!=FestivalSimulation.LightShowGame||b.ScoredAt>=0?null:LightShowRecord(b,viewerId);
            int slot=b?.LightShow?.Slot??-1;
            if(mine==null||slot<0||slot>=mine.Questions.Count)return (-1,null,NoAnswer);
            return (slot,mine.Questions[slot],slot<mine.Answers.Count?mine.Answers[slot]:NoAnswer);
        }
        /// <summary>MINI-3, once the Light Show is scored and through its banner: each of viewerId's questions, a line under it with their
        /// answer and the right one; "" before and after, and for anyone with no questions.</summary>
        public static string LightShowReveal(RoundState s,string viewerId)
        {
            var b=s.Booth;var mine=s.Phase==FestivalSimulation.MinigamePhase&&b.Kind==FestivalSimulation.LightShowGame&&b.ScoredAt>=0?LightShowRecord(b,viewerId):null;
            if(mine==null)return "";
            var lines=new System.Collections.Generic.List<string>();
            for(int k=0;k<mine.Questions.Count;k++)
            {
                var q=mine.Questions[k];int answer=k<mine.Answers.Count?mine.Answers[k]:-1;string right=Choice(q,q.CorrectIndex);
                lines.Add(q.Prompt);
                lines.Add(answer<0?q.Unsure+". It was "+right+".":answer==q.CorrectIndex?"You said "+right+". Right: +"+(1+mine.Dose)+".":"You said "+Choice(q,answer)+". It was "+right+": −1.");
            }
            return string.Join("\n",lines);
        }
        // Every player's points, in roster order, and the crew's, as LightShowPoints scores them.
        static string LightShowTally(RoundState s,PlayerState p)
        {
            var parts=new System.Collections.Generic.List<string>();int crew=0;
            foreach(string id in s.Booth.PlayerIds){var x=LightShowRecord(s.Booth,id);if(x==null)continue;int points=LightShowScore(x);crew+=points;parts.Add((id==p.Id?"You":NameOf(s,id))+" "+Signed(points));}
            return "Crew score "+Signed(crew)+(parts.Count>0?": "+string.Join(", ",parts):"")+".";
        }
        static int LightShowNet(MinigameState b)
        {
            int net=0;if(b.Kind!=FestivalSimulation.LightShowGame||b.ScoredAt<0||b.LightShow==null)return 0;
            foreach(var x in b.LightShow.Players)net+=LightShowScore(x);return net;
        }
        // A revealed record's points. LightShowPoints refuses what no host sends, such as a key still hidden, and the HUD skips it.
        static int LightShowScore(LightShowPlayer x)
        {
            int points=0;
            for(int k=0;k<x.Answers.Count&&k<x.Questions.Count;k++)
            {
                int answer=x.Answers[k],correct=x.Questions[k].CorrectIndex;
                if(correct>=0&&correct<=3&&answer>=-1&&answer<=3&&x.Dose>=0&&x.Dose<=4)points+=FestivalSimulation.LightShowPoints(x.Dose,answer,correct);
            }
            return points;
        }
        static LightShowPlayer LightShowRecord(MinigameState b,string id){var players=b.LightShow?.Players;if(players!=null)foreach(var x in players)if(x.PlayerId==id)return x;return null;}
        static string Choice(LightShowQuestion q,int i)=>i>=0&&i<q.Choices.Count?q.Choices[i]:q.Unsure;
        static string Signed(int n)=>n>0?"+"+n:n<0?"−"+(-n):"0";
        static int DrumBand(MinigameState b)=>b.Kind!=FestivalSimulation.DrumCircleGame||b.DrumWorstScore<0?0:DrumCircle.ClockChange(new[]{b.DrumWorstScore});
        static double DrumAccuracy(MinigameState b,string id)=>b.DrumAccuracies?.Find(x=>x.PlayerId==id)?.Accuracy??0;
        static string Percent(double accuracy)=>(int)Math.Round(accuracy*100)+"%";
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
        /// <summary>Whether the minigame card's result reads as a win (mint, not orange): scored, nothing taken off the clock, in
        /// Mystery Cups no spiked cup drunk, in the Drum Circle no losing worst part and in the Light Show no points lost, even where the
        /// floor kept the clock.</summary>
        public static bool MinigameWon(RoundState s)=>s.Phase==FestivalSimulation.MinigamePhase&&s.Booth.ScoredAt>=0&&s.Booth.ClockChange>=0&&s.Booth.SpikedBy==""&&DrumBand(s.Booth)>=0&&LightShowNet(s.Booth)>=0;

        /// <summary>MINI-1: a game's result on the clock, as its banner reads it: "+45 s", "−30 s", "+0 s".</summary>
        static string ClockChange(double seconds){int whole=(int)Math.Round(Math.Abs(seconds));return (seconds<0&&whole>0?"−":"+")+whole+" s";}

        /// <summary>The keys while a game runs, for the player whose move it is until it is scored; "" for anyone else, once it is scored,
        /// and in a game with no moves of its own. MINI-4, Mystery Cups: E drinks `cup`, the one under the aim dot
        /// (FestivalMinigameView.AimedCup, -1 none), and F calls it.</summary>
        public static string MinigamePrompt(RoundState s,PlayerState p,int cup,string chat="F")=>FestivalSimulation.CupsTurn(s)!=p.Id?""
            :(cup>=0?"E  DRINK CUP "+(cup+1):"LOOK AT A CUP TO DRINK IT")+"   •   "+chat+" CALL IT";

        // Seconds left, in whole seconds rounded up, as the clock counts them.
        static int WholeSeconds(double seconds)=>(int)Math.Ceiling(Math.Max(0,seconds));
    }
}
