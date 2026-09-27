using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Festival.Presentation
{
    public sealed class FestivalInput : IDisposable
    {
        public static bool IsRhythmKind(string kind) => kind=="Dance"||kind=="Conversation"||kind=="Sale"||kind=="Police"||kind=="Poi"||kind=="Dj";
        public readonly InputActionMap Map = new InputActionMap("Festival");
        public InputAction Move, Look, Sprint, Interact, Chat, Use, Drop, Objectives, Menu, Slot1, Slot2, Slot3;
        public readonly InputAction[] Notes = new InputAction[4];
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private readonly string saveKey;
        public bool Rebinding => rebind != null;
        public FestivalInput(string profile)
        {
            saveKey="festival-bindings-"+profile;
            Move=Map.AddAction("Move",InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            Look=Map.AddAction("Look",InputActionType.Value,"<Mouse>/delta");
            Sprint=Button("Sprint","<Keyboard>/leftShift"); Interact=Button("Interact","<Keyboard>/e"); Chat=Button("Chat","<Keyboard>/f");
            Use=Button("Use","<Keyboard>/q"); Drop=Button("Drop","<Keyboard>/g");
            Objectives=Button("Objectives","<Keyboard>/tab"); Menu=Button("Menu","<Keyboard>/escape");
            Slot1=Button("Slot1","<Keyboard>/1");Slot2=Button("Slot2","<Keyboard>/2");Slot3=Button("Slot3","<Keyboard>/3");
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
