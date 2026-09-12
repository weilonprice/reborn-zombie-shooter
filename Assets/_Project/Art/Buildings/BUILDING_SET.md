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

## Colliders: one box each, deliberately

Each building gets **a single BoxCollider inset to 88% of its bounds**, not per-part
mesh colliders. The apartment alone is 67 parts, and The Warren places dozens of
buildings — per-part colliders would mean well over a thousand non-convex meshes
for the flow field's 8,100-cell bake and for every bullet to test against.

The inset is what makes one box honest: a tight box would block the eaves and
overhangs that a player can visually walk under.

Buildings and their renderers are marked static, cast and receive shadows.
