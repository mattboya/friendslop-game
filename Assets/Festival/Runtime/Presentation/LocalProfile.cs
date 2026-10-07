using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Festival.Core;
using UnityEngine;
namespace Festival.Presentation
{
    [Serializable] public sealed class ProfileData
    {
        public int SchemaVersion=2;
        public List<string> Cosmetics=new List<string>();
        public List<string> SeenDialogue=new List<string>();
        public bool ReducedMotion=false;
        public bool HighContrast=false;
        public float TimingCalibrationMs;
        public float MouseSensitivity=0.12f;
        public float MusicVolume=0.35f;
        // Host only: festivals this machine has unlocked by clearing the one before.
        public int UnlockedFestivalCount=1;
        // CAM-1: the view over your shoulder rather than first person (V, or CAMERA VIEW). Older profiles load it as false.
        public bool ThirdPerson;
        // HUD-7: the first-time tips this machine has shown (FestivalHudText.Tip). Older profiles load it empty.
        public List<string> SeenTips=new List<string>();
        // MENU-1: what PLAY fills in, as HOST A GAME or JOIN A FRIEND last used it: your name and the host you last joined. Older
        // profiles load the defaults.
        public string PlayerName=DefaultName;
        public string LastHost=DefaultHost;
        public const string DefaultName="Friend",DefaultHost="127.0.0.1";
        // SET-1: all additive settings are initialized before JSON is applied, including partial schema-2 saves.
        public float MasterVolume=1,EffectsVolume=.6f,VoiceVolume=1;
        public bool MuteWhenUnfocused=true;
        // Current/0/-1 retain the platform display, resolution and vsync until the graphics controller applies a choice.
        public string DisplayMode="Current",QualityPreset="High";
        public int ResolutionWidth=0,ResolutionHeight=0,VSyncCount=-1,FrameCap=60;
        public float RenderScale=1,FieldOfView=75,Brightness=0;
        public bool InvertY=false;
        public string BindingOverridesJson="";
        public bool CameraShake=true,ComfortPromptComplete=false;
        public float TextScale=1;
        public string MicrophoneDevice="",VoiceMode="PushToTalk";
        public float InputGain=1;
        // CAMP-3: stable IDs shared with the authoritative egg table; observing a friend's gag grants no discovery.
        public List<string> FoundCampEggIds=new List<string>();
    }
    public sealed class LocalProfile
    {
        public ProfileData Data {get;private set;}
        public string LoadError {get;private set;}="";
        public string LastError {get;private set;}="";
        public bool ReadOnly {get;private set;}
        // U66 binds these exact strings in the pass. Unknown and duplicate IDs never increase the displayed count.
        public string CampEggCountText=>"EASTER EGGS "+CampEggIds().Count+" / "+CampFeatures.Eggs.Length;
        public string CampEggCompletionText=>CampEggIds().Count==CampFeatures.Eggs.Length?"You found every dumb secret. Seek help.":"";
        [Serializable] private sealed class SchemaHeader { public int SchemaVersion=0; }
        // CAM-2: where profiles are saved. This machine's own folder is the one the built game reads too, so tests point it at a
        // temp folder for their whole run (TestProfiles); null puts it back.
        public static string Folder {get=>folder??Path.Combine(Application.persistentDataPath,"profiles");set=>folder=value;}
        private static string folder;
        private readonly string path;
        private const int MaxFileBytes=65536;
        public LocalProfile(string id)
        {
            var safe=System.Text.RegularExpressions.Regex.Replace(id??"default","[^a-zA-Z0-9_-]", "_");
            if(safe.Length>40)safe=safe.Substring(0,40);
            path=Path.Combine(Folder,safe+".json");
            Data=new ProfileData();
            try
            {
                if(File.Exists(path))
                {
                    string error=ReadSavedData(out string json,out int version);
                    if(error!=""){Refuse(error);return;}
                    var loaded=new ProfileData();
                    if(version==1){loaded.ReducedMotion=true;loaded.HighContrast=true;}
                    JsonUtility.FromJsonOverwrite(json,loaded);
                    Data=version==1?Migrate(loaded):loaded;
                }
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Settings] Load failed: "+e.GetType().Name+": "+e.Message);
#endif
                Refuse("Settings could not be loaded. The saved file was left unchanged.");
            }
            FestivalSettings.Validate(Data);
        }
        private string ReadSavedData(out string json,out int version)
        {
            json="";version=0;
            if(new FileInfo(path).Length>=MaxFileBytes)return "This profile is too large to load. The saved file was left unchanged.";
            json=File.ReadAllText(path);
            var header=JsonUtility.FromJson<SchemaHeader>(json);
            version=header?.SchemaVersion??0;
            if(version>2)return "This profile was saved by a newer build. Settings cannot be saved here.";
            return version==1||version==2?"":"This profile uses an unsupported schema. Settings cannot be saved here.";
        }
        private string Refuse(string error)
        {
            ReadOnly=true;LoadError=error;return Error(error);
        }
        private static ProfileData Migrate(ProfileData old)
        {
            // Copy only schema-1 fields into fresh defaults. Unknown newer fields in a v1 file are not migration choices.
            return new ProfileData{Cosmetics=old.Cosmetics,SeenDialogue=old.SeenDialogue,SeenTips=old.SeenTips,
                ReducedMotion=old.ReducedMotion,HighContrast=old.HighContrast,TimingCalibrationMs=old.TimingCalibrationMs,
                MouseSensitivity=old.MouseSensitivity,MusicVolume=old.MusicVolume,UnlockedFestivalCount=old.UnlockedFestivalCount,
                ThirdPerson=old.ThirdPerson,PlayerName=old.PlayerName,LastHost=old.LastHost,ComfortPromptComplete=true};
        }
        public string Save()
        {
            if(ReadOnly)return Error(LoadError);
            string temp=null;
            try
            {
                // Recheck at save time: another build may have upgraded the file since this instance loaded it.
                if(File.Exists(path))
                {
                    string error=ReadSavedData(out _,out _);
                    if(error!="")return Refuse(error);
                }
                FestivalSettings.Validate(Data);
                if(Data.SchemaVersion!=2)return Error("Settings use an unsupported schema and could not be saved.");
                byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(Data,true));
                if(bytes.Length>=MaxFileBytes)return Error("Settings are too large to save. The previous profile was left unchanged.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                // Recheck immediately before the same-directory atomic replacement too.
                if(File.Exists(path))
                {
                    string error=ReadSavedData(out _,out _);
                    if(error!="")return Refuse(error);
                    File.Replace(temp,path,null);
                }
                else File.Move(temp,path);
                LastError="";return "";
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Settings] Save failed: "+e.GetType().Name+": "+e.Message);
#endif
                return Error("Settings could not be saved.");
            }
            finally
            {
                // This attempt owns its unique temp file; never remove another caller's staging file.
                if(temp!=null&&File.Exists(temp))
                {
                    try{File.Delete(temp);}
                    catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log("[Settings] Temp cleanup failed: "+e.GetType().Name+": "+e.Message);
#endif
                    }
                }
            }
        }
        private string Error(string message)
        {
            LastError=message;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Settings] "+message);
