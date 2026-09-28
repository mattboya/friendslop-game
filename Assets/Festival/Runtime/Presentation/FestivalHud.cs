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
    public sealed class FestivalHud : MonoBehaviour
    {
        private static readonly Color Ink=new Color(.035f,.065f,.075f,1);
        private static readonly Color DeepInk=new Color(.018f,.041f,.052f,1);
        private static readonly Color Paper=new Color(.948f,.928f,.852f,1);
        private static readonly Color MutedPaper=new Color(.735f,.765f,.729f,1);
        private static readonly Color Orange=new Color(.99f,.465f,.255f,1);
        private static readonly Color Mint=new Color(.385f,.86f,.725f,1);
        private static readonly Color Hairline=new Color(.51f,.56f,.50f,.35f);
        private static readonly Color[] RhythmColors={new Color(1,.38f,.61f,1),new Color(1,.76f,.29f,1),new Color(.34f,.78f,1,1),new Color(.46f,.95f,.68f,1)};
        private static readonly float[] RhythmAngles={90,180,0,-90};
        private FestivalSession session;
        private Canvas canvas;
        private Font font;
        private Font displayFont;
        private GameObject connectionPanel, menuPanel, menuShade, actionsPanel, rhythmPanel, rhythmShade, dancePanel, dialoguePanel, mapPanel, mapShade, noticePanel, promptPanel, rosterPanel, inventoryPanel, reticle,settingsPanel,menuHome,checkoutPanel,heldDetailPanel,reviewPanel;
        private FestivalDancePreview dancePreview;
        private GameObject objectivePanel, timerPanel, vitalPanel;
        private RectTransform actionsContent;
        private ScrollRect actionsScroll;
        private GameObject nextButton, cancelButton, resumeButton, promptKeycap;
        private Text status, notice, objective, objectiveTitle, vitals, roster, menuRoster, menuPhase, prompt, preview, rhythmStatus, rhythmDialogue, rhythmJudgment, rhythmCombo, rhythmTiming, dancerCaption, dialogueSpeaker, dialogueLine, mapText, mapTitle, voice, timerText,checkoutText,settingsText,heldDetailText,heldTitle,heldPrice,inventoryHeading,emptyGearLabel,reviewText;
        private Text musicValue,lookValue,motionValue,contrastValue;
        private RectTransform mapPlayerMarker;
        private GameObject campMap,festivalMap;
        private readonly Image[] slotFrames=new Image[3];
        private readonly Text[] slotTexts=new Text[3];
        private InputField nameField, addressField, portField;
        private readonly List<Image> noteViews = new List<Image>();
        private readonly Image[] rhythmReceptors=new Image[4];
        private readonly Image[] rhythmReceptorWells=new Image[4];
        private readonly float[] rhythmFlashUntil=new float[4];
        private readonly bool[] rhythmConsumed=new bool[32];
        private Image rhythmProgressFill;
        private RhythmChart displayedChart;
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
        private string checkoutItem="";
        private string connectionError="";
        private bool settingsOpen;
        private bool crewOpen;
        private AudioSource shopAudio;
        private AudioClip shopChime;
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
        private Image effectWash;
        private float baseFov=75;
        private float nextActionRefresh;
        private string lastNotice="";
        private float noticeUntil;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Text diagnostics;
        public bool DevelopmentMapVisible;
#endif

        private void Awake()
        {
            session=GetComponent<FestivalSession>();
            if(session==null)session=FindFirstObjectByType<FestivalSession>();
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
            Build();
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

            objectivePanel=Card(root.transform,"Objective card",new Vector2(.022f,.82f),new Vector2(.405f,.974f),true);
            Accent(objectivePanel.transform,Orange);
            var missionKicker=Label(objectivePanel.transform,"Mission label",17,TextAnchor.MiddleLeft);missionKicker.text="TONIGHT'S PLAN";missionKicker.fontStyle=FontStyle.Normal;missionKicker.color=Orange;Place(missionKicker.rectTransform,.06f,.69f,.96f,.94f);
            objectiveTitle=Label(objectivePanel.transform,"Objective title",29,TextAnchor.MiddleLeft);objectiveTitle.fontStyle=FontStyle.Normal;Place(objectiveTitle.rectTransform,.06f,.43f,.96f,.73f);
            objective=Label(objectivePanel.transform,"Objective detail",20,TextAnchor.MiddleLeft);objective.color=MutedPaper;Place(objective.rectTransform,.06f,.06f,.96f,.44f);
            timerPanel=Card(root.transform,"Round clock",new Vector2(.452f,.914f),new Vector2(.548f,.974f),true);
            timerText=Label(timerPanel.transform,"Time",31,TextAnchor.MiddleCenter);timerText.fontStyle=FontStyle.Normal;Fill(timerText.rectTransform,4);
            vitalPanel=Card(root.transform,"Player card",new Vector2(.695f,.862f),new Vector2(.978f,.974f),true);
            Accent(vitalPanel.transform,Mint);
            vitals=Label(vitalPanel.transform,"Vitals",21,TextAnchor.MiddleRight);Place(vitals.rectTransform,.045f,.10f,.94f,.92f);

            rosterPanel=Card(root.transform,"Crew card",new Vector2(.815f,.807f),new Vector2(.978f,.853f),true);
            roster=Label(rosterPanel.transform,"Roster",18,TextAnchor.MiddleRight);Fill(roster.rectTransform,10);
            voice=Label(root.transform,"Voice",17,TextAnchor.LowerLeft);voice.color=MutedPaper;SetRect(voice.rectTransform,new Vector2(.023f,.012f),new Vector2(.23f,.052f),Vector2.zero,Vector2.zero);
            // The upper-left information rail keeps both palms, held props and
            // the ground immediately ahead visible during close interactions.
            promptPanel=Card(root.transform,"Action prompt",new Vector2(.022f,.715f),new Vector2(.405f,.80f),true);
            promptKeycap=Keycap(promptPanel.transform,"E",new Vector2(.025f,.19f),new Vector2(.11f,.81f));
            prompt=Label(promptPanel.transform,"Prompt",19,TextAnchor.MiddleLeft);Place(prompt.rectTransform,.135f,.08f,.97f,.92f);promptPanel.SetActive(false);
            reviewPanel=Card(root.transform,"Camp round review",new Vector2(.29f,.40f),new Vector2(.71f,.80f),true);
            Accent(reviewPanel.transform,Orange);
            reviewText=Label(reviewPanel.transform,"Review text",23,TextAnchor.MiddleCenter);
            Place(reviewText.rectTransform,.06f,.06f,.94f,.94f);reviewPanel.SetActive(false);
            noticePanel=Card(root.transform,"Session notice",new Vector2(.326f,.785f),new Vector2(.674f,.849f),true);
            Accent(noticePanel.transform,Orange);
            notice=Label(noticePanel.transform,"Notice",20,TextAnchor.MiddleCenter);Fill(notice.rectTransform,14);noticePanel.SetActive(false);
            inventoryPanel=Card(root.transform,"Equipment bar",new Vector2(.714f,.024f),new Vector2(.978f,.148f),true);
            inventoryHeading=Label(inventoryPanel.transform,"Equipment heading",17,TextAnchor.UpperLeft);inventoryHeading.fontStyle=FontStyle.Normal;inventoryHeading.color=Mint;inventoryHeading.text="GEAR   /   1–3 EQUIP     Q USE     G DROP";
            Place(inventoryHeading.rectTransform,.045f,.75f,.98f,.96f);
            emptyGearLabel=Label(inventoryPanel.transform,"Empty gear",20,TextAnchor.MiddleCenter);
            emptyGearLabel.color=Mint;Fill(emptyGearLabel.rectTransform,6);emptyGearLabel.gameObject.SetActive(false);
            for(int i=0;i<3;i++)
            {
                float left=.045f+i*.313f;
                var slot=Card(inventoryPanel.transform,"Slot "+(i+1),new Vector2(left,.08f),new Vector2(left+.293f,.70f),true);
                slotFrames[i]=slot.GetComponent<Image>();
                slotTexts[i]=Label(slot.transform,"Slot label",18,TextAnchor.MiddleCenter);Fill(slotTexts[i].rectTransform,4);
            }
            reticle=Panel(root.transform,"Aim dot",Paper,new Vector2(.499f,.498f),new Vector2(.501f,.502f));
            reticle.GetComponent<Image>().raycastTarget=false;
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
            var dancerHeading=Label(dancePanel.transform,"Dancer heading",19,TextAnchor.MiddleLeft);dancerHeading.text="YOU  /  ON THE FLOOR";dancerHeading.fontStyle=FontStyle.Normal;dancerHeading.color=Mint;Place(dancerHeading.rectTransform,.07f,.915f,.93f,.985f);
            var dancerWindow=Panel(dancePanel.transform,"Dancer window",new Color(.025f,.073f,.083f,1),new Vector2(.04f,.20f),new Vector2(.96f,.90f));dancerWindow.GetComponent<Image>().raycastTarget=false;
            var dancerImageObject=new GameObject("Live character view",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));dancerImageObject.transform.SetParent(dancerWindow.transform,false);
            var dancerImage=dancerImageObject.GetComponent<RawImage>();dancerImage.color=Color.white;dancerImage.raycastTarget=false;Fill(dancerImage.rectTransform,0);
            var dancerAspect=dancerImageObject.AddComponent<AspectRatioFitter>();dancerAspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;dancerAspect.aspectRatio=768f/576f;
            dancerCaption=Label(dancePanel.transform,"Dancer caption",17,TextAnchor.MiddleCenter);dancerCaption.text="YOUR LOOK • YOUR MOVES";dancerCaption.color=MutedPaper;Place(dancerCaption.rectTransform,.055f,.025f,.945f,.11f);
            dancePreview=gameObject.AddComponent<FestivalDancePreview>();dancePreview.Initialize(dancerImage);
            dancePanel.SetActive(false);

            rhythmPanel=Card(root.transform,"Rhythm lane",new Vector2(.015f,.05f),new Vector2(.495f,.95f),true);
            Accent(rhythmPanel.transform,Orange);
            rhythmDialogue=Label(rhythmPanel.transform,"Challenge title",27,TextAnchor.MiddleLeft);rhythmDialogue.fontStyle=FontStyle.Normal;Place(rhythmDialogue.rectTransform,.085f,.91f,.65f,.975f);
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
            for(int i=0;i<32;i++)
            {
                var note=Panel(rhythmPanel.transform,"Note "+i,Color.white,Vector2.zero,Vector2.zero).GetComponent<Image>();
                note.sprite=arrowSprite;note.preserveAspect=true;note.raycastTarget=false;
                var noteEdge=note.gameObject.AddComponent<Outline>();noteEdge.effectColor=new Color(.005f,.018f,.025f,.94f);noteEdge.effectDistance=new Vector2(3,-3);
                noteViews.Add(note);note.gameObject.SetActive(false);
            }
            var hitLine=Panel(rhythmPanel.transform,"Judgment line",Orange,new Vector2(.08f,.755f),new Vector2(.92f,.761f));hitLine.GetComponent<Image>().raycastTarget=false;
            for(int lane=0;lane<4;lane++)
            {
                float x=.08f+lane*.21f;
                var well=Panel(rhythmPanel.transform,"Receptor well "+lane,new Color(.22f,.32f,.35f,1),new Vector2(x+.007f,.766f),new Vector2(x+.203f,.895f));Round(well);rhythmReceptorWells[lane]=well.GetComponent<Image>();rhythmReceptorWells[lane].raycastTarget=false;
                var arrow=Panel(well.transform,"Target arrow",new Color(.76f,.85f,.84f,.9f),new Vector2(.12f,.09f),new Vector2(.88f,.91f)).GetComponent<Image>();
                arrow.sprite=arrowOutlineSprite;arrow.preserveAspect=true;arrow.raycastTarget=false;arrow.transform.localRotation=Quaternion.Euler(0,0,RhythmAngles[lane]);rhythmReceptors[lane]=arrow;
            }
            rhythmJudgment=Label(rhythmPanel.transform,"Judgment",45,TextAnchor.MiddleCenter);rhythmJudgment.font=displayFont;rhythmJudgment.fontStyle=FontStyle.Normal;Place(rhythmJudgment.rectTransform,.06f,.46f,.94f,.57f);
            rhythmJudgment.gameObject.AddComponent<Outline>().effectColor=new Color(0,.015f,.02f,.95f);
            rhythmCombo=Label(rhythmPanel.transform,"Combo",56,TextAnchor.MiddleCenter);rhythmCombo.font=displayFont;rhythmCombo.fontStyle=FontStyle.Normal;rhythmCombo.color=Paper;Place(rhythmCombo.rectTransform,.06f,.36f,.94f,.47f);
            rhythmCombo.gameObject.AddComponent<Outline>().effectColor=new Color(0,.015f,.02f,.95f);
            rhythmTiming=Label(rhythmPanel.transform,"Timing",20,TextAnchor.MiddleCenter);rhythmTiming.color=MutedPaper;Place(rhythmTiming.rectTransform,.06f,.315f,.94f,.37f);
            var progressTrack=Panel(rhythmPanel.transform,"Step progress track",new Color(.33f,.43f,.44f,1),new Vector2(.08f,.055f),new Vector2(.92f,.071f));progressTrack.GetComponent<Image>().raycastTarget=false;
            rhythmProgressFill=Panel(rhythmPanel.transform,"Step progress",Mint,new Vector2(.08f,.055f),new Vector2(.08f,.071f)).GetComponent<Image>();rhythmProgressFill.raycastTarget=false;
            var controls=Label(rhythmPanel.transform,"Controls",17,TextAnchor.MiddleCenter);controls.text="← / A    ↓ / S    ↑ / W    → / D";controls.color=MutedPaper;Place(controls.rectTransform,.06f,.005f,.94f,.049f);
            rhythmPanel.SetActive(false);
            dialoguePanel=Card(root.transform,"Interaction dialogue",new Vector2(.70f,.52f),new Vector2(.96f,.69f),false);
            Accent(dialoguePanel.transform,Orange);
            dialogueSpeaker=Label(dialoguePanel.transform,"Speaker",17,TextAnchor.MiddleLeft);dialogueSpeaker.fontStyle=FontStyle.Normal;dialogueSpeaker.color=new Color(.17f,.47f,.40f);Place(dialogueSpeaker.rectTransform,.045f,.56f,.95f,.90f);
            dialogueLine=Label(dialoguePanel.transform,"Dialogue line",24,TextAnchor.MiddleLeft);dialogueLine.color=Ink;Place(dialogueLine.rectTransform,.045f,.09f,.95f,.62f);
            dialoguePanel.SetActive(false);

            BuildConnection(root.transform);
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
            heldDetailPanel=Card(root.transform,"Held item label",new Vector2(.61f,.245f),new Vector2(.955f,.365f),false);
            Accent(heldDetailPanel.transform,Orange);
            heldTitle=Label(heldDetailPanel.transform,"Held item title",24,TextAnchor.MiddleLeft);heldTitle.fontStyle=FontStyle.Normal;heldTitle.color=Ink;Place(heldTitle.rectTransform,.06f,.59f,.78f,.91f);
            heldPrice=Label(heldDetailPanel.transform,"Held item price",24,TextAnchor.MiddleRight);heldPrice.fontStyle=FontStyle.Normal;heldPrice.color=new Color(.12f,.43f,.37f);Place(heldPrice.rectTransform,.78f,.59f,.94f,.91f);
            Rule(heldDetailPanel.transform,.55f);
            heldDetailText=Label(heldDetailPanel.transform,"Held item details",22,TextAnchor.MiddleLeft);heldDetailText.color=new Color(.15f,.25f,.22f);Place(heldDetailText.rectTransform,.06f,.11f,.94f,.54f);
        }

        private void BuildConnection(Transform root)
        {
            connectionPanel=Card(root,"Connection",new Vector2(.285f,.115f),new Vector2(.715f,.885f),false);
            Accent(connectionPanel.transform,Orange);
            var eyebrow=Label(connectionPanel.transform,"Invitation label",18,TextAnchor.MiddleCenter);eyebrow.text="SUN + MOON FESTIVAL  /  CREW ENTRY";eyebrow.fontStyle=FontStyle.Normal;eyebrow.color=new Color(.30f,.38f,.33f);Place(eyebrow.rectTransform,.08f,.89f,.92f,.95f);
            var title=Label(connectionPanel.transform,"Title",59,TextAnchor.MiddleCenter);title.font=displayFont;title.text="AFTER HOURS";title.color=Ink;Place(title.rectTransform,.06f,.76f,.94f,.89f);
            var subtitle=Label(connectionPanel.transform,"Subtitle",20,TextAnchor.MiddleCenter);subtitle.text="Find your friend. Catch the last shuttle.";subtitle.color=new Color(.28f,.38f,.35f);Place(subtitle.rectTransform,.08f,.705f,.92f,.765f);
            Rule(connectionPanel.transform,.687f);
            nameField=Field(connectionPanel.transform,"YOUR NAME","Friend",.63f);
            addressField=Field(connectionPanel.transform,"HOST ADDRESS","127.0.0.1",.48f);
            portField=Field(connectionPanel.transform,"PORT","7777",.33f);
            portField.onValueChanged.AddListener(_=>connectionError="");
            Button(connectionPanel.transform,"CREATE GAME",new Vector2(.075f,.10f),new Vector2(.49f,.20f),()=>StartHost());
            Button(connectionPanel.transform,"JOIN GAME",new Vector2(.51f,.10f),new Vector2(.925f,.20f),()=>StartJoin());
            status=Label(connectionPanel.transform,"Status",18,TextAnchor.MiddleCenter);status.color=new Color(.25f,.35f,.32f);Place(status.rectTransform,.08f,.025f,.92f,.09f);
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
            var serial=Label(menuPanel.transform,"Pass serial",17,TextAnchor.MiddleCenter);serial.text="FESTIVAL PASS   /   LAST SHUTTLE CREW";serial.fontStyle=FontStyle.Normal;serial.color=new Color(.32f,.40f,.35f);Place(serial.rectTransform,.08f,.79f,.92f,.85f);
            Rule(menuPanel.transform,.782f);
            menuHome=Panel(menuPanel.transform,"Pass home",Color.clear,new Vector2(.055f,.025f),new Vector2(.945f,.76f));menuHome.GetComponent<Image>().raycastTarget=false;
            var crewHeading=Label(menuHome.transform,"Crew heading",17,TextAnchor.MiddleLeft);crewHeading.text="YOUR CREW";crewHeading.fontStyle=FontStyle.Normal;crewHeading.color=new Color(.31f,.42f,.38f);Place(crewHeading.rectTransform,.045f,.90f,.56f,.98f);
            menuRoster=Label(menuHome.transform,"Crew status",23,TextAnchor.UpperLeft);menuRoster.fontStyle=FontStyle.Normal;menuRoster.color=Ink;Place(menuRoster.rectTransform,.045f,.72f,.55f,.89f);menuRoster.raycastTarget=false;
            var phaseCard=Card(menuHome.transform,"Current stop",new Vector2(.59f,.735f),new Vector2(.955f,.98f),true);
            var phaseLabel=Label(phaseCard.transform,"Stop label",16,TextAnchor.MiddleLeft);phaseLabel.text="CURRENT STOP";phaseLabel.fontStyle=FontStyle.Normal;phaseLabel.color=Mint;Place(phaseLabel.rectTransform,.09f,.62f,.92f,.92f);
            menuPhase=Label(phaseCard.transform,"Stop value",24,TextAnchor.MiddleLeft);menuPhase.fontStyle=FontStyle.Normal;Place(menuPhase.rectTransform,.09f,.12f,.92f,.64f);
            Rule(menuHome.transform,.70f);
            var pocketHeading=Label(menuHome.transform,"Pockets heading",17,TextAnchor.MiddleLeft);pocketHeading.text="CURRENT LOADOUT";pocketHeading.fontStyle=FontStyle.Normal;pocketHeading.color=new Color(.31f,.42f,.38f);Place(pocketHeading.rectTransform,.045f,.61f,.95f,.69f);
            preview=Label(menuHome.transform,"Preview",21,TextAnchor.MiddleLeft);Place(preview.rectTransform,.045f,.51f,.95f,.62f);preview.color=Ink;
            nextButton=Button(menuHome.transform,"NEXT CAMP",new Vector2(.045f,.385f),new Vector2(.955f,.50f),()=>session.Command("Reset"));
            cancelButton=Button(menuHome.transform,"CANCEL ACTION",new Vector2(.045f,.385f),new Vector2(.955f,.50f),()=>session.Command("Cancel"));
            resumeButton=Button(menuHome.transform,"RESUME FESTIVAL",new Vector2(.045f,.385f),new Vector2(.955f,.50f),()=>{session.MenuOpen=false;settingsOpen=false;});
            Button(menuHome.transform,"CREW + NEARBY",new Vector2(.045f,.26f),new Vector2(.955f,.375f),()=>{crewOpen=!crewOpen;});
            Button(menuHome.transform,"SETTINGS",new Vector2(.045f,.135f),new Vector2(.955f,.25f),()=>{settingsOpen=true;});
            Button(menuHome.transform,"LEAVE SESSION",new Vector2(.045f,.01f),new Vector2(.955f,.125f),()=>session.Leave());
            settingsPanel=Panel(menuPanel.transform,"Pass settings",Color.clear,new Vector2(.055f,.025f),new Vector2(.945f,.76f));settingsPanel.GetComponent<Image>().raycastTarget=false;
            var settingsHeading=Label(settingsPanel.transform,"Settings heading",32,TextAnchor.MiddleLeft);settingsHeading.text="LOCAL SETTINGS";settingsHeading.fontStyle=FontStyle.Normal;settingsHeading.color=Ink;Place(settingsHeading.rectTransform,.04f,.88f,.96f,.98f);
            settingsText=Label(settingsPanel.transform,"Settings values",18,TextAnchor.MiddleLeft);settingsText.text="Saved on this machine. Changes apply immediately.";settingsText.color=new Color(.32f,.40f,.36f);Place(settingsText.rectTransform,.04f,.78f,.96f,.88f);
            SettingRow(settingsPanel.transform,"MUSIC VOLUME",.61f,out musicValue);
            SettingRow(settingsPanel.transform,"LOOK SPEED",.45f,out lookValue);
            SettingRow(settingsPanel.transform,"REDUCED MOTION",.29f,out motionValue);
            SettingRow(settingsPanel.transform,"HIGH CONTRAST",.13f,out contrastValue);
            Button(settingsPanel.transform,"−",new Vector2(.66f,.64f),new Vector2(.76f,.72f),()=>ChangeSetting("music",-1));
            Button(settingsPanel.transform,"+",new Vector2(.84f,.64f),new Vector2(.94f,.72f),()=>ChangeSetting("music",1));
            Button(settingsPanel.transform,"−",new Vector2(.66f,.48f),new Vector2(.76f,.56f),()=>ChangeSetting("look",-1));
            Button(settingsPanel.transform,"+",new Vector2(.84f,.48f),new Vector2(.94f,.56f),()=>ChangeSetting("look",1));
            Button(settingsPanel.transform,"TOGGLE",new Vector2(.70f,.32f),new Vector2(.94f,.40f),()=>ChangeSetting("motion",1));
            Button(settingsPanel.transform,"TOGGLE",new Vector2(.70f,.16f),new Vector2(.94f,.24f),()=>ChangeSetting("contrast",1));
            Button(settingsPanel.transform,"BACK TO PASS",new Vector2(.04f,.015f),new Vector2(.96f,.105f),()=>settingsOpen=false);
        }

        private void BuildMap(Transform root)
        {
            mapShade=Panel(root,"Map dimmer",new Color(.01f,.025f,.03f,.65f),Vector2.zero,Vector2.one);mapShade.GetComponent<Image>().raycastTarget=false;
            mapPanel=Card(root,"Map",new Vector2(.11f,.10f),new Vector2(.89f,.90f),false);
            Accent(mapPanel.transform,Mint);
            mapTitle=Label(mapPanel.transform,"Map title",38,TextAnchor.MiddleLeft);mapTitle.fontStyle=FontStyle.Normal;mapTitle.color=Ink;Place(mapTitle.rectTransform,.04f,.89f,.96f,.97f);
            var mapHint=Label(mapPanel.transform,"Map hint",17,TextAnchor.MiddleRight);mapHint.text="HOLD TAB TO VIEW";mapHint.color=new Color(.34f,.43f,.39f);Place(mapHint.rectTransform,.72f,.91f,.96f,.97f);
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
            MapPin(festivalMap.transform,"SHUTTLE",.50f,.12f,Orange);
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

        private void SettingRow(Transform parent,string title,float y,out Text value)
        {
            Rule(parent,y-.01f);
            var label=Label(parent,title,20,TextAnchor.MiddleLeft);label.text=title;label.color=Ink;label.fontStyle=FontStyle.Normal;Place(label.rectTransform,.04f,y+.01f,.53f,y+.11f);
            value=Label(parent,title+" value",21,TextAnchor.MiddleRight);value.color=new Color(.16f,.44f,.38f);Place(value.rectTransform,.52f,y+.01f,.65f,y+.11f);
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
            status.text=connectionError==""?session.Message:connectionError;
            status.color=connectionError==""?new Color(.25f,.35f,.32f):new Color(.58f,.16f,.12f);
            if(session.Message!=lastNotice){lastNotice=session.Message;noticeUntil=Time.unscaledTime+5;}
            notice.text=session.Message;
            noticePanel.SetActive(session.Connected&&!session.MenuOpen&&!string.IsNullOrEmpty(session.Message)&&Time.unscaledTime<noticeUntil&&session.Message!="Accepted");
            connectionPanel.SetActive(!session.Connected);
            bool showMenu=session.Connected&&session.MenuOpen;
            objectivePanel.SetActive(session.Connected&&!showMenu);
            timerPanel.SetActive(session.Connected&&!showMenu);
            vitalPanel.SetActive(session.Connected&&!showMenu);
            voice.gameObject.SetActive(session.Connected&&!showMenu);
            menuShade.SetActive(showMenu);
            menuPanel.SetActive(showMenu);
            menuHome.SetActive(!settingsOpen);
            settingsPanel.SetActive(settingsOpen);
            actionsPanel.SetActive(showMenu&&crewOpen&&!settingsOpen&&session.State?.Phase=="Playing");
            checkoutPanel.SetActive(session.Connected&&!showMenu&&checkoutItem!="");
            voice.text=session.Connected?(session.Voice.Available?session.Voice.Status:"VOICE OFF"):"";
            if(!session.Connected){objective.text="";objectiveTitle.text="";vitals.text="";timerText.text="";roster.text="";prompt.text="";promptPanel.SetActive(false);reviewPanel.SetActive(false);heldDetailPanel.SetActive(false);rosterPanel.SetActive(false);inventoryPanel.SetActive(false);reticle.SetActive(false);rhythmPanel.SetActive(false);rhythmShade.SetActive(false);dancePanel.SetActive(false);dancePreview.Hide();dialoguePanel.SetActive(false);mapPanel.SetActive(false);mapShade.SetActive(false);ApplyEffects(null);return;}
            rosterPanel.SetActive(!showMenu);
            var state=session.State;var player=session.LocalPlayer;if(state==null||player==null){rhythmShade.SetActive(false);rhythmPanel.SetActive(false);dancePanel.SetActive(false);dancePreview.Hide();return;}
            handGear.Clear();foreach(var item in player.Inventory)if(item.ItemId!="little_spoon")handGear.Add(item);
            reviewPanel.SetActive(state.Phase=="CampReview"&&!showMenu);
            if(state.Phase=="CampReview")
            {
                int votes=ConnectedReviewVotes(state);
                var mine=state.ReviewVotes.Find(v=>v.PlayerId==player.Id);
                reviewText.text="THE VERY OFFICIAL ROUND REVIEW\n\n"
                    +(state.ReviewResult=="Success"?"FRIEND FOUND":"A GLORIOUS DISASTER")+"   •   SALES $"+state.ReviewSales
                    +"   •   SURVIVORS "+state.ReviewSurvivors+"\n"
                    +"CAMP ANTICS "+state.ReviewAntics+"\n\n"
                    +"Pick the crew's totally serious award:\n"
                    +"1  CHAOS MAGNET\n2  UNLIKELY HERO\n3  MOST COMMITTED TO THE BIT\n\n"
                    +(mine==null?"YOUR VOTE IS WAITING":"YOU VOTED: "+CampFeatures.ReviewAwards[mine.Award])
                    +"   •   "+votes+" / "+state.ConnectedCrewCount+" CREW\n"
                    +(votes>=state.ConnectedCrewCount?"OFFICIAL VERDICT: "+CampFeatures.ReviewWinner(state.ReviewVotes)+"\n":"")
                    +(session.IsHost&&votes>=state.ConnectedCrewCount?"E  OPEN THE SHOP":"Shopping opens when the crew has voted.");
            }
            var heldDefinition=state.Phase=="Shopping"?Catalog.FindItem(player.HeldOfferId):null;
            heldDetailPanel.SetActive(heldDefinition!=null&&!showMenu);
            if(heldDefinition!=null){heldTitle.text=heldDefinition.Name.ToUpperInvariant();heldPrice.text="$"+heldDefinition.Price;heldDetailText.text=heldDefinition.Description;}
            ApplyContrast(session.Profile.Data.HighContrast);
            UpdatePurchaseFeedback(state,player);
            nextButton.SetActive(state.Phase=="Results"&&session.IsHost);
            cancelButton.SetActive(state.Phase=="Playing"&&player.InteractionId!="");
            resumeButton.SetActive(!nextButton.activeSelf&&!cancelButton.activeSelf);
            UpdateText(state,player);
            UpdateGearLayout(handGear.Count>0);
            UpdateActions(state,player);
            if(checkoutItem!="" && (Catalog.FindItem(checkoutItem)==null || (state.Phase=="Shopping"?(player.HeldOfferId!=checkoutItem||!Near(player,0,7)):(state.Phase!="Playing"||FocusedOffer(state,player)!=checkoutItem))))checkoutItem="";
            var checkoutDefinition=Catalog.FindItem(checkoutItem);
            checkoutPanel.SetActive(!showMenu&&checkoutDefinition!=null);
            if(checkoutDefinition!=null)checkoutText.text="E  PAY $"+checkoutDefinition.Price+"  •  "+checkoutDefinition.Name.ToUpperInvariant()+"    G CANCEL";
            musicValue.text=Mathf.RoundToInt(session.Profile.Data.MusicVolume*100)+"%";
            lookValue.text=session.Profile.Data.MouseSensitivity.ToString("0.00");
            motionValue.text=session.Profile.Data.ReducedMotion?"ON":"OFF";
            contrastValue.text=session.Profile.Data.HighContrast?"ON":"OFF";
            promptPanel.SetActive(!showMenu&&!string.IsNullOrEmpty(prompt.text));
            promptKeycap.SetActive(primaryAction!=null);
            UpdateKeyboard(state,player);
            UpdateRhythm(state,player);
            UpdateMap(state,player);
            if(mapPanel.activeSelf){rhythmShade.SetActive(false);dancePanel.SetActive(false);dancePreview.Hide();}
            if(rhythmPanel.activeSelf&&!showMenu&&!mapPanel.activeSelf)
            {
                objectivePanel.SetActive(false);timerPanel.SetActive(false);vitalPanel.SetActive(false);voice.gameObject.SetActive(false);rosterPanel.SetActive(false);promptPanel.SetActive(false);noticePanel.SetActive(false);heldDetailPanel.SetActive(false);checkoutPanel.SetActive(false);
            }
            inventoryPanel.SetActive(!showMenu&&!rhythmPanel.activeSelf&&!mapPanel.activeSelf&&state.Phase!="CampReview");
            reticle.SetActive(!showMenu&&!mapPanel.activeSelf&&!rhythmPanel.activeSelf);
            ApplyEffects(player);
            var gear=new List<string>();
            foreach(var item in handGear)gear.Add(Catalog.FindItem(item.ItemId)?.Name??item.ItemId);
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

        private void UpdateGearLayout(bool hasGear)
        {
            if(expandedGear!=hasGear)
            {
                expandedGear=hasGear;
                // A vacant three-slot bar needlessly covers the world during
                // arrival and after the last item is used.
                Place(Rect(inventoryPanel),hasGear ? .714f : .758f,.024f,.978f,hasGear ? .148f : .082f);
                inventoryHeading.gameObject.SetActive(hasGear);
                emptyGearLabel.gameObject.SetActive(!hasGear);
                foreach(var slot in slotFrames)slot.gameObject.SetActive(hasGear);
            }
            if(!hasGear)emptyGearLabel.text="0 / 3  •  NO GEAR";
        }

        private void UpdateText(RoundState state,PlayerState player)
        {
            double remaining=Math.Max(0,state.DurationSeconds-state.ElapsedSeconds);
            string mission=FestivalGuidance.Headline(state,player);
            string hint=FestivalGuidance.Hint(state,player);
            objectiveTitle.text=mission;
            objective.text=state.Phase=="Shopping"?"Browse gear • pay the seller • meet at the trailhead":hint;
            timerText.text=state.Phase=="Playing"?Math.Floor(remaining/60).ToString("0")+":"+(remaining%60).ToString("00"):state.Phase=="Shopping"?"CAMP":state.Phase=="CampReview"?"REVIEW":state.Phase=="Results"?"DONE":"WAIT";
            string effects=player.Effects.Count==0?"clear":string.Join(", ",player.Effects.ConvertAll(e=>e.Id+" "+e.RemainingSeconds.ToString("0")+"s"));
            var threat=state.Npcs.FindAll(n=>n.Kind=="Wook"&&n.Suspicion>0);double suspicion=0;string threatState="clear";
            foreach(var npc in threat)if(npc.Suspicion>suspicion){suspicion=npc.Suspicion;threatState=npc.Mode;}
            var police=state.Npcs.Find(n=>n.Kind=="Cop"&&n.TargetId==player.Id);
            vitals.text="HP "+player.Health+"  •  $"+player.Cash+"  •  STASH $"+state.StashCash+"\n"
                +"SALES $"+state.GrossSales+" / $50  •  "+player.Life.ToUpperInvariant()+"  •  "
                +(player.Effects.Count>0?effects.ToUpperInvariant():suspicion>0?"CROWD "+suspicion.ToString("0")+" / "+threatState.ToUpperInvariant():police==null?"CROWD CLEAR":"SECURITY "+police.Mode.ToUpperInvariant());
            int equippedIndex=handGear.FindIndex(item=>item.ItemId==player.EquippedItemId);
            if(equippedIndex>=0)selectedSlot=equippedIndex;
            for(int i=0;i<slotFrames.Length;i++)
            {
                var item=i<handGear.Count?handGear[i]:null;
                bool equipped=item!=null&&item.ItemId==player.EquippedItemId;
                slotFrames[i].color=equipped?new Color(.13f,.34f,.32f,1):new Color(.07f,.13f,.15f,1);
                slotTexts[i].text=(i+1)+"  "+(item==null?"EMPTY":(Catalog.FindItem(item.ItemId)?.Name??item.ItemId)+" ×"+item.Count+(equipped?"  ●":""));
                slotTexts[i].color=equipped?new Color(1,.79f,.53f):Paper;
            }
            int ready=state.Players.FindAll(p=>p.Ready).Count;
            roster.text=state.Players.Count+" CREW"+(state.Phase=="Shopping"?"  •  "+ready+" READY":"  •  TAB MAP");
            menuRoster.text=state.Players.Count+" / 8 FRIENDS\n"+string.Join("   •   ",state.Players.ConvertAll(p=>(p.Id==player.Id?"YOU":p.Name)+(p.Ready?" READY":"")));
            menuPhase.text=state.Phase=="Shopping"?"CAMPSITE":state.Phase=="CampReview"?"ROUND REVIEW":state.Phase=="Playing"?"FESTIVAL":state.Phase=="Results"?"LAST SHUTTLE":"GATHERING";
        }

        private void UpdateKeyboard(RoundState state,PlayerState player)
        {
            if(state.Phase=="CampReview"&&!session.MenuOpen)
            {
                int vote=session.Controls.Slot1.WasPressedThisFrame()?0:session.Controls.Slot2.WasPressedThisFrame()?1:session.Controls.Slot3.WasPressedThisFrame()?2:-1;
                if(vote>=0)session.Command("ReviewVote",amount:vote);
            }
            if(player.CampVisitId!=""&&!session.MenuOpen&&session.Controls.Drop.WasPressedThisFrame())
            {
                session.Command("ExitCamp");return;
            }
            if(player.CampVisitId!=""&&!session.MenuOpen&&session.Controls.Chat.WasPressedThisFrame())
            {
                session.Command("CampAntic");return;
            }
            int equipSlot=session.Controls.Slot1.WasPressedThisFrame()?0:session.Controls.Slot2.WasPressedThisFrame()?1:session.Controls.Slot3.WasPressedThisFrame()?2:-1;
            if(equipSlot>=0&&!session.MenuOpen&&state.Phase!="CampReview"){selectedSlot=equipSlot;if(equipSlot<handGear.Count)session.Command("Equip",item:handGear[equipSlot].ItemId);}
            if(session.Controls.Chat.WasPressedThisFrame()&&!session.MenuOpen&&state.Phase=="Playing"&&player.Life=="Alive"&&player.InteractionId=="")
            {
                var speaker=NearestTalker(state.Npcs,player.X,player.Z,2.7f);
                if(speaker!=null)session.Command("Talk",speaker.Id);
            }
            if(session.Controls.Interact.WasPressedThisFrame()&&!session.MenuOpen)primaryAction?.Invoke();
            if(session.Controls.Drop.WasPressedThisFrame()&&!session.MenuOpen&&checkoutItem!=""){checkoutItem="";return;}
            if(session.Controls.Drop.WasPressedThisFrame()&&!session.MenuOpen&&state.Phase=="Shopping"&&player.HeldOfferId!=""){session.Command("ReturnOffer");return;}
            if(handGear.Count>0)
            {
                selectedSlot=Mathf.Clamp(selectedSlot,0,handGear.Count-1);string item=player.EquippedItemId!=""?player.EquippedItemId:handGear[selectedSlot].ItemId;
                if(session.Controls.Use.WasPressedThisFrame()&&!session.MenuOpen)session.Command("Use",item:item);
                if(session.Controls.Drop.WasPressedThisFrame()&&!session.MenuOpen)session.Command("Drop",item:item);
            }
        }

        private void UpdateRhythm(RoundState state,PlayerState player)
        {
            var interaction=state.Interactions.Find(i=>i.Id==player.InteractionId&&i.Status=="Active");
            bool rhythm=interaction!=null&&FestivalInput.IsRhythmKind(interaction.Kind);
            rhythmPanel.SetActive(rhythm);
            bool showDancer=rhythm&&interaction.Kind=="Dance"&&!session.MenuOpen;
            rhythmShade.SetActive(rhythm&&!session.MenuOpen);
            Place(rhythmPanel.GetComponent<RectTransform>(),showDancer ? .015f : .32f,showDancer ? .05f : .06f,showDancer ? .495f : .68f,showDancer ? .95f : .87f);
            dancePanel.SetActive(showDancer);
            if(showDancer)dancePreview.Show(session.LocalWorldCharacter,session.ViewCamera);
            else dancePreview.Hide();
            bool chatter=!rhythm&&!session.MenuOpen&&player.NpcSpeechUntil>session.EstimatedSimulationSeconds&&!string.IsNullOrEmpty(player.NpcSpeech);
            bool showDialogue=(rhythm&&!string.IsNullOrEmpty(interaction.DialogueText))||chatter;
            dialoguePanel.SetActive(showDialogue);
            Place(dialoguePanel.GetComponent<RectTransform>(),showDancer ? .525f : .70f,showDancer ? .085f : .52f,showDancer ? .965f : .96f,showDancer ? .215f : .69f);
            dancerCaption.gameObject.SetActive(showDancer&&!showDialogue);
            if(chatter){dialogueSpeaker.text=player.NpcSpeaker;dialogueLine.text=player.NpcSpeech;}
            if(!rhythm)return;
            if(displayedChart==null||displayedInteractionId!=interaction.Id||displayedSeed!=interaction.ChartSeed||displayedNoteCount!=interaction.NoteCount||displayedPhrase!=interaction.Phrase||Math.Abs(displayedBeatSeconds-interaction.BeatSeconds)>.0001)
            {
                displayedChart=RhythmChart.Create(interaction.ChartSeed,interaction.NoteCount,interaction.BeatSeconds);
                displayedInteractionId=interaction.Id;displayedSeed=interaction.ChartSeed;displayedNoteCount=interaction.NoteCount;displayedPhrase=interaction.Phrase;displayedBeatSeconds=interaction.BeatSeconds;
                Array.Clear(rhythmConsumed,0,rhythmConsumed.Length);processedRhythmInputs=0;rhythmComboCount=0;rhythmHitCount=0;lastRhythmJudgment="";rhythmJudgmentUntil=0;
                shownRhythmHits=shownRhythmBpm=shownRhythmCount=shownRhythmCombo=-1;
                rhythmDialogue.text=interaction.Kind.ToUpperInvariant()+"  /  FOUR-LANE";
            }
            double now=session.EstimatedSimulationSeconds-interaction.StartSeconds;
            string effect=player.Effects.Count==0?"":player.Effects[0].Id;
            double lead=Catalog.FindEffect(effect)?.LeadSeconds??2;
            ProcessRhythmFeedback(interaction,now);
            int bpm=Mathf.RoundToInt(60f/(float)interaction.BeatSeconds);
            if(shownRhythmHits!=rhythmHitCount||shownRhythmCount!=interaction.NoteCount||shownRhythmBpm!=bpm)
            {
                rhythmStatus.text=rhythmHitCount+" / "+interaction.NoteCount+"   •   "+bpm+" BPM";
                shownRhythmHits=rhythmHitCount;shownRhythmCount=interaction.NoteCount;shownRhythmBpm=bpm;
            }
            if(dialoguePanel.activeSelf)
            {
                dialogueSpeaker.text=interaction.Kind=="Police"?"SECURITY":interaction.Kind=="Dj"?"STAGE CREW":"FESTIVALGOER";
                dialogueLine.text=interaction.DialogueText;
            }
            if(shownRhythmCombo!=rhythmComboCount){rhythmCombo.text=rhythmComboCount>=2?rhythmComboCount+" COMBO":"";shownRhythmCombo=rhythmComboCount;}
            bool feedbackVisible=Time.unscaledTime<rhythmJudgmentUntil;
            rhythmJudgment.text=now<0?(now<=-1?Math.Ceiling(-now).ToString("0"):"1"):feedbackVisible?lastRhythmJudgment:"";
            rhythmJudgment.color=now<0?Paper:lastRhythmJudgment=="PERFECT"?new Color(1,.80f,.35f):lastRhythmJudgment=="GOOD"?Mint:new Color(1,.45f,.58f);
            rhythmTiming.text=feedbackVisible&&(lastRhythmJudgment=="PERFECT"||lastRhythmJudgment=="GOOD")?(lastRhythmError<-.02?"EARLY":lastRhythmError>.02?"LATE":"ON BEAT"):"";
            float completed=Mathf.Clamp01((float)(now/displayedChart.DurationSeconds));
            var progressRect=rhythmProgressFill.rectTransform;progressRect.anchorMin=new Vector2(.08f,.055f);progressRect.anchorMax=new Vector2(.08f+.84f*completed,.071f);progressRect.offsetMin=progressRect.offsetMax=Vector2.zero;
            if(!string.IsNullOrEmpty(interaction.DialogueText))session.AcknowledgeDisplayedDialogue(interaction);
            for(int i=0;i<noteViews.Count;i++)
            {
                var view=noteViews[i];if(i>=displayedChart.Notes.Count){view.gameObject.SetActive(false);continue;}
                var note=displayedChart.Notes[i];double remaining=note.TimeSeconds-now;
                bool visible=!rhythmConsumed[i]&&remaining<=lead&&remaining>=-.18;view.gameObject.SetActive(visible);if(!visible)continue;
                double progress=1-remaining/lead;
                var visual=EffectPresentation.Path(effect,note.Id,progress,session.Profile.Data.ReducedMotion);
                view.color=RhythmColors[note.Direction];
                var rect=view.rectTransform;float x=.185f+note.Direction*.21f,y=.11f+.65f*(float)progress;
                rect.anchorMin=rect.anchorMax=new Vector2(x,y);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(96,96);
                rect.anchoredPosition=new Vector2((float)visual.X*32,0);rect.localRotation=Quaternion.Euler(0,0,RhythmAngles[note.Direction]+(float)visual.Rotation*.08f);
            }
            for(int direction=0;direction<4;direction++)
            {
                bool pressed=!session.MenuOpen&&!session.Controls.Objectives.IsPressed()&&session.Controls.Notes[direction].WasPressedThisFrame();
                if(pressed){rhythmFlashUntil[direction]=Time.unscaledTime+.16f;if(showDancer)dancePreview.Step(direction);session.Command("Rhythm",direction:direction,time:Math.Max(0,now-session.Profile.Data.TimingCalibrationMs/1000.0));}
                bool flashing=Time.unscaledTime<rhythmFlashUntil[direction];
                rhythmReceptors[direction].color=flashing?RhythmColors[direction]:new Color(.76f,.85f,.84f,.9f);
                rhythmReceptorWells[direction].color=flashing?new Color(.25f,.43f,.44f,1):new Color(.22f,.32f,.35f,1);
            }
        }

        private void ProcessRhythmFeedback(InteractionState interaction,double now)
        {
            if(interaction.Inputs.Count<processedRhythmInputs)
            {
                Array.Clear(rhythmConsumed,0,rhythmConsumed.Length);processedRhythmInputs=0;rhythmComboCount=0;rhythmHitCount=0;lastRhythmJudgment="";
            }
            while(processedRhythmInputs<interaction.Inputs.Count)
            {
                var input=interaction.Inputs[processedRhythmInputs++];
                int closest=-1;double best=double.MaxValue;
                for(int n=0;n<displayedChart.Notes.Count;n++)
                {
                    var note=displayedChart.Notes[n];
                    double error=Math.Abs(input.TimeSeconds-note.TimeSeconds);
                    if(!rhythmConsumed[n]&&note.Direction==input.Direction&&error<=interaction.GoodWindowSeconds+1e-9&&error<best){closest=n;best=error;}
                }
                if(closest>=0)
                {
                    rhythmConsumed[closest]=true;rhythmComboCount++;rhythmHitCount++;
                    lastRhythmJudgment=best<=.08+1e-9?"PERFECT":"GOOD";
                    lastRhythmError=input.TimeSeconds-displayedChart.Notes[closest].TimeSeconds;
                }
                else{rhythmComboCount=0;lastRhythmJudgment="OFF BEAT";lastRhythmError=0;}
                rhythmJudgmentUntil=Time.unscaledTime+.58f;
            }
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
            objectivePanel.SetActive(false);timerPanel.SetActive(false);vitalPanel.SetActive(false);rosterPanel.SetActive(false);inventoryPanel.SetActive(false);promptPanel.SetActive(false);noticePanel.SetActive(false);heldDetailPanel.SetActive(false);checkoutPanel.SetActive(false);rhythmPanel.SetActive(false);dialoguePanel.SetActive(false);
            bool camp=state.Phase=="Shopping";campMap.SetActive(camp);festivalMap.SetActive(!camp);
            mapTitle.text=camp?"CAMPSITE  /  FIND YOUR WAY":"FESTIVAL GROUNDS  /  FIND YOUR WAY";
            float x=camp?Mathf.InverseLerp(-30,30,player.X):Mathf.InverseLerp(-35,35,player.X);
            float y=camp?Mathf.InverseLerp(-20,25,player.Z):Mathf.InverseLerp(-35,35,player.Z);
            Place(mapPlayerMarker,x-.012f,y-.018f,x+.012f,y+.018f);
            if(camp)
            {
                mapText.text="CAMP\n\nGEAR\nPick up one item. Pay the seller before you enter.\n\nTRAILHEAD\nPress E to ready. Your crew enters together.\n\nYOU ARE THE ORANGE DOT.";
                return;
            }
            string team="";foreach(var p in state.Players)team+=(p.Id==player.Id?"YOU":p.Name)+"  /  "+p.Life.ToUpperInvariant()+"\n";
            string mission=state.FriendFound?"FRIEND FOUND\nEscort them to the shuttle.":"FRIEND MISSING\nSearch the grounds. Their exact location is unknown.";
            mapText.text=mission+"\n\nCREW\n"+team+"\nYOU ARE THE ORANGE DOT.\n\nMEDIC  /  HOLDING\nHelp a downed or detained friend.";
        }

        private void ApplyEffects(PlayerState player)
        {
            if(player==null||player.Effects.Count==0){effectWash.color=Color.clear;if(session?.ViewCamera!=null)session.ViewCamera.fieldOfView=baseFov;return;}
            bool lsd=player.Effects.Exists(e=>e.Id=="lsd"), mushrooms=player.Effects.Exists(e=>e.Id=="mushrooms");
            float amount=session.Profile.Data.ReducedMotion?.025f:.055f;
            var color=lsd?Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime*.035f,1),.7f,1):new Color(.25f,.6f,.3f);
            effectWash.color=new Color(color.r,color.g,color.b,amount);
            if(session.ViewCamera!=null)session.ViewCamera.fieldOfView=mushrooms&&!session.Profile.Data.ReducedMotion?baseFov+Mathf.Sin(Time.unscaledTime*.8f)*1.2f:baseFov;
        }

        private void UpdateActions(RoundState state,PlayerState player)
        {
            if(Time.unscaledTime<nextActionRefresh)return;
            nextActionRefresh=Time.unscaledTime+.2f;
            activeActionCount=0;primaryAction=null;
            float y=.89f;
            if(player.CampVisitId!="")
            {
                var site=CampFeatures.Find(player.CampVisitId);
                string antic=site?.Kind=="Car"?"HONK / FIDDLE":site?.Kind=="Tent"?"SHADOW PUPPET":"MYSTERY FLUSH";
                SetPromptAction("E  EXIT "+(site?.Kind??"CAMP SPACE").ToUpperInvariant()+"   •   F "+antic+"   •   G EXIT",()=>session.Command("ExitCamp"));
                FinishActions();return;
            }
            if(state.Phase=="CampReview")
            {
                var voted=state.ReviewVotes.Find(v=>v.PlayerId==player.Id)!=null;
                if(voted&&session.IsHost&&ConnectedReviewVotes(state)>=state.ConnectedCrewCount)
                    SetPromptAction("E  OPEN CAMP SHOP",()=>session.Command("FinishReview"));
                else SetPromptAction(voted?"WAIT FOR CREW VOTES":"1–3  VOTE FOR A SILLY AWARD",null);
                FinishActions();return;
            }
            if(state.Phase=="Shopping")
            {
                if(Near(player,CampFeatures.DjX,CampFeatures.DjZ,3f))
                {
                    int next=(state.CampMusicTrack+1)%CampFeatures.Tracks.Length;
                    SetPromptAction("E  DJ: "+CampFeatures.Tracks[next],()=>session.Command("ChooseCampTrack",amount:next));
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
                    if(checkoutItem==held.Id)SetPromptAction("E  PAY $"+held.Price+" FOR "+held.Name.ToUpperInvariant(),()=>{session.Command("Buy",item:held.Id);checkoutItem="";});
                    else SetPromptAction("E  SHOW "+held.Name.ToUpperInvariant()+" TO SELLER",()=>checkoutItem=held.Id);
                }
                else if(Near(player,0,19,3.2f))SetPromptAction(player.Ready?"E  UNREADY AT TRAILHEAD":"E  READY AT TRAILHEAD",()=>session.Command("Ready"));
                else if(player.HeldOfferId!="")SetPromptAction("UNPAID  "+Catalog.FindItem(player.HeldOfferId)?.Name.ToUpperInvariant()+"  •  SELLER AHEAD  •  G RETURN",null);
                else
                {
                    string itemId=FocusedOffer(state,player);
                    if(itemId!="")
                    {
                        var item=Catalog.FindItem(itemId);var stock=state.ShopStock.Find(s=>s.ItemId==itemId);
                        SetPromptAction(stock!=null&&stock.CampAvailable>0?"E  PICK UP "+item.Name.ToUpperInvariant()+"  •  $"+item.Price+"  •  "+item.Description:item.Name.ToUpperInvariant()+"  •  SOLD OUT",stock!=null&&stock.CampAvailable>0?()=>session.Command("HoldOffer",item:itemId):null);
                    }
                    else SetPromptAction("GEAR SHELVES BESIDE SELLER  •  READY AT LIT TRAILHEAD",null);
                }
                FinishActions();return;
            }
            if(state.Phase=="Playing"&&player.Life=="Alive")
            {
                string focused=FocusedOffer(state,player);
                if(focused!="")
                {
                    var item=Catalog.FindItem(focused);var stock=state.ShopStock.Find(s=>s.ItemId==focused);
                    if(stock!=null&&stock.MarketAvailable>0)
                    {
                        if(checkoutItem==focused)SetPromptAction("E  PAY $"+item.Price+" FOR "+item.Name.ToUpperInvariant(),()=>{session.Command("Buy",item:focused);checkoutItem="";});
                        else SetPromptAction("E  BUY "+item.Name.ToUpperInvariant()+"  •  $"+item.Price+"  •  "+item.Description,()=>checkoutItem=focused);
                    }
                    else SetPromptAction(item.Name.ToUpperInvariant()+"  •  SOLD OUT",null);
                    FinishActions();return;
                }
            }
            if(state.Phase=="Playing"&&(player.Life=="Downed"||player.Life=="Detained"))AddAction(player.Life=="Downed"?"Make a scene to distract attackers":"Distract security / work on escape",()=>session.Command("HelpSelf"),ref y);
            if(player.InteractionId!="")AddAction("Cancel current action",()=>session.Command("Cancel"),ref y);
            var offer=state.Transfers.Find(t=>t.ToId==player.Id);if(offer!=null)AddAction("Accept "+offer.ItemId+" ×"+offer.Amount,()=>session.Command("AcceptTransfer",offer.Id),ref y);
            var drop=Nearest(state.Drops,player.X,player.Z,2.5f);if(drop!=null)AddAction("Pick up "+drop.ItemId,()=>session.Command("Pickup",drop.Id),ref y);
            var stash=Nearest(state.Stashes,player.X,player.Z,2.7f);
            if(stash!=null)
            {
                string stashId=stash.Id;
                if(player.Cash>=5)AddAction("Deposit $5 in shared stash",()=>session.Command("Deposit",stashId,amount:5),ref y);
                if(state.StashCash>=5)AddAction("Withdraw $5 from shared stash",()=>session.Command("Withdraw",stashId,amount:5),ref y);
                if(handGear.Count>0)
                {
                    string item=handGear[Mathf.Clamp(selectedSlot,0,handGear.Count-1)].ItemId;
                    AddAction("Deposit "+item,()=>session.Command("Deposit",stashId,item,1),ref y);
                }
                var stored=stash.Items.Find(i=>i.Count>0);
                if(stored!=null){string item=stored.ItemId;AddAction("Withdraw "+item,()=>session.Command("Withdraw",stashId,item,1),ref y);}
            }
            foreach(var mate in state.Players)
            {
                if(mate.Id==player.Id||Distance(player.X,player.Z,mate.X,mate.Z)>2.5f)continue;
                if(mate.Life=="Downed"){AddAction("Rescue "+mate.Name,()=>session.Command("Rescue",mate.Id),ref y);AddAction("Drag "+mate.Name,()=>session.Command("Drag",mate.Id),ref y);}
                if(mate.Life=="Detained"&&Near(player,27,5))AddAction("Pay $10 release for "+mate.Name,()=>session.Command("BeginRelease",mate.Id,amount:1),ref y);
                if(handGear.Count>0){string item=handGear[Mathf.Clamp(selectedSlot,0,handGear.Count-1)].ItemId;AddAction("Offer "+item+" to "+mate.Name,()=>session.Command("Transfer",mate.Id,item,1),ref y);}
            }
            if(player.Life=="Spirit")
            {
                if(Near(player,24,-20)&&state.ConnectedCrewCount==1&&player.RevivalCount<2)AddAction("Self revive at medical (15 seconds)",()=>session.Command("BeginRevival",player.Id),ref y);
                if(Near(player,24,-20))AddAction("Memorial chime",()=>session.Command("Chime"),ref y);
            }
            else if(state.Phase=="Playing"&&player.Life=="Alive")
            {
                if(!state.GateOpened&&Near(player,-18,-22))AddAction("Volunteer for free clue tasting",()=>session.Command("ClueSupply"),ref y);
                var clue=FestivalSimulation.CluePoint(state.Seed,state.CluesRead);
                if(state.CluesRead<2&&Near(player,clue.X,clue.Z)&&FestivalSimulation.CanReadClues(player))AddAction(state.ConnectedCrewCount==1?"Interpret totem alone (6 seconds)":"Interpret totem with sober friend",()=>session.Command("ReadClue"),ref y);
                if(state.FriendPosition!=null&&(state.FriendPosition.X!=0||state.FriendPosition.Z!=0)&&Near(player,state.FriendPosition.X,state.FriendPosition.Z)&&(!state.FriendFound||state.FriendLeaderId!=player.Id))
                    AddAction(state.FriendFound?"Take over friend escort":"Recruit missing friend",()=>session.Command("FindFriend"),ref y);
                if(state.FriendFound&&Near(player,0,-32))AddAction("Extract at last shuttle",()=>session.Command("Extract"),ref y);
                if(Near(player,-28,16))
                {
                    var detained=state.Players.Find(p=>p.Life=="Detained");
                    if(detained!=null)AddAction("Free release task for "+detained.Name,()=>session.Command("BeginRelease",detained.Id),ref y);
                    AddAction("Return lost property for $5",()=>session.Command("LostProperty"),ref y);
                }
                if(Near(player,24,-20))
                {
                    var spirit=state.Players.Find(p=>p.Life=="Spirit"&&player.Wristbands.Contains(p.Id));
                    if(spirit!=null){AddAction("Revive "+spirit.Name+" — free task",()=>session.Command("BeginRevival",spirit.Id),ref y);AddAction("Revive "+spirit.Name+" — pay $10",()=>session.Command("BeginRevival",spirit.Id,amount:1),ref y);}
                    if(player.Effects.Count>0&&player.Inventory.Exists(i=>i.ItemId=="medical_voucher"))AddAction("Use medical voucher",()=>session.Command("Use",item:"medical_voucher"),ref y);
                }
                if(Near(player,0,26)&&player.Inventory.Exists(i=>i.ItemId=="stage_pass"))AddAction("Start DJ takeover",()=>session.Command("Dj"),ref y);
                var npc=Nearest(state.Npcs,player.X,player.Z,2.5f);
                if(npc!=null)
                {
                    if(npc.Kind=="Cop")AddAction("Talk to security",()=>session.Command("Police",npc.Id),ref y);
                    else
                    {
                        AddAction(npc.CanTalk?"Dance with festivalgoer  •  F CHAT":"Dance with festivalgoer",()=>session.Command("Dance",npc.Id),ref y);
                        if(npc.CanTalk){AddAction("Chat with festivalgoer",()=>session.Command("Talk",npc.Id),ref y);AddAction("Talk and keep the beat",()=>session.Command("Conversation",npc.Id),ref y);}
                        var stock=player.Inventory.Find(i=>i.ItemId=="stock_lsd"||i.ItemId=="stock_mushrooms");
                        if(stock!=null)AddAction("Offer "+stock.ItemId,()=>session.Command("StartSale",npc.Id,stock.ItemId),ref y);
                    }
                }
            }
            prompt.text=primaryAction==null?"":dynamicActions.Find(g=>g.name.StartsWith("Action:"))?.name.Substring(7);
            FinishActions();
        }

        private void AddAction(string label,Action action,ref float y)
        {
            GameObject item;
            if(activeActionCount<dynamicActions.Count)
            {
                item=dynamicActions[activeActionCount];item.SetActive(true);
                item.name="Action:"+label;
                item.transform.Find("Text").GetComponent<Text>().text=label;
                var control=item.GetComponent<Button>();control.onClick.RemoveAllListeners();control.onClick.AddListener(()=>action());
            }
            else
            {
                item=Button(actionsContent,label,Vector2.zero,Vector2.one,action);item.name="Action:"+label;
                item.AddComponent<LayoutElement>().preferredHeight=64;
                dynamicActions.Add(item);
            }
            activeActionCount++;
            if(primaryAction==null)primaryAction=action;y-=.09f;
        }
        private void FinishActions(){for(int i=activeActionCount;i<dynamicActions.Count;i++)dynamicActions[i].SetActive(false);}
        private void SetPromptAction(string label,Action action){prompt.text=label.StartsWith("E  ")?label.Substring(3):label;primaryAction=action;}
        private string FocusedOffer(RoundState state,PlayerState player)
        {
            if(state==null||player==null||session.ViewCamera==null||!(state.Phase=="Shopping"||state.Phase=="Playing"))return "";
            bool camp=state.Phase=="Shopping";
            Vector3 origin=session.ViewCamera.transform.position,forward=session.ViewCamera.transform.forward;
            string best="";float bestScore=.77f;
            for(int i=0;i<state.VendorOffers.Count;i++)
            {
                var point=Catalog.ShopPoint(camp,i);
                if(Distance(player.X,player.Z,point.X,point.Z)>3.3f)continue;
                var target=new Vector3(point.X,i<4?1.17f:2.05f,camp?9.0f:i<4?-21.15f:-20.05f);
                var delta=target-origin;float score=Vector3.Dot(forward,delta.normalized);
                if(score>bestScore){bestScore=score;best=state.VendorOffers[i];}
            }
            return best;
        }
        private void ChangeSetting(string key,int direction)
        {
            var data=session.Profile.Data;
            if(key=="music")data.MusicVolume=Mathf.Clamp(data.MusicVolume+direction*.05f,0,.7f);
            if(key=="look")data.MouseSensitivity=Mathf.Clamp(data.MouseSensitivity+direction*.02f,.02f,.5f);
            if(key=="motion")data.ReducedMotion=!data.ReducedMotion;
            if(key=="contrast")data.HighContrast=!data.HighContrast;
            string error=session.Profile.Save();if(error!="")session.SetMessage(error);
        }
        private void UpdatePurchaseFeedback(RoundState state,PlayerState player)
        {
            int units=0;foreach(var item in player.Inventory)units+=item.Count;
            if(previousRound==state.RoundId&&previousCash>=0&&player.Cash<previousCash&&units>previousUnits)
            {
                shopAudio.PlayOneShot(shopChime,Mathf.Clamp01(session.Profile.Data.MusicVolume+.25f));
                session.SetMessage("Oh, hey, thanks!  •  POCKET UPDATED");
            }
            previousRound=state.RoundId;previousCash=player.Cash;previousUnits=units;
        }
        private void ApplyContrast(bool high)
        {
            if(appliedContrast==high)return;appliedContrast=high;
            promptPanel.GetComponent<Image>().color=high?new Color(.01f,.025f,.03f,1):new Color(.025f,.064f,.071f,.95f);
            rosterPanel.GetComponent<Image>().color=high?new Color(.01f,.025f,.03f,1):new Color(.025f,.064f,.071f,.95f);
            reticle.GetComponent<Image>().color=high?new Color(1,.95f,.71f,1):new Color(.88f,.90f,.74f,.65f);
        }
        private void OnDestroy(){if(shopChime!=null)Destroy(shopChime);if(discSprite!=null)Destroy(discSprite);if(discTexture!=null)Destroy(discTexture);if(roundedSprite!=null)Destroy(roundedSprite);if(roundedTexture!=null)Destroy(roundedTexture);if(arrowSprite!=null)Destroy(arrowSprite);if(arrowTexture!=null)Destroy(arrowTexture);if(arrowOutlineSprite!=null)Destroy(arrowOutlineSprite);if(arrowOutlineTexture!=null)Destroy(arrowOutlineTexture);}

        private void StartHost(){if(!ushort.TryParse(portField.text,out var port)||port==0){connectionError="Port must be 1–65535.";return;}connectionError="";session.Host(nameField.text,port);}
        private void StartJoin(){if(!ushort.TryParse(portField.text,out var port)||port==0){connectionError="Port must be 1–65535.";return;}connectionError="";session.Join(nameField.text,addressField.text,port);}
        private static int ConnectedReviewVotes(RoundState state)
        {
            int votes=0;
            foreach(var crew in state.Players)
                if(crew.Connected&&state.ReviewVotes.Exists(v=>v.PlayerId==crew.Id))votes++;
            return votes;
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
        private void Rule(Transform parent,float y)
        {
            var go=Panel(parent,"Divider",Hairline,new Vector2(.04f,y),new Vector2(.96f,y+.003f));
            go.GetComponent<Image>().raycastTarget=false;
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
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=font;text.fontSize=size<=25?Mathf.RoundToInt(size*1.18f):size;text.alignment=anchor;text.color=Paper;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;return text;
        }
        private GameObject Button(Transform parent,string label,Vector2 min,Vector2 max,Action action)
        {
            bool primary=label=="RESUME FESTIVAL"||label=="CREATE GAME"||label=="JOIN GAME"||label=="NEXT CAMP";
            bool danger=label=="LEAVE SESSION";
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
            var text=Label(fieldObject.transform,"Text",24,TextAnchor.MiddleLeft);Fill(text.rectTransform,18);text.color=Ink;text.text=value;input.textComponent=text;input.text=value;input.characterLimit=128;
            var placeholder=Label(fieldObject.transform,"Placeholder",22,TextAnchor.MiddleLeft);Fill(placeholder.rectTransform,18);placeholder.text=label;placeholder.color=new Color(.35f,.45f,.40f,.6f);input.placeholder=placeholder;
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
