# Squadra

Squadra is a 2D real-time space strategy game in development with Unity 6. Command fleets between planets and moons, capture territory to build more Darts, and combine forces to overwhelm enemy defenses. A Dart is the basic fighter that planets and moons produce; a fleet is a group of Darts.

In Mission 1, First Contact, you lead Blue against Red. Each side starts with one planet and 30 Darts, and a neutral moon offers a foothold. Select a world you control, then click a world connected to it to send your entire stationed fleet. Red builds up its defenses while you expand. You win once Red owns no planet, moon or asteroid and has no Darts left anywhere, and you lose once the same is true of you. Neutral worlds and their guards never stand in the way: capturing the moon is optional, but its Dart production can give you the edge.

## Playing

Open `Assets/Squadra/MainMenu/Scenes/MainMenu.unity` in Unity 6 and press Play. A build starts there too.

In the menu:

- **Single Player** opens the mission map. Mission 1 is First Contact; Missions 2–5 are locked previews until their mission files exist.
- **Settings** has General (language), Graphics (display mode, resolution, VSync) and Sound (master, music and effects volume, and **Mute in Background**). Settings are saved on this computer. A display change must be confirmed within 12 seconds or it is reverted.
- **Quit** asks for confirmation.
- Esc goes back, or cancels a dialog. On the mission map, A/D or the left/right arrow keys turn pages when there is more than one.

Campaign progress lasts for one session: winning a mission marks it completed until the game is closed. A mission returns to the mission map from its results screen, or at any time from the pause menu's Quit to Menu.

In a mission:

- Click a Blue world to select it, then click another world to send every Dart stationed there. Fleets travel only along the dashed routes between worlds; an order to a world with no direct route from the selected one is refused ("No route from ... to ...").
- Click the selected world again, or right-click, to cancel.
- Press Esc to pause.
- A fleet that lands on an empty or beaten world needs five seconds to capture it.
- A fleet that reaches a defended hostile world fights there; see Battles below.
- Planets build one Dart a second and moons one every two seconds while owned. Asteroids never build Darts; otherwise they hold, receive and send fleets, are guarded, fought over and captured like any world.
- A side stays in the match while it owns any world, an empty one included, or has Darts anywhere: stationed, capturing, fighting on either side of a battle or in flight. The match ends the moment Red, or you, have neither. Should both go out at the same instant, it counts as a defeat; that cannot happen in a mission that starts with both sides owning a world.

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

## Mission editor

The Mission Editor is a separate Windows program, `SquadraMissionEditor.exe`, for drawing mission maps: worlds, the routes fleets may travel between them, who owns what and how many Darts each world starts with. It needs neither Unity nor the game, and can run beside the game. The game plays the mission files the campaign catalog assigns; see Missions in the game below.

To build it, run **Squadra > Build Mission Editor (Windows)** in Unity. The program is written to `Builds/MissionEditor`. To try it inside Unity instead, open `Assets/Squadra/MissionEditor/Scenes/MissionEditor.unity` and press Play.

The window is a toolbar, the map, and a panel with the selected object, the mission's settings, its players and the validation messages.

