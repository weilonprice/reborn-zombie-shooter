# RangedZombie

Asymmetric: one overgrown throwing arm and a satchel of what it throws.

Generated, not modelled. Source: [`ArtSource/Enemies/build_enemies.py`](../../../../../ArtSource/Enemies/build_enemies.py),
function `RangedZombie()`. Re-export with:

```bash
blender --background --python ArtSource/Enemies/build_enemies.py -- RangedZombie
```

## Files

- `RangedZombie.fbx` — Unity import. Carries **no animation clips**; it borrows the
  normal zombie's, which is only possible because the whole roster shares one
  skeleton. See [the kit README](../../../../../ArtSource/Enemies/README.md).
- `RangedZombie_Palette.png` — the sixteen-cell palette texture. The mesh has a single
  material that samples it, so each enemy is one draw call.

## Budget

| | |
|---|---|
| Vertices | 980 |
| Triangles | 1804 |
| Bones | 18 |
| Materials | 1 |
| Height | 2.011 m |
| Half-width at the chest | 0.61 m |

Half-width matters: the hit zone authored in `ArenaBuilder.AddLimbHitbox` is sized
to it, and the walking CharacterController is narrower than both.
