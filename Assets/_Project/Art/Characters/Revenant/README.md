# Revenant

Skeletal, half-wrapped in torn shroud bands, with ember sockets for eyes.

Generated, not modelled. Source: [`ArtSource/Enemies/build_enemies.py`](../../../../../ArtSource/Enemies/build_enemies.py),
function `Revenant()`. Re-export with:

```bash
blender --background --python ArtSource/Enemies/build_enemies.py -- Revenant
```

## Files

- `Revenant.fbx` — Unity import. Carries **no animation clips**; it borrows the
  normal zombie's, which is only possible because the whole roster shares one
  skeleton. See [the kit README](../../../../../ArtSource/Enemies/README.md).
- `Revenant_Palette.png` — the sixteen-cell palette texture. The mesh has a single
  material that samples it, so each enemy is one draw call.

## Budget

| | |
|---|---|
| Vertices | 1224 |
| Triangles | 2248 |
| Bones | 18 |
| Materials | 1 |
| Height | 2.019 m |
| Half-width at the chest | 0.589 m |

Half-width matters: the hit zone authored in `ArenaBuilder.AddLimbHitbox` is sized
to it, and the walking CharacterController is narrower than both.