- **New**, **Open**, **Save** and **Save As** work on one `.json` file per mission (Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S). Save asks for a file the first time.
- **Add Node**, then a click on the map, places a neutral planet of size 1 with no Darts. Nodes are named `N1`, `N2` and so on; a node keeps its ID for good, and deleting a node never renumbers the others.
- **Add Connection** adds a route. With a node selected the route starts from that node and one click on another node finishes it; with nothing selected, click one node and then the other. Routes run both ways. A node cannot be connected to itself, and two nodes cannot be connected twice.
- Click a node or a route to select it. Drag a node to move it. **Delete Selected**, or the Delete key, removes a route, or a node together with its routes. Esc, or a right-click anywhere on the map, cancels one step at a time: first a tool that is waiting for a click, keeping the selection, then the selection.
- Drag the background, or drag with the middle or right button, to pan; a right-drag cancels nothing. Scroll to zoom, or use **-** and **+**. **Fit to Map** shows every node, the whole of its body included. **Distances** writes each route's distance on the map. None of these change the mission.
- A selected node shows its ID, type (planet, moon or asteroid), size, owner, starting Darts and X and Y; type exact coordinates there. A selected route shows its two ends and its distance.
- **Size** is the body's radius in game world units, from 0.1 to 5 for every type. A planet starts at 1, a moon at 0.5 and an asteroid at 0.3. Changing a node's type sets its size to the new type's default, whatever it was; set the size again afterwards if it should differ. An asteroid is a node like the others, with an owner, Darts and routes; in the game it will never produce Darts, which follows from its type and is not a setting.
- Planets are drawn as discs, moons as crescents and asteroids as rocks, in their owner's color; neutral nodes are gray. Each is drawn at the real size of its body: its size divided by the distance scale, in map units, so at 100% a planet of size 1 on a map of scale 1 is 40 pixels in radius, and nodes of one size are equally large whatever their type. Zooming and the distance scale change how large a body looks, never its size. Bodies may overlap; nothing is moved or refused, and of two overlapping nodes a click picks the one added later. A body too small to see has a thin ring around it and can still be clicked. A route's distance is measured between the middles of its nodes, whatever their sizes.
- **Mission** holds the mission's ID, its name and the distance scale. A route's distance is the straight line between its nodes on the map, times the distance scale.
- **Players** lists up to six players, each with an ID and one of six colors that no other player has. A player's ID is fixed once the player is added. Removing a player that owns nodes asks first and makes those nodes neutral; a player that owns none is removed at once.
- **Validation** lists what is wrong with the mission. Warnings (separate groups of nodes, nodes on the same spot, routes of no length) never stop a save; click a message to select what it is about.

What is typed into a field is applied at once when it is a valid value. Text that is not stays in the field, marked in red with the reason, and the mission keeps its previous value; saving, leaving the mission or selecting something else then asks whether to discard that text or go back to it. Numbers use a period as the decimal sign.

The editor does not lose work quietly:

- New, Open and closing the window (or Ctrl+Q) offer **Save**, **Discard** and **Cancel** when the mission has unsaved changes. A new mission has none until it is first edited, so an untouched editor closes without asking. After Save the action only goes on if the save succeeded; after Discard the mission stays open until the new or opened mission has really taken its place.
- A file that cannot be read as a mission is refused with the reasons, and the mission being edited is left as it was.
- A save writes a temporary file beside the destination and swaps it in, so a failed save leaves the previous file whole and the changes unsaved.
- If the mission's file was changed or deleted by something else since the editor opened or saved it, Save asks: **Overwrite**, **Save As** or **Cancel**.

It does not protect against the program being killed or the power failing, there is no undo, and two programs editing the same file at the same time are not supported.

### Mission files

A mission is one UTF-8 JSON file with nothing beside it. Every field is required, names and values are case-sensitive, and a file with an unknown field, a missing field, a value of the wrong type, a property named twice or another `schemaVersion` is refused rather than read in part.

```json
{
  "schemaVersion": 1,
  "id": "first-contact",
  "name": "First Contact",
  "map": { "distanceScale": 1 },
  "players": [
    { "id": "player1", "color": "blue" },
    { "id": "player2", "color": "red" }
  ],
  "nodes": [
    { "id": "N1", "type": "planet", "size": 1, "position": { "x": -6, "y": 0 }, "owner": "player1", "darts": 30 },
    { "id": "N2", "type": "moon", "size": 0.5, "position": { "x": 0, "y": 2.5 }, "owner": null, "darts": 0 },
    { "id": "N3", "type": "planet", "size": 1.2, "position": { "x": 6, "y": 0 }, "owner": "player2", "darts": 30 },
    { "id": "N4", "type": "asteroid", "size": 0.3, "position": { "x": 2, "y": -3 }, "owner": null, "darts": 0 }
  ],
  "edges": [
    { "source": "N1", "target": "N2" },
    { "source": "N2", "target": "N3" },
    { "source": "N3", "target": "N4" }
  ]
}
```

- IDs are made of ASCII letters, digits, underscores and hyphens. Node IDs are unique among nodes and player IDs among players.
- `color` is one of `blue`, `red`, `green`, `yellow`, `purple`, `orange`; no two players share one. `type` is `planet`, `moon` or `asteroid`.
- `size` is the body's radius in game world units, a number from 0.1 to 5. Every node states it, one at its type's default too; a file is never given a default for a size it leaves out. There is no field for what a node produces.
- `owner` is a player's ID, or `null` for a neutral node. `darts` is a whole number from 0 to 2147483647.
- Positions are in map units, X to the right and Y up; negative and fractional values are fine. Coordinates and the distance scale may be at most 1000000000 in size, and the distance scale must be greater than 0.
- An edge names the two nodes it joins, in either order. Distances are calculated, never stored. Numbers are written in full, so saving a file again does not move anything.

