# Festival Co-op foundation review

Reviewed 15 September 2026. This is an understanding/reuse assessment, not a claim that the implementation handoff has been executed.

## Product understanding

The core experience is first-person social stealth for 2–8 friends in a stylized low-poly music festival. Looking normal provides cover; rhythm-based dance and conversation sustain that cover. Consuming stock can earn approval while impairing local presentation. Mistakes attract nearby wooks, whose suspicion can escalate into lethal pursuit. Police use separate evidence, especially witnessed selling. Cooperation provides recovery through distraction, DJ performances, paired poi, downed rescue, detention release and wristband-based revival. Dead players remain active in a limited spirit layer.

The initial playable objective is a five-minute rescue: locate the missing friend, escort them to the shuttle and extract at least one living player. Longer rescue/retrieve/deliver missions remain the product direction. Purchases, sales and shared storage must be host-validated; the $50 team gross-sales quest is separate from cash. Free recovery tasks prevent an all-broke team from getting stuck.

The handoff adds eleven item definitions, seeded vendor offers, isolated functional previews, dialogue history with 48 draft lines, two playable effects and contracts for four later effects. These are substantial systems, beyond a basic scaffold.

## Source differences to preserve

Both supplied files were read. Neither has been edited.

| Subject | Saved design spec | Implementation handoff | Working interpretation |
| --- | --- | --- | --- |
| Version | Header says 1.1 | Claims source design 1.3 | Actual v1.3 source is not present; do not claim it was reviewed |
| Duration | 15–20 minutes; five-minute prototype | Variable event lengths up to roughly an hour; five-minute prototype | Five minutes is common ground for the first slice |
| Effects | Fictional names and five candidate distortions | LSD/mushrooms playable; six named presets | Handoff supplies concrete prototype defaults; terminology differs |
| Vendors/dialogue/previews | Less detailed | Seeded offers, history, 48 lines, isolated previews | Treat these as handoff additions |
| Capacity | One acceptance sentence says eight clients plus host | Explicitly one host player plus seven remote players | Eight total, consistent with both documents' stated 2–8 range |
| Stack | Native Unity/C#, Windows-first Steam | Unity 6 LTS, URP, NGO, Transport, Input System | Preserve Unity unless user chooses otherwise |

## Claude-of-Tanks reuse assessment

Reference tree inspected at commit `20e6aa7222c07e03dd7e24b1b29bcd582874607e`:
[repository](https://github.com/Kevin-Liu-01/Claude-of-Tanks/tree/20e6aa7222c07e03dd7e24b1b29bcd582874607e).

Its current architecture is documented in `docs/SYSTEMS.md` and `docs/MULTIPLAYER-ARCHITECTURE.md`. `docs/ARCHITECTURE.md` explicitly labels itself historical. This review inspected documentation and selected source; it did not run or certify the reference game's tests.

| Reference | Reusable lesson | Application here |
| --- | --- | --- |
| `docs/SYSTEMS.md` | Gameplay truth independent of scene objects; fixed-step authority; quality changes presentation only | Plain C# rules/state behind Unity components; local hallucinations cannot mutate shared truth |
| `src/net/networkBattleBarrier.ts` | Wait for authoritative identity and peer readiness; cancel stale retries across rounds | Loading acknowledgements before starting the rescue clock; explicit timeout and cleanup |
| `docs/MULTIPLAYER-ARCHITECTURE.md` | Separate simulation, transport and presentation; durable state survives missed events | Host validates intent; replicate persistent facts plus reliable one-shot outcomes |
| Same document, broken-room contract | Bounded reconnect and stalled-host handling; prevent old callbacks/actions entering a new room | Clear host-loss UI and fresh input after recovery; no accidental replay of purchases |
| `src/net/adverseNetworkTransport.ts` | Seeded latency/jitter/loss tests distinguish reliable control from replaceable state | Use Unity Transport tooling plus timestamped rhythm fixtures; do not port a browser transport unnecessarily |
| `src/engine/frameLoopScheduler.ts` | Browser scheduling and lifecycle complexity | Engine-specific; Unity should supply the base player loop rather than porting browser scheduling |

The reference explicitly states that it has no host migration or automatic match restoration after host loss. It is not a shortcut to that release requirement. Tank movement, armor, weapons and vehicle cameras are a poor fit for festival player movement and interaction.

### Copying boundary

The current [license policy](https://github.com/Kevin-Liu-01/Claude-of-Tanks/blob/20e6aa7222c07e03dd7e24b1b29bcd582874607e/LICENSE-POLICY.md) describes first-party source, tests, tools and general engineering documentation as MIT by default, with explicit exceptions. Reserved paths include `src/world/**`, `src/vehicles/**`, `public/audio/**`, `public/fx/**`, maps, branding and other media. Individual and third-party notices also matter.

Do not wholesale copy the world generator, props, vehicles, music or effects assets. For any actual code reuse, inspect that exact file and dependencies at the pinned revision and retain applicable notices. No upstream code or assets have been incorporated into this workspace by this assessment.

## Foundation recommendation

Keep the specified Unity stack and use its existing rendering, input, collision, navigation and networking facilities. Transfer the reference's useful lifecycle and testing patterns. Switching engines just to reuse its scaffolding would change the selected native product architecture and needs an explicit decision.

First implementation milestone:

1. Pin an installed compatible Unity LTS editor and resolved package versions.
2. Generate an original compact festival map and eight spaced player spawns through repeatable editor tooling.
3. Wire first-person movement, remappable input, Host/Join/Ready/Leave, and one host plus one real client.
4. Validate one interaction and item handoff under host authority, then expand capacity testing to seven remote clients and rejection of a ninth participant.
5. Add the timed rescue/extraction/reset loop before broad item/content work.
6. Compile structured diagnostics and overlays only under `UNITY_EDITOR || DEVELOPMENT_BUILD`; log transitions, timing and transaction IDs without recording voice or credentials.

Use the handoff's M0–M8 and T01–T19 for the full prototype; a scaffold cannot satisfy those gates. The highest early technical risk is fair timestamp-based rhythm scoring across network delay, followed by authoritative transactions and recovery lifecycle consistency.

## Environment observed during the initial assessment

- Workspace initially contains only the two supplied Markdown documents; it is not a Git working tree.
- Unity/Unity Hub were not found at standard `/Applications` locations. Spotlight search for `Unity.app` returned no matches; no `Unity` command was found on PATH. A custom/unindexed installation remains possible.
- No `dotnet` command was found on PATH; Node and npm are available.
- Editor activation, Windows build modules, voice provider and Steam configuration have not been verified.
- No source scaffold, compiled native build or game/network tests existed at that assessment point. The subsequent implementation is tracked in the repository-root status documents; zero native game clients have still been tested because the Unity dependency remains absent.

The user chose native Unity for Steam. The next dependency is to locate/install and activate the pinned editor before claiming successful import, compilation or native execution. Uncompiled project tooling remains labeled `NOT_COMPILED`.
