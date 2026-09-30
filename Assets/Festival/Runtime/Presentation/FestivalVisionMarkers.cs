using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>VISION-1: draws the tripper's visions over festivalgoers and places. Only the tripper's view carries visions
    /// (FestivalSimulation.VisibleVisions), so every other client draws nothing. A fake's tells come from the host's Tell flag,
    /// never from its truth: it casts no shadow, is slightly off in colour and shimmers while the camera moves. A checked
    /// vision drops its tells and plainly says TRUE or FALSE. The shadow is a pool on the ground right under the marker, at the
    /// festivalgoer's feet (the festival sun is only 14 degrees up, so a real one would land about 10 m away): dark by day, and
    /// by night, when the ground is too dark for any shadow to show, a faint pale glow.</summary>
    public sealed class FestivalVisionMarkers : MonoBehaviour
    {
        // Over a festivalgoer's head (stacked when two visions share one), or over a spot on the grounds.
        private const float OverHead=2.45f,StackStep=.5f,OverPlace=1.4f,Size=.32f,LabelRise=.4f;
        // Fakes: hue a little off, and a scale shimmer that grows with how fast the camera turns or moves.
        private const float FakeHueShift=.06f,ShimmerDepth=.18f,ShimmerHz=9,FullShimmerTurn=90,FullShimmerMove=4;
        // Truths: a pool wider than the festivalgoer, so it shows around their feet; just clear of the footpaths (their tops sit
        // under .03 m). Night ground is about 15 of 255 at one dose, so the night pool glows instead of darkening.
        private const float ShadowWidth=1.4f,ShadowThickness=.01f,ShadowLift=.05f;
        private static readonly Color NightGlow=new Color(.03f,.03f,.04f);
        private static readonly Color CheckedFalse=new Color(.52f,.52f,.55f);
        private const string TrueVerdict="TRUE",FalseVerdict="FALSE";

        private sealed class Marker { public Transform Root,Label,Shadow; public MeshRenderer Glyph; public TextMesh Verdict; public Material Material; }
        private readonly Dictionary<string,Marker> markers=new Dictionary<string,Marker>();
        private readonly List<string> gone=new List<string>();
        private static readonly List<VisionState> None=new List<VisionState>();
        private Vector3 lastViewPosition;
        private Quaternion lastViewRotation;
        private bool viewSeen;
        private Material shade,glow;
        private FestivalSession session;
        private Func<string,Transform> actorFor;

        /// <summary>How many markers are drawn this frame.</summary>
        public int Shown {get;private set;}

        // After FestivalSession.Update has moved the actors and the first-person camera this frame.
        private void LateUpdate()
        {
            if(session==null){session=GetComponent<FestivalSession>();if(session==null)return;actorFor=id=>{var body=session.WorldCharacter(id);return body!=null?body.transform:null;};}
            Apply(session.State,session.ViewCamera!=null?session.ViewCamera.transform:null,actorFor,Time.time,Time.unscaledDeltaTime);
        }

        /// <summary>Draws state's visions while the festival is shown; actorFor finds a festivalgoer's drawn body (null when not drawn).</summary>
        public void Apply(RoundState state,Transform view,Func<string,Transform> actorFor,float time,float deltaTime)
        {
            var visions=state!=null&&FestivalWorld.ShowsFestival(state.Phase)?state.Visions:None;
            gone.Clear();foreach(var id in markers.Keys)if(!visions.Exists(v=>v.Id==id))gone.Add(id);
            foreach(var id in gone){Dispose(markers[id].Root.gameObject);Dispose(markers[id].Material);markers.Remove(id);}
            float shimmer=CameraMotion(view,deltaTime);Shown=0;
            for(int i=0;i<visions.Count;i++)
            {
                var vision=visions[i];if(!markers.TryGetValue(vision.Id,out var marker))markers[vision.Id]=marker=Create(vision,FestivalNightLighting.IsNight(state));
                var body=vision.NpcId==""?null:actorFor?.Invoke(vision.NpcId);
                bool drawn=vision.NpcId==""||body!=null&&body.gameObject.activeInHierarchy;
                if(marker.Root.gameObject.activeSelf!=drawn)marker.Root.gameObject.SetActive(drawn);
                if(!drawn)continue;
                Shown++;
                int stacked=0;for(int j=0;j<i;j++)if(vision.NpcId!=""&&visions[j].NpcId==vision.NpcId)stacked++;
                var ground=body!=null?body.position:new Vector3(vision.X,0,vision.Z);
                marker.Root.position=ground+Vector3.up*(body!=null?OverHead+StackStep*stacked:OverPlace);
                // Once checked the verdict is plain; until then only the host's Tell flag says which tells to show.
                bool tells=vision.Tell&&!vision.Confirmed;
                var look=Look(vision.Kind).Color;
                marker.Material.color=vision.Confirmed&&!vision.IsTrue?CheckedFalse:tells?Shifted(look):look;
                if(marker.Material.HasProperty("_EmissionColor"))marker.Material.SetColor("_EmissionColor",marker.Material.color*.6f);
                if(marker.Shadow.gameObject.activeSelf==tells)marker.Shadow.gameObject.SetActive(!tells);
                marker.Shadow.position=ground+Vector3.up*ShadowLift;
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

        // A vision belongs to one level, so its pool is picked once, when it is first drawn.
        private Material Pool(bool night)
        {
            var pool=night?glow:shade;if(pool!=null)return pool;
            pool=new Material(Resources.Load<Material>("FestivalLit")){color=Color.black};
            if(pool.HasProperty("_Smoothness"))pool.SetFloat("_Smoothness",0);
            if(night&&pool.HasProperty("_EmissionColor")){pool.EnableKeyword("_EMISSION");pool.SetColor("_EmissionColor",NightGlow);}
            return night?glow=pool:shade=pool;
        }

        private Marker Create(VisionState vision,bool night)
        {
            var root=new GameObject("Vision "+vision.Id).transform;root.SetParent(transform,false);
            var glyph=GameObject.CreatePrimitive(Look(vision.Kind).Shape);glyph.name="Glyph";glyph.transform.SetParent(root,false);
            var collider=glyph.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            // The included URP material: Shader.Find can return null in a player when a dynamically requested shader is stripped.
            var material=new Material(Resources.Load<Material>("FestivalLit")){mainTexture=Texture2D.whiteTexture};
            if(material.HasProperty("_EmissionColor"))material.EnableKeyword("_EMISSION");
            // The glyph's own shadow would land far off under the low sun; its shadow is the pool below.
            var renderer=glyph.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var shadow=GameObject.CreatePrimitive(PrimitiveType.Cylinder);shadow.name="Shadow";shadow.transform.SetParent(root,false);
            shadow.transform.localScale=new Vector3(ShadowWidth,ShadowThickness/2,ShadowWidth);
            var shadowCollider=shadow.GetComponent<Collider>();shadowCollider.enabled=false;Dispose(shadowCollider);
            var shadowRenderer=shadow.GetComponent<MeshRenderer>();shadowRenderer.sharedMaterial=Pool(night);shadowRenderer.shadowCastingMode=ShadowCastingMode.Off;shadowRenderer.receiveShadows=false;
            var label=new GameObject("Label").transform;label.SetParent(root,false);label.localPosition=Vector3.up*LabelRise;
            var verdict=label.gameObject.AddComponent<TextMesh>();verdict.fontSize=64;verdict.characterSize=.03f;verdict.anchor=TextAnchor.MiddleCenter;verdict.alignment=TextAlignment.Center;verdict.color=new Color(.96f,.94f,.84f);
            var font=Resources.Load<Font>("FestivalDisplay");if(font!=null){verdict.font=font;label.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
            return new Marker{Root=root,Label=label,Shadow=shadow.transform,Glyph=renderer,Verdict=verdict,Material=material};
        }

        private void OnDestroy(){foreach(var marker in markers.Values)Dispose(marker.Material);markers.Clear();if(shade!=null)Dispose(shade);if(glow!=null)Dispose(glow);}
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
