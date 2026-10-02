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
    public sealed class SplitObjectivePlayTests:FestivalPlayModeTest
    {
        GameObject world,hud;FestivalSession session;FestivalHud view;FestivalSimulation sim;PlayerState player;

        string Read(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;return "(hidden)";}
        // E runs the HUD's primary action (FestivalHud.UpdateKeyboard), the first action it lists.
        void PressE()=>((System.Action)typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view))?.Invoke();

        // A real hosted round of five on Night 1: the host plus four friends without clients of their own, who then wait at
        // the way back to camp while the host plays in an empty crowd.
        IEnumerator HostBigNight()
        {
            world=new GameObject("Split objective world");world.AddComponent<FestivalWorld>();yield return null;
            hud=new GameObject("Split objective HUD");
            session=hud.AddComponent<FestivalSession>();view=hud.AddComponent<FestivalHud>();
            yield return null;
            session.Host("Tester",HostPort);
            float deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
            sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
            player=sim.Player(session.LocalPlayerId);
            for(int i=0;i<4;i++)sim.AddPlayer("split_mate_"+i,"Mate "+i).Ready=true;
            sim.State.LevelIndex=1;player.X=0;player.Z=19;session.Command("Ready");
            yield return SkipCountdown(session);yield return SkipSpin(session);yield return FinishLoading(session);
            Assert.That(session.State.SecondFriend.Active,Is.True,"the host's view says a crew of five lost two friends");
            sim.State.Npcs.Clear();
            foreach(var mate in sim.State.Players)if(mate!=player){mate.X=Festivals.CampGateX;mate.Z=Festivals.CampGateZ;}
        }
        // The tripper follows both trails to their ends.
        void BothTrailsFollowed(){var second=sim.State.SecondFriend;sim.State.CluesRead=sim.State.ClueChain.Count;sim.State.GateOpened=true;second.CluesRead=second.ClueChain.Count;second.GateOpened=true;}

        [UnityTearDown]public IEnumerator LeaveTheRound()
        {
            session?.Leave();
            foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
            Object.Destroy(hud);Object.Destroy(world);
            yield return null;
        }

        // CROWD-2: the host walks up to the second lost friend. They appear once their trail ends, the objective card counts
        // both friends, and E recruits them.
        [UnityTest]public IEnumerator ABigCrewSeesAndRecruitsTheSecondFriend()
        {
            yield return HostBigNight();
            // The host right beside the second friend before their trail ends.
            var second=sim.State.SecondFriend;
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
            PressE();yield return null;
            Assert.That(sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="FindFriend"&&i.Status=="Active"),Is.True,"E starts recruiting the second friend: "+session.Message);
            yield return new WaitForSeconds(2.6f);
            Assert.That(second.Found&&second.LeaderId==player.Id,Is.True,"the second friend is found and follows the host");
            Assert.That(session.State.SecondFriend.Found,Is.True,"and the host's view shows it");
            Assert.That(Read("Objective title"),Is.EqualTo("TWO FRIENDS LOST • 1 / 2 FOUND"),"one found, one to go");
        }

        // The host, already escorting the second friend, stops 2.2 m from the first, the second trailing 1.4 m behind (inside
        // the 2.5 m reach). "Recruit missing friend" is offered and E recruits the first friend, not the second again.
        [UnityTest]public IEnumerator TheEscortOfOneFriendRecruitsTheOther()
        {
            yield return HostBigNight();
            BothTrailsFollowed();var second=sim.State.SecondFriend;second.Found=true;second.LeaderId=player.Id;
            var first=sim.State.FriendPosition;player.X=first.X+2.2f;player.Z=first.Z;second.Position.X=first.X+3.6f;second.Position.Z=first.Z;
            yield return new WaitForSeconds(.6f);
            Assert.That(Read("Prompt"),Is.EqualTo("Recruit missing friend"),"the HUD offers to recruit the first friend");
            PressE();yield return new WaitForSeconds(2.6f);
            Assert.That(sim.State.FriendFound&&sim.State.FriendLeaderId==player.Id&&second.LeaderId==player.Id,Is.True,"E recruits the first friend; both follow the host: "+session.Message);
        }

        // A mate escorts the first friend right past the host, who stands 2.2 m from the still-lost second friend. The HUD
        // offers what E does: recruit the lost friend, not take over the mate's escort.
        [UnityTest]public IEnumerator EOffersAndRecruitsTheLostFriendBesideAnEscort()
        {
            yield return HostBigNight();
            BothTrailsFollowed();var second=sim.State.SecondFriend;var mate=sim.State.Players.Find(p=>p!=player);
            sim.State.FriendFound=true;sim.State.FriendLeaderId=mate.Id;
            var spot=second.Position;player.X=spot.X+2.2f;player.Z=spot.Z;sim.State.FriendPosition.X=spot.X+3.2f;sim.State.FriendPosition.Z=spot.Z;mate.X=spot.X+4.6f;mate.Z=spot.Z;
            yield return new WaitForSeconds(.6f);
            Assert.That(Read("Prompt"),Is.EqualTo("Recruit missing friend"),"the HUD offers the lost second friend, not the first friend 1 m away");
            PressE();yield return null;
            Assert.That(sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="FindFriend"&&i.TargetId=="friend_2"&&i.Status=="Active"),Is.True,"E starts recruiting the second friend: "+session.Message);
            yield return new WaitForSeconds(2.6f);
            Assert.That(second.Found&&second.LeaderId==player.Id&&sim.State.FriendLeaderId==mate.Id,Is.True,"the host leads the second friend; the mate keeps the first");
        }

        // TRIP-7: a real host deals the night (its roles, both trails, where both friends are lost) from a secret of its own, which
        // its own view, like every client's, never carries.
        [UnityTest]public IEnumerator TheHostDealsFromASecretNoViewCarries()
        {
            yield return HostBigNight();
            Assert.That(sim.State.DealSeed,Is.Not.Zero,"the host deals the night from a secret");
            Assert.That(session.State.DealSeed,Is.Zero,"the host's own view never carries it");
        }
    }
}
