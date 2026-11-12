# Darts and progressive fleet battles

## Problem Statement

The basic space fighters are currently called ships, and hostile fleet arrivals resolve combat instantly. Travelling dots disappear and the outcome appears in orbit, followed by brief effects. Players cannot follow a battle, see the defenders prepare, or send reinforcements while fighting is underway.

Call each basic unit a Dart and make battles unfold over time. Preserve the predictable one-for-one loss rule while making arrival, positioning, firing, destruction, and the return to orbit feel continuous.

## Solution

Use Dart consistently for the small unit produced on planets and moons; retain fleet for a group of Darts. At a defended hostile destination, defenders gather toward the approaching fleet, which flows into a nearby opposing formation at its normal arrival time. Show moving Darts, particle shots, progressive explosions, and spatial battle sounds throughout combat.

Apply real paired casualties over approximately one to five seconds according to the numerical balance. Reinforcements join ongoing combat and change its remaining duration. Pause capture and local production while fighting. Keep exact counts authoritative while retaining the current compressed dot representation for large fleets.

## User Stories

1. As a player, I want the basic unit called a Dart everywhere, so that unit names are consistent.
2. As a developer, I want relevant unit identifiers and documentation to use Dart, so that the code and game vocabulary agree.
3. As a player, I want fleet to continue describing a group of Darts, so that group orders remain understandable.
4. As a player, I want heavily outnumbered forces to lose quickly and balanced forces to fight longer, so that battle pacing reflects the opposition.
5. As a player, I want equal-strength battles to have the same duration regardless of absolute fleet size, so that the timing rule is predictable.
6. As a player, I want both sides to lose actual Darts progressively, so that the displayed battle reflects the live simulation.
7. As a player, I want losses to remain one-for-one, so that combat outcomes remain calculable.
8. As a player, I want defenders to gather toward an incoming hostile fleet, so that they visibly prepare for its arrival.
9. As a player, I want travelling Darts to flow into combat without teleporting, so that arrival feels continuous.
10. As a player, I want two nearby formations facing each other near the arrival point, so that I can identify the opposing forces.
11. As a player, I want some Darts to move while particle shots and explosions play, so that combat does not look static.
12. As a player, I want shots and destruction sounds spread through the battle, so that sound matches the visible action.
13. As a player, I want survivors to return smoothly to ordinary orbit, so that battle completion is visually coherent.
14. As a player, I want both sides' remaining Dart counts and a fighting indicator, so that I can read the current contest.
15. As a player, I want incoming reinforcements to join their side, so that an unresolved battle can change while I play.
16. As a player, I want reinforcements to change the remaining battle duration without restoring losses, so that prior combat still matters.
17. As a player, I want the contested object's production paused, so that it does not generate combatants while fighting.
18. As a player, I want to keep issuing orders elsewhere while engaged Darts remain committed, so that other strategic activity continues.
19. As a player, I want capture progress visibly paused during fighting and resumed if my capturing force survives, so that interruption does not silently erase my progress.
20. As a player, I want undefended arrivals and capture to retain their current behavior, so that peaceful expansion does not acquire an extra delay.
21. As a player, I want pause to freeze combat and its effects, so that no casualties occur while I am paused.
22. As a player, I want results to count actual Dart casualties at the time they occur, so that the final statistics match the battle.
23. As a player, I want large fleets to remain readable and responsive, so that visual density does not obscure the battle or overwhelm audio.

## Implementation Decisions

### Terminology and compatibility

- Rename the basic unit to Dart, plural Darts, across player-facing text, relevant simulation and presentation identifiers, configuration, events, comments, tests, and maintained documentation. Results labels become Darts destroyed and Darts lost. Fleet retains its current meaning.
- This specification documents the future rename; the specification PR does not rename runtime code or rewrite older specifications. During implementation, update maintained documentation and explicitly identify superseded behavior. This specification supersedes earlier instant-combat requirements and ship terminology for the basic unit.
- Preserve configured counts, production intervals, travel speeds, ownership rules, mission content, and existing player identities. If renamed serialized fields or assets require migration, preserve their configured values. Do not introduce detailed fighter models or independent fighter physics.

