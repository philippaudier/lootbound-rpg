---
name: implement-slice
description: Plan and implement one bounded playable Lootbound development slice without expanding into future systems.
argument-hint: "[slice name and requirements]"
disable-model-invocation: true
---

Implement the following Lootbound slice:

$ARGUMENTS

## Required process

1. Read the root `CLAUDE.md`.
2. Read `.claude/rules/lootbound-vision.md`.
3. Read `.claude/rules/unity-development.md`.
4. Inspect all existing files relevant to the requested slice.
5. Summarize the current implementation.
6. Define:
   - the player-facing objective;
   - the exact V1 scope;
   - explicit exclusions;
   - dependencies;
   - validation criteria.
7. Implement the smallest complete vertical path first.
8. Keep unrelated systems unchanged.
9. Validate compilation and relevant runtime behaviour when possible.
10. Inspect the final diff for accidental scope expansion.

## Completion report

Report:

- what became playable;
- files created and modified;
- architecture introduced;
- tests and validation actually performed;
- manual Unity Editor setup;
- known limitations;
- what is deliberately postponed.

Stop after this slice. Do not implement the next one.