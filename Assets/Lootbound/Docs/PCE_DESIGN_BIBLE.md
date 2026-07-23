# Procedural Circulation Engine — Design Bible

> **Status**: founding document of the PCE (milestone PCE 0.1). This is a design
> lock, not a description of what exists — nothing in this document is
> implemented yet. Its companions are `PCE_ARCHITECTURE.md`, `PCE_GLOSSARY.md`,
> `PCE_INVARIANTS.md` and `PCE_ROADMAP.md`. Every term used here is defined
> once, in the glossary.

## Mantra

> *Le monde n'est pas parcouru parce qu'il possède des chemins.*
> *Il possède des chemins parce que la vie l'a parcouru.*
>
> The world is not travelled because it has paths.
> It has paths because life has travelled it.

> *Un joueur ne suit jamais un chemin parce qu'il existe.*
> *Il le suit parce qu'il promet quelque chose.*
>
> A player never follows a path because it exists.
> They follow it because it promises something.

## The chain of meaning

> **La connaissance calcule ; le PCE signifie.**
> Knowledge computes; the PCE means.

The World Engine says: *here is the world.*
A `TraversalProfile` says: *here is how I perceive it.*
The PCE says: *then here are the paths that make the most sense.*

Roads are never generated arbitrarily. Circulation **emerges from a
perception of the territory** — and the same chain serves humans, animals,
caravans, and systems not yet imagined.

## 1. Why this system exists

Most procedural games generate **terrain**: a heightfield, biomes, scattered
content. Very few generate a **territory**: land that appears to have been
lived in, crossed, worked, abandoned and reclaimed. Minecraft generates
mountains; it does not generate the pass a merchant would have used to cross
them, nor the shortcut a shepherd wore into the slope, nor the overgrown
roadbed that proves someone lived beyond it.

Lootbound's core promise is that **ordinary moments are the most precious**
and that a safe return is a victory. That promise needs a world the player can
*read*: where distance from the Refuge is felt through the land itself, where
a faint line in the grass is information, and where choosing between the wide
road and the animal trail is a real decision with real texture.

The Procedural Circulation Engine exists to give the world that memory. It
does not draw roads. It models the *reasons* things moved — humans, animals,
history — and lets paths emerge as the **consequence** of that movement:

```text
CirculationIntent  →  Flow  →  CirculationCorridor  →  VisibleTrace  →  world & gameplay influence
```

If the World Engine is Lootbound's physical heart, the PCE is its circulatory
system. They are peers, with their own architecture, documentation and pace.

Historically, this system is also the answer to a measured failure: the world
layout solver produced poor routes because it had **no geographic knowledge**
(see the "4096 bug" post-mortem in `WORLD_ENGINE_ARCHITECTURE.md`). The World
Knowledge layer (slope, hydrology, traversability, landscapes) was built as
the cure; the PCE is the system that finally consumes it.

## 2. Choosing a path IS gameplay

The PCE's real output is not geometry. It is a **decision surface**:

- Take the major road: faster, safer, legible — and predictable.
- Take the mountain trail: slower, exposed — and it promises a view, a pass,
  a place few reach.
- Follow the animal trail: it will not care about your comfort — but animals
  go to water, to food, to shelter.
- Notice the forgotten path: half-erased, reclaimed by moss — someone had a
  reason to come through here, once.
- Leave every path: always possible, always legitimate. The wild is not a
  wall; it is simply unassisted.

Every step of an expedition should quietly ask: *which line do you trust?*

## 3. The promise system

Each circulation family teaches the player an implicit **Promise** — an
expectation, learned through repetition, never a contract:

| Family | Promise |
|---|---|
| `MajorRoad` | relative safety, human activity, regional connection, easy movement |
| `SecondaryRoad` | an inhabited place or useful structure at a reasonable detour |
| `MountainTrail` | an interesting crossing, height, views, isolation |
| `AnimalTrail` | water, fauna, food, natural places |
| `ForgottenPath` | the past, mystery, the *possibility* of a forgotten place |

Promises are the vocabulary; the terrain writes the sentences. The player is
never told "this is a SecondaryRoad" by the UI — they learn it from width,
surface, curvature, upkeep, vegetation, sound and traffic (invariant:
*natural readability*).

**A promise is never a guarantee.** This is a design pillar, not a caveat.

## 4. Silences and false mysteries

A world where every path pays out becomes a slot machine, and paths become
rails with extra steps. The PCE must deliberately produce:

- **Silences** — trails that lead to a panorama, a clearing, a quiet dead end
  under a cliff. Nothing to loot. Something to remember.
- **False mysteries** — a fork that rejoins later; an old path whose
  destination has simply ceased to exist; parallel animal traces that braid
  and dissolve. The world does not owe an explanation for every line.
- **Real mysteries, rarely** — because silences exist, the cabin at the end
  of an overgrown path lands with force.

The ratio is a tuning question for later milestones; the *existence* of
silences is non-negotiable (see `PCE_INVARIANTS.md`, invariant 9).

## 5. Moments Lootbound

These vignettes are the acceptance test that numbers cannot express. If the
finished PCE cannot produce moments like these, it has failed regardless of
its metrics.

**The hesitation.** You are on the secondary road, dusk is coming, the Refuge
is far. A narrow trail forks left, climbing. You have durability for one
detour, maybe. You look at the trail, at the sky, and you choose. Nothing in
the UI moved. Everything in the game happened.

**The braid.** Following a stream, you notice flattened grass — an animal
trail braiding along the bank. You follow it; it splits, rejoins, and opens
onto a pool where the tracks multiply. You mark the place in your memory:
water, game, and a way back you now know.

**The pavement.** Cresting a ridge on a forgotten path, your boot catches on
something regular: cut stones under the moss, three in a row, then nothing.
The path is gone but the corridor continues — you can *feel* where the road
wanted to go — and beyond the col, half a wall stands in the bracken. Nobody
told you there was ever a town here. The stones did.

**The refusal.** A major road bends wide around a dark ravine. You could cut
through. The road, by existing, told you what travellers thought of that
ravine. You go around — or you don't, and now you know why they did.

**The return.** Coming home half-broken, you strike the great road at night.
You have never been on this stretch, yet you know exactly what it is, and what
it promises: the Refuge is at the end of something this worn. Relief, before
any interface confirms it.

## 6. What the PCE must never become

- A quest arrow made of dirt: circulation never encodes "the" way to play.
- A rail network: leaving a corridor is always possible and never punished by
  invisible design.
- A terrain editor: the terrain stays sovereign; the PCE adapts to it (a bad
  crossing may stay bad).
- A reward dispenser: see silences, above.
- A second world generator: it reads the world through the World Engine's
  knowledge and adds circulation — it does not absorb layout, landmarks or
  content generation.

## Related Documentation

- `PCE_GLOSSARY.md` — the official vocabulary (single source of definitions).
- `PCE_INVARIANTS.md` — the non-negotiable rules and violation examples.
- `PCE_ARCHITECTURE.md` — layers, data flow, streaming, future API.
- `PCE_ROADMAP.md` — milestones PCE 0.2 → 1.0.
- `WORLD_ENGINE_ARCHITECTURE.md` — the physical world the PCE reads.
