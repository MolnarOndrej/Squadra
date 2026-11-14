# Squadra Mission Editor

## Problem Statement

Mission authors need to create and change Squadra maps without editing configuration files or using the Unity development environment. They need to see worlds and their permitted fleet routes, place worlds precisely, configure their starting state, and exchange the complete mission as one file.

Current missions are authored as Unity scenes. The existing simulation uses fixed faction identities and integer node indices, and the presentation connects every pair of worlds. These are constraints of the current game, not a suitable file contract for a reusable graphical mission editor.

## Solution

Provide a Windows desktop Mission Editor built from the Squadra Unity project. It opens directly into a schematic graph editor and runs independently of the game. Authors can create, inspect, connect, move, configure, delete, validate, save, and reopen mission definitions.

One versioned JSON file is the permanent source of truth for each mission. The same runtime-compatible data model, validation, editing logic, and graphical editor must support future embedding in the game. This feature creates mission definitions; consuming them in gameplay is separate work.

## User Stories

1. As a mission author, I want to launch a standalone Windows editor without Unity installed, so that I can design missions outside the development environment.
2. As a mission author, I want to run the editor alongside Squadra, so that I can work without closing the game.
3. As a mission author, I want New Mission to provide an empty graph and useful defaults, so that I can start authoring immediately.
4. As a mission author, I want to edit the mission ID, name, and distance scale, so that the file identifies the mission and defines its distance units.
5. As a mission author, I want to add planet and moon nodes to a simple graph, so that I can design topology without gameplay rendering obscuring it.
6. As a mission author, I want each node to have a stable identifier, so that moving or reconfiguring it does not break route references.
7. As a mission author, I want to select and drag nodes, so that I can arrange the map visually.
8. As a mission author, I want to enter exact X/Y coordinates, so that I can position nodes precisely.
9. As a mission author, I want to change a node's type, owner, and starting Darts in an inspector, so that I can configure its initial state.
10. As a mission author, I want property edits reflected immediately on the graph, so that I can see their effect.
11. As a mission author, I want to connect two nodes visually, so that I can define an explicit bidirectional travel route.
12. As a mission author, I want duplicate and self-connections prevented, so that editing does not introduce invalid topology.
13. As a mission author, I want to inspect and delete routes, so that I can refine connectivity.
14. As a mission author, I want deleting a node to remove its incident routes, so that no dangling references remain.
15. As a mission author, I want pan, zoom, and Fit to Map, so that I can navigate a map larger than the visible canvas.
16. As a mission author, I want readable node IDs, type symbols, ownership colors, and selection highlighting, so that I can understand the schematic graph.
17. As a mission author, I want to inspect route distances and optionally show them on the canvas, so that I can judge spacing.
18. As a mission author, I want moving a node or changing the distance scale to update distances immediately, so that displayed values remain accurate.
19. As a mission author, I want to configure up to six players with stable IDs and distinct preset colors, so that ownership is explicit and visually distinguishable.
20. As a mission author, I want a player color change to update all its nodes, so that I do not have to edit each node separately.
21. As a mission author, I want confirmation before removing a player and neutralizing its nodes, so that I understand the effect of deletion.
22. As a mission author, I want incomplete and disconnected layouts to remain saveable when structurally valid, so that I can work incrementally.
23. As a mission author, I want actionable validation messages, so that I can locate and correct invalid data.
24. As a mission author, I want Save and Save As to write one complete JSON file, so that I can store and transfer a mission without companion assets.
25. As a mission author, I want reopening a file to preserve positions, IDs, ownership, Darts, settings, and routes, so that editing round trips do not change my design.
26. As a mission author, I want failed or cancelled file operations to preserve my current work, so that an unsuccessful operation does not lose changes.
27. As a mission author, I want Save, Discard, and Cancel before leaving unsaved work, so that I control whether changes are lost.
28. As a mission author, I want protection against overwriting external file changes, so that another application cannot silently lose its edits.
29. As a mission author, I want incompatible or invalid files rejected clearly, so that opening them cannot silently discard mission properties.
30. As a developer, I want the editor's data and editing logic independent of its Windows shell, so that the same editor can later run inside Squadra.

## Implementation Decisions

### Delivery and architecture

