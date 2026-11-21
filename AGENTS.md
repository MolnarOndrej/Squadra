# Project instructions

## Workflow

- **Feature specifications:** start with `grill-me`; ask only open product questions that change scope or behavior. When done, summarize the decisions and **end the turn**. Run `to-spec` only on an explicit go-ahead; it writes Markdown to `specifications/` with user stories, implementation decisions, behavior-level testing decisions, out-of-scope work and assumptions still to validate. A specification request is documentation only: implement or file issues only when asked.
- Read the nearby code and follow its patterns before editing. Keep the diff to what was asked: no unrelated fixes, formatting, dependency upgrades or regenerated assets. Record a defect you find in passing, with repro steps, for a separate change.
- **Refactors preserve behavior:** timing, event order, results, errors, input, navigation, visuals, audio, settings, file formats, persistence. Update every caller of a changed internal API. One subsystem at a time; commit pure moves/extractions separately from responsibility changes.

## Architecture

All code is in `Assets/Squadra`, one folder per feature (`README.md` > Project layout describes each). A feature holds its rules, scripts, UI, editor tooling, scenes, tests and assets in fixed subfolders: a rules folder (`Rules`, `Simulation`, `Preparation`, `Missions` or `Editing`), `View`, `UI`, `Editor`, `Scenes`, `Tests`. A feature without rules keeps scripts at its root. Shared things go to the feature that owns the idea (`Menus`, `Visuals`, `Audio`, `SceneBuilding`), never to kind-named folders (scripts, enums, helpers).

| Assembly | Folder | Notes |
| --- | --- | --- |
| `Squadra.Simulation` | `Match/Simulation` | The match |
| `Squadra.Preparation` | `Match/Preparation` | Mission file to playable mission and world looks; references `Squadra.Missions` and `Squadra.Simulation` |
| `Squadra.Campaign` | `Campaign/Rules` | |
| `Squadra.Pause` | `Pause/Rules` | Also holds `AudioMix`; leave it there |
| `Squadra.Menus` | `Menus/Rules` | Where a keyboard move goes along a declared line of menu controls |
| `Squadra.Sandbox` | `Sandbox/Rules` | Sandbox starting setup and session commands; references `Squadra.Simulation` |
| `Squadra.Missions` | `MissionEditor/Missions` | Mission file format |
| `Squadra.MissionEditing` | `MissionEditor/Editing` | References `Squadra.Missions` only |
| `Assembly-CSharp` (ns `Squadra`) | everything else | Unity, UI Toolkit, audio, `PlayerPrefs`, OS dialogs |
| `Assembly-CSharp-Editor` (ns `Squadra.EditorTools`) | every `Editor` folder | Scene builders |
| `Squadra.<Feature>.Tests` | every `Tests` folder | Edit Mode only |

- Rule assemblies are plain C# with `noEngineReferences`, tested without scenes. Dependencies point presentation → rules, never back; keep the graph acyclic. Change a boundary only for a named need.
- Rules reach the outside through interfaces they own (`IMissionFileStore`, `IMissionEditorShell`, `IMissionEditorPrompts`). A match reports through events and state; presentation shows it and decides no rule.
- Dependencies between presentation features are not compiler-checked. Don't add one without a reason, and state it in the change.
- Keep apart: the mission editor's JSON model vs. scene configuration (`WorldView`, `GameController`); session-only `CampaignSession` vs. persisted `GameSettings`.

## Code conventions

- One top-level type per file, named like the file (enforced by `TypeFileConventionTests`). Nest only private helpers; the only public nested type is `MissionCatalog.Entry`.
- No `partial` to split files; it exists only where a source generator needs it (`[UxmlElement]`, `[AutoStaticsCleanup]`).
- Use the game's vocabulary (Darts, fleets, capture, battles, missions, campaign); no `Utils`/`Helpers`/`Types` names; don't rename agreed concepts.
- One reason to change per type: separate rules, rendering, input, persistence and platform calls. Coordinators only coordinate; state changes stay with the owner (`Battle`, `Node`).
- Interfaces only for real boundaries or test doubles. No DI framework or service locator; pass collaborators via constructors or serialized fields. Deduplicate only the same rule.
- Comments explain why; keep existing timing and compatibility notes and move them with their code.
- Formatting follows `.editorconfig`.

## Contracts that break silently

- **Simulation order:** within one instant: production → capture completion → arrivals in launch order → battle casualties. One large time step equals many small ones. Don't weaken the tests that hold this.
- **Mission file format:** every field required, unknown/duplicate fields rejected, invariant-culture numbers that round-trip, UTF-8, atomic save, no silent overwrite of an externally changed file.
- **`.meta` files:** move/rename a script with its `.meta`. `MonoBehaviour`/`ScriptableObject` class name = file name.
- **Serialized fields:** renaming a `[SerializeField]` needs `[FormerlySerializedAs]`, and the builders set fields by name via `SceneWiring`: update them too.
- **UI Toolkit:** rename element names and USS classes in `.uxml`, `.uss` and C# queries together.
- **Path strings:** a move must update UXML `Style`/`Template` `src` (relative), builder path constants, `ProjectSettings/EditorBuildSettings.asset` and mission catalog scene paths.
- **Generated scenes:** `SquadraSceneBuilder`, `SquadraMenuBuilder`, `SquadraMissionEditorBuilder` generate the scenes; lasting scene changes go into the builder too. Don't regenerate as a side effect. `SquadraSandboxBuilder` only creates the Sandbox scene and adds missing structure: its worlds, samples and overrides are authored in the scene and must survive a rebuild.
- **Don't commit** changes to `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` or unrelated scene reserialization from play mode/saves.

## Verification

No hosted CI. Run what covers the change; run everything for a larger one. All tests passed when this was written, so a failure is yours to explain.

- **Tests:** `unity command test run -- --mode editor` (editor open), or Test Runner > EditMode > Run All.
- **Compile:** let Unity compile; check the Console for runtime and editor errors.
- **Format:** `dotnet format whitespace --folder Assets/Squadra --verify-no-changes` (drop the flag to fix).
- **Presentation/UI** (not covered by tests): play the affected scenes, `MainMenu/Scenes/MainMenu.unity`, `Match/Scenes/Mission.unity`, `MissionEditor/Scenes/MissionEditor.unity` (open `Sandbox/Scenes/Sandbox.unity` for a change to how the battlefield is drawn), and check missing scripts and lost serialized values after moves.
- **Builders:** run the changed builder's **Squadra** menu item; restore generated assets you didn't mean to change.

**Handover:** report what ran and what it showed, which scenes were played, and what was not verified. Never present a failing or skipped check as passed. For refactors, also say what was hard to maintain and who owns what now.
