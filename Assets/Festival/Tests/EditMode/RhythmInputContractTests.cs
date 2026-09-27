using Festival.Presentation;
using NUnit.Framework;

namespace Festival.Tests
{
    public sealed class RhythmInputContractTests
    {
        [Test]public void EachDanceLaneAcceptsAnArrowAndItsMatchingWasdKey()
        {
            string[] arrows={"leftArrow","downArrow","upArrow","rightArrow"};
            string[] wasd={"a","s","w","d"};
            using(var input=new FestivalInput("rhythm-contract-test"))
            {
                for(int lane=0;lane<4;lane++)
                {
                    var bindings=input.Notes[lane].bindings;
                    Assert.That(bindings.Count,Is.EqualTo(2),"Lane "+lane);
                    Assert.That(bindings[0].path,Is.EqualTo("<Keyboard>/"+arrows[lane]));
                    Assert.That(bindings[1].path,Is.EqualTo("<Keyboard>/"+wasd[lane]));
                }
            }
        }
    }
}
