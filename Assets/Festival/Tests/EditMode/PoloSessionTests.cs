using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Tests
{
    // POLO-1: every client sees who is filming and who guards the VIP rope; only a Ferris wheel rider's client learns, at night,
    // where the lost friends are.
    public sealed class PoloSessionTests
    {
        // A crew of `crew` loads into a Palm Mirage level.
        static FestivalSimulation Level(int level,int crew=3)
        {
            var game=new FestivalSimulation(31);game.State.LevelIndex=level;
            for(int k=0;k<crew;k++){var id="p"+k;var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
            Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: the level is under way");
            return game;
        }
        // What viewer's client receives, through the wire format.
        static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        // A crew member who is not tripping boards the wheel.
        static PlayerState Ride(FestivalSimulation game)
        {
            var rider=game.State.Players.Find(p=>p.Id!=game.State.TripperId);rider.X=Festivals.WheelX;rider.Z=Festivals.WheelZ+1;
            var boarded=game.Execute(rider.Id,new GameCommand{Id="ride_"+rider.Id+"_"+game.State.Tick,Kind="RideWheel"});
            Assert.That(boarded.Accepted,Is.True,"setup: "+rider.Id+" rides the wheel: "+boarded.Reason);
            return rider;
        }
        static bool Hidden(WorldPoint p)=>p.X==0&&p.Z==0;

        [Test] public void EveryClientSeesTheTwistsInTheCrowd()
        {
            var game=Level(0);
            Assert.That(game.State.Npcs.Exists(n=>n.Twist!=""),Is.True,"setup: Palm Mirage casts its twists");
            foreach(var viewer in game.State.Players)
            {
                var view=Wire(game,viewer.Id);
                foreach(var npc in game.State.Npcs)Assert.That(view.Npcs.Find(n=>n.Id==npc.Id).Twist,Is.EqualTo(npc.Twist),viewer.Id+" sees what "+npc.Id+" is doing");
                Assert.That(view.Npcs.FindAll(n=>n.Twist==FestivalSimulation.Influencer).Count,Is.EqualTo(Festivals.Influencers),viewer.Id+" sees every influencer filming");
                Assert.That(view.Npcs.FindAll(n=>n.Twist==FestivalSimulation.VipGuard).Count,Is.EqualTo(1),viewer.Id+" sees the VIP guard");
            }
        }

        [Test] public void OnlyTheRiderSeesTheLostFriendsFromTheWheel()
        {
            var game=Level(1,5);var second=game.State.SecondFriend;
            Assert.That(second.Active,Is.True,"setup: a crew of five loses two friends");
            foreach(var viewer in game.State.Players)
                Assert.That(Hidden(Wire(game,viewer.Id).FriendPosition)&&Hidden(Wire(game,viewer.Id).SecondFriend.Position),Is.True,"setup: nobody's client knows where the friends are yet");
            var rider=Ride(game);var mine=Wire(game,rider.Id);
            Assert.That(mine.FriendPosition.X==game.State.FriendPosition.X&&mine.FriendPosition.Z==game.State.FriendPosition.Z,Is.True,"from the wheel the rider's client sees where the friend is");
            Assert.That(mine.SecondFriend.Position.X==second.Position.X&&mine.SecondFriend.Position.Z==second.Position.Z,Is.True,"and where the second friend is");
            Assert.That(FestivalSimulation.OnWheel(mine,rider.Id),Is.True,"the rider's client knows it is on the wheel");
            Assert.That(mine.Npcs.FindAll(n=>n.Kind=="Cop").Count,Is.EqualTo(game.State.Npcs.FindAll(n=>n.Kind=="Cop").Count),"with every cop in its view to mark");
            foreach(var viewer in game.State.Players)
            {
                if(viewer==rider)continue;var theirs=Wire(game,viewer.Id);
                Assert.That(Hidden(theirs.FriendPosition)&&Hidden(theirs.SecondFriend.Position),Is.True,viewer.Id+"'s client learns nothing from someone else's ride");
                Assert.That(FestivalSimulation.OnWheel(theirs,rider.Id),Is.False,viewer.Id+"'s client can't tell the rider is up there looking");
            }
            game.Tick(Festivals.WheelRideSeconds+.1);
            Assert.That(Hidden(Wire(game,rider.Id).FriendPosition),Is.True,"the view ends with the ride");
        }

        // POLO-2: nothing cancels a turn on the wheel (the host refuses it), so the rider's HUD offers no Cancel and counts the
        // turn down instead, in whole seconds rounded up like the level clock. Off the wheel there is no such line, and every
        // other ride or task, an Ember Playa art car included, still offers Cancel (hopping off).
        [Test] public void OnTheWheelTheHudCountsDownTheTurnAndOffersNoCancel()
        {
            var game=Level(0);var rider=Ride(game);
            foreach(var (after,left) in new[]{(0.0,"20"),(5.5,"15"),(14.4,"1")})
            {
                if(after>0)game.Tick(after);var view=Wire(game,rider.Id);var me=view.Players.Find(p=>p.Id==rider.Id);
                Assert.That(FestivalHudText.CanCancel(view,me),Is.False,"nothing cancels the turn, so nothing offers to");
                Assert.That(FestivalHudText.WheelPrompt(view,me,view.SimulationSeconds),Is.EqualTo("ON THE FERRIS WHEEL  •  "+left+" s"),"the rider reads how long the turn has left");
            }
            game.Tick(.2);var down=Wire(game,rider.Id);var landed=down.Players.Find(p=>p.Id==rider.Id);
            Assert.That(FestivalHudText.WheelPrompt(down,landed,down.SimulationSeconds),Is.EqualTo(""),"back on the ground, no wheel line");
            var friend=game.State.Players.Find(p=>p.Id!=rider.Id);var theirs=Wire(game,friend.Id);
            Assert.That(FestivalHudText.WheelPrompt(theirs,theirs.Players.Find(p=>p.Id==friend.Id),theirs.SimulationSeconds),Is.EqualTo(""),"nobody else reads it");

            var playa=new RoundState{Phase="Playing",FestivalIndex=Festivals.PlayaFestival};var aboard=new PlayerState{Id="me",InteractionId="ride"};playa.Players.Add(aboard);
            playa.Interactions.Add(new InteractionState{Id="ride",PlayerId="me",Kind=FestivalSimulation.RideCarKind,TargetId="0",DurationSeconds=600});
            Assert.That(FestivalHudText.CanCancel(playa,aboard),Is.True,"an art car rider can still hop off");
            Assert.That(FestivalHudText.WheelPrompt(playa,aboard,0),Is.EqualTo(""),"and reads no wheel line");
            aboard.InteractionId="";
            Assert.That(FestivalHudText.CanCancel(playa,aboard),Is.False,"with nothing under way there is nothing to cancel");
        }

        [Test] public void ByDayTheWheelShowsNoFriend()
        {
            var game=Level(0);var rider=Ride(game);
            Assert.That(Hidden(Wire(game,rider.Id).FriendPosition),Is.True,"nobody is lost by day");
        }
    }
}
