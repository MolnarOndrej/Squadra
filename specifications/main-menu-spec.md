# Squadra main menu and mission map

## Problem Statement

I can play Squadra's first scenario by opening it directly in Unity. I want the game to start at a polished main menu where I can choose a mission, adjust settings, or quit. The mission map should show where I am in the campaign and what comes next, even while later scenarios are still being built.

## Solution

Open the game on a space-themed main menu with **Squadra** centered at the top. Below it, show **Single Player**, **Multi Player**, **Settings**, and **Quit** in that order. Single Player opens a connected map of five missions. Mission 1 launches the existing playable scenario. Missions 2–5 appear as locked previews until their scenarios are ready. Victory advances progress for the current session, and the map animates newly available missions when the player sees them.

## User Stories

1. As a player, I want Squadra to open on its main menu, so that I can decide what to do before starting a match.
2. As a player, I want the game name centered at the top, so that the title and menu choices are easy to find.
3. As a player, I want a space-themed interface with readable labels and clear focus states, so that the menu feels like part of the game and is easy to navigate.
4. As a player, I want Single Player to open the mission map, so that I can pick where to begin.
5. As a player, I want to see five connected dots labeled Mission 1 through Mission 5, so that I can see the current campaign route and its order.
6. As a new player, I want only Mission 1 to be available, so that the starting point is obvious.
7. As a player, I want a lock icon and status text on unavailable missions, so that I can recognize their state without relying on color.
8. As a player, I want previews to explain that their scenarios are coming soon, so that I understand why victory does not make them playable yet.
9. As a player, I want an available mission to open its own gameplay scene when selected, so that I can start playing directly from the map.
10. As a player, I want to keep access to missions I have completed, so that I can replay them.
11. As a player, I want to see which missions I have completed, so that I can distinguish them from the next mission to play.
12. As a player, I want each ready scenario to become available after its predecessor is completed, so that I can follow the campaign in order.
13. As a player, I want my victories to carry over when I return to the menu during the same session, so that I can continue through available missions.
14. As a player, I want a fresh launch to start the campaign at Mission 1, so that I know where to begin each session.
15. As a player, I want an unlock animation when a new mission becomes available, so that I can see where to go next.
16. As a player, I want the map to surface an unlock even if it is on another page, so that I can find the newly available mission.
17. As a player, I want to browse mission pages by clicking the arrows or using the keyboard, so that I can navigate with either input.
18. As a player, I want each page to show its own group of missions, so that I can see which missions come next when I use the arrows.
19. As a player, I want a Back action on the map and in Settings, so that I can return to the main menu.
20. As a player, I want Multi Player to appear disabled, so that I can see it is planned without opening an unfinished screen.
21. As a player, I want General, Graphics, and Sound sections, so that I can find settings by category.
22. As a player, I want General to show English as the available language, so that I can see which language the game supports.
23. As a player, I want to choose a supported display mode, resolution, and VSync setting, so that I can configure the game for my display.
24. As a player, I want separate Master, Music, and Sound Effects volume controls, so that I can set the audio balance I prefer.
25. As a player, I want my settings to carry over when I launch a mission or reopen the game, so that I can keep using my preferences.
26. As a player, I want Quit to ask for confirmation, so that I can recover from an accidental selection.
27. As a player, I want Cancel or Escape to dismiss the Quit dialog and Confirm to close the game, so that I can back out or exit deliberately.
28. As a designer, I want to set the number of missions per page in the Unity Inspector, so that I can adjust the mission map without editing code.
29. As a player, I want to hear a sound when I hover over a menu button, so that I get feedback before selecting it.
30. As a player, I want to hear a sound when I activate a menu button or Settings tab, so that I know my input registered.
31. As a player, I want a distinct sound when I go back or cancel a confirmation dialog, so that I can tell I dismissed it.
32. As a player, I want background music while I browse the menus, so that the menu carries the game's space theme.

## Implementation Decisions

