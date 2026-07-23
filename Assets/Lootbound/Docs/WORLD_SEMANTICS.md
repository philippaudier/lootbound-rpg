# World Engine — World Semantics

> **Status**: founding manifesto of the World Engine (milestone WE 0.1). This
> is philosophy, not specification: no API, no class, no pseudo-code — the
> architecture's constitution, meant to still be true in ten years. Its
> concrete companion is `WORLD_ENGINE_ARCHITECTURE.md`; the third family of
> knowledge it introduces (Singular / World Features) is deferred to WE 0.2.
>
> **Book I.** With Terrain Intelligence, the Terrain Cost System and
> Territorial Intelligence, this manifesto closes the first arc of Lootbound:
> *the world learning to know itself.* WE 0.2 (World Features) is its final
> chapter. Only then does Book II — *Emergence* — begin.
>
> **Constitutional.** This document has authority over the code, not the
> reverse. Before any major milestone, the first question is not "does it
> compile?" or "is it fast?" but **"does this idea respect World Semantics?"**
> An idea that violates a founding invariant is wrong even if it works. When a
> new question appears years from now, the answer is sought *here* first.

## Mantra

> *Le monde n'existe pas pour le joueur.*
> *Le joueur découvre un monde qui existait déjà.*
>
> The world does not exist for the player.
> The player discovers a world that already existed.

> *Les systèmes ne créent pas la vérité du monde.*
> *Ils la découvrent.*
>
> Systems do not create the world's truth. They discover it.

## 1. A reference reality, not a generator

It is tempting to describe the World Engine as "the thing that makes the
terrain". That description is not merely incomplete — it is the wrong shape,
and building on it would eventually corrupt the whole project.

The World Engine's role is to produce a **coherent world whose meaning
several independent systems can discover without ever talking to each
other.** It is not a factory of content. It is a **reference reality**: the
one description of the world that every system agrees to read, and that none
is allowed to author.

The consequence is strict and liberating at once: **gameplay never owns its
own representation of the world.** Wildlife, circulation, weather, quests,
ambience — none of them keeps a private map, a private notion of what is high
or wet or hard. They all read the same reality and reach their own
conclusions. There is one world, and everyone is looking at it.

## 2. From shape to shareable knowledge

A heightfield is not a world. Slope, moisture, material, the presence of
water — these are geometry. Geometry is *shape*. Shape carries no meaning yet.

To say "the ground here rises at 37°" is a fact. To say "this is hard to
climb", "this is a defensible ridge", "this is where water will never pool",
"this is a place a road would refuse" — those are **interpretations**, and
each one belongs to whoever is asking, not to the slope itself.

World Semantics is the study of how a world moves from *shape* to *shareable
knowledge*: how raw geometry becomes something many systems can read, trust,
and interpret — each in its own way — without any of them having invented it.

## 3. Shared Truth — the heart of the engine

This is the single idea the entire architecture protects.

**Every system reads exactly the same truth. They may draw different
conclusions from it. They never redefine it.**

Consider one fact the world holds at a point: *there is water here.*

- Wildlife reads it as **a reason to come** — animals go to water.
- Circulation reads it as **a cost and an obstacle** — a river to ford or
  bridge or avoid.
- Weather reads it as **a source of humidity and morning fog**.
- Ambience reads it as **a sound** to carry on the wind.
- A future quest reads it as **a landmark to describe**.

Five systems, five conclusions, one truth — and not one of them asked the
world to add "water" on its behalf. The water was already there. Had any
system been allowed to invent its own water, the five would silently
disagree, and the world would quietly stop being real. Shared Truth is what
keeps a world honest when no one is coordinating it.

Two properties make Shared Truth possible, and both are non-negotiable:

- **It is deterministic.** The same world always yields the same knowledge
  from the same place. Two systems asking at two different moments, on two
  different machines, receive the same answer — that is what makes it
  *shared* rather than merely *available*.
