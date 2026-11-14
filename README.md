# Squadra

Squadra is a 2D real-time space strategy game in development with Unity 6. Command fleets between planets and moons, capture territory to build more Darts, and combine forces to overwhelm enemy defenses. A Dart is the basic fighter that planets and moons produce; a fleet is a group of Darts.

In the first playable scenario, you lead Blue against Red. Each side starts with one planet and 30 Darts, and a neutral moon offers a foothold. Select a world you control, then click another world to send your entire stationed fleet. Red builds up its defenses while you expand. Win by securing both planets once no Red fleet is in transit or still fighting where it arrived. Capturing the moon is optional, but its Dart production can give you the edge.

## Playing

Open `Assets/Scenes/MainMenu.unity` in Unity 6 and press Play. A build starts there too.

In the menu:

- **Single Player** opens the mission map. Mission 1 is the first playable scenario; Missions 2–5 are locked previews until their scenarios exist.
- **Settings** has General (language), Graphics (display mode, resolution, VSync) and Sound (master, music and effects volume, and **Mute in Background**). Settings are saved on this computer. A display change must be confirmed within 12 seconds or it is reverted.
- **Quit** asks for confirmation.
- Esc goes back, or cancels a dialog. On the mission map, A/D or the left/right arrow keys turn pages when there is more than one.

Campaign progress lasts for one session: winning a mission marks it completed until the game is closed. A mission returns to the mission map from its results screen, or at any time from the pause menu's Quit to Menu.

In a mission (`Assets/Scenes/SampleScene.unity` also runs on its own):

- Click a Blue world to select it, then click another world to send every Dart stationed there.
- Click the selected world again, or right-click, to cancel.
- Press Esc to pause.
- A fleet that lands on an empty or beaten world needs five seconds to capture it.
- A fleet that reaches a defended hostile world fights there; see Battles below.

When the match ends, the results panel appears over the faded battlefield:

- It shows Victory or Defeat, the mission time (time actually played, as minutes:seconds) and one row per player with **Darts destroyed** and **Darts lost**. Your row is first. Darts lost counts every Dart a player lost in battle, attacking or defending; Darts destroyed counts the enemy and neutral guard Darts its fleets destroyed. Casualties are counted as they happen, so a battle still under way when the match ends counts the Darts lost up to that moment.
- **Next Mission** launches the mission that follows in the campaign. It is disabled after a defeat ("Win this mission to continue") and while the following mission has no scenario yet ("Next mission coming soon"); hover it to see why. It is not shown when no mission follows.
- **Restart** starts a fresh attempt at the same mission. **Back to Menu** opens the mission map.
- Winning the last mission of the catalog shows **Campaign Complete!** instead, with Restart and Back to Menu.
- Use the mouse, or the arrow keys or Tab with Enter or Space. Clicking beside the panel does nothing. Esc opens the pause menu over the results; resuming brings the same results back.

## Battles

A hostile fleet that reaches a world with Darts stationed there starts a battle. Nothing is decided on arrival: the two sides lose one Dart each at the same moment, again and again, until one side has none left.

- How long a battle lasts depends on the balance of the two sides, not on their size. Equal forces fight for five seconds; a side outnumbered ten to one or worse falls in one second; anything between is in between. Ten against ten and a hundred against a hundred both take five seconds. The losses are spread evenly over that time, the last pair at the end.
- The survivors are what the old instant rule left: 30 against 20 leaves 10 attackers, who then start the five-second capture; 20 against 30 leaves 10 defenders in control; a tie leaves nobody and the world stays with its owner, or neutral.
- A fleet of either side that arrives during the battle joins it at once. Earlier losses stay lost, and the rest of the battle is timed afresh, one to five seconds, for the forces then present, so a reinforced battle can last longer than five seconds.
- A world being fought over produces nothing, and a capture running there stands still; both carry on from where they were if the same side still holds the world afterwards. A new winner that is not the owner starts its own capture from zero, and an owner that wins back its world has it at once.
- Darts in a battle are committed: no fleet can be launched from a contested world, and nobody retreats. Orders from other worlds work as usual, including sending more Darts to the battle.
- An attack on a world with no Darts lands and starts capturing straight away, as before.

