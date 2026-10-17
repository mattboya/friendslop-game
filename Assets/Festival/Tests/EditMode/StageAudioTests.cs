using System;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // The recorded stage follows shared world time and exact sample joins. Dances keep its full gain; booth games/results hold
    // its position and a DJ takeover silences it. The tick follows the authored grid without changing calibrated input time.
    public sealed class StageAudioTests
    {
        private static string Clip(FestivalMusic.Track track)=>track==null?"nothing":track.Clip;

        // At quit Unity can destroy the speakers before the stage audio's OnDisable stops them (native smoke, 2026-10-16).
        [Test]public void StoppingAfterTheSpeakersAreGoneThrowsNothing()
        {
            var speakers=new GameObject("stage speakers under test");
            var transport=new FestivalMusic.Transport(new[]{speakers.AddComponent<AudioSource>(),speakers.AddComponent<AudioSource>()});
            UnityEngine.Object.DestroyImmediate(speakers);
            Assert.DoesNotThrow(transport.Stop);
        }

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
            // Held phases retain the selected recording and exact position, ready to resume without a sample-zero restart.
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
            finally{UnityEngine.Object.DestroyImmediate(speakers);}
        }

        [Test]public void DancesAndTrustDancesKeepTheStageFullAndOnlyADjTakeoverMutesIt()
        {
            var s=new RoundState{Phase="Playing"};var you=new PlayerState{Id="you"};var mate=new PlayerState{Id="mate"};s.Players.Add(you);s.Players.Add(mate);
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),"nothing on: full volume");
            InteractionState Doing(PlayerState p,string kind,string status="Active"){var i=new InteractionState{Id=p.Id+"_"+kind,PlayerId=p.Id,Kind=kind,Status=status};s.Interactions.Clear();s.Interactions.Add(i);p.InteractionId=i.Id;return i;}
            foreach(var kind in new[]{"Dance","Poi","ConfirmDance"})
            {
                Doing(you,kind);
                Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),kind+": the song leads the dance without ducking");
            }
            foreach(var kind in new[]{"Sale","Buy","Conversation","Police"})
            {
                var talk=Doing(you,kind);
                Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),kind+": a talk is not a dance, so the stage plays on");
                talk.TrustDance=true;
                Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),kind+": the song leads the trust dance");
                talk.Turns.Add(new ConversationTurn());
                Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),kind+": once the talking starts, the stage is back");
            }
            Doing(you,"Dance","Complete");
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),"a finished dance no longer ducks");
            Doing(mate,"Dance");you.InteractionId="";
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),"a friend's dance is theirs: your stage plays on");
            Assert.That(FestivalStageAudio.Level(s,"mate"),Is.EqualTo(1),"and stays full for them");

            // The DJ takeover: whoever is on the stage-front deck, their set is the stage's sound, for everyone.
            Doing(mate,"Dj");mate.VisualPose="Dj";
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(0),"a friend's takeover mutes the stage track");
            Assert.That(FestivalStageAudio.Level(s,"mate"),Is.EqualTo(0),"and the DJ's own");
            mate.VisualPose="Idle";s.Interactions.Clear();mate.InteractionId="";
            Assert.That(FestivalStageAudio.Level(s,"you"),Is.EqualTo(1),"the set over, the stage track is back");
        }

        static AudioSource[] Decks(FestivalStageAudio stage)=>(AudioSource[])typeof(FestivalStageAudio).GetField("decks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage);
        static FestivalStageAudio Stage(GameObject host)
        {
            var stage=host.AddComponent<FestivalStageAudio>();
            if(Decks(stage)==null)typeof(FestivalStageAudio).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stage,null);
            return stage;
        }
        static void RemoveStage(GameObject host)
        {
            var stage=host.GetComponent<FestivalStageAudio>();if(stage!=null&&Decks(stage)!=null)UnityEngine.Object.DestroyImmediate(Decks(stage)[0].gameObject);
            UnityEngine.Object.DestroyImmediate(host);
        }
        static RoundState Clock(double world,string phase="Playing")=>new RoundState{Phase=phase,RoundId="same-song",SimulationSeconds=world,FestivalIndex=0,LevelIndex=1};
        static int Expected(FestivalMusic.Track track,double world)=>(int)Math.Floor(RhythmChart.StageSampleAtWorld(track.Beats,world));
        [Test]public void TwoViewsScheduleTheProjectedSharedWorldSampleIncludingLoopJoins()
        {
            var a=new GameObject("First shared stage");var b=new GameObject("Second shared stage");
            try
            {
                var one=Stage(a);var two=Stage(b);var track=FestivalStageAudio.TrackFor(0,1);
                foreach(double world in new[]{87.123,track.LoopSeconds-.1,track.LoopSeconds*3+37.45})
                {
                    var state=Clock(world);state.RoundId=world.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    one.Follow(state,world,AudioSettings.dspTime);two.Follow(state,world,AudioSettings.dspTime);
                    Assert.That(one.Music.timeSamples,Is.EqualTo(Expected(track,world+.2)).Within(1),"the scheduled deck projects world time through the 200 ms stream lead");
                    Assert.That(two.Music.timeSamples,Is.EqualTo(one.Music.timeSamples),"two clients cue the same sample rather than sample zero");
                }
            }
            finally{RemoveStage(a);RemoveStage(b);}
        }
        [Test]public void ASameTrackNewRoundResetsAndHeldReconnectKeepsTheExactWorldPosition()
        {
            var host=new GameObject("Stage epoch hold");
            try
            {
                var stage=Stage(host);var state=Clock(65.25);var track=FestivalStageAudio.For(state);
                stage.Follow(state,state.SimulationSeconds,AudioSettings.dspTime);
                state.RoundId="next-round";state.SimulationSeconds=3.75;stage.Follow(state,state.SimulationSeconds,AudioSettings.dspTime);
                Assert.That(stage.Music.timeSamples,Is.EqualTo(Expected(track,3.95)).Within(1),"the same recording in a new round resets the transport");
                state.Phase=FestivalSimulation.MinigamePhase;state.SimulationSeconds=12.5;state.Booth.Seconds=4.25;
                stage.Follow(state,999,AudioSettings.dspTime);
                Assert.That(stage.Music.timeSamples,Is.EqualTo(Expected(track,8.25)).Within(1),"minigame position uses held WorldSeconds, never extrapolated game time");
                state.SimulationSeconds+=5;state.Booth.Seconds+=5;stage.Follow(state,999,AudioSettings.dspTime);
                Assert.That(stage.Music.timeSamples,Is.EqualTo(Expected(track,8.25)).Within(1),"a held stage never restarts or advances");
                state.Phase="Results";stage.Follow(state,999,AudioSettings.dspTime);
                Assert.That(stage.Music.timeSamples,Is.EqualTo(Expected(track,8.25)).Within(1),"results holds the same sample");
                state.RoundId="reconnect";stage.Follow(state,999,AudioSettings.dspTime);
                Assert.That(stage.Music.timeSamples,Is.EqualTo(Expected(track,8.25)).Within(1),"a paused reconnect cues its held position");
            }
            finally{RemoveStage(host);}
        }
        [Test]public void TheEffectsTickUsesAuthoredWholeBeatsThroughDriftAndWrap()
        {
            foreach(int choice in new[]{1,2,4,5})
            {
                var grid=StageBeats.For(choice);var interaction=new InteractionState{Kind="Dance",ChartSeed=12,NoteCount=16,BeatSeconds=.5};
                RhythmChart.InitializeStage(interaction,choice,grid.LoopSeconds-4.1);
                double origin=interaction.StageFirstHeadWorldSeconds-interaction.StageFirstHeadDelaySeconds;
                foreach(double after in new[]{0.0,interaction.StageFirstHeadDelaySeconds+.013,9.731,grid.LoopSeconds+19.3})
                {
                    double next=FestivalRhythmAudio.NextTick(interaction,after),world=origin+next;
                    Assert.That(next,Is.GreaterThanOrEqualTo(Math.Max(after,interaction.StageFirstHeadDelaySeconds)-1e-8),"no early tick before the first head or requested time");
                    double beat=grid.Beat(RhythmChart.StageSampleAtWorld(grid,world));
                    Assert.That(Math.Abs(beat-Math.Round(beat)),Is.LessThan(1e-6),grid.Clip+": tick lies on a measured whole beat, including the real loop join");
                }
            }
        }
        [Test]public void TheStageTickIsBriefAndQuieterWhileTheDjKeepsItsOwnPulse()
        {
            var host=new GameObject("Authored tick");
            try
            {
                var audio=host.AddComponent<FestivalRhythmAudio>();
                typeof(FestivalRhythmAudio).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(audio,null);
                var tick=(AudioClip)typeof(FestivalRhythmAudio).GetField("clip",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
                Assert.That(tick.length,Is.LessThanOrEqualTo(.04f),"a light tick instead of the former half-second bass pulse");
                var samples=new float[tick.samples];Assert.That(tick.GetData(samples,0),Is.True);
                float peak=0;foreach(var sample in samples)peak=Math.Max(peak,Math.Abs(sample));Assert.That(peak,Is.LessThanOrEqualTo(.14f));
                Assert.That(samples[0],Is.Zero);Assert.That(Math.Abs(samples[samples.Length-1]),Is.LessThan(1e-5f),"smooth ends avoid a tick-edge click");
                var dj=new InteractionState{Kind="Dj",BeatSeconds=.4};
                Assert.That(FestivalRhythmAudio.NextTick(dj,2.01),Is.EqualTo(2.4).Within(1e-9),"DJ own fixed-tempo set remains unchanged");
            }
            finally
            {
                var audio=host.GetComponent<FestivalRhythmAudio>();var clip=(AudioClip)typeof(FestivalRhythmAudio).GetField("clip",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
                if(clip!=null)UnityEngine.Object.DestroyImmediate(clip);
                var pulse=(AudioClip)typeof(FestivalRhythmAudio).GetField("pulse",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);if(pulse!=null)UnityEngine.Object.DestroyImmediate(pulse);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]public void TheDjTakeoverFadesOutAndComesBackOverASecond()
        {
            const float frame=1/60f;
            float level=1,t=0;
            while(level>1e-4f&&t<2){level=FestivalStageAudio.Ease(level,0,frame);t+=frame;}
            Assert.That(t,Is.LessThanOrEqualTo(.4f),"DJ silence within 0.4 s");
            Assert.That(t,Is.GreaterThan(.2f),"a quick fade, not a cut");
            Assert.That(FestivalStageAudio.Ease(level,0,frame),Is.EqualTo(0).Within(1e-4f),"and it stays silent during the takeover");
            t=0;
            while(level<1-1e-4f&&t<3){level=FestivalStageAudio.Ease(level,1,frame);t+=frame;}
            Assert.That(t,Is.InRange(.9f,1.1f),"back to full over about a second");
            Assert.That(FestivalStageAudio.Ease(0,0,frame),Is.EqualTo(0),"a muted stage stays muted");
            Assert.That(FestivalStageAudio.Ease(1,1,frame),Is.EqualTo(1),"a full stage stays full");
        }
    }
}
