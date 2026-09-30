using System;
using System.Collections.Generic;
using Festival.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>VISION-1: draws the tripper's visions over festivalgoers and places. Only the tripper's view carries visions
    /// (FestivalSimulation.VisibleVisions), so every other client draws nothing. A fake's tells come from the host's Tell flag,
    /// never from its truth: it casts no shadow, is slightly off in colour and shimmers while the camera moves. A checked
    /// vision drops its tells and plainly says TRUE or FALSE.</summary>
    public sealed class FestivalVisionMarkers : MonoBehaviour
    {
        // Over a festivalgoer's head (stacked when two visions share one), or over a spot on the grounds.
        private const float OverHead=2.45f,StackStep=.5f,OverPlace=1.4f,Size=.32f,LabelRise=.4f;
        // Fakes: hue a little off, and a scale shimmer that grows with how fast the camera turns or moves.
        private const float FakeHueShift=.06f,ShimmerDepth=.18f,ShimmerHz=9,FullShimmerTurn=90,FullShimmerMove=4;
        private static readonly Color CheckedFalse=new Color(.52f,.52f,.55f);
        private const string TrueVerdict="TRUE",FalseVerdict="FALSE";

        private sealed class Marker { public Transform Root,Label; public MeshRenderer Glyph; public TextMesh Verdict; public Material Material; }
        private readonly Dictionary<string,Marker> markers=new Dictionary<string,Marker>();
        private readonly List<string> gone=new List<string>();
        private static readonly List<VisionState> None=new List<VisionState>();
        private Vector3 lastViewPosition;
        private Quaternion lastViewRotation;
        private bool viewSeen;

        /// <summary>How many markers are drawn this frame.</summary>
        public int Shown {get;private set;}

        /// <summary>Draws state's visions while the festival is shown; actorFor finds a festivalgoer's drawn body (null when not drawn).</summary>
        public void Apply(RoundState state,Transform view,Func<string,Transform> actorFor,float time,float deltaTime)
        {
            var visions=state!=null&&FestivalWorld.ShowsFestival(state.Phase)?state.Visions:None;
            gone.Clear();foreach(var id in markers.Keys)if(!visions.Exists(v=>v.Id==id))gone.Add(id);
            foreach(var id in gone){Dispose(markers[id].Root.gameObject);Dispose(markers[id].Material);markers.Remove(id);}
            float shimmer=CameraMotion(view,deltaTime);Shown=0;
            for(int i=0;i<visions.Count;i++)
            {
                var vision=visions[i];if(!markers.TryGetValue(vision.Id,out var marker))markers[vision.Id]=marker=Create(vision);
                var body=vision.NpcId==""?null:actorFor?.Invoke(vision.NpcId);
                bool drawn=vision.NpcId==""||body!=null&&body.gameObject.activeInHierarchy;
                if(marker.Root.gameObject.activeSelf!=drawn)marker.Root.gameObject.SetActive(drawn);
                if(!drawn)continue;
                Shown++;
                int stacked=0;for(int j=0;j<i;j++)if(vision.NpcId!=""&&visions[j].NpcId==vision.NpcId)stacked++;
                marker.Root.position=body!=null?body.position+Vector3.up*(OverHead+StackStep*stacked):new Vector3(vision.X,OverPlace,vision.Z);
                // Once checked the verdict is plain; until then only the host's Tell flag says which tells to show.
                bool tells=vision.Tell&&!vision.Confirmed;
                var look=Look(vision.Kind).Color;
                marker.Material.color=vision.Confirmed&&!vision.IsTrue?CheckedFalse:tells?Shifted(look):look;
                if(marker.Material.HasProperty("_EmissionColor"))marker.Material.SetColor("_EmissionColor",marker.Material.color*.6f);
                marker.Glyph.shadowCastingMode=tells?ShadowCastingMode.Off:ShadowCastingMode.On;
                marker.Glyph.transform.localScale=Vector3.one*Size*(tells?1+ShimmerDepth*shimmer*Mathf.Sin(time*ShimmerHz*2*Mathf.PI+i):1);
                marker.Verdict.text=!vision.Confirmed?"":vision.IsTrue?TrueVerdict:FalseVerdict;
                if(view!=null)marker.Label.rotation=view.rotation;
            }
        }

        // 0 while the camera is still, 1 at a quick turn or a run; one frame's motion, so it settles as soon as the camera does.
        private float CameraMotion(Transform view,float deltaTime)
        {
            if(view==null){viewSeen=false;return 0;}
            float motion=viewSeen&&deltaTime>0?Quaternion.Angle(lastViewRotation,view.rotation)/deltaTime/FullShimmerTurn+Vector3.Distance(lastViewPosition,view.position)/deltaTime/FullShimmerMove:0;
            viewSeen=true;lastViewPosition=view.position;lastViewRotation=view.rotation;
            return Mathf.Clamp01(motion);
        }

        // Each kind has its own shape and colour, so colour is never the only cue. The secrets (a cash stash, a buyer who
        // pays double, a shortcut) share one gold look.
        private static (PrimitiveType Shape,Color Color) Look(string kind)=>kind switch
        {
            "Buyer"=>(PrimitiveType.Sphere,new Color(.25f,.95f,.35f)),
            "Narc"=>(PrimitiveType.Cube,new Color(1f,.25f,.2f)),
            "Clue"=>(PrimitiveType.Capsule,new Color(.3f,.85f,1f)),
            _=>(PrimitiveType.Cylinder,new Color(1f,.78f,.2f)),
        };
        private static Color Shifted(Color color){Color.RGBToHSV(color,out float h,out float s,out float v);return Color.HSVToRGB(Mathf.Repeat(h+FakeHueShift,1),s,v);}

        private Marker Create(VisionState vision)
        {
            var root=new GameObject("Vision "+vision.Id).transform;root.SetParent(transform,false);
            var glyph=GameObject.CreatePrimitive(Look(vision.Kind).Shape);glyph.name="Glyph";glyph.transform.SetParent(root,false);
            var collider=glyph.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            // The included URP material: Shader.Find can return null in a player when a dynamically requested shader is stripped.
            var material=new Material(Resources.Load<Material>("FestivalLit")){mainTexture=Texture2D.whiteTexture};
            if(material.HasProperty("_EmissionColor"))material.EnableKeyword("_EMISSION");
            var renderer=glyph.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            var label=new GameObject("Label").transform;label.SetParent(root,false);label.localPosition=Vector3.up*LabelRise;
            var verdict=label.gameObject.AddComponent<TextMesh>();verdict.fontSize=64;verdict.characterSize=.03f;verdict.anchor=TextAnchor.MiddleCenter;verdict.alignment=TextAlignment.Center;verdict.color=new Color(.96f,.94f,.84f);
            var font=Resources.Load<Font>("FestivalDisplay");if(font!=null){verdict.font=font;label.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
            return new Marker{Root=root,Label=label,Glyph=renderer,Verdict=verdict,Material=material};
        }

        private void OnDestroy(){foreach(var marker in markers.Values)Dispose(marker.Material);markers.Clear();}
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