### Authoritative battle state

- Keep the existing public match simulation as the authority for time, orders, arrivals, production, capture, combat, casualty totals, and results. Add an explicit active battle at a contested object rather than resolving the outcome immediately on arrival.
- A battle contains exactly two opposing faction identities and their current Dart counts. Identify the stationed side by the actual occupant, including a capturing force, rather than formal ownership. Neutral guards can remain the stationed opponent under existing accounting rules.
- Both sides must have positive counts to start a battle. Friendly arrivals outside combat retain normal merging behavior; hostile arrivals against an empty destination retain existing capture or control-restoration behavior without a battle delay.
- On battle start, move the arriving force into battle state without losing its faction, count, or visual continuity. Expose enough public state or events for presentation to show both sides, casualties, reinforcement, and completion. Do not infer authoritative counts from rendered dots or particle hits.
- Each casualty step removes one Dart from each side simultaneously. End the battle when at least one count reaches zero, then apply survivor, tie, ownership, and capture rules. Do not pre-apply the eventual casualties or declare the winner at battle start.
- Include surviving Darts in active battles in faction totals and survival checks. An attacking faction must not disappear from survival accounting when its travelling fleet becomes an engaged force. Preserve existing victory objectives, but do not conclude a mission on the basis that an unresolved hostile battle no longer counts as a fleet in transit.

### Duration and casualty scheduling

- At battle start, calculate the numerical ratio from the larger positive count divided by the smaller. Equal counts target five seconds; ratios of ten-to-one or greater target one second. Intermediate ratios produce a smooth duration between those endpoints. Swapping attacker and defender counts does not change duration.
- Duration depends on ratio rather than absolute count: 10 versus 10 and 100 versus 100 both take five seconds without reinforcement. Likewise, 1 versus 1 lasts five seconds, with its single casualty pair at the end.
- Spread the smaller force's casualty pairs evenly across the calculated duration, with the final pair at the end. For 100 versus 10, remove ten paired casualties over one second, leaving 90 attackers. The reversed matchup leaves 90 defenders. Equal forces eliminate each other at the end of five seconds.
- Hold the schedule fixed between reinforcement arrivals. Do not recalculate duration after every casualty as the ratio changes. Use simulation time and exact integer casualty counts; rendering frame rate must not change the result.
- On reinforcement, retain every prior casualty, add the arriving Darts to their faction's remaining force, and recalculate a fresh remaining duration of one to five seconds from the updated counts. Schedule the remaining casualty pairs over that interval. The total battle may consequently exceed five seconds or continue while reinforcements keep arriving.
- Initial implementation assumption: interpolate duration linearly using the smaller-to-larger count fraction, clamped between 0.1 and 1.0. This yields one second at 0.1 and five seconds at 1.0. Keep timing endpoints and the curve configurable for later tuning; the precise interpolation was not selected in the interview.

### Reinforcements and event ordering

- Fleets belonging to either of the two fighting factions join that side immediately at their normal arrival time. Their visible approach and merge remain smooth even though their count becomes authoritative immediately.
- Reinforcement never restarts the battle presentation, restores casualties, or grants ownership. Adjust the existing formations and remaining casualty schedule.
- At the same simulation instant, process arrivals before any casualty pair already due at that instant. A reinforcement must not postpone or cancel that already-due pair: incorporate arrivals, apply the due pair against the updated forces, then schedule any remaining combat using the updated surviving counts. If that pair ends combat, apply the normal outcome instead of creating a new schedule.
- Batch same-instant reinforcements before computing the replacement schedule, so arrival processing does not create intermediate schedules that affect the outcome. Retain deterministic ordering for arrivals that establish a new battle.
- Preserve existing production and capture-completion ordering before arrivals at the same instant. A Dart produced exactly at the initial hostile arrival participates; a capture completing exactly then completes before that arrival. While combat is active, neither production nor capture can complete.
- Resolve all due simulation events even when one time advance spans multiple casualty steps, reinforcement arrivals, battle completion, and subsequent capture. Splitting the same elapsed time into different frame sizes must produce equivalent public outcomes.

