using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Festival.Core;
namespace Festival.Presentation
{
    public sealed class FestivalInput : IDisposable
    {
        // DIALOG-3: the kinds always played on the four lanes. A sale, a buy, a chat and a talk with security are talks
        // (FestivalSimulation.Talks), on the chat card; they reach the lanes only for a trust dance (IsRhythm).
        public static bool IsRhythmKind(string kind) => kind=="Dance"||kind=="Poi"||kind=="Dj"||kind=="ConfirmDance";
        /// <summary>DIALOG-3: whether i plays on the four lanes now: a dance, the poi circle, the deck, a check dance, or the dance a
        /// wary festivalgoer asks for before a talk (FestivalSimulation.TrustDancing).</summary>
        public static bool IsRhythm(InteractionState i) => i!=null&&(IsRhythmKind(i.Kind)||FestivalSimulation.TrustDancing(i));
        /// <summary>DIALOG-3: whether doing this kind holds you still (FestivalSession freezes your move): anything on the lanes, and a
        /// talk, face to face until it ends or you walk out of it with Cancel.</summary>
        public static bool Roots(string kind) => IsRhythmKind(kind)||FestivalSimulation.Talks(kind);
        public readonly InputActionMap Map = new InputActionMap("Festival");
        public InputAction Move, Look, Sprint, Interact, Chat, Use, Drop, Objectives, Menu, Slot1, Slot2, Slot3, View, Cycle, Spin;
        public readonly InputAction[] Notes = new InputAction[4];
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private readonly string saveKey;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Disposable test/development runs can isolate saved controls independently of this machine's normal bindings.
        public static string PreferencesScope {get;set;}
#endif
        public bool Rebinding => rebind != null;
        public FestivalInput(string profile)
        {
            saveKey="festival-bindings-"+profile;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!string.IsNullOrEmpty(PreferencesScope))saveKey="festival-bindings-"+PreferencesScope+"-"+profile;
#endif
            Move=Map.AddAction("Move",InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            Look=Map.AddAction("Look",InputActionType.Value,"<Mouse>/delta");
            Sprint=Button("Sprint","<Keyboard>/leftShift"); Interact=Button("Interact","<Keyboard>/e"); Chat=Button("Chat","<Keyboard>/f");
            Use=Button("Use","<Keyboard>/q"); Drop=Button("Drop","<Keyboard>/g");
            Objectives=Button("Objectives","<Keyboard>/tab"); Menu=Button("Menu","<Keyboard>/escape");
            Slot1=Button("Slot1","<Keyboard>/1");Slot2=Button("Slot2","<Keyboard>/2");Slot3=Button("Slot3","<Keyboard>/3");
            // CAM-1: V flips between first person and over the shoulder.
            View=Button("View","<Keyboard>/v");
            // HUD-8: each notch of the mouse wheel steps through the hand slots, down to the next (FestivalHudGear.Notch).
            Cycle=Map.AddAction("Cycle",InputActionType.Value,"<Mouse>/scroll/y");
            // POI-1: hold left click with poi in hand to spin them (FestivalSession reads it while the pointer is locked).
            Spin=Button("Spin","<Mouse>/leftButton");
            string[] arrows={"leftArrow","downArrow","upArrow","rightArrow"};
            string[] wasd={"a","s","w","d"};
            for(int i=0;i<4;i++)
            {
                Notes[i]=Button("Rhythm"+i,"<Keyboard>/"+arrows[i]);
                Notes[i].AddBinding("<Keyboard>/"+wasd[i]);
            }
            try { if(PlayerPrefs.HasKey(saveKey)) Map.LoadBindingOverridesFromJson(PlayerPrefs.GetString(saveKey)); }
            catch(ArgumentException) { PlayerPrefs.DeleteKey(saveKey); }
            Map.Enable();
        }
        private InputAction Button(string name,string binding) => Map.AddAction(name,InputActionType.Button,binding);
        public string Label(InputAction action) => action.GetBindingDisplayString();
        // A rhythm lane's WASD key as bound now: binding 1 (binding 0 is its arrow), overrides included.
        public string NoteKey(int lane) => Notes[lane].GetBindingDisplayString(1);
        public void Rebind(InputAction action, int bindingIndex, Action done)
        {
            if(rebind!=null)return;
            Map.Disable();
            rebind=action.PerformInteractiveRebinding(bindingIndex).WithControlsExcluding("<Mouse>/position").WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op=>Finish(done)).OnCancel(op=>Finish(done));
            rebind.Start();
        }
        private void Finish(Action done)
        {
            rebind.Dispose(); rebind=null;
            PlayerPrefs.SetString(saveKey,Map.SaveBindingOverridesAsJson());PlayerPrefs.Save();Map.Enable();done?.Invoke();
        }
        public void Dispose() { rebind?.Dispose();rebind=null;Map.Dispose(); }
    }
}
