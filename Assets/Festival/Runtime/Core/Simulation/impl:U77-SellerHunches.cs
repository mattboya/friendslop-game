namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
        // ECON-7: this is a host-only category, not a promise that any stock remains.
        bool SellerCategory(NpcState npc)=>npc.Kind=="Wook"&&npc.Twist!=VipGuard&&(npc.Role=="Regular"||npc.Role=="ClueHolder"&&Festivals.Night(State.LevelIndex));
    }
}
