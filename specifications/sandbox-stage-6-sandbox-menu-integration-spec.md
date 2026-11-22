# Stage 6 of 6: Sandbox main menu integration

## Problem Statement

The playable Sandbox currently starts from its scene in the Unity Editor. Players and testers need a way to open that same battlefield in a standalone build without starting a campaign mission.

## Solution

Add a Sandbox entry to the main menu and include the playable scene in game builds. Enter a fresh sandbox run using the saved game-speed preference. Provide shared Settings and a clear return to the main menu.

Stage 6 integrates the Stage 5 playable scene and the Stage 4 shared game clock. It adds main-menu entry, build inclusion, and access to shared Settings and a return action from the Sandbox. The existing session, controls and battlefield presentation carry over. [Stage 5 playable sandbox](https://github.com/MolnarOndrej/Squadra/blob/6c5862f6bc9793b5617b04da544e91f1eef51e93/specifications/sandbox-stage-5-playable-sandbox-spec.md) · [Stage 4 game speed](https://github.com/MolnarOndrej/Squadra/blob/6c5862f6bc9793b5617b04da544e91f1eef51e93/specifications/sandbox-stage-4-game-speed-settings-spec.md)

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
- Package `Assets/Squadra/Sandbox/Scenes/Sandbox.unity` with its shared visual assets and runtime controls. Use the scene's serialized setup and build-included assets so the run works independently of the Unity Editor and source checkout.
- Register the Sandbox scene once in the game build and preserve that registration when rebuilding. Keep MainMenu first and Mission enabled. Preserve the Sandbox scene's authored worlds, routes, starting activity and behavior overrides when running its builder.
- Each main-menu entry starts the same authored moment as direct Play Mode entry, including fleet route progress, capture progress, battle forces and front directions, and scene-authored behavior overrides. Start unpaused, in Order mode, with the edit panel collapsed. Earlier runs' edits, colors and tuning are discarded. Reset Setup within a run restores the authored battlefield and keeps the current tuning, colors and speed.
- Initialize the shared game clock from the saved Settings preference. The existing Sandbox multiplier button cycles through the shared choices and changes the current run only.
- Entering or playing the sandbox does not change selected campaign identity, campaign completion or unlock reveals. Never route sandbox completion through campaign results; sandbox completion is disabled.
- Provide access to the shared Settings screen and a Back to Main Menu action from the Sandbox. Back to Main Menu returns to the root screen without requesting the campaign map. Keep gameplay frozen while Settings is open and follow the existing pause menu's resume behavior.
- Preserve the Sandbox's Space pause shortcut and battlefield selection behavior. While a menu or dialog has focus, consume its navigation input before it reaches the battlefield. An explicit Game Speed choice in Settings saves the global preference and clears the run override, including when the selected value is already saved. Saving another setting preserves the run override.
- On exit, drop runtime overrides and paused state. Preserve explicit Settings changes, restore menu timing/audio behavior and prevent duplicate scene navigation. A later normal mission must use normal source defaults plus the saved global speed.
- Preserve the main menu's existing Single Player, disabled Multiplayer, Settings and Quit behavior. The sandbox remains independent of whether any campaign entry is playable.
- Keep Edit Mode authoring controls separate from the supported runtime controls, while using the same sandbox setup, rule commands and production presentation.

## Testing Decisions

- Use the full main menu to sandbox to main menu flow as the primary integration seam. Extend existing menu navigation tests where they cover declared control order/focus, without asserting visual implementation details.
- In Editor Play Mode, compare direct Sandbox scene entry with main-menu entry. Verify both start with the same authored fleet positions, capture progress, and battle forces and front direction, and that the activity continues as the simulation advances. Verify the same authored start when entering from the menu in a standalone build.
- Enter by pointer and keyboard in Editor Play Mode and in a standalone build. Both start unpaused, in Order mode, with the edit panel collapsed. Use the three-player controls, change the battlefield and runtime tuning, then exit and reenter to verify a fresh run at the saved speed. Within a run, verify Reset Setup retains the current tuning, colors and speed.
- Verify the battlefield stays frozen while Sandbox Settings is open and that menu input does not reach the battlefield. Override the speed, then save an unrelated setting and verify the override remains. Choose a speed explicitly in Settings, including the already-saved choice, and verify the multiplier updates. Set another temporary speed, exit while paused, and start a normal mission to verify it uses the saved speed.
- Verify prior campaign completion, selected mission and pending unlock information remain unchanged across sandbox use. Launch a campaign mission afterward and check ordinary order restrictions, time, results and pause behavior.
- Verify return to root main menu, correct focus, audio, no stacked overlays, no inherited dimming and no accidental action from the activating input. Test rapid repeated activation for a single scene transition.
- Run the affected builders and verify the build list keeps MainMenu first, includes Mission and Sandbox once each, and preserves the Sandbox scene's authored setup and asset references. Compile, run affected Edit Mode tests and formatting checks, and inspect the generated asset diff. Play MainMenu, Sandbox and Mission. Include MissionEditor if shared changes affect it.
- Report standalone-build checks separately from Editor Play Mode checks.

## Out of Scope

Shipping the Unity Inspector, persistent sandbox saves, campaign unlock requirements for sandbox access, achievements, network play, a mission browser and unrelated main menu redesign.

## Further Notes

### Assumptions still to validate

- Recommended root-menu placement is after Single Player and before Multiplayer, with initial focus remaining on Single Player. Confirm this small ordering choice during implementation against the current declared navigation line.
- Confirm where Settings and Back to Main Menu sit in the Sandbox controls and how they are reached by keyboard. The existing battlefield Escape action clears the current order selection when no menu or dialog has focus.
- Confirm whether Back to Main Menu uses a leave-confirmation dialog. If it does, use the existing confirmation conventions and describe the loss of the temporary run.

This is the final integration stage. Each earlier specification must be reviewable and verifiable without waiting for it.
