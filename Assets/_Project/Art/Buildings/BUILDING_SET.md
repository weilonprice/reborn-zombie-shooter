# Arena building set

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

## The four arenas

See [`ArtSource/Buildings/ArenaLayouts.png`](../../../../ArtSource/Buildings/ArenaLayouts.png)
for a top-down map of all four.

| | |
|---|---|
| **The Yard** | 7 buildings scattered with long anchors. The balanced baseline. |
| **The Corridors** | 8 in four north-south lanes with an open central corridor. Sightlines run one way and not the other. |
| **The Ring** | 8 — six in a band at radius 22, two anchors outside it. You fight in a donut. |
| **The Warren** | 8 on a jittered 3×3 grid at 20 m spacing, middle cell empty. Streets, not a maze. |

Thinned from 55 placements to 31. Two of them needed more than deletion to survive
the cut: at half the count The Ring stopped reading as a ring and The Warren
stopped reading as anything, so both were **pulled tighter** rather than merely
emptied — radius 27 → 22, grid spacing 28 → 20 m. Fewer buildings closer together
keeps the shape; fewer buildings equally spread just removes it.

Positions were **checked geometrically, not by eye**: no two buildings within two
metres, nothing overlapping the player spawn, nothing crossing the arena wall. The
first pass of these had four such faults, including two buildings sitting on the
spawn point.

Tall buildings stay in the corners. The camera looks down at 61°, so a building of
height *h* hides roughly 0.55*h* of ground behind it — and the apartment block is
10.8 m. In a corner, most of what it hides is outside the arena.

## Budget

136k–192k triangles of static geometry per arena, and only one arena is active at a
time. Static, so it batches; worth re-checking if the target ever moves off desktop.
