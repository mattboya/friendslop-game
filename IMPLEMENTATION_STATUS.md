# Implementation status

Status at commit `b7a6431` (25 October 2026, game protocol version 10). Each claim below names its evidence on that commit, or says **unverified**. The evidence is the final gate run of wave 2026-10-24. Its logs are local to the build machine (`.worktrees/wave-2026-10-24-state/final-gates/`) and are not in the repository. Update this file after the final gates of each wave. The dated checkpoints from September 2026 are in [docs/STATUS_HISTORY_2026-09.md](docs/STATUS_HISTORY_2026-09.md).

**Summary:** a playable macOS prototype of the [festival-weekend design](docs/GAME_DESIGN_2026-09-30.md), for a LAN crew or one player. Scripted solo and two-player runs pass. Nobody has played a full weekend by hand yet, and Windows, Steam and proximity voice are not built.

## Gates on b7a6431

| Gate | Command | Result |
| --- | --- | --- |
| Rules | `node scripts/test-domain.mjs all` | Passed: 59 suites, then `DOMAIN TESTS PASSED` |
| EditMode | `node scripts/unity.mjs test-edit` | 889 of 891 passed, 0 failed. The 2 skipped are the two `[Explicit]` probes. ProjectSettings stayed unchanged. |
| PlayMode | `node scripts/unity.mjs test-play` | 314 of 314 passed, three full runs in a row. Since wave 2026-10-24, test-play also fails when a run changes the quality or graphics settings. |
| Generated scene | `node scripts/unity.mjs validate` | Passed |
| Builds | `node scripts/unity.mjs build-mac-development`, `build-mac-release` | Both built fresh from `b7a6431`. Test mode is on in the development build and refused in the release build (`verify-development-selection.mjs`). |
| Solo smoke | `node scripts/native-solo-smoke.mjs` | Passed on the first launch: 61 gameplay waits ended, 0 failed. The fog check now reads the view camera's own render: it passed under Couch Lock and Pony Dust lids, and fails with no fog. Three more solo runs in a row passed. |
| Two-player smoke | `node scripts/native-smoke.mjs` | Passed on the first launch: host 40 and client 82 gameplay waits ended, 0 failed. Three more runs in a row passed. Its security picture shows a held player through the jail bars. |
| Release diagnostics | `node scripts/verify-release-diagnostics.mjs` | Passed: no development diagnostics in the release player |

The smokes run one or two copies of the development player on one Mac. They move players by script; they do not show that the game is fun or that a person can find their way.

## What works

