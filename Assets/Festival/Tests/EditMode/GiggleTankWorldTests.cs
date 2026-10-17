using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Tests
{
    // GAS-2: a Giggle Tank stand-in, built from primitives, stands on the festival grounds wherever a living player's view has a
    // tank, with room all round it to grab it, and goes once someone has. It collides with nothing, and a spirit's view has none.
    // Whoever grabs it, the whole crew hears a deep "oh yeah!", and whoever carries it home is told so.
    public sealed class GiggleTankWorldTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        [Test]public void ThePlaceholderIsATankThatNeverCollides()
        {
            var parent=Made("Tank parent").transform;var tank=FestivalGiggleTank.Build(parent);
            Assert.That(tank.parent,Is.EqualTo(parent),"it is built where it is asked for");
            var renderers=tank.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length,Is.GreaterThanOrEqualTo(4),"a tank, its valve and a glow to find it by");
            Assert.That(System.Array.Exists(renderers,r=>r.name.StartsWith("GiggleTank",System.StringComparison.Ordinal)),Is.True,"the tank is ART-1's model");
            foreach(var renderer in renderers)Assert.That(renderer.sharedMaterial,Is.Not.Null,renderer.name+" has a material");
            foreach(var renderer in renderers)Assert.That(renderer.shadowCastingMode,Is.EqualTo(ShadowCastingMode.On),renderer.name+" casts a shadow, as the primitive tank did");
            var bounds=Bounds(tank);
            Assert.That(bounds.min.y,Is.EqualTo(0).Within(.01f),"it stands on the ground");
            Assert.That(bounds.max.y,Is.InRange(.9f,1.6f),"about waist high");
            Assert.That(new Vector2(bounds.center.x,bounds.center.z).magnitude,Is.LessThan(.01f),"centred on its spot");
            Assert.That(tank.GetComponentsInChildren<Collider>(true),Is.Empty,"it never blocks a step, a sight line or the navmesh");
        }

        [Test]public void TheWorldShowsTheTankWhereTheViewHasOneWithRoomToGrabIt()
        {
            var root=Made("Tank world");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");Physics.SyncTransforms();
            var grounds=root.transform.Find(FestivalWorld.RootName);
            var walls=new List<Collider>();foreach(var collider in grounds.GetComponentsInChildren<Collider>())if(collider.enabled&&collider.name!="Ground")walls.Add(collider);
            Assert.That(walls.Count,Is.GreaterThan(20),"setup: the grounds' walls, booths and props collide");
            var tank=grounds.Find(FestivalGiggleTank.Name);
            Assert.That(tank,Is.Not.Null,"the tank is built on the festival grounds");
            var state=new RoundState{Phase="Playing"};world.SetTwists(state);
            Assert.That(tank.gameObject.activeInHierarchy,Is.False,"no tank this level, none shown");
            for(int i=0;i<Festivals.GiggleTankSpots.Length;i++)
            {
                var spot=Festivals.GiggleTankSpots[i];string where="spot "+i+" ("+spot.X+", "+spot.Z+")";
                state.GiggleTankSpot=i;world.SetTwists(state);
                Assert.That(tank.gameObject.activeInHierarchy,Is.True,where+": the tank shows");
                Assert.That(Vector2.Distance(new Vector2(tank.position.x,tank.position.z),new Vector2(spot.X,spot.Z)),Is.LessThan(1e-3f),where+": it stands where the rules have it");
                // Nothing solid within its reach, so it can be grabbed from any side.
                var at=new Vector3(spot.X,1,spot.Z);
                foreach(var wall in walls)Assert.That(Vector3.Distance(wall.ClosestPoint(at),at),Is.GreaterThan(Festivals.GiggleTankReach),where+" is hemmed in by "+wall.name);
            }
            state.GiggleTankFound=true;world.SetTwists(state);
            Assert.That(tank.gameObject.activeInHierarchy,Is.False,"once grabbed it is gone");
            state.GiggleTankFound=false;state.GiggleTankSpot=0;world.SetTwists(state);world.SetPhase("Shopping");
            Assert.That(tank.gameObject.activeInHierarchy,Is.False,"camp hides it with the rest of the festival");
        }

        [Test]public void LivingPlayersSeeTheTankAndSpiritsDoNot()
        {
            var game=new FestivalSimulation(5);game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");
            game.State.Phase="Playing";game.State.GiggleTankSpot=2;
            var living=Wire(game,"p0");
            Assert.That(living.GiggleTankSpot,Is.EqualTo(2),"a living player's view carries the level's tank");
            Assert.That(FestivalSimulation.GiggleTankAt(living),Is.Not.Null,"so their client shows it, and offers it within reach");
            game.Player("p1").Life="Spirit";
            Assert.That(FestivalSimulation.GiggleTankAt(Wire(game,"p1")),Is.Null,"a spirit's view carries none");
            game.State.GiggleTankFound=true;
            var after=Wire(game,"p0");
            Assert.That(after.GiggleTankFound&&FestivalSimulation.GiggleTankAt(after)==null,Is.True,"once grabbed it is gone from every view");
            Assert.That(JsonUtility.FromJson<RoundState>("{\"Phase\":\"Playing\",\"StashCash\":5}").GiggleTankSpot,Is.EqualTo(-1),"a snapshot from before tanks reads as none");
        }

        // What each client is told and plays as the level's tank is grabbed and carried home, from its view as the wire
        // brings it. "sam" grabs; "host" is a friend and "ghost" a spirit, who sees no living player.
        static FestivalSimulation Crew()
        {
            var game=new FestivalSimulation(5);game.AddPlayer("host","Host");game.AddPlayer("sam","Sam");game.AddPlayer("ghost","Ghost");
            game.State.Phase="Playing";game.State.GiggleTankSpot=2;return game;
        }
        static void Grab(FestivalSimulation game,string id)
        {
            var p=game.Player(id);var at=FestivalSimulation.GiggleTankAt(game.State);p.X=at.X;p.Z=at.Z;
            Assert.That(game.Execute(id,new GameCommand{Id="grab "+id,Kind=FestivalSimulation.GrabGiggleTankKind}).Accepted,Is.True,"setup: "+id+" grabs the tank");
        }
        // One frame of viewer's HUD: whether it plays the oh-yeah, and the notice it shows.
        static (bool OhYeah,string Notice) Follow(FestivalHudText.GiggleTankNews news,FestivalSimulation game,string viewer,int connection=0)
        {
            var view=Wire(game,viewer);bool ohYeah=news.Follow(view,view.Players.Find(p=>p.Id==viewer),connection);return (ohYeah,news.Take());
        }
        const string SamGrabbed="Sam grabbed a Giggle Tank! Get it home: +$25 for camp",Home="Giggle Tank home! +$25 for camp";

        [Test]public void TheWholeCrewHearsAGrabOnceAndFriendsAndSpiritsLearnWhoGrabbedIt()
        {
            var game=Crew();game.Player("ghost").Life="Spirit";
            var host=new FestivalHudText.GiggleTankNews();var sam=new FestivalHudText.GiggleTankNews();var ghost=new FestivalHudText.GiggleTankNews();
            Assert.That(Follow(host,game,"host"),Is.EqualTo((false,"")),"setup: the host's first view");
            Assert.That(Follow(sam,game,"sam"),Is.EqualTo((false,"")),"setup: Sam's first view");
            Assert.That(Follow(ghost,game,"ghost"),Is.EqualTo((false,"")),"setup: the spirit's first view");
            Grab(game,"sam");
            Assert.That(Follow(host,game,"host"),Is.EqualTo((true,SamGrabbed)),"a friend hears it and is told who grabbed it");
            Assert.That(Follow(ghost,game,"ghost"),Is.EqualTo((true,SamGrabbed)),"so is a spirit, by the name the grab carries");
            Assert.That(Follow(sam,game,"sam"),Is.EqualTo((true,"")),"the grabber hears it too; their own reply comes from the host");
            foreach(var (news,viewer) in new[]{(host,"host"),(sam,"sam"),(ghost,"ghost")})
                Assert.That(Follow(news,game,viewer),Is.EqualTo((false,"")),viewer+" hears and reads it once");
        }

        [Test]public void AGrabFromBeforeAViewConnectsOrItsRoundBeginsStaysSilent()
        {
            var game=Crew();Grab(game,"sam");var news=new FestivalHudText.GiggleTankNews();
            Assert.That(Follow(news,game,"host"),Is.EqualTo((false,"")),"joining after the grab");
            Assert.That(Follow(news,game,"host"),Is.EqualTo((false,"")),"and the frame after");
            game=Crew();news=new FestivalHudText.GiggleTankNews();
            Assert.That(Follow(news,game,"host"),Is.EqualTo((false,"")),"setup: before the grab");
            Grab(game,"sam");
            Assert.That(Follow(news,game,"host",connection:1),Is.EqualTo((false,"")),"reconnecting after the grab");
            Assert.That(Follow(news,game,"host",connection:1),Is.EqualTo((false,"")),"and the frame after");
            game=Crew();news=new FestivalHudText.GiggleTankNews();
            Assert.That(Follow(news,game,"host"),Is.EqualTo((false,"")),"setup: a round without a grab");
            Grab(game,"sam");game.State.RoundId="next round";
            Assert.That(Follow(news,game,"host"),Is.EqualTo((false,"")),"a new round's first view, its tank already grabbed");
            Assert.That(Follow(news,game,"host"),Is.EqualTo((false,"")),"and the frame after");
        }

        [Test]public void OnlyTheCarrierIsToldTheirTankGotHome()
        {
            var game=Crew();var host=new FestivalHudText.GiggleTankNews();var sam=new FestivalHudText.GiggleTankNews();
            Grab(game,"sam");Follow(host,game,"host");Follow(sam,game,"sam");Follow(host,game,"host");Follow(sam,game,"sam");
            int stash=game.State.StashCash;var carrier=game.Player("sam");carrier.X=Festivals.CampGateX;carrier.Z=Festivals.CampGateZ;game.Tick(.1);
            Assert.That(game.State.StashCash,Is.EqualTo(stash+Festivals.GiggleTankCash),"setup: Sam banks the tank at the camp gate");
            Assert.That(Follow(sam,game,"sam"),Is.EqualTo((false,Home)),"the carrier is told it got home, with no second oh-yeah");
            Assert.That(Follow(host,game,"host"),Is.EqualTo((false,"")),"a friend sees the stash rise, never another's receipt");
            Assert.That(Follow(sam,game,"sam"),Is.EqualTo((false,"")),"told once");
        }

        // The deep "oh yeah!" the crew hears as a tank is grabbed, made in code at 22050 Hz.
        const int Rate=22050,Frame=Rate/50;
        static float Rms(float[] samples,int from,int to){double sum=0;for(int i=from;i<to;i++)sum+=samples[i]*samples[i];return (float)System.Math.Sqrt(sum/System.Math.Max(1,to-from));}
        // The quietest 20 ms frame with a loud stretch (at least 30% of the loudest frame) on each side, and its loudness against
        // the quieter of those two: where one syllable ends and the next begins. -1 when there is no such frame.
        static int Dip(float[] samples,out float depth)
        {
            var frames=new float[samples.Length/Frame];for(int i=0;i<frames.Length;i++)frames[i]=Rms(samples,i*Frame,(i+1)*Frame);
            float loudest=0;foreach(var f in frames)loudest=Mathf.Max(loudest,f);
            int dip=-1;depth=1;
            for(int m=1;m<frames.Length-1;m++)
            {
                float before=0,after=0;for(int i=0;i<m;i++)before=Mathf.Max(before,frames[i]);for(int i=m+1;i<frames.Length;i++)after=Mathf.Max(after,frames[i]);
                if(before<.3f*loudest||after<.3f*loudest)continue;
                float ratio=frames[m]/Mathf.Min(before,after);if(ratio<depth){depth=ratio;dip=m;}
            }
            return dip;
        }
        // The loudest 2048 samples (about 93 ms) between from and to.
        static float[] Loudest(float[] samples,int from,int to)
        {
            int best=from;float loudest=-1;
            for(int i=from;i+2048<=to;i+=256){float rms=Rms(samples,i,i+2048);if(rms>loudest){loudest=rms;best=i;}}
            var window=new float[2048];System.Array.Copy(samples,best,window,0,2048);return window;
        }
        // The pitch whose period best repeats the window (autocorrelation over 50-400 Hz).
        static float Fundamental(float[] window)
        {
            int best=0;double strongest=double.MinValue;
            for(int lag=Rate/400;lag<=Rate/50;lag++){double sum=0;for(int i=0;i+lag<window.Length;i++)sum+=window[i]*window[i+lag];if(sum>strongest){strongest=sum;best=lag;}}
            return Rate/(float)best;
        }
        // How fast the wave moves for its loudness: a vowel with higher formants is brighter.
        static float Brightness(float[] window){var change=new float[window.Length-1];for(int i=0;i<change.Length;i++)change[i]=window[i+1]-window[i];return Rms(change,0,change.Length)/Rms(window,0,window.Length);}

        [Test]public void OhYeahIsOneToOneAndAHalfSecondsOfTwoSyllablesThatNeverClip()
        {
            var samples=FestivalHud.OhYeahSamples(Rate);
            Assert.That(samples.Length/(float)Rate,Is.InRange(.8f,1.4f),"0.8-1.4 s long");
            float peak=0;foreach(var sample in samples)peak=Mathf.Max(peak,Mathf.Abs(sample));
            Assert.That(peak,Is.InRange(.3f,1f),"loud, and never past 1");
            Assert.That(Dip(samples,out float depth),Is.GreaterThan(0),"two loud syllables");
            Assert.That(depth,Is.LessThan(.3f),"its loudness dips between them");
            Assert.That(FestivalHud.OhYeahSamples(Rate),Is.EqualTo(samples),"the same voice every time");
        }

        [Test]public void OhYeahIsDeepWithADarkOhThenABrighterYeah()
        {
            var samples=FestivalHud.OhYeahSamples(Rate);int dip=Dip(samples,out _);
            Assert.That(dip,Is.GreaterThan(0),"setup: two syllables");
            float[] oh=Loudest(samples,0,dip*Frame),yeah=Loudest(samples,(dip+1)*Frame,samples.Length);
            Assert.That(Fundamental(oh),Is.LessThan(130),"a deep oh");
            Assert.That(Fundamental(yeah),Is.LessThan(130),"a deep yeah");
            Assert.That(Brightness(yeah),Is.GreaterThan(Brightness(oh)*1.3f),"two vowels: the yeah's formants sit higher than the oh's");
        }

        // What viewer's client receives, through the wire format.
        private static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
        private static Bounds Bounds(Transform part){var renderers=part.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
    }
}
