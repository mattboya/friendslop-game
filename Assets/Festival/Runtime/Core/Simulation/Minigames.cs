using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>A name captured when a minigame starts; public to spectators without any player state.</summary>
    [Serializable] public sealed class MinigameParticipant
    {
        public string Id="",Name="";
    }

    /// <summary>MINI-1: a level's minigame booth, public round state every view carries (FestivalSession.ViewFor).</summary>
    [Serializable] public sealed class MinigameState
    {
        // Kind: the booth's game, one of FestivalSimulation.MinigameKinds. Spot: its place in Festivals.BoothSpots, -1 none (camp,
        // and snapshots from before booths). Spent once its game has been played.
        public string Kind=""; public int Spot=-1; public bool Spent;
        // RingSeconds: how long every connected living player has stood in its ring, unbroken. Seconds: how long its game has run,
        // result banner included, which is the pause's length. ScoredAt: when on that clock the game was scored and its banner
        // went up, -1 before. ClockChange: what the result did to the level's length, in seconds.
        public double RingSeconds,Seconds,ScoredAt=-1,ClockChange;
        // Who plays: the connected living players as the game began. Spirits and the detained watch.
        public List<string> PlayerIds=new List<string>();
        // Captured once at the common start, in participant order. Older snapshots keep an empty roster, never backfilled.
        public List<MinigameParticipant> Participants=new List<MinigameParticipant>();
        // MINI-4, Mystery Cups (MysteryCups.cs); untouched in any other game. Cups: how many are on the table. Turns: who drinks when,
        // a seeded order that goes round; Turn: how many turns have passed, so Turns[Turn % count] is up, since TurnAt on the game's
        // clock. Drunk: the cups drunk, in order. Pot: what calling it pays, or what the spiked cup lost. SpikedBy: who drank it, ""
        // nobody. Spiked: which cup is, -1 before; it never leaves the host. Hint: the cup the tripper is shown as spiked, sent to them
        // alone, -1 in everyone else's view (FestivalSimulation.VisibleBooth).
        public int Cups,Turn,Pot,Spiked=-1,Hint=-1; public double TurnAt; public string SpikedBy="";
        public List<int> Drunk=new List<int>(); public List<string> Turns=new List<string>();
        // Detached public data before recipient-private cup filtering: no recipient can rewrite the host or another view.
        internal MinigameState Copy()
        {
            var b=(MinigameState)MemberwiseClone();b.PlayerIds=new List<string>(PlayerIds);b.Drunk=new List<int>(Drunk);b.Turns=new List<string>(Turns);
            b.Participants=(Participants??new List<MinigameParticipant>()).ConvertAll(p=>new MinigameParticipant{Id=p.Id,Name=p.Name});return b;
        }
    }

    /// <summary>MINI-1 (docs/MINIGAMES_2026-09-30.md): each level has one booth, a stranger with a sign at one of
    /// Festivals.BoothSpots, its game drawn from a bag the weekend carries, so a weekend offers every game before any repeats.
    /// Opting in is physical: once every connected living player has stood in its MinigameRingRadius ring for MinigameRingSeconds
    /// in a row, with nobody downed or busy, the game starts and the round's phase is MinigamePhase. Then the level clock and
    /// every danger timer stop (Step does nothing else, handoff expiry included), nobody moves (TryMove), and only the game's
    /// own commands go through (Paused). It runs at most MinigameMaxSeconds; its result changes the level's length and shows for
    /// MinigameBannerSeconds; then every deadline on the simulation clock, which ran on, moves on by the pause (ShiftClock), play
    /// resumes and the booth is spent. Unplayed, it is gone with the level.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string MinigamePhase="Minigame",MinigameKind="Minigame";
        public const float MinigameRingRadius=4;
        public const double MinigameRingSeconds=3,MinigameMaxSeconds=90,MinigameBannerSeconds=4,MinigameFloorSeconds=15;
        // The games a booth deals: MINI-4's Mystery Cups, which took over from MINI-1's no-op stub, until MINI-2's Drum Circle and
        // MINI-3's Light Show join it.
        public static readonly IReadOnlyList<string> MinigameKinds=new[]{MysteryCupsGame};

        /// <summary>Where this level's booth stands, or null: none (camp, an older snapshot) or spent. It reads only public state,
        /// so the HUD and the world show it from any client's view.</summary>
        public static WorldPoint BoothAt(RoundState s)=>!s.Booth.Spent&&s.Booth.Spot>=0&&s.Booth.Spot<Festivals.BoothSpots.Length?Festivals.BoothSpots[s.Booth.Spot]:null;
        /// <summary>Whether p stands in the booth's ring (never where the level has none). Like the rule's other half, HoldsBooth,
        /// it reads only public state, so the HUD counts the ring from any client's view.</summary>
        public static bool InRing(RoundState s,PlayerState p){var at=BoothAt(s);return at!=null&&Near(p,at.X,at.Z,MinigameRingRadius);}
        /// <summary>The first connected player, wherever they are, who keeps the booth's game from starting: downed, or in the middle
        /// of something (lying down, a ride, a talk, a dance, carrying a body); null when nobody does.</summary>
        public static PlayerState HoldsBooth(RoundState s)=>s.Players.Find(p=>p.Connected&&(p.Life=="Downed"||p.InteractionId!=""||p.CarryBodyId!=""));
        /// <summary>The world's clock: the simulation clock with this level's game taken out, so it stands still while one runs and
        /// runs on from there after it. What a client draws on the simulation clock (the Ferris wheel) runs on this, so the
        /// festival holds as a freeze-frame and never jumps once play resumes. at: a time on the simulation clock, read on it.</summary>
        // The game's own clock is the pause's length (MinigameTick), and the level has one booth, so it is all the level has paused.
        // A time stamped before the game moves on by the pause only as play resumes (ShiftClock), so until then it reads as it is.
        public static double WorldSeconds(RoundState s)=>s.SimulationSeconds-s.Booth.Seconds;
        public static double WorldSeconds(RoundState s,double at)=>s.Phase==MinigamePhase?at:at-s.Booth.Seconds;

        // As the crew leaves camp, after the spinners: the booth's game out of the weekend's bag (refilled once empty) and its
        // spot, from their own stream, warmed up with one draw as GiggleTanks.cs does.
        void PlaceBooth()
        {
            var random=new ContentRandom(unchecked(State.SpinSeed*61+29));random.Next(100);
            if(State.MinigameBag.Count==0)State.MinigameBag.AddRange(MinigameKinds);
            int pick=random.Next(State.MinigameBag.Count);
            State.Booth=new MinigameState{Kind=State.MinigameBag[pick],Spot=random.Next(Festivals.BoothSpots.Length)};State.MinigameBag.RemoveAt(pick);
        }
        // Last in each Playing step that doesn't end the level: the ring's countdown, which starts the game.
        void WatchBooth(double dt)
        {
            if(BoothAt(State)==null)return;
            var crew=State.Players.FindAll(p=>p.Connected&&p.Life=="Alive");
            bool ready=crew.Count>0&&crew.TrueForAll(p=>InRing(State,p))&&HoldsBooth(State)==null;
            State.Booth.RingSeconds=ready?State.Booth.RingSeconds+dt:0;
            if(State.Booth.RingSeconds<MinigameRingSeconds)return;
            State.Booth.RingSeconds=0;State.Booth.Seconds=0;State.Booth.ScoredAt=-1;State.Booth.ClockChange=0;State.Booth.PlayerIds=crew.ConvertAll(p=>p.Id);
            State.Booth.Participants=crew.ConvertAll(p=>new MinigameParticipant{Id=p.Id,Name=p.Name});
            State.Phase=MinigamePhase;
            if(State.Booth.Kind==MysteryCupsGame)PourCups();
        }
        // Each step of the game, in place of everything else Step does: the game's own clock, the cap, then the banner and the end.
        void MinigameTick(double dt)
        {
            var b=State.Booth;b.Seconds+=dt;
            // A game's own tick scores it when it ends, at the cap too (Mystery Cups calls it). One this build doesn't know (a snapshot
            // from a build with other games) has no moves here, so the cap scores it +0 s.
            if(b.ScoredAt<0){if(b.Kind==MysteryCupsGame)CupsTick();if(b.Seconds>=MinigameMaxSeconds)ScoreMinigame(0);return;}
            if(b.Seconds<b.ScoredAt+MinigameBannerSeconds)return;
            ShiftClock(b.Seconds);b.Spent=true;State.Phase="Playing";
        }
        // Apply, while the game runs: only the game's own commands go through (MINI-2's drum parts will be Rhythm).
        CommandResult Paused(PlayerState p,GameCommand c)=>c.Kind==MinigameKind?PlayMinigame(p,c):c.Kind=="Rhythm"?Submit(p,c):Reject("Wait: the festival is paused for a minigame");
        CommandResult PlayMinigame(PlayerState p,GameCommand c)
        {
            var b=State.Booth;
            if(b.ScoredAt>=0)return Reject("This game is over");
            if(!b.PlayerIds.Contains(p.Id))return Reject("You're watching this one");
            switch(b.Kind)
            {
                case MysteryCupsGame:return PlayCups(p,c);
                default:return Reject("Unknown minigame");
            }
        }
        /// <summary>Scores the game under way: its result, `seconds` on the level clock, changes the level's length. A gain is paid in
        /// full, and the spinner's dose, which lasts the level, lasts that much longer; a loss never leaves under
        /// MinigameFloorSeconds, nor less than was left. Its banner then shows for MinigameBannerSeconds.</summary>
        internal void ScoreMinigame(int seconds)
        {
            var b=State.Booth;if(State.Phase!=MinigamePhase||b.ScoredAt>=0)return;
            double left=State.DurationSeconds-State.ElapsedSeconds;
            b.ClockChange=seconds>=0?seconds:Math.Max(seconds,Math.Min(0,MinigameFloorSeconds-left));b.ScoredAt=b.Seconds;
            State.DurationSeconds+=b.ClockChange;
            if(b.ClockChange>0)foreach(var p in State.Players)foreach(var e in p.Effects)if(e.Id==DoseEffect)e.RemainingSeconds+=b.ClockChange;
        }
        // As play resumes: every absolute time on the simulation clock, which ran on through the pause, moves on by it, so each
        // deadline keeps the seconds it had. A time of 0 or less means never or not yet (AttackAt 0, ShowUntil 0, AccusationSeconds
        // -1, LastTalkSeconds -100, ...) and stays. MinigameTests fails on any double it neither shifts nor lists as no such time.
        void ShiftClock(double by)
        {
            double Shift(double at)=>at>0?at+by:at;
            State.CloudClueReadAt=Shift(State.CloudClueReadAt);
            foreach(var p in State.Players)
            {
                p.RecoveryUntil=Shift(p.RecoveryUntil);p.ChimeUntil=Shift(p.ChimeUntil);p.SprintUntil=Shift(p.SprintUntil);p.HelpUntil=Shift(p.HelpUntil);
                p.NpcSpeechUntil=Shift(p.NpcSpeechUntil);p.VisualReceiptAt=Shift(p.VisualReceiptAt);
                p.DeathSpinAt=p.DeathSpinAt==0&&p.DeathSpinSequence>0?by:Shift(p.DeathSpinAt);
                foreach(var e in p.Effects)e.StartSeconds=Shift(e.StartSeconds);
            }
            foreach(var n in State.Npcs)
            {
                n.DistractedUntil=Shift(n.DistractedUntil);n.AttackAt=Shift(n.AttackAt);n.AttackCooldownUntil=Shift(n.AttackCooldownUntil);n.LastTalkSeconds=Shift(n.LastTalkSeconds);
                foreach(var o in n.Observers){o.LastSeenSeconds=Shift(o.LastSeenSeconds);o.AccusationSeconds=Shift(o.AccusationSeconds);}
                foreach(var e in n.Evidence){e.DetainAt=Shift(e.DetainAt);e.StartSeconds=Shift(e.StartSeconds);e.SeenSeconds=Shift(e.SeenSeconds);}
            }
            foreach(var i in State.Interactions)i.StartSeconds=Shift(i.StartSeconds);
            foreach(var t in State.Transfers)t.ExpiresAt=Shift(t.ExpiresAt);
            foreach(var d in State.Drops){d.ClaimAt=Shift(d.ClaimAt);d.LandsAt=Shift(d.LandsAt);}
            foreach(var v in State.Visions)v.ShowUntil=Shift(v.ShowUntil);
        }
    }
}
