# Boss

The brute plus a crown of bone and dorsal spines - the only archetype that breaks the skyline.

Generated, not modelled. Source: [`ArtSource/Enemies/build_enemies.py`](../../../../../ArtSource/Enemies/build_enemies.py),
function `Boss()`. Re-export with:

```bash
blender --background --python ArtSource/Enemies/build_enemies.py -- Boss
```

## Files

- `Boss.fbx` — Unity import. Carries **no animation clips**; it borrows the
  normal zombie's, which is only possible because the whole roster shares one
  skeleton. See [the kit README](../../../../../ArtSource/Enemies/README.md).
- `Boss_Palette.png` — the sixteen-cell palette texture. The mesh has a single
  material that samples it, so each enemy is one draw call.

## Budget

| | |
|---|---|
| Vertices | 1470 |
| Triangles | 2736 |
| Bones | 18 |
| Materials | 1 |
| Height | 2.1 m |
| Half-width at the chest | 0.64 m |

Half-width matters: the hit zone authored in `ArenaBuilder.AddLimbHitbox` is sized
to it, and the walking CharacterController is narrower than both.
