# Festival Co-op — LLM implementation handoff

Version: 1.0 | Prepared: 8 September 2026

Source: `festival-coop-v1-spec.md`, document version **1.3**, retrieved from its current saved revision. This handoff includes the newer variable-duration missions, six named intoxicant effects, conversation history, randomized vendors and free item previews. It supersedes assumptions from earlier 15–20-minute-only drafts for this implementation attempt. The design source itself is unchanged.

## 1. How to use this document

Give this file to a coding agent in a local project directory on a machine with a licensed Unity editor, native build support, a graphics session and permission to run the game. The design spec is useful supplementary context; the required first-build behavior is contained here. Use an agent capable of editing files, invoking Unity, reading logs and inspecting screenshots. A chat model that can only return text cannot complete this task.

“One shot” means **one initial instruction followed by the agent's own build, test, inspect and repair loop**. It does not mean generating every file once and claiming success. No document can guarantee that an autonomous run will ship the complete commercial game.

Paste this instruction with the file:

> Implement `festival-coop-llm-implementation.md` in this workspace. Read it fully, inspect the environment, and execute milestones M0–M8 in order. Make the ordinary decisions already specified here without asking me to choose again. Build a native Unity/C# game with real multiplayer and the complete compact festival loop. Generate and wire the scenes, prefabs, input, UI, data and placeholder assets through repeatable editor tooling. Compile and test as you go, run the actual application when possible, and fix observed failures. Keep `IMPLEMENTATION_STATUS.md` updated with evidence and remaining work. Implement configured voice/internet integrations where access exists; otherwise expose the limitation clearly and finish the independent prototype work. Do not substitute bots for verified human/network clients, label an adapter stub as a working feature, or call an untested build complete. Preserve the full release requirements in the status file. Finish with the source project, native build if supported, exact run instructions, test evidence and a concise limitations report. Respect existing repository instructions and permission boundaries.

### Authority and scope

- The confirmed product decisions below are fixed. The first-build subset and numeric settings in this handoff are **proposed implementation defaults**, selected to make an autonomous attempt concrete. They do not silently approve or delete the source spec's remaining design decisions.
- Execute useful independent work when an optional integration is blocked. Stop at missing permissions, purchases, license activation or required account access; do not invent credentials or bypass controls.
- If Unity is unavailable, write the project and tooling and report `NOT_COMPILED`. Do not quietly switch to a browser engine. If the editor runs but no graphics session exists, report visual testing as unavailable.
- Never publish, buy assets, change account settings or upload a Steam build as part of this instruction.
- If repository instructions conflict with the proposed stack or folder layout, preserve the existing project and report the concrete conflict before invasive migration.

## 2. Fixed product requirements and first-run boundaries

The game is a stylized low-poly festival social-stealth comedy with real danger. **2–8 players** cooperate; wooks suspect them of being narcs, initiate dance/conversation rhythm challenges and can swarm and kill them. Players can buy, accept, consume or sell game intoxicants, which trade local acceptance for impairment. Police can witness deals and detain players. Friends can distract NPCs, DJ, use poi, rescue a downed player, release detainees and revive dead players through the medical tent. Dead players occupy a playable spirit layer.

Commercial direction: Unity/C#, native Windows-first Steam release, proximity voice, host migration, cosmetic/content unlocks without permanent power advantages, and music from cleared AI-generated tracks plus commissioned originals. Missions rotate among rescue, retrieve and deliver. Duration scales with festival and objective: compact events may take 10–20 minutes; a major-event mission may approach an hour. Do not use a real festival's brand as the project's title or asset dependency.

| Requirement | Autonomous first build | Preserved release work |
| --- | --- | --- |
| Multiplayer | One host player plus up to seven remote clients; direct address/port connection; 2–8 supported | Steam identity, private invites and reliable internet connectivity |
| Mission loop | One authored map; five-minute rescue mission fully playable | Fully authored retrieve/deliver missions; multiple scales and duration bands |
| Rhythm and NPCs | Dance, conversation, sales and police-stop contexts; lethal interruptible swarm | Larger encounter and animation variety |
| Economy | Purchases, stock trades, $50 side quest, shared stash and recovery income | Balanced campaign economy and more content |
| Effects | LSD and mushrooms playable; all six definitions represented in effect schema and contract tests | Finished ecstasy, ketamine, alcohol and weed presentation/assets |
| Items | Eleven functional item definitions described in section 9; all offered items previewable | Remaining catalog in section 18 |
| Dialogue | At least 48 distinct draft lines; history and fallback fully implemented | Large human-reviewed pool, localization and final audio |
| Voice | Real positional voice if a configured supported provider is available; clearly disabled otherwise | Mandatory real multi-user voice acceptance before release |
| Death | Downed rescue, wristband recovery, spirit movement and medical revival | Further spirit activities and tuned costs |
| Host migration | Serializable state with tested save/restore; honest host-loss UI | Actual election, reconnect, state transfer and fault-tested continuation |
| Progression | Versioned profile and one earnable cosmetic; no stat upgrades | Mission and item sidegrade unlock content |

The first build is a **prototype** even if every first-build gate passes. Voice without a configured provider is `BLOCKED`, not complete. Serialized state is not host migration. A LAN build is not verified internet co-op. Preserve these distinctions in the UI, README and final report.

## 3. Environment and dependency decisions

### M0 preflight

Inspect the repository, `AGENTS.md`, working-tree changes, available Unity editors, platform build modules, graphics access and configured integrations. Record results in `docs/TOOLCHAIN.md` before creating gameplay files.

