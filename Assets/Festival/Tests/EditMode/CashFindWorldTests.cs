using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace Festival.Tests
{
    // ECON-8: a random cash find glints so it catches the eye from about 15 m: twinkling, or steady with reduced motion. The glints are
    // a reused pool that never collides and is put away whenever the view has no find to show.
    public sealed class CashFindWorldTests
    {
        readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        FestivalCashGlint Glint()
        {
            var root=new GameObject("Cash glint test");made.Add(root);var glint=root.AddComponent<FestivalCashGlint>();
            typeof(FestivalCashGlint).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(glint,null);return glint;
        }
        static RoundState Level(params DropState[] drops){var state=new RoundState{Phase="Playing",RoundId="glint"};state.Drops.AddRange(drops);return state;}
        static DropState Find(float x,float z,int amount=4)=>new DropState{Id="cash_"+x+"_"+z,ItemId="cash",Count=amount,X=x,Z=z,CashFind=true};
        static List<Transform> Shown(FestivalCashGlint glint)=>glint.GetComponentsInChildren<Transform>(false).Where(t=>t.name==FestivalCashGlint.GlintName).ToList();
        static readonly Vector3 Eye=new Vector3(0,1.65f,-10);

        [Test]public void EachFindWithinFifteenMetresGlintsAndNothingElseDoes()
        {
            var glint=Glint();
            var state=Level(Find(0,0),new DropState{Id="death_cash",ItemId="cash",Count=40,X=3,Z=0},new DropState{Id="gear",ItemId="confetti",Count=1,X=-3,Z=0},Find(0,25));
            Assert.That(glint.Follow(state,Eye,0,false),Is.EqualTo(1),"the near find glints; a death's pile, gear and a find 35 m off do not");
            var shown=Shown(glint);Assert.That(shown,Has.Count.EqualTo(1));
            Assert.That(new Vector2(shown[0].position.x,shown[0].position.z),Is.EqualTo(Vector2.zero),"over the pile");
            Assert.That(shown[0].position.y,Is.InRange(.4f,1f),"just above it, where a pile on the grass catches the light");
            Assert.That(glint.Follow(state,new Vector3(0,1.65f,-14.9f),0,false),Is.EqualTo(1),"seen from 15 m");
            Assert.That(glint.Follow(state,new Vector3(0,1.65f,-15.5f),0,false),Is.Zero,"not from further");
            Assert.That(glint.Follow(state,new Vector3(0,1.65f,12.5f),0,false),Is.EqualTo(2),"each find in reach glints");
            Assert.That(glint.GetComponentsInChildren<Collider>(true),Is.Empty,"it never blocks a step or a sight line");
            foreach(var renderer in glint.GetComponentsInChildren<Renderer>(true))Assert.That(renderer.sharedMaterial,Is.Not.Null,renderer.name+" has a material");
            var facing=Shown(glint)[0];Assert.That(Vector3.Dot(facing.forward,(facing.position-new Vector3(0,1.65f,12.5f)).normalized),Is.GreaterThan(.99f),"it faces the eye");
        }

        [Test]public void TheGlintTwinklesAndHoldsStillWithReducedMotion()
        {
            var glint=Glint();var state=Level(Find(2,3));
            Assert.That(glint.Follow(state,Eye,0,false),Is.EqualTo(1),"setup: the find glints");
            var sizes=new List<float>();var turns=new List<Quaternion>();
            for(int i=0;i<8;i++){glint.Follow(state,Eye,i*.1f,false);var g=Shown(glint)[0];sizes.Add(g.localScale.x);turns.Add(g.rotation);}
            Assert.That(sizes.Max()-sizes.Min(),Is.GreaterThan(.1f),"it twinkles");
            Assert.That(turns.Distinct().Count(),Is.GreaterThan(4),"and turns");
            sizes.Clear();turns.Clear();
            for(int i=0;i<8;i++){glint.Follow(state,Eye,i*.1f,true);var g=Shown(glint)[0];sizes.Add(g.localScale.x);turns.Add(g.rotation);}
            Assert.That(sizes.Distinct().Count(),Is.EqualTo(1),"steady with reduced motion");Assert.That(turns.Distinct().Count(),Is.EqualTo(1),"and still");
            Assert.That(sizes[0],Is.GreaterThan(.3f),"and still plainly there");
        }

        [Test]public void TheGlintsAreReusedAndPutAwayOnTheEndALeaveAndANewRound()
        {
            var glint=Glint();var state=Level(Find(0,0),Find(4,0));
            glint.Follow(state,Eye,0,false);var first=Shown(glint);int parts=glint.GetComponentsInChildren<Transform>(true).Length;
            glint.Follow(state,Eye,1,false);
            Assert.That(Shown(glint),Is.EqualTo(first),"the same glints, frame to frame");
            state.Phase="Results";Assert.That(glint.Follow(state,Eye,2,false),Is.Zero);Assert.That(Shown(glint),Is.Empty,"the level's end puts them away");
            Assert.That(glint.Follow(null,Eye,3,false),Is.Zero);Assert.That(Shown(glint),Is.Empty,"so does leaving the session");
            var spirit=Level();Assert.That(glint.Follow(spirit,Eye,4,false),Is.Zero,"a spirit's view has no drops");
            var next=Level(Find(-4,2));next.RoundId="next";
            Assert.That(glint.Follow(next,Eye,5,false),Is.EqualTo(1),"a new round's find glints");
            Assert.That(glint.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(parts),"from the same pool");
            typeof(FestivalCashGlint).GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(glint,null);
            Assert.That(glint.GetComponentsInChildren<Transform>(true).Count(t=>t.name==FestivalCashGlint.GlintName),Is.Zero,"and disposed with the session");
        }

        // From the paths around it, a glint on either festival's grounds is in plain sight across 15 m of open ground.
        [TestCase(0)][TestCase(1)]public void EveryFindSpotGlintsInSightOfThePathsAround(int festival)
        {
            var root=new GameObject("Cash glint sight");made.Add(root);
            var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");world.SetTwists(new RoundState{Phase="Playing",FestivalIndex=festival});Physics.SyncTransforms();
            Assert.That(world.NavigationReady,Is.True);
            var glint=Glint();
            foreach(var spot in Festivals.CashFindSpots[festival])
            {
                var state=Level(Find(spot.X,spot.Z));int clear=0;
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI/8;var stand=new Vector3(spot.X+Mathf.Sin(angle)*12,0,spot.Z+Mathf.Cos(angle)*12);
                    if(!NavMesh.SamplePosition(stand,out var ground,.5f,NavMesh.AllAreas))continue;
                    var eye=ground.position+Vector3.up*1.65f;
                    Assert.That(glint.Follow(state,eye,0,false),Is.EqualTo(1),"spot "+spot.X+", "+spot.Z+": 12 m off, the glint shows");
                    if(!Physics.Linecast(eye,Shown(glint)[0].position,~0,QueryTriggerInteraction.Ignore))clear++;
                }
                Assert.That(clear,Is.GreaterThanOrEqualTo(3),"festival "+festival+" spot "+spot.X+", "+spot.Z+": seen 12 m off from at least three ways round, not hidden behind scenery");
            }
        }
    }
}
