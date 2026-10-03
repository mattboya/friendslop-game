using System.IO;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // CAM-2: this machine's own profiles (Application.persistentDataPath/profiles) are the ones the built game reads too, so a
    // test never saves one there; every hosted session's "default" profile included (FestivalSession saves through LocalProfile).
    public sealed class PlayModeProfileTests
    {
        [Test]public void ATestNeverSavesIntoThisMachinesOwnProfiles()
        {
            string mine=Path.Combine(Application.persistentDataPath,"profiles","cam2-probe.json");
            try
            {
                var probe=new LocalProfile("cam2-probe");probe.Data.PlayerName="CAM-2 probe";
                Assert.That(probe.Save(),Is.Empty,"setup: saved");
                Assert.That(File.Exists(mine),Is.False,"saved into this machine's own profiles: "+mine);
                Assert.That(new LocalProfile("cam2-probe").Data.PlayerName,Is.EqualTo("CAM-2 probe"),"loads back what it saved, from the run's own folder");
            }
            finally{if(File.Exists(mine))File.Delete(mine);}
        }
    }
}

// CAM-2: outside any namespace, so it wraps every test in this assembly: for the run, profiles live in a temp folder of their own,
// starting empty (ProfileData's defaults). The teardown deletes only the folder it made, never LocalProfile.Folder's fallback.
[SetUpFixture]public sealed class TestProfiles
{
    private string folder;
    [OneTimeSetUp]public void UseATempFolder()=>LocalProfile.Folder=folder=Path.Combine(Path.GetTempPath(),"festival-test-profiles-"+System.Guid.NewGuid().ToString("N"));
    [OneTimeTearDown]public void DropIt(){LocalProfile.Folder=null;if(folder!=null&&Directory.Exists(folder))Directory.Delete(folder,true);}
}
