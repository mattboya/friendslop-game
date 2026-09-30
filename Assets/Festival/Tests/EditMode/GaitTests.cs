using Festival.Presentation;
using NUnit.Framework;

namespace Festival.Tests
{
    public sealed class GaitTests
    {
        // World leg length (hip to ankle) of an .82-scale festivalgoer.
        const float Leg=.55f;

        [Test]public void CadenceFollowsLegLengthInsteadOfAFixedStride()
        {
            var stroll=FestivalGait.For(1.4f,Leg);
            var jog=FestivalGait.For(4f,Leg);
            var sprint=FestivalGait.For(6f,Leg);
            // The old fixed ~1.1 m cycle meant ~7 steps/s at the 4 m/s base speed.
            Assert.That(stroll.StepsPerSecond(1.4f),Is.InRange(2.2f,3.4f));
            Assert.That(jog.StepsPerSecond(4f),Is.InRange(3.8f,5.2f));
            Assert.That(sprint.StepsPerSecond(6f),Is.InRange(4.5f,6f));
            Assert.That(FestivalGait.For(4f,.9f).Stride,Is.GreaterThan(jog.Stride),"Longer legs take longer strides");
        }

        [Test]public void WalkBecomesARunWithAFlightPhaseInsideLegReach()
        {
            var stroll=FestivalGait.For(1.4f,Leg);
            var jog=FestivalGait.For(4f,Leg);
            var sprint=FestivalGait.For(6f,Leg);
            Assert.That(stroll.Run,Is.LessThan(.1f));
            Assert.That(stroll.Stance,Is.GreaterThan(.55f),"Walking keeps a double-support phase");
            Assert.That(jog.Run,Is.GreaterThan(.9f));
            Assert.That(sprint.Stance,Is.LessThan(.35f),"Running has both feet off the ground between steps");
            // Ground covered over one planted foot must fit these short legs' reach (~.31 m each way).
            foreach(var gait in new[]{stroll,jog,sprint})Assert.That(gait.Stance*gait.Stride,Is.LessThan(.62f));
        }
    }
}
