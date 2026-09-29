# Asset playtest results — 2026-09-29

Checklist: `SOL_ASSET_PLAYTEST_2026-09-28.md`. Tested the macOS development player with a native two-client scripted run, a solo scripted run, Unity tests, image review, and a live app view. The scripted runs teleport to objectives; they do not establish human-paced movement, feel, or visual acceptance. The supplied poi and rave-dance clips were viewed as motion references.

| Checklist area | Result | Evidence and remaining issue |
| --- | --- | --- |
| 1. Empty hands | Partial | Live first-person idle view puts fists at the lower edge, clear of the central view. `artifacts/native-smoke/host-walk-motion-*.png`, `host-stop-motion-*.png`, and `host-turn-motion-*.png` show scripted third-person locomotion. Sustained WASD/sprint/strafe and looking up/down were not exercised by a human. |
| 2. Carrying | Partial | First-person captures for bag, tin, map, pass, voucher, confetti, practice poi, and LED poi are in `artifacts/native-smoke/host-grip-*.png`. Bag handle and tin contact read clearly. The confetti cannon occupied too much of the view; its held scale was reduced and the final capture reviewed. Switching and dropping while moving remain unverified. |
| 3. Purchase and Little Spoon | Partial | Solo native smoke passed shelf pickup, seller purchase, equipment and receipt display (`solo-equipped-camp.png` in the app smoke directory). EditMode confirms Little Spoon cannot equip in a hand and does not consume a gear slot. Buying while carrying another distinct item was not visually exercised. |
| 4. Two-player handoff | Partial | Native host/client run accepted a bag transfer inside the same tent and captured both views (`artifacts/native-smoke/*-interior-handoff.png`). The HUD exposed Accept and Cancel actions. EditMode covered shared-room range, accepted quantity and cash, duplicate command, cancel, 15-second expiry, disconnect return, other-room rejection, and full-inventory rejection. Turning/facing away, maximum-range palm contact, and post-accept third-person reach were not visually accepted. |
| 5. Character close-ups and LOD | Partial | `artifacts/native-smoke/host-character-quality.png`, `host-fit-outfits.png`, `host-face-states.png`, `host-item-grips.png`, and `host-character-distance.png` show several variants and detail-distance probes. Close inspection still shows simple stylized fabric and hair surfaces. A real 8–10 m walk across the LOD switch was not observed. |
| 6. Poi and dancing | **Fail visual target** | `artifacts/native-smoke/host-poi-motion-0.png` through `-4.png` show the heads orbiting and the cord now reads thinner, but the arms remain mostly held ahead of the chest, with little visible body weight transfer. The five sampled frames are under a second, not the requested 10–15-second recording. The motion does not yet resemble the range and flow in the supplied poi clip; dance motion also lacks the grounded footwork and changing weight seen in the rave clip. |
| 7. Focused regressions | Scripted pass; human pass open | Solo native smoke entered/exited cars, tents, and porta potty, performed camp antics, changed DJ music, bought gear, finished a round, opened review, voted, and returned to shopping. The two-client smoke passed an interior handoff. `solo-tent_1-exit.png`, `solo-camp-review.png`, and native logs preserve evidence. Tent exit was mechanically verified by walking through its doorway. HUD prompts were visible and above the lower hands in reviewed captures. |

## Issues fixed during this pass

- Camp handoffs now work during shopping, including inside a shared tent, car, or porta potty. Players in separate interiors or outside the room cannot hand off. The giver can cancel; outstanding offers also expire during shopping and return before round launch.
- Arriving players receive separate interior standing spots, so one avatar no longer fills the other player's camera on entry.
- Poi cord width changed from a thick stick-like line to a thin cord. The confetti cannon's first-person held size was reduced.
- The native two-client smoke now exercises and captures the interior transfer; the sender/receiver action buttons are checked. The smoke uses the authoritative host state for the recipient inventory because other players' inventory is intentionally private in each client view.

## Verification

- Unity EditMode: 31 passed, 0 failed (`artifacts/editmode-results.xml`).
- Unity PlayMode: 19 passed, 0 failed (`artifacts/playmode-results.xml`).
- Rebuilt `Builds/macOS/Development/FestivalCoop.app` and passed the two-client native smoke (`artifacts/native-smoke/host.log`, `client.log`).
- Solo native smoke passed on the final build (`artifacts/native-smoke/solo.log`).

The next visual acceptance pass needs sustained first-person movement and a 10–15-second first/third-person poi and dance recording, plus a two-person handoff while turning and at maximum range. The current scripted captures cannot answer those feel and motion questions.
