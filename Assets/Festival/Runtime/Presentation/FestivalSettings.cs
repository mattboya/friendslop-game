using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Integrations;
using UnityEngine;

namespace Festival.Presentation
{
    // SET-1: profile validation and tab defaults are shared by load, save and the settings screen.
    public sealed class FestivalSettings
    {
        [Serializable] private sealed class BindingData { public List<BindingRecord> bindings=null; }
        [Serializable] private sealed class BindingRecord
        {
            public string action="",id="",path=null,interactions=null,processors=null;
        }
        private readonly LocalProfile profile;
        public ProfileData Data=>profile.Data;
        public string LastError {get;private set;}

        public FestivalSettings(LocalProfile profile)
        {
            this.profile=profile??throw new ArgumentNullException(nameof(profile));
            LastError=profile.LastError;
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
                    Data.TimingCalibrationMs=defaults.TimingCalibrationMs;Data.BindingOverridesJson=defaults.BindingOverridesJson;
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
}