For a new project, use the installed stable **Unity 6 LTS** editor compatible with the selected packages. If several are installed, prefer the newest installed LTS with the required Windows build module. Preserve a compatible existing Unity project's pinned version. Resolve exact package versions against that editor's official documentation and package registry, then commit `ProjectVersion.txt`, `Packages/manifest.json` and `Packages/packages-lock.json`. Do not write `latest` or invented package numbers into a manifest. Do not keep upgrading packages during implementation.

Default choices for this attempt:

| Area | Choice and constraint |
| --- | --- |
| Rendering | URP; one directional light, restrained bloom, emissive stage props; avoid expensive bespoke shaders initially |
| Gameplay | C# components and small testable rule classes; no ECS migration or custom engine |
| Networking | Unity Netcode for GameObjects and Unity Transport, compatible pinned versions; host-authoritative rules |
| Input | Unity Input System, keyboard/mouse first, remappable actions |
| UI | uGUI with text components appropriate to the installed package set; generate references through editor code |
| Navigation | Compatible Unity AI Navigation package or an existing project navigation solution; host moves agents |
| Audio | Unity audio scheduling, local music/effect mixer; provider-based voice |
| Testing | Unity Test Framework: edit-mode domain tests and play-mode integration tests |
| Storage | Versioned local JSON profile; authoritative round snapshot in separate DTOs |
| Assets | Procedural primitives, generated meshes/materials and a local tempo-locked music placeholder; no paid asset prerequisite |

Use the existing configured positional voice provider if compatible. If none exists, prepare a narrow `IVoiceSession` boundary with an explicit unavailable implementation. Do not spend the first run inventing a codec or unreviewed voice service. `CAPABILITIES.md` must say whether voice was actually connected and tested. A microphone permission denial is a user decision, not an error to bypass.

Unity's documentation exposes NGO, transport and DSP scheduling, but package generations differ. Use docs for the resolved versions. In particular, session-host ownership and transfer of live game state are different problems; never assume changing a lobby owner implements migration.

## 4. Required project deliverables

Create a runnable project, not a folder of unattached scripts. All runtime references must be assigned. An editor generation entry point must create the map, network prefab registration, scenes, input asset, data assets and build settings without manual inspector assembly.

Suggested structure; preserve an existing equivalent organization:

```text
Assets/Festival/
  Runtime/Core/          # Domain rules, IDs, commands, DTOs, configuration
  Runtime/Network/       # NetworkBehaviour adapters, authority and replication
  Runtime/Gameplay/      # Missions, players, NPCs, inventory, recovery
  Runtime/Presentation/  # HUD, rhythm lanes, effects, animation, audio
  Runtime/Integrations/  # Voice and future Steam/session boundaries
  Editor/               # Bootstrap, validation and build entry points
  Tests/EditMode/
  Tests/PlayMode/
  Scenes/               # Bootstrap, Lobby, Festival, Preview
  Data/                 # Item, effect, chart, NPC, dialogue, vendor definitions
  Art/                  # Generated meshes/materials and their generator inputs
  Audio/
Packages/
ProjectSettings/
Builds/Windows/
docs/TOOLCHAIN.md
docs/RUNBOOK.md
docs/CAPABILITIES.md
docs/ASSET_PROVENANCE.md
docs/TEST_RESULTS.md
IMPLEMENTATION_STATUS.md
```

Use assembly definitions to keep editor code out of players and let domain rules run independently in tests. Do not build a generic dependency-injection framework, service bus, ability language or speculative plugin architecture. Interfaces are warranted for clocks, transport/session access, voice and persistence; use ordinary classes for other rules.

Create editor methods with these names or document the actual equivalent:

- `Festival.Editor.ProjectBootstrap.EnsureGeneratedContent`: idempotent generation limited to the project's generated-content directory; preserve manually authored assets and stable IDs.
- `Festival.Editor.ProjectValidation.Validate`: detect missing scripts/references, invalid item IDs, unmatched chart/audio lengths, duplicate dialogue IDs, missing network prefabs and absent build scenes; return failure on errors.
- `Festival.Editor.BuildEntry.BuildWindowsDevelopment` and `BuildWindowsRelease`: deterministic output locations and failed-build exit status.

## 5. Compact map and player experience

Build an approximately 80 m × 80 m authored test festival with fencing and safe boundaries. Use fictional signage and a coherent twilight palette: warm vendor tents, cool stage lights, readable player colors. Include a clear main path and two alternate routes.

Required locations: entrance/shopping lobby, stage/DJ console, dance crowd, stock seller, checkpoint and police holding area, shared stash, medical tent, missing-friend location and extraction shuttle. Seed eight spawn positions with spacing. Spawn 24 interactive wooks and two cops initially, plus up to 40 cosmetic dancers without decision logic. These counts are adjustable profiling defaults, not promised production density.

First-person camera; capsule player; visible remote low-poly bodies with display name and distinct color. Base walk 4 m/s, sprint 6 m/s, interact range 2.5 m. No complex climbing, combat weapons, ragdoll network simulation or free physics carrying. Carry/drag uses explicit constrained interactions with host validation. Preserve grounded movement and camera collision where relevant.

Bind WASD movement, mouse look, E interact, Shift sprint, 1–3 select slot, Q use item, G drop, arrows rhythm, V push-to-talk when available, Tab objective/map, Escape menu. Do not reuse movement keys as rhythm inputs by default. Restrict movement during a challenge but always allow cancel/leave with a visible outcome. Remapping must update prompts.

HUD: objective/progress, remaining time, cash, stock/slots, active effect icons/timers, local suspicion indicator with state/reason, police stop/evidence status, nearby interaction prompt, teammate life states and voice status. Rhythm lane occupies the lower portion of the screen and leaves the encounter visible. No all-knowing map of NPC intentions.

