# Squadra mission results and campaign completion

## Problem Statement

When a mission ends, I see Victory or Defeat over a faded battlefield, but I cannot review each player's combat results or choose where to go next. I want to see the mission duration, ships destroyed, and ships lost, then continue, retry, or return to the mission map. Finishing the campaign should feel like a conclusion and thank me for playing.

## Solution

Replace the current click-anywhere replay interaction with a centered results panel over the existing faded battlefield. Keep cosmetic animation running while gameplay and statistics remain frozen. Show the outcome, mission duration excluding pauses, and a results table for every participating player. Provide Next Mission, Restart, and Back to Menu, with Next Mission available only after victory when the immediately following catalog mission is playable.

When there is no following catalog mission, omit Next Mission. Winning that final mission shows a celebratory Campaign Complete! panel thanking the player, with the same statistics and the remaining two actions. Losing it shows the normal Defeat panel.

## User Stories

1. As a player, I want a clear Victory or Defeat heading, so that I immediately understand the outcome.
2. As a player, I want the faded battlefield to remain behind the results, so that the outcome stays connected to the mission I played.
3. As a player, I want cosmetic animation to continue while gameplay stops, so that the screen feels alive without changing the result.
4. As a player, I want to see mission duration excluding pauses, so that I can judge how long I spent actively playing.
5. As a player, I want a row for every participating player, so that I can compare combat results across the mission.
6. As a player, I want my row first and each player identified by name and color, so that I can read the table easily.
7. As a player, I want eliminated players and players with zero totals to remain visible, so that the table represents the complete mission roster.
8. As a player, I want Ships destroyed to count individual enemy ships I destroyed, so that the number reflects combat rather than fleets or captured objects.
9. As a player, I want Ships lost to include attacking and defending casualties, so that I can see all my combat losses.
10. As a player, I want casualties attributed to the ships' player during a capture, so that ownership changes do not misattribute combat results.
11. As a player, I want destroying neutral guard ships to count toward my destroyed total, so that clearing guarded objects is represented.
12. As a player, I want losses to neutral guards to count toward my losses without crediting another player, so that the totals reflect who actually fought.
13. As a player, I want the final totals to stop changing once the mission ends, so that I can review a stable result.
14. As a victorious player, I want Next Mission to launch the immediately following playable mission directly, so that I can continue without visiting the map.
15. As a defeated player, I want Next Mission disabled, so that this attempt cannot advance me through the campaign.
16. As a player, I want an unimplemented next mission to remain visible as a disabled action with an explanation, so that I understand why I cannot continue yet.
17. As a player, I want Next Mission omitted when no following mission exists, so that the screen does not offer a nonexistent destination.
18. As a player, I want Restart to begin a fresh attempt at the same mission, so that I can try again with fresh time and statistics.
19. As a player, I want Back to Menu to open the mission map, so that I can review campaign progress and choose a mission.
20. As a player, I want victories retained for the current session when navigating or restarting, so that I keep access to completed and unlocked missions.
21. As a player, I want a celebration and thank-you message after winning the final catalog mission, so that completing the campaign feels rewarding.
22. As a player, I want statistics on the campaign completion screen too, so that I can review my final mission.
23. As a player, I want mouse and keyboard operation with visible focus, so that I can choose an action reliably.
24. As a player, I want background clicks to do nothing, so that I do not accidentally restart while reviewing results.
25. As a designer, I want statistics collection and display to support mission rosters of two through six players, so that later factions do not require redesigning the results screen.

## Implementation Decisions

