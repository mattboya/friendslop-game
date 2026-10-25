using System.Reflection;
using NUnit.Framework;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Festival.Tests
{
    // CROWD-3: every client sees festivalgoers scatter from a cop running in to make an arrest, running in the Panic clip, but
    // never whom they suspect, and a scattering one never reads as suspicion on your card.
    public sealed class CopScatterSessionTests
    {
        // What viewer's client receives, through the wire format.
        static RoundState Wire(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));

        // A level under way: cop_0 at the origin has seen p0 deal beside it and runs in to arrest them. The crowd's first eight
        // stand 4 m round the cop, each watching p1 (30), who is far off; p2 is far off too. The rest of the crowd is 50 m away.
        static FestivalSimulation RunIn()
        {
            var game=new FestivalSimulation(5);
            for(int k=0;k<3;k++)game.AddPlayer("p"+k,"P"+k);
            game.State.Phase="Playing";game.State.SimulationSeconds=100;
            var p0=game.Player("p0");p0.X=0;p0.Z=-1;var p1=game.Player("p1");p1.X=-40;p1.Z=-40;var p2=game.Player("p2");p2.X=40;p2.Z=-40;
            var crowd=game.State.Npcs.FindAll(n=>n.Kind=="Wook");
            for(int k=0;k<crowd.Count;k++)
            {
                crowd[k].X=k<8?4*Mathf.Sin(k*Mathf.PI/4):50+k;crowd[k].Z=k<8?4*Mathf.Cos(k*Mathf.PI/4):50;
                if(k<8)crowd[k].Observers.Add(new ObserverState{PlayerId="p1",Suspicion=30,LastSeenSeconds=100});
            }
            foreach(var cop in game.State.Npcs.FindAll(n=>n.Kind=="Cop")){cop.X=cop.Id=="cop_0"?0:80;cop.Z=0;cop.Yaw=180;}
            game.State.Npcs.Find(n=>n.Id=="cop_0").Evidence.Add(new EvidenceState{PlayerId="p0",Kind="WitnessedDeal",DetainAt=104});
            game.Tick(.3);
            return game;
        }

        [Test] public void EveryClientSeesTheScatterButNotWhomTheySuspect()
        {
            var game=RunIn();var near=game.State.Npcs.GetRange(0,8);
            var scattering=near.FindAll(n=>n.Mode==FestivalSimulation.Scattering);var staying=near.FindAll(n=>n.Mode!=FestivalSimulation.Scattering);
            Assert.That(scattering.Count,Is.EqualTo(6),"setup: three in four of the eight scatter on the host");
            Assert.That(staying.TrueForAll(n=>n.Mode=="Watching"&&n.TargetId=="p1"),Is.True,"setup: the two holding their ground still watch p1");
            Assert.That(scattering.TrueForAll(n=>n.TargetId=="p1"),Is.True,"setup: on the host, those scattering still suspect p1 most");
            foreach(var viewer in new[]{"p0","p1","p2"})
            {
                var view=Wire(game,viewer);
                foreach(var n in scattering)
                {
                    var seen=view.Npcs.Find(x=>x.Id==n.Id);
                    Assert.That(seen.Mode,Is.EqualTo(FestivalSimulation.Scattering),viewer+" sees "+n.Id+" scatter");
                    Assert.That(seen.TargetId,Is.EqualTo(""),viewer+" never learns whom "+n.Id+" suspects");
                    Assert.That(FestivalSession.NpcPose(seen),Is.EqualTo(FestivalSimulation.Scattering),viewer+"'s actor for "+n.Id+" is posed scattering");
                }
                foreach(var n in staying)
                {
                    var seen=view.Npcs.Find(x=>x.Id==n.Id);bool suspect=viewer=="p1";
                    Assert.That(seen.Mode,Is.EqualTo(suspect?"Watching":"Blending"),"as before, only the one "+n.Id+" watches sees it watching ("+viewer+")");
                    Assert.That(seen.TargetId,Is.EqualTo(suspect?"p1":""),"as before, only the one "+n.Id+" watches is named its target ("+viewer+")");
                }
            }
        }

        // The threat bar reads the most suspicious festivalgoer, but one scattering isn't after you while it flees: p1's bar
        // shows the 30 of the two still watching them, not the 60 of those running off.
        [Test] public void AScatteringFestivalgoerNeverReadsAsSuspicionOnYourCard()
        {
            var game=RunIn();
            foreach(var n in game.State.Npcs)if(n.Mode==FestivalSimulation.Scattering)n.Observers.Find(o=>o.PlayerId=="p1").Suspicion=60;
            var view=Wire(game,"p1");var p1=view.Players.Find(p=>p.Id=="p1");
            Assert.That(view.Npcs.Exists(n=>n.Mode==FestivalSimulation.Scattering&&n.Suspicion==60),Is.True,"setup: p1's client sees festivalgoers 60 suspicious of them scatter");
            var card=FestivalHudText.Condition(view,p1);
            Assert.That(card,Does.Not.Contain("SCATTERING"),"a scattering festivalgoer never reads on the card");
            Assert.That(FestivalHudText.Threat(view,p1),Is.EqualTo((.3f,1)),"the bar shows those still watching p1");
        }

        // A scattering festivalgoer runs in the Panic clip on its upper body, its legs left to the speed-driven run.
        [Test] public void ScatteringPlaysThePanicClip()
        {
            var go=new GameObject("scatter_actor");
            try
            {
                var character=go.AddComponent<FestivalCharacter>();
                var clip=((string name,bool loop,bool full,float rate))typeof(FestivalCharacter).GetMethod("ActingClip",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(character,new object[]{FestivalSimulation.Scattering,3.6f});
                Assert.That(clip.name,Is.EqualTo("Panic"),"scattering plays the Panic clip");
                Assert.That(clip.loop&&!clip.full,Is.True,"looped on the upper body, so the legs run at the festivalgoer's speed");
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
