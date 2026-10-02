using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>MINI-1 (docs/MINIGAMES_2026-09-30.md): the level's minigame booth where the view has one (FestivalSimulation.BoothAt),
    /// a stranger under a MINIGAME sign in the middle of a glowing ring the size of the rules' (MinigameRingRadius). Nothing in it
    /// collides, so only the rules decide who stands in the ring. While its game runs the festival holds as a freeze-frame: time
    /// stops for everything drawn on frame time (people, the crowd, the stage's lights), the Ferris wheel holds on the world's clock
    /// (FestivalTwistVisuals) and FestivalNightLighting drains a little colour. So whatever a game draws while it runs (MINI-2..4)
    /// runs on unscaled time, as the HUD does. ponytail: a people rig and primitives, as no booth model exists and the art is frozen.</summary>
    public sealed class FestivalMinigameView : MonoBehaviour
    {
        public const string BoothName="Minigame booth",RingName="Minigame ring",SignText="MINIGAME\nALL IN THE RING";
        // The ring's Dashes, each Dash m of its 25 m round. The sign's board stands SignBehind m behind the stranger, its middle
        // SignHeight up, over their head, its letters readable from the front within SignRange.
        private const int Dashes=24;
        private const float Dash=.6f,SignBehind=.7f,SignHeight=2.45f,LetterSize=.065f,SignRange=18;
        private FestivalSession session;
        private Transform booth;
        private Renderer lettering;
        private bool frozen;

        // After FestivalSession.Update has taken this frame's view of the round.
        private void LateUpdate()
        {
            if(session==null){session=GetComponent<FestivalSession>();if(session==null)return;}
            if(booth==null){booth=Build(null);lettering=booth.GetComponentInChildren<TextMesh>(true).GetComponent<Renderer>();}
            var state=session.State;Show(booth,state);
            var view=FestivalCharacter.ViewTransform;if(view!=null&&booth.gameObject.activeSelf)lettering.enabled=FestivalWorld.Readable(view,lettering.transform,SignRange*SignRange);
            bool freeze=state!=null&&state.Phase==FestivalSimulation.MinigamePhase;
            if(freeze!=frozen){frozen=freeze;Time.timeScale=freeze?0:1;}
        }
        // Switched off or gone mid-game (leaving the session, a test torn down), it gives time back.
        private void OnDisable(){if(frozen){frozen=false;Time.timeScale=1;}}
        private void OnDestroy(){if(booth!=null)Destroy(booth.gameObject);}

        /// <summary>The booth under parent (null: the scene's root): a festivalgoer rig facing forward, the sign over its shoulder
        /// and the ring round it, lying flat on the ground.</summary>
        public static Transform Build(Transform parent)
        {
            var booth=new GameObject(BoothName).transform;booth.SetParent(parent,false);
            var stranger=FestivalCharacter.Create(booth,"minigame_stranger",Color.white);
            stranger.transform.localScale=Vector3.Scale(Vector3.one*.82f,stranger.ShapeScale);
            // Sized as FestivalWorld's signs are, for its longest line and its two lines.
            int longest=0;foreach(var line in SignText.Split('\n'))longest=Mathf.Max(longest,line.Length);
            float width=longest*LetterSize*1.45f+.5f,height=2*LetterSize*2.25f+.13f;
            Part(booth,"Sign post",PrimitiveType.Cylinder,new Vector3(0,SignHeight/2,-SignBehind-.06f),new Vector3(.08f,SignHeight/2,.08f),"Wood");
            Part(booth,"Sign rim",PrimitiveType.Cube,new Vector3(0,SignHeight,-SignBehind-.01f),new Vector3(width+.08f,height+.08f,.04f),"StageGlowMint");
            Part(booth,"Sign board",PrimitiveType.Cube,new Vector3(0,SignHeight,-SignBehind),new Vector3(width,height,.04f),"Dark");
            // TextMesh draws through its board, so its legend faces forward, readable only from in front (LateUpdate).
            var face=new GameObject("Sign text");face.transform.SetParent(booth,false);face.transform.localPosition=new Vector3(0,SignHeight,-SignBehind+.03f);face.transform.localRotation=Quaternion.Euler(0,180,0);
            var text=face.AddComponent<TextMesh>();text.text=SignText;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.characterSize=LetterSize;text.fontSize=48;text.color=new Color(.96f,.94f,.84f);
            var font=Resources.Load<Font>("FestivalDisplay");if(font!=null){text.font=font;face.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
            var ring=new GameObject(RingName).transform;ring.SetParent(booth,false);
            for(int i=0;i<Dashes;i++)
            {
                var round=Quaternion.Euler(0,360f*i/Dashes,0);
                Part(ring,"Ring dash",PrimitiveType.Cube,round*Vector3.forward*FestivalSimulation.MinigameRingRadius+Vector3.up*.02f,new Vector3(Dash,.03f,.12f),"StageGlowMint").localRotation=round;
            }
            booth.gameObject.SetActive(false);
            return booth;
        }

        /// <summary>Every frame: the booth stands on the view's booth spot, facing the festival's middle, while the festival shows
        /// (its game under way too), and is hidden at camp, once played and where the level has none. It allocates nothing.</summary>
        public static void Show(Transform booth,RoundState state)
        {
            var at=state!=null&&FestivalWorld.ShowsFestival(state.Phase)?FestivalSimulation.BoothAt(state):null;bool shown=at!=null;
            if(booth.gameObject.activeSelf!=shown)booth.gameObject.SetActive(shown);
            if(shown)booth.SetPositionAndRotation(new Vector3(at.X,0,at.Z),Quaternion.LookRotation(new Vector3(-at.X,0,-at.Z)));
        }

        private static Transform Part(Transform parent,string name,PrimitiveType shape,Vector3 at,Vector3 size,string look)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor(look);
            var collider=go.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            return go.transform;
        }
    }
}
