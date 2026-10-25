namespace Festival.Core
{
    /// <summary>Now and then a Giggle Tank turns up at the festival, at one of Festivals.GiggleTankSpots. It is public, not a
    /// vision: everyone sees it, and the first living, free player to reach it with a free hand slot grabs it. Carried home inside
    /// the camp gate it banks Festivals.GiggleTankCash in the crew's shared stash: money to spend between rounds, withdrawn at the
    /// crew stash like the rest of the stash, never sale cash, so it never counts toward a day's quota.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string GrabGiggleTankKind="GrabGiggleTank",GiggleTankItem="giggle_tank";
        /// <summary>Where this level's Giggle Tank stands, or null: none this level, someone grabbed it, or the view has none (a
        /// spirit's). It reads only public state, so the HUD and the world show it from any client's view.</summary>
        public static WorldPoint GiggleTankAt(RoundState s)=>!s.GiggleTankFound&&s.GiggleTankSpot>=0&&s.GiggleTankSpot<Festivals.GiggleTankSpots.Length?Festivals.GiggleTankSpots[s.GiggleTankSpot]:null;
        // As the crew leaves camp, after the spinners: the level's own public stream rolls whether a tank turns up, and where. It
        // warms up with one draw, so the roll never reads a fresh stream's first value, which is its seed barely stirred. ECON-1:
        // never on the weekend's last level, whose stash the weekend's end empties, so "+$25 for camp" stays true; the guard
        // skips only that level's draws, so every other level rolls as before.
        void RollGiggleTank()
        {
            var random=new ContentRandom(unchecked(State.SpinSeed*19+11));random.Next(100);
            State.GiggleTankFound=false;State.GiggleTankSpot=State.LevelIndex<Festivals.LevelCount-1&&random.Next(100)<Festivals.GiggleTankPercent?random.Next(Festivals.GiggleTankSpots.Length):-1;
        }
        /// <summary>Whether p stands within reach of the level's Giggle Tank, where GrabGiggleTank takes it (the HUD offers it there).</summary>
        public static bool AtGiggleTank(RoundState s,PlayerState p){var at=GiggleTankAt(s);return at!=null&&Near(p,at.X,at.Z,Festivals.GiggleTankReach);}
        // Apply lets only a living, free player at the festival this far.
        CommandResult GrabGiggleTank(PlayerState p)
        {
            if(!AtGiggleTank(State,p))return Reject(State.GiggleTankFound?"Someone beat you to the Giggle Tank":"Get right up to the Giggle Tank to grab it");
            if(!CanAdd(p,GiggleTankItem,1))return Reject("Hands full: free a slot to carry the Giggle Tank");
            Add(p,GiggleTankItem,1);State.GiggleTankFound=true;State.GiggleTankGrabberId=p.Id;State.GiggleTankGrabberName=p.Name;
            return Ok("Giggle Tank! Carry it home to the camp gate: +$"+Festivals.GiggleTankCash+" for camp");
        }
        // Step runs this after the tick's arrests and deaths, and End before a level closes, so a carrier home on the last tick
        // still pays. A spirit's carried body is home for Extract, never for a tank.
        void BankGiggleTanks()
        {
            if(State.Phase!="Playing")return;
            foreach(var p in State.Players)if(p.Connected&&(p.Life=="Alive"||p.Life=="Downed")&&Count(p,GiggleTankItem)>0&&AtCampGate(p.X,p.Z))
            {Take(p,GiggleTankItem,1);State.StashCash+=Festivals.GiggleTankCash;p.GiggleTankBankSequence++;CrewNews(p,"Stash",MoneyNotice("STASH",Festivals.GiggleTankCash)+"  •  "+p.Name+" brought the Giggle Tank home");}
        }
        // Detention, death and the next level lose a tank, one still on offer included: returning it to full hands would drop it.
        void LoseGiggleTank(PlayerState p)
        {
            int lost=State.Transfers.RemoveAll(t=>t.FromId==p.Id&&t.ItemId==GiggleTankItem)+p.Inventory.RemoveAll(x=>x.ItemId==GiggleTankItem);if(lost>0)News(p,"Items","GIGGLE TANK LOST");
            if(p.EquippedItemId==GiggleTankItem)p.EquippedItemId="";
        }
    }
}
