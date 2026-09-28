# Camp interior visibility and tent finish — September 27, 2026

This checkpoint continues the [graphics upgrade plan](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). The [Dear Passengers teaser](https://www.youtube.com/watch?v=luWunttkCgY) remains the visual quality reference; this work does not close the playable sample or full-level art gates.

## Finding and change

The camp interiors occupied cells at x = ±70 m, inside the decorative woodland rise. In the [native tent capture before the move](graphics-interiors-2026-09-27/tent-before.png), the rise rendered across the floor and concealed the beds, pillow, and cooler. A PlayMode test confirmed that the rise renderer's bounds intersected the interior floor.

The cells now sit at x = ±125 m, beyond the rise and outdoor ground. The [revised native tent view](graphics-interiors-2026-09-27/tent-after.png) shows the floor and furnishings. I also gave the tent liner a canvas surface, visible stitched joins and bound groundsheet, rounded sleeping bags and pillows, wall pockets, and a more structured hanging lantern. A short vestibule closes the empty-world view from the [interior exit](graphics-interiors-2026-09-27/tent-exit.png) while the player can still walk through the near threshold.

## Verification

- The new PlayMode clearance test failed on the old x = ±70 m cells and passed after the move; all 7 PlayMode tests now pass.
- All 28 EditMode tests pass. The macOS development build and native solo smoke pass, including car, tent, and porta potty entry and exit, tent walk-out, purchase, and mission continuation.
- The captured room and exit views are from the rebuilt native player at 1280×720. They prove visibility and local composition at those camera angles, not trailer-level polish or sustained performance.

The interior still uses a simple runtime room shell and visibly modular props. It needs stronger authored structure and materials, real close-up art review, comparison motion clips, measured draw/triangle cost, and human-paced inspection. The camp exterior and other level locations remain below the reference's finish. M2 and later gates stay open.
