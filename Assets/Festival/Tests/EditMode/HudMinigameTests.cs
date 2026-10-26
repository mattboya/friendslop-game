using System.Collections.Generic;
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

        // The tripper's dose card and a living player's day quota stay up through every game and its banner, and a cups payout,
        // counted as sales the moment it is called, shows on the quota at once.
        [TestCase(FestivalSimulation.MysteryCupsGame)][TestCase(FestivalSimulation.DrumCircleGame)][TestCase(FestivalSimulation.LightShowGame)][TestCase(PlainGame)]
        public void Mini6TripperAndQuotaRemainVisibleDuringEveryGameAndBanner(string game)
        {
            var s=Level(out var you,out var sam,game);s.TripperId=you.Id;
            s.Doses.Add(new PlayerDose{PlayerId=you.Id,Dose=3,Substance="lsd"});s.LevelSales=12;
            string tripping=FestivalHudText.Tripping(s,you.Id),quota=FestivalHudText.ObjectiveTitle(s,you),hint=FestivalHudText.QuotaHint(s,you);
            Assert.That(tripping,Does.StartWith("YOU TRIP"));Assert.That(quota,Does.Contain("$12"));
            s.Phase=FestivalSimulation.MinigamePhase;
            Assert.That(FestivalHudText.Tripping(s,you.Id),Is.EqualTo(tripping),"dose card during the game");
            Assert.That(FestivalHudText.ObjectiveTitle(s,you),Is.EqualTo(quota),"quota during the game");
            Assert.That(FestivalHudText.ObjectiveDetail(s,you),Is.EqualTo(hint),"the quota line, without a step the pause refuses");
            s.Booth.ScoredAt=4;s.LevelSales=17;
            Assert.That(FestivalHudText.Tripping(s,you.Id),Is.EqualTo(tripping),"dose card during the banner");
            Assert.That(FestivalHudText.ObjectiveTitle(s,you),Is.EqualTo("DAY QUOTA  $17 / $30"),"payout is immediate during banner");
            sam.Life="Spirit";
            Assert.That(FestivalHudText.ObjectiveTitle(s,sam),Is.EqualTo(FestivalGuidance.Headline(s,sam)),"a spirit keeps its own guidance");
        }

        // By day the called pot is split as the host splits it: each share, then the odd amount to the stash, or the whole pot to the
        // stash when the crew outnumbers it; always in the viewer's money.
        [TestCase(Festivals.PoloFestival)][TestCase(Festivals.PlayaFestival)]
        public void Mini6CalledPotSaysEveryShareAndTheStashRemainder(int festival)
        {
            var s=Level(out var you,out var sam);s.FestivalIndex=festival;you.Ordinal=2;s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;b.ScoredAt=12;b.Seconds=12.5;
            string M(int amount)=>FestivalHudText.Money(s,you,amount);const string again=" The festival starts again in 4.";
            foreach(var (crew,pot,line) in new[]{(1,5,"Called it: "+M(5)+" each, counted as sales."),(3,15,"Called it: "+M(5)+" each, counted as sales."),
                (2,15,"Called it: "+M(7)+" each, "+M(1)+" to the stash, counted as sales."),(3,35,"Called it: "+M(11)+" each, "+M(2)+" to the stash, counted as sales."),
                (8,5,"Called it: the whole "+M(5)+" pot goes to the stash, counted as sales."),(8,60,"Called it: "+M(7)+" each, "+M(4)+" to the stash, counted as sales.")})
            {
                b.PlayerIds.Clear();for(int i=0;i<crew;i++)b.PlayerIds.Add("p"+i);b.Pot=pot;
                Assert.That(Card(s,you),Is.EqualTo(("+"+M(pot),line+again)),crew+" players calling a "+pot+" pot");
            }
            s.LevelIndex=1;Assert.That(Card(s,you).Item2,Is.EqualTo("Called it: "+M(60)+" to the stash."+again),"night banks it all");
        }

        // A spirit's view lists only spirits, so cups names who is up and who drank the spiked cup from the roster captured at the
        // start; an older snapshot without one still reads "A friend".
        [Test]public void Mini6SpiritWatchersReadCapturedNamesForTurnsAndTheSpikedCup()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;
            b.PlayerIds.AddRange(new[]{"you","sam"});b.Cups=4;b.Turns.AddRange(new[]{"sam","you"});b.TurnAt=2;b.Seconds=4.6;
            var ghost=new PlayerState{Id="ghost",Name="Ghost",Life="Spirit"};var spiritView=new RoundState{Phase=s.Phase,Booth=b,DurationSeconds=s.DurationSeconds};spiritView.Players.Add(ghost);
            Assert.That(Card(spiritView,ghost).Item2,Does.StartWith("A friend's turn"),"an older snapshot has no roster");
            b.Participants.Add(new MinigameParticipant{Id="you",Name="You"});b.Participants.Add(new MinigameParticipant{Id="sam",Name="Sam"});
            Assert.That(Card(spiritView,ghost),Is.EqualTo(Card(s,you)),"the spirit reads Sam's turn as the living do");
            b.ScoredAt=6;b.SpikedBy="sam";b.Pot=5;
            Assert.That(Card(spiritView,ghost).Item2,Does.StartWith("Sam drank the spiked cup"),"and who drank the spiked cup");
            sam.Connected=false;Assert.That(Card(s,you).Item2,Does.StartWith("Sam drank the spiked cup"),"a friend who left still has a name");
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

        // MINI-4, Mystery Cups under way, for everyone at the table: the pot, who is up and their seconds, what the next safe cup adds
        // and what calling now banks, and how many cups are left. To the tripper alone (whose view alone carries the hint), the cup their
        // hunch marks while it is on the table, with how often a hunch like theirs is right, never as fact.
        [Test]public void MysteryCupsShowsThePotWhoIsUpAndOnlyTheTrippersHunch()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;
            b.PlayerIds.AddRange(new[]{"you","sam"});b.Cups=4;b.Turns.AddRange(new[]{"sam","you"});b.TurnAt=2;b.Seconds=4.6;
            var ghost=new PlayerState{Id="ghost",Name="Ghost",Life="Spirit"};s.Players.Add(ghost);
            Assert.That(Card(s,you),Is.EqualTo(("MYSTERY CUPS  •  POT $0","Sam's turn: 8 s. A safe cup adds $5; calling it banks $0.\n1 spiked among 4 left.")),"Sam is up");
            Assert.That(Card(s,ghost),Is.EqualTo(Card(s,you)),"a spirit watches the same");
            Assert.That(Card(s,sam),Is.EqualTo(("MYSTERY CUPS  •  POT $0","Your turn: 8 s. Drink a cup for $5, or call it and bank $0.\n1 spiked among 4 left.")),"Sam's own card");
            b.Drunk.Add(2);b.Pot=5;b.Turn=1;b.TurnAt=5;b.Seconds=6;
            Assert.That(Card(s,you),Is.EqualTo(("MYSTERY CUPS  •  POT $5","Your turn: 9 s. Drink a cup for $10, or call it and bank $5.\n1 spiked among 3 left.")),"a safe cup later, you are up");
            b.Hint=3;s.TripperId=you.Id;
            foreach(var (dose,odds) in new[]{(1,"about 3 times in 4"),(2,"about half the time"),(3,"about 1 time in 4"),(4,"about 1 time in 10")})
            {
                s.Doses.Clear();s.Doses.Add(new PlayerDose{PlayerId=you.Id,Dose=dose});
                Assert.That(Card(s,you).Item2,Does.EndWith("1 spiked among 3 left. Your trip says cup 4… trips like yours are right "+odds+"."),"dose "+dose+": the hunch, numbered as the cups are, says how often it is right");
                Assert.That(Card(s,you).Item2,Does.Not.Contain("is spiked"),"a hunch never reads as the answer");
            }
            Assert.That(Card(s,sam).Item2,Does.EndWith("Your trip says cup 4… trips like yours are right about 1 time in 10."),"whoever's turn it is");
            b.PlayerIds.Remove("sam");s.Doses[0].Dose=1;
            Assert.That(Card(s,you).Item2,Does.EndWith("trips like yours are right about half the time."),"alone at the table a hunch is one step less sure");
            b.PlayerIds.Add("sam");b.Hint=2;Assert.That(Card(s,you).Item2,Does.EndWith("1 spiked among 3 left."),"once that cup is drunk the hunch is gone with it");
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
            Assert.That(Card(s,you),Is.EqualTo(("+$15","Called it: $7 each, $1 to the stash, counted as sales."+again)),"called by day, split across the crew, the odd dollar banked");
            Assert.That(FestivalHudText.MinigameWon(s),Is.True,"a win");
            s.LevelIndex=1;Assert.That(Card(s,you),Is.EqualTo(("+$15","Called it: $15 to the stash."+again)),"called at night, banked");
            b.Pot=0;Assert.That(Card(s,you),Is.EqualTo(("+$0","Called it with nothing in the pot."+again)),"called before a cup was drunk");
        }

        // Before the first turn's clock starts everyone at the booth reads the rules, and the tripper's hunch waits. The player up may
        // drink at once, which ends the intro. Only the player up may call it, so the rules say so.
        [Test]public void MysteryCupsOpensWithItsRules()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;s.TripperId=you.Id;b.Hint=1;
            b.PlayerIds.AddRange(new[]{"you","sam"});b.Cups=4;b.Turns.AddRange(new[]{"sam","you"});b.Seconds=.5;b.TurnAt=FestivalSimulation.CupsIntroSeconds;
            const string rules="One of these 4 cups is spiked. Each safe cup grows the pot.\nOn your turn, call it to bank the pot.\nThe spiked cup loses the pot and doses whoever drinks it.";
            Assert.That(Card(s,you),Is.EqualTo(("MYSTERY CUPS",rules)),"the tripper reads the rules, the hunch not yet");
            Assert.That(Card(s,sam),Is.EqualTo(Card(s,you)),"and so does the player up");
            Assert.That(rules.Split('\n').Length,Is.EqualTo(3),"in three lines");
            b.Seconds=FestivalSimulation.CupsIntroSeconds;Assert.That(Card(s,you).Item1,Is.EqualTo("MYSTERY CUPS  •  POT $0"),"then the game's own card");
            b.Seconds=1;b.Drunk.Add(0);b.Pot=5;b.Turn=1;b.TurnAt=1;Assert.That(Card(s,you).Item1,Is.EqualTo("MYSTERY CUPS  •  POT $5"),"a cup drunk early ends it");
        }

        // The tripper's end card says whether the hunch was right only as far as the drunk cups show it, since the spiked cup never
        // leaves the host.
        [Test]public void MysteryCupsEndCardSaysWhetherTheHunchWasRight()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;s.TripperId=you.Id;
            b.PlayerIds.AddRange(new[]{"you","sam"});b.Cups=4;b.Turns.AddRange(new[]{"sam","you"});b.ScoredAt=12;b.Seconds=12.5;
            const string again=" The festival starts again in 4.";
            b.Hint=2;b.Drunk.AddRange(new[]{0,2});b.Pot=5;b.SpikedBy="sam";
            Assert.That(Card(s,you).Item2,Is.EqualTo("Sam drank the spiked cup and lost the $5 pot. Your hunch was right: cup 3 was spiked."+again),"the spiked cup drunk was the hunch");
            b.Hint=3;Assert.That(Card(s,you).Item2,Is.EqualTo("Sam drank the spiked cup and lost the $5 pot. Your hunch was wrong: cup 3 was spiked."+again),"it was another");
            b.Hint=-1;Assert.That(Card(s,sam).Item2,Is.EqualTo("You drank the spiked cup and lost the $5 pot."+again),"a view with no hunch has no hunch line");
            b.SpikedBy="";b.Drunk.Clear();b.Drunk.AddRange(new[]{0,1,3});b.Pot=30;b.Hint=2;
            Assert.That(Card(s,you).Item2,Is.EqualTo("Called it: $15 each, counted as sales. Your hunch was right: cup 3 was spiked."+again),"only the hunch cup was left");
            b.Drunk.Clear();b.Drunk.Add(2);b.Pot=5;
            Assert.That(Card(s,you).Item2,Is.EqualTo("Called it: $2 each, $1 to the stash, counted as sales. Your hunch was wrong: cup 3 was safe."+again),"the hunch cup was drunk safely");
            b.Drunk.Clear();b.Drunk.Add(0);
            Assert.That(Card(s,you).Item2,Is.EqualTo("Called it: $2 each, $1 to the stash, counted as sales. Nobody drank cup 3, so your hunch stays a guess."+again),"called with the hunch cup still on the table");
        }

        // At 1920x1080, at normal and the largest text, the longest Mystery Cups cards fit the minigame card uncut: a crew of eight with
        // long names, a hunch and a $1,000 pot.
        [Test]public void EveryMysteryCupsCardFitsTheMinigameCard()
        {
            var root=new GameObject("Cups card fit HUD");
            try
            {
                var hud=root.AddComponent<FestivalHud>();typeof(FestivalHud).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);canvas.transform.localScale=Vector3.one;
                Text title=null,detail=null;foreach(var t in root.GetComponentsInChildren<Text>(true)){if(t.name=="Minigame title")title=t;if(t.name=="Minigame detail")detail=t;}
                Assert.That(detail,Is.Not.Null,"setup: the minigame card's detail");Canvas.ForceUpdateCanvases();
                var s=Level(out var you,out _);s.Phase=FestivalSimulation.MinigamePhase;s.TripperId=you.Id;s.Doses.Add(new PlayerDose{PlayerId=you.Id,Dose=4});var b=s.Booth;
                for(int i=0;i<7;i++){var p=new PlayerState{Id="long"+i,Name="Long Festival Friend "+i};s.Players.Add(p);b.PlayerIds.Add(p.Id);b.Turns.Add(p.Id);}
                b.PlayerIds.Add(you.Id);b.Turns.Add(you.Id);b.Cups=FestivalSimulation.CupsFor(8);b.Hint=9;
                var cards=new List<(string,string)>();
                b.Seconds=.5;b.TurnAt=3;cards.Add(Card(s,you));
                b.Seconds=3;b.Pot=1000;b.Drunk.AddRange(new[]{0,1,2});cards.Add(Card(s,you));
                b.Turn=7;cards.Add(Card(s,you));
                b.ScoredAt=4;b.Seconds=4;b.SpikedBy="long3";b.Drunk.Add(8);cards.Add(Card(s,you));
                b.SpikedBy="";b.Drunk.Clear();b.Drunk.Add(4);b.Pot=1003;cards.Add(Card(s,you));
                b.Drunk.Clear();cards.Add(Card(s,you));
                var measure=new TextGenerator();var cut=new List<string>();var reflow=typeof(FestivalHud).GetMethod("ApplyTextReflow",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach(float scale in new[]{1f,1.3f})
                {
                    reflow.Invoke(hud,new object[]{scale});
                    foreach(var (heading,line) in cards)
                    {
                        measure.Populate(line,detail.GetGenerationSettings(detail.rectTransform.rect.size));if(measure.characterCountVisible<line.Length)cut.Add(scale+"x in "+detail.rectTransform.rect.size+": "+line.Replace("\n"," | "));
                        measure.Populate(heading,title.GetGenerationSettings(title.rectTransform.rect.size));if(measure.characterCountVisible<heading.Length)cut.Add(scale+"x: "+heading);
                    }
                }
                Assert.That(cut,Is.Empty,"cut in the minigame card");
            }
            finally{Object.DestroyImmediate(root);UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();}
        }

        // E drinks the cup under the aim dot and F calls it, for the player who is up alone, until the game is scored.
        [Test]public void OnlyThePlayerUpDrinksOrCallsIt()
        {
            var s=Level(out var you,out var sam);s.Phase=FestivalSimulation.MinigamePhase;var b=s.Booth;
            b.PlayerIds.AddRange(new[]{"you","sam"});b.Cups=4;b.Turns.AddRange(new[]{"sam","you"});
            Assert.That(FestivalHudText.MinigamePrompt(s,sam,2),Is.EqualTo("E  DRINK CUP 3   •   F CALL IT"),"Sam, looking at cup 3");
            Assert.That(FestivalHudText.MinigamePrompt(s,sam,-1),Is.EqualTo("LOOK AT A CUP TO DRINK IT   •   F CALL IT"),"looking at none");
            Assert.That(FestivalHudText.MinigamePrompt(s,sam,2,"H"),Is.EqualTo("E  DRINK CUP 3   •   H CALL IT"),"the bound Chat key; the keycap carries E");
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

        // In test mode the level clock runs past the level's length. The clock reads NO LIMIT while the level plays or a game
        // runs, and no sundown warning shows, however far past the length it runs; the other phases read as ever.
        [Test]public void TestModesClockReadsNoLimitAndNeverWarnsOfSundown()
        {
            var s=Level(out var you,out _);s.TestMode=true;
            foreach(double past in new[]{-50.0,0,30,600})
            {
                s.ElapsedSeconds=s.DurationSeconds+past;
                foreach(var phase in new[]{"Playing",FestivalSimulation.MinigamePhase})
                {
                    s.Phase=phase;
                    Assert.That(FestivalHudText.Clock(s),Is.EqualTo("NO LIMIT"),phase+", "+past+" s past the length");
                    Assert.That(FestivalHudText.SundownStage(s),Is.Zero,phase+", "+past+" s past the length: no sundown warning");
                    Assert.That(FestivalHudText.SundownLine(s,you),Is.Empty);
                }
            }
            s.Phase="Results";Assert.That(FestivalHudText.Clock(s),Is.EqualTo("DONE"),"the results read DONE as ever");
            s.Phase="Playing";s.TestMode=false;s.ElapsedSeconds=s.DurationSeconds-50;
            Assert.That(FestivalHudText.Clock(s)=="0:50"&&FestivalHudText.SundownStage(s)==1,Is.True,"test mode off, the clock and the warning are back");
        }
    }
}
