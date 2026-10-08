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
        static float Play(FestivalTalkLine card,PlayerState p,ref double now,float seconds,string line,bool challenge=false,bool hidden=false,string round="game")
        {
            float shown=0;
            for(int f=Mathf.RoundToInt(seconds/Frame);f>0;f--){if(card.Shows(round,p,now,challenge,hidden,Frame)&&card.Line==line)shown+=Frame;now+=Frame;}
            return shown;
        }

        [Test]public void ANewRoundRetiresCoveredChallengeReadStateEvenWhenIdsRepeat()
        {
            var state=new RoundState{RoundId="old-card-round",Phase="Playing"};
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="old-card-round:speech:1",Sequence=1,SpeakerId="you",Speaker="YOU",Text="Old challenge",ExpiresAtWorldSeconds=1});
            var tracker=new FestivalSpeechDeliveryTracker();tracker.Follow(state,0,0);
            var player=new PlayerState{Id="you",SpeechDeliveryId="old-card-round:speech:1"};
            var challenge=new InteractionState{Id="reused-interaction",PlayerId="you",TargetId="npc",Kind="Dance",DialogueText="Old challenge"};
            var card=new FestivalTalkLine();Assert.That(card.Follow(tracker,player,challenge,false,.2f),Is.SameAs(tracker.Find(player.SpeechDeliveryId)));
            tracker.Follow(state,0,2);Assert.That(card.Follow(tracker,player,challenge,true,1),Is.Null);
            state.RoundId="new-card-round";state.PublicSpeech.Clear();state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="new-card-round:speech:1",Sequence=1,SpeakerId="you",Speaker="YOU",Text="New challenge",ExpiresAtWorldSeconds=7});
            player.SpeechDeliveryId="new-card-round:speech:1";challenge.DialogueText="New challenge";tracker.Follow(state,0,.1);
            Assert.That(card.Follow(tracker,player,challenge,false,.1f),Is.SameAs(tracker.Find(player.SpeechDeliveryId)));
            Assert.That(card.IsChallenge,Is.True,"the new round's literal is the current challenge, never a bark waiting behind a removed old reference");
        }

        [Test]public void SharedChallengeReadStateDistinguishesEqualLiteralDeliveries()
        {
            var state=new RoundState{RoundId="shared-card",Phase="Playing"};
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="shared-card:speech:1",Sequence=1,SpeakerId="npc",Speaker="FESTIVALGOER",Text="Same words",ExpiresAtWorldSeconds=7});
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="shared-card:speech:2",Sequence=2,SpeakerId="you",Speaker="YOU",Text="Same words",ExpiresAtWorldSeconds=60});
            var tracker=new FestivalSpeechDeliveryTracker();tracker.Follow(state,0,.5);
            var player=new PlayerState{Id="you",SpeechDeliveryId="shared-card:speech:2"};
            var challenge=new InteractionState{Id="dance",PlayerId="you",TargetId="npc",Kind="Dance",DialogueText="Same words"};
            var card=new FestivalTalkLine();
            Assert.That(card.Follow(tracker,player,challenge,false,1),Is.SameAs(tracker.Find("shared-card:speech:1")));
            Assert.That(card.IsChallenge,Is.False,"equal literals never acknowledge a different challenge delivery");
            card.Follow(tracker,player,challenge,false,2.1f);
            Assert.That(card.Follow(tracker,player,challenge,false,0),Is.SameAs(tracker.Find("shared-card:speech:2")));
            Assert.That(card.IsChallenge,Is.True);
        }
        [Test]public void ANewAcceptedBarkCanBeReadDuringTheSameChallenge()
        {
            var state=new RoundState{RoundId="shared-bark",Phase="Playing"};
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="shared-bark:speech:1",Sequence=1,SpeakerId="you",Speaker="YOU",Text="Challenge",ExpiresAtWorldSeconds=60});
            var tracker=new FestivalSpeechDeliveryTracker();tracker.Follow(state,0,1);
            var player=new PlayerState{Id="you",SpeechDeliveryId="shared-bark:speech:1"};
            var challenge=new InteractionState{Id="dance",PlayerId="you",TargetId="npc",Kind="Dance",DialogueText="Challenge"};
            var card=new FestivalTalkLine();card.Follow(tracker,player,challenge,false,2);
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="shared-bark:speech:2",Sequence=2,SpeakerId="cop",Speaker="SECURITY",Text="Hey!",StartedAtWorldSeconds=1,ExpiresAtWorldSeconds=6});
            player.SpeechDeliveryId="shared-bark:speech:2";tracker.Follow(state,0,1.5);
            Assert.That(card.Follow(tracker,player,challenge,false,0),Is.SameAs(tracker.Find(player.SpeechDeliveryId)),"a new accepted stop bark can interrupt a read challenge without resetting either delivery");
            Assert.That(card.IsChallenge,Is.False);
            state.PublicSpeech[1].Retracted=true;tracker.Follow(state,0,1.5);
            Assert.That(card.Follow(tracker,player,challenge,false,0),Is.SameAs(tracker.Find("shared-bark:speech:1")),"a retracted bark yields to the same original challenge identity");
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

        // K2: a line the host takes back (it empties NpcSpeech: a security stop over, SecurityStops.Hush) goes, even one held unread
        // behind the chat window, so "Last chance" never shows once the guard has waved you on. Only the newest line can be taken
        // back: one waiting behind an older line goes, and the older line stays to be read.
        [Test] public void ALineTheHostTakesBackGoes()
        {
            var card=new FestivalTalkLine();var p=new PlayerState();double now=0;Says(p,Psst,5,"SECURITY");
            Assert.That(Play(card,p,ref now,.5f,Psst),Is.EqualTo(.5f).Within(Frame/2),"setup: the guard's line is up");
            Assert.That(Play(card,p,ref now,8,Psst,hidden:true),Is.Zero,"setup: the chat window covers it past the host's 5 s");
            p.NpcSpeech="";
            Assert.That(Play(card,p,ref now,5,Psst),Is.Zero,"taken back, it never comes up again");

            card=new FestivalTalkLine();p=new PlayerState();now=0;Says(p,Hello,7);
            Assert.That(Play(card,p,ref now,.5f,Hello),Is.EqualTo(.5f).Within(Frame/2),"setup: a festivalgoer's line is up");
            Says(p,Psst,now+5,"SECURITY");
            Assert.That(Play(card,p,ref now,.5f,Hello),Is.EqualTo(.5f).Within(Frame/2),"setup: the guard's line waits behind it");
            p.NpcSpeech="";
            Assert.That(Play(card,p,ref now,8,Psst),Is.Zero,"taken back while waiting, it never comes up");
            Assert.That(card.Line,Is.EqualTo(Hello),"and the festivalgoer's line was left to be read");
        }

        // A line the host has already dropped (a client joining late, a new level) never comes up.
        [Test] public void ALineTheHostHasDroppedNeverShows()
        {
            var card=new FestivalTalkLine();var p=new PlayerState();double now=10;Says(p,Hello,5);
            Assert.That(Play(card,p,ref now,5,Hello),Is.Zero);
        }

        // The HUD outlives a game (Leave, then Host or Join again), but its line doesn't: the next game's clock starts again near 0,
        // where the last game's line would still read as said, for as long as the last game had run.
        [Test] public void ALineFromAnEarlierGameNeverShowsInTheNext()
        {
            var card=new FestivalTalkLine();var p=new PlayerState();double now=600;Says(p,Hello,607);
            Assert.That(Play(card,p,ref now,8,Hello),Is.EqualTo(7).Within(2*Frame),"setup: ten minutes into a game, the line is up for the host's 7 s");
            now=5;
            Assert.That(Play(card,new PlayerState(),ref now,3,Hello,round:"next game"),Is.Zero,"5 s into the next game, the last game's line is gone");
            Assert.That(card.Line,Is.Empty);
        }
    }
}
