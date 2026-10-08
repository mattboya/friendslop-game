using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Festival.Tests
{
    // MINI-1: the minigame booth on the HUD, built from the viewer's round state: the ring's head count and countdown, the clock
    // stopped while a game runs, the game under way and the banner with its result. MINI-4: Mystery Cups on that card, and its keys.
    public sealed class HudMinigameTests
    {
        // A game with no lines of its own, as MINI-2's and MINI-3's will be until they write theirs: the card's plain ones show.
        const string PlainGame="AnotherGame";
        // Palm Mirage's Day 1, its booth on spot 1 dealing `game`: you stand in its ring, Sam 20 m off.
        static RoundState Level(out PlayerState you,out PlayerState sam,string game=FestivalSimulation.MysteryCupsGame)
        {
            var at=Festivals.BoothSpots[1];
            var s=new RoundState{Phase="Playing",HostPlayerId="you",DurationSeconds=Festivals.Level(0,0,0).DurationSeconds,Booth=new MinigameState{Kind=game,Spot=1}};
            you=new PlayerState{Id="you",Name="You",X=at.X+1,Z=at.Z};sam=new PlayerState{Id="sam",Name="Sam",X=at.X+20,Z=at.Z};s.Players.Add(you);s.Players.Add(sam);
            return s;
        }
        [Test]public void Mini5HomeAndSundownCardsHoldThroughGameAndBanner()
        {
            var s=Level(out var you,out var sam);s.ElapsedSeconds=s.DurationSeconds-50;
            Assert.That(FestivalHudText.SundownStage(s),Is.EqualTo(1));
            s.Phase=FestivalSimulation.MinigamePhase;
            Assert.That(FestivalHudText.SundownStage(s),Is.EqualTo(1),"paused day keeps its sundown stage");
            s.LevelIndex=3;string before;
            s.Phase="Playing";before=FestivalHudText.HomeChecklist(s,you.Id);
            Assert.That(before,Is.Not.Empty);s.Phase=FestivalSimulation.MinigamePhase;
            Assert.That(FestivalHudText.HomeChecklist(s,you.Id),Is.EqualTo(before),"paused Night 2 keeps its home checklist");
            s.Booth.ScoredAt=4;
            Assert.That(FestivalHudText.HomeChecklist(s,you.Id),Is.EqualTo(before),"banner also keeps home checklist");
        }

        [Test]public void NightTwoHomeChecklistHoldsDuringGameAndBanner()
        {
            var state=Level(out var player,out _);state.LevelIndex=3;string before=FestivalHudText.HomeChecklist(state,player.Id);Assert.That(before,Is.Not.Empty);
            state.Phase=FestivalSimulation.MinigamePhase;
            Assert.That(FestivalHudText.HomeChecklist(state,player.Id),Is.EqualTo(before),"home status remains during game");
            state.Booth.ScoredAt=5;Assert.That(FestivalHudText.HomeChecklist(state,player.Id),Is.EqualTo(before),"and banner");
        }
        [Test]public void TimePayoutDoesNotReplayAnAlreadyReadSundownThreshold()
        {
            var root=new GameObject("impl-U58 payout HUD");
            try
            {
                var hud=root.AddComponent<FestivalHud>();typeof(FestivalHud).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
                var state=Level(out var player,out _);state.RoundId="impl-U58-payout";state.ElapsedSeconds=state.DurationSeconds-50;
                var update=typeof(FestivalHud).GetMethod("UpdateSundownAndResults",BindingFlags.Instance|BindingFlags.NonPublic);
                var shown=typeof(FestivalHud).GetField("sundownShown",BindingFlags.Instance|BindingFlags.NonPublic);
                update.Invoke(hud,new object[]{state,player,true});shown.SetValue(hud,FestivalHud.SundownNoticeSeconds);
                state.DurationSeconds+=60;update.Invoke(hud,new object[]{state,player,true});
                state.ElapsedSeconds+=60;update.Invoke(hud,new object[]{state,player,false});
                Assert.That((float)shown.GetValue(hud),Is.GreaterThanOrEqualTo(FestivalHud.SundownNoticeSeconds),"crossing one-minute threshold again cannot replay the warning");
            }
            finally{Object.DestroyImmediate(root);UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();}
        }

        [Test]public void EachSundownStageKeepsItsReadProgressAcrossPayoutAndPause()
        {
            var root=new GameObject("impl-U58 sundown HUD");
            try
            {
                var hud=root.AddComponent<FestivalHud>();
                typeof(FestivalHud).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
                var state=Level(out var player,out _);state.RoundId="impl-U58-sundown";state.ElapsedSeconds=state.DurationSeconds-50;
                var update=typeof(FestivalHud).GetMethod("UpdateSundownAndResults",BindingFlags.Instance|BindingFlags.NonPublic);
                var shown=typeof(FestivalHud).GetField("sundownShown",BindingFlags.Instance|BindingFlags.NonPublic);
                update.Invoke(hud,new object[]{state,player,true});shown.SetValue(hud,2f);
                state.Phase=FestivalSimulation.MinigamePhase;state.Booth.Seconds=20;
                update.Invoke(hud,new object[]{state,player,true});
                Assert.That((float)shown.GetValue(hud),Is.EqualTo(2),"game keeps the partly read warning");
                state.Booth.ScoredAt=20;state.DurationSeconds+=60;
                update.Invoke(hud,new object[]{state,player,true});
                state.Phase="Playing";state.ElapsedSeconds+=60;
                update.Invoke(hud,new object[]{state,player,true});
                Assert.That((float)shown.GetValue(hud),Is.EqualTo(2),"return across one-minute threshold after time payout keeps progress");
                shown.SetValue(hud,FestivalHud.SundownNoticeSeconds);state.ElapsedSeconds=state.DurationSeconds-10;
                update.Invoke(hud,new object[]{state,player,true});shown.SetValue(hud,FestivalHud.SundownNoticeSeconds);
                state.DurationSeconds+=60;update.Invoke(hud,new object[]{state,player,true});
                state.ElapsedSeconds+=60;update.Invoke(hud,new object[]{state,player,false});
                Assert.That((float)shown.GetValue(hud),Is.GreaterThanOrEqualTo(FestivalHud.SundownNoticeSeconds),"an already read fifteen-second warning never replays");
                state.LevelIndex=2;state.ElapsedSeconds=state.DurationSeconds-50;update.Invoke(hud,new object[]{state,player,true});
                Assert.That((float)shown.GetValue(hud),Is.Zero,"a new level has its own progress");
            }
            finally{Object.DestroyImmediate(root);UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();}
        }
        [Test]public void EscapePhaseCardNamesTheMinigameDuringGameAndBanner()
        {
            var root=new GameObject("impl-U58 menu HUD");
            try
            {
                var hud=root.AddComponent<FestivalHud>();typeof(FestivalHud).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
                var state=Level(out var player,out _);state.Phase=FestivalSimulation.MinigamePhase;
                var update=typeof(FestivalHud).GetMethod("UpdateText",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach(double scored in new[]{-1d,4d})
                {state.Booth.ScoredAt=scored;update.Invoke(hud,new object[]{state,player});Assert.That(((Text)typeof(FestivalHud).GetField("menuPhase",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hud)).text,Is.EqualTo("MINIGAME"));}
            }
            finally{Object.DestroyImmediate(root);UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();}
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

        // While a game with no lines of its own runs, the card says the festival is paused: a player in it reads how long the game
        // has left, anyone watching just watches, and nobody has a key to press. Then the banner: what the result did to the clock,
        // for everyone, and when the festival starts again.
        [Test]public void TheGameRunsPausedThenItsBannerShowsTheResult()
        {
            var s=Level(out var you,out var sam,PlainGame);s.Phase=FestivalSimulation.MinigamePhase;s.Booth.PlayerIds.Add("you");s.Booth.Seconds=10.2;sam.Life="Spirit";
            Assert.That(Card(s,you),Is.EqualTo(("MINIGAME","The festival is paused while you play: 80 s left.")),"a player in the game");
            Assert.That(Card(s,sam),Is.EqualTo(("MINIGAME","The festival is paused while the crew plays.")),"a spirit watching");
            Assert.That(new[]{FestivalHudText.MinigamePrompt(s,you,0),FestivalHudText.MinigamePrompt(s,sam,0)},Is.EqualTo(new[]{"",""}),"a game with no moves of its own offers nothing to press");
            Assert.That(FestivalHudText.MinigameWon(s),Is.False,"until it is scored it shows no win");
            s.Booth.ScoredAt=12;s.Booth.Seconds=12.5;
            foreach(var (change,title,detail) in new[]{(0.0,"+0 s","The clock is unchanged."),(45.0,"+45 s","Added to the clock."),(-25.0,"−25 s","Taken off the clock."),(-12.4,"−12 s","Taken off the clock.")})
            {
                s.Booth.ClockChange=change;
                Assert.That(Card(s,you),Is.EqualTo((title,detail+" The festival starts again in 4.")),"a result of "+change+" s");
                Assert.That(Card(s,sam),Is.EqualTo(Card(s,you)),"watchers see it too");
                Assert.That(FestivalHudText.MinigameWon(s),Is.EqualTo(change>=0),"the banner reads as a win unless it took time off the clock");
            }
            s.Booth.Seconds=15.9;Assert.That(Card(s,you).Item2,Does.EndWith("starts again in 1."),"counting down to play");
            Assert.That(Card(s,new PlayerState{Id="late",Life="Alive"}).Item1,Is.EqualTo("−12 s"),"the banner is up for everyone, in the ring or not");
        }

        // MINI-4, Mystery Cups under way, for everyone at the table: the pot, who is up and their seconds, and what the next safe cup
        // adds; to the tripper alone (whose view alone carries the hint), the cup their hunch says is spiked while it is on the table.
        [Test]public void MysteryCupsShowsThePotWhoIsUpAndOnlyTheTrippersHunch()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;
            b.PlayerIds.AddRange(new[]{"you","sam"});b.Cups=4;b.Turns.AddRange(new[]{"sam","you"});b.TurnAt=2;b.Seconds=4.6;
            var ghost=new PlayerState{Id="ghost",Name="Ghost",Life="Spirit"};s.Players.Add(ghost);
            Assert.That(Card(s,you),Is.EqualTo(("MYSTERY CUPS  •  POT $0","Sam's turn: 8 s. A safe cup adds $5; the spiked one loses the pot.")),"Sam is up");
            Assert.That(Card(s,ghost),Is.EqualTo(Card(s,you)),"a spirit watches the same");
            Assert.That(Card(s,sam),Is.EqualTo(("MYSTERY CUPS  •  POT $0","Your turn: 8 s. Drink a cup for $5, or call it. One is spiked.")),"Sam's own card");
            b.Drunk.Add(2);b.Pot=5;b.Turn=1;b.TurnAt=5;b.Seconds=6;
            Assert.That(Card(s,you),Is.EqualTo(("MYSTERY CUPS  •  POT $5","Your turn: 9 s. Drink a cup for $10, or call it. One is spiked.")),"a safe cup later, you are up");
            b.Hint=3;
            Assert.That(Card(s,you).Item2,Does.EndWith("One is spiked. Your hunch: cup 4 is spiked."),"the tripper's view carries the hunch, numbered as the cups are");
            Assert.That(Card(s,sam).Item2,Does.EndWith("Your hunch: cup 4 is spiked."),"whoever's turn it is");
            b.Hint=2;Assert.That(Card(s,you).Item2,Does.EndWith("One is spiked."),"once that cup is drunk the hunch is gone with it");
        }

        // Mystery Cups' banner says what became of the pot: lost to the spiked cup, split across the crew by day, or banked at night.
        // The clock is unchanged, so the banner reads as a win unless the spiked cup was drunk.
        [Test]public void MysteryCupsBannerSaysWhatBecameOfThePot()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;
            b.PlayerIds.AddRange(new[]{"you","sam"});b.Cups=4;b.Turns.AddRange(new[]{"sam","you"});b.ScoredAt=12;b.Seconds=12.5;b.Pot=15;b.SpikedBy="sam";
            const string again=" The festival starts again in 4.";
            Assert.That(Card(s,you),Is.EqualTo(("SPIKED!","Sam drank the spiked cup and lost the $15 pot."+again)),"the spiked cup, to everyone");
            Assert.That(Card(s,sam).Item2,Is.EqualTo("You drank the spiked cup and lost the $15 pot."+again),"to its drinker");
            Assert.That(FestivalHudText.MinigameWon(s),Is.False,"which is no win");
            b.Pot=0;Assert.That(Card(s,you).Item2,Is.EqualTo("Sam drank the spiked cup."+again),"with nothing in the pot yet");
            b.SpikedBy="";b.Pot=15;
            Assert.That(Card(s,you),Is.EqualTo(("+$15","Called it: $7 each, counted as sales."+again)),"called by day, split across the crew");
            Assert.That(FestivalHudText.MinigameWon(s),Is.True,"a win");
            s.LevelIndex=1;Assert.That(Card(s,you),Is.EqualTo(("+$15","Called it: $15 to the stash."+again)),"called at night, banked");
            b.Pot=0;Assert.That(Card(s,you),Is.EqualTo(("+$0","Called it with nothing in the pot."+again)),"called before a cup was drunk");
        }

        // E drinks the cup under the aim dot and F calls it, for the player who is up alone, until the game is scored.
        [Test]public void OnlyThePlayerUpDrinksOrCallsIt()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;
            b.PlayerIds.AddRange(new[]{"you","sam"});b.Cups=4;b.Turns.AddRange(new[]{"sam","you"});
            Assert.That(FestivalHudText.MinigamePrompt(s,sam,2),Is.EqualTo("E  DRINK CUP 3   •   F CALL IT"),"Sam, looking at cup 3");
            Assert.That(FestivalHudText.MinigamePrompt(s,sam,-1),Is.EqualTo("LOOK AT A CUP TO DRINK IT   •   F CALL IT"),"looking at none");
            Assert.That(FestivalHudText.MinigamePrompt(s,you,2),Is.Empty,"you wait your turn");
            b.ScoredAt=3;Assert.That(FestivalHudText.MinigamePrompt(s,sam,2),Is.Empty,"nothing once it is scored");
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
