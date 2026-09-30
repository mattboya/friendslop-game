# First package facial-expression diagnosis

September 29, 2026. Read-only investigation of `AH01_Attendee.fbx`, `AH01_Vendor.fbx`, the editable Blender scene, the generator, and the existing Unity native capture. No source asset, generator, Unity fixture, or test suite was changed for this diagnosis.

## Finding

**The Blink and Delight meshes are real and substantial. The exported animation takes also contain constant-zero shape-key curves.** Those curves are the strongest explanation for why the native `expressions-100.png` capture looked near neutral: the review harness set each `SkinnedMeshRenderer` weight to 100, then yielded a frame while the Animator continued playing a looping take. If Unity imports and evaluates the FBX shape curves, that animation writes the weights back to zero before the captured frame renders.

This supersedes the first test report's interpretation of that screenshot as proof the shape geometry itself was ineffective. Direct Unity evaluation of the weight before/after Animator update was not instrumented in this read-only pass, so the exact Unity overwrite remains a high-confidence diagnosis rather than a measured runtime trace.

## Evidence

- The Blender source scene has `Blink`, `Delight`, and `Concern` keys on both actors at neutral value 0. The generator creates the eye Blink key by shrinking the white eye's local vertical coordinate to 4.5% and the pupil to 4%. It creates a 2.5 cm brow raise and roughly 2.7 cm mouth-corner lift for Delight. See `scripts/create_first_production_package.py`, around lines 133–152.
- A read-only Blender mesh probe found the attendee source eye Blink reduces its vertical span from **13.728 cm to 0.618 cm**. The exported/imported FBX eye still reduces from **13.2 cm to 0.594 cm**, with **6.303 cm maximum vertex displacement**. The attendee mouth Delight has **2.967 cm maximum vertex displacement**. The vendor has the same eye span change and a **2.964 cm** mouth Delight maximum. The FBXs did not lose or flatten the shapes.
- Reimporting both FBXs into a fresh Blender process exposed animation actions for their shape-key data. On the attendee, every `Key...|Idle` and `Key...|Dance` action has 60 sampled keyframes for Blink, Delight, Concern, or Grip channels, **all with value 0**. The vendor's `Key...|Welcome` and `Key...|Offer` actions show the same constant-zero channels. The authored generator keys rig bone rotations; it does not key facial shape values. Its export path sets all shape values to zero and bakes animation/NLA, which explains why the exporter sampled neutral shape values into the takes.
- The Unity import audit lists the Blink/Delight/Concern channels and neutral default weights of 0. The existing `ProductionSampleCapture.cs` sets Blink and Delight to 100, yields one frame, then captures. The captured eyes remain open. This behavior is consistent with the zero-curve overwrite, but the screenshot alone cannot distinguish an Animator overwrite from another runtime binding issue.

## Recommended repair and verification

1. Inspect the imported Unity clips with `AnimationUtility.GetCurveBindings` and look for `blendShape.Blink`, `blendShape.Delight`, `blendShape.Concern`, and grip bindings with constant zero values. Record weights before and after Animator evaluation in a development-only probe, or compare a native frame with the Animator disabled. That closes the remaining causal gap.
2. Keep expression controls separate from body acting clips. A reliable Unity approach is to copy the imported clips into editable `.anim` assets, remove only unintended constant-zero blendshape curves, and have the sample controller use those copies. Alternatively, explicitly author facial keys if expressions should be part of each take. Avoid stripping meaningful grip/body curves wholesale.
3. Recheck the `GripL`/`GripR` channels too: the exported takes contain zero curves for them, so a gameplay grip weight applied while an Animator is active may face the same overwrite.
4. After the curves are addressed, capture neutral, Blink 100, Delight 100, and Concern 100 from the same native camera and lighting. Confirm the live weights and compare face crops at gameplay distance. The eye catchlights are separate meshes without a Blink key, so inspect whether they remain visible or float when the eye closes. Delight's 2–3 cm movement may still need stronger art direction for normal gameplay distance, but the current neutral-looking screenshot does not establish that.

The probe used a temporary script outside the project to open the existing `.blend` and FBXs in Blender 5.2.2. It did not save or re-export any asset. No broad Unity suite was rerun.
