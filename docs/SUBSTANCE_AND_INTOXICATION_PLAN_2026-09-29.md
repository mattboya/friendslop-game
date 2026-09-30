# Substance acquisition and intoxication plan — September 29, 2026

## Current state

- **Playable:** Prism tabs (`lsd`) and Moon caps (`mushrooms`) are purchasable in the campsite and night market. Each can be consumed or sold. Consuming one spends a stock unit, reduces suspicion for wooks that see the player, and starts a 60-second effect. At most two distinct effects can be active.
- **Acquisition:** Players can take a physical campsite shelf item to the seller, buy from a night-market shelf, pick up another player's dropped stock, accept a voluntary player handoff, or take the free 90-second Moon caps clue tasting at the night market. The tasting grants an effect without an inventory item.
- **Presentation and rules:** The host applies slower movement and bounded lateral drift. The local client shows a color wash, a small Moon caps field-of-view change, altered rhythm-note paths, and private clue markers. Peers see the intoxicated face/pose but do not receive the player's private effect or inventory details. Effects expire, and a medical voucher can remove the newest one.
- **Definitions only:** `ecstasy`, `ketamine`, `alcohol`, and `weed` have effect entries but no purchasable stock or complete playable presentation. These are not four more available substances.
- **Missing from the agreed spec:** Wooks cannot offer stock for players to accept. There are no authored world stock pickups beyond player-created drops. The two active effects have implicit presentation precedence: the first effect drives rhythm-note paths, while Prism tabs wins the color wash.

The domain suite passed on September 29, 2026 (`node scripts/test-domain.mjs all`). The September 25 native two-client smoke covers the free tasting and private clue, but scripts movement between landmarks. It does not establish human discoverability or the feel of the impairment.

## Recommended sequence

### 1. Make the existing two effects understandable and fair

Keep the first delivery limited to Prism tabs and Moon caps. Show the duration, benefit, impairment, and `Q` use action before purchase and in inventory. Use fictional display names instead of internal effect IDs in prompts and status. Keep crowd/security danger readable while an effect is active. Define an explicit primary-effect rule for two simultaneous effects and cap the combined presentation. Preserve fixed rhythm hit times and receptors, reduced-motion equivalents, and an unaffected teammate's view.

**Gate:** A first-time pair can intentionally buy or take a tasting, explain the trade-off, use it, read a clue, and recover without instructions from the developer. Reduced motion preserves the clue and timing information.

### 2. Add the missing wook offer and authored pickup routes

Give a nearby eligible wook a visible, optional offer with accept/decline actions. Acceptance transfers exactly one finite stock unit to inventory, respects slots and stack limits, and grants local approval only to witnesses. It does not consume the substance automatically. Use host-owned offer state, command IDs, expiry, and one-time settlement so simultaneous accepts, disconnects, and retries cannot duplicate stock. Add a small number of clearly marked world pickups only if a human run shows that shop, tasting, and NPC offers leave acquisition too hard to find; these should have host-owned finite stock and sensible respawn rules.

**Gate:** Two peers cannot claim the same offer or pickup. Full inventory leaves stock in place. Acceptance, consumption, sale, drop, and handoff each spend or move one unit exactly once. Police evidence follows explicit witness and visible-stock rules, with no evidence from a remote offer.

### 3. Add more substances only for distinct cooperative decisions

Treat the four existing non-playable effect entries as candidate designs. For each candidate, first write one specific advantage, one readable impairment, one way a sober friend can help, and where the player obtains it. Add one candidate at a time through catalog item, host rules, local presentation, shop/NPC/world supply, recovery, and UI. Do not expose all four simply to fill a catalog; the first human run should identify which new role improves the game. Keep fictional shop names and bounded effects.

**Gate per new substance:** The effect changes a meaningful choice in the rescue loop; it cannot alter authoritative rhythm timing, shared collision, or another player's private view. It has a tested purchase/offer path, expiry, stacking behavior, and reduced-motion presentation.

### 4. Validate the full flow

Add focused domain tests for stock conservation, command deduplication, capacity, witness approval, effect expiry, and snapshot restore. Add Unity checks for action prompts, private visuals, two-effect precedence, and reduced motion. Run a native host/client pass through campsite purchase, free tasting, wook offer, handoff, market purchase, consumption, recovery, and clue interpretation. Then do one complete human-paced two-person run before expanding content. Test eight-player stock contention and performance before treating this as release ready.

## Debugging and risk

In Editor/development builds, extend the existing F8 diagnostics and host logs with effect ID, source action, remaining duration, acquisition/consume/expiry result, stock count, and local witness suspicion changes. Log command IDs and simulation ticks for duplicate or rejected transactions. Do not log player names, voice, or session credentials. Guard these calls with `#if UNITY_EDITOR || DEVELOPMENT_BUILD` so release builds have no substance diagnostics.

**Risk score: 6/10** for the full sequence. New host-authoritative NPC/world stock and two-effect presentation touch network snapshots, inventory conservation, and mission cues. The initial usability and presentation pass is lower risk (3/10). Preserve the current two-item path as the regression baseline and merge each route only after its gate passes.
