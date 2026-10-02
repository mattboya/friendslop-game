using System.Collections;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // MINI-1 on the live HUD: standing in the booth's ring, the minigame card counts the crew in and then the 3 s down; while the
    // game runs it says the festival is paused, the clock stands at the time left and E finishes the stub; the banner shows its
    // result for 4 s; then the card goes and the clock counts down again.
    public sealed class HudMinigamePlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TheCardCountsTheRingInTheClockStopsAndTheBannerShowsTheResult()
        {
            var world=new GameObject("Minigame HUD world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Minigame HUD session");var session=host.AddComponent<FestivalSession>();var hud=host.AddComponent<FestivalHud>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                yield return StartLevel(session);
                Text Find(string name){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
                var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
                for(float until=Time.realtimeSinceStartup+.3f;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(Find("Minigame title"),Is.Null,"setup: away from the booth there is no minigame card");

                // In the ring, the card counts the crew in, then the 3 s down.
                var at=FestivalSimulation.BoothAt(session.State);var me=sim.Player(session.LocalPlayerId);me.X=at.X+1;me.Z=at.Z;
                bool counted=false;deadline=Time.realtimeSinceStartup+6;
                while(session.State.Phase=="Playing"&&Time.realtimeSinceStartup<deadline){counted|=Find("Minigame title")?.text.StartsWith("MINIGAME IN ")??false;yield return null;}
                Assert.That(counted,Is.True,"the card counted down to the game");
                Assert.That(session.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"setup: the game starts: "+session.Message);
                for(float until=Time.realtimeSinceStartup+.3f;Time.realtimeSinceStartup<until;)yield return null;

                // While it runs: the card, the stopped clock and E.
                Assert.That(Find("Minigame title")?.text,Is.EqualTo("MINIGAME"),"the card says a game is on");
                Assert.That(Find("Minigame detail")?.text,Does.StartWith("The festival is paused while you play"));
                string clock=Find("Time")?.text;
                Assert.That(clock,Is.EqualTo(FestivalHudText.Clock(session.State)).And.Not.EqualTo("WAIT"),"the clock shows the time left, stopped");
                for(float until=Time.realtimeSinceStartup+1.2f;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(Find("Time")?.text,Is.EqualTo(clock),"and stays there");
                Assert.That(Find("Prompt")?.text,Is.EqualTo("FINISH THE GAME"),"E finishes the stub");
                ((System.Action)primary.GetValue(hud))();
                for(float until=Time.realtimeSinceStartup+.3f;Time.realtimeSinceStartup<until;)yield return null;

                // Its result, for 4 s, the world still stopped.
                Assert.That(Find("Minigame title")?.text,Is.EqualTo("+0 s"),"the banner shows the result: "+session.Message);
                Assert.That(Find("Minigame detail")?.text,Does.StartWith("The clock is unchanged."));
                Assert.That(Find("Prompt")==null||Find("Prompt").text=="",Is.True,"nothing more to press");
                deadline=Time.realtimeSinceStartup+5;
                while(session.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"then play resumes");
                for(float until=Time.realtimeSinceStartup+1.2f;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(Find("Minigame title"),Is.Null,"the card goes with the booth");
                Assert.That(Find("Time")?.text,Is.Not.EqualTo(clock),"and the clock counts down again");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }
    }
}
