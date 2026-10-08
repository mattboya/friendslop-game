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
    /// <summary>One bounded delivery and reveal clock shared by world speech, audio and dialogue cards.
    /// Public literals outlive world expiry for a card that has already started reading them; retraction alone erases them.</summary>
    public sealed class FestivalSpeechDeliveryTracker
    {
        public const float LettersPerSecond=30;
        public sealed class Delivery
        {
            public string Id {get;internal set;}="";
            public string SpeakerId {get;internal set;}="";
            public string Speaker {get;internal set;}="";
            public string Text {get;internal set;}="";
            public long Sequence {get;internal set;}
            public bool IsPublic {get;internal set;}
            public bool Retracted {get;internal set;}
            public bool WorldActive {get;internal set;}
            public int VisibleCharacters {get;internal set;}
            public int CharacterCount {get;internal set;}
            public double AgeSeconds {get;internal set;}
            internal double Started,Expires;
        }
        readonly List<Delivery> deliveries=new List<Delivery>();
        readonly Queue<string> retiredLocal=new Queue<string>();
        string round="";
        int generation=-1;
        long publicSequence;
        double worldClock;
        public IReadOnlyList<Delivery> Deliveries=>deliveries;
        public double WorldSeconds=>worldClock;
        public Delivery Find(string id)=>deliveries.Find(d=>d.Id==id);
        /// <summary>Observe the filtered window, including expired literals and same-ID tombstones. Text/time never supplies identity.</summary>
        public void Follow(RoundState state,int connectionGeneration,double worldSeconds)
        {
            if(state==null){Retire();return;}
            if(generation!=connectionGeneration){Retire();generation=connectionGeneration;}
            if(round!=state.RoundId){Retire();round=state.RoundId;publicSequence=0;worldClock=0;retiredLocal.Clear();}
            worldClock=Math.Max(worldClock,worldSeconds);
            long newest=publicSequence;
            if(state.PublicSpeech!=null)foreach(var speech in state.PublicSpeech)
            {
                var delivery=Find(speech.DeliveryId);
                if(delivery==null)
                {
                    if(speech.Sequence<=publicSequence||string.IsNullOrEmpty(speech.DeliveryId)||!speech.DeliveryId.StartsWith(round+":speech:",StringComparison.Ordinal))continue;
                    delivery=Add(speech.DeliveryId,speech.SpeakerId,speech.Speaker,speech.Text,speech.StartedAtWorldSeconds,speech.ExpiresAtWorldSeconds,true,speech.Sequence);
                }
                if(speech.Retracted){delivery.Retracted=true;delivery.Text="";}
                newest=Math.Max(newest,speech.Sequence);
            }
            publicSequence=newest;
            foreach(var delivery in deliveries)Reveal(delivery);
        }
        /// <summary>Owner-only adapters register local identities here; this never publishes or changes a snapshot/projection.</summary>
        public Delivery RegisterLocal(string id,string speakerId,string speaker,string text,double started,double expires)
        {
            var existing=Find(id);if(existing!=null)return existing;
            if(string.IsNullOrEmpty(id)||retiredLocal.Contains(id))return null;
            var delivery=Add(id,speakerId,speaker,text,started,expires,false,0);Reveal(delivery);return delivery;
        }
        public void RetractLocal(string id)
        {
            var delivery=Find(id);if(delivery==null||delivery.IsPublic)return;
            delivery.Retracted=true;delivery.Text="";Reveal(delivery);
        }
        Delivery Add(string id,string speakerId,string speaker,string text,double started,double expires,bool isPublic,long sequence)
        {
            int count=0;foreach(var d in deliveries)if(d.IsPublic==isPublic)count++;
            if(count>=FestivalSimulation.PublicSpeechLimit)
            {
                int at=deliveries.FindIndex(d=>d.IsPublic==isPublic);RememberLocal(deliveries[at]);deliveries.RemoveAt(at);
            }
            var delivery=new Delivery{Id=id,SpeakerId=speakerId,Speaker=speaker,Text=text??"",Started=started,Expires=expires,IsPublic=isPublic,Sequence=sequence,CharacterCount=Characters(text)};
            deliveries.Add(delivery);return delivery;
        }
        void Reveal(Delivery delivery)
        {
            delivery.AgeSeconds=Math.Max(0,worldClock-delivery.Started);
            delivery.WorldActive=!delivery.Retracted&&delivery.Text!=""&&worldClock<delivery.Expires;
            // Canceling the accumulated minigame pause can leave an exact glyph boundary a few double ulps below its integer.
            delivery.VisibleCharacters=delivery.Retracted?0:(int)Math.Min(delivery.CharacterCount,delivery.AgeSeconds*LettersPerSecond+1e-6);
        }
        static int Characters(string text)
        {
            int count=0;if(text==null)return count;
            for(int i=0;i<text.Length;i++){count++;if(char.IsHighSurrogate(text[i])&&i+1<text.Length&&char.IsLowSurrogate(text[i+1]))i++;}
            return count;
        }
        void RememberLocal(Delivery delivery)
        {
            if(delivery.IsPublic)return;retiredLocal.Enqueue(delivery.Id);
            while(retiredLocal.Count>FestivalSimulation.PublicSpeechLimit)retiredLocal.Dequeue();
        }
        /// <summary>Release the active window but keep its round-local accepted cursor, so an unchanged view cannot replay on enable/reconnect.</summary>
        public void Retire(){foreach(var delivery in deliveries)RememberLocal(delivery);deliveries.Clear();}
    }

    /// <summary>Session-owned public speech. Ordinary world labels keep their existing renderer.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class FestivalSpeechBubbles : MonoBehaviour
    {
        public const float LettersPerSecond=FestivalSpeechDeliveryTracker.LettersPerSecond,Range=15;
        const float Width=2.6f,Padding=.16f;
        sealed class Bubble { public Transform Root; public TextMeshPro Text; public Transform Head,Body; public int LastVisible; public float Height; }
        readonly Dictionary<string,Bubble> bubbles=new Dictionary<string,Bubble>();
        readonly Stack<Bubble> pool=new Stack<Bubble>();
        readonly HashSet<string> shown=new HashSet<string>();
        readonly List<string> gone=new List<string>();
        readonly Dictionary<string,float> stack=new Dictionary<string,float>();
        FestivalSession session;
        FestivalSpeechDeliveryTracker tracker;
        string cacheRound="";
        int cacheGeneration=-1;
        public FestivalSpeechDeliveryTracker Deliveries
        {
            get
            {
                if(tracker==null){if(session==null)session=GetComponent<FestivalSession>();tracker=session!=null?session.SpeechDeliveries:new FestivalSpeechDeliveryTracker();}
                return tracker;
            }
        }
        /// <summary>Only new glyphs on a continuously visible bubble. Creating/reopening a bubble never plays a backlog.</summary>
        public event Action<FestivalSpeechDeliveryTracker.Delivery,int,int> GlyphsRevealed;
        TMP_FontAsset font;
        Material backing;

        void LateUpdate()
        {
            if(session==null)session=GetComponent<FestivalSession>();
            if(session==null||!session.enabled||session.State==null)return;
            Apply(session.State,id=>session.WorldCharacter(id)?.transform,session.ViewCamera.transform,session.EyePosition,session.LocalPlayerId,session.Profile.Data.ReducedMotion,session.ConnectionGeneration,session.MenuOpen,session.EstimatedWorldSeconds);
        }
        /// <summary>Draw the filtered session view after its actors, vision markers and camera have moved.</summary>
        public void Apply(RoundState state,Func<string,Transform> actorFor,Transform view,Vector3 eye,string localPlayerId,bool reducedMotion,int generation=0,bool hidden=false,double? worldSeconds=null)
        {
            if(state==null){Clear();return;}
            if(cacheRound!=state.RoundId||cacheGeneration!=generation){ClearAssets();cacheRound=state.RoundId;cacheGeneration=generation;}
            Deliveries.Follow(state,generation,worldSeconds??FestivalSimulation.WorldSeconds(state));
            shown.Clear();stack.Clear();
            if(!hidden&&view!=null&&enabled)foreach(var speech in Deliveries.Deliveries)
            {
                var body=actorFor?.Invoke(speech.SpeakerId);
                if(speech.SpeakerId!=localPlayerId&&body!=null&&body.gameObject.activeInHierarchy&&Vector3.Distance(body.position,eye)<=Range&&speech.WorldActive)shown.Add(speech.Id);
            }
            // Retire outgoing views before reusing slots, bounding objects by the delivery window even during rapid replies.
            gone.Clear();foreach(var id in bubbles.Keys)if(!shown.Contains(id))gone.Add(id);
            foreach(var id in gone){var bubble=bubbles[id];bubble.Root.gameObject.SetActive(false);pool.Push(bubble);bubbles.Remove(id);}
            foreach(var speech in Deliveries.Deliveries)
            {
                if(!shown.Contains(speech.Id))continue;
                var body=actorFor(speech.SpeakerId);
                bool fresh=!bubbles.TryGetValue(speech.Id,out var bubble);
                if(fresh)
                {
                    bubble=pool.Count>0?pool.Pop():Create();bubble.Root.gameObject.SetActive(true);
                    bubble.Text.text=speech.Text;bubble.Text.maxVisibleCharacters=int.MaxValue;
                    var preferred=bubble.Text.GetPreferredValues(speech.Text,Width,float.PositiveInfinity);
                    bubble.Height=Mathf.Max(.35f,preferred.y)+Padding*2;
                    bubble.Text.rectTransform.sizeDelta=new Vector2(Width,bubble.Height-Padding*2);
                    bubble.Root.Find("Bubble backing").localScale=new Vector3(Width+Padding*2,bubble.Height,1);
                    bubble.Text.ForceMeshUpdate();bubbles.Add(speech.Id,bubble);
                }
                if(bubble.Body!=body){bubble.Body=body;bubble.Head=Head(body);}
                bubble.Text.maxVisibleCharacters=speech.VisibleCharacters;
                if(!fresh&&speech.VisibleCharacters>bubble.LastVisible)GlyphsRevealed?.Invoke(speech,bubble.LastVisible,speech.VisibleCharacters);
                bubble.LastVisible=speech.VisibleCharacters;
                var head=bubble.Head!=null?bubble.Head.position:body.position+Vector3.up*1.8f;
                float bottom=head.y+.35f;
                foreach(var vision in state.Visions)
                {
                    if(vision.NpcId!=speech.SpeakerId)continue;
                    var marker=transform.Find("Vision "+vision.Id);
                    if(marker==null||!marker.gameObject.activeInHierarchy)continue;
                    foreach(var renderer in marker.GetComponentsInChildren<Renderer>(false))if(renderer.enabled)bottom=Mathf.Max(bottom,renderer.bounds.max.y+.15f);
                }
                stack.TryGetValue(speech.SpeakerId,out float offset);
                bubble.Root.position=new Vector3(head.x,bottom+bubble.Height*.5f+offset,head.z);
                bubble.Root.rotation=view.rotation;
                bubble.Root.localScale=Vector3.one*(reducedMotion?1:Mathf.Lerp(.94f,1,(float)speech.AgeSeconds/.16f));
                stack[speech.SpeakerId]=offset+bubble.Height+.1f;
            }
        }
        Bubble Create()
        {
            if(font==null)
            {
                font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("FestivalDisplay"));
                font.name="Festival speech font";font.isMultiAtlasTexturesEnabled=false;
                font.material.shader=TMP_Settings.defaultFontAsset.material.shader;
                font.material.SetFloat(ShaderUtilities.ShaderTag_ZTestMode,(float)CompareFunction.LessEqual);
                font.fallbackFontAssetTable=new List<TMP_FontAsset>{TMP_Settings.defaultFontAsset};
                backing=new Material(Resources.Load<Material>("FestivalLit")){name="Festival speech backing",color=new Color(.055f,.07f,.065f)};
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DevelopmentDiagnostics.GraphicsEvent("Speech","cache_ready","shared_font=true depth_test=LessEqual");
#endif
            }
            var root=new GameObject("Speech bubble").transform;root.SetParent(transform,false);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Bubble backing";quad.transform.SetParent(root,false);
            quad.transform.localPosition=Vector3.forward*.025f;
            var collider=quad.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            var renderer=quad.GetComponent<MeshRenderer>();renderer.sharedMaterial=backing;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var face=new GameObject("Speech text");face.transform.SetParent(root,false);
            var text=face.AddComponent<TextMeshPro>();text.font=font;text.fontSharedMaterial=font.material;text.fontSize=3;
            text.color=new Color(.96f,.94f,.84f);text.alignment=TextAlignmentOptions.Center;text.richText=false;text.parseCtrlCharacters=false;
            text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;
            text.rectTransform.sizeDelta=new Vector2(Width,.6f);
            text.renderer.shadowCastingMode=ShadowCastingMode.Off;text.renderer.receiveShadows=false;
            return new Bubble{Root=root,Text=text};
        }
        static Transform Head(Transform body)
        {
            foreach(var child in body.GetComponentsInChildren<Transform>(true))if(child.name=="Head")return child;
            return null;
        }
        public void Clear()
        {
            Deliveries.Retire();ClearAssets();
        }
        void ClearAssets()
        {
            foreach(var bubble in bubbles.Values)if(bubble.Root!=null){bubble.Root.gameObject.SetActive(false);Dispose(bubble.Root.gameObject);}bubbles.Clear();
            foreach(var bubble in pool)if(bubble.Root!=null)Dispose(bubble.Root.gameObject);pool.Clear();
            if(backing!=null)Dispose(backing);backing=null;
            if(font!=null)
            {
                foreach(var atlas in font.atlasTextures)if(atlas!=null)Dispose(atlas);
                Dispose(font.material);Dispose(font);font=null;
            }
        }
        void OnDisable()=>Clear();
        void OnDestroy()=>Clear();
        static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
