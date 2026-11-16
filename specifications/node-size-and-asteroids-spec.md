# Squadra mission editor node size and asteroids

## Problem Statement

Mission authors can place planets and moons, but cannot set their body radius or judge their physical footprint in the editor. The canvas uses decorative symbol sizes rather than sizes relative to mission distances. Mission JSON also cannot describe asteroids: ordinary fleet nodes that produce no Darts.

These properties must be authored and persisted before later work generates playable missions from JSON.

## Solution

Add a Size property to every mission node and an Asteroid node type. Size is an absolute body radius in game world units. Draw each node at its physical footprint relative to the mission's positions, distance scale, and canvas zoom. Save the radius as the required JSON field `size`.

Planets default to size 1, moons to 0.5, and asteroids to 0.3. All types accept sizes from 0.1 through 5. Asteroids have a distinctive rocky symbol and the same authoring controls as other nodes. Their type establishes a future gameplay requirement of no Dart production; no production setting is added to the editor or JSON.

This milestone covers mission authoring and persistence. Loading JSON into gameplay and implementing playable asteroid behavior remain separate work.

## User Stories

1. As a mission author, I want to edit a selected node's Size, so that I can configure its eventual body radius.
2. As a mission author, I want size expressed in game world units, so that its meaning matches the game's Body Radius property.
3. As a mission author, I want sensible defaults for planets, moons, and asteroids, so that new nodes need minimal configuration.
4. As a mission author, I want changing node type to reset Size to the target type's default even when I customized it, so that every type change starts with a predictable size.
5. As a mission author, I want to customize Size after changing node type, so that I can refine the target type's default for my mission.
6. As a mission author, I want clear validation for sizes outside the supported range, so that incorrect values cannot silently enter a mission.
7. As a mission author, I want node bodies shown at their physical map footprint, so that I can assess spacing and overlaps before mission generation exists.
8. As a mission author, I want zoom and distance scale reflected in the preview without changing stored sizes, so that navigation and layout remain predictable.
9. As a mission author, I want labels, selection, picking, and Fit to Map to account for body size, so that differently sized nodes remain usable.
10. As a mission author, I want overlapping nodes to remain editable and saveable, so that size does not introduce placement restrictions.
11. As a mission author, I want to choose Asteroid as a node type, so that I can author fleet locations that will not produce Darts.
12. As a mission author, I want asteroids to support ownership, starting Darts, and connections, so that I can configure them using the existing node workflow.
13. As a mission author, I want asteroids visually distinct from planets and moons at equal sizes, so that I can identify their type without relying on radius.
14. As a mission author, I want node types and sizes retained through save and reopen, so that the mission file is the source of truth.
15. As a developer of later mission generation, I want explicit size and type data without production fields, so that generation can map size to Body Radius and derive production behavior from type.

## Implementation Decisions

- **Relationship to the original editor specification.** This specification supersedes its restrictions to planet/moon nodes, decorative node sizes, and the exclusion of editable visual sizes. All other mission editor contracts remain in effect unless explicitly changed here.
- **Ownership and boundaries.** The mission model owns node size, supported types, defaults, validation, and JSON representation. The editing layer owns size edits and the type-change rule. Presentation owns inspector controls, projection, drawing, and picking. Keep the mission and editing assemblies independent of Unity and do not introduce a dependency on match presentation to read defaults or validate size. Gameplay scene configuration remains separate from the mission JSON model.
- **Property meaning.** Label the inspector field Size and use the case-sensitive JSON field `size`. It means radius, not diameter or a multiplier. A future generator assigns this value directly to `WorldView.BodyRadius`, subject to the existing float representation of that property. Changing mission distance scale never changes stored size. Clarify the radius unit in the inspector's existing explanatory UI style.
- **Defaults and limits.** Use the following values for every authoring and persistence workflow:

  | Node type | JSON type | Default size | Allowed size |
  | --- | --- | --- | --- |
  | Planet | `planet` | 1 | 0.1–5 inclusive |
  | Moon | `moon` | 0.5 | 0.1–5 inclusive |
  | Asteroid | `asteroid` | 0.3 | 0.1–5 inclusive |

