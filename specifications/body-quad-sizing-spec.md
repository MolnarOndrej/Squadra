# Squadra body quad sizing in one place

## Problem Statement

Planets, moons and asteroids are drawn by `Planet.shader` on a square quad. The shader paints the body as a disc in the middle of the quad and leaves the margin around it for the halo. The material's `_BodyRadius` is the disc's radius as a fraction of the quad's half-width (shader default 0.7). To show a body of a given radius, the quad must therefore be larger than the body:

```
fraction = material has _BodyRadius ? max(0.05, _BodyRadius) : 0.7
quad side = body radius × 2 / fraction
```

This rule is written out three times:

| Where | What it sizes |
| --- | --- |
| `Assets/Squadra/Match/View/WorldView.cs`, `Layout` | Worlds in missions and the Sandbox |
| `Assets/Squadra/MainMenu/MenuScenery.cs`, `Place` | The planets behind the main menu |
| `docs/visual-catalog/render-pictures.cs`, `ShowBody` | The visual catalog pictures and gameplay-scale comparison strips |

The copies are the same rule, but nothing keeps them in step. If one changes (a new minimum, another fallback, a renamed property, a different halo margin), the others quietly draw bodies at a different size. No error appears and no test fails, because all three are presentation code that the Edit Mode tests do not reach. The visible result would be menu planets sized differently from in-game ones, or catalog pictures and comparison strips that no longer show the size the game uses. The comparison strips exist to judge a look at gameplay size, so a mismatch there defeats their purpose.

## Solution

Give the rule one owner: a public static method in the Visuals feature, next to `Shapes.CreateQuad`, which already creates the quads these bodies are drawn on. `WorldView.Layout`, `MenuScenery.Place` and the render script call it instead of computing the side themselves. No drawn size changes.

## User Stories

1. As a maintainer, I want the body quad sizing rule in one method, so that changing it changes every place a body is drawn.
2. As a maintainer, I want the rule's numbers named once, so that I can read what the 0.05 and 0.7 mean without finding the shader.
3. As a player, I want worlds in missions, the Sandbox and the main menu drawn exactly as before, so that the cleanup changes nothing I see.
4. As a look author, I want the catalog pictures and comparison strips to size bodies with the game's own rule, so that what I judge there is what the game draws.

## Implementation Decisions

- Add a public static method to `Assets/Squadra/Visuals/Shapes.cs` (namespace `Squadra`, Assembly-CSharp) that takes the body's material and radius and returns the quad side. Suggested name: `BodyQuadSide(Material material, float bodyRadius)`. It keeps today's handling of a null material (`WorldView` and `MenuScenery` both fall back to 0.7 for it).
- Move the property ID and the two numbers into `Shapes` as named private constants with a comment saying what they mean: the disc's share of the quad's half-width, the minimum that keeps the division safe, and the fallback for a material without the property (the shader's own default).
- `WorldView.Layout` and `MenuScenery.Place` keep setting position and scale themselves and call the method only for the side. Remove their now unused `BodyRadiusId` fields.
- `render-pictures.cs` calls the same method in `ShowBody` and drops its local constants and the "keep these in step" comment. It runs inside the editor through `unity command script eval`, so Assembly-CSharp types are available to it.
- One commit: a responsibility change in presentation code only. No rule assembly, serialized field, scene, UXML or asset changes. No new dependency between presentation features: `Match` already uses `Shapes`, and `MenuScenery` already uses `Visuals` through `RingStyle`.

## Testing Decisions

- Presentation code cannot be reached from the Edit Mode test assemblies, so this change has no new unit test. Its evidence is that sizes are unchanged.
- Before and after the change, dump the `localScale` of every body quad from one `script eval` per scene: `Match/Scenes/Mission.unity` (each `WorldView` surface after `Layout`), `MainMenu/Scenes/MainMenu.unity` (the `MenuScenery` bodies) and `Sandbox/Scenes/Sandbox.unity` (the preview worlds). The dumps must be identical.
- Re-render the catalog pictures with `render-pictures.cs` and compare them pixel by pixel with the committed ones. Differences must stay within run-to-run animation noise: about 30 per channel on Thalassa's clouds, under 10 elsewhere. Restore the PNGs afterwards unless they are meant to change.
- Run all Edit Mode tests and `dotnet format whitespace --folder Assets/Squadra --verify-no-changes`, and check the Console for compile errors.
- Play `MainMenu.unity` and `Mission.unity` and look at the worlds; open `Sandbox.unity`.

## Out of Scope

- Changing the rule itself, including the mismatch between the code's 0.05 minimum and the shader's `Range(0.3, 1)` for `_BodyRadius`.
- The 0.7 that `SquadraAssetBuilder` writes into new materials. It is the value an author starts from, not the sizing fallback, and has a different reason to change.
- The aura and owner-ring sizing in `WorldView.Layout`, which follows other rules.
- Other duplication in the render script or the presentation code.

## Assumptions to Validate

- No other code sizes a `Planet.shader` quad. Search for `_BodyRadius` and `Planet.shader` usages before starting; one more copy belongs in this change too.
- `Shapes` is the right owner. If the implementer finds a closer owner in `Visuals` (one that already deals with `Planet.shader` materials), use it and say why in the change.
