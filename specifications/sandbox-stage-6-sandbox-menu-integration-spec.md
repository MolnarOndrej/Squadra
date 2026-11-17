# Stage 6 of 6: Sandbox main menu integration

## Problem Statement

An Editor-only scene cannot be used independently in a built game. Players and testers need a discoverable way to enter the same sandbox without launching a campaign mission or relying on Unity.

## Solution

Add a Sandbox entry to the main menu and include the playable scene in game builds. Enter a fresh sandbox run using the saved game-speed preference and provide a clear return to the main menu.

This is stage 6 and depends on the [playable sandbox](sandbox-stage-5-playable-sandbox-spec.md), including its resolved validation items. It completes the sequence beginning with [shared visual tuning](sandbox-stage-1-shared-visual-tuning-spec.md). This stage adds navigation and packaging, not another sandbox implementation.

## User Stories

1. As a player, I want a Sandbox main menu entry, so that I can experiment without opening Unity or starting a campaign mission.
2. As a player, I want the same three-player setup and controls in a build, so that testing is not restricted to the Editor.
3. As a player, I want each entry to start a fresh run, so that the last experiment does not leak into the next.
4. As a player, I want the sandbox to start at my saved game speed, so that its initial pace matches Settings.
5. As a player, I want Back to Main Menu, so that I can leave the sandbox without closing the game.
6. As a player, I want sandbox activity to leave campaign completion unchanged, so that experiments do not unlock or complete missions.
7. As a keyboard user, I want predictable focus and navigation, so that the new entry and exit controls work like the rest of the menus.
8. As a tester, I want Settings changes to persist and sandbox tweaks discarded on exit, so that the two kinds of controls remain distinct.

## Implementation Decisions

- Add the Sandbox entry to the main menu's existing control line and navigator. Reuse the current style, focus/highlight rules, hover/selection/back sounds and keyboard/pointer behavior. Update builder wiring with the UI change.
- Package the scene, shared visuals and runtime controls in game builds. Runtime sandbox code must not require UnityEditor, the source checkout, absolute local paths, external mission JSON or editor-only authoring UI.
- Scene construction and build inclusion belong in the existing builder/build setup. Keep the normal main menu entry scene and existing campaign selection flow.
- Every entry creates a fresh sandbox session from the saved setup. Reset runtime node edits, colors, fleets, captures, battles and non-speed run overrides. Apply saved scene-authored sandbox behavior overrides; initialize game speed solely from the current saved Settings preference.
- Entering or playing the sandbox does not change selected campaign identity, campaign completion or unlock reveals. Never route sandbox completion through campaign results; sandbox completion is disabled.
- Provide Back to Main Menu from the sandbox. It returns to the root main menu, not the campaign map. Reuse existing pause/Settings conventions where applicable, including temporary speed override replacement by an explicit Settings speed choice.
- On exit, drop runtime overrides and paused state. Preserve explicit Settings changes, restore menu timing/audio behavior and prevent duplicate scene navigation. A later normal mission must use normal source defaults plus the saved global speed.
- Preserve the main menu's existing Single Player, disabled Multiplayer, Settings and Quit behavior. The sandbox remains independent of whether any campaign entry is playable.
- Keep Edit Mode authoring controls separate from the supported runtime controls, while using the same sandbox setup, rule commands and production presentation.

## Testing Decisions

- Use the full main menu to sandbox to main menu flow as the primary integration seam. Extend existing menu navigation tests where they cover declared control order/focus, without asserting visual implementation details.
- In Editor Play Mode and a standalone game build, enter by pointer and keyboard, use three-player controls, change temporary speed, leave and reenter. Confirm fresh state and initialization from saved Settings.
- Change global speed from sandbox Settings, then make another temporary sandbox change. Exit and start a normal mission: the global choice persists, the temporary choice does not. Repeat while leaving a paused sandbox.
- Verify prior campaign completion, selected mission and pending unlock information remain unchanged across sandbox use. Launch a campaign mission afterward and check ordinary order restrictions, time, results and pause behavior.
- Verify return to root main menu, correct focus, audio, no stacked overlays, no inherited dimming and no accidental action from the activating input. Test rapid repeated activation for a single scene transition.
- Rebuild using the affected builders, verify scene inclusion and references, compile, run affected Edit Mode tests and formatting checks, and inspect the generated asset diff. Play MainMenu, Sandbox and Mission; include MissionEditor only if shared changes actually affect it.
- Report standalone-build checks separately; an Editor scene transition is not evidence that the packaged game includes every required asset.

## Out of Scope

Shipping the Unity Inspector, persistent sandbox saves, campaign unlock requirements for sandbox access, achievements, network play, a mission browser and unrelated main menu redesign.

## Further Notes

### Assumptions still to validate

- Recommended root-menu placement is after Single Player and before Multiplayer, with initial focus remaining on Single Player. Confirm this small ordering choice during implementation against the current declared navigation line.
- Confirm whether Back to Main Menu uses a leave-confirmation dialog. Reusing the existing pause quit convention is recommended; do not imply the temporary sandbox run can be saved.

This is the final integration stage. Each earlier specification must be reviewable and verifiable without waiting for it.