### Production, capture, and orders

- Pause production on the contested object for the full active battle, preserving partial production progress. If its incumbent retains or restores control, resume from that progress. A newly completed capture retains the existing fresh production interval rule. No production catches up for time spent fighting or capturing.
- Pause a current capture clock during combat and display its retained progress as paused. Resume it if the same capturing faction survives. If a different non-owner wins, begin its full capture duration from zero. If the incumbent owner wins, restore control immediately without requiring capture.
- Attacker survivors at an ordinarily defended hostile object begin the existing capture countdown after combat ends. Defender survivors retain control. A tie leaves zero Darts, clears any capture, and retains formal ownership, including neutrality where applicable.
- Survivors and capture resume immediately in simulation when combat ends; the visual transition back to orbit does not add another gameplay delay. Preserve friendly reinforcement of an uncontested capture without resetting its progress.
- Reject launch orders from an actively contested object, including attempts through the simulation boundary. Retain the current restriction on launching a capturing force. Travelling fleets cannot be retargeted, engaged forces cannot retreat, and capture cannot be abandoned in this feature. Orders from other eligible objects remain available.

### Formation and effects

- Gather defenders in the direction of the approaching enemy during the final portion of travel. At arrival, defenders occupy a nearby inner orbital formation and attackers an adjacent outer formation, facing each other across a visible gap. Do not place them on opposite hemispheres of the object.
- Preparation is presentation only: it does not pause production or capture, reserve defending Darts, disable otherwise valid orders, cause early casualties, or alter travel duration. If the destination changes before arrival, reconcile with its actual state and blend to the appropriate orbit or battle formation.
- Preserve positional continuity from transit into battle and from battle back to orbit. Move existing dot representations or blend them continuously rather than removing the travelling swarm and spawning unrelated dots at a different position. Apply the same continuity to reinforcements.
- Use the current small dot style. Animate some combatants with bounded local movement while keeping the two fronts readable. Emit particle shots toward the opponent, progressive casualty explosions, and matching sounds throughout combat. Visual shots do not add accuracy, dodging, range, projectile damage, or an alternative casualty model.
- Retain existing dot compression for large fleets. Counts remain exact, but representative dots and explosions illustrate aggregated casualties; one visible dot per actual Dart is not required. Small fleets should visibly lose individual representative dots. Do not erase all visible combatants at battle start or defer all destruction effects until battle completion.
- Reuse existing pooled effects, spatial audio, voice limits, settings, and mixer behavior where practical. Aggregate or limit simultaneous effects and sounds at large counts rather than requiring an audible shot or explosion for each real Dart. Effects must not extend or alter authoritative combat.
- Show both faction identities/colors, their current remaining Dart counts, and a clear fighting state at the contested object. Retain readable paused capture progress when applicable. Omit a battle countdown because reinforcement can change the remaining duration.

### Pause, statistics, and lifecycle

- Existing pause behavior freezes battle time, casualties, approach and battle motion, particles, and queued gameplay audio. Resume without accumulating paused time or bursting through missed casualties. Continue honoring existing sound settings and background muting.
- Record actual paired casualties when they occur, once each. Credit each side's Darts lost and the opponent's Darts destroyed using the fighting faction identities, including a captor that differs from the formal owner. Retain neutral-guard attribution rules and separate destroyed/lost counters.
- Mission results include the casualties that caused the outcome and remain a stable snapshot. Stop authoritative battles and combat effects/audio when gameplay ends; existing permitted idle cosmetic animation behind results can continue.
- Restarting a mission or returning to the menu clears active battles, schedules, formation transitions, particles, and queued combat sounds so nothing leaks into another attempt.

## Testing Decisions

