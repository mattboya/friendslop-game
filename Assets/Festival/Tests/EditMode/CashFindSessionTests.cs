using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class CashFindSessionTests
    {
        static int sequence;
        static FestivalSimulation Playing(int festival=0)
        {
            var game=new FestivalSimulation(12);game.DealSecret=()=>98765;game.State.UnlockedFestivalCount=Festivals.Count;game.State.FestivalIndex=festival;
            foreach(var id in new[]{"owner","friend","spirit"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;Assert.That(game.Execute(id,new GameCommand{Id="ready"+(sequence++),Kind="Ready"}).Accepted,Is.True);}
            game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
            foreach(var p in game.State.Players)Assert.That(game.Execute(p.Id,new GameCommand{Id="loaded"+(sequence++),Kind="MapReady"}).Accepted,Is.True);
            game.State.Npcs.Clear();game.Player("spirit").Life="Spirit";game.Tick(60);game.Tick(40);return game;
        }
        static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        [Test]public void ActualRecipientDtosMaskFutureScheduleAndKeepPublicFindsAndPrivateBalances()
        {
            var game=Playing();Assert.That(game.State.CashFindIndex,Is.GreaterThan(0));Assert.That(game.State.NextCashFindAt,Is.GreaterThan(game.State.ElapsedSeconds));
            foreach(var id in new[]{"owner","friend","spirit"})
            {
                var view=Wire(game,id);Assert.That(view.DealSeed,Is.Zero);Assert.That(view.CashFindIndex,Is.Zero);Assert.That(view.NextCashFindAt,Is.Zero);
                Assert.That(view.Players.Find(p=>p.Id==id).Cash,Is.EqualTo(game.Player(id).Cash));
                foreach(var p in view.Players.Where(p=>p.Id!=id))Assert.That(p.Cash,Is.Zero);
                Assert.That(view.Drops.Count(d=>d.CashFind),Is.EqualTo(id=="spirit"?0:game.State.Drops.Count(d=>d.CashFind)),"living recipients see actual scheduled finds; spirits keep no drop view");
            }
        }
        // Money news is owner-only, like the balance it reports: your view carries yours, a friend's view none of it.
        [Test]public void MoneyNewsReachesOnlyItsOwnersView()
        {
            var game=Playing();var owner=game.Player("owner");owner.X=-28;owner.Z=16;
            Assert.That(game.Execute("owner",new GameCommand{Id="news"+(sequence++),Kind="LostProperty"}).Accepted,Is.True);game.Tick(5.1);
            Assert.That(owner.MoneyNewsLog.Last().Text,Is.EqualTo("LOST PROPERTY RETURNED  •  +$5"),"setup: the host holds the owner's news");
            var mine=Wire(game,"owner").Players.Find(p=>p.Id=="owner");
            Assert.That(mine.MoneyNewsLog.Last().Text,Is.EqualTo("LOST PROPERTY RETURNED  •  +$5"));Assert.That(mine.MoneyNewsSequence,Is.EqualTo(owner.MoneyNewsSequence));
            var theirs=Wire(game,"friend").Players.Find(p=>p.Id=="owner");
            Assert.That(theirs.MoneyNewsLog,Is.Empty,"a friend never reads your pocket's news");Assert.That(theirs.MoneyNewsSequence,Is.Zero);
        }
        [Test]public void CashDropDtosAreDetachedFromTheAuthoritativeFinancialClaim()
        {
            var game=Playing();var view=FestivalSession.ViewFor(game,"friend");var source=game.State.Drops.Find(d=>d.CashFind);var copy=view.Drops.Find(d=>d.Id==source.Id);
            Assert.That(copy,Is.Not.SameAs(source),"recipient cash metadata cannot mutate the host's one financial claim");
            copy.Count=1000;copy.X=0;copy.CashFind=false;Assert.That(source.Count,Is.InRange(2,5));Assert.That(source.CashFind,Is.True);
        }
        [Test]public void UnityHostSnapshotContinuesThePersistedAttemptAndLegacyFieldsArmLazily()
        {
            var game=Playing();var saved=JsonUtility.ToJson(game.State);var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(saved));
            Assert.That(restored.State.CashFindIndex,Is.EqualTo(game.State.CashFindIndex));Assert.That(restored.State.NextCashFindAt,Is.EqualTo(game.State.NextCashFindAt));
            game.Tick(60);restored.Tick(60);Assert.That(JsonUtility.ToJson(new RoundState{Drops=restored.State.Drops}),Is.EqualTo(JsonUtility.ToJson(new RoundState{Drops=game.State.Drops})));
            string legacy=Regex.Replace(saved,"\"(?:CashFindIndex|NextCashFindAt)\":[^,}]+,?","");
            var old=JsonUtility.FromJson<RoundState>(legacy);Assert.That(old.CashFindIndex,Is.Zero);Assert.That(old.NextCashFindAt,Is.Zero);old.Drops.Clear();var lazy=new FestivalSimulation();lazy.Restore(old);lazy.Tick(.1);
            Assert.That(lazy.State.NextCashFindAt-lazy.State.ElapsedSeconds,Is.InRange(45,90));
        }
        [Test]public void UnityMalformedTaggedCashRejectsBeforeStateReplacement()
        {
            var game=Playing();foreach(int amount in new[]{0,1,6,int.MaxValue})
            {
                var snapshot=JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State));snapshot.Drops.Find(d=>d.CashFind).Count=amount;var target=new FestivalSimulation();var before=target.State;
                Assert.Throws<ArgumentException>(()=>target.Restore(snapshot));Assert.That(target.State,Is.SameAs(before));
            }
        }
        [TestCase(0)][TestCase(1)]public void EveryAuthoredFindHasCurrentSceneryStandingRoomAndACompleteNavMeshPath(int festival)
        {
            var root=new GameObject("impl-U19 cash navigation");
            try
            {
                var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");world.SetTwists(new RoundState{Phase="Playing",FestivalIndex=festival});Physics.SyncTransforms();Assert.That(world.NavigationReady,Is.True);
                foreach(var spot in Festivals.CashFindSpots[festival])
                {
                    var point=new Vector3(spot.X,0,spot.Z);var standing=point+Vector3.up;
                    foreach(var collider in root.GetComponentsInChildren<Collider>())if(collider.enabled&&!collider.isTrigger)Assert.That(Vector3.Distance(collider.ClosestPoint(standing),standing),Is.GreaterThan(.4f),"cash standing room "+point+" blocked by "+collider.name);
                    Assert.That(NavMesh.SamplePosition(point,out var ground,.3f,NavMesh.AllAreas),Is.True,"cash point off ground "+point);Assert.That(Mathf.Abs(ground.position.y),Is.LessThan(.3f));Assert.That(Vector3.Distance(ground.position,point),Is.LessThan(.3f));
                    var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(new Vector3(-7,0,-29),point,NavMesh.AllAreas,path),Is.True,"missing cash path "+point);Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"incomplete cash path "+point);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [UnityTearDown]public IEnumerator LeaveOwnedPlayMode(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest]public IEnumerator ActualHostedPickupAndAStaleOtherRecipientCannotBothCreditCash()
        {
            yield return new EnterPlayMode();string previous=LocalProfile.Folder,scope=FestivalInput.PreferencesScope;string folder=Path.Combine(Path.GetTempPath(),"impl-U19-cash-host-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);LocalProfile.Folder=folder;FestivalInput.PreferencesScope=folder;
            var root=new GameObject("impl-U19 cash host");
            try
            {
                var session=root.AddComponent<FestivalSession>();session.enabled=false;session.Host("Cash host",0);Assert.That(session.IsHost&&session.Connected,Is.True,session.Message);
                var game=Playing();var drop=game.State.Drops.Find(d=>d.CashFind);var owner=game.Player("owner");var friend=game.Player("friend");owner.X=friend.X=drop.X;owner.Z=friend.Z=drop.Z;
                owner.Inventory.AddRange(new[]{new ItemStack{ItemId="poi",Count=1},new ItemStack{ItemId="confetti",Count=1},new ItemStack{ItemId="stock_lsd",Count=1}});int cash=owner.Cash,other=friend.Cash;
                var stale=Wire(game,friend.Id).Drops.Find(d=>d.Id==drop.Id);Assert.That(stale,Is.Not.Null);
                typeof(FestivalSession).GetField("simulation",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,game);typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,owner.Id);typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(game,owner.Id));
                string issued=session.SendCommand("Pickup",drop.Id);Assert.That(issued,Is.Not.Empty);Assert.That(session.LastLocalCommandResult.Accepted,Is.True,session.Message);
                Assert.That(game.Execute(friend.Id,new GameCommand{Id="stale-wire-claim",Kind="Pickup",TargetId=stale.Id}).Accepted,Is.False);
                Assert.That(game.Execute(owner.Id,new GameCommand{Id=issued,Kind="Pickup",TargetId=drop.Id}).Accepted,Is.True,"duplicate authentic command returns its receipt");
                Assert.That(owner.Cash,Is.EqualTo(cash+drop.Count));Assert.That(friend.Cash,Is.EqualTo(other));Assert.That(game.State.LevelSales,Is.Zero);Assert.That(game.State.StashCash,Is.Zero);Assert.That(owner.Inventory,Has.Count.EqualTo(3));Assert.That(Wire(game,friend.Id).Drops.Exists(d=>d.Id==drop.Id),Is.False);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);LocalProfile.Folder=previous;FestivalInput.PreferencesScope=scope;if(Directory.Exists(folder))Directory.Delete(folder,true);}
            yield return new ExitPlayMode();
        }
    }
}
