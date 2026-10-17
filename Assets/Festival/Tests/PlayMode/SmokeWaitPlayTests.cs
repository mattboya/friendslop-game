using System.Collections;
using System.Reflection;
using System.Threading;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // The native smokes' gameplay steps, run by the smoke's own coroutines on a real hosted session.
    public sealed class SmokeWaitPlayTests:FestivalPlayModeTest
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        // A loaded machine: every frame takes .4 real seconds, and the host's per-frame cap gives the simulation only .2 of them.
        sealed class LoadedFrames:MonoBehaviour{void Update()=>Thread.Sleep(400);}

        static IEnumerator Hosted(FestivalSession session)
        {
            session.Host("Smoke waiter",HostPort);
            for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
        }
        IEnumerator HostALevel(FestivalSession session)
        {
            yield return Hosted(session);
            yield return StartLevel(session);
            var game=Simulation(session);game.State.Npcs.Clear();game.State.DurationSeconds=100000;
        }
        static string Failed(DevelopmentSmoke smoke)=>(string)typeof(DevelopmentSmoke).GetField("failed",Private).GetValue(smoke);

        [UnityTest]public IEnumerator ASoloSelfRevivalOnLoadedFramesGetsItsFullFifteenSimulationSeconds()
        {
            var world=new GameObject("impl:U74 smoke wait world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 smoke wait session");var session=holder.AddComponent<FestivalSession>();yield return null;
            GameObject load=null;
            try
            {
                yield return HostALevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
                var smoke=holder.AddComponent<DevelopmentSmoke>();smoke.enabled=false;
                load=new GameObject("impl:U74 loaded frames",typeof(LoadedFrames));
                double real=Time.realtimeSinceStartupAsDouble,simulation=game.State.SimulationSeconds;
                yield return (IEnumerator)typeof(DevelopmentSmoke).GetMethod("SoloRevival",Private).Invoke(smoke,new object[]{session});
                real=Time.realtimeSinceStartupAsDouble-real;simulation=game.State.SimulationSeconds-simulation;
                string failed=Failed(smoke);
                TestContext.WriteLine("impl:U74 SOLO_REVIVAL real="+real.ToString("F2")+" sim="+simulation.ToString("F2")+" failed="+failed+" life="+player.Life+" revivals="+player.RevivalCount);
                Assert.That(real,Is.GreaterThan(1.5*simulation),"setup: loaded frames spend real time faster than simulation time");
                Assert.That(failed,Is.Null,"the revival's wait counts simulation seconds from the host's acceptance");
                Assert.That(player.Life,Is.EqualTo("Alive"));Assert.That(player.RevivalCount,Is.EqualTo(1));
            }
            finally{if(load!=null)Object.Destroy(load);session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }

        [UnityTest]public IEnumerator ACommandTheHostRefusesFailsTheStepByName()
        {
            var world=new GameObject("impl:U74 smoke refusal world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 smoke refusal session");var session=holder.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return HostALevel(session);
                var player=Simulation(session).Player(session.LocalPlayerId);player.X=0;player.Z=0;
                var smoke=holder.AddComponent<DevelopmentSmoke>();smoke.enabled=false;yield return null;
                yield return (IEnumerator)typeof(DevelopmentSmoke).GetMethod("Interact",Private).Invoke(smoke,new object[]{session,"Extract","","",float.PositiveInfinity});
                string failed=Failed(smoke);var finished=typeof(DevelopmentSmoke).GetField("finished",Private).GetValue(smoke);
                TestContext.WriteLine("impl:U74 REFUSED failed="+failed+" message="+session.Message);
                Assert.That(failed,Does.StartWith("Extract refused: "),"an extraction away from the gate never starts, and the step says so");
                Assert.That(failed.Length,Is.GreaterThan("Extract refused: ".Length),"the host's reason is kept");
                Assert.That(finished,Is.Null);
            }
            finally{session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }

        // A festivalgoer who talks without asking for a dance first, standing in front of the hosted player.
        static NpcState Talker(FestivalSimulation game,PlayerState player)
        {
            string id="smoke_talker_0";for(int n=1;DialogueGrammar.PersonaFor(id,"")!="Wook";n++)id="smoke_talker_"+n;
            player.X=0;player.Z=0;var npc=new NpcState{Id=id,Kind="Wook",Z=1.5f,Yaw=180,CanTalk=true};game.State.Npcs.Add(npc);return npc;
        }

        [UnityTest]public IEnumerator TheSettingsCheckWalksEveryTabOfTheRealPass()
        {
            var world=new GameObject("impl:U74 settings world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 settings session");var session=holder.AddComponent<FestivalSession>();holder.AddComponent<FestivalHud>();yield return null;
            try
            {
                yield return Hosted(session);
                session.MenuOpen=true;yield return null;
                var pass=holder.transform.Find("Festival HUD/Festival pass");
                pass.Find("Pass home/SETTINGS").GetComponent<Button>().onClick.Invoke();yield return null;yield return null;
                var settings=pass.Find("Pass settings");
                var smoke=holder.AddComponent<DevelopmentSmoke>();smoke.enabled=false;
                yield return (IEnumerator)typeof(DevelopmentSmoke).GetMethod("CheckSettingsTabs",Private).Invoke(smoke,new object[]{settings});
                Assert.That(Failed(smoke),Is.Null,"every tab and CONTROLS section shows its own rows on the real pass");
                yield return null;
                Assert.That(DevelopmentSmoke.SettingsRowsWrong(settings,FestivalSettingsScreen.Tabs[0],""),Is.Null,"the check leaves the first tab open");
                settings.Find("LOOK SPEED").gameObject.SetActive(true);
                Assert.That(DevelopmentSmoke.SettingsRowsWrong(settings,FestivalSettingsScreen.Tabs[0],""),Is.EqualTo("LOOK SPEED shown"),"a row from another tab is caught");
            }
            finally{session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }

        [UnityTest]public IEnumerator TheStarterChecksUseTheRealCampShelves()
        {
            var world=new GameObject("impl:U74 starter world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 starter session");var session=holder.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return Hosted(session);yield return null;yield return null;
                Assert.That(DevelopmentSmoke.StarterShelfWrong(world.transform,session.State),Is.Null,"a fresh camp shows both starters, and the market none");
                var player=Simulation(session).Player(session.LocalPlayerId);
                string bought=(string)typeof(DevelopmentSmoke).GetMethod("BuyOneStarter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{session});
                Assert.That(bought,Is.Null,"the smoke's starter step passes");
                Assert.That(player.Inventory.Find(i=>i.ItemId=="stock_lsd")?.Count??0,Is.EqualTo(1),"one starter Tongue Stamp bought at camp");
                Assert.That(player.HeldOfferId,Is.Empty,"the second starter is never picked up");
                var copies=world.transform.Find(FestivalWorld.CampRootName+"/Campsite shelf goods/Shop item stock_mushrooms/Individual shelf copies");
                foreach(Transform copy in copies)copy.gameObject.SetActive(false);
                Assert.That(DevelopmentSmoke.StarterShelfWrong(world.transform,session.State),Is.EqualTo("stock_mushrooms missing from the camp shelf"));
            }
            finally{session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }

        [UnityTest]public IEnumerator TheTalkCheckSeesTheirLineTypeOutInABubble()
        {
            var world=new GameObject("impl:U74 typing world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 typing session");var session=holder.AddComponent<FestivalSession>();holder.AddComponent<FestivalHud>();yield return null;
            try
            {
                yield return HostALevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);var npc=Talker(game,player);
                session.MenuOpen=false;session.Command("Conversation",npc.Id);
                var talk=game.Interaction(player.InteractionId);
                Assert.That(talk?.Kind=="Conversation"&&!FestivalSimulation.TrustDancing(talk),Is.True,"setup: a talk with no dance first: "+session.Message);
                var typedOut=typeof(DevelopmentSmoke).GetMethod("SpeechTypedOut",Private);
                var smoke=holder.AddComponent<DevelopmentSmoke>();smoke.enabled=false;
                yield return (IEnumerator)typedOut.Invoke(smoke,new object[]{session,npc.Id,talk.Turns[0].Line});
                Assert.That(Failed(smoke),Is.Null,"their first line types out in a bubble drawn with the bubble text shader");
                var unsaid=holder.AddComponent<DevelopmentSmoke>();unsaid.enabled=false;
                yield return (IEnumerator)typedOut.Invoke(unsaid,new object[]{session,npc.Id,"A line nobody says."});
                Assert.That(Failed(unsaid),Does.StartWith("speech types out ("),"a line that never shows fails the step");
            }
            finally{session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }

        [UnityTest]public IEnumerator TheStageMusicCheckHearsTheLevelsRecording()
        {
            var world=new GameObject("impl:U74 stage music world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 stage music session");var session=holder.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return HostALevel(session);
                var heard=typeof(DevelopmentSmoke).GetMethod("StageMusicPlays",Private);
                var smoke=holder.AddComponent<DevelopmentSmoke>();smoke.enabled=false;
                yield return (IEnumerator)heard.Invoke(smoke,new object[]{session});
                Assert.That(Failed(smoke),Is.Null,"Day 1 plays its stage recording");
                Object.DestroyImmediate(holder.GetComponent<FestivalStageAudio>());
                var silent=holder.AddComponent<DevelopmentSmoke>();silent.enabled=false;
                yield return (IEnumerator)heard.Invoke(silent,new object[]{session});
                Assert.That(Failed(silent),Does.StartWith("stage music: "),"a silent stage fails the step");
            }
            finally{session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }

        [UnityTest]public IEnumerator TheTailCheckReadsTheRealRhythmLane()
        {
            var world=new GameObject("impl:U74 tails world");world.AddComponent<FestivalWorld>();yield return null;
            var holder=new GameObject("impl:U74 tails session");var session=holder.AddComponent<FestivalSession>();holder.AddComponent<FestivalHud>();yield return null;
            try
            {
                yield return HostALevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);var npc=Talker(game,player);
                session.MenuOpen=false;session.Command("Dance",npc.Id);
                var dance=game.Interaction(player.InteractionId);Assert.That(dance?.Kind,Is.EqualTo("Dance"),"setup: the dance starts: "+session.Message);
                var lane=holder.transform.Find("Festival HUD/Rhythm lane");
                for(float deadline=Time.realtimeSinceStartup+5;!lane.gameObject.activeInHierarchy&&Time.realtimeSinceStartup<deadline;)yield return null;
                yield return null;
                var chart=RhythmChart.For(dance);
                Assert.That(chart.Notes.Exists(n=>n.HoldSeconds>0),Is.False,"setup: Day 1 never holds");
                Assert.That(DevelopmentSmoke.HoldTailsWrong(lane,chart),Is.Null,"a Day 1 dance draws no tail, and the tails sit under the arrows");
                lane.Find("Hold tails").GetChild(0).gameObject.SetActive(true);
                Assert.That(DevelopmentSmoke.HoldTailsWrong(lane,chart),Is.EqualTo("a tail drawn on a chart without holds"));
            }
            finally{session.Leave();Object.Destroy(holder);Object.Destroy(world);}
        }
    }
}