- Deliver a separate Windows desktop executable, conventionally named `SquadraMissionEditor.exe`, from the existing Unity project. It starts in the editor scene and does not require Unity Editor or a running game. Other desktop platforms are deferred.
- Keep the game build's startup and scene selection independent of the editor build. Editor-only build configuration or build tooling is permitted; code needed by the running editor must not depend on Unity Editor namespaces or APIs.
- Separate mission definitions, JSON serialization, validation, document editing state, graphical canvas, property inspector, and application/file integration. Mission definitions and validation must be usable without a graphical interface or a scene.
- Use runtime UI Toolkit for the toolbar, panels, and schematic canvas, following the project's existing UXML/USS and shared control conventions where useful. Do not introduce a graph framework requiring Unity Editor. Never render gameplay planets, space backgrounds, or gameplay effects in the editor.
- Share one editor scene and editing implementation between the standalone shell and eventual in-game integration. File selection and application exit belong behind application integration boundaries; editing operations must not directly require Windows dialogs or process termination.
- Introduce a mission-definition model rather than serializing live simulation state or Unity scene objects. Generic player IDs and stable string node IDs must not inherit the simulation's faction identity or array-index constraints. Reuse compatible concepts such as planet/moon vocabulary and Euclidean distance without changing current gameplay.
- JSON must reconstruct the entire mission without a generated Unity scene, diagram, external graph, asset reference, or extra configuration file. A temporary file used for safe saving is an implementation detail, not a companion mission artifact.

### Mission defaults and identity

- New Mission creates an empty node list and edge list, name `Untitled Mission`, a generated editable mission ID, distance scale `1`, and two players: `player1` with blue and `player2` with red.
- Generate a nonempty collision-resistant mission ID using permitted characters. Mission ID uniqueness is local to the document's identity; checking a global library or campaign catalog is not required.
- IDs are nonempty, case-sensitive strings consisting only of ASCII letters, digits, underscores, and hyphens. Node IDs are unique within nodes; player IDs are unique within players. Different ID scopes may use the same string.
- New nodes are neutral planets with zero starting Darts. Generate read-only IDs in the `N1`, `N2`, and subsequent numeric sequence. Choose an unused ID, keep every surviving ID unchanged, and do not renumber after deletion, movement, type changes, or other property edits. Preserve valid imported IDs even when they do not follow the generated naming convention.
- Player IDs are supplied at creation and read-only afterward. Mission IDs remain editable. Renaming a mission ID does not rename its file automatically.

### Mission JSON contract

- Use UTF-8, human-readable JSON with integer `schemaVersion` equal to `1`. Property names and enum strings are case-sensitive.
- All fields below are required. Reject unknown fields at every object level, missing fields, wrong types, invalid enum values, and unsupported versions. Do not silently ignore unsupported information or supply defaults for malformed imported documents.

| Object | Fields and meaning |
| --- | --- |
| Mission root | `schemaVersion`, `id`, `name`, `map`, `players`, `nodes`, `edges` |
| `map` | `distanceScale`: finite positive number converting map distance to game distance |
| Player | `id`: player identity; `color`: one of `blue`, `red`, `green`, `yellow`, `purple`, `orange` |
| Node | `id`; `type`: `planet` or `moon`; `position`; `owner`: a player ID or JSON null; `darts`: nonnegative integer |
| `position` | `x` and `y`: finite numerical map coordinates |
| Edge | `source` and `target`: existing node IDs identifying an unordered pair |

- `id` and `name` at the root are strings. `players`, `nodes`, and `edges` are arrays. A name is a human-readable label; no uniqueness requirement applies to names.
- Neutral is represented only by null ownership, never a player entry or a color enum member. Do not duplicate player colors in nodes.
- Edges contain connectivity only. Do not persist node coordinates inside edges, calculated distances, fleet travel times, or selection and viewport state.
- Preserve mission values, IDs, player ordering, and graph topology through save/open. JSON whitespace and object-property ordering do not need to survive a round trip. Serialize numerical values without display rounding so repeated saves do not move nodes.
- Future schema changes must be explicit. Adding colors or mission properties later must account for older editors rejecting unknown values; version 1 does not promise forward-compatible editing.

### Players and ownership

