using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>Native festival interface assembled from a small, consistent set of UI primitives.</summary>
    [DefaultExecutionOrder(60)]
    public sealed class FestivalHud : MonoBehaviour
    {
        private static readonly Color Ink=new Color(.035f,.065f,.075f,1);
        private static readonly Color DeepInk=new Color(.018f,.041f,.052f,1);
        private static readonly Color Paper=new Color(.948f,.928f,.852f,1);
        private static readonly Color MutedPaper=new Color(.735f,.765f,.729f,1);
        private static readonly Color Orange=new Color(.99f,.465f,.255f,1);
        private static readonly Color Mint=new Color(.385f,.86f,.725f,1);
        private static readonly Color Hairline=new Color(.51f,.56f,.50f,.35f);
        // The threat bar warms from mint to a hot red with the band (ThreatBands). Its word always names the band too, so colour is
        // never the only cue.
        private static readonly Color[] ThreatColors={Mint,new Color(1,.76f,.29f,1),Orange,new Color(1,.33f,.3f,1),new Color(1,.16f,.24f,1)};
        /// <summary>The lowest edge of the top band's cards (the objective card's foot; the clock, tripper and player cards end above it),
        /// as a share of the screen's height.</summary>
        public const float TopCardsBottom=.858f;
        private static readonly Color[] RhythmColors={new Color(1,.38f,.61f,1),new Color(1,.76f,.29f,1),new Color(.34f,.78f,1,1),new Color(.46f,.95f,.68f,1)};
        private static readonly float[] RhythmAngles={90,180,0,-90};
        // The rhythm lane's receptor wells span WellBottom to WellTop with each outline arrow centred in its well, so a note
        // is on time at NoteHitY, the wells' middle. A missed note keeps rising for MissOvershootSeconds past it.
        private const float WellBottom=.766f,WellTop=.895f,NoteStartY=.11f,NoteHitY=(WellBottom+WellTop)/2;
        private const double MissOvershootSeconds=.18;
        // DANCE-12: the lane's bottom line, "ARROWS OR WASD" left of StopHoldLeft and "HOLD G TO STOP DANCING" from there to StopHoldRight.
        private const float StopHoldLeft=.52f,StopHoldRight=.94f;
        private FestivalSession session;
        private Canvas canvas;
        private Font font;
        private Font displayFont;
        private readonly List<Text> authoredLabels=new List<Text>();
        private readonly Dictionary<Text,(int Size,int Minimum,int Maximum)> authoredTextSizes=new Dictionary<Text,(int,int,int)>();
        private float appliedTextScale=1;
        private float textScale=>session?.Profile?.Data.TextScale??1;
        private GameObject ridePanel;
        private FestivalDjRide ride;
        private Text rideCombo,rideControls;
        private RhythmChart rideChart;
        private string rideId="";
        private int rideLane=DjRide.StartLane,ridePhrase=-1;
        private GameObject titlePanel, menuPanel, menuShade, actionsPanel, rhythmPanel, rhythmShade, dancePanel, dialoguePanel, mapPanel, mapShade, noticePanel, promptPanel, trippingPanel, rosterPanel, inventoryPanel, reticle,settingsPanel,menuHome,checkoutPanel,heldDetailPanel,reviewPanel;
        private FestivalDancePreview dancePreview;
        private GameObject objectivePanel, timerPanel, vitalPanel;
        private GameObject testModeTag, testModeButton, clearLevelButton;
        private RectTransform crewButton;
        private RectTransform threatFill;
        private Image threatFillImage;
        private Text threatWord;
        private float threatShown;
        private RectTransform actionsContent;
        private ScrollRect actionsScroll;
        private GameObject nextButton, cancelButton, resumeButton, promptKeycap;
        private Text status, notice, levelBanner, objective, objectiveTitle, vitals, roster, menuRoster, menuPhase, campSecrets, prompt, preview, rhythmStatus, rhythmDialogue, rhythmJudgment, rhythmCombo, rhythmTiming, dancerHeading, dancerCaption, dialogueSpeaker, dialogueLine, mapText, mapTitle, voice, timerText,checkoutText,settingsText,heldDetailText,heldTitle,heldPrice,inventoryHeading,emptyGearLabel,reviewText,reviewReveal,reviewStatus,tripping;
        // SET-1: the one settings screen the title and the pass open (FestivalSettingsScreen): its tab buttons, a widget row for each
        // of its Rows, and the line saying what the platform is actually doing.
        private FestivalSettingsScreen settingsScreen;
        private readonly List<(FestivalSettingsScreen.Tab Tab,Image Fill,Text Label)> settingsTabs=new List<(FestivalSettingsScreen.Tab,Image,Text)>();
        private readonly List<(FestivalSettingsScreen.Row Row,GameObject[] Parts,Text Value,Button[] Buttons)> settingsRows=new List<(FestivalSettingsScreen.Row,GameObject[],Text,Button[])>();
        private readonly List<(string Section,Image Fill,Text Label,GameObject Button)> settingsSections=new List<(string,Image,Text,GameObject)>();
        private Text settingsStatus;
        private RectTransform settingsMeter;
        // The settings' tap test plays its pulses here, so the shop chime's source never has to be idle for it.
        private AudioSource tapAudio;
        private RectTransform mapPlayerMarker;
        private GameObject campMap,festivalMap;
        private readonly Image[] slotFrames=new Image[3];
        private readonly Text[] slotTexts=new Text[3];
        private InputField nameField, addressField, portField, hostPortField;
        // MENU-1: the title screen, where the connection card was, and the settings' way out, which says BACK there.
        private FestivalTitleScreen titleScreen;
        private Text settingsBack;
        private readonly List<Image> noteViews = new List<Image>();
        private readonly List<Image> holdTails=new List<Image>();
        private Transform noteContainer,tailContainer;
        private readonly Image[] rhythmReceptors=new Image[4];
        private readonly Image[] rhythmReceptorWells=new Image[4];
        private readonly Text[] rhythmKeys=new Text[4];
        private Text promptKey,rhythmControls,mapHint;
        private readonly Dictionary<InputAction,(string Path,int Layout,string Label)> keyLabels=new Dictionary<InputAction,(string Path,int Layout,string Label)>();
        private readonly float[] rhythmFlashUntil=new float[4];
        private bool[] rhythmConsumed=Array.Empty<bool>(),rhythmHeld=Array.Empty<bool>(),rhythmDropped=Array.Empty<bool>();
        private Image rhythmProgressFill,stopHoldFill,walkHoldFill;
        // When G went down mid-dance (Time.unscaledTime), or -1 while it isn't held.
        private float stopHeldSince=-1;
        private float gearHeldSince=-1;
        private bool dropPressConsumed;
        private string dropHeldItem="",dropRound="",dropPhase="";
        private int dropGeneration=-1,dropCancellationSequence=-1;
        // After a manual choice, an old snapshot cannot authorize a Drop even if its two-second prop confirmation expires.
        private long manualEquipmentTick=-1;
        private string gearLossRound="",gearLossNotice="";
        private int gearLossGeneration=-1;
        private long gearLossSequence;
        private float gearLossShown;
        public const float GearDropHoldSeconds=.5f;
        private RhythmChart displayedChart;
        private RhythmJudge feedbackJudge;
        private readonly List<RhythmInput> replayedRhythmInputs=new List<RhythmInput>();
        private int rhythmConnectionGeneration=-1;
        private bool rhythmFocused=true;
        private string displayedInteractionId="";
        private int displayedSeed=-1,displayedNoteCount=-1,displayedPhrase=-1,processedRhythmInputs,rhythmComboCount,rhythmHitCount;
        private int shownRhythmHits=-1,shownRhythmBpm=-1,shownRhythmCount=-1,shownRhythmCombo=-1;
        private double displayedBeatSeconds=-1,lastRhythmError;
        private string lastRhythmJudgment="";
        private float rhythmJudgmentUntil;
        private readonly List<GameObject> dynamicActions = new List<GameObject>();
        private readonly List<ItemStack> handGear = new List<ItemStack>(3);
        private int activeActionCount;
        private Action primaryAction;
        private int selectedSlot;
        private bool pendingEquipment;
        private string pendingEquipmentItem="",pendingEquipmentRound="",pendingEquipmentCommand="";
        private const float EquipmentConfirmationSeconds=2;
        private float pendingEquipmentAt;
        private int pendingEquipmentGeneration,pendingEquipmentReceipt;
        // HUD-8: the line over the gear bar saying what a gear key or the wheel just switched to, or why not, until gearToastUntil;
        // and when the wheel last stepped (FestivalHudGear.Notch).
        private GameObject gearToast;
        private Text gearToastText;
        private float gearToastUntil,lastGearStep=-1;
        private string checkoutItem="";
        private string connectionError="";
        private bool settingsOpen,wasConnected;
        private bool crewOpen;
        private AudioSource shopAudio;
        private AudioClip shopChime;
        // Played on shopAudio, so the effects volume applies to it as to the chime.
        private AudioClip ohYeah;
        private readonly FestivalHudText.GiggleTankNews giggleTankNews=new FestivalHudText.GiggleTankNews();
        public int OhYeahPlays{get;private set;}
        private Texture2D discTexture;
        private Sprite discSprite;
        private Texture2D roundedTexture;
        private Sprite roundedSprite;
        private Texture2D arrowTexture,arrowOutlineTexture;
        private Sprite arrowSprite,arrowOutlineSprite;
        private int previousCash=-1,previousUnits=-1;
        private string previousRound="";
        private bool? appliedContrast;
        private bool? expandedGear;
        private int rosterLines;
        // What the dose card was last measured with; a change to any of them measures it again.
        private string trippingText;
        private Font trippingFont;
        private int trippingSize=-1;
        private float trippingWidth,trippingPixels,trippingCanvas,trippingScale;
        // Above and below the wrapped text, at 100% text: one line comes to the card's authored height.
        private const float TripperPadding=22;
        // The debrief ballot: a row per award (up to three) of buttons, one per connected crew member (up to eight).
        private readonly Text[] ballotLabels=new Text[3];
        private readonly GameObject[,] ballotPicks=new GameObject[3,8];
        private readonly List<string> ballotCrew=new List<string>(8);
        private int ballotColumns;
        // The verdict plays from when this client first sees every vote in (FestivalHudText.ReviewReveal).
        private string revealRound="";
        private float revealAt;
        // DIALOG-5: the chat window, the reply 1-3 picked this frame for it to take once it has followed the chat (-1: none), and
        // whether it sits beside the pass (the menu is open) rather than in play's spot.
        private FestivalChatWindow chatWindow;
        private int chatConnectionGeneration=-1;
        // CAMP-1: Marigold's talk at camp, on the chat window, and the world she stands in (FestivalWorld.CampSeller).
        private readonly CampSellerTalk sellerTalk=new CampSellerTalk(Environment.TickCount);
        private FestivalWorld world;
        private int chatReply=-1;
        // DIALOG-6: the one-line chat (Talk, a guard's bark) the dialogue card shows, kept up until read.
        private readonly FestivalTalkLine talkLine=new FestivalTalkLine();
        private FestivalSpeechReveal dialogueReveal;
        private bool? chatBesidePass;
        // ECON-2: the buy from a festivalgoer under way, the kind it buys and how many of that kind were held as it began. K1: and
        // whom from, as "<round id>/<their id>", kept once they hand a unit over (soldToYou): a seller never buys, so beside them E
        // buys again rather than offering the unit back (FestivalHudText.FestivalgoerActions).
        // ponytail: a key per buy, never pruned (a weekend makes a few dozen); clear it on a new round if that ever matters.
        private string buyInteractionId="",buyItem="",buySeller="";
        // ECON-8: the Pickup sent for a random find, and its amount, until the host answers it.
        private string foundCommand="";
        private int foundAmount;
        private int cashWhileBuying;
        private readonly HashSet<string> soldToYou=new HashSet<string>();
        private Image effectWash;
        private float baseFov=>session?.Profile?.Data.FieldOfView??75;
        private float nextActionRefresh;
        private string lastNotice="";
        private float noticeUntil;
        // ECON-3: the stock the last Q asked about taking (TakesOnQ), and when; "" when none.
        private string qAsked="";
        private float qAskedAt;
        // ECON-3: beside a festivalgoer or a cop, the strip under the prompt with what else you can do there, its line count as laid
        // out, and the labels it is built from (kept, so the 5 Hz refresh reuses one list).
        private GameObject nearbyPanel;
        private Text nearby;
        private int nearbyLines=-1;
        private readonly List<string> nearbyLabels=new List<string>();
        // ESC-2: whether the stop on the local player has had its session notice (FestivalHudText.StopNotice). TRIP-8: whether the
        // cloud's stash they found has had its own (FestivalHudText.CloudStashNotice), and a vision's (VisionStashNotice).
        private bool stopNoticed,stashNoticed,visionStashNoticed;
        // PLAYA-5: the round and receipt of the last deck set tips shown in the session notice.
        private string deckTipNoticed="";
        // LOOP-5: the sundown warning (FestivalHudText.SundownWarning) on a card of its own, so no reply in the session notice replaces
        // it, and the results banner. Each warning, a round's SundownStage, stays SundownNoticeSeconds on screen; its seconds run only
        // while it shows, so a dance, the map or the menu hiding it never uses them up. Mid-dance the lanes' title line says
        // it instead (rhythmSundown) for the whole last minute.
        public const float SundownNoticeSeconds=6;
        private GameObject sundownPanel,resultsPanel;
        private Text sundownText,resultsTitle,resultsDetail,rhythmSundown;
        private Image resultsAccent;
        private string sundownRound="";
        private int sundownStage;
        private (int Festival,int Level,int Encore,int Spin) sundownLevel;
        private readonly float[] sundownProgress=new float[3];
        private float sundownShown;
        // HUD-7: How to Play over the main menu or the pass (BuildHowToPlay), its six blocks' titles and text, and whether it's up.
        private GameObject howToPlayPanel;
        private readonly Text[] howToPlayTitles=new Text[6],howToPlayTexts=new Text[6];
        private bool howToPlayOpen;
        private GameObject comfortPanel;
        private Text comfortError;
        private Action pendingComfortStart;
        // HUD-7: the level briefing (FestivalHudText.Briefing), its fade, its three lines, the briefing they were laid out from, and the
        // round a key put it away for.
        private GameObject briefingPanel;
        private CanvasGroup briefingFade;
        private Text briefingGoal,briefingJob,briefingStakes;
        private string shownBriefing="",briefingSkipped="";
        // HUD-7: the first-time tip up (FestivalHudText.Tip), the round it came in, and how long it has shown: its seconds run only
        // while nothing covers it, as the sundown warning's do.
        private GameObject tipPanel;
        private Text tipText;
        private GameObject lifePanel,spiritTag;
        private Text lifeTitle,lifeDetail;
        private float lifeUntil;
        private readonly FestivalHudText.LifeNews lifeNews=new FestivalHudText.LifeNews();
        private const float LifeBannerSeconds=6;
        private GameObject saleStampPanel;
        private Text saleStamp;
        private float saleStampAt=-100;
        private bool saleFlash;
        private readonly FestivalHudText.SaleNews saleNews=new FestivalHudText.SaleNews();
        private string tipId="",tipRound="";
        private float tipShown;
        // MINI-1: the minigame card (FestivalHudText.MinigameCard): the booth's ring and countdown, the game under way, its result.
        private GameObject minigamePanel;
        private Text minigameTitle,minigameDetail;
        // MINI-2: the minigame card beside a drummer's lanes, over the dimmer that covers the card itself.
        private GameObject drumPanel;
        private Text drumTitle,drumStrip,stopHoldLabel;
        // MINI-3: the Light Show's question card over the minigame card: the open question and its five answers, then the reveal.
        // lightShowSent: the question slot a click has answered this frame or since, before the host's view says so; -1 none.
        private GameObject lightShowPanel;
        private Text lightShowPrompt,lightShowReveal;
        private readonly Button[] lightShowAnswers=new Button[5];
        private readonly Text[] lightShowLabels=new Text[5],lightShowKeys=new Text[5];
        private int lightShowSent=-1,lightShowChoice,lightShowFocus=-1;
        private static readonly string[] AnswerKeys={"1","2","3","4","5"};
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Text diagnostics;
        public bool DevelopmentMapVisible;
#endif

        private void Awake()
        {
            session=GetComponent<FestivalSession>();
            if(session==null)session=FindFirstObjectByType<FestivalSession>();
            world=FindFirstObjectByType<FestivalWorld>();
            displayFont=Resources.Load<Font>("FestivalDisplay");
            if(displayFont==null)displayFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // The AFTER HOURS wordmark's Boogaloo face is the festival's
            // interface typeface, from button labels through body copy.
            font=displayFont;
            discTexture=new Texture2D(48,48,TextureFormat.RGBA32,false);
            var discPixels=new Color[48*48];
            for(int y=0;y<48;y++)for(int x=0;x<48;x++)
            {
                float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(24,24));
                discPixels[y*48+x]=new Color(1,1,1,Mathf.Clamp01((24-radius)*1.8f));
            }
            discTexture.SetPixels(discPixels);discTexture.Apply();discTexture.filterMode=FilterMode.Bilinear;
            discSprite=Sprite.Create(discTexture,new Rect(0,0,48,48),new Vector2(.5f,.5f),100);
            roundedTexture=new Texture2D(48,48,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
            var corners=new Color[48*48];
            for(int y=0;y<48;y++)for(int x=0;x<48;x++)
            {
                var q=new Vector2(Mathf.Max(Mathf.Abs(x-23.5f)-15.5f,0),Mathf.Max(Mathf.Abs(y-23.5f)-15.5f,0));
                corners[y*48+x]=new Color(1,1,1,Mathf.Clamp01(8.5f-q.magnitude));
            }
            roundedTexture.SetPixels(corners);roundedTexture.Apply();
            roundedSprite=Sprite.Create(roundedTexture,new Rect(0,0,48,48),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(12,12,12,12));
            arrowSprite=CreateArrowSprite(false,out arrowTexture);
            arrowOutlineSprite=CreateArrowSprite(true,out arrowOutlineTexture);
            shopAudio=gameObject.AddComponent<AudioSource>();shopAudio.spatialBlend=0;shopAudio.playOnAwake=false;
            const int rate=22050;var samples=new float[rate/4];
            for(int n=0;n<samples.Length;n++)
            {
                float t=n/(float)rate,envelope=Mathf.Pow(1-t/.25f,2);
                samples[n]=(Mathf.Sin(2*Mathf.PI*660*t)+.55f*Mathf.Sin(2*Mathf.PI*990*t))*envelope*.16f;
            }
            shopChime=AudioClip.Create("Camp seller cash chime",samples.Length,1,rate,false);shopChime.SetData(samples,0);
            var spoken=OhYeahSamples(rate);ohYeah=AudioClip.Create("Giggle Tank oh yeah",spoken.Length,1,rate,false);ohYeah.SetData(spoken,0);
            tapAudio=gameObject.AddComponent<AudioSource>();tapAudio.spatialBlend=0;tapAudio.playOnAwake=false;
            if(session?.Profile!=null)settingsScreen=new FestivalSettingsScreen(session.Profile,session.Settings,session.Voice,session.Controls,tapAudio);
            Build();
        }

        // The oh-yeah's two syllables: when each sounds (s), its pitch glide (Hz), its first three formants from and to (Hz) and
        // their loudness, and how long it fades out (s). A dark, rising "oh", then a brighter "yeah" that falls away.
        private static readonly (float Start,float End,float PitchFrom,float PitchTo,Vector3 FormantsFrom,Vector3 FormantsTo,Vector3 Gains,float Fade)[] OhYeahSyllables=
        {
            (.03f,.46f,96,110,new Vector3(450,800,2600),new Vector3(430,760,2550),new Vector3(1,.45f,.08f),.1f),
            (.53f,1.12f,120,80,new Vector3(300,2200,2950),new Vector3(620,1700,2500),new Vector3(1,.9f,.3f),.35f),
        };
        private static readonly Vector3 FormantWidths=new Vector3(90,110,160);
        /// <summary>The crew's deep "oh yeah!" as a Giggle Tank is grabbed, made in code at `rate` Hz: a voice of 80-120 Hz whose
        /// harmonics each syllable's vowel formants shape, the same every time.</summary>
        public static float[] OhYeahSamples(int rate)
        {
            var samples=new float[(int)(rate*1.18f)];double phase=0;float peak=0;
            for(int n=0;n<samples.Length;n++)
            {
                float t=n/(float)rate;
                foreach(var s in OhYeahSyllables)
                {
                    if(t<s.Start-.04f||t>s.End)continue;
                    float u=Mathf.SmoothStep(0,1,(t-s.Start)/(s.End-s.Start)),pitch=Mathf.Lerp(s.PitchFrom,s.PitchTo,u)*(1+.012f*Mathf.Sin(2*Mathf.PI*5.5f*t));
                    var formants=Vector3.Lerp(s.FormantsFrom,s.FormantsTo,u);
                    phase+=2*Math.PI*pitch/rate;double sound=0;
                    for(int k=1;k*pitch<4000;k++)
                    {
                        float shape=.08f;
                        for(int i=0;i<3;i++){float d=(k*pitch-formants[i])/FormantWidths[i];shape+=s.Gains[i]/(1+d*d);}
                        sound+=shape/Math.Sqrt(k)*Math.Sin(k*phase);
                    }
                    samples[n]=(float)sound*Mathf.SmoothStep(0,1,(t-s.Start+.04f)/.08f)*Mathf.SmoothStep(0,1,(s.End-t)/s.Fade);
                    peak=Mathf.Max(peak,Mathf.Abs(samples[n]));
                }
            }
            for(int n=0;n<samples.Length;n++)samples[n]*=.8f/peak;
            return samples;
        }

        private static bool ArrowInside(float x,float y)
        {
            float center=Mathf.Abs(x-32);
            return (y>=6&&y<=31&&center<=9)||(y>=22&&y<=58&&center<=(58-y)*.72f+1);
        }
        private static Sprite CreateArrowSprite(bool outline,out Texture2D texture)
        {
            texture=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float alpha=0;
                for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)
                {
                    float px=x+(sx+.5f)*.5f,py=y+(sy+.5f)*.5f;
                    bool inside=ArrowInside(px,py);
                    bool edge=inside&&(!ArrowInside(px+3,py)||!ArrowInside(px-3,py)||!ArrowInside(px,py+3)||!ArrowInside(px,py-3));
                    if(outline?edge:inside)alpha+=.25f;
                }
                pixels[y*64+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),100);
        }

        private void Build()
        {
            var root=new GameObject("Festival HUD");root.transform.SetParent(transform,false);
            canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            root.AddComponent<GraphicRaycaster>();
            if(EventSystem.current==null)
            {
                var events=new GameObject("Festival UI events",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
            }
            effectWash=Panel(root.transform,"Effect wash",new Color(0,0,0,0),Vector2.zero,Vector2.one).GetComponent<Image>();effectWash.raycastTarget=false;

            objectivePanel=Card(root.transform,"Objective card",new Vector2(.022f,TopCardsBottom),new Vector2(.355f,.974f),true);
            Accent(objectivePanel.transform,Orange);
            levelBanner=Label(objectivePanel.transform,"Level banner",14,TextAnchor.MiddleLeft);levelBanner.fontStyle=FontStyle.Normal;levelBanner.color=Orange;Place(levelBanner.rectTransform,.06f,.69f,.96f,.94f);
            objectiveTitle=Label(objectivePanel.transform,"Objective title",23,TextAnchor.MiddleLeft);objectiveTitle.fontStyle=FontStyle.Normal;Place(objectiveTitle.rectTransform,.06f,.43f,.96f,.73f);
            // Ember Playa's quota names its odd objects twice ("12 FRIENDSHIP BRACELETS / 44 FRIENDSHIP BRACELETS"), so it may shrink.
            ShrinkToFit(objectiveTitle,14);
            objective=Label(objectivePanel.transform,"Objective detail",18,TextAnchor.MiddleLeft);objective.color=MutedPaper;Place(objective.rectTransform,.06f,.01f,.96f,.46f);
            // LOOP-5: a day's last minute adds "or the weekend restarts" to the quota's line and its next step, so it may shrink.
            ShrinkToFit(objective,14);
            timerPanel=Card(root.transform,"Round clock",new Vector2(.452f,.914f),new Vector2(.548f,.974f),true);
            timerText=Label(timerPanel.transform,"Time",31,TextAnchor.MiddleCenter);timerText.fontStyle=FontStyle.Normal;Fill(timerText.rectTransform,4);ShrinkToFit(timerText,18);
            testModeTag=Card(root.transform,"Test mode tag",new Vector2(.556f,.934f),new Vector2(.648f,.974f),true);Accent(testModeTag.transform,Orange);
            var testModeText=Label(testModeTag.transform,"Test mode",16,TextAnchor.MiddleCenter);testModeText.fontStyle=FontStyle.Normal;testModeText.color=Orange;testModeText.text=FestivalHudText.TestModeTag;Fill(testModeText.rectTransform,3);ShrinkToFit(testModeText,11);
            testModeTag.SetActive(false);
            // Who trips this level and on how many doses, under the clock for the whole crew (UpdateTrippingLayout sizes it).
            trippingPanel=Card(root.transform,"Tripper card",new Vector2(.36f,.864f),new Vector2(.64f,.906f),true);
            Accent(trippingPanel.transform,Orange);
            tripping=Label(trippingPanel.transform,"Tripping",16,TextAnchor.MiddleCenter);tripping.fontStyle=FontStyle.Normal;Fill(tripping.rectTransform,4);ShrinkToFit(tripping,16);trippingPanel.SetActive(false);
            vitalPanel=Card(root.transform,"Player card",new Vector2(.725f,.89f),new Vector2(.978f,.974f),true);
            Accent(vitalPanel.transform,Mint);
            // Health, money, life and the security warning, then any effects, over the threat bar. Ember Playa's odd objects
            // can push the warning to another line, so the text shrinks to fit rather than cut it (TRIP-1, PLAYA-1).
            vitals=Label(vitalPanel.transform,"Vitals",19,TextAnchor.MiddleRight);Place(vitals.rectTransform,.045f,.30f,.94f,.97f);ShrinkToFit(vitals,14);
            // Ticks where the crowd watches, questions and accuses. The bar stays inside the card, so the top band keeps TopCardsBottom.
            var threatTrack=Panel(vitalPanel.transform,"Threat bar",new Color(1,1,1,.12f),new Vector2(.045f,.13f),new Vector2(.62f,.22f)).transform;
            threatFillImage=Panel(threatTrack,"Threat fill",Mint,Vector2.zero,new Vector2(0,1)).GetComponent<Image>();threatFill=threatFillImage.rectTransform;
            foreach(float tick in new[]{.25f,.45f,.70f}){var mark=Rect(Panel(threatTrack,"Threat tick",Paper,new Vector2(tick,-.35f),new Vector2(tick,1.35f)));mark.offsetMin=new Vector2(-1,0);mark.offsetMax=new Vector2(1,0);}
            foreach(var part in threatTrack.GetComponentsInChildren<Image>(true))part.raycastTarget=false;
            threatWord=Label(vitalPanel.transform,"Threat level",15,TextAnchor.MiddleRight);threatWord.fontStyle=FontStyle.Normal;Place(threatWord.rectTransform,.64f,.03f,.94f,.32f);ShrinkToFit(threatWord,11);

            rosterPanel=Card(root.transform,"Crew card",new Vector2(.835f,.846f),new Vector2(.978f,.882f),true);
            roster=Label(rosterPanel.transform,"Roster",16,TextAnchor.MiddleRight);Fill(roster.rectTransform,3);ShrinkToFit(roster,12);
            voice=Label(root.transform,"Voice",17,TextAnchor.LowerLeft);voice.color=MutedPaper;SetRect(voice.rectTransform,new Vector2(.023f,.012f),new Vector2(.23f,.052f),Vector2.zero,Vector2.zero);
            // The upper-left information rail keeps both palms, held props and
            // the ground immediately ahead visible during close interactions.
            promptPanel=Card(root.transform,"Action prompt",new Vector2(.022f,.777f),new Vector2(.355f,.844f),true);
            // Legacy prompt fill is kept for ShowStopHold callers; live chats now leave through the effective Menu binding.
            walkHoldFill=Panel(promptPanel.transform,"Walk hold fill",new Color(Orange.r,Orange.g,Orange.b,.35f),Vector2.zero,new Vector2(0,1)).GetComponent<Image>();walkHoldFill.raycastTarget=false;walkHoldFill.gameObject.SetActive(false);
            promptKeycap=Keycap(promptPanel.transform,"E",new Vector2(.025f,.19f),new Vector2(.11f,.81f));promptKey=promptKeycap.GetComponentInChildren<Text>();ShrinkToFit(promptKey,12);
            prompt=Label(promptPanel.transform,"Prompt",18,TextAnchor.MiddleLeft);Place(prompt.rectTransform,.135f,.08f,.97f,.92f);promptPanel.SetActive(false);
            // A shelf item's prompt carries its price and description, longer on Ember Playa.
            ShrinkToFit(prompt,14);
            // ECON-3: beside a festivalgoer or a cop, what else you can do there, under the prompt; UpdateNearbyLayout sizes it. It stops
            // short of the prompt's width, clear of LOOP-5's centred sundown card (x from .30, y .71-.775).
            nearbyPanel=Card(root.transform,"Nearby actions",new Vector2(.022f,.70f),new Vector2(.295f,.769f),true);
            nearby=Label(nearbyPanel.transform,"Nearby",16,TextAnchor.MiddleLeft);nearby.color=MutedPaper;Place(nearby.rectTransform,.04f,0,.97f,1);ShrinkToFit(nearby,12);nearbyPanel.SetActive(false);
            // The campfire debrief: the verdict on the round, a ballot row per award to click a friend on, who has voted, then
            // the verdict read out award by award where the ballot was.
            reviewPanel=Card(root.transform,"Camp round review",new Vector2(.25f,.20f),new Vector2(.75f,.84f),true);
            Accent(reviewPanel.transform,Orange);
            reviewText=Label(reviewPanel.transform,"Review text",21,TextAnchor.MiddleCenter);Place(reviewText.rectTransform,.05f,.82f,.95f,.96f);ShrinkToFit(reviewText,16);
            reviewReveal=Label(reviewPanel.transform,"Review reveal",25,TextAnchor.MiddleCenter);reviewReveal.fontStyle=FontStyle.Normal;Place(reviewReveal.rectTransform,.05f,.20f,.95f,.80f);
            reviewStatus=Label(reviewPanel.transform,"Review status",18,TextAnchor.MiddleCenter);reviewStatus.color=MutedPaper;Place(reviewStatus.rectTransform,.05f,.03f,.95f,.18f);
            for(int award=0;award<3;award++)
            {
                float top=.79f-award*.195f;
                ballotLabels[award]=Label(reviewPanel.transform,"Award "+(award+1),19,TextAnchor.MiddleLeft);ballotLabels[award].fontStyle=FontStyle.Normal;ballotLabels[award].color=Orange;Place(ballotLabels[award].rectTransform,.05f,top-.07f,.95f,top);
                for(int pick=0;pick<ballotPicks.GetLength(1);pick++)
                {
                    int slot=award,column=pick;
                    var button=Button(reviewPanel.transform,"",new Vector2(.05f,top-.185f),new Vector2(.95f,top-.075f),()=>{if(column<ballotCrew.Count)session.Command("ReviewVote",ballotCrew[column],amount:slot);});
                    button.name="Award "+(award+1)+" pick "+(pick+1);ballotPicks[award,pick]=button;
                    // A crew of eight shares the row, so a long name shrinks to fit its button.
                    ShrinkToFit(button.transform.Find("Text").GetComponent<Text>(),12);
                }
            }
            reviewPanel.SetActive(false);
            saleStampPanel=Card(root.transform,"Sale stamp",new Vector2(.36f,.6f),new Vector2(.64f,.7f),true);Accent(saleStampPanel.transform,Orange);
            saleStamp=Label(saleStampPanel.transform,"Sale result",44,TextAnchor.MiddleCenter);saleStamp.color=Orange;Fill(saleStamp.rectTransform,10);ShrinkToFit(saleStamp,20);saleStampPanel.SetActive(false);
            noticePanel=Card(root.transform,"Session notice",new Vector2(.368f,.785f),new Vector2(.695f,.849f),true);
            Accent(noticePanel.transform,Orange);
            notice=Label(noticePanel.transform,"Notice",20,TextAnchor.MiddleCenter);Fill(notice.rectTransform,14);ShrinkToFit(notice,14);noticePanel.SetActive(false);
            // LOOP-5: the sundown warning, just under the session notice and clear of the prompt and the price confirmation.
            sundownPanel=Card(root.transform,"Sundown warning",new Vector2(.30f,.71f),new Vector2(.70f,.775f),true);
            Accent(sundownPanel.transform,Orange);
            sundownText=Label(sundownPanel.transform,"Sundown",22,TextAnchor.MiddleCenter);sundownText.fontStyle=FontStyle.Normal;sundownText.color=Orange;Fill(sundownText.rectTransform,12);ShrinkToFit(sundownText,14);sundownPanel.SetActive(false);
            inventoryPanel=Card(root.transform,"Equipment bar",new Vector2(.714f,.024f),new Vector2(.978f,.148f),true);
            inventoryHeading=Label(inventoryPanel.transform,"Equipment heading",17,TextAnchor.UpperLeft);inventoryHeading.fontStyle=FontStyle.Normal;inventoryHeading.color=Mint;inventoryHeading.text=FestivalHudText.GearHeading(true);
            Place(inventoryHeading.rectTransform,.045f,.75f,.98f,.96f);ShrinkToFit(inventoryHeading,12);
            emptyGearLabel=Label(inventoryPanel.transform,"Empty gear",20,TextAnchor.MiddleCenter);
            emptyGearLabel.color=Mint;Fill(emptyGearLabel.rectTransform,6);emptyGearLabel.gameObject.SetActive(false);
            for(int i=0;i<3;i++)
            {
                float left=.045f+i*.313f;
                var slot=Card(inventoryPanel.transform,"Slot "+(i+1),new Vector2(left,.08f),new Vector2(left+.293f,.70f),true);
                slotFrames[i]=slot.GetComponent<Image>();
                slotTexts[i]=Label(slot.transform,"Slot label",18,TextAnchor.MiddleCenter);Fill(slotTexts[i].rectTransform,4);
            }
            // HUD-8: a switch flashes just over the bar (UpdateKeyboard), riding on it, so it hides whenever the bar does.
            gearToast=Card(inventoryPanel.transform,"Gear switch line",Vector2.zero,Vector2.one,true);SetRect(Rect(gearToast),new Vector2(0,1),Vector2.one,new Vector2(0,8),new Vector2(0,52));
            gearToastText=Label(gearToast.transform,"Gear switch",20,TextAnchor.MiddleCenter);gearToastText.color=new Color(1,.79f,.53f);Fill(gearToastText.rectTransform,6);ShrinkToFit(gearToastText,14);gearToast.SetActive(false);
            reticle=Panel(root.transform,"Aim dot",Paper,new Vector2(.499f,.498f),new Vector2(.501f,.502f));
            reticle.GetComponent<Image>().raycastTarget=false;
            // HUD-7: the level briefing hangs from just under the aim dot, where the tripper's one hint used to sit, for the first
            // BriefingSeconds of each level: what it asks, your job (the tripper's carries "Trust, but verify.") and how the crew loses.
            // Built before the results banner, the map and the menus so they draw over it; it catches no clicks.
            briefingPanel=Card(root.transform,"Level briefing",new Vector2(.25f,.285f),new Vector2(.75f,.455f),true);briefingPanel.GetComponent<Image>().raycastTarget=false;
            Accent(briefingPanel.transform,Orange);
            briefingFade=briefingPanel.AddComponent<CanvasGroup>();briefingFade.blocksRaycasts=false;
            briefingGoal=Label(briefingPanel.transform,"Briefing goal",24,TextAnchor.MiddleCenter);briefingGoal.fontStyle=FontStyle.Normal;briefingGoal.color=Orange;Place(briefingGoal.rectTransform,.04f,.68f,.96f,.93f);ShrinkToFit(briefingGoal,16);
            briefingJob=Label(briefingPanel.transform,"Briefing job",19,TextAnchor.MiddleCenter);Place(briefingJob.rectTransform,.04f,.29f,.96f,.68f);ShrinkToFit(briefingJob,14);
            briefingStakes=Label(briefingPanel.transform,"Briefing stakes",17,TextAnchor.MiddleCenter);briefingStakes.color=MutedPaper;Place(briefingStakes.rectTransform,.04f,.06f,.96f,.29f);ShrinkToFit(briefingStakes,12);
            briefingPanel.SetActive(false);
            // MINI-1: the minigame card where the briefing hangs, which makes way for it (Update). Built before the map and the menus so
            // they draw over it; it catches no clicks.
            minigamePanel=Card(root.transform,"Minigame card",new Vector2(.31f,.285f),new Vector2(.69f,.42f),true);minigamePanel.GetComponent<Image>().raycastTarget=false;
            Accent(minigamePanel.transform,Mint);
            minigameTitle=Label(minigamePanel.transform,"Minigame title",34,TextAnchor.MiddleCenter);minigameTitle.fontStyle=FontStyle.Normal;Place(minigameTitle.rectTransform,.05f,.47f,.95f,.93f);ShrinkToFit(minigameTitle,18);
            minigameDetail=Label(minigamePanel.transform,"Minigame detail",19,TextAnchor.MiddleCenter);Place(minigameDetail.rectTransform,.05f,.07f,.95f,.47f);ShrinkToFit(minigameDetail,13);
            minigamePanel.SetActive(false);
            // MINI-3: over the minigame card, the Light Show's questions take clicks, the pointer free (FestivalSession.PointerFree).
            lightShowPanel=Card(root.transform,"Light Show questions",new Vector2(.3f,.44f),new Vector2(.7f,.9f),true);
            Accent(lightShowPanel.transform,Mint);
            lightShowPrompt=Label(lightShowPanel.transform,"Light Show prompt",26,TextAnchor.MiddleCenter);lightShowPrompt.fontStyle=FontStyle.Normal;Place(lightShowPrompt.rectTransform,.05f,.81f,.95f,.96f);ShrinkToFit(lightShowPrompt,16);
            for(int k=0;k<lightShowAnswers.Length;k++)
            {
                int choice=k<4?k:-1;float top=.79f-k*.15f;
                var answer=Button(lightShowPanel.transform,"",new Vector2(.08f,top-.13f),new Vector2(.92f,top),()=>AnswerLightShow(choice));answer.name="Light Show answer "+(k+1);
                lightShowAnswers[k]=answer.GetComponent<Button>();lightShowLabels[k]=answer.transform.Find("Text").GetComponent<Text>();ShrinkToFit(lightShowLabels[k],14);
                Place(lightShowLabels[k].rectTransform,.1f,0,.98f,1);
                lightShowKeys[k]=Label(answer.transform,"Key",22,TextAnchor.MiddleCenter);lightShowKeys[k].color=Ink;Place(lightShowKeys[k].rectTransform,.015f,0,.09f,1);ShrinkToFit(lightShowKeys[k],12);
                // Explicit, so the arrows move only between the answers shown (UpdateLightShow links them).
                var nav=lightShowAnswers[k].navigation;nav.mode=Navigation.Mode.Explicit;lightShowAnswers[k].navigation=nav;
            }
            lightShowReveal=Label(lightShowPanel.transform,"Light Show reveal",19,TextAnchor.MiddleCenter);Place(lightShowReveal.rectTransform,.05f,.04f,.95f,.79f);ShrinkToFit(lightShowReveal,12);
            lightShowPanel.SetActive(false);
            // HUD-7: a first-time tip down the left rail, under the prompt and the nearby strip and clear of the price confirmation.
            tipPanel=Card(root.transform,"First-time tip card",new Vector2(.022f,.535f),new Vector2(.42f,.615f),true);
            Accent(tipPanel.transform,Mint);
            tipText=Label(tipPanel.transform,"First-time tip",18,TextAnchor.MiddleLeft);Place(tipText.rectTransform,.035f,.06f,.97f,.90f);ShrinkToFit(tipText,13);tipPanel.SetActive(false);
            // Clear of the death reels in the bottom-left (their first row tops out at .29) and under the sundown card (.71).
            lifePanel=Card(root.transform,"Life banner",new Vector2(.30f,.56f),new Vector2(.70f,.70f),true);lifePanel.GetComponent<Image>().raycastTarget=false;
            Accent(lifePanel.transform,Mint);
            lifeTitle=Label(lifePanel.transform,"Life title",30,TextAnchor.MiddleCenter);lifeTitle.fontStyle=FontStyle.Normal;Place(lifeTitle.rectTransform,.04f,.56f,.96f,.94f);
            lifeDetail=Label(lifePanel.transform,"Life detail",18,TextAnchor.MiddleCenter);lifeDetail.color=MutedPaper;Place(lifeDetail.rectTransform,.05f,.05f,.95f,.56f);ShrinkToFit(lifeDetail,13);
            lifePanel.SetActive(false);
            // Between the voice line and the equipment bar.
            spiritTag=Card(root.transform,"Spirit tag",new Vector2(.44f,.03f),new Vector2(.56f,.08f),true);spiritTag.GetComponent<Image>().raycastTarget=false;
            var spiritWord=Label(spiritTag.transform,"Spirit tag text",20,TextAnchor.MiddleCenter);spiritWord.text="SPIRIT";spiritWord.fontStyle=FontStyle.Normal;spiritWord.color=new Color(.62f,.8f,1,1);Fill(spiritWord.rectTransform,2);
            spiritTag.SetActive(false);
            // LOOP-5: the results banner, in the middle for the whole results. Over the aim dot, which has nothing to aim at then;
            // built before the map and the menus so they draw over it, and it catches no clicks.
            resultsPanel=Card(root.transform,"Results banner",new Vector2(.25f,.37f),new Vector2(.75f,.63f),true);resultsPanel.GetComponent<Image>().raycastTarget=false;
            Accent(resultsPanel.transform,Orange);resultsAccent=resultsPanel.transform.Find("Accent rule").GetComponent<Image>();
            resultsTitle=Label(resultsPanel.transform,"Results title",46,TextAnchor.MiddleCenter);resultsTitle.fontStyle=FontStyle.Normal;Place(resultsTitle.rectTransform,.05f,.55f,.95f,.93f);ShrinkToFit(resultsTitle,24);
            resultsDetail=Label(resultsPanel.transform,"Results detail",22,TextAnchor.MiddleCenter);Place(resultsDetail.rectTransform,.05f,.07f,.95f,.53f);ShrinkToFit(resultsDetail,14);
            resultsPanel.SetActive(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            diagnostics=Label(root.transform,"Development diagnostics",14,TextAnchor.UpperLeft);
            SetRect(diagnostics.rectTransform,new Vector2(0,.48f),new Vector2(.38f,.9f),new Vector2(18,0),Vector2.zero);
            diagnostics.gameObject.SetActive(false);
#endif

            BuildMap(root.transform);
            mapPanel.SetActive(false);mapShade.SetActive(false);

            rhythmShade=Panel(root.transform,"Rhythm dimmer",new Color(.006f,.019f,.023f,.76f),Vector2.zero,Vector2.one);rhythmShade.GetComponent<Image>().raycastTarget=false;rhythmShade.SetActive(false);
            dancePanel=Card(root.transform,"Live dancer",new Vector2(.505f,.05f),new Vector2(.985f,.95f),true);
            Accent(dancePanel.transform,Mint);
            dancerHeading=Label(dancePanel.transform,"Dancer heading",19,TextAnchor.MiddleLeft);dancerHeading.text="YOU  /  ON THE FLOOR";dancerHeading.fontStyle=FontStyle.Normal;dancerHeading.color=Mint;Place(dancerHeading.rectTransform,.07f,.915f,.93f,.985f);
            var dancerWindow=Panel(dancePanel.transform,"Dancer window",new Color(.025f,.073f,.083f,1),new Vector2(.04f,.20f),new Vector2(.96f,.90f));dancerWindow.GetComponent<Image>().raycastTarget=false;
            var dancerImageObject=new GameObject("Live character view",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));dancerImageObject.transform.SetParent(dancerWindow.transform,false);
            var dancerImage=dancerImageObject.GetComponent<RawImage>();dancerImage.color=Color.white;dancerImage.raycastTarget=false;Fill(dancerImage.rectTransform,0);
            var dancerAspect=dancerImageObject.AddComponent<AspectRatioFitter>();dancerAspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;dancerAspect.aspectRatio=768f/576f;
            dancerCaption=Label(dancePanel.transform,"Dancer caption",17,TextAnchor.MiddleCenter);dancerCaption.text="YOUR LOOK • YOUR MOVES";dancerCaption.color=MutedPaper;Place(dancerCaption.rectTransform,.055f,.025f,.945f,.11f);
            dancePreview=gameObject.AddComponent<FestivalDancePreview>();dancePreview.Initialize(dancerImage);
            dancePanel.SetActive(false);
            drumPanel=Card(root.transform,"Drum crew",new Vector2(.525f,.40f),new Vector2(.965f,.64f),true);drumPanel.GetComponent<Image>().raycastTarget=false;
            Accent(drumPanel.transform,Mint);
            drumTitle=Label(drumPanel.transform,"Drum crew title",30,TextAnchor.MiddleCenter);drumTitle.fontStyle=FontStyle.Normal;drumTitle.color=Orange;Place(drumTitle.rectTransform,.05f,.62f,.95f,.93f);ShrinkToFit(drumTitle,18);
            drumStrip=Label(drumPanel.transform,"Drum crew strip",20,TextAnchor.MiddleCenter);Place(drumStrip.rectTransform,.05f,.07f,.95f,.62f);ShrinkToFit(drumStrip,13);
            drumPanel.SetActive(false);

            rhythmPanel=Card(root.transform,"Rhythm lane",new Vector2(.015f,.05f),new Vector2(.495f,.95f),true);
            Accent(rhythmPanel.transform,Orange);
            rhythmDialogue=Label(rhythmPanel.transform,"Challenge title",27,TextAnchor.MiddleLeft);rhythmDialogue.fontStyle=FontStyle.Normal;Place(rhythmDialogue.rectTransform,.085f,.91f,.65f,.975f);
            rhythmSundown=Label(rhythmPanel.transform,"Sundown line",27,TextAnchor.MiddleLeft);rhythmSundown.fontStyle=FontStyle.Normal;rhythmSundown.color=Orange;Place(rhythmSundown.rectTransform,.085f,.91f,.65f,.975f);ShrinkToFit(rhythmSundown,14);rhythmSundown.gameObject.SetActive(false);
            rhythmStatus=Label(rhythmPanel.transform,"Tempo and steps",19,TextAnchor.MiddleRight);rhythmStatus.color=Mint;Place(rhythmStatus.rectTransform,.60f,.91f,.92f,.975f);
            for(int lane=0;lane<4;lane++)
            {
                float x=.08f+lane*.21f;
                var track=Panel(rhythmPanel.transform,"Note column "+lane,new Color(.13f,.22f,.26f,lane%2==0?.90f:.73f),new Vector2(x,.104f),new Vector2(x+.21f,.90f));track.GetComponent<Image>().raycastTarget=false;
                var laneEdge=Panel(rhythmPanel.transform,"Column divider "+lane,new Color(1,1,1,.12f),new Vector2(x,.104f),new Vector2(x+.003f,.90f));laneEdge.GetComponent<Image>().raycastTarget=false;
            }
            for(int beat=0;beat<5;beat++)
            {
                float y=.19f+beat*.126f;
                var grid=Panel(rhythmPanel.transform,"Beat grid "+beat,new Color(.82f,.95f,.88f,.09f),new Vector2(.08f,y),new Vector2(.92f,y+.002f));grid.GetComponent<Image>().raycastTarget=false;
            }
            for(int lane=0;lane<4;lane++)
            {
                float x=.08f+lane*.21f;
                var well=Panel(rhythmPanel.transform,"Receptor well "+lane,new Color(.22f,.32f,.35f,1),new Vector2(x+.007f,WellBottom),new Vector2(x+.203f,WellTop));Round(well);rhythmReceptorWells[lane]=well.GetComponent<Image>();rhythmReceptorWells[lane].raycastTarget=false;
                var arrow=Panel(well.transform,"Target arrow",new Color(.76f,.85f,.84f,.9f),new Vector2(.12f,.09f),new Vector2(.88f,.91f)).GetComponent<Image>();
                arrow.sprite=arrowOutlineSprite;arrow.preserveAspect=true;arrow.raycastTarget=false;arrow.transform.localRotation=Quaternion.Euler(0,0,RhythmAngles[lane]);rhythmReceptors[lane]=arrow;
                // The arrow's sibling, not its child, so the key reads upright while the arrow turns.
                var key=Label(well.transform,"Key",30,TextAnchor.MiddleCenter);key.fontStyle=FontStyle.Bold;Place(key.rectTransform,.12f,.09f,.88f,.91f);ShrinkToFit(key,14);
                key.gameObject.AddComponent<Outline>().effectColor=new Color(0,.015f,.02f,.95f);rhythmKeys[lane]=key;
            }
            // Notes come after the wells, so they draw over the wells and their outlines, and before the judgment, combo and timing text.
            var tails=new GameObject("Hold tails",typeof(RectTransform));tails.transform.SetParent(rhythmPanel.transform,false);Fill(Rect(tails),0);tailContainer=tails.transform;
            var notes=new GameObject("Notes",typeof(RectTransform));notes.transform.SetParent(rhythmPanel.transform,false);Fill(Rect(notes),0);noteContainer=notes.transform;
            for(int i=0;i<32;i++)AddNoteView(); // ponytail: a typical chart's worth up front; ShowChart grows the pool for longer ones
            rhythmJudgment=Label(rhythmPanel.transform,"Judgment",45,TextAnchor.MiddleCenter);rhythmJudgment.font=displayFont;rhythmJudgment.fontStyle=FontStyle.Normal;Place(rhythmJudgment.rectTransform,.06f,.46f,.94f,.57f);
            rhythmJudgment.gameObject.AddComponent<Outline>().effectColor=new Color(0,.015f,.02f,.95f);
            rhythmCombo=Label(rhythmPanel.transform,"Combo",56,TextAnchor.MiddleCenter);rhythmCombo.font=displayFont;rhythmCombo.fontStyle=FontStyle.Normal;rhythmCombo.color=Paper;Place(rhythmCombo.rectTransform,.06f,.36f,.94f,.47f);
            rhythmCombo.gameObject.AddComponent<Outline>().effectColor=new Color(0,.015f,.02f,.95f);
            rhythmTiming=Label(rhythmPanel.transform,"Timing",20,TextAnchor.MiddleCenter);rhythmTiming.color=MutedPaper;Place(rhythmTiming.rectTransform,.06f,.315f,.94f,.37f);
            var progressTrack=Panel(rhythmPanel.transform,"Step progress track",new Color(.33f,.43f,.44f,1),new Vector2(.08f,.055f),new Vector2(.92f,.071f));progressTrack.GetComponent<Image>().raycastTarget=false;
            rhythmProgressFill=Panel(rhythmPanel.transform,"Step progress",Mint,new Vector2(.08f,.055f),new Vector2(.08f,.071f)).GetComponent<Image>();rhythmProgressFill.raycastTarget=false;
            rhythmControls=Label(rhythmPanel.transform,"Controls",17,TextAnchor.MiddleCenter);rhythmControls.text="ARROWS OR WASD";rhythmControls.color=MutedPaper;Place(rhythmControls.rectTransform,.06f,.005f,StopHoldLeft-.02f,.049f);
            // DANCE-12: only a held G stops a challenge; the fill under the words grows while it's held (ShowStopHold).
            stopHoldFill=Panel(rhythmPanel.transform,"Stop hold fill",new Color(Orange.r,Orange.g,Orange.b,.5f),new Vector2(StopHoldLeft,.008f),new Vector2(StopHoldLeft,.046f)).GetComponent<Image>();stopHoldFill.raycastTarget=false;stopHoldFill.gameObject.SetActive(false);
            stopHoldLabel=Label(rhythmPanel.transform,"Stop hold",17,TextAnchor.MiddleCenter);stopHoldLabel.text=FestivalHudText.HoldToStop;Place(stopHoldLabel.rectTransform,StopHoldLeft,.005f,StopHoldRight,.049f);
            rhythmPanel.SetActive(false);
            // No dimmer under the highway, so the crowd stays in sight.
            ridePanel=new GameObject("DJ ride",typeof(RectTransform));ridePanel.transform.SetParent(root.transform,false);Place(Rect(ridePanel),.2f,.02f,.8f,.46f);
            ride=new GameObject("Highway",typeof(RectTransform),typeof(CanvasRenderer),typeof(FestivalDjRide)).GetComponent<FestivalDjRide>();ride.transform.SetParent(ridePanel.transform,false);Fill(Rect(ride.gameObject),0);ride.raycastTarget=false;
            var rideTitle=Label(ridePanel.transform,"Ride title",27,TextAnchor.MiddleCenter);rideTitle.text=FestivalHudText.RideTitle;rideTitle.color=Orange;Place(rideTitle.rectTransform,.1f,.9f,.9f,1);
            rideTitle.gameObject.AddComponent<Outline>().effectColor=new Color(0,.015f,.02f,.95f);
            rideCombo=Label(ridePanel.transform,"Combo",48,TextAnchor.MiddleCenter);rideCombo.font=displayFont;rideCombo.fontStyle=FontStyle.Normal;Place(rideCombo.rectTransform,.25f,.5f,.75f,.7f);
            rideCombo.gameObject.AddComponent<Outline>().effectColor=new Color(0,.015f,.02f,.95f);
            rideControls=Label(ridePanel.transform,"Controls",17,TextAnchor.MiddleCenter);rideControls.color=MutedPaper;Place(rideControls.rectTransform,.05f,-.08f,.95f,.0f);
            ridePanel.SetActive(false);
            dialoguePanel=Card(root.transform,"Interaction dialogue",new Vector2(.70f,.52f),new Vector2(.96f,.69f),false);
            Accent(dialoguePanel.transform,Orange);
            dialogueSpeaker=Label(dialoguePanel.transform,"Speaker",17,TextAnchor.MiddleLeft);dialogueSpeaker.fontStyle=FontStyle.Normal;dialogueSpeaker.color=new Color(.17f,.47f,.40f);Place(dialogueSpeaker.rectTransform,.045f,.56f,.95f,.90f);
            dialogueLine=Label(dialoguePanel.transform,"Dialogue line",24,TextAnchor.MiddleLeft);dialogueLine.color=Ink;Place(dialogueLine.rectTransform,.045f,.09f,.95f,.62f);
            dialogueLine.supportRichText=false;dialogueReveal=dialogueLine.gameObject.AddComponent<FestivalSpeechReveal>();
            dialoguePanel.SetActive(false);
            BuildTitle(root.transform);
            BuildPass(root.transform);

            actionsPanel=Card(root.transform,"Actions",new Vector2(.73f,.20f),new Vector2(.975f,.78f),true);
            Accent(actionsPanel.transform,Mint);
            var actionsHeading=Label(actionsPanel.transform,"Actions heading",24,TextAnchor.MiddleLeft);actionsHeading.text="NEARBY / CREW";actionsHeading.fontStyle=FontStyle.Normal;Place(actionsHeading.rectTransform,.07f,.88f,.93f,.97f);
            var viewport=Panel(actionsPanel.transform,"Action scroll viewport",Color.clear,new Vector2(.045f,.035f),new Vector2(.955f,.87f));viewport.GetComponent<Image>().raycastTarget=true;
            viewport.AddComponent<RectMask2D>();
            var content=Panel(viewport.transform,"Action list",Color.clear,new Vector2(0,1),new Vector2(1,1));
            actionsContent=Rect(content);actionsContent.pivot=new Vector2(.5f,1);actionsContent.anchoredPosition=Vector2.zero;actionsContent.sizeDelta=new Vector2(0,0);
            var layout=content.AddComponent<VerticalLayoutGroup>();layout.spacing=10;layout.padding=new RectOffset(4,4,4,4);layout.childAlignment=TextAnchor.UpperCenter;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            var fitter=content.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            actionsScroll=actionsPanel.AddComponent<ScrollRect>();actionsScroll.viewport=Rect(viewport);actionsScroll.content=actionsContent;actionsScroll.horizontal=false;actionsScroll.vertical=true;actionsScroll.scrollSensitivity=26;actionsScroll.movementType=ScrollRect.MovementType.Clamped;
            checkoutPanel=Card(root.transform,"Counter price confirmation",new Vector2(.022f,.625f),new Vector2(.405f,.705f),false);
            Accent(checkoutPanel.transform,Orange);
            checkoutText=Label(checkoutPanel.transform,"Handoff price",22,TextAnchor.MiddleCenter);checkoutText.color=Ink;Fill(checkoutText.rectTransform,12);
            // Docked under the crew card, clear of the session notice, so it stays above
            // the shelves and never covers price tags near the reticle.
            heldDetailPanel=Card(root.transform,"Held item label",new Vector2(.633f,.66f),new Vector2(.978f,.78f),false);
            Accent(heldDetailPanel.transform,Orange);
            // The price shares the title's row with room for Ember Playa's "15 FRIENDSHIP BRACELETS" (PLAYA-1).
            heldTitle=Label(heldDetailPanel.transform,"Held item title",24,TextAnchor.MiddleLeft);heldTitle.fontStyle=FontStyle.Normal;heldTitle.color=Ink;Place(heldTitle.rectTransform,.06f,.59f,.52f,.91f);
            heldPrice=Label(heldDetailPanel.transform,"Held item price",24,TextAnchor.MiddleRight);heldPrice.fontStyle=FontStyle.Normal;heldPrice.color=new Color(.12f,.43f,.37f);Place(heldPrice.rectTransform,.52f,.59f,.94f,.91f);
            Rule(heldDetailPanel.transform,.55f);
            heldDetailText=Label(heldDetailPanel.transform,"Held item details",22,TextAnchor.MiddleLeft);heldDetailText.color=new Color(.15f,.25f,.22f);Place(heldDetailText.rectTransform,.06f,.11f,.94f,.54f);
            // A stock item's description runs to three lines, so it shrinks to fit its two.
            ShrinkToFit(heldDetailText,14);
            // DIALOG-5: the chat window, built last so that with the menu open it draws over the pass's dimmer (UpdateChat).
            chatWindow=FestivalChatWindow.Build(root.transform,font,roundedSprite);
            // DIALOG-3: a reply picked in a talk is the host's Reply command.
            chatWindow.Send=c=>session.Command(c.Kind,c.TargetId,c.ItemId,c.Amount,c.Direction,c.TimeSeconds);
            chatWindow.CheckReply=(check,choice)=>session.SpeechBubbles?.SelectCheckReply(session.State,session.LocalPlayer,check,choice,session.ConnectionGeneration,session.EstimatedWorldSeconds);
            BuildHowToPlay(root.transform);
            BuildComfortChoice(root.transform);
        }

        // MENU-1: the title screen (FestivalTitleScreen) over the campsite, where the connection card was: the name and the tagline
        // over a shade darkest at the left edge, PLAY, HOW TO PLAY, SETTINGS and QUIT under them, and PLAY's card in their place.
        private void BuildTitle(Transform root)
        {
            titlePanel=Panel(root,"Title screen",Color.clear,Vector2.zero,Vector2.one);titlePanel.GetComponent<Image>().raycastTarget=false;
            var shade=Panel(titlePanel.transform,"Title shade",new Color(.01f,.025f,.03f,.82f),Vector2.zero,new Vector2(.68f,1)).GetComponent<Image>();shade.raycastTarget=false;
            var wordmark=Label(titlePanel.transform,"Game name",150,TextAnchor.LowerLeft);wordmark.font=displayFont;wordmark.text=FestivalTitleScreen.GameName;Place(wordmark.rectTransform,.05f,.70f,.62f,.925f);ShrinkToFit(wordmark,90);
            var lift=wordmark.gameObject.AddComponent<Shadow>();lift.effectColor=new Color(0,.015f,.02f,.6f);lift.effectDistance=new Vector2(0,-6);
            Panel(titlePanel.transform,"Tagline accent",Orange,new Vector2(.057f,.688f),new Vector2(.12f,.695f)).GetComponent<Image>().raycastTarget=false;
            var tagline=Label(titlePanel.transform,"Tagline",30,TextAnchor.MiddleLeft);tagline.text=FestivalTitleScreen.Tagline;Place(tagline.rectTransform,.057f,.615f,.62f,.675f);ShrinkToFit(tagline,20);
            var home=Panel(titlePanel.transform,"Title menu",Color.clear,new Vector2(.055f,.215f),new Vector2(.30f,.56f));home.GetComponent<Image>().raycastTarget=false;
            Button(home.transform,"PLAY",new Vector2(0,.783f),Vector2.one,()=>titleScreen.Open(FestivalTitleScreen.Page.Play));
            Button(home.transform,"HOW TO PLAY",new Vector2(0,.522f),new Vector2(1,.739f),OpenHowToPlay);
            Button(home.transform,"SETTINGS",new Vector2(0,.261f),new Vector2(1,.478f),()=>settingsOpen=true);
            Button(home.transform,"QUIT",Vector2.zero,new Vector2(1,.217f),FestivalTitleScreen.Quit);
            var play=Card(titlePanel.transform,"Play",new Vector2(.055f,.05f),new Vector2(.37f,.595f),false);
            Accent(play.transform,Orange);
            var heading=Label(play.transform,"Play heading",36,TextAnchor.MiddleLeft);heading.font=displayFont;heading.text="PLAY";heading.color=Ink;Place(heading.rectTransform,.08f,.865f,.92f,.955f);
            nameField=Field(play.transform,"YOUR NAME","Friend",.80f);
            var choices=Panel(play.transform,"Play choices",Color.clear,Vector2.zero,Vector2.one);choices.GetComponent<Image>().raycastTarget=false;
            Button(choices.transform,"HOST A GAME",new Vector2(.08f,.53f),new Vector2(.92f,.645f),StartHost);
            Button(choices.transform,"JOIN A FRIEND",new Vector2(.08f,.395f),new Vector2(.92f,.51f),()=>titleScreen.Open(FestivalTitleScreen.Page.Join));
            Button(choices.transform,"ADVANCED",new Vector2(.60f,.285f),new Vector2(.92f,.37f),()=>titleScreen.ToggleAdvanced());
            var advanced=Panel(choices.transform,"Advanced",Color.clear,Vector2.zero,Vector2.one);advanced.GetComponent<Image>().raycastTarget=false;
            hostPortField=Field(advanced.transform,"PORT","7777",.27f);
            var join=Panel(play.transform,"Join a friend",Color.clear,Vector2.zero,Vector2.one);join.GetComponent<Image>().raycastTarget=false;
            addressField=Field(join.transform,"HOST ADDRESS","127.0.0.1",.64f);
            portField=Field(join.transform,"PORT","7777",.48f);
            Button(join.transform,"JOIN",new Vector2(.08f,.205f),new Vector2(.92f,.32f),StartJoin);
            Button(play.transform,"BACK",new Vector2(.08f,.035f),new Vector2(.36f,.13f),()=>titleScreen.Back());
            status=Label(play.transform,"Status",16,TextAnchor.MiddleLeft);status.color=new Color(.25f,.35f,.32f);Place(status.rectTransform,.39f,.02f,.93f,.145f);ShrinkToFit(status,12);
            hostPortField.onValueChanged.AddListener(_=>connectionError="");portField.onValueChanged.AddListener(_=>connectionError="");
            titleScreen=titlePanel.AddComponent<FestivalTitleScreen>();titleScreen.Configure(shade,home,play,choices,advanced,join);
        }

        private void BuildPass(Transform root)
        {
            menuShade=Panel(root,"Festival pass dimmer",new Color(.01f,.025f,.03f,.72f),Vector2.zero,Vector2.one);
            menuPanel=Card(root,"Festival pass",new Vector2(.275f,.095f),new Vector2(.725f,.905f),false);
            Accent(menuPanel.transform,Orange);
            Disc(menuPanel.transform,"Sun mark",new Vector2(.065f,.86f),new Vector2(.128f,.93f),Orange);
            Disc(menuPanel.transform,"Moon mark",new Vector2(.87f,.86f),new Vector2(.933f,.93f),new Color(.12f,.45f,.39f));
            Disc(menuPanel.transform,"Moon cutout",new Vector2(.887f,.876f),new Vector2(.950f,.946f),Paper);
            var menuTitle=Label(menuPanel.transform,"Menu title",58,TextAnchor.MiddleCenter);menuTitle.font=displayFont;menuTitle.text="AFTER HOURS";menuTitle.color=Ink;Place(menuTitle.rectTransform,.14f,.845f,.86f,.95f);
            var serial=Label(menuPanel.transform,"Pass serial",17,TextAnchor.MiddleCenter);serial.text="FESTIVAL PASS   /   CAMP CREW";serial.fontStyle=FontStyle.Normal;serial.color=new Color(.32f,.40f,.35f);Place(serial.rectTransform,.08f,.79f,.92f,.85f);
            Rule(menuPanel.transform,.782f);
            menuHome=Panel(menuPanel.transform,"Pass home",Color.clear,new Vector2(.055f,.025f),new Vector2(.945f,.76f));menuHome.GetComponent<Image>().raycastTarget=false;
            var crewHeading=Label(menuHome.transform,"Crew heading",17,TextAnchor.MiddleLeft);crewHeading.text="YOUR CREW";crewHeading.fontStyle=FontStyle.Normal;crewHeading.color=new Color(.31f,.42f,.38f);Place(crewHeading.rectTransform,.045f,.90f,.56f,.98f);
            menuRoster=Label(menuHome.transform,"Crew status",23,TextAnchor.UpperLeft);menuRoster.fontStyle=FontStyle.Normal;menuRoster.color=Ink;Place(menuRoster.rectTransform,.045f,.72f,.55f,.89f);menuRoster.raycastTarget=false;ShrinkToFit(menuRoster,11);
            var phaseCard=Card(menuHome.transform,"Current stop",new Vector2(.59f,.735f),new Vector2(.955f,.98f),true);
            var phaseLabel=Label(phaseCard.transform,"Stop label",16,TextAnchor.MiddleLeft);phaseLabel.text="CURRENT STOP";phaseLabel.fontStyle=FontStyle.Normal;phaseLabel.color=Mint;Place(phaseLabel.rectTransform,.09f,.62f,.92f,.92f);
            menuPhase=Label(phaseCard.transform,"Stop value",24,TextAnchor.MiddleLeft);menuPhase.fontStyle=FontStyle.Normal;Place(menuPhase.rectTransform,.09f,.12f,.92f,.64f);
            Rule(menuHome.transform,.70f);
            var pocketHeading=Label(menuHome.transform,"Pockets heading",17,TextAnchor.MiddleLeft);pocketHeading.text="CURRENT LOADOUT";pocketHeading.fontStyle=FontStyle.Normal;pocketHeading.color=new Color(.31f,.42f,.38f);Place(pocketHeading.rectTransform,.045f,.61f,.45f,.69f);
            // CAMP-3: the camp secrets this machine has found (LocalProfile), and the line for finding all eight.
            campSecrets=Label(menuHome.transform,"Easter eggs",17,TextAnchor.MiddleRight);campSecrets.fontStyle=FontStyle.Normal;campSecrets.color=new Color(.31f,.42f,.38f);Place(campSecrets.rectTransform,.45f,.61f,.955f,.69f);ShrinkToFit(campSecrets,10);
            preview=Label(menuHome.transform,"Preview",21,TextAnchor.MiddleLeft);Place(preview.rectTransform,.045f,.51f,.95f,.62f);preview.color=Ink;
            nextButton=Button(menuHome.transform,"NEXT CAMP",new Vector2(.045f,.385f),new Vector2(.955f,.50f),()=>session.Command("Reset"));
            cancelButton=Button(menuHome.transform,"CANCEL ACTION",new Vector2(.045f,.385f),new Vector2(.955f,.50f),()=>FinishCurrentAction(session?.LocalPlayer?.InteractionId));
            resumeButton=Button(menuHome.transform,"RESUME FESTIVAL",new Vector2(.045f,.385f),new Vector2(.955f,.50f),()=>{session.MenuOpen=false;settingsOpen=false;});
            crewButton=(RectTransform)Button(menuHome.transform,"CREW + NEARBY",new Vector2(.045f,.26f),new Vector2(.955f,.375f),()=>{crewOpen=!crewOpen;}).transform;
            testModeButton=Button(menuHome.transform,"TEST MODE: OFF",new Vector2(.505f,.26f),new Vector2(.725f,.375f),()=>session.Command(FestivalSimulation.TestModeKind));
            clearLevelButton=Button(menuHome.transform,FestivalHudText.ClearLevelButton,new Vector2(.735f,.26f),new Vector2(.955f,.375f),()=>session.Command(FestivalSimulation.TestClearLevelKind));
            foreach(var button in new[]{testModeButton,clearLevelButton}){ShrinkToFit(button.GetComponentInChildren<Text>(),12);button.SetActive(false);}
            Button(menuHome.transform,"SETTINGS",new Vector2(.045f,.135f),new Vector2(.495f,.25f),()=>{settingsOpen=true;});
            Button(menuHome.transform,"HOW TO PLAY",new Vector2(.505f,.135f),new Vector2(.955f,.25f),OpenHowToPlay);
            Button(menuHome.transform,"LEAVE SESSION",new Vector2(.045f,.01f),new Vector2(.955f,.125f),()=>{session.Leave();titleScreen.Open(FestivalTitleScreen.Page.Home);});
            settingsPanel=Panel(menuPanel.transform,"Pass settings",Color.clear,new Vector2(.055f,.025f),new Vector2(.945f,.76f));settingsPanel.GetComponent<Image>().raycastTarget=false;
            var settingsHeading=Label(settingsPanel.transform,"Settings heading",32,TextAnchor.MiddleLeft);settingsHeading.text="LOCAL SETTINGS";settingsHeading.fontStyle=FontStyle.Normal;settingsHeading.color=Ink;Place(settingsHeading.rectTransform,.04f,.89f,.96f,.98f);
            // SET-1: five tabs, the save message (or why a change could not be saved), the open tab's rows, what the platform is
            // actually doing, then RESET TAB for the open tab, SHOW TIPS AGAIN and the way out.
            for(int i=0;i<FestivalSettingsScreen.Tabs.Length;i++)
            {
                var tab=FestivalSettingsScreen.Tabs[i];
                var button=Button(settingsPanel.transform,FestivalSettingsScreen.TabName(tab),new Vector2(.04f+i*.186f,.815f),new Vector2(.216f+i*.186f,.885f),()=>settingsScreen.Open(tab));
                var label=button.GetComponentInChildren<Text>();ShrinkToFit(label,12);
                settingsTabs.Add((tab,button.GetComponent<Image>(),label));
            }
            settingsText=Label(settingsPanel.transform,"Settings values",18,TextAnchor.MiddleLeft);settingsText.text=FestivalSettingsScreen.SavedMessage;settingsText.color=new Color(.32f,.40f,.36f);Place(settingsText.rectTransform,.04f,.75f,.96f,.805f);ShrinkToFit(settingsText,12);
            // CONTROLS holds every key in sections; their buttons take its first row.
            for(int i=0;i<FestivalSettingsScreen.Sections.Length;i++)
            {
                // The sections share the tabs' width, however many there are.
                string section=FestivalSettingsScreen.Sections[i];float stride=.93f/FestivalSettingsScreen.Sections.Length,x=.04f+i*stride;
                var button=Button(settingsPanel.transform,section,new Vector2(x,.742f-SettingRowHeight),new Vector2(x+stride-.01f,.738f),()=>settingsScreen.OpenSection(section));
                var label=button.GetComponentInChildren<Text>();ShrinkToFit(label,12);
                settingsSections.Add((section,button.GetComponent<Image>(),label,button));
            }
            var slots=new Dictionary<(FestivalSettingsScreen.Tab,string),int>();
            foreach(var row in FestivalSettingsScreen.Rows)
            {
                if(!slots.TryGetValue((row.Tab,row.Section),out int slot))slot=row.Section==""?0:1;
                slots[(row.Tab,row.Section)]=slot+1;
                settingsRows.Add(SettingRow(settingsPanel.transform,row,.74f-(slot+1)*SettingRowHeight));
            }
            settingsStatus=Label(settingsPanel.transform,"Settings status",16,TextAnchor.MiddleLeft);settingsStatus.color=new Color(.32f,.40f,.36f);Place(settingsStatus.rectTransform,.04f,.105f,.96f,.16f);ShrinkToFit(settingsStatus,11);
            Button(settingsPanel.transform,"RESET TAB",new Vector2(.04f,.015f),new Vector2(.32f,.095f),()=>settingsScreen.ResetTab());
            ShrinkToFit(Button(settingsPanel.transform,"SHOW TIPS AGAIN",new Vector2(.34f,.015f),new Vector2(.64f,.095f),()=>settingsScreen.ShowTipsAgain()).GetComponentInChildren<Text>(),14);
            settingsBack=Button(settingsPanel.transform,"BACK TO PASS",new Vector2(.66f,.015f),new Vector2(.96f,.095f),()=>settingsOpen=false).GetComponentInChildren<Text>();
        }

        // HUD-7: How to Play, built last so it covers the main menu, the pass and whatever is open beside them; its dimmer takes the
        // clicks meant for what is under it. FestivalHudText.HowToPlay's six blocks sit in three columns: the weekend, the tripper and
        // everyone else; danger and camp; the keys. Each block is x min, y min, x max, y max on the card, its title along the top.
        private static readonly Vector4[] HowToPlayBlocks=
        {
            new Vector4(.035f,.66f,.345f,.86f),new Vector4(.035f,.37f,.345f,.65f),new Vector4(.035f,.14f,.345f,.36f),
            new Vector4(.365f,.44f,.675f,.86f),new Vector4(.365f,.14f,.675f,.43f),new Vector4(.695f,.14f,.965f,.86f),
        };
        private void BuildHowToPlay(Transform root)
        {
            howToPlayPanel=Panel(root,"How to play",new Color(.01f,.025f,.03f,.72f),Vector2.zero,Vector2.one);
            var card=Card(howToPlayPanel.transform,"How to play card",new Vector2(.07f,.06f),new Vector2(.93f,.94f),false);
            Accent(card.transform,Orange);
            var title=Label(card.transform,"How to play title",44,TextAnchor.MiddleLeft);title.font=displayFont;title.text="HOW TO PLAY";title.color=Ink;Place(title.rectTransform,.035f,.885f,.70f,.97f);
            Rule(card.transform,.875f);
            for(int i=0;i<HowToPlayBlocks.Length;i++)
            {
                var block=HowToPlayBlocks[i];
                howToPlayTitles[i]=Label(card.transform,"How to play heading "+(i+1),19,TextAnchor.LowerLeft);howToPlayTitles[i].fontStyle=FontStyle.Normal;howToPlayTitles[i].color=new Color(.12f,.43f,.37f);Place(howToPlayTitles[i].rectTransform,block.x,block.w-.04f,block.z,block.w);
                howToPlayTexts[i]=Label(card.transform,"How to play text "+(i+1),17,TextAnchor.UpperLeft);howToPlayTexts[i].color=Ink;Place(howToPlayTexts[i].rectTransform,block.x,block.y,block.z,block.w-.045f);ShrinkToFit(howToPlayTexts[i],12);
            }
            Button(card.transform,"BACK",new Vector2(.695f,.035f),new Vector2(.965f,.115f),()=>howToPlayOpen=false);
            howToPlayPanel.SetActive(false);
        }
        // HUD-7: How to Play reads the keys as bound when it opens, so a rebind shows.
        private void OpenHowToPlay()
        {
            var blocks=FestivalHudText.HowToPlay(session.Controls,session.State,session.LocalPlayer);
            for(int i=0;i<howToPlayTexts.Length;i++){howToPlayTitles[i].text=i<blocks.Length?blocks[i].Title:"";howToPlayTexts[i].text=i<blocks.Length?blocks[i].Text:"";}
            howToPlayOpen=true;
        }

        private void BuildComfortChoice(Transform root)
        {
            comfortPanel=Panel(root,"First-launch comfort choice",new Color(.01f,.025f,.03f,.94f),Vector2.zero,Vector2.one);
            var card=Card(comfortPanel.transform,"Comfort choice card",new Vector2(.2f,.18f),new Vector2(.8f,.82f),false);Accent(card.transform,Orange);
            var heading=Label(card.transform,"Comfort heading",38,TextAnchor.MiddleCenter);heading.font=displayFont;heading.color=Ink;heading.text="BEFORE YOU PLAY";Place(heading.rectTransform,.06f,.79f,.94f,.94f);ShrinkToFit(heading,24);
            var detail=Label(card.transform,"Comfort explanation",27,TextAnchor.MiddleCenter);detail.color=Ink;
            detail.text="This game has strong visual trip effects. Reduce motion?\n\nKeep the colours and clues with calmer effects. You can change this in Settings → Accessibility.";
            Place(detail.rectTransform,.07f,.43f,.93f,.79f);ShrinkToFit(detail,18);
            var yes=Button(card.transform,"YES, REDUCE MOTION",new Vector2(.07f,.29f),new Vector2(.93f,.41f),()=>ChooseComfort(true));
            var no=Button(card.transform,"NO, KEEP FULL EFFECTS",new Vector2(.07f,.14f),new Vector2(.93f,.26f),()=>ChooseComfort(false));
            ShrinkToFit(yes.GetComponentInChildren<Text>(),18);ShrinkToFit(no.GetComponentInChildren<Text>(),18);
            comfortError=Label(card.transform,"Comfort save error",20,TextAnchor.MiddleCenter);comfortError.color=new Color(.58f,.16f,.12f);Place(comfortError.rectTransform,.06f,.015f,.94f,.13f);ShrinkToFit(comfortError,14);
            comfortPanel.SetActive(false);
        }
        private void WaitForComfort(Action start)
        {
            if(session.Profile.Data.ComfortPromptComplete){start();return;}
            pendingComfortStart=start;comfortError.text="";comfortPanel.SetActive(true);
        }
        private void ChooseComfort(bool reduceMotion)
        {
            string error=session.Settings.CompleteComfortChoice(reduceMotion);
            comfortError.text=error;if(error!="")return;
            comfortPanel.SetActive(false);var start=pendingComfortStart;pendingComfortStart=null;start?.Invoke();
        }

        private void BuildMap(Transform root)
        {
            mapShade=Panel(root,"Map dimmer",new Color(.01f,.025f,.03f,.65f),Vector2.zero,Vector2.one);mapShade.GetComponent<Image>().raycastTarget=false;
            mapPanel=Card(root,"Map",new Vector2(.11f,.10f),new Vector2(.89f,.90f),false);
            Accent(mapPanel.transform,Mint);
            mapTitle=Label(mapPanel.transform,"Map title",38,TextAnchor.MiddleLeft);mapTitle.fontStyle=FontStyle.Normal;mapTitle.color=Ink;Place(mapTitle.rectTransform,.04f,.89f,.96f,.97f);
            mapHint=Label(mapPanel.transform,"Map hint",17,TextAnchor.MiddleRight);mapHint.text="HOLD TAB TO VIEW";mapHint.color=new Color(.34f,.43f,.39f);Place(mapHint.rectTransform,.72f,.91f,.96f,.97f);
            Rule(mapPanel.transform,.88f);
            var diagram=Card(mapPanel.transform,"Ground plan",new Vector2(.035f,.07f),new Vector2(.67f,.85f),true);
            for(int i=1;i<5;i++)
            {
                var v=Panel(diagram.transform,"Grid vertical "+i,new Color(1,1,1,.07f),new Vector2(i/5f,0),new Vector2(i/5f+.002f,1));v.GetComponent<Image>().raycastTarget=false;
                var h=Panel(diagram.transform,"Grid horizontal "+i,new Color(1,1,1,.07f),new Vector2(0,i/5f),new Vector2(1,i/5f+.002f));h.GetComponent<Image>().raycastTarget=false;
            }
            festivalMap=Panel(diagram.transform,"Festival landmarks",Color.clear,Vector2.zero,Vector2.one);festivalMap.GetComponent<Image>().raycastTarget=false;
            MapPath(festivalMap.transform,.50f,.12f,.27f,.25f);
            MapPath(festivalMap.transform,.27f,.25f,.28f,.62f);
            MapPath(festivalMap.transform,.28f,.62f,.50f,.86f);
            MapPath(festivalMap.transform,.50f,.86f,.72f,.50f);
            MapPath(festivalMap.transform,.72f,.50f,.83f,.28f);
            MapPath(festivalMap.transform,.72f,.50f,.87f,.61f);
            MapPin(festivalMap.transform,"STAGE",.50f,.86f,Orange);
            MapPin(festivalMap.transform,"SUN",.72f,.50f,Orange);
            MapPin(festivalMap.transform,"MOON",.28f,.62f,Mint);
            MapPin(festivalMap.transform,"MARKET",.27f,.25f,Paper);
            MapPin(festivalMap.transform,"MEDIC",.83f,.28f,Paper);
            MapPin(festivalMap.transform,"HOLDING",.87f,.61f,Paper);
            MapPin(festivalMap.transform,"BACK TO CAMP",.50f,.12f,Orange);
            campMap=Panel(diagram.transform,"Camp landmarks",Color.clear,Vector2.zero,Vector2.one);campMap.GetComponent<Image>().raycastTarget=false;
            MapPath(campMap.transform,.29f,.20f,.50f,.46f);
            MapPath(campMap.transform,.76f,.30f,.50f,.46f);
            MapPath(campMap.transform,.50f,.46f,.50f,.65f);
            MapPath(campMap.transform,.50f,.65f,.50f,.84f);
            MapPin(campMap.transform,"TRAILHEAD",.50f,.84f,Orange);
            MapPin(campMap.transform,"GEAR",.50f,.65f,Mint);
            MapPin(campMap.transform,"SHADE",.50f,.46f,Paper);
            MapPin(campMap.transform,"CARS",.29f,.20f,Paper);
            MapPin(campMap.transform,"TENTS",.76f,.30f,Paper);
            MapPin(campMap.transform,"TOILET",.82f,.52f,Paper);
            mapPlayerMarker=Rect(Panel(diagram.transform,"You marker",Orange,new Vector2(.48f,.46f),new Vector2(.52f,.52f)));mapPlayerMarker.GetComponent<Image>().sprite=discSprite;
            mapText=Label(mapPanel.transform,"Map text",21,TextAnchor.UpperLeft);mapText.color=Ink;Place(mapText.rectTransform,.70f,.08f,.96f,.84f);
        }

        // Nine rows fit between the save message and the status line: CONTROLS' section buttons over its eight dance keys.
        private const float SettingRowHeight=.58f/9;
        private (FestivalSettingsScreen.Row,GameObject[],Text,Button[]) SettingRow(Transform parent,FestivalSettingsScreen.Row row,float y)
        {
            float top=y+SettingRowHeight,inset=.002f;
            bool note=row.Buttons.Length==0;
            var rule=Rule(parent,y);
            var label=Label(parent,row.Label,20,TextAnchor.MiddleLeft);label.text=row.Label;label.color=Ink;label.fontStyle=FontStyle.Normal;Place(label.rectTransform,.04f,y,note?.28f:.44f,top);ShrinkToFit(label,12);
            var value=Label(parent,row.Label+" value",note?17:21,note?TextAnchor.MiddleLeft:TextAnchor.MiddleRight);value.color=new Color(.16f,.44f,.38f);Place(value.rectTransform,note?.28f:.44f,y,note?.96f:.665f,top);ShrinkToFit(value,12);
            var parts=new List<GameObject>{rule,label.gameObject,value.gameObject};
            if(row==FestivalSettingsScreen.MicrophoneTest)
            {
                // The level bar grows from the left of the value's box, under its text.
                var meter=Panel(parent,"Microphone level",new Color(.35f,.80f,.69f,.55f),new Vector2(.44f,y+.012f),new Vector2(.44f,top-.012f));meter.GetComponent<Image>().raycastTarget=false;
                meter.transform.SetSiblingIndex(value.transform.GetSiblingIndex());settingsMeter=Rect(meter);parts.Add(meter);
            }
            var buttons=new GameObject[row.Buttons.Length];
            for(int i=0;i<buttons.Length;i++)
            {
                bool pair=buttons.Length==2;int direction=pair&&i==0?-1:1;
                buttons[i]=Button(parent,row.Buttons[i],new Vector2(pair?(i==0?.69f:.83f):.70f,y+inset),new Vector2(pair?(i==0?.80f:.94f):.94f,top-inset),()=>settingsScreen.Change(row,direction));
            }
            foreach(var button in buttons){ShrinkToFit(button.GetComponentInChildren<Text>(),14);parts.Add(button);}
            return (row,parts.ToArray(),value,Array.ConvertAll(buttons,b=>b.GetComponent<Button>()));
        }

        private void MapPin(Transform parent,string name,float x,float y,Color color)
        {
            Disc(parent,name+" dot",new Vector2(x-.008f,y-.013f),new Vector2(x+.008f,y+.013f),color);
            var label=Label(parent,name,17,x>.7f?TextAnchor.MiddleRight:TextAnchor.MiddleLeft);label.text=name;label.fontStyle=FontStyle.Normal;label.color=color;
            Place(label.rectTransform,x>.7f?x-.22f:x+.018f,y-.026f,x>.7f?x-.018f:x+.23f,y+.026f);
        }
        private void MapPath(Transform parent,float x1,float y1,float x2,float y2)
        {
            var line=Panel(parent,"Footpath",new Color(.61f,.83f,.74f,.38f),Vector2.zero,Vector2.zero);
            var rect=Rect(line);rect.anchorMin=rect.anchorMax=new Vector2(x1,y1);
            rect.pivot=new Vector2(0,.5f);
            var span=new Vector2((x2-x1)*1920f*.78f*.635f,(y2-y1)*1080f*.8f*.78f);
            rect.sizeDelta=new Vector2(span.magnitude,5);rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(span.y,span.x)*Mathf.Rad2Deg);
            line.GetComponent<Image>().raycastTarget=false;
        }

        private void Update()
        {
            if(session==null)return;
            ResetChatForSession();
            FollowDropLifecycle();
            if(rhythmConnectionGeneration!=session.ConnectionGeneration){rhythmConnectionGeneration=session.ConnectionGeneration;ResetRhythm();}
            if(!session.Connected)ResetRhythm();
            FestivalSettings.ApplyEffects(shopAudio,session.Profile?.Data);
            // MENU-1: a game that ends, by LEAVE SESSION or a drop, comes back to the title as the game opened: the camp in its own
            // light (the session drives the world only while connected), and the settings put away so the title shows why it ended.
            if(wasConnected&&!session.Connected){settingsOpen=false;if(world!=null)world.ShowCamp();}
            wasConnected=session.Connected;
            // The host's replies name money in dollars; each player reads their own (PLAYA-1).
            string message=session.State!=null&&session.LocalPlayer!=null?FestivalHudText.MoneyText(session.State,session.LocalPlayer,session.Message):session.Message;
            var keys=session.Controls;
            message=FestivalHudText.PressKeys(FestivalHudText.DropNotice(message,keys),Bound(keys?.Interact,"E"),Bound(keys?.Chat,"F"),Bound(keys?.Drop,"G"),Bound(keys?.Use,"Q"),
                Bound(keys?.Slot1,"1"),Bound(keys?.Slot2,"2"),Bound(keys?.Slot3,"3"),Bound(keys?.Spin,"LMB"),Bound(keys?.SpinSides,"RMB"));
            status.text=connectionError==""?message:connectionError;
            status.color=connectionError==""?new Color(.25f,.35f,.32f):new Color(.58f,.16f,.12f);
            if(session.Message!=lastNotice){lastNotice=session.Message;noticeUntil=Time.unscaledTime+5;}
            notice.text=message;
            noticePanel.SetActive(session.Connected&&!session.MenuOpen&&!string.IsNullOrEmpty(session.Message)&&Time.unscaledTime<noticeUntil&&session.Message!="Accepted");
            titlePanel.SetActive(!session.Connected&&!settingsOpen);
            if(session.Connected){pendingComfortStart=null;comfortPanel.SetActive(false);}
            bool showMenu=session.Connected&&session.MenuOpen;
            // MENU-1: SETTINGS on the title opens the pass's own settings, on the pass's card.
            bool passUp=showMenu||!session.Connected&&settingsOpen;
            objectivePanel.SetActive(session.Connected&&!showMenu);
            timerPanel.SetActive(session.Connected&&!showMenu);
            vitalPanel.SetActive(session.Connected&&!showMenu);
            voice.gameObject.SetActive(session.Connected&&!showMenu);
            menuShade.SetActive(passUp);
            menuPanel.SetActive(passUp);
            menuHome.SetActive(!settingsOpen);
            settingsPanel.SetActive(settingsOpen);
            // HUD-7: How to Play stays up over the main menu or the pass until BACK, Esc or the pass closing.
            if(session.Connected&&!showMenu||session.Controls.Menu.WasPressedThisFrame())howToPlayOpen=false;
            howToPlayPanel.SetActive(howToPlayOpen);
            // MENU-1: the session reads no keys until it connects, so on the title Esc is the HUD's: it puts the settings away, or
            // steps back a page.
            if(!session.Connected&&session.Controls.Menu.WasPressedThisFrame()&&!session.Controls.Rebinding){if(settingsOpen)settingsOpen=false;else titleScreen.Back();}
            UpdateSettingsScreen();
            string allFound=session.Profile.CampEggCompletionText;campSecrets.text=session.Profile.CampEggCountText+(allFound==""?"":"\n"+allFound);
            settingsBack.text=session.Connected?"BACK TO PASS":"BACK";
            actionsPanel.SetActive(showMenu&&crewOpen&&!settingsOpen&&session.State?.Phase=="Playing");
            checkoutPanel.SetActive(session.Connected&&!showMenu&&checkoutItem!="");
            voice.text=session.Connected?(session.Voice.Available?session.Voice.Status:"VOICE OFF"):"";
            if(!session.Connected){lifeUntil=0;lifePanel.SetActive(false);spiritTag.SetActive(false);saleStampPanel.SetActive(false);ClearPendingEquipment();levelBanner.text="";objective.text="";objectiveTitle.text="";vitals.text="";timerText.text="";roster.text="";prompt.text="";promptPanel.SetActive(false);nearbyPanel.SetActive(false);reviewPanel.SetActive(false);heldDetailPanel.SetActive(false);rosterPanel.SetActive(false);inventoryPanel.SetActive(false);reticle.SetActive(false);rhythmPanel.SetActive(false);rhythmShade.SetActive(false);dancePanel.SetActive(false);drumPanel.SetActive(false);dancePreview.Hide();dialoguePanel.SetActive(false);mapPanel.SetActive(false);mapShade.SetActive(false);trippingPanel.SetActive(false);testModeTag.SetActive(false);briefingPanel.SetActive(false);tipPanel.SetActive(false);sellerTalk.End();chatWindow.Close();sundownPanel.SetActive(false);resultsPanel.SetActive(false);minigamePanel.SetActive(false);lightShowPanel.SetActive(false);ApplyEffects(null);FrameTitle();return;}
            rosterPanel.SetActive(!showMenu);
            var state=session.State;var player=session.LocalPlayer;if(state==null||player==null){rhythmShade.SetActive(false);rhythmPanel.SetActive(false);dancePanel.SetActive(false);drumPanel.SetActive(false);dancePreview.Hide();return;}
            handGear.Clear();foreach(var item in player.Inventory)if(item.ItemId!="little_spoon")handGear.Add(item);
            FollowPendingEquipment(state,player);
            reviewPanel.SetActive(state.Phase=="CampReview"&&!showMenu);
            if(state.Phase=="CampReview")UpdateReview(state,player);
            var heldDefinition=state.Phase=="Shopping"?Catalog.FindItem(player.HeldOfferId):null;
            heldDetailPanel.SetActive(heldDefinition!=null&&!showMenu);
            if(heldDefinition!=null){heldTitle.text=heldDefinition.Name.ToUpperInvariant();heldPrice.text=FestivalHudText.Money(state,player,heldDefinition.Price).ToUpperInvariant();heldDetailText.text=FestivalHudText.MoneyText(state,player,heldDefinition.Description);}
            ApplyContrast(session.Profile.Data.HighContrast);
            UpdatePurchaseFeedback(state,player);
            nextButton.SetActive(state.Phase=="Results"&&session.IsHost);
            cancelButton.SetActive(state.Phase=="Playing"&&FestivalHudText.CanCancel(state,player));
            cancelButton.GetComponentInChildren<Text>().text=ActiveConfirmation(state,player)!=null?"FINISH CHAT CHECK":"CANCEL ACTION";
            resumeButton.SetActive(!nextButton.activeSelf&&!cancelButton.activeSelf);
            // Only a test build's host may send these. Everyone else, a joined friend too, still sees the tag.
            bool tester=session.IsHost&&session.DevelopmentFestivalSelection;
            testModeButton.SetActive(tester);clearLevelButton.SetActive(tester&&state.TestMode&&state.Phase=="Playing");
            testModeButton.GetComponentInChildren<Text>().text=state.TestMode?"TEST MODE: ON":"TEST MODE: OFF";
            crewButton.anchorMax=new Vector2(tester?.495f:.955f,crewButton.anchorMax.y);
            testModeTag.SetActive(state.TestMode);
            UpdateText(state,player);
            trippingPanel.SetActive(!showMenu&&tripping.text!="");
            UpdateGearLayout(handGear.Count>0);
            UpdateActions(state,player);
            if(checkoutItem!="" && (Catalog.FindItem(checkoutItem)==null || (state.Phase=="Shopping"?(player.HeldOfferId!=checkoutItem||!Near(player,0,7)):(state.Phase!="Playing"||FocusedOffer(state,player)!=checkoutItem))))checkoutItem="";
            var checkoutDefinition=Catalog.FindItem(checkoutItem);
            checkoutPanel.SetActive(!showMenu&&checkoutDefinition!=null);
            if(checkoutDefinition!=null)checkoutText.text=Bound(session?.Controls?.Interact,"E")+"  PAY "+FestivalHudText.Money(state,player,checkoutDefinition.Price).ToUpperInvariant()+"  •  "+checkoutDefinition.Name.ToUpperInvariant()+"    "+Bound(session?.Controls?.Drop,"G")+" CANCEL";
            promptPanel.SetActive(!showMenu&&!string.IsNullOrEmpty(prompt.text));
            promptKeycap.SetActive(primaryAction!=null);
            nearbyPanel.SetActive(promptPanel.activeSelf&&nearby.text!="");
            UpdateKeyboard(state,player);
            UpdateChat(state,player,showMenu);
            UpdateRhythm(state,player);
            UpdateMap(state,player);
            if(mapPanel.activeSelf){rhythmShade.SetActive(false);dancePanel.SetActive(false);drumPanel.SetActive(false);dancePreview.Hide();}
            if(rhythmPanel.activeSelf&&!showMenu&&!mapPanel.activeSelf)
            {
                objectivePanel.SetActive(false);timerPanel.SetActive(false);vitalPanel.SetActive(false);voice.gameObject.SetActive(false);rosterPanel.SetActive(false);promptPanel.SetActive(false);nearbyPanel.SetActive(false);noticePanel.SetActive(false);heldDetailPanel.SetActive(false);checkoutPanel.SetActive(false);trippingPanel.SetActive(false);
            }
            inventoryPanel.SetActive(!showMenu&&!rhythmPanel.activeSelf&&!mapPanel.activeSelf&&state.Phase!="CampReview");
            gearToast.SetActive(Time.unscaledTime<gearToastUntil);
            UpdateSundownAndResults(state,player,showMenu);
            bool uncovered=!showMenu&&!mapPanel.activeSelf&&!rhythmPanel.activeSelf;
            UpdateLife(state,player,uncovered);
            UpdateMinigame(state,player,uncovered);
            UpdateLightShow(state,player,uncovered);
            UpdateBriefing(state,player,uncovered&&!minigamePanel.activeSelf);
            UpdateTip(state,player,uncovered);
            // ESC-2: a security stop on you says why in the session notice, once, as soon as nothing covers it (a dance, the map or
            // the menu would). Clearing lastNotice shows it again for a second stop, even with the first's still the last message.
            if(FestivalHudText.StoppedBy(state,player)==null)stopNoticed=false;
            else if(!stopNoticed&&!showMenu&&!mapPanel.activeSelf&&!rhythmPanel.activeSelf){stopNoticed=true;session.SetMessage(FestivalHudText.StopNotice);lastNotice="";}
            // TRIP-8: whoever walks onto the cloud's stash is told so the same way, once.
            if(state.CloudStashFinderId!=player.Id)stashNoticed=false;
            else if(!stashNoticed&&uncovered){stashNoticed=true;session.SetMessage(FestivalHudText.CloudStashNotice);lastNotice="";}
            if(state.VisionStashFinderId!=player.Id)visionStashNoticed=false;
            else if(!visionStashNoticed&&uncovered){visionStashNoticed=true;session.SetMessage(FestivalHudText.VisionStashNotice);lastNotice="";}
            // PLAYA-5: what the crowd tipped for your deck set, once. Only your own view carries the receipt.
            string tips=FestivalSimulation.DeckTipSpeech(state,player);
            if(tips!=""&&uncovered&&deckTipNoticed!=state.RoundId+"/"+player.DeckTipDeliveryId){deckTipNoticed=state.RoundId+"/"+player.DeckTipDeliveryId;session.SetMessage(tips);lastNotice="";}
            // A grab plays the oh-yeah at once; its notice waits, like the others, until nothing covers it.
            if(giggleTankNews.Follow(state,player,session.ConnectionGeneration))
            {
                shopAudio.PlayOneShot(ohYeah);OhYeahPlays++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("GiggleTank","oh_yeah","grabber="+(state.GiggleTankGrabberId==player.Id?"own":"crew")+" plays="+OhYeahPlays);
#endif
            }
            if(uncovered){string tank=giggleTankNews.Take();if(tank!=""){session.SetMessage(tank);lastNotice="";}}
            UpdateGearLossNotice(state,player,uncovered&&!minigamePanel.activeSelf&&!resultsPanel.activeSelf&&!reviewPanel.activeSelf&&!briefingPanel.activeSelf);
            reticle.SetActive(!showMenu&&!mapPanel.activeSelf&&!rhythmPanel.activeSelf&&!lightShowPanel.activeSelf);
            ApplyEffects(player);
            var gear=new List<string>();
            foreach(var item in handGear)gear.Add(ItemName(item.ItemId));
            preview.text=(gear.Count==0?"0 / 3   •   NOTHING PACKED":gear.Count+" / 3   •   "+string.Join("  /  ",gear))
                +(player.Inventory.Exists(i=>i.ItemId=="little_spoon")?"   •   LITTLE SPOON WORN":"");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame)diagnostics.gameObject.SetActive(!diagnostics.gameObject.activeSelf);
            if(diagnostics.gameObject.activeSelf)
            {
                var localNpc=state.Npcs.Find(n=>n.TargetId==player.Id||n.Suspicion>0);
                var interaction=state.Interactions.Find(i=>i.Id==player.InteractionId);
                diagnostics.text="DEVELOPMENT BUILD — F8\nround "+state.RoundId+"  tick "+state.Tick+"  tx "+state.TransactionSequence
                    +"\nRTT "+session.RoundTripMs.ToString("0")+" ms  snapshot "+session.LastSnapshotAge.ToString("0.000")+" s  frame "+(Time.unscaledDeltaTime*1000).ToString("0.0")+" ms"
                    +"\nNPCs "+state.Npcs.Count+"  state "+(localNpc==null?"none":localNpc.Id+" "+localNpc.Mode+" suspicion "+localNpc.Suspicion.ToString("0"))
                    +"\ninteraction "+(interaction==null?"none":interaction.Id+" "+interaction.Kind+" inputs "+interaction.Inputs.Count+" score "+interaction.Score.ToString("0.00"))
                    +"\neffects "+string.Join(",",player.Effects.ConvertAll(e=>e.Id+":"+e.InstanceId))
                    +"  spoon trust "+(player.Inventory.Exists(i=>i.ItemId=="little_spoon")?"x0.90":"off");
            }
