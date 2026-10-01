using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Tests
{
    // LIGHT-2: day skies at the festival get 8-14 soft clouds drifting in one wind, and every 60-120 s one of them turns into a
    // funny shape for 15-20 s. Nights, dust storms and camp have none. The sky comes from the round's spin seed and clock alone,
    // so two players' games show the same clouds and the same shape at the same moment.
    public sealed class CloudTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        private readonly List<FestivalClouds> skies=new List<FestivalClouds>();
        [TearDown]public void Cleanup()
        {
            foreach(var sky in skies)sky.Dispose();skies.Clear();
            foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();
        }

        const int Spin=424242;

        [Test]public void ADayLevelHas8To14CloudsAndNightsAndCampHaveNone()
        {
            var clouds=Sky(out var root);var counts=new HashSet<int>();
            foreach(int festival in new[]{Festivals.PoloFestival,Festivals.PlayaFestival})
                for(int spin=1;spin<=30;spin++)
                {
                    // A calm moment, so Ember Playa's storms don't hide the sky.
                    var state=Calm(Round(festival,0,"Playing",spin));clouds.Apply(state,Vector3.zero);
                    int expected=CloudShapes.For(spin).Clouds.Length;counts.Add(expected);
                    Assert.That(Shown(root).Count,Is.EqualTo(expected),Festivals.Name(festival)+" spin "+spin+": every cloud of the level is in the sky");
                    Assert.That(expected,Is.InRange(8,14),"a day level has 8-14 clouds");
                    state.LevelIndex=2;clouds.Apply(state,Vector3.zero);
                    Assert.That(Shown(root).Count,Is.EqualTo(expected),"Day 2 has clouds too");
                    state.Phase="Results";clouds.Apply(state,Vector3.zero);
                    Assert.That(Shown(root).Count,Is.EqualTo(expected),"they stay up over the day's results");
                    foreach(int night in new[]{1,3})
                    {
                        state.Phase="Playing";state.LevelIndex=night;clouds.Apply(state,Vector3.zero);
                        Assert.That(Shown(root),Is.Empty,"no clouds on night "+(night/2+1));
                        Assert.That(FestivalClouds.Shows(state),Is.False,"night "+(night/2+1));
                    }
                    state.LevelIndex=0;
                    foreach(var camp in new[]{"Shopping","Spinning","Loading","CampReview"})
                    {
                        state.Phase=camp;clouds.Apply(state,Vector3.zero);
                        Assert.That(Shown(root),Is.Empty,"none at camp ("+camp+")");
                    }
                }
            Assert.That(counts.Count,Is.GreaterThan(3),"the count changes from level to level");
        }

        [Test]public void ADustStormHidesTheCloudsUntilItBlowsOver()
        {
            var clouds=Sky(out var root);var state=Round(Festivals.PlayaFestival,0,"Playing",Spin);
            double storm=-1,after=-1;
            for(double t=0;t<state.DurationSeconds&&after<0;t++){state.ElapsedSeconds=t;bool blowing=FestivalSimulation.DustStorm(state);if(blowing&&storm<0)storm=t;else if(!blowing&&storm>=0)after=t;}
            Assert.That(storm>0&&after>storm,Is.True,"setup: this level has a storm that blows over");
            state.ElapsedSeconds=storm-1;clouds.Apply(state,Vector3.zero);
            Assert.That(Shown(root),Is.Not.Empty,"clouds before the storm");
            state.ElapsedSeconds=storm;clouds.Apply(state,Vector3.zero);
            Assert.That(Shown(root),Is.Empty,"the storm hides them");
            Assert.That(FestivalClouds.Shows(state),Is.False,"no clouds in a storm");
            state.ElapsedSeconds=after;clouds.Apply(state,Vector3.zero);
            Assert.That(Shown(root).Count,Is.EqualTo(CloudShapes.For(Spin).Clouds.Length),"back once it blows over");
            var mirage=Round(Festivals.PoloFestival,0,"Playing",Spin);
            for(double t=0;t<mirage.DurationSeconds;t+=10){mirage.ElapsedSeconds=t;Assert.That(FestivalClouds.Shows(mirage),Is.True,"Palm Mirage has no dust: clouds all day, "+t+" s");}
        }

        [Test]public void CloudsDriftWithTheWindAndFadeWhereTheyWrap()
        {
            var clouds=Sky(out var root);var sky=CloudShapes.For(Spin);var state=Round(Festivals.PoloFestival,0,"Playing",Spin);
            var block=new MaterialPropertyBlock();int moved=0;
            foreach(double t in new[]{0,30,60,91.5,240,477})
            {
                state.ElapsedSeconds=t;clouds.Apply(state,Vector3.zero);
                for(int c=0;c<sky.Clouds.Length;c++)
                {
                    var cloud=Cloud(root,c);var at=sky.At(c,t);
                    Assert.That(Vector3.Distance(cloud.localPosition,new Vector3(at.X,at.Y,at.Z)),Is.LessThan(1e-3f),"cloud "+c+" is where the sky says at "+t+" s");
                    cloud.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                    Assert.That(block.GetColor("_Color").a,Is.EqualTo(FestivalClouds.Opacity*at.Alpha).Within(1e-4f),"cloud "+c+" fades as it nears the end of its lane");
                    if(t>0&&Vector3.Distance(cloud.localPosition,new Vector3(sky.At(c,0).X,sky.At(c,0).Y,sky.At(c,0).Z))>1)moved++;
                }
            }
            Assert.That(moved,Is.EqualTo(5*sky.Clouds.Length),"every cloud drifts away from where it started");
        }

        [Test]public void TheSkyStaysOverTheViewerAndEveryCloudFacesThem()
        {
            var clouds=Sky(out var root);var sky=CloudShapes.For(Spin);var state=Round(Festivals.PoloFestival,0,"Playing",Spin,95);
            var viewer=new Vector3(31,1.65f,-24);clouds.Apply(state,viewer);
            Assert.That(Vector2.Distance(new Vector2(root.position.x,root.position.z),new Vector2(viewer.x,viewer.z)),Is.LessThan(1e-4f),"the middle of the sky is under the viewer");
            Assert.That(root.position.y,Is.EqualTo(0).Within(1e-5f),"heights count from the ground");
            float reach=Mathf.Sqrt(CloudShapes.HalfWidth*CloudShapes.HalfWidth+CloudShapes.HalfHeight*CloudShapes.HalfHeight);int solid=0;
            for(int c=0;c<sky.Clouds.Length;c++)
            {
                var cloud=Cloud(root,c);var away=cloud.position-root.position;
                // Vector3.Angle resolves only to about .02 degrees near zero.
                Assert.That(Vector3.Angle(cloud.forward,away),Is.LessThan(.1f),"cloud "+c+" faces the viewer, so its shape reads the right way round from below");
                Assert.That(Mathf.Abs(cloud.right.y),Is.LessThan(1e-4f),"cloud "+c+" stands upright, not rolled");
                Assert.That(cloud.up.y,Is.GreaterThan(0),"cloud "+c+" is the right way up");
                // Kept over the viewer, a cloud fully in the sky is inside the camera's 130 m far clip from anywhere on the grounds.
                if(sky.At(c,95).Alpha<1)continue;solid++;
                Assert.That(Mathf.Sqrt(away.sqrMagnitude+reach*reach),Is.LessThan(130f),"cloud "+c+" is inside the far clip");
            }
            Assert.That(solid,Is.GreaterThan(0),"setup: some clouds are fully in the sky");
        }

        [Test]public void TwoPlayersSeeTheSameCloudsAndTheSameShape()
        {
            var mine=Sky(out var myRoot);var yours=Sky(out var yourRoot);var sky=CloudShapes.For(Spin);
            var show=sky.Shows[1];
            foreach(double t in new[]{12.5,show.Start+1,show.Start+CloudShapes.MorphSeconds+show.Hold/2,show.End-1,show.End+3})
            {
                var state=Round(Festivals.PoloFestival,0,"Playing",Spin,t);
                mine.Apply(state,new Vector3(-20,1.65f,10));yours.Apply(state,new Vector3(14,1.65f,-30));
                for(int c=0;c<sky.Clouds.Length;c++)
                {
                    Transform a=Cloud(myRoot,c),b=Cloud(yourRoot,c);
                    Assert.That(Vector3.Distance(a.localPosition,b.localPosition),Is.LessThan(1e-4f),"cloud "+c+" at "+t+" s is in the same part of both skies");
                    Assert.That(Quaternion.Angle(a.localRotation,b.localRotation),Is.LessThan(1e-3f),"cloud "+c+" at "+t+" s turns the same way");
                    Mesh ma=a.GetComponent<MeshFilter>().sharedMesh,mb=b.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(ma.vertices,Is.EqualTo(mb.vertices),"cloud "+c+" at "+t+" s has the same puffs");
                    Assert.That(ma.colors,Is.EqualTo(mb.colors),"cloud "+c+" at "+t+" s has the same colours");
                }
            }
            var mid=Round(Festivals.PoloFestival,0,"Playing",Spin,show.Start+CloudShapes.MorphSeconds+show.Hold/2);mine.Apply(mid,Vector3.zero);
            Assert.That(Match(PuffsOf(Cloud(myRoot,show.Cloud)),CloudShapes.Shapes[show.Shape].Puffs),Is.True,"setup: both see the "+CloudShapes.Shapes[show.Shape].Name);
        }

        // Watches a whole day level, a frame every half second, for clouds that aren't their ordinary selves.
        [Test]public void AShapeShowsEvery60To120SecondsAndHolds15To20()
        {
            foreach(int spin in new[]{Spin,7,-90210})
            {
                var clouds=Sky(out var root);var sky=CloudShapes.For(spin);var state=Round(Festivals.PoloFestival,0,"Playing",spin);
                var starts=new List<double>();var holds=new List<double>();var morphs=new List<double>();var names=new List<string>();
                double began=-1,fullFrom=-1,fullTo=-1;int lastShape=-1;
                for(double t=0;t<=Festivals.DaySeconds;t+=.5)
                {
                    state.ElapsedSeconds=t;clouds.Apply(state,Vector3.zero);
                    int odd=-1,full=-1;
                    for(int c=0;c<sky.Clouds.Length;c++)
                    {
                        var puffs=PuffsOf(Cloud(root,c));if(Match(puffs,sky.Clouds[c].Puffs))continue;
                        Assert.That(odd,Is.EqualTo(-1),"spin "+spin+" at "+t+" s: one shape at a time");odd=c;
                        for(int s=0;s<CloudShapes.Shapes.Length;s++)if(Match(puffs,CloudShapes.Shapes[s].Puffs))full=s;
                    }
                    if(odd>=0&&began<0){began=t;starts.Add(t);}
                    if(full>=0){if(fullFrom<0){fullFrom=t;morphs.Add(t-began);names.Add(CloudShapes.Shapes[full].Name);Assert.That(full,Is.Not.EqualTo(lastShape),"never the same shape twice running");lastShape=full;}fullTo=t;}
                    if(odd<0&&began>=0)
                    {
                        Assert.That(fullFrom,Is.GreaterThanOrEqualTo(0),"spin "+spin+": the cloud from "+began+" s took its full shape before melting back");
                        holds.Add(fullTo-fullFrom);Assert.That(t-fullTo,Is.InRange(3,5.5),"spin "+spin+": melts back over about 4 s");
                        began=fullFrom=fullTo=-1;
                    }
                }
                string story="spin "+spin+": shapes "+string.Join(", ",names)+" from "+string.Join(", ",starts)+" s, holding "+string.Join(", ",holds)+" s";
                Assert.That(starts.Count,Is.GreaterThanOrEqualTo(3),story);
                Assert.That(starts[0],Is.InRange(59.5,120.5),"the first shape comes 60-120 s in; "+story);
                for(int k=1;k<starts.Count;k++)Assert.That(starts[k]-starts[k-1],Is.InRange(59.5,120.5),"start to start every 60-120 s; "+story);
                foreach(double hold in holds)Assert.That(hold,Is.InRange(14.5,20.5),"each holds its shape 15-20 s; "+story);
                foreach(double morph in morphs)Assert.That(morph,Is.InRange(3,5.5),"each morphs in over about 4 s; "+story);
            }
        }

        [Test]public void EveryShapeIsDrawnWithinOneCloudsFootprint()
        {
            var clouds=Sky(out var root);var seen=new HashSet<int>();
            for(int spin=1;spin<=60&&seen.Count<CloudShapes.Shapes.Length;spin++)
            {
                var sky=CloudShapes.For(spin);
                foreach(var show in sky.Shows)
                {
                    clouds.Apply(Round(Festivals.PoloFestival,0,"Playing",spin,show.Start+CloudShapes.MorphSeconds+show.Hold/2),Vector3.zero);
                    var cloud=Cloud(root,show.Cloud);var shape=CloudShapes.Shapes[show.Shape];var puffs=PuffsOf(cloud);
                    Assert.That(puffs,Is.Not.Empty,shape.Name+" has puffs");
                    Assert.That(Match(puffs,shape.Puffs),Is.True,"cloud "+show.Cloud+" shows the "+shape.Name+" at spin "+spin);
                    var bounds=cloud.GetComponent<MeshFilter>().sharedMesh.bounds;
                    Assert.That(Mathf.Max(-bounds.min.x,bounds.max.x),Is.LessThanOrEqualTo(CloudShapes.HalfWidth+1e-3f),shape.Name+" fits a cloud's width");
                    Assert.That(Mathf.Max(-bounds.min.y,bounds.max.y),Is.LessThanOrEqualTo(CloudShapes.HalfHeight+1e-3f),shape.Name+" fits a cloud's height");
                    seen.Add(show.Shape);
                }
            }
            Assert.That(seen.Count,Is.EqualTo(CloudShapes.Shapes.Length),"every shape turns up in the sky");
        }

        [Test]public void CloudsAreSoftUnlitPuffsThatCastNoShadowsAndNeverCollide()
        {
            var clouds=Sky(out var root);clouds.Apply(Round(Festivals.PoloFestival,0,"Playing",Spin,30),Vector3.zero);
            var renderers=root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length,Is.EqualTo(CloudShapes.MaxClouds),"one renderer a cloud");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty,"clouds never block a sight line");
            foreach(var renderer in renderers)
            {
                Assert.That(renderer.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off),renderer.name+" casts no shadow");
                Assert.That(renderer.receiveShadows,Is.False,renderer.name+" takes no shadow");
                // The always-included built-in sprite shader: unlit, fog-free and present in player builds.
                Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("Sprites/Default"),renderer.name);
            }
            var puff=(Texture2D)renderers[0].sharedMaterial.mainTexture;
            Assert.That(puff,Is.Not.Null,"a puff texture made in code");
            Assert.That(puff.GetPixelBilinear(.5f,.5f).a,Is.GreaterThan(.9f),"a puff is solid in the middle");
            foreach(var edge in new[]{new Vector2(0,.5f),new Vector2(.5f,1),new Vector2(1,1),new Vector2(.15f,.15f)})
                Assert.That(puff.GetPixelBilinear(edge.x,edge.y).a,Is.LessThan(.02f),"and fades to nothing by its edge "+edge);
            var mesh=renderers[0].GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var colors=mesh.colors;int low=0,high=0;
            for(int i=0;i<vertices.Length;i++){if(vertices[i].y<vertices[low].y)low=i;if(vertices[i].y>vertices[high].y)high=i;}
            Assert.That(colors[low].b,Is.LessThan(colors[high].b-.2f),"warm, sunlit undersides ("+colors[low]+") under cream tops ("+colors[high]+")");
            Assert.That(colors[low].r,Is.GreaterThan(colors[low].g),"the underside is peach-orange like the low sun");
        }

        [Test]public void TheWorldHangsTheCloudsOverTheFestivalAndCleansUp()
        {
            var root=Made("Cloudy world");var world=root.AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var festival=root.transform.Find(FestivalWorld.RootName);var sky=festival.Find(FestivalClouds.RootName);
            Assert.That(sky,Is.Not.Null,"the clouds hang under the festival, so camp has none");
            world.SetLighting(Round(Festivals.PoloFestival,0,"Playing",Spin,40),"me");
            Assert.That(Shown(sky).Count,Is.EqualTo(CloudShapes.For(Spin).Clouds.Length),"a day level's clouds");
            world.SetLighting(Round(Festivals.PoloFestival,1,"Playing",Spin,40),"me");
            Assert.That(Shown(sky),Is.Empty,"none at night");
            world.SetLighting(Round(Festivals.PoloFestival,2,"Playing",Spin,40),"me");world.SetPhase("CampReview");
            Assert.That(sky.gameObject.activeInHierarchy,Is.False,"camp hides the festival's sky");
            var material=sky.GetComponentInChildren<Renderer>(true).sharedMaterial;var puff=material.mainTexture;var mesh=sky.GetComponentInChildren<MeshFilter>(true).sharedMesh;
            // Edit mode sends the world no OnDestroy; play mode and builds do, so call it as they would.
            typeof(FestivalWorld).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(world,null);
            Assert.That(material==null&&puff==null&&mesh==null,Is.True,"the world destroys the clouds' material, texture and meshes as it goes");
        }

        private FestivalClouds Sky(out Transform root)
        {
            var grounds=Made("Festival grounds").transform;var clouds=new FestivalClouds(grounds);skies.Add(clouds);
            root=grounds.Find(FestivalClouds.RootName);Assert.That(root,Is.Not.Null,"setup: the clouds hang under the grounds");
            return clouds;
        }
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
        private static RoundState Round(int festival,int level,string phase,int spin,double seconds=0)=>
            new RoundState{Phase=phase,FestivalIndex=festival,LevelIndex=level,SpinSeed=spin,ElapsedSeconds=seconds,DurationSeconds=Festivals.Level(festival,level,0).DurationSeconds};
        // The first moment of the level with no dust storm blowing.
        private static RoundState Calm(RoundState state){while(FestivalSimulation.DustStorm(state))state.ElapsedSeconds++;return state;}
        private static List<Renderer> Shown(Transform root){var shown=new List<Renderer>();foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.enabled&&r.gameObject.activeInHierarchy)shown.Add(r);return shown;}
        private static Transform Cloud(Transform root,int c){var cloud=root.Find("Cloud "+c);Assert.That(cloud,Is.Not.Null,"cloud "+c);return cloud;}
        // A cloud's puffs, read back from its mesh: one square quad a puff, as wide as the puff reaches. Empty slots are left out.
        private static List<CloudShapes.Puff> PuffsOf(Transform cloud)
        {
            var vertices=cloud.GetComponent<MeshFilter>().sharedMesh.vertices;var puffs=new List<CloudShapes.Puff>();
            Assert.That(vertices.Length,Is.EqualTo(4*CloudShapes.MaxPuffs),"setup: a quad for every puff a cloud can have");
            for(int q=0;q<vertices.Length;q+=4)
            {
                float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
                for(int v=q;v<q+4;v++){minX=Mathf.Min(minX,vertices[v].x);maxX=Mathf.Max(maxX,vertices[v].x);minY=Mathf.Min(minY,vertices[v].y);maxY=Mathf.Max(maxY,vertices[v].y);}
                if(maxX-minX>1e-4f)puffs.Add(new CloudShapes.Puff((minX+maxX)/2,(minY+maxY)/2,(maxX-minX)/2));
            }
            return puffs;
        }
        private static bool Match(List<CloudShapes.Puff> puffs,CloudShapes.Puff[] table)
        {
            if(puffs.Count!=table.Length)return false;
            for(int i=0;i<table.Length;i++)if(Mathf.Abs(puffs[i].X-table[i].X)>1e-3f||Mathf.Abs(puffs[i].Y-table[i].Y)>1e-3f||Mathf.Abs(puffs[i].Radius-table[i].Radius)>1e-3f)return false;
            return true;
        }
    }
}