- Support zero through six players, each with a unique ID and a unique color from the six-color enum. No color may be shared by two players. More players and palette expansion are future work.
- Color is presentation configuration, separate from player identity. A blue player does not imply the game's current Blue faction, human control, or any AI behavior.
- Provide player creation, color editing, and deletion in the property area. Disable Add Player when six players exist. Offer unused colors when adding or changing a player, retaining the selected player's current color as an option.
- Updating a color immediately updates every node owned by that player. Draw neutral nodes gray.
- Before deleting a player, show a confirmation naming the player and reporting the affected node count. Confirmation removes the player and assigns all its nodes to neutral while preserving their other properties and routes. Cancellation changes nothing. The released color becomes available again.
- Permit players with no owned nodes, all-neutral missions, zero-player missions, and empty graphs. Authoring validity does not imply readiness for gameplay. Require no minimum roster or starting ownership configuration.

### Canvas and property editing

- Provide a toolbar containing New, Open, Save, Save As, Add Node, Add Connection, Delete Selected, zoom controls, Fit to Map, and a route-distance visibility toggle. Make validation messages and mission-level/player settings accessible alongside the selected-object inspector.
- Support single-object selection. Selecting a node shows its read-only ID and editable type, owner, starting Darts, X, and Y. Selecting an edge shows both endpoint IDs and calculated distance as read-only properties. Mission settings remain accessible without a selected object.
- Draw distinct simple symbols for planets and moons, IDs, ownership colors, and visible selection highlighting. Draw routes as straight lines between node centers. Keep labels legible during navigation.
- Implementation interaction defaults: Add Node arms placement by the next canvas click; Add Connection selects a first and then a second node. Escape cancels a pending tool or connection. Clicking a node selects it; dragging moves it. Provide pointer pan and wheel zoom without triggering placement or movement accidentally. Inspector and dialog interaction must not affect the canvas beneath them.
- Prevent self-connections, duplicates in either endpoint order, and dangling endpoints at editing time. Deleting an edge removes only that edge. Deleting a node removes the node and all incident edges in one editing operation.
- Positions use map coordinates independent of screen pixels, zoom, and panel layout. Positive X points right and positive Y points up. Allow negative and fractional values. Pan, zoom, Fit to Map, and label visibility must not change mission data or mark it dirty.
- Fit to Map frames all nodes with a usable margin; an empty graph restores a usable default view. Do not add snapping, multi-selection, or automatic graph layout in this release.
- Apply valid property changes to the document and graph immediately. Keep malformed or out-of-range field input visibly invalid and do not silently coerce it into another value. Saving or leaving must not silently discard a pending invalid field edit; require correction or an explicit discard decision.
- Node properties are limited to those in the JSON contract. Do not add editable world names, production rates, visual sizes, objectives, or other simulation properties.

### Routes and distances

- All routes are bidirectional. `source` and `target` are endpoint labels, not travel direction. Travel in a future consumer is allowed only along explicitly listed routes; physical proximity never implies connectivity.
- Calculate map distance as the square root of the sum of squared X and Y differences. Multiply by mission `distanceScale` to obtain game distance. Pan and zoom do not enter this calculation.
- Recalculate affected distances when coordinates change, and all distances when scale changes. The edge inspector always shows calculated distance. The canvas toggle controls optional distance labels only.
- Display rounding is permitted for labels; calculations and serialization must use the underlying values. Distance here is not travel duration; speed and movement integration are outside this feature.
- Permit overlapping nodes and zero-length routes between distinct coincident nodes. Report them as warnings, not errors. Self-connections remain errors even when all coordinates are valid.

### Validation and compatibility

- Use the same mission validator for candidate documents on Open, current document validation, and pre-save checks. Report the issue, affected field or object ID when available, and a useful explanation. Canvas editing must prevent invalid structural operations where possible, while imported JSON must receive equivalent validation.
- Errors include invalid or duplicate IDs within their scopes; unsupported node types or colors; nonfinite coordinates; negative, fractional, or unrepresentable Dart counts; missing owner references; more than six players; repeated player colors; dangling edges; unordered duplicate edges; self-connections; and nonfinite or nonpositive scale. Darts use the existing game's nonnegative 32-bit integer range.
- Reject syntactically malformed JSON, duplicate object property names, incompatible schema versions, unknown fields, missing required fields, and incorrect JSON types. Report errors rather than partially loading or repairing a file.
- Analyze connected components, counting isolated nodes as components. More than one component is a warning. An empty graph is valid without a disconnected-graph warning. Coincident nodes and zero-length routes also produce warnings. Warnings must be visible and must not block saving.
- Open builds and validates a candidate before replacing the active document. Any error rejects the candidate and leaves the active document, path, pending edits, and dirty state intact. A valid candidate with warnings may open and must show its warnings.
- Validation checks mission definitions only. Do not validate human/AI roles, victory conditions, current campaign membership, current scene availability, or whether the existing game can play the file.

