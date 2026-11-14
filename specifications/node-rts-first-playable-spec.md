# Build the first playable Node RTS space scene

> **Superseded in part by [Darts and progressive fleet battles](dart-battle-spec.md).** The basic unit this document calls a ship is now a Dart. A hostile arrival at a defended object no longer resolves immediately: it starts a battle that removes one Dart from each side at a time over one to five seconds, can be reinforced, and pauses production and capture at that object. The survivor, tie, ownership and capture rules below still describe how a battle ends. Blue's victory also waits for a Red fleet that has arrived and is still fighting. The text below is kept as written.

## Problem Statement

I want to build a 2D space strategy game in Unity 6 with Claude Code. The core is a Node RTS about moving fleets between planets, moons, and eventually space stations. The longer-term design also takes inspiration from *Swords & Souls: Neverseen*. Before deciding how that inspiration fits into progression, I want a playable scene that proves the basic strategy loop.

## Solution

Create one playable 2D space scene with exactly two planets and one moon. Blue is the human player. Blue and Red each begin with one planet and 30 ships. Red automatically produces ships and defends on arrival. Blue can select an owned object, send its entire stationed fleet to another object, and watch the fleet travel, fight, or capture. Blue wins once it owns both planets and Red has no ships in transit. Moon ownership is outside the victory check.

## User Stories

1. As a player, I want to see two planets and one moon in a single view, so that I can read the whole map at a glance.
2. As the Blue player, I want to start with one planet and 30 ships, so that I can make a move immediately.
3. As a player, I want Red to start on the other planet with 30 ships, so that I can see what I need to overcome.
4. As a player, I want the moon to start neutral and empty, so that I can expand without an initial battle.
5. As a player, I want ownership to be visible at a glance, so that I can distinguish Blue, Red, and neutral objects.
6. As a player, I want ships to appear as dots orbiting owned objects, so that I can see where fleets are stationed.
7. As a player, I want a numeric fleet count on each owned object, so that I can judge battles and see production.
8. As a player, I want an owned planet to produce one ship each second, so that I can build a fleet by waiting.
9. As a player, I want an owned moon to produce one ship every two seconds, so that capturing it increases my income.
10. As a player, I want Red to stay at its planet and grow its defense, so that I can learn the controls at my own pace.
11. As a player, I want to click an owned object and see it highlight, so that I know which fleet I’m about to command.
12. As a player, I want my second click to send all ships currently stationed at the selected object, so that orders are quick and predictable.
13. As a player, I want an empty launch point to stay mine and keep producing, so that I can rebuild its fleet after sending everything.
14. As a player, I want to see fleets move toward their targets, so that I can track where my ships are.
15. As a player, I want longer journeys to take more time, so that the layout affects my decisions.
16. As a player, I want friendly arrivals to merge with stationed fleets, so that I can concentrate forces before attacking.
17. As a player, I want to see a battle resolve when my fleet reaches a hostile object, so that I can tell what happened to both fleets.
18. As a player, I want combat to remove one ship from each side until one side runs out, so that I can calculate the expected survivors.
19. As a player, I want a successful attack to leave the surviving ships at the target, so that I can see what fleet remains to secure it.
20. As a player, I want an unsuccessful attack to leave the defender in control with its surviving ships, so that the result follows the visible counts.
21. As a player, I want a tie to leave the defender in control with zero ships, so that an equal attack has a predictable outcome.
22. As a player, I want an unopposed landing on the moon to preserve the whole fleet, so that I can expand without combat losses.
23. As a player, I want to see five seconds of capture progress after an unopposed landing or a winning attack, so that I know when control will change.
24. As a player, I want production to start when capture completes, so that I can tell when a newly acquired object begins helping me.
25. As a player, I want rival fleets to fight immediately if they reach the same object during capture, so that I can interrupt an unfinished capture.
26. As a player, I want to win after securing both planets once the remaining Red fleets in flight are gone, so that the map and match result agree.
27. As a player, I want the victory check to allow any moon ownership, so that I don’t need to clear every object on a future map.
28. As a player, I want a surviving fleet to get one last chance to capture an object after I lose my last base, so that the match stays live while I can recover.

## Implementation Decisions

