# Project instructions

## Feature specifications

1. Start with `grill-me`. Use project context and agreed decisions to avoid repeating questions, and resolve open product decisions that materially affect scope or behavior.
2. When the interview is complete, summarize the agreed decisions and end the turn. Wait for the user to explicitly ask to proceed with `to-spec` before invoking it or drafting the specification.
3. After the user gives the go-ahead, use `to-spec` to write a Markdown specification in `specifications/`. Include user stories, implementation decisions, behavior-level testing decisions, and out-of-scope work. Record any assumptions that still need validation.

- Treat a specification-only request as documentation work. Implement the feature or publish to an issue tracker when the user asks for that step.

## Before you change code

- Read the code next to what you are changing, and the instructions that apply to it, before you edit. Follow the patterns already there; do not bring in a new one where an existing one does the job.
- Keep a change focused on what was asked. Leave unrelated cleanup, bug fixes, formatting, dependency upgrades and asset regeneration out of it.
- Run the verification that covers what you touched (see Verification) and report what you ran and what it showed. Say plainly when a check failed or when you could not run one; never present a failing or skipped check as passed.
- If you find a defect while doing something else, record it with enough detail to reproduce it and leave it for a change of its own.

## Architecture

All project code is under `Assets/Squadra`, one folder per feature. `README.md` describes each folder; the boundaries that matter are these.

**Features.** A feature folder holds everything of that feature: rules, Unity-side scripts, UI files, editor tooling, scenes and tests.

| Folder | What it is | Uses |
| --- | --- | --- |
| `Match` | One mission being played: the simulation, the scene that shows it, its builder. | `Visuals`, `Audio`, `Campaign`, `Results`, `Pause`, `MainMenu`, `SceneBuilding` |
| `Results` | The results screen. | `Menus`, `Audio`, `Visuals`, `Campaign` and `Match` rules |
| `Pause` | The pause menu and its rules. | `Match`, `Settings`, `Menus`, `Audio` |
| `Campaign` | Campaign rules, session progress, the mission catalog, the mission map. | `Menus`, `Audio` |
| `MainMenu` | The menu scene and its builder. | `Campaign`, `Settings`, `Menus`, `Audio`, `Visuals`, `SceneBuilding`, `Match` (its builder) |
| `Settings` | Saved settings and the Settings screen. | `Menus`, `Audio`, `Pause` rules |
| `Menus` | What every menu screen is built from: dialog, input mode, focus, icons, panel and theme. | `Audio` |
| `Audio` | Sound effects, music, menu cues, the clips and the sound tooling. | `Settings`, `Visuals`, `Pause` rules |
| `Visuals` | The shared look: palette, shapes, camera rig, backdrop, shaders, materials, textures. | `Match` rules (faction colors), `SceneBuilding` |
| `SceneBuilding` | What the scene builders share: setting serialized fields, the project's root folder. | nothing |
| `MissionEditor` | The standalone mission editor. | `Visuals`, `SceneBuilding`, `MainMenu` (to quit) |
| `Conventions` | The check of file organization. | nothing |

Inside a feature the subfolders are always the same: a rules folder (`Rules`, or named for what it holds: `Simulation`, `Missions`, `Editing`), `View` for the Unity-side scripts of a feature that has rules, `UI`, `Editor`, `Scenes`, `Tests`, and folders for its assets. A feature without rules keeps its scripts at its root. A folder with a single file is fine.

**Rules, in plain C# with no Unity dependencies.** Each rules folder is its own assembly with `noEngineReferences` set, and each is tested without a scene.

| Assembly | Folder | Holds |
| --- | --- | --- |
| `Squadra.Simulation` | `Match/Simulation` | The match: orders, time, arrivals, battles, capture, production, results. |
| `Squadra.Campaign` | `Campaign/Rules` | Mission states, unlocks, map pages, what the results screen offers. |
| `Squadra.Pause` | `Pause/Rules` | Pause screens, what Esc does, quit confirmation, audio levels. |
| `Squadra.Missions` | `MissionEditor/Missions` | The mission file format: definition, JSON reader and writer, validator. |
| `Squadra.MissionEditing` | `MissionEditor/Editing` | Editing operations and the editor session. References `Squadra.Missions` only. |

**Presentation and platform**, in `Assembly-CSharp` (namespace `Squadra`): every script outside a rules folder, an `Editor` folder and a `Tests` folder. This is where Unity objects, UI Toolkit, audio, scene loading, `PlayerPrefs` and operating-system dialogs live.

