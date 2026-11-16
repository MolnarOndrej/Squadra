# Squadra JSON-driven mission scene generation

## Problem Statement

Mission authors can create complete mission definitions in the standalone Mission Editor, but the game still launches a separately authored Unity scene. Positions, owners, starting Darts and other starting configuration must therefore be maintained twice. The first authored file, `mission-01.json`, cannot yet supply the battlefield for Mission 1.

The existing game also assumes that every world is connected to every other world and that Blue wins by owning all planets. Those assumptions do not implement the editor's explicit routes or the intended elimination objective covering planets, moons, asteroids and remaining enemy forces.

## Solution

Make bundled Mission Editor JSON the source of truth for every campaign mission's authored map and starting state. Validate and prepare all explicitly assigned missions at application startup, before the main menu becomes usable. When a player selects a playable mission, populate one reusable gameplay scene from its prepared definition and start a fresh match.

Use `mission-01.json`, named First Contact, for Mission 1 in place of the authored SampleScene battlefield. Preserve campaign order and leave Missions 2–5 as previews until JSON files are assigned. Keep the main menu as the application's entry flow.

During startup preparation, independently assign each world a material and a name from pools specific to its type. Prefer variety within each mission and retain the assignments throughout the application session, including restarts and returns to the menu. Keep the assignment shareable so future multiplayer participants can see the same named worlds and appearances.

Apply one shared elimination objective: the human wins when no enemy-owned world and no enemy Darts remain. Neutral worlds and neutral guards are optional. Defeat applies the same elimination rule to the human. Preserve pause, results, restart, visual style and audio behavior except for the explicitly defined changes to routes, camera framing, asteroid support and match outcomes.

This follows [Mission Editor](mission-editor-spec.md) and [node size and asteroids](node-size-and-asteroids-spec.md). It implements the runtime consumption those specifications deferred. It supersedes the first playable scenario's all-pairs connectivity and planet-only victory rule; other capture, production and battle contracts remain in effect.

## User Stories

1. As a mission author, I want the game to consume my Mission Editor JSON, so that I maintain one authored mission definition.
2. As a mission author, I want positions, sizes, types, ownership and starting Darts reproduced in gameplay, so that the battlefield matches my design.
3. As a mission author, I want only my listed connections to permit travel, so that map topology affects play.
4. As a mission author, I want distance scale to affect game distances consistently, so that editor measurements predict fleet travel.
5. As a mission author, I want asteroids to hold fleets and be captured without producing Darts, so that they serve their intended role.
6. As a campaign designer, I want to assign bundled JSON files to an ordered catalog, so that adding a map does not require authoring another gameplay scene.
7. As a campaign designer, I want unassigned entries to remain previews, so that future missions can remain visible without being playable.
8. As a player, I want startup to prepare assigned missions before I use the menu, so that mission readiness is already known when I choose one.
9. As a player, I want only the selected battlefield created on entry, so that unused missions do not run or retain live battlefield objects.
10. As a player, I want every world framed when a mission begins, so that authored coordinates do not put worlds outside the camera.
11. As a player, I want capture of enemy planets, moons and asteroids to matter to victory, so that an enemy's remaining territory cannot be ignored.
12. As a player, I want remaining enemy fleets and combatants to prevent victory, so that a surviving enemy force can continue the match.
13. As a player, I want neutral territory to be optional, so that victory does not require clearing unrelated worlds or guards.
14. As a player, I want defeat only when I have no worlds or Darts left, so that travelling or capturing forces can keep me in the match.
15. As a player, I want Restart to restore the selected mission's original state, so that a previous attempt does not leak into the next one.
16. As a player, I want pause, results, audio and campaign navigation to retain their behavior, so that JSON-driven missions feel like the same game.
17. As a player, I want an invalid assigned mission disabled with a useful explanation, so that one bad mission does not prevent using the menu.
18. As a developer, I want mission preparation separate from scene construction and simulation, so that the same authored definitions can eventually serve multiplayer without implementing networking now.
19. As a content author, I want dedicated planet, moon and asteroid material folders, so that new world surfaces can be added without unrelated effects entering selection pools.
20. As a content author, I want one name dictionary per world type, so that names can grow independently of material variants.
21. As a player, I want names and materials selected without repetition while unused choices remain, so that a mission has varied worlds.
22. As a player, I want the same named worlds and materials after Restart or a return from the menu, so that the map remains recognizable during my session.
23. As a player, I want ownership rings and auras to follow the owner's color independently of the surface, so that random appearance does not obscure ownership.
24. As a future multiplayer participant, I want each world to have the same name and appearance for every participant, so that we can identify the same places.

