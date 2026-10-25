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
        // Both festivals share one ground, so one footprint check on each covers every level's second stage.
        [Test]public void TheSmallStageStandsOnOpenLawnClearOfEveryListedSpotOnBothFestivals()
        {
            root=new GameObject("Small stage placement");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var festival=root.transform.Find(FestivalWorld.RootName);var small=festival.Find(FestivalWorld.SmallStageName);
            Assert.That(small,Is.Not.Null,"the festival builds a small stage");
            var own=new HashSet<Transform>(small.GetComponentsInChildren<Transform>(true));
            var parts=small.GetComponentsInChildren<Renderer>();Assert.That(parts.Length,Is.GreaterThan(5),"setup: the small stage draws its deck, backdrop, speakers, riser and decks");
            var foot=Bounds(parts);var area=new Rect(foot.min.x,foot.min.z,foot.size.x,foot.size.z);
            float Gap(float x,float z){float dx=Mathf.Max(area.xMin-x,0,x-area.xMax),dz=Mathf.Max(area.yMin-z,0,z-area.yMax);return Mathf.Sqrt(dx*dx+dz*dz);}
            float RectGap(Rect r){float dx=Mathf.Max(r.xMin-area.xMax,area.xMin-r.xMax,0),dz=Mathf.Max(r.yMin-area.yMax,area.yMin-r.yMax,0);return Mathf.Sqrt(dx*dx+dz*dz);}
            TestContext.WriteLine("small stage footprint "+area+" top "+foot.max.y);
            Assert.That(area.xMin>=-38&&area.xMax<=38&&area.yMin>=-38&&area.yMax<=38,Is.True,"inside the fence: "+area);
            var stageDeck=festival.Find("Stage").GetComponent<Collider>().bounds;
            Assert.That(RectGap(new Rect(stageDeck.min.x,stageDeck.min.z,stageDeck.size.x,stageDeck.size.z)),Is.GreaterThanOrEqualTo(40),"40 m or more from the main stage");
            Assert.That(Vector2.Distance(new Vector2(StageMusic.SmallX,StageMusic.SmallZ),new Vector2(area.center.x,area.yMax)),Is.LessThan(.5f),"its lip is StageMusic's small stage front, where the speakers hang");
            var spots=new List<(string Name,float X,float Z,float Clear)>{("the camp gate",Festivals.CampGateX,Festivals.CampGateZ,Festivals.CampGateRadius+1),
                ("the DJ takeover",Catalog.StageTakeoverX,Catalog.StageTakeoverZ,2.5f),("the VIP stall",Festivals.VipStallX,Festivals.VipStallZ,2.5f),("the VIP guard",Festivals.VipGuardPostX,Festivals.VipGuardPostZ,2.5f),
                ("the Ferris wheel",Festivals.WheelX,Festivals.WheelZ,9),("the effigy's burn",Festivals.EffigyX,Festivals.EffigyZ,Festivals.BurnRadius),
                // Visions.cs keeps its secret spots private; they take in SplitObjective.cs's lost friends' spots.
                ("a secret spot",16,-4,2.5f),("a secret spot",-16,5,2.5f),("a secret spot",-24,25,2.5f),("a secret spot",25,24,2.5f),("a secret spot",18,5,2.5f)};
            foreach(var at in Festivals.GiggleTankSpots)spots.Add(("a Giggle Tank spot",at.X,at.Z,2.5f));
            foreach(var at in Festivals.BoothSpots)spots.Add(("a booth's ring",at.X,at.Z,FestivalSimulation.MinigameRingRadius+1));
            foreach(var row in Festivals.CashFindSpots)foreach(var at in row)spots.Add(("a cash find spot",at.X,at.Z,2));
            foreach(var landmark in CloudShapes.Landmarks)spots.Add(("the "+landmark.Name+" cloud stash",landmark.Stash.X,landmark.Stash.Z,2.5f));
            for(int i=0;i<FestivalCrowdLayout.Count;i++){var at=FestivalCrowdLayout.Spot(i);spots.Add(("festivalgoer "+i+"'s place",at.X,at.Z,2.8f));}
            // FestivalSimulation's MapReady puts the crew of up to eight at -7 + 2 i, -29.
            for(int i=0;i<8;i++)spots.Add(("crew member "+i+"'s arrival",-7+2*i,-29,1.5f));
            foreach(var spot in spots)Assert.That(Gap(spot.X,spot.Z),Is.GreaterThanOrEqualTo(spot.Clear),"the small stage "+area+" is within "+spot.Clear+" m of "+spot.Name+" at "+spot.X+", "+spot.Z);
            foreach(var zone in Festivals.VipZones)Assert.That(RectGap(new Rect(zone.MinX,zone.MinZ,zone.MaxX-zone.MinX,zone.MaxZ-zone.MinZ)),Is.GreaterThanOrEqualTo(3),"clear of the VIP ropes");
            // A lap of either loop takes under 40 s.
            for(double t=0;t<40;t+=.25)for(int k=0;k<Festivals.ArtCars;k++){var car=Festivals.ArtCarAt(k,t);Assert.That(Gap(car.X,car.Z),Is.GreaterThanOrEqualTo(5),"well off art car "+k+"'s loop");}
            foreach(var name in new[]{"Main footpath","Market footpath","East footpath","West crosspath","East crosspath"})
            {var path=festival.Find(name).GetComponent<Renderer>().bounds;Assert.That(RectGap(new Rect(path.min.x,path.min.z,path.size.x,path.size.z)),Is.GreaterThanOrEqualTo(2),"2 m or more off the "+name);}
            foreach(int index in new[]{Festivals.PoloFestival,Festivals.PlayaFestival})
            {
                world.SetTwists(new RoundState{Phase="Playing",FestivalIndex=index});Physics.SyncTransforms();
                var grown=foot;grown.Expand(new Vector3(2,0,2));
                foreach(var c in festival.GetComponentsInChildren<Collider>())
                    if(c.enabled&&!own.Contains(c.transform)&&c.name!="Ground"&&!c.name.EndsWith(" boundary",System.StringComparison.Ordinal))
                        Assert.That(c.bounds.Intersects(grown),Is.False,Festivals.Name(index)+": "+c.name+" stands within 1 m of the small stage");
                foreach(var r in festival.GetComponentsInChildren<Renderer>())
                    if(!own.Contains(r.transform)&&r.bounds.size.x<30&&r.bounds.size.z<30&&!IsAmbient(r.transform))
                        Assert.That(r.bounds.Intersects(foot),Is.False,Festivals.Name(index)+": "+Path(r.transform)+" "+r.bounds+" overlaps the small stage");
            }
            var dj=small.Find("small_stage_dj");Assert.That(dj,Is.Not.Null,"a DJ plays the small stage");
            Assert.That(dj.GetComponent<FestivalCharacter>().Pose,Is.EqualTo("Dj"));Assert.That(dj.position.y,Is.GreaterThan(.5f),"up on its deck");
            Assert.That(world.NavigationReady,Is.True,"setup: the festival navmesh");
            Assert.That(NavMesh.SamplePosition(new Vector3(StageMusic.SmallX,0,StageMusic.SmallZ+2.5f),out var lawn,.3f,NavMesh.AllAreas),Is.True,"the lawn at its lip is walkable");
            var route=new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(new Vector3(-7,0,-29),lawn.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete,Is.True,"the crew walks from its arrival to the small stage's lip");
        }
        private static bool IsAmbient(Transform t){for(;t!=null;t=t.parent)if(t.name.StartsWith("ambient_",System.StringComparison.Ordinal))return true;return false;}
        private static string Path(Transform t){var name=t.name;for(t=t.parent;t!=null&&t.name!=FestivalWorld.RootName;t=t.parent)name=t.name+"/"+name;return name;}
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
            Assert.That(crowd.MemberCount,Is.EqualTo(67),"62, and five dancing at the small stage");
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

        // Sight lines are tested against rendered meshes, not colliders: the bars have none, and the front collider is invisible.
        [Test]public void ADetainedPlayerIsHeldInsideTheSecurityBuildingWhereFriendsCanSeeThem()
        {
            root=new GameObject("Holding world");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");Physics.SyncTransforms();
            var grounds=root.transform.Find(FestivalWorld.RootName);var building=Festivals.SecurityBuilding;
            var held=new Vector3(building.CentreX,0,building.CentreZ);
            Assert.That(Festivals.InHolding(held.x,held.z),Is.True,"setup: the rules hold a detained player in the middle");
            // Walking out of the middle any way, a body (the host's .32 m capsule, as FestivalSession.Slide casts it) meets one of the
            // building's walls while still inside the room the rules allow, so walls slide it along and the rules never stop it dead.
            for(int angle=0;angle<360;angle+=15)
            {
                var way=Quaternion.Euler(0,angle,0)*Vector3.forward;
                Assert.That(Physics.CapsuleCast(held+Vector3.up*.38f,held+Vector3.up*1.4f,.32f,way,out var hit,20,~0,QueryTriggerInteraction.Ignore),Is.True,"walking out at "+angle+" degrees meets a wall");
                var stop=held+way*hit.distance;
                Assert.That(building.Contains(stop.x,stop.z),Is.True,"at "+angle+" degrees the wall is the building's own ("+hit.collider.name+" at "+stop+")");
                Assert.That(Festivals.InHolding(stop.x,stop.z),Is.True,"at "+angle+" degrees the wall stops a body, sliding it along, before the rules refuse the step ("+hit.collider.name+" at "+stop+")");
            }
            foreach(var outside in new[]{new Vector3(27,1,5),new Vector3(24,1,2)})
                Assert.That(building.Contains(outside.x,outside.z),Is.False,"setup: "+outside+" is outside");
            // A friend at the desk, standing a little to either side: bars hide the chest from some eyes, never a wall from any.
            var chest=held+Vector3.up*1.3f;int clear=0,eyes=0;var bars=new List<string>();
            for(float across=-.4f;across<.45f;across+=.1f)
            {
                var eye=new Vector3(27+across,1.65f,5);eyes++;
                string blocker=Blocker(grounds,eye,chest,false);
                Assert.That(blocker,Is.Null,"from the pay desk at "+eye+", the line to the held player's chest meets "+blocker);
                string bar=Blocker(grounds,eye,chest,true);if(bar==null)clear++;else bars.Add(bar);
            }
            Assert.That(clear*2,Is.GreaterThanOrEqualTo(eyes),"from most places at the desk the chest shows between the bars; hidden by "+string.Join(", ",bars));
            for(float yaw=0;yaw<360;yaw+=45)foreach(float pitch in new[]{-30f,0,30})
            {
                FestivalThirdPerson.Place(held+Vector3.up*1.65f,yaw,pitch,out float boom);
                Assert.That(FestivalThirdPerson.Mask(0,boom),Is.Not.Zero,"held in the middle, looking "+yaw+" degrees round and "+pitch+" up or down, the over-the-shoulder view stays out of your head (boom "+boom+" m)");
            }
        }
        // The first rendered mesh of the festival grounds the line from a to b meets, or null. Bars and their rails count only
        // when bars is true; the rules' invisible colliders never do.
        static string Blocker(Transform grounds,Vector3 a,Vector3 b,bool bars)
        {
            var ray=new Ray(a,b-a);float length=Vector3.Distance(a,b);
            foreach(var renderer in grounds.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||!renderer.bounds.IntersectRay(ray,out float near)||near>length)continue;
                if(!bars&&renderer.name.StartsWith("Holding front",System.StringComparison.Ordinal))continue;
                var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
                var vertices=mesh.vertices;var triangles=mesh.triangles;var toWorld=renderer.transform.localToWorldMatrix;
                for(int i=0;i<triangles.Length;i+=3)
                    if(Meets(ray,length,toWorld.MultiplyPoint3x4(vertices[triangles[i]]),toWorld.MultiplyPoint3x4(vertices[triangles[i+1]]),toWorld.MultiplyPoint3x4(vertices[triangles[i+2]])))return renderer.name;
            }
            return null;
        }
        // Moller-Trumbore: whether the ray meets triangle abc within length.
        static bool Meets(Ray ray,float length,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=b-a,ac=c-a,p=Vector3.Cross(ray.direction,ac);float det=Vector3.Dot(ab,p);if(Mathf.Abs(det)<1e-8f)return false;
            Vector3 s=ray.origin-a;float u=Vector3.Dot(s,p)/det;if(u<0||u>1)return false;
            Vector3 q=Vector3.Cross(s,ab);float v=Vector3.Dot(ray.direction,q)/det;if(v<0||u+v>1)return false;
            float t=Vector3.Dot(ac,q)/det;return t>0&&t<length;
        }
    }
}
