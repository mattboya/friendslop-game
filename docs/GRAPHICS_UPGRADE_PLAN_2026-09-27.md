# After Hours graphics upgrade plan

Date: September 27, 2026  
Status: Active implementation plan. See the dated checkpoints below for completed and open work.
Reference: [Dear Passengers — “Another Friendslop Game” teaser](https://www.youtube.com/watch?v=luWunttkCgY)  
Overall regression risk: **7/10** — estimated likelihood of disrupting existing functionality during the full upgrade, before the controls below. This is an engineering judgment, not a measured probability.
Tool constraint: use free tools and assets with documented commercial use rights; do not depend on paid subscriptions or expiring trials.

## 1. Outcome we are building toward

Make After Hours feel like a finished, expressive, stylized cooperative game at normal gameplay distance. The supplied trailer establishes the visual quality target: clean rounded forms, readable faces, coherent materials, convincing light, deliberate scene composition, lively crowds, and physical comedy. Apply those qualities to our outdoor festival, original adult attendees, campsite, market, stage, and shuttle.

The intended result is a colorful festival at dusk. Warm lamps and stalls illuminate faces against a cooler sky and woodland. Characters remain readable when surrounded by other attendees. Nearby objects hold up when inspected or carried. Dancing, purchasing, rescue, and extraction produce clear and amusing physical reactions. Interface elements leave enough of the world visible to appreciate that work.

This plan covers the existing campsite and first festival. It includes character art, environment art, materials, lighting, animation, first-person presentation, visual effects, interface presentation, asset authoring, and graphics performance. Additional festivals and major changes to the mission rules require their own scope.

The milestone that unlocks full production is a **finished playable sample area**, also called the vertical slice. It must demonstrate the proposed quality in a native build, with gameplay and performance evidence. An attractive Blender render alone does not pass this milestone.

## 2. Evidence and current baseline

The reference trailer was visually inspected at several points across its approximately 36-second duration. These observations describe visible results; they do not establish the reference game's engine, shader implementation, lighting method, asset budgets, or sustained performance.

| Reference observation | Translation to After Hours |
| --- | --- |
| Characters have large, readable eyes and strong silhouettes | Make faces and body reactions readable at conversation distance; retain our original character identity |
| Clothing, seats, doors, and props have finished contours | Give prominent edges believable thickness, bevels, and intentional silhouette design |
| Bright windows and fixtures separate subjects from backgrounds | Establish deliberate key light, sky fill, practical light, and background value separation |
| Cabin scenes use repeated structure with varied occupants and props | Build reusable festival modules, then compose them into distinct social spaces |
| Close interactions make hands, bodies, and objects part of the comedy | Author contact poses, anticipation, follow-through, and clear reactions for existing actions |
| Busy scenes still have a readable focal action | Control contrast, prop density, crowd placement, camera framing, and UI competition |

Current project evidence reviewed for this plan:

- [Approved direction](APPROVED_DIRECTION.md): 2–8 players, original stylized festival art, intentionally goofy animation, and an 8–12-minute run.
- [Implementation status](../IMPLEMENTATION_STATUS.md): playable native macOS development build; human visual acceptance, target hardware profiling, Windows validation, and several animation details remain open.
- [Character quality pass](CHARACTER_QUALITY_PASS.md): shared 13-joint body rig, compatible detailed and distant meshes, wardrobe fit controls, and substantial existing geometry.
- [Asset provenance](ASSET_PROVENANCE.md): original Blender sources, generated exports, and explicit records for included assets.
- Current native captures: [camp](../artifacts/native-smoke/created-camp.png), [stage](../artifacts/native-smoke/host-crowd-live.png), [character lineup](../artifacts/native-smoke/client-character-quality.png), and [rhythm](../artifacts/native-smoke/client-rhythm.png).

These capture paths are overwritten by subsequent smoke runs. Preserve dated copies before using them as comparison baselines.

### Confirmed gaps

1. **Environment composition:** wide flat paths, broad ground planes, repeated tree forms, and sparse transitions between attractions leave substantial parts of the frame visually unfinished.
2. **Surface treatment:** many objects rely on broad colors with limited material variation. Fabric, wood, painted metal, rubber, and foliage need more deliberate surface identities.
3. **Lighting:** existing dusk grading and bloom provide a starting point, but characters and landmarks need stronger separation and more convincing local illumination.
4. **Expression and contact:** facial animation, authored choreography, and articulated hand-object contact remain open in the project status.
5. **Interface footprint:** large opaque cards compete with the world in current gameplay captures.
6. **Performance evidence:** the latest status records roughly 17.3 ms p95 during the rhythm segment at 1280×720 with two processes on one Mac. This is a limited scripted observation, not a minimum-spec benchmark. It provides no established headroom for a major increase in graphics cost.

### Rendering configuration findings to verify first

The saved `FestivalPipeline.asset` currently has HDR enabled, render scale 1, MSAA 1, one main-light shadow cascade, soft-shadow support disabled, and additional-light shadows disabled. `FestivalWorld.cs` requests soft shadows on directional lights. The requested light mode and saved pipeline support therefore differ; confirm the active native-player configuration before changing quality settings.

The world is assembled at runtime. Static lighting cannot be treated as a simple checkbox: a reproducible editor authoring or generation path is needed for baked geometry, lightmaps, and probes. Both `FestivalWorld` and `FestivalSession` create global post-processing volumes; inspect their effective priorities and overrides before tuning bloom or exposure.

## 3. Art direction rules

### Characters

- Preserve goofy adult festival attendees, varied body proportions, distinctive hats and hair, oversized readable features, and clothing that communicates personality.
- Refine the face around a few strong shapes. Expression should remain legible without tiny wrinkles or dense surface detail.
- Give eyes, brows, cheeks, and mouth coordinated reactions. Avoid independent effects that make a face appear broken or unintentionally alarming.
- Keep silhouettes recognizable in neutral lighting, at 2–4 metres, and in a small crowd.
- Use controlled outfit palettes. Reserve the strongest contrast for faces, hands, role identifiers, and held items.
- Create clean joint deformation and consistent garment thickness. Judge hats, glasses, sleeves, skirts, and straps in moving poses.

### Environment and materials

- Use rounded, deliberately simplified forms with enough thickness to catch light.
- Build believable festival construction: canvas tension, timber connections, ropes, cables, speaker stacks, fasteners, stage decking, folded fabric, and functional signage.
- Place detail where players approach or interact. Spend less geometry and texture memory on distant dressing.
- Distinguish cloth, bark, wood, painted metal, rubber, skin, and glass through roughness, shape, and restrained texture.
- Break ground repetition with worn edges, grass clumps, footprints, localized litter, small stones, and color variation. Keep navigable routes immediately apparent.
- Frame attractions with foreground and background shapes. Use variation in tree silhouette, scale, spacing, and color instead of uniform rows.

### Light and atmosphere

- Use a warm key light with cooler sky fill and practical pools of light around interactions.
- Keep faces readable at the darkest required location without washing the entire world flat.
- Let stage colors animate the scene while maintaining stable visibility of players and rhythm cues.
- Use fog to create depth, with a continuous horizon and believable background woodland.
- Keep bloom localized to bright emitters. Avoid a haze that removes material definition.
- Preserve Reduced Motion behavior and avoid compulsory camera shake, strong blur, rapid flashes, or oscillating exposure.

## 4. Decisions and assumptions

These are planning defaults. Resolve the first four before committing substantial asset production; they do not block writing this plan or collecting a technical baseline.

| Decision | Recommended default | Why it matters |
| --- | --- | --- |
| Character direction | Polish our current adult festival identity toward the trailer's expressiveness | A major proportional redesign changes every garment, attachment, and animation |
| Time of day | Readable dusk with warm practical lights | Preserves the established festival identity while improving contrast |
| Performance target | 1080p, 60 fps on an explicitly named minimum Windows PC; separately measured Mac development configuration | A performance promise needs hardware, settings, and reproducible scenes |
| Asset production | Original hero assets and a coherent original modular kit made with free tools | Establishes a consistent look and clear source ownership without paid asset dependencies |
| Renderer | Continue with Unity URP | The initial work can be evaluated within the current project and deployment architecture |
| Wardrobe expansion | Freeze current variety while proving the new visual standard | Prevents multiplying fit and animation work before the master is accepted |
| Gameplay footprint | Use existing interactions and landmark anchors for the first slice | Allows visual quality to be judged without simultaneously redesigning navigation and rules |

### Free toolchain

- **Blender:** modeling, sculpting, UVs, texture painting, rigging, animation, baking, and export. Keep the existing original Blender sources and generators as the production base.
- **Krita:** optional free, open-source painting for concept art and detailed 2D texture work. Blender can cover the first texture pass without another application.
- **Unity Personal with URP:** current game engine and renderer. Unity Personal is free while the developer or organization meets its current eligibility rules; check eligibility as the project grows. Unity's [current Personal page](https://unity.com/products/unity-personal) states a USD $200,000 revenue and funds-raised threshold over the previous 12 months.
- **Project scripts and Git:** existing free build, validation, source-control, and asset-generation workflow.

Adobe Substance 3D Painter is a paid product after its [30-day trial](https://www.adobe.com/products/substance3d/plans.html), so it is excluded from this production plan. Any free third-party asset or tool must have a license compatible with the game's intended commercial release and be recorded in [asset provenance](ASSET_PROVENANCE.md). Do not build the pipeline around an expiring trial.

## 5. Technical architecture

### 5.1 World authoring

Separate visible scenery from gameplay anchors and collision. Keep `FestivalWorld.Build()` as the lifecycle entry point while migrating one area at a time to authored prefab assemblies.

Proposed responsibilities:

- **World layout data:** named landmark anchors, zone bounds, spawn points, route connections, and references to visual assemblies. New assets or classes are proposals until implemented.
- **Visual assemblies:** market, stage, grove, campsite, clinic, security, and shuttle prefabs with materials and local dressing.
- **Collision and navigation:** explicit proxy geometry, walkable surfaces, and interaction clearance independent of decorative detail.
- **Lighting configuration:** shared environment settings plus deliberate camp, festival, and stage variations.
- **Editor generation:** reproducibly builds review scenes and any static-lighting scene from layout data without overwriting hand-authored source assets.

Retain idempotent construction and owned-root cleanup. Verify repeated Build, camp transitions, reset, and scene teardown. Avoid a wholesale rewrite of the simulation or network layer to accommodate art.

Current rules describe movement primarily in X/Z. Begin terrain variation outside critical walking surfaces and use shallow visual relief where collision stays planar. Actual slopes or elevation changes require a separate movement, camera-height, and navigation design before introduction.

### 5.2 Character and animation presentation

Preserve deterministic appearance selection, wardrobe identifiers, fit shapes, compatible bind poses, and matching first-person skin and sleeves. Extend expression controls in the presentation layer. Any added bones or blendshapes need corresponding export validation and distant-mesh behavior.

Use authored animation clips for actions that benefit from deliberate timing and retain procedural layers for variation, look direction, small reactions, and adaptation. Visual motion must follow authoritative state without driving unauthorized movement or changing action timing.

Body pose, hand contact, face expression, and held-item attachment should have a defined priority order. For example, a rescue action owns the reaching arm while locomotion continues to control the lower body where appropriate. Root motion must not silently change network positions.

### 5.3 Asset reproducibility

The current Blender generators are useful for reproducible construction. Introduce clear boundaries between generated base assets and authored finishing work so regeneration cannot erase sculpting, UVs, weights, or animation.

Every production asset should record source path, export path, pivot, scale, material slots, LODs, collision responsibility, animation/socket dependencies, and provenance. Keep stable Unity GUIDs and asset references during replacements. Export into staging and validate before replacing runtime assets.

### 5.4 Materials and lighting

Establish shared material families and texture atlases or trim sheets where they help reuse. Measure material slots and draw calls as well as triangles. Preserve runtime palette variation without creating unnecessary unique materials per attendee.

During the slice, compare a controlled realtime lighting configuration with an editor-authored static-lighting approach. Decide based on visible quality, authoring cost, startup behavior, and target hardware measurements. If static lighting wins, document lightmap generation, probe placement, dynamic actor illumination, and reproducible build inclusion.

Test anti-aliasing, shadow support, cascade distribution, contact shading, reflection probes, and post-processing individually. Inspect actual rendered output and GPU cost before enabling them across quality levels. Verify installed Unity package APIs during implementation.

## 6. Production work packages

### A. Characters and faces

Initial slice deliverables:

- One polished player character with two representative outfits.
- One seller and one contrasting attendee using the shared asset system.
- A facial expression set: neutral, pleased, confused, suspicious, alarmed, and exhausted; integrate existing wide-eye and red-eye states coherently.
- Coordinated eye direction, blinks, brows, and mouth poses.
- Close, gameplay-distance, and distant versions reviewed under the same lighting.

After slice acceptance, expand the standard to the three body families, staff role overlays, current face pools, and existing wardrobe categories. Review stress combinations such as glasses plus hat, backpack plus jacket, and skirts during crouching and dance. Additional wardrobe counts are a separate production estimate.

Acceptance: no obvious intersections in the reviewed action set; stable silhouette through LOD changes; faces readable in camp and stage lighting; role identity preserved without relying only on text or color.

### B. Animation and first-person interactions

Prioritize these actions in order because they occur often or carry the game's promise:

1. Idle, walk, turn, stop, and run with grounded feet and readable weight shifts.
2. Pick up, hold, inspect, present to seller, equip, use, and drop.
3. Dance steps, transitions, successful reactions, and failed reactions.
4. Reach, assist, drag presentation, recover, crawl, and escort reactions.
5. Camp readiness, suspicion escalation, detention reactions, and shuttle boarding presentation.

Define hand sockets and grip poses per prop family. Test near-camera clipping, wrist bends, sleeve intersections, and mismatch between first-person and remote-player views. Interpolate visual reactions under latency and confirm actions still take effect at authoritative times.

Crowd motion should vary phase, posture, attention, and intensity without requiring every distant actor to update an expensive full rig every frame. Animation quality must be assessed in motion, not inferred from static screenshots.

### C. Environment kit

| Area | Main work | Completion evidence |
| --- | --- | --- |
| Camp | Finished canopy construction, tents, cars, seller kiosk, shelves, path edges, perimeter composition | Arrival and shopping walkthrough with clear seller and ready-point discovery |
| Market | Detailed stall fronts, readable physical signs, coherent stock props, fabric and wood surfaces, local light | Purchase flow at normal camera distance with hands and goods visible |
| Stage | Finished deck, rig, speakers, DJ equipment, backdrop, cables, controlled lighting and crowd framing | Approach, crowded dance, and rhythm-camera clips |
| Grove and paths | Varied trees, undergrowth, ground transitions, picnic/social clusters, landmark sightlines | Continuous walking route with readable turns and no empty horizon seams |
| Totems | Distinct sculpture silhouettes and materials, supporting set dressing | Both clue states, including proof that private cues remain private |
| Medical and security | Strong architectural and prop identities, readable entrances and recovery spaces | Recovery and detention walkthroughs with clear clearance |
| Shuttle | Finished exterior/interior and boarding presentation | Extraction with multiple players and a rescued friend |

Reuse secondary props across zones with restrained variation. Keep critical interaction areas free of visual clutter and decorative colliders.

### D. Effects and ambient movement

Use effects to communicate specific events: accepted interaction, successful performance, rescue completion, clue discovery, and extraction. Refine poi trails and stage lighting. Add limited fabric sway, foliage movement, dust, or confetti where appropriate to the location.

Pool repeating effects, cap concurrent emitters, measure transparent overdraw, and disable hidden effects. Private perception visuals must stay viewer-specific. Reduced Motion and effect intensity controls must preserve readable gameplay information.

### E. Interface and camera presentation

Reduce persistent opaque panel area and consolidate repeated messages. Show the current objective, urgent status, inventory, and focused interaction with clear priority. Reveal secondary detail contextually. Keep information readable at 1280×720 as well as 1080p and wider aspect ratios.

Review the rhythm camera alongside the note lane. It must show the actual player, readable full-body motion, and relevant scenery without clipping into props or hiding timing information. Preserve both arrow-key and WASD input behavior and the movement lock during a challenge.

Review the first-person field of view and hand framing during navigation and close interactions. Camera effects must be optional where appropriate and must not conceal hazards or private clue information.

## 7. Milestones and exit gates

### M0 — Baseline and recoverability

Tasks: preserve current source and build; establish version control or a verified recoverable snapshot because this workspace currently has no Git repository; copy dated baseline captures; record build configuration, hardware, resolution, frame timings, and crowd state; perform a human-paced route inspection to identify visual and interaction pain points.

Deliverables: baseline manifest, capture set, named target hardware, initial graphics cost breakdown, and an asset inventory checked against source and runtime usage.

Exit gate: current build can be reproduced or restored, benchmark conditions are recorded, and visual comparison views are repeatable.

### M1 — Art specification and technical experiments

Tasks: settle the character/time-of-day decisions; define palette, material examples, proportions, light direction, reference camera views, and environmental density; verify active URP settings; compare a small lighting and material sample; prototype the authored-world loading and lighting path.

Deliverables: art specification, proposed quality presets, material sample scene, initial hero asset, and chosen world/light authoring approach.

Exit gate: the intended look is concrete enough to review, source regeneration is safe, and the technical approach has a measured cost.

### M2 — Finished playable sample area

Tasks: finish the camp seller, market-to-stage sample route, initial three-character set, key held props, pickup/purchase/dance animation, lighting, ground/foliage composition, and revised local HUD presentation. These locations include a camp-to-festival transition; the review must exercise that transition rather than imply one continuous map.

Deliverables: native build, comparison stills, ordinary gameplay clips, profiler captures, and regression results.

Exit gate: user accepts the visual direction; close interactions and motion hold up; performance fits the provisional budget; mission interactions still function. Rework this sample if it misses the target before multiplying its assets across the level.

### M3 — Full first-level art production

Tasks: bring all locations and existing character combinations to the accepted standard; complete recovery/extraction presentation; integrate atmosphere and consistent signs; inspect every mission route and major state.

Deliverables: complete art inventory with status and evidence, full native level, outstanding-defect list, and updated provenance.

Exit gate: no required location remains visibly at prototype quality; no missing materials or unfinished placeholders appear in the normal route; crowd and action readability remain consistent.

### M4 — Optimization and platform verification

Tasks: tune LODs, culling, animation updates, batching, texture memory, shadows, lights, and effects using captures; measure actual Windows target hardware once the required build support is available; separately benchmark Mac; test the eight-player load with distinct hardware/network conditions where available.

Deliverables: quality preset matrix, benchmark report, native development and release evidence, and documented hardware limitations.

Exit gate: agreed performance targets pass under specified conditions, release builds exclude development controls, and visual quality remains acceptable at the minimum preset. A synthetic crowd or two local processes cannot establish eight-machine multiplayer acceptance.

### M5 — Visual and human acceptance

Tasks: conduct complete human-paced solo and cooperative runs; review navigation, role recognition, interaction clarity, motion comfort, and comedy; fix observed problems; repeat only the affected checks and the final full route.

Deliverables: acceptance record with user feedback, final reference comparison, known limitations, and a reproducible build identifier.

Exit gate: all criteria in Section 11 are satisfied or a specific exception is explicitly accepted. Tests and successful compilation do not substitute for visual approval.

## 8. Performance budgets and measurement

Treat these as proposed starting targets until M0 names hardware and establishes an isolated benchmark:

| Measure | Proposed gate |
| --- | --- |
| Main target | 1920×1080 at the agreed quality preset and minimum Windows specification |
| Frame time | p95 at or below 16.7 ms during required play segments; record p99 and maximum and investigate recurring hitches |
| Slice headroom | Aim for p95 at or below 13.3 ms on the same target configuration before full dressing |
| CPU/GPU | Report both separately; identify the limiting stage rather than assuming graphics are the cause |
| Memory | Set RAM/VRAM budgets from named hardware, then record steady state, peak, and growth across resets |
| Workloads | Camp shopping, populated stage, rhythm camera, clue effects, rescue, eight-player stress case, extraction, repeated resets |

Warm assets before steady-state timing and report cold-load behavior separately. Capture at least a repeatable two-minute crowded route and a full mission, with settings and scene seed recorded. Use frame timing data and profiler captures; an average fps counter is insufficient.

Count visible renderers, skinned meshes, material slots, triangles, lights, shadow casters, and particles at expensive locations. The current detailed character's conservative selected geometry budget is about 44,804 triangles and its distant counterpart about 14,323, according to the character quality document. Reassess those budgets against the measured crowd cost before adding geometry.

Prioritize shared materials and meshes, offscreen culling, stable LOD transitions, less frequent distant animation, limited shadowed lights, and fewer unnecessary secondary camera draws. Measure each change and retain comparison captures to catch visible degradation.

## 9. Debug-only diagnostics

Extend the existing development diagnostics with an opt-in graphics overlay and structured event logging guarded by `UNITY_EDITOR || DEVELOPMENT_BUILD`.

Record build ID, scene seed, quality preset, active pipeline, render resolution, light/shadow configuration, camera mode, visible crowd counts, LOD distribution, animation update counts, frame-time percentiles, and asset validation failures. Include material or socket identifiers when reporting a broken attachment or missing resource.

Use named diagnostic categories such as Rendering, Character, InteractionVisuals, and WorldLifecycle. Log state changes and sampled summaries; avoid per-actor, per-frame log spam that distorts performance. Keep benchmark runs with verbose logging disabled and capture settings in the report.

Provide repeatable development capture locations and a way to inspect LODs, lighting, bounds, sockets, and collision proxies. These controls must be absent from ordinary release builds. Verify both compile guards and a running release player; log personal chat or voice content nowhere in this graphics workflow.

## 10. Regression controls and risk register

| Change | Risk / 10 | Failure to prevent | Control |
| --- | --- | --- | --- |
| World assembly migration | 8 | Missing anchors, duplicate worlds, blocked routes, reset leaks | Migrate by zone; preserve owned roots and anchors; run lifecycle and route checks |
| Character rig/fit changes | 8 | Broken wardrobe, attachments, animation, LOD switching | Validate bind poses, slots, fit controls, representative combinations, and motion |
| Animation/contact work | 7 | Visual timing disagrees with state; unintended movement | State-driven presentation, explicit layer ownership, latency review |
| Terrain/elevation | 8 | Floating actors, movement or navigation failures | Keep first-pass walkable surfaces compatible; treat real elevation as separate work |
| Materials/lighting | 6 | Shader stripping, unreadable faces, expensive shadows | Native-player review, referenced assets, measured presets, consistent capture views |
| HUD/camera | 6 | Lost instructions, occlusion, input regressions | Task walkthroughs, aspect-ratio review, rhythm/input checks |
| Crowd optimization | 7 | Visible popping, frozen near actors, changed gameplay density | Separate cosmetic and authoritative actors; review distance transitions and interactions |

Run focused checks at each milestone, then the existing full regression workflow before integration acceptance:

```sh
node scripts/test-domain.mjs all
node scripts/verify-project.mjs
node scripts/unity.mjs test-edit
node scripts/unity.mjs test-play
node scripts/unity.mjs build-mac-development
node scripts/native-solo-smoke.mjs
node scripts/native-smoke.mjs
```

Add meaningful coverage only for new failure boundaries: world generation/teardown, stable landmark reachability, rig/export compatibility, visual-state transitions, attachment integrity, private cue visibility, and release diagnostic exclusion. Avoid tests that merely repeat material constants or assert subjective beauty.

The scripted smoke workflow teleports actors between some stages. Supplement it with actual traversal, camera aiming, shopping, recovery, and rhythm play. Re-run Windows and release checks when that platform is available; do not label them passed from macOS evidence.

## 11. Definition of done

- The user accepts native gameplay stills and motion clips against the supplied visual benchmark.
- Camp, market, stage, grove, totems, medical, security, shuttle, and connecting routes share a consistent finished visual language.
- Faces, clothing, hands, and common props hold up at ordinary interaction distance.
- The main action set has readable anticipation, contact, reaction, and transitions; reviewed poses show no obvious clipping or floating feet.
- The world stays readable with a populated crowd, private effects, low-health/recovery states, and the rhythm camera active.
- UI is legible across supported sizes and leaves the focal action visible.
- Agreed performance and memory targets pass on named hardware with reproducible evidence.
- Required regression checks and human-paced routes pass; outstanding platform or load gaps are explicitly recorded.
- Source assets, exports, materials, and lighting can be reproduced without losing authored work.
- Provenance is complete for every new asset, and development diagnostics are absent from release behavior.

If any of these are incomplete, report the specific remaining gate. Do not convert the overall result into an unsupported percentage of “trailer quality.”

## 12. Estimated effort and staffing

Initial planning range for the existing camp and first festival, with one experienced 3D artist/animator and one Unity engineer or technical artist working substantially on the project:

| Milestone | Approximate elapsed time |
| --- | --- |
| M0 baseline | 2–3 working days |
| M1 art specification and experiments | 3–5 working days |
| M2 playable sample | 2–4 weeks |
| M3 full-level production | 4–7 weeks |
| M4 optimization and platform checks | 1–3 weeks |
| M5 final acceptance and fixes | 1–2 weeks |

This is approximately 9–18 weeks with some overlap, not a delivery commitment. Character redesign, a larger wardrobe, real terrain elevation, missing platform access, or extensive animation revisions can increase it. A single person performing all roles should expect a longer schedule. Re-estimate after M2 using actual asset production speed and measured performance costs.

Automation can help with repetitive mesh generation, exports, manifests, capture collection, and validation. Art direction, deformation cleanup, convincing animation, and visual review still require iteration. Track finished, accepted assets rather than raw asset counts.

## 13. Implementation map

| Existing location | Planned responsibility |
| --- | --- |
| `scripts/generate_modular_character.py` | Character construction/export, stable contracts, LOD generation |
| `scripts/generate_festival_hands.py` | Matching hands, grip-compatible geometry and exports |
| `scripts/generate_festival_world_assets.py` | Reusable original world kit and export manifests |
| `ArtSource/` | Source assets, authored finishing/animation, manifests, provenance inputs |
| `Assets/Festival/Runtime/Presentation/FestivalWorld.cs` | Area assembly, anchors, proxy collision, lifecycle, migration entry point |
| `Assets/Festival/Runtime/Presentation/FestivalCharacter.cs` | Appearance, expressions, animation layering, LOD and attachments |
| `Assets/Festival/Runtime/Presentation/FestivalHands.cs` | First-person contact and held-object presentation |
| `Assets/Festival/Runtime/Presentation/FestivalAmbientCrowd.cs` | Cosmetic crowd variation and measured update/visibility policy |
| `Assets/Festival/Runtime/Presentation/FestivalDancePreview.cs` | Readable live dance camera and render cost |
| `Assets/Festival/Runtime/Presentation/FestivalHud.cs` | Contextual information hierarchy and reduced world obstruction |
| `Assets/Festival/Runtime/Network/FestivalSession.cs` | Camera and state-to-presentation integration; review volume ownership |
| `Assets/Festival/Editor/ProjectBootstrap.cs` | Reproducible rendering configuration and safe content generation |
| `Assets/Festival/Runtime/Presentation/DevelopmentDiagnostics.cs` | Development-only graphics inspection and structured diagnostics |
| `Assets/Festival/Tests/` and `scripts/native-*-smoke.mjs` | Focused compatibility checks and native regression evidence |

Update `IMPLEMENTATION_STATUS.md`, the art/source documentation, and this plan's milestone checklist as evidence arrives. Preserve the distinction between planned work, implemented work, automated checks, native rendering evidence, measured performance, and human acceptance.

## 14. Execution checklist

- [ ] M0: recoverable baseline, dated captures, target hardware, baseline profile.
- [ ] M1: agreed art specification, rendering verification, safe authoring/export workflow.
- [ ] M2: finished playable sample, user visual acceptance, regression and performance gates.
- [ ] M3: complete first-level art, animation, atmosphere, and interface integration.
- [ ] M4: measured optimization, quality presets, available platform/load verification.
- [ ] M5: complete human runs, final visual acceptance, release diagnostic check, source handoff.

The first implementation task is M0. The first production commitment is the M2 sample area. Full-level asset production follows evidence that the sample meets the intended visual and technical standard.

### September 27 implementation checkpoint

M0 has a recoverable source commit, archived Mac development build, [dated native views](GRAPHICS_BASELINE_2026-09-27.md) and an [initial sampled Mac cost breakdown](GRAPHICS_PROFILE_2026-09-27.md). The graphics baseline still needs a named Windows minimum machine, controlled CPU/GPU profiling and a human-paced route. M1 technical work has a single owned post-processing volume, native verification of the active URP shadow configuration, opt-in development graphics sampling and a Mac release diagnostic exclusion check. The staged Blender path validated a full generated set and published updated character and car sources and FBXs with stable Unity GUIDs. The requested campsite pass also adds varied vehicles and tents, enterable interior views, a camp DJ, a post-round review vote, passive Little Spoon wear, and first-person held poi with a cosmetic cord and weighted head. The four production decisions in Section 4 and art/sample acceptance remain open, so M0 and M1 exit gates are not marked complete.

The [environment shape and surface checkpoint](GRAPHICS_ENVIRONMENT_PASS_2026-09-27.md) adds original reusable surfaces, shaped fabric, revised foliage, a more defined car and a native comparison capture set. Camp feature clearance was corrected without changing collision or interaction locations. The market-to-stage route, remaining architecture, animation/contact, HUD, human acceptance and target performance are still open; this is progress within M2, not its exit.

The next [market-to-stage checkpoint](GRAPHICS_MARKET_STAGE_PASS_2026-09-27.md) develops the stall and performance-stage forms and adds dated native views of the route. Its scripted mission still passes, with populated festival point samples near the 16.7 ms budget on one Mac. It does not close M2: the captured market interaction remains visually crowded, the other buildings and moving action remain unfinished, and the normal walking route and target hardware have not been accepted.

The [camp and location follow-up](GRAPHICS_CAMP_LOCATIONS_PASS_2026-09-27.md) responds to the first-person screenshot and campsite feedback. The prompt and checkout controls moved above the hands; the poi grip and weighted cord were retuned; Little Spoon is passive outside the hand gear slots; garments, vehicles, tents, the clinic, security booth and shuttle gained modeled detail. Car, tent and porta potty doors now route to separate interior cells while their existing gags, camp DJ and post-round review stay available. Static/domain, Unity and Mac build gates pass, and an intermediate two-client native run passed with location comparisons. The final graphical smoke and user review are pending desktop unlock, so M2 and later exit gates remain open.

The [grove and camp reachability checkpoint](GRAPHICS_GROVE_AND_CAMP_REACHABILITY_2026-09-27.md) fixes the expanded campsite footprint so the new vans and dome tents are reachable, adds fir and ground-cover assemblies to the original woodland kit, and verifies every camp door approach on the generated NavMesh. Its neutral and native captures are inspection evidence; human-paced review and target hardware performance remain open.

The [vehicle shape checkpoint](GRAPHICS_VEHICLE_SHAPE_PASS_2026-09-27.md) rebuilds the wagon and camper silhouettes with seated glass, a continuous van cab, differentiated roof profiles and reviewed front/rear forms. The generated FBXs, native camp captures and two-client mission pass. These remain stylized prototype assets with limited material and interior finish; the full M2 visual sample and user acceptance remain open.

The [camp interior visibility checkpoint](GRAPHICS_INTERIOR_VISIBILITY_PASS_2026-09-27.md) moves the room cells clear of the distant woodland rise that covered their floors and props, adds a more legible tent fabric and bedding pass, and closes the empty view through the interior exit. A before/after native view, a red-to-green PlayMode clearance test, and a native solo smoke support the change. The rooms and broader environment still need authored finish, motion review, and measured performance; M2 remains open.

The [September 28 camp and motion checkpoint](GRAPHICS_CAMP_AND_MOTION_PASS_2026-09-28.md) reshapes the camp shelters and seller awning, corrects scaled hand attachments, gives poi visible full-circle corded motion, adds three distinct rave-footwork patterns, and rounds ambient walker turns. Native before/after and motion frames, PlayMode attachment and orbit tests, and the scripted two-client mission support the pass. Foot planting, authored interaction choreography, close material finish, HUD footprint and sustained target-hardware performance remain open; M2 remains open.

The [September 28 ankle rig checkpoint](GRAPHICS_ANKLE_RIG_PASS_2026-09-28.md) raises the published character to 15 bones so shoes can counter-rotate independently of the shin during walking, poi and dance motion. The generated vendor apron fit and published-source audit were corrected in the same stage. Blender fit checks, Unity tests, native solo and two-client routes, and macOS builds pass. True foot planting and continuous human motion review remain open; this does not close M2.

The [September 28 wagon form checkpoint](GRAPHICS_WAGON_FORM_PASS_2026-09-28.md) replaces the four camp wagons' box-like body and cabin with tapered original panels, raked glass and a crowned roof. Neutral multi-angle and native camp captures record the difference. Vehicle contact, paint and light response, the camper, campsite composition, and the reference-level character/environment interaction remain open; M2 remains open.

The [September 28 grounded locomotion checkpoint](GRAPHICS_FOOT_CONTACT_PASS_2026-09-28.md) gives the shared player/NPC rig world-space support feet, lifted swing feet and a visual pelvis response. A PlayMode contact test, native full-body frames, and a profiled two-client run support the change. Authored stops and turns, uneven-terrain contact, dance weight transfer, object interaction and human-paced reference review remain open; M2 remains open.

The [September 28 stage contact checkpoint](GRAPHICS_STAGE_CONTACT_PASS_2026-09-28.md) adds original folded scenic wings, a lighted backline, two stage dancers and a resident DJ whose hands reach the raised mixer. A close native capture and PlayMode contact check support the hand placement; two-client smoke and macOS builds pass. Player DJ takeover choreography, sustained performance and reference-level environment finish remain open; M2 remains open.