**Editor tooling**, in `Assembly-CSharp-Editor` (namespace `Squadra.EditorTools`): every `Editor` folder. The scene builders generate the scenes, materials and sounds from code.

**Tests**: every `Tests` folder is its own Edit Mode test assembly (`Squadra.Match.Tests`, `Squadra.Campaign.Tests`, `Squadra.Pause.Tests`, `Squadra.MissionEditor.Tests`, `Squadra.Conventions.Tests`).

- Dependencies point one way between layers: presentation and tooling use the rule assemblies, never the reverse. Do not add a Unity reference to a rule assembly, and keep the assembly graph acyclic. Change an assembly boundary only for a dependency or testing need you can name.
- The compiler enforces the rule assemblies only. The presentation scripts of all features compile into one assembly, so the "Uses" column above is what the code does today, not something a build checks. Some features use each other: `Audio` and `Settings`, `Match` and `Pause`, and the builders of `Match` and `MainMenu`. Do not add a dependency between features without a reason, and say so in the change when you do.
- Put a file in the feature it serves. Something several features use goes to the feature that owns the idea (`Menus` for menu building blocks, `Visuals` for drawing, `Audio` for sound), not into a folder named for its kind.
- `AudioMix` is in `Pause/Rules` although it is about sound: it is a rule in the `Squadra.Pause` assembly and namespace, and moving it would mean a new assembly for one class.
- A rule assembly reaches the outside world through an interface it owns (`IMissionFileStore`, `IMissionEditorShell`, `IMissionEditorPrompts`), and presentation implements it.
- A match reports what happened through events and state. Presentation shows it and decides no rule.
- The mission editor's JSON model and the playable scene's configuration (`WorldView`, `GameController`) are separate systems. Do not unify them unless asked.
- Campaign progress lasts for the session only (`CampaignSession`). Settings are persisted (`GameSettings`). Keep the two apart.
- A new self-contained feature gets one folder under `Assets/Squadra` with the same subfolders.

## Code conventions

- **One top-level type per file, named like the file.** This holds for classes, interfaces, structs and enums alike; an interface always has its own file. `TypeFileConventionTests` checks it.
- **Nested types are for private helpers.** A small type that only its owner uses may stay nested in it (`MissionCanvas.Gesture`, `FleetLayer.Garrison`). A type that other types use gets its own file. Two public nested types remain because only their owner uses them and Unity serializes them inline: `GameController.RosterEntry` and `MissionCatalog.Entry`.
- **Group by feature, not by kind.** A type lives in the folder of the feature it belongs to (see Architecture). There are no folders for scripts, enums, interfaces or helpers as such.
- **Keep a type in one file.** Do not split a class into partial declarations to make files shorter. The `partial` classes that exist are required by a source generator (`[UxmlElement]`: `MissionCanvas`, `MenuIcon`, `ResultsCelebration`; `[AutoStaticsCleanup]`: `CampaignSession`, `GameSettings`, `MusicPlayer`) and still have all their code in one file.
- **Name things for what they are for.** Avoid catch-all names such as `Types`, `Helpers` or `Utils`. Keep the game's vocabulary: Darts, fleets, capture, battles, missions, campaign. Do not rename a concept that code and specifications already agree on.
- **Give each type one reason to change.** Separate rules from rendering, input, persistence and platform calls. A coordinating class should read as coordination: it calls its collaborators and does not redo their work. State changes stay with the type that owns the state (`Battle` runs its casualty schedule, `Node` its capture and production clocks).
- **Be pragmatic about SOLID.** Introduce an interface when it marks a real boundary or allows a real substitute, such as a test double for the disk. Do not add one per class, and do not add extension points, base classes or layers for needs nobody has yet. Prefer composition. There is no dependency injection framework and no service locator; pass collaborators through constructors or serialized fields.
- **Size is a prompt, not a rule.** A long file or method is a reason to look for mixed responsibilities. There is no length limit, and splitting one operation across trivial wrappers makes code harder to read, not easier.
- **Remove duplication only when it is the same rule.** Code that merely looks alike but changes for different reasons stays separate.
- **Comments say why.** Explain intent, invariants, ordering that is not obvious, platform constraints and trade-offs. Do not restate the code. Keep the existing explanations of timing and compatibility guarantees, and update or remove a comment when the code it describes moves.
- **Formatting** follows `.editorconfig`: four spaces, braces on their own line, usings sorted with `System` first and none unused.

## Unity precautions

