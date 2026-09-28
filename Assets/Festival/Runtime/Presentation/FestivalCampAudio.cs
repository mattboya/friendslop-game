using System;
using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Original synthesized camp loops; selection comes from the host snapshot.</summary>
    public sealed class FestivalCampAudio : MonoBehaviour
    {
        const int Rate=24000;
        readonly AudioClip[] tracks=new AudioClip[3];
        readonly AudioClip[] antics=new AudioClip[3];
        AudioSource source,foley;
        FestivalSession session;
        int playing=-1;
        int lastAntics;

        void Awake()
        {
            session=GetComponent<FestivalSession>();
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.spatialBlend=0;
            foley=gameObject.AddComponent<AudioSource>();foley.playOnAwake=false;foley.spatialBlend=0;
            for(int track=0;track<tracks.Length;track++)tracks[track]=CreateTrack(track);
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
        static AudioClip CreateTrack(int track)
        {
            // Four bars in 4/4; every component is generated from a fixed pattern.
            const int seconds=8;
            var samples=new float[Rate*seconds];
            int[] notes=track==0?new[]{0,4,7,9,7,4,2,4}:track==1?new[]{0,3,7,10,7,3,5,7}:new[]{0,5,7,5,2,5,9,7};
            double root=track==0?196:track==1?174.61:220;
            for(int n=0;n<samples.Length;n++)
            {
                double t=n/(double)Rate,beat=t*2,beatPhase=beat-Math.Floor(beat);
                int step=(int)(t*2)%notes.Length;
                double note=root*Math.Pow(2,notes[step]/12.0);
                double melody=Math.Sin(2*Math.PI*note*t)*Math.Exp(-beatPhase*3.5)*.15;
                double kick=Math.Sin(2*Math.PI*(58+35*Math.Exp(-beatPhase*28))*beatPhase/2)*Math.Exp(-beatPhase*19)*.25;
                double clap=(track==2?Math.Sin(2*Math.PI*890*t):Math.Sin(2*Math.PI*1100*t))*Math.Exp(-beatPhase*30)*((int)beat%2==1?.065:.018);
                double bass=Math.Sin(2*Math.PI*(note/4)*t)*.09;
                samples[n]=(float)(melody+kick+clap+bass);
            }
            var clip=AudioClip.Create("Original "+CampFeatures.Tracks[track+1],samples.Length,1,Rate,false);
            clip.SetData(samples,0);return clip;
        }
        void Update()
        {
            var state=session.State;
            int selected=state!=null&&(state.Phase=="Shopping"||state.Phase=="CampReview")?state.CampMusicTrack:0;
            if(selected!=playing)
            {
                playing=selected;source.Stop();
                if(selected>0&&selected<=tracks.Length){source.clip=tracks[selected-1];source.Play();}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","camp_music","track="+selected);
#endif
            }
            source.volume=session.Profile==null?0:session.Profile.Data.MusicVolume*.6f;
            var player=session.LocalPlayer;
            if(player==null){lastAntics=0;return;}
            if(player.CampAntics>lastAntics&&player.CampVisitId!="")
            {
                var site=CampFeatures.Find(player.CampVisitId);
                int kind=site?.Kind=="Car"?0:site?.Kind=="Tent"?1:2;
                foley.PlayOneShot(antics[kind],Mathf.Clamp01(session.Profile.Data.MusicVolume+.25f));
            }
            lastAntics=player.CampAntics;
        }
        void OnDestroy(){foreach(var track in tracks)if(track!=null)Destroy(track);foreach(var antic in antics)if(antic!=null)Destroy(antic);}
    }
}
