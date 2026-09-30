# Remaining world assets and people upgrade

September 29, 2026. Authored Blender/FBX package; independent Unity/native review belongs to the existing Sol testing chat. No runtime Resources models, appearance selection code, scene wiring, build settings or gameplay rules were replaced.

## Package inventory

| Deliverable | Source | Export |
|---|---|---|
| 32 world/gear models | `ArtSource/ProductionPackage03/RemainingWorld.blend` | `Assets/Festival/Art/ProductionSample03/AH03_*.fbx` |
| Nine assembled characters | `ArtSource/ProductionPackage03/PeopleUpgrade.blend` | `Assets/Festival/Art/ProductionPeople03/AH03P_*.fbx` |
| Six body acting clips | Same people source, muted NLA tracks on first rig | `AH03P_Motion.fbx` |
| 80 wardrobe slots plus 11 hair companions | `ArtSource/ProductionPackage03/WardrobeExtension.blend` | `AH03W_WardrobeLibrary.fbx` |

Generators: `scripts/finish_festival_world_package.py`, `scripts/upgrade_festival_people.py`, `scripts/extend_festival_wardrobe.py`. Each incrementally loads original named library objects and produces an isolated package. Shared geometry functions are extracted from earlier generators without running their top-level code. Source dependencies must remain available. Run from the project root with Blender background/factory-startup and `--python <script>`.

Detailed inventories, triangle counts, pivots and styles are in `manifest.json`, `people-manifest.json`, and `wardrobe-manifest.json` under the source directory. Palettes are beside the FBXs; the people directory has a separate `wardrobe-palette.json` for AH03W materials. Original procedural meshes only, no downloaded game content or third-party assets.

## World and gear

The 24 continuing models are Medical, Security, Shuttle, StallSupplies, StallStock, CampShop, Tent, DomeTent, CampCar, CampVan, CampShade, PortaPotty, TreeA, TreeB, TreeFir, GroveDetail, LittleSpoon, Confetti, MerchBag, Map, StagePass, Stock, Voucher and Stash. These reuse existing original construction; some receive palette/geometry refinements rather than complete rebuilds.

Eight new support models: Bench, CrowdBarrier, RecyclingBin, WayfindingPost, StringLights, WaterBottle, RecoveryWristband and ShuttleStop. Instantiate the shade twice for the specified camp, and reuse lights/barriers/trees as modules rather than exporting duplicated scenes.

The stage, DJ equipment, speakers, Sun/Moon sculptures, first performance stall and poi remain in packages 01/02. The existing articulated first-person hands and ten-color hair palette remain in the original library; this package does not claim new first-person animation or a new renderer.

Notable edits: clinic privacy screen and rail supports; clinic/security/shop signage; shuttle folding-door pivots and continuous grab rail; market awning supports; toilet ribs and occupancy plate; tree root flares; confetti muzzle; rebuilt slatted stash with a lid pivot; concave small spoon; wearable recovery band with socket. The shuttle doorway stays open in the rest pose. Any collision/navigation/boarding behavior still belongs to Unity.

Exports are in metres and at asset-local origin. Converted front-facing buildings now face Blender -Y. Tree/camp/gear orientations retain original conventions where no single front applies. The manifest's gallery placements are Blender coordinates, not game anchors. Prior Unity review found FBX X conversion can reverse against the Blender manifest: use imported transforms and measured bounds for integration, not blind coordinate copies.

## People and expression repair

Cast: lanky, average and stocky attendees; Supplies, Performance and Stock vendors; Medic; Security; MissingFriend. Both existing gender pools and all three body families are represented. Existing body/garment weights and fit shapes provide the base. Each assembled outfit has its selected Fit baked; the modular wardrobe export separately retains all six Fit channels.

Faces are newly authored as one skinned mesh per person, with eye rims, layered irises/pupils/catchlights, brows, nose/nostrils, cheeks and mouth. Seven shared controls: Blink, Delight, Alarm, Concern, Suspicious, Exhausted, Confused. Neutral is all weights zero. Blink retracts the whites, irises, pupils and glints into the face and seats a closed-lid crease. Excessively protruding eyes, dangling belt pieces and the inherited detached friend bag were corrected during authoring. The friend keeps the separate fitted backpack.

All cast FBXs export WITHOUT animation. Body clips export on a rig-only FBX, with the exported armature consistently named FestivalRig. This avoids the constant-zero shape curves found by Sol in the first package. Configure Generic animation and remap rig-only clips against the imported shared hierarchy; prove the paths bind on each cast before accepting them. Do not replace this split with a combined export that reintroduces sampled facial curves.

Idle, Dance, Offer, Assist, Panic and Cheer are two-second stationary acting samples. Dance and reactions use larger arm/spine poses than package01. They are not walk/run/crawl/boarding cycles, inverse kinematics, physical handoffs, or network gameplay implementations. Rig-only motion must leave facial and grip control free; confirm that with the Animator running, not only while disabled.

## Wardrobe coverage

The library now contains ten slots in each of Headgear, Sunglasses, Shirt, Pants, Shoes, FacialHair, Hairstyle and Accessory: 32 retained originals plus 48 added variants. Eleven companion meshes are ten HairTop variants and HairUnderHat. New options mix silhouette changes, added construction and explicitly named color-block styles. The source manifest lists every style; this is not a claim that every variant is a wholly independent sculpt.

All slots retain six Fit channels for the existing body families. This is an authoring contract, not proof that every randomized outfit combination fits. Prioritize hat/hair/glasses, beard/mouth, long tops/lower garments, backpack/role overlay, shoe/ankle and moving sleeves. HairTop and Hairstyle use matching indices; opaque hats use HairUnderHat. The existing runtime still selects its original four slots. Expanding runtime selection to ten is a separate integration edit requiring testing, not silently enabled by this export.

The nine assembled cast FBXs are deliberate review looks. The new modular slots do not automatically appear on those nine characters. No new LODs, normal-map bake or texture atlas is claimed.

## Sol review instructions

1. Import all exports and both palettes. Audit missing materials, metres, facing, geometry bounds, normals, control pivots and renderers. Compare to these source renders, then capture native views under the actual festival URP/dusk settings.
2. Make an isolated fixture with the nine characters and a wardrobe selector for the full 80 slots. Preserve main game resources and other chats' changes. Add diagnostics only in editor/development builds.
3. For each cast, capture neutral and all seven facial poses at conversation and gameplay distances. Record requested and observed weights before/after Animator evaluation. Run the separate body clips while holding Blink/Delight/Grip values; verify they survive and that clip paths animate all characters.
4. Inspect sampled combinations across six Fits, including new long hems, flared trousers, pendant/vest and glasses/eye clearance. Do not claim exhaustive outfit acceptance from one lineup render. Inspect motion for clipping and contacts and compare foot grounding against the stationary-clip scope.
5. Inspect building interiors and entrances, moving stash/shuttle parts, support contacts, small gear at first-person distance, and forest sightlines. Collider proxies, walkability and private clue behavior remain explicit integration work.
6. Run only checks relevant to fixture/import changes. Record populated performance separately from an empty gallery. Save a package03 report and captures; list passes, defects and untested integration plainly. Authoring fixes remain in this chat; minimal importer/fixture repairs are authorized in Sol.

The earlier native expression screenshot was not proof that the shape data was missing. Sol found substantial vertex deltas plus zero-valued animation curves; see `docs/CHARACTER_EXPRESSION_DIAGNOSIS_2026-09-29.md`. Keep that corrected diagnosis with the first report.
