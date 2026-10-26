# Festival Co-op prototype

Native Unity/C# source for a festival social-stealth game playable solo or with up to eight players. One player hosts an authoritative session. Friends use rhythm, distractions and shared resources to find a missing friend and reach the last shuttle while wook suspicion and police evidence create separate threats.

## Current status

A playable macOS prototype of the [festival-weekend design](docs/GAME_DESIGN_2026-09-30.md) for a LAN crew or one player: camp, a spin that picks the tripper and their dose, then Day 1, Night 1, Day 2 and Night 2, with a debrief at the campfire after each level. Automated tests and scripted solo and two-player runs pass. Nobody has played a full weekend by hand yet, and Windows, Steam and proximity voice are not built. [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) says what works today, on which commit, and the evidence for each claim.

## Open the playable build

Open `Builds/macOS/Development/FestivalCoop.app`. On the title screen, choose **PLAY**, then **HOST A GAME** to enter the campsite immediately; friends choose **PLAY**, then **JOIN A FRIEND** with your LAN address. Pick up a shelf item with E, carry it to the seller, then press E twice to inspect and pay. Handheld gear equips automatically; the Little Spoon is worn as a necklace. Press 1–3 to equip another handheld item and Q to use it. Explore a car, tent or porta potty with E, use F for its gag, and E or G to leave; use E at the camp DJ to change the music. At the lit trailhead, press E to ready. When every connected player is ready, a five-second countdown and the spin start Day 1, an eight-minute level. After the round, everyone votes for a silly award with 1–3, and the host opens the shop with E. One player can also complete the full route alone.

See [the festival-weekend design](docs/GAME_DESIGN_2026-09-30.md) for the complete objective of each level.

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

The solo smoke ends with one pass line, for example `SOLO NATIVE SMOKE PASSED in 105.2 s real; 61 gameplay waits ended, 0 failed; slowest solo night 1 start sim 15.10/250.00 s real 15.09 s`. The two-player smoke prints a `HOST NATIVE SMOKE PASSED:` and a `CLIENT NATIVE SMOKE PASSED:` line, then `TWO-CLIENT NATIVE SMOKE PASSED`. Then a third player captures the connection screens, and a full pass ends with `CONNECTION UI CAPTURE PASSED in N s real`. A smoke passed only when it prints its last pass line and exits with code 0. Each gameplay wait has a budget of simulation seconds, not real seconds. A failed run names its wait as `FAILED <wait> (<reason>)`, and the player logs are in `artifacts/native-smoke/`. A whole smoke stops after 900 real seconds. On a heavily loaded machine, raise that limit with `FESTIVAL_SMOKE_TIMEOUT_SECONDS`, for example `FESTIVAL_SMOKE_TIMEOUT_SECONDS=1800 node scripts/native-solo-smoke.mjs`.

The generator creates `Assets/Festival/Generated/Bootstrap.unity`; generated content is deliberately kept separate from hand-authored source.

Run a host and one local client after building:

```sh
node scripts/launch-local-clients.mjs --binary "Builds/macOS/Development/FestivalCoop.app/Contents/MacOS/Festival Co-op Prototype" --players 2 --port 7777
```

## Controls

| Default key | Action |
| --- | --- |
| W A S D / mouse | Move / look around |
| Left Shift | Sprint (the crowd notices) |
| E | The top action on your list; leave a camp interior |
| F | Talk to someone; the tripper checks a vision; an antic inside a camp interior; at camp before Day 1, the host switches festival; call the cups in Mystery Cups |
| Q | Use what is in your hand |
| G | Leave a camp interior or put back a shelf item; hold 0.5 s to drop what is in your hand; in a security stop, ditch your stock; hold 1 s to stop dancing |
| 1 / 2 / 3 | Hand slot 1–3; reply 1–3 in a talk or chat; vote at the campfire debrief |
| Mouse wheel | Next hand slot down, previous hand slot up |
| Hold left click | Spin the poi in front of you |
| Hold right click | Spin the poi at your sides |
| V | First or third person |
| Hold Tab | Map |
| Arrow keys, or W A S D | The four dance lanes: left, down, up, right |
| 1–5 | Light Show answers 1–4, and 5 for not sure |
| Hold T | Push to talk (no voice provider is configured yet) |
| Escape | End or close a chat; menu and settings |
| F8 / F9 | Development builds only: diagnostics overlay / graphics overlay |

The table shows the default keys. SETTINGS > CONTROLS rebinds every one except Escape and F8 / F9.

## Architecture

- `Runtime/Core` contains renderer-free rules and serializable round state.
- `Runtime/Network` binds authenticated connection identity to commands and runs the 30 Hz host authority over Unity Transport.
- `Runtime/Presentation` assembles the original Blender festival visuals with Unity collision/navigation, first-person hands, HUD, rhythm lane and local impairments.
- `Editor` generates and validates the scene and creates deterministic native builds.

Claude-of-Tanks informed lifecycle and network testing patterns. No upstream source or reserved assets were copied. Unity remains responsible for rendering, input, physics, navigation and transport.

See [docs/RUNBOOK.md](docs/RUNBOOK.md) and [docs/DECISIONS.md](docs/DECISIONS.md) for operational detail, and [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) for what works today.