Menus: Host, Join by address/port, display name, Ready, Leave; host starts with 2–8 ready players. A debug-only bypass permits one-client testing. The player host counts toward eight: the requirement is not eight clients plus a ninth host. Reject a ninth participant with a clear reason.

## 6. State ownership and data contracts

### Authority rules

The host validates movement constraints, commands, NPC sight, economic transfers, suspicion, arrests, damage, inventory, mission credit, selected dialogue and active effect IDs. Clients capture input and render approved state. Local effects cannot create loot, move NPC colliders or change note timestamps.

Use stable application IDs distinct from temporary network object/client IDs. Resolve sender identity from the connection; do not trust an arbitrary `PlayerId` in an RPC. Initial LAN identity can be a session token issued by the host; record that it is not Steam authentication. Bind reconnect identity to a retained session token and restore the same player once.

| Record | Required fields |
| --- | --- |
| `RoundState` | Schema version, round ID, seed, lifecycle, tick/elapsed time, duration, mission, roster, entities, transaction sequence |
| `PlayerState` | Stable ID, connection binding, position, life state, cash, inventory, impairment instances, recovery count, current interaction ID |
| `MissionDefinition` | ID, family, map ID, duration seconds, objective markers, extraction rule, minimum players |
| `ItemDefinition` | Stable ID, category, price, stack limit, charges, equip/use context, setup/duration/range, cancellation rules, effect ID |
| `EffectDefinition` | ID, duration, chart-presentation parameters, comfort variant, audio variant, optional host movement modifier |
| `InteractionState` | ID, participants, NPC/target, context, chart ID/seed, start tick, reservation, status, chosen dialogue IDs |
| `NpcState` | Stable ID, type, position, target, suspicion by observed player, evidence, current state/deadlines |
| `CommandEnvelope` | Command ID, round ID, sender sequence, expected entity revision, payload |
| `CommandResult` | Same command ID, accepted/rejected, reason, committed sequence and affected revisions |
| `ProfileData` | Schema version, cosmetics/content unlock IDs, seen-line IDs by content version, settings |

Commands include `TryInteract`, `SubmitRhythmInput`, `Buy`, `Use`, `Transfer`, `Drop`, `Consume`, `StartSale`, `Deposit`, `Withdraw`, `BeginRevival`, `BeginRelease`, `Extract`. Naming is flexible; behavior is not.

Deduplicate commands by stable ID within the round. Validate distance, life state, target state, owned inventory, price and revision. Economic commits must atomically change all balances/reservations and emit quest credit once. Replayed requests return their original result. Disconnect/cancel must resolve an active reservation once. Bound command payload sizes and rate limits; reject non-finite positions, impossible sequence numbers and invalid enum IDs.

Use reliable messages for transactions, life-state changes and encounter outcomes; use the network package's appropriate snapshot/interpolation mechanisms for movement. Do not send cosmetic dancer transforms or every trail particle. Host movement can be implemented simply first; add local prediction only if measured input latency requires it.

## 7. Mission and lifecycle implementation

Lifecycle: `Lobby → Shopping → Loading → Playing → Results → Shopping`. Start the five-minute clock after clients acknowledge the map is ready. All mission durations are data, never hardcoded into scoring or networking. Record failure reasons. Re-entering Results cannot pay rewards twice.

Rescue mission: find a conspicuous NPC friend at one of three seeded reachable markers, complete a short interaction to recruit them, then escort them to extraction. The friend follows the designated living player at walking speed; another player can take over. If leader dies/disconnects, friend waits safely and remains recoverable. The objective requires the NPC at extraction plus at least one living player completing a 3-second extraction interaction. A spirit does not count as a survivor. Deadline expiry without valid extraction fails the run.

Configure retrieve and deliver definitions with schema-valid markers for later implementation. Label them unavailable in mission selection until their actual objective logic and acceptance tests pass. Do not implement fake completions for selectable modes.

Side quest: team gross sales reach $50. Purchases and transfers do not count. Show quest progress and cash separately. Add a legitimate repeatable low-risk lost-property return that pays $5 once per generated task. Keep its task source reachable and available to living players with no money or stock.

Between rounds, actual inventory/cash resets to the configured start, while unlocks and dialogue history persist. Award one cosmetic after the first successful run. Initially all prototype equipment is available in its vendor pools so progression cannot block testing. More unlocks are sidegrades, not stat bonuses.

## 8. Rhythm, dialogue and suspicion

### Timing

Represent charts as immutable note IDs, required input and hit offsets relative to encounter start. Use 120 BPM tap charts: 8 notes for a basic check, 16 for an accusation and repeated 8-note phrases for performances. Chart/music length must cover all notes and the final judgement window.

Host schedules an encounter sufficiently in the future (start with 2 seconds) after participants are ready. Estimate server-clock offset using the selected network library or a documented clock sync process. Map the future host start time to each client's local DSP clock and schedule music with `AudioSource.PlayScheduled`. `AudioSettings.dspTime` is local to each device; it is not a shared network clock. Input timestamps, audio calibration offsets and server times must have documented units and conversion signs.

Judge the original input timestamp relative to the intended hit time, not packet arrival. Perfect ≤80 ms absolute error gives 1 point; good ≤150 ms gives 0.6; otherwise 0. Consume each note at most once, nearest eligible matching input with deterministic tie-breaking. Penalize unmatched extra presses by 0.1 normalized points, floored at zero. Do not penalize key-release events. The host recomputes results and rejects fabricated out-of-range timestamps; this is basic validation, not a claim of cheat-proof clients.

