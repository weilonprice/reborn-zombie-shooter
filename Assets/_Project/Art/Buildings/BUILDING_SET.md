# Arena building set

> **NOT CURRENTLY IN THE GAME.** The map is an open Military Base with no
> buildings on it (2026-09-13). These models, their prefabs and
> `DetailedBuildingSetup` are all intact and nothing references them — the
> installer still works, but `ArenaBuilder` no longer places anything. Kept
> because the models are good and an unused folder is cheap. The layouts below
> are recorded for whoever brings cover back.


Four authored buildings, stylised low-poly with signage, corrugated siding and
window frames:

- `Shack` — 9.0 × 7.9 m, 5.6 m tall
- `Storefront` — 12.2 × 9.0 m, 5.4 m tall
- `Warehouse` — 16.6 × 10.5 m, 7.5 m tall
- `ApartmentBlock` — 10.3 × 9.7 m, **10.8 m tall**

Source in [`ArtSource/Buildings`](../../../../ArtSource/Buildings). Install with
**Tools ▸ Zombie Shooter ▸ Install Detailed Buildings**, which imports, builds URP
materials, fits a wall collider and writes the prefabs in
`Assets/_Project/Prefabs/Buildings`.

## They are placed, not fitted

`ArenaBuilder` instantiates the prefabs at **their authored size** — position and
yaw, nothing else.

That replaced a system which described cover as rectangles and squashed a building
non-uniformly to fill each one. That was right for four greybox blocks and wrong
the moment the models gained signage and corrugation: scaling x and z by different
amounts visibly distorts all of it. The layouts are now designed around the real
footprints instead.

The prefab also carries its own collider and static flags, so the builder no longer
measures wall bounds out of mesh vertices or rebuilds a collider every run. Those
decisions are settled once, at authoring time, where they can be inspected.

## The four arenas that used to use them

Recorded rather than live. `ArenaBuilder` had a `Placement` table — building,
position, yaw — and instantiated these prefabs at their authored size.

| | |
|---|---|
| **The Yard** | 7 scattered with long anchors — the balanced baseline |
| **The Corridors** | 8 in four north-south lanes around an open central corridor |
| **The Ring** | 8 — six in a band at radius 22, two anchors outside it |
| **The Warren** | 8 on a jittered 3×3 grid at 20 m spacing, middle cell empty |

Two things from building them that are worth not relearning:

**Check placements geometrically, not by eye.** The rule was no two buildings
within two metres, nothing overlapping the player spawn, nothing crossing the
arena wall. The first pass failed all three, including two buildings sitting on
the spawn point.

**Thinning a layout is not the same as shrinking it.** Cut by half, The Ring
stopped reading as a ring and The Warren stopped reading as anything — both had
to be pulled *tighter* (radius 27 → 22, spacing 28 → 20 m) rather than merely
emptied. Fewer buildings closer together keeps the shape; fewer buildings equally
spread removes it.

`ArtSource/Buildings/ArenaLayouts.png` is the top-down map of all four.

## Budget

136k–192k triangles of static geometry per arena when they were placed, one arena
active at a time. Static, so it batches; worth re-checking if cover returns and the
target ever moves off desktop.
