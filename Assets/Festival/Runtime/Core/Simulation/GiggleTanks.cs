namespace Festival.Core
{
    /// <summary>GAS-2: now and then a Giggle Tank turns up at the festival, at one of Festivals.GiggleTankSpots. It is public, not a
    /// vision: everyone sees it, and the first living, free player to reach it grabs it, which banks Festivals.GiggleTankCash in
    /// the crew's shared stash at once. That is money to spend between rounds, withdrawn at the crew stash like the rest of the
    /// stash; it is never sale cash, so it never counts toward a day's quota.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string GrabGiggleTankKind="GrabGiggleTank";
        /// <summary>Where this level's Giggle Tank stands, or null: none this level, someone grabbed it, or the view has none (a
        /// spirit's). It reads only public state, so the HUD and the world show it from any client's view.</summary>
        public static WorldPoint GiggleTankAt(RoundState s)=>!s.GiggleTankFound&&s.GiggleTankSpot>=0&&s.GiggleTankSpot<Festivals.GiggleTankSpots.Length?Festivals.GiggleTankSpots[s.GiggleTankSpot]:null;
        // As the crew leaves camp, after the spinners: the level's own public stream rolls whether a tank turns up, and where. It
        // warms up with one draw, so the roll never reads a fresh stream's first value, which is its seed barely stirred.
        void RollGiggleTank()
        {
            var random=new ContentRandom(unchecked(State.SpinSeed*19+11));random.Next(100);
            State.GiggleTankFound=false;State.GiggleTankSpot=random.Next(100)<Festivals.GiggleTankPercent?random.Next(Festivals.GiggleTankSpots.Length):-1;
        }
        /// <summary>Whether p stands within reach of the level's Giggle Tank, where GrabGiggleTank takes it (the HUD offers it there).</summary>
        public static bool AtGiggleTank(RoundState s,PlayerState p){var at=GiggleTankAt(s);return at!=null&&Near(p,at.X,at.Z,Festivals.GiggleTankReach);}
        // Apply lets only a living, free player at the festival this far. The notice is the grabber's reply alone; everyone sees
        // the stash rise and the tank go.
        CommandResult GrabGiggleTank(PlayerState p)
        {
            if(!AtGiggleTank(State,p))return Reject(State.GiggleTankFound?"Someone beat you to the Giggle Tank":"Get right up to the Giggle Tank to grab it");
            State.GiggleTankFound=true;State.StashCash+=Festivals.GiggleTankCash;
            return Ok("Giggle Tank! +$"+Festivals.GiggleTankCash+" for camp");
        }
    }
}