## Implementation Decisions

### Mission sources and campaign identity

- Bundle explicitly assigned JSON assets with the game. Loading must work in an installed build without the source checkout, Unity Editor APIs or access to the author's absolute filesystem path. Do not discover missions by scanning a directory or sorting filenames.
- Keep the campaign catalog as the owner of order, stable campaign identity and explicit mission assignments. Replace per-mission gameplay scene availability with prepared mission availability; all playable entries use the shared gameplay scene.
- Mission 1 references the existing First Contact definition. Preserve its three nodes, their authored positions and radii, two starting fleets of 30 Darts, neutral moon and three connections. The JSON takes precedence over the old scene's starting values.
- Leave Missions 2–5 unassigned. Adding a future playable mission means assigning its bundled definition to the appropriate catalog entry, subject to preparation and ordinary campaign unlock rules.
- Require each assigned catalog ID, JSON mission ID and filename stem to match exactly, including case. Mission 1 uses `mission-01` and `mission-01.json`. Preserve existing stable campaign IDs; renaming a mission's display name does not change its identity. Report mismatches during preparation rather than silently renaming or repairing content.
- Keep explicit asset assignments and catalog ordering; matching names do not introduce filename-based discovery or ordering. Restart and completion use the selected mission identity rather than the shared scene path.
- Mission definitions retain their authored mission names, and the campaign map retains its existing Mission N labels. Generated world display names come from the type-specific dictionaries below. Stable node IDs remain separate from display names and are used for references, diagnostics and assignment lookup.

### Startup preparation and mission lifecycle

- Before the menu accepts input, read, parse, validate and prepare every assigned definition, including missions currently locked by campaign progression. Startup preparation does not create live worlds, fleets or matches and does not advance mission time, production or combat.
- Reuse the strict JSON reader and mission validator. Preserve required fields, schema version, supported values and rejection of malformed, duplicate, missing or unknown fields. Add runtime playability checks separately; do not narrow what the Mission Editor may author or save.
- Retain prepared data, world material/name assignments and per-entry readiness or failure information for the application session. Gameplay must not mutate the prepared starting definition. Returning to the menu does not reset campaign progress, reroll world assignments or reprepare unchanged bundled content.
- On mission entry, load the shared gameplay scene, construct all required views and rule configuration, bind them, frame the camera and only then enable match advancement and player orders. Construction must not expose a partly initialized match.
- Restart creates fresh mutable match and presentation state from the same prepared definition: starting ownership and Darts, time, battles, captures, fleets, selection, result state and combat counters reset. Campaign completion already earned during the session remains intact.
- Next Mission uses the next catalog entry and its preparation status. Back to Menu and pause-menu Quit to Menu retain their existing destinations and confirmation behavior. Loading the shared scene must not lose the active mission identity.
- Remove the campaign's dependence on SampleScene as an authored mission. Generate any lasting shared scene wiring through the existing scene-builder workflow. No generated mission-specific `.unity` files are written at startup or selection time.

### Ownership and architecture

