# Minigames: booths that stop the clock (proposed 2026-09-30)

Status: approved 2026-10-01, every open decision taken as (A). MINI-1 is built: its rules (`Core/Simulation/Minigames.cs`)
and its screen (`Presentation/FestivalMinigameView.cs`, the HUD's minigame card). MINI-4, Mystery Cups, is built
(`Core/Simulation/MysteryCups.cs`) and is the game every booth deals for now. MINI-2 and MINI-3 are queue entries in
`WORK_QUEUE.md`.
This design builds on [GAME_DESIGN_2026-09-30.md](GAME_DESIGN_2026-09-30.md) (the tripper, doses, visions and their
tells, the group dance, the debrief). Where the two conflict, that document wins, except on one point: the minigame
pause is a deliberate exception to "the clock keeps running" in
[FRIENDSLOP_RESEARCH_2026-09-27.md](FRIENDSLOP_RESEARCH_2026-09-27.md).

## Goal

A short shared moment in the middle of a level. The whole crew plays together, and it goes one of two ways: a
windfall, or a mess everyone blames each other for at the debrief. Minigames are optional, and each one costs
something to start, so the pause is a gamble and never a free rest.

## The rules every minigame shares (MINI-1)

**Booths.** Each level has one booth: a stranger with a sign. It sits on a spot at the edge of a crowd, chosen by
seed. The booth's game is drawn from a bag of kinds, so a weekend offers every kind before any repeats. A booth
can be played once; if the crew ignores it, it's gone when the level ends.

**Opt-in is physical.** A game starts when every connected player who is Alive has stood in the booth's 4 m ring
for 3 seconds in a row. Leaving the ring resets the countdown. It can't start while anyone is Downed or in the
middle of an interaction. So anyone can refuse by staying away. Getting there costs travel time, and a bunched
crew draws suspicion (CROWD-1) while it waits. Solo play works: the ring holds one player.

**The clock stops.** While a minigame runs, the round's phase is `"Minigame"`:
- The level clock stops, and so do effect timers, bleed-out, wooks, cops, interactions and handoff expiry.
- Players can't move, and every command except the minigame's own is refused.
- Spirits and detained players watch but don't play.
- The world holds still as a slightly desaturated freeze-frame: the festival pauses because the crew has been
  pulled into a moment.

A minigame lasts at most 90 seconds. At that point the host ends it and scores what's in.

As built (MINI-1): the booths stand at `Festivals.BoothSpots`, three crowd-edge spots whose rings keep clear of every route
players routinely take. Players in the game send its own `Minigame` command; `Rhythm` also goes through, for the Drum
Circle. The result banner shows while the world is still stopped, so the pause is the game plus its banner. The bag held a
no-op stub until MINI-4's Mystery Cups replaced it; the Drum Circle and the Light Show join the bag as they are built.

On screen (MINI-1): the booth is a festivalgoer rig under a MINIGAME sign, in a glowing mint ring the size of the rules'.
Standing alive in it, the HUD's minigame card counts who of the crew is in, names whoever keeps it from starting (down or
busy, anywhere), then counts the 3 s down. While the game runs the festival stays on screen, frozen: `Time.timeScale` is 0, so
everything drawn on frame time holds still (a game's own visuals must run on unscaled time), the Ferris wheel stops on the
world's clock (`FestivalSimulation.WorldSeconds`: the simulation clock less the pause), the colour grade loses 30 saturation
and the HUD's clock stands at the time left. The card says the festival is paused, or shows the game's own lines (Mystery
Cups: below), then shows the result, such as "+45 s" or "−30 s", until play resumes.

**Resuming.** Many timers store an absolute time on the simulation clock:
- handoff offers (`ExpiresAt`)
- interaction start times
- NPC `DistractedUntil`, `AttackAt` and `LastSeenSeconds`
- player `SprintUntil`

The simulation clock keeps running during the minigame, so every such deadline moves forward by the pause's
length before play resumes. A test enumerates these fields, so a new deadline field that the shift doesn't
cover makes the test fail.

**What's at stake.** The main stake is time on the level clock:
- Each game adds or removes seconds, shown in a result banner such as "+45 s" or "−30 s" for 4 seconds. It changes the
  level's length, and a gain lengthens the spinner's dose with it, since a dose lasts the level.
- A loss never leaves fewer than 15 seconds on the clock, nor takes any when 15 or fewer are left.
- Each game also has one side effect of its own: suspicion, cash or a dose.

**Networking.** Minigame state is public round state, copied by `FestivalSession.ViewFor`. Private parts are
filtered per player: a player's own hallucinations and answers, and the tripper's hints.

## Drum Circle (MINI-2)

A drum circle at the edge of the crowd.
- Each player in the ring gets their own part: a separate rhythm chart of 24 notes at 0.5 s, all starting
  together.
- It reuses the rhythm framework: charts, the judge, and time-stamped inputs.
- The HUD shows everyone's live accuracy in a strip, so the whole crew sees who's dragging.

**The worst drummer sets the result**, just as the worst dancer does in the group dance:

| Worst part | Clock | Crowd |
| --- | --- | --- |
| 0.75 or more | +45 s | every wook within 25 m of the booth drops each player's suspicion by 20 |
| 0.40 to 0.75 | +0 s | nothing ("Decent.") |
| under 0.40 | −30 s | those wooks gain 15 suspicion on each player |

## Light Show (MINI-3)

A stranger (a glover) gives the crew a light show. The show is the distraction: the real test is what happened
around it, and you don't know in advance what you'll be asked.

- **The show** lasts about 20 seconds.
  - Each player's camera faces the glover, whose lit hands fill the middle of the screen.
  - Players can look up to 40° either way.
  - Around the edges, a script of background events plays, generated from the seed and the same for every player:
    - extras walking past (how many)
    - coloured lights sweeping over (which colours)
    - shapes flashing on a sign behind the glover (which shapes)
    - what one passer-by was carrying
  - The glover's own lights are fair game too ("what colour were my lights at the end?"), so ignoring the show
    doesn't work.
- **The questions.** Afterwards, each player privately gets their own 3 questions, drawn from those categories.
  - Each question has 4 answers plus "Not sure", with 8 seconds to answer.
  - Answers stay hidden until everyone has answered or time runs out. Then they're revealed all at once, the way
    the debrief reveals its votes.
- **Intoxication: harder, but worth more.** A player's dose `d` is the intensity of their `dose` effect, or 0.
  - *Harder:* the show gets bigger and brighter. The player's own view also gets `d` hallucinated extras or lights
    that nobody else sees. These carry the vision tells (no shadow, shimmer when the camera moves, slightly off
    colour) and don't count toward any answer.
  - *Worth more:* a correct answer earns `1 + d` points, a wrong answer costs 1, and "Not sure" scores 0.
- **Payout:** the crew's net points × 4 s, clamped to between −40 s and +60 s. For example, four sober players
  who get everything right earn +48 s. The tripper is the crew's big swing either way.

This also teaches the vision tells in a safe setting: players who learn to spot a fake extra here will spot a
fake buyer later.

## Mystery Cups (MINI-4)

A shady bartender sets out cups. One is spiked. It's push-your-luck with a shared pot.

- **Setup:** the number of cups is the crew's size plus 2, with a minimum of 4. Which cup is spiked is chosen by
  seed. Players take turns in a seeded order, with 10 s per turn.
- **Your turn:** drink a cup, or **Call it**. If time runs out, you've called it.
- **A safe cup** adds to the pot: the nth safe cup adds $5 × n.
- **The spiked cup:**
  - The pot is lost.
  - The drinker gets a `dose` effect for the rest of the level, or +1 intensity if they already have one, up to 4.
  - It slows them and makes them sway like any dose. It doesn't change the tripper's visions, which were rolled at
    the start of the level.
- **Calling it** pays the pot like sale cash: split evenly across the crew's cash and counted toward a day level's
  quota. On a night level it goes to the stash instead. When only the spiked cup is left, the game calls it
  automatically.
- **The tripper's hint:** the tripper alone sees one cup glowing as "spiked". It's right with the tripper's vision
  reliability (75 / 50 / 25 / 10% by dose); otherwise it marks a safe cup. They relay it over voice, and the crew
  decides how much to trust it.

As built (MINI-4): the cups, which one is spiked, the turn order and the hint come from one stream that mixes in the host's
secret deal seed, so no client can work them out; the spiked cup never leaves the host, and the hint reaches only the
tripper's game (`FestivalSimulation.VisibleBooth`). The game leaves the clock unchanged (+0 s): the pot is the stake. A
spiked dose raises only the drinker's dose effect, never the spin's result, so the tripper's visions and the sale payout
stay as rolled; its substance comes from a stream of its own. The pot counts as sales by night too, banked to the stash; by
day it is split across the players in the game, any odd dollars to the stash. A turn holder who has left the game simply
runs out of time, which calls it. On screen the cups stand in a numbered row on a table in front of the booth's stranger;
the player up drinks the cup under the aim dot with E and calls it with F; the tripper's hunch cup glows red in their view
alone; the card shows the pot, whose turn it is and their seconds, then what became of the pot.

## Rejected ideas

- **Group Photo**, where everyone picks a matching pose, and **Tripper Telephone**, where the tripper relays
  symbols. Neither was chosen for the first batch. They can be revived later: each fits the same framework.
- **An unpaused minigame.** It would compete with the level's objective instead of offering a timeout gamble.

## Decisions

All six were taken as (A) on 2026-10-01.

1. **Opt-in:** (A) everyone Alive stands in the ring, or (B) a majority vote from anywhere pulls the crew in.
2. **The main stake:** (A) time plus one side effect per game, or (B) cash only.
3. **Booths per level:** (A) one, or (B) one by day and two at night.
4. **Drum Circle result:** (A) the worst drummer, or (B) the crew's average.
5. **Light Show questions:** (A) each player gets their own draw, or (B) everyone gets the same questions.
6. **Mystery Cups spike:** (A) the drinker gets a dose, or (B) the whole crew takes a 90-second debrief-style shot.
