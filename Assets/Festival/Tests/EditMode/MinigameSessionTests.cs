using Festival.Core;
using Festival.Network;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // MINI-1: a level's minigame booth, and its game once under way, are public round state. Every client is sent them through the
    // wire format: the living, who play, and spirits, who watch. The weekend's bag of games stays on the host.
    public sealed class MinigameSessionTests
    {
        [Test] public void EveryClientSeesTheBoothAndTheGameUnderWay()
        {
            var game=new FestivalSimulation(5);game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");
            game.State.Phase="Playing";game.State.Booth=new MinigameState{Kind=FestivalSimulation.StubGame,Spot=1,RingSeconds=1.5};game.State.MinigameBag.Add("Later");
            var living=Wire(game,"p1");
            Assert.That(new object[]{living.Booth.Kind,living.Booth.Spot,living.Booth.RingSeconds},Is.EqualTo(new object[]{FestivalSimulation.StubGame,1,1.5}),"a living player's view carries the booth, its game and the ring's countdown");
            Assert.That(FestivalSimulation.BoothAt(living),Is.Not.Null,"so their client shows it where it stands");
            Assert.That(living.MinigameBag,Is.Empty,"the weekend's bag of games stays on the host");
            game.State.Phase=FestivalSimulation.MinigamePhase;game.State.Booth.Seconds=12;game.State.Booth.ScoredAt=10;game.State.Booth.ClockChange=-25;game.State.Booth.PlayerIds.Add("p0");
            game.Player("p1").Life="Spirit";var watching=Wire(game,"p1");
            Assert.That(watching.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"a spirit's view is in the game's phase too");
            Assert.That(new object[]{watching.Booth.Seconds,watching.Booth.ScoredAt,watching.Booth.ClockChange,string.Join(",",watching.Booth.PlayerIds)},Is.EqualTo(new object[]{12.0,10.0,-25.0,"p0"}),"and carries the game's clock, its result and who plays, to watch it");
            game.State.Booth.Spent=true;
            Assert.That(FestivalSimulation.BoothAt(Wire(game,"p0")),Is.Null,"once played the booth is gone from every view");
            Assert.That(JsonUtility.FromJson<RoundState>("{\"Phase\":\"Playing\",\"StashCash\":5}").Booth.Spot,Is.EqualTo(-1),"a snapshot from before booths reads as none");
        }

        // What viewer's client receives, through the wire format.
        private static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
    }
}
