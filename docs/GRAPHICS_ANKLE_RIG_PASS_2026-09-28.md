# Character ankle rig checkpoint — September 28, 2026

This is a measured follow-up to the [camp and motion checkpoint](GRAPHICS_CAMP_AND_MOTION_PASS_2026-09-28.md) within the active [graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). The supplied poi and rave videos remain motion references, not imported animation or artwork.

## Change

- Added left and right foot joints beneath the existing shins in the original Blender character generator. The detailed and distant character exports share the resulting 15-bone skeleton. Sneaker uppers, soles, tongues, laces, heel tabs and straps follow the new foot joints; boot cuffs continue to follow the shin.
- Counter-rotated the feet during walking, poi, and three dance patterns so a bent knee does not tilt the entire shoe with the shin. This keeps the sole closer to the ground plane in the sampled motion. The motion remains cosmetic; gameplay movement and collision are unchanged.
- Corrected the generated vendor apron clearance. The published Blender master exposed shirt/apron intersections that the fit audit missed because it read an older top-level source. The audit and fit-review scripts now default to the published generated master and accept explicit stage paths for pre-publication review.
- Kept the running user game isolated while building and smoking the new art through an alternate development app path. Development-only animation diagnostics remain excluded from release builds.

## Evidence

- [Prior native dance frame](graphics-camp-and-motion-2026-09-28/dance-motion-0.png) and [updated frame 0](graphics-ankle-rig-2026-09-28/motion-0.png), [updated frame 2](graphics-ankle-rig-2026-09-28/motion-2.png): ankle pose changes are visible at close gameplay distance. The [updated neutral character lineup](graphics-ankle-rig-2026-09-28/character-quality.png) shows the shoe and garment seams in the native player.
- [Blender side view of a stressed walking pose](graphics-ankle-rig-2026-09-28/walk-pose-left.png): both shoes remain close to horizontal while the thighs and shins are offset. It is a static stress pose, not a walking animation capture.
- Published-source Blender fit audit passed: 480 torso, 48 leg, 48 shoe, 432 role-overlay, 348 face, 30 eyewear, 108 hair/hat and 12 backpack interface checks; 114,788 skirt pose vertices examined; zero reported failures.
- Project static and domain checks, Unity validation, EditMode and PlayMode tests, alternate macOS development build, scripted solo and two-client native smoke, macOS release build, and release diagnostic-exclusion check passed. The solo route includes exiting both an A-frame and a dome tent.

The native captures are discrete frames. They do not establish continuous planted-foot contact, convincing stops/turns, natural upper-body dance timing, or full human-paced acceptance against the reference clips. Those remain open graphics work.
