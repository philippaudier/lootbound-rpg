# Procedural Circulation Engine — Glossary

> **Status**: official PCE vocabulary (PCE 0.1). Every PCE document and every
> future PCE type uses these words with exactly these meanings. If a future
> discussion needs a word that is not here, the word gets defined here first.
> `road`, `path`, `spline` and `flow` are NOT interchangeable in Lootbound.

## Core chain

### CirculationIntent
An abstract **reason** to move between two areas: Refuge toward the great
valley; village toward mine; herds toward water; an ancient city toward a
trade pass. An intent has endpoints as *areas or attractors*, an era, and a
mover kind — but **no route**. Intents are inputs, produced from the world
(terrain, attractors, history), never hand-authored per-path.

### Flow
The **pressure of movement** produced by one or more intents: a conceptual
origin, a destination or general direction, an intensity, a movement profile,
optionally an era, and a spatial footprint. A flow is **invisible** — it is
demand, not a line. Several intents can feed one flow; one intent can split
across flows.

### CirculationCorridor
A **band of territory** through which a flow can naturally travel — the PCE's
main structural output. A corridor is *not* a spline: it has an approximate
axis, a variable width, an intensity, a confidence, an age, a traffic level, a
family, a stable identifier and **deterministic continuity** (it continues
identically across chunk borders and across sessions). A corridor may carry
several visible traces, one, or none at all.

### VisibleTrace
The concrete **manifestation** of a corridor in the world: packed earth,
paving stones, short grass, displaced rocks, prints, thinned vegetation, an
almost-erased line. Traces are presentation-side, plural, and optional — a
corridor's logic never depends on its traces being rendered or even existing.

### CirculationProfile
The PCE-side **semantic bundle** for one kind of movement: its family
behaviour, its promise, how wide it travels, how it switchbacks — and the
`TraversalProfile` it perceives the terrain through. Examples: human major
road, mountain trail, animal movement, ancient trade route, military
passage. A CirculationProfile MEANS; the TraversalProfile it owns PERCEIVES.

### TraversalProfile
The terrain-perception parameters of one kind of mover — deliberately not
called "weights", because it is more than weighting: it is **a way of
perceiving the terrain** (slope tolerance, cliff penalty, roughness
sensitivity, fear of exposure, preferred cover…, growing over time). It
lives in Terrain Intelligence (`Lootbound.World`): the knowledge layer turns
a TraversalProfile into a `TerrainCostField`. The terrain itself is neutral —
cost only exists *through* a TraversalProfile (invariant 16).

### CirculationFamily
The functional **category** of a corridor, fixed at PCE 0.1 as:
`MajorRoad`, `SecondaryRoad`, `MountainTrail`, `AnimalTrail`,
`ForgottenPath`. A family bundles a profile, a promise, a rarity tier and a
visual language. Family is *functional*, never merely cosmetic.

## Supporting terms

### Attractor
A place that **generates or bends circulation**: the Refuge, a village, a
mine, a spring, a pass, a sanctuary. An attractor is not a destination of a
specific journey — it is a standing reason for journeys to exist. Some
important places are deliberately NOT attractors (a hidden cave attracts no
road; that is what keeps it hidden).

### Promise
The implicit **expectation** a circulation family creates in the player
(safety, curiosity, water, history…). Learned through play, communicated only
through the world's natural signs, and **never a guarantee** of reward.

### Terrain Cost Field
A deterministic field answering "how much does this mover dislike crossing
this exact ground?" — derived by Terrain Intelligence from the World Engine's
knowledge (slope, cliffs, roughness, water, landscape) **through a
TraversalProfile**, never from raw noise. Same terrain, different profiles,
different fields.

### Traffic
The **current frequentation** of a corridor: how much life uses it *today*.
Traffic is dynamic in concept (eras, decline) but deterministic per seed.

### Age
How long a corridor has existed structurally. Age shapes visual language
(sunken lanes, pavement, memorials) and history states.

### Existence / Visibility (with Traffic, the three axes)
- **Existence** — the corridor is structurally there (the PCE asserts it).
- **Traffic** — how used it currently is.
- **Visibility** — how legible its traces currently are.
An old road can be: existence *high*, traffic *near zero*, visibility
*fragmentary*. These three axes are independent by design.

### History state
A conceptual lifecycle for corridors: `Active` → `Declining` → `Abandoned` →
`Forgotten`. States move along the three axes above; a `Forgotten` corridor
keeps full structural continuity while its visibility becomes intermittent.

### Influence Request
The only way the PCE touches the world: a **request** (soften this micro
relief, clear this vegetation, wear this surface) submitted to the World
Engine's terrain systems, which remain sovereign and may refuse.

### Territorial Intelligence
The knowledge layer between Terrain Intelligence and the PCE (PCE 0.4). It
answers "what geographic logic governs this place?" with MEASURES, never
names (invariant 19): territories emerge, are never painted, and never have
exact boundaries (invariant 20).

### TerritorialIdentity
The measures Territorial Intelligence knows about a place — currently
`Accessibility`, `Isolation`, `Connectivity`, all 0..1 and
perception-relative (measured through a cost view built from one
`TraversalProfile`). Deliberately no enum, no label, no name.

### Accessibility
Territorial measure: how cheaply a perception moves AROUND a place (mean
outward cost against ideal ground). 1 = ideal ground everywhere nearby.

### Isolation
Territorial measure: how costly the EASIEST way out of a place is —
geographic enclosure, relative to a perception. 1 = even the best exit is
expensive. Once corridors exist, circulation-level isolation queries
(`SampleIsolation`) will ENRICH this geographic measure with distance from
used corridors — same word, one meaning, one refinement. A signal for other
systems, never an absolute artistic truth.

### Connectivity
Territorial measure: how many distinct easy directions leave a place (open
rays over total). High on plains and crossroads, low in dead-end pockets.

### Curiosity
A derived metric estimating how *inviting* a point or branch is: partial
visibility, a fork, age, relief variation, low traffic, plausible proximity
of an attractor, contrast with the current corridor. Same caveat as
isolation: derived, tunable, never authored per-spot.

### Territory Memory *(named early — future concept)*
The territory's own story: the future layer where the history model grows
into chained, deterministic events — a corridor used for seven centuries, a
bridge built, a village born of the bridge, the bridge lost, the village
abandoned, a forgotten trail. History as a layer ON TOP of geography, letting
the world evolve without breaking determinism. Expected to emerge around
PCE 0.11–0.15 and mature after 1.0; named now so the word is already unique
when it arrives.

## Distinctions that must never blur

| This… | …is not that | Because |
|---|---|---|
| Visible path (trace) | Corridor | The trace is paint and props; the corridor is the logic. A corridor can be invisible; a trace can be fragmentary while the corridor is continuous. |
| Flow | Corridor | Flow is demand (pressure); the corridor is where the land lets that demand pass. |
| Attractor | Destination | An attractor is a standing cause of journeys; a destination belongs to one abstract intent. |
| Existence | Traffic / Visibility | A road can exist, unused and unseen. All three are independent axes. |
| Family (functional) | Appearance (visual) | Family drives behaviour and promise; appearance is one *rendering* of it and may vary along a single corridor. |
| Circulation | Layout / content | The PCE feeds and reads other systems; it does not own villages, loot or quests. |

## Related Documentation

- `PCE_DESIGN_BIBLE.md` — why these words matter.
- `PCE_INVARIANTS.md` — the rules built on this vocabulary.
- `PCE_ARCHITECTURE.md` — how the concepts connect.
