using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Festival.Core;
using Festival.Presentation;

namespace Festival.Tests
{
    public sealed class BabbleTests
    {
        GameObject root,body,view;
        FestivalSpeechBubbles presenter;
        RoundState state;
        [SetUp]public void SetUp()
        {
            root=new GameObject("Babble presenter");presenter=root.AddComponent<FestivalSpeechBubbles>();
            body=new GameObject("Babble speaker");view=new GameObject("Babble view");
            state=new RoundState{RoundId="babble-round",Phase="Playing"};
            state.Npcs.Add(new NpcState{Id="speaker",Kind="Wook"});
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="babble-round:speech:1",Sequence=1,SpeakerId="speaker",Text="a b!aaaaaa",ExpiresAtWorldSeconds=7});
        }
        [TearDown]public void TearDown(){presenter.Clear();Object.DestroyImmediate(root);Object.DestroyImmediate(body);Object.DestroyImmediate(view);}
        void Draw(double seconds,bool hidden=false)
        {
            state.SimulationSeconds=seconds;
            presenter.Apply(state,id=>id=="speaker"?body.transform:null,view.transform,Vector3.zero,"you",true,hidden:hidden);
        }
        AudioSource[] Sources()=>root.GetComponentsInChildren<AudioSource>(true);
        [Test]public void RevealingALetterProducesAShortAudibleSpatialBlip()
        {
            Draw(0);Draw(1d/30);
            var source=Sources().FirstOrDefault(s=>s.clip!=null);
            Assert.That(source,Is.Not.Null,"the real glyph reveal must send generated audio to an AudioSource");
            Assert.That(source.spatialBlend,Is.EqualTo(1));Assert.That(source.maxDistance,Is.EqualTo(15));
            Assert.That(source.clip.length/source.pitch,Is.LessThan(.08f));
            var data=new float[source.clip.samples];source.clip.GetData(data,0);
            Assert.That(data.Max(v=>Mathf.Abs(v)),Is.GreaterThan(.05f));
            Assert.That(data[0],Is.EqualTo(0).Within(.001f));Assert.That(data[data.Length-1],Is.EqualTo(0).Within(.001f));
        }
        object Voice(string id,string kind)
        {
            var method=typeof(FestivalBabble).GetMethod("VoiceFor");
            Assert.That(method,Is.Not.Null,"speaker identity must select a deterministic voice within its public kind's band");
            return method.Invoke(null,new object[]{id,kind});
        }
        float Part(object voice,string name)=>(float)voice.GetType().GetField(name).GetValue(voice);
        [Test]public void TheSameIdAlwaysGetsTheSameVoiceAndTwentyFestivalgoersGetAtLeastTwelveVoices()
        {
            var before=Random.state;Random.InitState(725);var expected=Random.value;Random.InitState(725);
            try
            {
                var pitches=new System.Collections.Generic.HashSet<float>();
                for(int i=0;i<20;i++)
                {
                    var voice=Voice("festivalgoer:"+i,"Wook");var repeat=Voice("festivalgoer:"+i,"Wook");
                    Assert.That(Part(voice,"Frequency"),Is.EqualTo(Part(repeat,"Frequency")));
                    Assert.That(Part(voice,"Speed"),Is.EqualTo(Part(repeat,"Speed")));
                    Assert.That(Part(voice,"Frequency"),Is.InRange(150,300));Assert.That(Part(voice,"Speed"),Is.InRange(.85f,1.15f));
                    pitches.Add(Part(voice,"Frequency"));
                }
                Assert.That(pitches.Count,Is.GreaterThanOrEqualTo(12));Assert.That(Random.value,Is.EqualTo(expected),"voice selection must never reseed gameplay's Unity RNG");
                Assert.That(Part(Voice("officer","Cop"),"Frequency"),Is.InRange(95,140));
                Assert.That(Part(Voice("guard","VipGuard"),"Frequency"),Is.InRange(95,140));
                Assert.That(Part(Voice("friend","Player"),"Frequency"),Is.InRange(170,340));
            }
            finally{Random.state=before;}
        }
        [Test]public void VowelsUseACachedPairAndConsonantsUseATwelveMillisecondTick()
        {
            state.PublicSpeech[0].Text="aaa b! ";Draw(0);Draw(1d/30);
            var vowel=Sources().First(s=>s.clip!=null).clip;
            Assert.That(vowel.length,Is.EqualTo(.055f).Within(.0001f));
            Draw(2d/30);Draw(3d/30);
            Assert.That(Sources().Where(s=>s.clip!=null).All(s=>s.clip==vowel),Is.True,"repeated vowels reuse their generated clip");
            var clips=Sources().Select(s=>s.clip).ToArray();Draw(4d/30);
            Assert.That(Sources().Select(s=>s.clip),Is.EqualTo(clips),"a revealed space does not dispatch a blip");
            Draw(5d/30);
            Assert.That(Sources().Any(s=>s.clip!=null&&Mathf.Abs(s.clip.length-.012f)<.0001f),Is.True,"consonants have the short tick rather than a vowel blip");
            clips=Sources().Select(s=>s.clip).ToArray();Draw(6d/30);Draw(7d/30);
            Assert.That(Sources().Select(s=>s.clip),Is.EqualTo(clips),"punctuation and trailing spaces are silent too");
        }
        [Test]public void QuestionEndingsLiftTheVoiceWithoutExceedingTheSpeedBand()
        {
            state.PublicSpeech[0].Text="aaaaaaaaa?";Draw(0);Draw(1d/30);
            float first=Sources().Max(s=>s.pitch);Draw(9d/30);
            Assert.That(Sources().Max(s=>s.pitch),Is.GreaterThan(first+.02f));
            Assert.That(Sources().All(s=>s.pitch>=.85f&&s.pitch<=1.15f),Is.True);
        }
        [Test]public void RangeMenusRetractionAndHiddenActorsImmediatelyMuteExistingAudio()
        {
            Draw(0);Draw(1d/30);Assert.That(Sources().Any(s=>s.volume>0),Is.True);
            body.transform.position=Vector3.right*15.001f;Draw(2d/30);
            Assert.That(Sources().All(s=>s.volume==0),Is.True,"a moving source stops at the hard cutoff, even during a blip");
            body.transform.position=Vector3.right*2;Draw(3d/30);Draw(4d/30);
            Assert.That(Sources().Any(s=>s.volume>0),Is.True);
            Draw(5d/30,true);Assert.That(Sources().All(s=>s.volume==0),Is.True);
            Draw(6d/30);Draw(7d/30);body.SetActive(false);Draw(8d/30);Assert.That(Sources().All(s=>s.volume==0),Is.True);
            body.SetActive(true);state.PublicSpeech[0].Retracted=true;Draw(9d/30);
            Assert.That(Sources().All(s=>s.volume==0),Is.True);
        }
        [Test]public void RetractingOneLineStopsItsBlipWhileAnotherLineFromThatSpeakerRemains()
        {
            state.PublicSpeech[0].Text="a";Draw(0);Draw(1d/30);
            var first=Sources().First(source=>source.clip!=null);
            state.PublicSpeech.Add(new PublicSpeechState{DeliveryId="babble-round:speech:2",Sequence=2,SpeakerId="speaker",Text="a",StartedAtWorldSeconds=1d/30,ExpiresAtWorldSeconds=7});
            Draw(1d/30);Draw(2d/30);
            Assert.That(Sources(),Has.Length.EqualTo(2),"the two overlapping blips share a bounded speaker cache");
            state.PublicSpeech[0].Retracted=true;Draw(2d/30);
            Assert.That(first.volume,Is.Zero,"the retracted delivery's source must mute even when this speaker has another active line");
            Assert.That(Sources().Any(source=>source.volume>0),Is.True,"the other retained line is still audible");
        }
        [Test]public void APrivateDeliveryFromAForeignTrackerCannotPlayInThisOwnersSession()
        {
            state.Players.Add(new PlayerState{Id="you"});state.PublicSpeech[0].Retracted=true;Draw(0);
            var foreign=new FestivalSpeechDeliveryTracker();foreign.Follow(state,0,1d/30);
            var privateLine=foreign.RegisterLocal("foreign-check","speaker","FESTIVALGOER","a",0,7);
            presenter.Babble.PlayRevealed(privateLine,0,1);
            Assert.That(Sources(),Is.Empty,"private audio accepts only this session's shared owner-local registrations");
            var own=presenter.Deliveries.RegisterLocal("owner-check","speaker","FESTIVALGOER","a",0,7);
            Assert.That(own.VisibleCharacters,Is.Zero,"this owner's accepted line has not revealed a letter yet");
            presenter.Babble.PlayRevealed(own,0,1);
            Assert.That(Sources(),Is.Empty,"a local adapter cannot play a glyph ahead of the shared reveal clock");
            presenter.Deliveries.Follow(state,0,1d/30);Assert.That(own.VisibleCharacters,Is.EqualTo(1));
            presenter.Babble.PlayRevealed(own,0,1);
            Assert.That(Sources().Any(source=>source.clip!=null),Is.True,"an owner-local check is audible without writing public speech");
            Assert.That(state.PublicSpeech,Has.Count.EqualTo(1));
        }
    }
}