- Prefer the existing public match boundary: configure controlled scenarios, issue fleet orders, advance simulated time, and assert public Dart counts, battle state, ownership, capture progress, order validity, totals, and match results. Extend the existing match, result-accounting, and pause test patterns. Do not test private timer layout, individual particle trajectories, random sound choices, or internal helper methods.
- Check progressive losses before completion, no losses before the first scheduled pair, correct final survivors, and simultaneous elimination on a tie. Cover 100 versus 10, 10 versus 100, equal 10 and equal 100, 1 versus 1, intermediate ratios, and ratios beyond ten-to-one. Check the selected configurable curve's endpoints and symmetry.
- Verify a single long time advance and many shorter advances produce the same counts, casualty totals, battle state, capture state, and result. Include an advance that crosses battle completion and part of the resumed capture or production interval.
- Reinforce each side separately and both sides at the same instant. Assert immediate count changes, permanent previous losses, recalculated remaining duration, and potentially extended overall battle time. Include reinforcement at an already-due casualty instant and at the otherwise final pair; confirm the due pair still occurs and no premature outcome is emitted.
- Verify production and capture remain frozen during combat, pre-arrival production counts, preserved partial production, retained capture progress for a surviving captor, a fresh timer for a replacement captor, immediate owner restoration, and ownership-preserving ties. Include the existing same-instant production/capture/arrival ordering.
- Verify friendly merging, empty hostile arrival, unopposed neutral capture, normal capture duration, route travel duration, and distant orders retain their behavior. Assert contested sources reject launches and existing capturing sources remain unable to launch.
- Verify an engaged last force remains alive, unresolved hostile battles prevent premature mission completion, casualties are attributed to the occupant rather than formal owner, neutral guard totals remain correct, and final results freeze after the concluding casualty.
- Verify pausing midway through approach, combat, and a reinforced battle freezes simulation and gameplay effects/audio without catch-up on resume. Verify restart and menu navigation clear old battle activity and totals through existing lifecycle seams.
- In Unity Play Mode, review approach from different directions, short routes, arrival into formation, defenders that launch before arrival, moving combatants, particle shots, gradual explosions and sounds, reinforcement joins, compressed large fleets, ties, and survivors returning to orbit. Check both live counts, faction identification, fighting feedback, paused capture readability, audio settings, and pause/resume. Verify continuity visually rather than asserting private rendering structures.
- Review the rename across visible labels, relevant code/configuration, and maintained documentation. Verify serialized configuration values survive any necessary migration and the project compiles when the feature is implemented. This specification-only change requires documentation review rather than running gameplay tests against unchanged code.

## Out of Scope

- Detailed fighter models, new unit classes, independent fighter entities or physics, tactical targeting, hit probability, dodging, range, or altered one-for-one casualty math.
- Three or more players participating in the same battle, alliances, multiple players sharing a combat side, third-faction arrival arbitration, or a generalized multiplayer combat system. Separate two-sided battles elsewhere on the map remain possible.
- Retreat, withdrawal, retargeting or cancelling a travelling fleet, abandoning capture, or launching engaged Darts elsewhere.
- Battles in open space, route interception, changed fleet speeds, an additional preparation phase that consumes gameplay time, or changed peaceful capture rules.
- One rendered dot or sound for every Dart in arbitrarily large fleets, a battle countdown, new mission content, or changes to campaign objectives and progression.
- Implementing runtime code, modifying assets, or publishing a separate issue as part of this specification PR.

## Further Notes

- Product decisions were agreed through the specification interview. Preserve those decisions if timing or visual parameters are tuned later; changes to combat math, capture rules, or command scope require separate agreement.
- Assumptions requiring implementation validation: the initial interpolation described above; the duration of visual gathering and return-to-orbit blends; local movement amplitude; particle density; audio cadence; and whether existing pooling/compression remains responsive at representative fleet sizes. Validate without adding preparation time, hiding live counts, or changing exact losses.
- Existing public match tests are the proposed primary behavior seam, supplemented by existing pause/result tests and Play Mode presentation review. Reuse those boundaries rather than introducing a separate combat engine solely for testing.
- Third-faction participation is deliberately unresolved. Do not silently invent a priority, queue, alliance, or damage-sharing rule. Validate this milestone using at most two combatant identities at any one object; a later specification must define third-party arrivals before such scenarios are supported.
