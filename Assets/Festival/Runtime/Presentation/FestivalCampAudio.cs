using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>The camp's sound: the DJ's track, chosen in the host snapshot, and the original synthesized gag sounds (the camp
    /// secrets' horn and squeak play from their props, FestivalCampEggView). MUSIC-2: the
    /// tracks are Matt's recordings (FestivalMusic). Each loops between beats measured from the recording, not over the file's silent
    /// start and faded end. The stage's two-deck transport plays them on the audio clock (FestivalMusic.Transport.Play):
    /// the first pass from the track's start to the loop's end, then the decks take turns on the loop, joining to the sample.</summary>
    public sealed class FestivalCampAudio : MonoBehaviour
    {
        const int Rate=24000;
        readonly AudioClip[] antics=new AudioClip[3];
        readonly AudioSource[] decks=new AudioSource[2];
        AudioSource foley;
        FestivalSession session;
        FestivalMusic.Transport transport;
        int playing=-1;
        int lastAntics;
        float? beat;
        int beatFrame=-1;

        /// <summary>The deck sounding now (FestivalMusic.BeatPhase reads its beat), or null in silence.</summary>
        public AudioSource Music=>transport?.Current(AudioSettings.dspTime);
        /// <summary>GAS-5: the camp music's beat phase this frame (FestivalMusic.BeatPhase of Music), or null in silence. It is read
        /// once a frame, so the HUD's zoom and FestivalTrip's overlay and sound pulse on the same moment.</summary>
        public float? Beat{get{if(beatFrame!=Time.frameCount){beatFrame=Time.frameCount;beat=FestivalMusic.BeatPhase(Music);}return beat;}}

        void Awake()
        {
            session=GetComponent<FestivalSession>();
            for(int deck=0;deck<decks.Length;deck++){decks[deck]=gameObject.AddComponent<AudioSource>();decks[deck].playOnAwake=false;decks[deck].spatialBlend=0;}
            transport=new FestivalMusic.Transport(decks);
            foley=gameObject.AddComponent<AudioSource>();foley.playOnAwake=false;foley.spatialBlend=0;
            for(int kind=0;kind<antics.Length;kind++)antics[kind]=CreateAntic(kind);
        }
        static AudioClip CreateAntic(int kind)
        {
            var samples=new float[Rate];
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)Rate;
                double envelope=Math.Exp(-t*(kind==2?4:7));
                double sound=kind==0
                    ?Math.Sin(2*Math.PI*(220+16*Math.Sin(t*13))*t)*.23
                    :kind==1?Math.Sin(2*Math.PI*(490-250*t)*t)*.19
                    :(Math.Sin(2*Math.PI*(130+470*t)*t)+Math.Sin(2*Math.PI*890*t)*.2)*.12;
                samples[n]=(float)(sound*envelope);
            }
            var clip=AudioClip.Create("Original camp antic "+kind,samples.Length,1,Rate,false);
            clip.SetData(samples,0);return clip;
        }
        /// <summary>CAMP-3's DO NOT PRESS button: a 1.2 s air horn, two buzzy reeds a sour third apart, blaring at once and holding.</summary>
        public static AudioClip CreateHorn()
        {
            const double Seconds=1.2;
            var samples=new float[(int)(Rate*Seconds)];double low=0,high=0;
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)Rate,wobble=1+.006*Math.Sin(2*Math.PI*5.5*t);
                low+=415*wobble/Rate;high+=523*wobble/Rate;
                double reeds=Saw(low)+.8*Saw(high);
                samples[n]=(float)(Math.Tanh(1.6*reeds)*Math.Min(1,t/.012)*Fade(Seconds-t,.1));
            }
            return Clip("Camp air horn",samples,.85);
        }
        /// <summary>CAMP-3's flamingo: a long, sad, rubbery squeak sliding from about 700 Hz down to 180 Hz over escaping air.</summary>
        public static AudioClip CreateSqueak()
        {
            const double Seconds=2.2;
            var samples=new float[(int)(Rate*Seconds)];double phase=0,hiss=0;uint seed=0x3c6ef372;
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)Rate,u=t/Seconds;
                double hz=(180+520*Math.Exp(-u*3.2))*(1+(.02+.05*u)*Math.Sin(2*Math.PI*8*t));
                phase+=2*Math.PI*hz/Rate;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;
                hiss+=(((seed&0xffff)/32767.5-1)-hiss)*.08;
                double sound=(Math.Sin(phase)+.4*Math.Sin(2*phase)+.15*Math.Sin(3*phase))*(.9-.4*u)+hiss*.6;
                samples[n]=(float)(sound*Math.Min(1,t/.03)*Fade(Seconds-t,.25));
            }
            return Clip("Camp flamingo squeak",samples,.7);
        }
        // A rising ramp from -1 to 1 once a cycle.
        static double Saw(double cycles)=>2*(cycles-Math.Floor(cycles))-1;
        // Squared, so the last few milliseconds are near silence and the clip ends without a click.
        static double Fade(double left,double seconds)=>Math.Pow(Math.Min(1,left/seconds),2);
        static AudioClip Clip(string name,float[] samples,double loudness)
        {
            float peak=0;foreach(float sample in samples)peak=Math.Max(peak,Math.Abs(sample));
            for(int n=0;n<samples.Length;n++)samples[n]=(float)(samples[n]*loudness/peak);
            var clip=AudioClip.Create(name,samples.Length,1,Rate,false);clip.SetData(samples,0);return clip;
        }
        void Update()
        {
            var state=session.State;
            // MENU-1: before a game connects, the title plays its own track.
            int selected=!session.Connected?FestivalTitleScreen.MusicTrack
                :state!=null&&(state.Phase=="Shopping"||state.Phase=="Spinning"||state.Phase=="CampReview")?state.CampMusicTrack:0;
            if(selected!=playing)
            {
                // The DJ's choice (CampFeatures.Tracks' index; SILENCE stops), from the track's start.
                playing=selected;var track=FestivalMusic.For(selected);transport.Play(track,0,AudioSettings.dspTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","camp_music","track="+selected+" clip="+(track?.Clip??"silence")+" loop="+(track==null?"none":track.FirstBeat+".."+track.LoopEnd));
#endif
            }
            transport.Turn(AudioSettings.dspTime);
            float volume=FestivalSettings.MusicGain(session.Profile?.Data,.6f);
            foreach(var deck in decks)deck.volume=volume;
            FestivalSettings.ApplyEffects(foley,session.Profile?.Data);
            var player=session.LocalPlayer;
            if(player==null){lastAntics=0;return;}
            if(player.CampAntics>lastAntics&&player.CampVisitId!="")
            {
                var site=CampFeatures.Find(player.CampVisitId);
                int kind=site?.Kind=="Car"?0:site?.Kind=="Tent"?1:2;
                foley.PlayOneShot(antics[kind]);
            }
            lastAntics=player.CampAntics;
        }
        void OnDestroy(){foreach(var antic in antics)if(antic!=null)Destroy(antic);}
    }
}
