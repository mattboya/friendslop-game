using System;
using System.IO;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class WeekendSessionTests
    {
        [Test] public void ClientsSeeTheWeekendPosition()
        {
            var game=new FestivalSimulation(9);game.AddPlayer("host","Host");var friend=game.AddPlayer("friend","Friend");
            game.State.UnlockedFestivalCount=2;game.State.FestivalIndex=1;game.State.LevelIndex=2;game.State.EncoreTier=3;
            foreach(var life in new[]{"Alive","Spirit"})
            {
                friend.Life=life;var view=FestivalSession.ViewFor(game,friend.Id);
                Assert.That(new[]{view.FestivalIndex,view.LevelIndex,view.EncoreTier,view.UnlockedFestivalCount},Is.EqualTo(new[]{1,2,3,2}),life+" players see which festival, level and encore lap this is");
            }
        }

        [Test] public void HostProfileRemembersUnlockedFestivals()
        {
            string id="weekend_test_"+Guid.NewGuid().ToString("N").Substring(0,12);
            string file=Path.Combine(Application.persistentDataPath,"profiles",id+".json");
            try
            {
                var profile=new LocalProfile(id);
                Assert.That(profile.Data.UnlockedFestivalCount,Is.EqualTo(1),"a new profile has only the first festival");
                var cleared=new FestivalSimulation(4);cleared.State.UnlockedFestivalCount=2;
                profile.RememberUnlocks(cleared.State);
                var hosted=new FestivalSimulation(5);new LocalProfile(id).ApplyUnlocks(hosted.State);
                Assert.That(hosted.State.UnlockedFestivalCount,Is.EqualTo(2),"the next hosted session starts with the saved unlocks");
                var fresh=new FestivalSimulation(6);profile.RememberUnlocks(fresh.State);
                Assert.That(new LocalProfile(id).Data.UnlockedFestivalCount,Is.EqualTo(2),"a session with fewer unlocks never shrinks the saved count");
                File.WriteAllText(file,File.ReadAllText(file).Replace("\"UnlockedFestivalCount\": 2","\"UnlockedFestivalCount\": 99"));
                Assert.That(new LocalProfile(id).Data.UnlockedFestivalCount,Is.EqualTo(Festivals.Count),"a tampered profile cannot unlock festivals that do not exist");
            }
            finally{if(File.Exists(file))File.Delete(file);}
        }
    }
}
