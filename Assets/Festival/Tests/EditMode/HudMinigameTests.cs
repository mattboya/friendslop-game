using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;

namespace Festival.Tests
{
    // MINI-1: the minigame booth on the HUD, built from the viewer's round state: the ring's head count and countdown, the clock
    // stopped while a game runs, the game under way and the banner with its result.
    public sealed class HudMinigameTests
    {
        // Palm Mirage's Day 1, its booth on spot 1: you stand in its ring, Sam 20 m off.
        static RoundState Level(out PlayerState you,out PlayerState sam)
        {
            var at=Festivals.BoothSpots[1];
            var s=new RoundState{Phase="Playing",HostPlayerId="you",DurationSeconds=Festivals.Level(0,0,0).DurationSeconds,Booth=new MinigameState{Kind=FestivalSimulation.StubGame,Spot=1}};
            you=new PlayerState{Id="you",Name="You",X=at.X+1,Z=at.Z};sam=new PlayerState{Id="sam",Name="Sam",X=at.X+20,Z=at.Z};s.Players.Add(you);s.Players.Add(sam);
            return s;
        }
        static (string,string) Card(RoundState s,PlayerState p)=>FestivalHudText.MinigameCard(s,p);

        // In the ring the card counts who of the connected living crew is in; says who keeps it from starting, down or busy; then
        // counts the 3 s down. Outside the ring, dead, at camp or once the booth is played, there is no card.
        [Test]public void TheRingCardCountsTheCrewInThenCountsDown()
        {
            var s=Level(out var you,out var sam);var at=Festivals.BoothSpots[1];
            Assert.That(Card(s,you),Is.EqualTo(("MINIGAME BOOTH","1 / 2 in the ring. Everyone alive steps in to play, and the clock stops.")),"in the ring alone");
            Assert.That(Card(s,sam),Is.EqualTo(("","")),"Sam, outside it, has no card");
            sam.X=at.X-FestivalSimulation.MinigameRingRadius+.1f;
            s.Players.Add(new PlayerState{Id="ghost",Name="Ghost",Life="Spirit",X=at.X,Z=at.Z});s.Players.Add(new PlayerState{Id="gone",Name="Gone",Connected=false});
            Assert.That(Card(s,you),Is.EqualTo(("MINIGAME BOOTH","2 / 2 in the ring. Everyone alive steps in to play, and the clock stops.")),"Sam steps in; a spirit and a friend who left don't count");
            sam.InteractionId="talk";
            Assert.That(Card(s,you),Is.EqualTo(("MINIGAME BOOTH","It can't start while Sam is busy.")),"Sam is in the middle of something");
            sam.InteractionId="";sam.Life="Downed";sam.X=at.X+20;
            Assert.That(Card(s,you),Is.EqualTo(("MINIGAME BOOTH","It can't start while Sam is down.")),"Sam is down, wherever they are");
            sam.Life="Alive";sam.X=at.X;you.InteractionId="lie";
            Assert.That(Card(s,you),Is.EqualTo(("MINIGAME BOOTH","It can't start while you're busy.")),"you are lying down in the ring");
            you.InteractionId="";s.Booth.RingSeconds=.4;
            Assert.That(Card(s,you),Is.EqualTo(("MINIGAME IN 3","Stay in the ring. The clock stops while you play.")),"everyone in: the countdown");
            s.Booth.RingSeconds=2.1;Assert.That(Card(s,you).Item1,Is.EqualTo("MINIGAME IN 1"),"in whole seconds, rounded up");
            you.Life="Spirit";Assert.That(Card(s,you),Is.EqualTo(("","")),"a spirit isn't asked in");
            you.Life="Alive";s.Booth.Spent=true;Assert.That(Card(s,you),Is.EqualTo(("","")),"a played booth is gone");
            s.Booth.Spent=false;s.Phase="Results";Assert.That(Card(s,you),Is.EqualTo(("","")),"nor at the level's end");
        }

        // While the game runs the card says the festival is paused: a player in it reads how long the game has left, anyone watching
        // just watches. E finishes the stub for those in it, until it's scored. Then the banner: what the result did to the clock, for
        // everyone, and when the festival starts again.
        [Test]public void TheGameRunsPausedThenItsBannerShowsTheResult()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;s.Booth.PlayerIds.Add("you");s.Booth.Seconds=10.2;sam.Life="Spirit";
            Assert.That(Card(s,you),Is.EqualTo(("MINIGAME","The festival is paused while you play: 80 s left.")),"a player in the game");
            Assert.That(Card(s,sam),Is.EqualTo(("MINIGAME","The festival is paused while the crew plays.")),"a spirit watching");
            Assert.That(FestivalHudText.MinigamePrompt(s,you),Is.EqualTo("E  FINISH THE GAME"),"E finishes the stub");
            Assert.That(FestivalHudText.MinigamePrompt(s,sam),Is.Empty,"watchers have nothing to press");
            s.Booth.ScoredAt=12;s.Booth.Seconds=12.5;
            Assert.That(FestivalHudText.MinigamePrompt(s,you),Is.Empty,"a scored game takes no more moves");
            foreach(var (change,title,detail) in new[]{(0.0,"+0 s","The clock is unchanged."),(45.0,"+45 s","Added to the clock."),(-25.0,"−25 s","Taken off the clock."),(-12.4,"−12 s","Taken off the clock.")})
            {
                s.Booth.ClockChange=change;
                Assert.That(Card(s,you),Is.EqualTo((title,detail+" The festival starts again in 4.")),"a result of "+change+" s");
                Assert.That(Card(s,sam),Is.EqualTo(Card(s,you)),"watchers see it too");
            }
            s.Booth.Seconds=15.9;Assert.That(Card(s,you).Item2,Does.EndWith("starts again in 1."),"counting down to play");
            Assert.That(Card(s,new PlayerState{Id="late",Life="Alive"}).Item1,Is.EqualTo("−12 s"),"the banner is up for everyone, in the ring or not");
        }

        // While a game runs and its result shows, the clock stands at the time left, never WAIT, and shows the result's change at once.
        [Test]public void TheClockStandsAtTheTimeLeftWhileAGameRuns()
        {
            var s=Level(out _,out _);s.ElapsedSeconds=95.2;
            Assert.That(FestivalHudText.Clock(s),Is.EqualTo("6:25"),"setup: 384.8 s left in play");
            s.Phase=FestivalSimulation.MinigamePhase;
            Assert.That(FestivalHudText.Clock(s),Is.EqualTo("6:25"),"the clock stands at the time left");
            s.DurationSeconds+=45;Assert.That(FestivalHudText.Clock(s),Is.EqualTo("7:10"),"a gain shows on it at once");
        }
    }
}
