using System;
using System.Collections.Generic;
using Festival.Integrations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Festival.Presentation
{
    /// <summary>
    /// SET-1: the one settings screen that SETTINGS opens on the title and on the pass. FestivalHud builds a widget row for each of
    /// Rows; this keeps the open tab and what each row reads, and applies and saves every change at once. A change that cannot be
    /// saved is undone and its error stays on the screen. The key rebind, the tap test and the microphone test run only while
    /// their row shows: another tab or section, the screen closing, focus loss or teardown ends them.
    /// </summary>
    public sealed class FestivalSettingsScreen
    {
        public enum Tab{Audio,Graphics,Controls,Accessibility,Voice}
        public static readonly Tab[] Tabs={Tab.Audio,Tab.Graphics,Tab.Controls,Tab.Accessibility,Tab.Voice};
        // Every key does not fit on one page, so CONTROLS shows one of these sections at a time.
        public static readonly string[] Sections={"GENERAL","MOVE","ACTIONS","HANDS","DANCE","LIGHT SHOW"};
        public const string SavedMessage="Saved on this machine. Changes apply immediately.";
        public const string VoiceExplanation="Proximity voice isn't in this build yet; your mic settings are saved for when it is.";
        /// <summary>One setting: its buttons step it, flip it or act on it, and Value reads the saved choice.</summary>
        public sealed class Row
        {
            public readonly Tab Tab;public readonly string Section,Label;
            /// <summary>Left to right: − and + step the row; a single TOGGLE, REBIND or TEST acts on it; a note has none.</summary>
            public readonly string[] Buttons;
            internal readonly Func<FestivalSettingsScreen,string> Read;
            internal readonly Func<FestivalSettingsScreen,int,string> Press;
            internal readonly Func<FestivalSettingsScreen,bool> Usable,Running;
            internal readonly string Action;
            internal Row(Tab tab,string section,string label,string[] buttons,Func<FestivalSettingsScreen,string> read,Func<FestivalSettingsScreen,int,string> press,
                Func<FestivalSettingsScreen,bool> usable=null,Func<FestivalSettingsScreen,bool> running=null,string action=null)
            {
                Tab=tab;Section=section;Label=label;Buttons=buttons;Read=read;Press=press;Usable=usable;Running=running;Action=action;
            }
        }
        private static readonly string[] StepButtons={"−","+"},ToggleButton={"TOGGLE"},NoButtons=new string[0];
        private static readonly string[] DisplayModes={"Current","Windowed","Borderless","Fullscreen"},Presets={"Low","Medium","High"};
        private static readonly int[] Vsyncs={-1,0,1},FrameCaps={30,60,120,144,-1};
        // .25 s before the first pulse, eight pulses half a second apart, and the last tap's window.
        private const float TapTestSeconds=.25f+FestivalTapCalibration.BeatCount*(float)FestivalTapCalibration.BeatSeconds+.5f;
        /// <summary>TEST MICROPHONE, whose row also shows the level meter.</summary>
        public static readonly Row MicrophoneTest=new Row(Tab.Voice,"","TEST MICROPHONE",new[]{"TEST"},s=>s.MicrophoneMeter,(s,step)=>s.TestMicrophone(),
            s=>s.monitor.Devices.Length>0,s=>s.monitor.State!=FestivalMicrophoneMonitor.Phase.Off);
        public static readonly IReadOnlyList<Row> Rows=new[]
        {
            Volume("MASTER VOLUME",d=>d.MasterVolume,(d,v)=>d.MasterVolume=v),
            Volume("MUSIC VOLUME",d=>d.MusicVolume,(d,v)=>d.MusicVolume=v),
            Volume("EFFECTS VOLUME",d=>d.EffectsVolume,(d,v)=>d.EffectsVolume=v),
            Volume("VOICE VOLUME",d=>d.VoiceVolume,(d,v)=>d.VoiceVolume=v),
            Switch(Tab.Audio,"MUTE WHEN UNFOCUSED",d=>d.MuteWhenUnfocused,(d,v)=>d.MuteWhenUnfocused=v),
            Setting(Tab.Graphics,"DISPLAY MODE",s=>s.Data.DisplayMode.ToUpperInvariant(),(d,s,step)=>d.DisplayMode=Next(DisplayModes,d.DisplayMode,step)),
            Setting(Tab.Graphics,"RESOLUTION",s=>s.Data.ResolutionWidth==0?"CURRENT":Size(s.Data.ResolutionWidth,s.Data.ResolutionHeight),NextResolution),
            Setting(Tab.Graphics,"QUALITY",s=>s.Data.QualityPreset.ToUpperInvariant(),(d,s,step)=>d.QualityPreset=Next(Presets,d.QualityPreset,step)),
            Setting(Tab.Graphics,"RENDER SCALE",s=>Percent(s.Data.RenderScale),(d,s,step)=>d.RenderScale=Steps(d.RenderScale,step,.05f,.5f,1.5f)),
            Setting(Tab.Graphics,"VSYNC",s=>s.Data.VSyncCount<0?"SYSTEM":s.Data.VSyncCount==0?"OFF":"ON",(d,s,step)=>d.VSyncCount=Next(Vsyncs,d.VSyncCount,step)),
            // The saved cap stays shown while vsync paces the frames; the status line says which one is in charge.
            Setting(Tab.Graphics,"FRAME CAP",s=>s.Data.FrameCap<0?"UNLIMITED":s.Data.FrameCap+" FPS",(d,s,step)=>d.FrameCap=Next(FrameCaps,d.FrameCap,step),s=>!s.settings.FrameCapControlledByVsync),
            Setting(Tab.Graphics,"FIELD OF VIEW",s=>Mathf.RoundToInt(s.Data.FieldOfView)+"°",(d,s,step)=>d.FieldOfView=Steps(d.FieldOfView,step,5,70,100)),
            Setting(Tab.Graphics,"BRIGHTNESS",s=>s.Data.Brightness.ToString("+0.0;-0.0;0.0"),(d,s,step)=>d.Brightness=Steps(d.Brightness,step,.1f,-1,1)),
            Setting(Tab.Controls,"LOOK SPEED",s=>s.Data.MouseSensitivity.ToString("0.00"),(d,s,step)=>d.MouseSensitivity=Steps(d.MouseSensitivity,step,.02f,.02f,.5f),section:"GENERAL"),
            Switch(Tab.Controls,"INVERT Y",d=>d.InvertY,(d,v)=>d.InvertY=v,"GENERAL"),
            Setting(Tab.Controls,"CAMERA VIEW",s=>s.Data.ThirdPerson?"THIRD PERSON":"FIRST PERSON",(d,s,step)=>d.ThirdPerson=!d.ThirdPerson,section:"GENERAL",buttons:ToggleButton),
            new Row(Tab.Controls,"GENERAL","RHYTHM TIMING",new[]{"TAP TEST"},s=>s.TimingText,(s,step)=>s.TapTest(),s=>s.input!=null&&s.tapAudio!=null,s=>s.tapTest!=null),
            Key("MOVE","MOVE FORWARD","Move",1),Key("MOVE","MOVE BACK","Move",2),Key("MOVE","MOVE LEFT","Move",3),Key("MOVE","MOVE RIGHT","Move",4),
            Key("MOVE","SPRINT","Sprint"),Key("MOVE","LOOK","Look"),Key("MOVE","SWITCH VIEW","View"),
            // Esc both opens the pass and cancels a rebind, so the pass's own key is not offered: it could never be set back to Esc.
            Key("ACTIONS","INTERACT","Interact"),Key("ACTIONS","CHAT","Chat"),Key("ACTIONS","USE","Use"),Key("ACTIONS","DROP","Drop"),
            Key("ACTIONS","MAP","Objectives"),Key("ACTIONS","PUSH TO TALK","PushToTalk"),
            Key("HANDS","SLOT 1","Slot1"),Key("HANDS","SLOT 2","Slot2"),Key("HANDS","SLOT 3","Slot3"),Key("HANDS","NEXT SLOT","Cycle"),Key("HANDS","SPIN IN FRONT","Spin"),Key("HANDS","SPIN AT SIDES","SpinSides"),
            Key("DANCE","DANCE LEFT","Rhythm0"),Key("DANCE","DANCE DOWN","Rhythm1"),Key("DANCE","DANCE UP","Rhythm2"),Key("DANCE","DANCE RIGHT","Rhythm3"),
            Key("DANCE","DANCE LEFT ALT","Rhythm0",1),Key("DANCE","DANCE DOWN ALT","Rhythm1",1),Key("DANCE","DANCE UP ALT","Rhythm2",1),Key("DANCE","DANCE RIGHT ALT","Rhythm3",1),
            Key("LIGHT SHOW","ANSWER 1","Answer1"),Key("LIGHT SHOW","ANSWER 2","Answer2"),Key("LIGHT SHOW","ANSWER 3","Answer3"),Key("LIGHT SHOW","ANSWER 4","Answer4"),Key("LIGHT SHOW","NOT SURE","AnswerUnsure"),
            Switch(Tab.Accessibility,"REDUCED MOTION",d=>d.ReducedMotion,(d,v)=>d.ReducedMotion=v),
            Switch(Tab.Accessibility,"HIGH CONTRAST",d=>d.HighContrast,(d,v)=>d.HighContrast=v),
            Switch(Tab.Accessibility,"CAMERA SHAKE",d=>d.CameraShake,(d,v)=>d.CameraShake=v),
            Setting(Tab.Accessibility,"TEXT SIZE",s=>Percent(s.Data.TextScale),(d,s,step)=>d.TextScale=Steps(d.TextScale,step,.1f,1,1.3f)),
            new Row(Tab.Voice,"","VOICE CHAT",NoButtons,s=>VoiceExplanation,(s,step)=>""),
            new Row(Tab.Voice,"","MICROPHONE",StepButtons,s=>s.MicrophoneName,(s,step)=>s.ChangeMicrophone(step),s=>s.monitor.Devices.Length>0),
            Setting(Tab.Voice,"MIC VOLUME",s=>Percent(s.Data.InputGain),(d,s,step)=>d.InputGain=Steps(d.InputGain,step,.05f,0,1)),
            Setting(Tab.Voice,"VOICE MODE",s=>s.Data.VoiceMode=="OpenMic"?"OPEN MIC":"PUSH TO TALK",(d,s,step)=>d.VoiceMode=d.VoiceMode=="OpenMic"?"PushToTalk":"OpenMic",buttons:ToggleButton),
            Key("","PUSH TO TALK","PushToTalk",0,Tab.Voice),
            MicrophoneTest,
        };

        private readonly LocalProfile profile;
        private readonly FestivalSettings settings;
        private readonly IVoiceSession voice;
        private readonly FestivalInput input;
        private readonly AudioSource tapAudio;
        private readonly FestivalMicrophoneMonitor monitor;
        private FestivalTapCalibration tapTest;
        private float tapTestEnds;
        private (string Action,int Binding)? rebinding;
        private ProfileData Data=>settings.Data;
        public Tab Shown {get;private set;}
        public string Section {get;private set;}=Sections[0];
        public string Message {get;private set;}
        public bool Failed {get;private set;}
        /// <summary>The microphone test's level, 0-1, for the meter beside TEST MICROPHONE.</summary>
        public float MicrophoneLevel=>monitor.Level;

        /// <summary>input rebinds keys and hears the tap test, which plays on tapAudio; microphone stands in for Unity's.</summary>
        public FestivalSettingsScreen(LocalProfile profile,FestivalSettings settings,IVoiceSession voice,FestivalInput input=null,AudioSource tapAudio=null,IFestivalMicrophone microphone=null)
        {
            this.profile=profile;this.settings=settings;this.voice=voice;this.input=input;this.tapAudio=tapAudio;
            monitor=new FestivalMicrophoneMonitor(microphone);
            Failed=profile.LoadError!="";Message=Failed?profile.LoadError:SavedMessage;
        }
        public static string TabName(Tab tab)=>tab.ToString().ToUpperInvariant();
        public void Open(Tab tab){if(tab!=Shown)EndTests();Shown=tab;}
        public void OpenSection(string section){if(section!=Section)EndTests();Section=section;}
        public bool Shows(Row row)=>row.Tab==Shown&&(row.Section==""||row.Section==Section);
        public string Value(Row row)=>row.Read(this);
        public bool Usable(Row row)=>row.Usable==null||row.Usable(this);
        public string ButtonLabel(Row row,int index)=>row.Running?.Invoke(this)==true?"STOP":row.Buttons[index];
        /// <summary>Presses the row's button: direction -1 is −, otherwise + or its only button.</summary>
        public string Change(Row row,int direction)=>row.Press(this,direction);
        public string ResetTab(){EndTests();return Commit(()=>settings.ResetTab(Shown.ToString()),TabName(Shown)+" reset to its defaults. Saved on this machine.");}
        public string ShowTipsAgain()=>Commit(profile.ShowTipsAgain,"First-time tips will show again.");
        /// <summary>Once a frame while the screen shows: the microphone meter and the end of a tap test.</summary>
        public void Update()
        {
            string stopped=monitor.Update(Data.InputGain);
            if(stopped!=""){Failed=true;Message=stopped;}
            if(tapTest!=null&&Time.realtimeSinceStartup>=tapTestEnds)FinishTapTest();
        }
        /// <summary>The screen is hidden or torn down.</summary>
        public void Close()=>EndTests();
        public void FocusChanged(bool focused){if(!focused){monitor.Stop("focus");EndTapTest();}}
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
        private void EndTests()
        {
            monitor.Stop("closed");EndTapTest();
            if(rebinding!=null)input?.CancelRebind();
        }

        private string KeyName(string action,int binding)
        {
            var control=input?.Map.FindAction(action);
            if(control==null)return "";
            string key=control==input.Look?input.Label(control):control.GetBindingDisplayString(binding);
            return string.IsNullOrEmpty(key)?"NOT BOUND":key.ToUpperInvariant();
        }
        private string Rebind(string label,string action,int binding)
        {
            var control=input?.Map.FindAction(action);
            if(control==null||input.Rebinding)return "";
            if(profile.ReadOnly){Failed=true;Message=profile.LoadError;return Message;}
            string before=Data.BindingOverridesJson;
            rebinding=(action,binding);Failed=false;Message="Press the new key for "+label+". Esc cancels.";
            input.Rebind(control,binding,()=>Rebound(control,binding,before));
            return "";
        }
        private void Rebound(InputAction control,int binding,string before)
        {
            rebinding=null;
            if(Data.BindingOverridesJson==before){Failed=false;Message="Controls unchanged.";return;}
            if(input.LastError!="")
            {
                Data.BindingOverridesJson=before;input.SynchronizeBindings();
                Failed=true;Message=input.LastError;return;
            }
            Failed=false;
            Message=input.BindingNotice==""?SavedMessage:Clashes(input.BindingNotice)+" already uses "+KeyName(control.name,binding)+". Rebind one of them.";
        }
        // FestivalInput names the clashing actions ("Already used by Chat, Rhythm1."); the player reads the labels on these rows.
        private static string Clashes(string notice)
        {
            const string prefix="Already used by ";
            if(!notice.StartsWith(prefix,StringComparison.Ordinal)||!notice.EndsWith(".",StringComparison.Ordinal))return notice;
            var names=notice.Substring(prefix.Length,notice.Length-prefix.Length-1).Split(new[]{", "},StringSplitOptions.None);
            for(int i=0;i<names.Length;i++)
            {
                string label=null;
                // Movement's four keys share one action, so it reads as MOVE rather than its first row.
                if(names[i]!="Move")foreach(var row in Rows)if(row.Action==names[i]){label=row.Label;break;}
                names[i]=label??names[i].ToUpperInvariant();
            }
            return string.Join(" and ",names);
        }

        private string TimingText
        {
            get
            {
                if(tapTest!=null)return "TAP "+tapTest.SampleCount+" / "+FestivalTapCalibration.BeatCount;
                return Mathf.RoundToInt(Data.TimingCalibrationMs).ToString("+0;-0;0")+" MS";
            }
        }
        private string TapTest()
        {
            if(tapTest!=null){EndTapTest();Failed=false;Message="Tap test stopped. Your rhythm timing is unchanged.";return "";}
            if(input==null||tapAudio==null)return "";
            tapTest=settings.StartTapCalibration(tapAudio);tapTestEnds=Time.realtimeSinceStartup+TapTestSeconds;
            foreach(var note in input.Notes)note.performed+=Tapped;
            Failed=false;Message="Tap a dance key on each of the "+FestivalTapCalibration.BeatCount+" beats you hear.";
            return "";
        }
        // The press's own input-event time, moved onto the audio clock, rather than the later frame that reads it.
        private void Tapped(InputAction.CallbackContext context)=>tapTest?.Tap(AudioSettings.dspTime-Math.Max(0,InputState.currentTime-context.time));
        private void FinishTapTest()
        {
            string error=Commit(tapTest.Complete,"");
            EndTapTest();
            if(error=="")Message="Rhythm timing saved: "+TimingText+".";
        }
        private void EndTapTest()
        {
            if(tapTest==null)return;
            foreach(var note in input.Notes)note.performed-=Tapped;
            tapTest.Dispose();tapTest=null;
        }

        private string MicrophoneName
        {
            get
            {
                var devices=monitor.Devices;
                if(devices.Length==0)return monitor.Blocked!=""?"NOT IN THIS BUILD YET":"NO MICROPHONE FOUND";
                if(Data.MicrophoneDevice=="")return "SYSTEM DEFAULT";
                return Array.IndexOf(devices,Data.MicrophoneDevice)>=0?Data.MicrophoneDevice:"SYSTEM DEFAULT (SAVED MIC MISSING)";
            }
        }
        private string MicrophoneMeter=>monitor.State==FestivalMicrophoneMonitor.Phase.Listening?Percent(monitor.Level)
            :monitor.State==FestivalMicrophoneMonitor.Phase.Asking?"ASKING PERMISSION":"OFF";
        // The system default, then each device the platform reports. A saved device that is missing steps from the default.
        private string ChangeMicrophone(int direction)
        {
            var choices=new List<string>{""};choices.AddRange(monitor.Devices);
            string error=Commit(()=>{Data.MicrophoneDevice=Next(choices.ToArray(),Data.MicrophoneDevice,direction);return settings.Save();},SavedMessage);
            if(error==""&&monitor.State!=FestivalMicrophoneMonitor.Phase.Off)StartMicrophone();
            return error;
        }
        private string TestMicrophone()
        {
            if(monitor.State!=FestivalMicrophoneMonitor.Phase.Off){monitor.Stop("dismissed");Failed=false;Message="Microphone test stopped.";return "";}
            return StartMicrophone();
        }
        private string StartMicrophone()
        {
            string error=monitor.Start(Data.MicrophoneDevice);
            Failed=error!="";Message=Failed?error:"Testing your mic on this computer only. Nothing is recorded, sent or played back.";
            return error;
        }

        private static string ModeName(FullScreenMode mode)=>mode==FullScreenMode.ExclusiveFullScreen?"FULLSCREEN":mode==FullScreenMode.FullScreenWindow?"BORDERLESS":mode==FullScreenMode.MaximizedWindow?"MAXIMIZED":"WINDOWED";
        private static string Size(int width,int height)=>width+" × "+height;
        private static string Percent(float value)=>Mathf.RoundToInt(value*100)+"%";
        private static Row Setting(Tab tab,string label,Func<FestivalSettingsScreen,string> read,Action<ProfileData,FestivalSettingsScreen,int> step,
            Func<FestivalSettingsScreen,bool> usable=null,string section="",string[] buttons=null)
            =>new Row(tab,section,label,buttons??StepButtons,read,(s,direction)=>s.Commit(()=>{step(s.Data,s,direction);return s.settings.Save();},SavedMessage),usable);
        private static Row Volume(string label,Func<ProfileData,float> read,Action<ProfileData,float> write)
            =>Setting(Tab.Audio,label,s=>Percent(read(s.Data)),(d,s,step)=>write(d,Steps(read(d),step,.05f,0,1)));
        private static Row Switch(Tab tab,string label,Func<ProfileData,bool> read,Action<ProfileData,bool> write,string section="")
            =>Setting(tab,label,s=>read(s.Data)?"ON":"OFF",(d,s,step)=>write(d,!read(d)),section:section,buttons:ToggleButton);
        private static Row Key(string section,string label,string action,int binding=0,Tab tab=Tab.Controls)
            =>new Row(tab,section,label,new[]{"REBIND"},s=>s.rebinding==(action,binding)?"PRESS A KEY":s.KeyName(action,binding),(s,step)=>s.Rebind(label,action,binding),
                s=>s.input!=null,action:action);
        // Rounded to hundredths so repeated steps land on the values the rows read.
        private static float Steps(float value,int direction,float step,float min,float max)=>Mathf.Clamp(Mathf.Round((value+direction*step)*100)/100,min,max);
        private static T Next<T>(T[] values,T current,int direction)
        {
            int at=Array.IndexOf(values,current);
            return values[Mathf.Clamp(at<0?0:at+direction,0,values.Length-1)];
        }
        // CURRENT first, then each size the platform reports (FestivalSettings drops duplicates and adds the window's own size).
        private static void NextResolution(ProfileData data,FestivalSettingsScreen screen,int direction)
        {
            var sizes=new List<Resolution>{new Resolution()};sizes.AddRange(screen.settings.AvailableResolutions);
            int at=sizes.FindIndex(r=>r.width==data.ResolutionWidth&&r.height==data.ResolutionHeight);
            var next=sizes[Mathf.Clamp(at<0?0:at+direction,0,sizes.Count-1)];
            data.ResolutionWidth=next.width;data.ResolutionHeight=next.height;
        }
    }
}
