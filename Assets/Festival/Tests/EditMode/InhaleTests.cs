using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // GAS-6: the inhale every Giggle Balloon take plays is made in code: 3 s of breath swelling up, a rubbery squeak and a held breath
    // before the giggles. The same sound every time, with no draw on any random generator.
    public sealed class InhaleTests
    {
        private static float[] Samples(AudioClip clip){var samples=new float[clip.samples*clip.channels];Assert.That(clip.GetData(samples,0),Is.True,"setup: the clip's samples");return samples;}
        private static float Rms(float[] samples,int from,int to){double sum=0;for(int i=from;i<to;i++)sum+=samples[i]*samples[i];return (float)System.Math.Sqrt(sum/System.Math.Max(1,to-from));}
        private static float Loudest(float[] samples,int from,int to){float peak=0;for(int i=from;i<to;i++)peak=Mathf.Max(peak,Mathf.Abs(samples[i]));return peak;}

        [Test]public void AnInhaleIsThreeSecondsOfMonoBreathThatSwellsThenHolds()
        {
            var clip=FestivalInhale.CreateClip();
            try
            {
                Assert.That(clip.channels,Is.EqualTo(1),"mono");
                Assert.That(clip.length,Is.EqualTo(3).Within(.05f),"3 s long");
                var samples=Samples(clip);int half=samples.Length/2;
                Assert.That(Rms(samples,0,samples.Length),Is.GreaterThan(.03f),"not silent");
                Assert.That(Rms(samples,half,samples.Length),Is.GreaterThan(Rms(samples,0,half)*1.5f),"it swells: its second half is louder than its first");
                Assert.That(Loudest(samples,0,samples.Length),Is.InRange(.3f,1f),"loud, and never clipped");
                Assert.That(Loudest(samples,samples.Length-clip.frequency/20,samples.Length),Is.LessThan(1e-3f),"a held breath: its last 50 ms are silent");
                int second=clip.frequency;
                Assert.That(Rms(samples,second,2*second),Is.GreaterThan(Rms(samples,0,second)),"the breath still swells through its second second");
                Assert.That(Rms(samples,(int)(2.2f*second),(int)(2.35f*second)),Is.GreaterThan(Rms(samples,second,(int)(1.15f*second))),"and is loudest near its end, at about 2.4 s");
                Assert.That(Loudest(samples,(int)(2.6f*second),samples.Length),Is.LessThan(1e-3f),"then a held breath, silent from 2.6 s");
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
