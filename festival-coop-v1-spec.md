# Festival Co-op

## Game design and technical specification

Version: 1.1 | Date: 8 September 2026 | Status: Updated review draft

Prepared for Matt and the development team. Project name and branding remain undecided.

# 1. Product definition

## The game in one paragraph

A standalone 2–8 player co-op festival game in which friends must blend into a crowd of “wooks” that become lethal zombie-like swarms when they identify a narc. Players dance and converse through rhythm-based key sequences, use fictional substances to gain acceptance at the cost of distorted perception, trade for money, evade police and spend resources on performances and distractions to rescue one another.

## Design promise

The tone is comedy with genuine danger. Most social failures should be funny and legible; accusation, pursuit, arrest and swarming should create real pressure. The visual direction is stylized low-poly, emphasizing readable silhouettes, expressive animation and dense-crowd performance.

Looking normal makes you safer. The things that help you look normal can make you worse at acting normal. Friends can compensate for your mistakes, but doing so puts their own cover, money and time at risk.

## Status and interpretation

CORE identifies a confirmed product requirement. PROPOSED identifies a concrete design default that still needs playtesting or review. OPEN identifies an unresolved decision. All numeric timings, prices, thresholds and scope allocations remain provisional unless explicitly marked otherwise. This specification defines the product direction and first prototype; it does not claim that the game or an integration has been built.

The supported lobby range is **2–8 players**, with a full eight-player online session required for performance and network acceptance. Solo play and more than eight players are outside v1.

# 2. Round structure and objectives

## Core experience

CORE: multiplayer social stealth, music-festival setting, rhythmic dance and conversation checks, helpful purchasable items, substance pickup/acceptance/trading, cops and lethal wook swarms. Players must have useful ways to distract an NPC and free a teammate from trouble.

## Rotating missions

CORE: each 15–20 minute run selects a group mission from the **rescue, retrieve, or deliver** families. The first three missions are:

1. **Rescue:** find a missing friend and escort them to the departing shuttle.
2. **Retrieve:** recover essential belongings from a restricted or high-risk festival zone and extract them.
3. **Deliver:** transport fragile, conspicuous, or suspicious equipment across the festival to its destination.

Begin with one compact festival map and authored objective locations selected from a small pool. The first playable prototype uses a five-minute rescue mission. The clock creates pressure to spend resources and leave safety. Players know the departure deadline and extraction location early; objectives become more specific through exploration.

## Round flow

Briefing and loadout: join a private lobby, choose appearance, inspect the mission and purchase a small starting loadout. Enter together in a low-pressure arrival area.

Explore and earn: split into flexible groups, find clues, perform, sell stock or complete legitimate jobs. Nearby crowd behavior teaches the blending rules.

Escalate and rescue: interactions expose players, impairments complicate navigation and police interrupt risky income. Spend money or deploy equipment to recover teammates and maintain progress.

Extract and debrief: reach the shuttle with the mission target. A team wins when it completes the primary objective and at least one player extracts. Additional rescued and extracted teammates improve the team reward; stranded or dead players reduce it. Show rescued teammates, mission completion and shared highlights without rewarding griefing.

## Side quests and recovery income

CORE example: make $50 selling to wooks. PROPOSED alternatives: return lost property, deliver stage equipment, collect litter, complete a successful DJ set or recover a detained teammate. Each objective records its scope as individual or team; the first selling quest is team-shared.

Completed sales advance gross-sales progress once. Cash carried is a separate balance; confiscation does not undo quest credit. Reject repeated event IDs, player-to-player transfers and buyback loops as quest income. Legitimate jobs must remain available after the group loses stock or money.

## Loss and replay

A run fails when the departure deadline expires without a valid extraction, or everyone is dead or detained with no available recovery action. Vary routes, active patrols, objective placements and encounters before investing in procedural terrain. Between runs, players retain cosmetics and unlock additional missions, items, poi variants and outfits. Persistent power upgrades are outside v1 so experienced players do not gain a mechanical advantage over new friends.

# 3. Wooks, police and consequences

## Separate suspicion, attention and heat

CORE: high wook threat can trigger a lethal swarm; cops can bust players selling substances. PROPOSED: track wook suspicion by observer/group, attention as the current target or task, and police heat/evidence separately. A UI threat indicator summarizes nearby danger; there is no omniscient festival-wide narc flag.

