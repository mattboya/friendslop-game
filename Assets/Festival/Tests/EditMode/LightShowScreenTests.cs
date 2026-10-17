using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // MINI-3: the Light Show plays on the game's own clock while the festival holds. The booth's stranger gloves with two light trails
    // in the glover's colours while the real script's passers, sweeping lights and sign shapes play behind them; each viewer alone sees
    // their own dose's fakes, every one carrying the vision tells. Comfort settings never hide what the questions ask about.
    public sealed class LightShowScreenTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}
        [UnityTearDown]public IEnumerator LeaveOwnedPlayMode(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [Test]public void TheRealScriptPlaysBehindTheGloverInItsOwnWindows()
        {
            var game=Started();var view=Wire(game,"p1");var show=view.Booth.LightShow;var before=JsonUtility.ToJson(view);
            var lights=Lights();var glover=Glover();var audience=Audience(view);var eye=Eye(view,audience);var spot=Spot(view);var toward=(audience-spot).normalized;
            var passers=Of(show,LightShowEventKind.Passer);var sweeps=Of(show,LightShowEventKind.Sweep);var signs=Of(show,LightShowEventKind.Sign);var colours=Of(show,LightShowEventKind.Glover);
            Assert.That(passers.Count,Is.InRange(2,6),"setup: the script has its passers");
            var first=new Dictionary<string,Vector3>();var last=new Dictionary<string,Vector3>();
            for(int step=0;step<=88;step++)
            {
                float t=step*.25f;lights.Apply(view,"p1",t,glover,audience,eye,t,.02f);
                for(int k=0;k<passers.Count;k++)
                {
                    var e=passers[k];var passer=Find(lights,"Passer "+k);bool on=t>=e.StartsAt&&t<e.EndsAt;
                    Assert.That(passer!=null&&passer.gameObject.activeSelf,Is.EqualTo(on),"real passer "+k+" walks past only from "+e.StartsAt+" s to "+e.EndsAt+" s (at "+t+" s)");
                    if(!on)continue;
                    Assert.That(passer.GetComponentInChildren<FestivalCharacter>(),Is.Not.Null,"a passer is a festivalgoer rig");
                    Assert.That(Vector3.Dot(passer.position-spot,toward),Is.LessThan(-2),"passer "+k+" walks past behind the glover, as the audience sees it");
                    if(!first.ContainsKey(passer.name))first[passer.name]=passer.position;last[passer.name]=passer.position;
                    ShadowIs(passer.Find("Shadow"),false,LightShowShadowTell.Small,"real passer "+k);
                    Assert.That(Vector3.Distance(Flat(passer.Find("Shadow").position),Flat(passer.position)),Is.LessThan(.01f),"passer "+k+"'s shadow lies at their feet");
                }
                for(int k=0;k<sweeps.Count;k++)
                {
                    var e=sweeps[k];var sweep=Find(lights,"Sweep "+k);bool on=t>=e.StartsAt&&t<e.EndsAt;
                    Assert.That(sweep!=null&&sweep.gameObject.activeSelf,Is.EqualTo(on),"sweep "+k+" only from "+e.StartsAt+" s to "+e.EndsAt+" s (at "+t+" s)");
                    if(!on)continue;
                    HueIs(sweep.Find("Beam").GetComponent<Renderer>().sharedMaterial.color,e.Colour,"sweep "+k);
                    ShadowIs(sweep.Find("Shadow"),false,LightShowShadowTell.Small,"real sweep "+k+"'s pool of light");
                }
                var sign=SignShown(lights);var showing=signs.Find(e=>t>=e.StartsAt&&t<e.EndsAt);
                Assert.That(sign!=null,Is.EqualTo(showing!=null),"a shape shows on the sign only while one is flashed (at "+t+" s)");
                if(showing!=null)Assert.That(sign.GetComponent<MeshFilter>().sharedMesh.name,Does.EndWith(showing.Shape),"the sign shows the script's shape (at "+t+" s)");
                var lit=colours.LastOrDefault(e=>t>=e.StartsAt&&t<e.EndsAt);
                foreach(var side in new[]{"L","R"})
                {
                    var trail=Node(lights,"Glover trail "+side).GetComponent<LineRenderer>();
                    Assert.That(trail.gameObject.activeSelf,Is.EqualTo(lit!=null),"the glover's "+side+" trail glows through the show only (at "+t+" s)");
                    if(lit!=null)HueIs(trail.sharedMaterial.color,lit.Colour,"the glover's lights at "+t+" s");
                }
                Assert.That(glover.Pose,Is.EqualTo(t<LightShow.ShowSeconds?FestivalCharacter.GlovePose:"Idle"),"the glover gloves through the show, then rests (at "+t+" s)");
                Assert.That(Vector3.Angle(glover.transform.forward,toward),Is.LessThan(1),"the glover faces the audience");
            }
            Assert.That(first.Count,Is.EqualTo(passers.Count),"every real passer walks past, each one rig");
            for(int k=0;k<passers.Count;k++)
            {
                var name="Passer "+k;var walked=last[name]-first[name];walked.y=0;
                Assert.That(walked.magnitude,Is.GreaterThan(4),name+" crosses the show");
                Assert.That(Mathf.Abs(Vector3.Dot(walked.normalized,toward)),Is.LessThan(.05f),name+" walks across the audience's view, not toward it");
            }
            Assert.That(lights.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Fake ")),Is.Zero,"a sober viewer's show has no fakes");
            Assert.That(lights.GetComponentsInChildren<Collider>(true),Is.Empty,"nothing in the show collides");
            Assert.That(JsonUtility.ToJson(view),Is.EqualTo(before),"drawing the show changes no state");

            view.Phase="Playing";view.Booth.Spent=true;lights.Apply(view,"p1",30,glover,audience,eye,30,.02f);
            Assert.That(Node(lights,"").gameObject.activeSelf,Is.False,"the show leaves with the game");
            Assert.That(glover.Pose,Is.EqualTo("Idle"),"the stranger rests again");Assert.That(glover.transform.localRotation,Is.EqualTo(Quaternion.identity),"and faces the way the booth does");
        }

        // The questions ask which colour, which shape and what was carried: each named colour reads as its own hue, each shape as
        // itself and each prop as its own thing, and only the one real carrier holds it.
        [Test]public void EveryColourShapeAndPropReadsAsItself()
        {
            var game=Started();var view=Wire(game,"p1");var show=view.Booth.LightShow;var audience=Audience(view);
            show.Events.Clear();
            for(int i=0;i<6;i++)
            {
                show.Events.Add(new LightShowEvent{Kind=LightShowEventKind.Sweep,StartsAt=i*3,EndsAt=i*3+2,Colour=LightShowQuestions.Colours[i],Side=i%2==0?1:-1});
                show.Events.Add(new LightShowEvent{Kind=LightShowEventKind.Sign,StartsAt=i*3,EndsAt=i*3+2,Shape=LightShowQuestions.Shapes[i]});
                show.Events.Add(new LightShowEvent{Kind=LightShowEventKind.Glover,StartsAt=i*3,EndsAt=i*3+3,Colour=LightShowQuestions.Colours[i]});
            }
            var lights=Lights();var glover=Glover();var eye=Eye(view,audience);var hues=new List<float>();var meshes=new List<Mesh>();
            for(int i=0;i<6;i++)
            {
                float t=i*3+1;lights.Apply(view,"p1",t,glover,audience,eye,t,.02f);var colour=LightShowQuestions.Colours[i];
                var beam=Node(lights,"Sweep "+i).Find("Beam").GetComponent<Renderer>().sharedMaterial;HueIs(beam.color,colour,"sweep "+colour);
                Assert.That(beam.IsKeywordEnabled("_EMISSION"),Is.True,colour+" sweeps glow, so night and low brightness keep them");
                HueIs(Node(lights,"Glover trail L").GetComponent<LineRenderer>().sharedMaterial.color,colour,"glover "+colour);
                Color.RGBToHSV(beam.color,out float hue,out _,out _);hues.Add(hue*360);
                var shape=SignShown(lights);Assert.That(shape,Is.Not.Null,"setup: "+LightShowQuestions.Shapes[i]+" shows");
                var mesh=shape.GetComponent<MeshFilter>().sharedMesh;Assert.That(mesh.name,Does.EndWith(LightShowQuestions.Shapes[i]));meshes.Add(mesh);
                Assert.That(shape.GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("_EMISSION"),Is.True,"the shapes glow");
            }
            for(int a=0;a<6;a++)for(int b=a+1;b<6;b++)Assert.That(Mathf.Abs(Mathf.DeltaAngle(hues[a],hues[b])),Is.GreaterThan(20),LightShowQuestions.Colours[a]+" and "+LightShowQuestions.Colours[b]+" are told apart by hue");
            var corners=meshes.Select(m=>m.vertexCount-1).ToArray();
            Assert.That(corners[1],Is.EqualTo(3),"a triangle has three corners");Assert.That(corners[2],Is.EqualTo(4),"a square four");Assert.That(corners[5],Is.EqualTo(4),"a diamond four");Assert.That(corners[3],Is.EqualTo(10),"a star five points and five dips");
            Assert.That(corners[0],Is.GreaterThanOrEqualTo(24),"a circle is round");Assert.That(corners[4],Is.GreaterThanOrEqualTo(24),"a heart is curved");
            Assert.That(meshes[5].bounds.size.y,Is.GreaterThan(meshes[5].bounds.size.x*1.2f),"a diamond stands taller than it is wide, unlike the square");
            Assert.That(meshes[2].bounds.size.y,Is.EqualTo(meshes[2].bounds.size.x).Within(.01f),"the square is square");
            var outline=meshes[4].vertices.Skip(1).ToArray();float top=outline.Max(v=>v.y);var middle=outline.OrderBy(v=>Mathf.Abs(v.x)).First(v=>v.y>0);
            Assert.That(middle.y,Is.LessThan(top-.05f),"a heart dips between its lobes");

            var looks=new List<string>();
            foreach(var prop in LightShowQuestions.Props)
            {
                var props=Wire(game,"p1");var events=props.Booth.LightShow.Events;var carrier=events.FindIndex(e=>e.Kind==LightShowEventKind.Passer&&e.Prop!="");events[carrier].Prop=prop;
                var drawn=Lights();var at=events[carrier];float t=(at.StartsAt+at.EndsAt)/2;drawn.Apply(props,"p1",t,Glover(),audience,Eye(props,audience),t,.02f);
                var ordinal=events.Take(carrier).Count(e=>e.Kind==LightShowEventKind.Passer);
                foreach(Transform passer in Node(drawn,""))
                {
                    if(!passer.name.StartsWith("Passer ")||!passer.gameObject.activeSelf)continue;
                    bool carries=passer.name=="Passer "+ordinal;var held=passer.GetComponentsInChildren<Transform>(true).Where(x=>x.name.StartsWith("Prop ")).ToList();
                    bool backpack=passer.GetComponentsInChildren<Renderer>(true).Any(r=>r.name=="Accessory_0"&&r.enabled);
                    if(carries&&prop=="Backpack"){Assert.That(backpack,Is.True,"the carrier wears the backpack");Assert.That(held,Is.Empty);looks.Add("Accessory_0");continue;}
                    Assert.That(backpack,Is.False,passer.name+" wears no backpack, so only the carrier's could be the answer");
                    if(!carries){Assert.That(held,Is.Empty,passer.name+" carries nothing");continue;}
                    Assert.That(held.Select(x=>x.name),Is.EqualTo(new[]{"Prop "+prop}),"the carrier holds the "+prop);
                    Assert.That(held[0].GetComponentsInChildren<Collider>(true),Is.Empty,"the "+prop+" has no collider");
                    looks.Add(string.Join(",",held[0].GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh.name+"/"+f.GetComponent<Renderer>().sharedMaterial.name+"/"+f.transform.localScale.ToString("F2"))));
                }
            }
            Assert.That(looks.Distinct().Count(),Is.EqualTo(LightShowQuestions.Props.Count),"each of the six props looks like itself: "+string.Join(" | ",looks));
        }

        [Test]public void EachViewerSeesOnlyTheirOwnDosesFakesAndEachCarriesTheTells()
        {
            var game=Started();
            foreach(var id in new[]{"p0","p1","ghost"})
            {
                var view=Wire(game,id);var fakes=view.Booth.LightShow.Players.Find(p=>p.PlayerId==id)?.Fakes??new List<LightShowFake>();
                Assert.That(fakes.Count,Is.EqualTo(id=="p0"?2:0),"setup: "+id+"'s dose's fakes reach them alone");
                var lights=Lights();var audience=Audience(view);var eye=Eye(view,audience);var glover=Glover();var shown=new HashSet<string>();
                for(int step=0;step<=88;step++)
                {
                    float t=step*.25f;lights.Apply(view,id,t,glover,audience,eye,t,.02f);
                    foreach(Transform child in Node(lights,""))if(child.name.StartsWith("Fake ")&&child.gameObject.activeSelf)shown.Add(child.name);
                    for(int j=0;j<fakes.Count;j++)
                    {
                        var e=fakes[j].Event;var fake=Find(lights,(e.Kind==LightShowEventKind.Passer?"Fake passer ":"Fake sweep ")+j);
                        Assert.That(fake!=null&&fake.gameObject.activeSelf,Is.EqualTo(t>=e.StartsAt&&t<e.EndsAt),id+"'s fake "+j+" shows only in its window (at "+t+" s)");
                    }
                }
                Assert.That(shown.Count,Is.EqualTo(fakes.Count),id+" sees exactly their dose's fakes");
                if(id=="p0")Assert.That(Node(lights,"Glover trail L").GetComponent<LineRenderer>().widthMultiplier,Is.GreaterThan(TrailWidth(game,"p1")*1.2f),"a dosed viewer's show is bigger");
            }

            // Every kind of fake with each wrong shadow, beside the real script: wrong shadow, off colour, and a shimmer while the view moves.
            var crafted=Wire(game,"p0");var mine=crafted.Booth.LightShow.Players.Single();mine.Fakes.Clear();
            foreach(var kind in new[]{LightShowEventKind.Passer,LightShowEventKind.Sweep})
                foreach(var shadow in new[]{LightShowShadowTell.Small,LightShowShadowTell.Goblin})
                    mine.Fakes.Add(new LightShowFake{Shadow=shadow,Event=new LightShowEvent{Kind=kind,StartsAt=6,EndsAt=10,Side=1,Look=mine.Fakes.Count,Colour=kind==LightShowEventKind.Sweep?"Blue":""}});
            var real=crafted.Booth.LightShow.Events.First(e=>e.Kind==LightShowEventKind.Passer);real.StartsAt=6;real.EndsAt=10;
            var realSweep=crafted.Booth.LightShow.Events.First(e=>e.Kind==LightShowEventKind.Sweep);realSweep.StartsAt=6;realSweep.EndsAt=10;realSweep.Colour="Blue";
            var drawn=Lights();var look=Audience(crafted);var head=Eye(crafted,look);var body=Glover();
            drawn.Apply(crafted,"p0",7,body,look,head,7,.02f);drawn.Apply(crafted,"p0",7.02f,body,look,head,7.02f,.02f);
            var still=Scales(drawn);
            for(int j=0;j<4;j++)
            {
                var f=mine.Fakes[j];var node=Node(drawn,(f.Event.Kind==LightShowEventKind.Passer?"Fake passer ":"Fake sweep ")+j);
                ShadowIs(node.Find("Shadow"),true,f.Shadow,"fake "+j);
                if(f.Event.Kind==LightShowEventKind.Passer)
                    foreach(var renderer in node.GetComponentsInChildren<Renderer>().Where(r=>r.GetComponentInParent<FestivalCharacter>()!=null))
                    {
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                        Assert.That(renderer.HasPropertyBlock()&&block.GetColor("_BaseColor")!=Color.white,Is.True,"fake passer "+j+"'s "+renderer.name+" is off in colour");
                    }
                else
                {
                    var off=node.Find("Beam").GetComponent<Renderer>().sharedMaterial.color;var truth=Node(drawn,"Sweep "+crafted.Booth.LightShow.Events.Where(e=>e.Kind==LightShowEventKind.Sweep).ToList().IndexOf(realSweep)).Find("Beam").GetComponent<Renderer>().sharedMaterial.color;
                    Color.RGBToHSV(off,out float fh,out float fs,out float fv);Color.RGBToHSV(truth,out float th,out float ts,out float tv);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(fh*360,th*360)),Is.InRange(8f,40f),"a fake sweep's blue is slightly off");Assert.That(new[]{fs,fv},Is.EqualTo(new[]{ts,tv}).Within(.02f),"only its hue");
                }
            }
            var realPasser=Node(drawn,"Passer "+crafted.Booth.LightShow.Events.Where(e=>e.Kind==LightShowEventKind.Passer).ToList().IndexOf(real));
            foreach(var renderer in realPasser.GetComponentsInChildren<Renderer>().Where(r=>r.GetComponentInParent<FestivalCharacter>()!=null))Assert.That(renderer.HasPropertyBlock(),Is.False,"a real passer keeps their own colours");
            var peak=new Dictionary<string,float>();
            for(int frame=1;frame<=30;frame++)
            {
                head.Rotate(0,3,0);float t=7.02f+frame*.02f;drawn.Apply(crafted,"p0",t,body,look,head,t,.02f);
                foreach(var pair in Scales(drawn)){peak.TryGetValue(pair.Key,out float most);peak[pair.Key]=Mathf.Max(most,Mathf.Abs(pair.Value/still[pair.Key]-1));}
            }
            foreach(var pair in peak)
                if(pair.Key.StartsWith("Fake "))Assert.That(pair.Value,Is.GreaterThan(.03f),pair.Key+" shimmers while the view turns");
                else Assert.That(pair.Value,Is.LessThan(1e-4f),pair.Key+" stays solid while the view turns");
            for(int frame=0;frame<120;frame++)drawn.Apply(crafted,"p0",8+frame*.01f,body,look,head,8+frame*.01f,.02f);
            var calm=Scales(drawn);drawn.Apply(crafted,"p0",9.3f,body,look,head,9.3f,.02f);
            foreach(var pair in Scales(drawn))Assert.That(pair.Value,Is.EqualTo(calm[pair.Key]).Within(1e-4f),pair.Key+" holds still once the view does");

            // A record of another player's in the view (a revealed show) never draws their fakes.
            var revealed=Wire(game,"p1");var theirs=Wire(game,"p0").Booth.LightShow.Players.Single();revealed.Booth.LightShow.Players.Insert(0,theirs);
            var other=Lights();var where=Audience(revealed);var eyes=Eye(revealed,where);var stranger=Glover();
            for(int step=0;step<=80;step++){float t=step*.25f;other.Apply(revealed,"p1",t,stranger,where,eyes,t,.02f);}
            Assert.That(other.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Fake ")),Is.Zero,"another player's fakes are never drawn");
        }

        // Reduced motion and any brightness keep every answer: the same people, colours, shapes and lights at the same moments. Reduced
        // motion only shortens the glover's trails and lets shapes appear without a pop; the glow keeps its strength at any brightness.
        [Test]public void ComfortSettingsKeepEverythingTheQuestionsAsk()
        {
            var game=Started();var view=Wire(game,"p0");
            var baseline=Watch(view,false,0,out var plain);
            foreach(var (reduced,brightness) in new[]{(true,0f),(false,-1f),(false,1f),(true,1f),(true,-1f)})
            {
                var watched=Watch(view,reduced,brightness,out var comfort);
                Assert.That(watched,Is.EqualTo(baseline),"reduced motion "+reduced+", brightness "+brightness+": every answer still shows");
                Assert.That(comfort.Emission,Is.EqualTo(plain.Emission*Mathf.Pow(2,-brightness)).Within(.02f),"the glow is as bright on screen at brightness "+brightness);
                if(reduced){Assert.That(comfort.Trail,Is.LessThan(plain.Trail*.7f),"reduced motion shortens the trails");Assert.That(comfort.Pop,Is.EqualTo(1).Within(1e-3f),"and shapes appear without a pop");}
                else{Assert.That(comfort.Trail,Is.EqualTo(plain.Trail),"the trails' length");Assert.That(comfort.Pop,Is.LessThan(.9f),"shapes pop in");}
            }
        }

        [UnityTest]public IEnumerator TheBoothsGloverAndExtrasPerformOnTheGameClockWhileTheWorldHolds()
        {
            yield return new EnterPlayMode();string previous=LocalProfile.Folder,scope=FestivalInput.PreferencesScope;string folder=Path.Combine(Path.GetTempPath(),"impl-U63-light-show-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);LocalProfile.Folder=folder;FestivalInput.PreferencesScope=folder;
            var go=new GameObject("impl-U63 light show host");FestivalCharacter world=null;
            try
            {
                var session=go.AddComponent<FestivalSession>();session.Host("Light show test",8732);Assert.That(session.IsHost&&session.Connected,Is.True,session.Message);
                var lights=go.GetComponent<FestivalLightShowView>();Assert.That(lights,Is.Not.Null,"every session draws the Light Show");
                var game=Started();typeof(FestivalSession).GetField("simulation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,game);typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,"p0");
                world=FestivalCharacter.Create(null,"impl-U63 world rig",Color.white);world.Pose="Dance";
                for(int k=0;k<10;k++)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase));Assert.That(Time.timeScale,Is.Zero,"setup: the festival holds for the game");Assert.That(FestivalSession.WorldAnimationPaused,Is.True);
                var glover=go.GetComponent<FestivalMinigameView>().Booth.GetComponentInChildren<FestivalCharacter>(true);
                Assert.That(glover.Pose,Is.EqualTo(FestivalCharacter.GlovePose),"the booth's stranger is the glover");
                var me=game.Player("p0");var toward=new Vector3(me.X,0,me.Z)-glover.transform.position;toward.y=0;
                Assert.That(Vector3.Angle(glover.transform.forward,toward),Is.LessThan(2),"and gloves facing you");
                yield return new WaitForSecondsRealtime(.5f);
                var gloving=Bones(glover);var held=Bones(world);var hands=new Bounds(Hand(glover).position,Vector3.zero);float ahead=float.MaxValue;
                for(float end=Time.realtimeSinceStartup+.6f;Time.realtimeSinceStartup<end;)
                {
                    yield return null;var hand=Hand(glover).position;hands.Encapsulate(hand);ahead=Mathf.Min(ahead,Vector3.Dot(hand-glover.transform.position,glover.transform.forward));
                }
                Assert.That(Bones(glover),Is.Not.EqualTo(gloving),"the glover moves on the game's clock");Assert.That(hands.size.magnitude,Is.GreaterThan(.1f),"their lit hands wheel ("+hands.size+")");
                Assert.That(hands.min.y-glover.transform.position.y,Is.GreaterThan(.6f),"up at the chest, where the audience looks ("+hands.min.y+" over "+glover.transform.position.y+")");
                Assert.That(ahead,Is.GreaterThan(.1f),"and out in front of the glover ("+ahead+" m)");
                Assert.That(Bones(world),Is.EqualTo(held),"while a world rig holds still");
                Assert.That(Node(lights,"Glover trail R").GetComponent<LineRenderer>().positionCount,Is.GreaterThan(3),"the hands draw their trails");

                var first=game.State.Booth.LightShow.Events.First(e=>e.Kind==LightShowEventKind.Passer);
                for(float end=Time.realtimeSinceStartup+6;session.EstimatedMinigameSeconds<first.StartsAt+.5f&&Time.realtimeSinceStartup<end;)yield return null;
                var passer=Node(lights,"Passer 0");Assert.That(passer.gameObject.activeSelf,Is.True,"setup: the first passer is out");
                var walker=passer.GetComponentInChildren<FestivalCharacter>();var stride=Bones(walker);var from=passer.position;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(Bones(walker),Is.Not.EqualTo(stride),"a passer's legs walk on the game's clock");Assert.That(Vector3.Distance(passer.position,from),Is.GreaterThan(.2f),"as they cross");
                Assert.That(Bones(world),Is.EqualTo(held),"the world still holds");
                int fakes=game.State.Booth.LightShow.Players.Find(p=>p.PlayerId=="p0").Fakes.Count;
                Assert.That(Node(lights,"").Cast<Transform>().Count(t=>t.name.StartsWith("Fake ")),Is.EqualTo(fakes),"your own dose's fakes are on hand");

                var b=game.State.Booth;b.ScoredAt=Math.Max(0,b.Seconds-FestivalSimulation.MinigameBannerSeconds);
                for(float end=Time.realtimeSinceStartup+3;session.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<end;)yield return null;
                yield return null;yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"setup: play resumes");
                Assert.That(Node(lights,"").gameObject.activeSelf,Is.False,"the show leaves with the game");
                Assert.That(glover.Pose,Is.EqualTo("Idle"),"the stranger rests");Assert.That(glover.transform.localRotation,Is.EqualTo(Quaternion.identity),"facing the booth's way again");
                Assert.That(Time.timeScale,Is.EqualTo(1));
                session.Leave();
            }
            finally{if(world!=null)Object.Destroy(world.gameObject);Object.DestroyImmediate(go);LocalProfile.Folder=previous;FestivalInput.PreferencesScope=scope;if(Directory.Exists(folder))Directory.Delete(folder,true);}
            yield return new ExitPlayMode();
        }

        // A Light Show under way: p0 at dose 2, p1 sober and a spirit watching, all standing 2 m in front of the booth.
        private static FestivalSimulation Started()
        {
            var game=new FestivalSimulation(123);game.AddPlayer("p0","First").Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=100});game.AddPlayer("p1","Second");game.AddPlayer("ghost","Watcher").Life="Spirit";
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.Booth=new MinigameState{Kind=FestivalSimulation.LightShowGame,Spot=1};var at=FestivalSimulation.BoothAt(game.State);
            foreach(var p in game.State.Players){p.X=at.X;p.Z=at.Z+2;}for(int k=0;k<35&&game.State.Phase!=FestivalSimulation.MinigamePhase;k++)game.Tick(.1);
            Assert.That(game.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"setup: the Light Show starts");return game;
        }
        private static RoundState Wire(FestivalSimulation game,string id)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,id)));
        private static List<LightShowEvent> Of(LightShowState show,LightShowEventKind kind)=>show.Events.Where(e=>e.Kind==kind).ToList();
        private static Vector3 Spot(RoundState state){var at=FestivalSimulation.BoothAt(state);return new Vector3(at.X,0,at.Z);}
        private static Vector3 Audience(RoundState state)=>Spot(state)+Vector3.forward*2;
        private static Vector2 Flat(Vector3 at)=>new Vector2(at.x,at.z);
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
        private FestivalLightShowView Lights()=>Made("impl-U63 light show").AddComponent<FestivalLightShowView>();
        private FestivalCharacter Glover(){var glover=FestivalCharacter.Create(null,"impl-U63 glover",Color.white);made.Add(glover.gameObject);return glover;}
        private Transform Eye(RoundState state,Vector3 audience){var eye=Made("impl-U63 eye").transform;eye.position=audience+Vector3.up*1.65f;eye.rotation=Quaternion.LookRotation(Spot(state)+Vector3.up*1.3f-eye.position);return eye;}
        private static Transform Find(FestivalLightShowView lights,string path)=>lights.transform.Find(path==""?FestivalLightShowView.RootName:FestivalLightShowView.RootName+"/"+path);
        private static Transform Node(FestivalLightShowView lights,string path){var node=Find(lights,path);Assert.That(node,Is.Not.Null,"the show draws "+(path==""?"itself":path));return node;}
        private static Transform SignShown(FestivalLightShowView lights){var shape=Find(lights,"Shape sign/Shape");return shape!=null&&shape.gameObject.activeInHierarchy&&shape.GetComponent<Renderer>().enabled?shape:null;}
        private static Transform Hand(FestivalCharacter actor)=>actor.GetComponentsInChildren<Transform>(true).First(t=>t.name=="HandR");
        private static Quaternion[] Bones(FestivalCharacter actor)=>FestivalMotionLibrary.Bones.Select(name=>actor.GetComponentsInChildren<Transform>(true).First(t=>t.name==name).localRotation).ToArray();
        // Each light and passer of the show, by name, and its size this frame.
        private static Dictionary<string,float> Scales(FestivalLightShowView lights)
        {
            var scales=new Dictionary<string,float>();
            foreach(Transform node in Node(lights,""))
            {
                if(!node.gameObject.activeSelf||!(node.name.Contains("passer")||node.name.Contains("Passer")||node.name.Contains("weep")))continue;
                var shown=node.Find("Beam")??node.GetComponentInChildren<FestivalCharacter>().transform;scales[node.name]=shown.lossyScale.x;
            }
            return scales;
        }
        private float TrailWidth(FestivalSimulation game,string id)
        {
            var view=Wire(game,id);var lights=Lights();var audience=Audience(view);lights.Apply(view,id,1,Glover(),audience,Eye(view,audience),1,.02f);
            return Node(lights,"Glover trail L").GetComponent<LineRenderer>().widthMultiplier;
        }
        // The colour names as people say them: each within its own band of hues.
        private static readonly Dictionary<string,(float From,float To)> Hues=new Dictionary<string,(float,float)>{{"Red",(-15,12)},{"Orange",(18,45)},{"Yellow",(46,66)},{"Green",(90,150)},{"Blue",(200,240)},{"Purple",(260,300)}};
        private static void HueIs(Color color,string name,string what)
        {
            Color.RGBToHSV(color,out float h,out float s,out float v);float hue=name=="Red"?Mathf.DeltaAngle(0,h*360):h*360;var band=Hues[name];
            Assert.That(hue,Is.InRange(band.From,band.To),what+" reads as "+name+" ("+hue+" degrees)");Assert.That(s,Is.GreaterThan(.6f),what+" is a strong colour");Assert.That(v,Is.GreaterThan(.6f),what+" is bright");
        }
        // As VISION-4's markers: a truth's pool is 1.4 m round with no ears; a fake's is too small (.5-.7 of that) or a goblin's,
        // wider than deep with both ears.
        private static void ShadowIs(Transform shadow,bool fake,LightShowShadowTell shape,string what)
        {
            Assert.That(shadow,Is.Not.Null,what+" casts a shadow");Assert.That(shadow.gameObject.activeInHierarchy,Is.True,what+"'s shadow shows");
            var pool=shadow.Find("Pool").localScale;var ears=shadow.Find("Ears").gameObject;
            Assert.That(shadow.position.y,Is.InRange(0f,.1f),what+"'s shadow lies on the ground");
            if(!fake){Assert.That(pool.x,Is.EqualTo(1.4f).Within(1e-3f),what+": the normal pool");Assert.That(pool.z,Is.EqualTo(pool.x));Assert.That(ears.activeSelf,Is.False,what+": no ears");return;}
            if(shape==LightShowShadowTell.Small){Assert.That(pool.x/1.4f,Is.InRange(.5f,.7f),what+": a too-small pool");Assert.That(ears.activeSelf,Is.False,what+": no ears");return;}
            Assert.That(ears.activeSelf&&ears.transform.childCount==2,Is.True,what+": a goblin's ears");Assert.That(pool.x,Is.GreaterThan(pool.z),what+": a squat goblin pool");
        }
        // What the show puts on screen through the whole show, sampled, and three readings: the first sweep's glow, how many points
        // the glover's moving hand leaves in its trail and how far the first shape has grown on the frame it appears.
        private struct Reading { public float Emission,Trail,Pop; }
        private string Watch(RoundState view,bool reduced,float brightness,out Reading reading)
        {
            var lights=Lights();var glover=Glover();var audience=Audience(view);var eye=Eye(view,audience);var hand=Hand(glover);var log=new List<string>();reading=default;float popFirst=0,popFull=0;
            var sign=view.Booth.LightShow.Events.First(e=>e.Kind==LightShowEventKind.Sign);var sweep=view.Booth.LightShow.Events.First(e=>e.Kind==LightShowEventKind.Sweep);
            for(int frame=0;frame<=21*30;frame++)
            {
                float t=frame/30f;hand.position+=new Vector3(.01f,0,0);lights.Apply(view,"p0",t,glover,audience,eye,t,1/30f,reduced,brightness);
                var shape=SignShown(lights);var trail=Node(lights,"Glover trail R").GetComponent<LineRenderer>();
                if(frame==Mathf.RoundToInt(sign.StartsAt*30)&&shape!=null)popFirst=shape.localScale.x;
                if(frame==Mathf.RoundToInt((sign.StartsAt+1)*30)&&shape!=null)popFull=shape.localScale.x;
                if(frame==Mathf.RoundToInt((sweep.StartsAt+.5f)*30))reading.Emission=Node(lights,"Sweep 0/Beam").GetComponent<Renderer>().sharedMaterial.GetColor("_EmissionColor").maxColorComponent;
                if(frame==300)reading.Trail=trail.positionCount;
                if(frame%3!=0)continue;
                var line=new List<string>{t.ToString("F1")};
                foreach(Transform node in Find(lights,""))if(node.gameObject.activeSelf&&(node.name.StartsWith("Passer ")||node.name.StartsWith("Fake ")||node.name.StartsWith("Sweep ")))line.Add(node.name);
                foreach(Transform node in Find(lights,""))if(node.gameObject.activeSelf&&node.Find("Beam")!=null){Color.RGBToHSV(node.Find("Beam").GetComponent<Renderer>().sharedMaterial.color,out float h,out _,out _);line.Add(node.name+"="+Mathf.RoundToInt(h*360));}
                if(shape!=null)line.Add(shape.GetComponent<MeshFilter>().sharedMesh.name);
                if(trail.gameObject.activeSelf){Color.RGBToHSV(trail.sharedMaterial.color,out float h,out _,out _);line.Add("lights="+Mathf.RoundToInt(h*360));}
                log.Add(string.Join(" ",line));
            }
            reading.Pop=popFull>0?popFirst/popFull:-1;
            return string.Join("\n",log);
        }
    }
}