#endif
            return message;
        }
        // Only authenticated host acknowledgments reach this API. Keep the existing bounded schema-2 representation.
        public string RememberDialogue(List<string> keys)
        {
            if(!Festival.Network.FestivalSession.SafeDialogueKeys(keys))return "Invalid conversation history.";
            var previous=Data.SeenDialogue;var merged=new List<string>(previous??new List<string>());
            foreach(string key in keys){merged.RemoveAll(value=>value==key);merged.Add(key);}
            while(merged.Count>512)merged.RemoveAt(0);
            if(previous!=null&&System.Linq.Enumerable.SequenceEqual(previous,merged))return "";
            Data.SeenDialogue=merged;string error=Save();if(error!="")Data.SeenDialogue=previous;return error;
        }
        // A hosted session starts with this machine's unlocks and saves them back whenever they grow.
        public void ApplyUnlocks(RoundState state) { state.UnlockedFestivalCount=Math.Max(state.UnlockedFestivalCount,Data.UnlockedFestivalCount); }
        public void RememberUnlocks(RoundState state)
        {
            if(state.UnlockedFestivalCount<=Data.UnlockedFestivalCount)return;
            Data.UnlockedFestivalCount=Math.Min(state.UnlockedFestivalCount,Festivals.Count);Save();
        }
        // The recipient-filtered cumulative mask recovers pokes between snapshots. Never infer credit from public gag events.
        public string RememberCampEggs(RoundState state,string localPlayerId)
        {
            var player=state?.Players.Find(p=>p.Id==localPlayerId);
            if(player==null||string.IsNullOrEmpty(localPlayerId))return "";
            var found=CampEggIds(player.FoundCampEggMask);
            bool changed=Data.FoundCampEggIds==null||found.Count!=Data.FoundCampEggIds.Count;
            for(int index=0;!changed&&index<found.Count;index++)changed=found[index]!=Data.FoundCampEggIds[index];
            if(!changed)return "";
            var previous=Data.FoundCampEggIds;
            Data.FoundCampEggIds=found;
            string error=Save();
            if(error!="")Data.FoundCampEggIds=previous;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(error=="")Debug.Log("[CampEggs] Saved local discoveries: "+Data.FoundCampEggIds.Count);
#endif
            return error;
        }
        private List<string> CampEggIds(int mask=0)
        {
            var found=new List<string>();
            for(int index=0;index<CampFeatures.Eggs.Length;index++)
            {
                string id=CampFeatures.Eggs[index].Id;
                if(mask>=0&&(mask&(1<<index))!=0||Data.FoundCampEggIds?.Contains(id)==true)found.Add(id);
            }
            return found;
        }
        // HUD-7: a first-time tip shows once on this machine (FestivalHudText.Tip), until "Show tips again" in the settings, which
        // returns Save's error for the settings to say.
        public void SeeTip(string id){if(Data.SeenTips.Contains(id))return;Data.SeenTips.Add(id);Save();}
        public string ShowTipsAgain(){Data.SeenTips.Clear();return Save();}
        public void AwardFirstRescue()
        {
            if(!Data.Cosmetics.Contains("first_shuttle")) { Data.Cosmetics.Add("first_shuttle"); Save(); }
        }
    }
}