| **State**   | **Readable behavior**             | **Player opportunity**                |
|-------------|-----------------------------------|---------------------------------------|
| Blending    | Wooks dance and socialize         | Follow crowd cues; pursue objectives  |
| Watching    | Stares, turns, pauses             | Correct behavior or move calmly       |
| Questioning | Approach and rhythm prompt        | Perform; friend can cover or distract |
| Accusing    | Clear callout and gathering crowd | Brief rescue/escape window            |
| Swarming    | Visible pursuit and attacks       | Strong distraction, escape or death   |

Proposed suspicion sources: observed failed performances, running against crowd flow, conspicuous interference or restricted-area entry. Decay requires time without fresh evidence. Use separate enter/exit thresholds and a grace interval to prevent state flicker. Death requires successful attacks, not a meter instantly reaching its maximum.

## Distraction rules

A distraction changes attention first. It can enable suspicion decay while the target is no longer observed. Ordinary effects redirect idle or questioning wooks; only designated strong effects interrupt pursuit. Observers retain short-term memory, so hiding behind one repeated distraction is not permanent immunity.

## Police behavior

Patrol, observe, approach, stop, detain and return to patrol. Stops need a witnessed action, visible stock or another authored evidence trigger. Rhythm dialogue can resolve uncertainty; a perfect score cannot erase a clearly witnessed transaction. Police do not inherit wook suspicion automatically.

CORE arrest outcome: police confiscate carried contraband, apply a defined cash penalty and place the player in temporary detention. The detained player remains active through processing dialogue. Teammates can free them through a distraction, bail payment, or authored release objective. A map prop that occupies an idle officer cannot cancel an arrest already in progress. Keep searches abstract and fictional.

## Death and recovery

CORE: swarms can kill. A brief downed state precedes death, with interruption and drag/rescue options. After death, the player becomes a playable spirit in a distorted local festival layer while teammates attempt revival. A teammate must recover the dead player’s wristband and complete or pay for a medical-tent revival task. Define a finite recovery limit per run; costs and limits require playtesting.

Spirits can explore and provide limited help, but cannot manipulate authoritative items, block living characters, scout concealed objectives, or reveal hidden NPC information through game UI. Proximity voice for spirits requires an explicit range and audience rule during implementation.

# 4. Rhythm performance and impairment

## Shared challenge framework

CORE: DDR-style combinations of key presses resolve dancing and normal conversation. Better performance lowers wook threat; poor performance raises it. PROPOSED: four remappable directional inputs, short countdown, visible beat lane and graded timing. Start with tap notes; holds and simultaneous presses are later content.

Conversations map timing to delivery: successful inputs produce convincing lines; misses create awkward pauses or wrong reactions audible to nearby players. No speech recognition is required. Keep the world visible so friends can see the exchange and intervene.

PROPOSED scoring seed: perfect within ±80 ms, good within ±150 ms, miss outside the window. Match each input to one eligible note; consume it once. Extra presses incur a small penalty to prevent button mashing. Aggregate normalized accuracy per short phrase, then apply bounded suspicion changes. These values are tuning candidates, not validated tolerances.

Give a visible recovery opportunity after mistakes. Basic dancing helps maintain cover; direct challenges are harder; police dialogue has its own outcome rules. Poi modifiers apply to dance/performance contexts, never automatically to police conversations.

## Substances as a deliberate trade-off

CORE: pick up substances, accept them from wooks or purchase them; consuming them lowers threat but alters player perception. PROPOSED: acceptance grants approval only from witnesses. Consuming stock is distinct from carrying or selling it. Use fictional names and bounded effect durations, with at most two active effects in the prototype.

| **Candidate effect** | **Gameplay consequence**           | **Friend can help by…**               |
|----------------------|------------------------------------|---------------------------------------|
| False signs          | Directions become unreliable       | Navigating and placing markers        |
| False faces          | Some NPCs resemble teammates       | Verifying identity and regrouping     |
| Echo stage           | Apparent sound direction changes   | Leading the route                     |
| Phantom follower     | A harmless pursuer appears locally | Checking the actual surroundings      |
| Decoy beat lane      | A recognizable false cue appears   | Coaching; real lane remains learnable |

## Fairness and comfort