- The mission model continues to own the file contract and authored definition. It remains independent of Unity, scene components, campaign progress and mutable simulation state.
- Match-side preparation translates validated definitions into a starting configuration, stable node-ID mappings, explicit connections and rendering requirements. Scene construction owns Unity objects, assets and bindings. The simulation owns order legality, travel, production, capture, battles and elimination outcomes.
- Campaign owns assignment, ordering, readiness and active campaign identity. Menu, results and pause presentation coordinate navigation through that identity rather than using scene path as mission identity.
- Runtime loading introduces a named dependency from match integration to the mission-definition contract, and campaign readiness consumes the preparation outcome. Keep these boundaries explicit; presentation dependencies must not make simulation depend on the mission editor UI or Unity.
- Refactor the existing controller's setup and objective responsibilities as needed without folding file parsing, scene construction and elimination policy into one coordinator. Rule assemblies remain plain C#, acyclic and testable without scenes. Pass real collaborators directly; introduce no service locator or DI framework.
- Visuals owns the material pools, name dictionaries and appearance lookup. Mission preparation retains the resolved per-node assignment alongside the starting definition, while scene construction applies it. Do not put Unity materials into the JSON model or simulation; generated names are display data, never rule identities.

### Map conversion, connections and production

- Convert map coordinates to game world coordinates using mission distance scale. Positive X is right and positive Y is up. Center-to-center game distance is Euclidean map distance multiplied by distance scale; travel time uses that distance and the existing fleet speed.
- Size is an absolute body radius in game world units and is assigned without multiplication by distance scale. Changing scale changes spacing and travel distance, not stored radii. Camera fitting must not change positions, sizes or travel times.
- Preserve stable string node and player identities in the prepared definition. Internal integer node indices are permitted with an explicit mapping; resolve edges and ownership by IDs rather than assuming node order or names.
- Permit fleet launches only along an explicitly listed direct edge. Every edge is bidirectional. Do not infer connections from proximity or add automatic routing through intermediate worlds. Reject an order to an unconnected target without launching a fleet or changing stationed Darts.
- Draw idle lanes and valid order previews only for permitted connections. Preserve existing visual treatment of lanes and fleets. Visual endpoints near orbits do not shorten the center-to-center distance used by the rules.
- Planets produce one Dart per second and moons one Dart per two seconds under the existing production conditions. Asteroids never produce Darts, but support ownership, guards, fleet holding, arrivals, departures, battles and capture like other worlds. Give asteroids a recognizable rocky appearance within the game's existing visual style and do not show a positive production countdown.
- Preserve fleet speed, capture duration, battle timing, casualty accounting and the established event order within one instant: production, capture completion, arrivals in launch order, then battle casualties. One large time step must remain equivalent to many small ones.
- Do not add production rates, battle settings, objective fields, human roles or Unity asset references to JSON. The existing mission schema remains unchanged.

### Supported players and elimination

- This release supports exactly `player1` as the Blue human and `player2` as Red. Player identity and color remain separate concepts in the model and conversion boundary; this is an explicit runtime restriction, not a new meaning for every blue player in the file format.
- Preserve current opponent behavior: Red does not gain autonomous fleet orders. Additional player configurations, multi-party battles, AI and multiplayer are deferred. Report unsupported rosters during preparation rather than failing when a battle starts.
- Use one shared elimination condition for all campaign missions. No objective authoring or per-mission objective configuration is introduced.
- A side remains in the match while it owns any planet, moon or asteroid, including an empty one, or has any Darts stationed, travelling, fighting or capturing. Track Darts by their actual side during battles and captures, independently of the world's formal owner.
- Blue wins only when Red owns no world of any type and has no remaining Darts in any of those states. Blue loses when the same elimination condition holds for Blue. Neutral ownership and neutral guards do not prevent victory and are not an enemy player.
- Elimination means losing ownership and forces through existing capture and battle mechanics. Do not destroy world objects or introduce a new attack against the body itself. An enemy-owned empty asteroid still prevents victory until captured.
- Evaluate outcomes at the existing resolved-event boundary, after all events at the deciding instant. Freeze the match and capture results once, including the deciding casualties and elapsed time. Do not award victory between simultaneous events or merely because an enemy fleet has reached a world and left the transit list.
- Preserve the results roster, human-first order, Darts destroyed/lost accounting and campaign completion behavior. Mission 1 is not the final catalog mission while Missions 2–5 remain previews.

