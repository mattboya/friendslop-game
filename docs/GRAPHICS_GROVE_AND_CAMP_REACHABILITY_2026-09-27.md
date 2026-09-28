# Grove and camp reachability checkpoint — September 27, 2026

This is another checkpoint within the [graphics upgrade plan](GRAPHICS_UPGRADE_PLAN_2026-09-27.md), not acceptance of its M2 visual target.

## Changes

- Added an original 7.5 m fir with overlapping needle boughs and visible branch structure. Mixing it with the existing broadleaf forms breaks the repeated round canopy line in camp and the first festival.
- Added a small authored ground assembly with irregular stones, fern fronds and seed stems. The world scatters it at quiet margins, away from central routes and camp doors. These are visual objects without gameplay collision.
- Expanded the playable camp ground and collision boundary to enclose all six cars and eight tents. The newest van and dome tent sites had been placed beyond the old 24 m boundary. Separate interior cells now sit at x = ±70 m, outside the expanded outdoor camp.
- Added a PlayMode navigation check from camp center to every car, tent and porta potty approach, plus an EditMode footprint assertion. This checks reachability without claiming a human walking inspection.
- Added a low, rolling distant woodland mesh behind each map's tree line. Its tint matches the distant ground so the ridge does not form a hard dark ring in the overhead camp view. It has no collision and adds one renderer per map.
- Reanchored first-person gear to the calculated palm centers in camera space. Poi offered for sale now uses the same handle and weighted tether as equipped poi. Selected stock sits in the fist; a shorter, outward poi arc leaves its cord and ball visible beside the hand. Disabled automotive glass specular highlights after the van's windshield washed out in native lighting.

## Asset review and cost

The [fir](graphics-grove-2026-09-27/fir-neutral.png) and [ground cluster](graphics-grove-2026-09-27/ground-cluster-neutral.png) images are neutral Blender renders of the published source. The two-client native run captured the [grove](graphics-grove-2026-09-27/grove-native.png), [camp overview](graphics-grove-2026-09-27/camp-overview-native.png), [van](graphics-grove-2026-09-27/van-native.png) and [dome tent](graphics-grove-2026-09-27/dome-tent-native.png) in gameplay lighting. A solo native comparison shows the [earlier poi contact](graphics-grove-2026-09-27/poi-before-grip-native.png) and [revised grip and cord](graphics-grove-2026-09-27/poi-after-grip-native.png), plus [stock held in the fist](graphics-grove-2026-09-27/stock-held-native.png) and the [round review](graphics-grove-2026-09-27/round-review-native.png). These images verify integration while also showing that car surface finish, interior furnishings, horizon composition and some foreground clusters still need art direction work.

| Export | Triangles | Renderer groups |
| --- | ---: | ---: |
| FestivalTreeFir | 1,490 | 3 |
| FestivalGroveDetail | 1,722 | 5 |

The art pipeline staged the complete source kit; only the two new runtime FBXs remain changed because the existing assets' geometry and manifest counts were unchanged. Their new Unity GUIDs are stable. Domain, static project, Unity validation, EditMode and PlayMode checks passed; development and release macOS builds passed, as did the release diagnostic exclusion check. Solo and two-client native scripted missions passed while the Mac was locked. The solo run included car, tent and porta potty interiors, camp antics, DJ choice, poi, round review and next shopping. It does not replace a human walk through the scene.

At 1280×720 Ultra with two processes on the baseline Mac, the final scripted graphics samples reported rolling p95 9.2 ms in camp and 16.7–17.5 ms across populated festival points. These are point samples, not sustained target-hardware measurements; populated views still reach or exceed a 16.7 ms frame budget. Do not increase woodland density without profiling and reducing cost elsewhere.

## Next inspection

With an unlocked desktop, inspect the camp van and dome tent approaches, first-person poi and prompt placement, and the ordinary market-to-stage route at a human pace. Check frame time on target hardware before adding more forest density. The overall scene still needs stronger materials, lighting, motion and composition to meet the reference trailer.
