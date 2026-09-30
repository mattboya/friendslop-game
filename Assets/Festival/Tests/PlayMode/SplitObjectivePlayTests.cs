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
    public sealed class SplitObjectivePlayTests
    {
        // CROWD-2: in a real hosted round of five, the host walks up to the second lost friend. They appear once their trail
        // ends, the objective card counts both friends, and E recruits them.
        [UnityTest]public IEnumerator ABigCrewSeesAndRecruitsTheSecondFriend()
        {
            var world=new GameObject("Split objective world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Split objective HUD");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
            // E runs the HUD's primary action (FestivalHud.UpdateKeyboard), the first action it lists.
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                session.Host("Tester",8575);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                // Four friends without clients of their own make a crew of five for Night 1.
                for(int i=0;i<4;i++)sim.AddPlayer("split_mate_"+i,"Mate "+i).Ready=true;
                sim.State.LevelIndex=1;player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)
                {
                    if(sim.State.Phase=="Loading")foreach(var mate in sim.State.Players)if(mate!=player&&!mate.MapReady)sim.Execute(mate.Id,new GameCommand{Id="loaded_"+mate.Id,Kind="MapReady"});
                    yield return null;
                }
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"round starts: "+session.Message);
                Assert.That(session.State.SecondFriend.Active,Is.True,"the host's view says a crew of five lost two friends");

                // An empty crowd, the mates waiting at camp, and the host right beside the second friend before their trail ends.
                var second=sim.State.SecondFriend;sim.State.Npcs.Clear();
                foreach(var mate in sim.State.Players)if(mate!=player){mate.X=Festivals.CampGateX;mate.Z=Festivals.CampGateZ;}
                player.X=second.Position.X+1;player.Z=second.Position.Z;
                yield return new WaitForSeconds(.6f);
                Assert.That(GameObject.Find("mission_friend_2"),Is.Null,"the second friend is not shown before their trail ends");
                Assert.That(Read("Prompt"),Is.Not.EqualTo("Recruit missing friend"),"nor offered for recruiting");
                Assert.That(Read("Objective title"),Is.EqualTo("TWO FRIENDS LOST • 0 / 2 FOUND"),"the objective card counts both friends");

                // The tripper follows the second trail to its end.
                second.CluesRead=second.ClueChain.Count;second.GateOpened=true;
                yield return new WaitForSeconds(.6f);
                var actor=GameObject.Find("mission_friend_2");
                Assert.That(actor,Is.Not.Null,"the second friend appears beside the host");
                Assert.That(Vector2.Distance(new Vector2(actor.transform.position.x,actor.transform.position.z),new Vector2(second.Position.X,second.Position.Z)),Is.LessThan(.5f),"where the second friend is lost");
                Assert.That(GameObject.Find("mission_friend"),Is.Null,"the first friend, far away down an unfinished trail, stays hidden");
                Assert.That(Read("Prompt"),Is.EqualTo("Recruit missing friend"),"the HUD offers to recruit them");
                ((System.Action)primary.GetValue(view))?.Invoke();yield return null;
                Assert.That(sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="FindFriend"&&i.Status=="Active"),Is.True,"E starts recruiting the second friend: "+session.Message);
                yield return new WaitForSeconds(2.6f);
                Assert.That(second.Found&&second.LeaderId==player.Id,Is.True,"the second friend is found and follows the host");
                Assert.That(session.State.SecondFriend.Found,Is.True,"and the host's view shows it");
                Assert.That(Read("Objective title"),Is.EqualTo("TWO FRIENDS LOST • 1 / 2 FOUND"),"one found, one to go");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
