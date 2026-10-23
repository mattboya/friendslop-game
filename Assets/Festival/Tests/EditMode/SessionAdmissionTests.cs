using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Festival.Core;
using Festival.Network;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // The rules live in static helpers and the private Approve, so these cases need no port, transport or joined client.
    public sealed class SessionAdmissionTests
    {
        static readonly Type PeerInput=typeof(FestivalSession).Assembly.GetType("Festival.Network.PeerInput");
        static MethodInfo Helper(string name)
        {
            var method=typeof(FestivalSession).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null,"FestivalSession."+name+" is the host's static rule");return method;
        }

        [Test] public void MovesNoLongerEatTheCommandBudget()
        {
            var admit=Helper("Admit");var peer=Activator.CreateInstance(PeerInput);
            bool Admit(bool move,double now)=>(bool)admit.Invoke(null,new object[]{peer,move,now});
            for(int k=0;k<20;k++)Assert.That(Admit(true,10+k*.04),Is.True,"setup: move "+(k+1)+", at the client's 20 a second");
            for(int k=1;k<=60;k++)Assert.That(Admit(false,10.8),Is.True,"command "+k+" of 60 in the same second");
            Assert.That(Admit(false,10.8),Is.False,"the 61st command in that second is refused");
            for(int k=21;k<=90;k++)Assert.That(Admit(true,10.8),Is.True,"move "+k+" of 90 in that second");
            Assert.That(Admit(true,10.8),Is.False,"the 91st move in that second is refused");
            Assert.That(Admit(true,11),Is.True,"the next second starts the move budget over");
            Assert.That(Admit(false,11.8),Is.True,"and the command budget");
        }

        [Test] public void OnlyAConnectionsFirstHistorySeedCounts()
        {
            var seed=Helper("SeedHistory");var peer=Activator.CreateInstance(PeerInput);var player=new PlayerState();
            var packetType=typeof(FestivalSession).Assembly.GetType("Festival.Network.DialogueHistoryPacket");
            object Packet(params string[] keys){var packet=Activator.CreateInstance(packetType);packetType.GetField("Seen").SetValue(packet,keys.ToList());return packet;}
            Assert.That(seed.Invoke(null,new[]{peer,player,Packet("first_key")}),Is.True,"the seed sent on connect merges");
            Assert.That(player.DialogueRevision,Is.EqualTo(1),"setup: one new revision to publish back");
            Assert.That(seed.Invoke(null,new[]{peer,player,Packet("second_key")}),Is.False,"a second seed on the same connection is ignored");
            Assert.That(player.DialogueRevision,Is.EqualTo(1),"so the host has no new revision to publish back (PublishAcknowledgedHistory sends on a revision change only)");
            Assert.That(player.Dialogue.Seen,Is.EqualTo(new[]{"first_key"}));
            Assert.That(seed.Invoke(null,new[]{Activator.CreateInstance(PeerInput),player,Packet("second_key")}),Is.True,"a new connection seeds once more");
        }

        [Test] public void ASecondJoinWithAPendingJoinsTokenIsRefused()
        {
            var root=new GameObject("Session admission host");
            try
            {
                var session=root.AddComponent<FestivalSession>();var game=new FestivalSimulation(1);game.AddPlayer("returning","Returning");
                typeof(FestivalSession).GetField("simulation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,game);
                ((Dictionary<string,string>)typeof(FestivalSession).GetField("tokens",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session))["token_a"]="returning";
                Assert.That(Approve(session,1,"token_a"),Is.Empty,"the first join with a known token gets in and waits to connect");
                Assert.That(Approve(session,2,"token_a"),Is.EqualTo("This session identity is already connected."),"a second join with that token is refused while the first waits");
                Assert.That(Approve(session,3,""),Is.Empty,"a new player still gets in");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        // The host's real approval of a join with this token: "" when let in, otherwise the reason it gives.
        static string Approve(FestivalSession session,ulong client,string token)
        {
            var approve=typeof(FestivalSession).GetMethod("Approve",BindingFlags.Instance|BindingFlags.NonPublic);var parameters=approve.GetParameters();
            Type requestType=parameters[0].ParameterType,responseType=parameters[1].ParameterType;
            object request=Activator.CreateInstance(requestType),response=Activator.CreateInstance(responseType);
            requestType.GetField("Payload").SetValue(request,Encoding.UTF8.GetBytes("{\"Name\":\"Friend\",\"Token\":\""+token+"\",\"Protocol\":"+FestivalSession.ProtocolVersion+"}"));
            requestType.GetField("ClientNetworkId").SetValue(request,client);
            approve.Invoke(session,new[]{request,response});
            return (bool)responseType.GetField("Approved").GetValue(response)?"":(string)responseType.GetField("Reason").GetValue(response);
        }
    }
}
