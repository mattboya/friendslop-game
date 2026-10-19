using System.Collections;
using System.Linq;
using System.Reflection;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // POI-1 on a real hosted festival: hold left click with poi in hand and you spin them, in your own view and in what the host
    // sends your friends; let go and they stop. With the menu open the click spins nothing, and Q still starts the performance,
    // which takes over from the spin. The click and Q come from a virtual mouse and keyboard, through the session's own input.
    // The bodies show it too: your own from the click, a friend's from the host's view, even while that friend is dosed (the
    // tripper is dosed all level).
    public sealed class PoiSpinPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator HoldingLeftClickSpinsYourPoiForEveryone()
        {
            var input=InputSystem.settings;oldBackground=input.backgroundBehavior;oldEditor=input.editorInputBehaviorInPlayMode;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse=InputSystem.AddDevice<Mouse>("Poi spin mouse");keyboard=InputSystem.AddDevice<Keyboard>("Poi spin keyboard");
            var world=new GameObject("Poi spin world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Poi spin session");var session=host.AddComponent<FestivalSession>();host.AddComponent<FestivalHud>();
            yield return null;
            void Hold(bool down)=>InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,down));
            try
            {
                session.Host("Tester",HostPort);
                float deadline=Time.realtimeSinceStartup+30;
                while(session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,"host has a local player: "+session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);
                var friend=sim.AddPlayer("poi_spin_friend","Sam");friend.Ready=true;
                yield return StartLevel(session);
                sim.State.Npcs.Clear();player.Effects.Clear();
                player.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});session.Command("Equip",item:"poi_practice");
                yield return new WaitForSeconds(.3f);
                Assert.That(player.EquippedItemId,Is.EqualTo("poi_practice"),"setup: the poi are in hand: "+session.Message);
                bool Spinning()=>session.ViewCamera.GetComponentsInChildren<FestivalPoiRig>().Count(r=>r.Spinning)==2;
                // Your friend has poi in hand and is dosed, as the tripper is all level.
                friend.EquippedItemId="poi_led";friend.Effects.Clear();friend.X=player.X+2;friend.Z=player.Z;
                friend.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,InstanceId="poi_spin_dose",Intensity=1,RemainingSeconds=999});
                yield return new WaitForSeconds(.3f);
                var mine=session.WorldCharacter(player.Id);var theirs=session.WorldCharacter(friend.Id);
                Assert.That(theirs.Pose,Is.EqualTo("Intoxicated"),"setup: your friend stands dosed");
                bool BodySpins(FestivalCharacter body)=>body.GetComponentsInChildren<FestivalPoiRig>().Count(r=>r.Spinning&&!r.Carrying)==2;

                // Held: the host knows you spin, your friend's view shows it, and your own hands spin the pair.
                Hold(true);friend.VisualPoiSpin=true;yield return new WaitForSeconds(.4f);
                Assert.That(player.VisualPoiSpin,Is.True,"holding left click spins the poi in hand");
                Assert.That(session.LocalPlayer.VisualPoiSpin,Is.True,"your own view shows you spinning");
                Assert.That(FestivalSession.ViewFor(sim,friend.Id).Players.Find(p=>p.Id==player.Id).VisualPoiSpin,Is.True,"your friend sees you spin");
                Assert.That(Spinning(),Is.True,"your first-person hands spin both poi");
                Assert.That(mine.SpinningPoi&&BodySpins(mine),Is.True,"your own body spins its poi, as third person shows it");
                Assert.That(theirs.SpinningPoi&&BodySpins(theirs),Is.True,"your dosed friend's body spins their poi for you");

                // Let go: it stops within 0.2 s, everywhere.
                Hold(false);friend.VisualPoiSpin=false;float released=Time.realtimeSinceStartup;
                while(player.VisualPoiSpin&&Time.realtimeSinceStartup-released<1)yield return null;
                float stopped=Time.realtimeSinceStartup-released;
                Assert.That(stopped,Is.LessThanOrEqualTo(.2f),"letting go stops the spin");
                yield return null;
                Assert.That(FestivalSession.ViewFor(sim,friend.Id).Players.Find(p=>p.Id==player.Id).VisualPoiSpin,Is.False,"your friend sees you stop");
                Assert.That(Spinning(),Is.False,"your hands stop spinning");
                yield return new WaitForSeconds(.3f);
                Assert.That(mine.SpinningPoi||BodySpins(mine),Is.False,"your body stops spinning");
                Assert.That(theirs.SpinningPoi||BodySpins(theirs),Is.False,"your friend's body stops spinning");

                // The menu is open: holding spins nothing.
                session.MenuOpen=true;yield return null;
                Hold(true);yield return new WaitForSeconds(.4f);
                Assert.That(player.VisualPoiSpin,Is.False,"with the menu open, a held click spins nothing");
                Assert.That(Spinning(),Is.False,"with the menu open, your hands do not spin");
                Assert.That(mine.SpinningPoi,Is.False,"with the menu open, your body does not spin");
                Hold(false);yield return null;session.MenuOpen=false;yield return new WaitForSeconds(.2f);

                // Q while holding starts the performance, which takes over from the spin.
                Hold(true);yield return new WaitForSeconds(.4f);
                Assert.That(player.VisualPoiSpin,Is.True,"setup: spinning again");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.3f);
                var poi=sim.Interaction(player.InteractionId);
                Assert.That(poi?.Kind,Is.EqualTo("Poi"),"Q still starts the poi performance: "+session.Message);
                Assert.That(player.VisualPoiSpin,Is.False,"the performance takes over from the spin");
                Debug.Log($"[Festival.Test] poi spin stopped {stopped:F3} s after release");
                Hold(false);session.Command("Cancel");yield return null;
            }
            finally{Restore();session.Leave();Object.Destroy(host);Object.Destroy(world);}
        }

        // The virtual devices and the input settings go back however the test ended (an error log skips its own finally).
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        void Restore()
        {
            if(mouse==null)return;
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);mouse=null;keyboard=null;
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
        }
        [UnityTearDown]public IEnumerator RestoreTheDevices(){Restore();yield return null;}
    }
}

