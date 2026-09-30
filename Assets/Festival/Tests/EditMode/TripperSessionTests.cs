using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Tests
{
    // TRIP-1: the spin result is public, so every client animates the same wheels and sees who is tripping.
    public sealed class TripperSessionTests
    {
        [Test] public void EveryViewerSeesTheSameSpin()
        {
            var game=new FestivalSimulation(17);game.State.LevelIndex=3;
            foreach(var id in new[]{"host","friend","ghost"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2);
            Assert.That(game.State.Phase,Is.EqualTo("Spinning"),"setup: Night 2 spin under way");
            var tripper=game.Player(game.State.TripperId);var ghost=game.State.Players.Find(p=>p!=tripper);ghost.Life="Spirit";
            foreach(var viewer in game.State.Players)
            {
                var view=FestivalSession.ViewFor(game,viewer.Id);
                Assert.That(view.TripperId,Is.EqualTo(tripper.Id),viewer.Id+" sees who trips");
                Assert.That(view.SpinSeed,Is.EqualTo(game.State.SpinSeed),viewer.Id+" animates the same wheels");
                Assert.That(view.SpinEndsAt,Is.EqualTo(game.State.SpinEndsAt),viewer.Id+" lands the wheels at the same moment");
                Assert.That(view.Doses.ConvertAll(d=>d.PlayerId+":"+d.Dose),Is.EqualTo(game.State.Doses.ConvertAll(d=>d.PlayerId+":"+d.Dose)),viewer.Id+" sees every Night 2 dose");
                Assert.That(view.TripperBag,Is.Empty,viewer.Id+" is not told who is due next");
                if(viewer!=ghost)Assert.That(view.Players.Find(p=>p.Id==tripper.Id).VisualWideEyes,Is.True,viewer.Id+" sees the tripper's wide eyes");
            }
        }

        [Test] public void ItemPreviewRunsThroughTheSpin()
        {
            var session=new GameObject("Preview session").AddComponent<FestivalSession>();
            try{Assert.That(session.PreviewItem("stock_lsd"),Does.Contain("lsd effect started"),"the isolated preview waits out the spin before trying the item");}
            finally{Object.DestroyImmediate(session.gameObject);}
        }
    }
}
