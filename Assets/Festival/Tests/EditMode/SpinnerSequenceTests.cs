using System;
using System.Collections.Generic;
using System.Linq;
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

        // A fast scroll that eases into the stop: 0.3 s out a reel still runs past several tiles a second, then it passes its
        // result by a little and settles back onto it.
        [Test] public void EveryReelScrollsBrisklyThenOvershootsAndSettles()
        {
            for(int seed=1;seed<=20;seed++)
            {
                int size=1+seed%8;var state=Spin(seed*977,size,seed*7%size,1+seed%4,FestivalSimulation.Substances[seed%5]);var crew=FestivalSpinner.Crew(state);double start=Start(state);
                FestivalSpinner.Beat At(double seconds)=>FestivalSpinner.At(state,crew,start+seconds);
                for(int i=0;i<Reels.Length;i++)
                {
                    var reel=Reels[i];double stop=First(t=>reel.Landed(At(t)));float rest=reel.Scroll(At(stop));
                    float speed=(reel.Scroll(At(stop-.29))-reel.Scroll(At(stop-.3)))/.01f,peak=float.MinValue;
                    for(double t=stop-.3;t<stop;t+=.005)peak=Mathf.Max(peak,reel.Scroll(At(t)));
                    Assert.That(speed,Is.GreaterThan(5),"seed "+seed+": the "+reel.Name+" reel still scrolls briskly 0.3 s before it stops");
                    Assert.That(peak-rest,Is.GreaterThan(.05).And.LessThan(.5),"seed "+seed+": the "+reel.Name+" reel overshoots its result by a little, then settles back");
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

        // The lever drops as the reels start and springs back; the bulbs chase while the reels spin, flash as each stops, then stay lit.
        [Test] public void TheCabinetPullsItsLeverAndChasesItsBulbs()
        {
            var state=Spin(4242,4,2,3);var crew=FestivalSpinner.Crew(state);double start=Start(state);
            FestivalSpinner.Beat At(double seconds)=>FestivalSpinner.At(state,crew,start+seconds);
            Assert.That(At(.2).Lever,Is.EqualTo(1),"the lever is down as the reels start");
            Assert.That(At(1.2).Lever,Is.Zero,"and has sprung back while they spin");
            int Lit(double t){int lit=0;for(int b=0;b<FestivalSpinner.Bulbs;b++)if(FestivalSpinner.BulbLit((float)t,b))lit++;return lit;}
            bool Same(double a,double b){for(int i=0;i<FestivalSpinner.Bulbs;i++)if(FestivalSpinner.BulbLit((float)a,i)!=FestivalSpinner.BulbLit((float)b,i))return false;return true;}
            Assert.That(Lit(1),Is.GreaterThan(0).And.LessThan(FestivalSpinner.Bulbs/2),"some bulbs are lit while the reels spin");
            Assert.That(Same(1,1.1),Is.False,"and the lit ones chase along the row");
            for(int i=0;i<Reels.Length;i++)
            {
                double stop=FestivalSpinner.ReelStops(i);
                Assert.That(Lit(stop+.02),Is.EqualTo(FestivalSpinner.Bulbs),"the whole row flashes as the "+Reels[i].Name+" reel stops");
                Assert.That(Lit(stop+.15),Is.Zero,"on and off");
            }
            Assert.That(Lit(FestivalSpinner.ReelStops(1)-.3),Is.LessThan(FestivalSpinner.Bulbs/2),"the chase picks up again between stops");
            Assert.That(Lit(FestivalSpinner.TakeStarts-.05),Is.EqualTo(FestivalSpinner.Bulbs),"the row stays lit once every reel rests");
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
    }
}
