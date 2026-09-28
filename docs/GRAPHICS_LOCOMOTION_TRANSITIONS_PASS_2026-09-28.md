# Locomotion transitions checkpoint — September 28, 2026

This pass advances the [active graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). It improves ordinary player and NPC stopping and in-place turns; it does not satisfy the reference-level motion target or complete M2.

## Change

- The visual foot solver keeps a support shoe anchored when a character stops. If the other shoe is airborne, it completes that step. During an in-place turn it moves one shoe toward the new stance while the other carries weight. A large route or facing jump still resets the anchors.
- The shared character pose adds restrained torso, hip and head follow-through for heading changes and acceleration. The authoritative root, collision, and simulation movement stay untouched.
- The development-only native smoke now captures walk, stop and turn frames. The first foot-settle transition is logged in development builds; release diagnostics are excluded.

No new per-frame scene objects or managed collections are created by this transition. The existing distant actor update policy remains in effect.

## Evidence

The PlayMode transition test failed on the previous solver because a stationary turn never lifted a foot. After the change it passes: the root stays fixed, a turn step lifts a shoe, and a support foot remains near the floor through most sampled frames. The existing walking contact and dance contact tests still pass.

| Walk | Stop begins | Stop settles | Turn begins | Turn continues | Turn lands |
| --- | --- | --- | --- | --- | --- |
| [Frame](graphics-locomotion-transitions-2026-09-28/walk.png) | [Frame](graphics-locomotion-transitions-2026-09-28/stop-0.png) | [Frame](graphics-locomotion-transitions-2026-09-28/stop-1.png) | [Frame](graphics-locomotion-transitions-2026-09-28/turn-0.png) | [Frame](graphics-locomotion-transitions-2026-09-28/turn-1.png) | [Frame](graphics-locomotion-transitions-2026-09-28/turn-2.png) |

These stills show the support and weight-transfer pose in the native player, but do not establish natural timing through continuous human play or across every outfit and scale. Arms, clothing motion, facial attention and terrain-aware placement still need finishing.

## Verification and cost

`node scripts/verify-project.mjs`, `node scripts/unity.mjs test-play`, macOS development and release builds, release diagnostic exclusion, and `FESTIVAL_GRAPHICS_PROFILE=1 node scripts/native-smoke.mjs` passed. The two-client scripted mission and connection capture passed. The final client populated-festival samples were 16.5 and 16.7 ms rolling p95 at 1280×720 Ultra, with 11.6 and 12.4 ms GPU frames and 1,014 and 1,159 visible renderers. This is a short two-process Mac run with a different seed and camera workload from earlier checks; it does not prove sustained 60 fps or minimum Windows performance.

The smoke still teleports between major locations. A human-paced two-person traversal and continuous movement review remain required before accepting the motion standard.
