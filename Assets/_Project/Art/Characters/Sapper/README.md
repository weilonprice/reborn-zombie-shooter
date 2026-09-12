# Sapper

Carries its charge on its back, where the camera can see it.

Generated, not modelled. Source: [`ArtSource/Enemies/build_enemies.py`](../../../../../ArtSource/Enemies/build_enemies.py),
function `Sapper()`. Re-export with:

```bash
blender --background --python ArtSource/Enemies/build_enemies.py -- Sapper
```

## Files

- `Sapper.fbx` — Unity import. Carries **no animation clips**; it borrows the
  normal zombie's, which is only possible because the whole roster shares one
  skeleton. See [the kit README](../../../../../ArtSource/Enemies/README.md).
- `Sapper_Palette.png` — the sixteen-cell palette texture. The mesh has a single
  material that samples it, so each enemy is one draw call.

## Budget

| | |
|---|---|
| Vertices | 1022 |
| Triangles | 1888 |
| Bones | 18 |
| Materials | 1 |
| Height | 2.005 m |
| Half-width at the chest | 0.602 m |

Half-width matters: the hit zone authored in `ArenaBuilder.AddLimbHitbox` is sized
to it, and the walking CharacterController is narrower than both.
