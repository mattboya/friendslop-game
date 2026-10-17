using System;
using System.Collections.Generic;
using Festival.Integrations;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>
    /// SET-1: the one settings screen that SETTINGS opens on the title and on the pass. FestivalHud builds a widget row for each of
    /// Rows; this keeps the open tab and what each row reads, and applies and saves every change at once. A change that cannot be
    /// saved is undone and its error stays on the screen.
    /// </summary>
    public sealed class FestivalSettingsScreen
    {
        public enum Tab{Audio,Graphics,Controls,Accessibility,Voice}
        public static readonly Tab[] Tabs={Tab.Audio,Tab.Graphics,Tab.Controls,Tab.Accessibility,Tab.Voice};
        public const string SavedMessage="Saved on this machine. Changes apply immediately.";
        /// <summary>One setting: − and + step it (or TOGGLE flips it), and Value reads the saved choice.</summary>
        public sealed class Row
        {
            public readonly Tab Tab;public readonly string Label;public readonly bool Toggle;
            internal readonly Func<FestivalSettings,string> Read;
            internal readonly Action<ProfileData,FestivalSettings,int> Step;
            internal readonly Func<FestivalSettings,bool> Usable;
            internal Row(Tab tab,string label,bool toggle,Func<FestivalSettings,string> read,Action<ProfileData,FestivalSettings,int> step,Func<FestivalSettings,bool> usable=null)
            {
                Tab=tab;Label=label;Toggle=toggle;Read=read;Step=step;Usable=usable;
            }
        }
        private static readonly string[] DisplayModes={"Current","Windowed","Borderless","Fullscreen"},Presets={"Low","Medium","High"};
        private static readonly int[] Vsyncs={-1,0,1},FrameCaps={30,60,120,144,-1};
        public static readonly IReadOnlyList<Row> Rows=new[]
        {
            Volume("MASTER VOLUME",d=>d.MasterVolume,(d,v)=>d.MasterVolume=v),
            Volume("MUSIC VOLUME",d=>d.MusicVolume,(d,v)=>d.MusicVolume=v),
            Volume("EFFECTS VOLUME",d=>d.EffectsVolume,(d,v)=>d.EffectsVolume=v),
            Volume("VOICE VOLUME",d=>d.VoiceVolume,(d,v)=>d.VoiceVolume=v),
            Switch(Tab.Audio,"MUTE WHEN UNFOCUSED",d=>d.MuteWhenUnfocused,(d,v)=>d.MuteWhenUnfocused=v),
            new Row(Tab.Graphics,"DISPLAY MODE",false,s=>s.Data.DisplayMode.ToUpperInvariant(),(d,s,step)=>d.DisplayMode=Next(DisplayModes,d.DisplayMode,step)),
            new Row(Tab.Graphics,"RESOLUTION",false,s=>s.Data.ResolutionWidth==0?"CURRENT":Size(s.Data.ResolutionWidth,s.Data.ResolutionHeight),NextResolution),
            new Row(Tab.Graphics,"QUALITY",false,s=>s.Data.QualityPreset.ToUpperInvariant(),(d,s,step)=>d.QualityPreset=Next(Presets,d.QualityPreset,step)),
            new Row(Tab.Graphics,"RENDER SCALE",false,s=>Percent(s.Data.RenderScale),(d,s,step)=>d.RenderScale=Steps(d.RenderScale,step,.05f,.5f,1.5f)),
            new Row(Tab.Graphics,"VSYNC",false,s=>s.Data.VSyncCount<0?"SYSTEM":s.Data.VSyncCount==0?"OFF":"ON",(d,s,step)=>d.VSyncCount=Next(Vsyncs,d.VSyncCount,step)),
            // The saved cap stays shown while vsync paces the frames; the status line says which one is in charge.
            new Row(Tab.Graphics,"FRAME CAP",false,s=>s.Data.FrameCap<0?"UNLIMITED":s.Data.FrameCap+" FPS",(d,s,step)=>d.FrameCap=Next(FrameCaps,d.FrameCap,step),s=>!s.FrameCapControlledByVsync),
            new Row(Tab.Graphics,"FIELD OF VIEW",false,s=>Mathf.RoundToInt(s.Data.FieldOfView)+"°",(d,s,step)=>d.FieldOfView=Steps(d.FieldOfView,step,5,70,100)),
            new Row(Tab.Graphics,"BRIGHTNESS",false,s=>s.Data.Brightness.ToString("+0.0;-0.0;0.0"),(d,s,step)=>d.Brightness=Steps(d.Brightness,step,.1f,-1,1)),
            new Row(Tab.Controls,"LOOK SPEED",false,s=>s.Data.MouseSensitivity.ToString("0.00"),(d,s,step)=>d.MouseSensitivity=Steps(d.MouseSensitivity,step,.02f,.02f,.5f)),
            new Row(Tab.Controls,"CAMERA VIEW",true,s=>s.Data.ThirdPerson?"THIRD PERSON":"FIRST PERSON",(d,s,step)=>d.ThirdPerson=!d.ThirdPerson),
            Switch(Tab.Accessibility,"REDUCED MOTION",d=>d.ReducedMotion,(d,v)=>d.ReducedMotion=v),
            Switch(Tab.Accessibility,"HIGH CONTRAST",d=>d.HighContrast,(d,v)=>d.HighContrast=v),
        };

        private readonly LocalProfile profile;
        private readonly FestivalSettings settings;
        private readonly IVoiceSession voice;
        public Tab Shown {get;private set;}
        public string Message {get;private set;}
        public bool Failed {get;private set;}

        public FestivalSettingsScreen(LocalProfile profile,FestivalSettings settings,IVoiceSession voice)
        {
            this.profile=profile;this.settings=settings;this.voice=voice;
            Failed=profile.LoadError!="";Message=Failed?profile.LoadError:SavedMessage;
        }
        public static string TabName(Tab tab)=>tab.ToString().ToUpperInvariant();
        public void Open(Tab tab)=>Shown=tab;
        public string Value(Row row)=>row.Read(settings);
        public bool Usable(Row row)=>row.Usable==null||row.Usable(settings);
        public string Change(Row row,int direction)=>Commit(()=>{row.Step(settings.Data,settings,direction);return settings.Save();},SavedMessage);
        public string ResetTab()=>Commit(()=>settings.ResetTab(Shown.ToString()),TabName(Shown)+" reset to its defaults. Saved on this machine.");
        public string ShowTipsAgain()=>Commit(profile.ShowTipsAgain,"First-time tips will show again.");
        /// <summary>What the platform is actually doing, which can differ from the saved choices above it (a display request the
        /// platform has not finished, exclusive fullscreen it does not support, or vsync pacing over the saved cap).</summary>
        public string Status
        {
            get
            {
                if(Shown==Tab.Voice)return voice?.Status??"";
                if(Shown!=Tab.Graphics)return "";
                var size=settings.EffectiveResolution;
                string pacing=settings.FrameCapControlledByVsync?"VSYNC ON  •  FRAME RATE SET BY VSYNC"
                    :"VSYNC OFF  •  "+(Application.targetFrameRate>0?"CAPPED AT "+Application.targetFrameRate+" FPS":"NO FRAME CAP");
                return "NOW "+Size(size.width,size.height)+" "+ModeName(settings.EffectiveDisplayMode)+"  •  "+pacing;
            }
        }
        // Atomic for the player: either the change is applied and saved, or the previous choices come back with the reason shown.
        private string Commit(Func<string> change,string done)
        {
            string before=JsonUtility.ToJson(settings.Data);
            string error=change();
            if(error!="")JsonUtility.FromJsonOverwrite(before,settings.Data);
            Failed=error!="";Message=Failed?error:done;
            return error;
        }
        private static string ModeName(FullScreenMode mode)=>mode==FullScreenMode.ExclusiveFullScreen?"FULLSCREEN":mode==FullScreenMode.FullScreenWindow?"BORDERLESS":mode==FullScreenMode.MaximizedWindow?"MAXIMIZED":"WINDOWED";
        private static string Size(int width,int height)=>width+" × "+height;
        private static string Percent(float value)=>Mathf.RoundToInt(value*100)+"%";
        private static Row Volume(string label,Func<ProfileData,float> read,Action<ProfileData,float> write)
            =>new Row(Tab.Audio,label,false,s=>Percent(read(s.Data)),(d,s,step)=>write(d,Steps(read(d),step,.05f,0,1)));
        private static Row Switch(Tab tab,string label,Func<ProfileData,bool> read,Action<ProfileData,bool> write)
            =>new Row(tab,label,true,s=>read(s.Data)?"ON":"OFF",(d,s,step)=>write(d,!read(d)));
        // Rounded to hundredths so repeated steps land on the values the rows read.
        private static float Steps(float value,int direction,float step,float min,float max)=>Mathf.Clamp(Mathf.Round((value+direction*step)*100)/100,min,max);
        private static T Next<T>(T[] values,T current,int direction)
        {
            int at=Array.IndexOf(values,current);
            return values[Mathf.Clamp(at<0?0:at+direction,0,values.Length-1)];
        }
        // CURRENT first, then each size the platform reports (FestivalSettings drops duplicates and adds the window's own size).
        private static void NextResolution(ProfileData data,FestivalSettings settings,int direction)
        {
            var sizes=new List<Resolution>{new Resolution()};sizes.AddRange(settings.AvailableResolutions);
            int at=sizes.FindIndex(r=>r.width==data.ResolutionWidth&&r.height==data.ResolutionHeight);
            var next=sizes[Mathf.Clamp(at<0?0:at+direction,0,sizes.Count-1)];
            data.ResolutionWidth=next.width;data.ResolutionHeight=next.height;
        }
    }
}
