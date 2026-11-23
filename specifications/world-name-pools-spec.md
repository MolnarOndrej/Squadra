# Expanded world name pools

## Problem Statement

Campaign missions draw from eight planet names, five asteroid names, and five moon names. Players can quickly recognize the same small set across missions and game launches. A mission with more worlds of one type than its dictionary contains has to reuse names. The current dictionaries contain only conventional names, so every world label has a similar style.

## Solution

Expand each world type's dictionary to 40 entries. Keep every existing name and mix in eight fictional catalog designations per type. A planet might be called `Thalassa` or `VTX-17`, for example. Standard mission preparation draws both styles from the same pool, so the mix varies with the draw.

## User Stories

1. As a player, I want to encounter a wider variety of planet, asteroid, and moon names across missions and game launches, so that different battlefields are easier to remember.
2. As a player, I want to see occasional catalog designations alongside conventional names, so that worlds have a wider range of labels.
3. As a player, I want worlds of the same type to have distinct names while unused choices remain, so that I can tell them apart on busy maps.
4. As a player, I want each world to keep its assigned name when I restart or replay a mission during the same game session, so that the battlefield stays recognizable.
5. As a content author, I want to add both styles to the existing dictionaries, so that I can maintain world names using the same one-entry-per-line format.

## Implementation Decisions

| Dictionary | Existing names to keep | New conventional names | New catalog designations | Final total |
| --- | ---: | ---: | ---: | ---: |
| Planets | 8 | 24 | 8 | 40 |
| Asteroids | 5 | 27 | 8 | 40 |
| Moons | 5 | 27 | 8 | 40 |

- Author the additions in the three existing Visuals dictionaries. Each nonblank line contains one unique display name within that dictionary. Keep internal spaces and punctuation as part of the entry, such as the space in `S/47 N2`.
- Give the designations short, distinct styles by world type. Example planet designations are `VTX-17`, `ARC-52`, and `NVR-08`. Example asteroid designations are `KX-204`, `TR-731`, and `MV-095`. Example moon designations are `S/47 N2`, `S/19 R4`, and `S/63 H1`. Fictional formats and invented conventional names are welcome.
- Keep the existing selection behavior. A world draws from its own type's dictionary, with designations eligible on equal terms with conventional names. The 20% share applies to dictionary contents. The number of designations in a mission depends on its worlds and the draw.
- Preserve distribution within a mission. Each type uses all available entries before it repeats any, and additional draws use the pool evenly. Missions can reuse choices made by other missions.
- Keep each prepared assignment for the current game session through restarts, menu returns, replays, and captures. A fresh launch can draw different names. Names remain display data, with existing node IDs providing world identity and surface selection remaining independent of names.

## Testing Decisions

- Check the three bundled dictionaries for 40 unique usable entries each, including all existing names and exactly eight fictional catalog designations per type. Review the designations for legibility and type-specific style.
- Use the existing Edit Mode appearance and mission preparation tests to verify type-specific selection, pool exhaustion, balanced reuse, and assignment stability. Use controlled randomness to verify that a representative designation from each dictionary can become a prepared world name with its space and punctuation intact.
- During implementation, play a prepared mission with examples from all three pools and check that the longest conventional names and designations fit the existing battlefield captions at supported window sizes. Restart and re-enter the mission from the menu within the same session and confirm the labels stay assigned to the same worlds.

## Out of Scope

- Runtime generation of names or catalog codes, per-mission quotas, or player-facing naming controls.
- Astronomical standards, real catalog data, and designation metadata such as discovery dates or parent-body links.
- Mission file format or Mission Editor changes, changes to authored Sandbox names, and changes to battlefield caption layout, surfaces, or simulation.

## Further Notes

- The examples guide content authoring. Final wording for the new entries is a content decision during implementation. Verify the longest entries against the current captions before finalizing the pools.
