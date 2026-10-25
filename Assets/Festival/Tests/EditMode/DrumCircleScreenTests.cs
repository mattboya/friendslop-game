using System;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // MINI-2 on screen: a drummer plays their own part on the four lanes, rooted, with no live dancer and no dance; the crew's live
    // accuracy reads in a strip beside the lanes and on the card the watchers see; the banner names the worst part and what it did.
    // The circle's tick and its own song run on the game's clock from the stage's held sample while the festival stands still.
    public sealed class DrumCircleScreenTests
    {
        const string Rule="\nThe worst drummer sets the clock.";
        static FestivalSimulation Started()
        {
            var game=new FestivalSimulation(123);game.AddPlayer("p0","First");game.AddPlayer("p1","Second");game.AddPlayer("ghost","Watcher").Life="Spirit";
            game.State.Phase="Playing";game.State.Npcs.Clear();game.State.Booth=new MinigameState{Kind=FestivalSimulation.DrumCircleGame,Spot=1};var at=FestivalSimulation.BoothAt(game.State);
            foreach(var p in game.State.Players){p.X=at.X;p.Z=at.Z;}for(int k=0;k<35&&game.State.Phase!=FestivalSimulation.MinigamePhase;k++)game.Tick(.1);
            Assert.That(game.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"setup: the ring starts the drum circle");return game;
        }
        static InteractionState Part(FestivalSimulation game,string id)
        {var part=game.State.Interactions.Find(i=>i.PlayerId==id&&i.Kind==FestivalSimulation.DrumPartKind);Assert.That(part,Is.Not.Null,"setup: "+id+" has a part");return part;}
        static RoundState Wire(FestivalSimulation game,string id)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,id)));
        static (string,string) Card(RoundState view,string id)=>FestivalHudText.MinigameCard(view,view.Players.Find(p=>p.Id==id));

        [Test]public void ADrumPartPlaysTheLanesRootedButNeverAsADance()
        {
            var game=Started();var part=Part(game,"p0");var view=Wire(game,"p0");var own=view.Interactions.Find(i=>i.Id==part.Id);
            Assert.That(FestivalInput.IsRhythmKind(FestivalSimulation.DrumPartKind),Is.True,"a drum part plays on the four lanes");
            Assert.That(FestivalInput.IsRhythm(own),Is.True,"its lanes, input and tick follow the owner's own part");
            Assert.That(FestivalInput.Roots(own.Kind),Is.True,"it holds the drummer still");
            Assert.That(FestivalHudText.StopsByHold(view,view.Players.Find(p=>p.Id=="p0")),Is.True,"E runs nothing mid-part, and holding G withdraws");
            Assert.That(FestivalHudText.HoldToStopFor(own.Kind),Is.EqualTo("HOLD G TO STOP DRUMMING"),"the stop line names the drumming");
            foreach(var kind in new[]{"Dance","ConfirmDance","Poi","Dj"})Assert.That(FestivalHudText.HoldToStopFor(kind),Is.EqualTo(FestivalHudText.HoldToStop),kind+" keeps its line");
            Assert.That(FestivalHudText.ShowsDancer(own,false),Is.False,"nobody dances in the circle: no live dancer beside the lanes");
            Assert.That(FestivalHudText.RhythmTitle(own.Kind),Is.EqualTo("DRUM CIRCLE  /  YOUR PART"));
            foreach(string viewer in new[]{"p0","p1","ghost"})
                Assert.That(Wire(game,viewer).Players.Find(p=>p.Id=="p0")?.VisualPose??"",Is.Not.EqualTo("Dance"),viewer+" never sees the drummer dance");
            var note=RhythmChart.For(part).Notes[0];game.Tick(note.TimeSeconds);int steps=game.Player("p0").VisualDanceStepSequence;
            Assert.That(game.Execute("p0",new GameCommand{Id="drum-screen-hit",Kind="Rhythm",Direction=note.Direction,TimeSeconds=note.TimeSeconds}).Accepted,Is.True,"setup: a hit");
            Assert.That(game.Player("p0").VisualDanceStepSequence,Is.EqualTo(steps),"a hit pulses no dance step");
        }

        [Test]public void TheCrewStripShowsEveryPartsLiveAccuracyToDrummersAndWatchers()
        {
            var game=Started();var part=Part(game,"p0");var note=RhythmChart.For(part).Notes[0];game.Tick(note.TimeSeconds);
            Assert.That(game.Execute("p0",new GameCommand{Id="drum-strip-hit",Kind="Rhythm",Direction=note.Direction,TimeSeconds=note.TimeSeconds}).Accepted,Is.True,"setup: a hit");game.Tick(.1);
            Assert.That(Card(Wire(game,"p0"),"p0"),Is.EqualTo(("DRUM CIRCLE","You 100%  •  Second 0%"+Rule)),"a drummer reads the whole crew, themselves as You");
            Assert.That(Card(Wire(game,"p1"),"p1"),Is.EqualTo(("DRUM CIRCLE","First 100%  •  You 0%"+Rule)));
            Assert.That(Card(Wire(game,"ghost"),"ghost"),Is.EqualTo(("DRUM CIRCLE","First 100%  •  Second 0%"+Rule)),"a spirit reads the captured names, never living players' state");
            Assert.That(FestivalHudText.MinigameWon(Wire(game,"p0")),Is.False,"not scored yet");
        }

        // The verdict follows the worst part's band; the clock line says what the result actually did, after the fifteen-second floor.
        [Test]public void TheBannerNamesTheWorstPartAndWhatItDidToTheClock()
        {
            foreach(var (worst,change,title,detail) in new[]{
                (.8,45,"+45 s","In the pocket. Worst part: Sam, 80%. Added to the clock. Nearby wooks ease off."),
                (.55,0,"+0 s","Decent. Worst part: Sam, 55%. The clock is unchanged."),
                (.2,-30,"−30 s","Off the beat. Worst part: Sam, 20%. Taken off the clock. Nearby wooks grow suspicious."),
                (.2,-5,"−5 s","Off the beat. Worst part: Sam, 20%. Taken off the clock. Nearby wooks grow suspicious."),
                (.2,0,"+0 s","Off the beat. Worst part: Sam, 20%. The clock is unchanged. Nearby wooks grow suspicious.")})
            {
                var s=new RoundState{Phase=FestivalSimulation.MinigamePhase,Booth=new MinigameState{Kind=FestivalSimulation.DrumCircleGame,Spot=1,Seconds=20,ScoredAt=19,ClockChange=change,DrumWorstScore=worst}};
                var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);
                foreach(var (id,name,accuracy) in new[]{("you","Mo",.95),("sam","Sam",worst),("kai","Kai",worst)})
                {s.Booth.PlayerIds.Add(id);s.Booth.Participants.Add(new MinigameParticipant{Id=id,Name=name});s.Booth.DrumAccuracies.Add(new DrumAccuracy{PlayerId=id,Accuracy=accuracy});}
                Assert.That(FestivalHudText.MinigameCard(s,you),Is.EqualTo((title,detail+" The festival starts again in 3.")),"worst "+worst+", clock "+change+": the first worst part in the roster is named");
                Assert.That(FestivalHudText.MinigameWon(s),Is.EqualTo(worst>=.4),"only a worst part of 40% or better reads as a win, worst "+worst+", clock "+change);
            }
            var mine=new RoundState{Phase=FestivalSimulation.MinigamePhase,Booth=new MinigameState{Kind=FestivalSimulation.DrumCircleGame,Spot=1,Seconds=20,ScoredAt=19,ClockChange=-30,DrumWorstScore=0}};
            var me=new PlayerState{Id="me",Name="Me"};mine.Players.Add(me);mine.Booth.PlayerIds.Add("me");mine.Booth.Participants.Add(new MinigameParticipant{Id="me",Name="Me"});mine.Booth.DrumAccuracies.Add(new DrumAccuracy{PlayerId="me"});
            Assert.That(FestivalHudText.MinigameCard(mine,me).Item2,Does.StartWith("Off the beat. Worst part: you, 0%."),"the viewer's own part reads as theirs");
        }

        static AudioSource[] Sources(Component c)=>c.GetComponents<AudioSource>();
        static T Awake<T>(GameObject host) where T:MonoBehaviour
        {
            var component=host.AddComponent<T>();typeof(T).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(component,null);return component;
        }

        [Test]public void TheTickRunsOnTheGameClockWhileTheWorldHolds()
        {
            var game=Started();var part=Part(game,"p0");var held=Wire(game,"p0");var own=held.Interactions.Find(i=>i.Id==part.Id);
            var dance=new InteractionState{Id="dance",Kind="Dance",Status="Active"};
            Assert.That(FestivalRhythmAudio.Held(held,own),Is.False,"a drum part ticks on the game's own clock while the world holds");
            Assert.That(FestivalRhythmAudio.Held(held,dance),Is.True,"any other challenge holds with the world");
            Assert.That(FestivalRhythmAudio.Held(new RoundState{Phase="Playing"},dance),Is.False,"and ticks while the festival plays");
            Assert.That(FestivalRhythmAudio.Held(new RoundState{Phase="Results"},own),Is.True,"nothing ticks at the results");
            var host=new GameObject("Drum tick under test");
            try
            {
                var audio=Awake<FestivalRhythmAudio>(host);var cued=typeof(FestivalRhythmAudio).GetField("interactionId",BindingFlags.Instance|BindingFlags.NonPublic);
                audio.Follow(own,own.StageFirstHeadDelaySeconds-.5,FestivalRhythmAudio.Held(held,own),null,AudioSettings.dspTime);
                Assert.That(cued.GetValue(audio),Is.EqualTo(own.Id),"the tick is cued on the part's own grid");
                Assert.That(Array.Exists(Sources(audio),s=>s.clip!=null&&s.clip.name=="Authored festival tick"),Is.True,"the quiet authored tick");
                audio.Follow(own,own.StageFirstHeadDelaySeconds,true,null,AudioSettings.dspTime);
                Assert.That(cued.GetValue(audio),Is.EqualTo(""),"held, it stops");
            }
            finally
            {
                var audio=host.GetComponent<FestivalRhythmAudio>();
                foreach(var name in new[]{"clip","pulse"}){var clip=(AudioClip)typeof(FestivalRhythmAudio).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);if(clip!=null)UnityEngine.Object.DestroyImmediate(clip);}
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static int Expected(FestivalMusic.Track track,double world)=>(int)Math.Floor(RhythmChart.StageSampleAtWorld(track.Beats,world));
        [Test]public void TheCirclesSongPlaysOnFromTheStagesHeldSampleByTheGameClock()
        {
            var state=new RoundState{Phase=FestivalSimulation.MinigamePhase,RoundId="drum-round",FestivalIndex=0,LevelIndex=1,SimulationSeconds=72.5,Booth=new MinigameState{Kind=FestivalSimulation.DrumCircleGame,Spot=1,Seconds=4.25}};
            Assert.That(FestivalDrumAudio.SongSeconds(state,4.5),Is.EqualTo(68.25+4.5).Within(1e-9),"the held world position, on by the game's own seconds");
            var host=new GameObject("Drum song under test");
            try
            {
                var drums=Awake<FestivalDrumAudio>(host);var stage=Awake<FestivalStageAudio>(host);var track=FestivalStageAudio.For(state);
                Assert.That(FestivalMusic.For(FestivalSimulation.DrumCircleTrack(state)),Is.SameAs(track),"setup: the east crosspath booth is nearer the main stage");
                drums.Follow(state,4.5,AudioSettings.dspTime);stage.Follow(state,999,AudioSettings.dspTime);
                Assert.That(drums.Music,Is.Not.Null,"the circle plays its own song");
                Assert.That(drums.Music.clip,Is.SameAs(track.Audio),"the stage's own recording");
                Assert.That(drums.Music.timeSamples,Is.EqualTo(Expected(track,68.25+4.5+.2)).Within(1),"cued from the held sample, on by the game clock and the stream lead");
                Assert.That(drums.Music.spatialBlend,Is.Zero,"heard in the circle, not from the stage");
                Assert.That(stage.Music.timeSamples,Is.EqualTo(Expected(track,68.25)).Within(1),"the stage itself stays held");
                Assert.That(drums.Music,Is.Not.SameAs(stage.Music),"on decks of its own");
                foreach(var (phase,kind,scored,why) in new[]{("Playing",FestivalSimulation.DrumCircleGame,-1.0,"play resumed"),(FestivalSimulation.MinigamePhase,FestivalSimulation.DrumCircleGame,19.0,"the circle is scored"),(FestivalSimulation.MinigamePhase,FestivalSimulation.MysteryCupsGame,-1.0,"another game")})
                {
                    drums.Follow(state,4.5,AudioSettings.dspTime);Assert.That(drums.Music,Is.Not.Null,"setup: the song plays again");
                    var other=new RoundState{Phase=phase,RoundId=state.RoundId,FestivalIndex=0,LevelIndex=1,SimulationSeconds=72.5,Booth=new MinigameState{Kind=kind,Spot=1,Seconds=4.25,ScoredAt=scored}};
                    Assert.That(FestivalDrumAudio.SongSeconds(other,4.5),Is.Null,why);
                    drums.Follow(other,4.5,AudioSettings.dspTime);Assert.That(drums.Music,Is.Null,why+": the song stops");
                }
                drums.Follow(state,4.5,AudioSettings.dspTime);drums.Follow(null,0,AudioSettings.dspTime);Assert.That(drums.Music,Is.Null,"leaving the session stops it");
                state.Booth.Spot=0;var small=FestivalMusic.For(StageMusic.Track(state,StageMusic.Small));
                Assert.That(FestivalSimulation.DrumCircleTrack(state),Is.EqualTo(StageMusic.Track(state,StageMusic.Small)),"setup: the west booth is nearer the small stage");
                drums.Follow(state,4.5,AudioSettings.dspTime);Assert.That(drums.Music.clip,Is.SameAs(small.Audio),"the circle plays the small stage's song");
            }
            finally
            {
                var stage=host.GetComponent<FestivalStageAudio>();
                foreach(var field in new[]{"decks","smallDecks"}){var decks=(AudioSource[])typeof(FestivalStageAudio).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage);if(decks!=null)UnityEngine.Object.DestroyImmediate(decks[0].gameObject);}
                var drums=host.GetComponent<FestivalDrumAudio>();var own=(AudioSource[])typeof(FestivalDrumAudio).GetField("decks",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(drums);
                if(own!=null)UnityEngine.Object.DestroyImmediate(own[0].gameObject);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