### Camera and preserved presentation

- Derive camera content bounds from all generated worlds, including body extents and usable margins for orbits and labels. Center the view on that content and fit it at the current aspect ratio, including window resizing.
- Keep all worlds in view without moving the authored map. There is no gameplay pan, zoom, automatic layout or minimum on-screen world-size guarantee in this release.
- Preserve the existing planet and moon style, fleet rendering, effects, selection, capture feedback, HUD behavior, pause behavior, result overlays and music/sound transitions. Generate the same functional bindings the authored scene supplied. Visual variation comes from shared presentation assets, not new mission JSON properties.

### World materials, names and session assignments

- Dedicate separate Planets, Moons and Asteroids folders to selectable world surface materials under the Visuals feature. Keep ring, aura/glow, fleet, line, background and other unrelated materials outside these pools. Retain the existing Thalassa and Ember planet materials and Lune moon material in their appropriate pools; provide at least one recognizable asteroid material during implementation.
- Each selectable material variant has a stable identity and material reference. Its asset label may remain Thalassa, Ember or Lune, but that label does not prescribe the generated world's name. Future model choices, if introduced, must also have stable identities in the assignment.
- Provide one bundled, editable name dictionary file for each type: planets, moons and asteroids. Seed each dictionary with a few nonempty, distinct names during implementation. Dictionaries are separate from material pools and the mission JSON; a planet using the Thalassa material can have any planet name. Adding names does not require adding materials, and adding materials does not require adding names.
- During startup, assign a material and name independently to every node of each prepared mission, using only that node type's pools. Shuffle each pool and exhaust its choices before repeating. If demand exceeds a pool's size, continue in shuffled cycles so choice counts differ by at most one within that mission and type. Apply this rule independently to names and materials: one pool running out does not cause premature repetition in the other.
- Uniqueness is scoped to a mission, not the entire campaign. Repeated names or materials are allowed after their respective pool is exhausted; node IDs remain unique and unambiguous. Do not pair a dictionary name permanently with a material or tie either choice to owner, color, starting Darts or node size.
- Resolve and retain all assignments before the main menu becomes usable, including for assigned missions locked by campaign progress. Restart, returning to the menu, replaying the mission and capture must preserve the same node-to-name and node-to-material mapping. A new application launch may produce different assignments; there is no cross-session persistence requirement or guarantee that every new launch differs.
- Rings and auras show the current owner's player color, including after capture; neutral worlds retain neutral styling. Preserve existing capture feedback and color transitions. Ownership does not replace, tint to faction color or rerandomize the chosen surface material. Per-world rendering properties must not mutate shared material assets or alter another world's appearance.
- Expose a resolved presentation assignment keyed by stable mission and node IDs, recording the material variant ID and selected name, with room for a future model identity. Scene construction consumes the assignment rather than rolling random choices of its own.
- Future multiplayer must supply the same authoritative resolved assignment to every participant. Independent application-startup randomness on separate machines is insufficient, even when mission JSON matches. Use stable variant IDs rather than asset instance IDs, folder enumeration order or display names to identify appearance content; unknown variants must be detected rather than silently substituted. This release establishes that data boundary without implementing networking, a lobby, asset synchronization or multiplayer transport.
- A mission requiring a type with an empty material pool or no usable names fails preparation with an actionable explanation. Do not borrow from another type or silently use unrelated materials. Bundle pools and dictionaries so installed builds can prepare missions without Unity Editor asset discovery. More content can be added in future builds without hard-coding additional material or name choices into the generator.

### Failure handling and readiness

