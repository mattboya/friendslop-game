# Game design: festival weekends (approved 2026-09-30)

Agreed in a grilling session on 2026-09-29/30. Where this conflicts with
[APPROVED_DIRECTION.md](APPROVED_DIRECTION.md) or
[festival-coop-v1-spec.md](../festival-coop-v1-spec.md), this document wins.

## Goal

Worth $8 on Steam for a fun evening with friends. The first evening should run
past two hours (Steam's refund window) with the group still wanting more.

## Pitch

Friends survive a two-day festival weekend. Each level a spinner picks one
friend, a second spinner picks their dose, and that "tripper" is the only one
who can see where to go. Their visions are partly false, and the whole crew
has to dance to stay hidden.

The two hooks the design is built around:

1. **Blend-in dances that fail.** The crew dances together to avoid notice,
   and the worst dancer sets everyone's suspicion.
2. **Impaired-friend chaos.** The tripper sees too much, and some of it is
   false.

## Structure

- **Run = one festival weekend** of four levels: Day 1 (8 min), Night 1
  (10 min), Day 2 (8 min), Night 2 (10 min). About 55 minutes with camp time.
- **Day levels are the hustle:** meet a cash quota before sundown. The quota
  rises each day and each festival.
- **Night levels are the rescue:** neon lighting, with the tripper guiding the
  crew to a lost friend.
- **Night 2 finale:** everyone is dosed to some degree. The crew must rescue
  the friend AND get every player back to camp. It is all-or-nothing:
  - One dead body can be dragged slowly by a single player.
  - Every additional body needs two carriers.
- **Failure:** failing any level ends the weekend and restarts that festival
  from Day 1. Cleared festivals stay unlocked.
- **Carryover:** cash and gear carry through a weekend. Each new festival
  starts with fresh cash. Cosmetics are kept forever.
- **Difficulty rises with each level and festival:**
  - more narcs (undercover cops) in the crowd
  - faster wook suspicion
  - longer clue chains

  The number of cops stays the same, and they keep to the edges of crowds.
- **Group size:** tuned for 4, allows up to 8.
  - Bunched-up crews draw suspicion faster.
  - Crews of 5 or more get two objectives at once.
  - Solo is a practice mode where you are always the tripper.
- **Level length:** 8-minute days, 10-minute nights.

## The tripper

- **Who:** a people spinner picks the tripper each level. Nobody goes again
  until everyone has had a turn.
- **How much:** a dose spinner picks 1 to 4 doses. Its slices are 31/31/31/7:
  1, 2 and 3 doses are equally likely, and the 4-dose slice is a thin
  sliver.
- **Presentation:** an animation shows the tripper taking the dose and
  reacting.

**How reliable the visions are, by dose:**

| Doses | Visions that are true |
| --- | --- |
| 1 | 75% |
| 2 | 50% |
| 3 | 25% |
| 4 | 10% |

**What the tripper sees:**

- **Day:** which festivalgoers are buyers and which are narcs.
- **Night:** the clue trail to the lost friend.

**How the visions behave:**

- The truth is always somewhere among the visions, buried in fakes.
- Heavier doses also reveal secret things, by day or night: a hidden cash
  stash or a buyer who pays double. They also raise the level's payout.

**Spotting fakes:** false visions have learnable tells. They cast no shadow,
shimmer when the camera moves, and are slightly off in colour. The only hint in
the game is one line: "Trust, but verify."

**Checking a vision:**

| Method | Takes | Risk |
| --- | --- | --- |
| Chat with the person | about 5 s | safe |
| Dance with them | about 2 s | misses raise suspicion |

Only the tripper learns the truth and must relay it over voice.

**Removed:** the old "sober friend stands within 4 m to read a clue" rule is
gone, because checking replaces it.

**Chat dialogue** is a mix-and-match template grammar:

- Structure: opener × question × answer.
- Answers depend on the person's role (buyer, narc, regular, clue-holder), a
  persona type, and the festival.
- Narcs slip in cop-speak tells.
- Lines were drafted offline and hand-picked. No AI runs in the game.

## Social layer

- **Proximity voice chat,** garbled more with higher doses. This is waiting on
  the Steam work.
- **Group dancing:** the worst dancer among the crew near the watchers sets
  the crew's suspicion.
- **Debrief at the campfire after each level:**
  1. Everyone votes on the players at once.
  2. The results are revealed together, in a funny way.
  3. The award winner wears a badge of shame during the next level.
  4. The "worst" players take a shot that impairs them for the first 90 seconds
     of the next level.

## Festivals

The names are fictional parodies, never the real trademarks.

1. **The polo-field festival** (the existing map, restyled with palms, a
   Ferris wheel and a big main stage). Its twists:
   - influencers filming (being on camera adds suspicion)
   - VIP wristband zones with shortcuts and rich buyers
   - a Ferris wheel lookout (see the whole map, but you're stuck for one
     rotation)
2. **The playa festival** (a new map). Its twists:
   - cash is reskinned as odd objects (buttons, ramen), a different set per
     player, with the same economy underneath
   - dust storms that cut visibility
   - art cars to ride or hide behind
   - the effigy burn as the Night 2 set piece

## Business

- $8 on Steam. Windows first, then Mac.
- Friends join through Steam lobbies with invites and Steam's relay.
- The substance theme is fictional and goofy: invented names and cartoon
  effects, with no real-drug labels anywhere a player can see.
- The demo and the target date are parked.

## Order of work

1. Windows build support.
2. The developer plays a full round, then decides whether to freeze the art.
3. A friend playtest over Discord and Tailscale.
4. The fun loop: spinners, visions and checking, day/night weekends.
5. Steam lobbies and proximity voice.
6. The debrief.
7. The polo-festival restyle.
8. The playa festival.

**Biggest risk: scope** for a solo, part-time developer. Cut festival twists
first; the tripper, the spinners and the debrief are the core.
