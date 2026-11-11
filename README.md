# Squadra

Squadra is a 2D real-time space strategy game in development with Unity 6. Command fleets between planets and moons, capture territory to build more ships, and combine forces to overwhelm enemy defenses.

In the first playable scenario, you lead Blue against Red. Each side starts with one planet and 30 ships, and a neutral moon offers a foothold. Select a world you control, then click another world to send your entire stationed fleet. Red builds up its defenses while you expand. Win by securing both planets once no Red fleets remain in transit. Capturing the moon is optional, but its ship production can give you the edge.

## Playing

Open `Assets/Scenes/MainMenu.unity` in Unity 6 and press Play. A build starts there too.

In the menu:

- **Single Player** opens the mission map. Mission 1 is the first playable scenario; Missions 2–5 are locked previews until their scenarios exist.
- **Settings** has General (language), Graphics (display mode, resolution, VSync) and Sound (master, music and effects volume). Settings are saved on this computer. A display change must be confirmed within 12 seconds or it is reverted.
- **Quit** asks for confirmation.
- Esc goes back, or cancels a dialog. On the mission map, A/D or the left/right arrow keys turn pages when there is more than one.

Campaign progress lasts for one session: winning a mission marks it completed until the game is closed. There is no way back to the menu from inside a mission yet, so stop and relaunch to return.

In a mission (`Assets/Scenes/SampleScene.unity` also runs on its own):

- Click a Blue world to select it, then click another world to send every ship stationed there.
- Click the selected world again, right-click, or press Esc to cancel.
- A fleet that lands on an empty or beaten world needs five seconds to capture it.
- After the match ends, click (or press R) to play again.

## Project layout

- `Assets/Squadra/Scripts/Simulation` holds the rules: one `Match` class owns orders, time, combat, capture, production and the result. It has no Unity dependencies.
- `Assets/Squadra/Scripts/Presentation` holds the scene components that send orders to the match and draw its state.
- `Assets/Squadra/Scripts/Campaign` holds the campaign rules: which missions are completed, available, locked or previews, when an unlock is shown, and how missions are grouped into map pages. It has no Unity dependencies.
- `Assets/Squadra/Scripts/Menu` holds the menu scene's components: the main menu and its screens, the mission map, the settings, and the menu's sounds.
- `Assets/Squadra/UI` holds the menu's layout (`MainMenu.uxml`), look (`MainMenu.uss`) and panel settings. The menu is built with UI Toolkit.
- `Assets/Squadra/Campaign/Missions.asset` is the mission catalog: the ordered missions, each with a stable id and an optional gameplay scene. A mission needs its scene in the build to be playable.
- `Assets/Squadra/Shaders` holds the procedural shaders for the planets, space backdrop, rings and fleet dots.
- `Assets/Squadra/Editor` adds the menu item **Squadra > Rebuild First Playable Scene**, which regenerates the scene, materials and post-processing profile from code. It overwrites manual edits to those generated assets. **Squadra > Rebuild Main Menu Scene** does the same for the menu scene and puts it first in the build; it keeps the UI files and an existing mission catalog.
- `Assets/Squadra/Audio` holds the sound effects (`Sfx`), the mission track (`Music`) and the menu track (`MenuMusic`).
- `Assets/Squadra/Tests/EditMode` holds the match and campaign rule tests. Run them from **Window > General > Test Runner** (EditMode).
- The specifications are in `specifications/`.

## Mission map

The number of missions on one map page is the **Missions per Page** property of the `Mission Map` object in the menu scene (1 to 10, default 5). With more missions than fit on a page, the map shows page arrows. To add a mission, add an entry to the mission catalog, give it an id that will never change, assign its scene, and add that scene to the build settings.

Another scene can return to the menu on the mission map by calling `MainMenu.OpenCampaignMap()`.

## Sound

`SoundLayer` plays an effect when a fleet departs, arrives peacefully, or starts a battle, for the weapon fire and ship explosions of the battle, when a world is captured, and when the match is won or lost. The fleet and battle effects have several takes named `Name_1.wav`, `Name_2.wav`, and so on; one is picked at random each time, with a small random change in pitch and level. A capture and the two match results always sound the same. `MusicPlayer` loops the background track and keeps it playing across restarts; when a scene brings a different track, as a mission does after the menu, it fades the old one out and the new one in.

`MenuAudio` plays the menu's cues: one when the pointer enters a button, one when a button or tab is activated or the map page changes, and a different one for Back, Cancel and Esc. The three volume settings scale everything: Master all sound, Music both tracks, Sound Effects the menu cues and the gameplay effects.

Most effects are generated from code. `Assets/Squadra/Editor/SoundRecipes.cs` describes each one, and **Squadra > Rebuild Sound Effects** renders them to WAV files in `Assets/Squadra/Audio/Sfx`, overwriting the previous render. Battle starts, weapon fire, captures, the match results and the menu cues are synthesized; departures and arrivals are shaped from the recordings in `Assets/Squadra/Audio/Sources`. The menu track (`MenuMusic/MenuTheme.wav`) is synthesized too; put a track of your own in that folder and rebuild the menu scene to use it instead. The ship explosions are recordings used as they are (`ShipDestroyed_N.ogg`). To add a take of your own, drop a file with the next number into the `Sfx` folder and run **Squadra > Rewire Sounds In Open Scene**. Volumes are set on the `Sounds` and `Music` objects in the scene.

## Credits

- Music: "Observing The Star" by yd, from [OpenGameArt](https://opengameart.org/content/another-space-background-track), released under CC0.
- Engine, force-field and explosion recordings: "Sci-Fi Sounds" by [Kenney](https://www.kenney.nl), released under CC0.