- A script's `.meta` file is its identity. Move or rename a script together with its `.meta`, so scenes, prefabs and assets keep pointing at it. A `MonoBehaviour` or `ScriptableObject` class must keep the name of its file.
- Serialized field names are data. Renaming a `[SerializeField]` field loses its value in every scene and asset unless you add `[FormerlySerializedAs]`. The scene builders also set fields by name through `SceneWiring`, so update them in the same change.
- UI Toolkit looks elements up by name and class. When you rename a custom element, a UXML name or a USS class, update the `.uxml`, `.uss` and the C# that queries them together.
- Some paths are written down as text and do not follow a moved file: the `src` of a `Style` or `Template` in a `.uxml` (relative to that file, such as `../../Menus/UI/Dialog.uxml`), the path constants in the scene builders, and the scene paths in `ProjectSettings/EditorBuildSettings.asset` and in the mission catalog. Update them in the change that moves the file.
- The scenes are generated by `SquadraSceneBuilder`, `SquadraMenuBuilder` and `SquadraMissionEditorBuilder`. A lasting change to how a scene is put together belongs in the builder as well as in the scene. Do not regenerate scenes or assets as a side effect of an unrelated change.
- Play mode and scene saves can modify `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` and re-serialize unrelated scene values. Do not commit those.
- Keep the mission file format strict and stable: every field required, unknown and duplicate fields rejected, numbers written in invariant culture so they read back exactly, UTF-8, atomic replacement on save, and no silent overwrite of a file another program changed.

## Refactoring

- A refactor preserves behavior: timing, the order of events, results, error handling, input, navigation, visuals, audio, settings, file formats and persistence. Internal APIs may change if every caller is updated.
- Keep mechanical moves (extracting or relocating a type unchanged) in a separate commit from changes to responsibilities, so each can be reviewed for what it is.
- Refactor one subsystem at a time. Say what was hard to maintain, who owns what now, and what you verified.
- Within one instant the match resolves production, then capture completion, then arrivals in launch order, then battle casualties. One large time step must give the same result as many small ones. The tests hold both; do not weaken them.

## Verification

Run what covers your change, and all of it before handing over a larger one.

- **Tests.** All tests are Edit Mode tests, each feature's in its `Tests` folder: `Match/Tests` (match, battles, results), `Campaign/Tests`, `Pause/Tests`, `MissionEditor/Tests` (mission format, editing, files) and `Conventions/Tests` (file organization). In Unity: **Window > General > Test Runner > EditMode > Run All**. With the `unity` command-line tool and the project open in the editor: `unity command test run -- --mode editor`. Every test passed when these instructions were written, so a failure is yours to explain.
- **Compilation.** Let the editor compile after your change and check the Console for errors in both runtime and editor code.
- **Formatting.** `dotnet format whitespace --folder Assets/Squadra --verify-no-changes` reports formatting that differs from `.editorconfig`; the same command without `--verify-no-changes` fixes it. It needs the .NET SDK and no project file.
- **Scenes and UI.** The tests do not cover `MonoBehaviour` code or UI. After changing presentation code, play the scenes it affects and look: `Assets/Squadra/MainMenu/Scenes/MainMenu.unity`, `Assets/Squadra/Match/Scenes/SampleScene.unity` and `Assets/Squadra/MissionEditor/Scenes/MissionEditor.unity`. Check input, fleets and battles, pause and resume, the results screen, menu navigation, settings, and the editor's workflows as far as your change reaches. Look for missing scripts and lost serialized values after a move or rename.
- **Scene builders.** After changing a builder, run its menu item under **Squadra** and check that it finishes without errors. Restore generated assets you did not mean to change.

There is no hosted CI. These checks run locally.

## Review checklist

- [ ] **Findable.** Each top-level type is in a file of its own name, in the folder of the feature it belongs to.
- [ ] **One responsibility.** Each new or changed type has one reason to change, and coordinating code only coordinates.
- [ ] **Dependency direction.** Rule assemblies still have no Unity references; presentation depends on rules, not the reverse.
- [ ] **Abstractions earn their place.** Every new interface, base class or layer marks a real boundary or has a real second implementation.
- [ ] **Contracts kept.** Script `.meta` files, serialized field names, UXML and USS names, the mission file format and saved settings are unchanged, or the change is deliberate and stated.
- [ ] **Focused diff.** Moves are separate from logic changes, and nothing unrelated came along: no stray formatting, regenerated assets or font atlas changes.
- [ ] **Evidence.** The description says which tests and checks ran, which scenes were played, and what could not be verified.
