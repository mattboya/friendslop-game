using NUnit.Framework;
using TMPro;
using UnityEngine;
using Festival.Core;
using Festival.Presentation;
using UnityEngine.Rendering;

namespace Festival.Tests
{
    public sealed class SpeechBubbleTests
    {
        GameObject root,body,view;
        FestivalSpeechBubbles presenter;
        RoundState state;
        [SetUp]public void SetUpBubble()
        {
            root=new GameObject("Speech presenter");presenter=root.AddComponent<FestivalSpeechBubbles>();
            body=new GameObject("Speaker");body.transform.position=new Vector3(2,0,0);
            var head=new GameObject("Head");head.transform.SetParent(body.transform,false);head.transform.localPosition=Vector3.up*1.8f;
            view=new GameObject("Bubble view");view.transform.position=new Vector3(0,1.65f,-3);
            state=new RoundState{RoundId="bubble-round",Phase="Playing"};
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="bubble-round:speech:1",Sequence=1,SpeakerId="speaker",Speaker="FESTIVALGOER",Text="The festival has enough room for every friend to enjoy the music and dance together.",StartedAtWorldSeconds=0,ExpiresAtWorldSeconds=7});
        }
        [TearDown]public void TearDownBubble(){presenter.Clear();Object.DestroyImmediate(root);Object.DestroyImmediate(body);Object.DestroyImmediate(view);}
        void Draw(string local="you",bool reduced=false)=>presenter.Apply(state,id=>id=="speaker"?body.transform:null,view.transform,Vector3.zero,local,reduced);
        // The bubble's own text: the vision markers under the same root letter their labels in TextMeshPro too.
        TextMeshPro Text()=>System.Array.Find(root.GetComponentsInChildren<TextMeshPro>(false),t=>t.name=="Speech text");

        [Test]public void TheNativeSmokeChecksTheActualSupportedBubbleTextMaterial()
        {
            Draw();var material=Text().fontSharedMaterial;
            Assert.That(material.shader.isSupported,Is.True,"the actual world bubble material is supported by this runtime");
            Assert.That(DevelopmentSmoke.BubbleTextShaderSupported(material),Is.True);
            Assert.That(DevelopmentSmoke.BubbleTextShaderSupported(null),Is.False,"a missing consumer material must fail smoke acceptance");
            Assert.That(DevelopmentSmoke.BubbleTextShaderSupported(Text().transform.parent.Find("Bubble backing").GetComponent<Renderer>().sharedMaterial),Is.False,"a supported non-text material cannot pass as the shipping TMP shader");
        }
        [Test]public void TextTypesOutAtThirtyLettersASecond()
        {
            Draw();Assert.That(Text().maxVisibleCharacters,Is.Zero);
            state.SimulationSeconds=.5;Draw();var text=Text();text.ForceMeshUpdate();
            Assert.That(text.maxVisibleCharacters,Is.EqualTo(15));
            Assert.That(text.text,Is.EqualTo(state.PublicSpeech[0].Text),"typing never replaces the wrapped full text with a substring");
            Assert.That(text.textInfo.lineCount,Is.GreaterThan(1),"layout includes the full wrapped line before reveal");
        }
        [Test]public void ABubbleFollowsTheSpeakersHead()
        {
            Draw();var first=Text().transform.parent.position;
            body.transform.Find("Head").position+=new Vector3(.3f,1.4f,0);Draw();
            Assert.That(Text().transform.parent.position.y-first.y,Is.EqualTo(1.4f).Within(.01f));
            Assert.That(Text().transform.parent.position.x-first.x,Is.EqualTo(.3f).Within(.01f));
            Assert.That(Quaternion.Angle(Text().transform.parent.rotation,view.transform.rotation),Is.LessThan(.01f));
        }
        [Test]public void TheBubbleSitsAboveTheHighestActualVisionMarker()
        {
            state.Visions.Add(new VisionState{Id="low",NpcId="speaker",Kind="Buyer"});
            state.Visions.Add(new VisionState{Id="high",NpcId="speaker",Kind="Clue"});
            var markers=root.AddComponent<FestivalVisionMarkers>();
            markers.Apply(state,view.transform,id=>body.transform,0,0);
            state.SimulationSeconds=3;
            Draw();var text=Text();text.ForceMeshUpdate();
            var marker=root.transform.Find("Vision high");float top=0;
            foreach(var renderer in marker.GetComponentsInChildren<Renderer>(false))top=Mathf.Max(top,renderer.bounds.max.y);
            Assert.That(text.GetComponent<Renderer>().bounds.min.y,Is.GreaterThan(top+.05f));
            Assert.That(text.transform.parent.Find("Bubble backing").GetComponent<Renderer>().bounds.min.y,Is.GreaterThan(top+.05f));
        }
        [TestCase(false),TestCase(true)]public void NoBubbleOverYourOwnHead(bool thirdPerson)
        {
            if(thirdPerson)view.transform.position+=Vector3.back*2;
            Draw("speaker");Assert.That(Text(),Is.Null,"own-head speech is hidden in either camera mode");
        }
        [Test]public void NoBubblePastFifteenMetres()
        {
            body.transform.position=new Vector3(15,0,0);Draw();Assert.That(Text(),Is.Not.Null);
            body.transform.position=new Vector3(15.001f,0,0);Draw();Assert.That(Text(),Is.Null);
        }
        [Test]public void HiddenSpeakersHaveNoBubble()
        {
            body.SetActive(false);Draw();Assert.That(Text(),Is.Null);
        }
        [Test]public void TheBubbleGoesWhenTheLineEnds()
        {
            Draw();Assert.That(Text(),Is.Not.Null);
            state.SimulationSeconds=7;Draw();Assert.That(Text(),Is.Null);
        }
        [Test]public void AHostRetractionRemovesTheBubbleAtOnce()
        {
            Draw();state.PublicSpeech[0].Retracted=true;state.PublicSpeech[0].Text="";Draw();Assert.That(Text(),Is.Null);
        }
        [Test]public void BubbleTextIsLiteralAndUsesOneSharedFestivalFontAndDepthTestedQuad()
        {
            state.PublicSpeech[0].Text="<size=99>Hi & friends</size>";
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="bubble-round:speech:2",Sequence=2,SpeakerId="speaker",Text="Again",ExpiresAtWorldSeconds=7});
            Draw();var texts=root.GetComponentsInChildren<TextMeshPro>(false);
            Assert.That(texts,Has.Length.EqualTo(2));
            Assert.That(texts[0].font,Is.SameAs(texts[1].font));
            Assert.That(texts[0].fontSharedMaterial,Is.SameAs(texts[1].fontSharedMaterial));
            Assert.That(texts[0].font.sourceFontFile,Is.SameAs(Resources.Load<Font>("FestivalDisplay")));
            Assert.That(texts[0].fontSharedMaterial.shader.name,Is.EqualTo("TextMeshPro/Mobile/Distance Field"));
            Assert.That(texts[0].fontSharedMaterial.GetFloat(ShaderUtilities.ShaderTag_ZTestMode),Is.EqualTo((float)CompareFunction.LessEqual));
            Assert.That(texts[0].richText,Is.False);texts[0].ForceMeshUpdate();
            Assert.That(texts[0].textInfo.characterCount,Is.EqualTo(28),"angle brackets and ampersand render as authored characters");
            var quad=texts[0].transform.parent.Find("Bubble backing");
            Assert.That(quad.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.EqualTo(4));
            Assert.That(quad.GetComponent<Renderer>().sharedMaterial.renderQueue,Is.LessThan(2501));
            Assert.That(quad.GetComponent<Collider>(),Is.Null);
        }
        [Test]public void LeaveRetiresObservedDeliveryIdsWithoutReplayingTheirStaleView()
        {
            Draw();presenter.Clear();Draw();Assert.That(Text(),Is.Null);
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="bubble-round:speech:2",Sequence=2,SpeakerId="speaker",Text="A new accepted line",ExpiresAtWorldSeconds=7});
            Draw();Assert.That(Text().text,Is.EqualTo("A new accepted line"));
        }
        [Test]public void ANewRoundDisposesThePreviousRoundsFontCache()
        {
            Draw();var oldFont=Text().font;
            state.RoundId="next-round";state.PublicSpeech[0].DeliveryId="next-round:speech:1";Draw();
            Assert.That(oldFont==null,Is.True);
            Assert.That(Text().font,Is.Not.Null);Assert.That(Text().maxVisibleCharacters,Is.Zero);
        }
        [Test]public void ReconnectingDoesNotReplayAnObservedDeliveryFromTheSameRound()
        {
            Draw();presenter.Apply(state,id=>body.transform,view.transform,Vector3.zero,"you",false,1);
            Assert.That(Text(),Is.Null);
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="bubble-round:speech:2",Sequence=2,SpeakerId="speaker",Text="Fresh",ExpiresAtWorldSeconds=7});
            presenter.Apply(state,id=>body.transform,view.transform,Vector3.zero,"you",false,1);
            Assert.That(Text().text,Is.EqualTo("Fresh"));
        }
        [Test]public void ReducedMotionRemovesOnlyThePopAndKeepsTyping()
        {
            state.SimulationSeconds=.02;Draw();Assert.That(Text().transform.parent.localScale.x,Is.LessThan(1));
            Draw(reduced:true);Assert.That(Text().transform.parent.localScale,Is.EqualTo(Vector3.one));
            state.SimulationSeconds=.5;Draw(reduced:true);Assert.That(Text().maxVisibleCharacters,Is.EqualTo(15));
        }
        [Test]public void MenusHideWorldSpeechWhileItsRevealClockKeepsMoving()
        {
            Draw();state.SimulationSeconds=.5;
            presenter.Apply(state,id=>body.transform,view.transform,Vector3.zero,"you",false,hidden:true);
            Assert.That(Text(),Is.Null);
            Draw();Assert.That(Text().maxVisibleCharacters,Is.EqualTo(15));
        }
        [Test]public void OneSharedTrackerKeepsCoalescedEqualLinesAndExpiredHudLiterals()
        {
            string line=state.PublicSpeech[0].Text;
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="bubble-round:speech:2",Sequence=2,SpeakerId="speaker",Speaker="FESTIVALGOER",Text=line,ExpiresAtWorldSeconds=7});
            state.SimulationSeconds=.5;Draw();
            Assert.That(root.GetComponentsInChildren<TextMeshPro>(false),Has.Length.EqualTo(2));
            Assert.That(presenter.Deliveries.Deliveries,Has.Count.EqualTo(2));
            var first=presenter.Deliveries.Find("bubble-round:speech:1");
            Assert.That(first.VisibleCharacters,Is.EqualTo(15));
            Draw();Assert.That(presenter.Deliveries.Find(first.Id),Is.SameAs(first),"a retransmission neither redelivers nor resets typing");
            state.SimulationSeconds=7;Draw();Assert.That(Text(),Is.Null);
            Assert.That(first.Text,Is.EqualTo(line),"an already-shown HUD caption may still finish reading after world expiry");
            Assert.That(first.WorldActive,Is.False);
            state.PublicSpeech[0].Retracted=true;Draw();Assert.That(first.Text,Is.Empty);
        }
        [Test]public void SpeechPausesWithTheWorldAndResumesOnTheSameRevealClock()
        {
            state.SimulationSeconds=.5;Draw();Assert.That(Text().maxVisibleCharacters,Is.EqualTo(15));
            state.Phase=FestivalSimulation.MinigamePhase;state.SimulationSeconds=5.5;state.Booth.Seconds=5;Draw();
            Assert.That(Text().maxVisibleCharacters,Is.EqualTo(15));
            state.Phase="Playing";state.SimulationSeconds=5.6;Draw();Assert.That(Text().maxVisibleCharacters,Is.EqualTo(18));
        }
        [Test]public void OnlyNewlyVisibleGlyphsNotifyAudioAndHiddenBacklogsStaySilent()
        {
            var revealed=new System.Collections.Generic.List<(string Id,int From,int To)>();
            presenter.GlyphsRevealed+=(delivery,from,to)=>revealed.Add((delivery.Id,from,to));
            state.SimulationSeconds=.5;Draw();Assert.That(revealed,Is.Empty,"late first observation has no audio backlog");
            state.SimulationSeconds=.6;Draw();Draw();
            Assert.That(revealed,Is.EqualTo(new[]{("bubble-round:speech:1",15,18)}));
            state.SimulationSeconds=.7;presenter.Apply(state,id=>body.transform,view.transform,Vector3.zero,"you",false,hidden:true);
            state.SimulationSeconds=.8;Draw();Assert.That(revealed,Has.Count.EqualTo(1),"returning from a menu suppresses accumulated glyphs");
            state.SimulationSeconds=.9;Draw();Assert.That(revealed[1],Is.EqualTo(("bubble-round:speech:1",24,27)));
            body.transform.position=Vector3.right*15.01f;state.SimulationSeconds=.95;Draw();
            body.transform.position=Vector3.right*2;state.SimulationSeconds=1;Draw();Assert.That(revealed,Has.Count.EqualTo(2));
            state.SimulationSeconds=1.1;Draw();Assert.That(revealed[2],Is.EqualTo(("bubble-round:speech:1",30,33)));
        }
        [Test]public void TheDeliveryAndObjectCachesAreBoundedAndPrivateRegistrationNeverPublishes()
        {
            state.PublicSpeech.Clear();
            for(int i=5;i<=12;i++)state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="bubble-round:speech:"+i,Sequence=i,SpeakerId="speaker",Text="Public",ExpiresAtWorldSeconds=7});
            Draw();Assert.That(presenter.Deliveries.Deliveries,Has.Count.EqualTo(8));
            for(int i=0;i<10;i++)presenter.Deliveries.RegisterLocal("check:"+i,"speaker","FESTIVALGOER","Private",0,7);
            Draw();Assert.That(presenter.Deliveries.Deliveries,Has.Count.EqualTo(16));
            Assert.That(root.GetComponentsInChildren<TextMeshPro>(true),Has.Length.EqualTo(16));
            Assert.That(state.PublicSpeech,Has.Count.EqualTo(8),"local presentation never writes the public window");
            presenter.Deliveries.RetractLocal("check:9");Draw();Assert.That(presenter.Deliveries.Find("check:9").WorldActive,Is.False);
            presenter.Clear();Draw();Assert.That(Text(),Is.Null);
            Assert.That(presenter.Deliveries.RegisterLocal("check:9","speaker","FESTIVALGOER","Stale",0,7),Is.Null);
        }
        // The host's own view is a new copy each frame, as a client's is each snapshot. A private check follows the check in
        // the view it came from, matched by reference there, so each frame's copy keeps its opener up and a check from an older view
        // speaks for none.
        [Test]public void APrivateCheckFollowsEachHostViewsOwnCopyOfTheCheck()
        {
            var game=new FestivalSimulation(7);var owner=game.AddPlayer("owner","Owner");game.State.Phase="Playing";game.State.SimulationSeconds=5;game.State.Npcs.Clear();
            game.State.Npcs.Add(new NpcState{Id="goer_0",Role="Regular",CanTalk=true,Z=1.5f,Yaw=180,Twist=FestivalSimulation.VipGuard});owner.X=owner.Z=0;
            game.HasLineOfSight=(ax,az,bx,bz)=>false;game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);
            var start=game.Execute(owner.Id,new GameCommand{Id="check",Kind="ConfirmChat",TargetId="goer_0"});Assert.That(start.Accepted,Is.True,start.Reason);
            var first=Festival.Network.FestivalSession.ViewFor(game,owner.Id);var second=Festival.Network.FestivalSession.ViewFor(game,owner.Id);
            PlayerState Owner(RoundState view)=>view.Players.Find(p=>p.Id==owner.Id);
            var check=FestivalChatWindow.LiveChat(first,Owner(first));Assert.That(check?.Kind,Is.EqualTo("ConfirmChat"),"setup: the host's view carries the check");
            string opener=first.RoundId+":check:"+check.Id+":opener";
            presenter.FollowCheck(first,Owner(first),check,0,5);
            Assert.That(presenter.Deliveries.Find(opener)?.Retracted,Is.False,"the check's opener speaks");
            presenter.FollowCheck(second,Owner(second),FestivalChatWindow.LiveChat(second,Owner(second)),0,5);
            Assert.That(presenter.Deliveries.Find(opener).Retracted,Is.False,"the next view's own copy of the check keeps it speaking");
            presenter.FollowCheck(second,Owner(second),check,0,5);
            Assert.That(presenter.Deliveries.Find(opener).Retracted,Is.True,"a check from an older view is not this view's check");
        }
        [Test]
        public void EssentialResourcesResolveAndRenderFallbackAndRuntimeFestivalText()
        {
            var settings=Resources.Load<TMP_Settings>("TMP Settings");
            Assert.That(settings,Is.Not.Null,"The installed TMP Essential Resources must be imported.");
            Assert.That(TMP_Settings.instance,Is.SameAs(settings));
            var fallback=TMP_Settings.defaultFontAsset;
            Assert.That(fallback,Is.Not.Null);
            Assert.That(fallback,Is.SameAs(Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")));
            Assert.That(fallback.atlasTexture,Is.Not.Null);
            Assert.That(TMP_Settings.defaultSpriteAsset,Is.Not.Null);
            Assert.That(TMP_Settings.defaultStyleSheet,Is.Not.Null);
            var shader=Shader.Find("TextMeshPro/Mobile/Distance Field");
            Assert.That(shader,Is.Not.Null);
            Assert.That(shader.isSupported,Is.True);
            Assert.That(fallback.material.shader,Is.SameAs(shader),"The Resources font must retain the runtime bubble shader.");

            var source=Resources.Load<Font>("FestivalDisplay");
            Assert.That(source,Is.Not.Null);
            TMP_FontAsset dynamicFont=null;
            GameObject root=null;
            try
            {
                root=new GameObject("TMP resource consumer");
                var text=root.AddComponent<TextMeshPro>();
                text.font=fallback;
                text.text="Festival friends!";
                text.ForceMeshUpdate();
                Assert.That(text.textInfo.characterCount,Is.EqualTo(17));
                Assert.That(text.mesh.vertexCount,Is.GreaterThan(0));
                Assert.That(text.fontSharedMaterial.shader,Is.SameAs(shader));

                dynamicFont=TMP_FontAsset.CreateFontAsset(source);
                Assert.That(dynamicFont,Is.Not.Null);
                Assert.That(dynamicFont.sourceFontFile,Is.SameAs(source));
                Assert.That(dynamicFont.atlasPopulationMode,Is.EqualTo(AtlasPopulationMode.Dynamic));
                dynamicFont.material.shader=shader;
                text.font=dynamicFont;
                text.ForceMeshUpdate();
                Assert.That(text.textInfo.characterCount,Is.EqualTo(17));
                Assert.That(text.mesh.vertexCount,Is.GreaterThan(0));
                Assert.That(text.fontSharedMaterial.shader,Is.SameAs(fallback.material.shader));
            }
            finally
            {
                if(root!=null)Object.DestroyImmediate(root);
                if(dynamicFont!=null)
                {
                    foreach(var atlas in dynamicFont.atlasTextures)if(atlas!=null)Object.DestroyImmediate(atlas);
                    Object.DestroyImmediate(dynamicFont.material);
                    Object.DestroyImmediate(dynamicFont);
                }
            }
        }
    }
}
