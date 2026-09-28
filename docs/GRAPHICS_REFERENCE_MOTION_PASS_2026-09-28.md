# Poi and rave-motion reference pass — September 28, 2026

## Motion references

- [Paired practice poi](https://www.youtube.com/shorts/eCaw93VEEQk): both weighted heads rise and fall together while their lateral paths mirror and periodically cross. The handles stay in the performer's hands near the upper chest.
- [Rave dance compilation](https://www.youtube.com/watch?v=hG0DZpcLNbk): varied hakken, shuffle and jumpstyle footwork with a supporting leg, upper-body rhythm, and individually paced dancers. The game uses its original characters and motion; no footage or animation assets were imported.

## Implemented

- The two poi now use mirrored butterfly circles rather than alternating high and low orbits. A lateral offset and separate front/back paths keep the weighted heads from colliding as they cross. The rope remains taut at the established length, and each head still lags its moving hand. The grip pose rises so the high arc can reach the face and hat height.
- Dance cycles now take roughly 0.71–0.83 seconds per pair of steps, with smaller kicks, varied elbows, shoulder accents and slower head attention. Existing world-space supporting feet and authoritative actor roots remain intact.
- Development builds log poi and dance pose entry, including dance style and cadence. The new logs are compiled out of release builds.

## Native evidence

- Poi sequence: [low outward](graphics-reference-motion-2026-09-28/client-poi-motion-0.png), [crossing](graphics-reference-motion-2026-09-28/client-poi-motion-1.png), [high outward](graphics-reference-motion-2026-09-28/client-poi-motion-2.png), [high center](graphics-reference-motion-2026-09-28/client-poi-motion-3.png), [front/back pass](graphics-reference-motion-2026-09-28/client-poi-motion-4.png).
- Three-character sequence: [frame A](graphics-reference-motion-2026-09-28/client-motion-0.png), [frame B](graphics-reference-motion-2026-09-28/client-motion-2.png), [frame C](graphics-reference-motion-2026-09-28/client-motion-4.png).
- The new paired-orbit PlayMode test failed against the old animation and passed after the change. A chest-height grip check likewise failed on the low pose and passed after raising it; a head-separation check failed before the paths were separated. The full PlayMode suite, Mac development build, scripted two-client mission, and native capture pass.
- A short populated 1280×720 Ultra sample on this Mac reported 16.8–17.1 ms p95 in the client. This is a smoke sample, not sustained performance or a Windows minimum-hardware result.

## Remaining quality gap

These are procedural approximations. The stills show readable phase changes, but cannot establish natural timing through a full watched loop. The raised hands occasionally put a handle and cord in front of the face, and the dance remains more regular and stiff than the reference performers. Authored animation, grip and collision polish, clothing response, transition choreography, and a human-paced in-motion review are still needed. The M2 visual acceptance gate remains open.
