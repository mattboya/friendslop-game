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

        // TRIP-4 on a real hosted Day 1: lying down is the last thing the HUD lists, so beside a festivalgoer E is still theirs (by
        // day with empty hands, buying from them: ECON-3); with nothing else about E lies you down. Lying there, E gets you up before anything else, the view drops to the
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
                var sim=Simulation(session);
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

                // On the open lawn beside a festivalgoer, nobody else about: E buys from them, and lying down is listed last.
                var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook");sim.State.Npcs.RemoveAll(n=>n!=npc);
                npc.Mode="Blending";npc.X=-20;npc.Z=-2;player.X=npc.X+.8f;player.Z=npc.Z;
                yield return new WaitForSeconds(.6f);
                var listed=Actions();
                if(listed.Count<2||!listed[0].StartsWith(FestivalHudText.BuyAction(sim.State,player,npc.Id))||listed[listed.Count-1]!=LieDown)failures.Add("beside a festivalgoer: listed \""+string.Join(" | ",listed)+"\", not the buy first and lying down last");

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

        // TRIP-8 (with ECON-5's items 3 and 7) on a real hosted Day 1. In the middle of anything the host takes only its way out (and
        // a handoff, a body, a call for help), so that is all the HUD lists, and the strip under the prompt stays empty: lying on the
        // grass beside a festivalgoer and a dropped item, lying at the night market looking up at a shelf, mid chat check and mid talk.
        // Whoever walks onto the cloud's stash is told so.
        [UnityTest]public IEnumerator InTheMiddleOfAnythingOnlyItsWayOutIsListed()
        {
            var world=new GameObject("Busy HUD world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Busy HUD");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            var pitchField=typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance);
            var yawField=typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance);
            void PressE()=>((System.Action)primary.GetValue(view))?.Invoke();
            string Listed(){var listed=new List<string>();foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Action:")&&b.gameObject.activeSelf)listed.Add(b.name.Substring(7));return string.Join(" | ",listed);}
            string Read(string name,bool hidden=false){foreach(var text in hud.GetComponentsInChildren<Text>(hidden))if(text.name==name)return text.text;return "(hidden)";}
            void OnlyItsWayOut(string where,string way)
            {
                if(Listed()!=way)failures.Add(where+": listed \""+Listed()+"\", not only \""+way+"\"");
                if(Read("Nearby")!="(hidden)")failures.Add(where+": the strip under the prompt reads \""+Read("Nearby").Replace("\n"," | ")+"\"");
            }
            InteractionState Doing(FestivalSimulation sim,PlayerState p)=>sim.State.Interactions.Find(i=>i.PlayerId==p.Id&&i.Status=="Active");
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("busy_mate","Sam");mate.Ready=true;
                sim.State.FestivalIndex=Festivals.PoloFestival;sim.State.LevelIndex=0;
                yield return StartLevel(session);
                // As in the test above: Sam trips, far off at the way back to camp; you are sober, with empty hands.
                sim.State.TripperId=mate.Id;mate.X=Festivals.CampGateX;mate.Z=Festivals.CampGateZ;player.Effects.Clear();
                mate.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);mate.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";
                sim.State.Visions.Clear();
                // A chatty festivalgoer who wants no dance first (not a raver or burner: Wary), and a dropped confetti at your feet.
                string talker="busy_talker";for(int k=0;DialogueGrammar.PersonaFor(talker,"")=="Raver"||DialogueGrammar.PersonaFor(talker,"")=="Burner";k++)talker="busy_talker_"+k;
                var npc=new NpcState{Id=talker,Role="Buyer",X=-20,Z=-2,Yaw=90,CanTalk=true};sim.State.Npcs.Clear();sim.State.Npcs.Add(npc);
                player.X=npc.X+.8f;player.Z=npc.Z;
                sim.State.Drops.Add(new DropState{Id="busy_drop",ItemId="confetti",Count=1,X=player.X,Z=player.Z});
                yield return new WaitForSeconds(.6f);
                if(!Listed().Contains("Pick up")||Read("Nearby")=="(hidden)")failures.Add("setup: standing there, the pickup and the festivalgoer's strip are not offered: \""+Listed()+"\"");

                // 1. Lying there: only Get up, and E gets you up.
                session.Command(FestivalSimulation.LieDownKind);
                yield return new WaitForSeconds(.6f);
                Assert.That(FestivalSimulation.LyingDown(sim.State,player.Id),Is.True,"setup: you lie down ("+session.Message+")");
                OnlyItsWayOut("lying beside a festivalgoer and a drop",GetUp);
                if(Read("Prompt")!=GetUp)failures.Add("lying beside a festivalgoer: the prompt reads \""+Read("Prompt")+"\"");

                // 2. Lying at the night market, looking up at a shelf 1.6 m away: still only Get up, and E gets you up, buying nothing.
                sim.State.Npcs.Clear();sim.State.Drops.Clear();
                var shelf=Catalog.ShopPoint(false,0);player.X=shelf.X;player.Z=shelf.Z-1.6f;
                yawField.SetValue(session,0f);pitchField.SetValue(session,-30f);
                yield return new WaitForSeconds(.6f);
                OnlyItsWayOut("lying at the night market, looking at a shelf",GetUp);
                if(Read("Prompt")!=GetUp)failures.Add("lying at the night market, looking at a shelf: the prompt reads \""+Read("Prompt")+"\", not \""+GetUp+"\"");
                int cash=player.Cash;PressE();
                yield return new WaitForSeconds(.6f);
                if(FestivalSimulation.LyingDown(sim.State,player.Id))failures.Add("lying at the night market: E doesn't get you up ("+session.Message+")");
                var checkout=(string)typeof(FestivalHud).GetField("checkoutItem",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
                if(player.Cash!=cash||checkout!="")failures.Add("lying at the night market: E went to buy the shelf's "+checkout+" ("+session.Message+")");
                if(FestivalSimulation.LyingDown(sim.State,player.Id))session.Command("Cancel");

                // 3. Mid chat check (ECON-5 item 3) and mid talk (item 7), beside a festivalgoer: only the cancel, and no strip.
                sim.State.TripperId=player.Id;player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=600});
                var checkee=new NpcState{Id="busy_checkee",Role="Buyer",X=-20,Z=-2,Yaw=90,CanTalk=true};
                sim.State.Npcs.Add(checkee);sim.State.Visions.Add(new VisionState{Id="busy_vision",Kind="Buyer",NpcId=checkee.Id});
                player.X=checkee.X+.8f;player.Z=checkee.Z;
                session.Command("ConfirmChat",checkee.Id);
                Assert.That(Doing(sim,player)?.Kind,Is.EqualTo("ConfirmChat"),"setup: you chat to check your vision ("+session.Message+"), after:\n"+string.Join("\n",failures));
                yield return new WaitForSeconds(.6f);
                OnlyItsWayOut("mid chat check","Cancel current action");
                session.Command("Cancel");sim.State.Npcs.Clear();sim.State.Visions.Clear();
                npc.Mode="Blending";sim.State.Npcs.Add(npc);
                session.Command("Conversation",npc.Id);
                var talk=Doing(sim,player);
                Assert.That(talk?.Kind=="Conversation"&&!FestivalSimulation.TrustDancing(talk),Is.True,"setup: you talk with them, no dance first ("+session.Message+"), after:\n"+string.Join("\n",failures));
                yield return new WaitForSeconds(.6f);
                OnlyItsWayOut("mid talk","Cancel current action");
                session.Command("Cancel");

                // 4. The cloud's stash: whoever walks onto it is told, once.
                sim.State.CloudClueLandmark=0;sim.State.CloudClueReadAt=sim.State.SimulationSeconds;
                var stash=FestivalSimulation.CloudStashAt(sim.State);Assert.That(stash,Is.Not.Null,"setup: the cloud's stash waits");
                player.X=stash.X;player.Z=stash.Z;
                yield return new WaitForSeconds(.6f);
                Assert.That(sim.State.CloudStashFound,Is.True,"setup: you find the cloud's stash");
                const string Found="Found the cloud's stash! +$15 for camp";
                if(session.Message!=Found||Read("Notice")!=Found)failures.Add("found the cloud's stash: the notice reads \""+Read("Notice")+"\" (message \""+session.Message+"\"), not \""+Found+"\"");
                session.SetMessage("");
                yield return new WaitForSeconds(.3f);
                if(session.Message!="")failures.Add("found the cloud's stash: the notice comes back (\""+session.Message+"\")");

                Assert.That(failures,Is.Empty,string.Join("\n",failures));
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
        }

        static Transform Bone(FestivalCharacter actor,string name)=>actor.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static bool HandsShown()=>Object.FindObjectsByType<FestivalHands>(FindObjectsSortMode.None).Any(h=>h.gameObject.activeInHierarchy);
    }
}