#endif
        }

        private void LateUpdate()
        {
            if(session==null)return;
            ApplyTextReflow(textScale);
            if(!session.Connected)return;
            UpdateTrippingLayout();
            bool visible=chatWindow!=null&&chatWindow.Open&&!session.MenuOpen&&!mapPanel.activeSelf;
            foreach(var delivery in session.SpeechDeliveries.Deliveries)
            {
                if(delivery.IsPublic||delivery.SpeakerId!=session.LocalPlayerId||!delivery.Id.Contains(":check:"))continue;
                bool shows=visible&&delivery.WorldActive&&!delivery.Retracted;
                if(shows&&delivery.OwnAudioVisible&&delivery.VisibleCharacters>delivery.OwnAudioCharacters)
                    session.SpeechBubbles?.Babble.PlayRevealed(delivery,delivery.OwnAudioCharacters,delivery.VisibleCharacters);
                delivery.OwnAudioVisible=shows;delivery.OwnAudioCharacters=delivery.VisibleCharacters;
            }
        }

        // Scale authored text and its local cards; the canvas and rhythm geometry keep their gameplay coordinates.
        private void ApplyTextReflow(float scale)
        {
            bool changed=!Mathf.Approximately(appliedTextScale,scale);
            foreach(var label in authoredLabels)
            {
                if(label==null)continue;
                bool added=!authoredTextSizes.TryGetValue(label,out var size);
                if(added){size=(label.fontSize,label.resizeTextMinSize,label.resizeTextMaxSize);authoredTextSizes.Add(label,size);}
                if(!changed&&!added)continue;
                label.fontSize=Mathf.RoundToInt(size.Size*scale);
                label.resizeTextMinSize=Mathf.RoundToInt(size.Minimum*scale);
                label.resizeTextMaxSize=Mathf.Max(label.resizeTextMinSize,Mathf.RoundToInt(size.Maximum*scale));
            }
            if(changed)
            {
                appliedTextScale=scale;rosterLines=-1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Settings] HUD text scale="+scale.ToString("F2")+"; rhythm geometry unchanged.");
#endif
            }
            // The pass grows about its centre, so all of its authored click areas grow with its text.
            float passHeight=Mathf.Min(.95f,.81f*scale),passWidth=.45f*scale;
            Place(Rect(menuPanel),.5f-passWidth/2,.5f-passHeight/2,.5f+passWidth/2,.5f+passHeight/2);
            Place(Rect(briefingPanel),.25f,.455f-.17f*scale,.75f,.455f);
            Place(Rect(minigamePanel),.31f,.42f-.135f*scale,.69f,.42f);
            Place(Rect(inventoryPanel),.978f-.264f*scale,.024f,.978f,.024f+.124f*scale);
            foreach(var action in dynamicActions)if(action!=null)action.GetComponent<LayoutElement>().preferredHeight=64*scale;
        }

        private void UpdateGearLayout(bool hasGear)
        {
            if(expandedGear!=hasGear)
            {
                expandedGear=hasGear;
                // Empty destinations remain visible so the local cursor can keep moving after stowing or using the last item.
                Place(Rect(inventoryPanel),.714f,.024f,.978f,.148f);
                inventoryHeading.gameObject.SetActive(true);
                emptyGearLabel.gameObject.SetActive(false);
                foreach(var slot in slotFrames)slot.gameObject.SetActive(true);
            }
            if(!hasGear)emptyGearLabel.text="0 / 3  •  NO GEAR";
        }

        private void UpdateRosterLayout(int checklistLines)
        {
            if(rosterLines==checklistLines)return;
            rosterLines=checklistLines;
            // Night 2's everyone-home checklist grows the crew card downward, one row per line plus the card's padding,
            // so a one-row checklist (a solo crew) still shows its row under the header at 1080p.
            Place(Rect(rosterPanel),checklistLines>0?.78f:.835f,checklistLines>0?.876f-.024f*checklistLines:.846f,.978f,.882f);
        }

        // Measure only the rendered extras. Long friend names share the existing bounded best-fit range,
        // keeping each action on its own row and the complete list one menu press away.
        private void UpdateNearbyLayout()
        {
            int lines=nearby.text==""?0:nearby.text.Split('\n').Length;
            float width=nearby.rectTransform.rect.width;
            var settings=nearby.GetGenerationSettings(new Vector2(width,0));
            settings.resizeTextForBestFit=false;settings.horizontalOverflow=HorizontalWrapMode.Overflow;settings.verticalOverflow=VerticalWrapMode.Overflow;
            settings.fontSize=nearby.fontSize;
            var measured=nearby.cachedTextGeneratorForLayout;
            while(settings.fontSize>nearby.resizeTextMinSize&&measured.GetPreferredWidth(nearby.text,settings)/nearby.pixelsPerUnit>width)settings.fontSize--;
            nearby.resizeTextMaxSize=settings.fontSize;
            float height=measured.GetPreferredHeight(nearby.text,settings)/nearby.pixelsPerUnit;
            float parentHeight=((RectTransform)nearbyPanel.transform.parent).rect.height;
            float bottom=Mathf.Max(.629f,.769f-.014f-height/Mathf.Max(1,parentHeight));
            if(nearbyLines==lines&&Mathf.Approximately(Rect(nearbyPanel).anchorMin.y,bottom))return;
            nearbyLines=lines;
            Place(Rect(nearbyPanel),.022f,bottom,.295f,.769f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","nearby_rows","shown="+lines);
#endif
        }

        // Runs after the text scale reflows and the canvas scaler updates, so it measures this frame's sizes. The sundown warning
        // stacks under the notice as it was authored to.
        private void UpdateTrippingLayout()
        {
            float scale=textScale,right=.64f+.07f*(scale-1)/.3f;var card=Rect(trippingPanel);
            if(card.anchorMax.x!=right)Place(card,.36f,card.anchorMin.y,right,.906f);
            float width=tripping.rectTransform.rect.width,pixels=tripping.pixelsPerUnit,canvasHeight=((RectTransform)trippingPanel.transform.parent).rect.height;
            if(tripping.text==trippingText&&tripping.font==trippingFont&&tripping.fontSize==trippingSize&&width==trippingWidth&&pixels==trippingPixels&&canvasHeight==trippingCanvas&&scale==trippingScale)return;
            trippingText=tripping.text;trippingFont=tripping.font;trippingSize=tripping.fontSize;trippingWidth=width;trippingPixels=pixels;trippingCanvas=canvasHeight;trippingScale=scale;
            var settings=tripping.GetGenerationSettings(new Vector2(width,0));settings.resizeTextForBestFit=false;settings.verticalOverflow=VerticalWrapMode.Overflow;
            float height=trippingText==""?0:tripping.cachedTextGeneratorForLayout.GetPreferredHeight(trippingText,settings)/pixels;
            float bottom=.906f-(height+TripperPadding*scale)/Mathf.Max(1,canvasHeight),noticeTop=Mathf.Min(.849f,bottom-.008f),noticeBottom=noticeTop-.064f*scale,sundownTop=Mathf.Min(.775f,noticeBottom-.01f);
            Place(card,.36f,bottom,right,.906f);
            Place(Rect(noticePanel),.368f,noticeBottom,.695f,noticeTop);
            Place(Rect(sundownPanel),.30f,sundownTop-.065f,.70f,sundownTop);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("HudLayout","dose_card","lines="+(trippingText==""?0:tripping.cachedTextGeneratorForLayout.lineCount)+" text_height="+height.ToString("F1")+" width="+width.ToString("F1")+" scale="+scale.ToString("F2")+" ppu="+pixels.ToString("F3")+" bottom="+bottom.ToString("F3")+" notice_top="+noticeTop.ToString("F3"));
#endif
        }

        private void UpdateText(RoundState state,PlayerState player)
        {
            levelBanner.text=FestivalHudText.LevelBanner(state);
            objectiveTitle.text=FestivalHudText.ObjectiveTitle(state,player);
            objective.text=FestivalHudText.ObjectiveDetail(state,player,session!=null&&session.DevelopmentFestivalSelection,Bound(session?.Controls?.Chat,"F"),Bound(session?.Controls?.Interact,"E"),
                Bound(session?.Controls?.Slot1,"1"),Bound(session?.Controls?.Slot2,"2"),Bound(session?.Controls?.Slot3,"3"));
            timerText.text=FestivalHudText.Clock(state);
            tripping.text=FestivalHudText.Tripping(state,player.Id);
            TrackSale(state,player);
            vitals.text=FestivalHudText.Vitals(state,player,Bound(session?.Controls?.Interact,"E"),Bound(session?.Controls?.Drop,"G"),saleFlash&&FestivalHudText.MoneyFlashOn(Time.unscaledTime-saleStampAt));
            saleStampPanel.SetActive(state.Phase=="Playing"&&Time.unscaledTime-saleStampAt<FestivalHudText.SaleStampSeconds);
            var threat=FestivalHudText.Threat(state,player);
            threatShown=FestivalHudText.EaseThreat(threatShown,threat.Fill,Time.unscaledDeltaTime,session?.Profile?.Data.ReducedMotion??true);
            threatFill.anchorMax=new Vector2(threatShown,1);threatFillImage.color=threatWord.color=ThreatColors[threat.Band];threatWord.text=FestivalHudText.ThreatBands[threat.Band];
            promptKey.text=Bound(session?.Controls?.Interact,"E");
            for(int i=0;i<slotFrames.Length;i++)
            {
                var item=i<handGear.Count?handGear[i]:null;
                bool equipped=item!=null&&item.ItemId==EquippedItem(player);
                slotFrames[i].color=i==selectedSlot?new Color(.13f,.34f,.32f,1):new Color(.07f,.13f,.15f,1);
                slotTexts[i].text=(i+1)+(i==selectedSlot?" ▸ ":"  ")+(item==null?"EMPTY":ItemName(item.ItemId)+" ×"+item.Count+(equipped?"  ●":""));
                slotTexts[i].color=equipped?new Color(1,.79f,.53f):Paper;
            }
            int ready=state.Players.FindAll(p=>p.Ready).Count;
            string checklist=FestivalHudText.HomeChecklist(state,player.Id);
            roster.text=checklist!=""?checklist:state.Players.Count+" CREW"+(state.Phase=="Shopping"?"  •  "+ready+" READY":"  •  "+Bound(session?.Controls?.Objectives,"TAB")+" MAP");
            UpdateRosterLayout(checklist==""?0:checklist.Split('\n').Length);
            menuRoster.text=state.Players.Count+" / 8 FRIENDS\n"+string.Join("   •   ",state.Players.ConvertAll(p=>(p.Id==player.Id?"YOU":p.Name)+(p.Ready?" READY":"")));
            menuPhase.text=state.Phase=="Shopping"?"CAMPSITE":state.Phase=="CampReview"?"ROUND REVIEW":state.Phase=="Playing"?"FESTIVAL":state.Phase=="Results"?"BACK TO CAMP":state.Phase==FestivalSimulation.MinigamePhase?"MINIGAME":"GATHERING";
        }

        private void UpdateLife(RoundState state,PlayerState player,bool uncovered)
        {
            var news=lifeNews.Follow(state,player,session.ConnectionGeneration);
            if(news.Title!=""){lifeTitle.text=news.Title;lifeDetail.text=news.Detail;lifeUntil=Time.unscaledTime+LifeBannerSeconds;}
            lifePanel.SetActive(uncovered&&state.Phase=="Playing"&&Time.unscaledTime<lifeUntil);
            spiritTag.SetActive(uncovered&&player.Life=="Spirit"&&FestivalWorld.ShowsFestival(state.Phase));
        }

        // LOOP-5: the results banner, and the sundown warning once per stage (see SundownNoticeSeconds).
        private void UpdateSundownAndResults(RoundState state,PlayerState player,bool showMenu)
        {
            resultsTitle.text=FestivalHudText.ResultsBannerTitle(state);resultsTitle.color=resultsAccent.color=state.Result=="Success"?Mint:Orange;
            resultsDetail.text=FestivalHudText.ResultsBannerDetail(state,player);
            resultsPanel.SetActive(resultsTitle.text!="");
            int stage=FestivalHudText.SundownStage(state);
            string line=FestivalHudText.SundownLine(state,player);rhythmSundown.text=line;
            rhythmSundown.gameObject.SetActive(line!="");rhythmDialogue.gameObject.SetActive(line=="");
            var level=(state.FestivalIndex,state.LevelIndex,state.EncoreTier,state.SpinSeed);
            if(state.RoundId!=sundownRound||level!=sundownLevel)
            {sundownRound=state.RoundId;sundownLevel=level;sundownStage=0;sundownShown=0;Array.Clear(sundownProgress,0,sundownProgress.Length);}
            else if(sundownStage>0)sundownProgress[sundownStage]=sundownShown;
            if(stage!=sundownStage){sundownStage=stage;sundownShown=sundownProgress[stage];}
            sundownPanel.SetActive(stage>0&&sundownShown<SundownNoticeSeconds&&!showMenu&&!mapPanel.activeSelf&&!rhythmPanel.activeSelf);
            if(!sundownPanel.activeSelf)return;
            sundownText.text=FestivalHudText.SundownWarning(state,player);sundownShown+=Time.unscaledDeltaTime;
        }

        // MINI-1: the minigame card while nothing covers it (clear: no menu, map or dance); a win shows mint (MinigameWon), else orange.
        private void UpdateMinigame(RoundState state,PlayerState player,bool clear)
        {
            var (title,detail)=FestivalHudText.MinigameCard(state,player);
            minigamePanel.SetActive(clear&&title!="");if(!minigamePanel.activeSelf)return;
            minigameTitle.text=title;minigameDetail.text=detail;
            minigameTitle.color=FestivalHudText.MinigameWon(state)?Mint:Orange;
        }

        // MINI-3: the Light Show question open to you, its five answers to click until one is sent, the sent one marked; once scored,
        // your answers beside the right ones. Only while nothing covers it (clear: no menu, map or dance).
        private void UpdateLightShow(RoundState state,PlayerState player,bool clear)
        {
            var (slot,question,answer)=FestivalHudText.LightShowOpen(state,player.Id);
            if(slot!=lightShowSent)lightShowSent=-1;
            string reveal=question==null?FestivalHudText.LightShowReveal(state,player.Id):"";
            lightShowPanel.SetActive(clear&&(question!=null||reveal!=""));if(!lightShowPanel.activeSelf)return;
            lightShowPrompt.text=question!=null?question.Prompt:"YOUR ANSWERS";
            lightShowReveal.gameObject.SetActive(question==null);lightShowReveal.text=reveal;
            int sent=answer!=FestivalHudText.NoAnswer?answer:lightShowSent>=0?lightShowChoice:FestivalHudText.NoAnswer;
            for(int k=0;k<lightShowAnswers.Length;k++)
            {
                bool shown=question!=null&&(k==4||k<question.Choices.Count);
                if(lightShowAnswers[k].gameObject.activeSelf!=shown)lightShowAnswers[k].gameObject.SetActive(shown);
                if(!shown)continue;
                lightShowLabels[k].text=k<4?question.Choices[k]:question.Unsure;
                lightShowAnswers[k].interactable=sent==FestivalHudText.NoAnswer;
                lightShowAnswers[k].image.color=sent==(k<4?k:-1)?Mint:new Color(.84f,.85f,.76f,1);
                lightShowKeys[k].text=Bound(session.Controls.Answers[k],AnswerKeys[k]);
            }
            if(question==null){lightShowFocus=-1;return;}
            // Up and down ring round the answers shown (the choices, then Not sure). Each new question takes focus on its first answer,
            // so Enter sends the one picked.
            int count=question.Choices.Count;
            Button At(int row)=>lightShowAnswers[row==count?4:row];
            for(int row=0;row<=count;row++)
            {
                var nav=At(row).navigation;nav.selectOnUp=At(row==0?count:row-1);nav.selectOnDown=At(row==count?0:row+1);At(row).navigation=nav;
            }
            if(slot!=lightShowFocus&&EventSystem.current!=null){lightShowFocus=slot;EventSystem.current.SetSelectedGameObject(lightShowAnswers[0].gameObject);}
            // A key goes through AnswerLightShow, so a key and a click together still send one Answer per question.
            if(!session.MenuOpen&&!session.Controls.Rebinding)for(int k=0;k<lightShowAnswers.Length;k++)
                if((k==4||k<count)&&session.Controls.Answers[k].WasPressedThisFrame()){AnswerLightShow(k<4?k:-1);break;}
        }
        // A click answers the question still open to you, once: a second click, or one on a question since closed, sends nothing.
        private void AnswerLightShow(int choice)
        {
            var state=session.State;var player=session.LocalPlayer;if(state==null||player==null)return;
            var (slot,question,answer)=FestivalHudText.LightShowOpen(state,player.Id);
            if(question==null||answer!=FestivalHudText.NoAnswer||lightShowSent==slot)return;
            lightShowSent=slot;lightShowChoice=choice;
            session.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.LightShowAnswer,amount:choice,direction:slot);
        }

        // HUD-7: the level briefing for its window, unless a key put it away this level; only while nothing covers it (clear: no
        // menu, map or dance). Laid out again only when its text changes.
        private void UpdateBriefing(RoundState state,PlayerState player,bool clear)
        {
            string briefing=FestivalHudText.Briefing(state,player.Id,Bound(session?.Controls?.Chat,"F"));
            if(briefing!=shownBriefing){shownBriefing=briefing;var lines=briefing.Split('\n');briefingGoal.text=lines[0];briefingJob.text=lines.Length>1?lines[1]:"";briefingStakes.text=lines.Length>2?lines[2]:"";}
            if(briefingPanel.activeSelf&&SkipsBriefing())briefingSkipped=state.RoundId;
            briefingPanel.SetActive(clear&&briefing!=""&&briefingSkipped!=state.RoundId);
            briefingFade.alpha=FestivalHudText.BriefingAlpha(state.ElapsedSeconds,session.Profile.Data.ReducedMotion);
        }
        // HUD-7: the first tip whose moment has come, up for TipSeconds of being seen and remembered in the profile as it first shows;
        // the next waits for it to go. Only while nothing covers it (clear: no menu, map or dance).
        private void UpdateTip(RoundState state,PlayerState player,bool clear)
        {
            if(tipId!=""&&(tipRound!=state.RoundId||tipShown>=FestivalHudText.TipSeconds||state.Phase!="Playing"||player.Life!="Alive"))tipId="";
            if(tipId==""&&clear)
            {
                var tip=FestivalHudText.Tip(state,player,session.Profile.Data.SeenTips,Bound(session?.Controls?.Interact,"E"),Bound(session?.Controls?.Drop,"G"),Bound(session?.Controls?.Chat,"F"));
                if(tip.Id!=""){tipId=tip.Id;tipRound=state.RoundId;tipShown=0;tipText.text=tip.Text;session.Profile.SeeTip(tip.Id);}
            }
            tipPanel.SetActive(clear&&tipId!="");
            if(tipPanel.activeSelf)tipShown+=Time.unscaledDeltaTime;
        }
        // HUD-7: any key that doesn't move you (Move, Sprint) puts the briefing away; walking off keeps it up, to read on the way.
        private bool SkipsBriefing()
        {
            var keys=Keyboard.current;if(keys==null)return false;
            foreach(var key in keys.allKeys)if(key.wasPressedThisFrame&&!Binds(session.Controls.Move,key)&&!Binds(session.Controls.Sprint,key))return true;
            return false;
        }
        private static bool Binds(InputAction action,InputControl control){foreach(var bound in action.controls)if(bound==control)return true;return false;}

        private void UpdateKeyboard(RoundState state,PlayerState player)
        {
            bool dropTap=TakeDropPress(state);
            if(session.Controls.Rebinding){CancelGearHold();stopHeldSince=-1;ShowStopHold(0);return;}
            if(state.Phase=="CampReview"&&!session.MenuOpen)
            {
                int award=session.Controls.Slot1.WasPressedThisFrame()?0:session.Controls.Slot2.WasPressedThisFrame()?1:session.Controls.Slot3.WasPressedThisFrame()?2:-1;
                if(award>=0&&award<state.ReviewAwards.Count)session.Command("ReviewVote",NextReviewPick(state,player,award),amount:award);
            }
            if(player.CampVisitId!=""&&!DropCovered()&&dropTap)
            {
                session.Command("ExitCamp");return;
            }
            if(player.CampVisitId!=""&&!session.MenuOpen&&session.Controls.Chat.WasPressedThisFrame())
            {
                session.Command("CampAntic");return;
            }
            // CAMP-1: beside Marigold F talks to her (with nothing unpaid in hand), never the host's festival switch, which F is anywhere
            // else at camp.
            if(!session.MenuOpen&&session.Controls.Chat.WasPressedThisFrame()&&CampSellerTalk.Beside(state,player))
            {
                if(CampSellerTalk.CanTalk(state,player)&&!sellerTalk.Open)sellerTalk.Begin(chatWindow,state,player,Time.unscaledTime);
                return;
            }
            if(!session.MenuOpen&&session.Controls.Chat.WasPressedThisFrame()&&FestivalHudText.FestivalChoice(state,player.Id,session.DevelopmentFestivalSelection)!="")
            {
                session.Command("ChooseFestival",amount:FestivalHudText.NextFestival(state,session.DevelopmentFestivalSelection));return;
            }
            int equipSlot=session.Controls.Slot1.WasPressedThisFrame()?0:session.Controls.Slot2.WasPressedThisFrame()?1:session.Controls.Slot3.WasPressedThisFrame()?2:-1;
            // DIALOG-5: while the chat window has questions to ask this frame, or a talk's replies (DIALOG-3), 1-3 pick them instead of
            // equipping (never in a rope chat, which has none, nor once the conversation is over).
            // CAMP-1: so do Marigold's questions while you talk to her.
            if(equipSlot>=0&&!session.MenuOpen&&(FestivalChatWindow.TakesReplies(state,player)&&!chatWindow.IsDismissed(state,FestivalChatWindow.LiveChat(state,player))||sellerTalk.Open)){chatReply=equipSlot;equipSlot=-1;}
            // HUD-4: gear keys only where the host takes them (FestivalSimulation.Interactive): never at the debrief, the spin or loading.
            bool gearKeys=FestivalSimulation.Interactive(state.Phase)&&player.Life=="Alive"&&!FestivalHudText.StopsByHold(state,player);
            bool fromWheel=false,selectedThisFrame=false;
            // The cursor persists through free hands and empty slots. A wheel destination always equips; a held numeric key toggles.
            if(equipSlot<0&&!session.MenuOpen&&gearKeys&&!sellerTalk.Open&&FestivalHudGear.Cycles(state,player,handGear.Count))
            {
                int step=FestivalHudGear.Notch(session.Controls.Cycle.ReadValue<float>(),Time.unscaledTime,ref lastGearStep);
                if(step!=0){equipSlot=FestivalHudGear.NextSlot(selectedSlot,step);fromWheel=true;}
            }
            if(equipSlot>=0&&!session.MenuOpen&&gearKeys)
            {
                CancelGearHold();
                // HUD-8: the switch flashes over the gear bar, or why not: inside a tent, or behind an unpaid item, nothing switches.
                string toast=FestivalHudGear.Toast(player,handGear,equipSlot,Bound(session?.Controls?.Drop,"G"));if(toast!=""){gearToastText.text=toast;gearToastUntil=Time.unscaledTime+FestivalHudGear.ToastSeconds;}
                if(FestivalHudGear.Blocked(player)=="")
                {
                    selectedSlot=equipSlot;selectedThisFrame=true;manualEquipmentTick=state.Tick;
                    string item=equipSlot<handGear.Count&&handGear[equipSlot].Count>0?handGear[equipSlot].ItemId:"";
                    bool stowing=!fromWheel&&item!=""&&item==EquippedItem(player);
                    if(stowing)item="";
                    if(stowing)gearToastText.text=(equipSlot+1)+"  •  STOWED  •  HANDS FREE";
                    else if(item=="")gearToastText.text=(equipSlot+1)+"  •  EMPTY  •  HANDS FREE";
                    qAsked="";
                    pendingEquipment=true;pendingEquipmentItem=item;pendingEquipmentRound=state.RoundId;pendingEquipmentCommand="";
                    pendingEquipmentAt=Time.unscaledTime;pendingEquipmentGeneration=session.ConnectionGeneration;pendingEquipmentReceipt=player.VisualReceiptSequence;
                    ShowPendingEquipment(state,player);
                    pendingEquipmentCommand=session.SendCommand("Equip",item:item);
                    if(pendingEquipmentCommand=="")ClearPendingEquipment();
                    else EquipmentCommandResult(session.LastLocalCommandResult); // A host reply can arrive before SendCommand returns.
                    FollowPendingEquipment(state,player);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","gear_selection","source="+(fromWheel?"wheel":"key")+" cursor="+selectedSlot+" equipped="+(item==""?"none":item)+" result="+session.Message+" receipt_suppressed="+player.VisualReceiptSequence);
#endif
                }
            }
            // MINI-4: F calls Mystery Cups for the player who is up (MinigamePrompt says so).
            if(session.Controls.Chat.WasPressedThisFrame()&&!session.MenuOpen&&FestivalSimulation.CupsTurn(state)==player.Id){session.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups);return;}
            if(session.Controls.Chat.WasPressedThisFrame()&&!session.MenuOpen)ChatKey(state,player);
            // DANCE-12: E sits between the W and D lanes, so mid-dance it runs nothing, even in the moment before UpdateActions
            // next drops the primary action, and a tap of G drops nothing; holding G stops the dance (HoldToStop). K2: mid-talk a
            // tap of E runs nothing either; the effective Menu binding walks out of it.
            bool dancing=FestivalHudText.StopsByHold(state,player),talking=FestivalHudText.WalksOutByHold(state,player);
            if(session.Controls.Interact.WasPressedThisFrame()&&!session.MenuOpen&&!dancing&&!talking)primaryAction?.Invoke();
            HoldToStop(dancing&&!DropCovered()&&rhythmFocused?session.Controls.Drop:null);
            if(dropTap&&!DropCovered()&&checkoutItem!=""){checkoutItem="";return;}
            if(dropTap&&!DropCovered()&&state.Phase=="Shopping"&&player.HeldOfferId!=""){session.Command("ReturnOffer");return;}
            if(dropTap&&!DropCovered()&&gearKeys&&player.InteractionId==""&&FestivalHudText.StoppedBy(state,player)!=null)
            {
                var stock=player.Inventory.Find(i=>i.Count>0&&Catalog.FindItem(i.ItemId)?.Category=="Stock");
                if(stock!=null)session.Command("Drop",item:stock.ItemId);
                return; // Even an empty stop owns this press; it never falls through to equipped gear.
            }
            string heldItem=handGear.Find(i=>i.Count>0&&i.ItemId==EquippedItem(player))?.ItemId??"";
            if(heldItem=="")qAsked="";
            if(heldItem!=""&&gearKeys&&!selectedThisFrame&&player.InteractionId==""&&!sellerTalk.Open&&FestivalHudGear.Blocked(player)=="")
            {
                string item=heldItem;
                if(session.Controls.Use.WasPressedThisFrame()&&!session.MenuOpen)
                {
                    if(TakesOnQ(item,qAsked,qAskedAt,Time.unscaledTime)){qAsked="";session.Command("Use",item:item);}
                    // ECON-3: the first Q on stock asks; the notice stays up as long as a second Q would take it.
                    else{qAsked=item;qAskedAt=Time.unscaledTime;session.SetMessage(TakeStockAgainFor(Bound(session?.Controls?.Use,"Q")));lastNotice=session.Message;noticeUntil=qAskedAt+TakeStockSeconds;}
                }
            }
            bool dropEligible=heldItem!=""&&heldItem==player.EquippedItemId&&!pendingEquipment&&state.Tick>manualEquipmentTick&&gearKeys&&!selectedThisFrame
                &&player.InteractionId==""&&!sellerTalk.Open&&!chatWindow.Open&&FestivalHudGear.Blocked(player)==""&&FestivalHudText.StoppedBy(state,player)==null
                &&checkoutItem==""&&!DropCovered()&&rhythmFocused&&!session.Controls.Use.WasPressedThisFrame()&&!session.Controls.Interact.WasPressedThisFrame()&&!session.Controls.Chat.WasPressedThisFrame();
            HoldGearDrop(state,player,dropTap,dropEligible);
        }

        private void FollowDropLifecycle()
        {
            var state=session.State;
            if(!session.Connected||state==null||dropGeneration!=session.ConnectionGeneration||dropRound!=state.RoundId)
            {
                CancelGearHold();dropPressConsumed=session.Controls.DropPhysicallyPressed;
                dropGeneration=session.ConnectionGeneration;dropRound=state?.RoundId??"";manualEquipmentTick=-1;stopHeldSince=-1;
            }
            if(!session.Connected){gearLossRound="";gearLossGeneration=-1;gearLossSequence=0;gearLossNotice="";}
        }
        private bool DropCovered()
        {
            bool covered=session.MenuOpen||mapPanel.activeSelf||session.Controls.Objectives.IsPressed()||settingsOpen;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            covered|=DevelopmentMapVisible;
#endif
            return covered;
        }
        private bool TakeDropPress(RoundState state)
        {
            var key=session.Controls.Drop;
            if(dropCancellationSequence!=session.Controls.DropCancellationSequence){dropCancellationSequence=session.Controls.DropCancellationSequence;CancelGearHold();}
            if(!session.Controls.DropPhysicallyPressed&&!key.IsPressed()){dropPressConsumed=false;CancelGearHold();}
            bool fresh=key.WasPressedThisFrame()&&!dropPressConsumed;
            if(fresh)dropPressConsumed=true;
            if(gearHeldSince>=0&&(DropCovered()||!rhythmFocused||dropPhase!=state.Phase))CancelGearHold();
            return fresh;
        }
        private void HoldGearDrop(RoundState state,PlayerState player,bool fresh,bool eligible)
        {
            if(!eligible||!session.Controls.Drop.IsPressed()||gearHeldSince>=0&&dropHeldItem!=player.EquippedItemId)CancelGearHold();
            else if(fresh)
            {
                gearHeldSince=Time.unscaledTime;dropHeldItem=player.EquippedItemId;dropPhase=state.Phase;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DropHoldDiagnostic("start","fresh eligible press");
#endif
            }
            if(gearHeldSince<0)return;
            float progress=(Time.unscaledTime-gearHeldSince)/GearDropHoldSeconds;
            if(progress>=1)
            {
                string item=dropHeldItem;gearHeldSince=-1;dropHeldItem="";ShowStopHold(0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DropHoldDiagnostic("fire","threshold");
#endif
                session.Command("Drop",item:item);return;
            }
            prompt.text="HOLD "+session.Controls.Label(session.Controls.Drop).ToUpperInvariant()+" TO DROP "+ItemName(dropHeldItem).ToUpperInvariant();
            promptPanel.SetActive(true);promptKeycap.SetActive(false);nearbyPanel.SetActive(false);ShowStopHold(progress);
        }
        private void CancelGearHold()
        {
            if(gearHeldSince<0)return;
            gearHeldSince=-1;dropHeldItem="";ShowStopHold(0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DropHoldDiagnostic("cancel","input or context changed");
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void DropHoldDiagnostic(string action,string reason)
        {
            if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","gear_hold_"+action,"reason="+reason);
        }
#endif
        private void UpdateGearLossNotice(RoundState state,PlayerState player,bool uncovered)
        {
            if(gearLossRound!=state.RoundId||gearLossGeneration!=session.ConnectionGeneration)
            {
                gearLossRound=state.RoundId;gearLossGeneration=session.ConnectionGeneration;gearLossSequence=0;gearLossNotice="";gearLossShown=0;
            }
            if(player.GearLossSequence>gearLossSequence&&player.GearLossKind=="Death"&&!string.IsNullOrEmpty(player.GearLossNotice))
            {
                gearLossSequence=player.GearLossSequence;gearLossNotice=player.GearLossNotice;gearLossShown=0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","gear_loss_delivery","pending=true");
#endif
            }
            if(gearLossNotice==""||!uncovered)return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(gearLossShown==0&&DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","gear_loss_visible","uncovered=true");
#endif
            notice.text=gearLossNotice;noticePanel.SetActive(true);gearLossShown+=Time.unscaledDeltaTime;
            if(gearLossShown>=5)gearLossNotice="";
        }

        private void EquipmentCommandResult(CommandResult result)
        {
            if(!pendingEquipment||pendingEquipmentCommand==""||result==null||result.Id!=pendingEquipmentCommand)return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","gear_confirmation","cursor="+selectedSlot+" accepted="+result.Accepted);
#endif
            // Acceptance can precede its view. Keep intent until matching equipment, or the same bounded total lifetime.
            if(!result.Accepted)ClearPendingEquipment();
        }

        private string EquippedItem(PlayerState player)=>pendingEquipment?pendingEquipmentItem:player.EquippedItemId;
        private void FollowPendingEquipment(RoundState state,PlayerState player)
        {
            if(!pendingEquipment)return;
            if(state.RoundId!=pendingEquipmentRound||session.ConnectionGeneration!=pendingEquipmentGeneration||!FestivalSimulation.Interactive(state.Phase)||player.Life!="Alive"||FestivalHudGear.Blocked(player)!=""||player.EquippedItemId==pendingEquipmentItem)ClearPendingEquipment();
            else if(Time.unscaledTime-pendingEquipmentAt>=EquipmentConfirmationSeconds)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","gear_confirmation_timeout","cursor="+selectedSlot+" equipped="+(player.EquippedItemId==""?"none":player.EquippedItemId));
#endif
                ClearPendingEquipment();
            }
            else ShowPendingEquipment(state,player);
        }
        private void ShowPendingEquipment(RoundState state,PlayerState player)
        {
            var hands=session.ViewCamera?.GetComponentInChildren<FestivalHands>(true);
            hands?.SetManualEquipment(pendingEquipmentItem,pendingEquipmentReceipt);
            hands?.SetState(player,state.SimulationSeconds,state.Transfers.Exists(t=>t.ToId==player.Id),session.PresentedPoiSpin(player),FestivalSimulation.ArtCarOf(state,player.Id)>=0,session.PresentedPoiSpinSides(player));
            session.LocalWorldCharacter?.SetManualEquipment(pendingEquipmentItem,pendingEquipmentReceipt);
        }
        private void ClearPendingEquipment()
        {
            if(!pendingEquipment)return;
            pendingEquipment=false;pendingEquipmentItem="";pendingEquipmentRound="";pendingEquipmentCommand="";
            var hands=session?.ViewCamera?.GetComponentInChildren<FestivalHands>(true);hands?.ClearManualEquipment();
            var state=session?.State;var player=session?.LocalPlayer;
            if(state!=null&&player!=null)hands?.SetState(player,state.SimulationSeconds,state.Transfers.Exists(t=>t.ToId==player.Id),session.PresentedPoiSpin(player),FestivalSimulation.ArtCarOf(state,player.Id)>=0,session.PresentedPoiSpinSides(player));
            if(player!=null)session?.LocalWorldCharacter?.SetEquippedItem(player.EquippedItemId);
            session?.LocalWorldCharacter?.ClearManualEquipment();
        }

        // DANCE-12: G pressed mid-dance (`key`; null otherwise) and held FestivalHudText.HoldToStopSeconds stops it,
        // once; the fill shows how far along. A key already down as it began, or the menu opening, starts the hold over.
        private void HoldToStop(InputAction key)
        {
            if(key==null||session.MenuOpen||!key.IsPressed())stopHeldSince=-1;
            else if(key.WasPressedThisFrame())stopHeldSince=Time.unscaledTime;
            float held=stopHeldSince<0?0:(Time.unscaledTime-stopHeldSince)/FestivalHudText.HoldToStopSeconds;
            if(held>=1){stopHeldSince=-1;held=0;session.Command("Cancel",session.LocalPlayer?.InteractionId);}
            ShowStopHold(held);
        }

        // F while playing: the tripper checks a vision by chatting when they have one to check within reach, and anyone beside the
        // VIP guard talks their way past the rope (FestivalHudText.CheckTarget); otherwise F talks with the festivalgoer beside you
        // (DIALOG-3: a talk on the chat window, the one way to chat). Beside someone busy it does nothing, as the HUD says (VISION-3).
        // PLAYA-5: on a plain art car ride, F plays a set on the car's deck; otherwise F does nothing while you're busy.
        private void ChatKey(RoundState state,PlayerState player)
        {
            if(FestivalSimulation.CanPlayDeckSet(state,player)){session.Command(FestivalSimulation.DeckSetKind);return;}
            if(state.Phase!="Playing"||player.Life!="Alive"||player.InteractionId!="")return;
            var check=FestivalHudText.CheckTarget(state,player);
            if(check!=null){if(!FestivalSimulation.Engaged(state,check))session.Command("ConfirmChat",check.Id);return;}
            var speaker=NearestTalker(state.Npcs,player.X,player.Z,2.7f);
            if(speaker!=null)session.Command("Conversation",speaker.Id);
        }

        // The campfire debrief: click a friend for each award while the vote is open (the session frees the pointer for it),
        // then the verdict plays out where the ballot was, from when this client first saw every vote in.
        private void UpdateReview(RoundState state,PlayerState player)
        {
            bool open=FestivalHudText.VoteOpen(state);
            if(!open&&revealRound!=state.RoundId){revealRound=state.RoundId;revealAt=Time.unscaledTime;}
            double revealSeconds=RevealSeconds(state);
            reviewText.text=FestivalHudText.ReviewHeadline(state,player);
            reviewReveal.text=FestivalHudText.ReviewReveal(state,player.Id,revealSeconds);
            reviewStatus.text=FestivalHudText.ReviewStatus(state,player.Id,revealSeconds,Bound(session?.Controls?.Slot1,"1"),Bound(session?.Controls?.Slot2,"2"),Bound(session?.Controls?.Slot3,"3"),Bound(session?.Controls?.Interact,"E"));
            ballotCrew.Clear();if(open)foreach(var p in state.Players)if(p.Connected&&ballotCrew.Count<ballotPicks.GetLength(1))ballotCrew.Add(p.Id);
            bool relayout=ballotColumns!=ballotCrew.Count;ballotColumns=ballotCrew.Count;
            for(int award=0;award<ballotLabels.Length;award++)
            {
                bool row=open&&award<state.ReviewAwards.Count;
                ballotLabels[award].gameObject.SetActive(row);if(row)ballotLabels[award].text=FestivalHudText.Ballot(state,player.Id,award);
                string pick=state.ReviewVotes.Find(v=>v.PlayerId==player.Id&&v.Award==award)?.TargetId;
                for(int column=0;column<ballotPicks.GetLength(1);column++)
                {
                    var button=ballotPicks[award,column];bool shown=row&&column<ballotCrew.Count;button.SetActive(shown);if(!shown)continue;
                    string friend=ballotCrew[column];
                    button.transform.Find("Text").GetComponent<Text>().text=FestivalHudText.CrewName(state,player.Id,friend);
                    button.GetComponent<Image>().color=friend==pick?Orange:new Color(.84f,.85f,.76f,1);
                    if(relayout){var cell=Rect(button);float width=.9f/ballotCrew.Count;cell.anchorMin=new Vector2(.05f+column*width+.004f,cell.anchorMin.y);cell.anchorMax=new Vector2(.05f+(column+1)*width-.004f,cell.anchorMax.y);}
                }
            }
        }
        // Seconds since this client saw the verdict come in; 0 while the vote is open.
        private double RevealSeconds(RoundState state)=>FestivalHudText.VoteOpen(state)||revealRound!=state.RoundId?0:Time.unscaledTime-revealAt;

        // DIALOG-5: the chat window follows the local player's chat check, VIP rope chat or talk (DIALOG-3), then takes the reply 1-3
        // picked this frame (a click picks one itself). In play its top stays under Night 2's lowest crew card (.660, a crew of 8) and
        // the notice it pushes down, its foot above the gear bar (.148). With the menu open it moves beside the pass, over the dimmer,
        // so the freed pointer can click a reply, unless the crew list holds that spot.
        private void OnEnable()
        {
            if(session==null)return;
            session.ConsumeMenuPress=ConsumeChatMenu;session.LocalCommandResultReceived-=EquipmentCommandResult;session.LocalCommandResultReceived+=EquipmentCommandResult;
            session.LocalCommandResultReceived-=FoundCash;session.LocalCommandResultReceived+=FoundCash;
        }
        private void OnDisable()
        {
            if(session?.Controls!=null){session.Controls.CancelHeldNotes();ReleaseRhythmInputs();}ResetRhythm();
            if(session!=null){if(session.ConsumeMenuPress==ConsumeChatMenu)session.ConsumeMenuPress=null;session.LocalCommandResultReceived-=EquipmentCommandResult;session.LocalCommandResultReceived-=FoundCash;}
            CancelGearHold();dropPressConsumed=session?.Controls?.DropPhysicallyPressed??false;stopHeldSince=-1;
            ClearPendingEquipment();sellerTalk.End();chatWindow?.ResetSession();session?.SpeechBubbles?.RetireCheck();talkLine.Reset();
            pendingComfortStart=null;if(comfortPanel!=null)comfortPanel.SetActive(false);
            settingsScreen?.Close();
        }
        private void ResetChatForSession()
        {
            if(chatConnectionGeneration==session.ConnectionGeneration)return;
            chatConnectionGeneration=session.ConnectionGeneration;sellerTalk.End();chatWindow?.ResetSession();talkLine.Reset();
        }
        private bool ConsumeChatMenu()
        {
            ResetChatForSession();
            var state=session.State;var player=session.LocalPlayer;
            if(state==null||player==null)return false;
            chatWindow.CloseKey=session.Controls.Label(session.Controls.Menu).ToUpperInvariant();
            if(sellerTalk.Visible){sellerTalk.End();session.MenuOpen=false;return true;}
            var live=FestivalChatWindow.LiveChat(state,player);
            if(live!=null&&!chatWindow.IsDismissed(state,live))
            {
                chatWindow.Follow(state,player,session.EstimatedSimulationSeconds,Time.unscaledTime,session.Profile.Data.ReducedMotion);
                chatWindow.Dismiss();session.Command("EndChat",live.Id);session.MenuOpen=false;return true;
            }
            var outcome=session.PendingConversationOutcome;
            if(outcome!=null){session.DismissConversationOutcome(outcome.DeliveryId);chatWindow.Dismiss();session.MenuOpen=false;return true;}
            if(chatWindow.Open){chatWindow.Dismiss();session.MenuOpen=false;return true;}
            if(!session.MenuOpen)crewOpen=state.Phase=="Playing";
            return false;
        }

        private void UpdateChat(RoundState state,PlayerState player,bool showMenu)
        {
            if(chatBesidePass!=showMenu){chatBesidePass=showMenu;Place(Rect(chatWindow.gameObject),showMenu?.735f:.60f,.155f,showMenu?.985f:.975f,.652f);}
            bool covered=showMenu&&crewOpen;
            // CAMP-1: at camp the window may be Marigold's talk instead, which the host knows nothing of; she faces whoever talks to her.
            chatWindow.CloseKey=Bound(session?.Controls?.Menu,"ESC");
            chatWindow.SetReplyKeys(Bound(session?.Controls?.Slot1,"1"),Bound(session?.Controls?.Slot2,"2"),Bound(session?.Controls?.Slot3,"3"));
            var outcome=session.PendingConversationOutcome;
            bool live=!covered&&(sellerTalk.Follow(state,player,Time.unscaledTime,session.Profile.Data.ReducedMotion)
                ||outcome==null&&chatWindow.Follow(state,player,session.EstimatedSimulationSeconds,Time.unscaledTime,session.Profile.Data.ReducedMotion));
            if(!covered&&outcome!=null)
            {
                bool leave=state.Phase!="Playing"||player.InteractionId!=""||!chatWindow.OutcomeWithinReach(outcome,state,player)&&player.Life!="Detained";
                chatWindow.FollowOutcome(outcome,Time.unscaledTime,session.Profile.Data.ReducedMotion,leave,FestivalHudText.MoneyText(state,player,outcome.Notice));
                if(chatWindow.OutcomeDisplayed)session.AcknowledgeConversationOutcome(outcome);
                if(!chatWindow.Open)session.DismissConversationOutcome(outcome.DeliveryId);
            }
            var shown=FestivalChatWindow.LiveChat(state,player);
            if(live&&chatWindow.Open&&shown!=null)session.AcknowledgeConversationTurn(shown);
            if(world!=null&&world.CampSeller!=null)sellerTalk.Face(world.CampSeller,player,Time.unscaledDeltaTime);
            if(covered)chatWindow.Close();
            if(chatReply>=0){if(live)chatWindow.Pick(chatReply);chatReply=-1;}
            var check=FestivalChatWindow.LiveChat(state,player);
            if(check?.Kind!="ConfirmChat"||chatWindow.IsDismissed(state,check))check=null;
            session.SpeechBubbles?.FollowCheck(state,player,check,session.ConnectionGeneration,session.EstimatedWorldSeconds);
            if(chatWindow.Open)dialoguePanel.SetActive(false);
            // 1-3 ask the chat's questions, or reply in a talk, while it runs (UpdateKeyboard), so the gear bar stops offering them to
            // equip. Held Drop drops nothing while either chat is up (HoldGearDrop's eligibility), so the bar stops offering that too.
            inventoryHeading.text=FestivalHudText.GearHeading(!(FestivalChatWindow.TakesReplies(state,player)&&!chatWindow.IsDismissed(state,FestivalChatWindow.LiveChat(state,player))||sellerTalk.Open),
                Bound(session?.Controls?.Slot1,"1"),Bound(session?.Controls?.Slot2,"2"),Bound(session?.Controls?.Slot3,"3"),Bound(session?.Controls?.Use,"Q"),Bound(session?.Controls?.Drop,"G"),!(chatWindow.Open||sellerTalk.Open));
        }

        private void UpdateRhythm(RoundState state,PlayerState player)
        {
            var interaction=state.Interactions.Find(i=>i.Id==player.InteractionId&&i.Status=="Active");
            TrackBuy(state,interaction,player);
            // DIALOG-3: a talk is on the chat window (UpdateChat), and on the lanes only for its trust dance.
            bool rhythm=FestivalInput.IsRhythm(interaction),riding=FestivalHudText.ShowsRide(interaction);
            rhythmPanel.SetActive(rhythm&&!riding);ridePanel.SetActive(riding&&!session.MenuOpen);
            // DANCE-2: every four-lane challenge shows the live dancer beside its lanes. MINI-2: a drummer sees the crew's accuracy there.
            bool showDancer=FestivalHudText.ShowsDancer(interaction,session.MenuOpen),drumming=rhythm&&interaction.Kind==FestivalSimulation.DrumPartKind&&!session.MenuOpen,beside=showDancer||drumming;
            rhythmShade.SetActive(rhythm&&!riding&&!session.MenuOpen);
            Place(rhythmPanel.GetComponent<RectTransform>(),beside ? .015f : .32f,beside ? .05f : .06f,beside ? .495f : .68f,beside ? .95f : .87f);
            dancePanel.SetActive(showDancer);
            drumPanel.SetActive(drumming);
            if(drumming){var (title,strip)=FestivalHudText.MinigameCard(state,player);drumTitle.text=title;drumStrip.text=strip;}
            if(showDancer)
            {
                dancerHeading.text=FestivalHudText.DancerHeading(interaction.Kind);dancerCaption.text=FestivalHudText.DancerCaption(interaction.Kind);
                dancePreview.Show(session.LocalWorldCharacter,session.ViewCamera,session.EstimatedSimulationSeconds-interaction.StartSeconds,interaction.BeatSeconds,session.Profile.Data.ReducedMotion);
            }
            else dancePreview.Hide();
            // DIALOG-6: a one-line chat stays until read, through a challenge starting (it takes the challenge's line until then) and
            // anything covering the card.
            var speech=talkLine.Follow(session.SpeechDeliveries,player,rhythm?interaction:null,session.MenuOpen||chatWindow.Open||mapPanel.activeSelf,Time.unscaledDeltaTime,state);
            bool chatter=speech!=null&&!talkLine.IsChallenge;
            bool showDialogue=rhythm&&speech!=null;
            dialoguePanel.SetActive(showDialogue);
            Place(dialoguePanel.GetComponent<RectTransform>(),beside ? .525f : .70f,beside ? .085f : .52f,beside ? .965f : .96f,beside ? .215f : .69f);
            dancerCaption.gameObject.SetActive(showDancer&&!showDialogue);
            if(speech!=null){dialogueSpeaker.text=speech.Speaker;dialogueLine.text=speech.Text;dialogueReveal.VisibleCharacters=speech.VisibleCharacters;}
            if(!rhythm||!session.enabled){ResetRhythm();rideChart=null;return;}
            if(riding){UpdateRide(interaction,player);return;}
            if(displayedChart==null||displayedInteractionId!=interaction.Id||displayedSeed!=interaction.ChartSeed||displayedNoteCount!=interaction.NoteCount||displayedPhrase!=interaction.Phrase||Math.Abs(displayedBeatSeconds-interaction.BeatSeconds)>.0001)
            {
                ShowChart(interaction,RhythmChart.For(interaction));
                rhythmDialogue.text=FestivalHudText.RhythmTitle(interaction.Kind);stopHoldLabel.text=FestivalHudText.HoldToStopFor(interaction.Kind,Bound(session?.Controls?.Drop,"G"));
                ShowRhythmKeys(session.Controls);
            }
            double now=session.EstimatedSimulationSeconds-interaction.StartSeconds;
            // The selected substance supplies both the original look-ahead and its dose travel twist.
            string effect=FestivalTrip.LaneEffect(player);int dose=FestivalTrip.LaneDose(player);
            double lead=Catalog.FindEffect(effect)?.LeadSeconds??2;
            ProcessRhythmFeedback(interaction,now);
            if(shownRhythmCombo!=rhythmComboCount){rhythmCombo.text=rhythmComboCount>=2?rhythmComboCount+" COMBO":"";shownRhythmCombo=rhythmComboCount;}
            bool feedbackVisible=Time.unscaledTime<rhythmJudgmentUntil;
            rhythmJudgment.text=now<0?(now<=-1?Math.Ceiling(-now).ToString("0"):"1"):feedbackVisible?lastRhythmJudgment:"";
            rhythmJudgment.color=now<0?Paper:lastRhythmJudgment=="PERFECT"?new Color(1,.80f,.35f):lastRhythmJudgment=="GOOD"?Mint:new Color(1,.45f,.58f);
            rhythmTiming.text=feedbackVisible&&(lastRhythmJudgment=="PERFECT"||lastRhythmJudgment=="GOOD")?(lastRhythmError<-.02?"EARLY":lastRhythmError>.02?"LATE":"ON BEAT"):"";
            float completed=Mathf.Clamp01((float)(now/displayedChart.DurationSeconds));
            var progressRect=rhythmProgressFill.rectTransform;progressRect.anchorMin=new Vector2(.08f,.055f);progressRect.anchorMax=new Vector2(.08f+.84f*completed,.071f);progressRect.offsetMin=progressRect.offsetMax=Vector2.zero;
            if(dialoguePanel.activeSelf&&!chatter&&speech.VisibleCharacters>=speech.CharacterCount)session.AcknowledgeDisplayedDialogue(interaction);
            DrawChart(now,lead,effect,session.Profile.Data.ReducedMotion,dose);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogLaneFlight(effect,dose,lead);
#endif
            for(int direction=0;direction<4;direction++)
            {
                bool pressed=rhythmFocused&&!session.MenuOpen&&!session.Controls.Rebinding&&!session.Controls.Objectives.IsPressed()&&session.Controls.Notes[direction].WasPressedThisFrame();
                if(pressed){rhythmFlashUntil[direction]=Time.unscaledTime+.16f;if(showDancer)dancePreview.Step(direction);session.Rhythm(direction,now);}
                SendRhythmRelease(interaction,direction,now);
                bool flashing=Time.unscaledTime<rhythmFlashUntil[direction];
                rhythmReceptors[direction].color=flashing?RhythmColors[direction]:new Color(.76f,.85f,.84f,.9f);
                rhythmReceptorWells[direction].color=flashing?new Color(.25f,.43f,.44f,1):new Color(.22f,.32f,.35f,1);
            }
        }

        // Each phrase starts in the middle lane, as the host replays it (DjRide.Replay). A lane change goes as a Rhythm input whose
        // Direction is the new lane.
        private void UpdateRide(InteractionState interaction,PlayerState player)
        {
            if(rideChart==null||rideId!=interaction.Id||ridePhrase!=interaction.Phrase)
            {
                rideId=interaction.Id;ridePhrase=interaction.Phrase;rideLane=DjRide.StartLane;rideChart=RhythmChart.For(interaction);
                rideControls.text=FestivalHudText.RideControls(session.Controls.NoteKey(0),session.Controls.NoteKey(3))+"   •   "+FestivalHudText.HoldToStopFor(interaction.Kind,Bound(session?.Controls?.Drop,"G"));
            }
            double now=session.EstimatedSimulationSeconds-interaction.StartSeconds;
            bool keys=now>=0&&rhythmFocused&&!session.MenuOpen&&!session.Controls.Rebinding&&!session.Controls.Objectives.IsPressed();
            int lane=FestivalDjRide.Steer(rideLane,keys&&session.Controls.Notes[0].WasPressedThisFrame(),keys&&session.Controls.Notes[3].WasPressedThisFrame());
            if(lane!=rideLane){rideLane=lane;session.Rhythm(lane,now);}
            string effect=FestivalTrip.LaneEffect(player);
            ride.Show(rideChart,now,rideLane,Catalog.FindEffect(effect)?.LeadSeconds??2);
            int combo=DjRide.Replay(rideChart,interaction.Inputs,interaction.GoodWindowSeconds,now).Combo;
            rideCombo.text=now<0?(now<=-1?Math.Ceiling(-now).ToString("0"):"1"):combo>=2?combo+" COMBO":"";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float nextLaneFlightDiagnostic;
        private void LogLaneFlight(string effect,int dose,double lead)
        {
            if(!DevelopmentDiagnostics.Visible||Time.unscaledTime<nextLaneFlightDiagnostic)return;
            foreach(var view in noteViews)if(view.gameObject.activeSelf)
            {
                nextLaneFlightDiagnostic=Time.unscaledTime+10;
                var anchor=view.rectTransform.anchorMin;
                DevelopmentDiagnostics.GraphicsEvent("Rhythm","lane_flight","effect="+(effect==""?"plain":effect)+" dose="+dose+" lead="+lead.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)
                    +" anchor_x="+anchor.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" anchor_y="+anchor.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));return;
            }
        }
#endif

        private void OnApplicationFocus(bool focused)
        {
            rhythmFocused=focused;settingsScreen?.FocusChanged(focused);
            if(!focused){CancelGearHold();dropPressConsumed=session?.Controls?.DropPhysicallyPressed??false;stopHeldSince=-1;}
            if(!focused&&session?.Controls!=null){session.Controls.CancelHeldNotes();ReleaseRhythmInputs();}
        }
        private void ReleaseRhythmInputs()
        {
            var player=session?.LocalPlayer;
            var interaction=player==null?null:session.State?.Interactions.Find(i=>i.Id==player.InteractionId&&i.Status=="Active");
            double now=interaction==null?0:session.EstimatedSimulationSeconds-interaction.StartSeconds;
            for(int lane=0;lane<4;lane++)SendRhythmRelease(interaction,lane,now);
        }
        private void SendRhythmRelease(InteractionState interaction,int lane,double rawSeconds)
        {
            if(session?.Controls==null||!session.Controls.TakeNoteRelease(lane))return;
            if(!session.enabled||!session.Connected||!FestivalInput.IsRhythm(interaction))return;
            var chart=displayedChart!=null&&displayedInteractionId==interaction.Id?displayedChart:RhythmChart.For(interaction);
            double judgment=FestivalSettings.JudgmentSeconds(rawSeconds,session.Profile.Data);
            if(!chart.Notes.Exists(note=>note.Direction==lane&&note.HoldSeconds>0&&judgment>=note.TimeSeconds-interaction.GoodWindowSeconds-1e-9&&judgment<=note.TimeSeconds+note.HoldSeconds+1e-9))return;
            session.Rhythm(lane,rawSeconds,true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("Rhythm","hold_release","lane="+lane+" chart_seconds="+judgment.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
#endif
        }
        private void ResetRhythm()
        {
            displayedChart=null;feedbackJudge=null;replayedRhythmInputs.Clear();displayedInteractionId="";processedRhythmInputs=0;rhythmComboCount=0;rhythmHitCount=0;lastRhythmJudgment="";rhythmJudgmentUntil=0;
            if(session?.Controls!=null)for(int lane=0;lane<4;lane++)session.Controls.TakeNoteRelease(lane);
            if(rhythmJudgment!=null)rhythmJudgment.text="";
            if(rhythmCombo!=null)rhythmCombo.text="";
            if(rhythmTiming!=null)rhythmTiming.text="";
            foreach(var head in noteViews)head.gameObject.SetActive(false);
            foreach(var tail in holdTails)tail.gameObject.SetActive(false);
        }

        // Readies the lane for a challenge's chart. DANCE-5: a beat can hold up to four notes, so the note pool grows (inside the
        // Notes container, keeping DANCE-7's draw order) until every note of the chart has a view, and the hit record is the chart's size.
        public void ShowChart(InteractionState interaction,RhythmChart chart)
        {
            displayedChart=chart;feedbackJudge=new RhythmJudge(chart,interaction.GoodWindowSeconds);replayedRhythmInputs.Clear();
            displayedInteractionId=interaction.Id;displayedSeed=interaction.ChartSeed;displayedNoteCount=interaction.NoteCount;displayedPhrase=interaction.Phrase;displayedBeatSeconds=interaction.BeatSeconds;
            while(noteViews.Count<chart.Notes.Count)AddNoteView();
            rhythmConsumed=new bool[chart.Notes.Count];rhythmHeld=new bool[chart.Notes.Count];rhythmDropped=new bool[chart.Notes.Count];processedRhythmInputs=0;rhythmComboCount=0;rhythmHitCount=0;lastRhythmJudgment="";rhythmJudgmentUntil=0;
            shownRhythmHits=shownRhythmBpm=shownRhythmCount=shownRhythmCombo=-1;
        }

        // Draws the displayed chart `now` seconds in: the step counter (hits out of the chart's notes, at its tempo) and each note
        // within `lead` seconds of its time, or just missed.
        public void DrawChart(double now,double lead,string effect,bool reducedMotion,int dose=0)
        {
            int bpm=Mathf.RoundToInt(60f/(float)displayedBeatSeconds),notes=displayedChart.Notes.Count;
            if(shownRhythmHits!=rhythmHitCount||shownRhythmCount!=notes||shownRhythmBpm!=bpm)
            {
                rhythmStatus.text=rhythmHitCount+" / "+notes+"   •   "+bpm+" BPM";
                shownRhythmHits=rhythmHitCount;shownRhythmCount=notes;shownRhythmBpm=bpm;
            }
            for(int i=0;i<noteViews.Count;i++)
            {
                var view=noteViews[i];var tail=holdTails[i];tail.gameObject.SetActive(false);
                if(i>=displayedChart.Notes.Count){view.gameObject.SetActive(false);continue;}
                var note=displayedChart.Notes[i];double remaining=note.TimeSeconds-now;
                bool held=rhythmHeld[i]&&now<note.TimeSeconds+note.HoldSeconds;
                bool visible=held||!rhythmConsumed[i]&&remaining<=lead&&remaining>=-MissOvershootSeconds;view.gameObject.SetActive(visible);
                if(!visible)continue;
                PlaceNote(view,note,held?0:remaining,lead,effect,reducedMotion,dose);
                if(rhythmDropped[i])view.color=new Color(.55f,.55f,.55f,1);
                if(note.HoldSeconds<=0)continue;
                float top=held?NoteHitY:NoteStartY+(NoteHitY-NoteStartY)*(float)(1-remaining/lead);
                float bottom=Mathf.Max(NoteStartY,NoteStartY+(NoteHitY-NoteStartY)*(float)(1-(remaining+note.HoldSeconds)/lead));
                tail.gameObject.SetActive(top>bottom);
                var color=rhythmDropped[i]?new Color(.55f,.55f,.55f):RhythmColors[note.Direction];color.a=.5f;tail.color=color;
                var rect=tail.rectTransform;float x=.185f+note.Direction*.21f;
                rect.anchorMin=new Vector2(x,bottom);rect.anchorMax=new Vector2(x,top);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(28,0);
            }
        }

        // One more hidden note view at the end of the Notes container, so it draws over the wells and under the judgment text.
        private void AddNoteView()
        {
            var note=Panel(noteContainer,"Note "+noteViews.Count,Color.white,Vector2.zero,Vector2.zero).GetComponent<Image>();
            note.sprite=arrowSprite;note.preserveAspect=true;note.raycastTarget=false;
            var noteEdge=note.gameObject.AddComponent<Outline>();noteEdge.effectColor=new Color(.005f,.018f,.025f,.94f);noteEdge.effectDistance=new Vector2(3,-3);
            var tail=Panel(tailContainer,"Tail "+holdTails.Count,Color.white,Vector2.zero,Vector2.zero).GetComponent<Image>();
            tail.raycastTarget=false;holdTails.Add(tail);tail.gameObject.SetActive(false);
            noteViews.Add(note);note.gameObject.SetActive(false);
        }

        // DANCE-12: the fill under "HOLD G TO STOP DANCING", `held` of the way to the stop; none at 0.
        // The legacy prompt fill shares this API, but chat input never starts a hold.
        public void ShowStopHold(float held)
        {
            stopHoldFill.gameObject.SetActive(held>0);walkHoldFill.gameObject.SetActive(held>0);
            if(held>0){Place(stopHoldFill.rectTransform,StopHoldLeft,.008f,StopHoldLeft+(StopHoldRight-StopHoldLeft)*Mathf.Min(held,1),.046f);Place(walkHoldFill.rectTransform,0,0,Mathf.Min(held,1),1);}
        }
        // Labels each receptor well with its lane's WASD key as bound now, so a rebound key shows its new name. Read once per challenge.
        public void ShowRhythmKeys(FestivalInput controls)
        {
            for(int lane=0;lane<4;lane++)rhythmKeys[lane].text=controls.NoteKey(lane);
            // The bottom line names the keys in W-A-S-D order (up, left, down, right): the arrows as one word while none is moved.
            string arrows="",alternates="";bool moved=false;
            foreach(int lane in new[]{2,0,1,3})
            {
                var arrow=controls.Notes[lane].bindings[0];moved|=arrow.effectivePath!=arrow.path;
                arrows+=controls.Notes[lane].GetBindingDisplayString(0);alternates+=controls.NoteKey(lane);
            }
            rhythmControls.text=((moved?arrows:"ARROWS")+" OR "+alternates).ToUpperInvariant();
        }

        // Places a note's view `remaining` seconds before its time, with `lead` seconds of look-ahead under the effect: it rises
        // from just above the step progress bar onto its lane's outline arrow, and a missed one fades out as it rises past it.
        public static void PlaceNote(Image view,RhythmNote note,double remaining,double lead,string effect,bool reducedMotion,int dose=0)
        {
            double progress=1-remaining/lead;
            var visual=EffectPresentation.Path(effect,note.Id,progress,reducedMotion,dose,note.Direction);
            var color=RhythmColors[note.Direction];color.a=Mathf.Clamp01(1+(float)(remaining/MissOvershootSeconds));view.color=color;
            var rect=view.rectTransform;float x=.185f+(note.Direction+(float)visual.Lane)*.21f;
            float rise=progress<=1?(float)(1-visual.Y):(float)progress;
            float y=NoteStartY+(NoteHitY-NoteStartY)*rise;
            rect.anchorMin=rect.anchorMax=new Vector2(x,y);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(96,96);
            rect.anchoredPosition=new Vector2((float)visual.X*32,0);rect.localRotation=Quaternion.Euler(0,0,RhythmAngles[note.Direction]+(float)visual.Rotation*.08f);
        }

        private void ProcessRhythmFeedback(InteractionState interaction,double now)
        {
            bool rollback=interaction.Inputs.Count<processedRhythmInputs;
            for(int n=0;!rollback&&n<processedRhythmInputs;n++)
            {
                var current=interaction.Inputs[n];var previous=replayedRhythmInputs[n];
                rollback=current.Direction!=previous.Direction||current.TimeSeconds!=previous.TimeSeconds||current.Release!=previous.Release;
            }
            if(rollback)ShowChart(interaction,displayedChart);
            while(processedRhythmInputs<interaction.Inputs.Count)
            {
                var input=interaction.Inputs[processedRhythmInputs++];
                replayedRhythmInputs.Add(new RhythmInput{Direction=input.Direction,TimeSeconds=input.TimeSeconds,Release=input.Release});
                if(input.Release)
                {
                    feedbackJudge.Release(input.Direction,input.TimeSeconds);
                    if(!feedbackJudge.LastDroppedNoteId.HasValue)continue;
                    int dropped=displayedChart.Notes.FindIndex(n=>n.Id==feedbackJudge.LastDroppedNoteId.Value);
                    if(dropped>=0)rhythmDropped[dropped]=true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("Rhythm","hold_drop","note="+feedbackJudge.LastDroppedNoteId.Value+" lane="+input.Direction);
#endif
                    rhythmComboCount=0;rhythmHitCount=Math.Max(0,rhythmHitCount-1);lastRhythmJudgment="LET GO";lastRhythmError=0;
                }
                else
                {
                    string verdict=feedbackJudge.Submit(input.Direction,input.TimeSeconds);
                    int closest=feedbackJudge.LastMatchedNoteId.HasValue?displayedChart.Notes.FindIndex(n=>n.Id==feedbackJudge.LastMatchedNoteId.Value):-1;
                    if(closest>=0)
                    {
                        rhythmConsumed[closest]=true;rhythmHeld[closest]=displayedChart.Notes[closest].HoldSeconds>0;rhythmComboCount++;rhythmHitCount++;
                        lastRhythmJudgment=verdict=="Perfect"?"PERFECT":"GOOD";lastRhythmError=feedbackJudge.LastErrorSeconds;
                    }
                    else{rhythmComboCount=0;lastRhythmJudgment="OFF BEAT";lastRhythmError=0;}
                }
                rhythmJudgmentUntil=Time.unscaledTime+.58f;
            }
            feedbackJudge.Advance(now-.75);
            // The same transport grace used by the simulation prevents a delayed snapshot
            // from turning a valid press into a premature visual miss.
            for(int n=0;n<displayedChart.Notes.Count;n++)
            {
                if(rhythmConsumed[n]||now<=displayedChart.Notes[n].TimeSeconds+interaction.GoodWindowSeconds+.75)continue;
                rhythmConsumed[n]=true;rhythmComboCount=0;lastRhythmJudgment="MISS";lastRhythmError=0;rhythmJudgmentUntil=Time.unscaledTime+.58f;
            }
        }

        private void UpdateMap(RoundState state,PlayerState player)
        {
            bool shown=session.Controls.Objectives.IsPressed()&&!session.MenuOpen;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            shown|=DevelopmentMapVisible&&!session.MenuOpen;
#endif
            mapShade.SetActive(shown);mapPanel.SetActive(shown);if(!shown)return;
            objectivePanel.SetActive(false);timerPanel.SetActive(false);vitalPanel.SetActive(false);rosterPanel.SetActive(false);inventoryPanel.SetActive(false);promptPanel.SetActive(false);nearbyPanel.SetActive(false);noticePanel.SetActive(false);heldDetailPanel.SetActive(false);checkoutPanel.SetActive(false);rhythmPanel.SetActive(false);dialoguePanel.SetActive(false);trippingPanel.SetActive(false);chatWindow.Close();
            bool camp=state.Phase=="Shopping"||state.Phase=="Spinning";campMap.SetActive(camp);festivalMap.SetActive(!camp);
            mapTitle.text=camp?"CAMPSITE  /  FIND YOUR WAY":"FESTIVAL GROUNDS  /  FIND YOUR WAY";mapHint.text="HOLD "+Bound(session?.Controls?.Objectives,"TAB")+" TO VIEW";
            float x=camp?Mathf.InverseLerp(-30,30,player.X):Mathf.InverseLerp(-35,35,player.X);
            float y=camp?Mathf.InverseLerp(-20,25,player.Z):Mathf.InverseLerp(-35,35,player.Z);
            Place(mapPlayerMarker,x-.012f,y-.018f,x+.012f,y+.018f);
            if(camp)
            {
                mapText.text="CAMP\n\nGEAR\nPick up one item. Pay the seller before you enter.\n\nTRAILHEAD\nPress "+Bound(session?.Controls?.Interact,"E")+" to ready. Your crew enters together.\n\nYOU ARE THE ORANGE DOT.";
                return;
            }
            string team="";foreach(var p in state.Players)team+=(p.Id==player.Id?"YOU":p.Name)+"  /  "+p.Life.ToUpperInvariant()+"\n";
            string mission=!Festivals.For(state).Night?FestivalHudText.Quota(state,player)+"\n"+FestivalHudText.QuotaHint(state,player):state.FriendFound?"FRIEND FOUND\nEscort them back to camp.":"FRIEND MISSING\nSearch the grounds. Their exact location is unknown.";
            mapText.text=mission+"\n\nCREW\n"+team+"\nYOU ARE THE ORANGE DOT.\n\nMEDIC  /  HOLDING\nHelp a downed or detained friend.";
        }

        private void ApplyEffects(PlayerState player)
        {
            if(player==null||player.Effects.Count==0){effectWash.color=Color.clear;if(session?.ViewCamera!=null)session.ViewCamera.fieldOfView=baseFov;return;}
            bool mushrooms=player.Effects.Exists(e=>e.Id=="mushrooms"),reduced=session.Profile.Data.ReducedMotion,shake=session.Profile.Data.CameraShake;
            effectWash.color=EffectWash(player.Effects,reduced,Time.unscaledTime);
            // TRIP-5: the dose's substance adds its own swell (Fun Guys) on top of a shop-bought one; this stays the one writer.
            // GAS-5: the gas kicks the view on the camp music's beat, this frame's, as FestivalTrip's overlay and sound do.
            var campAudio=session.GetComponent<FestivalCampAudio>();
            float trip=shake?FestivalTrip.For(session.State,player.Id,reduced,Time.unscaledTime,campAudio!=null?campAudio.Beat:null).Fov:0;
            if(session.ViewCamera!=null)session.ViewCamera.fieldOfView=(mushrooms&&!reduced&&shake?baseFov+Mathf.Sin(Time.unscaledTime*.8f)*1.2f:baseFov)+trip;
        }

        /// <summary>
        /// The screen wash for what the player took from the shop (the catalog's effects). The spinner's dose lasts the whole level
        /// (everyone's, on Night 2) and the debrief's shot 90 s: a constant wash under either swamped LIGHT-1's neon night, and the
        /// dose already shows as exposure (FestivalNightLighting).
        /// </summary>
        public static Color EffectWash(List<ActiveEffect> effects,bool reducedMotion,float time)
        {
            // GAS-1: Giggle Gas is a catalog effect with a look of its own (FestivalTrip), so it never washes the screen either.
            if(!effects.Exists(e=>e.Id!=FestivalSimulation.GiggleGasEffect&&Catalog.FindEffect(e.Id)!=null))return Color.clear;
            var color=effects.Exists(e=>e.Id=="lsd")?Color.HSVToRGB(Mathf.Repeat(time*.035f,1),.7f,1):new Color(.25f,.6f,.3f);
            return new Color(color.r,color.g,color.b,reducedMotion?.025f:.055f);
        }

        private static InteractionState ActiveConfirmation(RoundState state,PlayerState player)=>player==null?null:state?.Interactions.Find(i=>i.Id==player.InteractionId&&i.PlayerId==player.Id&&i.Status=="Active"&&i.Kind=="ConfirmChat");
        // `seen` is the interaction the player acted on. The host refuses the Cancel once that one has ended, so a joined view
        // that lags can't stop what replaced it. A chat check ends only when it is the one acted on, so a stale stop can't end it.
        private void FinishCurrentAction(string seen)
        {
            var check=ActiveConfirmation(session.State,session.LocalPlayer);
            if(check!=null&&check.Id==seen)session.Command("EndChat",check.Id);else session.Command("Cancel",seen);
        }
        private void UpdateActions(RoundState state,PlayerState player)
        {
            if(Time.unscaledTime<nextActionRefresh)return;
            nextActionRefresh=Time.unscaledTime+.2f;
            activeActionCount=0;primaryAction=null;nearby.text="";
            float y=.89f;
            if(player.CampVisitId!="")
            {
                var site=CampFeatures.Find(player.CampVisitId);
                string antic=site?.Kind=="Car"?"HONK / FIDDLE":site?.Kind=="Tent"?"SHADOW PUPPET":"MYSTERY FLUSH";
                if(state.Phase=="Shopping")AddHandoffActions(state,player,ref y);
                SetPromptAction("E  EXIT "+(site?.Kind??"CAMP SPACE").ToUpperInvariant()+"   •   "+Bound(session?.Controls?.Chat,"F")+" "+antic+"   •   "+Bound(session?.Controls?.Drop,"G")+" EXIT",()=>session.Command("ExitCamp"));
                FinishActions();return;
            }
            if(state.Phase=="CampReview")
            {
                // The debrief card says how to vote and who has; E opens the shop for the host once the verdict has played out. The
                // card's foot already reads "E  OPEN THE CAMP SHOP" (ReviewStatus), so the prompt panel stays empty and E stays bound.
                bool done=!FestivalHudText.VoteOpen(state)&&FestivalHudText.RevealDone(state,RevealSeconds(state));
                if(done&&session.IsHost)SetPromptAction("",()=>session.Command("FinishReview"));
                else SetPromptAction("",null);
                FinishActions();return;
            }
            if(state.Phase=="Results")
            {
                SetPromptAction(FestivalHudText.ResultsPrompt(state,player.Id,Bound(session?.Controls?.Menu,"ESC")),null);
                FinishActions();return;
            }
            // MINI-1: while a minigame runs the host takes only its own command, so its keys are all that is offered. MINI-4: in
            // Mystery Cups E drinks the cup under the aim dot as E is pressed, for the player who is up (F calls it, UpdateKeyboard).
            if(state.Phase==FestivalSimulation.MinigamePhase)
            {
                int cup=FestivalMinigameView.AimedCup(state,FestivalCharacter.ViewTransform);
                string play=FestivalHudText.MinigamePrompt(state,player,cup,Bound(session?.Controls?.Chat,"F"));
                SetPromptAction(play,cup<0||play==""?null:(Action)(()=>
                {
                    int aimed=FestivalMinigameView.AimedCup(session.State,FestivalCharacter.ViewTransform);
                    if(aimed>=0)session.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.DrinkCup,amount:aimed);
                }));
                FinishActions();return;
            }
            // HUD-4: while the wheels spin and the festival loads the host refuses every action, so none is offered.
            if(!FestivalSimulation.Interactive(state.Phase))
            {
                SetPromptAction("",null);
                FinishActions();return;
            }
            if(state.Phase=="Shopping")
            {
                AddHandoffActions(state,player,ref y);
                // The button sits on the DJ deck and the dust beside car_1's door, so only the aim picks a secret over them. E aims
                // again when pressed, so a prompt left from a look away sends nothing.
                string egg=AimedCampEgg(state,player);
                if(egg!="")
                {
                    SetPromptAction(FestivalCampEggView.Prompt(egg),()=>{if(AimedCampEgg(session.State,session.LocalPlayer)==egg)session.Command("CampEgg",egg);});
                    FinishActions();return;
                }
                if(Near(player,CampFeatures.DjX,CampFeatures.DjZ,3f))
                {
                    int next=(state.CampMusicTrack+1)%CampFeatures.Tracks.Length;
                    SetPromptAction("E  DJ: "+CampFeatures.Tracks[next],()=>session.Command("ChooseCampTrack",amount:next));
                    FinishActions();return;
                }
                // Beside the Giggle Balloons, E takes one. While two are on, the prompt only reminds, with nothing to press.
                string balloon=FestivalHudText.GiggleBalloonPrompt(player);
                if(balloon!="")
                {
                    SetPromptAction(balloon,balloon==FestivalHudText.TakeGiggleBalloon?()=>session.Command(FestivalSimulation.TakeGiggleBalloonKind):(Action)null);
                    FinishActions();return;
                }
                CampFeatures.Site nearestSite=null;float nearestDistance=3.5f;
                foreach(var site in CampFeatures.Sites)
                {
                    float distance=Distance(player.X,player.Z,site.X,site.Z);
                    if(distance<nearestDistance){nearestSite=site;nearestDistance=distance;}
                }
                if(nearestSite!=null)
                {
                    var chosen=nearestSite;
                    SetPromptAction("E  ENTER "+chosen.Kind.ToUpperInvariant(),()=>session.Command("EnterCamp",chosen.Id));
                    FinishActions();return;
                }
                if(player.HeldOfferId!=""&&Near(player,0,7))
                {
                    var held=Catalog.FindItem(player.HeldOfferId);
                    if(checkoutItem==held.Id)SetPromptAction("E  PAY "+FestivalHudText.Money(state,player,held.Price).ToUpperInvariant()+" FOR "+held.Name.ToUpperInvariant(),()=>{session.Command("Buy",item:held.Id);checkoutItem="";});
                    else SetPromptAction("E  SHOW "+held.Name.ToUpperInvariant()+" TO SELLER",()=>checkoutItem=held.Id);
                }
                else if(Near(player,0,19,3.2f))SetPromptAction(player.Ready?"E  UNREADY AT TRAILHEAD":"E  READY AT TRAILHEAD",()=>session.Command("Ready"));
                else if(player.HeldOfferId!="")SetPromptAction("UNPAID  "+Catalog.FindItem(player.HeldOfferId)?.Name.ToUpperInvariant()+"  •  SELLER AHEAD  •  "+Bound(session?.Controls?.Drop,"G")+" RETURN",null);
                else
                {
                    string itemId=FocusedOffer(state,player);
                    if(itemId!="")
                    {
                        var item=Catalog.FindItem(itemId);var stock=state.ShopStock.Find(s=>s.ItemId==itemId);
                        SetPromptAction(stock!=null&&stock.CampAvailable>0?"E  PICK UP "+item.Name.ToUpperInvariant()+"  •  "+FestivalHudText.Money(state,player,item.Price).ToUpperInvariant()+"  •  "+FestivalHudText.MoneyText(state,player,item.Description):item.Name.ToUpperInvariant()+"  •  SOLD OUT",stock!=null&&stock.CampAvailable>0?()=>session.Command("HoldOffer",item:itemId):null);
                    }
                    else SetPromptAction("GEAR SHELVES BESIDE SELLER  •  READY AT LIT TRAILHEAD",null);
                }
                if(CampSellerTalk.CanTalk(state,player)&&!sellerTalk.Open){nearby.text=CampSellerTalk.TalkPromptFor(Bound(session?.Controls?.Chat,"F"));UpdateNearbyLayout();}
                FinishActions();return;
            }
            // TRIP-8: only a free player shops a shelf. Lying there, E gets you up; mid-dance it runs nothing (DANCE-12).
            if(state.Phase=="Playing"&&player.Life=="Alive"&&player.InteractionId=="")
            {
                string focused=FocusedOffer(state,player);
                if(focused!="")
                {
                    var item=Catalog.FindItem(focused);var stock=state.ShopStock.Find(s=>s.ItemId==focused);
                    if(stock!=null&&stock.MarketAvailable>0)
                    {
                        if(checkoutItem==focused)SetPromptAction("E  PAY "+FestivalHudText.Money(state,player,item.Price).ToUpperInvariant()+" FOR "+item.Name.ToUpperInvariant(),()=>{session.Command("Buy",item:focused);checkoutItem="";});
                        else SetPromptAction("E  BUY "+item.Name.ToUpperInvariant()+"  •  "+FestivalHudText.Money(state,player,item.Price).ToUpperInvariant()+"  •  "+FestivalHudText.MoneyText(state,player,item.Description),()=>checkoutItem=focused);
                    }
                    else SetPromptAction(item.Name.ToUpperInvariant()+"  •  SOLD OUT",null);
                    // POLO-2: the VIP stall stands at the night market's shelves. E stays the shelf's; the wristband is still listed.
                    var shelf=primaryAction;AddTwistActions(state,player,ref y);primaryAction=shelf;
                    FinishActions();return;
                }
            }
            // TRIP-4: E runs the first action listed, so lying on the grass E gets you up before anything else.
            bool lying=FestivalSimulation.LyingDown(state,player.Id);
            string doing=player.InteractionId;
            if(lying)AddAction(FestivalHudText.GetUpAction,()=>session.Command("Cancel",doing),ref y);
            // E runs the first action listed. Ending the level comes before the carry prompts, so the leader standing beside
            // a body already home at the gate still ends Night 2 with E, and a carrier arriving there does too.
            if(state.Phase=="Playing"&&player.Life=="Alive"&&player.InteractionId==""&&FestivalSimulation.CanExtract(state,player))AddAction(FestivalHudText.ExtractAction(state),()=>session.Command("Extract"),ref y);
            if(state.Phase=="Playing")foreach(var body in state.Bodies)
            {
                string label=FestivalHudText.BodyAction(state,player,body),bodyId=body.PlayerId;
                if(label!="")AddAction(label,()=>session.Command("CarryBody",bodyId),ref y);
            }
            if(state.Phase=="Playing"&&(player.Life=="Downed"||player.Life=="Detained"))AddAction(player.Life=="Downed"?"Make a scene to distract attackers":"Distract security / work on escape",()=>session.Command("HelpSelf"),ref y);
            if(state.Phase=="Playing"&&state.TestMode&&player.Life=="Detained")AddAction(FestivalHudText.FreeMeAction,()=>session.Command(FestivalSimulation.TestFreeKind),ref y);
            if(!lying&&FestivalHudText.CanCancel(state,player))
            {
                // PLAYA-5: a plain art car ride lists its deck set beside the way off. Both read the view again when chosen, so a list
                // from just before a set began can neither stop it nor ask for another. The way off names the ride it listed, so the
                // host refuses it too when the view itself is behind.
                bool deck=FestivalSimulation.CanPlayDeckSet(state,player);
                AddAction(ActiveConfirmation(state,player)!=null?"Finish chat check":FestivalHudText.CancelAction(state,player,Bound(session?.Controls?.Chat,"F")),()=>{if(!deck||session.LocalPlayer?.InteractionId==doing)FinishCurrentAction(doing);},ref y);
                if(deck)foreach(var (label,command) in FestivalGuidance.TwistActions(state,player))
                    AddAction(label,()=>{if(FestivalSimulation.CanPlayDeckSet(session.State,session.LocalPlayer))session.Command(command.Kind);},ref y);
            }
            // ESC-2: security stopping you comes next, so E talks to them as the card says (TALK (E)), even with a festivalgoer nearer
            // or the stock you just ditched at your feet. Until the guard has walked into reach (the host's 2.5 m), E says so and does
            // nothing: what E would do instead, a sale or a buy beside a festivalgoer, is a deal in the guard's sight, an arrest.
            var stopper=player.InteractionId==""?FestivalHudText.StoppedBy(state,player):null;
            if(stopper!=null){if(Near(player,stopper.X,stopper.Z,2.5f))AddAction("Talk to security",()=>session.Command("Police",stopper.Id),ref y);else AddAction(FestivalHudText.StopApproachAction,()=>{},ref y);}
            var offer=state.Transfers.Find(t=>t.ToId==player.Id);if(offer!=null)AddAction("Accept "+ItemName(offer.ItemId)+" ×"+offer.Amount,()=>session.Command("AcceptTransfer",offer.Id),ref y);
            var outgoing=state.Transfers.Find(t=>t.FromId==player.Id);if(outgoing!=null)AddAction("Cancel handoff",()=>session.Command("CancelTransfer",outgoing.Id),ref y);
            // TRIP-8: in the middle of anything (lying on the grass, a talk, a check, a task) the host takes only its way out above, a
            // handoff or a call for help, so nothing else is listed; with no festivalgoer beside you the strip stays empty (ECON-5).
            NpcState npc=null;
            if(player.InteractionId=="")
            {
                var drop=Nearest(state.Drops,player.X,player.Z,2.5f);if(drop!=null)AddAction(FestivalHudText.PickupAction(state,player,drop),()=>PickUp(drop),ref y);
                var stash=Nearest(state.Stashes,player.X,player.Z,2.7f);
                if(stash!=null)
                {
                    string stashId=stash.Id;
                    if(player.Cash>=5)AddAction("Deposit "+FestivalHudText.Money(state,player,5)+" in shared stash",()=>session.Command("Deposit",stashId,amount:5),ref y);
                    if(state.StashCash>=5)AddAction("Withdraw "+FestivalHudText.Money(state,player,5)+" from shared stash",()=>session.Command("Withdraw",stashId,amount:5),ref y);
                    if(handGear.Count>0)
                    {
                        string item=handGear[Mathf.Clamp(selectedSlot,0,handGear.Count-1)].ItemId;
                        AddAction("Deposit "+ItemName(item),()=>session.Command("Deposit",stashId,item,1),ref y);
                    }
                    var stored=stash.Items.Find(i=>i.Count>0);
                    if(stored!=null){string item=stored.ItemId;AddAction("Withdraw "+ItemName(item),()=>session.Command("Withdraw",stashId,item,1),ref y);}
                }
                foreach(var mate in state.Players)
                {
                    // Before the reach check: a detained friend is held out of arm's reach, and the host's Release checks only the payer's spot.
                    if(mate.Id!=player.Id&&mate.Life=="Detained"&&Near(player,27,5))AddAction("Pay "+FestivalHudText.Money(state,player,10)+" release for "+mate.Name,()=>session.Command("BeginRelease",mate.Id,amount:1),ref y);
                    if(mate.Id==player.Id||!FestivalSimulation.CanHandoff(player,mate))continue;
                    if(mate.Life=="Downed"){AddAction("Rescue "+mate.Name,()=>session.Command("Rescue",mate.Id),ref y);AddAction("Drag "+mate.Name,()=>session.Command("Drag",mate.Id),ref y);}
                }
                NpcState check=null;
                if(player.Life=="Spirit")
                {
                    if(Near(player,24,-20)&&state.ConnectedCrewCount==1&&player.RevivalCount<2)AddAction("Self revive at medical (15 seconds)",()=>session.Command("BeginRevival",player.Id),ref y);
                    if(Near(player,24,-20))AddAction("Memorial chime",()=>session.Command("Chime"),ref y);
                }
                else if(state.Phase=="Playing"&&player.Life=="Alive")
                {
                    // Either lost friend (a big crew has two, CROWD-2): the one FindFriend goes to, from the same pick.
                    string lost=FestivalSimulation.FriendToFind(state,player);
                    if(lost!="")AddAction((lost=="friend"?state.FriendFound:state.SecondFriend.Found)?"Take over friend escort":"Recruit missing friend",()=>session.Command("FindFriend"),ref y);
                    if(Near(player,-28,16))
                    {
                        var detained=state.Players.Find(p=>p.Life=="Detained");
                        if(detained!=null)AddAction("Free release task for "+detained.Name,()=>session.Command("BeginRelease",detained.Id),ref y);
                        AddAction("Return lost property for "+FestivalHudText.Money(state,player,5),()=>session.Command("LostProperty"),ref y);
                    }
                    if(Near(player,24,-20))
                    {
                        var spirit=state.Players.Find(p=>p.Life=="Spirit"&&player.Wristbands.Contains(p.Id));
                        if(spirit!=null){AddAction("Revive "+spirit.Name+" — free task",()=>session.Command("BeginRevival",spirit.Id),ref y);AddAction("Revive "+spirit.Name+" — pay "+FestivalHudText.Money(state,player,10),()=>session.Command("BeginRevival",spirit.Id,amount:1),ref y);}
                        if(player.Effects.Exists(FestivalSimulation.Treatable)&&player.Inventory.Exists(i=>i.ItemId=="medical_voucher"))AddAction("Use medical voucher",()=>session.Command("Use",item:"medical_voucher"),ref y);
                    }
                    if(Near(player,Catalog.StageTakeoverX,Catalog.StageTakeoverZ,Catalog.StageTakeoverStartRange)&&player.Inventory.Exists(i=>i.ItemId=="stage_pass"))AddAction("Start DJ takeover",()=>session.Command("Dj"),ref y);
                    // The tripper beside someone they have an unchecked vision about: E checks by dancing, F (ChatKey) by chatting.
                    // Anyone else beside Palm Mirage's VIP guard: E or F talks them past the rope (POLO-1).
                    check=FestivalHudText.CheckTarget(state,player);
                    if(check!=null)
                    {
                        string checkId=check.Id;
                        // VISION-3: someone the host won't let anyone check right now is said to be busy; E does nothing.
                        if(FestivalSimulation.Engaged(state,check))AddAction(FestivalHudText.BusyAction,()=>{},ref y);
                        else if(FestivalSimulation.CanCheckVision(state,player,check))
                        {
                            AddAction(FestivalHudText.CheckDanceActionFor(Bound(session?.Controls?.Chat,"F")),()=>session.Command("ConfirmDance",checkId),ref y);
                            AddAction(FestivalHudText.CheckChatAction,()=>session.Command("ConfirmChat",checkId),ref y);
                        }
                        else AddAction(FestivalHudText.RopeChatAction,()=>session.Command("ConfirmChat",checkId),ref y);
                    }
                    // The festival's twists where they stand: the Ferris wheel, the VIP stall, a passing art car.
                    AddTwistActions(state,player,ref y);
                }
                npc=state.Phase=="Playing"&&player.Life=="Alive"?Nearest(state.Npcs,player.X,player.Z,2.5f):null;
                if(npc!=null)
                {
                    if(npc.Kind=="Cop"){if(npc!=stopper)AddAction("Talk to security",()=>session.Command("Police",npc.Id),ref y);}
                    else foreach(var (label,command) in FestivalHudText.FestivalgoerActions(state,player,npc,soldToYou.Contains(state.RoundId+"/"+npc.Id),Bound(session?.Controls?.Chat,"F")))AddAction(label,()=>session.Command(command.Kind,command.TargetId,command.ItemId,command.Amount),ref y);
                }
                // Offering your gear to a friend in reach follows the errands above, the checks and the festivalgoer beside you: the sober
                // crew sticks by the tripper, so a friend is often in reach, and E must not hand them your gear (your stock, ECON-3)
                // instead of checking or selling.
                if(handGear.Count>0)
                {
                    string item=handGear[Mathf.Clamp(selectedSlot,0,handGear.Count-1)].ItemId;
                    foreach(var mate in state.Players)if(mate.Id!=player.Id&&FestivalSimulation.CanHandoff(player,mate))AddAction("Offer "+ItemName(item)+" to "+mate.Name,()=>session.Command("Transfer",mate.Id,item,1),ref y);
                }
                // TRIP-4: lying down comes last of all, after the festivalgoer beside you, so E never lies you down instead of dancing,
                // chatting or selling.
                if(FestivalSimulation.CanLieDown(state,player))AddAction(FestivalHudText.LieDownAction,()=>session.Command(FestivalSimulation.LieDownKind),ref y);
            }
            // DANCE-12: mid-dance E runs nothing, not even the cancel listed first, so a stray E can't stop it or start a buy, a
            // dance or a gear offer from the list; the lane's bottom line says to hold G (UpdateKeyboard). The list stays clickable.
            // Mid-talk E also runs nothing; the prompt names the effective Menu binding to walk away.
            bool talking=FestivalHudText.WalksOutByHold(state,player);
            if(FestivalHudText.StopsByHold(state,player)||talking)primaryAction=null;
            // POLO-2: up on the Ferris wheel nothing is worth E; the prompt counts the turn down instead.
            string riding=FestivalHudText.WheelPrompt(state,player,session.EstimatedSimulationSeconds);
            if(riding!=""){prompt.text=riding;primaryAction=null;}
            else if(talking)prompt.text=Bound(session?.Controls?.Menu,"ESC")+" TO WALK AWAY";
            else prompt.text=primaryAction==null?"":dynamicActions.Find(g=>g.name.StartsWith("Action:"))?.name.Substring(7);
            // ECON-3: beside a festivalgoer or a cop, the strip lists what else there is, each a click away in the Esc menu's list.
            nearbyLabels.Clear();
            if(npc!=null&&primaryAction!=null)for(int i=0;i<activeActionCount;i++)nearbyLabels.Add(dynamicActions[i].name.Substring(7));
            nearby.text=MoreActions(nearbyLabels,Bound(session?.Controls?.Menu,"ESC"));UpdateNearbyLayout();
            FinishActions();
        }
        private void AddTwistActions(RoundState state,PlayerState player,ref float y){foreach(var (label,command) in FestivalGuidance.TwistActions(state,player))AddAction(label,()=>session.Command(command.Kind,command.TargetId,command.ItemId),ref y);}

        private void AddHandoffActions(RoundState state,PlayerState player,ref float y)
        {
            var incoming=state.Transfers.Find(t=>t.ToId==player.Id);
            if(incoming!=null)AddAction("Accept "+ItemName(incoming.ItemId)+" ×"+incoming.Amount,()=>session.Command("AcceptTransfer",incoming.Id),ref y);
            var outgoing=state.Transfers.Find(t=>t.FromId==player.Id);
            if(outgoing!=null)AddAction("Cancel handoff",()=>session.Command("CancelTransfer",outgoing.Id),ref y);
            if(outgoing!=null||handGear.Count==0)return;
            string item=handGear[Mathf.Clamp(selectedSlot,0,handGear.Count-1)].ItemId;
            foreach(var mate in state.Players)
            {
                if(mate.Id==player.Id||!mate.Connected||mate.Life!="Alive"||!FestivalSimulation.CanHandoff(player,mate))continue;
                var recipient=mate;
                AddAction("Offer "+ItemName(item)+" to "+recipient.Name,()=>session.Command("Transfer",recipient.Id,item,1),ref y);
            }
        }

        private void AddAction(string label,Action action,ref float y)
        {
            // K1: a click on the Esc menu's list is a choice made, so the menu shuts on it: a dance, sale, buy or talk it starts takes
            // your keys, which the open menu would drop, and its pass no longer covers the lanes offering only CANCEL ACTION. E runs
            // the bare action (the menu is shut whenever E acts).
            Action chosen=()=>{action();session.MenuOpen=false;};
            GameObject item;
            if(activeActionCount<dynamicActions.Count)
            {
                item=dynamicActions[activeActionCount];item.SetActive(true);
                item.name="Action:"+label;
                item.transform.Find("Text").GetComponent<Text>().text=label;
                var control=item.GetComponent<Button>();control.onClick.RemoveAllListeners();control.onClick.AddListener(()=>chosen());
            }
            else
            {
                item=Button(actionsContent,label,Vector2.zero,Vector2.one,chosen);item.name="Action:"+label;
                item.AddComponent<LayoutElement>().preferredHeight=64;
                // PLAYA-2: Ember Playa's odd-object money and a long crew name shrink to fit the button.
                ShrinkToFit(item.transform.Find("Text").GetComponent<Text>(),14);
                dynamicActions.Add(item);
            }
            activeActionCount++;
            if(primaryAction==null)primaryAction=action;y-=.09f;
        }
        // An action's key as bound, for a prompt; fallback (today's letter) with no controls, as in EditMode. Cached while its bound
        // path and the keyboard layout (FestivalInput.LayoutChanges) stay the same, because GetBindingDisplayString allocates and the
        // HUD asks every frame.
        // ponytail: keyed on binding 0, every prompt action's only binding.
        private string Bound(InputAction action,string fallback)
        {
            if(action==null)return fallback;
            string path=action.bindings[0].effectivePath;int layout=session.Controls.LayoutChanges;
            if(!keyLabels.TryGetValue(action,out var key)||key.Path!=path||key.Layout!=layout)keyLabels[action]=key=(path,layout,session.Controls.Label(action).ToUpperInvariant());
            return key.Label;
        }
        // Players read an item's display name, never its internal id (THEME-2: "Tongue Stamps", not "stock_lsd").
        private static string ItemName(string id)=>Catalog.FindItem(id)?.Name??id;
        /// <summary>ECON-3: whether Q (Use) on item, pressed at `now` (the HUD's clock), takes it. Using stock is taking it yourself (the
        /// unit gone and a minute's trip), and a new player presses Q beside a buyer to "use" their stock on them. So on stock Q takes it
        /// only when the Q before, at armedAt, asked about that same stock (armed) no more than TakeStockSeconds ago; otherwise this Q
        /// asks, and the HUD says TakeStockAgain. Every other item's Q is unchanged.</summary>
        public static bool TakesOnQ(string item,string armed,float armedAt,float now)=>Catalog.FindItem(item)?.Category!="Stock"||item==armed&&now-armedAt<=TakeStockSeconds;
        public const float TakeStockSeconds=3;
        public static readonly string TakeStockAgain=TakeStockAgainFor("Q");
        public static string TakeStockAgainFor(string use)=>use+" again to take it yourself (it's stock for selling)";
        /// <summary>ECON-3 (Matt: "there is no button to actually sell to people"): the strip under the prompt beside a festivalgoer or a
        /// cop. The prompt is the first action listed, on E; the strip lists the rest, each marked ESC, the key that opens the menu on
        /// that list to click one. "" with nothing else to do there.</summary>
        public static string MoreActions(List<string> actions,string menu="ESC")
        {
            int extras=actions.Count-1,shown=extras>5?4:extras;string lines="";
            for(int i=1;i<=shown;i++)lines+=(i>1?"\n":"")+menu+"  "+actions[i];
            if(extras>5)lines+="\n"+menu+"  +"+(extras-shown)+" more";
            return lines;
        }
        private void FinishActions(){for(int i=activeActionCount;i<dynamicActions.Count;i++)dynamicActions[i].SetActive(false);}
        private void SetPromptAction(string label,Action action){prompt.text=label.StartsWith("E  ")?label.Substring(3):label;primaryAction=action;}
        private string FocusedOffer(RoundState state,PlayerState player)
        {
            if(state==null||player==null||session.ViewCamera==null||!(state.Phase=="Shopping"||state.Phase=="Playing"))return "";
            bool camp=state.Phase=="Shopping";
            // CAM-1: along the aim dot's line from level with your eye, so over the shoulder the dot picks the shelf item it is on.
            var view=session.ViewCamera.transform;Vector3 forward=view.forward,origin=FestivalThirdPerson.AimFrom(view.position,forward,session.EyePosition);
            string best="";float bestScore=.77f;
            for(int i=0;i<state.VendorOffers.Count;i++)
            {
                if(!camp&&Catalog.StarterStock(state.VendorOffers[i]))continue;
                var point=Catalog.ShopPoint(camp,i);
                if(Distance(player.X,player.Z,point.X,point.Z)>3.3f)continue;
                var target=new Vector3(point.X,i<4?1.17f:2.05f,camp?9.0f:i<4?-21.15f:-20.05f);
                var delta=target-origin;float score=Vector3.Dot(forward,delta.normalized);
                if(score>bestScore){bestScore=score;best=state.VendorOffers[i];}
            }
            return best;
        }
        private string AimedCampEgg(RoundState state,PlayerState player)
        {
            if(session.ViewCamera==null)return "";
            var view=session.ViewCamera.transform;Vector3 forward=view.forward;
            string egg=FestivalCampEggView.Aimed(state,player,FestivalThirdPerson.AimFrom(view.position,forward,session.EyePosition),forward);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible&&egg!=tracedCampEgg)DevelopmentDiagnostics.GraphicsEvent("CampEggs","prompt_target","egg="+(egg==""?"none":egg));
            tracedCampEgg=egg;
#endif
            return egg;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string tracedCampEgg="";
#endif
        // SET-1: only the open tab's rows show, each reading the saved choice; a row another choice overrides cannot be pressed.
        private void UpdateSettingsScreen()
        {
            if(settingsScreen==null)return;
            // BACK, Esc, RESUME, a drop or the pass closing all hide the screen, and a rebind or test ends with it.
            if(!settingsOpen||!menuPanel.activeSelf){settingsScreen.Close();return;}
            settingsScreen.Update();
            foreach(var tab in settingsTabs)
            {
                bool open=tab.Tab==settingsScreen.Shown;
                tab.Fill.color=open?new Color(.065f,.37f,.32f,1):new Color(.84f,.85f,.76f,1);tab.Label.color=open?Paper:Ink;
            }
            foreach(var section in settingsSections)
            {
                bool open=section.Section==settingsScreen.Section;
                section.Button.SetActive(settingsScreen.Shown==FestivalSettingsScreen.Tab.Controls);
                section.Fill.color=open?new Color(.065f,.37f,.32f,1):new Color(.84f,.85f,.76f,1);section.Label.color=open?Paper:Ink;
            }
            foreach(var row in settingsRows)
            {
                bool shown=settingsScreen.Shows(row.Row);
                foreach(var part in row.Parts)part.SetActive(shown);
                if(!shown)continue;
                row.Value.text=settingsScreen.Value(row.Row);
                bool usable=settingsScreen.Usable(row.Row);
                for(int i=0;i<row.Buttons.Length;i++){row.Buttons[i].interactable=usable;row.Buttons[i].GetComponentInChildren<Text>().text=settingsScreen.ButtonLabel(row.Row,i);}
            }
            if(settingsMeter!=null)settingsMeter.anchorMax=new Vector2(Mathf.Lerp(.44f,.665f,settingsScreen.MicrophoneLevel),settingsMeter.anchorMax.y);
            settingsText.text=settingsScreen.Message;settingsText.color=settingsScreen.Failed?new Color(.58f,.16f,.12f):new Color(.32f,.40f,.36f);
            settingsStatus.text=settingsScreen.Status;
        }
        private void UpdatePurchaseFeedback(RoundState state,PlayerState player)
        {
            FestivalSettings.ApplyEffects(shopAudio,session.Profile?.Data);
            int units=0;foreach(var item in player.Inventory)units+=item.Count;
            if(previousRound==state.RoundId&&previousCash>=0&&player.Cash<previousCash&&units>previousUnits)
            {
                shopAudio.PlayOneShot(shopChime);
                // POI-4: the purchase reply teaches players how to spin their poi; the chime must leave that reply visible.
                if(!FestivalSimulation.IsPoi(player.VisualReceivedItem))session.SetMessage("Oh, hey, thanks!  •  POCKET UPDATED");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Festival.Hud] Purchase notice: "+session.Message);
#endif
            }
            previousRound=state.RoundId;previousCash=player.Cash;previousUnits=units;
        }
        // ECON-2: a buy from a festivalgoer holds the price as its dance starts and hands the unit over as it ends, so the check above
        // never sees both at once; the buy's own reply comes when it ends (FestivalHudText.BuyReply), read in your money by Update.
        // It reads the held price, not your stack, which a handoff (taken mid-buy) can move either way.
        private void TrackBuy(RoundState state,InteractionState active,PlayerState player)
        {
            if(active!=null&&active.Kind=="Buy")
            {
                if(active.Id!=buyInteractionId){buyInteractionId=active.Id;buyItem=FestivalSimulation.SellerStock(active.TargetId);buySeller=state.RoundId+"/"+active.TargetId;}
                cashWhileBuying=player.Cash;return;
            }
            if(buyInteractionId=="")return;
            buyInteractionId="";
            string reply=FestivalHudText.BuyReply(buyItem,cashWhileBuying,player.Cash);
            if(reply!=""){session.SetMessage(reply);soldToYou.Add(buySeller);}
        }
        // Clearing lastNotice shows a second sale's notice even when it reads as the first's did.
        private void TrackSale(RoundState state,PlayerState player)
        {
            var sale=saleNews.Follow(state,player,session?.ConnectionGeneration??0);if(sale==null)return;
            session?.SetMessage(sale.Notice);lastNotice="";saleStampAt=Time.unscaledTime;saleFlash=sale.Result.StartsWith("SOLD",StringComparison.Ordinal);
            saleStamp.text=FestivalHudText.MoneyText(state,player,sale.Result).ToUpperInvariant();
        }
        // ECON-8: a random find is announced only once the host gives it to you, so whoever lost the race to it hears nothing.
        private void PickUp(DropState drop)
        {
            string issued=session.SendCommand("Pickup",drop.Id);
            if(!drop.CashFind)return;
            foundCommand=issued;foundAmount=drop.Count;
            FoundCash(session.LastLocalCommandResult); // A host reply can arrive before SendCommand returns.
        }
        private void FoundCash(CommandResult result)
        {
            if(foundCommand==""||result==null||result.Id!=foundCommand)return;
            foundCommand="";
            if(result.Accepted){session.SetMessage(FestivalHudText.FoundCashNotice(foundAmount));lastNotice="";}
        }
        private void ApplyContrast(bool high)
        {
            if(appliedContrast==high)return;appliedContrast=high;
            promptPanel.GetComponent<Image>().color=high?new Color(.01f,.025f,.03f,1):new Color(.025f,.064f,.071f,.95f);
            rosterPanel.GetComponent<Image>().color=high?new Color(.01f,.025f,.03f,1):new Color(.025f,.064f,.071f,.95f);
            reticle.GetComponent<Image>().color=high?new Color(1,.95f,.71f,1):new Color(.88f,.90f,.74f,.65f);
        }
        private void OnDestroy(){if(session!=null){if(session.ConsumeMenuPress==ConsumeChatMenu)session.ConsumeMenuPress=null;session.LocalCommandResultReceived-=EquipmentCommandResult;session.LocalCommandResultReceived-=FoundCash;}ClearPendingEquipment();if(shopChime!=null)Destroy(shopChime);if(ohYeah!=null)Destroy(ohYeah);if(discSprite!=null)Destroy(discSprite);if(discTexture!=null)Destroy(discTexture);if(roundedSprite!=null)Destroy(roundedSprite);if(roundedTexture!=null)Destroy(roundedTexture);if(arrowSprite!=null)Destroy(arrowSprite);if(arrowTexture!=null)Destroy(arrowTexture);if(arrowOutlineSprite!=null)Destroy(arrowOutlineSprite);if(arrowOutlineTexture!=null)Destroy(arrowOutlineTexture);}

        private void StartHost(){if(!ushort.TryParse(hostPortField.text,out var port)||port==0){connectionError="Port must be 1–65535.";return;}connectionError="";string name=nameField.text;WaitForComfort(()=>{Remember(null);session.Host(name,port);});}
        private void StartJoin(){if(!ushort.TryParse(portField.text,out var port)||port==0){connectionError="Port must be 1–65535.";return;}connectionError="";string name=nameField.text,address=addressField.text;WaitForComfort(()=>{Remember(address);session.Join(name,address,port);});}
        // MENU-1: until a game connects the session moves no camera, so the title drifts it over the campsite (FestivalTitleScreen.Shot).
        private void FrameTitle()
        {
            if(session.ViewCamera==null)return;
            FestivalTitleScreen.Shot(Time.unscaledTime,session.Profile.Data.ReducedMotion,out var at,out var turn);
            session.ViewCamera.transform.SetPositionAndRotation(at,turn);session.ViewCamera.fieldOfView=FestivalTitleScreen.Fov;
        }
        // MENU-1: PLAY fills in what was last used here (Start): your name, and the host you last joined.
        // ponytail: a failed save only costs that prefill, so it isn't reported over the connection's own status.
        private void Remember(string host){session.Profile.Data.PlayerName=nameField.text;if(host!=null)session.Profile.Data.LastHost=host;session.Profile.Save();}
        private void Start(){if(session==null)return;nameField.text=session.Profile.Data.PlayerName;addressField.text=session.Profile.Data.LastHost;}
        // The ballot's keyboard path beside clicking: each press of an award's key moves your pick to the next connected friend
        // (starting after you). The vote that completes the crew's ballot is final.
        private static string NextReviewPick(RoundState state,PlayerState player,int award)
        {
            var crew=state.Players.FindAll(p=>p.Connected);
            var mine=state.ReviewVotes.Find(v=>v.PlayerId==player.Id&&v.Award==award);
            int current=crew.FindIndex(p=>p.Id==(mine?.TargetId??player.Id));
            return crew[(current+1)%crew.Count].Id;
        }
        private static float Distance(float x,float z,float xx,float zz)=>Vector2.Distance(new Vector2(x,z),new Vector2(xx,zz));
        private static bool Near(PlayerState p,float x,float z,float range=2.7f)=>Distance(p.X,p.Z,x,z)<=range;
        private static DropState Nearest(List<DropState> values,float x,float z,float max){DropState best=null;float d=max;foreach(var v in values){float n=Distance(x,z,v.X,v.Z);if(n<d){d=n;best=v;}}return best;}
        private static NpcState Nearest(List<NpcState> values,float x,float z,float max){NpcState best=null;float d=max;foreach(var v in values){float n=Distance(x,z,v.X,v.Z);if(n<d){d=n;best=v;}}return best;}
        private static NpcState NearestTalker(List<NpcState> values,float x,float z,float max){NpcState best=null;float d=max;foreach(var v in values){if(!v.CanTalk||v.Kind!="Wook")continue;float n=Distance(x,z,v.X,v.Z);if(n<d){d=n;best=v;}}return best;}
        private static StashState Nearest(List<StashState> values,float x,float z,float max){StashState best=null;float d=max;foreach(var v in values){float n=Distance(x,z,v.X,v.Z);if(n<d){d=n;best=v;}}return best;}
        private static void Clear(List<GameObject> values){foreach(var go in values)if(go!=null)Destroy(go);values.Clear();}

        private GameObject Card(Transform parent,string name,Vector2 min,Vector2 max,bool dark)
        {
            var go=Panel(parent,name,dark?new Color(.025f,.064f,.071f,1):Paper,min,max);
            Round(go);
            var shadow=go.AddComponent<Shadow>();shadow.effectColor=new Color(0,.015f,.02f,dark?.24f:.46f);shadow.effectDistance=new Vector2(0,-8);
            shadow.useGraphicAlpha=true;
            return go;
        }
        private void Accent(Transform parent,Color color)
        {
            var bar=Panel(parent,"Accent rule",color,new Vector2(.025f,.974f),new Vector2(.975f,.982f));
            bar.GetComponent<Image>().raycastTarget=false;
        }
        private GameObject Rule(Transform parent,float y)
        {
            var go=Panel(parent,"Divider",Hairline,new Vector2(.04f,y),new Vector2(.96f,y+.003f));
            go.GetComponent<Image>().raycastTarget=false;
            return go;
        }
        private GameObject Keycap(Transform parent,string character,Vector2 min,Vector2 max)
        {
            var go=Card(parent,character+" key",min,max,false);
            go.GetComponent<Image>().color=Orange;
            var label=Label(go.transform,"Key",23,TextAnchor.MiddleCenter);label.text=character;label.fontStyle=FontStyle.Normal;label.color=Ink;Fill(label.rectTransform,2);
            return go;
        }
        private GameObject Panel(Transform parent,string name,Color color,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);var rect=Rect(go);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return go;
        }
        private void Round(GameObject go)
        {
            var image=go.GetComponent<Image>();image.sprite=roundedSprite;image.type=Image.Type.Sliced;
        }
        private Text Label(Transform parent,string name,int size,TextAnchor anchor)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=font;text.fontSize=size<=25?Mathf.RoundToInt(size*1.18f):size;text.alignment=anchor;text.color=Paper;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;authoredLabels.Add(text);return text;
        }
        // Text that would run past its box shrinks, down to min px, instead of being cut.
        private static void ShrinkToFit(Text text,int min){text.resizeTextForBestFit=true;text.resizeTextMinSize=min;text.resizeTextMaxSize=text.fontSize;}
        private GameObject Button(Transform parent,string label,Vector2 min,Vector2 max,Action action)
        {
            bool primary=label=="RESUME FESTIVAL"||label=="PLAY"||label=="HOST A GAME"||label=="JOIN A FRIEND"||label=="JOIN"||label=="NEXT CAMP";
            bool danger=label=="LEAVE SESSION"||label=="QUIT";
            var go=Card(parent,label,min,max,primary);
            var graphic=go.GetComponent<Image>();graphic.color=primary?new Color(.065f,.37f,.32f,1):danger?new Color(.91f,.85f,.78f,1):new Color(.84f,.85f,.76f,1);
            var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(()=>action());
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=primary?new Color(1.26f,1.15f,1.10f,1):new Color(1.11f,1.13f,1.10f,1);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.78f,.84f,.79f,1);colors.disabledColor=new Color(.55f,.58f,.55f,.65f);colors.fadeDuration=.12f;button.colors=colors;
            var outline=go.AddComponent<Outline>();outline.effectColor=primary?new Color(.55f,.95f,.82f,.32f):new Color(.08f,.22f,.19f,.16f);outline.effectDistance=new Vector2(1,-1);
            go.AddComponent<FestivalFocusFrame>().Configure(outline,new Color(.13f,.55f,.46f,1),outline.effectColor);
            var text=Label(go.transform,"Text",label=="+"||label=="−"?32:25,TextAnchor.MiddleCenter);text.fontStyle=FontStyle.Normal;text.color=primary?Paper:danger?new Color(.42f,.12f,.11f):Ink;text.text=label;Fill(text.rectTransform,4);return go;
        }
        private void Disc(Transform parent,string name,Vector2 min,Vector2 max,Color color)
        {
            var go=Panel(parent,name,color,min,max);var image=go.GetComponent<Image>();image.sprite=discSprite;image.preserveAspect=true;image.raycastTarget=false;
        }
        private InputField Field(Transform parent,string label,string value,float top)
        {
            var heading=Label(parent,label+" label",18,TextAnchor.MiddleLeft);heading.text=label;heading.fontStyle=FontStyle.Normal;heading.color=new Color(.31f,.41f,.36f);Place(heading.rectTransform,.08f,top,.92f,top+.05f);
            var fieldObject=Card(parent,label,new Vector2(.08f,top-.095f),new Vector2(.92f,top),false);fieldObject.GetComponent<Image>().color=new Color(.99f,.985f,.94f,1);var input=fieldObject.AddComponent<InputField>();
            var focus=fieldObject.AddComponent<Outline>();focus.effectColor=new Color(.08f,.22f,.19f,.16f);focus.effectDistance=new Vector2(1,-1);
            fieldObject.AddComponent<FestivalFocusFrame>().Configure(focus,new Color(.13f,.55f,.46f,1),focus.effectColor);
            // MENU-1: a thin top and bottom inset, so a field as short as the title card's still shows its line.
            var text=Label(fieldObject.transform,"Text",24,TextAnchor.MiddleLeft);SetRect(text.rectTransform,Vector2.zero,Vector2.one,new Vector2(18,2),new Vector2(-18,-2));text.color=Ink;text.text=value;input.textComponent=text;input.text=value;input.characterLimit=128;
            var placeholder=Label(fieldObject.transform,"Placeholder",22,TextAnchor.MiddleLeft);SetRect(placeholder.rectTransform,Vector2.zero,Vector2.one,new Vector2(18,2),new Vector2(-18,-2));placeholder.text=label;placeholder.color=new Color(.35f,.45f,.40f,.6f);input.placeholder=placeholder;
            var selected=input.selectionColor;selected=new Color(.35f,.80f,.69f,.45f);input.selectionColor=selected;
            return input;
        }
        private static RectTransform Rect(GameObject go)=>go.GetComponent<RectTransform>();
        private static void Fill(RectTransform rect,float inset){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(inset,inset);rect.offsetMax=new Vector2(-inset,-inset);}
        private static void Place(RectTransform rect,float xmin,float ymin,float xmax,float ymax){rect.anchorMin=new Vector2(xmin,ymin);rect.anchorMax=new Vector2(xmax,ymax);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static void SetRect(RectTransform rect,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;}
    }

    /// <summary>Visible pointer and keyboard focus without motion, so reduced-motion mode stays calm.</summary>
    public sealed class FestivalFocusFrame : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private Outline outline;
        private Color activeColor,inactiveColor;
        private bool hovered,selected;
        public void Configure(Outline target,Color active,Color inactive){outline=target;activeColor=active;inactiveColor=inactive;Refresh();}
        public void OnPointerEnter(PointerEventData eventData){hovered=true;Refresh();}
        public void OnPointerExit(PointerEventData eventData){hovered=false;Refresh();}
        public void OnSelect(BaseEventData eventData){selected=true;Refresh();}
        public void OnDeselect(BaseEventData eventData){selected=false;Refresh();}
        private void Refresh()
        {
            if(outline==null)return;
            bool active=hovered||selected;
            outline.effectColor=active?activeColor:inactiveColor;
            outline.effectDistance=active?new Vector2(2,-2):new Vector2(1,-1);
        }
    }
}