- **Existing behavior and boundaries.** The match simulation already owns elapsed time, resolves combat, and stops advancing once the match ends. The presentation continues to animate the battlefield and displays a faded result overlay. Replace the replay prompt and global restart activation with explicit results controls. Preserve existing combat and victory rules for the shipped Blue-versus-Red scenario.
- **Results snapshot.** Capture the final elapsed gameplay time and per-player counters at the existing match-result boundary, including the combat event that causes the outcome. Present one stable snapshot for that attempt. Do not derive losses from remaining fleet sizes, production totals, ownership changes, or visible ship dots. Do not accumulate results across retries or campaign missions.
- **Player roster.** Represent results as a collection keyed by stable player identity rather than separate Blue and Red fields. Include player display name, color, ships destroyed, and ships lost. Support 2–6 participating players in collection and presentation, including eliminated players and zero totals. Put the human player first and preserve mission roster order for the rest. The initial mission still has Blue as the human player and Red as its opponent. Avoid inferring participants solely from surviving objects or fleets. Providing additional playable factions, AI, or generalized victory conditions is separate work.
- **Combat accounting.** At each resolved combat, add each combatant's actual casualties to that combatant's Ships lost, and credit those casualties to the opposing combatant's Ships destroyed when the opponent is a participating player. Attribute ships by the actual arriving or stationed combatant, including a capturing force, rather than the object's formal owner. Count both attack and defense, ties, failed attacks, and interrupted captures. Friendly merging, reinforcement, production, unopposed landing, and capture without casualties change neither counter. Each combat event is counted once.
- **Neutral guards.** Neutrals are not participating players and have no results row. A player destroying neutral guard ships earns Ships destroyed credit. A player losing ships to guards gains Ships lost without crediting any other player. Keep this accounting rule compatible with future neutral defenders; do not add guarded neutral objects to production missions in this feature.
- **Current combat symmetry.** Current combat removes one ship from each side per exchange. Therefore a player's Ships destroyed and Ships lost may match under the present rules. Retain both distinct counters and labels; do not define one as an alias of the other or assume all future combat remains symmetric.
- **Duration.** Measure from the start of active mission simulation to its terminal outcome, excluding paused intervals, scene loading, and time spent viewing results. Reuse the simulation's elapsed time rather than a separate wall clock. Display whole elapsed seconds in the existing minutes:seconds style, allowing minutes to exceed 59. Reset elapsed time on every new attempt. This feature does not implement pausing; the accounting must remain compatible with a later pause system.
- **Normal results layout.** Center a readable panel with Victory or Defeat, mission time, and a table headed Player, Ships destroyed, and Ships lost. Names and numbers must remain legible over the faded backdrop at supported desktop sizes. Show player names as well as colors. Fit six rows without truncating statistics or obscuring controls. Hide obsolete gameplay hints and the click-to-play-again prompt. Cosmetic animation may continue, but production, fleet travel progression, combat, capture, player orders, and the final totals must remain stopped.
- **Actions and availability.** Order actions as Next Mission, Restart, Back to Menu. The next mission is the immediate successor to the active mission in the ordered campaign catalog; never skip an unimplemented mission to find a later playable one. Playability uses the existing campaign readiness and prerequisite rules, including a scene included in the build. Record the victory before determining availability. Use the following states:

  | Current result | Following catalog mission | Next Mission |
  | --- | --- | --- |
  | Victory | Playable | Visible and enabled; launches it directly |
  | Victory | Not implemented or otherwise not launchable | Visible and disabled; hover tooltip: Next mission coming soon |
  | Defeat | Exists, whether playable or not | Visible and disabled; hover tooltip: Win this mission to continue |
  | Victory or defeat | Does not exist | Hidden; no tooltip or empty button slot |

- **Final mission variant.** The final mission is the last entry in the campaign catalog, not the last currently implemented scene. Victory there uses the heading Campaign Complete! and the message Thank you for playing Squadra!, with a celebratory visual treatment within the results panel. Retain the faded animated battlefield, mission time, and per-player table. These are statistics for this final attempt, not campaign-wide totals. Show Restart and Back to Menu only, with Back to Menu initially focused. Defeat on that same mission retains the normal Defeat treatment with those two buttons. Winning Mission 1 while Missions 2–5 remain catalog previews is an ordinary victory with a disabled Next Mission button, not campaign completion.
- **Navigation and progress.** Next Mission starts its target through the campaign launch flow with the correct active mission identity and fresh statistics. Restart reloads the current mission and retains its campaign identity, with fresh time and counters. Back to Menu requests the existing mission-map entry point and clears the active gameplay mission identity when returning to the menu. Retain completed missions and pending unlock reveals for the session; defeat and restart do not grant progress or erase previous victories. Returning to the map uses its existing unlock-reveal behavior. Campaign progress remains in memory only, as in the main menu specification.
- **Input and focus.** Actions take effect immediately without confirmation dialogs. Support pointer activation and standard keyboard navigation with visible hover and focus states. On normal results, initially focus Next Mission if enabled, otherwise Restart. On campaign completion, initially focus Back to Menu. Disabled Next Mission cannot activate, but hovering over it shows the stated reason. Background clicks do nothing. Remove the previous click-anywhere and unconditional Enter, Space, or R restart shortcuts; activation keys operate the focused enabled button. Prevent the input that ends gameplay from immediately activating a result action, and guard repeated activation during scene navigation.
- **Escape ownership.** This feature assigns no results navigation or dismissal action to Escape. The planned pause menu will own Escape behavior in later work. Do not introduce a results-specific Escape binding that would conflict with it.
- **Integration with previous specifications.** This specification replaces the main menu specification's anticipated Continue action with the explicit Next Mission and Back to Menu actions above. Back to Menu uses the previously provided mission-map entry point. Existing settings and session progression behavior continue to apply.

