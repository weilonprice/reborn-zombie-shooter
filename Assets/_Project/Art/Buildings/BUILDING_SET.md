# Arena building set

The arena's cover footprints are filled with four authored low-poly buildings in a
worn survival-town palette:

- `Shack` — compact damaged shelter with boarded glazing and a pitched roof.
- `Storefront` — one-story shop with a projecting awning, sign panel, and display windows.
- `Warehouse` — long industrial cover piece with loading doors, vents, hazard stripe, and pitched roof.
- `ApartmentBlock` — two-story landmark with balconies, entry canopy, and repeated windows.

Generated, not modelled — source is
[`ArtSource/Buildings/build_buildings.py`](../../../../ArtSource/Buildings/build_buildings.py).

## How the builder places them

`ArenaBuilder.FillFootprint` tiles them along a footprint at roughly one building
per 9m, alternating yaw by 180 degrees on odd tiles so a run of them does not read
as one extruded block. `BuildingArtForFootprint` picks which model by scoring
candidates on how evenly they scale into the space, then varies by index.

Only about a fifth of the original greybox cover positions survive as buildings,
and each retained footprint is capped to a single building. The authored models
carry far more visual weight than the columns they replaced, and tiling every old
slab walled the lanes in.

## Colliders: one box each, measured

Each building gets **a single BoxCollider**, not per-part mesh colliders. The
apartment alone is 67 parts, and The Warren places dozens of buildings — per-part
colliders would mean well over a thousand non-convex meshes for the flow field's
8,100-cell bake and for every bullet to test against.

The box is **measured off the asset at build time**, from the vertices below 80%
of the model's height. That fraction is what separates wall from roof: eaves and
awnings overhang, and a player should be able to walk under them.

It used to be a blanket 88% of a hand-written dimensions table, and both halves
were wrong. The table had drifted — the shack was listed 7.13m tall against an
actual 6.25m — and the box was centred at x=0, z=0 when the apartment block's
mesh is centred 0.68m off its own origin. That put wall outside the collider on
one side and collider outside the wall on the other: one bug that showed up as
two, walking through buildings and hitting invisible walls beside them. The 88%
was also far too aggressive; the real overhang is 1–5%, not 12%.

Buildings and their renderers are marked static, cast and receive shadows.