- **Creation and editing.** Preserve the existing Add Node workflow: new nodes are neutral planets with zero Darts, now with size 1. Offer Planet, Moon, and Asteroid in the selected node's type control, and an editable Size alongside the existing node properties. Apply valid size input using the existing numeric-field workflow, redraw immediately, and mark an actual document change dirty. Invalid pending input remains visible, preserves the last valid model value, reports an actionable error, and follows existing save and navigation safeguards. Do not clamp, round to a fixed number of decimal places, or silently substitute a default.
- **Type changes.** Whenever the node type changes, reset Size to the target type's default, including when the previous size was customized. Planet 2 becomes moon 0.5; moon 5 becomes asteroid 0.3; asteroid 0.1 becomes planet 1. The author can customize Size again afterward. Reselecting the current type does not reset Size. Apply type and the size reset as one document edit. Preserve node identity, coordinates, owner, starting Darts, and connections.
- **JSON contract.** Extend the existing unused schema version 1 in place; retain `schemaVersion: 1`. Every node requires a numeric `size`, including nodes at a type's default. Accept only finite values in the inclusive range 0.1–5. Accept `asteroid` as a third type. Reject missing size, wrong JSON types, out-of-range values, unknown node types, duplicate fields, and unknown fields. Defaults apply to authoring, never as a fallback for malformed imported data. There are no previously saved missions requiring compatibility, no legacy-reader path, and no migration. Update relevant examples and test fixtures to the new contract during implementation.
- **Persistence guarantees.** Preserve invariant-culture, round-trip-safe numeric serialization, UTF-8, atomic saves, external-change detection, and failure handling. Preserve size through model cloning, document replacement, save, and reopen. The mission representation must retain accepted values across round trips without introducing renderer-derived rounding.
- **Physical canvas projection.** Compute body radius in map units as size divided by mission distance scale, then multiply by the current pixels-per-map-unit zoom for its canvas radius. At size 1, distance scale 1, and the existing 100% zoom of 40 pixels per map unit, the radius is 40 pixels. Doubling zoom doubles that footprint; doubling distance scale halves it without editing the stored size. Replace the decorative, clamped symbol scaling for body footprints. Equal sizes have equal outer radii regardless of type.
- **Symbols and interaction.** Retain the planet disc and moon crescent identities; add a distinct rocky polygon for an asteroid, with its outer extent governed by size. Preserve ownership colors and selection highlighting. Body culling, selection outlines, label placement, and hit regions follow the displayed footprint. Keep practical screen-space picking padding for tiny projected bodies without enlarging their visible footprint. Include body extents when fitting the map. Navigation, Fit to Map, and label visibility do not change mission data or mark it dirty.
- **Overlaps and distances.** Allow overlapping bodies without moving nodes, rejecting edits, or blocking saves. Size adds no placement rule or new overlap warning; preserve existing coincident-position validation. Retain existing overlap picking precedence; this feature does not add a node list or selection-cycling tool. Preserve center-to-center routes and distance calculations: size does not shorten a route or change its game distance.
- **Asteroid authoring and future behavior.** Asteroids expose the same ownership, starting-Darts, coordinate, size, and connection controls as other nodes. They may be neutral or player-owned. Their future gameplay role supports capture, battles, fleet arrivals, fleet holding, and fleet launches like other nodes, while never producing Darts. Production is derived from type; add no editable or serialized production rate or interval. Recording this contract does not implement runtime asteroid support in this milestone.
- **Scope of game integration.** Do not change the game's existing Body Radius defaults, authored scene radii, simulation node kinds, production logic, victory rules, gameplay captions, or scene builders as part of this authoring feature. No JSON-driven gameplay or scene generation is introduced.

## Testing Decisions

