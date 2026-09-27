# Test results — September 25, 2026

Environment: Apple Silicon macOS, Unity 6000.3.24f1, Blender 5.2.2 LTS.

## Passed

- `node scripts/test-domain.mjs all`: content, simulation and cooperative-mission suites. Includes private clue eligibility, sober support throughout the action, cancellation/retry, dance gate, reward dedupe, detained escape cooldown and downed movement limits. Uses Unity's bundled compiler/runtime when a standalone SDK is absent.
- `node scripts/verify-project.mjs`: pinned packages, project structure and source boundaries.
- `node scripts/unity.mjs test-edit`: 14/14 tests passed; modular FBX groups, six fit shapes, reachable optional clothing variants, hat/hair layers, cheapest Little Spoon offer, hidden passive trust math, seller distance, inventory carry-in, camp reset, phase-aware guidance with private totem order, and existing rule/world checks.
- `node scripts/unity.mjs test-play`: 6/6 tests passed; separate campsite/festival navigation and world switching, complete navigation paths through both totem orders and all friend spawns, animation facing, poi attachment bounds, matching body/sleeve first-person hands, and pose checks.
- `node scripts/unity.mjs build-mac-development`: native development build succeeded, including project validation.
- `node scripts/native-smoke.mjs`: one host and one client process both interact with the campsite seller, validate phase-specific menu controls, visit both four-offer shop pages and return, invoke CLOSE, and buy Little Spoon for $1 before passing the ready/loading barrier into Playing with inventory intact. Remote market browsing is rejected; nearby browsing opens the shop with a visible free clue tasting action and working CLOSE button. After the client takes the free clue tasting, the affected client reports the private marker visible while the sober host reports it hidden. The client interprets both totems with host support, submits the rhythm chart, recruits the friend, and extracts. Both peers report Success, two survivors, and a $10 survivor bonus. Inspect `artifacts/native-smoke/*-camp.png`, `*-camp-overview.png` and `*-camp-shop.png` for the lobby and shop; `client-market.png` and `client-market-approach.png` for the market UI/view; `client-clue.png` for the private cue; and `host.png`/`client.png` for the character/stage render.
- Blender 5.2.2 LTS regenerated the 57-group character and 26-model world kit. `scripts/audit_character_fit.py` passed with zero failures across six body profiles, 306 fit-shape checks, 480 torso, 432 role, 108 hair/hat, 48 leg, 48 shoe, 12 backpack interface checks and 36,075 posed skirt-vertex samples across 19 poses. `artifacts/fit-review/` contains orthographic Workbench views of the selected difficult outfit and spoon necklace.

Evidence lives under `artifacts/`: Unity logs, fresh XML results, domain log, native peer logs and screenshots. The September 25 native screenshots were visually inspected for crowd silhouettes, stage framing, first-person hand scale and private clue presentation. One first-pass native run stopped at clue two; after adding development diagnostics and rebuilding, a repeat completed the mission on both peers. The intermittent cause remains unproven.

The first private-marker native build exposed a shader lookup that Unity stripped from the player. The marker now derives its material from the included `FestivalLit` resource, and the subsequent native mission/render runs pass.

## Boundaries

The native smoke test proves local transport startup, camp purchase and world transition, market UI/range behavior, snapshot/lifecycle agreement, private clue visibility, scripted mission completion and rendering on one Mac. PlayMode proves navigation paths are complete, but the native smoke teleports test actors between landmarks. These checks do not prove walkable-route usability, human pacing, rhythm feel or fun. The generated beat was not independently listened to as audio QA. The Little Spoon benefit is tested as a bounded 10% reduction in positive suspicion gains; its actual play balance needs human review.

Windows, eight-player load, separate-machine networking, injected latency/loss, microphones, a full human-played native mission, sustained performance, Steam integration and release-binary diagnostics remain unverified. The new art pass was visually compared against `artifacts/graphics-before.png`; the requested 30% is a direction, not a measured quality score.

The September 25 video-inspired polish was visually reviewed in native 1280×720 camp, camp-shop, market and clue captures. The small bloom and additional lights compiled and rendered in the macOS development build, but their frame-time cost has not been profiled. The debug F8 diagnostics continue to show frame time, RTT and local state only in Editor/development builds.

The Blender generators completed headlessly and produced `ArtSource/character-manifest.json`, `hands-manifest.json` and `world-manifest.json`. Automated checks confirm measured interfaces and basic Unity presentation. Blender review used saved Solid-style Workbench orthographic captures; an interactive Blender viewport review was unavailable in this run. The checks do not establish 80 wardrobe variants, clipping freedom across every combination, stable frame time with 40 cosmetic dancers plus gameplay NPCs, finger contact, full shuttle boarding or authored facial expressions.

## Changelog

- Changed: replaces the pre-install September 15 report with executed Unity/native evidence.
- Added: mission, character and facing regressions; actual two-client native startup/render check.
- Explicitly removed: previous zero-client/uncompiled status, which is no longer accurate.
- Changed: extended the native smoke from startup/render to a scripted complete cooperative mission on two peers; updated visual evidence after the palette, ground and stage pass.
- Changed: the latest native render includes three clothing silhouettes, blended reaction poses and sweeping stage lights; a pose-transition PlayMode check passes.
- Added: an affected-only 3D clue marker, smaller themed totems, a staged gameplay screenshot and an explicit two-peer privacy check. The in-game HUD now separates objective, player, crew and action information.
- Added: Blender character/hands/world generation, modular appearance and first-person prop checks, and fresh two-client native render evidence.
- Added: measured six-profile character fit audit, Blender and native fit galleries, and a reachable-variant regression for optional clothing.
- Added: campsite assets and navigation, physical seller purchase, Little Spoon visual/passive-effect checks, two-client camp screenshots, and a native campsite-to-festival mission smoke check.
- Added: viewer-safe objective directions, proximity-gated market browsing, phase-specific menu buttons, close-button smoke checks, escort handoff regression, full mission landmark path checks and market screenshots. The smoke still scripts inter-landmark movement.
- Added: native camp-shop captures and explicit first/second-page navigation checks; refreshed market/camp/clue captures after dusk, HUD, shop and restrained bloom changes. Static, domain, 14 EditMode, six PlayMode, development build and two-client mission smoke passed on September 25. Human feel and sustained frame-time checks remain open.
