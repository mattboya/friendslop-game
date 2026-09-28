# Poi, dance, and player DJ checkpoint — September 28, 2026

This checkpoint advances the active [graphics upgrade plan](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). The user supplied a [poi spinning example](https://www.youtube.com/shorts/eCaw93VEEQk) and a [rave dance compilation](https://www.youtube.com/watch?v=hG0DZpcLNbk) as motion references. The models, poses, and effects remain original project work.

## Changes

- The two poi heads now travel in opposed vertical orbits, trading high and low positions on taut cords while the shoulders and wrists exchange the lead. The first-person camera hands drive the same spin; previously their poi hung still because they had no character transform to provide an orbit frame.
- In-place dance poses now have alternating planted and lifted feet, body bounce, and style-dependent short steps. The visual leg solver uses each actor's actual leg lengths and resets its contact targets after a large presentation move. It does not move an authoritative player or NPC root.
- A small stage-front deck sits at the existing DJ takeover landmark. Starting the interaction from close range places the player at its operating position. The player's world avatar faces the deck and uses two-bone visual hand contact with its platter and mixer. The resident DJ remains at the raised stage console.
- Poi and DJ rhythm challenges now show the live third-person performer view. Its heading identifies the activity, and the DJ view uses a side angle to show both hands on the equipment.
- Development builds log first DJ hand contact and unreachable animation targets. These diagnostics are guarded out of release builds.

## Evidence

- Native captures: [player DJ contact](graphics-poi-dance-dj-2026-09-28/player-dj-contact.png), [poi phase A](graphics-poi-dance-dj-2026-09-28/poi-0.png), [poi phase B](graphics-poi-dance-dj-2026-09-28/poi-1.png), [dance phase A](graphics-poi-dance-dj-2026-09-28/dance-0.png), and [dance phase B](graphics-poi-dance-dj-2026-09-28/dance-2.png). These are still frames from a native player; they do not establish the feel of the full loops.
- Domain, project static, EditMode, and PlayMode checks passed. New tests cover takeover placement, actual deck hand reach, opposed poi phases, first-person poi orbit, dance support and lift, and the existing stage navigation route.
- The scripted native two-client mission and connection UI capture passed. The final run reported no foot or DJ hand overreach warnings. Its rhythm section measured 17.5 ms p95 at 1280×720 on Ultra while two macOS player processes ran on one machine. This short sample is not a sustained 60 fps or minimum Windows hardware result.

## Still open

The poi and dance are stylized procedural approximations, not authored motion copied from the references. A human in-motion review should judge crossover timing, ankle contact, transitions, clipping with clothing, the first-person poi near the camera, and whether the DJ station reads clearly during a real interaction. The full vertical slice and named target-hardware performance gate in the graphics plan are still open.
