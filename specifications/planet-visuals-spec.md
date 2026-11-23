# Planet visuals

## Problem Statement

Squadra has two planet looks, Ember and Thalassa. With only two choices, a battlefield with several planets repeats the same surfaces quickly. Players should be able to recognize more planets by appearance, and the author should be able to see which looks are available in the game.

## Solution

Add eight detailed planet looks to the existing pool for a total of ten. Give each new globe a distinct palette and a large feature that reads at normal battlefield scale. Add two distinct planetary rings as part of those eight looks. Make all eight available through ordinary mission preparation and record them in the visual catalog with in-game thumbnails.

## User Stories

1. As a player, I want more visually distinct planets so that battlefields with several planets have more variety.
2. As a player, I want to recognize each new planet at ordinary battlefield scale and see finer details in close views.
3. As a player, I want ringed planets to be less common than unringed planets so that finding one feels distinctive.
4. As a player, I want the two ringed planets to have visibly different rings.
5. As a player, I want ownership, selection, capture progress, world labels, and routes to stay readable around a ringed planet.
6. As a player, I want a battlefield with the new looks to run comparably to the same battlefield with the existing looks.
7. As a mission author, I want the new looks to appear through the existing planet assignment path without editing mission files.
8. As a visual author, I want to browse the complete planet lineup with in-game thumbnails, stable IDs, descriptions, and Unity asset links.

## Implementation Decisions

### Planet lineup

The names below describe the concepts. Choose display names and unique stable IDs before registering the assets. Keep Ember and Thalassa in the lineup with their current IDs.

| Concept | Large feature at gameplay scale | Close-up detail |
| --- | --- | --- |
| Amber gas giant | Broad cream and caramel latitude bands, a large oval storm, and a broad, shallow-tilted champagne ring with a visible dark gap | Swirls along the storm edge and finer striations within the bands |
| Ice world | A pale blue-white globe with deep blue fissures and a narrow, steeply tilted slate-blue dust ring | Branching cracks in the ice and a soft, dusty ring edge |
| Dune world | Golden sand with long, sweeping dune belts | Wind-carved ridges and layered sand tones |
| Ocean world | Open deep navy water with one large white spiral storm | Broken outer cloud arms and fine cloud wisps |
| Verdant world | Dark green land broken up by broad, pale cloud banks | Tonal variation across the terrain and soft edges on the cloud banks |
| Salt basin world | Large, irregular white salt flats on a charcoal globe | Layered basin edges and fine mineral textures |
| Sulfur world | A yellow-green globe cut by broad, dark mineral streaks | Mottled mineral deposits and variation along the streak edges |
| Cloud world | A lavender globe wrapped in thick, diagonal violet cloud belts | Layered cloud edges and finer wisps between belts |

### Rendering and integration

- Use broad surface or cloud patterns for recognition, with finer details at close range. The visual work may use generated or hand-authored textures, new or customized shaders and materials, procedural effects, or dedicated geometry where they improve a look. Choose the rendering approach per look and keep the results coherent with Squadra's lighting and battlefield presentation.
- Treat the two planetary rings as part of their planet looks. Show the far side passing behind the globe and the near side crossing in front, with the ownership, selection, and capture indicators readable wherever they overlap. Tune ring extent, opacity, and orientation against the labels and routes. Keep the planetary rings cosmetic.
- Use the profile comparison to guide texture sizes, shader cost, geometry, and ring effects across the full lineup. Choose techniques that deliver the intended detail at gameplay scale and in close views within the available frame-time headroom.
- Register the eight accepted looks in the planet section of `WorldLooks.asset`. Use the existing material discovery workflow where it fits. A look can reference additional rendering assets as needed. Keep the visual selection interface, parameters, and shuffle rules unchanged. Each of the ten planet IDs is one equally eligible entry in the existing shuffle. The lower frequency of ringed planets comes solely from the pool containing two ringed looks and eight unringed looks. Individual missions can draw a different proportion when they use only part of the pool.
- Add one entry per accepted look to the planet section of `visual-catalog.md` in the same change that registers the assets. Include the display name, stable ID, description of what reads at gameplay scale, a rendered thumbnail, and links to the defining assets. Update the shared rendering description to reflect the techniques used. Use the catalog's Sandbox framing and adapt the capture workflow as needed to show the actual in-game rendering in thumbnails and comparison views. Both ringed thumbnails show the entire ring.

## Testing Decisions

- Use the visual catalog's Sandbox workflow to compare all ten looks at the default planet radius and normal battlefield framing, under the same camera, backdrop, and post-processing settings. Check each new look against its closest neighbor. The ocean storm should distinguish the ocean world from Thalassa, the dune belts should distinguish the dune world from Ember, and the cloud belts should distinguish the cloud world from the gas giant even when part of a ring is obscured.
- Inspect all eight looks up close and during rotation. Check that the large features remain recognizable and that seams, poles, cloud movement, and ring occlusion look coherent. At gameplay scale, check that the broad split ring and the narrow dust ring are distinct from each other and from ownership indicators.
- In Sandbox Play Mode, view ringed planets in neutral and owned states, while selected and during capture. Check readability of the ownership, selection and capture indicators, labels, and nearby routes. Use a crowded representative layout to catch overlap.
- Verify normal mission preparation with a setup containing ten planets and the existing selection settings. Confirm that all ten distinct IDs resolve to the intended looks, including both ringed variants, before any look repeats. Check a normal mission in Play Mode to confirm that the rings render there as well.
- Compare CPU and GPU frame times before and after on the same representative battlefield, with the same hardware, build settings, resolution, camera, and non-planet content. Include both ringed looks in the new lineup. After warm-up, record typical and 95th-percentile frame times over repeated runs. Investigate any sustained slowdown or recurring spikes that exceed normal run-to-run variation and materially reduce frame-rate headroom.
- Verify that the catalog and planet pool each list the same ten looks once, that the eight new thumbnails render correctly, and that their asset links resolve.

## Out of Scope

- Dart visuals, orbit behavior, and Dart-related visual acceptance.
- Mission format, gameplay rules, planet size, and planet selection or capture behavior.
- New selection arguments, rarity weights, ring-specific filters, changes to existing visual selection parameters or assignment rules, and new looks for other object types.

## Further Notes

- Follow the [visual variant workflow](visual-variant-workflow-spec.md) and use the [visual catalog](../docs/visual-catalog.md) as the reference for existing looks and preview conditions.

### Assumptions still to validate

- Validate the rings' width, tilt, opacity, and extent in rendered comparisons. The concept descriptions define how the two rings differ and leave exact values to visual review.
- Establish the reference hardware and benchmark battlefield before profiling. No frame-time budget has been specified.
