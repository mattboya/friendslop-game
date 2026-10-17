using System;
using System.IO;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>The platform's microphones, as Unity's Microphone class reports and records them.</summary>
    public interface IFestivalMicrophone
    {
        string[] Devices {get;}
        /// <summary>Why this build cannot ask for the microphone at all, or "".</summary>
        string Blocked {get;}
        bool Authorized {get;}
        bool AuthorizationPending {get;}
        void RequestAuthorization();
        /// <summary>Starts recording device ("" is the system default) into a looping clip, or returns null.</summary>
        AudioClip Start(string device);
        int GetPosition(string device);
        bool IsRecording(string device);
        void End(string device);
    }
    /// <summary>
    /// SET-1: the VOICE tab's TEST MICROPHONE meter. It records only between Start (the player's TEST press) and Stop, and only to
    /// read a level: the clip is never played, saved or sent. Any failure, lost permission or lost device stops it.
    /// </summary>
    public sealed class FestivalMicrophoneMonitor:IDisposable
    {
        public enum Phase{Off,Asking,Listening}
        public Phase State {get;private set;}
        public float Level {get;private set;}
        public string[] Devices=>microphone.Devices??Array.Empty<string>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Automated runs put a device fixture here, so no test ever opens the room's microphone.
        public static IFestivalMicrophone Substitute {get;set;}
#endif
        private readonly IFestivalMicrophone microphone;
        private string device="";
        private AudioClip clip;
        private float[] window;

        public FestivalMicrophoneMonitor(IFestivalMicrophone microphone=null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            microphone=microphone??Substitute;
#endif
            this.microphone=microphone??new PlatformMicrophone();
        }
        /// <summary>The device a test of the saved choice records: the saved one while connected, otherwise the system default.</summary>
        public static string EffectiveDevice(string[] devices,string saved)=>!string.IsNullOrEmpty(saved)&&Array.IndexOf(devices,saved)>=0?saved:"";
        public string Start(string savedDevice)
        {
            Stop("restart");
            var devices=Devices;
            if(devices.Length==0)return "No microphone found.";
            if(microphone.Blocked!="")return microphone.Blocked;
            device=EffectiveDevice(devices,savedDevice);
            if(microphone.Authorized)return Open();
            microphone.RequestAuthorization();State=Phase.Asking;
            return "";
        }
        /// <summary>Reads the level once a frame. Returns why the test just stopped, or "".</summary>
        public string Update(float gain)
        {
            if(State==Phase.Asking)
            {
                if(microphone.AuthorizationPending)return "";
                return microphone.Authorized?Open():Fail("Microphone permission was denied. Allow it in your system's privacy settings, then test again.");
            }
            if(State!=Phase.Listening)return "";
            if(!microphone.Authorized)return Fail("Microphone permission was turned off, so the test stopped.");
            var devices=Devices;
            if(devices.Length==0||device!=""&&Array.IndexOf(devices,device)<0||!microphone.IsRecording(device))return Fail("The microphone stopped. Check it is connected, then test again.");
            int samples=clip.samples,start=((microphone.GetPosition(device)-window.Length/clip.channels)%samples+samples)%samples;
            clip.GetData(window,start);
            float peak=0;foreach(float sample in window)peak=Math.Max(peak,Math.Abs(sample));
            Level=Mathf.Clamp01(peak*gain);
            return "";
        }
        public void Stop(string reason)
        {
            if(State==Phase.Off)return;
            if(State==Phase.Listening)microphone.End(device);
            if(clip!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(clip);else UnityEngine.Object.DestroyImmediate(clip);}
            clip=null;State=Phase.Off;Level=0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Settings] Microphone test stopped: "+reason+".");
#endif
        }
        public void Dispose()=>Stop("teardown");
        private string Open()
        {
            clip=microphone.Start(device);
            if(clip==null){State=Phase.Off;return "The microphone could not start. Choose another one, then test again.";}
            // About a tenth of a second of the newest samples, enough for a lively meter.
            window=new float[Math.Max(1,Math.Min(clip.samples,clip.frequency/10))*clip.channels];
            State=Phase.Listening;Level=0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Settings] Microphone test started; nothing is played back or sent.");
#endif
            return "";
        }
        private string Fail(string reason){Stop("failed");return reason;}

        private sealed class PlatformMicrophone:IFestivalMicrophone
        {
            private AsyncOperation request;
            public string[] Devices=>Microphone.devices;
            public string Blocked
            {
                get
                {
                    // macOS ends a process that opens the microphone when its Info.plist has no usage description.
                    if(Application.platform!=RuntimePlatform.OSXPlayer)return "";
                    try{if(File.ReadAllText(Path.Combine(Application.dataPath,"Info.plist")).Contains("NSMicrophoneUsageDescription"))return "";}
                    catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){}
                    return "This build cannot ask for the microphone yet, so nothing was recorded.";
                }
            }
            // Desktop systems ask on the first recording themselves; Unity's authorization calls cover phones and the web.
            public bool Authorized=>!(Application.isMobilePlatform||Application.platform==RuntimePlatform.WebGLPlayer)||Application.HasUserAuthorization(UserAuthorization.Microphone);
            public bool AuthorizationPending=>request!=null&&!request.isDone;
            public void RequestAuthorization()=>request=Application.RequestUserAuthorization(UserAuthorization.Microphone);
            public AudioClip Start(string device)
            {
                Microphone.GetDeviceCaps(device,out int min,out int max);
                // Unity throws for a device unplugged since the list was read.
                try{return Microphone.Start(device,true,1,max==0?16000:Mathf.Clamp(16000,min,max));}
                catch(ArgumentException){return null;}
            }
            public int GetPosition(string device)=>Microphone.GetPosition(device);
            public bool IsRecording(string device)=>Microphone.IsRecording(device);
            public void End(string device)=>Microphone.End(device);
        }
    }
}
