using System.Collections;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    // MINI-1 in a running game: the level's booth stands in its ring on the festival grounds. The crew (the host alone) stands in the
    // ring and 3 s later its game starts: the festival holds as a slightly desaturated freeze-frame, its festivalgoers still drawn
    // where they stood, everything on frame time and the Ferris wheel still, the level clock stopped. The game's result shows for 4 s
    // with the world still stopped; then play resumes in full colour, the wheel turning on from where it stopped, and the booth is gone.
    public sealed class MinigamePlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator TheRingStartsAFreezeFrameGameThenItsResultThenPlayResumes()
        {
            var world=new GameObject("Minigame world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Minigame session");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            yield return null;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);
                Assert.That(sim.State.FestivalIndex,Is.EqualTo(Festivals.PoloFestival),"setup: a new game starts at Palm Mirage, with its Ferris wheel");
                yield return StartLevel(session);
                yield return null;yield return null;

                var at=FestivalSimulation.BoothAt(session.State);
                Assert.That(at,Is.Not.Null,"setup: the level has a booth");
                var booth=GameObject.Find(FestivalMinigameView.BoothName);
                Assert.That(booth!=null&&booth.activeInHierarchy,Is.True,"the booth stands on the festival grounds");
                Assert.That(Vector2.Distance(new Vector2(booth.transform.position.x,booth.transform.position.z),new Vector2(at.X,at.Z)),Is.LessThan(.01f),"on the level's booth spot");
                var grounds=world.transform.Find(FestivalWorld.RootName);
                var rotor=grounds.Find(FestivalTwistVisuals.PoloRootName+"/Ferris wheel/Wheel rotor");
                Assert.That(rotor,Is.Not.Null,"setup: Palm Mirage's Ferris wheel");
                Assert.That(world.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"setup: the world's colour grade");
                float vivid=grade.saturation.value;
                string festivalgoer=session.State.Npcs.Find(n=>n.Kind!="Cop").Id;

                // The crew stands in the ring, and 3 s later the game starts.
                var me=sim.Player(session.LocalPlayerId);me.X=at.X+1;me.Z=at.Z;
                deadline=Time.realtimeSinceStartup+6;
                while(session.State.Phase=="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"3 s in the ring starts the game: "+session.Message);
                yield return null;yield return null;

                // The freeze-frame.
                Assert.That(Time.timeScale,Is.Zero,"the world stops: people, the crowd and the stage's lights hold still");
                Assert.That(grounds.gameObject.activeInHierarchy,Is.True,"the festival stays on screen, not camp");
                Assert.That(GameObject.Find(festivalgoer)?.activeInHierarchy??false,Is.True,"festivalgoers are still drawn where they stood");
                Assert.That(booth.activeInHierarchy,Is.True,"and the booth, its game under way");
                Assert.That(grade.saturation.value,Is.EqualTo(vivid+FestivalNightLighting.FreezeSaturation).Within(1e-3f),"the frame is slightly desaturated");
                var wheel=rotor.localRotation;double level=session.State.ElapsedSeconds,rules=session.State.SimulationSeconds;
                for(float until=Time.realtimeSinceStartup+.5f;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(session.State.SimulationSeconds,Is.GreaterThan(rules+.3),"setup: the rules' clock runs on");
                Assert.That(Quaternion.Angle(wheel,rotor.localRotation),Is.LessThan(.01f),"the Ferris wheel holds still");
                Assert.That(session.State.ElapsedSeconds,Is.EqualTo(level),"the level clock stops");

                // The stub game ends at once for any player in it, for +0 s; its result shows for 4 s, the world still stopped.
                session.Command(FestivalSimulation.MinigameKind);yield return null;
                Assert.That(session.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0),"the game is scored: "+session.Message);
                for(float until=Time.realtimeSinceStartup+3;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"its result shows with the world still stopped");
                Assert.That(Time.timeScale,Is.Zero,"everything still holds");
                Assert.That(Quaternion.Angle(wheel,rotor.localRotation),Is.LessThan(.01f),"the wheel too");

                // Play resumes where it stopped.
                deadline=Time.realtimeSinceStartup+3;
                while(session.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"then play resumes");
                yield return null;yield return null;
                Assert.That(Time.timeScale,Is.EqualTo(1),"the world moves again");
                Assert.That(grade.saturation.value,Is.EqualTo(vivid).Within(1e-3f),"in full colour");
                Assert.That(booth.activeInHierarchy,Is.False,"the played booth is gone");
                // The rules' clock ran on some 4.5 s through the game and its result: 80° of turn the wheel must not jump.
                Assert.That(Quaternion.Angle(wheel,rotor.localRotation),Is.LessThan(10f),"the wheel turns on from where it stopped, with no jump");
                for(float until=Time.realtimeSinceStartup+.5f;Time.realtimeSinceStartup<until;)yield return null;
                Assert.That(Quaternion.Angle(wheel,rotor.localRotation),Is.InRange(4f,30f),"and turns again, 18° a second");
                Assert.That(session.State.ElapsedSeconds,Is.GreaterThan(level),"the level clock runs again");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
            Assert.That(Time.timeScale,Is.EqualTo(1),"leaving gives time back");
        }
    }
}
