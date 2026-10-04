using System.Collections;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class RhythmReleaseSessionTests:FestivalPlayModeTest
    {
        [UnityTest] public IEnumerator AHostedDanceKeepsItsReleaseAndScoresTheDrop()=>Release("Dance");
        [UnityTest] public IEnumerator AHostedTrustDanceUsesTheSameDropScore()=>Release("Conversation");

        private IEnumerator Release(string kind)
        {
            var world=new GameObject("impl:U10 release world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U10 release session");var session=host.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);
            for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
            game.State.LevelIndex=3;game.State.Npcs.Clear();player.X=0;player.Z=0;player.Effects.Clear();
            var partner=new NpcState{Id="release_partner",X=0,Z=1.5f,Yaw=0,CanTalk=true};
            if(kind=="Conversation")partner.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=40,LastSeenSeconds=game.State.SimulationSeconds+60});
            game.State.Npcs.Add(partner);session.Command(kind,partner.Id);
            var interaction=game.Interaction(player.InteractionId);Assert.That(interaction,Is.Not.Null,session.Message);
            Assert.That(kind=="Dance"||interaction.TrustDance,Is.True);
            RhythmChart chart=null;RhythmNote hold=null;
            for(int seed=0;seed<256&&hold==null;seed++)
            {
                interaction.ChartSeed=seed;chart=RhythmChart.For(interaction);
                if(chart.Notes.Count>=14)hold=chart.Notes.Find(note=>note.HoldSeconds>0);
            }
            Assert.That(hold,Is.Not.Null,"the fixture uses a real generated hold chart");
            var commands=new List<GameCommand>();
            foreach(var note in chart.Notes)commands.Add(new GameCommand{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
            commands.Add(new GameCommand{Direction=hold.Direction,TimeSeconds=hold.TimeSeconds+hold.HoldSeconds/2,Amount=1});
            commands.Sort((a,b)=>a.TimeSeconds.CompareTo(b.TimeSeconds));
            foreach(var command in commands)
            {
                for(float deadline=Time.realtimeSinceStartup+15;game.State.SimulationSeconds-interaction.StartSeconds<command.TimeSeconds&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(interaction.Status,Is.EqualTo("Active"),"the real session keeps the challenge active for every input");
                int count=interaction.Inputs.Count,steps=player.VisualDanceStepSequence,direction=player.VisualDanceStepDirection;
                session.Command("Rhythm",amount:command.Amount,direction:command.Direction,time:command.TimeSeconds);
                Assert.That(interaction.Inputs.Count,Is.EqualTo(count+1),session.Message);
                if(command.Amount==1)
                {
                    Assert.That((player.VisualDanceStepSequence,player.VisualDanceStepDirection),Is.EqualTo((steps,direction)),"letting go does not animate another step");
                    for(float deadline=Time.realtimeSinceStartup+5;Time.realtimeSinceStartup<deadline;)
                    {
                        var visible=session.State.Interactions.Find(i=>i.Id==interaction.Id);
                        if(visible!=null&&visible.Inputs.Count>count)break;
                        yield return null;
                    }
                    var input=session.State.Interactions.Find(i=>i.Id==interaction.Id).Inputs[count];
                    Assert.That(JsonUtility.ToJson(input),Does.Contain("\"Release\":true"),"the live owner snapshot retains the release flag in append order");
                }
            }
            for(float deadline=Time.realtimeSinceStartup+15;interaction.Status=="Active"&&interaction.Turns.Count==0&&Time.realtimeSinceStartup<deadline;)yield return null;
            double score=(chart.Notes.Count-1.0)/chart.Notes.Count;
            if(kind=="Dance")
            {
                Assert.That(interaction.Status,Is.EqualTo("Complete"));
                Assert.That(interaction.Score,Is.EqualTo(score).Within(1e-9),"an early release loses exactly one head's credit");
                Assert.That(player.LastRhythmScore,Is.EqualTo(score).Within(1e-9));
            }
            else
            {
                Assert.That(interaction.Turns.Count,Is.EqualTo(1),"the otherwise perfect trust dance still unlocks the talk");
                Assert.That(partner.Observers[0].Suspicion,Is.EqualTo(40+System.Math.Round(20-35*score,System.MidpointRounding.AwayFromZero)),"trust settlement uses the dropped-note score");
            }
        }
    }
}
