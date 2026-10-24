# Implementation status

Status at commit `d6858de` (23 October 2026, game protocol version 9). Each claim below names its evidence on that commit, or says **unverified**. The evidence is the final gate run of wave 2026-10-23. Its logs are local to the build machine (`.worktrees/wave-2026-10-23-state/final-gates/`) and are not in the repository. Update this file after the final gates of each wave. The dated checkpoints from September 2026 are in [docs/STATUS_HISTORY_2026-09.md](docs/STATUS_HISTORY_2026-09.md).

**Summary:** a playable macOS prototype of the [festival-weekend design](docs/GAME_DESIGN_2026-09-30.md), for a LAN crew or one player. Scripted solo and two-player runs pass. Nobody has played a full weekend by hand yet, and Windows, Steam and proximity voice are not built.

## Gates on d6858de

| Gate | Command | Result |
| --- | --- | --- |
| Rules | `node scripts/test-domain.mjs all` | Passed: 54 suites, then `DOMAIN TESTS PASSED` |
| EditMode | `node scripts/unity.mjs test-edit` | 834 of 836 passed, 0 failed. The 2 skipped are the two `[Explicit]` probes. ProjectSettings stayed unchanged. |
| PlayMode | `node scripts/unity.mjs test-play` | 298 of 298 passed, three full runs in a row. The tests that failed at random before this wave (the Light Show switch-off test and the dose-pattern test) then passed 10 runs each alone. |
| Generated scene | `node scripts/unity.mjs validate` | Passed |
| Builds | `node scripts/unity.mjs build-mac-development`, `build-mac-release` | Both built fresh from `d6858de` |
| Solo smoke | `node scripts/native-solo-smoke.mjs` | The first launch failed its fog check under a Pony Dust view (a test fault, open as TEST-14). The second launch passed: 56 gameplay waits ended, 0 failed |
| Two-player smoke | `node scripts/native-smoke.mjs` | Passed on the first launch: host 40 and client 82 gameplay waits ended, 0 failed |
| Release diagnostics | `node scripts/verify-release-diagnostics.mjs` | Passed: no development diagnostics in the release player |

The smokes run one or two copies of the development player on one Mac. They move players by script; they do not show that the game is fun or that a person can find their way.

## What works

| Area | Evidence on d6858de | Unverified |
| --- | --- | --- |
| Weekend loop: camp, the spin, Day 1, Night 1, Day 2 and Night 2, a debrief after each level, a failed level restarts the festival | Rules: `WeekendTests`, `DayQuotaTests`, `FinaleTests`, `DebriefTests`. Both smokes play camp, Day 1, the debrief and Night 1. | Day 2 and Night 2 in a running game: no smoke plays them. |
| The tripper: three spinners pick who, how many doses and what; visions, some false, that the tripper checks | Rules: `TripperTests`, `VisionTests`, `ConfirmTests`. Unity: `SpinnerSequenceTests`, `SpinnerTakeTests`, `VisionSessionTests`. The two-player smoke shows the visions to the tripper only and checks a clue trail. | The look of the doses and the creatures: Matt's sign-off is open (TRIP-11, TRIP-16, VISION-11). |
| Dancing: four-lane rhythm charts with holds, group dances scored by the worst dancer | Rules: `RhythmChartTests`, `RhythmHoldTests`, `GroupDanceTests`. Unity: `HoldRhythmPlayTests`. The two-player smoke plays a chart and sees a PERFECT judgment. | How the timing feels to a player. |
| Hustle: buying stock from festivalgoers, talks, sales, chats that check a vision | Rules: `StockBuyTests`, `ConversationTests`, `UntimedChatTests`. Unity: `ChatSessionPlayTests`. Both smokes buy stock, check a buyer and sell to the day's quota. | |
| Danger: wook suspicion, narcs, security stops, downed players and revival | Rules: `EscalationTests`, `CrowdTests`, `CopScatterTests`, `SimulationTests`. Unity: `WookZombieTests`, `PatrolNavigationTests`. The solo smoke dies and revives at medical. | Balance over a full weekend. |
| Minigames: Mystery Cups, Drum Circle, Light Show | Rules: `MinigameTests`, `MysteryCupsTests`, `DrumCircleTests`, `LightShowTests`. Unity: `MinigamePlayTests`, `DrumCirclePlayTests`, `LightShowPlayTests`. | No smoke plays a minigame. |
| Two festivals and their twists: Palm Mirage (influencers, VIP zones, Ferris wheel) and Ember Playa (odd cash, dust storms, art cars, the effigy burn) | Rules: `PoloTwistTests`, `PlayaTwistTests`. Unity: `TwistHudPlayTests`, `DeckSetPlayTests`. The stage benchmark loads both maps. | The smokes play Palm Mirage only. |
| Camp: shelves, the seller Marigold, tents, cars and porta potties to enter, camp secrets | Rules: `CampEggTests`, `StarterShelfTests`. Unity: `CampSellerPlayTests`, `CampEggPlayTests`. The two-player smoke buys at camp and hands an item over inside a tent. | |
| Settings: audio, graphics, controls with key rebinding, accessibility (reduced motion, high contrast, camera shake, text size), voice | Unity: `SettingsTests`, `SettingsPlayTests`. The two-player smoke opens every settings tab. | Other screen sizes and keyboard layouts by hand. |
| Music: seven recorded loops for the camp DJ and the stages | Unity: `FestivalMusicTests`, `StageAudioPlayTests`, `CampMusicPlayTests`. The solo smoke hears the camp and stage music. | Public licence clearance of the music is on hold. |
| Multiplayer: one host runs the rules, friends join by LAN address; a different game version is turned away; the host holds each client to its message limits | Unity: `ProtocolGuardTests`, `SessionAdmissionTests`. The two-player smoke runs a host and a client on one Mac. | Three to eight players, separate machines, packet loss and reconnects. |
| Performance | Stage benchmark on the `d6858de` development build, 1280×720, on the build Mac: frame p95 5.0 ms (Palm Mirage) and 4.7 ms (Ember Playa) with the six stage performers, and 4.8 and 4.9 ms with them hidden. The older BASE build measured 25.3 ms in the same run. Most of the gain comes from batched GPU skinning for every person (CROWD-7). | Windows hardware. A sustained 60 fps is not claimed. |

## Not built

- **Windows build:** the Windows Build Support module is not installed (WIN-1).
- **Steam** lobbies, invites and relay (NET-1). Players join by LAN address only.
- **Proximity voice** (VOICE-1). The settings screen and the push-to-talk key exist, but no voice provider is configured.
- **Human playtests:** Matt's own playthrough, then a friend playtest (PLAYTEST-1). The art stays frozen until then.
