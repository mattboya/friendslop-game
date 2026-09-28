# Camp shelter and character motion checkpoint — September 28, 2026

This is progress within the [active graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md), not acceptance of the [Dear Passengers quality reference](https://youtu.be/luWunttkCgY). The additional [poi](https://www.youtube.com/shorts/eCaw93VEEQk) and [rave dance](https://www.youtube.com/watch?v=hG0DZpcLNbk) clips informed the motion pass. The performer and choreography in those videos were used as visual reference only; no video frames, animation data, or third-party assets were copied into the game.

## Changes

- Rebuilt the original camp shade source as a raised, tensioned shelter with a central mast, sewn edges, scalloped valance and underside seams. Rebuilt the seller awning with a shaped canvas roof and sewn edge treatment. The two camp shelters now use different fabric colors, hanging bulb fixtures and local warm lights.
- Corrected hand attachment offsets for poi and other held world props. The imported hand bone's scale had moved the poi grips several metres behind the actor despite correct prop size. The offset now uses world-sized metres before the prop is parented to the hand.
- Replaced the short sideways poi impulse with continuous, lagged, full-circle head travel. The longer cord stays taut; smaller weighted heads cross in front of the performer while both handles remain in the hands. The first-person resting tether remains short.
- Reworked three procedural crowd dance styles into compact running steps, lateral shuffle and larger alternating kicks. Added knee motion, opposing arm motion and a distinct chest-level poi pose. Ambient walking routes now round their corners rather than making instantaneous right-angle pivots.
- Added development-only multi-frame motion captures and diagnostic grip coordinates. Gameplay authority and collision roots remain untouched.

## Native visual evidence

The [same seller approach before](graphics-camp-and-motion-2026-09-28/seller-before.png) and [after](graphics-camp-and-motion-2026-09-28/seller-after.png) show shaped canopy hems and separate color identities. The [camp overview](graphics-camp-and-motion-2026-09-28/camp-overview-after.png) confirms that both roofs render in the native player. At the approach camera, the roofs still cover much of the top of the frame and the seller still competes with HUD panels; the composition needs more work.

The [poi first frame](graphics-camp-and-motion-2026-09-28/poi-motion-0.png) and [second frame](graphics-camp-and-motion-2026-09-28/poi-motion-1.png) show different head positions around an actor, visible taut cords and handles at both palms. The [dance first frame](graphics-camp-and-motion-2026-09-28/dance-motion-0.png) and [later frame](graphics-camp-and-motion-2026-09-28/dance-motion-2.png) show changed feet, knees, arms and torso. These are discrete frames, not proof that the motion has natural timing through an entire human viewing session.

The staged Blender source, manifest and two changed FBXs were published through the art pipeline with Unity GUIDs preserved. All other FBX exports were restored because their geometry and manifest entries did not change. Neutral six-side inspection of the shade and shop preceded publication.

## Verification and open quality gap

Project static checks, domain tests, Unity validation, EditMode and PlayMode tests, macOS development and release builds, release diagnostic exclusion, a scripted solo route, and the two-client native mission passed during this pass. New PlayMode coverage checks that both poi grips stay near the character, the tether length remains constant, the head covers the full vertical circle, and dancing moves lower legs without moving the authoritative actor root. The final longer tether and rounded walker changes were covered by PlayMode, the development and release builds, and the final two-client native run. Native screenshots were reviewed at 1280×720.

The characters still use a 13-joint procedural rig, so foot planting, turning, stopping, interaction contact, facial performance, garment deformation and weight transfer remain below the reference. The camp's broad ground, large overhead roofs, seller scale and opaque interface still need compositional and material work. A full 8–12-minute human-paced solo and cooperative walk, sustained frame-time profiling on a named Windows minimum target, and trailer-level art acceptance remain open. The overall graphics goal stays active.
