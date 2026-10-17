using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // CROWD-7: the main stage's crew dance on the stage song's beat, each with its own step, style and clock. The MC pumps a fist and
    // points at the crowd, the photographer turns between the crowd and the DJ, and the crew are culled like the ambient crowd.
    public sealed class StageCrewTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        private static readonly MethodInfo Animate=typeof(FestivalCharacter).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        [Test]public void TheCrewBeatFollowsTheSongsPhaseOnlyWhileTheSongPlays()
        {
            Assert.That(FestivalStageCrew.Follow(10.2,.05,null,1),Is.EqualTo(10.25).Within(1e-9),"with no song playing the crew keep their own pace");
            Assert.That(FestivalStageCrew.Follow(10.2,0,.7f,0),Is.EqualTo(10.2).Within(1e-9),"a held world holds the beat");
            Assert.That(FestivalStageCrew.Follow(10.2,.05,.3f,1),Is.EqualTo(10.3).Within(1e-5),"locked, the crew's beat lands with the song's");
            Assert.That(FestivalStageCrew.Follow(10.9,0,.05f,1),Is.EqualTo(11.05).Within(1e-5),"the nearer way round, forward over a beat");
            Assert.That(FestivalStageCrew.Follow(10.1,0,.95f,1),Is.EqualTo(9.95).Within(1e-5),"and back");
            Assert.That(FestivalStageCrew.Follow(10.2,0,.4f,.25f),Is.EqualTo(10.25).Within(1e-5),"a partial pull moves part of the way, so a coarse audio clock never jerks them");
            for(int i=0;i<200;i++)
            {
                double beats=i*.37;float phase=i*.13f%1;double locked=FestivalStageCrew.Follow(beats,0,phase,1);
                Assert.That(System.Math.Abs(locked-beats),Is.LessThanOrEqualTo(.5+1e-6),"never more than half a beat at "+beats);
                Assert.That(Mathf.Abs(Mathf.Repeat((float)(locked-System.Math.Floor(locked))-phase+.5f,1)-.5f),Is.LessThan(1e-4f),"on the song's phase at "+beats);
            }
        }

        [Test]public void EveryStageSongGetsADancersPaceOnItsBeat()
        {
            var seen=new HashSet<string>();
            for(int festival=0;festival<Festivals.Count;festival++)for(int level=0;level<Festivals.LevelCount;level++)
            {
                var track=FestivalStageAudio.TrackFor(festival,level);if(!seen.Add(track.Clip))continue;
                double bpm=track.Beats.Bpm;int steps=FestivalStageCrew.StepsPerBeat(bpm);
                Assert.That(steps,Is.EqualTo(1).Or.EqualTo(2),track.Clip+" takes a whole number of steps a beat, so feet land on it");
                Assert.That(steps*bpm/60,Is.InRange(2.0,3.0),track.Clip+" at "+bpm+" BPM keeps a dancer's pace");
            }
            Assert.That(seen.Count,Is.EqualTo(4),"setup: both festivals' day and night songs");
            Assert.That(FestivalStageCrew.StepsPerBeat(83.47),Is.EqualTo(2),"Ember Playa's one-drop dub gets a step on its skank too");
        }

        [Test]public void ThePhotographerShootsTheCrowdForABarThenTheDjForABar()
        {
            foreach(double bar in new[]{0.0,8,800})
            {
                for(double b=0;b<=3;b+=.25)Assert.That(FestivalStageCrew.Turn(bar+b),Is.EqualTo(0).Within(1e-5),"shooting the crowd at beat "+(bar+b));
                for(double b=4;b<=7;b+=.25)Assert.That(FestivalStageCrew.Turn(bar+b),Is.EqualTo(1).Within(1e-5),"shooting the DJ at beat "+(bar+b));
            }
            float last=FestivalStageCrew.Turn(0);
            for(double b=0;b<=16;b+=1/64.0)
            {
                float turn=FestivalStageCrew.Turn(b);
                Assert.That(turn,Is.InRange(0f,1f));Assert.That(Mathf.Abs(turn-last),Is.LessThan(.05f),"a smooth turn at beat "+b);last=turn;
            }
        }

        [Test]public void TheCrewCullLikeTheAmbientCrowdWithRoomToTurn()
        {
            var ahead=Vector3.forward;
            Assert.That(FestivalStageCrew.Visible(false,new Vector3(0,0,30),ahead),Is.True,"on the stage ahead");
            Assert.That(FestivalStageCrew.Visible(false,new Vector3(0,0,51),ahead),Is.False,"too far to wake");
            Assert.That(FestivalStageCrew.Visible(true,new Vector3(0,0,51),ahead),Is.True,"once shown, kept a little further");
            Assert.That(FestivalStageCrew.Visible(true,new Vector3(0,0,54),ahead),Is.False,"then culled");
            Assert.That(FestivalStageCrew.Visible(true,new Vector3(0,0,-20),ahead),Is.False,"behind the view");
            Assert.That(FestivalStageCrew.Visible(true,new Vector3(20,0,-3),ahead),Is.True,"just past the side, a shown performer stays while the view turns");
            Assert.That(FestivalStageCrew.Visible(false,new Vector3(20,0,-3),ahead),Is.False,"and a hidden one wakes only once in front");
            Assert.That(FestivalStageCrew.Visible(false,new Vector3(0,0,-7),ahead),Is.True,"close by, even behind");
        }

        // The stage beat sets the dance and the planted feet: two dancers alike in all but their own clock strike the same pose on
        // the same beat, and a step later is another pose.
        [Test]public void TheStageBeatDrivesTheDanceAndTheFeetWhateverTheDancersOwnClock()
        {
            var a=Actor("stage_crew_hype_west","Dance",new Vector3(-2,0,0));var b=Actor("stage_crew_hype_west","Dance",new Vector3(2,0,0));
            var later=Actor("stage_crew_hype_west","Dance",new Vector3(6,0,0));
            for(int frame=0;frame<150;frame++)
            {
                float beat=frame*.036f;
                Step(a,beat,frame/60f,1/60f);Step(b,beat,500+frame/60f,1/60f);Step(later,beat+1,frame/60f,1/60f);
            }
            foreach(var bone in FestivalMotionLibrary.Bones)
            {
                Assert.That(Quaternion.Angle(Bone(a,bone).localRotation,Bone(b,bone).localRotation),Is.LessThan(.05f),bone+" follows the beat, not the clock");
                Assert.That(Vector3.Distance(a.transform.InverseTransformPoint(Bone(a,bone).position),b.transform.InverseTransformPoint(Bone(b,bone).position)),Is.LessThan(.001f),bone+" stands where the beat puts it");
            }
            float most=0;foreach(var bone in FestivalMotionLibrary.Bones)most=Mathf.Max(most,Quaternion.Angle(Bone(a,bone).localRotation,Bone(later,bone).localRotation));
            Assert.That(most,Is.GreaterThan(5),"a step later is another pose");
        }

        [Test]public void TheMcPumpsAFistOnEachBeatAndEveryOtherPhrasePointsAtTheCrowd()
        {
            var mc=Actor("stage_crew_mc",FestivalCharacter.McPose,Vector3.zero);var head=Bone(mc,"Head");
            var hands=new[]{Bone(mc,"HandL"),Bone(mc,"HandR")};var elbows=new[]{Bone(mc,"ForearmL"),Bone(mc,"ForearmR")};var shoulders=new[]{Bone(mc,"ArmL"),Bone(mc,"ArmR")};
            var onBeat=new float[2];var offBeat=new float[2];var beats=new int[2];var offs=new int[2];int pointing=0,notPointing=0;
            for(int frame=0;frame<1000;frame++)
            {
                float beat=frame/60f*2.16f;Step(mc,beat,frame/60f,1/60f);if(frame<60)continue;
                float nearest=Mathf.Abs(beat-Mathf.Round(beat));bool any=false;
                for(int side=0;side<2;side++)
                {
                    float above=hands[side].position.y-head.position.y;
                    if(nearest<.06f){onBeat[side]+=above;beats[side]++;}else if(nearest>.44f){offBeat[side]+=above;offs[side]++;}
                    float arm=Vector3.Distance(shoulders[side].position,elbows[side].position)+Vector3.Distance(elbows[side].position,hands[side].position);
                    var reach=hands[side].position-shoulders[side].position;
                    if(Vector3.Dot(reach,mc.transform.forward)>.8f*arm&&Mathf.Abs(reach.y)<.35f*arm)any=true;
                }
                if(any)pointing++;else notPointing++;
            }
            int pump=onBeat[1]/beats[1]-offBeat[1]/offs[1]>onBeat[0]/beats[0]-offBeat[0]/offs[0]?1:0;
            Assert.That(onBeat[pump]/beats[pump],Is.GreaterThan(.05f),"on the beat a fist is up over the head");
            Assert.That(onBeat[pump]/beats[pump]-offBeat[pump]/offs[pump],Is.GreaterThan(.08f),"and it pumps: lower between beats");
            Assert.That(pointing,Is.GreaterThan(120),"for whole phrases an arm points straight out at the crowd ahead");
            Assert.That(notPointing,Is.GreaterThan(120),"and for others it doesn't");
        }

        private FestivalCharacter Actor(string name,string pose,Vector3 at)
        {
            var parent=new GameObject("impl:U68 "+name);made.Add(parent);parent.transform.position=at;
            var actor=FestivalCharacter.Create(parent.transform,name,Color.white);actor.Pose=pose;return actor;
        }
        private static void Step(FestivalCharacter actor,float beat,float seconds,float dt){actor.DanceBeat=beat;actor.SetAnimationClock(seconds,dt);Animate.Invoke(actor,null);}
        private static Transform Bone(FestivalCharacter actor,string name)=>System.Array.Find(actor.GetComponentsInChildren<Transform>(),t=>t.name==name);
    }
}