On screen, the defenders gather on the side an enemy fleet is approaching from during the last moments of its flight, the fleet forms up outside the orbit facing them, and the two fronts exchange shots while Darts explode as they are lost. The world shows both sides' counts in their colors and FIGHTING, with a capture shown as paused. Survivors drift back into orbit. Large fleets are drawn with fewer dots than Darts, so there a dot, a shot or an explosion stands for several; the counts are always exact.

Battles are between two sides. What should happen when a third faction's fleet reaches a battle has not been decided, and the match refuses it with an error instead of guessing; the current missions have only Blue and Red.

The battle length is set on the `Game` object in the mission scene (**Battles**: shortest and longest seconds, the decisive ratio and the curve between them). How the fight is drawn and heard is set on the `Fleets` object (**Battle**: how early defenders gather, the gap between the fronts, how much fighting dots move, and the most shots and sounds per second).

## Pausing

Esc opens the pause menu at any point of a mission, a victory or defeat screen included. The mission is frozen for as long as the menu is open: no production, fleet movement, battle casualties, capture or mission time, no orders, and the scene's animation and gameplay effects, a battle's shots and explosions included, stand still. Resuming carries on from the same moment with the same world selected; nothing is caught up.

- **Resume**, or Esc on the pause menu, returns to the mission or to its result.
- **Settings** is the same Settings as in the main menu. Back or Esc returns to the pause menu. Esc first closes an open dropdown list, then cancels a display confirmation, and only then leaves Settings. The 12-second display confirmation keeps counting while the mission is paused.
- **Quit to Menu** and **Quit to Desktop** ask for confirmation, with Cancel focused. Quit to Menu opens the mission map; an unfinished match is dropped, and a victory that was already on screen stays recorded. Quit to Desktop closes the game.
- The battlefield is dimmed by the same scrim the results use, so pausing over a result does not darken it further; the results panel is put away while the pause menu is open.
- The mission music keeps playing at half its level and the menu cues sound as usual. Volume changes made while paused apply on resume.

The game keeps running while another application is in front; it is never paused by switching away. With **Mute in Background** checked (Settings > Sound, off by default) nothing is heard until the game is in front again. It only silences the output: volumes, a pause and the music's place in its track are untouched.

## Project layout

- `Assets/Squadra/Scripts/Simulation` holds the rules: one `Match` class owns orders, time, battles, capture, production, each player's combat totals and the result. It has no Unity dependencies.
- `Assets/Squadra/Scripts/Presentation` holds the scene components that send orders to the match and draw its state, the results screen shown when it ends, and the pause menu. `GameController` owns the pause itself.
- `Assets/Squadra/Scripts/Campaign` holds the campaign rules: which missions are completed, available, locked or previews, when an unlock is shown, how missions are grouped into map pages, and what the results screen offers after a mission. It has no Unity dependencies.
- `Assets/Squadra/Scripts/Pause` holds the pause rules: which pause screen is in front, what each press of Esc does, when a quit goes ahead, the text of the quit confirmations, and how pausing and background muting combine with the volume settings. It has no Unity dependencies.
- `Assets/Squadra/Scripts/Menu` holds the menu scene's components: the main menu and its screens, the mission map, the settings, and the menu's sounds. The Settings screen and the confirmation dialog are shared with the pause menu.
- `Assets/Squadra/UI` holds the menu's layout (`MainMenu.uxml`), look (`MainMenu.uss`) and panel settings, the results panel's layout and look (`MissionResults.uxml`, `MissionResults.uss`) and the pause menu's (`PauseMenu.uxml`, `PauseMenu.uss`). All are built with UI Toolkit and share the panel settings and the button styles. `Settings.uxml` and `Dialog.uxml` are the Settings screen and the confirmation dialog, used by both the main menu and the pause menu.
- `Assets/Squadra/Campaign/Missions.asset` is the mission catalog: the ordered missions, each with a stable id and an optional gameplay scene. A mission needs its scene in the build to be playable.
- `Assets/Squadra/Shaders` holds the procedural shaders for the planets, space backdrop, rings and fleet dots.
- `Assets/Squadra/Editor` adds the menu item **Squadra > Rebuild First Playable Scene**, which regenerates the scene, materials and post-processing profile from code. It overwrites manual edits to those generated assets. **Squadra > Rebuild Main Menu Scene** does the same for the menu scene and puts it first in the build; it keeps the UI files and an existing mission catalog. **Squadra > Rewire Results Screen In Open Scene** adds the results screen to a mission scene that does not have it yet and leaves the rest of the scene alone; **Squadra > Rewire Pause Menu In Open Scene** does the same for the pause menu.
- `Assets/Squadra/Audio` holds the sound effects (`Sfx`), the mission track (`Music`) and the menu track (`MenuMusic`).
- `Assets/Squadra/Tests/EditMode` holds the match, battle, match results, campaign and pause rule tests. Run them from **Window > General > Test Runner** (EditMode).
- The specifications are in `specifications/`. `dart-battle-spec.md` is the current word on the basic unit's name and on combat; the older specifications keep their original wording and carry a note where it replaces them.