namespace Festival.Tests
{
    public sealed class PoiPurchaseHudPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator PayingForPoiKeepsTheSpinHintInTheDrawnHud()
        {
            return Purchase("poi_practice","PRACTICE POI","Practice poi bought for $5. "+FestivalSimulation.SpinWords,true);
        }

        [UnityTest]public IEnumerator PayingForAStarterKeepsItsPocketToast()
        {
            return Purchase("stock_lsd","TONGUE STAMPS","Oh, hey, thanks!  •  POCKET UPDATED",false);
        }

        private IEnumerator Purchase(string item,string title,string receipt,bool spin)
        {
            UseAVirtualKeyboard("fix:U15#1-keyboard");UseAVirtualMouse("fix:U15#1-mouse");
            var worldObject=new GameObject("fix:U15#1-world");worldObject.AddComponent<FestivalWorld>();yield return null;
            var hudObject=new GameObject("fix:U15#1-HUD");var session=hudObject.AddComponent<FestivalSession>();hudObject.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("fix:U15#1-host",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var player=sim.Player(session.LocalPlayerId);player.Cash=20;
                string Read(string name)
                {
                    foreach(var text in hudObject.GetComponentsInChildren<Text>(false))if(text.name==name)return text.text;
                    return "(hidden)";
                }
                // Only reach and camera are fixtures. E travels through the real input, HUD, session and payment rules.
                int slot=sim.State.VendorOffers.IndexOf(item);Assert.That(slot,Is.GreaterThanOrEqualTo(0));
                var point=Catalog.ShopPoint(true,slot);player.X=point.X;player.Z=6.3f;
                typeof(FestivalSession).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,0f);
                typeof(FestivalSession).GetField("pitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,-Mathf.Atan2((slot<4?1.17f:2.05f)-1.65f,9f-6.3f)*Mathf.Rad2Deg);
                yield return new WaitForSeconds(.4f);
                Assert.That(Read("Prompt"),Does.StartWith("PICK UP "+title));
                yield return Tap(Key.E,.25f);
                Assert.That(player.HeldOfferId,Is.EqualTo(item));
                if(spin)Assert.That(Read("Notice"),Does.EndWith(FestivalSimulation.SpinWords));
                player.X=0;player.Z=7;yield return new WaitForSeconds(.35f);
                Assert.That(Read("Prompt"),Does.StartWith("SHOW "+title+" TO SELLER"));
                yield return Tap(Key.E,.25f);Assert.That(Read("Prompt"),Does.StartWith("PAY $5"));
                yield return Tap(Key.E,.35f);
                Assert.That(player.Cash,Is.EqualTo(15));
                Assert.That(player.EquippedItemId,Is.EqualTo(item));
                Assert.That(player.Inventory.Find(x=>x.ItemId==item)?.Count,Is.EqualTo(1));
                Assert.That(Read("Notice"),Is.EqualTo(receipt),"the running HUD must show the item's intended purchase notice");
                if(spin)
                {
                    InputSystem.QueueStateEvent(TestMouse,new MouseState().WithButton(MouseButton.Left,true));yield return new WaitForSeconds(.35f);
                    Assert.That(player.VisualPoiSpin,Is.True,"following the purchase hint spins the equipped poi through real mouse input");
                    Assert.That(Read("Notice"),Is.EqualTo(receipt),"later HUD updates keep the purchase hint visible");
                    InputSystem.QueueStateEvent(TestMouse,new MouseState());yield return null;
                }
            }
            finally{session.Leave();Object.Destroy(hudObject);Object.Destroy(worldObject);}
        }
    }
}