Hold final scoring for a configurable bounded network grace (start 0.75 s). Inputs beyond the supported timing envelope yield an explicit connection issue, not unlimited retroactive perfect hits. If clock drift exceeds a threshold before a chart, resync before starting. Do not retime an active chart silently.

Phrase score `S` is normalized to [0,1]. Baseline suspicion delta is `round(20 - 35*S)`: perfect play gives −15, total failure +20. Clamp observer suspicion to [0,100]. Apply results only to witnesses participating in that interaction or distraction coverage; no remote global threat reset. Emergency invalidation by detention/downing cancels the chart with a defined outcome and no duplicate award.

### Observer state machine — proposed tuning

| State | Entry and behavior | Exit |
| --- | --- | --- |
| Blending | Suspicion <25; ambient activity | Observed behavior raises suspicion |
| Watching | ≥25; turn/stare and show cue | Falls below 15 or reaches questioning |
| Questioning | ≥45 and available; approach and offer challenge | Successful check drops below 35; refusal or failures escalate |
| Accusing | ≥70; loud cue and 4-second warning | Strong assistance/escape lowers threat; reaches swarm condition |
| Swarming | ≥90 after accusation grace, or unresolved accusation after 8 seconds | Loss of target, strong distraction, rescue or target death |

Sight default: 12 m, 120-degree cone, occlusion check; hearing notices an authored event within its radius, not arbitrary speech semantics. Sample decisions at 5 Hz, staggered. Broadcast an accusation to nearby wooks within 10 m; only those hearing it acquire the shared target. Cosmetic dancers never deal damage.

Observed sprinting +4 per second; restricted-area presence +8 once per second; consumed intoxicant −15 once per accepting witness; ignore distant NPCs. Decay at 2/second after 5 seconds out of observation. Continuous accusations and pursuit prevent passive decay. Use cooldowns to avoid eight wooks initiating incompatible questions on one player. At most one primary challenge per player; additional NPCs observe/join that encounter.

### Dialogue content and history

Create at least 48 non-identical draft lines: 8 each for greeting, dance invitation, narc suspicion, sale, police stop and medical/release. Include good/awkward delivery text variants where relevant. Use original text, stable IDs, context/speaker/tone/difficulty tags, prerequisites, locale, content revision and `Draft`/`Approved` status. No runtime LLM calls. Development can show draft content; commercial content validation requires approval.

Host filters by valid context, then prefers unseen lines for the addressed player. Avoid simultaneous reuse within the group when an eligible alternative exists. Store a bounded 512-ID LRU per profile for the prototype; history expiration permits old repeats. Reserve a line while delivering; mark seen only on actual display acknowledgement, deduplicated by interaction/line ID. Preview never reserves or marks seen. Pool exhaustion selects least-recently-seen eligible fallback and logs why. It never blocks the encounter. Stable IDs survive text edits that preserve meaning; intentional semantic replacements get new IDs.

Use subtitles and visible awkward gestures in the first build. If dialogue audio is absent, report it rather than claim spoken intoxication effects were heard. When audio is supplied, fast/slow NPC or avatar delivery must not change the chart or encounter schedule. Do not transform real player voice automatically in the first slice.

## 9. Item, vendor and transaction implementation

Start cash $20 per player; three small slots; stock stacks to five; one bulky hand slot; map is a free HUD feature. A wristband uses a separate rescue-token slot to avoid inventory deadlock. Players must confirm voluntary item handoff; bulk tools and equipped poi occupy hands during use.

| ID | Starting price | Required functional behavior |
| --- | --- | --- |
| `stock_lsd` | $5/unit | Buy, pick up, accept, sell or consume; consumption starts LSD effect |
| `stock_mushrooms` | $5/unit | Same flow, mushrooms effect |
| `stage_pass` | $15 | One 30-second DJ takeover with scored phrases; spend on activation |
| `confetti` | $5 | One charge; distract nearby idle/questioning wooks for 4 seconds; not active swarm immunity |
| `merch_bag` | $5 | Equipment hides carried stock from casual police visibility; witnessed trades/search remain effective |
| `stash_box` | $10 | Place once in an allowed location; 2.5 m interaction to deposit/withdraw cash and items |
| `medical_voucher` | $5 | At tent, remove the most recently acquired impairment after 3 seconds; not revival |
| `map` | Free | Show known landmarks, teammates only under the defined visibility rule, and objective information |
| `wristband` | Earned | Spawn once on death; retrieve and deliver for medical revival |
| `poi_practice` | $5 | Dance-only window multiplier 1.15, capped at 175 ms good window |
| `poi_led` | $10 | Normal timing; multiply negative suspicion delta by 1.25 on successful performance phrases |

This resolves “standard stock” into two prototype stock IDs. The map and wristband are not random vendor stock. Other unimplemented items must not appear as purchasable functional equipment.

Seed one offer list at Shopping start from run ID and vendor revision. Guarantee both basic stocks, medical voucher and practice poi; choose three distinct extras from stage pass, confetti, merch bag, stash box and LED poi. Show all seven offers with price, context, description and Preview. Stable until the next Shopping phase. Ensure stage/LED tests are directly selectable in debug QA even when absent from a particular seed; production offers remain seeded. Catalog category pools and guarantees are data.

Preview every offered item using the real effect/rule code in a separate isolated context, with a preview wallet/inventory, fixed targets and local dummy state. Do not simply apply to the real player and undo it later. Preview has no path to live economic transactions, quest credit, dialogue history, profile saves or authoritative NPCs. Clear it on close/disconnect/scene change. Demonstrate setup, targeting, lifetime and limits; mark items “Preview — not owned.”