## Mission map

The number of missions on one map page is the **Missions per Page** property of the `Mission Map` object in the menu scene (1 to 10, default 5). With more missions than fit on a page, the map shows page arrows. To add a mission, add an entry to the mission catalog, give it an id that will never change, assign its scene, and add that scene to the build settings.

Another scene can return to the menu on the mission map by calling `MainMenu.OpenCampaignMap()`; the results screen's Back to Menu does.

## Mission results

A mission's players are the **Players** list of the `Game` object in its scene: two to six entries, each a faction and a display name. The results table lists the human player (Blue) first and the others in that order, including players that were eliminated or never fought. Neutral is never a player. Only Blue and Red are playable so far; the other four factions exist so that rosters and results can already hold six players.

The `Game` object's **Catalog** tells the results screen where the mission sits in the campaign. The mission that follows is always the next catalog entry, and the last entry is the end of the campaign. A mission scene played on its own, without being launched from the map, shows a plain Victory or Defeat with Restart and Back to Menu.

## Sound

`SoundLayer` plays an effect when a fleet departs, arrives peacefully, or starts a battle, for the weapon fire and Dart explosions of the battle as they are shown, when a world is captured, and when the match is won or lost. The shots and explosions of battles are limited per battle and are skipped while most voices are busy, so large battles do not drown everything else. The fleet and battle effects have several takes named `Name_1.wav`, `Name_2.wav`, and so on; one is picked at random each time, with a small random change in pitch and level. A capture and the two match results always sound the same. `MusicPlayer` loops the background track and keeps it playing across restarts; when a scene brings a different track, as a mission does after the menu, it fades the old one out and the new one in.

`MenuAudio` plays the menu's cues: one when the pointer enters a button, one when a button or tab is activated or the map page changes, and a different one for Back, Cancel and Esc. The three volume settings scale everything: Master all sound, Music both tracks, Sound Effects the menu cues and the gameplay effects. Pausing and Mute in Background are factors on top of these settings and never change them.

Most effects are generated from code. `Assets/Squadra/Editor/SoundRecipes.cs` describes each one, and **Squadra > Rebuild Sound Effects** renders them to WAV files in `Assets/Squadra/Audio/Sfx`, overwriting the previous render. Battle starts, weapon fire, captures, the match results and the menu cues are synthesized; departures and arrivals are shaped from the recordings in `Assets/Squadra/Audio/Sources`. The menu track (`MenuMusic/MenuTheme.wav`) is synthesized too; put a track of your own in that folder and rebuild the menu scene to use it instead. The Dart explosions are recordings used as they are (`DartDestroyed_N.ogg`). To add a take of your own, drop a file with the next number into the `Sfx` folder and run **Squadra > Rewire Sounds In Open Scene**. Volumes are set on the `Sounds` and `Music` objects in the scene.

## Credits

- Music: "Observing The Star" by yd, from [OpenGameArt](https://opengameart.org/content/another-space-background-track), released under CC0.
- Engine, force-field and explosion recordings: "Sci-Fi Sounds" by [Kenney](https://www.kenney.nl), released under CC0.
