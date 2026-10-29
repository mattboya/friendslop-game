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
    // game runs the clock stands at the time left; the banner shows its result for 4 s; then the card goes and the clock counts down
    // again. MINI-4: the game is Mystery Cups. The card shows the pot and your turn; looking at a safe cup, E drinks it and the pot
    // grows; F calls it, and the banner shows what the pot paid.
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
                yield return StartLevel(session);sim.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;
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

                Assert.That(Find("Minigame title")?.text,Is.EqualTo("MYSTERY CUPS"),"the card names the game");
                Assert.That(Find("Minigame detail")?.text,Does.StartWith("One of these 4 cups is spiked."),"and reads its rules");
                for(float until=Time.realtimeSinceStartup+(float)FestivalSimulation.CupsIntroSeconds+2;sim.State.Booth.Seconds<sim.State.Booth.TurnAt&&Time.realtimeSinceStartup<until;)yield return null;
                yield return null;yield return null;
                // While it runs: the cups' card, the stopped clock and your turn's keys.
                Assert.That(Find("Minigame title")?.text,Is.EqualTo("MYSTERY CUPS  •  POT $0"),"the card shows the game and its pot");
                Assert.That(Find("Minigame detail")?.text,Does.StartWith("Your turn: ").And.Contains("Drink a cup for $5, or call it and bank $0."),"and that you are up");
                string clock=Find("Time")?.text;
                Assert.That(clock,Is.EqualTo(FestivalHudText.Clock(session.State)).And.Not.EqualTo("WAIT"),"the clock shows the time left, stopped");
                for(float until=Time.realtimeSinceStartup+1.2f;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(Find("Time")?.text,Is.EqualTo(clock),"and stays there");
                Assert.That(Find("Prompt")?.text,Does.EndWith("F CALL IT"),"F calls it");

                // Look at a safe cup (the host knows which): the prompt offers it, and E drinks it.
                var b=sim.State.Booth;int safe=b.Spiked==0?1:0;var cupAt=FestivalMinigameView.CupAt(at,safe,b.Cups);
                var yaw=typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance);var pitch=typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance);
                for(int look=0;look<8&&FestivalMinigameView.AimedCup(session.State,FestivalCharacter.ViewTransform)!=safe;look++)
                {
                    var toward=cupAt-FestivalCharacter.ViewTransform.position;
                    yaw.SetValue(session,Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg);pitch.SetValue(session,-Mathf.Atan2(toward.y,new Vector2(toward.x,toward.z).magnitude)*Mathf.Rad2Deg);
                    for(float until=Time.realtimeSinceStartup+.4f;Time.realtimeSinceStartup<until;)yield return null;
                }
                Assert.That(FestivalMinigameView.AimedCup(session.State,FestivalCharacter.ViewTransform),Is.EqualTo(safe),"setup: the view looks at cup "+(safe+1));
                for(float until=Time.realtimeSinceStartup+.3f;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(Find("Prompt")?.text,Is.EqualTo("DRINK CUP "+(safe+1)+"   •   F CALL IT"),"the prompt offers the cup looked at");
                ((System.Action)primary.GetValue(hud))();
                for(float until=Time.realtimeSinceStartup+.4f;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(session.State.Booth.Drunk,Is.EqualTo(new[]{safe}),"E drank it: "+session.Message);
                Assert.That(Find("Minigame title")?.text,Is.EqualTo("MYSTERY CUPS  •  POT $5"),"and the pot grew");
                session.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.CallCups);
                for(float until=Time.realtimeSinceStartup+.3f;Time.realtimeSinceStartup<until;)yield return null;

                // Its result, for 4 s, the world still stopped.
                Assert.That(Find("Minigame title")?.text,Is.EqualTo("+$5"),"the banner shows what the pot paid: "+session.Message);
                Assert.That(Find("Minigame detail")?.text,Does.StartWith("Called it: $5 to you, counted as sales."),"alone at the table, the pot paid you");
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
