using Festival.Core;
using Festival.Network;
using TMPro;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>MINI-1 (docs/MINIGAMES_2026-09-30.md): the level's minigame booth where the view has one (FestivalSimulation.BoothAt),
    /// a stranger under a sign naming its game in the middle of a glowing ring the size of the rules' (MinigameRingRadius). Nothing in it
    /// collides, so only the rules decide who stands in the ring. While its game runs the festival holds as a freeze-frame: time
    /// stops for everything drawn on frame time (people, the crowd, the stage's lights), the Ferris wheel holds on the world's clock
    /// (FestivalTwistVisuals) and FestivalNightLighting drains a little colour. So whatever a game draws while it runs (MINI-2..4)
    /// runs on unscaled time, as the HUD does. ponytail: a people rig and primitives, as no booth model exists and the art is frozen.
    /// MINI-4: a Mystery Cups booth sets its cups out in a numbered row on a table in front of the stranger (ShowCups); the cup under
    /// the aim dot is the one its player drinks (AimedCup), and in the tripper's view the cup their hunch marks glows. MINI-2: the
    /// Drum Circle's song comes with the booth (FestivalDrumAudio). MINI-3: the Light Show comes with it too, its stranger the glover
    /// (FestivalLightShowView).</summary>
    [RequireComponent(typeof(FestivalDrumAudio),typeof(FestivalLightShowView))]
    public sealed class FestivalMinigameView : MonoBehaviour
    {
        public const string BoothName="Minigame booth",RingName="Minigame ring",SignRule="\nALL IN THE RING",SignText="MINIGAME"+SignRule;
        private const string SignName="Sign text";
        // The ring's Dashes, each Dash m of its 25 m round. The sign's board stands SignBehind m behind the stranger, its middle
        // SignHeight up, over their head, its letters readable from the front.
        private const int Dashes=24;
        private const float Dash=.6f,SignBehind=.7f,SignHeight=2.45f,LetterSize=.065f;
        // MINI-4: the cups, CupGap m apart in a row on a TableTop m high table TableAhead m in front of the stranger, each CupWidth
        // across and CupHeight tall, its number NumberAbove m over its middle. The cup the player up looks at lifts AimLift m; the
        // aim picks the cup nearest its line within AimDegrees. Built for a full crew of eight's cups; ShowCups shows those played.
        public const string CupsName="Mystery cups",CupLook="Cream",HunchLook="StageGlowRed";
        private const float CupGap=.3f,TableTop=.82f,TableAhead=.95f,CupWidth=.16f,CupHeight=.2f,NumberAbove=.24f,AimLift=.08f,AimDegrees=6;
        private static readonly int MaxCups=FestivalSimulation.CupsFor(8);
        private FestivalSession session;
        private Transform booth;
        private bool frozen;
        private string signKind;
        /// <summary>The booth this view draws, built on its first frame; null before.</summary>
        public Transform Booth=>booth;

        // After FestivalSession.Update has taken this frame's view of the round.
        private void LateUpdate()
        {
            if(session==null){session=GetComponent<FestivalSession>();if(session==null)return;}
            if(booth==null){booth=Build(null);signKind=null;}
            var state=session.State;Show(booth,state);
            if(state!=null&&state.Booth.Kind!=signKind){signKind=state.Booth.Kind;NameGame(booth,signKind);}
            var view=FestivalCharacter.ViewTransform;
            ShowCups(booth,state,state!=null&&FestivalSimulation.CupsTurn(state)==session.LocalPlayerId?AimedCup(state,view):-1,view);
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
            int longest=SignText.Length-SignRule.Length;foreach(var sign in Signs)foreach(var line in sign.Split('\n'))longest=Mathf.Max(longest,line.Length);
            float width=longest*LetterSize*1.45f+.5f,height=2*LetterSize*2.25f+.13f;
            Part(booth,"Sign post",PrimitiveType.Cylinder,new Vector3(0,SignHeight/2,-SignBehind-.06f),new Vector3(.08f,SignHeight/2,.08f),"Wood");
            Part(booth,"Sign rim",PrimitiveType.Cube,new Vector3(0,SignHeight,-SignBehind-.01f),new Vector3(width+.08f,height+.08f,.04f),"StageGlowMint");
            Part(booth,"Sign board",PrimitiveType.Cube,new Vector3(0,SignHeight,-SignBehind),new Vector3(width,height,.04f),"Dark");
            // A label reads from its back (-z), so the legend turns to face forward, just clear of the board that hides it from behind.
            var face=new GameObject(SignName);face.transform.SetParent(booth,false);face.transform.localPosition=new Vector3(0,SignHeight,-SignBehind+.03f);face.transform.localRotation=Quaternion.Euler(0,180,0);
            FestivalWorldText.Add(face,SignText,48*LetterSize,FestivalWorldText.Cream);
            var ring=new GameObject(RingName).transform;ring.SetParent(booth,false);
            for(int i=0;i<Dashes;i++)
            {
                var round=Quaternion.Euler(0,360f*i/Dashes,0);
                Part(ring,"Ring dash",PrimitiveType.Cube,round*Vector3.forward*FestivalSimulation.MinigameRingRadius+Vector3.up*.02f,new Vector3(Dash,.03f,.12f),"StageGlowMint").localRotation=round;
            }
            // MINI-4: the cups' table and a full crew's cups, each a body under its number, which turns to face the view (ShowCups).
            var cups=new GameObject(CupsName).transform;cups.SetParent(booth,false);
            Part(cups,"Cup table",PrimitiveType.Cube,new Vector3(0,TableTop/2,TableAhead),new Vector3(TableWidth(MaxCups),TableTop,.5f),"Wood");
            for(int i=0;i<MaxCups;i++)
            {
                var cup=new GameObject(CupName(i)).transform;cup.SetParent(cups,false);cup.localPosition=CupLocal(i,MaxCups);
                Part(cup,"Cup body",PrimitiveType.Cylinder,Vector3.zero,new Vector3(CupWidth,CupHeight/2,CupWidth),CupLook);
                var number=new GameObject("Cup number");number.transform.SetParent(cup,false);number.transform.localPosition=Vector3.up*NumberAbove;
                FestivalWorldText.Add(number,(i+1).ToString(),48*LetterSize,FestivalWorldText.Cream);
            }
            booth.gameObject.SetActive(false);
            return booth;
        }
        /// <summary>MINI-4: the name of cup i's object under the booth's cups (CupsName); the HUD and the cups number it i + 1.</summary>
        public static string CupName(int i)=>"Cup "+(i+1);

        /// <summary>Every frame: the booth stands on the view's booth spot, facing the festival's middle, while the festival shows
        /// (its game under way too), and is hidden at camp, once played and where the level has none. It allocates nothing.</summary>
        public static void Show(Transform booth,RoundState state)
        {
            var at=state!=null&&FestivalWorld.ShowsFestival(state.Phase)?FestivalSimulation.BoothAt(state):null;bool shown=at!=null;
            if(booth.gameObject.activeSelf!=shown)booth.gameObject.SetActive(shown);
            if(shown)booth.SetPositionAndRotation(new Vector3(at.X,0,at.Z),Facing(at));
        }
        /// <summary>The sign names the booth's game (FestivalHudText.MinigameName), or reads MINIGAME for one this build cannot name.</summary>
        public static void NameGame(Transform booth,string kind)=>booth.Find(SignName).GetComponent<TextMeshPro>().text=SignFor(kind);
        // The games a booth can deal and the ones still to join the bag; the board is built wide enough for each name.
        private static readonly string[] SignKinds={FestivalSimulation.MysteryCupsGame,FestivalSimulation.DrumCircleGame,FestivalSimulation.LightShowGame};
        private static readonly string[] Signs=System.Array.ConvertAll(SignKinds,k=>FestivalHudText.MinigameName(k)+SignRule);
        private static string SignFor(string kind){int i=System.Array.IndexOf(SignKinds,kind);return i<0?SignText:Signs[i];}

        /// <summary>MINI-4, every frame: a Mystery Cups booth's cups, as many as the crew will play before its game (FestivalSimulation.
        /// CupsFor the living crew) and as were poured once it runs, every drunk one gone. The cup the hint marks, which only the
        /// tripper's view carries, glows; `aimed`, the cup the player up looks at, lifts; numbers turn to face the view. Another game's
        /// booth hides them. It allocates nothing.</summary>
        public static void ShowCups(Transform booth,RoundState state,int aimed,Transform view)
        {
            if(!booth.gameObject.activeSelf)return;
            var cups=booth.Find(CupsName);var b=state.Booth;
            bool shown=b.Kind==FestivalSimulation.MysteryCupsGame;if(cups.gameObject.activeSelf!=shown)cups.gameObject.SetActive(shown);
            if(!shown)return;
            int crew=0;foreach(var p in state.Players)if(p.Connected&&p.Life=="Alive")crew++;
            int count=state.Phase==FestivalSimulation.MinigamePhase?b.Cups:FestivalSimulation.CupsFor(crew);
            cups.GetChild(0).localScale=new Vector3(TableWidth(count),TableTop,.5f);
            for(int i=0;i<MaxCups;i++)
            {
                var cup=cups.GetChild(i+1);bool on=i<count&&!b.Drunk.Contains(i);
                if(cup.gameObject.activeSelf!=on)cup.gameObject.SetActive(on);
                if(!on)continue;
                cup.localPosition=CupLocal(i,count)+(i==aimed?Vector3.up*AimLift:Vector3.zero);
                cup.GetChild(0).GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor(i==b.Hint?HunchLook:CupLook);
                if(view!=null){var number=cup.GetChild(1);number.rotation=Quaternion.LookRotation(number.position-view.position);}
            }
        }
        /// <summary>MINI-4: where the middle of cup i of `cups` stands, on the table of the booth at `at`, which the aim is taken to.</summary>
        public static Vector3 CupAt(WorldPoint at,int i,int cups)=>new Vector3(at.X,0,at.Z)+Facing(at)*CupLocal(i,cups);
        /// <summary>MINI-4: the cup on the table that view looks most nearly at, within AimDegrees of its line, for the Mystery Cups game
        /// under way; -1 for none, and for a gap where a cup was drunk, so the aim never slides onto the cup beside it.</summary>
        public static int AimedCup(RoundState state,Transform view)
        {
            var b=state?.Booth;var at=state==null?null:FestivalSimulation.BoothAt(state);
            if(view==null||at==null||state.Phase!=FestivalSimulation.MinigamePhase||b.Kind!=FestivalSimulation.MysteryCupsGame||b.ScoredAt>=0)return -1;
            int best=-1;float nearest=AimDegrees;
            for(int i=0;i<b.Cups;i++){float off=Vector3.Angle(view.forward,CupAt(at,i,b.Cups)-view.position);if(off<nearest){nearest=off;best=i;}}
            return best>=0&&b.Drunk.Contains(best)?-1:best;
        }
        // The booth faces the festival's middle; its cups stand in a row across its front, the middle of the row before the stranger.
        private static Quaternion Facing(WorldPoint at)=>Quaternion.LookRotation(new Vector3(-at.X,0,-at.Z));
        // The table is as long as the row of cups on it, with a hand's width spare at each end.
        private static float TableWidth(int cups)=>Mathf.Max(1,cups)*CupGap+.3f;
        private static Vector3 CupLocal(int i,int cups)=>new Vector3((i-(cups-1)*.5f)*CupGap,TableTop+CupHeight/2,TableAhead);

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
