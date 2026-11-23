# Asteroid visuals

## Problem Statement

Squadra has two asteroid looks, Cinder and Ferrite. At normal battlefield scale, their craters mostly blend into the surface and their color is the clearest difference. A battlefield with several asteroids needs a wider range of silhouettes and surface patterns that players can recognize at the default asteroid radius.

## Solution

Add five rocky asteroid looks alongside Cinder and Ferrite for a total of seven. Give each new look a distinct silhouette and a broad surface feature that reads at the default asteroid radius of 0.3. Add a faint drifting dust plume to one look and a few slowly tumbling shards to another. Make all seven available through ordinary mission preparation and record the five additions in the visual catalog with in-game thumbnails.

## User Stories

1. As a player, I want distinct asteroid silhouettes and terrain patterns so that a battlefield with several asteroids has more visual variety.
2. As a player, I want each asteroid to read as rocky even beside similarly sized moons or planets.
3. As a player, I want close views to reveal details such as exposed layers, fracture faces, and frost in crevices.
4. As a player, I want dust and shards to add motion to some asteroids while leaving nearby Darts, routes, and ownership, selection, and capture cues easy to read.
5. As a player, I want to recognize the asteroid as a single node while debris drifts around it.
6. As a mission author, I want the new looks to appear through ordinary mission preparation so that existing mission files can use them.
7. As a visual author, I want to compare each new look with the existing lineup and browse all seven looks with rendered thumbnails and asset links.

## Implementation Decisions

### Asteroid lineup

Use Cinder's muted olive-gray rock and Ferrite's warmer copper-brown rock as baseline comparisons. The names below describe the concepts. Choose final display names and stable IDs before registration.

| Concept | What reads at gameplay scale | What shows up close | Closest existing comparison |
| --- | --- | --- | --- |
| Slate Blade (Kyanite) | A long, narrow slate-blue rock with one pale diagonal stripe | Fractured layers and chipped ends | Cinder shares its subdued palette. The elongated profile and pale stripe distinguish the Blade. |
| Broken Wedge (Flint) | A steep-sided charcoal wedge with a broad chalk-colored break and a few floating shards | Fresh fracture planes, chipped corners, and angular fragments tumbling at different speeds | Cinder shares its dark rock tones. The wedge and bright fracture face carry the look when individual shards are hard to see. |
| Frost Pair (Sleet) | Two joined lobes with a deep notch and a broad pale-cyan frost patch | Granular frost along the neck and exposed dark rock on the outer faces | Cinder shares its uneven outline. The two lobes and pale frost distinguish the Pair. Check it beside a similarly sized moon. |
| Iron Crest (Hematite) | A compact, angular wine-brown rock with a bright metallic ridge | Pitted ore, dull oxide patches, and smaller metallic veins | Ferrite shares its warm metallic tones. The bright ridge and compact outline distinguish the Crest from Ferrite's mottled rock. |
| Ash Mantle (Tephra) | A broad, low dark-plum rock with a pale ash apron and a faint plume on one side | Loose ash around embedded stones and a soft dust plume that drifts slowly near the surface | Cinder shares its dark base. The wide profile and pale apron identify the Mantle even when the dust is faint. |

### Rendering and integration

- Before creating or refining assets, fill in the reusable visual brief for each of the five asteroid candidates. Use the lineup above and the visual catalog to define the gameplay-scale feature, closest comparisons, close-up detail, rendering approach, and planned Sandbox captures. Include the intended extent and motion for the two effects. [Reusable visual brief](visual-variant-workflow-spec.md#reusable-visual-brief)
- Reuse the existing world material and asteroid rendering where they suit the look. Choose procedural shading, textures, or geometry to make the silhouette and major surface feature read at asteroid scale. Keep the solid body within the authored radius and use the battlefield's existing lighting direction.
- Give Ash Mantle a soft, faint dust plume that drifts slowly to one side. Give Broken Wedge a small cluster of angular shards that drift and tumble near the fracture. Keep both effects close to their source throughout their motion. The ash apron and fracture face should identify their respective looks on their own. Use matte rock colors and irregular fragment shapes that stay distinct from faction-colored Darts.
- Keep the effects in presentation and attach them to their asteroid looks. Each asteroid remains one gameplay node with its existing body radius, picking area, indicator positions, and route endpoints. Keep the indicators legible throughout the effects' motion.
- Register the five accepted looks in the Asteroid section of WorldLooks through the existing discovery workflow. Retain `cinder` and `ferrite` and keep all seven equally eligible in the existing shuffle. Mission preparation deals surfaces and world names independently.
- Add the five accepted looks to the Asteroid section of the visual catalog in the same change that registers them. Include each display name, stable ID, gameplay-scale description, rendered thumbnail, and defining asset links. Include the full dust or shard effect in the relevant thumbnail and describe how it moves. Attribute any adapted source imagery and update the rendering description to cover any techniques the new looks introduce.

## Testing Decisions

- Compare all seven looks in Sandbox at radius 0.3 with matching camera, backdrop, lighting, and post-processing. Compare each candidate with both existing looks and the rest of the batch. Accept each when its silhouette and large surface feature give it a recognizable identity at ordinary battlefield scale. Check the lineup on a mixed battlefield with planets and moons, including a similarly sized moon beside Frost Pair.
- Inspect the five looks up close and from representative angles. Look for visible seams, clipping at the edge of the body, and surface artifacts. Inspect both effects through their motion in Play Mode. Dust should drift gradually, and shards should tumble while remaining near the parent asteroid. Check for obvious jumps or clipping as the effects repeat.
- In crowded Sandbox Play Mode framing, check both effect-bearing looks in neutral, owned, selected, and capturing states with labels, nearby routes, and stationed and travelling Darts visible. Check that the indicators remain readable and that the shards read as debris. Confirm that picking an asteroid and issuing orders work as before, including when debris is visible.
- Prepare and play a representative mission with seven asteroids. Confirm that the existing shuffle assigns each asteroid ID once, each ID resolves to the intended look, and the dust and shards appear on their assigned looks in gameplay.
- Compare typical and 95th-percentile CPU and GPU frame times before and after on the same representative asteroid-heavy battlefield, with both effects active in the new lineup. Match hardware, build settings, resolution, and framing. Measure after warm-up over repeated runs and investigate sustained changes beyond run-to-run variation.
- Cross-check the Asteroid section of the catalog with WorldLooks. Verify that each of the seven entries appears once, its thumbnail shows the intended look, and its Unity asset links resolve.

## Out of Scope

- Moon visuals and other visual batches.
- Changes to Cinder or Ferrite, existing mission layouts, asteroid names used in missions, or the saved Sandbox layout.
- Changes to mission format, world size, visual assignment rules, or simulation. Debris interaction, collision, and destruction mechanics belong to separate work.

## Further Notes

Use the existing catalog capture workflow for matched previews and thumbnails. Adapt its framing where needed to fit the asteroid and its full effect. Judge legibility using the gameplay-scale comparison. [Visual catalog capture workflow](../docs/visual-catalog.md#adding-a-look)

### Assumptions still to validate

- Tune silhouette, surface contrast, and feature scale in rendered comparisons at the default asteroid radius and the closest intended view.
- Tune dust density, shard count, drift distance, and motion speed in Sandbox. Use the crowded gameplay view to set how far each effect can extend while staying visually associated with its asteroid.
- Choose the reference hardware and comparison battlefield before collecting performance measurements.
