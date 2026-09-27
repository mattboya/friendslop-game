# Engineering decisions — CTO summary

These are first-pass engineering decisions, not final product or balance approval.

| Decision | Why | Business/operating effect | Revisit when |
| --- | --- | --- | --- |
| Unity 6000.3.24f1 LTS, URP, Windows-first | Matches the native Steam requirement and provides supported rendering/input/navigation | Avoids owning an engine; establishes a supportable desktop baseline | A critical plugin cannot support the pinned editor |
| One player-hosted authority, eight total players | Fits private friend sessions and limits early infrastructure | Low initial hosting cost; host quality and host loss affect the group | Steam transport spike or migration work starts |
| Plain serializable game state outside scene objects | Transactions, recovery and missions can be tested without graphics | Reduces costly multiplayer regressions and makes migration feasible later | State size or profiling proves the DTO approach inadequate |
| 30 Hz host simulation, 10 Hz reliable prototype snapshots | Sufficient for walking/social play while keeping implementation simple | Faster route to group testing; bandwidth/feel still need measurement | Native latency profiling begins |
| NGO synchronized time; client timestamps validated by host | Rhythm cannot be judged fairly from packet-arrival time | Protects core game feel under ordinary latency | T03 shows drift or cheat surface requires stronger attestation |
| Direct-address LAN before Steam integration | Proves authority and game loop before account/service work | Keeps vendor dependencies out of the earliest prototype | Separate-machine and latency testing begins |
| Original Blender and procedural art pipeline | Enables fast route/readability changes with clear asset provenance | Low content cost and repeatable generation; art still needs human quality review | Human tests validate the loop and art production expands |
| Client-side pose blending on the existing skeleton | Smooth reactions without moving the authoritative player or sending animation frames over the network | More expressive co-op visuals at little networking cost | Foot grounding or prop contact requires authored animation/IK |
| Slow stage-light sweeps without real-time shadows | Makes the stage feel alive while limiting lighting cost | Distinctive mood with modest scene complexity; frame-time still needs profiling | Eight-player performance tests show a lighting bottleneck |
| Private clue ring from viewer-filtered state | Gives the affected player a visible perception difference without revealing it to sober peers | Turns the core communication rule into something players can see and describe; keeps authority and privacy intact | Human tests show the cue is too obvious or too hard to relay |
| Development-only diagnostics | Networking/rhythm failures need IDs, timings and state visibility | Lowers debugging cost without shipping internal controls/log volume | Release gate verifies symbols and overlays are absent |
| No voice provider selected yet | Provider choice needs account, privacy, moderation and Steam-fit review | Voice remains a launch blocker rather than hidden technical debt | Two-machine native networking is stable |
| Serializable snapshots before migration | Election, transfer and reconnect need stable authority data | De-risks migration without claiming it works | Steam/session service is chosen |
| Comfort settings default to reduced motion | Impairment should create readable disadvantage without making players sick | Broadens accessibility and lowers refund/reputation risk | External playtests compare comfort/fairness |

Current implementation risk: **7/10** for reaching a reliable eight-player native prototype from this source. Core rule risk is materially lower after executable and two-client tests; the remaining risk sits in network timing under loss, eight-player performance, separate-machine behavior, and human gameplay quality. Commercial Steam release remains higher risk because voice, host migration, moderation, content clearance and human testing are unstarted.
