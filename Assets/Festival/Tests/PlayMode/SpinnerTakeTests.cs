using System.Collections;
using System.Linq;
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
    // SPIN-1 in a running scene: the wheels cover the screen, then the camera cuts to the tripper taking the dose and reacting.
    public sealed class SpinnerTakeTests
    {
        static Transform Bone(FestivalCharacter actor,string name)=>actor.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static float Height(FestivalCharacter actor,string bone)=>actor.transform.InverseTransformPoint(Bone(actor,bone).position).y;

        [UnityTest]public IEnumerator TheTripperTakesTheDoseThenReactsByTheDose()
        {
            var root=new GameObject("Dose beat test");
            try
            {
                FestivalCharacter Actor(string name,float x)
                {
                    var actor=FestivalCharacter.Create(root.transform,name,Color.white);
                    actor.transform.localScale=Vector3.one*.82f;actor.transform.position=Vector3.right*x;actor.Pose="Intoxicated";return actor;
                }
                var sober=Actor("Beat reference",0);var taker=Actor("Beat taker",2.5f);var mild=Actor("Beat mild",5);var wild=Actor("Beat wild",7.5f);
                taker.Beat="TakeDose";
                yield return new WaitForSeconds(.8f);
                float Reach(FestivalCharacter actor)=>Vector3.Distance(Bone(actor,"HandR").position,Bone(actor,"Head").position);
                Debug.Log($"[Festival.Test] dose take reach={Reach(taker):F3} sober={Reach(sober):F3} hand={Height(taker,"HandR"):F3} sober hand={Height(sober,"HandR"):F3}");
                Assert.That(Reach(taker),Is.LessThan(Reach(sober)*.65f),"the take brings a hand up to the face");
                Assert.That(Height(taker,"HandR"),Is.GreaterThan(Height(sober,"HandR")+.35f),"from hanging at the hip");
                mild.Beat=wild.Beat="DoseReaction";mild.BeatStrength=.25f;wild.BeatStrength=1;
                yield return new WaitForSeconds(.8f);
                float Hands(FestivalCharacter actor)=>(Height(actor,"HandL")+Height(actor,"HandR"))*.5f;
                Debug.Log($"[Festival.Test] dose reaction hands mild={Hands(mild):F3} wild={Hands(wild):F3} sober={Hands(sober):F3} shoulders={Height(wild,"ArmL"):F3} head={Height(wild,"Head"):F3}");
                Assert.That(Hands(wild),Is.GreaterThan(Height(wild,"ArmL")-.1f),"four doses flings both arms out to shoulder height");
                Assert.That(Hands(wild),Is.GreaterThan(Hands(mild)+.25f),"one dose is a much smaller reaction");
                Assert.That(Hands(mild),Is.GreaterThan(Hands(sober)),"but still shows");
            }
            finally{Object.Destroy(root);}
        }

        [UnityTest]public IEnumerator EveryoneWatchesTheWheelsThenTheCameraCutsToTheTripper()
        {
            var world=new GameObject("Spinner world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Spinner session");var session=host.AddComponent<FestivalSession>();
            yield return null;
            try
            {
                session.Host("Tester",8607);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("spin_mate","Mate");
                player.X=0;player.Z=19;mate.X=1.5f;mate.Z=19;mate.Ready=true;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+30;
                while(session.State.Phase!="Spinning"&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.State.Phase,Is.EqualTo("Spinning"),"setup: the ready countdown ends in the spin: "+session.Message);
                double start=session.State.SpinEndsAt-FestivalSimulation.SpinSeconds;
                string tripper=session.State.TripperId;int dose=session.State.Doses.Find(d=>d.PlayerId==tripper).Dose;
                yield return new WaitForSeconds(.3f);

                var overlay=host.transform.Find("Festival spinner");
                Assert.That(overlay!=null&&overlay.gameObject.activeInHierarchy,"the wheels cover the screen during the spin");
                var shown=overlay.GetComponentsInChildren<Text>(false).Select(t=>t.text).ToList();
                Assert.That(shown,Does.Contain("TESTER").And.Contain("MATE"),"the people wheel has a slice for each friend");
                Assert.That(shown,Does.Contain("1").And.Contain("4"),"the dose wheel shows its slices, sliver included");

                while(session.EstimatedSimulationSeconds<start+FestivalSpinner.DoseStarts+FestivalSpinner.DoseSpin+.2&&Time.realtimeSinceStartup<deadline)yield return null;
                string name=sim.Player(tripper).Name.ToUpperInvariant();
                shown=overlay.GetComponentsInChildren<Text>(false).Select(t=>t.text).ToList();
                Assert.That(shown.Any(t=>t.Contains(name))&&shown.Any(t=>t.StartsWith(dose+" DOSE")),"both results read out once the wheels land: "+string.Join(" | ",shown));

                while(session.EstimatedSimulationSeconds<start+FestivalSpinner.TakeStarts+.4&&Time.realtimeSinceStartup<deadline)yield return null;
                var cut=host.GetComponentsInChildren<Camera>(true).First(c=>c.name=="Spinner take camera");
                var actor=session.WorldCharacter(tripper);
                Assert.That(cut.enabled,"the camera cuts to the tripper after the wheels land");
                var shot=overlay.Find("Take letterbox/Take shot").GetComponent<RawImage>();
                Assert.That(shot.enabled&&shot.texture!=null&&shot.texture==cut.targetTexture,"the shot fills the spinner's own canvas, over the HUD");
                Assert.That(actor.Beat,Is.EqualTo("TakeDose"),"the tripper takes the dose");
                var toFace=actor.transform.position+Vector3.up*1.25f-cut.transform.position;
                Assert.That(Vector3.Angle(cut.transform.forward,toFace),Is.LessThan(12f),"the shot frames the tripper");
                Assert.That(toFace.magnitude,Is.LessThan(3.5f),"close up");

                while(session.EstimatedSimulationSeconds<start+FestivalSpinner.ReactStarts+.3&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(actor.Beat,Is.EqualTo("DoseReaction"),"then reacts");
                Assert.That(actor.BeatStrength,Is.EqualTo(dose/4f),"sized by the dose");

                while(session.State.Phase=="Spinning"&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return null;yield return null;
                Assert.That(session.State.Phase,Is.Not.EqualTo("Spinning"),"setup: the spin ends");
                Assert.That(overlay.gameObject.activeSelf,Is.False,"the wheels clear for Loading");
                Assert.That(cut.enabled,Is.False,"the first-person view returns");
                Assert.That(cut.targetTexture,Is.Null,"and the shot's texture is let go");
                Assert.That(actor.Beat,Is.Empty,"the tripper goes back to the session's pose");
            }
            finally
            {
                session.Leave();
                foreach(var leftover in new[]{"First-person camera","Authoritative actor presentation"}){var found=GameObject.Find(leftover);if(found!=null)Object.Destroy(found);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }
    }
}
