# Replaceable shared Dart visuals

## Problem Statement

Fleets currently use shader-drawn batched quads. There is no shared Dart mesh authoring surface, and the author needs to change the individual fighter appearance while seeing stationed, travelling and fighting fleets.

## Solution

Support a shared Dart mesh and material, with editable visual size and formation spacing. Use that appearance in the sandbox and generated missions while keeping actual Dart counts independent from configurable visible counts.

This is stage 3, following [shared tuning](shared-visual-tuning-spec.md) and the [Edit Mode sandbox](visual-sandbox-edit-mode-spec.md). Motion and live override controls arrive in the [playable sandbox](playable-sandbox-spec.md).

## User Stories

1. As a visual author, I want to assign a Dart mesh and material, so that I can replace the dot appearance.
2. As a visual author, I want shared Dart size and formation spacing, so that formations remain readable around worlds and along routes.
3. As a visual author, I want changes to appear in both the sandbox and generated missions, so that the preview matches the game.
4. As a developer, I want separate stationed and travelling visible limits, so that I can tune density and rendering cost independently.
5. As a developer, I want orbit speed and visible limits to remain source-defined defaults, so that sandbox experiments do not alter the game.
6. As a player, I want exact fleet counts regardless of visible limits, so that rendering never changes strength or results.
7. As a visual author, I want owner coloring, formation direction and combat feedback to work with the replacement mesh, so that appearance remains coherent during play.

## Implementation Decisions

- Visuals owns the shared mesh/material and visual size/spacing. The fleet presentation consumes them for stationed, travelling and combat Dart representations. Keep formation movement and game rules with their existing owners.
- Provide a default compatible appearance so this stage does not require importing a custom model to run. Assigning a mesh changes presentation, not the number of simulated Darts or the mission format.
- Keep orbit speed and separate stationed/travelling visible limits in source by default. Expose sandbox-only override capability for the later live controls. Do not store those values as shared artist-editable behavior defaults.
- Visible limits cap the number of rendered representatives, not production, launch size, battle strength, capture, results or labels. A representative may stand for multiple Darts; exact numeric counts remain available.
- Preserve faction tinting without mutating the shared material per fleet. A sandbox palette override must reach stationed, travelling and fighting representations for that player.
- Preserve existing arrival formations, combat approach, front alignment, movement, shots, explosions and audio timing except where a chosen mesh's geometry changes appearance. Retain batching or equivalent bounded rendering; do not require one GameObject per simulated Dart.
- Keep normal mission defaults and existing look as close as practical during the renderer change. Any unavoidable default appearance difference must be identified and visually reviewed.
- Keep static preview samples compatible with the real fleet renderer. A separate simplified preview renderer is not sufficient acceptance.
- Invalid mesh/material assignments and invalid count limits need clear validation; do not crash or allocate unbounded geometry. Exact authoring validation UX is an implementation choice.

## Testing Decisions

- Use rendered fleets and public match state as the acceptance seams. Reuse MatchTests, BattleTests and MatchResultsTests for exact counts and combat results rather than checking private draw buffers.
- With equivalent simulation inputs, compare default, low and high visible limits: fleet strength, casualties, production and outcomes must match while visible density changes.
- Assign a visibly different mesh/material, save, rebuild and reopen. Inspect sandbox samples and a generated mission for the same shape, size, spacing and faction coloring.
- In Play Mode, inspect stationed orbit, launches, transit, arrival, capture, combat and survivor return. Verify orientation, overlap, route visibility and consistent color.
- Check large Dart counts for bounded representatives and practical rendering cost. Select concrete stress counts during implementation based on existing mission limits; this specification does not invent a performance benchmark.
- Run affected Edit Mode tests, compilation and formatting checks. Report manual rendering coverage separately from automated rule coverage.

## Out of Scope

Model creation/import pipelines, skeletal animation, faction-specific ship classes, changed combat rules, one rendered object per real Dart, runtime asset browsers and a general formation editor.

## Further Notes

### Assumptions still to validate

- Validate mesh orientation, material/shader compatibility and instancing support against the project's camera and render pipeline before choosing the draw implementation.
- Determine how the existing battle representative cap relates to the new stationed limit. Do not introduce an unrequested third independent count control without a concrete need.
- Pixel-identical results cannot be assumed for arbitrary replacement geometry; the default migration still requires visual comparison.
