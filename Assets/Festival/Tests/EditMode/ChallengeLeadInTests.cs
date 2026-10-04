using System;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    public sealed class ChallengeLeadInTests
    {
        private GameObject made;
        [TearDown]public void Cleanup()
        {
            if(made!=null)Object.DestroyImmediate(made);
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
        }

        private static FestivalSimulation Pair()
        {
            var game=new FestivalSimulation(7);var player=game.AddPlayer("p0","Tester");
            game.State.Phase="Playing";game.State.SimulationSeconds=10;game.State.Npcs.Clear();
            player.X=0;player.Z=0;player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
            player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,Substance="ketamine",RemainingSeconds=100});
            game.State.TripperId=player.Id;
            var partner=new NpcState{Id="lead_in_partner",Kind="Wook",Role="Buyer",X=0,Z=1.5f,Yaw=180,CanTalk=true};
            partner.Observers.Add(new ObserverState{PlayerId=player.Id,Suspicion=40});game.State.Npcs.Add(partner);
            game.State.Visions.Add(new VisionState{Id="check",NpcId=partner.Id,Kind="Buyer",IsTrue=true});
            return game;
        }

        private static InteractionState Start(FestivalSimulation game,string kind)
        {
            string item=kind=="Sale"?"stock_lsd":kind=="Buy"?FestivalSimulation.SellerStock("lead_in_partner"):"";
            if(kind=="Buy")game.State.Npcs[0].Role="Regular";
            var result=game.Execute("p0",new GameCommand{Id=Guid.NewGuid().ToString("N"),Kind=kind=="Sale"?"StartSale":kind=="Buy"?"StartBuy":kind,TargetId="lead_in_partner",ItemId=item});
            Assert.That(result.Accepted,Is.True,result.Reason);return game.Interaction(game.Player("p0").InteractionId);
        }

        [TestCase("ConfirmDance")]
        [TestCase("Sale")]
        [TestCase("Buy")]
        [TestCase("Conversation")]
        public void TheFirstPonyDustArrowEntersAtTheBottom(string kind)
        {
            var game=Pair();double pressed=game.State.SimulationSeconds;var interaction=Start(game,kind);
            var chart=RhythmChart.For(interaction);
            made=new GameObject("impl:U06 lane entry");var hud=made.AddComponent<FestivalHud>();
            typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            hud.ShowChart(interaction,chart);
            var note=made.transform.Find("Festival HUD/Rhythm lane/Notes/Note 0").GetComponent<Image>();
            hud.DrawChart(pressed-interaction.StartSeconds,2.4,"ketamine",false);
            Assert.That(note.gameObject.activeSelf,Is.False,"the first frame has no arrow already partway up the lane");
            // Sample the real first visibility boundary, as UpdateRhythm does from the host's start and the local lane lead.
            double enters=interaction.StartSeconds+chart.Notes[0].TimeSeconds-2.4;
            hud.DrawChart(enters-interaction.StartSeconds-.001,2.4,"ketamine",false);
            Assert.That(note.gameObject.activeSelf,Is.False,"just before its lane entry the first arrow is hidden");
            hud.DrawChart(enters-interaction.StartSeconds+.000001,2.4,"ketamine",false);
            Assert.That(note.gameObject.activeSelf,Is.True,"at lane entry the first arrow is visible");
            Assert.That(note.rectTransform.anchorMin.y,Is.EqualTo(.11f).Within(.00001f),"its first visible frame sits on the spawn line");
            hud.DrawChart(chart.Notes[0].TimeSeconds,2.4,"ketamine",false);
            var target=(RectTransform)made.transform.Find("Festival HUD/Rhythm lane/Receptor well "+chart.Notes[0].Direction+"/Target arrow");
            Assert.That(Vector3.Distance(note.rectTransform.TransformPoint(note.rectTransform.rect.center),target.TransformPoint(target.rect.center)),Is.LessThan(1),"the scheduled note still lands on its receptor");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [TestCase("ConfirmDance")]
        [TestCase("Conversation")]
        public void DebugDiagnosticsReportTheScheduledStartWithoutPrivateContent(string kind)
        {
            var game=Pair();var events=new List<(string stage,string detail,string round,double time)>();
            game.ChatDiagnostic=(stage,detail,round,time)=>events.Add((stage,detail,round,time));
            var interaction=Start(game,kind);
            Assert.That(events.Count,Is.EqualTo(1),"the existing opt-in debug callback reports challenge scheduling");
            Assert.That(events[0].stage,Is.EqualTo("ChallengeLeadIn"));
            Assert.That(events[0].detail,Is.EqualTo(kind),"only the interaction kind is logged");
            Assert.That(events[0].round,Is.EqualTo(game.State.RoundId));
            Assert.That(events[0].time,Is.EqualTo(interaction.StartSeconds));
        }
#endif
    }
}
