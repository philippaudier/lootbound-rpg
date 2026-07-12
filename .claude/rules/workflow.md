# Lootbound development workflow

For every requested slice:

1. Read `CLAUDE.md` and relevant rules.
2. Inspect existing implementation before proposing changes.
3. Define the exact V1 boundary.
4. Identify what is explicitly excluded.
5. Implement a thin end-to-end path first.
6. Compile and test after structural changes.
7. Test the system inside gameplay, not only in isolation.
8. Inspect the final diff.
9. Update documentation when necessary.
10. Stop when the requested slice is complete.

Do not begin the next slice automatically.

When encountering a major architectural ambiguity:
- prefer the simplest option compatible with known requirements;
- document the decision;
- avoid building both alternatives;
- do not create speculative extension points.

Never report a command, test, compilation, Play Mode session, or build as successful unless it actually ran successfully.