using System.Collections;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // DIALOG-6: the HUD (and the one-line chat its dialogue card keeps up, FestivalTalkLine) outlives a game. Leave, then Host again
    // in the same app run, and the new game's clock starts again near 0: the last game's line must not read as still said there.
    // From the V9 review's reproduction.
    public sealed class TalkLinePlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator ALineFromALeftGameNeverShowsInTheNextOne()
        {
            var world=new GameObject("Talk line world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Talk line HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            string Read(string name){foreach(var t in hud.GetComponentsInChildren<Text>(false))if(t.name==name)return t.text;return "(hidden)";}
            string spoke="";var failures=new List<string>();
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"setup: host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                yield return StartLevel(session);
                // Ten minutes into a level, a festivalgoer says their line, and it goes once the host drops it.
                sim.State.SimulationSeconds+=600;
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook"&&n.Twist=="");
                npc.X=player.X+.6f;npc.Z=player.Z;npc.Mode="Blending";npc.Suspicion=0;npc.CanTalk=true;npc.LastTalkSeconds=-10;
                session.Command("Talk",npc.Id);spoke=player.NpcSpeech;
                Assert.That(spoke,Is.Not.Empty,"setup: they talk: "+session.Message);
                yield return new WaitForSeconds(.5f);
                Assert.That(Read("Dialogue line"),Is.EqualTo(spoke),"setup: their line is up");
                float wait=Time.realtimeSinceStartup+15;
                while(sim.State.SimulationSeconds<player.NpcSpeechUntil+.5&&Time.realtimeSinceStartup<wait)yield return null;
                yield return new WaitForSeconds(.2f);
                Assert.That(Read("Dialogue line"),Is.EqualTo("(hidden)"),"setup: their line is gone once the host drops it");
                // Leave, and host a new game in the same app run.
                session.Leave();yield return null;yield return null;yield return null;
                session.Host("Tester",HostPort);
                deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"setup: the new game has a local player: "+session.Message);
                for(int i=0;i<4;i++)
                {
                    yield return new WaitForSeconds(.5f);
                    string shown=Read("Dialogue line");
                    if(shown!="(hidden)")failures.Add("the new game's "+session.State.Phase+" at "+session.EstimatedSimulationSeconds.ToString("0.0")+" s shows \""+shown+"\"");
                }
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"the last game's line (\""+spoke+"\") in the next one:\n"+string.Join("\n",failures));
        }
    }
}
