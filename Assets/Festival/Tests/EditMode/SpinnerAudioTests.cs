using System;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Festival.Tests
{
    public sealed class SpinnerAudioTests
    {
        GameObject root;
        FestivalSpinner spinner;
        RoundState state;
        ProfileData profile;
        [SetUp] public void SetUp()
        {
            root=new GameObject("Slot audio test");spinner=root.AddComponent<FestivalSpinner>();
            typeof(FestivalSpinner).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(spinner,null);
            state=new RoundState{RoundId="audio_round",Phase="Spinning",SpinSeed=71,SpinEndsAt=110,Tick=10};
            state.Players.Add(new PlayerState{Id="p",Name="Friend"});state.TripperId="p";
            profile=new ProfileData{EffectsVolume=.4f,MasterVolume=.7f,MuteWhenUnfocused=true};
        }
        [TearDown] public void TearDown(){if(spinner!=null)Dispose();UnityEngine.Object.DestroyImmediate(root);}
        void Dispose()=>typeof(FestivalSpinner).GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(spinner,null);
        int Follow(double elapsed,bool hidden=false,bool focused=true)
        {
            var method=typeof(FestivalSpinner).GetMethod("FollowAudio",BindingFlags.Public|BindingFlags.Instance);
            Assert.That(method,Is.Not.Null,"the actual spinner must consume the public spin timeline and emit local cues");
            return (int)method.Invoke(spinner,new object[]{state,100+elapsed,hidden,profile,focused});
        }
        AudioSource Source=>root.GetComponents<AudioSource>().Single(s=>s.name=="Slot audio test"&&s.clip!=null);
        [Test] public void AwakeOwnsOneEffectsSourceAndThreeOriginalClips()
        {
            Assert.That(root.GetComponents<AudioSource>().Length,Is.EqualTo(1),"actual spinner creates its own local source");
            Assert.That(Resources.FindObjectsOfTypeAll<AudioClip>().Count(c=>c.name.StartsWith("Original slot ")),Is.EqualTo(3),"lever, tick and stop sounds are generated locally");
        }
        [Test] public void PublicTimelinePullsTicksAndClunksOnce()
        {
            Assert.That(Follow(-.05),Is.Zero,"a prestart view must wait for the lever edge");
            Assert.That(Follow(0),Is.EqualTo(1),"on-time lever");Assert.That(Source.clip.name,Does.Contain("lever"));
            Assert.That(Source.spatialBlend,Is.Zero);Assert.That(Source.playOnAwake,Is.False);
            Assert.That(Follow(0),Is.Zero,"repeated snapshot");
            Assert.That(Follow(.2),Is.EqualTo(1),"moving payline tick");Assert.That(Source.clip.name,Does.Contain("tick"));
            Follow(2.49);Assert.That(Follow(2.5),Is.EqualTo(1));Assert.That(Source.clip.name,Does.Contain("clunk"));
            Assert.That(Follow(2.5),Is.Zero);
            Follow(3.99);Assert.That(Follow(4),Is.EqualTo(1));
            Follow(5.99);Assert.That(Follow(6),Is.EqualTo(1));
            Assert.That(Follow(7),Is.Zero,"take camera has no reel ticks");
        }
        // A spin frame's beat, bulbs and audio follow make no garbage, from the lever to the last reel's slow-down.
        [Test] public void AFollowedSpinFrameMakesNoGarbage()
        {
            var crew=FestivalSpinner.Crew(state);double now=100;Follow(0);
            TestDelegate frame=()=>{now+=.016;FestivalSpinner.At(state,crew,now);for(int b=0;b<FestivalSpinner.Bulbs;b++){FestivalSpinner.BulbLit((float)(now-100),b,false);FestivalSpinner.BulbLit((float)(now-100),b,true);}spinner.FollowAudio(state,now,false,profile,true);};frame();
            Assert.That(frame,Is.Not.AllocatingGCMemory(),"a spin frame makes no garbage");
        }
        [Test] public void LateStalledRepeatedAndOutOfOrderViewsNeverCatchUp()
        {
            Assert.That(Follow(3),Is.Zero,"late first view baselines");
            Assert.That(Follow(3),Is.Zero);Assert.That(Follow(2),Is.Zero,"older estimated clock ignored");
            Assert.That(Follow(3.1),Is.EqualTo(1),"only current tick");
            // The last reel's ticks slow from 4.5 s, so these steps stay while it still runs at full speed.
            Assert.That(Follow(4.1),Is.Zero,"stall baselines instead of replaying clunks/ticks");
            Assert.That(Follow(4.2),Is.EqualTo(1));
            state.Tick=9;state.SpinSeed=70;Assert.That(Follow(4.3),Is.Zero,"stale timeline identity ignored");
            state.Tick=11;state.SpinSeed=72;Assert.That(Follow(4.3),Is.Zero,"new identity late baselines");
            Assert.That(Follow(4.4),Is.EqualTo(1));
        }
        [Test] public void HiddenPhaseMutedAndFocusBoundariesBaselineWithoutBursts()
        {
            Follow(0);Assert.That(Follow(2.49,true),Is.Zero);Assert.That(Follow(4.1),Is.Zero,"menu reopen");
            Assert.That(Follow(4.2),Is.EqualTo(1));
            profile.EffectsVolume=0;Assert.That(Follow(4.3),Is.Zero);Assert.That(Source.volume,Is.Zero);
            profile.EffectsVolume=.4f;Assert.That(Follow(4.4),Is.Zero,"unmute baselines");
            Assert.That(Follow(4.5),Is.EqualTo(1));Assert.That(Source.volume,Is.EqualTo(.4f*.45f).Within(.001));
            Assert.That(Follow(4.6,false,false),Is.Zero);Assert.That(Follow(4.7),Is.Zero,"focus return baselines");
            profile.MuteWhenUnfocused=false;Assert.That(Follow(4.8,false,false),Is.EqualTo(1),"unfocused audio enabled");
            state.Phase="Loading";Assert.That(Follow(5),Is.Zero);state.Phase="Spinning";Assert.That(Follow(5.1),Is.Zero);
            spinner.enabled=false;Assert.That(Follow(5.2),Is.Zero);spinner.enabled=true;Assert.That(Follow(5.3),Is.Zero);
        }
        [Test] public void AnOlderRoundCannotRestartItsLeverAfterTwoRoundChanges()
        {
            Assert.That(Follow(0),Is.EqualTo(1));state.RoundId="second";Assert.That(Follow(0),Is.EqualTo(1));
            state.RoundId="third";Assert.That(Follow(0),Is.EqualTo(1));
            state.RoundId="audio_round";Assert.That(Follow(0),Is.Zero,"an old round cannot become a new audio identity again");
        }
        [Test] public void RoundSeedAndStartIdentityAreIndependentAndOwnedAudioIsDisposed()
        {
            Follow(0);var sources=root.GetComponents<AudioSource>();var clips=Resources.FindObjectsOfTypeAll<AudioClip>().Where(c=>c.name.StartsWith("Original slot ")).ToArray();
            Assert.That(sources.Length,Is.EqualTo(1));Assert.That(clips.Length,Is.EqualTo(3));
            foreach(var clip in clips){var samples=new float[clip.samples];Assert.That(clip.GetData(samples,0),Is.True);Assert.That(samples.Any(v=>Math.Abs(v)>.001),Is.True);}
            state.Tick++;state.SpinEndsAt=111;Assert.That(Follow(1),Is.EqualTo(1),"new start pulls on time");
            state.Tick++;state.SpinSeed++;Assert.That(Follow(1),Is.EqualTo(1),"new seed pulls on time");
            state.RoundId="next_round";state.Tick=0;state.SpinEndsAt=110;Assert.That(Follow(0),Is.EqualTo(1));
            state.RoundId="audio_round";Assert.That(Follow(1.1),Is.Zero,"previous round is stale");
            Dispose();UnityEngine.Object.DestroyImmediate(spinner);
            Assert.That(new[]{sources.All(s=>s==null),clips.All(c=>c==null)},Is.EqualTo(new[]{true,true}),"component disposal removes its own sources and procedural clips");
        }
    }
}
