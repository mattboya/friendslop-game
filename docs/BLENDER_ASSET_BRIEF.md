# After Hours — Blender asset brief

Confirmed in the September 24–25, 2026 asset interview. This brief is the first festival's art target. The first playable asset pass is now implemented; the full ten-variant library and final art acceptance remain open. It builds on [the approved game direction](APPROVED_DIRECTION.md).

## Art target

The setting is a fictional, well-equipped touring EDM festival temporarily set in a temperate, late-summer forest at dusk. The stage has large trusses, speakers, decks, and colored lights. **The graphics stay intentionally goofy and stylized.** Use readable faceted forms, lanky or uneven bodies, oversized hands and shoes, expressive faces, playful props, and loose offbeat dance motion. Give locations enough detail to distinguish their purpose without making the world feel visually premium or realistic.

Use saturated stage LEDs against warm path lights and cool dusk. The festival has one fictional name and an interlocking Sun/Moon symbol. The name is still undecided; keep sign text replaceable while building the symbol and sign geometry. The forest has an open stage clearing and short winding paths. Danger begins as comic panic and can become briefly unsettling, without gore.

## Production order

| Priority | Asset package | First playable target |
| --- | --- | --- |
| 1 | Shared character and first-person rigs | Three body silhouettes, expressive faces, grounded movement, always-visible hands and sleeves |
| 2 | Modular wardrobe | 3–5 options per category for playtests; robust random combinations |
| 3 | Gameplay cast | Crowd, role uniforms, missing friend, readable hostility progression |
| 4 | Hero interactions | LED poi, recovery wristband, DJ decks, Sun/Moon landmarks |
| 5 | Festival landmarks | Stage, market, medical, security, shuttle, forest kit |
| 6 | Supporting gear and dressing | Simple 3D models for visible usable items and environmental clutter |

The **full first-festival library target** is ten variants in each wardrobe category. The initial 3–5 options per category allow earlier human playtests while the library expands. Ten hair colors can be palette variations rather than ten hair meshes.

## Character system

- Use one compatible skeleton and modular fitting rules for **lanky, average, and stocky** adult silhouettes. Generated attendees include men and women, with varied face shapes, skin tones, and apparent adult ages roughly spanning the 20s through 50s.
- Gender selects body, face, and voice pools. Every clothing category is available to everyone. Facial hair is restricted to men in this brief.
- Model or author **ten eventual variants each** for headgear, sunglasses, shirts, lower garments, shoes, facial hair, hairstyles, and accessories: 80 item variants across eight categories. Lower garments include pants, shorts, skirts, tutus, and similarly playful forms. Add ten hair colors, mixing natural and vivid dyed colors.
- Headgear, sunglasses, facial hair, and accessories each have a chance of being absent. Generate deliberately absurd combinations freely. Every combination must avoid obvious hair, hat, glasses, beard, sleeve, waist, and leg clipping; do not silently forbid odd outfits to solve fitting.
- Players can combine outfits and accessories across the three body shapes. First-person hands remain visible throughout play and match the selected skin tone, body shape, and shirt sleeves. Held props and relevant hand actions must read from this camera.
- Make faces readable through large eyes, noses, brows, and mouths. Provide expressions for conversation, dancing, suspicion, panic, injury, relief, and cheer. Feet should remain grounded during the intentionally loose, offbeat dances.

### NPC cast

| Role | Blender and presentation need |
| --- | --- |
| Ordinary attendees / wooks | Modular adults generated from the wardrobe library. A hostile transition progresses from changed posture and locked stare to coordinated movement, stronger eye glow, and stronger color change. They remain recognizably human. |
| Security officers | Shared character system with clearly distinct festival officer vests and radios; readable patrol, intervention, and detention actions. |
| Three vendors | Shared body and face system with three role-specific stall uniforms or accessories. |
| Medic | Shared body and face system with a distinct clinic uniform and readable assistance actions. |
| Missing friend | Ordinary attendee in a distinctive jacket and backpack, easy to recognize once nearby. |

The current scenario has roughly 24 crowd NPCs and two officers. Exact visible counts remain subject to native performance and gameplay tests.

## Hero props and landmarks

| Asset | Shape and interaction |
| --- | --- |
| LED poi | A pair of tethered glowing orbs with visible cords. The meshes should support hand attachment and clear light trails during performances. |
| Recovery wristband | Slightly oversized, with a readable icon and glow. It is the dropped item for **teammate revival**, not the missing-friend identifier. |
| DJ station | Oversized tactile decks and faders on the polished touring stage. Controls need moving parts or clear animation pivots for close first-person use. |
| Sun/Moon clue totems | Two distinct illuminated sculptures with solid, touchable bases. The private clue cue remains a viewer-specific presentation effect. |
| Festival minibus | Colorful exterior, usable door, and simple visible interior for boarding and extraction. |

Give secondary visible items simpler models for the first art pass: confetti cannon, merch bag, map, stage pass, fictional stock containers, medical voucher, and stash equipment. Keep item shapes and labels fictional and readable; do not base them on real products.