## Project layout

Everything of the game is under `Assets/Squadra`, one folder per feature. A feature folder holds all that belongs to it: rules, scene components, UI files, editor tooling, scenes and tests. A feature with rules keeps them in a subfolder that is an assembly of its own without Unity dependencies, and its Unity-side scripts beside them in `View`. A feature without rules keeps its scripts at its root.

- `Match` is one mission being played. `Simulation` holds the rules: one `Match` class owns orders, time, battles, capture, production, each player's combat totals and the result. It has no Unity dependencies. `Preparation` turns a mission file into a mission the game can play: it checks what the game can run, converts the map to game positions, resolves routes and owners by ID and deals each world its name and surface. It has no Unity dependencies either. `View` holds the scene components that build the battlefield, send orders to the match and draw its state; `GameController` owns the pause itself. `Scenes` holds the shared mission scene every mission is played in and First Contact (`mission-01.json`), `Editor` the builder that generates the scene, and `Tests` the match, battle, elimination, route, preparation and match results tests.
- `Results` holds the results screen shown when a match ends and, in `UI`, its layout and look (`MissionResults.uxml`, `MissionResults.uss`).
- `Pause` holds the pause menu. `Rules` holds the pause rules: which pause screen is in front, what each press of Esc does, when a quit goes ahead, the text of the quit confirmations, and how pausing and background muting combine with the volume settings. It has no Unity dependencies. `View` holds the menu's component, `UI` its layout and look (`PauseMenu.uxml`, `PauseMenu.uss`), and `Tests` the pause rule tests.
- `Campaign` holds the campaign. `Rules` holds the campaign rules: which missions are completed, available, locked or previews, when an unlock is shown, how missions are grouped into map pages, and what the results screen offers after a mission. It has no Unity dependencies. `View` holds the session's progress, the missions prepared for the session (`MissionLibrary`), the mission catalog type and the mission map the menu shows. `Missions.asset` is the mission catalog: the ordered missions, each with a stable id and an optional mission file, the shared gameplay scene and the world looks. `Tests` holds the campaign rule tests.
- `MainMenu` holds the menu scene: the main menu, its screens and its scenery, with its layout (`MainMenu.uxml`) and look (`MainMenu.uss`) in `UI`, its scene in `Scenes` and the builder that generates it in `Editor`. The results panel and the pause menu use `MainMenu.uss` too, for the button styles.
- `Settings` holds the saved settings (`GameSettings`), the Settings screen and, in `UI`, its layout (`Settings.uxml`). Both the main menu and the pause menu show it.
- `Menus` holds what every menu screen is built from: the confirmation dialog (`Dialog.uxml`), the keyboard-or-pointer input mode, the delayed focus, the icons, and the panel settings and theme that the menu, the results panel and the pause menu share. All are built with UI Toolkit.
- `Audio` holds everything that sounds: `SoundLayer` for the gameplay effects, `MusicPlayer`, `MenuAudio` for the menu cues, the sound effects (`Sfx`), the mission track (`Music`), the menu track (`MenuMusic`) and the recordings some effects are shaped from (`Sources`). `Editor` holds the synth, the recipes and the builder that render the effects.
- `Visuals` holds the look all scenes share: the palette, the shader-driven shapes, lines, rings and dots, the camera rig and the space backdrop. `Shaders` holds the procedural shaders for the worlds, space backdrop, rings and fleet dots, `Materials` the generated materials (world surfaces in `Planets`, `Moons` and `Asteroids`, space backdrops in `Backdrops`, nothing else there), `Textures` the surface textures and `PostProcessing` the post-processing profile. `WorldLooks.asset` lists the surfaces each type of world can be given, its name dictionary and the space backdrops a mission can be shown against, and `WorldNames` holds the dictionaries, one text file per type. `Editor` holds the builder that generates the materials, the world looks and the profile.
- `SceneBuilding` holds what the scene builders share: the helper that sets serialized fields and the project's root folder.
- `MissionEditor` holds everything of the Mission Editor. `Missions` is the mission file format: the mission definition, the JSON reader and writer, and the validator. `Editing` is the editor's rules: the editing operations on a mission, and the session that carries out New, Open, Save and Quit and decides when work may be replaced or a file overwritten. Neither has Unity dependencies, a window or a scene. `View` is what the editor shows: its view, the map canvas, its dialogs, and the standalone shell with the Windows file dialogs. File selection and exit sit behind the shell, so the same editor can later be brought into the game with a shell of the game's own. `UI` holds its layout, look, theme and panel settings; unlike the menu it is laid out in real pixels instead of being scaled to the window. `Scenes` holds its scene, `Editor` the menu items that rebuild the scene and build the program, and `Tests` its tests.
- `Conventions/Tests` checks that every source file holds one top-level type named like the file.

