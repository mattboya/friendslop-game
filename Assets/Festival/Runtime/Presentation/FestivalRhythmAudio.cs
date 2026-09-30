using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Original synthesized beat scheduled on the audio clock, aligned to chart time.</summary>
    public sealed class FestivalRhythmAudio : MonoBehaviour
    {
        AudioSource beat;
        AudioClip clip;
        FestivalSession session;
        string phrase="";
        void Awake()
        {
            session=GetComponent<FestivalSession>();beat=gameObject.AddComponent<AudioSource>();
            const int rate=24000;var samples=new float[12000];
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)rate;
                samples[n]=(float)(Math.Sin(2*Math.PI*(65*t+8*(1-Math.Exp(-t*30))))*Math.Exp(-t*18)*.35
                    +Math.Sin(2*Math.PI*1600*t)*Math.Exp(-t*100)*.13);
            }
            clip=AudioClip.Create("Original festival pulse",samples.Length,1,rate,false);clip.SetData(samples,0);
            beat.clip=clip;beat.loop=true;beat.playOnAwake=false;beat.spatialBlend=0;
        }
        void Update()
        {
            var p=session.LocalPlayer;
            var i=p==null?null:session.State.Interactions.Find(x=>x.Id==p.InteractionId&&x.Status=="Active");
            bool rhythm=i!=null&&FestivalInput.IsRhythmKind(i.Kind);
            if(!rhythm){beat.Stop();phrase="";return;}
            beat.volume=session.Profile.Data.MusicVolume;
            string key=i.Id+":"+i.Phrase;if(phrase==key)return;phrase=key;
            beat.Stop();beat.pitch=(float)(.5/i.BeatSeconds);
            double now=session.EstimatedSimulationSeconds-i.StartSeconds;
            // First judged note occurs at chart time two seconds. Keep the pulse
            // aligned even for optional .4 second spacing and late snapshots.
            double first=2+Math.Ceiling((now-2)/i.BeatSeconds)*i.BeatSeconds;
            beat.time=0;beat.PlayScheduled(AudioSettings.dspTime+Math.Max(.02,first-now));
        }
        void OnDestroy(){if(clip!=null)Destroy(clip);}
    }
}
