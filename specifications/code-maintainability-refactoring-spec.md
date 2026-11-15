# Squadra code maintainability and reviewability refactoring

## Problem Statement

I find the current AI-generated code difficult to read because files combine many types and some classes handle too many responsibilities. Understanding a change requires reading unrelated code, and it is difficult to judge whether an agent has followed the project's conventions. I want smaller, cohesive units, predictable organization, and explicit guidance so that code reviews are easier and the codebase remains maintainable as it grows.

## Solution

Refactor all project-owned C# code in small, independently reviewable stages while preserving existing behavior. Put each top-level type in its own matching file, keep related code grouped by feature, and separate responsibilities where the current code mixes concerns. Preserve the existing separation between pure C# domain logic and Unity presentation.

Apply clean-code techniques and SOLID principles when they make a concrete responsibility, dependency, or testing boundary easier to understand. Success is readable code with clear ownership and reliable behavior, rather than a particular number of classes or abstractions.

During implementation, update the root `AGENTS.md` with the agreed conventions, architecture boundaries, verification requirements, and expectations for focused changes. Add a short review checklist and lightweight automated checks so future agents and human contributors can consistently follow these rules.

## User Stories

1. As a maintainer, I want each top-level type in a matching file, so that I can locate it by name without searching unrelated declarations.
2. As a maintainer, I want every top-level interface in its own matching file, so that contracts are as easy to find and review as implementations.
3. As a maintainer, I want classes, structs, and enums to follow the same file convention, so that organization is predictable throughout the project.
4. As a maintainer, I want related types grouped by feature, so that I can understand a subsystem without navigating folders organized only by language construct.
5. As a maintainer, I want small private implementation types to remain nested where useful, so that their ownership stays obvious.
6. As a reviewer, I want mechanical file moves separated from changes to responsibilities and dependencies, so that I can distinguish relocation from substantive changes.
7. As a reviewer, I want each stage to address a coherent subsystem or concern, so that I can review it independently.
8. As a maintainer, I want each class to have a focused responsibility, so that a change does not require understanding unrelated behavior.
9. As a maintainer, I want clear names and straightforward control flow, so that the code communicates its intent without excessive explanation.
10. As a maintainer, I want comments to explain constraints and reasons, so that important knowledge survives without comments merely repeating the code.
11. As a maintainer, I want genuinely shared behavior extracted carefully, so that duplication is reduced without coupling unrelated features.
12. As a maintainer, I want dependencies to be explicit, so that I can understand what a unit needs and which side effects it can cause.
13. As a maintainer, I want interfaces introduced for useful contracts, so that I gain clarity or meaningful testability without unnecessary indirection.
14. As a maintainer, I want domain rules to remain independent of Unity presentation, so that gameplay and editing behavior can be understood and tested without scenes.
15. As a player, I want gameplay, visuals, controls, audio, and navigation to behave as before, so that internal cleanup does not change my experience.
16. As a mission author, I want existing mission files and editing workflows preserved, so that refactoring does not damage or reinterpret my work.
17. As a maintainer, I want Unity references and generated scene workflows preserved, so that code organization changes do not break assets or scene wiring.
18. As a reviewer, I want existing behavior tests retained and coverage gaps addressed where relevant, so that structural changes have useful regression evidence.
19. As a reviewer, I want affected scenes and UI checked directly, so that correctness includes behavior outside the current domain test assemblies.
20. As a contributing agent, I want repository instructions describing architecture and coding conventions, so that my changes fit the codebase without relying on prior chat history.
21. As a reviewer, I want a concise checklist and repeatable automated checks, so that routine consistency issues do not dominate review time.
22. As a maintainer, I want discovered bugs recorded separately, so that intentional behavior changes are not hidden inside a refactoring.

## Implementation Decisions

### Scope and preservation contract

- Cover all project-owned C# code: simulation, campaign, pause, menus, presentation, mission definitions and serialization, mission editing, mission-editor views, Unity editor tooling, and tests. Exclude third-party, package, and generated code from manual cleanup.
- Preserve externally observable behavior, including timing, ordering, results, error handling, cancellation, input, navigation, visuals, audio, settings, and persistence. Preserve file formats and compatibility with existing project assets.
- Record discovered defects separately with enough evidence to reproduce them. Do not silently fix them within a structural refactor. A behavior change requires separate authorization and review.
- Internal APIs may change to support clearer responsibilities. Update their callers and retain coverage of the original behavior. Preserve serialized contracts, external file contracts, and Unity registrations even when an internal API changes.

