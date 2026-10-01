using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // DANCE-7: the rhythm lane's arrows rise onto the outline arrows in the receptor wells and draw over them.
    // DANCE-6: each well shows its lane's WASD key, as currently bound, upright on its outline arrow.
    // DANCE-5: a beat can hold up to four notes, so the lane grows a note view for every note of a chart and counts its notes.
    // DANCE-2: the live dancer view shows beside every four-lane challenge, and the character in it dances through each one.
    //
    // Building the HUD in EditMode (checked for DANCE-7, for DANCE-6, DANCE-2 and DANCE-5 to build on): AddComponent<FestivalHud>()
    // alone never calls Awake in EditMode (FestivalHud is not [ExecuteAlways]), so Hud() calls it directly. Awake then builds the
    // whole interface cleanly: nothing is logged, the dance preview's RenderTexture is created, and the overlay canvas has a real
    // size (640x480 in batch mode), so rects can be measured. DestroyImmediate skips OnDestroy in EditMode, which would leak the
    // generated sprites, textures, RenderTexture and chime, so Cleanup unloads them. UpdateRhythm still needs a connected session,
    // so the lane's per-frame work is tested through public members it calls (FestivalHud.PlaceNote, ShowRhythmKeys, ShowChart,
    // DrawChart).
    public sealed class HudRhythmTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup()
        {
            foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);
            if(made.Count>0)UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            made.Clear();
        }

        [Test]public void NotesDrawOverTheWellsAndUnderTheJudgment()
        {
            var lane=Lane();var order=DepthFirst(lane);
            int lastBackdrop=-1,firstWell=int.MaxValue,lastWell=-1,firstNote=int.MaxValue,lastNote=-1,firstText=int.MaxValue,notes=0;
            for(int i=0;i<order.Count;i++)
            {
                var t=order[i];
                if(t.name.StartsWith("Note column ")||t.name.StartsWith("Column divider ")||t.name.StartsWith("Beat grid "))lastBackdrop=i;
                if(InWell(t)){firstWell=Math.Min(firstWell,i);lastWell=i;}
                if(IsNote(t)){firstNote=Math.Min(firstNote,i);lastNote=i;notes++;}
                if(t.name=="Judgment"||t.name=="Combo"||t.name=="Timing")firstText=Math.Min(firstText,i);
            }
            Assert.That(notes,Is.GreaterThan(0),"the lane holds note views");
            Assert.That(lastWell,Is.GreaterThan(-1),"the lane holds receptor wells");
            Assert.That(lastBackdrop,Is.LessThan(firstWell),"the tracks and beat grid draw under the wells");
            Assert.That(firstNote,Is.GreaterThan(lastWell),"every note draws over every receptor well and its outline arrow");
            Assert.That(lastNote,Is.LessThan(firstText),"every note draws under the judgment, combo and timing text");
            foreach(var t in order)if(IsNote(t))Assert.That(t.parent.name,Is.EqualTo("Notes"),t.name+" lives in the lane's Notes container");
        }

        [Test]public void TheOrangeJudgmentLineIsGone()
        {
            Assert.That(Lane().Find("Judgment line"),Is.Null,"the outline arrows are the target, so nothing marks a second one");
        }

        [Test]public void ANoteOnItsTimeIsCentredOnItsLanesOutline()
        {
            var lane=Lane();var view=NoteView(lane);float tolerance=.02f*lane.rect.height;
            Assert.That(tolerance,Is.GreaterThan(0),"the lane has a measurable height");
            var looks=new List<EffectDefinition>{new EffectDefinition{Id="",LeadSeconds=2}};looks.AddRange(Catalog.Effects);
            foreach(var look in looks)for(int direction=0;direction<4;direction++)
            {
                FestivalHud.PlaceNote(view,new RhythmNote{Id=5+direction,Direction=direction,TimeSeconds=4},0,look.LeadSeconds,look.Id,false);
                var outline=(RectTransform)lane.Find("Receptor well "+direction+"/Target arrow");
                Assert.That(Vector2.Distance(Centre(lane,view.rectTransform),Centre(lane,outline)),Is.LessThanOrEqualTo(tolerance),
                    "lane "+direction+(look.Id==""?" sober":" on "+look.Id)+": a note at its exact time sits on its outline arrow");
            }
        }

        [Test]public void AMissedNoteKeepsRisingPastTheOutlineAndFadesOut()
        {
            var lane=Lane();var view=NoteView(lane);var note=new RhythmNote{Id=3,Direction=2,TimeSeconds=4};
            FestivalHud.PlaceNote(view,note,.5,2,"",false);
            Assert.That(view.color.a,Is.EqualTo(1).Within(1e-4),"a note on its way up is solid");
            FestivalHud.PlaceNote(view,note,0,2,"",false);float onTime=Centre(lane,view.rectTransform).y;
            Assert.That(view.color.a,Is.EqualTo(1).Within(1e-4),"a note on its time is solid");
            FestivalHud.PlaceNote(view,note,-.09,2,"",false);float halfway=Centre(lane,view.rectTransform).y;
            Assert.That(halfway,Is.GreaterThan(onTime),"a missed note keeps rising past the outline");
            Assert.That(view.color.a,Is.EqualTo(.5f).Within(.01),"halfway through the 0.18 s overshoot it is half faded");
            FestivalHud.PlaceNote(view,note,-.18,2,"",false);
            Assert.That(Centre(lane,view.rectTransform).y,Is.GreaterThan(halfway),"it rises for the whole overshoot");
            Assert.That(view.color.a,Is.EqualTo(0).Within(1e-4),"it has faded out when the overshoot ends");
        }

        [Test]public void EachWellShowsItsLanesWasdKey()
        {
            var hud=Hud();var lane=LaneOf(hud);string[] wasd={"a","s","w","d"};
            using(var input=new FestivalInput("hud-rhythm-keys-test"))
            {
                hud.ShowRhythmKeys(input);
                for(int direction=0;direction<4;direction++)
                    Assert.That(Key(lane,direction)?.text,Is.EqualTo(KeyName("<Keyboard>/"+wasd[direction])),"lane "+direction+" shows its WASD key (A / S / W / D on a US layout), not its arrow");
            }
        }

        [Test]public void AReboundLaneShowsItsNewKey()
        {
            var hud=Hud();var lane=LaneOf(hud);
            using(var input=new FestivalInput("hud-rhythm-keys-test"))
            {
                hud.ShowRhythmKeys(input);
                Assert.That(KeyName("<Keyboard>/j"),Is.Not.EqualTo(KeyName("<Keyboard>/w")),"the new key reads differently from the old one");
                input.Notes[2].ApplyBindingOverride(1,"<Keyboard>/j");
                hud.ShowRhythmKeys(input);
                Assert.That(Key(lane,2)?.text,Is.EqualTo(KeyName("<Keyboard>/j")),"the up lane, its WASD key rebound to J, shows J");
                Assert.That(Key(lane,0)?.text,Is.EqualTo(KeyName("<Keyboard>/a")),"the other lanes keep their keys");
                Assert.That(Key(lane,3)?.text,Is.EqualTo(KeyName("<Keyboard>/d")),"the other lanes keep their keys");
            }
        }

        [Test]public void KeyLabelsSitUprightOnTheirOutlineArrows()
        {
            var lane=Lane();float tolerance=.02f*lane.rect.height;int turned=0;
            for(int direction=0;direction<4;direction++)
            {
                var arrow=(RectTransform)lane.Find("Receptor well "+direction+"/Target arrow");var key=Key(lane,direction);
                if(Quaternion.Angle(lane.rotation,arrow.rotation)>1)turned++;
                Assert.That(key,Is.Not.Null,"well "+direction+" holds a key label");
                Assert.That(key.transform.parent,Is.SameAs(arrow.parent),"the key is the outline arrow's sibling, so it doesn't turn with it");
                Assert.That(key.transform.GetSiblingIndex(),Is.GreaterThan(arrow.GetSiblingIndex()),"the key draws over its outline arrow");
                Assert.That(Quaternion.Angle(lane.rotation,key.transform.rotation),Is.LessThan(.01f),"lane "+direction+"'s key reads upright");
                Assert.That(Vector2.Distance(Centre(lane,key.rectTransform),Centre(lane,arrow)),Is.LessThanOrEqualTo(tolerance),"lane "+direction+"'s key sits on its outline arrow");
                Assert.That(key.fontStyle,Is.EqualTo(FontStyle.Bold),"the key is bold");
                Assert.That(key.resizeTextForBestFit,Is.True,"the key shrinks to fit its well");
                Assert.That(key.resizeTextMinSize,Is.InRange(1,key.resizeTextMaxSize),"down to a minimum size");
            }
            Assert.That(turned,Is.EqualTo(3),"the left, down and right outline arrows are turned, so an upright key means something");
        }

        [Test]public void TheBottomLineReadsArrowsOrWasd()
        {
            Assert.That(Lane().Find("Controls").GetComponent<Text>().text,Is.EqualTo("ARROWS OR WASD"));
        }

        // A 16-beat chart of nothing but sixteenth runs is 64 notes, twice the lane's first pool of note views.
        [Test]public void TheLaneShowsEveryNoteOfAChartOfSixteenthRuns()
        {
            var hud=Hud();var lane=LaneOf(hud);float tolerance=.02f*lane.rect.width;
            var chart=new RhythmChart{DurationSeconds=2+16*.5};
            for(int i=0;i<64;i++)chart.Notes.Add(new RhythmNote{Id=i,Direction=i%4,TimeSeconds=2+i*.5/4});
            hud.ShowChart(new InteractionState{Id="runs",Kind="Dance",NoteCount=16,BeatSeconds=.5},chart);
            // Every miss is listed, so a counter that counts beats and a pool that stops at 32 show up together.
            var wrong=new List<string>();
            hud.DrawChart(0,2,"",false);
            string counter=lane.Find("Tempo and steps").GetComponent<Text>().text;
            if(counter!="0 / 64   •   120 BPM")wrong.Add("the step counter reads \""+counter+"\", not the chart's 64 notes");
            int shown=0;
            foreach(var note in chart.Notes)
            {
                hud.DrawChart(note.TimeSeconds-1,2,"",false);
                var view=(RectTransform)lane.Find("Notes/Note "+note.Id);
                if(view==null||!view.gameObject.activeSelf)continue;
                shown++;
                var outline=(RectTransform)lane.Find("Receptor well "+note.Direction+"/Target arrow");
                if(Mathf.Abs(Centre(lane,view).x-Centre(lane,outline).x)>tolerance)wrong.Add("note "+note.Id+" is out of its lane");
            }
            if(shown!=64)wrong.Add("only "+shown+" of the 64 notes show a second before their time");
            Assert.That(wrong,Is.Empty,string.Join("; ",wrong));
        }

        // A sale, a chat and a talk with security are danced (FestivalSimulation.DancesVisibly), so every client, the dancer's own
        // live view included, shows the player dancing with whoever they're talking to.
        [Test]public void EveryoneSeesASaleAChatAndSecurityDanced()
        {
            foreach(var kind in new[]{"Sale","Conversation","Police"})
            {
                var game=new FestivalSimulation(5);var dancer=game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");game.State.Phase="Playing";game.State.Npcs.Clear();
                game.State.Npcs.Add(new NpcState{Id="partner",Kind=kind=="Police"?"Cop":"Wook",X=0,Z=2,Yaw=180,CanTalk=true});
                dancer.X=0;dancer.Z=0;dancer.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                var started=game.Execute("p0",new GameCommand{Id="dance2_"+kind,Kind=kind=="Sale"?"StartSale":kind,TargetId="partner",ItemId=kind=="Sale"?"stock_lsd":""});
                Assert.That(started.Accepted,Is.True,"setup: the "+kind+" starts: "+started.Reason);
                foreach(var viewer in game.State.Players)
                    Assert.That(FestivalSession.ViewFor(game,viewer.Id).Players.Find(p=>p.Id=="p0").VisualPose,Is.EqualTo("Dance"),viewer.Id+" sees P0 dance through the "+kind);
            }
        }

        private static Text Key(RectTransform lane,int direction)=>lane.Find("Receptor well "+direction+"/Key")?.GetComponent<Text>();
        // What a binding to this key shows: the attached keyboard's name for it (its layout) when there is one, else the Keyboard
        // layout's default. Derived from the key's path, so the tests hold on any keyboard layout.
        private static string KeyName(string path)=>InputControlPath.ToHumanReadableString(path,InputControlPath.HumanReadableStringOptions.OmitDevice|InputControlPath.HumanReadableStringOptions.UseShortNames,InputSystem.FindControl(path));
        private static bool IsNote(Transform t)=>Regex.IsMatch(t.name,@"^Note \d+$");
        private static bool InWell(Transform t){for(;t!=null;t=t.parent)if(t.name.StartsWith("Receptor well "))return true;return false;}
        private static Image NoteView(RectTransform lane)=>DepthFirst(lane).Find(IsNote).GetComponent<Image>();
        // A rect's centre in the lane's own space, so lane-relative distances hold at any canvas size.
        private static Vector2 Centre(RectTransform lane,RectTransform rect)=>lane.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        private static List<Transform> DepthFirst(Transform root){var order=new List<Transform>();Walk(root,order);return order;}
        // uGUI draws a canvas's graphics in depth-first hierarchy order, so a later transform draws over an earlier one.
        private static void Walk(Transform t,List<Transform> into){into.Add(t);foreach(Transform child in t)Walk(child,into);}

        private RectTransform Lane()=>LaneOf(Hud());
        private static RectTransform LaneOf(FestivalHud hud)=>(RectTransform)hud.transform.Find("Festival HUD/Rhythm lane");
        private FestivalHud Hud()
        {
            var go=new GameObject("HUD under test");made.Add(go);
            var hud=go.AddComponent<FestivalHud>();
            typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            return hud;
        }
    }
}
