using System;
using System.Collections.Generic;
using System.IO;
using Festival.Core;
using UnityEngine;
namespace Festival.Presentation
{
    [Serializable] public sealed class ProfileData
    {
        public int SchemaVersion=1;
        public List<string> Cosmetics=new List<string>();
        public List<string> SeenDialogue=new List<string>();
        public bool ReducedMotion=true;
        public bool HighContrast=true;
        public float TimingCalibrationMs;
        public float MouseSensitivity=0.12f;
        public float MusicVolume=0.35f;
        // Host only: festivals this machine has unlocked by clearing the one before.
        public int UnlockedFestivalCount=1;
    }
    public sealed class LocalProfile
    {
        public ProfileData Data {get;private set;}
        private readonly string path;
        public LocalProfile(string id)
        {
            var safe=System.Text.RegularExpressions.Regex.Replace(id??"default","[^a-zA-Z0-9_-]", "_");
            if(safe.Length>40)safe=safe.Substring(0,40);
            path=Path.Combine(Application.persistentDataPath,"profiles",safe+".json");
            Data=new ProfileData();
            try
            {
                if(File.Exists(path) && new FileInfo(path).Length<65536)
                {
                    var loaded=JsonUtility.FromJson<ProfileData>(File.ReadAllText(path));
                    if(loaded!=null && loaded.SchemaVersion==1 && loaded.Cosmetics!=null && loaded.SeenDialogue!=null && loaded.SeenDialogue.Count<=512)
                        Data=loaded;
                }
            }
            catch(Exception e) when(e is IOException || e is ArgumentException) { }
            Data.TimingCalibrationMs=FiniteClamp(Data.TimingCalibrationMs,-200,200,0);
            Data.MouseSensitivity=FiniteClamp(Data.MouseSensitivity,0.02f,0.5f,0.12f);
            Data.MusicVolume=FiniteClamp(Data.MusicVolume,0,0.7f,0.35f);
            Data.UnlockedFestivalCount=Mathf.Clamp(Data.UnlockedFestivalCount,1,Festivals.Count);
            Data.Cosmetics.RemoveAll(x=>x!="first_shuttle");
        }
        private static float FiniteClamp(float v,float min,float max,float fallback) => float.IsNaN(v)||float.IsInfinity(v)?fallback:Mathf.Clamp(v,min,max);
        public string Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",JsonUtility.ToJson(Data,true));
                // Same-directory rename avoids partial JSON after a interrupted write.
                if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
                return "";
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) { return "Settings could not be saved."; }
        }
        // A hosted session starts with this machine's unlocks and saves them back whenever they grow.
        public void ApplyUnlocks(RoundState state) { state.UnlockedFestivalCount=Math.Max(state.UnlockedFestivalCount,Data.UnlockedFestivalCount); }
        public void RememberUnlocks(RoundState state)
        {
            if(state.UnlockedFestivalCount<=Data.UnlockedFestivalCount)return;
            Data.UnlockedFestivalCount=Math.Min(state.UnlockedFestivalCount,Festivals.Count);Save();
        }
        public void AwardFirstRescue()
        {
            if(!Data.Cosmetics.Contains("first_shuttle")) { Data.Cosmetics.Add("first_shuttle"); Save(); }
        }
    }
}
