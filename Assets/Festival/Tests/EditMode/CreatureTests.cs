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
    // everything the dose invents is off in colour and shimmers like a fake vision. VISION-4: they cast ordinary sun shadows.
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

        // VISION-6: each of the 5 kinds has 8 looks, palette swaps and a size; look 0 is the model as ART-1 painted it. Every arrival
        // rolls one from its own stream, every creature that shows up wears it, and the pool keeps each (kind, look) apart.
        [Test]public void EveryKindHasEightLooksAndEachArrivalRollsOne()
        {
            for(int kind=0;kind<Kinds.Length;kind++)
            {
                Assert.That(FestivalCreatures.SizeOf(kind,0),Is.EqualTo(1f),Kinds[kind]+"'s look 0 is today's size");
                for(int paint=0;paint<FestivalCreatures.Palette.Count;paint++)Assert.That(FestivalCreatures.PaintOf(kind,0,paint),Is.EqualTo(paint),Kinds[kind]+"'s look 0 keeps today's paints");
                for(int look=0;look<8;look++)
                {
                    Assert.That(new[]{.9f,1f,1.1f},Does.Contain(FestivalCreatures.SizeOf(kind,look)),Kinds[kind]+" look "+look+" is 0.9, 1 or 1.1 times today's size");
                    for(int paint=0;paint<FestivalCreatures.Palette.Count;paint++)Assert.That(FestivalCreatures.PaintOf(kind,look,paint),Is.InRange(0,FestivalCreatures.Palette.Count-1),Kinds[kind]+" look "+look+" paints from the palette");
                }
            }
            for(int look=0;look<8;look++)Assert.That(FestivalCreatures.SizeOf(0,look),Is.LessThanOrEqualTo(1f),"a gnome (about .48 m) never grows: look "+look);

            // Eight players' half hours at four doses in one level: all 40 (kind, look) pairs turn up, each look about an eighth.
            var state=Round(4);var pairs=new HashSet<(int,int)>();var counts=new int[8];int total=0;var rolled=new List<string>();
            foreach(var id in new[]{"me","friend","p3","p4","p5","p6","p7","p8"})
            {
                var text=new StringBuilder();
                foreach(var a in Arrivals(FestivalCreatures.SeedFor(state,id),4,30*60)){pairs.Add((a.Look,a.Variation));counts[a.Variation]++;total++;text.Append(a.Variation);}
                rolled.Add(text.ToString());
            }
            Assert.That(pairs.Count,Is.EqualTo(40),"every kind turns up in every one of its 8 looks");
            for(int look=0;look<8;look++)Assert.That((double)counts[look]/total,Is.InRange(.095,.155),"look "+look+"'s share of "+total+" arrivals is 12.5% +-3%");
            Assert.That(rolled[1],Is.Not.EqualTo(rolled[0]),"two players in one level roll their own looks");

            // Watched for ten minutes (leaving the ones that stay behind every 30 s): each creature that appears wears the look its
            // arrival rolled, its paints swapped and its model sized, whether newly built or taken back from the pool.
            var schedule=new FestivalCreatures.Schedule(FestivalCreatures.SeedFor(state,"me"));var creatures=Creatures();var eye=Eye();
            var wears=new Dictionary<Transform,(int Kind,int Look)>();var used=new Dictionary<int,HashSet<int>>();var tall=new Dictionary<int,float>();int spawned=0,reused=0;
            for(float t=0;t<10*60;t+=Step)
            {
                Wander(eye,t);Frame(creatures,state,eye,t);
                // The same schedule stepped alongside: a creature that appeared this frame (still at no size) came from the arrival due now.
                bool due=false;var arrival=default(FestivalCreatures.Schedule.Arrival);while(schedule.Due(state.ElapsedSeconds,4,out var next)){arrival=next;due=true;}
                foreach(Transform body in creatures.transform)
                {
                    if(!body.gameObject.activeSelf)continue;
                    if(body.localScale.x>=.5f&&wears.TryGetValue(body,out var worn)){var bounds=Extent(body);tall[worn.Kind]=Mathf.Max(tall.TryGetValue(worn.Kind,out var most)?most:0,bounds.size.y/body.localScale.x/FestivalCreatures.SizeOf(worn.Kind,worn.Look));}
                    if(body.localScale.x!=0)continue;
                    Assert.That(due,Is.True,"setup: "+body.name+" appeared when an arrival was due ("+t+" s)");
                    var pair=(arrival.Look,arrival.Variation);spawned++;
                    Assert.That(body.name,Is.EqualTo("Creature "+Kinds[arrival.Look]),"it is the kind its arrival drew");
                    if(wears.TryGetValue(body,out var was)){reused++;Assert.That(was,Is.EqualTo(pair),body.name+" is taken back from the pool only for its own look");}
                    wears[body]=pair;
                    Assert.That(body.GetChild(0).localScale.x,Is.EqualTo(FestivalCreatures.SizeOf(pair.Item1,pair.Item2)).Within(1e-4f),body.name+" look "+pair.Item2+" is its look's size");
                    foreach(var part in body.GetComponentsInChildren<Renderer>(true))
                    {
                        int paint=int.Parse(part.name.Substring(part.name.LastIndexOf("__P",StringComparison.Ordinal)+3).Split('.')[0]);
                        if(!used.TryGetValue(pair.Item1,out var paints))used[pair.Item1]=paints=new HashSet<int>();paints.Add(paint);
                        Assert.That(Near(part.sharedMaterial.color,FestivalVisionMarkers.FakeTint(FestivalCreatures.Palette[FestivalCreatures.PaintOf(pair.Item1,pair.Item2,paint)])),Is.True,body.name+"/"+part.name+" wears look "+pair.Item2+"'s paint");
                    }
                }
            }
            Assert.That(spawned,Is.GreaterThan(60),"setup: ten minutes bring creatures");
            Assert.That(reused,Is.GreaterThan(10),"setup: some are taken back from the pool");
            // Pairwise distinct on what each model is painted with, or in size; and every look .2 to .5 m tall.
            Assert.That(used.Count,Is.EqualTo(Kinds.Length),"setup: every kind was seen");
            for(int kind=0;kind<Kinds.Length;kind++)
                for(int look=0;look<8;look++)
                {
                    float height=tall[kind]*FestivalCreatures.SizeOf(kind,look);
                    Assert.That(height,Is.InRange(.2f,.5f),Kinds[kind]+" look "+look+" is 0.2 to 0.5 m tall ("+height+")");
                    for(int other=0;other<look;other++)
                    {
                        bool differs=FestivalCreatures.SizeOf(kind,look)!=FestivalCreatures.SizeOf(kind,other);
                        foreach(int paint in used[kind])differs|=FestivalCreatures.PaintOf(kind,look,paint)!=FestivalCreatures.PaintOf(kind,other,paint);
                        Assert.That(differs,Is.True,Kinds[kind]+" looks "+other+" and "+look+" differ in paint or size");
                    }
                }
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
                    Assert.That(part.shadowCastingMode,Is.EqualTo(ShadowCastingMode.On),body.name+"/"+part.name+" casts an ordinary shadow, like any prop (VISION-4)");
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
            // VISION-6: nor does a pointer's, looking its target up, beckoning and twinkling its sparkles.
            var hinted=Hinted(FirstArrival(true),"Clue",new Vector2(6,30));hinted.GiggleTankSpot=0;var pointing=Creatures();t=0;UntilOneArrives(pointing,hinted,eye,ref t);
            Assert.That(Pointing(pointing).Count,Is.EqualTo(1),"setup: a creature points");
            for(int i=0;i<5;i++,t+=Step)Frame(pointing,hinted,eye,t);
            TestDelegate beckon=()=>{hinted.ElapsedSeconds+=Step;pointing.Apply(hinted,"me",eye,t,Step);};beckon();
            Assert.That(beckon,Is.Not.AllocatingGCMemory(),"a pointer's frame makes no garbage");
        }

        // VISION-6: now and then one of the tripper's creatures points. Its kind says what at: a mushroom sprite a true clue, a
        // pixie a true buyer, a dragon a true narc, a gnome a stash or a double buyer. It faces its target, not you, and leads you
        // there when you come near.
        [Test]public void AHintedVisionGetsItsKindPointingAtIt()
        {
            var goal=new Vector2(6,30);var state=Hinted(FirstArrival(true),"Clue",goal);var creatures=Creatures();var eye=Eye();float t=0;
            var body=UntilOneArrives(creatures,state,eye,ref t);
            Assert.That(body.name,Is.EqualTo("Creature Mushroom sprite"),"a hinted clue brings a mushroom sprite at the next arrival");
            Assert.That(Pointing(creatures),Is.EqualTo(new[]{body}),"and it points");
            // VISION-6: its job sets only the kind; it wears the look its arrival rolled.
            int rolled=Arrivals(FestivalCreatures.SeedFor(state,"me"),1,200)[0].Variation;
            Assert.That(rolled,Is.Not.Zero,"setup: the first arrival rolls a look other than the model as painted");
            AssertWears(body,rolled,"the clue's pointer");
            for(float until=t+1.5f;t<until;t+=Step)Frame(creatures,state,eye,t);
            Assert.That(Facing(body,goal),Is.LessThan(15),"it faces the festivalgoer");
            Assert.That(Facing(body,Flat(eye.transform.position)),Is.GreaterThan(90),"not the camera");

            // Come within 5 m and it scurries about 6 m on toward them; follow it and it leads there, stopping about 3 m short.
            var before=Flat(body.position);StandBehind(eye,body,goal,3);
            for(float until=t+1.5f;t<until;t+=Step)Frame(creatures,state,eye,t);
            float ran=Vector2.Distance(before,goal)-Vector2.Distance(Flat(body.position),goal);
            Assert.That(ran,Is.InRange(5f,6.5f),"it scurries about 6 m toward them once the eye is within 5 m ("+ran+" m)");
            for(int follow=0;follow<6;follow++){StandBehind(eye,body,goal,3);for(float until=t+1.5f;t<until;t+=Step)Frame(creatures,state,eye,t);}
            Assert.That(Vector2.Distance(Flat(body.position),goal),Is.InRange(2.8f,3.7f),"following it leads to about 3 m short of them");
            Assert.That(Facing(body,goal),Is.LessThan(15),"still facing them");

            // Checked, the vision needs no pointer: it goes with the usual fade.
            state.Visions[0].Confirmed=true;
            for(float until=t+.5f;t<until;t+=Step)Frame(creatures,state,eye,t);
            Assert.That(body.gameObject.activeSelf&&body.localScale.x<.95f,Is.True,"once the vision is confirmed it fades");
            for(float until=t+.5f+2*Step;t<until;t+=Step)Frame(creatures,state,eye,t);
            Assert.That(creatures.Live,Is.Zero,"and is gone within the second");

            foreach(var (kind,looks) in new[]{("Narc","Creature Dragon"),("Stash","Creature Gnome"),("Buyer","Creature Pixie"),("DoubleBuyer","Creature Gnome")})
            {
                state=Hinted(FirstArrival(true),kind,goal);creatures=Creatures();eye=Eye();t=0;
                body=UntilOneArrives(creatures,state,eye,ref t);for(float until=t+1.5f;t<until;t+=Step)Frame(creatures,state,eye,t);
                Assert.That(body.name,Is.EqualTo(looks),"a hinted "+kind+" brings a "+looks);
                Assert.That(Pointing(creatures),Is.EqualTo(new[]{body}),"the "+kind+"'s creature points");
                AssertWears(body,Arrivals(FestivalCreatures.SeedFor(state,"me"),1,200)[0].Variation,"the "+kind+"'s pointer");
                Assert.That(Facing(body,goal),Is.LessThan(15),"it faces the "+kind+(kind=="Stash"?"'s spot":"'s festivalgoer"));
            }
        }

        // Its target gone (checked, or the tank grabbed), a pointer fades out still facing where its target stood, its sparkles too.
        [Test]public void AFadingPointerKeepsFacingItsTarget()
        {
            var goal=new Vector2(6,30);var state=Hinted(FirstArrival(true),"Clue",goal);var creatures=Creatures();var eye=Eye();float t=0;
            var body=UntilOneArrives(creatures,state,eye,ref t);for(float until=t+1.5f;t<until;t+=Step)Frame(creatures,state,eye,t);
            state.Visions[0].Confirmed=true;
            Assert.That(WorstFacingWhileFading(creatures,state,eye,body,goal,ref t),Is.LessThan(15),"once the vision is confirmed it fades still facing the festivalgoer");

            var tank=Festivals.GiggleTankSpots[0];var spot=new Vector2(tank.X,tank.Z);
            var grabbed=Round(4);grabbed.GiggleTankSpot=0;var shown=Creatures();var head=Eye();t=0;
            for(;t<3*60&&Pointing(shown).Count==0;t+=Step){Wander(head,t);Frame(shown,grabbed,head,t);}
            Assert.That(Pointing(shown).Count,Is.EqualTo(1),"setup: a jackalope points at the tank");
            var jackalope=Pointing(shown)[0];for(float until=t+1.5f;t<until;t+=Step)Frame(shown,grabbed,head,t);
            grabbed.GiggleTankFound=true;
            Assert.That(WorstFacingWhileFading(shown,grabbed,head,jackalope,spot,ref t),Is.LessThan(15),"once the tank is grabbed it fades still facing where the tank stood");
        }

        // VISION-6: a pointer stays put however fleeting the arrival it took over, so there is time to follow it.
        [Test]public void APointerStaysEvenWhenItsArrivalWouldHaveBeenFleeting()
        {
            var state=Hinted(FirstArrival(false),"Clue",new Vector2(6,30));var creatures=Creatures();var eye=Eye();float t=0;
            var body=UntilOneArrives(creatures,state,eye,ref t);
            Assert.That(Pointing(creatures),Is.EqualTo(new[]{body}),"setup: the fleeting first arrival points");
            for(float until=t+10;t<until;t+=Step)Frame(creatures,state,eye,t);
            Assert.That(Pointing(creatures),Is.EqualTo(new[]{body}),"10 s on, past a fleeting creature's 8 s at most, it is still there pointing");
            Assert.That(body.localScale.x,Is.EqualTo(1).Within(1e-4f),"and not fading");
        }

        // VISION-6: each level's jackalope is timed from that level's start, however late the last level's came.
        [Test]public void TheTankTimerStartsAgainEachLevel()
        {
            var state=Round(4);state.GiggleTankSpot=0;var creatures=Creatures();var eye=Eye();int came=0;var was=new List<Transform>();
            for(float t=0;t<5*60;t+=Step){Wander(eye,t);Frame(creatures,state,eye,t);var now=Pointing(creatures);foreach(var body in now)if(!was.Contains(body))came++;was=now;}
            Assert.That(came,Is.GreaterThanOrEqualTo(2),"setup: a five-minute level brings jackalopes ("+came+")");
            state.LevelIndex=1;state.ElapsedSeconds=0;double first=-1;
            for(float t=0;t<200&&first<0;t+=Step){Wander(eye,t);Frame(creatures,state,eye,t);if(Pointing(creatures).Count>0)first=state.ElapsedSeconds;}
            Assert.That(first,Is.InRange(60-Step,140),"the next level's first jackalope comes 60 s or so into it ("+first+")");
        }

        [Test]public void APointerLeavesOnEachRuleAndPointsOnlyOncePerLevel()
        {
            var goal=new Vector2(6,30);
            foreach(var (rule,end) in new (string,Action<RoundState>)[]{
                ("the vision leaves the view",s=>s.Visions.Clear()),
                ("its festivalgoer leaves the view",s=>s.Npcs.Clear()),
                ("the player is within 4 m of them",s=>{s.Players[0].X=goal.x+2;s.Players[0].Z=goal.y-3;}),
                ("the dose ends",s=>s.Players[0].Effects.Clear())})
            {
                var state=Hinted(FirstArrival(true),"Buyer",goal);var creatures=Creatures();var eye=Eye();float t=0;
                var body=UntilOneArrives(creatures,state,eye,ref t);for(float until=t+2;t<until;t+=Step)Frame(creatures,state,eye,t);
                Assert.That(Pointing(creatures),Is.EqualTo(new[]{body}),"setup: a pixie points before "+rule);
                end(state);
                for(float until=t+1+2*Step;t<until;t+=Step)Frame(creatures,state,eye,t);
                Assert.That(creatures.Live,Is.Zero,"it goes within a second once "+rule);
            }

            // Walked 40 m away from, it goes like any creature that stays; the same hint is never pointed at again this level.
            {
                var state=Hinted(FirstArrival(true),"Buyer",goal);var creatures=Creatures();var eye=Eye();float t=0;
                var body=UntilOneArrives(creatures,state,eye,ref t);
                eye.transform.position=new Vector3(body.position.x,1.65f,body.position.z-41);Frame(creatures,state,eye,t);t+=Step;
                Assert.That(creatures.Live,Is.Zero,"40 m away it is gone");
                Aim(eye);int pointers=0,came=0;
                for(float until=t+5*60;t<until;t+=Step){Frame(creatures,state,eye,t);pointers=Mathf.Max(pointers,Pointing(creatures).Count);came=Mathf.Max(came,creatures.Live);}
                Assert.That(came,Is.GreaterThan(0),"setup: other creatures still come");
                Assert.That(pointers,Is.Zero,"a hint is pointed at once per level");
                // A new level deals new visions whose ids start again, so the set of hints already pointed at starts again too.
                state.LevelIndex=1;
                for(float until=t+3*60;t<until&&pointers==0;t+=Step){Frame(creatures,state,eye,t);pointers=Pointing(creatures).Count;}
                Assert.That(pointers,Is.EqualTo(1),"the next level's hint gets its pointer");
            }

            // An arrival that cannot be placed is dropped, and the hint waits for the next one.
            {
                var state=Hinted(FirstArrival(true),"Narc",goal);var creatures=Creatures();var eye=Eye();
                var block=Solid("Block",new Vector3(0,1.5f,10),new Vector3(60,3,60));float t=0;
                for(;t<3*60;t+=Step)Frame(creatures,state,eye,t);
                Assert.That(creatures.Live,Is.Zero,"setup: nothing can stand inside the block");
                Object.DestroyImmediate(block);Physics.SyncTransforms();
                var body=UntilOneArrives(creatures,state,eye,ref t);
                Assert.That(body.name,Is.EqualTo("Creature Dragon"),"the next arrival points at the waiting hint");
                Assert.That(Pointing(creatures),Is.EqualTo(new[]{body}));
            }
        }

        // Decision A: a pointer beckons, so it stands out from the rest. It leans and nods toward its target with little hops,
        // sparkles twinkle on the ground toward it, and it squeaks once as it appears.
        [Test]public void APointerBeckonsWithANodSparklesAndASqueak()
        {
            var goal=new Vector2(6,30);var state=Hinted(FirstArrival(true),"Clue",goal);var creatures=Creatures();var eye=Eye();float t=0;
            var body=UntilOneArrives(creatures,state,eye,ref t);
            var voice=body.GetComponent<AudioSource>();
            Assert.That(voice,Is.Not.Null,"it squeaks from where it stands");
            Assert.That(voice.spatialBlend,Is.EqualTo(1f),"a 3-D sound, so it says which way to look");
            Assert.That(voice.minDistance,Is.GreaterThanOrEqualTo(15f),"heard in full anywhere a pointer appears (4 to 15 m off), not faded by distance");
            Assert.That(voice.dopplerLevel,Is.Zero,"and not bent in pitch as you turn or walk");
            var squeak=FestivalCreatures.Squeak;
            Assert.That(squeak.length,Is.InRange(.1f,.2f),"a short squeak ("+squeak.length+" s)");
            var samples=new float[squeak.samples];squeak.GetData(samples,0);int third=samples.Length/3;
            Assert.That(Crossings(samples,2*third,samples.Length),Is.GreaterThan(Crossings(samples,0,third)*1.3f),"that rises in pitch");

            float least=180,most=0,lowest=1,highest=0;
            for(float until=t+3;t<until;t+=Step)
            {
                Frame(creatures,state,eye,t);if(t<until-1.5f)continue;
                float pitch=Vector3.Angle(body.up,Vector3.up);least=Mathf.Min(least,pitch);most=Mathf.Max(most,pitch);
                lowest=Mathf.Min(lowest,body.position.y);highest=Mathf.Max(highest,body.position.y);
                var leaning=new Vector2(body.up.x,body.up.z);var ahead=goal-Flat(body.position);
                if(pitch>3)Assert.That(Vector2.Angle(leaning,ahead),Is.LessThan(20),"it leans toward its target, not away");
            }
            Assert.That(least,Is.LessThan(3),"it straightens up between nods");Assert.That(most,Is.InRange(8f,30f),"and nods toward its target");
            Assert.That(highest-lowest,Is.GreaterThan(.05f),"with little hops");

            var sparkles=new List<Renderer>();
            foreach(var part in body.GetComponentsInChildren<Renderer>())
                if(part.name=="Sparkle")sparkles.Add(part);
                else Assert.That(part.shadowCastingMode,Is.EqualTo(ShadowCastingMode.On),part.name+": a pointer casts an ordinary shadow too (VISION-4)");
            Assert.That(sparkles.Count,Is.InRange(4,6),"4 to 6 sparkles");
            foreach(var sparkle in sparkles)
            {
                var at=sparkle.transform.position;var off=Flat(at)-Flat(body.position);
                Assert.That(off.magnitude,Is.InRange(.2f,4f),"each sparkle is on the way, close by");
                Assert.That(Vector2.Angle(off,goal-Flat(body.position)),Is.LessThan(10),"toward the target");
                Assert.That(at.y,Is.InRange(-.01f,.1f),"on the ground");
                var size=sparkle.transform.lossyScale;Assert.That(size.y,Is.LessThan(size.x*.4f),"flattened");
                Assert.That(sparkle.GetComponent<Collider>(),Is.Null,"no collider");
                Assert.That(sparkle.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off),"no shadow");
                Assert.That(sparkle.sharedMaterial.GetColor("_EmissionColor").maxColorComponent,Is.GreaterThan(.5f),"glowing");
            }
            var sizes=new List<float>();for(int frame=0;frame<10;frame++,t+=Step){Frame(creatures,state,eye,t);sizes.Add(sparkles[0].transform.lossyScale.x);}
            sizes.Sort();Assert.That(sizes[sizes.Count-1]-sizes[0],Is.GreaterThan(.01f),"they twinkle");

            // An ordinary creature does none of it.
            var plain=FirstArrival(true);var others=Creatures();var head=Eye();float u=0;var still=UntilOneArrives(others,plain,head,ref u);
            for(float until=u+3;u<until;u+=Step){Frame(others,plain,head,u);Assert.That(Vector3.Angle(still.up,Vector3.up),Is.LessThan(1),"an ordinary creature stands straight");}
            Assert.That(Pointing(others),Is.Empty,"and has no sparkles");
        }

        // The Giggle Tank is public, so each dosed player's client times its own jackalope: no sooner than 60 s into the level, then
        // at most once every 120 s, and none once the tank is grabbed.
        [Test]public void TheJackalopePointsAtAnUngrabbedTank()
        {
            var tank=Festivals.GiggleTankSpots[0];var goal=new Vector2(tank.X,tank.Z);
            var state=Round(4);state.GiggleTankSpot=0;var creatures=Creatures();var eye=Eye();var came=new List<float>();var was=new List<Transform>();
            for(float t=0;t<10*60;t+=Step)
            {
                Wander(eye,t);Frame(creatures,state,eye,t);var now=Pointing(creatures);
                foreach(var body in now)
                {
                    if(!was.Contains(body)){came.Add(t);Assert.That(body.name,Is.EqualTo("Creature Jackalope"),"the tank's pointer is a jackalope");}
                    Assert.That(Facing(body,goal),Is.LessThan(15),"it faces the tank");
                }
                was=now;
            }
            Assert.That(came.Count,Is.InRange(3,5),"ten minutes at four doses bring a jackalope pointer every two minutes or so ("+came.Count+")");
            Assert.That(came[0],Is.GreaterThanOrEqualTo(60f),"the first no sooner than 60 s in");
            for(int i=1;i<came.Count;i++)Assert.That(came[i]-came[i-1],Is.GreaterThanOrEqualTo(120f-Step),"at most one every 120 s");

            // Grabbed: the pointer goes and none comes again.
            {
                var grabbed=Round(4);grabbed.GiggleTankSpot=0;var shown=Creatures();var head=Eye();float t=0;
                for(;t<3*60&&Pointing(shown).Count==0;t+=Step){Wander(head,t);Frame(shown,grabbed,head,t);}
                Assert.That(Pointing(shown).Count,Is.EqualTo(1),"setup: a jackalope points at the tank");
                grabbed.GiggleTankFound=true;int most=0;
                for(float until=t+1+2*Step;t<until;t+=Step)Frame(shown,grabbed,head,t);
                Assert.That(Pointing(shown),Is.Empty,"it goes once the tank is grabbed");
                for(float until=t+5*60;t<until;t+=Step){Wander(head,t);Frame(shown,grabbed,head,t);most=Mathf.Max(most,Pointing(shown).Count);}
                Assert.That(most,Is.Zero,"and none comes again");
            }

            // A hint waiting when the tank's pointer is due goes first.
            {
                var both=Hinted(Round(4),"Buyer",new Vector2(-6,30));both.GiggleTankSpot=0;var shown=Creatures();var head=Eye();
                var block=Solid("Block",new Vector3(0,1.5f,10),new Vector3(60,3,60));float t=0;
                for(;t<90;t+=Step)Frame(shown,both,head,t);
                Object.DestroyImmediate(block);Physics.SyncTransforms();
                var order=new List<string>();var seen=new List<Transform>();
                for(float until=t+60;t<until&&order.Count<2;t+=Step)
                {
                    Frame(shown,both,head,t);
                    foreach(var body in Pointing(shown))if(!seen.Contains(body)){seen.Add(body);order.Add(body.name);}
                }
                Assert.That(order,Is.EqualTo(new[]{"Creature Pixie","Creature Jackalope"}),"the waiting hint is pointed at before the due tank");
            }
        }

        [Test]public void NoPointersWithoutAHint()
        {
            // A dosed festivalgoer who is not the tripper (night 2 doses the whole crew): no visions reach them, and night 2 has no tank.
            var state=Round(4);int most=0,seen=0;var creatures=Creatures();var eye=Eye();
            for(float t=0;t<10*60;t+=Step){eye.transform.Rotate(0,10*Step,0);Frame(creatures,state,eye,t);most=Mathf.Max(most,Pointing(creatures).Count);seen=Mathf.Max(seen,creatures.Live);}
            Assert.That(seen,Is.GreaterThan(0),"setup: creatures come");
            Assert.That(most,Is.Zero,"no creature points without a hint or a tank");
            // The tripper's visions with none hinted bring none either.
            state=Hinted(Round(4),"Buyer",new Vector2(6,30));state.Visions[0].Hinted=false;creatures=Creatures();eye=Eye();
            for(float t=0;t<10*60;t+=Step){eye.transform.Rotate(0,10*Step,0);Frame(creatures,state,eye,t);most=Mathf.Max(most,Pointing(creatures).Count);}
            Assert.That(most,Is.Zero,"visions no creature hints at get no pointer");
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
            foreach(var a in arrivals)text.Append(a.At.ToString("0.000")).Append(a.Persists?" stays ":" goes ").Append(a.Look).Append('/').Append(a.Variation).Append(' ').Append(a.Life.ToString("0.00")).Append(' ').Append(a.Bearing.ToString("0.00")).Append(' ').Append(a.Distance.ToString("0.00")).Append('\n');
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
            for(float until=t+120;t<until;t+=Step){Frame(creatures,state,eye,t);if(creatures.Live>0){t+=Step;return Drawn(creatures);}}
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
        // The tripper's view of state with one hinted, true and unchecked vision of kind: over a festivalgoer standing at goal, or
        // (a Stash) the spot itself.
        private static RoundState Hinted(RoundState state,string kind,Vector2 goal)
        {
            bool spot=kind=="Stash";if(!spot)state.Npcs.Add(new NpcState{Id="npc-1",X=goal.x,Z=goal.y});
            state.Visions.Add(new VisionState{Id="vision-1",Kind=kind,NpcId=spot?"":"npc-1",X=spot?goal.x:0,Z=spot?goal.y:0,Hinted=true});
            return state;
        }
        // The live creatures pointing at something: the ones beckoning with their sparkles.
        private static List<Transform> Pointing(FestivalCreatures creatures)
        {
            var pointing=new List<Transform>();
            foreach(Transform body in creatures.transform){var trail=body.Find("Sparkles");if(body.gameObject.activeSelf&&trail!=null&&trail.gameObject.activeSelf)pointing.Add(body);}
            return pointing;
        }
        // From the next frame until body has all but faded away, the most degrees it turned from goal.
        private static float WorstFacingWhileFading(FestivalCreatures creatures,RoundState state,Camera eye,Transform body,Vector2 goal,ref float t)
        {
            float worst=0;int frames=0;
            for(float until=t+.9f;t<until;t+=Step)
            {
                Frame(creatures,state,eye,t);
                if(body.gameObject.activeSelf&&body.localScale.x>.05f){worst=Mathf.Max(worst,Facing(body,goal));frames++;}
            }
            Assert.That(frames,Is.GreaterThan(3),"setup: it was seen fading");
            return worst;
        }
        // Degrees between where body faces, seen from above, and the way to target.
        private static float Facing(Transform body,Vector2 target)=>Vector2.Angle(new Vector2(body.forward.x,body.forward.z),target-Flat(body.position));
        // The eye stands metres behind body, on the far side from goal, still looking along +z.
        private static void StandBehind(Camera eye,Transform body,Vector2 goal,float metres)
        {
            var at=Flat(body.position);var back=(at-goal).normalized*metres;eye.transform.position=new Vector3(at.x+back.x,1.65f,at.y+back.y);
        }
        // Every 30 s the eye jumps 50 m along x and back, so the creatures that stay are left 40 m behind and new ground has room.
        private static void Wander(Camera eye,float t)=>eye.transform.position=new Vector3((int)(t/30)%2*50,1.65f,0);
        private static int Crossings(float[] samples,int from,int to){int count=0;for(int i=from+1;i<to;i++)if(samples[i-1]<0!=samples[i]<0)count++;return count;}
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
        // FestivalCreatures' kinds, in its Looks' order.
        private static readonly string[] Kinds={"Gnome","Pixie","Dragon","Mushroom sprite","Jackalope"};
        private static Bounds Extent(Transform body){var bounds=body.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var part in body.GetComponentsInChildren<Renderer>())bounds.Encapsulate(part.bounds);return bounds;}
        // A creature wears its kind's look: that look's size on its model and that look's paint on every part.
        private static void AssertWears(Transform body,int look,string what)
        {
            int kind=Array.IndexOf(Kinds,body.name.Substring("Creature ".Length));
            Assert.That(body.GetChild(0).localScale.x,Is.EqualTo(FestivalCreatures.SizeOf(kind,look)).Within(1e-4f),what+" is look "+look+"'s size");
            foreach(var part in body.GetComponentsInChildren<Renderer>(true))
            {
                if(!part.name.Contains("__P"))continue;
                int paint=int.Parse(part.name.Substring(part.name.LastIndexOf("__P",StringComparison.Ordinal)+3).Split('.')[0]);
                Assert.That(Near(part.sharedMaterial.color,FestivalVisionMarkers.FakeTint(FestivalCreatures.Palette[FestivalCreatures.PaintOf(kind,look,paint)])),Is.True,what+"'s "+part.name+" wears look "+look+"'s paint");
            }
        }
        private static bool Near(Color a,Color b)=>Mathf.Abs(a.r-b.r)<1e-3f&&Mathf.Abs(a.g-b.g)<1e-3f&&Mathf.Abs(a.b-b.b)<1e-3f;
    }
}