namespace Festival.Tests
{
    public sealed class MinigamePoiIntentPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator APreheldSpinIgnoresReleaseAndStaleIntentsDuringThePause(){return FrozenIntent(true);}
        [UnityTest]public IEnumerator ANewSpinIntentIsIgnoredDuringTheGameAndBanner(){return FrozenIntent(false);}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [UnityTest]public IEnumerator DebugMovementSamplingExplainsIgnoredPoiIntent(){return FrozenIntent(false,true);}
#endif

        // Send a move intent through the actual host's acceptance and movement consumers, with its client-owned sequence.
        private IEnumerator FrozenIntent(bool before,bool diagnostics=false)
        {
            var world=new GameObject("impl:U23 poi world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("impl:U23 poi host");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var friend=sim.AddPlayer("impl:U23-poi-friend","Sam");friend.Ready=true;
                yield return StartLevel(session);session.enabled=false;sim.State.Npcs.Clear();
                var me=sim.Player(session.LocalPlayerId);me.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
                Assert.That(sim.Execute(me.Id,new GameCommand{Id="impl:U23-equip",Kind="Equip",ItemId="poi_practice"}).Accepted,Is.True);
                var inputs=(System.Collections.IDictionary)typeof(FestivalSession).GetField("inputs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                var peer=inputs[me.Id];var intentType=typeof(FestivalSession).Assembly.GetType("Festival.Network.MoveIntent");
                int sequence=(int)intentType.GetField("Sequence").GetValue(peer.GetType().GetField("Intent").GetValue(peer));
                void Move()=>typeof(FestivalSession).GetMethod("ApplyMovement",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new object[]{.1});
                void Intent(bool held,float z=0)
                {
                    var type=typeof(FestivalSession).Assembly.GetType("Festival.Network.MoveIntent");var intent=System.Activator.CreateInstance(type);
                    type.GetField("SpinPoi").SetValue(intent,held);type.GetField("Z").SetValue(intent,z);type.GetField("Sequence").SetValue(intent,++sequence);
                    typeof(FestivalSession).GetMethod("AcceptInput",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new[]{(object)me.Id,intent});
                    Assert.That(type.GetField("Sequence").GetValue(peer.GetType().GetField("Intent").GetValue(peer)),Is.EqualTo(sequence),"setup: the host accepts this fresh input");Move();
                }
                void Frozen(string stage)
                {
                    Assert.That(me.VisualPoiSpin,Is.EqualTo(before),stage+": host intent cannot change the spin held in the freeze-frame");
                    Assert.That(FestivalSession.ViewFor(sim,friend.Id).Players.Find(p=>p.Id==me.Id).VisualPoiSpin,Is.EqualTo(before),stage+": the friend's snapshot preserves the frozen spin");
                }
                Intent(before);Assert.That(me.VisualPoiSpin,Is.EqualTo(before),"setup: the actual host applied the pre-game intent");
                sim.State.Booth.Kind=FestivalSimulation.MysteryCupsGame;var at=FestivalSimulation.BoothAt(sim.State);Assert.That(at,Is.Not.Null);
                foreach(var p in sim.State.Players){p.X=at.X;p.Z=at.Z;}
                sim.Tick(3.1);Assert.That(sim.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"the actual ring countdown starts the game");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(diagnostics)
                {
                    var samples=new System.Collections.Generic.List<string>();bool visible=DevelopmentDiagnostics.Visible;
                    Application.LogCallback capture=(message,trace,type)=>{if(message.StartsWith("[Festival.Movement]"))samples.Add(message);};
                    Application.logMessageReceived+=capture;
                    try
                    {
                        DevelopmentDiagnostics.Visible=false;Intent(true,1);Assert.That(samples,Is.Empty,"debug sampling is opt-in");
                        DevelopmentDiagnostics.Visible=true;Intent(true,1);Assert.That(samples.Count,Is.EqualTo(1));
                        Assert.That(samples[0],Does.Contain("phase=Minigame").And.Contain("spin_requested=True").And.Contain("spin=False").And.Contain("accepted=False"),"the actual host sample explains why the held click cannot change its frozen spin");
                        Intent(true,1);Assert.That(samples.Count,Is.EqualTo(1),"repeated ignored intent does not flood the log");
                    }
                    finally{DevelopmentDiagnostics.Visible=visible;Application.logMessageReceived-=capture;}
                }
#endif
                Intent(!before);Frozen("game");
                peer.GetType().GetField("ReceivedAt").SetValue(peer,Time.realtimeSinceStartupAsDouble-1);Move();Frozen("stale input in game");
                Assert.That(sim.Execute(FestivalSimulation.CupsTurn(sim.State),new GameCommand{Id="impl:U23-call-poi",Kind=FestivalSimulation.MinigameKind,ItemId=FestivalSimulation.CallCups}).Accepted,Is.True);
                Intent(!before);Frozen("result banner");
                sim.Tick(FestivalSimulation.MinigameBannerSeconds+.1);Assert.That(sim.State.Phase,Is.EqualTo("Playing"));
                Intent(true);Assert.That(me.VisualPoiSpin,Is.True,"fresh held intent spins again after play resumes");
                Intent(false);Assert.That(me.VisualPoiSpin,Is.False,"fresh release stops it again after play resumes");
                Intent(true);peer.GetType().GetField("ReceivedAt").SetValue(peer,Time.realtimeSinceStartupAsDouble-1);Move();
                Assert.That(me.VisualPoiSpin,Is.False,"stale input still stops the spin during ordinary play");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }
    }
}

namespace Festival.Tests
{
    // POI-7 through the actual host's intent and movement consumers: right click's side spin reaches the snapshot a friend sees
    // and left click wins when both are held. A spinner's Sprint walks: TryMove refuses a sprint-length step mid-spin, so a
    // host that still moved at Sprint's speed would freeze the spinner instead.
    public sealed class PoiSideSpinIntentPlayTests:FestivalPlayModeTest
    {
        static int sequence=2000000;
        [UnityTest]public IEnumerator TheHostSetsTheSideSpinAndLeftClickWins(){return Hosted(false);}
        [UnityTest]public IEnumerator ASprintingSpinnerWalksAndSprintsAgainOnRelease(){return Hosted(true);}

