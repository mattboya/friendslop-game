using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // CAM-1: V (or the CAMERA VIEW setting) puts the camera on a boom over your right shoulder: 2.4 m back, .45 m right and .25 m
    // up from a pivot just over your eye, turned with the view. Walls that block a cop's sight pull it in, your own body and the
    // nameplates never do, it never sinks under the ground, and a boom pulled into your head hides your body again. Lying down,
    // a camp visit and the Ferris wheel stay first person. The setting is saved with the profile, and an old profile loads as
    // first person.
    public sealed class ThirdPersonCameraTests
    {
        // Far from anything a scene might hold, so only a test's own wall is ever in the way; an eye's height over the ground.
        private static readonly Vector3 Eye=new Vector3(200,1.65f,200);
        private static Vector3 Pivot=>Eye+Vector3.up*.1f;
        private readonly List<GameObject> made=new List<GameObject>();
        private const string ProfileId="cam-test";
        private static string ProfileFile=>Path.Combine(LocalProfile.Folder,ProfileId+".json");
        [TearDown]public void Cleanup()
        {
            foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();
            if(File.Exists(ProfileFile))File.Delete(ProfileFile);
        }
        // A wall across the view's way back, its near face `behind` metres behind the pivot, on `layer`.
        private void WallBehind(float behind,int layer=0)
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);made.Add(wall);wall.name="Wall behind";wall.layer=layer;
            wall.transform.position=Pivot+Vector3.back*(behind+.1f);wall.transform.localScale=new Vector3(6,6,.2f);
            Physics.SyncTransforms();
        }
        private static Vector3 Place(float yaw,float pitch,out float boom)=>FestivalThirdPerson.Place(Eye,yaw,pitch,out boom);

        // The stage DJ plays the set seen from behind and above the decks, looking down and out over the crowd, whatever the
        // camera setting; the set over, the view is your own again.
        [Test]public void TheStageDjSeesTheirSetFromBehindAndAboveTheDecks()
        {
            var host=new GameObject("DJ view");made.Add(host);var session=host.AddComponent<FestivalSession>();
            try
            {
                typeof(FestivalSession).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,null);
                session.Profile.Data.ThirdPerson=false;
                var update=typeof(FestivalSession).GetMethod("UpdateCamera",BindingFlags.NonPublic|BindingFlags.Instance);
                var dj=new PlayerState{Id="me",VisualPose="Dj",X=Catalog.StageDjX,Z=Catalog.StageDjZ,Yaw=180};
                update.Invoke(session,new object[]{dj});
                var view=session.ViewCamera.transform;
                Assert.That(session.ThirdPersonView,Is.True,"the set is seen from outside, though this player chose first person");
                Assert.That(view.position.z,Is.GreaterThan(Catalog.StageDjZ+1.5f),"from behind the DJ, got "+view.position);
                Assert.That(view.position.y,Is.GreaterThan(FestivalWorld.StageDjHeight+1.65f+.4f),"and above them, got "+view.position);
                Assert.That(view.forward.z,Is.LessThan(-.9f),"looking out over the crowd, got "+view.forward);
                Assert.That(view.forward.y,Is.LessThan(-.15f),"and down at it, got "+view.forward);
                update.Invoke(session,new object[]{new PlayerState{Id="me",VisualPose="Idle",X=Catalog.StageTakeoverX,Z=Catalog.StageTakeoverZ,Yaw=180}});
                Assert.That(session.ThirdPersonView,Is.False,"the set over, the camera is the player's own choice again");
                Assert.That(view.forward.y,Is.EqualTo(0).Within(.02f),"looking level again");
            }
            finally{GraphicsApplicationTests.Close(host,session);}
        }

        [Test]public void TheBoomSitsBehindAndOverTheRightShoulder()
        {
            var shoulder=Place(0,0,out float boom)-Pivot;
            Assert.That(Vector3.Distance(shoulder,new Vector3(.45f,.25f,-2.4f)),Is.LessThan(.001f),"facing north, the camera sits 2.4 m behind the pivot, .45 m right and .25 m up: "+shoulder.ToString("0.000"));
            Assert.That(boom,Is.EqualTo(shoulder.magnitude).Within(.001f),"with nothing in the way the boom runs its full length");
            var east=Place(90,0,out boom)-Pivot;
            Assert.That(Vector3.Distance(east,new Vector3(-2.4f,.25f,-.45f)),Is.LessThan(.001f),"facing east, the same offset turns with the view: "+east.ToString("0.000"));
            var down=Place(0,30,out boom)-Pivot;
            Assert.That(down.y,Is.GreaterThan(1),"looking down, the boom swings up behind you: "+down.ToString("0.000"));
        }

        [Test]public void AWallBehindPullsTheCameraIn()
        {
            WallBehind(1);
            var camera=Place(0,0,out float boom);
            Assert.That(camera.z,Is.GreaterThan(Pivot.z-1),"the camera stays on your side of a wall 1 m behind: "+camera.ToString("0.000"));
            Assert.That(Vector3.Distance(camera,Pivot),Is.LessThan(.85f),"pulled in to within .85 m of the pivot");
            Assert.That(boom,Is.EqualTo(Vector3.Distance(camera,Pivot)).Within(.001f),"the boom is as long as it was pulled in to");
            Cleanup();WallBehind(.3f);
            Assert.That(Vector3.Distance(Place(0,0,out boom),Pivot),Is.EqualTo(.35f).Within(.001f),"a wall right behind you still leaves the camera .35 m back");
        }

        [Test]public void YourOwnBodyNeverPullsTheCameraIn()
        {
            foreach(int layer in new[]{31,30})
            {
                WallBehind(1,layer);
                Assert.That(Vector3.Distance(Place(0,0,out float boom),Pivot),Is.GreaterThan(2.4f),"a collider on layer "+layer+" (your body, or a nameplate) is not in the way");
                Cleanup();
            }
        }

        [Test]public void LookingStraightUpNeverPutsTheCameraUnderground()
        {
            foreach(float yaw in new[]{0f,90f,200f})
                Assert.That(Place(yaw,-80,out float boom).y,Is.GreaterThanOrEqualTo(.25f),"looking up at yaw "+yaw+", the camera stays .25 m over the ground");
        }

        [Test]public void TooCloseHidesYourOwnBody()
        {
            const int Body=1<<31;int mask=~0&~Body;
            Assert.That(FestivalThirdPerson.Mask(mask,2.4f)&Body,Is.Not.Zero,"a full boom shows your body");
            Assert.That(FestivalThirdPerson.Mask(~0,.5f)&Body,Is.Zero,"a .5 m boom looks out from inside your head, so your body is hidden");
            Assert.That(FestivalThirdPerson.Mask(~0,0)&Body,Is.Zero,"first person (no boom) hides your body");
            Assert.That(FestivalThirdPerson.Mask(mask,.5f),Is.EqualTo(mask),"and nothing else changes");
            WallBehind(.7f);Place(0,0,out float boom);
            Assert.That(FestivalThirdPerson.Mask(mask,boom)&Body,Is.Zero,"a wall .7 m behind pulls the boom into your head ("+boom.ToString("0.00")+" m), hiding your body");
        }

        [Test]public void FirstPersonWhileLyingInACampOrOnTheWheel()
        {
            Assert.That(FestivalThirdPerson.Applies(true,false,false,false),Is.True,"wanted, and nothing in the way: over the shoulder");
            Assert.That(FestivalThirdPerson.Applies(true,true,false,false),Is.False,"lying down looks up from the grass");
            Assert.That(FestivalThirdPerson.Applies(true,false,true,false),Is.False,"a camp room is first person");
            Assert.That(FestivalThirdPerson.Applies(true,false,false,true),Is.False,"the Ferris wheel's gondola is first person");
            Assert.That(FestivalThirdPerson.Applies(false,false,false,false),Is.False,"not wanted: first person");
            Assert.That(FestivalThirdPerson.Applies(false,true,true,true),Is.False,"not wanted, anywhere: first person");
        }

        [Test]public void AnOldProfileLoadsAsFirstPerson()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfileFile));
            File.WriteAllText(ProfileFile,"{\"SchemaVersion\":1,\"Cosmetics\":[],\"SeenDialogue\":[],\"ReducedMotion\":false,\"HighContrast\":true,\"TimingCalibrationMs\":0,\"MouseSensitivity\":0.2,\"MusicVolume\":0.35,\"UnlockedFestivalCount\":1}");
            var old=new LocalProfile(ProfileId);
            Assert.That(old.Data.MouseSensitivity,Is.EqualTo(.2f).Within(1e-4f),"setup: the old profile loaded");
            Assert.That(old.Data.ThirdPerson,Is.False,"a profile saved before CAM-1 loads as first person");
            old.Data.ThirdPerson=true;Assert.That(old.Save(),Is.EqualTo(""),"setup: saved");
            Assert.That(new LocalProfile(ProfileId).Data.ThirdPerson,Is.True,"third person survives saving and loading again");
        }

        [Test]public void TheViewKeyIsVAndClashesWithNothing()
        {
            using(var input=new FestivalInput(ProfileId))
            {
                Assert.That(input.View,Is.Not.Null,"there is a View action");
                Assert.That(input.View.bindings.Count,Is.EqualTo(1),"bound once");
                Assert.That(input.View.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/v"),"to V");
                foreach(var action in input.Map.actions)
                {
                    if(action==input.View)continue;
                    foreach(var binding in action.bindings)Assert.That(binding.effectivePath,Is.Not.EqualTo("<Keyboard>/v"),action.name+" is also on V");
                }
            }
        }
    }
}
