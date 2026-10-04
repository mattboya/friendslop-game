using System.Collections.Generic;
using Festival.Core;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class RhythmHoldContractTests
    {
        [Test] public void HoldNotesRoundTripAndLegacyNotesRemainTaps()
        {
            var hold=new RhythmNote{Id=17,Direction=2,TimeSeconds=2,HoldSeconds=1};
            var restored=JsonUtility.FromJson<RhythmNote>(JsonUtility.ToJson(hold));
            Assert.That(restored.HoldSeconds,Is.EqualTo(1));
            Assert.That(restored.Id,Is.EqualTo(17));
            var legacy=JsonUtility.FromJson<RhythmNote>("{\"Id\":17,\"Direction\":2,\"TimeSeconds\":2}");
            Assert.That(legacy.HoldSeconds,Is.EqualTo(0));
            var chart=new RhythmChart{DurationSeconds=3,Notes=new List<RhythmNote>{restored}};
            var judge=new RhythmJudge(chart);judge.Submit(2,2);judge.Release(2,2.5);
            Assert.That(judge.Score,Is.EqualTo(0));Assert.That(judge.Drops,Is.EqualTo(1));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test] public void DebugDiagnosticsFollowTheProductionHeadAndDrop()
        {
            var chart=new RhythmChart{DurationSeconds=3,Notes=new List<RhythmNote>{new RhythmNote{Id=17,Direction=2,TimeSeconds=2,HoldSeconds=1}}};
            var judge=new RhythmJudge(chart);var messages=new List<string>();judge.Diagnostic=messages.Add;
            judge.Submit(2,2);judge.Release(2,2.5);judge.Release(2,2.6);
            Assert.That(messages.Count,Is.EqualTo(2),"one head and one drop; repeated releases are silent");
            Assert.That(messages[0],Does.Contain("head id=17"));
            Assert.That(messages[1],Does.Contain("drop id=17"));
        }
#endif
    }
}