### Type and file organization

- Require exactly one top-level class, interface, struct, or enum per source file, with the filename matching the type name. Every top-level interface has a separate file; interfaces are not an exception.
- Keep small private helper types nested inside their owning type when they support only that owner and improve local understanding. Do not nest shared types merely to evade the file convention.
- Organize files by feature or subsystem. Do not introduce repository-wide folders for enums, interfaces, structs, or generic helpers.
- Use specific filenames and type names that describe purpose. Replace catch-all collections of types with individually named files.
- Preserve existing namespaces and accessibility during mechanical extraction. Any later namespace or visibility change must have a concrete reason and account for all relevant references.
- Keep a type in a single file by default. Do not split a large class into partial declarations merely to reduce individual file lengths. If a platform constraint requires an exception, document its reason.

### Readability and focused responsibilities

- Separate decisions and rules from rendering, input processing, persistence, and platform integration where these responsibilities are currently combined. Keep orchestration readable: it should coordinate collaborators rather than reimplement their work.
- Give each extracted unit a coherent responsibility and clear owner. Size alone is not sufficient justification for an extraction, and moving code must not simply scatter a single operation across many trivial wrappers.
- Prefer explicit names, straightforward branches, and methods that operate at a consistent level of detail. Reduce deeply nested control flow where doing so clarifies behavior.
- Preserve useful domain vocabulary, including Darts, fleets, capture, battles, missions, and campaign progression. Avoid synonym churn that makes existing code and specifications harder to relate.
- Remove duplication when it represents the same rule or operation. Keep superficially similar code separate when the behaviors have different reasons to change.
- Comments explain intent, invariants, non-obvious ordering, platform constraints, and tradeoffs. Preserve existing explanations of compatibility and timing guarantees. Remove or update stale comments as code moves.
- Treat long files and methods as prompts for review, not rigid numerical failures. Do not add arbitrary maximum-length rules or optimize for line-count reductions.

### Architecture and pragmatic SOLID

- Preserve the existing pure C# boundaries for simulation, campaign, pause, missions, and mission editing. Do not introduce Unity engine dependencies into these domain assemblies.
- Keep presentation and platform integrations responsible for Unity objects, UI, audio, scene navigation, and concrete file or operating-system interactions. Domain behavior must remain independently exercisable at its existing public boundaries.
- Apply single responsibility by identifying distinct reasons to change. Keep state transitions and invariants with the code that owns them rather than distributing them among unrelated helpers.
- Apply dependency inversion and interface segregation when a contract clarifies an actual boundary or supports a meaningful alternative implementation or test substitute. Do not require an interface for every class or introduce a new abstraction solely to claim SOLID compliance.
- Respect behavioral contracts when using inheritance or implementations of an interface. Prefer composition for separated responsibilities unless inheritance expresses a real, substitutable relationship.
- Introduce extension points only for demonstrated needs. Do not build speculative plugin systems or add layers for hypothetical future features.
- Make collaborator ownership, lifecycle, and dependencies explicit. Use existing project patterns where adequate; a new dependency injection framework or service locator is not part of this work.
- Preserve existing assembly boundaries by default. Any assembly adjustment must be justified by an identified dependency or testing need, remain acyclic, and preserve runtime/editor separation.
- Keep the mission-editor JSON model and playable scene configuration as their current separate systems. Unifying them is a feature and architecture change outside this refactoring.

### Areas requiring responsibility review

