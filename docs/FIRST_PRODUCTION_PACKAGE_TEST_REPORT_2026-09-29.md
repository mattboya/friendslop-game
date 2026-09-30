# First production package: independent Unity review

Date: September 29, 2026. Verdict: **imports and runs in an isolated macOS development sample; visual and gameplay acceptance remain open.** The original gameplay scene, resource prefabs, build list, and source FBXs were not replaced. This report tests the package in the shared workspace while other simulation, HUD, and poi work was in progress.

## Evidence and confirmed passes

- Unity 6000.3.24f1 compiled the package, assembled `Assets/Festival/Art/ProductionSample/FirstProductionSample.unity`, and built `Builds/macOS/ProductionSample/FirstProductionSample.app`. Native screenshots came from that app at 1280 × 720 with URP Lit, dusk lighting, two stall lamps, and the sample camera. See [overview](first-production-package-review/overview.png), [front](first-production-package-review/front.png), [conversation](first-production-package-review/conversation.png), [left](first-production-package-review/left.png), and [back](first-production-package-review/back.png).
- Both characters import as Generic rigs with two 1.97-second looping clips each: attendee `Idle`/`Dance`, vendor `Welcome`/`Offer`. The 12 imported blendshape channels per character include grip, blink, brow, and mouth channels; their neutral weights are zero. All sampled renderer materials resolve to URP Lit. See [import audit](first-production-package-review/import-audit.txt).
- The static stall and path are combined into 10 and 8 Unity renderers respectively. The entire sample has 112 renderers, about 88,114 imported triangles, and three lights. The source `.blend` opens in Blender and contains 306 separate mesh objects in `AH01_Production`; source parts remain editable.
- Four display poi have distinct handle, tether, and head renderers. The straight samples hang at the display hooks. The stall's support posts, shelf tiers, counter, sign, and paired display are visible from the front and sides. At the captured poses, shoes appear near the floor and the vendor remains behind the counter.
- The native 180-frame sample measured 8.47 ms average frame interval, 33.21 ms maximum, and 80.7 MiB allocated Unity memory on this Mac. These are short, isolated-scene observations; they are not GPU/CPU profiler costs or a populated-festival target-hardware result. See [metrics](first-production-package-review/runtime-metrics.txt).
- Domain suite passed. Unity EditMode: 31/31. Unity PlayMode: 19/19. These checks exercised current shared-workspace code, including concurrent edits, but do not validate this package inside an actual mission.

## Defects and visual findings

1. **Expression extremes lack clear read.** Applying `Blink` and `Delight` at 100 in the native sample leaves the eyes visibly open and the faces close to neutral at gameplay distance. See [expression capture](first-production-package-review/expressions-100.png). The channels exist; this is a visual result, not proof that their vertex data is absent. Inspect the imported shape deltas and animation/blendshape evaluation before accepting facial acting.
2. **The dance and offer samples read as very small upper-body motion.** The [two-second capture](first-production-package-review/dance-welcome.gif) shows slight pose movement, while the planted legs and presentation distance make neither dance nor handoff legible as a finished interaction. This matches the handoff's stationary acting scope, but it misses the stated gameplay-quality target.
3. **The review composition obscures the shop in conversation range.** In the [conversation view](first-production-package-review/conversation.png), the attendee blocks much of the counter and price sign; the seller is comparatively small. The smaller sign text is hard to read from the normal front view. This should be checked with the actual gameplay camera and UI before changing geometry.
4. **The dressed route is very dark in the dusk overview.** Ground, dirt, and sparse foliage largely merge into one value from the [overview](first-production-package-review/overview.png). The bollards and stall lamps read, but the route itself does not yet guide approach clearly.
5. **The sample does not demonstrate hand contact.** Poi remain on hooks or the counter; the attendee holds none. The vendor's `Offer` animation has no object transfer. No collision, IK, or full motion contact assertion was made from these stills.

Imported triangle totals differ from the Blender manifest for the vendor (26,496 vs. 26,364) and stall (20,868 vs. 19,804); the other four totals match. This is likely export/import triangulation, but the exact cause has not been isolated. It does not presently block rendering.

## Untested gates

- Conversation, dance scoring, purchase/inventory transfer, first-person and remote-player hand contact, and two-client visibility **with these new assets**. The current runtime loads the shared `FestivalCharacter` resource by name; the new sample FBXs are isolated and have no gameplay binding. Swapping that resource would change all actors and exceed this focused import review.
- Full animated shoulder, sleeve, apron, hat, foot, and poi contact across clips and game poses; static views and the short acting loop provide partial coverage only.
- A human visual acceptance pass against the supplied references, normal walking approach, festival lighting among existing scenery/crowds, and a populated performance/profile run on target hardware.

## Recommended next asset iteration

First make blink and mouth/brow extremes readable in Unity at normal conversation distance, then increase the silhouette and weight shift of the dance and give the offer a clear reach/contact pose. In the isolated scene, move the camera and attendee through a real approach route and adjust sign readability and route contrast using native captures. After those changes, bind the attendee, vendor, and stall through a reversible development-only gameplay fixture and rerun the actual conversation, dance, purchase, and poi contact transitions from both clients before considering wider replacement.

## Fixture changes and reproduction

The editor scene builder needed three Unity-specific repairs: batch mode cannot open a new additive scene while its untitled scene is unsaved; Unity objects cannot safely use C# null-coalescing for an optional Animator; and a partially created controller can have zero layers. `ProductionSampleBuilder.cs` now handles those cases. `ProductionSampleAudit.cs` writes import facts and builds only the sample scene. `ProductionSampleCapture.cs` is compiled only in Editor/development builds and logs/captures only when `--sample-output` is supplied. No release-build logging was added.

Run `Festival.Editor.ProductionSampleAudit.AuditAndBuild` in batch mode, then launch the isolated app with `--sample-output <absolute-directory>`. Logs remain in `artifacts/first-production-audit-build.log` and `artifacts/first-production-sample/native.log`; selected evidence is preserved in `docs/first-production-package-review/`.
