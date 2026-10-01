using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // DANCE-7: the rhythm lane's arrows rise onto the outline arrows in the receptor wells and draw over them.
    //
    // Building the HUD in EditMode (checked for DANCE-7, for DANCE-6, DANCE-2 and DANCE-5 to build on): AddComponent<FestivalHud>()
    // alone never calls Awake in EditMode (FestivalHud is not [ExecuteAlways]), so Lane() calls it directly. Awake then builds the
    // whole interface cleanly: nothing is logged, the dance preview's RenderTexture is created, and the overlay canvas has a real
    // size (640x480 in batch mode), so rects can be measured. DestroyImmediate skips OnDestroy in EditMode, which would leak the
    // generated sprites, textures, RenderTexture and chime, so Cleanup unloads them. UpdateRhythm still needs a connected session,
    // so the lane's per-frame work is tested through public statics on the component (FestivalHud.PlaceNote).
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

        private static bool IsNote(Transform t)=>Regex.IsMatch(t.name,@"^Note \d+$");
        private static bool InWell(Transform t){for(;t!=null;t=t.parent)if(t.name.StartsWith("Receptor well "))return true;return false;}
        private static Image NoteView(RectTransform lane)=>DepthFirst(lane).Find(IsNote).GetComponent<Image>();
        // A rect's centre in the lane's own space, so lane-relative distances hold at any canvas size.
        private static Vector2 Centre(RectTransform lane,RectTransform rect)=>lane.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        private static List<Transform> DepthFirst(Transform root){var order=new List<Transform>();Walk(root,order);return order;}
        // uGUI draws a canvas's graphics in depth-first hierarchy order, so a later transform draws over an earlier one.
        private static void Walk(Transform t,List<Transform> into){into.Add(t);foreach(Transform child in t)Walk(child,into);}

        private RectTransform Lane()
        {
            var go=new GameObject("HUD under test");made.Add(go);
            var hud=go.AddComponent<FestivalHud>();
            typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            return (RectTransform)go.transform.Find("Festival HUD/Rhythm lane");
        }
    }
}
