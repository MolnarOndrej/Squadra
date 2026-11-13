# Squadra fleet movement and battles

## Problem Statement

Squadra currently shows stationed ships as orbiting dots and fleets in transit as loose swarms. A hostile arrival resolves in one step and plays a burst effect. Players should be able to follow a fleet from orbit into travel formation, read the direction of incoming threats, and watch two fleets take positions and exchange fire before the survivors regroup.

## Solution

Give fleets distinct movement for patrol, travel, defense, combat, and regrouping. Use smooth 2D paths and readable formations to evoke sci-fi space combat at map scale. Around an occupied world, the defending fleet can patrol, concentrate toward the largest incoming fleet, or divide among incoming directions in proportion to enemy ship counts. The first release includes all three behavior levels and uses Level 2 by default.

## User Stories

1. As a player, I want stationed ships to orbit their world together, so that I can read them as one fleet.
2. As a player, I want ships to peel out of orbit and gather into a travel formation, so that I can follow a departure from the world to the route.
3. As a player, I want traveling ships to keep their relative positions, so that I can track the fleet and its heading at a glance.
4. As a player, I want friendly arrivals to join the ships already at the world, so that reinforcements are visually clear.
5. As a player, I want defenders to move toward a detected threat and hold on its side of the world, so that I can see where they expect contact.
6. As a player, I want a Level 0 garrison to continue its patrol when enemies approach, so that its lack of preparation is visible.
7. As a player, I want a Level 1 garrison to concentrate on the largest approaching fleet, so that I can predict how it reacts to threats from several sides.
8. As a player, I want a Level 2 garrison to cover different directions according to how many ships are approaching from each, so that a small diversion draws fewer defenders than the main attack.
9. As a player, I want defense groups to adjust as new fleets enter awareness range, so that their positions reflect the current threat.
10. As a player, I want opposing fleets to take up separate positions with a clear gap, so that I can see who is shooting at whom.
11. As a player, I want a few ships to break formation, make a firing pass, and return if they survive, so that combat has visible movement while the main formations remain readable.
12. As a player, I want survivors to regroup and return to orbit or cover the next threat, so that I can follow what remains after the fight.
13. As a scenario designer, I want to choose any of the three behavior levels in configuration, so that I can verify each level and later connect it to progression.
14. As a player, I want fleets to keep fighting when enemies arrive from two directions at once, so that I can see how the garrison handles pressure on both sides.
15. As a player, I want each side's count to fall as the fight plays out, so that I can track which side is winning.
16. As a player, I want a contested capture to pause while the fleets fight, so that the world changes hands only after control is established.
17. As a player, I want attackers that break through to advance toward the remaining defenders, so that the battle moves around the world as each front falls.

## Implementation Decisions

### Movement and formation

- Use **world** for a fleet destination with a center and local movement bounds. Planets and moons use the same movement rules. A future base can use them by providing those bounds.
- A stationed fleet patrols in a coherent arc on a circular orbital band. Use stable formation slots with enough spacing for individual dots to be readable. Travel uses a compact formation aligned to the route, with staggered ranks and a stable heading.
- Transition from current positions into each new formation along continuous paths. Ships leave and join the orbital band along curved paths, bank around the world, and ease into station keeping. While waiting on a bearing, ships can make small local adjustments. They stop circulating around the world.
- Keep ship paths and their formation footprints outside the local world silhouette, including when shifting to the opposite side. Spread overflow into additional ranks or lanes. Use a deterministic clearance fallback for short routes or cramped placements. Existing direct routes retain their travel time and open-space crossing rules.
- Keep actual ship counts authoritative. Use the existing batched markers and allow aggregation for large fleets. A rendered group may represent multiple ships, but every nonempty defense group should be visible and the displayed count must match the simulation.
- At an unopposed or friendly arrival, blend from travel into the destination's local formation. Capture and friendly merge use the destination's occupant rules. After a battle, blend surviving ships from their last positions into the world’s current defense assignments or back into orbit when there are no active threats.

### Threat awareness and orbit positions

- A threat is a hostile fleet whose destination is this world and whose remaining travel time is within a configurable awareness window. Use one shared window initially, applied to both factions. Tune it against the time required to reposition from the far side of the world. Fleets passing nearby on other routes are ignored. Determine hostility against the current occupant, including a captor.
- Use the incoming route as the approach bearing, pointing outward from the world toward the approaching fleet. Use a stable fallback for a route with no usable direction.

