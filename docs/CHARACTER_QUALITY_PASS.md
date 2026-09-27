# Character quality revision — September 26, 2026

The character family keeps the approved goofy adult proportions and random wardrobe, with rebuilt geometry for conversational distance and the live dance portrait.

## Shape and construction

The master is approximately 2.3 m tall before the existing runtime scale. The head occupies roughly one quarter of that height. The 13 animation joints and all existing wardrobe names remain the shared contract.

- Shaped jaw, cheeks, cranium and nose replace the ellipsoid head and separate bulb nose. Iris, pupil, catchlight, thin eyelids, brows and smile details form the face. A Blink shape controls both eyes.
- Connected, smoothly weighted arm and trouser surfaces cross elbow and knee joints. Palms, four fingers and thumbs replace the large hand blobs.
- Garments have shoulder and neck transitions, fitted sleeves, hems, pockets, drawstrings, seams and zipper details. Staff outerwear follows the same body surface.
- Footwear has a tapered heel, curved vamp and toe, contoured soles, laces, tongues and heel tabs. The original large rectangular foot blocks are removed.
- Eyewear has shaped lenses, rims, bridges and temple arms. Hair follows the skull and retains the separate hat-compatible layers. Bags have curved bodies and attached straps; headphones have an arched band.
- First-person hands use smoother geometry. Skin, fabric, hair, equipment and eyewear receive separate runtime surface responses. Fabric color cells contain subtle original grain and mipmaps.

## Reproduction and geometry

`generate_modular_character.py` writes the detailed FBX and the Blender master, then derives a distance FBX without overwriting the detailed source. `character-manifest.json` records triangle counts for every group in both versions. Both versions retain the same bind poses, bone order, wardrobe names and six fit keys. Shape controls are explicitly reset before export and when the runtime selects a fit.

The detailed file contains 182,576 triangles across all mutually exclusive variants. The distance file contains 58,371. A conservative sum of the most expensive options, including mutually exclusive headgear and top hair, is 44,804 detailed triangles or 14,323 distant triangles. These are mesh budgets, not full-frame draw counts.

Runtime changes to the distance meshes beyond 10 m and returns to detailed meshes inside 8 m. The live dance portrait always uses detailed meshes. Meshes are shared across actors. The native desktop build uses Unity's GPU mesh deformation mode; see the [Unity mesh deformation API](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/playersettings/meshdeformation).

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python scripts/generate_modular_character.py
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python scripts/generate_festival_hands.py
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python scripts/audit_character_fit.py
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python scripts/review_character_quality.py
node scripts/unity.mjs build-mac-development
node scripts/native-smoke.mjs
```

`review_character_quality.py` produces disposable neutral-light front, back, side, top, bottom and face inspection renders. Its cameras and review materials are not exported into the game. The original pre-revision generator, manifest and native capture are retained in `artifacts/character-quality-before/`.

## Evidence and limits

The geometric audit passes six body profiles, 306 fit-key checks, 108 hair/hat pairs, torso/leg/shoe/role coverage, backpack interfaces, and 114,788 sampled skirt-pose vertices. This is measured coverage of the audit's samples; it is not an exhaustive proof for every combination and interpolated pose.

Native smoke captures include the live dance view, six staff roles, six generated outfits, a closer three-body lineup, and matching detailed/distant characters at the same on-screen scale. The distance comparison deliberately magnifies the distant mesh for inspection. Tests verify compatible bone order and bind poses, fit preservation when switching detail, portrait detail locking, and inactive unused shape controls.

Human visual acceptance, expressive dialogue faces, richer age variation and polished authored dance choreography remain open. The current pass uses original procedural modeling and animation, with no downloaded reference-game assets.

The final two-client macOS smoke run passed the complete mission, quality gallery and distance-switch checks. During its rhythm segment the client recorded 17.4 ms p95, 50.0 ms maximum over 442 frames at 1280×720, with two processes running on one Mac. The initial all-detailed revision recorded 39.5 ms p95. These are scripted-run observations, not an isolated benchmark or a promise of sustained 60 fps on every target.

Final Unity regression results: 17/17 EditMode and 6/6 PlayMode tests passed. Static project verification and the macOS development build also passed.
