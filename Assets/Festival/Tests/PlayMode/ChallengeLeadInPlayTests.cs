using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    public sealed class ChallengeLeadInPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator AHostedCheckDanceArrowEntersAtTheBottom()=>Entry("ConfirmDance");
        [UnityTest]public IEnumerator AHostedTrustDanceArrowEntersAtTheBottom()=>Entry("Conversation");

        private IEnumerator Entry(string kind)
        {
            var world=new GameObject("impl:U06 lead-in world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U06 lead-in HUD");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();yield return null;
            session.Host("Tester",HostPort);
            float deadline=Time.realtimeSinceStartup+30;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
            yield return StartLevel(session);
            var game=Simulation(session);var player=game.Player(session.LocalPlayerId);
            game.State.Npcs.Clear();player.X=0;player.Z=0;
            player.Effects.Clear();player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance="ketamine",RemainingSeconds=100});
            game.State.TripperId=player.Id;game.State.Doses.Clear();game.State.Doses.Add(new PlayerDose{PlayerId=player.Id,Dose=1,Substance="ketamine"});
            var partner=new NpcState{Id="lead_in_partner",Kind="Wook",Role="Buyer",CanTalk=true,X=0,Z=1.5f,Yaw=180};
            if(kind=="Conversation")partner.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=40});
            game.State.Npcs.Add(partner);game.State.Visions.Clear();game.State.Visions.Add(new VisionState{Id="check",NpcId=partner.Id,Kind="Buyer",IsTrue=true});
            session.Profile.Data.ReducedMotion=false;
            session.Command(kind,partner.Id);
            var interaction=game.Interaction(player.InteractionId);Assert.That(interaction,Is.Not.Null,session.Message);
            // Observe the actual HUD Update consumer, starting on the first rendered frame of the accepted challenge.
            var note=(RectTransform)host.transform.Find("Festival HUD/Rhythm lane/Notes/Note 0");
            bool hidden=false;deadline=Time.realtimeSinceStartup+10;
            do
            {
                yield return null;
                Assert.That(interaction.Status,Is.EqualTo("Active"),"the challenge survives until its first arrow");
                if(!note.gameObject.activeInHierarchy)hidden=true;
            }while(!note.gameObject.activeInHierarchy&&Time.realtimeSinceStartup<deadline);
            Assert.That(note.gameObject.activeInHierarchy,Is.True,"the live HUD shows the first arrow");
            Assert.That(hidden,Is.True,"the lane has a lead-in before showing its first arrow");
            // A rendered frame may cross the entry by a small amount. 2% allows 66 ms at the 2.4 s lane lead.
            Assert.That(note.anchorMin.y,Is.InRange(.109f,.13f),"the first rendered arrow enters at the spawn line");
        }
    }
}