Hallucinations change local presentation, not shared collision or the actual target of another player’s action. Keep scoring consistent and log the true note stream. Allow audio/video timing calibration, remapping, clear shapes as well as color, reduced motion and non-flashing equivalents. Comfort settings preserve the gameplay information disadvantage; no constant camera wobble or random control reversal in the prototype.

# 5. Economy and cooperative rescue

## Inventory and money - proposed defaults

Three small-item slots plus one hand-carried bulky item. Stock stacks have a fixed capacity; poi occupy one small slot when stowed and hands when performing. Bulky equipment limits movement or interactions. Items can be handed off; buying fails cleanly if inventory is full. Display price, charges, context and trade-off before purchase.

Players carry individual cash and can explicitly deposit or withdraw from a team stash. No automatic spending of another player’s wallet. Purchases, trades, withdrawals and penalties must be atomic, host-validated transactions. Prevent simultaneous grabs from duplicating items or stock.

## Selling loop

An interested wook initiates or accepts an offer. The seller commits one stock unit to a short transaction challenge. Strong performance completes the sale and improves local standing; weak performance changes payout or triggers suspicion. Prices and possible outcomes are visible enough to support informed choices. A police witness can interrupt and seize committed stock; settle the encounter exactly once.

Starter tuning proposal: $20 per player, basic stock at $5 and a successful sale at $10. Test against the $50 team side quest before finalizing. These are game-currency placeholders, not launch pricing or a monetization model. Cap safe repeat sales per buyer and refresh opportunities through movement or new encounters.

## DJ rescue sequence

CORE: buy access to the stage, become the DJ, distract wooks and lower everyone’s threat. PROPOSED: a stage pass unlocks one timed takeover. The DJ sustains a rhythm sequence; better phrases strengthen the effect. Main-stage broadcast reaches the festival, while smaller-stage effects are local. Police heat and existing evidence are unaffected.

The crowd turns toward the performance, enabling bounded suspicion reduction for all players in its coverage. Current accusers pause or redirect according to their state. When music ends, unresolved pursuit can resume. A second player may assist with stage equipment; this dependency is optional until the solo DJ loop works.

## Assistance and limits

Friends can cover a dialogue, perform nearby, draw an NPC away, guide an impaired teammate or retrieve a wristband. Assistance requires presence and time, and may attract suspicion toward the helper. Do not allow a strong rhythm player to resolve every remote challenge.

Effects do not multiply without limit: use one dominant distraction per observer and capped supporting bonuses. Repeated use in one location has diminishing effectiveness. Every crucial objective retains a route that does not require buying a particular item. Equipment creates temporary roles; permanent classes are outside the first prototype.

# 6. Item catalog - distraction and logistics

All item names and properties below are PROPOSED implementations of the agreed helpful-item system. P = prototype candidate; L = later backlog. Prices are relative tiers (low/medium/high), to be set after economy tests. No item is approved for launch merely by appearing here.

| **Item / tier**                    | **Function**                                                 | **Cost or limitation**                                    |
|------------------------------------|--------------------------------------------------------------|-----------------------------------------------------------|
| Stage pass • high • P              | Timed DJ takeover; crowd distraction and suspicion reduction | Consumed on activation; sustained performance required    |
| Confetti cannon • low • P          | Interrupt nearby questioning; brief attention shift          | Limited charges; conspicuous to cops; not a swarm reset   |
| Portable speaker • medium • L      | Place a temporary dance gathering                            | Bulky; setup time; gradual effect                         |
| Bubble machine • medium • L        | Redirect idle crowd movement                                 | Bulky; ineffective against an active swarm                |
| Glowstick bundle • low • L         | Giveaway performance draws nearby wooks                      | Consumes stock; giver completes a rhythm phrase           |
| Mascot head • medium • L           | Unlock a conspicuous crowd routine                           | Restricted view; teammate guidance useful                 |
| Official merch bag • low • P       | Hide stock from casual visual inspection                     | Still searchable; limited stock capacity                  |
| Lockable stash box • medium • P    | Store group cash and stock at a fixed place                  | Requires return trip; one shared risk concentration       |
| Vendor trolley • high • L          | Carry more stock and supplies                                | Bulky and conspicuous; narrow routes are difficult        |
| Mystery stock • medium • L         | Random fictional product assortment on opening               | Contents unknown until opened; no hidden lethal roll      |
| Standard fictional stock • low • P | Sell, hand off or consume                                    | One unit cannot be used for multiple outcomes             |
| Festival map • low • P             | Show stage, exit, vendors and medical tent                   | Local impairment can distort labels; share directions     |
| Landmark stickers • low • L        | Place persistent team navigation marks                       | Limited quantity; removable on round reset                |
| Disposable camera • low • L        | Share an authoritative reference image                       | Limited shots; exact hallucination behavior needs testing |

