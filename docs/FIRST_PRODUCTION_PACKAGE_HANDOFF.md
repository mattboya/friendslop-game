# First production package — Sol testing handoff

Date: September 29, 2026. Status: authored package, NOT Unity/native tested or visually accepted.

The user authorized the first package (one attendee, one vendor, one stall, poi, dressed path), with testing assigned to a separate Sol chat. No existing runtime resources, generators, gameplay rules, build settings, or character selection were replaced. The new Unity editor scene builder is also uncompiled/unexecuted in this authoring chat.

## Deliverables

- `ArtSource/ProductionPackage/FirstProductionPackage.blend`: editable assembled source scene. `AH01_Production` is the authored scene. Other factory-startup objects are outside it and are not exported.
- `ArtSource/ProductionPackage/package-preview.png` and `character-preview.png`: Blender presentation renders, NOT in-game captures.
- `ArtSource/ProductionPackage/manifest.json`: source provenance, palette, source triangle counts, and exported object counts.
- `Assets/Festival/Art/ProductionSample/AH01_Attendee.fbx`: original source body/shorts/shoes/hat adapted from the existing generated master; newly authored shirt, face, wristband. Existing 15-bone hierarchy retained.
- `AH01_Vendor.fbx`: existing stocky body/trousers/shoes/hair adapted with new shirt, apron, face, moustache, badge and straps.
- `AH01_Stall.fbx`: original Moonloop performance stall. Striped canvas, scalloped valance, rafters, supported timber frame, counter, stocked shelving, sign, lamps and poi display hooks. Static exports are combined by material; source parts remain editable.
- `AH01_PoiPractice.fbx` and `AH01_PoiLED.fbx`: single units; instantiate two for a pair. Separate handle, tether and weighted head assemblies. Sample source displays a pair of each.
- `AH01_Path.fbx`: ground, irregular dirt path, grass, stones, four light bollards. Combined by material at export.
- `Assets/Festival/Editor/ProductionSampleBuilder.cs`: editor-only setup for a separate review scene; does not change the main scene or build list. Creates URP materials and controllers using the exported clips. All diagnostics are editor-only by assembly placement.
- `scripts/create_first_production_package.py`: incremental source adaptation and new package generator. Target-prefixed outputs; does not regenerate or replace the shared character master.

## Authoring choices

Goofy adults with prominent eyes, coordinated coral/teal/ochre clothing, simplified garment surfaces and readable role differences. Moonloop is fictional shop signage within After Hours, not a new game title. Colors and principal package dimensions are in CONFIG at the top of the generator. Original geometry only; no reference-game content copied or downloaded.

The two-second attendee Idle/Dance and vendor Welcome/Offer clips are stationary upper-body acting samples. Feet stay in their authored positions; these are not walking, IK, full-body rave or purchase transaction implementations. Eye Blink and brow/mouth Delight/Concern shapes are authored separately. Existing body grip shapes are retained. Sol must inspect exported take names and shape defaults: Blender's FBX exporter can change in-memory shape values, and the generator explicitly restores a neutral source scene afterward.

Blender procedural bump is presentation-only. FBX plus palette carries geometry and base material colors; Unity setup uses existing URP Lit and the game's dusk sun/ambient values. Do not infer native appearance from the Blender render. No new diffuse/normal texture set or LOD library is claimed.

## Reproduction and assembly

Run asset creation only if a repair requires it:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python scripts/create_first_production_package.py
```

In Unity use **Festival > Create first production sample**, or invoke `Festival.Editor.ProductionSampleBuilder.Create` in batch mode using the existing project editor. It creates `Assets/Festival/Art/ProductionSample/FirstProductionSample.unity`, materials, and controllers. Open that scene explicitly; the builder restores the prior active scene and does not change build settings. Do not run a second editor against an already-open project.

Blender coordinates: Z up, fronts face -Y. FBX conversion: Unity Y up, fronts +Z. Unit scale is metres. All exports are at local origin before composition. The character wrapper scales/placements are in the builder. The path is translated +3.8 Unity Z in the sample. Poi grip origin is at the handle base, with the authored straight tether/head along local Blender +Z (Unity +Y); the display turns each unit 180 degrees around X so it hangs down. Runtime performance should control handle/head/tether separately rather than rotating a fixed rigid wand.

## Sol's task

1. Compile the editor setup, import the FBXs, create/open the sample scene. Report any source/export/import differences before wider integration. Fix minimal import/fixture defects when necessary; preserve the original gameplay assets.
2. Inspect normal scale, facing, material remaps, normals, skinning, visible eyes at neutral, blendshape ranges and exported clips. Check front/back/sides and conversation distance. Look for sleeve/shoulder, apron/body, hat/head, hand/prop and shoe/ground intersections during motion. Report coverage honestly.
3. Inspect stall contacts, canopy support, hooks, paired poi, counter height, walkable route and signage. Check that static exports have combined renderers while source parts remain editable.
4. Judge in the existing URP configuration and actual festival lighting. Capture native views at normal gameplay distance and record short clips. Compare against the four user references as a quality target, not a claim of equivalence.
5. Stage a reversible development-only hookup to existing interaction routes if needed to test conversation, dancing, purchasing and hand contact. The standalone sample does NOT implement those gameplay systems. Test actual state transitions and both views; an animation named Offer does not prove an inventory transfer or purchase.
6. Run only the checks appropriate to the import/fixture/runtime changes made. Include frame cost and renderer counts with a populated scene before suggesting broad replacement. No blanket high-quality/performance acceptance from a static render.
7. Return a concise report: confirmed passes, visible defects with captures, untested work, recommended incremental asset fixes. The user prefers asset authoring in the original chat and testing in Sol; avoid redesigning or replacing the package in the testing chat. Do not message the original chat automatically unless the user separately authorizes that direction of communication.

The package is ready for independent evaluation, not approved as a global art replacement. Asset creation/rendering occurred in the authoring chat; Unity builds, test suites, multiplayer validation and native visual acceptance were deliberately deferred.

## Shared-workspace note

`Assets/Festival/Runtime/Presentation/FestivalPoiRig.cs` became modified during this authoring turn by other work. It was not changed by this package's author. Preserve it, inspect its current behavior before testing poi integration, and do not attribute that diff to this asset package. Existing untracked research and substance-planning documents were also left untouched.
