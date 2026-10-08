using System.Collections;
using System.Linq;
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
            game.State.Phase="Playing";game.State.Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=1,RingSeconds=1.5};game.State.MinigameBag.Add("Later");
            var living=Wire(game,"p1");
            Assert.That(new object[]{living.Booth.Kind,living.Booth.Spot,living.Booth.RingSeconds},Is.EqualTo(new object[]{FestivalSimulation.MysteryCupsGame,1,1.5}),"a living player's view carries the booth, its game and the ring's countdown");
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

        // MINI-4: a Mystery Cups game is public, all but two parts: which cup is spiked never leaves the host, and the cup the
        // tripper is shown as spiked reaches only the tripper's client. Older snapshots read as no cups poured.
        [Test] public void OnlyTheTrippersClientIsShownTheHintAndNoClientTheSpikedCup()
        {
            var game=new FestivalSimulation(7);game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");game.AddPlayer("p2","P2");
            game.State.Phase=FestivalSimulation.MinigamePhase;game.State.TripperId="p0";
            var b=game.State.Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=1,Cups=5,Spiked=2,Hint=3,Pot=5,Turn=1,TurnAt=4.5};
            b.PlayerIds.AddRange(new[]{"p0","p1","p2"});b.Turns.AddRange(new[]{"p1","p0","p2"});b.Drunk.Add(0);
            var tripper=Wire(game,"p0");var friend=Wire(game,"p1");game.Player("p2").Life="Spirit";var spirit=Wire(game,"p2");
            Assert.That(tripper.Booth.Hint,Is.EqualTo(3),"the tripper's client is shown the cup they see as spiked");
            Assert.That(new[]{friend.Booth.Hint,spirit.Booth.Hint},Is.EqualTo(new[]{-1,-1}),"nobody else's is");
            Assert.That(new[]{tripper.Booth.Spiked,friend.Booth.Spiked,spirit.Booth.Spiked},Is.EqualTo(new[]{-1,-1,-1}),"and no client is told which cup is spiked");
            foreach(var view in new[]{tripper,friend,spirit})
                Assert.That(new object[]{view.Booth.Kind,view.Booth.Cups,view.Booth.Pot,view.Booth.Turn,view.Booth.TurnAt,string.Join(",",view.Booth.Turns),string.Join(",",view.Booth.Drunk),view.Booth.SpikedBy},
                    Is.EqualTo(new object[]{FestivalSimulation.MysteryCupsGame,5,5,1,4.5,"p1,p0,p2","0",""}),"every client sees the rest of the game, to play or watch it");
            Assert.That(new[]{b.Spiked,b.Hint},Is.EqualTo(new[]{2,3}),"the host's own game keeps both");
            var old=JsonUtility.FromJson<RoundState>("{\"Phase\":\"Minigame\",\"Booth\":{\"Kind\":\"Stub\",\"Spot\":1}}").Booth;
            Assert.That(new object[]{old.Cups,old.Spiked,old.Hint,old.Drunk.Count,old.Turns.Count},Is.EqualTo(new object[]{0,-1,-1,0,0}),"a snapshot from before Mystery Cups has none poured");
        }

        [Test] public void ASpiritGetsCapturedNamesWithoutLivingPlayerStateOrPrivateAnswers()
        {
            var game=StartedWithSpirit();var b=game.State.Booth;
            var field=typeof(MinigameState).GetField("Participants");Assert.That(field,Is.Not.Null,"common start captures the minimal public participant roster");
            var host=(IList)field.GetValue(b);Assert.That(host.Count,Is.EqualTo(2));
            Assert.That(host[0].GetType().GetFields().Select(f=>f.Name),Is.EquivalentTo(new[]{"Id","Name"}),"no inventory, cash, effects, roles or answers on name records");
            game.Player("p0").Name="Changed";game.Player("p1").Connected=false;
            foreach(string viewer in new[]{"p0","p1","ghost"})
            {
                var view=Wire(game,viewer);var names=(IList)field.GetValue(view.Booth);
                Assert.That(names.Count,Is.EqualTo(2));
                Assert.That(names[0].GetType().GetField("Name").GetValue(names[0]),Is.EqualTo("P0"),"immutable captured name");
                Assert.That(view.Booth.Spiked,Is.EqualTo(-1));Assert.That(view.Booth.Hint,Is.EqualTo(viewer==game.State.TripperId?b.Hint:-1));
                Assert.That(view.DealSeed,Is.Zero);
                if(viewer=="ghost")Assert.That(view.Players.Select(p=>p.Id),Is.EqualTo(new[]{"ghost"}),"spirit player visibility stays filtered");
            }
            var detached=FestivalSession.ViewFor(game,"ghost");var detachedNames=(IList)field.GetValue(detached.Booth);
            detachedNames[0].GetType().GetField("Name").SetValue(detachedNames[0],"Tampered");detachedNames.Clear();detached.Booth.PlayerIds.Clear();detached.Booth.Turns.Clear();
            Assert.That(host.Count,Is.EqualTo(2));Assert.That(host[0].GetType().GetField("Name").GetValue(host[0]),Is.EqualTo("P0"));
            Assert.That(b.PlayerIds.Count,Is.EqualTo(2));Assert.That(b.Turns.Count,Is.EqualTo(2));
            var old=JsonUtility.FromJson<RoundState>("{\"Phase\":\"Minigame\",\"Booth\":{\"Kind\":\"MysteryCups\",\"PlayerIds\":[\"p0\"]}}");
            Assert.That(((IList)field.GetValue(old.Booth)).Count,Is.Zero,"missing legacy names default empty");
        }

        private static FestivalSimulation StartedWithSpirit()
        {
            var game=new FestivalSimulation(157){DealSecret=()=>12345};game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");var ghost=game.AddPlayer("ghost","Watcher");ghost.Connected=false;
            foreach(var p in game.State.Players.Where(p=>p.Connected)){p.X=0;p.Z=19;Assert.That(game.Execute(p.Id,new GameCommand{Id="ready"+p.Id,Kind="Ready"}).Accepted,Is.True);}
            game.Tick(5.2);game.Tick(game.State.SpinEndsAt-game.State.SimulationSeconds+.1);
            foreach(var p in game.State.Players.Where(p=>p.Connected))Assert.That(game.Execute(p.Id,new GameCommand{Id="map"+p.Id,Kind="MapReady"}).Accepted,Is.True);
            game.State.Npcs.Clear();ghost.Connected=true;ghost.Life="Spirit";
            var at=FestivalSimulation.BoothAt(game.State);foreach(var p in game.State.Players){p.X=at.X;p.Z=at.Z;}
            for(int k=0;k<35&&game.State.Phase!=FestivalSimulation.MinigamePhase;k++)game.Tick(.1);
            Assert.That(game.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));return game;
        }

        // What viewer's client receives, through the wire format.
        private static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
    }
}