- **It is discovered, not stored.** The world is a function of its seed, not
  a database of authored facts. Knowledge is recomputed wherever it is
  needed; what a chunk paints or builds is a disposable view of that truth,
  never its source. Destroy the view and the truth is untouched.

## 4. The world never lies

Shared Truth is not only shared — it is **honest**. The World Engine never
fabricates knowledge for convenience.

When weather asks *"where does fog form?"*, the engine never answers *"here,
because it looks good."* It answers *"here, because the world's own
properties — a cold, still, humid hollow — lead to it."* Every fact the world
holds has a reason **inside the world**. Convenience, drama, pacing, the
player's mood — none of these may ever be the cause of a truth.

A world that lies once, even kindly, can no longer be trusted by the systems
that read it, and Shared Truth collapses into a set of private conveniences.
The world is allowed to be dull, empty, or unfair. It is never allowed to be
false.

## 5. Knowledge has no owner

Terrain Cost does not belong to the PCE. Accessibility does not belong to the
PCE. Tomorrow, humidity will not belong to Weather. They belong to the world,
and the system that reads a fact *first* has no more claim on it than the
tenth system to arrive.

This is why there is no `AnimalKnowledge`, no `WeatherKnowledge`, no
`RoadKnowledge`. Such a name fences a piece of shared truth behind one
interpreter; the next system is then forced to re-derive or duplicate it, and
the one world fractures into many private ones.

The test is simple: **knowledge is named for what it is about the world**
(height, cost, accessibility, moisture) — **never for who uses it.** If a fact
can only be described in terms of one system, it is not world knowledge. It
is that system's private conclusion, and it belongs *in that system*, not in
the world.

## 6. Systems never create knowledge

No system ever says to the World Engine: *"add me a WolfField."* That request
does not exist, and the engine could not honour it without ceasing to be a
reference reality.

A system asks a different question — **"what does the world already know?"** —
and then decides, alone, what that means for it. Wolves are a decision the
wildlife system makes from world knowledge (where is it sheltered, watered,
isolated); they are not a fact the world was asked to hold.

So the World Engine knows nothing of wolves, roads, quests, villages,
merchants, or the player. It knows the world. Everything else is
interpretation, and interpretation lives in the systems, never in the truth.
A truth that knew about wolves would no longer be a truth about the *world* —
it would be a truth about a *game*, and the next system would need its own.

## 7. The three families of knowledge

World knowledge is not uniform. A first, durable classification:

| Family | Answers | Character | Today |
|---|---|---|---|
| **Continuous** | *"What is the world like here?"* | a value at every point | Height, Slope, Curvature, Moisture, Terrain Cost… — largely built |
| **Regional** | *"What logic governs this part of the world?"* | a character over an area, by degree, with fuzzy edges | Territorial Intelligence (Accessibility, Isolation, Connectivity) — just born |
| **Singular** | *"What remarkable places exist in this world?"* | rare, located, discrete | **World Features — deferred to WE 0.2** |

Continuous knowledge is the ground floor: everywhere, read constantly.
Regional knowledge understands *neighbourhoods* rather than points — it never
classifies a place into one box; it describes the mix of logics present,
always by degree, because a territory never has an exact boundary. Singular
knowledge is different in kind: not "how is it here?" but "where are the
places that matter?" — the peaks, the springs, the lone ancient things. WE
0.1 only names this third family; WE 0.2 will define it.

These families are consumed, never confused: a system may read all three, but
each answers its own question, and none is derived by flattening another.

## 8. The hierarchy — the player arrives last

The project's conceptual progression, from the most objective to the most
interpreted:

```text
Geometry      the raw shape of the world
   ↓
Observation   measuring the shape (slope, curvature, flow…)
   ↓
Knowledge     shareable facts derived from observation
   ↓
Semantics     the possibilities of meaning those facts carry
   ↓
Simulation    the world changing over time (weather, life)
   ↓
History       the accumulated memory of that change
   ↓
Gameplay      systems and, last of all, the player
```

