using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class VisionMarkerSessionTests:FestivalPlayModeTest
    {
        // VISION-1: in a hosted game the solo host is the tripper, so their client draws every vision, over the festivalgoers'
        // drawn bodies; leaving the game clears them.
        [UnityTest]public IEnumerator TheTrippersGameDrawsVisionsOverTheCrowd()
        {
            var world=new GameObject("Vision marker world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Vision marker session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"Day 1 starts: "+session.Message);
                yield return null;

                Assert.That(session.State.TripperId,Is.EqualTo(session.LocalPlayerId),"setup: the solo host is the tripper");
                var markers=host.GetComponent<FestivalVisionMarkers>();
                Assert.That(markers,Is.Not.Null,"the game draws the tripper's visions");
                Assert.That(session.State.Visions,Is.Not.Empty,"setup: the tripper has visions");
                Assert.That(markers.Shown,Is.EqualTo(session.State.Visions.Count),"every vision is drawn");
                var bodies=GameObject.Find("Authoritative actor presentation").transform;
                foreach(var vision in session.State.Visions)
                {
                    if(vision.NpcId=="")continue;
                    Vector3 marker=host.transform.Find("Vision "+vision.Id).position,body=bodies.Find(vision.NpcId).position;
                    Assert.That(Vector2.Distance(new Vector2(marker.x,marker.z),new Vector2(body.x,body.z)),Is.LessThan(1e-3f),vision.Kind+" hangs over "+vision.NpcId+"'s drawn body");
                    Assert.That(marker.y-body.y,Is.GreaterThan(2f),vision.Kind+" floats over "+vision.NpcId+"'s head");
                }

                // VISION-2: the dosed tripper's own game also shows the little creatures the dose invents. Two minutes on the level
                // clock at four doses passes the first arrival, however it was armed, and several more.
                var creatures=host.GetComponent<FestivalCreatures>();
                Assert.That(creatures!=null&&creatures.enabled,Is.True,"the game shows a dosed player's creatures");
                player.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect).Intensity=4;yield return null;
                sim.State.ElapsedSeconds+=120;yield return null;yield return null;
                Assert.That(creatures.Live,Is.GreaterThan(0),"two minutes at four doses bring creatures");

                session.Leave();yield return null;
                Assert.That(markers.Shown,Is.Zero,"leaving the game clears the markers");
                Assert.That(creatures.Live,Is.Zero,"leaving the game clears the creatures");
                foreach(Transform child in host.transform)Assert.That(child.name,Does.Not.StartWith("Vision "),"leaving the game removes the markers");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        // VISION-1: a truth's shadow is one of the tells. The festival sun sits only 14 degrees up (and is dimmed at night), so a
        // marker's real shadow lands about 10 m away; the shadow the tripper learns from has to lie at the festivalgoer's feet.
        [UnityTest]public IEnumerator ByDayTheTripperSeesATruthsShadowAndNoneUnderAFake()=>ShadowAtTheFeet(0);
        [UnityTest]public IEnumerator ByNightTheTripperSeesATruthsShadowAndNoneUnderAFake()=>ShadowAtTheFeet(1);

        private static IEnumerator ShadowAtTheFeet(int level)
        {
            string when=level==0?"Day 1":"Night 1";
            var world=new GameObject("Vision shadow world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Vision shadow session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                sim.State.LevelIndex=level;
                var player=sim.Player(session.LocalPlayerId);player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),when+" starts: "+session.Message);
                Assert.That(FestivalNightLighting.IsNight(session.State),Is.EqualTo(level==1),"setup: "+when+" is lit as "+when);

                // The darkest a tripper's view gets: one dose brightens it least (LIGHT-1). TEST-1: a dose of nothing in particular, so no
                // substance's look (TRIP-5: tint, glows, trails, a lifted grade) touches the picture, and none of the dose's creatures
                // (VISION-2) wander into it.
                var dose=player.Effects.Find(e=>e.Id==FestivalSimulation.DoseEffect);Assert.That(dose,Is.Not.Null,"setup: the solo host trips");dose.Intensity=1;dose.Substance="";
                var creatures=host.GetComponent<FestivalCreatures>();Assert.That(creatures,Is.Not.Null,"setup: the game draws creatures");creatures.enabled=false;
                // One truth-marked festivalgoer 6 m ahead of the tripper on the open main path; the rest of the crowd is sent away.
                // TEST-1: and the rest of the visions. A dose of 3 or 4 can deal a second truth (a double buyer) over the same buyer, whose
                // shadow lies just where this one does and stays when this marker is hidden: no pixel changes. They film nothing (no
                // influencer's frame on the ground) and stand plainly idle.
                var truth=sim.State.Visions.Find(v=>v.NpcId!=""&&v.IsTrue);Assert.That(truth,Is.Not.Null,"setup: a truth over a festivalgoer");
                sim.State.Visions.RemoveAll(v=>v!=truth);
                var npc=sim.State.Npcs.Find(n=>n.Id==truth.NpcId);sim.State.Npcs.RemoveAll(n=>n!=npc);npc.Twist="";
                IEnumerator Hold(float seconds)
                {
                    for(float start=Time.realtimeSinceStartup;Time.realtimeSinceStartup-start<seconds;)
                    {player.X=0;player.Z=0;npc.X=0;npc.Z=6;npc.Yaw=180;npc.Mode="Blending";npc.IdlePose="Idle";yield return null;}
                    // Measured a whole frame later, so the markers' LateUpdate has placed them over the body as it stands. (The Editor's
                    // batch-mode test runner never resumes a WaitForEndOfFrame.)
                    yield return null;
                }
                yield return Hold(.6f);
                var marker=host.transform.Find("Vision "+truth.Id);var body=GameObject.Find("Authoritative actor presentation").transform.Find(npc.Id);
                Assert.That(marker!=null&&marker.gameObject.activeSelf,Is.True,"setup: the truth's marker is drawn");
                var shadow=marker.Find("Shadow");Aim(session.ViewCamera,body.position);var seen=session.ViewCamera.WorldToViewportPoint(shadow.position);
                Assert.That(shadow.gameObject.activeInHierarchy,Is.True,"setup: the truth's shadow is drawn");
                Assert.That(Vector2.Distance(new Vector2(shadow.position.x,shadow.position.z),new Vector2(body.position.x,body.position.z)),Is.LessThan(.05f),"setup: the truth's shadow lies at its festivalgoer's feet");
                Assert.That(seen.z>0&&seen.x>0&&seen.x<1&&seen.y>0&&seen.y<1,Is.True,"setup: the tripper's view takes in the shadow ("+seen+")");
                float solid=marker.Find("Glyph").localScale.x;int truthCue=AtTheFeet(session.ViewCamera,marker.gameObject,body.position);

                // The host's Tell flag is all the tripper's client goes by: the same festivalgoer, now marked by a fake.
                truth.Tell=true;
                yield return Hold(.4f);
                int fakeCue=AtTheFeet(session.ViewCamera,marker.gameObject,body.position);

                // Walking at 4 m/s moves the first-person camera, which sets the fake shimmering.
                var from=session.ViewCamera.transform.position;float peak=0;
                for(float start=Time.realtimeSinceStartup;Time.realtimeSinceStartup-start<1;)
                {
                    player.X=4*(Time.realtimeSinceStartup-start);npc.X=0;npc.Z=6;yield return null;
                    peak=Mathf.Max(peak,Mathf.Abs(marker.Find("Glyph").localScale.x/solid-1));
                }
                float walked=Vector3.Distance(from,session.ViewCamera.transform.position);
                Debug.Log("VISION-1 "+when+": pixels a truth's marker changes at its festivalgoer's feet "+truthCue+", a fake's "+fakeCue+"; fake's shimmer while walking "+walked.ToString("0.0")+" m: "+peak.ToString("0.00"));
                var failures=new List<string>();
                if(truthCue<=300)failures.Add("the tripper cannot see a truth's shadow at its festivalgoer's feet ("+truthCue+" pixels change, want over 300)");
                if(fakeCue>=10)failures.Add("a fake shows a shadow at its festivalgoer's feet ("+fakeCue+" pixels change)");
                if(walked<=2)failures.Add("setup: the tripper's camera walked only "+walked+" m");
                if(peak<=.05f)failures.Add("the running game's camera leaves a fake still as the tripper walks (shimmer "+peak+")");
                Assert.That(failures,Is.Empty,when);
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        // The tripper's eye 6 m back from the feet, looking their way.
        private static void Aim(Camera eye,Vector3 feet)=>eye.transform.SetPositionAndRotation(new Vector3(feet.x,1.65f,feet.z-6),Quaternion.identity);

        // Pixels on the ground within 1.5 m of the feet, in the tripper's 640x360 view from 6 m back, that change by more than 12
        // of 255 when the vision's marker is hidden: what the marker puts at its festivalgoer's feet.
        private static int AtTheFeet(Camera eye,GameObject marker,Vector3 feet)
        {
            const int width=640,height=360;
            Aim(eye,feet);
            var target=RenderTexture.GetTemporary(width,height,24);var picture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            eye.targetTexture=target;
            try
            {
                Color32[] Shot(){eye.Render();var was=RenderTexture.active;RenderTexture.active=target;picture.ReadPixels(new Rect(0,0,width,height),0,0);RenderTexture.active=was;return picture.GetPixels32();}
                var shown=Shot();marker.SetActive(false);var hidden=Shot();marker.SetActive(true);
                float left=1,right=0,low=1,high=0;
                foreach(float x in new[]{-1.5f,1.5f})foreach(float z in new[]{-1.5f,1.5f})
                {var at=eye.WorldToViewportPoint(feet+new Vector3(x,0,z));left=Mathf.Min(left,at.x);right=Mathf.Max(right,at.x);low=Mathf.Min(low,at.y);high=Mathf.Max(high,at.y);}
                int changed=0;
                for(int y=Mathf.Max(0,(int)(low*height));y<Mathf.Min(height,(int)(high*height));y++)
                    for(int x=Mathf.Max(0,(int)(left*width));x<Mathf.Min(width,(int)(right*width));x++)
                    {
                        Color32 a=shown[y*width+x],b=hidden[y*width+x];
                        if(Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)))>12)changed++;
                    }
                return changed;
            }
            finally{eye.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.Destroy(picture);}
        }
    }
}
