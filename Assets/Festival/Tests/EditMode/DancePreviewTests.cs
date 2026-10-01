using System.Collections.Generic;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // DANCE-4: the live dancer camera moves with the music. Its path is a pure function of how far into the challenge it is, the
    // beat and reduced motion: a slow orbit of ±20° around the dancer once every 16 beats, plus a 5% push in on each beat that
    // eases back; with reduced motion, a ±8° drift and no push. The DJ deck framing holds still, and walls still pull the camera in.
    public sealed class DancePreviewTests
    {
        // A dance or chat's beat (.5 s), poi and the DJ deck's (.4 s), and a slower one.
        private static readonly double[] Beats={.5,.4,.6};
        // Far from anything a scene might hold, so only a test's own wall is ever in the way.
        private static readonly Vector3 Spot=new Vector3(200,100,200);
        private readonly List<GameObject> made=new List<GameObject>();
        private Transform dancer,camera;
        [SetUp]public void Build(){dancer=Make("Dancer").transform;dancer.position=Spot;camera=Make("Dance camera").transform;}
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}
        private GameObject Make(string name){var go=new GameObject(name);made.Add(go);return go;}

        private static (float Yaw,float Push) Path(double seconds,double beat,bool reduced)=>FestivalDancePreview.Path(seconds,beat,reduced);
        // Where the camera sits `seconds` into a challenge, framing the dancer standing at Spot facing north.
        private Vector3 Camera(double seconds,double beat,bool reduced,bool atDeck=false)
        {
            FestivalDancePreview.Frame(camera,dancer,atDeck,seconds,beat,reduced);
            return camera.position;
        }
        // From the count-in through three orbits, 1/64 of a beat apart.
        private static IEnumerable<double> Moments(double beat){for(int i=-128;i<=3*16*64;i++)yield return i*beat/64;}
        private static string Motion(bool reduced,double beat)=>(reduced?"reduced motion":"normally")+", a "+beat+" s beat";

        [Test]public void TheOrbitSwingsOutTo20DegreesOr8WithReducedMotion()
        {
            foreach(double beat in Beats)foreach(bool reduced in new[]{false,true})
            {
                float widest=0,limit=reduced?8:20;
                foreach(double t in Moments(beat))widest=Mathf.Max(widest,Mathf.Abs(Path(t,beat,reduced).Yaw));
                Assert.That(widest,Is.InRange(limit-.05f,limit+.0001f),Motion(reduced,beat)+": the camera swings out to ±"+limit+"° around the dancer, and no further");
            }
        }

        [Test]public void After16BeatsTheCameraIsBackWhereItStarted()
        {
            foreach(double beat in Beats)foreach(bool reduced in new[]{false,true})foreach(double start in new[]{.1,.37,1.3,5.1})
            {
                string where=Motion(reduced,beat)+", from "+start+" s";
                var from=Path(start,beat,reduced);var back=Path(start+16*beat,beat,reduced);
                float swung=0;
                for(int i=1;i<16*64;i++)swung=Mathf.Max(swung,Mathf.Abs(Path(start+i*beat/64,beat,reduced).Yaw-from.Yaw));
                Assert.That(swung,Is.GreaterThan(reduced?7.9f:19.9f),where+": the camera goes round the dancer in those 16 beats");
                Assert.That(back.Yaw,Is.EqualTo(from.Yaw).Within(.001f),where+": the orbit is back where it started");
                Assert.That(back.Push,Is.EqualTo(from.Push).Within(.00001f),where+": so is the push");
                var first=Camera(start,beat,reduced);
                Assert.That(Vector3.Distance(Camera(start+16*beat,beat,reduced),first),Is.LessThan(.001f),where+": and so is the camera");
            }
        }

        [Test]public void TheBeatPushPeaksOnTheBeat()
        {
            foreach(double beat in Beats)
            {
                const float Peak=.05f;
                float most=0;
                foreach(double t in Moments(beat))most=Mathf.Max(most,Path(t,beat,false).Push);
                Assert.That(most,Is.GreaterThan(0).And.LessThanOrEqualTo(Peak),"a "+beat+" s beat: the camera pushes in, never more than 5% of the way to the dancer");
                // The chart's quarter notes fall on the beats. Each is read a hair after its time: the push restarts on the beat.
                foreach(var note in RhythmChart.Create(7,16,beat,RhythmChart.QuarterNotes).Notes)
                {
                    double on=note.TimeSeconds+1e-6;string where="a "+beat+" s beat, the note at "+note.TimeSeconds.ToString("0.00")+" s";
                    Assert.That(Path(on,beat,false).Push,Is.EqualTo(Peak).Within(.00001f),where+": the push peaks, at 5%, on the beat");
                    Assert.That(Path(on-beat/16,beat,false).Push,Is.LessThan(Peak/5),where+": having eased back just before it");
                    float before=Path(on,beat,false).Push;
                    for(int s=1;s<16;s++)
                    {
                        float push=Path(on+s*beat/16,beat,false).Push;
                        Assert.That(push,Is.LessThan(before),where+": it eases back through the beat ("+s+"/16)");before=push;
                    }
                    // The push is a dolly: the camera itself is nearest the dancer on the beat.
                    float nearest=Vector3.Distance(Camera(on,beat,false),Spot);
                    for(int s=-1;s<16;s++)if(s!=0)Assert.That(Vector3.Distance(Camera(on+s*beat/16,beat,false),Spot),Is.GreaterThan(nearest),where+": the camera is nearest the dancer on the beat, not at "+s+"/16");
                }
            }
        }

        [Test]public void ReducedMotionDriftsButNeverPushes()
        {
            foreach(double beat in Beats)
            {
                string where=Motion(true,beat);
                float nearest=float.MaxValue,farthest=0;
                foreach(double t in Moments(beat))
                {
                    Assert.That(Path(t,beat,true).Push,Is.EqualTo(0),where+": no push at "+t+" s");
                    float distance=Vector3.Distance(Camera(t,beat,true),Spot);nearest=Mathf.Min(nearest,distance);farthest=Mathf.Max(farthest,distance);
                }
                Assert.That(farthest-nearest,Is.LessThan(.001f),where+": the camera keeps its distance from the dancer");
                Assert.That(Vector3.Distance(Camera(4*beat,beat,true),Camera(0,beat,true)),Is.GreaterThan(.1f),where+": while it drifts around them");
            }
        }

        [Test]public void AWallTheOrbitSwingsBehindStillPullsTheCameraIn()
        {
            const double Beat=.5;
            double widest=0;
            for(int i=0;i<16*64;i++){double t=i*Beat/64;if(Mathf.Abs(Path(t,Beat,false).Yaw)>Mathf.Abs(Path(widest,Beat,false).Yaw))widest=t;}
            Assert.That(Mathf.Abs(Path(widest,Beat,false).Yaw),Is.GreaterThan(10),"the orbit swings the camera out");
            Vector3 clear=Camera(0,Beat,false),swung=Camera(widest,Beat,false);
            // A wall halfway along the line to where the orbit swings the camera, narrow enough to leave the starting line clear.
            Vector3 Flat(Vector3 v)=>new Vector3(v.x,0,v.z);
            var across=Flat(swung-Spot);
            var wall=Make("Wall");wall.transform.position=Spot+across/2+Vector3.up;wall.transform.rotation=Quaternion.LookRotation(across);
            wall.transform.localScale=new Vector3(.6f,4,.2f);wall.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            Assert.That(Vector3.Distance(Camera(0,Beat,false),clear),Is.LessThan(.001f),"the wall leaves the starting line clear");
            float wallFace=across.magnitude/2-.1f;
            for(int i=-8;i<=8;i++)
            {
                double t=widest+i*Beat/16;
                Assert.That(Flat(Camera(t,Beat,false)-Spot).magnitude,Is.LessThan(wallFace),"at "+t.ToString("0.000")+" s, swung behind the wall (push "+Path(t,Beat,false).Push.ToString("0.000")+"), the camera is pulled in to the dancer's side of it");
            }
        }

        [Test]public void TheDjDeckFramingHoldsStill()
        {
            const double Beat=.4;
            var still=Camera(0,Beat,false,atDeck:true);
            foreach(bool reduced in new[]{false,true})foreach(double t in Moments(Beat))
                Assert.That(Vector3.Distance(Camera(t,Beat,reduced,atDeck:true),still),Is.LessThan(.0001f),Motion(reduced,Beat)+": the DJ deck camera holds still at "+t+" s");
        }
    }
}