The builders add these menu items. **Squadra > Rebuild Mission Scene** regenerates the mission scene, the materials, the world looks and the post-processing profile from code, and points the mission catalog at the scene and the world looks. It overwrites manual edits to those generated assets; the name dictionaries and the catalog's missions are left alone. **Squadra > Rebuild Main Menu Scene** does the same for the menu scene and puts it first in the build; it keeps the UI files and an existing mission catalog. **Squadra > Rewire Results Screen In Open Scene** adds the results screen to a mission scene that does not have it yet and leaves the rest of the scene alone; **Squadra > Rewire Pause Menu In Open Scene** does the same for the pause menu. **Squadra > Rebuild Mission Editor Scene** regenerates the Mission Editor's scene, and **Squadra > Build Mission Editor (Windows)** builds the Mission Editor from that scene alone; neither touches the game's scene list, so a game build still starts in the menu.

Every feature's tests are in its `Tests` folder. Run them all from **Window > General > Test Runner** (EditMode).

`AGENTS.md` holds the workflow, the architecture boundaries, the code conventions, the contracts a change must keep and how to verify and hand over a change. `.editorconfig` holds the formatting rules; `dotnet format whitespace --folder Assets/Squadra --verify-no-changes` checks them.

The specifications are in `specifications/`. `dart-battle-spec.md` is the current word on the basic unit's name and on combat; the older specifications keep their original wording and carry a note where it replaces them.

## Mission map

The number of missions on one map page is the **Missions per Page** property of the `Mission Map` object in the menu scene (1 to 10, default 5). With more missions than fit on a page, the map shows page arrows. To add a mission, add an entry to the mission catalog, give it an id that will never change, and assign its mission file (see Missions in the game).

Another scene can return to the menu on the mission map by calling `MainMenu.OpenCampaignMap()`; the results screen's Back to Menu does.

## Mission results

A mission's players come from its mission file: `player1` is Blue and `player2` is Red. The results table lists the human player (Blue) first and the others after it, including players that were eliminated or never fought. Neutral is never a player. Only Blue and Red are playable so far; the other four factions exist so that rosters and results can already hold six players.

The `Game` object's **Catalog** tells the results screen where the mission sits in the campaign. The mission that follows is always the next catalog entry, and the last entry is the end of the campaign. A mission scene played on its own, without being launched from the map, shows a plain Victory or Defeat with Restart and Back to Menu.

## Missions in the game

Every campaign mission is a Mission Editor file, played in the one shared mission scene (`Assets/Squadra/Match/Scenes/Mission.unity`). There is no scene per mission.

