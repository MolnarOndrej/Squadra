# Moon visuals

## Problem Statement

Lune is Squadra's only moon look, so missions with several moons repeat the same surface. Players need enough visual variety to tell moons apart at battlefield scale while still recognizing them as moons alongside planets and asteroids.

## Solution

Add five round, airless moon looks alongside Lune for a total of six. Give each new moon a distinct palette and one large surface feature that reads at the default moon radius of 0.5. Make all six available through ordinary mission preparation and document the five additions in the visual catalog with in-game thumbnails.

## User Stories

1. As a player, I want missions with several moons to show a variety of surfaces so that the battlefield feels less repetitive.
2. As a player, I want to distinguish one moon from another and recognize moons alongside planets and asteroids at normal battlefield scale.
3. As a player, I want the moons to reveal finer terrain features when I see them up close.
4. As a player, I want to read ownership, selection, capture progress, world labels, and nearby routes clearly against each moon surface.
5. As a mission author, I want the new looks to appear through ordinary mission preparation so that existing mission files can use them.
6. As a visual author, I want to compare the new looks with Lune, with similar planets, and with one another so that each addition earns its place in the pool.
7. As a visual author, I want the catalog to show the full moon lineup with thumbnails, stable IDs, and asset links so that I can plan future batches.

## Implementation Decisions

### Moon lineup

Use Lune as the comparison baseline. Its cool-gray disk and bright rayed crater already read at gameplay scale. The names below describe the concepts. Choose final display names and unique stable IDs before registration.

| Concept | Large feature at gameplay scale | Close-up detail | Closest existing comparison |
| --- | --- | --- | --- |
| Ice rifts | Pale cyan ice crossed by two or three long, dark-blue fractures | Frost along fracture edges and smaller branching cracks | Rime shares the ice and fissures. Check that the broad fractures and round moon disk read in a mixed scene with Rime's ringed globe. |
| Basalt plains | Broad, smooth charcoal plains broken by islands of warm-gray highlands | Buried crater rims and overlapping lava flows | Halite also has a dark base. The highland islands have softer, darker edges than Halite's bright salt flats. |
| Two-tone moon | Ivory terrain meeting a broad rust-brown region along a ragged boundary | Dust streaks and bright rock exposed by small impacts | Ember shares the warm rock tones. The large ivory region and irregular boundary distinguish the moon from Ember's glowing fractures. |
| Terraced basin | A smoky violet moon with a wide, pale ring around one deep impact basin | Stepped walls, slumped debris, and nested craters | Lune shares the impact theme. The closed rim reads differently from Lune's bright radial ejecta. |
| Sulfur deposits | Golden-ochre ground with broad vermilion fans | Fine mineral bands and dark vents | Solfatara shares the sulfur theme. The red fan shapes and ochre base distinguish the moon from Solfatara's yellow-green ground and dark deposits. |

### Rendering and integration

- Before creating or refining assets, fill in the reusable visual brief for each of the five moon candidates. Use the agreed lineup and the visual catalog to define the gameplay-scale feature, closest comparisons, close-up detail, rendering approach, and planned Sandbox captures. [Reusable visual brief](visual-variant-workflow-spec.md#reusable-visual-brief)
- Keep the five silhouettes round and use airless surface treatments under the existing battlefield lighting. The terraced basin's pale ring is an impact rim on the surface. Make patterns broad enough to survive gameplay-scale rendering and use finer texture or relief for close views.
- Reuse the existing world material and mapped-globe rendering where they suit a look. For a mapped globe, start with a seamless, unlit 2:1 surface map with mipmaps. Use the workflow's 4096 × 2048 starting point for close-view detail and choose the final texture size from the rendered comparison and the resource cost. Supply relief data where it makes terrain features read more clearly.
- Register the five accepted materials in the Moon section of WorldLooks through the existing material discovery workflow. Retain Lune and its `lune` ID. All six looks are equally eligible in the existing shuffle, which deals from the Moon pool before repeating a look. Mission preparation assigns surface IDs and world names independently.
- Add the five accepted looks to the Moon section of the visual catalog in the same change that registers them. Include the display name, stable ID, description of what reads at gameplay scale, rendered thumbnail, and links to the defining Unity assets. Record sources and attribution for any adapted imagery. Update the catalog's rendering description if the new moons use a different rendering path from Lune.

## Testing Decisions

- Use the existing Sandbox comparison workflow at the default moon radius and normal battlefield framing. Keep the camera, backdrop, lighting, and post-processing matched across all six moons. Compare each candidate with Lune and the other candidates. Accept it when its large feature is recognizable at gameplay scale. Revise candidates whose patterns blend together or disappear at that scale.
- Check a mixed battlefield with nearby planets and asteroids. Compare Ice Rifts with Rime, Basalt Plains with Halite, Two-Tone Moon with Ember, and Sulfur Deposits with Solfatara. Make sure the moon silhouettes and surfaces remain readable against the space backdrop, including the darker Basalt Plains.
- Inspect each new look up close and through rotation in Play Mode. Check that the terrain reads from representative angles and that texture seams, poles, or relief artifacts do not interrupt the surface. In Sandbox Play Mode, check neutral and owned moons, selection, and capture. Check the owner ring, world labels, and routes in a crowded arrangement.
- Prepare a representative mission with six moons through the ordinary assignment path. Verify that each of the six surface IDs is assigned once and resolves to the intended material. Play the mission to check that all six looks render through the normal gameplay path.
- If the new maps, relief, or shader settings change rendering cost, compare typical and 95th-percentile CPU and GPU frame times before and after on the same representative battlefield after warm-up. Investigate sustained differences beyond normal run-to-run variation.
- Cross-check the Moon section of the catalog with the six registered Moon looks. Verify that each appears once and that the thumbnails and asset links work.

## Out of Scope

- Asteroid visuals and other visual batches.
- Changes to Lune's appearance, existing missions, Moon names used in missions, or the saved Sandbox layout.
- Changes to mission format, world size, gameplay rules, visual assignment, or selection and capture behavior.
- New preview tooling and in-game catalog UI.

## Further Notes

The catalog thumbnails show close detail. Use the separate gameplay-scale comparisons for acceptance. [Visual catalog capture workflow](../docs/visual-catalog.md#adding-a-look)

### Assumptions still to validate

- Tune feature width, contrast, and placement in rendered comparisons. Confirm that the two-tone boundary and the basin rim read across representative views as the moons rotate.
- Choose the closest review framing and final texture budget after checking detail at that view and at the default battlefield scale. If profiling is needed, select a representative battlefield and reference hardware before collecting the baseline.
