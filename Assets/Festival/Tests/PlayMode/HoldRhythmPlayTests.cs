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
    public sealed class HoldRhythmPlayTests:FestivalPlayModeTest
    {
        private static readonly Key[] ArrowKeys={Key.LeftArrow,Key.DownArrow,Key.UpArrow,Key.RightArrow};
        private static readonly Key[] AlternateKeys={Key.A,Key.S,Key.W,Key.D};
        [UnityTest]public IEnumerator EffectiveHoldKeyUpReachesTheCalibratedHost()=>Edges("plain");
        [UnityTest]public IEnumerator AMenuStillDeliversKeyUpWhileSuppressingPresses()=>Edges("menu");
        [UnityTest]public IEnumerator AMapStillDeliversKeyUpWhileSuppressingPresses()=>Edges("map");
        [UnityTest]public IEnumerator FocusLossDropsAHeldNoteOnlyOnce()=>Edges("focus");
        [UnityTest]public IEnumerator RebindingCancelsTheOldHeldAction()=>Edges("rebind");
        [UnityTest]public IEnumerator AReboundAlternateKeyUsesTheEffectiveLaneAction()=>Edges("alternate");
        [UnityTest]public IEnumerator ASecondEffectiveKeyKeepsItsLaneHeldUntilBothAreUp()=>Edges("two_keys");
        [UnityTest]public IEnumerator HoldingUntilTheEndKeepsItsHeadCredit()=>Edges("end");
        [UnityTest]public IEnumerator ReleasesOutsideTheAuthoredHoldIntervalEmitNoCommand()=>Edges("outside");

        [UnityTest]public IEnumerator ADisabledSessionRetiresVisibleJudgmentsAndHoldViews()
        {
            var world=new GameObject("impl:U44 disabled world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("impl:U44 disabled HUD");var session=root.AddComponent<FestivalSession>();var hud=root.AddComponent<FestivalHud>();yield return null;
            session.Host("Owner",HostPort);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);game.State.Npcs.Clear();player.Effects.Clear();game.State.Doses.Clear();
            game.State.LevelIndex=3;player.X=0;player.Z=0;game.State.Npcs.Add(new NpcState{Id="disable_partner",X=0,Z=1.5f,CanTalk=true});
            session.Command("Dance","disable_partner");var interaction=game.Interaction(player.InteractionId);Assert.That(interaction,Is.Not.Null);
            hud.enabled=false;var chart=RhythmChart.For(interaction);var note=chart.Notes[0];
            game.State.SimulationSeconds=interaction.StartSeconds+note.TimeSeconds;session.Rhythm(note.Direction,note.TimeSeconds);
            Consume(session,hud,game,interaction,note.TimeSeconds);
            var lane=root.transform.Find("Festival HUD/Rhythm lane");Assert.That(lane.Find("Judgment").GetComponent<Text>().text,Is.EqualTo("PERFECT"));
            session.enabled=false;typeof(FestivalHud).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            Assert.That(lane.Find("Judgment").GetComponent<Text>().text,Is.Empty,"a disabled session cannot leave a stale rhythm verdict visible");
            foreach(Transform head in lane.Find("Notes"))Assert.That(head.gameObject.activeSelf,Is.False);
            foreach(Transform tail in lane.Find("Hold tails"))Assert.That(tail.gameObject.activeSelf,Is.False);
            session.enabled=true;session.Leave();typeof(FestivalHud).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            Assert.That(lane.gameObject.activeSelf,Is.False);
        }

        [UnityTest]public IEnumerator AJoinedOwnersKeyboardDropMatchesAuthorityWithoutLeakingInputs()
        {
            UseAVirtualKeyboard("impl:U44 joined keyboard");
            var world=new GameObject("impl:U44 paired world");world.AddComponent<FestivalWorld>();yield return null;
            var hostRoot=new GameObject("impl:U44 paired host");var host=hostRoot.AddComponent<FestivalSession>();yield return null;
            var ownerRoot=new GameObject("impl:U44 paired owner");var owner=ownerRoot.AddComponent<FestivalSession>();var hud=ownerRoot.AddComponent<FestivalHud>();yield return null;
            ushort port=FreePort();host.Host("Host",port);yield return null;Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);host.Controls.Map.Disable();
            owner.Join("Owner","127.0.0.1",port);
            for(float deadline=Time.realtimeSinceStartup+30;owner.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(owner.LocalPlayer,Is.Not.Null,owner.Message);hud.enabled=false;
            var game=Simulation(host);var player=game.Player(owner.LocalPlayerId);player.X=0;player.Z=19;owner.Command("Ready");
            for(float deadline=Time.realtimeSinceStartup+5;!player.Ready&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(player.Ready,Is.True,owner.Message);yield return StartLevel(host,owner.LocalPlayerId);
            game.State.LevelIndex=3;game.State.Npcs.Clear();game.State.Doses.Clear();player.Effects.Clear();player.X=0;player.Z=0;
            game.HasLineOfSight=(x,z,tx,tz)=>true;game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);
            game.State.Npcs.Add(new NpcState{Id="paired_partner",X=0,Z=1.5f,CanTalk=true});
            owner.Command("Dance","paired_partner");
            for(float deadline=Time.realtimeSinceStartup+5;player.InteractionId==""&&Time.realtimeSinceStartup<deadline;)yield return null;
            var interaction=game.Interaction(player.InteractionId);Assert.That(interaction,Is.Not.Null,owner.Message);
            RhythmChart chart=null;RhythmNote hold=null;
            for(int seed=0;seed<128&&hold==null;seed++){interaction.ChartSeed=seed;chart=RhythmChart.For(interaction);hold=chart.Notes.Find(note=>note.HoldSeconds>=.9);}
            Assert.That(hold,Is.Not.Null);owner.Profile.Data.TimingCalibrationMs=-80;
            game.State.SimulationSeconds=interaction.StartSeconds+hold.TimeSeconds-.08;
            for(float deadline=Time.realtimeSinceStartup+5;owner.State.Interactions.Find(i=>i.Id==interaction.Id)?.ChartSeed!=interaction.ChartSeed&&Time.realtimeSinceStartup<deadline;)yield return null;
            InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(ArrowKeys[hold.Direction]));InputSystem.Update();
            Consume(owner,hud,game,interaction,hold.TimeSeconds-.08);
            for(float deadline=Time.realtimeSinceStartup+5;interaction.Inputs.Count<1&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(interaction.Inputs.Count,Is.EqualTo(1),owner.Message);Assert.That(interaction.Inputs[0].TimeSeconds,Is.EqualTo(hold.TimeSeconds).Within(.00001));
            int steps=player.VisualDanceStepSequence;
            InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());InputSystem.Update();Consume(owner,hud,game,interaction,hold.TimeSeconds+.2-.08);
            for(float deadline=Time.realtimeSinceStartup+5;interaction.Inputs.Count<2&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(interaction.Inputs.Count,Is.EqualTo(2),owner.Message);Assert.That(interaction.Inputs[1].Release,Is.True);Assert.That(player.VisualDanceStepSequence,Is.EqualTo(steps));
            for(float deadline=Time.realtimeSinceStartup+5;owner.State.Interactions.Find(i=>i.Id==interaction.Id)?.Inputs.Count<2&&Time.realtimeSinceStartup<deadline;)yield return null;
            // Only the real received owner snapshot supplies visual feedback; no host view is injected here.
            typeof(FestivalHud).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            var lane=ownerRoot.transform.Find("Festival HUD/Rhythm lane");Assert.That(lane.Find("Judgment").GetComponent<Text>().text,Is.EqualTo("LET GO"));
            Assert.That(lane.Find("Hold tails/Tail "+chart.Notes.IndexOf(hold)).GetComponent<Image>().color.r,Is.EqualTo(.55f));
            Assert.That(host.State.Interactions,Is.Empty,"a friend's private hold input log never reaches the host player's view");
            var replay=new RhythmJudge(chart,interaction.GoodWindowSeconds);foreach(var input in interaction.Inputs){if(input.Release)replay.Release(input.Direction,input.TimeSeconds);else replay.Submit(input.Direction,input.TimeSeconds);}
            Assert.That(replay.Drops,Is.EqualTo(1));Assert.That(replay.Score,Is.Zero);
        }

        [UnityTest]public IEnumerator ReconnectRebuildsAChartEvenWhenItsIdentityAndEmptyLogRepeat()
        {
            var world=new GameObject("impl:U44 reconnect world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("impl:U44 reconnect HUD");var session=root.AddComponent<FestivalSession>();var hud=root.AddComponent<FestivalHud>();yield return null;
            session.Host("Owner",HostPort);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);hud.enabled=false;
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);game.State.Npcs.Clear();player.Effects.Clear();game.State.Doses.Clear();player.X=0;player.Z=0;
            game.State.Npcs.Add(new NpcState{Id="reconnect_partner",X=0,Z=1.5f,CanTalk=true});session.Command("Dance","reconnect_partner");
            var interaction=game.Interaction(player.InteractionId);Assert.That(interaction,Is.Not.Null);var chart=RhythmChart.For(interaction);
            string identity=interaction.Id;int seed=interaction.ChartSeed;
            Consume(session,hud,game,interaction,chart.DurationSeconds-.1);
            var lane=root.transform.Find("Festival HUD/Rhythm lane");Assert.That(lane.Find("Notes/Note 0").gameObject.activeSelf,Is.False,"the empty log has already missed this head");
            session.Leave();yield return null;yield return null;session.Host("Owner",HostPort);yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
            game=Simulation(session);player=game.Player(session.LocalPlayerId);game.State.Npcs.Clear();player.Effects.Clear();game.State.Doses.Clear();player.X=0;player.Z=0;
            game.State.Npcs.Add(new NpcState{Id="reconnect_partner",X=0,Z=1.5f,CanTalk=true});session.Command("Dance","reconnect_partner");
            interaction=game.Interaction(player.InteractionId);Assert.That(interaction,Is.Not.Null);player.InteractionId=interaction.Id=identity;interaction.ChartSeed=seed;
            Consume(session,hud,game,interaction,1);
            Assert.That(lane.Find("Notes/Note 0").gameObject.activeSelf,Is.True,"a real Leave/rehost cannot carry consumed heads into a repeated chart identity");
            Assert.That(lane.Find("Judgment").GetComponent<Text>().text,Is.Empty);
        }

        private static void Consume(FestivalSession session,FestivalHud hud,FestivalSimulation game,InteractionState interaction,double raw)
        {
            game.State.SimulationSeconds=interaction.StartSeconds+raw;
            if(session.IsHost)typeof(FestivalSession).GetProperty("State").SetValue(session,FestivalSession.ViewFor(game,session.LocalPlayerId));
            else session.State.SimulationSeconds=game.State.SimulationSeconds;
            var manager=typeof(FestivalSession).GetField("manager",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
            var clock=manager.GetType().GetProperty("ServerTime").GetValue(manager);
            typeof(FestivalSession).GetField("serverClockAtSnapshot",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,clock.GetType().GetProperty("Time").GetValue(clock));
            typeof(FestivalHud).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
        }

        private IEnumerator Edges(string mode)
        {
            UseAVirtualKeyboard("impl:U44 hold keyboard");
            var world=new GameObject("impl:U44 hold world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("impl:U44 hold HUD");var session=root.AddComponent<FestivalSession>();var hud=root.AddComponent<FestivalHud>();yield return null;
            session.Host("Hold tester",HostPort);
            for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);game.State.Npcs.Clear();game.State.Doses.Clear();player.Effects.Clear();
            game.HasLineOfSight=(x,z,tx,tz)=>true;game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);
            player.X=0;player.Z=0;game.State.LevelIndex=3;
            var partner=new NpcState{Id="hold_partner",X=0,Z=1.5f,CanTalk=true};game.State.Npcs.Add(partner);session.Command("Dance",partner.Id);
            var interaction=game.Interaction(player.InteractionId);Assert.That(interaction,Is.Not.Null,session.Message);
            RhythmChart chart=null;RhythmNote hold=null;
            for(int seed=0;seed<128&&hold==null;seed++)
            {
                interaction.ChartSeed=seed;chart=RhythmChart.For(interaction);hold=chart.Notes.Find(note=>note.HoldSeconds>=.9);
            }
            Assert.That(hold,Is.Not.Null,"a bounded production search supplies an authored long hold");
            hud.enabled=false;session.Profile.Data.TimingCalibrationMs=60;
            void Consume(double raw)=>HoldRhythmPlayTests.Consume(session,hud,game,interaction,raw);
            Key key=ArrowKeys[hold.Direction];
            if(mode=="alternate"){session.Controls.Notes[hold.Direction].ApplyBindingOverride(1,"<Keyboard>/j");key=Key.J;}
            InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(key));InputSystem.Update();Consume(mode=="outside"?hold.TimeSeconds-.4:hold.TimeSeconds+.06);yield return null;
            Assert.That(interaction.Inputs.Count,Is.EqualTo(1),"one actual effective action produces one press: "+session.Message);
            Assert.That(interaction.Inputs[0].Release,Is.False);Assert.That(interaction.Inputs[0].Direction,Is.EqualTo(hold.Direction));
            Assert.That(interaction.Inputs[0].TimeSeconds,Is.EqualTo(mode=="outside"?hold.TimeSeconds-.46:hold.TimeSeconds).Within(.00001),"the HUD sends raw time through the session's one calibration");
            if(mode=="outside")
            {
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());InputSystem.Update();Consume(hold.TimeSeconds-.2);
                Assert.That(interaction.Inputs.Count,Is.EqualTo(1),"releasing before the calibrated head window emits nothing");yield return null;
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(key));InputSystem.Update();Consume(hold.TimeSeconds+hold.HoldSeconds+.2);yield return null;
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());InputSystem.Update();Consume(hold.TimeSeconds+hold.HoldSeconds+.3);
                Assert.That(interaction.Inputs.FindAll(input=>input.Release),Is.Empty,"releasing after the authored tail end emits nothing");yield break;
            }
            if(mode=="two_keys")
            {
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(key,AlternateKeys[hold.Direction]));InputSystem.Update();Consume(hold.TimeSeconds+.14);yield return null;
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(AlternateKeys[hold.Direction]));InputSystem.Update();Consume(hold.TimeSeconds+.17);
                Assert.That(interaction.Inputs.Count,Is.EqualTo(1),"the still-held alternative keeps the effective lane pressed");yield return null;
            }
            int steps=player.VisualDanceStepSequence;
            Consume(hold.TimeSeconds+.18);yield return null;
            if(mode=="menu")session.MenuOpen=true;
            if(mode=="map")
            {
                InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState(key,Key.Tab));InputSystem.Update();Consume(hold.TimeSeconds+.2);yield return null;
            }
            if(mode=="focus")hud.SendMessage("OnApplicationFocus",false,SendMessageOptions.DontRequireReceiver);
            else if(mode=="rebind")session.Controls.Rebind(session.Controls.Notes[hold.Direction],0,null);
            else{InputSystem.QueueStateEvent(TestKeyboard,mode=="map"?new KeyboardState(Key.Tab):new KeyboardState());InputSystem.Update();}
            Consume(mode=="end"?hold.TimeSeconds+hold.HoldSeconds-.04:hold.TimeSeconds+.3);
            Assert.That(interaction.Inputs.Count,Is.EqualTo(2),mode+": the canceled or released effective hold action reaches authority");
            Assert.That(interaction.Inputs[1].Release,Is.True);Assert.That(interaction.Inputs[1].Direction,Is.EqualTo(hold.Direction));
            Assert.That(player.VisualDanceStepSequence,Is.EqualTo(steps),"a release never animates another head");
            Consume(mode=="end"?hold.TimeSeconds+hold.HoldSeconds-.02:hold.TimeSeconds+.32);
            var lane=root.transform.Find("Festival HUD/Rhythm lane");
            if(mode=="end")Assert.That(lane.Find("Judgment").GetComponent<Text>().text,Is.Not.EqualTo("LET GO"),"unplayed other notes can miss, but the completed hold cannot drop");
            else Assert.That(lane.Find("Judgment").GetComponent<Text>().text,Is.EqualTo("LET GO"),"the owner's real snapshot replays the host hold outcome");
            var replay=new RhythmJudge(chart,interaction.GoodWindowSeconds);
            foreach(var input in interaction.Inputs){if(input.Release)replay.Release(input.Direction,input.TimeSeconds);else replay.Submit(input.Direction,input.TimeSeconds);}
            Assert.That(replay.Drops,Is.EqualTo(mode=="end"?0:1));
            Assert.That(replay.Score,Is.EqualTo(mode=="end"?1.0/chart.Notes.Count:0).Within(.00001),"the host retains end-held credit and loses an early-dropped head");
            var tail=lane.Find("Hold tails/Tail "+chart.Notes.IndexOf(hold)).GetComponent<Image>();
            if(mode=="end")Assert.That(tail.color.r,Is.Not.EqualTo(.55f));else Assert.That(tail.color.r,Is.EqualTo(.55f));
            if(mode=="menu"||mode=="map"||mode=="focus")
            {
                var other=ArrowKeys[(hold.Direction+1)%4];
                InputSystem.QueueStateEvent(TestKeyboard,mode=="map"?new KeyboardState(other,Key.Tab):new KeyboardState(other));InputSystem.Update();Consume(hold.TimeSeconds+.33);
                Assert.That(interaction.Inputs.FindAll(input=>!input.Release).Count,Is.EqualTo(1),"covered or unfocused gameplay suppresses new lane presses");
            }
            if(mode=="rebind")session.Controls.CancelRebind();
            if(mode=="focus")hud.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);
            InputSystem.QueueStateEvent(TestKeyboard,new KeyboardState());InputSystem.Update();Consume(mode=="end"?hold.TimeSeconds+hold.HoldSeconds+.02:hold.TimeSeconds+.34);
            Assert.That(interaction.Inputs.FindAll(input=>input.Release).Count,Is.EqualTo(1),"physical key-up after cancellation cannot send a second release");
            session.MenuOpen=false;session.Command("Cancel");yield return null;yield return null;
            session.Command("Dance",partner.Id);var tap=game.Interaction(player.InteractionId);Assert.That(tap,Is.Not.Null);
            tap.ChartDifficulty=RhythmChart.QuarterNotes;game.State.SimulationSeconds=tap.StartSeconds+2;yield return null;
            hud.enabled=true;yield return Tap(Key.RightArrow);
            Assert.That(tap.Inputs.FindAll(input=>input.Release),Is.Empty,"tap-only key-ups create no rhythm command");
        }
    }
}
