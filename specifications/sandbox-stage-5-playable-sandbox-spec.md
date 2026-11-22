# Stage 5 of 6: Playable endless sandbox and testing controls

## Problem Statement

The Sandbox shows a static battlefield in Edit Mode. An author can pose a fleet in transit, a capture or a battle, but must recreate that situation to see it play out. Static previews also cannot demonstrate orbit behavior or production. Normal missions also restrict control to one player and end on victory or defeat, interrupting experiments. The author needs a controllable setup that remains usable indefinitely and cannot save accidental Play Mode changes.

## Solution

Extend the saved Sandbox scene into an endless, no-AI, three-player testing environment. Start the simulation from the conditions shown in Edit Mode so authored fleets, captures and battles continue as soon as the scene enters Play Mode. Let the user order every player's fleets and edit selected nodes through an on-screen panel. Provide temporary behavior tuning, player colors, pause, reset, replenishment and repeatable capture/battle examples.

This is stage 5. It depends on [shared tuning](sandbox-stage-1-shared-visual-tuning-spec.md), the [Edit Mode scene](sandbox-stage-2-visual-sandbox-edit-mode-spec.md), [Dart visuals](sandbox-stage-3-dart-visuals-spec.md), and [game speed settings](sandbox-stage-4-game-speed-settings-spec.md). Stages 1 through 4 are implemented on `master`. Direct scene entry is sufficient for this stage. The future main-menu entry starts the same saved scene with the same authored starting conditions. [Menu integration is Stage 6](https://github.com/MolnarOndrej/Squadra/blob/856491e140b9a6aef713ab5c3f5e7bc81b163e32/specifications/sandbox-stage-6-sandbox-menu-integration-spec.md).

## User Stories

1. As a sandbox user, I want to start with three owned planets around a neutral moon, so that I can test several factions immediately.
2. As a sandbox user, I want no victory, defeat or AI interference, so that I control how long and how the experiment runs.
3. As a sandbox user, I want to order fleets from any player's node, so that I do not need to switch an active player.
4. As a sandbox user, I want to inspect neutral, capturing and contested nodes, so that editing is not limited to nodes that can launch fleets.
5. As a sandbox user, I want to set current stationed Darts, ownership, type and production state, so that I can create useful situations quickly.
6. As a sandbox user, I want to change any player's display color, so that all their nodes and fleets can be checked against the backdrop.
7. As a sandbox user, I want production, travel, orbit and visible-count overrides, so that I can test behavior without editing source defaults.
8. As a sandbox user, I want a four-choice on-screen game-speed selector initialized from Settings, so that I can adjust this run's pace temporarily.
9. As a sandbox user, I want pause to freeze simulation and animation while editing remains usable, so that I can inspect and adjust a moment.
10. As a sandbox user, I want reset and replenishment controls, so that an exhausted or conquered setup does not end testing.
11. As a sandbox user, I want repeatable capture and battle examples, so that I can inspect transitions without manually preparing every attempt.
12. As a developer, I want supported scenario commands callable without UI automation, so that deterministic sandbox behavior can be tested.
13. As an author, I want scene edits saved in Edit Mode and runtime edits discarded, so that experimentation never overwrites the starting setup or shared assets.
14. As an author, I want to press Play and see the fleets, captures and battles I set up continue from the moment shown in Edit Mode, so that I can test a situation without recreating it.
15. As a sandbox user, I want Reset Setup to restore the authored moment, including activity already underway, so that I can replay the same situation.
16. As a sandbox user, I want the future main-menu entry to start the scene as authored, so that I can run the configured situation without opening Unity's Edit Mode.
17. As an author, I want an invalid fleet entry to be identified while the rest of the scene starts, so that I can fix the entry and still test the valid setup.

## Implementation Decisions

### Ownership and lifetime

- Sandbox owns its starting setup, orchestration, temporary overrides and controls. Match/Simulation continues to own production, movement, capture and battle rules. Visuals owns shared authoring assets. Sandbox's dependencies on Match presentation and Visuals are justified by using the production renderer and rules.
- Use the same canonical triangle scene: three owned planets, one per player, and a neutral center moon. Apply source behavior defaults unless a supported scene-saved sandbox override exists. Use the saved game-speed preference at every run start; do not serialize a sandbox game-speed override.
- Disable AI and match completion only for sandbox sessions. Production, arrivals, capture and combat continue after any player loses all territory and Darts, including after one player owns everything. Do not change normal mission completion or campaign behavior.
- Resolve runtime configuration into copies. UI changes must not write scene assets, shared visual assets, source defaults, mission files or saved settings. The Settings screen remains an explicit persistent action; the sandbox speed selector is not one.
- Keep visual authoring shared, without a separate sandbox ring/route/Dart style. Runtime player-color changes are temporary palette overrides and never change faction identities or shared materials.

### Starting conditions authored in Edit Mode

- Use one authored starting setup for the frozen Edit Mode preview and the live Sandbox session. Stage 5 supersedes Stage 2's preview-only treatment of configured fleets, captures and battles. Start valid configured activity automatically when entering the scene, including through the future main-menu entry. Edit Mode keeps the setup frozen.
- Start each authored travelling fleet at its configured progress along its route, with its configured faction and Dart count, and let the simulation handle arrival. The fleet is already in transit, so its Darts are separate from the source world's authored stationed count. Skip invalid fleet entries such as missing worlds or disconnected routes, and report which entry failed and why. Start the remaining valid setup.
- During an authored capture, the world's stationed Darts belong to the captor and the world retains its authored owner until capture completes. Resume from the configured capture progress. With zero capturing Darts, clear the capture immediately and retain the authored owner.
- Start an authored battle with the configured attackers, stationed Darts and front direction. Begin the normal casualty schedule from those counts. If the stationed side is a capturing force, the normal battle rules hold its capture progress during combat. With zero attacking Darts, omit the battle and continue with the stationed side. With attackers present and zero stationed Darts, apply the normal landing rules immediately. The attackers begin a fresh capture if they do not already own the world.
- Clear the Edit Mode selection, hover and order previews on entry. Live pointer input can produce a hover highlight immediately.
- Reset Setup restores the authored battlefield, including fleets in transit and capture and battle state, and clears selection and battlefield changes made during the run. Starting a new run also restores the authored battlefield. Use the normal simulation event order as the run advances.

### Selection, orders and editing

- No active-player switch is required. For an order, use the selected source's owner as issuer. Neutral nodes cannot launch player orders; existing connection, stationed-force, capture and battle restrictions still apply.
- Allow selecting every node for inspection/editing, including neutral and contested nodes. Distinguish inspecting a target from issuing an order so editing a node cannot accidentally launch a fleet. The precise input affordance must be clear in the UI and verified by pointer and keyboard.
- The selected-node panel exposes current stationed Dart count, production on/off, node type (planet/moon/asteroid), and ownership (neutral or one of the three players). Counts must be nonnegative integers within supported rule limits; invalid input leaves state unchanged with a useful explanation.
- An ownership override applies immediately, cancels that node's local capture/battle, and assigns the stationed Darts to the chosen owner. It does not reroute or delete travelling fleets. Administrative edits are not combat casualties, production events or campaign achievements.
- Setting current Darts replaces the stationed total rather than adding to it. Show live counts rather than stale starting values. The treatment of opposing committed forces during count edits is an explicit validation item below, not a new combat rule implied by this UI.
- Changing type preserves ownership and stationed count and applies the new type's appearance and production defaults. Asteroids cannot produce; disable their production toggle with an explanation. Do not regenerate routes or cancel travelling fleets just because type changes.
- Production off prevents further production at that node. A later ownership/type change must leave the UI showing the actual resulting production state. Type changes apply the new type's defaults as agreed.
- Expose one color control per player. Changes immediately update all current player-owned nodes, stationed/travelling/combat Darts, player-colored routes/effects and labels. Later captures use that same player's current color. Neutral presentation remains neutral.

### Tuning and repeatable scenarios

- Provide production-rate, fleet-travel-speed, fleet-orbit-speed and separate stationed/travelling visible-Dart-limit overrides. Each identifies whether it uses the default or an override. Resetting an override returns to the current source default; changing the override never changes that source.
- Allow supported non-speed behavior overrides to be authored and saved in Edit Mode for this scene. Live controls change only runtime copies. Game speed is the exception: four shared Settings choices, preference initialization and temporary run override follow the game-speed specification exactly.
- Game speed scales simulation and animated presentation together. Pause freezes both while leaving camera, UI, settings and sandbox edits available. Orders and simulation do not advance behind an active pause menu or focused input control.
- Provide Reset Setup, Replenish Darts, and repeatable capture/battle examples. The treatment of current tuning during reset is listed for validation below. Starting a new run always drops all run overrides.
- Capture and battle examples must be deliberate user-triggered actions using the real rules, not a hidden loop that interferes with free testing. They must be repeatable after resolution or reset.
- Replenishment and example preparation are administrative commands; they do not falsify casualty totals or fire ordinary combat/production effects as if those edits had occurred naturally.
- Resolve invalid commands before mutating state. Do not use UI-side reflection or duplicate rule calculations to edit Node/Battle internals. Add narrowly named owner operations where required and update all affected callers.
- Existing third-faction arrivals into a two-sided battle are unsupported. Three controllable players do not silently authorize designing simultaneous three-sided combat. Resolve the policy identified below before implementing affected live orders; the sandbox must not encounter an unhandled exception, lose a fleet silently or freeze.

## Testing Decisions

- Prefer one public sandbox session/command boundary for deterministic tests: start setup, select/issue order, override node/settings, advance, reset and inspect public state. Exercise actual simulation rather than a second sandbox simulation. Follow MatchTests, BattleTests, EliminationTests and PauseTests as prior art.
- Test three source owners issuing legal orders without active-player switching; neutral/contested inspection; invalid and disconnected orders; and continued simulation after ordinary win/lose conditions. Normal missions must retain their existing restrictions and outcomes.
- Test ownership override during capture and battle, type changes, production toggles, count validation and continuing travelling fleets. Check no artificial casualty/production accounting and no half-applied mutation.
- Verify default inheritance and independent runtime overrides. For live travel/production changes, assert the agreed in-progress timing policy after it is validated; retain simulation order and time-step equivalence tests.
- Cover persistent preference initialization, sandbox speed override, Settings replacement, pause/resume, unrelated Settings saves, run exit and new-run initialization through the shared speed policy.
- Start and reset through the sandbox session boundary. Verify that a fleet at partial route progress arrives after the remaining travel time, a capture at partial progress completes after its remaining time and a battle with authored counts takes casualties on the normal schedule. Verify that a battle during a capture holds its progress and that reset restores the starting counts and progress after the run has changed them.
- Cover a zero-Dart capture, a battle without attackers, a battle without stationed Darts, and an invalid fleet alongside valid activity. Verify the resulting ownership, capture state and fleet behavior, and check that the diagnostic identifies the invalid entry.
- Test replenishment and repeated examples. Verify that reset clears activity created during the run and avoids duplicate fleets, battles and subscriptions. Confirm shared authoring objects and persisted preferences are unchanged by sandbox actions.
- In the Sandbox scene, compare the frozen Edit Mode setup with the first live moment and watch the configured activity continue. Check the fleet positions, capture progress and battle front direction, and verify that selection, hover and order previews reflect live input. Stop and re-enter to verify the same authored start. In Stage 6, verify main-menu entry produces the same initial battlefield.
- Manually play the Sandbox scene: select/edit all node states, launch all three factions, recolor players, inspect animated rings and fleets, change visible limits, pause and resume, trigger examples, conquer everything and reset. Verify UI input cannot leak into world orders.
- Save the scene in Edit Mode, play and edit every supported runtime property, exit and reopen: only intentional Edit Mode changes remain. Inspect generated missions for unchanged behavior and shared visual appearance.
- Keep automated tests Edit Mode-only. The sandbox can supply deterministic fixtures where useful; do not make all simulation tests load a Unity scene, add brittle screenshot assertions, or require sandbox access for unrelated rules.

## Out of Scope

Persistent sandbox runs, Save Tuning, arbitrary mission loading, runtime node creation/deletion or dragging, AI, multiplayer, general mission editing, animated Edit Mode playback, authoring battle timer phase or casualty history, simultaneous three-sided combat rules and menu entry (next stage).

## Further Notes

### Assumptions still to validate

- **Three-faction combat is an implementation gate for this stage.** The current game explicitly rejects a third side joining an existing battle. Decide a safe sandbox admission/arrival policy before implementing live orders. A third fleet may have launched before the battle existed, or the scene may start with the fleet already in transit toward an authored battle. Launch-time checks alone are insufficient. Do not silently invent combat outcomes; earlier stages remain independently implementable.
- Define a count edit's treatment of attacking/capturing forces and suspended capture state, plus a type edit's treatment of an existing battle/capture. The agreed ownership override cancels local conflict; that does not automatically define these other edits. Confirm before implementation of those commands.
- Specify when live production/travel overrides affect existing production progress and in-flight fleets. Preserve existing progress unless an explicitly chosen policy says otherwise; changing game speed already scales all ongoing time.
- Confirm whether Reset Setup retains current run tuning/colors/speed or resets those too, and choose replenishment amounts/targets plus capture/battle example setups. Recommended boundary: restore the battlefield while retaining tuning for comparison; new-run entry always restores configured defaults and saved game speed.
- Confirm clear inspection-versus-order gestures and UI layout during implementation. They must permit editing neutral and contested nodes without weakening ordinary order rules.

These points remain open for targeted follow-up before implementing the dependent behavior.

### Decisions taken at implementation

The points above were settled as follows before the dependent behavior was built.

- **Third side:** in the sandbox only, a fleet that reaches a battle between two other sides waits just outside. It arrives by the normal rules in the instant that battle ends, after its casualties, in launch order. It can join, land or start the next battle. Orders toward such a battle are allowed. Missions still refuse a third side with an error.
- **Count and type edits:** a count edit replaces the stationed side: the owner's Darts, the captor's during a capture, or the defenders' during a battle, which is then timed afresh as after a reinforcement. Setting none clears a capture, and the owner keeps the world. In a battle, none lets the attackers land as on an empty world. A type edit keeps the owner, the Darts, and any battle or capture, and switches production on with the new type's rate.
- **Live tuning:** a Dart in progress and fleets in flight keep their progress fraction and finish at the new rate or speed.
- **Reset and examples:** Reset Setup restores the battlefield and keeps the run's tuning, colors and speed. Replenish tops every player-owned world that is not being captured or fought over up to its authored Darts. The examples play out at the selected world, or the first moon. The capture example empties the world and lands 10 Darts of its first neighbor's owner 1.5 s later. The battle example gives the world 15 Darts of one player, and 25 of another arrive 1.5 s later.
- **Gestures:** the panel has an Inspect/Order mode toggle (M). Inspect clicks select any world and never order. Order clicks work as in a mission for the owner of the selected world. Space pauses, and Esc or a right-click clears the selection. The pointer over the panel, and keys while a panel control has the keyboard, never reach the battlefield.
- **Controls after the first review:** Order mode, pause/resume, game speed and Reset Setup moved to an icon menu in the top right corner: a hand lit in Order mode, a pause icon that becomes a play icon, one to four arrows for Slow to Turbo that step to the next speed and wrap, and a circling arrow. The panel under it is more transparent and slides away to the right behind an arrow, and the battlefield is reframed to the whole screen. Replenish and the two examples are no longer in the panel; the session keeps them. Each player row has Give World, which hands the selected world to that player, and a color picker with Reset Color in place of the hue slider, hex field and Own. Production is tuned in Darts per second. A new orbit speed carries on from where the Darts are instead of jumping to where the new speed would have put them.
- **Controls after the second review:** the menu has no border. The mode button shows an arrow pointer in Inspect mode and the hand in Order mode, both lit. Game speed shows its multiplier (×0.75 to ×2) in a button as wide as the others. The panel is as tall as its rows, its arrow sits halfway down its edge, and sliding it no longer moves the camera. Default and Reset Color are the reset icon with the tooltip Set Default; Reset Color sits beside Give World and shows only while the player is recolored. Typing a tuning value equal to the default clears the override.
- **Controls after the third review:** the menu is just its buttons, with no background, and the mode icon is no longer lit: the icon itself says the mode. Tooltips are drawn by the sandbox, since UI Toolkit shows none on a game panel. Give World is gone; owner and type are dropdowns. The panel's arrow tab is half the size, with the arrow unchanged. The panel scrolls only vertically, with a thin bar in its own colors and no arrow buttons. The world the panel edits changes only on a click on another world in Inspect mode: a click on empty space keeps it, and orders never change it. Slow game speed is 0.5, so the four speeds step by 0.5.
- **Controls after the fourth review:** the line under the menu is gone, and with it the notices of refused clicks; a click that cannot be carried out does nothing. Only the Set Default icons keep a tooltip. The panel is 420 px wide, its text box, dropdowns and the Produces Darts box share one column at one height, and the open dropdown list matches the closed value's size. A click on a world in either mode makes it the panel's world, except a click that sends a fleet; empty space never clears it. A run starts with the panel slid away. A run starts in Order mode.
