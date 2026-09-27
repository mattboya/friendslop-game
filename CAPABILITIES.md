# Capability inventory

Measured 15 September 2026. `PASS` means executed evidence exists in this workspace. `IMPLEMENTED_NOT_TESTED` means source exists but Unity did not compile or execute it. `BLOCKED` names a missing external dependency.

| Capability | Status | Evidence / limitation |
| --- | --- | --- |
| Native Unity project structure | IMPLEMENTED_NOT_TESTED | Exact editor/packages pinned; Unity absent |
| Host-authoritative 2–8 LAN session | IMPLEMENTED_NOT_TESTED | NGO/Transport admission, identity, commands, filtered snapshots and movement source written; zero native clients run |
| Capacity and ninth-player rejection rule | PASS (domain) | Eight accepted and ninth rejected in .NET domain tests; native connection path untested |
| Compact rescue/extraction/reset | PASS (domain) | Objective, deadline, one-time reward and reset tests pass; presentation untested |
| Rhythm grading | PASS (domain) | 80/81/150/151 ms boundaries, one note/award, misses and penalties pass |
| Network rhythm clock | IMPLEMENTED_NOT_TESTED | NGO synchronized server clock plus calibration and 750 ms grace written; latency/jitter test pending |
| Economy, transfers, stash, sale quest | PASS (domain) | Atomic/deduplicated commands and conservation scenarios pass |
| Functional item previews | IMPLEMENTED_NOT_TESTED | Each offered item runs against an isolated authority context in the HUD; UI not executed |
| Wook suspicion/swarm/downed/death | PASS (domain rules) | Observer-local suspicion and lethal state machine tested; rendered group rescue not executed |
| Police evidence/detention/release | PASS (domain rules) | Bust-before-sale and broke-pair free release pass |
| Spirit/wristband/revival | PASS (domain) | One band, one atomic revival and retry dedupe pass |
| LSD/mushroom presentation | IMPLEMENTED_NOT_TESTED | Note paths and local bounded tint/breathing source; graphics/audio review pending |
| Remaining four effects | CONTRACT_ONLY | Definitions and invariant rhythm-path tests; no finished presentation/audio |
| Dialogue/history | PASS (domain) | 48 draft lines, unseen preference, reservations, fallback and bounded history pass |
| Generated festival/NavMesh | IMPLEMENTED_NOT_TESTED | Original 80 m map, routes and 40 cosmetic dancers; Unity PlayMode path tests unexecuted |
| Development diagnostics | IMPLEMENTED_NOT_TESTED | F8 overlay and structured transition logging compile only for Editor/Development builds |
| Native Windows/macOS build | BLOCKED | Unity editor/build modules unavailable; no executable exists |
| Proximity voice | BLOCKED | `IVoiceSession` boundary exists; no provider/account configured |
| Steam identity/invites/transport | BLOCKED | No Steamworks environment or adapter configured |
| Host migration | BLOCKED | Portable snapshot/restore continuity passes; election, state transfer, network-ID rebinding and reconnect are not implemented |
| Human/physical-machine playtest | BLOCKED | Requires testers, machines and native build |

Direct-address LAN support is the development topology. It is not evidence of NAT traversal, Steam invites or public internet reliability.
