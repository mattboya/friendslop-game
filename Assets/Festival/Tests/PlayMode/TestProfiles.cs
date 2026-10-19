using System.IO;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // Under the Editor, Application.persistentDataPath/profiles is the Editor's Play-mode profile (the built player keeps its
    // own, on macOS under its bundle id), so a test never saves one there; every hosted session's "default" profile included
    // (FestivalSession saves through LocalProfile).
    public sealed class PlayModeProfileTests:FestivalPlayModeTest
    {
        [Test]public void ATestNeverSavesIntoThisMachinesOwnProfiles()
        {
            string id="impl-U01-probe-"+System.Guid.NewGuid().ToString("N").Substring(0,16);
            string mine=Path.Combine(Application.persistentDataPath,"profiles",id+".json");
            var probe=new LocalProfile(id);probe.Data.PlayerName="CAM-2 probe";
            Assert.That(probe.Save(),Is.Empty,"setup: saved");
            Assert.That(File.Exists(mine),Is.False,"saved into this machine's own profiles: "+mine);
            Assert.That(new LocalProfile(id).Data.PlayerName,Is.EqualTo("CAM-2 probe"),"loads back what it saved, from the test's own folder");
        }
    }
}

// CAM-2: outside any namespace, so it wraps every test in this assembly: for the run, profiles live in a temp folder of their own,
// starting empty (ProfileData's defaults). Hosted tests own a fresh subfolder inside it. Teardown deletes only its own folder.
[SetUpFixture]public sealed class TestProfiles
{
    private string folder,previousFolder;
    [OneTimeSetUp]public void UseATempFolder(){previousFolder=LocalProfile.Folder;LocalProfile.Folder=folder=Path.Combine(Path.GetTempPath(),"impl-U01-profiles-"+System.Guid.NewGuid().ToString("N"));}
    [OneTimeTearDown]public void DropIt(){LocalProfile.Folder=previousFolder;if(folder!=null&&Directory.Exists(folder))Directory.Delete(folder,true);}
}
