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
    public sealed class TwistHudPlayTests:FestivalPlayModeTest
    {
        // POLO-1 and PLAYA-1 (journey J2): a sober player uses every twist from the running HUD on a real hosted Night 1. On Palm
        // Mirage F beside the VIP guard talks them past the rope, E at the night market's VIP stall buys a wristband, and E at the
        // Ferris wheel's base boards it. Up there the objective card says where the lost friend and security are, and the friend
        // is drawn, though their trail is not yet followed and they stand far out of sight; by day nobody is lost to spot. On
        // Ember Playa E climbs aboard a passing art car. Labels are laid out on a 1920x1080 canvas, so a cut-off line fails.
        [UnityTest]public IEnumerator ASoberPlayerUsesEveryTwistFromTheHud()
        {
            var world=new GameObject("Twist HUD world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Twist HUD");
            var session=hud.AddComponent<FestivalSession>();var view=hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            Text Find(string name){foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
            string Read(string name)=>Find(name)?.text??"(hidden)";
            void Expect(string where,string label,string expected){string shown=Read(label);if(shown!=expected)failures.Add(where+": "+label+" reads \""+shown.Replace("\n"," | ")+"\", expected \""+expected+"\"");}
            void Fits(string where,string label)
            {
                var text=Find(label);if(text==null){failures.Add(where+": "+label+" is hidden");return;}
                Canvas.ForceUpdateCanvases();
                int shown=text.cachedTextGenerator.characterCountVisible;
                if(shown<text.text.Length)failures.Add(where+": "+label+" box "+text.rectTransform.rect.size+" cuts \""+text.text+"\" after \""+text.text.Substring(0,shown)+"\"");
            }
            // E runs the HUD's primary action, the first it lists, and F its chat key (FestivalHud.UpdateKeyboard).
            var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
            var chatKey=typeof(FestivalHud).GetMethod("ChatKey",BindingFlags.NonPublic|BindingFlags.Instance);
            void PressE()=>((System.Action)primary.GetValue(view))?.Invoke();
            void PressF()=>chatKey.Invoke(view,new object[]{session.State,session.LocalPlayer});
            string Actions(){var listed=new List<string>();foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Action:")&&b.gameObject.activeSelf)listed.Add(b.name.Substring(7));return string.Join(" | ",listed);}
            const string Rope="Talk your way past the VIP rope (5 s)",Band="Buy VIP wristband  •  $15",Wheel="Ride the Ferris wheel (20 s)",Car="Climb aboard the art car",DanceAndChat="Dance with festivalgoer  •  F CHAT",Cancel="Cancel current action";
            // The pause menu's CANCEL ACTION button.
            bool CancelShown(){foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.name=="CANCEL ACTION")return b.gameObject.activeSelf;return false;}
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("twist_mate","Sam");mate.Ready=true;
                sim.State.FestivalIndex=Festivals.PoloFestival;sim.State.LevelIndex=1;player.X=0;player.Z=19;session.Command("Ready");
                deadline=Time.realtimeSinceStartup+90;
                while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)
                {
                    if(sim.State.Phase=="Loading"&&!mate.MapReady)sim.Execute(mate.Id,new GameCommand{Id="twist_mate_loaded",Kind="MapReady"});
                    yield return null;
                }
                Assert.That(session.State.Phase,Is.EqualTo("Playing"),"round starts: "+session.Message);
                // Sam trips and waits at the way back to camp; you are sober, with a free hand, $20, and only the guard and the cops about.
                sim.State.TripperId=mate.Id;mate.X=Festivals.CampGateX;mate.Z=Festivals.CampGateZ;
                var guard=sim.State.Npcs.Find(n=>n.Twist==FestivalSimulation.VipGuard);Assert.That(guard,Is.Not.Null,"setup: Palm Mirage's VIP rope has a guard");
                sim.State.Npcs.RemoveAll(n=>n.Kind!="Cop"&&n!=guard);
                player.Inventory.RemoveAll(i=>i.ItemId!="little_spoon");player.EquippedItemId="";player.Cash=20;
                bool Banded()=>player.Inventory.Exists(i=>i.ItemId==FestivalSimulation.VipWristband);

                // Beside the VIP guard: E or F talks you past the rope, and F is no plain chat here.
                guard.Mode="Blending";player.X=guard.X+.6f;player.Z=guard.Z;
                yield return new WaitForSeconds(.6f);
                Expect("sober beside the VIP guard","Prompt",Rope);
                if(Actions().Contains(DanceAndChat))failures.Add("sober beside the VIP guard: the dance still offers F as a plain chat: \""+Actions()+"\"");
                PressF();yield return null;
                if(!sim.State.Interactions.Exists(i=>i.PlayerId==player.Id&&i.Kind=="ConfirmChat"&&i.TargetId==guard.Id&&i.Status=="Active"))failures.Add("sober beside the VIP guard: F starts no chat with them ("+session.Message+")");
                yield return new WaitForSeconds((float)FestivalSimulation.ConfirmChatSeconds+.4f);
                if(!Banded())failures.Add("the guard's chat is over and there is no VIP wristband ("+session.Message+")");

                // POLO-2: at the VIP stall's end of the night market's shelves, eyeing the nearest shelf: E stays the shelf's, and
                // the wristband is still listed.
                player.Inventory.RemoveAll(i=>i.ItemId==FestivalSimulation.VipWristband);player.X=Festivals.VipStallX-2.2f;player.Z=Festivals.VipStallZ;
                var shelf=Catalog.ShopPoint(false,3);var eyed=Catalog.FindItem(sim.State.VendorOffers[3]);var eyedStock=sim.State.ShopStock.Find(s=>s.ItemId==eyed.Id);
                Assert.That(FestivalSimulation.AtVipStall(sim.State,player)&&Vector2.Distance(new Vector2(player.X,player.Z),new Vector2(shelf.X,shelf.Z))<3.3f&&eyedStock!=null,Is.True,"setup: at the VIP stall and in reach of a stocked shelf");
                eyedStock.MarketAvailable=Mathf.Max(1,eyedStock.MarketAvailable);
                typeof(FestivalSession).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,Mathf.Atan2(shelf.X-player.X,shelf.Z-player.Z)*Mathf.Rad2Deg);
                typeof(FestivalSession).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,-Mathf.Atan2(1.17f-1.65f,Vector2.Distance(new Vector2(player.X,player.Z),new Vector2(shelf.X,shelf.Z)))*Mathf.Rad2Deg);
                yield return new WaitForSeconds(.6f);
                if(!Read("Prompt").StartsWith("BUY "+eyed.Name.ToUpperInvariant()+"  •  "))failures.Add("eyeing a shelf at the VIP stall: the prompt reads \""+Read("Prompt")+"\", not the shelf's");
                if(!Actions().Contains(Band))failures.Add("eyeing a shelf at the VIP stall: the wristband is not listed: \""+Actions()+"\"");
                PressE();yield return null;
                if(Banded()||player.Cash!=20)failures.Add("eyeing a shelf at the VIP stall: E buys a wristband instead of picking the shelf (cash $"+player.Cash+")");

                // At the night market's VIP stall: E buys a wristband.
                player.X=Festivals.VipStallX;player.Z=Festivals.VipStallZ+1;
                yield return new WaitForSeconds(.6f);
                Expect("at the VIP stall","Prompt",Band);
                PressE();yield return null;
                if(!Banded()||player.Cash!=5)failures.Add("at the VIP stall: E buys no wristband for $15 (cash $"+player.Cash+", "+session.Message+")");

                // Beside an Ember Playa art car as it rolls up: E climbs aboard; then you hop off.
                sim.State.FestivalIndex=Festivals.PlayaFestival;
                var car=Festivals.ArtCarAt(0,sim.State.ElapsedSeconds+.6);player.X=car.X;player.Z=car.Z;
                yield return new WaitForSeconds(.6f);
                Expect("beside an art car","Prompt",Car);
                PressE();yield return null;
                if(FestivalSimulation.ArtCarOf(sim.State,player.Id)!=0)failures.Add("beside an art car: E puts you aboard no car ("+session.Message+")");
                // Aboard, you can still hop off.
                yield return new WaitForSeconds(.6f);
                if(!Actions().Contains(Cancel)||!CancelShown())failures.Add("aboard an art car: there is no way to hop off: \""+Actions()+"\"");
                session.Command("Cancel");sim.State.FestivalIndex=Festivals.PoloFestival;

                // At the Ferris wheel's base on Night 1, the trail not yet followed and the friend far away: E boards it.
                var friend=sim.State.FriendPosition;
                Assert.That(sim.State.GateOpened,Is.False,"setup: the trail is not yet followed");
                Assert.That(Vector2.Distance(new Vector2(Festivals.WheelX,Festivals.WheelZ),new Vector2(friend.X,friend.Z)),Is.GreaterThan(12),"setup: the lost friend is far out of sight of the wheel");
                player.X=Festivals.WheelX;player.Z=Festivals.WheelZ+1.5f;
                yield return new WaitForSeconds(.6f);
                Expect("at the Ferris wheel's base","Prompt",Wheel);
                PressE();yield return null;
                if(!FestivalSimulation.OnWheel(sim.State,player.Id))failures.Add("at the Ferris wheel's base: E boards no ride ("+session.Message+")");
                yield return new WaitForSeconds(.6f);
                // POLO-2: up there nothing cancels the ride, so nothing offers to; the prompt counts down the turn and E does nothing.
                string riding=Read("Prompt");
                if(!System.Text.RegularExpressions.Regex.IsMatch(riding,@"^ON THE FERRIS WHEEL  •  (1?[0-9]|20) s$"))failures.Add("on the wheel: the prompt reads \""+riding+"\"");
                Fits("on the wheel","Prompt");
                if(primary.GetValue(view)!=null)failures.Add("on the wheel: E still runs an action");
                if(Actions().Contains(Cancel)||CancelShown())failures.Add("on the wheel: Cancel is still offered: \""+Actions()+"\", menu button "+(CancelShown()?"shown":"hidden"));
                string detail=Read("Objective detail");
                if(!detail.StartsWith("From the wheel you spot your friend ")||!detail.Contains(" and security "))failures.Add("on the wheel at night: the objective card reads \""+detail+"\"");
                Fits("on the wheel at night","Objective detail");
                var shown=GameObject.Find("mission_friend");
                if(shown==null)failures.Add("on the wheel at night: the lost friend is not drawn");
                else if(Vector2.Distance(new Vector2(shown.transform.position.x,shown.transform.position.z),new Vector2(friend.X,friend.Z))>.5f)failures.Add("on the wheel at night: the lost friend is drawn at "+shown.transform.position+", not where they are lost");
                // By day nobody is lost: the rider spots security only, over the quota line.
                sim.State.LevelIndex=0;
                yield return new WaitForSeconds(.6f);
                detail=Read("Objective detail");
                if(!detail.StartsWith("From the wheel you spot security "))failures.Add("on the wheel by day: the objective card reads \""+detail+"\"");
                if(GameObject.Find("mission_friend")!=null)failures.Add("on the wheel by day: a lost friend is drawn");
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Twists a sober player uses from the HUD:\n"+string.Join("\n",failures));
        }

        // PLAYA-2: on Ember Playa money is odd objects, and the longest ("10 friendship bracelets") beside a 24-letter crew name
        // overran the crew action buttons and the session notice at 1080p. Every crew action at the holding desk, lost property,
        // medical and the shared stash shows its whole label, the paid release naming the friend it frees, and a refused release
        // shows the whole notice of where to go. Labels are laid out on a 1920x1080 canvas, so a cut-off line fails.
        [UnityTest]public IEnumerator LongPlayaMoneyLabelsFitTheCrewActionsAndNotice()
        {
            var world=new GameObject("Playa HUD world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Playa HUD");
            var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();
            yield return null;
            var failures=new List<string>();
            void Fits(string where,Text text)
            {
                Canvas.ForceUpdateCanvases();
                int shown=text.cachedTextGenerator.characterCountVisible;
                if(shown<text.text.Length)failures.Add(where+": box "+text.rectTransform.rect.size+" cuts \""+text.text+"\" after \""+text.text.Substring(0,shown)+"\"");
            }
            List<Text> Actions(){var listed=new List<Text>();foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.name.StartsWith("Action:"))listed.Add(b.transform.Find("Text").GetComponent<Text>());return listed;}
            string Listed(){var labels=new List<string>();foreach(var t in Actions())labels.Add(t.text);return string.Join(" | ",labels);}
            void AllFit(string where,string mustList)
            {
                if(!Listed().Contains(mustList))failures.Add(where+": \""+mustList+"\" is not listed: \""+Listed()+"\"");
                foreach(var label in Actions())Fits(where,label);
            }
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mate=sim.AddPlayer("playa_mate","Persephone Wintersbottom");mate.Ready=true;
                Assert.That(mate.Name.Length,Is.EqualTo(24),"setup: the crew name is as long as a name can be");
                sim.State.FestivalIndex=Festivals.PlayaFestival;sim.State.LevelIndex=0;
                yield return StartLevel(session);
                // You pay in friendship bracelets, the longest odd object, and only the cops are about.
                player.Ordinal=3;player.Cash=40;sim.State.StashCash=10;
                sim.State.Npcs.RemoveAll(n=>n.Kind!="Cop");
                string ten=FestivalHudText.Money(sim.State,player,10);
                Assert.That(ten,Is.EqualTo("10 friendship bracelets"),"setup: you pay in friendship bracelets");

                // Away from both desks, a paid release is refused, and the notice says where to go.
                mate.Life="Detained";mate.X=27;mate.Z=5;player.X=0;player.Z=0;
                session.Command("BeginRelease",mate.Id,amount:1);
                yield return null;yield return null;
                Text notice=null;foreach(var text in hud.GetComponentsInChildren<Text>(false))if(text.name=="Notice")notice=text;
                if(notice==null)failures.Add("the release refusal: no notice is shown ("+session.Message+")");
                else if(!notice.text.StartsWith("Release: "+ten+" at holding desk"))failures.Add("the release refusal: the notice reads \""+notice.text+"\"");
                else Fits("the release refusal",notice);

                // The crew list is open on the pause menu.
                session.MenuOpen=true;yield return null;
                foreach(var b in hud.GetComponentsInChildren<Button>(false))if(b.name=="CREW + NEARBY")b.onClick.Invoke();
                // At the holding desk beside your detained friend.
                player.X=26.4f;player.Z=5;
                yield return new WaitForSeconds(.6f);
                AllFit("at the holding desk","Pay "+ten+" release for "+mate.Name);
                // At lost property, your friend still held.
                player.X=-28;player.Z=16;
                yield return new WaitForSeconds(.6f);
                AllFit("at lost property","Free release task for "+mate.Name);
                // At the shared stash, your friend downed beside you.
                mate.Life="Downed";mate.X=-25.6f;mate.Z=-8;player.X=-25;player.Z=-8.6f;
                yield return new WaitForSeconds(.6f);
                AllFit("at the shared stash beside a downed friend","Deposit "+FestivalHudText.Money(sim.State,player,5)+" in shared stash");
                // At medical, holding your friend's wristband after they died.
                mate.Life="Spirit";mate.X=24;mate.Z=-20;player.Wristbands.Add(mate.Id);player.X=24.4f;player.Z=-20;
                yield return new WaitForSeconds(.6f);
                AllFit("at medical with a friend's wristband","Revive "+mate.Name+" — pay "+ten);
            }
            finally
            {
                session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(hud);Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Ember Playa's long money labels at 1080p:\n"+string.Join("\n",failures));
        }
    }
}