- Review match orchestration for separation of scheduling, orders, arrivals, battle progression, and results accounting while retaining the match's public behavior boundary and ownership of simulation invariants.
- Review mission serialization for separation of syntax parsing, schema interpretation, numeric handling, and serialization. Preserve strict validation and diagnostic behavior.
- Review mission-editor session and view code for separation of editing rules, selection and tool state, invalid-input handling, file lifecycle, prompt orchestration, UI binding, and rendering.
- Review mission canvas and fleet presentation for cohesive boundaries around gestures, coordinate conversion, rendering, formations, effects, and sound coordination.
- Review game-controller and scene-builder orchestration for separable responsibilities in configuration, input, presentation coordination, navigation, and generated scene composition.
- Review remaining menus, settings, campaign, pause, audio, utilities, and test fixtures against the same conventions. These examples identify areas to assess, not a mandate to extract a particular class for each listed responsibility.

### Unity and data compatibility

- Preserve script asset identity and existing metadata GUIDs when moving or renaming existing scripts. New extracted types receive their own appropriate metadata. Keep attached component scripts associated with their original identity.
- Preserve scene, prefab, asset, serialized field, and component references. If a serialized name must change, explicitly retain compatibility and update any editor tooling that addresses the field by name.
- Preserve UI Toolkit element registration, UXML/USS bindings, and runtime/editor type discovery. Review non-C# references when moving or renaming a type.
- Keep scene-builder output consistent with the maintained scenes. Durable construction changes belong in the builder as well as any affected maintained asset; avoid unnecessary regeneration and unrelated asset churn.
- Preserve strict mission format validation, invariant numeric formatting, numeric round trips, UTF-8 handling, atomic replacement, and external-modification detection.
- Preserve session-only campaign progression separately from persisted settings, along with stable mission identities and existing launch-readiness behavior.

### Staged delivery and review evidence

1. Establish the baseline: inventory project-owned types, assembly dependencies, current verification commands, and known failures. Record results before changing behavior-sensitive code.
2. Perform mechanical type extraction in coherent batches. Preserve declarations, namespaces, access, logic, and Unity identity. Keep formatting churn out of these batches except where necessary for a valid extracted file.
3. Refactor responsibilities one subsystem at a time. Explain the original maintenance problem, new ownership and dependencies, and any tradeoff. Separate these changes from the mechanical moves so each can be reviewed independently.
4. Add or refine behavior coverage when a stage exposes a relevant gap. Update repository guidance and lightweight checks to describe and enforce the resulting conventions.
5. Complete an integration pass across the affected project and record final verification and remaining limitations.

- A stage must compile and pass its relevant checks before dependent work proceeds. A known baseline failure must remain visible and be investigated enough to distinguish it from a regression; do not present a failing baseline as a successful check.
- Keep unrelated bug fixes, dependency upgrades, broad formatting, asset regeneration, and feature changes out of refactoring stages.
- Each review summary states what changed, why the boundary is clearer, which behavior was verified, and any remaining risk. Include enough movement information for a reviewer to follow extracted code.

### Repository guidance and lightweight checks

- Update the root `AGENTS.md` during implementation. The instructions must be self-contained for a future agent with no access to this conversation.
- Document the one-type-per-file rule explicitly for interfaces, classes, structs, and enums; feature grouping; nested-helper exception; naming and comment expectations; and pragmatic use of SOLID.
- Document existing domain/presentation boundaries, dependency expectations, Unity identity and serialization precautions, and the requirement to preserve behavior during refactors.
- State that agents should inspect nearby code and applicable instructions before editing, make focused changes, use established patterns, run relevant verification, and report failures or checks they could not perform accurately.
- Preserve the existing specification interview and approval workflow when extending the instructions.
- Provide a concise review checklist covering discoverability, responsibility, dependency direction, appropriate abstractions, preserved contracts, focused diffs, and verification evidence.
- Establish consistent formatting using tooling compatible with the project's Unity and C# versions. Add a repeatable check for the top-level type and filename convention, accounting for the documented exclusions and nested types.
- Reuse existing checking infrastructure where available. Do not add a large analysis framework, impose a rigid length limit, or require a new hosted CI service. Document how to run the adopted checks locally; integrate with existing CI if suitable infrastructure already exists.

## Testing Decisions

