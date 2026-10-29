using System;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // The recorded stages follow shared world time and exact sample joins, and cross-fade by the listener's place. Dances and
    // the DJ set never duck them; booth games/results hold their position. The tick follows the authored grid without
    // changing calibrated input time. After each phrase of a set the crowd roars with its score.
    public sealed class StageAudioTests
    {
        // At quit Unity can destroy the speakers before the stage audio's OnDisable stops them (native smoke, 2026-10-16).
        [Test]public void StoppingAfterTheSpeakersAreGoneThrowsNothing()
        {
            var speakers=new GameObject("stage speakers under test");
            var transport=new FestivalMusic.Transport(new[]{speakers.AddComponent<AudioSource>(),speakers.AddComponent<AudioSource>()});
            UnityEngine.Object.DestroyImmediate(speakers);
            Assert.DoesNotThrow(transport.Stop);
        }

        // A spirit's view is filtered too, and still carries the spin seed the pick reads.
        [Test]public void EveryViewPlaysTheLevelsShuffledPick()
        {
            var game=new FestivalSimulation(3);foreach(var id in new[]{"host","friend","ghost"})game.AddPlayer(id,id);
            game.State.Phase="Playing";game.Player("ghost").Life="Spirit";
            var clips=new System.Collections.Generic.HashSet<string>();
            for(int spin=0;spin<60;spin++)
            {
                game.State.SpinSeed=spin*7919+1;var main=FestivalStageAudio.For(game.State);
                Assert.That(main,Is.SameAs(FestivalMusic.For(StageMusic.Track(game.State,StageMusic.Main))),"spin "+spin+": the main stage plays the main pick");
                Assert.That(StageMusic.Track(game.State,StageMusic.Main),Is.Not.EqualTo(StageMusic.Track(game.State,StageMusic.Small)),"spin "+spin+": the two stages differ");
                foreach(var viewer in new[]{"host","friend","ghost"})
                {
                    var view=JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(Festival.Network.FestivalSession.ViewFor(game,viewer)));
                    Assert.That(FestivalStageAudio.For(view),Is.SameAs(main),"spin "+spin+": "+viewer+"'s view plays the same main stage song");
                    Assert.That(StageMusic.Track(view,StageMusic.Small),Is.EqualTo(StageMusic.Track(game.State,StageMusic.Small)),"spin "+spin+": "+viewer+"'s view plays the same small stage song");
                }
                clips.Add(main.Clip);
            }
            Assert.That(clips.Count,Is.EqualTo(FestivalMusic.Tracks.Length),"every loop comes up on the main stage: "+string.Join(", ",clips));
        }

        [Test]public void TheStagePlaysWhileTheFestivalIsOnScreenAndStopsAtCamp()
        {
            // Held phases retain the selected recording and exact position, ready to resume without a sample-zero restart.
            foreach(var phase in new[]{"Playing",FestivalSimulation.MinigamePhase,"Results"})
            {
                var night=new RoundState{Phase=phase,FestivalIndex=1,LevelIndex=3,SpinSeed=4242};
                Assert.That(FestivalStageAudio.For(night),Is.Not.Null.And.SameAs(FestivalMusic.For(StageMusic.Track(4242,StageMusic.Main))),phase+": the level's main stage pick, from the shared spin seed");
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

        // Nothing a player does sets the stage's gain. StageAudioPlayTests hears it at full volume through a whole set.
        [Test]public void NoTakeoverDanceOrTalkMutesTheStageSong()
        {
            Assert.That(typeof(FestivalStageAudio).GetMethod("Level"),Is.Null,"no takeover, dance or talk mutes or ducks the stage song");
            Assert.That(typeof(FestivalStageAudio).GetMethod("Ease"),Is.Null,"so there is no fade to fade");
        }

        [Test]public void TheCrowdRoarsWithTheLastPhrasesScoreWhileItsCheerLasts()
        {
            var s=new RoundState{Phase="Playing",SimulationSeconds=50};
            Assert.That(FestivalStageAudio.RoarLevel(s),Is.Zero,"no set yet, no roar");
            Assert.That(FestivalStageAudio.RoarLevel(null),Is.Zero,"no state, no roar");
            s.StageCheerUntilWorldSeconds=56;
            float last=-1;
            foreach(double score in new[]{0,.3,.4,.6,.75,1})
            {
                s.StageCheerScore=score;float roar=FestivalStageAudio.RoarLevel(s);
                Assert.That(roar,Is.GreaterThanOrEqualTo(last).And.InRange(0f,1f),"the roar swells with the score ("+score+")");last=roar;
            }
            s.StageCheerScore=.2;Assert.That(FestivalStageAudio.RoarLevel(s),Is.Zero,"a flop gets no roar");
            s.StageCheerScore=.4;Assert.That(FestivalStageAudio.RoarLevel(s),Is.InRange(.1f,.35f),"a middling phrase gets a murmur");
            s.StageCheerScore=1;Assert.That(FestivalStageAudio.RoarLevel(s),Is.EqualTo(1),"a perfect phrase gets the full roar");
            s.Phase=FestivalSimulation.MinigamePhase;Assert.That(FestivalStageAudio.RoarLevel(s),Is.Zero,"a booth game holds the song, so the crowd waits too");
            s.Phase="Playing";
            s.SimulationSeconds=56;Assert.That(FestivalStageAudio.RoarLevel(s),Is.Zero,"and the roar ends with the cheer");
            const float frame=1/60f;float level=0,t=0;
            while(level<1-1e-4f&&t<3){level=FestivalStageAudio.EaseRoar(level,1,frame);t+=frame;}
            Assert.That(t,Is.InRange(.3f,.6f),"the roar swells up in about half a second");
            t=0;while(level>1e-4f&&t<4){level=FestivalStageAudio.EaseRoar(level,0,frame);t+=frame;}
            Assert.That(t,Is.InRange(1.2f,1.8f),"and dies away over about a second and a half");
        }

        [Test]public void TheRoarIsAProceduralLoopThatJoinsWithoutAClick()
        {
            var roar=FestivalStageAudio.CreateRoar();
            try
            {
                Assert.That(roar.length,Is.EqualTo(3).Within(.01f),"a three second loop");
                var samples=new float[roar.samples];Assert.That(roar.GetData(samples,0),Is.True);
                float peak=0,step=0;for(int n=0;n<samples.Length;n++){peak=Math.Max(peak,Math.Abs(samples[n]));if(n>0)step=Math.Max(step,Math.Abs(samples[n]-samples[n-1]));}
                Assert.That(peak,Is.InRange(.3f,.85f),"loud enough to hear, never clipping");
                Assert.That(Math.Abs(samples[0]-samples[samples.Length-1]),Is.LessThanOrEqualTo(step+1e-4f),"the join is no bigger a jump than any step inside the loop");
            }
            finally{UnityEngine.Object.DestroyImmediate(roar);}
        }

        static AudioSource[] Decks(FestivalStageAudio stage)=>(AudioSource[])typeof(FestivalStageAudio).GetField("decks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage);
        static FestivalStageAudio Stage(GameObject host)
        {
            var stage=host.AddComponent<FestivalStageAudio>();
            if(Decks(stage)==null)typeof(FestivalStageAudio).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stage,null);
            return stage;
        }
        static AudioSource[] SmallDecks(FestivalStageAudio stage)=>(AudioSource[])typeof(FestivalStageAudio).GetField("smallDecks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage);
        // EditMode skips OnDestroy, so every test that wakes a FestivalStageAudio cleans up its speakers and crowd roar here.
        internal static void RemoveStage(GameObject host)
        {
            var stage=host.GetComponent<FestivalStageAudio>();if(stage!=null&&Decks(stage)!=null)UnityEngine.Object.DestroyImmediate(Decks(stage)[0].gameObject);
            if(stage!=null&&SmallDecks(stage)!=null)UnityEngine.Object.DestroyImmediate(SmallDecks(stage)[0].gameObject);
            // EditMode skips OnDestroy, so the crowd roar's source and clip go here too.
            var roar=stage==null?null:typeof(FestivalStageAudio).GetField("roar",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage) as AudioSource;
            if(roar!=null){UnityEngine.Object.DestroyImmediate(roar.clip);UnityEngine.Object.DestroyImmediate(roar.gameObject);}
            UnityEngine.Object.DestroyImmediate(host);
        }
        [Test]public void TwoSpeakerSetsPlayTheTwoPickedSongs()
        {
            var host=new GameObject("Two stages");
            try
            {
                var stage=Stage(host);
                foreach(int spin in new[]{1,2,3,12345})
                {
                    var state=Clock(42.5);state.RoundId="two-stages-"+spin;state.SpinSeed=spin;stage.Follow(state,state.SimulationSeconds,AudioSettings.dspTime);
                    var main=FestivalStageAudio.For(state);var small=FestivalStageAudio.For(state,StageMusic.Small);
                    Assert.That(main,Is.Not.SameAs(small),"spin "+spin+": the two stages play different songs");
                    Assert.That(stage.Music.clip,Is.SameAs(main.Audio),"spin "+spin+": the main stage's speakers play its pick");
                    Assert.That(stage.SmallMusic,Is.Not.Null,"spin "+spin+": the small stage plays");
                    Assert.That(stage.SmallMusic.clip,Is.SameAs(small.Audio),"spin "+spin+": the small stage's speakers play its pick");
                    Assert.That(stage.SmallTrack,Is.SameAs(small),"spin "+spin+": and name it");
                    Assert.That(stage.SmallMusic.timeSamples,Is.EqualTo(Expected(small,42.5+.2)).Within(1),"spin "+spin+": on the shared world clock, through the stream lead");
                }
                var smallSpeakers=SmallDecks(stage)[0].transform.position;
                Assert.That(Vector2.Distance(new Vector2(smallSpeakers.x,smallSpeakers.z),new Vector2(StageMusic.SmallX,StageMusic.SmallZ)),Is.LessThan(.01f),"at the small stage's front");
                Assert.That(SmallDecks(stage)[0].gameObject.name,Is.EqualTo("Small stage speakers"));
                Assert.That(SmallDecks(stage).Length,Is.EqualTo(2),"two decks take turns here too");
                foreach(var deck in SmallDecks(stage))
                {
                    Assert.That(deck.spatialBlend,Is.EqualTo(1),"heard from the small stage, in 3-D");
                    Assert.That(deck.rolloffMode,Is.EqualTo(AudioRolloffMode.Logarithmic));Assert.That(deck.dopplerLevel,Is.Zero);Assert.That(deck.playOnAwake,Is.False);
                }
                stage.Follow(new RoundState{Phase="Shopping"},0,AudioSettings.dspTime);
                Assert.That(stage.Music,Is.Null,"camp silences the main stage");Assert.That(stage.SmallMusic,Is.Null,"and the small one");
            }
            finally{RemoveStage(host);}
        }
        // The cross-fade times Unity's inverse rolloff past minDistance 12, the loudness each stage's crowd hears. The small stage is
        // heard only within its earshot (StageMusic.SmallHearingRadius); inside it the songs slide from one to the other.
        [Test]public void AListenerAtEachStageHearsMostlyItsOwnSong()
        {
            float Heard(float x,float z,int stage)
            {
                float fx=stage==StageMusic.Small?StageMusic.SmallX:StageMusic.MainX,fz=stage==StageMusic.Small?StageMusic.SmallZ:StageMusic.MainZ;
                float d=Mathf.Sqrt((x-fx)*(x-fx)+(z-fz)*(z-fz));return FestivalStageAudio.Mix(x,z,stage)*Mathf.Min(1,12/Mathf.Max(d,.01f));
            }
            var fronts=new[]{("the main stage's crowd",StageMusic.MainX,StageMusic.MainZ-6,StageMusic.Main),("the small stage's crowd",StageMusic.SmallX,StageMusic.SmallZ+3,StageMusic.Small)};
            foreach(var (who,x,z,own) in fronts)
            {
                int other=1-own;
                Assert.That(FestivalStageAudio.Mix(x,z,own),Is.GreaterThan(.95f),who+" gets nearly all of its own stage");
                Assert.That(Heard(x,z,own),Is.GreaterThan(15*Heard(x,z,other)),who+" hears its own song over 15 times louder than the other");
            }
            Assert.That(FestivalStageAudio.Mix(StageMusic.SmallX,StageMusic.SmallZ,StageMusic.Small),Is.EqualTo(1).Within(1e-4f),"the small stage's song is full at its front");
            Assert.That(FestivalStageAudio.Mix(StageMusic.SmallX,StageMusic.SmallZ,StageMusic.Main),Is.EqualTo(0).Within(1e-4f),"with none of the main stage's");
            float halfX=(StageMusic.MainX+StageMusic.SmallX)/2,halfZ=(StageMusic.MainZ+StageMusic.SmallZ)/2;
            Assert.That(FestivalStageAudio.Mix(halfX,halfZ,StageMusic.Main),Is.GreaterThan(FestivalStageAudio.Mix(halfX,halfZ,StageMusic.Small)),"halfway between the fronts the main stage wins");
            for(int k=0;k<72;k++)
            {
                float angle=k*Mathf.PI/36,x=StageMusic.SmallX+Mathf.Sin(angle)*(StageMusic.SmallHearingRadius+.05f),z=StageMusic.SmallZ+Mathf.Cos(angle)*(StageMusic.SmallHearingRadius+.05f);
                Assert.That(FestivalStageAudio.Mix(x,z,StageMusic.Main),Is.EqualTo(1),"past the small stage's earshot at "+x+", "+z+" the main stage has it all");
                Assert.That(FestivalStageAudio.Mix(x,z,StageMusic.Small),Is.EqualTo(0),"and the small stage none");
            }
            for(int i=0;i<8;i++){var at=Festivals.Arrival(i);Assert.That(FestivalStageAudio.Mix(at.X,at.Z,StageMusic.Main),Is.EqualTo(1),"crew member "+i+" arrives to the main stage's song alone");Assert.That(FestivalStageAudio.Mix(at.X,at.Z,StageMusic.Small),Is.EqualTo(0));}
            // A walk from the main front to the small one, half a metre a step: a smooth, equal-power slide that only ever trades one way.
            var from=new Vector2(StageMusic.MainX,StageMusic.MainZ);var to=new Vector2(StageMusic.SmallX,StageMusic.SmallZ);int steps=Mathf.CeilToInt(Vector2.Distance(from,to)/.5f);
            float lastMain=1,lastSmall=0;
            for(int k=0;k<=steps;k++)
            {
                var at=Vector2.Lerp(from,to,k/(float)steps);float main=FestivalStageAudio.Mix(at.x,at.y,StageMusic.Main),small=FestivalStageAudio.Mix(at.x,at.y,StageMusic.Small);
                Assert.That(main*main+small*small,Is.EqualTo(1).Within(1e-4f),"equal power at step "+k);
                Assert.That(main,Is.LessThanOrEqualTo(lastMain).And.GreaterThanOrEqualTo(lastMain-.05f),"the main stage's song fades by .05 or less a step, "+k);
                Assert.That(small,Is.GreaterThanOrEqualTo(lastSmall).And.LessThanOrEqualTo(lastSmall+.05f),"the small stage's song comes up by .05 or less a step, "+k);
                Assert.That(small>0,Is.EqualTo(StageMusic.Nearer(at.x,at.y)==StageMusic.Small),"a performance at step "+k+" charts to the song heard there");
                lastMain=main;lastSmall=small;
            }
        }
        static RoundState Clock(double world,string phase="Playing")=>new RoundState{Phase=phase,RoundId="same-song",SimulationSeconds=world};
        static int Expected(FestivalMusic.Track track,double world)=>(int)Math.Floor(RhythmChart.StageSampleAtWorld(track.Beats,world));
        [Test]public void TwoViewsScheduleTheProjectedSharedWorldSampleIncludingLoopJoins()
        {
            var a=new GameObject("First shared stage");var b=new GameObject("Second shared stage");
            try
            {
                var one=Stage(a);var two=Stage(b);var track=FestivalStageAudio.For(Clock(0));
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
            for(int choice=1;choice<=StageBeats.Tracks.Length;choice++)
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
        [Test]public void TheStageTickIsBriefAndTheDjSetHearsNoPulse()
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
                var dj=new InteractionState{Id="set",Kind="Dj",BeatSeconds=.4,StartSeconds=10};
                audio.Follow(dj,.5,false,null,AudioSettings.dspTime);
                foreach(var source in host.GetComponents<AudioSource>())Assert.That(source.clip,Is.Null,"a DJ set loads no pulse or tick ("+source.clip?.name+")");
            }
            finally
            {
                var audio=host.GetComponent<FestivalRhythmAudio>();var clip=(AudioClip)typeof(FestivalRhythmAudio).GetField("clip",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
                if(clip!=null)UnityEngine.Object.DestroyImmediate(clip);
                var pulse=(AudioClip)typeof(FestivalRhythmAudio).GetField("pulse",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);if(pulse!=null)UnityEngine.Object.DestroyImmediate(pulse);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

    }
}
