using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Integrations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Festival.Presentation
{
    // Narrow operating-system boundary; settings policy is always applied by FestivalSettings.
    public interface IFestivalDisplay
    {
        int Width {get;} int Height {get;} FullScreenMode Mode {get;}
        bool SupportsExclusiveFullscreen {get;} Resolution[] Resolutions {get;}
        void SetResolution(int width,int height,FullScreenMode mode);
    }
    // SET-1: profile validation and tab defaults are shared by load, save and the settings screen.
    public sealed class FestivalSettings:IDisposable
    {
        [Serializable] private sealed class BindingData { public List<BindingRecord> bindings=null; }
        [Serializable] private sealed class BindingRecord
        {
            public string action="",id="",path=null,interactions=null,processors=null;
        }
        private readonly LocalProfile profile;
        private FestivalInput input;
        private FestivalTapCalibration calibration;
        private readonly IFestivalDisplay display;
        private sealed class PlatformDisplay:IFestivalDisplay
        {
            public int Width=>Screen.width;public int Height=>Screen.height;public FullScreenMode Mode=>Screen.fullScreenMode;
            public bool SupportsExclusiveFullscreen=>SupportsExclusiveDisplay(Application.platform,SystemInfo.graphicsDeviceType);
            public Resolution[] Resolutions=>Screen.resolutions;
            public void SetResolution(int width,int height,FullScreenMode mode)=>Screen.SetResolution(width,height,mode);
        }
        public FullScreenMode EffectiveDisplayMode=>display.Mode;
        public Resolution EffectiveResolution=>new Resolution{width=display.Width,height=display.Height};
        public bool FrameCapControlledByVsync=>QualitySettings.vSyncCount>0;
        public static bool SupportsExclusiveDisplay(RuntimePlatform platform,GraphicsDeviceType graphics)=>platform==RuntimePlatform.WindowsPlayer&&graphics!=GraphicsDeviceType.Vulkan;
        public IReadOnlyList<Resolution> AvailableResolutions
        {
            get
            {
                var choices=new List<Resolution>();
                foreach(var resolution in display.Resolutions??Array.Empty<Resolution>())
                    if(resolution.width>0&&resolution.height>0&&!choices.Exists(r=>r.width==resolution.width&&r.height==resolution.height))choices.Add(resolution);
                int width=runtimePipeline!=null?initialWidth:display.Width,height=runtimePipeline!=null?initialHeight:display.Height;
                if(!choices.Exists(r=>r.width==width&&r.height==height))choices.Add(new Resolution{width=width,height=height});
                return choices;
            }
        }
        private UniversalRenderPipelineAsset runtimePipeline;
        private readonly Dictionary<int,RenderPipelineAsset> originalPipelines=new Dictionary<int,RenderPipelineAsset>();
        private readonly Dictionary<int,int> originalVsync=new Dictionary<int,int>();
        private int originalQuality,initialVsync,initialFrameCap;
        private string appliedPreset,appliedMode;
        private int initialWidth,initialHeight,appliedWidth,appliedHeight,appliedVsync,appliedCap;
        private FullScreenMode initialMode,requestedMode;
        private int requestedWidth,requestedHeight;
        private bool displayApplied;
        private float appliedScale;
        public ProfileData Data=>profile.Data;
        public string LastError {get;private set;}

        public FestivalSettings(LocalProfile profile,IFestivalDisplay display=null)
        {
            this.profile=profile??throw new ArgumentNullException(nameof(profile));
            LastError=profile.LastError;this.display=display??new PlatformDisplay();
        }
        public void AttachInput(FestivalInput controls){input=controls;input?.SynchronizeBindings();}
        // Positive milliseconds mean the taps arrive late: subtract only at the judgment boundary.
        public static double JudgmentSeconds(double chartSeconds,ProfileData data)
        {
            float offset=FiniteClamp(data?.TimingCalibrationMs??0,-200,200,0);
            return Math.Max(0,chartSeconds-offset/1000.0);
        }
        public FestivalTapCalibration StartTapCalibration(AudioSource source)
        {
            calibration?.Dispose();calibration=new FestivalTapCalibration(this,source);return calibration;
        }
        internal string SaveCalibration(float milliseconds)
        {
            if(profile.ReadOnly)return Save();
            Data.TimingCalibrationMs=FiniteClamp(milliseconds,-200,200,0);return Save();
        }
        // Only runtime clones are edited; every touched quality slot is restored on teardown.
        public void ApplyGraphics()
        {
            if(runtimePipeline!=null&&appliedPreset==Data.QualityPreset&&appliedScale==Data.RenderScale&&appliedVsync==Data.VSyncCount&&appliedCap==Data.FrameCap&&appliedMode==Data.DisplayMode&&appliedWidth==Data.ResolutionWidth&&appliedHeight==Data.ResolutionHeight)return;
            Validate(Data);
            if(runtimePipeline==null)
            {
                originalQuality=QualitySettings.GetQualityLevel();initialVsync=QualitySettings.vSyncCount;initialFrameCap=Application.targetFrameRate;
                initialWidth=display.Width;initialHeight=display.Height;initialMode=display.Mode;
                var source=(QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
                if(source==null)return;
                runtimePipeline=UnityEngine.Object.Instantiate(source);runtimePipeline.name="Festival settings runtime pipeline";
                runtimePipeline.hideFlags=HideFlags.HideAndDontSave;
            }
            bool changed=appliedPreset!=Data.QualityPreset;
            if(changed)
            {
                if(appliedPreset!=null)Data.RenderScale=PresetScale(Data.QualityPreset);
                int level=Array.IndexOf(QualitySettings.names,Data.QualityPreset);
                if(level>=0)
                {
                    if(!originalPipelines.ContainsKey(level))
                    {
                        originalPipelines.Add(level,QualitySettings.GetRenderPipelineAssetAt(level));
                        QualitySettings.SetQualityLevel(level,false);originalVsync.Add(level,QualitySettings.vSyncCount);
                    }
                    else QualitySettings.SetQualityLevel(level,false);
                    QualitySettings.renderPipeline=runtimePipeline;
                }
                bool low=Data.QualityPreset=="Low",medium=Data.QualityPreset=="Medium";
                // URP exposes its serialized shadow switch as a read-only property. Overwrite only the clone.
                JsonUtility.FromJsonOverwrite(low?"{\"m_MainLightShadowsSupported\":false}":"{\"m_MainLightShadowsSupported\":true}",runtimePipeline);
                runtimePipeline.shadowDistance=low?0:medium?30:45;runtimePipeline.shadowCascadeCount=low||medium?1:2;
                runtimePipeline.msaaSampleCount=low?1:medium?2:4;
                appliedPreset=Data.QualityPreset;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Settings] Applied graphics preset "+appliedPreset+" using a runtime pipeline.");
#endif
            }
            runtimePipeline.renderScale=Data.RenderScale;
            QualitySettings.vSyncCount=Data.VSyncCount<0?initialVsync:Data.VSyncCount;
            Application.targetFrameRate=QualitySettings.vSyncCount>0?-1:Data.FrameCap;
            var mode=Data.DisplayMode=="Current"?initialMode:Data.DisplayMode=="Windowed"?FullScreenMode.Windowed:Data.DisplayMode=="Fullscreen"&&display.SupportsExclusiveFullscreen?FullScreenMode.ExclusiveFullScreen:FullScreenMode.FullScreenWindow;
            int width=initialWidth,height=initialHeight;
            foreach(var resolution in AvailableResolutions)
                if(resolution.width==Data.ResolutionWidth&&resolution.height==Data.ResolutionHeight){width=resolution.width;height=resolution.height;break;}
            bool changedDisplay=displayApplied&&(requestedMode!=mode||requestedWidth!=width||requestedHeight!=height);
            if(!displayApplied||changedDisplay)
            {
                if(changedDisplay||display.Mode!=mode||display.Width!=width||display.Height!=height)display.SetResolution(width,height,mode);
                requestedMode=mode;requestedWidth=width;requestedHeight=height;displayApplied=true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Settings] Requested display "+width+"x"+height+" "+mode+"; vsync="+QualitySettings.vSyncCount+"; saved frame cap="+Data.FrameCap+".");
#endif
            }
            appliedScale=Data.RenderScale;appliedVsync=Data.VSyncCount;appliedCap=Data.FrameCap;appliedMode=Data.DisplayMode;appliedWidth=Data.ResolutionWidth;appliedHeight=Data.ResolutionHeight;
        }
        private static float PresetScale(string preset)=>preset=="Low"?.75f:preset=="Medium"?.9f:1;
        public void Dispose()
        {
            calibration?.Dispose();calibration=null;
            if(runtimePipeline==null)return;
            foreach(var entry in originalPipelines)
            {
                QualitySettings.SetQualityLevel(entry.Key,false);QualitySettings.renderPipeline=entry.Value;QualitySettings.vSyncCount=originalVsync[entry.Key];
            }
            QualitySettings.SetQualityLevel(originalQuality,false);QualitySettings.vSyncCount=initialVsync;Application.targetFrameRate=initialFrameCap;
            if(displayApplied&&(requestedMode!=initialMode||requestedWidth!=initialWidth||requestedHeight!=initialHeight))display.SetResolution(initialWidth,initialHeight,initialMode);
            if(Application.isPlaying)UnityEngine.Object.Destroy(runtimePipeline);else UnityEngine.Object.DestroyImmediate(runtimePipeline);
            runtimePipeline=null;originalPipelines.Clear();originalVsync.Clear();appliedPreset=null;displayApplied=false;
        }
        // SET-1: sources apply authored gain and their bus. FestivalTrip alone applies master/focus/trip gain.
        public static float MusicGain(ProfileData data,float authoredGain=1)=>data==null?0:Mathf.Clamp01(data.MusicVolume*authoredGain);
        public static float EffectsGain(ProfileData data,float authoredGain=1)=>Mathf.Clamp01((data==null?1:data.EffectsVolume)*authoredGain);
        // New effect presenters use this adapter too; a source can follow slider changes during a one-shot.
        public static void ApplyEffects(AudioSource source,ProfileData data,float authoredGain=1)
        {
            if(source!=null)source.volume=EffectsGain(data,authoredGain);
        }
        public static void ConfigureVoice(IVoiceSession voice,ProfileData data=null)
        {
            if(voice is IConfigurableVoiceSession configurable)
                configurable.Configure(new VoiceConfiguration(data?.MicrophoneDevice??"",data?.InputGain??1,data?.VoiceMode??"PushToTalk",data?.VoiceVolume??1));
        }
        public string Save()
        {
            if(runtimePipeline!=null)ApplyGraphics();
            input?.SynchronizeBindings();
            LastError=profile.Save();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(LastError=="")Debug.Log("[Settings] Saved local settings.");
#endif
            return LastError;
        }
        public string ResetTab(string tab)
        {
            if(profile.ReadOnly)return Save();
            var defaults=new ProfileData();
            switch(tab)
            {
                case "Audio":
                    Data.MasterVolume=defaults.MasterVolume;Data.MusicVolume=defaults.MusicVolume;Data.EffectsVolume=defaults.EffectsVolume;
                    Data.VoiceVolume=defaults.VoiceVolume;Data.MuteWhenUnfocused=defaults.MuteWhenUnfocused;
                    break;
                case "Graphics":
                    Data.DisplayMode=defaults.DisplayMode;Data.ResolutionWidth=defaults.ResolutionWidth;Data.ResolutionHeight=defaults.ResolutionHeight;
                    Data.VSyncCount=defaults.VSyncCount;Data.FrameCap=defaults.FrameCap;Data.QualityPreset=defaults.QualityPreset;
                    Data.RenderScale=defaults.RenderScale;Data.FieldOfView=defaults.FieldOfView;Data.Brightness=defaults.Brightness;
                    break;
                case "Controls":
                    Data.MouseSensitivity=defaults.MouseSensitivity;Data.InvertY=defaults.InvertY;Data.ThirdPerson=defaults.ThirdPerson;
                    Data.TimingCalibrationMs=defaults.TimingCalibrationMs;Data.BindingOverridesJson="{\"bindings\":[]}";
                    break;
                case "Accessibility":
                    Data.ReducedMotion=defaults.ReducedMotion;Data.HighContrast=defaults.HighContrast;Data.CameraShake=defaults.CameraShake;Data.TextScale=defaults.TextScale;
                    break;
                case "Voice":
                    Data.MicrophoneDevice=defaults.MicrophoneDevice;Data.InputGain=defaults.InputGain;Data.VoiceMode=defaults.VoiceMode;
                    break;
                default:LastError="Unknown settings tab.";return LastError;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Settings] Reset "+tab+" tab.");
#endif
            return Save();
        }

        public static void Validate(ProfileData data)
        {
            var defaults=new ProfileData();
            data.TimingCalibrationMs=FiniteClamp(data.TimingCalibrationMs,-200,200,defaults.TimingCalibrationMs);
            data.MouseSensitivity=FiniteClamp(data.MouseSensitivity,.02f,.5f,defaults.MouseSensitivity);
            data.MasterVolume=FiniteClamp(data.MasterVolume,0,1,defaults.MasterVolume);
            data.MusicVolume=FiniteClamp(data.MusicVolume,0,1,defaults.MusicVolume);
            data.EffectsVolume=FiniteClamp(data.EffectsVolume,0,1,defaults.EffectsVolume);
            data.VoiceVolume=FiniteClamp(data.VoiceVolume,0,1,defaults.VoiceVolume);
            data.RenderScale=FiniteClamp(data.RenderScale,.5f,1.5f,defaults.RenderScale);
            data.FieldOfView=FiniteClamp(data.FieldOfView,70,100,defaults.FieldOfView);
            data.Brightness=FiniteClamp(data.Brightness,-1,1,defaults.Brightness);
            data.TextScale=FiniteClamp(data.TextScale,1,1.3f,defaults.TextScale);
            data.InputGain=FiniteClamp(data.InputGain,0,1,defaults.InputGain);
            if(data.DisplayMode!="Current"&&data.DisplayMode!="Fullscreen"&&data.DisplayMode!="Borderless"&&data.DisplayMode!="Windowed")data.DisplayMode=defaults.DisplayMode;
            if(data.QualityPreset!="Low"&&data.QualityPreset!="Medium"&&data.QualityPreset!="High")data.QualityPreset=defaults.QualityPreset;
            if(data.VSyncCount!=-1&&data.VSyncCount!=0&&data.VSyncCount!=1)data.VSyncCount=defaults.VSyncCount;
            if(data.FrameCap!=-1&&data.FrameCap!=30&&data.FrameCap!=60&&data.FrameCap!=120&&data.FrameCap!=144)data.FrameCap=defaults.FrameCap;
            if(data.ResolutionWidth<320||data.ResolutionWidth>16384||data.ResolutionHeight<200||data.ResolutionHeight>16384)
            {
                data.ResolutionWidth=defaults.ResolutionWidth;data.ResolutionHeight=defaults.ResolutionHeight;
            }
            if(data.VoiceMode!="PushToTalk"&&data.VoiceMode!="OpenMic")data.VoiceMode=defaults.VoiceMode;
            if(!PlainText(data.MicrophoneDevice,256))data.MicrophoneDevice=defaults.MicrophoneDevice;
            if(!ValidBindings(data.BindingOverridesJson))data.BindingOverridesJson=defaults.BindingOverridesJson;
            data.UnlockedFestivalCount=Math.Max(1,Math.Min(Festivals.Count,data.UnlockedFestivalCount));
            if(string.IsNullOrWhiteSpace(data.PlayerName))data.PlayerName=ProfileData.DefaultName;
            if(string.IsNullOrWhiteSpace(data.LastHost)||data.LastHost.Length>128)data.LastHost=ProfileData.DefaultHost;
            // "first_shuttle" is the persisted award ID even though the HUD now calls it a rescue.
            data.Cosmetics=Ids(data.Cosmetics,1,id=>id=="first_shuttle");
            data.SeenDialogue=Ids(data.SeenDialogue,512,ValidId);
            data.SeenTips=Ids(data.SeenTips,512,ValidId);
            data.FoundCampEggIds=Ids(data.FoundCampEggIds,8,IsCampEggId);
        }
        public static bool IsCampEggId(string id)=>id=="gnome"||id=="button"||id=="chair"||id=="lost_found"||id=="hammock"||id=="flamingo"||id=="cone"||id=="wash_me";
        private static bool ValidId(string id)
        {
            if(string.IsNullOrEmpty(id)||id.Length>64)return false;
            foreach(char c in id)if(!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_'||c=='-'))return false;
            return true;
        }
        private static List<string> Ids(List<string> values,int limit,Func<string,bool> valid)
        {
            if(values==null)return new List<string>();
            var seen=new HashSet<string>(StringComparer.Ordinal);
            for(int i=values.Count-1;i>=0;i--)if(!valid(values[i])||!seen.Add(values[i]))values.RemoveAt(i);
            if(values.Count>limit)values.RemoveRange(0,values.Count-limit);
            return values;
        }
        private static float FiniteClamp(float value,float min,float max,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)?fallback:Math.Max(min,Math.Min(max,value));
        private static bool PlainText(string value,int max)
        {
            if(value==null||value.Length>max)return false;
            foreach(char c in value)if(char.IsControl(c))return false;
            return true;
        }
        private static bool ValidBindings(string json)
        {
            if(string.IsNullOrEmpty(json))return true;
            if(json.Length>32768)return false;
            try
            {
                var data=JsonUtility.FromJson<BindingData>(json);
                if(data?.bindings==null||data.bindings.Count>128)return false;
                var ids=new HashSet<Guid>();
                foreach(var binding in data.bindings)
                {
                    if(binding==null||!Guid.TryParse(binding.id,out var id)||id==Guid.Empty||!ids.Add(id))return false;
                    if(string.IsNullOrWhiteSpace(binding.action)||!PlainText(binding.action,128))return false;
                    if(binding.path!=null&&!PlainText(binding.path,256))return false;
                    if(binding.interactions!=null&&!PlainText(binding.interactions,1024))return false;
                    if(binding.processors!=null&&!PlainText(binding.processors,1024))return false;
                }
                return true;
            }
            catch(ArgumentException){return false;}
        }
    }
    /// <summary>A short, scheduled local audio exercise. Its samples never read a microphone or shift music/chart transport.</summary>
    public sealed class FestivalTapCalibration:IDisposable
    {
        public const int BeatCount=8;
        public const double BeatSeconds=.5;
        private readonly FestivalSettings settings;
        private readonly AudioSource source;
        private readonly AudioClip originalClip,clip;
        private readonly float originalPitch,originalVolume,originalSpatialBlend;
        private readonly bool originalLoop,originalPlayOnAwake;
        private readonly bool[] tapped=new bool[BeatCount];
        private readonly List<double> offsets=new List<double>();
        private bool disposed,completed;
        public double ScheduledStartDspSeconds {get;}
        public int SampleCount=>offsets.Count;
        public bool Completed=>completed;
        internal FestivalTapCalibration(FestivalSettings settings,AudioSource source)
        {
            this.settings=settings;this.source=source??throw new ArgumentNullException(nameof(source));
            if(source.isPlaying)throw new InvalidOperationException("Use an idle audio source for calibration.");
            originalClip=source.clip;originalPitch=source.pitch;originalVolume=source.volume;originalLoop=source.loop;
            originalSpatialBlend=source.spatialBlend;originalPlayOnAwake=source.playOnAwake;
            const int rate=24000;var samples=new float[rate*4];
            for(int beat=0;beat<BeatCount;beat++)for(int sample=0;sample<rate/10;sample++)
            {
                double time=sample/(double)rate;
                samples[beat*rate/2+sample]=(float)(Math.Sin(2*Math.PI*880*time)*Math.Exp(-time*70)*.5);
            }
            clip=AudioClip.Create("Festival calibration pulses",samples.Length,1,rate,false);clip.SetData(samples,0);
            source.clip=clip;source.pitch=1;source.loop=false;source.playOnAwake=false;source.spatialBlend=0;source.volume=FestivalSettings.EffectsGain(settings.Data);
            ScheduledStartDspSeconds=AudioSettings.dspTime+.25;source.PlayScheduled(ScheduledStartDspSeconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Settings] Scheduled local tap calibration; microphone unused.");
#endif
        }
        // The UI passes the audio-clock timestamp captured on the actual tap, not its chart or frame time.
        public bool Tap(double dspSeconds)
        {
            if(disposed||completed||double.IsNaN(dspSeconds)||double.IsInfinity(dspSeconds))return false;
            double elapsed=dspSeconds-ScheduledStartDspSeconds;
            if(elapsed<-.25||elapsed>(BeatCount-1)*BeatSeconds+.25)return false;
            int beat=(int)Math.Round(elapsed/BeatSeconds,MidpointRounding.AwayFromZero);
            if(beat<0||beat>=BeatCount||tapped[beat])return false;
            double offset=(elapsed-beat*BeatSeconds)*1000;
            if(Math.Abs(offset)>250)return false;
            tapped[beat]=true;offsets.Add(offset);return true;
        }
        public string Complete()
        {
            if(disposed)return "Calibration has closed.";
            if(completed)return "";
            if(offsets.Count<4)return "Tap at least four beats before saving calibration.";
            var sorted=new List<double>(offsets);sorted.Sort();int middle=sorted.Count/2;
            float offset=(float)(sorted.Count%2==0?(sorted[middle-1]+sorted[middle])/2:sorted[middle]);
            string error=settings.SaveCalibration(offset);
            if(error=="")
            {
                completed=true;source.Stop();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Settings] Tap calibration saved; samples="+offsets.Count+" judgment_offset_ms="+settings.Data.TimingCalibrationMs.ToString("F1")+".");
#endif
            }
            return error;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(source!=null&&source.clip==clip)
            {
                source.Stop();source.clip=originalClip;source.pitch=originalPitch;source.volume=originalVolume;source.loop=originalLoop;
                source.spatialBlend=originalSpatialBlend;source.playOnAwake=originalPlayOnAwake;
            }
            if(Application.isPlaying)UnityEngine.Object.Destroy(clip);else UnityEngine.Object.DestroyImmediate(clip);
        }
    }

}
