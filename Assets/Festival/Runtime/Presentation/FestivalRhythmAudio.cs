using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>A light Effects tick follows the recorded grid. The DJ's separate set and legacy fixed charts keep their pulse.</summary>
    public sealed class FestivalRhythmAudio : MonoBehaviour
    {
        const float FeedbackGain=.35f/.6f,TickGain=.22f;
        const double ScheduleLead=.1,DriftCheckSeconds=.5,MaterialDriftSeconds=.06;
        AudioSource beat,other;
        AudioClip clip,pulse;
        FestivalSession session;
        string interactionId="";
        int phrase,seed,stageTrack;
        bool nextOnOther;
        double queuedTick,queuedDsp,nextCheck;
        void Awake()
        {
            session=GetComponent<FestivalSession>();beat=gameObject.AddComponent<AudioSource>();other=gameObject.AddComponent<AudioSource>();
            const int rate=24000;var samples=new float[768];
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)rate,envelope=Math.Sin(Math.PI*n/(samples.Length-1));
                samples[n]=(float)(Math.Sin(2*Math.PI*1800*t)*envelope*envelope*Math.Exp(-t*110)*.13);
            }
            clip=AudioClip.Create("Authored festival tick",samples.Length,1,rate,false);clip.SetData(samples,0);
            samples=new float[12000];
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)rate;
                samples[n]=(float)(Math.Sin(2*Math.PI*(65*t+8*(1-Math.Exp(-t*30))))*Math.Exp(-t*18)*.35
                    +Math.Sin(2*Math.PI*1600*t)*Math.Exp(-t*100)*.13);
            }
            pulse=AudioClip.Create("Original festival pulse",samples.Length,1,rate,false);pulse.SetData(samples,0);
            foreach(var source in new[]{beat,other}){source.playOnAwake=false;source.spatialBlend=0;}
        }
        /// <summary>The next measured whole beat after the chart's first head. Uses raw chart time: calibration belongs solely
        /// to the judge. The exact click-free loop span is kept, rather than rounding its length to a whole beat.</summary>
        public static double NextTick(InteractionState interaction,double afterChartSeconds)
        {
            if(interaction.StageTrack==0||interaction.Kind=="Dj")return 2+Math.Ceiling((afterChartSeconds-2)/interaction.BeatSeconds)*interaction.BeatSeconds;
            var track=FestivalMusic.For(interaction.StageTrack);
            double origin=interaction.StageFirstHeadWorldSeconds-interaction.StageFirstHeadDelaySeconds;
            return FestivalMusic.NextStageBeat(track,origin+Math.Max(afterChartSeconds,interaction.StageFirstHeadDelaySeconds))-origin;
        }
        void Update()
        {
            var player=session.LocalPlayer;var state=session.State;
            var interaction=player==null?null:state.Interactions.Find(x=>x.Id==player.InteractionId&&x.Status=="Active");
            Follow(interaction,interaction==null?0:session.EstimatedSimulationSeconds-interaction.StartSeconds,state?.Phase!="Playing",session.Profile?.Data,AudioSettings.dspTime);
        }
        /// <summary>Also accepts a booth-owned advancing raw chart clock while the stage is held.</summary>
        public void Follow(InteractionState interaction,double chartSeconds,bool held,ProfileData profile,double dspSeconds)
        {
            if(!FestivalInput.IsRhythm(interaction)||held){Stop();return;}
            bool authored=interaction.StageTrack!=0&&interaction.Kind!="Dj";
            if(authored&&!RhythmChart.ValidStageAnchor(interaction)){Stop();return;}
            float gain=authored?TickGain:FeedbackGain;
            FestivalSettings.ApplyEffects(beat,profile,gain);FestivalSettings.ApplyEffects(other,profile,gain);
            bool changed=interactionId!=interaction.Id||phrase!=interaction.Phrase||seed!=interaction.ChartSeed||stageTrack!=interaction.StageTrack;
            if(changed)
            {
                beat.Stop();other.Stop();interactionId=interaction.Id;phrase=interaction.Phrase;seed=interaction.ChartSeed;stageTrack=interaction.StageTrack;
                beat.clip=authored?clip:pulse;beat.loop=!authored;beat.pitch=authored?1:(float)(.5/interaction.BeatSeconds);
                other.clip=clip;other.loop=false;other.pitch=1;nextOnOther=false;
                if(!authored)
                {
                    double first=NextTick(interaction,chartSeconds);beat.time=0;beat.PlayScheduled(dspSeconds+Math.Max(.02,first-chartSeconds));return;
                }
                Cue(interaction,NextTick(interaction,chartSeconds+ScheduleLead),chartSeconds,dspSeconds);nextCheck=dspSeconds+DriftCheckSeconds;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(DevelopmentDiagnostics.Visible)DevelopmentDiagnostics.GraphicsEvent("StageTransport","tick_anchor","clip="+FestivalMusic.For(stageTrack).Clip+" anchor="+interaction.StageFirstHeadWorldSeconds.ToString("F3")+" stride="+interaction.StageBeatStride.ToString("F1"));
#endif
                return;
            }
            if(!authored)return;
            if(dspSeconds>=nextCheck)
            {
                nextCheck=dspSeconds+DriftCheckSeconds;
                if(Math.Abs((queuedDsp-dspSeconds)-(queuedTick-chartSeconds))>MaterialDriftSeconds)
                {
                    beat.Stop();other.Stop();Cue(interaction,NextTick(interaction,chartSeconds+ScheduleLead),chartSeconds,dspSeconds);return;
                }
            }
            if(dspSeconds+ScheduleLead<queuedDsp)return;
            // Never replay a backlog after a slow frame. Two sources let the previous brief tick finish naturally.
            double next=NextTick(interaction,Math.Max(queuedTick+.001,chartSeconds+ScheduleLead));
            Cue(interaction,next,chartSeconds,dspSeconds);
        }
        void Cue(InteractionState interaction,double at,double now,double dsp)
        {
            var source=nextOnOther?other:beat;source.Stop();source.timeSamples=0;
            queuedTick=at;queuedDsp=dsp+Math.Max(.02,at-now);source.PlayScheduled(queuedDsp);nextOnOther=!nextOnOther;
        }
        void Stop(){beat.Stop();other.Stop();interactionId="";}
        void OnDisable(){if(beat!=null)Stop();}
        void OnDestroy(){if(clip!=null)Destroy(clip);if(pulse!=null)Destroy(pulse);}
    }
}