Sales reserve one stock unit for the interaction. Score ≥0.75 pays $10, score 0.4–0.749 pays $5, score below 0.4 rejects with no payout. A rejected normal sale returns reserved stock; a witnessed bust seizes it. The host settles sale or bust, never both, in a deterministic event order. Quest credit equals committed gross payout. NPC buyers accept at most two completed sales per run. No negative inventory or cash under concurrent buy/use/drop/transfer/sale requests.

Consuming a third active effect is rejected before spending stock, with an explanation. Reusing an already active effect is rejected in this prototype. Medical removal updates both host state and local presentation immediately. Pricing and durations are configurable assets, not scattered literals.

## 10. Intoxicant presentation contracts

The names below describe fictional game behavior, not physiological claims. Implement LSD and mushrooms as the selectable first-build pair and create schema-valid presets/contract tests for the rest. Do not advertise an unfinished preset as a completed playable effect.

All active effect instances contain host-issued instance ID, effect ID, source command, start tick and duration (initially 60 seconds). Maximum two distinct effects. True rhythm targets remain fixed; effect code has read-only chart access. Freeze chart-presentation modifiers per phrase; transition at phrase boundaries while world effects can fade in immediately.

To change apparent note speed without changing its hit time, vary **visibility lead time** and path shape. For hit time `H`, current synchronized local chart time `t`, and lead `L`, draw based on normalized progress `u = clamp(1 - (H-t)/L, 0, 1)`. The visible path must meet the same receptor at `u=1`. For a curved path, add a bounded offset multiplied by `(1-u)` so the offset vanishes at the target. Never implement intoxication by multiplying simulation time, changing chart BPM, or incrementing arrows by unrelated frame velocity.

| Effect | Presentation contract | Initial preset / implementation boundary |
| --- | --- | --- |
| LSD | Irregular colorful trails and swirling notes converging at receptors | 2 s lead; deterministic per-note wobble; low amplitude default; playable |
| Mushrooms | Gentle curved path, pulse and distinguishable afterimages; breathing scenery | 2 s lead; smooth periodic path; decorative ghost notes visually separate; playable |
| Ecstasy | Faster apparent notes, brighter stage emphasis, rapid scripted delivery | 1 s lead; clamped brightness; actual timing unchanged; preset and contract test |
| Ketamine | Slower approach and drawn-out scripted delivery | 3 s lead, clear beat indicator; actual timing unchanged; preset and contract test |
| Alcohol | Spinning notes, optional mild view roll, scripted slur/hiccup cues | Fixed receptor; camera motion off by default; preset and contract test |
| Weed | Narrower vertical view, slower walk, local reggae-style arrangement | Letterbox/soft vignette with HUD intact; host-authorized speed ×0.8; same-BPM alternate audio arrangement; preset and contract test |

Source clarification: weed's reduced walking speed is an explicit **host-authorized movement modifier**. It cannot be implemented solely by making a client's camera appear slower. Physics rules, colliders, other players' input and rhythm timestamps remain unchanged. Record this interpretation in `IMPLEMENTATION_STATUS.md` as a resolved technical ambiguity, not a newly approved balance decision.

If two effects compete, use stable priority for the primary lane path: LSD > mushrooms > alcohol. Apply only one lead-time modifier, choosing the most recently acquired ecstasy/ketamine instance. Use one bounded brightness layer, one camera layer and one audio arrangement. Weed speed applies once. Never sum multiple camera oscillations or repeatedly rescale delta time.

Comfort controls: reduced motion, brightness clamp, camera motion off, fixed-receptor/high-contrast mode, audio cue preservation and vignette alternative. Remove aggressive motion without claiming a mathematical equality of difficulty; comfort variants are subject to playtesting. The beat indicator and required input glyph must remain identifiable. Reset all material/camera/audio overrides after removal, death or round reset.

For weed music, provide an original same-tempo offbeat arrangement or a clearly labeled prototype substitute; use a synchronized crossfade at a bar boundary. Do not pitch-shift the entire track and claim tempo preservation. Final songs, voice acting and commissioned assets are independent content tasks.

## 11. Swarms, detention, spirits and recovery

### Lethal swarm

Use up to six active attackers on a target, others visibly converge without overlapping attack damage. Give the targeted player a warning and clear chase audio. At 1.5 m an attacker winds up 0.75 s, then deals 20 damage if distance/line of sight still qualify. Per-attacker cooldown 2 s; host arbitrates hits. Start health 100. At zero enter Downed for 10 s with a visible countdown. An uninterrupted swarm finishes the target when the downed timer expires. Do not instantly kill on a suspicion threshold.

Dragging slows the helper and keeps the target out of attack range where possible. A living teammate can perform a 3-second rescue if no attacker is within 3 m; restore 40 health and short recovery grace. A strong DJ or coordinated poi event can redirect attackers for 5 seconds, giving time to drag/rescue. Remaining accusations resume after the effect unless line of sight is lost. Do not set all suspicion to zero.

### Police

Host line-of-sight evidence records witnessed sales and visible unbagged stock. Mere wook suspicion is insufficient. Approach, stop and offer dialogue for uncertain evidence. A confirmed witnessed deal leads to detention after a warning; good rhythm cannot erase it. Detain once: seize stock (including reserved sale stock), take `min(cash, $10)`, move to holding area and preserve non-contraband gear.

A detained player can perform processing dialogue to reduce release interaction time but cannot immediately free themselves. A teammate releases them with a 3-second desk interaction and $10, or performs a short free release task involving a lost-property marker. This supplies a recovery path for a broke pair. Both paths need reachable living teammates. A prisoner retains local movement in the holding area and nearby voice if available. Do not allow an unrelated clipboard to cancel an active arrest.

