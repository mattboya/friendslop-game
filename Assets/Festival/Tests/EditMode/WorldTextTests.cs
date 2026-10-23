using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Festival.Tests
{
    // World labels are crisp TextMeshPro in the festival font, depth-tested so walls hide them, as the speech bubbles are.
    public sealed class WorldTextTests
    {
        private GameObject root;
        [TearDown]public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);}

        // The built world (its signs, camp legends, the VIP stall and both shops' tags) and a minigame booth hold no legacy TextMesh,
        // and no runtime code makes or looks one up (the session's name tags, vision verdicts and the REC tag are built at play).
        [Test]public void NoLegacyTextMeshUnderTheBuiltWorld()
        {
            root=new GameObject("World text world");var world=root.AddComponent<FestivalWorld>();world.Build();
            var sim=new FestivalSimulation(7);var viewer=sim.AddPlayer("p0","P0");world.UpdateShop(sim.State,viewer);
            FestivalMinigameView.Build(root.transform);
            var legacy=root.GetComponentsInChildren<TextMesh>(true).Select(t=>t.transform.parent.name+"/"+t.name).ToArray();
            Assert.That(legacy,Is.Empty,"legacy TextMesh labels in the built world");
            var labels=root.GetComponentsInChildren<TextMeshPro>(true);
            Assert.That(labels,Is.Not.Empty,"the world's labels are TextMeshPro");
            foreach(var label in labels)Assert.That(label.fontSharedMaterial,Is.SameAs(FestivalWorldText.Font.material),label.name+" uses the shared depth-tested festival font");
            var code=Directory.GetFiles(Path.Combine(Application.dataPath,"Festival/Runtime"),"*.cs",SearchOption.AllDirectories)
                .Where(f=>Regex.IsMatch(File.ReadAllText(f),@"<TextMesh>")).Select(Path.GetFileName).ToArray();
            Assert.That(code,Is.Empty,"runtime code that still makes or finds a legacy TextMesh");
        }

        // Depth-tested, a label sunk into its own board or prop would vanish: each one the built world shows, at camp and at the
        // festival, still draws in front of what holds it, seen square on from its readable side.
        [Test]public void EveryWorldLabelShowsFromItsFront()
        {
            root=new GameObject("Label world");var world=root.AddComponent<FestivalWorld>();world.Build();
            var sim=new FestivalSimulation(7);world.UpdateShop(sim.State,sim.AddPlayer("p0","P0"));
            FestivalMinigameView.Build(root.transform).gameObject.SetActive(true);
            var eye=new GameObject("Label eye");eye.transform.SetParent(root.transform,false);var camera=eye.AddComponent<Camera>();
            // The holder turns the background's flat grey, so only its depth, never its colour, changes what its label lights.
            var grey=new Color(.5f,.5f,.5f);var unlit=Shader.Find("Universal Render Pipeline/Unlit");Assert.That(unlit,Is.Not.Null,"setup: the unlit shader");
            var flat=new Material(unlit){color=grey};
            camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=grey;camera.cullingMask=1<<29;
            camera.nearClipPlane=.01f;camera.farClipPlane=3;
            var target=new RenderTexture(96,96,24);var pixels=new Texture2D(96,96,TextureFormat.RGB24,false);camera.targetTexture=target;
            var previous=RenderTexture.active;var failures=new List<string>();var seen=new HashSet<TextMeshPro>();
            Color[] Shot(){camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,96,96),0,0);pixels.Apply();RenderTexture.active=previous;return pixels.GetPixels();}
            int Lit(TextMeshPro label)
            {
                var on=Shot();label.renderer.enabled=false;var off=Shot();label.renderer.enabled=true;
                int lit=0;for(int i=0;i<on.Length;i++)if(Mathf.Abs(on[i].r-off[i].r)+Mathf.Abs(on[i].g-off[i].g)+Mathf.Abs(on[i].b-off[i].b)>.15f)lit++;
                return lit;
            }
            try
            {
                foreach(var phase in new[]{"Shopping","Playing"})
                {
                    world.SetPhase(phase);
                    foreach(var label in root.GetComponentsInChildren<TextMeshPro>(false))
                    {
                        if(label.text==""||!seen.Add(label))continue;
                        var holder=label.transform.parent;var layers=new Dictionary<Transform,int>();
                        foreach(var part in holder.GetComponentsInChildren<Transform>(true)){layers[part]=part.gameObject.layer;part.gameObject.layer=29;}
                        var parts=new List<(Renderer Part,Material[] Look,bool Shown)>();
                        foreach(var part in holder.GetComponentsInChildren<Renderer>(true))
                        {
                            if(part==label.renderer)continue;
                            parts.Add((part,part.sharedMaterials,part.enabled));
                            var flats=new Material[part.sharedMaterials.Length];for(int i=0;i<flats.Length;i++)flats[i]=flat;part.sharedMaterials=flats;
                        }
                        label.ForceMeshUpdate();var bounds=label.renderer.bounds;
                        camera.orthographicSize=Mathf.Max(.05f,bounds.extents.magnitude*1.2f);
                        camera.transform.SetPositionAndRotation(bounds.center-label.transform.forward*1.5f,Quaternion.LookRotation(label.transform.forward,label.transform.up));
                        int held=Lit(label);
                        foreach(var part in parts)part.Part.enabled=false;
                        int alone=Lit(label);
                        foreach(var part in parts){part.Part.sharedMaterials=part.Look;part.Part.enabled=part.Shown;}
                        if(alone<8||held<alone*.9f)failures.Add(phase+" "+holder.name+"/"+label.name+" \""+label.text.Replace("\n"," | ")+"\" lights "+held+" of the "+alone+" pixels it lights alone");
                        foreach(var pair in layers)pair.Key.gameObject.layer=pair.Value;
                    }
                }
                Assert.That(seen.Count,Is.GreaterThan(20),"setup: the camp's and the festival's labels were looked at");
                Assert.That(failures,Is.Empty,"labels hidden by what holds them");
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(flat);}
        }

        // Every letter of a sign legend, front and back, lies on its dark board. The letters are the legend's vertex quads:
        // textBounds is the line box (ascender to descender), taller than capital letters.
        [Test]public void EverySignsBoardIsBehindItsWholeLegend()
        {
            root=new GameObject("Sign world");var world=root.AddComponent<FestivalWorld>();world.Build();
            var outside=new List<string>();int legends=0;
            foreach(var sign in root.GetComponentsInChildren<Transform>(true))
            {
                var board=sign.Find("Label bubble");if(board==null||!sign.name.EndsWith(" sign"))continue;
                foreach(var label in sign.GetComponentsInChildren<TextMeshPro>(true))
                {
                    // Inactive (the festival's signs while the camp shows) unless told to draw anyway.
                    label.ForceMeshUpdate(true);legends++;
                    float halfWidth=0,halfHeight=0;int quads=0;
                    foreach(var mesh in label.textInfo.meshInfo)
                        for(int v=0;v<mesh.vertexCount;v++)
                        {
                            var corner=sign.InverseTransformPoint(label.transform.TransformPoint(mesh.vertices[v]));quads++;
                            halfWidth=Mathf.Max(halfWidth,Mathf.Abs(corner.x));halfHeight=Mathf.Max(halfHeight,Mathf.Abs(corner.y));
                        }
                    Assert.That(halfWidth,Is.GreaterThan(0),"setup: "+sign.name+"'s legend is drawn ("+quads/4+" quads)");
                    var size=board.localScale;
                    if(halfWidth>size.x/2||halfHeight>size.y/2)
                        outside.Add(sign.name+": the legend spans "+(halfWidth*2).ToString("0.000")+" x "+(halfHeight*2).ToString("0.000")+" m, its board "+size.x.ToString("0.000")+" x "+size.y.ToString("0.000")+" m");
                }
            }
            Assert.That(legends,Is.GreaterThan(10),"setup: the camp's and the festival's signs were looked at");
            Assert.That(outside,Is.Empty,"Sign legends that run off their board:\n"+string.Join("\n",outside));
        }

        // A sign seen from in front reads; an opaque wall between it and the eye hides it, and from behind it shows nothing mirrored
        // (its board hides it there in play; here the board is left out of the picture).
        [Test]public void ASignBehindAWallIsHidden()
        {
            root=new GameObject("Depth world");var booth=FestivalMinigameView.Build(root.transform);booth.gameObject.SetActive(true);
            var sign=booth.Find("Sign text");Assert.That(sign,Is.Not.Null,"setup: the booth's sign");
            foreach(var tr in sign.GetComponentsInChildren<Transform>(true))tr.gameObject.layer=29;
            var eye=new GameObject("Depth eye");eye.transform.SetParent(root.transform,false);var camera=eye.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=.8f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<29;
            var target=new RenderTexture(160,160,24);var pixels=new Texture2D(160,160,TextureFormat.RGB24,false);camera.targetTexture=target;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Depth wall";wall.transform.SetParent(root.transform,false);wall.layer=29;
            wall.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor("Dark");wall.SetActive(false);
            var previous=RenderTexture.active;
            try
            {
                // The sign's readable side faces its own -z.
                camera.transform.SetPositionAndRotation(sign.position-sign.forward*5,Quaternion.LookRotation(sign.forward));
                int Bright()
                {
                    camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,160,160),0,0);pixels.Apply();RenderTexture.active=previous;
                    return pixels.GetPixels().Count(c=>c.r>.7f&&c.g>.7f&&c.b>.6f);
                }
                Assert.That(Bright(),Is.GreaterThan(20),"seen from in front, the sign's letters render");
                wall.transform.SetPositionAndRotation(sign.position-sign.forward,sign.rotation);wall.transform.localScale=new Vector3(6,6,.1f);wall.SetActive(true);
                Assert.That(Bright(),Is.Zero,"an opaque wall between the sign and the eye hides its letters");
                wall.SetActive(false);
                camera.transform.SetPositionAndRotation(sign.position+sign.forward*5,Quaternion.LookRotation(-sign.forward));
                Assert.That(Bright(),Is.Zero,"from behind, nothing shows through mirrored");
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}
