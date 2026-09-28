# Grounded locomotion checkpoint — September 28, 2026

This is progress within the active [graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). The [Dear Passengers teaser](https://youtu.be/luWunttkCgY) remains the movement and environment quality target. This pass improves contact during ordinary player and NPC walking; it does not yet provide authored interaction choreography or reference-level motion.

## Change

- Added a visual-only two-bone foot solver for the existing 15-bone original character rig. During stance, each ankle keeps a world-space anchor; during swing, it travels toward the next contact and lifts over the ground. A planted shoe keeps its world orientation while the actor turns. Player/NPC collision roots and network authority do not move.
- Added a small vertical and lateral pelvis response. The imported Blender armature uses a rotated, 100x parent transform, so the world-space bob is converted through that parent before changing the bone's local position.
- Shorter character instances now use shorter strides. Foot anchors reset on route jumps, pose changes and teleportation. Development builds report the first route reset and any leg target that overreaches by more than 10 cm; those strings are checked for exclusion from the release assembly.
- Added a development-only full-body walking capture to the native two-client smoke and copied its three frames into the smoke artifact set.

## Evidence

The [first](graphics-foot-contact-2026-09-28/walk-0.png), [middle](graphics-foot-contact-2026-09-28/walk-1.png) and [later](graphics-foot-contact-2026-09-28/walk-2.png) native frames show one attendee advancing across the festival path. They show a bent swing leg and a flatter support shoe, but still frames cannot prove natural timing through a full play session. The outfit and arms remain stylized and the feet still need terrain-aware contact on slopes or irregular objects.

The PlayMode contact test drove a character at the same 0.82 body scale used in the game. Across 73 sampled frames, it recorded 35 left-foot and 31 right-foot grounded frames while the root advanced, 9 cm of clearance for each swing foot, and a successful 8 m teleport reset. All ten PlayMode tests passed. The scripted solo camp route and two-client native mission also passed. The final profiled native run logged two route-jump resets per client and no leg-reach warnings.

Project static checks, domain tests, Unity EditMode tests, macOS development and release builds, and the release diagnostic-exclusion check passed. The release check verifies that the new `[Festival.Motion]` diagnostic strings are present in a development assembly and absent from the release assembly.

Short native point samples at 1280×720 Ultra recorded 9.2–9.3 ms p95 in camp and 16.6–17.6 ms in populated festival views. They do not establish sustained 60 fps headroom on a named Windows minimum target. Human-paced motion review, stopping and turning, uneven terrain, body weight transfer, dance foot contact, hand-to-object interaction and the broader reference quality remain open.
