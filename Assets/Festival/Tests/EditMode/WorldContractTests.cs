using System.Collections.Generic;
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
    public sealed class WorldContractTests
    {
        private GameObject root;
        // Every camp secret is a visible prop at the host's point. No prop collides, so the camp navmesh and routes are unchanged.
        [Test]public void EveryCampSecretHasAVisibleNonBlockingPropAtTheAuthoritativePoint()
        {
            root=new GameObject("impl:U65 camp egg placement");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Shopping");
            var secrets=root.transform.Find(FestivalWorld.CampRootName).Find(FestivalCampEggView.Name);
            Assert.That(secrets,Is.Not.Null,"the camp builds its secrets");Assert.That(world.CampEggs,Is.Not.Null);
            var published=new FestivalSimulation(0).State.CampEggs;
            foreach(var egg in CampFeatures.Eggs)
            {
                var prop=secrets.Find(FestivalCampEggView.PropName(egg.Id));Assert.That(prop,Is.Not.Null,"the shipped host interaction "+egg.Id+" has no discoverable prop");
                Assert.That(world.CampEggs.Prop(egg.Id),Is.SameAs(prop));
                var point=CampFeatures.EggPoint(egg,0);
                Assert.That(prop.localPosition.x,Is.EqualTo(point.X));Assert.That(prop.localPosition.z,Is.EqualTo(point.Z));Assert.That(prop.localPosition.y,Is.Zero);
                var renderers=Drawn(prop);Assert.That(renderers,Is.Not.Empty,egg.Id+" draws something");
                foreach(var r in renderers){Assert.That(r.sharedMaterial,Is.Not.Null,egg.Id+" "+r.name+" has a material");Assert.That(r.gameObject.layer,Is.Not.EqualTo(31),egg.Id+" is not on the hidden local-body layer");}
                Assert.That(prop.GetComponentsInChildren<Collider>(true),Is.Empty,"leave the standing point accessible");
                var bounds=Bounds(renderers);Assert.That(bounds.min.y,Is.GreaterThan(-.05f),egg.Id+" stands on or above the ground");
                var aim=FestivalCampEggView.AimPoint(published.Find(e=>e.Id==egg.Id));bounds.Expand(.1f);
                Assert.That(bounds.Contains(aim),Is.True,egg.Id+" aim point "+aim+" is on its rendered prop "+bounds);
                Assert.That(FestivalCampEggView.AimRadius(egg.Id),Is.InRange(.1f,1f),egg.Id+" is aimed at within 1 m");
            }
            Assert.That(secrets.childCount,Is.EqualTo(CampFeatures.Eggs.Length),"one prop for each secret");
        }
        // Each host point (both gnome spots) is inside camp, has standing room on the walkable camp, and stays 1 m clear of every
        // door, path, shelf, seller, trailhead, DJ and Giggle Balloon zone. The button sits on the DJ table and the dust on car_1 on purpose.
        [Test]public void EveryCampSecretStandsInsideCampWithRoomAndClearOfRoutesDoorsShelvesAndBalloons()
        {
            root=new GameObject("impl:U65 camp egg clearance");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Shopping");Physics.SyncTransforms();
            Assert.That(world.CampNavigationReady,Is.True,"setup: the camp navmesh");
            var camp=root.transform.Find(FestivalWorld.CampRootName);
            var colliders=new List<Collider>();foreach(var c in camp.GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger&&!c.name.StartsWith("Camp earth",System.StringComparison.Ordinal))colliders.Add(c);
            Assert.That(colliders.Count,Is.GreaterThan(20),"setup: camp tents, cars, posts and tables collide");
            // Zone centre and the host or HUD reach that offers its action there.
            var zones=new List<(string Name,Vector2 At,float Reach)>{("DJ",new Vector2(CampFeatures.DjX,CampFeatures.DjZ),3),("seller",new Vector2(CampSellerTalk.X,CampSellerTalk.Z),CampSellerTalk.Reach),
                ("trailhead",new Vector2(0,19),3.2f),("balloons",new Vector2(CampFeatures.GiggleBalloonX,CampFeatures.GiggleBalloonZ),CampFeatures.GiggleBalloonReach)};
            foreach(var site in CampFeatures.Sites)zones.Add((site.Id,new Vector2(site.X,site.Z),3.5f));
            for(int i=0;i<4;i++){var shelf=Catalog.ShopPoint(true,i);zones.Add(("shelf "+i,new Vector2(shelf.X,shelf.Z),3.3f));}
            var paths=new List<Bounds>();foreach(var name in new[]{"Camp footpath","Camp shade lane","Trailhead path"})paths.Add(camp.Find(name).GetComponent<Renderer>().bounds);
            paths.Add(Bounds(camp.Find(FestivalGiggleTank.BalloonsName).GetComponentsInChildren<Renderer>()));
            foreach(var shelf in camp.GetComponentsInChildren<Transform>())if(shelf.name=="FestivalCampShelf")paths.Add(Bounds(shelf.GetComponentsInChildren<Renderer>()));
            var points=new List<(string Id,Vector2 At)>();
            foreach(var egg in CampFeatures.Eggs)if(egg.Id!="gnome")points.Add((egg.Id,new Vector2(egg.X,egg.Z)));
            foreach(var spot in CampFeatures.GnomeSpots)points.Add(("gnome",new Vector2(spot.X,spot.Z)));
            var path=new NavMeshPath();
            foreach(var (id,at) in points)
            {
                string where=id+" at "+at;
                Assert.That(Mathf.Abs(at.x)<=CampFeatures.CampHalfWidth-1&&Mathf.Abs(at.y)<=CampFeatures.CampHalfDepth-1,Is.True,where+" is inside camp");
                var standing=new Vector3(at.x,1,at.y);
                foreach(var c in colliders)Assert.That(Vector3.Distance(c.ClosestPoint(standing),standing),Is.GreaterThan(.4f),where+" is blocked by "+c.name);
                Assert.That(NavMesh.SamplePosition(new Vector3(at.x,0,at.y),out var ground,.3f,NavMesh.AllAreas),Is.True,where+" is off the walkable camp");
                Assert.That(NavMesh.CalculatePath(Vector3.zero,ground.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True,where+" cannot be walked to");
                foreach(var zone in zones)
                {
                    if(id=="button"&&zone.Name=="DJ"||id=="wash_me"&&zone.Name=="car_1")continue;
                    Assert.That(Vector2.Distance(at,zone.At),Is.GreaterThanOrEqualTo(zone.Reach+1),where+" is within 1 m of the "+zone.Name+" zone");
                }
                foreach(var b in paths){var near=new Vector2(Mathf.Clamp(at.x,b.min.x,b.max.x),Mathf.Clamp(at.y,b.min.z,b.max.z));Assert.That(Vector2.Distance(near,at),Is.GreaterThanOrEqualTo(1),where+" is within 1 m of a path, shelf or the balloons "+b);}
            }
            foreach(var egg in CampFeatures.Eggs)
            {
                if(egg.Id=="wash_me")continue;
                var drawn=Bounds(Drawn(world.CampEggs.Prop(egg.Id)));
                foreach(var c in colliders)Assert.That(drawn.Intersects(c.bounds),Is.False,egg.Id+" prop stands inside "+c.name);
            }
        }
        // The gnome stands where the host's published visit put it, and moves when a visit picks the other spot.
        [Test]public void TheGnomePropFollowsThePublishedSpotOfEachVisit()
        {
            root=new GameObject("impl:U65 gnome visits");var world=root.AddComponent<FestivalWorld>();world.Build();
            var gnome=world.CampEggs.Prop("gnome");var box=world.CampEggs.Prop("lost_found");var rest=box.localPosition;
            foreach(int seed in new[]{1,0,1})
            {
                var eggs=new FestivalSimulation(seed).State.CampEggs;var spot=eggs.Find(e=>e.Id=="gnome");
                if(new Vector2(gnome.localPosition.x,gnome.localPosition.z)!=new Vector2(spot.X,spot.Z))
                    LogAssert.Expect(LogType.Log,new Regex(@"^\[Festival\.CampEggs\] action=prop_moved egg=gnome "));
                world.CampEggs.Show(eggs);
                Assert.That(gnome.localPosition,Is.EqualTo(new Vector3(spot.X,0,spot.Z)),"visit "+seed+" shows the gnome at its published spot");
                Assert.That(box.localPosition,Is.EqualTo(rest),"the other props keep their fixed points");
            }
            world.CampEggs.Show(null);world.CampEggs.Show(new List<CampEggState>{null,new CampEggState{Id="not_an_egg",X=3,Z=3}});
            Assert.That(gnome.localPosition,Is.EqualTo(new Vector3(CampFeatures.GnomeSpots[1].X,0,CampFeatures.GnomeSpots[1].Z)),"no records or unknown records move nothing");
        }
        // The editor's asset unload destroys shared art materials that no object holds. A destroyed one is made again, never handed out.
        [Test]public void AnUnloadedArtMaterialIsMadeAgain()
        {
            var rose=FestivalArtView.MaterialFor("Rose");Assert.That(rose!=null,Is.True,"setup: the shared material");
            Object.DestroyImmediate(rose);
            var again=FestivalArtView.MaterialFor("Rose");
            Assert.That(again!=null,Is.True,"a destroyed shared material is not handed out");Assert.That(again.name,Is.EqualTo("Festival art Rose"));
        }
        // Six stage crew join the resident DJ on the deck of both festivals: on its top, clear of the mixer, its riser, cables and
        // ramp, of Palm Mirage's stage dressing, of anything solid, of each other and of the aisle from the stage front to the DJ.
        [Test]public void SixStageCrewStandOnTheDeckClearOfTheMixerEachOtherAndTheDjAisleOnBothFestivals()
        {
            root=new GameObject("impl:U68 stage crew placement");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var festival=root.transform.Find(FestivalWorld.RootName);
            var crew=new List<Transform>();
            foreach(Transform child in festival)
            {
                if(child.name.StartsWith(FestivalStageCrew.Prefix,System.StringComparison.Ordinal))crew.Add(child);
                Assert.That(child.name.StartsWith("stage_hype_",System.StringComparison.Ordinal),Is.False,"the two hype dancers are two of the six, not extras");
            }
            Assert.That(crew.Count,Is.EqualTo(6),"six stage crew");
            var dj=festival.Find("resident_stage_dj");Assert.That(dj,Is.Not.Null,"the resident DJ stays");
            var resident=dj.GetComponent<FestivalCharacter>();Assert.That(resident.Pose,Is.EqualTo("Dj"));Assert.That(resident.DjConsole,Is.SameAs(festival.Find("FestivalDJDeck")),"at the mixer");
            var poses=new List<string>();foreach(var actor in crew)poses.Add(actor.GetComponent<FestivalCharacter>().Pose);
            Assert.That(poses.FindAll(p=>p=="Dance").Count,Is.EqualTo(4),"two hype and two go-go dancers");Assert.That(poses,Does.Contain(FestivalCharacter.McPose),"an MC");
            Assert.That(crew.FindAll(c=>System.Array.Exists(c.GetComponentsInChildren<Transform>(true),t=>t.name==FestivalFilmingPhone.PhoneName&&t.gameObject.activeSelf)).Count,Is.EqualTo(1),"a photographer, phone up");
            var deck=festival.Find("Stage").GetComponent<Collider>().bounds;
            var aisleFrom=new Vector2(0,deck.min.z);var aisleTo=new Vector2(dj.position.x,dj.position.z);
            // The mixer, its riser, cables and ramp, and Palm Mirage's stage dressing are measured by their meshes, not their boxes.
            var gear=new List<Collider>();
            foreach(var name in new[]{"FestivalDJDeck","FestivalDJRiser",FestivalPoloDressing.RootName+"/FestivalStageMirage",FestivalPoloDressing.RootName+"/FestivalPetalCanopy"})
                foreach(var mesh in festival.Find(name).GetComponentsInChildren<MeshFilter>(true))gear.Add(mesh.gameObject.AddComponent<MeshCollider>());
            Assert.That(gear.Count,Is.GreaterThan(10),"setup: the stage gear has meshes to measure");
            foreach(int index in new[]{Festivals.PoloFestival,Festivals.PlayaFestival})
            {
                world.SetTwists(new RoundState{Phase="Playing",FestivalIndex=index});Physics.SyncTransforms();
                Assert.That(festival.Find(FestivalPoloDressing.RootName).gameObject.activeSelf,Is.EqualTo(index==Festivals.PoloFestival),"setup: "+Festivals.Name(index)+"'s stage");
                foreach(var actor in crew)
                {
                    string who=actor.name+" at "+actor.position+" on "+Festivals.Name(index);var at=new Vector2(actor.position.x,actor.position.z);
                    Assert.That(actor.position.y,Is.EqualTo(deck.max.y).Within(.001f),who+" stands on the deck top");
                    Assert.That(actor.position.x,Is.InRange(deck.min.x+.5f,deck.max.x-.5f),who+" is inside the deck's width");
                    Assert.That(actor.position.z,Is.InRange(deck.min.z+.5f,deck.max.z-.5f),who+" is inside the deck's depth");
                    Assert.That(actor.GetComponentsInChildren<Collider>(true),Is.Empty,who+" is presentation only");
                    foreach(var c in Physics.OverlapCapsule(actor.position+Vector3.up*.1f,actor.position+Vector3.up*2,.7f,~0,QueryTriggerInteraction.Collide))
                        Assert.That(c.name,Is.EqualTo("Stage"),who+" stands within .7 m of "+c.name+(gear.Contains(c)?" ("+c.transform.parent.name+")":""));
                    foreach(var other in crew)if(other!=actor)Assert.That(Vector2.Distance(at,new Vector2(other.position.x,other.position.z)),Is.GreaterThanOrEqualTo(.7f),who+" crowds "+other.name);
                    Assert.That(Vector2.Distance(at,aisleTo),Is.GreaterThanOrEqualTo(.7f),who+" crowds the DJ");
                    var along=aisleTo-aisleFrom;float t=Mathf.Clamp01(Vector2.Dot(at-aisleFrom,along)/along.sqrMagnitude);
                    Assert.That(Vector2.Distance(at,aisleFrom+along*t),Is.GreaterThanOrEqualTo(.7f),who+" stands in the aisle from the stage front to the DJ");
                }
            }
            world.SetPhase("Shopping");foreach(var actor in crew)Assert.That(actor.gameObject.activeInHierarchy,Is.False,actor.name+" is hidden at camp");
        }
        // The stage block hides its riser from the host's waist-high sight line, yet a festivalgoer watching the DJ set there keeps the
        // DJ in view through the whole set, so the set never quietly cools their suspicion.
        [Test]public void AWatcherKeepsTheDjOnTheRiserInViewThroughTheHostsSightCheck()
        {
            root=new GameObject("DJ riser sight");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");Physics.SyncTransforms();
            Assert.That(FestivalSession.LineOfSight(0,20,Catalog.StageDjX,Catalog.StageDjZ),Is.False,"setup: the stage blocks the host's sight line to the mixer");
            var game=new FestivalSimulation(9);var p=game.AddPlayer("p0","P0");game.State.Phase="Playing";game.State.SimulationSeconds=10;game.State.Npcs.Clear();game.HasLineOfSight=FestivalSession.LineOfSight;
            p.X=Catalog.StageTakeoverX;p.Z=Catalog.StageTakeoverZ;p.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});
            var seen=new ObserverState{PlayerId="p0",Suspicion=40};var watcher=new NpcState{Id="watcher",Kind="Wook",X=0,Z=20,Yaw=0};watcher.Observers.Add(seen);game.State.Npcs.Add(watcher);
            Assert.That(game.Execute("p0",new GameCommand{Id="dj",Kind="Dj"}).Accepted,Is.True,"setup: the set starts");
            for(int step=0;step<2000&&p.InteractionId!="";step++){game.Tick(.05);if(p.InteractionId!="")Assert.That(game.State.SimulationSeconds-seen.LastSeenSeconds,Is.LessThan(.1),"the watcher sees the DJ at "+game.State.SimulationSeconds+", last saw them at "+seen.LastSeenSeconds);}
            Assert.That(p.InteractionId,Is.Empty,"setup: the whole set played");
            Assert.That(seen.Suspicion,Is.EqualTo(40).Within(.001),"the set leaves the watcher's suspicion where it was");
        }
        // The host's navigation stops a swarm at the stage lip, short of the riser: it strikes the DJ from the stage front, as it did
        // when the set was played there, so a stage pass never shelters a swarmed player.
        [Test]public void ASwarmAtTheStageFrontStrikesTheDjOnTheRiser()
        {
            root=new GameObject("DJ riser swarm");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");Physics.SyncTransforms();
            Assert.That(world.NavigationReady,Is.True,"setup: the host's navigation is ready");
            var game=new FestivalSimulation(9);var p=game.AddPlayer("p0","P0");game.State.Phase="Playing";game.State.SimulationSeconds=10;game.State.Npcs.Clear();
            game.HasLineOfSight=FestivalSession.LineOfSight;game.Navigate=FestivalSession.Navigate;
            p.X=Catalog.StageTakeoverX;p.Z=Catalog.StageTakeoverZ;p.Inventory.Add(new ItemStack{ItemId="stage_pass",Count=1});
            for(int k=0;k<4;k++){var n=new NpcState{Id="swarm"+k,Kind="Wook",X=-1.5f+k,Z=21,Yaw=0};n.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=95,AccusationSeconds=0,LastSeenSeconds=10});game.State.Npcs.Add(n);}
            Assert.That(game.Execute("p0",new GameCommand{Id="dj",Kind="Dj"}).Accepted,Is.True,"setup: the set starts");
            for(int step=0;step<300&&p.Health==100;step++)game.Tick(.05);
            Assert.That(p.Health,Is.LessThan(100),"a swarm strikes the DJ within 15 s; wooks at "+string.Join(" ",game.State.Npcs.ConvertAll(n=>n.X.ToString("0.0")+","+n.Z.ToString("0.0"))));
            Assert.That(p.InteractionId,Is.Not.Empty,"the strike lands mid-set");
        }
        private static Renderer[] Drawn(Transform prop)=>System.Array.FindAll(prop.GetComponentsInChildren<Renderer>(),r=>r.enabled);
        private static Bounds Bounds(Renderer[] renderers){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        [TearDown]public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);}

        // The resident DJ works the main mixer where a stage-pass DJ will stand (Catalog.StageDjX/Z, on the riser). While a
        // player DJs, the resident steps aside onto the deck, clear of the mixer and the crew, and hypes the crowd; then goes back.
        [Test]public void TheResidentStepsAsideWhileAPlayerDjsAndGoesBackAfter()
        {
            root=new GameObject("Resident aside test");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var festival=root.transform.Find(FestivalWorld.RootName);var dj=festival.Find("resident_stage_dj");var resident=dj.GetComponent<FestivalCharacter>();
            var mixerSpot=new Vector3(Catalog.StageDjX,FestivalWorld.StageDjHeight,Catalog.StageDjZ);
            Assert.That(world.StageDjConsole,Is.SameAs(festival.Find("FestivalDJDeck")),"the main mixer");
            Assert.That(Vector3.Distance(dj.position,mixerSpot),Is.LessThan(.01f),"the resident stands where a stage-pass DJ will");
            world.SetStageTakeover(true);
            Assert.That(resident.Pose,Is.EqualTo(FestivalCharacter.McPose),"the resident hypes the crowd");Assert.That(resident.DjConsole,Is.Null,"off the decks");
            var flat=new Vector2(dj.position.x,dj.position.z);
            Assert.That(Vector2.Distance(flat,new Vector2(Catalog.StageDjX,Catalog.StageDjZ)),Is.GreaterThan(1.5f),"clear of the mixer, at "+dj.position);
            foreach(Transform child in festival)if(child.name.StartsWith(FestivalStageCrew.Prefix,System.StringComparison.Ordinal))
                Assert.That(Vector2.Distance(flat,new Vector2(child.position.x,child.position.z)),Is.GreaterThan(.7f),"clear of "+child.name);
            var deck=festival.Find("Stage").GetComponent<Collider>().bounds;
            Assert.That(dj.position.x>deck.min.x+.5f&&dj.position.x<deck.max.x-.5f&&dj.position.z>deck.min.z+.5f&&dj.position.z<deck.max.z-.5f,Is.True,"still up on the stage");
            Assert.That(dj.position.y,Is.EqualTo(deck.max.y).Within(.02f),"standing on its deck");
            // The mixer, its riser, cables and ramp, and Palm Mirage's stage dressing are measured by their meshes, as for the crew.
            foreach(var name in new[]{"FestivalDJDeck","FestivalDJRiser",FestivalPoloDressing.RootName+"/FestivalStageMirage",FestivalPoloDressing.RootName+"/FestivalPetalCanopy"})
                foreach(var mesh in festival.Find(name).GetComponentsInChildren<MeshFilter>(true))mesh.gameObject.AddComponent<MeshCollider>();
            foreach(int index in new[]{Festivals.PoloFestival,Festivals.PlayaFestival})
            {
                world.SetTwists(new RoundState{Phase="Playing",FestivalIndex=index});Physics.SyncTransforms();
                foreach(var c in Physics.OverlapCapsule(dj.position+Vector3.up*.1f,dj.position+Vector3.up*2,.7f,~0,QueryTriggerInteraction.Collide))
                    Assert.That(c.name,Is.EqualTo("Stage"),"the resident stands within .7 m of "+c.name+" on "+Festivals.Name(index));
            }
            world.SetStageTakeover(true);Assert.That(resident.Pose,Is.EqualTo(FestivalCharacter.McPose),"a repeat changes nothing");
            world.SetStageTakeover(false);
            Assert.That(resident.Pose,Is.EqualTo("Dj"));Assert.That(resident.DjConsole,Is.SameAs(world.StageDjConsole),"back at the mixer");
            Assert.That(Vector3.Distance(dj.position,mixerSpot),Is.LessThan(.01f),"where they started");
        }

        // After each phrase of a DJ set, festivalgoers within 20 m of the stage front react to its score while the cheer
        // lasts: from 0.75 all cheer, from 0.4 about half sway, under 0.4 they stand and watch. Security, anyone after a player and
        // anyone further off keep their pose.
        [Test]public void TheCrowdNearTheStageReactsToThePhrasesScore()
        {
            var s=new RoundState{Phase="Playing",SimulationSeconds=40,StageCheerScore=-1,StageCheerUntilWorldSeconds=-1};
            var fans=new List<NpcState>();for(int k=0;k<20;k++)fans.Add(new NpcState{Id="wook_"+k,Kind="Wook",X=-9+k,Z=14,IdlePose="Watching"});
            var far=new NpcState{Id="far",Kind="Wook",X=0,Z=-10,IdlePose="Idle"};var cop=new NpcState{Id="cop",Kind="Cop",X=0,Z=20,Mode="Patrol"};
            var after=new NpcState{Id="after",Kind="Wook",X=1,Z=20,Mode="Accusing"};
            string Pose(NpcState n)=>Festival.Network.FestivalSession.StageCrowdPose(s,n);
            foreach(var n in fans)Assert.That(Pose(n),Is.EqualTo("Watching"),"no set, no reaction");
            s.StageCheerUntilWorldSeconds=46;
            s.StageCheerScore=.8;foreach(var n in fans)Assert.That(Pose(n),Is.EqualTo("Cheer"),"from .75 everyone near the stage cheers");
            fans[0].Mode="Distracted";Assert.That(Pose(fans[0]),Is.EqualTo("Cheer"),"a distracted fan cheers too");fans[0].Mode="Blending";
            Assert.That(Pose(far),Is.EqualTo("Idle"),"one far off keeps their pose");Assert.That(Pose(cop),Is.EqualTo("Patrol"),"security keeps theirs");
            Assert.That(Pose(after),Is.EqualTo("Accusing"),"one after a player keeps after them");
            s.StageCheerScore=.5;int swaying=fans.FindAll(n=>Pose(n)=="Dance").Count;
            Assert.That(swaying,Is.InRange(5,15),"from .4 about half sway, got "+swaying);Assert.That(fans.TrueForAll(n=>Pose(n)=="Dance"||Pose(n)=="Watching"),Is.True,"and the rest keep their pose");
            s.StageCheerScore=.2;foreach(var n in fans)Assert.That(Pose(n),Is.EqualTo("Idle"),"under .4 they just stand and watch");
            s.SimulationSeconds=46;s.StageCheerScore=1;foreach(var n in fans)Assert.That(Pose(n),Is.EqualTo("Watching"),"and once the cheer is over, the crowd is back to itself");
        }
        [Test]public void BuildPreservesManualChildrenAndDoesNotDuplicateGeometry()
        {
            root=new GameObject("Test world");var manual=new GameObject("Manual decoration");manual.transform.SetParent(root.transform);
            var world=root.AddComponent<FestivalWorld>();world.Build();int count=root.GetComponentsInChildren<Transform>().Length;
            world.Build();Assert.That(root.GetComponentsInChildren<Transform>().Length,Is.EqualTo(count));Assert.That(manual,Is.Not.Null);
            Assert.That(root.transform.childCount,Is.EqualTo(3));
        }
        [Test]public void WorldHasSolidPerimeterAndNonBlockingCosmeticCrowd()
        {
            root=new GameObject("Test world");root.AddComponent<FestivalWorld>().Build();var generated=root.transform.Find(FestivalWorld.RootName);
            foreach(string side in new[]{"North","South","West","East"})Assert.That(generated.Find(side+" boundary").GetComponent<BoxCollider>().enabled,Is.True);
            var crowd=generated.GetComponent<FestivalAmbientCrowd>();
            Assert.That(crowd,Is.Not.Null);
            Assert.That(crowd.MemberCount,Is.EqualTo(62));
            Assert.That(crowd.WalkerCount,Is.EqualTo(12));
            int count=0;foreach(Transform child in generated)if(child.name.StartsWith("ambient_")){count++;Assert.That(child.GetComponentsInChildren<Collider>(true),Is.Empty);}
            Assert.That(count,Is.EqualTo(crowd.MemberCount));
        }
        [Test]public void InteractionLandmarksHaveStandingRoom()
        {
            root=new GameObject("Test world");var world=root.AddComponent<FestivalWorld>();world.SetPhase("Playing");Physics.SyncTransforms();
            foreach(var point in new[]{new Vector3(-18,1,-22),new Vector3(24,1,-20),new Vector3(27,1,5),new Vector3(-25,1,-8),new Vector3(0,1,-32),new Vector3(-28,1,16),new Vector3(16,1,-4),new Vector3(-16,1,5)})
                foreach(var collider in root.GetComponentsInChildren<Collider>())
                    if(collider.enabled)Assert.That(Vector3.Distance(collider.ClosestPoint(point),point),Is.GreaterThan(.4f),"Blocked interaction landmark "+point);
        }
    }
}