| Level | Response to eligible threats |
| --- | --- |
| 0 | Continue orbital patrol until contact. |
| 1 | Send the entire garrison to the bearing of the single eligible fleet with the most ships. Break ties by earliest arrival, then by fleet ID. |
| 2 | Group eligible fleets approaching from similar bearings, then split the entire garrison among the resulting directions using the combined enemy ship count in each direction. |

- For Level 2, start with a configurable set of equal angular sectors in map coordinates. Fleets in the same sector share an assignment. Use the ship-count-weighted bearing within each sector as the station point on the orbit. Use a consistent sector boundary and handle wraparound at 360°.
- Allocate whole ships in proportion to sector weights. Floor the exact shares and award leftover ships in descending order of fractional remainder. When there are enough defenders to cover every threatened sector, give any uncovered sector one ship from the sector with the greatest surplus over its exact share among sectors holding more than one ship. When there are fewer defenders than sectors, give the strongest sectors one ship each. Break allocation ties by higher sector weight, earliest arrival, then sector index.
- For example, 22 defenders facing 20 ships from one direction and 2 from the opposite direction allocate 20 and 2. With two defenders and three sectors of weights 20, 5, and 2, they allocate 1, 1, and 0.
- Outside battle, recompute assignments when a fleet becomes eligible or arrives, when the occupant or stationed ship count changes, and when the configured level changes. Reuse slots and group identities where possible so reinforcements or a change of one ship do not cause the entire garrison to swap places. When threats clear, return to patrol. New ships join the current assignment and contribute to the same total.
- A valid launch from a world that is preparing for contact sends the full garrison, including ships in defensive positions. Defense groups are local assignments within one garrison. Expose behavior level and awareness window as separate configuration so later stats can select the level or change warning time independently.

### Battle choreography

- Stage a hostile encounter beside the world near the incoming bearing. The attacking formation takes the outward side and the defending formation takes the worldward side, facing across a clear gap. Orient the staging with the approach rather than a fixed screen axis. Use orbital lanes or offset ranks where needed to preserve clearance.
- During the exchange, the main formations hold position with limited drift. A small subset breaks away, fires on a curved pass toward the opposing ranks, and curves back into a slot if it survives. Stagger passes, preserve the readability of both lines, and stop paths for ships that are destroyed. At very small ship counts, favor a readable exchange over a pass.
- Let ships already on the contact side take the nearest defensive slots. Stage ships arriving from other bearings along clear paths that keep their faction identifiable. A battle should have a readable progression from approach to exchange to regrouping even at high ship counts.

### Battle timing and fronts

- A hostile arrival against a staffed world opens or joins a **front**, an encounter on the arrival’s approach sector. At Levels 1 and 2, only defenders assigned to that sector participate in the opening exchange. Use the assignment at contact before removing the arriving threat from the defense plan. Ships still moving toward that assignment may finish staging during the opening. At Level 0, the patrolling garrison reacts at contact and gathers at the earliest front. Break a same-time tie by incoming fleet ID.
- Group simultaneous hostile arrivals from the same sector into one front. Different sectors can fight concurrently. Resolve all contacts at the same instant before choosing destinations for groups that break through. A ship can be committed to only one front at a time.
- Lock each exchange’s participants when staging begins. Hostile ships arriving at a front with an exchange in progress wait for the next exchange and join any surviving attackers there. Defenders that survive and still face attackers stage for the next exchange at the same position. A friendly fleet joins the nearest surviving defensive group and waits for the next exchange if one is already in progress. Later arrivals leave a running exchange’s schedule intact.
- Resolve exchanges over match time. Start with a short staging interval followed by a bounded firing interval. For each exchange, remove one ship from each side per casualty pair until the smaller committed force is gone. Spread these losses over the firing interval and batch pairs when counts are high. Display each side’s current count. Firing passes use the same authoritative loss schedule as the main formations.
- When attackers clear their front, surviving attackers advance together along the orbital band toward the nearest surviving defensive group by arc distance. Break equal distances by sector index. Treat an uncovered sector as cleared at contact. Throughout the battle, defensive groups already engaged keep fighting at their positions, and groups awaiting contact hold their positions.
- If the destination group is already fighting when the advancing attackers arrive, they join the next exchange there. If it is waiting for contact, their arrival starts an exchange. If it has been eliminated, continue toward the nearest surviving group from the attackers’ current position. Keep the world contested while attackers are moving between fronts and defenders remain. An attack on a world with no stationed defenders and no active battle remains an unopposed landing.
- Give the match authority over warnings, assignments, active fronts, losses, and ownership. Drive formations, firing, explosions, and outcome feedback from those facts. Keep the total for each faction consistent across patrol, transit, station keeping, and combat, including any reinforcements waiting to join.
- Resolve control once one side has no ships left at the world. Combine survivors from all fronts into a single garrison. Suspend production and any running capture countdown at the world while its control is contested. A surviving incumbent resumes its production progress. A surviving captor resumes the same capture countdown. If the attacker clears all defenders and wins control, start the existing capture countdown or restore control immediately if the attacker is the formal owner. If all ships on both sides are destroyed, clear any capture and leave formal ownership in place.
- At a shared timestamp, keep production and capture completions that were already due ahead of new contact. Resolve due combat losses, process arrivals, and then determine control and match result from the complete timestamp. Include unresolved arriving forces in the match’s survival and victory checks after they have left transit. Block launches from a contested world until its battle is resolved.

