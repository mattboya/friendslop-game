# Second production package — stage and clue landmarks

September 29, 2026. Authored in Blender; Unity/native testing assigned to the existing Sol testing chat. Presentation renders are not gameplay captures or acceptance evidence.

## Deliverables

- `ArtSource/ProductionPackage02/SecondProductionPackage.blend`: editable `AH02_Production` scene.
- `ArtSource/ProductionPackage02/package-preview.png`, `dj-preview.png`: overview and controller detail.
- `ArtSource/ProductionPackage02/manifest.json`: geometry counts and exported control/interaction anchors.
- `Assets/Festival/Art/ProductionSample02/`: six original FBX assets plus material palette.
- `scripts/create_second_production_package.py`: repeatable isolated generator. CONFIG occupies the first five lines; shared geometry helpers are loaded as functions from the first package, without executing its generator.

| Asset | Authored features |
|---|---|
| Stage | 18 × 9 m deck, 1.4 m top, seven steps with handrails, braced trusses, supported pitched canopy, folded wings, halo, After Hours sign, seven wash fixtures |
| DJStation | Moonloop console with two platters, sixteen pads, four faders, twelve EQ knobs, recessed screens and cable exits |
| SpeakerStack | Three recessed drivers, baffle openings, rolled surrounds, fastening hardware and carry handles; instantiate two |
| FlightCase | Corner rails, lid seam, butterfly latches, handle and four casters |
| SunTotem | Ray silhouette, inset ring, capped pedestal and SUN plaque |
| MoonTotem | Cut crescent, metal mounts, inset accent, capped pedestal and MOON plaque |

Static geometry is joined by material for export while source parts stay editable. Moving controller parts are grouped by pivot and material. Controls have named pivots, not authored runtime behavior. Clue and hand-contact empties are placement aids, not working interactions. No third-party meshes, reference-game content or textures were copied.

## Reproduction and staging

From the project root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python scripts/create_second_production_package.py
```

Metres, Blender Z up, fronts -Y; exported Unity Y up, fronts +Z. Exports are authored at their own local origin before the Blender review composition is arranged. Palette names match FBX material slots; use URP Lit colors/metallic/smoothness and enable emission only for emitting entries. No baked lighting, texture library, LODs or collision setup is included.

The review composition uses these Unity positions, with identity rotations:

| Asset | Unity position |
|---|---|
| Stage | (0, 0, 0) |
| SpeakerStack × 2 | (-9.9, 0, 0), (9.9, 0, 0) |
| DJStation | (0, 1.4, -0.1) |
| FlightCase | (-5.58, 1.4, -0.3) |
| SunTotem | (-6.66, 0, 7.3) |
| MoonTotem | (6.66, 0, 7.3) |

Ground, camera and presentation lights are excluded from FBXs. Use a separate test scene and preserve the user's currently open scene. Existing game stage facing differs: a future main-scene replacement requires an explicit rotation/anchor mapping, with stair access, resident DJ, takeover interaction, camera and collider contacts checked together. Do not globally replace Resources models as a shortcut.

## Sol evaluation

1. Import six FBXs, inspect hierarchy, facing, size, normals, material assignment and renderer counts. Confirm joined controller meshes stay attached to the correct pivot when platters/knobs rotate and faders/pads translate. Inspect both sides and underside, not just the presentation view.
2. Build an isolated Unity review fixture using the placements above and existing festival URP/dusk lighting. Keep any diagnostics editor-only or inside development-build guards. Do not alter main build settings or unrelated gameplay changes.
3. Check stairs and deck dimensions against player scale; establish explicit collision proxies before claiming walkability. Inspect roof/truss contacts, stage sightlines, sign readability, speaker driver depth and front-facing material behavior.
4. Inspect controller spacing and hand-contact anchors at actual gameplay camera distances. Controls are not connected to DJ gameplay and no IK reach or interaction acceptance is claimed.
5. Inspect Sun/Moon differentiation at gameplay distance with emission disabled as well as enabled. Private clues must remain player-local if the fixture connects them to the game's clue system; shared model silhouettes reveal location identity only.
6. Capture native views, report visible defects and measured renderer/frame costs with honest scene population. Run only appropriate checks for fixture/import changes. Preserve the first package's report and outstanding defects separately.
7. Save a second-package report with confirmed passes, authoring fixes needed, limitations and captures. Keep asset redesign in the authoring chat; minimal fixture/import fixes are within testing scope.

Shared workspace has active unrelated simulation, HUD, poi and test edits. Preserve them. This authoring package does not replace gameplay assets, change rules or assert multiplayer/performance readiness.
