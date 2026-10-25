# Performance games: a different game each time you perform (proposed 2026-10-23)

Status: a design for Matt to pick from. Nothing here is built yet, except the two games that exist today (Arrow Lanes and
DJ Ride). After Matt's picks, the slot and each picked game become their own queue entries in `WORK_QUEUE.md`.
This design builds on [GAME_DESIGN_2026-09-30.md](GAME_DESIGN_2026-09-30.md) (the group dance, doses, the stage) and
sits beside [MINIGAMES_2026-09-30.md](MINIGAMES_2026-09-30.md) (the booth games). Where they conflict, the game design
wins.

Line numbers below are at commit `a0ede64` on local main.

## Goal

While your character dances, spins poi, DJs or drums, you play a short game on the HUD. Today that game is always the
same arrow lanes. With this design, each performance draws a game from a pool that keeps growing: the character on screen
keeps doing the thing, everyone around you sees the same performance pose, and only the game in front of you changes.
Variety is the fun, so every new game is a surprise. But each game still has to feel like what your character is doing.

## Name

These are **performance games**. "Minigame" already means the booth games that pause the festival (Mystery Cups, Drum
Circle, Light Show; [MINIGAMES_2026-09-30.md](MINIGAMES_2026-09-30.md)). The two kinds differ in one way that matters:

- A booth minigame pauses the festival. The level clock stops and the world freezes.
- A performance game runs inside a performance and never pauses anything. The clock, the crowd and the cops keep going.

The Drum Circle is both: it is a booth minigame, and each player's part in it is a performance. So a Drum Circle part
draws a performance game too.

In code and in the queue, use "performance game" (or `PerformanceGame`), never "minigame", for these.

## Today

Every performance plays the arrow lanes, except the stage DJ set, which plays DJ Ride since DANCE-13.

**Which actions are performances.** The list is in three places, and they agree:
- `FestivalInput.IsRhythmKind` (`Presentation/FestivalInput.cs:14`): Dance, Poi, Dj, ConfirmDance, the Drum Circle part
  (`DrumPartKind`) and the art car's deck set (`DeckSetKind`).
- `FestivalInput.IsRhythm` (`:17`) adds the trust dance (`FestivalSimulation.TrustDancing`).
- The host's `FestivalSimulation.IsRhythm` (`Core/Simulation/Interactions.cs:37`) holds the same list with the trust
  dance.

**How long each one is.** A chart is a 2 s lead, then its beats. The stage beat grid then lays the beats onto the song, so
the real seconds follow the song's tempo.

| Action | Beats | Beat | Where |
| --- | --- | --- | --- |
| Dance with a festivalgoer (and so a group dance) | 8 | 0.5 s | the defaults, `Core/Simulation/State.cs:117`; the level sets the pattern's difficulty (`ChartDifficulty = LevelIndex`, `Interactions.cs:4`) |
| Poi | 12 | 0.4 s | `BeginPoi`, `Interactions.cs:29` |
| Stage DJ set | 5 phrases of 12 | 0.4 s, 0.75 s apart | `BeginDj` `Interactions.cs:30`, the next phrase `:46` |
| Tripper's check dance | 4 | 0.5 s | `ConfirmDanceNotes`, `Core/Simulation/Confirm.cs:14`, used at `:46` |
| Trust dance before a talk | 8 | 0.5 s | `NoteCount` default, `State.cs:117`; started at `Core/Simulation/Conversations.cs:39` |
| Art car deck set | 16 | 0.4 s | `DeckSetNotes` and `DeckBeatSeconds`, `Core/Content/Festivals.cs:76-77`; used at `Core/Simulation/DeckSets.cs:23` |
| Drum Circle part | 24 | 0.5 s | `Core/Simulation/DrumCircle.cs:12` and `:58` |

So a performance lasts from about 4 s (a check dance) to about 14 s (a Drum Circle part). A whole DJ set is about 37 s.

**One score.** The host turns the player's timed inputs into one score from 0 to 1:
- `Judge` (`Interactions.cs:59`) builds the chart with `RhythmChart.For(i)` (`Core/Content/Rhythm.cs:14-24`) and replays the
  input log through `ReplayRhythm` (`Core/Simulation/RhythmInputs.cs:11`). For a DJ set it calls `DjRide.Replay` instead.
