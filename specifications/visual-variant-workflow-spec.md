# Visual variant workflow

## Problem Statement

Squadra will gain new visuals in small batches over time. The author needs to know which looks already exist and whether a candidate adds recognizable variety on the battlefield. Material settings can suggest a palette and surface treatment, but the shader, textures, mesh, lighting and gameplay scale determine what the player sees.

## Solution

Keep a single wiki-style visual catalog that the author can browse outside the game. Group entries by type and show each look's name, short description, and rendered thumbnail. Each visual task targets one type and may deliver one or several variants of that type. Use the catalog to plan the batch, then compare the rendered candidates with their closest existing looks and with each other at gameplay scale. The same workflow applies to types that Squadra has yet to introduce.

## User Stories

1. As the author, I want to request a single look or a batch for one object type, so that I can expand the game's visual variety incrementally.
2. As the author, I want to know what makes each existing look recognizable, so that I can brief new work without opening every material in Unity.
3. As the author, I want to see why each candidate differs from its closest existing look and the other candidates, so that a batch adds variety even when the objects appear together.
4. As the author, I want the workflow to handle an object type with no existing assets or preview, so that I can establish its first look and compare later additions against it.
5. As a player, I want variants to remain readable at gameplay scale and retain the cues that tell me what the object is and who owns it.
6. As the author, I want accepted variants recorded with their asset references and preview evidence, so that the next task can build on them.
7. As the author, I want to browse all current looks on one page without opening Unity or starting the game, so that I can see which visual resources are available.

## Implementation Decisions

- Start with a Markdown catalog in the repository, linked from the README. Give planets, moons, asteroids, scene backgrounds, and Darts their own sections. Add a section when a task introduces a new type.
- Use one entry per look. Include its display name, stable ID where available, short visual description, small rendered thumbnail, and links to the Unity assets that define it. The initial inventory comes from WorldLooks and FleetStyle. Related textures and meshes belong with the look they define.
- Capture thumbnails from the game renderer, using the Sandbox where it supports the look. Frame bodies and Darts large enough to recognize on the page and use a representative scene crop for a backdrop. Keep the gameplay-scale comparison as the acceptance check for new variants.
- Start each task with the object type, requested count, intended use, and delivery scope. The delivery scope says whether the task supplies previewable assets or also makes them available in gameplay. Record any visual direction the author supplies.
- Check the current Unity assets before drafting concepts. For each relevant look, record its name or stable ID, asset references, silhouette, dominant palette, and one or two features visible at gameplay scale. Include motion or lighting behavior where it affects recognition. Treat descriptions inferred from assets as provisional until reviewed in a rendered preview.
- Give each candidate a recognizable visual feature and identify its closest existing look. Use cues appropriate to the object type, such as broad banding or a prominent surface feature on a globe, an elongated profile on an asteroid, or a pointed silhouette on a Dart. Judge color differences by how they appear in the rendered comparison.
- Reuse the existing rendering and authoring paths where they fit. Planets, moons and asteroids use typed surface materials and the WorldLooks catalog. Darts use a shared mesh and material through FleetStyle. Several Dart candidates can be delivered and compared as assets using temporary assignments and matched captures. Simultaneous Dart looks in gameplay would require a separate integration decision.
- For a new object type, use the intended on-screen role and scale to establish its first visual reference. Compare it with nearby objects that a player could confuse with it. Reuse the Sandbox if it can represent the type. Otherwise use an appropriate temporary preview and record which rendering, catalog, or gameplay support would be needed for integration, if requested.
- Add or update the catalog entries in the same change that adds the accepted looks to the game. The catalog lists only looks that have been added, without a status; work in progress and candidates stay out of it. Keep scene or style changes made only for previews temporary unless the task calls for saving them.

### Reusable visual brief

Fill in the relevant fields for each candidate. Pelagos and Virelia are references for the level of detail a player can see up close. Pick a separate reference for the shape, materials, and natural processes that suit the proposed look.