- **Assigning a mission.** Put the `.json` file in the project and assign it to its entry in the mission catalog (`Assets/Squadra/Campaign/Missions.asset`). The entry's id, the file's `id` and the file name must be the same, case included: `mission-01` and `mission-01.json`. Only assigned files are used; nothing is found by looking through folders, and the catalog's order is the campaign's. Files assigned there go into the build with the game.
- **Preparation.** When the menu starts, before it takes input, every assigned mission is read, checked and prepared, locked ones included. Nothing is played or advanced then. A mission that cannot be played is shown on the map as **UNAVAILABLE**, with the reason logged as an error (and as the dot's tooltip); the rest of the menu works as usual. An entry with no file stays **COMING SOON**. A mission that fails never unlocks the one after it, and Next Mission before it is disabled ("Next mission could not be loaded").
- **What the game can play.** The file must be valid for the Mission Editor, and on top of that: exactly two players, `player1` in blue (you) and `player2` in red (the opponent, which gives no orders of its own yet); each of them owning at least one node; every position times the distance scale within 100000 game units either way; and at most 1000000 starting Darts in all. Editor warnings (separate groups of nodes, nodes on the same spot, routes of no length) are logged and do not stop a mission; a map that is split in two may not be winnable.
- **The battlefield.** A node's position times the distance scale is its place in the game, X to the right and Y up, and its size is its body radius as it is; a route's travel time is its length (center to center, times the scale) divided by the fleet speed. Only the file's routes can be flown, both ways. When a mission starts its worlds are built, the view is fitted to all of them with room for what is drawn around them, and only then does the match begin. A small map is shown at the scale of the original battlefield; a large one is shown whole, and the HUD keeps its size on screen. There is no panning or zooming.
- **Names and surfaces.** Each world gets a name from its type's dictionary (`Assets/Squadra/Visuals/WorldNames/Planets.txt`, `Moons.txt`, `Asteroids.txt`, one name per line) and a surface from its type's material folder (`Assets/Squadra/Visuals/Materials/Planets`, `Moons`, `Asteroids`), independently of each other and of the owner. Within a mission no name or surface repeats until its list has run out, and then they are used evenly. They are chosen once per run of the game, when the missions are prepared, and kept through restarts, returns to the menu, replays and captures; the next launch may choose differently. The ring and glow around a world show its owner's color; the surface does not change with ownership. To add names, edit a dictionary; to add a surface, put a material using the `Squadra/Planet` shader in the type's folder and run **Squadra > Rebuild Mission Scene**, which lists it under a stable ID (its file name in lower case). A type with no surface or no names makes every mission that uses it unavailable.
- **Space backdrop.** Each mission is also given one space backdrop from `Assets/Squadra/Visuals/Materials/Backdrops`, chosen at the same time as the names and surfaces and kept for the run of the game in the same way. There is one backdrop so far (`Space`); to add another, put a material using the `Squadra/Space` shader in that folder and run **Squadra > Rebuild Mission Scene**. The main menu always shows `Space`. With no backdrop in the folder, no mission can be played.
- **Playing the scene on its own.** Opened and played directly, the mission scene plays the **Standalone Mission** set on its `Game` object (First Contact) outside the campaign: no progress is recorded and the results offer no Next Mission.

## Sound

`SoundLayer` plays an effect when a fleet departs, arrives peacefully, or starts a battle, for the weapon fire and Dart explosions of the battle as they are shown, when a world is captured, and when the match is won or lost. The shots and explosions of battles are limited per battle and are skipped while most voices are busy, so large battles do not drown everything else. The fleet and battle effects have several takes named `Name_1.wav`, `Name_2.wav`, and so on; one is picked at random each time, with a small random change in pitch and level. A capture and the two match results always sound the same. `MusicPlayer` loops the background track and keeps it playing across restarts; when a scene brings a different track, as a mission does after the menu, it fades the old one out and the new one in.

`MenuAudio` plays the menu's cues: one when the pointer enters a button, one when a button or tab is activated or the map page changes, and a different one for Back, Cancel and Esc. The three volume settings scale everything: Master all sound, Music both tracks, Sound Effects the menu cues and the gameplay effects. Pausing and Mute in Background are factors on top of these settings and never change them.

Most effects are generated from code. `Assets/Squadra/Audio/Editor/SoundRecipes.cs` describes each one, and **Squadra > Rebuild Sound Effects** renders them to WAV files in `Assets/Squadra/Audio/Sfx`, overwriting the previous render. Battle starts, weapon fire, captures, the match results and the menu cues are synthesized; departures and arrivals are shaped from the recordings in `Assets/Squadra/Audio/Sources`. The menu track (`MenuMusic/MenuTheme.wav`) is synthesized too; put a track of your own in that folder and rebuild the menu scene to use it instead. The Dart explosions are recordings used as they are (`DartDestroyed_N.ogg`). To add a take of your own, drop a file with the next number into the `Sfx` folder and run **Squadra > Rewire Sounds In Open Scene**. Volumes are set on the `Sounds` and `Music` objects in the scene.

## Credits

- Music: "Observing The Star" by yd, from [OpenGameArt](https://opengameart.org/content/another-space-background-track), released under CC0.
- Engine, force-field and explosion recordings: "Sci-Fi Sounds" by [Kenney](https://www.kenney.nl), released under CC0.
