using System;
using System.Collections.Generic;
using Festival.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>
    /// DIALOG-5: a conversation as a chat thread, the card every conversation uses: the tripper's chat check and the VIP rope chat
    /// now (Follow), DIALOG-3's talks and CAMP-1's camp seller next (Begin, TheySay, YouSay, Offer). Under a header naming who you're
    /// talking to and a thin bar for the time left, their lines sit on the left marked with their initial, and yours on the right
    /// in the accent orange, marked YOU. Under the thread, the replies you can pick, each a button with its key cap: 1-3 or a click
    /// picks one (Pick), and one already picked dims. A line of theirs waits behind a "…" bubble for TypingSeconds, then types out at
    /// LettersPerSecond, DIALOG-2's pace (reduced motion shows it whole). The thread keeps the last MaxBubbles bubbles and fades
    /// older ones out. It lives here on the client, so it outlasts the interaction that fed it: a view carries only Active ones.
    /// Time is handed in (Begin, Advance), so tests read the thread at exact moments.
    /// </summary>
    public sealed class FestivalChatWindow : MonoBehaviour
    {
        public const int MaxBubbles=6,MaxReplies=3;
        public const float TypingSeconds=.5f,LettersPerSecond=30,FadeSeconds=.35f;
        public const string Ellipsis="…";
        // The HUD's palette (FestivalHud): a paper card with an orange accent, ink text, their bubbles sage, yours orange.
        static readonly Color Paper=new Color(.948f,.928f,.852f,1),Ink=new Color(.035f,.065f,.075f,1),Orange=new Color(.99f,.465f,.255f,1),Hairline=new Color(.51f,.56f,.50f,.35f);
        static readonly Color TheirBubble=new Color(.83f,.87f,.80f,1),TheirMark=new Color(.12f,.45f,.39f,1),ReplyColor=new Color(.84f,.85f,.76f,1),Dimmed=new Color(.035f,.065f,.075f,.4f);
        // Bubble geometry in canvas pixels: the mark beside each bubble, the text's padding, and the gap between bubbles.
        const float MarkWidth=36,MarkHeight=26,MarkGap=8,PadX=12,PadY=7,Spacing=7;
        // The thread's foot sits above the replies when there are some, else near the card's foot.
        const float ThreadTop=.87f,ThreadOverReplies=.295f,ThreadAlone=.03f;
        // Their typed line's hidden rest keeps its place, so the bubble doesn't grow as the letters come.
        const string Hidden="<color=#00000000>";

        sealed class Bubble{public GameObject Root;public RectTransform Back,Mark;public Image BackImage,MarkImage;public Text Line,MarkText;public CanvasGroup Group;public string Said="";public bool Theirs,Whole;public float At,FadeFrom;public int Letters;}
        readonly List<Bubble> thread=new List<Bubble>(),fading=new List<Bubble>(),spare=new List<Bubble>();
        readonly Button[] replies=new Button[MaxReplies];
        readonly Text[] replyTexts=new Text[MaxReplies],replyKeys=new Text[MaxReplies];
        readonly TextGenerator measure=new TextGenerator();
        Font font;Sprite rounded;Text header;RectTransform bar,viewport,content;
        Action<int> onPick;int offered;
        string threadId="",mark="";double followedStart=-1;
        float clock,laidWidth=-1;bool dirty,reduced;

        /// <summary>Builds the window, hidden, on the HUD's canvas where the chat card was: right of centre, between the crew card
        /// and the gear bar (FestivalHud moves it beside the pass while the menu is open).</summary>
        public static FestivalChatWindow Build(Transform parent,Font font,Sprite rounded)
        {
            var root=Panel(parent,"Chat window",Paper,rounded);root.GetComponent<Image>().raycastTarget=true;
            var shadow=root.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,.015f,.02f,.46f);shadow.effectDistance=new Vector2(0,-8);shadow.useGraphicAlpha=true;
            Place(root,.60f,.155f,.975f,.652f);
            var window=root.gameObject.AddComponent<FestivalChatWindow>();window.font=font;window.rounded=rounded;
            Place(Panel(root,"Accent rule",Orange,null),.025f,.974f,.975f,.982f);
            window.header=window.Label(root,"Chat header",21,TextAnchor.MiddleLeft,Ink);Place(window.header.rectTransform,.05f,.895f,.95f,.968f);
            var track=Panel(root,"Time left track",Hairline,null);Place(track,.05f,.879f,.95f,.887f);
            window.bar=Panel(track,"Time left",Orange,null);Place(window.bar,0,0,1,1);
            window.viewport=Panel(root,"Thread",Color.clear,null);Place(window.viewport,.04f,ThreadAlone,.96f,ThreadTop);
            window.viewport.gameObject.AddComponent<RectMask2D>();
            window.content=Panel(window.viewport,"Bubbles",Color.clear,null);Place(window.content,0,0,1,1);
            for(int i=0;i<MaxReplies;i++)
            {
                // Top to bottom, 1 to 3, each 40 px high at 1080p.
                float bottom=.03f+(MaxReplies-1-i)*.085f;
                var reply=Panel(root,"Reply "+(i+1),ReplyColor,rounded);Place(reply,.04f,bottom,.96f,bottom+.075f);
                var image=reply.GetComponent<Image>();image.raycastTarget=true;
                var button=reply.gameObject.AddComponent<Button>();button.targetGraphic=image;
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.11f,1.13f,1.10f,1);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.78f,.84f,.79f,1);colors.disabledColor=new Color(.55f,.58f,.55f,.65f);colors.fadeDuration=.12f;button.colors=colors;
                var outline=reply.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.08f,.22f,.19f,.16f);outline.effectDistance=new Vector2(1,-1);
                reply.gameObject.AddComponent<FestivalFocusFrame>().Configure(outline,new Color(.13f,.55f,.46f,1),outline.effectColor);
                int index=i;button.onClick.AddListener(()=>window.Pick(index));
                var key=Panel(reply,"Key cap",Orange,rounded);Pixels(key,new Vector2(0,0),new Vector2(0,1),new Vector2(6,5),new Vector2(36,-5));
                window.replyKeys[i]=window.Label(key,"Key",21,TextAnchor.MiddleCenter,Ink);window.replyKeys[i].text=(i+1).ToString();Place(window.replyKeys[i].rectTransform,0,0,1,1);
                var question=window.Label(reply,"Question",19,TextAnchor.MiddleLeft,Ink);Pixels(question.rectTransform,Vector2.zero,Vector2.one,new Vector2(46,1),new Vector2(-8,-1));
                question.resizeTextForBestFit=true;question.resizeTextMinSize=12;question.resizeTextMaxSize=question.fontSize;
                window.replies[i]=button;window.replyTexts[i]=question;
            }
            window.Offer(null,null);
            root.gameObject.SetActive(false);
            return window;
        }

        /// <summary>Seconds from when they start saying a line (the "…" bubble) until all of it shows.</summary>
        public static float Typed(string line)=>TypingSeconds+line.Length/LettersPerSecond;
        /// <summary>The chat check (or VIP rope chat) p is in right now; null without one. A view carries only its own player's
        /// interactions, and only Active ones, so no other client can read it.</summary>
        public static InteractionState LiveChat(RoundState s,PlayerState p){foreach(var i in s.Interactions)if(i.Id==p.InteractionId&&i.Kind=="ConfirmChat"&&i.Status=="Active")return i;return null;}
        /// <summary>Whether 1-3 answer the window this frame instead of equipping: only while p's live chat has questions to ask, so a
        /// rope chat, which has none, never swallows them, nor does a chat that has just ended.</summary>
        public static bool TakesReplies(RoundState s,PlayerState p){var chat=LiveChat(s,p);return chat!=null&&chat.Chat.Questions.Length>0;}
        /// <summary>Who you're talking to: "FESTIVALGOER  •  WOOK" by their persona (DialogueGrammar.PersonaFor), "VIP GUARD" for
        /// Palm Mirage's guard, and "VIP GUARD  •  TALKING YOUR WAY IN" for the rope chat, which checks nothing (POLO-4).</summary>
        public static string HeaderFor(RoundState s,InteractionState chat){string who=Speaker(s,chat);return who!="VIP GUARD"?"FESTIVALGOER  •  "+who:chat.Chat.Questions.Length==0?who+"  •  TALKING YOUR WAY IN":who;}
        static string Speaker(RoundState s,InteractionState chat)
        {
            NpcState npc=null;foreach(var n in s.Npcs)if(n.Id==chat.TargetId)npc=n;
            if(npc!=null&&npc.Twist==FestivalSimulation.VipGuard)return "VIP GUARD";
            string persona=DialogueGrammar.PersonaFor(chat.TargetId,npc?.Twist??"");
            return persona=="DraggedAlongDad"?"DRAGGED-ALONG DAD":persona.ToUpperInvariant();
        }

        public bool Open=>gameObject.activeSelf;
        public string Header=>header.text;
        /// <summary>The time bar's fill, 1 at the start of the conversation and 0 at its end.</summary>
        public float TimeLeft=>bar.anchorMax.x;
        /// <summary>The bubbles in the thread, oldest first: at most MaxBubbles. ShownBubbles also counts older ones still fading.</summary>
        public int Bubbles=>thread.Count;
        public int ShownBubbles=>thread.Count+fading.Count;
        public bool IsTheirs(int i)=>thread[i].Theirs;
        /// <summary>What bubble i shows now: their "…", their line typing (its rest hidden), or the whole line.</summary>
        public string Shown(int i)=>thread[i].Line.text;
        public int Replies=>offered;
        public string ReplyKey(int i)=>replyKeys[i].text;
        public string ReplyLabel(int i)=>replyTexts[i].text;
        public bool CanReply(int i)=>i>=0&&i<offered&&replies[i].interactable;

        /// <summary>Shows the window on a conversation at `now`. A new thread id clears the thread and the replies; the same one
        /// shows the thread as it was, so a conversation survives a frame or two without its interaction in the view.</summary>
        public void Begin(string threadId,string header,string mark,float now)
        {
            clock=now;gameObject.SetActive(true);this.header.text=header;
            if(threadId==this.threadId)return;
            this.threadId=threadId;this.mark=mark;
            foreach(var b in thread)Recycle(b);foreach(var b in fading)Recycle(b);thread.Clear();fading.Clear();
            Offer(null,null);dirty=true;
        }
        /// <summary>Their line, on the left: whole at once, or typed, after the "…" bubble, at LettersPerSecond.</summary>
        public void TheySay(string line,bool typed){var b=Take(true,line);b.Whole=!typed;Show(b);Add(b);}
        /// <summary>Your line, on the right. Whatever they were still typing shows whole first, so the thread reads in order.</summary>
        public void YouSay(string line)
        {
            foreach(var b in thread)if(b.Theirs&&!b.Whole){b.Whole=true;Show(b);}
            var yours=Take(false,line);yours.Whole=true;Show(yours);Add(yours);
        }
        /// <summary>The replies under the thread, up to MaxReplies, all pickable; null or none hides them. `pick` runs with the
        /// reply's index when 1-3 or a click picks it.</summary>
        public void Offer(string[] lines,Action<int> pick)
        {
            offered=lines==null?0:Mathf.Min(lines.Length,MaxReplies);onPick=pick;
            for(int i=0;i<MaxReplies;i++)
            {
                replies[i].gameObject.SetActive(i<offered);replies[i].interactable=true;
                replyTexts[i].text=i<offered?lines[i]:"";replyTexts[i].color=Ink;replyKeys[i].color=Ink;
            }
            Place(viewport,.04f,offered>0?ThreadOverReplies:ThreadAlone,.96f,ThreadTop);dirty=true;
        }
        /// <summary>Picks reply `index` (a 1-3 key or a click). It dims and can't be picked again. False, doing nothing, for a reply
        /// that isn't offered or was already picked.</summary>
        public bool Pick(int index)
        {
            if(!CanReply(index))return false;
            replies[index].interactable=false;replyTexts[index].color=Dimmed;replyKeys[index].color=Dimmed;
            onPick?.Invoke(index);
            return true;
        }
        /// <summary>Each frame: types their lines on, fades out bubbles past the last MaxBubbles, fills the time bar (0-1) and lays
        /// the thread out from the foot up when it changed. Allocates only as a typed line gains a letter.</summary>
        public void Advance(float now,float timeLeft,bool reducedMotion)
        {
            clock=now;reduced=reducedMotion;
            bar.anchorMax=new Vector2(Mathf.Clamp01(timeLeft),1);
            foreach(var b in thread)Show(b);
            for(int i=fading.Count-1;i>=0;i--)
            {
                var b=fading[i];float alpha=1-(clock-b.FadeFrom)/FadeSeconds;
                if(alpha>0){b.Group.alpha=alpha;continue;}
                Recycle(b);fading.RemoveAt(i);dirty=true;
            }
            if(content.rect.width!=laidWidth)dirty=true;
            if(dirty)Layout();
        }
        /// <summary>Hides the window. The thread stays, so the same conversation shows again as it was.</summary>
        public void Close()=>gameObject.SetActive(false);

        /// <summary>
        /// Follows p's chat check (or VIP rope chat) in their view: their opener, then the three questions as replies; picking one
        /// says it on your side and types their answer to it. A rope chat is the guard's opener and doorman line with nothing to
        /// ask. The bar runs down the chat's time at `simulationSeconds`. Hides the window and returns false with no chat.
        /// </summary>
        public bool Follow(RoundState s,PlayerState p,double simulationSeconds,float now,bool reducedMotion)
        {
            var live=LiveChat(s,p);
            if(live==null){Close();return false;}
            // Keyed on the start time too: interaction ids start over in a new session, but this window may outlive one.
            if(live.Id!=threadId||live.StartSeconds!=followedStart)
            {
                var chat=live.Chat;followedStart=live.StartSeconds;threadId="";
                Begin(live.Id,HeaderFor(s,live),Speaker(s,live).Substring(0,1),now);
                TheySay(chat.Opener,false);
                if(chat.Questions.Length==0)TheySay(chat.Doorman,true);
                else Offer(chat.Questions,i=>{YouSay(chat.Questions[i]);TheySay(chat.Answers[i],true);});
            }
            else Begin(threadId,header.text,mark,now);
            Advance(now,live.DurationSeconds>0?(float)(1-(simulationSeconds-live.StartSeconds)/live.DurationSeconds):0,reducedMotion);
            return true;
        }

        void Add(Bubble b)
        {
            thread.Add(b);dirty=true;
            while(thread.Count>MaxBubbles){var old=thread[0];thread.RemoveAt(0);old.FadeFrom=clock;fading.Add(old);}
        }
        // What b shows at the clock: their "…" for TypingSeconds, then the letters typed so far with the rest hidden, then the whole
        // line. Swapping "…" for the line resizes the bubble; the letters coming in don't.
        void Show(Bubble b)
        {
            int letters=b.Said.Length;
            if(!b.Whole)
            {
                if(clock<b.At+TypingSeconds)letters=-1;
                else if(!reduced&&clock<b.At+Typed(b.Said))letters=Mathf.Min(b.Said.Length,(int)((clock-b.At-TypingSeconds)*LettersPerSecond));
                else b.Whole=true;
            }
            if(letters==b.Letters)return;
            if(b.Letters<0||letters<0)dirty=true;
            b.Letters=letters;
            b.Line.text=letters<0?Ellipsis:letters>=b.Said.Length?b.Said:b.Said.Substring(0,letters)+Hidden+b.Said.Substring(letters)+"</color>";
        }
        // Newest at the foot, each bubble as wide as its line up to the thread's width less the mark and an indent on the far side.
        // Bubbles past the thread's top are clipped (RectMask2D).
        void Layout()
        {
            dirty=false;laidWidth=content.rect.width;float y=0;
            for(int i=thread.Count-1;i>=0;i--)y=Place(thread[i],y)+Spacing;
            for(int i=fading.Count-1;i>=0;i--)y=Place(fading[i],y)+Spacing;
        }
        float Place(Bubble b,float y)
        {
            string sizing=b.Letters<0?Ellipsis:b.Said;float units=b.Line.pixelsPerUnit;
            float widest=laidWidth-MarkWidth-MarkGap-laidWidth*.14f-2*PadX;
            float textWidth=Mathf.Min(measure.GetPreferredWidth(sizing,b.Line.GetGenerationSettings(Vector2.zero))/units+1,widest);
            float height=measure.GetPreferredHeight(sizing,b.Line.GetGenerationSettings(new Vector2(textWidth,0)))/units+2*PadY;
            float width=textWidth+2*PadX,x=b.Theirs?MarkWidth+MarkGap:laidWidth-MarkWidth-MarkGap-width;
            b.Back.anchoredPosition=new Vector2(x,y);b.Back.sizeDelta=new Vector2(width,height);
            b.Mark.anchoredPosition=new Vector2(b.Theirs?0:laidWidth-MarkWidth,y);b.Mark.sizeDelta=new Vector2(MarkWidth,MarkHeight);
            return y+height;
        }
        Bubble Take(bool theirs,string line)
        {
            Bubble b;
            if(spare.Count>0){b=spare[spare.Count-1];spare.RemoveAt(spare.Count-1);b.Root.SetActive(true);}
            else
            {
                b=new Bubble{Root=new GameObject("Bubble",typeof(RectTransform),typeof(CanvasGroup))};b.Root.transform.SetParent(content,false);Place((RectTransform)b.Root.transform,0,0,1,1);
                b.Group=b.Root.GetComponent<CanvasGroup>();b.Group.blocksRaycasts=false;
                b.Back=Panel(b.Root.transform,"Back",TheirBubble,rounded);Corner(b.Back);b.BackImage=b.Back.GetComponent<Image>();
                b.Line=Label(b.Back,"Line",21,TextAnchor.UpperLeft,Ink);Pixels(b.Line.rectTransform,Vector2.zero,Vector2.one,new Vector2(PadX,PadY),new Vector2(-PadX,-PadY));
                b.Mark=Panel(b.Root.transform,"Mark",TheirMark,rounded);Corner(b.Mark);b.MarkImage=b.Mark.GetComponent<Image>();
                b.MarkText=Label(b.Mark,"Who",16,TextAnchor.MiddleCenter,Paper);Place(b.MarkText.rectTransform,0,0,1,1);
            }
            b.Theirs=theirs;b.Said=line??"";b.At=clock;b.Whole=false;b.Letters=-2;b.FadeFrom=0;b.Group.alpha=1;
            b.BackImage.color=theirs?TheirBubble:Orange;b.MarkImage.color=theirs?TheirMark:Ink;
            b.MarkText.text=theirs?mark:"YOU";b.MarkText.color=theirs?Paper:Orange;b.MarkText.fontSize=theirs?16:13;
            return b;
        }
        void Recycle(Bubble b){b.Root.SetActive(false);spare.Add(b);}

        static RectTransform Panel(Transform parent,string name,Color color,Sprite sprite)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
            if(sprite!=null){image.sprite=sprite;image.type=Image.Type.Sliced;}
            return (RectTransform)go.transform;
        }
        Text Label(Transform parent,string name,int size,TextAnchor anchor,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.alignment=anchor;text.color=color;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;
            return text;
        }
        static void Place(RectTransform rect,float xmin,float ymin,float xmax,float ymax){rect.anchorMin=new Vector2(xmin,ymin);rect.anchorMax=new Vector2(xmax,ymax);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static void Pixels(RectTransform rect,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;}
        // Bubbles and marks are placed in pixels from the thread's foot-left corner.
        static void Corner(RectTransform rect){rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;}
    }
}