### Files, unsaved changes, and failure handling

- Use Windows local file dialogs for Open and Save As with JSON filtering. Save writes to the current path; without one, it invokes Save As. Successful Save As adopts the new path. Cancelling a file dialog leaves the active document and path unchanged.
- Track actual mission edits as unsaved changes, including player changes, node movement, and route edits. A newly created mission has no file and must be treated as unsaved work. A successful Open or Save establishes a clean document baseline. No-op property assignments and viewport changes do not dirty it.
- Before New, Open, or Quit can replace or discard unsaved work, offer Save, Discard, and Cancel. Save continues the requested action only after a successful save. A cancelled dialog, failed validation, or write failure cancels that continuation. Cancel retains all work. Choosing Discard permits the requested action, but the current document remains available until New or a valid Open actually succeeds.
- Apply the same quit protection to the standalone window-close action as to an editor Quit command. Do not claim recovery from an operating-system kill or power loss.
- Validate before writing. Write a complete candidate to a temporary file in the destination directory, then atomically replace the destination, using platform support appropriate to existing or new files. If validation, serialization, permissions, disk space, or replacement fails, show an actionable error, retain dirty work, and leave the previous destination intact. Never fall back to truncating the destination in place.
- Record the file baseline after Open or successful Save and detect content changes or deletion at that path before overwriting it. An external modification requires an explicit choice: Overwrite, Save As, or Cancel. The overwrite choice authorizes replacing the changed file; Save As preserves it. Repeated checks must not silently overwrite a newly changed baseline.
- Save As must confirm replacement of an existing destination through the file dialog or an equivalent explicit confirmation. Cancellation preserves the active file path and unsaved changes.
- Atomic replacement and baseline checks do not constitute multi-process locking. Concurrent editing of the same file and collaborative merging are not supported.

## Testing Decisions

