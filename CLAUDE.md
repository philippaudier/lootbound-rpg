# Lootbound — Instructions Claude Code

## Project identity

This repository contains **Lootbound**, a solo first-person procedural exploration RPG developed with **Unity 6 and the Universal Render Pipeline**.

Lootbound is not a direct port of the former Minecraft mod LootboundRPG. The mod is a source of tested ideas, not an architecture to reproduce.

The core loop is:

```text
Refuge
→ Preparation
→ Expedition
→ Exploration
→ Encounter
→ Decision
→ Loot
→ Return
→ Repair
→ Enhancement
→ New expedition
```

The project is developed through small playable slices.

Each system begins with a simple V1 that is:

* functional;
* testable;
* understandable;
* integrated into the existing game loop;
* extensible only where a concrete future need is already known.

## Current technology

* Unity 6
* Universal Render Pipeline
* C#
* New Unity Input System
* Single-player
* First-person
* Windows is the initial target platform

Before making assumptions about package versions or project configuration, inspect:

* `ProjectSettings/ProjectVersion.txt`
* `Packages/manifest.json`
* `Packages/packages-lock.json`
* existing Assembly Definitions
* existing project folders and scenes

## Absolute development rules

* Never begin a future slice unless explicitly requested.
* Never reproduce every system from the Minecraft mod at once.
* Never introduce multiplayer or networking infrastructure.
* Never create a generic engine or reusable framework instead of the requested game feature.
* Never silently replace an existing architecture.
* Never delete existing assets or settings without explaining why.
* Never make broad changes to URP, quality settings, input settings, or project settings without first inspecting their current state.
* Never claim that Unity compilation, Play Mode, a test, or a build succeeded unless it was actually verified.
* Never hide compilation errors or unfinished work behind placeholder implementations.

## Design priority

When making technical decisions, use this order:

1. emotional purpose;
2. player experience;
3. gameplay clarity;
4. reliability;
5. maintainability;
6. performance where measurements justify it;
7. theoretical flexibility.

A finished and enjoyable system is more valuable than a theoretically perfect system that delays the playable game.

## Architecture principles

Prefer:

* explicit dependencies;
* focused MonoBehaviours;
* plain C# classes for non-Unity logic;
* ScriptableObjects for immutable definitions and authoring data;
* serializable runtime instances for unique mutable objects;
* stable string IDs or GUIDs for persistence;
* composition over deep inheritance;
* small public APIs;
* data-driven content when several content variants genuinely exist.

Avoid:

* universal service locators;
* a global `GameManager` containing unrelated systems;
* singletons for every subsystem;
* static mutable global state;
* universal event buses;
* excessive interfaces with one implementation;
* one Assembly Definition per folder;
* premature dependency injection frameworks;
* unnecessary third-party packages;
* reflection-heavy systems;
* speculative abstractions;
* excessive editor tooling before runtime gameplay works.

## Unity rules

* All project-owned assets belong under `Assets/Lootbound/`.
* Do not modify generated folders such as `Library`, `Temp`, `Logs`, or `Obj`.
* Preserve `.meta` files.
* Use namespaces beginning with `Lootbound`.
* Use `[SerializeField] private` for Inspector references rather than public mutable fields.
* Do not use `GameObject.Find` or repeated scene-wide object searches in runtime gameplay.
* Avoid allocations inside per-frame loops when a straightforward alternative exists.
* Do not optimize without evidence.
* Do not put core gameplay calculations directly inside UI components.
* Do not store mutable runtime state directly inside shared ScriptableObject assets.
* Keep editor-only code inside an `Editor` folder or an editor-only Assembly Definition.
* Keep runtime assemblies independent from editor assemblies.

## Workflow for every implementation

Before editing:

1. inspect the relevant existing files;
2. identify current dependencies and conventions;
3. check for compilation risks;
4. state a concise implementation plan;
5. mention any assumption that materially affects the design.

During implementation:

1. implement the smallest complete version;
2. preserve unrelated behaviour;
3. integrate with existing systems rather than duplicating them;
4. keep the project compiling after meaningful stages;
5. avoid placeholder architecture for unrequested future features.

After implementation:

1. inspect the complete diff;
2. remove dead code and accidental debug code;
3. check for missing references and namespace issues;
4. run available tests;
5. verify Unity compilation when technically possible;
6. clearly identify manual Unity Editor steps;
7. update documentation only when architecture or workflow changed.

## Required final report

At the end of a task, report:

* files created;
* files modified;
* important design decisions;
* validation actually performed;
* compilation or testing limitations;
* required Unity Editor actions;
* known remaining issues;
* the next logical step, without implementing it.

## Important project documentation

Read the relevant documentation before working:

* `.claude/rules/lootbound-vision.md`
* `.claude/rules/unity-development.md`
* `.claude/rules/workflow.md`

Documentation must reflect implemented reality. Do not describe planned systems as already implemented.
