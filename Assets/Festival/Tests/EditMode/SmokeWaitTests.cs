using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;

namespace Festival.Tests
{
    // The native smokes' gameplay waits: a budget of simulation seconds in one round, and a real-time watchdog for a stopped clock.
    public sealed class SmokeWaitTests
    {
        static RoundState Round()=>new RoundState{RoundId="test-round",SimulationSeconds=70};
        static DevelopmentSmoke.GameplayWait Wait(RoundState state,double real=100,double budget=15.75,double realLimit=double.PositiveInfinity)
            =>new DevelopmentSmoke.GameplayWait("solo medical revival",state,real,budget,realLimit);
        [Test]public void LoadedFramesDoNotSpendTheRevivalSimulationBudget()
        {
            var state=Round();var wait=Wait(state);
            state.SimulationSeconds+=10;
            Assert.That(wait.CanContinue(state,130),Is.True,"30 real seconds at low frame rate must not spend a 15.75 simulation-second revival budget");
        }
        [Test]public void SpendingTheSimulationBudgetEndsTheWait()
        {
            var state=Round();var wait=Wait(state);
            state.SimulationSeconds+=15.75;
            Assert.That(wait.CanContinue(state,101),Is.False,"the sim budget ends independently of real elapsed time");
            Assert.That(wait.Failure,Is.EqualTo("simulation budget spent"));
        }
        [Test]public void ResetCannotReuseThePreviousRoundsBudget()
        {
            var state=Round();var wait=Wait(state);
            state.RoundId="new-round";state.SimulationSeconds=0;
            Assert.That(wait.CanContinue(state,101),Is.False);Assert.That(wait.Failure,Is.EqualTo("round changed"));
            state.SimulationSeconds=1;Assert.That(wait.CanContinue(state,102),Is.False,"a failed wait stays failed");
            var fresh=Wait(state,101);
            state.SimulationSeconds=2;Assert.That(fresh.CanContinue(state,102),Is.True,"a wait begun in the new round has its own budget");
        }
        [Test]public void SixtyRealSecondsWithoutSimulationProgressEndsTheWait()
        {
            var state=Round();var wait=Wait(state);
            Assert.That(wait.CanContinue(state,160),Is.False);Assert.That(wait.Failure,Is.EqualTo("no simulation progress"));
        }
        [Test]public void EachSimulationStepRestartsTheWatchdog()
        {
            var state=Round();var wait=Wait(state);
            state.SimulationSeconds+=.2;Assert.That(wait.CanContinue(state,159),Is.True);
            Assert.That(wait.CanContinue(state,218.9),Is.True,"59.9 real seconds since the last step");
            Assert.That(wait.CanContinue(state,219),Is.False);Assert.That(wait.Failure,Is.EqualTo("no simulation progress"));
        }
        [Test]public void AClockThatGoesBackOrASessionThatClosesEndsTheWait()
        {
            var state=Round();var back=Wait(state);
            state.SimulationSeconds-=.1;
            Assert.That(back.CanContinue(state,101),Is.False);Assert.That(back.Failure,Is.EqualTo("simulation clock regressed"));
            var closed=Wait(Round());
            Assert.That(closed.CanContinue(null,101),Is.False);Assert.That(closed.Failure,Is.EqualTo("session closed"));
        }
        [Test]public void ACallersRealLimitStillEndsTheWait()
        {
            var state=Round();var wait=Wait(state,100,15.75,105);
            state.SimulationSeconds+=1;Assert.That(wait.CanContinue(state,104.9),Is.True);
            Assert.That(wait.CanContinue(state,105),Is.False);Assert.That(wait.Failure,Is.EqualTo("real-time limit"));
        }
        [Test]public void TheLogLineCarriesSimulationAndRealTimeAndNoPlayerData()
        {
            var state=Round();var wait=Wait(state);
            Assert.That(wait.Report("start",state,100),Is.EqualTo("[Festival.SmokeWait] solo medical revival start sim=0.00/15.75 real=0.00"));
            state.SimulationSeconds+=3;
            Assert.That(wait.Report("end",state,106),Is.EqualTo("[Festival.SmokeWait] solo medical revival end sim=3.00/15.75 real=6.00"),"the end reads the clock it ended on");
            state.SimulationSeconds+=12.75;wait.CanContinue(state,140.5);
            Assert.That(wait.Report("fail",state,140.5),Is.EqualTo("[Festival.SmokeWait] solo medical revival fail sim=15.75/15.75 real=40.50 failure=simulation budget spent"));
            var reset=new RoundState{RoundId="next-round",SimulationSeconds=0};
            Assert.That(wait.Report("fail",reset,141),Does.Contain(" sim=15.75/15.75 "),"a new round's clock is not this wait's");
        }

