# Market-to-stage visual checkpoint — September 27, 2026

This is a native-rendered iteration within the [full graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). The reference-quality target and the M2 playable sample gate remain open.

## Changes

- Rebuilt the three market stall awnings as sloped, sewn canvas and their counters as framed, slatted construction. The supplies, poi and stock displays have more distinct forms. These visual meshes retain the existing collision and purchase locations.
- Changed the stage from a flat roof and dark deck front to a tensioned touring canopy, ribbing, constructed deck face, banners, speaker rims and a larger Sun/Moon mark. An `AFTER HOURS` sign remains visible from the normal approach after correcting its distance culling.
- Assigned authored canvas, wood, bark and foliage material families when `FestivalWorld` instantiates its FBX assemblies. Previously, several of those meshes kept their importer material at runtime. A narrow, uneven dirt border now breaks the main path's straight edge without changing its walkable surface or NavMesh.

The original Blender source and export manifest remain in `ArtSource/Generated/` and `ArtSource/`. `scripts/render_world_asset_review.py` now supports neutral views of the stage and all three stalls. Staged exports preserve their Unity GUIDs.

## Native comparison

| View | This checkpoint | Earlier view |
| --- | --- | --- |
| Market approach | [Near stall](graphics-market-stage-2026-09-27/client-market-approach.png) | [Initial baseline](graphics-baseline-2026-09-27/client-market-approach.png) |
| Market seller | [Full stalls](graphics-market-stage-2026-09-27/client-market.png) | No dated matching view |
| Stage approach | [Stage with HUD](graphics-market-stage-2026-09-27/client-crowd-live.png) | [Earlier environment pass](graphics-environment-2026-09-27/host-crowd-live.png) |
| Stage composition | [Stage without HUD](graphics-market-stage-2026-09-27/client-crowd-stage.png) | [Initial baseline](graphics-baseline-2026-09-27/host-crowd-live.png) |

The approach frame reads more like an assembled event structure, and the stall counters have visible construction. It still falls short of the supplied trailer's polish: broad geometry, crowded shop labels, repeated foliage, muted near-camera material detail, sparse intermediate set dressing and simple character motion remain visible. The four new images above are source-controlled.

## Geometry and cost

The stage export is 7,756 triangles across 12 renderer groups, up from 3,520 across 6. The supplies, performance and stock stalls are 3,032/3,852/3,004 triangles across 7/8/8 renderer groups respectively. The additional main-path border is one uncollidable mesh and one renderer. No new shadowed light was added.

The final scripted two-client development run used build `3ac60af3adec4d168f35d4e7be61e540`, seed `66500502`, 1280×720 Ultra windows on the [baseline Mac](GRAPHICS_BASELINE_2026-09-27.md). Client point samples were 10.1 ms rolling p95 in camp, 16.8–16.9 ms in two populated festival views, and 15.7 ms at results. CPU/GPU latest-frame samples and rolling p95 windows are different measures. The seed, camera focus and window focus differ from earlier runs; these numbers do not establish improvement or sustained 60 fps. Raw local logs: client SHA-256 `fc6f7938fbc1e1c6133ff794ecf8764a6271ad47d80d549480a2292f61abf813`; host SHA-256 `aa2a55c7940396a66a62a11476b80077a8092a79c7e40baba08e8cb552db4fb7`.

Project static checks, domain tests, Unity validation, EditMode and PlayMode tests, the macOS development and release builds, scripted solo and two-client native missions, and release diagnostic exclusion passed after the final changes. The native capture visually confirmed the stage sign at approach distance after its culling fix.

## Remaining gates

Review the market-to-stage walk at human pace, including how hands, props, goods and labels overlap. Reduce the persistent HUD footprint, finish the medical/security/shuttle architecture and path transitions, and improve local lighting, material response and animation contact. Measure on agreed target hardware with controlled CPU/GPU captures. The scripted run proves mission continuity and these specific views; it does not establish human visual acceptance.
