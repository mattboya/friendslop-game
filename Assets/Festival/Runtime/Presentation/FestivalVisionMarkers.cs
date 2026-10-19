using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>VISION-1: draws the tripper's visions over festivalgoers and places. Only the tripper's view carries visions
    /// (FestivalSimulation.VisibleVisions), so every other client draws nothing. A fake's tells come from the host's Tell flag,
    /// never from its truth: it casts a subtly wrong shadow (too small, or a goblin's, picked from its id: VISION-4), is slightly
    /// off in colour and shimmers while the camera moves. A checked vision drops its tells and is ringed in green (a truth) or
    /// red (a fake). While the aim dot is on it, a checked vision says what it found (NOT A NARC, PAYS DOUBLE...) and an
    /// unchecked one asks what it claims (NARC?) (VISION-5). The shadow is a pool on the ground right under the marker, at the
    /// festivalgoer's feet (FestivalShadowTellView, which MINI-3's Light Show shares).</summary>
    public sealed class FestivalVisionMarkers : MonoBehaviour
    {
        // Over a festivalgoer's head (stacked when two visions share one), or over a spot on the grounds. A label sits LabelGap over
        // its own glyph, and StackStep leaves room for it under the next glyph up: two .64 m glyphs (a clue under a double buyer)
        // plus a .23 m line and the gaps (VISION-5).
        private const float OverHead=2.45f,StackStep=.95f,OverPlace=1.4f,Size=.32f,LabelGap=.04f;
        // Under an unchecked claim, smaller: how to settle it. E dances and F chats with the festivalgoer (FestivalHud's check).
        private const string CheckLine="\n<size=62.5%>F CHAT • E DANCE TO CHECK</size>";
        // Fakes: hue a little off, and a scale shimmer that grows with how fast the camera turns or moves, eased over ShimmerEase
        // seconds so a mouse that reports slower than the screen refreshes cannot make it flicker.
        private const float FakeHueShift=.06f,ShimmerDepth=.18f,ShimmerHz=9,FullShimmerTurn=90,FullShimmerMove=4,ShimmerEase=.15f;
        private static readonly Color CheckedFalse=new Color(.52f,.52f,.55f);
        // A checked vision's ring: the glyph's own shape a little larger, its front faces culled so only a band around the glyph
        // shows. Bright neon, so it never reads as the dark, muted green of a suspicious wook's skin (CROWD-4).
        private const float RingScale=1.3f,Glow=.6f;
        private static readonly Color TrueRing=new Color(.45f,1f,.55f),FalseRing=new Color(1f,.3f,.3f);
        // The aim dot is on a vision when the view's ray passes this close to the upright line from its festivalgoer's feet up to
        // the top of their whole stack of markers (or from its spot on the ground up to the top of its marker).
        private const float AimReach=.5f;

        private sealed class Marker { public Transform Root,Label; public FestivalShadowTellView Shadow; public MeshRenderer Glyph; public GameObject Ring; public TextMeshPro Verdict; public Material Material,RingMaterial; public float Aimed; public Vector3 Ground; public string Ask; public FakeShadow Wrong; }
        private readonly Dictionary<string,Marker> markers=new Dictionary<string,Marker>();
        private readonly List<string> gone=new List<string>();
        private static readonly List<VisionState> None=new List<VisionState>();
        private readonly MotionTracker motion=new MotionTracker();
        private Material shade,glow;
        private FestivalSession session;
        private Func<string,Transform> actorFor;

        /// <summary>How many markers are drawn this frame.</summary>
        public int Shown {get;private set;}

        // After FestivalSession.Update has moved the actors and the camera this frame. CAM-1: over the shoulder the aim starts level
        // with your eye, so the aim dot reads what it is on and never a festivalgoer between the camera and you.
        private void LateUpdate()
        {
            if(session==null){session=GetComponent<FestivalSession>();if(session==null)return;actorFor=id=>{var body=session.WorldCharacter(id);return body!=null?body.transform:null;};}
            Apply(session.State,session.ViewCamera!=null?session.ViewCamera.transform:null,actorFor,Time.time,Time.unscaledDeltaTime,session.EyePosition);
        }

        /// <summary>Draws state's visions while the festival is shown; actorFor finds a festivalgoer's drawn body (null when not drawn).
        /// The aim runs along view's centre line, the aim dot's, from level with eye (the session's EyePosition, CAM-1:
        /// FestivalThirdPerson.AimFrom), or from view itself without one.</summary>
        public void Apply(RoundState state,Transform view,Func<string,Transform> actorFor,float time,float deltaTime,Vector3? eye=null)
        {
            var visions=state!=null&&FestivalWorld.ShowsFestival(state.Phase)?state.Visions:None;
            gone.Clear();
            foreach(var id in markers.Keys){bool kept=false;foreach(var v in visions)if(v.Id==id){kept=true;break;}if(!kept)gone.Add(id);}
            foreach(var id in gone){Dispose(markers[id].Root.gameObject);Dispose(markers[id].Material);Dispose(markers[id].RingMaterial);markers.Remove(id);}
            float shimmer=motion.Step(view,deltaTime);Shown=0;
            for(int i=0;i<visions.Count;i++)
            {
                var vision=visions[i];if(!markers.TryGetValue(vision.Id,out var marker))markers[vision.Id]=marker=Create(vision,FestivalNightLighting.IsNight(state));
                var body=vision.NpcId==""?null:actorFor?.Invoke(vision.NpcId);
                bool drawn=vision.NpcId==""||body!=null&&body.gameObject.activeInHierarchy;
                if(marker.Root.gameObject.activeSelf!=drawn)marker.Root.gameObject.SetActive(drawn);
                marker.Aimed=float.PositiveInfinity;
                if(!drawn)continue;
                Shown++;
                int stacked=0;for(int j=0;j<i;j++)if(vision.NpcId!=""&&visions[j].NpcId==vision.NpcId)stacked++;
                var ground=marker.Ground=body!=null?body.position:new Vector3(vision.X,0,vision.Z);
                marker.Root.position=ground+Vector3.up*(body!=null?OverHead+StackStep*stacked:OverPlace);
                // Once checked the verdict is plain; until then only the host's Tell flag says which tells to show.
                bool tells=vision.Tell&&!vision.Confirmed;
                var look=Look(vision.Kind).Color;
                marker.Material.color=vision.Confirmed&&!vision.IsTrue?CheckedFalse:tells?FakeTint(look):look;
                if(marker.Material.HasProperty("_EmissionColor"))marker.Material.SetColor("_EmissionColor",marker.Material.color*Glow);
                // VISION-4: a fake's shadow is wrong, never missing; checked, it is the normal pool again.
                marker.Shadow.Show(tells,marker.Wrong);marker.Shadow.Place(ground,view);
                marker.Glyph.transform.localScale=Vector3.one*Size*(tells?ShimmerScale(shimmer,time,i):1);
                if(marker.Ring.activeSelf!=vision.Confirmed)marker.Ring.SetActive(vision.Confirmed);
                if(vision.Confirmed)
                {
                    var ring=marker.RingMaterial;ring.color=vision.IsTrue?TrueRing:FalseRing;
                    if(ring.HasProperty("_EmissionColor"))ring.SetColor("_EmissionColor",ring.color*Glow);
                }
                if(view!=null)marker.Label.rotation=view.rotation;
            }
            // Once every marker is placed: a festivalgoer is aimed at up to the top of their whole stack, so every vision over them
            // answers the aim alike and the lower label stays up while the dot moves up to read the upper one. Checked or not: an
            // unchecked vision says what it claims.
            int aimed=-1;float nearest=float.PositiveInfinity;
            var from=view==null?Vector3.zero:FestivalThirdPerson.AimFrom(view.position,view.forward,eye??view.position);
            for(int i=0;i<visions.Count&&view!=null;i++)
            {
                var vision=visions[i];var marker=markers[vision.Id];
                if(!marker.Root.gameObject.activeSelf)continue;
                float top=marker.Glyph.bounds.max.y;
                for(int j=i+1;j<visions.Count;j++)if(vision.NpcId!=""&&visions[j].NpcId==vision.NpcId)top=Mathf.Max(top,markers[visions[j].Id].Glyph.bounds.max.y);
                marker.Aimed=Aim(from,view.forward,marker.Ground,top);
                if(marker.Aimed<nearest){nearest=marker.Aimed;aimed=i;}
            }
            // Only the target nearest the camera along the aim says what it found, or claims; every vision over that festivalgoer does.
            // One check settles them all, so only the top of the stack carries the check line, where no glyph sits above it.
            for(int i=0;i<visions.Count;i++)
            {
                var vision=visions[i];var marker=markers[vision.Id];
                bool said=marker.Aimed<float.PositiveInfinity&&(i==aimed||vision.NpcId!=""&&vision.NpcId==visions[aimed].NpcId),top=true;
                for(int j=i+1;j<visions.Count&&said;j++)if(vision.NpcId!=""&&visions[j].NpcId==vision.NpcId)top=false;
                marker.Verdict.text=!said?"":vision.Confirmed?VerdictText(vision.Kind,vision.IsTrue):top?marker.Ask:ClaimText(vision.Kind);
            }
        }

        /// <summary>What a checked vision of kind says it found: the claim it settles, never a bare TRUE or FALSE (VISION-5).</summary>
        public static string VerdictText(string kind,bool isTrue){var words=Words(kind);return isTrue?words.True:words.False;}
        /// <summary>What an unchecked vision of kind claims, as the question a check settles (VISION-5).</summary>
        public static string ClaimText(string kind)=>Words(kind).Claim;
        // One table, shared with HUD-7's How to Play. A stash and a double buyer are always true in play (SecretSights), so their
        // fake words only show in hand-built states; every kind has both all the same.
        private static (string Claim,string True,string False) Words(string kind)=>kind switch
        {
            "Buyer"=>("BUYER?","BUYER","NOT BUYING"),
            "Narc"=>("NARC?","NARC!","NOT A NARC"),
            "Clue"=>("CLUE?","REAL CLUE","FAKE CLUE"),
            "DoubleBuyer"=>("PAYS DOUBLE?","PAYS DOUBLE","PAYS NORMAL"),
            _=>("STASH HERE?","STASH HERE","NOTHING HERE"),
        };

        // How far along the aim (from eye, along forward) it passes within AimReach of the upright line from ground up to top;
        // infinity when it never does. The closest points of a ray and a segment: from the lines' closest points, clamp the ray to
        // its start and the line to its ends, and from a clamped end take the ray's nearest point to it.
        private static float Aim(Vector3 eye,Vector3 forward,Vector3 ground,float top)
        {
            var flat=new Vector2(forward.x,forward.z);
            float along=flat.sqrMagnitude>1e-6f?Mathf.Max(0,Vector2.Dot(new Vector2(ground.x-eye.x,ground.z-eye.z),flat)/flat.sqrMagnitude):0;
            float height=eye.y+forward.y*along,onLine=Mathf.Clamp(height,ground.y,top);
            if(onLine!=height)along=Mathf.Max(0,Vector3.Dot(new Vector3(ground.x,onLine,ground.z)-eye,forward));
            var near=eye+forward*along;
            return Vector3.Distance(near,new Vector3(ground.x,Mathf.Clamp(near.y,ground.y,top),ground.z))<=AimReach?along:float.PositiveInfinity;
        }

        // The shimmer and hue shift are shared with everything else the dose invents (VISION-2's creatures), so it all looks fake
        // the same way. The creatures cast ordinary shadows (VISION-4).
        /// <summary>How much the camera is turning or moving: 0 while it is still, 1 at a quick turn or a run; eased, so it settles
        /// within a few tenths of a second of the camera. Each component that shimmers keeps its own.</summary>
        public sealed class MotionTracker
        {
            private Vector3 lastPosition;
            private Quaternion lastRotation;
            private bool seen;
            private float eased;
            public float Step(Transform view,float deltaTime)
            {
                if(view==null){seen=false;return eased=0;}
                float motion=seen&&deltaTime>0?Quaternion.Angle(lastRotation,view.rotation)/deltaTime/FullShimmerTurn+Vector3.Distance(lastPosition,view.position)/deltaTime/FullShimmerMove:0;
                seen=true;lastPosition=view.position;lastRotation=view.rotation;
                return eased=Mathf.Lerp(eased,Mathf.Clamp01(motion),1-Mathf.Exp(-deltaTime/ShimmerEase));
            }
        }
        /// <summary>A fake's size factor this frame, from its MotionTracker reading: steady while the camera is still.</summary>
        public static float ShimmerScale(float motion,float time,int index)=>1+ShimmerDepth*motion*Mathf.Sin(time*ShimmerHz*2*Mathf.PI+index);
        /// <summary>VISION-4: the two ways a fake's shadow is wrong: too small, or a goblin's.</summary>
        public enum FakeShadow{Small,Goblin}
        /// <summary>Which wrong shadow a fake with this vision id casts, half each: an FNV hash of the id (as FestivalCreatures.SeedFor
        /// hashes a player), so it is the same on every run and never changes while the vision is shown.</summary>
        public static FakeShadow ShadowTell(string visionId)
        {
            uint hash=2166136261;foreach(char c in visionId??"")hash=unchecked((hash^c)*16777619);
            // FNV's bits barely move with the last digit of a numbered id (vision_1 to vision_9 would all match), so mix them first.
            hash^=hash>>16;hash=unchecked(hash*0x45d9f3bu);hash^=hash>>16;
            return (hash&1)==0?FakeShadow.Small:FakeShadow.Goblin;
        }
        /// <summary>A fake's colour: the hue a little off, brightness and saturation kept.</summary>
        public static Color FakeTint(Color color){Color.RGBToHSV(color,out float h,out float s,out float v);return Color.HSVToRGB(Mathf.Repeat(h+FakeHueShift,1),s,v);}

        // Each kind has its own shape and colour, so colour is never the only cue. The secrets (a cash stash, a buyer who
        // pays double) share one gold look.
        private static (PrimitiveType Shape,Color Color) Look(string kind)=>kind switch
        {
            "Buyer"=>(PrimitiveType.Sphere,new Color(.25f,.95f,.35f)),
            "Narc"=>(PrimitiveType.Cube,new Color(1f,.25f,.2f)),
            "Clue"=>(PrimitiveType.Capsule,new Color(.3f,.85f,1f)),
            _=>(PrimitiveType.Cylinder,new Color(1f,.78f,.2f)),
        };

        // A vision belongs to one level, so its pool is picked once, when it is first drawn.
        private Material Pool(bool night)
        {
            var pool=night?glow:shade;if(pool!=null)return pool;
            pool=FestivalShadowTellView.Material(night);
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
            // VISION-5: the ring is on the floating marker, never on the festivalgoer's body. Off until the vision is checked.
            var outline=GameObject.CreatePrimitive(Look(vision.Kind).Shape);outline.name="Outline";outline.transform.SetParent(glyph.transform,false);outline.transform.localScale=Vector3.one*RingScale;
            var outlineCollider=outline.GetComponent<Collider>();outlineCollider.enabled=false;Dispose(outlineCollider);
            var ring=new Material(Resources.Load<Material>("FestivalLit")){mainTexture=Texture2D.whiteTexture};ring.SetFloat("_Cull",(float)CullMode.Front);
            if(ring.HasProperty("_EmissionColor"))ring.EnableKeyword("_EMISSION");
            var ringRenderer=outline.GetComponent<MeshRenderer>();ringRenderer.sharedMaterial=ring;ringRenderer.shadowCastingMode=ShadowCastingMode.Off;outline.SetActive(false);
            var shadow=FestivalShadowTellView.Build(root,Pool(night));
            // VISION-5: the label grows up from just over its own glyph, so a second line never reaches down into it.
            var label=new GameObject("Label").transform;label.SetParent(root,false);label.localPosition=Vector3.up*(glyph.GetComponent<MeshFilter>().sharedMesh.bounds.extents.y*Size+LabelGap);
            // Its own authored lines only, so rich text stays on for the smaller check line.
            var verdict=FestivalWorldText.Add(label.gameObject,"",64*.03f,FestivalWorldText.Cream,true);verdict.richText=true;label=verdict.transform;
            // A spot on the ground is checked by walking to it (FindStashes), so it says no more than its claim.
            var ask=vision.NpcId==""?ClaimText(vision.Kind):ClaimText(vision.Kind)+CheckLine;
            return new Marker{Root=root,Label=label,Shadow=shadow,Wrong=ShadowTell(vision.Id),Glyph=renderer,Ring=outline,Verdict=verdict,Material=material,RingMaterial=ring,Ask=ask};
        }

        private void OnDestroy(){foreach(var marker in markers.Values){Dispose(marker.Material);Dispose(marker.RingMaterial);}markers.Clear();if(shade!=null)Dispose(shade);if(glow!=null)Dispose(glow);}
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