> Create or refine **[name and stable ID, if integrated]**, a **[planet, moon, asteroid, backdrop, Dart, or other type]** for **[scene and gameplay role]**. Deliver **[preview assets or a look integrated into the game]**. Preserve **[identity, palette, silhouette, motion, or other features that define an existing look]**.
>
> At **[normal gameplay size or frame]**, it should read as **[silhouette or composition, dominant palette, and one or two large features]**. Compare it with **[closest existing looks and neighboring gameplay elements]**. Up close, show **[specific fine features]** at several scales, with variation in shape, spacing and contrast. Use **[observational or art references]** for how those features form. If adapting source imagery, record its origin and attribution in the catalog.
>
> Set **[atmosphere, weather, lighting, shadows and motion]** to match the subject and scene. For worlds, specify whether they have an atmosphere and what processes shape the visible surface. Airless bodies can show impacts, ejecta, fractures, exposed strata or mineral deposits. Atmospheric bodies can have clear skies, haze or active weather according to their conditions. For a backdrop, specify the focal area, quiet areas behind gameplay, and behavior during camera motion.
>
> Deliver assets in the existing renderer's format at **[target display scale and texture or geometry budget]**. Specify projection or UV layout, color and data channels, and the import settings that preserve the intended detail. For a close-view mapped globe, start with a seamless 2:1 unlit surface map at 4096 × 2048 with mipmaps. Keep cloud coverage and relief available to the shader as data where the look needs them.
>
> Show matched renderer captures at gameplay scale and the closest intended view. Inspect any rotation, lighting, occlusion, seams, poles, camera crops or scrolling that matter for this type. Confirm that gameplay cues remain legible under the usual lighting and post-processing. Record the assets, sources, and verified behavior in the catalog when the look ships.

## Testing Decisions

- Open the catalog from the README. Check that each current look appears once in the expected section, with a readable thumbnail and working asset links. Cross-check the entries against WorldLooks and the assigned Dart assets.
- Use the Sandbox Game view for supported types. Compare the closest existing look and all candidates at representative gameplay sizes with the same camera, backdrop and rendering settings. When an existing style allows only one assignment, use matched captures for the comparison. Use close-ups to inspect surface or mesh detail.
- Accept a variant when its distinguishing feature is visible at ordinary gameplay scale. Revise candidates that look interchangeable in that view. For a new type, check that its visual cues distinguish it from objects players will see alongside it.
- For world surfaces, check that the body stays readable next to its owner ring, labels and routes. For Dart bodies, check faction tint, facing and readability in stationed, travelling and fighting fleets. For backgrounds, compare the same battlefield framing across the candidate backdrops and check that worlds, routes, labels and fleets remain readable. Inspect motion in Play Mode when it contributes to the design.
- If integration is in scope, verify in the relevant gameplay scene that the intended asset can be selected or appears through the existing assignment path. Report which looks and scenes were checked and which checks remain open. Use rendered comparisons for visual acceptance.

## Out of Scope

- Creating assets or choosing a first batch as part of this specification.
- Introducing new object rules, mission schema changes, or runtime variant selection as part of the workflow.
- Adding preview tooling or changing the default assets, existing missions, or Sandbox layout as a standing requirement.
- Shipping the catalog with the game or adding an in-game catalog screen.

## Further Notes

The initial reference below comes from repository assets. Confirm the gameplay-scale descriptions in Unity when a task first uses them as comparison targets.

| Type | Look | Asset-based visual description |
| --- | --- | --- |
| Planet | Thalassa (`thalassa`) | Earthlike globe with blue oceans, green-brown land, pale clouds, a blue atmospheric rim and warm night lights. |
| Planet | Ember (`ember`) | Rust-brown volcanic globe with glowing orange fractures, dusty clouds and a warm atmospheric rim. |
| Moon | Lune (`lune`) | Round, cool-gray moon with craters in its surface map and a faint blue-gray rim. |
| Asteroid | Cinder (`cinder`) | Irregular, cratered rock in muted gray-brown. |
| Asteroid | Ferrite (`ferrite`) | More irregular, cratered rock in warmer copper-brown. Compare with Cinder to see how much of that difference survives at asteroid scale. |
| Scene background | Space (`space`) | Dark starfield with blue, violet, and teal nebula colors and a warm sun glow. |
| Dart | Dart (shared FleetStyle) | Small round dot with a soft glow in the owner's color. |

### Assumptions still to validate

- Choose representative gameplay sizes and preview conditions per task, especially for a type with no established camera framing or renderer.
- Confirm the integration path for any new type before promising that its assets will appear in a mission.
