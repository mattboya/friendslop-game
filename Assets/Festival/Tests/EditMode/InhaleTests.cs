using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // GAS-6: the inhale every Giggle Balloon take plays is made in code: about a second of breath swelling up, a rubbery squeak and a
    // tiny held breath before the giggles. The same sound every time, with no draw on any random generator.
    public sealed class InhaleTests
    {
        private static float[] Samples(AudioClip clip){var samples=new float[clip.samples*clip.channels];Assert.That(clip.GetData(samples,0),Is.True,"setup: the clip's samples");return samples;}
        private static float Rms(float[] samples,int from,int to){double sum=0;for(int i=from;i<to;i++)sum+=samples[i]*samples[i];return (float)System.Math.Sqrt(sum/System.Math.Max(1,to-from));}
        private static float Loudest(float[] samples,int from,int to){float peak=0;for(int i=from;i<to;i++)peak=Mathf.Max(peak,Mathf.Abs(samples[i]));return peak;}

        [Test]public void AnInhaleIsAboutASecondOfMonoBreathThatSwellsThenHolds()
        {
            var clip=FestivalInhale.CreateClip();
            try
            {
                Assert.That(clip.channels,Is.EqualTo(1),"mono");
                Assert.That(clip.length,Is.InRange(.8f,1.4f),"0.8-1.4 s long");
                var samples=Samples(clip);int half=samples.Length/2;
                Assert.That(Rms(samples,0,samples.Length),Is.GreaterThan(.03f),"not silent");
                Assert.That(Rms(samples,half,samples.Length),Is.GreaterThan(Rms(samples,0,half)*1.5f),"it swells: its second half is louder than its first");
                Assert.That(Loudest(samples,0,samples.Length),Is.InRange(.3f,1f),"loud, and never clipped");
                Assert.That(Loudest(samples,samples.Length-clip.frequency/20,samples.Length),Is.LessThan(1e-3f),"a held breath: its last 50 ms are silent");
            }
            finally{Object.DestroyImmediate(clip);}
        }

        [Test]public void EveryInhaleIsTheSame()
        {
            var first=FestivalInhale.CreateClip();var second=FestivalInhale.CreateClip();
            try{Assert.That(Samples(second),Is.EqualTo(Samples(first)),"the same samples each time");}
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }
    }
}
