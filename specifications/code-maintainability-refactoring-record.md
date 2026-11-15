# Code maintainability refactoring: implementation record

This records how `code-maintainability-refactoring-spec.md` was carried out on the branch `claude/code-maintainability-refactoring`: what each stage changed, what was verified and how, which exceptions were left on purpose, and what was noticed and not touched. The conventions themselves are in `AGENTS.md`.

## Baseline

Taken on `master` at `6ef5dc8`, Unity 6000.7.0b8, before any change.

- 64 project C# files, 20,885 lines; 20 of the 64 held more than one top-level type.
- Edit Mode tests: 363 of 363 passed. There was no known failing test.
- Assemblies: `Squadra.Simulation`, `Squadra.Campaign`, `Squadra.Pause`, `Squadra.Missions` (none references another, none references Unity), `Squadra.MissionEditing` (references `Squadra.Missions`), two test assemblies, and Unity's `Assembly-CSharp` and `Assembly-CSharp-Editor`. This is unchanged except for one added test assembly.
- No CI and no formatter configuration existed.
- Play-mode baselines were recorded before any presentation code changed. Each is a scripted run that writes what the scene and the UI show to a text file, with screenshots:
  - **Mission editor**: add nodes, connect, select, click a route, drag a node, pan, zoom, type an invalid value, answer the invalid-input and remove-player dialogs, add a player, cause warnings, delete, ask for a new mission.
  - **Mission scene**: a match at a fixed time step of 1/60 s with a seeded random generator: select and order, fleets in transit, a landing, a battle, pause and resume, a capture, a second battle, victory and the results screen. Two baseline runs gave identical output, so every later difference would be a real one.
  - **Main menu**: an arrow key, a pointer move, a repeated pointer position, the map, the settings and the quit dialog. Recorded before the menu change only.
  - **Scene builders**: the scenes, materials and volume profile the three builders generate, kept for comparison and not committed.

## Stages

Each stage is one commit. Mechanical moves are separate from changes to responsibilities.

| Commit | Stage | What moved |
| --- | --- | --- |
| `4029eff` | One type per file | 63 types left the 20 shared files unchanged. `MatchTypes.cs`, `MissionEditorHost.cs` and `MissionFiles.cs` became `MatchResults.cs`, `Selection.cs` and `LocalMissionFiles.cs`. A line-by-line comparison showed one line gone: a divider comment. |
| `ce163e0` | Simulation | `MatchRoster` (new) keeps the roster and combat totals. `Battle` runs its casualty schedule and `Node` its capture and production clocks. `Match` keeps orders, the time loop, event order, arrivals and the result. |
| `c68f642` | Mission format | `MissionJson` remains the entry point; `MissionJsonWriter`, `MissionJsonBinder` and `JsonNumberLiteral` (new) hold writing, schema interpretation and number literals. `JsonParser` already held the syntax. |
| `6b4ca81` | Mission editing | `MissionFileOperations` (new) carries out reading and saving a file with their messages; `InvalidInputs` (new) lists fields with invalid text; `EditorWording` (new) holds shared wording. The session keeps its state and every decision. |
| `dc531d7` | Mission editor view | Four panel classes, `MissionTextEntry`, `MissionEditorRows`, `MissionEditorPrompts` and `PlayerColors` come out of `MissionEditorView`. `MapViewport`, `MissionCanvasPainter` and `MissionCanvasLabels` come out of `MissionCanvas`, which keeps gestures and hit testing. |
| `b220a3c` | Fleet presentation | `DartFormations` (geometry) and `DartEffects` (shots, explosions, sparks) come out of `FleetLayer`. |
| `4a9135b` | Game controller | `PlayerOrders`, `RouteLanes` and `MatchFeedback` come out of `GameController`, which keeps configuration, the frame loop, the pause and navigation. |
| `4ae1b48` | Editor tooling | `SquadraAssetBuilder`, `SceneMaterials` and `SceneWiring` come out of `SquadraSceneBuilder`. |
| `c26cf6c` | Menus | `MenuInputMode` and `MenuFocus` replace code the main menu, pause menu and results screen each had. `MenuScreen` leaves `MainMenu`. |
| `56dab2a` | Remaining shared types | `SynthWave`, `SynthFilter` and `Recording` leave `SoundSynth`. |
| `f24e04a` | Formatting | `.editorconfig`, and the twelve places where the code differed from it. |
| `f22342b` | Guidance and checks | `AGENTS.md`, `TypeFileConventionTests`, a pointer in `README.md`. |

After these stages there are 160 project C# files and 22,025 lines. Neither number was a goal.

## Regrouping by feature

A later commit moved every file into a folder for its feature, as `MissionEditor` already was: `Match`, `Results`, `Pause`, `Campaign`, `MainMenu`, `Settings`, `Menus`, `Audio`, `Visuals`, `SceneBuilding` and `Conventions`. `AGENTS.md` lists what each holds and uses. The folders `Scripts`, `Editor`, `UI`, `Tests`, `Shaders`, `Materials` and `Textures` under `Assets/Squadra`, and `Assets/Scenes`, are gone.

