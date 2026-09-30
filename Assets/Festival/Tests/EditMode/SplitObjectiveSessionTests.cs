using NUnit.Framework;
using Festival.Core;
using Festival.Network;

namespace Festival.Tests
{
    // CROWD-2: a big crew's clients see the second lost friend the way they see the first: once found, or once their trail is
    // finished, by the tripper or by anyone within sight of them. The second trail's clue chain never leaves the host.
    public sealed class SplitObjectiveSessionTests
    {
        // Five friends ready up for Night 1 and load into the crowd, then wait at the way back to camp, far from both friends.
        static FestivalSimulation BigNight()
        {
            var game=new FestivalSimulation(23);game.State.LevelIndex=1;
            for(int i=0;i<5;i++){var p=game.AddPlayer("p"+i,"P"+i);p.X=0;p.Z=19;game.Execute(p.Id,new GameCommand{Id="ready_"+i,Kind="Ready"});}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
            Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: Night 1 under way");
            Assert.That(game.State.SecondFriend.Active&&game.State.SecondFriend.ClueChain.Count>0,Is.True,"setup: five friends lose two, each with a trail");
            foreach(var p in game.State.Players){p.X=Festivals.CampGateX;p.Z=Festivals.CampGateZ;}
            game.HasLineOfSight=(ax,az,bx,bz)=>true;
            return game;
        }
        static bool Hidden(WorldPoint at)=>at.X==0&&at.Z==0;
        static bool Same(WorldPoint a,WorldPoint b)=>a.X==b.X&&a.Z==b.Z;

        [Test] public void EveryoneLearnsThereIsASecondFriendButNotTheirTrail()
        {
            var game=BigNight();game.State.SecondFriend.CluesRead=1;
            foreach(var viewer in game.State.Players)
            {
                var seen=FestivalSession.ViewFor(game,viewer.Id).SecondFriend;
                Assert.That(seen.Active&&seen.CluesRead==1&&!seen.GateOpened&&!seen.Found,Is.True,viewer.Id+" learns a second friend is lost and how far their trail has come");
                Assert.That(seen.ClueChain,Is.Empty,viewer.Id+" is never sent the second clue chain");
                Assert.That(Hidden(seen.Position),Is.True,viewer.Id+" does not know where the second friend is before their trail ends");
            }
            game.State.SecondFriend.Active=false;
            Assert.That(FestivalSession.ViewFor(game,"p0").SecondFriend.Active,Is.False,"a one-friend night says so");
        }

        [Test] public void TheSecondFriendShowsOnceTheirTrailEnds()
        {
            var game=BigNight();var second=game.State.SecondFriend;var tripper=game.Player(game.State.TripperId);
            var far=game.State.Players.Find(p=>p!=tripper);var near=game.State.Players.FindLast(p=>p!=tripper);
            near.X=second.Position.X+3;near.Z=second.Position.Z;second.GateOpened=true;
            Assert.That(Same(FestivalSession.ViewFor(game,tripper.Id).SecondFriend.Position,second.Position),Is.True,"the second trail's last link shows the tripper where the second friend is");
            Assert.That(Hidden(FestivalSession.ViewFor(game,far.Id).SecondFriend.Position),Is.True,"a sober friend far away still has to follow the tripper");
            Assert.That(Same(FestivalSession.ViewFor(game,near.Id).SecondFriend.Position,second.Position),Is.True,"a friend 3 m away sees them");
            Assert.That(Hidden(FestivalSession.ViewFor(game,tripper.Id).FriendPosition),Is.True,"the first friend stays hidden: their trail is not finished");
            near.Life="Spirit";
            Assert.That(Hidden(FestivalSession.ViewFor(game,near.Id).SecondFriend.Position),Is.True,"a spirit sees no lost friend");
        }

        [Test] public void AFoundSecondFriendIsSeenByTheLiving()
        {
            var game=BigNight();var second=game.State.SecondFriend;second.GateOpened=second.Found=true;second.LeaderId="p1";
            var spirit=game.Player("p4");spirit.Life="Spirit";
            foreach(var viewer in game.State.Players)
            {
                var seen=FestivalSession.ViewFor(game,viewer.Id).SecondFriend;
                if(viewer==spirit)Assert.That(!seen.Found&&seen.LeaderId==""&&Hidden(seen.Position),Is.True,"a spirit is not told about the escort");
                else Assert.That(seen.Found&&seen.LeaderId=="p1"&&Same(seen.Position,second.Position),Is.True,viewer.Id+" sees the found friend and their escort");
            }
        }
    }
}
