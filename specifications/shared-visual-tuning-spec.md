# Shared visual tuning and source-defined behavior defaults

## Problem Statement

Generated missions no longer provide an authored battlefield to inspect in Edit Mode. Their appearance also depends on hard-coded renderer values and assets whose properties are overwritten by scene builders. Inspector changes are therefore incomplete or lost on rebuild.

The author needs one durable visual design used by generated missions and a future sandbox, while keeping behavior defaults in source and allowing sandbox-only experiments.

## Solution

Establish shared, Inspector-editable visual assets consumed by the existing mission presentation. Preserve authored assets during normal scene rebuilds. Keep behavior defaults owned by source code; provide an explicit way for a future sandbox to override them without changing normal missions.

This is stage 1 of the sandbox work. It is independently useful for tuning existing generated missions and precedes the [Edit Mode sandbox](visual-sandbox-edit-mode-spec.md). Later stages are [Dart visuals](dart-visuals-spec.md), [game speed](game-speed-settings-spec.md), [playable sandbox](playable-sandbox-spec.md), and [menu integration](sandbox-menu-integration-spec.md).

## User Stories

1. As a visual author, I want editable ring colors, widths, radii offsets, glow and dash properties, so that I can tune ownership, selection and capture appearance.
2. As a visual author, I want editable route, order-preview, course and wake appearance, so that all route states belong to one visual design.
3. As a visual author, I want to edit world surfaces, backdrops and post-processing, so that the complete scene appearance can be tuned.
4. As a visual author, I want changes to shared visuals to reach generated missions and the sandbox, so that I do not maintain duplicate styles.
5. As a visual author, I want scene rebuilds to preserve saved visual edits and references, so that rebuilding does not undo my work.
6. As a developer, I want behavior defaults in source, so that changing the game's normal behavior is a deliberate code change.
7. As a developer, I want sandbox overrides isolated from normal missions, so that experiments cannot become accidental game defaults.
8. As a player, I want the initial migration to preserve existing appearance and behavior, so that the authoring change does not redesign the game.

## Implementation Decisions

- Visuals owns shared visual authoring assets. Match presentation consumes them; a future Sandbox feature may also consume them. This presentation dependency exists to guarantee the same appearance in both contexts. Do not move visual knowledge into plain C# rule assemblies.
- Expose the visual properties currently supplied by WorldView, RingStyle, RouteLanes, FleetLayer and the asset builder. Material inspector changes must not be silently defeated by a duplicate hard-coded property override. Keep one authoritative source for each authored visual property.
- Shared visual design includes materials, meshes, colors, geometric dimensions, ring styles, route styles, surfaces, backdrops and post-processing. Player identity remains separate from its display color. Runtime sandbox color edits are temporary overrides of the shared palette.
- Behavior defaults include production rate, fleet travel speed, game speed, orbit speed and the visible Dart limits. Despite their visual effect, orbit speed and visible counts are behavior settings for this workflow. Do not create an Inspector-editable shared behavior asset. Normal mission defaults stay in source under the subsystem that owns each concept.
- Keep existing authored mission data authoritative. Source defaults do not overwrite explicit starting Darts, types, sizes or other mission-file fields. No mission JSON changes or Unity appearance references in JSON are introduced.
- Provide sandbox override support at the owning boundary, with an explicit distinction between using the source default and supplying an override. An unoverridden value must follow future source-default changes rather than retaining an obsolete serialized copy. The game-speed preference introduced in its own stage is the agreed persistent exception.
- Create missing visual assets with the current code values as initial defaults. Normal rebuilds reuse existing assets and preserve authored properties, object identity and references. Material-pool discovery may update membership without resetting the members' properties. Preserve authored post-processing rather than deleting and recreating its profile.
- Keep lasting structural scene changes in builders. Do not make regeneration necessary to preserve visual property edits. Do not introduce an automatic destructive reset-to-defaults operation.
- Preserve generated mission construction, appearance assignment, camera behavior, production, capture, battle timing, event order, input and audio. Preserve serialized references and compatibility during any extraction.
- The initial public authoring surface is Unity's Inspector. This stage does not require a new custom editor window or a playable sandbox UI.

## Testing Decisions

- Test public outcomes rather than private asset layouts or field names. Prefer existing mission preparation and match tests for rules, and the rendered generated mission plus save/rebuild/reopen for authoring behavior.
- Verify a representative saved ring, route, backdrop material and post-processing edit survives rebuilding, reopening and entering Play Mode. Verify both values and asset references, including user-added material-pool entries.
- Compare the migration's default generated mission against its prior appearance. Check owned, neutral, selected, capturing and contested worlds; routes and fleet transit; camera framing; and missing references.
- Verify an omitted sandbox override resolves the current source default, an explicit override resolves its own value, and neither changes normal mission configuration. Add automated coverage only where executable resolution behavior exists.
- Run the affected Edit Mode tests, Unity compilation and C# whitespace verification when implementing this stage. Exercise the changed builder and review its generated diff; retain only intended changes.

## Out of Scope

The sandbox scene, runtime sandbox controls, a new Dart mesh renderer, persistent game-speed UI, mission format changes, a visual redesign and general-purpose configuration infrastructure.

## Further Notes

### Assumptions still to validate

- The exact grouping of visual assets should follow the current renderer ownership; one global asset containing every unrelated setting is not a requirement.
- Inventory hard-coded and material-driven values before implementation, especially shader animation rates. Classify rates as behavior and shape/color as visual design to avoid two competing sources.
- Shared asset edits made through Unity's Inspector can persist even during Play Mode. The playable sandbox must therefore edit runtime copies, not authoring assets; that safeguard belongs to its stage.

This specification is documentation only. Future implementation must report compilation, automated checks, scenes played and any unverified visual behavior.
