# Stage 2 of 6: Edit Mode visual sandbox

## Problem Statement

The reusable mission scene has an inactive world template and creates its battlefield at runtime. The author cannot inspect a representative scene while editing rings, worlds, fleets, routes and the backdrop.

## Solution

Add a saved Sandbox scene with stable, representative content visible without entering Play Mode. Use the same renderers and shared visual assets as generated missions. Arrange three owned planets at the corners of a triangle and a neutral moon in its center, with additional preview states available without advancing a match.

This is stage 2. It depends on [shared visual tuning](sandbox-stage-1-shared-visual-tuning-spec.md), works with the existing Dart renderer, and supplies the scene extended by the later [playable sandbox](sandbox-stage-5-playable-sandbox-spec.md).

## User Stories

1. As a visual author, I want to open a populated scene in Edit Mode, so that I can tune appearance without starting a mission.
2. As a visual author, I want three player-owned planets and a central neutral moon, so that I can compare ownership colors and neutral presentation.
3. As a visual author, I want representative ring, capture, selection and battle states, so that I can inspect more than an idle neutral world.
4. As a visual author, I want stationed and travelling fleet samples, so that fleet changes can be judged in context.
5. As a visual author, I want idle routes, order previews and transit visuals, so that I can tune every route state.
6. As a visual author, I want a visible backdrop and normal scene rendering, so that lighting and contrast match the game.
7. As a visual author, I want saved layout and preview-state changes, so that reopening restores my working setup.
8. As a developer, I want previews to reuse production presentation, so that the sandbox does not become a second renderer to maintain.

## Implementation Decisions

- Add a Sandbox feature with its own scene and editor tooling. Visuals continues to own shared looks; Match presentation owns the existing world, fleet and route rendering. Reuse or narrowly extract that presentation for the named need of editor previews, preserving behavior for normal missions.
- The canonical starting setup is three planets, one per player, at triangle corners and one neutral moon in the center. Use existing source defaults and shared looks. Keep the layout stable across reopening rather than selecting a random mission on each load.
- The scene is a curated test setup, not a campaign mission and not a new mission JSON format. Opening it must not depend on campaign startup, mission availability or completion.
- Make worlds, rings, sample fleets, routes and the backdrop visible in Edit Mode. Preview samples include neutral/owned/selected states, capture progress, battle presentation, different world sizes, and representative fleet counts. Asteroid appearance must be inspectable through a type preview even though the starting setup contains no asteroid.
- Keep the four-node starting layout readable. Alternate visual states may be selected on the samples rather than requiring a second battlefield or simultaneous impossible states on one node. Preview-only state must be identified as such and must not become a live battle or capture on later Play Mode entry.
- Save layout, selected world appearances and static preview configuration with the scene. Save shared visual edits in shared assets. Keep sandbox-only behavior overrides explicitly distinguished from inherited source defaults.
- Use deterministic static samples for fleet positions, route progress, capture progress and selection state. Edit Mode does not run production, resolve arrivals, advance battles, emit repeated audio or modify campaign/settings persistence.
- Refresh previews after relevant Inspector edits, scene load and script reload. Do not create accumulating transient objects, mark unrelated scenes dirty or modify shared assets just by opening the sandbox.
- Continuous motion preview, timeline scrubbing and gameplay controls are deferred. This stage's acceptance is static Edit Mode visibility; the later playable stage supplies motion testing.
- Any builder creating the scene must preserve the intended authoring workflow. Ordinary rebuilding must not silently erase saved sandbox layout or overrides. Keep scene structure support in the builder and avoid rewriting authored state on every rebuild.

## Testing Decisions

- Use opening, editing, saving and reopening the Sandbox scene as the primary acceptance seam. Do not test exact hierarchy names or incidental object counts as a substitute for visible results.
- Inspect world types/sizes and every representative ring, route and fleet state in Scene view and the configured Game view while still in Edit Mode. Compare equivalent samples with a generated mission using the same visual settings.
- Change shared ring, route and backdrop values and confirm the preview updates without Play Mode. Reopen the scene and verify saved state, references and visuals remain.
- Switch preview states repeatedly and reload scripts; verify no duplicated samples, persistent simulation progress, repeated sounds or unintended authoring-asset writes.
- Verify the changed builder preserves authored configuration. Compile and run applicable Edit Mode/convention tests; manually check the existing mission still renders and plays normally.
- Do not create tests that merely duplicate static setup constants. This stage does not require adopting Play Mode tests or changing the project's Edit Mode-only test policy.

## Out of Scope

Live simulation, player orders, runtime node editing, main menu access, loading arbitrary missions, custom model import tooling, moving-node runtime tools and animated Edit Mode playback.

## Further Notes

### Assumptions still to validate

- Use triangle edges and a route from each corner to the central moon as the initial useful route arrangement. Exact positions, world sizes and preview counts are authoring choices to check for readability rather than fixed gameplay requirements.
- The source-defined starting Dart defaults must be identified during implementation; no new balance values are agreed here.
- Verify that existing ExecuteAlways rendering, batching and shader time allow a stable static preview. Any required change must preserve generated mission rendering.

This specification adds an authoring surface, not another source of game rules.
