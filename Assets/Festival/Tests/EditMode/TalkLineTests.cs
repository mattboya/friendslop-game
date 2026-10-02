using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Tests
{
    // DIALOG-6: a festivalgoer's one-line chat (Talk) or a guard's bark stays on the HUD's dialogue card long enough to read, as the
    // card follows it from the player's view frame by frame (FestivalHud.UpdateRhythm). Matt, 2026-10-01: "when talking to people,
    // sometimes their text disappears immediately."
    public sealed class TalkLineTests
    {
        const float Frame=1/60f;
        // 60 letters: read in 1.5 s plus .05 s a letter, 4.5 s.
        const string Hello="You made it out here! The sunset over those dunes is unreal.",Psst="Psst. Security's walking this way.";
        static void Says(PlayerState p,string line,double until,string speaker="FESTIVALGOER"){p.NpcSpeaker=speaker;p.NpcSpeech=line;p.NpcSpeechUntil=until;}
        // Plays `seconds` of 60 fps frames from simulation time `now`, moving it on, and returns for how long the card showed `line`.
        static float Play(FestivalTalkLine card,PlayerState p,ref double now,float seconds,string line,bool challenge=false,bool hidden=false)
        {
            float shown=0;
            for(int f=Mathf.RoundToInt(seconds/Frame);f>0;f--){if(card.Shows(p,now,challenge,hidden,Frame)&&card.Line==line)shown+=Frame;now+=Frame;}
            return shown;
        }

        [Test] public void ALineReadsInASecondAndAHalfPlusAFewLettersASecond()
        {
            Assert.That(Hello.Length,Is.EqualTo(60),"setup: a 60-letter line");
            Assert.That(FestivalChatWindow.ReadingSeconds(Hello),Is.EqualTo(4.5f).Within(1e-4),"1.5 s and .05 s a letter");
            Assert.That(FestivalTalkLine.ReadFor(Hello),Is.EqualTo(4.5f).Within(1e-4),"a one-line chat shows that long");
            Assert.That(FestivalTalkLine.ReadFor("Hi!"),Is.EqualTo(3),"at least 3 s");
            Assert.That(FestivalTalkLine.ReadFor(new string('a',200)),Is.EqualTo(7),"at most the 7 s the host keeps a line up");
        }

        // With nothing else on, a line stays as long as the host keeps it (Talk: 7 s), as before.
        [Test] public void WithNothingElseOnALineStaysAsLongAsTheHostSaysIt()
        {
            var card=new FestivalTalkLine();var p=new PlayerState();double now=0;Says(p,Hello,7);
            Assert.That(Play(card,p,ref now,7,Hello),Is.EqualTo(7).Within(2*Frame),"up for the host's 7 s");
            Assert.That(card.Speaker,Is.EqualTo("FESTIVALGOER"));
            Assert.That(Play(card,p,ref now,3,Hello),Is.Zero,"then gone");
        }

        // A dance or sale started right after the line: the line takes the challenge card's dialogue until read, then the
        // challenge's own line does. A bark that comes mid-dance gets the same.
        [Test] public void ADanceStartedAtOnceLeavesTheLineUpUntilRead()
        {
            var card=new FestivalTalkLine();var p=new PlayerState();double now=0;Says(p,Hello,7);
            Assert.That(Play(card,p,ref now,Frame,Hello),Is.EqualTo(Frame),"setup: the line is up");
            Assert.That(Play(card,p,ref now,2,Hello,challenge:true),Is.EqualTo(2).Within(Frame/2),"2 s into the dance it is still up");
            Assert.That(Play(card,p,ref now,8,Hello,challenge:true),Is.EqualTo(4.5f-2-Frame).Within(1.5f*Frame),"until it has been up its 4.5 s reading time");
            Says(p,Psst,now+7,"SECURITY");
            Assert.That(Play(card,p,ref now,8,Psst,challenge:true),Is.EqualTo(FestivalTalkLine.ReadFor(Psst)).Within(1.5f*Frame),"a guard's bark mid-dance shows until read too");
            Assert.That(card.Speaker,Is.EqualTo("SECURITY"));
        }

        // The menu (or the chat window) hiding the card doesn't use the line up: closed again, it is there to read, even after
        // the host has dropped it.
        [Test] public void TheMenuDoesNotLoseALine()
        {
            var card=new FestivalTalkLine();var p=new PlayerState();double now=0;Says(p,Hello,7);
            Assert.That(Play(card,p,ref now,10,Hello,hidden:true),Is.Zero,"the menu is open as they say it, and stays open past the host's 7 s");
            Assert.That(Play(card,p,ref now,10,Hello),Is.EqualTo(4.5f).Within(1.5f*Frame),"closed, the line shows for its reading time");
        }

        // Talking to someone else replaces the line, but never in the old line's first 1.5 s: the new one waits that long.
        [Test] public void ANewLineWaitsUntilTheShownOneHasBeenUpASecondAndAHalf()
        {
            var card=new FestivalTalkLine();var p=new PlayerState();double now=0;Says(p,Hello,7);
            Assert.That(Play(card,p,ref now,.5f,Hello),Is.EqualTo(.5f).Within(Frame/2),"setup: the first line is up");
            Says(p,Psst,now+7,"SECURITY");
            Assert.That(Play(card,p,ref now,.95f,Hello),Is.EqualTo(.95f).Within(Frame/2),"the first line keeps the card");
            Assert.That(Play(card,p,ref now,.1f,Psst),Is.GreaterThan(0),"for its first 1.5 s, then the new one takes it");
            Assert.That(card.Speaker+": "+card.Line,Is.EqualTo("SECURITY: "+Psst));
            Assert.That(Play(card,p,ref now,2,Psst),Is.EqualTo(2).Within(Frame/2),"setup: the new line has been up a while");
            Says(p,Hello,now+7);
            Assert.That(Play(card,p,ref now,Frame,Hello),Is.EqualTo(Frame),"and the next one replaces it at once");
        }

        // A line the host has already dropped (a client joining late, a new level) never comes up.
        [Test] public void ALineTheHostHasDroppedNeverShows()
        {
            var card=new FestivalTalkLine();var p=new PlayerState();double now=10;Says(p,Hello,5);
            Assert.That(Play(card,p,ref now,5,Hello),Is.Zero);
        }
    }
}
