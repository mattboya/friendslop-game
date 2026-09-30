using System;
using Festival.Core;
using NUnit.Framework;

namespace Festival.Tests
{
    public sealed class CoreContractTests
    {
        [Test] public void CooperativeMissionAndActiveRecovery(){global::MissionTests.Run();}

        [Test] public void LittleSpoonIsWornAndCannotOccupyAHandSlot()
        {
            var game=new FestivalSimulation(21);var player=game.AddPlayer("host","Host");
            var point=Catalog.ShopPoint(true,game.State.VendorOffers.IndexOf("little_spoon"));
            player.X=point.X;player.Z=point.Z;
            Assert.That(game.Execute(player.Id,new GameCommand{Id="hold",Kind="HoldOffer",ItemId="little_spoon"}).Accepted,Is.True);
            player.X=0;player.Z=7;
            Assert.That(game.Execute(player.Id,new GameCommand{Id="buy",Kind="Buy",ItemId="little_spoon"}).Accepted,Is.True);
            Assert.That(player.EquippedItemId,Is.Empty);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="unowned",Kind="Equip",ItemId="confetti"}).Accepted,Is.False);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="unequip",Kind="Equip"}).Accepted,Is.True);
            Assert.That(player.EquippedItemId,Is.Empty);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="reequip",Kind="Equip",ItemId="little_spoon"}).Accepted,Is.False);
        }

        [Test] public void CampHandoffsRespectSharedInteriorAndConserveGear()
        {
            var game=new FestivalSimulation(21);
            var giver=game.AddPlayer("giver","Giver");var receiver=game.AddPlayer("receiver","Receiver");
            var tent=CampFeatures.Find("tent_1");
            giver.X=receiver.X=tent.X;giver.Z=receiver.Z=tent.Z;
            Assert.That(game.Execute(giver.Id,new GameCommand{Id="enter-giver",Kind="EnterCamp",TargetId=tent.Id}).Accepted,Is.True);
            Assert.That(game.Execute(receiver.Id,new GameCommand{Id="enter-receiver",Kind="EnterCamp",TargetId=tent.Id}).Accepted,Is.True);
            Assert.That(Math.Abs(giver.CampInteriorX-receiver.CampInteriorX),Is.GreaterThan(1));
            giver.Inventory.Add(new ItemStack{ItemId="map",Count=1});
            int cash=giver.Cash+receiver.Cash;
            Assert.That(game.Execute(giver.Id,new GameCommand{Id="offer-map",Kind="Transfer",TargetId=receiver.Id,ItemId="map",Amount=1}).Accepted,Is.True);
            var offer=game.State.Transfers[0];
            receiver.CampInteriorX+=2.6f;
            Assert.That(game.Execute(receiver.Id,new GameCommand{Id="too-far",Kind="AcceptTransfer",TargetId=offer.Id}).Accepted,Is.False);
            receiver.CampInteriorX=giver.CampInteriorX;
            Assert.That(game.Execute(receiver.Id,new GameCommand{Id="take-map",Kind="AcceptTransfer",TargetId=offer.Id}).Accepted,Is.True);
            Assert.That(game.Execute(receiver.Id,new GameCommand{Id="take-map",Kind="AcceptTransfer",TargetId=offer.Id}).Accepted,Is.True);
            Assert.That(receiver.Inventory.Find(i=>i.ItemId=="map").Count,Is.EqualTo(1));
            Assert.That(game.State.Transfers,Is.Empty);
            Assert.That(giver.Cash+receiver.Cash,Is.EqualTo(cash));

            giver.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
            Assert.That(game.Execute(giver.Id,new GameCommand{Id="offer-confetti",Kind="Transfer",TargetId=receiver.Id,ItemId="confetti",Amount=1}).Accepted,Is.True);
            offer=game.State.Transfers[0];
            Assert.That(game.Execute(giver.Id,new GameCommand{Id="cancel-confetti",Kind="CancelTransfer",TargetId=offer.Id}).Accepted,Is.True);
            Assert.That(giver.Inventory.Find(i=>i.ItemId=="confetti").Count,Is.EqualTo(1));
            Assert.That(game.State.Transfers,Is.Empty);

            Assert.That(game.Execute(giver.Id,new GameCommand{Id="offer-expiring",Kind="Transfer",TargetId=receiver.Id,ItemId="confetti",Amount=1}).Accepted,Is.True);
            game.Tick(15.1);
            Assert.That(game.State.Transfers,Is.Empty);
            Assert.That(giver.Inventory.Find(i=>i.ItemId=="confetti").Count,Is.EqualTo(1));

            Assert.That(game.Execute(giver.Id,new GameCommand{Id="offer-disconnect",Kind="Transfer",TargetId=receiver.Id,ItemId="confetti",Amount=1}).Accepted,Is.True);
            game.Disconnect(receiver.Id);
            Assert.That(game.State.Transfers,Is.Empty);
            Assert.That(giver.Inventory.Find(i=>i.ItemId=="confetti").Count,Is.EqualTo(1));
        }

        [Test] public void CampHandoffRejectsOtherRoomsAndFullInventory()
        {
            var game=new FestivalSimulation(21);
            var giver=game.AddPlayer("giver","Giver");var receiver=game.AddPlayer("receiver","Receiver");
            var tent=CampFeatures.Find("tent_1");var other=CampFeatures.Find("tent_7");
            giver.X=tent.X;giver.Z=tent.Z;receiver.X=other.X;receiver.Z=other.Z;
            giver.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});
            Assert.That(game.Execute(giver.Id,new GameCommand{Id="enter-one",Kind="EnterCamp",TargetId=tent.Id}).Accepted,Is.True);
            Assert.That(game.Execute(receiver.Id,new GameCommand{Id="enter-other",Kind="EnterCamp",TargetId=other.Id}).Accepted,Is.True);
            Assert.That(game.Execute(giver.Id,new GameCommand{Id="wrong-room",Kind="Transfer",TargetId=receiver.Id,ItemId="stage_pass",Amount=1}).Accepted,Is.False);
            Assert.That(game.Execute(receiver.Id,new GameCommand{Id="exit-other",Kind="ExitCamp"}).Accepted,Is.True);
            receiver.X=tent.X;receiver.Z=tent.Z;
            Assert.That(game.Execute(receiver.Id,new GameCommand{Id="enter-same",Kind="EnterCamp",TargetId=tent.Id}).Accepted,Is.True);
            foreach(var id in new[]{"map","poi_practice","merch_bag"})receiver.Inventory.Add(new ItemStack{ItemId=id,Count=1});
            Assert.That(game.Execute(giver.Id,new GameCommand{Id="offer-full",Kind="Transfer",TargetId=receiver.Id,ItemId="stage_pass",Amount=1}).Accepted,Is.True);
            var offer=game.State.Transfers[0];
            Assert.That(game.Execute(receiver.Id,new GameCommand{Id="reject-full",Kind="AcceptTransfer",TargetId=offer.Id}).Accepted,Is.False);
            Assert.That(game.State.Transfers.Count,Is.EqualTo(1));
            Assert.That(game.Execute(giver.Id,new GameCommand{Id="cancel-full",Kind="CancelTransfer",TargetId=offer.Id}).Accepted,Is.True);
            Assert.That(giver.Inventory.Find(i=>i.ItemId=="stage_pass").Count,Is.EqualTo(1));
        }

        [Test] public void DjTakeoverPlacesThePlayerAtTheStageFrontDeck()
        {
            var game=new FestivalSimulation(23);var player=game.AddPlayer("host","Host");
            game.State.Phase="Playing";
            player.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});
            player.X=1.4f;player.Z=26;
            Assert.That(game.Execute(player.Id,new GameCommand{Id="dj-too-far",Kind="Dj"}).Accepted,Is.False);
            player.X=1.0f;player.Yaw=113;
            Assert.That(game.Execute(player.Id,new GameCommand{Id="dj-start",Kind="Dj"}).Accepted,Is.True);
            Assert.That(player.X,Is.EqualTo(0));Assert.That(player.Z,Is.EqualTo(26));
            Assert.That(player.Yaw,Is.EqualTo(0));
            Assert.That(game.Interaction(player.InteractionId).Kind,Is.EqualTo("Dj"));
            Assert.That(player.Inventory.Exists(item=>item.ItemId=="stage_pass"),Is.False);
        }

        [Test] public void SeparateCampDoorsKeepPlayersInSeparateInteriors()
        {
            var game=new FestivalSimulation(21);
            var first=game.AddPlayer("first","First");var second=game.AddPlayer("second","Second");
            var left=CampFeatures.Find("tent_1");var right=CampFeatures.Find("tent_7");
            first.X=left.X;first.Z=left.Z;second.X=right.X;second.Z=right.Z;
            Assert.That(game.Execute(first.Id,new GameCommand{Id="enter-first",Kind="EnterCamp",TargetId=left.Id}).Accepted,Is.True);
            Assert.That(game.Execute(second.Id,new GameCommand{Id="enter-second",Kind="EnterCamp",TargetId=right.Id}).Accepted,Is.True);
            Assert.That(first.CampInteriorX!=second.CampInteriorX||first.CampInteriorZ!=second.CampInteriorZ,Is.True);
            Assert.That(game.TryMove(first.Id,first.CampInteriorX,first.CampInteriorZ+.1f,0,.1),Is.True);
            Assert.That(game.TryMove(first.Id,second.CampInteriorX,second.CampInteriorZ,0,.1),Is.False);
        }

        [Test] public void WalkingBackThroughAnyCampDoorReturnsPlayerOutside()
        {
            var game=new FestivalSimulation(21);var player=game.AddPlayer("host","Host");
            foreach(var site in CampFeatures.Sites)
            {
                player.X=site.X;player.Z=site.Z;
                Assert.That(game.Execute(player.Id,new GameCommand{Id="enter-"+site.Id,Kind="EnterCamp",TargetId=site.Id}).Accepted,Is.True,site.Id);
                float roomZ=CampFeatures.InteriorSlotZ(site);
                for(int step=1;step<=10&&player.CampVisitId!="";step++)
                    Assert.That(game.TryMove(player.Id,CampFeatures.InteriorSlotX(site),roomZ-1.5f-step*.1f,180,.1),Is.True,site.Id+" step "+step);
                Assert.That(player.CampVisitId,Is.Empty,site.Id+" should have a walk-out exit");
                Assert.That(player.X,Is.EqualTo(site.X),site.Id);
                Assert.That(player.Z,Is.EqualTo(site.Z-3.2f),site.Id);
            }
        }

        [Test] public void EveryCampDoorAndExitFitInsideThePlayableFootprint()
        {
            foreach(var site in CampFeatures.Sites)
            {
                Assert.That(Math.Abs(site.X)+3.5f,Is.LessThan(CampFeatures.CampHalfWidth),site.Id+" width");
                Assert.That(Math.Abs(site.Z)+3.5f,Is.LessThan(CampFeatures.CampHalfDepth),site.Id+" depth");
                Assert.That(site.Z-3.2f,Is.GreaterThan(-CampFeatures.CampHalfDepth),site.Id+" exit");
            }
        }

        [Test] public void WornNecklaceDoesNotConsumeOneOfThreeHandGearSlots()
        {
            var game=new FestivalSimulation(21);var player=game.AddPlayer("host","Host");
            player.Cash=100;
            foreach(var id in new[]{"map","poi_practice","merch_bag"})
                player.Inventory.Add(new ItemStack{ItemId=id,Count=1});
            var point=Catalog.ShopPoint(true,game.State.VendorOffers.IndexOf("little_spoon"));
            player.X=point.X;player.Z=point.Z;
            Assert.That(game.Execute(player.Id,new GameCommand{Id="hold-necklace",Kind="HoldOffer",ItemId="little_spoon"}).Accepted,Is.True);
            player.X=0;player.Z=7;
            Assert.That(game.Execute(player.Id,new GameCommand{Id="buy-necklace",Kind="Buy",ItemId="little_spoon"}).Accepted,Is.True);
            Assert.That(player.Inventory.Count,Is.EqualTo(4));
            Assert.That(player.EquippedItemId,Is.Empty);
        }

        [Test] public void OnlyDesignatedFestivalgoersChatAndLinesVary()
        {
            var game=new FestivalSimulation(21);var player=game.AddPlayer("host","Host");game.State.Phase="Playing";
            player.X=0;player.Z=0;game.State.Npcs.Clear();
            var quiet=new NpcState{Id="quiet",X=0,Z=1,CanTalk=false};
            var friendly=new NpcState{Id="friendly",X=0,Z=1,CanTalk=true};
            game.State.Npcs.Add(quiet);game.State.Npcs.Add(friendly);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="quiet-talk",Kind="Talk",TargetId=quiet.Id}).Accepted,Is.False);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="friendly-talk",Kind="Talk",TargetId=friendly.Id}).Accepted,Is.True);
            string first=player.NpcSpeech;
            Assert.That(first,Is.Not.Empty);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="spam-talk",Kind="Talk",TargetId=friendly.Id}).Accepted,Is.False);
            game.Tick(2.1);
            Assert.That(game.Execute(player.Id,new GameCommand{Id="second-talk",Kind="Talk",TargetId=friendly.Id}).Accepted,Is.True);
            Assert.That(player.NpcSpeech,Is.Not.EqualTo(first));
            Assert.That(player.NpcSpeechUntil,Is.GreaterThan(game.State.SimulationSeconds));
        }

        [Test] public void RhythmBoundariesRemainIndependentFromPresentation()
        {
            var chart=RhythmChart.Create(7,1);
            var note=chart.Notes[0];
            Assert.That(new RhythmJudge(chart).Submit(note.Direction,note.TimeSeconds+.08),Is.EqualTo("Perfect"));
            Assert.That(new RhythmJudge(chart).Submit(note.Direction,note.TimeSeconds+.081),Is.EqualTo("Good"));
            Assert.That(new RhythmJudge(chart).Submit(note.Direction,note.TimeSeconds+.151),Is.EqualTo("Extra"));
            foreach(var effect in Catalog.Effects)
            {
                var end=EffectPresentation.Path(effect.Id,note.Id,1,false);
                Assert.That(end.X,Is.EqualTo(0));Assert.That(end.Y,Is.EqualTo(0));
            }
        }

        [Test] public void HostLifecycleRequiresReadyPlayersAndStartsAfterMapAcknowledgements()
        {
            var game=new FestivalSimulation(42);
            var host=game.AddPlayer("host","Host");var guest=game.AddPlayer("guest","Guest");
            host.X=guest.X=0;host.Z=guest.Z=19;
            game.Execute("host",Command("ready-host","Ready"));game.Execute("guest",Command("ready-guest","Ready"));
            game.Tick(5.2);
            Assert.That(game.State.Phase,Is.EqualTo("Loading"));
            Assert.That(host.Ready||guest.Ready,Is.False);
            game.Execute("host",Command("loaded-host","MapReady"));
            Assert.That(game.State.Phase,Is.EqualTo("Loading"));
            game.Execute("guest",Command("loaded-guest","MapReady"));
            Assert.That(game.State.Phase,Is.EqualTo("Playing"));
        }

        [Test] public void LittleSpoonIsCheapestAndQuietlySoftensWookSuspicion()
        {
            var spoon=Catalog.FindItem("little_spoon");
            Assert.That(spoon,Is.Not.Null);
            Assert.That(spoon.Price,Is.GreaterThan(0));
            Assert.That(spoon.Description.ToLowerInvariant(),Does.Not.Contain("trust"));
            Assert.That(spoon.Description.ToLowerInvariant(),Does.Not.Contain("wook"));
            foreach(var item in Catalog.Items)
                if(item.Purchasable&&item.Id!=spoon.Id)Assert.That(spoon.Price,Is.LessThan(item.Price));
            Assert.That(Catalog.VendorOffers(42),Does.Contain(spoon.Id));

            var plain=new FestivalSimulation(42);var dressed=new FestivalSimulation(42);
            var ordinary=plain.AddPlayer("guest","Guest");var wearer=dressed.AddPlayer("guest","Guest");
            var spoonShelf=Catalog.ShopPoint(true,dressed.State.VendorOffers.IndexOf(spoon.Id));
            wearer.X=spoonShelf.X;wearer.Z=spoonShelf.Z;
            Assert.That(dressed.Execute(wearer.Id,new GameCommand{Id="hold-spoon",Kind="HoldOffer",ItemId=spoon.Id}).Accepted,Is.True);
            wearer.X=0;wearer.Z=7;
            Assert.That(dressed.Execute(wearer.Id,new GameCommand{Id="buy-spoon",Kind="Buy",ItemId=spoon.Id}).Accepted,Is.True);
            Assert.That(dressed.Execute(wearer.Id,new GameCommand{Id="buy-again",Kind="Buy",ItemId=spoon.Id}).Accepted,Is.False);
            foreach(var sim in new[]{plain,dressed})
            {
                sim.State.Phase="Playing";
                sim.State.Npcs.RemoveAll(n=>n.Id!="wook_0");
                var npc=sim.State.Npcs[0];npc.X=0;npc.Z=0;npc.Yaw=0;
                var player=sim.Player("guest");player.X=0;player.Z=2;player.SprintUntil=10;
                sim.Tick(2);
            }
            var ordinarySuspicion=plain.State.Npcs[0].Observers[0].Suspicion;
            var wearerSuspicion=dressed.State.Npcs[0].Observers[0].Suspicion;
            Assert.That(wearerSuspicion,Is.EqualTo(ordinarySuspicion*.9).Within(.001));
            Assert.That(dressed.Execute(wearer.Id,new GameCommand{Id="use-spoon",Kind="Use",ItemId=spoon.Id}).Accepted,Is.False);
        }

        [Test] public void CampPurchaseRequiresSellerAndLoadoutTravelsIntoTheFestival()
        {
            var game=new FestivalSimulation(21);
            var host=game.AddPlayer("host","Host");var guest=game.AddPlayer("guest","Guest");
            Assert.That(host.Z,Is.EqualTo(-9));
            Assert.That(game.Execute(host.Id,new GameCommand{Id="remote-buy",Kind="Buy",ItemId="little_spoon"}).Accepted,Is.False);
            var shelf=Catalog.ShopPoint(true,game.State.VendorOffers.IndexOf("little_spoon"));
            host.X=shelf.X;host.Z=shelf.Z;
            Assert.That(game.Execute(host.Id,new GameCommand{Id="camp-hold",Kind="HoldOffer",ItemId="little_spoon"}).Accepted,Is.True);
            host.X=0;host.Z=7;
            Assert.That(game.Execute(host.Id,new GameCommand{Id="camp-buy",Kind="Buy",ItemId="little_spoon"}).Accepted,Is.True);
            Assert.That(host.Inventory.Exists(item=>item.ItemId=="little_spoon"),Is.True);
            host.Z=guest.Z=19;guest.X=host.X=0;
            game.Execute(host.Id,Command("ready-host","Ready"));game.Execute(guest.Id,Command("ready-guest","Ready"));
            game.Tick(5.2);
            Assert.That(game.Execute(host.Id,Command("loaded-host","MapReady")).Accepted,Is.True);
            Assert.That(game.Execute(guest.Id,Command("loaded-guest","MapReady")).Accepted,Is.True);
            Assert.That(game.State.Phase,Is.EqualTo("Playing"));
            Assert.That(host.Z,Is.EqualTo(-29));
            Assert.That(host.Inventory.Exists(item=>item.ItemId=="little_spoon"),Is.True);
            game.State.Phase="Results";
            Assert.That(game.Execute(host.Id,Command("next-round","Reset")).Accepted,Is.True);
            Assert.That(game.State.Phase,Is.EqualTo("CampReview"));
            Assert.That(game.Player(host.Id).Z,Is.EqualTo(-9));
            Assert.That(game.Player(host.Id).Inventory,Is.Empty);
            Assert.That(game.Execute(host.Id,Command("early-shop","FinishReview")).Accepted,Is.False);
            for(int award=0;award<3;award++)
            {
                Assert.That(game.Execute(host.Id,new GameCommand{Id="host-award-"+award,Kind="ReviewVote",TargetId=guest.Id,Amount=award}).Accepted,Is.True);
                Assert.That(game.Execute(guest.Id,new GameCommand{Id="guest-award-"+award,Kind="ReviewVote",TargetId=guest.Id,Amount=award}).Accepted,Is.True);
            }
            Assert.That(game.Execute(host.Id,Command("open-shop","FinishReview")).Accepted,Is.True);
            Assert.That(game.State.Phase,Is.EqualTo("Shopping"));
        }

        [Test] public void SharedShelfReservesLastRareCopyAndReturnsUnpaidGearOnLaunch()
        {
            var game=new FestivalSimulation(42);
            var first=game.AddPlayer("first","First");var second=game.AddPlayer("second","Second");
            string rare=game.State.VendorOffers.Find(Catalog.RareShopItem);
            Assert.That(rare,Is.Not.Empty);
            int index=game.State.VendorOffers.IndexOf(rare);
            var shelf=Catalog.ShopPoint(true,index);
            first.X=second.X=shelf.X;first.Z=second.Z=shelf.Z;
            Assert.That(game.Execute(first.Id,new GameCommand{Id="hold-rare",Kind="HoldOffer",ItemId=rare}).Accepted,Is.True);
            Assert.That(game.Execute(second.Id,new GameCommand{Id="contend-rare",Kind="HoldOffer",ItemId=rare}).Accepted,Is.False);
            Assert.That(game.State.ShopStock.Find(s=>s.ItemId==rare).CampAvailable,Is.Zero);
            game.Disconnect(first.Id);
            Assert.That(game.State.ShopStock.Find(s=>s.ItemId==rare).CampAvailable,Is.EqualTo(1));
            first.Connected=true;
            Assert.That(game.Execute(second.Id,new GameCommand{Id="hold-again",Kind="HoldOffer",ItemId=rare}).Accepted,Is.True);
            first.X=second.X=0;first.Z=second.Z=19;
            Assert.That(game.Execute(first.Id,Command("ready-first","Ready")).Accepted,Is.True);
            Assert.That(game.Execute(second.Id,Command("ready-second","Ready")).Accepted,Is.True);
            game.Tick(5.2);
            Assert.That(game.State.Phase,Is.EqualTo("Loading"));
            Assert.That(second.HeldOfferId,Is.Empty);
            Assert.That(game.State.ShopStock.Find(s=>s.ItemId==rare).CampAvailable,Is.EqualTo(1));
            Assert.That(second.Inventory.Exists(i=>i.ItemId==rare),Is.False);
        }

        [Test] public void GuidanceFollowsTheLoopWithoutLeakingThePrivateTotemOrder()
        {
            var game=new FestivalSimulation(21);
            var host=game.AddPlayer("host","Host");
            var guest=game.AddPlayer("guest","Guest");
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("Camp supplies"));
            host.X=0;host.Z=7;
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("look at shelf props"));
            host.Ready=true;guest.Ready=true;
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("dancing"));

            game.State.Phase="Playing";
            host.X=0;host.Z=-29;
            var soberHint=FestivalGuidance.Hint(game.State,host);
            Assert.That(soberHint,Does.Contain("Night Market"));
            Assert.That(soberHint,Does.Not.Contain("Sun totem"));
            Assert.That(soberHint,Does.Not.Contain("Moon totem"));
            host.X=-18;host.Z=-22;
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("FREE CLUE TASTING"));
            host.X=0;host.Z=-29;
            guest.Effects.Add(new ActiveEffect{Id="mushrooms",RemainingSeconds=90});
            var affectedHint=FestivalGuidance.Hint(game.State,guest);
            Assert.That(affectedHint,Does.Contain("totem"));
            Assert.That(affectedHint,Does.Contain("sober teammate"));
            Assert.That(FestivalGuidance.Hint(game.State,host),Is.EqualTo(soberHint));

            game.State.CluesRead=2;
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("Dance"));
            game.State.GateOpened=true;
            // A viewer snapshot redacts the undiscovered friend's position.
            game.State.FriendPosition=new WorldPoint(0,0);
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("Search"));
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Not.Contain("Friend spotted"));
            game.State.FriendPosition=new WorldPoint(10,10);
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("Friend spotted"));
            game.State.FriendFound=true;
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("needs an escort"));
            game.State.FriendLeaderId=guest.Id;
            Assert.That(FestivalGuidance.Hint(game.State,guest),Does.Contain("Lead your friend"));
            guest.Life="Downed";
            Assert.That(FestivalGuidance.Hint(game.State,host),Does.Contain("needs an escort"));
        }

        private static GameCommand Command(string id,string kind)=>new GameCommand{Id=id,Kind=kind};
    }
}
