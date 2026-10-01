using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;

namespace Festival.Tests
{
    // SPIN-1: the wheels and the take are computed only from the public spin result, so every client shows the same spin.
    public sealed class SpinnerSequenceTests
    {
        static RoundState Spin(int seed,int crew,int tripper,int dose,string substance="lsd")
        {
            var state=new RoundState{Phase="Spinning",RoundId="round_"+seed,SpinSeed=seed,SpinEndsAt=100+FestivalSimulation.SpinSeconds,TripperId="p"+tripper};
            for(int i=0;i<crew;i++)state.Players.Add(new PlayerState{Id="p"+i,Name="Friend "+i});
            state.Doses.Add(new PlayerDose{PlayerId=state.TripperId,Dose=dose,Substance=substance});
            return state;
        }
        static int[] Equal(int count){var weights=new int[count];for(int i=0;i<count;i++)weights[i]=1;return weights;}
        static double Start(RoundState state)=>state.SpinEndsAt-FestivalSimulation.SpinSeconds;
        // SPIN-2: the wheels in the order they spin, as a beat shows them. TRIP-5 adds the substance wheel third.
        static readonly (string Name,Func<FestivalSpinner.Beat,float> Degrees,Func<FestivalSpinner.Beat,bool> Landed)[] Wheels=
            {("people",b=>b.PeopleDegrees,b=>b.PeopleLanded),("dose",b=>b.DoseDegrees,b=>b.DoseLanded),("substance",b=>b.SubstanceDegrees,b=>b.SubstanceLanded)};
        // The first millisecond of the spin at which reached holds.
        static double First(Func<double,bool> reached){for(int ms=0;ms<=FestivalSimulation.SpinSeconds*1000;ms++)if(reached(ms/1000.0))return ms/1000.0;return double.NaN;}

        [Test] public void ThePointerReadsTheSliceAtTheTopWithTheFourDoseSliver()
        {
            var doses=FestivalSimulation.DoseSlices;
            Assert.That(FestivalSpinner.SliceUnderPointer(doses,0),Is.EqualTo(0),"at rest the pointer sits on the start of the 1-dose slice");
            Assert.That(FestivalSpinner.SliceUnderPointer(doses,200),Is.EqualTo(1),"turned 200 degrees clockwise, the pointer reads 2 doses");
            Assert.That(FestivalSpinner.SliceUnderPointer(doses,20),Is.EqualTo(3),"the 4-dose sliver sits just before the top");
            Assert.That(FestivalSpinner.SliceUnderPointer(doses,26),Is.EqualTo(2),"and is thinner than 26 degrees (7% of the wheel is 25.2)");
            Assert.That(FestivalSpinner.SliceUnderPointer(Equal(4),-100),Is.EqualTo(1),"a people wheel of four has quarter slices");
        }

        [Test] public void TheWheelsLandOnTheStatesTripperAndDose()
        {
            for(int seed=1;seed<=40;seed++)
            {
                int crew=1+seed%8,tripper=seed*7%crew,dose=1+seed%4;
                var state=Spin(seed*977,crew,tripper,dose);var names=FestivalSpinner.Crew(state);
                var landed=FestivalSpinner.At(state,names,Start(state)+FestivalSpinner.TakeStarts);
                Assert.That(FestivalSpinner.SliceUnderPointer(Equal(crew),landed.PeopleDegrees),Is.EqualTo(tripper),"seed "+seed+": the people wheel stops on the tripper");
                Assert.That(FestivalSpinner.SliceUnderPointer(FestivalSimulation.DoseSlices,landed.DoseDegrees),Is.EqualTo(dose-1),"seed "+seed+": the dose wheel stops on "+dose);
                Assert.That(landed.PeopleDegrees,Is.GreaterThan(3*360),"seed "+seed+": the people wheel spins a few turns first");
            }
        }

