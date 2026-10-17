using Festival.Core;
using Festival.Network;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // Every view, a spirit's included, names who grabbed the level's Giggle Tank. Where it stood stays with the living, and a
    // carrier's bank receipt reaches only that carrier.
    public sealed class GiggleTankSessionTests
    {
        static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        // A crew of three at the festival, the tank at spot 2, grabbed by "carrier".
        static FestivalSimulation Grabbed()
        {
            var game=new FestivalSimulation(5);game.AddPlayer("host","Host");game.AddPlayer("carrier","Tank Grabber");game.AddPlayer("ghost","Ghost");
            game.State.Phase="Playing";game.State.GiggleTankSpot=2;
            var carrier=game.Player("carrier");var at=FestivalSimulation.GiggleTankAt(game.State);carrier.X=at.X;carrier.Z=at.Z;
            Assert.That(game.Execute("carrier",new GameCommand{Id="grab",Kind=FestivalSimulation.GrabGiggleTankKind}).Accepted,Is.True,"setup: the carrier grabs the tank");
            return game;
        }

        [Test]public void EveryViewNamesTheGrabberWhileOnlyTheLivingSeeTheTank()
        {
            var game=Grabbed();game.Player("ghost").Life="Spirit";
            foreach(var viewer in new[]{"host","carrier","ghost"})
            {
                var view=Wire(game,viewer);
                Assert.That(view.GiggleTankGrabberId,Is.EqualTo("carrier"),viewer+"'s view names who grabbed it");
                Assert.That(view.GiggleTankGrabberName,Is.EqualTo("Tank Grabber"),viewer+"'s view carries their name");
            }
            var spirit=Wire(game,"ghost");
            Assert.That(spirit.Players.Exists(p=>p.Id=="carrier"),Is.False,"the spirit still sees no living player");
            Assert.That(spirit.GiggleTankSpot==-1&&!spirit.GiggleTankFound,Is.True,"nor where the tank stood");
            var living=Wire(game,"host");
            Assert.That(living.GiggleTankSpot==2&&living.GiggleTankFound&&FestivalSimulation.GiggleTankAt(living)==null,Is.True,"the living see it gone from its spot");
            Assert.That(living.Players.Find(p=>p.Id=="carrier").Inventory,Is.Empty,"and never what a friend carries");
        }

        [Test]public void OnlyTheCarrierSeesTheirBankReceipt()
        {
            var game=Grabbed();var carrier=game.Player("carrier");int stash=game.State.StashCash;
            carrier.X=Festivals.CampGateX+Festivals.CampGateRadius-.2f;carrier.Z=Festivals.CampGateZ;game.Tick(.1);
            Assert.That(game.State.StashCash,Is.EqualTo(stash+Festivals.GiggleTankCash),"setup: the carrier banks the tank at the camp gate");
            var own=Wire(game,"carrier");
            Assert.That(own.Players.Find(p=>p.Id=="carrier").GiggleTankBankSequence,Is.EqualTo(1),"the carrier's own view carries their receipt");
            Assert.That(own.StashCash,Is.EqualTo(stash+Festivals.GiggleTankCash),"beside the crew's stash");
            var friend=Wire(game,"host");
            Assert.That(friend.Players.Find(p=>p.Id=="carrier").GiggleTankBankSequence,Is.EqualTo(0),"a friend's view masks it");
            Assert.That(friend.StashCash,Is.EqualTo(stash+Festivals.GiggleTankCash),"while everyone sees the stash rise");
        }

        [Test]public void OlderSnapshotsHaveNoGrabberOrReceipt()
        {
            var old=JsonUtility.FromJson<RoundState>("{\"Phase\":\"Playing\",\"StashCash\":5,\"Players\":[{\"Id\":\"p0\",\"Name\":\"Old\"}]}");
            Assert.That(old.GiggleTankGrabberId==""&&old.GiggleTankGrabberName=="",Is.True,"a snapshot from before carried tanks names no grabber");
            Assert.That(old.Players[0].GiggleTankBankSequence,Is.EqualTo(0),"and holds no receipt");
        }
    }
}
