#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>Explicit opt-in native transport/render smoke test. Absent from release builds.</summary>
    public sealed class DevelopmentSmoke : MonoBehaviour
    {
        Camera captureCamera;
        Vector3 capturePosition=new Vector3(0,2.3f,-10);
        Quaternion captureRotation=Quaternion.Euler(8,0,0);
        readonly float[] rhythmFrameMs=new float[512];
        int rhythmFrameCount;
        bool recordRhythmFrames;
        void LateUpdate()
        {
            if(captureCamera!=null){captureCamera.transform.position=capturePosition;captureCamera.transform.rotation=captureRotation;}
            if(recordRhythmFrames&&rhythmFrameCount<rhythmFrameMs.Length)rhythmFrameMs[rhythmFrameCount++]=Time.unscaledDeltaTime*1000f;
        }
        IEnumerator Start()
        {
            var session=GetComponent<FestivalSession>();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--solo-smoke-test")>=0)
            {
                yield return SoloSmoke(session);
                yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--ui-screens")>=0)
            {
                yield return new WaitForSeconds(.7f);
                string interfaceDir=Path.Combine(Application.persistentDataPath,"smoke");Directory.CreateDirectory(interfaceDir);
                string connectionPath=Path.Combine(interfaceDir,"connection.png");
                if(File.Exists(connectionPath))File.Delete(connectionPath);
                ScreenCapture.CaptureScreenshot(connectionPath);
                yield return new WaitForSeconds(.7f);
                if(!File.Exists(connectionPath)){Fail("connection render");yield break;}
                Debug.Log("FESTIVAL SMOKE CONNECTION RENDER PASSED: "+connectionPath);
                transform.Find("Festival HUD/Connection/PORT").GetComponent<InputField>().text="0";
                transform.Find("Festival HUD/Connection/JOIN GAME").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.15f);
                string connectionErrorPath=Path.Combine(interfaceDir,"connection-error.png");
                if(File.Exists(connectionErrorPath))File.Delete(connectionErrorPath);
                ScreenCapture.CaptureScreenshot(connectionErrorPath);
                yield return new WaitForSeconds(.6f);
                if(!File.Exists(connectionErrorPath)){Fail("connection error render");yield break;}
                Debug.Log("FESTIVAL SMOKE CONNECTION ERROR RENDER PASSED: "+connectionErrorPath);
                transform.Find("Festival HUD/Connection/PORT").GetComponent<InputField>().text="17781";
                transform.Find("Festival HUD/Connection/CREATE GAME").GetComponent<Button>().onClick.Invoke();
                float createDeadline=Time.realtimeSinceStartup+12;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<createDeadline)yield return null;
                if(session.LocalPlayer==null||session.State.Phase!="Shopping"||session.MenuOpen){Fail("menu create enters campsite");yield break;}
                yield return new WaitForSeconds(.3f);
                string createdCampPath=Path.Combine(interfaceDir,"created-camp.png");
                if(File.Exists(createdCampPath))File.Delete(createdCampPath);
                ScreenCapture.CaptureScreenshot(createdCampPath);
                yield return new WaitForSeconds(.7f);
                if(!File.Exists(createdCampPath)){Fail("menu create camp render");yield break;}
                Debug.Log("FESTIVAL SMOKE MENU CREATE PASSED: "+createdCampPath);
                Application.Quit(0);
                yield break;
            }
            float deadline=Time.realtimeSinceStartup+85+(float)FestivalSimulation.SpinSeconds;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            if(session.LocalPlayer==null){Debug.LogError("FESTIVAL SMOKE FAILED: no local identity");Application.Quit(2);yield break;}
            var festivalFont=Resources.Load<Font>("FestivalDisplay");
            if(festivalFont==null){Fail("festival display font");yield break;}
            foreach(var label in transform.Find("Festival HUD").GetComponentsInChildren<Text>(true))
                if(label.font!=festivalFont){Fail("interface font on "+label.name);yield break;}
            int wideEyes=0,redEyes=0,combinedEyes=0;
            foreach(var npc in session.State.Npcs)if(npc.Kind=="Wook")
            {
                if(npc.HighlyIntoxicated)wideEyes++;
                if(npc.RedEyes)redEyes++;
                if(npc.HighlyIntoxicated&&npc.RedEyes)combinedEyes++;
            }
            if(wideEyes<3||redEyes<4||combinedEyes<1){Fail("replicated crowd eye states");yield break;}
            Debug.Log("FESTIVAL SMOKE EYE STATES PASSED: wide="+wideEyes+" red="+redEyes+" both="+combinedEyes);
            var sim=session.DevelopmentSimulation;
            if(session.IsHost)
            {
                while(sim.State.Players.Count<2&&Time.realtimeSinceStartup<deadline)yield return null;
                if(sim.State.Players.Count<2){Fail("camp peers");yield break;}
                sim.State.Players[0].X=-1;sim.State.Players[0].Z=6;
                sim.State.Players[1].X=1;sim.State.Players[1].Z=6;
            }
            float campX=session.IsHost?-1:1;
            while(!Near(session.LocalPlayer,campX,6)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Time.realtimeSinceStartup>=deadline){Fail("camp placement");yield break;}
            var campMenu=transform.Find("Festival HUD/Festival pass");
            var campGoods=FindFirstObjectByType<FestivalWorld>()?.transform.Find(FestivalWorld.CampRootName+"/Campsite shelf goods");
            if(campMenu==null||campMenu.Find("Pass home/RESUME FESTIVAL")==null||campMenu.Find("Pass home/SETTINGS")==null||campGoods==null||campGoods.childCount!=session.State.VendorOffers.Count)
            {Fail("physical camp shop and pass controls");yield break;}
            session.MenuOpen=false;
            captureCamera=session.ViewCamera;
            capturePosition=new Vector3(0,2.1f,-1);
            captureRotation=Quaternion.LookRotation(new Vector3(0,1.35f,9)-capturePosition);
            yield return new WaitForSeconds(.3f);
            string shopDir=Path.Combine(Application.persistentDataPath,"smoke");Directory.CreateDirectory(shopDir);
            string shopPath=Path.Combine(shopDir,session.IsHost?"host-camp-shop.png":"client-camp-shop.png");
            if(File.Exists(shopPath))File.Delete(shopPath);
            ScreenCapture.CaptureScreenshot(shopPath);
            yield return new WaitForSeconds(.7f);
            if(!File.Exists(shopPath)){Fail("camp shop render");yield break;}
            Debug.Log("FESTIVAL SMOKE CAMP SHOP PASSED: "+shopPath);
            session.MenuOpen=true;
            yield return new WaitForSeconds(.2f);
            var passPath=Path.Combine(shopDir,session.IsHost?"host-pass.png":"client-pass.png");
            if(File.Exists(passPath))File.Delete(passPath);
            ScreenCapture.CaptureScreenshot(passPath);
            yield return new WaitForSeconds(.6f);
            if(!File.Exists(passPath)){Fail("festival pass render");yield break;}
            Debug.Log("FESTIVAL SMOKE PASS RENDER PASSED: "+passPath);
            campMenu.Find("Pass home/SETTINGS").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.2f);
            var settingsPath=Path.Combine(shopDir,session.IsHost?"host-settings.png":"client-settings.png");
            if(File.Exists(settingsPath))File.Delete(settingsPath);
            ScreenCapture.CaptureScreenshot(settingsPath);
            yield return new WaitForSeconds(.6f);
            if(!File.Exists(settingsPath)){Fail("settings render");yield break;}
            Debug.Log("FESTIVAL SMOKE SETTINGS RENDER PASSED: "+settingsPath);
            campMenu.Find("Pass settings/BACK TO PASS").GetComponent<Button>().onClick.Invoke();
            session.MenuOpen=false;
            var interfaceHud=GetComponent<FestivalHud>();
            interfaceHud.DevelopmentMapVisible=true;
            yield return new WaitForSeconds(.2f);
            var mapPath=Path.Combine(shopDir,session.IsHost?"host-map.png":"client-map.png");
            if(File.Exists(mapPath))File.Delete(mapPath);
            ScreenCapture.CaptureScreenshot(mapPath);
            yield return new WaitForSeconds(.6f);
            interfaceHud.DevelopmentMapVisible=false;
            if(!File.Exists(mapPath)){Fail("map render");yield break;}
            Debug.Log("FESTIVAL SMOKE MAP RENDER PASSED: "+mapPath);
            captureCamera=null;
            if(session.IsHost)
            {
                var spoon=Catalog.ShopPoint(true,session.State.VendorOffers.IndexOf("little_spoon"));
                sim.State.Players[0].X=spoon.X-.4f;sim.State.Players[0].Z=spoon.Z-1;
                sim.State.Players[1].X=spoon.X+.4f;sim.State.Players[1].Z=spoon.Z-1;
            }
            var shelfPoint=Catalog.ShopPoint(true,session.State.VendorOffers.IndexOf("little_spoon"));
            while(!Within(session.LocalPlayer,shelfPoint.X,shelfPoint.Z,2.8f)&&Time.realtimeSinceStartup<deadline)yield return null;
            session.Command("HoldOffer",item:"little_spoon");
            while(session.LocalPlayer.HeldOfferId!="little_spoon"&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Time.realtimeSinceStartup>=deadline){Fail("shared shelf pickup");yield break;}
            yield return new WaitForSeconds(.15f);
            string heldPath=Path.Combine(shopDir,session.IsHost?"host-held.png":"client-held.png");
            if(File.Exists(heldPath))File.Delete(heldPath);
            ScreenCapture.CaptureScreenshot(heldPath);
            yield return new WaitForSeconds(.6f);
            if(!File.Exists(heldPath)){Fail("held prop render");yield break;}
            Debug.Log("FESTIVAL SMOKE HELD RENDER PASSED: "+heldPath);
            if(session.IsHost)
            {
                while(!sim.State.Players.TrueForAll(p=>p.HeldOfferId=="little_spoon")&&Time.realtimeSinceStartup<deadline)yield return null;
                if(Time.realtimeSinceStartup>=deadline){Fail("two peers reserved separate shelf copies");yield break;}
                sim.State.Players[0].X=-1;sim.State.Players[0].Z=6;sim.State.Players[1].X=1;sim.State.Players[1].Z=6;
            }
            while(!Near(session.LocalPlayer,campX,6)&&Time.realtimeSinceStartup<deadline)yield return null;
            session.Command("Buy",item:"little_spoon");
            while(!session.LocalPlayer.Inventory.Exists(item=>item.ItemId=="little_spoon")&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Time.realtimeSinceStartup>=deadline){Fail("camp spoon purchase");yield break;}
            if(session.LocalPlayer.HeldOfferId!=""){Fail("purchase still held");yield break;}
            if(session.IsHost)
            {
                while(!sim.State.Players.TrueForAll(p=>p.Inventory.Exists(item=>item.ItemId=="little_spoon"))&&Time.realtimeSinceStartup<deadline)yield return null;
                if(Time.realtimeSinceStartup>=deadline){Fail("two camp purchases");yield break;}
                var site=CampFeatures.Find("tent_1");
                foreach(var peer in sim.State.Players)
                {
                    peer.X=site.X;peer.Z=site.Z;
                    if(!sim.Execute(peer.Id,new GameCommand{Id="smoke-enter-"+peer.Id,Kind="EnterCamp",TargetId=site.Id}).Accepted){Fail("interior handoff entry");yield break;}
                }
                sim.State.Players[0].Inventory.Add(new ItemStack{ItemId="merch_bag",Count=1});
                session.Command("Transfer",sim.State.Players[1].Id,"merch_bag",1);
            }
            while(session.LocalPlayer.CampVisitId!="tent_1"||session.State.Transfers.Count==0)
            {
                if(Time.realtimeSinceStartup>=deadline){Fail("interior handoff offer");yield break;}
                yield return null;
            }
            yield return new WaitForSeconds(.3f);
            if(!ShowsHandoffAction(transform.Find("Festival HUD"),session.IsHost)){Fail("interior handoff action "+HandoffAction(session.IsHost));yield break;}
            var transferPath=Path.Combine(shopDir,session.IsHost?"host-interior-handoff.png":"client-interior-handoff.png");
            if(File.Exists(transferPath))File.Delete(transferPath);
            ScreenCapture.CaptureScreenshot(transferPath);yield return new WaitForSeconds(.55f);
            if(!File.Exists(transferPath)){Fail("interior handoff render");yield break;}
            if(!session.IsHost)session.Command("AcceptTransfer",session.State.Transfers[0].Id);
            while(session.State.Transfers.Count>0||!(session.IsHost?sim.State.Players[1].Inventory:session.LocalPlayer.Inventory).Exists(item=>item.ItemId=="merch_bag"))
            {
                if(Time.realtimeSinceStartup>=deadline){Fail("interior handoff acceptance");yield break;}
                yield return null;
            }
            Debug.Log("FESTIVAL SMOKE INTERIOR HANDOFF PASSED: "+transferPath);
            if(session.IsHost)
            {
                yield return new WaitForSeconds(.8f);
                foreach(var peer in sim.State.Players)
                    if(!sim.Execute(peer.Id,new GameCommand{Id="smoke-exit-"+peer.Id,Kind="ExitCamp"}).Accepted){Fail("interior handoff exit");yield break;}
                sim.State.Players[0].X=-2;sim.State.Players[0].Z=-13;
                sim.State.Players[1].X=2;sim.State.Players[1].Z=-13;
            }
            while(session.LocalPlayer.CampVisitId!=""&&Time.realtimeSinceStartup<deadline)yield return null;
            if(session.LocalPlayer.CampVisitId!=""){Fail("interior handoff exit replication");yield break;}
            float overviewX=session.IsHost?-2:2;
            while(!Near(session.LocalPlayer,overviewX,-13)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Time.realtimeSinceStartup>=deadline){Fail("camp overview placement");yield break;}
            yield return new WaitForSeconds(.4f);
            string campDir=Path.Combine(Application.persistentDataPath,"smoke");Directory.CreateDirectory(campDir);
            string campPath=Path.Combine(campDir,session.IsHost?"host-camp.png":"client-camp.png");
            ScreenCapture.CaptureScreenshot(campPath);
            yield return new WaitForSeconds(.7f);
            if(!File.Exists(campPath)){Fail("camp render");yield break;}
            Debug.Log("FESTIVAL SMOKE CAMP PASSED: "+campPath);
            captureCamera=session.ViewCamera;
            capturePosition=new Vector3(0,19,-31);
            captureRotation=Quaternion.LookRotation(new Vector3(0,0,0)-capturePosition);
            yield return new WaitForSeconds(.2f);
            var overviewPath=Path.Combine(campDir,session.IsHost?"host-camp-overview.png":"client-camp-overview.png");
            ScreenCapture.CaptureScreenshot(overviewPath);
            yield return new WaitForSeconds(.7f);
            captureCamera=null;
            if(!File.Exists(overviewPath)){Fail("camp overview render");yield break;}
            Debug.Log("FESTIVAL SMOKE CAMP OVERVIEW PASSED: "+overviewPath);
            if(session.IsHost)
            {
                captureCamera=session.ViewCamera;
                capturePosition=new Vector3(-18.5f,1.9f,-19f);
                captureRotation=Quaternion.LookRotation(new Vector3(-14,1.0f,-15)-capturePosition);
                yield return new WaitForSeconds(.3f);
                var carPath=Path.Combine(campDir,"host-camp-car.png");
                ScreenCapture.CaptureScreenshot(carPath);
                yield return new WaitForSeconds(.7f);
                captureCamera=null;
                if(!File.Exists(carPath)){Fail("camp car render");yield break;}
                Debug.Log("FESTIVAL SMOKE CAMP CAR PASSED: "+carPath);
                captureCamera=session.ViewCamera;
                capturePosition=new Vector3(-13f,2.4f,-30f);
                captureRotation=Quaternion.LookRotation(new Vector3(-8,1.2f,-25)-capturePosition);
                yield return new WaitForSeconds(.3f);
                var vanPath=Path.Combine(campDir,"host-camp-van.png");
                ScreenCapture.CaptureScreenshot(vanPath);
                yield return new WaitForSeconds(.7f);
                captureCamera=null;
                if(!File.Exists(vanPath)){Fail("camp van render");yield break;}
                Debug.Log("FESTIVAL SMOKE CAMP VAN PASSED: "+vanPath);
                captureCamera=session.ViewCamera;
                capturePosition=new Vector3(-31f,2.7f,11f);
                captureRotation=Quaternion.LookRotation(new Vector3(-27,1.0f,17)-capturePosition);
                yield return new WaitForSeconds(.3f);
                var domePath=Path.Combine(campDir,"host-camp-dome-tent.png");
                ScreenCapture.CaptureScreenshot(domePath);
                yield return new WaitForSeconds(.7f);
                captureCamera=null;
                if(!File.Exists(domePath)){Fail("camp dome tent render");yield break;}
                Debug.Log("FESTIVAL SMOKE CAMP DOME TENT PASSED: "+domePath);
            }
            if(session.IsHost){sim.State.Players[0].X=-1;sim.State.Players[0].Z=19;sim.State.Players[1].X=1;sim.State.Players[1].Z=19;}
            while(!Within(session.LocalPlayer,0,19,3.2f)&&Time.realtimeSinceStartup<deadline)yield return null;
            session.Command("Ready");
            while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)
            {
                yield return new WaitForSeconds(.2f);
            }
            if(session.State.Phase!="Playing"||session.State.Players.Count!=2){Debug.LogError("FESTIVAL SMOKE FAILED: two-player loading barrier");Application.Quit(3);yield break;}
            Debug.Log("FESTIVAL SMOKE TRANSPORT PASSED: two peers ready and playing; host="+session.IsHost);
            var crowd=FindFirstObjectByType<FestivalAmbientCrowd>();
            if(crowd==null||crowd.MemberCount!=62||crowd.WalkerCount!=12){Fail("festival crowd population");yield break;}
            var walkerStart=crowd.FirstWalkerPosition;
            yield return new WaitForSeconds(.35f);
            if(Vector3.Distance(walkerStart,crowd.FirstWalkerPosition)<.15f){Fail("festival crowd walking");yield break;}
            Debug.Log("FESTIVAL SMOKE CROWD PASSED: "+crowd.MemberCount+" ambient attendees, "+crowd.WalkerCount+" moving walkers; host="+session.IsHost);
            var previousPosition=capturePosition;var previousRotation=captureRotation;
            captureCamera=session.ViewCamera;
            capturePosition=new Vector3(0,2.1f,5);
            captureRotation=Quaternion.LookRotation(new Vector3(0,1.8f,23)-capturePosition);
            yield return new WaitForSeconds(.3f);
            string liveCrowdDir=Path.Combine(Application.persistentDataPath,"smoke");Directory.CreateDirectory(liveCrowdDir);
            var liveCrowdPath=Path.Combine(liveCrowdDir,session.IsHost?"host-crowd-live.png":"client-crowd-live.png");
            ScreenCapture.CaptureScreenshot(liveCrowdPath);
            yield return new WaitForSeconds(.7f);
            if(!File.Exists(liveCrowdPath)){Fail("live crowd render");yield break;}
            Debug.Log("FESTIVAL SMOKE CROWD LIVE PASSED: "+liveCrowdPath);
            capturePosition=previousPosition;captureRotation=previousRotation;
            session.Command("Buy",item:"medical_voucher");
            yield return new WaitForSeconds(.2f);
            if(session.LocalPlayer.Inventory.Exists(item=>item.ItemId=="medical_voucher")){Fail("remote night market purchase");yield break;}
            Debug.Log("FESTIVAL SMOKE MARKET RANGE PASSED: remote purchase blocked; host="+session.IsHost);
            // Only test actor placement is privileged. The client uses the
            // ordinary command channel for every mission action and note.
            if(session.IsHost)
            {
                // Give the client time to verify remote browsing at the normal entry
                // before host-only test placement moves both peers to the market.
                yield return new WaitForSeconds(1f);
                sim.State.Npcs.RemoveAll(n=>n.Id!="wook_0");
                PlaceBoth(sim,-15.75f,-22);
                sim.Player(sim.State.HostPlayerId).X=-15;
                // The spinner picks the tripper; the smoke makes the client the dose-1 tripper so the host stays the sober helper.
                var reader=sim.State.Players.Find(p=>p.Id!=sim.State.HostPlayerId);
                foreach(var p in sim.State.Players)p.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);
                reader.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="smoke_dose",Intensity=1,RemainingSeconds=sim.State.DurationSeconds});
                sim.State.TripperId=reader.Id;sim.State.Doses.Clear();sim.State.Doses.Add(new PlayerDose{PlayerId=reader.Id,Dose=1});
            }
            else
            {
                while(!Near(session.LocalPlayer,-15.75f,-22)&&Time.realtimeSinceStartup<deadline)yield return null;
                if(Time.realtimeSinceStartup>=deadline){Fail("night market placement");yield break;}
                var marketGoods=FindFirstObjectByType<FestivalWorld>()?.transform.Find(FestivalWorld.RootName+"/Night market goods");
                if(marketGoods==null||marketGoods.childCount!=session.State.VendorOffers.Count){Fail("night market physical display");yield break;}
                session.Command("Buy",item:"medical_voucher");
                while(!session.LocalPlayer.Inventory.Exists(item=>item.ItemId=="medical_voucher")&&Time.realtimeSinceStartup<deadline)yield return null;
                if(Time.realtimeSinceStartup>=deadline){Fail("night market direct purchase");yield break;}
                captureCamera=session.ViewCamera;
                foreach(var location in new[]{
                    new {Name="medical",Camera=new Vector3(18,2.2f,-23),Target=new Vector3(24,1.7f,-17)},
                    new {Name="security",Camera=new Vector3(21,2.2f,2),Target=new Vector3(27,1.7f,8)},
                    new {Name="shuttle",Camera=new Vector3(8,2.2f,-30),Target=new Vector3(0,1.6f,-36)} })
                {
                    capturePosition=location.Camera;
                    captureRotation=Quaternion.LookRotation(location.Target-location.Camera);
                    yield return new WaitForSeconds(.25f);
                    var locationPath=Path.Combine(liveCrowdDir,"client-"+location.Name+".png");
                    if(File.Exists(locationPath))File.Delete(locationPath);
                    ScreenCapture.CaptureScreenshot(locationPath);
                    yield return new WaitForSeconds(.7f);
                    if(!File.Exists(locationPath)){Fail(location.Name+" location render");yield break;}
                    Debug.Log("FESTIVAL SMOKE LOCATION "+location.Name.ToUpperInvariant()+" RENDER PASSED: "+locationPath);
                }
                capturePosition=new Vector3(-18,2.2f,-24.5f);
                captureRotation=Quaternion.LookRotation(new Vector3(-18,1.5f,-20)-capturePosition);
                yield return new WaitForSeconds(.3f);
                string marketDir=Path.Combine(Application.persistentDataPath,"smoke");Directory.CreateDirectory(marketDir);
                string marketPath=Path.Combine(marketDir,"client-market.png");
                if(File.Exists(marketPath))File.Delete(marketPath);
                ScreenCapture.CaptureScreenshot(marketPath);
                yield return new WaitForSeconds(.7f);
                if(!File.Exists(marketPath)){Fail("night market UI render");yield break;}
                Debug.Log("FESTIVAL SMOKE MARKET RENDER PASSED: "+marketPath);
                captureCamera=null;
                yield return new WaitForSeconds(.2f);
                string marketApproachPath=Path.Combine(marketDir,"client-market-approach.png");
                if(File.Exists(marketApproachPath))File.Delete(marketApproachPath);
                ScreenCapture.CaptureScreenshot(marketApproachPath);
                yield return new WaitForSeconds(.7f);
                if(!File.Exists(marketApproachPath)){Fail("night market approach render");yield break;}
                Debug.Log("FESTIVAL SMOKE MARKET APPROACH PASSED: "+marketApproachPath);
            }
            while((session.IsHost?sim.State.Players.Find(p=>p.Id!=sim.State.HostPlayerId).Effects.Count:session.LocalPlayer.Effects.Count)==0&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Time.realtimeSinceStartup>=deadline){Fail("client tripper dose");yield break;}
            if(session.IsHost&&!session.State.Players.Find(p=>p.Id!=session.LocalPlayerId).VisualWideEyes){Fail("teammate intoxication face state");yield break;}
            // TRIP-2: the totems are gone. Only the tripper's client (here the client) gets visions; VISION-1 draws them and
            // SMOKE-1 drives the tripper's checks along the clue trail.
            bool shouldSeeVisions=!session.IsHost;
            while(session.State.Visions.Count>0!=shouldSeeVisions&&Time.realtimeSinceStartup<deadline)yield return null;
            if(session.State.Visions.Count>0!=shouldSeeVisions){Fail("private vision visibility");yield break;}
            Debug.Log("FESTIVAL SMOKE CLUE VISIBILITY PASSED: visible="+(session.State.Visions.Count>0)+" host="+session.IsHost);
            if(!session.IsHost)
            {
                string clueDir=Path.Combine(Application.persistentDataPath,"smoke");Directory.CreateDirectory(clueDir);
                string cluePath=Path.Combine(clueDir,"client-clue.png");if(File.Exists(cluePath))File.Delete(cluePath);
                ScreenCapture.CaptureScreenshot(cluePath);
                yield return new WaitForSeconds(.7f);
                if(!File.Exists(cluePath)){Fail("tripper vision render");yield break;}
                Debug.Log("FESTIVAL SMOKE CLUE RENDER PASSED: "+cluePath);
            }
            if(session.IsHost)
            {
                PlaceBoth(sim,0,0);
                sim.Player(sim.State.HostPlayerId).X=-2.5f;
                var dancer=sim.State.Npcs[0];dancer.X=0;dancer.Z=1;dancer.Yaw=180;
                // A dance no longer opens the way to the friend: the clue trail's last link does. Until SMOKE-1 drives the
                // tripper's checks, the host sets the finished trail directly once the client's dance is judged.
                var tripper=sim.State.Players.Find(p=>p.Id!=sim.State.HostPlayerId);
                while(!sim.State.Interactions.Exists(i=>i.PlayerId==tripper.Id&&i.Kind=="Dance"&&i.Status=="Complete")&&Time.realtimeSinceStartup<deadline)yield return null;
                sim.State.GateOpened=true;
            }
            else
            {
                while((!Near(session.LocalPlayer,0,0)||!Near(session.State.Npcs.Find(n=>n.Id=="wook_0"),0,1))&&Time.realtimeSinceStartup<deadline)yield return null;
                if(Time.realtimeSinceStartup>=deadline){Fail("performance placement");yield break;}
                session.MenuOpen=true;
                campMenu.Find("Pass home/CREW + NEARBY").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.3f);
                string actionPath=Path.Combine(Application.persistentDataPath,"smoke","client-actions.png");
                if(File.Exists(actionPath))File.Delete(actionPath);
                ScreenCapture.CaptureScreenshot(actionPath);
                yield return new WaitForSeconds(.6f);
                if(!File.Exists(actionPath)){Fail("nearby actions render");yield break;}
                Debug.Log("FESTIVAL SMOKE ACTIONS RENDER PASSED: "+actionPath);
                session.MenuOpen=false;
                session.Command("Dance","wook_0");
                InteractionState performance=null;
                while(performance==null&&Time.realtimeSinceStartup<deadline)
                {
                    performance=session.State.Interactions.Find(i=>i.PlayerId==session.LocalPlayerId&&i.Kind=="Dance"&&i.Status=="Active");
                    yield return null;
                }
                if(performance==null){Fail("dance challenge");yield break;}
                var dancePreview=GetComponent<FestivalDancePreview>();
                while((dancePreview==null||!dancePreview.IsVisible||dancePreview.Dancer==null)&&Time.realtimeSinceStartup<deadline)yield return null;
                var liveDancer=transform.Find("Festival HUD/Live dancer/Dancer window/Live character view")?.GetComponent<RawImage>();
                if(dancePreview==null||!dancePreview.IsVisible||dancePreview.Dancer==null||dancePreview.Dancer!=session.LocalWorldCharacter||dancePreview.Dancer.Pose!="Dance"||liveDancer==null||liveDancer.texture==null)
                {Fail("live dance avatar");yield break;}
                var lane=transform.Find("Festival HUD/Rhythm lane")?.GetComponent<RectTransform>();
                var dancerPanel=transform.Find("Festival HUD/Live dancer")?.GetComponent<RectTransform>();
                var controls=transform.Find("Festival HUD/Rhythm lane/Controls")?.GetComponent<Text>();
                if(lane==null||dancerPanel==null||controls==null||lane.anchorMax.x>.5f||dancerPanel.anchorMin.x<.5f||Mathf.Abs((lane.anchorMax.x-lane.anchorMin.x)-(dancerPanel.anchorMax.x-dancerPanel.anchorMin.x))>.001f||!controls.text.Contains("/ A")||!controls.text.Contains("/ D"))
                {Fail("split dance layout and WASD hint");yield break;}
                rhythmFrameCount=0;recordRhythmFrames=true;
                string rhythmPath=Path.Combine(Application.persistentDataPath,"smoke","client-rhythm.png");
                if(File.Exists(rhythmPath))File.Delete(rhythmPath);
                string judgmentPath=Path.Combine(Application.persistentDataPath,"smoke","client-rhythm-judgment.png");
                if(File.Exists(judgmentPath))File.Delete(judgmentPath);
                var chart=RhythmChart.Create(performance.ChartSeed,performance.NoteCount,performance.BeatSeconds);
                bool rhythmCaptured=false,judgmentCaptured=false;
                foreach(var note in chart.Notes)
                {
                    if(!rhythmCaptured)
                    {
                        while(session.EstimatedSimulationSeconds-performance.StartSeconds<note.TimeSeconds-.72&&Time.realtimeSinceStartup<deadline)yield return null;
                        ScreenCapture.CaptureScreenshot(rhythmPath);rhythmCaptured=true;
                    }
                    while(session.EstimatedSimulationSeconds-performance.StartSeconds<note.TimeSeconds-.02&&Time.realtimeSinceStartup<deadline)yield return null;
                    if(Time.realtimeSinceStartup>=deadline){Fail("rhythm clock");yield break;}
                    session.Command("Rhythm",direction:note.Direction,time:note.TimeSeconds);
                    if(!judgmentCaptured)
                    {
                        while((session.State.Interactions.Find(i=>i.Id==performance.Id)?.Inputs.Count??0)<1&&Time.realtimeSinceStartup<deadline)yield return null;
                        float stepDeadline=Time.realtimeSinceStartup+.25f;
                        while(session.LocalPlayer.VisualDanceStepSequence<1&&Time.realtimeSinceStartup<stepDeadline)yield return null;
                        if(session.LocalPlayer.VisualDanceStepSequence<1||session.LocalPlayer.VisualDanceStepDirection!=note.Direction)
                        {Fail("replicated dance step: seq="+session.LocalPlayer.VisualDanceStepSequence+" direction="+session.LocalPlayer.VisualDanceStepDirection+" expected="+note.Direction);yield break;}
                        var judgment=transform.Find("Festival HUD/Rhythm lane/Judgment")?.GetComponent<Text>();
                        while(judgment!=null&&judgment.text!="PERFECT"&&Time.realtimeSinceStartup<stepDeadline)yield return null;
                        if(judgment==null||judgment.text!="PERFECT"){Fail("rhythm judgment UI");yield break;}
                        ScreenCapture.CaptureScreenshot(judgmentPath);judgmentCaptured=true;
                    }
                }
                yield return null;
                recordRhythmFrames=false;
                if(!File.Exists(rhythmPath)||!File.Exists(judgmentPath)){Fail("rhythm render");yield break;}
                Debug.Log("FESTIVAL SMOKE RHYTHM RENDER PASSED: "+rhythmPath);
                Debug.Log("FESTIVAL SMOKE RHYTHM JUDGMENT RENDER PASSED: "+judgmentPath);
                if(rhythmFrameCount>0)
                {
                    var measured=new float[rhythmFrameCount];Array.Copy(rhythmFrameMs,measured,rhythmFrameCount);Array.Sort(measured);
                    Debug.Log("FESTIVAL SMOKE RHYTHM FRAME P95 "+measured[Mathf.Clamp(Mathf.CeilToInt(rhythmFrameCount*.95f)-1,0,rhythmFrameCount-1)].ToString("0.0")+" MS; MAX "+measured[rhythmFrameCount-1].ToString("0.0")+" MS; SAMPLES "+rhythmFrameCount);
                }
            }
            while(!session.State.GateOpened&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!session.State.GateOpened){Fail("networked clue trail setup");yield break;}
            if(session.IsHost)
            {
                // Nobody is lost by day (LOOP-2), so the rescue runs as Night 1 until SMOKE-1 scripts a real weekend.
                sim.State.LevelIndex=1;sim.State.FriendPosition=new WorldPoint(10,10);PlaceBoth(sim,10,10);
            }
            else
            {
                while((!Near(session.LocalPlayer,10,10)||!Near(session.State.FriendPosition,10,10))&&Time.realtimeSinceStartup<deadline)yield return null;
                if(Time.realtimeSinceStartup>=deadline){Fail("friend reveal");yield break;}
                session.Command("FindFriend");
            }
            while(!session.State.FriendFound&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!session.State.FriendFound){Fail("friend recruitment");yield break;}
            if(session.IsHost)
            {
                sim.State.FriendPosition=new WorldPoint(0,-32);PlaceBoth(sim,0,-32);
            }
            else
            {
                while((!Near(session.LocalPlayer,0,-32)||!Near(session.State.FriendPosition,0,-32))&&Time.realtimeSinceStartup<deadline)yield return null;
                if(Time.realtimeSinceStartup>=deadline){Fail("shuttle placement");yield break;}
                session.Command("Extract");
            }
            while(session.State!=null&&session.State.Phase!="Results"&&Time.realtimeSinceStartup<deadline)yield return null;
            if(session.State==null){Fail("networked mission settlement: session closed, "+session.Message);yield break;}
            if(session.State.Result!="Success"||!session.State.GateOpened||session.State.Survivors!=2||session.State.SurvivorBonus!=10)
            {
                var s=session.State;
                var active=s.Interactions.Find(i=>i.Kind=="Extract");
                Fail("networked mission settlement: phase="+s.Phase+" result="+s.Result+" trail="+s.GateOpened+" survivors="+s.Survivors+" bonus="+s.SurvivorBonus+" elapsed="+s.ElapsedSeconds.ToString("F1")+" extract="+(active==null?"none":active.Status)+" message="+session.Message);
                yield break;
            }
            Debug.Log("FESTIVAL SMOKE MISSION PASSED: clue trail (set directly until SMOKE-1), dance, friend, extraction, two survivors; host="+session.IsHost);
            session.MenuOpen=true;
            // Move the view only, leaving the authority/player positions untouched.
            captureCamera=session.ViewCamera;
            capturePosition=new Vector3(0,2.3f,-10);
            captureRotation=Quaternion.Euler(8,0,0);
            session.ViewCamera.transform.position=new Vector3(0,3,-10);
            session.ViewCamera.transform.rotation=Quaternion.Euler(8,0,0);
            var preview=FestivalCharacter.Create(transform,"Festival preview 1",Color.white);
            preview.transform.position=new Vector3(-2,0,-6);preview.transform.rotation=Quaternion.identity;preview.Pose="Dance";
            var second=FestivalCharacter.Create(transform,"Festival preview 2",Color.white);
            second.transform.position=new Vector3(0,0,-6);second.transform.rotation=Quaternion.Euler(0,180,0);second.Pose="Rescue";
            var third=FestivalCharacter.Create(transform,"Festival preview 3",Color.white);
            third.transform.position=new Vector3(2,0,-6);third.transform.rotation=Quaternion.Euler(0,180,0);third.Pose="ReadClue";
            var hud=GetComponent<FestivalHud>();if(hud!=null)hud.enabled=false;
            foreach(var canvas in GetComponentsInChildren<Canvas>())canvas.enabled=false;
            yield return new WaitForSeconds(1);
            string dir=Path.Combine(Application.persistentDataPath,"smoke");Directory.CreateDirectory(dir);
            string path=Path.Combine(dir,session.IsHost?"host.png":"client.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(2);
            if(!File.Exists(path)){Debug.LogError("FESTIVAL SMOKE FAILED: screenshot missing");Application.Quit(4);yield break;}
            Debug.Log("FESTIVAL SMOKE RENDER PASSED: "+path);
            Destroy(preview.gameObject);Destroy(second.gameObject);Destroy(third.gameObject);
            yield return null;
            session.MenuOpen=false;
            captureCamera.fieldOfView=67;
            capturePosition=new Vector3(0,2.1f,5);
            captureRotation=Quaternion.LookRotation(new Vector3(0,1.8f,23)-capturePosition);
            yield return new WaitForSeconds(.35f);
            var crowdStagePath=Path.Combine(dir,session.IsHost?"host-crowd-stage.png":"client-crowd-stage.png");
            ScreenCapture.CaptureScreenshot(crowdStagePath);
            yield return new WaitForSeconds(1);
            if(!File.Exists(crowdStagePath)){Fail("stage crowd render");yield break;}
            Debug.Log("FESTIVAL SMOKE CROWD STAGE PASSED: "+crowdStagePath);
            capturePosition=new Vector3(0,1.9f,14);
            captureRotation=Quaternion.LookRotation(new Vector3(0,3.3f,32)-capturePosition);
            yield return new WaitForSeconds(.35f);
            var stageDetailPath=Path.Combine(dir,session.IsHost?"host-stage-detail.png":"client-stage-detail.png");
            if(File.Exists(stageDetailPath))File.Delete(stageDetailPath);
            ScreenCapture.CaptureScreenshot(stageDetailPath);
            yield return new WaitForSeconds(.7f);
            if(!File.Exists(stageDetailPath)){Fail("stage detail render");yield break;}
            Debug.Log("FESTIVAL SMOKE STAGE DETAIL PASSED: "+stageDetailPath);
            capturePosition=new Vector3(2.4f,2.8f,26.1f);
            captureRotation=Quaternion.LookRotation(new Vector3(0,2.17f,30.3f)-capturePosition);
            yield return new WaitForSeconds(.35f);
            var djContactPath=Path.Combine(dir,session.IsHost?"host-dj-contact.png":"client-dj-contact.png");
            if(File.Exists(djContactPath))File.Delete(djContactPath);
            ScreenCapture.CaptureScreenshot(djContactPath);
            yield return new WaitForSeconds(.7f);
            if(!File.Exists(djContactPath)){Fail("DJ contact render");yield break;}
            Debug.Log("FESTIVAL SMOKE DJ CONTACT PASSED: "+djContactPath);
            var playerConsole=FindFirstObjectByType<FestivalWorld>()?.PlayerDjConsole;
            if(playerConsole==null){Fail("player DJ console");yield break;}
            var takeoverActor=FestivalCharacter.Create(transform,"Takeover motion review",Color.white);
            takeoverActor.transform.position=new Vector3(Catalog.StageTakeoverX,0,Catalog.StageTakeoverZ);
            takeoverActor.transform.localScale=Vector3.Scale(Vector3.one*.82f,takeoverActor.ShapeScale);
            takeoverActor.Pose="Dj";takeoverActor.DjConsole=playerConsole;
            capturePosition=new Vector3(2.7f,2.35f,24.45f);
            captureRotation=Quaternion.LookRotation(new Vector3(0,1.4f,26.55f)-capturePosition);
            captureCamera.fieldOfView=48;
            yield return new WaitForSeconds(.45f);
            var takeoverPath=Path.Combine(dir,session.IsHost?"host-player-dj-contact.png":"client-player-dj-contact.png");
            if(File.Exists(takeoverPath))File.Delete(takeoverPath);
            ScreenCapture.CaptureScreenshot(takeoverPath);
            yield return new WaitForSeconds(.7f);
            if(!File.Exists(takeoverPath)){Fail("player DJ contact render");yield break;}
            Debug.Log("FESTIVAL SMOKE PLAYER DJ CONTACT PASSED: "+takeoverPath);
            Destroy(takeoverActor.gameObject);
            capturePosition=new Vector3(-10,2.4f,-4);
            captureRotation=Quaternion.LookRotation(new Vector3(-24,1.3f,6)-capturePosition);
            yield return new WaitForSeconds(.35f);
            var crowdGrovePath=Path.Combine(dir,session.IsHost?"host-crowd-grove.png":"client-crowd-grove.png");
            ScreenCapture.CaptureScreenshot(crowdGrovePath);
            yield return new WaitForSeconds(1);
            if(!File.Exists(crowdGrovePath)){Fail("grove crowd render");yield break;}
            Debug.Log("FESTIVAL SMOKE CROWD GROVE PASSED: "+crowdGrovePath);
            var gallery=new FestivalCharacter[6];
            var roles=new[]{"Security","Medic","Friend","Vendor0","Vendor1","Vendor2"};
            var poses=new[]{"Accusing","Rescue","ReadClue","Idle","Dance","Extract"};
            for(int i=0;i<gallery.Length;i++)
            {
                var id=GalleryId(i%2,i/2,-1,-1);
                gallery[i]=FestivalCharacter.Create(transform,id,Color.white,roles[i]);
                gallery[i].transform.position=new Vector3(-3.5f+i*1.4f,0,-5.5f);
                gallery[i].transform.rotation=Quaternion.Euler(0,180,0);
                gallery[i].transform.localScale=Vector3.Scale(Vector3.one*.84f,gallery[i].ShapeScale);
                gallery[i].Pose=poses[i];
                if(i==0)gallery[i].SetLittleSpoon(true);
            }
            yield return new WaitForSeconds(1);
            var rolePath=Path.Combine(dir,session.IsHost?"host-fit-roles.png":"client-fit-roles.png");
            ScreenCapture.CaptureScreenshot(rolePath);
            yield return new WaitForSeconds(1);
            if(!File.Exists(rolePath)){Fail("role fit gallery");yield break;}
            Debug.Log("FESTIVAL SMOKE FIT ROLES PASSED: "+rolePath);
            foreach(var actor in gallery)Destroy(actor.gameObject);
            yield return null;
            for(int i=0;i<gallery.Length;i++)
            {
                var id=GalleryId(i%2,i/2,i%4,(i+1)%4);
                gallery[i]=FestivalCharacter.Create(transform,id,Color.white);
                gallery[i].transform.position=new Vector3(-3.5f+i*1.4f,0,-5.5f);
                gallery[i].transform.rotation=Quaternion.Euler(0,180,0);
                gallery[i].transform.localScale=Vector3.Scale(Vector3.one*.84f,gallery[i].ShapeScale);
                gallery[i].Pose=i%2==0?"Dance":"Rescue";
            }
            yield return new WaitForSeconds(1);
            var outfitPath=Path.Combine(dir,session.IsHost?"host-fit-outfits.png":"client-fit-outfits.png");
            ScreenCapture.CaptureScreenshot(outfitPath);
            yield return new WaitForSeconds(1);
            if(!File.Exists(outfitPath)){Fail("outfit fit gallery");yield break;}
            Debug.Log("FESTIVAL SMOKE FIT OUTFITS PASSED: "+outfitPath);
            foreach(var actor in gallery)Destroy(actor.gameObject);
            yield return null;
            var portraitIds=new[]{GalleryFaceId(false,0),GalleryFaceId(true,0),GalleryFaceId(false,1)};
            var portraits=new FestivalCharacter[3];
            capturePosition=new Vector3(0,1.3f,-9.7f);captureRotation=Quaternion.Euler(2,0,0);captureCamera.fieldOfView=46;
            var hands=captureCamera.GetComponentInChildren<FestivalHands>();if(hands!=null)hands.gameObject.SetActive(false);
            for(int i=0;i<portraits.Length;i++)
            {
                portraits[i]=FestivalCharacter.Create(transform,portraitIds[i],Color.white);
                portraits[i].transform.position=new Vector3((i-1)*1.35f,0,-5.5f);
                portraits[i].transform.rotation=Quaternion.Euler(0,180,0);
                portraits[i].transform.localScale=Vector3.Scale(Vector3.one*.88f,portraits[i].ShapeScale);
                if(i==2){portraits[i].SetHighlyIntoxicated(true);portraits[i].SetRedEyes(true);}
            }
            yield return new WaitForSeconds(.8f);
            var qualityPath=Path.Combine(dir,session.IsHost?"host-character-quality.png":"client-character-quality.png");
            ScreenCapture.CaptureScreenshot(qualityPath);yield return new WaitForSeconds(1);
            if(!File.Exists(qualityPath)){Fail("character quality gallery");yield break;}
            Debug.Log("FESTIVAL SMOKE CHARACTER QUALITY PASSED: "+qualityPath);
            portraits[0].SetEquippedItem("merch_bag");portraits[1].SetEquippedItem("stock_lsd");portraits[2].SetEquippedItem("map");
            yield return new WaitForSeconds(.5f);
            var gripPath=Path.Combine(dir,session.IsHost?"host-item-grips.png":"client-item-grips.png");
            ScreenCapture.CaptureScreenshot(gripPath);yield return new WaitForSeconds(.4f);
            if(!File.Exists(gripPath)){Fail("held item contact gallery");yield break;}
            Debug.Log("FESTIVAL SMOKE ITEM GRIPS PASSED: "+gripPath);
            captureCamera.fieldOfView=75;
            var gripHands=FestivalHands.Create(captureCamera,"native_grip_preview");
            foreach(var item in new[]{"merch_bag","stock_lsd","map","stage_pass","medical_voucher","confetti","poi_practice","poi_led"})
            {
                gripHands.SetState(new PlayerState{EquippedItemId=item,Life="Alive"});
                yield return new WaitForSeconds(.35f);
                var firstPersonPath=Path.Combine(dir,(session.IsHost?"host":"client")+"-grip-"+item+".png");
                ScreenCapture.CaptureScreenshot(firstPersonPath);yield return new WaitForSeconds(.2f);
                if(!File.Exists(firstPersonPath)){Fail("first-person grip "+item);yield break;}
            }
            Destroy(gripHands.gameObject);captureCamera.fieldOfView=46;
            foreach(var portrait in portraits)portrait.SetEquippedItem("");
            portraits[0].Pose="Dance";portraits[1].Pose="Poi";portraits[2].Pose="Dance";
            yield return new WaitForSeconds(.4f);
            for(int motionFrame=0;motionFrame<5;motionFrame++)
            {
                var motionPath=Path.Combine(dir,(session.IsHost?"host":"client")+"-motion-"+motionFrame+".png");
                ScreenCapture.CaptureScreenshot(motionPath);
                yield return new WaitForSeconds(.18f);
                if(!File.Exists(motionPath)){Fail("character motion frame "+motionFrame);yield break;}
            }
            portraits[0].gameObject.SetActive(false);portraits[2].gameObject.SetActive(false);
            capturePosition=new Vector3(0,1.35f,-8.8f);captureCamera.fieldOfView=40;
            portraits[1].transform.position=new Vector3(0,0,-5.5f);
            yield return new WaitForSeconds(.25f);
            var visiblePoi=portraits[1].GetComponentInChildren<FestivalPoiRig>();
            if(visiblePoi!=null)
            {
                var visibleCord=visiblePoi.GetComponent<LineRenderer>();
                Debug.Log("FESTIVAL SMOKE POI MOTION: actor="+portraits[1].transform.position+
                    " grip="+visibleCord.GetPosition(0)+" head="+visibleCord.GetPosition(1));
            }
            for(int poiFrame=0;poiFrame<5;poiFrame++)
            {
                var poiPath=Path.Combine(dir,(session.IsHost?"host":"client")+"-poi-motion-"+poiFrame+".png");
                ScreenCapture.CaptureScreenshot(poiPath);
                yield return new WaitForSeconds(.14f);
                if(!File.Exists(poiPath)){Fail("poi motion frame "+poiFrame);yield break;}
            }
            portraits[0].gameObject.SetActive(true);portraits[2].gameObject.SetActive(true);
            foreach(var actor in portraits)actor.Pose="Idle";
            capturePosition=new Vector3(0,1.62f,-8.0f);captureCamera.fieldOfView=40;
            for(int i=0;i<portraits.Length;i++)portraits[i].transform.position=new Vector3((i-1)*1.00f,0,-5.5f);
            yield return new WaitForSeconds(.5f);
            var facePath=Path.Combine(dir,session.IsHost?"host-face-states.png":"client-face-states.png");
            ScreenCapture.CaptureScreenshot(facePath);yield return new WaitForSeconds(1);
            if(!File.Exists(facePath)){Fail("native face states gallery");yield break;}
            Debug.Log("FESTIVAL SMOKE FACE STATES PASSED: "+facePath);
            foreach(var actor in portraits)Destroy(actor.gameObject);
            yield return null;
            var walker=FestivalCharacter.Create(transform,"Walking motion review",Color.white);
            walker.transform.position=new Vector3(-1.0f,0,-5.5f);
            walker.transform.rotation=Quaternion.Euler(0,90,0);
            walker.transform.localScale=Vector3.one*.88f;
            capturePosition=new Vector3(0,1.52f,-9.2f);
            captureRotation=Quaternion.LookRotation(new Vector3(0,1.05f,-5.5f)-capturePosition);
            captureCamera.fieldOfView=46;
            int walkFrame=0;
            float walkStart=Time.time;
            while(Time.time-walkStart<.82f)
            {
                walker.transform.position+=Vector3.right*(1.55f*Time.deltaTime);
                if(walkFrame<3&&Time.time-walkStart>=walkFrame*.25f)
                {
                    var walkPath=Path.Combine(dir,(session.IsHost?"host":"client")+
                        "-walk-motion-"+walkFrame+".png");
                    if(File.Exists(walkPath))File.Delete(walkPath);
                    ScreenCapture.CaptureScreenshot(walkPath);
                    walkFrame++;
                }
                yield return null;
            }
            int stopFrame=0;
            float stopStart=Time.time;
            while(Time.time-stopStart<.44f)
            {
                if(stopFrame<2&&Time.time-stopStart>=stopFrame*.30f)
                {
                    var stopPath=Path.Combine(dir,(session.IsHost?"host":"client")+
                        "-stop-motion-"+stopFrame+".png");
                    if(File.Exists(stopPath))File.Delete(stopPath);
                    ScreenCapture.CaptureScreenshot(stopPath);
                    stopFrame++;
                }
                yield return null;
            }
            int turnFrame=0;
            float turnStart=Time.time;
            while(Time.time-turnStart<.72f)
            {
                float elapsed=Time.time-turnStart;
                walker.transform.rotation=Quaternion.Euler(0,90+Mathf.Min(90,elapsed/.52f*90),0);
                if(turnFrame<3&&elapsed>=.12f+turnFrame*.21f)
                {
                    var turnPath=Path.Combine(dir,(session.IsHost?"host":"client")+
                        "-turn-motion-"+turnFrame+".png");
                    if(File.Exists(turnPath))File.Delete(turnPath);
                    ScreenCapture.CaptureScreenshot(turnPath);
                    turnFrame++;
                }
                yield return null;
            }
            yield return new WaitForSeconds(.7f);
            if(walkFrame!=3||stopFrame!=2||turnFrame!=3){Fail("locomotion transition frame count");yield break;}
            for(int frame=0;frame<3;frame++)
                if(!File.Exists(Path.Combine(dir,(session.IsHost?"host":"client")+"-walk-motion-"+frame+".png")))
                    {Fail("walking motion frame "+frame);yield break;}
            for(int frame=0;frame<2;frame++)
                if(!File.Exists(Path.Combine(dir,(session.IsHost?"host":"client")+"-stop-motion-"+frame+".png")))
                    {Fail("stopping motion frame "+frame);yield break;}
            for(int frame=0;frame<3;frame++)
                if(!File.Exists(Path.Combine(dir,(session.IsHost?"host":"client")+"-turn-motion-"+frame+".png")))
                    {Fail("turning motion frame "+frame);yield break;}
            Debug.Log("FESTIVAL SMOKE LOCOMOTION MOTION PASSED: "+dir);
            Destroy(walker.gameObject);yield return null;
            capturePosition=new Vector3(0,1.2f,-18.5f);captureRotation=Quaternion.identity;captureCamera.fieldOfView=12;
            var detailPair=new FestivalCharacter[2];
            for(int i=0;i<2;i++)
            {
                detailPair[i]=FestivalCharacter.Create(transform,portraitIds[0],Color.white);
                detailPair[i].AlwaysHighDetail=i==0;
                detailPair[i].transform.position=new Vector3(i==0?-.72f:.72f,0,-5.5f);
                detailPair[i].transform.rotation=Quaternion.Euler(0,180,0);
            }
            yield return new WaitForSeconds(.8f);
            if(detailPair[0].UsesDistantMesh||!detailPair[1].UsesDistantMesh){Fail("distance character switching");yield break;}
            var distancePath=Path.Combine(dir,session.IsHost?"host-character-distance.png":"client-character-distance.png");
            ScreenCapture.CaptureScreenshot(distancePath);yield return new WaitForSeconds(1);
            if(!File.Exists(distancePath)){Fail("character distance gallery");yield break;}
            Debug.Log("FESTIVAL SMOKE CHARACTER DISTANCE PASSED: "+distancePath);
            Application.Quit(0);
        }
        IEnumerator SoloSmoke(FestivalSession session)
        {
            float deadline=Time.realtimeSinceStartup+160+(float)FestivalSimulation.SpinSeconds;
            while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            if(session.LocalPlayer==null||!session.IsHost||session.MenuOpen||session.State.Phase!="Shopping"){Fail("solo starts in campsite");yield break;}
            var sim=session.DevelopmentSimulation;
            var player=sim.Player(session.LocalPlayerId);
            string soloCaptureDir=Path.Combine(Application.persistentDataPath,"smoke");Directory.CreateDirectory(soloCaptureDir);
            foreach(var id in new[]{"car_1","car_5","tent_1","tent_7","potty_1"})
            {
                var site=CampFeatures.Find(id);player.X=site.X;player.Z=site.Z;
                session.Command("EnterCamp",site.Id);
                if(player.CampVisitId!=site.Id||!sim.TryMove(player.Id,player.CampInteriorX+.1f,player.CampInteriorZ,0,.1)){Fail("solo enter and walk in "+id);yield break;}
                yield return new WaitForSeconds(.25f);
                string interior=Path.Combine(soloCaptureDir,"solo-"+id+"-interior.png");
                if(File.Exists(interior))File.Delete(interior);
                ScreenCapture.CaptureScreenshot(interior);yield return new WaitForSeconds(.55f);
                if(!File.Exists(interior)){Fail("solo interior render "+id);yield break;}
                if(id=="tent_1")
                {
                    captureCamera=session.ViewCamera;
                    capturePosition=new Vector3(player.CampInteriorX,1.65f,player.CampInteriorZ);
                    captureRotation=Quaternion.Euler(0,180,0);
                    yield return new WaitForSeconds(.2f);
                    string exitView=Path.Combine(soloCaptureDir,"solo-tent_1-exit.png");
                    if(File.Exists(exitView))File.Delete(exitView);
                    ScreenCapture.CaptureScreenshot(exitView);yield return new WaitForSeconds(.55f);
                    captureCamera=null;
                    if(!File.Exists(exitView)){Fail("solo tent doorway render");yield break;}
                }
                session.Command("CampAntic");
                if(player.CampGag==""){Fail("solo antic "+id);yield break;}
                if(site.Kind=="Tent")
                {
                    for(int step=0;step<16&&player.CampVisitId!="";step++)
                        if(!sim.TryMove(player.Id,player.CampInteriorX,player.CampInteriorZ-.1f,180,.1))
                        {Fail("solo walk out of "+id+" at step "+step);yield break;}
                }
                else session.Command("ExitCamp");
                if(player.CampVisitId!=""){Fail("solo exit "+id);yield break;}
            }
            player.X=CampFeatures.DjX;player.Z=CampFeatures.DjZ;
            session.Command("ChooseCampTrack",amount:2);
            if(sim.State.CampMusicTrack!=2){Fail("solo DJ track selection");yield break;}
            var shelf=Catalog.ShopPoint(true,sim.State.VendorOffers.IndexOf("stock_mushrooms"));
            player.X=shelf.X;player.Z=shelf.Z;session.Command("HoldOffer",item:"stock_mushrooms");
            if(player.HeldOfferId!="stock_mushrooms"){Fail("solo shelf pickup");yield break;}
            player.X=0;player.Z=7;session.Command("Buy",item:"stock_mushrooms");
            if(!player.Inventory.Exists(item=>item.ItemId=="stock_mushrooms")){Fail("solo camp purchase");yield break;}
            if(player.EquippedItemId!="stock_mushrooms"){Fail("solo purchased gear equips");yield break;}
            player.Z=1;
            yield return new WaitForSeconds(.3f);
            string equippedCapture=Path.Combine(soloCaptureDir,"solo-equipped-camp.png");
            if(File.Exists(equippedCapture))File.Delete(equippedCapture);
            ScreenCapture.CaptureScreenshot(equippedCapture);yield return new WaitForSeconds(.65f);
            if(!File.Exists(equippedCapture)){Fail("solo equipped camp render");yield break;}
            player.Z=19;session.Command("Ready");
            while(session.State.Phase!="Playing"&&Time.realtimeSinceStartup<deadline)yield return null;
            if(session.State.Phase!="Playing"){Fail("solo campsite countdown");yield break;}
            player.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});session.Command("Equip",item:"poi_practice");
            yield return new WaitForSeconds(.2f);
            string poiCapture=Path.Combine(soloCaptureDir,"solo-poi-grip.png");
            if(File.Exists(poiCapture))File.Delete(poiCapture);
            ScreenCapture.CaptureScreenshot(poiCapture);yield return new WaitForSeconds(.55f);
            if(!File.Exists(poiCapture)){Fail("solo poi grip render");yield break;}
            sim.State.Npcs.Clear();
            player.X=-18;player.Z=-22;session.Command("Use",item:"stock_mushrooms");
            if(player.Effects.Count==0){Fail("solo consumable use");yield break;}
            yield return new WaitForSeconds(.25f);
            var effectWash=transform.Find("Festival HUD/Effect wash")?.GetComponent<Image>();
            if(effectWash==null||effectWash.color.a<=0||!session.LocalPlayer.VisualWideEyes){Fail("solo intoxication first-person and face cues: wash="+(effectWash==null?"missing":effectWash.color.a.ToString("F3"))+" eyes="+session.LocalPlayer.VisualWideEyes);yield break;}
            player.X=0;player.Z=0;
            var npc=new NpcState{Id="solo_wook",X=0,Z=1,Yaw=180,CanTalk=true};sim.State.Npcs.Add(npc);
            session.Command("Talk",npc.Id);
            if(string.IsNullOrEmpty(player.NpcSpeech)||player.NpcSpeechUntil<=sim.State.SimulationSeconds){Fail("solo NPC short chat");yield break;}
            yield return new WaitForSeconds(.2f);
            string chatCapture=Path.Combine(soloCaptureDir,"solo-npc-chat.png");
            if(File.Exists(chatCapture))File.Delete(chatCapture);
            ScreenCapture.CaptureScreenshot(chatCapture);yield return new WaitForSeconds(.65f);
            if(!File.Exists(chatCapture)){Fail("solo NPC chat render");yield break;}
            session.Command("Conversation",npc.Id);
            var talk=sim.Interaction(player.InteractionId);
            if(talk==null||talk.Kind!="Conversation"||string.IsNullOrEmpty(talk.DialogueText)){Fail("solo NPC conversation");yield break;}
            foreach(var note in RhythmChart.Create(talk.ChartSeed,talk.NoteCount,talk.BeatSeconds).Notes)
            {
                while(sim.State.SimulationSeconds-talk.StartSeconds<note.TimeSeconds&&Time.realtimeSinceStartup<deadline)yield return null;
                session.Command("Rhythm",direction:note.Direction,time:note.TimeSeconds);
            }
            while(player.InteractionId!=""&&Time.realtimeSinceStartup<deadline)yield return null;
            if(player.LastRhythmScore<.6){Fail("solo NPC conversation result");yield break;}
            player.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});session.Command("Use",item:"confetti");
            if(npc.DistractedUntil<=sim.State.SimulationSeconds){Fail("solo wook distraction");yield break;}
            session.Command("Dance",npc.Id);
            var dance=sim.Interaction(player.InteractionId);
            if(dance==null||dance.Kind!="Dance"){Fail("solo dance start");yield break;}
            foreach(var note in RhythmChart.Create(dance.ChartSeed,dance.NoteCount,dance.BeatSeconds).Notes)
            {
                while(sim.State.SimulationSeconds-dance.StartSeconds<note.TimeSeconds&&Time.realtimeSinceStartup<deadline)yield return null;
                session.Command("Rhythm",direction:note.Direction,time:note.TimeSeconds);
            }
            while(dance.Status=="Active"&&Time.realtimeSinceStartup<deadline)yield return null;
            if(dance.Status!="Complete"){Fail("solo dance");yield break;}
            // TRIP-2: the clue trail's last link opens the way, not a dance. SMOKE-1 drives the solo tripper's checks; set it directly.
            sim.State.GateOpened=true;
            player.Life="Downed";player.DownedRemaining=.1;
            while(player.Life!="Spirit"&&Time.realtimeSinceStartup<deadline)yield return null;
            if(sim.State.Phase!="Playing"){Fail("solo death recovery window");yield break;}
            player.X=24;player.Z=-20;session.Command("BeginRevival",player.Id);
            while(player.Life!="Alive"&&Time.realtimeSinceStartup<deadline)yield return null;
            if(player.Life!="Alive"||player.RevivalCount!=1){Fail("solo medical revival");yield break;}
            // Nobody is lost by day (LOOP-2), so the rescue runs as Night 1 until SMOKE-1 scripts a real weekend.
            sim.State.LevelIndex=1;player.X=sim.State.FriendPosition.X;player.Z=sim.State.FriendPosition.Z;session.Command("FindFriend");
            while(!sim.State.FriendFound&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!sim.State.FriendFound){Fail("solo friend recruitment");yield break;}
            player.X=0;player.Z=-32;sim.State.FriendPosition=new WorldPoint(0,-32);session.Command("Extract");
            while(sim.State.Phase!="Results"&&Time.realtimeSinceStartup<deadline)yield return null;
            if(sim.State.Result!="Success"||sim.State.Survivors!=1){Fail("solo shuttle result");yield break;}
            session.Command("Reset");
            if(sim.State.Phase!="CampReview"||sim.State.ReviewResult!="Success"){Fail("solo camp review transition");yield break;}
            yield return new WaitForSeconds(.2f);
            string reviewCapture=Path.Combine(soloCaptureDir,"solo-camp-review.png");
            if(File.Exists(reviewCapture))File.Delete(reviewCapture);
            ScreenCapture.CaptureScreenshot(reviewCapture);yield return new WaitForSeconds(.55f);
            if(!File.Exists(reviewCapture)){Fail("solo review render");yield break;}
            // Solo practice skips the debrief vote, so the host can open the shop straight away.
            session.Command("FinishReview");
            if(sim.State.Phase!="Shopping"){Fail("solo review before shopping");yield break;}
            Debug.Log("FESTIVAL SOLO SMOKE PASSED: interiors, antics, DJ, poi, purchase, mission, camp review, next shopping");
            Application.Quit(0);
        }
        static string GalleryFaceId(bool deadpan,int ordinal)
        {
            for(int index=0,found=0;index<10000;index++)
            {
                var id="face_gallery_"+index;
                var look=FestivalAppearance.For(id);
                if((look.Face>=3)==deadpan&&look.Headgear<0&&look.Sunglasses<0&&look.FacialHair<0&&look.Accessory!=3&&found++==ordinal)return id;
            }
            throw new System.InvalidOperationException("Face gallery profile unavailable");
        }
        static string GalleryId(int gender,int shape,int shirt,int pants)
        {
            for(int index=0;index<10000;index++)
            {
                string id="fit_gallery_"+index;
                var look=FestivalAppearance.For(id);
                if(look.Gender==gender&&look.Shape==shape&&(shirt<0||look.Shirt==shirt)&&(pants<0||look.Pants==pants))return id;
            }
            throw new InvalidOperationException("Could not find a deterministic fit gallery profile.");
        }
        // The handoff button each side of the two-client smoke looks for: the host offered a merch bag, the client receives it.
        // The HUD labels items by their display name (THEME-1), so the client's button reads "Accept Official merch bag ×1".
        static string HandoffAction(bool isHost)=>isHost?"Action:Cancel handoff":"Action:Accept "+Catalog.FindItem("merch_bag").Name;
        public static bool ShowsHandoffAction(Transform hud,bool isHost)
        {
            foreach(var button in hud.GetComponentsInChildren<Button>(true))
                if(button.gameObject.activeSelf&&button.name.StartsWith(HandoffAction(isHost),StringComparison.Ordinal))return true;
            return false;
        }
        static void Fail(string stage){Debug.LogError("FESTIVAL SMOKE FAILED: "+stage);Application.Quit(5);}
        static bool Near(PlayerState p,float x,float z)=>p!=null&&Mathf.Abs(p.X-x)<.1f&&Mathf.Abs(p.Z-z)<.1f;
        static bool Within(PlayerState p,float x,float z,float range)=>p!=null&&Vector2.Distance(new Vector2(p.X,p.Z),new Vector2(x,z))<=range;
        static bool Near(NpcState p,float x,float z)=>p!=null&&Mathf.Abs(p.X-x)<.1f&&Mathf.Abs(p.Z-z)<.1f;
        static bool Near(WorldPoint p,float x,float z)=>p!=null&&Mathf.Abs(p.X-x)<.1f&&Mathf.Abs(p.Z-z)<.1f;
        static void PlaceBoth(FestivalSimulation sim,float x,float z)
        {
            foreach(var player in sim.State.Players){player.X=x;player.Z=z;}
        }
    }
}
#endif