### Spirit and revival

After death, spawn exactly one wristband and switch that player to Spirit. Implement a separate distorted copy of the compact map with no hidden live objectives or NPC/evidence replication. A ghost can move and trigger a cooldown-limited harmless memorial chime near the medical landmark; no economic or quest rewards. Ghost visuals are non-colliding. Limited help means an authored signal, not invisible reconnaissance of the whole live map.

Prototype voice rule: spirits hear other nearby spirits, living/detained players hear nearby living/detained players; no cross-life-state voice except an explicit optional medical-tent interaction added later. Clearly show the channel state. External chat cannot be prevented; do not claim it can.

A living teammate delivers the wristband and pays $10 or completes a 10-second free recovery task. Revive at the tent with 40 health, removed impairments and no restored seized/lost cash. Cap at two revivals per player per round initially. Consume the wristband atomically. End the run if no living, non-detained player can perform any remaining rescue and no authored recovery path exists; spirit movement alone does not prevent that failure condition.

## 12. DJ and poi cooperation

Stage pass activation requires stage-console range, Alive state, no other stage owner and available pass. Schedule a 30-second rhythm routine. Each successful phrase with score ≥0.75 sends a coverage event to wooks, reducing observed player suspicion by up to 10 per phrase and redirecting attention. Main-stage coverage is the whole compact map; later maps may define broadcast zones. Police evidence is unaffected. A qualifying strong phrase can interrupt pursuit/downed finishing for the 5-second rescue opening.

Practice and LED poi are reusable. Ordinary use is a local eight-note dance routine with movement limited. A second living player within 4 m can join a complementary lane after a ready countdown. Each scores independently. When both achieve ≥0.75 in the same phrase, emit a strong local event within 8 m, allowing a swarm rescue without a stage pass. One participant can maintain ordinary attention while the other misses. Cancel/reduce support on departure/downing/disconnect. Cap effects at two contributors in the prototype, with a data cap for three later.

One dominant distraction affects an NPC at a time; supporters add capped duration/strength. Trigger cooldown prevents refresh every frame. No automatic benefit from simply equipping poi, and no poi timing bonus during sales or police dialogue.

## 13. Voice, internet sessions and host migration

### Real integration boundary

Implement the configured provider behind `IVoiceSession` operations for join/leave, local mute, remote mute and positional updates. Do not send voice over the same reliable gameplay command stream. Proposed falloff: full near 2 m, fade to inaudible at 18 m. Configure using supported provider APIs; report if occlusion or separate spirit channels are unavailable. Microphone mute and accessibility controls must remain accessible during a challenge.

If no provider is configured, show “Voice unavailable — provider not configured”; disable PTT and mark the integration blocked. A fake waveform, local microphone loopback or prerecorded clip is not multiplayer voice. Keep the prototype build runnable and continue M0–M8 independent work. Record the exact account/project/API capability needed to finish, without requesting purchases implicitly.

Direct Unity Transport connections support the controlled development topology chosen here. They do not prove NAT traversal, Steam invites or public internet reachability. Document the exact tested topology. Steam integration is a separate gate requiring the actual configured Steamworks environment and a compatible transport adapter. Do not replace the chosen authoritative topology with distributed authority just to obtain a similarly named session feature.

### Migration-ready state required now

Define a versioned `RoundSnapshot` with stable entity/player IDs, mission timer/progress, seeds and RNG state, spawned/dropped items, wallets, inventories/reservations, committed transaction IDs/results, NPC states/evidence, life states, wristbands/revival counts, active effects, vendor offers and player dialogue histories. Store remaining durations or transferable simulation timestamps, never raw device DSP timestamps or Unity scene references.

Write and test snapshot→restore in a fresh scene. Rebind regenerated network entities to application IDs. Reconcile exactly-once transaction results before accepting new commands. This demonstrates serialization only.

### Mandatory release migration gate

Implement separately after the compact loop is stable: discover eligible survivors through the selected session service, replicate usable checkpoints/commit information to a standby, select one new authority with an epoch, establish the new endpoint, reconnect participants, restore state, remap network IDs and reschedule audio/charts. Reject stale-epoch commands. Preserve permanent profiles separately from transient round state.

Pause the round timer while migrating. Reschedule interrupted rhythm phrases with a countdown and no duplicate scoring or penalties. An old host that returns must join as a client and cannot continue as a second authority. Failed election or unavailable state must end clearly with a reason, not hang indefinitely. Test host quit/crash during sale settlement, downed countdown, detention, DJ, revival and extraction. A passing snapshot unit test cannot mark this release gate complete.

## 14. Debug instrumentation and evidence

Use `#if UNITY_EDITOR || DEVELOPMENT_BUILD` around diagnostics implementation and registration. Optional development callsites may use conditional methods; ensure argument construction and sensitive data capture also disappear from release. Diagnostics include structured events with round ID, tick, command/interaction/entity ID, action, before/after and reason. No recorded voice, tokens, credentials or persistent personal identifiers in logs.

Provide debug overlays for suspicion/evidence, AI target/state, rhythm input error and sync estimate, effect stack, wallet/transaction sequence, dialogue selection/fallback, preview context ID, NPC count, RTT/jitter and CPU/GPU frame timing. Log transitions, not every frame of every entity.

Debug scenario launcher: `rhythm_baseline`, `sale_bust_race`, `swarm_rescue`, `broke_pair`, `spirit_revival`, `vendor_preview`, `dialogue_exhaustion`, `eight_client_stress`, `snapshot_restore`. Scenarios must call ordinary systems; shortcuts may place actors but cannot substitute success flags for real interactions.

