# Co-op chaos reference research — September 27, 2026

## Scope and evidence

This is a design analysis of five relevant co-op games, selected for their overlap with *After Hours* rather than a sales ranking. Mechanics below come from developers' own Steam descriptions and feature lists. The **Why it works** column contains design inferences, not claims from player interviews or hands-on play. Popularity signals such as Steam review counts change over time, so they are not used to prove a mechanic caused success. We did not play these games for this note.

Our project baseline is [the approved direction](APPROVED_DIRECTION.md) and [the campsite brief](CAMPSITE_LOBBY_GAMEPLAY_BRIEF.md): 2–8 friends buy gear at camp, enter one 8–12 minute festival mission, combine private clues, perform, find a missing friend, and reach the last shuttle. The existing native smoke script uses scripted positions; it does not prove the human-paced flow.

## Reference loops

| Game | Verifiable loop and social tools | Why it works — inference | Direct lesson for *After Hours* |
| --- | --- | --- | --- |
| [RV There Yet?](https://store.steampowered.com/app/3949040/RV_There_Yet/) | Up to four friends drive one RV toward an exit. Front and rear physics winches solve obstacles; burgers, antidotes, and EpiPens support survival; proximity chat, hats, cooking and cigarettes create downtime. | One shared vehicle gives every mishap a common cost. Driving, spotting, winching and supplies create spontaneous roles; recovery can be as entertaining as the error. | Make the missing friend and last shuttle the team's shared object of concern. Let a bad plan cost minutes or resources, then provide a recoverable route. Give camp a small social activity while everyone loads in. |
| [R.E.P.O.](https://store.steampowered.com/app/3241660/REPO/) | Up to six players locate valuables, physically carry fragile or heavy objects to extraction, avoid monsters, use proximity voice, then buy upgrades and weapons with earned cash. | A simple objective becomes funny and tense because carrying it is awkward, visible and collaborative. The store turns consequences of one run into a group argument before the next. | Make festival interactions embodied: carry an unpaid item to the seller, escort a visibly vulnerable friend, and show wook distraction in the world. Give each purchased item a meaningful, readable use. |
| [Gamble With Your Friends](https://store.steampowered.com/app/3892270/Gamble_With_Your_Friends/) | One to six friends share a bank account and debt. Each casino day gives five minutes to meet a quota; games of chance, Tickets, sketchy items, emotes, cosmetics and proximity voice support group decisions. The store describes about 2–3 hours with three endings. | A public, short clock turns each individual decision into a team decision. Shared resources produce debate and accountability, while silly customization makes losses socially memorable. | Show a real mission countdown and shared outcome. At camp, make stock, price, inventory capacity and ready state obvious to the whole party. Keep the first level short and finishable. |
| [PEAK](https://store.steampowered.com/app/3527290/PEAK/) | Up to four scouts climb a mountain, help each other over ledges, place ropes and spikes, manage stamina injuries and questionable food, use proximity chat, and deal with ghosts. Its mountain layout changes daily. | Traversal, injury and assistance create repeated micro-rescues. Items change the *way* the team solves an obstacle instead of merely increasing a damage number. | Let injuries and intoxication alter task execution and create chances for others to help. Give the player a clear treatment or sober teammate workaround. Use a recognizable first map with varied circumstances, as already approved; a daily regenerated map is unnecessary. |
| [Content Warning](https://store.steampowered.com/app/2881650/Content_Warning/) | Two to four players buy gear, use a diving bell, film dangerous creatures before oxygen or battery runs out, return, then watch their own footage together and spend ad revenue on gear, emotes and props. | The debrief turns messy play into a shared story. A visible limit forces a decision about whether to risk one more objective. | Give the festival run a hard clock and a clear return/debrief moment. Show who escaped and the optional survivor bonus. Make emotes and dance readable from another player's view. |

Two useful commonalities emerge. First, every reference has a **one-sentence group goal** and a bounded attempt: get the RV home, extract valuables, hit quota, reach the peak, or return with footage. Second, each puts **consequences in a shared space**: teammates see the stuck RV, dropped cargo, lost money, injured climber, or dangerous footage. Neither depends on copying a specific physics system.

## Translation into a complete first playable run

### 1. Camp and starting a run

The initial menu should offer a plainly labeled host/create action and a way to join, then land the host in the walkable campsite. The first ten seconds in camp should reveal the seller, gear shelves, team, and lit trailhead. Picking up a shelf item should show *unpaid*, price and carrying state; presenting it to the seller should show the confirmation and cash change. A player should be able to buy at least one low-cost item without solving a hidden economy puzzle. The readiness countdown should start only when all connected players choose ready, and the purchased loadout must cross the transition. These are concrete adaptations of the pre-run stores and shared resource decisions in R.E.P.O. and Gamble With Your Friends.

### 2. Time, mission and completion

Use a **single, prominent countdown** for the playable festival phase, targeting the approved 8–12 minute run. Every objective needs an immediate next step: read a clue, find the correct totem, perform, search, escort, shuttle. The clock should continue through mistakes, rescue and NPC interactions. Reaching the shuttle with the objective complete and at least one living survivor wins; clock expiry or total party loss produces a readable failure result and a clean return to camp. This gives the urgency visible in Gamble With Your Friends and Content Warning without adopting their quota systems.

### 3. NPCs as gameplay, not scenery

| NPC | Player interaction | Consequence and feedback |
| --- | --- | --- |
| Camp seller | Approach, inspect physical stock, carry a choice, present, confirm, buy. | Shared stock and cash update authoritatively. Seller line, receipt and visible item state confirm the transaction. Invalid range or funds receive a specific response. |
| Wook | Approach and talk; choose a social line; use a dance or item to earn trust or redirect attention. | Trust/suspicion changes deterministically. A distracted wook visibly turns, moves or dances for a limited time, opening a route or reducing interference. Failed distractions cost time and raise suspicion; the player can try a different tactic. |
| Security | Avoid, talk if appropriate, or help a detained teammate. | Suspicion and detention are communicated before sudden failure. A detained player retains a useful action and can be recovered. |
| Medic | Ask for help or use a revival item/teammate action. | A downed player can be revived within a clear window. The revive consumes time or a resource, and both players see the recovery. |
| Missing friend | Discover through clue/search; talk to establish cooperation; escort to shuttle. | The friend follows a designated player. If the escort dies, is detained or disconnects, another living player can assume escort without duplicating rewards. |
| Performer / stage host | Start a dance challenge, follow beat prompts, finish or fail. | Results affect a clue, trust or progress. Nearby players see the performing body and can react, while the dancer gets clear timing feedback. |

NPC dialogue should offer short **situational choices** with mechanical outcomes, not long exposition. A text choice should change an explicit state (trust, clue, suspicion, route, escort) and the NPC should react in world and with a readable line. This keeps conversation attached to the time pressure and social decision making that makes these games legible.

### 4. Items, intoxication, death and recovery

Each purchasable item needs a clear category: passive, consumable, treatment, distraction or rescue. Show the public benefit and cost except for deliberately hidden small interactions such as Little Spoon's bounded wook trust bonus, already specified in [the campsite brief](CAMPSITE_LOBBY_GAMEPLAY_BRIEF.md). A consumable must visibly leave inventory, affect authoritative state once, and provide an immediate first-person cue plus a body/world cue for observers. Effects must have a finite duration and counterplay.

Intoxication should make a real tradeoff. A mild state can slightly alter perception and conversation; a stronger state can impair aim, rhythm timing or movement control while exposing private clue information. A sober friend should gain a complementary role rather than merely waiting. The intoxicated player needs first-person visual/audio feedback, and teammates need readable animation, expression or gait changes. Visual effects should not hide the timer, dialogue choices or accessibility-critical cues. Effects must be clamped so the run stays playable and can be treated or outlasted.

Downed and detained states should create brief rescue dilemmas rather than immediate spectator time. Show a revive prompt, give the incapacitated player a useful signal or crawl/action, and let a teammate spend a limited resource or time to recover them. If all players are lost, end decisively. The larger design lesson from PEAK and RV There Yet? is **repairable error**: the group makes a story by recovering, while permanent failure remains possible.

### 5. Visual and social feedback

Use the existing goofy bodies, first-person hands and compact HUD to show consequences. The local player should see the item in hand, intoxicant feedback, private clue and rhythm prompt. Others should see the same player's changed pose, face, carried item, dance and downed/revival states. Wook distraction needs a visible NPC response and end state. This draws on the physical group feedback in RV There Yet?, R.E.P.O. and Content Warning. Sound and proximity voice could strengthen the loop, but the run should remain understandable without voice chat.

## Design boundaries and acceptance

- Preserve the approved clue → performance → friend rescue → shuttle identity. Do not add casino gambling, an RV, or full physics cargo just because the references use them.
- Give every hazard a discoverable response: treat, distract, talk, revive, reroute, or accept the time loss. Avoid a single required item or NPC choice that can hard-lock the first mission.
- Make host authority explicit for money, stock, NPC state, items, timer, damage, revival, dance outcomes, and extraction. Effects on other players should be derived from shared state, while private clues and first-person distortion remain owner-only.
- Keep debug diagnostics in development builds only. Useful fields: phase, countdown, objective, actor status, intoxication intensity/duration, selected item/use result, NPC trust/suspicion/distraction timer, dialogue choice result, rhythm result, escort owner, revive cause, and extraction condition.
- Acceptance requires a real menu → create → camp → buy → ready → first festival level → result journey, including timeout. Test both solo and two-client paths; exercise each item and NPC interaction, dancer success/failure, death/revival, wook distraction, and intoxication from the affected view and a second player's view. Automated state checks and scripted native smoke are useful, but only human-paced play verifies discoverability and comedy.
