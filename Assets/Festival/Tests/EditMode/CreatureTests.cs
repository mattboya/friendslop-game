using System;
using System.Collections.Generic;
using System.Text;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // VISION-2: a dosed player's own client sometimes shows little mythical creatures, more often at higher doses. Nobody else
    // sees them and they change nothing. Arrivals run on the view's level clock, seeded by the round seed, level and player, and
    // everything the dose invents carries the fake-vision tells.
    public sealed class CreatureTests
    {
        // The spec's numbers for doses 1-4: the mean gap between arrivals, and how many may be on screen at once.
        private static readonly float[] MeanGaps={45,20,10,5};
        private static readonly int[] Caps={1,2,4,6};
        private const float Step=.1f;
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        [Test]public void OnlyTheLocalPlayersOwnLivingDoseBringsThem()
        {
            Assert.That(MostAlive(Round(2),30*60),Is.GreaterThan(0),"setup: a dosed player sees creatures");
            Assert.That(MostAlive(Round(0),30*60),Is.Zero,"at dose 0 nothing ever appears");
            var friendTrips=Round(0);friendTrips.Players.Add(Player("friend",4));
            Assert.That(MostAlive(friendTrips,10*60),Is.Zero,"a friend's dose shows nothing on my screen");
            var spirit=Round(4);spirit.Players[0].Life="Spirit";
            Assert.That(MostAlive(spirit,10*60),Is.Zero,"a spirit sees none, even before their effects are cleared");
            // The level clock only runs while Playing; whatever the clock says, nothing arrives at camp or over the results.
            foreach(var phase in new[]{"Shopping","Spinning","Loading","CampReview","Results"}){var off=Round(4);off.Phase=phase;Assert.That(MostAlive(off,5*60),Is.Zero,"none arrive during "+phase);}
        }

        [Test]public void ArrivalsComeEvery45_20_10_5SecondsOnAverage()
        {
            for(int dose=1;dose<=4;dose++)
            {
                var arrivals=Arrivals(FestivalCreatures.SeedFor(Round(dose),"me"),dose,30*60);
                Assert.That(arrivals.Count,Is.GreaterThan(10),"setup: dose "+dose+" brings arrivals");
                double mean=(arrivals[arrivals.Count-1].At-arrivals[0].At)/(arrivals.Count-1);
                Assert.That(mean,Is.InRange(MeanGaps[dose-1]*.75,MeanGaps[dose-1]*1.25),"dose "+dose+": the average gap over 30 minutes is within 25% of "+MeanGaps[dose-1]+" s");
                for(int i=1;i<arrivals.Count;i++)
                    Assert.That(arrivals[i].At-arrivals[i-1].At,Is.InRange(MeanGaps[dose-1]*.5-1e-3,MeanGaps[dose-1]*1.5+1e-3),"dose "+dose+": each gap is randomised by at most 50%");
            }
        }

        [Test]public void BothKindsArriveAndAboutAThirdPersist()
        {
            var arrivals=Arrivals(FestivalCreatures.SeedFor(Round(4),"me"),4,30*60);
            int persist=arrivals.FindAll(a=>a.Persists).Count;var looks=new HashSet<int>();
            foreach(var a in arrivals){looks.Add(a.Look);if(!a.Persists)Assert.That(a.Life,Is.InRange(2f,6f),"a fleeting creature bobs about for 2 to 6 s");}
            Assert.That(persist,Is.GreaterThan(0),"some persist");Assert.That(persist,Is.LessThan(arrivals.Count),"some are fleeting");
            Assert.That((double)persist/arrivals.Count,Is.InRange(.25,.42),"roughly a third persist ("+persist+" of "+arrivals.Count+")");
            Assert.That(looks.Count,Is.EqualTo(5),"gnomes, pixies, tiny dragons, mushroom sprites and jackalopes all turn up");
        }

        [Test]public void TheSameSeedGivesTheSameSchedule()
        {
            var state=Round(3);string mine=Describe(Arrivals(FestivalCreatures.SeedFor(state,"me"),3,10*60));
            Assert.That(mine,Is.Not.Empty,"setup: ten minutes at dose 3 bring arrivals");
            Assert.That(Describe(Arrivals(FestivalCreatures.SeedFor(Round(3),"me"),3,10*60)),Is.EqualTo(mine),"the same round seed, level and player give the same schedule");
            Assert.That(Describe(Arrivals(FestivalCreatures.SeedFor(state,"friend"),3,10*60)),Is.Not.EqualTo(mine),"another player gets their own");
            var nextLevel=Round(3);nextLevel.LevelIndex=1;
            Assert.That(Describe(Arrivals(FestivalCreatures.SeedFor(nextLevel,"me"),3,10*60)),Is.Not.EqualTo(mine),"the next level gets its own");
            var otherRound=Round(3);otherRound.Seed++;
            Assert.That(Describe(Arrivals(FestivalCreatures.SeedFor(otherRound,"me"),3,10*60)),Is.Not.EqualTo(mine),"another round gets its own");
        }

        [Test]public void NoMoreThan1_2_4_6AreOnScreenAtOnce()
        {
            for(int dose=1;dose<=4;dose++)
            {
                var state=Round(dose);var creatures=Creatures();var eye=Eye();int filled=0;
                for(float t=0;t<10*60;t+=Step)
                {
                    // Five minutes standing still, then five walking a slow circle, turning back to the ones left behind.
                    if(t>=5*60){eye.transform.Rotate(0,12*Step,0,Space.World);eye.transform.position+=eye.transform.forward*.5f*Step;}
                    Frame(creatures,state,eye,t);
                    int shown=OnScreen(creatures,eye);
                    if(shown>Caps[dose-1])Assert.Fail("dose "+dose+" shows "+shown+" creatures at once at "+t.ToString("0.0")+" s, more than "+Caps[dose-1]);
                    filled=Mathf.Max(filled,creatures.OnScreen);
                }
                Assert.That(filled,Is.EqualTo(Caps[dose-1]),"setup: dose "+dose+" fills the screen to its cap");
            }
        }

        [Test]public void AFleetingCreatureFadesInAndIsGoneWithin8Seconds()
        {
            var state=FirstArrival(false);var creatures=Creatures();var eye=Eye();
            float spawned=-1,gone=-1,full=0;Transform body=null;
            for(float t=0;t<120&&gone<0;t+=Step)
            {
                Frame(creatures,state,eye,t);
                if(body==null&&creatures.Live>0){body=Drawn(creatures);spawned=t;}
                else if(body!=null&&creatures.Live==0)gone=t;
                if(body!=null&&gone<0&&t-spawned>=1.5f&&full==0)full=body.localScale.x;
                if(body!=null&&gone<0&&Mathf.Abs(t-spawned-.5f)<Step/2)
                    Assert.That(body.localScale.x,Is.InRange(.05f,.95f),"half a second in, it is still fading in");
            }
            Assert.That(spawned,Is.GreaterThanOrEqualTo(0),"setup: a fleeting creature arrives");
            Assert.That(full,Is.EqualTo(1).Within(1e-4f),"it is fully there after its 1 s fade-in (still camera, no shimmer)");
            Assert.That(gone-spawned,Is.InRange(4-Step,8+2*Step),"1 s in, 2 to 6 s about, 1 s out: gone within 8 s ("+(gone-spawned)+" s)");
        }

        [Test]public void APersistentCreatureStaysUntilYouWalkAwayOrTheDoseEnds()
        {
            var state=FirstArrival(true);var creatures=Creatures();var eye=Eye();float t=0;
            var body=UntilOneArrives(creatures,state,eye,ref t);var spot=Flat(body.position);
            for(float until=t+5*60;t<until;t+=Step)
            {
                Frame(creatures,state,eye,t);
                if(!body.gameObject.activeSelf||Vector2.Distance(Flat(body.position),spot)>1e-3f)Assert.Fail("a persistent creature left its spot "+(t)+" s in");
            }
            // Walk straight back from it, still facing it.
            var away=new Vector3(spot.x-eye.transform.position.x,0,spot.y-eye.transform.position.z).normalized;
            eye.transform.position=new Vector3(spot.x,1.65f,spot.y)-away*39;for(int i=0;i<5;i++,t+=Step)Frame(creatures,state,eye,t);
            Assert.That(body.gameObject.activeSelf&&creatures.Live==1,Is.True,"still there 39 m away");
            eye.transform.position=new Vector3(spot.x,1.65f,spot.y)-away*41;Frame(creatures,state,eye,t);
            Assert.That(creatures.Live,Is.Zero,"gone once you are 40 m away");
            Assert.That(body.gameObject.activeSelf,Is.False,"and no longer drawn");

            // The dose wearing off and dying both fade it out over 1 s.
            foreach(var (ending,end) in new (string,Action<PlayerState>)[]{("the dose ends",p=>p.Effects.Clear()),("the player dies",p=>p.Life="Spirit")})
            {
                state=FirstArrival(true);creatures=Creatures();eye=Eye();t=0;body=UntilOneArrives(creatures,state,eye,ref t);
                for(float until=t+2;t<until;t+=Step)Frame(creatures,state,eye,t);
                end(state.Players[0]);
                for(float until=t+.5f;t<until;t+=Step)Frame(creatures,state,eye,t);
                Assert.That(body.gameObject.activeSelf&&body.localScale.x<.95f&&body.localScale.x>.05f,Is.True,ending+": half a second later it is fading ("+body.localScale.x+")");
                for(float until=t+.5f+2*Step;t<until;t+=Step)Frame(creatures,state,eye,t);
                Assert.That(creatures.Live,Is.Zero,ending+": gone within the second");
            }

            // Leaving the festival clears them at once.
            state=FirstArrival(true);creatures=Creatures();eye=Eye();t=0;body=UntilOneArrives(creatures,state,eye,ref t);
            state.Phase="Shopping";Frame(creatures,state,eye,t);
            Assert.That(creatures.Live==0&&!body.gameObject.activeSelf,Is.True,"back at camp, none are left");
            state=FirstArrival(true);creatures=Creatures();eye=Eye();t=0;body=UntilOneArrives(creatures,state,eye,ref t);
            creatures.Apply(null,"me",eye,t,Step);
            Assert.That(creatures.Live==0&&!body.gameObject.activeSelf,Is.True,"a client that left shows none");
        }

        [Test]public void CreaturesAppear4To15MetresOffOnTheGroundInView()
        {
            var eye=Eye();float half=Mathf.Atan(Mathf.Tan(eye.fieldOfView*.5f*Mathf.Deg2Rad)*eye.aspect)*Mathf.Rad2Deg;
            int seen=Watch(Round(4),eye,body=>
            {
                var off=new Vector3(body.position.x-eye.transform.position.x,0,body.position.z-eye.transform.position.z);
                Assert.That(off.magnitude,Is.InRange(4-1e-3f,15+1e-3f),body.name+" is 4 to 15 m away");
                Assert.That(Mathf.Abs(Vector3.SignedAngle(Vector3.forward,off,Vector3.up)),Is.LessThanOrEqualTo(half+10),body.name+" is in view or just at its edge");
                Assert.That(body.position.y,Is.InRange(0f,.1f),body.name+" is on the ground (nothing else is there)");
            });
            Assert.That(seen,Is.GreaterThan(0),"setup: creatures appear on open ground");

            // On whatever is underfoot: a wide stage half a metre up.
            var stage=Solid("Stage",new Vector3(0,.25f,10),new Vector3(60,.5f,60));
            Assert.That(Watch(Round(4),eye,body=>Assert.That(body.position.y,Is.InRange(.5f,.6f),body.name+" stands on the stage")),Is.GreaterThan(0),"setup: creatures appear on the stage");
            Object.DestroyImmediate(stage);

            // Never inside anything: a spot under a 3 m block (the eye inside it too) is dropped.
            Solid("Block",new Vector3(0,1.5f,10),new Vector3(60,3,60));
            Assert.That(Watch(Round(4),eye,_=>{}),Is.Zero,"no creature appears inside a solid block");
        }

        [Test]public void CreaturesCarryTheFakeVisionTells()
        {
            // Turning slowly at four doses, so new ground keeps coming into view: every kind turns up.
            var state=Round(4);var creatures=Creatures();var eye=Eye();var heights=new Dictionary<string,float>();
            for(float t=0;t<10*60;t+=Step)
            {
                eye.transform.Rotate(0,10*Step,0);Frame(creatures,state,eye,t);
                foreach(Transform body in creatures.transform)
                {
                    if(!body.gameObject.activeSelf||body.localScale.x<.5f||heights.ContainsKey(body.name))continue;
                    var bounds=body.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var part in body.GetComponentsInChildren<Renderer>())bounds.Encapsulate(part.bounds);
                    heights[body.name]=bounds.size.y/body.localScale.x;
                }
            }
            Assert.That(heights.Count,Is.EqualTo(5),"setup: all five kinds were seen fully grown ("+string.Join(", ",heights.Keys)+")");
            foreach(var kind in heights)Assert.That(kind.Value,Is.InRange(.2f,.5f),kind.Key+" is 0.2 to 0.5 m tall");
            foreach(Transform body in creatures.transform)
            {
                Assert.That(body.GetComponentsInChildren<Collider>(true),Is.Empty,body.name+" carries no colliders");
                foreach(var part in body.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(part.name.Contains("__P"),Is.True,body.name+"/"+part.name+" is a part of ART-1's creature model");
                    Assert.That(part.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off),body.name+"/"+part.name+" casts no shadow");
                    var color=part.sharedMaterial.color;bool tinted=false;
                    foreach(var paint in FestivalCreatures.Palette)
                    {
                        Assert.That(Near(color,paint),Is.False,body.name+"/"+part.name+" is not its palette colour");
                        tinted|=Near(color,FestivalVisionMarkers.FakeTint(paint));
                    }
                    Assert.That(tinted,Is.True,body.name+"/"+part.name+" has the fake-vision colour shift");
                    var glow=part.sharedMaterial.GetColor("_EmissionColor");
                    Assert.That(glow.maxColorComponent,Is.GreaterThan(0f).And.LessThan(color.maxColorComponent*.5f),body.name+"/"+part.name+" glows slightly, so it reads at night");
                }
            }

            // Still camera: solid. Turning or walking: it shimmers, like a fake vision.
            foreach(var (motion,move) in new (string,Action<Transform>)[]{("turns",v=>v.Rotate(0,6,0)),("walks",v=>v.position+=new Vector3(.2f,0,0))})
            {
                var still=FirstArrival(true);var shown=Creatures();var head=Eye();float t=0;var body=UntilOneArrives(shown,still,head,ref t);
                for(float until=t+1.5f;t<until;t+=Step)Frame(shown,still,head,t);
                float peak=0;
                for(int frame=0;frame<10;frame++,t+=Step){Frame(shown,still,head,t);peak=Mathf.Max(peak,Mathf.Abs(body.localScale.x-1));}
                Assert.That(peak,Is.LessThan(1e-4f),"nothing shimmers while the camera is still");
                for(int frame=0;frame<30;frame++,t+=Step){move(head.transform);Frame(shown,still,head,t);peak=Mathf.Max(peak,Mathf.Abs(body.localScale.x-1));}
                Assert.That(peak,Is.GreaterThan(.03f),"a creature shimmers while the camera "+motion);
            }
        }

        [Test]public void DrawingAFrameMakesNoGarbage()
        {
            var state=FirstArrival(true);var creatures=Creatures();var eye=Eye();float t=0;UntilOneArrives(creatures,state,eye,ref t);
            for(int i=0;i<5;i++,t+=Step)Frame(creatures,state,eye,t);
            // Called once first: the runtime's first call through a new delegate allocates on its own.
            TestDelegate frame=()=>{state.ElapsedSeconds+=Step;creatures.Apply(state,"me",eye,t,Step);};frame();
            Assert.That(frame,Is.Not.AllocatingGCMemory(),"a frame makes no garbage");
        }

        // A level being played by "me" (and whoever else is added), at the given dose.
        private static RoundState Round(int dose){var state=new RoundState{Phase="Playing",Seed=7};state.Players.Add(Player("me",dose));return state;}
        private static PlayerState Player(string id,int dose)
        {
            var player=new PlayerState{Id=id,Name=id};
            if(dose>0)player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=dose,RemainingSeconds=600});
            return player;
        }
        // A dose 1 level whose first arrival is fleeting or persists, well inside the view's width (a bearing is a fraction of the
        // half-width), so it holds dose 1's one place on screen; the next arrival comes at least 22.5 s later.
        private static RoundState FirstArrival(bool persists)
        {
            for(int seed=1;seed<500;seed++)
            {
                var state=Round(1);state.Seed=seed;var arrivals=Arrivals(FestivalCreatures.SeedFor(state,"me"),1,200);
                if(arrivals.Count>0&&arrivals[0].Persists==persists&&Mathf.Abs(arrivals[0].Bearing)<.8f)return state;
            }
            throw new InvalidOperationException("setup: no seed's first arrival "+(persists?"persists":"is fleeting"));
        }
        // The schedule stepped the way the creatures step it: one frame every Step seconds of level clock.
        private static List<FestivalCreatures.Schedule.Arrival> Arrivals(int seed,int dose,float seconds)
        {
            var schedule=new FestivalCreatures.Schedule(seed);var arrivals=new List<FestivalCreatures.Schedule.Arrival>();double clock=0;
            while(clock<seconds){clock+=Step;while(schedule.Due(clock,dose,out var arrival))arrivals.Add(arrival);}
            return arrivals;
        }
        private static string Describe(List<FestivalCreatures.Schedule.Arrival> arrivals)
        {
            var text=new StringBuilder();
            foreach(var a in arrivals)text.Append(a.At.ToString("0.000")).Append(a.Persists?" stays ":" goes ").Append(a.Look).Append(' ').Append(a.Life.ToString("0.00")).Append(' ').Append(a.Bearing.ToString("0.00")).Append(' ').Append(a.Distance.ToString("0.00")).Append('\n');
            return text.ToString();
        }
        private static void Frame(FestivalCreatures creatures,RoundState state,Camera eye,float t){state.ElapsedSeconds+=Step;creatures.Apply(state,"me",eye,t,Step);}
        private int MostAlive(RoundState state,float seconds)
        {
            var creatures=Creatures();var eye=Eye();int most=0;
            for(float t=0;t<seconds;t+=Step){Frame(creatures,state,eye,t);most=Mathf.Max(most,creatures.Live);}
            return most;
        }
        // Every creature drawn while a still eye watches a dose 4 level for five minutes; how many there were.
        private int Watch(RoundState state,Camera eye,Action<Transform> check)
        {
            Physics.SyncTransforms();var creatures=Creatures();var seen=new HashSet<Transform>();
            for(float t=0;t<5*60;t+=Step)
            {
                Frame(creatures,state,eye,t);
                foreach(Transform body in creatures.transform)if(body.gameObject.activeSelf){seen.Add(body);check(body);}
            }
            return seen.Count;
        }
        private static Transform UntilOneArrives(FestivalCreatures creatures,RoundState state,Camera eye,ref float t)
        {
            for(;t<120;t+=Step){Frame(creatures,state,eye,t);if(creatures.Live>0){t+=Step;return Drawn(creatures);}}
            Assert.Fail("setup: no creature arrived in two minutes");return null;
        }
        private static Transform Drawn(FestivalCreatures creatures)
        {
            foreach(Transform body in creatures.transform)if(body.gameObject.activeSelf)return body;
            Assert.Fail("setup: a live creature is drawn");return null;
        }
        // Drawn creatures whose middle is inside the camera's view, counted without asking the creatures.
        private static int OnScreen(FestivalCreatures creatures,Camera eye)
        {
            int shown=0;
            foreach(Transform body in creatures.transform)
            {
                if(!body.gameObject.activeSelf)continue;
                var at=eye.WorldToViewportPoint(body.position+Vector3.up*.2f);
                if(at.z>0&&at.x>=0&&at.x<=1&&at.y>=0&&at.y<=1)shown++;
            }
            return shown;
        }
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
        private FestivalCreatures Creatures()=>Made("Creatures").AddComponent<FestivalCreatures>();
        private Camera Eye(){var eye=Made("Eye").AddComponent<Camera>();Aim(eye);return eye;}
        // The first-person camera's settings, standing at the origin and looking along +z.
        private static void Aim(Camera eye){eye.fieldOfView=75;eye.aspect=16f/9;eye.nearClipPlane=.05f;eye.farClipPlane=130;eye.transform.SetPositionAndRotation(new Vector3(0,1.65f,0),Quaternion.identity);}
        private GameObject Solid(string name,Vector3 at,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=at;go.transform.localScale=size;made.Add(go);Physics.SyncTransforms();return go;
        }
        private static Vector2 Flat(Vector3 at)=>new Vector2(at.x,at.z);
        private static bool Near(Color a,Color b)=>Mathf.Abs(a.r-b.r)<1e-3f&&Mathf.Abs(a.g-b.g)<1e-3f&&Mathf.Abs(a.b-b.b)<1e-3f;
    }
}
