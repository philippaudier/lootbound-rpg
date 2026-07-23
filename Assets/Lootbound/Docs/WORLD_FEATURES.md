# World Engine — World Features

> **Status**: founding manifesto (milestone WE 0.2), the final chapter of
> Book I. Philosophy, not specification: no API, no class, no pseudo-code.
> It inherits the constitutional authority of `WORLD_SEMANTICS.md` — before
> anything is built on it, the first question is "does this respect World
> Semantics?". How singularities are actually detected is deliberately not
> here; that is a later, implementing milestone.
>
> **📖 Book I — Complete.** The world already knows how it is (Terrain
> Intelligence), how it is perceived (the Terrain Cost System), what logic
> governs its regions (Territorial Intelligence), and that all systems read
> one shared truth (World Semantics). This manifesto adds the last capacity:
> to recognize the singular places that compose it. Together with
> `WORLD_SEMANTICS.md`, these two documents are the World Engine's
> **constitution** — frozen, and with authority above the code; the rest of
> the project respects them. The world learning to know itself is now
> complete. Book II — *Emergence* — begins next.

## Mantra

> *Une World Feature n'est pas quelque chose que le moteur décide de créer.*
> *C'est quelque chose que le monde révèle de lui-même.*
>
> A World Feature is not something the engine decides to create.
> It is something the world reveals about itself.

> *Un point d'intérêt est placé pour attirer le joueur.*
> *Une singularité existe parce que le monde serait incomplet sans elle.*
>
> A point of interest is placed to attract the player.
> A singularity exists because the world would be incomplete without it.

## 1. What makes a place remarkable?

Do not begin with the idea of a "Feature". Begin with the older question:
**what makes a place remarkable?**

A mountain is not remarkable because it is a mountain. In a range of a
thousand peaks, no single peak is anything. A mountain becomes remarkable when
it stands **alone** — when it breaks the continuity of the plain around it. A
spring is not remarkable because it is water; it is remarkable because it is
the one point of water in a dry expanse. Remarkableness is never a property of
the object. It is a property of the **contrast** between the object and its
surroundings.

The world is mostly continuous — slope flows into slope, valley into valley,
forest into forest. A place becomes remarkable precisely where that continuity
**breaks**. The central concept of this document is therefore not the feature.
It is the **Singularity**.

## 2. Singularity — the central concept

A **Singularity** is a place where a pattern of the world is broken: a
discontinuity that the world's own knowledge reveals. Not "an important
object" — a break in continuity, made legible by everything Book I built.

An isolated peak is a break in elevation. A lone spring is a break in a dry
region's hydrology. A natural pass is a break in a wall of ridges. A perfectly
circular clearing is a break in the grain of a forest. A great solitary tree
is a break in scale. In each case the world did not *place* anything — it
simply failed to be uniform, and that failure is the singularity.

This reframes the whole subject. We are not cataloguing objects to scatter
across the map. We are asking the world where it ceases to repeat itself.

## 3. The world reveals, it does not place

The engine never decides *"I will put a feature here."* That sentence is
forbidden by everything in `WORLD_SEMANTICS.md`.

The order is inverted. **The world exists first.** Then the engine observes
that world — reads its elevation, its slope, its hydrology, its territorial
character — and *discovers* that certain places carry properties singular
enough to matter. The singularity was already there, latent in the geometry,
the moment the seed was chosen. Detection only makes it visible.

This is detection, never generation. A generated feature is content poured
into the world; a discovered singularity is a truth the world was always
holding. The first makes a map fuller; the second makes a world realer. Only
the second is allowed here — it is `WORLD_SEMANTICS.md` invariant "systems
never create knowledge", applied to places instead of fields.

## 4. A Singularity has no purpose

A World Feature never exists **for** anything.

Not for a quest. Not for the player. Not for a merchant, the PCE, the weather
or the animals. It exists because it is a remarkable property of the world,
and for no other reason. The lone peak would stand whether or not anyone ever
climbed it; the hidden spring would break the dry region whether or not any
creature ever drank there.

This is the exact mirror of `WORLD_SEMANTICS.md`'s *Knowledge Has No Owner*.
A singularity that existed *for* the PCE would be a road-planning fact, not a
world fact, and the next system would need its own. Purpose is added later, by
readers — never carried by the singularity itself. The world offers the
remarkable place; each system decides, alone, what it means to it.

## 5. The break in continuity

A singularity appears wherever a pattern is broken. The pattern may be
elevation, water, forest grain, ridge lines, openness, scale, age — any
continuity the world's knowledge can measure.

Illustrations, never a catalogue:

- an isolated mountain (a break in elevation)
- a single spring in a dry region (a break in hydrology)
- an immense solitary tree (a break in scale)
- a natural pass through a wall of ridges (a break in a barrier)
- a perfectly circular clearing (a break in a forest's grain)
- an exceptional waterfall (a break in a river's flow)
- a natural arch (a break in solid rock)
- a deep chasm (a break in the ground itself)

**This list is open and always will be.** It exists to convey the philosophy,
never to enumerate the world. A closed catalogue of feature types would be the
same mistake as a closed `enum` of territories: it would freeze the world into
what we thought of today. New kinds of break are recognized by the same
criterion — a genuine discontinuity — not by appearing on a list.

## 6. Families — origin, not gameplay

Singularities can be grouped by **where the break comes from**. This
classification describes origin only; it is never gameplay.

- **Natural** — the break is in the land itself: peaks, passes, springs,
  waterfalls, monumental trees, cliffs, chasms, arches.
- **Built** — the break is a trace of past life: ruins, bridges, villages,
  sanctuaries, towers, memorials.

The two poles are honest but not absolute — a village grown around a spring,
a shrine raised at a natural pass, are both at once, and that ambiguity is
itself true to the world. The family answers *"why is this place a break?"*;
it says nothing about what the place is *for* (that would be gameplay, and
gameplay does not belong here). Built singularities are also where Book I
meets the coming *Territory Memory* of the PCE: a ruined bridge is a
singularity today and a chapter of history tomorrow.

## 7. Properties, not types

A singularity is never understood through its *type*. "It is a waterfall"
tells a consumer almost nothing. It is understood through its **properties** —
measures and characters, exactly in the spirit of Territorial Intelligence:

- rare · isolated · dominant · sheltered · exposed · visible · hidden ·
  hard to reach · a point of convergence · a point of separation

A dominant, visible peak and a hidden, sheltered hollow are both
singularities, and their properties — not their labels — are what every
consumer reads. These properties describe **the world**: how the place sits
in its surroundings, never what a system should do with it. Two singularities
of the same "type" with opposite properties are opposite places; two of
different types with the same properties may serve a reader the same way. The
type is a convenience of description; the properties are the truth.

## 8. A Singularity is not a Point of Interest

This is a change of philosophy, not of vocabulary, and it is the soul of the
whole document.

Most engines speak of **Points of Interest**. A POI is *placed to attract the
player*: it is content authored so that someone will find it, a promise the
map makes to a person. Remove the player and the POI has no reason to exist.

A **Singularity** owes the player nothing. It exists because the world would
be **incomplete without it** — because a plain with no lone peak, a dry region
with no hidden spring, is a poorer, less true world. The player may discover
it. The player may walk past it forever and never know it was there. Both
outcomes are equally correct, because the singularity was never for them.

This is why silences are possible (`PCE_DESIGN_BIBLE.md`), why the world can
be honest (`WORLD_SEMANTICS.md`), and why discovery means something in
Lootbound: you are not collecting the map's promises, you are finding the
places a world already held, whether or not it expected you.

## 9. Every system reads them; none owns them

Like all world knowledge, singularities are shared truth. Every system reads
the same singular places and interprets them differently:

- the **PCE** may read a pass as an attractor, a reason for a corridor;
- **wildlife** may read a spring as a habitat, a reason to gather;
- **weather** may read a peak as an influence, where cloud and cold collect;
- **ambience** may read a chasm as a voice, a wind's low note;
- a future **quest** may read a ruin as a place worth describing;
- the **player** may read any of them as a memory — the peak they returned by.

No system owns a singularity. It belongs to the world; the readings belong to
the readers. This is `WORLD_SEMANTICS.md`'s Shared Truth, extended from
continuous fields to singular places.

## 10. Founding invariants

1. **A World Feature is discovered, never arbitrarily placed.** The world
   exists first; detection only reveals what was already there.
2. **A World Feature has no intrinsic purpose.** It exists because it is
   remarkable, not for a quest, a system or the player.
3. **A World Feature belongs to the world, never to a system.** Every system
   reads it; none owns it or defines it.
4. **A singularity is defined by its properties, never by its type or its
   use.** There is no closed catalogue of feature types.
5. **A singularity is a genuine break in the world's own patterns, never
   fabricated for effect.** It extends *the world never lies*: no feature
   exists for drama, pacing or the player's mood.
6. **World Features extend Shared Truth.** They are the Singular family of
   world knowledge, read by all, redefined by none.

## Conclusion — Book I is complete

With this manifesto the world holds the last of its self-knowledge. It now
knows:

- **how it is** — its relief, measured (Terrain Intelligence);
- **how it is perceived** — the same ground read by different movers
  (the Terrain Cost System);
- **what logic structures it** — the character of its regions
  (Territorial Intelligence);
- **that its truth is one and shared** (World Semantics);
- **which singular places compose it** — the breaks in its own continuity
  (World Features).

That is a world that understands itself. Nothing in Book I asked what should
*happen* in this world — only what the world *is*. That was deliberate:
phenomena built on an unknowing world are arbitrary; phenomena built on a
world that knows itself can be true.

Book II — **Emergence** — will not create new knowledge. It will let
phenomena — circulation, weather, life, history — *emerge* from the knowledge
Book I laid down. The world has learned to know itself. Now it will begin to
live.

## Related Documentation

- `WORLD_SEMANTICS.md` — the constitution this closes Book I beneath.
- `PCE_TERRITORIAL_INTELLIGENCE.md` — the Regional family; singularities are
  the Singular one.
- `PCE_DESIGN_BIBLE.md` — a first reader of singularities (attractors,
  Territory Memory).