- Test observable author workflows and file contracts, not canvas object hierarchies, internal flags, private counters, or serializer formatting. Use the document/application workflow as the primary automated boundary: issue public editing commands, inspect resulting mission data and validation results, and open/save through controllable file integration.
- Follow the project's existing Unity-independent rule tests and EditMode conventions. Exercise the new document workflow, validator, and serializer together where practical. Use a replaceable file integration boundary to produce cancellation, changed baselines, and I/O failures reliably. This avoids building tests around private implementation details.
- Supplement automated workflow tests with runtime UI interaction checks and Windows standalone acceptance checks. Native dialogs, actual atomic replacement, window-close interception, and independence from Unity Editor require verification in the built executable. Tests of the pure workflow alone do not establish those guarantees.
- Verify new-mission defaults, unnamed-file save behavior, node defaults, generated IDs, and preservation of imported IDs. Delete a middle node and confirm surviving IDs do not change; add nodes after reopening and confirm no collisions.
- Create planets and moons, drag and enter fractional/negative coordinates, change owners and Darts, connect and disconnect nodes, delete a connected node, and round-trip the resulting file. Assert complete value preservation and identical topology after repeated saves, including coordinates that would change if rounded for display.
- Verify case-sensitive ID behavior and scope-specific uniqueness. Test invalid characters, empty IDs, duplicate node/player IDs, unknown owners, unsupported enums, fractional or negative Darts, the Dart range boundary, nonfinite or unrepresentable values, and invalid scales. Check actionable diagnostics and unchanged active state after rejected Open.
- Verify reversed duplicate edges and self-connections are prevented in the UI and rejected on import. Check isolated nodes, disconnected components, coincident nodes, and zero-length edges produce the specified warnings and still save. Verify empty, zero-player, and all-neutral missions remain valid.
- Add all six players, check each available color is unique, and verify Add Player becomes unavailable at six. Reject a seventh player or repeated colors on import. Change a color and verify all owned nodes update. Cancel player deletion, then confirm it and verify only ownership changes on affected nodes and the color becomes reusable.
- Check Euclidean distance with a 3-by-4 coordinate difference yielding map distance 5, then apply a nonunit scale. Move either endpoint and change scale; verify both inspector and optional labels update. Pan/zoom and toggle labels; verify coordinates, computed values, and dirty state do not change.
- Exercise selection, property changes, tool cancellation, placement, connections, deletion, pan, zoom, and Fit to Map in runtime UI. Verify planet/moon symbols, IDs, colors, neutral gray, and selection are readable. Verify panel and modal interactions never create, drag, or delete canvas objects accidentally.
- Reject malformed JSON, duplicate property names, missing fields, unknown fields at nested levels, wrong types, unsupported schema versions, and future color strings. Confirm no partial document replacement or silent data loss. Verify valid warning-bearing imports open and display warnings.
- Exercise New, Open, Quit, and window close with dirty work, testing Save, Discard, and Cancel separately. Cancel or fail a Save As; verify the pending action does not continue. Choose Discard then open an invalid file; verify the original document is retained. Check invalid pending inspector input cannot disappear through a save or navigation action without explicit discard.
- Inject serialization, permission, write, and replacement failures and verify the old file remains complete, current work remains dirty, and retry succeeds. Verify successful Save As changes the active path only after success and that overwriting another destination requires confirmation.
- Modify or delete the current file externally, then exercise Overwrite, Save As, and Cancel. Verify there is no silent overwrite, alternate saving preserves the external file, cancellation retains work, and later saves use the updated baseline.
- Build and launch the Windows editor with Unity Editor closed, run Squadra at the same time, and complete a create/save/reopen workflow through native dialogs. Verify the editor opens directly, the game still starts normally, the editor requires no companion mission files, and runtime code has no Unity Editor dependency.
- This change is specification-only. Implement and run the feature's tests with the future implementation; document checks now verify consistency, completeness, and agreement with the accepted scope.

## Out of Scope

- Implementing the game's JSON mission loader, mapping generic players to simulation factions, changing integer runtime node IDs, instantiating gameplay scenes, or adding explicit-route enforcement to fleet movement.
- Integrating the editor into Custom Game, changing the campaign catalog, adding human/AI assignments, or expanding playable factions, combat rules, objectives, and victory conditions.
- Rendering gameplay planets, backgrounds, effects, or a gameplay preview in the editor.
- Instant playtesting, procedural generation, templates, presets, undo/redo, multi-selection, snapping, automatic layout, and advanced route properties.
- Custom colors, palette editing, more than six players, and arbitrary additional node or gameplay properties in schema version 1. These remain possible future extensions with explicit compatibility decisions.
- Online sharing, collaborative editing, file merging, multi-process file locking, autosave, and crash-recovery guarantees.
- macOS, Linux, mobile, web, and Unity Editor-only graphical authoring tools.

## Further Notes

- Acceptance requires an independently launchable Windows editor, complete graph/property editing, correct ownership and distance behavior, one-file round trips without position drift, actionable validation, safe failure handling, and a reusable runtime implementation. Producing mission JSON does not make a mission playable in the current game.
- **Assumptions to validate during implementation:** native Windows dialog integration can be added without an unnecessary framework; the target filesystem supports atomic replacement; runtime UI Toolkit remains usable for the schematic canvas at representative map sizes. Verify these with the earliest standalone vertical slice.
- **Numerical implementation assumption:** finite round-trip-safe double-precision mission coordinates and scale are suitable for the editor data model. Canvas projection may use Unity's rendering precision, but it must not rewrite stored positions during navigation. Confirm supported numeric bounds and reject values that cannot be represented or calculated safely rather than clamping them.
- **Usability assumptions to validate:** a resizable desktop window and ordinary mouse/keyboard interaction are suitable; precise pan gestures, zoom limits, symbol sizes, and a representative large-map performance budget need practical verification. These do not add advanced authoring features or change the schema.
- Generic player identity and the six-color constraint are deliberate separate concepts. Supporting additional colors or player counts later must not require rewriting ownership references or the shared graphical editing implementation.
