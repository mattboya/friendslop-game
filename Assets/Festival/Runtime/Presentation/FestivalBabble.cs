using System;
using System.Collections.Generic;
using Festival.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>Generated Effects audio follows the shared visible glyph clock. No audio delivery clock or gameplay RNG.</summary>
    public sealed class FestivalBabble : IDisposable
    {
        public readonly struct Voice
        {
            public readonly float Frequency,Speed;
            internal Voice(float frequency,float speed){Frequency=frequency;Speed=speed;}
        }
        sealed class Speaker
        {
            public Voice Voice;
            public Transform Root,Body,Head;
            public AudioSource[] Sources;
            public readonly FestivalSpeechDeliveryTracker.Delivery[] Playing=new FestivalSpeechDeliveryTracker.Delivery[2];
            public readonly Dictionary<char,AudioClip> Clips=new Dictionary<char,AudioClip>();
            public int Next;
        }
        const int SampleRate=24000,SpeakerLimit=FestivalSimulation.PublicSpeechLimit*2;
        const float Gain=.3f;
        readonly Transform root;
        readonly Dictionary<string,Speaker> speakers=new Dictionary<string,Speaker>();
        readonly List<string> gone=new List<string>();
        Func<string,Transform> actorFor;
        RoundState state;
        IReadOnlyList<FestivalSpeechDeliveryTracker.Delivery> deliveries;
        Vector3 eye;
        string localPlayerId="";
        bool hidden;
        ProfileData profile;
        public FestivalBabble(Transform root){this.root=root;}

        // FNV over UTF-16 code units is identical across machines; the private xorshift never touches Unity's gameplay RNG.
        static uint Hash(string id)
        {
            uint hash=2166136261;foreach(char c in id??""){hash^=c;hash=unchecked(hash*16777619);}return hash;
        }
        static float Next(ref uint seed)
        {
            if(seed==0)seed=0x9e3779b9;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return (seed&0xffffff)/16777215f;
        }
        public static Voice VoiceFor(string id,string kind)
        {
            uint seed=Hash(id);bool security=kind=="Cop"||kind=="VipGuard";
            float low=security?95:kind=="Player"?170:150,high=security?140:kind=="Player"?340:300;
            return new Voice(Mathf.Lerp(low,high,Next(ref seed)),Mathf.Lerp(.85f,1.15f,Next(ref seed)));
        }
        /// <summary>Called by the session's bubble presenter before any reveal. The filtered actor roster is the privacy boundary.</summary>
        public void Follow(RoundState current,Func<string,Transform> actors,Vector3 listener,string owner,bool hide,ProfileData data,IReadOnlyList<FestivalSpeechDeliveryTracker.Delivery> window)
        {
            state=current;actorFor=actors;eye=listener;localPlayerId=owner;hidden=hide;profile=data;deliveries=window;
            gone.Clear();
            foreach(var entry in speakers)
            {
                bool active=false;foreach(var delivery in deliveries)if(delivery.SpeakerId==entry.Key&&delivery.WorldActive){active=true;break;}
                if(!active){gone.Add(entry.Key);continue;}
                var body=actorFor?.Invoke(entry.Key);
                bool audible=!hidden&&Visible(entry.Key)&&body!=null&&body.gameObject.activeInHierarchy&&Vector3.Distance(body.position,eye)<=FestivalSpeechBubbles.Range;
                if(audible)entry.Value.Root.position=Head(entry.Value,body);
                for(int i=0;i<entry.Value.Sources.Length;i++)
                {
                    var source=entry.Value.Sources[i];var playing=entry.Value.Playing[i];
                    bool live=audible&&(playing==null||playing.WorldActive&&!playing.Retracted);
                    FestivalSettings.ApplyEffects(source,profile,live?Gain:0);
                    if(!live)source.Stop();
                }
            }
            foreach(string id in gone){Dispose(speakers[id]);speakers.Remove(id);}
        }
        bool Visible(string id)=>state!=null&&(state.Npcs.Exists(n=>n.Id==id)||state.Players.Exists(p=>p.Id==id&&p.Connected));
        /// <summary>Only continuously visible [from,to) glyphs, supplied by the shared presenter or owner HUD adapter.
        /// Initial observation and reopening must use the current count silently, as the bubble presenter already does.</summary>
        public void PlayRevealed(FestivalSpeechDeliveryTracker.Delivery delivery,int from,int to)
        {
            if(delivery==null||hidden||!delivery.WorldActive||delivery.Retracted||!Visible(delivery.SpeakerId))return;
            bool owned=false;foreach(var accepted in deliveries)if(ReferenceEquals(accepted,delivery)){owned=true;break;}
            if(!owned)return;
            from=Math.Max(0,from);to=Math.Min(to,delivery.VisibleCharacters);if(from>=to)return;
            if(!delivery.IsPublic&&!state.Players.Exists(p=>p.Id==localPlayerId&&p.Connected&&p.Life!="Spirit"))return;
            var body=actorFor?.Invoke(delivery.SpeakerId);
            if(body==null||!body.gameObject.activeInHierarchy||Vector3.Distance(body.position,eye)>FestivalSpeechBubbles.Range)return;
            int glyph=0;bool question=delivery.Text.TrimEnd().EndsWith("?",StringComparison.Ordinal);
            for(int at=0;at<delivery.Text.Length&&glyph<to;at++,glyph++)
            {
                if(glyph>=Math.Max(0,from)&&char.IsLetter(delivery.Text,at))
                {
                    var speaker=GetSpeaker(delivery.SpeakerId);
                    char letter=char.ToLowerInvariant(delivery.Text[at]);char sound="aeiouy".IndexOf(letter)>=0?letter:' ';
                    if(!speaker.Clips.TryGetValue(sound,out var clip))
                    {
                        var samples=Samples(speaker.Voice,sound,Hash(delivery.SpeakerId));
                        clip=AudioClip.Create("Festival babble "+sound,samples.Length,1,SampleRate,false);clip.SetData(samples,0);speaker.Clips.Add(sound,clip);
                    }
                    speaker.Root.position=Head(speaker,body);var source=speaker.Sources[speaker.Next];speaker.Playing[speaker.Next]=delivery;speaker.Next=(speaker.Next+1)%speaker.Sources.Length;
                    source.clip=clip;
                    float speed=speaker.Voice.Speed;
                    if(question)speed=.85f+(speed-.85f)*.72f+.08f*glyph/Math.Max(1,delivery.CharacterCount-2);
                    source.pitch=Mathf.Clamp(speed,.85f,1.15f);FestivalSettings.ApplyEffects(source,profile,Gain);source.PlayOneShot(clip);
                }
                if(char.IsHighSurrogate(delivery.Text[at])&&at+1<delivery.Text.Length&&char.IsLowSurrogate(delivery.Text[at+1]))at++;
            }
        }
        Speaker GetSpeaker(string id)
        {
            if(speakers.TryGetValue(id,out var existing))return existing;
            if(speakers.Count>=SpeakerLimit)
            {
                string oldest=null;foreach(string key in speakers.Keys){oldest=key;break;}
                Dispose(speakers[oldest]);speakers.Remove(oldest);
            }
            string kind=state.Players.Exists(p=>p.Id==id)?"Player":"Wook";
            var npc=state.Npcs.Find(n=>n.Id==id);if(npc!=null)kind=npc.Twist=="VipGuard"?"VipGuard":npc.Kind;
            var go=new GameObject("Festival babble");go.transform.SetParent(root,false);
            var speaker=new Speaker{Voice=VoiceFor(id,kind),Root=go.transform,Sources=new AudioSource[2]};
            for(int i=0;i<speaker.Sources.Length;i++)
            {
                var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;
                source.minDistance=1;source.maxDistance=FestivalSpeechBubbles.Range;source.dopplerLevel=0;
                source.rolloffMode=AudioRolloffMode.Custom;source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,AnimationCurve.Linear(0,1,1,0));
                FestivalSettings.ApplyEffects(source,profile,Gain);speaker.Sources[i]=source;
            }
            speakers.Add(id,speaker);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("Speech","voice_cache_ready","spatial=true effects_bus=true");
#endif
            return speaker;
        }
        static float[] Samples(Voice voice,char sound,uint seed)
        {
            bool vowel=sound!=' ';var samples=new float[vowel?1320:288];
            double formant=sound=='a'?800:sound=='e'?500:sound=='i'?300:sound=='o'?600:sound=='u'?350:450;
            for(int i=0;i<samples.Length;i++)
            {
                double t=i/(double)SampleRate,envelope=Math.Sin(Math.PI*i/(samples.Length-1));
                double wave=vowel?Math.Sin(2*Math.PI*voice.Frequency*t)*.65+Math.Sin(2*Math.PI*formant*t)*.25:(Next(ref seed)*2-1)*.65+Math.Sin(2*Math.PI*voice.Frequency*4*t)*.35;
                samples[i]=(float)(wave*envelope*envelope*.32);
            }
            return samples;
        }
        static Vector3 Head(Speaker speaker,Transform body)
        {
            if(speaker.Body!=body)
            {
                speaker.Body=body;speaker.Head=null;
                foreach(var child in body.GetComponentsInChildren<Transform>(true))if(child.name=="Head"){speaker.Head=child;break;}
            }
            return speaker.Head!=null?speaker.Head.position:body.position+Vector3.up*1.8f;
        }
        static void Dispose(Speaker speaker)
        {
            foreach(var source in speaker.Sources)if(source!=null)source.Stop();
            foreach(var clip in speaker.Clips.Values)Destroy(clip);speaker.Clips.Clear();Destroy(speaker.Root.gameObject);
        }
        public void Dispose(){foreach(var speaker in speakers.Values)Dispose(speaker);speakers.Clear();state=null;deliveries=null;actorFor=null;hidden=true;}
        static void Destroy(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