P catalog items remain candidates until the prototype subset is approved. A map may be issued as starter equipment instead of sold if navigation becomes a mandatory purchase. Mystery stock uses fictional game effects, not realistic product simulation.

# 7. Item catalog - support and security

| **Item / tier**                      | **Function**                                        | **Cost or limitation**                                     |
|--------------------------------------|-----------------------------------------------------|------------------------------------------------------------|
| Buddy tether • low • L               | Keep an impaired player near a guide                | Both players maneuver less freely; either can detach       |
| Ear defenders • low • L              | Reduce one fictional audio-distortion effect        | Muffle useful world cues; never universal immunity         |
| Medical tent voucher • medium • P    | Pay for removal of one impairment                   | Reach tent and complete a recovery interaction             |
| Recovery wristband • earned • P      | Dropped identifier used to revive a teammate        | Retrieve after death; medical recovery has a cash cost     |
| Lost-property claim ticket • low • L | Start an interaction occupying an idle desk officer | Desk locations only; must sustain a conversation           |
| Crew uniform • medium • L            | Reduce scrutiny in designated backstage areas       | Credentials can be checked; witnessed selling still counts |
| Equipment work order • medium • L    | Authorize a particular checkpoint delivery          | One player talks; another manages the equipment            |
| Complaint clipboard • low • L        | Occupy one idle officer with dialogue               | User is occupied too; failure redirects scrutiny           |

## Catalog consistency decisions

The recovery wristband is a teammate identifier, not a shop purchase; the associated revival service is purchased. The medical voucher treats fictional impairment and does not revive dead players. The merch bag is inventory equipment; the stash box is placed storage; the trolley is a movable container. These roles must remain distinct.

## Use and cancellation contract

Every item defines allowed user states, target types, cast/setup time, duration, range, charges, interruption behavior and refund rule. Validate before consuming. If setup is rejected, consume nothing. Once an effect starts, interruption does not automatically refund it. Show the reason an item cannot be used.

Confetti cannot erase evidence, the map cannot expose hidden NPC intent, uniforms cannot guarantee immunity and a stash cannot be accessed remotely. Fallen equipment remains recoverable when feasible. Functional inventory resets with each run; unlocked cosmetics, missions, items and poi variants remain available between runs without granting permanent stat advantages.

## Vendor placement

Proposed vendors: general festival supplies, performance equipment and fictional stock sellers. Place them in different but reachable zones, with at least one low-risk source of basic recovery equipment. High-value equipment requires travel or earned cash, not an irreversible economy trap.

# 8. Poi and performance equipment

CORE: multiple purchasable poi types help players perform. PROPOSED: reusable equipment with distinct benefits, not a linear upgrade ladder. Poi mainly affect dancing and intentional performances. All visual and scoring properties below are fictional balance choices.

| **Poi type**           | **Performance benefit**                             | **Trade-off / scope**                          |
|------------------------|-----------------------------------------------------|------------------------------------------------|
| Practice sock • P      | Slightly wider dance timing windows                 | Small audience; starter-friendly               |
| Ribbon • L             | Clearer advance pattern cues through trails         | Longer routines need open space                |
| LED • P                | Successful combos give stronger suspicion reduction | Visible to crowd and police                    |
| Glow • L               | Steady approval; smaller combo-break penalty        | Lower peak effect than LED                     |
| Programmable pixel • L | Completed sequence triggers a large spectacle       | Must finish the sequence for full benefit      |
| Fire • L               | Large audience and strong rescue potential          | Difficult pattern; mistakes draw security      |
| Double-ended • L       | Advanced patterns with larger combo rewards         | More complex inputs; optional expert equipment |

## Two performance modes

Maintain cover: use equipped poi in a dance check for the item’s modifier. Start a performance: deliberately begin a longer routine, occupy hands and limit movement while drawing an audience. Neither mode turns conversation checks into automatic successes.

## Join the flow

