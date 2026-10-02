using UnityEngine;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>
    /// MENU-1 (Matt, 2026-10-01: "we need a real main menu screen ... and we need to tell players what the goal of the game actually
    /// is"): the screen the game opens on, and comes back to on LEAVE SESSION. Over the campsite it shows the name, a tagline that
    /// says what you do, and PLAY, HOW TO PLAY, SETTINGS and QUIT (Home). PLAY asks your name, then HOST A GAME, its port under
    /// ADVANCED, or JOIN A FRIEND (Join), the one page with the host's address. Esc steps back a page (Back), and on Home does
    /// nothing. FestivalHud builds the widgets and wires the buttons; this keeps which page is up.
    /// </summary>
    public sealed class FestivalTitleScreen : MonoBehaviour
    {
        // The name already on the old menu (decision A), without the SUN + MOON FESTIVAL eyebrow that sat over it.
        public const string GameName="AFTER HOURS";
        public const string Tagline="Sell by day. Rescue by night. Get the whole crew home.";
        public enum Page{Home,Play,Join}
        public Page Shown{get;private set;}
        public bool Advanced{get;private set;}
        private GameObject home,play,choices,advanced,join;
        private Texture2D scrimTexture;
        private Sprite scrimSprite;

        /// <summary>Takes the pages FestivalHud built (choices and join inside play, advanced inside choices), shades shade darkest at
        /// the left edge so the title reads over the scene, and opens Home.</summary>
        public void Configure(Image shade,GameObject home,GameObject play,GameObject choices,GameObject advanced,GameObject join)
        {
            this.home=home;this.play=play;this.choices=choices;this.advanced=advanced;this.join=join;
            scrimTexture=new Texture2D(64,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            for(int x=0;x<64;x++)scrimTexture.SetPixel(x,0,new Color(1,1,1,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,63,x))));
            scrimTexture.Apply();
            scrimSprite=Sprite.Create(scrimTexture,new Rect(0,0,64,1),new Vector2(.5f,.5f),100);
            shade.sprite=scrimSprite;
            Open(Page.Home);
        }
        public void Open(Page page)
        {
            Shown=page;
            home.SetActive(page==Page.Home);play.SetActive(page!=Page.Home);
            choices.SetActive(page==Page.Play);join.SetActive(page==Page.Join);advanced.SetActive(Advanced);
        }
        public void ToggleAdvanced(){Advanced=!Advanced;advanced.SetActive(Advanced);}
        /// <summary>Esc and BACK: JOIN A FRIEND steps back to PLAY, PLAY to the title, and the title stays.</summary>
        public void Back(){if(Shown!=Page.Home)Open(Shown==Page.Join?Page.Play:Page.Home);}
        /// <summary>The title's music: ORGANIC HOUSE in CampFeatures.Tracks (MUSIC-2's decision A), which FestivalCampAudio plays
        /// until a game connects.</summary>
        public const int MusicTrack=3;
        // The title's view of the campsite: from 8 m up over its south end, inside the ring of trees round the camp, DriftDegrees
        // either way round the gathering mat (Pivot) and back over DriftSeconds, looking over the mat towards the trailhead, turned
        // AimLeft so the camp sits right of the title's column, through a narrower lens than play's 75°.
        public const float DriftSeconds=90,DriftDegrees=12,AimLeft=16,Fov=60;
        private static readonly Vector3 Pivot=new Vector3(0,0,2),Boom=new Vector3(0,8,-17),Aim=new Vector3(0,1.5f,0);
        /// <summary>Where the title's camera is seconds into the drift and where it looks; still (reduced motion) holds the drift's
        /// middle. FestivalHud moves the camera here until a game connects: the session drives it only from then.</summary>
        public static void Shot(float seconds,bool still,out Vector3 position,out Quaternion rotation)
        {
            var sway=Quaternion.Euler(0,still?0:Mathf.Sin(seconds*2*Mathf.PI/DriftSeconds)*DriftDegrees,0);
            position=Pivot+sway*Boom;
            rotation=Quaternion.Euler(0,-AimLeft,0)*Quaternion.LookRotation(Pivot+Aim-position);
        }
        /// <summary>QUIT: the game closes; in the editor, play mode stops.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        private void OnDestroy(){if(scrimSprite!=null)Destroy(scrimSprite);if(scrimTexture!=null)Destroy(scrimTexture);}
    }
}
