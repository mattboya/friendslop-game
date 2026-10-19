using System;
using System.Collections.Generic;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // TRIP-11: the dose's surface pattern is drawn by the real URP pass on the scenery a view marks, in that one view only: never on
    // an unmarked thing (a person, a vision, a tell), never on the sky, never in another camera, and gone the moment the look is.
    public sealed class TripSurfaceTests
    {
        private const int Width=160,Height=90;
        private const string Switch="FESTIVAL_TRIP_SURFACES";
        private readonly List<Object> made=new List<Object>();
        [TearDown]public void Cleanup(){foreach(var thing in made)if(thing!=null)Object.DestroyImmediate(thing);made.Clear();}

        [Test]public void TheDosePatternsMarkedSceneryInItsOwnViewOnly()
        {
            var view=Eye("Trip surface view");var other=Eye("Another camera");
            var light=new GameObject("Trip surface sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);made.Add(light.gameObject);
            var scenery=Block("Scenery",new Vector3(-1.2f,0,6));var person=Block("Unmarked",new Vector3(1.2f,0,6));
            FestivalTripSurfaces.Mark(scenery.transform);
            Color32[] plain;
            LogAssert.Expect(LogType.Log,"[Festival.Trip] action=surfaces available=True reason=ok");
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
            LogAssert.Expect(LogType.Log,"[Festival.Trip] action=surfaces available=False reason=missing_shader");
            using(var surfaces=new FestivalTripSurfaces("Festival trip surface missing"))
            {
                Assert.That(surfaces.Available,Is.False,"with no shader the pattern is unavailable");
                surfaces.Show(view,FestivalTrip.Look("lsd",4,false,2));
                Assert.That(Changed(plain,Shot(view),view,scenery),Is.Zero,"and four Tongue Stamps leave the scenery plain");
            }
        }

        // A development build run with FESTIVAL_TRIP_SURFACES=off draws every dose plain, so the stage benchmark can measure the
        // pattern's own cost in one build.
        [Test]public void SwitchedOffEveryDoseShowsAPlainView()
        {
            var view=Eye("Trip surface view");var scenery=Block("Scenery",new Vector3(0,0,6));
            FestivalTripSurfaces.Mark(scenery.transform);
            var plain=Shot(view);
            string was=Environment.GetEnvironmentVariable(Switch);
            try
            {
                Environment.SetEnvironmentVariable(Switch,"off");
                LogAssert.Expect(LogType.Log,"[Festival.Trip] action=surfaces available=False reason=switched_off");
                using(var surfaces=new FestivalTripSurfaces())
                {
                    Assert.That(surfaces.Available,Is.False,"switched off, the pattern is unavailable though its shader is here");
                    surfaces.Show(view,FestivalTrip.Look("lsd",4,false,2));
                    Assert.That(Changed(plain,Shot(view),view,scenery),Is.Zero,"and four Tongue Stamps leave the scenery plain");
                }
            }
            finally{Environment.SetEnvironmentVariable(Switch,was);}
        }

        // Only with diagnostics on, and only when something changes: the pattern starting in a camera, a new strength, its end and
        // the teardown. Drift and breathing move every frame, so they never make a line of their own.
        [Test]public void TransitionsAreTracedOnlyWhileDiagnosticsShow()
        {
            var view=Eye("Trip surface view");
            var heard=new List<string>();const string Prefix="[Festival.Trip] action=surfaces ";
            void Hear(string message,string stack,LogType type){if(message.StartsWith(Prefix))heard.Add(message.Substring(Prefix.Length));}
            bool visible=DevelopmentDiagnostics.Visible;
            Application.logMessageReceived+=Hear;
            try
            {
                DevelopmentDiagnostics.Visible=false;
                using(var quiet=new FestivalTripSurfaces()){quiet.Show(view,FestivalTrip.Look("lsd",4,false,2));quiet.Show(view,default);}
                Assert.That(heard,Is.EqualTo(new[]{"available=True reason=ok"}),"with diagnostics hidden, only the construction is logged");
                heard.Clear();
                DevelopmentDiagnostics.Visible=true;
                var first=FestivalTrip.Look("lsd",4,false,2);var weaker=FestivalTrip.Look("lsd",2,false,3);var guys=FestivalTrip.Look("mushrooms",3,false,3);
                using(var traced=new FestivalTripSurfaces())
                {
                    traced.Show(view,first);
                    for(int frame=1;frame<30;frame++)traced.Show(view,FestivalTrip.Look("lsd",4,false,2+frame*.1f));
                    traced.Show(view,weaker);traced.Show(view,weaker);
                    traced.Show(view,guys);
                    traced.Show(view,default);traced.Show(view,default);
                    traced.Show(view,first);
                }
                Assert.That(FestivalTrip.Look("lsd",4,false,4.9f).Drift,Is.Not.EqualTo(first.Drift),"setup: the lattice drifts between frames");
                Assert.That(heard,Is.EqualTo(new[]{"available=True reason=ok",On("on",first),On("change",weaker),On("change",guys),"state=off",On("on",first),"state=off","state=teardown"}));
            }
            finally{Application.logMessageReceived-=Hear;DevelopmentDiagnostics.Visible=visible;}
            string On(string state,FestivalTrip.Perception look)=>"state="+state+" camera=Trip surface view lattice="+look.Lattice.ToString("F2")+" cells="+look.Cells.ToString("F2")
                +" drift="+look.Drift.ToString("F2")+" breathe="+look.Breathe.ToString("F2");
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