A nearby teammate can join a complementary lane. Each performer receives their own sequence and score; successful coordination expands the distraction up to a cap. Proposed cap: three mechanically contributing performers. Additional players may dance visually without stacking more power.

If one player misses, another can briefly retain audience attention. Do not copy one player’s successful inputs to everyone. When a helper disconnects or leaves, recompute the effect rather than abandoning the encounter.

## Equipment interaction rules

One active poi modifier per player. Timing aids have a maximum effective window; prevent stacking with other equipment into near-automatic success. Poi use a cosmetic trail system with reduced-effects equivalents. Fire poi initially has stylized failure feedback, not simulated spreading fire or friendly-fire damage.

The prototype uses practice and LED poi to compare forgiveness against stronger crowd impact. Unlock additional types only when testers can explain their differences and meaningfully choose between them.

# 9. Multiplayer and technical direction

## Selected stack and authority

CORE: native desktop game, Windows-first Steam target, built with Unity and C#. Validate the exact Unity version, networking library, Steam transport and voice solution through a pinned toolchain spike. Astra is a development assistant, not a required runtime service. The supporting packages are not selected by this document.

Use a player-hosted authoritative session for eight players. Host owns NPC decisions, real interactions, inventory, cash, quest credit, damage, arrests and outcomes. Clients own input capture and local presentation, including hallucinations. Clients request actions; the host validates and broadcasts accepted results. Local hallucinations never create authoritative loot or collision.

## Rhythm timing contract

Host assigns encounter ID, chart ID/seed, start time and modifiers. Clients schedule against an estimated shared clock, with local audio/video calibration. Capture timestamped inputs locally; host validates order, plausible timing, rate and final scoring. Never judge a hit by packet-arrival time alone. Define bounded clock-drift handling and rescheduling before accepting latency-heavy sessions.

Spectators and peers receive performance animations and phrase outcomes; they need not receive every visual note. Purchased timing modifiers are included in authoritative encounter state. Network tests must cover duplicate, delayed and out-of-order submissions.

## Crowds, audio and sessions

Separate cosmetic crowd dancers from interactive wooks. Limit and measure active observers, police and pursuit agents; choose the numeric crowd budget only after an eight-player performance test. Use interest-based updates and pooled visual effects. Avoid detailed synchronized crowd physics in the first prototype.

CORE: the soundtrack combines commercially cleared AI-generated songs with commissioned originals. Preserve generation inputs, service terms, contributor records, stems and final human edits for every shipped track. Every rhythm track needs an authored tempo map and stable local playback clock.

CORE: in-game proximity voice is a launch feature and part of stealth, comedy and rescue. Define distance falloff, occlusion, police/detention behavior, spirit channels, impairment effects, mute/block controls, reporting and privacy behavior. Do not record voice for diagnostics.

Proposed first-session rules: private invites; joining during a run deferred; disconnected inventory remains once in-world; reconnect restores the same identity without duplicating assets.

CORE for commercial release: host migration allows an active 15–20 minute run to continue when the host disconnects. Build authoritative state so it can be serialized, transferred and resumed. The first prototype may end on host loss while this architecture is validated, but host migration must pass fault-injection tests before release.

## Debug-build-only diagnostics

Instrument encounter IDs, timing offset/error, score decisions, suspicion deltas with reasons, attention target, police evidence, transaction IDs, NPC counts, RTT/jitter and frame time. Provide overlays and deterministic scenario launchers only in debug builds. Compile development commands out of release; do not collect voice recordings or sensitive user data for diagnostics.

# 10. Prototype scope and acceptance

## First playable slice

One compact, stylized low-poly stage area, one checkpoint/patrol, one vendor, one medical point and an exit. Support 2–8 connected players and require an eight-player test; include one five-minute rescue-and-extraction objective; the $50 selling side quest; dance and conversation checks; two impairment effects; local suspicion, arrest and lethal swarm behavior. No procedural world, permanent classes or large progression system.

Equipment: standard stock, stage pass, confetti cannon, merch bag, shared stash, issued map, medical voucher, dropped recovery wristbands, practice poi and LED poi. Include the first playable-spirit and teammate-revival loop. Add a basic proximity-voice slice early enough to validate how encounters, detention and spirit play affect communication. “Join the flow” begins as a two-person experiment; complex stage-equipment dependencies and other catalog items are backlog.

