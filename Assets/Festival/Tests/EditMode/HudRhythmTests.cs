using System;
using System.Collections.Generic;
using System.Linq;
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
            foreach(var look in looks)foreach(int dose in new[]{0,4})foreach(bool reducedMotion in new[]{false,true})for(int direction=0;direction<4;direction++)
            {
                FestivalHud.PlaceNote(view,new RhythmNote{Id=5+direction,Direction=direction,TimeSeconds=4},0,look.LeadSeconds,look.Id,reducedMotion,dose);
                var outline=(RectTransform)lane.Find("Receptor well "+direction+"/Target arrow");
                Assert.That(Vector2.Distance(Centre(lane,view.rectTransform),Centre(lane,outline)),Is.LessThanOrEqualTo(tolerance),
                    "lane "+direction+(look.Id==""?" sober":" on "+look.Id)+": a note at its exact time sits on its outline arrow");
            }
        }

        // Exercise real chart placement, beyond the already-shipped pure Path values.
        [TestCase("lsd",0,false)]
        [TestCase("lsd",0,true)]
        [TestCase("lsd",3,false)]
        [TestCase("lsd",3,true)]
        [TestCase("ketamine",0,false)]
        [TestCase("ketamine",0,true)]
        public void ADosedArrowUsesTheFlightButStillLandsOnItsOutline(string effect,int direction,bool reducedMotion)
        {
            var hud=Hud();var lane=LaneOf(hud);var note=new RhythmNote{Id=7,Direction=direction,TimeSeconds=4};
            var chart=new RhythmChart{DurationSeconds=5,Notes=new List<RhythmNote>{note}};
            hud.ShowChart(new InteractionState{Id="trip13-flight",Kind="Dance",NoteCount=1,BeatSeconds=.5},chart);
            var view=NoteView(lane);double lead=Catalog.FindEffect(effect).LeadSeconds;
            if(effect=="lsd")
            {
                hud.DrawChart(note.TimeSeconds-lead*.5,lead,effect,reducedMotion,4);
                float centre=(view.rectTransform.anchorMin.x+view.rectTransform.anchorMax.x)*.5f;
                Assert.That(direction==0?centre-.185f:.815f-centre,Is.GreaterThanOrEqualTo(.21f*.5f),"a dose-4 Tongue Stamps note travels at least half a lane inward at half its rise");
                hud.DrawChart(note.TimeSeconds-lead,lead,effect,reducedMotion,4);
                Assert.That(view.rectTransform.anchorMin.x,Is.InRange(.185f,.815f),"both outer lanes enter inside the panel using their actual Direction");
            }
            else
            {
                hud.DrawChart(note.TimeSeconds-lead*.6,lead,effect,reducedMotion,4);float first=view.rectTransform.anchorMin.y;
                hud.DrawChart(note.TimeSeconds-lead*.4,lead,effect,reducedMotion,4);
                Assert.That(view.rectTransform.anchorMin.y,Is.EqualTo(first).Within(1e-6f),"a dose-4 Pony Dust note stays still during its shipped stall");
            }
            hud.DrawChart(note.TimeSeconds,lead,effect,reducedMotion,4);
            var outline=(RectTransform)lane.Find("Receptor well "+direction+"/Target arrow");
            Assert.That(Vector2.Distance(Centre(lane,view.rectTransform),Centre(lane,outline)),Is.LessThanOrEqualTo(.02f*lane.rect.height),"the travel twist still lands on its original beat and receptor");
        }

        // Worked flight samples on the lane's .11 spawn / .8305 receptor anchors; these are independent literal expectations.
        [TestCase("mushrooms",.125,.1599276f,false)]
        [TestCase("mushrooms",.125,.1599276f,true)]
        [TestCase("ecstasy",.5,.3476764f,false)]
        [TestCase("ecstasy",.5,.3476764f,true)]
        [TestCase("weed",.5,.5928236f,false)]
        [TestCase("weed",.5,.5928236f,true)]
        public void EveryDosedSubstanceReachesItsAuthoredFlight(string effect,double progress,float y,bool reducedMotion)
        {
            var hud=Hud();var lane=LaneOf(hud);var note=new RhythmNote{Id=7,Direction=2,TimeSeconds=4};
            hud.ShowChart(new InteractionState{Id="dose-flight",Kind="Dance",NoteCount=1,BeatSeconds=.5},new RhythmChart{DurationSeconds=5,Notes=new List<RhythmNote>{note}});
            double lead=Catalog.FindEffect(effect).LeadSeconds;
            hud.DrawChart(note.TimeSeconds-lead*(1-progress),lead,effect,reducedMotion,4);
            Assert.That(NoteView(lane).rectTransform.anchorMin.y,Is.EqualTo(y).Within(.000001f),"the actual chart uses this substance's dose-four travel, including under reduced motion");
        }

        [TestCase("",0,.8629225f,.895345f)]
        [TestCase("lsd",4,.8629225f,.895345f)]
        [TestCase("mushrooms",4,.8629225f,.895345f)]
        [TestCase("ecstasy",4,.8710281f,.9115562f)]
        [TestCase("ketamine",4,.8575187f,.8845375f)]
        [TestCase("weed",4,.8629225f,.895345f)]
        public void AMissedNoteKeepsRisingPastTheOutlineAndFadesOut(string effect,int dose,float halfwayY,float endY)
        {
            var lane=Lane();var view=NoteView(lane);var note=new RhythmNote{Id=3,Direction=2,TimeSeconds=4};double lead=Catalog.FindEffect(effect)?.LeadSeconds??2;
            FestivalHud.PlaceNote(view,note,.5,lead,effect,false,dose);
            Assert.That(view.color.a,Is.EqualTo(1).Within(1e-4),"a note on its way up is solid");
            FestivalHud.PlaceNote(view,note,0,lead,effect,false,dose);float onTime=Centre(lane,view.rectTransform).y;
            Assert.That(view.color.a,Is.EqualTo(1).Within(1e-4),"a note on its time is solid");
            FestivalHud.PlaceNote(view,note,-.09,lead,effect,false,dose);float halfway=Centre(lane,view.rectTransform).y;
            Assert.That(halfway,Is.GreaterThan(onTime),"a missed note keeps rising past the outline");
            Assert.That(view.rectTransform.anchorMin.y,Is.EqualTo(halfwayY).Within(.000001f),"doses keep the original linear overshoot");
            Assert.That(view.color.a,Is.EqualTo(.5f).Within(.01),"halfway through the 0.18 s overshoot it is half faded");
            FestivalHud.PlaceNote(view,note,-.18,lead,effect,false,dose);
            Assert.That(Centre(lane,view.rectTransform).y,Is.GreaterThan(halfway),"it rises for the whole overshoot");
            Assert.That(view.rectTransform.anchorMin.y,Is.EqualTo(endY).Within(.000001f));
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
                var bottom=lane.Find("Controls").GetComponent<Text>();
                string alternates=(KeyName("<Keyboard>/j")+KeyName("<Keyboard>/a")+KeyName("<Keyboard>/s")+KeyName("<Keyboard>/d")).ToUpperInvariant();
                Assert.That(bottom.text,Is.EqualTo("ARROWS OR "+alternates),"the bottom line names the alternates as bound, in W-A-S-D order");
                input.Notes[2].ApplyBindingOverride(0,"<Keyboard>/t");
                hud.ShowRhythmKeys(input);
                Assert.That(bottom.text,Does.StartWith(KeyName("<Keyboard>/t").ToUpperInvariant()).And.EndWith(" OR "+alternates),"a moved arrow names the arrow keys as bound");
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

        // DANCE-12: beside the lane keys, the bottom line says how to stop, and a fill bar under those words grows while G is held
        // (FestivalHud.ShowStopHold, from UpdateKeyboard's hold): empty until then, full at the stop.
        [Test]public void TheBottomLineSaysHoldGToStopAndFillsAsItIsHeld()
        {
            var hud=Hud();var lane=LaneOf(hud);
            var stop=lane.Find("Stop hold")?.GetComponent<Text>();var keys=(RectTransform)lane.Find("Controls");
            Assert.That(stop,Is.Not.Null,"the lane's bottom line has a stop prompt");
            Assert.That(stop.text,Is.EqualTo("HOLD G TO STOP DANCING"));
            Assert.That(Bounds(lane,stop.rectTransform).Overlaps(Bounds(lane,keys)),Is.False,"the stop prompt sits beside \"ARROWS OR WASD\", not over it");
            var fill=(RectTransform)lane.Find("Stop hold fill");
            Assert.That(fill,Is.Not.Null,"a fill bar sits on the stop prompt");
            Assert.That(fill.GetSiblingIndex(),Is.LessThan(stop.transform.GetSiblingIndex()),"the fill draws under the words");
            Assert.That(fill.gameObject.activeSelf,Is.False,"nothing held, nothing filled");
            float width=stop.rectTransform.rect.width;
            Assert.That(width,Is.GreaterThan(0),"setup: the prompt has a measurable width");
            foreach(float held in new[]{.25f,.5f,1f})
            {
                hud.ShowStopHold(held);
                Assert.That(fill.gameObject.activeSelf,Is.True,held+" of the hold shows the fill");
                Assert.That(fill.rect.width,Is.EqualTo(held*width).Within(.01f*width),held+" of the hold fills "+held+" of the prompt");
                Assert.That(Bounds(lane,fill).xMin,Is.EqualTo(Bounds(lane,stop.rectTransform).xMin).Within(.01f*width),"the fill grows from the prompt's left edge");
            }
            hud.ShowStopHold(0);
            Assert.That(fill.gameObject.activeSelf,Is.False,"let go, the fill empties");
        }

        [Test]public void HoldReleaseFeedbackMatchesTheJudgeAndIgnoresUnmatchedReleases()
        {
            var hud=Hud();var chart=new RhythmChart{DurationSeconds=4};
            chart.Notes.Add(new RhythmNote{Id=0,Direction=0,TimeSeconds=2,HoldSeconds=1});
            var interaction=new InteractionState{Id="hold-feedback",Kind="Dance",NoteCount=1,BeatSeconds=.5};
            hud.ShowChart(interaction,chart);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var feedback=typeof(FestivalHud).GetMethod("ProcessRhythmFeedback",flags);
            string Judgment()=>(string)typeof(FestivalHud).GetField("lastRhythmJudgment",flags).GetValue(hud);
            int Combo()=>(int)typeof(FestivalHud).GetField("rhythmComboCount",flags).GetValue(hud);
            interaction.Inputs.Add(new RhythmInput{Direction=0,TimeSeconds=2});
            feedback.Invoke(hud,new object[]{interaction,2.0});
            Assert.That(Judgment(),Is.EqualTo("PERFECT"));Assert.That(Combo(),Is.EqualTo(1));
            interaction.Inputs.Add(new RhythmInput{Direction=3,TimeSeconds=2.1,Release=true});
            feedback.Invoke(hud,new object[]{interaction,2.1});
            Assert.That(Judgment(),Is.EqualTo("PERFECT"),"a release outside a hold must be ignored, as on the host");
            Assert.That(Combo(),Is.EqualTo(1));
            interaction.Inputs.Add(new RhythmInput{Direction=0,TimeSeconds=2.2,Release=true});
            feedback.Invoke(hud,new object[]{interaction,2.2});
            Assert.That(Judgment(),Is.EqualTo("LET GO"));Assert.That(Combo(),Is.Zero);
        }

        [TestCase("lsd",false)]
        [TestCase("lsd",true)]
        [TestCase("mushrooms",false)]
        [TestCase("mushrooms",true)]
        [TestCase("ecstasy",false)]
        [TestCase("ecstasy",true)]
        [TestCase("ketamine",false)]
        [TestCase("ketamine",true)]
        [TestCase("weed",false)]
        [TestCase("weed",true)]
        public void AHoldShowsATailThatShrinksOnceHit(string effect,bool reducedMotion)
        {
            var hud=Hud();var lane=LaneOf(hud);var chart=new RhythmChart{DurationSeconds=4};double lead=Catalog.FindEffect(effect).LeadSeconds;
            chart.Notes.Add(new RhythmNote{Id=41,Direction=0,TimeSeconds=2,HoldSeconds=1});
            var interaction=new InteractionState{Id="hold-tail",Kind="Dance",NoteCount=1,BeatSeconds=.5};
            hud.ShowChart(interaction,chart);hud.DrawChart(1,lead,effect,reducedMotion,4);
            var tail=lane.Find("Hold tails/Tail 0")?.GetComponent<Image>();
            Assert.That(tail,Is.Not.Null,"a hold gets a plain pooled tail below its arrow");
            var head=(RectTransform)lane.Find("Notes/Note 0");var target=(RectTransform)lane.Find("Receptor well 0/Target arrow");
            Assert.That(tail.gameObject.activeSelf,Is.True);Assert.That(tail.sprite,Is.Null);Assert.That(tail.raycastTarget,Is.False);
            Assert.That(tail.color.a,Is.EqualTo(.5f));Assert.That(tail.transform.parent,Is.Not.SameAs(head));
            Assert.That(tail.transform.parent.GetSiblingIndex(),Is.LessThan(head.parent.GetSiblingIndex()));
            Assert.That(Centre(lane,tail.rectTransform).x,Is.EqualTo(Centre(lane,target).x).Within(.01f));
            Assert.That(Quaternion.Angle(lane.rotation,tail.transform.rotation),Is.LessThan(.01f),"tails never inherit arrow wobble");
            float before=tail.rectTransform.rect.height;
            interaction.Inputs.Add(new RhythmInput{Direction=0,TimeSeconds=2});
            Feedback(hud,interaction,2.5);hud.DrawChart(2.5,lead,effect,reducedMotion,4);
            Assert.That(head.gameObject.activeSelf,Is.True,"the hit hold head stays visible");
            Assert.That(Vector2.Distance(Centre(lane,head),Centre(lane,target)),Is.LessThan(.01f),"a hit head pins to its receptor");
            Assert.That(tail.rectTransform.rect.height,Is.LessThan(before));Assert.That(tail.rectTransform.rect.height,Is.GreaterThan(0));
            interaction.Inputs.Add(new RhythmInput{Direction=0,TimeSeconds=2.5,Release=true});
            Feedback(hud,interaction,2.5);hud.DrawChart(2.5,lead,effect,reducedMotion,4);
            Assert.That(Judgment(hud),Is.EqualTo("LET GO"));Assert.That(tail.color.r,Is.EqualTo(tail.color.g));Assert.That(tail.color.g,Is.EqualTo(tail.color.b));
            Assert.That(tail.color.a,Is.EqualTo(.5f));
            hud.DrawChart(3,lead,effect,reducedMotion,4);Assert.That(head.gameObject.activeSelf||tail.gameObject.activeSelf,Is.False,"both retire at the authored end");
            var tap=new RhythmChart{DurationSeconds=4};tap.Notes.Add(new RhythmNote{Id=7,Direction=3,TimeSeconds=2});
            hud.ShowChart(new InteractionState{Id="tap",BeatSeconds=.5},tap);hud.DrawChart(1,2,"",false);
            Assert.That(lane.Find("Hold tails/Tail 0").GetComponent<Image>(),Is.SameAs(tail),"the next chart reuses the tail pool");
            Assert.That(tail.gameObject.activeSelf,Is.False,"a reused tap has no stale hold tail");
        }

        [Test]public void ReplacedInputLogsRebuildTheRealJudgeAndDroppedTail()
        {
            var hud=Hud();var lane=LaneOf(hud);var chart=new RhythmChart{DurationSeconds=4};
            chart.Notes.Add(new RhythmNote{Id=91,Direction=2,TimeSeconds=2,HoldSeconds=1});
            var interaction=new InteractionState{Id="rollback",Kind="Dance",NoteCount=1,BeatSeconds=.5};hud.ShowChart(interaction,chart);
            interaction.Inputs.Add(new RhythmInput{Direction=2,TimeSeconds=2});
            interaction.Inputs.Add(new RhythmInput{Direction=2,TimeSeconds=2.2,Release=true});
            Feedback(hud,interaction,2.2);hud.DrawChart(2.2,2,"",false);Assert.That(Judgment(hud),Is.EqualTo("LET GO"));
            interaction.Inputs[1]=new RhythmInput{Direction=2,TimeSeconds=2.9,Release=true};
            Feedback(hud,interaction,2.9);hud.DrawChart(2.9,2,"",false);
            Assert.That(Judgment(hud),Is.EqualTo("PERFECT"),"a rollback can replace a same-size log with a valid release");
            Assert.That(lane.Find("Tempo and steps").GetComponent<Text>().text,Does.StartWith("1 / 1"));
            var tail=lane.Find("Hold tails/Tail 0").GetComponent<Image>();Assert.That(tail.color.r,Is.Not.EqualTo(tail.color.g),"the old drop cannot grey the rebuilt tail");
            interaction.Inputs.Clear();Feedback(hud,interaction,1);hud.DrawChart(1,2,"",false);
            Assert.That(Judgment(hud),Is.Empty);Assert.That(lane.Find("Tempo and steps").GetComponent<Text>().text,Does.StartWith("0 / 1"));
            interaction.Inputs.Add(new RhythmInput{Direction=2,TimeSeconds=2});Feedback(hud,interaction,2);hud.DrawChart(2.2,2,"",false);
            Assert.That(Judgment(hud),Is.EqualTo("PERFECT"),"the same head is eligible again after real log rollback");
            interaction.ChartSeed++;interaction.Phrase++;interaction.Id="next";hud.ShowChart(interaction,chart);Feedback(hud,interaction,2.2);hud.DrawChart(2.2,2,"",false);
            Assert.That(Judgment(hud),Is.EqualTo("PERFECT"));Assert.That(lane.Find("Tempo and steps").GetComponent<Text>().text,Does.StartWith("1 / 1"));
        }

        private static void Feedback(FestivalHud hud,InteractionState interaction,double now)=>typeof(FestivalHud).GetMethod("ProcessRhythmFeedback",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,new object[]{interaction,now});
        private static string Judgment(FestivalHud hud)=>(string)typeof(FestivalHud).GetField("lastRhythmJudgment",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);

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

        // HUD-4: the arrows take the dose's substance (TRIP-5), else the newest catalog effect taken, so a Tongue Stamp or a Fun Guy
        // still bends them for a friend who also took the debrief's shot or holds a dose from before TRIP-5. Neither the shot nor a
        // dose is a catalog effect: they have no look of their own. The HUD draws and times the lane by this (UpdateRhythm).
        [Test]public void TheArrowsKeepTheNewestLookAfterAShotOrADose()
        {
            var me=new PlayerState{Id="me"};
            void Take(string id)=>me.Effects.Add(new ActiveEffect{Id=id,RemainingSeconds=60});
            Take("shot");
            Assert.That(FestivalTrip.LaneEffect(me),Is.Empty,"the debrief's shot alone leaves the arrows plain");
            Take("lsd");
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("lsd"),"a Tongue Stamp taken after the shot bends them");
            Take("mushrooms");
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("mushrooms"),"the newest one taken leads");
            me.Effects.Insert(0,new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=60});
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("mushrooms"),"a dose from an older snapshot has no substance and no look");
            me.Effects[0].Substance="ketamine";
            Assert.That(FestivalTrip.LaneEffect(me),Is.EqualTo("ketamine"),"a dose's substance still comes first");
        }

        // Every four-lane challenge, and only one, opens the live dancer beside its lanes; the menu covers it. DIALOG-3: a sale, a buy,
        // a chat and a talk with security are talks, on the chat card (HudConversationTests), and are on the lanes only while dancing
        // the dance a wary festivalgoer asks for first (FestivalSimulation.TrustDancing). The HUD reads both panels from these
        // (FestivalHud.UpdateRhythm), and HudTripperPlayTests watches them on a hosted check dance, trust dance and talk.
        [Test]public void EveryFourLaneChallengeShowsTheDancerUnlessTheMenuIsOpen()
        {
            foreach(var kind in FourLaneKinds)
            {
                Assert.That(FestivalInput.IsRhythm(Doing(kind)),Is.True,"setup: a "+kind+" plays on the four lanes");
                Assert.That(FestivalHudText.ShowsDancer(Doing(kind),false),Is.True,"a "+kind+" shows the live dancer beside its lanes");
                Assert.That(FestivalHudText.ShowsDancer(Doing(kind),true),Is.False,"the menu hides the dancer during a "+kind);
            }
            foreach(var kind in TalkKinds)
            {
                Assert.That(FestivalInput.IsRhythmKind(kind),Is.False,"a "+kind+" is a talk, not a four-lane challenge");
                Assert.That(FestivalInput.IsRhythm(Doing(kind,turns:1))||FestivalHudText.ShowsDancer(Doing(kind,turns:1),false),Is.False,"talking, a "+kind+" has no lanes and no dancer");
                Assert.That(FestivalInput.IsRhythm(Doing(kind,trust:true)),Is.True,"its trust dance plays on the lanes");
                Assert.That(FestivalHudText.ShowsDancer(Doing(kind,trust:true),false),Is.True,"beside the live dancer");
                Assert.That(FestivalInput.IsRhythm(Doing(kind,trust:true,turns:1)),Is.False,"until the talk that follows it");
            }
            foreach(var kind in new[]{"","ConfirmChat","FindFriend",FestivalSimulation.LieDownKind})
                Assert.That(FestivalHudText.ShowsDancer(Doing(kind),false),Is.False,"no four-lane challenge ("+kind+"), no dancer");
            Assert.That(FestivalInput.IsRhythm(null)||FestivalHudText.ShowsDancer(null,false),Is.False,"doing nothing, no lanes and no dancer");
        }

        // A check dance reads as a dance. DIALOG-3: a talk is on the lanes only for the dance a wary festivalgoer asks for first, and
        // that says so, whatever the talk: the lane's title, and the "YOU  /  …" heading with two spaces round the slash.
        [Test]public void EachChallengeHasItsOwnHeadingAndCaption()
        {
            var expected=new Dictionary<string,(string heading,string caption)>
            {
                {"Dance",("YOU  /  ON THE FLOOR","YOUR LOOK • YOUR MOVES")},
                {"ConfirmDance",("YOU  /  ON THE FLOOR","YOUR LOOK • YOUR MOVES")},
                {"Poi",("YOU  /  POI CIRCLE","YOUR POI • YOUR FLOW")},
                {"Dj",("YOU  /  AT THE DECK","YOUR LOOK • YOUR MIX")},
                {FestivalSimulation.DeckSetKind,("YOU  /  ON THE ART CAR","YOUR LOOK • YOUR SET")},
                {"Sale",("YOU  /  DANCING FOR TRUST","YOUR LOOK • THEIR TRUST")},
                {"Buy",("YOU  /  DANCING FOR TRUST","YOUR LOOK • THEIR TRUST")},
                {"Conversation",("YOU  /  DANCING FOR TRUST","YOUR LOOK • THEIR TRUST")},
                {"Police",("YOU  /  DANCING FOR TRUST","YOUR LOOK • THEIR TRUST")},
            };
            Assert.That(expected.Keys,Is.EquivalentTo(FourLaneKinds.Concat(TalkKinds)),"every four-lane challenge and every talk is named here");
            foreach(var pair in expected)
            {
                Assert.That(FestivalHudText.DancerHeading(pair.Key),Is.EqualTo(pair.Value.heading),pair.Key+"'s heading");
                Assert.That(FestivalHudText.DancerCaption(pair.Key),Is.EqualTo(pair.Value.caption),pair.Key+"'s caption");
            }
            foreach(var kind in TalkKinds)Assert.That(FestivalHudText.RhythmTitle(kind),Is.EqualTo("TRUST DANCE  /  FOUR-LANE"),"a "+kind+"'s lane title");
            // PLAYA-5: holding G ends a deck set by hopping off the car.
            Assert.That(FestivalHudText.RhythmTitle(FestivalSimulation.DeckSetKind),Is.EqualTo("DECK SET  /  FOUR-LANE"),"the deck set's lane title");
            Assert.That(FestivalHudText.HoldToStopFor(FestivalSimulation.DeckSetKind),Is.EqualTo("HOLD G TO HOP OFF"),"the deck set's stop line");
        }

        // DANCE-12: E sits between the W and D lanes, so a stray E must never end a dance. Every four-lane challenge, and only those,
        // stops by holding G alone: the HUD runs nothing on E then (FestivalHud.UpdateActions, UpdateKeyboard), even beside a
        // festivalgoer to buy from or a friend to hand gear to, and a tap of G drops nothing. Everything else keeps its cancel on
        // E: lying on the grass still gets up first, and a chat check, a task or a ride still stops. DIALOG-3: a talk is replied to on
        // 1-3, far from E, so E walks out of it; its trust dance is on the lanes, so it stops only by holding G. HudTripperPlayTests
        // presses the keys in a hosted dance and buy.
        [Test]public void OnlyAFourLaneChallengeStopsByHoldingG()
        {
            foreach(var kind in FourLaneKinds)
            {
                Assert.That(StopsByHold(Doing(kind)),Is.True,"a "+kind+" stops only by holding G: E does nothing");
                Assert.That(StopsByHold(Doing(kind,status:"Cancelled")),Is.False,"a "+kind+" already over holds nothing back");
            }
            foreach(var kind in TalkKinds)
            {
                Assert.That(StopsByHold(Doing(kind,turns:1)),Is.False,"talking, E walks out of a "+kind);
                Assert.That(StopsByHold(Doing(kind,trust:true)),Is.True,"its trust dance stops only by holding G");
            }
            Assert.That(StopsByHold(Doing(FestivalSimulation.LieDownKind)),Is.False,"lying on the grass, E still gets you up");
            foreach(var kind in new[]{"ConfirmChat","Medical","Rescue","Revival","Release","FindFriend","LostProperty","Extract",FestivalSimulation.RideWheelKind,FestivalSimulation.RideCarKind})
                Assert.That(StopsByHold(Doing(kind)),Is.False,"a "+kind+" keeps its cancel on E");
            Assert.That(FestivalHudText.StopsByHold(new RoundState(),new PlayerState{Id="me"}),Is.False,"doing nothing, E keeps every action");
        }
        private static bool StopsByHold(InteractionState doing)
        {
            var s=new RoundState();s.Interactions.Add(doing);
            return FestivalHudText.StopsByHold(s,new PlayerState{Id="me",InteractionId=doing.Id});
        }
        // My interaction of this kind: a talk is in its trust dance until its first turn (FestivalSimulation.TrustDancing).
        private static InteractionState Doing(string kind,bool trust=false,int turns=0,string status="Active")
        {
            var doing=new InteractionState{Id="doing",PlayerId="me",Kind=kind,Status=status,TrustDance=trust};
            for(int t=0;t<turns;t++)doing.Turns.Add(new ConversationTurn());
            return doing;
        }

        private static readonly string[] FourLaneKinds={"Dance","ConfirmDance","Poi","Dj",FestivalSimulation.DeckSetKind},TalkKinds={"Sale","Buy","Conversation","Police"};

        // DIALOG-3: a sale, a buy from a festivalgoer (ECON-2), a chat and a talk with security are talks again, not dances, so every
        // client, the talker's own view included, sees the player in that talk's pose, never dancing. Only the dance a wary
        // festivalgoer asks for before a talk (FestivalSimulation.TrustDancing) shows them dancing. The buy starts the way the HUD's
        // Buy action starts it: StartBuy for the one kind the festivalgoer sells.
        [Test]public void EveryoneSeesATalkAsATalkAndOnlyItsTrustDanceDanced()
        {
            foreach(var kind in new[]{"Sale","Buy","Conversation","Police"})foreach(bool wary in new[]{false,true})
            {
                if(wary&&kind=="Police")continue;
                // Seed 6: the partner, a burner, is not one who always dances first, so only suspicion makes them wary.
                var game=new FestivalSimulation(6);var dancer=game.AddPlayer("p0","P0");game.AddPlayer("p1","P1");game.State.Phase="Playing";game.State.Npcs.Clear();
                var partner=new NpcState{Id="partner",Kind=kind=="Police"?"Cop":"Wook",Role=kind=="Buy"?"Regular":"",X=0,Z=2,Yaw=180,CanTalk=true};game.State.Npcs.Add(partner);
                if(wary)partner.Observers.Add(new ObserverState{PlayerId="p0",Suspicion=FestivalSimulation.TrustDanceSuspicion});
                dancer.X=0;dancer.Z=0;dancer.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                var started=game.Execute("p0",new GameCommand{Id="dance2_"+kind,Kind=kind=="Sale"?"StartSale":kind=="Buy"?"StartBuy":kind,TargetId="partner",ItemId=kind=="Sale"?"stock_lsd":kind=="Buy"?FestivalSimulation.SellerStock("partner"):""});
                Assert.That(started.Accepted,Is.True,"setup: the "+kind+" starts: "+started.Reason);
                Assert.That(game.Interaction(dancer.InteractionId).TrustDance,Is.EqualTo(wary),"setup: only a wary partner asks for a dance first");
                foreach(var viewer in game.State.Players)
                    Assert.That(FestivalSession.ViewFor(game,viewer.Id).Players.Find(p=>p.Id=="p0").VisualPose,Is.EqualTo(wary?"Dance":kind),viewer.Id+" sees P0 "+(wary?"dance before":"in")+" the "+kind);
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
        private static Rect Bounds(RectTransform lane,RectTransform rect){Vector2 min=lane.InverseTransformPoint(rect.TransformPoint(rect.rect.min)),max=lane.InverseTransformPoint(rect.TransformPoint(rect.rect.max));return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
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
