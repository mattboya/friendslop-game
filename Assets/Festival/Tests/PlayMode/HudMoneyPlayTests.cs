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
    public sealed class HudMoneyPlayTests:FestivalPlayModeTest
    {
        // HUD-3 with PLAYA-1: on Ember Playa the running HUD never shows a dollar. Hosts a real round as the crew member who counts
        // in friendship bracelets, the longest odd object: at camp they eye the Tongue Stamps on the shelf, pick them up, show them to
        // the seller and pay; out on Day 1 they read their pocket, the stash, the sales and the quota with security closing in,
        // and try to leave early, which the host refuses in dollars. Labels are laid out on a 1920x1080 canvas and must draw
        // whole, and the shelf's price tags must stay within their slot.
        [UnityTest]public IEnumerator EmberPlayaCountsInYourOddObjects()
        {
            var world=new GameObject("HUD money world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("HUD money");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected.Replace("\n"," | ")+"\"");}
            void Fits(string where,params string[] labels)
            {
                Canvas.ForceUpdateCanvases();
                foreach(var label in labels)
                {
                    var text=Find(label);if(text==null){failures.Add(where+": "+label+" is hidden");continue;}
                    int shown=text.cachedTextGenerator.characterCountVisible;
                    if(shown<text.text.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" (font "+text.fontSize+") cuts \""+text.text.Replace("\n"," | ")+"\" after \""+text.text.Substring(0,shown).Replace("\n"," | ")+"\"");
                }
            }
            // Nothing on screen names a dollar here.
            void NoDollars(string where){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.text.Contains("$"))failures.Add(where+": "+text.name+" reads \""+text.text.Replace("\n"," | ")+"\"");}
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            void PressE()=>((System.Action)primary.GetValue(view))?.Invoke();
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);var mate=sim.AddPlayer("money_mate","Sam");mate.Ready=true;
                // The weekend is at Ember Playa, and you are the crew member who counts in friendship bracelets.
                sim.State.FestivalIndex=Festivals.PlayaFestival;player.Ordinal=3;
                Assert.That(Festivals.CurrencyName(Festivals.PlayaFestival,3,2),Is.EqualTo("2 friendship bracelets"),"setup: the longest odd object");
                int shelf=sim.State.VendorOffers.IndexOf("stock_lsd");Assert.That(shelf,Is.GreaterThanOrEqualTo(0),"setup: Tongue Stamps are on the shelf");

                // At the shelf, eyeing the Tongue Stamps.
                var spot=Catalog.ShopPoint(true,shelf);player.X=spot.X;player.Z=6.3f;
                typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,0f);
                typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,-Mathf.Atan2((shelf<4?1.17f:2.05f)-1.65f,9f-6.3f)*Mathf.Rad2Deg);
                yield return new WaitForSeconds(.5f);
                Expect("at the shelf","Prompt","PICK UP TONGUE STAMPS  •  5 FRIENDSHIP BRACELETS  •  Clean sales pay 10 friendship bracelets and up, or take: 60s of warped cues and slower, drifting steps.");
                Fits("at the shelf","Prompt");NoDollars("at the shelf");
                // The price tags on both shelves, each lettered within its painted card.
                int tags=0;
                foreach(var tag in world.GetComponentsInChildren<TextMesh>(true))
                {
                    if(tag.name!="Name and price")continue;tags++;
                    if(tag.text.Contains("$")||!tag.text.Contains("FRIENDSHIP BRACELET"))failures.Add("at the shelf: a price tag reads \""+tag.text.Replace("\n"," | ")+"\"");
                    var card=tag.transform.parent.Find("Painted price tag");if(card==null){failures.Add("at the shelf: the tag \""+tag.text.Replace("\n"," | ")+"\" has no painted card");continue;}
                    float width=tag.GetComponent<MeshRenderer>().bounds.size.x,cardWidth=card.GetComponentInChildren<Renderer>().bounds.size.x;
                    if(width>cardWidth)failures.Add("at the shelf: the tag \""+tag.text.Replace("\n"," | ")+"\" is "+width.ToString("0.00")+" m wide, past its "+cardWidth.ToString("0.00")+" m card");
                }
                if(tags==0)failures.Add("at the shelf: no price tags");

                // Picked up, shown to the seller, paid.
                PressE();yield return new WaitForSeconds(.4f);
                Assert.That(player.HeldOfferId,Is.EqualTo("stock_lsd"),"setup: E picks up the Tongue Stamps ("+session.Message+"), after:\n"+string.Join("\n",failures));
                player.X=0;player.Z=7;yield return new WaitForSeconds(.4f);
                Expect("holding them","Held item price","5 FRIENDSHIP BRACELETS");
                Expect("holding them","Held item details","Clean sales pay 10 friendship bracelets and up, or take: 60s of warped cues and slower, drifting steps.");
                Fits("holding them","Held item title","Held item price","Held item details");
                PressE();yield return new WaitForSeconds(.4f);
                Expect("at the counter","Handoff price","E  PAY 5 FRIENDSHIP BRACELETS  •  TONGUE STAMPS    G CANCEL");
                Expect("at the counter","Prompt","PAY 5 FRIENDSHIP BRACELETS FOR TONGUE STAMPS");
                Fits("at the counter","Handoff price","Prompt","Held item price");NoDollars("at the counter");
                PressE();yield return new WaitForSeconds(.4f);
                Assert.That(player.Inventory.Exists(i=>i.ItemId=="stock_lsd"),Is.True,"setup: E pays for the Tongue Stamps ("+session.Message+")");
                Fits("paid","Vitals");NoDollars("paid");

                // Out on Day 1.
                yield return StartLevel(session);
                // Mid-day with money in every pot and security about to detain you; no effects trail the warning.
                player.Effects.Clear();player.Cash=187;sim.State.StashCash=45;sim.State.LevelSales=12;player.X=0;player.Z=0;mate.X=30;mate.Z=0;
                sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");
                var cop=sim.State.Npcs.Find(n=>n.Kind=="Cop");cop.X=1.5f;cop.Z=0;cop.Evidence.Add(new EvidenceState{PlayerId=player.Id,Kind="WitnessedDeal",DetainAt=sim.State.SimulationSeconds+300});
                yield return new WaitForSeconds(.4f);
                Expect("Day 1","Vitals","HP 100  •  187 FRIENDSHIP BRACELETS  •  STASH 45 FRIENDSHIP BRACELETS\nSALES 12 FRIENDSHIP BRACELETS  •  ALIVE  •  ARREST WARNING");
                int quota=FestivalSimulation.DayQuota(sim.State);
                Expect("Day 1","Objective title","DAY QUOTA  12 FRIENDSHIP BRACELETS / "+quota+" FRIENDSHIP BRACELETS");
                Expect("Day 1","Objective detail","Sell "+(quota-12)+" friendship bracelets more before sundown.");
                Fits("Day 1","Vitals","Objective title","Objective detail");NoDollars("Day 1");
                // Heading home before the quota is met: the host's refusal names the shortfall in dollars.
                session.Command("Extract");yield return new WaitForSeconds(.4f);
                Expect("leaving early","Notice","Sell "+(quota-12)+" friendship bracelets more to meet the day's quota");
                Fits("leaving early","Notice");NoDollars("leaving early");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Ember Playa's money on screen:\n"+string.Join("\n",failures));
        }
    }
}
