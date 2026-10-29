using System.Collections;
using System.Collections.Generic;
using System;
using Festival.Core;
using Festival.Network;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Festival.Presentation;

namespace Festival.Tests
{
    public sealed class SpeechBubblePlayTests:FestivalPlayModeTest
    {
        static string Send(FestivalSession session,string item)
        {
            var method=typeof(FestivalSession).GetMethod("SendCommand");
            if(method!=null)return (string)method.Invoke(session,new object[]{"Equip","",item,0,0,0d});
            session.Command("Equip",item:item);return "";
        }
        static CommandResult LastResult(FestivalSession session)=>(CommandResult)typeof(FestivalSession).GetProperty("LastLocalCommandResult")?.GetValue(session);
        static void Listen(FestivalSession session,Action<CommandResult> listener)=>typeof(FestivalSession).GetEvent("LocalCommandResultReceived")?.AddEventHandler(session,listener);
        static void Unlisten(FestivalSession session,Action<CommandResult> listener)=>typeof(FestivalSession).GetEvent("LocalCommandResultReceived")?.RemoveEventHandler(session,listener);

        [UnityTest]public IEnumerator AnOpaqueWallOccludesTheRenderedSpeechText()
        {
            var root=new GameObject("Occluded speech");var presenter=root.AddComponent<FestivalSpeechBubbles>();
            var body=new GameObject("Occluded speaker");var head=new GameObject("Head");head.transform.SetParent(body.transform,false);head.transform.localPosition=Vector3.up*1.8f;
            var cameraObject=new GameObject("Speech depth camera");var camera=cameraObject.AddComponent<Camera>();
            var target=new RenderTexture(128,128,24);var pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.SetActive(false);
            var previous=RenderTexture.active;
            try
            {
                camera.orthographic=true;camera.orthographicSize=1.5f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.cullingMask=1<<29;camera.targetTexture=target;
                var state=new RoundState{RoundId="depth-round",Phase="Playing",SimulationSeconds=1};
                state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="depth-round:speech:1",Sequence=1,SpeakerId="speaker",Text="Bright speech",ExpiresAtWorldSeconds=7});
                presenter.Apply(state,id=>body.transform,camera.transform,Vector3.zero,"you",true);
                var text=root.GetComponentInChildren<TextMeshPro>();Assert.That(text,Is.Not.Null);
                foreach(var tr in root.GetComponentsInChildren<Transform>(true))tr.gameObject.layer=29;
                camera.transform.position=text.transform.parent.position+Vector3.back*5;
                wall.layer=29;wall.transform.position=text.transform.parent.position+Vector3.back;
                wall.transform.localScale=new Vector3(8,8,.1f);wall.GetComponent<Renderer>().sharedMaterial=text.transform.parent.Find("Bubble backing").GetComponent<Renderer>().sharedMaterial;
                yield return null;yield return null;
                int BrightPixels()
                {
                    RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();
                    int count=0;foreach(var color in pixels.GetPixels())if(color.r>.7f&&color.g>.7f&&color.b>.7f)count++;
                    RenderTexture.active=previous;return count;
                }
                Assert.That(BrightPixels(),Is.GreaterThan(10),"the actual camera first renders readable white TMP glyphs");
                wall.SetActive(true);yield return null;yield return null;
                Assert.That(BrightPixels(),Is.Zero,"opaque world geometry must hide the text as well as its backing");
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;presenter.Clear();
                UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(body);UnityEngine.Object.Destroy(cameraObject);UnityEngine.Object.Destroy(wall);
                target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);
            }
            yield return null;
        }
        // Looking straight at a festivalgoer from eye height, their bubble sits whole on screen, under the top cards and over the bottom
        // HUD, its letters near the HUD's own size (18-20 px cap height at 1080p) near or far, a second bubble too.
        [UnityTest]public IEnumerator ABubbleStaysInViewUnderTheTopCardsAtAReadableSize()
        {
            var root=new GameObject("Framed speech");var presenter=root.AddComponent<FestivalSpeechBubbles>();
            var body=new GameObject("Framed speaker");var head=new GameObject("Head");head.transform.SetParent(body.transform,false);head.transform.localPosition=Vector3.up*1.8f;
            var eye=new GameObject("Framed eye");var camera=eye.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.aspect=16f/9;
            var state=new RoundState{RoundId="framed",Phase="Playing",SimulationSeconds=1};
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="framed:speech:1",Sequence=1,SpeakerId="speaker",Speaker="FESTIVALGOER",Text="Have you seen the cups? Someone said the night market has them.",ExpiresAtWorldSeconds=9});
            var failures=new List<string>();
            // The cap height of a bubble's first letter (an H) on a 1080-pixel-high screen, from the glyph TextMeshPro laid out.
            float Cap(TextMeshPro text)
            {
                text.ForceMeshUpdate();var letter=text.textInfo.characterInfo[0];
                float tall=text.font.characterLookupTable[letter.character].glyph.metrics.height*letter.scale;
                var foot=camera.WorldToViewportPoint(text.transform.TransformPoint(new Vector3(letter.bottomLeft.x,letter.baseLine,0)));
                var top=camera.WorldToViewportPoint(text.transform.TransformPoint(new Vector3(letter.bottomLeft.x,letter.baseLine+tall,0)));
                return (top.y-foot.y)*1080;
            }
            void Framed(string where)
            {
                foreach(var text in SpeechTexts(root))
                {
                    var backing=text.transform.parent.Find("Bubble backing");float low=1,high=0,left=1,right=0,near=float.MaxValue;
                    foreach(var corner in new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(-.5f,.5f),new Vector3(.5f,.5f)})
                    {var at=camera.WorldToViewportPoint(backing.TransformPoint(corner));low=Mathf.Min(low,at.y);high=Mathf.Max(high,at.y);left=Mathf.Min(left,at.x);right=Mathf.Max(right,at.x);near=Mathf.Min(near,at.z);}
                    string box=" (x "+left.ToString("F3")+"-"+right.ToString("F3")+", y "+low.ToString("F3")+"-"+high.ToString("F3")+")";
                    if(high>FestivalHud.TopCardsBottom)failures.Add(where+": \""+text.text+"\" runs up under the top cards"+box);
                    // The equipment bar's top reaches .185 at the largest text scale.
                    if(low<.185f)failures.Add(where+": \""+text.text+"\" runs down into the bottom HUD"+box);
                    if(left<0||right>1)failures.Add(where+": \""+text.text+"\" runs off the side of the screen"+box);
                    if(near<=camera.nearClipPlane)failures.Add(where+": \""+text.text+"\" sits in front of the near plane");
                }
            }
            void Look(float distance){eye.transform.SetPositionAndRotation(new Vector3(0,1.65f,-distance),Quaternion.identity);presenter.Apply(state,id=>body.transform,eye.transform,eye.transform.position,"you",true);}
            try
            {
                Look(1.5f);var text=SpeechTexts(root);Assert.That(text,Has.Length.EqualTo(1),"setup: one bubble");
                float near=Cap(text[0]);Framed("1.5 m");
                if(near<17.5f||near>20.5f)failures.Add("1.5 m: the cap height is "+near.ToString("F1")+" px at 1080p, not about 18-20");
                Look(10);float far=Cap(SpeechTexts(root)[0]);Framed("10 m");
                if(Mathf.Abs(far-near)>near*.2f)failures.Add("10 m: the cap height is "+far.ToString("F1")+" px against "+near.ToString("F1")+" px at 1.5 m");
                Look(1);Framed("1 m");
                state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="framed:speech:2",Sequence=2,SpeakerId="speaker",Speaker="FESTIVALGOER",Text="Or was it the art car? Ask the guy in the hat.",ExpiresAtWorldSeconds=9});
                foreach(float distance in new[]{1f,1.5f,10f})
                {
                    Look(distance);Assert.That(SpeechTexts(root),Has.Length.EqualTo(2),"setup: two bubbles at "+distance+" m");Framed("two bubbles at "+distance+" m");
                    var stacked=SpeechTexts(root);if(stacked[1].transform.parent.position.y<=stacked[0].transform.parent.position.y)failures.Add(distance+" m: the newer bubble does not stack over the first");
                }
            }
            finally{presenter.Clear();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(body);UnityEngine.Object.Destroy(eye);}
            yield return null;
            Assert.That(failures,Is.Empty,"Speech bubbles in a first-person view:\n"+string.Join("\n",failures));
        }
        // TRIP-23: through Pony Dust's sleepy eyes, a near speaker's stack keeps under the line where the resting upper lid is half dark
        // down the middle of the view, so its top line is read through the eye's opening. Couch Lock's lighter lids rest above the band.
        [UnityTest]public IEnumerator ANearStackKeepsUnderPonyDustsLids()
        {
            var root=new GameObject("Lidded speech");var presenter=root.AddComponent<FestivalSpeechBubbles>();
            var body=new GameObject("Lidded speaker");var head=new GameObject("Head");head.transform.SetParent(body.transform,false);head.transform.localPosition=Vector3.up*1.8f;
            var eye=new GameObject("Lidded eye");var camera=eye.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.aspect=16f/9;camera.enabled=false;
            var state=new RoundState{RoundId="lidded",Phase="Playing",SimulationSeconds=1};var you=new PlayerState{Id="you",Name="You"};state.Players.Add(you);
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="lidded:speech:1",Sequence=1,SpeakerId="speaker",Speaker="FESTIVALGOER",Text="Have you seen the cups? Someone said the night market has them.",ExpiresAtWorldSeconds=9});
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="lidded:speech:2",Sequence=2,SpeakerId="speaker",Speaker="FESTIVALGOER",Text="Or was it the art car? Ask the guy in the hat.",ExpiresAtWorldSeconds=9});
            var failures=new List<string>();
            // The lowest height on the screen where the upper lid is half dark down the middle, read off the lid's own shading
            // (FestivalTrip.EyeShade across the eye's opening, which spans the resting aperture round the screen's middle).
            float HalfShut(string substance,int dose)
            {
                float open=1-FestivalTrip.EyeRest(substance,dose);
                for(float y=.5f;y<1;y+=.0005f)if(FestivalTrip.EyeShade(0,(y-.5f)/(open/2),1,FestivalTrip.EyeSoftness(substance))>=.5f)return y;
                return 1;
            }
            void Dose(string substance,int dose){you.Effects.Clear();if(dose>0)you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="lidded_dose",Intensity=dose,Substance=substance,RemainingSeconds=500});}
            float Top(float distance)
            {
                eye.transform.SetPositionAndRotation(new Vector3(0,1.65f,-distance),Quaternion.identity);
                presenter.Apply(state,id=>body.transform,eye.transform,eye.transform.position,"you",true);
                var texts=SpeechTexts(root);if(texts.Length!=2)failures.Add(distance+" m: "+texts.Length+" bubbles, not 2");
                float high=0;
                foreach(var text in texts)foreach(var corner in new[]{new Vector3(-.5f,.5f),new Vector3(.5f,.5f)})high=Mathf.Max(high,camera.WorldToViewportPoint(text.transform.parent.Find("Bubble backing").TransformPoint(corner)).y);
                return high;
            }
            try
            {
                Dose("",0);float sober=Top(1.5f);
                Assert.That(sober,Is.GreaterThan(HalfShut("ketamine",4)),"setup: sober, a stack 1.5 m away reaches above Pony Dust's lids");
                foreach(int dose in new[]{1,2,3,4})
                {
                    Dose("ketamine",dose);float line=HalfShut("ketamine",dose);
                    foreach(float distance in new[]{1f,1.5f}){float top=Top(distance);if(top>line)failures.Add("Pony Dust x"+dose+", "+distance+" m: the stack's top at "+top.ToString("F3")+" runs up into the lid, half dark at "+line.ToString("F3"));}
                }
                Dose("weed",4);float couch=Top(1.5f);
                if(Mathf.Abs(couch-sober)>.001f)failures.Add("Couch Lock x4: the stack's top moved from "+sober.ToString("F3")+" to "+couch.ToString("F3")+", though its lids rest above the band (half dark at "+HalfShut("weed",4).ToString("F3")+")");
            }
            finally{presenter.Clear();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(body);UnityEngine.Object.Destroy(eye);}
            yield return null;
            Assert.That(failures,Is.Empty,"Speech bubbles under sleepy lids:\n"+string.Join("\n",failures));
        }
        // On Night 2 at 130% text, a crew of four on their doses grows the dose card down the screen's centre, pushing a notice under
        // it, and the checklist grows the crew card on the right. Two bubbles face to face, or off to the right, keep under them all.
        [UnityTest]public IEnumerator BubblesKeepUnderTheCardsThatHangIntoTheBand()
        {
            var world=new GameObject("Band world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("Band HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            var root=new GameObject("Band speech");var presenter=root.AddComponent<FestivalSpeechBubbles>();
            var body=new GameObject("Band speaker");var head=new GameObject("Head");head.transform.SetParent(body.transform,false);head.transform.localPosition=Vector3.up*1.8f;
            var eye=new GameObject("Band eye");var camera=eye.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.aspect=16f/9;camera.enabled=false;
            var talk=new RoundState{RoundId="band",Phase="Playing",SimulationSeconds=1};
            talk.PublicSpeech.Add(new PublicSpeechState{DeliveryId="band:speech:1",Sequence=1,SpeakerId="speaker",Speaker="FESTIVALGOER",Text="Have you seen the cups? Someone said the night market has them.",ExpiresAtWorldSeconds=9});
            talk.PublicSpeech.Add(new PublicSpeechState{DeliveryId="band:speech:2",Sequence=2,SpeakerId="speaker",Speaker="FESTIVALGOER",Text="Or was it the art car? Ask the guy in the hat.",ExpiresAtWorldSeconds=9});
            var failures=new List<string>();float textScale=session.Profile.Data.TextScale;
            var cards=new[]{"Objective card","Round clock","Test mode tag","Tripper card","Player card","Crew card","Session notice","Sundown warning"};
            try
            {
                session.Host("Tester",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var canvas=hud.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.localScale=Vector3.one;
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var mates=new List<PlayerState>();foreach(var name in new[]{"Sam","Kim","Jo"}){var mate=sim.AddPlayer("band_"+name.ToLowerInvariant(),name);mate.Ready=true;mates.Add(mate);}
                yield return StartLevel(session);
                session.Profile.Data.TextScale=1.3f;sim.State.LevelIndex=Festivals.LevelCount-1;sim.State.Npcs.RemoveAll(n=>n.Kind=="Wook");
                foreach(var crew in sim.State.Players){crew.X=-30;crew.Z=-30;}player.X=0;player.Z=0;
                sim.State.TripperId=player.Id;sim.State.Doses.Clear();sim.State.Doses.Add(new PlayerDose{PlayerId=player.Id,Dose=2,Substance="lsd"});
                for(int i=0;i<mates.Count;i++)sim.State.Doses.Add(new PlayerDose{PlayerId=mates[i].Id,Dose=i+1,Substance="ecstasy"});
                session.SetMessage("Sold a Tongue Stamp for $30");
                yield return new WaitForSeconds(.5f);yield return null;
                Canvas.ForceUpdateCanvases();
                Rect Share(string name)
                {
                    foreach(var part in hud.GetComponentsInChildren<RectTransform>(false))
                    {
                        if(part.name!=name)continue;
                        var corners=new Vector3[4];part.GetWorldCorners(corners);var low=canvasRect.InverseTransformPoint(corners[0]);var high=canvasRect.InverseTransformPoint(corners[2]);
                        var size=canvasRect.rect.size;return Rect.MinMaxRect(low.x/size.x+canvasRect.pivot.x,low.y/size.y+canvasRect.pivot.y,high.x/size.x+canvasRect.pivot.x,high.y/size.y+canvasRect.pivot.y);
                    }
                    return Rect.zero;
                }
                Assert.That(Share("Session notice").yMax,Is.LessThan(FestivalSpeechBubbles.BandTop),"setup: the notice hangs into the band under the dose card");
                Assert.That(Share("Crew card").yMin,Is.LessThan(.8f),"setup: Night 2's checklist grows the crew card");
                presenter.Ceiling=session.SpeechBubbles.Ceiling;
                void Look(string where,float distance,float side)
                {
                    eye.transform.SetPositionAndRotation(new Vector3(side,1.65f,-distance),Quaternion.identity);
                    presenter.Apply(talk,id=>body.transform,eye.transform,eye.transform.position,"you",true,0,false,null,session.Profile.Data);
                    var texts=SpeechTexts(root);if(texts.Length!=2)failures.Add(where+": "+texts.Length+" bubbles, not 2");
                    foreach(var text in texts)
                    {
                        var backing=text.transform.parent.Find("Bubble backing");float low=1,high=0,left=1,right=0;
                        foreach(var corner in new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(-.5f,.5f),new Vector3(.5f,.5f)})
                        {var at=camera.WorldToViewportPoint(backing.TransformPoint(corner));low=Mathf.Min(low,at.y);high=Mathf.Max(high,at.y);left=Mathf.Min(left,at.x);right=Mathf.Max(right,at.x);}
                        var bubble=Rect.MinMaxRect(left,low,right,high);
                        foreach(var name in cards)
                        {
                            var card=Share(name);
                            if(card.width>0&&card.Overlaps(bubble))failures.Add(where+": the "+name+" (y "+card.yMin.ToString("F3")+"-"+card.yMax.ToString("F3")+") covers \""+text.text+"\" (x "+left.ToString("F3")+"-"+right.ToString("F3")+", y "+low.ToString("F3")+"-"+high.ToString("F3")+")");
                        }
                        if(low<.185f)failures.Add(where+": \""+text.text+"\" runs down into the bottom HUD (y "+low.ToString("F3")+")");
                    }
                }
                Look("face to face at 1.5 m",1.5f,0);Look("face to face at 1 m",1,0);Look("1.5 m away, to the right",1.5f,-.9f);
            }
            finally
            {
                session.Profile.Data.TextScale=textScale;presenter.Clear();session.Leave();
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)UnityEngine.Object.Destroy(leftover);}
                UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(body);UnityEngine.Object.Destroy(eye);UnityEngine.Object.Destroy(hud);UnityEngine.Object.Destroy(world);
            }
            yield return null;
            Assert.That(failures,Is.Empty,"Speech bubbles under the top cards:\n"+string.Join("\n",failures));
        }
        [UnityTest]public IEnumerator EarlyHostRejectionsKeepTheIssuedIdentityAndAuthorityRules()
        {
            var host=new GameObject("Early rejection host").AddComponent<FestivalSession>();yield return null;
            host.Host("Host",HostPort);yield return null;Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);
            string hostId=host.SendCommand(null);
            Assert.That(LastResult(host).Accepted,Is.False);Assert.That(LastResult(host).Reason,Is.EqualTo("Invalid command envelope"));
            Assert.That(LastResult(host).Id,Is.EqualTo(hostId),"an early validation refusal still names this issued command");
            var sim=Simulation(host);
            Assert.That(sim.State.Commands.Exists(c=>c.Id==hostId),Is.False,"invalid envelopes never enter authoritative dedupe receipts");
            sim.Player(host.LocalPlayerId).Connected=false;
            string disconnected=host.SendCommand("Equip");Assert.That(LastResult(host).Id,Is.EqualTo(disconnected));
            Assert.That(LastResult(host).Reason,Is.EqualTo("Unknown or disconnected player"));sim.Player(host.LocalPlayerId).Connected=true;
            for(int i=sim.State.Commands.Count;i<50000;i++)sim.State.Commands.Add(new CommittedCommand{Id="cap:"+i});
            string capped=host.SendCommand("Equip");Assert.That(LastResult(host).Id,Is.EqualTo(capped));
            Assert.That(LastResult(host).Reason,Is.EqualTo("Round command limit reached"));
            Assert.That(sim.State.Commands,Has.Count.EqualTo(50000),"the identity adapter cannot relax the command cap");
            host.Leave();yield return null;
        }
        [UnityTest]public IEnumerator AnEarlyJoinedRejectionNamesTheActualIssuedCommand()
        {
            var host=new GameObject("Early result host").AddComponent<FestivalSession>();
            var client=new GameObject("Early result client").AddComponent<FestivalSession>();yield return null;
            ushort port=FreePort();host.Host("Host",port);yield return null;client.Join("Friend","127.0.0.1",port);
            float deadline=Time.realtimeSinceStartup+15;
            while(client.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(client.LocalPlayer,Is.Not.Null,client.Message);
            var replies=new List<CommandResult>();Action<CommandResult> listener=r=>replies.Add(r);Listen(client,listener);
            try
            {
                string clientId=client.SendCommand("Equip",target:new string('x',129));deadline=Time.realtimeSinceStartup+5;
                while(replies.Count<1&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(replies,Has.Count.EqualTo(1));Assert.That(replies[0].Accepted,Is.False);
                Assert.That(replies[0].Reason,Is.EqualTo("Invalid command envelope"));
                Assert.That(replies[0].Id,Is.EqualTo(clientId),"the trusted joined result route keeps early-rejection identity too");
                Assert.That(Simulation(host).State.Commands.Exists(c=>c.Id==clientId),Is.False);
            }
            finally{Unlisten(client,listener);client.Leave();host.Leave();}
            yield return null;
        }
        [UnityTest]public IEnumerator EveryHostCommandReportsItsExactIdAndDetachedResult()
        {
            var root=new GameObject("Command result host");var session=root.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
            var results=new List<CommandResult>();Action<CommandResult> listener=r=>results.Add(r);
            Action<CommandResult> mutator=r=>{r.Id="observer changed it";r.Accepted=true;r.Reason="observer changed it";};
            Listen(session,mutator);Listen(session,listener);
            try
            {
                string rejected=Send(session,"not_owned"),accepted=Send(session,"");
                Assert.That(session.Message,Is.EqualTo("Hands free"),"the production commands really executed");
                Assert.That(results,Has.Count.EqualTo(2),"each real local reply must notify the session consumer, including a rejection before a later success");
                Assert.That(rejected,Is.Not.Empty);Assert.That(accepted,Is.Not.EqualTo(rejected));
                Assert.That(results[0].Id,Is.EqualTo(rejected));Assert.That(results[0].Accepted,Is.False);Assert.That(results[0].Reason,Is.EqualTo("Item not owned"));
                Assert.That(results[1].Id,Is.EqualTo(accepted));Assert.That(results[1].Accepted,Is.True);
                var cached=LastResult(session);cached.Id="changed cache";
                Assert.That(LastResult(session).Id,Is.EqualTo(accepted),"the cache getter is detached");
                var committed=Simulation(session).State.Commands.Find(c=>c.Id==rejected);
                Assert.That(committed.Result.Id,Is.EqualTo(rejected));Assert.That(committed.Result.Accepted,Is.False,"observers cannot edit authoritative dedupe receipts");
                session.Leave();Assert.That(LastResult(session),Is.Null);Assert.That(Send(session,""),Is.Empty,"a disconnected command has no issued ID");
                yield return null;yield return null;
                session.Host("Tester",HostPort);yield return null;Send(session,"");
                Assert.That(results,Has.Count.EqualTo(3),"persistent callers keep their subscription across Leave/Host");
            }
            finally{Unlisten(session,listener);Unlisten(session,mutator);session.Leave();}
        }
        [UnityTest]public IEnumerator AJoinedClientsCoalescedRepliesKeepBothIdsAndNeverNotifyTheHostPlayer()
        {
            var host=new GameObject("Command host").AddComponent<FestivalSession>();
            var client=new GameObject("Command client").AddComponent<FestivalSession>();yield return null;
            ushort port=FreePort();host.Host("Host",port);yield return null;client.Join("Friend","127.0.0.1",port);
            float deadline=Time.realtimeSinceStartup+15;
            while(client.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(client.LocalPlayer,Is.Not.Null,client.Message);
            var local=new List<CommandResult>();var remote=new List<CommandResult>();
            Action<CommandResult> localListener=r=>local.Add(r),hostListener=r=>remote.Add(r);
            Listen(client,localListener);Listen(host,hostListener);
            try
            {
                string rejected=Send(client,"not_owned"),accepted=Send(client,"");deadline=Time.realtimeSinceStartup+5;
                while(local.Count<2&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(client.Message,Is.EqualTo("Hands free"),"both actual reliable replies reached the joined client");
                Assert.That(local,Has.Count.EqualTo(2),"the second reply cannot hide a matching first rejection");
                Assert.That(local[0].Id,Is.EqualTo(rejected));Assert.That(local[0].Accepted,Is.False);
                Assert.That(local[1].Id,Is.EqualTo(accepted));Assert.That(local[1].Accepted,Is.True);
                Assert.That(LastResult(client).Id,Is.EqualTo(accepted));Assert.That(remote,Is.Empty,"remote player commands do not notify the host player's consumer");
                client.Leave();Assert.That(LastResult(client),Is.Null);
            }
            finally{Unlisten(client,localListener);Unlisten(host,hostListener);client.Leave();host.Leave();}
            yield return null;
        }
        [UnityTest]public IEnumerator DisablingTheHostedSessionImmediatelyRetiresItsSpeechResources()
        {
            var world=new GameObject("Speech lifecycle world");world.AddComponent<Festival.Presentation.FestivalWorld>();yield return null;
            var root=new GameObject("Speech lifecycle session");var session=root.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);yield return null;yield return StartLevel(session);
            var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
            var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook"&&n.Twist=="");
            npc.X=player.X+.6f;npc.Z=player.Z;npc.Mode="Blending";npc.CanTalk=true;npc.LastTalkSeconds=-10;
            session.Command("Talk",npc.Id);yield return null;yield return null;
            var spoken=SpeechTexts(root);Assert.That(spoken,Is.Not.Empty);var text=spoken[0];
            var font=text.font;var atlas=font.atlasTexture;var material=text.fontSharedMaterial;
            session.enabled=false;yield return null;yield return null;
            Assert.That(System.Array.FindAll(root.GetComponentsInChildren<TextMeshPro>(true),t=>t.name=="Speech text"),Is.Empty,"disable releases active and pooled bubble objects");
            Assert.That(font==null&&atlas==null&&material==null,Is.True,"disable releases the session-owned font, atlas and material");
            session.enabled=true;yield return null;yield return null;
            Assert.That(SpeechTexts(root),Is.Empty,"the stale host snapshot does not replay");
            session.Leave();yield return null;
        }
        [UnityTest]public IEnumerator AHostedTalkDrawsTheActualSpeakersTextMeshProBubble()
        {
            var world=new GameObject("Speech bubble world");world.AddComponent<Festival.Presentation.FestivalWorld>();yield return null;
            var root=new GameObject("Speech bubble session");var session=root.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);yield return null;
            Assert.That(session.LocalPlayer,Is.Not.Null,"the real host connects: "+session.Message);
            yield return StartLevel(session);
            var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
            var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook"&&n.Twist=="");
            Assert.That(npc,Is.Not.Null,"the authored festival has an ordinary speaker");
            npc.X=player.X+.6f;npc.Z=player.Z;npc.Mode="Blending";npc.Suspicion=0;npc.CanTalk=true;npc.LastTalkSeconds=-10;
            session.Command("Talk",npc.Id);
            string said=player.NpcSpeech;
            Assert.That(said,Is.Not.Empty,"the production Talk command speaks: "+session.Message);
            yield return null;yield return null;
            var texts=SpeechTexts(root);
            Assert.That(texts,Has.Length.EqualTo(1),"accepted public Talk must draw a real TMP bubble on its session");
            Assert.That(texts[0].text,Is.EqualTo(said));
            Assert.That(texts[0].font.sourceFontFile,Is.SameAs(Resources.Load<Font>("FestivalDisplay")));
            Assert.That(session.SpeechBubbles.Deliveries,Is.SameAs(session.SpeechDeliveries));
            var head=session.WorldCharacter(npc.Id).transform;
            foreach(var tr in head.GetComponentsInChildren<Transform>(true))if(tr.name=="Head"){head=tr;break;}
            Assert.That(texts[0].transform.parent.position.x,Is.EqualTo(head.position.x).Within(.01f));
            Assert.That(texts[0].transform.parent.position.y,Is.GreaterThan(head.position.y+.3f));
            player.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=1,RemainingSeconds=300});session.Command("Dance",npc.Id);
            string danceId=player.SpeechDeliveryId;yield return null;yield return null;
            Assert.That(session.SpeechDeliveries.Deliveries,Has.Count.EqualTo(2),"Talk and Dance accepted before a view retain both identities");
            Assert.That(session.SpeechDeliveries.Find(danceId).SpeakerId,Is.EqualTo(player.Id));
            Assert.That(SpeechTexts(root),Has.Length.EqualTo(1),"the local dosed Dance never adds an own-head bubble");
            session.Profile.Data.ThirdPerson=true;yield return null;yield return null;
            Assert.That(SpeechTexts(root),Has.Length.EqualTo(1),"third person also hides your own-head speech");
        }
        [UnityTest]public IEnumerator AHostedTalkUsesEffectsAndMasterAndDisposesItsAudioOnDisableAndLeave()
        {
            var world=new GameObject("Babble hosted world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("Babble hosted session");var session=root.AddComponent<FestivalSession>();yield return null;
            session.Host("Tester",HostPort);yield return null;Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
            yield return StartLevel(session);
            var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
            var npc=sim.State.Npcs.Find(n=>n.Kind=="Wook"&&n.Twist=="");Assert.That(npc,Is.Not.Null);
            npc.X=player.X+.6f;npc.Z=player.Z;npc.Mode="Blending";npc.Suspicion=0;npc.CanTalk=true;npc.LastTalkSeconds=-10;
            float master=session.Profile.Data.MasterVolume,listener=AudioListener.volume;
            AudioSource[] Voices()=>Array.FindAll(root.GetComponentsInChildren<AudioSource>(true),source=>source.gameObject.name=="Festival babble");
            try
            {
                session.Profile.Data.EffectsVolume=.4f;session.Profile.Data.MasterVolume=0;session.Profile.Data.MuteWhenUnfocused=false;
                session.Command("Talk",npc.Id);Assert.That(player.NpcSpeech,Is.Not.Empty,session.Message);
                float deadline=Time.realtimeSinceStartup+5;
                while(Array.Find(Voices(),source=>source.clip!=null)==null&&Time.realtimeSinceStartup<deadline)yield return null;
                var voices=Voices();Assert.That(voices,Has.Length.EqualTo(2),"the actual session reveal creates one bounded pair of spatial sources");
                Assert.That(Array.Find(voices,source=>source.clip!=null),Is.Not.Null);
                foreach(var source in voices){Assert.That(source.spatialBlend,Is.EqualTo(1));Assert.That(source.volume,Is.EqualTo(.12f).Within(.0001f));}
                Assert.That(AudioListener.volume,Is.Zero,"the existing master listener mutes speech without a second master multiplication");
                session.Profile.Data.EffectsVolume=0;yield return null;yield return null;
                foreach(var source in voices)Assert.That(source.volume,Is.Zero,"the active source follows the real Effects slider");
                var clips=new System.Collections.Generic.HashSet<AudioClip>();foreach(var source in voices)if(source.clip!=null)clips.Add(source.clip);
                Assert.That(clips.Count,Is.GreaterThan(0));session.enabled=false;yield return null;yield return null;
                foreach(var source in voices)Assert.That(source==null,Is.True,"disable destroys each owned source once");
                foreach(var clip in clips)Assert.That(clip==null,Is.True,"disable releases generated samples too");
                session.enabled=true;yield return null;yield return null;
                Assert.That(Voices(),Is.Empty,"reenabling cannot replay a retired accepted delivery");
                session.Leave();yield return null;Assert.That(Voices(),Is.Empty);
            }
            finally{session.Profile.Data.MasterVolume=master;session.Leave();AudioListener.volume=listener;}
        }
        [UnityTest]public IEnumerator LateVisibleSpeechAndMenuOrRangeReturnsNeverPlayAnAudioBacklog()
        {
            var root=new GameObject("Babble visibility");var presenter=root.AddComponent<FestivalSpeechBubbles>();
            var body=new GameObject("Babble visible actor");var camera=new GameObject("Babble visibility camera");
            var state=new RoundState{RoundId="babble-visibility",Phase="Playing"};state.Npcs.Add(new NpcState{Id="speaker"});
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="babble-visibility:speech:1",Sequence=1,SpeakerId="speaker",Text="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",ExpiresAtWorldSeconds=7});
            void Draw(double time,bool hidden=false){state.SimulationSeconds=time;presenter.Apply(state,id=>body.transform,camera.transform,Vector3.zero,"you",true,hidden:hidden);}
            AudioSource[] Voices()=>root.GetComponentsInChildren<AudioSource>(true);
            try
            {
                Draw(.2);Assert.That(Voices(),Is.Empty,"late first observation is silent");
                Draw(.24);Assert.That(Array.Find(Voices(),source=>source.isPlaying),Is.Not.Null,"a subsequent visible glyph really plays audio");
                Draw(.3,true);Assert.That(Array.Exists(Voices(),source=>source.isPlaying),Is.False);
                Draw(.4);Assert.That(Array.Exists(Voices(),source=>source.isPlaying),Is.False,"reopening the menu skips missed glyphs");
                Draw(.44);Assert.That(Array.Exists(Voices(),source=>source.isPlaying),Is.True);
                body.transform.position=Vector3.right*15.001f;Draw(.5);Assert.That(Array.Exists(Voices(),source=>source.isPlaying),Is.False);
                body.transform.position=Vector3.right*2;Draw(.6);Assert.That(Array.Exists(Voices(),source=>source.isPlaying),Is.False,"walking back into range skips missed glyphs");
                Draw(.64);Assert.That(Array.Exists(Voices(),source=>source.isPlaying),Is.True);
                state.PublicSpeech[0].Retracted=true;Draw(.65);yield return null;
                Assert.That(Voices(),Is.Empty,"retraction retires this speaker's only cache");
            }
            finally{presenter.Clear();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(body);UnityEngine.Object.Destroy(camera);}
            yield return null;
        }
    }
}