`IMPLEMENTATION_STATUS.md` records each gate as `NOT_STARTED`, `IN_PROGRESS`, `IMPLEMENTED_NOT_TESTED`, `PASS`, `FAIL` or `BLOCKED`, with command/result/evidence path and next action. Record observed limits. Preserve it across context compaction; reread it before continuing rather than regenerating the project.

## 15. Execution milestones — continue without routine approvals

| Milestone | Build and verify before continuing |
| --- | --- |
| M0 Environment | Resolve and freeze toolchain; scaffold project; first editor compile; truthful capability inventory |
| M1 Native foundation | Generate map/scenes, menus, input and player; one host plus one client move and interact; produce first native build |
| M2 Rules and rhythm | Domain command validation, charts/DSP clock bridge, dialogue selection/history, suspicion; tests for scoring and duplicate commands |
| M3 Economy and previews | Eleven item definitions; offers, wallet/stock/stash/sales/quest credit; isolated functional previews and broke-player income |
| M4 Danger and rescue | Police evidence/detention/release; wook pursuit/downing/death; spirit map, wristband and revival; actual group rescue flow |
| M5 Performances/effects | LSD/mushrooms, comfort settings, practice/LED poi and DJ; contract fixtures for remaining four effects; configured voice slice |
| M6 Complete round | Five-minute rescue/extraction/failure/results/reset, profile cosmetic/history persistence and reconnect identity |
| M7 Multiplayer validation | One host plus seven clients; simultaneous interactions; injected network impairment; snapshot roundtrip; no real-human test claims from bots |
| M8 Handoff | Fix observed defects, export development and release builds, verify release diagnostics absent, finalize runbook and evidence ledger |

Compile after each coherent change, run relevant tests and fix regressions before adding breadth. Keep commits or equivalent recoverable checkpoints at working milestones. Do not stop after a plan, scene screenshot or green unit test if a safe relevant next step remains. If a failure repeats unchanged, diagnose the evidence rather than rerun blindly. Do not fabricate passing test output.

Do not start the remaining catalog, enormous festival maps or host migration implementation before the playable loop unless M0 finds a topology decision that must be resolved first. Preserve extensibility through stable IDs and state boundaries, not speculative framework code.

## 16. Required verification

### Rule and integration tests

| ID | Given / action | Required result |
| --- | --- | --- |
| T01 Capacity | One host player, seven remote players; ninth joins | Eight supported; ninth rejected without disrupting round |
| T02 Grade | Known inputs at 0/80/81/150/151 ms; duplicate note input | Exact boundary grades; one award per note |
| T03 Network timing | Same logical inputs with 0/100/200 ms RTT, 30 ms jitter and 1% loss within configured grace | Equal grades; late-envelope behavior explicit; no silent clock shift |
| T04 Effects | Same chart through both playable effects and the four remaining effect contract fixtures | Identical hit offsets/order/grades; paths converge at receptors; fixture coverage does not count as finished effect art/audio |
| T05 Stacking | Activate two effects then consume a third | Third rejected with inventory unchanged; removal restores presentation |
| T06 Race | Sale success and police bust compete; resend commands | Exactly one settlement; conserved stock/cash and quest credit |
| T07 Observer | Fail a check in one zone | Nearby witnesses respond; remote unrelated wooks do not gain suspicion |
| T08 Rescue | Swarm downs target; helpers complete paired poi or DJ | Readable interruption; ordinary drag/rescue can succeed before death |
| T09 Life state | Die, collect band, revive; retry revival RPC | One band, one payment/task, one revival; no duplicate body/inventory |
| T10 Broke pair | Two players, one detained/dead, neither has money | Living friend can perform free release/revival path; otherwise explicit loss |
| T11 Preview | Preview every offer then cancel, disconnect or start round | Cash/items/quest/history/profile/live NPCs unchanged |
| T12 Vendor | Generate multiple fixed seeds | Stable offers per phase; limited distinct selection; guarantees hold |
| T13 Dialogue | Repeat same eligible context, reconnect, exhaust pool | Unseen preferred; history retained; fallback works; no reward tied to repeat selection |
| T14 Snapshot | Serialize round during reservation/effects/revival; restore fresh | Stable IDs, timers, balances, dedupe and histories preserved; no hidden double commit |
| T15 Completion | Deliver friend and one survivor; retry extraction; timer expiry | Correct one-time win/reward or loss; new round fully resets transient state |
| T16 Profile | Earn cosmetic, quit/reload, load older/invalid profile | Unlock persists; no stat advantage; invalid data falls back safely |
| T17 Build | Run native application with editor closed | Lobby→round→results→quit works; no missing scripts/assets |
| T18 Release | Attempt diagnostic overlay/command in non-development build | Diagnostic code/registration unavailable; normal error UI remains |
| T19 Voice | Real microphones on two machines, near/far/mute/life-state transition | Actual remote audio follows rules; mark blocked if provider or hardware absent |

Run full-group round/reset three times with one host and seven connected clients where resources permit. Multiple local processes establish transport/state behavior, not performance across eight physical PCs. Scripted bots can stress systems but are not eight-person playtesting. Record client count, number of machines and exact tests executed.

Proposed performance target: 60 FPS at 1080p on reference hardware to be named in the runbook. Capture hardware, graphics settings, interactive/cosmetic NPC counts and measured frame times. Do not invent minimum specifications. Profile the host with all eight players active, not an empty single-player scene.

