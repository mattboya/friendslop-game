using System;
using System.Collections.Generic;

namespace Festival.Core
{
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
        public const string MinigamePhase="Minigame",MinigameKind="Minigame",StubGame="Stub";
        public const float MinigameRingRadius=4;
        public const double MinigameRingSeconds=3,MinigameMaxSeconds=90,MinigameBannerSeconds=4,MinigameFloorSeconds=15;
        // ponytail: only a no-op stub until MINI-2..4 land; any player in it ends it at once (PlayMinigame), for +0 s.
        public static readonly IReadOnlyList<string> MinigameKinds=new[]{StubGame};

        /// <summary>Where this level's booth stands, or null: none (camp, an older snapshot) or spent. It reads only public state,
        /// so the HUD and the world show it from any client's view.</summary>
        public static WorldPoint BoothAt(RoundState s)=>!s.Booth.Spent&&s.Booth.Spot>=0&&s.Booth.Spot<Festivals.BoothSpots.Length?Festivals.BoothSpots[s.Booth.Spot]:null;
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
            var at=BoothAt(State);if(at==null)return;
            var crew=State.Players.FindAll(p=>p.Connected&&p.Life=="Alive");
            bool ready=crew.Count>0&&crew.TrueForAll(p=>Near(p,at.X,at.Z,MinigameRingRadius))&&!State.Players.Exists(p=>p.Connected&&(p.Life=="Downed"||p.InteractionId!=""));
            State.Booth.RingSeconds=ready?State.Booth.RingSeconds+dt:0;
            if(State.Booth.RingSeconds<MinigameRingSeconds)return;
            State.Booth.RingSeconds=0;State.Booth.Seconds=0;State.Booth.ScoredAt=-1;State.Booth.ClockChange=0;State.Booth.PlayerIds=crew.ConvertAll(p=>p.Id);
            State.Phase=MinigamePhase;
        }
        // Each step of the game, in place of everything else Step does: the game's own clock, the cap, then the banner and the end.
        void MinigameTick(double dt)
        {
            var b=State.Booth;b.Seconds+=dt;
            // ponytail: the stub has nothing to score at the cap; each MINI-2..4 game scores what's in here.
            if(b.ScoredAt<0){if(b.Seconds>=MinigameMaxSeconds)ScoreMinigame(0);return;}
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
                case StubGame:ScoreMinigame(0);return Ok("Done");
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
            foreach(var d in State.Drops)d.ClaimAt=Shift(d.ClaimAt);
            foreach(var v in State.Visions)v.ShowUntil=Shift(v.ShowUntil);
        }
    }
}