- The Drum Circle replays its parts itself, live, with its own `RhythmJudge` (`DrumCircle.cs:77-81`), so its strip can
  show each player's accuracy as they play.

**The wire.** Each input is a `RhythmInput` {Direction, TimeSeconds, Release} (`State.cs:114`). `Submit`
(`RhythmInputs.cs:9`) accepts only:
- Direction 0-3,
- a press or a release (Amount 0 or 1),
- a time at most 0.15 s ahead of the host clock and at most 0.75 s old,
- at most 128 inputs in one performance.

The client sends each input through `FestivalSession.Rhythm`, which applies the player's timing offset from the settings.

**What the score drives.** Every payoff reads only the score:
- suspicion: `RhythmDelta(score) = round(20 - 35 x score)` (`Core/Simulation/GroupDance.cs:14`) for every watcher;
- the group dance: the worst score among the crew near the watchers (`GroupDance.cs:16`);
- poi: a small distraction, and a strong one for a pair who both score 0.75 (`Interactions.cs:44`);
- a DJ phrase: from 0.75, a 120 m distraction, and every phrase sets the crowd's cheer (`Interactions.cs:45`);
- a sale's trust dance: under 0.6 the festivalgoer walks off (`Conversations.cs:96-101`);
- a deck set's tips: from 0.4, and full tips from 0.75 (`DeckSets.cs:55-58`);
- the Drum Circle: the worst part sets the clock change (`DrumCircle.cs:86-89`).

**The beat.** `ScheduleStageRhythm` (`Core/Simulation/ChallengeTiming.cs:7-10`) anchors a chart to the measured beats of
the song of the nearer stage (`StageMusic.TrackAt`, MUSIC-7) through `RhythmChart.InitializeStage`. A deck set and a Drum
Circle part anchor the same way (`DeckSets.cs:26`, `DrumCircle.cs:59`).

**The dose twist.** A dose bends how the notes travel, never when they must be hit:
- `FestivalHud.cs:1704-1705` picks the substance (`FestivalTrip.LaneEffect`), its dose (`LaneDose`) and its look-ahead
  (`EffectDefinition.LeadSeconds`, `Core/Content/Catalog.cs:52-61`: 2 s; Rolly Pollies 1.6 s; Pony Dust 2.4 s).
- `EffectPresentation.Path` (`Core/Content/Rhythm.cs:200-225`) bends each note's path.
- `PlaceNote` (`FestivalHud.cs:1875-1885`) draws it, called from `DrawChart` (`:1813`, called at `:1715`).

Today the six substances bend the lanes like this:

| Substance | The lanes on it |
| --- | --- |
| Tongue Stamps (lsd) | notes trail side to side; with a dose they start in the next lane and slide into their own |
| Fun Guys (mushrooms) | notes curve; with a dose they surge and stall in waves |
| Rolly Pollies (ecstasy) | 1.6 s look-ahead; with a dose notes start slow and rush in late |
| Pony Dust (ketamine) | 2.4 s look-ahead; with a dose notes stop a third of the way down, hang, then catch up |
| Shot (alcohol) | notes spin and wobble |
| Couch Lock (weed) | with a dose notes rush out, then creep into the target |

The Reduced motion setting calms the trails, the curves and the spin (`Rhythm.cs:205-207`). DJ Ride today uses only the
look-ahead (`FestivalHud.cs:1743-1744`): it has no dose twist yet.

**Controls.** Four note keys, which the player can rebind (`FestivalInput.Notes`, `FestivalInput.cs:23`): Left, Down, Up and
Right, also A, S, W and D. DJ Ride steers with the Left and Right keys.

**Two players.** A paired poi copies the partner's chart seed and stage anchor (`BeginPoi`, `Interactions.cs:29`), so both
partners play the same chart. Group dancers each dance their own chart, and the worst score counts.

## The slot

DANCE-13 built the first slot when it added DJ Ride. A DJ set takes the ride's seed, its phrase and the level in, and gives
back timed inputs that the host replays into the same 0-1 score. Three seams carry it:

