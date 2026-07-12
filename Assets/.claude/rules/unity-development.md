# Lootbound — Unity 6 and URP development rules

## Supported environment

This project targets Unity 6 with the Universal Render Pipeline.

Always inspect the exact project version and installed package versions before using an API.

Do not assume that an API from an older Unity tutorial still applies.

Do not upgrade Unity or package versions unless explicitly requested.

## Project ownership

Project-owned content belongs under:

```text
Assets/Lootbound/
```

Do not move or rename third-party assets without a concrete reason.

Do not edit package cache contents.

Do not edit generated solution or project files as a source of truth.

## Runtime and editor separation

* Runtime code must not reference `UnityEditor`.
* Editor tools belong inside an `Editor` folder or editor-only Assembly Definition.
* Do not add editor-only code to player builds.
* Custom inspectors should improve a real workflow, not merely decorate the Inspector.
* Prefer validation attributes and `OnValidate` for simple authoring safeguards before creating a large custom editor.

## Data model

Use ScriptableObjects for:

* content definitions;
* designer-authored configuration;
* reusable immutable data;
* references to prefabs, sounds, icons, materials, and curves.

Do not use shared ScriptableObjects to hold mutable per-save runtime state.

Use plain serializable classes for:

* equipment instances;
* durability values;
* enhancement state;
* inventory entries;
* save data;
* procedural-world runtime state.

Use stable identifiers for saved references to definitions.

## MonoBehaviour responsibilities

A MonoBehaviour should mainly handle:

* Unity lifecycle;
* scene references;
* Unity components;
* collision and trigger callbacks;
* visual presentation;
* connecting runtime logic to Unity.

Move calculations and state transitions into plain C# classes when they do not require Unity lifecycle or scene objects.

Do not split a simple behaviour into many files merely to satisfy an architectural pattern.

## References and dependencies

Prefer, in this order:

1. serialized Inspector references;
2. references supplied during construction or initialization;
3. explicit local discovery during controlled initialization;
4. scene-wide searches only as a last resort.

Do not repeatedly call:

* `GameObject.Find`;
* `FindObjectOfType`;
* `FindFirstObjectByType`;
* `FindAnyObjectByType`;

inside gameplay loops.

Do not add global singletons merely to avoid assigning a reference.

## Update loops

* Do not add `Update` methods that perform no regular work.
* Centralize ticking only when measurements or scale justify it.
* Use `FixedUpdate` only for physics operations requiring the physics timestep.
* Use `LateUpdate` for camera follow or post-movement presentation when appropriate.
* Avoid LINQ and avoidable allocations in hot per-frame paths.
* Do not sacrifice readability for micro-optimization outside measured hot paths.

## Physics

* Keep physics layers and collision intent explicit.
* Avoid relying on default layer interactions without documenting their purpose.
* Prefer non-allocating physics queries only when query frequency makes allocations relevant.
* Clearly separate damage detection, physical collision, interaction detection, and environmental grounding.
* Validate behaviour at different frame rates and fixed timesteps when movement or combat timing is involved.

## Input

Use Unity's new Input System.

Gameplay components should consume semantic input such as movement, look, jump, or attack rather than directly checking keyboard keys.

Input actions should remain independent from the Character Controller implementation.

Support keyboard and mouse first, while avoiding designs that make future controller bindings unnecessarily difficult.

## URP

* Preserve the existing URP assets and renderer configuration unless the task explicitly requires changes.
* Do not create custom Renderer Features before ordinary lighting, materials, fog, volumes, and scene composition have been evaluated.
* Prefer standard URP workflows before custom shaders.
* Shaders must target the project's actual URP version.
* Avoid tessellation, compute-based terrain, custom render passes, or experimental GPU pipelines during early V1 slices unless explicitly requested.
* Document every required renderer or pipeline asset assignment.

## Procedural generation

Procedural generation must be deterministic when supplied with the same seed.

Avoid depending on global `UnityEngine.Random` state across unrelated systems.

Prefer explicit seeded random sources or isolated random streams.

Terrain generation must consider gameplay constraints such as:

* valid spawn area;
* traversable routes;
* slope limits;
* landmark visibility;
* point-of-interest access;
* refuge placement;
* region masks.

Do not treat noise output as finished world design.

## Scenes

* Keep bootstrap responsibilities separate from gameplay scene content.
* Avoid duplicated persistent objects.
* Document scene loading flow.
* Avoid hard-coded scene build indexes when stable scene identifiers or names are clearer.
* Validate that required scenes are included in the active Build Profile.
* Do not introduce additive world streaming before the game requires it.

## UI

* UI observes gameplay state and invokes explicit gameplay actions.
* UI must not own authoritative equipment, combat, inventory, or enhancement logic.
* Keep prototype UI visually simple.
* Do not spend substantial time on a UI framework before its gameplay workflow is stable.
* Ensure important gameplay information remains readable at the target resolution.

## Validation

When Unity is available, check:

* Console compilation errors;
* missing serialized references;
* invalid scene references;
* Assembly Definition references;
* Play Mode startup;
* relevant tests;
* behaviour after domain reload;
* build inclusion of scenes and required assets.

If Unity cannot be launched or compilation cannot be checked, state that explicitly.

Do not infer successful compilation only because the C# code looks valid.
