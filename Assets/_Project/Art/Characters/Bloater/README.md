# Bloater

A sphere with limbs. The silhouette is the counterplay: do not be near it when it dies.

Generated, not modelled. Source: [`ArtSource/Enemies/build_enemies.py`](../../../../../ArtSource/Enemies/build_enemies.py),
function `Bloater()`. Re-export with:

```bash
blender --background --python ArtSource/Enemies/build_enemies.py -- Bloater
```

## Files

- `Bloater.fbx` — Unity import. Carries **no animation clips**; it borrows the
  normal zombie's, which is only possible because the whole roster shares one
  skeleton. See [the kit README](../../../../../ArtSource/Enemies/README.md).
- `Bloater_Palette.png` — the sixteen-cell palette texture. The mesh has a single
  material that samples it, so each enemy is one draw call.

## Budget

| | |
|---|---|
| Vertices | 1096 |
| Triangles | 2032 |
| Bones | 18 |
| Materials | 1 |
| Height | 2.065 m |
| Half-width at the chest | 0.606 m |

Half-width matters: the hit zone authored in `ArenaBuilder.AddLimbHitbox` is sized
to it, and the walking CharacterController is narrower than both.
