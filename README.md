# Festival Co-op prototype

Native Unity/C# source for a festival social-stealth game playable solo or with up to eight players. One player hosts an authoritative session. Friends use rhythm, distractions and shared resources to find a missing friend and reach the last shuttle while wook suspicion and police evidence create separate threats.

## Current status

The Unity project compiles, automated rule and Unity tests pass, and a macOS development player has passed a two-client scripted cooperative mission and render smoke test. The first level also has a solo route, including slower clue interpretation and limited self-revival at medical. Original Blender character, hands, locations and props support the first playable art pass. A complete human-paced playthrough, performance profiling, the full wardrobe library and Windows/Steam release validation remain open. See [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md), the [reference research](docs/FRIENDSLOP_RESEARCH_2026-09-27.md), and the [approved design direction](docs/APPROVED_DIRECTION.md).

## Open the playable build

Open `Builds/macOS/Development/FestivalCoop.app`. On the main menu, choose **CREATE GAME** to enter the campsite immediately; friends can choose **JOIN GAME** with your LAN address. Pick up a shelf item with E, carry it to the seller, then press E twice to inspect and pay. Bought gear equips automatically; press 1–3 to equip another item and Q to use it. At the lit trailhead, press E to ready. When every connected player is ready, a five-second countdown starts the ten-minute festival level. One player can also complete the full route alone.

See [the playtest guide](docs/PLAYTEST_2026-09-24.md) for the complete objective and recovery steps.

## Rebuild the native player

With Unity `6000.3.24f1` and macOS Build Support active, run:

```sh
export UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity"
node scripts/unity.mjs validate
node scripts/unity.mjs test-edit
node scripts/unity.mjs test-play
node scripts/unity.mjs build-mac-development
node scripts/native-solo-smoke.mjs
node scripts/native-smoke.mjs
```

The generator creates `Assets/Festival/Generated/Bootstrap.unity`; generated content is deliberately kept separate from hand-authored source.

Run a host and one local client after building:

```sh
node scripts/launch-local-clients.mjs --binary "Builds/macOS/Development/FestivalCoop.app/Contents/MacOS/Festival Co-op Prototype" --players 2 --port 7777
```

## Controls

| Input | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Shift | Sprint |
| E | Primary nearby interaction |
| F | Chat with a nearby talkative festivalgoer |
| Q / G | Use / drop equipped item |
| 1–3 | Equip inventory slot |
| Arrow keys | Rhythm inputs |
| Tab | Hold festival map/objective view |
| Escape | Menu, shop, detailed nearby actions |
| F8 | Development-build diagnostics only |

Create/Join accepts a display name, address and port. Purchased gear enters the timed festival when every connected player readies at the trailhead. Solo games are supported in development and release builds; cooperative clue interpretation and teammate revival remain faster than the solo alternatives.

## Architecture

- `Runtime/Core` contains renderer-free rules and serializable round state.
- `Runtime/Network` binds authenticated connection identity to commands and runs the 30 Hz host authority over Unity Transport.
- `Runtime/Presentation` assembles the original Blender festival visuals with Unity collision/navigation, first-person hands, HUD, rhythm lane and local impairments.
- `Editor` generates and validates the scene and creates deterministic native builds.

Claude-of-Tanks informed lifecycle and network testing patterns. No upstream source or reserved assets were copied. Unity remains responsible for rendering, input, physics, navigation and transport.

See [docs/RUNBOOK.md](docs/RUNBOOK.md), [docs/DECISIONS.md](docs/DECISIONS.md), and [CAPABILITIES.md](CAPABILITIES.md) for operational detail.
