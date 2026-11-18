# Stage 3 of 6: Replaceable shared Dart visuals

## Problem Statement

Fleets currently use shader-drawn batched quads. Stages 1 and 2 provide a shared FleetStyle for dot size, brightness, transit spread/trails and battle-front appearance, plus static sandbox samples, but no replaceable Dart mesh. The author needs to change the individual fighter appearance while seeing stationed, travelling and fighting fleets.

## Solution

Support a shared Dart mesh and material, with editable visual size and formation spacing. Use that appearance in the sandbox and generated missions while keeping actual Dart counts independent from configurable visible counts.

This is stage 3, following [shared tuning](sandbox-stage-1-shared-visual-tuning-spec.md) and the [Edit Mode sandbox](sandbox-stage-2-visual-sandbox-edit-mode-spec.md). Stages 1 and 2 are implemented and merged; this stage extends their existing assets and preview scene. Motion in the sandbox and live override controls arrive in the [playable sandbox](https://github.com/MolnarOndrej/Squadra/blob/5d459bb5fc1b20c024d7a46cbf4ff6a6be2e4c03/specifications/sandbox-stage-5-playable-sandbox-spec.md).

## User Stories

1. As a visual author, I want to assign a Dart mesh and material, so that I can replace the dot appearance.
2. As a visual author, I want shared Dart size and formation spacing, so that formations remain readable around worlds and along routes.
3. As a visual author, I want changes to appear in both the sandbox and generated missions, so that the preview matches the game.
4. As a developer, I want separate stationed and travelling visible limits, so that I can tune density and rendering cost independently.
5. As a developer, I want orbit speed and visible limits to remain source-defined defaults, so that sandbox experiments do not alter the game.
6. As a player, I want exact fleet counts regardless of visible limits, so that rendering never changes strength or results.
7. As a visual author, I want owner coloring, formation direction and combat feedback to work with the replacement mesh, so that appearance remains coherent during play.

## Implementation Decisions

- Extend Visuals' existing FleetStyle authoring boundary for the shared Dart mesh/material and visual size/spacing, rather than introducing a competing fleet style. FleetLayer and SandboxDarts consume the same appearance through their shared formation/swarm presentation. Keep formation movement and game rules with their existing owners.
- Reuse the existing FleetStyle.asset and preserve authored size, brightness, spread, trails and battle-front values and references during migration. Move remaining formation spacing out of DartFormations' hard-coded geometry into shared visual tuning, updating DartSwarm and both mission and sandbox callers together. Preserve serialized compatibility and update scene-builder wiring where references change.
- Provide a default compatible appearance so this stage does not require importing a custom model to run. Assigning a mesh changes presentation, not the number of simulated Darts or the mission format.
- Extend the existing DartDisplay and SandboxBehavior boundaries for separate stationed/travelling visible limits. Currently DartDisplay supplies an orbit speed, a one-dot-per-Dart threshold and a single representative cap used by stationed, travelling and both combat fronts. Preserve the existing count-reduction behavior and source defaults when splitting the cap; an untouched mission must retain its current visible density. Reuse SandboxOverride's explicit default/override distinction, preserve saved sandbox experiments and leave unticked overrides following current source defaults. Do not store behavior defaults in FleetStyle. Static Inspector overrides work in this stage; live controls remain deferred.
- Visible limits cap the number of rendered representatives, not production, launch size, battle strength, capture, results or labels. A representative may stand for multiple Darts; exact numeric counts remain available.
- Preserve faction tinting without mutating the shared material per fleet. A sandbox palette override must reach stationed, travelling and fighting representations for that player.
- Preserve existing arrival formations, combat approach, front alignment, movement, shots, explosions and audio timing except where a chosen mesh's geometry changes appearance. Retain batching or equivalent bounded rendering; do not require one GameObject per simulated Dart.
- Keep normal mission defaults and existing look as close as practical during the renderer change. Any unavoidable default appearance difference must be identified and visually reviewed.
- Extend the existing SandboxPreview/SandboxDarts samples with the same Dart drawing path used by missions; sharing only geometry while retaining a separate dot-only preview is insufficient. Refresh mesh/material/size/spacing edits in Edit Mode without starting simulation, orbit motion, shots or audio. Preserve transient preview-object cleanup on scene/script reload and keep authored worlds, fleets, routes and behavior overrides through sandbox rebuilds.
- DotBatch currently also draws trails and DartEffects' shots, explosions and sparks. Replacing a Dart's body must preserve those effects and their existing style relationships; it must not inadvertently turn every effect or trail dot into the assigned ship mesh.
- Invalid mesh/material assignments and invalid count limits need clear validation; do not crash or allocate unbounded geometry. Exact authoring validation UX is an implementation choice.

## Testing Decisions

- Use rendered fleets and public match state as the acceptance seams. Reuse MatchTests, BattleTests and MatchResultsTests for exact counts and combat results rather than checking private draw buffers.
- With equivalent simulation inputs, compare default, low and high visible limits: fleet strength, casualties, production and outcomes must match while visible density changes.
- Start with the merged FleetStyle asset and a saved sandbox layout/override experiment. Assign a visibly different mesh/material and edit size/spacing in the Inspector; verify immediate static Edit Mode refresh. Save, run the affected mission/sandbox builders and reopen. Inspect sandbox samples and a generated mission for the same shape, size, spacing and faction coloring, preserved authored values/references and preserved sandbox samples/overrides.
- Reload scripts and repeatedly switch the mesh/material and preview states. Verify no accumulating transient meshes/objects, unwanted asset writes, simulation progress or preview sounds. Check the default body migration leaves trails, shots, explosions and sparks coherent.
- In Play Mode, inspect stationed orbit, launches, transit, arrival, capture, combat and survivor return. Verify orientation, overlap, route visibility and consistent color.
- Check large Dart counts for bounded representatives and practical rendering cost. Select concrete stress counts during implementation based on existing mission limits; this specification does not invent a performance benchmark.
- Run affected Edit Mode tests, compilation and formatting checks. Report manual rendering coverage separately from automated rule coverage.

## Out of Scope

Model creation/import pipelines, skeletal animation, faction-specific ship classes, changed combat rules, one rendered object per real Dart, runtime asset browsers and a general formation editor.

## Further Notes

### Assumptions still to validate

- Validate mesh orientation, material/shader compatibility and instancing support against the project's camera and render pipeline before choosing the draw implementation.
- Both combat fronts currently use DartDisplay's same cap as stationed and travelling fleets. Confirm how the new two-limit policy applies as travelling representatives form up for combat and return to orbit, preserving continuous handoff and exact simulated counts. Do not introduce an unrequested third independent count control.
- Pixel-identical results cannot be assumed for arbitrary replacement geometry; the default migration still requires visual comparison.
