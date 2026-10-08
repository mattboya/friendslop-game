using System;
using System.IO;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class WeekendSessionTests
    {
        [Test] public void ClientsSeeTheWeekendPosition()
        {
            var game=new FestivalSimulation(9);game.AddPlayer("host","Host");var friend=game.AddPlayer("friend","Friend");
            game.State.UnlockedFestivalCount=2;game.State.FestivalIndex=1;game.State.LevelIndex=2;game.State.EncoreTier=3;
            foreach(var life in new[]{"Alive","Spirit"})
            {
                friend.Life=life;var view=FestivalSession.ViewFor(game,friend.Id);
                Assert.That(new[]{view.FestivalIndex,view.LevelIndex,view.EncoreTier,view.UnlockedFestivalCount},Is.EqualTo(new[]{1,2,3,2}),life+" players see which festival, level and encore lap this is");
            }
        }

        [Test] public void ClientsSeeTheDaysSalesAgainstTheWholeCrewsQuota()
        {
            var game=new FestivalSimulation(9);game.AddPlayer("host","Host");var friend=game.AddPlayer("friend","Friend");game.State.LevelSales=25;
            foreach(var life in new[]{"Alive","Spirit"})
            {
                friend.Life=life;var view=FestivalSession.ViewFor(game,friend.Id);
                Assert.That(view.LevelSales,Is.EqualTo(25),life+" players see the day's sales");
                Assert.That(FestivalSimulation.DayQuota(view),Is.EqualTo(30),life+" players see the quota for the whole crew of two");
            }
        }

        [Test] public void DeathCashIsHiddenUntilTheAuthoritativeLanding()
        {
            var game=new FestivalSimulation(9);var owner=game.AddPlayer("owner","Owner");var friend=game.AddPlayer("friend","Friend");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.SpinSeed=4;owner.X=friend.X=0;owner.Z=friend.Z=0;owner.Cash=40;
            owner.Life="Downed";owner.DownedRemaining=.05;game.Tick(.1);
            Assert.That(owner.Cash,Is.Zero,"setup: the death drops cash immediately");
            Assert.That(FestivalSession.ViewFor(game,friend.Id).Drops.Exists(d=>d.ItemId=="cash"),Is.False,"living views cannot reveal the outcome early");
            game.State.SimulationSeconds=owner.DeathSpinAt+2.5-1e-6;
            Assert.That(FestivalSession.ViewFor(game,friend.Id).Drops.Exists(d=>d.ItemId=="cash"),Is.False);
            game.State.SimulationSeconds=owner.DeathSpinAt+2.5;
            Assert.That(FestivalSession.ViewFor(game,friend.Id).Drops.Find(d=>d.ItemId=="cash").Count,Is.EqualTo(40));
            Assert.That(FestivalSession.ViewFor(game,owner.Id).Drops,Is.Empty,"spirits still see no ground drops");
        }

        [TestCase("Alive")][TestCase("Spirit")]
        public void DeathLossAndFrozenCashReachOnlyTheirOwner(string viewerLife)
        {
            var game=new FestivalSimulation(9);var owner=game.AddPlayer("owner","Owner");var friend=game.AddPlayer("friend","Friend");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.SpinSeed=4;owner.Cash=40;
            owner.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});owner.Life="Downed";owner.DownedRemaining=.05;game.Tick(.1);
            friend.Life=viewerLife;var other=FestivalSession.ViewFor(game,friend.Id).Players.Find(p=>p.Id==owner.Id);
            Assert.That(other.GearLossSequence,Is.Zero,"another living or spirit recipient cannot learn the owner's gear-loss event");
            Assert.That(other.GearLossKind,Is.Empty);Assert.That(other.GearLossNotice,Is.Empty);
            Assert.That(other.DeathSpinAt,Is.EqualTo(-1));Assert.That(other.DeathSpinSequence,Is.Zero);
            Assert.That(other.DeathSpinId,Is.Empty);Assert.That(other.DeathSpinRoundId,Is.Empty);Assert.That(other.DeathSpinName,Is.Empty);
            Assert.That(new[]{other.DeathSpinSeed,other.DeathSpinAmount,other.DeathCashDropped},Is.EqualTo(new[]{0,0,0}));
            var own=FestivalSession.ViewFor(game,owner.Id);
            Assert.That(own.Players.Exists(p=>p.Id==friend.Id&&viewerLife=="Alive"),Is.False,"spirits never receive living actors for death animation");
            var copy=own.Players.Find(p=>p.Id==owner.Id);
            Assert.That(copy.GearLossSequence,Is.EqualTo(1));Assert.That(copy.GearLossKind,Is.EqualTo("Death"));
            Assert.That(copy.GearLossNotice,Is.EqualTo("Your gear dropped where you fell: a friend can grab it"));
            Assert.That(copy.DeathSpinAmount,Is.EqualTo(40));
            game.State.Phase=FestivalSimulation.MinigamePhase;
            Assert.That(FestivalSession.ViewFor(game,owner.Id).Players.Find(p=>p.Id==owner.Id).GearLossSequence,Is.EqualTo(1),"a covered screen cannot consume the pending owner event");
            game.Disconnect(owner.Id);owner=game.AddPlayer(owner.Id,"Reconnected");
            Assert.That(FestivalSession.ViewFor(game,owner.Id).Players.Find(p=>p.Id==owner.Id).GearLossSequence,Is.EqualTo(1),"reconnect retains the round-local notice");
        }

        [Test] public void OlderUnitySnapshotsDoNotInventDeathOrLossEvents()
        {
            var game=new FestivalSimulation(9);game.AddPlayer("owner","Owner");
            var player=JsonUtility.FromJson<PlayerState>("{\"Id\":\"owner\",\"Name\":\"Owner\"}");
            var drop=JsonUtility.FromJson<DropState>("{\"Id\":\"old-gear\",\"ItemId\":\"confetti\",\"Count\":1}");
            Assert.That(player.DeathSpinAt,Is.EqualTo(-1),"a missing timestamp keeps the explicit no-death sentinel");
            Assert.That(player.DeathSpinSequence,Is.Zero);Assert.That(player.GearLossSequence,Is.Zero);
            Assert.That(player.DeathSpinId,Is.Empty);Assert.That(player.GearLossKind,Is.Empty);Assert.That(drop.LandsAt,Is.Zero);
            game.State.Players[0]=player;game.State.Drops.Add(drop);game.Restore(game.State);
            Assert.That(FestivalSimulation.VisibleDrops(game.State).Contains(drop),Is.True,"legacy gear still lands immediately");
        }

        [Test] public void HostProfileRemembersUnlockedFestivals()
        {
            string id="weekend_test_"+Guid.NewGuid().ToString("N").Substring(0,12);
            string file=Path.Combine(LocalProfile.Folder,id+".json");
            try
            {
                var profile=new LocalProfile(id);
                Assert.That(profile.Data.UnlockedFestivalCount,Is.EqualTo(1),"a new profile has only the first festival");
                var cleared=new FestivalSimulation(4);cleared.State.UnlockedFestivalCount=2;
                profile.RememberUnlocks(cleared.State);
                var hosted=new FestivalSimulation(5);new LocalProfile(id).ApplyUnlocks(hosted.State);
                Assert.That(hosted.State.UnlockedFestivalCount,Is.EqualTo(2),"the next hosted session starts with the saved unlocks");
                var fresh=new FestivalSimulation(6);profile.RememberUnlocks(fresh.State);
                Assert.That(new LocalProfile(id).Data.UnlockedFestivalCount,Is.EqualTo(2),"a session with fewer unlocks never shrinks the saved count");
                File.WriteAllText(file,File.ReadAllText(file).Replace("\"UnlockedFestivalCount\": 2","\"UnlockedFestivalCount\": 99"));
                Assert.That(new LocalProfile(id).Data.UnlockedFestivalCount,Is.EqualTo(Festivals.Count),"a tampered profile cannot unlock festivals that do not exist");
            }
            finally{if(File.Exists(file))File.Delete(file);}
        }
    }
}
