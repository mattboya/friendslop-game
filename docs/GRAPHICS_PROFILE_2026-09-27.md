# Initial Mac graphics profile — September 27, 2026

The later [environment checkpoint](GRAPHICS_ENVIRONMENT_PASS_2026-09-27.md) contains a second native capture set and the post-shape-pass point samples. The measurement limits below still apply.

This is an initial cost breakdown for the [graphics upgrade plan](GRAPHICS_UPGRADE_PLAN_2026-09-27.md). It is a scripted two-client smoke run on the Mac listed in the [baseline](GRAPHICS_BASELINE_2026-09-27.md), at 1280×720, Unity Ultra quality, seed `57479502`, development build GUID `5a5842f7ad3b4a869c8be0bbba0ca1f4`. The client ran with `FESTIVAL_GRAPHICS_PROFILE=1`; the F9 overlay remained hidden so it did not appear in captures. Both clients passed the native mission.

| Client point sample | Frame p95, recent window | CPU frame | GPU frame | Visible renderers | Distant characters | Animation updates/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Camp shopping | 9.1 ms | 8.5 ms | 0.8 ms | 228 | 1 | 363 |
| Populated festival | 15.9 ms | 14.9 ms | unavailable | 985 | 57 | 3,181 |
| Populated festival, expensive view | 16.7 ms | 15.5 ms | 12.2 ms | 1,155 | 54 | 2,861 |
| Later festival view | 15.6 ms | 8.3 ms | 8.5 ms | 976 | 40 | 3,210 |
| Results | 16.3 ms | 8.3 ms | unavailable | 903 | 44 | 3,987 |

The scripted rhythm harness separately reported **16.8 ms p95**, 41.7 ms maximum, over 512 frame interval samples. The opt-in overlay p95 values are rolling 240-frame windows, while CPU/GPU values are individual latest frame timings. They are different measures; do not compare them as if they were one matched frame. GPU timing was unavailable in two samples. Two local processes, scripted teleports, development settings, window focus and warm-up behavior limit this evidence. It is not a two-minute traversal, full mission at natural pace, 1080p test, Windows benchmark or eight-machine load test.

The expensive view's 1,155 visible renderers and 15.5 ms reported CPU frame time suggest crowd rendering and animation need targeted profiling before denser scenery is accepted. This is an inference from one sampled view, not an identified root cause. Capture CPU/GPU profiler timelines, draw calls, material slots, skinned mesh cost and memory before choosing optimizations or a quality preset.

## Campsite pass follow-up sample

After the camp and character changes, the same two-process 1280×720 Ultra development smoke ran with opt-in graphics sampling. Build `52924ede798a409aaf60dd060728d780`, seed `61446558`:

| Client point sample | Rolling frame p95 | CPU frame | GPU frame | Visible renderers |
| --- | ---: | ---: | ---: | ---: |
| Expanded camp | 9.2 ms | 8.3 ms | 1.3 ms | 369 |
| Populated festival | 16.9 ms | 8.3 ms | 10.4 ms | 1,084 |
| Populated festival, second sample | 16.7 ms | 12.6 ms | 12.1 ms | 1,076 |
| Results | 17.1 ms | 8.3 ms | 7.3 ms | 905 |

The camp has more visible geometry while its point sample remains near the earlier frame time. These are different seeds and short rolling windows, so they do not prove a performance gain or a 60 fps guarantee. The populated festival remains near or above a 16.7 ms frame budget. Raw ignored logs: `artifacts/native-smoke/client.log` SHA-256 `4191897edfd9d8bdbd4d8a6e10bf8f037701cde3f7739cb368f02a43009c22a4` and `host.log` SHA-256 `bf1c6d7ff66e04b15a7d7207dd75c8b354d733b858c545a5188ce7864a5fcbf3`.

## Repeatable visual views

- [Camp arrival](graphics-profile-2026-09-27/created-camp.png)
- [Populated stage](graphics-profile-2026-09-27/host-crowd-live.png)
- [Character lineup](graphics-profile-2026-09-27/client-character-quality.png)
- [Rhythm camera](graphics-profile-2026-09-27/client-rhythm.png)

These captures show no obvious composition loss from removing the duplicate bloom volume and aligning the sun requests with the pipeline's hard-shadow support. They do not establish art acceptance or animation quality. The full native logs are preserved locally in ignored `artifacts/graphics-profile-2026-09-27/` (client SHA-256 `437698ab68e85dcd7f959f69c98e1ffbbf82cd75361bae3199dd03ff9ea8ae88`, host `cbaf806eb567ebd0296bbc9c9cd454f55c79b9b36b902cd9c6f4633ded908a91`).