- Prefer tests of observable behavior at the highest existing useful boundary. Retain the current domain seams rather than creating tests for every extracted collaborator.
- Use the existing Edit Mode suites for match, battle, results, campaign, and pause behavior, together with mission-format, file-handling, and editing tests. Preserve meaningful assertions; do not weaken them to accommodate structural changes.
- Test outcomes, state transitions, errors, and interactions visible through a contract. Avoid assertions about private methods, class counts, internal call sequences, or the precise implementation of an extraction. File-organization checks are convention checks, separate from behavior tests.
- Establish and record the current test baseline. Run relevant tests after each affected stage and run the full existing test suites at integration completion. Verify compilation of runtime and editor code in the project's supported Unity environment.
- For simulation changes, preserve equivalence between large and small advancement steps, simultaneous-event ordering, fleet arrivals, capture, reinforcement, battle casualties, terminal results, and pause-related timing. In particular, retain production before capture completion, then arrivals in launch order, then battle casualties for simultaneous events.
- For mission changes, preserve accepted and rejected documents, unknown and duplicate field handling, numeric precision and culture independence, read/write round trips, diagnostic behavior, and existing-format compatibility.
- For editing and file lifecycle changes, preserve dirty-state no-ops, invalid-input behavior, selection and tools, save and reopen, atomic writes, external changes, failed operations, cancellation, and unsaved-work prompts. Use established temporary-file and substitute-boundary patterns.
- For campaign, pause, settings, and results changes, preserve progression, availability, restart and navigation, result accounting, settings persistence, focus behavior, and quit confirmation.
- Verify affected Unity scenes and UI directly because current domain tests do not cover every MonoBehaviour or UI interaction. Check gameplay input, fleet and battle presentation, pause and resume, results, menu navigation, settings, and relevant mission-editor workflows. Compare against the pre-refactor behavior and visual baseline.
- Check for missing scripts, lost serialized values, broken bindings, registration failures, and scene-builder failures after moves or renames. Validate buildability using the existing supported build path without changing target platforms.
- Add targeted behavior coverage only where needed to protect a changed responsibility boundary or a discovered gap. Mechanical moves do not require redundant tests that mirror existing implementation.
- Run the adopted formatting and type-organization checks. Review whether extracted units improve cohesion and navigation; automated checks alone do not establish maintainability.

## Out of Scope

- Implementing the refactor or updating `AGENTS.md` as part of this specification-only change.
- Publishing this specification to an issue tracker without a separate request.
- Gameplay changes, balance changes, new features, intentional UI or audio redesigns, or changing persistence semantics.
- Fixing discovered behavioral defects within refactoring changes.
- Mission format migrations, campaign persistence, or unifying mission-editor files with playable scene configuration.
- A wholesale architecture rewrite, framework migration, new dependency injection package, or speculative extension framework.
- An interface for every class, artificial inheritance hierarchies, or abstractions introduced only to satisfy a principle by name.
- Hard file/method length limits, arbitrary coverage percentages, or a requirement that every method receive its own test.
- Manual cleanup of third-party packages, generated sources, or unrelated assets.
- Dependency upgrades, new platforms, new hosted CI infrastructure, or performance optimization projects. Existing performance-sensitive behavior must still be protected from regressions.

## Further Notes

- The interview is complete. Scope, behavior preservation, file conventions, architecture approach, staged delivery, and repository guidance were explicitly agreed. Every top-level interface having a separate matching file is a requirement.
- The current repository review found 64 project C# files and approximately 20,885 lines, with eight test files. These are planning observations, not acceptance targets or executed test results.
- Existing domain assemblies already provide useful separation and behavior tests. This work should strengthen those boundaries rather than replace them without evidence.
- Implementation must validate the exact extraction boundaries, current supported build and test commands, formatter/analyzer compatibility, available CI, and handling of any language or generated-code exceptions. Record justified exceptions explicitly rather than silently relaxing a convention.
- Establish visual and interaction baselines before changing presentation responsibilities. The appropriate capture method and representative scenes/workflows must be confirmed from the current project during implementation.
- Do not assume passing domain tests proves Unity integration, file compatibility, or unchanged performance. Validate affected integration paths and investigate any observed responsiveness, timing, or allocation regression using focused evidence.
- Completion requires the full project-owned scope to be assessed, required type extraction completed, identified mixed responsibilities resolved or explicitly justified, guidance and checks delivered, and verification evidence recorded. A smaller file count, smaller average file size, or increasing the number of interfaces does not establish completion.
