# One navigator for every menu screen

## Problem Statement

Every menu screen (main menu, mission map, Settings in the main menu and in the pause menu, pause menu, results screen, confirmation dialog) wires the same pointer and keyboard callbacks by hand and repeats the hover-cue rule. PR #24 made the behavior uniform, but screen by screen, so a new screen or a change to the rule has to be repeated in several places. Screens that still rely on UI Toolkit's position-based choice for Up/Down/Left/Right are where the next "Up from X goes somewhere odd" bug will come from; it already happened on Settings.

## Solution

One `MenuNavigation` per menu document root gives every screen in it the same pointer and keyboard behavior. Screens only declare their layout: which controls form a column or a row, whether it wraps, and how groups link to each other. The navigator interprets that layout, so no menu screen falls back to the position-based choice. The step logic ("the next stop of a line, wrapping, skipping what cannot take the focus") lives in a small rules assembly with Edit Mode tests.

The behavior players see stays as it is, with one deliberate fix at the end (A and D on the mission map).

## User Stories

1. As a player, I want the highlight to sit on the focused control whether I use the mouse or the keyboard, so that I always see what Enter would activate.
2. As a player, I want pointing at an enabled control to give it the focus, so that switching to the keyboard carries on from there.
3. As a player, I want the highlight to stay on the last control I pointed at, and a click on empty space to leave it there, so that I don't lose my place.
4. As a player, I want any key to switch to keyboard use, and my first real mouse move to switch back and focus the control under the cursor, so that the two inputs never fight.
5. As a player, I want one quiet hover cue each time the highlight moves because of me, and silence when it does not move or when the game moves it, so that the sound means something.
6. As a player, I want Up/Down/Left/Right (and W/S/A/D) to follow each screen's visible layout and to wrap where they wrap today, so that the keyboard never jumps somewhere odd.
7. As a player, I want Left/Right on the Settings tabs to walk the tabs and wrap, and Left/Right on a slider to change its value, as today.
8. As a player, I want one press of A or D on the mission map to move one stop, like Left or Right.
9. As a developer, I want a new menu screen to get all of this by declaring its layout, so that I don't have to copy callbacks or the cue rule.

## Behavior that must hold

The rules PR #24 settled with the user, unchanged:

1. **Highlight = focus** in both input modes (`MainMenu.uss`, shared by the main menu, pause menu and results screen). Pointer mode does not highlight `:hover`. `menu-root--pointer` / `menu-root--keys` only drive the pressed (`:active`) look.
2. **Pointing gives focus**: the pointer entering an enabled control outside keyboard mode gives that control the focus: buttons, mission dots, page arrows, Settings tabs and Settings fields. A disabled control does nothing.
3. **The highlight stays on the last control pointed at.** A press on empty space keeps the focus (main menu, pause menu); the results screen ignores any press that is not on a `Button`.
4. **Keyboard mode**: any key or navigation move switches to it; a control that appears under a resting pointer then neither takes the focus nor sounds. The first real pointer move (the same position reported again does not count) switches back and gives the focus to the control under the cursor.
5. **Hover cue** = "the highlight moved because of the player". It plays once when a pointer entry or a navigation move takes the focus to a different control. Silent when the control already has the focus, when the move is blocked (edge of a line without wrap, slider value change, map Left/Right from Back), when the program sets the focus (screen opens, dialog opens on Cancel, results default action, `KeepFocus`, unlock reveal), and on a page turn (it keeps its own selection cue). Pointer entry on a Settings field gives the focus silently, as today (see Assumptions).
6. **Results screen guards**: no pointer focus or cue before it is ready, while leaving, or while suspended under the pause menu. The first navigation move restores the default action when the focus was lost.
7. Escape, dropdown popups (Escape that closes one must not also go back), `MenuFocus.Soon`, the dialog's return of the focus to its opener, and Tab/Shift+Tab (UI Toolkit's own sequential order) stay as they are.

### Layouts

| Screen | Layout |
| --- | --- |
| Main menu | Column Single Player, Multi Player, Settings, Quit; wraps; disabled Multi Player skipped. Left/Right stay put. |
| Mission map | Row ◀ arrow, playable missions, ▶ arrow; wraps; hidden/disabled arrows and unplayable dots skipped. Up/Down from the row go to Back; from Back to the last focused mission, else the default mission, else an enabled arrow. Left/Right do nothing on Back. A page turns only when an arrow is activated; the focus then stays on that arrow, or goes to a playable mission if the arrow became disabled. |
| Settings | Column: the selected tab, the rows of the open category that can take the focus, Back; wraps. Any tab counts as the top; from Back, Down goes to the selected tab. The tabs form a row that wraps. Left/Right on a slider change its value. |
| Pause menu | Column Resume, Settings, Quit to Menu, Quit to Desktop; wraps. Left/Right stay put. |
| Confirmation dialog | Row Cancel, Confirm; wraps. Up/Down stay put. |
| Results screen | Row Next Mission, Restart, Back to Menu; wraps; disabled or hidden Next Mission skipped. Up/Down stay put. |