## Environment kit

- **Main stage:** a polished *in-world* EDM setup with chunky truss, speakers, LED panels, lights, and a DJ booth. Keep geometry and surfaces stylized and a little funny. The stage is the high-energy visual anchor, not a shift to realistic graphics.
- **Night market:** three compact role-specific vendor stalls gathered in one market area. Give each stall a distinct silhouette while reusing smaller details, palette rules, and festival branding.
- **Medical:** a clean temporary clinic with bold signage and a small playable interior. Include only the readable furnishings needed for recovery interactions.
- **Security:** a portable cabin with a visible holding room and a small playable interior. Its form and color should be recognizable from a distance.
- **Shuttle stop:** a clear extraction landmark around the enterable colorful minibus.
- **Forest and connective dressing:** mixed temperate trees, grass, path edges, barriers, strings of warm lights, signposts, bins, and other low-cost repetition. Preserve clear routes and sightlines through the stage clearing.
- **Pre-level campsite:** a separate walkable camp with parked cars, tents, two sun shade shelters over the middle gathering space, a porta potty, and a visible male seller. The seller is the proximity-gated pre-level shop. The $1 Little Spoon is a small wearable spoon necklace with no active use; its slight wook-trust benefit is kept out of public item text.

Locations have distinct architecture, but may share materials, small fittings, and the Sun/Moon brand language. This keeps them recognizable within the one-festival scope.

## Technical and review requirements

The [character manifest](../ArtSource/character-manifest.json) now describes a shared 13-bone modular FBX with three body shapes per gender pool, three face variants per gender pool, and four mesh variants for each of the eight wardrobe categories. A generated runtime palette supplies eight skin tones and ten hair colors. [First-person hands](../ArtSource/hands-manifest.json) and [26 world and prop models](../ArtSource/world-manifest.json) are also original Blender exports. Unity selects wardrobe slots deterministically, shows matching camera hands and selected held props, and uses Blender visuals over proxy collision and navigation geometry. Sources and generators are listed in [asset provenance](ASSET_PROVENANCE.md).

The second character pass freezes the current four variants per category and tailors non-body meshes with six fit shapes. Shirts and role layers use faceted shells, skirts flare for leg motion, and opaque hats hide top hair while open hats show a compressed cap and distinct lower locks. The deterministic appearance hash now reaches every optional variant. `scripts/audit_character_fit.py` checks torso, leg, shoe, role, hair/hat, backpack, and sampled pose interfaces; its report is `artifacts/character-fit-audit.json`. A Solid viewport review scene is saved at `artifacts/character-fit-review.blend`, with orthographic review captures under `artifacts/fit-review/`.

These checks sample important combinations and all named gameplay poses; they do not mathematically prove every possible 4-variant outfit combination or every deforming surface. Expression is currently mostly body pose and escalating eye/body tint; individual facial-expression animation, articulated finger poses, hand contact, DJ control movement, and a boarding animation are still needed. The minibus has an open doorway and visible interior, but extraction currently resolves at its entrance. The full ten variants per category, player-facing outfit selection, and adult-age variation need later work.

Keep Blender source files, exports, material palettes, and naming organized so variants can be regenerated and reviewed. Share skeletons and materials where useful. Design clothing attachment and fit checks for all three body shapes and both gender pools. Keep lighting, glow, and trails as Unity presentation effects where that makes them easier to tune and cheaper to render.

For any later runtime asset-selection code, add development-build-only diagnostics for generation seed, selected body and wardrobe slots, missing meshes, and attachment errors. Exclude these diagnostics from release builds.

Before calling a package done, review it in the native Unity build at first-person and crowd-view distances: role recognition, silhouettes, material consistency, clipping across random combinations, hand/prop contact, foot grounding, readability of hostility stages, and frame time with a full crowd. No such acceptance is claimed by this brief.

## Changelog

- Added: confirmed character, prop, NPC, and environment asset targets from the clickable interview.
- Changed: the polished EDM stage is defined as an **in-world structure** within intentionally goofy, modest-detail graphics; it is not a premium-fidelity art target.
- Changed: first playable assets are implemented with four variants per wardrobe category, original Blender hands, prop and environment exports, and Unity integration. The ten-variant and close-up animation targets remain open.
- Changed: the current four variants now have six fit shapes, revised torso and role shells, flared skirt clearance, and hat-specific hair layers; deterministic selection can reach all four optional variants.
- Added: original campsite car, shade, porta potty and Little Spoon models plus a wearable spoon mesh in the shared character asset.
- Explicitly removed: no confirmed game behavior or asset category was removed by this brief.
- Changed (2026-10-01, ART-1): Palm Mirage, festival 1, now has a polo-field look with palms, a lawn, desert ridges, a Ferris wheel, a dressed main stage and three landmarks. This replaces the temperate-forest target for that festival only. The Giggle Tank, balloons and vision creatures also changed, at every festival and camp, not only Palm Mirage.
