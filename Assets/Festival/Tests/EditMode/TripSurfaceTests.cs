using System.Collections.Generic;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // TRIP-11: the dose's surface pattern is drawn by the real URP pass on the scenery a view marks, in that one view only: never on
    // an unmarked thing (a person, a vision, a tell), never on the sky, never in another camera, and gone the moment the look is.
    public sealed class TripSurfaceTests
    {
        private const int Width=160,Height=90;
        private readonly List<Object> made=new List<Object>();
        [TearDown]public void Cleanup(){foreach(var thing in made)if(thing!=null)Object.DestroyImmediate(thing);made.Clear();}

        [Test]public void TheDosePatternsMarkedSceneryInItsOwnViewOnly()
        {
            var view=Eye("Trip surface view");var other=Eye("Another camera");
            var light=new GameObject("Trip surface sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);made.Add(light.gameObject);
            var scenery=Block("Scenery",new Vector3(-1.2f,0,6));var person=Block("Unmarked",new Vector3(1.2f,0,6));
            FestivalTripSurfaces.Mark(scenery.transform);
            Color32[] plain;
            using(var surfaces=new FestivalTripSurfaces())
            {
                Assert.That(surfaces.Available,"the pattern's shader loads from Resources and is supported here");
                plain=Shot(view);var plainOther=Shot(other);
                surfaces.Show(view,FestivalTrip.Look("lsd",4,false,2));
                var stamped=Shot(view);
                Assert.That(Changed(plain,stamped,view,scenery),Is.GreaterThan(40),"Tongue Stamps lay their lattice on the marked scenery");
                Assert.That(Changed(plain,stamped,view,person),Is.Zero,"and nothing on what is not marked");
                Assert.That(Sky(plain,stamped),Is.Zero,"nor on the sky");
                Assert.That(Changed(plainOther,Shot(other),other,scenery),Is.Zero,"and only in the dosed view's own camera");
                surfaces.Show(view,FestivalTrip.Look("mushrooms",4,false,2));
                var guys=Shot(view);
                Assert.That(Changed(stamped,guys,view,scenery),Is.GreaterThan(40),"Fun Guys draw their own cells, not the lattice");
                surfaces.Show(view,default);
                Assert.That(Changed(plain,Shot(view),view,scenery),Is.Zero,"sober again: the scenery is plain at once");
                surfaces.Show(view,FestivalTrip.Look("lsd",4,false,2));
            }
            Assert.That(Changed(plain,Shot(view),view,scenery),Is.Zero,"torn down mid-dose, it leaves no pass behind");
        }

        // A build without the shader, or a machine that cannot run it, draws a plain view and never fails.
        [Test]public void WithoutItsShaderEveryDoseShowsAPlainView()
        {
            var view=Eye("Trip surface view");var scenery=Block("Scenery",new Vector3(0,0,6));
            FestivalTripSurfaces.Mark(scenery.transform);
            var plain=Shot(view);
            using(var surfaces=new FestivalTripSurfaces("Festival trip surface missing"))
            {
                Assert.That(surfaces.Available,Is.False,"with no shader the pattern is unavailable");
                surfaces.Show(view,FestivalTrip.Look("lsd",4,false,2));
                Assert.That(Changed(plain,Shot(view),view,scenery),Is.Zero,"and four Tongue Stamps leave the scenery plain");
            }
        }

        // The festival's own grounds carry the mark on their scenery, but never on a person, the Giggle Tank or the sky's clouds.
        [Test]public void TheFestivalMarksItsSceneryButNeverItsPeopleTheTankOrTheClouds()
        {
            var root=new GameObject("Trip surface world");made.Add(root);root.AddComponent<FestivalWorld>().Build();
            var grounds=root.transform.Find(FestivalWorld.RootName);
            static bool Marked(Renderer renderer)=>(renderer.renderingLayerMask&FestivalTripSurfaces.SurfaceLayer)!=0;
            int scenery=0;foreach(var renderer in grounds.GetComponentsInChildren<Renderer>(true))if(Marked(renderer))scenery++;
            Assert.That(Marked(grounds.Find("Ground").GetComponent<Renderer>()),"the ground carries the pattern");
            Assert.That(scenery,Is.GreaterThan(100),"and so does the rest of the scenery");
            var people=grounds.GetComponentsInChildren<FestivalCharacter>(true);
            Assert.That(people.Length,Is.GreaterThan(10),"setup: the DJ, the stage crew, the vendors and the crowd");
            foreach(var person in people)foreach(var renderer in person.GetComponentsInChildren<Renderer>(true))
                Assert.That(Marked(renderer),Is.False,person.name+"'s "+renderer.name+": people are never patterned");
            foreach(var name in new[]{FestivalGiggleTank.Name,FestivalClouds.RootName})
                foreach(var renderer in grounds.Find(name).GetComponentsInChildren<Renderer>(true))Assert.That(Marked(renderer),Is.False,name+"'s "+renderer.name+" stays clean");
        }

        private Camera Eye(string name)
        {
            var go=new GameObject(name);made.Add(go);var camera=go.AddComponent<Camera>();
            camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.3f,.6f);
            camera.transform.position=new Vector3(0,0,0);camera.fieldOfView=60;camera.nearClipPlane=.05f;camera.farClipPlane=100;
            return camera;
        }
        private GameObject Block(string name,Vector3 at)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;made.Add(go);go.transform.position=at;go.transform.localScale=new Vector3(2,2,.2f);
            var material=new Material(Resources.Load<Material>("FestivalLit")){color=new Color(.7f,.6f,.5f)};made.Add(material);
            go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        private Color32[] Shot(Camera camera)
        {
            var target=RenderTexture.GetTemporary(Width,Height,24);var picture=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            var was=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;picture.ReadPixels(new Rect(0,0,Width,Height),0,0);return picture.GetPixels32();}
            finally{RenderTexture.active=was;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(picture);}
        }
        // Pixels inside thing's screen box that differ by more than 6 of 255 between two shots.
        private static int Changed(Color32[] a,Color32[] b,Camera camera,GameObject thing)
        {
            var bounds=thing.GetComponent<Renderer>().bounds;var low=camera.WorldToViewportPoint(bounds.min);var high=camera.WorldToViewportPoint(bounds.max);
            int count=0;
            for(int y=Mathf.Max(0,(int)(Mathf.Min(low.y,high.y)*Height)+2);y<Mathf.Min(Height,(int)(Mathf.Max(low.y,high.y)*Height)-2);y++)
                for(int x=Mathf.Max(0,(int)(Mathf.Min(low.x,high.x)*Width)+2);x<Mathf.Min(Width,(int)(Mathf.Max(low.x,high.x)*Width)-2);x++)
                    if(Differs(a[y*Width+x],b[y*Width+x]))count++;
            return count;
        }
        // The top rows, where only the background shows.
        private static int Sky(Color32[] a,Color32[] b){int count=0;for(int i=(Height-8)*Width;i<Height*Width;i++)if(Differs(a[i],b[i]))count++;return count;}
        private static bool Differs(Color32 a,Color32 b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)))>6;
    }
}
