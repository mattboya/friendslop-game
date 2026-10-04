using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class ChatSessionPlayTests:FestivalPlayModeTest
    {
        [UnityTest] public IEnumerator AJoinedClientCanEndOnlyItsOwnChatCheck()
        {
            var world=new GameObject("Chat session world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Chat session host");var host=hostObject.AddComponent<FestivalSession>();
            var clientObject=new GameObject("Chat session client");var client=clientObject.AddComponent<FestivalSession>();yield return null;
            try
            {
                ushort port=FreePort();host.Host("Host",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);client.Join("Client","127.0.0.1",port);
                while(client.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(client.LocalPlayer,Is.Not.Null,client.Message);
                var sim=Simulation(host);var player=sim.Player(client.LocalPlayerId);player.X=0;player.Z=19;client.Command("Ready");
                while(!player.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(player.Ready,Is.True,"client's Ready reached the authority");yield return StartLevel(host,client.LocalPlayerId);
                sim.State.TripperId=player.Id;sim.State.Npcs.Clear();sim.State.Visions.Clear();
                player.X=0;player.Z=0;sim.State.Npcs.Add(new NpcState{Id="check_target",Role="Regular",CanTalk=true,Z=1,Yaw=180});
                sim.State.Visions.Add(new VisionState{Id="private_vision",NpcId="check_target",Kind="Buyer",IsTrue=false});
                client.MenuOpen=false;host.MenuOpen=false;
                client.Command("ConfirmChat","check_target");deadline=Time.realtimeSinceStartup+10;
                while(player.InteractionId==""&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(player.InteractionId,Is.Not.Empty,"client started its check: "+client.Message);var chat=sim.Interaction(player.InteractionId);
                sim.Tick(60);yield return null;
                Assert.That(chat.Status,Is.EqualTo("Active"),"the check remains active after 60 simulation seconds");
                host.Command("EndChat",chat.Id);yield return null;
                Assert.That(chat.Status,Is.EqualTo("Active"),"the host's player cannot end someone else's check");
                client.Command("EndChat",chat.Id);deadline=Time.realtimeSinceStartup+10;
                while((chat.Status=="Active"||client.State.Visions.Find(v=>v.Id=="private_vision")?.Confirmed!=true)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(chat.Status,Is.EqualTo("Complete"),"the client's command ends its check: "+client.Message);
                Assert.That(client.State.Visions.Find(v=>v.Id=="private_vision")?.Confirmed,Is.True,"the owner receives its verdict");
                Assert.That(host.State.Visions,Is.Empty,"a joined player's truth stays private");
                Assert.That(client.MenuOpen,Is.False,"ending via command leaves the menu alone");
                client.Command("EndChat",chat.Id);yield return null;
                Assert.That(chat.Status,Is.EqualTo("Complete"),"a stale command cannot run completion again");
            }
            finally{client.Leave();host.Leave();Object.Destroy(clientObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
        }
    }
}
