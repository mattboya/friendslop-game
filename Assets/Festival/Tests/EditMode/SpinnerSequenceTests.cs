using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // SPIN-1: the spin and the take are computed only from the public spin result, so every client shows the same spin. SPIN-3: the
    // spin is a slot machine whose three reels start together and stop left to right on who trips, how many doses and what they took.
    public sealed class SpinnerSequenceTests
    {
        static RoundState Spin(int seed,int crew,int tripper,int dose,string substance="lsd")
        {
            var state=new RoundState{Phase="Spinning",RoundId="round_"+seed,SpinSeed=seed,SpinEndsAt=100+FestivalSimulation.SpinSeconds,TripperId="p"+tripper};
            for(int i=0;i<crew;i++)state.Players.Add(new PlayerState{Id="p"+i,Name="Friend "+i});
            state.Doses.Add(new PlayerDose{PlayerId=state.TripperId,Dose=dose,Substance=substance});
            return state;
        }
        static double Start(RoundState state)=>state.SpinEndsAt-FestivalSimulation.SpinSeconds;
        // The reels left to right, as a beat shows them, with what each one's strip holds: a crew index, a dose, a substance index.
        static readonly (string Name,Func<FestivalSpinner.Beat,float> Scroll,Func<FestivalSpinner.Beat,bool> Landed)[] Reels=
            {("people",b=>b.PeopleScroll,b=>b.PeopleLanded),("dose",b=>b.DoseScroll,b=>b.DoseLanded),("substance",b=>b.SubstanceScroll,b=>b.SubstanceLanded)};
        static int OnPayline(IReadOnlyList<int> strip,float scroll)=>strip[FestivalSpinner.TileAt(strip.Count,scroll)];
        static int SubstanceOnPayline(float scroll)=>FestivalSpinner.TileAt(FestivalSimulation.Substances.Count,scroll);
        // The first millisecond of the spin at which reached holds.
        static double First(Func<double,bool> reached){for(int ms=0;ms<=FestivalSimulation.SpinSeconds*1000;ms++)if(reached(ms/1000.0))return ms/1000.0;return double.NaN;}

        [Test] public void EachReelLandsOnTheStatesTripperDoseAndSubstance()
        {
            for(int seed=1;seed<=20;seed++)
                for(int size=1;size<=8;size++)
                {
                    int tripper=seed*7%size,dose=1+(seed+size)%4;string substance=FestivalSimulation.Substances[(seed+size)%FestivalSimulation.Substances.Count];
                    var state=Spin(seed*977+size,size,tripper,dose,substance);var crew=FestivalSpinner.Crew(state);
                    var landed=FestivalSpinner.At(state,crew,Start(state)+FestivalSpinner.TakeStarts);string spin="seed "+seed+", crew of "+size;
                    Assert.That(crew[OnPayline(FestivalSpinner.PeopleStrip(size),landed.PeopleScroll)],Is.EqualTo(state.TripperId),spin+": the people reel stops on the tripper");
                    Assert.That(OnPayline(FestivalSpinner.DoseStrip,landed.DoseScroll),Is.EqualTo(dose),spin+": the dose reel stops on "+dose);
                    Assert.That(FestivalSimulation.Substances[SubstanceOnPayline(landed.SubstanceScroll)],Is.EqualTo(substance),spin+": the substance reel stops on "+substance);
                    Assert.That(landed.Substance,Is.EqualTo(substance),spin+": the beat names what the tripper took");
                    foreach(var reel in Reels)
                    {
                        Assert.That(reel.Scroll(landed),Is.EqualTo(Mathf.Round(reel.Scroll(landed))),spin+": the "+reel.Name+" reel's result rests centred on the payline");
                        Assert.That(reel.Scroll(landed)-reel.Scroll(FestivalSpinner.At(state,crew,Start(state))),Is.GreaterThan(20),spin+": after a fast scroll through dozens of tiles");
                    }
                }
        }

        [Test] public void EveryViewerOfARealSpinSeesTheSameReels()
        {
            var seen=new HashSet<int>();
            for(int seed=1;seed<=12;seed++)
            {
                var game=new FestivalSimulation(seed);game.State.LevelIndex=seed%2==0?3:0;
                foreach(var id in new[]{"host","friend","third","fourth"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
                game.Tick(5.2);
                Assert.That(game.State.Phase,Is.EqualTo("Spinning"),"setup: seed "+seed+" is spinning");
                var spun=game.State.Doses.Find(d=>d.PlayerId==game.State.TripperId);int dose=spun.Dose;string substance=spun.Substance;seen.Add(dose);
                float[] first=null;
                foreach(var viewer in game.State.Players)
                {
                    var view=FestivalSession.ViewFor(game,viewer.Id);var crew=FestivalSpinner.Crew(view);
                    var beat=FestivalSpinner.At(view,crew,Start(view)+FestivalSpinner.TakeStarts);var midway=FestivalSpinner.At(view,crew,Start(view)+1.7);
                    Assert.That(crew[OnPayline(FestivalSpinner.PeopleStrip(crew.Count),beat.PeopleScroll)],Is.EqualTo(game.State.TripperId),viewer.Id+" sees the reel pick the tripper");
                    Assert.That(OnPayline(FestivalSpinner.DoseStrip,beat.DoseScroll),Is.EqualTo(dose),viewer.Id+" sees the tripper's dose");
                    Assert.That(FestivalSimulation.Substances[SubstanceOnPayline(beat.SubstanceScroll)],Is.EqualTo(substance),viewer.Id+" sees what the tripper took");
                    var scrolls=new[]{beat.PeopleScroll,beat.DoseScroll,beat.SubstanceScroll,midway.PeopleScroll,midway.DoseScroll,midway.SubstanceScroll};
                    if(first==null)first=scrolls;
                    else Assert.That(scrolls,Is.EqualTo(first),viewer.Id+" sees exactly the host's reels, on the way and where they land");
                }
            }
            Assert.That(seen.Count,Is.GreaterThanOrEqualTo(2),"setup: the real spins covered more than one dose");
        }

        // Decision 1 (A): all three reels start together as the lever drops, then stop left to right, who then how many then what:
        // the first 2.5 s in, the next 1.5 s later, the last held a beat longer. The results rest 1 s; the take and the reaction follow.
        [Test] public void TheReelsStartTogetherAndStopLeftToRight()
        {
            var state=Spin(4242,4,2,3);var crew=FestivalSpinner.Crew(state);double start=Start(state);
            FestivalSpinner.Beat At(double seconds)=>FestivalSpinner.At(state,crew,start+seconds);
            var landed=At(FestivalSimulation.SpinSeconds-.01);var resting=At(0);var stops=new double[Reels.Length];
            for(int i=0;i<Reels.Length;i++)
            {
                var reel=Reels[i];
                double moves=First(t=>reel.Scroll(At(t))!=reel.Scroll(resting));stops[i]=First(t=>reel.Landed(At(t)));
                Assert.That(moves,Is.LessThan(.02),"the "+reel.Name+" reel starts as the lever drops");
                Assert.That(reel.Scroll(At(stops[i]-.05)),Is.Not.EqualTo(reel.Scroll(landed)),"the "+reel.Name+" reel is still moving just before it stops");
                Assert.That(reel.Scroll(At(stops[i])),Is.EqualTo(reel.Scroll(landed)),"and stops where it lands");
                Assert.That(At(stops[i]-.01).Stage,Is.EqualTo(FestivalSpinner.Stage.Reels),"while the reels are up");
            }
            Assert.That(stops[0],Is.EqualTo(2.5).Within(.01),"who trips stops first, 2.5 s in");
            Assert.That(stops[1]-stops[0],Is.EqualTo(1.5).Within(.01),"how many stops 1.5 s later");
            Assert.That(stops[2]-stops[1],Is.EqualTo(2).Within(.01),"what they took is held a beat longer");
            double take=First(t=>At(t).Stage==FestivalSpinner.Stage.Take),react=First(t=>At(t).Stage==FestivalSpinner.Stage.React);
            Assert.That(take-stops[2],Is.EqualTo(1).Within(.01),"the results rest 1 s before the take");
            Assert.That(react-take,Is.EqualTo(1.4).Within(.01),"the take lasts 1.4 s");
            Assert.That(FestivalSimulation.SpinSeconds-react,Is.EqualTo(1.6).Within(.01),"and the reaction 1.6 s, up to Loading");
            Assert.That(FestivalSimulation.SpinSeconds,Is.EqualTo(10).Within(.01),"the whole spin is about 10 s");
            state.Phase="Loading";
            Assert.That(At(FestivalSimulation.SpinSeconds+.1).Stage,Is.EqualTo(FestivalSpinner.Stage.Hidden),"Loading ends the sequence");
        }

        // A reel runs at full speed until 1.5 s before its stop, then slows smoothly (a 0.4 s ease jumps 1.1 tiles a second in
        // one frame), passes its result by a little and settles back onto it right at its stop.
        [Test] public void EveryReelEasesOutOverItsLast1_5SecondsThenOvershootsAndSettles()
        {
            const double Frame=1/120.0;
            for(int seed=1;seed<=20;seed++)
            {
                int size=1+seed%8;var state=Spin(seed*977,size,seed*7%size,1+seed%4,FestivalSimulation.Substances[seed%5]);var crew=FestivalSpinner.Crew(state);double start=Start(state);
                FestivalSpinner.Beat At(double seconds)=>FestivalSpinner.At(state,crew,start+seconds);
                for(int i=0;i<Reels.Length;i++)
                {
                    var reel=Reels[i];double stop=FestivalSpinner.ReelStops(i);float rest=reel.Scroll(At(stop));string what="seed "+seed+": the "+reel.Name+" reel";
                    float Speed(double t)=>(reel.Scroll(At(t+Frame))-reel.Scroll(At(t)))/(float)Frame;
                    float cruise=Speed(stop-1.6),worst=0,peak=float.MinValue;
                    for(double t=0;t<stop+.1;t+=Frame)worst=Mathf.Max(worst,Mathf.Abs(Speed(t+Frame)-Speed(t)));
                    for(double t=stop-1.5;t<stop;t+=.005)peak=Mathf.Max(peak,reel.Scroll(At(t)));
                    Assert.That(cruise,Is.GreaterThan(15),what+" runs at full speed 1.6 s before its stop");
                    Assert.That(Speed(stop-1.5-Frame),Is.GreaterThan(cruise*.95f),what+" is still at full speed as its slow-down begins");
                    Assert.That(Speed(stop-1),Is.InRange(cruise*.1f,cruise*.6f),what+" is slowing, not running or stopped, a second before its stop");
                    Assert.That(worst,Is.LessThan(.8f),what+" changes speed smoothly: by under 0.8 tiles a second a frame");
                    Assert.That(peak-rest,Is.GreaterThan(.05).And.LessThan(.5),what+" overshoots its result by a little, then settles back");
                    Assert.That(reel.Scroll(At(stop-.05)),Is.Not.EqualTo(rest),what+" is still settling just before its stop");
                    Assert.That(reel.Scroll(At(stop+.5)),Is.EqualTo(rest),what+" rests where it landed");
                }
            }
        }

        // TRIP-12's 30/30/30/10 as tiles: 1, 2 and 3 three times each and 4 once, so the 4 flashes past rarely and what players
        // see is still what the spinner rolls.
        [Test] public void TheDoseStripHoldsEachDoseInProportionToItsSlice()
        {
            var strip=FestivalSpinner.DoseStrip;
            Assert.That(strip.Count,Is.EqualTo(10),"ten tiles");
            for(int dose=1;dose<=FestivalSimulation.DoseSlices.Count;dose++)
            {
                float share=100f*strip.Count(d=>d==dose)/strip.Count;
                Assert.That(share,Is.EqualTo(FestivalSimulation.DoseSlices[dose-1]).Within(1.5),dose+" doses fill as much of the strip as their slice");
            }
            Assert.That(strip.Count(d=>d==4),Is.EqualTo(1),"the 4 comes round once a loop");
        }

        // A crew of one repeats on every tile and a full crew of eight has one each: the window is never left with a gap.
        [Test] public void CrewsOfOneToEightFillThePeopleReel()
        {
            for(int size=1;size<=8;size++)
            {
                var strip=FestivalSpinner.PeopleStrip(size);
                for(int friend=0;friend<size;friend++)Assert.That(strip.Count(c=>c==friend)*size,Is.EqualTo(strip.Length),"crew of "+size+": friend "+friend+" has as many tiles as everyone else");
                for(float scroll=0;scroll<strip.Length;scroll+=.05f)
                    for(float y=-FestivalSpinner.WindowTiles/2;y<=FestivalSpinner.WindowTiles/2;y+=.05f)
                    {
                        // Neighbouring tiles meet half a tile from each middle; .501 forgives the float steps landing on that seam.
                        bool covered=false;for(int tile=0;tile<strip.Length;tile++)covered|=Mathf.Abs(FestivalSpinner.TileOffset(tile,strip.Length,scroll)-y)<=.501f;
                        Assert.That(covered,"crew of "+size+": a tile fills the window "+y.ToString("F2")+" tiles off the payline at scroll "+scroll.ToString("F2"));
                    }
            }
        }

        // The lever drops as the reels start and springs back. The bulbs chase round the ring while the reels spin, light in order
        // round it as each of the first two reels stops, and the whole ring blinks fast from the last stop until the take.
        [Test] public void TheCabinetPullsItsLeverAndChasesItsBulbs()
        {
            var state=Spin(4242,4,2,3);var crew=FestivalSpinner.Crew(state);double start=Start(state);
            FestivalSpinner.Beat At(double seconds)=>FestivalSpinner.At(state,crew,start+seconds);
            Assert.That(At(.2).Lever,Is.EqualTo(1),"the lever is down as the reels start");
            Assert.That(At(1.2).Lever,Is.Zero,"and has sprung back while they spin");
            const int Ring=FestivalSpinner.Bulbs;
            foreach(bool reduced in new[]{false,true})
            {
                string mode=reduced?"reduced motion: ":"";
                bool On(double t,int b)=>FestivalSpinner.BulbLit((float)t,b,reduced);
                int Lit(double t){int lit=0;for(int b=0;b<Ring;b++)if(On(t,b))lit++;return lit;}
                bool Same(double a,double b){for(int i=0;i<Ring;i++)if(On(a,i)!=On(b,i))return false;return true;}
                Assert.That(Lit(1),Is.EqualTo(Ring/3),mode+"every third bulb is lit while the reels spin");
                double next=1;while(Same(1,next))next+=.001;
                for(int b=0;b<Ring;b++)Assert.That(On(next,b),Is.EqualTo(On(1,(b+Ring-1)%Ring)),mode+"and the lit ones chase one bulb on round the ring");
                for(int i=0;i<Reels.Length-1;i++)
                {
                    double stop=FestivalSpinner.ReelStops(i);int grown=0;
                    for(double t=stop+.01;t<stop+.3;t+=.01)
                    {
                        int lit=Lit(t),arcs=0;for(int b=0;b<Ring;b++)if(On(t,b)&&!On(t,(b+Ring-1)%Ring))arcs++;
                        Assert.That(arcs,Is.EqualTo(1),mode+"the lit bulbs are one run round the ring as the "+Reels[i].Name+" reel stops, at "+t.ToString("F2")+" s");
                        Assert.That(lit,Is.GreaterThanOrEqualTo(grown),mode+"and it only grows");grown=lit;
                    }
                    Assert.That(Lit(stop+.01),Is.GreaterThan(0).And.LessThan(Ring/4),mode+"the sweep starts as the "+Reels[i].Name+" reel stops");
                    Assert.That(Lit(stop+.29),Is.GreaterThan(Ring/2),mode+"and runs on round the ring");
                }
                Assert.That(Lit(FestivalSpinner.ReelStops(1)-.3),Is.EqualTo(Ring/3),mode+"the chase picks up again between stops");
                double last=FestivalSpinner.LastReelStops,take=FestivalSpinner.TakeStarts;int flashes=0;bool on=false,dark=false;
                for(double t=last+.0005;t<take;t+=.001)
                {
                    int lit=Lit(t);Assert.That(lit==0||lit==Ring,mode+"the whole ring blinks together after the last stop, at "+t.ToString("F3")+" s");
                    if(lit==Ring&&!on)flashes++;on=lit==Ring;dark|=lit==0;
                }
                Assert.That(Lit(last+.01),Is.EqualTo(Ring),mode+"the ring lights as the last reel lands");
                Assert.That(dark,mode+"and blinks until the take");
                if(reduced)Assert.That(flashes/(take-last),Is.LessThan(3),"reduced motion blinks the ring under 3 times a second");
                else Assert.That(flashes/(take-last),Is.GreaterThanOrEqualTo(6),"the ring blinks fast after the last stop");
            }
            int Steps(bool reduced){int steps=0;for(double t=.5;t<2;t+=.001)for(int b=0;b<Ring;b++)if(FestivalSpinner.BulbLit((float)t,b,reduced)!=FestivalSpinner.BulbLit((float)(t+.001),b,reduced)){steps++;break;}return steps;}
            Assert.That(Steps(true)*3,Is.InRange(Steps(false)-3,Steps(false)+3),"reduced motion chases at a third of the speed");
        }

        // The bulbs ring the reels on all four sides, in order round the ring (the chase runs round it, every third bulb lit,
        // so it closes without a seam), evenly spaced, on the cabinet and clear of every reel window.
        [Test] public void TheBulbsRingTheReelWindowsOnAllFourSides()
        {
            var root=new GameObject("Bulb ring");var spinner=root.AddComponent<FestivalSpinner>();
            try
            {
                typeof(FestivalSpinner).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(spinner,null);
                var cabinet=(RectTransform)root.transform.Find("Festival spinner/Spinner backdrop/Cabinet");
                Rect Box(RectTransform r)=>new Rect(r.anchoredPosition-r.sizeDelta*.5f,r.sizeDelta);
                var bulbs=cabinet.Cast<Transform>().Where(t=>t.name.StartsWith("Bulb ")).OrderBy(t=>int.Parse(t.name.Substring(5))).Select(t=>(RectTransform)t).ToList();
                var windows=new[]{"People reel","Dose reel","Substance reel"}.Select(n=>Box((RectTransform)cabinet.Find(n))).ToList();
                var reels=Rect.MinMaxRect(windows.Min(w=>w.xMin),windows.Min(w=>w.yMin),windows.Max(w=>w.xMax),windows.Max(w=>w.yMax));
                Assert.That(bulbs.Count,Is.EqualTo(FestivalSpinner.Bulbs),"setup: every bulb is on the cabinet");
                Assert.That(FestivalSpinner.Bulbs%3,Is.Zero,"a whole number of every-third chases round the ring");
                Assert.That(bulbs.Count(b=>b.anchoredPosition.y>reels.yMax),Is.GreaterThanOrEqualTo(3),"bulbs run along the top");
                Assert.That(bulbs.Count(b=>b.anchoredPosition.y<reels.yMin),Is.GreaterThanOrEqualTo(3),"and the bottom");
                Assert.That(bulbs.Count(b=>b.anchoredPosition.x<reels.xMin),Is.GreaterThanOrEqualTo(3),"and the left");
                Assert.That(bulbs.Count(b=>b.anchoredPosition.x>reels.xMax),Is.GreaterThanOrEqualTo(3),"and the right");
                var face=new Rect(-cabinet.sizeDelta*.5f,cabinet.sizeDelta);
                foreach(var bulb in bulbs)
                {
                    float reach=bulb.sizeDelta.x*.71f; // rotated 45 degrees
                    var lit=Rect.MinMaxRect(bulb.anchoredPosition.x-reach,bulb.anchoredPosition.y-reach,bulb.anchoredPosition.x+reach,bulb.anchoredPosition.y+reach);
                    Assert.That(face.Contains(lit.min)&&face.Contains(lit.max),Is.True,bulb.name+" sits on the cabinet");
                    foreach(var window in windows)Assert.That(window.Overlaps(lit),Is.False,bulb.name+" stays clear of the reel windows");
                }
                var gaps=bulbs.Select((b,i)=>Vector2.Distance(b.anchoredPosition,bulbs[(i+1)%bulbs.Count].anchoredPosition)).ToList();
                Assert.That(gaps.Max()/gaps.Min(),Is.LessThan(1.5f),"the bulbs are evenly spaced, in order round the ring");
            }
            finally{typeof(FestivalSpinner).GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(spinner,null);UnityEngine.Object.DestroyImmediate(root);}
        }

        // The level clock and the tripper's dose stand still while the reels, the rest, the take and the reaction run.
        [Test] public void TheLevelClockWaitsOutTheWholeSpin()
        {
            var game=new FestivalSimulation(5);
            foreach(var id in new[]{"host","friend"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
            game.Tick(5.2);
            Assert.That(game.State.Phase,Is.EqualTo("Spinning"),"setup: the ready countdown ends in the spin");
            var dose=game.Player(game.State.TripperId).Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);
            double full=dose.RemainingSeconds,began=game.State.SimulationSeconds;
            while(game.State.Phase=="Spinning"&&game.State.SimulationSeconds<began+60)
            {
                game.Tick(.1);
                Assert.That(game.State.ElapsedSeconds,Is.Zero,"the level clock stands still "+(game.State.SimulationSeconds-began).ToString("F1")+" s into the spin");
                Assert.That(dose.RemainingSeconds,Is.EqualTo(full),"and so does the dose");
            }
            Assert.That(game.State.Phase,Is.EqualTo("Loading"),"the crew loads once the spin ends");
            Assert.That(game.State.SimulationSeconds-began,Is.EqualTo(6+1+1.4+1.6).Within(.15),"the last reel stops 6 s in, the results rest 1 s, then the 1.4 s take and the 1.6 s reaction");
        }

        [Test] public void TheReactionGrowsWithTheDose()
        {
            var sizes=new List<float>();
            for(int dose=1;dose<=4;dose++){var state=Spin(99,3,1,dose);sizes.Add(FestivalSpinner.At(state,FestivalSpinner.Crew(state),Start(state)+FestivalSpinner.ReactStarts+.1).Reaction);}
            Assert.That(sizes,Is.Ordered.Ascending,"every extra dose is a bigger reaction");
            Assert.That(sizes[0],Is.GreaterThan(0),"even one dose shows");
            Assert.That(sizes[3],Is.EqualTo(1),"four doses is the biggest reaction");
        }

        [Test] public void TheReelKeepsATileForEveryConnectedFriendAndTheTripper()
        {
            var state=Spin(7,4,3,1);state.Players[1].Connected=false;state.Players[3].Connected=false;
            Assert.That(FestivalSpinner.Crew(state),Is.EqualTo(new[]{"p0","p2","p3"}),"a friend who left has no tile, but a tripper who just dropped keeps one so the reel can land");
        }

        // LOOP-4: each positive-cash death spins a one-reel corner slot (three KEEP tiles, one DROP) from its frozen public summary,
        // on the world clock, so every viewer sees the same spin land as the pile does.
        static FestivalSimulation Death(int spinSeed,int cash=40,int festival=0)
        {
            var game=new FestivalSimulation(9);var owner=game.AddPlayer("owner","Owner");game.AddPlayer("friend","Friend");var spirit=game.AddPlayer("spirit","Spirit");game.AddPlayer("third","Third");
            game.State.Phase="Playing";game.State.FestivalIndex=festival;game.State.Npcs.Clear();game.State.SpinSeed=spinSeed;owner.Cash=cash;spirit.Life="Spirit";
            owner.Life="Downed";owner.DownedRemaining=.05;game.Tick(.1);
            Assert.That(game.State.DeathSpins,Has.Count.EqualTo(1),"setup: the death is summarised for everyone");
            return game;
        }
        static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        static bool OnDrop(float scroll)=>FestivalSpinner.DeathStrip[FestivalSpinner.TileAt(FestivalSpinner.DeathStrip.Count,scroll)]==FestivalSpinner.DeathDrop;
        static double Wheel=>FestivalSimulation.DeathWheelSeconds;

        [Test] public void TheDeathReelLandsOnDropExactlyWhenTheCashDropped()
        {
            Assert.That(FestivalSpinner.DeathStrip.Count(t=>t==FestivalSpinner.DeathKeep),Is.EqualTo(3),"three KEEP tiles");
            Assert.That(FestivalSpinner.DeathStrip.Count(t=>t==FestivalSpinner.DeathDrop),Is.EqualTo(1),"to one DROP: the 1 in 4");
            int drops=0,keeps=0;
            for(int seed=1;seed<=80;seed++)
            {
                var view=Wire(Death(seed),"friend");var spin=view.DeathSpins[0];
                var start=FestivalSpinner.DeathBeat(view,spin,spin.AtWorldSeconds,false);var landed=FestivalSpinner.DeathBeat(view,spin,spin.AtWorldSeconds+Wheel,false);
                Assert.That(landed.Landed,Is.True,"seed "+seed+": the reel has landed as the pile does");
                Assert.That(OnDrop(landed.Scroll),Is.EqualTo(spin.CashDropped>0),"seed "+seed+": the reel lands on DROP exactly when the cash dropped");
                Assert.That(landed.Scroll,Is.EqualTo(Mathf.Round(landed.Scroll)),"seed "+seed+": its result rests centred on the payline");
                Assert.That(landed.Scroll-start.Scroll,Is.GreaterThan(20),"seed "+seed+": after a fast scroll through dozens of tiles");
                if(spin.CashDropped>0)drops++;else keeps++;
            }
            Assert.That(drops,Is.GreaterThan(5),"setup: real deaths dropped");Assert.That(keeps,Is.GreaterThan(drops),"and kept more often");
        }

        [Test] public void TheDeathReelPopsUpSpinsUntilThePileLandsThenReadsAndHides()
        {
            var view=Wire(Death(4),"friend");var spin=view.DeathSpins[0];
            FestivalSpinner.DeathFrame At(double t)=>FestivalSpinner.DeathBeat(view,spin,spin.AtWorldSeconds+t,false);
            double end=Wheel+FestivalSpinner.DeathResultSeconds;
            Assert.That(At(-.01).Shown,Is.False,"hidden before the death");
            Assert.That(At(0).Shown,Is.True,"it pops up as they die");
            Assert.That(At(0).Pop,Is.LessThan(.2f),"from small");
            float peak=0;for(double t=0;t<FestivalSpinner.DeathPopSeconds;t+=.01)peak=Mathf.Max(peak,At(t).Pop);
            Assert.That(peak,Is.GreaterThan(1),"overshooting a little");
            Assert.That(At(FestivalSpinner.DeathPopSeconds).Pop,Is.EqualTo(1),"then settling at full size");
            double landing=First(t=>At(t).Landed);
            Assert.That(landing,Is.EqualTo(Wheel).Within(.002),"it lands as the pile does, 2.5 s after the death");
            Assert.That(At(landing-.05).Scroll,Is.Not.EqualTo(At(landing).Scroll),"still turning just before");
            Assert.That(At(landing+1).Scroll,Is.EqualTo(At(landing).Scroll),"and resting after");
            Assert.That(FestivalSpinner.DeathResultSeconds,Is.EqualTo(2.5f),"the result reads for 2.5 s");
            Assert.That(At(end-FestivalSpinner.DeathFadeSeconds-.01).Alpha,Is.EqualTo(1),"fully readable");
            Assert.That(At(end-.05).Alpha,Is.GreaterThan(0).And.LessThan(1),"fading out at the end");
            Assert.That(At(end-.01).Shown,Is.True);
            Assert.That(At(end).Shown,Is.False,"hidden from death + wheel + result on");Assert.That(At(60).Shown,Is.False);
            foreach(var phase in new[]{FestivalSimulation.MinigamePhase,"Results","Shopping"})
            {view.Phase=phase;Assert.That(At(1).Shown,Is.False,"hidden outside Playing: "+phase);}
            view.Phase="Playing";view.RoundId="next round";
            Assert.That(At(1).Shown,Is.False,"an event of another round never shows");
        }

        [Test] public void EveryViewerOfARealDeathSeesTheSameCornerReel()
        {
            for(int seed=1;seed<=12;seed++)
            {
                var game=Death(seed);
                Assert.That(game.Player("owner").Life,Is.EqualTo("Spirit"),"setup: the dead player watches as a spirit");
                string first=null;
                foreach(var viewer in new[]{"owner","friend","spirit"})
                {
                    var view=Wire(game,viewer);var spin=view.DeathSpins.Single();var frames="";
                    foreach(double t in new[]{0,.3,1.7,2.49,2.5,4})
                    {var f=FestivalSpinner.DeathBeat(view,spin,spin.AtWorldSeconds+t,false);frames+=f.Shown+","+f.Landed+","+f.Scroll+","+f.Pop+","+f.Alpha+";";}
                    if(first==null)first=frames;
                    else Assert.That(frames,Is.EqualTo(first),"seed "+seed+": "+viewer+" sees exactly the reel every other viewer does");
                }
            }
        }

        // A neutral reel waits for the same authoritative landing, then shows the same result for the same time: no spin, no pop.
        [Test] public void ReducedMotionWaitsOnANeutralReelForTheSameLanding()
        {
            var view=Wire(Death(4),"spirit");var spin=view.DeathSpins[0];
            FestivalSpinner.DeathFrame At(double t,bool reduced)=>FestivalSpinner.DeathBeat(view,spin,spin.AtWorldSeconds+t,reduced);
            float still=At(0,true).Scroll;
            for(int step=0;step*.05<Wheel-.01;step++)
            {
                double t=step*.05;var f=At(t,true);
                Assert.That(f.Shown&&f.Neutral&&!f.Landed,Is.True,t.ToString("F2")+" s: shown, neutral, pending");
                Assert.That(f.Pop,Is.EqualTo(1),"no pop");Assert.That(f.Scroll,Is.EqualTo(still),"no spin");
            }
            foreach(double t in new[]{Wheel,Wheel+1,Wheel+FestivalSpinner.DeathResultSeconds-.05,Wheel+FestivalSpinner.DeathResultSeconds})
            {
                FestivalSpinner.DeathFrame reduced=At(t,true),full=At(t,false);
                Assert.That((reduced.Shown,reduced.Landed,reduced.Scroll,reduced.Alpha),Is.EqualTo((full.Shown,full.Landed,full.Scroll,full.Alpha)),t+" s: the same result for the same time");
                Assert.That(reduced.Neutral,Is.False);
            }
            Assert.That(At(-.01,true).Shown,Is.False);
        }

        [Test] public void ReelsForDeathsAtTheSameTimeStackInDeathOrder()
        {
            var game=Death(4);var friend=game.Player("friend");friend.Cash=30;
            game.Tick(1);friend.Life="Downed";friend.DownedRemaining=.05;game.Tick(.1);
            var view=Wire(game,"spirit");var shown=new List<DeathSpinState>();double first=view.DeathSpins[0].AtWorldSeconds;
            Assert.That(view.DeathSpins.Select(s=>s.PlayerId),Is.EqualTo(new[]{"owner","friend"}),"setup: two real deaths a second apart");
            FestivalSpinner.ShownDeaths(view,first-.1,shown);Assert.That(shown,Is.Empty,"none before the first death");
            FestivalSpinner.ShownDeaths(view,first+1.5,shown);Assert.That(shown.Select(s=>s.PlayerId),Is.EqualTo(new[]{"owner","friend"}),"the earlier death's reel comes first");
            FestivalSpinner.ShownDeaths(view,first+Wheel+FestivalSpinner.DeathResultSeconds+.05,shown);Assert.That(shown.Select(s=>s.PlayerId),Is.EqualTo(new[]{"friend"}),"then the later one alone");
        }

        // The corner reel never takes a click from the HUD or the menu over it, nor moves the camera or opens anything.
        [Test] public void TheDeathReelDrawsUnderTheHudAndTakesNoClicks()
        {
            var root=new GameObject("Death reel canvas");
            try
            {
                var spinner=root.AddComponent<FestivalSpinner>();typeof(FestivalSpinner).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(spinner,null);
                var hud=root.AddComponent<FestivalHud>();typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
                var view=Wire(Death(4),"friend");var spin=view.DeathSpins[0];
                Assert.That(spinner.FollowDeaths(view,view.Players.Find(p=>p.Id=="friend"),spin.AtWorldSeconds+1,false),Is.EqualTo(1),"one reel shows");
                var canvas=root.GetComponentsInChildren<Canvas>(true).SingleOrDefault(c=>c.name==FestivalSpinner.DeathCanvasName);
                Assert.That(canvas,Is.Not.Null,"the corner reel has its own canvas");
                Assert.That(canvas.gameObject.activeInHierarchy,Is.True);
                var hudCanvas=root.GetComponentsInChildren<Canvas>(true).Single(c=>c.name=="Festival HUD");
                Assert.That(canvas.sortingOrder,Is.LessThan(hudCanvas.sortingOrder),"under the HUD");
                Assert.That(canvas.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true),Is.Empty,"no raycaster: clicks pass through to the HUD");
                foreach(var graphic in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))Assert.That(graphic.raycastTarget,Is.False,graphic.name+" takes no clicks");
                Assert.That(canvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        // What the corner reel shows: who died, the frozen amount in the viewer's money as it turns, then KEPT or DROPPED, then nothing.
        [Test] public void TheCornerReelNamesWhoDiedAndReadsTheirCashInYourMoney()
        {
            var root=new GameObject("Death reel text");
            try
            {
                var spinner=root.AddComponent<FestivalSpinner>();typeof(FestivalSpinner).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(spinner,null);
                int seed=1;while(Death(seed).State.DeathSpins[0].CashDropped==0)seed++;
                string Read(string name)=>root.GetComponentsInChildren<UnityEngine.UI.Text>(false).FirstOrDefault(t=>t.name==name&&t.GetComponentInParent<Canvas>().name==FestivalSpinner.DeathCanvasName)?.text??"(hidden)";
                var view=Wire(Death(seed),"friend");var spin=view.DeathSpins[0];var friend=view.Players.Find(p=>p.Id=="friend");
                spinner.FollowDeaths(view,friend,spin.AtWorldSeconds+1,false);
                Assert.That(Read("Title"),Is.EqualTo("OWNER DIED"),"it names who died");
                Assert.That(Read("Result"),Is.EqualTo("$40"),"and the cash at stake as it turns");
                int parts=root.GetComponentsInChildren<RectTransform>(true).Length;
                spinner.FollowDeaths(view,friend,spin.AtWorldSeconds+Wheel+.1,false);
                Assert.That(Read("Result"),Is.EqualTo("DROPPED $40"),"then the result");
                Assert.That(root.GetComponentsInChildren<RectTransform>(true).Length,Is.EqualTo(parts),"on the same card, not a new one");
                spinner.FollowDeaths(view,friend,spin.AtWorldSeconds+Wheel+FestivalSpinner.DeathResultSeconds,false);
                Assert.That(Read("Title"),Is.EqualTo("(hidden)"),"and gone once read");
                var playa=Wire(Death(seed,40,Festivals.PlayaFestival),"friend");spin=playa.DeathSpins[0];friend=playa.Players.Find(p=>p.Id=="friend");
                Assert.That(friend.Ordinal,Is.EqualTo(1),"setup: the friend counts in ramen packets");
                spinner.FollowDeaths(playa,friend,spin.AtWorldSeconds+Wheel+.1,false);
                Assert.That(Read("Result"),Is.EqualTo("DROPPED 40 RAMEN PACKETS"),"on Ember Playa, in the viewer's odd objects");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