## Testing Decisions

- Use the public match boundary in the existing Edit Mode suite. Drive configured worlds through normal fleet orders and time advancement. Assert the observable assignments, participants, counts, ownership, and capture and match states. Use controlled starting positions and launches to create threats from multiple directions.
- Check that Level 2 is the default for both factions and that all three levels can be configured. With the same incoming fleets, verify that Level 0 keeps patrolling, Level 1 chooses the strongest individual fleet, and Level 2 responds to the sum of ships per direction. Include a case where two smaller fleets from one sector outweigh the largest individual fleet in another sector.
- Check that only qualifying hostile fleets trigger repositioning. Cover entry into the awareness window, similar and opposite bearings, angle wraparound, equal weights, an empty garrison, and more directions than defenders. Verify the example allocations and that the groups always sum to the full garrison after new threats, production, friendly merges, and a launch.
- Exercise wins, losses, ties, unopposed arrival, counterattack during capture, friendly reinforcements, and same-time approaches. Verify that the battle has one result and every ship is counted once throughout transitions. Advance the same scenario in large and small time steps and compare the resulting observable states.
- With 22 defenders split 20 and 2, verify that a fleet reaching the weaker side initially faces 2 defenders. With 11 defenders split 10 and 1, verify that two attackers reaching the weaker side leave one attacker after the opening. That survivor advances toward the other group while the defenders hold position. Check that advancing attackers wait for the next exchange at an active front and continue to another group if their destination is cleared first. Cover simultaneous fronts, an uncovered sector, and several arrivals at one front. Verify that losses accumulate in pairs, that capture and production pause during combat, and that match end waits for relevant unresolved arriving forces.
- In Unity Play Mode, watch departures and arrivals, approach from both sides of a world, simultaneous warnings on several bearings, and a fight with surviving ships. Check continuous transitions, clearance around the world, a visible gap between the lines, readable firing passes, and survivor regrouping. Include a short route, a one-ship fleet, and a large fleet that uses aggregated markers.

## Out of Scope

- Ship silhouettes, sprites, faction art, and detailed weapon visuals.
- Orbital mechanics, fuel, gravity, collision damage, and 3D movement.
- Open-space interception, retreat and pursuit, player-issued tactical orders, and new enemy attack AI. Use controlled scenarios to demonstrate hostile approaches from multiple directions.
- Defense bonuses, stat-driven awareness and level selection, progression, and unlocks. All levels use the same paired-loss rule.
- New playable base or station content.

## Further Notes

- Start playtesting with a three-second warning window, eight sectors, and roughly three seconds per exchange including staging. Treat these as tuning assumptions. Validate warning time, sector boundaries, formation spacing, maneuver frequency, and battle pacing. Check whether paths near the world remain legible when defenders have little warning or must cross to the opposite side.
- For later progression, keep arrival warning time, assignment behavior, and any benefit for reaching a defensive position independent. A longer warning window should allow earlier repositioning without requiring a different allocation level.
