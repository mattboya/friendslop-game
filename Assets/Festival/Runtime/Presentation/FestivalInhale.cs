using System;
using System.Collections.Generic;
using Festival.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>GAS-6: each Giggle Balloon take the session reports (FestivalSession.GiggleBalloonTaken, once, for drawn players only)
    /// plays a made-in-code inhale at the hitter: in their own head (2-D), and for friends in 3-D from the hitter's body, silent beyond
    /// Range and with no Doppler. It goes through the effects volume, followed while it plays; FestivalTrip alone applies master, focus
    /// and the gas's own sound to the listener.</summary>
    public sealed class FestivalInhale : MonoBehaviour
    {
        /// <summary>Each player's inhale plays from a child of this name, then a space and their id.</summary>
        public const string VoiceName="Giggle inhale";
        /// <summary>How far a friend's inhale carries, in metres, as far as their speech (FestivalSpeechBubbles.Range).</summary>
        public const float Range=15;
        const int Rate=24000;
        // The breath swells for BreathSeconds, the balloon's neck squeaks from SqueakFrom to SqueakTo, and the rest is a held breath.
        // The squeak glides up through the breath's last third and ends just past it.
        const double Seconds=3,BreathSeconds=2.4,SqueakFrom=1.6,SqueakTo=2.45,Loudness=.6;
        // A friend's inhale comes from about mouth height on their body.
        const float MouthHeight=1.6f;

        readonly Dictionary<string,AudioSource> voices=new Dictionary<string,AudioSource>();
        FestivalSession session;
        AudioClip clip;

        /// <summary>How many inhales this presenter has started.</summary>
        public int Played {get;private set;}

        /// <summary>The inhale: breathy noise that swells and brightens, a rubbery squeak gliding up, then a held breath. The same
        /// samples every time, from a private generator.</summary>
        public static AudioClip CreateClip()
        {
            var samples=new float[(int)(Rate*Seconds)];uint seed=0x6a09e667;
            double low=0,rumble=0,phase=0,peak=0;
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)Rate,sound=0;
                if(t<BreathSeconds)
                {
                    double u=t/BreathSeconds,noise=Next(ref seed)*2-1;
                    low+=(noise-low)*Pole(600+2400*u);rumble+=(noise-rumble)*Pole(200);
                    sound=(low-rumble)*u*u*Math.Min(1,(BreathSeconds-t)/.03);
                }
                if(t>=SqueakFrom&&t<SqueakTo)
                {
                    double u=(t-SqueakFrom)/(SqueakTo-SqueakFrom),hz=650+600*u+40*Math.Sin(2*Math.PI*30*t),swell=Math.Sin(Math.PI*u);
                    phase+=2*Math.PI*hz/Rate;sound+=(Math.Sin(phase)+.3*Math.Sin(2*phase))*swell*swell*.35;
                }
                samples[n]=(float)sound;peak=Math.Max(peak,Math.Abs(sound));
            }
            for(int n=0;n<samples.Length;n++)samples[n]=(float)(samples[n]*Loudness/peak);
            var made=AudioClip.Create(VoiceName,samples.Length,1,Rate,false);made.SetData(samples,0);return made;
        }
        static double Pole(double hz)=>1-Math.Exp(-2*Math.PI*hz/Rate);
        static double Next(ref uint seed){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return (seed&0xffffff)/16777215.0;}

        void Awake(){session=GetComponent<FestivalSession>();clip=CreateClip();}
        void OnEnable(){if(session!=null)session.GiggleBalloonTaken+=Heard;}
        void OnDisable(){if(session!=null)session.GiggleBalloonTaken-=Heard;foreach(var voice in voices.Values)if(voice!=null)voice.Stop();}

        // One inhale for each report, however many takes it counts: two at once would only sound like one louder inhale. A take while
        // the hitter's inhale still plays starts it over, so two never layer.
        void Heard(string playerId,int takes)
        {
            bool own=playerId==session.LocalPlayerId;var body=own?null:session.WorldCharacter(playerId);
            bool audible=own||body!=null&&body.gameObject.activeInHierarchy&&Vector3.Distance(body.transform.position,session.EyePosition)<=Range;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("GiggleGas","inhale",(own?"voice=own":audible?"voice=friend":"voice=out_of_earshot")+" takes="+takes);
#endif
            if(!audible)return;
            var voice=Voice(playerId);voice.spatialBlend=own?0:1;
            if(!own)voice.transform.position=body.transform.position+Vector3.up*MouthHeight;
            FestivalSettings.ApplyEffects(voice,session.Profile?.Data);voice.Play();Played++;
        }
        AudioSource Voice(string playerId)
        {
            if(voices.TryGetValue(playerId,out var voice)&&voice!=null)return voice;
            var go=new GameObject(VoiceName+" "+playerId);go.transform.SetParent(transform,false);
            voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.clip=clip;voice.dopplerLevel=0;
            voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=1;voice.maxDistance=Range;
            return voices[playerId]=voice;
        }

        // After FestivalSession.Update has moved the actors: a friend's inhale follows their body, and every inhale its slider.
        void LateUpdate()
        {
            if(session==null||session.State==null){Clear();return;}
            foreach(var entry in voices)
            {
                var voice=entry.Value;FestivalSettings.ApplyEffects(voice,session.Profile?.Data);
                if(voice.spatialBlend==0||!voice.isPlaying)continue;
                var body=session.WorldCharacter(entry.Key);
                if(body==null||!body.gameObject.activeInHierarchy)voice.Stop();else voice.transform.position=body.transform.position+Vector3.up*MouthHeight;
            }
        }
        // Leaving the game stops every inhale and lets its voices go.
        void Clear(){foreach(var voice in voices.Values)if(voice!=null)Dispose(voice.gameObject);voices.Clear();}
        void OnDestroy(){Clear();if(clip!=null)Dispose(clip);}
        static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