Each layer depends **only** on the layers beneath it, never above. Semantics
may read Knowledge; Knowledge never reaches up to Semantics. This is not a
stylistic preference — it is the shape that lets the lower layers stay true
regardless of what is built on top, and lets new systems be added without
disturbing the world they read.

Note the layer is **Semantics, not Meaning.** The world does not produce
meaning — meaning needs a reader. It produces *possibilities* of meaning: a
spring is a semantic potential that a deer completes as *drink*, a merchant as
*rest*, a hermit as *home*, the player as *landmark*. The world offers; the
reader means.

The player is deliberately at the end of the list. The world is not arranged
for them, does not react to their appetite, and does not begin when they
arrive. They walk into a place that already made sense before them and will
go on making sense after. A world built *for* the player stops being a world
and becomes a service; a world built *to be true* can be discovered — and only
a world that can be discovered is worth returning from.

## 9. Discovery — the universal operation

Beneath everything written here is a single verb.

Circulation **discovers** where the land wants to be crossed. Weather
**discovers** where the air will still. Wildlife **discovers** where life can
hold. History **discovers** what the world has become. And the player
**discovers** all of it, by walking.

Discovery is the universal operation. Every system — and the player among
them — does the same thing: it questions the world and interprets the answer.
No one commands the world; everyone interrogates it. This is the deepest
symmetry of the engine, deeper even than Shared Truth, which it makes
possible: **the player is not privileged above the systems, only the last and
most human of them.**

The world exists. Everything else — every system, every creature, every
traveller — discovers it.

## 10. Founding invariants

1. **The World Engine is the single source of truth about the world.** No
   system keeps a private world model.
2. **Knowledge is never created to satisfy a particular system.** The world
   knows the world; systems interpret.
3. **Systems consume the world's knowledge; they never redefine it.**
   Different conclusions, one truth.
4. **Knowledge has no owner.** It is named for what it is about the world,
   never for who uses it — no `AnimalKnowledge`, no `WeatherKnowledge`.
5. **The world never fabricates knowledge for convenience.** Every fact has a
   cause inside the world; drama, pacing and the player are never that cause.
6. **Each layer depends only on the layers below it.** Truth never reaches up
   to interpretation.
7. **The meaning of a place is a property of the world, never of the
   player.** It derives from the world's own structure — its water, its
   passes, its distance from a place that matters — not from who is looking.
8. **Truth is deterministic.** The same seed and coordinate yield the same
   knowledge, always and everywhere — the precondition of anything being
   *shared*.
9. **Truth is a function, not a database.** Knowledge is recomputed where
   needed; a loaded view is disposable and authoritative of nothing.
10. **The world describes; it never judges.** It states the fact (37°, water,
    a saddle); calling it steep, dangerous or inviting is interpretation, and
    interpretation belongs to the systems above. (The PCE's Neutrality
    Principle inherits this.)

## What a new developer should leave with

Lootbound is not trying to generate content. It is trying to build a world
that possesses a common truth — one reality that many independent systems can
read, trust, and interpret differently, without ever coordinating and without
ever redefining it. The systems do not create that truth. **They discover
it.** And so, last of all, does the player.

**A coherent world is not one where every system agrees. It is one where
every system can disagree while observing the same reality.** The wolf calls
the ravine an excellent home; the merchant calls it a horrible road; both are
right, because both are reading the same truth. Coherence is not consensus —
it is a reality rich enough to be read in many ways at once.

## Related Documentation

- `WORLD_ENGINE_ARCHITECTURE.md` — the concrete architecture this grounds.
- `PCE_DESIGN_BIBLE.md` — a consumer built entirely on Shared Truth; its
  "one world, many perceptions" is invariant 10 in practice.
- `PCE_TERRITORIAL_INTELLIGENCE.md` — the first Regional knowledge.
- `WORLD_FEATURES.md` — the Singular family (WE 0.2), the last chapter of
  Book I.
