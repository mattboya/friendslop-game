using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // MUSIC-1: the main stage plays MUSIC-2's recordings out over the festival. Each festival has its own day and night track (MUSIC-2's
    // decision A), from the shared level, so every client hears the same one. It plays only while the festival is on screen, carries
    // from the stage front to the camp gate, and steps aside while you dance (decision A: separate beats) or a DJ takes the stage.
    public sealed class StageAudioTests
    {
        private static string Clip(FestivalMusic.Track track)=>track==null?"nothing":track.Clip;

        [Test]public void EachFestivalsStagePlaysItsOwnDayAndNightTrack()
        {
            string[] palmMirage={"disco-house-loop","tech-house-loop","disco-house-loop","tech-house-loop"};
            string[] emberPlaya={"dub-reggae-loop","drum-and-bass-loop","dub-reggae-loop","drum-and-bass-loop"};
            for(int level=0;level<Festivals.LevelCount;level++)
            {
                Assert.That(Clip(FestivalStageAudio.TrackFor(0,level)),Is.EqualTo(palmMirage[level]),"Palm Mirage, level "+level);
                Assert.That(Clip(FestivalStageAudio.TrackFor(1,level)),Is.EqualTo(emberPlaya[level]),"Ember Playa, level "+level);
                Assert.That(FestivalStageAudio.TrackFor(0,level),Is.Not.SameAs(FestivalStageAudio.TrackFor(1,level)),"the two festivals sound different, level "+level);
            }
            Assert.That(Festivals.Count,Is.EqualTo(2),"setup: every festival has its stage tracks");
        }

        [Test]public void TheStagePlaysWhileTheFestivalIsOnScreenAndStopsAtCamp()
        {
            // MINI-1's booth game pauses the level, not the festival: the stage plays on, rather than start its track again after.
            foreach(var phase in new[]{"Playing",FestivalSimulation.MinigamePhase,"Results"})
            {
                var night=new RoundState{Phase=phase,FestivalIndex=1,LevelIndex=3};
                Assert.That(Clip(FestivalStageAudio.For(night)),Is.EqualTo("drum-and-bass-loop"),phase+": Ember Playa's Night 2 track, from the shared level");
            }
            foreach(var phase in new[]{"Lobby","Shopping","Spinning","Loading","CampReview"})
                Assert.That(FestivalStageAudio.For(new RoundState{Phase=phase}),Is.Null,phase+": the stage is silent, and the camp DJ has the music");
            Assert.That(FestivalStageAudio.For(null),Is.Null,"before a session there is no stage");
        }

        [Test]public void TheSpeakersStandAtTheStageFrontAndCarryToTheCampGate()
        {
            var speakers=FestivalStageAudio.Speakers();
            try
            {
                var at=speakers.transform.position;
                // FestivalWorld's stage: 18 m wide at x 0, from its front edge at z 27.5 back to z 36.5.
                Assert.That(Mathf.Abs(at.x),Is.LessThanOrEqualTo(9),"across the stage");
                Assert.That(at.z,Is.InRange(27.5f,36.5f),"on the stage");
                var decks=speakers.GetComponents<AudioSource>();
                Assert.That(decks.Length,Is.EqualTo(2),"two decks take turns, so the loop joins on the audio clock");
                var gate=new Vector3(Festivals.CampGateX,1.65f,Festivals.CampGateZ);
                foreach(var deck in decks)
                {
                    Assert.That(deck.spatialBlend,Is.EqualTo(1),"heard from the stage, in 3-D");
                    Assert.That(deck.playOnAwake,Is.False,"silent until the festival opens");
                    Assert.That(deck.dopplerLevel,Is.EqualTo(0),"no pitch wobble riding an art car or swinging the camera");
                    Assert.That(deck.rolloffMode,Is.EqualTo(AudioRolloffMode.Logarithmic),"logarithmic rolloff");
                    Assert.That(Vector3.Distance(at,new Vector3(0,at.y,27.5f)+Vector3.back*12),Is.LessThanOrEqualTo(deck.minDistance+.01f),"full volume 12 m out from the stage front");
                    // Unity's logarithmic rolloff is the inverse law: minDistance/distance past minDistance, out to maxDistance.
                    float distance=Vector3.Distance(at,gate);
                    Assert.That(deck.maxDistance,Is.GreaterThan(distance),"still falling off at the gate");
                    Assert.That(deck.minDistance/distance,Is.InRange(.10f,.25f),"clearly there at the camp gate, "+distance+" m away");
                }
            }
            finally{Object.DestroyImmediate(speakers);}
        }

        [Test]public void ARealDanceDucksTheStageATalkDoesNotAndADjTakeoverMutesIt()
        {
            var s=new RoundState{Phase="Playing"};var you=new PlayerState{Id="you"};var mate=new PlayerState{Id="mate"};s.Players.Add(you);s.Players.Add(mate);
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),"nothing on: full volume");
            InteractionState Doing(PlayerState p,string kind,string status="Active"){var i=new InteractionState{Id=p.Id+"_"+kind,PlayerId=p.Id,Kind=kind,Status=status};s.Interactions.Clear();s.Interactions.Add(i);p.InteractionId=i.Id;return i;}
            foreach(var kind in new[]{"Dance","Poi","ConfirmDance"})
            {
                Doing(you,kind);
                Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(.25f).Within(.01f),kind+": the stage ducks to a quarter, so the dance's own beat leads");
            }
            foreach(var kind in new[]{"Sale","Buy","Conversation","Police"})
            {
                var talk=Doing(you,kind);
                Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),kind+": a talk is not a dance, so the stage plays on");
                talk.TrustDance=true;
                Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(.25f).Within(.01f),kind+": the trust dance before it is a dance on the lanes");
                talk.Turns.Add(new ConversationTurn());
                Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),kind+": once the talking starts, the stage is back");
            }
            Doing(you,"Dance","Complete");
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),"a finished dance no longer ducks");
            Doing(mate,"Dance");you.InteractionId="";
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),"a friend's dance is theirs: your stage plays on");
            Assert.That(FestivalStageAudio.Level(s,"mate"),Is.EqualTo(.25f).Within(.01f),"and ducks for them");

            // The DJ takeover: whoever is on the stage-front deck, their set is the stage's sound, for everyone.
            Doing(mate,"Dj");mate.VisualPose="Dj";
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(0),"a friend's takeover mutes the stage track");
            Assert.That(FestivalStageAudio.Level(s,"mate"),Is.EqualTo(0),"and the DJ's own");
            mate.VisualPose="Idle";s.Interactions.Clear();mate.InteractionId="";
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),"the set over, the stage track is back");
        }

        [Test]public void TheStageDucksWithinAFewTenthsOfASecondAndComesBackOverASecond()
        {
            const float frame=1/60f;
            float level=1,t=0;
            while(level>.25f+1e-4f&&t<2){level=FestivalStageAudio.Ease(level,.25f,frame);t+=frame;}
            Assert.That(t,Is.LessThanOrEqualTo(.4f),"down to a quarter within 0.4 s");
            Assert.That(t,Is.GreaterThan(.2f),"a quick fade, not a cut");
            Assert.That(FestivalStageAudio.Ease(level,.25f,frame),Is.EqualTo(.25f).Within(1e-4f),"and it stays there while the dance runs");
            t=0;
            while(level<1-1e-4f&&t<3){level=FestivalStageAudio.Ease(level,1,frame);t+=frame;}
            Assert.That(t,Is.InRange(.9f,1.1f),"back to full over about a second");
            Assert.That(FestivalStageAudio.Ease(0,0,frame),Is.EqualTo(0),"a muted stage stays muted");
            Assert.That(FestivalStageAudio.Ease(1,1,frame),Is.EqualTo(1),"a full stage stays full");
        }
    }
}