1. **The chart.** `RhythmChart.For` picks the chart maker by kind (`Rhythm.cs:17`): `DjRide.Create` for a DJ set, the arrow
   chart for everything else. The stage beat grid lays either one onto the song.
2. **The replay.** `Judge` replays a DJ set with `DjRide.Replay` (`Interactions.cs:59`). The same replay, cut at "now",
   gives the HUD its live combo (`FestivalHud.cs:1745`).
3. **The screen.** `FestivalHudText.ShowsRide` (`FestivalHudText.cs:598`) puts the ride on screen in place of the lanes
   (`FestivalHud.cs:1670`). `FestivalDjRide` draws the road. The Left and Right keys move the rider, and each move travels
   as a `RhythmInput` whose Direction is the new lane (`FestivalHud.cs:1741-1742`).

Every performance game fills this same slot. The slot entry turns "is this a DJ set?" into "which game did this
performance draw?":

**In.** The host gives the game:
- a seed (the performance's `ChartSeed`),
- the action (dance, poi, DJ, check, trust, deck set or drum part),
- the length: the action's beats and beat, as in the table above,
- the level (`ChartDifficulty`), for how busy the game gets,
- the stage beat grid, when a song plays,
- on the screen only: the player's substance, dose and look-ahead, and the Reduced motion setting.

**Out.** The game reduces all play to `RhythmInput` presses and releases, on the four note keys, in the `Submit` envelope.
The host replays them into one 0-1 score with the game's own replay, so:
- cheating stays as hard as today: the client sends only what the player pressed and when, and the host judges it;
- every payoff downstream stays exactly as it is, because each one reads only the score.

**What each game provides.** Three parts, the same as DJ Ride:
- a chart maker: seed, beats, beat and level in, a `RhythmChart` out (notes with a Direction, a time and any flags);
- a replay: the chart, the inputs and the Good window in, the score (and a live combo, cut at "now") out;
- a screen: a HUD panel that draws the chart and turns key presses into `RhythmInput`s.

**Rules every game follows.**
- **Length.** The game fills the action's window from the table above, so sales, distractions and the crowd keep their
  timing.
- **The wire.** Direction 0-3 and the release flag mean whatever the game says (DJ Ride's Direction is a lane, 0-2). Send
  where the player is, not a step, so a lost input cannot leave the host and the screen out of step. A perfect run must
  stay well under 128 inputs, a press and a release counting as two.
- **Dose hooks.** Each game says how each of the six substances warps it. Hit times never change, only the look. Where a
  game has no idea of its own, it uses the shared look-ahead.
- **Beat hooks.** When a song plays, the game's beats sit on the song's measured beats, through the same stage grid.
- **Controls.** The four note keys only, press and hold, keyboard first. No new keys.
- **Reduced motion.** Every game has a calm form.
- **Art.** Drawn on the HUD (like `FestivalDjRide`, a UI graphic) or from primitives. No new models, textures or sound
  files: sounds are made in code.
- **The character.** The game never changes the character's pose. Everyone sees the same dance, poi, DJ or drum pose as
  today.

**Picking.** When a performance starts, the host draws its game:
- from the games tagged for the action,
- never the game this player played last, unless the action has only one game,
- from a stream seeded by the performance's seed, so a replay of the round draws the same.

The draw is one new public field on the interaction (its game) and one on the player (their last game). The view copy
already carries the whole interaction to its player (`Network/FestivalSession.cs:1049`). So the slot entry raises the
protocol once. A game added after that changes no wire, as long as it fits the `Submit` envelope.

A DJ set is one performance: its five phrases all play the game it drew.

**Two players.** A paired poi or a group dance draws once:
- a paired poi copies the partner's game along with the seed (`BeginPoi` already does this for the seed);
- a dance that starts near a crew mate's running dance copies that dance's game, so a group dances one game;
- a Drum Circle draws once for the whole booth, and every part plays that game.

**What the slot entry must change.** For the record, so the slot's queue entry starts from the facts:
- `RhythmChart.For` and `Judge` pick the game's chart maker and replay, not the kind.
- The Drum Circle's live replay (`DrumCircle.cs:77-81`) goes through the same game replay.
- The HUD's lanes, the ride and the feedback (`ProcessRhythmFeedback`, `FestivalHud.cs:1887`) choose the panel by game.
- `FestivalHudText.ShowsRide` becomes "this performance drew DJ Ride".
- DJ Ride gets its dose twist (see its row below).

## Candidates

Seventeen candidates. Arrow Lanes and DJ Ride exist today. The keys are the four note keys: Left, Down, Up and Right.
Every score is held between 0 and 1. Each dose line names all six substances: **Stamps** (Tongue Stamps), **Guys** (Fun
Guys), **Rolly** (Rolly Pollies), **Pony** (Pony Dust), **Shot** and **Couch** (Couch Lock). "Fits" uses the short action
names from the slot's inputs (dance, poi, DJ, check, trust, deck set, drum part). A group dance is a dance near crew mates.

| # | Game | Pitch | Controls | Scoring (0-1) | Dose hook | Beat | Fits | Size |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | **Arrow Lanes** (today) | Arrows rise up four lanes and you hit each one on the beat.<br>Guitar Hero meets DDR, the game every performance plays now. | Each key is its lane; press, and hold for a long note. | Notes hit in the Good window over all notes, less a tenth for each extra press. | Stamps: notes slide in from the next lane; Guys: they surge and stall; Rolly: shorter look, slow then a rush; Pony: longer look, a hang a third of the way; Shot: notes spin; Couch: a rush out, then a creep in. | One note on each stage beat, with off-beats from the level. | all | S (built) |
| 2 | **DJ Ride** (DANCE-13) | You ride a three-lane road of light down the stage song, behind the decks.<br>Catch the blocks, dodge the greys, and the crowd roars. | Left and Right steer one lane. | Blocks caught over all blocks, less a tenth for each grey hit and each wasted move. | Stamps: the road ripples sideways; Guys: the road breathes in and out; Rolly: shorter look, blocks rush in late; Pony: longer look, the road freezes then jumps; Shot: the road rolls side to side; Couch: blocks rush out and creep in. Only the look-ahead is built today. | One block on each stage beat, off-beat blocks and greys from the level and the phrase. | DJ, deck set | M (built) |
| 3 | **Call and Response** | Your character (or the festivalgoer) shows four moves on the beat, then you repeat them.<br>Rhythm Heaven's call and answer, with a dash of Simon Says. | Repeat each shown move with its key. | Echoes in the right lane, in the Good window of their beat, over all echoes, less a tenth for each extra press. | Stamps: the shown moves trail into the next lane; Guys: the call bubbles wobble; Rolly: each call flashes shorter; Pony: the call hangs, then catches up; Shot: the shown arrows spin; Couch: the calls fade in late. | Half the beats call, half answer. | dance, trust | M |
| 4 | **Poi Orbit** | A glowing poi head circles a ring with four marks.<br>Tap the mark's key as the head sweeps past it, on the beat. | The key of the mark the head passes. | Marks hit in the Good window over all marks, less a tenth for each extra press. | Stamps: the head leaves long trails; Guys: the ring bends into a wobbly loop; Rolly: the head speeds up near each mark; Pony: the head stops a third of the way, then catches up; Shot: the ring spins; Couch: the head drags late and snaps in. | One sweep past a mark on each beat; the ring turns once in four beats. | poi | M |
| 5 | **Drop Hold** | The build-up rises: hold a key through it, and let go right on the drop.<br>Every DJ's favourite moment, with the crowd waiting. | Hold any key from the cue; release on the drop. | Drops held without a break and let go in the Good window, over all drops. | Stamps: the build-up meter trails sideways; Guys: the meter breathes; Rolly: the meter shows the drop later; Pony: the meter stalls, then jumps; Shot: the meter tilts; Couch: the meter fills fast, then crawls to the drop. | Each build-up is four beats; the drop falls on the next bar's first beat. | DJ, deck set, dance | S |
| 6 | **Mirror Mirror** | Your dance partner throws arrows at you, and you answer with the mirror move.<br>Left is right, right is left, and up and down stay. | The mirrored key of each arrow. | Mirrored notes hit in the Good window over all notes, less a tenth for each extra press. | Stamps: the partner's arrows slide in from the next lane; Guys: they surge and stall; Rolly: shorter look, a late rush; Pony: longer look, a hang; Shot: they spin; Couch: a rush out, then a creep in. | As Arrow Lanes. | dance, trust | S |
| 7 | **Drum Fill** | Two drums: the skin and the rim, on one lane.<br>Taiko no Tatsujin at the festival drum circle. | Left or Right hits the skin; Up or Down hits the rim. | Hits of the right kind in the Good window over all notes, less a tenth for each extra hit. | Stamps: notes trail; Guys: the lane waves; Rolly: notes rush in late; Pony: notes hang, then catch up; Shot: notes spin; Couch: notes creep in. | One note on each beat, fills from the level. | drum part, deck set | S |
| 8 | **Hold the Pose** | A cue card shows a pose a beat ahead: strike it on the beat and hold it to the end.<br>Dance Central's flash cards, for one key. | Press the pose's key on the beat; release when its bar ends. | Poses struck in the Good window and released in the Good window of their end, over all poses. | Stamps: the cards trail; Guys: the cards curve in; Rolly: cards show later; Pony: cards hang, then catch up; Shot: the cards spin; Couch: the cards fade in late. | Each pose starts on a beat and lasts one to three beats. | dance, trust, check | M |
| 9 | **Blind Bar** | The beat marks fade out for the last bars, and you keep the beat alone.<br>Rhythm Heaven's "keep it going", over the stage song. | Any key on each beat. | Taps in the Good window of a beat over all beats, less a tenth for each extra tap. | Stamps: ghost beat marks trail by; Guys: the marks pulse out of time (only the look); Rolly: the marks fade sooner; Pony: the marks fade later; Shot: the marks spin away; Couch: the haze covers the marks one beat earlier. | Every beat; half the bars hide the marks. | drum part, dance, poi | S |
| 10 | **Glowstick Sides** | One side of the crowd lights up on each beat: point your glowsticks at it.<br>Wave left, wave right, keep the whole field with you. | Hold Left or Right to point; switch on the beat. | Beats where the held side matches the lit side over all beats. | Stamps: the light smears across both sides; Guys: the sides glow in waves; Rolly: the next side shows later; Pony: the light lingers, then jumps; Shot: the crowd picture rocks; Couch: the light fades in slowly. | One side for each beat or two. | DJ, dance, deck set | S |
| 11 | **Crossfader** | A needle drifts between two decks: nudge it back to the gold band on each beat.<br>Keep the mix clean, or the crowd hears the train wreck. | Left and Right nudge one step. | Beats where the needle sits in the band over all beats, less a tenth for each extra nudge. | Stamps: the needle trails; Guys: the band breathes wider and narrower (only the look); Rolly: the drift shows later; Pony: the needle freezes, then catches up; Shot: the fader tilts; Couch: the needle drifts lazily, then snaps. | The needle drifts one step on each beat, from the seed. | DJ, deck set | M |
| 12 | **Crowd Surf** | A crowd surfer tumbles over four pairs of hands.<br>Raise the hands where they land, on the beat, and keep them up. | The key of the hands where the surfer lands. | Catches in the Good window over all landings, less a tenth for each extra press. | Stamps: the surfer leaves rainbow trails; Guys: the surfer floats in curves; Rolly: the surfer falls slow, then fast; Pony: the surfer hangs in the air; Shot: the surfer spins; Couch: the surfer drops fast, then drifts in. | One landing on each beat. | dance, DJ, deck set | M |
| 13 | **Air Horn** | Hit the air horn on the first beat of each bar, and nowhere else.<br>Simple, loud, and the crowd judges every honk. | Up on each bar's first beat. | Bar starts hit in the Good window over all bars, less a fifth for each extra honk. | Stamps: the bar mark trails; Guys: the bar mark curves in; Rolly: the bar mark shows later; Pony: the bar mark hangs; Shot: the horn spins; Couch: the bar mark creeps in. | The first beat of every bar. | DJ, deck set | S |
| 14 | **Lighter Sway** | The slow song moment: sway your lighter from side to side, one way each bar.<br>Calm and easy, until the song speeds up. | Hold Left or Right; switch on each bar. | Bars swayed to the right side, switched in the Good window, over all bars. | Stamps: the flame trails; Guys: the flame bends; Rolly: the next side shows later; Pony: the flame hangs, then catches up; Shot: the lighter tips; Couch: the flame drifts in late. | One switch for each bar of four beats. | check, trust, dance | S |
| 15 | **Flower Weave** | The poi trace a flower: hold the side the poi is on, and switch at each crossing.<br>Slow-motion poi tricks, one petal at a time. | Hold Left or Right; switch at each crossing. | Crossings switched in the Good window, held to the next one, over all crossings. | Stamps: the petals trail; Guys: the flower wobbles; Rolly: the crossing shows later; Pony: the poi hang at the top; Shot: the flower spins; Couch: the poi drag, then snap through. | One crossing for each two beats. | poi | M |
| 16 | **Microgrooves** | Four tiny games in a row, one each bar: "DODGE!", "CLAP!", "HOLD!", "MIRROR!".<br>WarioWare at the festival: read the word, do the thing. | The key each word names. | Bars done right over all bars. | Stamps: the words trail; Guys: the words wobble; Rolly: each word shows later; Pony: the word hangs before its bar; Shot: the words spin; Couch: the words fade in late. | Each tiny game lasts one bar. | dance, DJ, deck set, drum part | L |
| 17 | **Pass the Vibe** | A glowing ball of vibe passes around the crew.<br>When it reaches you, catch it on the beat and throw it on. | The key of the side it comes from; then the side it goes to. | Catches and throws in the Good window over all of yours. | Stamps: the ball trails; Guys: the ball curves; Rolly: the ball rushes in late; Pony: the ball hangs; Shot: the ball spins; Couch: the ball drifts in. | One catch or throw on each beat. | group dance | L |

Notes on the sizes:
- **S** reuses the arrow chart with a new screen and a small replay.
- **M** needs its own chart maker and replay, like DJ Ride.
- **L** needs more than the slot gives. Microgrooves needs the chart to mix games bar by bar. Pass the Vibe needs one chart
  across the crew's players, so each player's score depends on the others. Both would need a slot change, and so a protocol
  note.
- For the check dance (4 beats in about 2 s), few games fit. Hold the Pose and Lighter Sway fit it, and Arrow Lanes stays.

## First batch

Build five first, as the recommended answer to the first-batch question below:

1. **Arrow Lanes.** It is built. It stays in every pool, so every action keeps a game players already know.
2. **DJ Ride.** It is built. Moving it behind the slot proves the slot on a game that is not arrows, before any new game
   lands. It gets its dose twist at the same time.
3. **Call and Response.** It is the dance's own idea: copy the moves. It tests memory, not reading, so it feels most
   different from the arrows. It fits the dance and the trust dance, the two most common performances.
4. **Poi Orbit.** Poi get a game of their own, built on what poi look like: a circle. The paired poi rule (one draw for the
   pair) is tested here first.
5. **Drop Hold.** The only hold-and-release game in the batch. It gives the DJ set and the deck set a second game, and it
   is small (S).

Why these five together:
- Each one reduces to `RhythmInput` presses and releases in today's envelope, so after the slot entry no game changes the
  wire.
- Each one is drawn on the HUD, so the art freeze is safe.
- Every action except the check dance and the Drum Circle part then has at least two games, so the "never twice in a row"
  rule works. The check dance and the drum part keep Arrow Lanes until Hold the Pose and Drum Fill land.
- They cover four different skills: reading (Arrow Lanes), steering (DJ Ride), memory (Call and Response), timing a
  circle (Poi Orbit), and holding (Drop Hold).

The other twelve wait in this doc. Mirror Mirror, Drum Fill, Blind Bar and Air Horn are small (S) and good next picks.

## Decisions

Each block can go on the open-questions page as one card. The recommended answer is marked **(recommended)**.

### The entry's own decisions (answered)

**1. How random is "random"?** Answered: A.
- Situation: Today every performance is the same arrow game. You want a random game each time, while your character keeps
  dancing, DJing or spinning.
- Why it matters: Variety is the fun. But a game that makes no sense for the action (a typing game while DJing) can feel
  random in a bad way.
- **A (recommended, taken):** Random from the games that fit the action. Each game is tagged with the actions it suits,
  and the same game never comes twice in a row.
- B: Fully random: any game for any action.
- C: Each action gets its own fixed game (DJ = the ride, dance = arrows, poi = circles).

**2. How should the brainstorm run?** Answered: A.
- Situation: This item is the design, not the code.
- Why it matters: Your taste decides which games are fun, and the design sets the slot that every later game fills.
- **A (recommended, taken):** A wave writes this doc with 12-20 candidates and a recommended first batch, and you pick on
  the open-questions page.
- B: A live brainstorm session with you first, then the doc.

### Open questions

**3. What do we call these games, so they are not mixed up with the booth minigames?**
- Situation: "Minigame" already means the booth games that pause the festival (Mystery Cups, Drum Circle, Light Show).
- Why it matters: The name goes in code, docs and the queue. One word with two meanings causes bugs and confusion.
- **A (recommended):** Performance games.
- B: Groove games.
- C: Stage games.

**4. When two players perform together (a paired poi, a group dance), do they play the same game?**
- Situation: Today paired poi partners share one chart (`BeginPoi` copies the partner's seed). With a random pool, each
  could draw a different game.
- Why it matters: Playing the same game side by side is part of the fun of a pair. Different games make the group's score
  harder to read.
- **A (recommended):** One draw for the pair or the group: everyone plays the same game.
- B: Each player draws their own game.

**5. How long is each performance game?**
- Situation: Each action has a length today: a trust dance is 8 beats, poi 12, a deck set 16, a Drum Circle part 24, and
  the DJ set 5 phrases of 12.
- Why it matters: Keeping the lengths keeps the timing of sales, distractions and the crowd as it is. Free lengths give
  game designs more room.
- **A (recommended):** Each game fills the action's current window.
- B: Each game sets its own length, within about 4-30 s.

**6. Which keys can a performance game use?**
- Situation: Today every performance uses the four note keys, which you can rebind. The host scores only four directions,
  with press and release times.
- Why it matters: New keys mean new bindings to learn and new settings. The four keys keep every game playable at once,
  and as hard to cheat as today.
- **A (recommended):** The four note keys only (press and hold), keyboard first.
- B: Games may add their own keys, which you can bind.

**7. Must every game say how each of the six substances warps it?**
- Situation: Today each substance bends the arrow lanes its own way (trails, curves, spins, a shorter or longer
  look-ahead), and hit times never change.
- Why it matters: The dose twist is a big part of the game's identity. A game that ignores doses feels flat on a trip.
- **A (recommended):** Yes. Every game names a warp for all six, with the shared look-ahead as the fallback.
- B: Only where it fits. Other games play the same on any dose.

**8. What about Reduced motion?**
- Situation: The Reduced motion setting already calms the arrow lanes' dose effects.
- Why it matters: Some players need it. A game with no calm form would be unplayable for them.
- **A (recommended):** Every game must have a calm form.
- B: Games with no calm form are left out of the draw while Reduced motion is on. (The setting is on the player's own
  computer, so the host would need to learn it: one more thing on the wire.)

**9. Which games are built first?**
- Situation: This doc lists 17 candidates. A first batch of 4-6 gets its own queue entries.
- Why it matters: The first batch sets the feel of every performance for a while, and each game costs a build.
- **A (recommended):** Five: Arrow Lanes (today's), DJ Ride (DANCE-13), Call and Response (watch four moves, then repeat
  them), Poi Orbit (tap as the poi passes the mark, for poi), Drop Hold (hold through the build-up, let go on the drop).
- B: Four: A without Drop Hold.
- C: Another pick from the table.

**10. Does "never the same game twice in a row" count per player or per crew?**
- Situation: The picking rule says no repeat back to back.
- Why it matters: Per crew gives more variety on screen at once. Per player is simpler to follow.
- **A (recommended):** Per player.
- B: Per crew.