        // The budget the smoke gives an interaction covers the host's own end of it (a dance's .75 s of rhythm grace included), and
        // runs out EndGraceSeconds after.
        [Test]public void ADanceWaitLastsUntilTheHostEndsTheDanceAndNoLonger()
        {
            var game=new FestivalSimulation(4);var player=game.AddPlayer("dancer","Dancer");
            game.State.Phase="Playing";game.State.Npcs.Clear();player.X=0;player.Z=0;
            game.State.Npcs.Add(new NpcState{Id="partner",Kind="Wook",Z=1.5f,Yaw=180,CanTalk=true});
            Assert.That(game.Execute(player.Id,new GameCommand{Id="dance",Kind="Dance",TargetId="partner"}).Accepted,Is.True,"setup: the dance starts");
            var dance=game.Interaction(player.InteractionId);
            var wait=new DevelopmentSmoke.GameplayWait("dance",game.State,0,DevelopmentSmoke.UntilEnd(game.State,dance));
            while(dance.Status=="Active"){Assert.That(wait.CanContinue(game.State,0),Is.True,"the wait outlasts the host's dance: "+game.State.SimulationSeconds);game.Tick(1.0/30);}
            Assert.That(dance.Status,Is.EqualTo("Complete"));
            double ended=game.State.SimulationSeconds;
            for(int step=0;step<600&&wait.CanContinue(game.State,0);step++)game.Tick(1.0/30);
            Assert.That(game.State.SimulationSeconds-ended,Is.EqualTo(DevelopmentSmoke.EndGraceSeconds).Within(.1),"the wait runs out a grace after the host's end");
        }
        // A wait for another player's step in a level lasts until the level's own end.
        [Test]public void ALevelWaitLastsUntilTheLevelEnds()
        {
            var game=new FestivalSimulation(4);game.AddPlayer("a","A");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.ElapsedSeconds=game.State.DurationSeconds-3;
            var wait=new DevelopmentSmoke.GameplayWait("level",game.State,0,DevelopmentSmoke.LevelLeft(game.State));
            while(game.State.Phase=="Playing"){Assert.That(wait.CanContinue(game.State,0),Is.True,"the wait outlasts the level");game.Tick(1.0/30);}
            Assert.That(game.State.Phase,Is.EqualTo("Results"),"setup: the level ran out");
            game.Tick(.5);
            Assert.That(wait.CanContinue(game.State,0),Is.True,"a snapshot that shows the end half a second late still fits");
        }
        // A chat check and a festivalgoer talk wait for their owner: no host clock ends them, so the smoke still bounds them.
        [Test]public void AnUntimedChatStillGetsAFiniteWait()
        {
            var state=Round();var check=new InteractionState{Kind="ConfirmChat",StartSeconds=60};
            Assert.That(FestivalSimulation.UntimedChat(check),Is.True,"setup: a chat check is untimed");
            Assert.That(DevelopmentSmoke.UntilEnd(state,check),Is.EqualTo(DevelopmentSmoke.ChatSeconds));
        }
    }
}
