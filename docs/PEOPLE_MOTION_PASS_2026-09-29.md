# People motion pass — September 29, 2026

This pass makes the festivalgoers' movement read as people moving, not wind-up toys. Everything is presentation only: no animation moves collision, the gameplay root or authority.

## Changes

- **Gait (`FestivalGait.cs`, `FestivalFootPlant.cs`).**
  - Stride and stance come from speed and each actor's measured leg length, using dynamic similarity (Alexander's relative stride against the Froude number).
  - At the game's 4 m/s base speed, a 0.55 m leg now lands about 4.7 steps/s instead of about 7, and a 6 m/s sprint lands about 5.4 steps/s.
  - Stance shrinks from 60% to 27% as a walk becomes a run, which adds a flight phase and keeps planted feet inside these short legs' reach.
  - Feet land half a stance ahead of the hips. Swing height rises with speed.
  - A walk rides highest over the planted foot and a run lowest.
- **Walk and run body (`FestivalCharacter.cs`).**
  - Arms swing opposite the landing foot. Before this pass they were a quarter cycle out of step with the planted feet.
  - The pelvis turns with the stride while the shoulders counter-rotate, and the head stays level.
  - Running adds forward lean, elbows bent to about 85°, bigger arm swings and knee lift.
- **Standing life.**
  - Weight drifts from foot to foot through the planted legs, the chest breathes, and idle heads glance around, each actor on its own rhythm.
  - A third of actors stand with their hands on their hips.
  - Glances stay off during wook stares.
- **Authored acting clips (`FestivalMotionLibrary.cs`).**
  - The package 03/04 clips drive the runtime characters directly, because both rigs match bone for bone.
  - They are baked once through a Playables graph. `AnimationClip.SampleAnimation` leaves non-legacy clips at rest in players, although it works in the editor.
  - Clip mapping:

    | State | Clip |
    |---|---|
    | Idle | Idle |
    | Downed | CrawlDowned, or BeingDragged while being dragged; full body, so the body lies on the ground and no longer uses a −0.4 m root offset |
    | Drag | DragOther |
    | Detained | DetainedEscort, hands behind the back |
    | Watching | WookStare |
    | Questioning | Talk |
    | Accusing | WookLockedOn |
    | Swarming | SwarmLunge |
    | Extract | Cheer |
    | Handoff | Handoff |

  - Upper-body clips leave the legs to the gait and foot plant.
  - Rescue keeps its clearer procedural reach.
  - The runtime copies live in `Resources/FestivalMotion.fbx` and `Resources/FestivalMotionActing.fbx`.
- **Dance and poi.**
  - Hips roll over the supporting foot. Each style has its own upper body: hakken elbow pumps with an overhead fist, shuffle running-man arms with a clap phrase, and jumpstyle pendulum arms with a raised V.
  - Styles alternate across longer phrases so neighbours rarely match.
  - Poi arms trace circles from the shoulders while the torso follows.
- **Transitions.** Pose changes ease out of a snapshot of the previous pose over 0.22–0.5 s, instead of snapping toward the new target. Standing feet re-step sooner while turning in place.

## Carried poi

Equipped or held poi were a single rig on the right palm. In first person the head was sprung toward a point beside the hand under 28% gravity, so it hovered level with the hand.

Poi are now a pair, one per hand, in both views. While walking with poi equipped, each head wheels in a vertical circle beside its hand, and the two hands run half a turn apart. The pace follows gravity: tests measure 542°/s through the bottom against 287°/s over the top.

In first person the wheel leans out and down, so heads sweep beside and below the hands, and the heads are drawn at 70% size. A poi performance switches the same pair to the butterfly.

The wheel only turns while the carrier is moving: above 0.4 m/s in third person, or while the first-person camera travels. Standing still, each head is a damped pendulum under full gravity. After a stop it swings down and settles within about 10° in two seconds. Starting to walk picks the wheel up from where the head hangs and brings it to speed over 0.4 s, so both heads start together.

A dance challenge with poi equipped keeps the dance style's footwork, while the arms switch to butterfly circles on that style's beat and the heads orbit with them. The HUD's live dancer view shows the local player the same thing.

PlayMode tests cover the third- and first-person pairs, hanging still, and poi in a dance. The dance test measures 0° of footwork difference from the same dancer without poi. `--motion-review` adds a `poi-dance` sequence and records walk-then-stop for both poi views.

## Evidence

- New EditMode `GaitTests` cover cadence against leg length, the walk-to-run stance change and planted-foot reach.
- New PlayMode `MotionNaturalnessTests` measured:
  - Arm-to-opposite-foot correlation 0.95, pelvis 0.94, shoulders −0.95.
  - Jog cadence 4.70 steps/s.
  - Elbow bend 23° walking against 85° running, and forward lean 0.04 against 0.17.
  - Idle head yaw range 56°, hip sway 2.7 cm, and feet moved 0 mm.
  - The three dance styles have distinct upper-body averages.
  - Every acting state departs the rest pose, a downed hips height of 0.36 against 0.88, and detained hands behind the spine.
  - Pose changes ease in: 1.1° after the first frame against 46° at 0.12 s.
- Totals: EditMode 33/33 and PlayMode 26/26. Unity validation, static checks and release diagnostic exclusion pass, and the release build excludes the review capture modes.
- The native solo smoke passed in 68 s. One earlier attempt hit its 160 s deadline while the machine was under heavy unrelated load.
- The development player logs `clips_baked count=22 moving=21`; WookStare only turns the head.
- `--motion-review <dir>` in a development build records deterministic 30 fps sequences: walk, jog and run tracking shots, idle, acting states, dance, and a transition chain. Encoded reels are under `artifacts/motion-review/`, which is ignored.

## Remaining gaps

- The authored lying clips push some rigid garments through the floor. A flared skirt can sink up to 33 cm and vendor aprons about 13 cm. That needs garment reweighting.
- The Walk and Run clips are unused; locomotion stays procedural so it can follow real displacement and foot planting.
- BoardShuttle, Sell and Consume have no gameplay hook yet.
- Human-paced review, eight-player crowd performance and final motion polish remain open.
