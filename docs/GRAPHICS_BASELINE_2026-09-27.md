# Graphics baseline — September 27, 2026

This is the recoverable source and native-render comparison point for the [graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). It predates the graphics implementation on branch `codex/graphics-upgrade`.

## Recovery and capture conditions

- Source: Git commit `1d299c663da5d0bc905b0382444398608c2dcc11` (`Initial Unity festival game source`). The working tree was clean when the baseline was taken.
- Native player: `Builds/macOS/Development/FestivalCoop.app`, archived locally at `artifacts/graphics-baseline-2026-09-27/FestivalCoop-baseline.app.zip` (128 MiB, SHA-256 `7588c4857daf7779170679eb90bb2e45c5c023fcf105e1d4076a063cda304552`). The archive is ignored by Git; keep it with the workspace or rebuild from the pinned source.
- Editor: Unity 6000.3.24f1. Development player executable timestamp: September 27, 14:40 local time.
- Development machine: MacBook Pro Mac17,9, Apple M5 Pro (18 CPU cores, 20 GPU cores), 48 GB unified memory. Captures used two local 1280×720 processes on this machine; they are not Windows or eight-machine evidence.
- Reproduction: `node scripts/unity.mjs build-mac-development`, then `node scripts/native-smoke.mjs`. The smoke harness uses port 17779, 1280×720 windowed clients, scripted mission steps and teleports. It overwrites `artifacts/native-smoke/`, so dated copies are below.

## Dated views

| View | Capture |
| --- | --- |
| Camp arrival | [created-camp.png](graphics-baseline-2026-09-27/created-camp.png) |
| Camp seller | [client-camp-shop.png](graphics-baseline-2026-09-27/client-camp-shop.png) |
| Market approach | [client-market-approach.png](graphics-baseline-2026-09-27/client-market-approach.png) |
| Populated stage | [host-crowd-live.png](graphics-baseline-2026-09-27/host-crowd-live.png) |
| Grove crowd | [client-crowd-grove.png](graphics-baseline-2026-09-27/client-crowd-grove.png) |
| Character lineup | [client-character-quality.png](graphics-baseline-2026-09-27/client-character-quality.png) |
| Face effects | [client-face-states.png](graphics-baseline-2026-09-27/client-face-states.png) |
| Rhythm camera | [client-rhythm.png](graphics-baseline-2026-09-27/client-rhythm.png) |

The eight PNGs are source-controlled in `docs/graphics-baseline-2026-09-27/`; the same files are in the ignored local baseline directory. The scripted two-client mission and solo mission passed in their existing logs. The client rhythm log reported 512 frame samples, 8.3 ms p95 and 8.3 ms maximum. That is the harness's frame interval sample, not an isolated CPU/GPU profile; the earlier 17.3 ms p95 in `IMPLEMENTATION_STATUS.md` used a different run. Neither number establishes a minimum-spec graphics budget.

## Source and rendering inventory

- `ArtSource/world-manifest.json` lists 27 original generated world models, 25,764 triangles in aggregate before scene instancing. All 27 FBX exports exist in `Assets/Festival/Art/Resources`, and every model name has a literal reference in the current presentation or session code. This does not establish that every model appears in every round.
- Character source: `ArtSource/FestivalCharacter.blend` plus detailed and distant FBX exports; hand source: `ArtSource/FestivalHands.blend`. See [character quality](CHARACTER_QUALITY_PASS.md) for rig and geometry limits. There is no separate authored finishing layer yet, so direct regeneration still risks overwriting source work.
- The saved URP asset has HDR on, render scale 1, MSAA 1, main-light shadows on, one cascade, soft-shadow support off, additional-light shadows off, and a 2048 main shadow map. Both directional world lights request soft shadows, so the requested and supported modes differ.
- Before the first code change, `FestivalWorld` created a global post-processing volume at priority 20 with ACES, color grade and bloom, while `FestivalSession` created another bloom volume at priority 5. The priority-20 bloom overrides the lower-priority setting. This should be owned by one place.
- Human-paced walking, shopping, rescue, rhythm feel, visual acceptance, CPU/GPU profiling and a Windows minimum machine specification remain open. A two-minute crowded traversal and full human mission are still needed to close M0's performance and route gates.

## First comparison observations

The camp and stage captures show the planned gaps: large flat ground/path shapes, repeated tree silhouettes, little local light on faces, and a large persistent top UI area. The character lineup is useful for checking face readability and outfit fit under stage lighting. These are visual observations from the captured views, not a measured art-quality score.
