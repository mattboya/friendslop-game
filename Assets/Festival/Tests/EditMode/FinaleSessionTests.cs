using NUnit.Framework;
using Festival.Core;
using Festival.Network;

namespace Festival.Tests
{
    public sealed class FinaleSessionTests
    {
        [Test] public void EveryoneSeesTheBodiesAndWhoCarriesThem()
        {
            var game=new FestivalSimulation(9);game.AddPlayer("carrier","Carrier");game.AddPlayer("fallen","Fallen");game.AddPlayer("watcher","Watcher");
            game.State.Phase="Playing";game.State.LevelIndex=3;game.Player("fallen").Life="Spirit";
            game.State.Bodies.Add(new BodyState{PlayerId="fallen",X=3,Z=-20});game.Player("carrier").CarryBodyId="fallen";
            foreach(var viewer in new[]{"carrier","fallen","watcher"})
            {
                var view=FestivalSession.ViewFor(game,viewer);
                Assert.That(view.Bodies.Count,Is.EqualTo(1),viewer+" sees the body, so the Night 2 checklist is the same for everyone");
                Assert.That(new[]{view.Bodies[0].PlayerId,view.Bodies[0].X.ToString(),view.Bodies[0].Z.ToString()},Is.EqualTo(new[]{"fallen","3","-20"}),viewer+" sees whose body it is and where");
            }
            var carrier=FestivalSession.ViewFor(game,"watcher").Players.Find(p=>p.Id=="carrier");
            Assert.That(carrier.CarryBodyId,Is.EqualTo("fallen"),"other players see who is carrying the body");
            Assert.That(carrier.VisualPose,Is.EqualTo("Drag"),"a carrier plays the existing drag pose");
        }
    }
}
