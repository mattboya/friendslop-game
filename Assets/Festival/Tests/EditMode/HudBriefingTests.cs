using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Festival.Tests
{
    // HUD-7 (Matt: "if i had never played the game, it would be very unclear what i am supposed to do and what the objective is"):
    // the text a first-time player reads, from crafted round states the way a client's view holds them. The level briefing says
    // what this level asks, with real numbers, what this player's job is and how the crew loses.
    public sealed class HudBriefingTests
    {
        static RoundState Level(int festival,int level,string phase="Playing")=>new RoundState{FestivalIndex=festival,LevelIndex=level,Phase=phase,DurationSeconds=Festivals.Level(festival,level,0).DurationSeconds};
        static PlayerState Crew(RoundState s,string id,string name,int ordinal=0,string life="Alive"){var p=new PlayerState{Id=id,Name=name,Ordinal=ordinal,Life=life};s.Players.Add(p);return p;}
        // A level just under way for you and Sam, Sam tripping.
        static RoundState Start(int festival,int level){var s=Level(festival,level);Crew(s,"you","You");Crew(s,"sam","Sam",1);s.TripperId="sam";return s;}
        static string[] Lines(RoundState s,string id)=>FestivalHudText.Briefing(s,id).Split('\n');
        const string Lose="Run out of time or lose the whole crew, and the festival starts over at Day 1.";

        [Test] public void ADayBriefingSaysTheQuotaTheJobAndHowYouLose()
        {
            var s=Start(0,0);
            Assert.That(Lines(s,"sam"),Is.EqualTo(new[]{"DAY 1  •  SELL $30 BEFORE SUNDOWN",
                "You're tripping: only you can see who's buying. Trust, but verify: some visions lie, so check one with F first.",Lose}),
                "the crew's whole quota (two of you at $15), the tripper's job with the game's one hint folded in, and the stakes");
            Assert.That(Lines(s,"you"),Is.EqualTo(new[]{"DAY 1  •  SELL $30 BEFORE SUNDOWN","Sam is tripping and can see who's buying. Stay close and act on what they call out.",Lose}),
                "a friend reads the same goal, and who to stick with");
            Assert.That(FestivalHudText.Briefing(s,"you"),Does.Not.Contain("Trust, but verify"),"the one hint is the tripper's alone");
            var day2=Start(0,2);
            Assert.That(Lines(day2,"you")[0],Is.EqualTo("DAY 2  •  SELL $40 BEFORE SUNDOWN"),"Day 2 names itself and its higher quota");
        }

        [Test] public void OnEmberPlayaTheQuotaReadsInYourOwnOddObjects()
        {
            var s=Start(Festivals.PlayaFestival,0);
            Assert.That(Lines(s,"you")[0],Is.EqualTo("DAY 1  •  SELL 44 BUTTONS BEFORE SUNDOWN"));
            Assert.That(Lines(s,"sam")[0],Is.EqualTo("DAY 1  •  SELL 44 RAMEN PACKETS BEFORE SUNDOWN"),"each viewer counts in their own odd object (PLAYA-1)");
        }

        [Test] public void ANightBriefingIsTheRescue()
        {
            var s=Start(0,1);
            Assert.That(Lines(s,"sam"),Is.EqualTo(new[]{"NIGHT 1  •  FIND YOUR LOST FRIEND AND BRING THEM BACK TO CAMP",
                "You're tripping: only you can see the clue trail. Trust, but verify: some visions lie, so check one with F first.",Lose}));
            Assert.That(Lines(s,"you"),Is.EqualTo(new[]{"NIGHT 1  •  FIND YOUR LOST FRIEND AND BRING THEM BACK TO CAMP",
                "Sam is tripping and can see the clue trail. Stay close and act on what they call out.",Lose}));
            s.SecondFriend.Active=true;
            Assert.That(Lines(s,"you")[0],Is.EqualTo("NIGHT 1  •  FIND YOUR TWO LOST FRIENDS AND BRING THEM BACK TO CAMP"),"a big crew's night has two (CROWD-2)");
            var finale=Start(0,3);
            Assert.That(Lines(finale,"sam")[0],Is.EqualTo("NIGHT 2  •  RESCUE YOUR FRIEND, THEN GET EVERYONE HOME"));
            Assert.That(Lines(finale,"you")[1],Is.EqualTo("Sam is tripping and can see the clue trail. Stay close and act on what they call out."));
            finale.SecondFriend.Active=true;
            Assert.That(Lines(finale,"you")[0],Is.EqualTo("NIGHT 2  •  RESCUE BOTH FRIENDS, THEN GET EVERYONE HOME"));
        }

        [Test] public void TheBriefingShowsOnlyAsALevelStartsAndNeverToASpirit()
        {
            var s=Start(0,0);
            Assert.That(FestivalHudText.Briefing(s,"you"),Is.Not.Empty,"as play starts");
            s.ElapsedSeconds=FestivalHudText.BriefingSeconds-.1;
            Assert.That(FestivalHudText.Briefing(s,"you"),Is.Not.Empty,"for the whole window");
            s.ElapsedSeconds=FestivalHudText.BriefingSeconds;
            Assert.That(FestivalHudText.Briefing(s,"you"),Is.Empty,"then it is gone, and anyone joining later never sees it");
            TestDelegate after=()=>FestivalHudText.Briefing(s,"you");after();
            Assert.That(after,Is.Not.AllocatingGCMemory(),"asked every frame after that, it makes no garbage");
            Assert.That(FestivalHudText.BriefingSeconds,Is.EqualTo(10),"ten seconds of level clock");
            foreach(var phase in new[]{"Shopping","CampReview","Spinning","Loading","Results"}){var at=Start(0,0);at.Phase=phase;Assert.That(FestivalHudText.Briefing(at,"you"),Is.Empty,"never at "+phase);}
            var spirit=Start(0,1);spirit.Players[0].Life="Spirit";
            Assert.That(FestivalHudText.Briefing(spirit,"you"),Is.Empty,"a spirit sees no card");
            Assert.That(FestivalHudText.Briefing(spirit,"nobody"),Is.Empty,"nor does a viewer the view doesn't hold");
        }
    }
}