| Area | Evidence on b7a6431 | Unverified |
| --- | --- | --- |
| Weekend loop: camp, the spin, Day 1, Night 1, Day 2 and Night 2, a debrief after each level, a failed level restarts the festival | Rules: `WeekendTests`, `DayQuotaTests`, `FinaleTests`, `DebriefTests`. Both smokes play camp, Day 1, the debrief and Night 1. | Day 2 and Night 2 in a running game: no smoke plays them. |
| The tripper: three spinners pick who, how many doses and what; visions, some false, that the tripper checks | Rules: `TripperTests`, `VisionTests`, `ConfirmTests`. Unity: `SpinnerSequenceTests`, `SpinnerTakeTests`, `VisionSessionTests`. The two-player smoke shows the visions to the tripper only and checks a clue trail. | The look of the doses and the creatures: Matt's sign-off is open (TRIP-11, TRIP-16, VISION-11). |
| Dancing: four-lane rhythm charts with holds, group dances scored by the worst dancer; the stage-pass DJ set is a three-lane ride on the stage song's beat, played on the riser, and the crowd near the stage cheers a good phrase | Rules: `RhythmChartTests`, `RhythmHoldTests`, `GroupDanceTests`, `DjSetTests`. Unity: `HoldRhythmPlayTests`, `HudRhythmTests`, `StageAudioPlayTests`, `WorldLifecycleTests`. The two-player smoke plays a chart and sees a PERFECT judgment. | How the timing and the ride feel to a player; the look of the highway and the cheer. |
| Hustle: buying stock from festivalgoers, four-round talks with a meter, sales at a Fair, High or Greedy ask, a clear result for every sale, chats that check a vision | Rules: `StockBuyTests`, `ConversationTests`, `UntimedChatTests`, `DayQuotaTests`, `LineGrammarTests`. Unity: `ChatSessionPlayTests`, `HudConversationTests`, `HudMoneyPlayTests`. Both smokes buy stock, check a buyer and sell to the day's quota. | How the new talk lines read and how the asks balance over a weekend. |
| Danger: wook suspicion with a threat bar, narcs, security stops, detention inside a barred security building, downed players with a countdown, death as a spirit with a ghostly view, and revival | Rules: `EscalationTests`, `CrowdTests`, `CopScatterTests`, `SimulationTests`, `DeathCashTests`. Unity: `WookZombieTests`, `PatrolNavigationTests`, `HudVitalsTests`, `HudLoopPlayTests`, `WorldContractTests`. The solo smoke dies and revives at medical. | Balance over a full weekend; the look of the jail's box walls until the art pass. |
| Minigames: Mystery Cups, Drum Circle, Light Show | Rules: `MinigameTests`, `MysteryCupsTests`, `DrumCircleTests`, `LightShowTests`. Unity: `MinigamePlayTests`, `DrumCirclePlayTests`, `LightShowPlayTests`. | No smoke plays a minigame. |
| Two festivals and their twists: Palm Mirage (influencers, VIP zones, Ferris wheel) and Ember Playa (odd cash, dust storms, art cars, the effigy burn) | Rules: `PoloTwistTests`, `PlayaTwistTests`. Unity: `TwistHudPlayTests`, `DeckSetPlayTests`. The stage benchmark loads both maps. | The smokes play Palm Mirage only. |
| Camp: shelves, the seller Marigold, tents, cars and porta potties to enter, camp secrets | Rules: `CampEggTests`, `StarterShelfTests`. Unity: `CampSellerPlayTests`, `CampEggPlayTests`. The two-player smoke buys at camp and hands an item over inside a tent. | |
| Settings: audio, graphics, controls with key rebinding, accessibility (reduced motion, high contrast, camera shake, text size), voice | Unity: `SettingsTests`, `SettingsPlayTests`. The two-player smoke opens every settings tab. | Other screen sizes and keyboard layouts by hand. |
| Music: seven recorded loops for the camp DJ and two stages; each level shuffles a different loop onto the main stage and the small stage, and the songs cross-fade as you walk | Rules: `StageMusicTests`. Unity: `FestivalMusicTests`, `StageAudioTests`, `StageAudioPlayTests`, `CampMusicPlayTests`, `ProtocolGuardTests`. The solo smoke hears the camp and stage music. | Public licence clearance of the music is on hold. How the cross-fade sounds. |
| Multiplayer: one host runs the rules, friends join by LAN address; a different game version is turned away; the host holds each client to its message limits; a test build's host can turn on test mode (free yourself, no time limit, clear the level) | Unity: `DebriefSessionTests`, `ConfirmSessionTests` (a hello with another protocol version is refused); `ProtocolGuardTests` (a chart or spin change must raise the version); `SessionAdmissionTests`, `DevelopmentFestivalHostTests`, `WeekendSessionTests`. The two-player smoke runs a host and a client on one Mac. | Three to eight players, separate machines, packet loss and reconnects. |
| Performance | Stage benchmark on the `b7a6431` development build, 1280×720, on the build Mac: frame p95 5.4 ms (Palm Mirage) and 5.3 ms (Ember Playa), with the small stage, its DJ and dancers and the jail bars. The machine was busy (other apps at 100% CPU), so the 0.4-0.6 ms rise from wave 2026-10-23 (5.0 and 4.7 ms) is indicative only. | Windows hardware. A sustained 60 fps is not claimed. |

## Not built

- **Windows build:** the Windows Build Support module is not installed (WIN-1).
- **Steam** lobbies, invites and relay (NET-1). Players join by LAN address only.
- **Proximity voice** (VOICE-1). The settings screen and the push-to-talk key exist, but no voice provider is configured.
- **Human playtests:** Matt's own playthrough, then a friend playtest (PLAYTEST-1). The art stays frozen until then.
