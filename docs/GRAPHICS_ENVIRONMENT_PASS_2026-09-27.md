# Environment shape and surface checkpoint — September 27, 2026

This checkpoint is part of the [graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). It improves the original campsite and reusable outdoor materials; it does **not** complete the reference-quality target or the plan's M2 playable-sample gate.

## What changed

- Rebuilt the two camp shade roofs as tensioned, sewn canvas surfaces and the tent roof as a folded fabric grid with seams, zipper and guy lines. Their gameplay collision and interactions retain their previous coordinates.
- Reworked both tree meshes with tapered, branching trunks and varied leaf masses. The near camp ring now leaves clearance around vehicles, tents and porta potties; a tree previously overlapped the close parked-car view.
- Refined the compact car's bonnet, side windows, wheel trim, seals and grille. Dark automotive glass and a separate smoother painted-metal material distinguish it from scenery. The four existing car color variants still use shared meshes.
- Generated six original 1024² tileable surface images for ground, dirt, wood, bark, foliage and canvas. Runtime materials use the appropriate image and roughness; Unity import GUIDs were retained.

The Blender source, export manifest and procedural surface source remain reproducible through `node scripts/art-pipeline.mjs stage world` and `stage surfaces`. The reviewed native meshes still use the existing collision proxies and interaction anchors.

## Native visual evidence

| View | This checkpoint | Earlier camp view |
| --- | --- | --- |
| Car at interaction distance | [Close car](graphics-environment-2026-09-27/host-camp-car.png) | [Earlier car](graphics-camp-2026-09-27/car.png) |
| Camp approach | [Canopy and tents](graphics-environment-2026-09-27/client-camp.png) | [Earlier overview](graphics-camp-2026-09-27/camp-overview.png) |
| Camp layout | [Overhead](graphics-environment-2026-09-27/host-camp-overview.png) | [Initial baseline](graphics-baseline-2026-09-27/created-camp.png) |
| Populated festival | [Stage route](graphics-environment-2026-09-27/host-crowd-live.png) | [Initial baseline](graphics-baseline-2026-09-27/host-crowd-live.png) |

The captured car is no longer crossed by a perimeter trunk. The fabric and terrain still appear fairly flat at 1280×720, the foliage remains bulbous, and the stage and clinic/security/shuttle architecture remain well below the trailer's object finish. Large persistent HUD cards still crowd the frame. These are visual observations from native captures, not an acceptance score.

## Validation and cost

`node scripts/verify-project.mjs`, `node scripts/test-domain.mjs all`, Unity validation, EditMode and PlayMode tests, the macOS development and release builds, release diagnostic exclusion, scripted solo route, and two-client native mission passed during this checkpoint. The final two-client run used development build `c60d6c87d4ae4f07a922f7bb845dd035`, seed `64898802`, 1280×720 windows and Ultra quality on the Mac named in the [baseline](GRAPHICS_BASELINE_2026-09-27.md). Its client graphics samples were:

| Point sample | Rolling frame p95 | CPU frame | GPU frame | Visible renderers |
| --- | ---: | ---: | ---: | ---: |
| Camp shopping | 8.9 ms | 8.9 ms | 1.5 ms | 263 |
| Populated festival | 16.7 ms | 15.4 ms | 7.1 ms | 980 |
| Populated festival, costly view | 16.9 ms | 16.7 ms | 13.3 ms | 1,157 |
| Results | 16.6 ms | 11.1 ms | 6.3 ms | 903 |

These are short point samples with different seeds and window focus from earlier runs. They do not demonstrate a frame-time improvement, sustained 60 fps, or minimum Windows performance. Raw local logs: client SHA-256 `c6bbfbb29ce710ab68af4041bdd539930bfddd5adff134b77f76804cc361f209`; host SHA-256 `d4ce5729111f99c4b8a9bceb7823849806fa19754633737343a501b5a13ea695`.

## Remaining production work

Finish one normal-camera route to the plan's visual standard before expanding it: market stall and stage construction, clinic/security/shuttle object forms, stronger material definition in native light, foliage and path composition, contact animation, and a smaller contextual HUD. Then validate the result during a human-paced run and controlled target-hardware profiling. The current scripted smoke teleports between major locations, so it cannot establish route composition or play feel.
