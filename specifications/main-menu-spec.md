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
17. As a player, I want left and right arrows when the campaign grows beyond five missions, so that I can browse the whole campaign.
18. As a player, I want a page indicator and a clear end to the route in either direction, so that I know where I am while browsing.
19. As a player, I want a Back action on the map and in Settings, so that I can return to the main menu.
20. As a player, I want Multi Player to appear disabled, so that I can see it is planned without opening an unfinished screen.
21. As a player, I want General, Graphics, and Sound sections, so that I can find settings by category.
22. As a player, I want General to show English as the available language, so that I can see which language the game supports.
23. As a player, I want to choose a supported display mode, resolution, and VSync setting, so that I can configure the game for my display.
24. As a player, I want separate Master, Music, and Sound Effects volume controls, so that I can set the audio balance I prefer.
25. As a player, I want my settings to carry over when I launch a mission or reopen the game, so that I can keep using my preferences.
26. As a player, I want Quit to ask for confirmation, so that I can recover from an accidental selection.
27. As a player, I want Cancel to dismiss the prompt and Confirm to close the game, so that either choice has a predictable result.

## Implementation Decisions

- **Startup and navigation.** A main menu scene is the game's entry point. Its views are the main menu, mission map, and Settings. Quit opens a modal over the main menu. Provide visible mouse hover and keyboard focus. Escape backs out of the map or Settings and cancels the modal. While the modal is open, confine interaction to it and focus Cancel by default.
- **Visual direction.** Use a deep space backdrop, restrained star and orbit details, and a high-contrast accent for available actions. Keep the main menu hierarchy simple. On the mission map, keep connectors behind the dots and show availability through icons and text as well as color. Keep labels, dots, and arrows legible and unobstructed across supported desktop sizes.
- **Mission catalog.** Use one ordered catalog with stable mission IDs and optional gameplay scene references. Derive the displayed Mission X label from the mission's position in the catalog. Mission 1 uses the existing first playable scenario. Missions 2–5 have no launchable scenes in this milestone. A mission is launchable when its scene is included in the build and its immediate predecessor is complete. Mission 1 has no prerequisite. Guard against repeated activation while a scene load is in progress.
- **Progress.** Track completion by stable mission ID in memory for the current game session. Record it when the active campaign mission reports victory through the existing match result boundary. Defeat, leaving the scene, and restarting a match before victory do not advance progress. Replaying a completed mission does not replay an already acknowledged unlock. Keep campaign progress separate from gameplay simulation and settings. A fresh launch starts with no completed missions or pending reveals.
- **Preview and unlock states.** A mission with no launchable scene shows a locked preview with a Coming Soon label. A ready scene behind an incomplete predecessor shows a lock and identifies that predecessor. Available and completed missions are both clickable. When a ready mission first meets its prerequisite, retain its pending reveal for the session until the map shows it. On entry to the map, bring the relevant page into view and briefly animate the connection and lock-to-available transition. If the player leaves before the reveal completes, show it again on the next visit in the same session.
- **Pagination.** Show at most five missions per page. At six or more, show left and right arrows and the current page number. Disable navigation past the first and last pages. A route cue at a page boundary should make it clear that missions on the next page continue the same prerequisite chain. Adding a catalog entry requires no menu layout or pagination changes.
- **Results integration.** The menu supports opening directly to the campaign map when another scene requests it. The future results screen's **Continue** button will use that entry point. This milestone records victory and supports the map's reveal when reached, while leaving the existing gameplay result interaction in place.
- **Settings.** General contains only Language, with English selected as the sole option. Graphics contains Display Mode, Resolution, and VSync where supported. Offer valid display choices and revert a display change if the player cannot confirm it. Sound contains Master, Music, and Sound Effects volume. Apply audio controls to existing music and gameplay effects, including victory cues, and preserve the current mix at default values. Keep applied preferences across scene changes and persist them locally between launches, independently of session-only campaign progress.

## Testing Decisions

- Use the playable menu as the primary test seam. Exercise controls through the same paths players use and assert visible state, scene transitions, and behavior after reopening the game. The existing Edit Mode match tests set the precedent for testing public behavior and already cover what constitutes a Blue victory.
- Start with a clean profile. Verify the main menu, the disabled Multi Player control, the five connected labels, all four locked previews, and the absence of pagination. Launch Mission 1 and verify that it is the existing Blue versus Red scenario.
- Win Mission 1, reopen the map in the same session, and verify the completion marker, replay access, and locked previews. Relaunch the game and verify a fresh campaign. Use a loss or early exit to verify that neither records completion.
- Use a test catalog with ready scenarios and at least six entries to verify locked prerequisites, sequential unlocks, both arrow directions, the disabled end arrows, and an incomplete final page. Complete Mission 1 with Mission 2 ready and verify that Mission 2 becomes available with one reveal. Test a reveal across a page boundary. Exercise the map entry point directly until the results screen exists.
- Verify that General shows only Language and offers only English. Change Graphics and Sound preferences, launch a mission, and relaunch the game. Check that the preferences carry over while the campaign starts fresh. In a desktop build, check supported display changes and reversion, independent Music and Sound Effects volume, and Master volume affecting both. Check that Cancel dismisses Quit and Confirm closes the game.
- Review the menu visually at a typical desktop size and a small window. Check pointer and keyboard operation, focus visibility, modal focus, label legibility, and the distinction between completed, available, locked, and preview missions.

## Out of Scope

- Playable scenarios for Missions 2–5 and any additional production missions.
- The results screen and its Continue button.
- Save games and campaign progress across launches.
- Additional languages and translations.
- Multiplayer gameplay, networking, and a multiplayer lobby.
- Changes to fleet, combat, capture, or victory rules.

## Further Notes

- In the initial release, a Mission 1 victory changes its completion marker for the session. Once Mission 2 ships, a Mission 1 victory in that session can unlock it and trigger the first reveal on the mission map.
- Playable-menu testing is the proposed verification approach.