- **Startup and navigation.** The game opens on the main menu screen, with the Squadra title centered at the top and four buttons below it in order, Single Player, Multi Player, Settings, and Quit. Single Player opens the mission map screen. Multi Player is disabled. Settings opens the settings screen. Quit opens a confirmation dialog over the main menu. While the dialog is open, confine interaction to it and focus Cancel by default. Pressing Escape in the Quit dialog has the same effect as clicking Cancel and returns the player to the main menu. Provide visible mouse hover and keyboard focus. Escape also returns from the mission map or settings screen to the main menu.
- **Visual direction.** Use a deep space backdrop, restrained star and orbit details, and a high-contrast accent for available actions. Keep the main menu hierarchy simple. On the mission map, keep connectors behind the dots and show availability through icons and text as well as color. Keep labels, dots, and arrows legible and unobstructed across supported desktop sizes.
- **Mission catalog.** Use one ordered catalog with stable mission IDs and optional gameplay scene references. Derive the displayed Mission X label from the mission's position in the catalog. Mission 1 uses the existing first playable scenario. Missions 2–5 have no launchable scenes in this milestone. A mission is launchable when its scene is included in the build and its immediate predecessor is complete. Mission 1 has no prerequisite. Guard against repeated activation while a scene load is in progress.
- **Progress.** Track completion by stable mission ID in memory for the current game session. Record it when the active campaign mission reports victory through the existing match result boundary. Defeat, leaving the scene, and restarting a match before victory do not advance progress. Replaying a completed mission does not replay an already acknowledged unlock. Keep campaign progress separate from gameplay simulation and settings. A fresh launch starts with no completed missions or pending reveals.
- **Preview and unlock states.** A mission with no launchable scene shows a locked preview with a Coming Soon label. A ready scene behind an incomplete predecessor shows a lock and identifies that predecessor. Available and completed missions are both clickable. When a ready mission first meets its prerequisite, retain its pending reveal for the session until the map shows it. On entry to the map, bring the relevant page into view and briefly animate the lock-to-available transition. Animate the connection when both missions appear on the same page. If the player leaves before the reveal completes, show it again on the next visit in the same session.
- **Pagination.** Expose an integer **Missions per Page** property on the mission map object in the Unity Inspector. Allow 1–10 missions per page and default to 5. Use that value to group missions in order, lay out mission slots, determine when arrows appear, and find the page for a newly unlocked mission. On the mission map, pressing A or Left Arrow moves to the previous page, and pressing D or Right Arrow moves to the next page. Each key behaves the same as clicking the corresponding arrow, including at the first and last pages. On a partial final page, place the remaining missions in the leftmost slots and leave unused slots on the right empty. Draw connectors only between dots visible on the current page. Show the arrows when the mission count exceeds the configured page size, and disable each arrow at its respective end. Omit page numbers.
- **Results integration.** The menu supports opening directly to the campaign map when another scene requests it. The future results screen's **Continue** button will use that entry point. This milestone records victory and retains the current result and replay interaction. For now, players return from Mission 1 to the main menu by stopping and relaunching the game, which starts a fresh campaign.
- **Settings.** General contains only Language, with English selected as the sole option. Graphics contains Display Mode, Resolution, and VSync where supported. The Display Mode dropdown offers Windowed, Borderless Fullscreen, and Exclusive Fullscreen on platforms that support them. Offer supported resolutions for the selected mode. Preview a change to Display Mode or Resolution and give the player 12 seconds to confirm it. Save the change when the player confirms. If the player cancels or the timer expires, restore the previous display settings. Sound contains Master, Music, and Sound Effects volume. Apply audio controls to existing music and gameplay effects, including victory cues, and preserve the current mix at default values. Save preferences locally when the player finishes interacting with a control, such as completing a click or releasing a volume slider. Update the audible volume during a drag and save the final value on release. Apply saved preferences across scene changes and launches.
- **Menu audio.** Play one hover cue when the pointer enters an enabled menu button. Play a selection cue when the player activates an enabled menu button or Settings tab, including activation by keyboard. Play the same cue once when the player changes mission pages by clicking a pagination arrow or pressing A, D, Left Arrow, or Right Arrow. Play a distinct back cue for Back and for dismissing a confirmation dialog with Cancel or Escape. Use only the back cue for those actions. Source a new track dedicated to the menu. Loop it across the main menu, mission map, Settings, and their dialogs without restarting it on navigation. Switch to mission music when a mission loads. Apply Master volume to both music and cues, Music volume to both tracks, and Sound Effects volume to the cues.

