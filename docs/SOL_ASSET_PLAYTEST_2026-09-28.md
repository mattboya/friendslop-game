# Sol playtest: arms, grips, expressions and handoffs

## Scope and status

This pass focuses on Blender assets and the small amount of code needed to use them. It does not establish that the overall game matches the reference video's quality.

Created/updated:

- `ArtSource/Generated/FestivalHands.blend`: continuous tapered forearms, independent lowered left/right arm shapes, matching sleeve shapes, existing item-specific fingers retained. Empty arms lower; the unused arm lowers when carrying in one hand. Walking/turning adds small arm movement without moving the camera.
- `ArtSource/Generated/FestivalCharacter.blend`: connected finger geometry, independent grip and opposing-thumb shapes, smile and concern shapes following the face surface. Detailed and distant FBX exports updated.
- Offering/receiving presentation, short receipt presentation after successful purchases/transfers, and expression/grip controls. Reuses the existing two-bone hand contact solver. Visual animation does not grant inventory or move player collision.
- Blender sources save with neutral pose sliders. Independent shapes start from Basis so enabling both hands does not double-deform one side.

The third-person grip is currently one shared curl. First-person grips are item-specific. Check close-up intersections before accepting the shared third-person curl. At maximum transfer distance the arm solver clamps to natural arm length, so exact palm-to-palm contact is not guaranteed.

## Build and launch

Workspace: `/Users/mboyajian/Code/friendslop-game`.
Development app: `Builds/macOS/Development/FestivalCoop.app`.
Quit old game instances before launching the rebuilt app; an already-running instance still uses its old assets.

For a local host/client pair:

```sh
node scripts/launch-local-clients.mjs --players 2 --port 7787
```

If code/assets change after this handoff, rebuild with:

```sh
node scripts/unity.mjs build-mac-development
```

## Playtest in this order

1. **Empty hands:** unequip everything; idle, walk, sprint, strafe, turn quickly and stop. Arms should rest below the central view, move gently, and settle. No hands frozen ahead, sleeve gaps, stretched fingers, or camera bob from this system. Test looking straight ahead, up and down.
2. **Carrying:** equip bag, tin, map/pass/voucher, confetti and each poi. Right-hand items should raise the right arm while the unused arm stays low. Map raises both; poi performance raises both. Switch and drop items repeatedly while moving. Watch the first half-second for props separating from fingers or passing through sleeves.
3. **Purchase:** pick up shelf gear, carry it to the seller and buy it. Verify the held prop, brief receipt presentation, then return to selected equipment. Buy while already carrying a different equipped item. Little Spoon remains a necklace and never a hand prop.
4. **Two-player transfer:** watch both views and the other character. Giver displays the offered object, receiver reaches with an empty hand, accepted item briefly presents and then returns to equipment. Test close range, maximum permitted range, facing away and turning. Cancel/expire the offer, disconnect, reject with full inventory, and retry acceptance. No stuck reaching or visual duplicates; verify inventory quantities and cash separately.
5. **Character close-ups:** inspect fingers/thumbs, smile while dancing/after receipt, concern under threat/downed. Check several body/face/clothing variants, with and without glasses/hats. Look for floating facial lines, sleeves exposing joints, collapsed fingers, or excessive expression blending. Walk beyond 10 m and back within 8 m to exercise detail switching.
6. **Poi and dancing:** record 10–15 seconds from first and third person. Handle stays in palm; rope and end ball move independently; hands do not stretch or twist. Check starting/stopping performance while carrying another item. Judge rhythm and weight against the supplied reference clips.
7. **Focused regressions:** enter/leave a tent, car and porta potty; transfer inside one shared interior; try DJ interaction then stop; finish a round and reach review/shopping. Check that arms recover after each action and interaction text leaves useful view space.

## Evidence to return to Astra

For each failure: player profile/appearance, item, action sequence, first/third-person view, screenshot or short recording, and relevant development log. Prioritize visible attachment gaps, obstructed view, deformation and unnatural motion. Do not mark visual quality accepted based only on automated tests.

## Checks performed during authoring

- Unity PlayMode: 19 passed after the final first-person rest correction, including empty-arm lowering, unused-arm rest, finger deformation and prop motion/visibility.
- Blender shape audit: 50 nonzero arm/grip/expression shape checks; solid-view inspection of raised/resting arms and both character grips. Fixed cross-hand additive deformation found during inspection.
- Final character grip correction inspected in Blender and included in the development build; full native gameplay and multiplayer visual acceptance are delegated to this checklist.
- Debug-only interaction events use `InteractionVisuals` (`exchange`, `finger_grip`, `first_person_equipment`).