| **Gate**               | **Reviewable outcome**                                                                                  |
|------------------------|---------------------------------------------------------------------------------------------------------|
| A - Network foundation | Eight native clients join a host; movement, one interaction and inventory handoff remain consistent     |
| B - Cover and failure  | Dance/conversation grades change suspicion; visible escalation leads to interruptible pursuit and death |
| C - Economy and cops   | Sales credit once; witnessed dealing can cause detention; group can recover after losing stock          |
| D - Group rescue       | DJ and poi distract the correct observers; a threatened teammate can exploit the opening                |
| E - Full round         | Objective, side quest, impairment, rescue, extraction and reset work in a native build                  |

## Acceptance checks

Full group: complete and restart three consecutive rounds with eight players on separate machines. Include players on different home networks. No blocking desync, duplicate money/items or permanent softlocks.

Timing: compare identical scripted input offsets under baseline, 100 ms and 200 ms simulated RTT, plus jitter and modest packet loss. Within the declared supported envelope, network arrival must not change the intended rhythm grade. Report any unsupported conditions.

Fairness: a distraction cannot silently clear police evidence; a hallucination cannot change shared collision; one player’s failure cannot reveal remote teammates globally. Both all-broke and one-survivor scenarios retain a defined end or recovery path.

Release: launch with the editor closed, complete a round and exit; verify development overlays/commands are absent. Proposed performance goal is 60 FPS on named reference hardware; select that hardware before setting final CPU/GPU and crowd budgets.

Fun: observe at least two outside friend groups. Ask whether they understood escalation, could describe an item trade-off, used a rescue voluntarily and wanted another round. These are learning gates, not a forecast of commercial success.

# 11. Risks and decisions for review

These are product/implementation risks for a new project, not regression scores for an existing codebase. No code changes are proposed for approval in this draft.

| **Risk**                                | **Mitigation / decision gate**                                            |
|-----------------------------------------|---------------------------------------------------------------------------|
| Eight-player rhythm fairness            | Validate shared clock and scoring before building many charts             |
| Crowd cost and noisy feedback           | Separate cosmetic/interactive NPCs; cap activity and playtest readability |
| Too many overlapping minigames          | Keep one reusable rhythm framework; prioritize group-visible comedy       |
| Economy deadlock or farming             | Legitimate recovery jobs; atomic transactions; finite buyer opportunities |
| Motion discomfort / inaccessible timing | Comfort equivalents, calibration, remapping and readable cues             |
| Griefing and long downtime              | Voluntary assistance/tethers; bounded consequences and active recovery    |
| Host migration complexity               | Isolate transferable authority state; fault-test migration before release  |

## Resolved decisions

- Support 2–8 players and test full eight-player sessions.
- Use rotating rescue, retrieval and delivery missions.
- Balance comedy with genuine danger.
- Use team revival quests, a playable spirit state and rescuable police detention.
- Win by completing the objective and extracting at least one survivor.
- Build in Unity/C# with stylized low-poly visuals.
- Target 15–20 minute full rounds.
- Include proximity voice and host migration for commercial release.
- Retain cosmetic, item, poi and mission unlocks without permanent power upgrades.
- Use commercially cleared AI-generated music and commissioned originals.

## Decisions still open

D1 - Approve the prototype item subset and defer the remaining catalog.  
D2 - Select reference hardware and minimum specifications.  
D3 - Choose the exact Unity, networking, Steam transport and voice stack after a technical spike.  
D4 - Set revival limits, bail/release costs and spirit communication rules.  
D5 - Set Steam price, release strategy, launch content count and support plan.

## Commercial release work - outside this prototype

Before publishing, assign ownership of the Steam account, team assets and project decisions; settle the team’s commercial arrangements. Prepare original or properly cleared music, art and other content. Verify current Steam onboarding, content-disclosure, store-page and build-review requirements at the time of submission. No legal or platform-policy conclusions are made in this design draft.

Steam price, release date, business budget, content rating, launch map count, achievements, localization and ongoing support are OPEN. Do not advertise unvalidated player counts, hardware support or online features. Persistent progression, public matchmaking, dedicated servers and console builds need separate scope decisions.

## Review request

Review the resolved decisions and remaining D1–D5 items. Comments on item prices and exact timing windows can wait until the first full-group test. The next deliverable after design approval should be an implementation backlog with independently testable milestones and a pinned toolchain.
