using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class WeekendSessionTests
    {
        [Test] public void PositiveCashDeathPublishesTheSameMinimalSummaryForEveryViewer()
        {
            var game=new FestivalSimulation(9);var owner=game.AddPlayer("owner","Owner");var friend=game.AddPlayer("friend","Friend");var spirit=game.AddPlayer("spirit","Spirit");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.SpinSeed=4;owner.Cash=40;spirit.Life="Spirit";
            owner.Life="Downed";owner.DownedRemaining=.05;game.Tick(.1);
            foreach(var viewer in new[]{owner.Id,friend.Id,spirit.Id})
            {
                var wire=JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer));
                StringAssert.Contains("\"DeathSpins\":[{",wire,"every recipient receives the actual frozen event even before the pile lands");
                var view=JsonUtility.FromJson<RoundState>(wire);var spin=view.DeathSpins[0];
                Assert.That(view.DeathSpins,Has.Count.EqualTo(1));
                Assert.That(new object[]{spin.Id,spin.RoundId,spin.PlayerId,spin.Name,spin.Sequence,spin.Amount,spin.Seed,spin.CashDropped,spin.AtWorldSeconds},
                    Is.EqualTo(new object[]{game.State.RoundId+":death:owner:1",game.State.RoundId,"owner","Owner",1L,40,276,40,.1}));
            }
        }

        [TestCase("null")][TestCase("wrong-round")][TestCase("duplicate")][TestCase("amount")][TestCase("future")]
        public void UnityRestoreRejectsMalformedPublicDeathHistory(string malformed)
        {
            var game=new FestivalSimulation(9);var owner=game.AddPlayer("owner","Owner");game.AddPlayer("friend","Friend");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.SpinSeed=4;owner.Cash=40;owner.Life="Downed";owner.DownedRemaining=.05;game.Tick(.1);
            var saved=JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State));
            switch(malformed)
            {
                case "null":saved.DeathSpins=null;break;
                case "wrong-round":saved.DeathSpins[0].RoundId="older-round";break;
                case "duplicate":saved.DeathSpins.Add(saved.DeathSpins[0]);break;
                case "amount":saved.DeathSpins[0].Amount=saved.DeathSpins[0].CashDropped=41;break;
                case "future":saved.DeathSpins[0].AtWorldSeconds=1;break;
            }
            var target=new FestivalSimulation();var before=target.State;
            Assert.Throws<ArgumentException>(()=>target.Restore(saved),"new malformed public events must be refused, never silently repaired");
            Assert.That(target.State,Is.SameAs(before),"invalid history cannot replace authoritative state");
        }

        [Test] public void PublicDeathViewsAreDetachedAndCarryOnlyTheFrozenSummary()
        {
            var game=PublicDeathGame();var owner=game.Player("owner");var friend=game.Player("friend");friend.Life="Spirit";
            var view=FestivalSession.ViewFor(game,friend.Id);
            Assert.That(view.DeathSpins,Is.Not.SameAs(game.State.DeathSpins));
            Assert.That(view.Players.Exists(p=>p.Life!="Spirit"),Is.False,"public events never broaden spirit actor visibility");
            Assert.That(view.DeathSpins[0],Is.Not.SameAs(game.State.DeathSpins[0]),"each recipient gets a detached immutable summary");
            Assert.That(typeof(DeathSpinState).GetFields().Select(f=>f.Name),Is.EquivalentTo(new[]{"Id","RoundId","PlayerId","Name","Sequence","Amount","Seed","CashDropped","AtWorldSeconds"}),"no inventory, current cash, gear notice or position can enter this minimal public type");
            view.DeathSpins[0].Name="Viewer changed";view.DeathSpins[0].Amount=999;view.DeathSpins.Clear();
            Assert.That(game.State.DeathSpins,Has.Count.EqualTo(1));Assert.That(game.State.DeathSpins[0].Name,Is.EqualTo("Owner"));Assert.That(game.State.DeathSpins[0].Amount,Is.EqualTo(40));
            owner.Life="Alive";owner.Cash=17;owner.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
            view=FestivalSession.ViewFor(game,friend.Id);
            Assert.That(view.Players.Exists(p=>p.Id==owner.Id),Is.False,"revived living owner remains hidden from a spirit");
            Assert.That(view.DeathSpins[0].Name,Is.EqualTo("Owner"));Assert.That(view.DeathSpins[0].Amount,Is.EqualTo(40));
        }

        [Test] public void PublicHistorySurvivesActualRevivalLaterDeathAndDisconnectBetweenViews()
        {
            var game=PublicDeathGame();var owner=game.Player("owner");var friend=game.Player("friend");string first=JsonUtility.ToJson(game.State.DeathSpins[0]);
            owner.X=friend.X=0;owner.Z=friend.Z=0;
            var band=game.State.Drops.Find(d=>d.ItemId=="wristband"&&d.OwnerId==owner.Id);
            Assert.That(game.Execute(friend.Id,new GameCommand{Id="public-band",Kind="Pickup",TargetId=band.Id}).Accepted,Is.True);
            owner.X=friend.X=24;owner.Z=friend.Z=-20;
            Assert.That(game.Execute(friend.Id,new GameCommand{Id="public-revive",Kind="BeginRevival",TargetId=owner.Id}).Accepted,Is.True);
            game.Tick(10.1);Assert.That(owner.Life,Is.EqualTo("Alive"));
            owner.Name="Renamed";owner.Cash=55;owner.Life="Downed";owner.DownedRemaining=.05;game.Tick(.1);
            game.Disconnect(owner.Id);game.AddPlayer(owner.Id,"Reconnected");
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State)));
            foreach(var life in new[]{"Alive","Spirit"})
            {
                restored.Player(friend.Id).Life=life;var view=FestivalSession.ViewFor(restored,friend.Id);
                Assert.That(view.DeathSpins,Has.Count.EqualTo(2),"no polls between actual deaths may lose a public event");
                Assert.That(JsonUtility.ToJson(view.DeathSpins[0]),Is.EqualTo(first),"the first frozen event never reads current name, cash or life");
                Assert.That(view.DeathSpins[1].Sequence,Is.EqualTo(2));Assert.That(view.DeathSpins[1].Name,Is.EqualTo("Renamed"));Assert.That(view.DeathSpins[1].Amount,Is.EqualTo(55));
                var other=view.Players.Find(p=>p.Id==owner.Id);Assert.That(other.GearLossSequence,Is.Zero);Assert.That(other.GearLossNotice,Is.Empty);Assert.That(other.Cash,Is.Zero);Assert.That(other.Inventory,Is.Empty);
            }
            string round=restored.State.RoundId;restored.State.Phase="Results";restored.State.Result="Failure";
            Assert.That(restored.Execute(owner.Id,new GameCommand{Id="public-reset",Kind="Reset"}).Accepted,Is.True);
            Assert.That(restored.State.RoundId,Is.Not.EqualTo(round));Assert.That(FestivalSession.ViewFor(restored,friend.Id).DeathSpins,Is.Empty);
        }

        [Test] public void MissingUnityPublicHistoryStaysQuietWithoutInventingTheLatestOwnerEvent()
        {
            var game=PublicDeathGame();string json=JsonUtility.ToJson(game.State);
            int start=json.IndexOf("\"DeathSpins\":",StringComparison.Ordinal),end=json.IndexOf("],",start,StringComparison.Ordinal)+2;
            string old=json.Remove(start,end-start);Assert.That(old,Does.Not.Contain("\"DeathSpins\""),"setup: actual JSON omits only the additive history");
            var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(old));
            Assert.That(restored.Player("owner").DeathSpinSequence,Is.EqualTo(1),"the old latest owner record is present");
            Assert.That(restored.State.DeathSpins,Is.Empty);Assert.That(FestivalSession.ViewFor(restored,"friend").DeathSpins,Is.Empty,"no partial public history can be synthesized from owner state");
            Assert.That(JsonUtility.FromJson<RoundState>("{\"RoundId\":\"old\"}").DeathSpins,Is.Empty,"unmutated legacy Unity JSON preserves the quiet initializer");
            Assert.That(JsonUtility.FromJson<DeathSpinState>("{}").AtWorldSeconds,Is.EqualTo(-1),"a quiet default DTO is never mistaken for an emitted event");
        }

        [Test] public void RealBoothViewsKeepTheSameDeathWorldTimeAndLandingAge()
        {
            var game=new FestivalSimulation(9);var owner=game.AddPlayer("owner","Owner");var friend=game.AddPlayer("friend","Friend");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.SpinSeed=4;owner.Cash=40;
            game.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;game.State.Booth.Spot=0;game.State.Booth.RingSeconds=2.95;
            var at=Festivals.BoothSpots[0];owner.X=friend.X=at.X;owner.Z=friend.Z=at.Z;owner.Life="Downed";owner.DownedRemaining=.05;game.Tick(.1);
            Assert.That(game.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));string frozen=JsonUtility.ToJson(game.State.DeathSpins[0]);
            game.Tick(3);
            Assert.That(FestivalSimulation.WorldSeconds(FestivalSession.ViewFor(game,friend.Id))-.1,Is.EqualTo(0).Within(1e-6));
            Assert.That(game.Execute(friend.Id,new GameCommand{Id="end-view-cups",Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.CallCups}).Accepted,Is.True);
            game.Tick(4.1);Assert.That(game.State.Phase,Is.EqualTo("Playing"));
            var cash=game.State.Drops.Find(d=>d.ItemId=="cash");game.State.SimulationSeconds=cash.LandsAt;
            foreach(var viewer in new[]{owner.Id,friend.Id})
            {
                var view=FestivalSession.ViewFor(game,viewer);Assert.That(JsonUtility.ToJson(view.DeathSpins[0]),Is.EqualTo(frozen));
                Assert.That(FestivalSimulation.WorldSeconds(view)-view.DeathSpins[0].AtWorldSeconds,Is.EqualTo(2.5).Within(1e-6));
                Assert.That(view.Drops.Exists(d=>d.ItemId=="cash"),Is.EqualTo(viewer==friend.Id));
            }
        }

        static FestivalSimulation PublicDeathGame()
        {
            var game=new FestivalSimulation(9);var owner=game.AddPlayer("owner","Owner");game.AddPlayer("friend","Friend");
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.SpinSeed=4;owner.Cash=40;owner.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
            foreach(var player in game.State.Players){player.X=0;player.Z=0;}
            owner.Life="Downed";owner.DownedRemaining=.05;game.Tick(.1);return game;
        }

        [TestCase("Alive")][TestCase("Spirit")]
        public void PublicSummaryDoesNotUnmaskCurrentPlayersOrOwnerGear(string viewerLife)
        {
            var game=PublicDeathGame();var owner=game.Player("owner");var friend=game.Player("friend");friend.Life=viewerLife;
            owner.Cash=17;owner.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
            var view=FestivalSession.ViewFor(game,friend.Id);var other=view.Players.Find(p=>p.Id==owner.Id);
            Assert.That(other.GearLossSequence,Is.Zero);Assert.That(other.GearLossNotice,Is.Empty);Assert.That(other.Inventory,Is.Empty);Assert.That(other.Cash,Is.Zero);
            Assert.That(view.DeathSpins[0].Amount,Is.EqualTo(40),"only the immutable death amount is public");
            owner.Life="Alive";view=FestivalSession.ViewFor(game,friend.Id);
            Assert.That(view.Players.Exists(p=>p.Id==owner.Id),Is.EqualTo(viewerLife=="Alive"),"public death summaries never add a living actor to spirit views");
        }

        [Test] public void ValidTimeZeroPublicDeathRestoresInUnityWithoutRerolling()
        {
            var game=PublicDeathGame();var saved=JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State));
            saved.DeathSpins[0].AtWorldSeconds=saved.Players[0].DeathSpinAt=saved.SimulationSeconds=0;saved.Drops.Find(d=>d.ItemId=="cash").LandsAt=2.5;
            string frozen=JsonUtility.ToJson(saved.DeathSpins[0]);var restored=new FestivalSimulation();
            restored.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(saved)));
            foreach(var viewer in new[]{"owner","friend"})Assert.That(JsonUtility.ToJson(FestivalSession.ViewFor(restored,viewer).DeathSpins[0]),Is.EqualTo(frozen),"a valid zero timestamp is an actual event, and its seed/outcome/amount survive unchanged");
        }

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

        // The host bootstrap the Editor runs offers locked Ember Playa on a fresh profile; a level cleared there saves nothing.
        [Test] public void TestBuildHostPicksEmberPlayaAndTheSavedProfileKeepsOneUnlock()
        {
            string id="dev_selection_"+Guid.NewGuid().ToString("N").Substring(0,12);
            string file=Path.Combine(LocalProfile.Folder,id+".json");
            try
            {
                var profile=new LocalProfile(id);
                var game=new FestivalSimulation(7);profile.ApplyUnlocks(game.State);FestivalHostBootstrap.Configure(game);
                var host=game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");
                var view=FestivalSession.ViewFor(game,host.Id);
                Assert.That(FestivalHudText.FestivalChoice(view,host.Id,game.DevelopmentFestivalSelection),Is.EqualTo("F  switch to Ember Playa (test build: all festivals)"),"a fresh profile is offered the next festival");
                Assert.That(game.Execute(host.Id,new GameCommand{Id="dev-pick",Kind="ChooseFestival",Amount=FestivalHudText.NextFestival(view,game.DevelopmentFestivalSelection)}).Accepted,Is.True);
                Assert.That(game.State.FestivalIndex,Is.EqualTo(1));
                foreach(var p in game.State.Players){p.X=0;p.Z=19;Assert.That(game.Execute(p.Id,new GameCommand{Id="dev-ready-"+p.Id,Kind="Ready"}).Accepted,Is.True);}
                game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
                foreach(var p in game.State.Players)Assert.That(game.Execute(p.Id,new GameCommand{Id="dev-loaded-"+p.Id,Kind="MapReady"}).Accepted,Is.True);
                Assert.That(game.State.Phase,Is.EqualTo("Playing"),"setup: Ember Playa's Day 1 is on");
                game.State.LevelSales=FestivalSimulation.DayQuota(game.State);host.X=Festivals.CampGateX;host.Z=Festivals.CampGateZ;
                Assert.That(game.Execute(host.Id,new GameCommand{Id="dev-extract",Kind="Extract"}).Accepted,Is.True);game.Tick(3.1);
                Assert.That(game.State.Phase=="Results"&&game.State.Result=="Success",Is.True,"setup: the day is cleared");
                profile.RememberUnlocks(game.State);
                Assert.That(new LocalProfile(id).Data.UnlockedFestivalCount,Is.EqualTo(1),"the saved profile still says 1 unlocked");
                var rehosted=new FestivalSimulation(8);new LocalProfile(id).ApplyUnlocks(rehosted.State);FestivalHostBootstrap.Configure(rehosted);
                Assert.That(rehosted.DevelopmentFestivalSelection&&rehosted.State.UnlockedFestivalCount==1,Is.True,"the next host starts from the same earned count with the pick");
            }
            finally{if(File.Exists(file))File.Delete(file);}
        }

        // The Editor's own host bootstrap, so the capability comes from where a test build's host gets it. A spirit's view is
        // filtered apart from a living player's, so both must carry the flag.
        [Test] public void TestModeReachesEveryViewAndClearingNightTwoEarnsNothing()
        {
            string id="dev_test_mode_"+Guid.NewGuid().ToString("N").Substring(0,12);
            string file=Path.Combine(LocalProfile.Folder,id+".json");
            try
            {
                var profile=new LocalProfile(id);
                var game=new FestivalSimulation(7);profile.ApplyUnlocks(game.State);FestivalHostBootstrap.Configure(game);
                var host=game.AddPlayer("host","Host");var friend=game.AddPlayer("friend","Friend");
                Assert.That(game.DevelopmentFestivalSelection,Is.True,"setup: the Editor's bootstrap is a test build's");
                Assert.That(FestivalSession.ViewFor(game,friend.Id).TestMode,Is.False,"test mode starts off");
                Assert.That(game.Execute(host.Id,new GameCommand{Id="dev-test-mode",Kind=FestivalSimulation.TestModeKind}).Accepted,Is.True,"the host turns it on");
                foreach(var life in new[]{"Alive","Spirit"}){friend.Life=life;Assert.That(FestivalSession.ViewFor(game,friend.Id).TestMode,Is.True,"a "+life+" friend's view carries it");}
                friend.Life="Alive";game.State.LevelIndex=Festivals.LevelCount-1;game.State.Phase="Playing";
                Assert.That(game.Execute(host.Id,new GameCommand{Id="dev-clear",Kind=FestivalSimulation.TestClearLevelKind}).Accepted,Is.True,"the host clears Night 2");
                Assert.That(game.State.Result,Is.EqualTo("Success"));
                profile.RememberUnlocks(game.State);
                Assert.That(game.State.UnlockedFestivalCount,Is.EqualTo(1),"clearing Night 2 in test mode unlocks nothing");
                Assert.That(new LocalProfile(id).Data.UnlockedFestivalCount,Is.EqualTo(1),"the saved profile still says 1 unlocked");
                Assert.That(game.State.Players.Exists(p=>p.HasCosmetic),Is.False,"and nobody earns the first-rescue cosmetic");
                Assert.That(game.Execute(host.Id,new GameCommand{Id="dev-next",Kind="Reset"}).Accepted,Is.True);
                Assert.That(game.State.FestivalIndex==1&&game.State.LevelIndex==0,Is.True,"the crew moves on to the next festival's Day 1, at "+game.State.FestivalIndex+" level "+game.State.LevelIndex);
                Assert.That(FestivalSession.ViewFor(game,friend.Id).TestMode,Is.True,"still in test mode");
            }
            finally{if(File.Exists(file))File.Delete(file);}
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