- Distinguish an unassigned preview from an assigned definition that failed preparation. Both are unplayable; the latter exposes an actionable explanation identifying the mission and relevant field or object when available.
- Missing or unreadable assigned content, JSON errors and unsupported gameplay configurations must disable that entry without preventing the rest of the menu from working. Do not silently repair a definition, drop unsupported data or substitute the old battlefield.
- File validity and runtime readiness are separate. The editor intentionally accepts empty graphs, zero-player missions and other unfinished layouts. A runtime loader must refuse configurations it cannot initialize and run safely rather than weakening the editor's authoring contract.
- Preserve existing campaign gating. Preparation success does not unlock a progression-locked mission, and failure does not grant completion or skip an unavailable next entry. Next Mission remains disabled with an appropriate explanation when the next entry cannot be played.
- Keep editor warnings available for diagnostics. Disconnected graphs, coincident positions and zero-length edges are not JSON errors. Do not silently connect, reposition or remove them; validate their runtime handling as described below.

## Testing Decisions

- Test observable behavior at existing mission JSON/validation, match simulation and campaign-rule boundaries. Prefer preparation-to-match behavior tests over tests of private conversion methods or object-creation call sequences. Use manual gameplay checks for construction, visuals, input and bindings.
- Prepare the actual First Contact definition and verify node identity, type, coordinates, radii, ownership, starting Darts, player mapping and all three bidirectional connections. Verify it runs without reading the source checkout in a built game.
- Verify catalog ID, JSON ID and filename stem agree for Mission 1. Reject a mismatch without silently rewriting content, and verify changing a display name does not change campaign identity or order.
- Verify each world type selects only from its own material pool and name dictionary, with no background or effects material eligible. Verify names and materials vary independently, including different names for the same material.
- With controlled randomness, verify pools larger than, equal to and smaller than node demand: choices do not repeat before exhaustion and repeated cycles keep usage balanced. Cover single-choice pools, independent pool sizes and multiple missions so uniqueness is scoped correctly. Assert these invariants rather than one incidental random shuffle.
- Verify assigned names and variant IDs remain identical after Restart, menu return, replay and capture, while fresh mutable gameplay state resets. A new session may reroll; do not write a probabilistic test requiring every new run to differ.
- Apply one resolved assignment to two independently constructed battlefield instances and verify matching mission/node IDs show the same names and material variants regardless of local random state or pool enumeration order. Detect unavailable variant IDs. This verifies the sharing boundary without a networking test.
- Verify empty required pools or dictionaries disable only affected missions with useful explanations. Check dictionaries and all pool assets are included in a built game. During visual checks, verify ownership rings and auras track player colors after capture while surfaces retain their selected appearance and shared materials remain unchanged.
- Use a sparse graph to verify listed routes work in both directions, unlisted targets reject orders without state changes, and connected intermediate nodes do not imply a direct route. Verify lane and preview rendering agrees with rule legality.
- Verify known coordinate distances and a non-unit scale produce the expected travel time, while radii remain unchanged. Include fractional and negative coordinates and different node array orders to detect accidental order-based references.
- Verify planets and moons retain their production timing, asteroids never produce, and asteroid guards, ownership, capture, battles and fleet launches behave like other nodes. Retain the simulation's event-order and large-step/small-step equivalence coverage.
- Exercise elimination with an enemy's last moon or asteroid, an empty owned world, a fleet in transit, stationed Darts, a capturing force and both battle sides. None may be ignored. Verify neutral worlds and guards do not block victory, and mirror the remaining-presence cases for human defeat.
- Verify capture completion and battle casualties at the deciding instant produce one final result with accurate time and totals. Cover enemy worlds without planets and remaining forces after the last owned world is captured.
- Verify startup prepares all assigned entries without starting matches; one malformed or unsupported entry does not block other menu actions. Verify unassigned previews, assigned failures, locked valid entries and playable entries remain distinct in behavior and explanation.
- Verify selection and Next Mission carry the intended catalog identity through the shared scene. Restart must reproduce starting state without old fleets, captures, counters, elapsed time or selection. Repeated entry and return must not duplicate worlds, listeners, effects or audio.
- Verify campaign order, session-only progress, completion, preview behavior and final-mission results remain consistent with existing campaign tests. Specifically preserve Mission 1's distinction from campaign completion.
- Play the main menu and generated Mission 1 flow, including victory, defeat, Restart, pause/resume, Quit to Menu, results navigation and disabled Next Mission. Check UI focus, music continuity, sound effects, missing scripts and required serialized bindings. Check the Mission Editor still opens and saves compatible files.
- Visually inspect camera framing at wide and narrow supported window sizes using First Contact and maps offset from the origin with varied body radii. Check all world bodies, orbits and labels remain visible without changing game distances. Review the new asteroid appearance and nonproduction caption.
- Run Edit Mode tests, Unity compilation and the project whitespace check during implementation. Exercise the changed scene builder and inspect its output; restore unrelated generated changes. Include a built-game smoke test to prove bundled asset availability.
- This change includes the specification and the authored Mission 1 JSON with its Unity metadata. Review the specification against the accepted decisions, validate the JSON syntax and matching identity, and verify only these intended files are included. Runtime tests, compilation and scene playthroughs belong to implementation; new pools and dictionaries are described here rather than implemented in this change.

