#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Festival.Presentation
{
    // UI smoke replaces sessions before deferred OnDestroy. Leases keep their process-owned storage live until the last one leaves.
    internal sealed class DevelopmentSmokeProfileScope : IDisposable
    {
        static string ownedFolder,previousFolder,previousScope;
        static int leases;
        static readonly HashSet<string> bindingKeys=new HashSet<string>();
        bool disposed;

        internal static DevelopmentSmokeProfileScope Load(string[] args,string profile,Action initialize)
        {
            DevelopmentSmokeProfileScope lease=null;
            if(Array.IndexOf(args,"--smoke-test")>=0||Array.IndexOf(args,"--solo-smoke-test")>=0||Array.IndexOf(args,"--ui-screens")>=0)
            {
                if(leases==0)
                {
                    string folder=Path.Combine(Path.GetTempPath(),"festival-smoke-profiles-"+Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(folder);
                    previousFolder=LocalProfile.Folder;previousScope=FestivalInput.PreferencesScope;ownedFolder=folder;
                    LocalProfile.Folder=FestivalInput.PreferencesScope=ownedFolder;
                    Debug.Log("[Festival.SmokeProfiles] owned temporary storage opened");
                }
                lease=new DevelopmentSmokeProfileScope();leases++;
                bindingKeys.Add("festival-bindings-"+ownedFolder+"-"+profile);
            }
            try{initialize();return lease;}
            catch{lease?.Dispose();throw;}
        }

        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(--leases!=0)return;
            // A later caller override belongs to that caller; an old session never overwrites it.
            if(LocalProfile.Folder==ownedFolder)LocalProfile.Folder=previousFolder;
            if(FestivalInput.PreferencesScope==ownedFolder)FestivalInput.PreferencesScope=previousScope;
            string retired=ownedFolder;ownedFolder=previousFolder=previousScope=null;
            try
            {
                foreach(string key in bindingKeys)PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            finally
            {
                bindingKeys.Clear();
                try{if(Directory.Exists(retired))Directory.Delete(retired,true);}
                catch(Exception e) when(e is IOException||e is UnauthorizedAccessException)
                {
                    Debug.Log("[Festival.SmokeProfiles] owned storage cleanup failed: "+e.GetType().Name);
                }
            }
            Debug.Log("[Festival.SmokeProfiles] owned temporary storage released");
        }
    }
}
#endif