        private IEnumerator Hosted(bool sprint)
        {
            var world=new GameObject("Side spin world");world.AddComponent<FestivalWorld>();yield return null;
            var host=new GameObject("Side spin host");var session=host.AddComponent<FestivalSession>();yield return null;
            try
            {
                session.Host("Tester",HostPort);
                for(float end=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<end;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);
                var sim=Simulation(session);var friend=sim.AddPlayer("side_spin_friend","Sam");friend.Ready=true;
                yield return StartLevel(session);session.enabled=false;sim.State.Npcs.Clear();
                var me=sim.Player(session.LocalPlayerId);me.Effects.Clear();me.Inventory.Add(new ItemStack{ItemId="poi_practice",Count=1});
                Assert.That(sim.Execute(me.Id,new GameCommand{Id="side_spin_equip",Kind="Equip",ItemId="poi_practice"}).Accepted,Is.True);
                var type=typeof(FestivalSession).Assembly.GetType("Festival.Network.MoveIntent");
                var inputs=(System.Collections.IDictionary)typeof(FestivalSession).GetField("inputs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                void Move()=>typeof(FestivalSession).GetMethod("ApplyMovement",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new object[]{.1});
                void Intent(bool front,bool sides,bool run=false,float z=0)
                {
                    var intent=System.Activator.CreateInstance(type);
                    type.GetField("SpinPoi").SetValue(intent,front);type.GetField("SpinSides").SetValue(intent,sides);type.GetField("Sprint").SetValue(intent,run);
                    type.GetField("Z").SetValue(intent,z);type.GetField("Sequence").SetValue(intent,++sequence);
                    typeof(FestivalSession).GetMethod("AcceptInput",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,new[]{(object)me.Id,intent});
                    var peer=inputs[me.Id];
                    Assert.That(type.GetField("Sequence").GetValue(peer.GetType().GetField("Intent").GetValue(peer)),Is.EqualTo(sequence),"setup: the host accepts this fresh input");Move();
                }
                if(!sprint)
                {
                    PlayerState Seen()=>FestivalSession.ViewFor(sim,friend.Id).Players.Find(p=>p.Id==me.Id);
                    Intent(false,true);
                    Assert.That(me.VisualPoiSpin&&me.VisualPoiSpinSides,Is.True,"right click alone spins the poi at your sides");
                    Assert.That(Seen().VisualPoiSpin&&Seen().VisualPoiSpinSides,Is.True,"your friend sees the side spin");
                    Intent(true,true);
                    Assert.That(me.VisualPoiSpin&&!me.VisualPoiSpinSides,Is.True,"with both held, left click's spin in front wins");
                    Assert.That(Seen().VisualPoiSpinSides,Is.False,"your friend sees the spin in front");
                    Intent(true,false);Assert.That(me.VisualPoiSpin&&!me.VisualPoiSpinSides,Is.True,"left click alone spins them in front");
                    Intent(false,false);Assert.That(me.VisualPoiSpin||me.VisualPoiSpinSides,Is.False,"letting go clears both");
                    Intent(false,true);var peer=inputs[me.Id];peer.GetType().GetField("ReceivedAt").SetValue(peer,Time.realtimeSinceStartupAsDouble-1);Move();
                    Assert.That(me.VisualPoiSpin||me.VisualPoiSpinSides,Is.False,"a silent client stops the side spin");
                    yield break;
                }
                const float X=0,Z=7;
                Assert.That(FestivalSession.Navigate(X,Z,X,Z+1,.6).Z,Is.GreaterThan(Z+.5f),"setup: this walking path is clear of colliders");
                me.X=X;me.Z=Z;Intent(true,false,true,1);
                Assert.That(me.VisualPoiSpin,Is.True,"setup: spinning");
                Assert.That(me.Z-Z,Is.EqualTo(.4f).Within(1e-4f),"a spinner holding Sprint walks 0.4 m in 0.1 s");
                me.X=X;me.Z=Z;Intent(false,false,true,1);
                Assert.That(me.Z-Z,Is.EqualTo(.6f).Within(1e-4f),"let go, and Sprint runs 0.6 m in 0.1 s again");
            }
            finally{session.Leave();Object.Destroy(host);Object.Destroy(world);}
            yield return null;
        }
    }
}