        [Test] public void EveryViewerOfARealSpinSeesTheSameWheels()
        {
            var seen=new HashSet<int>();
            for(int seed=1;seed<=12;seed++)
            {
                var game=new FestivalSimulation(seed);game.State.LevelIndex=seed%2==0?3:0;
                foreach(var id in new[]{"host","friend","third","fourth"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
                game.Tick(5.2);
                Assert.That(game.State.Phase,Is.EqualTo("Spinning"),"setup: seed "+seed+" is spinning");
                var spun=game.State.Doses.Find(d=>d.PlayerId==game.State.TripperId);int dose=spun.Dose;string substance=spun.Substance;seen.Add(dose);
                FestivalSpinner.Beat? first=null;
                foreach(var viewer in game.State.Players)
                {
                    var view=FestivalSession.ViewFor(game,viewer.Id);var crew=FestivalSpinner.Crew(view);
                    var beat=FestivalSpinner.At(view,crew,Start(view)+FestivalSpinner.TakeStarts);
                    Assert.That(crew[FestivalSpinner.SliceUnderPointer(Equal(crew.Count),beat.PeopleDegrees)],Is.EqualTo(game.State.TripperId),viewer.Id+" sees the wheel pick the tripper");
                    Assert.That(FestivalSpinner.SliceUnderPointer(FestivalSimulation.DoseSlices,beat.DoseDegrees)+1,Is.EqualTo(dose),viewer.Id+" sees the tripper's dose");
                    Assert.That(FestivalSimulation.Substances[FestivalSpinner.SliceUnderPointer(Equal(FestivalSimulation.Substances.Count),beat.SubstanceDegrees)],Is.EqualTo(substance),viewer.Id+" sees what the tripper took");
                    if(first==null)first=beat;
                    else Assert.That(new[]{beat.PeopleDegrees,beat.DoseDegrees,beat.SubstanceDegrees},Is.EqualTo(new[]{first.Value.PeopleDegrees,first.Value.DoseDegrees,first.Value.SubstanceDegrees}),viewer.Id+" sees exactly the host's spin");
                }
            }
            Assert.That(seen.Count,Is.GreaterThanOrEqualTo(2),"setup: the real spins covered more than one dose");
        }

        // TRIP-5: the third wheel's five equal slices are the substances, and it lands on the one the tripper's dose list entry
        // holds, on its own seeded spot, so every client stops on the same point.
        [Test] public void TheSubstanceWheelLandsOnTheRolledSlice()
        {
            var slices=Equal(FestivalSimulation.Substances.Count);
            for(int seed=1;seed<=40;seed++)
                foreach(var substance in FestivalSimulation.Substances)
                {
                    var state=Spin(seed*977,1+seed%8,0,1+seed%4,substance);
                    var landed=FestivalSpinner.At(state,FestivalSpinner.Crew(state),Start(state)+FestivalSpinner.TakeStarts);
                    Assert.That(landed.Substance,Is.EqualTo(substance),"seed "+seed+": the beat names what the tripper took");
                    Assert.That(FestivalSimulation.Substances[FestivalSpinner.SliceUnderPointer(slices,landed.SubstanceDegrees)],Is.EqualTo(substance),"seed "+seed+": the substance wheel stops on "+substance);
                    Assert.That(landed.SubstanceDegrees,Is.GreaterThan(3*360),"seed "+seed+": after a few turns");
                }
            Assert.That(FestivalSimulation.WheelCount,Is.EqualTo(3),"the timeline makes room for three wheels");
        }

        [Test] public void TheSequenceRunsFromTheSpinResult()
        {
            var state=Spin(4242,4,2,3);var crew=FestivalSpinner.Crew(state);double start=Start(state);
            FestivalSpinner.Beat At(double seconds)=>FestivalSpinner.At(state,crew,start+seconds);
            var landed=At(FestivalSpinner.TakeStarts);
            var spinning=At(1);
            Assert.That(spinning.Stage,Is.EqualTo(FestivalSpinner.Stage.People),"the people wheel goes first");
            Assert.That(spinning.PeopleDegrees,Is.GreaterThan(0).And.LessThan(landed.PeopleDegrees),"and is still turning a second in");
            Assert.That(spinning.PeopleLanded||spinning.DoseDegrees!=0,Is.False,"the dose wheel waits its turn");
            var picked=At(FestivalSpinner.WheelStarts(1)-.05);
            Assert.That(picked.PeopleLanded&&picked.PeopleDegrees==landed.PeopleDegrees,"the people wheel has landed before the dose wheel starts");
            var dosing=At(FestivalSpinner.WheelStarts(1)+.5);
            Assert.That(dosing.Stage,Is.EqualTo(FestivalSpinner.Stage.Dose));
            Assert.That(dosing.DoseDegrees,Is.GreaterThan(0).And.LessThan(landed.DoseDegrees),"then the dose wheel turns");
            var tasting=At(FestivalSpinner.WheelStarts(2)+.5);
            Assert.That(At(FestivalSpinner.WheelStarts(2)-.05).DoseLanded,"the dose wheel has landed before the substance wheel starts");
            Assert.That(tasting.Stage,Is.EqualTo(FestivalSpinner.Stage.Substance),"then the substance wheel");
            Assert.That(tasting.SubstanceDegrees,Is.GreaterThan(0).And.LessThan(landed.SubstanceDegrees),"turns");
            Assert.That(At(FestivalSpinner.TakeStarts-.05).SubstanceLanded,"and lands before the take");
            Assert.That(At(FestivalSpinner.TakeStarts+.1).Stage,Is.EqualTo(FestivalSpinner.Stage.Take),"then the tripper takes the dose");
            Assert.That(At(FestivalSpinner.ReactStarts+.1).Stage,Is.EqualTo(FestivalSpinner.Stage.React),"and reacts");
            Assert.That(FestivalSimulation.SpinSeconds-FestivalSpinner.TakeStarts,Is.EqualTo(3).Within(.01),"the take and reaction fill the last 3 s before Loading");
            Assert.That(FestivalSpinner.ReactStarts,Is.GreaterThan(FestivalSpinner.TakeStarts).And.LessThan(FestivalSimulation.SpinSeconds),"the reaction starts inside the spin");
            state.Phase="Loading";
            Assert.That(At(FestivalSimulation.SpinSeconds+.1).Stage,Is.EqualTo(FestivalSpinner.Stage.Hidden),"Loading ends the sequence");
        }

        // SPIN-2: one wheel after another, each turning 4 s from start to stop and resting .6 s on its result; after the last rest
        // the tripper takes the dose for 1.4 s and reacts for 1.6 s, which ends the spin.
        [Test] public void EachWheelTurnsForFourSecondsThenRestsOnItsResult()
        {
            var state=Spin(4242,4,2,3);var crew=FestivalSpinner.Crew(state);double start=Start(state);
            FestivalSpinner.Beat At(double seconds)=>FestivalSpinner.At(state,crew,start+seconds);
            var landed=At(FestivalSimulation.SpinSeconds-.01);double rested=0;
            for(int i=0;i<Wheels.Length;i++)
            {
                var wheel=Wheels[i];
                double moves=First(t=>wheel.Degrees(At(t))>0),stops=First(t=>wheel.Landed(At(t)));
                Assert.That(stops-moves,Is.EqualTo(4).Within(.01),"the "+wheel.Name+" wheel turns for 4 s from start to stop");
                Assert.That(moves-rested,Is.EqualTo(i==0?0:.6).Within(.01),i==0?"the people wheel turns as the spin starts":"the "+wheel.Name+" wheel waits .6 s after the last one stops");
                Assert.That(wheel.Degrees(At(stops-.05)),Is.LessThan(wheel.Degrees(landed)),"the "+wheel.Name+" wheel is still turning just before it stops");
                Assert.That(wheel.Degrees(At(stops)),Is.EqualTo(wheel.Degrees(landed)),"and stops where it lands");
                rested=stops;
            }
            double take=First(t=>At(t).Stage==FestivalSpinner.Stage.Take),react=First(t=>At(t).Stage==FestivalSpinner.Stage.React);
            Assert.That(take-rested,Is.EqualTo(.6).Within(.01),"the last wheel rests .6 s on its result before the take");
            Assert.That(react-take,Is.EqualTo(1.4).Within(.01),"the take lasts 1.4 s");
            Assert.That(FestivalSimulation.SpinSeconds-react,Is.EqualTo(1.6).Within(.01),"and the reaction 1.6 s, up to Loading");
        }

        // About 8 turns under the cubic ease-out: 3 s into its 4 s, a wheel still sweeps over 120 degrees a second instead of
        // crawling through the second half of its spin.
        [Test] public void EveryWheelStillTurnsBrisklyThreeSecondsIn()
        {
            for(int seed=1;seed<=20;seed++)
            {
                int size=1+seed%8;var state=Spin(seed*977,size,seed*7%size,1+seed%4);var crew=FestivalSpinner.Crew(state);double start=Start(state);
                FestivalSpinner.Beat At(double seconds)=>FestivalSpinner.At(state,crew,start+seconds);
                foreach(var wheel in Wheels)
                {
                    double moves=First(t=>wheel.Degrees(At(t))>0);
                    float speed=(wheel.Degrees(At(moves+3.01))-wheel.Degrees(At(moves+3)))/.01f;
                    Assert.That(speed,Is.GreaterThan(120),"seed "+seed+": the "+wheel.Name+" wheel still turns briskly 3 s in");
                }
            }
        }

        // The level clock and the tripper's dose stand still while the wheels, the rests, the take and the reaction run.
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
            Assert.That(game.State.SimulationSeconds-began,Is.EqualTo(Wheels.Length*(4+.6)+1.4+1.6).Within(.15),"the spin lasts 4 s and a .6 s rest per wheel (three wheels), the 1.4 s take and the 1.6 s reaction");
        }

        [Test] public void TheReactionGrowsWithTheDose()
        {
            var sizes=new List<float>();
            for(int dose=1;dose<=4;dose++){var state=Spin(99,3,1,dose);sizes.Add(FestivalSpinner.At(state,FestivalSpinner.Crew(state),Start(state)+FestivalSpinner.ReactStarts+.1).Reaction);}
            Assert.That(sizes,Is.Ordered.Ascending,"every extra dose is a bigger reaction");
            Assert.That(sizes[0],Is.GreaterThan(0),"even one dose shows");
            Assert.That(sizes[3],Is.EqualTo(1),"four doses is the biggest reaction");
        }

        [Test] public void TheWheelKeepsASliceForEveryConnectedFriendAndTheTripper()
        {
            var state=Spin(7,4,3,1);state.Players[1].Connected=false;state.Players[3].Connected=false;
            Assert.That(FestivalSpinner.Crew(state),Is.EqualTo(new[]{"p0","p2","p3"}),"a friend who left has no slice, but a tripper who just dropped keeps one so the wheel can land");
        }
    }
}
