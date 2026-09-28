# Stage scenery and DJ contact checkpoint — September 28, 2026

This is a checkpoint within the active [graphics upgrade](GRAPHICS_UPGRADE_PLAN_2026-09-27.md), using the [Dear Passengers teaser](https://www.youtube.com/watch?v=luWunttkCgY) as a quality reference. The stage, characters and animation here are original project work; no reference-game asset or footage was imported.

## What changed

- The scripted Blender stage now has two folded, bound scenic wings with inset light cassettes and a curved metal-and-light backline behind the DJ. These large shapes make the rear wall read more deliberately from the crowd. The stage collision, footprint and interaction anchor are unchanged.
- A resident DJ and two facing stage dancers make the platform active. The DJ has a restrained upper-body performance pose; a visual-only two-bone arm solver keeps both hands on the raised mixer during that motion. The solver never moves a gameplay root or changes rhythm timing.
- Stage glow materials are mapped to the shared URP art material system. A development-only diagnostic reports an unreachable DJ target; the release assembly excludes `[Festival.Motion]` diagnostics. The stage performers use the existing distance mesh policy.
- Native smoke now captures the crowd approach, a stage-wide view and a close DJ-console view.

## Visual and measured evidence

The [neutral front inspection](graphics-stage-contact-2026-09-28/stage-neutral-front.png) shows the stage's physical scenic forms. The [native crowd approach](graphics-stage-contact-2026-09-28/crowd-stage.png), [stage detail](graphics-stage-contact-2026-09-28/stage-detail.png) and [DJ contact](graphics-stage-contact-2026-09-28/dj-contact.png) show the final source in the macOS development player at 1280×720. The close view shows the DJ's hands over the mixer and the performers facing the audience. Still frames do not prove the naturalness of the full motion cycle.

The single stage source grew from 7,756 to 11,996 triangles and from 12 to 14 material renderers; it remains one stage instance. A PlayMode contact test passed with the left hand within about 4 cm of its moving mixer target in its sampled frame, and all 11 PlayMode tests passed. Unity validation, EditMode tests, project static checks, macOS development and release builds, release diagnostic exclusion, and the scripted two-client native mission passed. The final native run logged no DJ reach warnings.

The final two-client point samples in populated festival views were 17.0–17.6 ms p95 at 1280×720 Ultra. These short samples do not establish sustained 60 fps or a Windows minimum-spec result. The earlier extra DJ spotlight was removed after a weak visual result and higher, non-comparable samples; the retained performers follow distance LOD.

## Still open

The player-triggered DJ takeover still starts from the existing stage-front interaction anchor; its authored body-to-console choreography is not complete. Stage floor finish, cables, equipment cases, moving-light integration, flatter ground and repeated woodland still fall short of the reference. Human-paced dance and DJ motion review, full first-level visual acceptance, and named target-hardware profiling remain required.
