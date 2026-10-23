namespace Festival.Core
{
    /// <summary>POI-1 (Matt, 2026-10-01): hold left click with poi in hand and you spin them in front of you, and your friends see
    /// it; POI-7: right click spins them at your sides (PlayerState.VisualPoiSpinSides). No interaction, score or distraction, but a
    /// spinner blends in: festivalgoers who see them grow suspicious 10% slower (WookTrustFactor), and Sprint does nothing mid-spin
    /// (MoveSpeed). The host sets both flags from CanSpinPoi and the move intent each tick (FestivalSession.ApplyMovement); Q's poi
    /// performance is still the routine that counts.</summary>
    public sealed partial class FestivalSimulation
    {
        public static bool IsPoi(string item)=>item=="poi_practice"||item=="poi_led";
        /// <summary>POI-7: how every poi reply (hold, buy, equip) names the two spins, by action, so the HUD reads in each player's
        /// bound buttons (FestivalHudText.PressKeys); by default "Hold left click to spin them in front of you, right click at your sides."</summary>
        public const string SpinWords="Hold {Spin} to spin them in front of you, {SpinSides} at your sides.";
        /// <summary>Whether p may spin the poi in hand: alive, doing nothing else (no performance, check, ride or lying down), holding no
        /// friend or body, and with poi in hand: an unpaid shelf item over what is equipped, as the hands show it.</summary>
        public static bool CanSpinPoi(PlayerState p)=>p.Life=="Alive"&&p.InteractionId==""&&p.DragTargetId==""&&p.CarryBodyId==""&&IsPoi(p.HeldOfferId!=""?p.HeldOfferId:p.EquippedItemId);
    }
}