Human evaluation remains necessary: at least two outside friend groups should understand why someone was swarmed, find rescue options, distinguish LSD/mushrooms, use item previews and want another round. Leave these checks `IMPLEMENTED_NOT_TESTED` with the reason “human playtest pending” until humans actually perform them.

### Reproducible command tooling

Create scripts that resolve the editor from an explicit argument or recorded installation and invoke documented Unity CLI options. Implement the project-specific static methods named in section 4; those names are requested deliverables, not existing Unity APIs. The generated runbook must contain exact commands used for:

1. Opening/importing the project and running `EnsureGeneratedContent`.
2. Validation and edit-mode tests.
3. Play-mode tests, with graphics/audio where required.
4. Development and release export.
5. Launching host plus one client and, separately, host plus seven clients with unique local profiles/ports as needed.
6. Reproducing each tested scenario and collecting logs/results.

Respect Unity's asynchronous test runner; wait for result XML and process completion. `-nographics` runs cannot verify shaders, rendering, audible rhythm alignment or real microphones. Do not use identical mutable project/profile directories for concurrent editor/player processes. If Windows support is unavailable, optionally export the host platform for inspection and mark the required Windows build blocked.

## 17. Definition of done and final agent response

`PROTOTYPE_COMPLETE` requires the M0–M8 mandatory prototype behavior and applicable T01–T18 tests, a compiled native build, the full rescue round and its failures, functional previews and recovery, and honest evidence for every claimed result. T01 requires one host and seven actual remote-client processes; a two-process test is an early milestone only. Where eight-client, graphics or required native-build tests cannot run, label `PROTOTYPE_IMPLEMENTED — VALIDATION_INCOMPLETE`; never promote it to verified eight-player support. T19 may remain `BLOCKED` with explicit integration impact, and human/multiple-home validation may remain pending, but neither situation permits a release-ready claim. The final report must distinguish local multi-process verification from physical-machine and human playtests.

Every requested feature needs an implementation path and evidence or a named status. `TODO`, `NotImplementedException`, decorative UI and disabled interactions cannot fulfill a functional first-build requirement. Stubs are acceptable only at explicitly external/deferred boundaries and must be listed.

Final response must include:

- Source project path and exact native executable/build directory, verified to exist.
- Quickest Host/Join instructions and controls.
- Feature/gate results and number/type of clients actually tested.
- Tests run, failures remaining, graphics/audio review performed and measured performance if available.
- Integration blockers and release requirements still outstanding.
- Next reproducible action, with no unsupported claim of a finished Steam product.

Do not overwrite or reinterpret the design spec to make the implementation look complete. Document proposed decisions, deviations and evidence in the implementation status file.

## 18. Preserved backlog and traceability

These remain product work, not forgotten features. Later catalog: portable speaker, bubble machine, glowstick bundle, mascot head, vendor trolley, mystery stock, landmark stickers, disposable camera, buddy tether, ear defenders, lost-property ticket, crew uniform, equipment work order and complaint clipboard. Later poi: ribbon, glow, programmable pixel, fire and double-ended. Require real item previews whenever each becomes purchasable.

| Source spec section | Covered by this handoff | Still requires later decisions/content |
| --- | --- | --- |
| 1 Product | 2, 5 | Branding, final platform list |
| 2 Missions | 7, 15–16 | Full retrieve/deliver logic, larger events and duration bands |
| 3 Danger/recovery | 8, 11–12 | Final revive limits and balance |
| 4 Rhythm/effects/dialogue | 8, 10, 16 | Finished remaining four effects, large reviewed content and final audio |
| 5 Economy/vendor/DJ | 9, 12 | Final prices, randomization and launch pools |
| 6–7 Items | 9, 18 | Remaining catalog and final art |
| 8 Poi | 12, 18 | Five additional poi types and three-person tuning |
| 9 Technical | 3–6, 13–14 | Exact provider setup, Steam internet sessions and real host migration |
| 10 Prototype | 15–17 | Eight humans across different homes, comfort and fun testing |
| 11 Review/commercial | 2, 13, 18 | Hardware targets, release strategy, storefront, content clearance and support |

Risk assessment for this handoff: **8/10 likelihood of substantial integration rework if the entire commercial scope is attempted in one uninterrupted build**, an engineering estimate rather than a measured probability. The largest risks are eight-player timing, reliable migration, voice/session integration and the amount of authored content. Regression risk against an existing game cannot be rated until M0 inspects that codebase. In a new project the concern is implementation and validation risk, not breaking absent functionality.

## 19. Primary technical references

Consulted 8 September 2026. These establish relevant engine facilities; they do not certify this proposed game architecture or select a compatible package lockfile for an unseen machine.

- [Unity Netcode for GameObjects repository](https://github.com/Unity-Technologies/com.unity.netcode.gameobjects): official package and Unity-version compatibility guidance. Use the installed version's documentation.
- [Unity Transport documentation](https://docs.unity3d.com/Packages/com.unity.transport@6.5/manual/index.html): transport documentation; verify the actual resolved package and editor compatibility instead of trusting a page's `latest` redirect.
- [AudioSource.PlayScheduled, Unity 6.0](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.PlayScheduled.html): scheduling playback against the local DSP timeline.
- [AudioSettings.dspTime](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/AudioSettings-dspTime.html): sample-based local audio clock.
- [Unity session-host migration](https://docs.unity.com/en-us/mps-sdk/session-host-migration): ownership/election and data migration must be evaluated separately for the chosen topology.

The source design v1.3 and this file supply the game requirements. Technical implementation defaults in this handoff are proposals chosen for the first autonomous attempt; verify package APIs before coding.
