using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // VISION-1: only the tripper's client draws vision markers. Fakes carry learnable tells (no shadow, a shimmer while the camera
    // moves, a slightly shifted hue) read from the view's Tell flag; truths are solid; a checked vision plainly shows true or false.
    public sealed class VisionMarkerTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();}

        [Test]public void OnlyTheTrippersClientDrawsMarkers()
        {
            var game=DayWithSecrets();var actors=Actors(game.State);var tripper=game.State.TripperId;
            foreach(var viewer in game.State.Players)
            {
                var markers=Markers();markers.Apply(FestivalSession.ViewFor(game,viewer.Id),View(),actors,0,.02f);
                Assert.That(markers.Shown,Is.EqualTo(viewer.Id==tripper?game.State.Visions.Count:0),viewer.Id+(viewer.Id==tripper?" (the tripper) sees every vision":" is not tripping and sees none"));
            }
            var mine=Markers();var view=FestivalSession.ViewFor(game,tripper);mine.Apply(view,View(),actors,0,.02f);
            foreach(var vision in game.State.Visions)
            {
                var marker=Marker(mine,vision);
                if(vision.NpcId=="")
                {
                    Assert.That(Vector2.Distance(Flat(marker.position),new Vector2(vision.X,vision.Z)),Is.LessThan(1e-3f),vision.Kind+" hangs over its spot");
                    Assert.That(marker.position.y,Is.GreaterThan(.5f),vision.Kind+" floats where it can be seen");
                    continue;
                }
                var body=actors(vision.NpcId).position;
                Assert.That(Vector2.Distance(Flat(marker.position),Flat(body)),Is.LessThan(1e-3f),vision.Kind+" follows "+vision.NpcId+"'s body");
                Assert.That(marker.position.y-body.y,Is.GreaterThan(2f),vision.Kind+" floats over "+vision.NpcId+"'s head");
            }

            var hidden=Actors(game.State,except:view.Visions.Find(v=>v.NpcId!="").NpcId);mine.Apply(view,View(),hidden,0,.02f);
            Assert.That(mine.Shown,Is.EqualTo(game.State.Visions.Count-view.Visions.FindAll(v=>v.NpcId==view.Visions.Find(o=>o.NpcId!="").NpcId).Count),"no marker floats over a festivalgoer who is not drawn");
            foreach(var phase in new[]{"Shopping","Spinning","Loading","CampReview"}){view.Phase=phase;mine.Apply(view,View(),actors,0,.02f);Assert.That(mine.Shown,Is.Zero,"no markers at camp ("+phase+")");}
            mine.Apply(null,View(),actors,0,.02f);Assert.That(mine.Shown,Is.Zero,"a client that left draws nothing");
            Assert.That(mine.transform.childCount,Is.Zero,"markers of a finished level are removed, not just hidden");
        }

        [Test]public void FakesCarryTheirTellsAndTruthsAreSolid()
        {
            // From the real deal: the tripper's view carries no truth, yet every fake shows its tells and every truth is solid.
            var game=DayWithSecrets();var actors=Actors(game.State);var markers=Markers();
            var view=FestivalSession.ViewFor(game,game.State.TripperId);
            Assert.That(view.Visions.Exists(v=>v.IsTrue),Is.False,"setup: the view carries no truth before a check");
            Assert.That(game.State.Visions.Exists(v=>v.IsTrue)&&game.State.Visions.Exists(v=>!v.IsTrue),Is.True,"setup: the deal has truths and fakes");
            markers.Apply(view,View(),actors,0,.02f);
            foreach(var vision in game.State.Visions)
            {
                var shadow=Shadow(markers,vision);
                Assert.That(shadow.gameObject.activeSelf,Is.EqualTo(vision.IsTrue),vision.Kind+" "+(vision.IsTrue?"truth casts a shadow":"fake casts none"));
                // On the ground right under the marker, so it reads at the festivalgoer's feet whatever the sun is doing.
                var ground=vision.NpcId==""?new Vector3(vision.X,0,vision.Z):actors(vision.NpcId).position;
                Assert.That(Vector2.Distance(Flat(shadow.position),Flat(ground)),Is.LessThan(1e-3f),vision.Kind+"'s shadow lies right under it");
                Assert.That(shadow.position.y-ground.y,Is.InRange(0f,.1f),vision.Kind+"'s shadow lies on the ground");
            }

            // Crafted: a truth and a fake of every kind, side by side.
            var state=Round();var eye=View();var crafted=Markers();var bodies=new Dictionary<string,Transform>();
            foreach(var kind in new[]{"Buyer","Narc","Clue","Stash","DoubleBuyer","Shortcut"})
                foreach(bool fake in new[]{false,true})
                {
                    var id=kind+(fake?" fake":" truth");bool place=kind=="Stash"||kind=="Shortcut";
                    state.Visions.Add(new VisionState{Id=id,Kind=kind,NpcId=place?"":id,X=bodies.Count,Z=4,Tell=fake});
                    if(!place)bodies[id]=Made(id).transform;
                }
            crafted.Apply(state,eye,id=>bodies.TryGetValue(id,out var t)?t:null,0,.02f);
            crafted.Apply(state,eye,id=>bodies.TryGetValue(id,out var t)?t:null,.5f,.02f);
            float still=Glyph(crafted,state.Visions[0]).transform.localScale.x;
            foreach(var vision in state.Visions)Assert.That(Glyph(crafted,vision).transform.localScale.x,Is.EqualTo(still).Within(1e-5f),vision.Id+": nothing shimmers while the camera is still");
            foreach(var kind in new[]{"Buyer","Narc","Clue","Stash","DoubleBuyer","Shortcut"})
            {
                Color truth=Glyph(crafted,state.Visions.Find(v=>v.Id==kind+" truth")).sharedMaterial.color,fake=Glyph(crafted,state.Visions.Find(v=>v.Id==kind+" fake")).sharedMaterial.color;
                Color.RGBToHSV(truth,out float th,out float ts,out float tv);Color.RGBToHSV(fake,out float fh,out float fs,out float fv);
                float shift=Mathf.Abs(Mathf.DeltaAngle(th*360,fh*360));
                Assert.That(shift,Is.InRange(8f,40f),kind+": a fake is slightly off in colour ("+shift+" degrees of hue)");
                Assert.That(new[]{fs,fv},Is.EqualTo(new[]{ts,tv}).Within(.02f),kind+": only the hue is off, not the brightness");
            }

            // Turning the head or walking moves the camera; either sets the fakes shimmering and leaves the truths solid.
            foreach(var (motion,move) in new (string,Action<Transform>)[]{("turns",v=>v.Rotate(0,3,0)),("walks",v=>v.position+=new Vector3(0,0,.04f))})
            {
                var moving=Markers();var head=View();var peak=new Dictionary<string,float>();
                for(int frame=0;frame<=30;frame++)
                {
                    if(frame>0)move(head);
                    moving.Apply(state,head,id=>bodies.TryGetValue(id,out var t)?t:null,.5f+frame*.02f,.02f);
                    foreach(var vision in state.Visions){peak.TryGetValue(vision.Id,out float most);peak[vision.Id]=Mathf.Max(most,Mathf.Abs(Glyph(moving,vision).transform.localScale.x/still-1));}
                }
                foreach(var vision in state.Visions)
                    if(vision.Tell)Assert.That(peak[vision.Id],Is.GreaterThan(.03f),vision.Id+" shimmers while the camera "+motion);
                    else Assert.That(peak[vision.Id],Is.LessThan(1e-4f),vision.Id+" stays solid while the camera "+motion);
            }
        }

        // A 125 Hz mouse on a 240 Hz screen turns the camera on every other frame only; a fake keeps shimmering in between
        // instead of snapping back to solid each frame, which would read as flicker.
        [Test]public void AFakesShimmerHoldsBetweenTheMousesReports()
        {
            var state=Round();state.Visions.Add(new VisionState{Id="fake",Kind="Stash",X=0,Z=4,Tell=true});
            var markers=Markers();var view=View();markers.Apply(state,view,_=>null,.013f,1/240f);
            float still=Glyph(markers,"fake").transform.localScale.x;int snaps=0;
            for(int frame=1;frame<=60;frame++)
            {
                if(frame%2==1)view.Rotate(0,1.5f,0);
                markers.Apply(state,view,_=>null,.013f+frame/240f,1/240f);
                if(frame>10&&Mathf.Abs(Glyph(markers,"fake").transform.localScale.x/still-1)<1e-6f)snaps++;
            }
            Assert.That(snaps,Is.Zero,"frames where a turning camera's fake snapped back to solid");
        }

        [Test]public void CheckedVisionsPlainlyShowTrueOrFalse()
        {
            var state=Round();var bodies=new Dictionary<string,Transform>{{"a",Made("a").transform},{"b",Made("b").transform},{"c",Made("c").transform}};
            Func<string,Transform> actorFor=id=>bodies.TryGetValue(id,out var t)?t:null;
            state.Visions.Add(new VisionState{Id="open",Kind="Buyer",NpcId="a",Tell=true});
            state.Visions.Add(new VisionState{Id="true",Kind="Buyer",NpcId="b",Tell=false,IsTrue=true,Confirmed=true});
            state.Visions.Add(new VisionState{Id="false",Kind="Buyer",NpcId="c",Tell=true,IsTrue=false,Confirmed=true});
            var markers=Markers();var view=View();markers.Apply(state,view,actorFor,0,.02f);
            Assert.That(Label(markers,"open"),Is.Empty,"an unchecked vision makes no claim");
            Assert.That(Label(markers,"true"),Is.EqualTo("TRUE"),"a checked truth says so");
            Assert.That(Label(markers,"false"),Is.EqualTo("FALSE"),"a checked fake says so");

            var plain=Markers();var solid=new RoundState{Phase="Playing"};solid.Visions.Add(new VisionState{Id="true",Kind="Buyer",NpcId="b"});plain.Apply(solid,View(),actorFor,0,.02f);
            Assert.That(Glyph(markers,"true").sharedMaterial.color,Is.EqualTo(Glyph(plain,"true").sharedMaterial.color),"a checked truth keeps its kind's colour");
            Color.RGBToHSV(Glyph(markers,"false").sharedMaterial.color,out _,out float saturation,out _);
            Assert.That(saturation,Is.LessThan(.15f),"a checked fake goes grey");
            Assert.That(Shadow(markers,"false").gameObject.activeSelf,Is.True,"a checked fake is plainly marked, so it drops its tells");
            float still=Glyph(markers,"true").transform.localScale.x;
            for(int frame=1;frame<=20;frame++){view.Rotate(0,4,0);markers.Apply(state,view,actorFor,frame*.02f,.02f);Assert.That(Glyph(markers,"false").transform.localScale.x,Is.EqualTo(still).Within(1e-5f),"a checked fake no longer shimmers");}
            Assert.That(Label(markers,"true").Length>0&&Marker(markers,"true").Find("Label").rotation==view.rotation,Is.True,"the verdict faces the camera");

            // Night: finding the next clue holder replaces the link's visions; the old link's markers go with them.
            state.Visions.RemoveAll(v=>v.Id=="open");markers.Apply(state,view,actorFor,1,.02f);
            Assert.That(markers.Shown,Is.EqualTo(2),"a vision the host dropped is no longer drawn");
            Assert.That(markers.transform.Find("Vision open"),Is.Null,"and its marker is removed");
        }

        [Test]public void KindsLookDistinctAndStackOverOnePerson()
        {
            var state=Round();var bodies=new Dictionary<string,Transform>();
            foreach(var kind in new[]{"Buyer","Narc","Clue"}){state.Visions.Add(new VisionState{Id=kind,Kind=kind,NpcId=kind});bodies[kind]=Made(kind).transform;}
            // A buyer who pays double can also carry a buyer mark: one festivalgoer, two visions.
            state.Visions.Add(new VisionState{Id="DoubleBuyer",Kind="DoubleBuyer",NpcId="Buyer"});
            foreach(var kind in new[]{"Stash","Shortcut"})state.Visions.Add(new VisionState{Id=kind,Kind=kind,X=3,Z=3});
            var markers=Markers();Func<string,Transform> actorFor=id=>bodies.TryGetValue(id,out var t)?t:null;var view=View();markers.Apply(state,view,actorFor,0,.02f);
            Assert.That(markers.Shown,Is.EqualTo(state.Visions.Count),"every vision is drawn");
            Assert.That(()=>markers.Apply(state,view,actorFor,.02f,.02f),Is.Not.AllocatingGCMemory(),"drawing a frame makes no garbage");
            Assert.That(Mathf.Abs(Marker(markers,"DoubleBuyer").position.y-Marker(markers,"Buyer").position.y),Is.GreaterThan(Glyph(markers,"Buyer").bounds.size.y),"two visions over one festivalgoer stack instead of overlapping");
            var looks=new HashSet<string>();
            foreach(var kind in new[]{"Buyer","Narc","Clue","Stash"})
            {
                var glyph=Glyph(markers,kind);
                Assert.That(looks.Add(glyph.GetComponent<MeshFilter>().sharedMesh.name),Is.True,kind+" has its own shape, so colour is never the only cue");
                Assert.That(looks.Add(ColorUtility.ToHtmlStringRGB(glyph.sharedMaterial.color)),Is.True,kind+" has its own colour");
            }
            foreach(var secret in new[]{"DoubleBuyer","Shortcut"})
            {
                Assert.That(Glyph(markers,secret).GetComponent<MeshFilter>().sharedMesh,Is.EqualTo(Glyph(markers,"Stash").GetComponent<MeshFilter>().sharedMesh),secret+" looks like the other secrets");
                Assert.That(Glyph(markers,secret).sharedMaterial.color,Is.EqualTo(Glyph(markers,"Stash").sharedMaterial.color),secret+" shares the secrets' colour");
            }
        }

        // A day level at dose 3 or 4 on the real deal, so the tripper sees truths, fakes and a secret. Three friends.
        private static FestivalSimulation DayWithSecrets()
        {
            for(int seed=1;seed<400;seed++)
            {
                var game=new FestivalSimulation(seed);game.State.LevelIndex=0;
                foreach(var id in new[]{"host","friend","guest"}){var p=game.AddPlayer(id,id);p.X=0;p.Z=19;game.Execute(id,new GameCommand{Id="ready_"+id,Kind="Ready"});}
                game.Tick(5.2+FestivalSimulation.SpinSeconds+.1);
                foreach(var p in game.State.Players)game.Execute(p.Id,new GameCommand{Id="map_"+p.Id,Kind="MapReady"});
                if(game.State.Phase=="Playing"&&game.State.Doses.Find(d=>d.PlayerId==game.State.TripperId).Dose>=3)return game;
            }
            throw new InvalidOperationException("setup: no seed spins a day tripper to dose 3");
        }
        private static RoundState Round()=>new RoundState{Phase="Playing"};
        // Every festivalgoer drawn where the state puts them, the way the session draws its actors.
        private Func<string,Transform> Actors(RoundState state,string except="")
        {
            var bodies=new Dictionary<string,Transform>();
            foreach(var npc in state.Npcs)if(npc.Id!=except){var body=Made("Actor "+npc.Id).transform;body.position=new Vector3(npc.X,0,npc.Z);bodies[npc.Id]=body;}
            return id=>bodies.TryGetValue(id,out var t)?t:null;
        }
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
        private FestivalVisionMarkers Markers()=>Made("Vision markers").AddComponent<FestivalVisionMarkers>();
        private Transform View(){var view=Made("View").transform;view.position=new Vector3(0,1.65f,-10);return view;}
        private static Vector2 Flat(Vector3 at)=>new Vector2(at.x,at.z);
        private static Transform Marker(FestivalVisionMarkers markers,VisionState vision)=>Marker(markers,vision.Id);
        private static Transform Marker(FestivalVisionMarkers markers,string id)
        {
            var marker=markers.transform.Find("Vision "+id);Assert.That(marker,Is.Not.Null,"a marker for vision "+id);return marker;
        }
        private static MeshRenderer Glyph(FestivalVisionMarkers markers,VisionState vision)=>Glyph(markers,vision.Id);
        private static MeshRenderer Glyph(FestivalVisionMarkers markers,string id)=>Marker(markers,id).Find("Glyph").GetComponent<MeshRenderer>();
        private static Transform Shadow(FestivalVisionMarkers markers,VisionState vision)=>Shadow(markers,vision.Id);
        private static Transform Shadow(FestivalVisionMarkers markers,string id)
        {
            var shadow=Marker(markers,id).Find("Shadow");Assert.That(shadow,Is.Not.Null,"vision "+id+" has a shadow to show");return shadow;
        }
        private static string Label(FestivalVisionMarkers markers,string id)=>Marker(markers,id).Find("Label").GetComponent<TextMesh>().text;
    }
}