- **Map and presentation.** Use the existing Unity 6 2D project. Show all three stationary objects on one screen. Use blue and red fleet dots, a distinct neutral treatment, a selection outline, visible fleet counts, and a capture timer or progress bar. Counts are authoritative. Dots may represent larger fleets in aggregate to keep the map readable. Show a brief arrival battle effect and the resulting count.
- **Initial state and production.** Both planets start owned with 30 stationed ships each. The moon starts neutral with zero ships. Planets add one whole ship every second and the moon adds one whole ship every two seconds while owned and outside capture. Starting objects start their production clocks at match start. Newly captured objects start a fresh interval on capture completion. Expose production rates as node configuration so later upgrades can change them. Red issues no fleet orders in this milestone.
- **Orders and transit.** A click on a Blue object selects it. With a source selected, clicking a different object sends every ship currently stationed at the source and clears the selection. Clicking the source again cancels selection. Empty sources cannot launch. Any pair of objects has a direct route at a common fleet speed, so travel time equals distance divided by speed. Crossing fleets pass in open space. Resolve arrivals against the destination state at the time of arrival, including ships produced while the fleet traveled.
- **Arrival and combat.** An allied arrival adds its ships to the stationed fleet. An unopposed neutral arrival starts capture with all arriving ships intact. A hostile arrival resolves immediately. If 30 ships attack 20 defenders, 10 attackers remain and begin capture. If 20 attack 30, 10 defenders remain and retain control. A 20–20 tie leaves the incumbent owner with zero ships. A hostile arrival at an empty owned object wins with its full fleet and begins the same capture countdown.
- **Capture.** Use a five-second countdown for both neutral landings and successful attacks. The captor’s survivors stay at the target during the countdown. Formal ownership changes at completion, and production is paused throughout the countdown. The capture display identifies who is claiming the object. An arriving fleet allied with the captor reinforces it without restarting the clock. A rival arrival fights the capturing fleet immediately. If the captor wins, its timer continues. If the rival wins, it starts its own five-second timer to claim the object, or restores control immediately if it is the incumbent owner. A tie clears the capture, leaving the object neutral if it was neutral or under its prior owner if it was owned. An incumbent that retains or restores control resumes production.
- **Match result.** Blue wins once both planets are formally Blue and there are no Red fleets in transit. Evaluate the result after capture and arrival updates. Moon ownership does not enter the victory check. A side that loses its last object remains alive while it has a surviving fleet in transit or actively capturing an object. Blue loses once it owns no objects and has no surviving fleets. The passive Red setup makes the in-transit victory condition and Blue defeat unreachable through ordinary play. Verify them with controlled scenarios.
- **Gameplay boundary.** Keep one match simulation responsible for fleet orders, elapsed time, arrivals, capture, production, and match results. The scene sends player orders to that boundary and displays the resulting state. A ship dot is presentation rather than an independently simulated combatant.

## Testing Decisions

- Use the public match boundary as the primary automated test seam. Exercise the same fleet order and time advancement that the scene uses, and assert visible game outcomes such as selection-independent order validity, ship counts, ownership, capture progress, and match status. Avoid testing internal timers, rendering objects, or private methods. The Unity starter project has no existing gameplay test suite to extend.
- Verify the initial map and fleet counts, whole-ship production cadence, Red’s passive behavior, full-fleet launch, continued source production at zero ships, distance-based travel, and allied merging.
- Test hostile wins, losses, ties, and attacks against empty objects. Check that defenders produced during travel count in battle, that capture starts with the surviving fleet, that ownership and production wait until five seconds elapse, and that a neutral landing costs zero ships.
- Use controlled match states to cover interrupted capture, reinforcements, neutral ties, Blue’s last-fleet recovery, and a Red fleet in transit delaying victory. Test the victory rule with a neutral moon.
- In Unity Play Mode, click through one full match. Check selection and highlighting, visible fleet counts and moving dots, capture feedback, and the victory display. Demonstrate the opening by taking the moon, combining forces, and capturing Red’s planet against the growing garrison.

## Out of Scope

- Red AI, Red-issued fleet orders, multiplayer, and combat between fleets in open space.
- Space stations, extra maps, ship types, tactical battle controls, and detailed ship art.
- Upgrades, training, character progression, and other mechanics inspired by *Swords & Souls: Neverseen*.

## Further Notes

- The moon is optional in the victory rule. Under the specified symmetric opening, a single direct attack from Blue’s starting planet can never outnumber Red at arrival. Both begin with 30 ships, both produce at the same rate, and Red keeps producing during travel. The intended opening is to use the moon’s production and merge fleets before attacking.
- Implementation defaults for this milestone include deselection after launch, capture progress and production behavior during a contested takeover, and retaining the match while a surviving fleet is actively capturing. These make the approved rules concrete for implementation and testing.
