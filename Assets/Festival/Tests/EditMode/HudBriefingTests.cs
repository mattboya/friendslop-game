using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
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

        // The card fades in as the level opens and out as its window closes; reduced motion drops the fade, so it is whole throughout.
        [Test] public void TheBriefingFadesInAndOutUnlessMotionIsReduced()
        {
            double fade=FestivalHudText.BriefingFadeSeconds,end=FestivalHudText.BriefingSeconds;
            Assert.That(fade,Is.GreaterThan(0).And.LessThanOrEqualTo(.5),"a short fade, not a slow one");
            Assert.That(FestivalHudText.BriefingAlpha(0,false),Is.EqualTo(0),"it fades in as the level opens");
            Assert.That(FestivalHudText.BriefingAlpha(fade/2,false),Is.EqualTo(.5f).Within(.001f));
            Assert.That(FestivalHudText.BriefingAlpha(fade,false),Is.EqualTo(1),"then it is whole");
            Assert.That(FestivalHudText.BriefingAlpha(end/2,false),Is.EqualTo(1),"while it is read");
            Assert.That(FestivalHudText.BriefingAlpha(end-fade/2,false),Is.EqualTo(.5f).Within(.001f),"and fades out as its window closes");
            Assert.That(FestivalHudText.BriefingAlpha(end,false),Is.EqualTo(0));
            foreach(var at in new[]{0,fade/2,end/2,end-fade/2})Assert.That(FestivalHudText.BriefingAlpha(at,true),Is.EqualTo(1),"reduced motion: whole at "+at+" s");
        }

        // How to Play: the panel the main menu and the Escape menu open, as titled blocks.
        static string Block(FestivalInput input,string title){foreach(var (t,text) in FestivalHudText.HowToPlay(input))if(t==title)return text;Assert.Fail("How to Play has no "+title+" block");return "";}

        [Test] public void HowToPlayIsSixShortBlocks()
        {
            using(var input=new FestivalInput("hud-briefing-test"))
            {
                var blocks=FestivalHudText.HowToPlay(input);
                Assert.That(Array.ConvertAll(blocks,b=>b.Title),Is.EqualTo(new[]{"THE WEEKEND","THE TRIPPER","EVERYONE ELSE","DANGER","CAMP","KEYS"}));
                foreach(var b in blocks)Assert.That(b.Text,Is.Not.Empty,b.Title);
            }
        }

        // Every action with its key as bound right now, so a rebind shows. The mouse look reads "Mouse": its binding is the mouse's
        // movement, which the Input System calls "Delta".
        [Test] public void HowToPlayListsEveryActionWithItsKeyAsBoundNow()
        {
            using(var input=new FestivalInput("hud-briefing-test"))
            {
                var lines=Block(input,"KEYS").Split('\n');
                Assert.That(lines.Length,Is.EqualTo(input.Map.actions.Count),"one line per action");
                foreach(var action in input.Map.actions)
                {
                    string key=action==input.Look?"Mouse":input.Label(action);
                    var line=Array.Find(lines,l=>l.StartsWith(key+"  "));
                    Assert.That(line,Is.Not.Null,action.name+" reads with its key, "+key);
                    Assert.That(line.Length,Is.GreaterThan(key.Length+2),action.name+" says what it does");
                }
                input.Interact.ApplyBindingOverride(0,"<Keyboard>/r");
                string rebound=input.Label(input.Interact);
                Assert.That(rebound,Is.Not.EqualTo("E"),"setup: the new key reads differently");
                lines=Block(input,"KEYS").Split('\n');
                Assert.That(lines,Has.Some.StartsWith(rebound+"  "),"rebinding Interact changes its line");
                Assert.That(lines,Has.None.StartsWith("E  "),"and the old key is gone");
                Assert.That(Block(input,"DANGER"),Does.Contain("talk ("+rebound+")"),"the other blocks read the rebound key too");
            }
        }

        [Test] public void HowToPlayDescribesWhatShipped()
        {
            using(var input=new FestivalInput("hud-briefing-test"))
            {
                string weekend=Block(input,"THE WEEKEND"),tripper=Block(input,"THE TRIPPER"),crew=Block(input,"EVERYONE ELSE"),danger=Block(input,"DANGER"),camp=Block(input,"CAMP");
                Assert.That(weekend,Does.Contain("Day 1, Night 1, Day 2, Night 2").And.Contain("starts over at Day 1"));
                // VISION-5's own words: the legend of what each shape claims, and what a check says it found.
                foreach(var shape in new[]{"green ball","red cube","blue pill","gold drum"})Assert.That(tripper,Does.Contain(shape),"the legend has the "+shape);
                foreach(var kind in new[]{"Buyer","Narc","Clue","DoubleBuyer","Stash"})Assert.That(tripper,Does.Contain(FestivalVisionMarkers.ClaimText(kind)),"the legend has "+kind);
                Assert.That(tripper,Does.Contain("green ring").And.Contain("red one").And.Contain(FestivalVisionMarkers.VerdictText("Narc",false)),"a check rings it and says what it found");
                Assert.That(crew,Does.Contain("from festivalgoers"),"stock comes from festivalgoers (ECON-2)");
                // DIALOG-3: anyone sizes someone up by talking: the chat key starts a talk, 1-3 reply, and a smooth one leaves a hunch.
                Assert.That(crew,Does.Contain("Talk to them ("+input.Label(input.Chat)+")").And.Contain(input.Label(input.Slot1)+"-"+input.Label(input.Slot3)).And.Contain("hunch"),"talk to people to sus them out before you buy or offer");
                Assert.That(Block(input,"KEYS"),Does.Contain(input.Label(input.Chat)+"  talk to someone").And.Contain(input.Label(input.Slot1)+"  hand 1 • reply 1 in a talk"),"F talks and 1-3 reply");
                Assert.That(danger,Does.Contain("narcs").And.Contain("busted"),"and some of them are narcs");
                Assert.That(danger,Does.Contain("zombie-green"),"CROWD-4's suspicious festivalgoers");
                Assert.That(danger,Does.Contain("SWARMING"),"HUD-5's swarm on the card");
                Assert.That(danger,Does.Contain("dust storm").And.Contain(Festivals.DustStormSightRange+" m"),"PLAYA-3");
                Assert.That(danger,Does.Contain("influencers film").And.Contain("ON CAMERA"),"POLO-6: who films and what the card says while they film you");
                Assert.That(camp,Does.Contain("gear only"),"shelves sell no stock");
                Assert.That(Block(input,"KEYS"),Does.Contain(input.Label(input.View)+"  first or third person"),"CAM-1's view key");
                // The design's one hint about fakes is "Trust, but verify.": How to Play never gives their tells away.
                foreach(var b in FestivalHudText.HowToPlay(input))Assert.That(b.Text.ToLowerInvariant(),Does.Not.Contain("shadow").And.Not.Contain("shimmer"),b.Title);
            }
        }

        // First-time tips: a short card the first time each moment comes, once per profile on this machine (LocalProfile.SeenTips),
        // until "Show tips again". Sam trips Day 1, the briefing is over, and you stand beside them.
        static RoundState Under(){var s=Start(0,0);s.ElapsedSeconds=FestivalHudText.BriefingSeconds;return s;}
        static (string Id,string Text) TipOf(RoundState s,string id,List<string> seen)=>FestivalHudText.Tip(s,s.Players.Find(p=>p.Id==id),seen);
        const string TipProfile="hud-briefing-tips-test";
        static string TipFile=>Path.Combine(LocalProfile.Folder,TipProfile+".json");
        [SetUp][TearDown] public void ForgetTheTipProfile(){if(File.Exists(TipFile))File.Delete(TipFile);}

        [Test] public void EachFirstTimeTipComesAtItsMomentOnce()
        {
            var s=Under();var seen=new List<string>();
            var tip=TipOf(s,"you",seen);
            Assert.That(tip.Text,Is.EqualTo("Only Sam can see the visions.\nStick close and ask what they see."),"a friend tripping");
            seen.Add(tip.Id);
            Assert.That(TipOf(s,"you",seen).Id,Is.Empty,"once seen, never again");
            Assert.That(TipOf(s,"sam",seen).Id,Is.Empty,"the tripper has no tip before a vision");
            s.Visions.Add(new VisionState{Id="v1",Kind="Narc",NpcId="wook_1"});
            tip=TipOf(s,"sam",seen);
            foreach(var shape in new[]{"green ball","red cube","blue pill","gold drum"})Assert.That(tip.Text,Does.Contain(shape),"the first vision names every marker shape");
            Assert.That(tip.Text,Does.Contain("F to chat").And.Contain("E to dance"),"and how to check one");
            Assert.That(TipOf(s,"you",seen).Id,Is.Empty,"a friend's view holds no visions, and gets no vision tip");
            seen.Add(tip.Id);
            s.Visions[0].Confirmed=true;
            tip=TipOf(s,"sam",seen);
            Assert.That(tip.Text,Does.Contain("green ring").And.Contain("red ring").And.Contain(FestivalVisionMarkers.VerdictText("Narc",false)),"the first verdict, in VISION-5's words");
            seen.Add(tip.Id);
            s.Npcs.Add(new NpcState{Id="cop_1",Kind="Cop",Mode="Stop",TargetId="sam"});
            Assert.That(TipOf(s,"you",seen).Id,Is.Empty,"a friend's stop is not yours");
            tip=TipOf(s,"sam",seen);
            Assert.That(tip.Text,Does.StartWith("Security stopped you").And.Contain("Talk to them (E), ditch it (G) or walk away out of sight"),"the first security stop (ESC-2)");
            seen.Add(tip.Id);
            s.Npcs.Add(new NpcState{Id="wook_2",Kind="Wook",Mode="Swarming",TargetId="sam"});
            tip=TipOf(s,"sam",seen);
            Assert.That(tip.Text,Does.Contain("SWARMING"),"the first swarm, as the card says it (HUD-5)");
            seen.Add(tip.Id);
            Assert.That(seen,Is.Unique.And.Count.EqualTo(5),"five tips");
            Assert.That(TipOf(s,"sam",seen).Id,Is.Empty,"all seen");
        }

        [Test] public void TipsWaitForTheBriefingAndComeMostUrgentFirst()
        {
            var s=Under();var none=new List<string>();
            s.Npcs.Add(new NpcState{Id="cop_1",Kind="Cop",Mode="Stop",TargetId="you"});
            Assert.That(TipOf(s,"you",none).Text,Does.StartWith("Security stopped you"),"a stop before the friend tripping");
            s.Npcs.Add(new NpcState{Id="wook_2",Kind="Wook",Mode="Swarming",TargetId="you"});
            Assert.That(TipOf(s,"you",none).Text,Does.Contain("SWARMING"),"a swarm before anything");
            s.ElapsedSeconds=FestivalHudText.BriefingSeconds-.1;
            Assert.That(TipOf(s,"you",none).Id,Is.Empty,"never over the briefing");
            s.ElapsedSeconds=FestivalHudText.BriefingSeconds;s.Players[0].Life="Spirit";
            Assert.That(TipOf(s,"you",none).Id,Is.Empty,"never for a spirit");
            s.Players[0].Life="Alive";s.Phase="Shopping";
            Assert.That(TipOf(s,"you",none).Id,Is.Empty,"never at camp");
            // Every tip is two short lines: the objective card's measured detail box holds lines of about 80 (ECON-3).
            s=Under();s.Visions.Add(new VisionState{Id="v1",Kind="Buyer",NpcId="wook_1",Confirmed=true});
            s.Npcs.Add(new NpcState{Id="cop_1",Kind="Cop",Mode="Stop",TargetId="sam"});s.Npcs.Add(new NpcState{Id="wook_2",Kind="Wook",Mode="Swarming",TargetId="sam"});
            var seen=new List<string>();
            foreach(var id in new[]{"you","sam","sam","sam","sam"})
            {
                var tip=TipOf(s,id,seen);var lines=tip.Text.Split('\n');
                Assert.That(lines.Length,Is.EqualTo(2),tip.Text);
                foreach(var line in lines)Assert.That(line.Length,Is.LessThanOrEqualTo(80),line);
                seen.Add(tip.Id);
            }
            Assert.That(seen,Is.Unique.And.Count.EqualTo(5));
            TestDelegate ask=()=>FestivalHudText.Tip(s,s.Players[1],seen);ask();
            Assert.That(ask,Is.Not.AllocatingGCMemory(),"asked every frame with every tip seen, it makes no garbage");
        }

        [Test] public void ATipSeenOnThisMachineStaysSeenUntilShowTipsAgain()
        {
            var s=Under();var you=s.Players[0];
            var profile=new LocalProfile(TipProfile);
            var tip=FestivalHudText.Tip(s,you,profile.Data.SeenTips);
            Assert.That(tip.Id,Is.Not.Empty,"a new profile gets the tip");
            profile.SeeTip(tip.Id);
            Assert.That(FestivalHudText.Tip(s,you,profile.Data.SeenTips).Id,Is.Empty,"seen");
            Assert.That(FestivalHudText.Tip(s,you,new LocalProfile(TipProfile).Data.SeenTips).Id,Is.Empty,"and still seen after a restart");
            profile.ShowTipsAgain();
            Assert.That(FestivalHudText.Tip(s,you,profile.Data.SeenTips).Id,Is.EqualTo(tip.Id),"Show tips again brings it back");
            Assert.That(FestivalHudText.Tip(s,you,new LocalProfile(TipProfile).Data.SeenTips).Id,Is.EqualTo(tip.Id),"for good");
        }

        [Test] public void AProfileFromBeforeTipsKeepsItsSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TipFile));
            File.WriteAllText(TipFile,"{\"SchemaVersion\":1,\"Cosmetics\":[],\"SeenDialogue\":[],\"ReducedMotion\":false,\"HighContrast\":true,\"TimingCalibrationMs\":0,\"MouseSensitivity\":0.2,\"MusicVolume\":0.5,\"UnlockedFestivalCount\":1,\"ThirdPerson\":true}");
            var old=new LocalProfile(TipProfile);
            Assert.That(old.Data.MusicVolume,Is.EqualTo(.5f),"its settings load");
            Assert.That(old.Data.ThirdPerson,Is.True);
            Assert.That(old.Data.SeenTips,Is.Not.Null.And.Empty,"with no tips seen");
        }
    }
}
