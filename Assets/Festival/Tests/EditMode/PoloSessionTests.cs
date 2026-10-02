using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Tests
{
    // POLO-1: every client sees who is filming and who guards the VIP rope; only a Ferris wheel rider's client learns, at night,
    // roughly where the lost friends are (POLO-3: the square each stands in, never their spot).
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
        // A crew member who is not tripping (or the tripper) boards the wheel.
        static PlayerState Ride(FestivalSimulation game,bool tripper=false)
        {
            var rider=game.State.Players.Find(p=>(p.Id==game.State.TripperId)==tripper);rider.X=Festivals.WheelX;rider.Z=Festivals.WheelZ+1;
            var boarded=game.Execute(rider.Id,new GameCommand{Id="ride_"+rider.Id+"_"+game.State.Tick,Kind="RideWheel"});
            Assert.That(boarded.Accepted,Is.True,"setup: "+rider.Id+" rides the wheel: "+boarded.Reason);
            return rider;
        }
        static bool Hidden(WorldPoint p)=>p.X==0&&p.Z==0;
        static string At(WorldPoint p)=>p.X+","+p.Z;
        // A view's wheel areas, written out as "x,z x,z".
        static string Areas(RoundState view)=>string.Join(" ",view.WheelFriendAreas.ConvertAll(At));

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
                Assert.That(Hidden(Wire(game,viewer.Id).FriendPosition)&&Hidden(Wire(game,viewer.Id).SecondFriend.Position)&&Areas(Wire(game,viewer.Id))=="",Is.True,"setup: nobody's client knows where the friends are yet");
            var rider=Ride(game);var mine=Wire(game,rider.Id);
            Assert.That(Hidden(mine.FriendPosition)&&Hidden(mine.SecondFriend.Position),Is.True,"POLO-3: from the wheel the rider's client never learns either friend's spot: got "+At(mine.FriendPosition)+" and "+At(mine.SecondFriend.Position));
            Assert.That(Areas(mine),Is.EqualTo(At(FestivalSimulation.WheelArea(game.State.FriendPosition))+" "+At(FestivalSimulation.WheelArea(second.Position))),"only the square each friend is lost in, the first friend's then the second's");
            Assert.That(Areas(mine),Is.Not.EqualTo(At(game.State.FriendPosition)+" "+At(second.Position)),"setup: neither friend stands at their square's centre");
            Assert.That(game.State.WheelFriendAreas,Is.Empty,"the host's own state never holds them");
            Assert.That(FestivalSimulation.OnWheel(mine,rider.Id),Is.True,"the rider's client knows it is on the wheel");
            Assert.That(mine.Npcs.FindAll(n=>n.Kind=="Cop").Count,Is.EqualTo(game.State.Npcs.FindAll(n=>n.Kind=="Cop").Count),"with every cop in its view to mark");
            foreach(var viewer in game.State.Players)
            {
                if(viewer==rider)continue;var theirs=Wire(game,viewer.Id);
                Assert.That(Hidden(theirs.FriendPosition)&&Hidden(theirs.SecondFriend.Position)&&Areas(theirs)=="",Is.True,viewer.Id+"'s client learns nothing from someone else's ride");
                Assert.That(FestivalSimulation.OnWheel(theirs,rider.Id),Is.False,viewer.Id+"'s client can't tell the rider is up there looking");
            }
            game.Tick(Festivals.WheelRideSeconds+.1);
            Assert.That(Hidden(Wire(game,rider.Id).FriendPosition)&&Areas(Wire(game,rider.Id))=="",Is.True,"the view ends with the ride");
        }

        // POLO-3: the wheel takes away no reason a client already had to know a friend's spot. The tripper, once their trail is
        // done, still has the first friend's spot while riding, and the rough areas besides.
        [Test] public void TheTripperWithTheTrailDoneStillSeesTheSpotFromTheWheel()
        {
            var game=Level(1);game.State.GateOpened=true;var tripper=Ride(game,tripper:true);var friend=game.State.FriendPosition;
            Assert.That(tripper.Id,Is.EqualTo(game.State.TripperId),"setup: the tripper rides");
            var mine=Wire(game,tripper.Id);
            Assert.That(At(mine.FriendPosition),Is.EqualTo(At(friend)),"the trail's last link still shows the tripper where the friend is");
            Assert.That(Areas(mine),Is.EqualTo(At(FestivalSimulation.WheelArea(friend))),"and the wheel shows their square");
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

        // POLO-5: every client draws a rider in the gondola they boarded (FestivalTwistVisuals), so every client's copy of the
        // rider, the rider's own included, carries when the turn began, and nobody's does once it is over.
        [Test] public void EveryClientKnowsWhenTheRiderBoardedTheWheel()
        {
            var game=Level(0);
            foreach(var viewer in game.State.Players)Assert.That(Wire(game,viewer.Id).Players.TrueForAll(p=>p.VisualWheelSince==-1),Is.True,"setup: nobody rides yet, so "+viewer.Id+"'s client seats nobody");
            var rider=Ride(game);var turn=game.State.Interactions.Find(i=>i.Id==rider.InteractionId);game.Tick(5);
            foreach(var viewer in game.State.Players)
            {
                var view=Wire(game,viewer.Id);
                Assert.That(view.Players.Find(p=>p.Id==rider.Id).VisualWheelSince,Is.EqualTo(turn.StartSeconds).Within(1e-6),viewer.Id+"'s client knows when "+rider.Id+" boarded, so it seats them in the same gondola as everyone else's");
                Assert.That(view.Players.FindAll(p=>p.Id!=rider.Id).TrueForAll(p=>p.VisualWheelSince==-1),Is.True,viewer.Id+"'s client seats nobody else");
            }
            Assert.That(rider.VisualWheelSince,Is.EqualTo(-1),"the host's own state never carries it");
            game.Tick(Festivals.WheelRideSeconds);
            Assert.That(FestivalSimulation.OnWheel(game.State,rider.Id),Is.False,"setup: the turn is over");
            foreach(var viewer in game.State.Players)Assert.That(Wire(game,viewer.Id).Players.Find(p=>p.Id==rider.Id).VisualWheelSince,Is.EqualTo(-1),viewer.Id+"'s client lets "+rider.Id+" off the wheel");
        }

        // POLO-6: an influencer filming you is no secret to you: your card says ON CAMERA, and the phone that has you shows REC. So
        // your own copy in your own view names every influencer filming you. Nobody else's view says so, and the host never holds it.
        [Test] public void OnlyThePlayerOnCameraKnowsWhoIsFilmingThem()
        {
            var game=Level(0);var me=game.State.Players[0];var cam=game.State.Npcs.Find(n=>n.Twist==FestivalSimulation.Influencer);
            double yaw=cam.Yaw*System.Math.PI/180;
            void Stand(PlayerState p,double ahead){p.X=cam.X+(float)(ahead*System.Math.Sin(yaw));p.Z=cam.Z+(float)(ahead*System.Math.Cos(yaw));}
            // Me two metres in front of the phone, the rest of the crew two metres behind it.
            Stand(me,2);foreach(var p in game.State.Players)if(p!=me)Stand(p,-2);
            Assert.That(game.State.Npcs.FindAll(n=>game.Films(n,me)),Is.EqualTo(new[]{cam}),"setup: "+cam.Id+", and only "+cam.Id+", films me");
            Assert.That(game.State.Players.TrueForAll(p=>p==me||!game.State.Npcs.Exists(n=>game.Films(n,p))),Is.True,"setup: nobody films the rest of the crew");
            foreach(var viewer in game.State.Players)
            {
                var view=Wire(game,viewer.Id);
                Assert.That(view.Players.Find(p=>p.Id==me.Id).VisualFilmedBy,Is.EqualTo(viewer==me?new[]{cam.Id}:new string[0]),viewer==me?"my client knows "+cam.Id+" has me on camera":viewer.Id+"'s client can't tell I'm on camera");
                Assert.That(view.Players.TrueForAll(p=>p.Id==me.Id||p.VisualFilmedBy.Count==0),Is.True,viewer.Id+"'s client has nobody else on camera");
            }
            Assert.That(me.VisualFilmedBy,Is.Empty,"the host's own state never holds it");
            Stand(me,-3);
            Assert.That(Wire(game,me.Id).Players.Find(p=>p.Id==me.Id).VisualFilmedBy,Is.Empty,"behind the phone, I'm off camera");
        }

        [Test] public void ByDayTheWheelShowsNoFriend()
        {
            var game=Level(0);var rider=Ride(game);
            Assert.That(Hidden(Wire(game,rider.Id).FriendPosition)&&Areas(Wire(game,rider.Id))=="",Is.True,"nobody is lost by day");
        }
    }
}
