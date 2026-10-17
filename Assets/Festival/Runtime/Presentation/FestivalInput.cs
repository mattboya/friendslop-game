using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Festival.Core;
namespace Festival.Presentation
{
    public sealed class FestivalInput : IDisposable
    {
        // DIALOG-3: the kinds always played on the four lanes. A sale, a buy, a chat and a talk with security are talks
        // (FestivalSimulation.Talks), on the chat card; they reach the lanes only for a trust dance (IsRhythm).
        public static bool IsRhythmKind(string kind) => kind=="Dance"||kind=="Poi"||kind=="Dj"||kind=="ConfirmDance"||kind==FestivalSimulation.DrumPartKind;
        /// <summary>DIALOG-3: whether i plays on the four lanes now: a dance, the poi circle, the deck, a check dance, a Drum Circle
        /// part, or the dance a wary festivalgoer asks for before a talk (FestivalSimulation.TrustDancing).</summary>
        public static bool IsRhythm(InteractionState i) => i!=null&&(IsRhythmKind(i.Kind)||FestivalSimulation.TrustDancing(i));
        /// <summary>DIALOG-3: whether doing this kind holds you still (FestivalSession freezes your move): anything on the lanes, and a
        /// talk, face to face until it ends or you walk out of it with Cancel.</summary>
        public static bool Roots(string kind) => IsRhythmKind(kind)||FestivalSimulation.Talks(kind);
        public readonly InputActionMap Map;
        public InputAction Move, Look, Sprint, Interact, Chat, Use, Drop, Objectives, Menu, Slot1, Slot2, Slot3, View, Cycle, Spin, PushToTalk;
        public readonly InputAction[] Notes = new InputAction[4];
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private readonly string saveKey;
        private readonly LocalProfile profile;
        private string appliedJson;
        private bool mapWasEnabled;
        private readonly bool[] noteHeld=new bool[4],noteReleased=new bool[4];
        public string LastError {get;private set;}="";
        public string BindingNotice {get;private set;}="";
        [Serializable] private sealed class LegacyOverrides { public List<LegacyBinding> bindings=null; }
        [Serializable] private sealed class LegacyBinding { public string action="",id="",path=null,interactions=null,processors=null; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Disposable test/development runs can isolate saved controls independently of this machine's normal bindings.
        public static string PreferencesScope {get;set;}
#endif
        public bool Rebinding => rebind != null;
        // Cancellation survives a disable/re-enable between HUD frames. The physical latch prevents a held key becoming a new press.
        public int DropCancellationSequence {get;private set;}
        public bool DropPhysicallyPressed
        {
            get{foreach(var control in Drop.controls)if(control is UnityEngine.InputSystem.Controls.ButtonControl button&&button.isPressed)return true;return false;}
        }
        public FestivalInput(string profile,LocalProfile savedProfile=null)
        {
            this.profile=savedProfile??new LocalProfile(profile);
            saveKey="festival-bindings-"+profile;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!string.IsNullOrEmpty(PreferencesScope))saveKey="festival-bindings-"+PreferencesScope+"-"+profile;
#endif
            Map=new InputActionMap("Festival");
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
            PushToTalk=Button("PushToTalk","<Keyboard>/t");
            string[] arrows={"leftArrow","downArrow","upArrow","rightArrow"};
            string[] wasd={"a","s","w","d"};
            for(int i=0;i<4;i++)
            {
                Notes[i]=Button("Rhythm"+i,"<Keyboard>/"+arrows[i]);
                Notes[i].AddBinding("<Keyboard>/"+wasd[i]);
            }
            // Identities are authored from names, independent of process, map order or override paths.
            // Keep these semantic names stable when adding actions; official Input System JSON remains the only save format.
            string json=Map.ToJson();
            json=json.Replace(Map.id.ToString(),Identity("map/Festival"));
            foreach(var action in Map.actions)
            {
                json=json.Replace(action.id.ToString(),Identity("action/"+action.name));
                for(int index=0;index<action.bindings.Count;index++)
                {
                    var binding=action.bindings[index];
                    string slot=binding.isComposite?"composite":binding.isPartOfComposite?binding.name:action.name.StartsWith("Rhythm",StringComparison.Ordinal)?index==0?"arrow":"alternate":"primary";
                    json=json.Replace(binding.id.ToString(),Identity("binding/"+action.name+"/"+slot));
                }
            }
            Map.Dispose();Map=InputActionMap.FromJson(json)[0];
            Move=Map.FindAction("Move");Look=Map.FindAction("Look");Sprint=Map.FindAction("Sprint");Interact=Map.FindAction("Interact");Chat=Map.FindAction("Chat");
            Use=Map.FindAction("Use");Drop=Map.FindAction("Drop");Objectives=Map.FindAction("Objectives");Menu=Map.FindAction("Menu");
            Slot1=Map.FindAction("Slot1");Slot2=Map.FindAction("Slot2");Slot3=Map.FindAction("Slot3");View=Map.FindAction("View");Cycle=Map.FindAction("Cycle");Spin=Map.FindAction("Spin");PushToTalk=Map.FindAction("PushToTalk");
            for(int lane=0;lane<4;lane++)
            {
                int direction=lane;Notes[lane]=Map.FindAction("Rhythm"+lane);
                Notes[lane].performed+=context=>noteHeld[direction]=context.ReadValueAsButton();
                Notes[lane].canceled+=context=>{if(noteHeld[direction])noteReleased[direction]=true;noteHeld[direction]=false;};
            }
            Drop.canceled+=context=>DropCancellationSequence++;
            if(string.IsNullOrEmpty(this.profile.Data.BindingOverridesJson))ImportLegacy();
            SynchronizeBindings();
            Map.Enable();
        }
        private static string Identity(string name)
        {
            using(var hash=SHA256.Create())
            {
                var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes("FestivalControls/"+name));var id=new byte[16];Array.Copy(bytes,id,id.Length);
                return new Guid(id).ToString();
            }
        }
        private InputAction Button(string name,string binding) => Map.AddAction(name,InputActionType.Button,binding);
        public string Label(InputAction action) => action==Look&&action.bindings[0].effectivePath=="<Mouse>/delta"?"Mouse":action.GetBindingDisplayString();
        // A rhythm lane's WASD key as bound now: binding 1 (binding 0 is its arrow), overrides included.
        public string NoteKey(int lane) => Notes[lane].GetBindingDisplayString(1);
        // A changed profile (including RESET TAB) reaches this same live map without losing its authored identity.
        public void SynchronizeBindings()
        {
            string json=profile.Data.BindingOverridesJson;
            if(appliedJson==json)return;
            DropCancellationSequence++;
            Map.RemoveAllBindingOverrides();
            if(!string.IsNullOrEmpty(json))Map.LoadBindingOverridesFromJson(json);
            appliedJson=json;
        }
        private void ImportLegacy()
        {
            if(profile.ReadOnly||!PlayerPrefs.HasKey(saveKey))return;
            string json=PlayerPrefs.GetString(saveKey);
            var validated=new ProfileData{BindingOverridesJson=json};FestivalSettings.Validate(validated);
            if(string.IsNullOrEmpty(json)||validated.BindingOverridesJson!=json){BindingNotice="Old controls could not be imported. Rebind them here.";return;}
            var saved=JsonUtility.FromJson<LegacyOverrides>(json);int imported=0,skipped=0;
            foreach(var record in saved.bindings)
            {
                string name=record.action.StartsWith("Festival/",StringComparison.Ordinal)?record.action.Substring(9):record.action;
                var action=Map.FindAction(name);
                int count=saved.bindings.FindAll(binding=>binding.action==record.action).Count;
                // Old random GUIDs cannot identify a composite part or one of two rhythm alternatives. Never guess.
                if(action==null||action.bindings.Count!=1||action.bindings[0].isComposite||count!=1){skipped++;continue;}
                action.ApplyBindingOverride(0,new InputBinding{overridePath=record.path=="null"?null:record.path,
                    overrideInteractions=record.interactions=="null"?null:record.interactions,overrideProcessors=record.processors=="null"?null:record.processors});imported++;
            }
            if(imported>0){profile.Data.BindingOverridesJson=Map.SaveBindingOverridesAsJson();LastError=profile.Save();}
            if(skipped>0)BindingNotice="Some old controls were ambiguous. Rebind movement and rhythm keys here.";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(imported>0||skipped>0)Debug.Log("[Settings] Legacy controls imported="+imported+" ambiguous="+skipped+"; original preferences retained.");
#endif
        }
        // W/A/S/D share movement and rhythm intentionally; every other overlap is visible, rather than silently stolen.
        public string BindingClashes(InputAction action,int bindingIndex,string path=null)
        {
            CheckBinding(action,bindingIndex);
            path=path??action.bindings[bindingIndex].effectivePath;
            if(string.IsNullOrEmpty(path))return "";
            var clashes=new List<string>();
            foreach(var other in Map.actions)
            {
                bool shared=action==Move&&Array.IndexOf(Notes,other)>=0||other==Move&&Array.IndexOf(Notes,action)>=0;
                if(shared)continue;
                for(int index=0;index<other.bindings.Count;index++)
                {
                    if(other==action&&index==bindingIndex||other.bindings[index].isComposite)continue;
                    string existing=other.bindings[index].effectivePath;
                    if(!string.IsNullOrEmpty(existing)&&(string.Equals(path,existing,StringComparison.OrdinalIgnoreCase)
                        ||path.StartsWith(existing+"/",StringComparison.OrdinalIgnoreCase)||existing.StartsWith(path+"/",StringComparison.OrdinalIgnoreCase)))
                    {if(!clashes.Contains(other.name))clashes.Add(other.name);}
                }
            }
            return clashes.Count==0?"":"Already used by "+string.Join(", ",clashes)+".";
        }
        private void CheckBinding(InputAction action,int bindingIndex)
        {
            if(action==null||action.actionMap!=Map)throw new ArgumentException("This action belongs to another input map.",nameof(action));
            if(bindingIndex<0||bindingIndex>=action.bindings.Count||action.bindings[bindingIndex].isComposite)throw new ArgumentOutOfRangeException(nameof(bindingIndex),"Choose a single binding or movement part.");
        }
        public void Rebind(InputAction action,int bindingIndex,Action done)
        {
            if(rebind!=null)return;
            CheckBinding(action,bindingIndex);
            if(profile.ReadOnly){LastError=profile.LastError;done?.Invoke();return;}
            mapWasEnabled=Map.enabled;Map.Disable();
            try
            {
                // The stock convenience method excludes mouse delta, which would make LOOK impossible to rebind.
                rebind=new InputActionRebindingExtensions.RebindingOperation().WithAction(action).WithTargetBinding(bindingIndex)
                    .WithExpectedControlType(action==Look?"Vector2":action==Cycle?"Axis":"Button").OnMatchWaitForAnother(.05f)
                    .WithControlsExcluding("<Pointer>/position").WithControlsExcluding("<Touchscreen>/touch*/position")
                    .WithControlsExcluding("<Mouse>/clickCount").WithCancelingThrough("<Keyboard>/escape").WithMatchingEventsBeingSuppressed()
                    .OnComplete(op=>Finish(action,bindingIndex,done,true)).OnCancel(op=>Finish(action,bindingIndex,done,false));
                if(action!=Look)rebind.WithControlsExcluding("<Pointer>/delta");
                rebind.Start();
            }
            catch{rebind?.Dispose();rebind=null;if(mapWasEnabled)Map.Enable();throw;}
        }
        private void Finish(InputAction action,int index,Action done,bool completed)
        {
            rebind.Dispose();rebind=null;
            if(completed)
            {
                profile.Data.BindingOverridesJson=Map.SaveBindingOverridesAsJson();appliedJson=profile.Data.BindingOverridesJson;
                LastError=profile.Save();BindingNotice=BindingClashes(action,index);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Settings] Binding updated; saved="+string.IsNullOrEmpty(LastError)+" clash="+!string.IsNullOrEmpty(BindingNotice)+".");
#endif
            }
            if(mapWasEnabled)Map.Enable();done?.Invoke();
        }
        // Retain effective action cancellations across map disable/rebinding until the HUD consumes them.
        public bool TakeNoteRelease(int lane){bool released=noteReleased[lane];noteReleased[lane]=false;return released;}
        public void CancelHeldNotes()
        {
            for(int lane=0;lane<4;lane++)if(noteHeld[lane]){noteReleased[lane]=true;noteHeld[lane]=false;}
        }
        public void CancelRebind(){rebind?.Cancel();}
        public void Dispose(){rebind?.Dispose();rebind=null;Map.Dispose();}
    }
}
