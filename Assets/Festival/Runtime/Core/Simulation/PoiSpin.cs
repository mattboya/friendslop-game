namespace Festival.Core
{
    /// <summary>POI-1 (Matt, 2026-10-01): hold left click with poi in hand and you spin them, and your friends see it. It is only for
    /// show: no interaction, score, distraction or suspicion. The host sets PlayerState.VisualPoiSpin from CanSpinPoi and the move
    /// intent's SpinPoi each tick (FestivalSession.ApplyMovement); Q's poi performance is still the routine that counts.</summary>
    public sealed partial class FestivalSimulation
    {
        public static bool IsPoi(string item)=>item=="poi_practice"||item=="poi_led";
        /// <summary>Whether p may spin the poi in hand: alive, doing nothing else (no performance, check, ride or lying down), holding no
        /// friend or body, and with poi in hand: an unpaid shelf item over what is equipped, as the hands show it.</summary>
        public static bool CanSpinPoi(PlayerState p)=>p.Life=="Alive"&&p.InteractionId==""&&p.DragTargetId==""&&p.CarryBodyId==""&&IsPoi(p.HeldOfferId!=""?p.HeldOfferId:p.EquippedItemId);
    }
}
