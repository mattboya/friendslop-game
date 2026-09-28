# Camp and location follow-up — September 27, 2026

This is a checkpoint within the [graphics upgrade plan](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). The supplied trailer remains the visual quality target; this checkpoint does not close the M2 sample gate.

## What changed

- Moved the primary interaction and checkout cards into the upper-left information rail. The center and lower part of the first-person view remain open for hands and held objects.
- Shifted the first-person hands upward and changed the poi to a palm-side handle, visible short cord, and weighted head with a damped cosmetic tether. The head should lag the hand when moving; gameplay authority is unchanged.
- Made Little Spoon passive throughout the loadout: it renders as a character necklace, cannot be equipped, does not occupy a numbered hand slot, and does not use one of the three hand-gear capacity slots.
- Added larger readable stitches, cuffs, garment bands, pocket detail, and first-person sleeve trim to the original Blender character and hand sources.
- Expanded the camp to six cars and eight tents. Two new original exports give it a camper van and dome tent alongside the hatchback and A-frame. Car and van bodies have cut wheel openings, distinct windows, doors, lamps, and trim. Existing cars retain color and roof-cargo variation.
- Assigned a separate interior coordinate cell to every car, tent, and porta potty doorway. Players sharing one site see each other inside; players in other sites stay out of that room's view. The three interior types retain their silly actions, moving prop reactions, and synthesized sounds. Refined the visible car cabin, tent liner, and porta potty furnishings.
- Kept the host-selected camp DJ loops and the post-round crew award vote that gates the next shop. The round review records the previous result, sales, survivors, and camp antics before shopping resumes.
- Rebuilt the medical tent, security booth, and shuttle shells with shaped roofs, construction detail, usable openings, and more legible signs. Their interaction and collision anchors are unchanged.

## Visual evidence

The [hatchback](graphics-camp-followup-2026-09-27/hatchback-neutral.png), [camper van](graphics-camp-followup-2026-09-27/camper-van-neutral.png), and [dome tent](graphics-camp-followup-2026-09-27/dome-tent-neutral.png) are neutral Blender views of the published source. They demonstrate shape, not native lighting or gameplay contact.

| Location | Earlier native view | Revised native view |
| --- | --- | --- |
| Medical | [Before](graphics-locations-2026-09-27/before-medical.png) | [After](graphics-locations-2026-09-27/after-medical.png) |
| Security | [Before](graphics-locations-2026-09-27/before-security.png) | [After](graphics-locations-2026-09-27/after-security.png) |
| Shuttle | [Before](graphics-locations-2026-09-27/before-shuttle.png) | [After](graphics-locations-2026-09-27/after-shuttle.png) |

The location “after” views came from a successful scripted two-client macOS development run after the new location exports were integrated. That run preceded the final interior, necklace-slot, wheel-opening, and poi-arc adjustments. The Mac graphical session locked before the final native screenshot run could finish; fresh final captures are still required.

## Asset cost and verification

| Export | Triangles | Renderer groups |
| --- | ---: | ---: |
| Hatchback | 4,136 | 7 |
| Camper van | 4,260 | 7 |
| A-frame tent | 2,172 | 6 |
| Dome tent | 4,260 | 7 |
| Medical | 4,236 | 9 |
| Security | 3,340 | 7 |
| Shuttle | 5,760 | 11 |

The final source passed project static checks, domain tests, Unity validation, EditMode and PlayMode tests, and macOS development and release builds. The release runtime excludes development diagnostics. A previous native solo smoke covered car, tent, and porta potty entry, antics, DJ selection, poi, purchase, mission completion, review vote, and next shopping; a previous two-client smoke covered the full cooperative mission and the three location views. The final build's solo screenshot run stopped when the Mac locked. The game was launched again from the final development build on port 7777 for the user's visual review when the desktop is unlocked.

The last successful two-client point samples, from the intermediate build at 1280×720 Ultra on the baseline Mac, were 8.8 ms rolling p95 in camp and 16.7–16.8 ms in populated festival views. Those samples do not measure the final car and interior changes or establish sustained target hardware performance.

## Remaining work

The vehicles and rooms are still deliberately stylized and need human acceptance against the reference. The final poi arc, cabin and tent views, and doorway-specific interior routing need native screenshots after desktop unlock. A human-paced solo and cooperative walk, target Windows profiling, clothing fit in motion, stronger material and lighting finish, and the broader M2 visual quality gate remain open.
