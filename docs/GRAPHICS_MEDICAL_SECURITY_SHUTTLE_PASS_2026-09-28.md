# Medical, security and shuttle object checkpoint — September 28, 2026

This checkpoint advances the [graphics upgrade plan](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). It does not complete the reference-quality target, the M2 playable sample, or full first-level production.

## Changes

- The original last-shuttle mesh has a tapered minibus cab and rear hatch, a sloped windscreen seated against the shell, framed rear glass, and adjusted roof and side panels. Its open 2.6 m boarding doorway and the existing separate collision proxies retain their locations.
- The medical tent has screened side windows, rolled flaps, a weather skirt, visible crosses from both side approaches, and tied roof guy lines. The recovery entrance remains open.
- The security cabin has lower pressed-metal ribs, a kick plate, warning band, side badges and a small roof beacon. The service entrance and holding space remain open.

All geometry is original and reproducible from `scripts/generate_festival_world_assets.py` and `ArtSource/Generated/FestivalWorld.blend`. The asset pipeline staged and validated the whole kit; only the three changed FBX exports were retained in the runtime tree. Their Unity GUIDs are unchanged.

| Asset | Before | After |
| --- | ---: | ---: |
| Shuttle | 5,760 triangles / 11 renderers | 6,356 triangles / 11 renderers |
| Medical | 4,236 triangles / 9 renderers | 4,676 triangles / 10 renderers |
| Security | 3,340 triangles / 7 renderers | 4,112 triangles / 7 renderers |

These are unique asset counts from the export manifest, not measured frame totals. Material-group merging avoids a renderer increase for shuttle and security.

## Native comparison

| View | Before | After |
| --- | --- | --- |
| Medical | [Before](graphics-shuttle-2026-09-28/medical-before.png) | [After](graphics-shuttle-2026-09-28/medical-after.png) |
| Security | [Before](graphics-shuttle-2026-09-28/security-before.png) | [After](graphics-shuttle-2026-09-28/security-after.png) |
| Shuttle | [Before](graphics-shuttle-2026-09-28/shuttle-before.png) | [After](graphics-shuttle-2026-09-28/shuttle-after.png) |

At these scripted cameras the side identities are clearer, the clinic sign and door remain visible, and the shuttle reads more like a passenger vehicle. The cabin wall forms and bus body still have a simplified, toy-like finish; the large HUD panels obscure substantial scenery. Close interior movement and boarding with multiple human players remain unreviewed.

## Verification and remaining risk

`node scripts/verify-project.mjs`, `node scripts/unity.mjs test-play`, both macOS builds, and the final `FESTIVAL_GRAPHICS_PROFILE=1 node scripts/native-smoke.mjs` passed on the selected final assets. The PlayMode world test covers routes to the medical area and shuttle and verifies the generated world/NavMesh. The two-client smoke completes the mission, but teleports between major locations.

The final populated client samples on one Mac at 1280×720 Ultra were 12.9 and 16.7 ms rolling p95. An earlier run of the same pass had a 21.7 ms playing sample and later 17.0/17.5 ms samples. Different seeds, camera contents and short sampling intervals prevent a reliable before/after performance claim. Sustained frame timing, minimum Windows hardware, the full boarding flow, and human acceptance are still open.