## Out of Scope

- Implementing the feature or filing issues as part of this specification change.
- Creating Mission 2–5 definitions or replacing their preview status without assignments.
- Runtime directory discovery, externally editable installed-game missions, downloads, hot reload or an in-game mission browser.
- Generating and saving a Unity scene for each mission, or keeping every battlefield alive at startup.
- Embedding the Mission Editor in the game or adding a playtest button to it.
- Multiplayer, networking, synchronization, additional playable rosters, multi-party battles and new AI behavior.
- Configurable objectives, alliances, alternative victory modes or objective fields in JSON.
- Gameplay pan and zoom, automatic layout, pathfinding and multi-hop fleet orders.
- New mission-authored production, balance, music, material, display-name or other presentation settings.
- Persistent campaign saves, unrelated refactors, dependency upgrades and changes to settings behavior.

## Further Notes

- **Future multiplayer use:** authored mission definitions, resolved world presentation assignments and prepared starting data must remain separate from local controls and live scene state. Participants must use one agreed assignment for the same mission; networking and distribution remain deferred. This boundary does not make the current simulation or presentation multiplayer-ready.
- **Numerical assumption to validate:** the editor accepts a much wider coordinate/scale range than the current Unity presentation was designed around. Confirm safe conversion, distance computation and camera fitting, including near-coincident and extreme values. If a definition cannot be represented safely, report a runtime readiness failure without clamping or modifying authored values. Establish and document any necessary runtime limits during implementation.
- **Warning-layout assumption to validate:** disconnected maps may be unwinnable, and coincident worlds or zero-length routes may stress picking and fleet arrival behavior. Preserve authoring validity and existing warnings. Verify safe runtime handling; do not introduce topology repair or claim every valid authoring file is winnable.
- **Initial-state assumption to validate:** campaign missions are expected to begin with both supported sides present. Confirm the runtime readiness policy for initially eliminated sides, empty maps and simultaneous elimination before encoding their outcome precedence. The shared objective must not accidentally award an arbitrary result to an unfinished editor document.
- **Direct-scene development assumption to validate:** the campaign flow always supplies an explicit selected mission. Decide the shared scene's developer-only behavior when played without that context; it must not silently choose another campaign entry or accidentally credit progress. Preserve standalone result behavior if direct-scene play is retained.
- **Presentation assumption to validate:** shared assets can reproduce the current style for arbitrary authored maps and provide a recognizable asteroid without per-mission art configuration. Extremely large maps may fit mathematically while becoming difficult to read; pan, zoom and broader navigation remain future work.
