using NUnit.Framework;
using Festival.Core;
using Festival.Network;

namespace Festival.Tests
{
    // TRIP-2: visions reach only the tripper's client, and a vision's truth only once it is checked. Roles and the clue
    // chain never leave the host.
    public sealed class VisionSessionTests
    {
        // The host deals from `secret` (TRIP-7); null, none, as in tests and previews.
        static FestivalSimulation Night(System.Func<int> secret=null)
        {
            var game=new FestivalSimulation(23){DealSecret=secret};game.State.LevelIndex=1;
            foreach(var id in new[]{"host","friend","guest"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
            Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: Night 1 under way");
            Assert.That(game.State.Visions,Is.Not.Empty,"setup: the tripper has visions");
            Assert.That(game.State.ClueChain,Is.Not.Empty,"setup: the night has a clue chain");
            return game;
        }

        [Test] public void OnlyTheTrippersClientGetsVisions()
        {
            var game=Night();var tripper=game.Player(game.State.TripperId);
            var seen=FestivalSession.ViewFor(game,tripper.Id).Visions;
            Assert.That(seen.ConvertAll(v=>v.Id+v.Kind+v.NpcId+v.Tell),Is.EqualTo(game.State.Visions.ConvertAll(v=>v.Id+v.Kind+v.NpcId+v.Tell)),"the tripper gets every vision with its tell");
            Assert.That(seen.Exists(v=>v.IsTrue),Is.False,"no truth before a vision is checked");
            foreach(var viewer in game.State.Players)
            {
                if(viewer==tripper)continue;
                foreach(var life in new[]{"Alive","Detained","Spirit"})
                {
                    viewer.Life=life;
                    Assert.That(FestivalSession.ViewFor(game,viewer.Id).Visions,Is.Empty,"a "+life+" friend who is not tripping gets no visions");
                }
                viewer.Life="Alive";
            }
            tripper.Life="Spirit";
            Assert.That(FestivalSession.ViewFor(game,tripper.Id).Visions,Is.Empty,"a fallen tripper's spirit sees no visions");
        }

        [Test] public void TruthArrivesOnlyOnceChecked()
        {
            var game=Night();var tripper=game.Player(game.State.TripperId);
            var truth=game.State.Visions.Find(v=>v.IsTrue);truth.Confirmed=true;
            var seen=FestivalSession.ViewFor(game,tripper.Id).Visions;
            Assert.That(seen.Find(v=>v.Id==truth.Id).IsTrue,Is.True,"the checked vision's truth reaches the tripper");
            Assert.That(seen.FindAll(v=>v.IsTrue).Count,Is.EqualTo(1),"and no other vision's");
        }

        [Test] public void TheTrippersViewShowsTheFriendOnceTheTrailEnds()
        {
            var game=Night();var tripper=game.Player(game.State.TripperId);var friend=game.State.Players.Find(p=>p!=tripper);
            game.State.FriendPosition=new WorldPoint(25,24);tripper.X=friend.X=-20;tripper.Z=friend.Z=-20;
            var hidden=FestivalSession.ViewFor(game,tripper.Id).FriendPosition;
            Assert.That(hidden.X==0&&hidden.Z==0,Is.True,"the friend stays hidden while the trail is still being followed");
            game.State.GateOpened=true;
            var seen=FestivalSession.ViewFor(game,tripper.Id).FriendPosition;
            Assert.That(seen.X==25&&seen.Z==24,Is.True,"the trail's last link shows the tripper where the lost friend is");
            var far=FestivalSession.ViewFor(game,friend.Id).FriendPosition;
            Assert.That(far.X==0&&far.Z==0,Is.True,"a sober friend far away still has to follow the tripper");
        }

        [Test] public void RolesAndTheChainStayOnTheHost()
        {
            var game=Night();
            foreach(var viewer in game.State.Players)
            {
                var view=FestivalSession.ViewFor(game,viewer.Id);
                Assert.That(view.Npcs,Is.Not.Empty,"setup: "+viewer.Id+" sees the crowd");
                Assert.That(view.Npcs.TrueForAll(n=>n.Role==""),Is.True,viewer.Id+" is never told who is a buyer, narc or clue holder");
                Assert.That(view.ClueChain,Is.Empty,viewer.Id+" is never sent the clue chain");
            }
        }

        // TRIP-7: the host deals each level from a secret of its own, so no client can deal the roles, the trails, the friends'
        // spots or the cloud's landmark again from the public seeds. No view carries it, whoever is looking and however they are.
        [Test] public void TheDealSeedNeverLeavesTheHost()
        {
            const int secret=1987654321;var game=Night(()=>secret);var tripper=game.Player(game.State.TripperId);
            Assert.That(game.State.DealSeed,Is.EqualTo(secret),"setup: the host dealt Night 1 from its secret");
            // The tripper has checked a vision and finished the trail, so their view holds all it ever does.
            game.State.Visions.Find(v=>v.IsTrue).Confirmed=true;game.State.GateOpened=true;
            foreach(var viewer in game.State.Players)foreach(var life in new[]{"Alive","Detained","Spirit"})
            {
                viewer.Life=life;var view=FestivalSession.ViewFor(game,viewer.Id);string who=(viewer==tripper?"the tripper":viewer.Id)+", "+life;
                Assert.That(view.DealSeed,Is.Zero,who+": the view has no deal seed");
                Assert.That(UnityEngine.JsonUtility.ToJson(view),Does.Not.Contain(secret.ToString()),who+": nor the secret anywhere else");
                viewer.Life="Alive";
            }
        }
    }
}
