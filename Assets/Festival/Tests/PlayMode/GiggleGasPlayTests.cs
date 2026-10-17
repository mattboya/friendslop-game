using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // GAS-1 in a running session: at camp, beside the Giggle Balloons, the prompt offers one and E takes it (GAS-4: two at a time,
    // and two drain the grade further). Once it kicks in, the
    // gas reaches the hitter's screen through the single writers: FestivalHud adds the kick to the field of view, FestivalNightLighting
    // drains the colour grade, and FestivalTrip pulses its overlay, closes a low-pass, adds a big reverb (GAS-5) and dips the volume.
    // All of it lets go when the gas ends, when the player leaves the session, and when FestivalTrip is turned off or destroyed.
    // Friends see the hitter giggle. GAS-5: it pulses on the camp music's beat, and on a 120 BPM clock with the music off.
    public sealed class GiggleGasPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator EAtTheBalloonsTakesAHitThatPulsesThroughTheSingleWriters()
        {
            var world=new GameObject("Gas world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Gas session");var session=host.AddComponent<FestivalSession>();var hud=host.AddComponent<FestivalHud>();
            yield return null;
            bool reduced=session.Profile.Data.ReducedMotion;
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=(FestivalSimulation)typeof(FestivalSession).GetProperty("DevelopmentSimulation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var player=sim.Player(session.LocalPlayerId);var me=session.LocalPlayerId;var view=session.ViewCamera;
                Assert.That(world.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade),Is.True,"setup: the world's colour grade");
                float camp=grade.saturation.value;var campFog=(RenderSettings.fogStartDistance,RenderSettings.fogEndDistance,RenderSettings.fogColor);
                Text Find(string name){foreach(var text in host.GetComponentsInChildren<Text>(false))if(text.name==name)return text;return null;}
                var primary=typeof(FestivalHud).GetField("primaryAction",BindingFlags.NonPublic|BindingFlags.Instance);
                var campAudio=host.GetComponent<FestivalCampAudio>();
                FestivalTrip.Perception Now()=>FestivalTrip.For(session.State,me,session.Profile.Data.ReducedMotion,Time.unscaledTime,campAudio.Beat);
                var overlay=host.transform.Find("Festival trip");
                void Sober(string when)
                {
                    var lowPass=view.GetComponent<AudioLowPassFilter>();var reverb=view.GetComponent<AudioReverbFilter>();
                    Assert.That(overlay==null||!overlay.gameObject.activeSelf,when+": no overlay");
                    Assert.That(lowPass==null||!lowPass.enabled,when+": no low-pass");
                    Assert.That(reverb==null||!reverb.enabled,when+": no reverb");
                    Assert.That(AudioListener.volume,Is.EqualTo(1),when+": the sound at full");
                }

                // Beside the balloons, E takes one.
                player.X=CampFeatures.GiggleBalloonX+1.2f;player.Z=CampFeatures.GiggleBalloonZ;
                yield return new WaitForSeconds(.5f);
                Assert.That(Find("Prompt")?.text,Is.EqualTo("TAKE A GIGGLE BALLOON"),"beside the balloons the prompt offers one, on E");
                ((System.Action)primary.GetValue(hud))();
                var gas=player.Effects.Find(e=>e.Id==FestivalSimulation.GiggleGasEffect);
                Assert.That(gas,Is.Not.Null,"E takes a hit: "+session.Message);
                Assert.That(session.Message,Is.EqualTo("Giggle Balloon: give it a few seconds…"),"and the host says so");
                // GAS-4: two at a time. While the first kicks in, the prompt offers another and E takes it; then E does nothing.
                yield return new WaitForSeconds(.5f);
                Assert.That(Find("Prompt")?.text,Is.EqualTo("TAKE A GIGGLE BALLOON"),"with one on, the prompt offers another");
                ((System.Action)primary.GetValue(hud))();
                var hits=player.Effects.FindAll(e=>e.Id==FestivalSimulation.GiggleGasEffect);
                Assert.That(hits.Count,Is.EqualTo(2),"E takes a second hit: "+session.Message);
                yield return new WaitForSeconds(.5f);
                Assert.That(Find("Prompt")?.text,Is.EqualTo("TWO GIGGLE BALLOONS AT A TIME"),"two at a time");
                Assert.That(primary.GetValue(hud),Is.Null,"so E does nothing");
                overlay=host.transform.Find("Festival trip");
                Sober("while it kicks in");
                Assert.That(view.fieldOfView,Is.EqualTo(75).Within(1e-3f),"while it kicks in: the view holds still");
                Assert.That(grade.saturation.value,Is.EqualTo(camp).Within(1e-3f),"while it kicks in: camp's colours");
                Assert.That(session.WorldCharacter(me).Giggling,Is.False,"while it kicks in: no giggle");

                // Kicked in (the wait skipped), at full motion: each writer moves its own property with the pulse, every frame. A test
                // resumes after the frame's Updates (the HUD's kick, the session's grade) and before its LateUpdates, so the overlay and
                // the sound FestivalTrip drew are last frame's moment, on last frame's beat.
                foreach(var hit in hits)hit.StartSeconds=sim.State.SimulationSeconds-5;session.Profile.Data.ReducedMotion=false;
                yield return null;yield return null;
                float widest=0,quietest=1,drawnAt=Time.unscaledTime;float? drawnBeat=campAudio.Beat;
                for(deadline=Time.realtimeSinceStartup+1.5f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    var look=Now();var drawn=FestivalTrip.For(session.State,me,false,drawnAt,drawnBeat);drawnAt=Time.unscaledTime;drawnBeat=campAudio.Beat;var lowPass=view.GetComponent<AudioLowPassFilter>();
                    Assert.That(view.fieldOfView,Is.EqualTo(75+look.Fov).Within(.01f),"the HUD adds the gas's kick to the 75 degree view");
                    Assert.That(grade.saturation.value,Is.EqualTo(camp+look.Saturation).Within(1e-3f),"the one grade drains camp's colours");
                    overlay=host.transform.Find("Festival trip");
                    Assert.That(overlay!=null&&overlay.gameObject.activeSelf,"the overlay is up");
                    Assert.That(overlay.Find("Trip vignette").GetComponent<RawImage>().color.a,Is.EqualTo(drawn.Vignette).Within(1e-4f),"its vignette pulses");
                    Assert.That(lowPass!=null&&lowPass.enabled,"the first-person listener is low-passed");
                    Assert.That(lowPass.cutoffFrequency,Is.EqualTo(FestivalTrip.LowPassHz(drawn.Muffle)).Within(1),"on the pulse");
                    Assert.That(AudioListener.volume,Is.EqualTo(1-drawn.Tremolo).Within(1e-4f),"and the tremolo dips it");
                    var reverb=view.GetComponent<AudioReverbFilter>();
                    Assert.That(reverb!=null&&reverb.enabled,"a big reverb on the first-person listener");
                    Assert.That(reverb.room,Is.EqualTo(FestivalTrip.ReverbRoom(drawn.Reverb)).Within(1),"as wet as the gas is strong");
                    widest=Mathf.Max(widest,Mathf.Abs(view.fieldOfView-75));quietest=Mathf.Min(quietest,AudioListener.volume);
                }
                Assert.That(grade.saturation.value,Is.LessThan(camp+FestivalTrip.Gas(1,false,0).Saturation-30),"both kicked in, the colours drain past a single hit's drain");
                Assert.That(grade.saturation.value,Is.EqualTo(camp-95).Within(1e-3f),"by two hits' 95, through the one grade");
                Assert.That(session.WorldCharacter(me).Giggling,Is.True,"the hitter's body giggles, as friends see it");
                Assert.That(widest,Is.GreaterThan(2),"the view kicks wide on the beat");
                Assert.That(quietest,Is.LessThan(.8f),"the sound goes wah-wah");

                // GAS-5: on the camp music's beat. A new camp plays TECH HOUSE, about 129 BPM and drifting, not the 120 BPM clock. Each
                // frame the vignette drawn last frame is read with where the music was then. Its peaks land on the track's kicks (its
                // measured beat grid, which FestivalMusicTests holds to the onsets heard) within 60 ms, one a kick, beat after beat.
                var techHouse=FestivalMusic.For(1);
                Assert.That(campAudio.Music!=null&&campAudio.Music.isPlaying&&campAudio.Music.clip==techHouse.Audio,"setup: TECH HOUSE plays at camp");
                var vignette=overlay.Find("Trip vignette").GetComponent<RawImage>();
                var drawnVignette=new List<float>();var heardAt=new List<int>();int was=campAudio.Music.timeSamples;
                for(deadline=Time.realtimeSinceStartup+2.5f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    drawnVignette.Add(vignette.color.a);heardAt.Add(was);was=campAudio.Music.timeSamples;
                }
                int kicks=0,peaks=0;var late=new List<string>();
                for(int i=1;i<drawnVignette.Count-1;i++)
                {
                    if(techHouse.BeatPhase(heardAt[i])<techHouse.BeatPhase(heardAt[i-1]))kicks++;
                    if(!(drawnVignette[i]>drawnVignette[i-1]&&drawnVignette[i]>=drawnVignette[i+1]))continue;
                    peaks++;float phase=techHouse.BeatPhase(heardAt[i]),off=Mathf.Min(phase,1-phase)*60/(float)techHouse.Bpm;
                    if(off>.06f)late.Add($"{off*1000:F0} ms");
                }
                Assert.That(kicks,Is.GreaterThanOrEqualTo(4),"setup: four beats or more of TECH HOUSE");
                Assert.That(late,Is.Empty,"the vignette peaks within 60 ms of each of the music's kicks ("+peaks+" peaks, "+kicks+" kicks)");
                Assert.That(peaks,Is.InRange(kicks-1,kicks+1),"one peak a kick");

                // GAS-5 decision A: with the DJ on SILENCE there is no beat to follow, and the gas keeps pulsing on a 120 BPM clock.
                sim.State.CampMusicTrack=0;
                for(deadline=Time.realtimeSinceStartup+2;campAudio.Music!=null&&Time.realtimeSinceStartup<deadline;)yield return null;
                yield return null;
                Assert.That(campAudio.Beat,Is.Null,"setup: SILENCE, no beat");
                float dimmest=1,deepest=0;
                for(deadline=Time.realtimeSinceStartup+.6f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    Assert.That(view.fieldOfView,Is.EqualTo(75+Now().Fov).Within(.01f),"the zoom keeps the clock's beat");
                    dimmest=Mathf.Min(dimmest,vignette.color.a);deepest=Mathf.Max(deepest,vignette.color.a);
                }
                Assert.That(deepest-dimmest,Is.GreaterThan(.2f),"the vignette still pulses with the music off");
                sim.State.CampMusicTrack=1;

                // Reduced motion: the view holds still; the sound still pulses.
                session.Profile.Data.ReducedMotion=true;quietest=1;
                yield return null;
                for(deadline=Time.realtimeSinceStartup+.6f;Time.realtimeSinceStartup<deadline;)
                {
                    yield return null;
                    Assert.That(view.fieldOfView,Is.EqualTo(75).Within(1e-3f),"reduced motion: no throb");
                    quietest=Mathf.Min(quietest,AudioListener.volume);
                }
                Assert.That(quietest,Is.LessThan(.8f),"reduced motion: the wah-wah stays");

                // The host ends both: every writer lets go, and the balloons offer another.
                foreach(var hit in hits)hit.RemainingSeconds=.01;
                yield return new WaitForSeconds(.5f);
                Assert.That(player.Effects.Exists(e=>e.Id==FestivalSimulation.GiggleGasEffect),Is.False,"setup: the host removed it");
                Sober("once it has worn off");
                Assert.That(view.fieldOfView,Is.EqualTo(75).Within(1e-3f),"once it has worn off: the view");
                Assert.That(grade.saturation.value,Is.EqualTo(camp).Within(1e-3f),"once it has worn off: camp's colours");
                Assert.That(session.WorldCharacter(me).Giggling,Is.False,"once it has worn off: the giggling stops");
                Assert.That(Find("Prompt")?.text,Is.EqualTo("TAKE A GIGGLE BALLOON"),"and E takes another");

                // Another, kicked in at once, then the player leaves the session: the sound comes back for the menu.
                ((System.Action)primary.GetValue(hud))();
                gas=player.Effects.Find(e=>e.Id==FestivalSimulation.GiggleGasEffect);Assert.That(gas,Is.Not.Null,"setup: a second hit");
                gas.StartSeconds=sim.State.SimulationSeconds-5;
                for(deadline=Time.realtimeSinceStartup+1;Time.realtimeSinceStartup<deadline&&AudioListener.volume>.8f;)yield return null;
                Assert.That(AudioListener.volume,Is.LessThan(.8f),"setup: the second hit is dipping the sound");
                session.Leave();
                // The camp's own grade and fog are back as Leave returns, before the HUD's next frame could put them back.
                Assert.That(grade.saturation.value,Is.EqualTo(camp).Within(1e-3f),"as Leave returns: camp's colours");
                Assert.That((RenderSettings.fogStartDistance,RenderSettings.fogEndDistance,RenderSettings.fogColor),Is.EqualTo(campFog),"as Leave returns: camp's fog");
                yield return null;
                Sober("after leaving the session");
                Assert.That(grade.saturation.value,Is.EqualTo(camp).Within(1e-3f),"after leaving the session: camp's colours");
                Assert.That((RenderSettings.fogStartDistance,RenderSettings.fogEndDistance,RenderSettings.fogColor),Is.EqualTo(campFog),"after leaving the session: camp's fog");

                // FestivalTrip turned off, or destroyed, with the gas on: the global volume never stays dipped.
                var trip=host.GetComponent<FestivalTrip>();
                var on=new RoundState{Phase="Shopping",SimulationSeconds=100};var hitter=new PlayerState{Id="me"};
                hitter.Effects.Add(new ActiveEffect{Id=FestivalSimulation.GiggleGasEffect,StartSeconds=95,RemainingSeconds=25});on.Players.Add(hitter);
                trip.Apply(on,"me",view.transform,null,false,0,.02f);
                Assert.That(AudioListener.volume,Is.LessThan(.8f),"setup: the gas on a kick");
                trip.enabled=false;
                Sober("with the trip layer turned off");
                trip.enabled=true;trip.Apply(on,"me",view.transform,null,false,0,.02f);
                Assert.That(AudioListener.volume,Is.LessThan(.8f),"setup: on again");
                Object.DestroyImmediate(trip);
                Assert.That(AudioListener.volume,Is.EqualTo(1),"destroyed, it leaves the sound at full");
            }
            finally
            {
                if(session!=null){session.Profile.Data.ReducedMotion=reduced;session.Leave();}
                AudioListener.volume=1;
                foreach(var name in new[]{"First-person camera","Authoritative actor presentation"}){var leftover=GameObject.Find(name);if(leftover!=null)Object.Destroy(leftover);}
                Object.Destroy(host);Object.Destroy(world);
            }
            yield return null;
        }

        // GAS-6: every take a session reports plays the inhale once: in the hitter's own head (2-D), and for a friend in 3-D from the
        // hitter's body, with no Doppler, fading to silence at 15 m. Two takes reported together play one inhale, not two at once.
        [UnityTest]public IEnumerator EveryTakeInhalesOnceInTheHittersHeadAndFromTheirBodyForAFriend()
        {
            yield return HostAndFriend("Inhale");
            try
            {
                var sim=Simulation(host);string me=host.LocalPlayerId,pal=friend.LocalPlayerId;
                var mine=hostObject.GetComponent<FestivalInhale>();var theirs=friendObject.GetComponent<FestivalInhale>();
                Assert.That(mine!=null&&theirs!=null,"every session plays the inhales");
                Stand(sim,me,1.2f);Stand(sim,pal,-1.2f);
                yield return Settle(.8f);
                Assert.That((mine.Played,theirs.Played),Is.EqualTo((0,0)),"hosting and joining play nothing");

                host.Command(FestivalSimulation.TakeGiggleBalloonKind);
                Assert.That(sim.Player(me).VisualGiggleTakeSequence,Is.EqualTo(1),"setup: the host takes a balloon: "+host.Message);
                yield return Until(()=>theirs.Played>0,3);
                Assert.That(mine.Played,Is.EqualTo(1),"the host hears their own take once");
                var own=Voice(mine,me);
                Assert.That(own!=null&&own.isPlaying,"the host's own inhale plays");
                Assert.That(own.spatialBlend,Is.Zero,"in their own head: 2-D");
                Assert.That(own.clip.length,Is.InRange(.8f,1.4f),"the inhale");
                Assert.That(theirs.Played,Is.EqualTo(1),"the friend hears it once");
                var heard=Voice(theirs,me);
                Assert.That(heard!=null&&heard.isPlaying,"the host's inhale plays for the friend");
                Assert.That(heard.spatialBlend,Is.EqualTo(1),"in 3-D");
                Assert.That(Vector3.Distance(heard.transform.position,friend.WorldCharacter(me).transform.position),Is.LessThan(2),"from the host's body, where the friend sees it");
                Assert.That(heard.dopplerLevel,Is.Zero,"with no Doppler");
                Assert.That((heard.rolloffMode,heard.maxDistance),Is.EqualTo((AudioRolloffMode.Linear,FestivalInhale.Range)),"fading to silence at 15 m");

                friend.Command(FestivalSimulation.TakeGiggleBalloonKind);
                yield return Until(()=>mine.Played>1&&theirs.Played>1,3);
                Assert.That(theirs.Played,Is.EqualTo(2),"the friend hears their own take: "+friend.Message);
                Assert.That(Voice(theirs,pal).spatialBlend,Is.Zero,"in their own head");
                Assert.That(mine.Played,Is.EqualTo(2),"the host hears the friend's take");
                Assert.That(Voice(mine,pal).spatialBlend,Is.EqualTo(1),"in 3-D");
                Assert.That(Vector3.Distance(Voice(mine,pal).transform.position,host.WorldCharacter(pal).transform.position),Is.LessThan(2),"from the friend's body, where the host sees it");

                yield return Settle(1.6f);
                Assert.That((mine.Played,theirs.Played),Is.EqualTo((2,2)),"each take plays once, never again");
                Assert.That(Voice(mine,me).isPlaying||Voice(theirs,me).isPlaying,Is.False,"and the inhales end");

                // Two takes in one host step reach both sessions as one report.
                yield return WearOff(sim,me);
                Assert.That(Take(sim,me)&&Take(sim,me),"setup: two takes in one step");
                yield return Until(()=>theirs.Played>2,3);yield return Settle(.3f);
                Assert.That((mine.Played,theirs.Played),Is.EqualTo((3,3)),"two takes reported together play one inhale");
            }
            finally{LeaveCrew();}
            yield return null;
        }

        // GAS-6: a friend beyond 15 m still learns of a take but hears nothing, and a friend who rejoins hears no take from before.
        [UnityTest]public IEnumerator AFriendOutOfEarshotOrRejoiningHearsNoInhale()
        {
            yield return HostAndFriend("Far inhale");
            try
            {
                var sim=Simulation(host);string me=host.LocalPlayerId,pal=friend.LocalPlayerId;
                var theirs=friendObject.GetComponent<FestivalInhale>();int reports=0;friend.GiggleBalloonTaken+=(player,count)=>reports++;
                Stand(sim,me,1.2f);Stand(sim,pal,-1.2f);yield return Settle(.8f);
                Assert.That(Take(sim,me),"setup: a take beside the friend");
                yield return Until(()=>reports>0,3);yield return null;
                Assert.That(theirs.Played,Is.EqualTo(1),"a friend nearby hears it");

                Stand(sim,pal,20);yield return Settle(1);
                Assert.That(Vector3.Distance(friend.WorldCharacter(me).transform.position,friend.EyePosition),Is.GreaterThan(FestivalInhale.Range),"setup: the friend is out of earshot");
                Assert.That(Take(sim,me),"setup: a second take");
                yield return Until(()=>reports>1,3);yield return Settle(.3f);
                Assert.That(reports,Is.EqualTo(2),"setup: the friend's session reports it");
                Assert.That(theirs.Played,Is.EqualTo(1),"beyond 15 m the friend hears nothing");

                friend.Leave();yield return null;yield return null;
                Assert.That(Voices(theirs),Is.Empty,"leaving lets every inhale voice go");
                yield return WearOff(sim,me);Assert.That(Take(sim,me),"setup: a take while the friend is away");
                friend.Join("Friend","127.0.0.1",port);
                for(float deadline=Time.realtimeSinceStartup+30;friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,"setup: the friend joins again: "+friend.Message);
                Stand(sim,friend.LocalPlayerId,-1.2f);yield return Settle(1);
                Assert.That(theirs.Played,Is.EqualTo(1),"a friend who rejoins hears no take from before");
            }
            finally{LeaveCrew();}
            yield return null;
        }

        // GAS-6: an inhale goes through the effects volume and follows its slider while it plays; the master volume stays with
        // FestivalTrip's listener. Leaving stops it and lets its voices go, and a destroyed session lets its clip go.
        [UnityTest]public IEnumerator TheInhaleFollowsTheEffectsVolumeAndLetsGo()
        {
            yield return HostAndFriend("Inhale volume");
            try
            {
                var sim=Simulation(host);string me=host.LocalPlayerId,pal=friend.LocalPlayerId;
                var mine=hostObject.GetComponent<FestivalInhale>();var theirs=friendObject.GetComponent<FestivalInhale>();
                Stand(sim,me,1.2f);Stand(sim,pal,-1.2f);yield return Settle(.8f);
                host.Profile.Data.EffectsVolume=.5f;friend.Profile.Data.EffectsVolume=.5f;
                Assert.That(Take(sim,me),"setup: a take");
                yield return Until(()=>theirs.Played>0,3);
                var own=Voice(mine,me);var heard=Voice(theirs,me);
                Assert.That(own!=null&&own.isPlaying&&heard!=null&&heard.isPlaying,"both inhales play");
                Assert.That((own.volume,heard.volume),Is.EqualTo((.5f,.5f)),"at each player's effects volume");
                host.Profile.Data.EffectsVolume=.2f;friend.Profile.Data.EffectsVolume=.9f;friend.Profile.Data.MasterVolume=.5f;
                yield return null;
                Assert.That(own.isPlaying&&heard.isPlaying,"setup: still playing");
                Assert.That(own.volume,Is.EqualTo(.2f).Within(1e-5f),"the host's slider moves their inhale as it plays");
                Assert.That(heard.volume,Is.EqualTo(.9f).Within(1e-5f),"the friend's moves theirs, with no second master volume");

                Assert.That(Take(sim,me),"setup: a second take");
                yield return Until(()=>theirs.Played>1,3);
                Assert.That(heard.isPlaying,"setup: mid breath");
                var clip=heard.clip;
                friend.Leave();yield return null;yield return null;
                Assert.That(Voices(theirs),Is.Empty,"leaving stops the inhale and lets its voices go");
                Object.Destroy(friendObject);friendObject=null;yield return null;
                Assert.That(clip==null,"a destroyed session lets the inhale's clip go");
            }
            finally{LeaveCrew();}
            yield return null;
        }

        private GameObject crewWorld,hostObject,friendObject;
        private FestivalSession host,friend;
        private ushort port;
        // A hosted session and a friend joined to it, both at camp.
        private IEnumerator HostAndFriend(string name)
        {
            crewWorld=new GameObject(name+" world");crewWorld.AddComponent<FestivalWorld>();yield return null;
            hostObject=new GameObject(name+" host");host=hostObject.AddComponent<FestivalSession>();
            friendObject=new GameObject(name+" friend");friend=friendObject.AddComponent<FestivalSession>();
            yield return null;
            port=FreePort();host.Host("Tester",port);
            float deadline=Time.realtimeSinceStartup+30;
            while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.LocalPlayer,Is.Not.Null,"setup: host has a local player: "+host.Message);
            friend.Join("Friend","127.0.0.1",port);
            while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(friend.LocalPlayer,Is.Not.Null,"setup: the friend joins: "+friend.Message);
        }
        private void LeaveCrew()
        {
            if(friend!=null)friend.Leave();if(host!=null)host.Leave();
            foreach(var go in new[]{friendObject,hostObject,crewWorld})if(go!=null)Object.Destroy(go);
        }
        // Puts a player x metres east of the Giggle Balloons.
        private static void Stand(FestivalSimulation sim,string id,float x){var p=sim.Player(id);p.X=CampFeatures.GiggleBalloonX+x;p.Z=CampFeatures.GiggleBalloonZ;}
        private static IEnumerator Settle(float seconds){for(float until=Time.realtimeSinceStartup+seconds;Time.realtimeSinceStartup<until;)yield return null;}
        private static IEnumerator Until(System.Func<bool> done,float seconds){for(float until=Time.realtimeSinceStartup+seconds;!done()&&Time.realtimeSinceStartup<until;)yield return null;}
        private static bool Take(FestivalSimulation sim,string id)=>sim.Execute(id,new GameCommand{Id=System.Guid.NewGuid().ToString("N"),Kind=FestivalSimulation.TakeGiggleBalloonKind}).Accepted;
        private static IEnumerator WearOff(FestivalSimulation sim,string id)
        {
            foreach(var e in sim.Player(id).Effects)if(e.Id==FestivalSimulation.GiggleGasEffect)e.RemainingSeconds=.01;
            yield return Until(()=>!sim.Player(id).Effects.Exists(e=>e.Id==FestivalSimulation.GiggleGasEffect),3);
            Assert.That(sim.Player(id).Effects.Exists(e=>e.Id==FestivalSimulation.GiggleGasEffect),Is.False,"setup: the host's gas has worn off");
        }
        private static AudioSource Voice(FestivalInhale inhale,string playerId){var voice=inhale.transform.Find(FestivalInhale.VoiceName+" "+playerId);return voice!=null?voice.GetComponent<AudioSource>():null;}
        private static List<AudioSource> Voices(FestivalInhale inhale)
        {
            var voices=new List<AudioSource>();
            foreach(Transform child in inhale.transform)if(child.name.StartsWith(FestivalInhale.VoiceName,System.StringComparison.Ordinal))voices.Add(child.GetComponent<AudioSource>());
            return voices;
        }
    }
}
