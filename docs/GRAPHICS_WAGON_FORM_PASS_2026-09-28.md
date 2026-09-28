# Camp wagon form checkpoint — September 28, 2026

This is an environment-object pass within the active [graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). The [Dear Passengers teaser](https://youtu.be/luWunttkCgY) remains a quality reference. No footage, meshes, textures or design files from it were imported.

## Change

- Replaced the wagon's bevelled rectangular body with an original longitudinal panel loft. Its nose and tail narrow, the beltline and bonnet rise toward the cabin, and the sides form around the existing wheel openings. The gameplay footprint and interaction positions stay the same.
- Rebuilt the cabin as a tapered, raked glasshouse with a crowned roof. The windshield, side panes and rear hatch glass now sit outside their painted panels and have visible rubber surrounds. The side pillars follow the changing roof width.
- Preserved the shared color variations and roof cargo used by the four camp wagons. Only the wagon FBX changed; the camper, tents and other world exports were restored after generating the updated master source and manifest.

## Inspection

The [old neutral three-quarter view](graphics-vehicles-2026-09-27/wagon-neutral.png) and [new neutral view](graphics-wagon-form-2026-09-28/wagon-neutral.png) show the changed body and cabin. [Front](graphics-wagon-form-2026-09-28/wagon-front.png), [side](graphics-wagon-form-2026-09-28/wagon-side.png) and [rear](graphics-wagon-form-2026-09-28/wagon-rear.png) views check pane seating and silhouette from all directions. The [old native camp view](graphics-vehicles-2026-09-27/wagon-native.png) and [new native camp view](graphics-wagon-form-2026-09-28/wagon-native.png) show the result under the actual dusk lighting at 1280×720.

The model increased from 4,132 to 4,836 triangles per wagon, with eight material renderers unchanged. Four wagon instances add roughly 2,816 source triangles before culling and shadow passes. This is a geometry count, not a performance benchmark.

## Validation and remaining quality work

The original Blender source, manifest and changed FBX were staged, visually inspected and explicitly published with the existing Unity GUID intact. Project static checks, Unity validation, macOS development and release builds, and release diagnostic exclusion passed. A scripted two-client native mission produced the new camp capture. The scripted solo route passed car, tent and porta potty interior entry/exit as well as the mission and post-round review.

The wagon is more recognizable but still has simplified lights, tires, interior, paint response and environmental contact. The foreground camper, tents, trees, ground composition, and character interaction with vehicle doors are below the reference quality. No full human-paced visual acceptance or sustained Windows performance result is claimed.
