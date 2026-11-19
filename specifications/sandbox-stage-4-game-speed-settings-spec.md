# Stage 4 of 6: Persistent game speed settings

## Problem Statement

Players cannot choose a persistent game speed. The controller currently restores a fixed normal time scale on resume, which would also overwrite a selected speed. The future sandbox needs the same choices with a temporary override that cannot leak into saved preferences.

## Solution

Add Game Speed to the shared Settings screen's General tab with exactly four options. Save changes globally and apply them to active gameplay. Define one effective-speed policy shared by normal missions and the sandbox, with pause taking precedence.

| Option | Multiplier |
| --- | ---: |
| Slow | 0.75 |
| Normal | 1.0 |
| Fast | 1.5 |
| Turbo | 2.0 |

Normal is the default. This is stage 4 and can be implemented independently of the sandbox scene once existing settings and time ownership have been reviewed. It precedes the [playable sandbox](https://github.com/MolnarOndrej/Squadra/blob/3cd6587932c486d1bd819024edb8ab47e69583a3/specifications/sandbox-stage-5-playable-sandbox-spec.md).

## User Stories

1. As a player, I want four named speed choices in General settings, so that I can select a predictable pace.
2. As a player, I want Normal by default, so that an untouched profile retains today's pace.
3. As a player, I want speed saved across application launches, so that I do not repeatedly configure it.
4. As a player, I want the same choice in main menu and pause Settings, so that the two screens never disagree.
5. As a player, I want production, travel, capture, battles, orbiting and effects scaled together, so that the game's motion matches its rules.
6. As a player, I want responsive camera and UI at every speed, so that changing pace does not affect navigation.
7. As a player, I want pause to freeze gameplay and resume at my chosen speed, so that pause does not reset my preference.
8. As a sandbox user, I want each run initialized from Settings, so that its initial pace matches the rest of the game.
9. As a sandbox user, I want an on-screen temporary speed selector, so that experiments do not overwrite Settings.
10. As a sandbox user, I want changing speed through Settings to replace my temporary override, so that both controls have clear precedence.

## Implementation Decisions

- Settings owns the persistent preference and shared General-tab control. Use the existing GameSettings load/save lifecycle and shared SettingsScreen, including immediate application and existing menu navigation conventions. No additional settings tab is needed.
- Offer exactly Slow 0.75, Normal 1.0, Fast 1.5 and Turbo 2.0 in that order. A new or unsupported stored value resolves to Normal. Store a stable supported choice rather than accepting arbitrary floats through the UI.
- Load the preference before gameplay starts. Save completed Settings interactions. Changes from main menu Settings affect subsequent missions; changes from pause Settings update the selected pace immediately while the match remains frozen until resumed.
- Maintain distinct saved preference, optional sandbox-run override and paused state. Effective gameplay speed is zero while paused; otherwise it is the sandbox override when present, or the saved preference. Pausing must not save zero or erase the chosen speed.
- A new sandbox run starts from the current saved preference with no speed override, including direct Editor scene entry. Game speed is explicitly excluded from scene-saved sandbox behavior overrides.
- The future on-screen sandbox selector uses the same four choices. It sets only the run override and does not call persistent preference setters or saving. Saving another preference, such as volume, must not accidentally persist the override.
- An explicit game-speed choice through Settings always saves globally, clears the active sandbox speed override and updates the on-screen selector, even if the chosen preference equals the previously saved value. A later sandbox selector change is temporary again.
- Scale production, fleet travel, capture, battle timing, match elapsed time, orbit movement, gameplay animation and effects by the effective speed exactly once. Preserve simulation event order and large-step/small-step equivalence. Do not bake the multiplier into individual rule defaults as well as elapsed time.
- Keep camera controls, UI, menu feedback, music and display-confirmation deadlines on their normal clocks. Game speed is not an audio pitch setting. Preserve the existing pause and background-mute audio behavior.
- Resuming restores the latest selected effective speed, including Settings changes made while paused. Leaving gameplay discards sandbox overrides and releases pause state without changing the saved preference or speeding up menus. Restarted normal missions use the saved setting.
- Rule assemblies remain independent of Unity time and persistence. Presentation supplies elapsed simulation time; Settings handles platform persistence. Prefer a small explicit clock-policy boundary over unrelated controllers independently writing time scale.

## Testing Decisions

- Test externally visible effective-speed behavior through the settings/clock boundary. Follow the public-behavior style of PauseTests and existing simulation tests. Persistence tests must isolate and restore their preference keys.
- Cover all four choices, missing/unsupported saved values, loading across sessions, main menu/pause consistency and unchanged unrelated preferences.
- For identical starting state, advance each speed for equivalent simulated time and compare production, arrivals, capture, casualties and elapsed match time. Retain the existing event-order and time-step equivalence tests unchanged.
- Pause at each speed, change Settings while paused, resume and verify the new speed without catch-up. Repeat pause/resume and scene transitions to detect reset-to-1 or compounded scaling.
- Cover preference to temporary override, Settings replacement including reselecting the stored choice, later temporary override, and run exit/reentry. Verify saving volume while overridden does not save the sandbox speed.
- Exercise the shared General control by pointer and keyboard. In Play Mode and a standalone build, inspect orbit/effect speed, normal UI/camera responsiveness, unchanged music and the real-time display confirmation timeout while paused.
- Run affected Edit Mode suites, compilation and formatting when implementing. Sandbox policy can be tested before its UI exists; the playable stage verifies the actual on-screen control.

## Out of Scope

Additional speed values, numeric speed entry, saved per-mission speeds, difficulty/balance changes, multiplayer time synchronization and implementing the rest of the sandbox UI.

## Further Notes

### Assumptions still to validate

- Audit shader time, effects and camera input clocks; changing only simulation elapsed time or Unity time scale may not satisfy every animation and camera contract.
- Confirm the current settings control can commit a selection equal to its stored preference while a sandbox override exists. The user action must still clear the override.

This extends shared settings and supersedes restoring a fixed 1.0 speed on resume; other pause contracts remain intact.
