using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace Festival.Tests
{
    // MINI-6 on the live HUD of a hosted crew of three and a spirit: the booth's sign names its game; the dose card and the day
    // quota stay up through Mystery Cups and its banner; the called pot shows on the quota at once, and the banner says each share
    // and what the stash keeps; the spirit's own view names whose turn it is from the roster captured at the start. The card
    // opens with the rules, and the tripper's hunch cup shimmers amber in their view alone. The spirit's rules say it watches,
    // and F on a friend's turn calls nothing: the notice says whose turn it is.
    public sealed class MinigameCardPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TheCardsHoldThroughCupsAndItsBannerSaysWhereThePotWent()
        {
            UseAVirtualKeyboard("cups card keyboard");
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
                // The host trips (the people spinner's bag holds only them), so the host's own card carries the hunch.
                sim.State.TripperBag.Clear();sim.State.TripperBag.Add(session.LocalPlayerId);
                yield return StartLevel(session);sim.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;
                Text Find(string name){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
                IEnumerator Wait(float seconds){for(float until=Time.realtimeSinceStartup+seconds;Time.realtimeSinceStartup<until;)yield return null;}
                int sequence=0;
                CommandResult Play(string id,string item,int amount=0)=>sim.Execute(id,new GameCommand{Id="impl-U60-"+(sequence++),Kind=FestivalSimulation.MinigameKind,ItemId=item,Amount=amount});

                sim.State.Npcs.Clear();ghost.Life="Spirit";
                var at=FestivalSimulation.BoothAt(sim.State);Assert.That(at,Is.Not.Null,"setup: the level has a booth");
                Assert.That(sim.State.Booth.Kind,Is.EqualTo(FestivalSimulation.MysteryCupsGame),"setup: the bag deals Mystery Cups");
                yield return Wait(.4f);
                TextMeshPro sign=null;foreach(var mesh in Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None))if(mesh.name=="Sign text")sign=mesh;
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
                Assert.That(sim.State.TripperId,Is.EqualTo(me.Id),"setup: the host trips");
                // The host drinks last, so the spirit reads two friends' turns.
                sim.State.Booth.Turns.Remove(me.Id);sim.State.Booth.Turns.Add(me.Id);
                yield return Wait(.4f);
                Assert.That(Find("Tripping")?.text,Is.EqualTo(tripping),"the dose card stays up while the crew plays cups");
                Assert.That(Find("Objective title")?.text,Is.EqualTo(quota),"and so does the quota");

                var b=sim.State.Booth;int safe=b.Spiked==0?1:0;string up=FestivalSimulation.CupsTurn(sim.State);
                Assert.That(up,Is.Not.EqualTo(me.Id),"setup: a friend drinks first");
                // The game holds the world's clock, so this waits in real time.
                yield return Tap(Key.F);yield return Wait(.2f);
                Assert.That(FestivalSimulation.CupsTurn(sim.State)==up&&b.Pot==0&&b.ScoredAt<0,Is.True,"F on a friend's turn calls nothing: "+session.Message);
                Assert.That(Find("Notice")?.text,Is.EqualTo("It's "+sim.Player(up).Name+"'s turn"),"and the notice says whose turn it is");
                Assert.That(b.Seconds,Is.LessThan(b.TurnAt),"setup: the rules intro is up");
                Assert.That(Find("Minigame detail")?.text,Does.StartWith("One of these "+b.Cups+" cups is spiked."),"the live card opens with the rules");
                var spirit=FestivalSession.ViewFor(sim,ghost.Id);var living=FestivalSession.ViewFor(sim,me.Id);var friend=FestivalSession.ViewFor(sim,sam.Id);
                Assert.That(FestivalHudText.MinigameCard(living,living.Players.Find(p=>p.Id==me.Id)),Is.EqualTo(FestivalHudText.MinigameCard(friend,friend.Players.Find(p=>p.Id==sam.Id))),"the tripper's rules carry no hunch");
                Assert.That(FestivalHudText.MinigameCard(spirit,spirit.Players.Find(p=>p.Id==ghost.Id)).Detail.Split('\n')[1],Is.EqualTo("You're watching: the players call it on their turn."),"the spirit's rules say it watches");
                // In the tripper's view alone the hunch cup shimmers amber; in a friend's view no cup does.
                var booth=host.GetComponent<FestivalMinigameView>().Booth.Find(FestivalMinigameView.CupsName);
                Material Look(Transform cups,int i)=>cups.Find(FestivalMinigameView.CupName(i)).GetComponentInChildren<MeshRenderer>().sharedMaterial;
                var amber=new[]{FestivalArtView.MaterialFor(FestivalMinigameView.HunchLook),FestivalArtView.MaterialFor(FestivalMinigameView.HunchDimLook)};
                Assert.That(b.Hint,Is.InRange(0,b.Cups-1),"setup: the host trips, so their view carries a hunch");
                for(int i=0;i<b.Cups;i++)
                    if(i==b.Hint)Assert.That(amber,Does.Contain(Look(booth,i)),"in the tripper's view the hunch, cup "+(i+1)+", shimmers amber");
                    else Assert.That(Look(booth,i),Is.EqualTo(FestivalArtView.MaterialFor(FestivalMinigameView.CupLook)),"in the tripper's view cup "+(i+1)+" is plain");
                var friends=FestivalMinigameView.Build(null);
                try
                {
                    var theirs=FestivalSession.ViewFor(sim,sam.Id);FestivalMinigameView.Show(friends,theirs);FestivalMinigameView.ShowCups(friends,theirs,-1,null);
                    for(int i=0;i<b.Cups;i++)Assert.That(Look(friends.Find(FestivalMinigameView.CupsName),i),Is.EqualTo(FestivalArtView.MaterialFor(FestivalMinigameView.CupLook)),"in Sam's view cup "+(i+1)+" is plain");
                }
                finally{Object.Destroy(friends.gameObject);}
                deadline=Time.realtimeSinceStartup+(float)FestivalSimulation.CupsIntroSeconds+2;
                while(b.Seconds<b.TurnAt&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(b.Seconds,Is.GreaterThanOrEqualTo(b.TurnAt),"setup: the first turn's clock starts");
                spirit=FestivalSession.ViewFor(sim,ghost.Id);
                Assert.That(spirit.Players.Exists(p=>p.Id==up),Is.False,"setup: the spirit's view lists no living player");
                living=FestivalSession.ViewFor(sim,me.Id);var seen=FestivalHudText.MinigameCard(spirit,spirit.Players.Find(p=>p.Id==ghost.Id));
                double sure=FestivalSimulation.HunchReliability(sim.State);string odds=sure>=.75?"about 3 times in 4":sure>=.5?"about half the time":sure>=.25?"about 1 time in 4":"about 1 time in 10";
                Assert.That(FestivalHudText.MinigameCard(living,living.Players.Find(p=>p.Id==me.Id)),Is.EqualTo((seen.Title,seen.Detail+" Your trip says cup "+(b.Hint+1)+"… trips like yours are right "+odds+".")),
                    "the spirit reads whose turn it is as the living do, and only the tripper's own card adds their hunch, with its odds");
                Assert.That(Play(up,FestivalSimulation.DrinkCup,safe).Accepted,Is.True,"a safe cup");
                up=FestivalSimulation.CupsTurn(sim.State);
                Assert.That(up,Is.Not.EqualTo(me.Id),"setup: a second friend drinks next");
                spirit=FestivalSession.ViewFor(sim,ghost.Id);
                Assert.That(FestivalHudText.MinigameCard(spirit,spirit.Players.Find(p=>p.Id==ghost.Id)).Detail,Does.StartWith(sim.Player(up).Name+"'s turn"),"the spirit reads whose turn it is by name");
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
