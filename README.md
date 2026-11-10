# Squadra

Squadra is a 2D real-time space strategy game in development with Unity 6. Command fleets between planets and moons, capture territory to build more ships, and combine forces to overwhelm enemy defenses.

In the first playable scenario, you lead Blue against Red. Each side starts with one planet and 30 ships, and a neutral moon offers a foothold. Select a world you control, then click another world to send your entire stationed fleet. Red builds up its defenses while you expand. Win by securing both planets once no Red fleets remain in transit. Capturing the moon is optional, but its ship production can give you the edge.

## Playing

Open `Assets/Scenes/SampleScene.unity` in Unity 6 and press Play.

- Click a Blue world to select it, then click another world to send every ship stationed there.
- Click the selected world again, right-click, or press Esc to cancel.
- A fleet that lands on an empty or beaten world needs five seconds to capture it.
- After the match ends, click (or press R) to play again.

## Project layout

- `Assets/Squadra/Scripts/Simulation` holds the rules: one `Match` class owns orders, time, combat, capture, production and the result. It has no Unity dependencies.
- `Assets/Squadra/Scripts/Presentation` holds the scene components that send orders to the match and draw its state.
- `Assets/Squadra/Shaders` holds the procedural shaders for the planets, space backdrop, rings and fleet dots.
- `Assets/Squadra/Editor` adds the menu item **Squadra > Rebuild First Playable Scene**, which regenerates the scene, materials and post-processing profile from code. It overwrites manual edits to those generated assets.
- `Assets/Squadra/Tests/EditMode` holds the rule tests. Run them from **Window > General > Test Runner** (EditMode).
- The specification is in `specifications/node-rts-first-playable-spec.md`.
