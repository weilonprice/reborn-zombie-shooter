# Arena building set

The greybox cover footprints now use four authored low-poly buildings with a worn survival-town palette:

- `Shack` — compact damaged shelter with boarded glazing and a pitched roof.
- `Storefront` — one-story shop with a projecting awning, sign panel, and display windows.
- `Warehouse` — long industrial cover piece with loading doors, vents, hazard stripe, and pitched roof.
- `ApartmentBlock` — two-story landmark with balconies, entry canopy, and repeated windows.

`ArenaBuilder` reuses and scales these models across every arena layout, including the corridor slabs and the dense Warren. It adds static mesh colliders to each imported mesh so the buildings provide real movement and line-of-sight cover.