- Every file moved with its `.meta`, so its identity is unchanged. No namespace, type name or rule assembly changed. A line-by-line comparison of all C# files before and after shows only the path constants and messages of the builders; the builders' root folder constant moved from `SquadraSceneBuilder` to the new `ProjectFolders`, so that `Visuals` and `MissionEditor` no longer reach into `Match` for it.
- The one test assembly became one per feature (`Squadra.Match.Tests`, `Squadra.Campaign.Tests`, `Squadra.Pause.Tests`), and `Squadra.Tests.Conventions` became `Squadra.Conventions.Tests`. The tests and their names are the same.
- Paths written as text were updated: the builders' constants, the `src` references of three `.uxml` files, the mission catalog's scene path and the build scene list.
- `AudioMix` stays in `Pause/Rules`, in the `Squadra.Pause` assembly, although it is about sound. `MainMenu.uss` stays with the main menu although the results panel and the pause menu use it too.
- Verified: 364 of 364 tests pass; formatting and unused usings are clean; the mission editor and match scenarios give output identical to the baseline; the main menu scenario gives the same elements, classes, texts and focus (Unity lists an element's classes in an order that differs between editor sessions, so classes were compared as sets); the pause menu, results screen and main menu are styled as before; the three builders run from their new folders and generate the same scenes and materials; a Windows player with the moved scenes builds without errors.

## Assessed and left as they are

These were reviewed against the same conventions and found to have one responsibility each; none was changed beyond the type extraction.

- Campaign and pause rules (`CampaignRules`, `CampaignProgress`, `MissionPages`, `ResultsPlan`, `PauseFlow`, `QuitPrompt`, `AudioMix`).
- `MissionDocument`, `MissionValidator`, `MissionFields`, `LocalMissionFiles`, `JsonParser`.
- `WorldView`, `HudView`, `EffectsLayer`, `SoundLayer`, `MusicPlayer`, `CameraRig`, `Backdrop`, `Palette`, `Shapes`, `ResultsCelebration`.
- `GameSettings`, `SettingsScreen`, `MissionMap`, `MissionCatalog`, `CampaignSession`, `MenuDialog`, `MenuAudio`, `MenuIcon`, `MenuScenery`.
- `SoundRecipes`, `SoundSynth`, `SquadraSoundBuilder`, `SquadraMenuBuilder`, `SquadraMissionEditorBuilder`, the mission editor's dialog and shells.
- The test fixtures: each file already held one fixture, with its fakes nested in it.

## Exceptions kept on purpose

- **`MissionEditorSession` keeps selection and tools** (655 lines). They are the session's own state transitions and share its busy guard and its invalid-input questions; a separate class would need that state handed back and forth.
- **`FleetLayer` keeps garrisons and transits together** (663 lines). A dot passes from a transit to a garrison without leaving the screen, and both read the same serialized tuning.
- **Two public nested types remain**: `GameController.RosterEntry` and `MissionCatalog.Entry`. Only their owner uses them, and Unity serializes them inline.
- **Six classes are `partial`** because a source generator requires it (`[UxmlElement]`, `[AutoStaticsCleanup]`). Each still has all its code in one file.
- **A file switched off entirely by a platform directive** (`WindowsFileDialogs.cs`) would be reported by `TypeFileConventionTests` on a platform where the directive is false. The project builds for Windows only.

## Verification

- **Edit Mode tests**: run after every stage; 363 of 363 passed each time, and 364 of 364 once the convention test existed. No test was changed or weakened.
- **Convention test**: passes on the result. Adding a second type to a file made it fail with a message naming the type; that change was reverted.
- **Formatting**: `dotnet format whitespace --folder Assets/Squadra --verify-no-changes` exits clean.
- **Unused usings**: a build of the Unity-generated solution with `IDE0005` enabled reports none.
- **Mission editor scenario**: the UI tree, classes, field values, statuses, dialogs, label positions, zoom and mission text are identical to the baseline after the view stage and at the end.
- **Mission scene scenario**: all 955 lines are identical to the baseline after the fleet and controller stages, after the menu stage and at the end: match state at every checkpoint, dot, shot and spark counts, every text in the scene, renderer visibility, and the pause menu and results screen trees.
- **Main menu scenario**: identical before and after the menu stage and at the end.
- **Scene builders**: the scenes, materials and volume profile generated after the tooling stage equal those generated before it once file IDs are normalized. The generated assets were restored from git.
- **Player build**: a Windows player with the menu, mission and mission editor scenes built without errors. Its output was discarded and the settings files the build rewrote were restored.

## Not verified

- **Sound by ear.** Sound calls moved unchanged with the code that makes them; nobody listened.
- **Physical input.** Scenarios drove the game through its public methods and synthesized UI events. Reading the real mouse (`PlayerOrders.ReadPointer`) and the mission editor's keyboard shortcuts moved unchanged but were not exercised with a device.
- **Native file dialogs.** Open and Save run through fakes in the tests; the Windows dialogs were not opened.
- **The built player was not started.**
- **`SquadraSoundBuilder` was not run**, because it regenerates the audio files. Its change is three type names and it compiles.
- **Performance** was not measured. The frame loop does the same work; the mission canvas now reaches its content rectangle through one delegate call.

## Noticed and not changed

These predate the work. None was fixed here.

1. **Static-state analyzer warnings.** Building the generated solution reports `UAL0010` and `UAL0013` (a type with static fields needs `AutoStaticsCleanup` or `NoAutoStaticsCleanup`) for `EditResult`, `Selection`, `LocalMissionFiles` and `Shapes`. To reproduce: `dotnet build Squadra.slnx`.
2. **The committed scenes differ from what the builders generate.** `SampleScene.unity` and `MainMenu.unity` have layout values that depend on the Game view's shape when the builder ran, and lack fields added since they were last built (`gatherSeconds`, `frontGap`, `settleSeconds` and the other battle tuning on `FleetLayer`; `pauseSeconds`). The scenes work because the fields take their defaults. To reproduce: run **Squadra > Rebuild First Playable Scene** and look at the diff.
3. **Play mode leaves traces.** It rewrites `LiberationSans SDF.asset`, and a capture of the Game view leaves an empty `Assets/Resources` folder. Restoring the font asset from git while the editor is open made some glyphs go missing in the mission editor's text until the editor is restarted; `master` shows the same in the same editor session, so it is the editor's font atlas and not the code.
