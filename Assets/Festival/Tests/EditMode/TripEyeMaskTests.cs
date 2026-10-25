using System;
using System.Collections.Generic;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // TRIP-16: Pony Dust and Couch Lock look out through sleepy eyes that now and then blink: the lids' shape, how far they rest
    // closed by substance and dose, the one-second blink and the local scheduler that times it, all pure.
    public sealed class TripEyeMaskTests
    {
        // Where the shade first reaches half along a column x, for lids as soft as soft: the lid's edge. Found by halving, to float
        // precision, so the flatter middle of the rounder lids still shows its curve.
        private static float Edge(float x,float aperture,float soft)
        {
            float clear=0,shut=1;
            for(int step=0;step<40;step++){float y=(clear+shut)/2;if(FestivalTrip.EyeShade(x,y,aperture,soft)<.5f)clear=y;else shut=y;}
            return shut;
        }

        [Test]public void TheOpeningIsACurvedAlmondWithTaperedCorners()
        {
            foreach(var substance in new[]{"ketamine","weed"})foreach(float aperture in new[]{.55f,.7f,.85f})
            {
                float soft=FestivalTrip.EyeSoftness(substance);string at=" at aperture "+aperture+" for "+substance;
                Assert.That(FestivalTrip.EyeShade(0,0,aperture,soft),Is.Zero,"the centre is clear"+at);
                Assert.That(FestivalTrip.EyeShade(0,.98f,aperture,soft),Is.EqualTo(1),"the top is shut"+at);
                Assert.That(FestivalTrip.EyeShade(0,-.98f,aperture,soft),Is.EqualTo(1),"and the bottom"+at);
                foreach(float side in new[]{-1f,1f})Assert.That(FestivalTrip.EyeShade(side,0,aperture,soft),Is.EqualTo(1),"the lids meet in a corner at each side"+at);
                // Couch Lock's narrower soft edge reaches the middle row only nearer the corner.
                float corner=substance=="weed"?.95f:.9f;
                Assert.That(FestivalTrip.EyeShade(corner,0,aperture,soft),Is.GreaterThan(FestivalTrip.EyeShade(.3f,0,aperture,soft)),"the corners close in more than the middle"+at);
                float previous=Edge(0,aperture,soft);
                Assert.That(previous,Is.GreaterThan(.4f*aperture),"the opening stands tall in the middle"+at);
                for(float x=.1f;x<.95f;x+=.1f){float edge=Edge(x,aperture,soft);Assert.That(edge,Is.LessThan(previous),"the lids curve in towards the corners at x="+x+at);previous=edge;}
                Assert.That(Edge(0,aperture,soft)-Edge(.6f,aperture,soft),Is.GreaterThan(.2f*aperture),"a curve, never a straight bar"+at);
                for(float x=-.9f;x<=.9f;x+=.3f)for(float y=0;y<=1;y+=.05f)
                {
                    float shade=FestivalTrip.EyeShade(x,y,aperture,soft);
                    Assert.That(shade,Is.InRange(0f,1f));
                    Assert.That(FestivalTrip.EyeShade(-x,-y,aperture,soft),Is.EqualTo(shade).Within(1e-6f),"mirrored left to right and top to bottom");
                    Assert.That(FestivalTrip.EyeShade(x,y+.05f,aperture,soft),Is.GreaterThanOrEqualTo(shade),"darker further from the middle");
                }
            }
            Assert.That(Edge(0,.85f,FestivalTrip.EyeSoftness("ketamine")),Is.GreaterThan(Edge(0,.55f,FestivalTrip.EyeSoftness("ketamine"))),"a wider aperture opens the eye further");
            Assert.That(FestivalTrip.EyeSoftness("weed"),Is.LessThan(FestivalTrip.EyeSoftness("ketamine")).And.GreaterThan(0),"Couch Lock's lids have the narrower soft edge");
            Assert.That(FestivalTrip.EyeShade(0,0,0),Is.EqualTo(1),"shut eyes see nothing");
        }

        [Test]public void TheEyesRestMoreClosedWithEveryDose()
        {
            foreach(var substance in new[]{"ketamine","weed"})
            {
                Assert.That(FestivalTrip.EyeRest(substance,0),Is.Zero,"sober eyes are open");
                Assert.That(FestivalTrip.EyeRest(substance,-1),Is.Zero);
                Assert.That(FestivalTrip.EyeRest(substance,1),Is.GreaterThan(.1f),"one dose of "+substance+" is clearly sleepy");
                for(int dose=2;dose<=4;dose++)Assert.That(FestivalTrip.EyeRest(substance,dose),Is.GreaterThan(FestivalTrip.EyeRest(substance,dose-1)),substance+" dose "+dose+" rests more closed than "+(dose-1));
                Assert.That(1-FestivalTrip.EyeRest(substance,4),Is.GreaterThanOrEqualTo(.5f),"four doses still leave the middle of the view open");
            }
            for(int dose=1;dose<=4;dose++)Assert.That(FestivalTrip.EyeRest("weed",dose),Is.LessThan(FestivalTrip.EyeRest("ketamine",dose)),"Couch Lock rests lighter at dose "+dose);
            foreach(var substance in new[]{"lsd","mushrooms","ecstasy",""})Assert.That(FestivalTrip.EyeRest(substance,4),Is.Zero,"'"+substance+"' keeps the eyes open");
        }

        [Test]public void ABlinkClosesHoldsAndReopensInExactlyOneSecond()
        {
            Assert.That(FestivalTrip.BlinkSeconds,Is.EqualTo(1));
            Assert.That(FestivalTrip.Blink(0),Is.Zero,"open as it starts");
            Assert.That(FestivalTrip.Blink(.2f),Is.EqualTo(.5f).Within(1e-4f),"half shut halfway through the .4 s close");
            Assert.That(FestivalTrip.Blink(.4f),Is.EqualTo(1),"shut at .4 s");
            Assert.That(FestivalTrip.Blink(.45f),Is.EqualTo(1),"held shut");
            Assert.That(FestivalTrip.Blink(.5f),Is.EqualTo(1),"to .5 s");
            Assert.That(FestivalTrip.Blink(.75f),Is.EqualTo(.5f).Within(1e-4f),"half open halfway through the .5 s reopening");
            Assert.That(FestivalTrip.Blink(1),Is.Zero,"open again at exactly 1 s");
            Assert.That(FestivalTrip.Blink(-.1f),Is.Zero);Assert.That(FestivalTrip.Blink(1.1f),Is.Zero,"open outside the blink");
            float previous=0;
            for(float t=.001f;t<1;t+=.001f)
            {
                float now=FestivalTrip.Blink(t);
                if(t<.4f)Assert.That(now,Is.GreaterThanOrEqualTo(previous),"closing at "+t);else if(t>.5f)Assert.That(now,Is.LessThanOrEqualTo(previous),"opening at "+t);
                Assert.That(Mathf.Abs(now-previous),Is.LessThan(.005f),"smooth at "+t);
                previous=now;
            }
            foreach(float join in new[]{0f,.4f,.5f,1f})
                Assert.That(Mathf.Abs(FestivalTrip.Blink(join+.002f)-FestivalTrip.Blink(join-.002f)),Is.LessThan(1e-4f),"at rest across the join at "+join+" s");
        }

        [Test]public void ABlinkStartsAndEndsAtTheRestingOpeningAndReducedMotionNeverShutsIt()
        {
            foreach(var substance in new[]{"ketamine","weed"})for(int dose=1;dose<=4;dose++)
            {
                float rest=FestivalTrip.EyeRest(substance,dose),open=1-rest;
                Assert.That(FestivalTrip.Aperture(rest,FestivalTrip.Blink(0),false),Is.EqualTo(open),"dose "+dose+" starts from its resting opening");
                Assert.That(FestivalTrip.Aperture(rest,FestivalTrip.Blink(1),false),Is.EqualTo(open),"and comes back to it");
                Assert.That(FestivalTrip.Aperture(rest,1,false),Is.Zero,"a full blink shuts the eyes");
                float calm=FestivalTrip.Aperture(rest,1,true);
                Assert.That((open-calm)/open,Is.EqualTo(FestivalTrip.ReducedMotionShare).Within(1e-5f),"reduced motion moves the lids a quarter of the way");
                Assert.That(calm,Is.GreaterThan(.5f*open),"and never shuts the world out");
                Assert.That(FestivalTrip.Aperture(rest,0,true),Is.EqualTo(open),"while resting at the same opening");
            }
        }

        // An injected list of waits, counting each draw.
        private sealed class Waits
        {
            private readonly Queue<float> left;public int Drawn;
            public Waits(params float[] waits){left=new Queue<float>(waits);}
            public float Next(){Drawn++;return left.Count>1?left.Dequeue():left.Peek();}
        }
        // When each blink began, advancing blinker for seconds by frames of the given lengths in turn.
        private static List<float> Starts(FestivalTrip.Blinker blinker,float seconds,params float[] frames)
        {
            var starts=new List<float>();double time=0;float previous=0;
            for(int i=0;time<seconds;i++){float frame=frames[i%frames.Length];time+=frame;float now=blinker.Advance(frame,false);if(previous==0&&now>0)starts.Add((float)time);previous=now;}
            return starts;
        }

        [Test]public void BlinksComeAfterEachDrawnWaitAndNeverOverlap()
        {
            var waits=new Waits(8,13.5f,18,9.25f);var blinker=new FestivalTrip.Blinker(waits.Next);
            var starts=Starts(blinker,60,1/60f);
            Assert.That(starts.Count,Is.EqualTo(4),"four blinks in a minute of these waits");
            var expected=new[]{8f,8+1+13.5f,8+1+13.5f+1+18,8+1+13.5f+1+18+1+9.25f};
            for(int i=0;i<4;i++)Assert.That(starts[i],Is.EqualTo(expected[i]).Within(.02f),"blink "+i+" starts after its own wait, once the last has reopened");
            Assert.That(waits.Drawn,Is.EqualTo(5),"one wait drawn at the start and one after each reopening: never per frame");
            var seeded=FestivalTrip.Blinker.Seeded(7);float least=float.MaxValue,most=float.MinValue;var seen=new HashSet<float>();
            var gaps=Starts(seeded,2000,1/60f);
            for(int i=1;i<gaps.Count;i++){float wait=gaps[i]-gaps[i-1]-FestivalTrip.BlinkSeconds;least=Mathf.Min(least,wait);most=Mathf.Max(most,wait);seen.Add(Mathf.Round(wait));}
            Assert.That(least,Is.GreaterThanOrEqualTo(FestivalTrip.Blinker.MinWait-.02f),"never sooner than 8 s");
            Assert.That(most,Is.LessThanOrEqualTo(FestivalTrip.Blinker.MaxWait+.02f),"never later than 18 s");
            Assert.That(most-least,Is.GreaterThan(5),"irregular across the range");
            Assert.That(seen.Count,Is.GreaterThan(5),"and not a fixed rhythm");
        }

        [Test]public void FrameRateAndLongFramesDoNotChangeTheBlinkOrStickItShut()
        {
            var smooth=new FestivalTrip.Blinker(new Waits(8,11,9).Next);var choppy=new FestivalTrip.Blinker(new Waits(8,11,9).Next);
            Assert.That(Starts(smooth,40,1/144f),Is.EqualTo(Starts(choppy,40,1/20f,1/45f,1/30f)).Within(.06f),"the same blinks at 144 fps and at an uneven 20-45 fps");
            var a=new FestivalTrip.Blinker(new Waits(8).Next);var b=new FestivalTrip.Blinker(new Waits(8).Next);
            for(int i=0;i<522;i++)a.Advance(1/60f,false);float whole=b.Advance(8.7f,false);
            Assert.That(whole,Is.GreaterThan(.5f).And.EqualTo(a.Advance(0,false)).Within(1e-3f),"one 8.7 s frame lands mid-blink where 522 small ones do");
            var hitch=new FestivalTrip.Blinker(new Waits(8).Next);hitch.Advance(8.45f,false);
            Assert.That(hitch.Advance(0,false),Is.EqualTo(1),"setup: shut mid-blink");
            hitch.Advance(1000,false);bool opened=false;
            for(float t=0;t<FestivalTrip.Blinker.MaxWait+FestivalTrip.BlinkSeconds;t+=1/60f)opened|=hitch.Advance(1/60f,false)==0;
            Assert.That(opened,"a 1000 s hitch never leaves the eyes stuck shut");
            Assert.That(new FestivalTrip.Blinker(new Waits(8).Next).Advance(float.NaN,false),Is.Zero,"a broken frame time changes nothing");
        }

        [Test]public void AProtectedMomentEasesAClosingBlinkOpenAndWaitsAFreshIntervalAfter()
        {
            var waits=new Waits(8,10,12);var blinker=new FestivalTrip.Blinker(waits.Next);
            blinker.Advance(8.2f,false);float closing=blinker.Advance(0,false);
            Assert.That(closing,Is.EqualTo(.5f).Within(1e-3f),"setup: half shut, still closing");
            float previous=closing,time=0;
            for(;time<30;time+=1/60f)
            {
                float now=blinker.Advance(1/60f,true);
                Assert.That(now,Is.LessThanOrEqualTo(previous+1e-5f),"held: the lids only open, at "+time);
                Assert.That(previous-now,Is.LessThan(.052f),"smoothly from where they were, no faster than the reopening's own ease, at "+time);
                if(time>=.5f)Assert.That(now,Is.Zero,"open within half a second, and no blink while held");
                previous=now;
            }
            Assert.That(waits.Drawn,Is.EqualTo(1),"a held moment draws no wait");
            var starts=Starts(blinker,30,1/60f);
            Assert.That(waits.Drawn,Is.GreaterThanOrEqualTo(2),"letting go draws a fresh wait");
            Assert.That(starts[0],Is.EqualTo(10).Within(.02f),"and the next blink comes that fresh wait after play resumes");
            var held=new FestivalTrip.Blinker(new Waits(8,10).Next);held.Advance(5,false);held.Advance(20,true);
            Assert.That(Starts(held,20,1/60f)[0],Is.EqualTo(10).Within(.02f),"a hold before any blink also starts a fresh wait on return");
        }
    }
}