The pause menu, dialog and results rows are what UI Toolkit does today, recorded in play mode on `47f6365`:

- Pause menu: Up from Resume → Quit to Desktop, Down from Quit to Desktop → Resume; Left/Right stay put.
- Dialog (measured in the pause menu; same template in the main menu): Left/Right go to the other button; Up/Down stay put.
- Results with Next Mission enabled: Left from Next Mission → Back to Menu, Right from Back to Menu → Next Mission; Up/Down stay put.
- Results with Next Mission disabled or hidden: Left/Right go to the other of Restart and Back to Menu; Up/Down stay put.

### W, A, S and D

UI Toolkit reads its Navigate input from the project-wide input actions (`Assets/Settings/InputSystem_Actions.inputactions`, map "UI", action "Navigate"), which bind W/A/S/D next to the arrows. So on every menu screen W/A/S/D already act as Up/Left/Down/Right, with key repeat, slider values and open dropdown lists included. This stays.

The mission map also handles A and D itself (`MainMenu.Update` calls `MissionMap.StepAlongRow`). A press then most likely steps twice: once from the navigation move, once from that code. Repro: make several missions playable and give the map page arrows (`CampaignSession.Progress.Complete(id)` / `MarkRevealed(id)` for the catalog's missions, then `map.Configure(map.Catalog, 2)`), open the map, focus the ◀ arrow, press D: the focus lands two stops along instead of one. The last change removes the map's own A/D handling.

## Implementation Decisions

- **`MenuNavigation`** (`Assets/Squadra/Menus`, Assembly-CSharp, namespace `Squadra`), one per document root: main menu, pause menu, results screen. It absorbs `MenuInputMode` (mode and its style classes), takes the pointer-gives-focus rule from `MenuAudio.AttachHover`, `MenuInputMode.FollowPointer` and `ResultsScreen.OnPointerEnter`, and handles empty presses, first-move re-entry and the hover cue. It listens once on the root instead of once per control.
- **The cue is played where the player's move is handled** (pointer entry, navigation move), never from `FocusInEvent`, so program focus stays silent without guessing.
- **`MenuAudio`** goes back to only playing cues; `AttachHover` and `PointerActive` go once nothing uses them.
- **Declared layouts**: a screen gives the navigator lines (column or row, wrapping or not) whose stops come from a provider called at the time of the move, so they follow page turns, tab changes and controls that hide or disable. A line can be a stop of another line (the map's row and Back; the Settings tab row in its column), with an entry stop chosen by the screen (last focused mission, selected tab). Disabled or hidden controls are skipped. A move across a line from one of its stops is claimed and does nothing, unless the stop keeps that direction for itself (a slider keeps Left/Right). A move from a control outside every declared line is left to UI Toolkit; once every screen is converted, none is.
- **Page turns and tab switching stay with their screens** (`MissionMap`, `SettingsScreen`).
- **The results screen** passes its "not now" condition to the navigator instead of keeping its own handlers, and its press rule (only a `Button` press takes the focus).
- **Rules assembly `Squadra.Menus`** (`Assets/Squadra/Menus/Rules`, `noEngineReferences`): only the step logic over a line's stops. Named need: this change rewires six screens, and their navigation would otherwise be checked in play mode only. The presentation scripts of `Menus` move to `Menus/View`, as in other features with rules.
- **Order of changes**, one commit each, in one branch and PR: move the `Menus` scripts to `View`; add the step model; `MenuNavigation` and the main menu; the pause menu; the results screen; Settings and the map as declared layouts; remove the `MenuInputMode` / `AttachHover` leftovers; remove the map's own A/D handling. All but the last preserve behavior.

## Testing Decisions

- Edit Mode tests for the step model: stepping forward and back, wrapping at both ends, no wrap at the edges, skipping stops that cannot take the focus, a line with one usable stop, and the move along versus across a line.
- Presentation is checked in play mode on `MainMenu/Scenes/MainMenu.unity` and `Match/Scenes/Mission.unity` (pause menu, dialog, results): the arrow-key results of every layout above, compared with the recordings, plus hover-cue counts (`MenuAudio.HoverCount`) for pointer entry, re-entry, blocked moves and program focus.
- The existing Edit Mode tests and `dotnet format whitespace --verify-no-changes` run after each step.

## Out of Scope

- The Mission Editor, which receives the same project-wide Navigate input.
- Gamepad input, Tab/Shift+Tab order, Escape handling.
- Any change to the layouts above, the cue sounds, styles or focus after screen changes.
- `specifications/main-menu-spec.md`, which still describes the pre-#24 page keys; it is a record and stays as is.

## Assumptions still to validate

- Pointer entry on a Settings field (dropdown, slider, checkbox) gives the focus silently, while a keyboard move onto a field plays the cue. The refactor keeps that. Whether fields should also cue on pointer entry is open.
- What Left/Right do today on the Settings tabs, dropdowns, checkboxes and Back is measured before Settings is converted; any difference from the layout above is raised before it is changed.
- The A/D double step on the map follows from the code and was not observed with real key presses (simulated keys do not reach UI Toolkit while the editor is unfocused).