- Test author-visible behavior and persisted contracts through the existing mission document, validation, JSON, and file-workflow boundaries. Extend the established mission editing, format, and file tests rather than testing private controls, painter calls, or duplicating implementation formulas in isolated tests. Use manual scene checks for rendering and input behavior.
- Verify new planets have size 1 and type changes follow all three defaults. Cover every type transition with both a default and customized size; verify Size always resets to the target type's default, while identity, owner, Darts, coordinates, and routes remain intact. Verify Size can be customized again afterward and reselecting the current type preserves it.
- Accept the exact boundaries 0.1 and 5 for every type, along with fractional values. Reject values below and above the bounds, zero, negative values, nonfinite model values, and malformed numeric input. Verify invalid inspector text retains the last valid size and participates in existing save/navigation safeguards without silent clamping.
- Round-trip a mission containing all three types, their defaults, and customized fractional sizes. Verify type, size, and unrelated mission data survive cloning, save, and reopen under different current cultures. Check schema version remains 1 and every serialized node explicitly contains size.
- Reject JSON with missing size, null or string size, out-of-range size, duplicate size fields, unknown fields such as production settings, unsupported types, or unsupported schema versions. Verify failed Open leaves the current document intact. Preserve existing atomic-write and external-change tests through the extended file format.
- In the mission editor scene, compare physical footprints against grid distances at known zoom and scale values. Verify planet and moon sizes 1 and 0.5 show a 2:1 radius ratio; equal-size nodes of different types share an outer radius; asteroid defaults show a radius of 0.3 world units. Zoom and change distance scale, verifying the projected footprint changes correctly while stored size remains unchanged.
- Review the asteroid polygon, moon crescent, and planet disc at default and boundary sizes, varied zoom levels, neutral and player ownership, and selected/unselected states. Verify readable labels, correctly placed selection outlines, culling near canvas edges, picking of tiny bodies, and Fit to Map including large body extents.
- Create partially overlapping bodies and verify each can be selected at an exposed part of its hit region, neither is moved automatically, and saving succeeds. Verify fully obscured nodes retain the existing picking precedence. Resize connected nodes and verify route endpoints and center-to-center distances retain their established meaning.
- Exercise size and asteroid editing in the standalone Windows editor as well as the mission editor scene. Verify valid edits mark the document dirty and visual navigation does not. Check extreme supported scales and zooms for rendering errors without changing stored values.
- This PR is documentation only. Feature tests, Unity compilation, scene playthroughs, and standalone acceptance belong to the implementation change. For this specification, verify agreement with the accepted decisions, Markdown consistency, and that the diff contains documentation only.

## Out of Scope

- Implementing the feature in this specification change.
- Runtime JSON loading, scene/mission generation, immediate playtesting, and procedural generation.
- Playable asteroid simulation or rendering, changes to current gameplay scenes, and changes to game Body Radius defaults.
- Editable or serialized production rates, production intervals, or other simulation balance settings.
- New victory or defeat rules, including deciding whether asteroid ownership contributes to an objective.
- Backward compatibility, legacy-format import, and migration of files without size.
- Collision avoidance, automatic repositioning, overlap restrictions, snapping, or automatic layout.
- New creation tools, multi-selection, undo/redo, and arbitrary new node properties.
- Production-quality gameplay visuals or a gameplay preview inside the mission editor.

## Further Notes

- The accepted product decisions are settled. This document specifies authoring support; JSON containing an asteroid does not make it playable in the current game.
- **Numerical assumption to validate during implementation:** the mission model can retain round-trip-safe size values while the canvas uses Unity rendering precision and a later generator converts to the game's float radius. Default size values must remain stable after save/reopen, especially asteroid size 0.3.
- **Rendering assumption to validate during implementation:** existing supported scales and zooms can project physical body extents safely. Tiny bodies need usable picking and large offscreen bodies need correct clipping/culling without silently clamping their stored or visible physical size. Validate this early with the boundary-size and extreme-scale checks above.
- **Usability assumption to validate during implementation:** the asteroid polygon remains distinguishable at practical zoom levels and existing selection controls suffice for overlapping nodes. Refine symbol detail and screen-space decoration without changing the physical footprint contract.
- **Future integration:** derive zero production from asteroid type and provide suitable runtime presentation for a nonproducing node when playable JSON missions are implemented. Asteroid participation in victory conditions remains for that later specification.