## Testing Decisions

- Prefer externally observable behavior at the existing public match boundary, following the Edit Mode match tests: issue orders, advance simulation, and assert final results. Extend the existing campaign test seam for successor availability and progress. Review the actual results UI for layout, input, tooltips, focus, and scene transitions rather than testing private UI construction details.
- Verify exact destroyed and lost totals for attacking and defending players across an attacker win, defender win, tie, repeated battles, and combat during capture. Include a capture battle where the stationed combatant differs from the object's owner, and verify correct attribution. Verify friendly reinforcement, production, and peaceful capture add no casualties.
- Verify future-compatible neutral combat with a controlled simulation fixture: player losses to guards receive no other player credit, destroyed guards count toward the attacking player's destroyed total, and neutral has no row. This does not require shipping a guarded mission.
- Use controlled result rosters of two and six players to verify independent totals, player identity, deterministic order, eliminated-player retention, and visible zero values. Verify collection with additional player identities without requiring new AI or multiplayer. Ensure no result is lost when a player has no surviving ships or objects.
- Verify that the terminal battle is included exactly once and further time advancement or result rendering changes neither counters nor elapsed time. With paused simulation advancement withheld, verify that elapsed gameplay duration excludes the paused interval. Verify that a fresh attempt starts at zero and that displayed durations remain readable beyond one hour.
- Exercise victory with a ready successor, victory with an unimplemented immediate successor, defeat with a locked successor, and defeat with a previously unlocked successor. Assert visible enabled or disabled state, tooltip text, and actual navigation. Include a catalog where a later mission is ready but the immediate successor is not, and verify that Next Mission does not skip ahead.
- Win and lose the last catalog mission. Verify Next Mission is absent in both cases, the celebration and thank-you appear only on victory, statistics remain mission-specific, and initial focus follows the variant rules. Verify a single-entry catalog behaves as a final mission, while the shipped five-entry catalog does not celebrate completion after Mission 1.
- Activate Next Mission and verify its scene and campaign identity; activate Restart and verify the same mission with fresh statistics. Return through Back to Menu after victory and defeat and verify the mission map, retained session progress, and existing pending unlock reveals. Replay a completed mission and verify defeat or restart does not revoke its completion. A fresh application launch still resets campaign progress.
- Verify background clicks do nothing, disabled actions cannot activate, keyboard activation uses the focused enabled button, and repeated activation initiates only one navigation. Verify a final gameplay click cannot accidentally activate results controls. Confirm this feature adds no Escape binding; actual pause-menu behavior remains outside these checks.
- Visually review victory, defeat, and campaign completion at a typical desktop size and a small supported window, with both two and six rows. Check text contrast, tooltip visibility over a disabled control, readable names and totals, focus visibility, button placement when Next Mission is hidden, cosmetic animation, and the absence of gameplay advancement behind the overlay.

## Out of Scope

- Implementing the feature as part of this specification-only change.
- Additional playable missions, factions, AI, networking, or multiplayer.
- Generalizing combat or victory rules beyond what statistics collection and display require.
- Shipping neutral guards or changing neutral object behavior.
- A pause menu, pause controls, and Escape behavior for that menu.
- Campaign-wide aggregated statistics, rankings, rewards, medals, or results history.
- Persistent campaign progress or statistics across application launches.
- New result confirmation dialogs or changes to fleet production, capture, and combat balance.

## Further Notes

- The agreed product behavior is settled. Implementation should validate that final combat accounting is committed before publishing the terminal results snapshot and that cosmetic updates cannot advance gameplay.
- The current player representation explicitly names Blue and Red. Validate during implementation how to carry a stable 2–6-player roster through statistics without expanding this milestone into additional playable factions or a new victory system.
- Standalone gameplay scenes opened without an active catalog mission need an implementation fallback. Proposed assumption to validate: show normal Victory or Defeat, omit Next Mission and campaign completion, and retain Restart plus Back to Menu to the mission map.
- The existing results animation has a short readiness delay. The exact entrance animation and input-readiness timing are implementation details to validate visually; preserve protection against accidental activation without requiring new player input or confirmation.
