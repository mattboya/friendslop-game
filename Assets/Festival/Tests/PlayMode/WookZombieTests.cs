using System.Collections;
using System.Collections.Generic;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    /// <summary>CROWD-4: the more suspicious a festivalgoer is of you (its Threat), the more it looks like a zombie. Its skin alone
    /// turns a dark, muted green and its face scowls, from none at Watching (.25) to full at Accusing (.70).</summary>
    public sealed class WookZombieTests:FestivalPlayModeTest
    {
        // FestivalSession.Actor's tints for a festivalgoer and for security.
        static readonly Color Festivalgoer=new Color(.8f,.5f,.3f),Security=new Color(.25f,.4f,.7f);

        [UnityTest]public IEnumerator SuspicionTurnsOnlyTheSkinZombieGreen()
        {
            var root=new GameObject("Wook zombie skin test");
            try
            {
                // The darkest and the lightest skin tones (FestivalAppearance's Skin[0] and Skin[7]).
                var wooks=new[]{FestivalCharacter.Create(root.transform,WithSkin(0),Festivalgoer),FestivalCharacter.Create(root.transform,WithSkin(7),Festivalgoer)};
                var untouched=new[]{FestivalCharacter.Create(root.transform,WithSkin(0),Festivalgoer),FestivalCharacter.Create(root.transform,WithSkin(7),Festivalgoer)};
                var guard=FestivalCharacter.Create(root.transform,"cop_1",Security,"Security");
                yield return null;
                var guardLooks=Looks(guard);
                float[] threats={0,.20f,.25f,.45f,.70f,.85f,1,0};
                for(int w=0;w<wooks.Length;w++)
                {
                    var wook=wooks[w];
                    var before=Looks(wook);var skin=SkinOf(wook);var tone=Slots(SkinOf(untouched[w]))[0];
                    var shown=new Color[threats.Length];
                    for(int i=0;i<threats.Length;i++)
                    {
                        wook.Threat=threats[i];yield return null;
                        var now=Looks(wook);shown[i]=now[skin][0];
                        foreach(var surface in before)for(int slot=0;slot<8;slot++)
                        {
                            if(surface.Key==skin&&slot==0)continue;
                            Assert.That(Gap(now[surface.Key][slot],surface.Value[slot]),Is.LessThan(.01f),
                                wook.name+" at Threat "+threats[i]+": "+surface.Key.name+" slot "+slot+" keeps its colour (only the skin turns)");
                        }
                    }
                    // None below Watching, and back to the wook's own tone once it calms down.
                    foreach(int i in new[]{0,1,2,7})Assert.That(Gap(shown[i],tone),Is.LessThan(.01f),wook.name+" at Threat "+threats[i]+" has its own skin");
                    // Steadily toward the fully turned green from .25 to .70, then it stays put.
                    var turned=shown[6];
                    Assert.That(Gap(shown[2],turned),Is.GreaterThan(Gap(shown[3],turned)+.05f),wook.name+": Questioning is greener than Watching");
                    Assert.That(Gap(shown[3],turned),Is.GreaterThan(.05f),wook.name+": Questioning is only part turned");
                    Assert.That(Gap(shown[4],turned),Is.LessThan(.01f),wook.name+": Accusing is fully turned");
                    Assert.That(Gap(shown[5],turned),Is.LessThan(.01f),wook.name+": it stays put above Accusing");
                    var green=shown[4];
                    Assert.That(green.g-green.r,Is.GreaterThan(.08f),wook.name+" reads green at .70, not red: "+green);
                    Assert.That(green.g-green.b,Is.GreaterThan(.08f),wook.name+" reads green at .70, not blue: "+green);
                }
                // Security (and every player) always gets Threat 0, so it never changes.
                var guardNow=Looks(guard);
                foreach(var surface in guardLooks)for(int slot=0;slot<8;slot++)
                    Assert.That(Gap(guardNow[surface.Key][slot],surface.Value[slot]),Is.LessThan(.01f),"security's "+surface.Key.name+" slot "+slot+" never changes");
            }
            finally{Object.Destroy(root);}
        }

        [UnityTest]public IEnumerator TheSkinRedrawsInAFewStepsNotEveryFrame()
        {
            var root=new GameObject("Wook zombie redraw test");
            try
            {
                var wook=FestivalCharacter.Create(root.transform,"wook_redraw",Festivalgoer);
                yield return null;
                var palette=SkinOf(wook).mainTexture;
                // A wook's suspicion climbs a point at a time, as the session's integer Suspicion/100.
                int redraws=0;var colours=new HashSet<Color32>();
                for(int point=25;point<=70;point++)
                {
                    uint before=palette.updateCount;
                    wook.Threat=point/100f;yield return null;
                    if(palette.updateCount!=before)redraws++;
                    colours.Add(Slots(SkinOf(wook))[0]);
                }
                Assert.That(redraws,Is.InRange(3,17),"the skin is redrawn on a few of the 46 frames from Watching to Accusing, in quantized steps");
                Assert.That(colours.Count,Is.InRange(3,17),"the skin shows a few steps of green, not a new colour each point");
                uint held=palette.updateCount;
                for(int frame=0;frame<10;frame++)yield return null;
                Assert.That(palette.updateCount,Is.EqualTo(held),"a steady suspicion never redraws the skin");
            }
            finally{Object.Destroy(root);}
        }

        [UnityTest]public IEnumerator SuspicionScowlsWithoutWorrying()
        {
            var root=new GameObject("Wook zombie face test");
            try
            {
                string[] poses={"Idle","Watching","Questioning","Accusing","Swarming","Downed"};
                float[] threats={0,.25f,.45f,.70f,1,.70f};
                var actors=new FestivalCharacter[poses.Length];
                for(int i=0;i<actors.Length;i++)
                {
                    actors[i]=FestivalCharacter.Create(root.transform,"wook_face_"+i,Festivalgoer);
                    actors[i].AlwaysHighDetail=true;actors[i].Pose=poses[i];actors[i].Threat=threats[i];
                }
                var maxConcern=new float[actors.Length];var minBlink=new float[actors.Length];var maxBlink=new float[actors.Length];
                for(int i=0;i<actors.Length;i++){maxConcern[i]=float.MinValue;minBlink[i]=float.MaxValue;maxBlink[i]=float.MinValue;}
                // The expression eases in over a second; then a blink cycle (at most about 5.1 s) passes for each face.
                for(float start=Time.time;Time.time-start<7f;)
                {
                    yield return null;
                    bool settled=Time.time-start>1.5f;
                    for(int i=0;i<actors.Length;i++)
                    {
                        var face=Face(actors[i],out int smile,out int concern,out int blink);
                        maxConcern[i]=Mathf.Max(maxConcern[i],face.GetBlendShapeWeight(concern));
                        if(!settled)continue;
                        minBlink[i]=Mathf.Min(minBlink[i],face.GetBlendShapeWeight(blink));
                        maxBlink[i]=Mathf.Max(maxBlink[i],face.GetBlendShapeWeight(blink));
                    }
                }
                var brow=new float[actors.Length];var mouth=new float[actors.Length];
                for(int i=0;i<actors.Length;i++)
                {
                    var face=Face(actors[i],out int smile,out int concern,out _);
                    brow[i]=face.GetBlendShapeWeight(concern);mouth[i]=face.GetBlendShapeWeight(smile);
                }
                // A suspicious wook never looks worried: the alive ones' Concern never rises above zero.
                for(int i=0;i<5;i++)Assert.That(maxConcern[i],Is.LessThanOrEqualTo(.01f),poses[i]+" at Threat "+threats[i]+" never looks worried");
                // Calm and Watching: no scowl yet.
                for(int i=0;i<2;i++)
                {
                    Assert.That(Mathf.Abs(brow[i])+Mathf.Abs(mouth[i]),Is.LessThan(.5f),poses[i]+" doesn't scowl");
                    Assert.That(minBlink[i],Is.LessThan(.5f),poses[i]+"'s lids are open between blinks");
                }
                // Accusing: brows pulled down (negative Concern), mouth corners down (negative Smile) and the lids hanging part-closed.
                Assert.That(brow[3],Is.LessThan(-20f),"Accusing pulls the brows down");
                Assert.That(mouth[3],Is.LessThan(-20f),"Accusing drops the mouth corners");
                Assert.That(minBlink[3],Is.InRange(20f,70f),"Accusing's lids hang part-closed");
                // Full at .70: Swarming at 1.0 scowls no harder.
                Assert.That(brow[4],Is.EqualTo(brow[3]).Within(.5f),"the brows are full at .70");
                Assert.That(mouth[4],Is.EqualTo(mouth[3]).Within(.5f),"the mouth is full at .70");
                Assert.That(minBlink[4],Is.EqualTo(minBlink[3]).Within(.5f),"the lids are full at .70");
                // Questioning (.45) is on the way.
                Assert.That(brow[2],Is.InRange(brow[3]+5,-5f),"Questioning's brows are part way down");
                Assert.That(mouth[2],Is.InRange(mouth[3]+5,-5f),"Questioning's mouth is part way down");
                Assert.That(minBlink[2],Is.InRange(5f,minBlink[3]-5),"Questioning's lids are part way down");
                // Every face still blinks.
                for(int i=0;i<5;i++)Assert.That(maxBlink[i],Is.GreaterThan(95f),poses[i]+" still blinks");
                // Downed still drives the worried look.
                Assert.That(brow[5],Is.GreaterThan(60f),"a downed actor looks worried");
            }
            finally{Object.Destroy(root);}
        }

        static string WithSkin(int tone){for(int i=0;;i++)if(FestivalAppearance.For("wook_"+i).SkinColor==tone)return "wook_"+i;}

        static Material SkinOf(FestivalCharacter actor)
        {
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())if(renderer.enabled&&renderer.name.StartsWith("Body_"))return renderer.sharedMaterial;
            Assert.Fail(actor.name+" has no body");return null;
        }

        static SkinnedMeshRenderer Face(FestivalCharacter actor,out int smile,out int concern,out int blink)
        {
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.enabled||!renderer.name.StartsWith("Face_"))continue;
                smile=concern=-1;blink=renderer.sharedMesh.GetBlendShapeIndex("Blink");
                for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++)
                {
                    var key=renderer.sharedMesh.GetBlendShapeName(i);
                    if(key.EndsWith("Smile"))smile=i;if(key.EndsWith("Concern"))concern=i;
                }
                Assert.That(smile>=0&&concern>=0&&blink>=0,Is.True,actor.name+"'s face has Smile, Concern and Blink");
                return renderer;
            }
            Assert.Fail(actor.name+" has no face");smile=concern=blink=-1;return null;
        }

        // What each of the actor's surfaces draws: its palette's eight slots (FestivalAppearance.CreatePalette), times its tint.
        // The eye glow is left out: it still lights up with suspicion.
        static Dictionary<Material,Color[]> Looks(FestivalCharacter actor)
        {
            var looks=new Dictionary<Material,Color[]>();
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.name=="EyeGlow")continue;
                foreach(var material in renderer.sharedMaterials)
                    if(material!=null&&material.mainTexture!=null&&!looks.ContainsKey(material))looks[material]=Slots(material);
            }
            return looks;
        }

        // The palettes are not readable on the CPU, so each is read back through the GPU at its own size.
        static Color[] Slots(Material material)
        {
            var texture=material.mainTexture;
            var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var read=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            var was=RenderTexture.active;
            try
            {
                Graphics.Blit(texture,target);RenderTexture.active=target;
                read.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);read.Apply();
                var slots=new Color[8];
                for(int slot=0;slot<8;slot++)slots[slot]=read.GetPixel(texture.width*(2*slot+1)/16,texture.height/2)*material.color;
                return slots;
            }
            finally{RenderTexture.active=was;RenderTexture.ReleaseTemporary(target);Object.Destroy(read);}
        }

        static float Gap(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)));
    }
}
