using System.Collections;
using System.Linq;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Festival.Tests
{
    // POI-1 on a real hosted festival: hold left click with poi in hand and you spin them, in your own view and in what the host
    // sends your friends; let go and they stop. With the menu open the click spins nothing, and Q still starts the performance,
    // which takes over from the spin. The click and Q come from a virtual mouse and keyboard, through the session's own input.
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

                // Held: the host knows you spin, your friend's view shows it, and your own hands spin the pair.
                Hold(true);yield return new WaitForSeconds(.4f);
                Assert.That(player.VisualPoiSpin,Is.True,"holding left click spins the poi in hand");
                Assert.That(session.LocalPlayer.VisualPoiSpin,Is.True,"your own view shows you spinning");
                Assert.That(FestivalSession.ViewFor(sim,friend.Id).Players.Find(p=>p.Id==player.Id).VisualPoiSpin,Is.True,"your friend sees you spin");
                Assert.That(Spinning(),Is.True,"your first-person hands spin both poi");

                // Let go: it stops within 0.2 s, everywhere.
                Hold(false);float released=Time.realtimeSinceStartup;
                while(player.VisualPoiSpin&&Time.realtimeSinceStartup-released<1)yield return null;
                float stopped=Time.realtimeSinceStartup-released;
                Assert.That(stopped,Is.LessThanOrEqualTo(.2f),"letting go stops the spin");
                yield return null;
                Assert.That(FestivalSession.ViewFor(sim,friend.Id).Players.Find(p=>p.Id==player.Id).VisualPoiSpin,Is.False,"your friend sees you stop");
                Assert.That(Spinning(),Is.False,"your hands stop spinning");

                // The menu is open: holding spins nothing.
                session.MenuOpen=true;yield return null;
                Hold(true);yield return new WaitForSeconds(.4f);
                Assert.That(player.VisualPoiSpin,Is.False,"with the menu open, a held click spins nothing");
                Assert.That(Spinning(),Is.False,"with the menu open, your hands do not spin");
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
