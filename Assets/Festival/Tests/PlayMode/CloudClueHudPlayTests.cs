using System.Collections;
using System.Collections.Generic;
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
    public sealed class CloudClueHudPlayTests:FestivalPlayModeTest
    {
        const string LieDown="Lie down and watch the clouds",GetUp="Get up";

        // TRIP-4 on a real hosted Day 1: lying down is the last thing the HUD lists, so beside a festivalgoer E still dances with
        // them; with nothing else about E lies you down. Lying there, E gets you up before anything else, the view drops to the
        // grass and looks up at the sky, and getting up puts your view back where it was. A friend lying down lies on their back.
        [UnityTest]public IEnumerator EListsLyingDownLastAndGetsYouUpFirst()
        {
            var world=new GameObject("Cloud HUD world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Cloud HUD");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            var pitchField=typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance);
            void PressE()=>((System.Action)primary.GetValue(view))?.Invoke();
            List<string> Actions(){var listed=new List<string>();foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Action:")&&b.gameObject.activeSelf)listed.Add(b.name.Substring(7));return listed;}
            string Prompt(){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Prompt")return text.text;return "(hidden)";}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("cloud_mate","Sam");mate.Ready=true;
                sim.State.FestivalIndex=Festivals.PoloFestival;sim.State.LevelIndex=0;
                yield return StartLevel(session);
                // Sober, with empty hands, among the crowd's chatty festivalgoers; Sam trips at the way back to camp, dosed, so the
                // rules keep them the tripper. Whatever the spin dosed you with is gone, as a dose's look can trail the view (TRIP-5).
                sim.State.TripperId=mate.Id;mate.X=Festivals.CampGateX;mate.Z=Festivals.CampGateZ;player.Effects.Clear();
                mate.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);mate.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";
                foreach(var n in sim.State.Npcs)n.Twist="";

                // On the open lawn beside a festivalgoer, nobody else about: E dances with them, and lying down is listed last.
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook");sim.State.Npcs.RemoveAll(n=>n!=npc);
                npc.Mode="Blending";npc.X=-20;npc.Z=-2;player.X=npc.X+.8f;player.Z=npc.Z;
                yield return new WaitForSeconds(.6f);
                var listed=Actions();
                if(listed.Count<2||!listed[0].StartsWith("Dance with festivalgoer")||listed[listed.Count-1]!=LieDown)failures.Add("beside a festivalgoer: listed \""+string.Join(" | ",listed)+"\", not the dance first and lying down last");

                // Once they wander off: E lies you down, looking up at the sky from the grass.
                sim.State.Npcs.Clear();
                pitchField.SetValue(session,7f);
                yield return new WaitForSeconds(.6f);
                if(Prompt()!=LieDown)failures.Add("on the open lawn: the prompt reads \""+Prompt()+"\", not \""+LieDown+"\"");
                PressE();
                yield return new WaitForSeconds(.6f);
                if(!FestivalSimulation.LyingDown(sim.State,player.Id))failures.Add("on the open lawn: E doesn't lie you down ("+session.Message+")");
                listed=Actions();
                if(listed.Count==0||listed[0]!=GetUp)failures.Add("lying down: the first action is \""+string.Join(" | ",listed)+"\", not \""+GetUp+"\"");
                if(listed.Contains(LieDown)||listed.Contains("Cancel current action"))failures.Add("lying down: listed \""+string.Join(" | ",listed)+"\"");
                var eye=session.ViewCamera.transform;
                if(eye.forward.y<.9f)failures.Add("lying down: the view looks along "+eye.forward+", not up at the sky");
                if(eye.position.y>.6f)failures.Add("lying down: the view is "+eye.position.y.ToString("0.00")+" m up, not down on the grass");
                if(HandsShown())failures.Add("lying down: the first-person hands still reach into the sky");
                // The mouse still looks round while lying there.
                pitchField.SetValue(session,-40f);yield return new WaitForSeconds(.3f);
                if(eye.forward.y>.8f)failures.Add("lying down: the view can't look round, still along "+eye.forward);

                // E gets you up, and the view goes back to where it looked before.
                PressE();
                yield return new WaitForSeconds(.6f);
                if(FestivalSimulation.LyingDown(sim.State,player.Id))failures.Add("lying down: E doesn't get you up ("+session.Message+")");
                if(Mathf.Abs((float)pitchField.GetValue(session)-7f)>.01f)failures.Add("got up: the view looks at pitch "+pitchField.GetValue(session)+", not the 7 degrees it had before lying down");
                if(eye.position.y<1.4f)failures.Add("got up: the view is still "+eye.position.y.ToString("0.00")+" m up");

                // Sam lies down where you can see them: on their back, head behind and feet ahead, down on the grass.
                mate.X=player.X;mate.Z=player.Z+4;
                Assert.That(sim.Execute(mate.Id,new GameCommand{Id="cloud_mate_lies",Kind=FestivalSimulation.LieDownKind}).Accepted,Is.True,"setup: Sam lies down");
                yield return new WaitForSeconds(1.4f);
                var sam=Object.FindObjectsByType<FestivalCharacter>(FindObjectsSortMode.None).FirstOrDefault(c=>c.name==mate.Id);
                if(sam==null)failures.Add("Sam is not drawn");
                else
                {
                    if(sam.Pose!=FestivalSimulation.LieDownKind)failures.Add("Sam's pose is "+sam.Pose);
                    float Up(string bone)=>sam.transform.InverseTransformPoint(Bone(sam,bone).position).y;
                    float Ahead(string bone)=>sam.transform.InverseTransformPoint(Bone(sam,bone).position).z;
                    if(Up("Hips")>.35f||Up("Head")>.45f)failures.Add("Sam lies with hips "+Up("Hips").ToString("0.00")+" m and head "+Up("Head").ToString("0.00")+" m up, not on the grass");
                    if(!(Ahead("Head")<Ahead("Hips")-.3f&&Ahead("FootL")>Ahead("Hips")+.3f))failures.Add("Sam doesn't lie on their back facing the way they lay down: head "+Ahead("Head").ToString("0.00")+", hips "+Ahead("Hips").ToString("0.00")+", foot "+Ahead("FootL").ToString("0.00")+" m ahead");
                    if(Mathf.Abs(sam.transform.position.y)>.01f)failures.Add("lying down moved Sam's actor off the ground: "+sam.transform.position.y);
                }
                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
        }

        static Transform Bone(FestivalCharacter actor,string name)=>actor.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static bool HandsShown()=>Object.FindObjectsByType<FestivalHands>(FindObjectsSortMode.None).Any(h=>h.gameObject.activeInHierarchy);
    }
}
