using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // MINI-6 on the live HUD of a hosted crew of three and a spirit: the booth's sign names its game; the dose card and the day
    // quota stay up through Mystery Cups and its banner; the called pot shows on the quota at once, and the banner says each share
    // and what the stash keeps; the spirit's own view names whose turn it is from the roster captured at the start.
    public sealed class MinigameCardPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TheCardsHoldThroughCupsAndItsBannerSaysWhereThePotWent()
        {
            var world=new GameObject("impl-U60 card world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl-U60 card session");var session=host.AddComponent<FestivalSession>();var hud=host.AddComponent<FestivalHud>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                var sam=sim.AddPlayer("impl-U60-sam","Sam");sam.Ready=true;var ash=sim.AddPlayer("impl-U60-ash","Ash");ash.Ready=true;
                var ghost=sim.AddPlayer("impl-U60-ghost","Ghost");ghost.Ready=true;
                sim.State.FestivalIndex=Festivals.PoloFestival;sim.State.LevelIndex=0;
                yield return StartLevel(session);
                Text Find(string name){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
                IEnumerator Wait(float seconds){for(float until=Time.realtimeSinceStartup+seconds;Time.realtimeSinceStartup<until;)yield return null;}
                int sequence=0;
                CommandResult Play(string id,string item,int amount=0)=>sim.Execute(id,new GameCommand{Id="impl-U60-"+(sequence++),Kind=FestivalSimulation.MinigameKind,ItemId=item,Amount=amount});

                sim.State.Npcs.Clear();ghost.Life="Spirit";
                var at=FestivalSimulation.BoothAt(sim.State);Assert.That(at,Is.Not.Null,"setup: the level has a booth");
                Assert.That(sim.State.Booth.Kind,Is.EqualTo(FestivalSimulation.MysteryCupsGame),"setup: the bag deals Mystery Cups");
                yield return Wait(.4f);
                TextMesh sign=null;foreach(var mesh in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))if(mesh.name=="Sign text")sign=mesh;
                Assert.That(sign?.text,Is.EqualTo("MYSTERY CUPS\nALL IN THE RING"),"the booth's sign names its game");
                string tripping=Find("Tripping")?.text;
                Assert.That(tripping,Is.Not.Empty,"setup: the dose card is up while the crew plays");
                int sales=sim.State.LevelSales;string quota="DAY QUOTA  $"+sales+" / $"+FestivalSimulation.DayQuota(sim.State);
                Assert.That(Find("Objective title")?.text,Is.EqualTo(quota),"setup: the quota card");

                var me=sim.Player(session.LocalPlayerId);
                foreach(var p in new[]{me,sam,ash}){p.X=at.X+.5f;p.Z=at.Z;}
                deadline=Time.realtimeSinceStartup+8;
                while(sim.State.Phase=="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(sim.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"setup: the game starts: "+session.Message);
                yield return Wait(.4f);
                Assert.That(Find("Tripping")?.text,Is.EqualTo(tripping),"the dose card stays up while the crew plays cups");
                Assert.That(Find("Objective title")?.text,Is.EqualTo(quota),"and so does the quota");

                var b=sim.State.Booth;int safe=b.Spiked==0?1:0;string up=FestivalSimulation.CupsTurn(sim.State);
                if(up!=me.Id)
                {
                    var spirit=FestivalSession.ViewFor(sim,ghost.Id);
                    Assert.That(spirit.Players.Exists(p=>p.Id==up),Is.False,"setup: the spirit's view lists no living player");
                    var living=FestivalSession.ViewFor(sim,me.Id);
                    Assert.That(FestivalHudText.MinigameCard(spirit,spirit.Players.Find(p=>p.Id==ghost.Id)),Is.EqualTo(FestivalHudText.MinigameCard(living,living.Players.Find(p=>p.Id==me.Id))),"the spirit reads whose turn it is as the living do");
                }
                Assert.That(Play(up,FestivalSimulation.DrinkCup,safe).Accepted,Is.True,"a safe cup");
                up=FestivalSimulation.CupsTurn(sim.State);
                if(up!=me.Id)
                {
                    var spirit=FestivalSession.ViewFor(sim,ghost.Id);
                    Assert.That(FestivalHudText.MinigameCard(spirit,spirit.Players.Find(p=>p.Id==ghost.Id)).Detail,Does.StartWith(sim.Player(up).Name+"'s turn"),"the spirit reads whose turn it is by name");
                }
                Assert.That(Play(up,FestivalSimulation.CallCups).Accepted,Is.True,"then a call");
                Assert.That(sim.State.LevelSales,Is.EqualTo(sales+5),"setup: the pot counts as sales");
                yield return Wait(.4f);
                Assert.That(sim.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"setup: the banner is up");
                Assert.That(Find("Minigame detail")?.text,Does.StartWith("Called it: $1 each, $2 to the stash, counted as sales."),"each share and the odd dollars");
                Assert.That(Find("Objective title")?.text,Is.EqualTo("DAY QUOTA  $"+(sales+5)+" / $"+FestivalSimulation.DayQuota(sim.State)),"the quota shows the payout at once");
                Assert.That(Find("Tripping")?.text,Is.EqualTo(tripping),"the dose card stays up through the banner");

                deadline=Time.realtimeSinceStartup+6;
                while(sim.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(sim.State.Phase,Is.EqualTo("Playing"),"setup: play resumes");
                yield return Wait(.4f);
                Assert.That(Find("Tripping")?.text,Is.EqualTo(tripping),"and after it");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }
    }
}
