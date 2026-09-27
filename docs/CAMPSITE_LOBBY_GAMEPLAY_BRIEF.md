# Campsite lobby and next gameplay pass

Confirmed September 25, 2026, after the modular-character fit interview. The first playable campsite and Little Spoon pass is implemented. Human-played pacing and usability still need acceptance.

## Current playable scenario

The native prototype has one scripted two-player festival mission: buy a loadout, interpret two private clues with a sober helper, complete a dance, find and escort the missing friend, and extract at the shuttle. Downed and detained players have recovery actions. This has passed a two-process native smoke test, which teleports test players between objectives. A complete human-paced playthrough remains open.

The prior `Shopping` phase used an always-open UI shop with no walkable campsite. The current build switches between separate camp and festival world roots, with independent collision and navigation. Players take visible items from shelves, carry one unpaid copy to the seller, and confirm payment through the authoritative simulation.

## Intended loop

1. Friends join a **walkable festival campsite** and see each other before the level. The camp is visually distinct from the festival grounds but keeps the same deliberately goofy art style.
2. The camp includes parked cars, player tents, **sun shade shelters clustered in the middle**, and a porta potty. These define a small, readable social space rather than a second full level.
3. One visible seller stands at camp. Players approach him to browse and buy pre-level gear. Purchases use the existing authoritative cash, inventory limits, offer validation, and command deduplication.
4. Each player readies at the lit trailhead. Ready players dance in place; when all connected players are ready, a five-second countdown launches automatically. Unpaid held goods return to stock, and purchased inventory enters the festival.
5. Players complete the existing clue, performance, friend rescue, and shuttle extraction scenario. Results and reset return the group to a fresh camp shopping phase.

## Little spoon

- Add a small spoon necklace named **Little Spoon**. It is the **cheapest item players can buy** and occupies an equipment slot or equivalent passive ownership state.
- It has no button action, consumable effect, or explicit combat power. Wearing/owning it gives a **small, bounded improvement to wook trust** through the existing suspicion/approval rules.
- The seller and public item UI describe only the necklace. They do **not** reveal the hidden trust mechanic, its numbers, or a hint such as “wooks like this.”
- The effect remains deterministic and host-authoritative. Development-build-only logs may expose the applied modifier for debugging; release builds must not.
- Balance against other items and the three-slot inventory limit so the cheapest choice is useful but does not become mandatory.

## Current implementation and acceptance

1. Implemented original Blender camp car, sun shade, porta potty and spoon models alongside existing tents. Camp collision/nav is separate from the festival route. Native camp screenshots are under `artifacts/native-smoke/`.
2. Implemented walkable `Shopping`, visible party, shared physical shelf stock, one unpaid held item per player, seller checkout, automatic trailhead launch, inventory carry-in, and reset to camp. The shop keeps host-authoritative purchase and command-deduplication rules.
3. Implemented the $1 necklace, always offered; it occupies one of three inventory slots and reduces **positive wook suspicion gains by 10%** while owned. The rule is deterministic and host-authoritative, with no active use and no public effect disclosure. A development-only F8 diagnostic displays the modifier.
4. Automated tests cover seller distance, offer/price, ownership, passive effect, no active use, transition, reset, camp navigation and art contracts. The native two-client smoke buys the spoon at camp and completes the existing mission. It scripts player positions, so human exploration, interaction feel, 8–12 minute pacing, and balance acceptance remain open.
5. The newer pass replaced full shop panels with physical camp shelves and a faster look, press E, confirm flow at the Night Market. Escape opens a compact festival pass with Resume, Crew + Nearby, and saved local settings. An affected volunteer sees the current totem direction; the sober teammate does not receive the private order. When the friend is stranded because the escort disconnects or is incapacitated, another living player can take over nearby without duplicating the reward.
6. PlayMode verifies complete navigation paths from entry through the market, both totem orders, stage, all three possible friend locations and shuttle, plus the camp route from seller to trailhead. Native two-client smoke checks shared camp stock, payment, readiness countdown, market stock and the scripted mission. It still teleports actors between landmarks and does not establish human-paced traversal or camera-aim usability.

The next gameplay work should concentrate on a human-paced full loop: approach and shop naturally, choose different loadouts, ready/start, traverse the festival, recover from mistakes, extract, and return to camp. Tune the spoon benefit against its inventory-slot cost using observed play.
