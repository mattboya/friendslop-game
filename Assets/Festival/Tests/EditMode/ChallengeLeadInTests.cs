using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    public sealed class ChallengeLeadInTests
    {
        private GameObject made;
        [TearDown]public void Cleanup()
        {
            if(made!=null)Object.DestroyImmediate(made);
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
        }

        private static FestivalSimulation Pair()
        {
            var game=new FestivalSimulation(7);var player=game.AddPlayer("p0","Tester");
            game.State.Phase="Playing";game.State.SimulationSeconds=10;game.State.Npcs.Clear();
            player.X=0;player.Z=0;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance="ketamine",RemainingSeconds=100});
            game.State.TripperId=player.Id;
            var partner=new NpcState{Id="lead_in_partner",Kind="Wook",Role="Buyer",X=0,Z=1.5f,Yaw=180,CanTalk=true};
            partner.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=40});game.State.Npcs.Add(partner);
            game.State.Visions.Add(new VisionState{Id="check",NpcId=partner.Id,Kind="Buyer",IsTrue=true});
            return game;
        }

        private static InteractionState Start(FestivalSimulation game,string kind)
        {
            string item=kind=="Sale"?"stock_lsd":kind=="Buy"?FestivalSimulation.SellerStock("lead_in_partner"):"";
            if(kind=="Buy")game.State.Npcs[0].Role="Regular";
            var result=game.Execute("p0",new GameCommand{Id=Guid.NewGuid().ToString("N"),Kind=kind=="Sale"?"StartSale":kind=="Buy"?"StartBuy":kind,TargetId="lead_in_partner",ItemId=item});
            Assert.That(result.Accepted,Is.True,result.Reason);return game.Interaction(game.Player("p0").InteractionId);
        }

        [TestCase("ConfirmDance")]
        [TestCase("Sale")]
        [TestCase("Buy")]
        [TestCase("Conversation")]
        public void TheFirstPonyDustArrowEntersAtTheBottom(string kind)
        {
            var game=Pair();double pressed=game.State.SimulationSeconds;var interaction=Start(game,kind);
            Assert.That(JsonUtility.ToJson(interaction),Does.Contain("\"StageTrack\":"),"the real HUD interaction carries the stage grid");
            var chart=RhythmChart.For(interaction);
            made=new GameObject("impl:U06 lane entry");var hud=made.AddComponent<FestivalHud>();
            typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            hud.ShowChart(interaction,chart);
            var note=made.transform.Find("Festival HUD/Rhythm lane/Notes/Note 0").GetComponent<Image>();
            hud.DrawChart(pressed-interaction.StartSeconds,2.4,"ketamine",false);
            Assert.That(note.gameObject.activeSelf,Is.False,"the first frame has no arrow already partway up the lane");
            // Sample the real first visibility boundary, as UpdateRhythm does from the host's start and the local lane lead.
            double enters=interaction.StartSeconds+chart.Notes[0].TimeSeconds-2.4;
            hud.DrawChart(enters-interaction.StartSeconds-.001,2.4,"ketamine",false);
            Assert.That(note.gameObject.activeSelf,Is.False,"just before its lane entry the first arrow is hidden");
            hud.DrawChart(enters-interaction.StartSeconds+.000001,2.4,"ketamine",false);
            Assert.That(note.gameObject.activeSelf,Is.True,"at lane entry the first arrow is visible");
            Assert.That(note.rectTransform.anchorMin.y,Is.EqualTo(.11f).Within(.00001f),"its first visible frame sits on the spawn line");
            hud.DrawChart(chart.Notes[0].TimeSeconds,2.4,"ketamine",false);
            var target=(RectTransform)made.transform.Find("Festival HUD/Rhythm lane/Receptor well "+chart.Notes[0].Direction+"/Target arrow");
            Assert.That(Vector3.Distance(note.rectTransform.TransformPoint(note.rectTransform.rect.center),target.TransformPoint(target.rect.center)),Is.LessThan(1),"the scheduled note still lands on its receptor");
        }

        [Test]public void IncomingStageAnchorChangesTheRealHudChartAndSurvivesTheOwnerView()
        {
            var incoming=JsonUtility.FromJson<InteractionState>("{\"Kind\":\"Dance\",\"ChartSeed\":17,\"NoteCount\":16,\"ChartDifficulty\":3,\"BeatSeconds\":0.5,\"StageTrack\":1,\"StageFirstHeadWorldSeconds\":10,\"StageFirstHeadDelaySeconds\":2.23,\"StageBeatStride\":1}");
            var chart=RhythmChart.For(incoming);Assert.That(chart.Notes[0].TimeSeconds,Is.EqualTo(2.23).Within(.000001),"JSON drives the production For entry point");
            // The production pick finds each loop's first spin seed, so the case follows any change to the pick.
            var seeds=new List<int>();
            for(int track=1;track<=StageBeats.Tracks.Length;track++)
            {
                int seed=0;while(seed<1000&&StageMusic.Track(seed,StageMusic.Main)!=track)seed++;
                Assert.That(seed,Is.LessThan(1000),"setup: a spin seed puts loop "+track+" on the main stage");seeds.Add(seed);
            }
            seeds.Sort();Assert.That(seeds[0],Is.EqualTo(0),"setup: spin seed 0, whose decode drifted first, comes first");
            foreach(int seed in seeds)
            {
                string at="spin seed "+seed+" (main loop "+StageMusic.Track(seed,StageMusic.Main)+")";
                var game=Pair();game.State.SpinSeed=seed;game.State.Players.Add(new PlayerState{Id="p1",Name="Friend"});var interaction=Start(game,"ConfirmDance");
                var owner=JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,"p0")));
                var p0=game.Player("p0");Assert.That(interaction.StageTrack,Is.EqualTo(StageMusic.TrackAt(game.State,p0.X,p0.Z)),at+": the check rides the nearer stage's song");
                Assert.That(interaction.StageTrack,Is.EqualTo(StageMusic.Track(seed,StageMusic.Main)),at+": the main stage's song");
                Assert.That(JsonUtility.ToJson(owner.Interactions[0]),Does.Contain("\"StageTrack\":"+interaction.StageTrack),at);
                SameChart(RhythmChart.For(owner.Interactions[0]),RhythmChart.For(interaction),at+": host and owner decode the same chart");
                Assert.That(FestivalSession.ViewFor(game,"p1").Interactions,Is.Empty,at+": private check charts remain owner-only");
                var restored=new FestivalSimulation();restored.Restore(JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(game.State)));
                SameChart(RhythmChart.For(restored.Interaction(interaction.Id)),RhythmChart.For(interaction),at+": a restored snapshot decodes the same chart");
            }
        }
        // JsonUtility writes a few doubles one unit in the last place off, so a decoded chart's times can sit about 1e-16 s from the
        // host's. Every other field is exact.
        static void SameChart(RhythmChart decoded,RhythmChart host,string why)
        {
            Assert.That(decoded.Notes.Count,Is.EqualTo(host.Notes.Count),why+": note count");
            Assert.That(decoded.DurationSeconds,Is.EqualTo(host.DurationSeconds).Within(1e-9),why+": length");
            for(int i=0;i<host.Notes.Count;i++)
            {
                RhythmNote a=decoded.Notes[i],b=host.Notes[i];
                Assert.That((a.Id,a.Direction,a.Grey),Is.EqualTo((b.Id,b.Direction,b.Grey)),why+": note "+i);
                Assert.That(a.TimeSeconds,Is.EqualTo(b.TimeSeconds).Within(1e-9),why+": note "+i+" time");
                Assert.That(a.HoldSeconds,Is.EqualTo(b.HoldSeconds).Within(1e-9),why+": note "+i+" hold");
            }
        }

        [UnityTearDown]public IEnumerator LeaveOwnedPlayMode(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest]public IEnumerator HostedCalibratedStageHoldsUseMappedHeadsAndTails()
        {
            yield return new EnterPlayMode();
            string priorFolder=LocalProfile.Folder,priorScope=FestivalInput.PreferencesScope;
            string folder=Path.Combine(Path.GetTempPath(),"impl-U25-stage-input-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            LocalProfile.Folder=folder;FestivalInput.PreferencesScope=folder;var host=new GameObject("impl-U25 stage host");
            try
            {
                var session=host.AddComponent<FestivalSession>();session.enabled=false;session.Host("Stage test",8610);
                Assert.That(session.Connected&&session.IsHost,Is.True,session.Message);
                var game=Pair();game.State.LevelIndex=3;var interaction=Start(game,"Conversation");
                var p0=game.Player("p0");Assert.That(JsonUtility.ToJson(interaction),Does.Contain("\"StageTrack\":"+StageMusic.TrackAt(game.State,p0.X,p0.Z)),"real trust command selected the nearer stage's song");
                interaction.NoteCount=16;interaction.ChartSeed=0;interaction.DurationSeconds=RhythmChart.For(interaction).DurationSeconds;
                var chart=RhythmChart.For(interaction);var hold=chart.Notes.Find(n=>n.HoldSeconds>0);Assert.That(hold,Is.Not.Null,"production-verified seed 0 contains a hold at ID 2");Assert.That(hold.Id,Is.EqualTo(2));
                typeof(FestivalSession).GetField("simulation",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,game);
                typeof(FestivalSession).GetProperty("LocalPlayerId").SetValue(session,"p0");typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(game,"p0"));
                session.Profile.Data.TimingCalibrationMs=60;
                foreach(var note in chart.Notes)
                {
                    game.State.SimulationSeconds=interaction.StartSeconds+note.TimeSeconds+.06;session.Rhythm(note.Direction,note.TimeSeconds+.06);
                    Assert.That(interaction.Inputs[interaction.Inputs.Count-1].TimeSeconds,Is.EqualTo(note.TimeSeconds).Within(.000001),session.Message);
                    if(note.HoldSeconds>0)
                    {
                        game.State.SimulationSeconds=interaction.StartSeconds+note.TimeSeconds+note.HoldSeconds+.06;
                        session.Rhythm(note.Direction,note.TimeSeconds+note.HoldSeconds+.06,true);
                        Assert.That(interaction.Inputs[interaction.Inputs.Count-1].Release,Is.True,session.Message);
                    }
                }
                game.State.SimulationSeconds=interaction.StartSeconds+chart.DurationSeconds+.74;game.Tick(.001);
                Assert.That(interaction.Turns,Is.Empty,"the mapped duration still has a late verdict window");
                game.Tick(.02);Assert.That(interaction.Turns.Count,Is.EqualTo(1),"the real mapped hold judge unlocks the conversation");
                Assert.That(game.State.Npcs[0].Observers[0].Suspicion,Is.EqualTo(25).Within(.001),"every calibrated head and retained tail scores perfect");
                Assert.That(JsonUtility.ToJson(RhythmChart.For(interaction)),Is.EqualTo(JsonUtility.ToJson(chart)),"calibration never moves chart time");
            }
            finally{Object.DestroyImmediate(host);LocalProfile.Folder=priorFolder;FestivalInput.PreferencesScope=priorScope;Directory.Delete(folder,true);}
            yield return new ExitPlayMode();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [TestCase("ConfirmDance")]
        [TestCase("Conversation")]
        public void DebugDiagnosticsReportTheScheduledStartWithoutPrivateContent(string kind)
        {
            var game=Pair();var events=new List<(string stage,string detail,string round,double time)>();
            game.ChatDiagnostic=(stage,detail,round,time)=>events.Add((stage,detail,round,time));
            var interaction=Start(game,kind);
            Assert.That(events.Count,Is.EqualTo(1),"the existing opt-in debug callback reports challenge scheduling");
            Assert.That(events[0].stage,Is.EqualTo("ChallengeLeadIn"));
            Assert.That(events[0].detail,Is.EqualTo(kind),"only the interaction kind is logged");
            Assert.That(events[0].round,Is.EqualTo(game.State.RoundId));
            Assert.That(events[0].time,Is.EqualTo(interaction.StartSeconds));
        }
#endif
    }
}
