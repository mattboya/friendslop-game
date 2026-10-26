using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>MINI-4 (docs/MINIGAMES_2026-09-30.md, Mystery Cups): a shady bartender sets out CupsFor(crew) cups, one of them spiked,
    /// and the crew pushes its luck on a shared pot. The first turn's clock starts CupsIntroSeconds after the pour, while the card reads
    /// the rules. Turns go round the players in a seeded order, CupTurnSeconds each: drink a cup
    /// (DrinkCup) or call it (CallCups), and a turn that runs out calls it. The nth safe cup adds CupStep × n to the pot. The spiked cup
    /// loses the pot and doses its drinker for the rest of the level, or one stronger up to the dose wheel's top; it never touches the
    /// spin's State.Doses, so the tripper's visions stay as they were rolled. Calling it pays the pot as sales: by day split across the
    /// players' cash, toward the quota; by night into the stash. Once only the spiked cup is left the game calls it. The tripper alone
    /// is shown a hunch cup (VisibleBooth), the spiked one at HunchReliability, else a safe one. The clock is unchanged (+0 s): the
    /// pot is the stake.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string MysteryCupsGame="MysteryCups",DrinkCup="Drink",CallCups="Call";
        public const int CupStep=5,MinCups=4;
        public const double CupTurnSeconds=10,CupsIntroSeconds=3;

        /// <summary>How many cups a crew of `crew` plays: two more than it has, at least MinCups.</summary>
        public static int CupsFor(int crew)=>Math.Max(MinCups,crew+2);
        /// <summary>Who is up in the Mystery Cups game under way; "" in any other game, before one and once it is scored.</summary>
        public static string CupsTurn(RoundState s){var b=s.Booth;return s.Phase==MinigamePhase&&b.Kind==MysteryCupsGame&&b.ScoredAt<0&&b.Turns.Count>0?b.Turns[b.Turn%b.Turns.Count]:"";}
        /// <summary>What the next safe cup adds to the pot: the nth adds CupStep × n.</summary>
        public static int NextCupPays(MinigameState b)=>CupStep*(b.Drunk.Count+1);
        /// <summary>How often the tripper's hunch marks the spiked cup: their vision reliability for the spun dose (75 / 50 / 25 / 10%),
        /// one step less sure in a game the tripper plays alone (50 / 25 / 10 / 10%), so a lone player cannot skip the marked cup for a
        /// free pot. A friend alone at the table while the tripper is away from it (detained, say) keeps the crew's odds.
        /// Every view carries the doses and the players, so the card can name these odds.</summary>
        public static double HunchReliability(RoundState s)=>Reliability[Math.Min(Reliability.Length-1,TripperDose(s)-1+(s.Booth.PlayerIds.Count==1&&s.Booth.PlayerIds[0]==s.TripperId?1:0))];
        /// <summary>The booth as viewer's view carries it (FestivalSession.ViewFor): never which cup is spiked, and the tripper's hint
        /// to the tripper alone. The rest of the game is public.</summary>
        public static MinigameState VisibleBooth(RoundState s,string viewer){var b=s.Booth.Copy();b.Spiked=-1;if(viewer==""||viewer!=s.TripperId)b.Hint=-1;return b;}

        // As the game starts: the cups, the spiked one, the tripper's hint and the turns, from their own stream, which mixes in the host's
        // DealSeed (TRIP-7) so no client can pour them again from what it is sent; warmed up with one draw as PlaceBooth's is.
        internal void PourCups()
        {
            var b=State.Booth;var random=new ContentRandom(unchecked((State.SpinSeed*67+37)^State.DealSeed));random.Next(100);
            b.Cups=CupsFor(b.PlayerIds.Count);b.Spiked=random.Next(b.Cups);
            b.Hint=random.Next(100)<Math.Round(HunchReliability(State)*100)?b.Spiked:(b.Spiked+1+random.Next(b.Cups-1))%b.Cups;
            b.Turns=Shuffled(new List<string>(b.PlayerIds),random);b.Turn=0;b.TurnAt=b.Seconds+CupsIntroSeconds;b.Pot=0;b.Drunk.Clear();b.SpikedBy="";
        }
        // A move by a player in the game: only the one who is up drinks a cup still on the table or calls it.
        CommandResult PlayCups(PlayerState p,GameCommand c)
        {
            var b=State.Booth;string up=CupsTurn(State);
            if(p.Id!=up)return Reject("It's "+(Player(up)?.Name??"someone else")+"'s turn");
            if(c.ItemId==CallCups){CallIt();return Ok("Called it");}
            if(c.ItemId!=DrinkCup||c.Amount<0||c.Amount>=b.Cups||b.Drunk.Contains(c.Amount))return Reject("Drink a cup still on the table, or call it");
            b.Drunk.Add(c.Amount);
            if(c.Amount==b.Spiked){b.SpikedBy=p.Id;Spike(p);ScoreMinigame(0);return Ok("Spiked");}
            b.Pot+=CupStep*b.Drunk.Count;
            if(b.Drunk.Count==b.Cups-1){CallIt();return Ok("Only the spiked cup is left");}
            b.Turn++;b.TurnAt=b.Seconds;return Ok("Safe");
        }
        // Each step of the game: a turn that runs out calls it, and so does the minigame cap, which scores what's in.
        void CupsTick(){var b=State.Booth;if(b.Seconds-b.TurnAt>=CupTurnSeconds||b.Seconds>=MinigameMaxSeconds)CallIt();}
        // Calling it pays the pot as sale cash: sales toward the day's quota, then by day split evenly across the players' cash (the
        // odd dollars to the stash), by night all of it to the stash.
        void CallIt()
        {
            var b=State.Booth;State.GrossSales+=b.Pot;State.LevelSales+=b.Pot;
            var crew=DayLevel?b.PlayerIds.ConvertAll(Player).FindAll(p=>p!=null):new List<PlayerState>();
            int share=crew.Count>0?b.Pot/crew.Count:0;foreach(var p in crew)p.Cash+=share;State.StashCash+=b.Pot-share*crew.Count;
            ScoreMinigame(0);
        }
        // The spiked cup: a dose for the rest of the level, its substance from its own stream, or the dose already taken one stronger,
        // up to the dose wheel's top. Only the effect: the spin's State.Doses, which visions and the payout read, stay as they were.
        void Spike(PlayerState p)
        {
            var dose=p.Effects.Find(e=>e.Id==DoseEffect);
            if(dose!=null)dose.Intensity=Math.Min(DoseWeights.Length,dose.Intensity+1);
            else AddDose(p,1,Substances[new ContentRandom(unchecked(State.SpinSeed*71+41)).Next(Substances.Count)]);
        }
    }
}
