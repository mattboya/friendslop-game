using System;
using System.Collections;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // MINI-2 between two real sessions: a joined friend and the host stand in a Drum Circle booth's ring. The friend's HUD plays only
    // their own 24-tap part on the lanes, with the crew's accuracy beside it and no dancer; a lane key reaches the host's judge on the
    // game's clock. The circle's song plays on from the stage's held sample while the stage and the festival hold. Scoring closes the
    // parts and stops the song and the tick; the banner names the worst part; then play and the stage resume. Leaving mid-circle
    // stops the friend's song and lanes, and the host still scores their part.
    public sealed class DrumCirclePlayTests:FestivalPlayModeTest
    {
        static readonly Key[] ArrowKeys={Key.LeftArrow,Key.DownArrow,Key.UpArrow,Key.RightArrow};
        static IEnumerator Wait(double seconds){double until=Time.realtimeSinceStartupAsDouble+seconds;while(Time.realtimeSinceStartupAsDouble<until)yield return null;}
        static Transform Hud(GameObject root,string path)=>root.transform.Find("Festival HUD/"+path);
        static bool Shown(GameObject root,string path){var t=Hud(root,path);return t!=null&&t.gameObject.activeInHierarchy;}
        static string Text(GameObject root,string path)=>Hud(root,path)?.GetComponent<Text>()?.text;
        static bool Ticking(GameObject root)=>Array.Exists(root.GetComponent<FestivalRhythmAudio>().GetComponents<AudioSource>(),s=>s.clip!=null&&s.clip.name=="Authored festival tick"&&s.isPlaying);
        // How far the circle's song is from the held world position on by the game's own clock, in seconds.
        static double SongMiss(FestivalSession session,FestivalDrumAudio drums)
        {
            var track=FestivalMusic.For(FestivalSimulation.DrumCircleTrack(session.State));double expected=RhythmChart.StageSampleAtWorld(track.Beats,FestivalSimulation.WorldSeconds(session.State)+session.EstimatedMinigameSeconds);
            double miss=Math.Abs(drums.Music.timeSamples-expected);return Math.Min(miss,Math.Abs(track.LoopEnd-track.FirstBeat-miss))/FestivalMusic.Rate;
        }

        IEnumerator Circle(GameObject hostRoot,GameObject ownerRoot)
        {
            var host=hostRoot.GetComponent<FestivalSession>();var owner=ownerRoot.GetComponent<FestivalSession>();
            ushort port=FreePort();host.Host("Host",port);yield return null;Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);host.Controls.Map.Disable();
            owner.Join("Owner","127.0.0.1",port);
            for(double deadline=Time.realtimeSinceStartupAsDouble+30;owner.LocalPlayer==null&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
            Assert.That(owner.LocalPlayer,Is.Not.Null,owner.Message);
            var game=Simulation(host);var mate=game.Player(owner.LocalPlayerId);mate.X=0;mate.Z=19;owner.Command("Ready");
            for(double deadline=Time.realtimeSinceStartupAsDouble+5;!mate.Ready&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
            Assert.That(mate.Ready,Is.True,owner.Message);yield return StartLevel(host,owner.LocalPlayerId);
            owner.Profile.Data.MusicVolume=1;game.State.Booth.Kind=FestivalSimulation.DrumCircleGame;
            var at=FestivalSimulation.BoothAt(game.State);foreach(var p in game.State.Players){p.X=at.X+(p.Id==mate.Id?1:-1);p.Z=at.Z;}
            for(double deadline=Time.realtimeSinceStartupAsDouble+8;(owner.State.Phase!=FestivalSimulation.MinigamePhase||owner.State.Interactions.Count==0)&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
            Assert.That(owner.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"the ring starts the circle: "+owner.Message);
            yield return Wait(.3);
        }

        [UnityTest]public IEnumerator AJoinedDrummerPlaysTheirOwnPartToTheCirclesSongWhileTheFestivalHolds()
        {
            UseAVirtualKeyboard("impl:U61 drum keyboard");
            var world=new GameObject("impl:U61 drum world");world.AddComponent<FestivalWorld>();yield return null;
            var hostRoot=new GameObject("impl:U61 drum host");hostRoot.AddComponent<FestivalSession>();yield return null;
            var ownerRoot=new GameObject("impl:U61 drum owner");var owner=ownerRoot.AddComponent<FestivalSession>();ownerRoot.AddComponent<FestivalHud>();yield return null;
            var host=hostRoot.GetComponent<FestivalSession>();
            try
            {
                yield return Circle(hostRoot,ownerRoot);
                var game=Simulation(host);var mate=game.Player(owner.LocalPlayerId);
                var part=game.State.Interactions.Find(i=>i.PlayerId==mate.Id&&i.Kind==FestivalSimulation.DrumPartKind);Assert.That(part,Is.Not.Null,"the friend has a part");
                Assert.That(owner.State.Interactions.TrueForAll(i=>i.PlayerId==owner.LocalPlayerId),Is.True,"the friend's client carries only their own part");
                Assert.That(host.State.Interactions.TrueForAll(i=>i.PlayerId==host.LocalPlayerId),Is.True,"and the host's view only the host's");
                Assert.That(Time.timeScale,Is.Zero,"the festival holds");

                // The lanes: the friend's own 24 taps, the crew's accuracy beside them, no dancer, and the minigame card under them.
                Assert.That(Shown(ownerRoot,"Rhythm lane"),Is.True,"the drummer's lanes are up");
                Assert.That(Text(ownerRoot,"Rhythm lane/Challenge title"),Is.EqualTo("DRUM CIRCLE  /  YOUR PART"));
                Assert.That(Text(ownerRoot,"Rhythm lane/Tempo and steps"),Does.StartWith("0 / 24   •   "),"all 24 taps of the part");
                Assert.That(Text(ownerRoot,"Rhythm lane/Stop hold"),Is.EqualTo("HOLD G TO STOP DRUMMING"));
                Assert.That(Shown(ownerRoot,"Live dancer"),Is.False,"nobody dances in the circle");
                Assert.That(Shown(ownerRoot,"Drum crew"),Is.True,"the crew's accuracy shows beside the lanes");
                Assert.That(Text(ownerRoot,"Drum crew/Drum crew title"),Is.EqualTo("DRUM CIRCLE"));
                Assert.That(Text(ownerRoot,"Drum crew/Drum crew strip"),Does.StartWith("Host 0%  •  You 0%"),"the host first, the friend as You");
                Assert.That(Shown(ownerRoot,"Minigame card"),Is.False,"the lanes cover the watchers' card");

                // The circle's song plays on from the held stage sample by the game's clock; the stage itself holds. The tick is cued.
                var drums=ownerRoot.GetComponent<FestivalDrumAudio>();var stage=ownerRoot.GetComponent<FestivalStageAudio>();
                Assert.That(drums,Is.Not.Null,"the drum circle's own audio rides on the session");
                Assert.That(drums.Music!=null&&drums.Music.isPlaying,Is.True,"the circle's song plays");
                Assert.That(drums.Music.clip,Is.SameAs(FestivalMusic.For(FestivalSimulation.DrumCircleTrack(owner.State)).Audio),"the song of the stage nearer the booth");
                Assert.That(stage.Music.isPlaying,Is.False,"the stage holds its sample");
                double held=owner.EstimatedWorldSeconds,song=drums.WorldSeconds;yield return Wait(.8);
                Assert.That(owner.EstimatedWorldSeconds,Is.EqualTo(held).Within(1e-9),"the world clock stands still");
                Assert.That(drums.WorldSeconds-song,Is.InRange(.6,1.0),"while the song runs on");
                // Sampled where the transport vouches for the song: the frame of its own drift check, or as that check's re-cue sounds.
                // Between checks a long frame on a loaded machine costs the game's clock time while the song plays on.
                var transport=typeof(FestivalDrumAudio).GetField("transport",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(drums);
                object Field(string name)=>transport.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(transport);
                double check=(double)Field("nextCheck");
                for(double deadline=Time.realtimeSinceStartupAsDouble+2;(double)Field("nextCheck")==check&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That((double)Field("nextCheck"),Is.Not.EqualTo(check),"setup: the transport checks the song's drift");
                for(double deadline=Time.realtimeSinceStartupAsDouble+2;(bool)Field("handoff")&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(SongMiss(owner,drums),Is.LessThan(.075),"at the held world position plus the game's own seconds");
                Assert.That(Ticking(ownerRoot),Is.True,"the part's tick is cued on the game clock");

                // A lane key reaches the host's judge in time, with no dance step.
                var chart=RhythmChart.For(part);double now=owner.EstimatedSimulationSeconds-part.StartSeconds;var note=chart.Notes.Find(n=>n.TimeSeconds>now+.6);
                Assert.That(note,Is.Not.Null,"setup: a note still to come");int steps=mate.VisualDanceStepSequence;
                for(double deadline=Time.realtimeSinceStartupAsDouble+15;owner.EstimatedSimulationSeconds-part.StartSeconds<note.TimeSeconds-.01&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(ArrowKeys[note.Direction]));yield return null;yield return null;
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());yield return null;
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;part.Inputs.Count==0&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(part.Inputs.Count,Is.EqualTo(1),"one press reached the host: "+owner.Message);
                Assert.That(part.Inputs[0].Direction,Is.EqualTo(note.Direction));
                Assert.That(part.Inputs[0].TimeSeconds,Is.EqualTo(note.TimeSeconds).Within(part.GoodWindowSeconds),"on the game clock, inside the window");
                Assert.That(mate.VisualDanceStepSequence,Is.EqualTo(steps),"a hit is no dance step");
                yield return Wait(.4);
                Assert.That(owner.State.Booth.DrumAccuracies.Find(a=>a.PlayerId==owner.LocalPlayerId).Accuracy,Is.GreaterThan(0),"the host publishes the friend's live accuracy");
                Assert.That(Text(ownerRoot,"Drum crew/Drum crew strip"),Is.EqualTo(FestivalHudText.MinigameCard(owner.State,owner.LocalPlayer).Item2),"and the strip shows it");

                // Every part ends: the host scores them all, the lanes, song and tick stop, and the banner names the worst part.
                foreach(var i in game.State.Interactions)if(i.Kind==FestivalSimulation.DrumPartKind)i.DurationSeconds=0;
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;owner.State.Booth.ScoredAt<0&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(owner.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0),"the circle is scored");yield return Wait(.3);
                Assert.That(game.State.Interactions.TrueForAll(i=>i.Kind!=FestivalSimulation.DrumPartKind||i.Status!="Active"),Is.True,"every part is closed");
                Assert.That(Shown(ownerRoot,"Rhythm lane")||Shown(ownerRoot,"Drum crew"),Is.False,"the lanes and the strip go");
                Assert.That(drums.Music,Is.Null,"the song stops");
                Assert.That(Ticking(ownerRoot),Is.False,"and the tick");
                Assert.That(Shown(ownerRoot,"Minigame card"),Is.True,"the banner shows");
                Assert.That(Text(ownerRoot,"Minigame card/Minigame title"),Is.EqualTo("−30 s"));
                Assert.That(Text(ownerRoot,"Minigame card/Minigame detail"),Does.StartWith("Off the beat. Worst part: Host, 0%. Taken off the clock."));
                Assert.That(stage.Music.isPlaying,Is.False,"the stage still holds through the banner");

                for(double deadline=Time.realtimeSinceStartupAsDouble+6;owner.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(owner.State.Phase,Is.EqualTo("Playing"),"play resumes");yield return Wait(.5);
                Assert.That(Time.timeScale,Is.EqualTo(1),"the festival moves again");
                Assert.That(stage.Music.isPlaying,Is.True,"the stage plays on from its held sample");
                Assert.That(drums.Music,Is.Null,"and the circle's song stays off");
            }
            finally{owner.Leave();host.Leave();UnityEngine.Object.Destroy(ownerRoot);UnityEngine.Object.Destroy(hostRoot);UnityEngine.Object.Destroy(world);}
            yield return null;
        }

        [UnityTest]public IEnumerator LeavingMidCircleStopsTheSongAndLanesAndTheHostStillScoresThePart()
        {
            var world=new GameObject("impl:U61 drum leave world");world.AddComponent<FestivalWorld>();yield return null;
            var hostRoot=new GameObject("impl:U61 drum leave host");hostRoot.AddComponent<FestivalSession>();yield return null;
            var ownerRoot=new GameObject("impl:U61 drum leave owner");var owner=ownerRoot.AddComponent<FestivalSession>();ownerRoot.AddComponent<FestivalHud>();yield return null;
            var host=hostRoot.GetComponent<FestivalSession>();
            try
            {
                yield return Circle(hostRoot,ownerRoot);
                var game=Simulation(host);string id=owner.LocalPlayerId;var drums=ownerRoot.GetComponent<FestivalDrumAudio>();
                Assert.That(Shown(ownerRoot,"Rhythm lane"),Is.True,"setup: the friend drums");
                Assert.That(drums!=null&&drums.Music!=null&&drums.Music.isPlaying,Is.True,"setup: to the circle's song");
                owner.Leave();yield return Wait(.3);
                Assert.That(drums.Music,Is.Null,"leaving stops the song");
                Assert.That(Shown(ownerRoot,"Rhythm lane")||Shown(ownerRoot,"Drum crew"),Is.False,"and the lanes and strip");
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;game.State.Interactions.Exists(i=>i.PlayerId==id&&i.Kind==FestivalSimulation.DrumPartKind&&i.Status=="Active")&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(game.State.Interactions.Find(i=>i.PlayerId==id&&i.Kind==FestivalSimulation.DrumPartKind).Status,Is.Not.EqualTo("Active"),"the host closes the leaver's part");
                Assert.That(game.State.Booth.PlayerIds,Does.Contain(id),"and still scores it with the crew");
                foreach(var i in game.State.Interactions)if(i.Kind==FestivalSimulation.DrumPartKind)i.DurationSeconds=0;
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;game.State.Booth.ScoredAt<0&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(game.State.Booth.ClockChange,Is.EqualTo(-30),"a silent leaver sinks the circle");
            }
            finally{owner.Leave();host.Leave();UnityEngine.Object.Destroy(ownerRoot);UnityEngine.Object.Destroy(hostRoot);UnityEngine.Object.Destroy(world);}
            yield return null;
        }
    }
}