## Testing Decisions

- Use the playable menu as the primary test seam. Exercise controls through the same paths players use and assert visible state, scene transitions, and behavior after reopening the game. The existing Edit Mode match tests set the precedent for testing public behavior and already cover what constitutes a Blue victory.
- Start with a clean profile. Verify the main menu, the disabled Multi Player control, the five connected labels, all four locked previews, and the absence of pagination. Launch Mission 1 and verify that it is the existing Blue versus Red scenario.
- Win Mission 1 and use the map entry point in a controlled test to reopen the map in the same session. Verify the completion marker, replay access, and locked previews. In a normal playthrough, check that the existing replay action works and that relaunching returns to the main menu with a fresh campaign. Use a loss or early exit in a controlled test to verify that neither records completion.
- Use a seven-mission test catalog with ready scenarios to verify locked prerequisites, sequential unlocks, both arrow directions, and the disabled end arrows. Check that the first page shows Missions 1–5 and the second shows only Missions 6–7 in the first two slots, with the three slots on the right empty and no page number. Complete Mission 1 with Mission 2 ready and verify that Mission 2 becomes available with one reveal. Test that unlocking Mission 6 brings its page into view and animates its dot. Exercise the map entry point directly until the results screen exists.
- With the same catalog, set **Missions per Page** to 3 in the Inspector. Verify that the pages show Missions 1–3, 4–6, and 7, with two empty slots on the right of the last page. Check that unlocking Mission 4 reveals the second page. Set the page size to 7 and verify that all missions appear on one page and the arrows are hidden.
- Check that the Inspector limits the page size to 1–10 and that the mission map remains readable with ten missions on a page.
- Compare A and Left Arrow with clicking the previous-page arrow, and compare D and Right Arrow with clicking the next-page arrow. Verify that each key moves one page when available and stays on the current page at the corresponding end or when there is only one page.
- Verify that General shows only Language and offers only English. Check that a setting is saved after a completed click and that dragging a volume slider previews the volume and saves the final value on release. In a Windows standalone build, verify that Windowed, Borderless Fullscreen, and Exclusive Fullscreen are available and that a supported display change is saved on confirmation and reverts on Cancel or after 12 seconds. Launch a mission and relaunch the game to check that saved preferences carry over while the campaign starts fresh. Open the Quit dialog separately for each action and verify that Escape and Cancel return to the main menu and Confirm exits the game.
- Check that hovering over an enabled button plays the hover cue once per pointer entry. Verify selection cues for button and tab activation. Check that a page change plays the selection cue once through either mouse or keyboard input and that blocked navigation is silent. Verify that Back, Cancel, and Escape each play only the back cue when they dismiss a screen or dialog. Check that the dedicated menu soundtrack loops and continues across menu navigation, then switches to mission music when a mission loads. Verify that Music and Sound Effects independently control the music and cues, and that Master controls both.
- Review the menu visually at a typical desktop size and a small window. Check pointer and keyboard operation, focus visibility, modal focus, label legibility, and the distinction between completed, available, locked, and preview missions.

## Out of Scope

- Playable scenarios for Missions 2–5 and any additional production missions.
- The results screen and its Continue button.
- The in-mission Escape menu and return-to-menu action.
- Save games and campaign progress across launches.
- Additional languages and translations.
- Multiplayer gameplay, networking, and a multiplayer lobby.
- Changes to fleet, combat, capture, or victory rules.

## Further Notes

- In the initial release, a Mission 1 victory changes its completion marker for the session. Once Mission 2 ships, a Mission 1 victory in that session can unlock it and trigger the first reveal on the mission map.
- Playable-menu testing is the proposed verification approach.
