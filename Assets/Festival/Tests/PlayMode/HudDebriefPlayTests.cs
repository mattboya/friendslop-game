using System.Collections;
using System.Collections.Generic;
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
    public sealed class HudDebriefPlayTests:FestivalPlayModeTest
    {
        // HUD-3: the running HUD's campfire debrief. Hosts a real round for a full crew of eight back at camp after a won Day 1:
        // the host clicks a friend for each award, reads who has voted, watches the verdict play out award by award with the
        // shots, then opens the shop with E only once it has. The winners wear their awards over their heads. Labels are laid
        // out on a 1920x1080 canvas, so a line its box cuts off fails, down to the longest names on a ballot of eight. Then the
        // crew clears Palm Mirage's last night, and the review counts that weekend's sales in its dollars (PLAYA-2).
        [UnityTest]public IEnumerator TheCrewVotesByClickingAndWatchesTheVerdictPlayOut()
        {
            var world=new GameObject("HUD debrief world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD debrief");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            void Fits(string where,string label)
            {
                var text=Find(label);if(text==null)failures.Add(where+": "+label+" is hidden");else Drawn(where,label,text);
            }
            void Drawn(string where,string label,Text text)
            {
                Canvas.ForceUpdateCanvases();
                var drawn=text.cachedTextGenerator;int shown=drawn.characterCountVisible;
                if(shown<text.text.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" (font "+text.fontSize+") cuts \""+text.text.Replace("\n"," | ")+"\" after \""+text.text.Substring(0,shown).Replace("\n"," | ")+"\"");
            }
            // The friends on an award's ballot row, by the name each button shows.
            Dictionary<string,Button> Picks(int award)
            {
                var picks=new Dictionary<string,Button>();
                foreach(var button in hud.GetComponentsInChildren<Button>(false))if(button.name.StartsWith("Award "+(award+1)+" pick "))picks[button.GetComponentInChildren<Text>().text]=button;
                return picks;
            }
            void Click(string where,int award,string friend)
            {
                var picks=Picks(award);
                if(picks.TryGetValue(friend,out var button))button.onClick.Invoke();
                else failures.Add(where+": award "+(award+1)+" has no "+friend+" button among ["+string.Join(" / ",picks.Keys)+"]");
            }
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            // A player's floating name tag, as the actor presentation draws it.
            string Tag(string id)
            {
                foreach(Transform actor in GameObject.Find("Authoritative actor presentation").transform)if(actor.name.Contains(id))return actor.Find("Label/Text")?.GetComponent<TextMesh>()?.text??"(no tag)";
                return "(no tag)";
            }
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var host=session.LocalPlayerId;var mate=sim.AddPlayer("hud_mate","Sam");
                // The rest of a full crew, most with a name as long as a name gets (24 letters).
                var rest=new List<PlayerState>();
                foreach(var name in new[]{"Bartholomew Fizzlebottom","Guinevere Moonpuddle-Ray","Maximiliana Starwhistle","Kim","Florentyna Dustbunnykins","Lou"})rest.Add(sim.AddPlayer("hud_crew_"+rest.Count,name));
                string others=string.Join(", ",rest.ConvertAll(p=>p.Name.ToUpperInvariant()));

                // Day 1 won: the host brings the crew back to the campfire.
                sim.State.Phase="Results";sim.State.Result="Success";sim.State.GrossSales=45;
                session.Command("Reset");
                yield return new WaitForSeconds(.6f);
                Assert.That(sim.State.Phase,Is.EqualTo("CampReview"),"back at camp: "+session.Message);
                var awards=sim.State.ReviewAwards;Assert.That(awards,Has.Count.EqualTo(3),"setup: three awards are up for a vote");
                Expect("campfire","Review text","THE VERY OFFICIAL ROUND REVIEW\nDAY 1 CLEARED  •  SALES $45  •  SURVIVORS 0  •  CAMP ANTICS 0");
                Expect("campfire","Award 1","1  "+awards[0].ToUpperInvariant()+"  •  PICK A FRIEND");
                Expect("campfire","Review status","CLICK A FRIEND FOR EACH AWARD, OR PRESS 1–3\nWAITING ON  YOU, SAM, "+others);
                if(!session.PointerFree)failures.Add("campfire: the pointer is locked, so nobody can click a friend");
                if(string.Join(", ",Picks(0).Keys)!="YOU, SAM, "+others)failures.Add("campfire: award 1 offers ["+string.Join(" / ",Picks(0).Keys)+"], expected the whole crew");
                foreach(var pick in Picks(0).Values)Drawn("campfire","a friend's button",pick.GetComponentInChildren<Text>());

                // The host clicks Sam for the first award.
                Click("campfire",0,"SAM");
                yield return new WaitForSeconds(.4f);
                if(!sim.State.ReviewVotes.Exists(v=>v.PlayerId==host&&v.TargetId==mate.Id&&v.Award==0))failures.Add("campfire: clicking SAM casts no vote for Sam ("+session.Message+")");
                Expect("host picked Sam","Award 1","1  "+awards[0].ToUpperInvariant()+"  •  YOUR PICK: SAM");
                var picks=Picks(0);
                if(picks.Count==8&&picks["SAM"].GetComponent<Image>().color==picks["YOU"].GetComponent<Image>().color)failures.Add("host picked Sam: Sam's button looks the same as the others");

                // The host finishes the ballot, choosing themself for the last award; nobody else has voted yet.
                Click("host voting",1,"SAM");Click("host voting",2,"YOU");
                yield return new WaitForSeconds(.4f);
                Expect("host voted","Review status","CLICK A FRIEND FOR EACH AWARD, OR PRESS 1–3\nVOTED  YOU  •  WAITING ON  SAM, "+others);
                foreach(var label in new[]{"Review text","Award 1","Award 2","Award 3","Review status"})Fits("host voted",label);

                // The rest of the crew votes the same way: the verdict is in and plays out award by award.
                foreach(var voter in new List<PlayerState>(rest){mate})
                    for(int award=0;award<3;award++)sim.Execute(voter.Id,new GameCommand{Id=voter.Id+"_votes_"+award,Kind="ReviewVote",TargetId=award<2?mate.Id:host,Amount=award});
                yield return new WaitForSeconds(.3f);
                Assert.That(sim.State.ReviewWinners,Is.EqualTo(new[]{mate.Id,mate.Id,host}),"setup: Sam wins both worst awards and the host the best, after:\n"+string.Join("\n",failures));
                string drumroll="AND THE AWARDS GO TO…\n"+awards[0].ToUpperInvariant()+"  •  ?\n"+awards[1].ToUpperInvariant()+"  •  ?\n"+awards[2].ToUpperInvariant()+"  •  ?";
                Expect("every vote in","Review reveal",drumroll);
                if(Picks(0).Count>0||Find("Award 1")!=null)failures.Add("every vote in: the ballot is still up");
                if(session.PointerFree)failures.Add("every vote in: the pointer stays free and the view stays still");
                if(primary.GetValue(view)!=null)failures.Add("every vote in: E opens the shop before the verdict has played");
                if(Tag(mate.Id)!="Sam")failures.Add("every vote in: Sam's tag reads \""+Tag(mate.Id)+"\" before the drumroll has read out a single award");
                yield return new WaitForSeconds((float)FestivalHudText.RevealStepSeconds);
                Expect("the first award","Review reveal","AND THE AWARDS GO TO…\n"+awards[0].ToUpperInvariant()+"  •  SAM\n"+awards[1].ToUpperInvariant()+"  •  ?\n"+awards[2].ToUpperInvariant()+"  •  ?");
                yield return new WaitForSeconds((float)(3*FestivalHudText.RevealStepSeconds));
                Expect("the verdict","Review reveal","AND THE AWARDS GO TO…\n"+awards[0].ToUpperInvariant()+"  •  SAM\n"+awards[1].ToUpperInvariant()+"  •  SAM\n"+awards[2].ToUpperInvariant()+"  •  YOU\nSAM TAKES 2 SHOTS");
                Expect("the verdict","Review status","E  OPEN THE CAMP SHOP");
                Expect("the verdict","Prompt","(hidden)");
                Fits("the verdict","Review reveal");

                // E opens the shop, where the next level starts: Sam wears both awards over their head.
                ((System.Action)primary.GetValue(view))?.Invoke();
                yield return new WaitForSeconds(.4f);
                if(sim.State.Phase!="Shopping")failures.Add("the verdict: E does not open the shop ("+session.Message+")");
                if(Tag(mate.Id)!="Sam  •  "+awards[0]+", "+awards[1])failures.Add("the shop: Sam's tag reads \""+Tag(mate.Id)+"\", expected their awards");

                // PLAYA-2: Palm Mirage's last night won. Camp moves on to Ember Playa, but the review counts the weekend's sales in dollars.
                sim.State.LevelIndex=Festivals.LevelCount-1;sim.State.Phase="Results";sim.State.Result="Success";sim.State.GrossSales=45;
                session.Command("Reset");
                yield return new WaitForSeconds(.6f);
                if(Festivals.Name(sim.State.FestivalIndex)!="Ember Playa")failures.Add("weekend cleared: camp is at "+Festivals.Name(sim.State.FestivalIndex)+", not Ember Playa ("+session.Message+")");
                Expect("weekend cleared","Review text","THE VERY OFFICIAL ROUND REVIEW\nWEEKEND CLEARED  •  SALES $45  •  SURVIVORS 0  •  CAMP ANTICS 0");
                Fits("weekend cleared","Review text");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"the debrief a player sees:\n"+string.Join("\n",failures));
        }
    }
}
